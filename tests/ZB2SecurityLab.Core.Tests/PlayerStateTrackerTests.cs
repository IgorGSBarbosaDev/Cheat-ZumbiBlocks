using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class PlayerStateTrackerTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void Observe_EmitsSpawnOnceAndAcceptsMissingOptionalSections()
    {
        var tracker = new PlayerStateTracker();
        var snapshot = Snapshot("101", healthFast: 100f, ammo: null, inventorySignature: null);
        snapshot.Weapon = null;
        snapshot.Inventory = null;

        var first = tracker.Observe(snapshot, Start);
        var unchanged = tracker.Observe(snapshot, Start.AddSeconds(1));

        Assert.AreEqual(1, first.Count);
        Assert.AreEqual(PlayerStateTransitionKind.PLAYER_SPAWNED, first[0].Kind);
        Assert.AreEqual("101", first[0].CurrentValue);
        Assert.AreEqual(0, unchanged.Count);
    }

    [TestMethod]
    public void Observe_EmitsDeathOnlyOnAliveToDeadTransition()
    {
        var tracker = new PlayerStateTracker();
        tracker.Observe(Snapshot("101", 100f, 30, "inventory-a"), Start);

        var dead = Snapshot("101", 0f, 30, "inventory-a");
        dead.PlayerState!.HealthState = "Dead";
        var firstDeadPoll = tracker.Observe(dead, Start.AddSeconds(1));
        var secondDeadPoll = tracker.Observe(dead, Start.AddSeconds(2));

        var transition = firstDeadPoll.Single(item => item.Kind == PlayerStateTransitionKind.PLAYER_DIED);
        Assert.AreEqual("Alive", transition.PreviousValue);
        Assert.AreEqual("Dead", transition.CurrentValue);
        Assert.AreEqual(0, secondDeadPoll.Count);
    }

    [TestMethod]
    public void Observe_CoalescesHealthAndAmmoChangesForOneSecond()
    {
        var tracker = new PlayerStateTracker();
        tracker.Observe(Snapshot("101", 100f, 30, "inventory-a"), Start);

        var first = tracker.Observe(Snapshot("101", 90f, 29, "inventory-a"), Start.AddMilliseconds(100));
        var throttled = tracker.Observe(Snapshot("101", 80f, 25, "inventory-a"), Start.AddMilliseconds(500));
        var coalesced = tracker.Observe(Snapshot("101", 70f, 20, "inventory-a"), Start.AddMilliseconds(1100));

        CollectionAssert.AreEquivalent(
            new[] { PlayerStateTransitionKind.HEALTH_CHANGED, PlayerStateTransitionKind.AMMO_CHANGED },
            first.Select(item => item.Kind).ToArray());
        Assert.AreEqual(0, throttled.Count);
        Assert.AreEqual("fast=90;slow=90;max=100;state=Alive", coalesced.Single(item => item.Kind == PlayerStateTransitionKind.HEALTH_CHANGED).PreviousValue);
        Assert.AreEqual("fast=70;slow=70;max=100;state=Alive", coalesced.Single(item => item.Kind == PlayerStateTransitionKind.HEALTH_CHANGED).CurrentValue);
        Assert.AreEqual("29", coalesced.Single(item => item.Kind == PlayerStateTransitionKind.AMMO_CHANGED).PreviousValue);
        Assert.AreEqual("20", coalesced.Single(item => item.Kind == PlayerStateTransitionKind.AMMO_CHANGED).CurrentValue);
    }

    [TestMethod]
    public void Observe_EmitsWeaponChangeWithoutDuplicateAmmoChange()
    {
        var tracker = new PlayerStateTracker();
        tracker.Observe(Snapshot("101", 100f, 30, "inventory-a", "HiPoint"), Start);

        var changed = tracker.Observe(Snapshot("101", 100f, 12, "inventory-a", "Luger"), Start.AddSeconds(1));

        var weapon = changed.Single(item => item.Kind == PlayerStateTransitionKind.WEAPON_CHANGED);
        Assert.AreEqual("HiPoint", weapon.PreviousValue);
        Assert.AreEqual("Luger", weapon.CurrentValue);
        Assert.IsFalse(changed.Any(item => item.Kind == PlayerStateTransitionKind.AMMO_CHANGED));
    }

    [TestMethod]
    public void Observe_EmitsInventoryChangeButIgnoresMovementAndStaminaNoise()
    {
        var tracker = new PlayerStateTracker();
        tracker.Observe(Snapshot("101", 100f, 30, "inventory-a"), Start);
        var changed = Snapshot("101", 100f, 30, "inventory-b");
        changed.PlayerState!.StaminaFast = 25f;
        changed.Movement = new MovementStateSnapshot
        {
            State = "Sprint",
            Velocity = new LabVector3(20f, 0f, 10f)
        };

        var transitions = tracker.Observe(changed, Start.AddSeconds(1));

        Assert.AreEqual(1, transitions.Count);
        Assert.AreEqual(PlayerStateTransitionKind.INVENTORY_CHANGED, transitions[0].Kind);
    }

    [TestMethod]
    public void Observe_ReinitializesAfterPlayerLossOrReplacement()
    {
        var tracker = new PlayerStateTracker();
        tracker.Observe(Snapshot("101", 100f, 30, "inventory-a"), Start);
        tracker.Observe(new LabSnapshot(), Start.AddSeconds(1));

        var reacquired = tracker.Observe(Snapshot("202", 100f, 30, "inventory-a"), Start.AddSeconds(2));

        Assert.AreEqual(1, reacquired.Count);
        Assert.AreEqual(PlayerStateTransitionKind.PLAYER_SPAWNED, reacquired[0].Kind);
        Assert.AreEqual("202", reacquired[0].CurrentValue);
    }

    private static LabSnapshot Snapshot(
        string playerToken,
        float healthFast,
        int? ammo,
        string? inventorySignature,
        string weaponId = "HiPoint")
    {
        return new LabSnapshot
        {
            InGame = true,
            LocalPlayerAvailable = true,
            HasLocalControl = true,
            LocalPlayerToken = playerToken,
            Role = "SINGLE_PLAYER",
            PlayerState = new PlayerStateSnapshot
            {
                HealthFast = healthFast,
                HealthSlow = healthFast,
                MaxHealth = 100f,
                StaminaFast = 100f,
                StaminaSlow = 100f,
                MaxStamina = 100f,
                HealthState = "Alive"
            },
            Weapon = new WeaponStateSnapshot
            {
                Id = weaponId,
                Ammo = ammo
            },
            Inventory = inventorySignature == null
                ? null
                : new InventoryStateSnapshot { Signature = inventorySignature }
        };
    }
}
