using System;
using UnityEngine;

namespace MSC.World.Data
{
    public static class WorldGeometryDatabaseVersion
    {
        public const int CurrentSchemaVersion = 1;
        public const string CurrentDatabaseVersion = "04A1.1";
    }

    public static class WorldGeometryDatabaseMigration
    {
        public static bool TryMigrate(string json, out string migratedJson, out string error)
        {
            migratedJson = string.Empty;
            error = string.Empty;
            try
            {
                WorldGeometryDatabase database = WorldGeometryDatabase.FromJson(json);
                if (database.SchemaVersion == WorldGeometryDatabaseVersion.CurrentSchemaVersion)
                {
                    migratedJson = json;
                    return true;
                }

                if (database.SchemaVersion == 0 && json.Contains("\"schemaVersion\": 0", StringComparison.Ordinal))
                {
                    migratedJson = json.Replace("\"schemaVersion\": 0", "\"schemaVersion\": 1");
                    return true;
                }

                error = $"Unsupported world database schema {database.SchemaVersion}.";
                return false;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }

    public enum WorldReplacementStatus
    {
        DonorReference,
        PrototypeRuntime,
        ReplacementPlanned,
        ReplacementInProgress,
        ProductionReplacement,
        Verified,
        Rejected,
        Missing,
        Unsupported
    }

    [Serializable]
    public sealed class WorldGeometryDatabase
    {
        [SerializeField] private int schemaVersion = WorldGeometryDatabaseVersion.CurrentSchemaVersion;
        [SerializeField] private string databaseVersion = WorldGeometryDatabaseVersion.CurrentDatabaseVersion;
        [SerializeField] private string generatorId = string.Empty;
        [SerializeField] private string generatorVersion = string.Empty;
        [SerializeField] private WorldCoordinateConversionConfig coordinateConversion = new WorldCoordinateConversionConfig();
        [SerializeField] private WorldSourceRecord[] sources = Array.Empty<WorldSourceRecord>();
        [SerializeField] private WorldEntityRecord[] entities = Array.Empty<WorldEntityRecord>();
        [SerializeField] private WorldCellRecord[] cells = Array.Empty<WorldCellRecord>();
        [SerializeField] private WorldLandmarkRecord[] landmarks = Array.Empty<WorldLandmarkRecord>();
        [SerializeField] private WorldMissingReferenceRecord[] missingReferences = Array.Empty<WorldMissingReferenceRecord>();

        public int SchemaVersion => schemaVersion;
        public string DatabaseVersion => databaseVersion;
        public string GeneratorId => generatorId;
        public string GeneratorVersion => generatorVersion;
        public WorldCoordinateConversionConfig CoordinateConversion => coordinateConversion;
        public WorldSourceRecord[] Sources => sources;
        public WorldEntityRecord[] Entities => entities;
        public WorldCellRecord[] Cells => cells;
        public WorldLandmarkRecord[] Landmarks => landmarks;
        public WorldMissingReferenceRecord[] MissingReferences => missingReferences;

        public static WorldGeometryDatabase FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("World geometry database JSON must not be empty.", nameof(json));
            }

            WorldGeometryDatabase database = JsonUtility.FromJson<WorldGeometryDatabase>(json);
            if (database == null)
            {
                throw new FormatException("World geometry database JSON could not be parsed.");
            }

            return database;
        }
    }

    [Serializable]
    public sealed class WorldSourceRecord
    {
        [SerializeField] private string sourceId = string.Empty;
        [SerializeField] private string donorRelativePath = string.Empty;
        [SerializeField] private string sourceSha256 = string.Empty;
        [SerializeField] private string sourceScene = string.Empty;
        [SerializeField] private string extractionTool = string.Empty;
        [SerializeField] private string extractionToolVersion = string.Empty;
        [SerializeField] private string extractionStatus = string.Empty;

        public string SourceId => sourceId;
        public string DonorRelativePath => donorRelativePath;
        public string SourceSha256 => sourceSha256;
        public string SourceScene => sourceScene;
        public string ExtractionTool => extractionTool;
        public string ExtractionToolVersion => extractionToolVersion;
        public string ExtractionStatus => extractionStatus;
    }

