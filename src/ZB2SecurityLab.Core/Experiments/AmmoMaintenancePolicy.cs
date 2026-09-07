using System;
using System.Collections.Generic;

namespace ZB2SecurityLab.Core.Experiments;

public enum AmmoSelectionKind
{
    NONE,
    NON_GUN,
    GUN
}

public enum AmmoMaintenanceAction
{
    NONE,
    TRACK,
    PAUSE,
    WRITE,
    FAIL
}

public sealed class AmmoObservation
{
    public AmmoSelectionKind SelectionKind { get; set; }

    public bool TargetAvailable { get; set; } = true;

    public string? TargetToken { get; set; }

    public string? WeaponId { get; set; }

    public string? Slot { get; set; }

    public int CurrentAmmo { get; set; }

    public int MaxAmmo { get; set; }

    public int AmmoConsumption { get; set; }

    public string? Context { get; set; }
}

public sealed class AmmoTargetBaseline
{
    internal AmmoTargetBaseline(AmmoObservation observation)
    {
        TargetToken = observation.TargetToken!;
        WeaponId = observation.WeaponId!;
        Slot = observation.Slot!;
        Ammo = observation.CurrentAmmo;
        MaxAmmo = observation.MaxAmmo;
        AmmoConsumption = observation.AmmoConsumption;
        Context = observation.Context;
    }

    public string TargetToken { get; }

    public string WeaponId { get; }

    public string Slot { get; }

    public int Ammo { get; }

    public int MaxAmmo { get; }

    public int AmmoConsumption { get; }

    public string? Context { get; }
}

public sealed class AmmoMaintenanceDecision
{
    internal AmmoMaintenanceDecision(AmmoMaintenanceAction action, string reason, int? requestedAmmo = null)
    {
        Action = action;
        Reason = reason;
        RequestedAmmo = requestedAmmo;
    }

    public AmmoMaintenanceAction Action { get; }

    public string Reason { get; }

    public int? RequestedAmmo { get; }
}

public sealed class AmmoMaintenancePolicy
{
    private readonly List<AmmoTargetBaseline> _trackedTargets = new();
    private readonly Dictionary<string, AmmoTargetBaseline> _targetsByToken = new(StringComparer.Ordinal);

    public IReadOnlyList<AmmoTargetBaseline> TrackedTargets => _trackedTargets;

    public AmmoMaintenanceDecision Evaluate(AmmoObservation observation)
    {
        if (observation is null)
        {
            throw new ArgumentNullException(nameof(observation));
        }

        if (!string.IsNullOrEmpty(observation.TargetToken) &&
            _targetsByToken.TryGetValue(observation.TargetToken, out var baseline))
        {
            if (!observation.TargetAvailable)
            {
                return new AmmoMaintenanceDecision(AmmoMaintenanceAction.FAIL, "AMMO_TARGET_UNAVAILABLE");
            }

            if (observation.SelectionKind != AmmoSelectionKind.GUN ||
                !string.Equals(observation.WeaponId, baseline.WeaponId, StringComparison.Ordinal) ||
                !string.Equals(observation.Slot, baseline.Slot, StringComparison.Ordinal) ||
                observation.MaxAmmo != baseline.MaxAmmo ||
                observation.AmmoConsumption != baseline.AmmoConsumption)
            {
                return new AmmoMaintenanceDecision(AmmoMaintenanceAction.FAIL, "AMMO_TARGET_CONFIGURATION_CHANGED");
            }

            if (observation.CurrentAmmo < 0 || observation.CurrentAmmo > baseline.MaxAmmo)
            {
                return new AmmoMaintenanceDecision(AmmoMaintenanceAction.FAIL, "AMMO_VALUE_OUT_OF_RANGE");
            }

            return observation.CurrentAmmo == baseline.Ammo
                ? new AmmoMaintenanceDecision(AmmoMaintenanceAction.NONE, "AMMO_AT_BASELINE")
                : new AmmoMaintenanceDecision(AmmoMaintenanceAction.WRITE, "AMMO_CONSUMPTION_OBSERVED", baseline.Ammo);
        }

        if (observation.SelectionKind == AmmoSelectionKind.NONE)
        {
            return new AmmoMaintenanceDecision(AmmoMaintenanceAction.PAUSE, "NO_WEAPON_SELECTED");
        }

        if (observation.SelectionKind == AmmoSelectionKind.NON_GUN)
        {
            return new AmmoMaintenanceDecision(AmmoMaintenanceAction.PAUSE, "SELECTED_ITEM_IS_NOT_A_GUN");
        }

        if (!observation.TargetAvailable || string.IsNullOrEmpty(observation.TargetToken))
        {
            return new AmmoMaintenanceDecision(AmmoMaintenanceAction.FAIL, "AMMO_TARGET_UNAVAILABLE");
        }

        if (string.IsNullOrEmpty(observation.WeaponId) || string.IsNullOrEmpty(observation.Slot))
        {
            return new AmmoMaintenanceDecision(AmmoMaintenanceAction.FAIL, "AMMO_TARGET_METADATA_MISSING");
        }

        if (observation.MaxAmmo <= 0 || observation.AmmoConsumption <= 0)
        {
            return new AmmoMaintenanceDecision(AmmoMaintenanceAction.PAUSE, "INVALID_AMMO_CONFIGURATION");
        }

        if (observation.CurrentAmmo < 0 || observation.CurrentAmmo > observation.MaxAmmo)
        {
            return new AmmoMaintenanceDecision(AmmoMaintenanceAction.FAIL, "AMMO_VALUE_OUT_OF_RANGE");
        }

        if (observation.CurrentAmmo != observation.MaxAmmo)
        {
            return new AmmoMaintenanceDecision(AmmoMaintenanceAction.PAUSE, "MAGAZINE_NOT_FULL");
        }

        var tracked = new AmmoTargetBaseline(observation);
        _trackedTargets.Add(tracked);
        _targetsByToken.Add(tracked.TargetToken, tracked);
        return new AmmoMaintenanceDecision(AmmoMaintenanceAction.TRACK, "AMMO_TARGET_TRACKED");
    }
}

public sealed class AmmoWriteResult
{
    public bool Succeeded { get; set; }

    public int ObservedBefore { get; set; }

    public int RequestedAmmo { get; set; }

    public int ObservedAfter { get; set; }

    public string? Context { get; set; }

    public string? Error { get; set; }
}

public interface IAmmoRuntimeAdapter
{
    AmmoObservation CaptureSelected();

    AmmoObservation CaptureTarget(string targetToken);

    AmmoWriteResult WriteAmmo(string targetToken, int expectedAmmo, int requestedAmmo);
}
