namespace MSC.Interaction.Capabilities
{
    /// <summary>
    /// Marks a target that is meaningful to the central interaction ray only
    /// while the player carries an object. Empty assembly sockets use this so
    /// they cannot hide already installed parts from ordinary interaction.
    /// </summary>
    public interface IRequiresCarriedObjectRaycastTarget
    {
    }

    /// <summary>
    /// Lets a carried-object-only target reject an incompatible held object
    /// before raycast priority is resolved. This is intentionally narrower
    /// than IMountHandoffTarget.CanAccept: compatibility filters candidate
    /// selection, while installation prerequisites still produce a useful
    /// prompt on the correctly selected target.
    /// </summary>
    public interface ICarriedObjectRaycastFilter
    {
        bool CanSelectForCarriedObject(
            IPickupTarget pickupTarget,
            in InteractionContext context);
    }
}
