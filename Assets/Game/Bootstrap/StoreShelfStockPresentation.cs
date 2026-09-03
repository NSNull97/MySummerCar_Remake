using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Items.Presentation;
using MSC.LegacyImport;
using MSC.Services;
using UnityEngine;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Connects one project-owned store offer to the reviewed donor shelf
    /// presentation. The service runtime owns stock; donor objects are only
    /// removable Phase-1 visuals and collider bounds.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoreShelfStockPresentation : MonoBehaviour
    {
        private const float RetryIntervalSeconds = 0.5f;
        private const float ColliderPaddingMeters = 0.025f;

        private readonly List<StockVisualUnit> stockUnits = new();
        private ServiceRuntime runtime;
        private string offerId = string.Empty;
        private string sourceGroupStableId = string.Empty;
        private string sourceUnitStableId = string.Empty;
        private Collider fallbackCollider;
        private float nextBindAttemptTime;
        private bool bound;
        private bool usesProjectOwnedVisuals;
        private bool usesExactAuthoredCollider;
        private int stockIndexOffset;
        private Func<IReadOnlyList<GameObject>> projectVisualRebuildFactory;

        public bool IsBound => bound;
        public int BoundUnitCount => stockUnits.Count;
        public string OfferId => offerId;
        public string SourceGroupStableId => sourceGroupStableId;
        public string SourceUnitStableId => sourceUnitStableId;
        public bool UsesProjectOwnedVisuals => usesProjectOwnedVisuals;
        public bool UsesExactAuthoredCollider => usesExactAuthoredCollider;
        public int StockIndexOffset => stockIndexOffset;

        public void Configure(
            ServiceRuntime configuredRuntime,
            string configuredOfferId,
            string configuredSourceGroupStableId,
            string configuredSourceUnitStableId,
            Collider configuredFallbackCollider)
        {
            if (configuredRuntime == null || !configuredRuntime.IsInitialized)
            {
                throw new ArgumentException(
                    "Store stock presentation requires an initialized service runtime.",
                    nameof(configuredRuntime));
            }

            if (string.IsNullOrWhiteSpace(configuredOfferId) ||
                (string.IsNullOrWhiteSpace(configuredSourceGroupStableId) &&
                 string.IsNullOrWhiteSpace(configuredSourceUnitStableId)))
            {
                throw new ArgumentException(
                    "Store stock presentation requires an offer and reviewed source identity.");
            }

            Unsubscribe();
            runtime = configuredRuntime;
            offerId = configuredOfferId;
            sourceGroupStableId = configuredSourceGroupStableId ?? string.Empty;
            sourceUnitStableId = configuredSourceUnitStableId ?? string.Empty;
            fallbackCollider = configuredFallbackCollider;
            usesProjectOwnedVisuals = false;
            usesExactAuthoredCollider = false;
            stockIndexOffset = 0;
            runtime.StateChanged += HandleStateChanged;
            nextBindAttemptTime = 0f;
            TryBindVisuals();
        }

        public void ConfigureProjectOwned(
            ServiceRuntime configuredRuntime,
            string configuredOfferId,
            IReadOnlyList<GameObject> configuredStockUnits,
            Collider configuredFallbackCollider,
            Func<IReadOnlyList<GameObject>> configuredRebuildFactory = null)
        {
            if (configuredRuntime == null || !configuredRuntime.IsInitialized)
            {
                throw new ArgumentException(
                    "Store stock presentation requires an initialized service runtime.",
                    nameof(configuredRuntime));
            }

            if (string.IsNullOrWhiteSpace(configuredOfferId) ||
                configuredStockUnits == null ||
                !configuredRuntime.Catalog.TryGetOffer(
                    configuredOfferId,
                    out ServiceOfferDefinition offer) ||
                offer.Kind != ServiceOfferKind.RetailItem ||
                configuredStockUnits.Count != offer.StockCapacity ||
                configuredStockUnits.Any(value => value == null))
            {
                throw new ArgumentException(
                    "Project-owned shelf presentation must provide one visual " +
                    "unit for every stock unit of a retail offer.");
            }

            Unsubscribe();
            runtime = configuredRuntime;
            offerId = configuredOfferId;
            sourceGroupStableId = string.Empty;
            sourceUnitStableId = string.Empty;
            fallbackCollider = configuredFallbackCollider;
            usesProjectOwnedVisuals = true;
            usesExactAuthoredCollider = false;
            stockIndexOffset = 0;
            projectVisualRebuildFactory = configuredRebuildFactory;
            BindProjectOwnedUnits(configuredStockUnits);

            if (!TryConfigureBoundsCollider())
            {
                throw new InvalidOperationException(
                    $"Project-owned shelf presentation '{configuredOfferId}' " +
                    "has no renderer bounds.");
            }

            bound = true;
            runtime.StateChanged += HandleStateChanged;
            if (projectVisualRebuildFactory != null)
            {
                ItemPresentationProviderHub.ProviderChanged +=
                    HandleItemPresentationProviderChanged;
            }
            RefreshVisibleStock();
        }

        public void ConfigureExactProjectOwnedLayout(
            ServiceRuntime configuredRuntime,
            string configuredOfferId,
            IReadOnlyList<GameObject> configuredStockUnits,
            Collider configuredInteractionCollider,
            int configuredStockIndexOffset)
        {
            if (configuredRuntime == null || !configuredRuntime.IsInitialized)
            {
                throw new ArgumentException(
                    "Store stock presentation requires an initialized " +
                    "service runtime.",
                    nameof(configuredRuntime));
            }

            if (string.IsNullOrWhiteSpace(configuredOfferId) ||
                configuredStockUnits == null ||
                configuredInteractionCollider == null ||
                configuredStockIndexOffset < 0 ||
                !configuredRuntime.Catalog.TryGetOffer(
                    configuredOfferId,
                    out ServiceOfferDefinition offer) ||
                offer.Kind != ServiceOfferKind.RetailItem ||
                configuredStockIndexOffset + configuredStockUnits.Count >
                offer.StockCapacity ||
                configuredStockUnits.Any(value => value == null))
            {
                throw new ArgumentException(
                    "Exact shelf presentation must provide a valid authored " +
                    "collider and a non-overflowing stock range.");
            }

            Unsubscribe();
            runtime = configuredRuntime;
            offerId = configuredOfferId;
            sourceGroupStableId = string.Empty;
            sourceUnitStableId = string.Empty;
            fallbackCollider = configuredInteractionCollider;
            usesProjectOwnedVisuals = true;
            usesExactAuthoredCollider = true;
            stockIndexOffset = configuredStockIndexOffset;
            projectVisualRebuildFactory = null;
            BindProjectOwnedUnits(configuredStockUnits);
            configuredInteractionCollider.enabled = true;
            bound = true;
            runtime.StateChanged += HandleStateChanged;
            RefreshVisibleStock();
        }

        private void BindProjectOwnedUnits(
            IReadOnlyList<GameObject> configuredStockUnits)
        {
            stockUnits.Clear();
            for (int index = 0; index < configuredStockUnits.Count; index++)
            {
                stockUnits.Add(new StockVisualUnit(
                    new[] { configuredStockUnits[index] }));
            }
        }

        private void HandleItemPresentationProviderChanged(
            IItemPresentationProvider provider)
        {
            if (provider == null || projectVisualRebuildFactory == null ||
                runtime == null || !usesProjectOwnedVisuals ||
                !runtime.Catalog.TryGetOffer(
                    offerId,
                    out ServiceOfferDefinition offer))
            {
                return;
            }

            IReadOnlyList<GameObject> replacementUnits =
                projectVisualRebuildFactory();
            if (replacementUnits == null ||
                replacementUnits.Count != offer.StockCapacity ||
                replacementUnits.Any(value => value == null))
            {
                return;
            }

            DestroyProjectOwnedUnits();
            BindProjectOwnedUnits(replacementUnits);
            if (!TryConfigureBoundsCollider())
            {
                throw new InvalidOperationException(
                    $"Rebuilt shelf presentation '{offerId}' has no " +
                    "renderer bounds.");
            }

            bound = true;
            RefreshVisibleStock();
        }

        private void DestroyProjectOwnedUnits()
        {
            foreach (StockVisualUnit unit in stockUnits)
            {
                foreach (GameObject member in unit.Members)
                {
                    if (member == null)
                    {
                        continue;
                    }

                    member.SetActive(false);
                    if (Application.isPlaying)
                    {
                        Destroy(member);
                    }
                    else
                    {
                        DestroyImmediate(member);
                    }
                }
            }

            stockUnits.Clear();
        }

        private void Update()
        {
            if (bound && stockUnits.Any(unit => unit.HasMissingMember))
            {
                ResetBindingAfterStreamingUnload();
            }

            if (bound || runtime == null ||
                Time.unscaledTime < nextBindAttemptTime)
            {
                return;
            }

            nextBindAttemptTime = Time.unscaledTime + RetryIntervalSeconds;
            TryBindVisuals();
        }

        private void ResetBindingAfterStreamingUnload()
        {
            bound = false;
            stockUnits.Clear();
            BoxCollider boundsCollider = GetComponent<BoxCollider>();
            if (boundsCollider != null && boundsCollider != fallbackCollider)
            {
                boundsCollider.enabled = false;
            }

            if (fallbackCollider != null)
            {
                fallbackCollider.enabled = true;
            }

            StoreShelfStockVisualIndex.Invalidate();
            nextBindAttemptTime = 0f;
        }

        private void TryBindVisuals()
        {
            IReadOnlyList<DonorWorldBaselineEntityMetadata> matches =
                StoreShelfStockVisualIndex.Find(
                    sourceGroupStableId,
                    sourceUnitStableId);
            if (matches.Count == 0)
            {
                return;
            }

            stockUnits.Clear();
            foreach (DonorWorldBaselineEntityMetadata metadata in matches
                         .Where(value => value != null)
                         .OrderBy(value => value.StableId, StringComparer.Ordinal))
            {
                IReadOnlyList<DonorWorldBaselineEntityMetadata> visualMembers =
                    StoreShelfStockVisualIndex.ExpandVisualUnit(metadata);
                var members = new List<GameObject>(visualMembers.Count);
                for (int memberIndex = 0;
                     memberIndex < visualMembers.Count;
                     memberIndex++)
                {
                    DonorWorldBaselineEntityMetadata member =
                        visualMembers[memberIndex];
                    if (member != null &&
                        member.GetComponentInChildren<Renderer>(true) != null)
                    {
                        members.Add(member.gameObject);
                    }
                }

                if (members.Count > 0)
                {
                    stockUnits.Add(new StockVisualUnit(members));
                }
            }

            if (stockUnits.Count == 0 || !TryConfigureBoundsCollider())
            {
                stockUnits.Clear();
                return;
            }

            bound = true;
            RefreshVisibleStock();
        }

        private bool TryConfigureBoundsCollider()
        {
            bool hasBounds = false;
            Bounds localBounds = default;
            for (int unitIndex = 0; unitIndex < stockUnits.Count; unitIndex++)
            {
                IReadOnlyList<GameObject> members = stockUnits[unitIndex].Members;
                for (int memberIndex = 0;
                     memberIndex < members.Count;
                     memberIndex++)
                {
                    GameObject member = members[memberIndex];
                    if (member == null)
                    {
                        continue;
                    }

                    Renderer[] renderers = member.GetComponentsInChildren<
                        Renderer>(true);
                    for (int rendererIndex = 0;
                         rendererIndex < renderers.Length;
                         rendererIndex++)
                    {
                        Renderer renderer = renderers[rendererIndex];
                        if (renderer == null)
                        {
                            continue;
                        }

                        Bounds worldBounds = renderer.bounds;
                        for (int corner = 0; corner < 8; corner++)
                        {
                            Vector3 worldCorner = new(
                                (corner & 1) == 0
                                    ? worldBounds.min.x
                                    : worldBounds.max.x,
                                (corner & 2) == 0
                                    ? worldBounds.min.y
                                    : worldBounds.max.y,
                                (corner & 4) == 0
                                    ? worldBounds.min.z
                                    : worldBounds.max.z);
                            Vector3 localCorner = transform.InverseTransformPoint(
                                worldCorner);
                            if (!hasBounds)
                            {
                                localBounds = new Bounds(
                                    localCorner,
                                    Vector3.zero);
                                hasBounds = true;
                            }
                            else
                            {
                                localBounds.Encapsulate(localCorner);
                            }
                        }
                    }
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            BoxCollider boundsCollider = GetComponent<BoxCollider>();
            if (boundsCollider == null)
            {
                boundsCollider = gameObject.AddComponent<BoxCollider>();
            }
            boundsCollider.center = localBounds.center;
            boundsCollider.size = new Vector3(
                Mathf.Max(0.05f, localBounds.size.x + ColliderPaddingMeters * 2f),
                Mathf.Max(0.05f, localBounds.size.y + ColliderPaddingMeters * 2f),
                Mathf.Max(0.05f, localBounds.size.z + ColliderPaddingMeters * 2f));
            boundsCollider.isTrigger = false;
            boundsCollider.enabled = true;
            if (fallbackCollider != null && fallbackCollider != boundsCollider)
            {
                fallbackCollider.enabled = false;
            }

            return true;
        }

        private void HandleStateChanged(ulong _) => RefreshVisibleStock();

        private void RefreshVisibleStock()
        {
            if (!bound || runtime == null)
            {
                return;
            }

            int remainingStock = runtime.GetRemainingStock(offerId);
            for (int index = 0; index < stockUnits.Count; index++)
            {
                stockUnits[index].SetActive(
                    stockIndexOffset + index < remainingStock);
            }
        }

        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            ItemPresentationProviderHub.ProviderChanged -=
                HandleItemPresentationProviderChanged;
            projectVisualRebuildFactory = null;
            if (runtime != null)
            {
                runtime.StateChanged -= HandleStateChanged;
                runtime = null;
            }
        }

        private sealed class StockVisualUnit
        {
            private readonly IReadOnlyList<GameObject> members;

            public StockVisualUnit(IReadOnlyList<GameObject> configuredMembers)
            {
                members = configuredMembers ?? Array.Empty<GameObject>();
            }

            public IReadOnlyList<GameObject> Members => members;

            public bool HasMissingMember
            {
                get
                {
                    for (int index = 0; index < members.Count; index++)
                    {
                        if (members[index] == null)
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }

            public void SetActive(bool active)
            {
                // Cellization can flatten donor child renderers into siblings.
                // Toggle every provenance descendant so a beer crate never
                // leaves its bottle mesh behind when stock is consumed.
                for (int index = 0; index < members.Count; index++)
                {
                    GameObject member = members[index];
                    if (member != null && member.activeSelf != active)
                    {
                        member.SetActive(active);
                    }
                }
            }
        }
    }

    internal static class StoreShelfStockVisualIndex
    {
        private const float RefreshIntervalSeconds = 0.5f;
        private static DonorWorldBaselineEntityMetadata[] entities =
            Array.Empty<DonorWorldBaselineEntityMetadata>();
        private static readonly Dictionary<string,
            DonorWorldBaselineEntityMetadata> byStableId =
            new(StringComparer.Ordinal);
        private static readonly Dictionary<string,
            List<DonorWorldBaselineEntityMetadata>> byParentStableId =
            new(StringComparer.Ordinal);
        private static float nextRefreshTime;

        public static IReadOnlyList<DonorWorldBaselineEntityMetadata> Find(
            string sourceGroupStableId,
            string sourceUnitStableId)
        {
            if (entities.Length == 0 || Time.unscaledTime >= nextRefreshTime)
            {
                RefreshIndex();
            }

            if (!string.IsNullOrEmpty(sourceUnitStableId))
            {
                return byStableId.TryGetValue(
                    sourceUnitStableId,
                    out DonorWorldBaselineEntityMetadata unit)
                        ? new[] { unit }
                        : Array.Empty<DonorWorldBaselineEntityMetadata>();
            }

            return byParentStableId.TryGetValue(
                sourceGroupStableId,
                out List<DonorWorldBaselineEntityMetadata> children)
                    ? children
                    : Array.Empty<DonorWorldBaselineEntityMetadata>();
        }

        public static IReadOnlyList<DonorWorldBaselineEntityMetadata>
            ExpandVisualUnit(DonorWorldBaselineEntityMetadata root)
        {
            if (root == null)
            {
                return Array.Empty<DonorWorldBaselineEntityMetadata>();
            }

            var result = new List<DonorWorldBaselineEntityMetadata>();
            var pending = new Queue<DonorWorldBaselineEntityMetadata>();
            var visited = new HashSet<string>(StringComparer.Ordinal);
            pending.Enqueue(root);
            while (pending.Count > 0)
            {
                DonorWorldBaselineEntityMetadata current = pending.Dequeue();
                if (current == null ||
                    !visited.Add(current.StableId ?? string.Empty))
                {
                    continue;
                }

                result.Add(current);
                if (!byParentStableId.TryGetValue(
                        current.StableId,
                        out List<DonorWorldBaselineEntityMetadata> children))
                {
                    continue;
                }

                for (int index = 0; index < children.Count; index++)
                {
                    pending.Enqueue(children[index]);
                }
            }

            return result;
        }

        private static void RefreshIndex()
        {
            entities = UnityEngine.Object.FindObjectsByType<
                DonorWorldBaselineEntityMetadata>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            byStableId.Clear();
            byParentStableId.Clear();
            for (int index = 0; index < entities.Length; index++)
            {
                DonorWorldBaselineEntityMetadata entity = entities[index];
                if (entity == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entity.StableId))
                {
                    byStableId[entity.StableId] = entity;
                }

                if (string.IsNullOrWhiteSpace(entity.SourceParentStableId))
                {
                    continue;
                }

                if (!byParentStableId.TryGetValue(
                        entity.SourceParentStableId,
                        out List<DonorWorldBaselineEntityMetadata> siblings))
                {
                    siblings = new List<DonorWorldBaselineEntityMetadata>();
                    byParentStableId.Add(entity.SourceParentStableId, siblings);
                }

                siblings.Add(entity);
            }

            nextRefreshTime = Time.unscaledTime + RefreshIntervalSeconds;
        }

        public static void Invalidate()
        {
            entities = Array.Empty<DonorWorldBaselineEntityMetadata>();
            byStableId.Clear();
            byParentStableId.Clear();
            nextRefreshTime = 0f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Invalidate();
        }
    }
}
