namespace MSC.Core.Lifecycle
{
    /// <summary>
    /// Explicit teardown hook for services owned by one playable session. The
    /// composition root invokes it before replacing or leaving that session so
    /// static ownership and scene subscriptions are released synchronously.
    /// </summary>
    public interface IGameSessionLifetime
    {
        void EndGameSession();
    }
}
