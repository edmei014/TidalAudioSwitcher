namespace TidalAudioSwitcher.Launcher;

internal static class UiReadyHandshake
{
    internal const string EventName = "TidalAudioSwitcher.UiReady";
    internal static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    public static EventWaitHandle CreateForWait()
    {
        var handle = new EventWaitHandle(false, EventResetMode.ManualReset, EventName, out _);
        handle.Reset();
        return handle;
    }
}
