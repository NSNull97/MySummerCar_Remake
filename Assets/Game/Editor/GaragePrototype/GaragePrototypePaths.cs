using UnityEngine;

namespace MSC.Editor.GaragePrototype
{
    public static class GaragePrototypePaths
    {
        public const string BuilderVersion = "1.0.0";
        public const string ProductionRoot = "Assets/Game/World/Content/GaragePrototype";
        public const string MeshRoot = ProductionRoot + "/Meshes";
        public const string PrefabRoot = ProductionRoot + "/Prefabs";
        public const string SceneRoot = ProductionRoot + "/Scenes";
        public const string ProductionScene = SceneRoot + "/GarageArtPrototype.unity";

        public const string MaterialRoot =
            "Assets/Game/Presentation/Materials/GaragePrototype";
        public const string TextureRoot = MaterialRoot + "/Textures";
        public const string LightingRoot =
            "Assets/Game/Presentation/Lighting/GaragePrototype";

        public const string GarageRoofPrefab = PrefabRoot + "/M3_GarageRoof.prefab";
        public const string GarageShellPrefab = PrefabRoot + "/M3_GarageShell.prefab";
        public const string GarageInteriorPrefab = PrefabRoot + "/M3_GarageInterior.prefab";
        public const string RoadPrefab = PrefabRoot + "/M3_RoadSegment.prefab";
        public const string TerrainPrefab = PrefabRoot + "/M3_TerrainPatch.prefab";
        public const string TreePrefab = PrefabRoot + "/M3_SpruceTree.prefab";
        public const string VegetationPrefab = PrefabRoot + "/M3_VegetationCluster.prefab";

        public const string NeutralLightingPrefab =
            LightingRoot + "/M3_Lighting_Neutral.prefab";
        public const string LateDayLightingPrefab =
            LightingRoot + "/M3_Lighting_LateDay.prefab";
        public const string NeutralVolumeProfile =
            LightingRoot + "/M3_NeutralVolume.asset";
        public const string LateDayVolumeProfile =
            LightingRoot + "/M3_LateDayVolume.asset";

        public const string ScalePivotRecord =
            "Assets/Game/LegacyImport/Manifests/M3_GarageScalePivotRecord.asset";
        public const string ComparisonScene =
            "Assets/Game/LegacyImport/ReferenceOnly/Comparison/GarageM3Comparison.unity";
        public const string DonorRoofReference =
            "Assets/Game/LegacyImport/ReferenceOnly/ControlledProof/Environment/garage_shed_roof_reference.obj";

        public const string UnitBoxMesh = MeshRoot + "/M3_UnitBox.asset";
        public const string GarageRoofLod0Mesh = MeshRoot + "/M3_GarageRoof_LOD0.asset";
        public const string GarageRoofLod1Mesh = MeshRoot + "/M3_GarageRoof_LOD1.asset";
        public const string RoadLod0Mesh = MeshRoot + "/M3_Road_LOD0.asset";
        public const string RoadLod1Mesh = MeshRoot + "/M3_Road_LOD1.asset";
        public const string RoadShoulderMesh = MeshRoot + "/M3_RoadShoulder.asset";
        public const string TerrainMesh = MeshRoot + "/M3_TerrainPatch.asset";
        public const string CylinderMesh = MeshRoot + "/M3_Cylinder.asset";
        public const string TreeTrunkMesh = MeshRoot + "/M3_TreeTrunk.asset";
        public const string TreeCrownLod0Mesh = MeshRoot + "/M3_TreeCrown_LOD0.asset";
        public const string TreeCrownLod1Mesh = MeshRoot + "/M3_TreeCrown_LOD1.asset";

        public const string StaticCapturePath =
            "PerformanceCaptures/Milestone03/GaragePrototype_Static.json";
        public const string PlayerCapturePath =
            "PerformanceCaptures/Milestone03/GaragePrototype_Player_1080p.json";
        public const string PerformanceBuildPath =
            "Builds/Milestone03/MySummerCar_Remake_M3_Performance.exe";

        public const float RoadLengthMeters = 180f;
        public const float GarageEaveHeightMeters = 2.45f;
        public const float GarageWallThicknessMeters = 0.16f;
        public const float DimensionalToleranceMeters = 0.005f;

        public static readonly Vector3 DonorRoofBoundsMin =
            new Vector3(-2.280219078f, -1.648790002f, 0.179946005f);
        public static readonly Vector3 DonorRoofBoundsMax =
            new Vector3(2.489785910f, 1.861611009f, 0.524957001f);
        public static readonly Vector3 ProductionRoofBoundsMin =
            new Vector3(DonorRoofBoundsMin.x, DonorRoofBoundsMin.z, DonorRoofBoundsMin.y);
        public static readonly Vector3 ProductionRoofBoundsMax =
            new Vector3(DonorRoofBoundsMax.x, DonorRoofBoundsMax.z, DonorRoofBoundsMax.y);

        public static float GarageWidthMeters =>
            ProductionRoofBoundsMax.x - ProductionRoofBoundsMin.x;
        public static float GarageDepthMeters =>
            ProductionRoofBoundsMax.z - ProductionRoofBoundsMin.z;
    }
}
