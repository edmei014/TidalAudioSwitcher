using TidalAudioSwitcher.Shared;

namespace TidalAudioSwitcher.App.Services;

public static class TidalProcessService
{
    public static bool IsTidalRunning() => TidalBootstrap.IsTidalRunning();

    public static string? FindTidalExecutable() => TidalBootstrap.FindTidalExecutable();
}
