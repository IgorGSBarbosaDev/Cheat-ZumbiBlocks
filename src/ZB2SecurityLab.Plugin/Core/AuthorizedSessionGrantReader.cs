using System;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Plugin.Core;

internal sealed class AuthorizedSessionGrantReader
{
    private string? _cachedPath;
    private DateTime _cachedWriteTimeUtc;
    private AuthorizedSessionGrantReadResult _cached = new(null, null);

    internal AuthorizedSessionGrantReadResult Read(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new AuthorizedSessionGrantReadResult(null, "GRANT_PATH_MISSING");
        }

        try
        {
            var absolutePath = Path.GetFullPath(path);
            if (!File.Exists(absolutePath))
            {
                _cachedPath = absolutePath;
                _cachedWriteTimeUtc = default;
                _cached = new AuthorizedSessionGrantReadResult(null, null);
                return _cached;
            }

            var writeTimeUtc = File.GetLastWriteTimeUtc(absolutePath);
            if (string.Equals(_cachedPath, absolutePath, StringComparison.OrdinalIgnoreCase) &&
                writeTimeUtc == _cachedWriteTimeUtc)
            {
                return _cached;
            }

            var document = JsonConvert.DeserializeObject<GrantDocument>(File.ReadAllText(absolutePath));
            if (document is null ||
                !Enum.TryParse(document.AllowedRole, ignoreCase: false, out AuthorizedMultiplayerRole allowedRole) ||
                !DateTimeOffset.TryParse(
                    document.ExpiresUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var expiresUtc))
            {
                return Cache(absolutePath, writeTimeUtc, null, "GRANT_FORMAT_INVALID");
            }

            var grant = new AuthorizedSessionGrant
            {
                SchemaVersion = document.SchemaVersion,
                BuildId = document.BuildId ?? string.Empty,
                SteamLobbyId = document.SteamLobbyId ?? string.Empty,
                ServerSteamId = document.ServerSteamId ?? string.Empty,
                AuthorizedLocalSteamId = document.AuthorizedLocalSteamId ?? string.Empty,
                AllowedRole = allowedRole,
                ExpiresUtc = expiresUtc,
                RunLabel = document.RunLabel ?? string.Empty
            };
            return Cache(absolutePath, writeTimeUtc, grant, null);
        }
        catch (Exception exception)
        {
            return Cache(path, default, null, $"GRANT_READ_FAILED:{exception.GetType().Name}");
        }
    }

    private AuthorizedSessionGrantReadResult Cache(
        string path,
        DateTime writeTimeUtc,
        AuthorizedSessionGrant? grant,
        string? error)
    {
        _cachedPath = path;
        _cachedWriteTimeUtc = writeTimeUtc;
        _cached = new AuthorizedSessionGrantReadResult(grant, error);
        return _cached;
    }

    private sealed class GrantDocument
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonProperty("buildId")]
        public string? BuildId { get; set; }

        [JsonProperty("steamLobbyId")]
        public string? SteamLobbyId { get; set; }

        [JsonProperty("serverSteamId")]
        public string? ServerSteamId { get; set; }

        [JsonProperty("authorizedLocalSteamId")]
        public string? AuthorizedLocalSteamId { get; set; }

        [JsonProperty("allowedRole")]
        public string? AllowedRole { get; set; }

        [JsonProperty("expiresUtc")]
        public string? ExpiresUtc { get; set; }

        [JsonProperty("runLabel")]
        public string? RunLabel { get; set; }
    }
}

internal sealed class AuthorizedSessionGrantReadResult
{
    internal AuthorizedSessionGrantReadResult(AuthorizedSessionGrant? grant, string? error)
    {
        Grant = grant;
        Error = error;
    }

    internal AuthorizedSessionGrant? Grant { get; }

    internal string? Error { get; }
}
