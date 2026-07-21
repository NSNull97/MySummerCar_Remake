using System;

namespace MSC.Editor.WorldBaseline
{
    public static class WorldBaseline06B2Paths
    {
        public const string GeneratorVersion = "1.2.0-08A1";
        public const string ProfileId = "donor-feature-parity-06b2";
        public const string GameplayCatalogId =
            "project-gameplay-anchors-06b2";

        public const string StreamingRoot =
            WorldBaselinePaths.WorldRoot + "/Streaming";
        public const string StreamingSceneRoot =
            StreamingRoot + "/Scenes";
        public const string StreamingCellSceneRoot =
            StreamingSceneRoot + "/Cells";
        public const string CollisionMeshRoot =
            StreamingRoot + "/CollisionMeshes";
        public const string GlobalScene =
            StreamingSceneRoot + "/World_Global_Legacy.unity";

        public const string ActiveManifest =
            "Assets/Game/World/Content/Streaming/" +
            "ProductionWorldStreamingManifest.asset";
        public const string PrototypeManifest =
            "Assets/Game/World/Content/Streaming/" +
            "PrototypeWorldStreamingManifest.asset";
        public const string GameplayCatalog =
            "Assets/Game/World/Content/Streaming/" +
            "WorldGameplayCellCatalog.asset";
        public const string PrototypeFixtureScene =
            "Assets/Game/World/Debug/Streaming/" +
            "PrototypeWorldStreamingFixture.unity";
        public const string ActiveBootstrapScene =
            "Assets/Game/Bootstrap/Bootstrap.unity";

        public const string SafeColliderAllowlist =
            "Assets/Game/LegacyImport/Manifests/" +
            "WorldBaseline06B2SafeColliderAllowlist.csv";
        public const string GameplayAnchorManifest =
            "Assets/Game/LegacyImport/Manifests/" +
            "WorldBaseline06B2GameplayAnchors.csv";

        public const string OwnershipManifest =
            "Docs/WorldBaseline/LEGACY_OBJECT_CELL_MANIFEST.csv";
        public const string OwnershipMatrix =
            "Docs/WorldBaseline/GLOBAL_AND_CELL_OWNERSHIP_MATRIX.csv";
        public const string SolidColliderDispositionManifest =
            "Docs/WorldBaseline/" +
            "LEGACY_SOLID_COLLIDER_DISPOSITIONS.csv";
        public const string SolidColliderDispositionManifestSha256 =
            "Docs/WorldBaseline/" +
            "LEGACY_SOLID_COLLIDER_DISPOSITIONS.sha256";
        public const string MaterialTextureManifest =
            "Docs/WorldBaseline/LEGACY_MATERIAL_TEXTURE_MANIFEST.csv";
        public const string MaterialShaderMapping =
            "Docs/WorldBaseline/MATERIAL_SHADER_MAPPING.md";
        public const string VisualCompletenessReport =
            "Docs/WorldBaseline/BASELINE_VISUAL_COMPLETENESS_REPORT.md";
        public const string TextureMemoryBaseline =
            "Docs/WorldBaseline/TEXTURE_MEMORY_BASELINE.md";
        public const string PresentationSourceManifest =
            "Assets/Game/LegacyImport/Manifests/" +
            "DonorWorld06B2PresentationManifest.json";
        public const string ValidationEvidence =
            "PerformanceCaptures/Milestone06B2/" +
            "M06B2_STREAMING_VALIDATION.json";
        public const string PerformanceEvidence =
            "PerformanceCaptures/Milestone06B2/" +
            "M06B2_STREAMING_PERFORMANCE.json";

        public const float CellSizeMeters = 512f;
        public const int LoadingRadiusCells = 1;
        public const int UnloadingRadiusCells = 2;
        public const float VehiclePreloadSpeedMetersPerSecond = 12f;
        public const int VehiclePreloadRadiusCells = 2;

        public static string CellScene(string cellId)
        {
            if (string.IsNullOrWhiteSpace(cellId) ||
                !cellId.StartsWith("cell_", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Cell ID must use the canonical cell_X_Z form.",
                    nameof(cellId));
            }

            return StreamingCellSceneRoot + "/World_" +
                   "Cell_" + cellId.Substring("cell_".Length) +
                   "_Legacy.unity";
        }

        public static string CollisionMesh(string meshGuid) =>
            CollisionMeshRoot + "/" + meshGuid + ".asset";
    }
}
