using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ZB2SecurityLab.Launcher.Domain;

namespace ZB2SecurityLab.Launcher.Services;

internal static class PayloadConstants
{
    internal const string BepInExVersion = "5.4.23.5";
    internal const string BepInExArchiveSha256 = "82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4";
    internal const string BepInExResource = "ZB2SecurityLab.Payload.BepInEx.zip";
    internal const string CoreResource = "ZB2SecurityLab.Payload.ZB2SecurityLab.Core.dll";
    internal const string PluginResource = "ZB2SecurityLab.Payload.ZB2SecurityLab.Plugin.dll";
    internal const string MarkerRelativePath = @"BepInEx\.zb2securitylab-session";
}

internal interface IPayloadProvider
{
    PayloadPackage Load(bool mutationEnabled, LocalDataPaths dataPaths);
}

internal sealed class EmbeddedPayloadProvider(Assembly? assembly = null) : IPayloadProvider
{
    private readonly Assembly _assembly = assembly ?? Assembly.GetExecutingAssembly();

    public PayloadPackage Load(bool mutationEnabled, LocalDataPaths dataPaths)
    {
        var archiveBytes = ReadResource(PayloadConstants.BepInExResource);
        var archiveHash = Hash(archiveBytes);
        if (!string.Equals(archiveHash, PayloadConstants.BepInExArchiveSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"BepInEx incorporado possui hash inesperado: {archiveHash}.");
        }

        var files = ExtractLoaderFiles(archiveBytes);
        Add(files, @"BepInEx\plugins\ZB2SecurityLab\ZB2SecurityLab.Core.dll", ReadResource(PayloadConstants.CoreResource));
        Add(files, @"BepInEx\plugins\ZB2SecurityLab\ZB2SecurityLab.Plugin.dll", ReadResource(PayloadConstants.PluginResource));
        Add(files, @"BepInEx\config\com.igorgsbarbosa.zb2securitylab.cfg", BuildPluginConfig(mutationEnabled, dataPaths));
        return new PayloadPackage(files.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    internal static List<PayloadFile> ExtractLoaderFiles(byte[] archiveBytes)
    {
        var files = new List<PayloadFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var stream = new MemoryStream(archiveBytes, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name))
            {
                continue;
            }

            var relativePath = NormalizeArchivePath(entry.FullName);
            var isRootLoader = relativePath is ".doorstop_version" or "doorstop_config.ini" or "winhttp.dll";
            if (!isRootLoader && !relativePath.StartsWith("BepInEx\\", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!seen.Add(relativePath))
            {
                throw new InvalidDataException($"Entrada duplicada no payload: {relativePath}.");
            }

            using var input = entry.Open();
            using var output = new MemoryStream();
            input.CopyTo(output);
            Add(files, relativePath, output.ToArray());
        }

        foreach (var required in new[] { ".doorstop_version", "doorstop_config.ini", "winhttp.dll", @"BepInEx\core\BepInEx.dll" })
        {
            if (!seen.Contains(required))
            {
                throw new InvalidDataException($"Arquivo obrigatório ausente no payload: {required}.");
            }
        }

        return files;
    }

    internal static string NormalizeArchivePath(string entryPath)
    {
        var normalized = entryPath.Replace('/', '\\');
        if (Path.IsPathRooted(normalized) || normalized.Split('\\').Any(segment => segment is ".." or "." or ""))
        {
            throw new InvalidDataException($"Caminho inseguro no payload: {entryPath}.");
        }

        var sandbox = Path.Combine(Path.GetTempPath(), "zb2securitylab-payload-root");
        var resolved = Path.GetFullPath(Path.Combine(sandbox, normalized));
        SteamInstallationLocator.EnsureContained(sandbox, resolved);
        return normalized;
    }

    internal static byte[] BuildPluginConfig(bool mutationEnabled, LocalDataPaths dataPaths)
    {
        var text = $"""
            [AuthorizedMultiplayer]
            Enabled = false
            GrantPath = {dataPaths.GrantPath}

            [ControlledMutations]
            Enabled = {mutationEnabled.ToString().ToLowerInvariant()}

            [Diagnostics]
            OutputDirectory = {dataPaths.PluginLogs}
            ToggleShortcut = F8
            """;
        return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text + Environment.NewLine);
    }

    internal static string Hash(byte[] contents) => Convert.ToHexString(SHA256.HashData(contents));

    private byte[] ReadResource(string name)
    {
        using var stream = _assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Recurso incorporado ausente: {name}.");
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    private static void Add(ICollection<PayloadFile> files, string relativePath, byte[] contents)
        => files.Add(new PayloadFile(relativePath, contents, Hash(contents)));
}
