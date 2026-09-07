using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class AmmoMaintenancePolicyTests
{
    [TestMethod]
    public void Evaluate_TracksOnlyANewFullValidGun()
    {
        var policy = new AmmoMaintenancePolicy();

        var decision = policy.Evaluate(Gun("first", ammo: 30, maxAmmo: 30));

        Assert.AreEqual(AmmoMaintenanceAction.TRACK, decision.Action);
        Assert.AreEqual(1, policy.TrackedTargets.Count);
        Assert.AreEqual("first", policy.TrackedTargets[0].TargetToken);
    }

    [TestMethod]
    public void Evaluate_PausesEmptyMeleePartialAndInvalidNewSelections()
    {
        var policy = new AmmoMaintenancePolicy();

        AssertDecision(policy.Evaluate(new AmmoObservation { SelectionKind = AmmoSelectionKind.NONE }), AmmoMaintenanceAction.PAUSE, "NO_WEAPON_SELECTED");
        AssertDecision(policy.Evaluate(new AmmoObservation { SelectionKind = AmmoSelectionKind.NON_GUN }), AmmoMaintenanceAction.PAUSE, "SELECTED_ITEM_IS_NOT_A_GUN");
        AssertDecision(policy.Evaluate(Gun("partial", ammo: 29, maxAmmo: 30)), AmmoMaintenanceAction.PAUSE, "MAGAZINE_NOT_FULL");
        AssertDecision(policy.Evaluate(Gun("invalid", ammo: 0, maxAmmo: 0, consumption: 0)), AmmoMaintenanceAction.PAUSE, "INVALID_AMMO_CONFIGURATION");
        Assert.AreEqual(0, policy.TrackedTargets.Count);
    }

    [TestMethod]
    public void Evaluate_WritesOnlyAfterTrackedAmmoDrops()
    {
        var policy = new AmmoMaintenancePolicy();
        policy.Evaluate(Gun("first", ammo: 30, maxAmmo: 30));

        var unchanged = policy.Evaluate(Gun("first", ammo: 30, maxAmmo: 30));
        var consumed = policy.Evaluate(Gun("first", ammo: 21, maxAmmo: 30));

        Assert.AreEqual(AmmoMaintenanceAction.NONE, unchanged.Action);
        Assert.AreEqual(AmmoMaintenanceAction.WRITE, consumed.Action);
        Assert.AreEqual(30, consumed.RequestedAmmo);
    }

    [TestMethod]
    public void Evaluate_TracksDistinctTargetsEvenWithSameWeaponId()
    {
        var policy = new AmmoMaintenancePolicy();

        policy.Evaluate(Gun("first", weaponId: "HiPoint", slot: "Weapon:0"));
        policy.Evaluate(Gun("second", weaponId: "HiPoint", slot: "Weapon:1"));

        Assert.AreEqual(2, policy.TrackedTargets.Count);
        Assert.AreEqual("first", policy.TrackedTargets[0].TargetToken);
        Assert.AreEqual("second", policy.TrackedTargets[1].TargetToken);
    }

    [TestMethod]
    public void Evaluate_FailsClosedForRemovedTargetMetadataDriftAndInvalidValue()
    {
        var policy = new AmmoMaintenancePolicy();
        policy.Evaluate(Gun("first"));

        var removed = Gun("first");
        removed.TargetAvailable = false;
        AssertDecision(policy.Evaluate(removed), AmmoMaintenanceAction.FAIL, "AMMO_TARGET_UNAVAILABLE");

        AssertDecision(
            policy.Evaluate(Gun("first", consumption: 2)),
            AmmoMaintenanceAction.FAIL,
            "AMMO_TARGET_CONFIGURATION_CHANGED");

        var changedType = Gun("first");
        changedType.SelectionKind = AmmoSelectionKind.NON_GUN;
        AssertDecision(
            policy.Evaluate(changedType),
            AmmoMaintenanceAction.FAIL,
            "AMMO_TARGET_CONFIGURATION_CHANGED");

        AssertDecision(
            policy.Evaluate(Gun("first", ammo: 31)),
            AmmoMaintenanceAction.FAIL,
            "AMMO_VALUE_OUT_OF_RANGE");
    }

    private static AmmoObservation Gun(
        string token,
        int ammo = 30,
        int maxAmmo = 30,
        int consumption = 1,
        string weaponId = "HiPoint",
        string slot = "Weapon:0")
    {
        return new AmmoObservation
        {
            SelectionKind = AmmoSelectionKind.GUN,
            TargetToken = token,
            WeaponId = weaponId,
            Slot = slot,
            CurrentAmmo = ammo,
            MaxAmmo = maxAmmo,
            AmmoConsumption = consumption,
            Context = $"target={token};weapon={weaponId};slot={slot}"
        };
    }

    private static void AssertDecision(AmmoMaintenanceDecision decision, AmmoMaintenanceAction action, string reason)
    {
        Assert.AreEqual(action, decision.Action);
        Assert.AreEqual(reason, decision.Reason);
    }
}
