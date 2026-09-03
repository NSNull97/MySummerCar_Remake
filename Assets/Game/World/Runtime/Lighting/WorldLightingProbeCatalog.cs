using System;
using System.Collections.Generic;
using MSC.Core.Time;
using UnityEngine;

namespace MSC.World.Lighting
{
    public enum WorldLightActivationPolicy
    {
        AlwaysOn = 0,
        NightOnly = 1,
    }

    public enum WorldLightSourceKind
    {
        Point = 0,
        Spot = 1,
    }

    public enum WorldLightBakeMode
    {
        Realtime = 0,
        Baked = 1,
        Mixed = 2,
    }

    [Serializable]
    public sealed class WorldLightDefinition
    {
        [SerializeField] private string lightId = string.Empty;
        [SerializeField] private string cellId = string.Empty;
        [SerializeField] private string donorReferencePath = string.Empty;
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 rotationEulerAngles =
            new Vector3(90f, 0f, 0f);
        [SerializeField] private Color color = Color.white;
        [SerializeField] private float intensity = 850f;
        [SerializeField] private float range = 10f;
        [SerializeField] private float spotInnerAngle = 72f;
        [SerializeField] private float spotOuterAngle = 105f;
        [SerializeField] private bool intensityIsLux;
        [SerializeField] private float luxAtDistance = 1f;
        [SerializeField] private float colorTemperatureKelvin;
        [SerializeField] private float indirectMultiplier = 1f;
        [SerializeField] private float shapeRadius = 0.025f;
        [SerializeField] private Vector2 areaSize;
        [SerializeField] private bool suppressGeneratedEmission;
        [SerializeField] private bool volumetricEnabled = true;
        [SerializeField] private float volumetricDimmer = 1f;
        [SerializeField] private WorldLightSourceKind sourceKind;
        [SerializeField] private WorldLightBakeMode bakeMode;
        [SerializeField] private WorldLightActivationPolicy activationPolicy;
        [SerializeField] private bool castsShadows;

        public string LightId => lightId;
        public string CellId => cellId;
        public string DonorReferencePath => donorReferencePath;
        public Vector3 Position => position;
        public Vector3 RotationEulerAngles => rotationEulerAngles;
        public Color Color => color;
        public float Intensity => intensity;
        public float Range => range;
        public float SpotInnerAngle => spotInnerAngle;
        public float SpotOuterAngle => spotOuterAngle;
        public bool IntensityIsLux => intensityIsLux;
        public float LuxAtDistance => luxAtDistance;
        public float ColorTemperatureKelvin => colorTemperatureKelvin;
        public float IndirectMultiplier => indirectMultiplier;
        public float ShapeRadius => shapeRadius;
        public Vector2 AreaSize => areaSize;
        public bool HasAreaSizeOverride =>
            areaSize.x > 0f && areaSize.y > 0f;
        public bool SuppressGeneratedEmission => suppressGeneratedEmission;
        public bool VolumetricEnabled => volumetricEnabled;
        public float VolumetricDimmer => volumetricDimmer;
        public WorldLightSourceKind SourceKind => sourceKind;
        public WorldLightBakeMode BakeMode => bakeMode;
        public WorldLightActivationPolicy ActivationPolicy => activationPolicy;
        public bool CastsShadows => castsShadows;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string id,
            string ownerCellId,
            Vector3 worldPosition,
            Vector3 worldRotationEulerAngles,
            Color lightColor,
            float lightIntensity,
            float lightRange,
            float innerSpotAngle,
            float outerSpotAngle,
            bool useLux,
            float configuredLuxAtDistance,
            float configuredColorTemperatureKelvin,
            float configuredIndirectMultiplier,
            float configuredShapeRadius,
            bool configuredVolumetricEnabled,
            float configuredVolumetricDimmer,
            WorldLightSourceKind configuredSourceKind,
            WorldLightBakeMode configuredBakeMode,
            WorldLightActivationPolicy policy,
            bool shadowCasting)
        {
            lightId = id;
            cellId = ownerCellId;
            position = worldPosition;
            rotationEulerAngles = worldRotationEulerAngles;
            color = lightColor;
            intensity = lightIntensity;
            range = lightRange;
            spotInnerAngle = innerSpotAngle;
            spotOuterAngle = outerSpotAngle;
            intensityIsLux = useLux;
            luxAtDistance = configuredLuxAtDistance;
            colorTemperatureKelvin = configuredColorTemperatureKelvin;
            indirectMultiplier = configuredIndirectMultiplier;
            shapeRadius = configuredShapeRadius;
            volumetricEnabled = configuredVolumetricEnabled;
            volumetricDimmer = configuredVolumetricDimmer;
            sourceKind = configuredSourceKind;
            bakeMode = configuredBakeMode;
            activationPolicy = policy;
            castsShadows = shadowCasting;
        }

