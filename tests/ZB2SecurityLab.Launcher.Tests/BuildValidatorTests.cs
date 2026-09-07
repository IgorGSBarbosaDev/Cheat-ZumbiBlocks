using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Launcher.Domain;
using ZB2SecurityLab.Launcher.Services;

namespace ZB2SecurityLab.Launcher.Tests;

[TestClass]
public sealed class BuildValidatorTests
{
    [TestMethod]
    public void Validate_AcceptsExactSupportedContract()
    {
        using var fixture = new BuildFixture();

        var result = fixture.Validator.Validate(fixture.Installation);

        Assert.IsTrue(result.IsSupported, string.Join(" ", result.Errors));
    }

    [TestMethod]
    public void Validate_BlocksWrongSteamBuild()
    {
        using var fixture = new BuildFixture();
        var installation = fixture.Installation with { BuildId = "other" };

        Assert.IsFalse(fixture.Validator.Validate(installation).IsSupported);
    }

    [TestMethod]
    public void Validate_BlocksPendingUpdate()
    {
        using var fixture = new BuildFixture();
        var installation = fixture.Installation with { UpdatePending = true };

        Assert.IsTrue(fixture.Validator.Validate(installation).Errors.Any(error => error.Contains("atualização", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Validate_BlocksExistingLoaderWithoutOverwritingIt()
    {
        using var fixture = new BuildFixture();
        var existing = Path.Combine(fixture.GamePath, "winhttp.dll");
        File.WriteAllText(existing, "user-owned");

        var result = fixture.Validator.Validate(fixture.Installation);

        Assert.IsFalse(result.IsSupported);
        Assert.AreEqual("user-owned", File.ReadAllText(existing));
    }

    [TestMethod]
    public void Validate_BlocksMissingMonoLayout()
    {
        using var fixture = new BuildFixture();
        File.Delete(Path.Combine(fixture.GamePath, SupportedBuild.MonoLibraryRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        Assert.IsFalse(fixture.Validator.Validate(fixture.Installation).IsSupported);
    }

    [TestMethod]
    public void Validate_BlocksNonAmd64Executable()
    {
        using var fixture = new BuildFixture(isAmd64: false);

        Assert.IsFalse(fixture.Validator.Validate(fixture.Installation).IsSupported);
    }

    [TestMethod]
    public void Validate_BlocksFingerprintMismatch()
    {
        using var fixture = new BuildFixture(fingerprintsMatch: false);

        Assert.IsFalse(fixture.Validator.Validate(fixture.Installation).IsSupported);
    }

    [TestMethod]
    public void ResolveGamePath_RejectsTraversal()
    {
        using var fixture = new BuildFixture();

        Assert.ThrowsException<InvalidOperationException>(() => BuildValidator.ResolveGamePath(fixture.GamePath, @"..\outside.bin"));
    }

    [TestMethod]
    public void VerifyBuildScript_UsesSharedContractValues()
    {
        var script = File.ReadAllText(Path.Combine(TestPaths.RepositoryRoot(), "scripts", "Verify-Build.ps1"));

        StringAssert.Contains(script, SupportedBuild.ExecutableSha256);
        StringAssert.Contains(script, SupportedBuild.AssemblyCSharpSha256);
    }

    private sealed class BuildFixture : IDisposable
    {
        private readonly TemporaryDirectory _temp = new();

        internal BuildFixture(bool fingerprintsMatch = true, bool isAmd64 = true)
        {
            GamePath = _temp.Combine("game");
            Directory.CreateDirectory(GamePath);
            Create(SupportedBuild.ExecutableRelativePath);
            Create(SupportedBuild.AssemblyCSharpRelativePath);
            Create(SupportedBuild.MonoLibraryRelativePath);
            Installation = new SteamInstallation(
                _temp.Path,
                _temp.Combine("steam.exe"),
                _temp.Path,
                _temp.Combine("manifest.acf"),
                GamePath,
                SupportedBuild.SteamAppId,
                SupportedBuild.BuildId,
                false);
            Validator = new BuildValidator(
                (path, expected) => new BuildFingerprintResult(path, expected, fingerprintsMatch ? expected : "BAD", null),
                _ => isAmd64);
        }

        internal string GamePath { get; }
        internal SteamInstallation Installation { get; }
        internal BuildValidator Validator { get; }

        private void Create(string relativePath)
        {
            var path = Path.Combine(GamePath, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "fixture");
        }

        public void Dispose() => _temp.Dispose();
    }
}
