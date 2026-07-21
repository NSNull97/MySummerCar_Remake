using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MSC.Editor.WorldTransfer;
using MSC.World.Data;
using UnityEngine;

namespace MSC.Editor.WorldBaseline
{
    public static class DonorWorldSolidCollisionPolicy
    {
        public const string PolicyVersion = "08A1.6";
        public const string WorldSurfaceLayer = "WorldSurface";
        public const string WorldSolidLayer = "WorldSolid";
        public const string WorldSurfacePhysicsMaterial =
            "Assets/Game/World/Content/Physics/" +
            "WorldSurfaceZeroBounce.physicMaterial";
        public const string WorldSolidPhysicsMaterial =
            "Assets/Game/World/Content/Physics/" +
            "WorldSolidZeroBounce.physicMaterial";

        private const string BuiltInMeshGuid =
            "0000000000000000e000000000000000";
        private const string SaunaIndoorWallsColliderStableId =
            "db8f7c3ab9163a68b6d7cb01bf32f415";
        private const string DanceHallNoRainColliderStableId =
            "1eb95efde812a3d7e245026c86bf4b1a";
        private const int RigidbodyClassId = 54;

        private static readonly HashSet<string>
            ReviewedStaticVehicleObstacleColliderIds =
                new HashSet<string>(StringComparer.Ordinal)
                {
                    "0085180aa6be4f91d4df43837e666fbb",
                    "01c3402f3d3f59b0464454ea0147d68f",
                    "1c9a956af57d878d13a40a595619039d",
                    "4ec963e836e5a4ded593bac5b0386128",
                    "4feaf7d59355d785eba3367a669c1cf4",
                    "62cf57c3e23b2f75b5df8ad3eb3d0665",
                    "982e4845cb7f7956c58a898a413f67d2",
                    "aaae5c5ea3fd36e340d0098be490c3cd",
                    "ab556998c80caf2d12b28f63d60b768e",
                    "ac742ad01974971a11d7943d01983868",
                    "c2f2636d1c8a5e24548359fb9b3d8e31",
                    "c930fd89c7ab95b88ed8b599d17763f9",
                    "d3dbf6fa52297a3d3dd92f02690b4660"
                };

        private static readonly HashSet<string>
            ReviewedStaticObstacleColliderIds =
                new HashSet<string>(StringComparer.Ordinal)
                {
                    // The donor taxonomy classifies the physical well shell
                    // as Water. These frozen rows are reviewed solid props,
                    // not traversable water surfaces.
                    "c278369891f09728f6be083a38529ade",
                    "43903189d1d95d8161c3a2b2e0a9c329",

                    // These construction-site cable reels are fixed scenery,
                    // despite the broad donor Wire category.
                    "d583dc35d17cb2d7ea5b80aded288d46",
                    "42a5b4862a5027c15b740ff3c6ba8588"
                };

        internal static IReadOnlyCollection<string>
            ReviewedStaticVehicleObstacleIds =>
                ReviewedStaticVehicleObstacleColliderIds;

        internal static IReadOnlyCollection<string>
            ReviewedStaticObstacleIds =>
                ReviewedStaticObstacleColliderIds;

        private static readonly HashSet<string> IncludedCategories =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "BuildingExterior",
                "BuildingInterior",
                "Roof",
                "Window",
                "Fence",
                "Floor",
                "ColliderOnly",
                "VegetationTree",
                "Road",
                "Bridge",
                "StaticProp",
                "UtilityPole",
                "Rock",
                "Landmark",
                "Field",
                "VegetationGrass"
            };

