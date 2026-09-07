using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class InfiniteAmmoControllerTests
{
    [TestMethod]
    public void CaptureBaseline_ArmsWithoutWriting()
    {
        var runtime = RuntimeWith("first", ammo: 30);
        var controller = new InfiniteAmmoController(runtime);

        controller.CaptureBaseline();
        controller.Arm();

        Assert.AreEqual(0, runtime.Writes.Count);
        Assert.AreEqual(1, controller.TrackedTargetCount);
        Assert.IsFalse(controller.RequestedValueObserved);
        Assert.AreEqual(MutationPhase.TARGET_TRACKED, controller.DrainRuntimeEvents().Single().Phase);
    }

    [TestMethod]
    public void CaptureBaseline_RejectsPartialMagazine()
    {
        var runtime = RuntimeWith("first", ammo: 29);
        var controller = new InfiniteAmmoController(runtime);

        var exception = Assert.ThrowsException<InvalidOperationException>(() => controller.CaptureBaseline());

        Assert.AreEqual("MAGAZINE_NOT_FULL", exception.Message);
        Assert.AreEqual(0, runtime.Writes.Count);
    }

    [TestMethod]
    public void Observe_RefillsConsumedAmmoAndEmitsOneWritePerChange()
    {
        var runtime = RuntimeWith("first", ammo: 30);
        var controller = Start(runtime);
        controller.DrainRuntimeEvents();
        runtime.Targets["first"].Ammo = 21;

        controller.Observe();
        controller.Observe();

        Assert.AreEqual(30, runtime.Targets["first"].Ammo);
        Assert.AreEqual(1, runtime.Writes.Count);
        Assert.AreEqual(1, controller.WriteCount);
        Assert.IsTrue(controller.RequestedValueObserved);
        var write = controller.DrainRuntimeEvents().Single();
        Assert.AreEqual(MutationPhase.WRITE_APPLIED, write.Phase);
        Assert.AreEqual("21", write.OldValue);
        Assert.AreEqual("30", write.NewValue);
    }

    [TestMethod]
    public void Observe_FollowsFullWeaponsAndPausesIneligibleSelections()
    {
        var runtime = RuntimeWith("first", ammo: 30);
        runtime.Add("second", ammo: 20, maxAmmo: 20, slot: "Weapon:1", weaponId: "Luger");
        runtime.Add("partial", ammo: 5, maxAmmo: 10, slot: "Weapon:2", weaponId: "C96");
        var controller = Start(runtime);
        controller.DrainRuntimeEvents();

        runtime.SelectedToken = "second";
        controller.Observe();
        runtime.SelectedToken = "partial";
        controller.Observe();
        controller.Observe();
        runtime.SelectedToken = null;
        controller.Observe();
        runtime.SelectedToken = "first";
        runtime.Targets["first"].Ammo = 29;
        controller.Observe();

        Assert.AreEqual(2, controller.TrackedTargetCount);
        Assert.AreEqual(30, runtime.Targets["first"].Ammo);
        Assert.IsNull(controller.PausedReason);
        Assert.AreEqual(1, controller.WriteCount);
        var events = controller.DrainRuntimeEvents();
        Assert.AreEqual(1, events.Count(item => item.Phase == MutationPhase.TARGET_TRACKED));
        Assert.AreEqual(2, events.Count(item => item.Phase == MutationPhase.TARGET_PAUSED));
        Assert.AreEqual(1, events.Count(item => item.Phase == MutationPhase.WRITE_APPLIED));
    }

    [TestMethod]
    public void Observe_DeduplicatesRepeatedPauseEvents()
    {
        var runtime = RuntimeWith("first", ammo: 30);
        runtime.Add("partial", ammo: 5, maxAmmo: 10, slot: "Weapon:1");
        var controller = Start(runtime);
        controller.DrainRuntimeEvents();
        runtime.SelectedToken = "partial";

        controller.Observe();
        controller.Observe();

        Assert.AreEqual(1, controller.DrainRuntimeEvents().Count);
        Assert.AreEqual("MAGAZINE_NOT_FULL", controller.PausedReason);
    }

    [TestMethod]
    public void Observe_PausesMeleeWithoutWriting()
    {
        var runtime = RuntimeWith("first", ammo: 30);
        runtime.Add("melee", ammo: 0, maxAmmo: 0, slot: "Weapon:1");
        runtime.Targets["melee"].IsGun = false;
        var controller = Start(runtime);
        controller.DrainRuntimeEvents();
        runtime.SelectedToken = "melee";

        controller.Observe();

        Assert.AreEqual("SELECTED_ITEM_IS_NOT_A_GUN", controller.PausedReason);
        Assert.AreEqual(0, runtime.Writes.Count);
    }

    [TestMethod]
    public void Restore_UsesReverseTargetOrderAndContinuesAfterFailure()
    {
        var runtime = RuntimeWith("first", ammo: 30);
        runtime.Add("second", ammo: 20, maxAmmo: 20, slot: "Weapon:1");
        var controller = Start(runtime);
        runtime.SelectedToken = "second";
        controller.Observe();
        runtime.Targets["first"].Ammo = 29;
        runtime.Targets["second"].Ammo = 19;
        runtime.Targets["second"].FailWrites = true;

        var exception = Assert.ThrowsException<InvalidOperationException>(() => controller.Restore());

        StringAssert.Contains(exception.Message, "second");
        CollectionAssert.AreEqual(new[] { "second", "first" }, runtime.WriteAttempts.TakeLast(2).ToArray());
        Assert.AreEqual(30, runtime.Targets["first"].Ammo);
        Assert.AreEqual(19, runtime.Targets["second"].Ammo);
        Assert.IsFalse(controller.RestoreConfirmed);
    }

    [TestMethod]
    public void Restore_ContinuesWhenCapturingOneTargetThrows()
    {
        var runtime = RuntimeWith("first", ammo: 30);
        runtime.Add("second", ammo: 20, maxAmmo: 20, slot: "Weapon:1");
        var controller = Start(runtime);
        runtime.SelectedToken = "second";
        controller.Observe();
        runtime.Targets["first"].Ammo = 29;
        runtime.Targets["second"].Ammo = 19;
        runtime.Targets["second"].ThrowOnCapture = true;

        Assert.ThrowsException<InvalidOperationException>(() => controller.Restore());

        Assert.AreEqual(30, runtime.Targets["first"].Ammo);
        Assert.AreEqual(19, runtime.Targets["second"].Ammo);
        CollectionAssert.Contains(runtime.WriteAttempts, "first");
        Assert.IsFalse(controller.RestoreConfirmed);
    }

    [TestMethod]
    public void Observe_RemovedTargetFailsClosed()
    {
        var runtime = RuntimeWith("first", ammo: 30);
        var controller = Start(runtime);
        controller.DrainRuntimeEvents();
        runtime.Targets["first"].Available = false;

        var exception = Assert.ThrowsException<InvalidOperationException>(() => controller.Observe());

        Assert.AreEqual("AMMO_TARGET_UNAVAILABLE", exception.Message);
        Assert.IsTrue(controller.InterferenceDetected);
        Assert.AreEqual(MutationPhase.WRITE_FAILED, controller.DrainRuntimeEvents().Single().Phase);
    }

    [TestMethod]
    public void Coordinator_ClassifiesNoShotInconclusiveAndConfirmedRefillLocalOnly()
    {
        var noShotRuntime = RuntimeWith("first", ammo: 30);
        var noShot = new ControllerExperiment(noShotRuntime);
        var noShotCoordinator = new ExperimentCoordinator();
        noShotCoordinator.Start(noShot, 0d);
        noShotCoordinator.Stop(RestoreReason.MANUAL);

        Assert.AreEqual(TestOutcome.INCONCLUSIVE, noShotCoordinator.LastResult!.Outcome);

        var refillRuntime = RuntimeWith("first", ammo: 30);
        var refill = new ControllerExperiment(refillRuntime);
        var refillCoordinator = new ExperimentCoordinator();
        refillCoordinator.Start(refill, 0d);
        refillRuntime.Targets["first"].Ammo = 29;
        refillCoordinator.Tick(1d, new MutationGuardDecision(true, "ALLOWED_SINGLE_PLAYER"), refill.TargetToken);
        refillCoordinator.Stop(RestoreReason.MANUAL);

        Assert.AreEqual(TestOutcome.LOCAL_ONLY, refillCoordinator.LastResult!.Outcome);
        Assert.IsTrue(refillCoordinator.LastResult.RestoreSucceeded);
    }

    private static InfiniteAmmoController Start(FakeAmmoRuntime runtime)
    {
        var controller = new InfiniteAmmoController(runtime);
        controller.CaptureBaseline();
        controller.Arm();
        controller.Observe();
        return controller;
    }

    private static FakeAmmoRuntime RuntimeWith(string token, int ammo)
    {
        var runtime = new FakeAmmoRuntime { SelectedToken = token };
        runtime.Add(token, ammo, 30, "Weapon:0");
        return runtime;
    }

    private sealed class FakeAmmoRuntime : IAmmoRuntimeAdapter
    {
        internal Dictionary<string, Target> Targets { get; } = new(StringComparer.Ordinal);

        internal List<string> Writes { get; } = new();

        internal List<string> WriteAttempts { get; } = new();

        internal string? SelectedToken { get; set; }

        internal void Add(string token, int ammo, int maxAmmo, string slot, string weaponId = "HiPoint")
        {
            Targets.Add(token, new Target
            {
                Token = token,
                WeaponId = weaponId,
                Slot = slot,
                Ammo = ammo,
                MaxAmmo = maxAmmo
            });
        }

        public AmmoObservation CaptureSelected()
        {
            return SelectedToken is null
                ? new AmmoObservation { SelectionKind = AmmoSelectionKind.NONE, Context = "selection=NONE" }
                : CaptureTarget(SelectedToken);
        }

        public AmmoObservation CaptureTarget(string targetToken)
        {
            var target = Targets[targetToken];
            if (target.ThrowOnCapture)
            {
                throw new InvalidOperationException("CAPTURE_FAILED");
            }

            return new AmmoObservation
            {
                SelectionKind = target.IsGun ? AmmoSelectionKind.GUN : AmmoSelectionKind.NON_GUN,
                TargetAvailable = target.Available,
                TargetToken = target.Token,
                WeaponId = target.WeaponId,
                Slot = target.Slot,
                CurrentAmmo = target.Ammo,
                MaxAmmo = target.MaxAmmo,
                AmmoConsumption = target.Consumption,
                Context = $"target={target.Token};weapon={target.WeaponId};slot={target.Slot}"
            };
        }

        public AmmoWriteResult WriteAmmo(string targetToken, int expectedAmmo, int requestedAmmo)
        {
            WriteAttempts.Add(targetToken);
            var target = Targets[targetToken];
            if (target.FailWrites || !target.Available || target.Ammo != expectedAmmo)
            {
                return new AmmoWriteResult
                {
                    Succeeded = false,
                    ObservedBefore = target.Ammo,
                    RequestedAmmo = requestedAmmo,
                    ObservedAfter = target.Ammo,
                    Context = $"target={targetToken}",
                    Error = "WRITE_FAILED"
                };
            }

            target.Ammo = requestedAmmo;
            Writes.Add(targetToken);
            return new AmmoWriteResult
            {
                Succeeded = true,
                ObservedBefore = expectedAmmo,
                RequestedAmmo = requestedAmmo,
                ObservedAfter = target.Ammo,
                Context = $"target={targetToken}"
            };
        }
    }

    private sealed class Target
    {
        internal string Token { get; set; } = string.Empty;

        internal string WeaponId { get; set; } = string.Empty;

        internal string Slot { get; set; } = string.Empty;

        internal int Ammo { get; set; }

        internal int MaxAmmo { get; set; }

        internal int Consumption { get; set; } = 1;

        internal bool IsGun { get; set; } = true;

        internal bool Available { get; set; } = true;

        internal bool FailWrites { get; set; }

        internal bool ThrowOnCapture { get; set; }
    }

    private sealed class ControllerExperiment : ILabExperiment, IRuntimeMutationEventSource
    {
        private readonly InfiniteAmmoController _controller;

        internal ControllerExperiment(IAmmoRuntimeAdapter runtime)
        {
            _controller = new InfiniteAmmoController(runtime);
        }

        public string Id => "INFINITE_AMMO";

        public string TargetToken => "player";

        public string? OriginalValue => _controller.OriginalValue;

        public string? RequestedValue => _controller.RequestedValue;

        public string? LocalObservedValue => _controller.LocalObservedValue;

        public bool RequestedValueObserved => _controller.RequestedValueObserved;

        public bool InterferenceDetected => _controller.InterferenceDetected;

        public bool RestoreConfirmed => _controller.RestoreConfirmed;

        public void CaptureBaseline() => _controller.CaptureBaseline();

        public void Apply() => _controller.Arm();

        public void Observe() => _controller.Observe();

        public void Restore() => _controller.Restore();

        public IReadOnlyList<MutationRuntimeEvent> DrainRuntimeEvents() => _controller.DrainRuntimeEvents();
    }
}
