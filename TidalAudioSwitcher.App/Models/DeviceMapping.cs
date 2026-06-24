namespace TidalAudioSwitcher.App.Models;

/// <summary>
/// Maps a TIDAL playback device to a Windows audio endpoint.
/// </summary>
public sealed class DeviceMapping
{
    public required TidalDevice TidalDevice { get; init; }
    public required AudioEndpoint WindowsEndpoint { get; init; }
    public int MatchScore { get; init; }
    public string MatchReason { get; init; } = string.Empty;

    public string DisplayName =>
        $"{TidalDevice.Name}  →  {WindowsEndpoint.FriendlyName}";

    public override string ToString() => DisplayName;
}
