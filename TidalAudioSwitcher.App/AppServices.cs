using TidalAudioSwitcher.App.Services;
using TidalAudioSwitcher.App.ViewModels;

namespace TidalAudioSwitcher.App;

public sealed class AppServices
{
    public AppServices()
    {
        Settings = new SettingsService();
        TidalCdp = new TidalCdpService(debugPort: TidalLaunchConstants.DebugPort);
        TidalLaunch = new TidalLaunchService(TidalCdp);
        var audio = new AudioEndpointService();
        var windowsDefault = new WindowsDefaultDeviceService();
        Switcher = new AudioSwitcherService(TidalCdp, audio, windowsDefault);
        ViewModel = new MainViewModel(Switcher, Settings, TidalCdp, TidalLaunch);
    }

    public SettingsService Settings { get; }

    public ITidalCdpService TidalCdp { get; }

    public TidalLaunchService TidalLaunch { get; }

    public IAudioSwitcherService Switcher { get; }

    public MainViewModel ViewModel { get; }

    public Task InitializeAsync() => ViewModel.InitializeAsync();
}
