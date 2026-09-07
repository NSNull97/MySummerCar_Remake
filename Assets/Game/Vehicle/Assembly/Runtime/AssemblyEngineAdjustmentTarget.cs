using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DisallowMultipleComponent]
    public sealed class AssemblyEngineAdjustmentTarget : MonoBehaviour,
        IDirectionalIncrementalInteractionTarget,
        IDirectionalScrollHeldToolActivationTarget,
        IToolActivationTarget, IInteractionDisplayTarget,
        IInteractionOutlineRendererSource, IInteractionOutlineFeedbackSource,
        IParentColliderOcclusionBypass
    {
        [SerializeField] private AssemblyEngineAdjustmentState state;
        [SerializeField] private Renderer outlineRenderer;
        private Collider interactionCollider;
        private float nextOperationTime = float.NegativeInfinity;

        public AssemblyEngineAdjustmentState State => state;
        public Renderer OutlineRenderer => outlineRenderer;
        public string InteractionDisplayName => state == null ? string.Empty : state.Kind switch
        {
            SatsumaEngineAdjustmentKind.Alternator => "Положение генератора",
            SatsumaEngineAdjustmentKind.Distributor => "Угол зажигания",
            SatsumaEngineAdjustmentKind.CarburetorMixture => "Винт смеси карбюратора",
            SatsumaEngineAdjustmentKind.OilFilter => "Масляный фильтр",
            _ => string.Empty,
        };
        public string AdjustmentPrompt => "Колесо вверх/вниз — регулировать";
        public string ToolPrompt => "Отвёртка: колесо вверх/вниз — регулировать смесь";
        private bool IsMixture => state != null && state.Kind == SatsumaEngineAdjustmentKind.CarburetorMixture;
        private bool Available => isActiveAndEnabled && state != null && state.IsAvailable;

        public void Configure(AssemblyEngineAdjustmentState configuredState, Renderer configuredRenderer)
        {
            state = configuredState != null ? configuredState : throw new ArgumentNullException(nameof(configuredState));
            outlineRenderer = configuredRenderer != null ? configuredRenderer : throw new ArgumentNullException(nameof(configuredRenderer));
            interactionCollider = GetComponent<Collider>();
        }

        public bool CanAdjust(in InteractionContext context) => !IsMixture && Available;
        public bool CanAdjust(in InteractionContext context, InteractionScrollDirection direction) =>
            CanAdjust(context) && state.CanAdjust((float)direction);
        public bool TryAdjust(in InteractionContext context, float signedNotches) =>
            !IsMixture && Operate(signedNotches);

        public string GetAdjustmentPrompt(InteractionScrollDirection direction) =>
            state != null && state.Kind == SatsumaEngineAdjustmentKind.OilFilter
                ? direction == InteractionScrollDirection.Positive ? "ЗАТЯНУТЬ ФИЛЬТР" : "ОСЛАБИТЬ ФИЛЬТР"
                : direction == InteractionScrollDirection.Positive ? "УМЕНЬШИТЬ УГОЛ" : "УВЕЛИЧИТЬ УГОЛ";

        public bool CanActivateHeldTool(IHeldToolIdentity tool, in InteractionContext context,
            InteractionScrollDirection direction) => IsMixture && Available && ToolMatches(tool) &&
            state.CanAdjust((float)direction);
        public bool TryActivateHeldTool(IHeldToolIdentity tool, in InteractionContext context,
            float signedNotches) => IsMixture && ToolMatches(tool) && Operate(signedNotches);
        public string GetHeldToolScrollPrompt(InteractionScrollDirection direction) =>
            direction == InteractionScrollDirection.Positive ? "ОБЕДНИТЬ СМЕСЬ" : "ОБОГАТИТЬ СМЕСЬ";

        // No magic F fastening and no untyped tool fall-through.
        public bool CanActivateTool(in InteractionContext context) => false;
        public void ActivateTool(in InteractionContext context) { }

        public Renderer ResolveOutlineRenderer() => Available && outlineRenderer != null &&
            outlineRenderer.enabled ? outlineRenderer : null;
        public InteractionOutlineFeedback GetOutlineFeedback(IHeldToolIdentity tool) =>
            IsMixture && tool != null && !ToolMatches(tool)
                ? InteractionOutlineFeedback.Invalid : InteractionOutlineFeedback.Default;
        public bool CanBypassParentCollider(InteractionTargetHost parentHost) => Available &&
            parentHost != null && state.Part != null && parentHost.transform == state.Part.transform;

        public void RefreshAvailability()
        {
            if (interactionCollider == null) interactionCollider = GetComponent<Collider>();
            if (interactionCollider != null) interactionCollider.enabled = Available;
            // The original tuning screw is present on the carb itself. Only
            // interaction is install-gated; do not hide arbitrary part meshes.
        }

        private bool Operate(float signedNotches)
        {
            if (!Available || !float.IsFinite(signedNotches) || Mathf.Abs(signedNotches) < 0.001f ||
                Time.timeScale <= 0f) return false;
            if (Time.unscaledTime < nextOperationTime) return true;
            if (!state.TryAdjust(signedNotches)) return false;
            // Frozen donor has a 0.1 s alternator wait, 0.01 s distributor wait,
            // and the shared bolting cycle for filter/tool use.
            float cooldown = state.Kind == SatsumaEngineAdjustmentKind.Distributor ? 0.01f : 0.1f;
            nextOperationTime = Time.unscaledTime + cooldown;
            return true;
        }

        private static bool ToolMatches(IHeldToolIdentity tool) => tool != null &&
            string.Equals(tool.ToolType, SatsumaAuxiliaryAssemblyTools.ScrewdriverType, StringComparison.Ordinal) &&
            int.TryParse(tool.ToolVariant, out int variant) && variant == 0;

        private void Update() => RefreshAvailability();
        private void OnDisable()
        {
            nextOperationTime = float.NegativeInfinity;
            if (interactionCollider != null) interactionCollider.enabled = false;
        }
    }
}
