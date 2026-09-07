using System.Text.Json.Serialization;
using ZB2SecurityLab.Core.Diagnostics;

namespace ZB2SecurityLab.Launcher.Domain;

internal enum LauncherState
{
    Detecting,
    Validating,
    Ready,
    Preparing,
    LaunchRequested,
    Running,
    Cleaning,
    Completed,
    Blocked,
    RecoveryRequired
}

internal sealed record SteamInstallation(
    string SteamRoot,
    string SteamExecutablePath,
    string LibraryRoot,
    string ManifestPath,
    string GamePath,
    string AppId,
    string BuildId,
    bool UpdatePending);

internal sealed record SteamDiscoveryResult(
    IReadOnlyList<SteamInstallation> Installations,
    IReadOnlyList<string> Errors);

internal sealed record BuildValidationResult(
    bool IsSupported,
    BuildFingerprintResult Executable,
    BuildFingerprintResult AssemblyCSharp,
    IReadOnlyList<string> Errors);

internal enum JournalPhase
{
    Validated,
    Installing,
    Installed,
    LaunchRequested,
    Running,
    Cleaning,
    Completed,
    RecoveryRequired
}

internal sealed class LaunchSessionJournal
{
    public int SchemaVersion { get; set; } = 1;
    public string SessionId { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string GamePath { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string SteamExecutablePath { get; set; } = string.Empty;
    public string SteamRoot { get; set; } = string.Empty;
    public string LibraryRoot { get; set; } = string.Empty;
    public string ManifestPath { get; set; } = string.Empty;
    public string AppId { get; set; } = SupportedBuild.SteamAppId;
    public string DataRoot { get; set; } = string.Empty;
    public string BuildId { get; set; } = SupportedBuild.BuildId;
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public JournalPhase Phase { get; set; } = JournalPhase.Validated;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public int? GameProcessId { get; set; }
    public bool MutationEnabled { get; set; }
    public bool BepInExDirectoryCreated { get; set; }
    public bool RuntimeEvidenceConfirmed { get; set; }
    public List<string> CreatedFiles { get; set; } = new();
    public Dictionary<string, string> RootFileHashes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Errors { get; set; } = new();
}

internal sealed record PayloadFile(string RelativePath, byte[] Contents, string Sha256);

internal sealed record PayloadPackage(IReadOnlyList<PayloadFile> Files);

internal sealed record ProcessSnapshot(int Id, string ImagePath, DateTimeOffset StartedUtc);

internal static class LauncherProtocol
{
    internal const string WorkerArgument = "--zb2-worker";
    internal const string RecoverArgument = "--zb2-recover";
    internal const string Installed = "INSTALLED";
    internal const string ElevationRequired = "ELEVATION_REQUIRED";
    internal const string Running = "RUNNING";
    internal const string LabLoaded = "LAB_LOADED";
    internal const string LabNotConfirmed = "LAB_NOT_CONFIRMED";
    internal const string Cleaning = "CLEANING";
    internal const string Completed = "COMPLETED";
    internal const string RecoveryRequired = "RECOVERY_REQUIRED";
    internal const string Blocked = "BLOCKED";
}
