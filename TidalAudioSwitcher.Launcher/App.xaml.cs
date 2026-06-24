using System.Windows;

namespace TidalAudioSwitcher.Launcher;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var exitCode = TidalLauncherBootstrap.Run();
        Shutdown(exitCode);
    }
}
