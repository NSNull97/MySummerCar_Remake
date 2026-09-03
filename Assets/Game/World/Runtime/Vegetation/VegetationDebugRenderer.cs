using System.Collections.Generic;
using UnityEngine;

namespace MSC.World.Vegetation
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class VegetationDebugRenderer : MonoBehaviour
    {
        [SerializeField] private VegetationCellCatalog catalog;
        [SerializeField] private VegetationDebugMode debugMode =
            VegetationDebugMode.None;
        [SerializeField, Range(4, 64)] private int densityPreviewResolution = 16;
        [SerializeField, Min(0.01f)] private float densityPointSize = 0.35f;

        public VegetationDebugMode DebugMode
        {
            get => debugMode;
            set => debugMode = value;
        }

        private void OnDrawGizmos()
        {
            if (catalog == null || debugMode == VegetationDebugMode.None)
            {
                return;
            }

            if ((debugMode & VegetationDebugMode.DensityMaskOverlay) != 0)
            {
                DrawDensityMasks();
            }

            if ((debugMode & VegetationDebugMode.BlockerMaskOverlay) != 0)
            {
                DrawBlockers();
            }

            if ((debugMode & VegetationDebugMode.DirtyTileBounds) != 0)
            {
                DrawDirtyTileBounds();
            }

            DrawTileDebug();
        }

        private void DrawDensityMasks()
        {
            IReadOnlyList<VegetationCellAsset> cells = catalog.Cells;
            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                VegetationCellAsset cell = cells[cellIndex];
                if (cell == null || cell.DensityMask == null)
                {
                    continue;
                }

                for (int x = 0; x < densityPreviewResolution; x++)
                {
                    for (int z = 0; z < densityPreviewResolution; z++)
                    {
                        float u = (x + 0.5f) / densityPreviewResolution;
                        float v = (z + 0.5f) / densityPreviewResolution;
                        Color density = cell.DensityMask.GetPixelBilinear(u, v);
                        float maximum = Mathf.Max(
                            Mathf.Max(density.r, density.g),
                            Mathf.Max(density.b, density.a));
                        if (maximum <= 0.01f)
                        {
                            continue;
                        }

                        Bounds bounds = cell.WorldBounds;
                        Vector3 position = new Vector3(
                            Mathf.Lerp(bounds.min.x, bounds.max.x, u),
                            bounds.center.y,
                            Mathf.Lerp(bounds.min.z, bounds.max.z, v));
                        Gizmos.color = new Color(
                            density.r,
                            density.g,
                            density.b,
                            Mathf.Max(0.25f, density.a));
                        Gizmos.DrawCube(
                            position,
                            new Vector3(
                                densityPointSize,
                                densityPointSize,
                                densityPointSize));
                    }
                }
            }
        }

        private void DrawBlockers()
        {
            VegetationBlocker[] blockers =
                FindObjectsByType<VegetationBlocker>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            Gizmos.color = new Color(1f, 0.15f, 0.05f, 0.7f);
            for (int index = 0; index < blockers.Length; index++)
            {
                Collider collider = blockers[index].BlockerCollider;
                if (collider != null)
                {
                    Gizmos.DrawWireCube(collider.bounds.center, collider.bounds.size);
                }
            }
        }

        private void DrawDirtyTileBounds()
        {
            IReadOnlyList<Bounds> dirtyBounds = catalog.DirtyTileDebugBounds;
            Gizmos.color = new Color(1f, 0.5f, 0.05f, 0.95f);
            for (int index = 0; index < dirtyBounds.Count; index++)
            {
                Gizmos.DrawWireCube(
                    dirtyBounds[index].center,
                    dirtyBounds[index].size);
            }
        }

        private void DrawTileDebug()
        {
            bool drawBounds =
                (debugMode & VegetationDebugMode.LodAndCullingBounds) != 0;
            bool drawCandidates =
                (debugMode & VegetationDebugMode.CandidatePositions) != 0;
            bool drawRejected =
                (debugMode & VegetationDebugMode.RejectedSamples) != 0;
            bool drawCounts =
                (debugMode & VegetationDebugMode.InstanceCountPerTile) != 0;

            IReadOnlyList<VegetationCellAsset> cells = catalog.Cells;
            for (int cellIndex = 0; cellIndex < cells.Count; cellIndex++)
            {
                VegetationCellAsset cell = cells[cellIndex];
                if (cell == null)
                {
                    continue;
                }

                IReadOnlyList<VegetationTileRecord> tiles = cell.Tiles;
                for (int tileIndex = 0; tileIndex < tiles.Count; tileIndex++)
                {
                    VegetationTileRecord tile = tiles[tileIndex];
                    if (tile == null)
                    {
                        continue;
                    }

                    if (drawBounds)
                    {
                        Gizmos.color = new Color(0.1f, 1f, 0.9f, 0.75f);
                        Gizmos.DrawWireCube(
                            tile.WorldBounds.center,
                            tile.WorldBounds.size);
                    }

                    if (drawCandidates)
                    {
                        Gizmos.color = Color.cyan;
                        IReadOnlyList<Vector3> candidates =
                            tile.CandidateDebugPositions;
                        for (int index = 0; index < candidates.Count; index++)
                        {
                            Gizmos.DrawSphere(candidates[index], 0.08f);
                        }
                    }

                    if (drawRejected)
                    {
                        IReadOnlyList<VegetationRejectedSample> rejected =
                            tile.RejectedDebugSamples;
                        for (int index = 0; index < rejected.Count; index++)
                        {
                            Gizmos.color = GetRejectionColor(rejected[index].Reason);
                            Gizmos.DrawCube(
                                rejected[index].WorldPosition,
                                Vector3.one * 0.12f);
                        }
                    }

#if UNITY_EDITOR
                    if (drawCounts)
                    {
                        UnityEditor.Handles.Label(
                            tile.WorldBounds.center,
                            $"{cell.CellId}/{tile.TileX},{tile.TileZ}: " +
                            tile.TotalInstanceCount);
                    }
#endif
                }
            }
        }

        private static Color GetRejectionColor(
            VegetationRejectionReason rejectionReason)
        {
            return rejectionReason switch
            {
                VegetationRejectionReason.Blocked => Color.red,
                VegetationRejectionReason.Water => Color.blue,
                VegetationRejectionReason.ExcessiveSlope => Color.yellow,
                VegetationRejectionReason.ExclusionVolume => Color.magenta,
                _ => new Color(1f, 0.5f, 0f, 1f)
            };
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(VegetationCellCatalog configuredCatalog)
        {
            catalog = configuredCatalog;
        }
#endif
    }
}
