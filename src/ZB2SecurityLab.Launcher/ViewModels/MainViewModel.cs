using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Windows.Input;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Launcher.Domain;
using ZB2SecurityLab.Launcher.Services;
using ZB2SecurityLab.Launcher.Transactions;
using ZB2SecurityLab.Launcher.Worker;

namespace ZB2SecurityLab.Launcher.ViewModels;

internal sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly LocalDataPaths _dataPaths;
    private readonly JournalStore _journalStore;
    private readonly ISteamInstallationLocator _locator;
    private readonly IBuildValidator _validator;
    private readonly ISteamGameLauncher _steamLauncher;
    private readonly IGameProcessMonitor _processMonitor;
    private readonly WorkerClient _workerClient;
    private readonly LauncherAuditLog _audit;
    private SteamInstallation? _installation;
    private LauncherState _state = LauncherState.Detecting;
    private string _status = "Localizando a Steam e o Zumbi Blocks 2...";
    private string _gamePath = "Ainda não localizado";
    private string _steamPath = "Ainda não localizada";
    private string _buildStatus = "Não verificado";
    private string? _error;
    private bool _mutationEnabled;
    private bool _isBusy;
    private bool _runtimeEvidenceConfirmed;

    internal MainViewModel(
        LocalDataPaths? dataPaths = null,
        ISteamInstallationLocator? locator = null,
        IBuildValidator? validator = null,
        ISteamGameLauncher? steamLauncher = null,
        IGameProcessMonitor? processMonitor = null,
        WorkerClient? workerClient = null)
    {
        _dataPaths = dataPaths ?? new LocalDataPaths();
        _journalStore = new JournalStore(_dataPaths);
        _locator = locator ?? new SteamInstallationLocator(new RegistrySteamRootProvider());
        _validator = validator ?? new BuildValidator();
        _steamLauncher = steamLauncher ?? new SteamGameLauncher();
        _processMonitor = processMonitor ?? new GameProcessMonitor(new SystemProcessCatalog());
        _workerClient = workerClient ?? new WorkerClient();
        _audit = new LauncherAuditLog(_dataPaths);

        StartCommand = new AsyncCommand(StartAsync, () => CanLaunch);
        OpenLogsCommand = new RelayCommand(OpenLogs);
        RetryCommand = new AsyncCommand(InitializeAsync, () => !IsBusy);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand StartCommand { get; }
    public ICommand OpenLogsCommand { get; }
    public ICommand RetryCommand { get; }

    internal LauncherState State
    {
        get => _state;
        private set => Set(ref _state, value);
    }

    public string Status
    {
        get => _status;
        private set => Set(ref _status, value);
    }

    public string GamePath
    {
        get => _gamePath;
        private set => Set(ref _gamePath, value);
    }

    public string SteamPath
    {
        get => _steamPath;
        private set => Set(ref _steamPath, value);
    }

    public string BuildStatus
    {
        get => _buildStatus;
        private set => Set(ref _buildStatus, value);
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (Set(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    public bool MutationEnabled
    {
        get => _mutationEnabled;
        set => Set(ref _mutationEnabled, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanLaunch));
                RefreshCommands();
            }
        }
    }

    public bool CanLaunch => !IsBusy && State == LauncherState.Ready && _installation is not null;
    public string DataPath => _dataPaths.Root;

    internal async Task InitializeAsync()
    {
        IsBusy = true;
        Error = null;
        try
        {
            _dataPaths.EnsureCreated();
            _audit.Write("initialization_started", null);
            if (!await RecoverPendingTransactionsAsync().ConfigureAwait(true))
            {
                return;
            }

            State = LauncherState.Detecting;
            Status = "Localizando bibliotecas Steam...";
            var discovery = _locator.Locate();
            if (discovery.Installations.Count == 0)
            {
                Block(string.Join(Environment.NewLine, discovery.Errors.DefaultIfEmpty("Zumbi Blocks 2 não foi encontrado.")));
                return;
            }

            State = LauncherState.Validating;
            Status = "Validando manifesto e fingerprints...";
            var validationErrors = new List<string>();
            foreach (var candidate in discovery.Installations)
            {
                var validation = _validator.Validate(candidate);
                if (validation.IsSupported)
                {
                    _installation = candidate;
                    GamePath = candidate.GamePath;
                    SteamPath = candidate.SteamExecutablePath;
                    BuildStatus = $"Suportado: build {SupportedBuild.BuildId} / {SupportedBuild.Runtime}";
                    State = LauncherState.Ready;
                    Status = "Pronto para iniciar. Nenhum arquivo do jogo foi alterado.";
                    _audit.Write("build_ready", candidate.GamePath);
                    OnPropertyChanged(nameof(CanLaunch));
                    RefreshCommands();
                    return;
                }

                validationErrors.AddRange(validation.Errors.Select(error => $"{candidate.GamePath}: {error}"));
            }

            Block(string.Join(Environment.NewLine, validationErrors.Concat(discovery.Errors)));
        }
        catch (Exception exception)
        {
            Block($"{exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> RecoverPendingTransactionsAsync()
    {
        foreach (var journalPath in _journalStore.PendingJournalPaths())
        {
            LaunchSessionJournal journal;
            try
            {
                journal = _journalStore.Load(journalPath);
            }
            catch (Exception exception)
            {
                State = LauncherState.RecoveryRequired;
                Error = $"Journal de recuperação inválido: {journalPath}{Environment.NewLine}{exception.Message}";
                Status = "Recuperação manual necessária.";
                return false;
            }

            State = LauncherState.Cleaning;
            Status = $"Concluindo cleanup pendente da sessão {journal.SessionId}...";
            var result = await _workerClient.RecoverAsync(journalPath, journal.Nonce, HandleWorkerStatus, CancellationToken.None).ConfigureAwait(true);
            if (!result.Succeeded)
            {
                State = LauncherState.RecoveryRequired;
                Error = result.Error ?? "Não foi possível concluir o cleanup pendente.";
                Status = "Recuperação necessária antes de um novo launch.";
                return false;
            }
        }

        return true;
    }

    private async Task StartAsync()
    {
        if (_installation is null)
        {
            return;
        }

        IsBusy = true;
        Error = null;
        try
        {
            var validation = _validator.Validate(_installation);
            if (!validation.IsSupported)
            {
                Block(string.Join(Environment.NewLine, validation.Errors));
                return;
            }

            var executablePath = BuildValidator.ResolveGamePath(_installation.GamePath, SupportedBuild.ExecutableRelativePath);
            if (_processMonitor.IsGameRunning(executablePath))
            {
                Block("O Zumbi Blocks 2 já está em execução. Feche-o antes de usar o launcher.");
                return;
            }

            var sessionId = Guid.NewGuid().ToString("N");
            var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var journal = new LaunchSessionJournal
            {
                SessionId = sessionId,
                Nonce = nonce,
                GamePath = _installation.GamePath,
                ExecutablePath = executablePath,
                SteamExecutablePath = _installation.SteamExecutablePath,
                SteamRoot = _installation.SteamRoot,
                LibraryRoot = _installation.LibraryRoot,
                ManifestPath = _installation.ManifestPath,
                AppId = _installation.AppId,
                DataRoot = _dataPaths.Root,
                BuildId = _installation.BuildId,
                MutationEnabled = MutationEnabled,
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow,
                Phase = JournalPhase.Validated
            };
            var journalPath = _journalStore.Save(journal);
            _runtimeEvidenceConfirmed = false;
            _audit.Write("session_validated", sessionId);
            State = LauncherState.Preparing;
            Status = "Preparando componentes temporários...";

            var result = await _workerClient.RunSessionAsync(
                journalPath,
                nonce,
                async () =>
                {
                    State = LauncherState.LaunchRequested;
                    Status = "Solicitando inicialização à Steam...";
                    var requestedUtc = DateTimeOffset.UtcNow;
                    _steamLauncher.Launch(_installation);
                    MutationEnabled = false;
                    _audit.Write("steam_launch_requested", sessionId);
                    await Task.CompletedTask;
                    return requestedUtc;
                },
                HandleWorkerStatus,
                CancellationToken.None).ConfigureAwait(true);

            if (result.Succeeded)
            {
                State = LauncherState.Completed;
                Status = _runtimeEvidenceConfirmed
                    ? "Jogo encerrado e cleanup concluído. A instalação Steam está limpa."
                    : "Cleanup concluído, mas o carregamento do Security Lab não foi confirmado pelos logs.";
                BuildStatus = $"Suportado: build {SupportedBuild.BuildId}; fingerprints preservados";
                _audit.Write("session_completed", sessionId);
            }
            else if (result.RecoveryRequired)
            {
                State = LauncherState.RecoveryRequired;
                Status = "O jogo encerrou, mas o cleanup precisa ser retomado.";
                Error = result.Error;
                _audit.Write("session_recovery_required", result.Error);
            }
            else
            {
                Block(result.Error ?? "A sessão foi bloqueada pelo worker.");
            }
        }
        catch (System.ComponentModel.Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            State = LauncherState.Blocked;
            Status = "A elevação foi cancelada; nenhuma nova sessão foi iniciada.";
            Error = "A permissão administrativa necessária foi cancelada.";
        }
        catch (Exception exception)
        {
            State = LauncherState.RecoveryRequired;
            Status = "A sessão foi interrompida; o journal será recuperado na próxima execução.";
            Error = $"{exception.GetType().Name}: {exception.Message}";
            _audit.Write("session_failed", Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void HandleWorkerStatus(string code, string? detail)
    {
        switch (code)
        {
            case LauncherProtocol.Installed:
                State = LauncherState.Preparing;
                Status = "Componentes temporários instalados e verificados.";
                break;
            case LauncherProtocol.Running:
                State = LauncherState.Running;
                Status = $"Zumbi Blocks 2 em execução (PID {detail}). Aguardando o encerramento para cleanup.";
                break;
            case LauncherProtocol.LabLoaded:
                State = LauncherState.Running;
                _runtimeEvidenceConfirmed = true;
                Status = "Security Lab carregado. Pressione F8 dentro do jogo.";
                break;
            case LauncherProtocol.LabNotConfirmed:
                State = LauncherState.Running;
                Status = detail ?? "Security Lab ainda não foi confirmado pelos logs.";
                break;
            case LauncherProtocol.Cleaning:
                State = LauncherState.Cleaning;
                Status = "Removendo somente os componentes temporários desta sessão...";
                break;
            case LauncherProtocol.ElevationRequired:
                Status = detail ?? "Solicitando elevação...";
                break;
            case LauncherProtocol.Blocked:
                Error = detail;
                break;
            case LauncherProtocol.RecoveryRequired:
                Error = detail;
                break;
        }
    }

    private void OpenLogs()
    {
        _dataPaths.EnsureCreated();
        Process.Start(new ProcessStartInfo { FileName = _dataPaths.Logs, UseShellExecute = true });
    }

    private void Block(string message)
    {
        State = LauncherState.Blocked;
        Status = "Inicialização bloqueada; nenhum componente foi instalado.";
        Error = message;
        BuildStatus = "Bloqueado";
        _audit.Write("blocked", message);
        OnPropertyChanged(nameof(CanLaunch));
        RefreshCommands();
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private void RefreshCommands()
    {
        (StartCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RetryCommand as AsyncCommand)?.RaiseCanExecuteChanged();
    }
}
