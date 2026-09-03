using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Items.Presentation
{
    [Serializable]
    public sealed class ItemPresentationBinding
    {
        [SerializeField] private string definitionId = string.Empty;
        [SerializeField] private string replacementKey = string.Empty;
        [SerializeField] private int variantIndex = -1;
        [SerializeField] private GameObject visualPrefab;

        public string DefinitionId => definitionId;
        public string ReplacementKey => replacementKey;
        public int VariantIndex => variantIndex;
        public GameObject VisualPrefab => visualPrefab;

#if UNITY_EDITOR
        public void Configure(
            string configuredDefinitionId,
            string configuredReplacementKey,
            GameObject configuredVisualPrefab,
            int configuredVariantIndex = -1)
        {
            definitionId = configuredDefinitionId ?? string.Empty;
            replacementKey = configuredReplacementKey ?? string.Empty;
            variantIndex = configuredVariantIndex;
            visualPrefab = configuredVisualPrefab;
        }
#endif
    }

    public interface IItemPresentationProvider
    {
        bool TryInstantiate(
            ItemDefinitionRecord definition,
            Transform parent,
            out GameObject visualRoot);
    }

    /// <summary>
    /// Project-owned state binding implemented by generated presentation
    /// wrappers that need the authoritative world-item instance. This keeps
    /// the provider data-driven as new visual mechanics are added.
    /// </summary>
    public interface IWorldItemPresentationBinding
    {
        void Bind(WorldItemInstance owner);
    }

    /// <summary>
    /// Narrow composition handshake used by the ignored private RuntimeBaseline
    /// scene. Gameplay never searches donor hierarchy names or asset paths.
    /// </summary>
    public static class ItemPresentationProviderHub
    {
        private static IItemPresentationProvider current;

        public static event Action<IItemPresentationProvider> ProviderChanged;

        public static IItemPresentationProvider Current => current;

        internal static void Register(IItemPresentationProvider provider)
        {
            if (provider == null)
            {
                throw new ArgumentNullException(nameof(provider));
            }

            if (current != null && !ReferenceEquals(current, provider))
            {
                throw new InvalidOperationException(
                    "Only one item presentation provider may own the active " +
                    "private runtime baseline.");
            }

            current = provider;
            ProviderChanged?.Invoke(current);
        }

        internal static void Unregister(IItemPresentationProvider provider)
        {
            if (!ReferenceEquals(current, provider))
            {
                return;
            }

            current = null;
            ProviderChanged?.Invoke(null);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            current = null;
            ProviderChanged = null;
        }
    }

    [DisallowMultipleComponent]
    public sealed class ItemPresentationProvider : MonoBehaviour,
        IItemPresentationProvider
    {
        [SerializeField] private string donorRevision = string.Empty;
        [SerializeField] private string classification =
            "TemporaryDirectImport";
        [SerializeField] private ItemPresentationBinding[] bindings =
            Array.Empty<ItemPresentationBinding>();

        private Dictionary<string, ItemPresentationBinding> byPresentationKey;

        public string DonorRevision => donorRevision;
        public string Classification => classification;
        public IReadOnlyList<ItemPresentationBinding> Bindings =>
            bindings ?? Array.Empty<ItemPresentationBinding>();

        public bool TryInstantiate(
            ItemDefinitionRecord definition,
            Transform parent,
            out GameObject visualRoot)
        {
            if (definition == null || parent == null)
            {
                visualRoot = null;
                return false;
            }

            EnsureIndex();
            int variantIndex = ResolveVariantIndex(parent);
            if (!byPresentationKey.TryGetValue(
                    CreatePresentationKey(
                        definition.DefinitionId,
                        variantIndex),
                    out ItemPresentationBinding binding) &&
                !byPresentationKey.TryGetValue(
                    CreatePresentationKey(definition.DefinitionId, -1),
                    out binding))
            {
                return TryInstantiateContainedBottle(
                    definition,
                    parent,
                    out visualRoot);
            }

            if (binding.VisualPrefab == null ||
                !string.Equals(
                    binding.ReplacementKey,
                    definition.ReplacementKey,
                    StringComparison.Ordinal))
            {
                visualRoot = null;
                return false;
            }

            visualRoot = Instantiate(binding.VisualPrefab, parent, false);
            visualRoot.name = "Temporary item presentation";
            WorldItemInstance owner = parent.GetComponent<WorldItemInstance>();
            MonoBehaviour[] presentationBehaviours =
                visualRoot.GetComponentsInChildren<MonoBehaviour>(true);
            for (int index = 0;
                 index < presentationBehaviours.Length;
                 index++)
            {
                if (presentationBehaviours[index] is
                    IWorldItemPresentationBinding stateBinding)
                {
                    stateBinding.Bind(owner);
                }
            }

            HelmetPaintPresentation helmetPaint =
                visualRoot.GetComponentInChildren<HelmetPaintPresentation>(
                    true);
            if (helmetPaint != null)
            {
                helmetPaint.Bind(owner);
            }

            SpannerSetPresentationController spannerSet =
                visualRoot.GetComponentInChildren<
                    SpannerSetPresentationController>(true);
            if (spannerSet != null)
            {
                spannerSet.Bind(owner);
            }

            if (string.Equals(
                    definition.DefinitionId,
                    BeerCaseContentsPresentation.BeerCaseDefinitionId,
                    StringComparison.Ordinal))
            {
                BeerCaseContentsPresentation contents =
                    visualRoot.AddComponent<BeerCaseContentsPresentation>();
                contents.Configure(
                    owner,
                    definition,
                    visualRoot);
            }

            return true;
        }

        private bool TryInstantiateContainedBottle(
            ItemDefinitionRecord definition,
            Transform parent,
            out GameObject visualRoot)
        {
            visualRoot = null;
            if (!string.Equals(
                    definition.DefinitionId,
                    BeerCaseContentsPresentation.BeerBottleDefinitionId,
                    StringComparison.Ordinal) ||
                !byPresentationKey.TryGetValue(
                    CreatePresentationKey(
                        BeerCaseContentsPresentation.BeerCaseDefinitionId,
                        -1),
                    out ItemPresentationBinding caseBinding) ||
                caseBinding.VisualPrefab == null)
            {
                return false;
            }

            GameObject caseTemplate = Instantiate(caseBinding.VisualPrefab);
            caseTemplate.SetActive(false);
            Renderer bottleRenderer =
                BeerCaseContentsPresentation.FindBottleRenderer(caseTemplate);
            if (bottleRenderer == null)
            {
                Destroy(caseTemplate);
                return false;
            }

            visualRoot = Instantiate(bottleRenderer.gameObject, parent, false);
            visualRoot.name = "Temporary beer bottle presentation";
            visualRoot.transform.localPosition = Vector3.zero;
            // The donor crate slot carries an incidental placement rotation.
            // A standalone bottle must keep its visual pivot aligned with the
            // project-owned Rigidbody so the reviewed hand grip is exact.
            visualRoot.transform.localRotation = Quaternion.identity;
            visualRoot.SetActive(true);
            Destroy(caseTemplate);
            return true;
        }

#if UNITY_EDITOR
        public void Configure(
            string configuredDonorRevision,
            ItemPresentationBinding[] configuredBindings)
        {
            donorRevision = configuredDonorRevision ?? string.Empty;
            classification = "TemporaryDirectImport";
            bindings = configuredBindings ??
                Array.Empty<ItemPresentationBinding>();
            byPresentationKey = null;
        }
#endif

        private void Awake()
        {
            ValidateOrThrow();
            ItemPresentationProviderHub.Register(this);
        }

        private void OnDestroy()
        {
            ItemPresentationProviderHub.Unregister(this);
        }

        private void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(donorRevision) ||
                !string.Equals(
                    classification,
                    "TemporaryDirectImport",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Item presentation provider provenance is invalid.");
            }

            EnsureIndex();
        }

        private void EnsureIndex()
        {
            if (byPresentationKey != null)
            {
                return;
            }

            byPresentationKey =
                new Dictionary<string, ItemPresentationBinding>(
                StringComparer.Ordinal);
            var replacementKeyOwners = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (ItemPresentationBinding binding in Bindings)
            {
                string presentationKey = binding == null
                    ? string.Empty
                    : CreatePresentationKey(
                        binding.DefinitionId,
                        binding.VariantIndex);
                bool replacementKeyConflict = binding != null &&
                    replacementKeyOwners.TryGetValue(
                        binding.ReplacementKey,
                        out string replacementKeyOwner) &&
                    !string.Equals(
                        replacementKeyOwner,
                        binding.DefinitionId,
                        StringComparison.Ordinal);
                if (binding == null ||
                    !ItemDefinitionId.IsValid(binding.DefinitionId) ||
                    string.IsNullOrWhiteSpace(binding.ReplacementKey) ||
                    binding.VariantIndex < -1 ||
                    binding.VisualPrefab == null ||
                    !byPresentationKey.TryAdd(presentationKey, binding) ||
                    replacementKeyConflict)
                {
                    throw new InvalidOperationException(
                        "Item presentation bindings contain a missing or " +
                        "duplicate project-owned identity.");
                }

                replacementKeyOwners[binding.ReplacementKey] =
                    binding.DefinitionId;
            }
        }

        private static int ResolveVariantIndex(Transform parent)
        {
            WorldItemInstance worldItem =
                parent.GetComponent<WorldItemInstance>();
            ItemInstanceState state = worldItem?.State;
            return state?.variantIndex ?? 0;
        }

        internal static string CreatePresentationKey(
            string definitionId,
            int variantIndex) =>
            (definitionId ?? string.Empty) + "\n" + variantIndex;
    }

    [DisallowMultipleComponent]
    public sealed class ItemProxyPresentation : MonoBehaviour
    {
    }

    [DisallowMultipleComponent]
    public sealed class BeerCaseContentsPresentation : MonoBehaviour
    {
        public const string BeerCaseDefinitionId = "item.beer-case";
        public const string BeerBottleDefinitionId = "item.beer-bottle";

        private const int Columns = 6;
        private const float HorizontalSpacing = 0.061f;
        private const float VerticalSpacing = 0.061f;

        private readonly List<GameObject> bottleSlots = new List<GameObject>(24);
        private WorldItemInstance owner;

        public void Configure(
            WorldItemInstance configuredOwner,
            ItemDefinitionRecord definition,
            GameObject visualRoot)
        {
            if (configuredOwner == null || definition == null ||
                visualRoot == null ||
                definition.InitialChildCount <= 0 ||
                !string.Equals(
                    definition.ChildDefinitionId,
                    BeerBottleDefinitionId,
                    StringComparison.Ordinal))
            {
                return;
            }

            Renderer bottleRenderer = FindBottleRenderer(visualRoot);
            if (bottleRenderer == null)
            {
                return;
            }

            owner = configuredOwner;
            GameObject template = bottleRenderer.gameObject;
            Transform slotParent = template.transform.parent;
            Renderer caseRenderer = FindLargestRenderer(
                visualRoot,
                bottleRenderer);
            Vector3 gridCenter = caseRenderer != null
                ? slotParent.InverseTransformPoint(caseRenderer.bounds.center)
                : Vector3.zero;
            Vector3 origin = new Vector3(
                gridCenter.x,
                gridCenter.y,
                template.transform.localPosition.z);
            int rows = Mathf.CeilToInt(
                definition.InitialChildCount / (float)Columns);
            for (int index = 0;
                 index < definition.InitialChildCount;
                 index++)
            {
                GameObject slot = index == 0
                    ? template
                    : Instantiate(template, slotParent, false);
                slot.name = $"Beer bottle slot {index + 1:00}";
                int column = index % Columns;
                int row = index / Columns;
                slot.transform.localPosition = origin + new Vector3(
                    (column - (Columns - 1) * 0.5f) * HorizontalSpacing,
                    (row - (rows - 1) * 0.5f) * VerticalSpacing,
                    0f);
                bottleSlots.Add(slot);
            }

            owner.StatusChanged += HandleStatusChanged;
            Refresh();
        }

        internal static Renderer FindBottleRenderer(GameObject visualRoot)
        {
            Renderer[] renderers =
                visualRoot != null
                    ? visualRoot.GetComponentsInChildren<Renderer>(true)
                    : Array.Empty<Renderer>();
            Renderer smallest = null;
            float smallestVolume = float.PositiveInfinity;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                Vector3 size = renderer.localBounds.size;
                float volume = Mathf.Abs(size.x * size.y * size.z);
                if (volume < smallestVolume)
                {
                    smallest = renderer;
                    smallestVolume = volume;
                }
            }

            return smallest;
        }

        private static Renderer FindLargestRenderer(
            GameObject visualRoot,
            Renderer excluded)
        {
            Renderer[] renderers =
                visualRoot != null
                    ? visualRoot.GetComponentsInChildren<Renderer>(true)
                    : Array.Empty<Renderer>();
            Renderer largest = null;
            float largestVolume = float.NegativeInfinity;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || renderer == excluded)
                {
                    continue;
                }

                Vector3 size = renderer.localBounds.size;
                float volume = Mathf.Abs(size.x * size.y * size.z);
                if (volume > largestVolume)
                {
                    largest = renderer;
                    largestVolume = volume;
                }
            }

            return largest;
        }

        private void HandleStatusChanged(IItemStatusSource _)
        {
            Refresh();
        }

        private void Refresh()
        {
            ItemInstanceState state = owner != null ? owner.State : null;
            int visibleCount = state?.containedStableIds?.Length ?? 0;
            for (int index = 0; index < bottleSlots.Count; index++)
            {
                if (bottleSlots[index] != null)
                {
                    bottleSlots[index].SetActive(index < visibleCount);
                }
            }
        }

        private void OnDestroy()
        {
            if (owner != null)
            {
                owner.StatusChanged -= HandleStatusChanged;
            }
        }
    }
}
