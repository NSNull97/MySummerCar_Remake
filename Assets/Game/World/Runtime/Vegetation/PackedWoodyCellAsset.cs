using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.World.Vegetation
{
    /// <summary>
    /// Compact, generator-owned presentation for one woody category in one
    /// streamed cell. No prefab hierarchy or serialized collider object is
    /// required to render or query the saved population.
    /// </summary>
    [CreateAssetMenu(menuName = "MSC/World/Vegetation/Packed Woody Cell")]
    public sealed class PackedWoodyCellAsset : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;
        public const long SerializedAssetBaseByteBudget = 256L * 1024L;
        public const long SerializedAssetBytesPerPlacementBudget = 1024L;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string generatorId;
        [SerializeField] private string presentationVersion;
        [SerializeField] private string cellId;
        [SerializeField] private string planFingerprint;
        [SerializeField] private PackedWoodyCategory category;
        [SerializeField] private Bounds worldBounds;
        [SerializeField] private PackedWoodyPrototypeAsset[] prototypes =
            Array.Empty<PackedWoodyPrototypeAsset>();
        [SerializeField] private PackedWoodyBatch[] batches =
            Array.Empty<PackedWoodyBatch>();
        [SerializeField] private PackedWoodyPlacementRecord[] placements =
            Array.Empty<PackedWoodyPlacementRecord>();
        [SerializeField] private PackedWoodyCollisionTile[] collisionTiles =
            Array.Empty<PackedWoodyCollisionTile>();
        [SerializeField] private PackedWoodyCollisionRecord[] collisionRecords =
            Array.Empty<PackedWoodyCollisionRecord>();

        public int SchemaVersion => schemaVersion;
        public string GeneratorId => generatorId;
        public string PresentationVersion => presentationVersion;
        public string CellId => cellId;
        public string PlanFingerprint => planFingerprint;
        public PackedWoodyCategory Category => category;
        public Bounds WorldBounds => worldBounds;
        public IReadOnlyList<PackedWoodyPrototypeAsset> Prototypes =>
            prototypes ?? Array.Empty<PackedWoodyPrototypeAsset>();
        public IReadOnlyList<PackedWoodyBatch> Batches =>
            batches ?? Array.Empty<PackedWoodyBatch>();
        public IReadOnlyList<PackedWoodyPlacementRecord> Placements =>
            placements ?? Array.Empty<PackedWoodyPlacementRecord>();
        public IReadOnlyList<PackedWoodyCollisionTile> CollisionTiles =>
            collisionTiles ?? Array.Empty<PackedWoodyCollisionTile>();
        public IReadOnlyList<PackedWoodyCollisionRecord> CollisionRecords =>
            collisionRecords ?? Array.Empty<PackedWoodyCollisionRecord>();
        internal PackedWoodyPrototypeAsset[] PrototypeData =>
            prototypes ?? Array.Empty<PackedWoodyPrototypeAsset>();
        internal PackedWoodyBatch[] BatchData =>
            batches ?? Array.Empty<PackedWoodyBatch>();
        internal PackedWoodyCollisionTile[] CollisionTileData =>
            collisionTiles ?? Array.Empty<PackedWoodyCollisionTile>();
        internal PackedWoodyCollisionRecord[] CollisionRecordData =>
            collisionRecords ?? Array.Empty<PackedWoodyCollisionRecord>();
        public int InstanceCount => placements?.Length ?? 0;
        public int BatchCount => batches?.Length ?? 0;
        public int CollisionRecordCount => collisionRecords?.Length ?? 0;

        public static long SerializedAssetByteBudget(
            int placementMetadataCount)
        {
            if (placementMetadataCount < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(placementMetadataCount));
            return checked(SerializedAssetBaseByteBudget +
                SerializedAssetBytesPerPlacementBudget *
                placementMetadataCount);
        }

        public static bool SerializedAssetBytesFitBudget(
            long serializedBytes,
            int placementMetadataCount)
        {
            if (serializedBytes < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(serializedBytes));
            return serializedBytes <=
                SerializedAssetByteBudget(placementMetadataCount);
        }

        public int CountSpecies(PackedWoodySpecies value)
        {
            int count = 0;
            PackedWoodyPlacementRecord[] configured = placements ??
                Array.Empty<PackedWoodyPlacementRecord>();
            for (int index = 0; index < configured.Length; index++)
                if (configured[index].Species == value) count++;
            return count;
        }

        public int CountMethod(PackedWoodyPlacementMethod value)
        {
            int count = 0;
            PackedWoodyPlacementRecord[] configured = placements ??
                Array.Empty<PackedWoodyPlacementRecord>();
            for (int index = 0; index < configured.Length; index++)
                if (configured[index].Method == value) count++;
            return count;
        }

        public IReadOnlyList<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (schemaVersion != CurrentSchemaVersion)
                errors.Add("Packed woody cell schema is unsupported.");
            if (string.IsNullOrWhiteSpace(generatorId) ||
                string.IsNullOrWhiteSpace(presentationVersion) ||
                string.IsNullOrWhiteSpace(cellId) ||
                string.IsNullOrWhiteSpace(planFingerprint))
                errors.Add("Packed woody cell provenance is incomplete.");
            if (!FiniteBounds(worldBounds) || worldBounds.size.sqrMagnitude <= 0f)
                errors.Add("Packed woody cell world bounds are invalid.");

            PackedWoodyPrototypeAsset[] configuredPrototypes = prototypes ??
                Array.Empty<PackedWoodyPrototypeAsset>();
            for (int index = 0; index < configuredPrototypes.Length; index++)
            {
                PackedWoodyPrototypeAsset prototype = configuredPrototypes[index];
                if (prototype == null)
                {
                    errors.Add("Packed woody cell contains a null prototype.");
                    continue;
                }
                foreach (string error in prototype.ValidateConfiguration())
                    errors.Add(prototype.name + ": " + error);
            }

            PackedWoodyBatch[] configuredBatches = batches ??
                Array.Empty<PackedWoodyBatch>();
            int matrixCount = 0;
            for (int index = 0; index < configuredBatches.Length; index++)
            {
                PackedWoodyBatch batch = configuredBatches[index];
                if (batch == null)
                {
                    errors.Add("Packed woody cell contains a null batch.");
                    continue;
                }
                if (batch.PrototypeIndex < 0 ||
                    batch.PrototypeIndex >= configuredPrototypes.Length)
                    errors.Add("Packed woody batch prototype index is invalid.");
                if (batch.Count <= 0 ||
                    batch.Count > PackedWoodyBatch.MaximumInstanceCount)
                    errors.Add("Packed woody batch must contain 1..1023 matrices.");
                if (!float.IsFinite(batch.MaximumHeightMeters) ||
                    batch.MaximumHeightMeters <= 0f)
                    errors.Add("Packed woody batch height is invalid.");
                if (!FiniteBounds(batch.WorldBounds) ||
                    batch.WorldBounds.size.sqrMagnitude <= 0f)
                    errors.Add("Packed woody batch world bounds are invalid.");
                IReadOnlyList<Matrix4x4> matrices = batch.Matrices;
                for (int matrixIndex = 0; matrixIndex < matrices.Count; matrixIndex++)
                    if (!FiniteMatrix(matrices[matrixIndex]))
                        errors.Add("Packed woody batch contains a non-finite matrix.");
                matrixCount += batch.Count;
            }

            PackedWoodyPlacementRecord[] configuredPlacements = placements ??
                Array.Empty<PackedWoodyPlacementRecord>();
            if (matrixCount != configuredPlacements.Length)
                errors.Add("Packed woody matrix count differs from placement metadata.");
            var placementByStableId =
                new Dictionary<Hash128, PackedWoodyPlacementRecord>();
            var claimedMatrixPointers = new HashSet<long>();
            for (int index = 0; index < configuredPlacements.Length; index++)
            {
                PackedWoodyPlacementRecord record = configuredPlacements[index];
                if (placementByStableId.ContainsKey(record.StableIdHash))
                    errors.Add("Packed woody stable-ID hash is duplicated.");
                else placementByStableId.Add(record.StableIdHash, record);
                if (record.Category != category)
                    errors.Add("Packed woody placement category differs from its asset.");
                if (!MethodMatchesCategory(record.Method, category))
                    errors.Add("Packed woody placement method differs from its category.");
                bool treeSpecies = record.Species >= PackedWoodySpecies.Spruce &&
                    record.Species <= PackedWoodySpecies.Aspen;
                if (category == PackedWoodyCategory.ShrubOrUndergrowth
                    ? treeSpecies : !treeSpecies)
                    errors.Add("Packed woody placement species differs from its category.");
                if (record.PrototypeIndex < 0 ||
                    record.PrototypeIndex >= configuredPrototypes.Length ||
                    record.BatchIndex < 0 ||
                    record.BatchIndex >= configuredBatches.Length)
                {
                    errors.Add("Packed woody placement pointer is invalid.");
                    continue;
                }
                PackedWoodyBatch batch = configuredBatches[record.BatchIndex];
                if (batch == null || record.MatrixIndex < 0 ||
                    record.MatrixIndex >= batch.Count ||
                    batch.PrototypeIndex != record.PrototypeIndex)
                {
                    errors.Add("Packed woody placement matrix pointer is invalid.");
                    continue;
                }
                long matrixPointer = ((long)record.BatchIndex << 32) |
                    (uint)record.MatrixIndex;
                if (!claimedMatrixPointers.Add(matrixPointer))
                    errors.Add("Packed woody placement matrix pointer is duplicated.");
                if (!float.IsFinite(record.WorldPosition.x) ||
                    !float.IsFinite(record.WorldPosition.y) ||
                    !float.IsFinite(record.WorldPosition.z) ||
                    !float.IsFinite(record.HeightMeters) ||
                    record.HeightMeters <= 0f)
                {
                    errors.Add("Packed woody placement contains non-finite data.");
                    continue;
                }
                PackedWoodyPrototypeAsset prototype =
                    configuredPrototypes[record.PrototypeIndex];
                if (prototype != null && prototype.Species != record.Species)
                    errors.Add("Packed woody placement species differs from its prototype.");
                Vector3 matrixPosition = batch.Matrices[record.MatrixIndex]
                    .GetColumn(3);
                if ((matrixPosition - record.WorldPosition).sqrMagnitude > 0.000001f)
                    errors.Add("Packed woody placement position differs from its matrix translation.");
            }

            PackedWoodyCollisionRecord[] configuredCollision = collisionRecords ??
                Array.Empty<PackedWoodyCollisionRecord>();
            PackedWoodyCollisionTile[] configuredTiles = collisionTiles ??
                Array.Empty<PackedWoodyCollisionTile>();
            if (category != PackedWoodyCategory.OriginalTree &&
                (configuredCollision.Length != 0 || configuredTiles.Length != 0))
                errors.Add("Only playable original/infill trees may own pooled collision records.");
            int covered = 0;
            for (int index = 0; index < configuredTiles.Length; index++)
            {
                PackedWoodyCollisionTile tile = configuredTiles[index];
                if (tile.StartIndex != covered || tile.Count <= 0 ||
                    tile.StartIndex + tile.Count > configuredCollision.Length)
                    errors.Add("Packed woody collision tiles are not a contiguous valid index.");
                if (!FiniteBounds(tile.WorldBounds) ||
                    tile.WorldBounds.size.sqrMagnitude <= 0f)
                    errors.Add("Packed woody collision tile bounds are invalid.");
                covered += tile.Count;
            }
            if (covered != configuredCollision.Length)
                errors.Add("Packed woody collision tile coverage differs from records.");
            var collisionStableIds = new HashSet<Hash128>();
            for (int index = 0; index < configuredCollision.Length; index++)
            {
                PackedWoodyCollisionRecord collision = configuredCollision[index];
                bool hasPlacement = placementByStableId.TryGetValue(
                    collision.StableIdHash,
                    out PackedWoodyPlacementRecord placement);
                if (!collisionStableIds.Add(collision.StableIdHash))
                    errors.Add("Packed woody collision stable-ID hash is duplicated.");
                if (!hasPlacement ||
                    !float.IsFinite(collision.BottomCenter.x) ||
                    !float.IsFinite(collision.BottomCenter.y) ||
                    !float.IsFinite(collision.BottomCenter.z) ||
                    !float.IsFinite(collision.CapsuleHeight) ||
                    !float.IsFinite(collision.CapsuleRadius) ||
                    collision.CapsuleHeight <= 0f || collision.CapsuleRadius <= 0f ||
                    collision.CapsuleRadius * 2f > collision.CapsuleHeight)
                    errors.Add("Packed woody collision record is invalid.");
                if (hasPlacement &&
                    (collision.BottomCenter - placement.WorldPosition)
                        .sqrMagnitude > 0.000001f)
                    errors.Add("Packed woody collision position differs from its placement.");
            }
            return errors;
        }

        /// <summary>
        /// Cheap structural gate used when a streamed renderer becomes active.
        /// Full matrix/placement equality remains an Editor generation guard;
        /// repeating that O(instance count) audit in scene activation would
        /// recreate the main-thread integration cost this format removes.
        /// </summary>
        internal bool TryValidateRuntimeRenderingHeader(out string error)
        {
            if (!TryValidateRuntimeCommon(out error)) return false;
            if (prototypes == null || batches == null || placements == null)
            {
                error = "render arrays are missing";
                return false;
            }
            string policySignature = null;
            for (int index = 0; index < prototypes.Length; index++)
            {
                PackedWoodyPrototypeAsset prototype = prototypes[index];
                if (prototype == null)
                {
                    error = "prototype reference is missing";
                    return false;
                }
                if (!prototype.TryValidateRuntimeHeader(
                    out string prototypeError))
                {
                    error = prototypeError;
                    return false;
                }
                bool treeSpecies = prototype.Species >=
                    PackedWoodySpecies.Spruce && prototype.Species <=
                    PackedWoodySpecies.Aspen;
                if (prototype.PresentationVersion != presentationVersion ||
                    (category == PackedWoodyCategory.ShrubOrUndergrowth
                        ? treeSpecies : !treeSpecies))
                {
                    error = "prototype presentation/category is stale or invalid";
                    return false;
                }
                if (policySignature == null)
                    policySignature = prototype.ProjectPolicySignature;
                else if (!string.Equals(
                    policySignature,
                    prototype.ProjectPolicySignature,
                    StringComparison.Ordinal))
                {
                    error = "prototype policy signatures differ";
                    return false;
                }
            }

            long matrixCount = 0;
            for (int index = 0; index < batches.Length; index++)
            {
                PackedWoodyBatch batch = batches[index];
                if (batch == null || batch.PrototypeIndex < 0 ||
                    batch.PrototypeIndex >= prototypes.Length ||
                    batch.Count <= 0 ||
                    batch.Count > PackedWoodyBatch.MaximumInstanceCount ||
                    !float.IsFinite(batch.MaximumHeightMeters) ||
                    batch.MaximumHeightMeters <= 0f ||
                    !FiniteBounds(batch.WorldBounds) ||
                    batch.WorldBounds.size.sqrMagnitude <= 0f)
                {
                    error = "render batch header is invalid";
                    return false;
                }
                matrixCount += batch.Count;
            }
            if (matrixCount != placements.Length)
            {
                error = "matrix/placement counts differ";
                return false;
            }
            error = string.Empty;
            return true;
        }

        internal bool TryValidateRuntimeCollisionHeader(out string error)
        {
            if (!TryValidateRuntimeCommon(out error)) return false;
            if (category != PackedWoodyCategory.OriginalTree)
            {
                error = "collision asset category is not playable OriginalTree";
                return false;
            }
            if (collisionTiles == null || collisionRecords == null ||
                collisionTiles.Length == 0 || collisionRecords.Length == 0)
            {
                error = "collision index is empty or missing";
                return false;
            }
            int covered = 0;
            for (int index = 0; index < collisionTiles.Length; index++)
            {
                PackedWoodyCollisionTile tile = collisionTiles[index];
                long end = (long)tile.StartIndex + tile.Count;
                if (tile.StartIndex != covered || tile.Count <= 0 ||
                    end > collisionRecords.Length ||
                    !FiniteBounds(tile.WorldBounds) ||
                    tile.WorldBounds.size.sqrMagnitude <= 0f)
                {
                    error = "collision tile header is invalid";
                    return false;
                }
                covered += tile.Count;
            }
            if (covered != collisionRecords.Length)
            {
                error = "collision tile coverage differs from records";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private bool TryValidateRuntimeCommon(out string error)
        {
            if (schemaVersion != CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(generatorId) ||
                string.IsNullOrWhiteSpace(presentationVersion) ||
                string.IsNullOrWhiteSpace(cellId) ||
                string.IsNullOrWhiteSpace(planFingerprint))
            {
                error = "cell schema/provenance is invalid";
                return false;
            }
            if ((int)category < (int)PackedWoodyCategory.OriginalTree ||
                (int)category > (int)PackedWoodyCategory.ShrubOrUndergrowth ||
                !FiniteBounds(worldBounds) ||
                worldBounds.size.sqrMagnitude <= 0f)
            {
                error = "cell category/world bounds are invalid";
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool FiniteBounds(Bounds bounds) =>
            float.IsFinite(bounds.center.x) &&
            float.IsFinite(bounds.center.y) &&
            float.IsFinite(bounds.center.z) &&
            float.IsFinite(bounds.size.x) &&
            float.IsFinite(bounds.size.y) &&
            float.IsFinite(bounds.size.z) &&
            bounds.size.x >= 0f && bounds.size.y >= 0f && bounds.size.z >= 0f;

        private static bool FiniteMatrix(Matrix4x4 matrix)
        {
            for (int index = 0; index < 16; index++)
                if (!float.IsFinite(matrix[index])) return false;
            return true;
        }

        private static bool MethodMatchesCategory(
            PackedWoodyPlacementMethod method,
            PackedWoodyCategory configuredCategory)
        {
            switch (configuredCategory)
            {
                case PackedWoodyCategory.OriginalTree:
                    return method == PackedWoodyPlacementMethod.DonorOriginal ||
                        method == PackedWoodyPlacementMethod.NaturalInfill;
                case PackedWoodyCategory.BoundaryForest:
                    return method == PackedWoodyPlacementMethod.BoundaryForest;
                case PackedWoodyCategory.ShrubOrUndergrowth:
                    return method == PackedWoodyPlacementMethod.DonorShrub ||
                        method == PackedWoodyPlacementMethod.ForestFloor;
                default:
                    return false;
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredGeneratorId,
            string configuredPresentationVersion,
            string configuredCellId,
            string configuredPlanFingerprint,
            PackedWoodyCategory configuredCategory,
            Bounds configuredWorldBounds,
            PackedWoodyPrototypeAsset[] configuredPrototypes,
            PackedWoodyBatch[] configuredBatches,
            PackedWoodyPlacementRecord[] configuredPlacements,
            PackedWoodyCollisionTile[] configuredCollisionTiles,
            PackedWoodyCollisionRecord[] configuredCollisionRecords)
        {
            schemaVersion = CurrentSchemaVersion;
            generatorId = configuredGeneratorId;
            presentationVersion = configuredPresentationVersion;
            cellId = configuredCellId;
            planFingerprint = configuredPlanFingerprint;
            category = configuredCategory;
            worldBounds = configuredWorldBounds;
            prototypes = configuredPrototypes != null
                ? (PackedWoodyPrototypeAsset[])configuredPrototypes.Clone()
                : Array.Empty<PackedWoodyPrototypeAsset>();
            batches = configuredBatches != null
                ? (PackedWoodyBatch[])configuredBatches.Clone()
                : Array.Empty<PackedWoodyBatch>();
            placements = configuredPlacements != null
                ? (PackedWoodyPlacementRecord[])configuredPlacements.Clone()
                : Array.Empty<PackedWoodyPlacementRecord>();
            collisionTiles = configuredCollisionTiles != null
                ? (PackedWoodyCollisionTile[])configuredCollisionTiles.Clone()
                : Array.Empty<PackedWoodyCollisionTile>();
            collisionRecords = configuredCollisionRecords != null
                ? (PackedWoodyCollisionRecord[])configuredCollisionRecords.Clone()
                : Array.Empty<PackedWoodyCollisionRecord>();
        }
#endif
    }
}
