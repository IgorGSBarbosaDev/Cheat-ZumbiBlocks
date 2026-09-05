using System;
using System.IO;
using System.Security.Cryptography;

namespace ZB2SecurityLab.Core.Diagnostics;

public sealed class BuildFingerprintResult
{
    public BuildFingerprintResult(string path, string expectedSha256, string? actualSha256, string? error)
    {
        Path = path;
        ExpectedSha256 = expectedSha256;
        ActualSha256 = actualSha256;
        Error = error;
    }

    public string Path { get; }

    public string ExpectedSha256 { get; }

    public string? ActualSha256 { get; }

    public string? Error { get; }

    public bool IsMatch => Error is null && string.Equals(ExpectedSha256, ActualSha256, StringComparison.OrdinalIgnoreCase);
}

public static class BuildFingerprint
{
    public static BuildFingerprintResult Verify(string path, string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A file path is required.", nameof(path));
        }

        if (string.IsNullOrWhiteSpace(expectedSha256))
        {
            throw new ArgumentException("An expected SHA-256 is required.", nameof(expectedSha256));
        }

        try
        {
            using var stream = File.OpenRead(path);
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(stream);
            var actual = BitConverter.ToString(hash).Replace("-", string.Empty);
            return new BuildFingerprintResult(path, expectedSha256, actual, null);
        }
        catch (Exception exception)
        {
            return new BuildFingerprintResult(path, expectedSha256, null, exception.Message);
        }
    }
}

