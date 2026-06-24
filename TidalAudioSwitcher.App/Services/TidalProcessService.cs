using System.Diagnostics;
using System.IO;

namespace TidalAudioSwitcher.App.Services;

public static class TidalProcessService
{
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

    public static string? FindTidalExecutable()
    {
        var tidalRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TIDAL");

        if (!Directory.Exists(tidalRoot))
        {
            return null;
        }

        var candidates = Directory
            .GetDirectories(tidalRoot, "app-*")
            .Select(dir => Path.Combine(dir, "TIDAL.exe"))
            .Where(File.Exists)
            .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return candidates.FirstOrDefault();
    }
}
