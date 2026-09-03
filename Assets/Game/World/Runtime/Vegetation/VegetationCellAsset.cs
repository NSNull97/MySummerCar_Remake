using System;
using System.Collections.Generic;
using MSC.World.Partition;
using UnityEngine;

namespace MSC.World.Vegetation
{
    [CreateAssetMenu(
        fileName = "VegetationCell",
        menuName = "MSC/World/Vegetation/Cell Asset")]
    public sealed class VegetationCellAsset : ScriptableObject
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string cellId = "cell_0_0";
        [SerializeField] private int cellX;
        [SerializeField] private int cellZ;
        [SerializeField] private Bounds worldBounds =
            new Bounds(Vector3.zero, new Vector3(512f, 2048f, 512f));
        [SerializeField, Min(16)] private int maskResolution = 512;
        [SerializeField, Min(16f)] private float tileSizeMeters = 32f;
        [SerializeField] private Texture2D densityMask;
        [SerializeField] private VegetationTileRecord[] tiles =
            Array.Empty<VegetationTileRecord>();

        public int SchemaVersion => schemaVersion;
        public string CellId => cellId;
        public WorldCellIndex CellIndex => new WorldCellIndex(cellX, cellZ);
        public Bounds WorldBounds => worldBounds;
        public int MaskResolution => maskResolution;
        public float TileSizeMeters => tileSizeMeters;
        public Texture2D DensityMask => densityMask;
        public IReadOnlyList<VegetationTileRecord> Tiles =>
            tiles ?? Array.Empty<VegetationTileRecord>();
        public int TileCountX => Mathf.CeilToInt(worldBounds.size.x / tileSizeMeters);
        public int TileCountZ => Mathf.CeilToInt(worldBounds.size.z / tileSizeMeters);

        public bool ContainsWorldXZ(Vector3 worldPosition)
        {
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            return worldPosition.x >= min.x &&
                   worldPosition.x <= max.x &&
                   worldPosition.z >= min.z &&
                   worldPosition.z <= max.z;
        }

        public Vector2 WorldToMaskUv(Vector3 worldPosition)
        {
            Vector3 min = worldBounds.min;
            Vector3 size = worldBounds.size;
            return new Vector2(
                Mathf.Clamp01((worldPosition.x - min.x) / size.x),
                Mathf.Clamp01((worldPosition.z - min.z) / size.z));
        }

        public Vector2Int WorldToMaskPixel(Vector3 worldPosition)
        {
            Vector2 uv = WorldToMaskUv(worldPosition);
            int maximumPixel = Mathf.Max(0, maskResolution - 1);
            return new Vector2Int(
                Mathf.Clamp(Mathf.FloorToInt(uv.x * maskResolution), 0, maximumPixel),
                Mathf.Clamp(Mathf.FloorToInt(uv.y * maskResolution), 0, maximumPixel));
        }

