using System;
using System.Collections.Generic;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Items.Presentation
{
    [Serializable]
    public sealed class ItemScalarSurfaceBinding
    {
        [SerializeField] private string stateId = string.Empty;
        [SerializeField] private float visibleAbove = 0.0001f;
        [SerializeField] private Renderer[] renderers = Array.Empty<Renderer>();

        public string StateId => stateId;
        public float VisibleAbove => visibleAbove;
        public IReadOnlyList<Renderer> Renderers =>
            renderers ?? Array.Empty<Renderer>();

        internal void Refresh(WorldItemInstance owner)
        {
            bool visible = owner != null &&
                           owner.TryGetScalar(stateId, out float value) &&
                           value > visibleAbove;
            SetRenderersEnabled(renderers, visible);
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredStateId,
            float configuredVisibleAbove,
            params Renderer[] configuredRenderers)
        {
            stateId = configuredStateId ?? string.Empty;
            visibleAbove = Mathf.Max(0f, configuredVisibleAbove);
            renderers = configuredRenderers ?? Array.Empty<Renderer>();
            SetRenderersEnabled(renderers, false);
        }
#endif

        private static void SetRenderersEnabled(
            Renderer[] targets,
            bool enabled)
        {
            if (targets == null)
            {
                return;
            }

            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] != null)
                {
                    targets[index].enabled = enabled;
                }
            }
        }
    }

    /// <summary>
    /// A project-owned hinged cover layered over an existing world item. The
    /// body keeps its pickup/primary action, while aiming at the cover exposes
    /// an independent F action backed by the same versioned item state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HingedItemCoverPresentationController : MonoBehaviour,
        IWorldItemPresentationBinding,
        IContextInteractionTarget,
        IToolActivationTarget,
        IInteractionDisplayTarget
    {
        public const string PortableGrillDefinitionId = "item.portable-grill";
        public const string KiljuBucketDefinitionId = "item.kilju-bucket";
        public const string KiljuLidDefinitionId = "item.kilju-lid";
        public const string KiljuLidStableEntityId =
            "216a0dc4d7da58e5ae2fe6d1784ff54c";

        [SerializeField] private string expectedDefinitionId = string.Empty;
        [SerializeField] private string displayName = "Крышка";
        [SerializeField] private Transform hingePivot;
        [SerializeField] private Quaternion closedLocalRotation =
            Quaternion.identity;
        [SerializeField] private Vector3 hingeAxisLocal = Vector3.up;
        [SerializeField, Range(1f, 170f)] private float openAngleDegrees = 105f;
        [SerializeField, Min(1f)] private float angularSpeedDegrees = 220f;
        [SerializeField] private Renderer[] authoredCoverRenderers =
            Array.Empty<Renderer>();
        [SerializeField] private BoxCollider interactionCollider;
        [SerializeField] private string companionStableEntityId = string.Empty;
        [SerializeField] private string companionDefinitionId = string.Empty;
        [SerializeField] private Vector3 companionClosedLocalPosition;
        [SerializeField] private Quaternion companionClosedLocalRotation =
            Quaternion.identity;
        [SerializeField] private Vector3 companionClosedLocalScale = Vector3.one;

        private WorldItemInstance owner;
        private ItemWorldRuntime runtime;
        private WorldItemInstance companion;
        private GameObject companionPresentation;
        private Renderer[] activeCoverRenderers = Array.Empty<Renderer>();
        private bool targetOpen;

        public string InteractionDisplayName => displayName;
        public string ExpectedDefinitionId => expectedDefinitionId;
        public string InteractionPrompt =>
            targetOpen ? "Закрыть крышку" : "Открыть крышку";
        public string ToolPrompt => InteractionPrompt;
        public Transform HingePivot => hingePivot;
        public BoxCollider InteractionCollider => interactionCollider;
        public float OpenAngleDegrees => openAngleDegrees;
        public float AngularSpeedDegrees => angularSpeedDegrees;
        public bool TargetOpen => targetOpen;
        public bool IsCompanionAttached => companionPresentation != null;
        public IReadOnlyList<Renderer> CoverRenderers =>
            activeCoverRenderers ?? Array.Empty<Renderer>();
        public Quaternion OpenLocalRotation =>
            closedLocalRotation * Quaternion.AngleAxis(
                openAngleDegrees,
                NormalizedHingeAxis());

        public bool CanInteract(in InteractionContext context) =>
            owner != null && owner.CanToggleOpen &&
            hingePivot != null && interactionCollider != null &&
            enabled && gameObject.activeInHierarchy;

        public void Interact(in InteractionContext context)
        {
            TryToggle();
        }

        public bool CanActivateTool(in InteractionContext context) =>
            CanInteract(context);

        public void ActivateTool(in InteractionContext context)
        {
            TryToggle();
        }

        public void Bind(WorldItemInstance configuredOwner)
        {
            if (configuredOwner == null || owner != null ||
                !string.Equals(
                    configuredOwner.DefinitionId,
                    expectedDefinitionId,
                    StringComparison.Ordinal))
            {
                return;
            }

            owner = configuredOwner;
            runtime = owner.RuntimeOwner;
            activeCoverRenderers = authoredCoverRenderers ??
                Array.Empty<Renderer>();
            owner.StatusChanged += HandleOwnerStatusChanged;
            owner.StateRestored += HandleOwnerStateRestored;
            if (runtime != null)
            {
                runtime.InstanceMaterialized += HandleInstanceMaterialized;
                runtime.InstanceRemoved += HandleInstanceRemoved;
                runtime.PresentationAttached += HandlePresentationAttached;
            }

            targetOpen = owner.IsOpen;
            TryResolveCompanion();
            ConfigureInteractionHost();
            SnapToState();
        }

        public void SnapToState()
        {
            targetOpen = owner?.IsOpen == true;
            if (hingePivot != null)
            {
                hingePivot.localRotation = targetOpen
                    ? OpenLocalRotation
                    : closedLocalRotation;
            }

            SynchronizeCompanionPose();
        }

        private bool TryToggle()
        {
            return owner != null && owner.TryToggleOpen();
        }

        private void Update()
        {
            if (hingePivot != null)
            {
                Quaternion desired = targetOpen
                    ? OpenLocalRotation
                    : closedLocalRotation;
                hingePivot.localRotation = Quaternion.RotateTowards(
                    hingePivot.localRotation,
                    desired,
                    angularSpeedDegrees * Time.deltaTime);
            }

            SynchronizeCompanionPose();
        }

        private void HandleOwnerStatusChanged(IItemStatusSource _)
        {
            targetOpen = owner?.IsOpen == true;
        }

        private void HandleOwnerStateRestored(WorldItemInstance _)
        {
            SnapToState();
        }

        private void HandleInstanceMaterialized(WorldItemInstance instance)
        {
            TryAdoptCompanion(instance);
        }

        private void HandlePresentationAttached(
            WorldItemInstance instance,
            GameObject _)
        {
            TryAdoptCompanion(instance);
        }

        private void HandleInstanceRemoved(WorldItemInstance instance)
        {
            if (instance == null || companion == null || instance != companion)
            {
                return;
            }

            companion.StateRestored -= HandleCompanionStateRestored;

            if (companionPresentation != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(companionPresentation);
                }
                else
                {
                    DestroyImmediate(companionPresentation);
                }
            }

            ClearCompanionReferences();
            activeCoverRenderers = authoredCoverRenderers ??
                Array.Empty<Renderer>();
            FitInteractionColliderToCover();
            ConfigureInteractionHost();
        }

        private void HandleCompanionStateRestored(WorldItemInstance _)
        {
            if (companion != null && companionPresentation != null)
            {
                companion.gameObject.SetActive(false);
                SynchronizeCompanionPose();
            }
        }

        private void TryResolveCompanion()
        {
            if (runtime == null ||
                string.IsNullOrWhiteSpace(companionStableEntityId))
            {
                return;
            }

            if (runtime.TryGetInstance(
                    companionStableEntityId,
                    out WorldItemInstance instance))
            {
                TryAdoptCompanion(instance);
            }
        }

        private void TryAdoptCompanion(WorldItemInstance instance)
        {
            if (instance == null || hingePivot == null ||
                string.IsNullOrWhiteSpace(companionStableEntityId) ||
                !string.Equals(
                    instance.StableId.Value,
                    companionStableEntityId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    instance.DefinitionId,
                    companionDefinitionId,
                    StringComparison.Ordinal) ||
                instance.PresentationRoot == null)
            {
                return;
            }

            if (companion == instance &&
                companionPresentation == instance.PresentationRoot &&
                companionPresentation.transform.parent == hingePivot)
            {
                instance.gameObject.SetActive(false);
                return;
            }

            ReleaseCompanionPresentation(reactivate: false);
            companion = instance;
            companionPresentation = instance.PresentationRoot;
            companion.StateRestored += HandleCompanionStateRestored;
            Transform presentationTransform = companionPresentation.transform;
            presentationTransform.SetParent(hingePivot, false);
            presentationTransform.localPosition = companionClosedLocalPosition;
            presentationTransform.localRotation =
                companionClosedLocalRotation;
            presentationTransform.localScale = companionClosedLocalScale;
            companionPresentation.SetActive(true);
            activeCoverRenderers = companionPresentation
                .GetComponentsInChildren<Renderer>(true);
            FitInteractionColliderToCover();
            ConfigureInteractionHost();
            SynchronizeCompanionPose();
            companion.gameObject.SetActive(false);
        }

        private void ConfigureInteractionHost()
        {
            if (owner == null || hingePivot == null ||
                interactionCollider == null)
            {
                return;
            }

            interactionCollider.isTrigger = true;
            InteractionTargetHost host = hingePivot
                .GetComponent<InteractionTargetHost>();
            if (host == null)
            {
                host = hingePivot.gameObject
                    .AddComponent<InteractionTargetHost>();
            }

            PhysicsPickupTarget pickup =
                owner.GetComponent<PhysicsPickupTarget>();
            if (pickup != null)
            {
                host.Configure(pickup, this);
            }
            else
            {
                host.Configure(this);
            }

            host.ConfigureOutlineRenderers(activeCoverRenderers);
            Collider rootCollider = owner.GetComponent<Collider>();
            interactionCollider.sharedMaterial =
                rootCollider != null ? rootCollider.sharedMaterial : null;
        }

        private void FitInteractionColliderToCover()
        {
            if (hingePivot == null || interactionCollider == null ||
                activeCoverRenderers == null ||
                activeCoverRenderers.Length == 0)
            {
                return;
            }

            bool hasBounds = false;
            Bounds bounds = default;
            for (int index = 0; index < activeCoverRenderers.Length; index++)
            {
                Renderer renderer = activeCoverRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                Bounds localBounds = renderer.localBounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 rendererCorner = new Vector3(
                        (corner & 1) == 0
                            ? localBounds.min.x
                            : localBounds.max.x,
                        (corner & 2) == 0
                            ? localBounds.min.y
                            : localBounds.max.y,
                        (corner & 4) == 0
                            ? localBounds.min.z
                            : localBounds.max.z);
                    Vector3 pivotCorner = hingePivot.InverseTransformPoint(
                        renderer.transform.TransformPoint(rendererCorner));
                    if (!hasBounds)
                    {
                        bounds = new Bounds(pivotCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(pivotCorner);
                    }
                }
            }

            if (!hasBounds)
            {
                return;
            }

            const float interactionPadding = 0.035f;
            interactionCollider.center = bounds.center;
            interactionCollider.size = new Vector3(
                Mathf.Max(0.04f, bounds.size.x + interactionPadding),
                Mathf.Max(0.04f, bounds.size.y + interactionPadding),
                Mathf.Max(0.04f, bounds.size.z + interactionPadding));
        }

        private void SynchronizeCompanionPose()
        {
            if (owner == null || companion == null ||
                companionPresentation == null)
            {
                return;
            }

            Rigidbody companionBody = companion.GetComponent<Rigidbody>();
            if (companionBody != null)
            {
                companionBody.position = owner.transform.position;
                companionBody.rotation = owner.transform.rotation;
            }
            else
            {
                companion.transform.SetPositionAndRotation(
                    owner.transform.position,
                    owner.transform.rotation);
            }
        }

        private void ReleaseCompanionPresentation(bool reactivate)
        {
            if (companion != null)
            {
                companion.StateRestored -= HandleCompanionStateRestored;
            }

            if (companion != null && companionPresentation != null)
            {
                Transform presentationTransform = companionPresentation.transform;
                presentationTransform.SetParent(companion.transform, false);
                presentationTransform.localPosition = Vector3.zero;
                presentationTransform.localRotation = Quaternion.identity;
                presentationTransform.localScale = Vector3.one;
                if (reactivate && companion.State?.isConsumed != true)
                {
                    companion.gameObject.SetActive(true);
                }
            }

            ClearCompanionReferences();
        }

        private void ClearCompanionReferences()
        {
            companion = null;
            companionPresentation = null;
        }

        private Vector3 NormalizedHingeAxis()
        {
            return hingeAxisLocal.sqrMagnitude > 0.0001f
                ? hingeAxisLocal.normalized
                : Vector3.up;
        }

        private void OnDestroy()
        {
            if (owner != null)
            {
                owner.StatusChanged -= HandleOwnerStatusChanged;
                owner.StateRestored -= HandleOwnerStateRestored;
            }

            if (runtime != null)
            {
                runtime.InstanceMaterialized -= HandleInstanceMaterialized;
                runtime.InstanceRemoved -= HandleInstanceRemoved;
                runtime.PresentationAttached -= HandlePresentationAttached;
            }

            ReleaseCompanionPresentation(reactivate: true);
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredDefinitionId,
            string configuredDisplayName,
            Transform configuredHingePivot,
            Quaternion configuredClosedLocalRotation,
            Vector3 configuredHingeAxisLocal,
            float configuredOpenAngleDegrees,
            float configuredAngularSpeedDegrees,
            Renderer[] configuredCoverRenderers,
            BoxCollider configuredInteractionCollider,
            string configuredCompanionStableEntityId,
            string configuredCompanionDefinitionId,
            Vector3 configuredCompanionClosedLocalPosition,
            Quaternion configuredCompanionClosedLocalRotation,
            Vector3 configuredCompanionClosedLocalScale)
        {
            expectedDefinitionId = configuredDefinitionId ?? string.Empty;
            displayName = string.IsNullOrWhiteSpace(configuredDisplayName)
                ? "Крышка"
                : configuredDisplayName;
            hingePivot = configuredHingePivot;
            closedLocalRotation = configuredClosedLocalRotation;
            hingeAxisLocal = configuredHingeAxisLocal;
            openAngleDegrees = Mathf.Clamp(
                configuredOpenAngleDegrees,
                1f,
                170f);
            angularSpeedDegrees = Mathf.Max(
                1f,
                configuredAngularSpeedDegrees);
            authoredCoverRenderers = configuredCoverRenderers ??
                Array.Empty<Renderer>();
            activeCoverRenderers = authoredCoverRenderers;
            interactionCollider = configuredInteractionCollider;
            companionStableEntityId =
                configuredCompanionStableEntityId ?? string.Empty;
            companionDefinitionId =
                configuredCompanionDefinitionId ?? string.Empty;
            companionClosedLocalPosition =
                configuredCompanionClosedLocalPosition;
            companionClosedLocalRotation =
                configuredCompanionClosedLocalRotation;
            companionClosedLocalScale = configuredCompanionClosedLocalScale;
            if (interactionCollider != null)
            {
                interactionCollider.isTrigger = true;
                FitInteractionColliderToCover();
            }
        }
#endif
    }
}
