using System;
using System.Collections.Generic;

namespace ZB2SecurityLab.Core.Diagnostics;

public enum LabTransitionKind
{
    CONTEXT_INITIALIZED,
    MATCH_STARTED,
    MATCH_ENDED,
    LOCAL_PLAYER_ACQUIRED,
    LOCAL_PLAYER_LOST,
    LOCAL_PLAYER_REACQUIRED,
    ROLE_CHANGED,
    CONNECTION_CHANGED,
    LOBBY_CHANGED,
    SERVER_CHANGED,
    LOCAL_LOBBY_PLAYER_CHANGED
}

public sealed class LabStateTransition
{
    public LabStateTransition(LabTransitionKind kind, string? previousValue, string? currentValue)
    {
        Kind = kind;
        PreviousValue = previousValue;
        CurrentValue = currentValue;
    }

    public LabTransitionKind Kind { get; }

    public string? PreviousValue { get; }

    public string? CurrentValue { get; }
}

public sealed class LabStateTracker
{
    private LabSnapshot? _previous;

    public IReadOnlyList<LabStateTransition> Observe(LabSnapshot current)
    {
        if (current is null)
        {
            throw new ArgumentNullException(nameof(current));
        }

        var transitions = new List<LabStateTransition>();
        if (_previous is null)
        {
            transitions.Add(new LabStateTransition(LabTransitionKind.CONTEXT_INITIALIZED, null, Describe(current)));
            if (current.LocalPlayerAvailable)
            {
                transitions.Add(new LabStateTransition(LabTransitionKind.LOCAL_PLAYER_ACQUIRED, null, current.LocalPlayerToken));
            }

            _previous = Copy(current);
            return transitions;
        }

        if (_previous.InGame != current.InGame)
        {
            transitions.Add(new LabStateTransition(
                current.InGame ? LabTransitionKind.MATCH_STARTED : LabTransitionKind.MATCH_ENDED,
                _previous.InGame.ToString(),
                current.InGame.ToString()));
        }

        TrackPlayerTransition(_previous, current, transitions);
        AddWhenChanged(transitions, LabTransitionKind.ROLE_CHANGED, _previous.Role, current.Role);
        AddWhenChanged(transitions, LabTransitionKind.CONNECTION_CHANGED, _previous.ConnectionState, current.ConnectionState);
        AddWhenChanged(transitions, LabTransitionKind.LOBBY_CHANGED, _previous.LobbyId, current.LobbyId);
        AddWhenChanged(transitions, LabTransitionKind.SERVER_CHANGED, _previous.ServerSteamId, current.ServerSteamId);
        AddWhenChanged(
            transitions,
            LabTransitionKind.LOCAL_LOBBY_PLAYER_CHANGED,
            Format(_previous.LocalLobbyPlayerId),
            Format(current.LocalLobbyPlayerId));

        _previous = Copy(current);
        return transitions;
    }

    private static void TrackPlayerTransition(
        LabSnapshot previous,
        LabSnapshot current,
        ICollection<LabStateTransition> transitions)
    {
        if (!previous.LocalPlayerAvailable && current.LocalPlayerAvailable)
        {
            transitions.Add(new LabStateTransition(LabTransitionKind.LOCAL_PLAYER_ACQUIRED, null, current.LocalPlayerToken));
            return;
        }

        if (previous.LocalPlayerAvailable && !current.LocalPlayerAvailable)
        {
            transitions.Add(new LabStateTransition(LabTransitionKind.LOCAL_PLAYER_LOST, previous.LocalPlayerToken, null));
            return;
        }

        if (current.LocalPlayerAvailable &&
            !string.Equals(previous.LocalPlayerToken, current.LocalPlayerToken, StringComparison.Ordinal))
        {
            transitions.Add(new LabStateTransition(
                LabTransitionKind.LOCAL_PLAYER_REACQUIRED,
                previous.LocalPlayerToken,
                current.LocalPlayerToken));
        }
    }

    private static void AddWhenChanged(
        ICollection<LabStateTransition> transitions,
        LabTransitionKind kind,
        string? previous,
        string? current)
    {
        if (!string.Equals(previous, current, StringComparison.Ordinal))
        {
            transitions.Add(new LabStateTransition(kind, previous, current));
        }
    }

    private static string Describe(LabSnapshot snapshot)
    {
        return $"inGame={snapshot.InGame};player={snapshot.LocalPlayerAvailable};role={snapshot.Role};connection={snapshot.ConnectionState}";
    }

    private static LabSnapshot Copy(LabSnapshot snapshot)
    {
        return new LabSnapshot
        {
            InGame = snapshot.InGame,
            LocalPlayerAvailable = snapshot.LocalPlayerAvailable,
            HasLocalControl = snapshot.HasLocalControl,
            LocalPlayerToken = snapshot.LocalPlayerToken,
            Role = snapshot.Role,
            ConnectionState = snapshot.ConnectionState,
            LobbyId = snapshot.LobbyId,
            LocalLobbyPlayerId = snapshot.LocalLobbyPlayerId,
            ServerSteamId = snapshot.ServerSteamId,
            LobbyOwnerSteamId = snapshot.LobbyOwnerSteamId,
            LocalSteamId = snapshot.LocalSteamId,
            MultiplayerSession = Copy(snapshot.MultiplayerSession),
            PingMilliseconds = snapshot.PingMilliseconds
        };
    }

    private static MultiplayerSessionSnapshot? Copy(MultiplayerSessionSnapshot? session)
    {
        if (session is null)
        {
            return null;
        }

        return new MultiplayerSessionSnapshot
        {
            Role = session.Role,
            ConnectionState = session.ConnectionState,
            IsMultiplayer = session.IsMultiplayer,
            ServerStarted = session.ServerStarted,
            ServerMultiplayerMode = session.ServerMultiplayerMode,
            ServerSinglePlayerMode = session.ServerSinglePlayerMode,
            ClientConnected = session.ClientConnected,
            ClientMatchmakingConnected = session.ClientMatchmakingConnected,
            ServerLobbyLaunched = session.ServerLobbyLaunched,
            FriendsOnlySignal = session.FriendsOnlySignal,
            ServerConnectionResolved = session.ServerConnectionResolved,
            SteamLobbyId = session.SteamLobbyId,
            ServerSteamId = session.ServerSteamId,
            LobbyOwnerSteamId = session.LobbyOwnerSteamId,
            GameServerSteamId = session.GameServerSteamId,
            ServerConnectionSteamId = session.ServerConnectionSteamId,
            LocalSteamId = session.LocalSteamId,
            LocalLobbyPlayerId = session.LocalLobbyPlayerId,
            LobbyRegion = session.LobbyRegion,
            LobbyVersion = session.LobbyVersion
        };
    }

    private static string? Format(int? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
