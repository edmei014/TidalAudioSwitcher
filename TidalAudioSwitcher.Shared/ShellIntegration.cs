using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TidalAudioSwitcher.Shared;

[SupportedOSPlatform("windows")]
public static class ShellIntegration
{
    private static readonly Guid PkeyAppUserModelFmtId = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    private static readonly Guid IidIPropertyStore = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");

    private const uint PidRelaunchCommand = 2;
    private const uint PidRelaunchIconResource = 3;
    private const uint PidRelaunchDisplayNameResource = 4;
    private const uint PidAppUserModelId = 5;

    private const ushort VtLpWStr = 31;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(
        IntPtr hwnd,
        [In] ref Guid riid,
        out IPropertyStore propertyStore);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);

    public static void SetAppUserModelId()
    {
        _ = SetCurrentProcessExplicitAppUserModelID(ApplicationEntryConstants.AppUserModelId);
    }

    /// <summary>
    /// Binds the HWND to the public launcher identity so the taskbar/pin uses
    /// TidalAudioSwitcher.exe (icon + relaunch target) instead of TidalAudioSwitcher.UI.exe.
    /// Must run after HWND creation (e.g. SourceInitialized) and before the shell
    /// fully registers the taskbar button when possible.
    /// </summary>
    public static bool ApplyWindowTaskbarIdentity(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var launcherPath = ApplicationPaths.ResolveLauncherExe();
        if (string.IsNullOrEmpty(launcherPath))
        {
            // Still stamp the AUMID so launcher/UI group together.
            return TrySetWindowProperties(hwnd, ApplicationEntryConstants.AppUserModelId, null, null, null);
        }

        var iconResource = $"{launcherPath},0";
        return TrySetWindowProperties(
            hwnd,
            ApplicationEntryConstants.AppUserModelId,
            launcherPath,
            iconResource,
            ApplicationEntryConstants.DisplayName);
    }

    private static bool TrySetWindowProperties(
        IntPtr hwnd,
        string appId,
        string? relaunchCommand,
        string? relaunchIconResource,
        string? displayName)
    {
        var iid = IidIPropertyStore;
        var hr = SHGetPropertyStoreForWindow(hwnd, ref iid, out var store);
        if (hr != 0 || store is null)
        {
            return false;
        }

        try
        {
            SetStringProperty(store, PidAppUserModelId, appId);

            if (!string.IsNullOrEmpty(relaunchCommand))
            {
                SetStringProperty(store, PidRelaunchCommand, relaunchCommand);
            }

            if (!string.IsNullOrEmpty(relaunchIconResource))
            {
                SetStringProperty(store, PidRelaunchIconResource, relaunchIconResource);
            }

            if (!string.IsNullOrEmpty(displayName))
            {
                SetStringProperty(store, PidRelaunchDisplayNameResource, displayName);
            }

            store.Commit();
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }

    private static void SetStringProperty(IPropertyStore store, uint pid, string value)
    {
        var key = new PropertyKey { fmtid = PkeyAppUserModelFmtId, pid = pid };
        var pv = new PropVariant
        {
            vt = VtLpWStr,
            pointerValue = Marshal.StringToCoTaskMemUni(value)
        };

        try
        {
            store.SetValue(ref key, ref pv);
        }
        finally
        {
            _ = PropVariantClear(ref pv);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pointerValue;
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        void GetCount(out uint cProps);

        void GetAt(uint iProp, out PropertyKey pkey);

        void GetValue(ref PropertyKey key, out PropVariant pv);

        void SetValue(ref PropertyKey key, ref PropVariant pv);

        void Commit();
    }
}
