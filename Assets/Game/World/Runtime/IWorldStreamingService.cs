namespace MSC.World
{
    /// <summary>
    /// Reports whether world-cell load or unload work is in progress.
    /// </summary>
    public interface IWorldStreamingService
    {
        bool IsStreaming { get; }
    }
}
