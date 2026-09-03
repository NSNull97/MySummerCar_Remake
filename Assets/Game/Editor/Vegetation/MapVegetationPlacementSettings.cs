using System;
using System.Collections.Generic;
using MSC.Traffic;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    public enum MapVegetationKind { Tree, Shrub, Grass }

    public enum MapVegetationExclusionKind
    {
        None, AsphaltRoad, DirtRoad, Railway, Building, Door, Gate,
        GarageOpening, Driveway, Bridge, Water, AgriculturalField,
        InteractiveObject, VehicleRoute, OpenSpace, ArtificialStructure
    }

    [Serializable]
    public sealed class MapVegetationCategorySettings
    {
        [SerializeField] private GameObject[] prefabs = Array.Empty<GameObject>();
        [SerializeField, Range(0f, 89f)] private float maximumSlopeDegrees = 42f;
        [SerializeField] private float surfaceOffsetMeters;
        [SerializeField] private Vector2 scaleRange = new Vector2(0.92f, 1.08f);
        [SerializeField, Min(0.05f)] private float minimumSpacingMeters = 3f;
        [SerializeField, Range(0f, 1f)] private float density = 1f;
        [SerializeField, Range(0f, 1f)] private float normalAlignment = 0.15f;

        public IReadOnlyList<GameObject> Prefabs => prefabs ?? Array.Empty<GameObject>();
        public float MaximumSlopeDegrees => Mathf.Clamp(maximumSlopeDegrees, 0f, 89f);
        public float SurfaceOffsetMeters => surfaceOffsetMeters;
        public Vector2 ScaleRange => new Vector2(Mathf.Max(0.01f, scaleRange.x), Mathf.Max(Mathf.Max(0.01f, scaleRange.x), scaleRange.y));
        public Vector2 UniformScaleRange => ScaleRange;
        public float MinimumSpacingMeters => Mathf.Max(0.05f, minimumSpacingMeters);
        public float Density => Mathf.Clamp01(density);
        public float NormalAlignment => Mathf.Clamp01(normalAlignment);

        public MapVegetationCategorySettings() { }
        internal MapVegetationCategorySettings(float slope, float spacing, float alignment)
        {
            maximumSlopeDegrees = slope;
            minimumSpacingMeters = spacing;
            normalAlignment = alignment;
        }

        public void SetPrefabs(GameObject[] values) => prefabs = values ?? Array.Empty<GameObject>();
    }

    [Serializable]
    public sealed class MapVegetationClearanceRule
    {
        [SerializeField] private MapVegetationExclusionKind kind;
        [SerializeField, Min(0f)] private float treeMeters;
        [SerializeField, Min(0f)] private float shrubMeters;
        [SerializeField, Min(0f)] private float grassMeters;
        public MapVegetationExclusionKind Kind => kind;

        public MapVegetationClearanceRule(MapVegetationExclusionKind category, float tree, float shrub, float grass)
        { kind = category; treeMeters = tree; shrubMeters = shrub; grassMeters = grass; }

        public float For(MapVegetationKind vegetation) => Mathf.Max(0f,
            vegetation == MapVegetationKind.Tree ? treeMeters :
            vegetation == MapVegetationKind.Shrub ? shrubMeters : grassMeters);
    }

    /// <summary>Explicit material/layer/tag classification; an empty rule never matches.</summary>
    [Serializable]
    public sealed class MapVegetationSurfaceRule
    {
        [SerializeField] private string label = "Authored exclusion";
        [SerializeField] private LayerMask layers;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField] private Material[] materials = Array.Empty<Material>();
        [SerializeField] private MapVegetationExclusionKind category = MapVegetationExclusionKind.ArtificialStructure;
        public string Label => label;
        public MapVegetationExclusionKind Category => category;

        public bool Matches(GameObject target, Material material)
        {
            if (((1 << target.layer) & layers.value) != 0) return true;
            foreach (string tag in tags ?? Array.Empty<string>())
                if (!string.IsNullOrWhiteSpace(tag) && string.Equals(target.tag, tag, StringComparison.Ordinal)) return true;
            foreach (Material allowed in materials ?? Array.Empty<Material>())
                if (allowed != null && allowed == material) return true;
            return false;
        }
    }

    [Serializable]
    public sealed class MapVegetationAuthoredExclusion
    {
        [SerializeField] private string label = "Gameplay clearance";
        [SerializeField] private Bounds worldBounds = new Bounds(Vector3.zero, new Vector3(5f, 10f, 5f));
        [SerializeField] private MapVegetationExclusionKind category = MapVegetationExclusionKind.InteractiveObject;
        public string Label => label;
        public Bounds WorldBounds => worldBounds;
        public MapVegetationExclusionKind Category => category;
    }

    [CreateAssetMenu(menuName = "MSC/Vegetation/Map Placement Settings")]
    public sealed class MapVegetationPlacementSettings : ScriptableObject
    {
        [Header("Source Data")]
        [SerializeField] private string sourceGlobalScenePath = string.Empty;
        [SerializeField] private string[] sourceCellScenePaths = Array.Empty<string>();
        [SerializeField] private int seed = 20260831;
        [SerializeField, Min(1f)] private float cellSizeMeters = 512f;

        [Header("Coordinate Mapping — optional correction after the canonical import (identity by default)")]
        [SerializeField] private Vector3 donorTranslation;
        [SerializeField] private Vector3 donorRotationDegrees;
        [SerializeField] private Vector3 donorScale = Vector3.one;

        [Header("Surface Detection — deny rules win")]
        [SerializeField] private LayerMask allowedGroundLayers;
        [SerializeField] private LayerMask disallowedLayers;
        [SerializeField] private string[] allowedGroundTags = Array.Empty<string>();
        [SerializeField] private string[] disallowedTags = Array.Empty<string>();
        [SerializeField] private Material[] allowedGroundMaterials = Array.Empty<Material>();
        [SerializeField] private Material[] disallowedMaterials = Array.Empty<Material>();
        [Tooltip("Only these canonical donor terrain paths opt in without a marker. Texture colour can reject grass later, but never opts ground in.")]
        [SerializeField] private string[] canonicalNaturalGroundPaths =
        {
            "MAP/MESH/TERRAIN_OBJ/Grass1", "MAP/MESH/TERRAIN_OBJ/Grass2", "MAP/SkijumpHill/grass"
        };
        [Tooltip("Opt-in for an explicitly reviewed Terrain. Keep off for mixed agricultural/road terrain unless exclusions cover those regions.")]
        [SerializeField] private bool allowTerrainSurfaces;
        [SerializeField] private Vector2 worldHeightRange = new Vector2(-1000f, 2000f);
        [SerializeField, Min(1f)] private float spatialIndexCellMeters = 32f;
        [SerializeField] private MapVegetationSurfaceRule[] exclusionRules = Array.Empty<MapVegetationSurfaceRule>();
        [SerializeField] private MapVegetationAuthoredExclusion[] authoredExclusions = Array.Empty<MapVegetationAuthoredExclusion>();
        [SerializeField] private TrafficRoadNetworkCatalog trafficNetwork;
        [SerializeField, Min(0f)] private float trafficRouteHalfWidthMeters = 3f;

        [Header("Grass only — texture mask on already approved natural ground")]
        [SerializeField] private bool grassTextureMaskEnabled = true;
        [Tooltip("Require G - max(R,B) at least this value in the base colour texture (sRGB 0..1).")]
        [SerializeField, Range(0f, 1f)] private float grassTextureMinimumGreenExcess = 1f / 255f;
        [SerializeField, Range(0f, 1f)] private float grassTextureMinimumSaturation = 0.1f;
        [SerializeField, Range(0f, 1f)] private float grassTextureMinimumValue = 0.03f;
        [Tooltip("Expand authored green texels by this many source-texture pixels. This softens atlas seams without classifying brown texels as green.")]
        [SerializeField, Range(0, 8)] private int grassTextureGreenDilationPixels = 3;
        [Tooltip("Radius in source-texture pixels used to identify a locally green carpet region. Brown holes are filled only when green evidence surrounds them; isolated green speckles are suppressed.")]
        [SerializeField, Range(0, 32)] private int grassTextureCarpetRadiusPixels = 12;
        [Tooltip("Minimum fraction of deterministic neighbourhood samples that must be green before the point belongs to a continuous carpet region.")]
        [SerializeField, Range(0f, 1f)] private float grassTextureCarpetMinimumGreenFraction = 0.3f;
        [Tooltip("Require green evidence from this many quadrants. Three prevents a single green edge from bleeding grass into a broad brown surface.")]
        [SerializeField, Range(1, 4)] private int grassTextureCarpetMinimumSectors = 3;
        [Tooltip("Reject covered surfaces whose base texture/UV mapping cannot be sampled. Every unsupported binding is reported.")]
        [SerializeField] private bool grassTextureRejectUnsupported = true;
        [SerializeField] private string[] grassTextureCanonicalPaths =
        {
            "MAP/MESH/TERRAIN_OBJ/Grass1", "MAP/MESH/TERRAIN_OBJ/Grass2",
            "MAP/SkijumpHill/grass", "BetterMSC/MissingTerrain"
        };
        [Tooltip("Additional explicitly reviewed materials to mask. This does not opt a surface into natural ground.")]
        [SerializeField] private Material[] grassTextureMaterials = Array.Empty<Material>();

        [Header("Original Trees / Shrubs / Grass")]
        [SerializeField] private MapVegetationCategorySettings trees = new MapVegetationCategorySettings(42f, 1.5f, 0.15f);
        [SerializeField] private MapVegetationCategorySettings shrubs = new MapVegetationCategorySettings(48f, 2f, 0.35f);
        [SerializeField] private MapVegetationCategorySettings grass = new MapVegetationCategorySettings(52f, 1.5f, 0.8f);

        [Header("Clearance from geometry edges, in metres")]
        [SerializeField] private MapVegetationClearanceRule[] clearances = DefaultClearances();

        public int Seed => seed;
        public string SourceGlobalScenePath => sourceGlobalScenePath;
        public IReadOnlyList<string> SourceCellScenePaths => sourceCellScenePaths ?? Array.Empty<string>();
        public float CellSizeMeters => Mathf.Max(1f, cellSizeMeters);
        public float SpatialIndexCellMeters => Mathf.Max(1f, spatialIndexCellMeters);
        public Vector2 WorldHeightRange => worldHeightRange;
        public bool AllowTerrainSurfaces => allowTerrainSurfaces;
        public IReadOnlyList<MapVegetationAuthoredExclusion> AuthoredExclusions => authoredExclusions ?? Array.Empty<MapVegetationAuthoredExclusion>();
        public TrafficRoadNetworkCatalog TrafficNetwork => trafficNetwork;
        public float TrafficRouteHalfWidthMeters => Mathf.Max(0f, trafficRouteHalfWidthMeters);
        public bool GrassTextureMaskEnabled => grassTextureMaskEnabled;
        public float GrassTextureMinimumGreenExcess => Mathf.Clamp01(grassTextureMinimumGreenExcess);
        public float GrassTextureMinimumSaturation => Mathf.Clamp01(grassTextureMinimumSaturation);
        public float GrassTextureMinimumValue => Mathf.Clamp01(grassTextureMinimumValue);
        public int GrassTextureGreenDilationPixels => Mathf.Clamp(grassTextureGreenDilationPixels, 0, 8);
        public int GrassTextureCarpetRadiusPixels => Mathf.Clamp(grassTextureCarpetRadiusPixels, 0, 32);
        public float GrassTextureCarpetMinimumGreenFraction =>
            Mathf.Clamp01(grassTextureCarpetMinimumGreenFraction);
        public int GrassTextureCarpetMinimumSectors =>
            Mathf.Clamp(grassTextureCarpetMinimumSectors, 1, 4);
        public bool GrassTextureRejectUnsupported => grassTextureRejectUnsupported;
        public IReadOnlyList<string> GrassTextureCanonicalPaths => grassTextureCanonicalPaths ?? Array.Empty<string>();
        public IReadOnlyList<Material> GrassTextureMaterials => grassTextureMaterials ?? Array.Empty<Material>();
        public Matrix4x4 DonorCoordinateMatrix => Matrix4x4.TRS(donorTranslation, Quaternion.Euler(donorRotationDegrees), donorScale);
        public Vector3 DonorToWorld(Vector3 point) => DonorCoordinateMatrix.MultiplyPoint3x4(point);
        public MapVegetationCategorySettings Category(MapVegetationKind kind) => kind == MapVegetationKind.Tree ? trees : kind == MapVegetationKind.Shrub ? shrubs : grass;

        public float GetMargin(MapVegetationKind vegetation, MapVegetationExclusionKind exclusion)
        {
            foreach (MapVegetationClearanceRule rule in clearances ?? Array.Empty<MapVegetationClearanceRule>())
                if (rule != null && rule.Kind == exclusion) return rule.For(vegetation);
            return 0f;
        }

        internal float MaximumMargin(MapVegetationExclusionKind exclusion) => Mathf.Max(
            GetMargin(MapVegetationKind.Tree, exclusion),
            Mathf.Max(GetMargin(MapVegetationKind.Shrub, exclusion), GetMargin(MapVegetationKind.Grass, exclusion)));

        internal bool IsAllowedGround(GameObject target, Material material, string canonicalPath)
        {
            if (Matches(target, material, allowedGroundLayers, allowedGroundTags, allowedGroundMaterials)) return true;
            foreach (string path in canonicalNaturalGroundPaths ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(path) && string.Equals(path, canonicalPath, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        internal MapVegetationExclusionKind ConfiguredExclusion(GameObject target, Material material)
        {
            foreach (MapVegetationSurfaceRule rule in exclusionRules ?? Array.Empty<MapVegetationSurfaceRule>())
                if (rule != null && rule.Matches(target, material)) return rule.Category;
            return Matches(target, material, disallowedLayers, disallowedTags, disallowedMaterials)
                ? MapVegetationExclusionKind.ArtificialStructure : MapVegetationExclusionKind.None;
        }

        private static bool Matches(GameObject target, Material material, LayerMask layers, string[] tags, Material[] materials)
        {
            if ((layers.value & (1 << target.layer)) != 0) return true;
            foreach (string tag in tags ?? Array.Empty<string>())
                if (!string.IsNullOrEmpty(tag) && string.Equals(tag, target.tag, StringComparison.Ordinal)) return true;
            foreach (Material candidate in materials ?? Array.Empty<Material>())
                if (candidate != null && candidate == material) return true;
            return false;
        }

        private static MapVegetationClearanceRule[] DefaultClearances() => new[]
        {
            new MapVegetationClearanceRule(MapVegetationExclusionKind.AsphaltRoad, 4.75f, 1.5f, 0.55f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.DirtRoad, 4.75f, 1.5f, 0.55f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.Railway, 5f, 3f, 1f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.Building, 3.5f, 1f, 0.55f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.Door, 4f, 2f, 0.5f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.Gate, 5f, 2f, 0.5f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.GarageOpening, 6f, 3f, 0.75f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.Driveway, 4f, 1.5f, 0.55f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.Bridge, 4.75f, 2f, 0.5f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.Water, 3.5f, 1f, 0.35f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.AgriculturalField, 3f, 1f, 0.1f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.InteractiveObject, 3f, 1.5f, 0.35f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.VehicleRoute, 2f, 1f, 0.1f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.OpenSpace, 2f, 1f, 0f),
            new MapVegetationClearanceRule(MapVegetationExclusionKind.ArtificialStructure, 3f, 1f, 0.55f)
        };
    }
}
