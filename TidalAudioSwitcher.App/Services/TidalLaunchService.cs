using System.Diagnostics;
using System.IO;
using TidalAudioSwitcher.App.Models;

namespace TidalAudioSwitcher.App.Services;

public sealed class TidalLaunchService
{
    private readonly ITidalCdpService _tidalCdp;

    public TidalLaunchService(ITidalCdpService tidalCdp)
    {
        _tidalCdp = tidalCdp;
    }

    public static string? FindLauncherExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "TidalAudioSwitcher.exe"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "TidalAudioSwitcher.exe"))
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    public async Task<TidalLaunchOutcome> StartTidalAndWaitForCdpAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (TidalProcessService.IsTidalRunning())
        {
            var runningHealth = await _tidalCdp.CheckHealthAsync(cancellationToken);
            if (runningHealth.IsReady)
            {
                return TidalLaunchOutcome.Success;
            }

            return TidalLaunchOutcome.TidalAlreadyRunningNeedsClose;
        }

        var tidalExe = TidalProcessService.FindTidalExecutable();
        if (tidalExe is null)
        {
            return TidalLaunchOutcome.TidalNotFound;
        }

        progress?.Report("Starte TIDAL …");
        StartTidal(tidalExe);

        var deadline = DateTime.UtcNow.AddSeconds(45);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report("Warte auf TIDAL-Verbindung …");

            var health = await _tidalCdp.CheckHealthAsync(cancellationToken);
            if (health.IsReady)
            {
                return TidalLaunchOutcome.Success;
            }

            await Task.Delay(1000, cancellationToken);
        }

        return TidalLaunchOutcome.Timeout;
    }

    public static TidalLaunchOutcome StartLauncherExecutable(string launcherPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = launcherPath,
            WorkingDirectory = Path.GetDirectoryName(launcherPath)!,
            UseShellExecute = true
        });

        return TidalLaunchOutcome.LauncherStarted;
    }

    private static void StartTidal(string tidalExe)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = tidalExe,
            Arguments = $"{TidalLaunchConstants.DebugPortArgument} {TidalLaunchConstants.RemoteAllowOriginsArgument}",
            UseShellExecute = true
        });
    }
}
