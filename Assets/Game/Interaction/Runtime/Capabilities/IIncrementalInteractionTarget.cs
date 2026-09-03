namespace MSC.Interaction.Capabilities
{
    public enum InteractionScrollDirection
    {
        Negative = -1,
        Positive = 1,
    }

    /// <summary>
    /// Explicit capability for controls adjusted by a signed input increment,
    /// such as a mouse-wheel driven knob. It is intentionally separate from
    /// one-shot context interaction.
    /// </summary>
    public interface IIncrementalInteractionTarget
    {
        string AdjustmentPrompt { get; }

        bool CanAdjust(in InteractionContext context);

        bool TryAdjust(
            in InteractionContext context,
            float signedNotches);
    }

    /// <summary>
    /// Optional read-only directional contract used by contextual UI. It keeps
    /// availability queries separate from the mutating scroll operation.
    /// </summary>
    public interface IDirectionalIncrementalInteractionTarget :
        IIncrementalInteractionTarget
    {
        bool CanAdjust(
            in InteractionContext context,
            InteractionScrollDirection direction);

        string GetAdjustmentPrompt(InteractionScrollDirection direction);
    }
}
