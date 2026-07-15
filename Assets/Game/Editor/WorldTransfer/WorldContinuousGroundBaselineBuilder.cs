using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Core.Identity;
using MSC.World.Partition;
using MSC.World.Remaster;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldTransfer
{
    public readonly struct WorldVoidProfileStation
    {
        public WorldVoidProfileStation(float z, float outerX, float outerY, float innerX, float innerY)
        {
            Z = z;
            OuterX = outerX;
            OuterY = outerY;
            InnerX = innerX;
            InnerY = innerY;
        }

        public float Z { get; }
        public float OuterX { get; }
        public float OuterY { get; }
        public float InnerX { get; }
        public float InnerY { get; }
    }

    public sealed class WorldVoidRegionPieceDefinition
    {
        public WorldVoidRegionPieceDefinition(
            string regionId,
            string pieceId,
            string stableEntityId,
            string displayName,
            WorldVoidRegionClassification classification,
            string implementationStatus,
            string cellId,
            IReadOnlyList<WorldVoidProfileStation> stations,
            float longitudinalStepMeters,
            float surfaceOffsetMeters,
            string waterMaskStatus,
            string boundaryEvidenceStableIds,
            string meshAssetPath,
            string sceneAssetPath,
            string declaredAuthoringFingerprintSha256,
            string computedAuthoringFingerprintSha256,
            string notes)
        {
            RegionId = regionId;
            PieceId = pieceId;
            StableEntityId = stableEntityId;
            DisplayName = displayName;
            Classification = classification;
            ImplementationStatus = implementationStatus;
            CellId = cellId;
            Stations = stations;
            LongitudinalStepMeters = longitudinalStepMeters;
            SurfaceOffsetMeters = surfaceOffsetMeters;
            WaterMaskStatus = waterMaskStatus;
            BoundaryEvidenceStableIds = boundaryEvidenceStableIds;
            MeshAssetPath = meshAssetPath;
            SceneAssetPath = sceneAssetPath;
            DeclaredAuthoringFingerprintSha256 = declaredAuthoringFingerprintSha256;
            ComputedAuthoringFingerprintSha256 = computedAuthoringFingerprintSha256;
            Notes = notes;
        }

        public string RegionId { get; }
        public string PieceId { get; }
        public string StableEntityId { get; }
        public string DisplayName { get; }
        public WorldVoidRegionClassification Classification { get; }
        public string ImplementationStatus { get; }
        public string CellId { get; }
        public IReadOnlyList<WorldVoidProfileStation> Stations { get; }
        public float LongitudinalStepMeters { get; }
        public float SurfaceOffsetMeters { get; }
        public string WaterMaskStatus { get; }
        public string BoundaryEvidenceStableIds { get; }
        public string MeshAssetPath { get; }
        public string SceneAssetPath { get; }
        public string DeclaredAuthoringFingerprintSha256 { get; }
        public string ComputedAuthoringFingerprintSha256 { get; }
        public string Notes { get; }

        public WorldCellIndex CellIndex
        {
            get
            {
                string[] parts = CellId.Split('_');
                if (parts.Length != 3 || parts[0] != "cell" ||
                    !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
                    !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int z))
                {
                    throw new InvalidDataException("Invalid void-fill cell ID: " + CellId);
                }

                return new WorldCellIndex(x, z);
            }
        }
    }

    public readonly struct WorldVoidPieceGeometry
    {
        public WorldVoidPieceGeometry(Vector3[] vertices, Vector2[] uv, int[] triangles, Vector3 cellOrigin)
        {
            Vertices = vertices;
            Uv = uv;
            Triangles = triangles;
            CellOrigin = cellOrigin;
        }

        public Vector3[] Vertices { get; }
        public Vector2[] Uv { get; }
        public int[] Triangles { get; }
        public Vector3 CellOrigin { get; }
    }

    public static class WorldContinuousGroundBaselineBuilder
    {
        public const string BuilderVersion = "05C1.1";
        public const string RegionTableRelativePath = "Docs/WorldRemaster/M05C1_DONOR_VOID_REGIONS.csv";
        public const string TerrainMaterialPath = "Assets/Game/World/Production/Materials/WR_Terrain.mat";
        public const float CellSizeMeters = 512f;

        private static readonly string[] FingerprintColumns =
        {
            "RegionId", "PieceId", "StableEntityId", "DisplayName", "Classification",
            "ImplementationStatus", "CellId", "StationProfile", "LongitudinalStepMeters",
            "SurfaceOffsetMeters", "WaterMaskStatus", "BoundaryEvidenceStableIds",
            "MeshAssetPath", "SceneAssetPath"
        };

        [MenuItem("Tools/MSC Remake/World Transfer/05C1/Rebuild Teimo Continuous Ground Baseline")]
        public static void BuildAll()
        {
            IReadOnlyList<WorldVoidRegionPieceDefinition> definitions = LoadDefinitions();
            WorldVoidRegionPieceDefinition[] approved = definitions
                .Where(definition => definition.ImplementationStatus == "ApprovedBoundedPilot")
                .OrderBy(definition => definition.CellId, StringComparer.Ordinal)
                .ToArray();
            if (approved.Length == 0)
            {
                throw new InvalidOperationException("05C1 has no approved bounded void-fill pieces.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(TerrainMaterialPath);
            if (material == null)
            {
                throw new FileNotFoundException("Project-owned terrain material is missing.", TerrainMaterialPath);
            }

            foreach (WorldVoidRegionPieceDefinition definition in approved)
            {
                BuildPiece(definition, material);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Debug.Log($"WORLD_MAP_05C1_BUILD_OK regions={approved.Select(value => value.RegionId).Distinct().Count()} pieces={approved.Length} cells={approved.Select(value => value.CellId).Distinct().Count()}");
        }

        [MenuItem("Tools/MSC Remake/World Transfer/05C1/Open Teimo Reference + Fill Review")]
        public static void OpenReview()
        {
            WorldPartitionBuilder.OpenReferenceOverview();
            foreach (string scenePath in LoadDefinitions()
                         .Where(value => value.ImplementationStatus == "ApprovedBoundedPilot")
                         .Select(value => value.SceneAssetPath)
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    throw new FileNotFoundException("Generate the 05C1 fill scenes first.", scenePath);
                }

                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            if (SceneView.lastActiveSceneView != null)
            {
                SceneView.lastActiveSceneView.LookAt(
                    new Vector3(-1585f, 4f, 160f),
                    Quaternion.Euler(90f, 0f, 0f),
                    260f,
                    false,
                    true);
            }
        }

        public static void RunBatch()
        {
            BuildAll();
            WorldContinuousGroundBaselineValidator.RunBatch();
        }

        public static IReadOnlyList<WorldVoidRegionPieceDefinition> LoadDefinitions()
        {
            string absolutePath = ToAbsoluteProjectPath(RegionTableRelativePath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException("05C1 void-region table is missing.", absolutePath);
            }

            List<IReadOnlyList<string>> records = ParseCsv(File.ReadAllText(absolutePath));
            if (records.Count < 2)
            {
                throw new InvalidDataException("05C1 void-region table contains no data rows.");
            }

            IReadOnlyList<string> header = records[0];
            var columnIndex = header.Select((name, index) => (name, index))
                .ToDictionary(value => value.name, value => value.index, StringComparer.Ordinal);
            string[] required = FingerprintColumns
                .Concat(new[] { "DeclaredAuthoringFingerprintSha256", "Notes" })
                .ToArray();
            foreach (string column in required)
            {
                if (!columnIndex.ContainsKey(column))
                {
                    throw new InvalidDataException("05C1 void-region table is missing column: " + column);
                }
            }

            var result = new List<WorldVoidRegionPieceDefinition>();
            for (int rowIndex = 1; rowIndex < records.Count; rowIndex++)
            {
                IReadOnlyList<string> values = records[rowIndex];
                if (values.Count == 1 && string.IsNullOrWhiteSpace(values[0]))
                {
                    continue;
                }

                if (values.Count != header.Count)
                {
                    throw new InvalidDataException($"05C1 CSV row {rowIndex + 1} has {values.Count} columns; expected {header.Count}.");
                }

                string Get(string column) => values[columnIndex[column]].Trim();
                if (!Enum.TryParse(Get("Classification"), false, out WorldVoidRegionClassification classification))
                {
                    throw new InvalidDataException($"05C1 CSV row {rowIndex + 1} has invalid classification.");
                }

                string canonical = string.Join("|", FingerprintColumns.Select(Get));
                string computedFingerprint = Sha256(canonical);
                string declaredFingerprint = Get("DeclaredAuthoringFingerprintSha256");
                if (!string.Equals(computedFingerprint, declaredFingerprint, StringComparison.Ordinal))
                {
                    throw new InvalidDataException($"05C1 CSV row {rowIndex + 1} authoring fingerprint mismatch. Expected {computedFingerprint}.");
                }

                var definition = new WorldVoidRegionPieceDefinition(
                    Get("RegionId"),
                    Get("PieceId"),
                    Get("StableEntityId"),
                    Get("DisplayName"),
                    classification,
                    Get("ImplementationStatus"),
                    Get("CellId"),
                    ParseStations(Get("StationProfile"), rowIndex + 1),
                    ParseFloat(Get("LongitudinalStepMeters"), "LongitudinalStepMeters", rowIndex + 1),
                    ParseFloat(Get("SurfaceOffsetMeters"), "SurfaceOffsetMeters", rowIndex + 1),
                    Get("WaterMaskStatus"),
                    Get("BoundaryEvidenceStableIds"),
                    Get("MeshAssetPath"),
                    Get("SceneAssetPath"),
                    declaredFingerprint,
                    computedFingerprint,
                    Get("Notes"));
                ValidateDefinition(definition, rowIndex + 1);
                result.Add(definition);
            }

            if (result.Select(value => value.StableEntityId).Distinct(StringComparer.Ordinal).Count() != result.Count)
            {
                throw new InvalidDataException("05C1 void-region table contains duplicate stable entity IDs.");
            }

            return result;
        }

        public static WorldVoidPieceGeometry CreateGeometry(WorldVoidRegionPieceDefinition definition)
        {
            WorldCellIndex cell = definition.CellIndex;
            var origin = new Vector3(cell.X * CellSizeMeters, 0f, cell.Z * CellSizeMeters);
            IReadOnlyList<WorldVoidProfileStation> stations = Densify(definition.Stations, definition.LongitudinalStepMeters);
            var vertices = new Vector3[stations.Count * 2];
            var uv = new Vector2[vertices.Length];
            float cellMinX = origin.x;
            float cellMaxX = origin.x + CellSizeMeters;

            for (int index = 0; index < stations.Count; index++)
            {
                WorldVoidProfileStation station = stations[index];
                float leftX = Mathf.Max(station.OuterX, cellMinX);
                float rightX = Mathf.Min(station.InnerX, cellMaxX);
                if (rightX - leftX <= 0.01f)
                {
                    throw new InvalidDataException($"05C1 piece {definition.PieceId} has no width at z={station.Z:0.###}.");
                }

                vertices[index * 2] = new Vector3(
                    leftX - origin.x,
                    SurfaceHeight(station, leftX, definition.SurfaceOffsetMeters),
                    station.Z - origin.z);
                vertices[index * 2 + 1] = new Vector3(
                    rightX - origin.x,
                    SurfaceHeight(station, rightX, definition.SurfaceOffsetMeters),
                    station.Z - origin.z);
                uv[index * 2] = new Vector2(leftX * 0.02f, station.Z * 0.02f);
                uv[index * 2 + 1] = new Vector2(rightX * 0.02f, station.Z * 0.02f);
            }

            var triangles = new int[(stations.Count - 1) * 6];
            for (int row = 0; row < stations.Count - 1; row++)
            {
                int a = row * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                int triangle = row * 6;
                triangles[triangle] = a;
                triangles[triangle + 1] = c;
                triangles[triangle + 2] = b;
                triangles[triangle + 3] = b;
                triangles[triangle + 4] = c;
                triangles[triangle + 5] = d;
            }

            return new WorldVoidPieceGeometry(vertices, uv, triangles, origin);
        }

        public static string ComputeGeometryFingerprint(WorldVoidPieceGeometry geometry)
        {
            var canonical = new StringBuilder();
            foreach (Vector3 vertex in geometry.Vertices)
            {
                canonical.Append(vertex.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(vertex.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                    .Append(vertex.z.ToString("R", CultureInfo.InvariantCulture)).Append(';');
            }

            canonical.Append('|').Append(string.Join(",", geometry.Triangles));
            return Sha256(canonical.ToString());
        }

        private static void BuildPiece(WorldVoidRegionPieceDefinition definition, Material material)
        {
            EnsureAssetFolder(definition.MeshAssetPath);
            EnsureAssetFolder(definition.SceneAssetPath);
            WorldVoidPieceGeometry geometry = CreateGeometry(definition);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(definition.MeshAssetPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "WR_" + definition.RegionId + "_" + definition.PieceId };
                AssetDatabase.CreateAsset(mesh, definition.MeshAssetPath);
            }

            mesh.Clear();
            mesh.indexFormat = geometry.Vertices.Length > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = geometry.Vertices;
            mesh.uv = geometry.Uv;
            mesh.triangles = geometry.Triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            string expectedRootName = "WR_05C1_VoidFill_" + definition.CellId;
            SceneAsset existingSceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(definition.SceneAssetPath);
            Scene scene;
            GameObject root;
            GameObject piece;
            if (existingSceneAsset == null)
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                root = new GameObject(expectedRootName);
                piece = new GameObject(definition.DisplayName);
                piece.transform.SetParent(root.transform, false);
            }
            else
            {
                scene = EditorSceneManager.OpenScene(definition.SceneAssetPath, OpenSceneMode.Single);
                GameObject[] roots = scene.GetRootGameObjects();
                root = roots.SingleOrDefault(value =>
                    string.Equals(value.name, expectedRootName, StringComparison.Ordinal));
                if (roots.Length != 1 || root == null)
                {
                    throw new InvalidDataException(
                        $"Existing 05C1 scene {definition.SceneAssetPath} does not have its single deterministic root.");
                }

                Transform[] directChildren = root.transform.Cast<Transform>().ToArray();
                Transform pieceTransform = directChildren.SingleOrDefault(value =>
                    string.Equals(value.name, definition.DisplayName, StringComparison.Ordinal));
                if (directChildren.Length != 1 || pieceTransform == null)
                {
                    throw new InvalidDataException(
                        $"Existing 05C1 scene {definition.SceneAssetPath} does not have its single deterministic piece.");
                }

                piece = pieceTransform.gameObject;
            }

            root.name = expectedRootName;
            root.transform.position = geometry.CellOrigin;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            piece.name = definition.DisplayName;
            piece.transform.SetParent(root.transform, false);
            piece.transform.localPosition = Vector3.zero;
            piece.transform.localRotation = Quaternion.identity;
            piece.transform.localScale = Vector3.one;
            GetOrAddComponent<MeshFilter>(piece).sharedMesh = mesh;
            GetOrAddComponent<MeshRenderer>(piece).sharedMaterial = material;
            MeshCollider collider = GetOrAddComponent<MeshCollider>(piece);
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
            collider.convex = false;
            collider.isTrigger = false;
            WorldVoidFillMarker marker = GetOrAddComponent<WorldVoidFillMarker>(piece);
            marker.Configure(
                definition.RegionId,
                definition.PieceId,
                definition.Classification,
                definition.CellId,
                definition.ComputedAuthoringFingerprintSha256,
                definition.BoundaryEvidenceStableIds);
            SetStableId(piece, definition.StableEntityId);
            GameObjectUtility.SetStaticEditorFlags(
                piece,
                StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, definition.SceneAssetPath);
        }

        private static IReadOnlyList<WorldVoidProfileStation> Densify(
            IReadOnlyList<WorldVoidProfileStation> source,
            float maximumStep)
        {
            var result = new List<WorldVoidProfileStation> { source[0] };
            for (int index = 0; index < source.Count - 1; index++)
            {
                WorldVoidProfileStation a = source[index];
                WorldVoidProfileStation b = source[index + 1];
                int segments = Mathf.Max(1, Mathf.CeilToInt((b.Z - a.Z) / maximumStep));
                for (int segment = 1; segment <= segments; segment++)
                {
                    float t = segment / (float)segments;
                    result.Add(new WorldVoidProfileStation(
                        Mathf.Lerp(a.Z, b.Z, t),
                        Mathf.Lerp(a.OuterX, b.OuterX, t),
                        Mathf.Lerp(a.OuterY, b.OuterY, t),
                        Mathf.Lerp(a.InnerX, b.InnerX, t),
                        Mathf.Lerp(a.InnerY, b.InnerY, t)));
                }
            }

            return result;
        }

        private static float SurfaceHeight(WorldVoidProfileStation station, float worldX, float offset)
        {
            float linear = Mathf.InverseLerp(station.OuterX, station.InnerX, worldX);
            float smooth = linear * linear * (3f - 2f * linear);
            return Mathf.Lerp(station.OuterY, station.InnerY, smooth) + offset;
        }

        private static void ValidateDefinition(WorldVoidRegionPieceDefinition definition, int csvRow)
        {
            if (string.IsNullOrWhiteSpace(definition.RegionId) || string.IsNullOrWhiteSpace(definition.PieceId))
            {
                throw new InvalidDataException($"05C1 CSV row {csvRow} is missing region or piece ID.");
            }

            if (!StableEntityId.TryParse(definition.StableEntityId, out _))
            {
                throw new InvalidDataException($"05C1 CSV row {csvRow} has invalid stable entity ID.");
            }

            if (definition.Classification != WorldVoidRegionClassification.IntentionalDonorVoid)
            {
                throw new InvalidDataException($"05C1 CSV row {csvRow} is not an approved IntentionalDonorVoid.");
            }

            if (definition.LongitudinalStepMeters <= 0f || definition.LongitudinalStepMeters > 10f ||
                definition.Stations.Count < 2)
            {
                throw new InvalidDataException($"05C1 CSV row {csvRow} has invalid station density.");
            }

            float previousZ = float.NegativeInfinity;
            foreach (WorldVoidProfileStation station in definition.Stations)
            {
                if (!float.IsFinite(station.Z) || !float.IsFinite(station.OuterX) || !float.IsFinite(station.OuterY) ||
                    !float.IsFinite(station.InnerX) || !float.IsFinite(station.InnerY) || station.Z <= previousZ ||
                    station.OuterX >= station.InnerX)
                {
                    throw new InvalidDataException($"05C1 CSV row {csvRow} has invalid ordered station data.");
                }

                previousZ = station.Z;
            }

            if (!definition.MeshAssetPath.StartsWith("Assets/Game/World/Production/", StringComparison.Ordinal) ||
                !definition.SceneAssetPath.StartsWith("Assets/Game/World/Generated/VoidFillCells/", StringComparison.Ordinal))
            {
                throw new InvalidDataException($"05C1 CSV row {csvRow} has an invalid project-owned destination.");
            }

            _ = definition.CellIndex;
        }

        private static IReadOnlyList<WorldVoidProfileStation> ParseStations(string serialized, int csvRow)
        {
            var result = new List<WorldVoidProfileStation>();
            foreach (string station in serialized.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] values = station.Split(':');
                if (values.Length != 5)
                {
                    throw new InvalidDataException($"05C1 CSV row {csvRow} has invalid station: {station}");
                }

                result.Add(new WorldVoidProfileStation(
                    ParseFloat(values[0], "StationZ", csvRow),
                    ParseFloat(values[1], "OuterX", csvRow),
                    ParseFloat(values[2], "OuterY", csvRow),
                    ParseFloat(values[3], "InnerX", csvRow),
                    ParseFloat(values[4], "InnerY", csvRow)));
            }

            return result;
        }

        private static float ParseFloat(string value, string field, int csvRow)
        {
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ||
                !float.IsFinite(parsed))
            {
                throw new InvalidDataException($"05C1 CSV row {csvRow} has invalid {field}: {value}");
            }

            return parsed;
        }

        private static List<IReadOnlyList<string>> ParseCsv(string text)
        {
            var records = new List<IReadOnlyList<string>>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool quoted = false;
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (character == '"')
                {
                    if (quoted && index + 1 < text.Length && text[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = !quoted;
                    }
                }
                else if (character == ',' && !quoted)
                {
                    row.Add(field.ToString());
                    field.Clear();
                }
                else if ((character == '\n' || character == '\r') && !quoted)
                {
                    if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                    {
                        index++;
                    }

                    row.Add(field.ToString());
                    field.Clear();
                    if (row.Any(value => value.Length > 0))
                    {
                        records.Add(row.ToArray());
                    }

                    row.Clear();
                }
                else
                {
                    field.Append(character);
                }
            }

            if (quoted)
            {
                throw new InvalidDataException("05C1 CSV contains an unterminated quoted field.");
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                records.Add(row.ToArray());
            }

            return records;
        }

        private static void SetStableId(GameObject target, string stableId)
        {
            StableEntityIdAuthoring identity = GetOrAddComponent<StableEntityIdAuthoring>(target);
            var serialized = new SerializedObject(identity);
            serialized.FindProperty("stableId").stringValue = stableId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null ? component : target.AddComponent<T>();
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string folder = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
            {
                throw new InvalidDataException("Asset path has no folder: " + assetPath);
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

        private static string ToAbsoluteProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ??
                                 throw new InvalidOperationException("Unity project root is unavailable.");
            string candidate = Path.GetFullPath(Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string boundary = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("05C1 path escapes the project root: " + relativePath);
            }

            return candidate;
        }

        private static string Sha256(string value)
        {
            using SHA256 sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(Encoding.UTF8.GetBytes(value)))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }
    }
}