        public void SetDonorReferencePathForAuthoring(string referencePath)
        {
            donorReferencePath = referencePath ?? string.Empty;
        }

        public void SetAreaLightOverridesForAuthoring(
            Vector2 configuredAreaSize,
            bool suppressEmission)
        {
            areaSize = configuredAreaSize;
            suppressGeneratedEmission = suppressEmission;
        }
#endif
    }

    [Serializable]
    public sealed class WorldReflectionProbeDefinition
    {
        [SerializeField] private string probeId = string.Empty;
        [SerializeField] private string cellId = string.Empty;
        [SerializeField] private Vector3 position;
        [SerializeField] private Vector3 size = new Vector3(12f, 6f, 12f);
        [SerializeField] private float intensity = 0.7f;
        [SerializeField] private int resolution = 128;

        public string ProbeId => probeId;
        public string CellId => cellId;
        public Vector3 Position => position;
        public Vector3 Size => size;
        public float Intensity => intensity;
        public int Resolution => resolution;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string id,
            string ownerCellId,
            Vector3 worldPosition,
            Vector3 probeSize,
            float probeIntensity,
            int probeResolution)
        {
            probeId = id;
            cellId = ownerCellId;
            position = worldPosition;
            size = probeSize;
            intensity = probeIntensity;
            resolution = probeResolution;
        }
#endif
    }

    [CreateAssetMenu(
        menuName = "MSC/World/World Lighting Probe Catalog",
        fileName = "WorldLightingProbeCatalog")]
    public sealed class WorldLightingProbeCatalog : ScriptableObject
    {
        [SerializeField] private string catalogId =
            "world-lighting.phase1-fixtures.v1";
        [SerializeField] private List<WorldLightDefinition> lights =
            new List<WorldLightDefinition>();
        [SerializeField] private List<WorldReflectionProbeDefinition> probes =
            new List<WorldReflectionProbeDefinition>();

        public string CatalogId => catalogId;
        public IReadOnlyList<WorldLightDefinition> Lights => lights;
        public IReadOnlyList<WorldReflectionProbeDefinition> Probes => probes;

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(catalogId))
            {
                throw new InvalidOperationException(
                    "World lighting catalog ID is missing.");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < lights.Count; index++)
            {
                WorldLightDefinition definition = lights[index];
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.LightId) ||
                    string.IsNullOrWhiteSpace(definition.CellId) ||
                    !ids.Add(definition.LightId) ||
                    !IsFinite(definition.Position) ||
                    definition.Intensity <= 0f ||
                    definition.Range <= 0f ||
                    definition.SpotInnerAngle <= 0f ||
                    definition.SpotOuterAngle <
                        definition.SpotInnerAngle ||
                    definition.LuxAtDistance <= 0f ||
                    definition.IndirectMultiplier < 0f ||
                    definition.ShapeRadius < 0f ||
                    !IsFinite(definition.ShapeRadius) ||
                    !IsFinite(definition.AreaSize) ||
                    (definition.AreaSize != Vector2.zero &&
                     (definition.AreaSize.x <= 0f ||
                      definition.AreaSize.y <= 0f)) ||
                    definition.VolumetricDimmer < 0f ||
                    !IsFinite(definition.VolumetricDimmer) ||
                    !Enum.IsDefined(
                        typeof(WorldLightSourceKind),
                        definition.SourceKind) ||
                    !Enum.IsDefined(
                        typeof(WorldLightBakeMode),
                        definition.BakeMode))
                {
                    throw new InvalidOperationException(
                        $"World light definition {index} is invalid.");
                }
            }

            for (int index = 0; index < probes.Count; index++)
            {
                WorldReflectionProbeDefinition definition = probes[index];
                if (definition == null ||
                    string.IsNullOrWhiteSpace(definition.ProbeId) ||
                    string.IsNullOrWhiteSpace(definition.CellId) ||
                    !ids.Add(definition.ProbeId) ||
                    !IsFinite(definition.Position) ||
                    !IsFinite(definition.Size) ||
                    definition.Size.x <= 0f ||
                    definition.Size.y <= 0f ||
                    definition.Size.z <= 0f ||
                    definition.Intensity < 0f ||
                    definition.Resolution < 16)
                {
                    throw new InvalidOperationException(
                        $"World reflection probe definition {index} is invalid.");
                }
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string id,
            List<WorldLightDefinition> configuredLights,
            List<WorldReflectionProbeDefinition> configuredProbes)
        {
            catalogId = id;
            lights = configuredLights ?? new List<WorldLightDefinition>();
            probes = configuredProbes ??
                new List<WorldReflectionProbeDefinition>();
            Validate();
        }
#endif

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) &&
                   IsFinite(value.y) &&
                   IsFinite(value.z);
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
