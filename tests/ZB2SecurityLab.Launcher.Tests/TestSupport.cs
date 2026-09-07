using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Launcher.Domain;
using ZB2SecurityLab.Launcher.Services;

namespace ZB2SecurityLab.Launcher.Tests;

internal sealed class TemporaryDirectory : IDisposable
{
    internal TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"zb2-launcher-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    internal string Path { get; }
    internal string Combine(params string[] parts) => parts.Aggregate(Path, System.IO.Path.Combine);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}

internal sealed class AlwaysValidBuildValidator : IBuildValidator
{
    private static BuildValidationResult Result(string gamePath)
    {
        var executable = new BuildFingerprintResult(gamePath, "A", "A", null);
        var assembly = new BuildFingerprintResult(gamePath, "B", "B", null);
        return new BuildValidationResult(true, executable, assembly, Array.Empty<string>());
    }

    public BuildValidationResult Validate(SteamInstallation installation, bool checkLoaderConflicts = true) => Result(installation.GamePath);
    public BuildValidationResult ValidateFiles(string gamePath) => Result(gamePath);
}

internal sealed class FakeProcessCatalog(params ProcessSnapshot[] snapshots) : IProcessCatalog
{
    internal List<ProcessSnapshot> Snapshots { get; } = snapshots.ToList();
    public IReadOnlyList<ProcessSnapshot> Snapshot(string processName) => Snapshots;
    public Task WaitForExitAsync(int processId, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal static class TestPaths
{
    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "ZB2SecurityLab.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
