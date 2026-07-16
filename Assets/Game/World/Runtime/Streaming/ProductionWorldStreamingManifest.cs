using System;
using System.Collections.Generic;
using MSC.World.Partition;
using UnityEngine;

namespace MSC.World.Streaming
{
    public enum ProductionWorldProfileKind
    {
        PrototypeFixture = 0,
        DonorFeatureParity = 1,
        ProductionOverride = 2
    }

    [Serializable]
    public struct ProductionWorldGlobalScene
    {
        [SerializeField] private string sceneId;
        [SerializeField] private int buildIndex;
        [SerializeField] private string scenePath;

        public ProductionWorldGlobalScene(
            string id,
            int index,
            string path)
        {
            sceneId = id;
            buildIndex = index;
            scenePath = path;
        }

        public string SceneId => sceneId;
        public int BuildIndex => buildIndex;
        public string ScenePath => scenePath;
    }

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
        public const int CurrentSchemaVersion = 2;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string profileId = "prototype-fixture";
        [SerializeField] private ProductionWorldProfileKind profileKind =
            ProductionWorldProfileKind.PrototypeFixture;
        [SerializeField] private bool privateLocalRuntimeBaseline;
        [SerializeField, Min(1f)] private float cellSizeMeters = 512f;
        [SerializeField, Min(0)] private int loadingRadiusCells;
        [SerializeField, Min(0)] private int unloadingRadiusCells = 1;
        [SerializeField, Min(0f)]
        private float vehiclePreloadSpeedMetersPerSecond = 12f;
        [SerializeField, Min(0)]
        private int vehiclePreloadRadiusCells = 1;
        [SerializeField] private ProductionWorldGlobalScene[] globalScenes =
            Array.Empty<ProductionWorldGlobalScene>();
        [SerializeField] private ProductionWorldCellScene[] cells = Array.Empty<ProductionWorldCellScene>();
        [SerializeField] private WorldGameplayCellCatalog gameplayCatalog;

        public int SchemaVersion => schemaVersion;
        public string ProfileId => profileId;
        public ProductionWorldProfileKind ProfileKind => profileKind;
        public bool PrivateLocalRuntimeBaseline => privateLocalRuntimeBaseline;
        public float CellSizeMeters => cellSizeMeters;
        public int LoadingRadiusCells => loadingRadiusCells;
        public int UnloadingRadiusCells => unloadingRadiusCells;
        public float VehiclePreloadSpeedMetersPerSecond =>
            vehiclePreloadSpeedMetersPerSecond;
        public int VehiclePreloadRadiusCells => vehiclePreloadRadiusCells;
        public IReadOnlyList<ProductionWorldGlobalScene> GlobalScenes =>
            globalScenes ?? Array.Empty<ProductionWorldGlobalScene>();
        public IReadOnlyList<ProductionWorldCellScene> Cells => cells ?? Array.Empty<ProductionWorldCellScene>();
        public WorldGameplayCellCatalog GameplayCatalog => gameplayCatalog;

        public int GetLoadingRadiusForSpeed(float speedMetersPerSecond)
        {
            if (!float.IsFinite(speedMetersPerSecond) ||
                speedMetersPerSecond < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(speedMetersPerSecond));
            }

