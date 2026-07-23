namespace TidalAudioSwitcher.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Redirect before setting AppUserModelID so a direct UI.exe start (e.g. wrong
        // taskbar pin) does not register itself as the app identity Windows will relaunch.
        if (UiLauncherRedirect.TryRedirectToLauncher(args))
        {
            return;
        }

        TidalAudioSwitcher.Shared.ShellIntegration.SetAppUserModelId();

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
