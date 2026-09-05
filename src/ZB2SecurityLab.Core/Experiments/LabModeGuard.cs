using System;
using System.Collections.Generic;

namespace ZB2SecurityLab.Core.Experiments;

public sealed class LabModeGuard
{
    private readonly HashSet<string> _allowedLobbyIds;

    public LabModeGuard(bool mutationEnabled, bool allowSinglePlayer, IEnumerable<string>? allowedLobbyIds = null)
    {
        MutationEnabled = mutationEnabled;
        AllowSinglePlayer = allowSinglePlayer;
        _allowedLobbyIds = new HashSet<string>(allowedLobbyIds ?? Array.Empty<string>(), StringComparer.Ordinal);
    }

    public bool MutationEnabled { get; }

    public bool AllowSinglePlayer { get; }

    public bool CanMutate(bool isSinglePlayer, string? currentLobbyId)
    {
        if (!MutationEnabled)
        {
            return false;
        }

        if (isSinglePlayer)
        {
            return AllowSinglePlayer;
        }

        return currentLobbyId is not null && _allowedLobbyIds.Contains(currentLobbyId);
    }
}

