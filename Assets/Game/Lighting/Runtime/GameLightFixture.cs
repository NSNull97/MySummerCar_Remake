using System;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Lighting
{
    /// <summary>
    /// Marks the small project-owned emissive surface added to legacy area
    /// fixtures. Donor renderers must keep their authored base colour and
    /// transparency; only these generated lenses may be faded to black.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuntimeEmissionLens : MonoBehaviour
    {
    }

    internal static class LightingFixtureLifecycle
    {
        public static event Action<GameLightFixture> Enabled;
        public static event Action<GameLightFixture> Disabled;

        public static void PublishEnabled(GameLightFixture fixture) =>
            Enabled?.Invoke(fixture);

        public static void PublishDisabled(GameLightFixture fixture) =>
            Disabled?.Invoke(fixture);
    }

    [DisallowMultipleComponent]
    public sealed class GameLightFixture : MonoBehaviour
    {
        private static readonly int EmissiveColorId =
            Shader.PropertyToID("_EmissiveColor");
        private static readonly int EmissionColorId =
            Shader.PropertyToID("_EmissionColor");
        private static readonly int UnlitColorId =
            Shader.PropertyToID("_UnlitColor");
        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId =
            Shader.PropertyToID("_Color");
        private static readonly int EmissiveIntensityId =
            Shader.PropertyToID("_EmissiveIntensity");
        private static readonly int UseEmissiveIntensityId =
            Shader.PropertyToID("_UseEmissiveIntensity");

        [SerializeField] private string fixtureId = string.Empty;
        [SerializeField] private LightFixtureProfile profile;
        [SerializeField] private Light[] lights = Array.Empty<Light>();
        [SerializeField] private Renderer[] emissiveRenderers =
            Array.Empty<Renderer>();
        [SerializeField] private VolumetricBeamAdapter volumetricBeam;
        [SerializeField] private string powerSourceId = "power.grid";
        [SerializeField] private string circuitId = "circuit.always";
        [SerializeField] private string switchId = string.Empty;
        [SerializeField] private string zoneId = "zone.exterior";
        [SerializeField] private string businessId = string.Empty;
        [SerializeField] private string vehicleChannelId = string.Empty;
        [SerializeField] private bool fixtureAvailable = true;
        [SerializeField] private bool scriptedPolicyAllows = true;
        [SerializeField, Min(0.01f)] private float intensityMultiplier = 1f;

        private MaterialPropertyBlock propertyBlock;
        private IVehicleElectricalLightingSource vehicleElectricalSource;
        private float visualFactor;
        private bool logicalOn;
        private bool qualityVisible = true;
        private bool lifecycleRegistered;
        private bool hasAppliedQuality;
        private bool appliedShadowSelected;
        private bool appliedEveryFrameSelected;
        private bool appliedLogicalOn;
        private bool appliedShadowsEnabled;
        private bool appliedContactShadows;
        private ShadowUpdateMode appliedShadowUpdateMode;
        private int appliedShadowResolution;

        public string FixtureId => fixtureId;
        public LightFixtureProfile Profile => profile;
        public string PowerSourceId => powerSourceId;
        public string CircuitId => circuitId;
        public string SwitchId => switchId;
        public string ZoneId => zoneId;
        public string BusinessId => businessId;
        public string VehicleChannelId => vehicleChannelId;
        public bool FixtureAvailable => fixtureAvailable;
        public bool ScriptedPolicyAllows => scriptedPolicyAllows;
        public bool LogicalOn => logicalOn;
        public bool QualityVisible => qualityVisible;
        public bool WantsVisuals => logicalOn && qualityVisible;
        public float VisualFactor => visualFactor;
        public float IntensityMultiplier => intensityMultiplier;
        public IVehicleElectricalLightingSource VehicleElectricalSource =>
            vehicleElectricalSource;
        public Vector3 WorldPosition => transform.position;
        public bool HasSpotLight
        {
            get
            {
                for (int index = 0; index < lights.Length; index++)
                {
                    if (lights[index] != null &&
                        lights[index].type == LightType.Spot)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private void OnEnable()
        {
            TryPublishEnabled();
        }

        private void OnDisable()
        {
            if (lifecycleRegistered)
            {
                LightingFixtureLifecycle.PublishDisabled(this);
                lifecycleRegistered = false;
            }
        }

        private void TryPublishEnabled()
        {
            if (lifecycleRegistered || string.IsNullOrWhiteSpace(fixtureId) ||
                profile == null || lights == null || lights.Length == 0)
            {
                return;
            }

            LightingFixtureLifecycle.PublishEnabled(this);
            lifecycleRegistered = true;
        }

        public void RepublishLifecycleRegistration()
        {
            if (!isActiveAndEnabled || string.IsNullOrWhiteSpace(fixtureId) ||
                profile == null || lights == null || lights.Length == 0)
            {
                return;
            }

            // Registration is deliberately idempotent in the manager. A
            // streamed light can outlive a composition-root replacement or be
            // created between scene callbacks; replaying the event lets the
            // current manager heal that missed hand-off without cloning state.
            LightingFixtureLifecycle.PublishEnabled(this);
            lifecycleRegistered = true;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(fixtureId))
            {
                throw new InvalidOperationException(
                    $"Light fixture '{name}' has no stable fixture ID.");
            }

            if (profile == null)
            {
                throw new InvalidOperationException(
                    $"Light fixture '{fixtureId}' has no profile.");
            }

            profile.Validate();
            if (lights == null || lights.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Light fixture '{fixtureId}' has no Light component.");
            }

            if (string.IsNullOrWhiteSpace(powerSourceId) ||
                string.IsNullOrWhiteSpace(circuitId))
            {
                throw new InvalidOperationException(
                    $"Light fixture '{fixtureId}' has no power source/circuit.");
            }

            if (!float.IsFinite(intensityMultiplier) ||
                intensityMultiplier <= 0f)
            {
                throw new InvalidOperationException(
                    $"Light fixture '{fixtureId}' has an invalid intensity multiplier.");
            }
        }

        public void ApplyProfilePhotometry()
        {
            if (profile == null)
            {
                return;
            }

            for (int index = 0; index < lights.Length; index++)
            {
                Light light = lights[index];
                if (light == null)
                {
                    continue;
                }

                light.type = ResolveLightType(profile.Shape);
                light.lightUnit = profile.LightUnit;
                light.intensity = profile.Intensity * intensityMultiplier;
                light.range = profile.RangeMeters;
                light.color = profile.LightColor;
                // The garage exterior luminaire has a white donor diffuser and
                // the accepted visual target is neutral white. Keeping HDRP's
                // temperature filter enabled made both its pool and emission
                // visibly amber despite a white base colour.
                light.useColorTemperature = profile.Category !=
                    LightFixtureCategory.HomeExterior;
                light.colorTemperature = profile.ColorTemperatureKelvin;
                light.enableSpotReflector = profile.EnableSpotReflector;
                if (light.type == LightType.Spot)
                {
                    light.innerSpotAngle = profile.InnerSpotAngle;
                    light.spotAngle = profile.OuterSpotAngle;
                }

                HDAdditionalLightData hd =
                    light.GetComponent<HDAdditionalLightData>();
                if (hd == null)
                {
                    hd = light.gameObject.AddComponent<HDAdditionalLightData>();
                }

                hd.shapeRadius = profile.SourceRadiusMeters;
                light.areaSize = new Vector2(
                    profile.SourceWidthMeters,
                    profile.SourceHeightMeters);
                hd.affectsVolumetric = profile.NativeVolumetricMultiplier > 0f;
                hd.volumetricDimmer = profile.NativeVolumetricMultiplier;
                hd.volumetricShadowDimmer =
                    profile.NativeVolumetricMultiplier;
            }
        }

        public void SetLogicalState(bool isOn)
        {
            logicalOn = isOn;
        }

        public bool SetQualityVisible(bool visible)
        {
            if (qualityVisible == visible)
            {
                return false;
            }

            qualityVisible = visible;
            return true;
        }

        public void SetFixtureAvailable(bool available)
        {
            fixtureAvailable = available;
        }

        public void SetScriptedPolicyAllows(bool allows)
        {
            scriptedPolicyAllows = allows;
        }

        public void BindVehicleElectricalSource(
            IVehicleElectricalLightingSource source)
        {
            vehicleElectricalSource = source;
        }

        public void ApplyVisualFactor(float factor)
        {
            propertyBlock ??= new MaterialPropertyBlock();
            visualFactor = Mathf.Clamp01(factor);
            bool lightEnabled = visualFactor > 0.002f;
            for (int index = 0; index < lights.Length; index++)
            {
                Light light = lights[index];
                if (light == null)
                {
                    continue;
                }

                light.enabled = lightEnabled;
                light.intensity = profile.Intensity * intensityMultiplier *
                    visualFactor;
            }

            // A white emissive lens next to a temperature-tinted Light reads as
            // two unrelated sources (and produced the bright white dots above
            // the donor fixtures). Keep both outputs on the same photometric
            // colour contract.
            Color temperatureTint = UsesWhiteEmitterPresentation(profile.Category)
                ? Color.white
                : Mathf.CorrelatedColorTemperatureToRGB(
                    profile.ColorTemperatureKelvin);
            Color emissiveTint = profile.EmissiveColor *
                profile.LightColor * temperatureTint;
            Color emissive = emissiveTint *
                (profile.EmissiveIntensity * visualFactor);
            for (int index = 0; index < emissiveRenderers.Length; index++)
            {
                Renderer renderer = emissiveRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(EmissiveColorId, emissive);
                propertyBlock.SetColor(EmissionColorId, emissive);
                if (renderer.TryGetComponent<RuntimeEmissionLens>(out _))
                {
                    // The generated lens uses an unlit material and therefore
                    // needs its base colour faded with the switch. Applying
                    // these properties to a donor lampshade turned the entire
                    // textured/translucent shade into an opaque white blob.
                    propertyBlock.SetColor(UnlitColorId, emissive);
                    propertyBlock.SetColor(BaseColorId, emissive);
                    propertyBlock.SetColor(ColorId, emissive);
                }
                propertyBlock.SetFloat(
                    EmissiveIntensityId,
                    profile.EmissiveIntensity * visualFactor);
                propertyBlock.SetFloat(
                    UseEmissiveIntensityId,
                    lightEnabled ? 1f : 0f);
                renderer.SetPropertyBlock(propertyBlock);
            }
        }

        private static bool UsesWhiteEmitterPresentation(
            LightFixtureCategory category)
        {
            // The donor garage, Fleetari and both Teimo zones use fluorescent
            // luminaires. Their room light keeps its authored neutral-white
            // colour temperature, while the visible diffuser/tube itself must
            // stay white instead of inheriting an amber CCT tint.
            return category == LightFixtureCategory.HomeExterior ||
                category == LightFixtureCategory.Fluorescent ||
                category == LightFixtureCategory.TeimoShop ||
                category == LightFixtureCategory.TeimoPub ||
                category == LightFixtureCategory.FleetariWorkshop;
        }

        public void ApplyQuality(
            LightFixtureQualitySettings settings,
            bool shadowSelected,
            bool everyFrameSelected,
            VolumetricBeamQuality beamQuality,
            float beamIntensity)
        {
            bool qualityChanged = !hasAppliedQuality ||
                appliedShadowSelected != shadowSelected ||
                appliedEveryFrameSelected != everyFrameSelected ||
                appliedLogicalOn != logicalOn ||
                appliedShadowsEnabled != settings.ShadowsEnabled ||
                appliedContactShadows != settings.ContactShadowsEnabled ||
                appliedShadowUpdateMode != settings.ShadowUpdateMode ||
                appliedShadowResolution != settings.ShadowResolution;
            if (qualityChanged)
            {
                for (int index = 0; index < lights.Length; index++)
                {
                    Light light = lights[index];
                    if (light == null)
                    {
                        continue;
                    }

                    bool shadows = logicalOn && shadowSelected &&
                        settings.ShadowsEnabled;
                    light.shadows = shadows
                        ? LightShadows.Soft
                        : LightShadows.None;
                    HDAdditionalLightData hd =
                        light.GetComponent<HDAdditionalLightData>();
                    if (hd == null)
                    {
                        continue;
                    }

                    // Keep static wall/ceiling occlusion in the cached atlas so
                    // a light does not start shining through a room merely
                    // because it fell outside the tiny realtime budget. Only
                    // the nearest bounded set updates every frame for moving
                    // characters/items. Non-selected lights stay out of the
                    // cached atlas altogether, which also avoids duplicate
                    // records during additive-cell replacement.
                    ShadowUpdateMode resolvedUpdateMode = shadows
                        ? everyFrameSelected
                            ? ShadowUpdateMode.EveryFrame
                            : ShadowUpdateMode.OnDemand
                        : ShadowUpdateMode.EveryFrame;
                    bool requestCachedShadow = shadows &&
                        resolvedUpdateMode == ShadowUpdateMode.OnDemand &&
                        hd.shadowUpdateMode != ShadowUpdateMode.OnDemand;
                    hd.shadowUpdateMode = resolvedUpdateMode;
                    hd.SetShadowResolution(settings.ShadowResolution);
                    hd.SetShadowResolutionOverride(true);
                    hd.SetShadowFadeDistance(
                        Mathf.Max(1f, settings.ShadowDistanceMeters));
                    hd.useContactShadow.useOverride = true;
                    hd.useContactShadow.@override =
                        settings.ContactShadowsEnabled && everyFrameSelected;
                    hd.alwaysDrawDynamicShadows = false;
                    if (requestCachedShadow)
                    {
                        hd.RequestShadowMapRendering();
                    }
                }

                hasAppliedQuality = true;
                appliedShadowSelected = shadowSelected;
                appliedEveryFrameSelected = everyFrameSelected;
                appliedLogicalOn = logicalOn;
                appliedShadowsEnabled = settings.ShadowsEnabled;
                appliedContactShadows = settings.ContactShadowsEnabled;
                appliedShadowUpdateMode = settings.ShadowUpdateMode;
                appliedShadowResolution = settings.ShadowResolution;
            }

            if (volumetricBeam != null && HasSpotLight)
            {
                volumetricBeam.Apply(
                    WantsVisuals ? beamQuality : VolumetricBeamQuality.Off,
                    beamIntensity * visualFactor,
                    profile.RangeMeters,
                    profile.OuterSpotAngle,
                    shadowSelected);
            }
        }

        public void EnsureVolumetricAdapter()
        {
            volumetricBeam ??= GetComponent<VolumetricBeamAdapter>();
            volumetricBeam ??= gameObject.AddComponent<VolumetricBeamAdapter>();
        }

        private static LightType ResolveLightType(LightFixtureShape shape)
        {
            return shape switch
            {
                LightFixtureShape.Point => LightType.Point,
                LightFixtureShape.AreaRectangle => LightType.Rectangle,
                LightFixtureShape.AreaTube => LightType.Tube,
                _ => LightType.Spot,
            };
        }

        public void InitializeRuntime(
            string id,
            LightFixtureProfile configuredProfile,
            Light[] configuredLights,
            Renderer[] configuredEmissiveRenderers,
            string configuredPowerSourceId,
            string configuredCircuitId,
            string configuredSwitchId,
            string configuredZoneId,
            string configuredBusinessId,
            string configuredVehicleChannelId,
            bool available,
            float configuredIntensityMultiplier = 1f)
        {
            fixtureId = id ?? string.Empty;
            profile = configuredProfile;
            lights = configuredLights ?? Array.Empty<Light>();
            emissiveRenderers = configuredEmissiveRenderers ??
                Array.Empty<Renderer>();
            powerSourceId = configuredPowerSourceId ?? string.Empty;
            circuitId = configuredCircuitId ?? string.Empty;
            switchId = configuredSwitchId ?? string.Empty;
            zoneId = configuredZoneId ?? string.Empty;
            businessId = configuredBusinessId ?? string.Empty;
            vehicleChannelId = configuredVehicleChannelId ?? string.Empty;
            fixtureAvailable = available;
            intensityMultiplier = configuredIntensityMultiplier;
            if (profile != null && profile.GetQuality(
                    LightingQualityTier.High).BeamQuality !=
                VolumetricBeamQuality.Off)
            {
                EnsureVolumetricAdapter();
            }

            Validate();
            ApplyProfilePhotometry();
            if (isActiveAndEnabled)
            {
                TryPublishEnabled();
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string id,
            LightFixtureProfile configuredProfile,
            Light[] configuredLights,
            Renderer[] configuredEmissiveRenderers,
            string configuredPowerSourceId,
            string configuredCircuitId,
            string configuredSwitchId,
            string configuredZoneId,
            string configuredBusinessId,
            string configuredVehicleChannelId,
            bool available,
            float configuredIntensityMultiplier = 1f)
        {
            InitializeRuntime(
                id,
                configuredProfile,
                configuredLights,
                configuredEmissiveRenderers,
                configuredPowerSourceId,
                configuredCircuitId,
                configuredSwitchId,
                configuredZoneId,
                configuredBusinessId,
                configuredVehicleChannelId,
                available,
                configuredIntensityMultiplier);
        }
#endif
    }
}
