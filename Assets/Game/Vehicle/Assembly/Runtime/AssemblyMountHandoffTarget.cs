using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DisallowMultipleComponent]
    public sealed class AssemblyMountHandoffTarget : MonoBehaviour,
        IMountHandoffTarget,
        IMountHandoffPreReleaseTarget,
        IRequiresCarriedObjectRaycastTarget,
        ICarriedObjectRaycastFilter,
        IParentColliderOcclusionBypass
    {
        [SerializeField] private VehicleAssemblyController controller;
        [SerializeField] private MountPointAuthoring mountPoint;

        private string prompt = "Установить деталь";

        public string HandoffPrompt => prompt;

        public void Configure(
            VehicleAssemblyController assemblyController,
            MountPointAuthoring mount)
        {
            controller = assemblyController;
            mountPoint = mount;
        }

        public bool CanAccept(
            IPickupTarget pickupTarget,
            in InteractionContext context)
        {
            if (controller == null || mountPoint == null ||
                mountPoint.GetComponent<AssemblyPhysicalDockingOnly>() != null)
            {
                prompt = "Точка установки не настроена";
                return false;
            }

            PartInstance part = controller.ResolvePart(pickupTarget);
            AssemblyOperationResult result = controller.EvaluateHandoffInstall(
                part,
                mountPoint);
            prompt = result.Succeeded
                ? BuildInstallPrompt(part)
                : result.Message;
            return result.Succeeded;
        }

        public void Accept(
            IPickupTarget pickupTarget,
            in InteractionContext context)
        {
            if (controller == null || mountPoint == null ||
                mountPoint.GetComponent<AssemblyPhysicalDockingOnly>() != null)
            {
                return;
            }

            AssemblyOperationResult result = controller
                .BeginInstallFromHandoff(pickupTarget, mountPoint);
            prompt = result.Message;
        }

        public bool TryPrepareHandoff(
            IPickupTarget pickupTarget,
            in InteractionContext context)
        {
            if (controller == null || mountPoint == null ||
                mountPoint.GetComponent<AssemblyPhysicalDockingOnly>() != null)
            {
                return false;
            }

            AssemblyOperationResult result = controller.TryPrepareHandoffInstall(
                controller.ResolvePart(pickupTarget), mountPoint);
            prompt = result.Message;
            return result.Succeeded;
        }

        public bool CanSelectForCarriedObject(
            IPickupTarget pickupTarget,
            in InteractionContext context)
        {
            if (controller == null || mountPoint == null ||
                mountPoint.GetComponent<AssemblyPhysicalDockingOnly>() != null ||
                mountPoint.Definition == null)
            {
                return false;
            }

            PartInstance part = controller.ResolvePart(pickupTarget);
            return part?.Definition != null &&
                   part.Definition.IsCompatibleWith(mountPoint.Definition);
        }

        public bool CanBypassParentCollider(
            InteractionTargetHost parentHost)
        {
            if (controller == null || mountPoint == null || parentHost == null)
            {
                return false;
            }

            // Mount helpers may be physically behind their own spindle, drum,
            // disc or body-shell collider. Only that registered vehicle
            // ancestor may be transparent; walls and unrelated vehicles still
            // occlude the socket normally.
            return parentHost.GetComponentInParent<
                       VehicleAssemblyController>() == controller &&
                   transform.IsChildOf(parentHost.transform);
        }

        private static string BuildInstallPrompt(PartInstance part) =>
            part?.Definition != null
                ? "Установить: " + part.Definition.DisplayName
                : "Установить деталь";
    }
}
