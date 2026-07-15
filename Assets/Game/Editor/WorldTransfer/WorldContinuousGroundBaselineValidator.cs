using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MSC.Core.Identity;
using MSC.World.Partition;
using MSC.World.Remaster;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldTransfer
{
    public sealed class WorldContinuousGroundBaselineValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public int RegionCount { get; internal set; }
        public int PieceCount { get; internal set; }
        public int CellCount { get; internal set; }
        public int MeshAssetCount { get; internal set; }
        public int SceneAssetCount { get; internal set; }
        public int GeometryVertexCount { get; internal set; }
        public int GeometryTriangleCount { get; internal set; }
        public int SeamVertexPairCount { get; internal set; }
        public int ColliderRaycastSampleCount { get; internal set; }
        public int ColliderRaycastHitCount { get; internal set; }
        public float MaximumObservedSlopeDegrees { get; internal set; }
        public bool IsValid => Errors.Count == 0;
    }

    /// <summary>
    /// Validates the bounded 05C1 Teimo safety-topology baseline against its
    /// approved source table and the deterministic builder output.
    /// </summary>
    public static class WorldContinuousGroundBaselineValidator
    {
        public const string ExpectedRegionId = "M05C1-VOID-TEIMO-001";
        public const int ExpectedRegionCount = 1;
        public const int ExpectedPieceCount = 2;
        public const int ExpectedCellCount = 2;
        public const int ExpectedGeometryVertexCount = 104;
        public const int ExpectedGeometryTriangleCount = 100;
        public const int ExpectedSeamVertexPairCount = 26;
        public const int ExpectedColliderRaycastSampleCount = 100;
        public const float SeamWorldX = -1536f;
        public const float MaximumAllowedSlopeDegrees = 15f;

        private const string ApprovedImplementationStatus = "ApprovedBoundedPilot";
        private const string ExpectedWaterMaskStatus = "LakebedAabbNonOverlap";
        private const float GeometryToleranceMeters = 0.0001f;
        private const float UvTolerance = 0.0001f;
        private const float NormalMagnitudeTolerance = 0.001f;
        private const float RaycastStartHeightMeters = 10f;
        private const float RaycastDistanceMeters = 20f;
        private const float RaycastPointToleranceMeters = 0.02f;

        private static readonly HashSet<string> ExpectedCells = new HashSet<string>(StringComparer.Ordinal)
        {
            "cell_-4_0",
            "cell_-3_0"
        };

        private static readonly string[] ForbiddenDependencyRoots =
        {
            "Assets/Game/LegacyImport/ReferenceOnly/",
            "Assets/Game/Imported/DonorGenerated/"
        };

        [MenuItem("Tools/MSC Remake/World Transfer/05C1/Validate Teimo Continuous Ground Baseline")]
        public static void ValidateFromMenu() => RunBatch();

        public static WorldContinuousGroundBaselineValidationResult Validate()
        {
            var result = new WorldContinuousGroundBaselineValidationResult();
            WorldVoidRegionPieceDefinition[] approved;
            try
            {
                approved = WorldContinuousGroundBaselineBuilder.LoadDefinitions()
                    .Where(definition => string.Equals(
                        definition.ImplementationStatus,
                        ApprovedImplementationStatus,
                        StringComparison.Ordinal))
                    .OrderBy(definition => definition.CellIndex.X)
                    .ThenBy(definition => definition.CellIndex.Z)
                    .ToArray();
            }
            catch (Exception exception)
            {
                AddError(result, "05C1 approved definition parsing failed: " + exception.Message);
                return result;
            }

            ValidateApprovedDefinitions(approved, result);

            Material terrainMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                WorldContinuousGroundBaselineBuilder.TerrainMaterialPath);
            if (terrainMaterial == null)
            {
                AddError(result, "Project-owned 05C1 terrain material is missing: " +
                                 WorldContinuousGroundBaselineBuilder.TerrainMaterialPath);
            }
            else
            {
                ValidateProjectOwnedDependencies(
                    WorldContinuousGroundBaselineBuilder.TerrainMaterialPath,
                    result);
            }

            var snapshots = new List<PieceSnapshot>();
            var actualStableIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorldVoidRegionPieceDefinition definition in approved)
            {
                ValidateDestinationPaths(definition, result);

                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(definition.MeshAssetPath);
                if (mesh == null)
                {
                    AddError(result, $"05C1 mesh asset is missing for {definition.PieceId}: {definition.MeshAssetPath}");
                }
                else
                {
                    result.MeshAssetCount++;
                    ValidateProjectOwnedDependencies(definition.MeshAssetPath, result);
                }

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(definition.SceneAssetPath) == null)
                {
                    AddError(result, $"05C1 generated scene is missing for {definition.PieceId}: {definition.SceneAssetPath}");
                    continue;
                }

                result.SceneAssetCount++;
                ValidateProjectOwnedDependencies(definition.SceneAssetPath, result);
                if (mesh == null || terrainMaterial == null)
                {
                    continue;
                }

                PieceSnapshot snapshot = ValidateGeneratedPieceScene(
                    definition,
                    mesh,
                    terrainMaterial,
                    actualStableIds,
                    result);
                if (snapshot != null)
                {
                    snapshots.Add(snapshot);
                }
            }

            if (actualStableIds.Count != approved.Length)
            {
                AddError(result,
                    $"Generated 05C1 stable-ID coverage is {actualStableIds.Count}; expected {approved.Length} unique IDs.");
            }

            ValidateSeam(snapshots, result);
            RequireCount(result, "mesh assets", result.MeshAssetCount, ExpectedPieceCount);
            RequireCount(result, "generated scene assets", result.SceneAssetCount, ExpectedPieceCount);
            RequireCount(result, "geometry vertices", result.GeometryVertexCount, ExpectedGeometryVertexCount);
            RequireCount(result, "geometry triangles", result.GeometryTriangleCount, ExpectedGeometryTriangleCount);
            RequireCount(result, "seam vertex pairs", result.SeamVertexPairCount, ExpectedSeamVertexPairCount);
            RequireCount(result,
                "collider raycast samples",
                result.ColliderRaycastSampleCount,
                ExpectedColliderRaycastSampleCount);
            if (result.ColliderRaycastHitCount != result.ColliderRaycastSampleCount)
            {
                AddError(result,
                    $"Collider raycast coverage is {result.ColliderRaycastHitCount}/{result.ColliderRaycastSampleCount}; expected full coverage.");
            }

            return result;
        }

        public static void RunBatch()
        {
            WorldContinuousGroundBaselineValidationResult result = Validate();
            foreach (string warning in result.Warnings)
            {
                Debug.LogWarning("WORLD_MAP_05C1_WARNING " + warning);
            }

            if (!result.IsValid)
            {
                throw new InvalidOperationException(
                    "WORLD_MAP_05C1_INVALID\n" + string.Join("\n", result.Errors));
            }

            Debug.Log(
                $"WORLD_MAP_05C1_VALID regions={result.RegionCount} pieces={result.PieceCount} " +
                $"cells={result.CellCount} vertices={result.GeometryVertexCount} " +
                $"triangles={result.GeometryTriangleCount} seamPairs={result.SeamVertexPairCount} " +
                $"raycasts={result.ColliderRaycastHitCount}/{result.ColliderRaycastSampleCount} " +
                $"maxSlope={result.MaximumObservedSlopeDegrees.ToString("0.###", CultureInfo.InvariantCulture)}");
        }

        private static void ValidateApprovedDefinitions(
            IReadOnlyList<WorldVoidRegionPieceDefinition> approved,
            WorldContinuousGroundBaselineValidationResult result)
        {
            result.PieceCount = approved.Count;
            result.RegionCount = approved
                .Select(definition => definition.RegionId)
                .Distinct(StringComparer.Ordinal)
                .Count();
            result.CellCount = approved
                .Select(definition => definition.CellId)
                .Distinct(StringComparer.Ordinal)
                .Count();

            RequireCount(result, "approved regions", result.RegionCount, ExpectedRegionCount);
            RequireCount(result, "approved pieces", result.PieceCount, ExpectedPieceCount);
            RequireCount(result, "approved cells", result.CellCount, ExpectedCellCount);

            HashSet<string> regionIds = approved
                .Select(definition => definition.RegionId)
                .ToHashSet(StringComparer.Ordinal);
            if (!regionIds.SetEquals(new[] { ExpectedRegionId }))
            {
                AddError(result,
                    "Approved 05C1 region set differs from the bounded Teimo pilot: " +
                    string.Join(", ", regionIds.OrderBy(value => value, StringComparer.Ordinal)));
            }

            HashSet<string> cellIds = approved
                .Select(definition => definition.CellId)
                .ToHashSet(StringComparer.Ordinal);
            if (!cellIds.SetEquals(ExpectedCells))
            {
                AddError(result,
                    "Approved 05C1 cell set differs from cell_-4_0 + cell_-3_0: " +
                    string.Join(", ", cellIds.OrderBy(value => value, StringComparer.Ordinal)));
            }

            if (approved.Select(definition => definition.PieceId)
                    .Distinct(StringComparer.Ordinal).Count() != approved.Count)
            {
                AddError(result, "Approved 05C1 definitions contain duplicate piece IDs.");
            }

            foreach (WorldVoidRegionPieceDefinition definition in approved)
            {
                if (definition.Classification != WorldVoidRegionClassification.IntentionalDonorVoid)
                {
                    AddError(result, $"{definition.PieceId} is not classified as IntentionalDonorVoid.");
                }

                if (!string.Equals(
                        definition.WaterMaskStatus,
                        ExpectedWaterMaskStatus,
                        StringComparison.Ordinal))
                {
                    AddError(result,
                        $"{definition.PieceId} water-mask status is '{definition.WaterMaskStatus}'; expected {ExpectedWaterMaskStatus}.");
                }

                if (!StableEntityId.TryParse(definition.StableEntityId, out _))
                {
                    AddError(result, $"{definition.PieceId} has an invalid project-owned stable ID.");
                }

                if (!string.Equals(
                        definition.DeclaredAuthoringFingerprintSha256,
                        definition.ComputedAuthoringFingerprintSha256,
                        StringComparison.Ordinal))
                {
                    AddError(result, $"{definition.PieceId} authoring fingerprint is stale.");
                }
            }
        }

        private static void ValidateDestinationPaths(
            WorldVoidRegionPieceDefinition definition,
            WorldContinuousGroundBaselineValidationResult result)
        {
            if (!definition.MeshAssetPath.StartsWith(
                    "Assets/Game/World/Production/",
                    StringComparison.Ordinal))
            {
                AddError(result, $"{definition.PieceId} mesh is not project-owned production content: {definition.MeshAssetPath}");
            }

            if (!definition.SceneAssetPath.StartsWith(
                    "Assets/Game/World/Generated/VoidFillCells/",
                    StringComparison.Ordinal))
            {
                AddError(result, $"{definition.PieceId} scene is outside the isolated void-fill root: {definition.SceneAssetPath}");
            }
        }

        private static void ValidateProjectOwnedDependencies(
            string assetPath,
            WorldContinuousGroundBaselineValidationResult result)
        {
            string[] dependencies;
            try
            {
                dependencies = AssetDatabase.GetDependencies(assetPath, true);
            }
            catch (Exception exception)
            {
                AddError(result, $"Could not inspect dependencies for {assetPath}: {exception.Message}");
                return;
            }

            foreach (string dependency in dependencies)
            {
                foreach (string forbiddenRoot in ForbiddenDependencyRoots)
                {
                    if (dependency.StartsWith(forbiddenRoot, StringComparison.Ordinal))
                    {
                        AddError(result,
                            $"Project-owned 05C1 asset '{assetPath}' depends on donor/reference content: {dependency}");
                    }
                }

                bool recognizedProjectPath =
                    dependency.StartsWith("Assets/", StringComparison.Ordinal) ||
                    dependency.StartsWith("Packages/", StringComparison.Ordinal) ||
                    string.Equals(dependency, "Resources/unity_builtin_extra", StringComparison.Ordinal) ||
                    string.Equals(dependency, "Library/unity default resources", StringComparison.Ordinal);
                if (!recognizedProjectPath)
                {
                    AddError(result,
                        $"05C1 asset '{assetPath}' has an unrecognized non-project dependency: {dependency}");
                }
            }
        }

        private static PieceSnapshot ValidateGeneratedPieceScene(
            WorldVoidRegionPieceDefinition definition,
            Mesh expectedMeshAsset,
            Material expectedMaterial,
            ISet<string> actualStableIds,
            WorldContinuousGroundBaselineValidationResult result)
        {
            Scene scene = SceneManager.GetSceneByPath(definition.SceneAssetPath);
            bool openedByValidator = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedByValidator)
                {
                    scene = EditorSceneManager.OpenScene(definition.SceneAssetPath, OpenSceneMode.Additive);
                }

                GameObject[] roots = scene.GetRootGameObjects();
                if (roots.Length != 1)
                {
                    AddError(result,
                        $"{definition.PieceId} scene has {roots.Length} roots; expected one deterministic cell root.");
                }

                WorldVoidFillMarker[] markers = roots
                    .SelectMany(root => root.GetComponentsInChildren<WorldVoidFillMarker>(true))
                    .ToArray();
                if (markers.Length != 1)
                {
                    AddError(result,
                        $"{definition.PieceId} scene has {markers.Length} WorldVoidFillMarker components; expected one.");
                    return null;
                }

                WorldVoidFillMarker marker = markers[0];
                GameObject piece = marker.gameObject;
                WorldVoidPieceGeometry expectedGeometry =
                    WorldContinuousGroundBaselineBuilder.CreateGeometry(definition);

                ValidateHierarchyAndTransforms(
                    definition,
                    roots,
                    piece,
                    expectedGeometry,
                    result);
                ValidateMarker(definition, marker, result);
                ValidateStableIdentity(definition, piece, actualStableIds, result);

                MeshFilter[] filters = piece.GetComponents<MeshFilter>();
                MeshRenderer[] renderers = piece.GetComponents<MeshRenderer>();
                MeshCollider[] colliders = piece.GetComponents<MeshCollider>();
                RequireComponentCount(result, definition.PieceId, nameof(MeshFilter), filters.Length, 1);
                RequireComponentCount(result, definition.PieceId, nameof(MeshRenderer), renderers.Length, 1);
                RequireComponentCount(result, definition.PieceId, nameof(MeshCollider), colliders.Length, 1);

                if (piece.GetComponents<Collider>().Length != 1)
                {
                    AddError(result, $"{definition.PieceId} must have exactly one collider.");
                }

                Transform rootTransform = piece.transform.root;
                if (rootTransform.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                {
                    AddError(result, $"{definition.PieceId} cell hierarchy contains a Rigidbody.");
                }

                StaticEditorFlags requiredStaticFlags =
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic;
                StaticEditorFlags actualStaticFlags = GameObjectUtility.GetStaticEditorFlags(piece);
                if ((actualStaticFlags & requiredStaticFlags) != requiredStaticFlags)
                {
                    AddError(result,
                        $"{definition.PieceId} lacks required BatchingStatic/OccludeeStatic flags.");
                }

                if (!piece.activeInHierarchy || !marker.enabled)
                {
                    AddError(result, $"{definition.PieceId} safety topology is inactive.");
                }

                MeshFilter filter = filters.SingleOrDefault();
                MeshRenderer renderer = renderers.SingleOrDefault();
                MeshCollider collider = colliders.SingleOrDefault();
                if (filter == null || renderer == null || collider == null)
                {
                    return null;
                }

                if (filter.sharedMesh != expectedMeshAsset)
                {
                    AddError(result, $"{definition.PieceId} MeshFilter does not use {definition.MeshAssetPath}.");
                }

                if (renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial != expectedMaterial)
                {
                    AddError(result,
                        $"{definition.PieceId} renderer does not use the single project-owned terrain material.");
                }

                if (!collider.enabled || collider.isTrigger || collider.convex ||
                    collider.sharedMesh != expectedMeshAsset || collider.attachedRigidbody != null)
                {
                    AddError(result,
                        $"{definition.PieceId} requires an enabled, non-trigger, non-convex static MeshCollider using its production mesh.");
                }

                Vector3[] worldVertices = ValidateGeometry(
                    definition,
                    piece.transform,
                    expectedMeshAsset,
                    expectedGeometry,
                    result);
                ValidateColliderRaycasts(
                    definition,
                    collider,
                    expectedGeometry,
                    result);
                return new PieceSnapshot(definition, worldVertices);
            }
            catch (Exception exception)
            {
                AddError(result,
                    $"Generated scene validation failed for {definition.PieceId}: {exception.Message}");
                return null;
            }
            finally
            {
                if (openedByValidator && scene.IsValid() && scene.isLoaded &&
                    !EditorSceneManager.CloseScene(scene, true))
                {
                    AddError(result,
                        $"Validator could not close generated scene after checking {definition.PieceId}.");
                }
            }
        }

        private static void ValidateHierarchyAndTransforms(
            WorldVoidRegionPieceDefinition definition,
            IReadOnlyList<GameObject> roots,
            GameObject piece,
            WorldVoidPieceGeometry expectedGeometry,
            WorldContinuousGroundBaselineValidationResult result)
        {
            string expectedRootName = "WR_05C1_VoidFill_" + definition.CellId;
            GameObject root = roots.FirstOrDefault(candidate =>
                string.Equals(candidate.name, expectedRootName, StringComparison.Ordinal));
            if (root == null)
            {
                AddError(result, $"{definition.PieceId} is missing expected root '{expectedRootName}'.");
                root = piece.transform.root.gameObject;
            }

            if (piece.transform.parent != root.transform)
            {
                AddError(result, $"{definition.PieceId} is not a direct child of its cell root.");
            }

            if (!string.Equals(piece.name, definition.DisplayName, StringComparison.Ordinal))
            {
                AddError(result,
                    $"{definition.PieceId} object name is '{piece.name}'; expected '{definition.DisplayName}'.");
            }

            ValidateVector(result, definition.PieceId + " root position", root.transform.position, expectedGeometry.CellOrigin);
            ValidateQuaternion(result, definition.PieceId + " root rotation", root.transform.rotation, Quaternion.identity);
            ValidateVector(result, definition.PieceId + " root scale", root.transform.lossyScale, Vector3.one);
            ValidateVector(result, definition.PieceId + " local position", piece.transform.localPosition, Vector3.zero);
            ValidateQuaternion(result, definition.PieceId + " local rotation", piece.transform.localRotation, Quaternion.identity);
            ValidateVector(result, definition.PieceId + " local scale", piece.transform.localScale, Vector3.one);
        }

        private static void ValidateMarker(
            WorldVoidRegionPieceDefinition definition,
            WorldVoidFillMarker marker,
            WorldContinuousGroundBaselineValidationResult result)
        {
            if (!string.Equals(marker.RegionId, definition.RegionId, StringComparison.Ordinal) ||
                !string.Equals(marker.PieceId, definition.PieceId, StringComparison.Ordinal) ||
                marker.Classification != definition.Classification ||
                !string.Equals(marker.CellId, definition.CellId, StringComparison.Ordinal) ||
                !string.Equals(
                    marker.AuthoringFingerprintSha256,
                    definition.ComputedAuthoringFingerprintSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    marker.BoundaryEvidenceStableIds,
                    definition.BoundaryEvidenceStableIds,
                    StringComparison.Ordinal))
            {
                AddError(result, $"{definition.PieceId} WorldVoidFillMarker metadata differs from the approved definition.");
            }

            if (!marker.SafetyTopologyBaseline || marker.OpensGameplayArea)
            {
                AddError(result,
                    $"{definition.PieceId} marker must remain safety topology and must not open a gameplay area.");
            }
        }

        private static void ValidateStableIdentity(
            WorldVoidRegionPieceDefinition definition,
            GameObject piece,
            ISet<string> actualStableIds,
            WorldContinuousGroundBaselineValidationResult result)
        {
            StableEntityIdAuthoring[] identities = piece.GetComponents<StableEntityIdAuthoring>();
            RequireComponentCount(
                result,
                definition.PieceId,
                nameof(StableEntityIdAuthoring),
                identities.Length,
                1);
            if (identities.Length != 1)
            {
                return;
            }

            StableEntityIdAuthoring identity = identities[0];
            if (!identity.TryGetStableId(out _) ||
                !string.Equals(identity.SerializedId, definition.StableEntityId, StringComparison.Ordinal))
            {
                AddError(result,
                    $"{definition.PieceId} stable ID differs from approved ID {definition.StableEntityId}.");
                return;
            }

            if (!actualStableIds.Add(identity.SerializedId))
            {
                AddError(result, $"Duplicate generated 05C1 stable ID: {identity.SerializedId}");
            }
        }

        private static Vector3[] ValidateGeometry(
            WorldVoidRegionPieceDefinition definition,
            Transform pieceTransform,
            Mesh mesh,
            WorldVoidPieceGeometry expected,
            WorldContinuousGroundBaselineValidationResult result)
        {
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            int[] triangles = mesh.triangles;
            Vector3[] normals = mesh.normals;
            result.GeometryVertexCount += vertices.Length;
            result.GeometryTriangleCount += triangles.Length / 3;

            CompareVectors(definition.PieceId, "vertex", vertices, expected.Vertices, result);
            CompareUvs(definition.PieceId, uv, expected.Uv, result);
            CompareTriangles(definition.PieceId, triangles, expected.Triangles, result);

            string expectedFingerprint = WorldContinuousGroundBaselineBuilder.ComputeGeometryFingerprint(expected);
            string actualFingerprint = WorldContinuousGroundBaselineBuilder.ComputeGeometryFingerprint(
                new WorldVoidPieceGeometry(vertices, uv, triangles, expected.CellOrigin));
            if (!string.Equals(expectedFingerprint, actualFingerprint, StringComparison.Ordinal))
            {
                AddError(result,
                    $"{definition.PieceId} mesh fingerprint differs from deterministic builder geometry.");
            }

            var worldVertices = new Vector3[vertices.Length];
            WorldCellIndex cell = definition.CellIndex;
            float cellMinX = cell.X * WorldContinuousGroundBaselineBuilder.CellSizeMeters;
            float cellMaxX = cellMinX + WorldContinuousGroundBaselineBuilder.CellSizeMeters;
            float cellMinZ = cell.Z * WorldContinuousGroundBaselineBuilder.CellSizeMeters;
            float cellMaxZ = cellMinZ + WorldContinuousGroundBaselineBuilder.CellSizeMeters;
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 world = pieceTransform.TransformPoint(vertices[index]);
                worldVertices[index] = world;
                if (!IsFinite(world))
                {
                    AddError(result, $"{definition.PieceId} vertex {index} is non-finite.");
                    continue;
                }

                if (world.x < cellMinX - GeometryToleranceMeters ||
                    world.x > cellMaxX + GeometryToleranceMeters ||
                    world.z < cellMinZ - GeometryToleranceMeters ||
                    world.z > cellMaxZ + GeometryToleranceMeters)
                {
                    AddError(result,
                        $"{definition.PieceId} vertex {index} lies outside {definition.CellId}: {world}.");
                }
            }

            ValidateNormalsAndSlope(definition.PieceId, pieceTransform, normals, worldVertices, triangles, result);
            return worldVertices;
        }

        private static void ValidateNormalsAndSlope(
            string pieceId,
            Transform pieceTransform,
            IReadOnlyList<Vector3> normals,
            IReadOnlyList<Vector3> worldVertices,
            IReadOnlyList<int> triangles,
            WorldContinuousGroundBaselineValidationResult result)
        {
            if (normals.Count != worldVertices.Count)
            {
                AddError(result,
                    $"{pieceId} has {normals.Count} stored normals for {worldVertices.Count} vertices.");
            }
            else
            {
                for (int index = 0; index < normals.Count; index++)
                {
                    Vector3 normal = normals[index];
                    if (!IsFinite(normal) || Mathf.Abs(normal.magnitude - 1f) > NormalMagnitudeTolerance)
                    {
                        AddError(result, $"{pieceId} stored normal {index} is invalid or not normalized.");
                        continue;
                    }

                    Vector3 worldNormal = pieceTransform.TransformDirection(normal).normalized;
                    float slope = Vector3.Angle(worldNormal, Vector3.up);
                    result.MaximumObservedSlopeDegrees =
                        Mathf.Max(result.MaximumObservedSlopeDegrees, slope);
                    if (worldNormal.y <= 0f || slope > MaximumAllowedSlopeDegrees + GeometryToleranceMeters)
                    {
                        AddError(result,
                            $"{pieceId} stored normal {index} is not upward within {MaximumAllowedSlopeDegrees:0.###} degrees (observed {slope:0.###}).");
                    }
                }
            }

            if (triangles.Count % 3 != 0)
            {
                AddError(result, $"{pieceId} triangle index count is not divisible by three.");
                return;
            }

            for (int index = 0; index < triangles.Count; index += 3)
            {
                int a = triangles[index];
                int b = triangles[index + 1];
                int c = triangles[index + 2];
                if (!IsValidIndex(a, worldVertices.Count) ||
                    !IsValidIndex(b, worldVertices.Count) ||
                    !IsValidIndex(c, worldVertices.Count))
                {
                    AddError(result, $"{pieceId} triangle {index / 3} contains an invalid vertex index.");
                    continue;
                }

                Vector3 cross = Vector3.Cross(
                    worldVertices[b] - worldVertices[a],
                    worldVertices[c] - worldVertices[a]);
                if (!IsFinite(cross) || cross.sqrMagnitude <= 0.00000001f)
                {
                    AddError(result, $"{pieceId} triangle {index / 3} is degenerate.");
                    continue;
                }

                Vector3 faceNormal = cross.normalized;
                float slope = Vector3.Angle(faceNormal, Vector3.up);
                result.MaximumObservedSlopeDegrees =
                    Mathf.Max(result.MaximumObservedSlopeDegrees, slope);
                if (faceNormal.y <= 0f || slope > MaximumAllowedSlopeDegrees + GeometryToleranceMeters)
                {
                    AddError(result,
                        $"{pieceId} triangle {index / 3} is not upward within {MaximumAllowedSlopeDegrees:0.###} degrees (observed {slope:0.###}).");
                }
            }
        }

        private static void ValidateColliderRaycasts(
            WorldVoidRegionPieceDefinition definition,
            MeshCollider collider,
            WorldVoidPieceGeometry expected,
            WorldContinuousGroundBaselineValidationResult result)
        {
            if (!collider.enabled || !collider.gameObject.activeInHierarchy)
            {
                return;
            }

            Physics.SyncTransforms();
            for (int index = 0; index < expected.Triangles.Length; index += 3)
            {
                int a = expected.Triangles[index];
                int b = expected.Triangles[index + 1];
                int c = expected.Triangles[index + 2];
                Vector3 expectedPoint = expected.CellOrigin +
                                        (expected.Vertices[a] + expected.Vertices[b] + expected.Vertices[c]) / 3f;
                result.ColliderRaycastSampleCount++;
                var ray = new Ray(
                    expectedPoint + Vector3.up * RaycastStartHeightMeters,
                    Vector3.down);
                if (!collider.Raycast(ray, out RaycastHit hit, RaycastDistanceMeters))
                {
                    AddError(result,
                        $"{definition.PieceId} MeshCollider misses triangle-centroid sample {index / 3}.");
                    continue;
                }

                result.ColliderRaycastHitCount++;
                if (hit.collider != collider ||
                    Vector3.Distance(hit.point, expectedPoint) > RaycastPointToleranceMeters)
                {
                    AddError(result,
                        $"{definition.PieceId} MeshCollider raycast sample {index / 3} differs from expected geometry.");
                }
            }
        }

        private static void ValidateSeam(
            IReadOnlyList<PieceSnapshot> snapshots,
            WorldContinuousGroundBaselineValidationResult result)
        {
            PieceSnapshot left = snapshots.SingleOrDefault(snapshot =>
                string.Equals(snapshot.Definition.CellId, "cell_-4_0", StringComparison.Ordinal));
            PieceSnapshot right = snapshots.SingleOrDefault(snapshot =>
                string.Equals(snapshot.Definition.CellId, "cell_-3_0", StringComparison.Ordinal));
            if (left == null || right == null)
            {
                AddError(result, "Both 05C1 cell snapshots are required for seam validation at x=-1536.");
                return;
            }

            Vector3[] leftSeam = ExtractStripSide(left.WorldVertices, rightSide: true);
            Vector3[] rightSeam = ExtractStripSide(right.WorldVertices, rightSide: false);
            if (leftSeam.Length != rightSeam.Length)
            {
                AddError(result,
                    $"05C1 seam vertex counts differ: {leftSeam.Length} in cell_-4_0 and {rightSeam.Length} in cell_-3_0.");
            }

            int pairCount = Math.Min(leftSeam.Length, rightSeam.Length);
            result.SeamVertexPairCount = pairCount;
            for (int index = 0; index < pairCount; index++)
            {
                Vector3 a = leftSeam[index];
                Vector3 b = rightSeam[index];
                if (Mathf.Abs(a.x - SeamWorldX) > GeometryToleranceMeters ||
                    Mathf.Abs(b.x - SeamWorldX) > GeometryToleranceMeters)
                {
                    AddError(result,
                        $"05C1 seam pair {index} is not exactly on x={SeamWorldX.ToString("R", CultureInfo.InvariantCulture)} within {GeometryToleranceMeters} m.");
                }

                if (Mathf.Abs(a.y - b.y) > GeometryToleranceMeters ||
                    Mathf.Abs(a.z - b.z) > GeometryToleranceMeters)
                {
                    AddError(result,
                        $"05C1 seam pair {index} has a crack: left={a}, right={b}.");
                }
            }
        }

        private static Vector3[] ExtractStripSide(IReadOnlyList<Vector3> vertices, bool rightSide)
        {
            int offset = rightSide ? 1 : 0;
            var result = new List<Vector3>((vertices.Count + 1) / 2);
            for (int index = offset; index < vertices.Count; index += 2)
            {
                result.Add(vertices[index]);
            }

            return result.OrderBy(value => value.z).ThenBy(value => value.y).ToArray();
        }

        private static void CompareVectors(
            string pieceId,
            string label,
            IReadOnlyList<Vector3> actual,
            IReadOnlyList<Vector3> expected,
            WorldContinuousGroundBaselineValidationResult result)
        {
            if (actual.Count != expected.Count)
            {
                AddError(result,
                    $"{pieceId} {label} count is {actual.Count}; expected {expected.Count}.");
                return;
            }

            for (int index = 0; index < actual.Count; index++)
            {
                if ((actual[index] - expected[index]).sqrMagnitude >
                    GeometryToleranceMeters * GeometryToleranceMeters)
                {
                    AddError(result,
                        $"{pieceId} {label} {index} differs from deterministic builder geometry.");
                }
            }
        }

        private static void CompareUvs(
            string pieceId,
            IReadOnlyList<Vector2> actual,
            IReadOnlyList<Vector2> expected,
            WorldContinuousGroundBaselineValidationResult result)
        {
            if (actual.Count != expected.Count)
            {
                AddError(result,
                    $"{pieceId} UV count is {actual.Count}; expected {expected.Count}.");
                return;
            }

            for (int index = 0; index < actual.Count; index++)
            {
                if ((actual[index] - expected[index]).sqrMagnitude > UvTolerance * UvTolerance)
                {
                    AddError(result, $"{pieceId} UV {index} differs from deterministic builder geometry.");
                }
            }
        }

        private static void CompareTriangles(
            string pieceId,
            IReadOnlyList<int> actual,
            IReadOnlyList<int> expected,
            WorldContinuousGroundBaselineValidationResult result)
        {
            if (actual.Count != expected.Count)
            {
                AddError(result,
                    $"{pieceId} triangle-index count is {actual.Count}; expected {expected.Count}.");
                return;
            }

            for (int index = 0; index < actual.Count; index++)
            {
                if (actual[index] != expected[index])
                {
                    AddError(result,
                        $"{pieceId} triangle index {index} differs from deterministic builder geometry.");
                }
            }
        }

        private static void ValidateVector(
            WorldContinuousGroundBaselineValidationResult result,
            string label,
            Vector3 actual,
            Vector3 expected)
        {
            if (!IsFinite(actual) ||
                (actual - expected).sqrMagnitude > GeometryToleranceMeters * GeometryToleranceMeters)
            {
                AddError(result, $"{label} is {actual}; expected {expected}.");
            }
        }

        private static void ValidateQuaternion(
            WorldContinuousGroundBaselineValidationResult result,
            string label,
            Quaternion actual,
            Quaternion expected)
        {
            if (!IsFinite(actual) || Quaternion.Angle(actual, expected) > GeometryToleranceMeters)
            {
                AddError(result, $"{label} differs from the deterministic builder transform.");
            }
        }

        private static void RequireComponentCount(
            WorldContinuousGroundBaselineValidationResult result,
            string pieceId,
            string componentName,
            int actual,
            int expected)
        {
            if (actual != expected)
            {
                AddError(result,
                    $"{pieceId} has {actual} {componentName} components; expected {expected}.");
            }
        }

        private static void RequireCount(
            WorldContinuousGroundBaselineValidationResult result,
            string label,
            int actual,
            int expected)
        {
            if (actual != expected)
            {
                AddError(result,
                    $"Unexpected {label}: {actual.ToString(CultureInfo.InvariantCulture)}; " +
                    $"expected {expected.ToString(CultureInfo.InvariantCulture)}.");
            }
        }

        private static bool IsValidIndex(int value, int count) => value >= 0 && value < count;

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z) && float.IsFinite(value.w);

        private static void AddError(
            WorldContinuousGroundBaselineValidationResult result,
            string message)
        {
            if (!result.Errors.Contains(message))
            {
                result.Errors.Add(message);
            }
        }

        private sealed class PieceSnapshot
        {
            public PieceSnapshot(
                WorldVoidRegionPieceDefinition definition,
                Vector3[] worldVertices)
            {
                Definition = definition;
                WorldVertices = worldVertices;
            }

            public WorldVoidRegionPieceDefinition Definition { get; }
            public Vector3[] WorldVertices { get; }
        }
    }
}
