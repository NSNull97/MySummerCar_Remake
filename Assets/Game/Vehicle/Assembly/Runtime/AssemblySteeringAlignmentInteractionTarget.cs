using System;
using System.Globalization;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// The donor 14 mm toe nut adjusts per-rod Alignment, not Stage/Tightness.
    /// Its visual rotation is intentionally independent of the clamped value.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblySteeringAlignmentInteractionTarget : MonoBehaviour,
        IToolActivationTarget,
        IHeldToolActivationTarget,
        IDirectionalScrollHeldToolActivationTarget,
        IFirstPersonToolSnapTarget,
        IInteractionOutlineFeedbackSource,
        IInteractionOutlineRendererSource
    {
        public const float OperationCooldownSeconds = 0.28f;
        public const float NutTurnDegrees = 13f;
        private const float WorkSeconds = 0.18f;
        private const float SpannerWorkArcDegrees = 60f;
        private static readonly Vector3 SpannerOffset = new Vector3(-0.073f, 0.028f, 0.016f);
        private static readonly Quaternion SpannerRotation = Quaternion.Euler(0f, 0f, 160f);

        [SerializeField] private AssemblySteeringAlignmentState alignment;
        [SerializeField] private Transform nutPresentation;
        [SerializeField] private Renderer nutRenderer;
        [SerializeField] private string cornerId = string.Empty;
        private Collider interactionCollider;
        private Transform spannerAnchor;
        private float operationStartedAt = float.NegativeInfinity;
        private float workDirection;

        public AssemblySteeringAlignmentState Alignment => alignment;
        public Transform NutPresentation => nutPresentation;
        public string CornerId => cornerId;
        public bool IsAvailable => isActiveAndEnabled && alignment != null &&
            alignment.Part != null && alignment.Part.IsInstalled;
        public string ToolPrompt => IsAvailable
            ? "Гайка регулировки схождения (14 мм): колесо вверх/вниз — регулировать"
            : "Регулировка схождения недоступна";

        public void Configure(AssemblySteeringAlignmentState configuredAlignment,
            Transform configuredNutPresentation, Renderer configuredNutRenderer,
            string configuredCornerId)
        {
            alignment = configuredAlignment != null ? configuredAlignment :
                throw new ArgumentNullException(nameof(configuredAlignment));
            nutPresentation = configuredNutPresentation != null ? configuredNutPresentation :
                throw new ArgumentNullException(nameof(configuredNutPresentation));
            nutRenderer = configuredNutRenderer != null ? configuredNutRenderer :
                throw new ArgumentNullException(nameof(configuredNutRenderer));
            cornerId = configuredCornerId ?? string.Empty;
            interactionCollider = GetComponent<Collider>();
            RefreshAvailability();
        }

        // Like staged fasteners, this prompt surface exposes no magic F action.
        public bool CanActivateTool(in InteractionContext context) => false;
        public void ActivateTool(in InteractionContext context) { }
        public bool CanActivateHeldTool(IHeldToolIdentity tool,
            in InteractionContext context) => false;
        public void ActivateHeldTool(IHeldToolIdentity tool,
            in InteractionContext context) { }

        public bool TryActivateHeldTool(IHeldToolIdentity tool,
            in InteractionContext context, float signedNotches)
        {
            if (!IsAvailable || !ToolMatches(tool) || !float.IsFinite(signedNotches) ||
                Mathf.Abs(signedNotches) < 0.001f || Time.timeScale <= 0f)
            {
                return false;
            }

            // Ignore repeated/high-resolution wheel ticks within one scaled
            // donor input cycle; a large delta still means one 0.1 degree step.
            if (Time.time - operationStartedAt < OperationCooldownSeconds)
            {
                return true;
            }

            bool tighten = signedNotches > 0f;
            if (!alignment.TryAdjust(tighten))
            {
                return false;
            }

            operationStartedAt = Time.time;
            workDirection = tighten ? -1f : 1f;
            // Rotate ThisBolt, not the marker: no staged axial travel here.
            nutPresentation.Rotate(0f, 0f, workDirection * NutTurnDegrees, Space.Self);
            EnsureSpannerAnchor();
            ApplySpannerPose();
            return true;
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

            float value = alignment.AlignmentDegrees;
            return direction == InteractionScrollDirection.Positive
                ? value > -AssemblySteeringAlignmentState.LimitDegrees + 0.0001f
                : value < AssemblySteeringAlignmentState.LimitDegrees - 0.0001f;
        }

        public string GetHeldToolScrollPrompt(
            InteractionScrollDirection direction) =>
            direction == InteractionScrollDirection.Positive
                ? "УМЕНЬШИТЬ СХОЖДЕНИЕ"
                : "УВЕЛИЧИТЬ СХОЖДЕНИЕ";

        public bool TryResolveToolSnapAnchor(IHeldToolIdentity tool,
            in InteractionContext context, out Transform anchor)
        {
            anchor = null;
            if (!IsAvailable || !ToolMatches(tool))
            {
                return false;
            }

            EnsureSpannerAnchor();
            ApplySpannerPose();
            anchor = spannerAnchor;
            return true;
        }

        public InteractionOutlineFeedback GetOutlineFeedback(IHeldToolIdentity tool) =>
            tool != null && !ToolMatches(tool)
                ? InteractionOutlineFeedback.Invalid
                : InteractionOutlineFeedback.Default;

        public Renderer ResolveOutlineRenderer() =>
            IsAvailable && nutRenderer != null && nutRenderer.enabled ? nutRenderer : null;

        public void RefreshAvailability()
        {
            if (interactionCollider == null)
            {
                interactionCollider = GetComponent<Collider>();
            }

            bool available = IsAvailable;
            if (interactionCollider != null)
            {
                interactionCollider.enabled = available;
            }
            if (nutRenderer != null)
            {
                nutRenderer.enabled = available;
            }
            if (!available)
            {
                operationStartedAt = float.NegativeInfinity;
                workDirection = 0f;
            }
        }

        private static bool ToolMatches(IHeldToolIdentity tool) => tool != null &&
            string.Equals(tool.ToolType, "Wrench", StringComparison.Ordinal) &&
            int.TryParse(tool.ToolVariant, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int size) && size == 14;

        private void EnsureSpannerAnchor()
        {
            if (spannerAnchor == null)
            {
                spannerAnchor = new GameObject("Steering adjustment spanner pose").transform;
                spannerAnchor.SetParent(transform, false);
            }
        }

        private void ApplySpannerPose()
        {
            if (spannerAnchor == null)
            {
                return;
            }

            float elapsed = Time.time - operationStartedAt;
            float arc = 0f;
            float lift = 0f;
            if (elapsed < WorkSeconds)
            {
                arc = Mathf.SmoothStep(0f, SpannerWorkArcDegrees, elapsed / WorkSeconds);
            }
            else if (elapsed < OperationCooldownSeconds)
            {
                float fraction = (elapsed - WorkSeconds) /
                    (OperationCooldownSeconds - WorkSeconds);
                arc = Mathf.SmoothStep(SpannerWorkArcDegrees, 0f, fraction);
                lift = 0.024f * Mathf.Sin(fraction * Mathf.PI);
            }

            spannerAnchor.localPosition = SpannerOffset + Vector3.forward * lift;
            spannerAnchor.localRotation = SpannerRotation *
                Quaternion.AngleAxis(workDirection * arc, Vector3.forward);
        }

        private void OnEnable() => RefreshAvailability();
        private void OnDisable()
        {
            RefreshAvailability();
            ApplySpannerPose();
        }
        private void Update()
        {
            RefreshAvailability();
            ApplySpannerPose();
        }
    }
}
