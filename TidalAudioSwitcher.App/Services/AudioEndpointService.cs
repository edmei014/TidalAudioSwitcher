using NAudio.CoreAudioApi;
using TidalAudioSwitcher.App.Models;

namespace TidalAudioSwitcher.App.Services;

public interface IAudioEndpointService
{
    IReadOnlyList<AudioEndpoint> ListRenderEndpoints();
}

/// <summary>
/// Enumerates Windows Core Audio render endpoints (MMDevice).
/// </summary>
public sealed class AudioEndpointService : IAudioEndpointService
{
    public IReadOnlyList<AudioEndpoint> ListRenderEndpoints()
    {
        using var enumerator = new MMDeviceEnumerator();
        var defaultId = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)?.ID;

        var endpoints = new List<AudioEndpoint>();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            endpoints.Add(new AudioEndpoint
            {
                Id = device.ID,
                FriendlyName = device.FriendlyName,
                IsDefault = device.ID == defaultId
            });
            device.Dispose();
        }

        return endpoints
            .OrderByDescending(e => e.IsDefault)
            .ThenBy(e => e.FriendlyName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
