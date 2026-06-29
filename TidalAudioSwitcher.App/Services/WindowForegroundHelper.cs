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

    public static void BringToForeground(Window window, string path, string phase, bool persistentTopmost)
    {
        WindowLifecycleTrace.LogForeground(path, $"{phase}:before", window);

        var wasMinimized = window.WindowState == WindowState.Minimized;
        if (wasMinimized)
        {
            window.WindowState = WindowState.Normal;
        }

        if (!window.IsVisible)
        {
            WindowLifecycleTrace.Log(window, path, $"{phase}:Show()");
            window.Show();
        }

        window.Activate();

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != nint.Zero && wasMinimized)
        {
            WindowLifecycleTrace.Log(window, path, $"{phase}:ShowWindow(SW_RESTORE)");
            ShowWindow(handle, SwRestore);
            SetForegroundWindow(handle);
        }
        else if (handle != nint.Zero)
        {
            SetForegroundWindow(handle);
        }

        if (persistentTopmost)
        {
            WindowLifecycleTrace.Log(window, path, $"{phase}:Topmost=true");
            window.Topmost = true;
        }
        else
        {
            WindowLifecycleTrace.Log(window, path, $"{phase}:Topmost-flash");
            window.Topmost = true;
            window.Topmost = false;
        }

        window.Activate();
        window.Focus();
        WindowLifecycleTrace.Log(window, path, $"{phase}:after");
    }

    public static bool TryBringToForeground(Window window, string path, string phase, bool persistentTopmost)
    {
        BringToForeground(window, path, phase, persistentTopmost);
        return new WindowInteropHelper(window).Handle != nint.Zero;
    }
}
