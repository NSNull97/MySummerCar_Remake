using UnityEngine;

namespace MSC.LegacyImport
{
    public enum DonorWorldStreamingOwnership
    {
        GlobalLegacy = 0,
        CellLegacy = 1
    }

    [DisallowMultipleComponent]
    public sealed class DonorWorldStreamingSceneMetadata : MonoBehaviour
    {
        [SerializeField] private string generatorVersion = string.Empty;
        [SerializeField] private string sceneId = string.Empty;
        [SerializeField] private DonorWorldStreamingOwnership ownership;
        [SerializeField] private string ownerCellId = string.Empty;
        [SerializeField] private string sourceRevisionId = string.Empty;
        [SerializeField] private string sourceSceneSha256 = string.Empty;
        [SerializeField] private DonorWorldBaselineClassification classification;
        [SerializeField] private DonorWorldBaselineActivationState activationState;
        [SerializeField] private int entityCount;
        [SerializeField] private int rendererCount;
        [SerializeField] private int colliderCount;
        [SerializeField] private string ownershipFingerprintSha256 = string.Empty;

        public string GeneratorVersion => generatorVersion;
        public string SceneId => sceneId;
        public DonorWorldStreamingOwnership Ownership => ownership;
        public string OwnerCellId => ownerCellId;
        public string SourceRevisionId => sourceRevisionId;
        public string SourceSceneSha256 => sourceSceneSha256;
        public DonorWorldBaselineClassification Classification => classification;
        public DonorWorldBaselineActivationState ActivationState => activationState;
        public int EntityCount => entityCount;
        public int RendererCount => rendererCount;
        public int ColliderCount => colliderCount;
        public string OwnershipFingerprintSha256 => ownershipFingerprintSha256;

        public void Configure(
            string version,
            string id,
            DonorWorldStreamingOwnership configuredOwnership,
            string cellId,
            string revisionId,
            string sceneSha256,
            int entities,
            int renderers,
            int colliders,
            string fingerprint)
        {
            generatorVersion = version;
            sceneId = id;
            ownership = configuredOwnership;
            ownerCellId = cellId;
            sourceRevisionId = revisionId;
            sourceSceneSha256 = sceneSha256;
            classification =
                DonorWorldBaselineClassification.TemporaryDirectImport;
            activationState =
                DonorWorldBaselineActivationState.ActiveFeatureParityProfile;
            entityCount = entities;
            rendererCount = renderers;
            colliderCount = colliders;
            ownershipFingerprintSha256 = fingerprint;
        }
    }
}