            return speedMetersPerSecond >=
                   vehiclePreloadSpeedMetersPerSecond
                ? Mathf.Max(
                    loadingRadiusCells,
                    vehiclePreloadRadiusCells)
                : loadingRadiusCells;
        }

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

            if (string.IsNullOrWhiteSpace(profileId))
            {
                errors.Add("World streaming manifest has no stable profile ID.");
            }

            if (profileKind == ProductionWorldProfileKind.DonorFeatureParity &&
                !privateLocalRuntimeBaseline)
            {
                errors.Add(
                    "Donor feature-parity profile must be explicitly marked " +
                    "private-local runtime baseline.");
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

            if (!float.IsFinite(vehiclePreloadSpeedMetersPerSecond) ||
                vehiclePreloadSpeedMetersPerSecond < 0f)
            {
                errors.Add(
                    "Vehicle preload speed must be a finite non-negative value.");
            }

            if (vehiclePreloadRadiusCells < loadingRadiusCells)
            {
                errors.Add(
                    "Vehicle preload radius must be greater than or equal to " +
                    "the normal loading radius.");
            }

            if (unloadingRadiusCells < vehiclePreloadRadiusCells)
            {
                errors.Add(
                    "Unloading radius must be greater than or equal to the " +
                    "vehicle preload radius.");
            }

            ProductionWorldGlobalScene[] configuredGlobalScenes =
                globalScenes ?? Array.Empty<ProductionWorldGlobalScene>();
            if (profileKind == ProductionWorldProfileKind.DonorFeatureParity &&
                configuredGlobalScenes.Length == 0)
            {
                errors.Add(
                    "Donor feature-parity profile requires at least one global scene.");
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
            var scenePaths = new HashSet<string>(StringComparer.Ordinal);
            var globalSceneIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < configuredGlobalScenes.Length; index++)
            {
                ProductionWorldGlobalScene scene = configuredGlobalScenes[index];
                string prefix = $"Global scene entry {index}";
                if (string.IsNullOrWhiteSpace(scene.SceneId) ||
                    !globalSceneIds.Add(scene.SceneId))
                {
                    errors.Add(
                        prefix + " has a missing or duplicate stable scene ID.");
                }

                ValidateSceneAddress(
                    prefix,
                    scene.BuildIndex,
                    scene.ScenePath,
                    buildIndices,
                    scenePaths,
                    errors);
            }

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

                ValidateSceneAddress(
                    prefix,
                    cell.BuildIndex,
                    cell.ScenePath,
                    buildIndices,
                    scenePaths,
                    errors);
            }

            if (gameplayCatalog != null)
            {
                IReadOnlyList<string> gameplayErrors =
                    gameplayCatalog.ValidateConfiguration();
                for (int index = 0; index < gameplayErrors.Count; index++)
                {
                    errors.Add("Gameplay catalog: " + gameplayErrors[index]);
                }
            }
            else if (profileKind ==
                     ProductionWorldProfileKind.DonorFeatureParity)
            {
                errors.Add(
                    "Donor feature-parity profile requires a project-owned " +
                    "gameplay cell catalog.");
            }

            return errors;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredProfileId,
            ProductionWorldProfileKind configuredProfileKind,
            bool isPrivateLocalRuntimeBaseline,
            float cellSize,
            int loadRadius,
            int unloadRadius,
            float vehiclePreloadSpeed,
            int vehiclePreloadRadius,
            ProductionWorldGlobalScene[] configuredGlobalScenes,
            ProductionWorldCellScene[] configuredCells,
            WorldGameplayCellCatalog configuredGameplayCatalog)
        {
            schemaVersion = CurrentSchemaVersion;
            profileId = configuredProfileId;
            profileKind = configuredProfileKind;
            privateLocalRuntimeBaseline = isPrivateLocalRuntimeBaseline;
            cellSizeMeters = cellSize;
            loadingRadiusCells = loadRadius;
            unloadingRadiusCells = unloadRadius;
            vehiclePreloadSpeedMetersPerSecond = vehiclePreloadSpeed;
            vehiclePreloadRadiusCells = vehiclePreloadRadius;
            globalScenes = configuredGlobalScenes != null
                ? (ProductionWorldGlobalScene[])configuredGlobalScenes.Clone()
                : Array.Empty<ProductionWorldGlobalScene>();
            cells = configuredCells != null
                ? (ProductionWorldCellScene[])configuredCells.Clone()
                : Array.Empty<ProductionWorldCellScene>();
            gameplayCatalog = configuredGameplayCatalog;
        }

        public void ConfigureForAuthoring(
            float cellSize,
            int loadRadius,
            int unloadRadius,
            ProductionWorldCellScene[] configuredCells)
        {
            ConfigureForAuthoring(
                "prototype-fixture",
                ProductionWorldProfileKind.PrototypeFixture,
                false,
                cellSize,
                loadRadius,
                unloadRadius,
                float.MaxValue,
                loadRadius,
                Array.Empty<ProductionWorldGlobalScene>(),
                configuredCells,
                null);
        }
#endif

        private static void ValidateSceneAddress(
            string prefix,
            int buildIndex,
            string scenePath,
            ISet<int> buildIndices,
            ISet<string> scenePaths,
            ICollection<string> errors)
        {
            if (buildIndex < 0)
            {
                errors.Add(prefix + " has an invalid build index.");
            }
            else if (!buildIndices.Add(buildIndex))
            {
                errors.Add(
                    prefix + " duplicates build index " + buildIndex + ".");
            }

            if (string.IsNullOrWhiteSpace(scenePath) ||
                !scenePath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !scenePath.EndsWith(
                    ".unity",
                    StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(
                    prefix + " has an invalid project-relative scene path.");
            }
            else if (!scenePaths.Add(scenePath))
            {
                errors.Add(prefix + " duplicates scene path " + scenePath + ".");
            }
        }
    }
}
