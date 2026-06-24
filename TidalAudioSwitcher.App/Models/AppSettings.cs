namespace TidalAudioSwitcher.App.Models;

public sealed class AppSettings
{
    public string? TidalDeviceId { get; set; }

    public string? SilentDeviceId { get; set; }

    public AudioMode ActiveMode { get; set; } = AudioMode.Normal;

    public bool CompactMode { get; set; }

    public double? WindowLeft { get; set; }

    public double? WindowTop { get; set; }

    public double? WindowWidth { get; set; }

    public double? WindowHeight { get; set; }
}