    [Serializable]
    public sealed class WorldEntityRecord
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string sourceId = string.Empty;
        [SerializeField] private long sourceObjectId;
        [SerializeField] private string originalHierarchyPath = string.Empty;
        [SerializeField] private string originalName = string.Empty;
        [SerializeField] private string normalizedName = string.Empty;
        [SerializeField] private string semanticCategory = string.Empty;
        [SerializeField] private WorldTransformRecord transform = new WorldTransformRecord();
        [SerializeField] private WorldHierarchyRecord hierarchy = new WorldHierarchyRecord();
        [SerializeField] private WorldMeshReferenceRecord meshReference = new WorldMeshReferenceRecord();
        [SerializeField] private WorldColliderRecord[] colliders = Array.Empty<WorldColliderRecord>();
        [SerializeField] private Bounds sourceBounds;
        [SerializeField] private Bounds convertedBounds;
        [SerializeField] private long staticFlags;
        [SerializeField] private bool active;
        [SerializeField] private string cellId = string.Empty;
        [SerializeField] private string interiorExterior = string.Empty;
        [SerializeField] private string landmarkTag = string.Empty;
        [SerializeField] private WorldReplacementStatus replacementStatus;
        [SerializeField] private string transferStatus = string.Empty;
        [SerializeField] private string sourceManifestReference = string.Empty;
        [SerializeField] private string notes = string.Empty;

