using System.Collections.Generic;
using UnityEngine;

namespace MSC.LegacyImport.Editor.Proof
{
    [CreateAssetMenu(
        fileName = "ReauthoredAssetProvenance",
        menuName = "My Summer Car/Legacy Import/Reauthored Asset Provenance")]
    public sealed class ReauthoredAssetProvenance : ScriptableObject
    {
        [SerializeField] private DonorAssetRegistry registry = null;
        [SerializeField] private string donorRecordId = string.Empty;
        [SerializeField] private DonorProofRole role;
        [SerializeField] private GameObject productionPrefab = null;
        [SerializeField] private Vector3 referencePivotMeters = Vector3.zero;
        [SerializeField] private Vector3 productionPivotMeters = Vector3.zero;
        [SerializeField, Min(0.0001f)] private float dimensionalToleranceMeters = 0.005f;
        [SerializeField] private List<MountPointComparison> mountPoints = new List<MountPointComparison>();
        [SerializeField] private List<Texture2D> authoredTextures = new List<Texture2D>();
        [SerializeField, TextArea] private string authoringNotes = string.Empty;

        public DonorAssetRegistry Registry => registry;
        public string DonorRecordId => donorRecordId;
        public DonorProofRole Role => role;
        public GameObject ProductionPrefab => productionPrefab;
        public Vector3 ReferencePivotMeters => referencePivotMeters;
        public Vector3 ProductionPivotMeters => productionPivotMeters;
        public float DimensionalToleranceMeters => dimensionalToleranceMeters;
        public IReadOnlyList<MountPointComparison> MountPoints => mountPoints;
        public IReadOnlyList<Texture2D> AuthoredTextures => authoredTextures;
        public string AuthoringNotes => authoringNotes;
    }
}
