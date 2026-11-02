using System.Diagnostics;
using System.IO;

namespace TidalAudioSwitcher.Shared;

public static class ApplicationPaths
{
    /// <summary>
    /// Resolves the public launcher executable (release: next to UI; dev: sibling Launcher output).
    /// </summary>
    public static string? ResolveLauncherExe()
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

        // Last resort: current process when this is already the launcher.
        var processPath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (!string.IsNullOrEmpty(processPath)
            && string.Equals(
                Path.GetFileName(processPath),
                ApplicationEntryConstants.LauncherExeName,
                StringComparison.OrdinalIgnoreCase)
            && File.Exists(processPath))
        {
            return processPath;
        }

        return null;
    }
}
