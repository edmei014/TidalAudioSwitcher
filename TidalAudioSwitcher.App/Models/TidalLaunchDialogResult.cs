namespace TidalAudioSwitcher.App.Models;

public enum TidalLaunchDialogResult
{
    Cancel,
    AutoStart,
    LaunchLauncher
}

public enum TidalLaunchOutcome
{
    Success,
    TidalAlreadyRunningNeedsClose,
    TidalNotFound,
    Timeout,
    LauncherStarted
}
