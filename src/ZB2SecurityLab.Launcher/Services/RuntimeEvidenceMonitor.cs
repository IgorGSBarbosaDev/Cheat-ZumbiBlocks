using ZB2SecurityLab.Launcher.Domain;

namespace ZB2SecurityLab.Launcher.Services;

internal sealed class RuntimeEvidenceMonitor(LocalDataPaths dataPaths)
{
    internal async Task<bool> WaitForLabAsync(LaunchSessionJournal journal, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var loaderLog = BuildValidator.ResolveGamePath(journal.GamePath, @"BepInEx\LogOutput.log");
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Contains(loaderLog, "Loading [ZB2 Security Lab") || PluginLogContainsInitialization(journal.CreatedUtc))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }

        return false;
    }

    private bool PluginLogContainsInitialization(DateTimeOffset sessionStart)
    {
        if (!Directory.Exists(dataPaths.PluginLogs))
        {
            return false;
        }

        foreach (var file in Directory.EnumerateFiles(dataPaths.PluginLogs, "*.jsonl"))
        {
            if (File.GetLastWriteTimeUtc(file) >= sessionStart.UtcDateTime && Contains(file, "INSTRUMENTATION_INITIALIZED"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(string path, string value)
    {
        try
        {
            return File.Exists(path) && File.ReadAllText(path).Contains(value, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
