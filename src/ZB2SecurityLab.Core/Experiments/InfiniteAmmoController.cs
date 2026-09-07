using System;
using System.Collections.Generic;
using System.Globalization;

namespace ZB2SecurityLab.Core.Experiments;

public sealed class InfiniteAmmoController : IRuntimeMutationEventSource
{
    private readonly IAmmoRuntimeAdapter _runtime;
    private readonly AmmoMaintenancePolicy _policy = new();
    private readonly List<MutationRuntimeEvent> _runtimeEvents = new();
    private bool _baselineCaptured;
    private string? _lastPauseKey;

    public InfiniteAmmoController(IAmmoRuntimeAdapter runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string? OriginalValue { get; private set; }

    public string? RequestedValue { get; private set; }

    public string? LocalObservedValue { get; private set; }

    public bool RequestedValueObserved { get; private set; }

    public bool InterferenceDetected { get; private set; }

    public bool RestoreConfirmed { get; private set; }

    public string? ActiveTarget { get; private set; }

    public int TrackedTargetCount => _policy.TrackedTargets.Count;

    public int WriteCount { get; private set; }

    public string? PausedReason { get; private set; }

    public void CaptureBaseline()
    {
        var observation = _runtime.CaptureSelected();
        var decision = _policy.Evaluate(observation);
        if (decision.Action != AmmoMaintenanceAction.TRACK)
        {
            throw new InvalidOperationException(decision.Reason);
        }

        _baselineCaptured = true;
        OriginalValue = FormatValue(observation);
        RequestedValue = $"ammo={observation.MaxAmmo.ToString(CultureInfo.InvariantCulture)};mode=reactive";
        ActiveTarget = observation.Context;
        LocalObservedValue = FormatStatus("ARMED", observation, decision.Reason);
        QueueTargetEvent(MutationPhase.TARGET_TRACKED, "controlled_mutation_target_tracked", observation, decision.Reason);
    }

    public void Arm()
    {
        EnsureBaseline();
    }

    public void Observe()
    {
        EnsureBaseline();
        ValidateTrackedTargets();
        var observation = _runtime.CaptureSelected();
        var decision = _policy.Evaluate(observation);
        HandleDecision(observation, decision, countAsObservedWrite: true);
    }

    public void Restore()
    {
        EnsureBaseline();
        var errors = new List<string>();
        for (var index = _policy.TrackedTargets.Count - 1; index >= 0; index--)
        {
            var baseline = _policy.TrackedTargets[index];
            try
            {
                var observation = _runtime.CaptureTarget(baseline.TargetToken);
                var decision = _policy.Evaluate(observation);
                if (decision.Action == AmmoMaintenanceAction.NONE)
                {
                    continue;
                }

                if (decision.Action != AmmoMaintenanceAction.WRITE || !decision.RequestedAmmo.HasValue)
                {
                    errors.Add($"{baseline.TargetToken}:{decision.Reason}");
                    continue;
                }

                Write(observation, decision.RequestedAmmo.Value, "controlled_mutation_restore_write", countAsObservedWrite: false);
            }
            catch (Exception exception)
            {
                errors.Add($"{baseline.TargetToken}:{exception.Message}");
            }
        }

        RestoreConfirmed = errors.Count == 0;
        PausedReason = null;
        LocalObservedValue = $"status=RESTORED;targets={TrackedTargetCount.ToString(CultureInfo.InvariantCulture)};writes={WriteCount.ToString(CultureInfo.InvariantCulture)}";
        if (errors.Count != 0)
        {
            throw new InvalidOperationException(string.Join(" | ", errors));
        }
    }

    public IReadOnlyList<MutationRuntimeEvent> DrainRuntimeEvents()
    {
        var drained = _runtimeEvents.ToArray();
        _runtimeEvents.Clear();
        return drained;
    }

    private void ValidateTrackedTargets()
    {
        var trackedTargets = new List<AmmoTargetBaseline>(_policy.TrackedTargets);
        foreach (var baseline in trackedTargets)
        {
            var observation = _runtime.CaptureTarget(baseline.TargetToken);
            var decision = _policy.Evaluate(observation);
            if (decision.Action == AmmoMaintenanceAction.NONE)
            {
                continue;
            }

            if (decision.Action == AmmoMaintenanceAction.WRITE && decision.RequestedAmmo.HasValue)
            {
                Write(observation, decision.RequestedAmmo.Value, "controlled_mutation_write", countAsObservedWrite: true);
                continue;
            }

            Fail(observation, decision.Reason);
        }
    }

    private void HandleDecision(AmmoObservation observation, AmmoMaintenanceDecision decision, bool countAsObservedWrite)
    {
        switch (decision.Action)
        {
            case AmmoMaintenanceAction.TRACK:
                _lastPauseKey = null;
                PausedReason = null;
                ActiveTarget = observation.Context;
                LocalObservedValue = FormatStatus("TRACKED", observation, decision.Reason);
                QueueTargetEvent(MutationPhase.TARGET_TRACKED, "controlled_mutation_target_tracked", observation, decision.Reason);
                return;

            case AmmoMaintenanceAction.PAUSE:
                ActiveTarget = observation.Context;
                PausedReason = decision.Reason;
                LocalObservedValue = FormatStatus("PAUSED", observation, decision.Reason);
                var pauseKey = $"{observation.TargetToken}|{decision.Reason}";
                if (!string.Equals(_lastPauseKey, pauseKey, StringComparison.Ordinal))
                {
                    QueueTargetEvent(MutationPhase.TARGET_PAUSED, "controlled_mutation_target_paused", observation, decision.Reason);
                    _lastPauseKey = pauseKey;
                }

                return;

            case AmmoMaintenanceAction.NONE:
                _lastPauseKey = null;
                PausedReason = null;
                ActiveTarget = observation.Context;
                LocalObservedValue = FormatStatus("PROTECTED", observation, decision.Reason);
                return;

            case AmmoMaintenanceAction.WRITE:
                if (!decision.RequestedAmmo.HasValue)
                {
                    Fail(observation, "AMMO_WRITE_VALUE_MISSING");
                }

                _lastPauseKey = null;
                PausedReason = null;
                ActiveTarget = observation.Context;
                Write(observation, decision.RequestedAmmo!.Value, "controlled_mutation_write", countAsObservedWrite);
                return;

            case AmmoMaintenanceAction.FAIL:
                Fail(observation, decision.Reason);
                return;

            default:
                Fail(observation, "UNKNOWN_AMMO_DECISION");
                return;
        }
    }

    private void Write(AmmoObservation observation, int requestedAmmo, string eventName, bool countAsObservedWrite)
    {
        if (string.IsNullOrEmpty(observation.TargetToken))
        {
            Fail(observation, "AMMO_TARGET_TOKEN_MISSING");
        }

        var result = _runtime.WriteAmmo(observation.TargetToken!, observation.CurrentAmmo, requestedAmmo);
        var succeeded = result.Succeeded && result.ObservedAfter == requestedAmmo;
        _runtimeEvents.Add(new MutationRuntimeEvent
        {
            Phase = succeeded ? MutationPhase.WRITE_APPLIED : MutationPhase.WRITE_FAILED,
            Event = eventName,
            OldValue = result.ObservedBefore.ToString(CultureInfo.InvariantCulture),
            NewValue = requestedAmmo.ToString(CultureInfo.InvariantCulture),
            LocalObservedValue = result.ObservedAfter.ToString(CultureInfo.InvariantCulture),
            Context = result.Context ?? observation.Context,
            Error = succeeded ? null : result.Error ?? "AMMO_WRITE_NOT_CONFIRMED"
        });

        if (!succeeded)
        {
            InterferenceDetected = true;
            throw new InvalidOperationException(result.Error ?? "AMMO_WRITE_NOT_CONFIRMED");
        }

        if (countAsObservedWrite)
        {
            RequestedValueObserved = true;
            WriteCount++;
        }

        LocalObservedValue = $"status=PROTECTED;ammo={result.ObservedAfter.ToString(CultureInfo.InvariantCulture)};targets={TrackedTargetCount.ToString(CultureInfo.InvariantCulture)};writes={WriteCount.ToString(CultureInfo.InvariantCulture)}";
    }

    private void Fail(AmmoObservation observation, string reason)
    {
        InterferenceDetected = true;
        LocalObservedValue = FormatStatus("FAILED", observation, reason);
        _runtimeEvents.Add(new MutationRuntimeEvent
        {
            Phase = MutationPhase.WRITE_FAILED,
            Event = "controlled_mutation_failure",
            OldValue = observation.CurrentAmmo.ToString(CultureInfo.InvariantCulture),
            LocalObservedValue = LocalObservedValue,
            Context = observation.Context,
            Error = reason
        });
        throw new InvalidOperationException(reason);
    }

    private void QueueTargetEvent(
        MutationPhase phase,
        string eventName,
        AmmoObservation observation,
        string reason)
    {
        _runtimeEvents.Add(new MutationRuntimeEvent
        {
            Phase = phase,
            Event = eventName,
            OldValue = observation.SelectionKind == AmmoSelectionKind.GUN
                ? observation.CurrentAmmo.ToString(CultureInfo.InvariantCulture)
                : null,
            NewValue = observation.SelectionKind == AmmoSelectionKind.GUN
                ? observation.MaxAmmo.ToString(CultureInfo.InvariantCulture)
                : null,
            LocalObservedValue = reason,
            Context = observation.Context
        });
    }

    private void EnsureBaseline()
    {
        if (!_baselineCaptured)
        {
            throw new InvalidOperationException("Infinite Ammo baseline has not been captured.");
        }
    }

    private string FormatStatus(string status, AmmoObservation observation, string reason)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "status={0};reason={1};ammo={2};maxAmmo={3};targets={4};writes={5}",
            status,
            reason,
            observation.CurrentAmmo,
            observation.MaxAmmo,
            TrackedTargetCount,
            WriteCount);
    }

    private static string FormatValue(AmmoObservation observation)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "weapon={0};slot={1};ammo={2};maxAmmo={3};ammoConsumption={4}",
            observation.WeaponId,
            observation.Slot,
            observation.CurrentAmmo,
            observation.MaxAmmo,
            observation.AmmoConsumption);
    }
}
