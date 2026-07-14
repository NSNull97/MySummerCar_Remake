namespace MSC.Interaction.Capabilities
{
    /// <summary>
    /// Narrow boundary for a future assembly mount. Milestone 4 does not own mount compatibility or assembly state.
    /// </summary>
    public interface IMountHandoffTarget
    {
        string HandoffPrompt { get; }

        bool CanAccept(IPickupTarget pickupTarget, in InteractionContext context);

        void Accept(IPickupTarget pickupTarget, in InteractionContext context);
    }
}
