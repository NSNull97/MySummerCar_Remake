using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.World.Vegetation
{
    public enum PackedWoodyCategory : byte
    {
        OriginalTree = 0,
        BoundaryForest = 1,
        ShrubOrUndergrowth = 2
    }

    public enum PackedWoodyPlacementMethod : byte
    {
        DonorOriginal = 0,
        NaturalInfill = 1,
        BoundaryForest = 2,
        DonorShrub = 3,
        ForestFloor = 4
    }

    public enum PackedWoodySpecies : byte
    {
        Spruce = 0,
        Pine = 1,
        Birch = 2,
        Aspen = 3,
        Shrub = 4,
        Understory = 5,
        RockAccent = 6
    }

    [Serializable]
    public sealed class PackedWoodyDrawPart
    {
        [SerializeField] private Mesh mesh;
        [SerializeField] private Material material;
        [SerializeField, Min(0)] private int subMeshIndex;
        [SerializeField] private ShadowCastingMode shadowCasting =
            ShadowCastingMode.On;
        [SerializeField] private bool receiveShadows = true;

        public Mesh Mesh => mesh;
        public Material Material => material;
        public int SubMeshIndex => subMeshIndex;
        public ShadowCastingMode ShadowCasting => shadowCasting;
        public bool ReceiveShadows => receiveShadows;

        public PackedWoodyDrawPart(
            Mesh configuredMesh,
            Material configuredMaterial,
            int configuredSubMeshIndex,
            ShadowCastingMode configuredShadowCasting,
            bool configuredReceiveShadows)
        {
            mesh = configuredMesh;
            material = configuredMaterial;
            subMeshIndex = configuredSubMeshIndex;
            shadowCasting = configuredShadowCasting;
            receiveShadows = configuredReceiveShadows;
        }
    }

    [Serializable]
    public sealed class PackedWoodyLod
    {
        [SerializeField, Range(0.0001f, 1f)]
        private float screenRelativeTransitionHeight = 0.01f;
        [SerializeField] private PackedWoodyDrawPart[] drawParts =
            Array.Empty<PackedWoodyDrawPart>();

        public float ScreenRelativeTransitionHeight =>
            screenRelativeTransitionHeight;
        public IReadOnlyList<PackedWoodyDrawPart> DrawParts =>
            drawParts ?? Array.Empty<PackedWoodyDrawPart>();
        internal PackedWoodyDrawPart[] DrawPartData =>
            drawParts ?? Array.Empty<PackedWoodyDrawPart>();

        public PackedWoodyLod(
            float configuredScreenRelativeTransitionHeight,
            PackedWoodyDrawPart[] configuredDrawParts)
        {
            screenRelativeTransitionHeight =
                configuredScreenRelativeTransitionHeight;
            drawParts = configuredDrawParts != null
                ? (PackedWoodyDrawPart[])configuredDrawParts.Clone()
                : Array.Empty<PackedWoodyDrawPart>();
        }
    }

    /// <summary>
    /// One reviewed prefab reduced to static proxy meshes. Renderer-child
    /// transforms are baked into the meshes, while authored materials and every
    /// LOD renderer/submesh remain explicit draw parts.
    /// </summary>
    public sealed partial class PackedWoodyPrototypeAsset : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string presentationVersion;
        [SerializeField] private string stableKey;
        [SerializeField] private string sourceAssetGuid;
        [SerializeField] private string sourceDependencyHash;
        [SerializeField] private string projectPolicySignature;
        [SerializeField] private PackedWoodySpecies species;
        [SerializeField, Min(0.01f)] private float sourceHeightMeters = 1f;
        [SerializeField] private Bounds localBounds =
            new Bounds(Vector3.zero, Vector3.one);
        [SerializeField] private PackedWoodyLod[] lods =
            Array.Empty<PackedWoodyLod>();

        public int SchemaVersion => schemaVersion;
        public string PresentationVersion => presentationVersion;
        public string StableKey => stableKey;
        public string SourceAssetGuid => sourceAssetGuid;
        public string SourceDependencyHash => sourceDependencyHash;
        public string ProjectPolicySignature => projectPolicySignature;
        public PackedWoodySpecies Species => species;
        public float SourceHeightMeters => sourceHeightMeters;
        public Bounds LocalBounds => localBounds;
        public IReadOnlyList<PackedWoodyLod> Lods =>
            lods ?? Array.Empty<PackedWoodyLod>();
        internal PackedWoodyLod[] LodData =>
            lods ?? Array.Empty<PackedWoodyLod>();

        public IReadOnlyList<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (schemaVersion != CurrentSchemaVersion)
                errors.Add("Packed woody prototype schema is unsupported.");
            if (string.IsNullOrWhiteSpace(presentationVersion) ||
                string.IsNullOrWhiteSpace(stableKey) ||
                string.IsNullOrWhiteSpace(sourceAssetGuid) ||
                string.IsNullOrWhiteSpace(sourceDependencyHash) ||
                string.IsNullOrWhiteSpace(projectPolicySignature))
                errors.Add("Packed woody prototype provenance is incomplete.");
            if (!float.IsFinite(sourceHeightMeters) || sourceHeightMeters <= 0f)
                errors.Add("Packed woody prototype height is invalid.");
            if ((int)species < (int)PackedWoodySpecies.Spruce ||
                (int)species > (int)PackedWoodySpecies.RockAccent)
                errors.Add("Packed woody prototype species is invalid.");
            if (!FiniteBounds(localBounds) || localBounds.size.sqrMagnitude <= 0f)
                errors.Add("Packed woody prototype bounds are invalid.");

            PackedWoodyLod[] configured = lods ?? Array.Empty<PackedWoodyLod>();
            if (configured.Length == 0)
                errors.Add("Packed woody prototype contains no LODs.");
            if (species >= PackedWoodySpecies.Spruce &&
                species <= PackedWoodySpecies.Aspen && configured.Length < 2)
                errors.Add("Packed woody tree prototype requires a near and far LOD.");
            float previousThreshold = float.PositiveInfinity;
            for (int lodIndex = 0; lodIndex < configured.Length; lodIndex++)
            {
                PackedWoodyLod lod = configured[lodIndex];
                if (lod == null)
                {
                    errors.Add("Packed woody prototype contains a null LOD.");
                    continue;
                }
                float threshold = lod.ScreenRelativeTransitionHeight;
                if (!float.IsFinite(threshold) || threshold <= 0f ||
                    threshold >= previousThreshold)
                    errors.Add("Packed woody LOD thresholds must be finite, positive and descending.");
                previousThreshold = threshold;
                if (lod.DrawParts.Count == 0)
                    errors.Add("Packed woody LOD contains no draw parts.");
                foreach (PackedWoodyDrawPart part in lod.DrawParts)
                {
                    if (part == null || part.Mesh == null ||
                        part.Material == null || part.Material.shader == null)
                    {
                        errors.Add("Packed woody LOD has a missing mesh/material/shader.");
                        continue;
                    }
                    if (part.SubMeshIndex < 0 ||
                        part.SubMeshIndex >= part.Mesh.subMeshCount)
                        errors.Add("Packed woody draw part submesh is invalid.");
                    if (!part.Material.enableInstancing)
                        errors.Add("Packed woody material must enable GPU instancing: " +
                            part.Material.name + ".");
                }
            }
            return errors;
        }

        internal bool TryValidateRuntimeHeader(out string error)
        {
            if (schemaVersion != CurrentSchemaVersion ||
                string.IsNullOrWhiteSpace(presentationVersion) ||
                string.IsNullOrWhiteSpace(stableKey) ||
                string.IsNullOrWhiteSpace(sourceAssetGuid) ||
                string.IsNullOrWhiteSpace(sourceDependencyHash) ||
                string.IsNullOrWhiteSpace(projectPolicySignature))
            {
                error = "prototype schema/provenance is invalid";
                return false;
            }
            if ((int)species < (int)PackedWoodySpecies.Spruce ||
                (int)species > (int)PackedWoodySpecies.RockAccent ||
                !float.IsFinite(sourceHeightMeters) ||
                sourceHeightMeters <= 0f || !FiniteBounds(localBounds) ||
                localBounds.size.sqrMagnitude <= 0f)
            {
                error = "prototype height/bounds are invalid";
                return false;
            }

            PackedWoodyLod[] configured = lods;
            if (configured == null || configured.Length == 0)
            {
                error = "prototype has no LODs";
                return false;
            }
            if (species >= PackedWoodySpecies.Spruce &&
                species <= PackedWoodySpecies.Aspen && configured.Length < 2)
            {
                error = "tree prototype has no separate far LOD";
                return false;
            }
            float previousThreshold = float.PositiveInfinity;
            for (int lodIndex = 0; lodIndex < configured.Length; lodIndex++)
            {
                PackedWoodyLod lod = configured[lodIndex];
                if (lod == null ||
                    !float.IsFinite(lod.ScreenRelativeTransitionHeight) ||
                    lod.ScreenRelativeTransitionHeight <= 0f ||
                    lod.ScreenRelativeTransitionHeight >= previousThreshold)
                {
                    error = "prototype LOD thresholds are invalid";
                    return false;
                }
                previousThreshold = lod.ScreenRelativeTransitionHeight;
                PackedWoodyDrawPart[] parts = lod.DrawPartData;
                if (parts == null || parts.Length == 0)
                {
                    error = "prototype LOD has no draw parts";
                    return false;
                }
                for (int partIndex = 0; partIndex < parts.Length; partIndex++)
                {
                    PackedWoodyDrawPart part = parts[partIndex];
                    if (part == null || part.Mesh == null ||
                        part.Material == null || part.Material.shader == null ||
                        !part.Material.enableInstancing ||
                        part.SubMeshIndex < 0 ||
                        part.SubMeshIndex >= part.Mesh.subMeshCount)
                    {
                        error = "prototype draw part is invalid";
                        return false;
                    }
                }
            }
            error = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredPresentationVersion,
            string configuredStableKey,
            string configuredSourceAssetGuid,
            string configuredSourceDependencyHash,
            string configuredProjectPolicySignature,
            PackedWoodySpecies configuredSpecies,
            float configuredSourceHeightMeters,
            Bounds configuredLocalBounds,
            PackedWoodyLod[] configuredLods)
        {
            schemaVersion = CurrentSchemaVersion;
            presentationVersion = configuredPresentationVersion;
            stableKey = configuredStableKey;
            sourceAssetGuid = configuredSourceAssetGuid;
            sourceDependencyHash = configuredSourceDependencyHash;
            projectPolicySignature = configuredProjectPolicySignature;
            species = configuredSpecies;
            sourceHeightMeters = configuredSourceHeightMeters;
            localBounds = configuredLocalBounds;
            lods = configuredLods != null
                ? (PackedWoodyLod[])configuredLods.Clone()
                : Array.Empty<PackedWoodyLod>();
        }
