using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Launcher.Domain;
using ZB2SecurityLab.Launcher.Services;
using ZB2SecurityLab.Launcher.Transactions;

namespace ZB2SecurityLab.Launcher.Tests;

[TestClass]
public sealed class TransactionTests
{
    [TestMethod]
    public void Install_CreatesPayloadAndSessionMarkerWithoutChangingOriginals()
    {
        using var fixture = new TransactionFixture();
        var original = File.ReadAllText(fixture.OriginalFile);

        fixture.Install();

        Assert.AreEqual(JournalPhase.Installed, fixture.Journal.Phase);
        Assert.IsTrue(File.Exists(Path.Combine(fixture.GamePath, "winhttp.dll")));
        Assert.AreEqual(fixture.Journal.SessionId, File.ReadAllText(Path.Combine(fixture.GamePath, PayloadConstants.MarkerRelativePath)));
        Assert.AreEqual(original, File.ReadAllText(fixture.OriginalFile));
    }

    [TestMethod]
    public void Install_RefusesExistingBepInExAndWritesNothingElse()
    {
        using var fixture = new TransactionFixture();
        Directory.CreateDirectory(Path.Combine(fixture.GamePath, "BepInEx"));

        Assert.ThrowsException<IOException>(() => fixture.Install());
        Assert.IsFalse(File.Exists(Path.Combine(fixture.GamePath, "winhttp.dll")));
    }

    [TestMethod]
    public void Install_RefusesExistingRootFileWithoutOverwritingIt()
    {
        using var fixture = new TransactionFixture();
        var rootFile = Path.Combine(fixture.GamePath, "winhttp.dll");
        File.WriteAllText(rootFile, "user-owned");

        Assert.ThrowsException<IOException>(() => fixture.Install());
        Assert.AreEqual("user-owned", File.ReadAllText(rootFile));
    }

