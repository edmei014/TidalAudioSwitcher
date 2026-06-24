namespace TidalAudioSwitcher.App.Models;

/// <summary>
/// Windows Core Audio render endpoint (MMDevice).
/// </summary>
public sealed class AudioEndpoint
{
    public required string Id { get; init; }
    public required string FriendlyName { get; init; }
    public bool IsDefault { get; init; }

    public override string ToString() => FriendlyName;
}
