using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZB2SecurityLab.Core.Diagnostics;

namespace ZB2SecurityLab.Core.Tests;

[TestClass]
public sealed class MultiplayerSessionSnapshotTests
{
    [TestMethod]
    public void CreateToken_BindsBuildRoleSessionAccountLobbyPlayerAndPlayer()
    {
        var baseline = Session();
        var token = baseline.CreateToken("24525702", "player-1");

        Assert.AreNotEqual(token, Session(role: NetworkRole.HOST).CreateToken("24525702", "player-1"));
        Assert.AreNotEqual(token, Session(lobbyId: "109775241012345679").CreateToken("24525702", "player-1"));
        Assert.AreNotEqual(token, Session(serverId: "76561198000000003").CreateToken("24525702", "player-1"));
        Assert.AreNotEqual(token, Session(localSteamId: "76561198000000004").CreateToken("24525702", "player-1"));
        Assert.AreNotEqual(token, Session(localLobbyPlayerId: 2).CreateToken("24525702", "player-1"));
        Assert.AreNotEqual(token, baseline.CreateToken("24525703", "player-1"));
        Assert.AreNotEqual(token, baseline.CreateToken("24525702", "player-2"));
    }

    private static MultiplayerSessionSnapshot Session(
        NetworkRole role = NetworkRole.CLIENT,
        string lobbyId = "109775241012345678",
        string serverId = "76561198000000001",
        string localSteamId = "76561198000000002",
        int localLobbyPlayerId = 1)
    {
        return new MultiplayerSessionSnapshot
        {
            Role = role,
            SteamLobbyId = lobbyId,
            ServerSteamId = serverId,
            LobbyOwnerSteamId = "76561198000000001",
            LocalSteamId = localSteamId,
            LocalLobbyPlayerId = localLobbyPlayerId
        };
    }
}
