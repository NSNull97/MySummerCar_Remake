using System;
using System.Collections.Generic;
using MSC.World.Vegetation;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    internal readonly struct VegetationDirtyTile :
        IEquatable<VegetationDirtyTile>
    {
        public VegetationDirtyTile(
            VegetationCellAsset configuredCell,
            int configuredTileX,
            int configuredTileZ)
        {
            Cell = configuredCell;
            TileX = configuredTileX;
            TileZ = configuredTileZ;
        }

        public VegetationCellAsset Cell { get; }
        public int TileX { get; }
        public int TileZ { get; }

        public bool Equals(VegetationDirtyTile other)
        {
            return Cell == other.Cell &&
                   TileX == other.TileX &&
                   TileZ == other.TileZ;
        }

        public override bool Equals(object obj)
        {
            return obj is VegetationDirtyTile other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Cell != null ? Cell.GetHashCode() : 0;
                hashCode = (hashCode * 397) ^ TileX;
                hashCode = (hashCode * 397) ^ TileZ;
                return hashCode;
            }
        }
    }

    internal sealed class VegetationDirtyTileRegistry
    {
        private readonly HashSet<VegetationDirtyTile> dirtyTiles =
            new HashSet<VegetationDirtyTile>();

        public int Count => dirtyTiles.Count;

        public void MarkCircle(
            VegetationCellAsset cell,
            Vector3 center,
            float radius)
        {
            Bounds cellBounds = cell.WorldBounds;
            float minimumX = Mathf.Max(center.x - radius, cellBounds.min.x);
            float maximumX = Mathf.Min(center.x + radius, cellBounds.max.x);
            float minimumZ = Mathf.Max(center.z - radius, cellBounds.min.z);
            float maximumZ = Mathf.Min(center.z + radius, cellBounds.max.z);
            Vector2Int minimumTile = cell.WorldToTileCoordinate(
                new Vector3(minimumX, center.y, minimumZ));
            Vector2Int maximumTile = cell.WorldToTileCoordinate(
                new Vector3(maximumX, center.y, maximumZ));
            float radiusSquared = radius * radius;

            for (int tileX = minimumTile.x; tileX <= maximumTile.x; tileX++)
            {
                for (int tileZ = minimumTile.y; tileZ <= maximumTile.y; tileZ++)
                {
                    Bounds tileBounds = cell.GetTileWorldBounds(tileX, tileZ);
                    float closestX = Mathf.Clamp(
                        center.x,
                        tileBounds.min.x,
                        tileBounds.max.x);
                    float closestZ = Mathf.Clamp(
                        center.z,
                        tileBounds.min.z,
                        tileBounds.max.z);
                    float deltaX = center.x - closestX;
                    float deltaZ = center.z - closestZ;
                    if (deltaX * deltaX + deltaZ * deltaZ <= radiusSquared)
                    {
                        dirtyTiles.Add(
                            new VegetationDirtyTile(cell, tileX, tileZ));
                    }
                }
            }
        }

        public VegetationDirtyTile[] Consume()
        {
            var result = new VegetationDirtyTile[dirtyTiles.Count];
            dirtyTiles.CopyTo(result);
            Array.Sort(
                result,
                (left, right) =>
                {
                    int cellComparison = string.CompareOrdinal(
                        left.Cell != null ? left.Cell.CellId : string.Empty,
                        right.Cell != null ? right.Cell.CellId : string.Empty);
                    if (cellComparison != 0)
                    {
                        return cellComparison;
                    }

                    int zComparison = left.TileZ.CompareTo(right.TileZ);
                    return zComparison != 0
                        ? zComparison
                        : left.TileX.CompareTo(right.TileX);
                });
            dirtyTiles.Clear();
            return result;
        }

        public void Clear()
        {
            dirtyTiles.Clear();
        }
    }
}
