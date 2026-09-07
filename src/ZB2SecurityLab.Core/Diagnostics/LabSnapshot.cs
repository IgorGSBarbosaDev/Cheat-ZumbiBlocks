namespace ZB2SecurityLab.Core.Diagnostics;

public sealed class LabSnapshot
{
    public bool InGame { get; set; }

    public bool LocalPlayerAvailable { get; set; }

    public bool HasLocalControl { get; set; }

    public string? LocalPlayerToken { get; set; }

    public string Role { get; set; } = "UNKNOWN";

    public string ConnectionState { get; set; } = "UNKNOWN";

    public string? LobbyId { get; set; }

    public int? LocalLobbyPlayerId { get; set; }

    public string? ServerSteamId { get; set; }

    public string? LobbyOwnerSteamId { get; set; }

    public string? LocalSteamId { get; set; }

    public MultiplayerSessionSnapshot? MultiplayerSession { get; set; }

    public int? PingMilliseconds { get; set; }

    public PlayerStateSnapshot? PlayerState { get; set; }

    public MovementStateSnapshot? Movement { get; set; }

    public WeaponStateSnapshot? Weapon { get; set; }

    public InventoryStateSnapshot? Inventory { get; set; }

    public ProgressionStateSnapshot? Progression { get; set; }

    public CurrencyStateSnapshot? Currency { get; set; }

    public string[] CaptureWarnings { get; set; } = System.Array.Empty<string>();
}

public sealed class PlayerStateSnapshot
{
    public float HealthFast { get; set; }

    public float HealthSlow { get; set; }

    public float MaxHealth { get; set; }

    public float StaminaFast { get; set; }

    public float StaminaSlow { get; set; }

    public float MaxStamina { get; set; }

    public string HealthState { get; set; } = "UNKNOWN";

    public LabVector3? Position { get; set; }

    public LabVector3? Rotation { get; set; }
}

public sealed class MovementStateSnapshot
{
    public string State { get; set; } = "UNKNOWN";

    public bool Grounded { get; set; }

    public bool Sprinting { get; set; }

    public LabVector3? Velocity { get; set; }

    public float WalkSpeed { get; set; }

    public float JumpSpeed { get; set; }

    public LabVector2? TargetSpeed { get; set; }

    public LabVector2? SpeedCoefficient { get; set; }
}

public sealed class WeaponStateSnapshot
{
    public string? Name { get; set; }

    public string? Id { get; set; }

    public string? SelectedSlot { get; set; }

    public bool IsGun { get; set; }

    public int? Ammo { get; set; }

    public int? ReserveAmmo { get; set; }

    public int? MagazineSize { get; set; }

    public int? AmmoConsumption { get; set; }

    public float? FireRate { get; set; }

    public float? FireIntervalSeconds { get; set; }

    public float? CooldownSeconds { get; set; }

    public LabVector2? Recoil { get; set; }

    public float? Spread { get; set; }

    public float? Damage { get; set; }

    public float? ReloadTimeSeconds { get; set; }
}

public sealed class InventoryStateSnapshot
{
    public string? SelectedSlot { get; set; }

    public int StoredEntryCount { get; set; }

    public int EquippedEntryCount { get; set; }

    public int TotalQuantity { get; set; }

    public int CapacityWidth { get; set; }

    public int CapacityHeight { get; set; }

    public string[] StoredItems { get; set; } = System.Array.Empty<string>();

    public string[] EquippedItems { get; set; } = System.Array.Empty<string>();

    public string Signature { get; set; } = string.Empty;
}

public sealed class ProgressionStateSnapshot
{
    public int LoadoutLevel { get; set; }

    public string[] Perks { get; set; } = System.Array.Empty<string>();
}

public sealed class CurrencyStateSnapshot
{
    public int Dollar { get; set; }

    public int Silver { get; set; }

    public int Gold { get; set; }
}

public sealed class LabVector2
{
    public LabVector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { get; }

    public float Y { get; }
}

public sealed class LabVector3
{
    public LabVector3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public float X { get; }

    public float Y { get; }

    public float Z { get; }
}
