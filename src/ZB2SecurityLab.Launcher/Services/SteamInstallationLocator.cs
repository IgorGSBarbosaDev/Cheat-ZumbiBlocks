using Microsoft.Win32;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Launcher.Domain;

namespace ZB2SecurityLab.Launcher.Services;

internal interface ISteamInstallationLocator
{
    SteamDiscoveryResult Locate();
}

internal interface ISteamRootProvider
{
    IEnumerable<string> GetCandidateRoots();
}

internal sealed class RegistrySteamRootProvider : ISteamRootProvider
{
    public IEnumerable<string> GetCandidateRoots()
    {
        var roots = new List<string>();
        AddFromKey(roots, Registry.CurrentUser, @"Software\Valve\Steam");

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
            AddFromKey(roots, baseKey, @"SOFTWARE\Valve\Steam");
        }

        roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
        return roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddFromKey(ICollection<string> roots, RegistryKey baseKey, string subKeyName)
    {
        using var key = baseKey.OpenSubKey(subKeyName);
        var path = key?.GetValue("SteamPath") as string ?? key?.GetValue("InstallPath") as string;
        if (!string.IsNullOrWhiteSpace(path))
        {
            roots.Add(path);
        }
    }
}

internal sealed class FixedSteamRootProvider(params string[] roots) : ISteamRootProvider
{
    public IEnumerable<string> GetCandidateRoots() => roots;
}

internal sealed class SteamInstallationLocator(ISteamRootProvider rootProvider) : ISteamInstallationLocator
{
    public SteamDiscoveryResult Locate()
    {
        var installations = new List<SteamInstallation>();
        var errors = new List<string>();
        var visitedManifests = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidateRoot in rootProvider.GetCandidateRoots())
        {
            string steamRoot;
            try
            {
                steamRoot = Path.GetFullPath(candidateRoot);
            }
            catch (Exception exception)
            {
                errors.Add($"Steam root inválida '{candidateRoot}': {exception.Message}");
                continue;
            }

            var steamExecutable = Path.Combine(steamRoot, "steam.exe");
            if (!File.Exists(steamExecutable))
            {
                errors.Add($"steam.exe não encontrado em '{steamRoot}'.");
                continue;
            }

            foreach (var library in DiscoverLibraries(steamRoot, errors))
            {
                var manifestPath = Path.GetFullPath(Path.Combine(library, "steamapps", $"appmanifest_{SupportedBuild.SteamAppId}.acf"));
                if (!visitedManifests.Add(manifestPath) || !File.Exists(manifestPath))
                {
                    continue;
                }

                try
                {
                    installations.Add(ParseManifest(steamRoot, steamExecutable, library, manifestPath));
                }
                catch (Exception exception)
                {
                    errors.Add($"Manifesto Steam inválido '{manifestPath}': {exception.Message}");
                }
            }
        }

        if (installations.Count == 0 && errors.Count == 0)
        {
            errors.Add($"O aplicativo Steam {SupportedBuild.SteamAppId} não foi encontrado.");
        }

        return new SteamDiscoveryResult(installations, errors);
    }

    private static IEnumerable<string> DiscoverLibraries(string steamRoot, ICollection<string> errors)
    {
        var libraries = new List<string> { steamRoot };
        var libraryFile = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(libraryFile))
        {
            return libraries;
        }

        try
        {
            var root = ValveKeyValues.Parse(File.ReadAllText(libraryFile));
            var libraryFolders = root.Child("libraryfolders") ?? root;

            foreach (var value in libraryFolders.Values)
            {
                if (int.TryParse(value.Key, out _))
                {
                    libraries.Add(value.Value);
                }
            }

            foreach (var child in libraryFolders.Children)
            {
                if (int.TryParse(child.Key, out _) && child.Value.Value("path") is { Length: > 0 } path)
                {
                    libraries.Add(path);
                }
            }
        }
        catch (Exception exception)
        {
            errors.Add($"Não foi possível ler '{libraryFile}': {exception.Message}");
        }

        return libraries
            .Where(Directory.Exists)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static SteamInstallation ParseManifest(
        string steamRoot,
        string steamExecutable,
        string libraryRoot,
        string manifestPath)
    {
        var root = ValveKeyValues.Parse(File.ReadAllText(manifestPath));
        var appState = root.Child("AppState") ?? throw new FormatException("Bloco AppState ausente.");
        var appId = appState.Value("appid") ?? throw new FormatException("appid ausente.");
        if (!string.Equals(appId, SupportedBuild.SteamAppId, StringComparison.Ordinal))
        {
            throw new FormatException($"appid inesperado: {appId}.");
        }

        var installDirectory = appState.Value("installdir") ?? throw new FormatException("installdir ausente.");
        if (Path.IsPathRooted(installDirectory))
        {
            throw new FormatException("installdir não pode ser absoluto.");
        }

        var commonRoot = Path.GetFullPath(Path.Combine(libraryRoot, "steamapps", "common"));
        var gamePath = Path.GetFullPath(Path.Combine(commonRoot, installDirectory));
        EnsureContained(commonRoot, gamePath);

        var buildId = appState.Value("buildid") ?? string.Empty;
        var targetBuildId = appState.Value("TargetBuildID");
        var updatePending =
            (!string.IsNullOrWhiteSpace(targetBuildId) && !string.Equals(buildId, targetBuildId, StringComparison.Ordinal)) ||
            CountersDiffer(appState, "BytesToDownload", "BytesDownloaded") ||
            CountersDiffer(appState, "BytesToStage", "BytesStaged");

        return new SteamInstallation(
            steamRoot,
            steamExecutable,
            Path.GetFullPath(libraryRoot),
            manifestPath,
            gamePath,
            appId,
            buildId,
            updatePending);
    }

    private static bool CountersDiffer(ValveKeyValuesNode node, string expectedName, string actualName)
    {
        var expected = node.Value(expectedName);
        var actual = node.Value(actualName);
        return expected is not null && actual is not null && !string.Equals(expected, actual, StringComparison.Ordinal);
    }

    internal static void EnsureContained(string parent, string child)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(parent), Path.GetFullPath(child));
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException($"O caminho '{child}' está fora de '{parent}'.");
        }
    }
}
