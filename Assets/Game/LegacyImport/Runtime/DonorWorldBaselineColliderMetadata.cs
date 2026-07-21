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
        [SerializeField] private string collisionDisposition = string.Empty;
        [SerializeField] private string collisionLayerName = string.Empty;
        [SerializeField] private string physicsMaterialAssetPath = string.Empty;
        [SerializeField] private bool safetyCritical;
        [SerializeField] private DonorWorldBaselineClassification classification;

        public string ColliderStableId => colliderStableId;
        public string EntityStableId => entityStableId;
        public string ReplacementKey => replacementKey;
        public string SourceMeshGuid => sourceMeshGuid;
        public string SourceColliderType => sourceColliderType;
        public string CollisionDisposition => collisionDisposition;
        public string CollisionLayerName => collisionLayerName;
        public string PhysicsMaterialAssetPath => physicsMaterialAssetPath;
        public bool SafetyCritical => safetyCritical;
        public DonorWorldBaselineClassification Classification => classification;

        public void Configure(
            string colliderId,
            string ownerEntityId,
            string meshGuid,
            string colliderType,
            string disposition,
            string layerName,
            string physicsMaterialPath,
            bool isSafetyCritical)
        {
            colliderStableId = colliderId;
            entityStableId = ownerEntityId;
            replacementKey = "legacy-world:" + ownerEntityId;
            sourceMeshGuid = meshGuid;
            sourceColliderType = colliderType;
            collisionDisposition = disposition;
            collisionLayerName = layerName;
            physicsMaterialAssetPath = physicsMaterialPath;
            safetyCritical = isSafetyCritical;
            classification =
                DonorWorldBaselineClassification.TemporaryDirectImport;
        }
    }
}
