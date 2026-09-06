using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class MutationOutcomeClassifierTests
{
    [TestMethod]
    public void Classify_ReturnsLocalOnlyForObservedAndRestoredLocalMutation()
    {
        var outcome = MutationOutcomeClassifier.Classify(
            requestedValueObserved: true,
            interferenceDetected: false,
            restoreRegistered: true,
            SuccessfulReport(RestoreReason.DURATION_ELAPSED),
            RestoreReason.DURATION_ELAPSED,
            error: null);

        Assert.AreEqual(TestOutcome.LOCAL_ONLY, outcome);
    }

    [TestMethod]
    public void Classify_ReturnsInconclusiveForMissingEvidenceInterferenceOrRestoreFailure()
    {
        Assert.AreEqual(
            TestOutcome.INCONCLUSIVE,
            MutationOutcomeClassifier.Classify(false, false, true, SuccessfulReport(RestoreReason.MANUAL), RestoreReason.MANUAL, null));
        Assert.AreEqual(
            TestOutcome.INCONCLUSIVE,
            MutationOutcomeClassifier.Classify(true, true, true, SuccessfulReport(RestoreReason.MANUAL), RestoreReason.MANUAL, null));

        var failedReport = new RestoreReport(
            RestoreReason.MANUAL,
            new[] { new RestoreEntryResult("test", false, "failed") });
        Assert.AreEqual(
            TestOutcome.INCONCLUSIVE,
            MutationOutcomeClassifier.Classify(true, false, true, failedReport, RestoreReason.MANUAL, null));
    }

    [TestMethod]
    public void Classify_ContextAndTargetLossAreAlwaysInconclusive()
    {
        Assert.AreEqual(
            TestOutcome.INCONCLUSIVE,
            MutationOutcomeClassifier.Classify(true, false, true, SuccessfulReport(RestoreReason.CONTEXT_INVALID), RestoreReason.CONTEXT_INVALID, null));
        Assert.AreEqual(
            TestOutcome.INCONCLUSIVE,
            MutationOutcomeClassifier.Classify(true, false, true, SuccessfulReport(RestoreReason.TARGET_CHANGED), RestoreReason.TARGET_CHANGED, null));
    }

    private static RestoreReport SuccessfulReport(RestoreReason reason)
    {
        return new RestoreReport(reason, new[] { new RestoreEntryResult("test", true, null) });
    }
}
