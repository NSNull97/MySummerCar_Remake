using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Editor.WorldTransfer;
using MSC.World.Data;
using MSC.World.Partition;
using UnityEngine;

namespace MSC.Editor.WorldBaseline
{
    public sealed class DonorWorldCellizationAssignment
    {
        public DonorWorldCellizationAssignment(
            WorldBaselineSanitationEntry sanitationEntry,
            string ownerId,
            string ownershipReason)
        {
            SanitationEntry = sanitationEntry;
            OwnerId = ownerId;
            OwnershipReason = ownershipReason;
        }

        public WorldBaselineSanitationEntry SanitationEntry { get; }
        public string OwnerId { get; }
        public string OwnershipReason { get; }
        public bool IsGlobal =>
            string.Equals(OwnerId, "global", StringComparison.Ordinal);
    }

    public sealed class DonorWorldSafeColliderRecord
    {
        public string ColliderStableId { get; internal set; } = string.Empty;
        public string EntityStableId { get; internal set; } = string.Empty;
        public string HierarchyPath { get; internal set; } = string.Empty;
        public string ObjectName { get; internal set; } = string.Empty;
        public string ColliderType { get; internal set; } = string.Empty;
        public string MeshGuid { get; internal set; } = string.Empty;
        public Vector3 Center { get; internal set; }
        public Vector3 Size { get; internal set; }
        public float Radius { get; internal set; }
        public float Height { get; internal set; }
        public int Direction { get; internal set; }
        public string OwnershipPolicy { get; internal set; } = string.Empty;
        public string Reason { get; internal set; } = string.Empty;
    }

    public sealed class DonorWorldCellizationPlan
    {
        public const int ExpectedEntityCount = 3842;
        public const int ExpectedGlobalEntityCount = 88;
        public const int ExpectedCellEntityCount = 3754;
        public const int ExpectedCellCount = 49;
        public const int ExpectedColliderCount = 32;
        public const int ExpectedMeshColliderCount = 20;
        public const int ExpectedBoxColliderCount = 12;

        private DonorWorldCellizationPlan(
            DonorWorldCellizationAssignment[] assignments,
            DonorWorldSafeColliderRecord[] colliders,
            string ownershipFingerprint)
        {
            Assignments = assignments;
            SafeColliders = colliders;
            OwnershipFingerprintSha256 = ownershipFingerprint;
        }

        public IReadOnlyList<DonorWorldCellizationAssignment> Assignments
        {
            get;
        }

        public IReadOnlyList<DonorWorldSafeColliderRecord> SafeColliders
        {
            get;
        }

        public string OwnershipFingerprintSha256 { get; }

