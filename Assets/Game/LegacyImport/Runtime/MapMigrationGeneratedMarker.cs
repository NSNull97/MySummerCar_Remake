using UnityEngine;

namespace MSCMapMigration
{
    [DisallowMultipleComponent]
    public sealed class MapMigrationGeneratedMarker : MonoBehaviour
    {
        [SerializeField] private string sourceFingerprint = string.Empty;
        [SerializeField] private int tileCountX;
        [SerializeField] private int tileCountZ;
        [SerializeField] private int heightmapResolution;
        [SerializeField] private float tileSize;
        [SerializeField] private bool committed;

        public string SourceFingerprint => sourceFingerprint;
        public int TileCountX => tileCountX;
        public int TileCountZ => tileCountZ;
        public int HeightmapResolution => heightmapResolution;
        public float TileSize => tileSize;
        public bool Committed => committed;

        public void Configure(
            string fingerprint,
            int sourceTileCountX,
            int sourceTileCountZ,
            int sourceHeightmapResolution,
            float sourceTileSize,
            bool isCommitted)
        {
            sourceFingerprint = fingerprint ?? string.Empty;
            tileCountX = sourceTileCountX;
            tileCountZ = sourceTileCountZ;
            heightmapResolution = sourceHeightmapResolution;
            tileSize = sourceTileSize;
            committed = isCommitted;
        }

        public void MarkCommitted() => committed = true;
    }
}
