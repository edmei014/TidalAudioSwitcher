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

        var launcherPath = ResolveLauncherPath();
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

    /// <summary>
    /// Prefer the launcher sitting next to the UI (release layout). For development builds
    /// where only TidalAudioSwitcher.UI.exe is in App/bin, resolve the sibling Launcher output.
    /// </summary>
    private static string? ResolveLauncherPath()
    {
        var adjacent = Path.Combine(AppContext.BaseDirectory, ApplicationEntryConstants.LauncherExeName);
        if (File.Exists(adjacent))
        {
            return adjacent;
        }

        var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
        // .../TidalAudioSwitcher.App/bin/{Configuration}/net8.0-windows
        var configuration = baseDir.Parent?.Name ?? "Release";
        var solutionDir = baseDir.Parent?.Parent?.Parent?.Parent;
        if (solutionDir is null)
        {
            return null;
        }

        foreach (var config in new[] { configuration, "Release", "Debug" }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidate = Path.Combine(
                solutionDir.FullName,
                "TidalAudioSwitcher.Launcher",
                "bin",
                config,
                "net8.0-windows",
                ApplicationEntryConstants.LauncherExeName);

            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
