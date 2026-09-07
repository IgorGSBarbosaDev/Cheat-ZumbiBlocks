using System.Diagnostics;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Launcher.Domain;

namespace ZB2SecurityLab.Launcher.Services;

internal interface ISteamGameLauncher
{
    void Launch(SteamInstallation installation);
}

internal sealed class SteamGameLauncher : ISteamGameLauncher
{
    public void Launch(SteamInstallation installation)
    {
        if (!File.Exists(installation.SteamExecutablePath))
        {
            throw new FileNotFoundException("steam.exe não foi encontrado.", installation.SteamExecutablePath);
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = installation.SteamExecutablePath,
            Arguments = $"-applaunch {SupportedBuild.SteamAppId}",
            WorkingDirectory = installation.SteamRoot,
            UseShellExecute = true
        });
        process?.Dispose();
    }
}
