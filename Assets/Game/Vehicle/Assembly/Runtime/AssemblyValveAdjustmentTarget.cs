using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DisallowMultipleComponent]
    public sealed class AssemblyValveAdjustmentTarget : MonoBehaviour,
        IDirectionalScrollHeldToolActivationTarget, IToolActivationTarget, IInteractionDisplayTarget,
        IInteractionOutlineRendererSource, IInteractionOutlineFeedbackSource, IParentColliderOcclusionBypass
    {
        [SerializeField] private AssemblyValveAdjustmentState state;
        [SerializeField] private int valveIndex;
        [SerializeField] private Renderer screwRenderer;
        [SerializeField] private Quaternion screwBaseRotation = Quaternion.identity;
        private Collider hitbox;
        private float nextTurnTime;
        private int appliedRevision = -1;
        public AssemblyValveAdjustmentState State => state;
        public int ValveIndex => valveIndex;
        public Renderer ScrewRenderer => screwRenderer;
        public string InteractionDisplayName => $"Клапан {valveIndex / 2 + 1}: {((valveIndex & 1) == 0 ? "впуск" : "выпуск")}";
        public string ToolPrompt => "Отвёртка: колесо вверх/вниз — зазор клапана";
        private bool Available => isActiveAndEnabled && state != null && state.IsAvailable;

        public void Configure(AssemblyValveAdjustmentState settings, int index, Renderer screw)
        {
            if (settings == null || index < 0 || index >= 8 || screw == null ||
                screw.transform == settings.Part.transform || !screw.transform.IsChildOf(settings.Part.transform))
                throw new ArgumentException("Bind the typed valve setting and its separate part-owned screw renderer.");
            state = settings; valveIndex = index; screwRenderer = screw;
            screwBaseRotation = screw.transform.localRotation;
            hitbox = GetComponent<Collider>(); appliedRevision = -1;
        }
        public bool CanActivateHeldTool(IHeldToolIdentity tool, in InteractionContext context,
            InteractionScrollDirection direction) => Available && ToolMatches(tool) && state.CanAdjust(valveIndex, (float)direction);
        public bool TryActivateHeldTool(IHeldToolIdentity tool, in InteractionContext context, float signedNotches)
        {
            if (!Available || !ToolMatches(tool) || Time.timeScale <= 0f || !state.CanAdjust(valveIndex, signedNotches)) return false;
            if (Time.unscaledTime < nextTurnTime) return true;
            if (!state.TryAdjust(valveIndex, signedNotches)) return false;
            nextTurnTime = Time.unscaledTime + 0.1f;
            ApplyPresentation(); return true;
        }
        public string GetHeldToolScrollPrompt(InteractionScrollDirection direction) =>
            direction == InteractionScrollDirection.Positive ? "УМЕНЬШИТЬ ЗАЗОР" : "УВЕЛИЧИТЬ ЗАЗОР";
        public bool CanActivateTool(in InteractionContext context) => false;
        public void ActivateTool(in InteractionContext context) { }
        public Renderer ResolveOutlineRenderer() => Available ? screwRenderer : null;
        public InteractionOutlineFeedback GetOutlineFeedback(IHeldToolIdentity tool) =>
            tool != null && !ToolMatches(tool) ? InteractionOutlineFeedback.Invalid : InteractionOutlineFeedback.Default;
        public bool CanBypassParentCollider(InteractionTargetHost parentHost) => Available &&
            parentHost != null && parentHost.transform == state.Part.transform;
        public void RefreshAvailability()
        { if (hitbox == null) hitbox = GetComponent<Collider>(); if (hitbox != null) hitbox.enabled = Available; }
        private void ApplyPresentation()
        {
            if (state == null || screwRenderer == null || appliedRevision == state.Revision) return;
            screwRenderer.transform.localRotation = screwBaseRotation * Quaternion.AngleAxis(state.GetSetting(valveIndex) * 43f, Vector3.forward);
            appliedRevision = state.Revision;
        }
        private void Update() => RefreshAvailability();
        private void LateUpdate() => ApplyPresentation();
        private void OnDisable() { nextTurnTime = 0f; if (hitbox != null) hitbox.enabled = false; }
        private static bool ToolMatches(IHeldToolIdentity tool) => tool != null &&
            tool.ToolType == SatsumaAuxiliaryAssemblyTools.ScrewdriverType &&
            int.TryParse(tool.ToolVariant, out int size) && size == 0;
    }
}
