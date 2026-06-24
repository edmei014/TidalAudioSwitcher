using NAudio.CoreAudioApi;
using TidalAudioSwitcher.App.Models;
using TidalAudioSwitcher.App.Services.Interop;

namespace TidalAudioSwitcher.App.Services;

public interface IWindowsDefaultDeviceService
{
    AudioEndpoint? GetDefaultMultimediaEndpoint();

    DefaultDeviceSnapshot CaptureCurrentDefaults();

    DeviceSwitchLog SetDefaultMultimediaEndpoint(
        AudioEndpoint target,
        AudioEndpoint? previousDefault,
        string operationLabel);

    void RestoreDefaults(DefaultDeviceSnapshot snapshot, out DeviceSwitchLog log);
}

public sealed class WindowsDefaultDeviceService : IWindowsDefaultDeviceService
{
    private IPolicyConfigWriter? _policyConfig;

    private IPolicyConfigWriter PolicyConfig =>
        _policyConfig ??= PolicyConfigFactory.Create();

    public AudioEndpoint? GetDefaultMultimediaEndpoint()
    {
        using var enumerator = new MMDeviceEnumerator();
        using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return ToEndpoint(device, isDefault: true);
    }

    public DefaultDeviceSnapshot CaptureCurrentDefaults()
    {
        using var enumerator = new MMDeviceEnumerator();

        using var console = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
        using var multimedia = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        using var communications = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Communications);

        return new DefaultDeviceSnapshot
        {
            ConsoleDeviceId = console.ID,
            MultimediaDeviceId = multimedia.ID,
            CommunicationsDeviceId = communications.ID,
            ConsoleDeviceName = console.FriendlyName,
            MultimediaDeviceName = multimedia.FriendlyName,
            CommunicationsDeviceName = communications.FriendlyName
        };
    }

    public DeviceSwitchLog SetDefaultMultimediaEndpoint(
        AudioEndpoint target,
        AudioEndpoint? previousDefault,
        string operationLabel)
    {
        var roleResults = new List<RoleSwitchResult>();
        foreach (var role in new[] { ERole.eConsole, ERole.eMultimedia, ERole.eCommunications })
        {
            var hr = PolicyConfig.SetDefaultEndpoint(target.Id, role);
            roleResults.Add(new RoleSwitchResult
            {
                Role = role.ToString(),
                HResult = hr,
                Message = hr == 0 ? "OK" : new System.ComponentModel.Win32Exception(hr).Message
            });
        }

        Thread.Sleep(300);

        var verified = GetDefaultMultimediaEndpoint();
        return new DeviceSwitchLog
        {
            Operation = operationLabel,
            PreviousDefault = previousDefault,
            RequestedDefault = target,
            VerifiedDefault = verified,
            RoleResults = roleResults
        };
    }

    public void RestoreDefaults(DefaultDeviceSnapshot snapshot, out DeviceSwitchLog log)
    {
        var previous = GetDefaultMultimediaEndpoint();
        var roleResults = new List<RoleSwitchResult>();

        TrySetRole(snapshot.ConsoleDeviceId, ERole.eConsole, roleResults);
        TrySetRole(snapshot.MultimediaDeviceId, ERole.eMultimedia, roleResults);
        TrySetRole(snapshot.CommunicationsDeviceId, ERole.eCommunications, roleResults);

        Thread.Sleep(300);

        var verified = GetDefaultMultimediaEndpoint();
        log = new DeviceSwitchLog
        {
            Operation = "Normal Mode — Windows-Standard wiederherstellen",
            PreviousDefault = previous,
            RequestedDefault = new AudioEndpoint
            {
                Id = snapshot.MultimediaDeviceId,
                FriendlyName = snapshot.MultimediaDeviceName,
                IsDefault = true
            },
            VerifiedDefault = verified,
            RoleResults = roleResults
        };
    }

    private void TrySetRole(string deviceId, ERole role, List<RoleSwitchResult> results)
    {
        var hr = PolicyConfig.SetDefaultEndpoint(deviceId, role);
        results.Add(new RoleSwitchResult
        {
            Role = role.ToString(),
            HResult = hr,
            Message = hr == 0 ? "OK" : new System.ComponentModel.Win32Exception(hr).Message
        });
    }

    private static AudioEndpoint ToEndpoint(MMDevice device, bool isDefault) =>
        new()
        {
            Id = device.ID,
            FriendlyName = device.FriendlyName,
            IsDefault = isDefault
        };
}
