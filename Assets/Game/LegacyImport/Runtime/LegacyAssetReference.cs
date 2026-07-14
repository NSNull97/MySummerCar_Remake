using UnityEngine;

namespace MSC.LegacyImport
{
    /// <summary>
    /// Provenance marker allowed only on development-only reference objects.
    /// Production prefabs and build scenes are validated to reject this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LegacyAssetReference : MonoBehaviour
    {
        [SerializeField] private string recordId = string.Empty;
        [SerializeField] private string donorObjectName = string.Empty;
        [SerializeField] private string sourceSha256 = string.Empty;
        [SerializeField] private DonorTransferClassification classification =
            DonorTransferClassification.ReferenceOnly;

        public string RecordId => recordId;
        public string DonorObjectName => donorObjectName;
        public string SourceSha256 => sourceSha256;
        public DonorTransferClassification Classification => classification;
    }
}
