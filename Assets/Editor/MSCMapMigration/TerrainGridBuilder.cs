using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;

namespace MSCMapMigration
{
    internal sealed class TerrainBuildResult
    {
        public TerrainGridDomain Domain { get; init; }
        public TerrainSamplingResult Sampling { get; init; } = null!;
        public TerrainMaterialPlan MaterialPlan { get; init; } = null!;
        public int CommittableGroundCount { get; init; }
        public int PreservedResidualTriangleCount { get; init; }
    }

    internal sealed class GroundResidualPlan
    {
        public string RecordId { get; init; } = string.Empty;
        public string HierarchyPath { get; init; } = string.Empty;
        public List<int[]> SubMeshIndices { get; init; } = new List<int[]>();
        public int TriangleCount { get; init; }
    }

    internal static class TerrainGridBuilder
    {
        public static TerrainBuildResult BuildPreview(
            IMapMigrationSettings settings,
            MapScanResult scan)
        {
            ScannedMeshInstance[] ground = scan.Instances
                .Where(instance =>
                    string.Equals(instance.Record.scenePath, settings.SourceScene, StringComparison.Ordinal) &&
                    instance.Record.category == MapMeshCategory.GroundCandidate)
                .ToArray();
            ScannedMeshInstance[] roads = scan.Instances
                .Where(instance =>
                    string.Equals(instance.Record.scenePath, settings.SourceScene, StringComparison.Ordinal) &&
                    (instance.Record.category == MapMeshCategory.RoadAsphalt ||
                     instance.Record.category == MapMeshCategory.RoadDirtOrGravel))
                .ToArray();
            if (ground.Length == 0)
            {
                throw new InvalidDataException("No GroundCandidate renderers were found in the source scene.");
            }

            List<SurfaceTriangle> groundTriangles = TerrainSurfaceSampler.CollectTriangles(
                ground,
                category => category == MapMeshCategory.GroundCandidate,
                minimumAbsoluteNormalY: 0.25f);
            List<SurfaceTriangle> allRoadTriangles = TerrainSurfaceSampler.CollectTriangles(
                roads,
                category => category == MapMeshCategory.RoadAsphalt ||
                            category == MapMeshCategory.RoadDirtOrGravel,
                minimumAbsoluteNormalY: 0.65f);
            TerrainMaterialPlan materialPlan = TerrainMaterialTransfer.Analyze(
                ground,
                settings.MaterialTransferMode);
            TerrainGridDomain domain = TerrainGridDomain.Create(groundTriangles, settings);
            TerrainSamplingResult sampling = TerrainSurfaceSampler.Sample(
                groundTriangles,
                domain);
            float coverage = sampling.CoveredSampleCount /
                             (float)Math.Max(1, domain.TotalSamples - sampling.ExteriorSampleCount);
            if (coverage < settings.MinimumSourceCoverage)
            {
                throw new InvalidDataException(
                    $"Ground coverage {coverage:P3} is below configured minimum {settings.MinimumSourceCoverage:P3}.");
            }

            TerrainHeightSmoother.Smooth(sampling, settings);
            List<GroundResidualPlan> residualPlans = BuildGroundResidualPlans(
                ground,
                sampling,
                maximumHeightError: 1f);
            List<SurfaceTriangle> roadTriangles = RoadConstraintBuilder.FilterRoadBedTriangles(
                allRoadTriangles,
                out int elevatedRoadTriangles);
            sampling.ElevatedRoadTriangleCount = elevatedRoadTriangles;
            RoadConstraintBuilder.Apply(sampling, roadTriangles, settings);
            PopulateGroundCoverage(ground, groundTriangles, sampling, settings);

            if (settings.DryRun)
            {
                Debug.Log(
                    $"MAP_MIGRATION_DRY_RUN terrain={domain.TileCountX}x{domain.TileCountZ} " +
                    $"resolution={domain.HeightmapResolution} triangles={groundTriangles.Count}");
                return new TerrainBuildResult
                {
                    Domain = domain,
                    Sampling = sampling,
                    MaterialPlan = materialPlan,
                    PreservedResidualTriangleCount = residualPlans.Sum(plan => plan.TriangleCount),
                    CommittableGroundCount = ground.Count(instance =>
                        instance.Record.terrainCoveragePassed)
                };
            }

            CreateOutputSceneCopy(settings);
            RecreateGeneratedAssets();
            Scene output = EditorSceneManager.OpenScene(settings.OutputScene, OpenSceneMode.Single);
            GameObject sourceRoot = FindSourceRoot(output, settings.SourceRoot);
            var generatedRoot = new GameObject(MapMigrationPaths.GeneratedRootObjectName);
            generatedRoot.transform.SetParent(sourceRoot.transform, false);
            generatedRoot.AddComponent<MapMigrationGeneratedMarker>()
                .Configure(
                    scan.Inventory.sourceSetFingerprint,
                    domain.TileCountX,
                    domain.TileCountZ,
                    domain.HeightmapResolution,
                    domain.TileSize,
                    isCommitted: false);
            var terrainRoot = new GameObject(MapMigrationPaths.TerrainRootObjectName);
            terrainRoot.transform.SetParent(generatedRoot.transform, false);

            Terrain[,] terrains = CreateTiles(
                terrainRoot.transform,
                domain,
                sampling,
                materialPlan);
            ConnectNeighbors(terrains);
            terrainRoot.AddComponent<MapMigrationTerrainNeighborConnector>()
                .Configure(
                    domain.TileCountX,
                    domain.TileCountZ,
                    FlattenTerrains(terrains));
            CreateGroundResidualMeshes(sourceRoot, residualPlans);
            AddSourceMarkers(sourceRoot, ground, settings.MinimumSourceCoverage);
            WriteGeneratedManifest(
                scan,
                domain,
                sampling,
                materialPlan,
                residualPlans.Sum(plan => plan.TriangleCount));
            EditorSceneManager.MarkSceneDirty(output);
            if (!EditorSceneManager.SaveScene(output, settings.OutputScene))
            {
                throw new IOException("Failed to save generated Terrain migration scene.");
            }

            AssetDatabase.SaveAssets();
            return new TerrainBuildResult
            {
                Domain = domain,
                Sampling = sampling,
                MaterialPlan = materialPlan,
                PreservedResidualTriangleCount = residualPlans.Sum(plan => plan.TriangleCount),
                CommittableGroundCount = ground.Count(instance =>
                    instance.Record.terrainCoveragePassed)
            };
        }

