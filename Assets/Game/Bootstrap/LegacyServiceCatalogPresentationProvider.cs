using System;
using System.Collections.Generic;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Items;
using UnityEngine;

namespace MSC.Bootstrap
{
    public enum LegacyServiceCatalogKind
    {
        HomeParts = 0,
        FleetariServices = 1,
    }

    /// <summary>
    /// Owns the two reviewed temporary catalog surfaces generated from the
    /// locked donor export. Service logic remains project-owned.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LegacyServiceCatalogPresentationProvider : MonoBehaviour
    {
        [SerializeField] private Material homePartsCover;
        [SerializeField] private Material fleetariServicesCover;
        [SerializeField] private Material[] homePartsPages =
            Array.Empty<Material>();
        [SerializeField] private Material homePartsOrderPage;
        [SerializeField] private Material homePartsSelectionMark;
        [SerializeField] private Font homePartsOrderFont;
        [SerializeField] private Material[] fleetariServicePages =
            Array.Empty<Material>();
        [SerializeField] private Material fleetariOrderPage;

        public static LegacyServiceCatalogPresentationProvider Current
        {
            get;
            private set;
        }

        public Material HomePartsCover => homePartsCover;
        public Material FleetariServicesCover => fleetariServicesCover;
        public IReadOnlyList<Material> HomePartsPages => homePartsPages;
        public Material HomePartsOrderPage => homePartsOrderPage;
        public Material HomePartsSelectionMark => homePartsSelectionMark;
        public Font HomePartsOrderFont => homePartsOrderFont;
        public IReadOnlyList<Material> FleetariServicePages =>
            fleetariServicePages;
        public Material FleetariOrderPage => fleetariOrderPage;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            Material configuredHomePartsCover,
            Material configuredFleetariServicesCover,
            Material[] configuredHomePartsPages,
            Material configuredHomePartsOrderPage,
            Material[] configuredFleetariServicePages,
            Material configuredFleetariOrderPage)
        {
            ConfigureForAuthoring(
                configuredHomePartsCover,
                configuredFleetariServicesCover,
                configuredHomePartsPages,
                configuredHomePartsOrderPage,
                configuredFleetariServicePages,
                configuredFleetariOrderPage,
                configuredHomePartsSelectionMark: null,
                configuredHomePartsOrderFont: null);
        }

        public void ConfigureForAuthoring(
            Material configuredHomePartsCover,
            Material configuredFleetariServicesCover,
            Material[] configuredHomePartsPages,
            Material configuredHomePartsOrderPage,
            Material[] configuredFleetariServicePages,
            Material configuredFleetariOrderPage,
            Material configuredHomePartsSelectionMark,
            Font configuredHomePartsOrderFont)
        {
            homePartsCover = configuredHomePartsCover ??
                throw new ArgumentNullException(
                    nameof(configuredHomePartsCover));
            fleetariServicesCover = configuredFleetariServicesCover ??
                throw new ArgumentNullException(
                    nameof(configuredFleetariServicesCover));
            homePartsPages = CopyRequiredMaterials(
                configuredHomePartsPages,
                8,
                nameof(configuredHomePartsPages));
            homePartsOrderPage = configuredHomePartsOrderPage ??
                throw new ArgumentNullException(
                    nameof(configuredHomePartsOrderPage));
            fleetariServicePages = CopyRequiredMaterials(
                configuredFleetariServicePages,
                6,
                nameof(configuredFleetariServicePages));
            fleetariOrderPage = configuredFleetariOrderPage ??
                throw new ArgumentNullException(
                    nameof(configuredFleetariOrderPage));
            homePartsSelectionMark = configuredHomePartsSelectionMark;
            homePartsOrderFont = configuredHomePartsOrderFont;
        }

        private static Material[] CopyRequiredMaterials(
            Material[] configured,
            int expectedCount,
            string parameterName)
        {
            if (configured == null || configured.Length != expectedCount ||
                Array.Exists(configured, value => value == null))
            {
                throw new ArgumentException(
                    $"Catalog requires {expectedCount} reviewed page materials.",
                    parameterName);
            }

            return (Material[])configured.Clone();
        }
#endif

