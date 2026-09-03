using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    public enum DeliveredPartResolutionKind
    {
        SinglePart = 0,
        MultiPartKit = 1,
        CosmeticBinding = 2,
        Tool = 3,
    }

    [Serializable]
    public sealed class VehicleDeliveredPartCompatibilityRecord
    {
        [SerializeField] private string itemDefinitionId = string.Empty;
        [SerializeField] private string targetVehicleContentId = string.Empty;
        [SerializeField] private string installationGroupId = string.Empty;
        [SerializeField] private DeliveredPartResolutionKind resolutionKind;
        [SerializeField] private string[] partDefinitionIds = Array.Empty<string>();

        public string ItemDefinitionId => itemDefinitionId;
        public string TargetVehicleContentId => targetVehicleContentId;
        public string InstallationGroupId => installationGroupId;
        public DeliveredPartResolutionKind ResolutionKind => resolutionKind;
        public IReadOnlyList<string> PartDefinitionIds => partDefinitionIds;

        public static VehicleDeliveredPartCompatibilityRecord Create(
            string itemId,
            string groupId,
            DeliveredPartResolutionKind kind,
            params string[] targetPartIds) =>
            new VehicleDeliveredPartCompatibilityRecord
            {
                itemDefinitionId = itemId ?? string.Empty,
                targetVehicleContentId = SatsumaMailOrderCompatibilityDefaults.VehicleContentId,
                installationGroupId = groupId ?? string.Empty,
                resolutionKind = kind,
                partDefinitionIds = targetPartIds ?? Array.Empty<string>(),
            };

        public bool TryValidate(out string failure)
        {
            if (!itemDefinitionId.StartsWith(
                    "item.mail-order.",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    targetVehicleContentId,
                    SatsumaMailOrderCompatibilityDefaults.VehicleContentId,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(installationGroupId) ||
                !Enum.IsDefined(typeof(DeliveredPartResolutionKind), resolutionKind))
            {
                failure = "Delivered part compatibility identity is invalid.";
                return false;
            }

            bool isTool = resolutionKind == DeliveredPartResolutionKind.Tool;
            if (isTool != (partDefinitionIds.Length == 0) ||
                partDefinitionIds.Any(value =>
                    string.IsNullOrWhiteSpace(value) ||
                    !value.StartsWith(
                        "vehicle.satsuma.part.",
                        StringComparison.Ordinal)) ||
                partDefinitionIds.Distinct(StringComparer.Ordinal).Count() !=
                    partDefinitionIds.Length)
            {
                failure = "Delivered part compatibility targets are invalid.";
                return false;
            }

            if (resolutionKind == DeliveredPartResolutionKind.SinglePart &&
                partDefinitionIds.Length != 1)
            {
                failure = "A single-part delivery must resolve to exactly one part.";
                return false;
            }

            if (resolutionKind == DeliveredPartResolutionKind.MultiPartKit &&
                partDefinitionIds.Length < 2)
            {
                failure = "A multi-part delivery must resolve to at least two parts.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [CreateAssetMenu(menuName = "MSC/Vehicle Assembly/Delivered Part Compatibility Catalog")]
    public sealed class VehicleDeliveredPartCompatibilityCatalog : ScriptableObject
    {
        [SerializeField] private string catalogId = string.Empty;
        [SerializeField] private VehicleDeliveredPartCompatibilityRecord[] entries =
            Array.Empty<VehicleDeliveredPartCompatibilityRecord>();

        private Dictionary<string, VehicleDeliveredPartCompatibilityRecord> byItemId;

        public string CatalogId => catalogId;
        public IReadOnlyList<VehicleDeliveredPartCompatibilityRecord> Entries => entries;

        public void ConfigureForAuthoring(
            string id,
            IEnumerable<VehicleDeliveredPartCompatibilityRecord> configuredEntries)
        {
            catalogId = id ?? string.Empty;
            entries = (configuredEntries ??
                Enumerable.Empty<VehicleDeliveredPartCompatibilityRecord>())
                .ToArray();
            byItemId = null;
        }

        public bool TryResolve(
            string itemDefinitionId,
            out VehicleDeliveredPartCompatibilityRecord record)
        {
            EnsureIndex();
            return byItemId.TryGetValue(
                itemDefinitionId ?? string.Empty,
                out record);
        }

        public IReadOnlyList<string> ValidateConfiguration()
        {
            var failures = new List<string>();
            if (!string.Equals(
                    catalogId,
                    SatsumaMailOrderCompatibilityDefaults.CatalogId,
                    StringComparison.Ordinal))
            {
                failures.Add("Delivered part compatibility catalog ID is invalid.");
            }

            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < entries.Length; index++)
            {
                VehicleDeliveredPartCompatibilityRecord entry = entries[index];
                if (entry == null)
                {
                    failures.Add($"Compatibility entry {index} is missing.");
                    continue;
                }

                if (!entry.TryValidate(out string failure))
                {
                    failures.Add($"Compatibility entry {index}: {failure}");
                    continue;
                }

                if (!itemIds.Add(entry.ItemDefinitionId))
                {
                    failures.Add(
                        $"Duplicate delivered item mapping '{entry.ItemDefinitionId}'.");
                }
            }

            if (entries.Length != SatsumaMailOrderCompatibilityDefaults.ExpectedEntryCount)
            {
                failures.Add(
                    $"Expected {SatsumaMailOrderCompatibilityDefaults.ExpectedEntryCount} " +
                    $"Satsuma mail-order mappings, found {entries.Length}.");
            }

            return failures;
        }

        private void EnsureIndex()
        {
            if (byItemId != null)
            {
                return;
            }

            byItemId = new Dictionary<string, VehicleDeliveredPartCompatibilityRecord>(
                StringComparer.Ordinal);
            for (int index = 0; index < entries.Length; index++)
            {
                VehicleDeliveredPartCompatibilityRecord entry = entries[index];
                if (entry != null && !byItemId.ContainsKey(entry.ItemDefinitionId))
                {
                    byItemId.Add(entry.ItemDefinitionId, entry);
                }
            }
        }
    }
}
