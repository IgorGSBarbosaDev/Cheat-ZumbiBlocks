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

    public int? PingMilliseconds { get; set; }
}