        public IReadOnlyList<string> CellIds =>
            Assignments
                .Select(assignment =>
                    assignment.SanitationEntry.Placement.CellId)
                .Where(cellId => !string.Equals(
                    cellId,
                    "global",
                    StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Select(cellId => new
                {
                    CellId = cellId,
                    Index = ParseCellId(cellId)
                })
                .OrderBy(value => value.Index.X)
                .ThenBy(value => value.Index.Z)
                .Select(value => value.CellId)
                .ToArray();

        public IReadOnlyList<DonorWorldCellizationAssignment> GetOwnerEntries(
            string ownerId) =>
            Assignments
                .Where(assignment => string.Equals(
                    assignment.OwnerId,
                    ownerId,
                    StringComparison.Ordinal))
                .OrderBy(
                    assignment =>
                        assignment.SanitationEntry.Placement.StableId,
                    StringComparer.Ordinal)
                .ToArray();

        public IReadOnlyList<DonorWorldSafeColliderRecord>
            GetCollidersForOwner(string ownerId)
        {
            HashSet<string> entityIds = GetOwnerEntries(ownerId)
                .Select(assignment =>
                    assignment.SanitationEntry.Placement.StableId)
                .ToHashSet(StringComparer.Ordinal);
            return SafeColliders
                .Where(collider => entityIds.Contains(
                    collider.EntityStableId))
                .OrderBy(
                    collider => collider.ColliderStableId,
                    StringComparer.Ordinal)
                .ToArray();
        }

        public static DonorWorldCellizationPlan Load()
        {
            IReadOnlyList<WorldBaselineSanitationEntry> sanitation =
                WorldBaselineSanitationPlan.Load();
            DonorWorldSafeColliderRecord[] colliders =
                LoadSafeColliders(sanitation);
            HashSet<string> collisionOwnerIds = colliders
                .Select(record => record.EntityStableId)
                .ToHashSet(StringComparer.Ordinal);

            DonorWorldCellizationAssignment[] assignments = sanitation
                .Select(entry =>
                {
                    string reason = ResolveOwnershipReason(
                        entry,
                        collisionOwnerIds);
                    string ownerId = reason == string.Empty
                        ? entry.Placement.CellId
                        : "global";
                    if (!string.Equals(
                            ownerId,
                            "global",
                            StringComparison.Ordinal))
                    {
                        ParseCellId(ownerId);
                    }

                    return new DonorWorldCellizationAssignment(
                        entry,
                        ownerId,
                        reason == string.Empty
                            ? "SourceCellDeterministic"
                            : reason);
                })
                .OrderBy(
                    assignment =>
                        assignment.SanitationEntry.Placement.StableId,
                    StringComparer.Ordinal)
                .ToArray();

            ValidateCounts(assignments, colliders);
            string fingerprint = ComputeFingerprint(
                assignments,
                colliders);
            return new DonorWorldCellizationPlan(
                assignments,
                colliders,
                fingerprint);
        }

        public static WorldCellIndex ParseCellId(string cellId)
        {
            string[] parts =
                (cellId ?? string.Empty).Split('_');
            if (parts.Length != 3 ||
                !string.Equals(
                    parts[0],
                    "cell",
                    StringComparison.Ordinal) ||
                !int.TryParse(
                    parts[1],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int x) ||
                !int.TryParse(
                    parts[2],
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int z))
            {
                throw new FormatException(
                    "Invalid canonical world cell ID: " + cellId);
            }

            return new WorldCellIndex(x, z);
        }

        private static string ResolveOwnershipReason(
            WorldBaselineSanitationEntry entry,
            ISet<string> collisionOwnerIds)
        {
            WorldEntityPlacement placement = entry.Placement;
            if (string.Equals(
                    placement.CellId,
                    "global",
                    StringComparison.Ordinal))
            {
                return "SourceGlobalLargeOrContinuous";
            }

            if (placement.HierarchyPath.StartsWith(
                    "MAP/MESH/",
                    StringComparison.Ordinal))
            {
                return "ExplicitMapMeshAggregateGlobal";
            }

            if (string.Equals(
                    placement.HierarchyPath,
                    "MAP/SkijumpHill/grass",
                    StringComparison.Ordinal))
            {
                return "ExplicitCrossCellTraversalGlobal";
            }

            if (collisionOwnerIds.Contains(placement.StableId))
            {
                return "BootstrapSpawnOrTraversalCollisionGlobal";
            }

            return string.Empty;
        }

        private static DonorWorldSafeColliderRecord[] LoadSafeColliders(
            IReadOnlyList<WorldBaselineSanitationEntry> sanitation)
        {
            Dictionary<string, WorldBaselineSanitationEntry> entityById =
                sanitation.ToDictionary(
                    entry => entry.Placement.StableId,
                    StringComparer.Ordinal);
            Dictionary<string, SourceColliderRecord> sourceById =
                ParseSourceColliders();
            string path = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaseline06B2Paths.SafeColliderAllowlist);
            using var reader = new StreamReader(path);
            List<string> headers = WorldEntityTable.ParseRow(
                reader.ReadLine() ?? string.Empty);
            int colliderIdIndex = headers.IndexOf("ColliderStableId");
            int entityIdIndex = headers.IndexOf("EntityStableId");
            int typeIndex = headers.IndexOf("ExpectedColliderType");
            int meshGuidIndex = headers.IndexOf("ExpectedMeshGuid");
            int ownershipIndex = headers.IndexOf("OwnershipPolicy");
            int reasonIndex = headers.IndexOf("Reason");
            if (colliderIdIndex < 0 ||
                entityIdIndex < 0 ||
                typeIndex < 0 ||
                meshGuidIndex < 0 ||
                ownershipIndex < 0 ||
                reasonIndex < 0)
            {
                throw new FormatException(
                    "06B2 collider allowlist has an invalid header.");
            }

            var result = new List<DonorWorldSafeColliderRecord>();
            var colliderIds = new HashSet<string>(StringComparer.Ordinal);
            var entityIds = new HashSet<string>(StringComparer.Ordinal);
            string line;
            int lineNumber = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> values = WorldEntityTable.ParseRow(line);
                if (values.Count != headers.Count)
                {
                    throw new FormatException(
                        $"06B2 collider allowlist line {lineNumber} has " +
                        $"{values.Count} values; expected {headers.Count}.");
                }

                string colliderId = values[colliderIdIndex];
                string entityId = values[entityIdIndex];
                if (!colliderIds.Add(colliderId))
                {
                    throw new InvalidDataException(
                        "Duplicate collider allowlist ID: " + colliderId);
                }
                if (!entityIds.Add(entityId))
                {
                    throw new InvalidDataException(
                        "06B2 allows only one collider record per source " +
                        "entity; duplicate entity: " + entityId);
                }
                if (!sourceById.TryGetValue(
                        colliderId,
                        out SourceColliderRecord source))
                {
                    throw new InvalidDataException(
                        "Collider allowlist entry is absent from the frozen " +
                        "source table: " + colliderId);
                }
                if (!entityById.TryGetValue(
                        entityId,
                        out WorldBaselineSanitationEntry entity) ||
                    !entity.EffectiveActive)
                {
                    throw new InvalidDataException(
                        "Collider allowlist entity is not an effectively " +
                        "active 06B1 baseline entity: " + entityId);
                }
                if (!string.Equals(
                        source.EntityStableId,
                        entityId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        source.ColliderType,
                        values[typeIndex],
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        source.MeshGuid,
                        values[meshGuidIndex],
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        values[ownershipIndex],
                        "GlobalLegacy",
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Collider allowlist lock does not match the frozen " +
                        "source row: " + colliderId);
                }
                if (!source.Enabled ||
                    source.IsTrigger ||
                    source.Convex ||
                    source.ColliderType is not
                        ("MeshCollider" or "BoxCollider"))
                {
                    throw new InvalidDataException(
                        "Unsafe collider configuration entered the 06B2 " +
                        "allowlist: " + colliderId);
                }
                if (source.ColliderType == "MeshCollider" &&
                    !WorldReferenceMeshLibrarySync.IsUsableMeshGuid(
                        source.MeshGuid))
                {
                    throw new InvalidDataException(
                        "Allowed MeshCollider has no usable mesh GUID: " +
                        colliderId);
                }
                if (source.ColliderType == "BoxCollider" &&
                    (!IsFinitePositive(source.Size.x) ||
                     !IsFinitePositive(source.Size.y) ||
                     !IsFinitePositive(source.Size.z)))
                {
                    throw new InvalidDataException(
                        "Allowed BoxCollider has invalid size: " +
                        colliderId);
                }

                result.Add(new DonorWorldSafeColliderRecord
                {
                    ColliderStableId = colliderId,
                    EntityStableId = entityId,
                    HierarchyPath = source.HierarchyPath,
                    ObjectName = source.ObjectName,
                    ColliderType = source.ColliderType,
                    MeshGuid = source.MeshGuid,
                    Center = source.Center,
                    Size = source.Size,
                    Radius = source.Radius,
                    Height = source.Height,
                    Direction = source.Direction,
                    OwnershipPolicy = values[ownershipIndex],
                    Reason = values[reasonIndex]
                });
            }

            return result
                .OrderBy(
                    record => record.ColliderStableId,
                    StringComparer.Ordinal)
                .ToArray();
        }

