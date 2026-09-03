using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSCMapMigration
{
    internal static class MapMigrationValidator
    {
        public static MapMigrationValidationReport Validate(
            IMapMigrationSettings settings,
            MapMeshInventory inventory,
            TerrainBuildResult build,
            bool requireCommitted)
        {
            var report = new MapMigrationValidationReport
            {
                generatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                sourceScene = settings.SourceScene,
                outputScene = settings.OutputScene,
                sourceSceneSha256Before = inventory.sourceSceneSha256,
                inventoryMeshCount = inventory.records.Count,
                residualUnsupportedCount = inventory.summary.residualUnsupportedCount,
                ambiguousCount = inventory.summary.ambiguousCount
            };

            try
            {
                ValidateSource(settings, inventory, report);
                ValidateExport(settings, inventory, report);
                ValidateSceneAndTerrain(settings, inventory, build, requireCommitted, report);
            }
            catch (Exception exception)
            {
                report.errors.Add("Validation threw: " + exception.GetBaseException().Message);
            }

            report.passed = report.errors.Count == 0;
            WriteReports(report, inventory, build, requireCommitted);
            AssetDatabase.Refresh();
            return report;
        }

        private static void ValidateSource(
            IMapMigrationSettings settings,
            MapMeshInventory inventory,
            MapMigrationValidationReport report)
        {
            report.sourceSceneSha256After =
                MapMigrationPaths.ComputeSha256(settings.SourceScene);
            if (!string.Equals(
                    report.sourceSceneSha256Before,
                    report.sourceSceneSha256After,
                    StringComparison.Ordinal))
            {
                report.errors.Add("The source scene SHA-256 changed during migration.");
            }

            foreach (string forbidden in new[]
                     {
                         MapMigrationSettings.RequestedSourceScene,
                         MapMigrationSettings.SecondaryRequestedSourceScene
                     })
            {
                if (!File.Exists(MapMigrationPaths.ToAbsoluteProjectPath(forbidden)))
                {
                    continue;
                }

                if (string.Equals(forbidden, settings.SourceScene, StringComparison.Ordinal))
                {
                    continue;
                }

                report.warnings.Add(
                    $"Alternate source scene exists but was not selected: {forbidden}.");
            }
        }

        private static void ValidateExport(
            IMapMigrationSettings settings,
            MapMeshInventory inventory,
            MapMigrationValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(settings.LastExportRelativePath))
            {
                report.errors.Add("No Blender export directory is recorded in settings.");
                return;
            }

            string root = MapMigrationPaths.ToAbsoluteProjectPath(
                settings.LastExportRelativePath);
            string manifestPath = Path.Combine(root, "Metadata", "manifest.json");
            if (!File.Exists(manifestPath))
            {
                report.errors.Add("Blender export manifest is missing.");
                return;
            }

            MapExportManifest manifest = JsonUtility.FromJson<MapExportManifest>(
                File.ReadAllText(manifestPath, Encoding.UTF8));
            if (manifest == null)
            {
                report.errors.Add("Blender export manifest JSON is invalid.");
                return;
            }

            report.exportedMeshCount = manifest.entries.Count;
            if (manifest.entries.Count != inventory.records.Count)
            {
                report.errors.Add(
                    $"Export manifest has {manifest.entries.Count} entries for {inventory.records.Count} inventory instances.");
            }

            HashSet<string> ids = manifest.entries
                .Select(entry => entry.recordId)
                .ToHashSet(StringComparer.Ordinal);
            foreach (MapMeshRecord record in inventory.records)
            {
                if (!ids.Contains(record.recordId))
                {
                    report.errors.Add("Export manifest is missing record " + record.recordId + ".");
                    if (report.errors.Count > 100)
                    {
                        break;
                    }
                }
            }

            foreach (string required in new[]
                     {
                         "Scene/BaseMap_All.obj", "Scene/Ground_All.obj",
                         "Scene/Roads_All.obj", "Metadata/inventory.csv",
                         "Metadata/materials.json", "Blender/import_base_map.py", "README.md"
                     })
            {
                string path = Path.Combine(root, required.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path) || new FileInfo(path).Length == 0)
                {
                    report.errors.Add("Required export file is missing or empty: " + required);
                }
            }

            string sceneObj = Path.Combine(root, "Scene", "BaseMap_All.obj");
            if (File.Exists(sceneObj))
            {
                ValidateObjStreams(sceneObj, report);
            }

            if (manifest.fbxExporterAvailable)
            {
                foreach (string fbx in new[]
                         {
                             "Scene/BaseMap_All.fbx", "Scene/Ground_All.fbx", "Scene/Roads_All.fbx"
                         })
                {
                    string path = Path.Combine(root, fbx.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(path) || new FileInfo(path).Length == 0)
                    {
                        report.errors.Add("FBX Exporter was available but required FBX is missing: " + fbx);
                    }
                }
            }
            else
            {
                report.warnings.Add(
                    "Official Unity FBX Exporter is not installed; OBJ/MTL is the actual export format.");
            }
        }

        private static void ValidateObjStreams(
            string path,
            MapMigrationValidationReport report)
        {
            bool vertex = false;
            bool normal = false;
            bool uv = false;
            bool material = false;
            foreach (string line in File.ReadLines(path))
            {
                vertex |= line.StartsWith("v ", StringComparison.Ordinal);
                normal |= line.StartsWith("vn ", StringComparison.Ordinal);
                uv |= line.StartsWith("vt ", StringComparison.Ordinal);
                material |= line.StartsWith("usemtl ", StringComparison.Ordinal);
                if (vertex && normal && uv && material)
                {
                    break;
                }
            }

            if (!vertex)
            {
                report.errors.Add("Combined OBJ contains no vertices.");
            }

            if (!normal)
            {
                report.errors.Add("Combined OBJ contains no normals.");
            }

            if (!uv)
            {
                report.errors.Add("Combined OBJ contains no UV0 stream.");
            }

            if (!material)
            {
                report.errors.Add("Combined OBJ contains no material assignments.");
            }
        }

        private static void ValidateSceneAndTerrain(
            IMapMigrationSettings settings,
            MapMeshInventory inventory,
            TerrainBuildResult build,
            bool requireCommitted,
            MapMigrationValidationReport report)
        {
            if (!File.Exists(MapMigrationPaths.ToAbsoluteProjectPath(settings.OutputScene)))
            {
                report.errors.Add("Generated output scene does not exist.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(settings.OutputScene, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            MapMigrationTerrainNeighborConnector[] neighborConnectors = roots
                .SelectMany(root =>
                    root.GetComponentsInChildren<MapMigrationTerrainNeighborConnector>(true))
                .ToArray();
            if (neighborConnectors.Length != 1 || !neighborConnectors[0].Apply())
            {
                report.errors.Add(
                    "Generated Terrain neighbor connector is missing, duplicated, or invalid.");
            }

            MapMigrationGeneratedMarker marker = roots
                .SelectMany(root => root.GetComponentsInChildren<MapMigrationGeneratedMarker>(true))
                .SingleOrDefault();
            if (marker == null)
            {
                report.errors.Add("Generated scene marker is missing or duplicated.");
                return;
            }

            if (marker.Committed != requireCommitted)
            {
                report.errors.Add(
                    $"Generated scene committed={marker.Committed}, expected {requireCommitted}.");
            }

            Terrain[] terrains = roots
                .SelectMany(root => root.GetComponentsInChildren<Terrain>(true))
                .OrderBy(terrain => terrain.name, StringComparer.Ordinal)
                .ToArray();
            report.terrainTileCount = terrains.Length;
            report.tileCountX = marker.TileCountX;
            report.tileCountZ = marker.TileCountZ;
            report.heightmapResolution = marker.HeightmapResolution;
            report.tileSize = marker.TileSize;
            report.sampleSpacing = marker.TileSize / Math.Max(1, marker.HeightmapResolution - 1);
            if (terrains.Length != marker.TileCountX * marker.TileCountZ)
            {
                report.errors.Add(
                    $"Terrain tile count {terrains.Length} does not match marker grid " +
                    $"{marker.TileCountX}x{marker.TileCountZ}.");
            }

            var byPosition = new Dictionary<Vector2Int, Terrain>();
            Vector3 minimum = terrains.Length > 0
                ? new Vector3(
                    terrains.Min(terrain => terrain.transform.position.x),
                    terrains.Min(terrain => terrain.transform.position.y),
                    terrains.Min(terrain => terrain.transform.position.z))
                : Vector3.zero;
            foreach (Terrain terrain in terrains)
            {
                TerrainData data = terrain.terrainData;
                if (data == null)
                {
                    report.errors.Add("TerrainData is missing on " + terrain.name + ".");
                    continue;
                }

                if (data.heightmapResolution != marker.HeightmapResolution ||
                    Mathf.Abs(data.size.x - marker.TileSize) > 1e-4f ||
                    Mathf.Abs(data.size.z - marker.TileSize) > 1e-4f ||
                    data.size.y <= 0f)
                {
                    report.errors.Add("Terrain configuration mismatch on " + terrain.name + ".");
                }

                TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
                if (collider == null || collider.terrainData != data)
                {
                    report.errors.Add("TerrainCollider is missing or mismatched on " + terrain.name + ".");
                }
                else if (collider.enabled != requireCommitted)
                {
                    report.errors.Add(
                        $"TerrainCollider enabled={collider.enabled} on {terrain.name}; expected {requireCommitted}.");
                }

                int x = Mathf.RoundToInt((terrain.transform.position.x - minimum.x) / marker.TileSize);
                int z = Mathf.RoundToInt((terrain.transform.position.z - minimum.z) / marker.TileSize);
                byPosition[new Vector2Int(x, z)] = terrain;
            }

            report.maximumSeamError = ValidateSeamsAndNeighbors(
                byPosition,
                marker.TileCountX,
                marker.TileCountZ,
                report);
            ValidateComponents(roots, report);
            ValidatePreservation(roots, settings, inventory, requireCommitted, report);
            ValidateSurfaceErrors(roots, settings, inventory, terrains, build, report);
            if (build != null)
            {
                report.sourceCoverage = build.Sampling.CoveredSampleCount /
                    (float)Math.Max(1,
                        build.Sampling.CoveredSampleCount +
                        build.Sampling.InteriorFilledCount);
                if (report.sourceCoverage < settings.MinimumSourceCoverage)
                {
                    report.errors.Add(
                        $"Measured source coverage {report.sourceCoverage:P3} is below minimum " +
                        $"{settings.MinimumSourceCoverage:P3}.");
                }
            }
            else
            {
                GeneratedTerrainManifest generated = ReadGeneratedManifest(report);
                if (generated != null)
                {
                    if (!string.Equals(
                            generated.sourceFingerprint,
                            inventory.sourceSetFingerprint,
                            StringComparison.Ordinal))
                    {
                        report.errors.Add(
                            "Generated Terrain manifest source fingerprint does not match the current inventory.");
                    }

                    report.sourceCoverage = generated.coveredSamples /
                        (float)Math.Max(1,
                            generated.coveredSamples + generated.interiorFilledSamples);
                    if (report.sourceCoverage < settings.MinimumSourceCoverage)
                    {
                        report.errors.Add(
                            $"Recorded source coverage {report.sourceCoverage:P3} is below minimum " +
                            $"{settings.MinimumSourceCoverage:P3}.");
                    }
                }
            }
        }

        private static GeneratedTerrainManifest ReadGeneratedManifest(
            MapMigrationValidationReport report = null)
        {
            string path = MapMigrationPaths.ToAbsoluteProjectPath(MapMigrationPaths.ToolManifest);
            if (!File.Exists(path))
            {
                report?.errors.Add("Generated Terrain manifest is missing.");
                return null;
            }

            GeneratedTerrainManifest manifest = JsonUtility.FromJson<GeneratedTerrainManifest>(
                File.ReadAllText(path, Encoding.UTF8));
            if (manifest == null)
            {
                report?.errors.Add("Generated Terrain manifest JSON is invalid.");
            }

            return manifest;
        }

        private static float ValidateSeamsAndNeighbors(
            IReadOnlyDictionary<Vector2Int, Terrain> terrains,
            int countX,
            int countZ,
            MapMigrationValidationReport report)
        {
            float maximum = 0f;
            for (int z = 0; z < countZ; z++)
            {
                for (int x = 0; x < countX; x++)
                {
                    if (!terrains.TryGetValue(new Vector2Int(x, z), out Terrain current))
                    {
                        report.errors.Add($"Terrain tile [{x},{z}] is missing.");
                        continue;
                    }

                    if (x + 1 < countX &&
                        terrains.TryGetValue(new Vector2Int(x + 1, z), out Terrain right))
                    {
                        maximum = Mathf.Max(maximum, CompareVertical(current.terrainData, right.terrainData));
                        if (current.rightNeighbor != right || right.leftNeighbor != current)
                        {
                            report.errors.Add($"Terrain neighbors are not reciprocal at [{x},{z}] -> right.");
                        }
                    }

                    if (z + 1 < countZ &&
                        terrains.TryGetValue(new Vector2Int(x, z + 1), out Terrain top))
                    {
                        maximum = Mathf.Max(maximum, CompareHorizontal(current.terrainData, top.terrainData));
                        if (current.topNeighbor != top || top.bottomNeighbor != current)
                        {
                            report.errors.Add($"Terrain neighbors are not reciprocal at [{x},{z}] -> top.");
                        }
                    }
                }
            }

            if (maximum > 1e-6f)
            {
                report.errors.Add($"Maximum normalized Terrain seam error is {maximum:R}.");
            }

            return maximum;
        }

        private static float CompareVertical(TerrainData left, TerrainData right)
        {
            int resolution = left.heightmapResolution;
            float[,] leftBorder = left.GetHeights(resolution - 1, 0, 1, resolution);
            float[,] rightBorder = right.GetHeights(0, 0, 1, resolution);
            float maximum = 0f;
            for (int z = 0; z < resolution; z++)
            {
                maximum = Mathf.Max(maximum, Mathf.Abs(leftBorder[z, 0] - rightBorder[z, 0]));
            }

            return maximum;
        }

        private static float CompareHorizontal(TerrainData bottom, TerrainData top)
        {
            int resolution = bottom.heightmapResolution;
            float[,] bottomBorder = bottom.GetHeights(0, resolution - 1, resolution, 1);
            float[,] topBorder = top.GetHeights(0, 0, resolution, 1);
            float maximum = 0f;
            for (int x = 0; x < resolution; x++)
            {
                maximum = Mathf.Max(maximum, Mathf.Abs(bottomBorder[0, x] - topBorder[0, x]));
            }

            return maximum;
        }

        private static void ValidateComponents(
            IEnumerable<GameObject> roots,
            MapMigrationValidationReport report)
        {
            foreach (Transform transform in roots
                         .SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
            {
                Component[] components = transform.GetComponents<Component>();
                if (components.Any(component => component == null))
                {
                    report.missingScriptCount++;
                }
            }

            if (report.missingScriptCount > 0)
            {
                report.errors.Add(
                    $"Generated scene contains {report.missingScriptCount} GameObjects with missing scripts.");
            }
        }

        private static void ValidatePreservation(
            GameObject[] roots,
            IMapMigrationSettings settings,
            MapMeshInventory inventory,
            bool requireCommitted,
            MapMigrationValidationReport report)
        {
            GameObject sourceRoot = FindSourceRoot(roots, settings.SourceRoot);
            Dictionary<string, Renderer> renderers = sourceRoot
                .GetComponentsInChildren<Renderer>(true)
                .ToDictionary(
                    renderer => BuildHierarchyPath(sourceRoot.transform, renderer.transform),
                    renderer => renderer,
                    StringComparer.Ordinal);
            Dictionary<string, MapMigrationSourceMarker> markers = sourceRoot
                .GetComponentsInChildren<MapMigrationSourceMarker>(true)
                .ToDictionary(marker => marker.RecordId, marker => marker, StringComparer.Ordinal);
            foreach (MapMeshRecord record in inventory.records.Where(record =>
                         string.Equals(record.scenePath, settings.SourceScene, StringComparison.Ordinal)))
            {
                if (!renderers.TryGetValue(record.hierarchyPath, out Renderer renderer))
                {
                    report.errors.Add("Source renderer is missing from output scene: " + record.hierarchyPath);
                    continue;
                }

                if (!MatricesEqual(renderer.localToWorldMatrix, record.worldMatrix.ToMatrix(), 1e-4f))
                {
                    report.errors.Add("Transform changed: " + record.hierarchyPath);
                }

                if (MapMeshScanner.IsRoad(record.category))
                {
                    ValidateRoadRecord(record, renderer, report);
                }

                if (record.category == MapMeshCategory.GroundCandidate)
                {
                    if (!markers.TryGetValue(record.recordId, out MapMigrationSourceMarker sourceMarker))
                    {
                        report.errors.Add("Ground source marker is missing: " + record.hierarchyPath);
                        continue;
                    }

                    MeshRenderer meshRenderer = renderer as MeshRenderer;
                    bool expectedEnabled = requireCommitted && sourceMarker.CoveragePassed
                        ? false
                        : sourceMarker.RendererWasEnabled;
                    if (meshRenderer != null && meshRenderer.enabled != expectedEnabled)
                    {
                        report.errors.Add("Ground renderer enable state is invalid: " + record.hierarchyPath);
                    }

                    MeshCollider collider = renderer.GetComponent<MeshCollider>();
                    bool expectedCollider = requireCommitted && sourceMarker.CoveragePassed
                        ? false
                        : sourceMarker.ColliderWasEnabled;
                    if (collider != null && collider.enabled != expectedCollider)
                    {
                        report.errors.Add("Ground collider enable state is invalid: " + record.hierarchyPath);
                    }

                    if (requireCommitted && sourceMarker.CoveragePassed)
                    {
                        report.committedGroundRendererCount++;
                    }
                }
            }
        }

        private static void ValidateRoadRecord(
            MapMeshRecord record,
            Renderer renderer,
            MapMigrationValidationReport report)
        {
            Mesh mesh = renderer is MeshRenderer
                ? renderer.GetComponent<MeshFilter>()?.sharedMesh
                : (renderer as SkinnedMeshRenderer)?.sharedMesh;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out string guid, out long localId);
            bool materialsEqual = renderer.sharedMaterials
                .Select(material => material != null ? material.name : "<null>")
                .SequenceEqual(record.materialNames);
            MeshCollider collider = renderer.GetComponent<MeshCollider>();
            if (!string.Equals(guid ?? string.Empty, record.assetGuid, StringComparison.Ordinal) ||
                localId != record.localFileId ||
                !materialsEqual ||
                (collider != null) != record.hasMeshCollider ||
                collider != null && collider.enabled != record.meshColliderEnabled)
            {
                report.roadPreservationFailureCount++;
                report.errors.Add("Road mesh/material/collider changed: " + record.hierarchyPath);
            }
        }

        private static void ValidateSurfaceErrors(
            GameObject[] roots,
            IMapMigrationSettings settings,
            MapMeshInventory inventory,
            Terrain[] terrains,
            TerrainBuildResult build,
            MapMigrationValidationReport report)
        {
            if (terrains.Length == 0)
            {
                return;
            }

            GameObject sourceRoot = FindSourceRoot(roots, settings.SourceRoot);
            Dictionary<string, MapMeshRecord> records = inventory.records
                .Where(record => string.Equals(record.scenePath, settings.SourceScene, StringComparison.Ordinal))
                .ToDictionary(record => record.hierarchyPath, record => record, StringComparer.Ordinal);
            var instances = new List<ScannedMeshInstance>();
            foreach (MeshFilter filter in sourceRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null)
                {
                    continue;
                }

                string path = BuildHierarchyPath(sourceRoot.transform, renderer.transform);
                if (!records.TryGetValue(path, out MapMeshRecord record))
                {
                    continue;
                }

                instances.Add(new ScannedMeshInstance
                {
                    Record = record,
                    Renderer = renderer,
                    Mesh = filter.sharedMesh,
                    LocalToWorld = renderer.localToWorldMatrix
                });
            }

            List<SurfaceTriangle> ground = TerrainSurfaceSampler.CollectTriangles(
                instances,
                category => category == MapMeshCategory.GroundCandidate,
                0.25f);
            List<SurfaceTriangle> allRoad = TerrainSurfaceSampler.CollectTriangles(
                instances,
                category => category == MapMeshCategory.RoadAsphalt ||
                            category == MapMeshCategory.RoadDirtOrGravel,
                0.65f);
            List<SurfaceTriangle> road = RoadConstraintBuilder.FilterRoadBedTriangles(
                allRoad,
                out int elevatedRoadTriangles);
            report.elevatedRoadTriangleCount = elevatedRoadTriangles;
            if (elevatedRoadTriangles > 0)
            {
                report.warnings.Add(
                    $"{elevatedRoadTriangles} vertically overlapping road triangles are retained as " +
                    "elevated RoadStructure and excluded from road-bed clearance measurements.");
            }

            List<SurfaceTriangle> residual = CollectResidualTriangles(roots, report);
            TriangleSpatialIndex residualIndex = residual.Count > 0
                ? new TriangleSpatialIndex(residual, 16f)
                : null;
            MeasureGroundError(
                ground,
                residualIndex,
                terrains,
                settings.ValidationSampleCount,
                report);
            MeasureRoadClearance(road, terrains, settings, report);
        }

        private static List<SurfaceTriangle> CollectResidualTriangles(
            GameObject[] roots,
            MapMigrationValidationReport report)
        {
            var instances = new List<ScannedMeshInstance>();
            foreach (MapMigrationTerrainResidualMarker marker in roots
                         .SelectMany(root =>
                             root.GetComponentsInChildren<MapMigrationTerrainResidualMarker>(true)))
            {
                report.preservedResidualTriangleCount += marker.TriangleCount;
                MeshFilter filter = marker.GetComponent<MeshFilter>();
                MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
                if (filter == null || filter.sharedMesh == null || renderer == null || !renderer.enabled)
                {
                    report.errors.Add(
                        "A Terrain residual marker is missing its enabled mesh presentation.");
                    continue;
                }

                instances.Add(new ScannedMeshInstance
                {
                    Record = new MapMeshRecord
                    {
                        recordId = marker.SourceRecordId,
                        category = MapMeshCategory.GroundCandidate
                    },
                    Renderer = renderer,
                    Mesh = filter.sharedMesh,
                    LocalToWorld = renderer.localToWorldMatrix
                });
            }

            return TerrainSurfaceSampler.CollectTriangles(
                instances,
                category => category == MapMeshCategory.GroundCandidate,
                0f);
        }

        private static void MeasureGroundError(
            IReadOnlyList<SurfaceTriangle> triangles,
            TriangleSpatialIndex residual,
            Terrain[] terrains,
            int requestedSamples,
            MapMigrationValidationReport report)
        {
            int stride = Math.Max(1, triangles.Count / Math.Max(1, requestedSamples));
            double sum = 0d;
            float maximum = 0f;
            float maximumSigned = 0f;
            string maximumRecord = string.Empty;
            Vector3 maximumPoint = Vector3.zero;
            int count = 0;
            for (int index = 0; index < triangles.Count; index += stride)
            {
                SurfaceTriangle triangle = triangles[index];
                Vector3 point = (triangle.A + triangle.B + triangle.C) / 3f;
                if (!TrySampleTerrain(terrains, point.x, point.z, out float terrainHeight))
                {
                    continue;
                }

                float signedError = terrainHeight - point.y;
                float error = Mathf.Abs(signedError);
                if (residual != null &&
                    residual.TrySampleUpperSurface(
                        point.x,
                        point.z,
                        out float residualHeight,
                        out _) &&
                    Mathf.Abs(residualHeight - point.y) < error)
                {
                    signedError = residualHeight - point.y;
                    error = Mathf.Abs(signedError);
                }
                sum += error;
                if (error > maximum)
                {
                    maximum = error;
                    maximumSigned = signedError;
                    maximumRecord = triangle.RecordId;
                    maximumPoint = point;
                }
                count++;
            }

            report.meanHeightError = count > 0 ? (float)(sum / count) : 0f;
            report.maximumHeightError = maximum;
            report.maximumHeightErrorSigned = maximumSigned;
            report.maximumHeightErrorRecordId = maximumRecord;
            report.maximumHeightErrorPoint = maximumPoint;
            if (count == 0)
            {
                report.errors.Add("No deterministic ground comparison samples could be measured.");
            }
        }

        private static void MeasureRoadClearance(
            IReadOnlyList<SurfaceTriangle> triangles,
            Terrain[] terrains,
            IMapMigrationSettings settings,
            MapMigrationValidationReport report)
        {
            float protrusion = 0f;
            float edgeGap = 0f;
            string protrusionRecord = string.Empty;
            Vector3 protrusionPoint = Vector3.zero;
            string edgeGapRecord = string.Empty;
            Vector3 edgeGapPoint = Vector3.zero;
            int sampled = 0;
            int stride = Math.Max(1, triangles.Count / Math.Max(1, settings.ValidationSampleCount));
            for (int index = 0; index < triangles.Count; index += stride)
            {
                SurfaceTriangle triangle = triangles[index];
                Vector3 point = (triangle.A + triangle.B + triangle.C) / 3f;
                if (!TrySampleTerrain(terrains, point.x, point.z, out float terrainHeight))
                {
                    continue;
                }

                float gap = point.y - terrainHeight;
                if (Mathf.Abs(gap) > 2.5f)
                {
                    continue;
                }

                float measuredProtrusion =
                    terrainHeight - (point.y - settings.RoadClearance);
                if (measuredProtrusion > protrusion)
                {
                    protrusion = measuredProtrusion;
                    protrusionRecord = triangle.RecordId;
                    protrusionPoint = point;
                }
                if (gap > edgeGap)
                {
                    edgeGap = gap;
                    edgeGapRecord = triangle.RecordId;
                    edgeGapPoint = point;
                }
                sampled++;
            }

            report.maximumRoadProtrusion = protrusion;
            report.maximumRoadEdgeGap = edgeGap;
            report.maximumRoadEdgeGapRecordId = edgeGapRecord;
            report.maximumRoadEdgeGapPoint = edgeGapPoint;
            if (sampled == 0)
            {
                report.warnings.Add("No near-ground road comparison samples were available.");
            }
            else if (protrusion > 0.015f)
            {
                report.errors.Add(
                    $"Terrain protrudes above constrained roads by up to {protrusion:R} m " +
                    $"at {protrusionPoint:R} (record {protrusionRecord}).");
            }

            if (edgeGap > 1.5f)
            {
                report.warnings.Add(
                    $"Maximum measured near-ground road gap is {edgeGap:R} m at " +
                    $"{edgeGapPoint:R} (record {edgeGapRecord}); manual shoulder review is required.");
            }
        }

        private static bool TrySampleTerrain(
            IEnumerable<Terrain> terrains,
            float x,
            float z,
            out float height)
        {
            foreach (Terrain terrain in terrains)
            {
                Vector3 position = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (x < position.x || x > position.x + size.x ||
                    z < position.z || z > position.z + size.z)
                {
                    continue;
                }

                height = terrain.SampleHeight(new Vector3(x, 0f, z)) + position.y;
                return true;
            }

            height = 0f;
            return false;
        }

        internal static bool MatricesEqual(
            Matrix4x4 left,
            Matrix4x4 right,
            float tolerance)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    if (Mathf.Abs(left[row, column] - right[row, column]) > tolerance)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static GameObject FindSourceRoot(GameObject[] roots, string target)
        {
            foreach (GameObject root in roots)
            {
                Transform found = FindRecursive(root.transform, target);
                if (found != null)
                {
                    return found.gameObject;
                }
            }

            throw new InvalidDataException("Source root is missing in output scene.");
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

        private static void WriteReports(
            MapMigrationValidationReport report,
            MapMeshInventory inventory,
            TerrainBuildResult build,
            bool committed)
        {
            MapMigrationPaths.EnsureAssetFolder(MapMigrationPaths.ReportsRoot);
            File.WriteAllText(
                MapMigrationPaths.ToAbsoluteProjectPath(MapMigrationPaths.ValidationJson),
                JsonUtility.ToJson(report, true),
                new UTF8Encoding(false));

            GeneratedTerrainManifest generated = build == null
                ? ReadGeneratedManifest()
                : null;
            string material = build?.MaterialPlan?.Description ??
                              generated?.materialTransfer ??
                              "Not available in this validation process.";
            string smoothing = build == null && generated == null
                ? "Not available in this validation process."
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "mean {0:R} m; median {1:R} m; p95 {2:R} m; max {3:R} m; limit hits {4}",
                    build?.Sampling.MeanSmoothingDisplacement ?? generated.meanSmoothingDisplacement,
                    build?.Sampling.MedianSmoothingDisplacement ?? generated.medianSmoothingDisplacement,
                    build?.Sampling.P95SmoothingDisplacement ?? generated.p95SmoothingDisplacement,
                    build?.Sampling.MaximumSmoothingDisplacement ?? generated.maximumSmoothingDisplacement,
                    build?.Sampling.SmoothingLimitHitCount ?? generated.smoothingLimitHitCount);
            var markdown = new StringBuilder();
            markdown.AppendLine("# Map terrain migration validation");
            markdown.AppendLine();
            markdown.AppendLine($"Status: **{(report.passed ? "PASS" : "FAIL")}**");
            markdown.AppendLine();
            markdown.AppendLine($"- Source: `{report.sourceScene}`");
            markdown.AppendLine($"- Output: `{report.outputScene}`");
            markdown.AppendLine($"- Source choice: {inventory.authoritativeSourceReason}");
            markdown.AppendLine($"- Source SHA-256 unchanged: `{report.sourceSceneSha256Before == report.sourceSceneSha256After}`");
            markdown.AppendLine($"- Inventory/export instances: {report.inventoryMeshCount}/{report.exportedMeshCount}");
            markdown.AppendLine($"- Ground candidates: {inventory.summary.groundCandidateCount}");
            markdown.AppendLine($"- Road meshes: {inventory.summary.roadMeshCount}");
            markdown.AppendLine($"- Residual/Ambiguous: {report.residualUnsupportedCount}/{report.ambiguousCount}");
            markdown.AppendLine($"- Terrain: {report.terrainTileCount} tiles ({report.tileCountX} x {report.tileCountZ}), " +
                                $"tile {report.tileSize:R} m, resolution {report.heightmapResolution}, spacing {report.sampleSpacing:R} m");
            markdown.AppendLine($"- Source coverage: {report.sourceCoverage:P3}");
            markdown.AppendLine($"- Height error mean/max: {report.meanHeightError:R} / {report.maximumHeightError:R} m");
            markdown.AppendLine(
                $"- Maximum height-error sample: signed {report.maximumHeightErrorSigned:R} m at " +
                $"{report.maximumHeightErrorPoint:R} (record {report.maximumHeightErrorRecordId})");
            markdown.AppendLine($"- Maximum normalized seam error: {report.maximumSeamError:R}");
            markdown.AppendLine($"- Road protrusion/edge gap: {report.maximumRoadProtrusion:R} / {report.maximumRoadEdgeGap:R} m");
            markdown.AppendLine(
                $"- Maximum road-gap sample: {report.maximumRoadEdgeGapPoint:R} " +
                $"(record {report.maximumRoadEdgeGapRecordId})");
            markdown.AppendLine(
                $"- Elevated road triangles retained as structure: {report.elevatedRoadTriangleCount}");
            markdown.AppendLine(
                $"- Ground triangles preserved as residual meshes: {report.preservedResidualTriangleCount}");
            markdown.AppendLine($"- Road preservation failures: {report.roadPreservationFailureCount}");
            markdown.AppendLine($"- Missing scripts: {report.missingScriptCount}");
            markdown.AppendLine($"- Committed: {committed}; ground renderers replaced: {report.committedGroundRendererCount}");
            markdown.AppendLine($"- Material transfer: {material}");
            markdown.AppendLine($"- Smoothing: {smoothing}");
            markdown.AppendLine();
            markdown.AppendLine("## Errors");
            markdown.AppendLine();
            if (report.errors.Count == 0)
            {
                markdown.AppendLine("- None.");
            }
            else
            {
                foreach (string error in report.errors)
                {
                    markdown.AppendLine("- " + error);
                }
            }

            markdown.AppendLine();
            markdown.AppendLine("## Warnings and manual review");
            markdown.AppendLine();
            if (report.warnings.Count == 0)
            {
                markdown.AppendLine("- None.");
            }
            else
            {
                foreach (string warning in report.warnings)
                {
                    markdown.AppendLine("- " + warning);
                }
            }

            markdown.AppendLine("- Bridge decks and other RoadStructure meshes remain meshes and are excluded from road height constraints.");
            markdown.AppendLine("- Visual review remains required for road shoulders, bridge clearance, water boundaries and material alignment.");
            File.WriteAllText(
                MapMigrationPaths.ToAbsoluteProjectPath(MapMigrationPaths.ValidationMarkdown),
                markdown.ToString(),
                new UTF8Encoding(false));
        }
    }
}
