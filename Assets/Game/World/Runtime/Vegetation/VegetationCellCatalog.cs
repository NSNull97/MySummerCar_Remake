using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.World.Vegetation
{
    [CreateAssetMenu(
        fileName = "VegetationCellCatalog",
        menuName = "MSC/World/Vegetation/Cell Catalog")]
    public sealed class VegetationCellCatalog : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private VegetationCellAsset[] cells =
            Array.Empty<VegetationCellAsset>();
        [SerializeField] private VegetationProfile[] profiles =
            Array.Empty<VegetationProfile>();
        [SerializeField] private LayerMask relevantRaycastLayers = ~0;
        [SerializeField, Min(1f)] private float candidateRaycastHeight = 2048f;
        [SerializeField, Min(1f)] private float candidateRaycastDistance = 4096f;
        [NonSerialized] private readonly List<Bounds> dirtyTileDebugBounds =
            new List<Bounds>();

        public IReadOnlyList<VegetationCellAsset> Cells =>
            cells ?? Array.Empty<VegetationCellAsset>();
        public IReadOnlyList<VegetationProfile> Profiles =>
            profiles ?? Array.Empty<VegetationProfile>();
        public LayerMask RelevantRaycastLayers => relevantRaycastLayers;
        public float CandidateRaycastHeight => candidateRaycastHeight;
        public float CandidateRaycastDistance => candidateRaycastDistance;
        public IReadOnlyList<Bounds> DirtyTileDebugBounds =>
            dirtyTileDebugBounds;

        public bool TryGetCell(Vector3 worldPosition, out VegetationCellAsset cell)
        {
            VegetationCellAsset[] configuredCells =
                cells ?? Array.Empty<VegetationCellAsset>();
            for (int index = 0; index < configuredCells.Length; index++)
            {
                VegetationCellAsset candidate = configuredCells[index];
                if (candidate != null && candidate.ContainsWorldXZ(worldPosition))
                {
                    cell = candidate;
                    return true;
                }
            }

            cell = null;
            return false;
        }

        public void GetCellsIntersectingCircle(
            Vector3 center,
            float radius,
            List<VegetationCellAsset> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            results.Clear();
            float radiusSquared = radius * radius;
            VegetationCellAsset[] configuredCells =
                cells ?? Array.Empty<VegetationCellAsset>();
            for (int index = 0; index < configuredCells.Length; index++)
            {
                VegetationCellAsset cell = configuredCells[index];
                if (cell == null)
                {
                    continue;
                }

                Bounds bounds = cell.WorldBounds;
                float closestX = Mathf.Clamp(center.x, bounds.min.x, bounds.max.x);
                float closestZ = Mathf.Clamp(center.z, bounds.min.z, bounds.max.z);
                float deltaX = center.x - closestX;
                float deltaZ = center.z - closestZ;
                if (deltaX * deltaX + deltaZ * deltaZ <= radiusSquared)
                {
                    results.Add(cell);
                }
            }
        }

        public IReadOnlyList<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (schemaVersion != CurrentSchemaVersion)
            {
                errors.Add("Vegetation catalog schema is unsupported.");
            }

            VegetationCellAsset[] configuredCells =
                cells ?? Array.Empty<VegetationCellAsset>();
            if (configuredCells.Length == 0)
            {
                errors.Add("Vegetation catalog contains no streaming cells.");
            }

            var cellIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < configuredCells.Length; index++)
            {
                VegetationCellAsset cell = configuredCells[index];
                if (cell == null)
                {
                    errors.Add("Vegetation catalog contains a null cell.");
                    continue;
                }

                if (!cellIds.Add(cell.CellId))
                {
                    errors.Add("Vegetation catalog duplicates cell " + cell.CellId + ".");
                }

                IReadOnlyList<string> cellErrors = cell.ValidateConfiguration();
                for (int errorIndex = 0; errorIndex < cellErrors.Count; errorIndex++)
                {
                    errors.Add(cellErrors[errorIndex]);
                }
            }

            VegetationProfile[] configuredProfiles =
                profiles ?? Array.Empty<VegetationProfile>();
            if (configuredProfiles.Length != 4)
            {
                errors.Add(
                    "Vegetation catalog requires exactly four density-channel profiles.");
            }

            var channels = new HashSet<VegetationDensityChannel>();
            var profileIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < configuredProfiles.Length; index++)
            {
                VegetationProfile profile = configuredProfiles[index];
                if (profile == null)
                {
                    errors.Add("Vegetation catalog contains a null profile.");
                    continue;
                }

                if (!channels.Add(profile.DensityChannel))
                {
                    errors.Add(
                        "Vegetation catalog duplicates channel " +
                        profile.DensityChannel + ".");
                }

                if (!profileIds.Add(profile.ProfileId))
                {
                    errors.Add(
                        "Vegetation catalog duplicates profile ID " +
                        profile.ProfileId + ".");
                }

                IReadOnlyList<string> profileErrors =
                    profile.ValidateConfiguration();
                for (int errorIndex = 0; errorIndex < profileErrors.Count; errorIndex++)
                {
                    errors.Add(profileErrors[errorIndex]);
                }
            }

            return errors;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            VegetationCellAsset[] configuredCells,
            VegetationProfile[] configuredProfiles,
            LayerMask configuredRelevantLayers,
            float configuredRaycastHeight,
            float configuredRaycastDistance)
        {
            schemaVersion = CurrentSchemaVersion;
            cells = configuredCells != null
                ? (VegetationCellAsset[])configuredCells.Clone()
                : Array.Empty<VegetationCellAsset>();
            profiles = configuredProfiles != null
                ? (VegetationProfile[])configuredProfiles.Clone()
                : Array.Empty<VegetationProfile>();
            relevantRaycastLayers = configuredRelevantLayers;
            candidateRaycastHeight = Mathf.Max(1f, configuredRaycastHeight);
            candidateRaycastDistance = Mathf.Max(1f, configuredRaycastDistance);
        }

        public void SetDirtyTileDebugBoundsForAuthoring(
            IReadOnlyList<Bounds> configuredBounds)
        {
            dirtyTileDebugBounds.Clear();
            if (configuredBounds == null)
            {
                return;
            }

            for (int index = 0; index < configuredBounds.Count; index++)
            {
                dirtyTileDebugBounds.Add(configuredBounds[index]);
            }
        }
#endif
    }
}
