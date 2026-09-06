using System;
using System.Collections.Generic;

namespace ZB2SecurityLab.Core.Experiments;

public sealed class ExperimentCoordinator
{
    private readonly RestoreManager _restoreManager;
    private readonly double _durationSeconds;
    private double _deadlineSeconds;
    private bool _restoreRegistered;

    public ExperimentCoordinator(RestoreManager? restoreManager = null, double durationSeconds = 10d)
    {
        if (durationSeconds <= 0d || double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds))
        {
            throw new ArgumentOutOfRangeException(nameof(durationSeconds));
        }

        _restoreManager = restoreManager ?? new RestoreManager();
        _durationSeconds = durationSeconds;
    }

    public ILabExperiment? ActiveExperiment { get; private set; }

    public MutationResult? LastResult { get; private set; }

    public double RemainingSeconds(double nowSeconds)
    {
        return ActiveExperiment is null ? 0d : Math.Max(0d, _deadlineSeconds - nowSeconds);
    }

    public IReadOnlyList<MutationLifecycleEvent> Start(ILabExperiment experiment, double nowSeconds)
    {
        if (experiment is null)
        {
            throw new ArgumentNullException(nameof(experiment));
        }

        if (ActiveExperiment is not null)
        {
            throw new InvalidOperationException($"Experiment '{ActiveExperiment.Id}' is already active.");
        }

        ActiveExperiment = experiment;
        LastResult = null;
        _deadlineSeconds = nowSeconds + _durationSeconds;
        _restoreRegistered = false;
        var events = new List<MutationLifecycleEvent>();

        try
        {
            experiment.CaptureBaseline();
            events.Add(Event(experiment, MutationPhase.BASELINE_CAPTURED));

            _restoreManager.Register(experiment);
            _restoreRegistered = true;

            try
            {
                experiment.Apply();
                events.Add(Event(experiment, MutationPhase.APPLIED));
            }
            catch (Exception exception)
            {
                events.Add(Event(experiment, MutationPhase.FAILED, error: exception.Message));
                Complete(RestoreReason.APPLY_FAILED, exception.Message, events);
                return events;
            }

            try
            {
                experiment.Observe();
                events.Add(Event(experiment, MutationPhase.OBSERVED));
                events.Add(Event(experiment, MutationPhase.MONITORING));
            }
            catch (Exception exception)
            {
                events.Add(Event(experiment, MutationPhase.FAILED, error: exception.Message));
                Complete(RestoreReason.OBSERVATION_FAILED, exception.Message, events);
            }
        }
        catch (Exception exception)
        {
            events.Add(Event(experiment, MutationPhase.FAILED, error: exception.Message));
            if (_restoreRegistered)
            {
                Complete(RestoreReason.APPLY_FAILED, exception.Message, events);
            }
            else
            {
                LastResult = new MutationResult
                {
                    TestId = experiment.Id,
                    Outcome = Diagnostics.TestOutcome.INCONCLUSIVE,
                    RestoreReason = RestoreReason.APPLY_FAILED,
                    OriginalValue = experiment.OriginalValue,
                    RequestedValue = experiment.RequestedValue,
                    LocalObservedValue = experiment.LocalObservedValue,
                    RestoreSucceeded = false,
                    Error = exception.Message
                };
                events.Add(Event(experiment, MutationPhase.COMPLETED, outcome: LastResult.Outcome, error: exception.Message));
                ActiveExperiment = null;
            }
        }

        return events;
    }

    public IReadOnlyList<MutationLifecycleEvent> Tick(
        double nowSeconds,
        MutationGuardDecision guardDecision,
        string? currentPlayerToken)
    {
        var events = new List<MutationLifecycleEvent>();
        var experiment = ActiveExperiment;
        if (experiment is null)
        {
            return events;
        }

        if (!guardDecision.Allowed)
        {
            Complete(RestoreReason.CONTEXT_INVALID, guardDecision.Reason, events);
            return events;
        }

        if (!string.Equals(experiment.TargetToken, currentPlayerToken, StringComparison.Ordinal))
        {
            Complete(RestoreReason.TARGET_CHANGED, "PLAYER_TOKEN_CHANGED", events);
            return events;
        }

        var interferenceBefore = experiment.InterferenceDetected;
        try
        {
            experiment.Observe();
            if (!interferenceBefore && experiment.InterferenceDetected)
            {
                events.Add(Event(experiment, MutationPhase.INTERFERENCE_DETECTED, error: "MUTATED_VALUE_OVERWRITTEN"));
            }
        }
        catch (Exception exception)
        {
            events.Add(Event(experiment, MutationPhase.FAILED, error: exception.Message));
            Complete(RestoreReason.OBSERVATION_FAILED, exception.Message, events);
            return events;
        }

        if (nowSeconds >= _deadlineSeconds)
        {
            Complete(RestoreReason.DURATION_ELAPSED, null, events);
        }

        return events;
    }

    public IReadOnlyList<MutationLifecycleEvent> Stop(RestoreReason reason, string? error = null)
    {
        var events = new List<MutationLifecycleEvent>();
        if (ActiveExperiment is not null)
        {
            Complete(reason, error, events);
        }

        return events;
    }

    private void Complete(RestoreReason reason, string? error, ICollection<MutationLifecycleEvent> events)
    {
        var experiment = ActiveExperiment;
        if (experiment is null)
        {
            return;
        }

        RestoreReport report;
        try
        {
            events.Add(Event(experiment, MutationPhase.RESTORE_REQUESTED, restoreReason: reason));
            report = _restoreManager.RestoreAll(reason);
            events.Add(Event(
                experiment,
                report.Succeeded ? MutationPhase.RESTORED : MutationPhase.RESTORE_FAILED,
                restoreReason: reason,
                restoreSucceeded: report.Succeeded,
                error: report.ErrorSummary));
        }
        finally
        {
            ActiveExperiment = null;
        }

        var combinedError = CombineErrors(error, report.ErrorSummary);
        var outcome = MutationOutcomeClassifier.Classify(
            experiment.RequestedValueObserved,
            experiment.InterferenceDetected,
            _restoreRegistered,
            report,
            reason,
            combinedError);

        LastResult = new MutationResult
        {
            TestId = experiment.Id,
            Outcome = outcome,
            RestoreReason = reason,
            OriginalValue = experiment.OriginalValue,
            RequestedValue = experiment.RequestedValue,
            LocalObservedValue = experiment.LocalObservedValue,
            RestoreSucceeded = report.Succeeded && experiment.RestoreConfirmed,
            Error = combinedError
        };
        events.Add(Event(
            experiment,
            MutationPhase.COMPLETED,
            restoreReason: reason,
            restoreSucceeded: LastResult.RestoreSucceeded,
            outcome: outcome,
            error: combinedError));
        _restoreRegistered = false;
    }

    private static MutationLifecycleEvent Event(
        ILabExperiment experiment,
        MutationPhase phase,
        RestoreReason? restoreReason = null,
        bool? restoreSucceeded = null,
        Diagnostics.TestOutcome? outcome = null,
        string? error = null)
    {
        return new MutationLifecycleEvent
        {
            TestId = experiment.Id,
            Phase = phase,
            OriginalValue = experiment.OriginalValue,
            RequestedValue = experiment.RequestedValue,
            LocalObservedValue = experiment.LocalObservedValue,
            RestoreReason = restoreReason,
            RestoreSucceeded = restoreSucceeded,
            Outcome = outcome,
            Error = error
        };
    }

    private static string? CombineErrors(string? first, string? second)
    {
        if (first is null)
        {
            return second;
        }

        return second is null ? first : $"{first} | {second}";
    }
}
