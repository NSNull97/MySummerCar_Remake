namespace MSC.Core.Lifecycle
{
    /// <summary>
    /// Narrow session UI boundary for suspending gameplay input without knowing
    /// concrete player or vehicle router types.
    /// </summary>
    public interface IGameplayInputGate
    {
        bool IsGameplayInputEnabled { get; }

        void SetGameplayInputEnabled(bool enabled);
    }

    /// <summary>
    /// Presentation-only overlays implement this boundary so menus and captures
    /// can suppress them without object-name or hierarchy lookups.
    /// </summary>
    public interface IUiVisibilityGate
    {
        bool IsUiSuppressed { get; }

        void SetUiSuppressed(bool suppressed);
    }
}
