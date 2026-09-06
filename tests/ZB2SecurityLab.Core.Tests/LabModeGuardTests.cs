using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class LabModeGuardTests
{
    [TestMethod]
    public void Evaluate_FailsClosedWithDeterministicReasons()
    {
        var guard = new LabModeGuard();

        AssertDecision(guard, Context(mutationEnabled: false), false, "MUTATIONS_DISABLED");
        AssertDecision(guard, Context(buildSupported: false), false, "UNSUPPORTED_BUILD");
        AssertDecision(guard, Context(inGame: false), false, "NOT_IN_GAME");
        AssertDecision(guard, Context(localPlayerAvailable: false, playerToken: null), false, "LOCAL_PLAYER_UNAVAILABLE");
        AssertDecision(guard, Context(hasLocalControl: false), false, "LOCAL_CONTROL_REQUIRED");
        AssertDecision(guard, Context(role: "HOST"), false, "SINGLE_PLAYER_ONLY");
        AssertDecision(guard, Context(role: "CLIENT"), false, "SINGLE_PLAYER_ONLY");
    }

    [TestMethod]
    public void Evaluate_AllowsOnlyCompleteSinglePlayerContext()
    {
        var decision = new LabModeGuard().Evaluate(Context());

        Assert.IsTrue(decision.Allowed);
        Assert.AreEqual("ALLOWED_SINGLE_PLAYER", decision.Reason);
    }

    private static MutationEligibilityContext Context(
        bool mutationEnabled = true,
        bool buildSupported = true,
        bool inGame = true,
        bool localPlayerAvailable = true,
        bool hasLocalControl = true,
        string role = "SINGLE_PLAYER",
        string? playerToken = "player")
    {
        return new MutationEligibilityContext
        {
            MutationEnabled = mutationEnabled,
            BuildSupported = buildSupported,
            InGame = inGame,
            LocalPlayerAvailable = localPlayerAvailable,
            HasLocalControl = hasLocalControl,
            Role = role,
            PlayerToken = playerToken
        };
    }

    private static void AssertDecision(
        LabModeGuard guard,
        MutationEligibilityContext context,
        bool allowed,
        string reason)
    {
        var decision = guard.Evaluate(context);
        Assert.AreEqual(allowed, decision.Allowed);
        Assert.AreEqual(reason, decision.Reason);
    }
}
