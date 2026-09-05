using System.Globalization;
using ZB2SecurityLab.Core.Diagnostics;

namespace ZB2SecurityLab.Plugin.Core;

internal sealed class LabContext
{
    internal LabSnapshot Capture()
    {
        var multiplayerController = MultiplayerController.instance;
        var clientController = ClientController.instance;
        var serverController = ServerController.instance;
        var player = FindLocalPlayer(clientController);

        return new LabSnapshot
        {
            InGame = MatchController.instance != null && MatchController.InGame,
            LocalPlayerAvailable = player != null,
            HasLocalControl = player != null && player.HasLocalControl,
            LocalPlayerToken = player == null ? null : player.GetInstanceID().ToString(CultureInfo.InvariantCulture),
            Role = ResolveRole(multiplayerController),
            ConnectionState = ResolveConnectionState(clientController, serverController),
            LobbyId = ResolveLobbyId(multiplayerController),
            PingMilliseconds = clientController == null ? null : clientController.myPingInMS
        };
    }

    private static PlayerMain? FindLocalPlayer(ClientController? clientController)
    {
        PlayerMain? player = null;
        if (clientController != null)
        {
            player = clientController.GetMyPlayer();
        }

        if (player == null && PlayersController.instance != null)
        {
            player = PlayersController.instance.MyPlayer();
        }

        return player;
    }

    private static string ResolveRole(MultiplayerController? controller)
    {
        if (controller == null)
        {
            return "UNKNOWN";
        }

        if (controller.IsSinglePlayer)
        {
            return "SINGLE_PLAYER";
        }

        if (controller.IsServer())
        {
            return "HOST";
        }

        if (controller.IsClient())
        {
            return "CLIENT";
        }

        return "OFFLINE";
    }

    private static string ResolveConnectionState(ClientController? client, ServerController? server)
    {
        if (client != null)
        {
            return $"CLIENT:{client.state}";
        }

        return server == null ? "UNKNOWN" : $"SERVER:{server.state}";
    }

    private static string? ResolveLobbyId(MultiplayerController? controller)
    {
        if (controller == null || !controller.KnowMyLobbyID())
        {
            return null;
        }

        return controller.GetMyLobbyID().ToString(CultureInfo.InvariantCulture);
    }
}
