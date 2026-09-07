using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Launcher.Domain;
using ZB2SecurityLab.Launcher.Services;
using ZB2SecurityLab.Launcher.ViewModels;

namespace ZB2SecurityLab.Launcher.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public void Constructor_StartsWithMutationModeDisabled()
    {
        using var temp = new TemporaryDirectory();
        var viewModel = CreateViewModel(temp, Array.Empty<SteamInstallation>());

        Assert.IsFalse(viewModel.MutationEnabled);
        Assert.IsFalse(viewModel.CanLaunch);
    }

    [TestMethod]
    public async Task Initialize_EnablesLaunchForSupportedInstallation()
    {
        using var temp = new TemporaryDirectory();
        var installation = Installation(temp.Path);
        var viewModel = CreateViewModel(temp, new[] { installation });

        await viewModel.InitializeAsync();

        Assert.AreEqual(LauncherState.Ready, viewModel.State);
        Assert.IsTrue(viewModel.CanLaunch);
        Assert.AreEqual(installation.GamePath, viewModel.GamePath);
        Assert.IsFalse(viewModel.HasError);
    }

    [TestMethod]
    public async Task Initialize_BlocksWhenSteamInstallationIsMissing()
    {
        using var temp = new TemporaryDirectory();
        var viewModel = CreateViewModel(temp, Array.Empty<SteamInstallation>());

        await viewModel.InitializeAsync();

        Assert.AreEqual(LauncherState.Blocked, viewModel.State);
        Assert.IsFalse(viewModel.CanLaunch);
        Assert.IsTrue(viewModel.HasError);
    }

    private static MainViewModel CreateViewModel(TemporaryDirectory temp, IReadOnlyList<SteamInstallation> installations)
    {
        var dataPaths = new LocalDataPaths(temp.Combine("data"));
        return new MainViewModel(
            dataPaths,
            new FakeLocator(installations),
            new AlwaysValidBuildValidator(),
            new FakeSteamLauncher(),
            new GameProcessMonitor(new FakeProcessCatalog()));
    }

    private static SteamInstallation Installation(string root)
        => new(
            root,
            Path.Combine(root, "steam.exe"),
            root,
            Path.Combine(root, "manifest.acf"),
            Path.Combine(root, "game"),
            SupportedBuild.SteamAppId,
            SupportedBuild.BuildId,
            false);

    private sealed class FakeLocator(IReadOnlyList<SteamInstallation> installations) : ISteamInstallationLocator
    {
        public SteamDiscoveryResult Locate()
            => new(installations, installations.Count == 0 ? new[] { "not found" } : Array.Empty<string>());
    }

    private sealed class FakeSteamLauncher : ISteamGameLauncher
    {
        public void Launch(SteamInstallation installation)
        {
        }
    }
}
