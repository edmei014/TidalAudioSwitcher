namespace TidalAudioSwitcher.App.Models;

public sealed class DefaultDeviceSnapshot
{
    public required string ConsoleDeviceId { get; init; }
    public required string MultimediaDeviceId { get; init; }
    public required string CommunicationsDeviceId { get; init; }

    public required string ConsoleDeviceName { get; init; }
    public required string MultimediaDeviceName { get; init; }
    public required string CommunicationsDeviceName { get; init; }
}
