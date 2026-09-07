using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class SatsumaWiringConnectorInteractionTarget : MonoBehaviour,
        IToolActivationTarget,
        IHeldToolActivationTarget,
        IInteractionDisplayTarget,
        IInteractionOutlineFeedbackSource,
        IRequiresCarriedObjectRaycastTarget,
        ICarriedObjectRaycastFilter,
        IParentColliderOcclusionBypass,
        IRaycastOriginOverlapTarget
    {
        [SerializeField] private SatsumaElectricalSystem electricalSystem;
        [SerializeField] private SatsumaElectricalConnection connection;
        [SerializeField, Range(0, 1)] private int endpoint;
        [SerializeField] private string donorPrompt = "WIRING";
        [SerializeField] private string[] requiredPartDefinitionIds =
            Array.Empty<string>();
        [SerializeField] private bool matchAnyRequiredPart;
        [SerializeField] private bool requiresFastener;
        [SerializeField] private SatsumaElectricalFastener requiredFastener;
        [SerializeField] private int requiredFastenerStage;
        [SerializeField] private string requiredMountId = string.Empty;
        [SerializeField] private string requiredMountFastenerId = string.Empty;
        [SerializeField] private int maximumMountFastenerStageExclusive;
        [SerializeField] private bool logWiringActivations;

        private Collider interactionCollider;

        public SatsumaElectricalConnection Connection => connection;
        public int Endpoint => endpoint;
        public bool ArePartRequirementsMet => electricalSystem != null &&
            electricalSystem.ArePartRequirementsMet(
                requiredPartDefinitionIds,
                matchAnyRequiredPart) &&
            (!requiresFastener || electricalSystem.GetFastenerStage(
                requiredFastener) >= requiredFastenerStage) &&
            (string.IsNullOrEmpty(requiredMountId) ||
                electricalSystem.IsMountFastenerBelowStage(requiredMountId,
                    requiredMountFastenerId, maximumMountFastenerStageExclusive));
        public bool IsEndpointAvailable => isActiveAndEnabled && ArePartRequirementsMet &&
            !electricalSystem.IsConnectionInstalled(connection) &&
            !electricalSystem.IsEndpointArmed(connection, endpoint);

        public string InteractionDisplayName => string.IsNullOrWhiteSpace(donorPrompt)
            ? ConnectionName(connection)
            : donorPrompt;

        public string ToolPrompt => electricalSystem != null &&
            electricalSystem.IsEndpointArmed(connection, 1 - endpoint)
            ? "СОЕДИНИТЬ ПРОВОД"
            : "ВЫБРАТЬ КОНЕЦ ПРОВОДА";

        public void Configure(
            SatsumaElectricalSystem configuredSystem,
            SatsumaElectricalConnection configuredConnection,
            int configuredEndpoint,
            string configuredDonorPrompt,
            string[] configuredRequiredPartDefinitionIds = null,
            bool configuredMatchAnyRequiredPart = false,
            bool configuredRequiresFastener = false,
            SatsumaElectricalFastener configuredRequiredFastener = default,
            int configuredRequiredFastenerStage = 0)
        {
            electricalSystem = configuredSystem != null
                ? configuredSystem
                : throw new ArgumentNullException(nameof(configuredSystem));
            connection = configuredConnection;
            endpoint = Mathf.Clamp(configuredEndpoint, 0, 1);
            donorPrompt = configuredDonorPrompt ?? string.Empty;
            requiredPartDefinitionIds = configuredRequiredPartDefinitionIds != null
                ? (string[])configuredRequiredPartDefinitionIds.Clone()
                : Array.Empty<string>();
            matchAnyRequiredPart = configuredMatchAnyRequiredPart;
            requiresFastener = configuredRequiresFastener;
            requiredFastener = configuredRequiredFastener;
            requiredFastenerStage = Mathf.Clamp(
                configuredRequiredFastenerStage,
                0,
                SatsumaElectricalSystem.FastenerMaximumStage);
            interactionCollider = GetComponent<Collider>();
            electricalSystem.RegisterEndpoint(this);
            RefreshAvailability();
        }

        public void ConfigureMountFastenerGate(
            string mountId, string fastenerDefinitionId, int maximumStageExclusive)
        {
            requiredMountId = mountId ?? string.Empty;
            requiredMountFastenerId = fastenerDefinitionId ?? string.Empty;
            maximumMountFastenerStageExclusive = maximumStageExclusive;
            // This gate is the mounting screw, not the separate cable fastener.
            requiresFastener = false;
            RefreshAvailability();
        }

        public bool CanSelectForCarriedObject(
            IPickupTarget pickupTarget, in InteractionContext context)
        {
            InteractionTargetHost host = pickupTarget?.Body != null
                ? pickupTarget.Body.GetComponentInParent<InteractionTargetHost>()
                : null;
            return IsEndpointAvailable && host != null &&
                host.TryGetCapability(out IHeldToolIdentity tool) &&
                TryGetReachableWiringToolPosition(tool, out _);
        }

        public bool CanActivateTool(in InteractionContext context) => false;

        public bool CanBypassParentCollider(InteractionTargetHost parentHost) =>
            IsEndpointAvailable && parentHost != null &&
            parentHost.GetComponentInParent<SatsumaElectricalSystem>() == electricalSystem &&
            transform.IsChildOf(parentHost.transform);

        public void ActivateTool(in InteractionContext context)
        {
        }

        public bool CanActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context) => IsEndpointAvailable &&
            TryGetReachableWiringToolPosition(tool, out Vector3 toolPosition) &&
            electricalSystem.CanActivateCluster(toolPosition);

        public void ActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context)
        {
            if (CanActivateHeldTool(tool, context) &&
                TryGetWiringToolPosition(tool, out Vector3 toolPosition))
            {
                bool firstWasArmed = electricalSystem.IsEndpointArmed(connection, 0);
                bool secondWasArmed = electricalSystem.IsEndpointArmed(connection, 1);
                int completed = electricalSystem.ActivateCluster(toolPosition);
                if (logWiringActivations)
                    Debug.Log($"SATSUMA_WIRING_USE connection={connection} endpoint={endpoint} " +
                        $"spool={toolPosition:F4} selected={transform.position:F4} " +
                        $"pendingBefore={firstWasArmed}/{secondWasArmed} completed={completed} " +
                        $"installed={electricalSystem.IsConnectionInstalled(connection)}", this);
                RefreshAvailability();
            }
        }

        private bool TryGetReachableWiringToolPosition(IHeldToolIdentity tool, out Vector3 position)
        {
            // The donor enables its wiring prompt only inside the spool's reach.
            // Filter before ray ranking, so an unreachable, nearer-camera endpoint
            // cannot mask another endpoint that the carried spool actually reaches.
            return TryGetWiringToolPosition(tool, out position) &&
                (transform.position - position).sqrMagnitude <=
                    SatsumaElectricalSystem.DonorEndpointToleranceMeters *
                    SatsumaElectricalSystem.DonorEndpointToleranceMeters;
        }

        private static bool TryGetWiringToolPosition(IHeldToolIdentity tool, out Vector3 position)
        {
            // Both real WorldItemInstance and its explicit held-tool capability
            // are components on the carried spool. A selected connector is not
            // a substitute for the original WiringTool distance reference.
            if (ToolMatches(tool) && tool is Component component && component != null)
            {
                position = component.transform.position;
                return true;
            }
            position = default;
            return false;
        }

        public InteractionOutlineFeedback GetOutlineFeedback(
            IHeldToolIdentity heldTool) => ToolMatches(heldTool)
            ? InteractionOutlineFeedback.Default
            : InteractionOutlineFeedback.Invalid;

        private void OnEnable()
        {
            interactionCollider = GetComponent<Collider>();
            electricalSystem?.RegisterEndpoint(this);
            RefreshAvailability();
        }

        private void Update() => RefreshAvailability();

        private void RefreshAvailability()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }

            if (interactionCollider != null)
            {
                interactionCollider.enabled = IsEndpointAvailable;
            }
        }

        private static bool ToolMatches(IHeldToolIdentity tool) =>
            tool != null &&
            string.Equals(tool.ToolType, "Wiring", StringComparison.Ordinal) &&
            string.Equals(tool.ToolVariant, "mess", StringComparison.Ordinal);

        private static string ConnectionName(SatsumaElectricalConnection value) =>
            value switch
            {
                SatsumaElectricalConnection.BatteryHarness => "Жгут аккумулятора",
                SatsumaElectricalConnection.GroundBattery => "Масса аккумулятора",
                SatsumaElectricalConnection.Starter => "Плюс стартера",
                SatsumaElectricalConnection.Ignition => "Замок зажигания",
                SatsumaElectricalConnection.SwitchLights => "Переключатели света",
                _ => "Проводка Satsuma: " + value,
            };
    }
}
