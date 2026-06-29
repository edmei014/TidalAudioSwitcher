using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace TidalAudioSwitcher.App.Services;

/// <summary>
/// Pushes the embedded exe icon to the HWND via WM_SETICON (taskbar uses ICON_SMALL).
/// </summary>
internal static class TaskbarIconHelper
{
    private const int WmSetIcon = 0x0080;
    private const int IconBig = 1;
    private const int IconSmall = 0;

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lIcon);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr ExtractAssociatedIcon(IntPtr hInst, StringBuilder iconPath, ref int index);

    public static void ApplyExeIcon(Window window, string path, string phase)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == nint.Zero)
        {
            WindowLifecycleTrace.Log(window, path, $"{phase}:ApplyExeIcon-skipped(no-hwnd)");
            return;
        }

        var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath))
        {
            WindowLifecycleTrace.Log(window, path, $"{phase}:ApplyExeIcon-skipped(no-exe-path)");
            return;
        }

        var index = 0;
        var iconPath = new StringBuilder(exePath, 260);
        var hIcon = ExtractAssociatedIcon(nint.Zero, iconPath, ref index);
        if (hIcon == nint.Zero)
        {
            WindowLifecycleTrace.Log(window, path, $"{phase}:ApplyExeIcon-skipped(extract-failed)");
            return;
        }

        SendMessage(hwnd, WmSetIcon, (IntPtr)IconBig, hIcon);
        SendMessage(hwnd, WmSetIcon, (IntPtr)IconSmall, hIcon);
        WindowLifecycleTrace.Log(window, path, $"{phase}:ApplyExeIcon-applied");
    }
}
