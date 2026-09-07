using System.Reflection.PortableExecutable;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Launcher.Domain;

namespace ZB2SecurityLab.Launcher.Services;

internal interface IBuildValidator
{
    BuildValidationResult Validate(SteamInstallation installation, bool checkLoaderConflicts = true);
    BuildValidationResult ValidateFiles(string gamePath);
}

internal sealed class BuildValidator : IBuildValidator
{
    private readonly Func<string, string, BuildFingerprintResult> _fingerprint;
    private readonly Func<string, bool> _isAmd64;

    internal static readonly string[] LoaderConflictPaths =
    {
        "BepInEx",
        ".doorstop_version",
        "doorstop_config.ini",
        "winhttp.dll",
        "MelonLoader"
    };

    internal BuildValidator(
        Func<string, string, BuildFingerprintResult>? fingerprint = null,
        Func<string, bool>? isAmd64 = null)
    {
        _fingerprint = fingerprint ?? BuildFingerprint.Verify;
        _isAmd64 = isAmd64 ?? IsAmd64PortableExecutable;
    }

    public BuildValidationResult Validate(SteamInstallation installation, bool checkLoaderConflicts = true)
    {
        var errors = new List<string>();
        if (!string.Equals(installation.AppId, SupportedBuild.SteamAppId, StringComparison.Ordinal))
        {
            errors.Add($"AppID incompatível: {installation.AppId}.");
        }

        if (!string.Equals(installation.BuildId, SupportedBuild.BuildId, StringComparison.Ordinal))
        {
            errors.Add($"Build Steam incompatível: {installation.BuildId}.");
        }

        if (installation.UpdatePending)
        {
            errors.Add("A Steam indica uma atualização incompleta ou pendente.");
        }

        var files = ValidateFiles(installation.GamePath);
        errors.AddRange(files.Errors);

        if (checkLoaderConflicts)
        {
            foreach (var relativePath in LoaderConflictPaths)
            {
                if (File.Exists(Path.Combine(installation.GamePath, relativePath)) || Directory.Exists(Path.Combine(installation.GamePath, relativePath)))
                {
                    errors.Add($"Loader ou arquivo conflitante já existe: {relativePath}.");
                }
            }
        }

        return new BuildValidationResult(errors.Count == 0, files.Executable, files.AssemblyCSharp, errors);
    }

    public BuildValidationResult ValidateFiles(string gamePath)
    {
        var errors = new List<string>();
        var executablePath = ResolveGamePath(gamePath, SupportedBuild.ExecutableRelativePath);
        var assemblyPath = ResolveGamePath(gamePath, SupportedBuild.AssemblyCSharpRelativePath);
        var executable = _fingerprint(executablePath, SupportedBuild.ExecutableSha256);
        var assembly = _fingerprint(assemblyPath, SupportedBuild.AssemblyCSharpSha256);

        if (!executable.IsMatch)
        {
            errors.Add($"Fingerprint do executável incompatível: {executable.ActualSha256 ?? executable.Error}.");
        }

        if (!assembly.IsMatch)
        {
            errors.Add($"Fingerprint de Assembly-CSharp.dll incompatível: {assembly.ActualSha256 ?? assembly.Error}.");
        }

        var monoLibrary = ResolveGamePath(gamePath, SupportedBuild.MonoLibraryRelativePath);
        if (!File.Exists(monoLibrary))
        {
            errors.Add("Layout Unity Mono não encontrado (mscorlib.dll ausente).");
        }

        if (File.Exists(executablePath) && !_isAmd64(executablePath))
        {
            errors.Add("ZumbiBlocks2.exe não é um executável PE x64 compatível.");
        }

        return new BuildValidationResult(errors.Count == 0, executable, assembly, errors);
    }

    internal static string ResolveGamePath(string gamePath, string relativePath)
    {
        var root = Path.GetFullPath(gamePath);
        var target = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        SteamInstallationLocator.EnsureContained(root, target);
        return target;
    }

    private static bool IsAmd64PortableExecutable(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var reader = new PEReader(stream);
            return reader.PEHeaders.CoffHeader.Machine == System.Reflection.PortableExecutable.Machine.Amd64;
        }
        catch
        {
            return false;
        }
    }
}
