using System.Text.Json;

namespace ZB2SecurityLab.Launcher.Services;

internal sealed class LauncherAuditLog
{
    private readonly string _path;
    private readonly object _gate = new();

    internal LauncherAuditLog(LocalDataPaths paths)
    {
        paths.EnsureCreated();
        _path = Path.Combine(paths.LauncherLogs, $"launcher-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.jsonl");
    }

    internal string FilePath => _path;

    internal void Write(string eventName, string? detail = null)
    {
        var json = JsonSerializer.Serialize(new
        {
            timestampUtc = DateTimeOffset.UtcNow,
            @event = eventName,
            detail
        });
        lock (_gate)
        {
            File.AppendAllText(_path, json + Environment.NewLine);
        }
    }
}
