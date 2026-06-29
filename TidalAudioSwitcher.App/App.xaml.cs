using System.Windows;
using System.Windows.Threading;
using TidalAudioSwitcher.App.Services;
using TidalAudioSwitcher.App.ViewModels;

namespace TidalAudioSwitcher.App;

public partial class App : System.Windows.Application
{
    public AppServices Services { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        StartupLogger.Log("OnStartup Begin");

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                StartupLogger.LogException("AppDomain.UnhandledException", ex);
                ShowFatalError("Unbehandelte Ausnahme (AppDomain)", ex);
            }
        };

        DispatcherUnhandledException += (_, args) =>
        {
            StartupLogger.LogException("DispatcherUnhandledException", args.Exception);
            ShowFatalError("Unbehandelte Ausnahme (UI-Thread)", args.Exception);
            args.Handled = true;
            Shutdown(-1);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            StartupLogger.LogException("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        try
        {
            base.OnStartup(e);

            var startCompact = e.Args.Any(arg =>
                string.Equals(arg, "--compact", StringComparison.OrdinalIgnoreCase));

            StartupLogger.Log("MainWindow Create");
            var window = new MainWindow(startCompact);
            MainWindow = window;
            window.ContentRendered += OnMainWindowContentRendered;
            WindowLifecycleTrace.Log(window, startCompact ? "B-startup" : "A-expanded", "App.Show():before");
            window.Show();
            WindowLifecycleTrace.Log(window, startCompact ? "B-startup" : "A-expanded", "App.Show():after");

            StartupLogger.Log("OnStartup Complete");
        }
        catch (Exception ex)
        {
            StartupLogger.LogException("OnStartup Failed", ex);
            ShowFatalError("Startup fehlgeschlagen", ex);
            Shutdown(-1);
        }
    }

    private void OnMainWindowContentRendered(object? sender, EventArgs e)
    {
        if (sender is not Window window)
        {
            return;
        }

        window.ContentRendered -= OnMainWindowContentRendered;
        UiReadyHandshake.SignalReady();
        StartupLogger.Log("UiReady signal sent");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (MainWindow is MainWindow mainWindow)
            {
                mainWindow.PersistWindowState();
            }

            Services.ViewModel.SaveSettingsOnExit();
        }
        catch (Exception ex)
        {
            StartupLogger.LogException("OnExit save failed", ex);
        }

        base.OnExit(e);
    }

    private static void ShowFatalError(string title, Exception ex)
    {
        StartupLogger.LogException(title, ex);
        MessageBox.Show(
            $"{title}\n\nDetails wurden in der Protokolldatei gespeichert.",
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
