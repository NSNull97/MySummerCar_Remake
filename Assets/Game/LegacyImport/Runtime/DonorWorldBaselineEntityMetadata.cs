using UnityEngine;

namespace MSC.LegacyImport
{
    [DisallowMultipleComponent]
    public sealed class DonorWorldBaselineEntityMetadata : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private string replacementKey = string.Empty;
        [SerializeField] private long sourceObjectId;
        [SerializeField] private string sourceParentStableId = string.Empty;
        [SerializeField] private string sourceHierarchyPath = string.Empty;
        [SerializeField] private string sourceMeshGuid = string.Empty;
        [SerializeField] private string sourceCellId = string.Empty;
        [SerializeField] private string semanticCategory = string.Empty;
        [SerializeField] private string sourceComponentClassIds = string.Empty;
        [SerializeField] private string sanitationDisposition = string.Empty;
        [SerializeField] private string sanitationReason = string.Empty;
        [SerializeField] private bool sourceActiveSelf;
        [SerializeField] private bool sourceActiveInHierarchy;
        [SerializeField] private bool hasSanitizedRenderer;
        [SerializeField] private DonorWorldBaselineClassification classification;

        public string StableId => stableId;
        public string ReplacementKey => replacementKey;
        public long SourceObjectId => sourceObjectId;
        public string SourceParentStableId => sourceParentStableId;
        public string SourceHierarchyPath => sourceHierarchyPath;
        public string SourceMeshGuid => sourceMeshGuid;
        public string SourceCellId => sourceCellId;
        public string SemanticCategory => semanticCategory;
        public string SourceComponentClassIds => sourceComponentClassIds;
        public string SanitationDisposition => sanitationDisposition;
        public string SanitationReason => sanitationReason;
        public bool SourceActiveSelf => sourceActiveSelf;
        public bool SourceActiveInHierarchy => sourceActiveInHierarchy;
        public bool HasSanitizedRenderer => hasSanitizedRenderer;
        public DonorWorldBaselineClassification Classification => classification;

        public void Configure(
            string id,
            long donorObjectId,
            string parentStableId,
            string hierarchyPath,
            string meshGuid,
            string cellId,
            string category,
            string componentClassIds,
            string disposition,
            string reason,
            bool wasActiveSelf,
            bool wasActiveInHierarchy,
            bool rendererIncluded)
        {
            stableId = id;
            replacementKey = "legacy-world:" + id;
            sourceObjectId = donorObjectId;
            sourceParentStableId = parentStableId;
            sourceHierarchyPath = hierarchyPath;
            sourceMeshGuid = meshGuid;
            sourceCellId = cellId;
            semanticCategory = category;
            sourceComponentClassIds = componentClassIds;
            sanitationDisposition = disposition;
            sanitationReason = reason;
            sourceActiveSelf = wasActiveSelf;
            sourceActiveInHierarchy = wasActiveInHierarchy;
            hasSanitizedRenderer = rendererIncluded;
            classification =
                DonorWorldBaselineClassification.TemporaryDirectImport;
        }
    }
}