        public RectInt WorldCircleToPixelRect(Vector3 center, float radius)
        {
            Vector3 minimum = center - new Vector3(radius, 0f, radius);
            Vector3 maximum = center + new Vector3(radius, 0f, radius);
            Vector2 minimumUv = WorldToMaskUv(minimum);
            Vector2 maximumUv = WorldToMaskUv(maximum);
            int xMin = Mathf.Clamp(
                Mathf.FloorToInt(minimumUv.x * maskResolution),
                0,
                maskResolution - 1);
            int yMin = Mathf.Clamp(
                Mathf.FloorToInt(minimumUv.y * maskResolution),
                0,
                maskResolution - 1);
            int xMax = Mathf.Clamp(
                Mathf.CeilToInt(maximumUv.x * maskResolution),
                xMin + 1,
                maskResolution);
            int yMax = Mathf.Clamp(
                Mathf.CeilToInt(maximumUv.y * maskResolution),
                yMin + 1,
                maskResolution);
            return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        public Vector3 MaskPixelToWorldXZ(int pixelX, int pixelY, float worldY)
        {
            float u = (pixelX + 0.5f) / maskResolution;
            float v = (pixelY + 0.5f) / maskResolution;
            Vector3 min = worldBounds.min;
            return new Vector3(
                Mathf.Lerp(min.x, worldBounds.max.x, u),
                worldY,
                Mathf.Lerp(min.z, worldBounds.max.z, v));
        }

        public float SampleDensity(
            VegetationDensityChannel channel,
            Vector3 worldPosition)
        {
            if (densityMask == null || !ContainsWorldXZ(worldPosition))
            {
                return 0f;
            }

            Color sampled = densityMask.GetPixelBilinear(
                WorldToMaskUv(worldPosition).x,
                WorldToMaskUv(worldPosition).y);
            return channel switch
            {
                VegetationDensityChannel.ShortGrass => sampled.r,
                VegetationDensityChannel.MeadowGrass => sampled.g,
                VegetationDensityChannel.TallGrass => sampled.b,
                VegetationDensityChannel.Decorative => sampled.a,
                _ => 0f
            };
        }

        public Vector2Int WorldToTileCoordinate(Vector3 worldPosition)
        {
            Vector3 min = worldBounds.min;
            return new Vector2Int(
                Mathf.Clamp(
                    Mathf.FloorToInt((worldPosition.x - min.x) / tileSizeMeters),
                    0,
                    TileCountX - 1),
                Mathf.Clamp(
                    Mathf.FloorToInt((worldPosition.z - min.z) / tileSizeMeters),
                    0,
                    TileCountZ - 1));
        }

        public Bounds GetTileWorldBounds(int tileX, int tileZ)
        {
            Vector3 cellMin = worldBounds.min;
            float minimumX = cellMin.x + tileX * tileSizeMeters;
            float minimumZ = cellMin.z + tileZ * tileSizeMeters;
            float maximumX = Mathf.Min(minimumX + tileSizeMeters, worldBounds.max.x);
            float maximumZ = Mathf.Min(minimumZ + tileSizeMeters, worldBounds.max.z);
            Vector3 minimum = new Vector3(minimumX, worldBounds.min.y, minimumZ);
            Vector3 maximum = new Vector3(maximumX, worldBounds.max.y, maximumZ);
            return new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
        }

        public bool TryGetTile(int tileX, int tileZ, out VegetationTileRecord tile)
        {
            VegetationTileRecord[] configuredTiles =
                tiles ?? Array.Empty<VegetationTileRecord>();
            for (int index = 0; index < configuredTiles.Length; index++)
            {
                VegetationTileRecord candidate = configuredTiles[index];
                if (candidate != null &&
                    candidate.TileX == tileX &&
                    candidate.TileZ == tileZ)
                {
                    tile = candidate;
                    return true;
                }
            }

            tile = null;
            return false;
        }

        public IReadOnlyList<string> ValidateConfiguration()
        {
            var errors = new List<string>();
            if (schemaVersion != CurrentSchemaVersion)
            {
                errors.Add(
                    name + " schema does not match " + CurrentSchemaVersion + ".");
            }

            if (string.IsNullOrWhiteSpace(cellId) ||
                !string.Equals(cellId, CellIndex.Id, StringComparison.Ordinal))
            {
                errors.Add(name + " has an invalid stable cell ID.");
            }

            if (worldBounds.size.x <= 0f || worldBounds.size.z <= 0f)
            {
                errors.Add(name + " has invalid world bounds.");
            }

            if (densityMask == null)
            {
                errors.Add(name + " has no RGBA density mask.");
            }
            else if (densityMask.width != maskResolution ||
                     densityMask.height != maskResolution)
            {
                errors.Add(name + " density mask resolution does not match metadata.");
            }

            if (tileSizeMeters != 16f && tileSizeMeters != 32f)
            {
                errors.Add(name + " tile size must be 16 or 32 metres.");
            }

            var uniqueTiles = new HashSet<Vector2Int>();
            VegetationTileRecord[] configuredTiles =
                tiles ?? Array.Empty<VegetationTileRecord>();
            for (int index = 0; index < configuredTiles.Length; index++)
            {
                VegetationTileRecord tile = configuredTiles[index];
                if (tile == null)
                {
                    errors.Add(name + " contains a null tile record.");
                    continue;
                }

                var key = new Vector2Int(tile.TileX, tile.TileZ);
                if (!uniqueTiles.Add(key))
                {
                    errors.Add(name + " duplicates tile " + key + ".");
                }
            }

            return errors;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string configuredCellId,
            WorldCellIndex configuredCellIndex,
            Bounds configuredWorldBounds,
            int configuredMaskResolution,
            float configuredTileSizeMeters,
            Texture2D configuredDensityMask)
        {
            schemaVersion = CurrentSchemaVersion;
            cellId = configuredCellId;
            cellX = configuredCellIndex.X;
            cellZ = configuredCellIndex.Z;
            worldBounds = configuredWorldBounds;
            maskResolution = Mathf.Max(16, configuredMaskResolution);
            tileSizeMeters = configuredTileSizeMeters <= 16f ? 16f : 32f;
            densityMask = configuredDensityMask;
            if (tiles == null)
            {
                tiles = Array.Empty<VegetationTileRecord>();
            }
        }

        public void ReplaceTileForAuthoring(VegetationTileRecord replacement)
        {
            if (replacement == null)
            {
                throw new ArgumentNullException(nameof(replacement));
            }

            var configuredTiles = new List<VegetationTileRecord>(
                tiles ?? Array.Empty<VegetationTileRecord>());
            int existingIndex = configuredTiles.FindIndex(
                tile => tile != null &&
                        tile.TileX == replacement.TileX &&
                        tile.TileZ == replacement.TileZ);
            if (existingIndex >= 0)
            {
                configuredTiles[existingIndex] = replacement;
            }
            else
            {
                configuredTiles.Add(replacement);
                configuredTiles.Sort((left, right) =>
                {
                    int zComparison = left.TileZ.CompareTo(right.TileZ);
                    return zComparison != 0
                        ? zComparison
                        : left.TileX.CompareTo(right.TileX);
                });
            }

            tiles = configuredTiles.ToArray();
        }
#endif
    }
}
