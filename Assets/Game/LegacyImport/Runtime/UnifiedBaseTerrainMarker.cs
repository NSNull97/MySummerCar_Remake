using UnityEngine;

namespace MSC.LegacyImport
{
    /// <summary>
    /// Project-owned provenance and replacement identity for the private Phase 1
    /// unified donor-derived base terrain presentation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UnifiedBaseTerrainMarker : MonoBehaviour
    {
        [SerializeField] private string stableReplacementKey = string.Empty;
        [SerializeField] private string sourceAssetPath = string.Empty;
        [SerializeField] private string sourceSha256 = string.Empty;
        [SerializeField] private float gridStepMeters;
        [SerializeField] private int sourceVertexCount;
        [SerializeField] private int sourceTriangleCount;
        [SerializeField] private DonorWorldBaselineClassification classification;

        public string StableReplacementKey => stableReplacementKey;
        public string SourceAssetPath => sourceAssetPath;
        public string SourceSha256 => sourceSha256;
        public float GridStepMeters => gridStepMeters;
        public int SourceVertexCount => sourceVertexCount;
        public int SourceTriangleCount => sourceTriangleCount;
        public DonorWorldBaselineClassification Classification => classification;

        public void Configure(
            string replacementKey,
            string projectRelativeSourceAssetPath,
            string sha256,
            float sampleGridStepMeters,
            int vertexCount,
            int triangleCount)
        {
            stableReplacementKey = replacementKey ?? string.Empty;
            sourceAssetPath = projectRelativeSourceAssetPath ?? string.Empty;
            sourceSha256 = sha256 ?? string.Empty;
            gridStepMeters = sampleGridStepMeters;
            sourceVertexCount = vertexCount;
            sourceTriangleCount = triangleCount;
            classification =
                DonorWorldBaselineClassification.TemporaryDirectImport;
        }
    }
}
