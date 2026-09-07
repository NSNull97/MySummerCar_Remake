using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.LegacyImport;
using MSC.World.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldBaseline
{
    public static class Phase1UnifiedTerrainPilotBuilder
    {
        private const string RootName =
            "PHASE1_UNIFIED_EDITABLE_TERRAIN_PILOT";
        private const string ContentRootName =
            "TEMPORARY_DIRECT_IMPORT_ENTITIES";
        private const string GlobalSceneRootName =
            "World_Global_Legacy";
        private const string BetterMscMissingTerrainPath =
            "BetterMSC/MissingTerrain";
        private const string GeneratedRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/" +
            "World/TerrainPilot";
        private const string TerrainDataRoot =
            GeneratedRoot + "/TerrainData";
        private const string TerrainLayerRoot =
            GeneratedRoot + "/Layers";
        private const string TerrainTextureRoot =
            GeneratedRoot + "/Textures";
        private const int TileSizeMeters = 1024;
        private const int HeightmapResolution = 1025;
        private const int DetailResolution = 1024;
        private const int DetailResolutionPerPatch = 32;
        private const int AlphamapResolution = 256;
        private const int SurfaceTextureResolution = 256;
        private const float RoadClearanceMeters = 0.04f;
        private const float MaximumRoadConstraintDeltaMeters = 2.5f;
        private const float MinimumGroundNormalY = 0.25f;
        private const float MinimumRoadNormalY = 0.65f;
        private const float HeightMarginMeters = 16f;
        private const float SmoothDifferenceLimitMeters = 1.5f;
        private const float SmoothBlend = 0.32f;
        private const int TerrainGroupingId = 9041975;

        private static readonly HashSet<string> BaseGroundPaths =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "MAP/MESH/TERRAIN_OBJ/Grass1",
                "MAP/MESH/TERRAIN_OBJ/Grass2",
                "MAP/MESH/TERRAIN_OBJ/Fields",
                "MAP/MESH/TERRAIN_OBJ/4tie",
                "MAP/MESH/TERRAINOUT",
                "MAP/MESH/LAKEBED"
            };

        private static readonly HashSet<string> RoadBedPaths =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "MAP/MESH/TERRAIN_OBJ/Asphalt",
                "MAP/MESH/TERRAIN_OBJ/DirtRoad",
                "MAP/MESH/TERRAIN_OBJ/Gravel",
                "MAP/MESH/TERRAIN_OBJ/Pavement",
                "MAP/MESH/TERRAIN_OBJ/Road",
                "MAP/MESH/TERRAIN_OBJ/Roadside"
            };

        [MenuItem(
            "Tools/MSC Remake/World Baseline/" +
            "Build Phase 1 Unified Editable Terrain Pilot")]
        public static void BuildFromMenu()
        {
            Build();
        }

        [MenuItem(
            "Tools/MSC Remake/World Baseline/" +
            "Remove Phase 1 Unified Editable Terrain Pilot")]
        public static void RemoveFromMenu()
        {
            Scene scene = EditorSceneManager.OpenScene(
                WorldBaseline06B2Paths.GlobalScene,
                OpenSceneMode.Single);
            GameObject contentRoot = FindContentRoot(scene);
            RemoveExistingPilot(contentRoot);
            SetSourceGroundRenderers(contentRoot, true);
            SetGeneratedGrassEnabled(contentRoot, true);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(
                    scene,
                    WorldBaseline06B2Paths.GlobalScene))
            {
                throw new IOException(
                    "Failed to save removal of unified terrain pilot.");
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem(
            "Tools/MSC Remake/World Baseline/" +
            "Return To Legacy Meshes Without Generated Grass")]
        public static void ReturnToLegacyMeshesWithoutGrassFromMenu()
        {
            ReturnToLegacyMeshesWithoutGrass();
        }

        [MenuItem(
            "Tools/MSC Remake/World Baseline/" +
            "Validate Phase 1 Unified Editable Terrain Pilot")]
        public static void ValidateFromMenu()
        {
            ValidateExistingPilot();
        }

        public static void RunBatch()
        {
            Build();
        }

        public static void RunValidationBatch()
        {
            ValidateExistingPilot();
        }

        public static void RunReturnToLegacyMeshesWithoutGrassBatch()
        {
            ReturnToLegacyMeshesWithoutGrass();
        }

        public static void Build()
        {
            Scene scene = EditorSceneManager.OpenScene(
                WorldBaseline06B2Paths.GlobalScene,
                OpenSceneMode.Single);
            GameObject contentRoot = FindContentRoot(scene);
            RemoveExistingPilot(contentRoot);

            List<HeightSource> sources =
                CollectHeightSources(contentRoot);
            if (sources.Count < 6)
            {
                throw new InvalidDataException(
                    "Unified terrain pilot found too few height sources: " +
                    sources.Count + ".");
            }

            Bounds sourceBounds = CalculateBounds(sources);
            RasterDomain domain = RasterDomain.Create(
                sourceBounds,
                TileSizeMeters,
                HeightmapResolution);
            RasterResult raster = Rasterize(sources, domain);
            FillMissingHeights(raster, domain);
            SmoothHeights(raster, domain);
            ApplyRoadSurfaceConstraints(sources, raster, domain);

            RecreateGeneratedFolder();
            TerrainLayer[] layers = CreateTerrainLayers();
            var root = new GameObject(RootName);
            root.transform.SetParent(contentRoot.transform, false);

            Terrain[,] terrains = CreateTerrainTiles(
                root.transform,
                domain,
                raster,
                layers);
            ConnectTerrainTiles(terrains);
            SetSourceGroundRenderers(contentRoot, false);
            SetGeneratedGrassEnabled(contentRoot, false);
            Validate(terrains, domain, raster);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(
                    scene,
                    WorldBaseline06B2Paths.GlobalScene))
            {
                throw new IOException(
                    "Failed to save unified editable terrain pilot.");
            }
            AssetDatabase.SaveAssets();

            Debug.Log(
                "PHASE1_UNIFIED_TERRAIN_PILOT_OK " +
                $"sources={sources.Count} triangles={raster.TriangleCount} " +
                $"tiles={domain.TileCountX * domain.TileCountZ} " +
                $"tileGrid={domain.TileCountX}x{domain.TileCountZ} " +
                $"samples={domain.SampleCountX}x{domain.SampleCountZ} " +
                $"coveredSamples={raster.CoveredSampleCount} " +
                $"interiorFilled={raster.InteriorFilledCount} " +
                $"exteriorSamples={raster.ExteriorSampleCount} " +
                $"roadConstraints={raster.RoadConstraintCount} " +
                $"roadConstraintsRejected={raster.RejectedRoadConstraintCount} " +
                $"boundsMin={domain.Minimum} boundsMax={domain.Maximum} " +
                "legacyColliders=preserved terrainColliders=disabled " +
                "proceduralGrass=disabled editableDetails=enabled");
        }

        private static void ReturnToLegacyMeshesWithoutGrass()
        {
            Scene scene = EditorSceneManager.OpenScene(
                WorldBaseline06B2Paths.GlobalScene,
                OpenSceneMode.Single);
            GameObject contentRoot = FindContentRoot(scene);

            RemoveExistingPilot(contentRoot);
            SetSourceGroundRenderers(contentRoot, true);
            SetGeneratedGrassEnabled(contentRoot, false);
            ValidateLegacyMeshesWithoutGrass(contentRoot);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(
                    scene,
                    WorldBaseline06B2Paths.GlobalScene))
            {
                throw new IOException(
                    "Failed to save legacy-mesh world without grass.");
            }
            AssetDatabase.SaveAssets();

            int groundRendererCount =
                CountSourceGroundRenderers(contentRoot);
            int grassFieldCount = contentRoot
                .GetComponentsInChildren<Phase1GrassFieldRenderer>(true)
                .Length;
            Debug.Log(
                "PHASE1_LEGACY_MESHES_WITHOUT_GRASS_OK " +
                $"groundRenderers={groundRendererCount} " +
                $"grassFieldsDisabled={grassFieldCount} " +
                "terrainPilot=removed trees=preserved");
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
            Transform content =
                sceneRoot != null
                    ? sceneRoot.transform.Find(ContentRootName)
                    : null;
            if (content == null)
            {
                throw new InvalidDataException(
                    "Global donor content root is missing.");
            }
            return content.gameObject;
        }

        private static void ValidateLegacyMeshesWithoutGrass(
            GameObject contentRoot)
        {
            if (contentRoot.transform.Find(RootName) != null)
            {
                throw new InvalidDataException(
                    "Unified terrain pilot still exists.");
            }

            int totalGroundRenderers = 0;
            int enabledGroundRenderers = 0;
            foreach (DonorWorldBaselineEntityMetadata metadata in
                     contentRoot.GetComponentsInChildren<
                         DonorWorldBaselineEntityMetadata>(true))
            {
                string path =
                    metadata.SourceHierarchyPath ?? string.Empty;
                if (!BaseGroundPaths.Contains(path) &&
                    !string.Equals(
                        path,
                        BetterMscMissingTerrainPath,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Renderer renderer in
                         metadata.GetComponentsInChildren<Renderer>(true))
                {
                    totalGroundRenderers++;
                    if (renderer.enabled)
                    {
                        enabledGroundRenderers++;
                    }
                }
            }

            if (totalGroundRenderers == 0 ||
                enabledGroundRenderers != totalGroundRenderers)
            {
                throw new InvalidDataException(
                    "Legacy ground renderers were not fully restored: " +
                    $"{enabledGroundRenderers}/{totalGroundRenderers}.");
            }

            int enabledGrassFields = contentRoot
                .GetComponentsInChildren<Phase1GrassFieldRenderer>(true)
                .Count(renderer => renderer.enabled);
            if (enabledGrassFields != 0)
            {
                throw new InvalidDataException(
                    "Generated grass fields are still enabled: " +
                    enabledGrassFields + ".");
            }
        }

        private static int CountSourceGroundRenderers(
            GameObject contentRoot)
        {
            int count = 0;
            foreach (DonorWorldBaselineEntityMetadata metadata in
                     contentRoot.GetComponentsInChildren<
                         DonorWorldBaselineEntityMetadata>(true))
            {
                string path =
                    metadata.SourceHierarchyPath ?? string.Empty;
                if (!BaseGroundPaths.Contains(path) &&
                    !string.Equals(
                        path,
                        BetterMscMissingTerrainPath,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                count += metadata
                    .GetComponentsInChildren<Renderer>(true)
                    .Length;
            }
            return count;
        }

        private static void ValidateExistingPilot()
        {
            Scene scene = EditorSceneManager.OpenScene(
                WorldBaseline06B2Paths.GlobalScene,
                OpenSceneMode.Single);
            GameObject contentRoot = FindContentRoot(scene);
            Transform pilotRoot =
                contentRoot.transform.Find(RootName);
            if (pilotRoot == null)
            {
                throw new InvalidDataException(
                    "Unified editable terrain pilot root is missing.");
            }

            Terrain[] terrains =
                pilotRoot.GetComponentsInChildren<Terrain>(true);
            if (terrains.Length == 0)
            {
                throw new InvalidDataException(
                    "Unified editable terrain pilot contains no tiles.");
            }

            var byGrid =
                new Dictionary<Vector2Int, Terrain>();
            foreach (Terrain terrain in terrains)
            {
                ValidateTerrainConfiguration(terrain);
                Vector3 position = terrain.transform.position;
                var key = new Vector2Int(
                    Mathf.RoundToInt(position.x / TileSizeMeters),
                    Mathf.RoundToInt(position.z / TileSizeMeters));
                if (!byGrid.TryAdd(key, terrain))
                {
                    throw new InvalidDataException(
                        "Duplicate editable terrain tile grid key " +
                        key + ".");
                }
            }

            float maximumNormalizedSeamError = 0f;
            int comparedSeams = 0;
            foreach (KeyValuePair<Vector2Int, Terrain> pair in byGrid)
            {
                Vector2Int rightKey =
                    pair.Key + Vector2Int.right;
                if (byGrid.TryGetValue(
                        rightKey,
                        out Terrain right))
                {
                    maximumNormalizedSeamError = Mathf.Max(
                        maximumNormalizedSeamError,
                        CompareVerticalSeam(pair.Value, right));
                    comparedSeams++;
                }

                Vector2Int topKey =
                    pair.Key + Vector2Int.up;
                if (byGrid.TryGetValue(
                        topKey,
                        out Terrain top))
                {
                    maximumNormalizedSeamError = Mathf.Max(
                        maximumNormalizedSeamError,
                        CompareHorizontalSeam(pair.Value, top));
                    comparedSeams++;
                }
            }
            if (comparedSeams == 0 ||
                maximumNormalizedSeamError > 0.000001f)
            {
                throw new InvalidDataException(
                    "Editable terrain seams are invalid: compared=" +
                    comparedSeams + " maxNormalizedError=" +
                    maximumNormalizedSeamError + ".");
            }

            int visibleLegacyGroundRenderers =
                CountVisibleSourceGroundRenderers(contentRoot);
            if (visibleLegacyGroundRenderers != 0)
            {
                throw new InvalidDataException(
                    "Legacy ground remains visible under the terrain " +
                    "pilot: " + visibleLegacyGroundRenderers + ".");
            }

            int enabledProceduralGrass =
                contentRoot.GetComponentsInChildren<
                        Phase1GrassFieldRenderer>(true)
                    .Count(renderer => renderer.enabled);
            if (enabledProceduralGrass != 0)
            {
                throw new InvalidDataException(
                    "Procedural grass must be disabled for editable " +
                    "terrain validation.");
            }

            Debug.Log(
                "PHASE1_UNIFIED_TERRAIN_PILOT_VALIDATION_PASS " +
                $"tiles={terrains.Length} seams={comparedSeams} " +
                "maxNormalizedSeamError=" +
                maximumNormalizedSeamError.ToString("R") + " " +
                "legacyGroundVisible=0 proceduralGrassEnabled=0 " +
                "terrainColliders=disabled");
        }

        private static List<HeightSource> CollectHeightSources(
            GameObject contentRoot)
        {
            var result = new List<HeightSource>();
            foreach (DonorWorldBaselineEntityMetadata metadata in
                     contentRoot.GetComponentsInChildren<
                         DonorWorldBaselineEntityMetadata>(true))
            {
                string path =
                    metadata.SourceHierarchyPath ?? string.Empty;
                bool baseGround =
                    BaseGroundPaths.Contains(path) ||
                    string.Equals(
                        path,
                        BetterMscMissingTerrainPath,
                        StringComparison.Ordinal);
                bool roadBed = RoadBedPaths.Contains(path);
                if (!baseGround && !roadBed)
                {
                    continue;
                }

                HeightSourceKind kind =
                    roadBed
                        ? HeightSourceKind.RoadConstraint
                        : HeightSourceKind.Ground;
                GroundSurfaceClass surfaceClass =
                    ResolveSurfaceClass(path);
                foreach (MeshFilter filter in
                         metadata.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh != null)
                    {
                        result.Add(
                            new HeightSource(
                                filter,
                                path,
                                kind,
                                surfaceClass));
                    }
                }
            }
            return result;
        }

        private static Bounds CalculateBounds(
            IReadOnlyList<HeightSource> sources)
        {
            bool initialized = false;
            Bounds bounds = default;
            foreach (HeightSource source in sources)
            {
                Renderer renderer =
                    source.Filter.GetComponent<Renderer>();
                Bounds candidate =
                    renderer != null
                        ? renderer.bounds
                        : TransformBounds(
                            source.Filter.sharedMesh.bounds,
                            source.Filter.transform.localToWorldMatrix);
                if (!initialized)
                {
                    bounds = candidate;
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(candidate);
                }
            }
            if (!initialized ||
                bounds.size.x < TileSizeMeters ||
                bounds.size.z < TileSizeMeters)
            {
                throw new InvalidDataException(
                    "Height-source bounds are invalid.");
            }
            return bounds;
        }

        private static RasterResult Rasterize(
            IReadOnlyList<HeightSource> sources,
            RasterDomain domain)
        {
            int sampleCount = checked(
                domain.SampleCountX * domain.SampleCountZ);
            var heights = new float[sampleCount];
            var covered = new bool[sampleCount];
            var surfaceClasses = new byte[sampleCount];
            Array.Fill(heights, float.NaN);
            int triangleCount = 0;
            int coveredCount = 0;

            foreach (HeightSource source in sources)
            {
                if (source.Kind != HeightSourceKind.Ground)
                {
                    continue;
                }
                Mesh mesh = source.Filter.sharedMesh;
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
                        "Skipping non-readable terrain source '" +
                        source.SourcePath + "': " + exception.Message);
                    continue;
                }

                Matrix4x4 matrix =
                    source.Filter.transform.localToWorldMatrix;
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
                    if (!IsFinite(a) ||
                        !IsFinite(b) ||
                        !IsFinite(c))
                    {
                        continue;
                    }

                    Vector3 cross =
                        Vector3.Cross(b - a, c - a);
                    float crossMagnitude = cross.magnitude;
                    if (crossMagnitude < 0.00001f ||
                        Mathf.Abs(cross.y) / crossMagnitude <
                        MinimumGroundNormalY)
                    {
                        continue;
                    }

                    float denominator =
                        (b.z - c.z) * (a.x - c.x) +
                        (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(denominator) < 0.00001f)
                    {
                        continue;
                    }

                    int minimumX = Mathf.Clamp(
                        Mathf.FloorToInt(
                            (Mathf.Min(a.x, Mathf.Min(b.x, c.x)) -
                             domain.Minimum.x) /
                            domain.SampleSpacing),
                        0,
                        domain.SampleCountX - 1);
                    int maximumX = Mathf.Clamp(
                        Mathf.CeilToInt(
                            (Mathf.Max(a.x, Mathf.Max(b.x, c.x)) -
                             domain.Minimum.x) /
                            domain.SampleSpacing),
                        0,
                        domain.SampleCountX - 1);
                    int minimumZ = Mathf.Clamp(
                        Mathf.FloorToInt(
                            (Mathf.Min(a.z, Mathf.Min(b.z, c.z)) -
                             domain.Minimum.z) /
                            domain.SampleSpacing),
                        0,
                        domain.SampleCountZ - 1);
                    int maximumZ = Mathf.Clamp(
                        Mathf.CeilToInt(
                            (Mathf.Max(a.z, Mathf.Max(b.z, c.z)) -
                             domain.Minimum.z) /
                            domain.SampleSpacing),
                        0,
                        domain.SampleCountZ - 1);
                    triangleCount++;

                    for (int z = minimumZ; z <= maximumZ; z++)
                    {
                        float worldZ =
                            domain.Minimum.z +
                            z * domain.SampleSpacing;
                        int row = z * domain.SampleCountX;
                        for (int x = minimumX; x <= maximumX; x++)
                        {
                            float worldX =
                                domain.Minimum.x +
                                x * domain.SampleSpacing;
                            float weightA =
                                ((b.z - c.z) * (worldX - c.x) +
                                 (c.x - b.x) * (worldZ - c.z)) /
                                denominator;
                            float weightB =
                                ((c.z - a.z) * (worldX - c.x) +
                                 (a.x - c.x) * (worldZ - c.z)) /
                                denominator;
                            float weightC =
                                1f - weightA - weightB;
                            const float tolerance = -0.0005f;
                            if (weightA < tolerance ||
                                weightB < tolerance ||
                                weightC < tolerance)
                            {
                                continue;
                            }

                            float height =
                                weightA * a.y +
                                weightB * b.y +
                                weightC * c.y;
                            int sampleIndex = row + x;
                            if (!covered[sampleIndex])
                            {
                                covered[sampleIndex] = true;
                                heights[sampleIndex] = height;
                                surfaceClasses[sampleIndex] =
                                    (byte)source.SurfaceClass;
                                coveredCount++;
                            }
                            else if (height > heights[sampleIndex])
                            {
                                heights[sampleIndex] = height;
                                surfaceClasses[sampleIndex] =
                                    (byte)source.SurfaceClass;
                            }
                        }
                    }
                }
            }
            if (coveredCount < 100000)
            {
                throw new InvalidDataException(
                    "Terrain rasterization produced too little coverage: " +
                    coveredCount + ".");
            }
            return new RasterResult(
                heights,
                covered,
                surfaceClasses,
                new bool[sampleCount],
                triangleCount,
                coveredCount);
        }

        private static void FillMissingHeights(
            RasterResult raster,
            RasterDomain domain)
        {
            int width = domain.SampleCountX;
            int depth = domain.SampleCountZ;
            var exteriorQueue = new Queue<int>();
            for (int x = 0; x < width; x++)
            {
                EnqueueExterior(x, 0, width, raster, exteriorQueue);
                EnqueueExterior(
                    x,
                    depth - 1,
                    width,
                    raster,
                    exteriorQueue);
            }
            for (int z = 1; z < depth - 1; z++)
            {
                EnqueueExterior(0, z, width, raster, exteriorQueue);
                EnqueueExterior(
                    width - 1,
                    z,
                    width,
                    raster,
                    exteriorQueue);
            }
            FloodExterior(raster, width, depth, exteriorQueue);

            var fillQueue = new Queue<int>();
            var assigned = new bool[raster.Heights.Length];
            for (int z = 0; z < depth; z++)
            {
                int row = z * width;
                for (int x = 0; x < width; x++)
                {
                    int index = row + x;
                    if (!raster.Covered[index])
                    {
                        continue;
                    }
                    assigned[index] = true;
                    if (HasMissingNeighbor(
                            x,
                            z,
                            width,
                            depth,
                            raster.Covered))
                    {
                        fillQueue.Enqueue(index);
                    }
                }
            }
            if (fillQueue.Count == 0)
            {
                throw new InvalidDataException(
                    "Terrain raster has no covered/missing boundary.");
            }
            while (fillQueue.Count > 0)
            {
                int index = fillQueue.Dequeue();
                int x = index % width;
                int z = index / width;
                TryPropagateHeight(
                    x - 1,
                    z,
                    index,
                    width,
                    depth,
                    raster,
                    assigned,
                    fillQueue);
                TryPropagateHeight(
                    x + 1,
                    z,
                    index,
                    width,
                    depth,
                    raster,
                    assigned,
                    fillQueue);
                TryPropagateHeight(
                    x,
                    z - 1,
                    index,
                    width,
                    depth,
                    raster,
                    assigned,
                    fillQueue);
                TryPropagateHeight(
                    x,
                    z + 1,
                    index,
                    width,
                    depth,
                    raster,
                    assigned,
                    fillQueue);
            }

            int interiorFilled = 0;
            int exterior = 0;
            for (int index = 0; index < raster.Heights.Length; index++)
            {
                if (raster.Exterior[index])
                {
                    exterior++;
                }
                else if (!raster.Covered[index])
                {
                    interiorFilled++;
                }
            }
            raster.InteriorFilledCount = interiorFilled;
            raster.ExteriorSampleCount = exterior;
        }

        private static void SmoothHeights(
            RasterResult raster,
            RasterDomain domain)
        {
            int width = domain.SampleCountX;
            int depth = domain.SampleCountZ;
            var smoothed = new float[raster.Heights.Length];
            Array.Copy(
                raster.Heights,
                smoothed,
                raster.Heights.Length);
            for (int z = 1; z < depth - 1; z++)
            {
                int row = z * width;
                for (int x = 1; x < width - 1; x++)
                {
                    int index = row + x;
                    if (raster.Exterior[index])
                    {
                        continue;
                    }
                    float center = raster.Heights[index];
                    float sum = center;
                    int count = 1;
                    for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
                    {
                        for (int offsetX = -1;
                             offsetX <= 1;
                             offsetX++)
                        {
                            if (offsetX == 0 && offsetZ == 0)
                            {
                                continue;
                            }
                            int neighbor =
                                index + offsetZ * width + offsetX;
                            if (raster.Exterior[neighbor])
                            {
                                continue;
                            }
                            float value = raster.Heights[neighbor];
                            if (Mathf.Abs(value - center) >
                                SmoothDifferenceLimitMeters)
                            {
                                continue;
                            }
                            sum += value;
                            count++;
                        }
                    }
                    float average = sum / count;
                    smoothed[index] = Mathf.Lerp(
                        center,
                        average,
                        SmoothBlend);
                }
            }
            raster.Heights = smoothed;
        }

        private static void ApplyRoadSurfaceConstraints(
            IReadOnlyList<HeightSource> sources,
            RasterResult raster,
            RasterDomain domain)
        {
            var roadHeights =
                new float[raster.Heights.Length];
            Array.Fill(roadHeights, float.NaN);

            int width = domain.SampleCountX;
            foreach (HeightSource source in sources)
            {
                if (source.Kind != HeightSourceKind.RoadConstraint)
                {
                    continue;
                }

                Mesh mesh = source.Filter.sharedMesh;
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
                        "Skipping non-readable road constraint '" +
                        source.SourcePath + "': " + exception.Message);
                    continue;
                }

                Matrix4x4 matrix =
                    source.Filter.transform.localToWorldMatrix;
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
                    if (!IsFinite(a) ||
                        !IsFinite(b) ||
                        !IsFinite(c))
                    {
                        continue;
                    }

                    Vector3 cross =
                        Vector3.Cross(b - a, c - a);
                    float crossMagnitude = cross.magnitude;
                    if (crossMagnitude < 0.00001f ||
                        Mathf.Abs(cross.y) / crossMagnitude <
                        MinimumRoadNormalY)
                    {
                        continue;
                    }

                    float denominator =
                        (b.z - c.z) * (a.x - c.x) +
                        (c.x - b.x) * (a.z - c.z);
                    if (Mathf.Abs(denominator) < 0.00001f)
                    {
                        continue;
                    }

                    int minimumX = Mathf.Clamp(
                        Mathf.FloorToInt(
                            (Mathf.Min(a.x, Mathf.Min(b.x, c.x)) -
                             domain.Minimum.x) /
                            domain.SampleSpacing),
                        0,
                        domain.SampleCountX - 1);
                    int maximumX = Mathf.Clamp(
                        Mathf.CeilToInt(
                            (Mathf.Max(a.x, Mathf.Max(b.x, c.x)) -
                             domain.Minimum.x) /
                            domain.SampleSpacing),
                        0,
                        domain.SampleCountX - 1);
                    int minimumZ = Mathf.Clamp(
                        Mathf.FloorToInt(
                            (Mathf.Min(a.z, Mathf.Min(b.z, c.z)) -
                             domain.Minimum.z) /
                            domain.SampleSpacing),
                        0,
                        domain.SampleCountZ - 1);
                    int maximumZ = Mathf.Clamp(
                        Mathf.CeilToInt(
                            (Mathf.Max(a.z, Mathf.Max(b.z, c.z)) -
                             domain.Minimum.z) /
                            domain.SampleSpacing),
                        0,
                        domain.SampleCountZ - 1);

                    for (int z = minimumZ; z <= maximumZ; z++)
                    {
                        float worldZ =
                            domain.Minimum.z +
                            z * domain.SampleSpacing;
                        int row = z * width;
                        for (int x = minimumX; x <= maximumX; x++)
                        {
                            float worldX =
                                domain.Minimum.x +
                                x * domain.SampleSpacing;
                            float weightA =
                                ((b.z - c.z) * (worldX - c.x) +
                                 (c.x - b.x) * (worldZ - c.z)) /
                                denominator;
                            float weightB =
                                ((c.z - a.z) * (worldX - c.x) +
                                 (a.x - c.x) * (worldZ - c.z)) /
                                denominator;
                            float weightC =
                                1f - weightA - weightB;
                            const float tolerance = -0.0005f;
                            if (weightA < tolerance ||
                                weightB < tolerance ||
                                weightC < tolerance)
                            {
                                continue;
                            }

                            float height =
                                weightA * a.y +
                                weightB * b.y +
                                weightC * c.y;
                            int sampleIndex = row + x;
                            if (float.IsNaN(roadHeights[sampleIndex]) ||
                                height > roadHeights[sampleIndex])
                            {
                                roadHeights[sampleIndex] = height;
                            }
                        }
                    }
                }
            }

            int applied = 0;
            int rejected = 0;
            for (int index = 0;
                 index < roadHeights.Length;
                 index++)
            {
                float roadHeight = roadHeights[index];
                if (float.IsNaN(roadHeight) ||
                    raster.Exterior[index])
                {
                    continue;
                }

                float delta =
                    roadHeight - raster.Heights[index];
                if (Mathf.Abs(delta) >
                    MaximumRoadConstraintDeltaMeters)
                {
                    rejected++;
                    continue;
                }

                raster.Heights[index] =
                    roadHeight - RoadClearanceMeters;
                applied++;
            }
            raster.RoadConstraintCount = applied;
            raster.RejectedRoadConstraintCount = rejected;
        }

        private static GroundSurfaceClass ResolveSurfaceClass(
            string sourcePath)
        {
            if (string.Equals(
                    sourcePath,
                    "MAP/MESH/TERRAIN_OBJ/Fields",
                    StringComparison.Ordinal) ||
                string.Equals(
                    sourcePath,
                    "MAP/MESH/TERRAIN_OBJ/4tie",
                    StringComparison.Ordinal))
            {
                return GroundSurfaceClass.Meadow;
            }
            if (string.Equals(
                    sourcePath,
                    "MAP/MESH/LAKEBED",
                    StringComparison.Ordinal))
            {
                return GroundSurfaceClass.Soil;
            }
            return GroundSurfaceClass.ForestGround;
        }

        private static Terrain[,] CreateTerrainTiles(
            Transform root,
            RasterDomain domain,
            RasterResult raster,
            TerrainLayer[] layers)
        {
            var terrains = new Terrain[
                domain.TileCountZ,
                domain.TileCountX];
            float heightRange =
                domain.Maximum.y - domain.Minimum.y;
            for (int tileZ = 0;
                 tileZ < domain.TileCountZ;
                 tileZ++)
            {
                for (int tileX = 0;
                     tileX < domain.TileCountX;
                     tileX++)
                {
                    var terrainData = new TerrainData
                    {
                        name =
                            $"Phase1 Editable Terrain {tileX},{tileZ}",
                        heightmapResolution = HeightmapResolution,
                        alphamapResolution = AlphamapResolution,
                        baseMapResolution = 256,
                        size = new Vector3(
                            TileSizeMeters,
                            heightRange,
                            TileSizeMeters),
                        terrainLayers = layers
                    };
                    terrainData.SetDetailResolution(
                        DetailResolution,
                        DetailResolutionPerPatch);
                    float[,] heights = ExtractTileHeights(
                        tileX,
                        tileZ,
                        domain,
                        raster);
                    terrainData.SetHeightsDelayLOD(0, 0, heights);
                    terrainData.SyncHeightmap();
                    bool[,] holes = ExtractTileVisibility(
                        tileX,
                        tileZ,
                        domain,
                        raster);
                    terrainData.SetHoles(0, 0, holes);
                    float[,,] alphamaps =
                        ExtractTileAlphamaps(
                            tileX,
                            tileZ,
                            domain,
                            raster,
                            layers.Length);
                    terrainData.SetAlphamaps(0, 0, alphamaps);

                    string path =
                        $"{TerrainDataRoot}/" +
                        $"Terrain_{tileX:00}_{tileZ:00}.asset";
                    AssetDatabase.CreateAsset(terrainData, path);
                    GameObject terrainObject =
                        Terrain.CreateTerrainGameObject(terrainData);
                    terrainObject.name =
                        $"Editable Terrain [{tileX},{tileZ}]";
                    terrainObject.transform.SetParent(root, true);
                    terrainObject.transform.position = new Vector3(
                        domain.Minimum.x + tileX * TileSizeMeters,
                        domain.Minimum.y,
                        domain.Minimum.z + tileZ * TileSizeMeters);
                    Terrain terrain =
                        terrainObject.GetComponent<Terrain>();
                    terrain.groupingID = TerrainGroupingId;
                    terrain.allowAutoConnect = true;
                    terrain.drawInstanced = true;
                    terrain.heightmapPixelError = 4f;
                    terrain.basemapDistance = 1800f;
                    terrain.shadowCastingMode =
                        ShadowCastingMode.On;
                    TerrainCollider collider =
                        terrainObject.GetComponent<TerrainCollider>();
                    if (collider != null)
                    {
                        collider.enabled = false;
                    }
                    terrains[tileZ, tileX] = terrain;
                }
            }
            return terrains;
        }

        private static float[,] ExtractTileHeights(
            int tileX,
            int tileZ,
            RasterDomain domain,
            RasterResult raster)
        {
            var result = new float[
                HeightmapResolution,
                HeightmapResolution];
            int startX =
                tileX * (HeightmapResolution - 1);
            int startZ =
                tileZ * (HeightmapResolution - 1);
            float heightRange =
                domain.Maximum.y - domain.Minimum.y;
            for (int z = 0; z < HeightmapResolution; z++)
            {
                int globalRow =
                    (startZ + z) * domain.SampleCountX;
                for (int x = 0; x < HeightmapResolution; x++)
                {
                    float worldHeight =
                        raster.Heights[globalRow + startX + x];
                    result[z, x] = Mathf.Clamp01(
                        (worldHeight - domain.Minimum.y) /
                        heightRange);
                }
            }
            return result;
        }

        private static float[,,] ExtractTileAlphamaps(
            int tileX,
            int tileZ,
            RasterDomain domain,
            RasterResult raster,
            int layerCount)
        {
            var result = new float[
                AlphamapResolution,
                AlphamapResolution,
                layerCount];
            int samplesPerTile = HeightmapResolution - 1;
            int startX = tileX * samplesPerTile;
            int startZ = tileZ * samplesPerTile;
            for (int z = 0; z < AlphamapResolution; z++)
            {
                int sampleZ =
                    startZ +
                    Mathf.RoundToInt(
                        z /
                        (float)(AlphamapResolution - 1) *
                        samplesPerTile);
                int row = sampleZ * domain.SampleCountX;
                for (int x = 0; x < AlphamapResolution; x++)
                {
                    int sampleX =
                        startX +
                        Mathf.RoundToInt(
                            x /
                            (float)(AlphamapResolution - 1) *
                            samplesPerTile);
                    int surfaceClass = Mathf.Clamp(
                        raster.SurfaceClasses[row + sampleX],
                        0,
                        layerCount - 1);
                    result[z, x, surfaceClass] = 1f;
                }
            }
            return result;
        }

        private static bool[,] ExtractTileVisibility(
            int tileX,
            int tileZ,
            RasterDomain domain,
            RasterResult raster)
        {
            int resolution = HeightmapResolution - 1;
            var result = new bool[resolution, resolution];
            int startX = tileX * resolution;
            int startZ = tileZ * resolution;
            for (int z = 0; z < resolution; z++)
            {
                int globalZ = startZ + z;
                int row = globalZ * domain.SampleCountX;
                int nextRow =
                    (globalZ + 1) * domain.SampleCountX;
                for (int x = 0; x < resolution; x++)
                {
                    int globalX = startX + x;
                    bool allExterior =
                        raster.Exterior[row + globalX] &&
                        raster.Exterior[row + globalX + 1] &&
                        raster.Exterior[nextRow + globalX] &&
                        raster.Exterior[nextRow + globalX + 1];
                    result[z, x] = !allExterior;
                }
            }
            return result;
        }

        private static void ConnectTerrainTiles(
            Terrain[,] terrains)
        {
            int depth = terrains.GetLength(0);
            int width = terrains.GetLength(1);
            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    Terrain left =
                        x > 0 ? terrains[z, x - 1] : null;
                    Terrain right =
                        x + 1 < width ? terrains[z, x + 1] : null;
                    Terrain top =
                        z + 1 < depth ? terrains[z + 1, x] : null;
                    Terrain bottom =
                        z > 0 ? terrains[z - 1, x] : null;
                    terrains[z, x].SetNeighbors(
                        left,
                        top,
                        right,
                        bottom);
                    terrains[z, x].Flush();
                }
            }
        }

        private static TerrainLayer[] CreateTerrainLayers()
        {
            EnsureAssetFolder(TerrainLayerRoot);
            EnsureAssetFolder(TerrainTextureRoot);

            Texture2D mask = CreateSurfaceMaskTexture();
            Texture2D forest = CreateSurfaceTexture(
                "Phase1_Terrain_ForestGround",
                new Color32(73, 78, 54, 255),
                new Color32(100, 92, 63, 255),
                17.4f);
            Texture2D meadow = CreateSurfaceTexture(
                "Phase1_Terrain_Meadow",
                new Color32(91, 105, 57, 255),
                new Color32(126, 118, 69, 255),
                51.8f);
            Texture2D soil = CreateSurfaceTexture(
                "Phase1_Terrain_Soil",
                new Color32(83, 68, 49, 255),
                new Color32(119, 99, 68, 255),
                93.2f);

            return new[]
            {
                CreateTerrainLayer(
                    "Phase1_Editable_ForestGround",
                    forest,
                    mask,
                    12f),
                CreateTerrainLayer(
                    "Phase1_Editable_Meadow",
                    meadow,
                    mask,
                    10f),
                CreateTerrainLayer(
                    "Phase1_Editable_Soil",
                    soil,
                    mask,
                    8f)
            };
        }

        private static TerrainLayer CreateTerrainLayer(
            string name,
            Texture2D diffuse,
            Texture2D mask,
            float tileSize)
        {
            var layer = new TerrainLayer
            {
                name = name,
                diffuseTexture = diffuse,
                maskMapTexture = mask,
                tileSize = new Vector2(tileSize, tileSize),
                tileOffset = Vector2.zero,
                metallic = 0f,
                smoothness = 0f,
                normalScale = 0f,
                specular = Color.black,
                diffuseRemapMin = Vector4.zero,
                diffuseRemapMax = Vector4.one,
                maskMapRemapMin = Vector4.zero,
                maskMapRemapMax =
                    new Vector4(1f, 1f, 1f, 0.025f),
                smoothnessSource =
                    TerrainLayerSmoothnessSource.ConstantOnly
            };
            string path =
                $"{TerrainLayerRoot}/{name}.terrainlayer";
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        private static Texture2D CreateSurfaceTexture(
            string name,
            Color32 low,
            Color32 high,
            float seed)
        {
            var texture = new Texture2D(
                SurfaceTextureResolution,
                SurfaceTextureResolution,
                TextureFormat.RGBA32,
                true,
                false)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            var pixels = new Color32[
                SurfaceTextureResolution *
                SurfaceTextureResolution];
            for (int y = 0;
                 y < SurfaceTextureResolution;
                 y++)
            {
                for (int x = 0;
                     x < SurfaceTextureResolution;
                     x++)
                {
                    float u =
                        x / (float)SurfaceTextureResolution;
                    float v =
                        y / (float)SurfaceTextureResolution;
                    float broad = Mathf.PerlinNoise(
                        seed + u * 5.1f,
                        seed * 0.71f + v * 5.1f);
                    float fine = Mathf.PerlinNoise(
                        seed * 1.37f + u * 27.3f,
                        seed * 0.43f + v * 27.3f);
                    float blend =
                        Mathf.Clamp01(
                            broad * 0.72f +
                            fine * 0.28f);
                    pixels[y * SurfaceTextureResolution + x] =
                        Color32.Lerp(low, high, blend);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            AssetDatabase.CreateAsset(
                texture,
                $"{TerrainTextureRoot}/{name}.asset");
            return texture;
        }

        private static Texture2D CreateSurfaceMaskTexture()
        {
            const string name =
                "Phase1_Terrain_MatteMask";
            var texture = new Texture2D(
                4,
                4,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };
            var pixels = new Color32[16];
            var matteMask =
                new Color32(0, 255, 128, 0);
            for (int index = 0; index < pixels.Length; index++)
            {
                pixels[index] = matteMask;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            AssetDatabase.CreateAsset(
                texture,
                $"{TerrainTextureRoot}/{name}.asset");
            return texture;
        }

        private static void SetSourceGroundRenderers(
            GameObject contentRoot,
            bool enabled)
        {
            foreach (DonorWorldBaselineEntityMetadata metadata in
                     contentRoot.GetComponentsInChildren<
                         DonorWorldBaselineEntityMetadata>(true))
            {
                string path =
                    metadata.SourceHierarchyPath ?? string.Empty;
                if (!BaseGroundPaths.Contains(path) &&
                    !string.Equals(
                        path,
                        BetterMscMissingTerrainPath,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                foreach (Renderer renderer in
                         metadata.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.enabled = enabled;
                    EditorUtility.SetDirty(renderer);
                }
            }

        }

        private static void SetGeneratedGrassEnabled(
            GameObject contentRoot,
            bool enabled)
        {
            foreach (Phase1GrassFieldRenderer renderer in
                     contentRoot.GetComponentsInChildren<
                         Phase1GrassFieldRenderer>(true))
            {
                renderer.enabled = enabled;
                EditorUtility.SetDirty(renderer);
            }
        }

        private static void RemoveExistingPilot(
            GameObject contentRoot)
        {
            Transform existing =
                contentRoot.transform.Find(RootName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(
                    existing.gameObject);
            }
        }

        private static void RecreateGeneratedFolder()
        {
            if (AssetDatabase.IsValidFolder(GeneratedRoot) &&
                !AssetDatabase.DeleteAsset(GeneratedRoot))
            {
                throw new IOException(
                    "Failed to clear generated terrain-pilot assets.");
            }
            EnsureAssetFolder(TerrainDataRoot);
            EnsureAssetFolder(TerrainLayerRoot);
            EnsureAssetFolder(TerrainTextureRoot);
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
                    AssetDatabase.CreateFolder(
                        current,
                        parts[index]);
                }
                current = next;
            }
        }

        private static void Validate(
            Terrain[,] terrains,
            RasterDomain domain,
            RasterResult raster)
        {
            int terrainCount =
                terrains.GetLength(0) * terrains.GetLength(1);
            if (terrainCount !=
                domain.TileCountX * domain.TileCountZ)
            {
                throw new InvalidDataException(
                    "Generated terrain tile count is invalid.");
            }
            foreach (Terrain terrain in terrains)
            {
                ValidateTerrainConfiguration(terrain);
            }
            if (raster.CoveredSampleCount <= 0 ||
                raster.ExteriorSampleCount <= 0)
            {
                throw new InvalidDataException(
                    "Terrain pilot raster statistics are invalid.");
            }
        }

        private static void ValidateTerrainConfiguration(
            Terrain terrain)
        {
            if (terrain == null ||
                terrain.terrainData == null ||
                terrain.terrainData.heightmapResolution !=
                HeightmapResolution ||
                terrain.terrainData.detailResolution !=
                DetailResolution ||
                terrain.terrainData.terrainLayers == null ||
                terrain.terrainData.terrainLayers.Length !=
                Enum.GetValues(typeof(GroundSurfaceClass)).Length ||
                terrain.terrainData.terrainLayers.Any(
                    layer =>
                        layer == null ||
                        layer.diffuseTexture == null ||
                        layer.maskMapTexture == null ||
                        layer.metallic > 0.001f ||
                        layer.smoothness > 0.001f) ||
                terrain.drawInstanced == false ||
                terrain.groupingID != TerrainGroupingId)
            {
                throw new InvalidDataException(
                    "Generated terrain configuration is invalid.");
            }
            TerrainCollider collider =
                terrain.GetComponent<TerrainCollider>();
            if (collider == null || collider.enabled)
            {
                throw new InvalidDataException(
                    "Terrain pilot must preserve legacy collision " +
                    "authority.");
            }
        }

        private static int CountVisibleSourceGroundRenderers(
            GameObject contentRoot)
        {
            int count = 0;
            foreach (DonorWorldBaselineEntityMetadata metadata in
                     contentRoot.GetComponentsInChildren<
                         DonorWorldBaselineEntityMetadata>(true))
            {
                string path =
                    metadata.SourceHierarchyPath ?? string.Empty;
                if (!BaseGroundPaths.Contains(path) &&
                    !string.Equals(
                        path,
                        BetterMscMissingTerrainPath,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                count += metadata
                    .GetComponentsInChildren<Renderer>(true)
                    .Count(renderer => renderer.enabled);
            }
            return count;
        }

        private static float CompareVerticalSeam(
            Terrain left,
            Terrain right)
        {
            int resolution = HeightmapResolution;
            float[,] leftHeights = left.terrainData.GetHeights(
                resolution - 1,
                0,
                1,
                resolution);
            float[,] rightHeights = right.terrainData.GetHeights(
                0,
                0,
                1,
                resolution);
            float maximum = 0f;
            for (int z = 0; z < resolution; z++)
            {
                maximum = Mathf.Max(
                    maximum,
                    Mathf.Abs(
                        leftHeights[z, 0] -
                        rightHeights[z, 0]));
            }
            return maximum;
        }

        private static float CompareHorizontalSeam(
            Terrain bottom,
            Terrain top)
        {
            int resolution = HeightmapResolution;
            float[,] bottomHeights = bottom.terrainData.GetHeights(
                0,
                resolution - 1,
                resolution,
                1);
            float[,] topHeights = top.terrainData.GetHeights(
                0,
                0,
                resolution,
                1);
            float maximum = 0f;
            for (int x = 0; x < resolution; x++)
            {
                maximum = Mathf.Max(
                    maximum,
                    Mathf.Abs(
                        bottomHeights[0, x] -
                        topHeights[0, x]));
            }
            return maximum;
        }

        private static void EnqueueExterior(
            int x,
            int z,
            int width,
            RasterResult raster,
            Queue<int> queue)
        {
            int index = z * width + x;
            if (raster.Covered[index] ||
                raster.Exterior[index])
            {
                return;
            }
            raster.Exterior[index] = true;
            queue.Enqueue(index);
        }

        private static void FloodExterior(
            RasterResult raster,
            int width,
            int depth,
            Queue<int> queue)
        {
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int z = index / width;
                TryEnqueueExteriorNeighbor(
                    x - 1,
                    z,
                    width,
                    depth,
                    raster,
                    queue);
                TryEnqueueExteriorNeighbor(
                    x + 1,
                    z,
                    width,
                    depth,
                    raster,
                    queue);
                TryEnqueueExteriorNeighbor(
                    x,
                    z - 1,
                    width,
                    depth,
                    raster,
                    queue);
                TryEnqueueExteriorNeighbor(
                    x,
                    z + 1,
                    width,
                    depth,
                    raster,
                    queue);
            }
        }

        private static void TryEnqueueExteriorNeighbor(
            int x,
            int z,
            int width,
            int depth,
            RasterResult raster,
            Queue<int> queue)
        {
            if (x < 0 || x >= width ||
                z < 0 || z >= depth)
            {
                return;
            }
            EnqueueExterior(x, z, width, raster, queue);
        }

        private static void TryPropagateHeight(
            int x,
            int z,
            int sourceIndex,
            int width,
            int depth,
            RasterResult raster,
            bool[] assigned,
            Queue<int> queue)
        {
            if (x < 0 || x >= width ||
                z < 0 || z >= depth)
            {
                return;
            }
            int targetIndex = z * width + x;
            if (assigned[targetIndex])
            {
                return;
            }
            assigned[targetIndex] = true;
            raster.Heights[targetIndex] =
                raster.Heights[sourceIndex];
            raster.SurfaceClasses[targetIndex] =
                raster.SurfaceClasses[sourceIndex];
            queue.Enqueue(targetIndex);
        }

        private static bool HasMissingNeighbor(
            int x,
            int z,
            int width,
            int depth,
            IReadOnlyList<bool> covered)
        {
            return x > 0 && !covered[z * width + x - 1] ||
                   x + 1 < width && !covered[z * width + x + 1] ||
                   z > 0 && !covered[(z - 1) * width + x] ||
                   z + 1 < depth && !covered[(z + 1) * width + x];
        }

        private static Bounds TransformBounds(
            Bounds localBounds,
            Matrix4x4 matrix)
        {
            Vector3 center =
                matrix.MultiplyPoint3x4(localBounds.center);
            Vector3 extents = localBounds.extents;
            Vector3 axisX =
                matrix.MultiplyVector(
                    new Vector3(extents.x, 0f, 0f));
            Vector3 axisY =
                matrix.MultiplyVector(
                    new Vector3(0f, extents.y, 0f));
            Vector3 axisZ =
                matrix.MultiplyVector(
                    new Vector3(0f, 0f, extents.z));
            extents = new Vector3(
                Mathf.Abs(axisX.x) +
                Mathf.Abs(axisY.x) +
                Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) +
                Mathf.Abs(axisY.y) +
                Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) +
                Mathf.Abs(axisY.z) +
                Mathf.Abs(axisZ.z));
            return new Bounds(center, extents * 2f);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) &&
                   !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) &&
                   !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.z);
        }

        private readonly struct HeightSource
        {
            public HeightSource(
                MeshFilter filter,
                string sourcePath,
                HeightSourceKind kind,
                GroundSurfaceClass surfaceClass)
            {
                Filter = filter;
                SourcePath = sourcePath;
                Kind = kind;
                SurfaceClass = surfaceClass;
            }

            public MeshFilter Filter { get; }
            public string SourcePath { get; }
            public HeightSourceKind Kind { get; }
            public GroundSurfaceClass SurfaceClass { get; }
        }

        private enum HeightSourceKind : byte
        {
            Ground = 0,
            RoadConstraint = 1
        }

        private enum GroundSurfaceClass : byte
        {
            ForestGround = 0,
            Meadow = 1,
            Soil = 2
        }

        private readonly struct RasterDomain
        {
            private RasterDomain(
                Vector3 minimum,
                Vector3 maximum,
                int tileCountX,
                int tileCountZ,
                int sampleCountX,
                int sampleCountZ,
                float sampleSpacing)
            {
                Minimum = minimum;
                Maximum = maximum;
                TileCountX = tileCountX;
                TileCountZ = tileCountZ;
                SampleCountX = sampleCountX;
                SampleCountZ = sampleCountZ;
                SampleSpacing = sampleSpacing;
            }

            public Vector3 Minimum { get; }
            public Vector3 Maximum { get; }
            public int TileCountX { get; }
            public int TileCountZ { get; }
            public int SampleCountX { get; }
            public int SampleCountZ { get; }
            public float SampleSpacing { get; }

            public static RasterDomain Create(
                Bounds bounds,
                int tileSize,
                int heightmapResolution)
            {
                float minimumX =
                    Mathf.Floor(bounds.min.x / tileSize) * tileSize;
                float minimumZ =
                    Mathf.Floor(bounds.min.z / tileSize) * tileSize;
                float maximumX =
                    Mathf.Ceil(bounds.max.x / tileSize) * tileSize;
                float maximumZ =
                    Mathf.Ceil(bounds.max.z / tileSize) * tileSize;
                float minimumY =
                    Mathf.Floor(
                        (bounds.min.y - HeightMarginMeters) /
                        16f) * 16f;
                float maximumY =
                    Mathf.Ceil(
                        (bounds.max.y + HeightMarginMeters) /
                        16f) * 16f;
                int tilesX = Mathf.RoundToInt(
                    (maximumX - minimumX) / tileSize);
                int tilesZ = Mathf.RoundToInt(
                    (maximumZ - minimumZ) / tileSize);
                int samplesPerTile = heightmapResolution - 1;
                float spacing =
                    tileSize / (float)samplesPerTile;
                return new RasterDomain(
                    new Vector3(minimumX, minimumY, minimumZ),
                    new Vector3(maximumX, maximumY, maximumZ),
                    tilesX,
                    tilesZ,
                    tilesX * samplesPerTile + 1,
                    tilesZ * samplesPerTile + 1,
                    spacing);
            }
        }

        private sealed class RasterResult
        {
            public RasterResult(
                float[] heights,
                bool[] covered,
                byte[] surfaceClasses,
                bool[] exterior,
                int triangleCount,
                int coveredSampleCount)
            {
                Heights = heights;
                Covered = covered;
                SurfaceClasses = surfaceClasses;
                Exterior = exterior;
                TriangleCount = triangleCount;
                CoveredSampleCount = coveredSampleCount;
            }

            public float[] Heights { get; set; }
            public bool[] Covered { get; }
            public byte[] SurfaceClasses { get; }
            public bool[] Exterior { get; }
            public int TriangleCount { get; }
            public int CoveredSampleCount { get; }
            public int InteriorFilledCount { get; set; }
            public int ExteriorSampleCount { get; set; }
            public int RoadConstraintCount { get; set; }
            public int RejectedRoadConstraintCount { get; set; }
        }
    }
}
