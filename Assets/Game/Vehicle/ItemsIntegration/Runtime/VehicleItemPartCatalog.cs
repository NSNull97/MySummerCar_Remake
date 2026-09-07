using System;
using System.Collections.Generic;
using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle.ItemsIntegration
{
    [Serializable]
    public sealed class VehicleItemPartRecord
    {
        [SerializeField] private string itemDefinitionId = string.Empty;
        [SerializeField] private PartDefinition partDefinition;
        public string ItemDefinitionId => itemDefinitionId;
        public PartDefinition PartDefinition => partDefinition;

        public VehicleItemPartRecord(string itemId, PartDefinition part)
        {
            itemDefinitionId = itemId ?? string.Empty;
            partDefinition = part;
        }
    }

    /// <summary>Reviewed stock consumables only. Mail-order delivery remains a separate catalog.</summary>
    [CreateAssetMenu(menuName = "MSC/Vehicle Assembly/Item Part Catalog")]
    public sealed class VehicleItemPartCatalog : ScriptableObject
    {
        [SerializeField] private VehicleItemPartRecord[] records = Array.Empty<VehicleItemPartRecord>();
        public IReadOnlyList<VehicleItemPartRecord> Records => records;

        public void Configure(VehicleItemPartRecord[] configuredRecords)
        {
            Validate(configuredRecords);
            records = (VehicleItemPartRecord[])configuredRecords.Clone();
        }

        public bool TryValidate(out string failure)
        {
            try { Validate(records); failure = string.Empty; return true; }
            catch (ArgumentException exception) { failure = exception.Message; return false; }
        }

        public bool TryResolve(string itemDefinitionId, out PartDefinition definition)
        {
            foreach (VehicleItemPartRecord record in records ?? Array.Empty<VehicleItemPartRecord>())
                if (record != null && string.Equals(record.ItemDefinitionId, itemDefinitionId, StringComparison.Ordinal) &&
                    record.PartDefinition != null && record.PartDefinition.DefinitionId == ExpectedPartDefinitionId(itemDefinitionId))
                {
                    definition = record.PartDefinition;
                    return true;
                }
            definition = null;
            return false;
        }

        public PartDefinition[] GetPartDefinitions()
        {
            Validate(records);
            var result = new PartDefinition[records.Length];
            for (int index = 0; index < records.Length; index++) result[index] = records[index].PartDefinition;
            return result;
        }

        public static string ExpectedPartDefinitionId(string itemDefinitionId) => itemDefinitionId switch
        {
            "item.spark-plug" => "vehicle.satsuma.part.spark-plug",
            "item.alternator-belt" => "vehicle.satsuma.part.alternator-belt",
            "item.oil-filter" => "vehicle.satsuma.part.oilfilter0",
            "item.light-bulb" => "vehicle.satsuma.part.light-bulb",
            _ => string.Empty,
        };

        private static void Validate(VehicleItemPartRecord[] candidates)
        {
            if (candidates == null || candidates.Length == 0 || candidates.Length > 4)
                throw new ArgumentException("A consumable catalog requires one to four reviewed mappings.");
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            var partIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (VehicleItemPartRecord record in candidates)
            {
                string expected = ExpectedPartDefinitionId(record?.ItemDefinitionId);
                if (record == null || expected.Length == 0 || record.PartDefinition == null ||
                    record.PartDefinition.DefinitionId != expected || !itemIds.Add(record.ItemDefinitionId) ||
                    !partIds.Add(expected))
                    throw new ArgumentException("Consumable mapping is unknown, duplicated or references the wrong part definition.");
            }
        }
    }
}
