using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DisallowMultipleComponent]
    public sealed class AssemblyInstalledPartInteractionTarget : MonoBehaviour, IContextInteractionTarget
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
            return controller != null && part != null && part.IsInstalled && !part.IsAssemblyRoot;
        }

        public void Interact(in InteractionContext context)
        {
            if (CanInteract(context))
            {
                controller.TryRemove(part);
            }
        }
    }
}
