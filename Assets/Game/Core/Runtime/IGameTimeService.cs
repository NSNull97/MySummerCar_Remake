namespace MSC.Core.Time
{
    /// <summary>
    /// Provides simulation time in game seconds. The concrete clock is owned by a later gameplay milestone.
    /// </summary>
    public interface IGameTimeService
    {
        double CurrentGameTimeSeconds { get; }
    }
}
