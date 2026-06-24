using System.IO;
using System.Text;

namespace TidalAudioSwitcher.App;

internal static class StartupLogger
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "TidalAudioSwitcher.log");
    private static readonly object Gate = new();

    public static string FilePath => LogPath;

    public static void Log(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
        lock (Gate)
        {
            File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
        }
    }

    public static void LogException(string context, Exception ex)
    {
        Log($"{context}: {ex.GetType().FullName}: {ex.Message}");
        Log(ex.ToString());
    }
}
