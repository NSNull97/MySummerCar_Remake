using System;
using UnityEngine;

namespace MSCMapMigration
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MapMigrationTerrainNeighborConnector : MonoBehaviour
    {
        [Min(1)] [SerializeField] private int tileCountX = 1;
        [Min(1)] [SerializeField] private int tileCountZ = 1;
        [SerializeField] private Terrain[] tiles = Array.Empty<Terrain>();

        public int TileCountX => tileCountX;
        public int TileCountZ => tileCountZ;

        public void Configure(int sourceTileCountX, int sourceTileCountZ, Terrain[] sourceTiles)
        {
            tileCountX = Mathf.Max(1, sourceTileCountX);
            tileCountZ = Mathf.Max(1, sourceTileCountZ);
            tiles = sourceTiles ?? Array.Empty<Terrain>();
            Apply();
        }

        public bool Apply()
        {
            if (tiles == null || tiles.Length != tileCountX * tileCountZ)
            {
                return false;
            }

            for (int z = 0; z < tileCountZ; z++)
            {
                for (int x = 0; x < tileCountX; x++)
                {
                    Terrain current = Get(x, z);
                    if (current == null)
                    {
                        return false;
                    }

                    current.SetNeighbors(
                        Get(x - 1, z),
                        Get(x, z + 1),
                        Get(x + 1, z),
                        Get(x, z - 1));
                }
            }

            return true;
        }

        private void OnEnable() => Apply();

        private Terrain Get(int x, int z)
        {
            if (x < 0 || z < 0 || x >= tileCountX || z >= tileCountZ)
            {
                return null;
            }

            return tiles[z * tileCountX + x];
        }
    }
}
