namespace MSC.Interaction.Capabilities
{
    public enum HeldActivationReleaseMode
    {
        None = 0,
        Release = 1,
        Throw = 2,
    }

    /// <summary>
    /// Explicit capability for an action performed by an item while it is held.
    /// Item implementations remain authoritative for availability and effects.
    /// </summary>
    public interface IHeldActivationTarget
    {
        bool CanActivateHeld(in InteractionContext context);

        void ActivateHeld(in InteractionContext context);
    }

    /// <summary>
    /// Explicit press/hold/release capability for an action performed by an
    /// already carried item. Real-time duration is supplied by the input owner;
    /// implementations must not read input or depend on an animation frame.
    /// </summary>
    public interface IContinuousHeldActivationTarget
    {
        bool IsContinuousHeldActivationActive { get; }

        bool CanBeginContinuousHeldActivation(
            in InteractionContext context);

        void BeginContinuousHeldActivation(
            in InteractionContext context);

        /// <summary>
        /// Advances the held action. Returns false when the action can no
        /// longer continue, for example after a drink container becomes empty.
        /// </summary>
        bool ContinueContinuousHeldActivation(
            float unscaledDeltaTime);

        void EndContinuousHeldActivation();
    }

    /// <summary>
    /// Explicit opt-in for a world target that may be picked up by the use
    /// action and immediately enter its continuous held activation. Ordinary
    /// pickup targets do not receive this behavior and remain LMB-only.
    /// </summary>
    public interface IContinuousActivationPickupTarget
    {
        bool CanPickupForContinuousActivation(
            in InteractionContext context);
    }

    /// <summary>
    /// Optional one-shot outcome produced by a held activation. It lets an
    /// item request release without coupling the item domain to the player or
    /// carry implementation.
    /// </summary>
    public interface IHeldActivationReleaseRequest
    {
        HeldActivationReleaseMode ConsumeHeldActivationReleaseRequest();
    }
}
