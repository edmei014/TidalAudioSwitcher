namespace TidalAudioSwitcher.App.Models;

public enum TidalCdpHealthStatus
{
    Ready,
    TidalNotRunning,
    CdpNotReachable,
    TidalRunningWithoutCdp,
    NoDesktopTarget
}

public sealed class TidalCdpHealthResult
{
    public required TidalCdpHealthStatus Status { get; init; }

    public required string UserMessage { get; init; }

    public string? Detail { get; init; }

    public bool IsReady => Status == TidalCdpHealthStatus.Ready;
}
