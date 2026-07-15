using UnityEngine;

namespace MSC.World.Remaster.Editor
{
    public static class WorldRemasterPaths
    {
        public const string BuilderVersion = "05A.1";
        public const string PilotZoneId = "cell_0_-3";
        public const string SourceDatabaseVersion = "04A1.1";

        public const string EntityTable =
            "Assets/Game/World/Content/WorldTransfer/M04A1_WorldEntities.csv";
        public const string Database =
            "Assets/Game/World/Content/WorldTransfer/M04A1_WorldGeometryDatabase.json";

        public const string ProductionRoot = "Assets/Game/World/Production";
        public const string MeshRoot = ProductionRoot + "/Meshes";
        public const string MaterialRoot = ProductionRoot + "/Materials";
        public const string PrefabRoot = ProductionRoot + "/Prefabs";
        public const string SceneRoot = ProductionRoot + "/Scenes";
        public const string AuthoringRoot = "Assets/Game/World/Authoring";
        public const string ReplacementProfileRoot = AuthoringRoot + "/ReplacementProfiles";
        public const string MaterialProfileRoot = AuthoringRoot + "/MaterialProfiles";
        public const string VegetationProfileRoot = AuthoringRoot + "/VegetationProfiles";
        public const string RoadProfileRoot = AuthoringRoot + "/RoadProfiles";
        public const string BuildingProfileRoot = AuthoringRoot + "/BuildingProfiles";
        public const string ZoneProfileRoot = AuthoringRoot + "/ZoneProfiles";
        public const string RegistryAsset = ReplacementProfileRoot + "/WR_WorldProductionAssetRegistry.asset";

        public const string GeneratedRoot = "Assets/Game/World/Generated";
        public const string ProductionCellRoot = GeneratedRoot + "/ProductionCells";
        public const string ProductionProxyRoot = GeneratedRoot + "/ProductionProxies";
        public const string PilotCellScene = ProductionCellRoot + "/Production_cell_0_-3.unity";
        public const string PilotPlaytestScene = SceneRoot + "/WorldRemasterPilotPlaytest.unity";
        public const string ComparisonScene = "Assets/Game/World/Debug/Comparison/WR_HomeYardComparison.unity";
        public const string VehicleAssemblyScene =
            "Assets/Game/Vehicle/Content/Assembly/Scenes/VehicleAssemblyPrototype.unity";

        public const string TerrainMesh = MeshRoot + "/WR_HomeYardTerrain.asset";
        public const string RoadMesh = MeshRoot + "/WR_HomeRoad.asset";
        public const string DrivewayMesh = MeshRoot + "/WR_HomeDriveway.asset";
        public const string DitchWaterMesh = MeshRoot + "/WR_DitchWater.asset";

        public const string GaragePrefab = PrefabRoot + "/WR_HomeGarage.prefab";
        public const string HousePrefab = PrefabRoot + "/WR_HomeHouseShell.prefab";
        public const string InteriorPrefab = PrefabRoot + "/WR_HomeInteriorSlice.prefab";
        public const string TerrainRoadPrefab = PrefabRoot + "/WR_HomeTerrainRoadDitch.prefab";
        public const string VegetationPrefab = PrefabRoot + "/WR_PilotVegetation.prefab";
        public const string TreePrefab = PrefabRoot + "/WR_SpruceTree.prefab";
        public const string PropsPrefab = PrefabRoot + "/WR_HomePropsInfrastructure.prefab";
        public const string PilotZonePrefab = PrefabRoot + "/WR_HomeYardPilot.prefab";

        public const string DocumentationRoot = "Docs/WorldRemaster";
        public const string ReplacementLedger = DocumentationRoot + "/WORLD_REPLACEMENT_LEDGER.csv";
        public const string ArtBacklog = DocumentationRoot + "/WORLD_ART_BACKLOG.csv";
        public const string ZoneStatus = DocumentationRoot + "/ZONE_REMASTER_STATUS.csv";
        public const string PerformanceReport = DocumentationRoot + "/WORLD_REMASTER_PERFORMANCE_REPORT.md";
        public const string ValidationReport = DocumentationRoot + "/WORLD_REMASTER_VALIDATION_REPORT.md";
        public const string VisualCaptureRoot = "PerformanceCaptures/Milestone05A";

        public const string M3MaterialRoot = "Assets/Game/Presentation/Materials/GaragePrototype";
        public const string NeutralLightingPrefab =
            "Assets/Game/Presentation/Lighting/GaragePrototype/M3_Lighting_Neutral.prefab";
        public const string PlayerPrefab = "Assets/Game/Player/Content/Prefabs/M4_FirstPersonPlayer.prefab";
        public static readonly float GarageDoorClearWidthMeters = 3.12f;
        public static readonly float GarageDoorClearHeightMeters = 2.22f;
        public static readonly float RepresentativeVehicleWidthMeters = 2.2f;
        public static readonly float RepresentativeVehicleHeightMeters = 1.75f;

        public static readonly Vector3 HomeGarageAnchor = new Vector3(153.495f, 0.95f, -1033.23f);
        public static readonly Quaternion HomeGarageRotation = Quaternion.Euler(0f, 180f, 0f);
        public static readonly Bounds PilotLocalBounds = new Bounds(
            new Vector3(0f, 2.5f, 5f),
            new Vector3(160f, 12f, 130f));
    }
}
