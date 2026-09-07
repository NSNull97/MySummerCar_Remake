namespace MSC.Interaction.Capabilities
{
    /// <summary>
    /// Optional actual-attempt hook, never a preview query. False consumes
    /// the attempt while keeping the item held; Accept is not called.
    /// Targets without this capability retain the original handoff flow.
    /// </summary>
    public interface IMountHandoffPreReleaseTarget
    {
        bool TryPrepareHandoff(IPickupTarget pickupTarget, in InteractionContext context);
    }
}
