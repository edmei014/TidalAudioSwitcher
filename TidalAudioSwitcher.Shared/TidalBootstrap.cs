using System.Diagnostics;
using System.Net.Http;
using System.Net.Sockets;

namespace TidalAudioSwitcher.Shared;

public static class TidalBootstrap
{
    public static string? FindTidalExecutable()
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

    public static bool IsTidalRunning()
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

    public static bool IsCdpReady(int debugPort = TidalLaunchConstants.DebugPort)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var json = http.GetStringAsync($"http://127.0.0.1:{debugPort}/json/list").GetAwaiter().GetResult();
            return json.Contains("desktop.tidal.com", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SocketException)
        {
            return false;
        }
    }

    public static void StartTidal(string tidalExe)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = tidalExe,
            Arguments = $"{TidalLaunchConstants.DebugPortArgument} {TidalLaunchConstants.RemoteAllowOriginsArgument}",
            UseShellExecute = true
        });
    }

    public static async Task<bool> CloseTidalGracefullyAsync(CancellationToken cancellationToken = default)
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
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (process.HasExited)
                {
                    continue;
                }

                if (process.MainWindowHandle != nint.Zero)
                {
                    process.CloseMainWindow();
                }
            }
            catch
            {
                // Fall back to force close below.
            }
            finally
            {
                process.Dispose();
            }
        }

        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsTidalRunning())
            {
                return true;
            }

            await Task.Delay(250, cancellationToken);
        }

        return await ForceCloseTidalAsync(cancellationToken);
    }

    public static async Task<bool> WaitForCdpAsync(
        TimeSpan timeout,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default,
        int debugPort = TidalLaunchConstants.DebugPort)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (IsCdpReady(debugPort))
            {
                return true;
            }

            progress?.Report("Warte auf TIDAL-Verbindung …");
            await Task.Delay(1000, cancellationToken);
        }

        return false;
    }

    /// <summary>
    /// Closes TIDAL if running, starts it with CDP parameters, and waits until CDP is ready.
    /// Matches the launcher cold-start / restart path.
    /// </summary>
    public static async Task<TidalBootstrapResult> RestartTidalAsync(
        IProgress<string>? progress = null,
        TimeSpan? cdpTimeout = null,
        CancellationToken cancellationToken = default)
    {
        if (IsTidalRunning())
        {
            progress?.Report("Schließe TIDAL …");
            if (!await CloseTidalGracefullyAsync(cancellationToken))
            {
                return TidalBootstrapResult.CloseFailed;
            }
        }

        var tidalExe = FindTidalExecutable();
        if (tidalExe is null)
        {
            return TidalBootstrapResult.TidalNotFound;
        }

        progress?.Report("Starte TIDAL …");
        StartTidal(tidalExe);

        var ready = await WaitForCdpAsync(
            cdpTimeout ?? TimeSpan.FromSeconds(45),
            progress,
            cancellationToken);

        return ready ? TidalBootstrapResult.Success : TidalBootstrapResult.CdpTimeout;
    }

    /// <summary>
    /// Starts TIDAL when not running and waits for CDP. Skips close when already ready.
    /// </summary>
    public static async Task<TidalBootstrapResult> EnsureTidalWithCdpAsync(
        IProgress<string>? progress = null,
        TimeSpan? cdpTimeout = null,
        CancellationToken cancellationToken = default)
    {
        if (IsTidalRunning() && IsCdpReady())
        {
            return TidalBootstrapResult.Success;
        }

        if (IsTidalRunning())
        {
            return TidalBootstrapResult.CloseFailed;
        }

        var tidalExe = FindTidalExecutable();
        if (tidalExe is null)
        {
            return TidalBootstrapResult.TidalNotFound;
        }

        progress?.Report("Starte TIDAL …");
        StartTidal(tidalExe);

        var ready = await WaitForCdpAsync(
            cdpTimeout ?? TimeSpan.FromSeconds(45),
            progress,
            cancellationToken);

        return ready ? TidalBootstrapResult.Success : TidalBootstrapResult.CdpTimeout;
    }

    private static async Task<bool> ForceCloseTidalAsync(CancellationToken cancellationToken)
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
            cancellationToken.ThrowIfCancellationRequested();

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
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsTidalRunning())
            {
                return true;
            }

            await Task.Delay(250, cancellationToken);
        }

        return !IsTidalRunning();
    }
}
