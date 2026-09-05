using System;
using System.Collections.Generic;
using System.Globalization;

namespace ZB2SecurityLab.Core.Diagnostics;

public enum PlayerStateTransitionKind
{
    PLAYER_SPAWNED,
    PLAYER_DIED,
    HEALTH_CHANGED,
    WEAPON_CHANGED,
    AMMO_CHANGED,
    INVENTORY_CHANGED
}

public sealed class PlayerStateTransition
{
    public PlayerStateTransition(PlayerStateTransitionKind kind, string? previousValue, string? currentValue)
    {
        Kind = kind;
        PreviousValue = previousValue;
        CurrentValue = currentValue;
    }

    public PlayerStateTransitionKind Kind { get; }

    public string? PreviousValue { get; }

    public string? CurrentValue { get; }
}

public sealed class PlayerStateTracker
{
    private static readonly TimeSpan ContinuousEventInterval = TimeSpan.FromSeconds(1);

    private string? _playerToken;
    private bool _playerAvailable;
    private bool _dead;
    private string? _healthState;
    private string? _lastEmittedHealth;
    private DateTimeOffset? _lastHealthEventAt;
    private string? _weaponId;
    private int? _lastEmittedAmmo;
    private DateTimeOffset? _lastAmmoEventAt;
    private string? _inventorySignature;

    public IReadOnlyList<PlayerStateTransition> Observe(LabSnapshot current, DateTimeOffset observedAt)
    {
        if (current is null)
        {
            throw new ArgumentNullException(nameof(current));
        }

        var transitions = new List<PlayerStateTransition>();
        if (!current.LocalPlayerAvailable || current.PlayerState is null)
        {
            Reset();
            return transitions;
        }

        if (!_playerAvailable || !string.Equals(_playerToken, current.LocalPlayerToken, StringComparison.Ordinal))
        {
            Initialize(current);
            transitions.Add(new PlayerStateTransition(
                PlayerStateTransitionKind.PLAYER_SPAWNED,
                null,
                current.LocalPlayerToken));
            return transitions;
        }

        var dead = IsDead(current.PlayerState);
        if (!_dead && dead)
        {
            transitions.Add(new PlayerStateTransition(
                PlayerStateTransitionKind.PLAYER_DIED,
                _healthState,
                "Dead"));
        }

        _dead = dead;
        _healthState = current.PlayerState.HealthState;
        TrackHealth(current, observedAt, transitions);
        TrackWeapon(current, transitions);
        TrackAmmo(current, observedAt, transitions);
        TrackInventory(current, transitions);
        return transitions;
    }

    private void Initialize(LabSnapshot current)
    {
        _playerAvailable = true;
        _playerToken = current.LocalPlayerToken;
        _dead = IsDead(current.PlayerState!);
        _healthState = current.PlayerState!.HealthState;
        _lastEmittedHealth = DescribeHealth(current.PlayerState!);
        _lastHealthEventAt = null;
        _weaponId = current.Weapon?.Id;
        _lastEmittedAmmo = current.Weapon?.Ammo;
        _lastAmmoEventAt = null;
        _inventorySignature = current.Inventory?.Signature;
    }

    private void TrackHealth(
        LabSnapshot current,
        DateTimeOffset observedAt,
        ICollection<PlayerStateTransition> transitions)
    {
        var health = DescribeHealth(current.PlayerState!);
        if (string.Equals(_lastEmittedHealth, health, StringComparison.Ordinal) ||
            !IntervalElapsed(_lastHealthEventAt, observedAt))
        {
            return;
        }

        transitions.Add(new PlayerStateTransition(
            PlayerStateTransitionKind.HEALTH_CHANGED,
            _lastEmittedHealth,
            health));
        _lastEmittedHealth = health;
        _lastHealthEventAt = observedAt;
    }

    private void TrackWeapon(LabSnapshot current, ICollection<PlayerStateTransition> transitions)
    {
        var weaponId = current.Weapon?.Id;
        if (string.Equals(_weaponId, weaponId, StringComparison.Ordinal))
        {
            return;
        }

        transitions.Add(new PlayerStateTransition(
            PlayerStateTransitionKind.WEAPON_CHANGED,
            _weaponId,
            weaponId));
        _weaponId = weaponId;
        _lastEmittedAmmo = current.Weapon?.Ammo;
        _lastAmmoEventAt = null;
    }

    private void TrackAmmo(
        LabSnapshot current,
        DateTimeOffset observedAt,
        ICollection<PlayerStateTransition> transitions)
    {
        var ammo = current.Weapon?.Ammo;
        if (_lastEmittedAmmo == ammo || !IntervalElapsed(_lastAmmoEventAt, observedAt))
        {
            return;
        }

        transitions.Add(new PlayerStateTransition(
            PlayerStateTransitionKind.AMMO_CHANGED,
            FormatNullable(_lastEmittedAmmo),
            FormatNullable(ammo)));
        _lastEmittedAmmo = ammo;
        _lastAmmoEventAt = observedAt;
    }

    private void TrackInventory(LabSnapshot current, ICollection<PlayerStateTransition> transitions)
    {
        var signature = current.Inventory?.Signature;
        if (string.Equals(_inventorySignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        transitions.Add(new PlayerStateTransition(
            PlayerStateTransitionKind.INVENTORY_CHANGED,
            _inventorySignature,
            signature));
        _inventorySignature = signature;
    }

    private void Reset()
    {
        _playerAvailable = false;
        _playerToken = null;
        _dead = false;
        _healthState = null;
        _lastEmittedHealth = null;
        _lastHealthEventAt = null;
        _weaponId = null;
        _lastEmittedAmmo = null;
        _lastAmmoEventAt = null;
        _inventorySignature = null;
    }

    private static bool IntervalElapsed(DateTimeOffset? previous, DateTimeOffset current)
    {
        return !previous.HasValue || current - previous.Value >= ContinuousEventInterval;
    }

    private static bool IsDead(PlayerStateSnapshot state)
    {
        return string.Equals(state.HealthState, "Dead", StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeHealth(PlayerStateSnapshot state)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "fast={0:0.###};slow={1:0.###};max={2:0.###};state={3}",
            state.HealthFast,
            state.HealthSlow,
            state.MaxHealth,
            state.HealthState);
    }

    private static string? FormatNullable(int? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture);
    }
}
