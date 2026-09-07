using ZB2SecurityLab.Core.Diagnostics;

namespace ZB2SecurityLab.Core.Experiments;

public static class MutationOutcomeClassifier
{
    public static TestOutcome Classify(
        bool requestedValueObserved,
        bool interferenceDetected,
        bool restoreRegistered,
        RestoreReport restoreReport,
        RestoreReason reason,
        string? error)
    {
        return Classify(
            requestedValueObserved,
            interferenceDetected,
            restoreRegistered,
            restoreReport,
            reason,
            error,
            MutationExecutionScope.SINGLE_PLAYER,
            ServerEvidenceKind.NOT_APPLICABLE);
    }

    public static TestOutcome Classify(
        bool requestedValueObserved,
        bool interferenceDetected,
        bool restoreRegistered,
        RestoreReport restoreReport,
        RestoreReason reason,
        string? error,
        MutationExecutionScope executionScope,
        ServerEvidenceKind serverEvidence)
    {
        if (!requestedValueObserved ||
            !restoreRegistered ||
            !restoreReport.Succeeded ||
            error is not null ||
            reason == RestoreReason.CONTEXT_INVALID ||
            reason == RestoreReason.TARGET_CHANGED ||
            reason == RestoreReason.SESSION_CHANGED ||
            reason == RestoreReason.APPLY_FAILED ||
            reason == RestoreReason.OBSERVATION_FAILED)
        {
            return TestOutcome.INCONCLUSIVE;
        }

        if (serverEvidence == ServerEvidenceKind.CORRECTED)
        {
            return TestOutcome.SERVER_CORRECTED;
        }

        if (interferenceDetected || serverEvidence == ServerEvidenceKind.CONFLICTING)
        {
            return TestOutcome.INCONCLUSIVE;
        }

        if (serverEvidence == ServerEvidenceKind.ACCEPTED &&
            executionScope != MutationExecutionScope.SINGLE_PLAYER)
        {
            return TestOutcome.SERVER_ACCEPTED;
        }

        return TestOutcome.LOCAL_ONLY;
    }
}