        private static readonly HashSet<string> SurfaceCategories =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Road",
                "Bridge",
                "Floor",
                "Field",
                "VegetationGrass",
                "Landmark",
                "Water"
            };

        private static readonly HashSet<string> ReviewedSurfacePaths =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "MAP/MESH/AIRPORT",
                "MAP/MESH/RAILROAD/PLANKS",
                "MAP/MESH/SWAMP"
            };

        // These are frozen donor hierarchy fragments used only by the Editor
        // import policy. Runtime code never searches donor hierarchy names.
        private static readonly string[] ActorOrPlayerFragments =
        {
            "/skeleton",
            "/ragdoll",
            "/humancoll",
            "/playerrigid",
            "/humantriggercrime/",
            "/facepisstrigger",
            "/functions/band/",
            "/functions/dancecouple",
            "/functions/dancer",
            "/functions/fighter/",
            "/functions/guard/",
            "/lod/kid1/",
            "/lod/kid2/",
            "/drunksbar/",
            "/teimoinbike/",
            "/teimoinshop/",
            "/audience/markkutheatre",
            "/audience/signetheatre",
            "/actorsdisable/",
            "/servant/",
            "yard/uncle/home/",
            "yard/uncle/unclewalking/"
        };

        private static readonly string[] VehicleFragments =
        {
            "/staticcar",
            "/junkcar",
            "/garbage/tractor/",
            "/tractor_tire",
            "/truck_wreck",
            "/truck_cabin",
            "/dragcar",
            "/bicycle/"
        };

        public static DonorWorldSolidCollisionPlanData Load(
            IReadOnlyList<WorldBaselineSanitationEntry> sanitation)
        {
            Dictionary<string, WorldBaselineSanitationEntry> entityById =
                sanitation.ToDictionary(
                    entry => entry.Placement.StableId,
                    StringComparer.Ordinal);
            Dictionary<string, DonorWorldSafeColliderRecord> sourceById =
                ParseSourceColliders();
            IReadOnlyDictionary<string, bool> rigidbodyInAncestryByEntityId =
                LoadSourceRigidbodyAncestry(entityById.Keys);
            Dictionary<string, SafetyCriticalLock> safetyById =
                LoadSafetyCriticalLocks(sourceById, entityById);

            DonorWorldSafeColliderRecord[] dispositions = sourceById.Values
                .Where(record => entityById.ContainsKey(
                    record.EntityStableId))
                .Select(record =>
                {
                    WorldBaselineSanitationEntry entity =
                        entityById[record.EntityStableId];
                    record.SemanticCategory = ResolveSemanticCategory(
                        record.ColliderStableId,
                        entity.Placement.Category);
                    record.EffectiveActive = entity.EffectiveActive;
                    record.SourceHasRigidbodyInAncestry =
                        rigidbodyInAncestryByEntityId[
                            record.EntityStableId];
                    if (safetyById.TryGetValue(
                            record.ColliderStableId,
                            out SafetyCriticalLock safety))
                    {
                        if (record.SourceHasRigidbodyInAncestry)
                        {
                            throw new InvalidDataException(
                                "A safety-critical static collider is owned " +
                                "by a donor Rigidbody hierarchy and requires " +
                                "a project presenter: " +
                                record.ColliderStableId);
                        }

                        record.Disposition =
                            "IncludedSafetyCriticalGlobal";
                        record.OwnershipPolicy = safety.OwnershipPolicy;
                        record.Reason = safety.Reason;
                        record.IsSafetyCritical = true;
                    }
                    else
                    {
                        record.Disposition = ResolveDisposition(record);
                        record.OwnershipPolicy = record.IsIncluded
                            ? "FrozenEntityOwnership"
                            : "Excluded";
                        record.Reason =
                            DispositionReason(record.Disposition);
                    }

                    if (record.IsIncluded)
                    {
                        record.CollisionLayerName =
                            ResolveLayerName(record);
                        record.PhysicsMaterialAssetPath =
                            record.CollisionLayerName == WorldSurfaceLayer
                                ? WorldSurfacePhysicsMaterial
                                : WorldSolidPhysicsMaterial;
                    }

                    return record;
                })
                .OrderBy(
                    record => record.ColliderStableId,
                    StringComparer.Ordinal)
                .ToArray();

            return new DonorWorldSolidCollisionPlanData(
                dispositions,
                dispositions.Where(record => record.IsIncluded).ToArray());
        }

        public static string DispositionReason(string disposition) =>
            disposition switch
            {
                "IncludedSafetyCriticalGlobal" =>
                    "Preserved by the immutable 06B2 safety allowlist.",
                "IncludedStaticWorldSolid" =>
                    "Reviewed static-world solid collider from the frozen " +
                    "M04A1 source table.",
                "ExcludedDisabled" =>
                    "The donor collider component is disabled.",
                "ExcludedTrigger" =>
                    "Trigger volumes are not imported by the solid pass.",
                "ExcludedDoorRequiresBinding" =>
                    "A door cannot become a static blocker before a " +
                    "project-owned hinge/interaction binding is authored.",
                "ExcludedInactive" =>
                    "The collider owner is inactive through the frozen " +
                    "donor hierarchy.",
                "ExcludedActorOrPlayer" =>
                    "Actor, NPC, skeleton or player-owned collision belongs " +
                    "to a later project-owned gameplay presenter.",
                "ExcludedVehicle" =>
                    "Vehicle collision belongs to the project-owned vehicle " +
                    "runtime, not the static-world pass.",
                "ExcludedWeatherShelterVolume" =>
                    "The donor NoRain mesh is a weather/shelter query " +
                    "volume, not a physical static wall.",
                "ExcludedCategory" =>
                    "The semantic category is outside the reviewed static-" +
                    "world collision set.",
                "ExcludedUnsupportedType" =>
                    "The collider type is outside Box/Mesh/Capsule/Sphere.",
                "ExcludedBuiltinMeshRequiresMapping" =>
                    "The donor collider references a Unity built-in mesh; " +
                    "no render-mesh fallback is permitted.",
                "ExcludedMeshRequiresMapping" =>
                    "The source MeshCollider has no resolvable audited mesh " +
                    "GUID.",
                "ExcludedInvalidShape" =>
                    "The frozen primitive collider dimensions are invalid.",
                "ExcludedDynamicRequiresPresenter" =>
                    "The collider is owned by a donor Rigidbody on itself " +
                    "or an ancestor and requires a project-owned dynamic " +
                    "presenter instead of a frozen static blocker.",
                _ => "Explicit reviewed collision disposition."
            };

        private static string ResolveDisposition(
            DonorWorldSafeColliderRecord record)
        {
            if (!record.SourceEnabled)
            {
                return "ExcludedDisabled";
            }
            if (record.SourceIsTrigger)
            {
                return "ExcludedTrigger";
            }
            if (IsDoorSource(record))
            {
                return "ExcludedDoorRequiresBinding";
            }
            if (!record.EffectiveActive)
            {
                return "ExcludedInactive";
            }
            if (ContainsAny(
                    record.HierarchyPath,
                    ActorOrPlayerFragments))
            {
                return "ExcludedActorOrPlayer";
            }
            if (ContainsAny(record.HierarchyPath, VehicleFragments) &&
                !ReviewedStaticVehicleObstacleColliderIds.Contains(
                    record.ColliderStableId))
            {
                return "ExcludedVehicle";
            }
            if (!IncludedCategories.Contains(record.SemanticCategory))
            {
                return "ExcludedCategory";
            }
            if (string.Equals(
                    record.ColliderStableId,
                    DanceHallNoRainColliderStableId,
                    StringComparison.Ordinal))
            {
                return "ExcludedWeatherShelterVolume";
            }
            if (record.ColliderType is not
                ("MeshCollider" or "BoxCollider" or
                 "CapsuleCollider" or "SphereCollider"))
            {
                return "ExcludedUnsupportedType";
            }
            if (record.ColliderType == "MeshCollider")
            {
                if (string.Equals(
                        record.MeshGuid,
                        BuiltInMeshGuid,
                        StringComparison.Ordinal))
                {
                    return "ExcludedBuiltinMeshRequiresMapping";
                }
                if (!WorldReferenceMeshLibrarySync.IsUsableMeshGuid(
                        record.MeshGuid))
                {
                    return "ExcludedMeshRequiresMapping";
                }
            }
            else if (!HasValidPrimitiveShape(record))
            {
                return "ExcludedInvalidShape";
            }
            if (record.SourceHasRigidbodyInAncestry)
            {
                return "ExcludedDynamicRequiresPresenter";
            }

            return "IncludedStaticWorldSolid";
        }

        private static string ResolveSemanticCategory(
            string colliderStableId,
            string sourceCategory)
        {
            if (string.Equals(
                    colliderStableId,
                    SaunaIndoorWallsColliderStableId,
                    StringComparison.Ordinal))
            {
                return "BuildingInterior";
            }

            return ReviewedStaticObstacleColliderIds.Contains(
                colliderStableId)
                    ? "StaticProp"
                    : sourceCategory;
        }

        private static IReadOnlyDictionary<string, bool>
            LoadSourceRigidbodyAncestry(IEnumerable<string> entityStableIds)
        {
            string path = Path.Combine(
                WorldTransferEditorConfiguration.Load().NormalizedDataPath,
                "WorldObjectPlacements.csv");
            using var reader = new StreamReader(path);
            List<string> headers = WorldEntityTable.ParseRow(
                reader.ReadLine() ?? string.Empty);
            int stableIdIndex = headers.IndexOf("StableId");
            int objectIdIndex = headers.IndexOf("SourceObjectId");
            int parentObjectIdIndex = headers.IndexOf("ParentObjectId");
            int componentClassIdsIndex = headers.IndexOf(
                "ComponentClassIds");
            if (stableIdIndex < 0 || objectIdIndex < 0 ||
                parentObjectIdIndex < 0 || componentClassIdsIndex < 0)
            {
                throw new FormatException(
                    "Full source placement inventory lacks the stable ID, " +
                    "object ancestry or component-class columns required " +
                    "by the collision policy.");
            }

            var byStableId = new Dictionary<string, SourceHierarchyNode>(
                StringComparer.Ordinal);
            var byObjectId = new Dictionary<string, SourceHierarchyNode>(
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
                        $"Full source placement inventory line {lineNumber} " +
                        "has an invalid column count.");
                }

                var node = new SourceHierarchyNode(
                    values[stableIdIndex],
                    values[objectIdIndex],
                    values[parentObjectIdIndex],
                    values[componentClassIdsIndex]
                        .Split(
                            ';',
                            StringSplitOptions.RemoveEmptyEntries)
                        .Any(value => int.Parse(
                                value,
                                NumberStyles.Integer,
                                CultureInfo.InvariantCulture) ==
                            RigidbodyClassId));
                if (!byStableId.TryAdd(node.StableId, node) ||
                    !byObjectId.TryAdd(node.SourceObjectId, node))
                {
                    throw new InvalidDataException(
                        "Full source placement inventory contains a " +
                        "duplicate stable or source object ID at line " +
                        lineNumber + ".");
                }
            }

            var memo = new Dictionary<string, bool>(StringComparer.Ordinal);
            var result = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (string stableId in entityStableIds)
            {
                if (!byStableId.TryGetValue(
                        stableId,
                        out SourceHierarchyNode node))
                {
                    throw new InvalidDataException(
                        "Collision owner is absent from the full source " +
                        "placement inventory: " + stableId);
                }

                result.Add(
                    stableId,
                    ResolveSourceRigidbodyAncestry(
                        node,
                        byObjectId,
                        memo,
                        new HashSet<string>(StringComparer.Ordinal)));
            }

            return result;
        }

        private static bool ResolveSourceRigidbodyAncestry(
            SourceHierarchyNode node,
            IReadOnlyDictionary<string, SourceHierarchyNode> byObjectId,
            IDictionary<string, bool> memo,
            ISet<string> recursionStack)
        {
            if (memo.TryGetValue(node.SourceObjectId, out bool resolved))
            {
                return resolved;
            }
            if (!recursionStack.Add(node.SourceObjectId))
            {
                throw new InvalidDataException(
                    "Cycle detected in full source placement ancestry at " +
                    node.SourceObjectId + ".");
            }

            bool hasRigidbody = node.HasRigidbody;
            if (!hasRigidbody &&
                !string.IsNullOrWhiteSpace(node.ParentObjectId) &&
                !string.Equals(
                    node.ParentObjectId,
                    "0",
                    StringComparison.Ordinal))
            {
                if (!byObjectId.TryGetValue(
                        node.ParentObjectId,
                        out SourceHierarchyNode parent))
                {
                    throw new InvalidDataException(
                        "Full source placement ancestry is missing parent " +
                        node.ParentObjectId + " for object " +
                        node.SourceObjectId + ".");
                }

                hasRigidbody = ResolveSourceRigidbodyAncestry(
                    parent,
                    byObjectId,
                    memo,
                    recursionStack);
            }

            recursionStack.Remove(node.SourceObjectId);
            memo[node.SourceObjectId] = hasRigidbody;
            return hasRigidbody;
        }

        private static bool IsDoorSource(
            DonorWorldSafeColliderRecord record)
        {
            if (string.Equals(
                    record.SemanticCategory,
                    "Door",
                    StringComparison.Ordinal))
            {
                return true;
            }

            return (record.HierarchyPath ?? string.Empty)
                .Split('/')
                .Any(SegmentContainsDoorToken);
        }

        private static bool SegmentContainsDoorToken(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                return false;
            }

            var token = new System.Text.StringBuilder(segment.Length);
            for (int index = 0; index <= segment.Length; index++)
            {
                bool atEnd = index == segment.Length;
                char current = atEnd ? '\0' : segment[index];
                bool separator = atEnd || !char.IsLetterOrDigit(current);
                bool camelBoundary = !atEnd && token.Length > 0 &&
                    char.IsUpper(current) && index > 0 &&
                    char.IsLower(segment[index - 1]);
                if (separator || camelBoundary)
                {
                    if (IsDoorToken(token.ToString()))
                    {
                        return true;
                    }

                    token.Clear();
                    if (separator)
                    {
                        continue;
                    }
                }

                token.Append(char.ToLowerInvariant(current));
            }

            return false;
        }

        private static bool IsDoorToken(string token)
        {
            if (string.Equals(token, "door", StringComparison.Ordinal) ||
                string.Equals(token, "doors", StringComparison.Ordinal))
            {
                return true;
            }

            string suffix;
            if (token.StartsWith("doors", StringComparison.Ordinal))
            {
                suffix = token.Substring("doors".Length);
            }
            else if (token.StartsWith("door", StringComparison.Ordinal))
            {
                suffix = token.Substring("door".Length);
            }
            else
            {
                return false;
            }

            return suffix.Length > 0 && suffix.All(char.IsDigit);
        }

        private static bool HasValidPrimitiveShape(
            DonorWorldSafeColliderRecord record) =>
            record.ColliderType switch
            {
                "BoxCollider" =>
                    IsFinitePositive(record.Size.x) &&
                    IsFinitePositive(record.Size.y) &&
                    IsFinitePositive(record.Size.z),
                "CapsuleCollider" =>
                    IsFinitePositive(record.Radius) &&
                    IsFinitePositive(record.Height) &&
                    record.Direction is >= 0 and <= 2,
                "SphereCollider" => IsFinitePositive(record.Radius),
                _ => false
            };

        private static string ResolveLayerName(
            DonorWorldSafeColliderRecord record)
        {
            if (SurfaceCategories.Contains(record.SemanticCategory) ||
                ReviewedSurfacePaths.Contains(record.HierarchyPath) ||
                record.HierarchyPath.StartsWith(
                    "MAP/MESH/TERRAIN_OBJ/",
                    StringComparison.Ordinal) ||
                string.Equals(
                    record.HierarchyPath,
                    "MAP/MESH/TRACKFIELD",
                    StringComparison.Ordinal))
            {
                return WorldSurfaceLayer;
            }

            return WorldSolidLayer;
        }

        private static bool ContainsAny(
            string value,
            IEnumerable<string> fragments)
        {
            string normalized = (value ?? string.Empty).ToLowerInvariant();
            return fragments.Any(fragment =>
                normalized.Contains(fragment, StringComparison.Ordinal));
        }

        private static Dictionary<string, SafetyCriticalLock>
            LoadSafetyCriticalLocks(
                IReadOnlyDictionary<string, DonorWorldSafeColliderRecord>
                    sourceById,
                IReadOnlyDictionary<string, WorldBaselineSanitationEntry>
                    entityById)
        {
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
            if (colliderIdIndex < 0 || entityIdIndex < 0 ||
                typeIndex < 0 || meshGuidIndex < 0 ||
                ownershipIndex < 0 || reasonIndex < 0)
            {
                throw new FormatException(
                    "06B2 collider allowlist has an invalid header.");
            }

            var result = new Dictionary<string, SafetyCriticalLock>(
                StringComparer.Ordinal);
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
                        "an invalid column count.");
                }

                string colliderId = values[colliderIdIndex];
                string entityId = values[entityIdIndex];
                if (!result.TryAdd(
                        colliderId,
                        new SafetyCriticalLock(
                            values[ownershipIndex],
                            values[reasonIndex])) ||
                    !entityIds.Add(entityId))
                {
                    throw new InvalidDataException(
                        "The immutable 06B2 safety allowlist contains a " +
                        "duplicate collider or entity: " + colliderId);
                }
                if (!sourceById.TryGetValue(
                        colliderId,
                        out DonorWorldSafeColliderRecord source) ||
                    !entityById.TryGetValue(entityId, out var entity) ||
                    !entity.EffectiveActive ||
                    !string.Equals(
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
                        StringComparison.Ordinal) ||
                    !source.SourceEnabled || source.SourceIsTrigger ||
                    source.Convex ||
                    source.ColliderType is not
                        ("MeshCollider" or "BoxCollider"))
                {
                    throw new InvalidDataException(
                        "06B2 safety lock differs from the frozen source " +
                        "row: " + colliderId);
                }
                if (source.ColliderType == "MeshCollider" &&
                    !WorldReferenceMeshLibrarySync.IsUsableMeshGuid(
                        source.MeshGuid))
                {
                    throw new InvalidDataException(
                        "Safety-critical MeshCollider has no audited mesh: " +
                        colliderId);
                }
                if (source.ColliderType == "BoxCollider" &&
                    !HasValidPrimitiveShape(source))
                {
                    throw new InvalidDataException(
                        "Safety-critical BoxCollider has invalid size: " +
                        colliderId);
                }
            }

            return result;
        }

        private static Dictionary<string, DonorWorldSafeColliderRecord>
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
                "Convex", "MeshGuid", "MeshFileId", "CenterX",
                "CenterY", "CenterZ", "SizeX", "SizeY", "SizeZ",
                "Radius", "Height", "Direction"
            };
            foreach (string column in required)
            {
                if (!indices.ContainsKey(column))
                {
                    throw new FormatException(
                        "Frozen collider table lacks column " + column);
                }
            }

            var result = new Dictionary<string, DonorWorldSafeColliderRecord>(
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
                var record = new DonorWorldSafeColliderRecord
                {
                    ColliderStableId = Get("StableId"),
                    EntityStableId = Get("EntityStableId"),
                    HierarchyPath = Get("HierarchyPath"),
                    ObjectName = Get("ObjectName"),
                    ColliderType = Get("ColliderType"),
                    SourceEnabled = Get("Enabled") == "1",
                    SourceIsTrigger = Get("IsTrigger") == "1",
                    Convex = Get("Convex") == "1",
                    MeshGuid = Get("MeshGuid"),
                    MeshFileId = long.Parse(
                        Get("MeshFileId"),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture),
                    Center = new Vector3(
                        Number("CenterX"), Number("CenterY"),
                        Number("CenterZ")),
                    Size = new Vector3(
                        Number("SizeX"), Number("SizeY"),
                        Number("SizeZ")),
                    Radius = Number("Radius"),
                    Height = Number("Height"),
                    Direction = int.Parse(
                        Get("Direction"),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture)
                };
                result.Add(record.ColliderStableId, record);
            }

            return result;
        }

        private static bool IsFinitePositive(float value) =>
            float.IsFinite(value) && value > 0f;

        private readonly struct SafetyCriticalLock
        {
            public SafetyCriticalLock(
                string ownershipPolicy,
                string reason)
            {
                OwnershipPolicy = ownershipPolicy;
                Reason = reason;
            }

            public string OwnershipPolicy { get; }
            public string Reason { get; }
        }

        private sealed class SourceHierarchyNode
        {
            public SourceHierarchyNode(
                string stableId,
                string sourceObjectId,
                string parentObjectId,
                bool hasRigidbody)
            {
                StableId = stableId;
                SourceObjectId = sourceObjectId;
                ParentObjectId = parentObjectId;
                HasRigidbody = hasRigidbody;
            }

            public string StableId { get; }
            public string SourceObjectId { get; }
            public string ParentObjectId { get; }
            public bool HasRigidbody { get; }
        }
    }

    public sealed class DonorWorldSolidCollisionPlanData
    {
        public DonorWorldSolidCollisionPlanData(
            DonorWorldSafeColliderRecord[] dispositions,
            DonorWorldSafeColliderRecord[] included)
        {
            Dispositions = dispositions;
            Included = included;
        }

        public IReadOnlyList<DonorWorldSafeColliderRecord> Dispositions
        {
            get;
        }

        public IReadOnlyList<DonorWorldSafeColliderRecord> Included
        {
            get;
        }
    }
}
