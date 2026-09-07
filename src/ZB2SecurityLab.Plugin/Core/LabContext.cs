using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Steamworks;
using Steamworks.Data;
using ZB2SecurityLab.Core.Diagnostics;
using ZB2SecurityLab.Core.Experiments;

namespace ZB2SecurityLab.Plugin.Core;

internal sealed class LabContext
{
    internal LabSnapshot Capture()
    {
        var clientController = ClientController.instance;
        var player = GetLocalPlayer();
        var multiplayerSession = CaptureMultiplayerSession();
        var snapshot = new LabSnapshot
        {
            InGame = MatchController.instance != null && MatchController.InGame,
            LocalPlayerAvailable = player != null,
            HasLocalControl = player != null && player.HasLocalControl,
            LocalPlayerToken = player == null ? null : player.GetInstanceID().ToString(CultureInfo.InvariantCulture),
            Role = multiplayerSession.Role.ToString(),
            ConnectionState = multiplayerSession.ConnectionState,
            LobbyId = multiplayerSession.SteamLobbyId,
            LocalLobbyPlayerId = multiplayerSession.LocalLobbyPlayerId,
            ServerSteamId = multiplayerSession.ServerSteamId,
            LobbyOwnerSteamId = multiplayerSession.LobbyOwnerSteamId,
            LocalSteamId = multiplayerSession.LocalSteamId,
            MultiplayerSession = multiplayerSession,
            PingMilliseconds = clientController == null ? null : clientController.myPingInMS
        };

        if (player == null)
        {
            return snapshot;
        }

        var warnings = new List<string>();
        TryCapture("player", () => snapshot.PlayerState = CapturePlayerState(player), warnings);
        TryCapture("movement", () => snapshot.Movement = CaptureMovement(player.movement), warnings);
        TryCapture("weapon", () => snapshot.Weapon = CaptureWeapon(player), warnings);
        TryCapture("inventory", () => snapshot.Inventory = CaptureInventory(player), warnings);
        TryCapture("progression", () => snapshot.Progression = CaptureProgression(player), warnings);
        TryCapture("currency", () => snapshot.Currency = CaptureCurrency(), warnings);
        snapshot.CaptureWarnings = warnings.ToArray();
        return snapshot;
    }

    private static PlayerStateSnapshot CapturePlayerState(PlayerMain player)
    {
        var position = player.transform.position;
        var rotation = player.transform.eulerAngles;
        return new PlayerStateSnapshot
        {
            HealthFast = player.healthFast,
            HealthSlow = player.healthSlow,
            MaxHealth = player.MaxHealth,
            StaminaFast = player.staminaFast,
            StaminaSlow = player.staminaSlow,
            MaxStamina = player.maxStamina,
            HealthState = player.healthState.ToString(),
            Position = Vector(position),
            Rotation = Vector(rotation)
        };
    }

    private static MovementStateSnapshot? CaptureMovement(PlayerMovement? movement)
    {
        if (movement == null)
        {
            return null;
        }

        return new MovementStateSnapshot
        {
            State = movement.state.ToString(),
            Grounded = movement.touchingGround,
            Sprinting = movement.IsSprinting,
            Velocity = movement.body == null ? null : Vector(movement.body.linearVelocity),
            WalkSpeed = movement.walkSpeed,
            JumpSpeed = movement.jumpSpeed,
            TargetSpeed = Vector(movement.targetSpeed),
            SpeedCoefficient = Vector(movement.speedCoef)
        };
    }

    private static WeaponStateSnapshot? CaptureWeapon(PlayerMain player)
    {
        var arms = player.arms;
        var inventory = player.inventory;
        if (arms == null || inventory == null || !arms.selectedItem.Exists)
        {
            return null;
        }

        var selectedItem = inventory.GetEquipment(arms.selectedItem);
        if (selectedItem == null || selectedItem.IsNone)
        {
            return null;
        }

        var databaseItem = selectedItem.GetDataBaseItem();
        var databaseGun = databaseItem as DatabaseGun;
        var physicalGun = arms.EquippedGun;
        var weapon = new WeaponStateSnapshot
        {
            Name = databaseItem == null ? null : databaseItem.GetName,
            Id = selectedItem.id.ToString(),
            SelectedSlot = EquipmentLabel(arms.selectedItem),
            IsGun = databaseGun != null,
            Ammo = databaseGun == null ? null : selectedItem.ammo
        };

        if (databaseGun == null)
        {
            return weapon;
        }

        weapon.ReserveAmmo = inventory.StoredItemCount(databaseGun.ammoID);
        weapon.MagazineSize = databaseGun.maxAmmo;
        weapon.AmmoConsumption = databaseGun.ammoConsumption;
        weapon.FireRate = databaseGun.rof;
        weapon.FireIntervalSeconds = databaseGun.rof > 0f ? 1f / databaseGun.rof : null;
        weapon.CooldownSeconds = physicalGun == null ? null : physicalGun.Cooldown;
        weapon.Recoil = Vector(databaseGun.recoil);
        weapon.Spread = databaseGun.spread;
        weapon.Damage = databaseGun.dmg;

        var reloadDatabase = ReloadAnimationDatabase.instance;
        if (reloadDatabase != null)
        {
            var reloadAnimation = reloadDatabase.GetAnimation(Perks.ModifyReloadAnimation(databaseGun));
            weapon.ReloadTimeSeconds = reloadAnimation == null ? null : reloadAnimation.reloadTime;
        }

        return weapon;
    }

