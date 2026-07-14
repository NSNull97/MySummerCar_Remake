using System;
using System.Collections.Generic;
using System.IO;
using MSC.LegacyImport.Editor.Ledger;
using MSC.World.GaragePrototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.Editor.GaragePrototype
{
    public static class GaragePrototypeAssetBuilder
    {
        private const string LedgerPath = "Docs/Porting/PORTING_LEDGER.csv";

        private static readonly MaterialSpec[] MaterialSpecs =
        {
            new MaterialSpec("Concrete", new Color(0.42f, 0.43f, 0.40f), 0f, 0.24f, 11, SurfacePattern.Grain),
            new MaterialSpec("PaintedWood", new Color(0.25f, 0.12f, 0.075f), 0f, 0.28f, 23, SurfacePattern.Boards),
            new MaterialSpec("CorrugatedMetal", new Color(0.18f, 0.23f, 0.22f), 0.72f, 0.34f, 31, SurfacePattern.Corrugated),
            new MaterialSpec("StructuralMetal", new Color(0.12f, 0.13f, 0.12f), 0.82f, 0.32f, 41, SurfacePattern.Grain),
            new MaterialSpec("WorkbenchWood", new Color(0.32f, 0.19f, 0.095f), 0f, 0.26f, 53, SurfacePattern.Wood),
            new MaterialSpec("RoadGravel", new Color(0.31f, 0.29f, 0.25f), 0f, 0.18f, 61, SurfacePattern.Gravel),
            new MaterialSpec("RoadShoulder", new Color(0.25f, 0.22f, 0.17f), 0f, 0.12f, 67, SurfacePattern.Gravel),
            new MaterialSpec("SoilGrass", new Color(0.18f, 0.25f, 0.105f), 0f, 0.16f, 71, SurfacePattern.Ground),
            new MaterialSpec("Foliage", new Color(0.085f, 0.20f, 0.055f), 0f, 0.20f, 83, SurfacePattern.Needles),
            new MaterialSpec("Bark", new Color(0.16f, 0.085f, 0.04f), 0f, 0.18f, 97, SurfacePattern.Bark),
            new MaterialSpec("WindowGlass", new Color(0.18f, 0.29f, 0.31f), 0.08f, 0.86f, 101, SurfacePattern.Grain),
            new MaterialSpec("ReferenceNeutral", new Color(0.26f, 0.48f, 0.62f), 0.1f, 0.34f, 107, SurfacePattern.Grain)
        };

        [MenuItem("Tools/My Summer Car/Milestone 3/Build Garage Art Prototype")]
        public static void Build()
        {
            EnsureAssetDirectory(GaragePrototypePaths.UnitBoxMesh);
            EnsureAssetDirectory(GaragePrototypePaths.TextureRoot + "/placeholder.txt");
            EnsureAssetDirectory(GaragePrototypePaths.NeutralVolumeProfile);

            Dictionary<string, Material> materials = BuildMaterialLibrary();
            Dictionary<string, Mesh> meshes = BuildMeshLibrary();

            GameObject roofPrefab = BuildRoofPrefab(meshes, materials);
            GameObject garageShellPrefab = BuildGarageShellPrefab(meshes, materials, roofPrefab);
            GameObject interiorPrefab = BuildInteriorPrefab(meshes, materials);
            GameObject roadPrefab = BuildRoadPrefab(meshes, materials);
            GameObject terrainPrefab = BuildTerrainPrefab(meshes, materials);
            GameObject treePrefab = BuildTreePrefab(meshes, materials);
            GameObject vegetationPrefab = BuildVegetationPrefab(treePrefab);
            GameObject neutralLighting = BuildLightingPreset(
                "M3_Lighting_Neutral",
                GaragePrototypePaths.NeutralLightingPrefab,
                GaragePrototypePaths.NeutralVolumeProfile,
                14f,
                620f,
                95000f,
                new Color(1f, 0.97f, 0.90f),
                new Vector3(48f, -28f, 0f));
            GameObject lateDayLighting = BuildLightingPreset(
                "M3_Lighting_LateDay",
                GaragePrototypePaths.LateDayLightingPrefab,
                GaragePrototypePaths.LateDayVolumeProfile,
                12.7f,
                420f,
                48000f,
                new Color(1f, 0.78f, 0.56f),
                new Vector3(72f, -42f, 0f));

            BuildScalePivotRecord(roofPrefab, meshes["GarageRoofLod0"].bounds);
            BuildProductionScene(
                garageShellPrefab,
                interiorPrefab,
                roadPrefab,
                terrainPrefab,
                vegetationPrefab,
                lateDayLighting);
            BuildComparisonScene(roofPrefab, neutralLighting, materials["ReferenceNeutral"]);
            EnsureProductionSceneInBuildSettings();
            UpdateLedger();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GaragePrototypeMetricsCapture capture =
                GaragePrototypeMetrics.CollectProductionScene(writeCapture: true);
            Debug.Log(
                $"M3_GARAGE_BUILD_OK version={GaragePrototypePaths.BuilderVersion} " +
                $"triangles={capture.triangles} renderers={capture.renderers} " +
                $"materials={capture.uniqueMaterials} lodGroups={capture.lodGroups}");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Milestone 3 batch build requires batch mode.");
            }

            Build();
        }

        private static Dictionary<string, Material> BuildMaterialLibrary()
        {
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (MaterialSpec spec in MaterialSpecs)
            {
                string basePath = $"{GaragePrototypePaths.TextureRoot}/M3_{spec.Id}_BaseColor.png";
                string normalPath = $"{GaragePrototypePaths.TextureRoot}/M3_{spec.Id}_Normal.png";
                string maskPath = $"{GaragePrototypePaths.TextureRoot}/M3_{spec.Id}_Mask.png";
                WriteTexture(basePath, spec, TextureKind.BaseColor);
                WriteTexture(normalPath, spec, TextureKind.Normal);
                WriteTexture(maskPath, spec, TextureKind.Mask);

                Shader shader = Shader.Find("HDRP/Lit");
                if (shader == null)
                {
                    throw new InvalidOperationException("HDRP/Lit shader is unavailable.");
                }

                string materialPath = $"{GaragePrototypePaths.MaterialRoot}/M3_{spec.Id}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null)
                {
                    material = new Material(shader) { name = "M3_" + spec.Id };
                    AssetDatabase.CreateAsset(material, materialPath);
                }
                else
                {
                    material.shader = shader;
                }

                material.SetColor("_BaseColor", Color.white);
                material.SetTexture("_BaseColorMap", AssetDatabase.LoadAssetAtPath<Texture2D>(basePath));
                material.SetTexture("_NormalMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                material.SetTexture("_MaskMap", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
                material.SetFloat("_NormalScale", spec.Pattern == SurfacePattern.Gravel ? 1f : 0.55f);
                material.SetFloat("_Metallic", spec.Metallic);
                material.SetFloat("_Smoothness", spec.Smoothness);
                EditorUtility.SetDirty(material);
                result.Add(spec.Id, material);
            }

            return result;
        }

        private static Dictionary<string, Mesh> BuildMeshLibrary()
        {
            var result = new Dictionary<string, Mesh>(StringComparer.Ordinal)
            {
                ["UnitBox"] = CreateOrUpdateMesh(
                    GaragePrototypePaths.UnitBoxMesh,
                    "M3_UnitBox",
                    GaragePrototypeMeshFactory.BuildUnitBox),
                ["GarageRoofLod0"] = CreateOrUpdateMesh(
                    GaragePrototypePaths.GarageRoofLod0Mesh,
                    "M3_GarageRoof_LOD0",
                    GaragePrototypeMeshFactory.BuildMappedGarageRoof),
                ["GarageRoofLod1"] = CreateOrUpdateMesh(
                    GaragePrototypePaths.GarageRoofLod1Mesh,
                    "M3_GarageRoof_LOD1",
                    mesh => GaragePrototypeMeshFactory.BuildBoundsBox(
                        mesh,
                        new Bounds(
                            (GaragePrototypePaths.ProductionRoofBoundsMin + GaragePrototypePaths.ProductionRoofBoundsMax) * 0.5f,
                            GaragePrototypePaths.ProductionRoofBoundsMax - GaragePrototypePaths.ProductionRoofBoundsMin))),
                ["RoadLod0"] = CreateOrUpdateMesh(
                    GaragePrototypePaths.RoadLod0Mesh,
                    "M3_Road_LOD0",
                    mesh => GaragePrototypeMeshFactory.BuildRoadStrip(mesh, 72, 5.4f, 0.03f)),
                ["RoadLod1"] = CreateOrUpdateMesh(
                    GaragePrototypePaths.RoadLod1Mesh,
                    "M3_Road_LOD1",
                    mesh => GaragePrototypeMeshFactory.BuildRoadStrip(mesh, 18, 5.4f, 0.03f)),
                ["RoadShoulder"] = CreateOrUpdateMesh(
                    GaragePrototypePaths.RoadShoulderMesh,
                    "M3_RoadShoulder",
                    mesh => GaragePrototypeMeshFactory.BuildRoadStrip(mesh, 72, 8.4f, 0.015f)),
                ["Terrain"] = CreateOrUpdateMesh(
                    GaragePrototypePaths.TerrainMesh,
                    "M3_TerrainPatch",
                    mesh => GaragePrototypeMeshFactory.BuildTerrainPatch(mesh, 44, 84)),
                ["Cylinder"] = CreateOrUpdateMesh(
                    GaragePrototypePaths.CylinderMesh,
                    "M3_Cylinder",
                    mesh => GaragePrototypeMeshFactory.BuildCylinder(mesh, 20, 0.5f, 1f)),
                ["TreeTrunk"] = CreateOrUpdateMesh(
                    GaragePrototypePaths.TreeTrunkMesh,
                    "M3_TreeTrunk",
                    mesh => GaragePrototypeMeshFactory.BuildCylinder(mesh, 10, 0.18f, 2.4f)),
                ["TreeCrownLod0"] = CreateOrUpdateMesh(
                    GaragePrototypePaths.TreeCrownLod0Mesh,
                    "M3_TreeCrown_LOD0",
                    mesh => GaragePrototypeMeshFactory.BuildCone(mesh, 12, 1.5f, 3.8f)),
                ["TreeCrownLod1"] = CreateOrUpdateMesh(
                    GaragePrototypePaths.TreeCrownLod1Mesh,
                    "M3_TreeCrown_LOD1",
                    mesh => GaragePrototypeMeshFactory.BuildCone(mesh, 6, 1.45f, 3.7f))
            };
            return result;
        }

        private static GameObject BuildRoofPrefab(
            IReadOnlyDictionary<string, Mesh> meshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("M3_GarageRoof");
            try
            {
                Bounds bounds = meshes["GarageRoofLod0"].bounds;
                BoxCollider collider = root.AddComponent<BoxCollider>();
                collider.center = bounds.center;
                collider.size = bounds.size;
                MeshRenderer lod0 = AddMeshChild(
                    root.transform, "LOD0_CorrugatedRoof", meshes["GarageRoofLod0"], materials["CorrugatedMetal"]);
                MeshRenderer lod1 = AddMeshChild(
                    root.transform, "LOD1_RoofProxy", meshes["GarageRoofLod1"], materials["CorrugatedMetal"]);
                ConfigureLodGroup(root, new[] { lod0 }, new[] { lod1 }, 0.46f, 0.08f);
                AddMarker(root.transform, "RoofPivot_DonorLocalZero", Vector3.zero);
                return SavePrefab(root, GaragePrototypePaths.GarageRoofPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildGarageShellPrefab(
            IReadOnlyDictionary<string, Mesh> meshes,
            IReadOnlyDictionary<string, Material> materials,
            GameObject roofPrefab)
        {
            var root = new GameObject("M3_GarageShell");
            try
            {
                Mesh box = meshes["UnitBox"];
                float minX = GaragePrototypePaths.ProductionRoofBoundsMin.x;
                float maxX = GaragePrototypePaths.ProductionRoofBoundsMax.x;
                float minZ = GaragePrototypePaths.ProductionRoofBoundsMin.z;
                float maxZ = GaragePrototypePaths.ProductionRoofBoundsMax.z;
                float centerX = (minX + maxX) * 0.5f;
                float centerZ = (minZ + maxZ) * 0.5f;
                float width = maxX - minX;
                float depth = maxZ - minZ;
                float height = GaragePrototypePaths.GarageEaveHeightMeters;
                float thickness = GaragePrototypePaths.GarageWallThicknessMeters;

                AddBox(root.transform, "FloorSlab", box, materials["Concrete"],
                    new Vector3(centerX, 0.09f, centerZ), new Vector3(width + 0.35f, 0.18f, depth + 0.35f));
                AddBox(root.transform, "RearWall", box, materials["PaintedWood"],
                    new Vector3(centerX, height * 0.5f, maxZ), new Vector3(width, height, thickness));
                AddBox(root.transform, "LeftWall", box, materials["PaintedWood"],
                    new Vector3(minX, height * 0.5f, centerZ), new Vector3(thickness, height, depth));

                const float vehicleOpeningWidth = 3.2f;
                const float vehicleOpeningHeight = 2.05f;
                float sideWidth = (width - vehicleOpeningWidth) * 0.5f;
                AddBox(root.transform, "FrontWall_Left", box, materials["PaintedWood"],
                    new Vector3(minX + sideWidth * 0.5f, height * 0.5f, minZ),
                    new Vector3(sideWidth, height, thickness));
                AddBox(root.transform, "FrontWall_Right", box, materials["PaintedWood"],
                    new Vector3(maxX - sideWidth * 0.5f, height * 0.5f, minZ),
                    new Vector3(sideWidth, height, thickness));
                AddBox(root.transform, "FrontWall_Header", box, materials["PaintedWood"],
                    new Vector3(centerX, vehicleOpeningHeight + (height - vehicleOpeningHeight) * 0.5f, minZ),
                    new Vector3(vehicleOpeningWidth, height - vehicleOpeningHeight, thickness));

                const float sideDoorWidth = 0.86f;
                const float sideDoorHeight = 2.0f;
                float doorCenterZ = centerZ + 0.25f;
                float frontSegment = doorCenterZ - sideDoorWidth * 0.5f - minZ;
                float rearSegment = maxZ - (doorCenterZ + sideDoorWidth * 0.5f);
                AddBox(root.transform, "RightWall_Front", box, materials["PaintedWood"],
                    new Vector3(maxX, height * 0.5f, minZ + frontSegment * 0.5f),
                    new Vector3(thickness, height, frontSegment));
                AddBox(root.transform, "RightWall_Rear", box, materials["PaintedWood"],
                    new Vector3(maxX, height * 0.5f, maxZ - rearSegment * 0.5f),
                    new Vector3(thickness, height, rearSegment));
                AddBox(root.transform, "RightWall_Header", box, materials["PaintedWood"],
                    new Vector3(maxX, sideDoorHeight + (height - sideDoorHeight) * 0.5f, doorCenterZ),
                    new Vector3(thickness, height - sideDoorHeight, sideDoorWidth));

                AddBox(root.transform, "VehicleDoor_Left", box, materials["PaintedWood"],
                    new Vector3(centerX - vehicleOpeningWidth * 0.25f, vehicleOpeningHeight * 0.5f, minZ - 0.07f),
                    new Vector3(vehicleOpeningWidth * 0.5f - 0.025f, vehicleOpeningHeight, 0.08f));
                AddBox(root.transform, "VehicleDoor_Right", box, materials["PaintedWood"],
                    new Vector3(centerX + vehicleOpeningWidth * 0.25f, vehicleOpeningHeight * 0.5f, minZ - 0.07f),
                    new Vector3(vehicleOpeningWidth * 0.5f - 0.025f, vehicleOpeningHeight, 0.08f));
                AddBox(root.transform, "SideDoor", box, materials["PaintedWood"],
                    new Vector3(maxX + 0.07f, sideDoorHeight * 0.5f, doorCenterZ),
                    new Vector3(0.08f, sideDoorHeight, sideDoorWidth));
                AddBox(root.transform, "Window", box, materials["WindowGlass"],
                    new Vector3(minX - 0.055f, 1.45f, centerZ + 0.65f),
                    new Vector3(0.045f, 0.72f, 1.05f), addCollider: false);

                AddBox(root.transform, "FramePost_FL", box, materials["StructuralMetal"],
                    new Vector3(minX, height * 0.5f, minZ), new Vector3(0.12f, height, 0.12f));
                AddBox(root.transform, "FramePost_FR", box, materials["StructuralMetal"],
                    new Vector3(maxX, height * 0.5f, minZ), new Vector3(0.12f, height, 0.12f));
                AddBox(root.transform, "FramePost_RL", box, materials["StructuralMetal"],
                    new Vector3(minX, height * 0.5f, maxZ), new Vector3(0.12f, height, 0.12f));
                AddBox(root.transform, "FramePost_RR", box, materials["StructuralMetal"],
                    new Vector3(maxX, height * 0.5f, maxZ), new Vector3(0.12f, height, 0.12f));

                GameObject roof = PrefabUtility.InstantiatePrefab(roofPrefab) as GameObject;
                if (roof == null)
                {
                    throw new InvalidOperationException("Could not instantiate garage roof prefab.");
                }

                roof.name = "RoofAssembly";
                roof.transform.SetParent(root.transform, false);
                roof.transform.localPosition = new Vector3(
                    0f,
                    height - GaragePrototypePaths.ProductionRoofBoundsMin.y + 0.02f,
                    0f);
                AddMarker(root.transform, "GarageRootPivot", Vector3.zero);
                return SavePrefab(root, GaragePrototypePaths.GarageShellPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildInteriorPrefab(
            IReadOnlyDictionary<string, Mesh> meshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("M3_GarageInteriorAndProps");
            try
            {
                Mesh box = meshes["UnitBox"];
                float minX = GaragePrototypePaths.ProductionRoofBoundsMin.x;
                float maxX = GaragePrototypePaths.ProductionRoofBoundsMax.x;
                float maxZ = GaragePrototypePaths.ProductionRoofBoundsMax.z;
                float centerX = (minX + maxX) * 0.5f;

                var workbench = new GameObject("Workbench");
                workbench.transform.SetParent(root.transform, false);
                AddBox(workbench.transform, "Top", box, materials["WorkbenchWood"],
                    new Vector3(centerX - 0.45f, 0.9f, maxZ - 0.43f), new Vector3(2.05f, 0.12f, 0.68f));
                AddBox(workbench.transform, "Leg_L", box, materials["StructuralMetal"],
                    new Vector3(centerX - 1.25f, 0.44f, maxZ - 0.43f), new Vector3(0.09f, 0.88f, 0.58f));
                AddBox(workbench.transform, "Leg_R", box, materials["StructuralMetal"],
                    new Vector3(centerX + 0.35f, 0.44f, maxZ - 0.43f), new Vector3(0.09f, 0.88f, 0.58f));
                AddBox(workbench.transform, "Backboard", box, materials["PaintedWood"],
                    new Vector3(centerX - 0.45f, 1.38f, maxZ - 0.09f), new Vector3(2.05f, 0.78f, 0.06f));

                var shelves = new GameObject("Shelves");
                shelves.transform.SetParent(root.transform, false);
                for (int shelfIndex = 0; shelfIndex < 4; shelfIndex++)
                {
                    AddBox(shelves.transform, "Shelf_" + shelfIndex, box, materials["StructuralMetal"],
                        new Vector3(minX + 0.42f, 0.3f + shelfIndex * 0.48f, 0.35f),
                        new Vector3(0.72f, 0.055f, 1.48f));
                }
                AddBox(shelves.transform, "Upright_A", box, materials["StructuralMetal"],
                    new Vector3(minX + 0.12f, 1.0f, -0.32f), new Vector3(0.055f, 1.9f, 0.055f));
                AddBox(shelves.transform, "Upright_B", box, materials["StructuralMetal"],
                    new Vector3(minX + 0.72f, 1.0f, -0.32f), new Vector3(0.055f, 1.9f, 0.055f));
                AddBox(shelves.transform, "Upright_C", box, materials["StructuralMetal"],
                    new Vector3(minX + 0.12f, 1.0f, 1.02f), new Vector3(0.055f, 1.9f, 0.055f));
                AddBox(shelves.transform, "Upright_D", box, materials["StructuralMetal"],
                    new Vector3(minX + 0.72f, 1.0f, 1.02f), new Vector3(0.055f, 1.9f, 0.055f));

                AddBox(root.transform, "ToolCabinet", box, materials["StructuralMetal"],
                    new Vector3(maxX - 0.48f, 0.72f, maxZ - 0.46f), new Vector3(0.7f, 1.44f, 0.58f));
                GameObject drum = AddMeshObject(root.transform, "OilDrum", meshes["Cylinder"], materials["CorrugatedMetal"]);
                drum.transform.localPosition = new Vector3(maxX - 0.55f, 0.42f, -0.65f);
                drum.transform.localScale = new Vector3(0.72f, 0.84f, 0.72f);
                CapsuleCollider drumCollider = drum.AddComponent<CapsuleCollider>();
                drumCollider.radius = 0.5f;
                drumCollider.height = 1f;
                AddBox(root.transform, "FuelCan", box, materials["PaintedWood"],
                    new Vector3(maxX - 0.52f, 0.28f, 0.36f), new Vector3(0.34f, 0.56f, 0.22f));
                AddBox(root.transform, "PartsCrate", box, materials["WorkbenchWood"],
                    new Vector3(centerX + 0.75f, 0.25f, maxZ - 0.55f), new Vector3(0.62f, 0.5f, 0.48f));
                AddBox(root.transform, "WallToolRail", box, materials["StructuralMetal"],
                    new Vector3(centerX - 0.45f, 1.48f, maxZ - 0.045f), new Vector3(1.58f, 0.045f, 0.04f));
                return SavePrefab(root, GaragePrototypePaths.GarageInteriorPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildRoadPrefab(
            IReadOnlyDictionary<string, Mesh> meshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("M3_RoadSegment_180m");
            try
            {
                var lod0 = new GameObject("LOD0_RoadAndShoulders");
                lod0.transform.SetParent(root.transform, false);
                MeshRenderer shoulder = AddMeshChild(
                    lod0.transform, "Shoulder", meshes["RoadShoulder"], materials["RoadShoulder"]);
                MeshRenderer surface = AddMeshChild(
                    lod0.transform, "RoadSurface", meshes["RoadLod0"], materials["RoadGravel"]);
                MeshCollider roadCollider = surface.gameObject.AddComponent<MeshCollider>();
                roadCollider.sharedMesh = meshes["RoadLod0"];

                var lod1 = new GameObject("LOD1_RoadProxy");
                lod1.transform.SetParent(root.transform, false);
                MeshRenderer proxy = AddMeshChild(
                    lod1.transform, "RoadProxy", meshes["RoadLod1"], materials["RoadGravel"]);
                ConfigureLodGroup(root, new Renderer[] { shoulder, surface }, new Renderer[] { proxy }, 0.38f, 0.035f);
                AddMarker(root.transform, "RoadStart_0m", new Vector3(15f, 0.03f, -90f));
                AddMarker(root.transform, "RoadEnd_180m", new Vector3(15f, 0.03f, 90f));
                return SavePrefab(root, GaragePrototypePaths.RoadPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildTerrainPrefab(
            IReadOnlyDictionary<string, Mesh> meshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("M3_TerrainPatch");
            try
            {
                MeshRenderer renderer = AddMeshChild(
                    root.transform, "TerrainSurface", meshes["Terrain"], materials["SoilGrass"]);
                MeshCollider collider = renderer.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = meshes["Terrain"];
                return SavePrefab(root, GaragePrototypePaths.TerrainPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildTreePrefab(
            IReadOnlyDictionary<string, Mesh> meshes,
            IReadOnlyDictionary<string, Material> materials)
        {
            var root = new GameObject("M3_SpruceTree");
            try
            {
                var lod0Root = new GameObject("LOD0");
                lod0Root.transform.SetParent(root.transform, false);
                MeshRenderer trunk = AddMeshChild(
                    lod0Root.transform, "Trunk", meshes["TreeTrunk"], materials["Bark"]);
                trunk.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                MeshRenderer crown = AddMeshChild(
                    lod0Root.transform, "Crown", meshes["TreeCrownLod0"], materials["Foliage"]);
                crown.transform.localPosition = new Vector3(0f, 1.05f, 0f);

                var lod1Root = new GameObject("LOD1");
                lod1Root.transform.SetParent(root.transform, false);
                MeshRenderer crownProxy = AddMeshChild(
                    lod1Root.transform, "CrownProxy", meshes["TreeCrownLod1"], materials["Foliage"]);
                crownProxy.transform.localPosition = new Vector3(0f, 1.05f, 0f);
                ConfigureLodGroup(root, new Renderer[] { trunk, crown }, new Renderer[] { crownProxy }, 0.34f, 0.03f);
                CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
                collider.center = new Vector3(0f, 1.2f, 0f);
                collider.radius = 0.22f;
                collider.height = 2.4f;
                return SavePrefab(root, GaragePrototypePaths.TreePrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildVegetationPrefab(GameObject treePrefab)
        {
            var root = new GameObject("M3_VegetationCluster");
            try
            {
                for (int index = 0; index < 34; index++)
                {
                    GameObject tree = PrefabUtility.InstantiatePrefab(treePrefab) as GameObject;
                    if (tree == null)
                    {
                        throw new InvalidOperationException("Could not instantiate tree prefab.");
                    }

                    tree.name = "Spruce_" + index.ToString("00");
                    tree.transform.SetParent(root.transform, false);
                    float z = -82f + index * 5.05f;
                    bool leftSide = index % 2 == 0;
                    float x = leftSide
                        ? -12f - (index % 5) * 2.2f
                        : 30f + (index % 4) * 2.6f;
                    tree.transform.localPosition = new Vector3(x, -0.1f, z);
                    tree.transform.localRotation = Quaternion.Euler(0f, (index * 47) % 360, 0f);
                    float scale = 0.72f + (index % 7) * 0.055f;
                    tree.transform.localScale = new Vector3(scale, scale, scale);
                }

                return SavePrefab(root, GaragePrototypePaths.VegetationPrefab);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static GameObject BuildLightingPreset(
            string name,
            string prefabPath,
            string profilePath,
            float exposureValue,
            float fogMeanFreePath,
            float sunLux,
            Color sunColor,
            Vector3 sunEuler)
        {
            VolumeProfile profile = CreateOrUpdateVolumeProfile(
                profilePath,
                exposureValue,
                fogMeanFreePath);
            var root = new GameObject(name);
            try
            {
                Volume volume = root.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 10f;
                volume.sharedProfile = profile;
                var sunObject = new GameObject("Directional Sun");
                sunObject.transform.SetParent(root.transform, false);
                sunObject.transform.localRotation = Quaternion.Euler(sunEuler);
                Light sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.lightUnit = LightUnit.Lux;
                sun.intensity = sunLux;
                sun.color = sunColor;
                sun.shadows = LightShadows.Soft;
                sunObject.AddComponent<HDAdditionalLightData>();
                return SavePrefab(root, prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static VolumeProfile CreateOrUpdateVolumeProfile(
            string path,
            float fixedExposure,
            float fogMeanFreePath)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = Path.GetFileNameWithoutExtension(path);
                AssetDatabase.CreateAsset(profile, path);
            }

            VisualEnvironment environment = GetOrAdd<VisualEnvironment>(profile);
            environment.active = true;
            environment.skyType.Override((int)SkyType.PhysicallyBased);
            environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            PhysicallyBasedSky sky = GetOrAdd<PhysicallyBasedSky>(profile);
            sky.active = true;
            sky.type.Override(PhysicallyBasedSkyModel.EarthSimple);
            sky.atmosphericScattering.Override(true);
            Exposure exposure = GetOrAdd<Exposure>(profile);
            exposure.active = true;
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(fixedExposure);
            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);
            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.active = true;
            bloom.intensity.Override(0.06f);
            bloom.threshold.Override(1.15f);
            Fog fog = GetOrAdd<Fog>(profile);
            fog.active = true;
            fog.enabled.Override(true);
            fog.colorMode.Override(FogColorMode.SkyColor);
            fog.baseHeight.Override(-2f);
            fog.maximumHeight.Override(90f);
            fog.meanFreePath.Override(fogMeanFreePath);
            fog.maxFogDistance.Override(1400f);
            fog.enableVolumetricFog.Override(true);
            fog.depthExtent.Override(220f);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
            {
                return component;
            }

            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        private static void BuildScalePivotRecord(GameObject roofPrefab, Bounds roofBounds)
        {
            GarageScalePivotRecord record =
                AssetDatabase.LoadAssetAtPath<GarageScalePivotRecord>(GaragePrototypePaths.ScalePivotRecord);
            if (record == null)
            {
                record = ScriptableObject.CreateInstance<GarageScalePivotRecord>();
                record.name = "M3_GarageScalePivotRecord";
                AssetDatabase.CreateAsset(record, GaragePrototypePaths.ScalePivotRecord);
            }

            record.Configure(roofPrefab, roofBounds);
            EditorUtility.SetDirty(record);
        }

        private static void BuildProductionScene(
            GameObject garageShell,
            GameObject interior,
            GameObject road,
            GameObject terrain,
            GameObject vegetation,
            GameObject lighting)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var sceneRoot = new GameObject("M3_GarageArtPrototype");
            sceneRoot.AddComponent<GaragePrototypeSceneMarker>();
            sceneRoot.AddComponent<GaragePrototypePerformanceProbe>();

            var environment = new GameObject("Environment");
            environment.transform.SetParent(sceneRoot.transform, false);
            InstantiatePrefab(terrain, environment.transform, "Terrain");
            InstantiatePrefab(road, environment.transform, "Road_180m");
            InstantiatePrefab(vegetation, environment.transform, "Vegetation");
            InstantiatePrefab(garageShell, environment.transform, "GarageShell");
            InstantiatePrefab(interior, environment.transform, "GarageInteriorAndProps");

            var lightingRoot = new GameObject("Lighting");
            lightingRoot.transform.SetParent(sceneRoot.transform, false);
            GameObject lightingInstance = InstantiatePrefab(lighting, lightingRoot.transform, "LateDayPreset_Active");
            Light sun = lightingInstance.GetComponentInChildren<Light>(true);
            RenderSettings.sun = sun;

            var reflectionObject = new GameObject("GarageReflectionProbe");
            reflectionObject.transform.SetParent(lightingRoot.transform, false);
            reflectionObject.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            ReflectionProbe probe = reflectionObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.size = new Vector3(12f, 6f, 12f);
            probe.intensity = 0.75f;

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(sceneRoot.transform, false);
            cameraObject.transform.position = new Vector3(-11.5f, 6.8f, -13.5f);
            cameraObject.transform.rotation = Quaternion.LookRotation(
                new Vector3(0.4f, 1.25f, 0.25f) - cameraObject.transform.position,
                Vector3.up);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 56f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 500f;
            cameraObject.AddComponent<HDAdditionalCameraData>();
            cameraObject.AddComponent<AudioListener>();

            EditorSceneManager.MarkSceneDirty(scene);
            EnsureAssetDirectory(GaragePrototypePaths.ProductionScene);
            if (!EditorSceneManager.SaveScene(scene, GaragePrototypePaths.ProductionScene))
            {
                throw new InvalidOperationException("Could not save Milestone 3 production scene.");
            }
        }

        private static void BuildComparisonScene(
            GameObject productionRoof,
            GameObject lighting,
            Material referenceMaterial)
        {
            GameObject referenceRoof =
                AssetDatabase.LoadAssetAtPath<GameObject>(GaragePrototypePaths.DonorRoofReference);
            if (referenceRoof == null)
            {
                throw new InvalidOperationException(
                    "The reviewed Milestone 2 garage roof reference is required for the comparison scene.");
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("M3_GarageScalePivotComparison_REFERENCE_ONLY");
            GameObject reference = PrefabUtility.InstantiatePrefab(referenceRoof) as GameObject;
            if (reference == null)
            {
                throw new InvalidOperationException("Could not instantiate donor roof reference.");
            }

            reference.name = "REFERENCE__garage_shed_roof_native_axes";
            reference.transform.SetParent(root.transform, false);
            reference.transform.localPosition = new Vector3(-4f, 1.5f, 0f);
            foreach (Renderer renderer in reference.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = referenceMaterial;
            }

            GameObject production = InstantiatePrefab(
                productionRoof,
                root.transform,
                "PRODUCTION__M3_GarageRoof_y_up");
            production.transform.localPosition = new Vector3(4f, 0f, 0f);
            AddMarker(reference.transform, "REFERENCE_PIVOT_0_0_0", Vector3.zero);
            AddMarker(production.transform, "PRODUCTION_PIVOT_0_0_0", Vector3.zero);

            GameObject lightingInstance = InstantiatePrefab(lighting, root.transform, "NeutralLighting");
            RenderSettings.sun = lightingInstance.GetComponentInChildren<Light>(true);
            var cameraObject = new GameObject("Comparison Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 7f, -14f);
            cameraObject.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 1.2f, 0f) - cameraObject.transform.position);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 52f;
            cameraObject.AddComponent<HDAdditionalCameraData>();
            cameraObject.AddComponent<AudioListener>();

            EditorSceneManager.MarkSceneDirty(scene);
            EnsureAssetDirectory(GaragePrototypePaths.ComparisonScene);
            if (!EditorSceneManager.SaveScene(scene, GaragePrototypePaths.ComparisonScene))
            {
                throw new InvalidOperationException("Could not save Milestone 3 comparison scene.");
            }
        }

        private static void EnsureProductionSceneInBuildSettings()
        {
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes;
            var updated = new List<EditorBuildSettingsScene>(existing.Length + 1);
            bool inserted = false;
            foreach (EditorBuildSettingsScene entry in existing)
            {
                if (string.Equals(entry.path, GaragePrototypePaths.ProductionScene, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                updated.Add(entry);
                if (!inserted && string.Equals(
                        entry.path,
                        Foundation.FoundationSceneBuilder.BootstrapScenePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    updated.Add(new EditorBuildSettingsScene(GaragePrototypePaths.ProductionScene, true));
                    inserted = true;
                }
            }

            if (!inserted)
            {
                updated.Add(new EditorBuildSettingsScene(GaragePrototypePaths.ProductionScene, true));
            }

            EditorBuildSettings.scenes = updated.ToArray();
        }

        private static void UpdateLedger()
        {
            const string donorPath = "mysummercar_Data/sharedassets3.assets";
            const string donorHash = "1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684";
            var entries = new[]
            {
                new PortingLedgerEntry(
                    donorPath,
                    "garage_shed_roof bounds/pivot -> M3 garage roof",
                    donorHash,
                    "Prefab/Mesh",
                    "ReauthoredGeometry",
                    GaragePrototypePaths.GarageRoofPrefab,
                    "PrototypeReady",
                    GaragePrototypePaths.DonorRoofReference + "; " + GaragePrototypePaths.ScalePivotRecord,
                    "MSC GaragePrototypeAssetBuilder " + GaragePrototypePaths.BuilderVersion,
                    "Coordinate axes mapped donor (X,Y,Z) to production (X,Z,Y); geometry fully rebuilt.",
                    "Bounds and zero pivot match the reviewed donor roof within 0.005 m."),
                new PortingLedgerEntry(
                    donorPath,
                    "garage shed layout -> M3 production garage shell",
                    donorHash,
                    "Prefab",
                    "ReauthoredGeometry",
                    GaragePrototypePaths.GarageShellPrefab,
                    "PrototypeReady",
                    GaragePrototypePaths.GarageRoofPrefab,
                    "MSC GaragePrototypeAssetBuilder " + GaragePrototypePaths.BuilderVersion,
                    "Only roof extents are donor-validated; wall openings and modular construction are reconstructed.",
                    "Production shell contains rebuilt doors, window, structure, collision and roof LODs."),
                new PortingLedgerEntry(
                    "project-authored",
                    "Milestone 3 reusable HDRP material library",
                    string.Empty,
                    "MaterialLibrary",
                    "ReauthoredMaterial",
                    GaragePrototypePaths.MaterialRoot,
                    "PrototypeReady",
                    GaragePrototypePaths.TextureRoot,
                    "MSC GaragePrototypeAssetBuilder " + GaragePrototypePaths.BuilderVersion,
                    "Procedural 128 px prototype textures require later authored high-resolution replacements.",
                    "HDRP mask packing: R metallic, G ambient occlusion, B detail mask, A smoothness."),
                new PortingLedgerEntry(
                    "project-authored",
                    "Milestone 3 PBR texture sets",
                    string.Empty,
                    "TextureSet",
                    "ReauthoredTexture",
                    GaragePrototypePaths.TextureRoot,
                    "PrototypeReady",
                    "none",
                    "MSC GaragePrototypeAssetBuilder " + GaragePrototypePaths.BuilderVersion,
                    "Procedural prototype detail; no donor pixels used.",
                    "Each material has BaseColor, tangent-space Normal and HDRP Mask maps."),
                new PortingLedgerEntry(
                    "donor world relationship (behavioral reference; route geometry not extracted)",
                    "nearby rural road segment",
                    string.Empty,
                    "Prefab/Mesh",
                    "Reimplemented",
                    GaragePrototypePaths.RoadPrefab,
                    "PrototypeReady",
                    GaragePrototypePaths.TerrainPrefab,
                    "MSC GaragePrototypeAssetBuilder " + GaragePrototypePaths.BuilderVersion,
                    "180 m prototype curve is not claimed as donor-exact centerline or elevation.",
                    "Layered gravel surface and shoulder use rebuilt meshes, collision and two LODs."),
                new PortingLedgerEntry(
                    donorPath,
                    "Milestone 3 garage comparison scene",
                    donorHash,
                    "Scene",
                    "ReferenceOnly",
                    GaragePrototypePaths.ComparisonScene,
                    "ComparisonReady",
                    GaragePrototypePaths.DonorRoofReference + "; " + GaragePrototypePaths.GarageRoofPrefab,
                    "MSC GaragePrototypeAssetBuilder " + GaragePrototypePaths.BuilderVersion,
                    "Ignored reference scene; never included in player builds.",
                    "Contains donor roof reference and rebuilt production roof side by side."),
                new PortingLedgerEntry(
                    "project-authored with reviewed donor dimensional anchor",
                    "Milestone 3 garage art prototype scene",
                    donorHash,
                    "Scene",
                    "Reimplemented",
                    GaragePrototypePaths.ProductionScene,
                    "PrototypeReady",
                    GaragePrototypePaths.GarageShellPrefab + "; " + GaragePrototypePaths.RoadPrefab,
                    "MSC GaragePrototypeAssetBuilder " + GaragePrototypePaths.BuilderVersion,
                    "Static art prototype only; no player, vehicle, weather simulation or world streaming.",
                    "Production scene has no ReferenceOnly or DonorGenerated dependency.")
            };

            string existing = File.Exists(LedgerPath) ? File.ReadAllText(LedgerPath) : string.Empty;
            PortingLedgerUpdatePlan plan = PortingLedgerUpdater.CreatePlan(existing, entries);
            PortingLedgerUpdater.ExecuteFile(LedgerPath, plan);
            Debug.Log($"M3_LEDGER_UPDATE added={plan.AddedCount} updated={plan.UpdatedCount}");
        }

        private static Mesh CreateOrUpdateMesh(string path, string name, Action<Mesh> build)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }

            mesh.name = name;
            build(mesh);
            return mesh;
        }

        private static MeshRenderer AddMeshChild(
            Transform parent,
            string name,
            Mesh mesh,
            Material material)
        {
            GameObject child = AddMeshObject(parent, name, mesh, material);
            return child.GetComponent<MeshRenderer>();
        }

        private static GameObject AddMeshObject(
            Transform parent,
            string name,
            Mesh mesh,
            Material material)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            MeshFilter filter = child.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return child;
        }

        private static GameObject AddBox(
            Transform parent,
            string name,
            Mesh boxMesh,
            Material material,
            Vector3 localPosition,
            Vector3 localScale,
            bool addCollider = true)
        {
            GameObject child = AddMeshObject(parent, name, boxMesh, material);
            child.transform.localPosition = localPosition;
            child.transform.localScale = localScale;
            if (addCollider)
            {
                child.AddComponent<BoxCollider>();
            }

            return child;
        }

        private static void AddMarker(Transform parent, string name, Vector3 localPosition)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
        }

        private static void ConfigureLodGroup(
            GameObject root,
            Renderer[] lod0,
            Renderer[] lod1,
            float lod0Height,
            float lod1Height)
        {
            LODGroup group = root.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = false;
            var firstLod = new LOD(lod0Height, lod0)
            {
                fadeTransitionWidth = 0.08f
            };
            var secondLod = new LOD(lod1Height, lod1)
            {
                fadeTransitionWidth = 0.08f
            };
            group.SetLODs(new[]
            {
                firstLod,
                secondLod
            });
            group.RecalculateBounds();
        }

        private static GameObject InstantiatePrefab(
            GameObject prefab,
            Transform parent,
            string instanceName)
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException("Could not instantiate prefab: " + prefab.name);
            }

            instance.name = instanceName;
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static GameObject SavePrefab(GameObject root, string path)
        {
            EnsureAssetDirectory(path);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            if (prefab == null)
            {
                throw new InvalidOperationException("Could not save prefab: " + path);
            }

            return prefab;
        }

        private static void WriteTexture(string path, MaterialSpec spec, TextureKind kind)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, kind != TextureKind.BaseColor);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float height = PatternHeight(x, y, spec.Seed, spec.Pattern);
                    if (kind == TextureKind.BaseColor)
                    {
                        float shade = Mathf.Lerp(0.72f, 1.18f, height);
                        pixels[y * size + x] = new Color(
                            Mathf.Clamp01(spec.BaseColor.r * shade),
                            Mathf.Clamp01(spec.BaseColor.g * shade),
                            Mathf.Clamp01(spec.BaseColor.b * shade),
                            1f);
                    }
                    else if (kind == TextureKind.Normal)
                    {
                        float dx = PatternHeight(x + 1, y, spec.Seed, spec.Pattern) -
                                   PatternHeight(x - 1, y, spec.Seed, spec.Pattern);
                        float dy = PatternHeight(x, y + 1, spec.Seed, spec.Pattern) -
                                   PatternHeight(x, y - 1, spec.Seed, spec.Pattern);
                        Vector3 normal = new Vector3(-dx * 2.1f, -dy * 2.1f, 1f).normalized;
                        pixels[y * size + x] = new Color(
                            normal.x * 0.5f + 0.5f,
                            normal.y * 0.5f + 0.5f,
                            normal.z * 0.5f + 0.5f,
                            1f);
                    }
                    else
                    {
                        float ao = Mathf.Lerp(0.78f, 1f, height);
                        float smoothness = Mathf.Clamp01(spec.Smoothness + (height - 0.5f) * 0.08f);
                        pixels[y * size + x] = new Color(spec.Metallic, ao, 1f, smoothness);
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(Path.GetFullPath(path), texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Texture importer is unavailable: " + path);
            }

            importer.textureType = kind == TextureKind.Normal
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            importer.sRGBTexture = kind == TextureKind.BaseColor;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.SaveAndReimport();
        }

        private static float PatternHeight(int x, int y, int seed, SurfacePattern pattern)
        {
            int wrappedX = (x % 128 + 128) % 128;
            int wrappedY = (y % 128 + 128) % 128;
            float noise = Hash01(wrappedX, wrappedY, seed);
            switch (pattern)
            {
                case SurfacePattern.Boards:
                    return Mathf.Clamp01(noise * 0.45f + (wrappedX % 32 < 2 ? 0.08f : 0.56f));
                case SurfacePattern.Corrugated:
                    return Mathf.Clamp01(0.48f + Mathf.Sin(wrappedX * Mathf.PI / 8f) * 0.24f + noise * 0.12f);
                case SurfacePattern.Wood:
                    return Mathf.Clamp01(0.42f + Mathf.Sin((wrappedX + noise * 12f) * 0.18f) * 0.15f + noise * 0.24f);
                case SurfacePattern.Gravel:
                    return Mathf.Clamp01(noise * 0.8f + Hash01(wrappedX / 3, wrappedY / 3, seed + 17) * 0.2f);
                case SurfacePattern.Ground:
                    return Mathf.Clamp01(noise * 0.38f + Hash01(wrappedX / 8, wrappedY / 8, seed + 29) * 0.62f);
                case SurfacePattern.Needles:
                    return Mathf.Clamp01(noise * 0.48f + ((wrappedX + wrappedY * 3) % 13 < 3 ? 0.28f : 0.12f));
                case SurfacePattern.Bark:
                    return Mathf.Clamp01(noise * 0.36f + Mathf.Abs(Mathf.Sin(wrappedX * 0.22f + noise)) * 0.58f);
                default:
                    return Mathf.Clamp01(0.34f + noise * 0.54f);
            }
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint hash = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                hash = (hash ^ (hash >> 13)) * 1274126177u;
                return (hash & 0x00ffffffu) / 16777215f;
            }
        }

        private static void EnsureAssetDirectory(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            string[] parts = directory.Split('/');
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

        private readonly struct MaterialSpec
        {
            internal MaterialSpec(
                string id,
                Color baseColor,
                float metallic,
                float smoothness,
                int seed,
                SurfacePattern pattern)
            {
                Id = id;
                BaseColor = baseColor;
                Metallic = metallic;
                Smoothness = smoothness;
                Seed = seed;
                Pattern = pattern;
            }

            internal string Id { get; }
            internal Color BaseColor { get; }
            internal float Metallic { get; }
            internal float Smoothness { get; }
            internal int Seed { get; }
            internal SurfacePattern Pattern { get; }
        }

        private enum SurfacePattern
        {
            Grain,
            Boards,
            Corrugated,
            Wood,
            Gravel,
            Ground,
            Needles,
            Bark
        }

        private enum TextureKind
        {
            BaseColor,
            Normal,
            Mask
        }
    }
}
