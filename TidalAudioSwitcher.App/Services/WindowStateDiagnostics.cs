using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace TidalAudioSwitcher.App.Services;

internal static class WindowStateDiagnostics
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    public static void Log(Window window, string path, string phase, bool? settingsCompactMode = null)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var vmCompact = window.DataContext is ViewModels.MainViewModel vm && vm.IsCompactView;
        var settingsCompact = settingsCompactMode switch
        {
            null when window.DataContext is ViewModels.MainViewModel viewModel =>
                ((App)System.Windows.Application.Current).Services.Settings.Load().CompactMode,
            null => null,
            var value => value
        };

        var foreground = GetForegroundWindow();
        var isForeground = handle != nint.Zero && foreground == handle;

        StartupLogger.Log(
            "[CompactState] " +
            $"path={path} " +
            $"phase={phase} " +
            $"SettingsCompactMode={FormatNullable(settingsCompact)} " +
            $"IsCompactView={vmCompact} " +
            $"Topmost={window.Topmost} " +
            $"IsVisible={window.IsVisible} " +
            $"IsActive={window.IsActive} " +
            $"IsFocused={window.IsFocused} " +
            $"WindowState={window.WindowState} " +
            $"ResizeMode={window.ResizeMode} " +
            $"Visibility={window.Visibility} " +
            $"HwndPresent={handle != nint.Zero} " +
            $"Hwnd=0x{handle:X} " +
            $"IsForegroundWindow={isForeground} " +
            $"ForegroundHwnd=0x{foreground:X} " +
            $"ForegroundTitle={Quote(GetWindowTitle(foreground))} " +
            $"Bounds=({window.Left:F0},{window.Top:F0},{window.ActualWidth:F0}x{window.ActualHeight:F0})");
    }

    private static string GetWindowTitle(IntPtr handle)
    {
        if (handle == nint.Zero)
        {
            return "(none)";
        }

        var builder = new StringBuilder(256);
        _ = GetWindowText(handle, builder, builder.Capacity);
        return builder.Length > 0 ? builder.ToString() : "(no title)";
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static string FormatNullable(bool? value) =>
        value switch
        {
            null => "n/a",
            true => "True",
            false => "False"
        };
}
