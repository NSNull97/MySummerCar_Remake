using UnityEngine;

namespace MSC.LegacyImport
{
    public enum DonorWorldBaselineClassification
    {
        TemporaryDirectImport = 0
    }

    public enum DonorWorldBaselineActivationState
    {
        PreparedNotActiveUntil06B2 = 0,
        ActiveFeatureParityProfile = 1
    }

    [DisallowMultipleComponent]
    public sealed class DonorWorldBaselineSceneMetadata : MonoBehaviour
    {
        [SerializeField] private string sourceRevisionId = string.Empty;
        [SerializeField] private string sourceSceneSha256 = string.Empty;
        [SerializeField] private DonorWorldBaselineClassification classification;
        [SerializeField] private DonorWorldBaselineActivationState activationState;
        [SerializeField] private Vector3 sourceToProjectTranslation;
        [SerializeField] private Quaternion sourceToProjectRotation = Quaternion.identity;
        [SerializeField] private Vector3 sourceToProjectScale = Vector3.one;
        [SerializeField] private int sourceEntityCount;
        [SerializeField] private int rendererEntityCount;
        [SerializeField] private int metadataOnlyEntityCount;
        [SerializeField] private string semanticFingerprintSha256 = string.Empty;

        public string SourceRevisionId => sourceRevisionId;
        public string SourceSceneSha256 => sourceSceneSha256;
        public DonorWorldBaselineClassification Classification => classification;
        public DonorWorldBaselineActivationState ActivationState => activationState;
        public Vector3 SourceToProjectTranslation => sourceToProjectTranslation;
        public Quaternion SourceToProjectRotation => sourceToProjectRotation;
        public Vector3 SourceToProjectScale => sourceToProjectScale;
        public int SourceEntityCount => sourceEntityCount;
        public int RendererEntityCount => rendererEntityCount;
        public int MetadataOnlyEntityCount => metadataOnlyEntityCount;
        public string SemanticFingerprintSha256 => semanticFingerprintSha256;

        public void Configure(
            string revisionId,
            string sceneSha256,
            Vector3 translation,
            Quaternion rotation,
            Vector3 scale,
            int entityCount,
            int rendererCount,
            int metadataOnlyCount,
            string semanticFingerprint)
        {
            sourceRevisionId = revisionId;
            sourceSceneSha256 = sceneSha256;
            classification =
                DonorWorldBaselineClassification.TemporaryDirectImport;
            activationState =
                DonorWorldBaselineActivationState.PreparedNotActiveUntil06B2;
            sourceToProjectTranslation = translation;
            sourceToProjectRotation = rotation;
            sourceToProjectScale = scale;
            sourceEntityCount = entityCount;
            rendererEntityCount = rendererCount;
            metadataOnlyEntityCount = metadataOnlyCount;
            semanticFingerprintSha256 = semanticFingerprint;
        }
    }
}
