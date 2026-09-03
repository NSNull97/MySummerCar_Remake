namespace MSC.Interaction.Capabilities
{
    /// <summary>
    /// Identifies which world-interaction button is being held. The input
    /// owner supplies the direction; targets never read input directly.
    /// </summary>
    public enum ContinuousContextInteractionDirection
    {
        Primary = 0,
        Secondary = 1,
    }

    /// <summary>
    /// Explicit press/hold/release capability for a world interaction target.
    /// Primary is LMB and secondary is RMB in the current donor control map.
    /// </summary>
    public interface IContinuousContextInteractionTarget
    {
        string InteractionPrompt { get; }

        bool UsesDirectionalHold { get; }

        bool CanBeginContinuousInteraction(
            in InteractionContext context,
            ContinuousContextInteractionDirection direction);

        void BeginContinuousInteraction(
            in InteractionContext context,
            ContinuousContextInteractionDirection direction);

        bool ContinueContinuousInteraction(float deltaTime);

        void EndContinuousInteraction();
    }
}
