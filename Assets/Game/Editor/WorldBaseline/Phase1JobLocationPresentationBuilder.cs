using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MSC.Editor.WorldTransfer;
using MSC.LegacyImport;
using MSC.World.Data;
using MSC.World.Partition;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldBaseline
{
    public sealed class Phase1JobLocationRendererRecord
    {
        public Phase1JobLocationRendererRecord(
            WorldEntityPlacement placement,
            string[] materialGuids,
            int[] staticBatchSubMeshIndices,
            string cellId)
        {
            Placement = placement;
            MaterialGuids = materialGuids;
            StaticBatchSubMeshIndices = staticBatchSubMeshIndices;
            CellId = cellId;
        }

        public WorldEntityPlacement Placement { get; }
        public string[] MaterialGuids { get; }
        public int[] StaticBatchSubMeshIndices { get; }
        public string CellId { get; }
        public bool IsStaticBatchSubset =>
            StaticBatchSubMeshIndices.Length > 0;
    }

    public sealed class Phase1JobLocationColliderRecord
    {
        public string StableId { get; internal set; } = string.Empty;
        public string EntityStableId { get; internal set; } = string.Empty;
        public string ColliderType { get; internal set; } = string.Empty;
        public string MeshGuid { get; internal set; } = string.Empty;
        public Vector3 Center { get; internal set; }
        public Vector3 Size { get; internal set; }
        public float Radius { get; internal set; }
        public float Height { get; internal set; }
        public int Direction { get; internal set; }
    }

    public sealed class Phase1JobLocationPresentationPlan
    {
        public const string ManifestPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "Phase1JobLocationPresentationManifest.json";
        public const string GeneratedRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/" +
            "Phase1JobLocations";
        public const string OverlayRootName =
            "PHASE1_JOB_LOCATIONS_TEMPORARY_DIRECT_IMPORT";

        private const int MeshRendererClassId = 23;
        private const int MeshFilterClassId = 33;

        private Phase1JobLocationPresentationPlan(
            Phase1JobLocationManifest manifest,
            Phase1JobLocationRendererRecord[] renderers,
            Phase1JobLocationColliderRecord[] colliders,
            IReadOnlyDictionary<string, WorldEntityPlacement> placements)
        {
            Manifest = manifest;
            Renderers = renderers;
            Colliders = colliders;
            Placements = placements;
        }

        internal Phase1JobLocationManifest Manifest { get; }
        public string ManifestId => Manifest.manifestId;
        public IReadOnlyList<Phase1JobLocationRendererRecord> Renderers
        {
            get;
        }
        public IReadOnlyList<Phase1JobLocationColliderRecord> Colliders
        {
            get;
        }
        public IReadOnlyDictionary<string, WorldEntityPlacement> Placements
        {
            get;
        }
        public IReadOnlyList<string> CellIds =>
            Manifest.expectedCellIds;

        public static Phase1JobLocationPresentationPlan Load()
        {
            Phase1JobLocationManifest manifest = LoadManifest();
            string entityTablePath = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldTransferPaths.EntityTableAssetPath);
            string entityCsv = File.ReadAllText(entityTablePath);
            RequireHash(
                entityTablePath,
                manifest.worldEntityTableSha256,
                "world entity table");

            IReadOnlyList<WorldEntityPlacement> allPlacements =
                WorldEntityTable.Parse(entityCsv);
            IReadOnlyDictionary<string, EntitySourceMetadata> sourceMetadata =
                ParseEntitySourceMetadata(entityCsv);
            WorldEntityPlacement[] selectedPlacements = allPlacements
                .Where(placement => IsSelectedPath(
                    manifest,
                    placement.HierarchyPath))
                .ToArray();
            Dictionary<string, WorldEntityPlacement> placementById =
                selectedPlacements.ToDictionary(
                    placement => placement.StableId,
                    StringComparer.Ordinal);

            IReadOnlyDictionary<long, int[]> allStaticBatchSubsets =
                WorldStaticBatchSubsetTable.ParseFrozenScene();
            Phase1JobLocationRendererRecord[] renderers =
                selectedPlacements
                    .Where(placement =>
                    {
                        EntitySourceMetadata metadata =
                            sourceMetadata[placement.StableId];
                        return placement.Active &&
                               WorldReferenceMeshLibrarySync.IsUsableMeshGuid(
                                   placement.MeshGuid) &&
                               metadata.ComponentClassIds.Contains(
                                   MeshRendererClassId) &&
                               metadata.ComponentClassIds.Contains(
                                   MeshFilterClassId);
                    })
                    .Select(placement =>
                    {
                        EntitySourceMetadata metadata =
                            sourceMetadata[placement.StableId];
                        int[] subsets = allStaticBatchSubsets.TryGetValue(
                            placement.SourceObjectId,
                            out int[] values)
                                ? values
                                : Array.Empty<int>();
                        string cellId = WorldCellMembershipUtility.FromPosition(
                            placement.Position,
                            WorldBaseline06B2Paths.CellSizeMeters).Id;
                        return new Phase1JobLocationRendererRecord(
                            placement,
                            metadata.MaterialGuids,
                            subsets,
                            cellId);
                    })
                    .OrderBy(record => record.Placement.StableId,
                        StringComparer.Ordinal)
                    .ToArray();

            string colliderTablePath =
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    WorldTransferPaths.ColliderTableAssetPath);
            RequireHash(
                colliderTablePath,
                manifest.worldColliderTableSha256,
                "world collider table");
            Phase1JobLocationColliderRecord[] colliders = ParseColliders(
                    File.ReadAllText(colliderTablePath),
                    placementById,
                    manifest)
                .OrderBy(record => record.StableId, StringComparer.Ordinal)
                .ToArray();

            ValidatePlan(manifest, renderers, colliders, placementById);
            return new Phase1JobLocationPresentationPlan(
                manifest,
                renderers,
                colliders,
                placementById);
        }

        public static bool IsSelectedHierarchyPath(string hierarchyPath)
        {
            return IsSelectedPath(LoadManifest(), hierarchyPath);
        }

        private static void ValidatePlan(
            Phase1JobLocationManifest manifest,
            IReadOnlyList<Phase1JobLocationRendererRecord> renderers,
            IReadOnlyList<Phase1JobLocationColliderRecord> colliders,
            IReadOnlyDictionary<string, WorldEntityPlacement> placements)
        {
            string[] cells = renderers
                .Select(record => record.CellId)
                .Concat(colliders.Select(record =>
                    WorldCellMembershipUtility.FromPosition(
                        placements[record.EntityStableId].Position,
                        WorldBaseline06B2Paths.CellSizeMeters).Id))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] expectedCells = manifest.expectedCellIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            int staticBatchCount = renderers.Count(record =>
                record.IsStaticBatchSubset);
            string[] doubleSidedIds = manifest.doubleSidedRendererStableIds ??
                Array.Empty<string>();
            if (renderers.Count != manifest.expectedRendererCount ||
                staticBatchCount !=
                    manifest.expectedStaticBatchRendererCount ||
                colliders.Count != manifest.expectedColliderCount ||
                !cells.SequenceEqual(expectedCells, StringComparer.Ordinal) ||
                renderers.Any(record =>
                    record.MaterialGuids.Length == 0 ||
                    record.StaticBatchSubMeshIndices.Any(index => index < 0) ||
                    (record.IsStaticBatchSubset &&
                     record.MaterialGuids.Length !=
                     record.StaticBatchSubMeshIndices.Length)) ||
                colliders.Any(record =>
                    !placements.ContainsKey(record.EntityStableId)) ||
                doubleSidedIds.Distinct(StringComparer.Ordinal).Count() !=
                    doubleSidedIds.Length ||
                doubleSidedIds.Any(id => !renderers.Any(record =>
                    string.Equals(
                        record.Placement.StableId,
                        id,
                        StringComparison.Ordinal))))
            {
                throw new InvalidDataException(
                    "Phase 1 job-location presentation selection drifted: " +
                    $"renderers={renderers.Count}, " +
                    $"staticBatches={staticBatchCount}, " +
                    $"colliders={colliders.Count}, " +
                    $"cells=[{string.Join(",", cells)}].");
            }
        }

        private static IReadOnlyDictionary<string, EntitySourceMetadata>
            ParseEntitySourceMetadata(string csv)
        {
            using var reader = new StringReader(csv);
            List<string> headers = WorldEntityTable.ParseRow(
                reader.ReadLine() ?? string.Empty);
            int stableIdIndex = headers.IndexOf("StableId");
            int componentsIndex = headers.IndexOf("ComponentClassIds");
            int materialsIndex = headers.IndexOf("MaterialGuids");
            if (stableIdIndex < 0 || componentsIndex < 0 ||
                materialsIndex < 0)
            {
                throw new FormatException(
                    "World entity table lacks supplemental selection columns.");
            }

            var result = new Dictionary<string, EntitySourceMetadata>(
                StringComparer.Ordinal);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                List<string> values = WorldEntityTable.ParseRow(line);
                if (values.Count != headers.Count)
                {
                    throw new FormatException(
                        "World entity table contains an invalid row.");
                }
                int[] componentIds = values[componentsIndex]
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => int.Parse(
                        value,
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture))
                    .ToArray();
                string[] materialGuids = values[materialsIndex]
                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                    .ToArray();
                result.Add(
                    values[stableIdIndex],
                    new EntitySourceMetadata(componentIds, materialGuids));
            }
            return result;
        }

        private static IEnumerable<Phase1JobLocationColliderRecord>
            ParseColliders(
                string csv,
                IReadOnlyDictionary<string, WorldEntityPlacement> placements,
                Phase1JobLocationManifest manifest)
        {
            var expectedEmptyMeshColliders = new HashSet<string>(
                manifest.ignoredEmptyMeshColliderStableIds ??
                    Array.Empty<string>(),
                StringComparer.Ordinal);
            var encounteredEmptyMeshColliders = new HashSet<string>(
                StringComparer.Ordinal);
            using var reader = new StringReader(csv);
            List<string> headers = WorldEntityTable.ParseRow(
                reader.ReadLine() ?? string.Empty);
            var indices = headers
                .Select((name, index) => (name, index))
                .ToDictionary(value => value.name, value => value.index,
                    StringComparer.Ordinal);
            string[] required =
            {
                "StableId", "EntityStableId", "ColliderType", "Enabled",
                "IsTrigger", "MeshGuid", "CenterX", "CenterY", "CenterZ",
                "SizeX", "SizeY", "SizeZ", "Radius", "Height", "Direction"
            };
            if (required.Any(name => !indices.ContainsKey(name)))
            {
                throw new FormatException(
                    "World collider table lacks supplemental selection columns.");
            }

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                List<string> values = WorldEntityTable.ParseRow(line);
                if (values.Count != headers.Count)
                {
                    throw new FormatException(
                        "World collider table contains an invalid row.");
                }
                string Get(string name) => values[indices[name]];
                float Float(string name) => float.Parse(
                    Get(name),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture);
                string entityId = Get("EntityStableId");
                string type = Get("ColliderType");
                if (!placements.ContainsKey(entityId) ||
                    Get("Enabled") != "1" ||
                    Get("IsTrigger") != "0" ||
                    type is not ("MeshCollider" or "BoxCollider" or
                        "CapsuleCollider"))
                {
                    continue;
                }

                string meshGuid = Get("MeshGuid");
                if (type == "MeshCollider" &&
                    !WorldReferenceMeshLibrarySync.IsUsableMeshGuid(meshGuid))
                {
                    string stableId = Get("StableId");
                    if (expectedEmptyMeshColliders.Contains(stableId))
                    {
                        encounteredEmptyMeshColliders.Add(stableId);
                        continue;
                    }
                    throw new InvalidDataException(
                        "Selected job-location MeshCollider has no usable mesh: " +
                        stableId);
                }

                yield return new Phase1JobLocationColliderRecord
                {
                    StableId = Get("StableId"),
                    EntityStableId = entityId,
                    ColliderType = type,
                    MeshGuid = meshGuid,
                    Center = new Vector3(
                        Float("CenterX"), Float("CenterY"), Float("CenterZ")),
                    Size = new Vector3(
                        Float("SizeX"), Float("SizeY"), Float("SizeZ")),
                    Radius = Float("Radius"),
                    Height = Float("Height"),
                    Direction = int.Parse(
                        Get("Direction"),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture)
                };
            }

            if (!encounteredEmptyMeshColliders.SetEquals(
                    expectedEmptyMeshColliders))
            {
                throw new InvalidDataException(
                    "Audited empty job-location MeshCollider selection " +
                    "drifted: expected=[" +
                    string.Join(",", expectedEmptyMeshColliders) +
                    "], encountered=[" +
                    string.Join(",", encounteredEmptyMeshColliders) + "].");
            }
        }

        private static Phase1JobLocationManifest LoadManifest()
        {
            string path = WorldBaselinePaths.ToAbsoluteProjectPath(
                ManifestPath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Phase 1 job-location manifest is missing.", path);
            }
            Phase1JobLocationManifest manifest =
                JsonUtility.FromJson<Phase1JobLocationManifest>(
                    File.ReadAllText(path));
            if (manifest == null || manifest.schemaVersion != 1 ||
                string.IsNullOrWhiteSpace(manifest.manifestId) ||
                manifest.classification != "TemporaryDirectImport" ||
                manifest.sourceRevisionId != WorldBaselinePaths.SourceRevisionId ||
                manifest.sourceSceneSha256 != WorldBaselinePaths.SourceSceneSha256 ||
                manifest.includeHierarchyRoots == null ||
                manifest.includeHierarchyRoots.Length != 14 ||
                manifest.excludeHierarchyFragments == null ||
                manifest.expectedCellIds == null ||
                manifest.doubleSidedRendererStableIds == null ||
                manifest.supplementalMaterials == null)
            {
                throw new InvalidDataException(
                    "Phase 1 job-location manifest is malformed.");
            }
            return manifest;
        }

        private static bool IsSelectedPath(
            Phase1JobLocationManifest manifest,
            string hierarchyPath)
        {
            if (string.IsNullOrWhiteSpace(hierarchyPath) ||
                !manifest.includeHierarchyRoots.Any(root =>
                    hierarchyPath.StartsWith(
                        root,
                        StringComparison.Ordinal)))
            {
                return false;
            }
            return !manifest.excludeHierarchyFragments.Any(fragment =>
                hierarchyPath.IndexOf(
                    fragment,
                    StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void RequireHash(
            string path,
            string expected,
            string label)
        {
            string actual = DonorWorldBaselineManifest.ComputeFileSha256(path);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Frozen {label} hash differs: expected {expected}, got {actual}.");
            }
        }

        private readonly struct EntitySourceMetadata
        {
            public EntitySourceMetadata(
                int[] componentClassIds,
                string[] materialGuids)
            {
                ComponentClassIds = componentClassIds;
                MaterialGuids = materialGuids;
            }

            public int[] ComponentClassIds { get; }
            public string[] MaterialGuids { get; }
        }
    }

    public static class Phase1JobLocationPresentationBuilder
    {
        private const string MeshRoot =
            Phase1JobLocationPresentationPlan.GeneratedRoot + "/Meshes";
        private const string SourceMeshRoot = MeshRoot + "/Source";
        private const string DerivedMeshRoot = MeshRoot + "/Derived";
        private const string CollisionMeshRoot = MeshRoot + "/Collision";
        private const string MaterialRoot =
            Phase1JobLocationPresentationPlan.GeneratedRoot + "/Materials";
        private const string TextureRoot =
            Phase1JobLocationPresentationPlan.GeneratedRoot + "/Textures";
        private const string ExtractedAssetsRelativePath =
            "assetripper-unity-project/ExportedProject/Assets";

        [MenuItem(
            "Tools/MSC Remake/Phase 1/Build Missing Job Locations")]
        public static void BuildFromMenu()
        {
            Build();
        }

        public static void RunBatch()
        {
            Build();
        }

        public static void Build()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException(
                    "Phase 1 job-location build was cancelled to preserve unsaved scenes.");
            }

            Phase1JobLocationPresentationPlan plan =
                Phase1JobLocationPresentationPlan.Load();
            SynchronizeReferenceMeshes(plan);
            ResetGeneratedAssets();
            IReadOnlyDictionary<string, Mesh> renderMeshes =
                BuildRenderMeshes(plan);
            IReadOnlyDictionary<string, Mesh> collisionMeshes =
                BuildCollisionMeshes(plan);
            IReadOnlyDictionary<string, Material> supplementalMaterials =
                BuildSupplementalMaterials(plan);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            BuildCellOverlays(
                plan,
                renderMeshes,
                collisionMeshes,
                supplementalMaterials);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            ValidateGenerated(plan);

            Debug.Log(
                "PHASE1_JOB_LOCATIONS_BUILD_OK " +
                $"renderers={plan.Renderers.Count} " +
                $"colliders={plan.Colliders.Count} " +
                $"cells={plan.CellIds.Count} " +
                $"manifest={plan.ManifestId}");
        }

        public static void ValidateGenerated(
            Phase1JobLocationPresentationPlan plan = null)
        {
            plan ??= Phase1JobLocationPresentationPlan.Load();
            int rendererCount = 0;
            int colliderCount = 0;
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string cellId in plan.CellIds)
            {
                string scenePath = WorldBaseline06B2Paths.CellScene(cellId);
                Scene scene = EditorSceneManager.OpenPreviewScene(scenePath);
                try
                {
                    DonorWorldSupplementalEntityMetadata[] entities = scene
                        .GetRootGameObjects()
                        .SelectMany(root => root.GetComponentsInChildren<
                            DonorWorldSupplementalEntityMetadata>(true))
                        .ToArray();
                    foreach (DonorWorldSupplementalEntityMetadata entity in
                             entities)
                    {
                        if (!stableIds.Add(entity.StableId) ||
                            entity.ManifestId != plan.ManifestId ||
                            entity.SourceCellId != cellId ||
                            !plan.Placements.ContainsKey(entity.StableId))
                        {
                            throw new InvalidDataException(
                                "Generated job-location supplemental metadata drifted in " +
                                scenePath + ".");
                        }
                        rendererCount += entity.HasSanitizedRenderer ? 1 : 0;
                        colliderCount += entity.SanitizedColliderCount;
                    }
                    string[] dependencies = AssetDatabase.GetDependencies(
                        scenePath,
                        recursive: true);
                    if (dependencies.Any(path => path.StartsWith(
                            WorldTransferPaths.ReferenceRoot + "/",
                            StringComparison.Ordinal)))
                    {
                        throw new InvalidDataException(
                            "Generated job-location scene depends on ReferenceOnly: " +
                            scenePath);
                    }
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }

            if (rendererCount != plan.Renderers.Count ||
                colliderCount != plan.Colliders.Count)
            {
                throw new InvalidDataException(
                    "Generated job-location overlay count drift: " +
                    $"renderers={rendererCount}, colliders={colliderCount}.");
            }

            DonorWorldCellizationValidationResult baseline =
                DonorWorldCellizationValidator.Validate(
                    verifySourceHashes: false,
                    inspectAllGeneratedScenes: true);
            if (!baseline.Passed)
            {
                Debug.LogWarning(
                    "PHASE1_JOB_LOCATIONS_BASELINE_DRIFT " +
                    "The supplemental overlay passed its own closure checks, " +
                    "but the pre-existing active world profile still has " +
                    "independent validation debt:\n- " +
                    string.Join("\n- ", baseline.Errors));
            }
        }

        private static void SynchronizeReferenceMeshes(
            Phase1JobLocationPresentationPlan plan)
        {
            string[] guids = plan.Renderers
                .Select(record => record.Placement.MeshGuid)
                .Concat(plan.Colliders
                    .Where(record => record.ColliderType == "MeshCollider")
                    .Select(record => record.MeshGuid))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            WorldReferenceMeshSyncResult result =
                WorldReferenceMeshLibrarySync.SynchronizeGuids(guids);
            if (result.MissingGuids.Count != 0 ||
                result.ResolvedAssetCount != guids.Length)
            {
                throw new InvalidDataException(
                    "Phase 1 job-location mesh closure is incomplete: " +
                    string.Join(", ", result.MissingGuids));
            }
        }

        private static void ResetGeneratedAssets()
        {
            if (AssetDatabase.IsValidFolder(
                    Phase1JobLocationPresentationPlan.GeneratedRoot))
            {
                AssetDatabase.DeleteAsset(
                    Phase1JobLocationPresentationPlan.GeneratedRoot);
            }
            EnsureAssetFolder(SourceMeshRoot);
            EnsureAssetFolder(DerivedMeshRoot);
            EnsureAssetFolder(CollisionMeshRoot);
            EnsureAssetFolder(MaterialRoot);
            EnsureAssetFolder(TextureRoot);
        }

        private static IReadOnlyDictionary<string, Mesh> BuildRenderMeshes(
            Phase1JobLocationPresentationPlan plan)
        {
            var result = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            var directByGuid = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            foreach (Phase1JobLocationRendererRecord record in plan.Renderers)
            {
                Mesh mesh;
                if (record.IsStaticBatchSubset)
                {
                    Mesh source = RequireReferenceMesh(
                        record.Placement.MeshGuid);
                    mesh = BuildStaticBatchSubsetMesh(source, record);
                    string path = DerivedMeshRoot + "/" +
                                  record.Placement.StableId + ".asset";
                    AssetDatabase.CreateAsset(mesh, path);
                }
                else if (!directByGuid.TryGetValue(
                             record.Placement.MeshGuid,
                             out mesh))
                {
                    mesh = CopyReferenceMesh(
                        record.Placement.MeshGuid,
                        SourceMeshRoot + "/" +
                        record.Placement.MeshGuid + ".asset");
                    directByGuid.Add(record.Placement.MeshGuid, mesh);
                }

                result.Add(record.Placement.StableId, mesh);
            }
            return result;
        }

        private static IReadOnlyDictionary<string, Mesh> BuildCollisionMeshes(
            Phase1JobLocationPresentationPlan plan)
        {
            var result = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            foreach (string guid in plan.Colliders
                         .Where(record => record.ColliderType == "MeshCollider")
                         .Select(record => record.MeshGuid)
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(value => value, StringComparer.Ordinal))
            {
                result.Add(
                    guid,
                    CopyReferenceMesh(
                        guid,
                        CollisionMeshRoot + "/" + guid + ".asset"));
            }
            return result;
        }

        private static Mesh CopyReferenceMesh(
            string guid,
            string destination)
        {
            string sourcePath = AssetDatabase.GUIDToAssetPath(guid);
            if (!WorldReferenceMeshLibrarySync.IsBelowReferenceMeshRoot(
                    sourcePath) ||
                AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath) == null)
            {
                throw new InvalidDataException(
                    "Supplemental mesh GUID resolves outside ReferenceOnly: " +
                    guid);
            }
            EnsureAssetFolder(destination);
            if (!AssetDatabase.CopyAsset(sourcePath, destination))
            {
                throw new IOException(
                    "Could not copy supplemental mesh to RuntimeBaseline: " +
                    destination);
            }
            return AssetDatabase.LoadAssetAtPath<Mesh>(destination) ??
                   throw new InvalidDataException(
                       "Copied supplemental mesh is invalid: " + destination);
        }

        private static Mesh RequireReferenceMesh(string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Mesh mesh = WorldReferenceMeshLibrarySync.IsBelowReferenceMeshRoot(path)
                ? AssetDatabase.LoadAssetAtPath<Mesh>(path)
                : null;
            return mesh ?? throw new InvalidDataException(
                "Static-batch source mesh is unavailable: " + guid);
        }

        private static Mesh BuildStaticBatchSubsetMesh(
            Mesh source,
            Phase1JobLocationRendererRecord record)
        {
            int[] subsets = record.StaticBatchSubMeshIndices;
            if (subsets.Length == 0 || subsets.Any(index =>
                    index < 0 || index >= source.subMeshCount))
            {
                throw new InvalidDataException(
                    "Invalid supplemental static-batch subset for " +
                    record.Placement.StableId);
            }

            Vector3[] sourceVertices = source.vertices;
            Vector3[] sourceNormals = source.normals;
            Vector4[] sourceTangents = source.tangents;
            Vector2[] sourceUv = source.uv;
            bool copyNormals = sourceNormals.Length == sourceVertices.Length;
            bool copyTangents = sourceTangents.Length == sourceVertices.Length;
            bool copyUv = sourceUv.Length == sourceVertices.Length;
            Matrix4x4 positionTransform = Matrix4x4.TRS(
                record.Placement.SourcePosition,
                record.Placement.Rotation,
                record.Placement.Scale).inverse;
            Matrix4x4 normalTransform = positionTransform.inverse.transpose;
            float handedness = positionTransform.determinant < 0f ? -1f : 1f;
            var vertexMap = new Dictionary<int, int>();
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uv = new List<Vector2>();
            var subMeshIndices = new List<int[]>(subsets.Length);

            foreach (int sourceSubMesh in subsets)
            {
                int[] sourceIndices = source.GetIndices(
                    sourceSubMesh,
                    applyBaseVertex: true);
                var remapped = new int[sourceIndices.Length];
                for (int index = 0; index < sourceIndices.Length; index++)
                {
                    int sourceVertex = sourceIndices[index];
                    if (sourceVertex < 0 ||
                        sourceVertex >= sourceVertices.Length)
                    {
                        throw new InvalidDataException(
                            "Supplemental static-batch vertex is out of range for " +
                            record.Placement.StableId);
                    }
                    if (!vertexMap.TryGetValue(sourceVertex, out int target))
                    {
                        target = vertices.Count;
                        vertexMap.Add(sourceVertex, target);
                        vertices.Add(positionTransform.MultiplyPoint3x4(
                            sourceVertices[sourceVertex]));
                        if (copyUv)
                        {
                            uv.Add(sourceUv[sourceVertex]);
                        }
                        if (copyNormals)
                        {
                            Vector3 normal = normalTransform.MultiplyVector(
                                sourceNormals[sourceVertex]).normalized;
                            if (!IsFinite(normal) || normal.sqrMagnitude < 0.5f)
                            {
                                copyNormals = false;
                                normals.Clear();
                            }
                            else
                            {
                                normals.Add(normal);
                            }
                        }
                        if (copyTangents)
                        {
                            Vector4 sourceTangent = sourceTangents[sourceVertex];
                            Vector3 tangent = positionTransform.MultiplyVector(
                                new Vector3(
                                    sourceTangent.x,
                                    sourceTangent.y,
                                    sourceTangent.z)).normalized;
                            if (!IsFinite(tangent) ||
                                !float.IsFinite(sourceTangent.w))
                            {
                                copyTangents = false;
                                tangents.Clear();
                            }
                            else
                            {
                                tangents.Add(new Vector4(
                                    tangent.x,
                                    tangent.y,
                                    tangent.z,
                                    sourceTangent.w * handedness));
                            }
                        }
                    }
                    remapped[index] = target;
                }
                subMeshIndices.Add(remapped);
            }

            if (vertices.Count == 0)
            {
                throw new InvalidDataException(
                    "Supplemental static-batch subset is empty: " +
                    record.Placement.StableId);
            }
            var mesh = new Mesh
            {
                name = "P1JOB_" + record.Placement.StableId,
                indexFormat = vertices.Count > ushort.MaxValue
                    ? IndexFormat.UInt32
                    : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            if (copyUv && uv.Count == vertices.Count)
            {
                mesh.SetUVs(0, uv);
            }
            mesh.subMeshCount = subMeshIndices.Count;
            for (int index = 0; index < subMeshIndices.Count; index++)
            {
                mesh.SetIndices(
                    subMeshIndices[index],
                    source.GetTopology(subsets[index]),
                    index,
                    calculateBounds: false);
            }
            mesh.RecalculateBounds();
            if (copyNormals && normals.Count == vertices.Count)
            {
                mesh.SetNormals(normals);
            }
            else
            {
                mesh.RecalculateNormals();
            }
            if (copyTangents && tangents.Count == vertices.Count)
            {
                mesh.SetTangents(tangents);
            }
            else if (copyUv && uv.Count == vertices.Count)
            {
                mesh.RecalculateTangents();
            }
            return mesh;
        }

        private static IReadOnlyDictionary<string, Material>
            BuildSupplementalMaterials(
                Phase1JobLocationPresentationPlan plan)
        {
            string sourceAssetsRoot = ResolveSourceAssetsRoot();
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (Phase1JobLocationMaterialSpec spec in
                     plan.Manifest.supplementalMaterials)
            {
                string sourceMaterial = ResolveBelow(
                    sourceAssetsRoot,
                    spec.sourceRelativePath);
                RequireSourceHash(
                    sourceMaterial,
                    spec.sourceSha256,
                    "supplemental material");
                Texture2D texture = null;
                if (!string.IsNullOrWhiteSpace(spec.textureGuid))
                {
                    string sourceTexture = ResolveBelow(
                        sourceAssetsRoot,
                        spec.textureRelativePath);
                    RequireSourceHash(
                        sourceTexture,
                        spec.textureSha256,
                        "supplemental texture");
                    string texturePath = TextureRoot + "/" +
                                         spec.textureGuid + ".png";
                    File.Copy(
                        sourceTexture,
                        WorldBaselinePaths.ToAbsoluteProjectPath(texturePath),
                        overwrite: true);
                    AssetDatabase.ImportAsset(
                        texturePath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                    TextureImporter importer =
                        AssetImporter.GetAtPath(texturePath) as TextureImporter ??
                        throw new InvalidDataException(
                            "Supplemental texture importer is missing: " +
                            texturePath);
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    importer.mipmapEnabled = true;
                    importer.streamingMipmaps = true;
                    importer.isReadable = false;
                    importer.textureCompression =
                        TextureImporterCompression.CompressedHQ;
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.SaveAndReimport();
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                        texturePath);
                }

                Shader shader = Shader.Find("HDRP/Lit") ??
                    throw new InvalidOperationException(
                        "HDRP/Lit shader is unavailable.");
                var material = new Material(shader)
                {
                    name = "Phase 1 Job Location " + spec.sourceGuid,
                    enableInstancing = true
                };
                Color color = new Color(
                    spec.baseColor[0],
                    spec.baseColor[1],
                    spec.baseColor[2],
                    spec.baseColor[3]);
                material.SetColor("_BaseColor", color);
                if (texture != null)
                {
                    material.SetTexture("_BaseColorMap", texture);
                }
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0.18f);
                string materialPath = MaterialRoot + "/" +
                                      spec.sourceGuid + ".mat";
                AssetDatabase.CreateAsset(material, materialPath);
                result.Add(spec.sourceGuid, material);
            }
            return result;
        }

        private static void BuildCellOverlays(
            Phase1JobLocationPresentationPlan plan,
            IReadOnlyDictionary<string, Mesh> renderMeshes,
            IReadOnlyDictionary<string, Mesh> collisionMeshes,
            IReadOnlyDictionary<string, Material> supplementalMaterials)
        {
            ILookup<string, Phase1JobLocationRendererRecord> renderersByCell =
                plan.Renderers.ToLookup(record => record.CellId,
                    StringComparer.Ordinal);
            ILookup<string, Phase1JobLocationColliderRecord> collidersByCell =
                plan.Colliders.ToLookup(record =>
                    WorldCellMembershipUtility.FromPosition(
                        plan.Placements[record.EntityStableId].Position,
                        WorldBaseline06B2Paths.CellSizeMeters).Id,
                    StringComparer.Ordinal);

            foreach (string cellId in plan.CellIds)
            {
                string scenePath = WorldBaseline06B2Paths.CellScene(cellId);
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
                {
                    throw new FileNotFoundException(
                        "Active donor cell scene is missing.",
                        WorldBaselinePaths.ToAbsoluteProjectPath(scenePath));
                }
                Scene scene = EditorSceneManager.OpenScene(
                    scenePath,
                    OpenSceneMode.Single);
                GameObject sceneRoot = scene.GetRootGameObjects().Single();
                RemoveExistingOverlay(sceneRoot);
                var overlay = new GameObject(
                    Phase1JobLocationPresentationPlan.OverlayRootName);
                overlay.transform.SetParent(sceneRoot.transform, false);
                SetStaticFlags(overlay);

                Phase1JobLocationRendererRecord[] cellRenderers =
                    renderersByCell[cellId].ToArray();
                Phase1JobLocationColliderRecord[] cellColliders =
                    collidersByCell[cellId].ToArray();
                Dictionary<string, Phase1JobLocationRendererRecord>
                    rendererById = cellRenderers.ToDictionary(
                        record => record.Placement.StableId,
                        StringComparer.Ordinal);
                ILookup<string, Phase1JobLocationColliderRecord>
                    colliderByOwner = cellColliders.ToLookup(
                        record => record.EntityStableId,
                        StringComparer.Ordinal);
                string[] entityIds = rendererById.Keys
                    .Concat(cellColliders.Select(record =>
                        record.EntityStableId))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                foreach (string entityId in entityIds)
                {
                    WorldEntityPlacement placement = plan.Placements[entityId];
                    var entity = new GameObject(
                        "P1JOB_" + placement.OriginalName + "_" +
                        placement.StableId.Substring(0, 8));
                    entity.transform.SetParent(overlay.transform, false);
                    entity.transform.SetPositionAndRotation(
                        placement.Position,
                        placement.Rotation);
                    entity.transform.localScale = placement.Scale;
                    SetStaticFlags(entity);

                    bool hasRenderer = rendererById.TryGetValue(
                        entityId,
                        out Phase1JobLocationRendererRecord rendererRecord);
                    if (hasRenderer)
                    {
                        AddRenderer(
                            entity,
                            rendererRecord,
                            renderMeshes[entityId],
                            supplementalMaterials,
                            plan.Manifest.doubleSidedRendererStableIds.Contains(
                                entityId,
                                StringComparer.Ordinal));
                    }
                    Phase1JobLocationColliderRecord[] ownerColliders =
                        colliderByOwner[entityId].ToArray();
                    foreach (Phase1JobLocationColliderRecord collider in
                             ownerColliders)
                    {
                        AddCollider(entity, collider, collisionMeshes);
                    }
                    entity.AddComponent<DonorWorldSupplementalEntityMetadata>()
                        .Configure(
                            placement.StableId,
                            placement.SourceObjectId,
                            placement.HierarchyPath,
                            hasRenderer ? placement.MeshGuid : string.Empty,
                            cellId,
                            plan.ManifestId,
                            hasRenderer,
                            ownerColliders.Length);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                {
                    throw new IOException(
                        "Could not save supplemental job-location cell: " +
                        scenePath);
                }
            }
        }

        private static void AddRenderer(
            GameObject entity,
            Phase1JobLocationRendererRecord record,
            Mesh mesh,
            IReadOnlyDictionary<string, Material> supplementalMaterials,
            bool forceDoubleSided)
        {
            if (mesh == null || mesh.subMeshCount != record.MaterialGuids.Length)
            {
                throw new InvalidDataException(
                    "Supplemental job-location material slots do not match " +
                    record.Placement.StableId);
            }
            Material[] materials = record.MaterialGuids
                .Select(guid => ResolveMaterial(guid, supplementalMaterials))
                .ToArray();
            if (forceDoubleSided)
            {
                for (int index = 0; index < materials.Length; index++)
                {
                    Material source = materials[index];
                    var doubleSided = new Material(source)
                    {
                        name = source.name + " Front-Face Remediation",
                        doubleSidedGI = true,
                    };
                    doubleSided.SetFloat("_DoubleSidedEnable", 1f);
                    doubleSided.SetFloat("_CullMode", 0f);
                    doubleSided.SetFloat("_CullModeForward", 0f);
                    doubleSided.EnableKeyword("_DOUBLESIDED_ON");
                    string materialPath = MaterialRoot + "/DoubleSided_" +
                                          record.Placement.StableId + "_" +
                                          index.ToString(
                                              CultureInfo.InvariantCulture) +
                                          ".mat";
                    AssetDatabase.CreateAsset(doubleSided, materialPath);
                    materials[index] = doubleSided;
                }
            }
            entity.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = entity.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = true;
            var binding = entity.AddComponent<DonorWorldLegacyMaterialBinding>();
            binding.Configure(
                renderer,
                record.MaterialGuids,
                materials,
                materials);
        }

        private static Material ResolveMaterial(
            string guid,
            IReadOnlyDictionary<string, Material> supplementalMaterials)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                WorldBaselinePaths.TexturedMaterial(guid));
            if (material == null)
            {
                supplementalMaterials.TryGetValue(guid, out material);
            }
            return material ?? throw new FileNotFoundException(
                "Supplemental job-location material is missing: " + guid);
        }

        private static void AddCollider(
            GameObject entity,
            Phase1JobLocationColliderRecord record,
            IReadOnlyDictionary<string, Mesh> collisionMeshes)
        {
            int layer = LayerMask.NameToLayer(
                DonorWorldSolidCollisionPolicy.WorldSolidLayer);
            PhysicsMaterial physicsMaterial =
                AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(
                    DonorWorldSolidCollisionPolicy.WorldSolidPhysicsMaterial);
            if (layer < 0 || physicsMaterial == null)
            {
                throw new InvalidOperationException(
                    "Project-owned WorldSolid collision configuration is missing.");
            }
            var child = new GameObject("P1JOB_COLLIDER_" + record.StableId);
            child.transform.SetParent(entity.transform, false);
            child.layer = layer;
            SetStaticFlags(child);
            Collider collider;
            if (record.ColliderType == "MeshCollider")
            {
                var meshCollider = child.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = collisionMeshes[record.MeshGuid];
                meshCollider.convex = false;
                collider = meshCollider;
            }
            else if (record.ColliderType == "BoxCollider")
            {
                var box = child.AddComponent<BoxCollider>();
                box.center = record.Center;
                box.size = record.Size;
                collider = box;
            }
            else if (record.ColliderType == "CapsuleCollider")
            {
                var capsule = child.AddComponent<CapsuleCollider>();
                capsule.center = record.Center;
                capsule.radius = record.Radius;
                capsule.height = record.Height;
                capsule.direction = record.Direction;
                collider = capsule;
            }
            else
            {
                throw new InvalidDataException(
                    "Unsupported supplemental collider type: " +
                    record.ColliderType);
            }
            collider.sharedMaterial = physicsMaterial;
            collider.isTrigger = false;
            collider.enabled = true;
        }

        private static void RemoveExistingOverlay(GameObject root)
        {
            Transform existing = root.GetComponentsInChildren<Transform>(true)
                .SingleOrDefault(transform => string.Equals(
                    transform.name,
                    Phase1JobLocationPresentationPlan.OverlayRootName,
                    StringComparison.Ordinal));
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }

        private static string ResolveSourceAssetsRoot()
        {
            string rawRoot =
                WorldTransferEditorConfiguration.Load().RawExtractionPath;
            string result = Path.GetFullPath(Path.Combine(
                rawRoot,
                ExtractedAssetsRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar)));
            string normalizedRoot = Path.GetFullPath(rawRoot)
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!result.StartsWith(
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(result))
            {
                throw new DirectoryNotFoundException(
                    "Frozen AssetRipper Assets root is invalid: " + result);
            }
            return result;
        }

        private static string ResolveBelow(string root, string relativePath)
        {
            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string result = Path.GetFullPath(Path.Combine(
                root,
                relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!result.StartsWith(
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(result))
            {
                throw new FileNotFoundException(
                    "Supplemental source asset is missing or escapes staging.",
                    result);
            }
            return result;
        }

        private static void RequireSourceHash(
            string path,
            string expected,
            string label)
        {
            string actual = DonorWorldBaselineManifest.ComputeFileSha256(path);
            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Pinned {label} hash differs: {path}");
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static void SetStaticFlags(GameObject target)
        {
            GameObjectUtility.SetStaticEditorFlags(
                target,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccludeeStatic |
                StaticEditorFlags.ReflectionProbeStatic);
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string folder = Path.HasExtension(assetPath)
                ? Path.GetDirectoryName(assetPath)?.Replace('\\', '/')
                : assetPath;
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new InvalidOperationException(
                    "Asset path has no folder: " + assetPath);
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
    }

    [Serializable]
    internal sealed class Phase1JobLocationManifest
    {
        public int schemaVersion;
        public string manifestId;
        public string classification;
        public string sourceRevisionId;
        public string sourceSceneSha256;
        public string worldEntityTableSha256;
        public string worldColliderTableSha256;
        public string[] includeHierarchyRoots;
        public string[] excludeHierarchyFragments;
        public int expectedRendererCount;
        public int expectedStaticBatchRendererCount;
        public int expectedColliderCount;
        public string[] ignoredEmptyMeshColliderStableIds;
        public string[] expectedCellIds;
        public string[] doubleSidedRendererStableIds;
        public Phase1JobLocationMaterialSpec[] supplementalMaterials;
    }

    [Serializable]
    internal sealed class Phase1JobLocationMaterialSpec
    {
        public string sourceGuid;
        public string sourceRelativePath;
        public string sourceSha256;
        public float[] baseColor;
        public string textureGuid;
        public string textureRelativePath;
        public string textureSha256;
    }
}
