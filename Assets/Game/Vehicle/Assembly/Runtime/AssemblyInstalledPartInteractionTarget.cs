using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DisallowMultipleComponent]
    public sealed class AssemblyInstalledPartInteractionTarget : MonoBehaviour,
        IContextInteractionTarget,
        ISecondaryInteractionOnlyTarget,
        IRemovalInteractionTarget,
        IParentColliderOcclusionBypass,
        IRaycastOriginOverlapTarget
    {
        [SerializeField]
        private VehicleAssemblyController controller;

        [SerializeField]
        private PartInstance part;

        public string InteractionPrompt
        {
            get
            {
                if (controller == null || part == null)
                {
                    return "Сборка не настроена";
                }

                return controller.EvaluateRemoval(part).Message;
            }
        }

        public void Configure(VehicleAssemblyController assemblyController, PartInstance partInstance)
        {
            controller = assemblyController;
            part = partInstance;
        }

        public bool CanInteract(in InteractionContext context)
        {
            if (controller == null || part == null ||
                !part.IsInstalled || part.IsAssemblyRoot)
            {
                return false;
            }

            // Keep the proxy queryable for nested mount handoff, but expose
            // the removal action only when the same authoritative evaluation
            // used by dispatch says it can succeed right now. A blocked,
            // obstructed or secured part must not advertise a dead action.
            return controller.EvaluateRemoval(part).Succeeded;
        }

        public void Interact(in InteractionContext context)
        {
            if (CanInteract(context))
            {
                controller.TryRemove(part);
            }
        }

        public bool CanBypassParentCollider(
            InteractionTargetHost parentHost)
        {
            if (controller == null || part == null || parentHost == null)
            {
                return false;
            }

            // The ray may pass only through a registered ancestor belonging to
            // the same vehicle. Unrelated walls and world geometry remain
            // authoritative occluders.
            return parentHost.GetComponentInParent<
                       VehicleAssemblyController>() == controller &&
                   part.transform.IsChildOf(parentHost.transform);
        }
    }
}
