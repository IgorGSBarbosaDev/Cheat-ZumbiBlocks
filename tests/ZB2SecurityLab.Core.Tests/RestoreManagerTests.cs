using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class RestoreManagerTests
{
    [TestMethod]
    public void RestoreAll_UsesLifoOrderAndIsIdempotent()
    {
        var calls = new List<string>();
        var manager = new RestoreManager();
        manager.Register(new FakeExperiment("first", calls));
        manager.Register(new FakeExperiment("second", calls));

        var firstReport = manager.RestoreAll(RestoreReason.MANUAL);
        var repeatedReport = manager.RestoreAll(RestoreReason.PLUGIN_DESTROYED);

        CollectionAssert.AreEqual(new[] { "second", "first" }, calls);
        Assert.IsTrue(firstReport.Succeeded);
        Assert.AreEqual(2, firstReport.Entries.Count);
        Assert.AreEqual(0, repeatedReport.Entries.Count);
        Assert.AreEqual(0, manager.PendingCount);
    }

    [TestMethod]
    public void RestoreAll_ContinuesAfterFailureAndAggregatesErrors()
    {
        var calls = new List<string>();
        var manager = new RestoreManager();
        manager.Register(new FakeExperiment("successful", calls));
        manager.Register(new FakeExperiment("failing", calls) { ThrowOnRestore = true });

        var report = manager.RestoreAll(RestoreReason.CONTEXT_INVALID);

        CollectionAssert.AreEqual(new[] { "failing", "successful" }, calls);
        Assert.IsFalse(report.Succeeded);
        Assert.IsNotNull(report.ErrorSummary);
        StringAssert.Contains(report.ErrorSummary, "failing:restore failure");
    }

    [TestMethod]
    public void Register_RejectsSameExperimentTwice()
    {
        var manager = new RestoreManager();
        var experiment = new FakeExperiment("duplicate", new List<string>());
        manager.Register(experiment);

        Assert.ThrowsException<InvalidOperationException>(() => manager.Register(experiment));
    }

    private sealed class FakeExperiment : ILabExperiment
    {
        private readonly ICollection<string> _calls;

        internal FakeExperiment(string id, ICollection<string> calls)
        {
            Id = id;
            _calls = calls;
        }

        public string Id { get; }

        public string TargetToken => "player";

        public string? OriginalValue => "original";

        public string? RequestedValue => "requested";

        public string? LocalObservedValue => null;

        public bool RequestedValueObserved => true;

        public bool InterferenceDetected => false;

        public bool RestoreConfirmed { get; private set; }

        internal bool ThrowOnRestore { get; set; }

        public void CaptureBaseline()
        {
        }

        public void Apply()
        {
        }

        public void Observe()
        {
        }

        public void Restore()
        {
            _calls.Add(Id);
            if (ThrowOnRestore)
            {
                throw new InvalidOperationException("restore failure");
            }

            RestoreConfirmed = true;
        }
    }
}
