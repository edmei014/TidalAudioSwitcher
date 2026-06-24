using System.Runtime.InteropServices;
using System.Windows;

namespace TidalAudioSwitcher.App.Services;

internal static class WindowPlacementHelper
{
    private const uint MonitorDefaultToNull = 0;

    public static bool IsPositionVisible(double left, double top, double width, double height)
    {
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        var rect = new NativeRect
        {
            Left = (int)Math.Floor(left),
            Top = (int)Math.Floor(top),
            Right = (int)Math.Ceiling(left + width),
            Bottom = (int)Math.Ceiling(top + height)
        };

        return MonitorFromRect(ref rect, MonitorDefaultToNull) != IntPtr.Zero;
    }

    public static void CenterOnScreen(Window window)
    {
        window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromRect(ref NativeRect lprc, uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
