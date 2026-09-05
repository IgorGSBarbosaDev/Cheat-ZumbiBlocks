using UnityEngine;
using ZB2SecurityLab.Core.Diagnostics;

namespace ZB2SecurityLab.Plugin.UI;

internal sealed class DiagnosticPanel
{
    private readonly Rect _panelRect = new(20f, 20f, 360f, 250f);

    internal bool Visible { get; set; }

    internal void Draw(bool buildSupported, string buildStatus, LabSnapshot? snapshot, string sessionId)
    {
        if (!Visible)
        {
            return;
        }

        GUI.Box(_panelRect, "");
        GUILayout.BeginArea(new Rect(_panelRect.x + 12f, _panelRect.y + 10f, _panelRect.width - 24f, _panelRect.height - 20f));
        GUILayout.Label("ZB2 SECURITY LAB — READ ONLY");
        GUILayout.Space(6f);
        GUILayout.Label($"Build: {(buildSupported ? "SUPPORTED" : "BLOCKED")}");
        GUILayout.Label(buildStatus);
        GUILayout.Label($"Session: {ShortSession(sessionId)}");
        GUILayout.Space(8f);

        if (snapshot is null)
        {
            GUILayout.Label("Game context: unavailable");
        }
        else
        {
            GUILayout.Label($"In game: {YesNo(snapshot.InGame)}");
            GUILayout.Label($"Local player: {YesNo(snapshot.LocalPlayerAvailable)}");
            GUILayout.Label($"Local control: {YesNo(snapshot.HasLocalControl)}");
            GUILayout.Label($"Role: {snapshot.Role}");
            GUILayout.Label($"Connection: {snapshot.ConnectionState}");
            GUILayout.Label($"Lobby ID: {snapshot.LobbyId ?? "UNKNOWN"}");
            GUILayout.Label($"Ping: {(snapshot.PingMilliseconds.HasValue ? snapshot.PingMilliseconds + " ms" : "UNKNOWN")}");
        }

        GUILayout.Space(8f);
        GUILayout.Label("F8 closes this diagnostic panel.");
        GUILayout.EndArea();
    }

    private static string YesNo(bool value) => value ? "YES" : "NO";

    private static string ShortSession(string value) => value.Length <= 8 ? value : value.Substring(0, 8);
}

