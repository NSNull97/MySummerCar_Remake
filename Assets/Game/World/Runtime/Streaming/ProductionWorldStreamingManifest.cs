using System;
using System.Collections.Generic;
using MSC.World.Partition;
using UnityEngine;

namespace MSC.World.Streaming
{
    [Serializable]
    public struct ProductionWorldCellScene
    {
        [SerializeField] private string cellId;
        [SerializeField] private int x;
        [SerializeField] private int z;
        [SerializeField] private int buildIndex;
        [SerializeField] private string scenePath;

        public ProductionWorldCellScene(string id, int cellX, int cellZ, int index, string path)
        {
            cellId = id;
            x = cellX;
            z = cellZ;
            buildIndex = index;
            scenePath = path;
        }

        public string CellId => cellId;
        public WorldCellIndex Index => new WorldCellIndex(x, z);
        public int BuildIndex => buildIndex;
        public string ScenePath => scenePath;
    }

    /// <summary>
    /// Immutable-at-runtime catalog for the bounded production world cells accepted by Milestone 05B.1.
    /// Build indices are authored by the Editor builder and verified against their project-relative paths.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ProductionWorldStreamingManifest",
        menuName = "MSC/World/Production World Streaming Manifest")]
    public sealed class ProductionWorldStreamingManifest : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField, Min(1f)] private float cellSizeMeters = 512f;
        [SerializeField, Min(0)] private int loadingRadiusCells;
        [SerializeField, Min(0)] private int unloadingRadiusCells = 1;
        [SerializeField] private ProductionWorldCellScene[] cells = Array.Empty<ProductionWorldCellScene>();

        public int SchemaVersion => schemaVersion;
        public float CellSizeMeters => cellSizeMeters;
        public int LoadingRadiusCells => loadingRadiusCells;
        public int UnloadingRadiusCells => unloadingRadiusCells;
        public IReadOnlyList<ProductionWorldCellScene> Cells => cells ?? Array.Empty<ProductionWorldCellScene>();

        public bool TryGetCell(WorldCellIndex index, out ProductionWorldCellScene cell)
        {
            ProductionWorldCellScene[] configuredCells = cells ?? Array.Empty<ProductionWorldCellScene>();
            for (int cellIndex = 0; cellIndex < configuredCells.Length; cellIndex++)
            {
                if (configuredCells[cellIndex].Index.Equals(index))
                {
                    cell = configuredCells[cellIndex];
                    return true;
                }
            }

            cell = default;
            return false;
        }

        public bool TryGetCell(string cellId, out ProductionWorldCellScene cell)
        {
            ProductionWorldCellScene[] configuredCells = cells ?? Array.Empty<ProductionWorldCellScene>();
            for (int cellIndex = 0; cellIndex < configuredCells.Length; cellIndex++)
            {
                if (string.Equals(configuredCells[cellIndex].CellId, cellId, StringComparison.Ordinal))
                {
                    cell = configuredCells[cellIndex];
                    return true;
                }
            }

            cell = default;
            return false;
        }

        public IReadOnlyList<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (schemaVersion != CurrentSchemaVersion)
            {
                errors.Add($"Manifest schema {schemaVersion} does not match supported schema {CurrentSchemaVersion}.");
            }

            if (!float.IsFinite(cellSizeMeters) || cellSizeMeters <= 0f)
            {
                errors.Add("Cell size must be a finite positive value.");
            }

            if (loadingRadiusCells < 0)
            {
                errors.Add("Loading radius cannot be negative.");
            }

            if (unloadingRadiusCells < loadingRadiusCells)
            {
                errors.Add("Unloading radius must be greater than or equal to the loading radius.");
            }

            ProductionWorldCellScene[] configuredCells = cells ?? Array.Empty<ProductionWorldCellScene>();
            if (configuredCells.Length == 0)
            {
                errors.Add("At least one production cell is required.");
                return errors;
            }

            var cellIds = new HashSet<string>(StringComparer.Ordinal);
            var cellIndices = new HashSet<WorldCellIndex>();
            var buildIndices = new HashSet<int>();
            for (int index = 0; index < configuredCells.Length; index++)
            {
                ProductionWorldCellScene cell = configuredCells[index];
                string prefix = $"Cell entry {index}";
                if (string.IsNullOrWhiteSpace(cell.CellId))
                {
                    errors.Add(prefix + " has no stable cell ID.");
                }
                else
                {
                    if (!cellIds.Add(cell.CellId))
                    {
                        errors.Add(prefix + " duplicates cell ID " + cell.CellId + ".");
                    }

                    if (!string.Equals(cell.CellId, cell.Index.Id, StringComparison.Ordinal))
                    {
                        errors.Add(prefix + $" ID {cell.CellId} does not match coordinates {cell.Index.Id}.");
                    }
                }

                if (!cellIndices.Add(cell.Index))
                {
                    errors.Add(prefix + " duplicates coordinates " + cell.Index + ".");
                }

                if (cell.BuildIndex < 0)
                {
                    errors.Add(prefix + " has an invalid build index.");
                }
                else if (!buildIndices.Add(cell.BuildIndex))
                {
                    errors.Add(prefix + " duplicates build index " + cell.BuildIndex + ".");
                }

                if (string.IsNullOrWhiteSpace(cell.ScenePath) ||
                    !cell.ScenePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                    !cell.ScenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add(prefix + " has an invalid project-relative scene path.");
                }
            }

            return errors;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            float cellSize,
            int loadRadius,
            int unloadRadius,
            ProductionWorldCellScene[] configuredCells)
        {
            schemaVersion = CurrentSchemaVersion;
            cellSizeMeters = cellSize;
            loadingRadiusCells = loadRadius;
            unloadingRadiusCells = unloadRadius;
            cells = configuredCells != null
                ? (ProductionWorldCellScene[])configuredCells.Clone()
                : Array.Empty<ProductionWorldCellScene>();
        }
#endif
    }
}
