using UnityEngine;

namespace MSCMapMigration
{
    [DisallowMultipleComponent]
    public sealed class MapMigrationSourceMarker : MonoBehaviour
    {
        [SerializeField] private string recordId = string.Empty;
        [SerializeField] private float coverageRatio;
        [SerializeField] private bool coveragePassed;
        [SerializeField] private bool rendererWasEnabled;
        [SerializeField] private bool colliderWasEnabled;

        public string RecordId => recordId;
        public float CoverageRatio => coverageRatio;
        public bool CoveragePassed => coveragePassed;
        public bool RendererWasEnabled => rendererWasEnabled;
        public bool ColliderWasEnabled => colliderWasEnabled;

        public void Configure(
            string sourceRecordId,
            float sourceCoverage,
            bool passed,
            bool originalRendererEnabled,
            bool originalColliderEnabled)
        {
            recordId = sourceRecordId ?? string.Empty;
            coverageRatio = sourceCoverage;
            coveragePassed = passed;
            rendererWasEnabled = originalRendererEnabled;
            colliderWasEnabled = originalColliderEnabled;
        }
    }
}
