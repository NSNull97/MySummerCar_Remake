using UnityEngine;

namespace MSC.World.Remaster
{
    [DisallowMultipleComponent]
    public sealed class WorldRemasterPilotMarker : MonoBehaviour
    {
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private Bounds localBounds;
        [SerializeField] private Vector3 worldGarageAnchor;
        [SerializeField] private int productionGroupCount;
        [SerializeField] private int mappedReferenceRecordCount;
        [SerializeField] private int totalReferenceRecordCount;
        [SerializeField] private bool donorBinaryIndependent = true;
        [SerializeField] private bool manualVisualValidationPending = true;

        public int SchemaVersion => schemaVersion;
        public string ZoneId => zoneId;
        public Bounds LocalBounds => localBounds;
        public Vector3 WorldGarageAnchor => worldGarageAnchor;
        public int ProductionGroupCount => productionGroupCount;
        public int MappedReferenceRecordCount => mappedReferenceRecordCount;
        public int TotalReferenceRecordCount => totalReferenceRecordCount;
        public bool DonorBinaryIndependent => donorBinaryIndependent;
        public bool ManualVisualValidationPending => manualVisualValidationPending;

        public void Configure(
            string pilotZone,
            Bounds bounds,
            Vector3 garageAnchor,
            int groupCount,
            int mappedCount,
            int referenceCount,
            bool manualPending)
        {
            schemaVersion = CurrentSchemaVersion;
            zoneId = pilotZone ?? string.Empty;
            localBounds = bounds;
            worldGarageAnchor = garageAnchor;
            productionGroupCount = Mathf.Max(0, groupCount);
            mappedReferenceRecordCount = Mathf.Max(0, mappedCount);
            totalReferenceRecordCount = Mathf.Max(0, referenceCount);
            donorBinaryIndependent = true;
            manualVisualValidationPending = manualPending;
        }
    }
}
