using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.World.Partition
{
    [Serializable]
    public sealed class WorldPartitionConfig
    {
        [SerializeField] private float cellSizeMeters = 512f;
        [SerializeField] private Vector3 worldOrigin;
        [SerializeField] private Bounds worldBounds;
        [SerializeField] private int loadingRadiusCells = 1;
        [SerializeField] private int unloadingRadiusCells = 2;
        [SerializeField] private float largeObjectThresholdRatio = 0.8f;
        [SerializeField] private string largeObjectPolicy = "GlobalReference";
        [SerializeField] private string crossCellParentPolicy = "CentroidCellWithParentMetadata";
        [SerializeField] private string crossCellColliderPolicy = "FollowSourceEntity";

        public float CellSizeMeters => cellSizeMeters;
        public Vector3 WorldOrigin => worldOrigin;
        public Bounds WorldBounds => worldBounds;
        public int LoadingRadiusCells => loadingRadiusCells;
        public int UnloadingRadiusCells => unloadingRadiusCells;
        public float LargeObjectThresholdRatio => largeObjectThresholdRatio;
        public string LargeObjectPolicy => largeObjectPolicy;
        public string CrossCellParentPolicy => crossCellParentPolicy;
        public string CrossCellColliderPolicy => crossCellColliderPolicy;
    }

    [Serializable]
    public readonly struct WorldCellIndex : IEquatable<WorldCellIndex>
    {
        public WorldCellIndex(int x, int z) { X = x; Z = z; }
        public int X { get; }
        public int Z { get; }
        public string Id => $"cell_{X}_{Z}";
        public bool Equals(WorldCellIndex other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is WorldCellIndex other && Equals(other);
        public override int GetHashCode() => (X * 397) ^ Z;
        public override string ToString() => Id;
    }

    [Serializable]
    public readonly struct WorldCellBounds
    {
        public WorldCellBounds(WorldCellIndex index, Bounds bounds) { Index = index; Bounds = bounds; }
        public WorldCellIndex Index { get; }
        public Bounds Bounds { get; }
    }

    public static class WorldCellMembershipUtility
    {
        public static WorldCellIndex FromPosition(Vector3 position, float cellSizeMeters)
        {
            ValidateCellSize(cellSizeMeters);
            return new WorldCellIndex(
                Mathf.FloorToInt(position.x / cellSizeMeters),
                Mathf.FloorToInt(position.z / cellSizeMeters));
        }

        public static string Assign(Bounds bounds, string category, float cellSizeMeters, float largeObjectThresholdRatio = 0.8f)
        {
            ValidateCellSize(cellSizeMeters);
            if (string.Equals(category, "Terrain", StringComparison.Ordinal) ||
                bounds.size.x > cellSizeMeters * largeObjectThresholdRatio ||
                bounds.size.z > cellSizeMeters * largeObjectThresholdRatio)
            {
                return "global";
            }

            return FromPosition(bounds.center, cellSizeMeters).Id;
        }

        public static WorldCellBounds GetBounds(WorldCellIndex index, float cellSizeMeters, float minY, float maxY)
        {
            ValidateCellSize(cellSizeMeters);
            Vector3 min = new Vector3(index.X * cellSizeMeters, minY, index.Z * cellSizeMeters);
            Vector3 max = new Vector3((index.X + 1) * cellSizeMeters, maxY, (index.Z + 1) * cellSizeMeters);
            var bounds = new Bounds((min + max) * 0.5f, max - min);
            return new WorldCellBounds(index, bounds);
        }

        public static IReadOnlyList<WorldCellIndex> EnumerateRadius(WorldCellIndex center, int radius)
        {
            if (radius < 0) throw new ArgumentOutOfRangeException(nameof(radius));
            var result = new List<WorldCellIndex>((radius * 2 + 1) * (radius * 2 + 1));
            for (int x = center.X - radius; x <= center.X + radius; x++)
            for (int z = center.Z - radius; z <= center.Z + radius; z++)
                result.Add(new WorldCellIndex(x, z));
            return result;
        }

        private static void ValidateCellSize(float cellSizeMeters)
        {
            if (!float.IsFinite(cellSizeMeters) || cellSizeMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(cellSizeMeters));
        }
    }

    [Serializable]
    public sealed class WorldSceneGenerationSettings
    {
        [SerializeField] private string generatorVersion = "1.0.0";
        [SerializeField] private string generatedSceneRoot = "Assets/Game/LegacyImport/ReferenceOnly/World/Generated";
        [SerializeField] private bool useBoundsProxies = true;
        [SerializeField] private bool generateColliderDebugProxies = true;
        [SerializeField] private bool includeInactiveEntities = true;

        public string GeneratorVersion => generatorVersion;
        public string GeneratedSceneRoot => generatedSceneRoot;
        public bool UseBoundsProxies => useBoundsProxies;
        public bool GenerateColliderDebugProxies => generateColliderDebugProxies;
        public bool IncludeInactiveEntities => includeInactiveEntities;
    }
}
