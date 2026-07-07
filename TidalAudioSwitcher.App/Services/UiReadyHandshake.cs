namespace TidalAudioSwitcher.App.Services;

internal static class UiReadyHandshake
{
    internal const string EventName = "TidalAudioSwitcher.UiReady";

    public static void SignalReady()
    {
        try
        {
            using var handle = EventWaitHandle.OpenExisting(EventName);
            handle.Set();
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            // Launcher did not register a waiter (should not happen when --launched-by-launcher is set).
        }
    }
}
