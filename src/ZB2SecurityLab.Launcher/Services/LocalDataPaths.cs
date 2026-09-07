namespace ZB2SecurityLab.Launcher.Services;

internal sealed class LocalDataPaths
{
    internal LocalDataPaths(string? root = null)
    {
        Root = Path.GetFullPath(root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ZB2SecurityLab"));
    }

    internal string Root { get; }
    internal string Logs => Path.Combine(Root, "logs");
    internal string PluginLogs => Path.Combine(Logs, "plugin");
    internal string LoaderLogs => Path.Combine(Logs, "loader");
    internal string LauncherLogs => Path.Combine(Logs, "launcher");
    internal string Grants => Path.Combine(Root, "grants");
    internal string Journals => Path.Combine(Root, "transactions");

    internal void EnsureCreated()
    {
        foreach (var directory in new[] { Root, Logs, PluginLogs, LoaderLogs, LauncherLogs, Grants, Journals })
        {
            Directory.CreateDirectory(directory);
        }
    }

    internal string JournalPath(string sessionId) => Path.Combine(Journals, $"{sessionId}.json");
    internal string GrantPath => Path.Combine(Grants, "authorized-session.json");
}
