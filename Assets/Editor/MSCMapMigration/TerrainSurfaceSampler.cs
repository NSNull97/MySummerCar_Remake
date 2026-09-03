using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSCMapMigration
{
    internal readonly struct SurfaceTriangle
    {
        public SurfaceTriangle(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            string recordId,
            MapMeshCategory category)
        {
            A = a;
            B = b;
            C = c;
            RecordId = recordId;
            Category = category;
            MinX = Mathf.Min(a.x, Mathf.Min(b.x, c.x));
            MaxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x));
            MinZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z));
            MaxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z));
            Normal = Vector3.Cross(b - a, c - a).normalized;
        }

        public Vector3 A { get; }
        public Vector3 B { get; }
        public Vector3 C { get; }
        public string RecordId { get; }
        public MapMeshCategory Category { get; }
        public float MinX { get; }
        public float MaxX { get; }
        public float MinZ { get; }
        public float MaxZ { get; }
        public Vector3 Normal { get; }

        public bool TrySample(float x, float z, out float y)
        {
            if (TryBarycentricXZ(A, B, C, x, z, out Vector3 barycentric))
            {
                y = barycentric.x * A.y + barycentric.y * B.y + barycentric.z * C.y;
                return true;
            }

            y = 0f;
            return false;
        }

        public static bool TryBarycentricXZ(
            Vector3 a,
            Vector3 b,
            Vector3 c,
            float x,
            float z,
            out Vector3 barycentric)
        {
            float denominator =
                (b.z - c.z) * (a.x - c.x) +
                (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(denominator) < 1e-8f)
            {
                barycentric = default;
                return false;
            }

            float u = ((b.z - c.z) * (x - c.x) +
                       (c.x - b.x) * (z - c.z)) / denominator;
            float v = ((c.z - a.z) * (x - c.x) +
                       (a.x - c.x) * (z - c.z)) / denominator;
            float w = 1f - u - v;
            const float tolerance = 1e-5f;
            barycentric = new Vector3(u, v, w);
            return u >= -tolerance && v >= -tolerance && w >= -tolerance;
        }
    }

    internal sealed class TriangleSpatialIndex
    {
        private readonly IReadOnlyList<SurfaceTriangle> triangles;
        private readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
        private readonly float cellSize;

        public TriangleSpatialIndex(
            IReadOnlyList<SurfaceTriangle> source,
            float cellSizeMeters)
        {
            triangles = source;
            cellSize = Mathf.Max(1f, cellSizeMeters);
            for (int index = 0; index < triangles.Count; index++)
            {
                SurfaceTriangle triangle = triangles[index];
                int minX = Cell(triangle.MinX);
                int maxX = Cell(triangle.MaxX);
                int minZ = Cell(triangle.MinZ);
                int maxZ = Cell(triangle.MaxZ);
                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        long key = Key(x, z);
                        if (!cells.TryGetValue(key, out List<int> indices))
                        {
                            indices = new List<int>();
                            cells.Add(key, indices);
                        }

                        indices.Add(index);
                    }
                }
            }
        }

        public bool TrySampleUpperSurface(
            float x,
            float z,
            out float height,
            out string recordId)
        {
            height = float.NegativeInfinity;
            recordId = string.Empty;
            if (!cells.TryGetValue(Key(Cell(x), Cell(z)), out List<int> indices))
            {
                return false;
            }

            bool found = false;
            foreach (int triangleIndex in indices)
            {
                SurfaceTriangle triangle = triangles[triangleIndex];
                if (x < triangle.MinX || x > triangle.MaxX ||
                    z < triangle.MinZ || z > triangle.MaxZ ||
                    !triangle.TrySample(x, z, out float candidate))
                {
                    continue;
                }

                if (!found || candidate > height)
                {
                    found = true;
                    height = candidate;
                    recordId = triangle.RecordId;
                }
            }

            return found;
        }

        public bool TrySampleLowerSurface(
            float x,
            float z,
            out float height,
            out string recordId)
        {
            height = float.PositiveInfinity;
            recordId = string.Empty;
            if (!cells.TryGetValue(Key(Cell(x), Cell(z)), out List<int> indices))
            {
                return false;
            }

            bool found = false;
            foreach (int triangleIndex in indices)
            {
                SurfaceTriangle triangle = triangles[triangleIndex];
                if (x < triangle.MinX || x > triangle.MaxX ||
                    z < triangle.MinZ || z > triangle.MaxZ ||
                    !triangle.TrySample(x, z, out float candidate))
                {
                    continue;
                }

                if (!found || candidate < height)
                {
                    found = true;
                    height = candidate;
                    recordId = triangle.RecordId;
                }
            }

            return found;
        }

        private int Cell(float coordinate) => Mathf.FloorToInt(coordinate / cellSize);

        private static long Key(int x, int z) =>
            ((long)x << 32) ^ (uint)z;
    }

    internal sealed class TerrainSamplingResult
    {
        public TerrainSamplingResult(
            TerrainGridDomain domain,
            float[] heights,
            bool[] valid,
            bool[] exterior)
        {
            Domain = domain;
            Heights = heights;
            Valid = valid;
            Exterior = exterior;
        }

        public TerrainGridDomain Domain { get; }
        public float[] Heights { get; }
        public bool[] Valid { get; }
        public bool[] Exterior { get; }
        public int TriangleCount { get; set; }
        public int CoveredSampleCount { get; set; }
        public int InteriorFilledCount { get; set; }
        public int ExteriorSampleCount { get; set; }
        public int RoadConstraintCount { get; set; }
        public int RejectedRoadConstraintCount { get; set; }
        public int ElevatedRoadTriangleCount { get; set; }
        public float MeanSmoothingDisplacement { get; set; }
        public float MedianSmoothingDisplacement { get; set; }
        public float P95SmoothingDisplacement { get; set; }
        public float MaximumSmoothingDisplacement { get; set; }
        public int SmoothingLimitHitCount { get; set; }

        public int Index(int x, int z) => z * Domain.SampleCountX + x;
    }

    internal readonly struct TerrainGridDomain
    {
        private TerrainGridDomain(
            Vector3 minimum,
            Vector3 maximum,
            float tileSize,
            int heightmapResolution,
            int tileCountX,
            int tileCountZ)
        {
            Minimum = minimum;
            Maximum = maximum;
            TileSize = tileSize;
            HeightmapResolution = heightmapResolution;
            TileCountX = tileCountX;
            TileCountZ = tileCountZ;
            SampleSpacing = tileSize / (heightmapResolution - 1);
            SampleCountX = tileCountX * (heightmapResolution - 1) + 1;
            SampleCountZ = tileCountZ * (heightmapResolution - 1) + 1;
        }

        public Vector3 Minimum { get; }
        public Vector3 Maximum { get; }
        public float TileSize { get; }
        public int HeightmapResolution { get; }
        public int TileCountX { get; }
        public int TileCountZ { get; }
        public float SampleSpacing { get; }
        public int SampleCountX { get; }
        public int SampleCountZ { get; }
        public long TotalSamples => (long)SampleCountX * SampleCountZ;

        public static TerrainGridDomain Create(
            IReadOnlyList<SurfaceTriangle> triangles,
            IMapMigrationSettings settings)
        {
            if (triangles.Count == 0)
            {
                throw new InvalidOperationException("No ground triangles were available.");
            }

            float minX = triangles.Min(triangle => triangle.MinX);
            float maxX = triangles.Max(triangle => triangle.MaxX);
            float minZ = triangles.Min(triangle => triangle.MinZ);
            float maxZ = triangles.Max(triangle => triangle.MaxZ);
            float minY = triangles.Min(triangle => Mathf.Min(
                triangle.A.y, Mathf.Min(triangle.B.y, triangle.C.y))) - 16f;
            float maxY = triangles.Max(triangle => Mathf.Max(
                triangle.A.y, Mathf.Max(triangle.B.y, triangle.C.y))) + 16f;
            float tileSize = settings.TerrainTileSize;
            float domainMinX = Mathf.Floor(minX / tileSize) * tileSize;
            float domainMinZ = Mathf.Floor(minZ / tileSize) * tileSize;
            int tilesX = Mathf.Max(1, Mathf.CeilToInt((maxX - domainMinX) / tileSize));
            int tilesZ = Mathf.Max(1, Mathf.CeilToInt((maxZ - domainMinZ) / tileSize));
            int resolution = SelectResolution(
                tileSize,
                settings.TargetHeightSampleSpacing,
                tilesX,
                tilesZ,
                settings.MaximumTotalHeightSamples);
            return new TerrainGridDomain(
                new Vector3(domainMinX, minY, domainMinZ),
                new Vector3(domainMinX + tilesX * tileSize, maxY,
                    domainMinZ + tilesZ * tileSize),
                tileSize,
                resolution,
                tilesX,
                tilesZ);
        }

        internal static int SelectResolution(
            float tileSize,
            float targetSpacing,
            int tilesX,
            int tilesZ,
            int maximumSamples)
        {
            int segmentsTarget = Mathf.CeilToInt(tileSize / targetSpacing);
            int power = 5;
            while ((1 << power) < segmentsTarget && power < 12)
            {
                power++;
            }

            int resolution = (1 << power) + 1;
            while (resolution > 33)
            {
                long sampleX = (long)tilesX * (resolution - 1) + 1;
                long sampleZ = (long)tilesZ * (resolution - 1) + 1;
                if (sampleX * sampleZ <= maximumSamples)
                {
                    return resolution;
                }

                resolution = ((resolution - 1) >> 1) + 1;
            }

            long minimum = ((long)tilesX * 32 + 1) * ((long)tilesZ * 32 + 1);
            if (minimum > maximumSamples)
            {
                throw new InvalidOperationException(
                    $"Terrain grid needs at least {minimum} samples, exceeding configured maximum {maximumSamples}.");
            }

            return 33;
        }
    }

    internal static class TerrainSurfaceSampler
    {
        public static List<SurfaceTriangle> CollectTriangles(
            IEnumerable<ScannedMeshInstance> instances,
            Func<MapMeshCategory, bool> categoryFilter,
            float minimumAbsoluteNormalY)
        {
            var triangles = new List<SurfaceTriangle>();
            foreach (ScannedMeshInstance instance in instances)
            {
                if (!categoryFilter(instance.Record.category))
                {
                    continue;
                }

                ReadableMeshData data = MapMeshDataReader.Read(instance.Mesh);
                foreach (int[] indices in data.TriangleSubMeshes)
                {
                    for (int index = 0; index + 2 < indices.Length; index += 3)
                    {
                        Vector3 a = instance.LocalToWorld.MultiplyPoint3x4(
                            data.Vertices[indices[index]]);
                        Vector3 b = instance.LocalToWorld.MultiplyPoint3x4(
                            data.Vertices[indices[index + 1]]);
                        Vector3 c = instance.LocalToWorld.MultiplyPoint3x4(
                            data.Vertices[indices[index + 2]]);
                        var triangle = new SurfaceTriangle(
                            a, b, c, instance.Record.recordId, instance.Record.category);
                        if (Mathf.Abs(triangle.Normal.y) < minimumAbsoluteNormalY ||
                            (triangle.MaxX - triangle.MinX) *
                            (triangle.MaxZ - triangle.MinZ) < 1e-6f)
                        {
                            continue;
                        }

                        triangles.Add(triangle);
                    }
                }
            }

            return triangles;
        }

        public static TerrainSamplingResult Sample(
            IReadOnlyList<SurfaceTriangle> triangles,
            TerrainGridDomain domain)
        {
            int count = checked(domain.SampleCountX * domain.SampleCountZ);
            var heights = new float[count];
            var valid = new bool[count];
            var exterior = new bool[count];
            var index = new TriangleSpatialIndex(
                triangles,
                Mathf.Max(32f, domain.SampleSpacing * 8f));
            int covered = 0;
            for (int z = 0; z < domain.SampleCountZ; z++)
            {
                if ((z & 31) == 0)
                {
                    MapMigrationProgress.Check(
                        "Build Terrain Preview",
                        $"Barycentric height sampling row {z}/{domain.SampleCountZ}",
                        z / (float)Math.Max(1, domain.SampleCountZ));
                }

                float worldZ = domain.Minimum.z + z * domain.SampleSpacing;
                int row = z * domain.SampleCountX;
                for (int x = 0; x < domain.SampleCountX; x++)
                {
                    float worldX = domain.Minimum.x + x * domain.SampleSpacing;
                    if (index.TrySampleUpperSurface(worldX, worldZ, out float height, out _))
                    {
                        heights[row + x] = height;
                        valid[row + x] = true;
                        covered++;
                    }
                }
            }

            var result = new TerrainSamplingResult(domain, heights, valid, exterior)
            {
                TriangleCount = triangles.Count,
                CoveredSampleCount = covered
            };
            FillInteriorHoles(result, maximumIterations: 8);
            return result;
        }

        private static void FillInteriorHoles(
            TerrainSamplingResult result,
            int maximumIterations)
        {
            TerrainGridDomain domain = result.Domain;
            var queue = new Queue<int>();
            for (int x = 0; x < domain.SampleCountX; x++)
            {
                EnqueueExterior(result, x, 0, queue);
                EnqueueExterior(result, x, domain.SampleCountZ - 1, queue);
            }

            for (int z = 1; z < domain.SampleCountZ - 1; z++)
            {
                EnqueueExterior(result, 0, z, queue);
                EnqueueExterior(result, domain.SampleCountX - 1, z, queue);
            }

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                int x = current % domain.SampleCountX;
                int z = current / domain.SampleCountX;
                TryFlood(result, x - 1, z, queue);
                TryFlood(result, x + 1, z, queue);
                TryFlood(result, x, z - 1, queue);
                TryFlood(result, x, z + 1, queue);
            }

            int exteriorCount = 0;
            foreach (bool exterior in result.Exterior)
            {
                if (exterior)
                {
                    exteriorCount++;
                }
            }

            result.ExteriorSampleCount = exteriorCount;
            int filled = 0;
            var pending = new List<(int index, float height)>();
            for (int iteration = 0; iteration < maximumIterations; iteration++)
            {
                pending.Clear();
                for (int z = 1; z < domain.SampleCountZ - 1; z++)
                {
                    for (int x = 1; x < domain.SampleCountX - 1; x++)
                    {
                        int sample = result.Index(x, z);
                        if (result.Valid[sample] || result.Exterior[sample])
                        {
                            continue;
                        }

                        float sum = 0f;
                        int neighbors = 0;
                        AddNeighbor(result, x - 1, z, ref sum, ref neighbors);
                        AddNeighbor(result, x + 1, z, ref sum, ref neighbors);
                        AddNeighbor(result, x, z - 1, ref sum, ref neighbors);
                        AddNeighbor(result, x, z + 1, ref sum, ref neighbors);
                        if (neighbors >= 2)
                        {
                            pending.Add((sample, sum / neighbors));
                        }
                    }
                }

                if (pending.Count == 0)
                {
                    break;
                }

                foreach ((int sample, float height) in pending)
                {
                    result.Heights[sample] = height;
                    result.Valid[sample] = true;
                    filled++;
                }
            }

            result.InteriorFilledCount = filled;
        }

        private static void EnqueueExterior(
            TerrainSamplingResult result,
            int x,
            int z,
            Queue<int> queue)
        {
            int index = result.Index(x, z);
            if (result.Valid[index] || result.Exterior[index])
            {
                return;
            }

            result.Exterior[index] = true;
            queue.Enqueue(index);
        }

        private static void TryFlood(
            TerrainSamplingResult result,
            int x,
            int z,
            Queue<int> queue)
        {
            if (x < 0 || z < 0 ||
                x >= result.Domain.SampleCountX ||
                z >= result.Domain.SampleCountZ)
            {
                return;
            }

            EnqueueExterior(result, x, z, queue);
        }

        private static void AddNeighbor(
            TerrainSamplingResult result,
            int x,
            int z,
            ref float sum,
            ref int count)
        {
            int index = result.Index(x, z);
            if (!result.Valid[index])
            {
                return;
            }

            sum += result.Heights[index];
            count++;
        }
    }
}
