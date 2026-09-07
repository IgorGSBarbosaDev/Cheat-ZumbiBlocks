using System;
using System.Collections.Generic;
using System.Globalization;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Plugin.Experiments;

internal sealed class InfiniteAmmoMutationTest : ILabExperiment, IRuntimeMutationEventSource
{
    private readonly InfiniteAmmoController _controller;

    internal InfiniteAmmoMutationTest(PlayerMain player, string targetToken)
    {
        TargetToken = targetToken ?? throw new ArgumentNullException(nameof(targetToken));
        Runtime = new UnityAmmoRuntimeAdapter(player);
        _controller = new InfiniteAmmoController(Runtime);
    }

    internal UnityAmmoRuntimeAdapter Runtime { get; }

    public string Id => "INFINITE_AMMO";

    public string TargetToken { get; }

    public string? OriginalValue => _controller.OriginalValue;

    public string? RequestedValue => _controller.RequestedValue;

    public string? LocalObservedValue => _controller.LocalObservedValue;

    public bool RequestedValueObserved => _controller.RequestedValueObserved;

    public bool InterferenceDetected => _controller.InterferenceDetected;

    public bool RestoreConfirmed => _controller.RestoreConfirmed;

    internal string? ActiveTarget => _controller.ActiveTarget;

    internal int TrackedTargetCount => _controller.TrackedTargetCount;

    internal int WriteCount => _controller.WriteCount;

    internal string? PausedReason => _controller.PausedReason;

    internal bool CanStart(out string reason)
    {
        try
        {
            var policy = new AmmoMaintenancePolicy();
            var decision = policy.Evaluate(Runtime.CaptureSelected());
            reason = decision.Reason;
            return decision.Action == AmmoMaintenanceAction.TRACK;
        }
        catch (Exception exception)
        {
            reason = exception.Message;
            return false;
        }
    }

    public void CaptureBaseline() => _controller.CaptureBaseline();

    public void Apply() => _controller.Arm();

    public void Observe() => _controller.Observe();

    public void Restore() => _controller.Restore();

    public IReadOnlyList<MutationRuntimeEvent> DrainRuntimeEvents() => _controller.DrainRuntimeEvents();
}

internal sealed class UnityAmmoRuntimeAdapter : IAmmoRuntimeAdapter
{
    private readonly PlayerMain _player;
    private readonly List<TargetRegistration> _registrations = new();
    private int _nextTargetId = 1;

    internal UnityAmmoRuntimeAdapter(PlayerMain player)
    {
        _player = player == null ? throw new ArgumentNullException(nameof(player)) : player;
    }

    public AmmoObservation CaptureSelected()
    {
        EnsurePlayerAvailable();
        var arms = _player.arms;
        var inventory = _player.inventory;
        if (arms == null || inventory == null || inventory.equippedItems == null)
        {
            return new AmmoObservation
            {
                SelectionKind = AmmoSelectionKind.GUN,
                TargetAvailable = false,
                Context = "weaponContext=UNAVAILABLE"
            };
        }

        if (!arms.selectedItem.Exists)
        {
            return PauseObservation(AmmoSelectionKind.NONE, "selection=NONE");
        }

        var item = inventory.GetEquipment(arms.selectedItem);
        if (item == null || item.IsNone)
        {
            return PauseObservation(AmmoSelectionKind.NONE, $"slot={FormatSlot(arms.selectedItem)};selection=EMPTY");
        }

        var registration = GetOrCreateRegistration(item, arms.selectedItem);
        return CaptureRegistration(registration, requireOriginalSlot: true);
    }

    public AmmoObservation CaptureTarget(string targetToken)
    {
        EnsurePlayerAvailable();
        var registration = FindRegistration(targetToken);
        if (registration is null)
        {
            return new AmmoObservation
            {
                SelectionKind = AmmoSelectionKind.GUN,
                TargetAvailable = false,
                TargetToken = targetToken,
                Context = $"target={targetToken};availability=UNKNOWN"
            };
        }

        return CaptureRegistration(registration, requireOriginalSlot: true);
    }

    public AmmoWriteResult WriteAmmo(string targetToken, int expectedAmmo, int requestedAmmo)
    {
        AmmoObservation observation;
        try
        {
            observation = CaptureTarget(targetToken);
        }
        catch (Exception exception)
        {
            return FailedWrite(expectedAmmo, requestedAmmo, null, exception.Message);
        }

        if (!observation.TargetAvailable || observation.SelectionKind != AmmoSelectionKind.GUN)
        {
            return FailedWrite(observation.CurrentAmmo, requestedAmmo, observation.Context, "AMMO_TARGET_UNAVAILABLE");
        }

        var registration = FindRegistration(targetToken);
        if (registration is null)
        {
            return FailedWrite(observation.CurrentAmmo, requestedAmmo, observation.Context, "AMMO_TARGET_UNAVAILABLE");
        }

        if (observation.CurrentAmmo != expectedAmmo)
        {
            return FailedWrite(observation.CurrentAmmo, requestedAmmo, observation.Context, "AMMO_COMPARE_AND_SET_MISMATCH");
        }

        registration.Item.ammo = requestedAmmo;
        var observedAfter = registration.Item.ammo;
        return new AmmoWriteResult
        {
            Succeeded = observedAfter == requestedAmmo,
            ObservedBefore = observation.CurrentAmmo,
            RequestedAmmo = requestedAmmo,
            ObservedAfter = observedAfter,
            Context = FormatContext(registration, observation.WeaponId, observation.CurrentAmmo, observation.MaxAmmo, observation.AmmoConsumption),
            Error = observedAfter == requestedAmmo ? null : "AMMO_WRITE_NOT_CONFIRMED"
        };
    }

