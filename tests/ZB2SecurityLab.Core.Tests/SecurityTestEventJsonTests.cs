using System;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class SecurityTestEventJsonTests
{
    [TestMethod]
    public void Serialize_ProducesValidJsonAndEscapesDiagnosticText()
    {
        var value = new SecurityTestEvent
        {
            TimestampUtc = new DateTimeOffset(2026, 9, 5, 12, 30, 0, TimeSpan.FromHours(-3)),
            SessionId = "session",
            BuildId = "24525702",
            BuildFingerprint = "ABC",
            Test = "Instrumentation",
            Phase = "CONTEXT_CAPTURE_FAILED",
            LocalObservedValue = "line 1\n\"line 2\"",
            Error = null
        };

        var json = SecurityTestEventJson.Serialize(value);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.AreEqual("2026-09-05T15:30:00.0000000+00:00", root.GetProperty("timestampUtc").GetString());
        Assert.AreEqual("line 1\n\"line 2\"", root.GetProperty("localObservedValue").GetString());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("outcome").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("restoreReason").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("restoreSucceeded").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("serverAccepted").ValueKind);
        Assert.IsFalse(root.GetProperty("disconnected").GetBoolean());
    }

    [TestMethod]
    public void Serialize_PreservesLegacyFieldsAndAddsOptionalPlayerStateFields()
    {
        var value = new SecurityTestEvent
        {
            SessionId = "session",
            BuildId = "24525702",
            BuildFingerprint = "ABC",
            Test = "Instrumentation",
            Phase = "AMMO_CHANGED",
            Event = "ammo_changed",
            OldValue = "30",
            NewValue = "27",
            Context = "playerToken=1;role=SINGLE_PLAYER;lobby=NULL;weapon=HiPoint",
            OriginalValue = "30",
            LocalObservedValue = "27",
            RemoteObservedValue = "hostShotCount=31",
            ServerEvidence = "ACCEPTED",
            ExecutionScope = "AUTHORIZED_MULTIPLAYER_CLIENT",
            NetworkMode = "MULTIPLAYER",
            NetworkRole = "CLIENT",
            ConnectionState = "CLIENT:Connected",
            SteamLobbyId = "109775241012345678",
            ServerSteamId = "76561198000000001",
            LobbyOwnerSteamId = "76561198000000001",
            LocalSteamId = "76561198000000002",
            LocalLobbyPlayerId = 1,
            AuthorizationId = "run-1",
            AuthorizationDecision = "AUTHORIZED_SESSION_MATCH",
            ExperimentRunId = "experiment-1",
            EvidenceSource = "AUTHORIZED_HOST_LOG",
            Outcome = TestOutcome.LOCAL_ONLY,
            RestoreReason = "DURATION_ELAPSED",
            RestoreSucceeded = true
        };

        var json = SecurityTestEventJson.Serialize(value);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.AreEqual("AMMO_CHANGED", root.GetProperty("phase").GetString());
        Assert.AreEqual("ammo_changed", root.GetProperty("event").GetString());
        Assert.AreEqual("30", root.GetProperty("oldValue").GetString());
        Assert.AreEqual("27", root.GetProperty("newValue").GetString());
        Assert.AreEqual(value.Context, root.GetProperty("context").GetString());
        Assert.AreEqual("30", root.GetProperty("originalValue").GetString());
        Assert.AreEqual("27", root.GetProperty("localObservedValue").GetString());
        Assert.AreEqual("hostShotCount=31", root.GetProperty("remoteObservedValue").GetString());
        Assert.AreEqual("AUTHORIZED_MULTIPLAYER_CLIENT", root.GetProperty("executionScope").GetString());
        Assert.AreEqual("109775241012345678", root.GetProperty("steamLobbyId").GetString());
        Assert.AreEqual(1, root.GetProperty("localLobbyPlayerId").GetInt32());
        Assert.AreEqual("experiment-1", root.GetProperty("experimentRunId").GetString());
        Assert.AreEqual("LOCAL_ONLY", root.GetProperty("outcome").GetString());
        Assert.AreEqual("DURATION_ELAPSED", root.GetProperty("restoreReason").GetString());
        Assert.IsTrue(root.GetProperty("restoreSucceeded").GetBoolean());
    }

    [TestMethod]
    public void Serialize_PreservesInfiniteAmmoWriteEvidence()
    {
        var value = new SecurityTestEvent
        {
            SessionId = "session",
            BuildId = "24525702",
            BuildFingerprint = "ABC",
            Test = "INFINITE_AMMO",
            Phase = "WRITE_APPLIED",
            Event = "controlled_mutation_write",
            OldValue = "29",
            NewValue = "30",
            Context = "playerToken=1;role=SINGLE_PLAYER;target=ammo-1;weapon=HiPoint;slot=Weapon:0;maxAmmo=30;ammoConsumption=1",
            RequestedValue = "ammo=30;mode=reactive",
            LocalObservedValue = "30",
            ServerEvidence = "NOT_EVALUATED_SINGLE_PLAYER_ONLY"
        };

        var json = SecurityTestEventJson.Serialize(value);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.AreEqual("INFINITE_AMMO", root.GetProperty("test").GetString());
        Assert.AreEqual("WRITE_APPLIED", root.GetProperty("phase").GetString());
        Assert.AreEqual("controlled_mutation_write", root.GetProperty("event").GetString());
        Assert.AreEqual("29", root.GetProperty("oldValue").GetString());
        Assert.AreEqual("30", root.GetProperty("newValue").GetString());
        StringAssert.Contains(root.GetProperty("context").GetString(), "ammoConsumption=1");
        Assert.AreEqual("NOT_EVALUATED_SINGLE_PLAYER_ONLY", root.GetProperty("serverEvidence").GetString());
    }
}
