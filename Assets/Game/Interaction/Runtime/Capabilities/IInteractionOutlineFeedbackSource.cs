namespace MSC.Interaction.Capabilities
{
    /// <summary>
    /// Presentation-only feedback for the currently aimed interaction target.
    /// Gameplay remains authoritative in the target; the outline only explains
    /// whether the held tool can operate it and whether work is complete.
    /// </summary>
    public enum InteractionOutlineFeedback
    {
        Default = 0,
        Invalid = 1,
        Complete = 2,
        Loose = 3,
        Partial = 4,
    }

    /// <summary>
    /// Shared project layer used by both bolt and nut interaction markers.
    /// A selected wrench queries only this layer so bodywork and installed
    /// parts cannot hide fasteners from the tool ray.
    /// </summary>
    public static class FastenerToolRaycastLayer
    {
        public const string Name = "bolt-gayka-only";
    }

    public interface IInteractionOutlineFeedbackSource
    {
        InteractionOutlineFeedback GetOutlineFeedback(
            IHeldToolIdentity heldTool);
    }
}
