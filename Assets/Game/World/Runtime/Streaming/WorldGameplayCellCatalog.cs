using System;
using System.Collections.Generic;
using MSC.Core.Identity;
using MSC.World.Partition;
using UnityEngine;

namespace MSC.World.Streaming
{
    [Serializable]
    public struct WorldGameplayAnchorRecord
    {
        [SerializeField] private string anchorId;
        [SerializeField] private string stableEntityId;
        [SerializeField] private int cellX;
        [SerializeField] private int cellZ;
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 eulerAngles;

        public WorldGameplayAnchorRecord(
            string id,
            string stableId,
            WorldCellIndex cell,
            Vector3 worldPosition,
            Quaternion worldRotation)
        {
            anchorId = id;
            stableEntityId = stableId;
            cellX = cell.X;
            cellZ = cell.Z;
            position = worldPosition;
            eulerAngles = worldRotation.eulerAngles;
        }

        public string AnchorId => anchorId;
        public string StableEntityId => stableEntityId;
        public WorldCellIndex Cell => new WorldCellIndex(cellX, cellZ);
        public Vector3 Position => position;
        public Quaternion Rotation => Quaternion.Euler(eulerAngles);
    }

    /// <summary>
    /// Project-owned gameplay identity and anchor data that remains available
    /// independently of the currently selected visual world profile.
    /// </summary>
    [CreateAssetMenu(
        fileName = "WorldGameplayCellCatalog",
        menuName = "MSC/World/Gameplay Cell Catalog")]
    public sealed class WorldGameplayCellCatalog : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string catalogId = string.Empty;
        [SerializeField] private WorldGameplayAnchorRecord[] anchors =
            Array.Empty<WorldGameplayAnchorRecord>();

        public int SchemaVersion => schemaVersion;
        public string CatalogId => catalogId;
        public IReadOnlyList<WorldGameplayAnchorRecord> Anchors =>
            anchors ?? Array.Empty<WorldGameplayAnchorRecord>();

        public IReadOnlyList<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (schemaVersion != CurrentSchemaVersion)
            {
                errors.Add(
                    $"Gameplay catalog schema {schemaVersion} does not match " +
                    $"supported schema {CurrentSchemaVersion}.");
            }

            if (string.IsNullOrWhiteSpace(catalogId))
            {
                errors.Add("Gameplay catalog has no stable catalog ID.");
            }

            var anchorIds = new HashSet<string>(StringComparer.Ordinal);
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            WorldGameplayAnchorRecord[] configured =
                anchors ?? Array.Empty<WorldGameplayAnchorRecord>();
            for (int index = 0; index < configured.Length; index++)
            {
                WorldGameplayAnchorRecord anchor = configured[index];
                string prefix = $"Gameplay anchor {index}";
                if (string.IsNullOrWhiteSpace(anchor.AnchorId) ||
                    !anchorIds.Add(anchor.AnchorId))
                {
                    errors.Add(prefix + " has a missing or duplicate anchor ID.");
                }

                if (!StableEntityId.TryParse(
                        anchor.StableEntityId,
                        out _) ||
                    !stableIds.Add(anchor.StableEntityId))
                {
                    errors.Add(
                        prefix + " has an invalid or duplicate stable entity ID.");
                }

                if (!IsFinite(anchor.Position) ||
                    !IsFinite(anchor.Rotation))
                {
                    errors.Add(prefix + " has a non-finite transform.");
                }
            }

            return errors;
        }

        public bool TryGetAnchor(
            string anchorId,
            out WorldGameplayAnchorRecord anchor)
        {
            WorldGameplayAnchorRecord[] configured =
                anchors ?? Array.Empty<WorldGameplayAnchorRecord>();
            for (int index = 0; index < configured.Length; index++)
            {
                if (string.Equals(
                        configured[index].AnchorId,
                        anchorId,
                        StringComparison.Ordinal))
                {
                    anchor = configured[index];
                    return true;
                }
            }

            anchor = default;
            return false;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string id,
            WorldGameplayAnchorRecord[] configuredAnchors)
        {
            schemaVersion = CurrentSchemaVersion;
            catalogId = id;
            anchors = configuredAnchors != null
                ? (WorldGameplayAnchorRecord[])configuredAnchors.Clone()
                : Array.Empty<WorldGameplayAnchorRecord>();
        }
#endif

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);
    }
}
