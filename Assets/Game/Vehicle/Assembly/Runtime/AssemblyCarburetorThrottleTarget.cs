using System;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Frozen stock throttle-linkage108301: hold LMB for local-X 40 degrees;
    /// when installed also request full throttle, never synthesize engine RPM.
    /// A loose carb permits the same linkage motion without engine input.
    /// Transient button intent deliberately is not persisted.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyCarburetorThrottleTarget : MonoBehaviour,
        IContinuousContextInteractionTarget, IInteractionDisplayTarget,
        IInteractionOutlineRendererSource, IParentColliderOcclusionBypass
    {
        public const float OpenDegrees = 40f;
        public const float ReleaseDistanceMeters = 2f;
        [SerializeField] private PartInstance part;
        [SerializeField] private Transform linkage;
        [SerializeField] private Renderer linkageRenderer;
        [SerializeField] private Renderer closedButterfly;
        [SerializeField] private Renderer openButterfly;
        [SerializeField] private Vector3 localPivot;
        [SerializeField] private Vector3 restPartLocalPosition;
        [SerializeField] private Quaternion restPartLocalRotation = Quaternion.identity;
        private bool held;
        private bool installedWhenPressed;
        private Transform interactor;

        public PartInstance Part => part;
        public Transform Linkage => linkage;
        public bool IsHeld => held;
        public float RequestedThrottle01
        {
            get
            {
                if (held) ContinueContinuousInteraction(0f);
                return held && part != null && part.IsInstalled && installedWhenPressed ? 1f : 0f;
            }
        }
        public string InteractionDisplayName => "Рычаг газа карбюратора";
        public string InteractionPrompt => "Удерживать ЛКМ — открыть дроссель";
        // Opt into the existing player press/hold/release route. This flag
        // does not require both buttons: the donor throttle accepts only LMB.
        public bool UsesDirectionalHold => true;

        public void Configure(PartInstance configuredPart, Transform configuredLinkage,
            Renderer configuredClosedButterfly, Renderer configuredOpenButterfly,
            Vector3 authoredPivot, Vector3 authoredPosition, Quaternion authoredRotation)
        {
            if (configuredPart?.Definition == null || configuredPart.Definition.DefinitionId !=
                SatsumaEngineAdjustmentRules.PartId(SatsumaEngineAdjustmentKind.CarburetorMixture))
                throw new ArgumentException("Stock throttle must belong to the stock carburetor.");
            if (configuredLinkage == null || configuredLinkage == configuredPart.transform)
                throw new ArgumentException("Bind the separate throttle linkage presentation.");
            part = configuredPart;
            linkage = configuredLinkage;
            linkageRenderer = linkage.GetComponent<Renderer>();
            closedButterfly = configuredClosedButterfly;
            openButterfly = configuredOpenButterfly;
            localPivot = authoredPivot;
            restPartLocalPosition = authoredPosition;
            restPartLocalRotation = authoredRotation;
        }

        public bool CanBeginContinuousInteraction(in InteractionContext context,
            ContinuousContextInteractionDirection direction) => isActiveAndEnabled && part != null &&
            part.gameObject.activeInHierarchy && linkage != null &&
            direction == ContinuousContextInteractionDirection.Primary;

        public void BeginContinuousInteraction(in InteractionContext context,
            ContinuousContextInteractionDirection direction)
        {
            if (!CanBeginContinuousInteraction(context, direction)) return;
            held = true;
            installedWhenPressed = part.IsInstalled;
            interactor = context.Interactor != null ? context.Interactor.transform : null;
            ApplyPresentation();
        }

        public bool ContinueContinuousInteraction(float deltaTime)
        {
            if (!held) return false;
            if (!isActiveAndEnabled || part == null || !part.gameObject.activeInHierarchy ||
                installedWhenPressed != part.IsInstalled || installedWhenPressed && interactor != null &&
                Vector3.Distance(interactor.position, transform.position) > ReleaseDistanceMeters)
            { EndContinuousInteraction(); return false; }
            return true;
        }

        public void EndContinuousInteraction()
        {
            held = false;
            installedWhenPressed = false;
            interactor = null;
            ApplyPresentation();
        }

        public Renderer ResolveOutlineRenderer() => isActiveAndEnabled ? linkageRenderer : null;
        public bool CanBypassParentCollider(InteractionTargetHost parentHost) => isActiveAndEnabled &&
            part != null && parentHost != null && parentHost.transform == part.transform;

        private void ApplyPresentation()
        {
            if (part == null || linkage == null) return;
            Quaternion delta = Quaternion.Euler(held ? OpenDegrees : 0f, 0f, 0f);
            linkage.SetPositionAndRotation(part.transform.TransformPoint(localPivot +
                delta * (restPartLocalPosition - localPivot)),
                part.transform.rotation * delta * restPartLocalRotation);
            if (closedButterfly != null) closedButterfly.enabled = !held;
            if (openButterfly != null) openButterfly.enabled = held;
        }

        private void Start() => EndContinuousInteraction();
        private void LateUpdate()
        {
            if (held) ContinueContinuousInteraction(Time.deltaTime);
            ApplyPresentation();
        }
        private void OnDisable() => EndContinuousInteraction();
    }
}
