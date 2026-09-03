using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MSCMapMigration
{
    internal interface IMapMigrationSettings
    {
        string SourceScene { get; }
        string SourceRoot { get; }
        bool IncludeSiblingBaselineCells { get; }
        string OutputScene { get; }
        string BlenderExportFolder { get; }
        float TerrainTileSize { get; }
        float TargetHeightSampleSpacing { get; }
        int MaximumTotalHeightSamples { get; }
        float SmoothingStrength { get; }
        int SmoothingIterations { get; }
        float MaximumAllowedSmoothingDisplacement { get; }
        float RoadClearance { get; }
        float RoadShoulderBlendDistance { get; }
        float MinimumSourceCoverage { get; }
        int ValidationSampleCount { get; }
        TerrainMaterialTransferMode MaterialTransferMode { get; }
        bool DryRun { get; }
        string LastExportRelativePath { get; }
        string LastExportFingerprint { get; }
    }

    internal sealed class MapMigrationSettingsSnapshot : IMapMigrationSettings
    {
        public MapMigrationSettingsSnapshot(MapMigrationSettings source)
        {
            SourceScene = source.SourceScene;
            SourceRoot = source.SourceRoot;
            IncludeSiblingBaselineCells = source.IncludeSiblingBaselineCells;
            OutputScene = source.OutputScene;
            BlenderExportFolder = source.BlenderExportFolder;
            TerrainTileSize = source.TerrainTileSize;
            TargetHeightSampleSpacing = source.TargetHeightSampleSpacing;
            MaximumTotalHeightSamples = source.MaximumTotalHeightSamples;
            SmoothingStrength = source.SmoothingStrength;
            SmoothingIterations = source.SmoothingIterations;
            MaximumAllowedSmoothingDisplacement = source.MaximumAllowedSmoothingDisplacement;
            RoadClearance = source.RoadClearance;
            RoadShoulderBlendDistance = source.RoadShoulderBlendDistance;
            MinimumSourceCoverage = source.MinimumSourceCoverage;
            ValidationSampleCount = source.ValidationSampleCount;
            MaterialTransferMode = source.MaterialTransferMode;
            DryRun = source.DryRun;
            LastExportRelativePath = source.LastExportRelativePath;
            LastExportFingerprint = source.LastExportFingerprint;
        }

        public string SourceScene { get; }
        public string SourceRoot { get; }
        public bool IncludeSiblingBaselineCells { get; }
        public string OutputScene { get; }
        public string BlenderExportFolder { get; }
        public float TerrainTileSize { get; }
        public float TargetHeightSampleSpacing { get; }
        public int MaximumTotalHeightSamples { get; }
        public float SmoothingStrength { get; }
        public int SmoothingIterations { get; }
        public float MaximumAllowedSmoothingDisplacement { get; }
        public float RoadClearance { get; }
        public float RoadShoulderBlendDistance { get; }
        public float MinimumSourceCoverage { get; }
        public int ValidationSampleCount { get; }
        public TerrainMaterialTransferMode MaterialTransferMode { get; }
        public bool DryRun { get; }
        public string LastExportRelativePath { get; }
        public string LastExportFingerprint { get; }
    }

    [CreateAssetMenu(
        fileName = "MapTerrainMigrationSettings",
        menuName = "MSC Remake/Map Terrain Migration Settings")]
    public sealed class MapMigrationSettings : ScriptableObject, IMapMigrationSettings
    {
        public const string SettingsAssetPath =
            "Assets/_Generated/MapTerrainMigration/Settings/" +
            "MapTerrainMigrationSettings.asset";
        public const string DefaultLegacyGlobalScene =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/" +
            "Streaming/Scenes/World_Global_Legacy.unity";
        public const string RequestedSourceScene =
            "Assets/Scenes/3Buildings.unity";
        public const string SecondaryRequestedSourceScene =
            "Assets/Scenes/2BasicMap.unity";
        public const string DefaultOutputScene =
            "Assets/Scenes/Generated/3Buildings_TerrainMigration.unity";

        [Header("Source")]
        [SerializeField] private string sourceScene = DefaultLegacyGlobalScene;
        [SerializeField] private string sourceRoot =
            "TEMPORARY_DIRECT_IMPORT_ENTITIES";
        [SerializeField] private bool includeSiblingBaselineCells = true;

        [Header("Output")]
        [SerializeField] private string outputScene = DefaultOutputScene;
        [SerializeField] private string blenderExportFolder =
            "BlenderWork/BaseMap_SourceExport";

        [Header("Terrain")]
        [Min(32f)] [SerializeField] private float terrainTileSize = 1024f;
        [Min(0.25f)] [SerializeField] private float targetHeightSampleSpacing = 2f;
        [Min(1089)] [SerializeField] private int maximumTotalHeightSamples = 30000000;
        [Range(0f, 1f)] [SerializeField] private float smoothingStrength = 0.32f;
        [Range(0, 8)] [SerializeField] private int smoothingIterations = 2;
        [Min(0f)] [SerializeField] private float maximumAllowedSmoothingDisplacement = 0.75f;
        [Min(0f)] [SerializeField] private float roadClearance = 0.075f;
        [Min(0f)] [SerializeField] private float roadShoulderBlendDistance = 4f;
        [Range(0f, 1f)] [SerializeField] private float minimumSourceCoverage = 0.95f;
        [Min(128)] [SerializeField] private int validationSampleCount = 20000;
        [SerializeField] private TerrainMaterialTransferMode materialTransferMode =
            TerrainMaterialTransferMode.Auto;
        [SerializeField] private bool dryRun;

        [HideInInspector] [SerializeField] private string lastExportRelativePath = string.Empty;
        [HideInInspector] [SerializeField] private string lastExportFingerprint = string.Empty;

        public string SourceScene => sourceScene;
        public string SourceRoot => sourceRoot;
        public bool IncludeSiblingBaselineCells => includeSiblingBaselineCells;
        public string OutputScene => outputScene;
        public string BlenderExportFolder => blenderExportFolder;
        public float TerrainTileSize => terrainTileSize;
        public float TargetHeightSampleSpacing => targetHeightSampleSpacing;
        public int MaximumTotalHeightSamples => maximumTotalHeightSamples;
        public float SmoothingStrength => smoothingStrength;
        public int SmoothingIterations => smoothingIterations;
        public float MaximumAllowedSmoothingDisplacement => maximumAllowedSmoothingDisplacement;
        public float RoadClearance => roadClearance;
        public float RoadShoulderBlendDistance => roadShoulderBlendDistance;
        public float MinimumSourceCoverage => minimumSourceCoverage;
        public int ValidationSampleCount => validationSampleCount;
        public TerrainMaterialTransferMode MaterialTransferMode => materialTransferMode;
        public bool DryRun => dryRun;
        public string LastExportRelativePath => lastExportRelativePath;
        public string LastExportFingerprint => lastExportFingerprint;

        internal MapMigrationSettingsSnapshot CreateSnapshot() => new MapMigrationSettingsSnapshot(this);

        public void ConfigureSourceForTests(
            string scene,
            string root,
            bool includeCells = false)
        {
            sourceScene = scene;
            sourceRoot = root;
            includeSiblingBaselineCells = includeCells;
        }

        public void RecordExport(string relativePath, string fingerprint)
        {
            lastExportRelativePath = relativePath ?? string.Empty;
            lastExportFingerprint = fingerprint ?? string.Empty;
            EditorUtility.SetDirty(this);
        }

        public void ValidateOrThrow()
        {
            if (string.IsNullOrWhiteSpace(sourceScene) ||
                !sourceScene.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("A valid source scene asset path is required.");
            }

            if (!File.Exists(MapMigrationPaths.ToAbsoluteProjectPath(sourceScene)))
            {
                throw new FileNotFoundException(
                    "Map migration source scene was not found.", sourceScene);
            }

            if (string.Equals(sourceScene, outputScene, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Source and output scenes must be different.");
            }

            if (!outputScene.StartsWith("Assets/Scenes/Generated/", StringComparison.Ordinal) ||
                !outputScene.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Output scene must be below Assets/Scenes/Generated/.");
            }

            if (terrainTileSize <= 0f || targetHeightSampleSpacing <= 0f ||
                maximumTotalHeightSamples < 1089)
            {
                throw new InvalidDataException("Terrain sampling settings are invalid.");
            }

            if (roadClearance < 0f || roadShoulderBlendDistance < 0f)
            {
                throw new InvalidDataException("Road constraints cannot use negative distances.");
            }

            string normalizedExport = blenderExportFolder.Replace('\\', '/').Trim('/');
            if (Path.IsPathRooted(normalizedExport) ||
                normalizedExport.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                normalizedExport.Contains(".."))
            {
                throw new InvalidDataException(
                    "Blender export folder must be project-relative, outside Assets, and traversal-free.");
            }
        }

        internal void ApplySafeDefaults()
        {
            sourceScene = ResolveDefaultSourceScene();
            sourceRoot = string.Equals(
                    sourceScene,
                    DefaultLegacyGlobalScene,
                    StringComparison.Ordinal)
                ? "TEMPORARY_DIRECT_IMPORT_ENTITIES"
                : "Map2";
            includeSiblingBaselineCells = string.Equals(
                sourceScene,
                DefaultLegacyGlobalScene,
                StringComparison.Ordinal);
            outputScene = DefaultOutputScene;
            blenderExportFolder = "BlenderWork/BaseMap_SourceExport";
        }

        internal static string ResolveDefaultSourceScene()
        {
            if (File.Exists(MapMigrationPaths.ToAbsoluteProjectPath(RequestedSourceScene)))
            {
                return RequestedSourceScene;
            }

            if (File.Exists(MapMigrationPaths.ToAbsoluteProjectPath(SecondaryRequestedSourceScene)))
            {
                return SecondaryRequestedSourceScene;
            }

            return DefaultLegacyGlobalScene;
        }

        public static MapMigrationSettings LoadOrCreate()
        {
            MapMigrationSettings settings =
                AssetDatabase.LoadAssetAtPath<MapMigrationSettings>(SettingsAssetPath);
            if (settings != null)
            {
                return settings;
            }

            MapMigrationPaths.EnsureAssetFolder(Path.GetDirectoryName(SettingsAssetPath)!
                .Replace('\\', '/'));
            settings = CreateInstance<MapMigrationSettings>();
            settings.ApplySafeDefaults();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
            return settings;
        }
    }
}
