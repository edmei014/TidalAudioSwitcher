namespace TidalAudioSwitcher.App.Models;

public sealed class TidalCdpUserException : Exception
{
    public TidalCdpUserException(string userMessage, Exception? innerException = null)
        : base(userMessage, innerException)
    {
    }
}
