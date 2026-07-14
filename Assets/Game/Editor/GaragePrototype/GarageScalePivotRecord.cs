using UnityEngine;

namespace MSC.Editor.GaragePrototype
{
    public sealed class GarageScalePivotRecord : ScriptableObject
    {
        [SerializeField] private string sourceObject = string.Empty;
        [SerializeField] private string sourceContainerSha256 = string.Empty;
        [SerializeField] private string normalizedReferenceSha256 = string.Empty;
        [SerializeField] private Vector3 donorBoundsMinMeters;
        [SerializeField] private Vector3 donorBoundsMaxMeters;
        [SerializeField] private Vector3 referencePivotMeters;
        [SerializeField] private Vector3 productionBoundsMinMeters;
        [SerializeField] private Vector3 productionBoundsMaxMeters;
        [SerializeField] private Vector3 productionPivotMeters;
        [SerializeField] private float dimensionalToleranceMeters;
        [SerializeField] private GameObject productionRoofPrefab = null;
        [SerializeField, TextArea] private string coordinateMapping = string.Empty;
        [SerializeField, TextArea] private string knownDifferences = string.Empty;

        public string SourceObject => sourceObject;
        public string SourceContainerSha256 => sourceContainerSha256;
        public string NormalizedReferenceSha256 => normalizedReferenceSha256;
        public Vector3 DonorBoundsMinMeters => donorBoundsMinMeters;
        public Vector3 DonorBoundsMaxMeters => donorBoundsMaxMeters;
        public Vector3 ReferencePivotMeters => referencePivotMeters;
        public Vector3 ProductionBoundsMinMeters => productionBoundsMinMeters;
        public Vector3 ProductionBoundsMaxMeters => productionBoundsMaxMeters;
        public Vector3 ProductionPivotMeters => productionPivotMeters;
        public float DimensionalToleranceMeters => dimensionalToleranceMeters;
        public GameObject ProductionRoofPrefab => productionRoofPrefab;
        public string CoordinateMapping => coordinateMapping;
        public string KnownDifferences => knownDifferences;

        public float PivotDeltaMeters =>
            Vector3.Distance(referencePivotMeters, productionPivotMeters);

        public float MaximumBoundsDeltaMeters
        {
            get
            {
                Vector3 expectedMin = MapDonorToProduction(donorBoundsMinMeters);
                Vector3 expectedMax = MapDonorToProduction(donorBoundsMaxMeters);
                return Mathf.Max(
                    MaximumComponentDelta(expectedMin, productionBoundsMinMeters),
                    MaximumComponentDelta(expectedMax, productionBoundsMaxMeters));
            }
        }

        public bool IsWithinTolerance =>
            PivotDeltaMeters <= dimensionalToleranceMeters &&
            MaximumBoundsDeltaMeters <= dimensionalToleranceMeters;

        internal void Configure(GameObject roofPrefab, Bounds productionBounds)
        {
            sourceObject = "garage_shed_roof (Mesh PathID 2186; level2 GameObject PathID 1064)";
            sourceContainerSha256 =
                "1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684";
            normalizedReferenceSha256 =
                "ea1c43d48a1b65ccee1bb616626e82cb89b2a79e12ba9ce1e2dbe7f96f1814ed";
            donorBoundsMinMeters = GaragePrototypePaths.DonorRoofBoundsMin;
            donorBoundsMaxMeters = GaragePrototypePaths.DonorRoofBoundsMax;
            referencePivotMeters = Vector3.zero;
            productionBoundsMinMeters = productionBounds.min;
            productionBoundsMaxMeters = productionBounds.max;
            productionPivotMeters = Vector3.zero;
            dimensionalToleranceMeters = GaragePrototypePaths.DimensionalToleranceMeters;
            productionRoofPrefab = roofPrefab;
            coordinateMapping =
                "Donor local (X,Y,Z) maps to production y-up (X,Z,Y). Units are treated as metres; prefab root remains the donor-local zero pivot.";
            knownDifferences =
                "Only the roof bounds and pivot are validated donor measurements. Wall openings, interior furniture, terrain and the 180 m road curve are independent Milestone 3 reconstruction choices and are not claimed as exact donor layout.";
        }

        private static Vector3 MapDonorToProduction(Vector3 donor)
        {
            return new Vector3(donor.x, donor.z, donor.y);
        }

        private static float MaximumComponentDelta(Vector3 left, Vector3 right)
        {
            Vector3 delta = left - right;
            return Mathf.Max(Mathf.Abs(delta.x), Mathf.Abs(delta.y), Mathf.Abs(delta.z));
        }
    }
}
