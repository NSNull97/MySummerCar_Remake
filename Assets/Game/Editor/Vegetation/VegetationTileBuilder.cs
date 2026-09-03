using System;
using System.Collections.Generic;
using MSC.World.Vegetation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    internal static class VegetationTileBuilder
    {
        private const int MaximumDebugCandidates = 128;
        private const int MaximumDebugRejections = 128;

        public static void RebuildDirtyTiles(
            VegetationCellCatalog catalog,
            IReadOnlyList<VegetationDirtyTile> dirtyTiles)
        {
            if (catalog == null || dirtyTiles == null || dirtyTiles.Count == 0)
            {
                return;
            }

            VegetationExclusionVolume[] exclusions =
                UnityEngine.Object.FindObjectsByType<VegetationExclusionVolume>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            var rebuiltBounds = new List<Bounds>(dirtyTiles.Count);
            for (int dirtyIndex = 0; dirtyIndex < dirtyTiles.Count; dirtyIndex++)
            {
                VegetationDirtyTile dirty = dirtyTiles[dirtyIndex];
                if (dirty.Cell == null)
                {
                    continue;
                }

                VegetationTileRecord rebuilt = BuildTile(
                    catalog,
                    dirty.Cell,
                    dirty.TileX,
                    dirty.TileZ,
                    exclusions);
                dirty.Cell.ReplaceTileForAuthoring(rebuilt);
                EditorUtility.SetDirty(dirty.Cell);
                rebuiltBounds.Add(rebuilt.WorldBounds);
            }

            catalog.SetDirtyTileDebugBoundsForAuthoring(rebuiltBounds);
            AssetDatabase.SaveAssets();
            RebuildActiveRenderers(catalog);
        }

        private static VegetationTileRecord BuildTile(
            VegetationCellCatalog catalog,
            VegetationCellAsset cell,
            int tileX,
            int tileZ,
            IReadOnlyList<VegetationExclusionVolume> exclusions)
        {
            Bounds generationBounds = cell.GetTileWorldBounds(tileX, tileZ);
            var profileBatches = new List<VegetationProfileTileInstances>();
            var debugCandidates = new List<Vector3>(MaximumDebugCandidates);
            var debugRejections =
                new List<VegetationRejectedSample>(MaximumDebugRejections);
            var instancePositions = new List<Vector3>();
            float maximumInstanceRadius = 0.25f;

            IReadOnlyList<VegetationProfile> profiles = catalog.Profiles;
            for (int profileIndex = 0; profileIndex < profiles.Count; profileIndex++)
            {
                VegetationProfile profile = profiles[profileIndex];
                if (profile == null)
                {
                    continue;
                }

                var instances = new List<VegetationInstanceRecord>();
                GenerateProfileInstances(
                    catalog,
                    cell,
                    generationBounds,
                    profile,
                    profileIndex,
                    exclusions,
                    instances,
                    instancePositions,
                    debugCandidates,
                    debugRejections,
                    ref maximumInstanceRadius);
                if (instances.Count > 0)
                {
                    profileBatches.Add(
                        new VegetationProfileTileInstances(
                            profileIndex,
                            instances.ToArray()));
                }
            }

            Bounds tightBounds = CalculateTightBounds(
                generationBounds,
                instancePositions,
                maximumInstanceRadius);
            return new VegetationTileRecord(
                tileX,
                tileZ,
                tightBounds,
                profileBatches.ToArray(),
                debugCandidates.ToArray(),
                debugRejections.ToArray());
        }

        private static void GenerateProfileInstances(
            VegetationCellCatalog catalog,
            VegetationCellAsset cell,
            Bounds tileBounds,
            VegetationProfile profile,
            int profileIndex,
            IReadOnlyList<VegetationExclusionVolume> exclusions,
            ICollection<VegetationInstanceRecord> output,
            ICollection<Vector3> instancePositions,
            ICollection<Vector3> debugCandidates,
            ICollection<VegetationRejectedSample> debugRejections,
            ref float maximumInstanceRadius)
        {
            float spacing = profile.CandidateSpacingMeters;
            int minimumGridX = Mathf.FloorToInt(tileBounds.min.x / spacing);
            int maximumGridX = Mathf.CeilToInt(tileBounds.max.x / spacing);
            int minimumGridZ = Mathf.FloorToInt(tileBounds.min.z / spacing);
            int maximumGridZ = Mathf.CeilToInt(tileBounds.max.z / spacing);

            for (int gridX = minimumGridX; gridX < maximumGridX; gridX++)
            {
                for (int gridZ = minimumGridZ; gridZ < maximumGridZ; gridZ++)
                {
                    Vector2 jitter = VegetationStableHash.JitteredGridOffset(
                        gridX,
                        gridZ,
                        profile.StableSeed);
                    float worldX = (gridX + jitter.x) * spacing;
                    float worldZ = (gridZ + jitter.y) * spacing;
                    if (worldX < tileBounds.min.x ||
                        worldX >= tileBounds.max.x ||
                        worldZ < tileBounds.min.z ||
                        worldZ >= tileBounds.max.z)
                    {
                        continue;
                    }

                    var candidate = new Vector3(worldX, tileBounds.center.y, worldZ);
                    if (debugCandidates.Count < MaximumDebugCandidates)
                    {
                        debugCandidates.Add(candidate);
                    }

                    float density = cell.SampleDensity(
                        profile.DensityChannel,
                        candidate);
                    uint densityHash = VegetationStableHash.Hash(
                        gridX,
                        gridZ,
                        profile.StableSeed ^ 0x2a41);
                    if (VegetationStableHash.ToUnitFloat(densityHash) >
                        Mathf.Clamp01(density * profile.DensityMultiplier))
                    {
                        AddRejection(
                            candidate,
                            VegetationRejectionReason.NoDensity,
                            debugRejections);
                        continue;
                    }

                    var rayOrigin = new Vector3(
                        worldX,
                        catalog.CandidateRaycastHeight,
                        worldZ);
                    var ray = new Ray(rayOrigin, Vector3.down);
                    if (!VegetationSceneRaycaster.TryResolve(
                            ray,
                            catalog.CandidateRaycastDistance,
                            catalog.RelevantRaycastLayers,
                            profile.DensityChannel,
                            out VegetationResolvedHit resolved,
                            out VegetationRejectionReason rejection))
                    {
                        AddRejection(candidate, rejection, debugRejections);
                        continue;
                    }

                    Vector3 position = resolved.Hit.point;
                    float slope = Vector3.Angle(resolved.Hit.normal, Vector3.up);
                    float maximumSlope = Mathf.Min(
                        profile.MaximumSlopeDegrees,
                        resolved.Surface.SlopeOverrideDegrees);
                    if (slope > maximumSlope)
                    {
                        AddRejection(
                            position,
                            VegetationRejectionReason.ExcessiveSlope,
                            debugRejections);
                        continue;
                    }

                    Vector2 heightRange = profile.WorldHeightRange;
                    if (position.y < heightRange.x || position.y > heightRange.y)
                    {
                        AddRejection(
                            position,
                            VegetationRejectionReason.OutsideHeightRange,
                            debugRejections);
                        continue;
                    }

                    if (IsExcluded(profile.DensityChannel, position, exclusions))
                    {
                        AddRejection(
                            position,
                            VegetationRejectionReason.ExclusionVolume,
                            debugRejections);
                        continue;
                    }

                    uint transformHash = VegetationStableHash.Hash(
                        gridX,
                        gridZ,
                        profile.StableSeed ^ 0x6d2b);
                    uint variationHash = VegetationStableHash.Hash(
                        gridX,
                        gridZ,
                        profile.StableSeed ^ 0x15ad);
                    float yaw = VegetationStableHash.ToUnitFloat(transformHash) * 360f;
                    Vector2 scaleRange = profile.UniformScaleRange;
                    float scale = Mathf.Lerp(
                        scaleRange.x,
                        scaleRange.y,
                        VegetationStableHash.ToUnitFloat(
                            VegetationStableHash.Hash(transformHash ^ 0xa511e9b3u)));
                    Vector3 partiallyAlignedNormal = Vector3.Slerp(
                        Vector3.up,
                        resolved.Hit.normal.normalized,
                        profile.SurfaceNormalAlignment).normalized;
                    byte colorVariation = (byte)(variationHash & 0xffu);
                    ushort windPhase = (ushort)((variationHash >> 8) & 0xffffu);
                    position += resolved.Hit.normal * 0.015f;

                    output.Add(
                        new VegetationInstanceRecord(
                            position,
                            partiallyAlignedNormal,
                            yaw,
                            scale,
                            (byte)profileIndex,
                            colorVariation,
                            windPhase));
                    instancePositions.Add(position);
                    Mesh nearMesh = profile.GetLodMesh(0);
                    if (nearMesh != null)
                    {
                        maximumInstanceRadius = Mathf.Max(
                            maximumInstanceRadius,
                            nearMesh.bounds.extents.magnitude * scale +
                            0.5f);
                    }
                }
            }
        }

        private static bool IsExcluded(
            VegetationDensityChannel channel,
            Vector3 position,
            IReadOnlyList<VegetationExclusionVolume> exclusions)
        {
            for (int index = 0; index < exclusions.Count; index++)
            {
                VegetationExclusionVolume exclusion = exclusions[index];
                if (exclusion != null && exclusion.Excludes(channel, position))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddRejection(
            Vector3 position,
            VegetationRejectionReason reason,
            ICollection<VegetationRejectedSample> debugRejections)
        {
            if (debugRejections.Count < MaximumDebugRejections)
            {
                debugRejections.Add(
                    new VegetationRejectedSample(position, reason));
            }
        }

        private static Bounds CalculateTightBounds(
            Bounds fallback,
            IReadOnlyList<Vector3> positions,
            float expansion)
        {
            if (positions.Count == 0)
            {
                return new Bounds(
                    new Vector3(fallback.center.x, 0f, fallback.center.z),
                    new Vector3(fallback.size.x, 0.1f, fallback.size.z));
            }

            var result = new Bounds(positions[0], Vector3.zero);
            for (int index = 1; index < positions.Count; index++)
            {
                result.Encapsulate(positions[index]);
            }

            result.Expand(expansion * 2f);
            return result;
        }

        private static void RebuildActiveRenderers(
            VegetationCellCatalog catalog)
        {
            VegetationWorldRenderer[] renderers =
                UnityEngine.Object.FindObjectsByType<VegetationWorldRenderer>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int index = 0; index < renderers.Length; index++)
            {
                if (renderers[index].Catalog == catalog)
                {
                    renderers[index].RebuildGpuResources();
                }
            }
        }
    }
}
