using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Editor.Vegetation;
using MSC.LegacyImport;
using MSC.World.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldBaseline
{
    public static class Phase1ForestRemediationBuilder
    {
        private const string RootName =
            "PHASE1_VEGETATION_REPLACEMENT_LICENSED_THIRD_PARTY";
        private const string PreviousRootName =
            "PHASE1_FOREST_REMEDIATION_LICENSED_THIRD_PARTY";
        private const string BetterMscRootName =
            "BETTERMSC_MAP_REMEDIATION_TEMPORARY_DIRECT_IMPORT";
        private const string ContentRootName =
            "TEMPORARY_DIRECT_IMPORT_ENTITIES";
        private const string GlobalSceneRootName =
            "World_Global_Legacy";
        private const string MapAnchorPath =
            "MAP/MESH/TERRAIN_OBJ/Grass1";
        private const string ApprovedMapBoundaryColliderPath =
            "MAP/MESH/FOLIAGE/TREEWALL_LOW/treewallcoll";
        private const int MaximumSourceTreeCount = 26000;
        private const int MaximumOrdinaryTreeCount = 80000;
        private const int MaximumBoundaryTreeCount = 15000;
        private const int MaximumRoadsideTreeCount = 12000;
        private const int MaximumTreeCount = 94000;
        private const int MaximumShrubCount = 6500;
        private const int MaximumGrassClusterCount = 420000;
        private const float TreeAnchorGridMeters = 4f;
        private const float ShrubAnchorGridMeters = 6f;
        private const float GrassAnchorGridMeters = 2.1f;
        private const float BoundaryGridMeters = 5.4f;
        private const float RoadsideSampleSpacingMeters = 8f;
        private const float TreeSurfaceClearanceMeters = 3.25f;
        internal const float TreeHeightScale = 0.72f;
        internal const int SpruceDistributionPercent = 65;
        internal const int PineDistributionPercent = 20;
        internal const float BirchDistributionPercent = 7.5f;
        internal const float AspenDistributionPercent = 7.5f;
        private const float SparseVoidGridMeters = 40f;
        private const float SparseVoidMinimumTreeDistanceMeters = 24f;
        private const int MaximumSparseVoidTreeCount = 1800;
        private const uint SparseVoidKeepThreshold = 19000u;
        private const float CellSizeMeters = 512f;
        private const float RuntimeForestVisibleDistanceMeters = 620f;
        private const float RuntimeForestHysteresisMeters = 160f;
        private const string GrassFieldMeshRoot =
            "Assets/Game/Presentation/Vegetation/Generated/Phase1/" +
            "Meshes/GrassFields";
        private const string GrassPatchMeshPath =
            GrassFieldMeshRoot + "/Phase1_Low_Grass_Patch.asset";
        private const string GrassFieldDataRoot =
            "Assets/Game/Presentation/Vegetation/Generated/Phase1/" +
            "Data/GrassFields";
        private const string GrassFieldMaterialPath =
            "Assets/Game/Presentation/Vegetation/Generated/Phase1/" +
            "Materials/GroundCover/Phase1_Grass_Field_HDRP.mat";
        private const string GrassGroundMaterialPath =
            "Assets/Game/Presentation/Vegetation/Generated/Phase1/" +
            "Materials/GroundCover/Phase1_Grass_Ground_HDRP.mat";
        private const string GrassFieldTexturePath =
            "Assets/Game/Presentation/Vegetation/Generated/Phase1/" +
            "Textures/GroundCover/Phase1_Grass_Clump.asset";

        internal static readonly string[] SprucePrefabs =
            MapVegetationAlpSpruceBindings.OutputPrefabPaths;

        private static readonly string[] OtherTreePrefabs =
        {
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Aspen_01.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Aspen_02.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Aspen_03.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Aspen_04.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Aspen_05.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Birch_01.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Birch_02.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Birch_03.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Birch_04.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Pine_01.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Pine_02.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Pine_03.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Pine_04.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Tree/Pine_05.prefab"
        };

        private static readonly string[] ShrubPrefabs =
        {
            "Assets/Chernobyl/Prefabs/Vegetation/Bushes_01.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Bushes_02.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Bushes_03.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Fern_01.prefab",
            "Assets/Chernobyl/Prefabs/Vegetation/Fern_02.prefab",
            "Assets/NatureManufacture Assets/" +
            "Forest Environment Dynamic Nature/Foliage and Grass/" +
            "Prefabs/prefab_fern_01_1.prefab",
            "Assets/NatureManufacture Assets/" +
            "Forest Environment Dynamic Nature/Foliage and Grass/" +
            "Prefabs/prefab_fern_01_2.prefab",
            "Assets/NatureManufacture Assets/" +
            "Meadow Environment Dynamic Nature/Bushes/Prefabs/" +
            "prefab_grey_willow_01.prefab",
            "Assets/NatureManufacture Assets/" +
            "Meadow Environment Dynamic Nature/Bushes/Prefabs/" +
            "prefab_grey_willow_02.prefab"
        };

        [MenuItem(
            "Tools/MSC Remake/World Baseline/" +
            "Apply Phase 1 Vegetation Replacement")]
        public static void ApplyFromMenu()
        {
            Apply();
        }

        public static void RunBatch()
        {
            Apply();
        }

        public static void AuditPlacementExclusionsBatch()
        {
            Scene scene = EditorSceneManager.OpenScene(
                WorldBaseline06B2Paths.GlobalScene,
                OpenSceneMode.Single);
            GameObject contentRoot = FindContentRoot(scene);
            DonorWorldBaselineEntityMetadata[] metadata =
                contentRoot.GetComponentsInChildren<
                    DonorWorldBaselineEntityMetadata>(true);
            Phase1TreePlacementExclusionIndex exclusionIndex =
                Phase1TreePlacementExclusionIndex.Build(metadata);
            int indexedCellSceneCount =
                AddCellScenePlacementExclusions(exclusionIndex);
            Debug.Log(
                "PHASE1_TREE_EXCLUSION_AUDIT_OK " +
                $"footprints={exclusionIndex.FootprintCount} " +
                $"roadTriangles={exclusionIndex.RoadTriangleCount} " +
                $"buildingTriangles={exclusionIndex.BuildingTriangleCount} " +
                $"buildingBounds={exclusionIndex.BuildingBoundsCount} " +
                $"buildingAreaSum=" +
                $"{exclusionIndex.BuildingProjectedAreaSum:0.###} " +
                $"largestBuildingArea=" +
                $"{exclusionIndex.LargestBuildingBoundsArea:0.###} " +
                $"largestBuildingSpan=" +
                $"{exclusionIndex.LargestBuildingBoundsSpan:0.###} " +
                $"largestBuildingSource='" +
                $"{exclusionIndex.LargestBuildingBoundsSource}' " +
                $"outsideSourceCell=" +
                $"{exclusionIndex.BuildingBoundsOutsideSourceCell} " +
                $"firstOutsideSourceCell='" +
                $"{exclusionIndex.FirstOutsideSourceCellSource}' " +
                $"supplementalEntities=" +
                $"{exclusionIndex.SupplementalEntityCount} " +
                $"supplementalTriangles=" +
                $"{exclusionIndex.SupplementalTriangleCount} " +
                $"supplementalBounds=" +
                $"{exclusionIndex.SupplementalBoundsCount} " +
                $"indexedCellScenes={indexedCellSceneCount}");
        }

        public static void Apply()
        {
            Phase1VegetationAssetBuilder.Build();
            BetterMscMapRemediationImporter.Apply();
            MapVegetationAlpSpruceBindings.BuildPrefabs();

            GameObject[] spruces = LoadPrefabs(SprucePrefabs);
            GameObject[] otherTrees = LoadPrefabs(OtherTreePrefabs);
            GameObject[] pines = otherTrees.Where(prefab =>
                prefab.name.StartsWith("Pine_", StringComparison.Ordinal)).ToArray();
            GameObject[] birches = otherTrees.Where(prefab =>
                prefab.name.StartsWith("Birch_", StringComparison.Ordinal)).ToArray();
            GameObject[] aspens = otherTrees.Where(prefab =>
                prefab.name.StartsWith("Aspen_", StringComparison.Ordinal)).ToArray();
            GameObject[] shrubs = LoadPrefabs(ShrubPrefabs);
            Material grassFieldMaterial =
                CreateOrUpdateGrassFieldMaterial();

            Scene scene = EditorSceneManager.OpenScene(
                WorldBaseline06B2Paths.GlobalScene,
                OpenSceneMode.Single);
            GameObject contentRoot = FindContentRoot(scene);
            RemoveExistingRoot(contentRoot);
            Transform betterMscRoot =
                contentRoot.transform.Find(BetterMscRootName);
            if (betterMscRoot == null)
            {
                throw new InvalidDataException(
                    "BetterMSC remediation root is missing.");
            }

            DonorWorldBaselineEntityMetadata[] metadata =
                contentRoot.GetComponentsInChildren<
                    DonorWorldBaselineEntityMetadata>(true);
            Phase1TreePlacementExclusionIndex exclusionIndex =
                Phase1TreePlacementExclusionIndex.Build(metadata);
            int indexedCellSceneCount =
                AddCellScenePlacementExclusions(exclusionIndex);
            ApplyGrassGroundMaterial(metadata);
            Vector3 mapCenter = FindMapCenter(metadata);

            List<Anchor> ordinaryTreeAnchors = CollectLegacyAnchors(
                metadata,
                VegetationSource.Tree,
                TreeAnchorGridMeters);
            ordinaryTreeAnchors.AddRange(
                CollectBetterMscForestAnchors(
                    betterMscRoot,
                    TreeAnchorGridMeters));
            ordinaryTreeAnchors = DeduplicateAndLimit(
                ordinaryTreeAnchors,
                TreeAnchorGridMeters,
                MaximumSourceTreeCount);
            List<Anchor> localForestClusters =
                BuildLocalForestClusters(ordinaryTreeAnchors);
            ordinaryTreeAnchors.AddRange(localForestClusters);
            List<Anchor> boundaryAnchors = CollectLegacyAnchors(
                metadata,
                VegetationSource.Boundary,
                BoundaryGridMeters);
            List<Anchor> shrubAnchors = CollectLegacyAnchors(
                metadata,
                VegetationSource.Shrub,
                ShrubAnchorGridMeters);
            List<Anchor> grassAnchors = CollectTerrainAnchors(
                metadata,
                GrassAnchorGridMeters,
                MaximumGrassClusterCount);

            List<Anchor> outwardForest =
                BuildOutwardForestBands(boundaryAnchors, mapCenter);
            List<Anchor> roadsideForest =
                BuildRoadsideForestBands(metadata);
            ordinaryTreeAnchors = DeduplicateAndLimit(
                ordinaryTreeAnchors,
                TreeAnchorGridMeters,
                MaximumOrdinaryTreeCount);
            outwardForest = DeduplicateAndLimit(
                outwardForest,
                TreeAnchorGridMeters,
                MaximumBoundaryTreeCount);
            roadsideForest = DeduplicateAndLimit(
                roadsideForest,
                TreeAnchorGridMeters,
                MaximumRoadsideTreeCount);
            shrubAnchors = DeduplicateAndLimit(
                shrubAnchors,
                ShrubAnchorGridMeters,
                MaximumShrubCount);
            grassAnchors = DeduplicateAndLimit(
                grassAnchors,
                GrassAnchorGridMeters,
                MaximumGrassClusterCount);

            var allTrees = new List<Anchor>(
                ordinaryTreeAnchors.Count +
                outwardForest.Count +
                roadsideForest.Count);
            allTrees.AddRange(ordinaryTreeAnchors);
            allTrees.AddRange(outwardForest);
            allTrees.AddRange(roadsideForest);
            List<Anchor> sparseVoidFill =
                BuildSparseVoidFillAnchors(
                    grassAnchors,
                    allTrees,
                    exclusionIndex);
            allTrees.AddRange(sparseVoidFill);
            allTrees = DeduplicateAndLimit(
                allTrees,
                3.8f,
                MaximumTreeCount);
            if (allTrees.Count < 500)
            {
                throw new InvalidDataException(
                    "Vegetation source geometry produced too few tree " +
                    $"anchors: {allTrees.Count}.");
            }

            GameObject root = CreateRemediationRoot(
                scene,
                contentRoot);
            int spruceCount = 0;
            int otherTreeCount = 0;
            int aspenCount = 0;
            int birchCount = 0;
            int pineCount = 0;
            int boundaryCount = 0;
            int gameplayTrunkColliderCount = 0;
            int placedTreeCount = 0;
            int groundRejectedTreeCount = 0;
            int exclusionRejectedTreeCount = 0;
            int surfaceRejectedTreeCount = 0;
            var exclusionRejectionsBySource =
                new Dictionary<string, int>(StringComparer.Ordinal);
            var cellRoots = new Dictionary<GridKey, Transform>();
            var prefabHeightCache =
                new Dictionary<GameObject, float>();
            for (int index = 0; index < allTrees.Count; index++)
            {
                Anchor anchor = allTrees[index];
                uint random = Hash(
                    anchor.Position,
                    (uint)(index + 0x51f2));
                GameObject prefab;
                int roll = (int)(random % 1000);
                if (roll < SpruceDistributionPercent * 10)
                {
                    prefab = spruces[
                        (int)((random >> 8) %
                              (uint)spruces.Length)];
                }
                else if (roll < (SpruceDistributionPercent + PineDistributionPercent) * 10)
                {
                    prefab = pines[(int)((random >> 8) % (uint)pines.Length)];
                }
                else if (roll < 925)
                {
                    prefab = birches[(int)((random >> 8) % (uint)birches.Length)];
                }
                else
                {
                    prefab = aspens[(int)((random >> 8) % (uint)aspens.Length)];
                }

                Transform parent = GetOrCreateCellRoot(
                    root.transform,
                    anchor.Position,
                    cellRoots);
                parent = GetOrCreateCategoryRoot(
                    parent,
                    "Trees");
                if (!PlaceTree(
                    prefab,
                    anchor,
                     index,
                     parent,
                     prefabHeightCache,
                     exclusionIndex,
                     out TreePlacementRejectionReason rejectionReason,
                     out string exclusionSource))
                {
                    switch (rejectionReason)
                    {
                        case TreePlacementRejectionReason.Ground:
                            groundRejectedTreeCount++;
                            break;
                        case TreePlacementRejectionReason.Exclusion:
                            exclusionRejectedTreeCount++;
                            if (!exclusionRejectionsBySource.TryGetValue(
                                    exclusionSource,
                                    out int sourceCount))
                            {
                                sourceCount = 0;
                            }
                            exclusionRejectionsBySource[exclusionSource] =
                                sourceCount + 1;
                            break;
                        case TreePlacementRejectionReason.Surface:
                            surfaceRejectedTreeCount++;
                            break;
                    }
                    continue;
                }

                placedTreeCount++;
                if (roll < SpruceDistributionPercent * 10)
                {
                    spruceCount++;
                }
                else
                {
                    otherTreeCount++;
                    if (prefab.name.StartsWith(
                            "Aspen_",
                            StringComparison.Ordinal))
                    {
                        aspenCount++;
                    }
                    else if (prefab.name.StartsWith(
                                 "Birch_",
                                 StringComparison.Ordinal))
                    {
                        birchCount++;
                    }
                    else if (prefab.name.StartsWith(
                                 "Pine_",
                                 StringComparison.Ordinal))
                    {
                        pineCount++;
                    }
                }
                if (anchor.IsBoundary)
                {
                    boundaryCount++;
                }
                gameplayTrunkColliderCount++;
            }

            int shrubCount = PlaceDecorativeInstances(
                shrubAnchors,
                shrubs,
                root.transform,
                cellRoots,
                prefabHeightCache,
                "Shrub",
                "Shrubs",
                MaximumShrubCount,
                3.5f,
                0.65f,
                2.4f,
                0x0f34u,
                true);
            GrassFieldBuildResult grassFields = BuildGrassFields(
                grassAnchors,
                root.transform,
                cellRoots,
                grassFieldMaterial);
            ConfigureRuntimeCellCulling(root, cellRoots);

            int disabledLegacyVegetationColliderCount =
                DisableReplacedLegacyPresentation(
                    metadata,
                    betterMscRoot,
                    root.transform,
                    out int preservedMapBoundaryColliderCount);
            Validate(
                root,
                metadata,
                placedTreeCount,
                shrubCount,
                grassFields.FieldCount,
                grassFields.ClusterCount,
                gameplayTrunkColliderCount,
                disabledLegacyVegetationColliderCount,
                preservedMapBoundaryColliderCount,
                exclusionIndex);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(
                    scene,
                    WorldBaseline06B2Paths.GlobalScene))
            {
                throw new IOException(
                    "Failed to save Phase 1 forest remediation.");
            }

            AssetDatabase.SaveAssets();
            string topExclusionSources = string.Join(
                ";",
                exclusionRejectionsBySource
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .Take(8)
                    .Select(pair => pair.Value + ":" + pair.Key));
            Debug.Log(
                "PHASE1_VEGETATION_REPLACEMENT_APPLY_OK " +
                $"trees={placedTreeCount} spruce={spruceCount} " +
                $"otherTrees={otherTreeCount} " +
                $"aspen={aspenCount} birch={birchCount} " +
                $"pine={pineCount} " +
                $"boundaryTrees={boundaryCount} " +
                $"treeCandidates={allTrees.Count} " +
                $"groundRejected={groundRejectedTreeCount} " +
                $"exclusionRejected={exclusionRejectedTreeCount} " +
                $"surfaceRejected={surfaceRejectedTreeCount} " +
                $"topExclusions='{topExclusionSources}' " +
                $"roadsideCandidates={roadsideForest.Count} " +
                $"sparseVoidCandidates={sparseVoidFill.Count} " +
                $"trunkColliders={gameplayTrunkColliderCount} " +
                $"exclusionFootprints={exclusionIndex.FootprintCount} " +
                $"roadTriangles={exclusionIndex.RoadTriangleCount} " +
                $"buildingTriangles={exclusionIndex.BuildingTriangleCount} " +
                $"buildingBounds={exclusionIndex.BuildingBoundsCount} " +
                $"oversizedBuildingBoundsSkipped=" +
                $"{exclusionIndex.OversizedBuildingBoundsSkipped} " +
                $"indexedCellScenes={indexedCellSceneCount} " +
                $"shrubs={shrubCount} " +
                $"grassClusters={grassFields.ClusterCount} " +
                $"grassFields={grassFields.FieldCount} " +
                $"cells={cellRoots.Count} " +
                "spriteTreeWallRenderers=disabled " +
                $"legacyInvisibleInteriorColliders=" +
                $"disabled({disabledLegacyVegetationColliderCount}) " +
                $"mapBoundaryColliders=" +
                $"preserved({preservedMapBoundaryColliderCount})");
        }

        private static GameObject[] LoadPrefabs(string[] paths)
        {
            var result = new GameObject[paths.Length];
            for (int index = 0; index < paths.Length; index++)
            {
                result[index] =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        paths[index]);
                if (result[index] == null)
                {
                    throw new FileNotFoundException(
                        "Vegetation prefab is unavailable.",
                        paths[index]);
                }
                if (result[index]
                        .GetComponentInChildren<LODGroup>(true) ==
                    null)
                {
                    throw new InvalidDataException(
                        "Vegetation prefab has no LODGroup: " +
                        paths[index]);
                }

                Renderer[] renderers = result[index]
                    .GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    throw new InvalidDataException(
                        "Vegetation prefab has no renderer: " +
                        paths[index]);
                }
                foreach (Renderer renderer in renderers)
                {
                    foreach (Material material in
                             renderer.sharedMaterials)
                    {
                        if (material == null ||
                            material.shader == null ||
                            !material.shader.isSupported ||
                            string.Equals(
                                material.shader.name,
                                "Hidden/InternalErrorShader",
                                StringComparison.Ordinal))
                        {
                            throw new InvalidDataException(
                                "Vegetation prefab has an invalid material " +
                                "or unsupported shader: " +
                                paths[index]);
                        }
                        if (!material.enableInstancing)
                        {
                            material.enableInstancing = true;
                            EditorUtility.SetDirty(material);
                        }
                    }
                }
            }
            return result;
        }

        private static GameObject FindContentRoot(Scene scene)
        {
            GameObject sceneRoot = scene
                .GetRootGameObjects()
                .SingleOrDefault(
                    root => string.Equals(
                        root.name,
                        GlobalSceneRootName,
                        StringComparison.Ordinal));
            if (sceneRoot == null)
            {
                throw new InvalidDataException(
                    "World_Global_Legacy root is missing.");
            }
            Transform content = sceneRoot.transform.Find(
                ContentRootName);
            if (content == null)
            {
                throw new InvalidDataException(
                    "Global donor content root is missing.");
            }
            return content.gameObject;
        }

        private static int AddCellScenePlacementExclusions(
            Phase1TreePlacementExclusionIndex exclusionIndex)
        {
            string[] scenePaths = AssetDatabase.FindAssets(
                    "t:Scene",
                    new[]
                    {
                        WorldBaseline06B2Paths.StreamingCellSceneRoot
                    })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(
                    path =>
                        Path.GetFileName(path).StartsWith(
                            "World_Cell_",
                            StringComparison.Ordinal) &&
                        path.EndsWith(
                            "_Legacy.unity",
                            StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (scenePaths.Length == 0)
            {
                throw new InvalidDataException(
                    "No generated legacy cell scenes were found for " +
                    "tree placement exclusion indexing.");
            }

            for (int index = 0; index < scenePaths.Length; index++)
            {
                Scene cellScene = EditorSceneManager.OpenScene(
                    scenePaths[index],
                    OpenSceneMode.Additive);
                try
                {
                    DonorWorldBaselineEntityMetadata[] cellMetadata =
                        cellScene.GetRootGameObjects()
                            .SelectMany(
                                root => root.GetComponentsInChildren<
                                    DonorWorldBaselineEntityMetadata>(true))
                            .ToArray();
                    exclusionIndex.Add(cellMetadata);
                    DonorWorldSupplementalEntityMetadata[]
                        supplementalMetadata =
                            cellScene.GetRootGameObjects()
                                .SelectMany(
                                    root => root.GetComponentsInChildren<
                                        DonorWorldSupplementalEntityMetadata>(
                                            true))
                                .ToArray();
                    exclusionIndex.Add(supplementalMetadata);
                }
                finally
                {
                    if (!EditorSceneManager.CloseScene(
                            cellScene,
                            true))
                    {
                        throw new IOException(
                            "Failed to close indexed legacy cell scene: " +
                            scenePaths[index]);
                    }
                }
            }
            Physics.SyncTransforms();
            return scenePaths.Length;
        }

        private static void RemoveExistingRoot(GameObject contentRoot)
        {
            string[] removableRoots =
            {
                RootName,
                PreviousRootName
            };
            foreach (string rootName in removableRoots)
            {
                Transform existing =
                    contentRoot.transform.Find(rootName);
                if (existing != null)
                {
                    UnityEngine.Object.DestroyImmediate(
                        existing.gameObject);
                }
            }
        }

        private static Vector3 FindMapCenter(
            IEnumerable<DonorWorldBaselineEntityMetadata> metadata)
        {
            DonorWorldBaselineEntityMetadata anchor = metadata
                .SingleOrDefault(
                    item => string.Equals(
                        item.SourceHierarchyPath,
                        MapAnchorPath,
                        StringComparison.Ordinal));
            Renderer renderer =
                anchor != null
                    ? anchor.GetComponentInChildren<Renderer>(true)
                    : null;
            if (renderer == null)
            {
                throw new InvalidDataException(
                    "Canonical map anchor renderer is missing.");
            }
            return renderer.bounds.center;
        }

        private static List<Anchor> CollectLegacyAnchors(
            IEnumerable<DonorWorldBaselineEntityMetadata> metadata,
            VegetationSource source,
            float gridSize)
        {
            var buckets = new Dictionary<GridKey, AnchorAccumulator>();
            foreach (DonorWorldBaselineEntityMetadata item in metadata)
            {
                string path = item.SourceHierarchyPath ?? string.Empty;
                bool isBoundary =
                    path.IndexOf(
                        "TREEWALL_",
                        StringComparison.OrdinalIgnoreCase) >= 0;
                bool isTree =
                    !isBoundary &&
                    path.IndexOf(
                        "/FOLIAGE/TREES",
                        StringComparison.OrdinalIgnoreCase) >= 0;
                bool isShrub =
                    path.IndexOf(
                        "/FOLIAGE/BUSHES",
                        StringComparison.OrdinalIgnoreCase) >= 0;
                bool matches =
                    source == VegetationSource.Boundary &&
                    isBoundary ||
                    source == VegetationSource.Tree &&
                    isTree ||
                    source == VegetationSource.Shrub &&
                    isShrub;
                if (!matches)
                {
                    continue;
                }

                MeshFilter[] filters =
                    item.GetComponentsInChildren<MeshFilter>(true);
                foreach (MeshFilter filter in filters)
                {
                    AddMeshVerticesToBuckets(
                        filter,
                        gridSize,
                        isBoundary,
                        buckets);
                }
            }
            return buckets.Values
                .Where(value => value.Count > 0)
                .Select(value => value.ToAnchor())
                .ToList();
        }

        private static List<Anchor> CollectTerrainAnchors(
            IEnumerable<DonorWorldBaselineEntityMetadata> metadata,
            float gridSize,
            int maximum)
        {
            DonorWorldBaselineEntityMetadata terrain = metadata
                .SingleOrDefault(
                    item => string.Equals(
                        item.SourceHierarchyPath,
                        MapAnchorPath,
                        StringComparison.Ordinal));
            if (terrain == null)
            {
                throw new InvalidDataException(
                    "Canonical grass terrain source is missing.");
            }

            var surfaces = new List<SurfaceTriangle>();
            double totalProjectedArea = 0d;
            int sourceTriangleIndex = 0;
            foreach (MeshFilter filter in
                     terrain.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Vector3[] vertices;
                int[] triangles;
                try
                {
                    vertices = mesh.vertices;
                    triangles = mesh.triangles;
                }
                catch (UnityException exception)
                {
                    Debug.LogWarning(
                        "Skipping non-readable grass surface mesh '" +
                        mesh.name +
                        "': " +
                        exception.Message);
                    continue;
                }

                Matrix4x4 matrix = filter.transform.localToWorldMatrix;
                for (int triangleIndex = 0;
                     triangleIndex + 2 < triangles.Length;
                     triangleIndex += 3)
                {
                    Vector3 a = matrix.MultiplyPoint3x4(
                        vertices[triangles[triangleIndex]]);
                    Vector3 b = matrix.MultiplyPoint3x4(
                        vertices[triangles[triangleIndex + 1]]);
                    Vector3 c = matrix.MultiplyPoint3x4(
                        vertices[triangles[triangleIndex + 2]]);
                    if (!IsFinite(a) || !IsFinite(b) || !IsFinite(c))
                    {
                        continue;
                    }

                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    if (normal.sqrMagnitude < 0.0001f)
                    {
                        continue;
                    }
                    normal.Normalize();
                    if (Mathf.Abs(Vector3.Dot(normal, Vector3.up)) < 0.68f)
                    {
                        continue;
                    }

                    float projectedArea = Mathf.Abs(
                        (b.x - a.x) * (c.z - a.z) -
                        (b.z - a.z) * (c.x - a.x)) * 0.5f;
                    if (projectedArea < 0.05f)
                    {
                        continue;
                    }

                    surfaces.Add(
                        new SurfaceTriangle(
                            a,
                            b,
                            c,
                            projectedArea,
                            sourceTriangleIndex++));
                    totalProjectedArea += projectedArea;
                }
            }
            if (surfaces.Count == 0 || totalProjectedArea <= 0d)
            {
                throw new InvalidDataException(
                    "Canonical grass terrain exposes no eligible " +
                    "surface triangles.");
            }

            var anchors = new Dictionary<GridKey, Anchor>();
            int desiredSamples = Mathf.CeilToInt(maximum * 1.65f);
            foreach (SurfaceTriangle surface in surfaces)
            {
                double exactSamples =
                    surface.ProjectedArea /
                    totalProjectedArea *
                    desiredSamples;
                int samples = Mathf.FloorToInt((float)exactSamples);
                float remainder =
                    (float)(exactSamples - samples);
                uint allocationRandom = Hash(
                    surface.Center,
                    (uint)(surface.SourceIndex + 0x27d4));
                if ((allocationRandom & 0xffffu) / 65535f <
                    remainder)
                {
                    samples++;
                }

                for (int sampleIndex = 0;
                     sampleIndex < samples;
                     sampleIndex++)
                {
                    uint random = Hash(
                        surface.Center,
                        (uint)(
                            surface.SourceIndex * 17 +
                            sampleIndex * 7919 +
                            0x73a1));
                    float u =
                        (random & 0xffffu) / 65535f;
                    float v =
                        ((random >> 16) & 0xffffu) / 65535f;
                    float root = Mathf.Sqrt(u);
                    Vector3 position =
                        (1f - root) * surface.A +
                        root * (1f - v) * surface.B +
                        root * v * surface.C;
                    GridKey key =
                        GridKey.From(position, gridSize);
                    if (anchors.ContainsKey(key))
                    {
                        continue;
                    }
                    if (!TryResolveVegetationGround(
                            position,
                            position.y,
                            rejectHardSurfaces: true,
                            out float groundHeight))
                    {
                        continue;
                    }
                    position.y = groundHeight + 0.02f;
                    anchors.Add(
                        key,
                        new Anchor(position, false));
                }
            }
            List<Anchor> result = anchors.Values
                .OrderBy(
                    anchor => Hash(
                        anchor.Position,
                        0x83d29e1bu))
                .Take(maximum)
                .ToList();
            if (result.Count < 5000)
            {
                throw new InvalidDataException(
                    "Canonical grass terrain produced too few surface " +
                    $"anchors: {result.Count}.");
            }
            return result;
        }

        private static bool IsReplaceableVegetation(
            DonorWorldBaselineEntityMetadata metadata,
            string path)
        {
            if (path.IndexOf(
                    "TREEWALL_",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            if (path.IndexOf(
                    "/FOLIAGE/TREES",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                path.IndexOf(
                    "/FOLIAGE/BUSHES",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
            return string.Equals(
                       metadata.SemanticCategory,
                       "VegetationTree",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       metadata.SemanticCategory,
                       "VegetationBush",
                       StringComparison.Ordinal);
        }

        private static List<Anchor> CollectBetterMscForestAnchors(
            Transform betterMscRoot,
            float gridSize)
        {
            Transform detailed =
                betterMscRoot.Find("Detailed Forest Replacement");
            if (detailed == null)
            {
                throw new InvalidDataException(
                    "BetterMSC detailed forest instance is missing.");
            }
            var buckets = new Dictionary<GridKey, AnchorAccumulator>();
            foreach (MeshFilter filter in
                     detailed.GetComponentsInChildren<MeshFilter>(true))
            {
                AddMeshVerticesToBuckets(
                    filter,
                    gridSize,
                    false,
                    buckets);
            }
            return buckets.Values
                .Where(value => value.Count > 0)
                .Select(value => value.ToAnchor())
                .ToList();
        }

        private static void AddMeshVerticesToBuckets(
            MeshFilter filter,
            float gridSize,
            bool boundary,
            IDictionary<GridKey, AnchorAccumulator> buckets)
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                return;
            }

            Vector3[] vertices;
            try
            {
                vertices = mesh.vertices;
            }
            catch (UnityException exception)
            {
                Debug.LogWarning(
                    "Skipping non-readable vegetation mesh '" +
                    mesh.name +
                    "': " +
                    exception.Message);
                return;
            }

            Matrix4x4 matrix = filter.transform.localToWorldMatrix;
            int stride = Math.Max(1, vertices.Length / 60000);
            for (int index = 0;
                 index < vertices.Length;
                 index += stride)
            {
                Vector3 world = matrix.MultiplyPoint3x4(
                    vertices[index]);
                if (!IsFinite(world))
                {
                    continue;
                }
                GridKey key = GridKey.From(world, gridSize);
                if (!buckets.TryGetValue(
                        key,
                        out AnchorAccumulator accumulator))
                {
                    accumulator = new AnchorAccumulator(
                        boundary);
                    buckets.Add(key, accumulator);
                }
                accumulator.Add(world);
            }
        }

        private static List<Anchor> BuildOutwardForestBands(
            IReadOnlyList<Anchor> boundary,
            Vector3 mapCenter)
        {
            float[] distances =
            {
                8f,
                18f,
                31f,
                47f,
                67f,
                92f,
                122f,
                156f,
                194f,
                236f,
                282f,
                332f
            };
            var result = new List<Anchor>(
                boundary.Count * distances.Length);
            for (int index = 0; index < boundary.Count; index++)
            {
                Anchor source = boundary[index];
                Vector2 outward2 = new Vector2(
                    source.Position.x - mapCenter.x,
                    source.Position.z - mapCenter.z);
                if (outward2.sqrMagnitude < 0.001f)
                {
                    continue;
                }
                outward2.Normalize();
                var tangent = new Vector2(-outward2.y, outward2.x);
                uint hash = Hash(source.Position, (uint)index);
                for (int band = 0; band < distances.Length; band++)
                {
                    float tangentOffset =
                        Signed01(hash >> (band * 3)) *
                        (7f + band * 2.5f);
                    Vector3 position = source.Position;
                    position.x +=
                        outward2.x * distances[band] +
                        tangent.x * tangentOffset;
                    position.z +=
                        outward2.y * distances[band] +
                        tangent.y * tangentOffset;
                    position.y = FindGroundHeight(
                        position,
                        source.Position.y);
                    result.Add(new Anchor(position, true));
                }
            }
            return result;
        }

        private static List<Anchor> BuildRoadsideForestBands(
            IEnumerable<DonorWorldBaselineEntityMetadata> metadata)
        {
            var edges =
                new Dictionary<RoadEdgeKey, RoadEdgeAccumulator>();
            foreach (DonorWorldBaselineEntityMetadata item in metadata)
            {
                if (!IsRoadsidePlantingSource(item))
                {
                    continue;
                }

                foreach (MeshFilter filter in
                         item.GetComponentsInChildren<MeshFilter>(true))
                {
                    AddRoadBoundaryEdges(filter, edges);
                }
            }

            float[] offsets =
            {
                6.5f,
                11.5f
            };
            var result = new List<Anchor>(
                Math.Min(
                    MaximumRoadsideTreeCount * 2,
                    edges.Count * offsets.Length));
            int edgeIndex = 0;
            foreach (RoadEdgeAccumulator edge in edges.Values
                         .Where(value => value.Count == 1)
                         .OrderBy(
                             value => Hash(
                                 (value.A + value.B) * 0.5f,
                                 0x4a27u)))
            {
                Vector2 segment = new Vector2(
                    edge.B.x - edge.A.x,
                    edge.B.z - edge.A.z);
                float length = segment.magnitude;
                if (length < 1f ||
                    float.IsNaN(length) ||
                    float.IsInfinity(length))
                {
                    continue;
                }
                Vector2 tangent = segment / length;
                Vector2 outward =
                    new Vector2(-tangent.y, tangent.x);
                Vector2 midpoint = new Vector2(
                    (edge.A.x + edge.B.x) * 0.5f,
                    (edge.A.z + edge.B.z) * 0.5f);
                Vector2 interior = new Vector2(
                    edge.Interior.x,
                    edge.Interior.z) - midpoint;
                if (Vector2.Dot(outward, interior) > 0f)
                {
                    outward = -outward;
                }

                int sampleCount = Math.Max(
                    1,
                    Mathf.FloorToInt(
                        length /
                        RoadsideSampleSpacingMeters));
                for (int sample = 0;
                     sample < sampleCount;
                     sample++)
                {
                    float t = (sample + 0.5f) / sampleCount;
                    Vector3 edgePoint = Vector3.Lerp(
                        edge.A,
                        edge.B,
                        t);
                    uint seed = Hash(
                        edgePoint,
                        (uint)(
                            edgeIndex * 131 +
                            sample * 17 +
                            0x75b3));
                    float alongJitter =
                        Signed01(seed) *
                        Mathf.Min(
                            1.25f,
                            length /
                            (sampleCount * 3f));
                    edgePoint.x += tangent.x * alongJitter;
                    edgePoint.z += tangent.y * alongJitter;

                    for (int band = 0;
                         band < offsets.Length;
                         band++)
                    {
                        float lateralJitter =
                            Signed01(seed >> (5 + band * 7)) *
                            1.1f;
                        Vector3 position = edgePoint;
                        position.x +=
                            outward.x *
                            (offsets[band] + lateralJitter);
                        position.z +=
                            outward.y *
                            (offsets[band] + lateralJitter);
                        if (!TryResolveVegetationGround(
                                position,
                                edgePoint.y,
                                rejectHardSurfaces: true,
                                out float groundHeight))
                        {
                            continue;
                        }
                        position.y = groundHeight;
                        if (!HasTreeSurfaceClearance(
                                position,
                                TreeSurfaceClearanceMeters))
                        {
                            continue;
                        }
                        result.Add(
                            new Anchor(
                                position,
                                false));
                    }
                }
                edgeIndex++;
            }
            return result;
        }

        private static bool IsRoadsidePlantingSource(
            DonorWorldBaselineEntityMetadata metadata)
        {
            string path =
                metadata.SourceHierarchyPath ?? string.Empty;
            return path.EndsWith(
                       "/TERRAIN_OBJ/ROAD",
                       StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(
                       "/TERRAIN_OBJ/DIRTROAD",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void AddRoadBoundaryEdges(
            MeshFilter filter,
            IDictionary<RoadEdgeKey, RoadEdgeAccumulator> edges)
        {
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                return;
            }

            Vector3[] vertices;
            int[] triangles;
            try
            {
                vertices = mesh.vertices;
                triangles = mesh.triangles;
            }
            catch (UnityException exception)
            {
                Debug.LogWarning(
                    "Skipping non-readable road mesh '" +
                    mesh.name +
                    "' while building roadside forest: " +
                    exception.Message);
                return;
            }

            Matrix4x4 matrix =
                filter.transform.localToWorldMatrix;
            for (int index = 0;
                 index + 2 < triangles.Length;
                 index += 3)
            {
                Vector3 a = matrix.MultiplyPoint3x4(
                    vertices[triangles[index]]);
                Vector3 b = matrix.MultiplyPoint3x4(
                    vertices[triangles[index + 1]]);
                Vector3 c = matrix.MultiplyPoint3x4(
                    vertices[triangles[index + 2]]);
                Vector3 normal = Vector3.Cross(
                    b - a,
                    c - a);
                if (normal.sqrMagnitude < 0.0001f ||
                    Mathf.Abs(
                        Vector3.Dot(
                            normal.normalized,
                            Vector3.up)) < 0.72f)
                {
                    continue;
                }

                AddRoadBoundaryEdge(
                    a,
                    b,
                    c,
                    edges);
                AddRoadBoundaryEdge(
                    b,
                    c,
                    a,
                    edges);
                AddRoadBoundaryEdge(
                    c,
                    a,
                    b,
                    edges);
            }
        }

        private static void AddRoadBoundaryEdge(
            Vector3 a,
            Vector3 b,
            Vector3 interior,
            IDictionary<RoadEdgeKey, RoadEdgeAccumulator> edges)
        {
            RoadEdgeKey key = RoadEdgeKey.From(a, b);
            if (!edges.TryGetValue(
                    key,
                    out RoadEdgeAccumulator accumulator))
            {
                accumulator =
                    new RoadEdgeAccumulator(a, b, interior);
                edges.Add(key, accumulator);
            }
            else
            {
                accumulator.Increment();
            }
        }

        private static List<Anchor> BuildLocalForestClusters(
            IReadOnlyList<Anchor> sourceAnchors)
        {
            var result = new List<Anchor>(
                sourceAnchors.Count + sourceAnchors.Count / 3);
            for (int index = 0; index < sourceAnchors.Count; index++)
            {
                Anchor source = sourceAnchors[index];
                uint seed = Hash(
                    source.Position,
                    (uint)(index + 0x6bc1));
                int copies = (seed & 3u) == 0u ? 5 : 4;
                for (int copy = 0; copy < copies; copy++)
                {
                    uint random = Hash(
                        source.Position,
                        seed + (uint)(copy * 0x45d9f3b));
                    float angle =
                        (random & 0xffffu) / 65535f *
                        Mathf.PI * 2f;
                    float distance = Mathf.Lerp(
                        4.5f,
                        17f,
                        ((random >> 16) & 0xffffu) / 65535f);
                    Vector3 position = source.Position;
                    position.x += Mathf.Cos(angle) * distance;
                    position.z += Mathf.Sin(angle) * distance;
                    if (!TryResolveVegetationGround(
                            position,
                            source.Position.y,
                            rejectHardSurfaces: true,
                            out float groundHeight))
                    {
                        continue;
                    }
                    position.y = groundHeight;
                    result.Add(new Anchor(position, false));
                }
            }
            return result;
        }

        private static List<Anchor> BuildSparseVoidFillAnchors(
            IReadOnlyList<Anchor> groundAnchors,
            IReadOnlyList<Anchor> existingTreeAnchors,
            Phase1TreePlacementExclusionIndex exclusionIndex)
        {
            var coarseCandidates = new Dictionary<GridKey, Anchor>();
            for (int index = 0; index < groundAnchors.Count; index++)
            {
                Anchor candidate = groundAnchors[index];
                GridKey key = GridKey.From(
                    candidate.Position,
                    SparseVoidGridMeters);
                if (!coarseCandidates.TryGetValue(
                        key,
                        out Anchor current) ||
                    Hash(candidate.Position, 0x2cf5u) <
                    Hash(current.Position, 0x2cf5u))
                {
                    coarseCandidates[key] = candidate;
                }
            }

            var proximity = new AnchorProximityIndex(
                existingTreeAnchors,
                SparseVoidMinimumTreeDistanceMeters);
            var result = new List<Anchor>(MaximumSparseVoidTreeCount);
            foreach (Anchor candidate in coarseCandidates.Values
                         .OrderBy(
                             anchor => Hash(
                                 anchor.Position,
                                 0x73a9u)))
            {
                uint seed = Hash(candidate.Position, 0x5bd1u);
                if ((seed & 0xffffu) > SparseVoidKeepThreshold)
                {
                    continue;
                }

                Vector3 position = candidate.Position;
                if (!TryResolveVegetationGround(
                        position,
                        candidate.Position.y,
                        rejectHardSurfaces: true,
                        out float groundHeight))
                {
                    continue;
                }
                position.y = groundHeight;
                if (exclusionIndex.Blocks(position) ||
                    !HasTreeSurfaceClearance(
                        position,
                        TreeSurfaceClearanceMeters) ||
                    proximity.HasNeighbor(
                        position,
                        SparseVoidMinimumTreeDistanceMeters))
                {
                    continue;
                }

                var anchor = new Anchor(position, false);
                result.Add(anchor);
                proximity.Add(position);
                if (result.Count >= MaximumSparseVoidTreeCount)
                {
                    break;
                }
            }
            return result;
        }

        private static float FindGroundHeight(
            Vector3 position,
            float fallback)
        {
            return TryResolveVegetationGround(
                position,
                fallback,
                rejectHardSurfaces: false,
                out float height)
                ? height
                : fallback;
        }

        private static bool TryResolveVegetationGround(
            Vector3 position,
            float fallback,
            bool rejectHardSurfaces,
            out float height)
        {
            Vector3 origin = new Vector3(
                position.x,
                Mathf.Max(position.y + 300f, 450f),
                position.z);
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                Vector3.down,
                1200f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            Array.Sort(
                hits,
                (left, right) =>
                    left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                DonorWorldBaselineEntityMetadata metadata =
                    hit.collider.GetComponentInParent<
                        DonorWorldBaselineEntityMetadata>();
                if (metadata != null &&
                    IsReplaceableVegetation(
                        metadata,
                        metadata.SourceHierarchyPath ?? string.Empty))
                {
                    continue;
                }
                if (rejectHardSurfaces &&
                    IsForbiddenVegetationSurface(
                        hit.collider,
                        metadata))
                {
                    height = fallback;
                    return false;
                }
                if (Vector3.Dot(hit.normal, Vector3.up) < 0.55f)
                {
                    continue;
                }
                height = hit.point.y;
                return true;
            }
            height = fallback;
            return !rejectHardSurfaces;
        }

        private static bool IsForbiddenVegetationSurface(
            DonorWorldBaselineEntityMetadata metadata)
        {
            if (metadata == null)
            {
                return false;
            }

            string category =
                metadata.SemanticCategory ?? string.Empty;
            if (category.IndexOf(
                    "Road",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                category.IndexOf(
                    "Building",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                category.IndexOf(
                    "Bridge",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                category.IndexOf(
                    "Water",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            string path =
                metadata.SourceHierarchyPath ?? string.Empty;
            string[] forbiddenTokens =
            {
                "/ROAD",
                "/HIGHWAY",
                "/STREET",
                "/BRIDGE",
                "/RAIL",
                "/BUILDING",
                "/AIRPORT",
                "/WATER"
            };
            return forbiddenTokens.Any(
                token => path.IndexOf(
                    token,
                    StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static bool IsForbiddenVegetationSurface(
            Collider collider,
            DonorWorldBaselineEntityMetadata metadata)
        {
            if (IsForbiddenVegetationSurface(metadata))
            {
                return true;
            }
            if (collider == null)
            {
                return false;
            }

            Transform current = collider.transform;
            while (current != null)
            {
                string name = current.name ?? string.Empty;
                string[] forbiddenTokens =
                {
                    "road",
                    "dirtroad",
                    "street",
                    "highway",
                    "building",
                    "foundation",
                    "airport",
                    "bridge",
                    "water"
                };
                if (forbiddenTokens.Any(
                        token => name.IndexOf(
                            token,
                            StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return true;
                }
                current = current.parent;
            }
            return false;
        }

        private static bool HasTreeSurfaceClearance(
            Vector3 position,
            float radius)
        {
            Vector2[] offsets =
            {
                Vector2.zero,
                new Vector2(radius, 0f),
                new Vector2(-radius, 0f),
                new Vector2(0f, radius),
                new Vector2(0f, -radius),
                new Vector2(
                    radius * 0.70710678f,
                    radius * 0.70710678f),
                new Vector2(
                    -radius * 0.70710678f,
                    radius * 0.70710678f),
                new Vector2(
                    radius * 0.70710678f,
                    -radius * 0.70710678f),
                new Vector2(
                    -radius * 0.70710678f,
                    -radius * 0.70710678f)
            };
            foreach (Vector2 offset in offsets)
            {
                Vector3 sample = position;
                sample.x += offset.x;
                sample.z += offset.y;
                if (!TryResolveVegetationGround(
                        sample,
                        position.y,
                        rejectHardSurfaces: true,
                        out float groundHeight) ||
                    Mathf.Abs(groundHeight - position.y) > 1.6f)
                {
                    return false;
                }
            }
            return true;
        }

        private static List<Anchor> DeduplicateAndLimit(
            IEnumerable<Anchor> source,
            float gridSize,
            int maximum)
        {
            var unique = new Dictionary<GridKey, Anchor>();
            foreach (Anchor anchor in source)
            {
                GridKey key = GridKey.From(anchor.Position, gridSize);
                if (!unique.TryGetValue(key, out Anchor current) ||
                    anchor.IsBoundary && !current.IsBoundary)
                {
                    unique[key] = anchor;
                }
            }

            return unique.Values
                .OrderBy(
                    anchor => Hash(anchor.Position, 0x9e3779b9u))
                .Take(maximum)
                .ToList();
        }

        private static GameObject CreateRemediationRoot(
            Scene scene,
            GameObject contentRoot)
        {
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetParent(contentRoot.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            DonorWorldBaselineEntityMetadata metadata =
                root.AddComponent<DonorWorldBaselineEntityMetadata>();
            metadata.Configure(
                StableIdFor("phase1-forest-remediation-root"),
                0,
                string.Empty,
                "Phase1/ForestRemediation",
                string.Empty,
                "global",
                "VegetationTree",
                "4;114",
                "LicensedThirdPartyPhase1Presentation",
                "User-approved replacement for legacy sprite tree " +
                "walls and distant forest presentation.",
                true,
                true,
                false);
            return root;
        }

        private static Transform GetOrCreateCellRoot(
            Transform root,
            Vector3 position,
            IDictionary<GridKey, Transform> roots)
        {
            GridKey key = GridKey.From(position, CellSizeMeters);
            if (roots.TryGetValue(key, out Transform existing))
            {
                return existing;
            }
            var cell = new GameObject(
                $"Forest_Cell_{key.X}_{key.Z}");
            cell.transform.SetParent(root, false);
            roots.Add(key, cell.transform);
            return cell.transform;
        }

        private static void ConfigureRuntimeCellCulling(
            GameObject forestRoot,
            IReadOnlyDictionary<GridKey, Transform> cellRoots)
        {
            KeyValuePair<GridKey, Transform>[] orderedCells =
                cellRoots
                    .OrderBy(pair => pair.Key.X)
                    .ThenBy(pair => pair.Key.Z)
                    .ToArray();
            var roots = new GameObject[orderedCells.Length];
            var bounds = new Bounds[orderedCells.Length];
            for (int index = 0; index < orderedCells.Length; index++)
            {
                GridKey key = orderedCells[index].Key;
                roots[index] = orderedCells[index].Value.gameObject;
                bounds[index] = new Bounds(
                    new Vector3(
                        (key.X + 0.5f) * CellSizeMeters,
                        0f,
                        (key.Z + 0.5f) * CellSizeMeters),
                    new Vector3(
                        CellSizeMeters,
                        100000f,
                        CellSizeMeters));
            }

            Phase1ForestRuntimeCuller culler =
                forestRoot.AddComponent<Phase1ForestRuntimeCuller>();
            culler.ConfigureGeneratedCells(
                roots,
                bounds,
                RuntimeForestVisibleDistanceMeters,
                RuntimeForestHysteresisMeters);
            forestRoot.AddComponent<Phase1SpruceWindController>();
        }

        private static Transform GetOrCreateCategoryRoot(
            Transform cellRoot,
            string categoryName)
        {
            Transform existing = cellRoot.Find(categoryName);
            if (existing != null)
            {
                return existing;
            }
            var category = new GameObject(categoryName);
            category.transform.SetParent(cellRoot, false);
            return category.transform;
        }

        private static bool PlaceTree(
            GameObject prefab,
            Anchor anchor,
            int index,
            Transform parent,
            IDictionary<GameObject, float> prefabHeightCache,
            Phase1TreePlacementExclusionIndex exclusionIndex,
            out TreePlacementRejectionReason rejectionReason,
            out string exclusionSource)
        {
            rejectionReason = TreePlacementRejectionReason.None;
            exclusionSource = string.Empty;
            uint random = Hash(anchor.Position, (uint)index);
            Vector3 position = anchor.Position;
            float jitterRadius = anchor.IsBoundary ? 1.8f : 0.75f;
            position.x += Signed01(random) * jitterRadius;
            position.z += Signed01(random >> 7) * jitterRadius;
            if (!TryResolveVegetationGround(
                    position,
                    anchor.Position.y,
                    rejectHardSurfaces: true,
                    out float groundHeight))
            {
                position = anchor.Position;
                if (!TryResolveVegetationGround(
                        position,
                        anchor.Position.y,
                        rejectHardSurfaces: true,
                        out groundHeight))
                {
                    rejectionReason = TreePlacementRejectionReason.Ground;
                    return false;
                }
            }
            position.y = groundHeight;
            if (exclusionIndex.TryGetBlockingInfo(
                    position,
                    out string exclusionKind,
                    out string blockingSource))
            {
                rejectionReason = TreePlacementRejectionReason.Exclusion;
                exclusionSource = exclusionKind + ":" + blockingSource;
                return false;
            }
            if (!HasTreeSurfaceClearance(
                    position,
                    TreeSurfaceClearanceMeters))
            {
                rejectionReason = TreePlacementRejectionReason.Surface;
                return false;
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(
                    prefab,
                    parent.gameObject.scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Failed to instantiate vegetation prefab: " +
                    prefab.name);
            }
            Quaternion sourceRotation =
                instance.transform.localRotation;
            instance.name = $"Tree_{index:00000}_{prefab.name}";
            instance.transform.SetParent(parent, true);
            instance.transform.position = position;
            instance.transform.rotation =
                Quaternion.Euler(
                    0f,
                    (random >> 13) % 360,
                    0f) *
                sourceRotation;
            instance.transform.localScale = Vector3.one;

            float sourceHeight = GetPrefabHeight(
                prefab,
                instance,
                prefabHeightCache);
            float targetHeight = TargetTreeHeight(
                prefab.name,
                random) * TreeHeightScale;
            float maximumUniformScale =
                IsGeneratedWindSpruceName(prefab.name)
                    ? 24f
                    : 3.25f;
            float uniformScale = Mathf.Clamp(
                targetHeight / sourceHeight,
                0.35f,
                maximumUniformScale);
            instance.transform.localScale =
                Vector3.one * uniformScale;
            ConfigurePlacedInstance(
                instance,
                castShadows: true);
            AddGameplayTrunkCollider(
                instance,
                position,
                targetHeight);
            return true;
        }

        private enum TreePlacementRejectionReason
        {
            None,
            Ground,
            Exclusion,
            Surface
        }

        private static void AddGameplayTrunkCollider(
            GameObject tree,
            Vector3 groundPosition,
            float treeHeight)
        {
            float colliderHeight = Mathf.Clamp(
                treeHeight * 0.56f,
                4f,
                10f);
            float colliderRadius = Mathf.Clamp(
                treeHeight * 0.034f,
                0.36f,
                0.78f);
            var collision = new GameObject(
                "Gameplay Trunk Collider");
            collision.transform.position =
                groundPosition +
                Vector3.up * (colliderHeight * 0.5f);
            collision.transform.rotation = Quaternion.identity;
            collision.transform.localScale = Vector3.one;
            collision.transform.SetParent(tree.transform, true);
            var collider =
                collision.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.center = Vector3.zero;
            collider.height = colliderHeight;
            collider.radius = colliderRadius;
        }

        private static int PlaceDecorativeInstances(
            IReadOnlyList<Anchor> anchors,
            IReadOnlyList<GameObject> prefabs,
            Transform root,
            IDictionary<GridKey, Transform> cellRoots,
            IDictionary<GameObject, float> prefabHeightCache,
            string instancePrefix,
            string categoryName,
            int maximumCount,
            float jitterRadius,
            float minimumHeight,
            float maximumHeight,
            uint salt,
            bool castShadows)
        {
            int count = Math.Min(
                maximumCount,
                anchors.Count);
            int placedCount = 0;
            for (int index = 0; index < count; index++)
            {
                Anchor anchor = anchors[index];
                uint random = Hash(
                    anchor.Position,
                    (uint)index + salt);
                GameObject prefab =
                    prefabs[
                        (int)(random % (uint)prefabs.Count)];
                Transform parent = GetOrCreateCellRoot(
                    root,
                    anchor.Position,
                    cellRoots);
                parent = GetOrCreateCategoryRoot(
                    parent,
                    categoryName);
                GameObject instance =
                    PrefabUtility.InstantiatePrefab(
                        prefab,
                        parent.gameObject.scene) as GameObject;
                if (instance == null)
                {
                    throw new InvalidOperationException(
                        "Failed to instantiate undergrowth prefab: " +
                        prefab.name);
                }
                Quaternion sourceRotation =
                    instance.transform.localRotation;
                instance.name =
                    $"{instancePrefix}_{index:00000}_{prefab.name}";
                instance.transform.SetParent(parent, true);
                Vector3 position = anchor.Position;
                position.x +=
                    Signed01(random >> 5) * jitterRadius;
                position.z +=
                    Signed01(random >> 11) * jitterRadius;
                if (!TryResolveVegetationGround(
                        position,
                        anchor.Position.y,
                        rejectHardSurfaces: true,
                        out float groundHeight))
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                    continue;
                }
                position.y = groundHeight;
                instance.transform.position = position;
                instance.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        (random >> 17) % 360,
                        0f) *
                    sourceRotation;
                instance.transform.localScale = Vector3.one;
                float sourceHeight = GetPrefabHeight(
                    prefab,
                    instance,
                    prefabHeightCache);
                float targetHeight =
                    Mathf.Lerp(
                        minimumHeight,
                        maximumHeight,
                        ((random >> 23) & 255) / 255f);
                instance.transform.localScale =
                    Vector3.one *
                    Mathf.Clamp(
                        targetHeight / sourceHeight,
                        0.25f,
                        2.5f);
                ConfigurePlacedInstance(
                    instance,
                    castShadows);
                placedCount++;
            }
            return placedCount;
        }

        private static GrassFieldBuildResult BuildGrassFields(
            IReadOnlyList<Anchor> anchors,
            Transform root,
            IDictionary<GridKey, Transform> cellRoots,
            Material material)
        {
            RecreateGeneratedGrassMeshFolder();
            Mesh patchMesh = BuildGrassPatchMesh();
            AssetDatabase.CreateAsset(
                patchMesh,
                GrassPatchMeshPath);
            Dictionary<GridKey, List<Anchor>> byCell = anchors
                .GroupBy(
                    anchor => GridKey.From(
                        anchor.Position,
                        CellSizeMeters))
                .ToDictionary(
                    group => group.Key,
                    group => group.ToList());
            int fieldCount = 0;
            int clusterCount = 0;
            foreach (KeyValuePair<GridKey, List<Anchor>> pair in
                     byCell.OrderBy(item => item.Key.X)
                         .ThenBy(item => item.Key.Z))
            {
                if (pair.Value.Count == 0)
                {
                    continue;
                }

                Vector3 origin = new Vector3(
                    pair.Key.X * CellSizeMeters,
                    0f,
                    pair.Key.Z * CellSizeMeters);
                Vector3[] localPositions = pair.Value
                    .Select(anchor => anchor.Position - origin)
                    .ToArray();
                Bounds localBounds = CalculateBounds(localPositions);
                var data = ScriptableObject.CreateInstance<
                    Phase1GrassFieldData>();
                data.name =
                    $"Phase1 Grass Field {pair.Key.X},{pair.Key.Z}";
                data.SetGeneratedData(
                    localPositions,
                    localBounds);
                string dataPath =
                    GrassFieldDataRoot +
                    $"/Phase1_Grass_Field_{pair.Key.X}_{pair.Key.Z}.asset";
                AssetDatabase.CreateAsset(data, dataPath);

                Transform cellRoot = GetOrCreateCellRoot(
                    root,
                    pair.Value[0].Position,
                    cellRoots);
                Transform parent = GetOrCreateCategoryRoot(
                    cellRoot,
                    "Grass Fields");
                var field = new GameObject(
                    $"GrassField_{pair.Key.X}_{pair.Key.Z}_" +
                    $"{pair.Value.Count:00000}");
                field.transform.SetParent(parent, false);
                field.transform.position = origin;
                var renderer =
                    field.AddComponent<Phase1GrassFieldRenderer>();
                renderer.ConfigureGeneratedField(
                    data,
                    patchMesh,
                    material,
                    115f,
                    180f);
                // Keep the generated data for the future mesh-painting pass,
                // but the currently rejected procedural carpet must not render.
                renderer.enabled = false;
                fieldCount++;
                clusterCount += pair.Value.Count;
            }

            if (fieldCount == 0 ||
                clusterCount < 5000)
            {
                throw new InvalidDataException(
                    "Dense grass field generation produced too little " +
                    $"content: fields={fieldCount}, " +
                    $"clusters={clusterCount}.");
            }
            return new GrassFieldBuildResult(
                fieldCount,
                clusterCount);
        }

        private static Mesh BuildGrassPatchMesh()
        {
            const int tuftsPerPatch = 36;
            const int planesPerTuft = 2;
            const int verticesPerPlane = 4;
            const int indicesPerPlane = 6;
            int vertexCapacity =
                tuftsPerPatch *
                planesPerTuft *
                verticesPerPlane;
            int indexCapacity =
                tuftsPerPatch *
                planesPerTuft *
                indicesPerPlane;
            var vertices = new List<Vector3>(vertexCapacity);
            var normals = new List<Vector3>(vertexCapacity);
            var uv = new List<Vector2>(vertexCapacity);
            var triangles = new List<int>(indexCapacity);

            var random = new System.Random(0x5197);
            for (int tuft = 0; tuft < tuftsPerPatch; tuft++)
            {
                float distributionAngle =
                    (float)random.NextDouble() * Mathf.PI * 2f;
                float radius =
                    Mathf.Sqrt((float)random.NextDouble()) * 1.68f;
                Vector3 center = new Vector3(
                    Mathf.Cos(distributionAngle) * radius,
                    0f,
                    Mathf.Sin(distributionAngle) * radius);
                float width = Mathf.Lerp(
                    0.16f,
                    0.32f,
                    (float)random.NextDouble());
                float height = Mathf.Lerp(
                    0.18f,
                    0.36f,
                    (float)random.NextDouble());
                float yaw =
                    (float)random.NextDouble() * 180f;
                Vector3 lean = new Vector3(
                    Mathf.Lerp(
                        -0.035f,
                        0.035f,
                        (float)random.NextDouble()),
                    0f,
                    Mathf.Lerp(
                        -0.035f,
                        0.035f,
                        (float)random.NextDouble()));
                for (int plane = 0;
                     plane < planesPerTuft;
                     plane++)
                {
                    float angle =
                        yaw + plane * 90f;
                    Vector3 axis =
                        Quaternion.Euler(0f, angle, 0f) *
                        Vector3.right;
                    Vector3 normal =
                        Quaternion.Euler(0f, angle, 0f) *
                        Vector3.forward;
                    Vector3 left =
                        center - axis * (width * 0.5f);
                    Vector3 right =
                        center + axis * (width * 0.5f);
                    int first = vertices.Count;
                    vertices.Add(left);
                    vertices.Add(right);
                    vertices.Add(
                        left + Vector3.up * height + lean);
                    vertices.Add(
                        right + Vector3.up * height + lean);
                    normals.Add(normal);
                    normals.Add(normal);
                    normals.Add(normal);
                    normals.Add(normal);
                    uv.Add(new Vector2(0f, 0f));
                    uv.Add(new Vector2(1f, 0f));
                    uv.Add(new Vector2(0f, 1f));
                    uv.Add(new Vector2(1f, 1f));
                    triangles.Add(first);
                    triangles.Add(first + 2);
                    triangles.Add(first + 1);
                    triangles.Add(first + 2);
                    triangles.Add(first + 3);
                    triangles.Add(first + 1);
                }
            }

            var mesh = new Mesh
            {
                name = "Phase1 Low Grass Patch"
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Bounds CalculateBounds(
            IReadOnlyList<Vector3> positions)
        {
            if (positions.Count == 0)
            {
                return new Bounds(Vector3.zero, Vector3.one);
            }
            var bounds = new Bounds(
                positions[0],
                Vector3.zero);
            for (int index = 1; index < positions.Count; index++)
            {
                bounds.Encapsulate(positions[index]);
            }
            bounds.Expand(new Vector3(4f, 1f, 4f));
            return bounds;
        }

        private static Material CreateOrUpdateGrassFieldMaterial()
        {
            EnsureAssetFolder(GrassFieldMaterialPath);
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    GrassFieldMaterialPath);
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "HDRP/Lit shader is unavailable.");
            }
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "Phase 1 Grass Field HDRP"
                };
                AssetDatabase.CreateAsset(
                    material,
                    GrassFieldMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            Texture2D albedo = CreateOrUpdateGrassFieldTexture();
            material.SetTexture("_BaseColorMap", albedo);
            material.SetColor(
                "_BaseColor",
                Color.white);
            material.SetTexture("_NormalMap", null);
            SetFloatIfPresent(material, "_NormalScale", 0f);
            SetFloatIfPresent(material, "_SurfaceType", 0f);
            SetFloatIfPresent(material, "_AlphaCutoffEnable", 1f);
            SetFloatIfPresent(material, "_AlphaCutoff", 0.36f);
            SetFloatIfPresent(material, "_DoubleSidedEnable", 1f);
            SetFloatIfPresent(material, "_DoubleSidedNormalMode", 1f);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.04f);
            SetFloatIfPresent(material, "_ReceivesSSR", 0f);
            material.doubleSidedGI = true;
            material.enableInstancing = true;
            if (!HDMaterial.ValidateMaterial(material))
            {
                throw new InvalidDataException(
                    "HDRP rejected the generated grass field material.");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyGrassGroundMaterial(
            IEnumerable<DonorWorldBaselineEntityMetadata> metadata)
        {
            DonorWorldBaselineEntityMetadata terrain = metadata
                .SingleOrDefault(
                    item => string.Equals(
                        item.SourceHierarchyPath,
                        MapAnchorPath,
                        StringComparison.Ordinal));
            if (terrain == null)
            {
                throw new InvalidDataException(
                    "Canonical grass terrain source is missing.");
            }

            Renderer[] renderers =
                terrain.GetComponentsInChildren<Renderer>(true);
            Material sourceMaterial = renderers
                .SelectMany(renderer => renderer.sharedMaterials)
                .FirstOrDefault(
                    material =>
                        material != null &&
                        !string.Equals(
                            AssetDatabase.GetAssetPath(material),
                            GrassGroundMaterialPath,
                            StringComparison.Ordinal));
            Material groundMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    GrassGroundMaterialPath);
            if (groundMaterial == null)
            {
                if (sourceMaterial == null)
                {
                    throw new InvalidDataException(
                        "Canonical grass terrain has no source material.");
                }
                EnsureAssetFolder(GrassGroundMaterialPath);
                groundMaterial = new Material(sourceMaterial)
                {
                    name = "Phase 1 Grass Ground HDRP"
                };
                AssetDatabase.CreateAsset(
                    groundMaterial,
                    GrassGroundMaterialPath);
            }
            else if (sourceMaterial != null)
            {
                string materialName = groundMaterial.name;
                groundMaterial.CopyPropertiesFromMaterial(sourceMaterial);
                groundMaterial.shader = sourceMaterial.shader;
                groundMaterial.name = materialName;
            }

            if (groundMaterial.HasProperty("_BaseColor"))
            {
                Color original =
                    groundMaterial.GetColor("_BaseColor");
                groundMaterial.SetColor(
                    "_BaseColor",
                    new Color(
                        0.44f,
                        0.57f,
                        0.31f,
                        original.a));
            }
            SetFloatIfPresent(groundMaterial, "_Metallic", 0f);
            SetFloatIfPresent(groundMaterial, "_Smoothness", 0.1f);
            SetFloatIfPresent(groundMaterial, "_ReceivesSSR", 0f);
            if (!HDMaterial.ValidateMaterial(groundMaterial))
            {
                throw new InvalidDataException(
                    "HDRP rejected the generated grass ground material.");
            }
            EditorUtility.SetDirty(groundMaterial);

            int assignedRendererCount = 0;
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = renderer.sharedMaterials;
                if (materials.Length == 0)
                {
                    continue;
                }
                materials[0] = groundMaterial;
                renderer.sharedMaterials = materials;
                EditorUtility.SetDirty(renderer);
                assignedRendererCount++;
            }
            if (assignedRendererCount == 0)
            {
                throw new InvalidDataException(
                    "Canonical grass terrain exposes no material slots.");
            }
        }

        private static Texture2D CreateOrUpdateGrassFieldTexture()
        {
            const int width = 256;
            const int height = 256;
            EnsureAssetFolder(GrassFieldTexturePath);
            Texture2D texture =
                AssetDatabase.LoadAssetAtPath<Texture2D>(
                    GrassFieldTexturePath);
            if (texture == null ||
                texture.width != width ||
                texture.height != height)
            {
                if (texture != null)
                {
                    AssetDatabase.DeleteAsset(
                        GrassFieldTexturePath);
                }
                texture = new Texture2D(
                    width,
                    height,
                    TextureFormat.RGBA32,
                    true,
                    false)
                {
                    name = "Phase 1 Low Grass Clump"
                };
                AssetDatabase.CreateAsset(
                    texture,
                    GrassFieldTexturePath);
            }

            var pixels = new Color32[width * height];
            var random = new System.Random(0x4195);
            for (int blade = 0; blade < 112; blade++)
            {
                float baseX =
                    Mathf.Lerp(3f, width - 4f, (float)random.NextDouble());
                int bladeHeight = Mathf.RoundToInt(
                    Mathf.Lerp(
                        height * 0.34f,
                        height * 0.92f,
                        (float)random.NextDouble()));
                float baseHalfWidth = Mathf.Lerp(
                    1.4f,
                    3.8f,
                    (float)random.NextDouble());
                float bend = Mathf.Lerp(
                    -15f,
                    15f,
                    (float)random.NextDouble());
                float tone = Mathf.Lerp(
                    0.82f,
                    1.12f,
                    (float)random.NextDouble());
                for (int y = 0; y < bladeHeight; y++)
                {
                    float t = y / (float)Math.Max(1, bladeHeight - 1);
                    float centerX =
                        baseX + bend * t * t;
                    int halfWidth = Mathf.Max(
                        0,
                        Mathf.RoundToInt(
                            baseHalfWidth *
                            Mathf.Pow(1f - t, 0.72f)));
                    Color bladeColor = Color.Lerp(
                        new Color(0.08f, 0.25f, 0.055f, 1f),
                        new Color(0.38f, 0.58f, 0.18f, 1f),
                        t * 0.8f) * tone;
                    bladeColor.a = 1f;
                    Color32 color = bladeColor;
                    int roundedCenter = Mathf.RoundToInt(centerX);
                    for (int xOffset = -halfWidth;
                         xOffset <= halfWidth;
                         xOffset++)
                    {
                        int x = roundedCenter + xOffset;
                        if (x < 0 || x >= width)
                        {
                            continue;
                        }
                        int pixelIndex = y * width + x;
                        if (pixels[pixelIndex].a == 0 ||
                            pixels[pixelIndex].g < color.g)
                        {
                            pixels[pixelIndex] = color;
                        }
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 2;
            texture.Apply(true, false);
            EditorUtility.SetDirty(texture);
            return texture;
        }

        private static Texture FindTexture(
            Material material,
            params string[] properties)
        {
            foreach (string property in properties)
            {
                if (!material.HasProperty(property))
                {
                    continue;
                }
                Texture texture = material.GetTexture(property);
                if (texture != null)
                {
                    return texture;
                }
            }
            return null;
        }

        private static void RecreateGeneratedGrassMeshFolder()
        {
            if (AssetDatabase.IsValidFolder(GrassFieldMeshRoot) &&
                !AssetDatabase.DeleteAsset(GrassFieldMeshRoot))
            {
                throw new IOException(
                    "Failed to clear generated grass field meshes.");
            }
            if (AssetDatabase.IsValidFolder(GrassFieldDataRoot) &&
                !AssetDatabase.DeleteAsset(GrassFieldDataRoot))
            {
                throw new IOException(
                    "Failed to clear generated grass field data.");
            }
            EnsureAssetFolder(GrassFieldMeshRoot);
            EnsureAssetFolder(GrassFieldDataRoot);
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string folder = Path.HasExtension(assetPath)
                ? Path.GetDirectoryName(assetPath)?.Replace('\\', '/')
                : assetPath;
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException(
                    "Asset path has no parent folder.",
                    nameof(assetPath));
            }
            string[] parts = folder.Split('/');
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

        private static void SetFloatIfPresent(
            Material material,
            string property,
            float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static float GetPrefabHeight(
            GameObject prefab,
            GameObject instance,
            IDictionary<GameObject, float> cache)
        {
            if (cache.TryGetValue(prefab, out float height))
            {
                return height;
            }
            Renderer[] renderers =
                instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidDataException(
                    "Vegetation prefab has no renderer: " +
                    prefab.name);
            }
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            height = Mathf.Max(bounds.size.y, 0.01f);
            cache.Add(prefab, height);
            return height;
        }

        private static float TargetTreeHeight(
            string prefabName,
            uint random)
        {
            float variation = ((random >> 20) & 255) / 255f;
            string lower = prefabName.ToLowerInvariant();
            if (lower.Contains("small"))
            {
                return Mathf.Lerp(4.5f, 8.5f, variation);
            }
            if (lower.Contains("med"))
            {
                return Mathf.Lerp(9f, 15f, variation);
            }
            if (lower.Contains("hill"))
            {
                return Mathf.Lerp(13f, 20f, variation);
            }
            return Mathf.Lerp(16f, 25f, variation);
        }

        private static void ConfigurePlacedInstance(
            GameObject instance,
            bool castShadows)
        {
            foreach (Collider collider in
                     instance.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }
            foreach (Renderer renderer in
                     instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode =
                    castShadows
                        ? ShadowCastingMode.On
                        : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage =
                    LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage =
                    ReflectionProbeUsage.BlendProbes;
                renderer.motionVectorGenerationMode =
                    renderer.sharedMaterials.Any(material =>
                        material != null &&
                        material.shader != null &&
                        string.Equals(
                            material.shader.name,
                            Phase1SpruceWindController.ShaderName,
                            StringComparison.Ordinal))
                        ? MotionVectorGenerationMode.Object
                        : MotionVectorGenerationMode.ForceNoMotion;
                GameObjectUtility.SetStaticEditorFlags(
                    renderer.gameObject,
                    StaticEditorFlags.OccludeeStatic);
            }
            foreach (LODGroup group in
                     instance.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = group.GetLODs();
                bool isEngelmannSpruce =
                    instance.name.IndexOf(
                        "EngelmannSpruce_",
                        StringComparison.Ordinal) >= 0;
                bool isNorwaySpruce =
                    instance.name.IndexOf(
                        "NorwaySpruce_",
                        StringComparison.Ordinal) >= 0;
                float[] transitionProfile = isEngelmannSpruce
                    ? new[]
                    {
                        0.32f,
                        0.045f,
                        0.0025f
                    }
                    : isNorwaySpruce
                    ? new[]
                    {
                        0.18f,
                        0.055f,
                        0.0025f
                    }
                    : new[]
                {
                    0.20f,
                    0.075f,
                    0.028f,
                    0.010f,
                    0.0035f
                };
                for (int index = 0; index < lods.Length; index++)
                {
                    lods[index].screenRelativeTransitionHeight =
                        lods.Length == 1
                            ? 0.0035f
                            : transitionProfile[
                                Math.Min(
                                    index,
                                    transitionProfile.Length - 1)];
                    lods[index].fadeTransitionWidth = 0.18f;
                    Renderer[] lodRenderers = lods[index].renderers;
                    for (int rendererIndex = 0;
                         rendererIndex < lodRenderers.Length;
                         rendererIndex++)
                    {
                        Renderer renderer = lodRenderers[rendererIndex];
                        if (renderer != null)
                        {
                            renderer.shadowCastingMode =
                                castShadows && index <= 1
                                    ? ShadowCastingMode.On
                                    : ShadowCastingMode.Off;
                        }
                    }
                }
                group.SetLODs(lods);
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = false;
                group.RecalculateBounds();
            }
        }

        private static bool IsGeneratedWindSpruceName(string name)
        {
            return name.StartsWith(
                       "NorwaySpruce_",
                       StringComparison.Ordinal) ||
                   name.StartsWith(
                       "EngelmannSpruce_",
                       StringComparison.Ordinal);
        }

        private static int DisableReplacedLegacyPresentation(
            IEnumerable<DonorWorldBaselineEntityMetadata> metadata,
            Transform betterMscRoot,
            Transform generatedForestRoot,
            out int preservedMapBoundaryColliderCount)
        {
            int disabledColliderCount = 0;
            var processedColliders = new HashSet<Collider>();
            var preservedMapBoundaryColliders = new HashSet<Collider>();
            foreach (DonorWorldBaselineEntityMetadata item in metadata)
            {
                string path =
                    item.SourceHierarchyPath ?? string.Empty;
                if (!IsReplaceableVegetation(item, path))
                {
                    continue;
                }
                foreach (Renderer renderer in
                         item.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = false;
                    EditorUtility.SetDirty(renderer);
                }
                foreach (Collider collider in
                         item.GetComponentsInChildren<Collider>(true))
                {
                    if (!processedColliders.Add(collider) ||
                        collider.transform.IsChildOf(generatedForestRoot))
                    {
                        continue;
                    }
                    if (IsApprovedMapBoundaryCollider(item, collider))
                    {
                        collider.enabled = true;
                        preservedMapBoundaryColliders.Add(collider);
                        EditorUtility.SetDirty(collider);
                        continue;
                    }
                    if (!collider.enabled)
                    {
                        continue;
                    }
                    collider.enabled = false;
                    disabledColliderCount++;
                    EditorUtility.SetDirty(collider);
                }
            }

            Transform detailed =
                betterMscRoot.Find("Detailed Forest Replacement");
            if (detailed == null)
            {
                preservedMapBoundaryColliderCount =
                    preservedMapBoundaryColliders.Count;
                return disabledColliderCount;
            }
            foreach (Renderer renderer in
                     detailed.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
                EditorUtility.SetDirty(renderer);
            }
            foreach (Collider collider in
                     detailed.GetComponentsInChildren<Collider>(true))
            {
                if (!processedColliders.Add(collider) ||
                    !collider.enabled ||
                    collider.transform.IsChildOf(generatedForestRoot))
                {
                    continue;
                }
                collider.enabled = false;
                disabledColliderCount++;
                EditorUtility.SetDirty(collider);
            }
            preservedMapBoundaryColliderCount =
                preservedMapBoundaryColliders.Count;
            return disabledColliderCount;
        }

        private static bool IsApprovedMapBoundaryCollider(
            DonorWorldBaselineEntityMetadata metadata,
            Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            DonorWorldBaselineEntityMetadata owner =
                collider.GetComponentInParent<
                    DonorWorldBaselineEntityMetadata>();
            string ownerPath = owner != null
                ? owner.SourceHierarchyPath ?? string.Empty
                : metadata != null
                    ? metadata.SourceHierarchyPath ?? string.Empty
                    : string.Empty;
            ownerPath = ownerPath.Replace('\\', '/').TrimEnd('/');
            if (ownerPath.EndsWith(
                    ApprovedMapBoundaryColliderPath,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            const string boundaryParentPath =
                "MAP/MESH/FOLIAGE/TREEWALL_LOW";
            return ownerPath.EndsWith(
                       boundaryParentPath,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       collider.name,
                       "treewallcoll",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void Validate(
            GameObject root,
            IEnumerable<DonorWorldBaselineEntityMetadata> metadata,
            int expectedTrees,
            int expectedShrubs,
            int expectedGrassFields,
            int expectedGrassClusters,
            int expectedTrunkColliders,
            int expectedDisabledLegacyVegetationColliders,
            int expectedPreservedMapBoundaryColliders,
            Phase1TreePlacementExclusionIndex exclusionIndex)
        {
            Transform[] generatedTrees = root
                .GetComponentsInChildren<Transform>(true)
                .Where(item => item.name.StartsWith(
                    "Tree_",
                    StringComparison.Ordinal))
                .ToArray();
            int treeCount = generatedTrees.Length;
            int shrubCount = root
                .GetComponentsInChildren<Transform>(true)
                .Count(item => item.name.StartsWith(
                    "Shrub_",
                    StringComparison.Ordinal));
            Transform[] transforms = root
                .GetComponentsInChildren<Transform>(true)
                .ToArray();
            int grassFieldCount = transforms.Count(
                item => item.name.StartsWith(
                    "GrassField_",
                    StringComparison.Ordinal));
            int grassClusterCount = transforms
                .Where(
                    item => item.name.StartsWith(
                        "GrassField_",
                        StringComparison.Ordinal))
                .Sum(
                    item =>
                    {
                        string[] parts = item.name.Split('_');
                        return parts.Length >= 4 &&
                               int.TryParse(
                                   parts[parts.Length - 1],
                                   out int value)
                            ? value
                            : 0;
                    });
            if (treeCount != expectedTrees ||
                shrubCount != expectedShrubs ||
                grassFieldCount != expectedGrassFields ||
                grassClusterCount != expectedGrassClusters)
            {
                throw new InvalidDataException(
                    "Generated vegetation counts do not match the plan.");
            }
            Transform blockedTree = generatedTrees.FirstOrDefault(
                tree => exclusionIndex.Blocks(tree.position));
            if (blockedTree != null)
            {
                throw new InvalidDataException(
                    "Generated tree overlaps an indexed road, building " +
                    "or water exclusion footprint: " +
                    blockedTree.name +
                    " at " +
                    blockedTree.position +
                    ".");
            }

            int lodCount = root
                .GetComponentsInChildren<LODGroup>(true)
                .Length;
            if (lodCount <
                treeCount + shrubCount)
            {
                throw new InvalidDataException(
                    "Generated vegetation is missing LOD groups.");
            }

            CapsuleCollider[] trunkColliders = root
                .GetComponentsInChildren<CapsuleCollider>(true)
                .Where(
                    collider => string.Equals(
                        collider.gameObject.name,
                        "Gameplay Trunk Collider",
                        StringComparison.Ordinal))
                .ToArray();
            if (trunkColliders.Length != expectedTrunkColliders ||
                trunkColliders.Any(
                    collider =>
                        !collider.enabled ||
                        collider.radius < 0.35f ||
                        collider.height < 4f))
            {
                throw new InvalidDataException(
                    "Generated gameplay trunk colliders are invalid.");
            }
            if (expectedTrunkColliders != treeCount)
            {
                throw new InvalidDataException(
                    "Every generated tree must have one visible trunk " +
                    "collider, including boundary trees.");
            }

            Phase1GrassFieldRenderer[] grassFields = root
                .GetComponentsInChildren<
                    Phase1GrassFieldRenderer>(true)
                .ToArray();
            if (grassFields.Length != expectedGrassFields ||
                grassFields.Any(
                    field =>
                        field.enabled ||
                        field.InstanceCount <= 0) ||
                grassFields.Sum(field => field.InstanceCount) !=
                expectedGrassClusters)
            {
                throw new InvalidDataException(
                    "Disabled generated GPU grass field data is invalid.");
            }

            DonorWorldBaselineEntityMetadata[] treeWalls = metadata
                .Where(
                    item =>
                        (item.SourceHierarchyPath ?? string.Empty)
                        .IndexOf(
                            "TREEWALL_",
                            StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            if (treeWalls.Length == 0)
            {
                throw new InvalidDataException(
                    "No legacy tree-wall entities were found.");
            }
            if (treeWalls
                .SelectMany(
                    item => item.GetComponentsInChildren<
                        Renderer>(true))
                .Any(renderer => renderer.enabled))
            {
                throw new InvalidDataException(
                    "A legacy sprite tree-wall renderer remains enabled.");
            }
            var enabledHiddenLegacyVegetationColliders =
                new HashSet<Collider>();
            var enabledMapBoundaryColliders = new HashSet<Collider>();
            foreach (DonorWorldBaselineEntityMetadata item in metadata
                .Where(item => IsReplaceableVegetation(
                    item,
                    item.SourceHierarchyPath ?? string.Empty)))
            {
                foreach (Collider collider in item
                    .GetComponentsInChildren<Collider>(true))
                {
                    if (!collider.enabled ||
                        collider.transform.IsChildOf(root.transform))
                    {
                        continue;
                    }
                    if (IsApprovedMapBoundaryCollider(item, collider))
                    {
                        enabledMapBoundaryColliders.Add(collider);
                    }
                    else
                    {
                        enabledHiddenLegacyVegetationColliders.Add(collider);
                    }
                }
            }
            if (enabledHiddenLegacyVegetationColliders.Count > 0)
            {
                throw new InvalidDataException(
                    "An invisible legacy vegetation collider remains " +
                    "enabled: " +
                    enabledHiddenLegacyVegetationColliders.First().name);
            }
            if (expectedDisabledLegacyVegetationColliders <= 0)
            {
                throw new InvalidDataException(
                    "No superseded invisible vegetation colliders were " +
                    "disabled; the collision migration did not run.");
            }
            if (expectedPreservedMapBoundaryColliders <= 0 ||
                enabledMapBoundaryColliders.Count !=
                expectedPreservedMapBoundaryColliders)
            {
                throw new InvalidDataException(
                    "The approved invisible outer map boundary collider " +
                    "was not preserved.");
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return
                !float.IsNaN(value.x) &&
                !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) &&
                !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) &&
                !float.IsInfinity(value.z);
        }

        private static uint Hash(Vector3 position, uint salt)
        {
            unchecked
            {
                uint hash = 2166136261u ^ salt;
                hash = (hash ^ (uint)Mathf.RoundToInt(position.x * 10f)) *
                       16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(position.y * 10f)) *
                       16777619u;
                hash = (hash ^ (uint)Mathf.RoundToInt(position.z * 10f)) *
                       16777619u;
                hash ^= hash >> 16;
                hash *= 0x7feb352du;
                hash ^= hash >> 15;
                return hash;
            }
        }

        private static float Signed01(uint value)
        {
            return (value & 0xffff) / 32767.5f - 1f;
        }

        private static string StableIdFor(string source)
        {
            using SHA256 algorithm = SHA256.Create();
            byte[] hash = algorithm.ComputeHash(
                Encoding.UTF8.GetBytes(source));
            var builder = new StringBuilder(32);
            for (int index = 0; index < 16; index++)
            {
                builder.Append(hash[index].ToString("x2"));
            }
            return builder.ToString();
        }

        private readonly struct Anchor
        {
            public Anchor(Vector3 position, bool isBoundary)
            {
                Position = position;
                IsBoundary = isBoundary;
            }

            public Vector3 Position { get; }
            public bool IsBoundary { get; }
        }

        private readonly struct SurfaceTriangle
        {
            public SurfaceTriangle(
                Vector3 a,
                Vector3 b,
                Vector3 c,
                float projectedArea,
                int sourceIndex)
            {
                A = a;
                B = b;
                C = c;
                ProjectedArea = projectedArea;
                SourceIndex = sourceIndex;
                Center = (a + b + c) / 3f;
            }

            public Vector3 A { get; }
            public Vector3 B { get; }
            public Vector3 C { get; }
            public Vector3 Center { get; }
            public float ProjectedArea { get; }
            public int SourceIndex { get; }
        }

        private readonly struct GrassFieldBuildResult
        {
            public GrassFieldBuildResult(
                int fieldCount,
                int clusterCount)
            {
                FieldCount = fieldCount;
                ClusterCount = clusterCount;
            }

            public int FieldCount { get; }
            public int ClusterCount { get; }
        }

        private enum VegetationSource
        {
            Tree,
            Shrub,
            Boundary
        }

        private sealed class AnchorAccumulator
        {
            private float sumX;
            private float sumZ;
            private float minimumY = float.PositiveInfinity;

            public AnchorAccumulator(bool boundary)
            {
                Boundary = boundary;
            }

            public int Count { get; private set; }
            public bool Boundary { get; }

            public void Add(Vector3 position)
            {
                sumX += position.x;
                sumZ += position.z;
                minimumY = Mathf.Min(minimumY, position.y);
                Count++;
            }

            public Anchor ToAnchor()
            {
                if (Count <= 0)
                {
                    throw new InvalidOperationException(
                        "Cannot create an anchor from an empty bucket.");
                }
                return new Anchor(
                    new Vector3(
                        sumX / Count,
                        minimumY,
                        sumZ / Count),
                    Boundary);
            }
        }

        private sealed class AnchorProximityIndex
        {
            private readonly float cellSize;
            private readonly Dictionary<GridKey, List<Vector3>> positions =
                new Dictionary<GridKey, List<Vector3>>();

            public AnchorProximityIndex(
                IEnumerable<Anchor> anchors,
                float configuredCellSize)
            {
                cellSize = Mathf.Max(1f, configuredCellSize);
                foreach (Anchor anchor in anchors)
                {
                    Add(anchor.Position);
                }
            }

            public void Add(Vector3 position)
            {
                GridKey key = GridKey.From(position, cellSize);
                if (!positions.TryGetValue(
                        key,
                        out List<Vector3> bucket))
                {
                    bucket = new List<Vector3>();
                    positions.Add(key, bucket);
                }
                bucket.Add(position);
            }

            public bool HasNeighbor(Vector3 position, float radius)
            {
                GridKey center = GridKey.From(position, cellSize);
                int cellRadius = Mathf.CeilToInt(radius / cellSize);
                float radiusSquared = radius * radius;
                for (int x = center.X - cellRadius;
                     x <= center.X + cellRadius;
                     x++)
                {
                    for (int z = center.Z - cellRadius;
                         z <= center.Z + cellRadius;
                         z++)
                    {
                        if (!positions.TryGetValue(
                                new GridKey(x, z),
                                out List<Vector3> bucket))
                        {
                            continue;
                        }
                        for (int index = 0;
                             index < bucket.Count;
                             index++)
                        {
                            Vector3 delta = bucket[index] - position;
                            float planarDistanceSquared =
                                delta.x * delta.x + delta.z * delta.z;
                            if (planarDistanceSquared < radiusSquared)
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
        }

        private sealed class RoadEdgeAccumulator
        {
            public RoadEdgeAccumulator(
                Vector3 a,
                Vector3 b,
                Vector3 interior)
            {
                A = a;
                B = b;
                Interior = interior;
                Count = 1;
            }

            public Vector3 A { get; }
            public Vector3 B { get; }
            public Vector3 Interior { get; }
            public int Count { get; private set; }

            public void Increment()
            {
                Count++;
            }
        }

        private readonly struct RoadVertexKey :
            IEquatable<RoadVertexKey>,
            IComparable<RoadVertexKey>
        {
            private const float Quantization = 20f;

            public RoadVertexKey(int x, int z)
            {
                X = x;
                Z = z;
            }

            public int X { get; }
            public int Z { get; }

            public static RoadVertexKey From(Vector3 value)
            {
                return new RoadVertexKey(
                    Mathf.RoundToInt(value.x * Quantization),
                    Mathf.RoundToInt(value.z * Quantization));
            }

            public int CompareTo(RoadVertexKey other)
            {
                int xComparison = X.CompareTo(other.X);
                return xComparison != 0
                    ? xComparison
                    : Z.CompareTo(other.Z);
            }

            public bool Equals(RoadVertexKey other)
            {
                return X == other.X && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is RoadVertexKey other &&
                       Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (X * 397) ^ Z;
                }
            }
        }

        private readonly struct RoadEdgeKey :
            IEquatable<RoadEdgeKey>
        {
            public RoadEdgeKey(
                RoadVertexKey first,
                RoadVertexKey second)
            {
                First = first;
                Second = second;
            }

            public RoadVertexKey First { get; }
            public RoadVertexKey Second { get; }

            public static RoadEdgeKey From(
                Vector3 a,
                Vector3 b)
            {
                RoadVertexKey first = RoadVertexKey.From(a);
                RoadVertexKey second = RoadVertexKey.From(b);
                return first.CompareTo(second) <= 0
                    ? new RoadEdgeKey(first, second)
                    : new RoadEdgeKey(second, first);
            }

            public bool Equals(RoadEdgeKey other)
            {
                return First.Equals(other.First) &&
                       Second.Equals(other.Second);
            }

            public override bool Equals(object obj)
            {
                return obj is RoadEdgeKey other &&
                       Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (First.GetHashCode() * 397) ^
                           Second.GetHashCode();
                }
            }
        }

        private readonly struct GridKey : IEquatable<GridKey>
        {
            public GridKey(int x, int z)
            {
                X = x;
                Z = z;
            }

            public int X { get; }
            public int Z { get; }

            public static GridKey From(
                Vector3 position,
                float size)
            {
                return new GridKey(
                    Mathf.FloorToInt(position.x / size),
                    Mathf.FloorToInt(position.z / size));
            }

            public bool Equals(GridKey other)
            {
                return X == other.X && Z == other.Z;
            }

            public override bool Equals(object obj)
            {
                return obj is GridKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (X * 397) ^ Z;
                }
            }
        }
    }
}
