using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.Traffic
{
    [Serializable]
    public sealed class TrafficPresentationCatalogEntry
    {
        [SerializeField] private string presentationId = string.Empty;
        [SerializeField] private string productionReplacementKey = string.Empty;
        [SerializeField] private GameObject wrapperPrefab;

        public TrafficPresentationCatalogEntry(
            string configuredPresentationId,
            string configuredProductionReplacementKey,
            GameObject configuredWrapperPrefab)
        {
            presentationId = configuredPresentationId?.Trim() ?? string.Empty;
            productionReplacementKey =
                configuredProductionReplacementKey?.Trim() ?? string.Empty;
            wrapperPrefab = configuredWrapperPrefab;
        }

        public string PresentationId => presentationId;
        public string ProductionReplacementKey => productionReplacementKey;
        public GameObject WrapperPrefab => wrapperPrefab;
    }

    [CreateAssetMenu(
        fileName = "TrafficPresentationCatalog",
        menuName = "My Summer Car/Traffic/Presentation Catalog")]
    public sealed class TrafficPresentationCatalog : ScriptableObject
    {
        [SerializeField] private TrafficPresentationCatalogEntry[] entries =
            Array.Empty<TrafficPresentationCatalogEntry>();

        private Dictionary<string, TrafficPresentationCatalogEntry> byId;

        public IReadOnlyList<TrafficPresentationCatalogEntry> Entries => entries;

        public bool TryGet(
            string presentationId,
            out TrafficPresentationCatalogEntry entry)
        {
            EnsureIndex();
            return byId.TryGetValue(presentationId ?? string.Empty, out entry);
        }

        public bool TryValidate(out string failure)
        {
            if (entries == null || entries.Length == 0)
            {
                failure = "Traffic presentation catalog is empty.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (TrafficPresentationCatalogEntry entry in entries)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.PresentationId) ||
                    !entry.PresentationId.StartsWith(
                        "presentation.traffic.",
                        StringComparison.Ordinal) ||
                    !ids.Add(entry.PresentationId) ||
                    string.IsNullOrWhiteSpace(entry.ProductionReplacementKey) ||
                    entry.WrapperPrefab == null)
                {
                    failure = "Traffic presentation catalog entry is invalid.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private void EnsureIndex()
        {
            byId ??= (entries ?? Array.Empty<TrafficPresentationCatalogEntry>())
                .Where(value => value != null)
                .ToDictionary(
                    value => value.PresentationId,
                    StringComparer.Ordinal);
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            IEnumerable<TrafficPresentationCatalogEntry> configuredEntries)
        {
            entries = (configuredEntries ??
                    Enumerable.Empty<TrafficPresentationCatalogEntry>())
                .OrderBy(value => value.PresentationId, StringComparer.Ordinal)
                .ToArray();
            byId = null;
        }
#endif
    }
}
