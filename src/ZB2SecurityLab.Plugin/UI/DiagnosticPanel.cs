using System.Globalization;
using UnityEngine;
using ZB2SecurityLab.Core.Diagnostics;

namespace ZB2SecurityLab.Plugin.UI;

internal sealed class DiagnosticPanel
{
    private Vector2 _scrollPosition;

    internal bool Visible { get; set; }

    internal void Draw(bool buildSupported, string buildStatus, LabSnapshot? snapshot, string sessionId)
    {
        if (!Visible)
        {
            return;
        }

        var width = Mathf.Max(320f, Mathf.Min(480f, Screen.width - 40f));
        var height = Mathf.Max(250f, Mathf.Min(720f, Screen.height - 40f));
        var panelRect = new Rect(20f, 20f, width, height);
        GUI.Box(panelRect, "");
        GUILayout.BeginArea(new Rect(panelRect.x + 12f, panelRect.y + 10f, panelRect.width - 24f, panelRect.height - 20f));
        _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
        GUILayout.Label("ZB2 SECURITY LAB — READ ONLY");
        GUILayout.Space(6f);
        GUILayout.Label($"Build: {(buildSupported ? "SUPPORTED" : "BLOCKED")}");
        GUILayout.Label(buildStatus);
        GUILayout.Label($"Session: {ShortSession(sessionId)}");

        if (snapshot is null)
        {
            GUILayout.Space(8f);
            GUILayout.Label("Game context: unavailable");
        }
        else
        {
            DrawPlayer(snapshot.PlayerState);
            DrawMovement(snapshot.Movement);
            DrawWeapon(snapshot.Weapon);
            DrawInventory(snapshot.Inventory, snapshot.Currency);
            DrawProgression(snapshot.Progression);
            DrawNetwork(snapshot);
            DrawWarnings(snapshot);
        }

        GUILayout.Space(8f);
        GUILayout.Label("F8 closes this diagnostic panel.");
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private static void DrawPlayer(PlayerStateSnapshot? player)
    {
        Section("PLAYER STATE");
        if (player is null)
        {
            GUILayout.Label("Unavailable");
            return;
        }

        GUILayout.Label($"Health: {Number(player.HealthFast)} fast / {Number(player.HealthSlow)} slow / {Number(player.MaxHealth)} max");
        GUILayout.Label($"Health state: {player.HealthState}");
        GUILayout.Label($"Stamina: {Number(player.StaminaFast)} fast / {Number(player.StaminaSlow)} slow / {Number(player.MaxStamina)} max");
        GUILayout.Label($"Position: {Vector(player.Position)}");
        GUILayout.Label($"Rotation: {Vector(player.Rotation)}");
    }

    private static void DrawMovement(MovementStateSnapshot? movement)
    {
        Section("MOVEMENT");
        if (movement is null)
        {
            GUILayout.Label("Unavailable");
            return;
        }

        GUILayout.Label($"State: {movement.State}");
        GUILayout.Label($"Grounded: {YesNo(movement.Grounded)}");
        GUILayout.Label($"Sprinting: {YesNo(movement.Sprinting)}");
        GUILayout.Label($"Velocity: {Vector(movement.Velocity)}");
        GUILayout.Label($"Walk speed: {Number(movement.WalkSpeed)}");
        GUILayout.Label($"Jump speed: {Number(movement.JumpSpeed)}");
        GUILayout.Label($"Target speed: {Vector(movement.TargetSpeed)}");
        GUILayout.Label($"Speed coefficient: {Vector(movement.SpeedCoefficient)}");
    }

    private static void DrawWeapon(WeaponStateSnapshot? weapon)
    {
        Section("WEAPON");
        if (weapon is null)
        {
            GUILayout.Label("No selected item");
            return;
        }

        GUILayout.Label($"Name: {weapon.Name ?? "UNKNOWN"}");
        GUILayout.Label($"ID: {weapon.Id ?? "UNKNOWN"}");
        GUILayout.Label($"Selected slot: {weapon.SelectedSlot ?? "UNKNOWN"}");
        if (!weapon.IsGun)
        {
            GUILayout.Label("Gun data: not applicable");
            return;
        }

        GUILayout.Label($"Ammo: {NullableNumber(weapon.Ammo)} / {NullableNumber(weapon.MagazineSize)}");
        GUILayout.Label($"Reserve: {NullableNumber(weapon.ReserveAmmo)}");
        GUILayout.Label($"Fire rate: {NullableNumber(weapon.FireRate)} shots/s");
        GUILayout.Label($"Fire interval: {NullableNumber(weapon.FireIntervalSeconds)} s");
        GUILayout.Label($"Current cooldown: {NullableNumber(weapon.CooldownSeconds)} s");
        GUILayout.Label($"Recoil: {Vector(weapon.Recoil)}");
        GUILayout.Label($"Spread: {NullableNumber(weapon.Spread)}");
        GUILayout.Label($"Damage: {NullableNumber(weapon.Damage)}");
        GUILayout.Label($"Reload time: {NullableNumber(weapon.ReloadTimeSeconds)} s");
    }

    private static void DrawInventory(InventoryStateSnapshot? inventory, CurrencyStateSnapshot? currency)
    {
        Section("INVENTORY");
        if (inventory is null)
        {
            GUILayout.Label("Unavailable");
        }
        else
        {
            GUILayout.Label($"Selected slot: {inventory.SelectedSlot ?? "UNKNOWN"}");
            GUILayout.Label($"Capacity: {inventory.CapacityWidth} x {inventory.CapacityHeight}");
            GUILayout.Label($"Entries: {inventory.StoredEntryCount} stored / {inventory.EquippedEntryCount} equipped");
            GUILayout.Label($"Total quantity: {inventory.TotalQuantity}");
            foreach (var item in inventory.EquippedItems)
            {
                GUILayout.Label($"Equipped: {item}");
            }

            foreach (var item in inventory.StoredItems)
            {
                GUILayout.Label($"Stored: {item}");
            }
        }

        if (currency != null)
        {
            GUILayout.Label($"Currency: ${currency.Dollar} / Silver {currency.Silver} / Gold {currency.Gold}");
        }
    }

    private static void DrawProgression(ProgressionStateSnapshot? progression)
    {
        Section("PROGRESSION");
        if (progression is null)
        {
            GUILayout.Label("Unavailable");
            return;
        }

        GUILayout.Label($"Loadout level: {progression.LoadoutLevel}");
        GUILayout.Label($"Perks: {(progression.Perks.Length == 0 ? "NONE" : string.Join(", ", progression.Perks))}");
        GUILayout.Label("XP / points / skills: not present in this build");
    }

    private static void DrawNetwork(LabSnapshot snapshot)
    {
        Section("NETWORK");
        GUILayout.Label($"In game: {YesNo(snapshot.InGame)}");
        GUILayout.Label($"Local player: {YesNo(snapshot.LocalPlayerAvailable)}");
        GUILayout.Label($"Local control: {YesNo(snapshot.HasLocalControl)}");
        GUILayout.Label($"Role: {snapshot.Role}");
        GUILayout.Label($"Connection: {snapshot.ConnectionState}");
        GUILayout.Label($"Lobby ID: {snapshot.LobbyId ?? "UNKNOWN"}");
        GUILayout.Label($"Ping: {(snapshot.PingMilliseconds.HasValue ? snapshot.PingMilliseconds + " ms" : "UNKNOWN")}");
    }

    private static void DrawWarnings(LabSnapshot snapshot)
    {
        if (snapshot.CaptureWarnings.Length == 0)
        {
            return;
        }

        Section("CAPTURE WARNINGS");
        foreach (var warning in snapshot.CaptureWarnings)
        {
            GUILayout.Label(warning);
        }
    }

    private static void Section(string title)
    {
        GUILayout.Space(10f);
        GUILayout.Label(title);
    }

    private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string NullableNumber(float? value) => value.HasValue ? Number(value.Value) : "UNKNOWN";

    private static string NullableNumber(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "UNKNOWN";

    private static string Vector(LabVector2? value) => value is null ? "UNKNOWN" : $"({Number(value.X)}, {Number(value.Y)})";

    private static string Vector(LabVector3? value) => value is null ? "UNKNOWN" : $"({Number(value.X)}, {Number(value.Y)}, {Number(value.Z)})";

    private static string YesNo(bool value) => value ? "YES" : "NO";

    private static string ShortSession(string value) => value.Length <= 8 ? value : value.Substring(0, 8);
}
