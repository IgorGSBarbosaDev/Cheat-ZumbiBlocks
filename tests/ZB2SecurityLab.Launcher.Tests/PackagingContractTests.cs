using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Launcher.Services;

namespace ZB2SecurityLab.Launcher.Tests;

[TestClass]
public sealed class PackagingContractTests
{
    [TestMethod]
    public void LauncherProject_DeclaresSingleFileSelfContainedWinX64Publish()
    {
        var project = File.ReadAllText(Path.Combine(
            TestPaths.RepositoryRoot(),
            "src",
            "ZB2SecurityLab.Launcher",
            "ZB2SecurityLab.Launcher.csproj"));

        StringAssert.Contains(project, "<TargetFramework>net8.0-windows</TargetFramework>");
        StringAssert.Contains(project, "<UseWPF>true</UseWPF>");
        StringAssert.Contains(project, "<RuntimeIdentifier>win-x64</RuntimeIdentifier>");
        StringAssert.Contains(project, "<SelfContained>true</SelfContained>");
        StringAssert.Contains(project, "<PublishSingleFile>true</PublishSingleFile>");
        StringAssert.Contains(project, "<PublishTrimmed>false</PublishTrimmed>");
        StringAssert.Contains(project, "ZB2SecurityLab.THIRD-PARTY-NOTICES.md");
    }

    [TestMethod]
    public void PublishScript_RequiresKnownBepInExAndDoesNotDownload()
    {
        var script = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot(), "scripts", "Publish-Launcher.ps1"));

        StringAssert.Contains(script, PayloadConstants.BepInExArchiveSha256);
        StringAssert.Contains(script, "RequireLauncherPayload=true");
        Assert.IsFalse(script.Contains("Invoke-WebRequest", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(script.Contains("Start-BitsTransfer", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void LauncherSource_DoesNotIntroduceGameplayWriteTargets()
    {
        var launcherRoot = Path.Combine(TestPaths.RepositoryRoot(), "src", "ZB2SecurityLab.Launcher");
        var source = string.Join(Environment.NewLine, Directory.EnumerateFiles(launcherRoot, "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));

        Assert.IsFalse(source.Contains("FOVController.UserDefinedFOV", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("staminaFast", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("staminaSlow", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("InventoryItem.ammo", StringComparison.Ordinal));
        Assert.IsFalse(source.Contains("Harmony", StringComparison.Ordinal));
    }
}
