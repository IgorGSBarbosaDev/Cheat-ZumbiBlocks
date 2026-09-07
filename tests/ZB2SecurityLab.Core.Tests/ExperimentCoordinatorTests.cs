using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class ExperimentCoordinatorTests
{
    private static readonly MutationGuardDecision Allowed = new(
        true,
        "ALLOWED_SINGLE_PLAYER",
        MutationExecutionScope.SINGLE_PLAYER,
        "session-a");

    [TestMethod]
    public void Start_CapturesRegistersAppliesAndObservesInOrder()
    {
        var calls = new List<string>();
        var experiment = new FakeExperiment("first", calls);
        var coordinator = new ExperimentCoordinator(new RestoreManager(), 10d);

        var events = coordinator.Start(experiment, 5d);

        CollectionAssert.AreEqual(new[] { "capture", "apply", "observe" }, calls);
        CollectionAssert.AreEqual(
            new[] { MutationPhase.BASELINE_CAPTURED, MutationPhase.APPLIED, MutationPhase.OBSERVED, MutationPhase.MONITORING },
            events.Select(item => item.Phase).ToArray());
        Assert.AreSame(experiment, coordinator.ActiveExperiment);
        Assert.AreEqual(10d, coordinator.RemainingSeconds(5d));
    }

    [TestMethod]
    public void Start_RejectsSecondConcurrentExperiment()
    {
        var coordinator = new ExperimentCoordinator();
        coordinator.Start(new FakeExperiment("first"), 0d);

        Assert.ThrowsException<InvalidOperationException>(() => coordinator.Start(new FakeExperiment("second"), 0d));
    }

    [TestMethod]
    public void Tick_RestoresExactlyAtDurationBoundary()
    {
        var experiment = new FakeExperiment("timed");
        var coordinator = new ExperimentCoordinator(durationSeconds: 10d);
        coordinator.Start(experiment, 2d);

        coordinator.Tick(11.999d, Allowed, experiment.TargetToken);
        Assert.IsNotNull(coordinator.ActiveExperiment);
        Assert.AreEqual(0, experiment.RestoreCount);

        var events = coordinator.Tick(12d, Allowed, experiment.TargetToken);

        Assert.IsNull(coordinator.ActiveExperiment);
        Assert.AreEqual(1, experiment.RestoreCount);
        Assert.AreEqual(TestOutcome.LOCAL_ONLY, coordinator.LastResult!.Outcome);
        Assert.AreEqual(RestoreReason.DURATION_ELAPSED, coordinator.LastResult.RestoreReason);
        Assert.IsTrue(events.Any(item => item.Phase == MutationPhase.RESTORED));
    }

    [TestMethod]
    public void Stop_IsIdempotentAfterManualRestore()
    {
        var experiment = new FakeExperiment("manual");
        var coordinator = new ExperimentCoordinator();
        coordinator.Start(experiment, 0d);

        coordinator.Stop(RestoreReason.MANUAL);
        var repeated = coordinator.Stop(RestoreReason.PLUGIN_DESTROYED);

        Assert.AreEqual(1, experiment.RestoreCount);
        Assert.AreEqual(0, repeated.Count);
        Assert.AreEqual(TestOutcome.LOCAL_ONLY, coordinator.LastResult!.Outcome);
    }

    [TestMethod]
    public void Tick_ContextOrTargetLossRestoresAndIsInconclusive()
    {
        var contextExperiment = new FakeExperiment("context");
        var contextCoordinator = new ExperimentCoordinator();
        contextCoordinator.Start(contextExperiment, 0d);
        contextCoordinator.Tick(1d, new MutationGuardDecision(false, "NOT_IN_GAME"), contextExperiment.TargetToken);

        Assert.AreEqual(1, contextExperiment.RestoreCount);
        Assert.AreEqual(TestOutcome.INCONCLUSIVE, contextCoordinator.LastResult!.Outcome);
        Assert.AreEqual(RestoreReason.CONTEXT_INVALID, contextCoordinator.LastResult.RestoreReason);

        var targetExperiment = new FakeExperiment("target");
        var targetCoordinator = new ExperimentCoordinator();
        targetCoordinator.Start(targetExperiment, 0d);
        targetCoordinator.Tick(1d, Allowed, "replacement");

        Assert.AreEqual(1, targetExperiment.RestoreCount);
        Assert.AreEqual(TestOutcome.INCONCLUSIVE, targetCoordinator.LastResult!.Outcome);
        Assert.AreEqual(RestoreReason.TARGET_CHANGED, targetCoordinator.LastResult.RestoreReason);
    }

    [TestMethod]
    public void Tick_SessionIdentityDriftRestoresAndIsInconclusive()
    {
        var experiment = new FakeExperiment("session");
        var coordinator = new ExperimentCoordinator();
        coordinator.Start(experiment, 0d, Allowed);

        coordinator.Tick(
            1d,
            new MutationGuardDecision(
                true,
                "ALLOWED_SINGLE_PLAYER",
                MutationExecutionScope.SINGLE_PLAYER,
                "session-b"),
            experiment.TargetToken);

        Assert.AreEqual(1, experiment.RestoreCount);
        Assert.AreEqual(RestoreReason.SESSION_CHANGED, coordinator.LastResult!.RestoreReason);
        Assert.AreEqual(TestOutcome.INCONCLUSIVE, coordinator.LastResult.Outcome);
    }

    [TestMethod]
    public void Tick_AuthorizationGrantDriftRestoresAndIsInconclusive()
    {
        var experiment = new FakeExperiment("authorization");
        var coordinator = new ExperimentCoordinator();
        var initial = new MutationGuardDecision(
            true,
            "AUTHORIZED_SESSION_MATCH",
            MutationExecutionScope.AUTHORIZED_MULTIPLAYER_CLIENT,
            "session-a",
            "run-1");
        coordinator.Start(experiment, 0d, initial);

        coordinator.Tick(
            1d,
            new MutationGuardDecision(
                true,
                "AUTHORIZED_SESSION_MATCH",
                MutationExecutionScope.AUTHORIZED_MULTIPLAYER_CLIENT,
                "session-a",
                "run-2"),
            experiment.TargetToken);

        Assert.AreEqual(1, experiment.RestoreCount);
        Assert.AreEqual(RestoreReason.SESSION_CHANGED, coordinator.LastResult!.RestoreReason);
        Assert.AreEqual(TestOutcome.INCONCLUSIVE, coordinator.LastResult.Outcome);
        StringAssert.Contains(coordinator.LastResult.Error, "AUTHORIZATION_CHANGED");
    }

    [TestMethod]
    public void Coordinator_ClassifiesExplicitMultiplayerServerEvidence()
    {
        var experiment = new FakeExperiment("multiplayer");
        var coordinator = new ExperimentCoordinator();
        var decision = new MutationGuardDecision(
            true,
            "AUTHORIZED_SESSION_MATCH",
            MutationExecutionScope.AUTHORIZED_MULTIPLAYER_CLIENT,
            "session-a",
            "run-1");
        var started = coordinator.Start(experiment, 0d, decision);

        var evidence = coordinator.RecordServerEvidence(
            ServerEvidenceKind.ACCEPTED,
            "hostShotCount=31",
            "AUTHORIZED_HOST_LOG");
        coordinator.Stop(RestoreReason.MANUAL);

        Assert.AreEqual(TestOutcome.SERVER_ACCEPTED, coordinator.LastResult!.Outcome);
        Assert.AreEqual("run-1", coordinator.LastResult.AuthorizationId);
        Assert.AreEqual("hostShotCount=31", coordinator.LastResult.RemoteObservedValue);
        Assert.IsFalse(string.IsNullOrEmpty(started[0].ExperimentRunId));
        Assert.AreEqual("server_evidence_observed", evidence.Single().Event);
    }

    [TestMethod]
    public void Start_CaptureFailureDoesNotRegisterRestore()
    {
        var experiment = new FakeExperiment("capture") { ThrowOnCapture = true };
        var coordinator = new ExperimentCoordinator();

        var events = coordinator.Start(experiment, 0d);

        Assert.AreEqual(0, experiment.RestoreCount);
        Assert.IsNull(coordinator.ActiveExperiment);
        Assert.AreEqual(TestOutcome.INCONCLUSIVE, coordinator.LastResult!.Outcome);
        Assert.IsTrue(events.Any(item => item.Phase == MutationPhase.FAILED));
    }

    [TestMethod]
    public void Start_ApplyOrObservationFailureRestoresAndClears()
    {
        var applyExperiment = new FakeExperiment("apply") { ThrowOnApply = true };
        var applyCoordinator = new ExperimentCoordinator();
        applyCoordinator.Start(applyExperiment, 0d);

        Assert.AreEqual(1, applyExperiment.RestoreCount);
        Assert.IsNull(applyCoordinator.ActiveExperiment);
        Assert.AreEqual(RestoreReason.APPLY_FAILED, applyCoordinator.LastResult!.RestoreReason);

        var observeExperiment = new FakeExperiment("observe") { ThrowOnObserve = true };
        var observeCoordinator = new ExperimentCoordinator();
        observeCoordinator.Start(observeExperiment, 0d);

        Assert.AreEqual(1, observeExperiment.RestoreCount);
        Assert.IsNull(observeCoordinator.ActiveExperiment);
        Assert.AreEqual(RestoreReason.OBSERVATION_FAILED, observeCoordinator.LastResult!.RestoreReason);
    }

    [TestMethod]
    public void Stop_RestoreFailureStillClearsAndIsInconclusive()
    {
        var experiment = new FakeExperiment("restore") { ThrowOnRestore = true };
        var coordinator = new ExperimentCoordinator();
        coordinator.Start(experiment, 0d);

        var events = coordinator.Stop(RestoreReason.MANUAL);

        Assert.IsNull(coordinator.ActiveExperiment);
        Assert.AreEqual(1, experiment.RestoreCount);
        Assert.AreEqual(TestOutcome.INCONCLUSIVE, coordinator.LastResult!.Outcome);
        Assert.IsFalse(coordinator.LastResult.RestoreSucceeded);
        Assert.IsTrue(events.Any(item => item.Phase == MutationPhase.RESTORE_FAILED));
    }

    [TestMethod]
    public void Tick_LogsInterferenceOnceAndClassifiesInconclusive()
    {
        var experiment = new FakeExperiment("interference") { InterfereAfterFirstObservation = true };
        var coordinator = new ExperimentCoordinator();
        coordinator.Start(experiment, 0d);

        var firstTick = coordinator.Tick(1d, Allowed, experiment.TargetToken);
        var secondTick = coordinator.Tick(2d, Allowed, experiment.TargetToken);
        coordinator.Stop(RestoreReason.MANUAL);

        Assert.AreEqual(1, firstTick.Count(item => item.Phase == MutationPhase.INTERFERENCE_DETECTED));
        Assert.AreEqual(0, secondTick.Count(item => item.Phase == MutationPhase.INTERFERENCE_DETECTED));
        Assert.AreEqual(TestOutcome.INCONCLUSIVE, coordinator.LastResult!.Outcome);
    }

    [TestMethod]
    public void Coordinator_DrainsRuntimeEventsAfterCaptureObserveAndRestore()
    {
        var experiment = new RuntimeEventExperiment();
        var coordinator = new ExperimentCoordinator();

        var started = coordinator.Start(experiment, 0d);
        var ticked = coordinator.Tick(1d, Allowed, experiment.TargetToken);
        var stopped = coordinator.Stop(RestoreReason.MANUAL);

        CollectionAssert.AreEqual(
            new[] { "captured", "started" },
            started.Where(item => item.Event is not null).Select(item => item.Event).ToArray());
        CollectionAssert.AreEqual(
            new[] { "tick" },
            ticked.Where(item => item.Event is not null).Select(item => item.Event).ToArray());
        CollectionAssert.AreEqual(
            new[] { "restore" },
            stopped.Where(item => item.Event is not null).Select(item => item.Event).ToArray());
        Assert.IsTrue(stopped.Single(item => item.Event == "restore").RestoreSucceeded is null);
    }

    private sealed class FakeExperiment : ILabExperiment
    {
        private readonly ICollection<string>? _calls;

        internal FakeExperiment(string id, ICollection<string>? calls = null)
        {
            Id = id;
            _calls = calls;
        }

        public string Id { get; }

        public string TargetToken { get; } = "player";

        public string? OriginalValue { get; private set; }

        public string? RequestedValue { get; private set; }

        public string? LocalObservedValue { get; private set; }

        public bool RequestedValueObserved { get; private set; }

        public bool InterferenceDetected { get; set; }

        public bool RestoreConfirmed { get; private set; }

        internal bool ThrowOnCapture { get; set; }

        internal bool ThrowOnApply { get; set; }

        internal bool ThrowOnObserve { get; set; }

        internal bool ThrowOnRestore { get; set; }

        internal bool InterfereAfterFirstObservation { get; set; }

        internal int RestoreCount { get; private set; }

        private int ObservationCount { get; set; }

        public void CaptureBaseline()
        {
            _calls?.Add("capture");
            if (ThrowOnCapture)
            {
                throw new InvalidOperationException("capture failure");
            }

            OriginalValue = "original";
            RequestedValue = "requested";
        }

        public void Apply()
        {
            _calls?.Add("apply");
            if (ThrowOnApply)
            {
                throw new InvalidOperationException("apply failure");
            }
        }

        public void Observe()
        {
            _calls?.Add("observe");
            ObservationCount++;
            if (ThrowOnObserve)
            {
                throw new InvalidOperationException("observe failure");
            }

            LocalObservedValue = "requested";
            RequestedValueObserved = true;
            if (InterfereAfterFirstObservation && ObservationCount > 1)
            {
                InterferenceDetected = true;
            }
        }

        public void Restore()
        {
            RestoreCount++;
            _calls?.Add("restore");
            if (ThrowOnRestore)
            {
                throw new InvalidOperationException("restore failure");
            }

            RestoreConfirmed = true;
            LocalObservedValue = "original";
        }
    }

    private sealed class RuntimeEventExperiment : ILabExperiment, IRuntimeMutationEventSource
    {
        private readonly List<MutationRuntimeEvent> _events = new();
        private int _observationCount;

        public string Id => "runtime";

        public string TargetToken => "player";

        public string? OriginalValue => "30";

        public string? RequestedValue => "30";

        public string? LocalObservedValue => "30";

        public bool RequestedValueObserved => true;

        public bool InterferenceDetected => false;

        public bool RestoreConfirmed { get; private set; }

        public void CaptureBaseline()
        {
            Queue("captured", MutationPhase.TARGET_TRACKED);
        }

        public void Apply()
        {
        }

        public void Observe()
        {
            Queue(_observationCount++ == 0 ? "started" : "tick", MutationPhase.WRITE_APPLIED);
        }

        public void Restore()
        {
            RestoreConfirmed = true;
            Queue("restore", MutationPhase.WRITE_APPLIED);
        }

        public IReadOnlyList<MutationRuntimeEvent> DrainRuntimeEvents()
        {
            var drained = _events.ToArray();
            _events.Clear();
            return drained;
        }

        private void Queue(string eventName, MutationPhase phase)
        {
            _events.Add(new MutationRuntimeEvent
            {
                Event = eventName,
                Phase = phase,
                OldValue = "29",
                NewValue = "30",
                Context = "target=ammo-1"
            });
        }
    }
}
