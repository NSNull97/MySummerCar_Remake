using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DisallowMultipleComponent]
    public sealed class AssemblyMountHandoffTarget : MonoBehaviour, IMountHandoffTarget
    {
        [SerializeField]
        private VehicleAssemblyController controller;

        [SerializeField]
        private MountPointAuthoring mountPoint;

        private string prompt = "Установить деталь";

        public string HandoffPrompt => prompt;

        public void Configure(VehicleAssemblyController assemblyController, MountPointAuthoring mount)
        {
            controller = assemblyController;
            mountPoint = mount;
        }

        public bool CanAccept(IPickupTarget pickupTarget, in InteractionContext context)
        {
            if (controller == null || mountPoint == null)
            {
                prompt = "Точка установки не настроена";
                return false;
            }

            PartInstance part = controller.ResolvePart(pickupTarget);
            AssemblyOperationResult result = controller.EvaluateInstall(part, mountPoint);
            prompt = result.Message;
            if (!result.Succeeded)
            {
                return false;
            }

            AssemblyMountCandidate best = controller.FindBestMount(part);
            bool isDeterministicWinner = best.IsValid && best.Mount.Authoring == mountPoint;
            if (!isDeterministicWinner)
            {
                prompt = "Есть более близкая совместимая точка";
            }

            return isDeterministicWinner;
        }

        public void Accept(IPickupTarget pickupTarget, in InteractionContext context)
        {
            if (controller == null || mountPoint == null)
            {
                return;
            }

            AssemblyOperationResult result = controller.TryInstall(pickupTarget, mountPoint);
            prompt = result.Message;
        }
    }
}
