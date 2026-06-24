namespace TidalAudioSwitcher.App.Models;

/// <summary>
/// Audio output device reported by TIDAL via media.devices.
/// </summary>
public sealed class TidalDevice
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Type { get; init; }
    public bool IsDefault { get; init; }
    public Dictionary<string, object?> Raw { get; init; } = new();

    public override string ToString() => Name;
}
