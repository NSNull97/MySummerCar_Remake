using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Items
{
    [CreateAssetMenu(
        fileName = "ItemPlacementCatalog",
        menuName = "MSC/Items/Placement Catalog")]
    public sealed class ItemPlacementCatalog : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string catalogId = "phase1.item-placements.09b.v1";
        [SerializeField] private string donorRevision = string.Empty;
        [SerializeField] private ItemPlacementRecord[] placements =
            Array.Empty<ItemPlacementRecord>();

        public int SchemaVersion => schemaVersion;
        public string CatalogId => catalogId;
        public string DonorRevision => donorRevision;
        public IReadOnlyList<ItemPlacementRecord> Placements =>
            placements ?? Array.Empty<ItemPlacementRecord>();

        public IReadOnlyList<string> ValidateConfiguration(
            ItemDefinitionCatalog definitions)
        {
            var failures = new List<string>();
            if (schemaVersion != CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(catalogId) ||
                string.IsNullOrWhiteSpace(donorRevision))
            {
                failures.Add("Item placement catalog schema, ID, or donor revision is invalid.");
            }

            if (definitions == null)
            {
                failures.Add("Item placement catalog has no definition catalog.");
                return failures;
            }

            var placementIds = new HashSet<string>(StringComparer.Ordinal);
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemPlacementRecord placement in Placements)
            {
                string failure = string.Empty;
                if (placement == null || !placement.TryValidate(out failure))
                {
                    failures.Add(failure ?? "Item placement is missing.");
                    continue;
                }

                if (!placementIds.Add(placement.PlacementId))
                {
                    failures.Add($"Duplicate item placement ID '{placement.PlacementId}'.");
                }

                if (!stableIds.Add(placement.StableEntityId))
                {
                    failures.Add($"Duplicate item stable ID '{placement.StableEntityId}'.");
                }

                if (!definitions.TryGet(
                        placement.DefinitionId,
                        out ItemDefinitionRecord definition) ||
                    !definition.Required)
                {
                    failures.Add(
                        $"Placement '{placement.PlacementId}' references an " +
                        "unknown or non-required definition.");
                }
            }

            return failures;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredCatalogId,
            string configuredDonorRevision,
            ItemPlacementRecord[] configuredPlacements)
        {
            schemaVersion = CurrentSchemaVersion;
            catalogId = configuredCatalogId ?? string.Empty;
            donorRevision = configuredDonorRevision ?? string.Empty;
            placements = configuredPlacements ?? Array.Empty<ItemPlacementRecord>();
        }
#endif
    }
}
