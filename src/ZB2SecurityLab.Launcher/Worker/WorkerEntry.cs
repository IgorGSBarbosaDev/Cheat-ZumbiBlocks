using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using ZB2SecurityLab.Launcher.Domain;
using ZB2SecurityLab.Launcher.Services;
using ZB2SecurityLab.Launcher.Transactions;

namespace ZB2SecurityLab.Launcher.Worker;

internal static class WorkerEntry
{
    internal static bool IsWorker(IReadOnlyList<string> args)
        => args.Count > 0 && args[0] is LauncherProtocol.WorkerArgument or LauncherProtocol.RecoverArgument;

    internal static async Task<int> RunAsync(IReadOnlyList<string> args)
    {
        if (args.Count != 4)
        {
            return 2;
        }

        var mode = args[0];
        var journalPath = args[1];
        var pipeName = args[2];
        var nonce = args[3];

        await using var pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
        await pipe.ConnectAsync(30_000).ConfigureAwait(false);
        using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
        using var writer = new StreamWriter(pipe, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
        await writer.WriteLineAsync($"HELLO|{nonce}").ConfigureAwait(false);

        LaunchSessionJournal? activeJournal = null;
        JournalStore? activeStore = null;
        LocalDataPaths? activeDataPaths = null;
        IGameProcessMonitor? activeMonitor = null;
        ICleanupCoordinator? activeCleanup = null;
        try
        {
            var dataPaths = new LocalDataPaths();
            activeDataPaths = dataPaths;
            var canonicalJournalPath = Path.GetFullPath(journalPath);
            SteamInstallationLocator.EnsureContained(dataPaths.Journals, canonicalJournalPath);
            var store = new JournalStore(dataPaths);
            activeStore = store;
            var journal = store.Load(canonicalJournalPath);
            activeJournal = journal;
            if (!string.Equals(journal.Nonce, nonce, StringComparison.Ordinal) ||
                !string.Equals(Path.GetFullPath(journal.DataRoot), dataPaths.Root, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetFileNameWithoutExtension(canonicalJournalPath), journal.SessionId, StringComparison.Ordinal))
            {
                await SendAsync(writer, LauncherProtocol.Blocked, "Journal ou nonce inválido.").ConfigureAwait(false);
                return 3;
            }

            var monitor = new GameProcessMonitor(new SystemProcessCatalog());
            activeMonitor = monitor;
            var validator = new BuildValidator();
            var cleanup = new CleanupCoordinator(validator);
            activeCleanup = cleanup;

            if (mode == LauncherProtocol.RecoverArgument)
            {
                return await RecoverAsync(journal, store, dataPaths, monitor, cleanup, writer).ConfigureAwait(false);
            }

            SteamInstallation installation;
            PayloadPackage payload;
            try
            {
                installation = FindExactInstallation(journal);
                var validation = validator.Validate(installation);
                if (!validation.IsSupported)
                {
                    await SendAsync(writer, LauncherProtocol.Blocked, string.Join(" ", validation.Errors)).ConfigureAwait(false);
                    return 4;
                }

                if (monitor.IsGameRunning(journal.ExecutablePath))
                {
                    await SendAsync(writer, LauncherProtocol.Blocked, "O jogo já está em execução.").ConfigureAwait(false);
                    return 5;
                }

                payload = new EmbeddedPayloadProvider().Load(journal.MutationEnabled, dataPaths);
            }
            catch (Exception exception)
            {
                await SendAsync(writer, LauncherProtocol.Blocked, $"{exception.GetType().Name}: {exception.Message}").ConfigureAwait(false);
                return 4;
            }

            try
            {
                TransientInstaller.ProbeWriteAccess(journal.GamePath);
            }
            catch (UnauthorizedAccessException)
            {
                await SendAsync(writer, LauncherProtocol.ElevationRequired, null).ConfigureAwait(false);
                return 20;
            }
            catch (Exception exception)
            {
                await SendAsync(writer, LauncherProtocol.Blocked, $"Não foi possível validar acesso de escrita: {exception.Message}").ConfigureAwait(false);
                return 5;
            }

            var installer = new TransientInstaller();
            try
            {
                installer.Install(journal, payload, store);
            }
            catch (UnauthorizedAccessException)
            {
                var cleaned = cleanup.Cleanup(journal, store, dataPaths);
                await SendAsync(writer, cleaned ? LauncherProtocol.ElevationRequired : LauncherProtocol.RecoveryRequired, cleaned ? null : string.Join(" ", journal.Errors)).ConfigureAwait(false);
                return cleaned ? 20 : 6;
            }
            catch (Exception exception)
            {
                var cleaned = cleanup.Cleanup(journal, store, dataPaths);
                await SendAsync(writer, cleaned ? LauncherProtocol.Blocked : LauncherProtocol.RecoveryRequired, exception.Message).ConfigureAwait(false);
                return 6;
            }

            var refreshedInstallation = FindExactInstallation(journal);
            var postInstallValidation = validator.Validate(refreshedInstallation, checkLoaderConflicts: false);
            if (!postInstallValidation.IsSupported)
            {
                var cleaned = cleanup.Cleanup(journal, store, dataPaths);
                await SendAsync(writer, cleaned ? LauncherProtocol.Blocked : LauncherProtocol.RecoveryRequired, "O build mudou durante a preparação.").ConfigureAwait(false);
                return 7;
            }

            await SendAsync(writer, LauncherProtocol.Installed, null).ConfigureAwait(false);
            var command = await ReadCommandAsync(reader, TimeSpan.FromSeconds(30)).ConfigureAwait(false);
            if (command is null || !command.StartsWith("LAUNCH|", StringComparison.Ordinal) ||
                !DateTimeOffset.TryParse(command[7..], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var requestedUtc))
            {
                journal.Errors.Add(command?.StartsWith("ABORT|", StringComparison.Ordinal) == true ? command[6..] : "LAUNCH_NOT_CONFIRMED");
                var cleaned = cleanup.Cleanup(journal, store, dataPaths);
                await SendAsync(writer, cleaned ? LauncherProtocol.Blocked : LauncherProtocol.RecoveryRequired, "Launch cancelado; cleanup concluído.").ConfigureAwait(false);
                return 8;
            }

            journal.Phase = JournalPhase.LaunchRequested;
            store.Save(journal);
            var process = await monitor.WaitForStartAsync(journal.ExecutablePath, requestedUtc, TimeSpan.FromSeconds(120), CancellationToken.None).ConfigureAwait(false);
            if (process is null)
            {
                journal.Errors.Add("GAME_START_TIMEOUT");
                var cleaned = cleanup.Cleanup(journal, store, dataPaths);
                await SendAsync(writer, cleaned ? LauncherProtocol.Blocked : LauncherProtocol.RecoveryRequired, "O jogo não iniciou em 120 segundos; cleanup concluído.").ConfigureAwait(false);
                return 9;
            }

            journal.GameProcessId = process.Id;
            journal.Phase = JournalPhase.Running;
            store.Save(journal);
            await SendAsync(writer, LauncherProtocol.Running, process.Id.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);

            using var evidenceCancellation = new CancellationTokenSource();
            var evidence = new RuntimeEvidenceMonitor(dataPaths)
                .WaitForLabAsync(journal, TimeSpan.FromSeconds(30), evidenceCancellation.Token);
            var exit = monitor.WaitForExitAsync(process, CancellationToken.None);
            if (await Task.WhenAny(evidence, exit).ConfigureAwait(false) == evidence)
            {
                if (await evidence.ConfigureAwait(false))
                {
                    journal.RuntimeEvidenceConfirmed = true;
                    store.Save(journal);
                    await SendAsync(writer, LauncherProtocol.LabLoaded, null).ConfigureAwait(false);
                }
                else
                {
                    await SendAsync(writer, LauncherProtocol.LabNotConfirmed, "Security Lab não foi confirmado nos logs em 30 segundos.").ConfigureAwait(false);
                }
            }
            else
            {
                await SendAsync(writer, LauncherProtocol.LabNotConfirmed, "O jogo encerrou antes da confirmação do Security Lab.").ConfigureAwait(false);
            }

            await exit.ConfigureAwait(false);
            evidenceCancellation.Cancel();
            if (monitor.IsGameRunning(journal.ExecutablePath))
            {
                journal.Errors.Add("ANOTHER_GAME_PROCESS_IS_RUNNING");
                journal.Phase = JournalPhase.RecoveryRequired;
                store.Save(journal);
                await SendAsync(writer, LauncherProtocol.RecoveryRequired, "Outro processo do jogo ainda está ativo.").ConfigureAwait(false);
                return 10;
            }

            await SendAsync(writer, LauncherProtocol.Cleaning, null).ConfigureAwait(false);
            var succeeded = cleanup.Cleanup(journal, store, dataPaths);
            await SendAsync(writer, succeeded ? LauncherProtocol.Completed : LauncherProtocol.RecoveryRequired, succeeded ? null : string.Join(" ", journal.Errors)).ConfigureAwait(false);
            return succeeded ? 0 : 11;
        }
        catch (Exception exception)
        {
            var detail = $"{exception.GetType().Name}: {exception.Message}";
            var safe = false;
            if (activeJournal is not null && activeStore is not null && activeDataPaths is not null && activeMonitor is not null && activeCleanup is not null)
            {
                activeJournal.Errors.Add($"WORKER_FAILED:{detail}");
                try
                {
                    if (!activeMonitor.IsGameRunning(activeJournal.ExecutablePath))
                    {
                        safe = activeCleanup.Cleanup(activeJournal, activeStore, activeDataPaths);
                    }
                    else
                    {
                        activeJournal.Phase = JournalPhase.RecoveryRequired;
                        activeStore.Save(activeJournal);
                    }
                }
                catch (Exception cleanupException)
                {
                    activeJournal.Errors.Add($"EMERGENCY_CLEANUP_FAILED:{cleanupException.GetType().Name}:{cleanupException.Message}");
                    activeJournal.Phase = JournalPhase.RecoveryRequired;
                    try
                    {
                        activeStore.Save(activeJournal);
                    }
                    catch
                    {
                    }
                }
            }

            await SendAsync(writer, safe ? LauncherProtocol.Blocked : LauncherProtocol.RecoveryRequired, detail).ConfigureAwait(false);
            return safe ? 14 : 1;
        }
    }

    private static async Task<int> RecoverAsync(
        LaunchSessionJournal journal,
        JournalStore store,
        LocalDataPaths dataPaths,
        IGameProcessMonitor monitor,
        ICleanupCoordinator cleanup,
        StreamWriter writer)
    {
        if (monitor.IsGameRunning(journal.ExecutablePath))
        {
            await SendAsync(writer, LauncherProtocol.RecoveryRequired, "O jogo ainda está em execução; cleanup adiado.").ConfigureAwait(false);
            return 12;
        }

        var hasGameArtifacts = Directory.Exists(BuildValidator.ResolveGamePath(journal.GamePath, "BepInEx")) ||
                               journal.RootFileHashes.Keys.Any(relativePath => File.Exists(BuildValidator.ResolveGamePath(journal.GamePath, relativePath)));
        try
        {
            if (hasGameArtifacts)
            {
                TransientInstaller.ProbeWriteAccess(journal.GamePath);
            }
        }
        catch (UnauthorizedAccessException)
        {
            await SendAsync(writer, LauncherProtocol.ElevationRequired, null).ConfigureAwait(false);
            return 20;
        }

        await SendAsync(writer, LauncherProtocol.Cleaning, null).ConfigureAwait(false);
        var succeeded = cleanup.Cleanup(journal, store, dataPaths);
        await SendAsync(writer, succeeded ? LauncherProtocol.Completed : LauncherProtocol.RecoveryRequired, succeeded ? null : string.Join(" ", journal.Errors)).ConfigureAwait(false);
        return succeeded ? 0 : 13;
    }

    private static SteamInstallation FindExactInstallation(LaunchSessionJournal journal)
    {
        var discovery = new SteamInstallationLocator(new FixedSteamRootProvider(journal.SteamRoot)).Locate();
        return discovery.Installations.FirstOrDefault(candidate =>
                   string.Equals(Path.GetFullPath(candidate.GamePath), Path.GetFullPath(journal.GamePath), StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(Path.GetFullPath(candidate.ManifestPath), Path.GetFullPath(journal.ManifestPath), StringComparison.OrdinalIgnoreCase))
               ?? throw new InvalidOperationException("A instalação Steam registrada não foi reencontrada.");
    }

    private static async Task<string?> ReadCommandAsync(StreamReader reader, TimeSpan timeout)
    {
        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            return await reader.ReadLineAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task SendAsync(StreamWriter writer, string code, string? detail)
    {
        try
        {
            await writer.WriteLineAsync(detail is null ? code : $"{code}|{detail.Replace(Environment.NewLine, " ")}").ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
    }
}
