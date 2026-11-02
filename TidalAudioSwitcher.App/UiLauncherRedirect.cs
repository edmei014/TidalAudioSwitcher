using System.Diagnostics;
using System.IO;
using TidalAudioSwitcher.Shared;

namespace TidalAudioSwitcher.App;

internal static class UiLauncherRedirect
{
    /// <summary>
    /// When the UI is started directly (e.g. pinned taskbar shortcut to TidalAudioSwitcher.UI.exe),
    /// restart through the public launcher entry point and exit.
    /// </summary>
    public static bool TryRedirectToLauncher(string[] args)
    {
        if (WasLaunchedByLauncher(args))
        {
            return false;
        }

        var launcherPath = ApplicationPaths.ResolveLauncherExe();
        if (launcherPath is null)
        {
            StartupLogger.Log(
                $"UI direct start: launcher not found near {AppContext.BaseDirectory} — continuing without redirect");
            return false;
        }

        StartupLogger.Log(
            $"UI direct start detected — redirecting to launcher: {launcherPath}");

        Process.Start(new ProcessStartInfo
        {
            FileName = launcherPath,
            WorkingDirectory = Path.GetDirectoryName(launcherPath)!,
            UseShellExecute = true
        });

        return true;
    }

    public static bool WasLaunchedByLauncher(string[] args) =>
        args.Any(arg =>
            string.Equals(arg, ApplicationEntryConstants.LaunchedByLauncherArgument, StringComparison.OrdinalIgnoreCase));
}