        public string StableId => stableId;
        public string SourceId => sourceId;
        public long SourceObjectId => sourceObjectId;
        public string OriginalHierarchyPath => originalHierarchyPath;
        public string OriginalName => originalName;
        public string NormalizedName => normalizedName;
        public string SemanticCategory => semanticCategory;
        public WorldTransformRecord Transform => transform;
        public WorldHierarchyRecord Hierarchy => hierarchy;
        public WorldMeshReferenceRecord MeshReference => meshReference;
        public WorldColliderRecord[] Colliders => colliders;
        public Bounds SourceBounds => sourceBounds;
        public Bounds ConvertedBounds => convertedBounds;
        public long StaticFlags => staticFlags;
        public bool Active => active;
        public string CellId => cellId;
        public string InteriorExterior => interiorExterior;
        public string LandmarkTag => landmarkTag;
        public WorldReplacementStatus ReplacementStatus => replacementStatus;
        public string TransferStatus => transferStatus;
        public string SourceManifestReference => sourceManifestReference;
        public string Notes => notes;
    }

    [Serializable]
    public sealed class WorldTransformRecord
    {
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Quaternion localRotation = Quaternion.identity;
        [SerializeField] private Vector3 localScale = Vector3.one;
        [SerializeField] private Vector3 sourceWorldPosition;
        [SerializeField] private Quaternion sourceWorldRotation = Quaternion.identity;
        [SerializeField] private Vector3 sourceWorldScale = Vector3.one;
        [SerializeField] private Vector3 convertedWorldPosition;
        [SerializeField] private Quaternion convertedWorldRotation = Quaternion.identity;
        [SerializeField] private Vector3 convertedWorldScale = Vector3.one;

        public Vector3 LocalPosition => localPosition;
        public Quaternion LocalRotation => localRotation;
        public Vector3 LocalScale => localScale;
        public Vector3 SourceWorldPosition => sourceWorldPosition;
        public Quaternion SourceWorldRotation => sourceWorldRotation;
        public Vector3 SourceWorldScale => sourceWorldScale;
        public Vector3 ConvertedWorldPosition => convertedWorldPosition;
        public Quaternion ConvertedWorldRotation => convertedWorldRotation;
        public Vector3 ConvertedWorldScale => convertedWorldScale;
    }

    [Serializable]
    public sealed class WorldHierarchyRecord
    {
        [SerializeField] private string parentStableId = string.Empty;
        [SerializeField] private long parentSourceObjectId;

        public string ParentStableId => parentStableId;
        public long ParentSourceObjectId => parentSourceObjectId;
    }

    [Serializable]
    public sealed class WorldMeshReferenceRecord
    {
        [SerializeField] private string meshGuid = string.Empty;
        [SerializeField] private string meshName = string.Empty;
        [SerializeField] private string externalAssetPath = string.Empty;
        [SerializeField] private string[] materialGuids = Array.Empty<string>();
        [SerializeField] private string resolutionStatus = string.Empty;

        public string MeshGuid => meshGuid;
        public string MeshName => meshName;
        public string ExternalAssetPath => externalAssetPath;
        public string[] MaterialGuids => materialGuids;
        public string ResolutionStatus => resolutionStatus;
    }

    [Serializable]
    public sealed class WorldColliderRecord
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string colliderType = string.Empty;
        [SerializeField] private bool convex;
        [SerializeField] private Vector3 dimensions;
        [SerializeField] private Vector3 center;
        [SerializeField] private bool trigger;
        [SerializeField] private string meshGuid = string.Empty;
        [SerializeField] private WorldReplacementStatus replacementStatus;

        public string StableId => stableId;
        public string ColliderType => colliderType;
        public bool Convex => convex;
        public Vector3 Dimensions => dimensions;
        public Vector3 Center => center;
        public bool Trigger => trigger;
        public string MeshGuid => meshGuid;
        public WorldReplacementStatus ReplacementStatus => replacementStatus;
    }

    [Serializable] public sealed class WorldTerrainRecord { [SerializeField] private string stableId = string.Empty; [SerializeField] private Bounds bounds; public string StableId => stableId; public Bounds Bounds => bounds; }
    [Serializable] public sealed class WorldRoadRecord { [SerializeField] private string stableId = string.Empty; [SerializeField] private Vector3[] centerline = Array.Empty<Vector3>(); public string StableId => stableId; public Vector3[] Centerline => centerline; }
    [Serializable] public sealed class WorldWaterRecord { [SerializeField] private string stableId = string.Empty; [SerializeField] private Bounds bounds; public string StableId => stableId; public Bounds Bounds => bounds; }
    [Serializable] public sealed class WorldVegetationRecord { [SerializeField] private string stableId = string.Empty; [SerializeField] private Vector3 position; public string StableId => stableId; public Vector3 Position => position; }
    [Serializable] public sealed class WorldInteriorRecord { [SerializeField] private string stableId = string.Empty; [SerializeField] private string buildingStableId = string.Empty; public string StableId => stableId; public string BuildingStableId => buildingStableId; }
    [Serializable] public sealed class WorldPortalRecord { [SerializeField] private string stableId = string.Empty; [SerializeField] private string interiorStableId = string.Empty; public string StableId => stableId; public string InteriorStableId => interiorStableId; }
    [Serializable] public sealed class WorldLandmarkRecord { [SerializeField] private string stableId = string.Empty; [SerializeField] private string name = string.Empty; [SerializeField] private Vector3 position; public string StableId => stableId; public string Name => name; public Vector3 Position => position; }
    [Serializable] public sealed class WorldCellRecord { [SerializeField] private string cellId = string.Empty; [SerializeField] private Bounds bounds; [SerializeField] private string[] entityStableIds = Array.Empty<string>(); public string CellId => cellId; public Bounds Bounds => bounds; public string[] EntityStableIds => entityStableIds; }
    [Serializable] public sealed class WorldMissingReferenceRecord { [SerializeField] private string entityStableId = string.Empty; [SerializeField] private string referenceType = string.Empty; [SerializeField] private string referenceId = string.Empty; [SerializeField] private string reason = string.Empty; public string EntityStableId => entityStableId; public string ReferenceType => referenceType; public string ReferenceId => referenceId; public string Reason => reason; }
    [Serializable] public sealed class WorldReplacementRecord { [SerializeField] private string entityStableId = string.Empty; [SerializeField] private string replacementAssetPath = string.Empty; [SerializeField] private WorldReplacementStatus status; public string EntityStableId => entityStableId; public string ReplacementAssetPath => replacementAssetPath; public WorldReplacementStatus Status => status; }
    [Serializable] public sealed class WorldTransferReport { [SerializeField] private int sourceObjectCount; [SerializeField] private int generatedEntityCount; [SerializeField] private int missingReferenceCount; public int SourceObjectCount => sourceObjectCount; public int GeneratedEntityCount => generatedEntityCount; public int MissingReferenceCount => missingReferenceCount; }
}
