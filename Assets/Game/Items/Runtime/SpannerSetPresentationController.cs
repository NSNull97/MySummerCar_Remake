using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Items.Presentation
{
    /// <summary>
    /// Project-owned presentation wrapper for the donor-evidenced toolbox.
    /// The item state owns open/closed gameplay; this component only animates
    /// the reviewed lid pose and exposes the individual selectable spanners.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpannerSetPresentationController : MonoBehaviour
    {
        public const string DefinitionId = "item.spanner-set";

        private static readonly Vector3 DonorHingeLocalPosition =
            new Vector3(0.156f, 0f, 0.0257f);
        private static readonly Quaternion DonorOpenLocalRotation =
            new Quaternion(0f, 0.91509414f, 0f, 0.40324026f);
        private const float DonorOpenDurationSeconds = 0.45f;
        private const float DonorCloseDurationSeconds = 0.25f;

        [SerializeField] private Renderer lidRenderer;
        [SerializeField] private Renderer[] spannerRenderers =
            Array.Empty<Renderer>();
        [SerializeField] private string[] spannerSizes =
            Array.Empty<string>();
        [SerializeField] private Renderer[] auxiliaryToolRenderers =
            Array.Empty<Renderer>();

        private readonly List<SpannerSetToolPickupTarget> pickupTargets =
            new List<SpannerSetToolPickupTarget>(11);
        private WorldItemInstance owner;
        private Transform hinge;
        private Coroutine animationRoutine;

        public IReadOnlyList<SpannerSetToolPickupTarget> PickupTargets =>
            pickupTargets;
        public Renderer LidRenderer => lidRenderer;
        public IReadOnlyList<Renderer> SpannerRenderers =>
            spannerRenderers ?? Array.Empty<Renderer>();
        public IReadOnlyList<string> SpannerSizes =>
            spannerSizes ?? Array.Empty<string>();
        public IReadOnlyList<Renderer> AuxiliaryToolRenderers =>
            auxiliaryToolRenderers ?? Array.Empty<Renderer>();

        public void Bind(WorldItemInstance configuredOwner)
        {
            if (configuredOwner == null || owner != null ||
                !string.Equals(
                    configuredOwner.DefinitionId,
                    DefinitionId,
                    StringComparison.Ordinal))
            {
                return;
            }

            owner = configuredOwner;
            BuildLidHinge();
            BuildSpannerTargets();
            owner.StatusChanged += HandleStatusChanged;
            ApplyStateImmediately(IsOpen());
        }

        private void BuildLidHinge()
        {
            if (lidRenderer == null)
            {
                return;
            }

            var hingeObject = new GameObject("Project-owned toolbox lid hinge");
            hinge = hingeObject.transform;
            hinge.SetParent(transform, false);
            hinge.localPosition = DonorHingeLocalPosition;
            hinge.localRotation = Quaternion.identity;
            hinge.localScale = Vector3.one;
            lidRenderer.transform.SetParent(hinge, true);
        }

        private void BuildSpannerTargets()
        {
            int count = Mathf.Min(
                spannerRenderers?.Length ?? 0,
                spannerSizes?.Length ?? 0);
            for (int index = 0; index < count; index++)
            {
                Renderer renderer = spannerRenderers[index];
                string size = spannerSizes[index];
                if (renderer == null || string.IsNullOrWhiteSpace(size))
                {
                    continue;
                }

                GameObject targetObject = renderer.gameObject;
                RemoveLegacyPhysicalToolComponents(targetObject);
                BoxCollider collider =
                    targetObject.GetComponent<BoxCollider>();
                if (collider == null)
                {
                    collider = targetObject.AddComponent<BoxCollider>();
                }

                Bounds bounds = renderer.localBounds;
                collider.center = bounds.center;
                collider.size = new Vector3(
                    Mathf.Max(0.02f, bounds.size.x),
                    Mathf.Max(0.02f, bounds.size.y),
                    Mathf.Max(0.02f, bounds.size.z));
                // Docked tools are queryable triggers. Solid nested colliders
                // would collide with the moving dynamic case and launch it
                // while the lid opens.
                collider.isTrigger = true;

                SpannerSetToolPickupTarget pickup = targetObject
                    .GetComponent<SpannerSetToolPickupTarget>();
                if (pickup == null)
                {
                    pickup = targetObject
                        .AddComponent<SpannerSetToolPickupTarget>();
                }

                pickup.Configure(
                    this,
                    renderer,
                    collider,
                    size);
                InteractionTargetHost host = targetObject
                    .GetComponent<InteractionTargetHost>();
                if (host == null)
                {
                    host = targetObject.AddComponent<InteractionTargetHost>();
                }

                host.Configure(pickup);
                host.ConfigureSelectionPriority(30);
                host.ConfigureOutlineRenderers(renderer);
                pickupTargets.Add(pickup);
            }
        }

        private static void RemoveLegacyPhysicalToolComponents(
            GameObject targetObject)
        {
            PhysicsPickupTarget legacyPickup =
                targetObject.GetComponent<PhysicsPickupTarget>();
            if (legacyPickup != null)
            {
                legacyPickup.enabled = false;
                DestroyComponent(legacyPickup);
            }

            Rigidbody legacyBody = targetObject.GetComponent<Rigidbody>();
            if (legacyBody == null)
            {
                return;
            }

            // Neutralize immediately before deferred runtime destruction. A
            // stale generated prefab therefore cannot get even one physics
            // step in which a nested wrench launches the whole toolbox.
            legacyBody.linearVelocity = Vector3.zero;
            legacyBody.angularVelocity = Vector3.zero;
            legacyBody.isKinematic = true;
            legacyBody.detectCollisions = false;
            DestroyComponent(legacyBody);
        }

        private static void DestroyComponent(Component component)
        {
            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                DestroyImmediate(component);
            }
        }

        internal bool CanTakeTool(SpannerSetToolPickupTarget target)
        {
            if (target == null || owner == null || !IsOpen())
            {
                return false;
            }

            PhysicsPickupTarget casePickup =
                owner.GetComponent<PhysicsPickupTarget>();
            return (casePickup == null || !casePickup.IsCarried) &&
                   target.IsDocked;
        }

        internal void NotifyToolDocked(SpannerSetToolPickupTarget target)
        {
            if (target != null && target.IsDocked)
            {
                target.gameObject.SetActive(IsOpen());
            }
        }

        private void HandleStatusChanged(IItemStatusSource _)
        {
            bool open = IsOpen();
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
            }

            animationRoutine = StartCoroutine(AnimateLid(open));
        }

        private IEnumerator AnimateLid(bool open)
        {
            SetDockedToolsVisible(true);
            StabilizeCaseWhileAnimating();
            Quaternion start = hinge != null
                ? hinge.localRotation
                : Quaternion.identity;
            Quaternion target = open
                ? DonorOpenLocalRotation
                : Quaternion.identity;
            float duration = open
                ? DonorOpenDurationSeconds
                : DonorCloseDurationSeconds;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float eased = normalized * normalized *
                              (3f - 2f * normalized);
                if (hinge != null)
                {
                    hinge.localRotation = Quaternion.SlerpUnclamped(
                        start,
                        target,
                        eased);
                }

                StabilizeCaseWhileAnimating();

                yield return null;
            }

            if (hinge != null)
            {
                hinge.localRotation = target;
            }

            SetDockedToolsVisible(open);
            animationRoutine = null;
        }

        private void StabilizeCaseWhileAnimating()
        {
            if (owner == null)
            {
                return;
            }

            PhysicsPickupTarget casePickup =
                owner.GetComponent<PhysicsPickupTarget>();
            Rigidbody caseBody = owner.GetComponent<Rigidbody>();
            if (caseBody == null || caseBody.isKinematic ||
                casePickup != null && casePickup.IsCarried)
            {
                return;
            }

            // The donor lid is presentation-only and cannot impart momentum
            // to the 8 kg case. Clear contact jitter while its sanitized visual
            // rotates so opening the case never turns it into a propeller.
            caseBody.linearVelocity = Vector3.zero;
            caseBody.angularVelocity = Vector3.zero;
        }

        private void ApplyStateImmediately(bool open)
        {
            if (hinge != null)
            {
                hinge.localRotation = open
                    ? DonorOpenLocalRotation
                    : Quaternion.identity;
            }

            SetDockedToolsVisible(open);
        }

        private void SetDockedToolsVisible(bool visible)
        {
            for (int index = 0; index < pickupTargets.Count; index++)
            {
                SpannerSetToolPickupTarget target = pickupTargets[index];
                if (target != null && target.IsDocked)
                {
                    target.gameObject.SetActive(visible);
                }
            }

            if (auxiliaryToolRenderers == null)
            {
                return;
            }

            for (int index = 0; index < auxiliaryToolRenderers.Length; index++)
            {
                Renderer renderer = auxiliaryToolRenderers[index];
                if (renderer != null)
                {
                    renderer.gameObject.SetActive(visible);
                }
            }
        }

        private bool IsOpen()
        {
            ItemInstanceState state = owner?.State;
            return state != null && state.isOpen;
        }

        private void OnDestroy()
        {
            for (int index = 0; index < pickupTargets.Count; index++)
            {
                SpannerSetToolPickupTarget target = pickupTargets[index];
                if (target != null && !target.IsDocked)
                {
                    target.NotifyDeselected();
                }
            }

            if (owner != null)
            {
                owner.StatusChanged -= HandleStatusChanged;
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            Renderer configuredLidRenderer,
            Renderer[] configuredSpannerRenderers,
            string[] configuredSpannerSizes,
            Renderer[] configuredAuxiliaryToolRenderers)
        {
            lidRenderer = configuredLidRenderer;
            spannerRenderers = configuredSpannerRenderers ??
                Array.Empty<Renderer>();
            spannerSizes = configuredSpannerSizes ?? Array.Empty<string>();
            auxiliaryToolRenderers = configuredAuxiliaryToolRenderers ??
                Array.Empty<Renderer>();
        }
#endif
    }

    [DisallowMultipleComponent]
    public sealed class SpannerSetToolPickupTarget : MonoBehaviour,
        IFirstPersonToolSelectionTarget,
        IFirstPersonToolSelectionTransition,
        IInteractionDisplayTarget,
        IParentColliderOcclusionBypass
    {
        // Reference pose for a 14 mm key. The handle exits below the viewport
        // and the extra local-X twist exposes the thin edge of the donor mesh
        // instead of presenting it as a flat white silhouette.
        private static readonly Vector3 ReferenceIdleViewmodelLocalPosition =
            new Vector3(0.28f, -0.235f, 0.34f);
        private static readonly Quaternion IdleViewmodelLocalRotation =
            Quaternion.Euler(18f, -22f, -48f) *
            Quaternion.AngleAxis(22f, Vector3.left);
        private const float ReferenceSpannerSizeMillimetres = 14f;
        private const float DistanceSizeCompensation = 0.35f;
        private const float SelectionTransitionDurationSecondsValue = 0.28f;

        private SpannerSetPresentationController owner;
        private Renderer targetRenderer;
        private BoxCollider targetCollider;
        private Transform dockParent;
        private Vector3 dockLocalPosition;
        private Quaternion dockLocalRotation;
        private Vector3 dockLocalScale;
        private string size = string.Empty;
        private bool selected;

        public string SelectionPrompt => $"Выбрать ключ {size} мм";
        public string InteractionDisplayName => $"Ключ {size} мм";
        public Transform ToolVisual => transform;
        public string ToolType => "Wrench";
        public string ToolVariant => size;
        public Vector3 IdleLocalPosition =>
            ReferenceIdleViewmodelLocalPosition *
            ResolveViewmodelDistanceScale();
        public Quaternion IdleLocalRotation => IdleViewmodelLocalRotation;
        public float SelectionTransitionDurationSeconds =>
            SelectionTransitionDurationSecondsValue;
        public bool IsDocked { get; private set; }

        public bool CanSelect(in InteractionContext context) =>
            !selected && targetRenderer != null && targetCollider != null &&
            owner != null && owner.CanTakeTool(this);

        public void NotifySelected(in InteractionContext context)
        {
            selected = true;
            IsDocked = false;
            targetCollider.enabled = false;
        }

        public bool CanBypassParentCollider(
            InteractionTargetHost parentHost) =>
            owner != null && parentHost != null &&
            parentHost.transform == owner.transform;

        public void NotifyDeselected()
        {
            selected = false;
            Dock();
        }

        internal void Configure(
            SpannerSetPresentationController configuredOwner,
            Renderer configuredRenderer,
            BoxCollider configuredCollider,
            string configuredSize)
        {
            owner = configuredOwner;
            targetRenderer = configuredRenderer;
            targetCollider = configuredCollider;
            size = configuredSize ?? string.Empty;
            dockParent = transform.parent;
            dockLocalPosition = transform.localPosition;
            dockLocalRotation = transform.localRotation;
            dockLocalScale = transform.localScale;
            IsDocked = true;
        }

        private void Dock()
        {
            if (dockParent == null)
            {
                return;
            }

            transform.SetParent(dockParent, false);
            transform.localPosition = dockLocalPosition;
            transform.localRotation = dockLocalRotation;
            transform.localScale = dockLocalScale;
            targetCollider.isTrigger = true;
            targetCollider.enabled = true;
            IsDocked = true;
            owner?.NotifyToolDocked(this);
        }

        private float ResolveViewmodelDistanceScale()
        {
            if (!float.TryParse(
                    size,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float millimetres) ||
                !float.IsFinite(millimetres))
            {
                millimetres = ReferenceSpannerSizeMillimetres;
            }

            float physicalSizeRatio = Mathf.Clamp(
                millimetres / ReferenceSpannerSizeMillimetres,
                5f / ReferenceSpannerSizeMillimetres,
                15f / ReferenceSpannerSizeMillimetres);
            // Only part of the physical size difference is compensated by
            // camera distance. Small keys stay in front of the near plane but
            // remain visibly smaller than large keys in the player's hand.
            return Mathf.Lerp(
                1f,
                physicalSizeRatio,
                DistanceSizeCompensation);
        }
    }
}
