using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class AuthorizedSessionPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Evaluate_AllowsExactClientAndHostSessions()
    {
        var policy = new AuthorizedSessionPolicy();

        var client = policy.Evaluate(Session(NetworkRole.CLIENT), Grant(AuthorizedMultiplayerRole.CLIENT), "24525702", Now);
        var host = policy.Evaluate(Session(NetworkRole.HOST), Grant(AuthorizedMultiplayerRole.HOST), "24525702", Now);

        Assert.IsTrue(client.Allowed);
        Assert.AreEqual("run-1", client.AuthorizationId);
        Assert.IsTrue(host.Allowed);
    }

    [TestMethod]
    public void Evaluate_FailsClosedForMissingMalformedOrExpiredGrant()
    {
        var policy = new AuthorizedSessionPolicy();
        var session = Session(NetworkRole.CLIENT);

        Assert.AreEqual("AUTHORIZATION_GRANT_MISSING", policy.Evaluate(session, null, "24525702", Now).Reason);

        var grant = Grant(AuthorizedMultiplayerRole.CLIENT);
        grant.SchemaVersion = 2;
        Assert.AreEqual("AUTHORIZATION_SCHEMA_UNSUPPORTED", policy.Evaluate(session, grant, "24525702", Now).Reason);

        grant = Grant(AuthorizedMultiplayerRole.CLIENT);
        grant.RunLabel = string.Empty;
        Assert.AreEqual("AUTHORIZATION_RUN_LABEL_MISSING", policy.Evaluate(session, grant, "24525702", Now).Reason);

        grant = Grant(AuthorizedMultiplayerRole.CLIENT);
        grant.ExpiresUtc = Now;
        Assert.AreEqual("AUTHORIZATION_EXPIRED", policy.Evaluate(session, grant, "24525702", Now).Reason);

        grant = Grant(AuthorizedMultiplayerRole.CLIENT);
        grant.ExpiresUtc = Now.AddHours(25);
        Assert.AreEqual("AUTHORIZATION_EXPIRY_TOO_FAR", policy.Evaluate(session, grant, "24525702", Now).Reason);
    }

    [TestMethod]
    public void Evaluate_RejectsEverySessionIdentityMismatch()
    {
        var policy = new AuthorizedSessionPolicy();

        var session = Session(NetworkRole.CLIENT);
        var grant = Grant(AuthorizedMultiplayerRole.CLIENT);
        grant.BuildId = "other";
        Assert.AreEqual("AUTHORIZATION_BUILD_MISMATCH", policy.Evaluate(session, grant, "24525702", Now).Reason);

        grant = Grant(AuthorizedMultiplayerRole.CLIENT);
        grant.AllowedRole = AuthorizedMultiplayerRole.HOST;
        Assert.AreEqual("AUTHORIZATION_ROLE_MISMATCH", policy.Evaluate(session, grant, "24525702", Now).Reason);

        grant = Grant(AuthorizedMultiplayerRole.CLIENT);
        grant.SteamLobbyId = "109775241012345679";
        Assert.AreEqual("AUTHORIZATION_LOBBY_MISMATCH", policy.Evaluate(session, grant, "24525702", Now).Reason);

        grant = Grant(AuthorizedMultiplayerRole.CLIENT);
        grant.ServerSteamId = "76561198000000003";
        Assert.AreEqual("AUTHORIZATION_SERVER_MISMATCH", policy.Evaluate(session, grant, "24525702", Now).Reason);

        grant = Grant(AuthorizedMultiplayerRole.CLIENT);
        grant.AuthorizedLocalSteamId = "76561198000000003";
        Assert.AreEqual("AUTHORIZATION_LOCAL_STEAM_ID_MISMATCH", policy.Evaluate(session, grant, "24525702", Now).Reason);
    }

    [TestMethod]
    public void Evaluate_RequiresPrivateSignalAndExactServerConnection()
    {
        var policy = new AuthorizedSessionPolicy();
        var grant = Grant(AuthorizedMultiplayerRole.CLIENT);
        var session = Session(NetworkRole.CLIENT);
        session.FriendsOnlySignal = false;
        Assert.AreEqual("FRIENDS_ONLY_REQUIRED", policy.Evaluate(session, grant, "24525702", Now).Reason);

        session = Session(NetworkRole.CLIENT);
        session.ServerConnectionSteamId = "76561198000000003";
        Assert.AreEqual("CLIENT_SERVER_CONNECTION_MISMATCH", policy.Evaluate(session, grant, "24525702", Now).Reason);

        session = Session(NetworkRole.HOST);
        session.ServerLobbyLaunched = false;
        Assert.AreEqual(
            "HOST_SESSION_NOT_READY",
            policy.Evaluate(session, Grant(AuthorizedMultiplayerRole.HOST), "24525702", Now).Reason);
    }

    [TestMethod]
    public void Evaluate_RejectsUnresolvedLobbyAndServerIdentity()
    {
        var policy = new AuthorizedSessionPolicy();
        var grant = Grant(AuthorizedMultiplayerRole.CLIENT);

        var session = Session(NetworkRole.CLIENT);
        session.LocalLobbyPlayerId = null;
        Assert.AreEqual("LOCAL_LOBBY_PLAYER_UNAVAILABLE", policy.Evaluate(session, grant, "24525702", Now).Reason);

        grant = Grant(AuthorizedMultiplayerRole.CLIENT);
        grant.SteamLobbyId = "not-a-steam-id";
        Assert.AreEqual("AUTHORIZATION_STEAM_ID_INVALID", policy.Evaluate(Session(NetworkRole.CLIENT), grant, "24525702", Now).Reason);

        session = Session(NetworkRole.CLIENT);
        session.LobbyOwnerSteamId = "76561198000000003";
        Assert.AreEqual("SESSION_SERVER_IDENTITY_MISMATCH", policy.Evaluate(session, Grant(AuthorizedMultiplayerRole.CLIENT), "24525702", Now).Reason);

        session = Session(NetworkRole.CLIENT);
        session.ClientMatchmakingConnected = false;
        Assert.AreEqual("CLIENT_NOT_CONNECTED", policy.Evaluate(session, Grant(AuthorizedMultiplayerRole.CLIENT), "24525702", Now).Reason);
    }

    private static MultiplayerSessionSnapshot Session(NetworkRole role)
    {
        return new MultiplayerSessionSnapshot
        {
            Role = role,
            IsMultiplayer = true,
            ServerStarted = role == NetworkRole.HOST,
            ServerMultiplayerMode = role == NetworkRole.HOST,
            ClientConnected = role == NetworkRole.CLIENT,
            ClientMatchmakingConnected = role == NetworkRole.CLIENT,
            ServerLobbyLaunched = role == NetworkRole.HOST,
            FriendsOnlySignal = true,
            ServerConnectionResolved = role == NetworkRole.CLIENT,
            SteamLobbyId = "109775241012345678",
            ServerSteamId = "76561198000000001",
            LobbyOwnerSteamId = "76561198000000001",
            GameServerSteamId = "76561198000000001",
            ServerConnectionSteamId = role == NetworkRole.CLIENT ? "76561198000000001" : null,
            LocalSteamId = role == NetworkRole.HOST ? "76561198000000001" : "76561198000000002",
            LocalLobbyPlayerId = role == NetworkRole.HOST ? 0 : 1
        };
    }

    private static AuthorizedSessionGrant Grant(AuthorizedMultiplayerRole role)
    {
        return new AuthorizedSessionGrant
        {
            BuildId = "24525702",
            SteamLobbyId = "109775241012345678",
            ServerSteamId = "76561198000000001",
            AuthorizedLocalSteamId = role == AuthorizedMultiplayerRole.HOST ? "76561198000000001" : "76561198000000002",
            AllowedRole = role,
            ExpiresUtc = Now.AddHours(1),
            RunLabel = "run-1"
        };
    }
}
