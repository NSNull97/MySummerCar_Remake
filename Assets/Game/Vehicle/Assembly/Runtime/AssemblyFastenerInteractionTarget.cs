using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DisallowMultipleComponent]
    public sealed class AssemblyFastenerInteractionTarget : MonoBehaviour, IToolActivationTarget
    {
        [SerializeField]
        private VehicleAssemblyController controller;

        [SerializeField]
        private string mountId = string.Empty;

        [SerializeField]
        private string fastenerDefinitionId = string.Empty;

        [SerializeField]
        private ToolDefinition tool;

        [SerializeField]
        private bool automaticReverseAtLimits = true;

        private bool loosening;

        public string ToolPrompt
        {
            get
            {
                if (!TryGetFastener(out FastenerInstance fastener))
                {
                    return "Крепёж недоступен";
                }

                string verb = loosening ? "Ослабить" : "Затянуть";
                return $"{verb} {fastener.Definition.DisplayName} " +
                    $"({fastener.Stage}/{fastener.Definition.MaximumStage}, {tool?.DisplayName ?? "нет инструмента"})";
            }
        }

        public void Configure(
            VehicleAssemblyController assemblyController,
            string runtimeMountId,
            string fastenerId,
            ToolDefinition activeTool,
            bool reverseAtLimits)
        {
            controller = assemblyController;
            mountId = runtimeMountId ?? string.Empty;
            fastenerDefinitionId = fastenerId ?? string.Empty;
            tool = activeTool;
            automaticReverseAtLimits = reverseAtLimits;
            loosening = false;
        }

        public bool CanActivateTool(in InteractionContext context)
        {
            return TryGetFastener(out FastenerInstance fastener) && fastener.IsInserted;
        }

        public void ActivateTool(in InteractionContext context)
        {
            if (!TryGetFastener(out FastenerInstance fastener))
            {
                return;
            }

            if (automaticReverseAtLimits)
            {
                if (fastener.Stage >= fastener.Definition.MaximumStage)
                {
                    loosening = true;
                }
                else if (fastener.Stage <= 0)
                {
                    loosening = false;
                }
            }

            bool tighten = !loosening;
            bool clockwiseTightens =
                fastener.Definition.TighteningDirection == FastenerDirection.ClockwiseToTighten;
            FastenerRotationDirection rotationDirection = tighten == clockwiseTightens
                ? FastenerRotationDirection.Clockwise
                : FastenerRotationDirection.CounterClockwise;
            controller.TryTurnFastener(
                mountId,
                fastenerDefinitionId,
                tool,
                rotationDirection);
        }

        private bool TryGetFastener(out FastenerInstance fastener)
        {
            fastener = null;
            if (controller == null ||
                !controller.Graph.TryGetMount(mountId, out MountPointRuntime mount) ||
                !mount.IsOccupied)
            {
                return false;
            }

            return mount.TryGetFastener(fastenerDefinitionId, out fastener);
        }
    }
}
