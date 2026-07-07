using System.Runtime.InteropServices;

namespace TidalAudioSwitcher.Shared;

public static class ShellIntegration
{
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);

    public static void SetAppUserModelId()
    {
        _ = SetCurrentProcessExplicitAppUserModelID(ApplicationEntryConstants.AppUserModelId);
    }
}
