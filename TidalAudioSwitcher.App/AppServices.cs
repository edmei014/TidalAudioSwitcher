using TidalAudioSwitcher.App.Services;
using TidalAudioSwitcher.App.ViewModels;
using TidalAudioSwitcher.Shared;

namespace TidalAudioSwitcher.App;

public sealed class AppServices
{
    public AppServices()
    {
        Settings = new SettingsService();
        TidalCdp = new TidalCdpService(debugPort: TidalLaunchConstants.DebugPort);
        var audio = new AudioEndpointService();
        var windowsDefault = new WindowsDefaultDeviceService();
        Switcher = new AudioSwitcherService(TidalCdp, audio, windowsDefault);
        ViewModel = new MainViewModel(Switcher, Settings, TidalCdp);
    }

    public SettingsService Settings { get; }

    public ITidalCdpService TidalCdp { get; }

    public IAudioSwitcherService Switcher { get; }

    public MainViewModel ViewModel { get; }

    public Task InitializeAsync() => ViewModel.InitializeAsync();
}
