namespace TidalAudioSwitcher.App;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        TidalAudioSwitcher.Shared.ShellIntegration.SetAppUserModelId();

        if (UiLauncherRedirect.TryRedirectToLauncher(args))
        {
            return;
        }

        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
