using UnityEngine;

namespace MSC.LegacyImport
{
    /// <summary>
    /// Project-owned provenance for a bounded Phase 1 visual/collision overlay
    /// that supplements, but does not mutate, the frozen 06B baseline plan.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DonorWorldSupplementalEntityMetadata : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string replacementKey = string.Empty;
        [SerializeField] private long sourceObjectId;
        [SerializeField] private string sourceHierarchyPath = string.Empty;
        [SerializeField] private string sourceMeshGuid = string.Empty;
        [SerializeField] private string sourceCellId = string.Empty;
        [SerializeField] private string manifestId = string.Empty;
        [SerializeField] private bool hasSanitizedRenderer;
        [SerializeField] private int sanitizedColliderCount;
        [SerializeField] private DonorWorldBaselineClassification classification;

        public string StableId => stableId;
        public string ReplacementKey => replacementKey;
        public long SourceObjectId => sourceObjectId;
        public string SourceHierarchyPath => sourceHierarchyPath;
        public string SourceMeshGuid => sourceMeshGuid;
        public string SourceCellId => sourceCellId;
        public string ManifestId => manifestId;
        public bool HasSanitizedRenderer => hasSanitizedRenderer;
        public int SanitizedColliderCount => sanitizedColliderCount;
        public DonorWorldBaselineClassification Classification =>
            classification;

        public void Configure(
            string id,
            long donorObjectId,
            string hierarchyPath,
            string meshGuid,
            string cellId,
            string sourceManifestId,
            bool rendererIncluded,
            int colliderCount)
        {
            stableId = id;
            replacementKey = "legacy-world:" + id;
            sourceObjectId = donorObjectId;
            sourceHierarchyPath = hierarchyPath;
            sourceMeshGuid = meshGuid;
            sourceCellId = cellId;
            manifestId = sourceManifestId;
            hasSanitizedRenderer = rendererIncluded;
            sanitizedColliderCount = colliderCount;
            classification =
                DonorWorldBaselineClassification.TemporaryDirectImport;
        }
    }
}
