using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    [DisallowMultipleComponent]
    public sealed class AssemblyServiceCapTarget : MonoBehaviour,
        IDirectionalIncrementalInteractionTarget, IToolActivationTarget,
        IInteractionDisplayTarget, IInteractionOutlineRendererSource, IParentColliderOcclusionBypass
    {
        [SerializeField] private AssemblyServiceCapState state;
        [SerializeField] private int capIndex;
        [SerializeField] private Renderer capRenderer;
        [SerializeField] private Quaternion closedLocalRotation = Quaternion.identity;
        private Collider hitbox;
        private int appliedRevision = -1;
        private float nextTurnTime = float.NegativeInfinity;
        public AssemblyServiceCapState State => state;
        public int CapIndex => capIndex;
        public Renderer CapRenderer => capRenderer;
        public string InteractionDisplayName => state == null ? "Крышка" : state.Kind(capIndex) switch
        {
            SatsumaServiceCapKind.MotorOil => "Крышка маслозаливной горловины",
            SatsumaServiceCapKind.Coolant => "Крышка радиатора",
            SatsumaServiceCapKind.BrakeFront => "Крышка тормозного бачка — передний контур",
            SatsumaServiceCapKind.BrakeRear => "Крышка тормозного бачка — задний контур",
            _ => "Крышка бачка сцепления",
        };
        public string AdjustmentPrompt => "Колесо вниз — открыть, вверх — закрыть";
        public string ToolPrompt => string.Empty;
        private bool Available => isActiveAndEnabled && state != null && state.IsAvailable;
        public void Configure(AssemblyServiceCapState caps, int index, Renderer renderer)
        {
            if (caps == null || index < 0 || index >= caps.Count || renderer == null ||
                renderer.transform == caps.Part.transform || !renderer.transform.IsChildOf(caps.Part.transform))
                throw new ArgumentException("Explicit separate cap renderer and part-owned state required.");
            state = caps; capIndex = index; capRenderer = renderer;
            closedLocalRotation = renderer.transform.localRotation; hitbox = GetComponent<Collider>(); appliedRevision = -1;
        }
        public bool CanAdjust(in InteractionContext context) => Available;
        public bool CanAdjust(in InteractionContext context, InteractionScrollDirection direction) =>
            Available && state.CanAdjust(capIndex, (float)direction);
        public bool TryAdjust(in InteractionContext context, float signedNotches)
        {
            if (!Available || Time.timeScale <= 0f || !state.CanAdjust(capIndex, signedNotches)) return false;
            if (Time.unscaledTime < nextTurnTime) return true;
            if (!state.TryAdjust(capIndex, signedNotches)) return false;
            nextTurnTime = Time.unscaledTime + .1f; RefreshPresentation(); return true;
        }
        public string GetAdjustmentPrompt(InteractionScrollDirection direction) =>
            direction == InteractionScrollDirection.Positive ? "ЗАКРУТИТЬ КРЫШКУ" : "ОТКРУТИТЬ КРЫШКУ";
        public bool CanActivateTool(in InteractionContext context) => false;
        public void ActivateTool(in InteractionContext context) { }
        public Renderer ResolveOutlineRenderer() => Available && capRenderer != null && capRenderer.enabled ? capRenderer : null;
        public bool CanBypassParentCollider(InteractionTargetHost parentHost) => Available &&
            parentHost != null && parentHost.transform == state.Part.transform;
        public void RefreshPresentation()
        {
            if (state == null || capRenderer == null || appliedRevision == state.Revision) return;
            capRenderer.transform.localRotation = closedLocalRotation * Quaternion.AngleAxis(state.Angle(capIndex) - 359f, Vector3.forward);
            capRenderer.enabled = !state.IsOpen(capIndex); appliedRevision = state.Revision;
        }
        private void Update()
        {
            if (hitbox == null) hitbox = GetComponent<Collider>();
            if (hitbox != null) hitbox.enabled = Available;
        }
        private void LateUpdate() => RefreshPresentation();
        private void OnDisable() { nextTurnTime = float.NegativeInfinity; if (hitbox != null) hitbox.enabled = false; }
    }
}
