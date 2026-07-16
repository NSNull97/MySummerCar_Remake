using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Query;
using MSC.Player;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using MSC.World.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.World.Remaster.Editor
{
    public static class ProductionWorldCellBuilder
    {
        private static readonly string[] RequiredFolders =
        {
            WorldRemasterPaths.MeshRoot,
            WorldRemasterPaths.MaterialRoot,
            WorldRemasterPaths.PrefabRoot,
            WorldRemasterPaths.SceneRoot,
            WorldRemasterPaths.ReplacementProfileRoot,
            WorldRemasterPaths.MaterialProfileRoot,
            WorldRemasterPaths.VegetationProfileRoot,
            WorldRemasterPaths.RoadProfileRoot,
            WorldRemasterPaths.BuildingProfileRoot,
            WorldRemasterPaths.ZoneProfileRoot,
            WorldRemasterPaths.ProductionCellRoot,
            WorldRemasterPaths.ProductionProxyRoot,
            "Assets/Game/World/Debug/Comparison"
        };

        [MenuItem("Tools/MSC Remake/World Remaster/Rebuild Generated Production Cells")]
        public static void BuildAll()
        {
            try
            {
                Progress("Подготовка production folders", 0, 10);
                EnsureFolders();
                Dictionary<string, Material> materials = BuildMaterials();
                Dictionary<string, Mesh> meshes = BuildMeshes();
                Progress("Дом и гараж", 1, 10);
                GameObject garage = BuildGaragePrefab(materials);
                GameObject house = BuildHousePrefab(materials);
                GameObject interior = BuildInteriorPrefab(materials);
                Progress("Terrain, road и ditch", 2, 10);
                GameObject terrainRoad = BuildTerrainRoadPrefab(materials, meshes);
                Progress("LOD vegetation", 3, 10);
                GameObject tree = BuildTreePrefab(materials);
                GameObject vegetation = BuildVegetationPrefab(tree);
                Progress("Props и infrastructure", 4, 10);
                GameObject props = BuildPropsInfrastructurePrefab(materials);
                GameObject pilot = BuildPilotZonePrefab(
                    garage,
                    house,
                    interior,
                    terrainRoad,
                    vegetation,
                    props);
                GameObject hedge = BuildHedgePrefab(materials);
                GameObject pier = BuildPierPrefab(materials);
                GameObject shoreline = BuildShorelinePrefab(materials);
                GameObject nextZone = BuildNextZonePrefab(hedge, pier, shoreline);

                Progress("Replacement registry и backlog", 5, 10);
                WorldRemasterRegistryBuilder.BuildRegistryAndMachineReadableFiles();
                Progress("Production cell", 6, 10);
                BuildProductionCellScene(pilot);
                BuildNextZoneCellScene(nextZone);
                Progress("Playtest и comparison", 7, 10);
                BuildPilotPlaytestScene(pilot);
                BuildComparisonScene(pilot, materials["Reference"]);
                BuildNextZonePlaytestScene(pilot, nextZone);
                BuildNextZoneComparisonScene(pilot, nextZone, materials["Reference"]);
                Progress("Интеграция M05 assembly", 8, 10);
                IntegrateVehicleAssemblyScene(pilot);
                Progress("Build Settings и сохранение", 9, 10);
                EnsureScenesInBuildSettings();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log(
                    $"WORLD_REMASTER_05A_BUILD_OK version={WorldRemasterPaths.BuilderVersion} " +
                    $"pilot={WorldRemasterPaths.PilotZoneId} next={WorldRemasterPaths.NextZoneId} " +
                    $"mapped={WorldRemasterRegistryBuilder.TotalMappedRecordCount}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("World Remaster batch build requires batch mode.");
            }

            BuildAll();
        }

        [MenuItem("Tools/MSC Remake/World Remaster/Build Selected Production Cell")]
        public static void BuildSelectedProductionCell() => BuildAll();

        [MenuItem("Tools/MSC Remake/World Remaster/Build All Production Cells")]
        public static void BuildAllProductionCells() => BuildAll();

        [MenuItem("Tools/MSC Remake/World Remaster/Clear Generated Production Cells")]
        public static void ClearGeneratedProductionCells()
        {
            if (!EditorUtility.DisplayDialog(
                    "World Remaster",
                    "Удалить только детерминированные production-cell сцены и proxies? Production prefabs останутся.",
                    "Удалить generated",
                    "Отмена"))
            {
                return;
            }

            AssetDatabase.DeleteAsset(WorldRemasterPaths.ProductionCellRoot);
            AssetDatabase.DeleteAsset(WorldRemasterPaths.ProductionProxyRoot);
            AssetDatabase.Refresh();
        }

        private static Dictionary<string, Material> BuildMaterials()
        {
            return new Dictionary<string, Material>(StringComparer.Ordinal)
            {
                ["Terrain"] = BuildMaterial("WR_Terrain", "SoilGrass", new Color(0.72f, 0.82f, 0.62f), 0f, 0.16f),
                ["Road"] = BuildMaterial("WR_GravelRoad", "RoadGravel", new Color(0.92f, 0.91f, 0.86f), 0f, 0.14f),
                ["Driveway"] = BuildMaterial("WR_Driveway", "RoadShoulder", new Color(0.90f, 0.86f, 0.78f), 0f, 0.12f),
                ["Wood"] = BuildMaterial("WR_PaintedWood", "PaintedWood", new Color(0.72f, 0.60f, 0.52f), 0f, 0.26f),
                ["Interior"] = BuildMaterial("WR_InteriorWall", "PaintedWood", new Color(1.05f, 1.02f, 0.92f), 0f, 0.31f),
                ["Roof"] = BuildMaterial("WR_MetalRoof", "CorrugatedMetal", new Color(0.64f, 0.73f, 0.68f), 0.75f, 0.33f),
                ["Metal"] = BuildMaterial("WR_StructuralMetal", "StructuralMetal", new Color(0.78f, 0.80f, 0.76f), 0.82f, 0.30f),
                ["Concrete"] = BuildMaterial("WR_Concrete", "Concrete", new Color(0.88f, 0.90f, 0.86f), 0f, 0.22f),
                ["Workbench"] = BuildMaterial("WR_WorkbenchWood", "WorkbenchWood", new Color(0.92f, 0.80f, 0.62f), 0f, 0.24f),
                ["Glass"] = BuildMaterial("WR_WindowGlass", "WindowGlass", new Color(0.62f, 0.83f, 0.89f), 0.08f, 0.86f),
                ["Foliage"] = BuildMaterial("WR_Foliage", "Foliage", new Color(0.70f, 0.88f, 0.62f), 0f, 0.18f),
                ["Bark"] = BuildMaterial("WR_Bark", "Bark", new Color(0.85f, 0.70f, 0.57f), 0f, 0.17f),
                ["Water"] = BuildWaterMaterial(),
                ["Reference"] = BuildReferenceMaterial()
            };
        }

        private static Material BuildMaterial(
            string name,
            string m3TextureSet,
            Color tint,
            float metallic,
            float smoothness)
        {
            string path = WorldRemasterPaths.MaterialRoot + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("HDRP/Lit shader is unavailable.");
            }

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            string textureRoot = WorldRemasterPaths.M3MaterialRoot + "/Textures/M3_" + m3TextureSet;
            material.SetColor("_BaseColor", tint);
            material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot + "_BaseColor.png"));
            material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot + "_Normal.png"));
            material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(textureRoot + "_Mask.png"));
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_NormalScale", 0.55f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material BuildWaterMaterial()
        {
            string path = WorldRemasterPaths.MaterialRoot + "/WR_DitchWater.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("HDRP/Lit");
            if (material == null)
            {
                material = new Material(shader) { name = "WR_DitchWater" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", new Color(0.08f, 0.20f, 0.22f, 0.68f));
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.82f);
            material.SetFloat("_SurfaceType", 1f);
            material.SetFloat("_BlendMode", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material BuildReferenceMaterial()
        {
            string path = WorldRemasterPaths.MaterialRoot + "/WR_ReferenceOverlay.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("HDRP/Lit");
            if (material == null)
            {
                material = new Material(shader) { name = "WR_ReferenceOverlay" };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.SetColor("_BaseColor", new Color(1f, 0.08f, 0.72f, 0.32f));
            material.SetColor("_UnlitColor", new Color(1f, 0.08f, 0.72f, 0.32f));
            material.SetFloat("_SurfaceType", 1f);
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Dictionary<string, Mesh> BuildMeshes()
        {
            return new Dictionary<string, Mesh>(StringComparer.Ordinal)
            {
                ["Terrain"] = CreateOrUpdateMesh(WorldRemasterPaths.TerrainMesh, "WR_HomeYardTerrain", mesh => WorldRemasterMeshFactory.BuildTerrain(mesh)),
                ["Road"] = CreateOrUpdateMesh(WorldRemasterPaths.RoadMesh, "WR_HomeRoad", mesh => WorldRemasterMeshFactory.BuildRoad(mesh)),
                ["Driveway"] = CreateOrUpdateMesh(WorldRemasterPaths.DrivewayMesh, "WR_HomeDriveway", WorldRemasterMeshFactory.BuildDriveway),
                ["Water"] = CreateOrUpdateMesh(WorldRemasterPaths.DitchWaterMesh, "WR_DitchWater", WorldRemasterMeshFactory.BuildDitchWater)
            };
        }

        private static GameObject BuildGaragePrefab(IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("WR_HomeGarage");
            try
            {
                const float width = 6.4f;
                const float depth = 12f;
                const float height = 2.7f;
                const float wall = 0.16f;
                GameObject floor = AddBox(
                    root.transform,
                    "Floor",
                    materials["Concrete"],
                    new Vector3(0f, 0.09f, depth * 0.5f),
                    new Vector3(width, 0.18f, depth));
                floor.AddComponent<VehicleSurfaceMetadataAuthoring>()
                    .Configure(VehicleSurfaceType.Paved);
                AddBox(root.transform, "RearWall", materials["Wood"], new Vector3(0f, height * 0.5f, depth), new Vector3(width, height, wall));
                AddBox(root.transform, "LeftWall", materials["Wood"], new Vector3(-width * 0.5f, height * 0.5f, depth * 0.5f), new Vector3(wall, height, depth));
                AddBox(root.transform, "RightWallFront", materials["Wood"], new Vector3(width * 0.5f, height * 0.5f, 2.2f), new Vector3(wall, height, 4.4f));
                AddBox(root.transform, "RightWallRear", materials["Wood"], new Vector3(width * 0.5f, height * 0.5f, 10.35f), new Vector3(wall, height, 3.3f));
                AddBox(root.transform, "RightWallSideDoorHeader", materials["Wood"], new Vector3(width * 0.5f, 2.37f, 6.85f), new Vector3(wall, 0.66f, 1.0f));
                AddBox(root.transform, "FrontLeft", materials["Wood"], new Vector3(-2.38f, height * 0.5f, 0f), new Vector3(1.64f, height, wall));
                AddBox(root.transform, "FrontRight", materials["Wood"], new Vector3(2.38f, height * 0.5f, 0f), new Vector3(1.64f, height, wall));
                AddBox(root.transform, "FrontHeader", materials["Wood"], new Vector3(0f, 2.46f, 0f), new Vector3(3.12f, 0.48f, wall));

                CreateHingedDoor(root.transform, "GarageDoorLeft", materials["Wood"], new Vector3(-1.56f, 0f, -0.1f), new Vector3(0.78f, 1.11f, 0f), new Vector3(1.54f, 2.22f, 0.09f), new Vector3(0f, -105f, 0f));
                CreateHingedDoor(root.transform, "GarageDoorRight", materials["Wood"], new Vector3(1.56f, 0f, -0.1f), new Vector3(-0.78f, 1.11f, 0f), new Vector3(1.54f, 2.22f, 0.09f), new Vector3(0f, 105f, 0f));
                CreateHingedDoor(root.transform, "GarageSideDoor", materials["Wood"], new Vector3(width * 0.5f + 0.1f, 0f, 6.35f), new Vector3(0f, 1.0f, 0.45f), new Vector3(0.09f, 2.0f, 0.9f), new Vector3(0f, 100f, 0f));
                AddBox(root.transform, "SideWindow", materials["Glass"], new Vector3(-width * 0.5f - 0.02f, 1.55f, 7.2f), new Vector3(0.055f, 0.86f, 1.4f), addCollider: false);

                AddBox(root.transform, "RoofLeft", materials["Roof"], new Vector3(-1.58f, 3.18f, depth * 0.5f), new Vector3(3.55f, 0.13f, depth + 0.45f), new Vector3(0f, 0f, 18f));
                AddBox(root.transform, "RoofRight", materials["Roof"], new Vector3(1.58f, 3.18f, depth * 0.5f), new Vector3(3.55f, 0.13f, depth + 0.45f), new Vector3(0f, 0f, -18f));
                AddBox(root.transform, "RoofRidge", materials["Metal"], new Vector3(0f, 3.72f, depth * 0.5f), new Vector3(0.12f, 0.12f, depth + 0.5f));
                return SavePrefab(root, WorldRemasterPaths.GaragePrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildHousePrefab(IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("WR_HomeHouseShell");
            try
            {
                const float width = 16f;
                const float depth = 14f;
                const float height = 2.85f;
                const float wall = 0.18f;
                Vector3 center = new Vector3(11.4f, 0f, 7f);
                AddBox(root.transform, "Foundation", materials["Concrete"], center + new Vector3(0f, 0.13f, 0f), new Vector3(width, 0.26f, depth));
                AddBox(root.transform, "RearWall", materials["Wood"], center + new Vector3(0f, height * 0.5f, depth * 0.5f), new Vector3(width, height, wall));
                AddBox(root.transform, "LeftWall", materials["Wood"], center + new Vector3(-width * 0.5f, height * 0.5f, 0f), new Vector3(wall, height, depth));
                AddBox(root.transform, "RightWall", materials["Wood"], center + new Vector3(width * 0.5f, height * 0.5f, 0f), new Vector3(wall, height, depth));
                AddBox(root.transform, "FrontWallLeft", materials["Wood"], center + new Vector3(-5.8f, height * 0.5f, -depth * 0.5f), new Vector3(4.4f, height, wall));
                AddBox(root.transform, "FrontWallMiddle", materials["Wood"], center + new Vector3(0.2f, height * 0.5f, -depth * 0.5f), new Vector3(5.8f, height, wall));
                AddBox(root.transform, "FrontWallRight", materials["Wood"], center + new Vector3(6.7f, height * 0.5f, -depth * 0.5f), new Vector3(2.6f, height, wall));
                AddBox(root.transform, "FrontDoorHeader", materials["Wood"], center + new Vector3(3.55f, 2.45f, -depth * 0.5f), new Vector3(1.0f, 0.8f, wall));
                CreateHingedDoor(root.transform, "HouseFrontDoor", materials["Wood"], center + new Vector3(3.05f, 0f, -depth * 0.5f - 0.1f), new Vector3(0.5f, 1.05f, 0f), new Vector3(1.0f, 2.1f, 0.09f), new Vector3(0f, -102f, 0f));
                AddBox(root.transform, "FrontWindowLiving", materials["Glass"], center + new Vector3(-2.8f, 1.65f, -depth * 0.5f - 0.03f), new Vector3(2.1f, 1.15f, 0.055f), addCollider: false);
                AddBox(root.transform, "FrontWindowBedroom", materials["Glass"], center + new Vector3(6.0f, 1.65f, -depth * 0.5f - 0.03f), new Vector3(1.25f, 1.15f, 0.055f), addCollider: false);
                AddBox(root.transform, "RearWindowKitchen", materials["Glass"], center + new Vector3(-3.5f, 1.65f, depth * 0.5f + 0.03f), new Vector3(1.8f, 1.1f, 0.055f), addCollider: false);
                AddBox(root.transform, "FrontStep", materials["Concrete"], center + new Vector3(3.55f, 0.18f, -depth * 0.5f - 0.75f), new Vector3(1.7f, 0.36f, 1.35f));
                AddBox(root.transform, "RoofLeft", materials["Roof"], center + new Vector3(-4.0f, 3.62f, 0f), new Vector3(8.9f, 0.15f, depth + 0.55f), new Vector3(0f, 0f, 19f));
                AddBox(root.transform, "RoofRight", materials["Roof"], center + new Vector3(4.0f, 3.62f, 0f), new Vector3(8.9f, 0.15f, depth + 0.55f), new Vector3(0f, 0f, -19f));
                AddBox(root.transform, "Chimney", materials["Concrete"], center + new Vector3(-2.4f, 4.0f, 1.8f), new Vector3(0.72f, 2.15f, 0.72f));
                return SavePrefab(root, WorldRemasterPaths.HousePrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildInteriorPrefab(IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("WR_HomeInteriorSlice");
            try
            {
                AddBox(root.transform, "GarageWorkbenchTop", materials["Workbench"], new Vector3(2.25f, 0.92f, 10.8f), new Vector3(1.65f, 0.14f, 0.72f));
                AddBox(root.transform, "GarageWorkbenchLegA", materials["Metal"], new Vector3(1.62f, 0.45f, 10.8f), new Vector3(0.10f, 0.9f, 0.62f));
                AddBox(root.transform, "GarageWorkbenchLegB", materials["Metal"], new Vector3(2.88f, 0.45f, 10.8f), new Vector3(0.10f, 0.9f, 0.62f));
                for (int shelf = 0; shelf < 4; shelf++)
                {
                    AddBox(root.transform, "GarageShelf_" + shelf, materials["Metal"], new Vector3(-2.65f, 0.42f + shelf * 0.52f, 10.9f), new Vector3(0.85f, 0.06f, 1.65f));
                }

                AddBox(root.transform, "LivingKitchenDivider", materials["Interior"], new Vector3(11.4f, 1.42f, 7.1f), new Vector3(0.14f, 2.75f, 13.6f));
                AddBox(root.transform, "BedroomDivider", materials["Interior"], new Vector3(15.3f, 1.42f, 6.4f), new Vector3(7.7f, 2.75f, 0.14f));
                AddBox(root.transform, "KitchenCounter", materials["Workbench"], new Vector3(7.7f, 0.52f, 11.8f), new Vector3(4.6f, 1.04f, 0.72f));
                AddBox(root.transform, "KitchenStove", materials["Metal"], new Vector3(9.55f, 0.54f, 11.35f), new Vector3(0.75f, 1.08f, 0.65f));
                AddBox(root.transform, "LivingTable", materials["Workbench"], new Vector3(15.2f, 0.56f, 3.2f), new Vector3(2.0f, 0.12f, 1.1f));
                AddBox(root.transform, "SaunaBenchLower", materials["Workbench"], new Vector3(7.1f, 0.55f, 3.7f), new Vector3(2.2f, 0.16f, 0.72f));
                AddBox(root.transform, "SaunaBenchUpper", materials["Workbench"], new Vector3(7.1f, 1.05f, 4.25f), new Vector3(2.2f, 0.16f, 0.72f));
                AddBox(root.transform, "ToolCabinet", materials["Metal"], new Vector3(2.55f, 0.8f, 8.9f), new Vector3(0.75f, 1.6f, 0.58f));
                return SavePrefab(root, WorldRemasterPaths.InteriorPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildTerrainRoadPrefab(
            IReadOnlyDictionary<string, Material> materials,
            IReadOnlyDictionary<string, Mesh> meshes)
        {
            var root = new GameObject("WR_HomeTerrainRoadDitch");
            try
            {
                GameObject terrain = AddMesh(
                    root.transform,
                    "Terrain",
                    meshes["Terrain"],
                    materials["Terrain"],
                    addCollider: true);
                terrain.AddComponent<VehicleSurfaceMetadataAuthoring>()
                    .Configure(VehicleSurfaceType.Grass);
                GameObject road = AddMesh(
                    root.transform,
                    "Road",
                    meshes["Road"],
                    materials["Road"],
                    addCollider: true);
                road.AddComponent<VehicleSurfaceMetadataAuthoring>()
                    .Configure(VehicleSurfaceType.Gravel);
                GameObject driveway = AddMesh(
                    root.transform,
                    "Driveway",
                    meshes["Driveway"],
                    materials["Driveway"],
                    addCollider: true);
                driveway.AddComponent<VehicleSurfaceMetadataAuthoring>()
                    .Configure(VehicleSurfaceType.Gravel);
                AddMesh(root.transform, "DitchWater", meshes["Water"], materials["Water"], addCollider: false);
                return SavePrefab(root, WorldRemasterPaths.TerrainRoadPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildTreePrefab(IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("WR_SpruceTree");
            try
            {
                GameObject lod0 = new GameObject("LOD0");
                lod0.transform.SetParent(root.transform, false);
                GameObject trunk = AddPrimitive(lod0.transform, "Trunk", PrimitiveType.Cylinder, materials["Bark"], new Vector3(0f, 1.8f, 0f), new Vector3(0.38f, 1.8f, 0.38f), addCollider: false);
                GameObject crownA = AddPrimitive(lod0.transform, "CrownLower", PrimitiveType.Sphere, materials["Foliage"], new Vector3(0f, 3.5f, 0f), new Vector3(2.6f, 4.8f, 2.6f), addCollider: false);
                GameObject crownB = AddPrimitive(lod0.transform, "CrownUpper", PrimitiveType.Sphere, materials["Foliage"], new Vector3(0f, 5.4f, 0f), new Vector3(1.75f, 3.25f, 1.75f), addCollider: false);
                GameObject lod1 = new GameObject("LOD1");
                lod1.transform.SetParent(root.transform, false);
                GameObject crownProxy = AddPrimitive(lod1.transform, "CrownProxy", PrimitiveType.Sphere, materials["Foliage"], new Vector3(0f, 4.2f, 0f), new Vector3(2.25f, 6.2f, 2.25f), addCollider: false);
                LODGroup group = root.AddComponent<LODGroup>();
                group.SetLODs(new[]
                {
                    new LOD(0.30f, new[] { trunk.GetComponent<Renderer>(), crownA.GetComponent<Renderer>(), crownB.GetComponent<Renderer>() }),
                    new LOD(0.045f, new[] { crownProxy.GetComponent<Renderer>() })
                });
                group.RecalculateBounds();
                CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0f, 1.8f, 0f);
                collider.radius = 0.28f;
                collider.height = 3.6f;
                return SavePrefab(root, WorldRemasterPaths.TreePrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildVegetationPrefab(GameObject treePrefab)
        {
            var root = new GameObject("WR_PilotVegetation");
            try
            {
                var random = new System.Random(5013);
                int created = 0;
                int attempts = 0;
                while (created < 64 && attempts++ < 1000)
                {
                    float x = Mathf.Lerp(-76f, 76f, (float)random.NextDouble());
                    float z = Mathf.Lerp(-56f, 66f, (float)random.NextDouble());
                    bool homeClearance = x > -13f && x < 25f && z > -8f && z < 21f;
                    bool roadClearance = Mathf.Abs(z + 30f) < 7.5f;
                    bool drivewayClearance = Mathf.Abs(x) < 7f && z > -32f && z < 2f;
                    if (homeClearance || roadClearance || drivewayClearance)
                    {
                        continue;
                    }

                    GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab);
                    tree.name = "Spruce_" + created.ToString("00");
                    tree.transform.SetParent(root.transform, false);
                    tree.transform.localPosition = new Vector3(x, WorldRemasterMeshFactory.TerrainHeight(x, z), z);
                    tree.transform.localRotation = Quaternion.Euler(0f, random.Next(0, 360), 0f);
                    float scale = Mathf.Lerp(0.72f, 1.34f, (float)random.NextDouble());
                    tree.transform.localScale = Vector3.one * scale;
                    created++;
                }

                return SavePrefab(root, WorldRemasterPaths.VegetationPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildPropsInfrastructurePrefab(IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("WR_HomePropsInfrastructure");
            try
            {
                AddPrimitive(root.transform, "OilDrum", PrimitiveType.Cylinder, materials["Metal"], new Vector3(-2.4f, 0.45f, 8.6f), new Vector3(0.72f, 0.45f, 0.72f));
                AddBox(root.transform, "PartsCrate", materials["Workbench"], new Vector3(2.1f, 0.32f, 7.6f), new Vector3(0.8f, 0.64f, 0.62f));
                AddBox(root.transform, "MailboxPost", materials["Wood"], new Vector3(9.5f, 0.85f, -25.6f), new Vector3(0.12f, 1.7f, 0.12f));
                AddBox(root.transform, "Mailbox", materials["Metal"], new Vector3(9.5f, 1.65f, -25.6f), new Vector3(0.55f, 0.38f, 0.72f));
                for (int log = 0; log < 14; log++)
                {
                    AddPrimitive(root.transform, "Firewood_" + log.ToString("00"), PrimitiveType.Cylinder, materials["Bark"], new Vector3(21.2f + (log % 5) * 0.32f, 0.22f + (log / 5) * 0.28f, 12.4f), new Vector3(0.22f, 0.55f, 0.22f), new Vector3(0f, 0f, 90f));
                }

                AddPrimitive(root.transform, "UtilityPoleA", PrimitiveType.Cylinder, materials["Bark"], new Vector3(-14f, 4.5f, -25f), new Vector3(0.42f, 4.5f, 0.42f));
                AddPrimitive(root.transform, "UtilityPoleB", PrimitiveType.Cylinder, materials["Bark"], new Vector3(28f, 4.5f, -31f), new Vector3(0.42f, 4.5f, 0.42f));
                CreateWire(root.transform, materials["Metal"], new Vector3(-14f, 8.7f, -25f), new Vector3(28f, 8.7f, -31f));

                AddBox(root.transform, "GatePostLeft", materials["Wood"], new Vector3(-4.4f, 1.1f, -14.8f), new Vector3(0.22f, 2.2f, 0.22f));
                AddBox(root.transform, "GatePostRight", materials["Wood"], new Vector3(4.4f, 1.1f, -14.8f), new Vector3(0.22f, 2.2f, 0.22f));
                CreateHingedDoor(root.transform, "DrivewayGateLeft", materials["Wood"], new Vector3(-4.25f, 0f, -14.8f), new Vector3(2.1f, 0.8f, 0f), new Vector3(4.2f, 1.6f, 0.12f), new Vector3(0f, -96f, 0f));
                CreateHingedDoor(root.transform, "DrivewayGateRight", materials["Wood"], new Vector3(4.25f, 0f, -14.8f), new Vector3(-2.1f, 0.8f, 0f), new Vector3(4.2f, 1.6f, 0.12f), new Vector3(0f, 96f, 0f));
                return SavePrefab(root, WorldRemasterPaths.PropsPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildHedgePrefab(IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("WR_HedgeSegment");
            try
            {
                GameObject lod0 = new GameObject("LOD0");
                lod0.transform.SetParent(root.transform, false);
                GameObject foliageA = AddBox(
                    lod0.transform,
                    "FoliageLower",
                    materials["Foliage"],
                    Vector3.zero,
                    new Vector3(4.9f, 1.35f, 0.82f),
                    addCollider: false);
                GameObject foliageB = AddBox(
                    lod0.transform,
                    "FoliageUpper",
                    materials["Foliage"],
                    new Vector3(0.1f, 0.58f, 0.02f),
                    new Vector3(4.65f, 0.55f, 0.72f),
                    addCollider: false);

                GameObject lod1 = new GameObject("LOD1");
                lod1.transform.SetParent(root.transform, false);
                GameObject proxy = AddBox(
                    lod1.transform,
                    "FoliageProxy",
                    materials["Foliage"],
                    new Vector3(0f, 0.24f, 0f),
                    new Vector3(4.9f, 1.75f, 0.78f),
                    addCollider: false);

                LODGroup group = root.AddComponent<LODGroup>();
                group.SetLODs(new[]
                {
                    new LOD(0.28f, new[] { foliageA.GetComponent<Renderer>(), foliageB.GetComponent<Renderer>() }),
                    new LOD(0.045f, new[] { proxy.GetComponent<Renderer>() })
                });
                group.RecalculateBounds();
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.24f, 0f);
                collider.size = new Vector3(4.9f, 1.75f, 0.78f);
                return SavePrefab(root, WorldRemasterPaths.HedgePrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildPierPrefab(IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("WR_HomePier");
            try
            {
                GameObject deck = new GameObject("PierDeck");
                deck.transform.SetParent(root.transform, false);
                for (int plank = 0; plank < 14; plank++)
                {
                    AddBox(
                        deck.transform,
                        "DeckPlank_" + plank.ToString("00"),
                        materials["Workbench"],
                        new Vector3(0f, 0.22f, -6.5f + plank),
                        new Vector3(2.65f, 0.22f, 0.92f));
                }

                for (int side = -1; side <= 1; side += 2)
                {
                    for (int support = 0; support < 4; support++)
                    {
                        AddPrimitive(
                            root.transform,
                            $"Support_{side}_{support}",
                            PrimitiveType.Cylinder,
                            materials["Wood"],
                            new Vector3(side * 1.08f, -1.0f, -5.4f + support * 3.6f),
                            new Vector3(0.16f, 1.45f, 0.16f));
                    }
                }

                AddBox(root.transform, "PontoonLeft", materials["Metal"], new Vector3(-0.92f, -0.6f, 0f), new Vector3(0.5f, 0.58f, 12.8f));
                AddBox(root.transform, "PontoonRight", materials["Metal"], new Vector3(0.92f, -0.6f, 0f), new Vector3(0.5f, 0.58f, 12.8f));
                AddBox(root.transform, "ShoreThreshold", materials["Workbench"], new Vector3(0f, 0.16f, -7.25f), new Vector3(3.0f, 0.26f, 1.0f));
                return SavePrefab(root, WorldRemasterPaths.PierPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildShorelinePrefab(IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("WR_HomeShorelineWater");
            try
            {
                Vector3 anchor = WorldRemasterPaths.HomePierAnchor;
                AddBox(
                    root.transform,
                    "BoundedLakeBottom",
                    materials["Terrain"],
                    new Vector3(256f, -35.029f, -768f) - anchor,
                    new Vector3(512f, 2f, 512f),
                    addCollider: false);
                AddBox(
                    root.transform,
                    "BoundedLakeSurface",
                    materials["Water"],
                    new Vector3(336f, -2.929f, -711f) - anchor,
                    new Vector3(352f, 0.045f, 398f),
                    addCollider: false);
                GameObject shoreApproach = AddBox(
                    root.transform,
                    "ShoreApproach",
                    materials["Terrain"],
                    new Vector3(177f, -0.65f, -939f) - anchor,
                    new Vector3(72f, 0.55f, 80f),
                    new Vector3(1.8f, 0f, 0f));
                shoreApproach.AddComponent<VehicleSurfaceMetadataAuthoring>()
                    .Configure(VehicleSurfaceType.Grass);
                GameObject footpath = AddBox(
                    root.transform,
                    "FootpathToPier",
                    materials["Driveway"],
                    new Vector3(177.5f, -0.42f, -939f) - anchor,
                    new Vector3(3.2f, 0.12f, 76f),
                    new Vector3(1.8f, 0f, 0f));
                footpath.AddComponent<VehicleSurfaceMetadataAuthoring>()
                    .Configure(VehicleSurfaceType.Gravel);

                GameObject lakeAnchor = new GameObject("LakeTileReferenceAnchor");
                lakeAnchor.transform.SetParent(root.transform, false);
                lakeAnchor.transform.localPosition = WorldRemasterPaths.LakeTileAnchor - anchor;
                AddStableId(lakeAnchor, "wr05a.batch01.cell_0_-2.lake-tile-anchor");
                GameObject bottomAnchor = new GameObject("LakeBottomReferenceAnchor");
                bottomAnchor.transform.SetParent(root.transform, false);
                bottomAnchor.transform.localPosition = WorldRemasterPaths.LakeBottomAnchor - anchor;
                AddStableId(bottomAnchor, "wr05a.batch01.cell_0_-2.lake-bottom-anchor");
                return SavePrefab(root, WorldRemasterPaths.ShorelinePrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildNextZonePrefab(GameObject hedgePrefab, GameObject pierPrefab, GameObject shorelinePrefab)
        {
            var root = new GameObject("WR_HomeShorelinePier");
            try
            {
                WorldRemasterPilotMarker marker = root.AddComponent<WorldRemasterPilotMarker>();
                marker.Configure(
                    WorldRemasterPaths.NextZoneId,
                    WorldRemasterPaths.NextZoneLocalBounds,
                    WorldRemasterPaths.HomePierAnchor,
                    3,
                    WorldRemasterRegistryBuilder.NextZoneMappedRecordCount,
                    15,
                    manualPending: true);

                GameObject shoreline = InstantiatePrefab(shorelinePrefab, root.transform, "ShorelineAndWater");
                shoreline.transform.localPosition = Vector3.zero;
                AddStableId(shoreline, "wr05a.batch01.cell_0_-2.shoreline");
                GameObject pier = InstantiatePrefab(pierPrefab, root.transform, "HomePier");
                pier.transform.localPosition = Vector3.zero;
                AddStableId(pier, "wr05a.batch01.cell_0_-2.pier");

                string[] hedgeIds =
                {
                    "b8de7336e204fae3ba333227b3e94d19",
                    "449b18de0c10f87887e3f3304a90366e",
                    "847f56ce8c1be238f4bcae514bb55fdf"
                };
                for (int index = 0; index < WorldRemasterPaths.HomeHedgeAnchors.Length; index++)
                {
                    GameObject hedge = InstantiatePrefab(hedgePrefab, root.transform, "Hedge_" + hedgeIds[index]);
                    hedge.transform.localPosition = WorldRemasterPaths.HomeHedgeAnchors[index] - WorldRemasterPaths.HomePierAnchor;
                    hedge.transform.localRotation = Quaternion.Euler(0f, 28.05f, 0f);
                    AddStableId(hedge, "wr05a.batch01.cell_0_-2.hedge." + index);
                }

                return SavePrefab(root, WorldRemasterPaths.NextZonePrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildPilotZonePrefab(params GameObject[] prefabs)
        {
            var root = new GameObject("WR_HomeYardPilot");
            try
            {
                WorldRemasterPilotMarker marker = root.AddComponent<WorldRemasterPilotMarker>();
                marker.Configure(
                    WorldRemasterPaths.PilotZoneId,
                    WorldRemasterPaths.PilotLocalBounds,
                    WorldRemasterPaths.HomeGarageAnchor,
                    12,
                    WorldRemasterRegistryBuilder.PilotMappedRecordCount,
                    671,
                    manualPending: true);
                for (int index = 0; index < prefabs.Length; index++)
                {
                    InstantiatePrefab(prefabs[index], root.transform, prefabs[index].name);
                }

                return SavePrefab(root, WorldRemasterPaths.PilotZonePrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void BuildProductionCellScene(GameObject pilotPrefab)
        {
            SceneAsset existingAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldRemasterPaths.PilotCellScene);
            Scene scene = existingAsset != null
                ? EditorSceneManager.OpenScene(WorldRemasterPaths.PilotCellScene, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            WorldRemasterPilotMarker existingMarker = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldRemasterPilotMarker>(true))
                .FirstOrDefault();
            GameObject production = existingMarker != null && existingMarker.transform.parent == null
                ? existingMarker.gameObject
                : null;
            if (production == null)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                production = InstantiatePrefab(pilotPrefab, null, "WR_Production_cell_0_-3");
            }

            production.name = "WR_Production_cell_0_-3";
            production.transform.SetPositionAndRotation(WorldRemasterPaths.HomeGarageAnchor, WorldRemasterPaths.HomeGarageRotation);
            AddStableId(production, "wr05a.production.cell_0_-3.root");
            WorldHingedArchitecture[] hinges = production.GetComponentsInChildren<WorldHingedArchitecture>(true);
            for (int index = 0; index < hinges.Length; index++)
            {
                AddStableId(hinges[index].gameObject, "wr05a.production.cell_0_-3.hinge." + hinges[index].name);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            SaveScene(scene, WorldRemasterPaths.PilotCellScene);
        }

        private static void BuildNextZoneCellScene(GameObject zonePrefab)
        {
            SceneAsset existingAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldRemasterPaths.NextZoneCellScene);
            Scene scene = existingAsset != null
                ? EditorSceneManager.OpenScene(WorldRemasterPaths.NextZoneCellScene, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            WorldRemasterPilotMarker existingMarker = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldRemasterPilotMarker>(true))
                .FirstOrDefault(marker => marker.ZoneId == WorldRemasterPaths.NextZoneId);
            GameObject production = existingMarker != null && existingMarker.transform.parent == null
                ? existingMarker.gameObject
                : null;
            if (production == null)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }

                production = InstantiatePrefab(zonePrefab, null, "WR_Production_cell_0_-2");
            }

            production.name = "WR_Production_cell_0_-2";
            production.transform.SetPositionAndRotation(WorldRemasterPaths.HomePierAnchor, WorldRemasterPaths.HomePierRotation);
            AddStableId(production, "wr05a.production.cell_0_-2.root");
            EditorSceneManager.MarkSceneDirty(scene);
            SaveScene(scene, WorldRemasterPaths.NextZoneCellScene);
        }

        private static void BuildPilotPlaytestScene(GameObject pilotPrefab)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.PlayerPrefab);
            GameObject lightingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.NeutralLightingPrefab);
            if (playerPrefab == null || lightingPrefab == null)
            {
                throw new InvalidOperationException("M4 player and M3 neutral lighting are required for the 05A playtest scene.");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("WR_05A_PilotPlaytest");
            GameObject production = InstantiatePrefab(pilotPrefab, root.transform, "ProductionWorld");
            production.transform.localPosition = Vector3.zero;
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "Player_M4Architecture";
            player.transform.SetParent(root.transform, false);
            player.transform.localPosition = new Vector3(0f, 0.15f, -5.2f);
            player.transform.localRotation = Quaternion.identity;
            if (player.GetComponent<CrossdotPresenter>() == null)
            {
                throw new InvalidOperationException("05A requires the existing M4 player prefab with CrossdotPresenter.");
            }

            GameObject lighting = InstantiatePrefab(lightingPrefab, root.transform, "NeutralLighting");
            RenderSettings.sun = lighting.GetComponentInChildren<Light>(true);
            root.transform.SetPositionAndRotation(WorldRemasterPaths.HomeGarageAnchor, WorldRemasterPaths.HomeGarageRotation);
            WorldPilotTraversalRoute traversalRoute = WorldPilotTraversalAuthoring.Attach(root, production, player);
            EditorSceneManager.MarkSceneDirty(scene);
            SaveScene(scene, WorldRemasterPaths.PilotPlaytestScene);
            WorldPilotTraversalAuthoring.RefreshDependencyFingerprint(traversalRoute);
            SaveScene(scene, WorldRemasterPaths.PilotPlaytestScene);
        }

        private static void BuildNextZonePlaytestScene(GameObject pilotPrefab, GameObject zonePrefab)
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.PlayerPrefab);
            GameObject lightingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.NeutralLightingPrefab);
            if (playerPrefab == null || lightingPrefab == null)
            {
                throw new InvalidOperationException("M4 player and M3 neutral lighting are required for the 05A Batch 01 playtest scene.");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("WR_05A_Batch01_HomeShorelinePlaytest");
            GameObject pilotContext = InstantiatePrefab(pilotPrefab, root.transform, "AcceptedHomeYardContext");
            pilotContext.transform.SetPositionAndRotation(WorldRemasterPaths.HomeGarageAnchor, WorldRemasterPaths.HomeGarageRotation);
            GameObject production = InstantiatePrefab(zonePrefab, root.transform, "ProductionWorld_cell_0_-2");
            production.transform.SetPositionAndRotation(WorldRemasterPaths.HomePierAnchor, WorldRemasterPaths.HomePierRotation);
            GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
            player.name = "Player_M4Architecture";
            player.transform.SetParent(root.transform, false);
            player.transform.localPosition = new Vector3(177.5f, 1.2f, -925f);
            player.transform.localRotation = Quaternion.identity;
            if (player.GetComponent<CrossdotPresenter>() == null)
            {
                throw new InvalidOperationException("05A Batch 01 requires the existing M4 player prefab with CrossdotPresenter.");
            }

            GameObject lighting = InstantiatePrefab(lightingPrefab, root.transform, "NeutralLighting");
            RenderSettings.sun = lighting.GetComponentInChildren<Light>(true);
            EditorSceneManager.MarkSceneDirty(scene);
            SaveScene(scene, WorldRemasterPaths.NextZonePlaytestScene);
        }

        private static void BuildComparisonScene(GameObject pilotPrefab, Material referenceMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("WR_05A_Comparison");
            GameObject production = InstantiatePrefab(pilotPrefab, root.transform, "ProductionReplacement");
            production.transform.SetPositionAndRotation(WorldRemasterPaths.HomeGarageAnchor, WorldRemasterPaths.HomeGarageRotation);
            GameObject reference = new GameObject("DonorReference_MetadataProxies");
            reference.transform.SetParent(root.transform, false);

            IReadOnlyList<WorldEntityPlacement> entities = WorldRemasterRegistryBuilder.LoadEntities();
            foreach (WorldEntityPlacement entity in entities.Where(item =>
                         item.CellId == WorldRemasterPaths.PilotZoneId &&
                         item.Bounds.size.sqrMagnitude > 0.0001f &&
                         (item.HierarchyPath.StartsWith("YARD/Building/Garage", StringComparison.Ordinal) ||
                          item.Category == "Door" ||
                          item.Category == "Window")))
            {
                GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                proxy.name = "REF_" + entity.StableId + "_" + Sanitize(entity.OriginalName);
                proxy.transform.SetParent(reference.transform, false);
                proxy.transform.position = entity.Bounds.center;
                proxy.transform.localScale = ClampSize(entity.Bounds.size);
                proxy.GetComponent<Renderer>().sharedMaterial = referenceMaterial;
                UnityEngine.Object.DestroyImmediate(proxy.GetComponent<Collider>());
            }

            WorldRemasterModeController modes = root.AddComponent<WorldRemasterModeController>();
            modes.Configure(production, reference, WorldComparisonMode.OverlayComparison);
            GameObject lightingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.NeutralLightingPrefab);
            GameObject lighting = InstantiatePrefab(lightingPrefab, root.transform, "NeutralLighting");
            RenderSettings.sun = lighting.GetComponentInChildren<Light>(true);
            CreateComparisonCamera(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            SaveScene(scene, WorldRemasterPaths.ComparisonScene);
        }

        private static void BuildNextZoneComparisonScene(GameObject pilotPrefab, GameObject zonePrefab, Material referenceMaterial)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("WR_05A_Batch01_Comparison");
            GameObject production = new GameObject("ProductionReplacement");
            production.transform.SetParent(root.transform, false);
            GameObject pilotContext = InstantiatePrefab(pilotPrefab, production.transform, "AcceptedHomeYardContext");
            pilotContext.transform.SetPositionAndRotation(WorldRemasterPaths.HomeGarageAnchor, WorldRemasterPaths.HomeGarageRotation);
            GameObject zone = InstantiatePrefab(zonePrefab, production.transform, "Batch01_HomeShorelinePier");
            zone.transform.SetPositionAndRotation(WorldRemasterPaths.HomePierAnchor, WorldRemasterPaths.HomePierRotation);
            GameObject reference = new GameObject("DonorReference_MetadataPositionProxies");
            reference.transform.SetParent(root.transform, false);

            HashSet<string> includedIds = new HashSet<string>(StringComparer.Ordinal)
            {
                "345dc7662dae9f1f01d77b15f74e5f8f",
                "56a7aa7c66146248d6c820c31a6b99fd",
                "de5d5682cbef7d27a48a473d5e85877d",
                "e0fa39e1ceeed93727dd86e749c6d115",
                "b412961b75cb019e74a83b24faac32a4",
                "f700b12cf5c75a3906dd079acea3f274",
                "b8de7336e204fae3ba333227b3e94d19",
                "449b18de0c10f87887e3f3304a90366e",
                "847f56ce8c1be238f4bcae514bb55fdf"
            };
            foreach (WorldEntityPlacement entity in WorldRemasterRegistryBuilder.LoadEntities()
                         .Where(item => includedIds.Contains(item.StableId)))
            {
                GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                proxy.name = "REF_" + entity.StableId + "_" + Sanitize(entity.OriginalName);
                proxy.transform.SetParent(reference.transform, false);
                proxy.transform.position = entity.Position;
                proxy.transform.localScale = Vector3.one * 0.7f;
                proxy.GetComponent<Renderer>().sharedMaterial = referenceMaterial;
                UnityEngine.Object.DestroyImmediate(proxy.GetComponent<Collider>());
            }

            WorldRemasterModeController modes = root.AddComponent<WorldRemasterModeController>();
            modes.Configure(production, reference, WorldComparisonMode.OverlayComparison);
            GameObject lightingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(WorldRemasterPaths.NeutralLightingPrefab);
            GameObject lighting = InstantiatePrefab(lightingPrefab, root.transform, "NeutralLighting");
            RenderSettings.sun = lighting.GetComponentInChildren<Light>(true);
            CreateZoneComparisonCamera(
                root.transform,
                "Pier Comparison Camera",
                WorldRemasterPaths.HomePierAnchor + new Vector3(20f, 12f, -22f),
                WorldRemasterPaths.HomePierAnchor + new Vector3(0f, 0f, 0f),
                addAudioListener: true);
            CreateZoneComparisonCamera(
                root.transform,
                "Hedge Comparison Camera",
                new Vector3(181f, 10f, -1038f),
                WorldRemasterPaths.HomeHedgeAnchors[1],
                addAudioListener: false);
            CreateZoneComparisonCamera(
                root.transform,
                "Seam Comparison Camera",
                new Vector3(196f, 12f, -992f),
                new Vector3(177.5f, 0f, -972f),
                addAudioListener: false);
            EditorSceneManager.MarkSceneDirty(scene);
            SaveScene(scene, WorldRemasterPaths.NextZoneComparisonScene);
        }

        private static void IntegrateVehicleAssemblyScene(GameObject pilotPrefab)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(WorldRemasterPaths.VehicleAssemblyScene) == null)
            {
                throw new InvalidOperationException("M05 vehicle assembly scene is required before 05A integration.");
            }

            Scene scene = EditorSceneManager.OpenScene(WorldRemasterPaths.VehicleAssemblyScene, OpenSceneMode.Single);
            VehicleAssemblyController controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<VehicleAssemblyController>(true))
                .FirstOrDefault();
            if (controller == null)
            {
                throw new InvalidOperationException("M05 assembly controller was not found for 05A integration.");
            }

            Transform sceneRoot = controller.transform.root;
            RemoveDirectChild(sceneRoot, "AssemblyWorkshopFloor");
            RemoveDirectChild(sceneRoot, "WR_05A_PilotWorld");
            sceneRoot.SetPositionAndRotation(WorldRemasterPaths.HomeGarageAnchor, WorldRemasterPaths.HomeGarageRotation);
            GameObject world = InstantiatePrefab(pilotPrefab, sceneRoot, "WR_05A_PilotWorld");
            world.transform.localPosition = Vector3.zero;
            world.transform.localRotation = Quaternion.identity;
            EditorSceneManager.MarkSceneDirty(scene);
            SaveScene(scene, WorldRemasterPaths.VehicleAssemblyScene);
        }

        private static void EnsureScenesInBuildSettings()
        {
            var paths = new[]
            {
                WorldRemasterPaths.PilotPlaytestScene,
                WorldRemasterPaths.PilotCellScene,
                WorldRemasterPaths.NextZonePlaytestScene,
                WorldRemasterPaths.NextZoneCellScene
            };
            var updated = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (string path in paths)
            {
                int index = updated.FindIndex(scene => string.Equals(scene.path, path, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    updated[index] = new EditorBuildSettingsScene(path, true);
                }
                else
                {
                    updated.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            EditorBuildSettings.scenes = updated.ToArray();
        }

        private static void CreateHingedDoor(
            Transform parent,
            string name,
            Material material,
            Vector3 hingePosition,
            Vector3 panelLocalPosition,
            Vector3 panelScale,
            Vector3 openEuler)
        {
            GameObject hinge = new GameObject(name);
            hinge.transform.SetParent(parent, false);
            hinge.transform.localPosition = hingePosition;
            GameObject panel = AddBox(hinge.transform, "Panel", material, panelLocalPosition, panelScale);
            WorldHingedArchitecture architecture = hinge.AddComponent<WorldHingedArchitecture>();
            architecture.Configure(hinge.transform, Vector3.zero, openEuler, 105f, "Открыть " + name, "Закрыть " + name);
            hinge.AddComponent<InteractionTargetHost>().Configure(architecture);
            panel.isStatic = false;
        }

        private static void CreateWire(Transform parent, Material material, Vector3 start, Vector3 end)
        {
            GameObject wire = new GameObject("UtilityWire");
            wire.transform.SetParent(parent, false);
            LineRenderer renderer = wire.AddComponent<LineRenderer>();
            renderer.sharedMaterial = material;
            renderer.positionCount = 5;
            renderer.startWidth = 0.035f;
            renderer.endWidth = 0.035f;
            renderer.useWorldSpace = false;
            for (int index = 0; index < 5; index++)
            {
                float t = index / 4f;
                Vector3 point = Vector3.Lerp(start, end, t);
                point.y -= Mathf.Sin(t * Mathf.PI) * 0.42f;
                renderer.SetPosition(index, point);
            }
        }

        private static GameObject AddBox(
            Transform parent,
            string name,
            Material material,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler = default,
            bool addCollider = true)
        {
            return AddPrimitive(parent, name, PrimitiveType.Cube, material, localPosition, localScale, localEuler, addCollider);
        }

        private static GameObject AddPrimitive(
            Transform parent,
            string name,
            PrimitiveType primitive,
            Material material,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler = default,
            bool addCollider = true)
        {
            GameObject instance = GameObject.CreatePrimitive(primitive);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(localEuler);
            instance.transform.localScale = localScale;
            instance.GetComponent<Renderer>().sharedMaterial = material;
            Collider collider = instance.GetComponent<Collider>();
            if (!addCollider && collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            if (addCollider)
            {
                GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            }

            return instance;
        }

        private static GameObject AddMesh(
            Transform parent,
            string name,
            Mesh mesh,
            Material material,
            bool addCollider)
        {
            GameObject instance = new GameObject(name);
            instance.transform.SetParent(parent, false);
            instance.AddComponent<MeshFilter>().sharedMesh = mesh;
            instance.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (addCollider)
            {
                instance.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            GameObjectUtility.SetStaticEditorFlags(
                instance,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            return instance;
        }

        private static Mesh CreateOrUpdateMesh(string path, string name, Action<Mesh> build)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }

            build(mesh);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (saved == null)
            {
                throw new InvalidOperationException("Could not save production prefab: " + path);
            }

            return saved;
        }

        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent, string name)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            if (parent != null)
            {
                instance.transform.SetParent(parent, false);
            }

            return instance;
        }

        private static void CreateComparisonCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("Comparison Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(parent, false);
            Vector3 target = WorldRemasterPaths.HomeGarageAnchor + new Vector3(7f, 1.5f, -5f);
            cameraObject.transform.position = WorldRemasterPaths.HomeGarageAnchor + new Vector3(34f, 24f, 31f);
            cameraObject.transform.rotation = Quaternion.LookRotation(target - cameraObject.transform.position, Vector3.up);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 54f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 900f;
            cameraObject.AddComponent<HDAdditionalCameraData>();
            cameraObject.AddComponent<AudioListener>();
        }

        private static void CreateZoneComparisonCamera(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 target,
            bool addAudioListener)
        {
            GameObject cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.position = position;
            cameraObject.transform.rotation = Quaternion.LookRotation(target - position, Vector3.up);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 52f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 1200f;
            cameraObject.AddComponent<HDAdditionalCameraData>();
            if (addAudioListener)
            {
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<AudioListener>();
            }
        }

        private static void AddStableId(GameObject target, string seed)
        {
            StableEntityIdAuthoring identity = target.GetComponent<StableEntityIdAuthoring>();
            if (identity == null)
            {
                identity = target.AddComponent<StableEntityIdAuthoring>();
            }

            var serialized = new SerializedObject(identity);
            SerializedProperty property = serialized.FindProperty("stableId");
            property.stringValue = Hash128.Compute(seed).ToString();
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveDirectChild(Transform parent, string name)
        {
            for (int index = parent.childCount - 1; index >= 0; index--)
            {
                Transform child = parent.GetChild(index);
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static Vector3 ClampSize(Vector3 size) => new Vector3(
            Mathf.Max(0.04f, Mathf.Abs(size.x)),
            Mathf.Max(0.04f, Mathf.Abs(size.y)),
            Mathf.Max(0.04f, Mathf.Abs(size.z)));

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unnamed";
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(character => invalid.Contains(character) ? '_' : character).ToArray());
        }

        private static void SaveScene(Scene scene, string path)
        {
            EnsureFolderForAsset(path);
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException("Could not save scene: " + path);
            }
        }

        private static void EnsureFolders()
        {
            foreach (string folder in RequiredFolders)
            {
                EnsureFolder(folder);
            }
        }

        private static void EnsureFolderForAsset(string assetPath)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(folder))
            {
                EnsureFolder(folder);
            }
        }

        private static void EnsureFolder(string folder)
        {
            string normalized = folder.Replace('\\', '/').TrimEnd('/');
            string[] parts = normalized.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }

                current = next;
            }
        }

        private static void Progress(string stage, int index, int total)
        {
            float progress = total <= 0 ? 0f : index / (float)total;
            if (Application.isBatchMode)
            {
                Debug.Log($"WORLD_REMASTER_05A_PROGRESS {index}/{total} {stage}");
                return;
            }

            if (EditorUtility.DisplayCancelableProgressBar("World Remaster 05A", stage, progress))
            {
                throw new OperationCanceledException(
                    "World Remaster generation cancelled before registry/cell validation; partial output is not marked valid.");
            }
        }
    }
}
