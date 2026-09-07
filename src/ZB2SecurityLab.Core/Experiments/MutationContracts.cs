using System;
using System.Collections.Generic;

namespace ZB2SecurityLab.Core.Experiments;

public enum MutationPhase
{
    REQUESTED,
    ELIGIBILITY_CHECKED,
    BLOCKED,
    BASELINE_CAPTURED,
    APPLIED,
    OBSERVED,
    MONITORING,
    TARGET_TRACKED,
    TARGET_PAUSED,
    WRITE_APPLIED,
    WRITE_FAILED,
    INTERFERENCE_DETECTED,
    RESTORE_REQUESTED,
    RESTORED,
    RESTORE_FAILED,
    FAILED,
    COMPLETED
}

public enum RestoreReason
{
    DURATION_ELAPSED,
    MANUAL,
    PANEL_CLOSED,
    CONTEXT_INVALID,
    TARGET_CHANGED,
    APPLY_FAILED,
    OBSERVATION_FAILED,
    SESSION_CHANGED,
    PLUGIN_DISABLED,
    PLUGIN_DESTROYED
}

public enum MutationExecutionScope
{
    NOT_ELIGIBLE,
    SINGLE_PLAYER,
    AUTHORIZED_MULTIPLAYER_CLIENT,
    AUTHORIZED_MULTIPLAYER_HOST
}

public enum ServerEvidenceKind
{
    NOT_APPLICABLE,
    NOT_OBSERVED,
    ACCEPTED,
    CORRECTED,
    CONFLICTING
}

public sealed class MutationEligibilityContext
{
    public bool MutationEnabled { get; set; }

    public bool BuildSupported { get; set; }

    public bool InGame { get; set; }

    public bool LocalPlayerAvailable { get; set; }

    public bool HasLocalControl { get; set; }

    public string Role { get; set; } = string.Empty;

    public string? PlayerToken { get; set; }

    public string BuildId { get; set; } = string.Empty;

    public DateTimeOffset ObservedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool AuthorizedMultiplayerEnabled { get; set; }

    public string? AuthorizationGrantError { get; set; }

    public AuthorizedSessionGrant? AuthorizationGrant { get; set; }

    public Diagnostics.MultiplayerSessionSnapshot? MultiplayerSession { get; set; }
}

public sealed class MutationGuardDecision
{
    public MutationGuardDecision(
        bool allowed,
        string reason,
        MutationExecutionScope scope = MutationExecutionScope.NOT_ELIGIBLE,
        string? sessionToken = null,
        string? authorizationId = null)
    {
        Allowed = allowed;
        Reason = reason ?? throw new ArgumentNullException(nameof(reason));
        Scope = scope;
        SessionToken = sessionToken;
        AuthorizationId = authorizationId;
    }

    public bool Allowed { get; }

    public string Reason { get; }

    public MutationExecutionScope Scope { get; }

    public string? SessionToken { get; }

    public string? AuthorizationId { get; }
}

public sealed class MutationLifecycleEvent
{
    public string TestId { get; set; } = string.Empty;

    public MutationPhase Phase { get; set; }

    public string? Event { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? Context { get; set; }

    public MutationExecutionScope? ExecutionScope { get; set; }

    public string? SessionToken { get; set; }

    public string? AuthorizationId { get; set; }

    public string? ExperimentRunId { get; set; }

    public ServerEvidenceKind? ServerEvidence { get; set; }

    public string? EvidenceSource { get; set; }

    public string? OriginalValue { get; set; }

    public string? RequestedValue { get; set; }

    public string? LocalObservedValue { get; set; }

    public string? RemoteObservedValue { get; set; }

    public RestoreReason? RestoreReason { get; set; }

    public bool? RestoreSucceeded { get; set; }

    public Diagnostics.TestOutcome? Outcome { get; set; }

    public string? Error { get; set; }
}

public sealed class MutationRuntimeEvent
{
    public MutationPhase Phase { get; set; }

    public string Event { get; set; } = string.Empty;

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? LocalObservedValue { get; set; }

    public string? Context { get; set; }

    public string? Error { get; set; }
}

public interface IRuntimeMutationEventSource
{
    IReadOnlyList<MutationRuntimeEvent> DrainRuntimeEvents();
}

public sealed class RestoreEntryResult
{
    public RestoreEntryResult(string experimentId, bool succeeded, string? error)
    {
        ExperimentId = experimentId;
        Succeeded = succeeded;
        Error = error;
    }

    public string ExperimentId { get; }

    public bool Succeeded { get; }

    public string? Error { get; }
}

public sealed class RestoreReport
{
    public RestoreReport(RestoreReason reason, IReadOnlyList<RestoreEntryResult> entries)
    {
        Reason = reason;
        Entries = entries ?? throw new ArgumentNullException(nameof(entries));
    }

    public RestoreReason Reason { get; }

    public IReadOnlyList<RestoreEntryResult> Entries { get; }

    public bool Succeeded
    {
        get
        {
            foreach (var entry in Entries)
            {
                if (!entry.Succeeded)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public string? ErrorSummary
    {
        get
        {
            var errors = new List<string>();
            foreach (var entry in Entries)
            {
                if (!entry.Succeeded && entry.Error is not null)
                {
                    errors.Add($"{entry.ExperimentId}:{entry.Error}");
                }
            }

            return errors.Count == 0 ? null : string.Join(" | ", errors);
        }
    }
}

public sealed class MutationResult
{
    public string TestId { get; set; } = string.Empty;

    public Diagnostics.TestOutcome Outcome { get; set; }

    public MutationExecutionScope ExecutionScope { get; set; }

    public string? SessionToken { get; set; }

    public string? AuthorizationId { get; set; }

    public string? ExperimentRunId { get; set; }

    public ServerEvidenceKind ServerEvidence { get; set; }

    public string? RemoteObservedValue { get; set; }

    public string? EvidenceSource { get; set; }

    public RestoreReason RestoreReason { get; set; }

    public string? OriginalValue { get; set; }

    public string? RequestedValue { get; set; }

    public string? LocalObservedValue { get; set; }

    public bool RestoreSucceeded { get; set; }

    public string? Error { get; set; }
}

public sealed class MutationPanelState
{
    public bool MutationsEnabled { get; set; }

    public bool AuthorizedMultiplayerEnabled { get; set; }

    public bool Eligible { get; set; }

    public string EligibilityReason { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? ActiveTestId { get; set; }

    public double RemainingSeconds { get; set; }

    public string? OriginalValue { get; set; }

    public string? RequestedValue { get; set; }

    public string? LocalObservedValue { get; set; }

    public MutationResult? LastResult { get; set; }

    public bool AmmoEligible { get; set; }

    public string AmmoEligibilityReason { get; set; } = string.Empty;

    public string? ActiveTarget { get; set; }

    public int? TrackedTargetCount { get; set; }

    public int? WriteCount { get; set; }

    public string? PausedReason { get; set; }

    public MutationExecutionScope ExecutionScope { get; set; }

    public string? AuthorizationId { get; set; }

    public string? SteamLobbyId { get; set; }

    public string? ServerSteamId { get; set; }
}
