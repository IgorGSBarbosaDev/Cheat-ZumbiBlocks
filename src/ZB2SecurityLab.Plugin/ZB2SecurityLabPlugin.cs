using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Core.Experiments;
using ZB2SecurityLab.Plugin.Core;
using ZB2SecurityLab.Plugin.Diagnostics;
using ZB2SecurityLab.Plugin.Experiments;
using ZB2SecurityLab.Plugin.UI;

namespace ZB2SecurityLab.Plugin;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class ZB2SecurityLabPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.igorgsbarbosa.zb2securitylab";
    public const string PluginName = "ZB2 Security Lab";
    public const string PluginVersion = "0.5.0";

    private const float PollIntervalSeconds = 0.25f;
    private const float ErrorLogIntervalSeconds = 5f;

    private readonly DiagnosticPanel _panel = new();
    private readonly LabStateTracker _stateTracker = new();
    private readonly PlayerStateTracker _playerStateTracker = new();
    private readonly LabContext _labContext = new();
    private readonly AuthorizedSessionGrantReader _authorizedSessionGrantReader = new();
    private readonly LabModeGuard _labModeGuard = new();
    private readonly ExperimentCoordinator _experimentCoordinator = new(durationSeconds: 10d);

    private ConfigEntry<KeyboardShortcut>? _toggleShortcut;
    private ConfigEntry<bool>? _mutationEnabled;
    private ConfigEntry<bool>? _authorizedMultiplayerEnabled;
    private ConfigEntry<string>? _authorizedSessionGrantPath;
    private JsonlEventWriter? _eventWriter;
    private LabSnapshot? _lastSnapshot;
    private MultiplayerSessionSnapshot? _lastMutationSession;
    private MutationPanelCommand _pendingCommand;
    private string _sessionId = string.Empty;
    private string _buildStatus = "NOT CHECKED";
    private string _buildFingerprint = string.Empty;
    private bool _buildSupported;
    private float _nextPollTime;
    private float _nextErrorLogTime;

    private void Awake()
    {
        _sessionId = Guid.NewGuid().ToString("N");
        _toggleShortcut = Config.Bind(
            "Diagnostics",
            "ToggleShortcut",
            new KeyboardShortcut(KeyCode.F8),
            "Toggles the ZB2 Security Lab panel.");
        _mutationEnabled = Config.Bind(
            "ControlledMutations",
            "Enabled",
            false,
            "Explicit opt-in for 10-second controlled mutations on supported builds.");
        _authorizedMultiplayerEnabled = Config.Bind(
            "AuthorizedMultiplayer",
            "Enabled",
            false,
            "Independent opt-in for exact, grant-bound private multiplayer sessions.");
        _authorizedSessionGrantPath = Config.Bind(
            "AuthorizedMultiplayer",
            "GrantPath",
            DefaultAuthorizationGrantPath(),
            "Absolute path to the short-lived authorized-session JSON grant.");

        var outputDirectory = Config.Bind(
            "Diagnostics",
            "OutputDirectory",
            DefaultOutputDirectory(),
            "Absolute directory for Security Lab JSONL events.").Value;

        VerifyBuild();

        try
        {
            _eventWriter = new JsonlEventWriter(outputDirectory, _sessionId);
            WriteEvent("INSTRUMENTATION_INITIALIZED", $"unity={Application.unityVersion};status={_buildStatus};mutations={MutationEnabled}");
            Logger.LogInfo($"{PluginName} {PluginVersion} initialized. Build {_buildStatus}.");
            Logger.LogInfo($"Diagnostic events: {_eventWriter.FilePath}");
            Logger.LogInfo($"Controlled mutations: {(MutationEnabled ? "ENABLED" : "DISABLED")}.");
            Logger.LogInfo($"Authorized multiplayer: {(AuthorizedMultiplayerEnabled ? "ENABLED" : "DISABLED")} (exact session grant required).");
        }
        catch (Exception exception)
        {
            Logger.LogError($"Unable to initialize diagnostic event output: {exception}");
        }
    }

    private void Update()
    {
        if (_toggleShortcut != null && _toggleShortcut.Value.IsDown())
        {
            _panel.Visible = !_panel.Visible;
            WriteEvent(_panel.Visible ? "PANEL_OPENED" : "PANEL_CLOSED", null);
            if (!_panel.Visible)
            {
                WriteMutationEvents(_experimentCoordinator.Stop(RestoreReason.PANEL_CLOSED));
                _pendingCommand = MutationPanelCommand.NONE;
            }
        }

        if (!_buildSupported)
        {
            _pendingCommand = MutationPanelCommand.NONE;
            return;
        }

        if (Time.unscaledTime >= _nextPollTime)
        {
            _nextPollTime = Time.unscaledTime + PollIntervalSeconds;
            PollContext();
        }

        TickActiveExperiment();
        ProcessPendingCommand();
    }

    private void OnGUI()
    {
        var command = _panel.Draw(
            _buildSupported,
            _buildStatus,
            _lastSnapshot,
            _sessionId,
            BuildMutationPanelState());
        if (command != MutationPanelCommand.NONE && _pendingCommand == MutationPanelCommand.NONE)
        {
            _pendingCommand = command;
        }
    }

    private void OnDisable()
    {
        WriteMutationEvents(_experimentCoordinator.Stop(RestoreReason.PLUGIN_DISABLED));
    }

    private void OnDestroy()
    {
        WriteMutationEvents(_experimentCoordinator.Stop(RestoreReason.PLUGIN_DESTROYED));
        WriteEvent("INSTRUMENTATION_STOPPED", null);
        _eventWriter?.Dispose();
        _eventWriter = null;
    }

    private void PollContext()
    {
        try
        {
            var observedAt = DateTimeOffset.UtcNow;
            _lastSnapshot = _labContext.Capture();
            foreach (var transition in _stateTracker.Observe(_lastSnapshot))
            {
                WriteEvent(
                    transition.Kind.ToString(),
                    $"previous={transition.PreviousValue ?? "NULL"};current={transition.CurrentValue ?? "NULL"}");
            }

            foreach (var transition in _playerStateTracker.Observe(_lastSnapshot, observedAt))
            {
                WriteEvent(
                    transition.Kind.ToString(),
                    transition.CurrentValue,
                    eventName: transition.Kind.ToString().ToLowerInvariant(),
                    oldValue: transition.PreviousValue,
                    newValue: transition.CurrentValue,
                    context: BuildEventContext(_lastSnapshot));
            }

        }
        catch (Exception exception)
        {
            WriteMutationEvents(_experimentCoordinator.Stop(RestoreReason.CONTEXT_INVALID, exception.Message));
            if (Time.unscaledTime >= _nextErrorLogTime)
            {
                _nextErrorLogTime = Time.unscaledTime + ErrorLogIntervalSeconds;
                Logger.LogError($"Game context capture failed: {exception}");
                WriteEvent("CONTEXT_CAPTURE_FAILED", null, exception.ToString());
            }
        }
    }

    private void TickActiveExperiment()
    {
        if (_experimentCoordinator.ActiveExperiment is null)
        {
            return;
        }

        try
        {
            var context = CaptureMutationEligibility();
            _lastMutationSession = context.MultiplayerSession;
            var decision = _labModeGuard.Evaluate(context);
            WriteMutationEvents(_experimentCoordinator.Tick(
                Time.unscaledTime,
                decision,
                context.PlayerToken));
        }
        catch (Exception exception)
        {
            WriteMutationEvents(_experimentCoordinator.Stop(RestoreReason.CONTEXT_INVALID, exception.Message));
            if (Time.unscaledTime >= _nextErrorLogTime)
            {
                _nextErrorLogTime = Time.unscaledTime + ErrorLogIntervalSeconds;
                Logger.LogError($"Mutation context capture failed: {exception}");
                WriteEvent("MUTATION_CONTEXT_CAPTURE_FAILED", null, exception.ToString());
            }
        }
    }

    private void ProcessPendingCommand()
    {
        var command = _pendingCommand;
        if (command == MutationPanelCommand.NONE)
        {
            return;
        }

        _pendingCommand = MutationPanelCommand.NONE;
        if (command == MutationPanelCommand.RESTORE)
        {
            WriteMutationEvents(_experimentCoordinator.Stop(RestoreReason.MANUAL));
            return;
        }

        var testId = command switch
        {
            MutationPanelCommand.RUN_FOV => "FOV",
            MutationPanelCommand.RUN_STAMINA => "STAMINA",
            MutationPanelCommand.RUN_INFINITE_AMMO => "INFINITE_AMMO",
            _ => "UNKNOWN"
        };
        WriteMutationEvent(new MutationLifecycleEvent { TestId = testId, Phase = MutationPhase.REQUESTED });

        try
        {
            _lastSnapshot = _labContext.Capture();
        }
        catch (Exception exception)
        {
            WriteMutationEvent(new MutationLifecycleEvent
            {
                TestId = testId,
                Phase = MutationPhase.BLOCKED,
                Outcome = TestOutcome.INCONCLUSIVE,
                Error = exception.Message
            });
            return;
        }

        var eligibilityContext = CaptureMutationEligibility();
        _lastMutationSession = eligibilityContext.MultiplayerSession;
        var decision = _labModeGuard.Evaluate(eligibilityContext);
        WriteMutationEvent(new MutationLifecycleEvent
        {
            TestId = testId,
            Phase = MutationPhase.ELIGIBILITY_CHECKED,
            ExecutionScope = decision.Scope,
            SessionToken = decision.SessionToken,
            AuthorizationId = decision.AuthorizationId,
            LocalObservedValue = decision.Reason,
            Error = decision.Allowed ? null : decision.Reason
        });
        if (!decision.Allowed)
        {
            WriteMutationEvent(new MutationLifecycleEvent
            {
                TestId = testId,
                Phase = MutationPhase.BLOCKED,
                ExecutionScope = decision.Scope,
                SessionToken = decision.SessionToken,
                AuthorizationId = decision.AuthorizationId,
                Outcome = TestOutcome.INCONCLUSIVE,
                Error = decision.Reason
            });
            return;
        }

        var player = _labContext.GetLocalPlayer();
        var playerToken = player == null ? null : player.GetInstanceID().ToString(CultureInfo.InvariantCulture);
        if (player == null ||
            !string.Equals(playerToken, _lastSnapshot.LocalPlayerToken, StringComparison.Ordinal) ||
            !string.Equals(playerToken, eligibilityContext.PlayerToken, StringComparison.Ordinal))
        {
            WriteMutationEvent(new MutationLifecycleEvent
            {
                TestId = testId,
                Phase = MutationPhase.BLOCKED,
                Outcome = TestOutcome.INCONCLUSIVE,
                Error = "LOCAL_PLAYER_CHANGED_BEFORE_APPLY"
            });
            return;
        }

        ILabExperiment experiment;
        if (command == MutationPanelCommand.RUN_FOV)
        {
            experiment = new FovMutationTest(playerToken!);
        }
        else if (command == MutationPanelCommand.RUN_STAMINA)
        {
            experiment = new StaminaMutationTest(player, playerToken!);
        }
        else
        {
            var ammoExperiment = new InfiniteAmmoMutationTest(player, playerToken!);
            if (!ammoExperiment.CanStart(out var reason))
            {
                WriteMutationEvent(new MutationLifecycleEvent
                {
                    TestId = testId,
                    Phase = MutationPhase.BLOCKED,
                    Outcome = TestOutcome.INCONCLUSIVE,
                    LocalObservedValue = reason,
                    Error = reason
                });
                return;
            }

            experiment = ammoExperiment;
        }

        WriteMutationEvents(_experimentCoordinator.Start(experiment, Time.unscaledTime, decision));
    }

    private MutationPanelState BuildMutationPanelState()
    {
        var decision = _labModeGuard.Evaluate(BuildEligibilityContext(_lastSnapshot));
        var active = _experimentCoordinator.ActiveExperiment;
        var ammoReason = AmmoEligibilityReason(_lastSnapshot);
        var ammoExperiment = active as InfiniteAmmoMutationTest;
        return new MutationPanelState
        {
            MutationsEnabled = MutationEnabled,
            AuthorizedMultiplayerEnabled = AuthorizedMultiplayerEnabled,
            Eligible = decision.Allowed && active is null,
            EligibilityReason = active is null ? decision.Reason : "EXPERIMENT_ALREADY_ACTIVE",
            IsActive = active is not null,
            ActiveTestId = active?.Id,
            RemainingSeconds = _experimentCoordinator.RemainingSeconds(Time.unscaledTime),
            OriginalValue = active?.OriginalValue,
            RequestedValue = active?.RequestedValue,
            LocalObservedValue = active?.LocalObservedValue,
            LastResult = _experimentCoordinator.LastResult,
            AmmoEligible = decision.Allowed && active is null && string.Equals(ammoReason, "AMMO_READY", StringComparison.Ordinal),
            AmmoEligibilityReason = decision.Allowed ? ammoReason : decision.Reason,
            ActiveTarget = ammoExperiment?.ActiveTarget,
            TrackedTargetCount = ammoExperiment?.TrackedTargetCount,
            WriteCount = ammoExperiment?.WriteCount,
            PausedReason = ammoExperiment?.PausedReason,
            ExecutionScope = decision.Scope,
            AuthorizationId = decision.AuthorizationId,
            SteamLobbyId = _lastSnapshot?.MultiplayerSession?.SteamLobbyId,
            ServerSteamId = _lastSnapshot?.MultiplayerSession?.ServerSteamId
        };
    }

    private static string AmmoEligibilityReason(LabSnapshot? snapshot)
    {
        var weapon = snapshot?.Weapon;
        if (weapon is null)
        {
            return "NO_WEAPON_SELECTED";
        }

        if (!weapon.IsGun)
        {
            return "SELECTED_ITEM_IS_NOT_A_GUN";
        }

        if (!weapon.Ammo.HasValue || !weapon.MagazineSize.HasValue || !weapon.AmmoConsumption.HasValue ||
            weapon.MagazineSize.Value <= 0 || weapon.AmmoConsumption.Value <= 0)
        {
            return "INVALID_AMMO_CONFIGURATION";
        }

        return weapon.Ammo.Value == weapon.MagazineSize.Value
            ? "AMMO_READY"
            : "MAGAZINE_NOT_FULL";
    }

    private MutationEligibilityContext BuildEligibilityContext(LabSnapshot? snapshot)
    {
        var grant = ReadAuthorizationGrant();
        return new MutationEligibilityContext
        {
            MutationEnabled = MutationEnabled,
            BuildSupported = _buildSupported,
            InGame = snapshot?.InGame == true,
            LocalPlayerAvailable = snapshot?.LocalPlayerAvailable == true,
            HasLocalControl = snapshot?.HasLocalControl == true,
            Role = snapshot?.Role ?? "UNKNOWN",
            PlayerToken = snapshot?.LocalPlayerToken,
            BuildId = KnownBuild.BuildId,
            ObservedAtUtc = DateTimeOffset.UtcNow,
            AuthorizedMultiplayerEnabled = AuthorizedMultiplayerEnabled,
            AuthorizationGrant = grant.Grant,
            AuthorizationGrantError = grant.Error,
            MultiplayerSession = snapshot?.MultiplayerSession
        };
    }

    private bool MutationEnabled => _mutationEnabled?.Value == true;

    private bool AuthorizedMultiplayerEnabled => _authorizedMultiplayerEnabled?.Value == true;

    private MutationEligibilityContext CaptureMutationEligibility()
    {
        var grant = ReadAuthorizationGrant();
        return _labContext.CaptureMutationEligibility(
            MutationEnabled,
            _buildSupported,
            AuthorizedMultiplayerEnabled,
            grant.Grant,
            grant.Error,
            KnownBuild.BuildId);
    }

    private AuthorizedSessionGrantReadResult ReadAuthorizationGrant()
    {
        return _authorizedSessionGrantReader.Read(_authorizedSessionGrantPath?.Value ?? string.Empty);
    }

    private void VerifyBuild()
    {
        var executablePath = Path.Combine(Paths.GameRootPath, "ZumbiBlocks2.exe");
        var assemblyPath = Path.Combine(Paths.GameRootPath, "ZumbiBlocks2_Data", "Managed", "Assembly-CSharp.dll");
        var executable = BuildFingerprint.Verify(executablePath, KnownBuild.ExecutableSha256);
        var assembly = BuildFingerprint.Verify(assemblyPath, KnownBuild.AssemblyCSharpSha256);

        _buildSupported = executable.IsMatch && assembly.IsMatch &&
            string.Equals(Application.unityVersion, KnownBuild.UnityVersion, StringComparison.Ordinal);
        _buildFingerprint = assembly.ActualSha256 ?? "UNAVAILABLE";
        _buildStatus = _buildSupported
            ? $"SUPPORTED {KnownBuild.BuildId} / Unity {KnownBuild.UnityVersion}"
            : $"BLOCKED expected {KnownBuild.BuildId} / Unity {KnownBuild.UnityVersion}";

        if (!_buildSupported)
        {
            Logger.LogWarning("Build fingerprint mismatch. All game-context polling and controlled mutations are disabled.");
            Logger.LogWarning($"Executable: expected={executable.ExpectedSha256} actual={executable.ActualSha256 ?? executable.Error}");
            Logger.LogWarning($"Assembly-CSharp: expected={assembly.ExpectedSha256} actual={assembly.ActualSha256 ?? assembly.Error}");
            Logger.LogWarning($"Unity: expected={KnownBuild.UnityVersion} actual={Application.unityVersion}");
        }
    }

    private void WriteMutationEvents(IEnumerable<MutationLifecycleEvent> events)
    {
        foreach (var mutationEvent in events)
        {
            WriteMutationEvent(mutationEvent);
        }
    }

    private void WriteMutationEvent(MutationLifecycleEvent mutationEvent)
    {
        if (_eventWriter is null)
        {
            return;
        }

        try
        {
            var network = _lastMutationSession ?? _lastSnapshot?.MultiplayerSession;
            var serverEvidence = mutationEvent.ServerEvidence ??
                (network?.Role == NetworkRole.SINGLE_PLAYER
                    ? ServerEvidenceKind.NOT_APPLICABLE
                    : ServerEvidenceKind.NOT_OBSERVED);
            _eventWriter.Write(new SecurityTestEvent
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                SessionId = _sessionId,
                BuildId = KnownBuild.BuildId,
                BuildFingerprint = _buildFingerprint,
                Test = mutationEvent.TestId,
                Phase = mutationEvent.Phase.ToString(),
                Event = mutationEvent.Event ?? "controlled_mutation",
                OldValue = mutationEvent.OldValue,
                NewValue = mutationEvent.NewValue,
                Context = CombineContext(BuildEventContext(_lastSnapshot), mutationEvent.Context),
                OriginalValue = mutationEvent.OriginalValue,
                RequestedValue = mutationEvent.RequestedValue,
                LocalObservedValue = mutationEvent.LocalObservedValue,
                RemoteObservedValue = mutationEvent.RemoteObservedValue,
                ServerEvidence = serverEvidence.ToString(),
                ExecutionScope = mutationEvent.ExecutionScope?.ToString(),
                NetworkMode = NetworkMode(network),
                NetworkRole = network?.Role.ToString() ?? _lastSnapshot?.Role,
                ConnectionState = network?.ConnectionState ?? _lastSnapshot?.ConnectionState,
                SteamLobbyId = network?.SteamLobbyId,
                ServerSteamId = network?.ServerSteamId,
                LobbyOwnerSteamId = network?.LobbyOwnerSteamId,
                LocalSteamId = network?.LocalSteamId,
                LocalLobbyPlayerId = network?.LocalLobbyPlayerId,
                AuthorizationId = mutationEvent.AuthorizationId,
                AuthorizationDecision = mutationEvent.Phase == MutationPhase.ELIGIBILITY_CHECKED
                    ? mutationEvent.LocalObservedValue
                    : null,
                ExperimentRunId = mutationEvent.ExperimentRunId,
                EvidenceSource = mutationEvent.EvidenceSource,
                Outcome = mutationEvent.Outcome,
                RestoreReason = mutationEvent.RestoreReason?.ToString(),
                RestoreSucceeded = mutationEvent.RestoreSucceeded,
                ServerCorrected = serverEvidence == ServerEvidenceKind.CORRECTED ? true : null,
                ServerAccepted = serverEvidence == ServerEvidenceKind.ACCEPTED ? true : null,
                Disconnected = IsDisconnected(network, mutationEvent.ExecutionScope),
                Error = mutationEvent.Error
            });
        }
        catch (Exception exception)
        {
            Logger.LogError($"Unable to write controlled mutation event: {exception.Message}");
        }
    }

    private void WriteEvent(
        string phase,
        string? observedValue,
        string? error = null,
        string? eventName = null,
        string? oldValue = null,
        string? newValue = null,
        string? context = null)
    {
        if (_eventWriter is null)
        {
            return;
        }

        try
        {
            var network = _lastSnapshot?.MultiplayerSession;
            _eventWriter.Write(new SecurityTestEvent
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                SessionId = _sessionId,
                BuildId = KnownBuild.BuildId,
                BuildFingerprint = _buildFingerprint,
                Test = "Instrumentation",
                Phase = phase,
                Event = eventName,
                OldValue = oldValue,
                NewValue = newValue,
                Context = context,
                OriginalValue = oldValue,
                LocalObservedValue = observedValue,
                NetworkMode = NetworkMode(network),
                NetworkRole = network?.Role.ToString() ?? _lastSnapshot?.Role,
                ConnectionState = network?.ConnectionState ?? _lastSnapshot?.ConnectionState,
                SteamLobbyId = network?.SteamLobbyId,
                ServerSteamId = network?.ServerSteamId,
                LobbyOwnerSteamId = network?.LobbyOwnerSteamId,
                LocalSteamId = network?.LocalSteamId,
                LocalLobbyPlayerId = network?.LocalLobbyPlayerId,
                Disconnected = false,
                Error = error
            });
        }
        catch (Exception exception)
        {
            Logger.LogError($"Unable to write Security Lab event: {exception.Message}");
        }
    }

    private static string? BuildEventContext(LabSnapshot? snapshot)
    {
        return snapshot is null
            ? null
            : $"playerToken={snapshot.LocalPlayerToken ?? "NULL"};role={snapshot.Role};steamLobby={snapshot.MultiplayerSession?.SteamLobbyId ?? "NULL"};serverSteam={snapshot.MultiplayerSession?.ServerSteamId ?? "NULL"};localLobbyPlayer={snapshot.MultiplayerSession?.LocalLobbyPlayerId?.ToString(CultureInfo.InvariantCulture) ?? "NULL"};weapon={snapshot.Weapon?.Id ?? "NONE"}";
    }

    private static string? CombineContext(string? sessionContext, string? eventContext)
    {
        if (sessionContext is null)
        {
            return eventContext;
        }

        return eventContext is null ? sessionContext : $"{sessionContext};{eventContext}";
    }

    private static string DefaultOutputDirectory()
    {
        return Path.GetFullPath(Path.Combine(Paths.BepInExRootPath, "logs", "ZB2SecurityLab"));
    }

    private static string DefaultAuthorizationGrantPath()
    {
        return Path.GetFullPath(Path.Combine(Paths.ConfigPath, "ZB2SecurityLab", "authorized-session.json"));
    }

    private static string NetworkMode(MultiplayerSessionSnapshot? network)
    {
        return network is null ? "UNKNOWN" : network.IsMultiplayer ? "MULTIPLAYER" : "SINGLE_PLAYER";
    }

    private static bool IsDisconnected(
        MultiplayerSessionSnapshot? network,
        MutationExecutionScope? executionScope)
    {
        return executionScope == MutationExecutionScope.AUTHORIZED_MULTIPLAYER_CLIENT && network?.ClientConnected != true;
    }
}
