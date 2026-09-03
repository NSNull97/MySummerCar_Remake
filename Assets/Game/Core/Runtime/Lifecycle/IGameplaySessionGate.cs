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

    /// <summary>
    /// Optional asynchronous preparation boundary used by front ends that must
    /// remain lightweight until the player explicitly starts a session.
    /// Implementations keep world-scene loading separate from service/UI boot.
    /// </summary>
    public interface IGameplaySessionPreparationGate : IGameplaySessionGate
    {
        bool IsGameplayPreparationRunning { get; }

        string LastGameplayPreparationFailure { get; }

        bool TryBeginGameplayPreparation(out string failure);
    }
}
