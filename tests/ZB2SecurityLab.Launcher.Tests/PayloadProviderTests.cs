using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Launcher.Services;

namespace ZB2SecurityLab.Launcher.Tests;

[TestClass]
public sealed class PayloadProviderTests
{
    [TestMethod]
    public void Load_VerifiesAndBuildsTheActualEmbeddedPayload()
    {
        using var temp = new TemporaryDirectory();

        var payload = new EmbeddedPayloadProvider().Load(false, new LocalDataPaths(temp.Path));

        Assert.IsTrue(payload.Files.Any(file => file.RelativePath == @"BepInEx\core\BepInEx.dll"));
        Assert.IsTrue(payload.Files.Any(file => file.RelativePath == @"BepInEx\plugins\ZB2SecurityLab\ZB2SecurityLab.Core.dll"));
        Assert.IsTrue(payload.Files.Any(file => file.RelativePath == @"BepInEx\plugins\ZB2SecurityLab\ZB2SecurityLab.Plugin.dll"));
        Assert.IsFalse(payload.Files.Any(file => file.RelativePath.Contains("changelog", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ExtractLoaderFiles_IncludesOnlyRequiredLoaderTree()
    {
        var archive = CreateArchive(new Dictionary<string, string>
        {
            [".doorstop_version"] = "4",
            ["doorstop_config.ini"] = "enabled=true",
            ["winhttp.dll"] = "binary",
            ["BepInEx/core/BepInEx.dll"] = "core",
            ["changelog.txt"] = "not deployed"
        });

        var files = EmbeddedPayloadProvider.ExtractLoaderFiles(archive);

        Assert.AreEqual(4, files.Count);
        Assert.IsFalse(files.Any(file => file.RelativePath.Contains("changelog", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ExtractLoaderFiles_RejectsMissingRequiredEntry()
    {
        var archive = CreateArchive(new Dictionary<string, string> { ["winhttp.dll"] = "binary" });

        Assert.ThrowsException<InvalidDataException>(() => EmbeddedPayloadProvider.ExtractLoaderFiles(archive));
    }

    [TestMethod]
    public void ExtractLoaderFiles_RejectsTraversalEntry()
    {
        var archive = CreateArchive(new Dictionary<string, string>
        {
            [".doorstop_version"] = "4",
            ["doorstop_config.ini"] = "enabled=true",
            ["winhttp.dll"] = "binary",
            ["BepInEx/core/BepInEx.dll"] = "core",
            ["BepInEx/../evil.dll"] = "evil"
        });

        Assert.ThrowsException<InvalidDataException>(() => EmbeddedPayloadProvider.ExtractLoaderFiles(archive));
    }

    [TestMethod]
    public void BuildPluginConfig_DefaultsAuthorizedMultiplayerOff()
    {
        using var temp = new TemporaryDirectory();
        var paths = new LocalDataPaths(temp.Path);
        var text = Encoding.UTF8.GetString(EmbeddedPayloadProvider.BuildPluginConfig(false, paths));

        StringAssert.Contains(text, "[AuthorizedMultiplayer]");
        StringAssert.Contains(text, "Enabled = false");
        StringAssert.Contains(text, $"OutputDirectory = {paths.PluginLogs}");
        StringAssert.Contains(text, "ToggleShortcut = F8");
    }

    [TestMethod]
    public void BuildPluginConfig_EnablesMutationsOnlyWhenRequested()
    {
        using var temp = new TemporaryDirectory();
        var text = Encoding.UTF8.GetString(EmbeddedPayloadProvider.BuildPluginConfig(true, new LocalDataPaths(temp.Path)));

        var controlledSection = text[(text.IndexOf("[ControlledMutations]", StringComparison.Ordinal))..];
        StringAssert.StartsWith(controlledSection, "[ControlledMutations]");
        StringAssert.Contains(controlledSection, "Enabled = true");
    }

    [TestMethod]
    public void Hash_IsStableSha256()
    {
        Assert.AreEqual(
            "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD",
            EmbeddedPayloadProvider.Hash(Encoding.UTF8.GetBytes("abc")));
    }

    private static byte[] CreateArchive(IReadOnlyDictionary<string, string> entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var item in entries)
            {
                var entry = archive.CreateEntry(item.Key);
                using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                writer.Write(item.Value);
            }
        }

        return stream.ToArray();
    }
}
