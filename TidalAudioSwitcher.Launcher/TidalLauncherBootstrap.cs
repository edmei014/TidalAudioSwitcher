using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using TidalAudioSwitcher.Shared;

namespace TidalAudioSwitcher.Launcher;

internal static class TidalLauncherBootstrap
{
    private const string UiExeName = "TidalAudioSwitcher.UI.exe";

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    public static int Run()
    {
        var uiPath = Path.Combine(AppContext.BaseDirectory, UiExeName);
        if (!File.Exists(uiPath))
        {
            MessageBox.Show(
                $"Die Anwendungsoberfläche wurde nicht gefunden:\n{uiPath}\n\nBitte die Installation erneut durchführen.",
                "Tidal Audio Switcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return 1;
        }

        var tidalExe = TidalBootstrap.FindTidalExecutable();
        if (tidalExe is null)
        {
            MessageBox.Show(
                "TIDAL.exe wurde auf diesem PC nicht gefunden.\n\n" +
                $"Erwartet unter:\n{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TIDAL", "app-*", "TIDAL.exe")}",
                "Tidal Audio Switcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            return 1;
        }

        if (TidalBootstrap.IsTidalRunning())
        {
            if (TidalBootstrap.IsCdpReady())
            {
                return StartUi(uiPath, waitWindow: null);
            }

            if (!ConfirmTidalRestart())
            {
                return 0;
            }

            if (!TidalBootstrap.CloseTidalGracefullyAsync().ConfigureAwait(false).GetAwaiter().GetResult())
            {
                MessageBox.Show(
                    "TIDAL konnte nicht vollständig beendet werden.\n\nBitte TIDAL manuell schließen und erneut starten.",
                    "Tidal Audio Switcher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return 1;
            }
        }

        TidalBootstrap.StartTidal(tidalExe);

        var waitWindow = new TidalWaitWindow();
        waitWindow.Show();
        try
        {
            var progress = new Progress<string>(waitWindow.SetStatus);
            var ready = TidalBootstrap.WaitForCdpAsync(
                    TimeSpan.FromSeconds(45),
                    progress)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            if (!ready)
            {
                MessageBox.Show(
                    "TIDAL antwortet nicht rechtzeitig auf dem Debug-Port.\n\n" +
                    "Bitte erneut versuchen, sobald TIDAL vollständig geladen ist.",
                    "Tidal Audio Switcher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return 1;
            }

            return StartUi(uiPath, waitWindow);
        }
        finally
        {
            if (waitWindow.IsVisible)
            {
                waitWindow.Close();
            }
        }
    }

    private static bool ConfirmTidalRestart()
    {
        var dialog = new TidalAlreadyRunningDialog();
        return dialog.ShowDialog() == true && dialog.RestartRequested;
    }

    private static int StartUi(string uiPath, TidalWaitWindow? waitWindow)
    {
        using var readyHandle = UiReadyHandshake.CreateForWait();

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = uiPath,
            WorkingDirectory = Path.GetDirectoryName(uiPath)!,
            UseShellExecute = true
        });

        if (process is not null)
        {
            try
            {
                AllowSetForegroundWindow(process.Id);
            }
            catch
            {
                // Best effort — UI activates itself on startup.
            }
        }

        readyHandle.WaitOne(UiReadyHandshake.WaitTimeout);

        waitWindow?.Close();
        return 0;
    }
}
