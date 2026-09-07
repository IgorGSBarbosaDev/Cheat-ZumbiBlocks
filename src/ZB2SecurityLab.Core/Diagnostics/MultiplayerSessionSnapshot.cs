using System;
using System.Globalization;

namespace ZB2SecurityLab.Core.Diagnostics;

public enum NetworkRole
{
    UNKNOWN,
    OFFLINE,
    SINGLE_PLAYER,
    HOST,
    CLIENT
}

public sealed class MultiplayerSessionSnapshot
{
    public NetworkRole Role { get; set; } = NetworkRole.UNKNOWN;

    public string ConnectionState { get; set; } = "UNKNOWN";

    public bool IsMultiplayer { get; set; }

    public bool ServerStarted { get; set; }

    public bool ServerMultiplayerMode { get; set; }

    public bool ServerSinglePlayerMode { get; set; }

    public bool ClientConnected { get; set; }

    public bool ClientMatchmakingConnected { get; set; }

    public bool ServerLobbyLaunched { get; set; }

    public bool FriendsOnlySignal { get; set; }

    public bool ServerConnectionResolved { get; set; }

    public string? SteamLobbyId { get; set; }

    public string? ServerSteamId { get; set; }

    public string? LobbyOwnerSteamId { get; set; }

    public string? GameServerSteamId { get; set; }

    public string? ServerConnectionSteamId { get; set; }

    public string? LocalSteamId { get; set; }

    public int? LocalLobbyPlayerId { get; set; }

    public string? LobbyRegion { get; set; }

    public string? LobbyVersion { get; set; }

    public string CreateToken(string buildId, string playerToken)
    {
        if (string.IsNullOrWhiteSpace(buildId))
        {
            throw new ArgumentException("Build ID is required.", nameof(buildId));
        }

        if (string.IsNullOrWhiteSpace(playerToken))
        {
            throw new ArgumentException("Player token is required.", nameof(playerToken));
        }

        return string.Join(
            "|",
            buildId,
            Role.ToString(),
            SteamLobbyId ?? "NONE",
            ServerSteamId ?? "NONE",
            LobbyOwnerSteamId ?? "NONE",
            LocalSteamId ?? "NONE",
            LocalLobbyPlayerId?.ToString(CultureInfo.InvariantCulture) ?? "NONE",
            playerToken);
    }
}