        private static Dictionary<string, SourceColliderRecord>
            ParseSourceColliders()
        {
            string path = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldTransferPaths.ColliderTableAssetPath);
            using var reader = new StreamReader(path);
            List<string> headers = WorldEntityTable.ParseRow(
                reader.ReadLine() ?? string.Empty);
            var indices = new Dictionary<string, int>(
                StringComparer.Ordinal);
            for (int index = 0; index < headers.Count; index++)
            {
                indices.Add(headers[index], index);
            }

            string[] required =
            {
                "StableId", "EntityStableId", "HierarchyPath",
                "ObjectName", "ColliderType", "Enabled", "IsTrigger",
                "Convex", "MeshGuid", "CenterX", "CenterY", "CenterZ",
                "SizeX", "SizeY", "SizeZ", "Radius", "Height",
                "Direction"
            };
            foreach (string column in required)
            {
                if (!indices.ContainsKey(column))
                {
                    throw new FormatException(
                        "Frozen collider table lacks column " + column);
                }
            }

            var result = new Dictionary<string, SourceColliderRecord>(
                StringComparer.Ordinal);
            string line;
            int lineNumber = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> values = WorldEntityTable.ParseRow(line);
                if (values.Count != headers.Count)
                {
                    throw new FormatException(
                        $"Frozen collider table line {lineNumber} has an " +
                        "invalid column count.");
                }

