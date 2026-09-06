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
        if (!requestedValueObserved ||
            interferenceDetected ||
            !restoreRegistered ||
            !restoreReport.Succeeded ||
            error is not null ||
            reason == RestoreReason.CONTEXT_INVALID ||
            reason == RestoreReason.TARGET_CHANGED ||
            reason == RestoreReason.APPLY_FAILED ||
            reason == RestoreReason.OBSERVATION_FAILED)
        {
            return TestOutcome.INCONCLUSIVE;
        }

        return TestOutcome.LOCAL_ONLY;
    }
}
