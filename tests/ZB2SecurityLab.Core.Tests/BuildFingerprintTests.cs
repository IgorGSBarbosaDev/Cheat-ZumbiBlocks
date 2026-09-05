using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class BuildFingerprintTests
{
    [TestMethod]
    public void Verify_ReturnsMatch_ForExpectedHash()
    {
        var path = CreateTemporaryFile("abc");
        try
        {
            var result = BuildFingerprint.Verify(
                path,
                "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD");

            Assert.IsTrue(result.IsMatch);
            Assert.IsNull(result.Error);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void Verify_FailsClosed_ForMissingFile()
    {
        var result = BuildFingerprint.Verify(
            Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.bin"),
            new string('0', 64));

        Assert.IsFalse(result.IsMatch);
        Assert.IsNotNull(result.Error);
        Assert.IsNull(result.ActualSha256);
    }

    private static string CreateTemporaryFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"zb2-security-lab-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, contents);
        return path;
    }
}