    private AmmoObservation CaptureRegistration(TargetRegistration registration, bool requireOriginalSlot)
    {
        var inventory = _player.inventory;
        if (inventory == null || inventory.equippedItems == null)
        {
            return Unavailable(registration, "inventory=UNAVAILABLE");
        }

        InventoryItem? current;
        try
        {
            current = inventory.GetEquipment(registration.Slot);
        }
        catch (Exception exception)
        {
            return Unavailable(registration, $"resolutionError={exception.GetType().Name}");
        }

        if (current == null || current.IsNone || !ReferenceEquals(current, registration.Item))
        {
            return Unavailable(registration, requireOriginalSlot ? "availability=REMOVED_OR_REPLACED" : "availability=UNAVAILABLE");
        }

        var databaseItem = current.GetDataBaseItem();
        var databaseGun = databaseItem as DatabaseGun;
        if (databaseGun == null)
        {
            return new AmmoObservation
            {
                SelectionKind = AmmoSelectionKind.NON_GUN,
                TargetAvailable = true,
                TargetToken = registration.Token,
                WeaponId = current.id.ToString(),
                Slot = FormatSlot(registration.Slot),
                Context = $"target={registration.Token};weapon={current.id};slot={FormatSlot(registration.Slot)};selection=NON_GUN"
            };
        }

        return new AmmoObservation
        {
            SelectionKind = AmmoSelectionKind.GUN,
            TargetAvailable = true,
            TargetToken = registration.Token,
            WeaponId = current.id.ToString(),
            Slot = FormatSlot(registration.Slot),
            CurrentAmmo = current.ammo,
            MaxAmmo = databaseGun.maxAmmo,
            AmmoConsumption = databaseGun.ammoConsumption,
            Context = FormatContext(registration, current.id.ToString(), current.ammo, databaseGun.maxAmmo, databaseGun.ammoConsumption)
        };
    }

    private TargetRegistration GetOrCreateRegistration(InventoryItem item, EquipmentIndex slot)
    {
        foreach (var registration in _registrations)
        {
            if (ReferenceEquals(registration.Item, item))
            {
                return registration;
            }
        }

        var created = new TargetRegistration(
            $"ammo-{_nextTargetId++.ToString(CultureInfo.InvariantCulture)}",
            item,
            slot);
        _registrations.Add(created);
        return created;
    }

    private TargetRegistration? FindRegistration(string token)
    {
        foreach (var registration in _registrations)
        {
            if (string.Equals(registration.Token, token, StringComparison.Ordinal))
            {
                return registration;
            }
        }

        return null;
    }

    private void EnsurePlayerAvailable()
    {
        if (_player == null)
        {
            throw new InvalidOperationException("Captured PlayerMain is no longer available.");
        }
    }

    private static AmmoObservation PauseObservation(AmmoSelectionKind kind, string context)
    {
        return new AmmoObservation
        {
            SelectionKind = kind,
            TargetAvailable = true,
            Context = context
        };
    }

    private static AmmoObservation Unavailable(TargetRegistration registration, string detail)
    {
        return new AmmoObservation
        {
            SelectionKind = AmmoSelectionKind.GUN,
            TargetAvailable = false,
            TargetToken = registration.Token,
            WeaponId = registration.Item.id.ToString(),
            Slot = FormatSlot(registration.Slot),
            CurrentAmmo = registration.Item.ammo,
            Context = $"target={registration.Token};weapon={registration.Item.id};slot={FormatSlot(registration.Slot)};{detail}"
        };
    }

    private static AmmoWriteResult FailedWrite(int observedBefore, int requestedAmmo, string? context, string error)
    {
        return new AmmoWriteResult
        {
            Succeeded = false,
            ObservedBefore = observedBefore,
            RequestedAmmo = requestedAmmo,
            ObservedAfter = observedBefore,
            Context = context,
            Error = error
        };
    }

    private static string FormatContext(
        TargetRegistration registration,
        string? weaponId,
        int ammo,
        int maxAmmo,
        int ammoConsumption)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "target={0};weapon={1};slot={2};ammo={3};maxAmmo={4};ammoConsumption={5}",
            registration.Token,
            weaponId,
            FormatSlot(registration.Slot),
            ammo,
            maxAmmo,
            ammoConsumption);
    }

    private static string FormatSlot(EquipmentIndex slot)
    {
        return $"{slot.SetType}:{slot.Value.ToString(CultureInfo.InvariantCulture)}";
    }

    private sealed class TargetRegistration
    {
        internal TargetRegistration(string token, InventoryItem item, EquipmentIndex slot)
        {
            Token = token;
            Item = item;
            Slot = slot;
        }

        internal string Token { get; }

        internal InventoryItem Item { get; }

        internal EquipmentIndex Slot { get; }
    }
}
