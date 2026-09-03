using UnityEngine;

namespace MSCMapMigration
{
    [DisallowMultipleComponent]
    public sealed class MapMigrationTerrainResidualMarker : MonoBehaviour
    {
        [SerializeField] private string sourceRecordId = string.Empty;
        [SerializeField] private int triangleCount;

        public string SourceRecordId => sourceRecordId;
        public int TriangleCount => triangleCount;

        public void Configure(string recordId, int preservedTriangleCount)
        {
            sourceRecordId = recordId ?? string.Empty;
            triangleCount = Mathf.Max(0, preservedTriangleCount);
        }
    }
}
