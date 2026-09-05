using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        var snapshot = new LabSnapshot
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
