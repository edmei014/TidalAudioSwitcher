using System.Windows;
using System.Windows.Interop;

namespace TidalAudioSwitcher.App.Services;

internal static class WindowLifecycleTrace
{
    private static nint _lastHwnd;

    public static void Log(Window window, string path, string phase)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        var hwndChanged = hwnd != nint.Zero && _lastHwnd != nint.Zero && hwnd != _lastHwnd;
        if (hwnd != nint.Zero)
        {
            _lastHwnd = hwnd;
        }

        var vmCompact = window.DataContext is ViewModels.MainViewModel vm && vm.IsCompactView;

        StartupLogger.Log(
            "[Lifecycle] " +
            $"path={path} " +
            $"phase={Quote(phase)} " +
            $"hwnd=0x{hwnd:X} " +
            $"hwndChanged={hwndChanged} " +
            $"IconAssigned={window.Icon != null} " +
            $"ShowInTaskbar={window.ShowInTaskbar} " +
            $"WindowStyle={window.WindowStyle} " +
            $"Topmost={window.Topmost} " +
            $"IsVisible={window.IsVisible} " +
            $"Visibility={window.Visibility} " +
            $"IsCompactView={vmCompact} " +
            $"Bounds=({window.Left:F0},{window.Top:F0},{window.ActualWidth:F0}x{window.ActualHeight:F0}) " +
            $"ResizeMode={window.ResizeMode}");
    }

    public static void LogApp(string phase)
    {
        StartupLogger.Log($"[Lifecycle] path=app phase={Quote(phase)}");
    }

    public static void LogForeground(string path, string phase, Window window)
    {
        var wasMinimized = window.WindowState == WindowState.Minimized;
        Log(window, path, $"{phase}(wasMinimized={wasMinimized},willShow={!window.IsVisible})");
    }

    private static string Quote(string value) => $"\"{value}\"";
}