        public static int CommitReplacement(
            IMapMigrationSettings settings,
            MapMigrationValidationReport validation)
        {
            if (validation == null || !validation.passed)
            {
                throw new InvalidOperationException(
                    "Terrain replacement cannot be committed before validation passes.");
            }

            Scene scene = EditorSceneManager.OpenScene(
                settings.OutputScene,
                OpenSceneMode.Single);
            MapMigrationGeneratedMarker generated = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MapMigrationGeneratedMarker>(true))
                .SingleOrDefault();
            if (generated == null)
            {
                throw new InvalidDataException("Generated marker is missing from output scene.");
            }

            int committed = 0;
            foreach (MapMigrationSourceMarker marker in scene
                         .GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<MapMigrationSourceMarker>(true)))
            {
                if (!marker.CoveragePassed)
                {
                    continue;
                }

                MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }

                MeshCollider collider = marker.GetComponent<MeshCollider>();
                if (collider != null)
                {
                    collider.enabled = false;
                }

                committed++;
            }

            foreach (TerrainCollider collider in scene
                         .GetRootGameObjects()
                         .SelectMany(root => root.GetComponentsInChildren<TerrainCollider>(true)))
            {
                collider.enabled = true;
            }

            generated.MarkCommitted();
            EditorUtility.SetDirty(generated);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, settings.OutputScene))
            {
                throw new IOException("Failed to save committed Terrain replacement.");
            }

            AssetDatabase.SaveAssets();
            return committed;
        }

        internal static float CompareVerticalBorder(float[,] left, float[,] right)
        {
            int resolution = left.GetLength(0);
            if (right.GetLength(0) != resolution ||
                left.GetLength(1) != resolution ||
                right.GetLength(1) != resolution)
            {
                throw new ArgumentException("Terrain tile arrays must use the same square resolution.");
            }

            float maximum = 0f;
            for (int z = 0; z < resolution; z++)
            {
                maximum = Mathf.Max(maximum,
                    Mathf.Abs(left[z, resolution - 1] - right[z, 0]));
            }

            return maximum;
        }

        internal static string TileName(int tileX, int tileZ) =>
            $"MapMigration_Terrain_{tileX:000}_{tileZ:000}";

        private static void CreateOutputSceneCopy(IMapMigrationSettings settings)
        {
            MapMigrationPaths.EnsureAssetFolder("Assets/Scenes");
            MapMigrationPaths.EnsureAssetFolder("Assets/Scenes/Generated");
            Scene source = SceneManager.GetSceneByPath(settings.SourceScene);
            if (!source.IsValid() || !source.isLoaded)
            {
                source = EditorSceneManager.OpenScene(settings.SourceScene, OpenSceneMode.Single);
            }

            if (source.isDirty)
            {
                throw new InvalidOperationException(
                    "Source scene is dirty. Migration refuses to save or copy a modified source.");
            }

            string before = MapMigrationPaths.ComputeSha256(settings.SourceScene);
            if (!EditorSceneManager.SaveScene(source, settings.OutputScene, saveAsCopy: true))
            {
                throw new IOException("Failed to create output scene copy.");
            }

            string after = MapMigrationPaths.ComputeSha256(settings.SourceScene);
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                throw new IOException("Source scene hash changed while creating output copy.");
            }
        }

        private static Terrain[,] CreateTiles(
            Transform parent,
            TerrainGridDomain domain,
            TerrainSamplingResult sampling,
            TerrainMaterialPlan materialPlan)
        {
            var terrains = new Terrain[domain.TileCountX, domain.TileCountZ];
            float heightRange = domain.Maximum.y - domain.Minimum.y;
            for (int z = 0; z < domain.TileCountZ; z++)
            {
                for (int x = 0; x < domain.TileCountX; x++)
                {
                    int tileIndex = z * domain.TileCountX + x;
                    MapMigrationProgress.Check(
                        "Build Terrain Preview",
                        $"Creating Terrain tile {tileIndex + 1}/{domain.TileCountX * domain.TileCountZ}",
                        tileIndex / (float)(domain.TileCountX * domain.TileCountZ));
                    var data = new TerrainData
                    {
                        name = TileName(x, z),
                        heightmapResolution = domain.HeightmapResolution,
                        alphamapResolution = Math.Min(512, domain.HeightmapResolution - 1),
                        baseMapResolution = Math.Min(1024, domain.HeightmapResolution - 1),
                        size = new Vector3(domain.TileSize, heightRange, domain.TileSize)
                    };
                    float[,] heights = ExtractTileHeights(sampling, x, z, heightRange);
                    data.SetHeightsDelayLOD(0, 0, heights);
                    data.terrainLayers = new[]
                    {
                        TerrainMaterialTransfer.CreateLayer(materialPlan, domain, x, z)
                    };
                    float[,,] alphamap = new float[data.alphamapResolution,
                        data.alphamapResolution, 1];
                    for (int az = 0; az < data.alphamapResolution; az++)
                    {
                        for (int ax = 0; ax < data.alphamapResolution; ax++)
                        {
                            alphamap[az, ax, 0] = 1f;
                        }
                    }

                    data.SetAlphamaps(0, 0, alphamap);
                    bool[,] holes = ExtractTileVisibility(sampling, x, z);
                    data.SetHoles(0, 0, holes);
                    string assetPath = $"{MapMigrationPaths.TerrainDataRoot}/" +
                                       $"Terrain_{x:000}_{z:000}.asset";
                    AssetDatabase.CreateAsset(data, assetPath);
                    GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
                    terrainObject.name = TileName(x, z).Replace("MapMigration_", string.Empty);
                    terrainObject.transform.SetParent(parent, worldPositionStays: false);
                    terrainObject.transform.position = new Vector3(
                        domain.Minimum.x + x * domain.TileSize,
                        domain.Minimum.y,
                        domain.Minimum.z + z * domain.TileSize);
                    Terrain terrain = terrainObject.GetComponent<Terrain>();
                    terrain.groupingID = 912603;
                    terrain.allowAutoConnect = false;
                    terrain.drawInstanced = true;
                    terrain.heightmapPixelError = 5f;
                    TerrainCollider collider = terrainObject.GetComponent<TerrainCollider>();
                    collider.enabled = false;
                    terrains[x, z] = terrain;
                    data.SyncHeightmap();
                }
            }

            return terrains;
        }

        private static float[,] ExtractTileHeights(
            TerrainSamplingResult sampling,
            int tileX,
            int tileZ,
            float heightRange)
        {
            int resolution = sampling.Domain.HeightmapResolution;
            int segments = resolution - 1;
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                int globalZ = tileZ * segments + z;
                for (int x = 0; x < resolution; x++)
                {
                    int globalX = tileX * segments + x;
                    int index = sampling.Index(globalX, globalZ);
                    float worldHeight = sampling.Valid[index]
                        ? sampling.Heights[index]
                        : sampling.Domain.Minimum.y;
                    heights[z, x] = Mathf.Clamp01(
                        (worldHeight - sampling.Domain.Minimum.y) / heightRange);
                }
            }

            return heights;
        }

        private static bool[,] ExtractTileVisibility(
            TerrainSamplingResult sampling,
            int tileX,
            int tileZ)
        {
            int holesResolution = sampling.Domain.HeightmapResolution - 1;
            int segments = sampling.Domain.HeightmapResolution - 1;
            var visible = new bool[holesResolution, holesResolution];
            for (int z = 0; z < holesResolution; z++)
            {
                int globalZ = tileZ * segments + z;
                for (int x = 0; x < holesResolution; x++)
                {
                    int globalX = tileX * segments + x;
                    visible[z, x] = sampling.Valid[sampling.Index(globalX, globalZ)];
                }
            }

            return visible;
        }

        private static void ConnectNeighbors(Terrain[,] terrains)
        {
            int countX = terrains.GetLength(0);
            int countZ = terrains.GetLength(1);
            for (int z = 0; z < countZ; z++)
            {
                for (int x = 0; x < countX; x++)
                {
                    Terrain left = x > 0 ? terrains[x - 1, z] : null;
                    Terrain right = x + 1 < countX ? terrains[x + 1, z] : null;
                    Terrain bottom = z > 0 ? terrains[x, z - 1] : null;
                    Terrain top = z + 1 < countZ ? terrains[x, z + 1] : null;
                    terrains[x, z].SetNeighbors(left, top, right, bottom);
                }
            }
        }

        private static List<GroundResidualPlan> BuildGroundResidualPlans(
            IReadOnlyList<ScannedMeshInstance> ground,
            TerrainSamplingResult sampling,
            float maximumHeightError)
        {
            var plans = new List<GroundResidualPlan>();
            foreach (ScannedMeshInstance instance in ground)
            {
                ReadableMeshData data = MapMeshDataReader.Read(instance.Mesh);
                var selectedSubMeshes = new List<int[]>(data.TriangleSubMeshes.Count);
                int selectedTriangleCount = 0;
                foreach (int[] indices in data.TriangleSubMeshes)
                {
                    var selected = new List<int>();
                    for (int index = 0; index + 2 < indices.Length; index += 3)
                    {
                        Vector3 a = instance.LocalToWorld.MultiplyPoint3x4(
                            data.Vertices[indices[index]]);
                        Vector3 b = instance.LocalToWorld.MultiplyPoint3x4(
                            data.Vertices[indices[index + 1]]);
                        Vector3 c = instance.LocalToWorld.MultiplyPoint3x4(
                            data.Vertices[indices[index + 2]]);
                        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                        Vector3 point = (a + b + c) / 3f;
                        bool preserve = Mathf.Abs(normal.y) < 0.25f ||
                                        !RoadConstraintBuilder.TrySampleHeightfield(
                                            sampling,
                                            point.x,
                                            point.z,
                                            out float terrainHeight) ||
                                        Mathf.Abs(terrainHeight - point.y) > maximumHeightError;
                        if (!preserve)
                        {
                            continue;
                        }

                        selected.Add(indices[index]);
                        selected.Add(indices[index + 1]);
                        selected.Add(indices[index + 2]);
                        selectedTriangleCount++;
                    }

                    selectedSubMeshes.Add(selected.ToArray());
                }

                if (selectedTriangleCount == 0)
                {
                    continue;
                }

                plans.Add(new GroundResidualPlan
                {
                    RecordId = instance.Record.recordId,
                    HierarchyPath = instance.Record.hierarchyPath,
                    SubMeshIndices = selectedSubMeshes,
                    TriangleCount = selectedTriangleCount
                });
            }

            return plans;
        }

        private static void CreateGroundResidualMeshes(
            GameObject sourceRoot,
            IReadOnlyList<GroundResidualPlan> plans)
        {
            if (plans.Count == 0)
            {
                return;
            }

            Dictionary<string, GroundResidualPlan> byPath = plans.ToDictionary(
                plan => plan.HierarchyPath,
                plan => plan,
                StringComparer.Ordinal);
            foreach (MeshFilter sourceFilter in sourceRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer sourceRenderer = sourceFilter.GetComponent<MeshRenderer>();
                if (sourceRenderer == null || sourceFilter.sharedMesh == null)
                {
                    continue;
                }

                string path = BuildHierarchyPath(sourceRoot.transform, sourceRenderer.transform);
                if (!byPath.TryGetValue(path, out GroundResidualPlan plan))
                {
                    continue;
                }

                Mesh residualMesh = CreateResidualMesh(
                    sourceFilter.sharedMesh,
                    plan.SubMeshIndices,
                    plan.RecordId);
                string assetPath = $"{MapMigrationPaths.ResidualMeshRoot}/" +
                                   $"Residual_{MapMigrationPaths.StableHash(plan.RecordId)}.asset";
                AssetDatabase.CreateAsset(residualMesh, assetPath);

                var residualObject = new GameObject(
                    "MapMigration_Residual_" + MapMigrationPaths.StableHash(plan.RecordId));
                residualObject.layer = sourceRenderer.gameObject.layer;
                residualObject.transform.SetParent(sourceRenderer.transform, false);
                residualObject.AddComponent<MeshFilter>().sharedMesh = residualMesh;
                MeshRenderer residualRenderer = residualObject.AddComponent<MeshRenderer>();
                residualRenderer.sharedMaterials = sourceRenderer.sharedMaterials;
                residualRenderer.shadowCastingMode = sourceRenderer.shadowCastingMode;
                residualRenderer.receiveShadows = sourceRenderer.receiveShadows;
                residualRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
                residualRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
                residualRenderer.motionVectorGenerationMode = sourceRenderer.motionVectorGenerationMode;
                MeshCollider sourceCollider = sourceRenderer.GetComponent<MeshCollider>();
                if (sourceCollider != null && sourceCollider.enabled)
                {
                    MeshCollider residualCollider = residualObject.AddComponent<MeshCollider>();
                    residualCollider.sharedMesh = residualMesh;
                    residualCollider.convex = false;
                }

                residualObject.AddComponent<MapMigrationTerrainResidualMarker>()
                    .Configure(plan.RecordId, plan.TriangleCount);
            }
        }

        private static Mesh CreateResidualMesh(
            Mesh source,
            IReadOnlyList<int[]> selectedSubMeshes,
            string recordId)
        {
            Mesh.MeshDataArray readOnly = Mesh.AcquireReadOnlyMeshData(source);
            Mesh.MeshDataArray writable = Mesh.AllocateWritableMeshData(1);
            bool applied = false;
            try
            {
                Mesh.MeshData input = readOnly[0];
                Mesh.MeshData output = writable[0];
                output.SetVertexBufferParams(input.vertexCount, source.GetVertexAttributes());
                for (int stream = 0; stream < input.vertexBufferCount; stream++)
                {
                    NativeArray<byte> sourceBytes = input.GetVertexData<byte>(stream);
                    NativeArray<byte> destinationBytes = output.GetVertexData<byte>(stream);
                    NativeArray<byte>.Copy(sourceBytes, destinationBytes);
                }

                int totalIndices = selectedSubMeshes.Sum(indices => indices.Length);
                output.SetIndexBufferParams(totalIndices, IndexFormat.UInt32);
                NativeArray<uint> destinationIndices = output.GetIndexData<uint>();
                int offset = 0;
                output.subMeshCount = selectedSubMeshes.Count;
                for (int subMesh = 0; subMesh < selectedSubMeshes.Count; subMesh++)
                {
                    int[] indices = selectedSubMeshes[subMesh];
                    for (int index = 0; index < indices.Length; index++)
                    {
                        destinationIndices[offset + index] = (uint)indices[index];
                    }

                    output.SetSubMesh(
                        subMesh,
                        new SubMeshDescriptor(offset, indices.Length, MeshTopology.Triangles)
                        {
                            baseVertex = 0,
                            firstVertex = 0,
                            vertexCount = input.vertexCount,
                            bounds = source.bounds
                        },
                        MeshUpdateFlags.DontRecalculateBounds |
                        MeshUpdateFlags.DontValidateIndices |
                        MeshUpdateFlags.DontNotifyMeshUsers);
                    offset += indices.Length;
                }

                var residual = new Mesh
                {
                    name = "MapMigration_Residual_" + MapMigrationPaths.StableHash(recordId)
                };
                Mesh.ApplyAndDisposeWritableMeshData(
                    writable,
                    residual,
                    MeshUpdateFlags.DontRecalculateBounds |
                    MeshUpdateFlags.DontValidateIndices |
                    MeshUpdateFlags.DontNotifyMeshUsers);
                applied = true;
                residual.bounds = source.bounds;
                return residual;
            }
            finally
            {
                readOnly.Dispose();
                if (!applied)
                {
                    writable.Dispose();
                }
            }
        }

        private static Terrain[] FlattenTerrains(Terrain[,] terrains)
        {
            int countX = terrains.GetLength(0);
            int countZ = terrains.GetLength(1);
            var flattened = new Terrain[countX * countZ];
            for (int z = 0; z < countZ; z++)
            {
                for (int x = 0; x < countX; x++)
                {
                    flattened[z * countX + x] = terrains[x, z];
                }
            }

            return flattened;
        }

        private static void AddSourceMarkers(
            GameObject sourceRoot,
            IReadOnlyList<ScannedMeshInstance> ground,
            float minimumCoverage)
        {
            Dictionary<string, ScannedMeshInstance> byPath = ground
                .GroupBy(instance => instance.Record.hierarchyPath)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            foreach (MeshRenderer renderer in sourceRoot.GetComponentsInChildren<MeshRenderer>(true))
            {
                string path = BuildHierarchyPath(sourceRoot.transform, renderer.transform);
                if (!byPath.TryGetValue(path, out ScannedMeshInstance source))
                {
                    continue;
                }

                MeshCollider collider = renderer.GetComponent<MeshCollider>();
                MapMigrationSourceMarker marker =
                    renderer.GetComponent<MapMigrationSourceMarker>() ??
                    renderer.gameObject.AddComponent<MapMigrationSourceMarker>();
                marker.Configure(
                    source.Record.recordId,
                    source.Record.terrainCoverageRatio,
                    source.Record.terrainCoverageRatio >= minimumCoverage,
                    renderer.enabled,
                    collider != null && collider.enabled);
            }
        }

        private static void PopulateGroundCoverage(
            IReadOnlyList<ScannedMeshInstance> ground,
            IReadOnlyList<SurfaceTriangle> acceptedTriangles,
            TerrainSamplingResult sampling,
            IMapMigrationSettings settings)
        {
            Dictionary<string, int> accepted = acceptedTriangles
                .GroupBy(triangle => triangle.RecordId)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
            foreach (ScannedMeshInstance instance in ground)
            {
                accepted.TryGetValue(instance.Record.recordId, out int acceptedCount);
                float representableRatio = instance.Record.triangleCount > 0
                    ? acceptedCount / (float)instance.Record.triangleCount
                    : 0f;
                float coverage = Mathf.Clamp01(representableRatio);
                instance.Record.terrainCoverageRatio = coverage;
                instance.Record.terrainCoveragePassed =
                    coverage >= settings.MinimumSourceCoverage;
            }
        }

        private static GameObject FindSourceRoot(Scene scene, string configuredRoot)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform result = FindRecursive(root.transform, configuredRoot);
                if (result != null)
                {
                    return result.gameObject;
                }
            }

            throw new InvalidDataException(
                $"Source root '{configuredRoot}' was not found in output scene.");
        }

        private static Transform FindRecursive(Transform current, string target)
        {
            if (string.Equals(current.name, target, StringComparison.Ordinal) ||
                string.Equals(current.name, Path.GetFileName(target), StringComparison.Ordinal))
            {
                return current;
            }

            foreach (Transform child in current)
            {
                Transform found = FindRecursive(child, target);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static string BuildHierarchyPath(Transform root, Transform current)
        {
            var segments = new Stack<string>();
            Transform cursor = current;
            while (cursor != null)
            {
                segments.Push(cursor.name);
                if (cursor == root)
                {
                    break;
                }

                cursor = cursor.parent;
            }

            return string.Join("/", segments);
        }

        private static void RecreateGeneratedAssets()
        {
            foreach (string path in new[]
                     {
                         MapMigrationPaths.TerrainDataRoot,
                         MapMigrationPaths.TerrainLayerRoot,
                         MapMigrationPaths.ResidualMeshRoot
                     })
            {
                if (AssetDatabase.IsValidFolder(path))
                {
                    AssetDatabase.DeleteAsset(path);
                }

                MapMigrationPaths.EnsureAssetFolder(path);
            }
        }

        private static void WriteGeneratedManifest(
            MapScanResult scan,
            TerrainGridDomain domain,
            TerrainSamplingResult sampling,
            TerrainMaterialPlan materialPlan,
            int residualTriangleCount)
        {
            string json = "{\n" +
                          $"  \"schemaVersion\": \"1.1\",\n" +
                          $"  \"sourceFingerprint\": \"{scan.Inventory.sourceSetFingerprint}\",\n" +
                          $"  \"tileCountX\": {domain.TileCountX},\n" +
                          $"  \"tileCountZ\": {domain.TileCountZ},\n" +
                          $"  \"heightmapResolution\": {domain.HeightmapResolution},\n" +
                          $"  \"tileSize\": {domain.TileSize.ToString(System.Globalization.CultureInfo.InvariantCulture)},\n" +
                          $"  \"coveredSamples\": {sampling.CoveredSampleCount},\n" +
                          $"  \"interiorFilledSamples\": {sampling.InteriorFilledCount},\n" +
                          $"  \"exteriorHoleSamples\": {sampling.ExteriorSampleCount},\n" +
                          $"  \"elevatedRoadTriangles\": {sampling.ElevatedRoadTriangleCount},\n" +
                          $"  \"residualGroundTriangles\": {residualTriangleCount},\n" +
                          $"  \"materialTransfer\": \"{EscapeJson(materialPlan.Description)}\",\n" +
                          $"  \"meanSmoothingDisplacement\": {sampling.MeanSmoothingDisplacement.ToString(System.Globalization.CultureInfo.InvariantCulture)},\n" +
                          $"  \"medianSmoothingDisplacement\": {sampling.MedianSmoothingDisplacement.ToString(System.Globalization.CultureInfo.InvariantCulture)},\n" +
                          $"  \"p95SmoothingDisplacement\": {sampling.P95SmoothingDisplacement.ToString(System.Globalization.CultureInfo.InvariantCulture)},\n" +
                          $"  \"maximumSmoothingDisplacement\": {sampling.MaximumSmoothingDisplacement.ToString(System.Globalization.CultureInfo.InvariantCulture)},\n" +
                          $"  \"smoothingLimitHitCount\": {sampling.SmoothingLimitHitCount}\n" +
                          "}\n";
            File.WriteAllText(
                MapMigrationPaths.ToAbsoluteProjectPath(MapMigrationPaths.ToolManifest),
                json,
                new UTF8Encoding(false));
        }

        private static string EscapeJson(string value) =>
            (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
