using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Launcher.Domain;
using ZB2SecurityLab.Launcher.Services;

namespace ZB2SecurityLab.Launcher.Tests;

[TestClass]
public sealed class GameProcessMonitorTests
{
    [TestMethod]
    public void Find_SelectsExactExecutablePath()
    {
        var expected = Path.GetFullPath(@"C:\Games\ZB2\ZumbiBlocks2.exe");
        var catalog = new FakeProcessCatalog(
            new ProcessSnapshot(1, Path.GetFullPath(@"D:\Other\ZumbiBlocks2.exe"), DateTimeOffset.UtcNow),
            new ProcessSnapshot(2, expected, DateTimeOffset.UtcNow));

        var result = new GameProcessMonitor(catalog).Find(expected, DateTimeOffset.MinValue);

        Assert.AreEqual(2, result!.Id);
    }

    [TestMethod]
    public void Find_IgnoresProcessesOlderThanLaunchWindow()
    {
        var expected = Path.GetFullPath(@"C:\Games\ZB2\ZumbiBlocks2.exe");
        var catalog = new FakeProcessCatalog(new ProcessSnapshot(1, expected, DateTimeOffset.UtcNow.AddMinutes(-5)));

        Assert.IsNull(new GameProcessMonitor(catalog).Find(expected, DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    [TestMethod]
    public void IsGameRunning_ReturnsFalseForOnlyHomonymousProcess()
    {
        var expected = Path.GetFullPath(@"C:\Games\ZB2\ZumbiBlocks2.exe");
        var catalog = new FakeProcessCatalog(new ProcessSnapshot(1, Path.GetFullPath(@"D:\Other\ZumbiBlocks2.exe"), DateTimeOffset.UtcNow));

        Assert.IsFalse(new GameProcessMonitor(catalog).IsGameRunning(expected));
    }

    [TestMethod]
    public void IsGameRunning_ReturnsTrueForExactProcess()
    {
        var expected = Path.GetFullPath(@"C:\Games\ZB2\ZumbiBlocks2.exe");
        var catalog = new FakeProcessCatalog(new ProcessSnapshot(1, expected, DateTimeOffset.UtcNow));

        Assert.IsTrue(new GameProcessMonitor(catalog).IsGameRunning(expected));
    }
}
