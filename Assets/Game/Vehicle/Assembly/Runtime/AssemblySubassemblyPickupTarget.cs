using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// An explicitly authored pickup surface for a loose subassembly. Installed
    /// children keep their own removal action, but LMB carries the outer loose
    /// owner. Never traverses arbitrary Transform parents or lifts the chassis.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblySubassemblyPickupTarget : MonoBehaviour, IPickupTarget,
        IInteractionOutlineVisibility
    {
        [SerializeField] private VehicleAssemblyController controller;
        [SerializeField] private PartInstance surfacePart;
        private PhysicsPickupTarget carriedPickup;
        public VehicleAssemblyController Controller => controller;
        public PartInstance SurfacePart => surfacePart;

        // Carrying the outer engine is not permission to advertise this
        // installed child's removal. Keep pickup reachable without outlining
        // secured or enclosed internals as if they could be taken separately.
        public bool ShouldShowOutline => surfacePart != null &&
            (!surfacePart.IsInstalled || controller != null &&
                controller.EvaluateRemoval(surfacePart).Succeeded);

        private PhysicsPickupTarget Pickup => carriedPickup != null
            ? carriedPickup : ResolveLooseOwner()?.PickupTarget;

        public string PickupPrompt => Pickup?.PickupPrompt ?? string.Empty;
        public Rigidbody Body => Pickup?.Body;
        public StableEntityId StableId => Pickup != null ? Pickup.StableId : default;

        public void Configure(VehicleAssemblyController assembly, PartInstance part)
        {
            controller = assembly;
            surfacePart = part;
        }

        public bool CanPickup(in InteractionContext context)
        {
            PhysicsPickupTarget pickup = Pickup;
            return carriedPickup == null && pickup != null && pickup.CanPickup(context);
        }

        public void NotifyPickedUp(in InteractionContext context)
        {
            // Keep the exact pickup delegate until release, including handoff
            // transitions that reparent the entire assembly while it is held.
            carriedPickup = ResolveLooseOwner()?.PickupTarget;
            carriedPickup?.NotifyPickedUp(context);
        }

        public void NotifyReleased(PickupReleaseReason reason)
        {
            PhysicsPickupTarget released = carriedPickup;
            carriedPickup = null;
            released?.NotifyReleased(reason);
        }

        private PartInstance ResolveLooseOwner()
        {
            if (controller == null || surfacePart == null)
            {
                return null;
            }

            PartInstance candidate = surfacePart;
            // Bound traversal even if malformed authoring creates a cycle.
            for (int depth = 0; depth < controller.AllRuntimeParts.Length; depth++)
            {
                if (candidate == null || candidate.IsAssemblyRoot)
                {
                    return null;
                }

                if (!candidate.IsInstalled)
                {
                    return candidate;
                }

                MountPointRuntime mountedAt = controller.Graph.FindMountForPart(candidate);
                AssemblyOwnedMountAuthoring owner = mountedAt?.Authoring != null
                    ? mountedAt.Authoring.GetComponent<AssemblyOwnedMountAuthoring>() : null;
                if (owner == null || owner.OwnerPart == candidate)
                {
                    return null;
                }

                candidate = owner.OwnerPart;
            }

            return null;
        }
    }
}
