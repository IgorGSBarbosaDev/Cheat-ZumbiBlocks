using System;

namespace ZB2SecurityLab.Core.Experiments;

public sealed class LabModeGuard
{
    public MutationGuardDecision Evaluate(MutationEligibilityContext context)
    {
        if (!context.MutationEnabled)
        {
            return new MutationGuardDecision(false, "MUTATIONS_DISABLED");
        }

        if (!context.BuildSupported)
        {
            return new MutationGuardDecision(false, "UNSUPPORTED_BUILD");
        }

        if (!context.InGame)
        {
            return new MutationGuardDecision(false, "NOT_IN_GAME");
        }

        if (!context.LocalPlayerAvailable || string.IsNullOrEmpty(context.PlayerToken))
        {
            return new MutationGuardDecision(false, "LOCAL_PLAYER_UNAVAILABLE");
        }

        if (!context.HasLocalControl)
        {
            return new MutationGuardDecision(false, "LOCAL_CONTROL_REQUIRED");
        }

        if (!string.Equals(context.Role, "SINGLE_PLAYER", StringComparison.Ordinal))
        {
            return new MutationGuardDecision(false, "SINGLE_PLAYER_ONLY");
        }

        return new MutationGuardDecision(true, "ALLOWED_SINGLE_PLAYER");
    }
}
