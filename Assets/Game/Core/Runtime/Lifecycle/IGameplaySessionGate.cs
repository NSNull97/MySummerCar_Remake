namespace MSC.Core.Lifecycle
{
    /// <summary>
    /// Explicit boundary between a prepared Bootstrap world and an active
    /// gameplay session. Front-end UI may wait for preparation without
    /// exposing the gameplay camera or starting simulation.
    /// </summary>
    public interface IGameplaySessionGate
    {
        bool IsGameplayPrepared { get; }

        bool IsGameplayActive { get; }

        bool TryActivateGameplay(out string failure);
    }
}
