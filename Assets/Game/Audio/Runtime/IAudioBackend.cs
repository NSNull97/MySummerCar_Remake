namespace MSC.Audio
{
    /// <summary>
    /// Boundary for the temporary Unity Audio backend and a future official Wwise integration.
    /// </summary>
    public interface IAudioBackend
    {
        bool IsReady { get; }
    }
}
