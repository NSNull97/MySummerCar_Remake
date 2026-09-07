using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.LegacyImport;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSCMapMigration
{
    internal static class MapMeshScanner
    {
        private const string BaselineCellFolder =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/" +
            "Streaming/Scenes/Cells";

        public static MapScanResult Scan(IMapMigrationSettings settings)
        {
            List<string> scenes = ResolveSceneSet(settings);
            var opened = new List<string>(scenes.Count);
            var instances = new List<ScannedMeshInstance>(14000);
            var records = new List<MapMeshRecord>(14000);
            var meshStats = new Dictionary<EntityId, MeshGeometryStats>();
            var sourceHashes = new List<string>(scenes.Count);

            for (int sceneIndex = 0; sceneIndex < scenes.Count; sceneIndex++)
            {
                string scenePath = scenes[sceneIndex];
                MapMigrationProgress.Check(
                    "Scan Source Map",
                    $"Opening {Path.GetFileName(scenePath)}",
                    sceneIndex / (float)Math.Max(1, scenes.Count));

                Scene scene = EditorSceneManager.OpenScene(
                    scenePath,
                    sceneIndex == 0 ? OpenSceneMode.Single : OpenSceneMode.Additive);
                opened.Add(scenePath);
                sourceHashes.Add(scenePath + ":" + MapMigrationPaths.ComputeSha256(scenePath));
                GameObject sourceRoot = FindSourceRoot(scene, settings.SourceRoot);
                ScanRoot(scenePath, sourceRoot, instances, records, meshStats);
            }

            PopulateReuseCounts(records);
            records.Sort((left, right) =>
            {
                int sceneComparison = string.CompareOrdinal(left.scenePath, right.scenePath);
                return sceneComparison != 0
                    ? sceneComparison
                    : string.CompareOrdinal(left.hierarchyPath, right.hierarchyPath);
            });
            instances.Sort((left, right) => string.CompareOrdinal(
                left.Record.recordId,
                right.Record.recordId));

            string sourceFingerprint = MapMigrationPaths.StableHash(
                string.Join("\n", sourceHashes) + "\n" +
                string.Join("\n", records.Select(record =>
                    record.recordId + ":" + record.assetGuid + ":" +
                    record.worldMatrix.values.Length.ToString(CultureInfo.InvariantCulture))));

            var inventory = new MapMeshInventory
            {
                generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                sourceScene = settings.SourceScene,
                sourceRoot = settings.SourceRoot,
                sourceSceneSha256 = MapMigrationPaths.ComputeSha256(settings.SourceScene),
                sourceSetFingerprint = sourceFingerprint,
                authoritativeSourceReason = BuildAuthorityReason(settings.SourceScene),
                scannedScenes = scenes,
                records = records,
                summary = CreateSummary(records)
            };

            return new MapScanResult
            {
                Inventory = inventory,
                Instances = instances,
                OpenedScenePaths = opened
            };
        }

        public static void Release(MapScanResult scan)
        {
            if (scan == null)
            {
                return;
            }

            foreach (ScannedMeshInstance instance in scan.Instances)
            {
                if (instance.OwnsMesh && instance.Mesh != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance.Mesh);
                }
            }
        }

        private static List<string> ResolveSceneSet(IMapMigrationSettings settings)
        {
            var paths = new List<string> { settings.SourceScene };
            if (!settings.IncludeSiblingBaselineCells ||
                !string.Equals(
                    settings.SourceScene,
                    MapMigrationSettings.DefaultLegacyGlobalScene,
                    StringComparison.Ordinal))
            {
                return paths;
            }

            string absolute = MapMigrationPaths.ToAbsoluteProjectPath(BaselineCellFolder);
            if (!Directory.Exists(absolute))
            {
                throw new DirectoryNotFoundException(
                    "Baseline cell scene folder is missing: " + BaselineCellFolder);
            }

            paths.AddRange(Directory
                .EnumerateFiles(absolute, "World_Cell_*_Legacy.unity", SearchOption.TopDirectoryOnly)
                .Select(path => MapMigrationPaths.NormalizeRelative(
                    Path.GetRelativePath(MapMigrationPaths.ProjectRoot, path)))
                .OrderBy(path => path, StringComparer.Ordinal));
            return paths;
        }

        private static GameObject FindSourceRoot(Scene scene, string configuredRoot)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            string normalized = (configuredRoot ?? string.Empty)
                .Replace('\\', '/')
                .Trim('/');

            if (!string.IsNullOrEmpty(normalized))
            {
                foreach (GameObject root in roots)
                {
                    Transform found = FindByPathOrLeaf(root.transform, normalized);
                    if (found != null)
                    {
                        return found.gameObject;
                    }
                }
            }

            foreach (string fallback in new[]
                     {
                         "Map2", "TEMPORARY_DIRECT_IMPORT_ENTITIES",
                         "World_Global_Legacy", "GAME", "MAP"
                     })
            {
                foreach (GameObject root in roots)
                {
                    Transform found = FindByPathOrLeaf(root.transform, fallback);
                    if (found != null)
                    {
                        return found.gameObject;
                    }
                }
            }

            if (roots.Length == 1)
            {
                return roots[0];
            }

            throw new InvalidDataException(
                $"Could not resolve source root '{configuredRoot}' in scene '{scene.path}'.");
        }

        private static Transform FindByPathOrLeaf(Transform root, string configured)
        {
            if (string.Equals(root.name, configured, StringComparison.Ordinal) ||
                string.Equals(root.name, Path.GetFileName(configured), StringComparison.Ordinal))
            {
                return root;
            }

            Transform exact = root.Find(configured);
            if (exact != null)
            {
                return exact;
            }

            foreach (Transform child in root)
            {
                Transform found = FindByPathOrLeaf(child, configured);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void ScanRoot(
            string scenePath,
            GameObject root,
            List<ScannedMeshInstance> instances,
            List<MapMeshRecord> records,
            Dictionary<EntityId, MeshGeometryStats> meshStats)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            SkinnedMeshRenderer[] skinned =
                root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            int total = filters.Length + skinned.Length;
            int processed = 0;

            foreach (MeshFilter filter in filters)
            {
                processed++;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null ||
                    !IsSanitizedSourceRenderer(renderer.gameObject))
                {
                    continue;
                }

                AddInstance(
                    scenePath,
                    root.transform,
                    renderer,
                    filter.sharedMesh,
                    ownsMesh: false,
                    instances,
                    records,
                    meshStats);
                if ((processed & 255) == 0)
                {
                    MapMigrationProgress.Check(
                        "Scan Source Map",
                        $"{Path.GetFileName(scenePath)}: {processed}/{total}",
                        processed / (float)Math.Max(1, total));
                }
            }

            foreach (SkinnedMeshRenderer renderer in skinned)
            {
                processed++;
                if (renderer.sharedMesh == null ||
                    !IsSanitizedSourceRenderer(renderer.gameObject))
                {
                    continue;
                }

                var baked = new Mesh
                {
                    name = renderer.sharedMesh.name + "_MigrationBake",
                    hideFlags = HideFlags.HideAndDontSave
                };
                renderer.BakeMesh(baked, true);
                AddInstance(
                    scenePath,
                    root.transform,
                    renderer,
                    baked,
                    ownsMesh: true,
                    instances,
                    records,
                    meshStats,
                    renderer.sharedMesh);
            }
        }

        private static void AddInstance(
            string scenePath,
            Transform sourceRoot,
            Renderer renderer,
            Mesh mesh,
            bool ownsMesh,
            List<ScannedMeshInstance> instances,
            List<MapMeshRecord> records,
            Dictionary<EntityId, MeshGeometryStats> meshStats,
            Mesh identityMesh = null)
        {
            Mesh assetMesh = identityMesh != null ? identityMesh : mesh;
            EntityId statsKey = assetMesh.GetEntityId();
            if (!meshStats.TryGetValue(statsKey, out MeshGeometryStats stats))
            {
                stats = AnalyzeGeometry(mesh);
                meshStats.Add(statsKey, stats);
            }

            GameObject gameObject = renderer.gameObject;
            Matrix4x4 matrix = renderer.localToWorldMatrix;
            string hierarchyPath = BuildHierarchyPath(sourceRoot, renderer.transform);
            DonorWorldBaselineEntityMetadata metadata =
                gameObject.GetComponent<DonorWorldBaselineEntityMetadata>();
            DonorWorldSupplementalEntityMetadata supplemental =
                gameObject.GetComponent<DonorWorldSupplementalEntityMetadata>();

            string provenancePath = metadata != null
                ? metadata.SourceHierarchyPath
                : supplemental != null ? supplemental.SourceHierarchyPath : string.Empty;
            string semantic = metadata != null ? metadata.SemanticCategory : string.Empty;
            string stableId = metadata != null
                ? metadata.StableId
                : supplemental != null ? supplemental.StableId : string.Empty;
            Material[] materials = renderer.sharedMaterials;
            string materialText = string.Join("|", materials
                .Where(material => material != null)
                .Select(material => material.name));

            var evidence = new MapClassificationEvidence
            {
                HierarchyPath = hierarchyPath,
                ProvenancePath = provenancePath,
                SemanticCategory = semantic,
                MaterialText = materialText,
                Layer = LayerMask.LayerToName(gameObject.layer),
                Tag = gameObject.tag,
                ObjectName = gameObject.name,
                MeshName = assetMesh.name,
                UpwardTriangleRatio = stats.UpwardTriangleRatio,
                VerticalRange = stats.VerticalRange,
                HasMultipleHeightsAtSameXZ = stats.HasMultipleHeightsAtSameXZ
            };
            MapClassificationResult classification = MapMeshClassifier.Classify(evidence);

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                assetMesh,
                out string guid,
                out long localFileId);
            MeshCollider collider = gameObject.GetComponent<MeshCollider>();
            string recordKey = scenePath + "|" + hierarchyPath + "|" +
                               renderer.GetType().Name + "|" + localFileId;
            var record = new MapMeshRecord
            {
                recordId = MapMigrationPaths.StableHash(recordKey),
                scenePath = scenePath,
                hierarchyPath = hierarchyPath,
                category = classification.Category,
                gameObjectName = gameObject.name,
                rendererType = renderer.GetType().Name,
                meshAssetName = assetMesh.name,
                assetGuid = guid ?? string.Empty,
                localFileId = localFileId,
                instanceId = 0,
                entityId = gameObject.GetEntityId().GetRawData(),
                vertexCount = stats.VertexCount,
                triangleCount = stats.TriangleCount,
                subMeshCount = mesh.subMeshCount,
                materialNames = materials
                    .Select(material => material != null ? material.name : "<null>")
                    .ToList(),
                localTransform = SerializableTransform.From(renderer.transform),
                worldMatrix = SerializableMatrix.From(matrix),
                worldBounds = SerializableBounds.From(renderer.bounds),
                hasMeshCollider = collider != null,
                meshColliderEnabled = collider != null && collider.enabled,
                activeSelf = gameObject.activeSelf,
                activeInHierarchy = gameObject.activeInHierarchy,
                hasNegativeWorldDeterminant = matrix.determinant < 0f,
                layerName = LayerMask.LayerToName(gameObject.layer),
                tag = gameObject.tag,
                provenanceStableId = stableId ?? string.Empty,
                provenanceHierarchyPath = provenancePath ?? string.Empty,
                provenanceSemanticCategory = semantic ?? string.Empty,
                classificationReason = classification.Reason,
                upwardTriangleRatio = stats.UpwardTriangleRatio,
                uvPlanarityRms = stats.UvPlanarityRms
            };
            records.Add(record);
            instances.Add(new ScannedMeshInstance
            {
                Record = record,
                Renderer = renderer,
                Mesh = mesh,
                LocalToWorld = matrix,
                OwnsMesh = ownsMesh
            });
        }

        private static bool IsSanitizedSourceRenderer(GameObject gameObject)
        {
            // The accepted baseline root also owns later project-generated forest,
            // grass and lighting overlays. They are not source-map geometry and can
            // contain hundreds of thousands of repeated instances. Only explicit
            // TemporaryDirectImport provenance belongs to this source audit/export.
            return gameObject.GetComponent<DonorWorldBaselineEntityMetadata>() != null ||
                   gameObject.GetComponent<DonorWorldSupplementalEntityMetadata>() != null;
        }

        private static MeshGeometryStats AnalyzeGeometry(Mesh mesh)
        {
            ReadableMeshData data = MapMeshDataReader.Read(mesh);
            int triangles = 0;
            int upward = 0;
            var sampledHeights = new Dictionary<Vector2Int, float>();
            bool multiHeight = false;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;

            foreach (Vector3 vertex in data.Vertices)
            {
                minY = Mathf.Min(minY, vertex.y);
                maxY = Mathf.Max(maxY, vertex.y);
            }

            foreach (int[] indices in data.TriangleSubMeshes)
            {
                for (int index = 0; index + 2 < indices.Length; index += 3)
                {
                    Vector3 a = data.Vertices[indices[index]];
                    Vector3 b = data.Vertices[indices[index + 1]];
                    Vector3 c = data.Vertices[indices[index + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                    triangles++;
                    if (Mathf.Abs(normal.y) >= 0.45f)
                    {
                        upward++;
                    }

                    if ((triangles & 31) == 0)
                    {
                        Vector3 center = (a + b + c) / 3f;
                        var key = new Vector2Int(
                            Mathf.RoundToInt(center.x * 0.25f),
                            Mathf.RoundToInt(center.z * 0.25f));
                        if (sampledHeights.TryGetValue(key, out float prior) &&
                            Mathf.Abs(prior - center.y) > 2f)
                        {
                            multiHeight = true;
                        }
                        else
                        {
                            sampledHeights[key] = center.y;
                        }
                    }
                }
            }

            return new MeshGeometryStats
            {
                VertexCount = data.Vertices.Length,
                TriangleCount = triangles,
                UpwardTriangleRatio = triangles > 0 ? upward / (float)triangles : 0f,
                VerticalRange = data.Vertices.Length > 0 ? maxY - minY : 0f,
                HasMultipleHeightsAtSameXZ = multiHeight,
                UvPlanarityRms = EstimateUvPlanarity(data.Vertices, data.Uv0)
            };
        }

        private static float EstimateUvPlanarity(Vector3[] vertices, Vector2[] uv)
        {
            if (uv.Length != vertices.Length || vertices.Length < 3)
            {
                return -1f;
            }

            int stride = Math.Max(1, vertices.Length / 2048);
            double meanError = 0d;
            int count = 0;
            Vector3 first = vertices[0];
            Vector2 firstUv = uv[0];
            for (int index = stride; index < vertices.Length; index += stride)
            {
                Vector3 delta = vertices[index] - first;
                Vector2 uvDelta = uv[index] - firstUv;
                float distance = Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z);
                if (distance < 0.001f)
                {
                    continue;
                }

                float uvDistance = uvDelta.magnitude;
                meanError += Math.Abs(uvDistance / distance);
                count++;
            }

            return count > 0 ? (float)(meanError / count) : -1f;
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

        private static void PopulateReuseCounts(List<MapMeshRecord> records)
        {
            Dictionary<string, int> counts = records
                .Where(record => !string.IsNullOrEmpty(record.assetGuid))
                .GroupBy(record => record.assetGuid + ":" + record.localFileId)
                .ToDictionary(group => group.Key, group => group.Count());
            foreach (MapMeshRecord record in records)
            {
                string key = record.assetGuid + ":" + record.localFileId;
                record.sharedMeshInstanceCount = counts.TryGetValue(key, out int count)
                    ? count
                    : 1;
            }
        }

        private static MapInventorySummary CreateSummary(List<MapMeshRecord> records)
        {
            return new MapInventorySummary
            {
                meshInstanceCount = records.Count,
                uniqueMeshAssetCount = records
                    .Select(record => record.assetGuid + ":" + record.localFileId)
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                groundCandidateCount = records.Count(record =>
                    record.category == MapMeshCategory.GroundCandidate),
                roadMeshCount = records.Count(record => IsRoad(record.category)),
                residualUnsupportedCount = records.Count(record =>
                    record.category == MapMeshCategory.ResidualUnsupported),
                ambiguousCount = records.Count(record =>
                    record.category == MapMeshCategory.Ambiguous),
                meshColliderCount = records.Count(record => record.hasMeshCollider),
                negativeScaleCount = records.Count(record => record.hasNegativeWorldDeterminant),
                reusedMeshAssetCount = records
                    .Where(record => record.sharedMeshInstanceCount > 1)
                    .Select(record => record.assetGuid + ":" + record.localFileId)
                    .Distinct(StringComparer.Ordinal)
                    .Count()
            };
        }

        internal static bool IsRoad(MapMeshCategory category) =>
            category == MapMeshCategory.RoadAsphalt ||
            category == MapMeshCategory.RoadDirtOrGravel ||
            category == MapMeshCategory.RoadStructure;

        private static string BuildAuthorityReason(string sourceScene)
        {
            if (string.Equals(
                    sourceScene,
                    MapMigrationSettings.DefaultLegacyGlobalScene,
                    StringComparison.Ordinal))
            {
                return "3Buildings/2BasicMap are absent. The frozen, sanitized " +
                       "World_Global_Legacy scene is the accepted canonical donor-map " +
                       "baseline; sibling cells are included for full-instance export. " +
                       "Later project-generated vegetation/grass/lighting overlays without " +
                       "TemporaryDirectImport provenance are intentionally outside the source root.";
            }

            return "Selected from the available project scene set after hierarchy audit.";
        }

        private readonly struct MeshGeometryStats
        {
            public int VertexCount { get; init; }
            public int TriangleCount { get; init; }
            public float UpwardTriangleRatio { get; init; }
            public float VerticalRange { get; init; }
            public bool HasMultipleHeightsAtSameXZ { get; init; }
            public float UvPlanarityRms { get; init; }
        }
    }
}
