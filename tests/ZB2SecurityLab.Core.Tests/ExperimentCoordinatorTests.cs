using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class ExperimentCoordinatorTests
{
    [TestMethod]
    public void Start_RejectsSecondConcurrentExperiment()
    {
        var coordinator = new ExperimentCoordinator();
        coordinator.Start(new FakeExperiment("first"));

        Assert.ThrowsException<InvalidOperationException>(() => coordinator.Start(new FakeExperiment("second")));
    }

    [TestMethod]
    public void Start_RestoresAndClears_WhenApplyFails()
    {
        var coordinator = new ExperimentCoordinator();
        var experiment = new FakeExperiment("failing") { ThrowOnApply = true };

        Assert.ThrowsException<InvalidOperationException>(() => coordinator.Start(experiment));
        Assert.AreEqual(1, experiment.RestoreCount);
        Assert.IsNull(coordinator.ActiveExperiment);
    }

    private sealed class FakeExperiment : ILabExperiment
    {
        internal FakeExperiment(string id)
        {
            Id = id;
        }

        public string Id { get; }

        internal bool ThrowOnApply { get; set; }

        internal int RestoreCount { get; private set; }

        public void CaptureBaseline()
        {
        }

        public void Apply()
        {
            if (ThrowOnApply)
            {
                throw new InvalidOperationException("test failure");
            }
        }

        public void Observe()
        {
        }

        public void Restore()
        {
            RestoreCount++;
        }
    }
}

