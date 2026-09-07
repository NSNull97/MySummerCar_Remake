using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSCMapMigration
{
    public enum MapMeshCategory
    {
        GroundCandidate,
        RoadAsphalt,
        RoadDirtOrGravel,
        RoadStructure,
        Water,
        Building,
        Vegetation,
        Utility,
        Prop,
        Technical,
        ResidualUnsupported,
        Ambiguous
    }

    public enum TerrainMaterialTransferMode
    {
        Auto,
        PlanarGlobalTexture,
        SafePlaceholder
    }

    [Serializable]
    internal sealed class GeneratedTerrainManifest
    {
        public string schemaVersion = string.Empty;
        public string sourceFingerprint = string.Empty;
        public int tileCountX;
        public int tileCountZ;
        public int heightmapResolution;
        public float tileSize;
        public int coveredSamples;
        public int interiorFilledSamples;
        public int exteriorHoleSamples;
        public int elevatedRoadTriangles;
        public int residualGroundTriangles;
        public string materialTransfer = string.Empty;
        public float meanSmoothingDisplacement;
        public float medianSmoothingDisplacement;
        public float p95SmoothingDisplacement;
        public float maximumSmoothingDisplacement;
        public int smoothingLimitHitCount;
    }

    [Serializable]
    public sealed class MapMeshInventory
    {
        public string schemaVersion = "1.1";
        public string generatedUtc = string.Empty;
        public string unityVersion = string.Empty;
        public string sourceScene = string.Empty;
        public string sourceRoot = string.Empty;
        public string sourceSceneSha256 = string.Empty;
        public string sourceSetFingerprint = string.Empty;
        public string authoritativeSourceReason = string.Empty;
        public List<string> scannedScenes = new List<string>();
        public List<MapMeshRecord> records = new List<MapMeshRecord>();
        public MapInventorySummary summary = new MapInventorySummary();
    }

    [Serializable]
    public sealed class MapInventorySummary
    {
        public int meshInstanceCount;
        public int uniqueMeshAssetCount;
        public int groundCandidateCount;
        public int roadMeshCount;
        public int residualUnsupportedCount;
        public int ambiguousCount;
        public int meshColliderCount;
        public int negativeScaleCount;
        public int reusedMeshAssetCount;
    }

    [Serializable]
    public sealed class MapMeshRecord
    {
        public string recordId = string.Empty;
        public string scenePath = string.Empty;
        public string hierarchyPath = string.Empty;
        public MapMeshCategory category;
        public string gameObjectName = string.Empty;
        public string rendererType = string.Empty;
        public string meshAssetName = string.Empty;
        public string assetGuid = string.Empty;
        public long localFileId;
        // Legacy report field: preserved when reading 1.0 reports; new scans leave it zero.
        public int instanceId;
        // Full Unity 6.6 session identity for diagnostics only, never a persistent record key.
        // Absent from 1.0 reports, where its default zero means unavailable.
        public ulong entityId;
        public int vertexCount;
        public int triangleCount;
        public int subMeshCount;
        public List<string> materialNames = new List<string>();
        public SerializableTransform localTransform = new SerializableTransform();
        public SerializableMatrix worldMatrix = new SerializableMatrix();
        public SerializableBounds worldBounds = new SerializableBounds();
        public bool hasMeshCollider;
        public bool meshColliderEnabled;
        public bool activeSelf;
        public bool activeInHierarchy;
        public bool hasNegativeWorldDeterminant;
        public string layerName = string.Empty;
        public string tag = string.Empty;
        public string provenanceStableId = string.Empty;
        public string provenanceHierarchyPath = string.Empty;
        public string provenanceSemanticCategory = string.Empty;
        public string classificationReason = string.Empty;
        public float upwardTriangleRatio;
        public float uvPlanarityRms = -1f;
        public int sharedMeshInstanceCount;
        public bool terrainCoveragePassed;
        public float terrainCoverageRatio;
    }

    [Serializable]
    public sealed class SerializableTransform
    {
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 scale = Vector3.one;

        public static SerializableTransform From(Transform transform) =>
            new SerializableTransform
            {
                position = transform.localPosition,
                rotation = transform.localRotation,
                scale = transform.localScale
            };
    }

    [Serializable]
    public sealed class SerializableMatrix
    {
        public float[] values = new float[16];

        public static SerializableMatrix From(Matrix4x4 matrix)
        {
            var value = new SerializableMatrix();
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    value.values[row * 4 + column] = matrix[row, column];
                }
            }

            return value;
        }

        public Matrix4x4 ToMatrix()
        {
            var matrix = new Matrix4x4();
            if (values == null || values.Length != 16)
            {
                return matrix;
            }

            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    matrix[row, column] = values[row * 4 + column];
                }
            }

            return matrix;
        }
    }

    [Serializable]
    public sealed class SerializableBounds
    {
        public Vector3 center;
        public Vector3 size;

        public static SerializableBounds From(Bounds bounds) =>
            new SerializableBounds { center = bounds.center, size = bounds.size };
    }

    internal sealed class ScannedMeshInstance
    {
        public MapMeshRecord Record { get; init; } = null!;
        public Renderer Renderer { get; init; } = null!;
        public Mesh Mesh { get; init; } = null!;
        public Matrix4x4 LocalToWorld { get; init; }
        public bool OwnsMesh { get; init; }
    }

    internal sealed class MapScanResult
    {
        public MapMeshInventory Inventory { get; init; } = null!;
        public List<ScannedMeshInstance> Instances { get; init; } = new List<ScannedMeshInstance>();
        public List<string> OpenedScenePaths { get; init; } = new List<string>();
    }

    [Serializable]
    public sealed class MapExportManifest
    {
        public string schemaVersion = "1.0";
        public string exporterVersion = string.Empty;
        public string generatedUtc = string.Empty;
        public string sourceFingerprint = string.Empty;
        public string exportRoot = string.Empty;
        public string preferredFormat = "OBJ";
        public bool fbxExporterAvailable;
        public int instanceCount;
        public int uniqueObjectFileCount;
        public int roadInstanceCount;
        public int groundInstanceCount;
        public SerializableBounds sceneBounds = new SerializableBounds();
        public List<MapExportEntry> entries = new List<MapExportEntry>();
        public List<string> createdFiles = new List<string>();
        public List<string> warnings = new List<string>();
    }

    [Serializable]
    public sealed class MapExportEntry
    {
        public string recordId = string.Empty;
        public string scenePath = string.Empty;
        public string hierarchyPath = string.Empty;
        public MapMeshCategory category;
        public string geometryFile = string.Empty;
        public bool geometryShared;
        public SerializableMatrix worldMatrix = new SerializableMatrix();
    }

    [Serializable]
    public sealed class MapMaterialCatalog
    {
        public string schemaVersion = "1.0";
        public List<MapMaterialRecord> materials = new List<MapMaterialRecord>();
    }

    [Serializable]
    public sealed class MapMaterialRecord
    {
        public string name = string.Empty;
        public string assetGuid = string.Empty;
        public string shader = string.Empty;
        public Color baseColor = Color.white;
        public string baseColorTexture = string.Empty;
        public string normalTexture = string.Empty;
        public string maskTexture = string.Empty;
        public Vector2 tiling = Vector2.one;
        public Vector2 offset = Vector2.zero;
        public List<string> usedByRecordIds = new List<string>();
    }

    [Serializable]
    public sealed class MapMigrationValidationReport
    {
        public string schemaVersion = "1.0";
        public string generatedUtc = string.Empty;
        public bool passed;
        public string sourceScene = string.Empty;
        public string outputScene = string.Empty;
        public string sourceSceneSha256Before = string.Empty;
        public string sourceSceneSha256After = string.Empty;
        public int inventoryMeshCount;
        public int exportedMeshCount;
        public int terrainTileCount;
        public int tileCountX;
        public int tileCountZ;
        public int heightmapResolution;
        public float tileSize;
        public float sampleSpacing;
        public float sourceCoverage;
        public float meanHeightError;
        public float maximumHeightError;
        public float maximumHeightErrorSigned;
        public string maximumHeightErrorRecordId = string.Empty;
        public Vector3 maximumHeightErrorPoint;
        public float maximumSeamError;
        public float maximumRoadProtrusion;
        public float maximumRoadEdgeGap;
        public string maximumRoadEdgeGapRecordId = string.Empty;
        public Vector3 maximumRoadEdgeGapPoint;
        public int elevatedRoadTriangleCount;
        public int preservedResidualTriangleCount;
        public int roadPreservationFailureCount;
        public int bridgeBurialFailureCount;
        public int missingScriptCount;
        public int committedGroundRendererCount;
        public int residualUnsupportedCount;
        public int ambiguousCount;
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
    }

    internal sealed class MapMigrationCancelledException : OperationCanceledException
    {
        public MapMigrationCancelledException() : base("Map migration was cancelled by the user.")
        {
        }
    }
}
