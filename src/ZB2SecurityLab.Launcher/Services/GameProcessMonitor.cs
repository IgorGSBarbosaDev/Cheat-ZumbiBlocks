using System.Diagnostics;
using ZB2SecurityLab.Launcher.Domain;

namespace ZB2SecurityLab.Launcher.Services;

internal interface IProcessCatalog
{
    IReadOnlyList<ProcessSnapshot> Snapshot(string processName);
    Task WaitForExitAsync(int processId, CancellationToken cancellationToken);
}

internal sealed class SystemProcessCatalog : IProcessCatalog
{
    public IReadOnlyList<ProcessSnapshot> Snapshot(string processName)
    {
        var snapshots = new List<ProcessSnapshot>();
        foreach (var process in Process.GetProcessesByName(processName))
        {
            using (process)
            {
                try
                {
                    var path = process.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        snapshots.Add(new ProcessSnapshot(process.Id, Path.GetFullPath(path), process.StartTime.ToUniversalTime()));
                    }
                }
                catch (InvalidOperationException)
                {
                }
                catch (System.ComponentModel.Win32Exception)
                {
                }
            }
        }

        return snapshots;
    }

    public async Task WaitForExitAsync(int processId, CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}

internal interface IGameProcessMonitor
{
    bool IsGameRunning(string executablePath);
    Task<ProcessSnapshot?> WaitForStartAsync(string executablePath, DateTimeOffset requestedAfterUtc, TimeSpan timeout, CancellationToken cancellationToken);
    Task WaitForExitAsync(ProcessSnapshot process, CancellationToken cancellationToken);
}

internal sealed class GameProcessMonitor(IProcessCatalog processCatalog) : IGameProcessMonitor
{
    public bool IsGameRunning(string executablePath)
        => Find(executablePath, DateTimeOffset.MinValue) is not null;

    public async Task<ProcessSnapshot?> WaitForStartAsync(
        string executablePath,
        DateTimeOffset requestedAfterUtc,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var process = Find(executablePath, requestedAfterUtc.AddSeconds(-2));
            if (process is not null)
            {
                return process;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }

        return null;
    }

    public Task WaitForExitAsync(ProcessSnapshot process, CancellationToken cancellationToken)
        => processCatalog.WaitForExitAsync(process.Id, cancellationToken);

    internal ProcessSnapshot? Find(string executablePath, DateTimeOffset startedAfterUtc)
    {
        var expected = Path.GetFullPath(executablePath);
        var processName = Path.GetFileNameWithoutExtension(expected);
        return processCatalog.Snapshot(processName)
            .Where(process => process.StartedUtc >= startedAfterUtc)
            .Where(process => string.Equals(Path.GetFullPath(process.ImagePath), expected, StringComparison.OrdinalIgnoreCase))
            .OrderBy(process => process.StartedUtc)
            .FirstOrDefault();
    }
}
