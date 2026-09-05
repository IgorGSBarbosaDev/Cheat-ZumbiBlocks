using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Plugin.Core;
using ZB2SecurityLab.Plugin.Diagnostics;
using ZB2SecurityLab.Plugin.UI;

namespace ZB2SecurityLab.Plugin;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class ZB2SecurityLabPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "com.igorgsbarbosa.zb2securitylab";
    public const string PluginName = "ZB2 Security Lab";
    public const string PluginVersion = "0.1.0";

    private const float PollIntervalSeconds = 0.25f;
    private const float ErrorLogIntervalSeconds = 5f;

    private readonly DiagnosticPanel _panel = new();
    private readonly LabStateTracker _stateTracker = new();
    private readonly LabContext _labContext = new();

    private ConfigEntry<KeyboardShortcut>? _toggleShortcut;
    private JsonlEventWriter? _eventWriter;
    private LabSnapshot? _lastSnapshot;
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
            "Toggles the read-only Security Lab diagnostic panel.");

        var outputDirectory = Config.Bind(
            "Diagnostics",
            "OutputDirectory",
            DefaultOutputDirectory(),
            "Absolute directory for Security Lab JSONL events.").Value;

        VerifyBuild();

        try
        {
            _eventWriter = new JsonlEventWriter(outputDirectory, _sessionId);
            WriteEvent("INSTRUMENTATION_INITIALIZED", $"unity={Application.unityVersion};status={_buildStatus}");
            Logger.LogInfo($"{PluginName} {PluginVersion} initialized. Build {_buildStatus}.");
            Logger.LogInfo($"Diagnostic events: {_eventWriter.FilePath}");
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
        }

        if (!_buildSupported || Time.unscaledTime < _nextPollTime)
        {
            return;
        }

        _nextPollTime = Time.unscaledTime + PollIntervalSeconds;
        try
        {
            _lastSnapshot = _labContext.Capture();
            foreach (var transition in _stateTracker.Observe(_lastSnapshot))
            {
                WriteEvent(
                    transition.Kind.ToString(),
                    $"previous={transition.PreviousValue ?? "NULL"};current={transition.CurrentValue ?? "NULL"}");
            }
        }
        catch (Exception exception)
        {
            if (Time.unscaledTime >= _nextErrorLogTime)
            {
                _nextErrorLogTime = Time.unscaledTime + ErrorLogIntervalSeconds;
                Logger.LogError($"Read-only context capture failed: {exception}");
                WriteEvent("CONTEXT_CAPTURE_FAILED", null, exception.ToString());
            }
        }
    }

    private void OnGUI()
    {
        _panel.Draw(_buildSupported, _buildStatus, _lastSnapshot, _sessionId);
    }

    private void OnDestroy()
    {
        WriteEvent("INSTRUMENTATION_STOPPED", null);
        _eventWriter?.Dispose();
        _eventWriter = null;
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
            Logger.LogWarning("Build fingerprint mismatch. All game-context polling is disabled.");
            Logger.LogWarning($"Executable: expected={executable.ExpectedSha256} actual={executable.ActualSha256 ?? executable.Error}");
            Logger.LogWarning($"Assembly-CSharp: expected={assembly.ExpectedSha256} actual={assembly.ActualSha256 ?? assembly.Error}");
            Logger.LogWarning($"Unity: expected={KnownBuild.UnityVersion} actual={Application.unityVersion}");
        }
    }

    private void WriteEvent(string phase, string? observedValue, string? error = null)
    {
        if (_eventWriter is null)
        {
            return;
        }

        try
        {
            _eventWriter.Write(new SecurityTestEvent
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                SessionId = _sessionId,
                BuildId = KnownBuild.BuildId,
                BuildFingerprint = _buildFingerprint,
                Test = "Instrumentation",
                Phase = phase,
                LocalObservedValue = observedValue,
                Disconnected = false,
                Error = error
            });
        }
        catch (Exception exception)
        {
            Logger.LogError($"Unable to write Security Lab event: {exception.Message}");
        }
    }

    private static string DefaultOutputDirectory()
    {
        return Path.GetFullPath(Path.Combine(Paths.GameRootPath, "..", "..", "logs", "security-tests"));
    }
}

