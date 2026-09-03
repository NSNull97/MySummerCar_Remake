using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSCMapMigration
{
    internal static class TerrainHeightSmoother
    {
        public static void Smooth(
            TerrainSamplingResult result,
            IMapMigrationSettings settings)
        {
            if (settings.SmoothingIterations <= 0 ||
                settings.SmoothingStrength <= 0f)
            {
                return;
            }

            float[] original = (float[])result.Heights.Clone();
            float[] current = result.Heights;
            var next = new float[current.Length];
            var neighbors = new float[8];
            TerrainGridDomain domain = result.Domain;
            for (int iteration = 0; iteration < settings.SmoothingIterations; iteration++)
            {
                Array.Copy(current, next, current.Length);
                for (int z = 1; z < domain.SampleCountZ - 1; z++)
                {
                    if ((z & 63) == 0)
                    {
                        MapMigrationProgress.Check(
                            "Build Terrain Preview",
                            $"Edge-aware smoothing {iteration + 1}/{settings.SmoothingIterations}, row {z}",
                            (iteration + z / (float)domain.SampleCountZ) /
                            settings.SmoothingIterations);
                    }

                    for (int x = 1; x < domain.SampleCountX - 1; x++)
                    {
                        int index = result.Index(x, z);
                        if (!result.Valid[index])
                        {
                            continue;
                        }

                        float center = current[index];
                        int count = 0;
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            for (int dx = -1; dx <= 1; dx++)
                            {
                                if (dx == 0 && dz == 0)
                                {
                                    continue;
                                }

                                int neighbor = result.Index(x + dx, z + dz);
                                if (result.Valid[neighbor])
                                {
                                    neighbors[count++] = current[neighbor];
                                }
                            }
                        }

                        if (count < 4)
                        {
                            continue;
                        }

                        float median = Median(neighbors, count);
                        float localRange = 0f;
                        for (int neighbor = 0; neighbor < count; neighbor++)
                        {
                            localRange = Mathf.Max(
                                localRange,
                                Mathf.Abs(neighbors[neighbor] - center));
                        }

                        float slopeProtection = 1f / (1f + localRange * 1.5f);
                        float blend = settings.SmoothingStrength * slopeProtection;
                        float candidate = Mathf.Lerp(center, median, blend);
                        next[index] = Mathf.Clamp(
                            candidate,
                            original[index] - settings.MaximumAllowedSmoothingDisplacement,
                            original[index] + settings.MaximumAllowedSmoothingDisplacement);
                    }
                }

                (current, next) = (next, current);
            }

            if (!ReferenceEquals(current, result.Heights))
            {
                Array.Copy(current, result.Heights, current.Length);
            }

            var displacement = new List<float>();
            int limitHits = 0;
            float sum = 0f;
            float maximum = 0f;
            for (int index = 0; index < result.Heights.Length; index++)
            {
                if (!result.Valid[index])
                {
                    continue;
                }

                float value = Mathf.Abs(result.Heights[index] - original[index]);
                displacement.Add(value);
                sum += value;
                maximum = Mathf.Max(maximum, value);
                if (settings.MaximumAllowedSmoothingDisplacement > 0f &&
                    value >= settings.MaximumAllowedSmoothingDisplacement - 1e-5f)
                {
                    limitHits++;
                }
            }

            displacement.Sort();
            result.MeanSmoothingDisplacement = displacement.Count > 0
                ? sum / displacement.Count
                : 0f;
            result.MedianSmoothingDisplacement = Percentile(displacement, 0.5f);
            result.P95SmoothingDisplacement = Percentile(displacement, 0.95f);
            result.MaximumSmoothingDisplacement = maximum;
            result.SmoothingLimitHitCount = limitHits;
        }

        private static float Median(float[] values, int count)
        {
            for (int i = 1; i < count; i++)
            {
                float value = values[i];
                int j = i - 1;
                while (j >= 0 && values[j] > value)
                {
                    values[j + 1] = values[j];
                    j--;
                }

                values[j + 1] = value;
            }

            return count % 2 == 0
                ? (values[count / 2 - 1] + values[count / 2]) * 0.5f
                : values[count / 2];
        }

        private static float Percentile(IReadOnlyList<float> sorted, float percentile)
        {
            if (sorted.Count == 0)
            {
                return 0f;
            }

            int index = Mathf.Clamp(
                Mathf.RoundToInt((sorted.Count - 1) * percentile),
                0,
                sorted.Count - 1);
            return sorted[index];
        }
    }
}
