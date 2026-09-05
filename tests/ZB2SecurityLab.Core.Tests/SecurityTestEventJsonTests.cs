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
            LocalObservedValue = "27"
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
    }
}
