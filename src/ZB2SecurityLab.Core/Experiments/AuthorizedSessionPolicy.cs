using System;
using ZB2SecurityLab.Core.Diagnostics;

namespace ZB2SecurityLab.Core.Experiments;

public enum AuthorizedMultiplayerRole
{
    CLIENT,
    HOST
}

public sealed class AuthorizedSessionGrant
{
    public int SchemaVersion { get; set; } = 1;

    public string BuildId { get; set; } = string.Empty;

    public string SteamLobbyId { get; set; } = string.Empty;

    public string ServerSteamId { get; set; } = string.Empty;

    public string AuthorizedLocalSteamId { get; set; } = string.Empty;

    public AuthorizedMultiplayerRole AllowedRole { get; set; }

    public DateTimeOffset ExpiresUtc { get; set; }

    public string RunLabel { get; set; } = string.Empty;
}

public sealed class SessionAuthorizationDecision
{
    public SessionAuthorizationDecision(bool allowed, string reason, string? authorizationId = null)
    {
        Allowed = allowed;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        AuthorizationId = authorizationId;
    }

    public bool Allowed { get; }

    public string Reason { get; }

    public string? AuthorizationId { get; }
}

public sealed class AuthorizedSessionPolicy
{
    public SessionAuthorizationDecision Evaluate(
        MultiplayerSessionSnapshot session,
        AuthorizedSessionGrant? grant,
        string buildId,
        DateTimeOffset observedAtUtc)
    {
        if (session is null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (grant is null)
        {
            return Denied("AUTHORIZATION_GRANT_MISSING");
        }

        if (grant.SchemaVersion != 1)
        {
            return Denied("AUTHORIZATION_SCHEMA_UNSUPPORTED");
        }

        if (string.IsNullOrWhiteSpace(grant.RunLabel))
        {
            return Denied("AUTHORIZATION_RUN_LABEL_MISSING");
        }

        if (!string.Equals(grant.BuildId, buildId, StringComparison.Ordinal))
        {
            return Denied("AUTHORIZATION_BUILD_MISMATCH");
        }

        if (grant.ExpiresUtc == default || observedAtUtc >= grant.ExpiresUtc)
        {
            return Denied("AUTHORIZATION_EXPIRED");
        }

        if (grant.ExpiresUtc > observedAtUtc.AddHours(24))
        {
            return Denied("AUTHORIZATION_EXPIRY_TOO_FAR");
        }

        if (!session.IsMultiplayer)
        {
            return Denied("MULTIPLAYER_SESSION_REQUIRED");
        }

        if (!session.LocalLobbyPlayerId.HasValue || session.LocalLobbyPlayerId.Value < 0)
        {
            return Denied("LOCAL_LOBBY_PLAYER_UNAVAILABLE");
        }

        if (!ValidSteamId(grant.SteamLobbyId) ||
            !ValidSteamId(grant.ServerSteamId) ||
            !ValidSteamId(grant.AuthorizedLocalSteamId))
        {
            return Denied("AUTHORIZATION_STEAM_ID_INVALID");
        }

        var expectedRole = grant.AllowedRole == AuthorizedMultiplayerRole.CLIENT
            ? NetworkRole.CLIENT
            : NetworkRole.HOST;
        if (session.Role != expectedRole)
        {
            return Denied("AUTHORIZATION_ROLE_MISMATCH");
        }

        if (!Exact(grant.SteamLobbyId, session.SteamLobbyId))
        {
            return Denied("AUTHORIZATION_LOBBY_MISMATCH");
        }

        if (!Exact(grant.ServerSteamId, session.ServerSteamId))
        {
            return Denied("AUTHORIZATION_SERVER_MISMATCH");
        }

        if (!Exact(grant.AuthorizedLocalSteamId, session.LocalSteamId))
        {
            return Denied("AUTHORIZATION_LOCAL_STEAM_ID_MISMATCH");
        }

        if (!Exact(grant.ServerSteamId, session.LobbyOwnerSteamId) ||
            !Exact(grant.ServerSteamId, session.GameServerSteamId))
        {
            return Denied("SESSION_SERVER_IDENTITY_MISMATCH");
        }

        if (!session.FriendsOnlySignal)
        {
            return Denied("FRIENDS_ONLY_REQUIRED");
        }

        if (session.Role == NetworkRole.CLIENT)
        {
            if (!session.ClientConnected || !session.ClientMatchmakingConnected)
            {
                return Denied("CLIENT_NOT_CONNECTED");
            }

            if (!session.ServerConnectionResolved ||
                !Exact(grant.ServerSteamId, session.ServerConnectionSteamId))
            {
                return Denied("CLIENT_SERVER_CONNECTION_MISMATCH");
            }
        }
        else if (!session.ServerStarted ||
                 !session.ServerMultiplayerMode ||
                 !session.ServerLobbyLaunched ||
                 !Exact(grant.ServerSteamId, session.LocalSteamId))
        {
            return Denied("HOST_SESSION_NOT_READY");
        }

        return new SessionAuthorizationDecision(true, "AUTHORIZED_SESSION_MATCH", grant.RunLabel);
    }

    private static bool Exact(string expected, string? observed)
    {
        return !string.IsNullOrWhiteSpace(expected) &&
            !string.IsNullOrWhiteSpace(observed) &&
            string.Equals(expected, observed, StringComparison.Ordinal);
    }

    private static bool ValidSteamId(string value)
    {
        return ulong.TryParse(value, out var parsed) && parsed != 0UL;
    }

    private static SessionAuthorizationDecision Denied(string reason) => new(false, reason);
}
