using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.Items
{
    [CreateAssetMenu(
        fileName = "ItemDefinitionCatalog",
        menuName = "MSC/Items/Definition Catalog")]
    public sealed class ItemDefinitionCatalog : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string catalogId = "phase1.items.09b.v1";
        [SerializeField] private ItemDefinitionRecord[] definitions =
            Array.Empty<ItemDefinitionRecord>();

        private Dictionary<string, ItemDefinitionRecord> byId;

        public int SchemaVersion => schemaVersion;
        public string CatalogId => catalogId;
        public IReadOnlyList<ItemDefinitionRecord> Definitions =>
            definitions ?? Array.Empty<ItemDefinitionRecord>();

        public bool TryGet(string definitionId, out ItemDefinitionRecord definition)
        {
            EnsureIndex();
            return byId.TryGetValue(definitionId ?? string.Empty, out definition);
        }

        public IReadOnlyList<string> ValidateConfiguration()
        {
            var failures = new List<string>();
            if (schemaVersion != CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(catalogId))
            {
                failures.Add("Item definition catalog schema or ID is invalid.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var featureIds = new HashSet<string>(StringComparer.Ordinal);
            var replacementKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (ItemDefinitionRecord definition in Definitions)
            {
                string failure = string.Empty;
                if (definition == null || !definition.TryValidate(out failure))
                {
                    failures.Add(failure ?? "Item definition is missing.");
                    continue;
                }

                if (!ids.Add(definition.DefinitionId))
                {
                    failures.Add($"Duplicate item definition ID '{definition.DefinitionId}'.");
                }

                if (!featureIds.Add(definition.FeatureId))
                {
                    failures.Add($"Duplicate item feature ID '{definition.FeatureId}'.");
                }

                if (!replacementKeys.Add(definition.ReplacementKey))
                {
                    failures.Add($"Duplicate item replacement key '{definition.ReplacementKey}'.");
                }
            }

            foreach (ItemDefinitionRecord definition in Definitions)
            {
                if (definition != null &&
                    !string.IsNullOrEmpty(definition.ChildDefinitionId) &&
                    !ids.Contains(definition.ChildDefinitionId))
                {
                    failures.Add(
                        $"Item '{definition.DefinitionId}' references unknown child " +
                        $"'{definition.ChildDefinitionId}'.");
                }

                if (definition != null &&
                    !string.IsNullOrEmpty(definition.ProducedDefinitionId) &&
                    !ids.Contains(definition.ProducedDefinitionId))
                {
                    failures.Add(
                        $"Item '{definition.DefinitionId}' references unknown output " +
                        $"'{definition.ProducedDefinitionId}'.");
                }

                if (definition?.Combustion.IsConfigured == true &&
                    !ids.Contains(
                        definition.Combustion.AcceptedFuelDefinitionId))
                {
                    failures.Add(
                        $"Item '{definition.DefinitionId}' references unknown " +
                        $"fuel '{definition.Combustion.AcceptedFuelDefinitionId}'.");
                }
            }

            return failures;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredCatalogId,
            ItemDefinitionRecord[] configuredDefinitions)
        {
            schemaVersion = CurrentSchemaVersion;
            catalogId = configuredCatalogId ?? string.Empty;
            definitions = configuredDefinitions ?? Array.Empty<ItemDefinitionRecord>();
            byId = null;
        }
#endif

        private void EnsureIndex()
        {
            if (byId != null)
            {
                return;
            }

            byId = Definitions
                .Where(definition => definition != null)
                .ToDictionary(
                    definition => definition.DefinitionId,
                    StringComparer.Ordinal);
        }
    }
}
