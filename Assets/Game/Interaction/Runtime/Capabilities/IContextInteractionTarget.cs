namespace MSC.Interaction.Capabilities
{
    /// <summary>
    /// Optional presentation metadata for a capability host. The interaction
    /// action remains context-sensitive and is resolved by
    /// <see cref="MSC.Interaction.Query.InteractionCandidate.GetPrompt"/>; this boundary only
    /// supplies the separate target title used by the HUD.
    /// </summary>
    public interface IInteractionDisplayTarget
    {
        string InteractionDisplayName { get; }
    }

    /// <summary>
    /// Optional stable key for localizing a target title. The raw display name
    /// remains a compatibility fallback for content without a catalog entry.
    /// </summary>
    public interface IInteractionLocalizationTarget
    {
        string InteractionLocalizationKey { get; }
    }

    public interface IContextInteractionTarget
    {
        string InteractionPrompt { get; }

        bool CanInteract(in InteractionContext context);

        void Interact(in InteractionContext context);
    }

    /// <summary>
    /// Marker for context targets that are owned exclusively by the primary
    /// world-interaction action and must not fall through to tool/use input.
    /// </summary>
    public interface IPrimaryInteractionOnlyTarget
    {
    }

    /// <summary>
    /// Marks a one-shot world action owned by secondary interaction. It is
    /// intentionally separate from held-object throw and tool/use input.
    /// </summary>
    public interface ISecondaryInteractionOnlyTarget
    {
    }

    /// <summary>
    /// Marks an available world action that detaches or removes an installed
    /// object. Presentation uses this semantic capability for the removal
    /// reticle instead of inspecting localized prompt text.
    /// </summary>
    public interface IRemovalInteractionTarget
    {
    }
}
