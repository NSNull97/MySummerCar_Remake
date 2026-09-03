using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace MSC.World.Vegetation
{
    public enum VegetationDensityChannel
    {
        ShortGrass = 0,
        MeadowGrass = 1,
        TallGrass = 2,
        Decorative = 3
    }

    public enum VegetationBrushOperation
    {
        Paint = 0,
        Erase = 1,
        Smooth = 2,
        Noise = 3
    }

    public enum VegetationBlockerKind
    {
        GroundRoad = 0,
        BridgeDeck = 1,
        BridgePillar = 2,
        Building = 3,
        Water = 4,
        Foundation = 5,
        Other = 6
    }

    public enum VegetationExclusionShape
    {
        Box = 0,
        Sphere = 1
    }

    public enum VegetationRejectionReason
    {
        None = 0,
        NoDensity = 1,
        NoSurface = 2,
        Blocked = 3,
        ExcessiveSlope = 4,
        Water = 5,
        OutsideHeightRange = 6,
        ExclusionVolume = 7,
        OutsideCell = 8
    }

    [Flags]
    public enum VegetationDebugMode
    {
        None = 0,
        DensityMaskOverlay = 1 << 0,
        BlockerMaskOverlay = 1 << 1,
        DirtyTileBounds = 1 << 2,
        CandidatePositions = 1 << 3,
        RejectedSamples = 1 << 4,
        InstanceCountPerTile = 1 << 5,
        LodAndCullingBounds = 1 << 6
    }

    /// <summary>
    /// Compact GPU instance record. The layout is mirrored in
    /// MSC_VegetationIndirectHDRP.shader and must remain exactly 24 bytes.
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct VegetationInstanceRecord
    {
        public const int Stride = 24;

        [SerializeField] private Vector3 worldPosition;
        [SerializeField] private uint packedNormal;
        [SerializeField] private uint packedYawScale;
        [SerializeField] private uint packedVariation;

        public VegetationInstanceRecord(
            Vector3 position,
            Vector3 surfaceNormal,
            float yawDegrees,
            float uniformScale,
            byte profileIndex,
            byte colorVariation,
            ushort windPhase)
        {
            worldPosition = position;
            packedNormal = VegetationPacking.PackNormal(surfaceNormal);
            packedYawScale = VegetationPacking.PackYawScale(yawDegrees, uniformScale);
            packedVariation = VegetationPacking.PackVariation(profileIndex, colorVariation, windPhase);
        }

        public Vector3 WorldPosition => worldPosition;
        public Vector3 SurfaceNormal => VegetationPacking.UnpackNormal(packedNormal);
        public float YawDegrees => VegetationPacking.UnpackYaw(packedYawScale);
        public float UniformScale => VegetationPacking.UnpackScale(packedYawScale);
        public byte ProfileIndex => (byte)(packedVariation & 0xffu);
        public byte ColorVariation => (byte)((packedVariation >> 8) & 0xffu);
        public ushort WindPhase => (ushort)((packedVariation >> 16) & 0xffffu);
    }

    [Serializable]
    public sealed class VegetationProfileTileInstances
    {
        [SerializeField] private int profileIndex;
        [SerializeField] private VegetationInstanceRecord[] instances =
            Array.Empty<VegetationInstanceRecord>();

        public int ProfileIndex => profileIndex;
        public IReadOnlyList<VegetationInstanceRecord> Instances =>
            instances ?? Array.Empty<VegetationInstanceRecord>();
        // GraphicsBuffer.SetData consumes this immutable authored array directly.
        // Do not allocate another full tile copy just to cross the public read-only API.
        internal VegetationInstanceRecord[] InstanceData =>
            instances ?? Array.Empty<VegetationInstanceRecord>();
        public int Count => instances?.Length ?? 0;

        public VegetationProfileTileInstances(
            int configuredProfileIndex,
            VegetationInstanceRecord[] configuredInstances)
        {
            profileIndex = configuredProfileIndex;
            instances = configuredInstances != null
                ? (VegetationInstanceRecord[])configuredInstances.Clone()
                : Array.Empty<VegetationInstanceRecord>();
        }
    }

    [Serializable]
    public struct VegetationRejectedSample
    {
        [SerializeField] private Vector3 worldPosition;
        [SerializeField] private VegetationRejectionReason reason;

        public VegetationRejectedSample(
            Vector3 position,
            VegetationRejectionReason rejectionReason)
        {
            worldPosition = position;
            reason = rejectionReason;
        }

        public Vector3 WorldPosition => worldPosition;
        public VegetationRejectionReason Reason => reason;
    }

    [Serializable]
    public sealed class VegetationTileRecord
    {
        [SerializeField] private int tileX;
        [SerializeField] private int tileZ;
        [SerializeField] private Bounds worldBounds;
        [SerializeField] private VegetationProfileTileInstances[] profileInstances =
            Array.Empty<VegetationProfileTileInstances>();
        [SerializeField] private Vector3[] candidateDebugPositions =
            Array.Empty<Vector3>();
        [SerializeField] private VegetationRejectedSample[] rejectedDebugSamples =
            Array.Empty<VegetationRejectedSample>();

        public int TileX => tileX;
        public int TileZ => tileZ;
        public Bounds WorldBounds => worldBounds;
        public IReadOnlyList<VegetationProfileTileInstances> ProfileInstances =>
            profileInstances ?? Array.Empty<VegetationProfileTileInstances>();
        public IReadOnlyList<Vector3> CandidateDebugPositions =>
            candidateDebugPositions ?? Array.Empty<Vector3>();
        public IReadOnlyList<VegetationRejectedSample> RejectedDebugSamples =>
            rejectedDebugSamples ?? Array.Empty<VegetationRejectedSample>();

        public int TotalInstanceCount
        {
            get
            {
                int count = 0;
                VegetationProfileTileInstances[] configured =
                    profileInstances ?? Array.Empty<VegetationProfileTileInstances>();
                for (int index = 0; index < configured.Length; index++)
                {
                    count += configured[index]?.Count ?? 0;
                }

                return count;
            }
        }

        public VegetationTileRecord(
            int configuredTileX,
            int configuredTileZ,
            Bounds configuredBounds,
            VegetationProfileTileInstances[] configuredProfileInstances,
            Vector3[] configuredCandidateDebugPositions,
            VegetationRejectedSample[] configuredRejectedDebugSamples)
        {
            tileX = configuredTileX;
            tileZ = configuredTileZ;
            worldBounds = configuredBounds;
            profileInstances = configuredProfileInstances != null
                ? (VegetationProfileTileInstances[])configuredProfileInstances.Clone()
                : Array.Empty<VegetationProfileTileInstances>();
            candidateDebugPositions = configuredCandidateDebugPositions != null
                ? (Vector3[])configuredCandidateDebugPositions.Clone()
                : Array.Empty<Vector3>();
            rejectedDebugSamples = configuredRejectedDebugSamples != null
                ? (VegetationRejectedSample[])configuredRejectedDebugSamples.Clone()
                : Array.Empty<VegetationRejectedSample>();
        }
    }

    public static class VegetationStableHash
    {
        public static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7feb352du;
            value ^= value >> 15;
            value *= 0x846ca68bu;
            value ^= value >> 16;
            return value;
        }

        public static uint Hash(int x, int z, int seed)
        {
            unchecked
            {
                uint value = (uint)x * 0x9e3779b9u;
                value ^= (uint)z * 0x85ebca6bu;
                value ^= (uint)seed * 0xc2b2ae35u;
                return Hash(value);
            }
        }

        public static float ToUnitFloat(uint hash)
        {
            return (hash & 0x00ffffffu) / 16777215f;
        }

        public static Vector2 JitteredGridOffset(int gridX, int gridZ, int seed)
        {
            uint first = Hash(gridX, gridZ, seed);
            uint second = Hash(gridX, gridZ, seed ^ unchecked((int)0x68bc21ebu));
            return new Vector2(ToUnitFloat(first), ToUnitFloat(second));
        }
    }

    public static class VegetationPacking
    {
        private const float MinimumPackedScale = 0.01f;
        private const float MaximumPackedScale = 8f;

        public static uint PackNormal(Vector3 normal)
        {
            Vector3 normalized = normal.sqrMagnitude > 0.000001f
                ? normal.normalized
                : Vector3.up;
            float denominator =
                Mathf.Abs(normalized.x) +
                Mathf.Abs(normalized.y) +
                Mathf.Abs(normalized.z);
            Vector2 oct = new Vector2(normalized.x, normalized.z) /
                          Mathf.Max(denominator, 0.000001f);
            if (normalized.y < 0f)
            {
                oct = new Vector2(
                    (1f - Mathf.Abs(oct.y)) * Mathf.Sign(oct.x),
                    (1f - Mathf.Abs(oct.x)) * Mathf.Sign(oct.y));
            }

            ushort x = PackSignedUnit(oct.x);
            ushort y = PackSignedUnit(oct.y);
            return x | ((uint)y << 16);
        }

        public static Vector3 UnpackNormal(uint packed)
        {
            float x = UnpackSignedUnit((ushort)(packed & 0xffffu));
            float z = UnpackSignedUnit((ushort)(packed >> 16));
            Vector3 normal = new Vector3(x, 1f - Mathf.Abs(x) - Mathf.Abs(z), z);
            if (normal.y < 0f)
            {
                float oldX = normal.x;
                normal.x = (1f - Mathf.Abs(normal.z)) * Mathf.Sign(oldX);
                normal.z = (1f - Mathf.Abs(oldX)) * Mathf.Sign(normal.z);
            }

            return normal.normalized;
        }

        public static uint PackYawScale(float yawDegrees, float uniformScale)
        {
            float wrappedYaw = Mathf.Repeat(yawDegrees, 360f);
            ushort yaw = (ushort)Mathf.RoundToInt(wrappedYaw / 360f * ushort.MaxValue);
            float scale01 = Mathf.InverseLerp(
                MinimumPackedScale,
                MaximumPackedScale,
                Mathf.Clamp(uniformScale, MinimumPackedScale, MaximumPackedScale));
            ushort scale = (ushort)Mathf.RoundToInt(scale01 * ushort.MaxValue);
            return yaw | ((uint)scale << 16);
        }

        public static float UnpackYaw(uint packed)
        {
            return (packed & 0xffffu) / (float)ushort.MaxValue * 360f;
        }

        public static float UnpackScale(uint packed)
        {
            float scale01 = (packed >> 16) / (float)ushort.MaxValue;
            return Mathf.Lerp(MinimumPackedScale, MaximumPackedScale, scale01);
        }

        public static uint PackVariation(
            byte profileIndex,
            byte colorVariation,
            ushort windPhase)
        {
            return profileIndex |
                   ((uint)colorVariation << 8) |
                   ((uint)windPhase << 16);
        }

        private static ushort PackSignedUnit(float value)
        {
            return (ushort)Mathf.RoundToInt(
                (Mathf.Clamp(value, -1f, 1f) * 0.5f + 0.5f) *
                ushort.MaxValue);
        }

        private static float UnpackSignedUnit(ushort value)
        {
            return value / (float)ushort.MaxValue * 2f - 1f;
        }
    }
}
