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
    internal static class MapMigrationVisualComparison
    {
        private readonly struct MapMark
        {
            public MapMark(Vector3 point, Color32 color)
            {
                Point = point;
                Color = color;
            }

            public Vector3 Point { get; }
            public Color32 Color { get; }
        }

        private sealed class TerrainLookup
        {
            public Terrain[,] Tiles { get; init; } = null!;
            public float MinimumX { get; init; }
            public float MinimumZ { get; init; }
            public float MaximumX { get; init; }
            public float MaximumZ { get; init; }
            public float TileSize { get; init; }
        }

        public static void Generate(IMapMigrationSettings settings)
        {
            if (!File.Exists(MapMigrationPaths.ToAbsoluteProjectPath(settings.OutputScene)))
            {
                throw new FileNotFoundException(
                    "Generated Terrain migration scene is required before visual comparison.",
                    settings.OutputScene);
            }

            MapMeshInventory inventory = MapMeshInventoryWriter.Read();
            MapMigrationValidationReport report = JsonUtility.FromJson<MapMigrationValidationReport>(
                File.ReadAllText(
                    MapMigrationPaths.ToAbsoluteProjectPath(MapMigrationPaths.ValidationJson),
                    Encoding.UTF8));
            Scene scene = EditorSceneManager.OpenScene(settings.OutputScene, OpenSceneMode.Single);
            GameObject sourceRoot = FindSourceRoot(scene, settings.SourceRoot);
            Dictionary<string, MapMeshRecord> records = inventory.records
                .Where(record =>
                    string.Equals(record.scenePath, settings.SourceScene, StringComparison.Ordinal))
                .ToDictionary(record => record.hierarchyPath, record => record, StringComparer.Ordinal);
            var groundInstances = new List<ScannedMeshInstance>();
            foreach (MeshFilter filter in sourceRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || filter.sharedMesh == null)
                {
                    continue;
                }

                string path = BuildHierarchyPath(sourceRoot.transform, renderer.transform);
                if (!records.TryGetValue(path, out MapMeshRecord record) ||
                    record.category != MapMeshCategory.GroundCandidate)
                {
                    continue;
                }

                groundInstances.Add(new ScannedMeshInstance
                {
                    Record = record,
                    Renderer = renderer,
                    Mesh = filter.sharedMesh,
                    LocalToWorld = renderer.localToWorldMatrix
                });
            }

            List<SurfaceTriangle> triangles = TerrainSurfaceSampler.CollectTriangles(
                groundInstances,
                category => category == MapMeshCategory.GroundCandidate,
                0.25f);
            var sourceIndex = new TriangleSpatialIndex(triangles, 32f);
            TriangleSpatialIndex residualIndex = BuildResidualIndex(scene);
            TerrainLookup terrain = BuildTerrainLookup(scene);
            MapMigrationPaths.EnsureAssetFolder(MapMigrationPaths.ReportsRoot);
            var marks = new[]
            {
                new MapMark(report.maximumHeightErrorPoint, new Color32(255, 60, 48, 255)),
                new MapMark(report.maximumRoadEdgeGapPoint, new Color32(230, 70, 255, 255))
            };

            Render(
                MapMigrationPaths.VisualComparisonOverview,
                sourceIndex,
                residualIndex,
                terrain,
                terrain.MinimumX,
                terrain.MaximumX,
                terrain.MinimumZ,
                terrain.MaximumZ,
                640,
                560,
                5f,
                marks);
            RenderDetail(
                MapMigrationPaths.VisualComparisonHeightDetail,
                sourceIndex,
                residualIndex,
                terrain,
                report.maximumHeightErrorPoint,
                128f,
                Mathf.Max(5f, report.maximumHeightError),
                marks);
            RenderDetail(
                MapMigrationPaths.VisualComparisonRoadDetail,
                sourceIndex,
                residualIndex,
                terrain,
                report.maximumRoadEdgeGapPoint,
                128f,
                Mathf.Max(3f, report.maximumRoadEdgeGap),
                marks);
            WriteMarkdown(report);
            AssetDatabase.Refresh();
        }

        private static void RenderDetail(
            string path,
            TriangleSpatialIndex source,
            TriangleSpatialIndex residual,
            TerrainLookup terrain,
            Vector3 center,
            float radius,
            float errorCap,
            IReadOnlyList<MapMark> marks) =>
            Render(
                path,
                source,
                residual,
                terrain,
                center.x - radius,
                center.x + radius,
                center.z - radius,
                center.z + radius,
                512,
                512,
                errorCap,
                marks);

        private static void Render(
            string path,
            TriangleSpatialIndex source,
            TriangleSpatialIndex residual,
            TerrainLookup terrain,
            float minimumX,
            float maximumX,
            float minimumZ,
            float maximumZ,
            int panelWidth,
            int height,
            float errorCap,
            IReadOnlyList<MapMark> marks)
        {
            int sampleCount = panelWidth * height;
            var sourceHeights = new float[sampleCount];
            var terrainHeights = new float[sampleCount];
            var available = new bool[sampleCount];
            var distribution = new List<float>(sampleCount * 2);
            for (int y = 0; y < height; y++)
            {
                float z = Mathf.Lerp(minimumZ, maximumZ, y / (float)Math.Max(1, height - 1));
                for (int x = 0; x < panelWidth; x++)
                {
                    float worldX = Mathf.Lerp(
                        minimumX,
                        maximumX,
                        x / (float)Math.Max(1, panelWidth - 1));
                    int index = y * panelWidth + x;
                    if (!source.TrySampleUpperSurface(worldX, z, out float sourceHeight, out _) ||
                        !TrySampleTerrain(terrain, worldX, z, out float terrainHeight))
                    {
                        sourceHeights[index] = float.NaN;
                        terrainHeights[index] = float.NaN;
                        continue;
                    }

                    available[index] = true;
                    sourceHeights[index] = sourceHeight;
                    if (residual != null &&
                        residual.TrySampleUpperSurface(worldX, z, out float residualHeight, out _) &&
                        Mathf.Abs(residualHeight - sourceHeight) <
                        Mathf.Abs(terrainHeight - sourceHeight))
                    {
                        terrainHeight = residualHeight;
                    }
                    terrainHeights[index] = terrainHeight;
                    distribution.Add(sourceHeight);
                    distribution.Add(terrainHeight);
                }
            }

            distribution.Sort();
            float low = Percentile(distribution, 0.02f);
            float high = Percentile(distribution, 0.98f);
            if (high <= low)
            {
                high = low + 1f;
            }

            const int separator = 2;
            int textureWidth = panelWidth * 3 + separator * 2;
            var pixels = new Color32[textureWidth * height];
            Color32 empty = new Color32(18, 22, 26, 255);
            Color32 divider = new Color32(220, 224, 228, 255);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < textureWidth; x++)
                {
                    pixels[y * textureWidth + x] = empty;
                }

                for (int x = 0; x < separator; x++)
                {
                    pixels[y * textureWidth + panelWidth + x] = divider;
                    pixels[y * textureWidth + panelWidth * 2 + separator + x] = divider;
                }
            }

            for (int index = 0; index < sampleCount; index++)
            {
                if (!available[index])
                {
                    continue;
                }

                int x = index % panelWidth;
                int y = index / panelWidth;
                pixels[y * textureWidth + x] = ElevationColor(sourceHeights[index], low, high);
                pixels[y * textureWidth + panelWidth + separator + x] =
                    ElevationColor(terrainHeights[index], low, high);
                pixels[y * textureWidth + panelWidth * 2 + separator * 2 + x] =
                    DifferenceColor(
                        Mathf.Abs(sourceHeights[index] - terrainHeights[index]),
                        errorCap);
            }

            foreach (MapMark mark in marks)
            {
                DrawMark(
                    pixels,
                    textureWidth,
                    height,
                    panelWidth,
                    separator,
                    minimumX,
                    maximumX,
                    minimumZ,
                    maximumZ,
                    mark);
            }

            var texture = new Texture2D(textureWidth, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                MapMigrationPaths.EnsureFilesystemDirectoryForFile(path);
                File.WriteAllBytes(
                    MapMigrationPaths.ToAbsoluteProjectPath(path),
                    texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static TerrainLookup BuildTerrainLookup(Scene scene)
        {
            MapMigrationGeneratedMarker marker = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MapMigrationGeneratedMarker>(true))
                .Single();
            Terrain[] all = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Terrain>(true))
                .ToArray();
            float minX = all.Min(value => value.transform.position.x);
            float minZ = all.Min(value => value.transform.position.z);
            var tiles = new Terrain[marker.TileCountX, marker.TileCountZ];
            foreach (Terrain value in all)
            {
                int x = Mathf.RoundToInt((value.transform.position.x - minX) / marker.TileSize);
                int z = Mathf.RoundToInt((value.transform.position.z - minZ) / marker.TileSize);
                tiles[x, z] = value;
            }

            return new TerrainLookup
            {
                Tiles = tiles,
                MinimumX = minX,
                MinimumZ = minZ,
                MaximumX = minX + marker.TileCountX * marker.TileSize,
                MaximumZ = minZ + marker.TileCountZ * marker.TileSize,
                TileSize = marker.TileSize
            };
        }

        private static TriangleSpatialIndex BuildResidualIndex(Scene scene)
        {
            var instances = new List<ScannedMeshInstance>();
            foreach (MapMigrationTerrainResidualMarker marker in scene
                         .GetRootGameObjects()
                         .SelectMany(root =>
                             root.GetComponentsInChildren<MapMigrationTerrainResidualMarker>(true)))
            {
                MeshFilter filter = marker.GetComponent<MeshFilter>();
                MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
                if (filter == null || filter.sharedMesh == null || renderer == null || !renderer.enabled)
                {
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

            List<SurfaceTriangle> triangles = TerrainSurfaceSampler.CollectTriangles(
                instances,
                category => category == MapMeshCategory.GroundCandidate,
                0f);
            return triangles.Count > 0 ? new TriangleSpatialIndex(triangles, 16f) : null;
        }

        private static bool TrySampleTerrain(
            TerrainLookup lookup,
            float x,
            float z,
            out float height)
        {
            int tileX = Mathf.FloorToInt((x - lookup.MinimumX) / lookup.TileSize);
            int tileZ = Mathf.FloorToInt((z - lookup.MinimumZ) / lookup.TileSize);
            tileX = Mathf.Clamp(tileX, 0, lookup.Tiles.GetLength(0) - 1);
            tileZ = Mathf.Clamp(tileZ, 0, lookup.Tiles.GetLength(1) - 1);
            Terrain terrain = lookup.Tiles[tileX, tileZ];
            if (terrain == null ||
                x < lookup.MinimumX || x > lookup.MaximumX ||
                z < lookup.MinimumZ || z > lookup.MaximumZ)
            {
                height = 0f;
                return false;
            }

            Vector3 position = terrain.transform.position;
            float normalizedX = Mathf.Clamp01((x - position.x) / lookup.TileSize);
            float normalizedZ = Mathf.Clamp01((z - position.z) / lookup.TileSize);
            height = terrain.terrainData.GetInterpolatedHeight(normalizedX, normalizedZ) + position.y;
            return true;
        }

        private static Color32 ElevationColor(float value, float low, float high)
        {
            float t = Mathf.Clamp01((value - low) / (high - low));
            if (t < 0.35f)
            {
                return Lerp(new Color32(24, 74, 109, 255), new Color32(62, 133, 83, 255), t / 0.35f);
            }

            if (t < 0.75f)
            {
                return Lerp(
                    new Color32(62, 133, 83, 255),
                    new Color32(181, 151, 91, 255),
                    (t - 0.35f) / 0.4f);
            }

            return Lerp(
                new Color32(181, 151, 91, 255),
                new Color32(242, 240, 231, 255),
                (t - 0.75f) / 0.25f);
        }

        private static Color32 DifferenceColor(float difference, float cap)
        {
            float t = Mathf.Clamp01(difference / Mathf.Max(0.001f, cap));
            if (t < 0.25f)
            {
                return Lerp(new Color32(19, 34, 48, 255), new Color32(42, 125, 162, 255), t / 0.25f);
            }

            if (t < 0.65f)
            {
                return Lerp(
                    new Color32(42, 125, 162, 255),
                    new Color32(244, 193, 68, 255),
                    (t - 0.25f) / 0.4f);
            }

            return Lerp(
                new Color32(244, 193, 68, 255),
                new Color32(220, 47, 47, 255),
                (t - 0.65f) / 0.35f);
        }

        private static Color32 Lerp(Color32 left, Color32 right, float t) =>
            Color32.Lerp(left, right, Mathf.Clamp01(t));

        private static float Percentile(IReadOnlyList<float> values, float percentile)
        {
            if (values.Count == 0)
            {
                return 0f;
            }

            int index = Mathf.Clamp(
                Mathf.RoundToInt((values.Count - 1) * percentile),
                0,
                values.Count - 1);
            return values[index];
        }

        private static void DrawMark(
            Color32[] pixels,
            int textureWidth,
            int textureHeight,
            int panelWidth,
            int separator,
            float minimumX,
            float maximumX,
            float minimumZ,
            float maximumZ,
            MapMark mark)
        {
            if (mark.Point.x < minimumX || mark.Point.x > maximumX ||
                mark.Point.z < minimumZ || mark.Point.z > maximumZ)
            {
                return;
            }

            int x = Mathf.RoundToInt(
                Mathf.InverseLerp(minimumX, maximumX, mark.Point.x) * (panelWidth - 1));
            int y = Mathf.RoundToInt(
                Mathf.InverseLerp(minimumZ, maximumZ, mark.Point.z) * (textureHeight - 1));
            int[] panelOffsets =
            {
                0,
                panelWidth + separator,
                panelWidth * 2 + separator * 2
            };
            foreach (int offset in panelOffsets)
            {
                for (int delta = -6; delta <= 6; delta++)
                {
                    SetPixel(pixels, textureWidth, textureHeight, offset + x + delta, y, mark.Color);
                    SetPixel(pixels, textureWidth, textureHeight, offset + x, y + delta, mark.Color);
                }
            }
        }

        private static void SetPixel(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y,
            Color32 color)
        {
            if (x >= 0 && y >= 0 && x < width && y < height)
            {
                pixels[y * width + x] = color;
            }
        }

        private static void WriteMarkdown(MapMigrationValidationReport report)
        {
            var text = new StringBuilder();
            text.AppendLine("# Source mesh / Unity Terrain visual comparison");
            text.AppendLine();
            text.AppendLine("Panels in every PNG: **source upper surface | generated Terrain + residual ground | absolute difference**.");
            text.AppendLine("Red cross: maximum ground-height error. Magenta cross: maximum road-edge gap.");
            text.AppendLine();
            text.AppendLine($"- Overview: `{MapMigrationPaths.VisualComparisonOverview}` (difference capped at 5 m)");
            text.AppendLine($"- Height outlier detail: `{MapMigrationPaths.VisualComparisonHeightDetail}`");
            text.AppendLine($"- Road-gap detail: `{MapMigrationPaths.VisualComparisonRoadDetail}`");
            text.AppendLine($"- Maximum ground error: {report.maximumHeightError.ToString("R", CultureInfo.InvariantCulture)} m, " +
                            $"signed {report.maximumHeightErrorSigned.ToString("R", CultureInfo.InvariantCulture)} m, " +
                            $"record `{report.maximumHeightErrorRecordId}`, point `{report.maximumHeightErrorPoint}`");
            text.AppendLine($"- Maximum road gap: {report.maximumRoadEdgeGap.ToString("R", CultureInfo.InvariantCulture)} m, " +
                            $"record `{report.maximumRoadEdgeGapRecordId}`, point `{report.maximumRoadEdgeGapPoint}`");
            text.AppendLine();
            text.AppendLine("The generated panel uses Terrain where possible and the preserved residual-ground mesh where it is closer to the source surface. " +
                            "Buildings, vegetation, roads and bridges remain unchanged and are not rasterized into the elevation panels.");
            File.WriteAllText(
                MapMigrationPaths.ToAbsoluteProjectPath(MapMigrationPaths.VisualComparisonMarkdown),
                text.ToString(),
                new UTF8Encoding(false));
        }

        private static GameObject FindSourceRoot(Scene scene, string target)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindRecursive(root.transform, target);
                if (found != null)
                {
                    return found.gameObject;
                }
            }

            throw new InvalidDataException("Source root is missing from generated scene.");
        }

        private static Transform FindRecursive(Transform current, string target)
        {
            if (string.Equals(current.name, target, StringComparison.Ordinal) ||
                string.Equals(current.name, Path.GetFileName(target), StringComparison.Ordinal))
            {
                return current;
            }

            for (int index = 0; index < current.childCount; index++)
            {
                Transform found = FindRecursive(current.GetChild(index), target);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static string BuildHierarchyPath(Transform root, Transform target)
        {
            var segments = new Stack<string>();
            Transform current = target;
            while (current != null)
            {
                segments.Push(current.name);
                if (current == root)
                {
                    break;
                }

                current = current.parent;
            }

            return string.Join("/", segments);
        }
    }
}
