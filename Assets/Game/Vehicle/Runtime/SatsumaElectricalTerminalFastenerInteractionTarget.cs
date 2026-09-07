using System;
using System.Globalization;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class SatsumaElectricalTerminalFastenerInteractionTarget :
        MonoBehaviour,
        IToolActivationTarget,
        IHeldToolActivationTarget,
        IDirectionalScrollHeldToolActivationTarget,
        IInteractionDisplayTarget,
        IInteractionOutlineFeedbackSource,
        IInteractionOutlineRendererSource
    {
        public const int WrenchSizeMillimeters = 8;
        public const float TurnDegrees = 13f;

        [SerializeField] private SatsumaElectricalSystem electricalSystem;
        [SerializeField] private SatsumaElectricalFastener fastener;
        [SerializeField] private Transform fastenerPresentation;
        [SerializeField] private Renderer fastenerRenderer;

        private Collider interactionCollider;
        [SerializeField] private Quaternion baseRotation = Quaternion.identity;
        [SerializeField] private bool hasAuthoredBaseRotation;

        public string InteractionDisplayName => fastener switch
        {
            SatsumaElectricalFastener.BatteryPositiveTerminal =>
                "Плюсовая клемма (8 мм)",
            SatsumaElectricalFastener.BatteryNegativeTerminal =>
                "Минусовая клемма (8 мм)",
            SatsumaElectricalFastener.StarterCable =>
                "Провод стартера (8 мм)",
            _ => "Крепёж электрики (8 мм)",
        };

        public string ToolPrompt =>
            $"Клемма аккумулятора (8 мм): {CurrentStage}/" +
            SatsumaElectricalSystem.TerminalMaximumStage;

        private int CurrentStage => electricalSystem != null
            ? electricalSystem.GetFastenerStage(fastener)
            : 0;

        private bool IsAvailable => electricalSystem != null &&
            electricalSystem.IsFastenerAvailable(fastener);

        public void Configure(
            SatsumaElectricalSystem configuredSystem,
            SatsumaElectricalFastener configuredFastener,
            Transform configuredPresentation,
            Renderer configuredRenderer)
        {
            electricalSystem = configuredSystem != null
                ? configuredSystem
                : throw new ArgumentNullException(nameof(configuredSystem));
            if (!Enum.IsDefined(
                    typeof(SatsumaElectricalFastener),
                    configuredFastener))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(configuredFastener));
            }

            fastener = configuredFastener;
            fastenerPresentation = configuredPresentation != null
                ? configuredPresentation
                : throw new ArgumentNullException(nameof(configuredPresentation));
            fastenerRenderer = configuredRenderer != null
                ? configuredRenderer
                : throw new ArgumentNullException(nameof(configuredRenderer));
            interactionCollider = GetComponent<Collider>();
            baseRotation = fastenerPresentation.localRotation;
            hasAuthoredBaseRotation = true;
            RefreshPresentation();
        }

        public void ConfigureAuthoredBaseRotation(Quaternion rotation)
        {
            baseRotation = rotation.normalized;
            hasAuthoredBaseRotation = true;
            RefreshPresentation();
        }

        public bool CanActivateTool(in InteractionContext context) => false;

        public void ActivateTool(in InteractionContext context)
        {
        }

        public bool CanActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context) => false;

        public void ActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context)
        {
        }

        public bool TryActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context,
            float signedNotches)
        {
            if (!IsAvailable || !ToolMatches(tool) || Time.timeScale <= 0f)
            {
                return false;
            }

            bool changed = electricalSystem.TryTurnFastener(
                fastener,
                signedNotches);
            RefreshPresentation();
            return changed;
        }

        public bool CanActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context,
            InteractionScrollDirection direction)
        {
            if (!IsAvailable || !ToolMatches(tool) || Time.timeScale <= 0f)
            {
                return false;
            }

            return direction == InteractionScrollDirection.Positive
                ? CurrentStage < SatsumaElectricalSystem.TerminalMaximumStage
                : CurrentStage > 0;
        }

        public string GetHeldToolScrollPrompt(
            InteractionScrollDirection direction) =>
            direction == InteractionScrollDirection.Positive
                ? "ЗАТЯНУТЬ КЛЕММУ"
                : "ОСЛАБИТЬ КЛЕММУ";

        public InteractionOutlineFeedback GetOutlineFeedback(
            IHeldToolIdentity heldTool)
        {
            if (!ToolMatches(heldTool))
            {
                return InteractionOutlineFeedback.Invalid;
            }

            return CurrentStage switch
            {
                0 => InteractionOutlineFeedback.Loose,
                SatsumaElectricalSystem.TerminalMaximumStage =>
                    InteractionOutlineFeedback.Complete,
                _ => InteractionOutlineFeedback.Partial,
            };
        }

        public Renderer ResolveOutlineRenderer() =>
            IsAvailable && fastenerRenderer != null && fastenerRenderer.enabled
                ? fastenerRenderer
                : null;

        private void OnEnable() => RefreshPresentation();

        private void Update() => RefreshPresentation();

        private void RefreshPresentation()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }

            bool visible = IsAvailable;
            if (interactionCollider != null)
            {
                interactionCollider.enabled = visible;
            }

            if (fastenerRenderer != null)
            {
                fastenerRenderer.enabled = visible;
            }

            if (fastenerPresentation != null)
            {
                // Compatibility with already-authored prefabs: their marker
                // transform stores the donor rest pose, but older components
                // did not serialize its cached rotation. Capture once, never
                // again from a live, already-turned terminal on re-enable.
                if (!hasAuthoredBaseRotation)
                {
                    baseRotation = fastenerPresentation.localRotation;
                    hasAuthoredBaseRotation = true;
                }
                fastenerPresentation.localRotation = baseRotation *
                    Quaternion.AngleAxis(-TurnDegrees * CurrentStage, Vector3.forward);
            }
        }

        private static bool ToolMatches(IHeldToolIdentity tool) =>
            tool != null &&
            string.Equals(tool.ToolType, "Wrench", StringComparison.Ordinal) &&
            int.TryParse(
                tool.ToolVariant,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int size) &&
            size == WrenchSizeMillimeters;
    }
}
