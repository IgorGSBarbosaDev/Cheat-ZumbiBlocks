using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Launcher.Domain;
using ZB2SecurityLab.Launcher.Services;

namespace ZB2SecurityLab.Launcher.Tests;

[TestClass]
public sealed class SteamInstallationLocatorTests
{
    [TestMethod]
    public void Locate_FindsManifestInMainLibrary()
    {
        using var fixture = new SteamFixture();
        fixture.WriteManifest(fixture.SteamRoot);

        var result = fixture.Locate();

        Assert.AreEqual(1, result.Installations.Count);
        Assert.AreEqual(fixture.GamePath(fixture.SteamRoot), result.Installations[0].GamePath);
        Assert.IsFalse(result.Installations[0].UpdatePending);
    }

    [TestMethod]
    public void Locate_FindsManifestInAdditionalModernLibrary()
    {
        using var fixture = new SteamFixture();
        var library = fixture.Temp.Combine("Second Library");
        Directory.CreateDirectory(library);
        fixture.WriteLibraryFolders($"\"1\" {{ \"path\" \"{Escape(library)}\" }}");
        fixture.WriteManifest(library);

        var result = fixture.Locate();

        Assert.AreEqual(1, result.Installations.Count);
        Assert.AreEqual(Path.GetFullPath(library), result.Installations[0].LibraryRoot);
    }

    [TestMethod]
    public void Locate_FindsManifestInLegacyLibraryFormat()
    {
        using var fixture = new SteamFixture();
        var library = fixture.Temp.Combine("Legacy");
        Directory.CreateDirectory(library);
        fixture.WriteLibraryFolders($"\"1\" \"{Escape(library)}\"");
        fixture.WriteManifest(library);

        Assert.AreEqual(1, fixture.Locate().Installations.Count);
    }

    [TestMethod]
    public void Locate_DeduplicatesRepeatedLibraries()
    {
        using var fixture = new SteamFixture();
        fixture.WriteLibraryFolders($"\"1\" {{ \"path\" \"{Escape(fixture.SteamRoot)}\" }}");
        fixture.WriteManifest(fixture.SteamRoot);

        Assert.AreEqual(1, fixture.Locate().Installations.Count);
    }

    [TestMethod]
    public void Locate_ReportsWrongAppId()
    {
        using var fixture = new SteamFixture();
        fixture.WriteManifest(fixture.SteamRoot, appId: "999");

        var result = fixture.Locate();

        Assert.AreEqual(0, result.Installations.Count);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("appid inesperado", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Locate_RejectsInstallDirectoryTraversal()
    {
        using var fixture = new SteamFixture();
        fixture.WriteManifest(fixture.SteamRoot, installDirectory: @"..\outside");

        Assert.AreEqual(0, fixture.Locate().Installations.Count);
    }

    [TestMethod]
    public void Locate_MarksDifferentTargetBuildAsPending()
    {
        using var fixture = new SteamFixture();
        fixture.WriteManifest(fixture.SteamRoot, targetBuildId: "999999");

        Assert.IsTrue(fixture.Locate().Installations.Single().UpdatePending);
    }

    [TestMethod]
    public void Locate_MarksIncompleteDownloadAsPending()
    {
        using var fixture = new SteamFixture();
        fixture.WriteManifest(fixture.SteamRoot, bytesDownloaded: "50");

        Assert.IsTrue(fixture.Locate().Installations.Single().UpdatePending);
    }

    [TestMethod]
    public void Locate_ContinuesWithMainLibraryWhenLibraryFileIsMalformed()
    {
        using var fixture = new SteamFixture();
        fixture.WriteLibraryFolders("\"libraryfolders\" {");
        fixture.WriteManifest(fixture.SteamRoot);

        var result = fixture.Locate();

        Assert.AreEqual(1, result.Installations.Count);
        Assert.IsTrue(result.Errors.Count > 0);
    }

    [TestMethod]
    public void Locate_ReportsMissingSteamExecutable()
    {
        using var temp = new TemporaryDirectory();
        var result = new SteamInstallationLocator(new FixedSteamRootProvider(temp.Path)).Locate();

        Assert.AreEqual(0, result.Installations.Count);
        Assert.IsTrue(result.Errors.Any(error => error.Contains("steam.exe", StringComparison.Ordinal)));
    }

    private static string Escape(string path) => path.Replace("\\", "\\\\", StringComparison.Ordinal);

    private sealed class SteamFixture : IDisposable
    {
        internal SteamFixture()
        {
            Temp = new TemporaryDirectory();
            SteamRoot = Temp.Combine("Steam");
            Directory.CreateDirectory(Path.Combine(SteamRoot, "steamapps"));
            File.WriteAllBytes(Path.Combine(SteamRoot, "steam.exe"), Array.Empty<byte>());
        }

        internal TemporaryDirectory Temp { get; }
        internal string SteamRoot { get; }

        internal void WriteLibraryFolders(string body)
            => File.WriteAllText(Path.Combine(SteamRoot, "steamapps", "libraryfolders.vdf"), $"\"libraryfolders\" {{ {body} }}");

        internal void WriteManifest(
            string library,
            string appId = SupportedBuild.SteamAppId,
            string installDirectory = "Zumbi Blocks 2 Open Alpha",
            string targetBuildId = SupportedBuild.BuildId,
            string bytesDownloaded = "100")
        {
            Directory.CreateDirectory(Path.Combine(library, "steamapps"));
            File.WriteAllText(Path.Combine(library, "steamapps", $"appmanifest_{SupportedBuild.SteamAppId}.acf"), $$"""
                "AppState"
                {
                    "appid" "{{appId}}"
                    "installdir" "{{installDirectory}}"
                    "buildid" "{{SupportedBuild.BuildId}}"
                    "TargetBuildID" "{{targetBuildId}}"
                    "BytesToDownload" "100"
                    "BytesDownloaded" "{{bytesDownloaded}}"
                    "BytesToStage" "100"
                    "BytesStaged" "100"
                }
                """);
            Directory.CreateDirectory(GamePath(library));
        }

        internal string GamePath(string library)
            => Path.GetFullPath(Path.Combine(library, "steamapps", "common", "Zumbi Blocks 2 Open Alpha"));

        internal SteamDiscoveryResult Locate()
            => new SteamInstallationLocator(new FixedSteamRootProvider(SteamRoot)).Locate();

        public void Dispose() => Temp.Dispose();
    }
}
