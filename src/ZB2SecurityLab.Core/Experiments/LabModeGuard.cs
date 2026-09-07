using System;

namespace ZB2SecurityLab.Core.Experiments;

public sealed class LabModeGuard
{
    private readonly AuthorizedSessionPolicy _authorizedSessionPolicy;

    public LabModeGuard(AuthorizedSessionPolicy? authorizedSessionPolicy = null)
    {
        _authorizedSessionPolicy = authorizedSessionPolicy ?? new AuthorizedSessionPolicy();
    }

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

        if (string.IsNullOrWhiteSpace(context.BuildId))
        {
            return new MutationGuardDecision(false, "BUILD_ID_UNAVAILABLE");
        }

        if (context.ObservedAtUtc == default)
        {
            return new MutationGuardDecision(false, "OBSERVATION_TIME_UNAVAILABLE");
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

        var session = context.MultiplayerSession;
        if (session is null)
        {
            return new MutationGuardDecision(false, "SESSION_CONTEXT_UNAVAILABLE");
        }

        if (session.Role == Diagnostics.NetworkRole.SINGLE_PLAYER)
        {
            if (!session.ServerStarted || !session.ServerSinglePlayerMode || session.ClientConnected)
            {
                return new MutationGuardDecision(false, "SINGLE_PLAYER_CONTEXT_INVALID");
            }

            return Allowed(
                context,
                MutationExecutionScope.SINGLE_PLAYER,
                "ALLOWED_SINGLE_PLAYER",
                authorizationId: null);
        }

        if (session.Role != Diagnostics.NetworkRole.CLIENT && session.Role != Diagnostics.NetworkRole.HOST)
        {
            return new MutationGuardDecision(false, "NOT_ELIGIBLE_ROLE");
        }

        if (!context.AuthorizedMultiplayerEnabled)
        {
            return new MutationGuardDecision(false, "AUTHORIZED_MULTIPLAYER_DISABLED");
        }

        if (!string.IsNullOrEmpty(context.AuthorizationGrantError))
        {
            return new MutationGuardDecision(false, "AUTHORIZATION_GRANT_INVALID");
        }

        var authorization = _authorizedSessionPolicy.Evaluate(
            session,
            context.AuthorizationGrant,
            context.BuildId,
            context.ObservedAtUtc);
        if (!authorization.Allowed)
        {
            return new MutationGuardDecision(false, authorization.Reason);
        }

        var scope = session.Role == Diagnostics.NetworkRole.CLIENT
            ? MutationExecutionScope.AUTHORIZED_MULTIPLAYER_CLIENT
            : MutationExecutionScope.AUTHORIZED_MULTIPLAYER_HOST;
        return Allowed(context, scope, authorization.Reason, authorization.AuthorizationId);
    }

    private static MutationGuardDecision Allowed(
        MutationEligibilityContext context,
        MutationExecutionScope scope,
        string reason,
        string? authorizationId)
    {
        var sessionToken = context.MultiplayerSession!.CreateToken(context.BuildId, context.PlayerToken!);
        return new MutationGuardDecision(true, reason, scope, sessionToken, authorizationId);
    }
}
