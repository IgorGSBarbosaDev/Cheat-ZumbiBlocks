using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class LabModeGuardTests
{
    [TestMethod]
    public void Evaluate_FailsClosedWithDeterministicReasons()
    {
        var guard = new LabModeGuard();

        AssertDecision(guard, Context(mutationEnabled: false), false, "MUTATIONS_DISABLED");
        AssertDecision(guard, Context(buildSupported: false), false, "UNSUPPORTED_BUILD");
        var missingBuild = Context();
        missingBuild.BuildId = string.Empty;
        AssertDecision(guard, missingBuild, false, "BUILD_ID_UNAVAILABLE");
        AssertDecision(guard, Context(inGame: false), false, "NOT_IN_GAME");
        AssertDecision(guard, Context(localPlayerAvailable: false, playerToken: null), false, "LOCAL_PLAYER_UNAVAILABLE");
        AssertDecision(guard, Context(hasLocalControl: false), false, "LOCAL_CONTROL_REQUIRED");
        AssertDecision(guard, Context(role: NetworkRole.HOST), false, "AUTHORIZED_MULTIPLAYER_DISABLED");
        AssertDecision(guard, Context(role: NetworkRole.CLIENT), false, "AUTHORIZED_MULTIPLAYER_DISABLED");

        var missingTime = Context();
        missingTime.ObservedAtUtc = default;
        AssertDecision(guard, missingTime, false, "OBSERVATION_TIME_UNAVAILABLE");

        var missingSession = Context();
        missingSession.MultiplayerSession = null;
        AssertDecision(guard, missingSession, false, "SESSION_CONTEXT_UNAVAILABLE");

        AssertDecision(guard, Context(role: NetworkRole.OFFLINE), false, "NOT_ELIGIBLE_ROLE");

        var invalidSinglePlayer = Context();
        invalidSinglePlayer.MultiplayerSession!.ServerStarted = false;
        AssertDecision(guard, invalidSinglePlayer, false, "SINGLE_PLAYER_CONTEXT_INVALID");

        var invalidGrant = Context(role: NetworkRole.CLIENT);
        invalidGrant.AuthorizedMultiplayerEnabled = true;
        invalidGrant.AuthorizationGrantError = "GRANT_FORMAT_INVALID";
        AssertDecision(guard, invalidGrant, false, "AUTHORIZATION_GRANT_INVALID");
    }

    [TestMethod]
    public void Evaluate_AllowsOnlyCompleteSinglePlayerContext()
    {
        var decision = new LabModeGuard().Evaluate(Context());

        Assert.IsTrue(decision.Allowed);
        Assert.AreEqual("ALLOWED_SINGLE_PLAYER", decision.Reason);
        Assert.AreEqual(MutationExecutionScope.SINGLE_PLAYER, decision.Scope);
        Assert.IsNotNull(decision.SessionToken);
    }

    [TestMethod]
    public void Evaluate_AllowsOnlyExactAuthorizedMultiplayerSession()
    {
        var context = Context(role: NetworkRole.CLIENT);
        context.AuthorizedMultiplayerEnabled = true;
        context.AuthorizationGrant = Grant(AuthorizedMultiplayerRole.CLIENT);

        var decision = new LabModeGuard().Evaluate(context);

        Assert.IsTrue(decision.Allowed);
        Assert.AreEqual(MutationExecutionScope.AUTHORIZED_MULTIPLAYER_CLIENT, decision.Scope);
        Assert.AreEqual("run-1", decision.AuthorizationId);

        context.MultiplayerSession!.SteamLobbyId = "other";
        AssertDecision(new LabModeGuard(), context, false, "AUTHORIZATION_LOBBY_MISMATCH");
    }

    [TestMethod]
    public void Evaluate_BlocksTransitionalClientAndHostStates()
    {
        var client = Context(role: NetworkRole.CLIENT);
        client.AuthorizedMultiplayerEnabled = true;
        client.AuthorizationGrant = Grant(AuthorizedMultiplayerRole.CLIENT);
        client.MultiplayerSession!.ClientConnected = false;
        AssertDecision(new LabModeGuard(), client, false, "CLIENT_NOT_CONNECTED");

        var host = Context(role: NetworkRole.HOST);
        host.AuthorizedMultiplayerEnabled = true;
        host.AuthorizationGrant = Grant(AuthorizedMultiplayerRole.HOST);
        host.MultiplayerSession!.ServerStarted = false;
        AssertDecision(new LabModeGuard(), host, false, "HOST_SESSION_NOT_READY");
    }

    private static MutationEligibilityContext Context(
        bool mutationEnabled = true,
        bool buildSupported = true,
        bool inGame = true,
        bool localPlayerAvailable = true,
        bool hasLocalControl = true,
        NetworkRole role = NetworkRole.SINGLE_PLAYER,
        string? playerToken = "player")
    {
        var session = Session(role);
        return new MutationEligibilityContext
        {
            MutationEnabled = mutationEnabled,
            BuildSupported = buildSupported,
            InGame = inGame,
            LocalPlayerAvailable = localPlayerAvailable,
            HasLocalControl = hasLocalControl,
            Role = role.ToString(),
            PlayerToken = playerToken,
            BuildId = "24525702",
            ObservedAtUtc = new DateTimeOffset(2026, 9, 6, 12, 0, 0, TimeSpan.Zero),
            MultiplayerSession = session
        };
    }

    private static MultiplayerSessionSnapshot Session(NetworkRole role)
    {
        return new MultiplayerSessionSnapshot
        {
            Role = role,
            IsMultiplayer = role == NetworkRole.CLIENT || role == NetworkRole.HOST,
            ServerStarted = role == NetworkRole.SINGLE_PLAYER || role == NetworkRole.HOST,
            ServerSinglePlayerMode = role == NetworkRole.SINGLE_PLAYER,
            ServerMultiplayerMode = role == NetworkRole.HOST,
            ClientConnected = role == NetworkRole.CLIENT,
            ClientMatchmakingConnected = role == NetworkRole.CLIENT,
            ServerLobbyLaunched = role == NetworkRole.HOST,
            FriendsOnlySignal = role == NetworkRole.CLIENT || role == NetworkRole.HOST,
            ServerConnectionResolved = role == NetworkRole.CLIENT,
            SteamLobbyId = role == NetworkRole.SINGLE_PLAYER ? null : "109775241012345678",
            ServerSteamId = role == NetworkRole.SINGLE_PLAYER ? null : "76561198000000001",
            LobbyOwnerSteamId = role == NetworkRole.SINGLE_PLAYER ? null : "76561198000000001",
            GameServerSteamId = role == NetworkRole.SINGLE_PLAYER ? null : "76561198000000001",
            ServerConnectionSteamId = role == NetworkRole.CLIENT ? "76561198000000001" : null,
            LocalSteamId = role == NetworkRole.HOST ? "76561198000000001" : role == NetworkRole.CLIENT ? "76561198000000002" : null,
            LocalLobbyPlayerId = role == NetworkRole.SINGLE_PLAYER ? 0 : 1
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
            ExpiresUtc = new DateTimeOffset(2026, 9, 6, 13, 0, 0, TimeSpan.Zero),
            RunLabel = "run-1"
        };
    }

    private static void AssertDecision(
        LabModeGuard guard,
        MutationEligibilityContext context,
        bool allowed,
        string reason)
    {
        var decision = guard.Evaluate(context);
        Assert.AreEqual(allowed, decision.Allowed);
        Assert.AreEqual(reason, decision.Reason);
    }
}