        public bool TryCreate(
            LegacyServiceCatalogKind kind,
            Transform parent,
            out GameObject visual)
        {
            Material material = kind == LegacyServiceCatalogKind.HomeParts
                ? homePartsCover
                : fleetariServicesCover;
            if (parent == null || material == null)
            {
                visual = null;
                return false;
            }

            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = kind == LegacyServiceCatalogKind.HomeParts
                ? "Temporary reviewed home parts catalog"
                : "Temporary reviewed Fleetari service brochure";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = kind ==
                                             LegacyServiceCatalogKind.HomeParts
                ? new Quaternion(0f, -0.65323746f, 0f, 0.7571531f)
                : new Quaternion(0f, 0.89758277f, 0f, -0.44084606f);
            visual.transform.localScale = new Vector3(
                0.21f,
                0.002168025f,
                0.3f);
            visual.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(collider);
                }
                else
                {
                    DestroyImmediate(collider);
                }
            }

            return true;
        }

        private void Awake()
        {
            if (homePartsCover == null || fleetariServicesCover == null)
            {
                throw new InvalidOperationException(
                    "Legacy service catalog surfaces are not configured.");
            }

            if (homePartsPages == null || homePartsPages.Length != 8 ||
                Array.Exists(homePartsPages, value => value == null) ||
                homePartsOrderPage == null ||
                fleetariServicePages == null ||
                fleetariServicePages.Length != 6 ||
                Array.Exists(fleetariServicePages, value => value == null) ||
                fleetariOrderPage == null)
            {
                Debug.LogWarning(
                    "Catalog page materials are not generated yet; physical " +
                    "catalogs will use fail-visible paper until the reviewed " +
                    "item presentation pipeline is rebuilt.",
                    this);
            }

            Current = this;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(
            RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;
    }

    [DisallowMultipleComponent]
    public sealed class HomePartsCatalogPhysicalBinding : MonoBehaviour
    {
        private const float RetrySeconds = 0.5f;
        private string stableEntityId = string.Empty;
        private GameObject fallbackVisual;
        private MonoBehaviour catalogCapability;
        private WorldItemInstance boundMagazine;
        private Collider anchorCollider;
        private Renderer[] boundRenderers = Array.Empty<Renderer>();
        private bool[] boundRendererStates = Array.Empty<bool>();
        private Collider[] boundColliders = Array.Empty<Collider>();
        private bool[] boundColliderStates = Array.Empty<bool>();
        private Rigidbody boundBody;
        private Vector3 fallbackPosition;
        private Quaternion fallbackRotation;
        private float nextAttemptTime;
        private bool bound;
        private bool catalogVisible = true;
        private bool readingPhysicsOwned;
        private bool readingUseGravity;
        private bool readingIsKinematic;
        private bool readingDetectCollisions;

        public Transform PhysicalCatalogTransform =>
            boundMagazine != null ? boundMagazine.transform : transform;

        public bool HasPhysicalCatalog => boundMagazine != null;

        public bool TryBeginReading()
        {
            if (boundMagazine == null)
            {
                return false;
            }

            PhysicsPickupTarget pickup = boundMagazine.GetComponent<
                PhysicsPickupTarget>();
            if (pickup != null && pickup.IsCarried)
            {
                return false;
            }

            SynchronizePose();
            if (boundBody == null || readingPhysicsOwned)
            {
                return true;
            }

            readingUseGravity = boundBody.useGravity;
            readingIsKinematic = boundBody.isKinematic;
            readingDetectCollisions = boundBody.detectCollisions;
            boundBody.linearVelocity = Vector3.zero;
            boundBody.angularVelocity = Vector3.zero;
            boundBody.useGravity = false;
            boundBody.isKinematic = true;
            for (int index = 0; index < boundColliders.Length; index++)
            {
                if (boundColliders[index] != null)
                {
                    boundColliders[index].enabled = false;
                }
            }

            readingPhysicsOwned = true;
            return true;
        }

        public void EndReading()
        {
            if (!readingPhysicsOwned || boundBody == null)
            {
                readingPhysicsOwned = false;
                return;
            }

            boundBody.detectCollisions = readingDetectCollisions;
            boundBody.isKinematic = readingIsKinematic;
            boundBody.useGravity = readingUseGravity;
            for (int index = 0; index < boundColliders.Length; index++)
            {
                if (boundColliders[index] != null)
                {
                    boundColliders[index].enabled = boundColliderStates[index];
                }
            }

            if (!readingIsKinematic)
            {
                boundBody.linearVelocity = Vector3.zero;
                boundBody.angularVelocity = Vector3.zero;
                boundBody.WakeUp();
            }

            readingPhysicsOwned = false;
        }

        public void Configure(
            string configuredStableEntityId,
            GameObject configuredFallbackVisual,
            MonoBehaviour configuredCatalogCapability = null)
        {
            stableEntityId = configuredStableEntityId ?? string.Empty;
            fallbackVisual = configuredFallbackVisual;
            catalogCapability = configuredCatalogCapability;
            anchorCollider = GetComponent<Collider>();
            fallbackPosition = transform.position;
            fallbackRotation = transform.rotation;
            nextAttemptTime = 0f;
            TryBind();
        }

        public void SetFallbackVisual(GameObject configuredFallbackVisual)
        {
            fallbackVisual = configuredFallbackVisual;
            if (fallbackVisual != null)
            {
                fallbackVisual.SetActive(catalogVisible && !bound);
            }
        }

        public void SetCatalogVisible(bool visible)
        {
            catalogVisible = visible;
            if (fallbackVisual != null)
            {
                fallbackVisual.SetActive(visible && !bound);
            }

            for (int index = 0; index < boundRenderers.Length; index++)
            {
                Renderer renderer = boundRenderers[index];
                if (renderer != null)
                {
                    renderer.enabled = visible && boundRendererStates[index];
                }
            }
        }

        public void SynchronizePose()
        {
            if (boundMagazine == null)
            {
                return;
            }

            transform.position = boundMagazine.transform.position;
            Vector3 flatForward = Vector3.ProjectOnPlane(
                boundMagazine.transform.forward,
                Vector3.up);
            if (flatForward.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(
                    flatForward.normalized,
                    Vector3.up);
            }
        }

        private void Update()
        {
            if (bound && boundMagazine == null)
            {
                EndReading();
                bound = false;
                boundBody = null;
                boundRenderers = Array.Empty<Renderer>();
                boundRendererStates = Array.Empty<bool>();
                boundColliders = Array.Empty<Collider>();
                boundColliderStates = Array.Empty<bool>();
                transform.SetPositionAndRotation(
                    fallbackPosition,
                    fallbackRotation);
                if (anchorCollider != null)
                {
                    anchorCollider.enabled = true;
                }

                fallbackVisual?.SetActive(catalogVisible);
                nextAttemptTime = 0f;
            }

            if (bound)
            {
                SynchronizePose();
                return;
            }

            if (Time.unscaledTime < nextAttemptTime)
            {
                return;
            }

            nextAttemptTime = Time.unscaledTime + RetrySeconds;
            TryBind();
        }

        private void TryBind()
        {
            if (string.IsNullOrWhiteSpace(stableEntityId))
            {
                return;
            }

            WorldItemInstance[] items = FindObjectsByType<WorldItemInstance>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            foreach (WorldItemInstance item in items)
            {
                if (item == null || !string.Equals(
                        item.StableId.Value,
                        stableEntityId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                PhysicsPickupTarget pickup = item.GetComponent<
                    PhysicsPickupTarget>();
                pickup?.SetPickupEnabled(true);
                InteractionTargetHost host = item.GetComponent<
                    InteractionTargetHost>();
                if (host != null && catalogCapability != null)
                {
                    host.AddCapabilityFirst(catalogCapability);
                }

                if (fallbackVisual != null)
                {
                    fallbackVisual.SetActive(false);
                }

                boundMagazine = item;
                boundBody = item.GetComponent<Rigidbody>();
                boundRenderers = item.GetComponentsInChildren<Renderer>(true);
                boundRendererStates = new bool[boundRenderers.Length];
                for (int index = 0; index < boundRenderers.Length; index++)
                {
                    boundRendererStates[index] =
                        boundRenderers[index] != null &&
                        boundRenderers[index].enabled;
                }

                boundColliders = item.GetComponentsInChildren<Collider>(true);
                boundColliderStates = new bool[boundColliders.Length];
                for (int index = 0; index < boundColliders.Length; index++)
                {
                    boundColliderStates[index] =
                        boundColliders[index] != null &&
                        boundColliders[index].enabled;
                }

                bound = true;
                if (anchorCollider != null)
                {
                    anchorCollider.enabled = false;
                }

                SynchronizePose();
                SetCatalogVisible(catalogVisible);
                return;
            }
        }

        private void OnDisable()
        {
            EndReading();
        }
    }
}
