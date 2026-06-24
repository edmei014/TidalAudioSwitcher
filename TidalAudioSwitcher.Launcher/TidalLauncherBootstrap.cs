using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows;

namespace TidalAudioSwitcher.Launcher;

internal static class TidalLauncherBootstrap
{
    private const int DebugPort = 9222;
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

        var tidalExe = FindTidalExecutable();
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

        if (IsTidalRunning())
        {
            if (IsCdpReady())
            {
                return StartUi(uiPath, waitWindow: null);
            }

            var dialog = new TidalAlreadyRunningDialog();
            if (dialog.ShowDialog() != true || !dialog.RestartRequested)
            {
                return 0;
            }

            if (!TryCloseTidal())
            {
                MessageBox.Show(
                    "TIDAL konnte nicht vollständig beendet werden.\n\nBitte TIDAL manuell schließen und erneut starten.",
                    "Tidal Audio Switcher",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return 1;
            }
        }

        StartTidal(tidalExe);

        var waitWindow = new TidalWaitWindow();
        waitWindow.Show();
        try
        {
            if (!WaitForCdp(waitWindow, TimeSpan.FromSeconds(45)))
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

    private static string? FindTidalExecutable()
    {
        var tidalRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TIDAL");

        if (!Directory.Exists(tidalRoot))
        {
            return null;
        }

        return Directory
            .GetDirectories(tidalRoot, "app-*")
            .Select(dir => Path.Combine(dir, "TIDAL.exe"))
            .Where(File.Exists)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool IsTidalRunning()
    {
        try
        {
            return Process.GetProcessesByName("TIDAL").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryCloseTidal()
    {
        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName("TIDAL");
        }
        catch
        {
            return false;
        }

        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                process.Dispose();
            }
        }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            if (!IsTidalRunning())
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return !IsTidalRunning();
    }

    private static void StartTidal(string tidalExe)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = tidalExe,
            Arguments = "--remote-debugging-port=9222 --remote-allow-origins=*",
            UseShellExecute = true
        });
    }

    private static bool IsCdpReady()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var json = http.GetStringAsync($"http://127.0.0.1:{DebugPort}/json/list").GetAwaiter().GetResult();
            return json.Contains("desktop.tidal.com", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SocketException)
        {
            return false;
        }
    }

    private static bool WaitForCdp(TidalWaitWindow waitWindow, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (IsCdpReady())
            {
                return true;
            }

            waitWindow.SetStatus("Warte auf TIDAL-Verbindung …");
            waitWindow.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);
            Thread.Sleep(1000);
        }

        return false;
    }
}
