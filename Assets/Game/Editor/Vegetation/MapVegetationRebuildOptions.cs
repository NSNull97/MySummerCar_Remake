using System;
using MSC.World.Vegetation;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    [Flags]
    public enum MapVegetationCategories
    {
        None = 0, OriginalTrees = 1, BoundaryForest = 2,
        GrassCoverage = 4, ShrubsAndUndergrowth = 8, All = 15
    }

    [CreateAssetMenu(menuName = "MSC/World/Vegetation/Map Rebuild Options")]
    public sealed class MapVegetationRebuildOptions : ScriptableObject
    {
        public const string AssetPath = "Assets/Game/Editor/Vegetation/MapVegetationRebuildOptions.asset";
        public const string GeneratedRoot = "Assets/Game/LegacyImport/RuntimeBaseline/VegetationRebuild";
        public const string GeneratorId = "msc.map-vegetation-rebuild.v1";

        [SerializeField] private MapVegetationPlacementSettings placement;
        [SerializeField] private MapVegetationCategories categories = MapVegetationCategories.All;
        [SerializeField] private string selectedCell = "cell_0_0";
        [SerializeField] private VegetationProfile[] grassProfiles = Array.Empty<VegetationProfile>();
        [SerializeField] private Material[] materialOverrides = Array.Empty<Material>();
        [SerializeField, Range(0f, 100f)] private float sprucePercent = 65f;
        [SerializeField, Range(0f, 100f)] private float pinePercent = 20f;
        [SerializeField, Range(0f, 100f)] private float birchPercent = 7.5f;
        [SerializeField, Range(0f, 100f)] private float aspenPercent = 7.5f;
        [SerializeField, Min(1f)] private float boundaryDepthMeters = 75f;
        [SerializeField] private Vector2 boundaryFrontHeightRange = new Vector2(4.5f, 12f);
        [SerializeField] private Vector2 boundaryBackHeightRange = new Vector2(12f, 22f);
        [SerializeField, Min(1f)] private float boundaryCandidateSpacingMeters = 5f;
        [SerializeField, Range(0f, 1f)] private float boundaryFrontDensity = 0.35f;
        [SerializeField, Range(0f, 1f)] private float boundaryBackDensity = 0.95f;
        [SerializeField, Min(1)] private int boundaryMaximumTrees = 12000;
        [SerializeField, Min(1)] private int boundaryMaximumForestFloor = 8000;
        [SerializeField, Min(4f)] private float naturalInfillSpacingMeters = 8.5f;
        [SerializeField, Range(0f, 1f)] private float naturalInfillDensity = 0.94f;
        [SerializeField, Min(1f)] private float naturalInfillMinimumTreeDistanceMeters = 5.75f;
        [SerializeField, Min(1f)] private float naturalInfillForestInfluenceMeters = 72f;
        [SerializeField, Range(3, 12)] private int naturalInfillMinimumDonorTrees = 3;
        [SerializeField, Range(6, 16)] private int naturalInfillAngularSectorCount = 12;
        [SerializeField, Range(90f, 180f)] private float naturalInfillMaximumEmptyArcDegrees = 180f;
        [SerializeField, Min(8f)] private float naturalInfillClusterScaleMeters = 48f;
        [SerializeField] private Vector2 naturalInfillHeightRange = new Vector2(7f, 20f);
        [SerializeField, Range(0f, 1f)] private float naturalInfillMaximumOriginalFraction = 0.65f;
        [SerializeField, Min(10f)] private float distantForestMinimumBoundaryDistanceMeters = 95f;
        [SerializeField, Min(20f)] private float distantForestDepthMeters = 650f;
        [SerializeField, Min(4f)] private float distantForestSpacingMeters = 15f;
        [SerializeField, Range(0f, 1f)] private float distantForestDensity = 0.72f;
        [SerializeField] private Vector2 distantForestHeightRange = new Vector2(11f, 24f);
        [SerializeField, Min(1)] private int distantForestMaximumTrees = 16000;
        [SerializeField, Min(1)] private int distantForestMaximumRenderers = 512;
        [SerializeField, Min(4)] private int distantForestMaximumVertices = 2000000;
        [SerializeField, Min(4)] private int distantForestMaximumVerticesPerBatch = 250000;
        [SerializeField, Min(4)] private int distantForestMaximumVerticesPerScene = 500000;
        [SerializeField, Min(1)] private int distantForestMaximumScenes = 128;
        [SerializeField, Range(1, 4)] private int distantForestLoadingRadiusCells = 2;
        [SerializeField, Min(1)] private int canonicalRockMaximumCount = 128;
        [SerializeField, Min(0.2f)] private float grassSpacingMeters = 0.5f;
        [SerializeField, Range(0f, 1f)] private float grassDensity = 0.96f;
        [SerializeField, Min(1f)] private float undergrowthSpacingMeters = 9f;
        [SerializeField, Range(0f, 1f)] private float undergrowthDensity = 0.3f;
        [SerializeField] private bool createTreeColliders = true;
        [SerializeField] private bool createBoundaryColliders;
        [SerializeField, Range(1, 4)] private int forestLoadingRadiusCells = 1;
        [SerializeField, Min(100)] private int maximumPreviewSamples = 8000;
        [SerializeField] private bool showPreview = true;
        [SerializeField] private bool showExclusions = true;

        public MapVegetationPlacementSettings Placement => placement;
        public MapVegetationCategories Categories => categories;
        public string SelectedCell => selectedCell;
        public VegetationProfile[] GrassProfiles => grassProfiles;
        public Material[] MaterialOverrides => materialOverrides;
        public float[] TreeSpeciesPercentages => new[] { sprucePercent, pinePercent, birchPercent, aspenPercent };
        public float BoundaryDepthMeters => boundaryDepthMeters;
        public Vector2 BoundaryFrontHeightRange => boundaryFrontHeightRange;
        public Vector2 BoundaryBackHeightRange => boundaryBackHeightRange;
        public float BoundaryCandidateSpacingMeters => boundaryCandidateSpacingMeters;
        public float BoundaryFrontDensity => boundaryFrontDensity;
        public float BoundaryBackDensity => boundaryBackDensity;
        public int BoundaryMaximumTrees => boundaryMaximumTrees;
        public int BoundaryMaximumForestFloor => boundaryMaximumForestFloor;
        public float NaturalInfillSpacingMeters => naturalInfillSpacingMeters;
        public float NaturalInfillDensity => naturalInfillDensity;
        public float NaturalInfillMinimumTreeDistanceMeters =>
            naturalInfillMinimumTreeDistanceMeters;
        public float NaturalInfillForestInfluenceMeters =>
            naturalInfillForestInfluenceMeters;
        public int NaturalInfillMinimumDonorTrees =>
            naturalInfillMinimumDonorTrees;
        public int NaturalInfillAngularSectorCount =>
            naturalInfillAngularSectorCount;
        public float NaturalInfillMaximumEmptyArcDegrees =>
            naturalInfillMaximumEmptyArcDegrees;
        public float NaturalInfillClusterScaleMeters =>
            naturalInfillClusterScaleMeters;
        public Vector2 NaturalInfillHeightRange => naturalInfillHeightRange;
        public float NaturalInfillMaximumOriginalFraction =>
            naturalInfillMaximumOriginalFraction;
        public float DistantForestMinimumBoundaryDistanceMeters =>
            distantForestMinimumBoundaryDistanceMeters;
        public float DistantForestDepthMeters => distantForestDepthMeters;
        public float DistantForestSpacingMeters => distantForestSpacingMeters;
        public float DistantForestDensity => distantForestDensity;
        public Vector2 DistantForestHeightRange => distantForestHeightRange;
        public int DistantForestMaximumTrees => distantForestMaximumTrees;
        public int DistantForestMaximumRenderers => distantForestMaximumRenderers;
        public int DistantForestMaximumVertices => distantForestMaximumVertices;
        public int DistantForestMaximumVerticesPerBatch =>
            distantForestMaximumVerticesPerBatch;
        public int DistantForestMaximumVerticesPerScene =>
            distantForestMaximumVerticesPerScene;
        public int DistantForestMaximumScenes => distantForestMaximumScenes;
        public int DistantForestLoadingRadiusCells =>
            distantForestLoadingRadiusCells;
        public int CanonicalRockMaximumCount => canonicalRockMaximumCount;
        public float GrassSpacingMeters => grassSpacingMeters;
        public float GrassDensity => grassDensity;
        public float UndergrowthSpacingMeters => undergrowthSpacingMeters;
        public float UndergrowthDensity => undergrowthDensity;
        public bool CreateTreeColliders => createTreeColliders;
        public bool CreateBoundaryColliders => createBoundaryColliders;
        public int ForestLoadingRadiusCells => forestLoadingRadiusCells;
        public int MaximumPreviewSamples => maximumPreviewSamples;
        public bool ShowPreview => showPreview;
        public bool ShowExclusions => showExclusions;

        public void Initialize(MapVegetationPlacementSettings settings, VegetationProfile[] profiles)
        {
            placement = settings;
            grassProfiles = profiles;
        }

        public string Validate()
        {
            if (placement == null) return "Placement settings are missing.";
            float treeTotal = 0f;
            foreach (float share in TreeSpeciesPercentages)
            {
                if (!float.IsFinite(share) || share < 0f || share > 100f) return "Tree percentages must be finite values from 0 to 100.";
                treeTotal += share;
            }
            if (Mathf.Abs(treeTotal - 100f) > .001f) return "Tree species percentages must total 100.";
            if (!float.IsFinite(boundaryDepthMeters) || boundaryDepthMeters < 10f || boundaryDepthMeters > 300f)
                return "Boundary depth must be finite and between 10 and 300 metres.";
            foreach (Vector2 heights in new[] { boundaryFrontHeightRange, boundaryBackHeightRange })
                if (!float.IsFinite(heights.x) || !float.IsFinite(heights.y) || heights.x < 1f || heights.y < heights.x || heights.y > 35f)
                    return "Boundary tree heights must be finite increasing ranges within 1–35 metres.";
            if (!float.IsFinite(grassSpacingMeters) || grassSpacingMeters < 0.2f)
                return "Grass spacing must be finite and at least 0.2 metres.";
            if (!float.IsFinite(boundaryCandidateSpacingMeters) || boundaryCandidateSpacingMeters < 1f)
                return "Boundary spacing must be finite and at least one metre.";
            if (boundaryMaximumTrees < 1 || boundaryMaximumForestFloor < 1)
                return "Near boundary tree and forest-floor budgets must be positive.";
            if (!float.IsFinite(naturalInfillSpacingMeters) || naturalInfillSpacingMeters < 4f)
                return "Natural infill spacing must be finite and at least four metres.";
            if (!float.IsFinite(naturalInfillMinimumTreeDistanceMeters) ||
                naturalInfillMinimumTreeDistanceMeters < 1f ||
                !float.IsFinite(naturalInfillForestInfluenceMeters) ||
                naturalInfillForestInfluenceMeters <= naturalInfillMinimumTreeDistanceMeters)
                return "Natural infill influence must exceed its finite positive tree clearance.";
            if (naturalInfillMinimumDonorTrees < 3 ||
                naturalInfillMinimumDonorTrees > 12 ||
                naturalInfillAngularSectorCount < 6 ||
                naturalInfillAngularSectorCount > 16 ||
                !float.IsFinite(naturalInfillMaximumEmptyArcDegrees) ||
                naturalInfillMaximumEmptyArcDegrees < 90f ||
                naturalInfillMaximumEmptyArcDegrees > 180f)
                return "Natural infill forest evidence must use 3-12 donor trees, 6-16 angular sectors and a finite 90-180 degree maximum empty arc.";
            if (!float.IsFinite(naturalInfillClusterScaleMeters) ||
                naturalInfillClusterScaleMeters < 8f)
                return "Natural infill cluster scale must be finite and at least eight metres.";
            if (!ValidTreeHeightRange(naturalInfillHeightRange))
                return "Natural infill heights must be finite increasing values within 1–35 metres.";
            if (!float.IsFinite(naturalInfillMaximumOriginalFraction) ||
                naturalInfillMaximumOriginalFraction < 0f ||
                naturalInfillMaximumOriginalFraction > 1f)
                return "Natural infill maximum fraction must be finite and between zero and one.";
            if (!float.IsFinite(distantForestMinimumBoundaryDistanceMeters) ||
                distantForestMinimumBoundaryDistanceMeters < 10f ||
                !float.IsFinite(distantForestDepthMeters) ||
                distantForestDepthMeters <= distantForestMinimumBoundaryDistanceMeters ||
                distantForestDepthMeters > 1500f)
                return "Distant forest depth must exceed its finite 10 m+ boundary offset and stay within 1500 metres.";
            if (!float.IsFinite(distantForestSpacingMeters) || distantForestSpacingMeters < 4f)
                return "Distant forest spacing must be finite and at least four metres.";
            if (!ValidTreeHeightRange(distantForestHeightRange))
                return "Distant forest heights must be finite increasing values within 1–35 metres.";
            if (distantForestMaximumTrees < 1 ||
                distantForestMaximumRenderers < 1 ||
                distantForestMaximumVertices < 4 ||
                distantForestMaximumVerticesPerBatch < 4 ||
                distantForestMaximumVerticesPerScene < 4 ||
                distantForestMaximumScenes < 1 ||
                distantForestMaximumVerticesPerBatch >
                    distantForestMaximumVerticesPerScene ||
                distantForestMaximumVerticesPerScene >
                    distantForestMaximumVertices)
                return "Distant forest tree/renderer/scene/vertex budgets must be positive; per-renderer vertices must not exceed the per-scene budget, and the per-scene budget must not exceed the total.";
            if (distantForestLoadingRadiusCells < 1 ||
                distantForestLoadingRadiusCells > 4)
                return "Distant forest loading radius must be between one and four cells.";
            if (canonicalRockMaximumCount < 1)
                return "Canonical rock count budget must be positive.";
            if (!float.IsFinite(undergrowthSpacingMeters) || undergrowthSpacingMeters < 1f)
                return "Undergrowth spacing must be finite and at least one metre.";
            foreach (float density in new[] { boundaryFrontDensity, boundaryBackDensity,
                         naturalInfillDensity, distantForestDensity, grassDensity,
                         undergrowthDensity })
                if (!float.IsFinite(density) || density < 0f || density > 1f) return "Densities must be finite values between zero and one.";
            if (boundaryBackDensity < boundaryFrontDensity) return "Rear boundary density must not be lower than front density.";
            if (forestLoadingRadiusCells < 1 || forestLoadingRadiusCells > 4) return "Forest loading radius must be between one and four cells.";
            foreach (MapVegetationKind kind in Enum.GetValues(typeof(MapVegetationKind)))
            {
                MapVegetationCategorySettings category = placement.Category(kind);
                if (category == null || !float.IsFinite(category.SurfaceOffsetMeters) ||
                    !float.IsFinite(category.ScaleRange.x) || !float.IsFinite(category.ScaleRange.y) ||
                    !float.IsFinite(category.MinimumSpacingMeters) || !float.IsFinite(category.MaximumSlopeDegrees) ||
                    !float.IsFinite(category.Density) || !float.IsFinite(category.NormalAlignment))
                    return "Vegetation category settings contain a non-finite value.";
            }
            if (grassProfiles == null || grassProfiles.Length == 0) return "Existing grass profiles are required.";
            var channels = new System.Collections.Generic.HashSet<VegetationDensityChannel>();
            foreach (VegetationProfile profile in grassProfiles)
            {
                if (profile == null || profile.ValidateConfiguration().Count > 0)
                    return "A grass profile is missing or invalid.";
                if (!channels.Add(profile.DensityChannel)) return "Use one profile per density channel; edit its existing mesh/LOD variants for visual changes.";
            }
            return string.Empty;
        }

        private static bool ValidTreeHeightRange(Vector2 range) =>
            float.IsFinite(range.x) && float.IsFinite(range.y) &&
            range.x >= 1f && range.y >= range.x && range.y <= 35f;
    }
}
