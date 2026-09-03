using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSCMapMigration
{
    internal static class RoadConstraintBuilder
    {
        private const float ElevatedRoadSeparation = 1.5f;

        private readonly struct Constraint
        {
            public Constraint(float distance, float roadHeight)
            {
                Distance = distance;
                RoadHeight = roadHeight;
            }

            public float Distance { get; }
            public float RoadHeight { get; }
        }

        public static void Apply(
            TerrainSamplingResult result,
            IReadOnlyList<SurfaceTriangle> roadTriangles,
            IMapMigrationSettings settings)
        {
            var constraints = new Dictionary<int, Constraint>();
            TerrainGridDomain domain = result.Domain;
            float blendDistance = settings.RoadShoulderBlendDistance;
            int triangleIndex = 0;
            foreach (SurfaceTriangle triangle in roadTriangles)
            {
                triangleIndex++;
                if ((triangleIndex & 127) == 0)
                {
                    MapMigrationProgress.Check(
                        "Build Terrain Preview",
                        $"Road polygon constraints {triangleIndex}/{roadTriangles.Count}",
                        triangleIndex / (float)Math.Max(1, roadTriangles.Count));
                }

                int minX = Mathf.Clamp(Mathf.FloorToInt(
                    (triangle.MinX - blendDistance - domain.Minimum.x) /
                    domain.SampleSpacing), 0, domain.SampleCountX - 1);
                int maxX = Mathf.Clamp(Mathf.CeilToInt(
                    (triangle.MaxX + blendDistance - domain.Minimum.x) /
                    domain.SampleSpacing), 0, domain.SampleCountX - 1);
                int minZ = Mathf.Clamp(Mathf.FloorToInt(
                    (triangle.MinZ - blendDistance - domain.Minimum.z) /
                    domain.SampleSpacing), 0, domain.SampleCountZ - 1);
                int maxZ = Mathf.Clamp(Mathf.CeilToInt(
                    (triangle.MaxZ + blendDistance - domain.Minimum.z) /
                    domain.SampleSpacing), 0, domain.SampleCountZ - 1);

                for (int z = minZ; z <= maxZ; z++)
                {
                    float worldZ = domain.Minimum.z + z * domain.SampleSpacing;
                    for (int x = minX; x <= maxX; x++)
                    {
                        int sample = result.Index(x, z);
                        if (!result.Valid[sample])
                        {
                            continue;
                        }

                        float worldX = domain.Minimum.x + x * domain.SampleSpacing;
                        float distance;
                        float roadHeight;
                        if (triangle.TrySample(worldX, worldZ, out roadHeight))
                        {
                            distance = 0f;
                        }
                        else if (!TryNearestEdge(
                                     triangle,
                                     worldX,
                                     worldZ,
                                     blendDistance,
                                     out distance,
                                     out roadHeight))
                        {
                            continue;
                        }

                        if (!constraints.TryGetValue(sample, out Constraint current) ||
                            ShouldReplaceConstraint(
                                distance,
                                roadHeight,
                                current.Distance,
                                current.RoadHeight))
                        {
                            constraints[sample] = new Constraint(distance, roadHeight);
                        }
                    }
                }
            }

            int applied = 0;
            foreach (KeyValuePair<int, Constraint> pair in constraints)
            {
                float ground = result.Heights[pair.Key];
                float target = pair.Value.RoadHeight - settings.RoadClearance;
                float weight = ConstraintWeight(
                    pair.Value.Distance,
                    blendDistance,
                    domain.SampleSpacing);
                result.Heights[pair.Key] = Mathf.Lerp(ground, target, weight);
                applied++;
            }

            result.RoadConstraintCount = applied;
            result.RejectedRoadConstraintCount = 0;
            result.RoadConstraintCount += EnforceContinuousClearance(
                result,
                roadTriangles,
                settings.RoadClearance);
        }

        internal static List<SurfaceTriangle> FilterRoadBedTriangles(
            IReadOnlyList<SurfaceTriangle> triangles,
            out int elevatedCount)
        {
            var result = new List<SurfaceTriangle>(triangles.Count);
            var index = new TriangleSpatialIndex(triangles, 32f);
            elevatedCount = 0;
            foreach (SurfaceTriangle triangle in triangles)
            {
                Vector3 point = (triangle.A + triangle.B + triangle.C) / 3f;
                if (index.TrySampleLowerSurface(
                        point.x,
                        point.z,
                        out float lower,
                        out _) &&
                    point.y - lower > ElevatedRoadSeparation)
                {
                    elevatedCount++;
                    continue;
                }

                result.Add(triangle);
            }

            return result;
        }

        internal static float ApplyClearance(float roadHeight, float clearance) =>
            roadHeight - Mathf.Max(0f, clearance);

        internal static float ConstraintWeight(
            float distance,
            float blendDistance,
            float sampleSpacing)
        {
            if (distance <= 0f || blendDistance <= 0f)
            {
                return 1f;
            }

            float hardBand = Mathf.Min(
                blendDistance,
                Mathf.Max(0f, sampleSpacing) * 1.41421356f);
            if (distance <= hardBand)
            {
                return 1f;
            }

            float remainingBlend = blendDistance - hardBand;
            return remainingBlend <= 1e-5f
                ? 0f
                : 1f - SmoothStep01((distance - hardBand) / remainingBlend);
        }

        internal static bool ShouldReplaceConstraint(
            float candidateDistance,
            float candidateHeight,
            float currentDistance,
            float currentHeight) =>
            candidateDistance < currentDistance ||
            Mathf.Approximately(candidateDistance, currentDistance) &&
            candidateHeight < currentHeight;

        internal static bool TrySampleHeightfield(
            TerrainSamplingResult result,
            float worldX,
            float worldZ,
            out float height)
        {
            TerrainGridDomain domain = result.Domain;
            float gridX = (worldX - domain.Minimum.x) / domain.SampleSpacing;
            float gridZ = (worldZ - domain.Minimum.z) / domain.SampleSpacing;
            int x = Mathf.FloorToInt(gridX);
            int z = Mathf.FloorToInt(gridZ);
            if (x < 0 || z < 0 ||
                x >= domain.SampleCountX - 1 ||
                z >= domain.SampleCountZ - 1)
            {
                height = 0f;
                return false;
            }

            float tx = Mathf.Clamp01(gridX - x);
            float tz = Mathf.Clamp01(gridZ - z);
            float h00 = TerrainHeight(result, x, z);
            float h10 = TerrainHeight(result, x + 1, z);
            float h01 = TerrainHeight(result, x, z + 1);
            float h11 = TerrainHeight(result, x + 1, z + 1);
            height = Mathf.Lerp(
                Mathf.Lerp(h00, h10, tx),
                Mathf.Lerp(h01, h11, tx),
                tz);
            return true;
        }

        private static int EnforceContinuousClearance(
            TerrainSamplingResult result,
            IReadOnlyList<SurfaceTriangle> roadTriangles,
            float clearance)
        {
            const float quantizationSafety = 0.01f;
            int adjusted = 0;
            foreach (SurfaceTriangle triangle in roadTriangles)
            {
                Vector3 point = (triangle.A + triangle.B + triangle.C) / 3f;
                float allowed = point.y - Mathf.Max(0f, clearance) - quantizationSafety;
                TerrainGridDomain domain = result.Domain;
                float gridX = (point.x - domain.Minimum.x) / domain.SampleSpacing;
                float gridZ = (point.z - domain.Minimum.z) / domain.SampleSpacing;
                int x = Mathf.FloorToInt(gridX);
                int z = Mathf.FloorToInt(gridZ);
                if (x < 0 || z < 0 ||
                    x >= domain.SampleCountX - 1 ||
                    z >= domain.SampleCountZ - 1)
                {
                    continue;
                }

                int[] indices =
                {
                    result.Index(x, z),
                    result.Index(x + 1, z),
                    result.Index(x, z + 1),
                    result.Index(x + 1, z + 1)
                };
                bool changed = false;
                for (int corner = 0; corner < indices.Length; corner++)
                {
                    int index = indices[corner];
                    if (result.Valid[index] && result.Heights[index] > allowed)
                    {
                        result.Heights[index] = Mathf.Max(
                            domain.Minimum.y,
                            allowed);
                        changed = true;
                    }
                }

                if (changed)
                {
                    adjusted++;
                }
            }

            return adjusted;
        }

        private static float TerrainHeight(TerrainSamplingResult result, int x, int z)
        {
            int index = result.Index(x, z);
            return result.Valid[index]
                ? Mathf.Max(result.Domain.Minimum.y, result.Heights[index])
                : result.Domain.Minimum.y;
        }

        private static bool TryNearestEdge(
            SurfaceTriangle triangle,
            float x,
            float z,
            float maximumDistance,
            out float distance,
            out float height)
        {
            float ab = DistanceToSegmentXZ(triangle.A, triangle.B, x, z, out float abT);
            float bc = DistanceToSegmentXZ(triangle.B, triangle.C, x, z, out float bcT);
            float ca = DistanceToSegmentXZ(triangle.C, triangle.A, x, z, out float caT);
            distance = ab;
            height = Mathf.Lerp(triangle.A.y, triangle.B.y, abT);
            if (bc < distance)
            {
                distance = bc;
                height = Mathf.Lerp(triangle.B.y, triangle.C.y, bcT);
            }

            if (ca < distance)
            {
                distance = ca;
                height = Mathf.Lerp(triangle.C.y, triangle.A.y, caT);
            }

            return distance <= maximumDistance;
        }

        private static float DistanceToSegmentXZ(
            Vector3 a,
            Vector3 b,
            float x,
            float z,
            out float t)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;
            float denominator = dx * dx + dz * dz;
            t = denominator > 1e-8f
                ? Mathf.Clamp01(((x - a.x) * dx + (z - a.z) * dz) / denominator)
                : 0f;
            float nearestX = a.x + dx * t;
            float nearestZ = a.z + dz * t;
            float offsetX = x - nearestX;
            float offsetZ = z - nearestZ;
            return Mathf.Sqrt(offsetX * offsetX + offsetZ * offsetZ);
        }

        private static float SmoothStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