    [TestMethod]
    public void Cleanup_RemovesOwnedPayloadAndKeepsOriginalsByteIdentical()
    {
        using var fixture = new TransactionFixture();
        var original = File.ReadAllBytes(fixture.OriginalFile);
        fixture.Install();

        var result = fixture.Cleanup();

        Assert.IsTrue(result);
        Assert.AreEqual(JournalPhase.Completed, fixture.Journal.Phase);
        Assert.IsFalse(Directory.Exists(Path.Combine(fixture.GamePath, "BepInEx")));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.GamePath, "winhttp.dll")));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(fixture.OriginalFile));
    }

    [TestMethod]
    public void Cleanup_IsIdempotent()
    {
        using var fixture = new TransactionFixture();
        fixture.Install();

        Assert.IsTrue(fixture.Cleanup());
        Assert.IsTrue(fixture.Cleanup());
    }

    [TestMethod]
    public void Cleanup_PreservesAlteredRootFileAndRequiresRecovery()
    {
        using var fixture = new TransactionFixture();
        fixture.Install();
        File.WriteAllText(Path.Combine(fixture.GamePath, "winhttp.dll"), "changed-after-install");

        var result = fixture.Cleanup();

        Assert.IsFalse(result);
        Assert.IsTrue(File.Exists(Path.Combine(fixture.GamePath, "winhttp.dll")));
        Assert.AreEqual(JournalPhase.RecoveryRequired, fixture.Journal.Phase);
        Assert.IsTrue(fixture.Journal.Errors.Any(error => error.StartsWith("ROOT_FILE_CHANGED", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Cleanup_PreservesBepInExWhenMarkerDoesNotMatch()
    {
        using var fixture = new TransactionFixture();
        fixture.Install();
        File.WriteAllText(Path.Combine(fixture.GamePath, PayloadConstants.MarkerRelativePath), "foreign-session");

        Assert.IsFalse(fixture.Cleanup());
        Assert.IsTrue(Directory.Exists(Path.Combine(fixture.GamePath, "BepInEx")));
    }

    [TestMethod]
    public void Cleanup_RejectsRootPathNotInThePayloadAllowlist()
    {
        using var fixture = new TransactionFixture();
        var unexpected = Path.Combine(fixture.GamePath, "original-game-file.bin");
        fixture.Journal.CreatedFiles.Add("original-game-file.bin");
        fixture.Journal.RootFileHashes["original-game-file.bin"] = EmbeddedPayloadProvider.Hash(File.ReadAllBytes(unexpected));

        Assert.IsFalse(fixture.Cleanup());
        Assert.AreEqual("original", File.ReadAllText(unexpected));
        Assert.IsTrue(fixture.Journal.Errors.Any(error => error.StartsWith("UNSAFE_ROOT_JOURNAL_ENTRY", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Cleanup_ArchivesLoaderLogBeforeRemovingBepInEx()
    {
        using var fixture = new TransactionFixture();
        fixture.Install();
        File.WriteAllText(Path.Combine(fixture.GamePath, "BepInEx", "LogOutput.log"), "loader evidence");

        Assert.IsTrue(fixture.Cleanup());
        Assert.AreEqual("loader evidence", File.ReadAllText(Path.Combine(fixture.Paths.LoaderLogs, $"{fixture.Journal.SessionId}.log")));
    }

    [TestMethod]
    public void JournalStore_ReportsPendingAndIgnoresCompleted()
    {
        using var fixture = new TransactionFixture();
        fixture.Store.Save(fixture.Journal);
        Assert.AreEqual(1, fixture.Store.PendingJournalPaths().Count);

        fixture.Journal.Phase = JournalPhase.Completed;
        fixture.Store.Save(fixture.Journal);

        Assert.AreEqual(0, fixture.Store.PendingJournalPaths().Count);
    }

    [TestMethod]
    public void JournalStore_TreatsCorruptJournalAsPending()
    {
        using var fixture = new TransactionFixture();
        fixture.Paths.EnsureCreated();
        File.WriteAllText(fixture.Paths.JournalPath("corrupt"), "not-json");

        Assert.AreEqual(1, fixture.Store.PendingJournalPaths().Count);
    }

    [TestMethod]
    public void ProbeWriteAccess_LeavesNoFileBehind()
    {
        using var fixture = new TransactionFixture();

        TransientInstaller.ProbeWriteAccess(fixture.GamePath);

        Assert.IsFalse(Directory.EnumerateFiles(fixture.GamePath).Any(path => Path.GetFileName(path).StartsWith(".zb2securitylab-write-probe", StringComparison.Ordinal)));
    }

    private sealed class TransactionFixture : IDisposable
    {
        private readonly TemporaryDirectory _temp = new();

        internal TransactionFixture()
        {
            GamePath = _temp.Combine("game");
            Directory.CreateDirectory(GamePath);
            OriginalFile = Path.Combine(GamePath, "original-game-file.bin");
            File.WriteAllText(OriginalFile, "original");
            Paths = new LocalDataPaths(_temp.Combine("data"));
            Store = new JournalStore(Paths);
            Journal = new LaunchSessionJournal
            {
                SessionId = Guid.NewGuid().ToString("N"),
                Nonce = "nonce",
                GamePath = GamePath,
                ExecutablePath = OriginalFile,
                DataRoot = Paths.Root,
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            Payload = new PayloadPackage(new[]
            {
                PayloadFile(".doorstop_version", "4"),
                PayloadFile("doorstop_config.ini", "enabled=true"),
                PayloadFile("winhttp.dll", "proxy"),
                PayloadFile(@"BepInEx\core\BepInEx.dll", "core"),
                PayloadFile(@"BepInEx\plugins\ZB2SecurityLab\ZB2SecurityLab.Plugin.dll", "plugin")
            });
        }

        internal string GamePath { get; }
        internal string OriginalFile { get; }
        internal LocalDataPaths Paths { get; }
        internal JournalStore Store { get; }
        internal LaunchSessionJournal Journal { get; }
        internal PayloadPackage Payload { get; }

        internal void Install() => new TransientInstaller().Install(Journal, Payload, Store);
        internal bool Cleanup() => new CleanupCoordinator(new AlwaysValidBuildValidator()).Cleanup(Journal, Store, Paths);

        private static PayloadFile PayloadFile(string path, string contents)
        {
            var bytes = Encoding.UTF8.GetBytes(contents);
            return new PayloadFile(path, bytes, EmbeddedPayloadProvider.Hash(bytes));
        }

        public void Dispose() => _temp.Dispose();
    }
}
