using TidalAudioSwitcher.App.Models;

namespace TidalAudioSwitcher.App.Services;

public interface IAudioSwitcherService
{
    IReadOnlyList<AudioEndpoint> ListWindowsDevices();

    Task<(DeviceSwitchLog WindowsLog, string TidalStep)> ApplyMusicModeAsync(
        AudioEndpoint tidalDevice,
        AudioEndpoint silentDevice,
        CancellationToken cancellationToken = default);

    Task<(DeviceSwitchLog WindowsLog, string TidalStep)> ApplyNormalModeAsync(
        AudioEndpoint tidalDevice,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates Windows default device switching and TIDAL exclusive/shared mode.
/// </summary>
public sealed class AudioSwitcherService : IAudioSwitcherService
{
    private readonly ITidalCdpService _tidal;
    private readonly IAudioEndpointService _audio;
    private readonly IWindowsDefaultDeviceService _windowsDefault;

    public AudioSwitcherService(
        ITidalCdpService tidal,
        IAudioEndpointService audio,
        IWindowsDefaultDeviceService windowsDefault)
    {
        _tidal = tidal;
        _audio = audio;
        _windowsDefault = windowsDefault;
    }

    public IReadOnlyList<AudioEndpoint> ListWindowsDevices() => _audio.ListRenderEndpoints();

    public async Task<(DeviceSwitchLog WindowsLog, string TidalStep)> ApplyMusicModeAsync(
        AudioEndpoint tidalDevice,
        AudioEndpoint silentDevice,
        CancellationToken cancellationToken = default)
    {
        var previousDefault = await Task.Run(() => _windowsDefault.GetDefaultMultimediaEndpoint());

        var windowsLog = await Task.Run(() =>
            _windowsDefault.SetDefaultMultimediaEndpoint(
                silentDevice,
                previousDefault,
                "Music Mode — Silent Device als Windows-Standard setzen"));

        if (!windowsLog.AllRolesSucceeded)
        {
            throw new InvalidOperationException(
                $"SetDefaultEndpoint fehlgeschlagen. Details:\n{windowsLog.ToDisplayText()}");
        }

        if (!windowsLog.VerifiedMatch)
        {
            throw new InvalidOperationException(
                $"Windows-Standard konnte nicht verifiziert werden. Details:\n{windowsLog.ToDisplayText()}");
        }

        await Task.Delay(300, cancellationToken);

        var tidal = ToTidalDevice(tidalDevice);
        await _tidal.SelectDeviceAsync(tidal, "exclusive", cancellationToken);

        var tidalStep =
            $"TIDAL → exclusive auf \"{tidalDevice.FriendlyName}\" [{tidalDevice.Id}]";

        return (windowsLog, tidalStep);
    }

    public async Task<(DeviceSwitchLog WindowsLog, string TidalStep)> ApplyNormalModeAsync(
        AudioEndpoint tidalDevice,
        CancellationToken cancellationToken = default)
    {
        var tidal = ToTidalDevice(tidalDevice);
        await _tidal.SelectDeviceAsync(tidal, "shared", cancellationToken);

        var tidalStep =
            $"TIDAL → shared auf \"{tidalDevice.FriendlyName}\" [{tidalDevice.Id}]";

        var previousDefault = await Task.Run(() => _windowsDefault.GetDefaultMultimediaEndpoint());

        var windowsLog = await Task.Run(() =>
            _windowsDefault.SetDefaultMultimediaEndpoint(
                tidalDevice,
                previousDefault,
                "Normal Mode — TIDAL Device als Windows-Standard setzen"));

        if (!windowsLog.AllRolesSucceeded)
        {
            throw new InvalidOperationException(
                $"SetDefaultEndpoint fehlgeschlagen. Details:\n{windowsLog.ToDisplayText()}");
        }

        if (!windowsLog.VerifiedMatch)
        {
            throw new InvalidOperationException(
                $"Windows-Standard konnte nicht verifiziert werden. Details:\n{windowsLog.ToDisplayText()}");
        }

        return (windowsLog, tidalStep);
    }

    private static TidalDevice ToTidalDevice(AudioEndpoint endpoint) =>
        new()
        {
            Id = endpoint.Id,
            Name = endpoint.FriendlyName,
            IsDefault = endpoint.IsDefault,
            Raw = new Dictionary<string, object?>
            {
                ["id"] = endpoint.Id,
                ["name"] = endpoint.FriendlyName
            }
        };
}
