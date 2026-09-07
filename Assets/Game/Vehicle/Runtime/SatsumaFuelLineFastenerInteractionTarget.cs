using System;
using System.Globalization;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Vehicle
{
    [DisallowMultipleComponent]
    public sealed class SatsumaFuelLineFastenerInteractionTarget : MonoBehaviour,
        IToolActivationTarget, IHeldToolActivationTarget, IDirectionalScrollHeldToolActivationTarget,
        IInteractionDisplayTarget, IInteractionOutlineFeedbackSource, IInteractionOutlineRendererSource
    {
        public const int WrenchSizeMillimeters = 12;
        public const float StageTravelMeters = .002f; // Source .0025m * marker Z scale .8.
        public const float StageRotationDegrees = 45f;
        [SerializeField] private SatsumaFuelLineConnection connection;
        [SerializeField] private Transform fastenerPresentation;
        [SerializeField] private Renderer fastenerRenderer;
        [SerializeField] private Collider interactionCollider;
        private SatsumaFuelLineConnection subscribedConnection;

        public SatsumaFuelLineConnection Connection => connection;
        public Transform Presentation => fastenerPresentation;
        public Renderer FastenerRenderer => fastenerRenderer;
        public string InteractionDisplayName => "Гайка топливной магистрали (12 мм)";
        public string ToolPrompt => "Топливная магистраль (12 мм): " +
            (connection != null ? connection.Stage : 0) + "/8";
        private bool IsAvailable => isActiveAndEnabled && connection != null && connection.IsAvailable;

        public void Configure(SatsumaFuelLineConnection owner, Transform presentation,
            Renderer renderer, Collider collider)
        {
            if (owner == null || presentation == null || presentation.parent != transform ||
                renderer == null || renderer.transform != presentation ||
                collider == null || collider.transform != transform)
                throw new ArgumentException("Explicit fuel-line fitting presentation and collider are required.");
            Unsubscribe();
            connection = owner;
            fastenerPresentation = presentation;
            fastenerRenderer = renderer;
            interactionCollider = collider;
            if (isActiveAndEnabled) Subscribe();
            RefreshPresentation();
        }

        // The fitting is adjusted only by scrolling with the matching held wrench.
        public bool CanActivateTool(in InteractionContext context) => false;
        public void ActivateTool(in InteractionContext context) { }
        public bool CanActivateHeldTool(IHeldToolIdentity tool, in InteractionContext context) => false;
        public void ActivateHeldTool(IHeldToolIdentity tool, in InteractionContext context) { }

        public bool TryActivateHeldTool(IHeldToolIdentity tool, in InteractionContext context, float signedNotches)
        {
            if (!IsAvailable || !ToolMatches(tool) || Time.timeScale <= 0f) return false;
            bool changed = connection.TryTurn(signedNotches);
            RefreshPresentation();
            return changed;
        }

        public bool CanActivateHeldTool(IHeldToolIdentity tool, in InteractionContext context,
            InteractionScrollDirection direction) =>
            IsAvailable && ToolMatches(tool) && Time.timeScale > 0f &&
            (direction == InteractionScrollDirection.Positive
                ? connection.Stage < SatsumaFuelLineConnection.MaximumStage
                : connection.Stage > 0);

        public string GetHeldToolScrollPrompt(InteractionScrollDirection direction) =>
            direction == InteractionScrollDirection.Positive ? "ЗАТЯНУТЬ ГАЙКУ" : "ОСЛАБИТЬ ГАЙКУ";

        public InteractionOutlineFeedback GetOutlineFeedback(IHeldToolIdentity tool) =>
            !IsAvailable || !ToolMatches(tool) ? InteractionOutlineFeedback.Invalid : connection.Stage switch
            {
                0 => InteractionOutlineFeedback.Loose,
                SatsumaFuelLineConnection.MaximumStage => InteractionOutlineFeedback.Complete,
                _ => InteractionOutlineFeedback.Partial,
            };

        public Renderer ResolveOutlineRenderer() =>
            IsAvailable && fastenerRenderer != null && fastenerRenderer.enabled ? fastenerRenderer : null;

        public void RefreshPresentation()
        {
            bool available = IsAvailable;
            if (interactionCollider != null) interactionCollider.enabled = available;
            if (fastenerRenderer != null) fastenerRenderer.enabled = available;
            if (fastenerPresentation == null) return;
            int currentStage = connection != null ? connection.Stage : 0;
            // Immutable marker stores the donor rest pose; no recapture after a
            // reload/re-enable, and the nonuniform physical scale is applied once.
            fastenerPresentation.localPosition = new Vector3(0f, 0f, -StageTravelMeters * currentStage);
            fastenerPresentation.localRotation = Quaternion.AngleAxis(StageRotationDegrees * currentStage, Vector3.forward);
        }

        private void OnEnable() { Subscribe(); RefreshPresentation(); }
        private void OnDisable()
        {
            Unsubscribe();
            if (interactionCollider != null) interactionCollider.enabled = false;
            if (fastenerRenderer != null) fastenerRenderer.enabled = false;
        }
        private void Update() => RefreshPresentation(); // Tank lifecycle can change without save/action events.
        private void Subscribe()
        {
            if (subscribedConnection == connection) return;
            Unsubscribe();
            subscribedConnection = connection;
            if (subscribedConnection != null) subscribedConnection.StateChanged += RefreshPresentation;
        }
        private void Unsubscribe()
        {
            if (subscribedConnection != null) subscribedConnection.StateChanged -= RefreshPresentation;
            subscribedConnection = null;
        }
        private static bool ToolMatches(IHeldToolIdentity tool) => tool != null &&
            string.Equals(tool.ToolType, "Wrench", StringComparison.Ordinal) &&
            int.TryParse(tool.ToolVariant, NumberStyles.Integer, CultureInfo.InvariantCulture, out int size) &&
            size == WrenchSizeMillimeters;
    }
}
