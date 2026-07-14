using MSC.Core.Identity;
using UnityEngine;

namespace MSC.Interaction.Capabilities
{
    public interface IPickupTarget
    {
        string PickupPrompt { get; }

        Rigidbody Body { get; }

        StableEntityId StableId { get; }

        bool CanPickup(in InteractionContext context);

        void NotifyPickedUp(in InteractionContext context);

        void NotifyReleased(PickupReleaseReason reason);
    }
}
