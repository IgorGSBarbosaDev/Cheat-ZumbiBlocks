using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class LabStateTrackerTests
{
    [TestMethod]
    public void Observe_ReportsMatchAndPlayerTransitions()
    {
        var tracker = new LabStateTracker();

        var initial = tracker.Observe(Snapshot(inGame: false, playerToken: null));
        var started = tracker.Observe(Snapshot(inGame: true, playerToken: "101"));
        var ended = tracker.Observe(Snapshot(inGame: false, playerToken: null));

        CollectionAssert.Contains(initial.Select(item => item.Kind).ToList(), LabTransitionKind.CONTEXT_INITIALIZED);
        CollectionAssert.Contains(started.Select(item => item.Kind).ToList(), LabTransitionKind.MATCH_STARTED);
        CollectionAssert.Contains(started.Select(item => item.Kind).ToList(), LabTransitionKind.LOCAL_PLAYER_ACQUIRED);
        CollectionAssert.Contains(ended.Select(item => item.Kind).ToList(), LabTransitionKind.MATCH_ENDED);
        CollectionAssert.Contains(ended.Select(item => item.Kind).ToList(), LabTransitionKind.LOCAL_PLAYER_LOST);
    }

    [TestMethod]
    public void Observe_ReportsPlayerReacquisition_WhenUnityInstanceChanges()
    {
        var tracker = new LabStateTracker();
        tracker.Observe(Snapshot(inGame: true, playerToken: "101"));

        var transitions = tracker.Observe(Snapshot(inGame: true, playerToken: "202"));

        Assert.AreEqual(1, transitions.Count(item => item.Kind == LabTransitionKind.LOCAL_PLAYER_REACQUIRED));
    }

    [TestMethod]
    public void Observe_DoesNotLogPingChangesEveryPoll()
    {
        var tracker = new LabStateTracker();
        var first = Snapshot(inGame: true, playerToken: "101");
        first.PingMilliseconds = 20;
        tracker.Observe(first);

        var second = Snapshot(inGame: true, playerToken: "101");
        second.PingMilliseconds = 45;
        var transitions = tracker.Observe(second);

        Assert.AreEqual(0, transitions.Count);
    }

    [TestMethod]
    public void Observe_ReportsSteamSessionIdentityChanges()
    {
        var tracker = new LabStateTracker();
        var initial = Snapshot(inGame: true, playerToken: "101");
        initial.LobbyId = "lobby-a";
        initial.ServerSteamId = "server-a";
        initial.LocalLobbyPlayerId = 1;
        tracker.Observe(initial);

        var changed = Snapshot(inGame: true, playerToken: "101");
        changed.LobbyId = "lobby-b";
        changed.ServerSteamId = "server-b";
        changed.LocalLobbyPlayerId = 2;
        var transitions = tracker.Observe(changed);

        CollectionAssert.Contains(transitions.Select(item => item.Kind).ToList(), LabTransitionKind.LOBBY_CHANGED);
        CollectionAssert.Contains(transitions.Select(item => item.Kind).ToList(), LabTransitionKind.SERVER_CHANGED);
        CollectionAssert.Contains(transitions.Select(item => item.Kind).ToList(), LabTransitionKind.LOCAL_LOBBY_PLAYER_CHANGED);
    }

    private static LabSnapshot Snapshot(bool inGame, string? playerToken)
    {
        return new LabSnapshot
        {
            InGame = inGame,
            LocalPlayerAvailable = playerToken is not null,
            HasLocalControl = playerToken is not null,
            LocalPlayerToken = playerToken,
            Role = "CLIENT",
            ConnectionState = "CLIENT:Connected",
            LobbyId = "7"
        };
    }
}