    private static InventoryStateSnapshot? CaptureInventory(PlayerMain player)
    {
        var inventory = player.inventory;
        if (inventory == null || inventory.storage == null || inventory.equippedItems == null)
        {
            return null;
        }

        var storedItems = new List<string>();
        var storedSignature = new List<string>();
        var totalQuantity = 0;
        foreach (var item in inventory.storage.items)
        {
            if (item == null)
            {
                continue;
            }

            totalQuantity += item.stackCount;
            storedItems.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0} x{1} ammo={2} pos={3},{4} rotated={5}",
                item.id,
                item.stackCount,
                item.ammo,
                item.pos.x,
                item.pos.y,
                item.rotated));
            storedSignature.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}:{2}:{3}:{4}",
                item.id,
                item.stackCount,
                item.pos.x,
                item.pos.y,
                item.rotated));
        }

        var equippedItems = new List<string>();
        var equippedSignature = new List<string>();
        foreach (var entry in inventory.equippedItems.AllItemsIndexed())
        {
            var item = entry.Item1;
            var index = entry.Item2;
            if (item == null || item.IsNone)
            {
                continue;
            }

            totalQuantity += item.stackCount;
            var slot = EquipmentLabel(index);
            equippedItems.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0}={1} x{2} ammo={3}",
                slot,
                item.id,
                item.stackCount,
                item.ammo));
            equippedSignature.Add(string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}:{2}",
                slot,
                item.id,
                item.stackCount));
        }

        storedItems.Sort(StringComparer.Ordinal);
        storedSignature.Sort(StringComparer.Ordinal);
        equippedItems.Sort(StringComparer.Ordinal);
        equippedSignature.Sort(StringComparer.Ordinal);
        var usableSize = inventory.storage.UsableSize;
        return new InventoryStateSnapshot
        {
            SelectedSlot = player.arms == null ? null : EquipmentLabel(player.arms.selectedItem),
            StoredEntryCount = storedItems.Count,
            EquippedEntryCount = equippedItems.Count,
            TotalQuantity = totalQuantity,
            CapacityWidth = usableSize.x,
            CapacityHeight = usableSize.y,
            StoredItems = storedItems.ToArray(),
            EquippedItems = equippedItems.ToArray(),
            Signature = $"stored=[{string.Join("|", storedSignature)}];equipped=[{string.Join("|", equippedSignature)}]"
        };
    }

    private static ProgressionStateSnapshot? CaptureProgression(PlayerMain player)
    {
        var lobbyPlayer = player.lobbyPlayer;
        if (lobbyPlayer == null)
        {
            return null;
        }

        return new ProgressionStateSnapshot
        {
            LoadoutLevel = lobbyPlayer.loadoutLevel,
            Perks = lobbyPlayer.perks == null
                ? Array.Empty<string>()
                : lobbyPlayer.perks.Select(perk => perk.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToArray()
        };
    }

    private static CurrencyStateSnapshot? CaptureCurrency()
    {
        var matchController = MatchController.instance;
        var currency = matchController == null ? null : matchController.currency;
        if (currency == null)
        {
            return null;
        }

        return new CurrencyStateSnapshot
        {
            Dollar = currency.Dollar.amount,
            Silver = currency.Silver.amount,
            Gold = currency.Gold.amount
        };
    }

    private static void TryCapture(string section, Action capture, ICollection<string> warnings)
    {
        try
        {
            capture();
        }
        catch (Exception exception)
        {
            warnings.Add($"{section}:{exception.GetType().Name}");
        }
    }

    private static string EquipmentLabel(EquipmentIndex index)
    {
        return index.Exists
            ? $"{index.SetType}:{index.Value.ToString(CultureInfo.InvariantCulture)}"
            : "NONE";
    }

    private static LabVector2 Vector(UnityEngine.Vector2 value) => new(value.x, value.y);

    private static LabVector3 Vector(UnityEngine.Vector3 value) => new(value.x, value.y, value.z);

    internal PlayerMain? GetLocalPlayer()
    {
        var clientController = ClientController.instance;
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

    internal MutationEligibilityContext CaptureMutationEligibility(
        bool mutationEnabled,
        bool buildSupported,
        bool authorizedMultiplayerEnabled,
        AuthorizedSessionGrant? authorizationGrant,
        string? authorizationGrantError,
        string buildId)
    {
        var player = GetLocalPlayer();
        var multiplayerSession = CaptureMultiplayerSession();
        return new MutationEligibilityContext
        {
            MutationEnabled = mutationEnabled,
            BuildSupported = buildSupported,
            InGame = MatchController.instance != null && MatchController.InGame,
            LocalPlayerAvailable = player != null,
            HasLocalControl = player != null && player.HasLocalControl,
            Role = multiplayerSession.Role.ToString(),
            PlayerToken = player == null ? null : player.GetInstanceID().ToString(CultureInfo.InvariantCulture),
            BuildId = buildId,
            ObservedAtUtc = DateTimeOffset.UtcNow,
            AuthorizedMultiplayerEnabled = authorizedMultiplayerEnabled,
            AuthorizationGrant = authorizationGrant,
            AuthorizationGrantError = authorizationGrantError,
            MultiplayerSession = multiplayerSession
        };
    }

    private static MultiplayerSessionSnapshot CaptureMultiplayerSession()
    {
        var controller = MultiplayerController.instance;
        var client = ClientController.instance;
        var server = ServerController.instance;
        var connections = SteamConnectionsController.instance;
        var steam = SteamController.instance;
        if (controller == null)
        {
            return new MultiplayerSessionSnapshot();
        }

        var serverStarted = server != null && server.state == ServerController.State.Started;
        var serverSinglePlayer = server != null && server.mode == ServerController.Mode.Singleplayer;
        var serverMultiplayer = server != null && server.mode == ServerController.Mode.Multiplayer;
        var clientConnected = client != null && client.state == ClientController.State.Connected;
        var role = NetworkRole.OFFLINE;
        if (server != null && server.state != ServerController.State.Off && serverSinglePlayer)
        {
            role = NetworkRole.SINGLE_PLAYER;
        }
        else if (server != null && server.state != ServerController.State.Off && serverMultiplayer)
        {
            role = NetworkRole.HOST;
        }
        else if (client != null && client.state != ClientController.State.Off)
        {
            role = NetworkRole.CLIENT;
        }

        var result = new MultiplayerSessionSnapshot
        {
            Role = role,
            ConnectionState = role == NetworkRole.CLIENT
                ? $"CLIENT:{client?.state.ToString() ?? "UNAVAILABLE"}"
                : $"SERVER:{server?.state.ToString() ?? "UNAVAILABLE"}/{server?.mode.ToString() ?? "UNAVAILABLE"}",
            IsMultiplayer = role == NetworkRole.CLIENT || role == NetworkRole.HOST,
            ServerStarted = serverStarted,
            ServerMultiplayerMode = serverMultiplayer,
            ServerSinglePlayerMode = serverSinglePlayer,
            ClientConnected = clientConnected,
            ClientMatchmakingConnected = client != null && client.Matchmaking != null && client.Matchmaking.IsConnected,
            ServerLobbyLaunched = server != null && server.Matchmaking != null && server.Matchmaking.LobbyLaunched,
            LocalSteamId = steam == null ? null : ValidSteamId(steam.MySteamID),
            LocalLobbyPlayerId = controller.KnowMyLobbyID() ? controller.GetMyLobbyID() : null
        };

        var hasLobby = false;
        Lobby lobby = default;
        var serverMatchmaking = server?.Matchmaking;
        var clientMatchmaking = client?.Matchmaking;
        if (role == NetworkRole.HOST && result.ServerLobbyLaunched && serverMatchmaking != null)
        {
            lobby = serverMatchmaking.CurrentLobby;
            hasLobby = lobby.Id.IsValid;
        }
        else if (role == NetworkRole.CLIENT && result.ClientMatchmakingConnected && clientMatchmaking != null)
        {
            lobby = clientMatchmaking.ConnectedLobby;
            hasLobby = lobby.Id.IsValid;
        }

        if (hasLobby)
        {
            result.SteamLobbyId = ValidSteamId(lobby.Id);
            result.LobbyOwnerSteamId = ValidSteamId(lobby.Owner.Id);
            result.LobbyRegion = lobby.GetData("region");
            result.LobbyVersion = lobby.GetData("version");
            uint gameServerIp = 0;
            ushort gameServerPort = 0;
            SteamId gameServerId = default;
            if (lobby.GetGameServer(ref gameServerIp, ref gameServerPort, ref gameServerId))
            {
                result.GameServerSteamId = ValidSteamId(gameServerId);
            }
        }

        if (role == NetworkRole.CLIENT && connections != null)
        {
            result.ServerSteamId = ValidSteamId(connections.ServerID);
            try
            {
                var serverConnection = connections.GetServerConnection();
                result.ServerConnectionResolved = serverConnection != null;
                result.ServerConnectionSteamId = serverConnection == null
                    ? null
                    : ValidSteamId(serverConnection.SteamID);
            }
            catch
            {
                result.ServerConnectionResolved = false;
                result.ServerConnectionSteamId = null;
            }
        }
        else if (role == NetworkRole.HOST)
        {
            result.ServerSteamId = result.GameServerSteamId;
        }

        result.FriendsOnlySignal = role == NetworkRole.HOST
            ? server?.FriendsOnly == true
            : role == NetworkRole.CLIENT && string.Equals(result.LobbyRegion, WorldRegion.Friends.ToString(), StringComparison.Ordinal);
        return result;
    }

    private static string? ValidSteamId(SteamId id) => id.IsValid ? id.ToString() : null;
}
