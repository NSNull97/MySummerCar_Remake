using System;
using UnityEngine;

namespace MSC.World.Remaster
{
    public enum WorldVoidRegionClassification
    {
        UnknownGap,
        IntentionalDonorVoid,
        WaterOrShoreline,
        ExternalWorldBoundary
    }

    /// <summary>
    /// Explicit metadata for a project-authored safety/topology fill. It does
    /// not make the donor out-of-bounds area a supported gameplay zone.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldVoidFillMarker : MonoBehaviour
    {
        [SerializeField] private string regionId = string.Empty;
        [SerializeField] private string pieceId = string.Empty;
        [SerializeField] private WorldVoidRegionClassification classification;
        [SerializeField] private string cellId = string.Empty;
        [SerializeField] private string authoringFingerprintSha256 = string.Empty;
        [SerializeField] private string boundaryEvidenceStableIds = string.Empty;
        [SerializeField] private bool safetyTopologyBaseline = true;
        [SerializeField] private bool opensGameplayArea;

        public string RegionId => regionId;
        public string PieceId => pieceId;
        public WorldVoidRegionClassification Classification => classification;
        public string CellId => cellId;
        public string AuthoringFingerprintSha256 => authoringFingerprintSha256;
        public string BoundaryEvidenceStableIds => boundaryEvidenceStableIds;
        public bool SafetyTopologyBaseline => safetyTopologyBaseline;
        public bool OpensGameplayArea => opensGameplayArea;

        public void Configure(
            string id,
            string regionPieceId,
            WorldVoidRegionClassification regionClassification,
            string ownerCellId,
            string fingerprint,
            string evidenceStableIds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("Void region ID is required.", nameof(id));
            }

            regionId = id;
            pieceId = regionPieceId ?? string.Empty;
            classification = regionClassification;
            cellId = ownerCellId ?? string.Empty;
            authoringFingerprintSha256 = fingerprint ?? string.Empty;
            boundaryEvidenceStableIds = evidenceStableIds ?? string.Empty;
            safetyTopologyBaseline = true;
            opensGameplayArea = false;
        }
    }
}
