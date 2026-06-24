using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TidalAudioSwitcher.App.Services;

internal static class WindowForegroundHelper
{
    private const int SwRestore = 9;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    public static void BringToForeground(Window window, bool persistentTopmost)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != nint.Zero)
        {
            ShowWindow(handle, SwRestore);
            SetForegroundWindow(handle);
        }

        if (persistentTopmost)
        {
            window.Topmost = true;
        }
        else
        {
            window.Topmost = true;
            window.Topmost = false;
        }

        window.Activate();
        window.Focus();
    }

    public static bool TryBringToForeground(Window window, bool persistentTopmost)
    {
        BringToForeground(window, persistentTopmost);
        return new WindowInteropHelper(window).Handle != nint.Zero;
    }
}