#endif

        private static bool FiniteBounds(Bounds bounds) =>
            float.IsFinite(bounds.center.x) &&
            float.IsFinite(bounds.center.y) &&
            float.IsFinite(bounds.center.z) &&
            float.IsFinite(bounds.size.x) &&
            float.IsFinite(bounds.size.y) &&
            float.IsFinite(bounds.size.z) &&
            bounds.size.x >= 0f && bounds.size.y >= 0f && bounds.size.z >= 0f;
    }

    [Serializable]
    public sealed class PackedWoodyBatch
    {
        public const int MaximumInstanceCount = 1023;

        [SerializeField, Min(0)] private int prototypeIndex;
        [SerializeField] private Bounds worldBounds;
        [SerializeField, Min(0.01f)] private float maximumHeightMeters = 1f;
        [SerializeField] private Matrix4x4[] matrices = Array.Empty<Matrix4x4>();

        public int PrototypeIndex => prototypeIndex;
        public Bounds WorldBounds => worldBounds;
        public float MaximumHeightMeters => maximumHeightMeters;
        public IReadOnlyList<Matrix4x4> Matrices =>
            matrices ?? Array.Empty<Matrix4x4>();
        internal Matrix4x4[] MatrixData => matrices ?? Array.Empty<Matrix4x4>();
        public int Count => matrices?.Length ?? 0;

        public PackedWoodyBatch(
            int configuredPrototypeIndex,
            Bounds configuredWorldBounds,
            float configuredMaximumHeightMeters,
            Matrix4x4[] configuredMatrices)
        {
            prototypeIndex = configuredPrototypeIndex;
            worldBounds = configuredWorldBounds;
            maximumHeightMeters = configuredMaximumHeightMeters;
            matrices = configuredMatrices != null
                ? (Matrix4x4[])configuredMatrices.Clone()
                : Array.Empty<Matrix4x4>();
        }
    }

    [Serializable]
    public struct PackedWoodyPlacementRecord
    {
        [SerializeField] private Hash128 stableIdHash;
        [SerializeField, Min(0)] private int prototypeIndex;
        [SerializeField, Min(0)] private int batchIndex;
        [SerializeField, Min(0)] private int matrixIndex;
        [SerializeField] private PackedWoodyCategory category;
        [SerializeField] private PackedWoodyPlacementMethod method;
        [SerializeField] private PackedWoodySpecies species;
        [SerializeField] private Vector3 worldPosition;
        [SerializeField, Min(0.01f)] private float heightMeters;

        public Hash128 StableIdHash => stableIdHash;
        public int PrototypeIndex => prototypeIndex;
        public int BatchIndex => batchIndex;
        public int MatrixIndex => matrixIndex;
        public PackedWoodyCategory Category => category;
        public PackedWoodyPlacementMethod Method => method;
        public PackedWoodySpecies Species => species;
        public Vector3 WorldPosition => worldPosition;
        public float HeightMeters => heightMeters;

        public PackedWoodyPlacementRecord(
            Hash128 configuredStableIdHash,
            int configuredPrototypeIndex,
            int configuredBatchIndex,
            int configuredMatrixIndex,
            PackedWoodyCategory configuredCategory,
            PackedWoodyPlacementMethod configuredMethod,
            PackedWoodySpecies configuredSpecies,
            Vector3 configuredWorldPosition,
            float configuredHeightMeters)
        {
            stableIdHash = configuredStableIdHash;
            prototypeIndex = configuredPrototypeIndex;
            batchIndex = configuredBatchIndex;
            matrixIndex = configuredMatrixIndex;
            category = configuredCategory;
            method = configuredMethod;
            species = configuredSpecies;
            worldPosition = configuredWorldPosition;
            heightMeters = configuredHeightMeters;
        }
    }

    [Serializable]
    public struct PackedWoodyCollisionRecord
    {
        [SerializeField] private Hash128 stableIdHash;
        [SerializeField] private Vector3 bottomCenter;
        [SerializeField, Min(0.01f)] private float capsuleHeight;
        [SerializeField, Min(0.01f)] private float capsuleRadius;

        public Hash128 StableIdHash => stableIdHash;
        public Vector3 BottomCenter => bottomCenter;
        public float CapsuleHeight => capsuleHeight;
        public float CapsuleRadius => capsuleRadius;

        public PackedWoodyCollisionRecord(
            Hash128 configuredStableIdHash,
            Vector3 configuredBottomCenter,
            float configuredCapsuleHeight,
            float configuredCapsuleRadius)
        {
            stableIdHash = configuredStableIdHash;
            bottomCenter = configuredBottomCenter;
            capsuleHeight = configuredCapsuleHeight;
            capsuleRadius = configuredCapsuleRadius;
        }
    }

    [Serializable]
    public struct PackedWoodyCollisionTile
    {
        [SerializeField] private int tileX;
        [SerializeField] private int tileZ;
        [SerializeField, Min(0)] private int startIndex;
        [SerializeField, Min(0)] private int count;
        [SerializeField] private Bounds worldBounds;

        public int TileX => tileX;
        public int TileZ => tileZ;
        public int StartIndex => startIndex;
        public int Count => count;
        public Bounds WorldBounds => worldBounds;

        public PackedWoodyCollisionTile(
            int configuredTileX,
            int configuredTileZ,
            int configuredStartIndex,
            int configuredCount,
            Bounds configuredWorldBounds)
        {
            tileX = configuredTileX;
            tileZ = configuredTileZ;
            startIndex = configuredStartIndex;
            count = configuredCount;
            worldBounds = configuredWorldBounds;
        }
    }
}
