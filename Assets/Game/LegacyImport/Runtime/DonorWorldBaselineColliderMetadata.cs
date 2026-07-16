using UnityEngine;

namespace MSC.LegacyImport
{
    [DisallowMultipleComponent]
    public sealed class DonorWorldBaselineColliderMetadata : MonoBehaviour
    {
        [SerializeField] private string colliderStableId = string.Empty;
        [SerializeField] private string entityStableId = string.Empty;
        [SerializeField] private string replacementKey = string.Empty;
        [SerializeField] private string sourceMeshGuid = string.Empty;
        [SerializeField] private string sourceColliderType = string.Empty;
        [SerializeField] private DonorWorldBaselineClassification classification;

        public string ColliderStableId => colliderStableId;
        public string EntityStableId => entityStableId;
        public string ReplacementKey => replacementKey;
        public string SourceMeshGuid => sourceMeshGuid;
        public string SourceColliderType => sourceColliderType;
        public DonorWorldBaselineClassification Classification => classification;

        public void Configure(
            string colliderId,
            string ownerEntityId,
            string meshGuid,
            string colliderType)
        {
            colliderStableId = colliderId;
            entityStableId = ownerEntityId;
            replacementKey = "legacy-world:" + ownerEntityId;
            sourceMeshGuid = meshGuid;
            sourceColliderType = colliderType;
            classification =
                DonorWorldBaselineClassification.TemporaryDirectImport;
        }
    }
}
