namespace TidalAudioSwitcher.Shared;

public static class ApplicationEntryConstants
{
    public const string LauncherExeName = "TidalAudioSwitcher.exe";
    public const string UiExeName = "TidalAudioSwitcher.UI.exe";
    public const string LaunchedByLauncherArgument = "--launched-by-launcher";

    /// <summary>
    /// Shared AppUserModelID so launcher and UI group as one taskbar application.
    /// </summary>
    public const string AppUserModelId = "TidalAudioSwitcher";
}