                string Get(string column) => values[indices[column]];
                float Number(string column) => float.Parse(
                    Get(column),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture);
                var record = new SourceColliderRecord
                {
                    StableId = Get("StableId"),
                    EntityStableId = Get("EntityStableId"),
                    HierarchyPath = Get("HierarchyPath"),
                    ObjectName = Get("ObjectName"),
                    ColliderType = Get("ColliderType"),
                    Enabled = Get("Enabled") == "1",
                    IsTrigger = Get("IsTrigger") == "1",
                    Convex = Get("Convex") == "1",
                    MeshGuid = Get("MeshGuid"),
                    Center = new Vector3(
                        Number("CenterX"),
                        Number("CenterY"),
                        Number("CenterZ")),
                    Size = new Vector3(
                        Number("SizeX"),
                        Number("SizeY"),
                        Number("SizeZ")),
                    Radius = Number("Radius"),
                    Height = Number("Height"),
                    Direction = int.Parse(
                        Get("Direction"),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture)
                };
                result.Add(record.StableId, record);
            }

            return result;
        }

        private static void ValidateCounts(
            IReadOnlyCollection<DonorWorldCellizationAssignment> assignments,
            IReadOnlyCollection<DonorWorldSafeColliderRecord> colliders)
        {
            int global = assignments.Count(assignment =>
                assignment.IsGlobal);
            int cellOwned = assignments.Count - global;
            int cells = assignments
                .Select(assignment =>
                    assignment.SanitationEntry.Placement.CellId)
                .Where(cellId => !string.Equals(
                    cellId,
                    "global",
                    StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .Count();
            int meshColliders = colliders.Count(collider =>
                collider.ColliderType == "MeshCollider");
            int boxColliders = colliders.Count(collider =>
                collider.ColliderType == "BoxCollider");
            if (assignments.Count != ExpectedEntityCount ||
                global != ExpectedGlobalEntityCount ||
                cellOwned != ExpectedCellEntityCount ||
                cells != ExpectedCellCount ||
                colliders.Count != ExpectedColliderCount ||
                meshColliders != ExpectedMeshColliderCount ||
                boxColliders != ExpectedBoxColliderCount)
            {
                throw new InvalidDataException(
                    "06B2 cellization count drift: " +
                    $"entities={assignments.Count}, global={global}, " +
                    $"cellOwned={cellOwned}, cells={cells}, " +
                    $"colliders={colliders.Count}, mesh={meshColliders}, " +
                    $"box={boxColliders}.");
            }
        }

        private static string ComputeFingerprint(
            IEnumerable<DonorWorldCellizationAssignment> assignments,
            IEnumerable<DonorWorldSafeColliderRecord> colliders)
        {
            var builder = new StringBuilder();
            builder.Append("GENERATOR|")
                .Append(WorldBaseline06B2Paths.GeneratorVersion)
                .Append('\n')
                .Append("SOURCE|")
                .Append(WorldBaselinePaths.SourceRevisionId)
                .Append('|')
                .Append(WorldBaselinePaths.SourceSceneSha256)
                .Append('\n');
            foreach (DonorWorldCellizationAssignment assignment in
                     assignments.OrderBy(
                         value =>
                             value.SanitationEntry.Placement.StableId,
                         StringComparer.Ordinal))
            {
                WorldBaselineSanitationEntry entry =
                    assignment.SanitationEntry;
                builder.Append("ENTITY|")
                    .Append(entry.Placement.StableId).Append('|')
                    .Append(assignment.OwnerId).Append('|')
                    .Append(assignment.OwnershipReason).Append('|')
                    .Append(entry.IncludeRenderer ? '1' : '0').Append('|')
                    .Append(entry.EffectiveActive ? '1' : '0')
                    .Append('\n');
            }

            foreach (DonorWorldSafeColliderRecord collider in
                     colliders.OrderBy(
                         value => value.ColliderStableId,
                         StringComparer.Ordinal))
            {
                builder.Append("COLLIDER|")
                    .Append(collider.ColliderStableId).Append('|')
                    .Append(collider.EntityStableId).Append('|')
                    .Append(collider.ColliderType).Append('|')
                    .Append(collider.MeshGuid).Append('|')
                    .Append(collider.OwnershipPolicy).Append('\n');
            }

            return DonorWorldBaselineManifest.Sha256Text(
                builder.ToString());
        }

        private static bool IsFinitePositive(float value) =>
            float.IsFinite(value) && value > 0f;

        private sealed class SourceColliderRecord
        {
            public string StableId = string.Empty;
            public string EntityStableId = string.Empty;
            public string HierarchyPath = string.Empty;
            public string ObjectName = string.Empty;
            public string ColliderType = string.Empty;
            public bool Enabled;
            public bool IsTrigger;
            public bool Convex;
            public string MeshGuid = string.Empty;
            public Vector3 Center;
            public Vector3 Size;
            public float Radius;
            public float Height;
            public int Direction;
        }
    }
}
