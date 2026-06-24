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
            // UI started directly without launcher — no waiter registered.
        }
    }
}
