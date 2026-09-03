using System;
using System.Collections.Generic;
using MSC.Weather.Domain;
using MSC.Weather.Presentation;
using MSC.Weather.System.Local;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Weather.System.NativeHDRP
{
    /// <summary>
    /// Complete native HDRP presentation backend. It owns one dedicated Volume,
    /// clones its profile once on attach, caches every override and restores all
    /// suspended legacy owners on detach.
    /// </summary>
    [DefaultExecutionOrder(-30000)]
    [DisallowMultipleComponent]
    public sealed class NativeHDRPWeatherBackend : MonoBehaviour, IWeatherBackend
    {
        private const string MissingVolumeCode = "WEATHER-HDRP-001";
        private const string MissingSunCode = "WEATHER-HDRP-002";
        private const string DuplicateOwnerCode = "WEATHER-HDRP-003";
        private const string InvalidFrameCode = "WEATHER-HDRP-004";
        private const string StaleFrameCode = "WEATHER-HDRP-005";
        private const string MissingPrecipitationCode = "WEATHER-HDRP-006";

        private const EnvironmentPresentationCapabilities SupportedCapabilities =
            EnvironmentPresentationCapabilities.TimeOfDay |
            EnvironmentPresentationCapabilities.Sky |
            EnvironmentPresentationCapabilities.Clouds |
            EnvironmentPresentationCapabilities.Precipitation |
            EnvironmentPresentationCapabilities.Fog |
            EnvironmentPresentationCapabilities.Wind |
            EnvironmentPresentationCapabilities.LightningVisual |
            EnvironmentPresentationCapabilities.EnvironmentRefresh |
            EnvironmentPresentationCapabilities.QualityTiers |
            EnvironmentPresentationCapabilities.Exposure;

        private static readonly ProfilerMarker ApplyMarker =
            new ProfilerMarker("Weather.HDRPApply");

        [Header("Dedicated native owners")]
        [SerializeField] private Volume weatherVolume;
        [SerializeField] private Light directionalSun;
        [SerializeField] private HDAdditionalLightData directionalSunData;
        [SerializeField] private WeatherGeographySettings geography;
        [SerializeField] private Camera presentationCamera;
        [SerializeField] private MonoBehaviour localContextSourceComponent;

        [Header("Optional project-owned presentation")]
        [SerializeField] private ParticleSystem[] rainSystems =
            Array.Empty<ParticleSystem>();
        [SerializeField] private ParticleSystem[] drizzleSystems =
            Array.Empty<ParticleSystem>();
        [SerializeField] private Light lightningFlashLight;
        [SerializeField] private Vector3 precipitationEmitterOffset =
            new Vector3(0f, 12f, 0f);

        [Header("Reversible legacy suspension")]
        [SerializeField] private Volume[] legacyVolumesToSuspend =
            Array.Empty<Volume>();
        [SerializeField] private Behaviour[] legacyOwnersToSuspend =
            Array.Empty<Behaviour>();

        [Header("Native response")]
        [SerializeField, Min(0f)] private float smoothingSeconds = 0.35f;
        [SerializeField] private Vector2 fixedExposureLimitsEv =
            new Vector2(-1f, 14f);
        [SerializeField, Range(-1f, 16f)] private float daylightFixedExposureEv =
            12.3f;
        [SerializeField, Range(-1f, 16f)] private float readableNightFixedExposureEv =
            6.8f;
        [SerializeField, Range(1000f, 150000f)] private float clearNoonSunLux =
            95000f;
        [SerializeField, Range(1000f, 20000f)] private float horizonSunLux =
            4500f;

        private const float RainRatePerSecond = 4200f;
        private const float DrizzleRatePerSecond = 1800f;
        private const float LightningDurationSeconds = 0.42f;

        private readonly List<EnvironmentPresentationDiagnostic> diagnostics =
            new List<EnvironmentPresentationDiagnostic>(8);
        private readonly List<UnityEngine.Object> runtimeObjects =
            new List<UnityEngine.Object>(16);

        private VolumeProfile authoredProfile;
        private VolumeProfile runtimeProfile;
        private VisualEnvironment visualEnvironment;
        private PhysicallyBasedSky physicallyBasedSky;
        private VolumetricClouds volumetricClouds;
        private Fog fog;
        private Exposure exposure;
        private WhiteBalance whiteBalance;
        private ColorAdjustments colorAdjustments;
        private Tonemapping tonemapping;
        private IndirectLightingController indirectLighting;
        private bool[] legacyVolumeEnabled;
        private bool[] legacyOwnerEnabled;
        private Light[] competingDirectionalLights = Array.Empty<Light>();
        private float[] rainBaseRates;
        private bool[] rainBaseEnabled;
        private float[] drizzleBaseRates;
        private bool[] drizzleBaseEnabled;
        private float[] rainPresentationRates;
        private float[] drizzlePresentationRates;
        private PrecipitationPresentationState[] rainPresentationStates;
        private PrecipitationPresentationState[] drizzlePresentationStates;
        private ParticleSystem surfaceImpactSystem;
        private LineRenderer lightningBolt;
        private Material runtimePrecipitationMaterial;
        private Material runtimeLightningMaterial;
        private bool previousWeatherVolumeEnabled;
        private float previousWeatherVolumeWeight;
        private bool previousSunGameObjectActive;
        private bool previousSunEnabled;
        private float previousSunIntensity;
        private float previousSunShadowStrength;
        private LightShadows previousSunShadows;
        private Color previousSunColor;
        private bool previousSunUseColorTemperature;
        private float previousSunColorTemperature;
        private Quaternion previousSunRotation;
        private float previousSunLightDimmer;
        private float previousSunVolumetricDimmer;
        private bool previousFlashEnabled;
        private float previousFlashIntensity;
        private float previousFlashRange;
        private Color previousFlashColor;
        private bool attached;
        private bool hasCurrentValues;
        private bool hasFrame;
        private NativeWeatherValues currentValues;
        private NativeWeatherValues targetValues;
        private EnvironmentPresentationFrame lastFrame;
        private ILocalWeatherContextSource localContextSource;
        private EnvironmentPresentationStatus status =
            EnvironmentPresentationStatus.Detached;
        private ulong lastAppliedRevision;
        private uint lastLightningSequence;
        private uint lastRefreshSequence;
        private float lightningFlashStartedAt;
        private float lightningFlashPeakIntensity;
        private float lightningFlashUntil;

        public WeatherBackendType BackendType => WeatherBackendType.NativeHDRP;
        public WeatherPresentationOwnership Ownership =>
            WeatherPresentationOwnership.Time |
            WeatherPresentationOwnership.Sky |
            WeatherPresentationOwnership.Clouds |
            WeatherPresentationOwnership.Precipitation |
            WeatherPresentationOwnership.Fog |
            WeatherPresentationOwnership.Exposure |
            WeatherPresentationOwnership.Color |
            WeatherPresentationOwnership.IndirectLighting |
            WeatherPresentationOwnership.Sun |
            WeatherPresentationOwnership.Wind |
            WeatherPresentationOwnership.LightningPresentation;
        public bool IsAttached => attached;
        public EnvironmentPresentationCapabilities Capabilities =>
            attached
                ? SupportedCapabilities
                : EnvironmentPresentationCapabilities.None;
        public EnvironmentPresentationStatus Status => status;
        public IReadOnlyList<EnvironmentPresentationDiagnostic> Diagnostics =>
            diagnostics;
        public Volume RuntimeWeatherVolume => weatherVolume;
        public VolumeProfile RuntimeProfile => runtimeProfile;
        public Light DirectionalSun => directionalSun;
        public HDAdditionalLightData DirectionalSunData => directionalSunData;
        public WeatherGeographySettings Geography => geography;
        public IReadOnlyList<ParticleSystem> RainSystems => rainSystems;
        public IReadOnlyList<ParticleSystem> DrizzleSystems => drizzleSystems;
        public ParticleSystem SurfaceImpactSystem => surfaceImpactSystem;
        public Light LightningFlashLight => lightningFlashLight;
        public LineRenderer LightningBolt => lightningBolt;
        public IReadOnlyList<Volume> LegacyVolumesToSuspend =>
            legacyVolumesToSuspend;
        public IReadOnlyList<Behaviour> LegacyOwnersToSuspend =>
            legacyOwnersToSuspend;

        private void Reset()
        {
            weatherVolume = GetComponent<Volume>();
        }

        private void Awake()
        {
            ResolveLocalContextSource();
        }

        private void Update()
        {
            if (attached)
            {
                UpdatePrecipitationAnchors();
            }

            if (!attached || !hasFrame || !lastFrame.Enabled)
            {
                UpdateLightningFlash();
                return;
            }

            float t = smoothingSeconds <= 0f
                ? 1f
                : 1f - Mathf.Exp(-Time.unscaledDeltaTime / smoothingSeconds);
            currentValues = NativeWeatherValues.Lerp(
                currentValues,
                targetValues,
                t);
            ApplyCurrentValues();
            UpdateLightningFlash();
        }

        private void OnDisable()
        {
            if (attached)
            {
                Detach();
            }
        }

        public EnvironmentPresentationStatus Attach()
        {
            if (attached)
            {
                return status;
            }

            diagnostics.Clear();
            ResolveLocalContextSource();
            if (weatherVolume == null)
            {
                return Fault(MissingVolumeCode, "Native HDRP backend has no dedicated Volume.");
            }

            if (directionalSun == null)
            {
                return Fault(MissingSunCode, "Native HDRP backend has no authored directional sun.");
            }

            if (directionalSunData == null)
            {
                directionalSunData =
                    directionalSun.GetComponent<HDAdditionalLightData>();
            }

            CaptureOwnerState();
            directionalSun.gameObject.SetActive(true);
            SuspendLegacyOwners();
            SuspendCompetingDirectionalLights();
            PrepareRuntimeProfile();
            if (runtimeProfile == null)
            {
                RestoreOwnerState();
                return Fault(
                    MissingVolumeCode,
                    "Native HDRP runtime Volume profile could not be prepared.");
            }

            attached = true;
            weatherVolume.enabled = true;
            weatherVolume.weight = 1f;
            CaptureParticleState();
            PreparePrecipitationPresentation();
            PrepareLightningPresentation();
            EnvironmentPresentationStatus ownership =
                RevalidateSceneOwnership();
            if (!ownership.IsOperational)
            {
                Detach();
                return ownership;
            }

            int warningCount = 0;
            if (!HasAnyParticleSystem(rainSystems) &&
                !HasAnyParticleSystem(drizzleSystems))
            {
                diagnostics.Add(new EnvironmentPresentationDiagnostic(
                    EnvironmentDiagnosticSeverity.Warning,
                    MissingPrecipitationCode,
                    "Native precipitation particle systems are not assigned; sky, light, fog and wetness remain operational."));
                warningCount++;
            }

            status = new EnvironmentPresentationStatus(
                warningCount > 0
                    ? EnvironmentPresentationState.Degraded
                    : EnvironmentPresentationState.Ready,
                SupportedCapabilities,
                lastAppliedRevision,
                warningCount,
                0);
            return status;
        }

        public EnvironmentPresentationStatus Present(
            in EnvironmentPresentationFrame frame)
        {
            if (!attached)
            {
                EnvironmentPresentationStatus attachStatus = Attach();
                if (!attachStatus.IsOperational)
                {
                    return attachStatus;
                }
            }

            if (!ValidateFrame(frame))
            {
                return Fault(InvalidFrameCode, "Native HDRP backend rejected an invalid frame.");
            }

            if (frame.Enabled && frame.Revision < lastAppliedRevision)
            {
                return Fault(
                    StaleFrameCode,
                    $"Native HDRP frame {frame.Revision} is older than {lastAppliedRevision}.");
            }

            RemoveDiagnostics(InvalidFrameCode);
            RemoveDiagnostics(StaleFrameCode);
            lastFrame = frame;
            hasFrame = true;
            if (!frame.Enabled)
            {
                weatherVolume.weight = 0f;
                ApplyPrecipitation(0f, 0f);
                status = new EnvironmentPresentationStatus(
                    EnvironmentPresentationState.Disabled,
                    SupportedCapabilities,
                    lastAppliedRevision,
                    CountWarnings(),
                    0);
                return status;
            }

            weatherVolume.weight = 1f;
            targetValues = NativeWeatherValues.From(frame);
            if (!hasCurrentValues)
            {
                currentValues = targetValues;
                hasCurrentValues = true;
                ApplyCurrentValues();
            }

            HandleLightning(frame.LightningVisual);
            HandleEnvironmentRefresh(frame.EnvironmentRefresh);
            lastAppliedRevision = frame.Revision;
            status = new EnvironmentPresentationStatus(
                CountWarnings() > 0
                    ? EnvironmentPresentationState.Degraded
                    : EnvironmentPresentationState.Ready,
                SupportedCapabilities,
                lastAppliedRevision,
                CountWarnings(),
                0);
            return status;
        }

        public void Detach()
        {
            if (!attached && runtimeProfile == null)
            {
                status = EnvironmentPresentationStatus.Detached;
                return;
            }

            ApplyPrecipitation(0f, 0f);
            RestorePrecipitationPresentation();
            RestoreParticleState();
            RestoreAuthoredProfile();
            DestroyRuntimeProfile();
            RestoreOwnerState();
            attached = false;
            hasFrame = false;
            hasCurrentValues = false;
            status = EnvironmentPresentationStatus.Detached;
        }

        public bool TrySetPresentationCamera(Camera camera)
        {
            if (camera == null)
            {
                return false;
            }

            presentationCamera = camera;
            UpdatePrecipitationAnchors();
            return true;
        }

        public EnvironmentPresentationStatus RevalidateSceneOwnership()
        {
            if (!attached)
            {
                return status;
            }

            RemoveDiagnostics(DuplicateOwnerCode);
            int errors = 0;
            NativeHDRPWeatherBackend[] backends =
                FindObjectsByType<NativeHDRPWeatherBackend>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int index = 0; index < backends.Length; index++)
            {
                if (backends[index] != this && backends[index].IsAttached)
                {
                    AddOwnershipError(
                        "Another NativeHDRPWeatherBackend is attached.");
                    errors++;
                }
            }

            Volume[] volumes = FindObjectsByType<Volume>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < volumes.Length; index++)
            {
                Volume candidate = volumes[index];
                if (candidate == null || candidate == weatherVolume ||
                    !candidate.enabled || !candidate.isGlobal ||
                    candidate.weight <= 0f || IsSuspendedLegacyVolume(candidate) ||
                    !ContainsGlobalWeatherOverride(candidate.sharedProfile))
                {
                    continue;
                }

                AddOwnershipError(
                    $"Global Volume '{candidate.name}' overlaps native weather ownership.");
                errors++;
            }

            Light[] lights = FindObjectsByType<Light>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < lights.Length; index++)
            {
                Light candidate = lights[index];
                if (candidate == null || candidate == directionalSun ||
                    !candidate.enabled || candidate.type != LightType.Directional ||
                    candidate.intensity < 500f)
                {
                    continue;
                }

                AddOwnershipError(
                    $"Directional light '{candidate.name}' is a second bright sun candidate.");
                errors++;
            }

            if (errors > 0)
            {
                status = new EnvironmentPresentationStatus(
                    EnvironmentPresentationState.Faulted,
                    SupportedCapabilities,
                    lastAppliedRevision,
                    CountWarnings(),
                    errors);
                return status;
            }

            status = new EnvironmentPresentationStatus(
                CountWarnings() > 0
                    ? EnvironmentPresentationState.Degraded
                    : EnvironmentPresentationState.Ready,
                SupportedCapabilities,
                lastAppliedRevision,
                CountWarnings(),
                0);
            return status;
        }

        public void ConfigureForAuthoring(
            Volume authoredWeatherVolume,
            Light authoredDirectionalSun,
            HDAdditionalLightData authoredSunData,
            WeatherGeographySettings authoredGeography,
            ParticleSystem[] authoredRainSystems,
            ParticleSystem[] authoredDrizzleSystems,
            Volume[] authoredLegacyVolumesToSuspend,
            Behaviour[] authoredLegacyOwnersToSuspend,
            MonoBehaviour authoredLocalContextSource = null,
            Light authoredLightningFlashLight = null)
        {
            weatherVolume = authoredWeatherVolume;
            directionalSun = authoredDirectionalSun;
            directionalSunData = authoredSunData;
            geography = authoredGeography;
            rainSystems = authoredRainSystems ?? Array.Empty<ParticleSystem>();
            drizzleSystems = authoredDrizzleSystems ?? Array.Empty<ParticleSystem>();
            legacyVolumesToSuspend = authoredLegacyVolumesToSuspend ??
                Array.Empty<Volume>();
            legacyOwnersToSuspend = authoredLegacyOwnersToSuspend ??
                Array.Empty<Behaviour>();
            localContextSourceComponent = authoredLocalContextSource;
            lightningFlashLight = authoredLightningFlashLight;
            ResolveLocalContextSource();
        }

        private void ApplyCurrentValues()
        {
            using (ApplyMarker.Auto())
            {
                if (runtimeProfile == null)
                {
                    return;
                }

                float windAngle = Mathf.Repeat(
                    Mathf.Atan2(
                        currentValues.WindDirectionXZ.x,
                        currentValues.WindDirectionXZ.y) * Mathf.Rad2Deg,
                    360f);
                visualEnvironment.active = true;
                visualEnvironment.skyType.Override(
                    SkySettings.GetUniqueID<PhysicallyBasedSky>());
                visualEnvironment.cloudType.Override(0);
                visualEnvironment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
                visualEnvironment.windOrientation.Override(windAngle);
                visualEnvironment.windSpeed.Override(
                    currentValues.WindSpeedMetersPerSecond);

                float cloudAmount = Mathf.Clamp01(
                    currentValues.CloudCoverage01 * 0.65f +
                    currentValues.CloudIntensity01 * 0.35f);
                LocalWeatherContext localContext = ResolveLocalContext();
                float localFogIntensity = currentValues.FogIntensity01 *
                                          localContext.FogExposure01;
                float localVisibility = Mathf.Lerp(
                    Mathf.Max(currentValues.VisibilityMeters, 30000f),
                    currentValues.VisibilityMeters,
                    localContext.FogExposure01);
                SolarPosition solar = CalculateSolarPosition();
                float twilightPhase = TwilightFactor(
                    solar.ElevationDegrees);
                float nightReadability = NightReadabilityFactor(
                    solar.ElevationDegrees);
                physicallyBasedSky.active = true;
                physicallyBasedSky.atmosphericScattering.Override(true);
                physicallyBasedSky.aerosolDensity.Override(
                    Mathf.Lerp(0.008f, 0.035f, currentValues.FogIntensity01));
                physicallyBasedSky.aerosolAnisotropy.Override(
                    Mathf.Lerp(0.72f, 0.84f, cloudAmount));
                physicallyBasedSky.colorSaturation.Override(
                    Mathf.Lerp(0.96f, 0.86f, cloudAmount));
                physicallyBasedSky.exposure.Override(
                    Mathf.Lerp(0f, 1f, nightReadability));
                physicallyBasedSky.horizonTint.Override(Color.Lerp(
                    new Color(0.96f, 0.98f, 1f),
                    new Color(0.78f, 0.87f, 1f),
                    twilightPhase));
                physicallyBasedSky.zenithTint.Override(Color.Lerp(
                    new Color(0.94f, 0.98f, 1f),
                    new Color(0.68f, 0.80f, 1f),
                    twilightPhase));

                volumetricClouds.active = true;
                volumetricClouds.enable.Override(cloudAmount > 0.015f);
                volumetricClouds.cloudControl.Override(
                    VolumetricClouds.CloudControl.Simple);
                volumetricClouds.cloudSimpleMode.Override(
                    lastFrame.QualityTier == EnvironmentQualityTier.Low
                        ? VolumetricClouds.CloudSimpleMode.Performance
                        : VolumetricClouds.CloudSimpleMode.Quality);
                volumetricClouds.densityMultiplier.Override(
                    Mathf.Lerp(0.04f, 0.88f, cloudAmount));
                volumetricClouds.shapeFactor.Override(
                    Mathf.Lerp(0.92f, 0.48f, cloudAmount));
                volumetricClouds.erosionFactor.Override(
                    Mathf.Lerp(0.86f, 0.48f, cloudAmount));
                volumetricClouds.ambientLightProbeDimmer.Override(
                    Mathf.Lerp(1f, 0.9f, cloudAmount));
                volumetricClouds.sunLightDimmer.Override(
                    Mathf.Lerp(1f, 0.52f, cloudAmount));
                volumetricClouds.shadows.Override(cloudAmount > 0.08f);
                volumetricClouds.shadowOpacity.Override(
                    Mathf.Lerp(0.04f, 0.42f, cloudAmount));

                float meanFreePath = Mathf.Clamp(
                    localVisibility / 3.912f /
                    Mathf.Lerp(1f, 2.4f, localFogIntensity),
                    20f,
                    8000f);
                fog.active = true;
                fog.enabled.Override(true);
                fog.colorMode.Override(FogColorMode.SkyColor);
                fog.tint.Override(Color.Lerp(
                    new Color(0.96f, 0.98f, 1f),
                    new Color(0.82f, 0.89f, 0.96f),
                    localFogIntensity));
                fog.meanFreePath.Override(meanFreePath);
                fog.maxFogDistance.Override(Mathf.Max(
                    100f,
                    localVisibility * 1.25f));
                fog.baseHeight.Override(-5f);
                fog.maximumHeight.Override(
                    Mathf.Lerp(180f, 90f, localFogIntensity));
                fog.enableVolumetricFog.Override(true);
                fog.albedo.Override(new Color(0.92f, 0.95f, 0.98f));
                fog.anisotropy.Override(
                    Mathf.Lerp(0.15f, 0.45f, localFogIntensity));

                float daylight = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(-6f, 32f, solar.ElevationDegrees));
                float indoorFactor = 1f - localContext.OutdoorExposure01;
                float authoredMinimumEv = Mathf.Min(
                    fixedExposureLimitsEv.x,
                    fixedExposureLimitsEv.y);
                float authoredMaximumEv = Mathf.Max(
                    fixedExposureLimitsEv.x,
                    fixedExposureLimitsEv.y);
                float automaticMinimumEv = Mathf.Max(
                    authoredMinimumEv,
                    readableNightFixedExposureEv - Mathf.Lerp(
                        3f,
                        4.25f,
                        nightReadability));
                float automaticMaximumEv = Mathf.Min(
                    authoredMaximumEv,
                    daylightFixedExposureEv + 0.7f);
                float exposureCompensation =
                    0.35f +
                    cloudAmount * 0.35f +
                    localContext.ExposureCompensationEv * 0.85f;
                exposure.active = true;
                exposure.mode.Override(ExposureMode.Automatic);
                exposure.meteringMode.Override(MeteringMode.CenterWeighted);
                exposure.compensation.Override(exposureCompensation);
                exposure.limitMin.Override(automaticMinimumEv);
                exposure.limitMax.Override(Mathf.Max(
                    automaticMinimumEv + 0.5f,
                    automaticMaximumEv));
                exposure.adaptationMode.Override(AdaptationMode.Progressive);
                exposure.adaptationSpeedDarkToLight.Override(3.2f);
                exposure.adaptationSpeedLightToDark.Override(1.6f);
                exposure.targetMidGray.Override(TargetMidGray.Grey14);

                whiteBalance.active = true;
                whiteBalance.temperature.Override(
                    Mathf.Lerp(-3f, -8f, cloudAmount) -
                    twilightPhase * 5f);
                whiteBalance.tint.Override(0f);
                colorAdjustments.active = true;
                colorAdjustments.postExposure.Override(0f);
                colorAdjustments.contrast.Override(
                    Mathf.Lerp(0f, -2f, cloudAmount));
                colorAdjustments.saturation.Override(
                    Mathf.Lerp(0f, -5f, cloudAmount));
                colorAdjustments.colorFilter.Override(Color.Lerp(
                    Color.white,
                    new Color(0.94f, 0.98f, 1f),
                    Mathf.Max(
                        cloudAmount * 0.3f,
                        twilightPhase * 0.45f)));
                tonemapping.active = true;
                tonemapping.mode.Override(TonemappingMode.ACES);
                tonemapping.useFullACES.Override(true);

                indirectLighting.active = true;
                indirectLighting.indirectDiffuseLightingMultiplier.Override(
                    Mathf.Lerp(0.72f, 1.16f, daylight) *
                    Mathf.Lerp(1f, 1.18f, cloudAmount) *
                    Mathf.Lerp(1f, 0.86f, indoorFactor));
                indirectLighting.reflectionLightingMultiplier.Override(
                    Mathf.Lerp(1f, 0.82f, indoorFactor));
                indirectLighting.reflectionProbeIntensityMultiplier.Override(
                    Mathf.Lerp(1f, 0.82f, indoorFactor));

                ApplySun(solar, cloudAmount);
                float precipitationExposure =
                    localContext.PrecipitationExposure01;
                ApplyPrecipitation(
                    currentValues.Precipitation01 * precipitationExposure,
                    currentValues.Drizzle01 * precipitationExposure);
            }
        }

        private void ApplySun(SolarPosition solar, float cloudAmount)
        {
            directionalSun.transform.rotation = solar.ToDirectionalLightRotation();
            float altitude01 = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(-2f, 48f, solar.ElevationDegrees));
            float baseLux = Mathf.Lerp(
                horizonSunLux,
                clearNoonSunLux,
                altitude01) * 1.05f;
            float cloudOcclusion = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.08f, 0.65f, cloudAmount));
            float attenuation = Mathf.Lerp(0.92f, 0.1f, cloudOcclusion);
            float surfaceVisibility = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(
                    -1.5f,
                    1.5f,
                    solar.ElevationDegrees));
            // Keep the celestial body registered with Physically Based Sky.
            // Disabling it below -6 degrees removes all atmospheric scattering
            // in one frame. Surface and volumetric contribution fade through
            // the horizon independently via the HDRP light dimmers.
            directionalSun.enabled = true;
            directionalSun.intensity = baseLux * attenuation;
            directionalSun.color = Color.white;
            directionalSun.useColorTemperature = true;
            directionalSun.colorTemperature = Mathf.Lerp(
                5900f,
                6800f,
                altitude01);
            directionalSun.shadowStrength = Mathf.Lerp(
                0.9f,
                0.08f,
                cloudOcclusion) * surfaceVisibility;
            directionalSun.shadows = surfaceVisibility > 0.001f
                ? previousSunShadows
                : LightShadows.None;
            if (directionalSunData != null)
            {
                directionalSunData.lightDimmer = surfaceVisibility;
                directionalSunData.volumetricDimmer = surfaceVisibility *
                    Mathf.Lerp(
                        1f,
                        0.62f,
                        cloudOcclusion);
            }
        }

        private SolarPosition CalculateSolarPosition()
        {
            if (geography != null)
            {
                return geography.Calculate(
                    lastFrame.Year,
                    lastFrame.Month,
                    lastFrame.Day,
                    currentValues.TimeOfDay01);
            }

            int dayOfYear = 220;
            if (lastFrame.Year >= 1 && lastFrame.Year <= 9999 &&
                lastFrame.Month >= 1 && lastFrame.Month <= 12 &&
                lastFrame.Day >= 1 &&
                lastFrame.Day <= DateTime.DaysInMonth(
                    lastFrame.Year,
                    lastFrame.Month))
            {
                dayOfYear = new DateTime(
                    lastFrame.Year,
                    lastFrame.Month,
                    lastFrame.Day).DayOfYear;
            }

            return SolarPositionCalculator.Calculate(
                62.4f,
                25.7f,
                dayOfYear,
                3f,
                currentValues.TimeOfDay01,
                0f);
        }

        private void ApplyPrecipitation(float rain01, float drizzle01)
        {
            UpdatePrecipitationAnchors();
            ApplyParticleIntensity(
                rainSystems,
                rainPresentationRates,
                rain01);
            ApplyParticleIntensity(
                drizzleSystems,
                drizzlePresentationRates,
                drizzle01);
            if (surfaceImpactSystem != null &&
                rain01 <= 0.001f && drizzle01 <= 0.001f)
            {
                surfaceImpactSystem.Clear(withChildren: false);
            }
        }

        private void UpdatePrecipitationAnchors()
        {
            if (presentationCamera == null)
            {
                return;
            }

            Vector3 position =
                presentationCamera.transform.position + precipitationEmitterOffset;
            MoveEmitterRoots(rainSystems, position);
            MoveEmitterRoots(drizzleSystems, position);
        }

        private static void MoveEmitterRoots(
            ParticleSystem[] systems,
            Vector3 worldPosition)
        {
            if (systems == null)
            {
                return;
            }

            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem system = systems[index];
                if (system != null)
                {
                    system.transform.position = worldPosition;
                }
            }
        }

        private static void ApplyParticleIntensity(
            ParticleSystem[] systems,
            float[] baseRates,
            float intensity01)
        {
            if (systems == null || baseRates == null)
            {
                return;
            }

            float intensity = Mathf.Clamp01(intensity01);
            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem system = systems[index];
                if (system == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = system.emission;
                emission.enabled = intensity > 0.001f;
                emission.rateOverTimeMultiplier = baseRates[index] * intensity;
                if (intensity > 0.001f && !system.isPlaying)
                {
                    system.Play(withChildren: false);
                }
                else if (intensity <= 0.001f &&
                         (system.isPlaying || system.particleCount > 0))
                {
                    system.Stop(
                        withChildren: false,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void HandleLightning(
            in EnvironmentLightningVisualRequest request)
        {
            if (!request.IsRequested || request.Sequence <= lastLightningSequence ||
                (lightningFlashLight == null && lightningBolt == null))
            {
                return;
            }

            lastLightningSequence = request.Sequence;
            lightningFlashStartedAt = Time.unscaledTime;
            lightningFlashPeakIntensity = Mathf.Lerp(
                65000f,
                180000f,
                request.Intensity01);
            lightningFlashUntil =
                lightningFlashStartedAt + LightningDurationSeconds;
            if (lightningFlashLight != null)
            {
                lightningFlashLight.transform.position = request.WorldPosition +
                    Vector3.up * 35f;
                lightningFlashLight.enabled = true;
                lightningFlashLight.intensity = lightningFlashPeakIntensity;
            }

            BuildLightningBolt(request);
        }

        private void UpdateLightningFlash()
        {
            if (lightningFlashUntil <= 0f)
            {
                return;
            }

            float elapsed = Time.unscaledTime - lightningFlashStartedAt;
            if (Time.unscaledTime >= lightningFlashUntil)
            {
                if (lightningFlashLight != null)
                {
                    lightningFlashLight.enabled = false;
                }

                if (lightningBolt != null)
                {
                    lightningBolt.enabled = false;
                }

                lightningFlashUntil = 0f;
                return;
            }

            float pulse = LightningPulse(elapsed);
            if (lightningFlashLight != null)
            {
                lightningFlashLight.enabled = pulse > 0.025f;
                lightningFlashLight.intensity =
                    lightningFlashPeakIntensity * pulse;
            }

            if (lightningBolt != null)
            {
                lightningBolt.enabled = pulse > 0.16f;
                lightningBolt.widthMultiplier = Mathf.Lerp(
                    0.45f,
                    0.78f,
                    pulse);
            }
        }

        private void HandleEnvironmentRefresh(
            in EnvironmentRefreshRequest request)
        {
            if (request.Targets == EnvironmentRefreshTarget.None ||
                request.Sequence <= lastRefreshSequence)
            {
                return;
            }

            lastRefreshSequence = request.Sequence;
            if ((request.Targets &
                 (EnvironmentRefreshTarget.Sky |
                  EnvironmentRefreshTarget.Ambient)) != 0)
            {
                if (RenderPipelineManager.currentPipeline is HDRenderPipeline hdrp)
                {
                    hdrp.RequestSkyEnvironmentUpdate();
                }

                DynamicGI.UpdateEnvironment();
            }
        }

        private void BuildLightningBolt(
            in EnvironmentLightningVisualRequest request)
        {
            if (lightningBolt == null)
            {
                return;
            }

            const int SegmentCount = 17;
            Vector3 strike = request.WorldPosition + Vector3.up * 2f;
            Vector3 cloud = strike + Vector3.up * Mathf.Lerp(
                170f,
                260f,
                request.Intensity01);
            lightningBolt.positionCount = SegmentCount;
            for (int index = 0; index < SegmentCount; index++)
            {
                float progress = index / (SegmentCount - 1f);
                Vector3 position = Vector3.Lerp(cloud, strike, progress);
                if (index > 0 && index < SegmentCount - 1)
                {
                    float envelope = Mathf.Sin(progress * Mathf.PI) *
                                     Mathf.Lerp(8f, 2f, progress);
                    position.x += SignedHash(
                        request.Sequence,
                        (uint)(index * 2)) * envelope;
                    position.z += SignedHash(
                        request.Sequence,
                        (uint)(index * 2 + 1)) * envelope;
                }

                lightningBolt.SetPosition(index, position);
            }

            lightningBolt.enabled = true;
        }

        private static float LightningPulse(float elapsedSeconds)
        {
            if (elapsedSeconds < 0.06f)
            {
                return 1f;
            }

            if (elapsedSeconds < 0.12f)
            {
                return 0.08f;
            }

            if (elapsedSeconds < 0.22f)
            {
                return 0.78f;
            }

            if (elapsedSeconds < 0.3f)
            {
                return 0.04f;
            }

            return 0.46f;
        }

        private static float SignedHash(uint sequence, uint ordinal)
        {
            uint hash = sequence * 0x9E3779B9u + ordinal * 0x85EBCA6Bu;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            return (hash & 0x00FFFFFFu) / 8388607.5f - 1f;
        }

        private void PreparePrecipitationPresentation()
        {
            rainPresentationStates = CapturePrecipitationPresentationState(
                rainSystems);
            drizzlePresentationStates = CapturePrecipitationPresentationState(
                drizzleSystems);
            if (!HasAnyParticleSystem(rainSystems) &&
                !HasAnyParticleSystem(drizzleSystems))
            {
                rainPresentationRates = Array.Empty<float>();
                drizzlePresentationRates = Array.Empty<float>();
                return;
            }

            runtimePrecipitationMaterial = CreateRuntimePrecipitationMaterial();
            rainPresentationRates = ConfigurePrecipitationSystems(
                rainSystems,
                RainRatePerSecond,
                true);
            drizzlePresentationRates = ConfigurePrecipitationSystems(
                drizzleSystems,
                DrizzleRatePerSecond,
                false);
            surfaceImpactSystem = CreateSurfaceImpactSystem();
            AttachSurfaceImpactPresenters(rainSystems, 28);
            AttachSurfaceImpactPresenters(drizzleSystems, 12);
        }

        private Material CreateRuntimePrecipitationMaterial()
        {
            Material source = FindFirstPrecipitationMaterial();
            Shader shader = source != null
                ? source.shader
                : Shader.Find("HDRP/Unlit");
            if (shader == null)
            {
                return null;
            }

            Material material = source != null
                ? new Material(source)
                : new Material(shader);
            material.name = "Native Weather Rain (Runtime)";
            material.hideFlags = HideFlags.DontSave;
            SetMaterialColorIfPresent(
                material,
                "_BaseColor",
                new Color(0.58f, 0.62f, 0.68f, 0.48f));
            SetMaterialColorIfPresent(
                material,
                "_UnlitColor",
                new Color(0.58f, 0.62f, 0.68f, 0.48f));
            SetMaterialFloatIfPresent(material, "_EmissiveExposureWeight", 1f);
            runtimeObjects.Add(material);
            return material;
        }

        private Material FindFirstPrecipitationMaterial()
        {
            Material material = FindFirstPrecipitationMaterial(rainSystems);
            return material != null
                ? material
                : FindFirstPrecipitationMaterial(drizzleSystems);
        }

        private static Material FindFirstPrecipitationMaterial(
            ParticleSystem[] systems)
        {
            if (systems == null)
            {
                return null;
            }

            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem system = systems[index];
                if (system == null)
                {
                    continue;
                }

                ParticleSystemRenderer renderer =
                    system.GetComponent<ParticleSystemRenderer>();
                if (renderer != null && renderer.sharedMaterial != null)
                {
                    return renderer.sharedMaterial;
                }
            }

            return null;
        }

        private float[] ConfigurePrecipitationSystems(
            ParticleSystem[] systems,
            float totalRatePerSecond,
            bool heavyRain)
        {
            int validCount = 0;
            for (int index = 0; index < systems.Length; index++)
            {
                if (systems[index] != null)
                {
                    validCount++;
                }
            }

            float[] rates = new float[systems.Length];
            if (validCount == 0)
            {
                return rates;
            }

            float ratePerSystem = totalRatePerSecond / validCount;
            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem system = systems[index];
                if (system == null)
                {
                    continue;
                }

                rates[index] = ratePerSystem;
                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.startLifetime = heavyRain
                    ? new ParticleSystem.MinMaxCurve(0.72f, 0.92f)
                    : new ParticleSystem.MinMaxCurve(1.25f, 1.65f);
                main.startSpeed = 0f;
                main.startSize = heavyRain
                    ? new ParticleSystem.MinMaxCurve(0.008f, 0.014f)
                    : new ParticleSystem.MinMaxCurve(0.006f, 0.01f);
                main.startColor = heavyRain
                    ? new Color(0.78f, 0.82f, 0.88f, 0.28f)
                    : new Color(0.8f, 0.84f, 0.89f, 0.2f);
                main.gravityModifier = 0f;
                main.maxParticles = heavyRain ? 5200 : 2800;

                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = heavyRain
                    ? new Vector3(18f, 0.6f, 14f)
                    : new Vector3(16f, 0.6f, 12f);

                ParticleSystem.VelocityOverLifetimeModule velocity =
                    system.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                velocity.x = 0f;
                velocity.y = heavyRain ? -27f : -10f;
                velocity.z = 0f;

                ParticleSystem.CollisionModule collision = system.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.World;
                collision.mode = ParticleSystemCollisionMode.Collision3D;
                collision.collidesWith = Physics.DefaultRaycastLayers;
                collision.dampen = 0.04f;
                collision.bounce = 0f;
                collision.lifetimeLoss = 1f;
                collision.radiusScale = 0.12f;
                collision.quality = ParticleSystemCollisionQuality.Low;
                collision.enableDynamicColliders = false;
                collision.maxCollisionShapes = 128;
                collision.sendCollisionMessages = true;

                ParticleSystemRenderer renderer =
                    system.GetComponent<ParticleSystemRenderer>();
                if (renderer == null)
                {
                    renderer = system.gameObject.AddComponent<
                        ParticleSystemRenderer>();
                }

                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = heavyRain ? 0.022f : 0.015f;
                renderer.lengthScale = heavyRain ? 0.42f : 0.28f;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingFudge = 0f;
                if (runtimePrecipitationMaterial != null)
                {
                    renderer.sharedMaterial = runtimePrecipitationMaterial;
                }
            }

            return rates;
        }

        private ParticleSystem CreateSurfaceImpactSystem()
        {
            var owner = new GameObject("Rain Surface Impacts")
            {
                hideFlags = HideFlags.DontSave,
            };
            owner.transform.SetParent(transform, false);
            runtimeObjects.Add(owner);

            ParticleSystem system = owner.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.032f);
            main.startColor = new Color(0.72f, 0.78f, 0.84f, 0.34f);
            main.gravityModifier = 0.8f;
            main.maxParticles = 600;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(
                1f,
                AnimationCurve.Linear(0f, 1f, 1f, 0f));

            ParticleSystemRenderer renderer =
                owner.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.035f;
            renderer.lengthScale = 0.12f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = 0f;
            if (runtimePrecipitationMaterial != null)
            {
                renderer.sharedMaterial = runtimePrecipitationMaterial;
            }

            return system;
        }

        private void AttachSurfaceImpactPresenters(
            ParticleSystem[] systems,
            int totalDropletBudgetPerFrame)
        {
            int validCount = 0;
            for (int index = 0; index < systems.Length; index++)
            {
                if (systems[index] != null)
                {
                    validCount++;
                }
            }

            int budgetPerSource = Mathf.Max(
                1,
                totalDropletBudgetPerFrame / Mathf.Max(1, validCount));
            for (int index = 0; index < systems.Length; index++)
            {
                ParticleSystem source = systems[index];
                if (source == null)
                {
                    continue;
                }

                RainSurfaceImpactPresenter presenter =
                    source.gameObject.AddComponent<RainSurfaceImpactPresenter>();
                presenter.ConfigureForRuntime(
                    source,
                    surfaceImpactSystem,
                    budgetPerSource);
                runtimeObjects.Add(presenter);
            }
        }

        private void PrepareLightningPresentation()
        {
            if (lightningFlashLight != null)
            {
                lightningFlashLight.range = Mathf.Max(
                    lightningFlashLight.range,
                    520f);
                lightningFlashLight.color = new Color(0.72f, 0.82f, 1f);
            }

            Shader shader = runtimePrecipitationMaterial != null
                ? runtimePrecipitationMaterial.shader
                : Shader.Find("HDRP/Unlit");
            if (shader == null)
            {
                return;
            }

            runtimeLightningMaterial = runtimePrecipitationMaterial != null
                ? new Material(runtimePrecipitationMaterial)
                : new Material(shader);
            runtimeLightningMaterial.name = "Native Weather Lightning (Runtime)";
            runtimeLightningMaterial.hideFlags = HideFlags.DontSave;
            Color boltColor = new Color(5f, 7f, 11f, 0.95f);
            SetMaterialColorIfPresent(
                runtimeLightningMaterial,
                "_BaseColor",
                boltColor);
            SetMaterialColorIfPresent(
                runtimeLightningMaterial,
                "_UnlitColor",
                boltColor);
            SetMaterialColorIfPresent(
                runtimeLightningMaterial,
                "_EmissiveColor",
                boltColor);
            SetMaterialFloatIfPresent(
                runtimeLightningMaterial,
                "_EmissiveExposureWeight",
                0f);
            SetMaterialTextureIfPresent(
                runtimeLightningMaterial,
                "_BaseColorMap",
                Texture2D.whiteTexture);
            SetMaterialTextureIfPresent(
                runtimeLightningMaterial,
                "_UnlitColorMap",
                Texture2D.whiteTexture);
            runtimeObjects.Add(runtimeLightningMaterial);

            var owner = new GameObject("Lightning Bolt")
            {
                hideFlags = HideFlags.DontSave,
            };
            owner.transform.SetParent(transform, false);
            runtimeObjects.Add(owner);
            lightningBolt = owner.AddComponent<LineRenderer>();
            lightningBolt.useWorldSpace = true;
            lightningBolt.positionCount = 0;
            lightningBolt.alignment = LineAlignment.View;
            lightningBolt.textureMode = LineTextureMode.Stretch;
            lightningBolt.numCapVertices = 2;
            lightningBolt.numCornerVertices = 1;
            lightningBolt.startColor = new Color(0.82f, 0.9f, 1f, 0.96f);
            lightningBolt.endColor = new Color(0.62f, 0.76f, 1f, 0.74f);
            lightningBolt.startWidth = 0.7f;
            lightningBolt.endWidth = 0.12f;
            lightningBolt.shadowCastingMode = ShadowCastingMode.Off;
            lightningBolt.receiveShadows = false;
            lightningBolt.sharedMaterial = runtimeLightningMaterial;
            lightningBolt.enabled = false;
        }

        private static void SetMaterialColorIfPresent(
            Material material,
            string propertyName,
            Color value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetMaterialFloatIfPresent(
            Material material,
            string propertyName,
            float value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetMaterialTextureIfPresent(
            Material material,
            string propertyName,
            Texture value)
        {
            if (material != null && material.HasProperty(propertyName))
            {
                material.SetTexture(propertyName, value);
            }
        }

        private void PrepareRuntimeProfile()
        {
            authoredProfile = weatherVolume.sharedProfile;
            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeProfile.name = (authoredProfile != null
                ? authoredProfile.name
                : "NativeHDRPWeather") + " (Runtime)";
            runtimeProfile.hideFlags = HideFlags.DontSave;
            runtimeObjects.Add(runtimeProfile);
            if (authoredProfile != null)
            {
                for (int index = 0; index < authoredProfile.components.Count; index++)
                {
                    VolumeComponent source = authoredProfile.components[index];
                    if (source == null)
                    {
                        continue;
                    }

                    VolumeComponent copy = Instantiate(source);
                    copy.hideFlags = HideFlags.DontSave;
                    runtimeProfile.components.Add(copy);
                    runtimeObjects.Add(copy);
                }
            }

            visualEnvironment = GetOrAddRuntimeComponent<VisualEnvironment>();
            physicallyBasedSky = GetOrAddRuntimeComponent<PhysicallyBasedSky>();
            volumetricClouds = GetOrAddRuntimeComponent<VolumetricClouds>();
            fog = GetOrAddRuntimeComponent<Fog>();
            exposure = GetOrAddRuntimeComponent<Exposure>();
            whiteBalance = GetOrAddRuntimeComponent<WhiteBalance>();
            colorAdjustments = GetOrAddRuntimeComponent<ColorAdjustments>();
            tonemapping = GetOrAddRuntimeComponent<Tonemapping>();
            indirectLighting =
                GetOrAddRuntimeComponent<IndirectLightingController>();
            weatherVolume.sharedProfile = runtimeProfile;
        }

        private T GetOrAddRuntimeComponent<T>() where T : VolumeComponent
        {
            if (runtimeProfile.TryGet(out T component))
            {
                return component;
            }

            component = runtimeProfile.Add<T>(true);
            component.hideFlags = HideFlags.DontSave;
            runtimeObjects.Add(component);
            return component;
        }

        private void RestoreAuthoredProfile()
        {
            if (weatherVolume != null &&
                weatherVolume.sharedProfile == runtimeProfile)
            {
                weatherVolume.sharedProfile = authoredProfile;
            }
        }

        private void DestroyRuntimeProfile()
        {
            for (int index = runtimeObjects.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object value = runtimeObjects[index];
                if (value == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(value);
                }
                else
                {
                    DestroyImmediate(value);
                }
            }

            runtimeObjects.Clear();
            runtimeProfile = null;
            visualEnvironment = null;
            physicallyBasedSky = null;
            volumetricClouds = null;
            fog = null;
            exposure = null;
            whiteBalance = null;
            colorAdjustments = null;
            tonemapping = null;
            indirectLighting = null;
            surfaceImpactSystem = null;
            lightningBolt = null;
            runtimePrecipitationMaterial = null;
            runtimeLightningMaterial = null;
            lightningFlashStartedAt = 0f;
            lightningFlashPeakIntensity = 0f;
            lightningFlashUntil = 0f;
        }

        private void CaptureOwnerState()
        {
            previousWeatherVolumeEnabled = weatherVolume.enabled;
            previousWeatherVolumeWeight = weatherVolume.weight;
            previousSunGameObjectActive = directionalSun.gameObject.activeSelf;
            previousSunEnabled = directionalSun.enabled;
            previousSunIntensity = directionalSun.intensity;
            previousSunShadowStrength = directionalSun.shadowStrength;
            previousSunShadows = directionalSun.shadows;
            previousSunColor = directionalSun.color;
            previousSunUseColorTemperature = directionalSun.useColorTemperature;
            previousSunColorTemperature = directionalSun.colorTemperature;
            previousSunRotation = directionalSun.transform.rotation;
            if (directionalSunData != null)
            {
                previousSunLightDimmer = directionalSunData.lightDimmer;
                previousSunVolumetricDimmer =
                    directionalSunData.volumetricDimmer;
            }
            if (lightningFlashLight != null)
            {
                previousFlashEnabled = lightningFlashLight.enabled;
                previousFlashIntensity = lightningFlashLight.intensity;
                previousFlashRange = lightningFlashLight.range;
                previousFlashColor = lightningFlashLight.color;
            }
        }

        private void SuspendLegacyOwners()
        {
            legacyVolumeEnabled = new bool[legacyVolumesToSuspend.Length];
            for (int index = 0; index < legacyVolumesToSuspend.Length; index++)
            {
                Volume volume = legacyVolumesToSuspend[index];
                if (volume == null || volume == weatherVolume)
                {
                    continue;
                }

                legacyVolumeEnabled[index] = volume.enabled;
                volume.enabled = false;
            }

            legacyOwnerEnabled = new bool[legacyOwnersToSuspend.Length];
            for (int index = 0; index < legacyOwnersToSuspend.Length; index++)
            {
                Behaviour owner = legacyOwnersToSuspend[index];
                if (owner == null || owner == this)
                {
                    continue;
                }

                legacyOwnerEnabled[index] = owner.enabled;
                owner.enabled = false;
            }
        }

        private void SuspendCompetingDirectionalLights()
        {
            Light[] sceneLights = FindObjectsByType<Light>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            var suspended = new List<Light>(2);
            for (int index = 0; index < sceneLights.Length; index++)
            {
                Light candidate = sceneLights[index];
                if (candidate == null || candidate == directionalSun ||
                    !candidate.enabled || candidate.type != LightType.Directional)
                {
                    continue;
                }

                candidate.enabled = false;
                suspended.Add(candidate);
            }

            competingDirectionalLights = suspended.ToArray();
        }

        private void RestoreOwnerState()
        {
            if (weatherVolume != null)
            {
                weatherVolume.enabled = previousWeatherVolumeEnabled;
                weatherVolume.weight = previousWeatherVolumeWeight;
            }

            if (directionalSun != null)
            {
                directionalSun.enabled = previousSunEnabled;
                directionalSun.intensity = previousSunIntensity;
                directionalSun.shadowStrength = previousSunShadowStrength;
                directionalSun.shadows = previousSunShadows;
                directionalSun.color = previousSunColor;
                directionalSun.useColorTemperature =
                    previousSunUseColorTemperature;
                directionalSun.colorTemperature = previousSunColorTemperature;
                directionalSun.transform.rotation = previousSunRotation;
                directionalSun.gameObject.SetActive(previousSunGameObjectActive);
            }

            if (directionalSunData != null)
            {
                directionalSunData.lightDimmer = previousSunLightDimmer;
                directionalSunData.volumetricDimmer =
                    previousSunVolumetricDimmer;
            }

            for (int index = 0;
                 legacyVolumeEnabled != null &&
                 index < legacyVolumeEnabled.Length &&
                 index < legacyVolumesToSuspend.Length;
                 index++)
            {
                if (legacyVolumesToSuspend[index] != null)
                {
                    legacyVolumesToSuspend[index].enabled =
                        legacyVolumeEnabled[index];
                }
            }

            for (int index = 0;
                 legacyOwnerEnabled != null &&
                 index < legacyOwnerEnabled.Length &&
                 index < legacyOwnersToSuspend.Length;
                 index++)
            {
                if (legacyOwnersToSuspend[index] != null)
                {
                    legacyOwnersToSuspend[index].enabled =
                        legacyOwnerEnabled[index];
                }
            }

            for (int index = 0;
                 index < competingDirectionalLights.Length;
                 index++)
            {
                if (competingDirectionalLights[index] != null)
                {
                    competingDirectionalLights[index].enabled = true;
                }
            }

            competingDirectionalLights = Array.Empty<Light>();

            if (lightningFlashLight != null)
            {
                lightningFlashLight.enabled = previousFlashEnabled;
                lightningFlashLight.intensity = previousFlashIntensity;
                lightningFlashLight.range = previousFlashRange;
                lightningFlashLight.color = previousFlashColor;
            }
        }

        private void CaptureParticleState()
        {
            CaptureParticleState(rainSystems, out rainBaseRates, out rainBaseEnabled);
            CaptureParticleState(
                drizzleSystems,
                out drizzleBaseRates,
                out drizzleBaseEnabled);
        }

        private static void CaptureParticleState(
            ParticleSystem[] systems,
            out float[] rates,
            out bool[] enabledStates)
        {
            rates = new float[systems.Length];
            enabledStates = new bool[systems.Length];
            for (int index = 0; index < systems.Length; index++)
            {
                if (systems[index] == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = systems[index].emission;
                rates[index] = emission.rateOverTimeMultiplier;
                enabledStates[index] = emission.enabled;
            }
        }

        private static PrecipitationPresentationState[]
            CapturePrecipitationPresentationState(ParticleSystem[] systems)
        {
            var states = new PrecipitationPresentationState[systems.Length];
            for (int index = 0; index < systems.Length; index++)
            {
                states[index] = new PrecipitationPresentationState(
                    systems[index]);
            }

            return states;
        }

        private void RestorePrecipitationPresentation()
        {
            RestorePrecipitationPresentation(rainPresentationStates);
            RestorePrecipitationPresentation(drizzlePresentationStates);
            rainPresentationStates = null;
            drizzlePresentationStates = null;
            rainPresentationRates = null;
            drizzlePresentationRates = null;
        }

        private static void RestorePrecipitationPresentation(
            PrecipitationPresentationState[] states)
        {
            if (states == null)
            {
                return;
            }

            for (int index = 0; index < states.Length; index++)
            {
                states[index].Restore();
            }
        }

        private void RestoreParticleState()
        {
            RestoreParticleState(rainSystems, rainBaseRates, rainBaseEnabled);
            RestoreParticleState(
                drizzleSystems,
                drizzleBaseRates,
                drizzleBaseEnabled);
        }

        private static void RestoreParticleState(
            ParticleSystem[] systems,
            float[] rates,
            bool[] enabledStates)
        {
            if (rates == null || enabledStates == null)
            {
                return;
            }

            for (int index = 0; index < systems.Length; index++)
            {
                if (systems[index] == null)
                {
                    continue;
                }

                ParticleSystem.EmissionModule emission = systems[index].emission;
                emission.rateOverTimeMultiplier = rates[index];
                emission.enabled = enabledStates[index];
            }
        }

        private bool ValidateFrame(in EnvironmentPresentationFrame frame)
        {
            if (!frame.Enabled)
            {
                return !frame.LightningVisual.IsRequested &&
                       frame.EnvironmentRefresh.Targets ==
                       EnvironmentRefreshTarget.None;
            }

            return frame.BindingId.IsValid &&
                   frame.Revision > 0UL &&
                   frame.Year >= 1 && frame.Year <= 9999 &&
                   frame.Month >= 1 && frame.Month <= 12 &&
                   frame.Day >= 1 &&
                   frame.Day <= DateTime.DaysInMonth(frame.Year, frame.Month) &&
                   Is01(frame.NormalizedTimeOfDay01) &&
                   Is01(frame.CloudCoverage01) &&
                   Is01(frame.CloudIntensity01) &&
                   Is01(frame.PrecipitationIntensity01) &&
                   Is01(frame.FogMistIntensity01) &&
                   IsFinite(frame.VisibilityMeters) &&
                   frame.VisibilityMeters > 0f &&
                   IsFinite(frame.WindDirectionXZ.x) &&
                   IsFinite(frame.WindDirectionXZ.y) &&
                   IsFinite(frame.WindSpeedMetersPerSecond) &&
                   frame.WindSpeedMetersPerSecond >= 0f &&
                   IsFinite(frame.WindGustSpeedMetersPerSecond) &&
                   frame.WindGustSpeedMetersPerSecond >=
                   frame.WindSpeedMetersPerSecond;
        }

        private bool IsSuspendedLegacyVolume(Volume volume)
        {
            for (int index = 0; index < legacyVolumesToSuspend.Length; index++)
            {
                if (legacyVolumesToSuspend[index] == volume)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsGlobalWeatherOverride(VolumeProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            return HasActive<VisualEnvironment>(profile) ||
                   HasActive<PhysicallyBasedSky>(profile) ||
                   HasActive<VolumetricClouds>(profile) ||
                   HasActive<Fog>(profile) ||
                   HasActive<Exposure>(profile) ||
                   HasActive<WhiteBalance>(profile) ||
                   HasActive<ColorAdjustments>(profile) ||
                   HasActive<Tonemapping>(profile) ||
                   HasActive<IndirectLightingController>(profile);
        }

        private static bool HasActive<T>(VolumeProfile profile)
            where T : VolumeComponent =>
            profile.TryGet(out T component) && component != null &&
            component.active;

        private void AddOwnershipError(string message)
        {
            diagnostics.Add(new EnvironmentPresentationDiagnostic(
                EnvironmentDiagnosticSeverity.Error,
                DuplicateOwnerCode,
                message));
        }

        private void RemoveDiagnostics(string code)
        {
            for (int index = diagnostics.Count - 1; index >= 0; index--)
            {
                if (string.Equals(
                        diagnostics[index].Code,
                        code,
                        StringComparison.Ordinal))
                {
                    diagnostics.RemoveAt(index);
                }
            }
        }

        private EnvironmentPresentationStatus Fault(string code, string message)
        {
            RemoveDiagnostics(code);
            diagnostics.Add(new EnvironmentPresentationDiagnostic(
                EnvironmentDiagnosticSeverity.Error,
                code,
                message));
            status = new EnvironmentPresentationStatus(
                EnvironmentPresentationState.Faulted,
                attached
                    ? SupportedCapabilities
                    : EnvironmentPresentationCapabilities.None,
                lastAppliedRevision,
                CountWarnings(),
                CountErrors());
            return status;
        }

        private int CountWarnings()
        {
            int count = 0;
            for (int index = 0; index < diagnostics.Count; index++)
            {
                if (diagnostics[index].Severity ==
                    EnvironmentDiagnosticSeverity.Warning)
                {
                    count++;
                }
            }

            return count;
        }

        private int CountErrors()
        {
            int count = 0;
            for (int index = 0; index < diagnostics.Count; index++)
            {
                if (diagnostics[index].Severity ==
                    EnvironmentDiagnosticSeverity.Error)
                {
                    count++;
                }
            }

            return count;
        }

        private static bool HasAnyParticleSystem(ParticleSystem[] systems)
        {
            for (int index = 0; index < systems.Length; index++)
            {
                if (systems[index] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static float ExposureContextFactor(WeatherExposureContext context)
        {
            switch (context)
            {
                case WeatherExposureContext.Interior:
                    return 1f;
                case WeatherExposureContext.Sheltered:
                    return 0.4f;
                default:
                    return 0f;
            }
        }

        private void ResolveLocalContextSource()
        {
            localContextSource =
                localContextSourceComponent as ILocalWeatherContextSource;
        }

        private LocalWeatherContext ResolveLocalContext()
        {
            if (localContextSource != null)
            {
                return localContextSource.Current;
            }

            float indoor = ExposureContextFactor(currentValues.ExposureContext);
            float precipitation = currentValues.ExposureContext ==
                WeatherExposureContext.Interior
                ? 0f
                : currentValues.ExposureContext ==
                    WeatherExposureContext.Sheltered
                    ? 0.08f
                    : 1f;
            return new LocalWeatherContext(
                null,
                currentValues.ExposureContext == WeatherExposureContext.Interior,
                currentValues.ExposureContext != WeatherExposureContext.Exterior,
                1f - indoor,
                precipitation,
                1f - indoor,
                1f - indoor,
                1f - indoor * 0.8f,
                1f - indoor * 0.8f,
                1f - indoor * 0.35f,
                indoor,
                precipitation,
                1f - indoor,
                indoor * 1.35f);
        }

        private static float TwilightFactor(float solarElevationDegrees)
        {
            float risesFromNight = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(-12f, -5f, solarElevationDegrees));
            float fadesIntoDay = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(-1f, 8f, solarElevationDegrees));
            return risesFromNight * fadesIntoDay;
        }

        private static float NightReadabilityFactor(
            float solarElevationDegrees) =>
            1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(-12f, 4f, solarElevationDegrees));

        private static bool Is01(float value) =>
            IsFinite(value) && value >= 0f && value < 1.000001f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private sealed class PrecipitationPresentationState
        {
            private readonly ParticleSystem system;
            private readonly bool loop;
            private readonly bool playOnAwake;
            private readonly ParticleSystemSimulationSpace simulationSpace;
            private readonly ParticleSystemCullingMode cullingMode;
            private readonly ParticleSystem.MinMaxCurve startLifetime;
            private readonly ParticleSystem.MinMaxCurve startSpeed;
            private readonly ParticleSystem.MinMaxCurve startSize;
            private readonly ParticleSystem.MinMaxGradient startColor;
            private readonly ParticleSystem.MinMaxCurve gravityModifier;
            private readonly int maxParticles;
            private readonly bool shapeEnabled;
            private readonly ParticleSystemShapeType shapeType;
            private readonly Vector3 shapeScale;
            private readonly bool velocityEnabled;
            private readonly ParticleSystemSimulationSpace velocitySpace;
            private readonly ParticleSystem.MinMaxCurve velocityX;
            private readonly ParticleSystem.MinMaxCurve velocityY;
            private readonly ParticleSystem.MinMaxCurve velocityZ;
            private readonly bool collisionEnabled;
            private readonly ParticleSystemCollisionType collisionType;
            private readonly ParticleSystemCollisionMode collisionMode;
            private readonly LayerMask collisionLayers;
            private readonly ParticleSystem.MinMaxCurve collisionDampen;
            private readonly ParticleSystem.MinMaxCurve collisionBounce;
            private readonly ParticleSystem.MinMaxCurve collisionLifetimeLoss;
            private readonly float collisionRadiusScale;
            private readonly ParticleSystemCollisionQuality collisionQuality;
            private readonly bool dynamicColliders;
            private readonly int maximumCollisionShapes;
            private readonly bool sendCollisionMessages;
            private readonly ParticleSystemRenderer renderer;
            private readonly ParticleSystemRenderMode renderMode;
            private readonly float velocityScale;
            private readonly float lengthScale;
            private readonly float sortingFudge;
            private readonly ShadowCastingMode shadowCastingMode;
            private readonly bool receiveShadows;
            private readonly Material sharedMaterial;

            public PrecipitationPresentationState(ParticleSystem source)
            {
                system = source;
                if (source == null)
                {
                    return;
                }

                ParticleSystem.MainModule main = source.main;
                loop = main.loop;
                playOnAwake = main.playOnAwake;
                simulationSpace = main.simulationSpace;
                cullingMode = main.cullingMode;
                startLifetime = main.startLifetime;
                startSpeed = main.startSpeed;
                startSize = main.startSize;
                startColor = main.startColor;
                gravityModifier = main.gravityModifier;
                maxParticles = main.maxParticles;

                ParticleSystem.ShapeModule shape = source.shape;
                shapeEnabled = shape.enabled;
                shapeType = shape.shapeType;
                shapeScale = shape.scale;

                ParticleSystem.VelocityOverLifetimeModule velocity =
                    source.velocityOverLifetime;
                velocityEnabled = velocity.enabled;
                velocitySpace = velocity.space;
                velocityX = velocity.x;
                velocityY = velocity.y;
                velocityZ = velocity.z;

                ParticleSystem.CollisionModule collision = source.collision;
                collisionEnabled = collision.enabled;
                collisionType = collision.type;
                collisionMode = collision.mode;
                collisionLayers = collision.collidesWith;
                collisionDampen = collision.dampen;
                collisionBounce = collision.bounce;
                collisionLifetimeLoss = collision.lifetimeLoss;
                collisionRadiusScale = collision.radiusScale;
                collisionQuality = collision.quality;
                dynamicColliders = collision.enableDynamicColliders;
                maximumCollisionShapes = collision.maxCollisionShapes;
                sendCollisionMessages = collision.sendCollisionMessages;

                renderer = source.GetComponent<ParticleSystemRenderer>();
                if (renderer == null)
                {
                    return;
                }

                renderMode = renderer.renderMode;
                velocityScale = renderer.velocityScale;
                lengthScale = renderer.lengthScale;
                sortingFudge = renderer.sortingFudge;
                shadowCastingMode = renderer.shadowCastingMode;
                receiveShadows = renderer.receiveShadows;
                sharedMaterial = renderer.sharedMaterial;
            }

            public void Restore()
            {
                if (system == null)
                {
                    return;
                }

                ParticleSystem.MainModule main = system.main;
                main.loop = loop;
                main.playOnAwake = playOnAwake;
                main.simulationSpace = simulationSpace;
                main.cullingMode = cullingMode;
                main.startLifetime = startLifetime;
                main.startSpeed = startSpeed;
                main.startSize = startSize;
                main.startColor = startColor;
                main.gravityModifier = gravityModifier;
                main.maxParticles = maxParticles;

                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = shapeEnabled;
                shape.shapeType = shapeType;
                shape.scale = shapeScale;

                ParticleSystem.VelocityOverLifetimeModule velocity =
                    system.velocityOverLifetime;
                velocity.enabled = velocityEnabled;
                velocity.space = velocitySpace;
                velocity.x = velocityX;
                velocity.y = velocityY;
                velocity.z = velocityZ;

                ParticleSystem.CollisionModule collision = system.collision;
                collision.enabled = collisionEnabled;
                collision.type = collisionType;
                collision.mode = collisionMode;
                collision.collidesWith = collisionLayers;
                collision.dampen = collisionDampen;
                collision.bounce = collisionBounce;
                collision.lifetimeLoss = collisionLifetimeLoss;
                collision.radiusScale = collisionRadiusScale;
                collision.quality = collisionQuality;
                collision.enableDynamicColliders = dynamicColliders;
                collision.maxCollisionShapes = maximumCollisionShapes;
                collision.sendCollisionMessages = sendCollisionMessages;

                if (renderer == null)
                {
                    return;
                }

                renderer.renderMode = renderMode;
                renderer.velocityScale = velocityScale;
                renderer.lengthScale = lengthScale;
                renderer.sortingFudge = sortingFudge;
                renderer.shadowCastingMode = shadowCastingMode;
                renderer.receiveShadows = receiveShadows;
                renderer.sharedMaterial = sharedMaterial;
            }
        }

        private readonly struct NativeWeatherValues
        {
            public NativeWeatherValues(
                float timeOfDay01,
                float cloudCoverage01,
                float cloudIntensity01,
                float precipitation01,
                float drizzle01,
                float fogIntensity01,
                float visibilityMeters,
                Vector2 windDirectionXZ,
                float windSpeedMetersPerSecond,
                float windGustMetersPerSecond,
                WeatherExposureContext exposureContext)
            {
                TimeOfDay01 = timeOfDay01;
                CloudCoverage01 = cloudCoverage01;
                CloudIntensity01 = cloudIntensity01;
                Precipitation01 = precipitation01;
                Drizzle01 = drizzle01;
                FogIntensity01 = fogIntensity01;
                VisibilityMeters = visibilityMeters;
                WindDirectionXZ = windDirectionXZ.sqrMagnitude > 0.000001f
                    ? windDirectionXZ.normalized
                    : Vector2.up;
                WindSpeedMetersPerSecond = windSpeedMetersPerSecond;
                WindGustMetersPerSecond = windGustMetersPerSecond;
                ExposureContext = exposureContext;
            }

            public float TimeOfDay01 { get; }
            public float CloudCoverage01 { get; }
            public float CloudIntensity01 { get; }
            public float Precipitation01 { get; }
            public float Drizzle01 { get; }
            public float FogIntensity01 { get; }
            public float VisibilityMeters { get; }
            public Vector2 WindDirectionXZ { get; }
            public float WindSpeedMetersPerSecond { get; }
            public float WindGustMetersPerSecond { get; }
            public WeatherExposureContext ExposureContext { get; }

            public static NativeWeatherValues From(
                in EnvironmentPresentationFrame frame)
            {
                float drizzle = frame.PrecipitationType ==
                    EnvironmentPrecipitationType.Rain &&
                    frame.PrecipitationIntensity01 < 0.3f
                    ? 1f - frame.PrecipitationIntensity01 / 0.3f
                    : 0f;
                return new NativeWeatherValues(
                    frame.NormalizedTimeOfDay01,
                    frame.CloudCoverage01,
                    frame.CloudIntensity01,
                    frame.PrecipitationIntensity01,
                    drizzle * frame.PrecipitationIntensity01,
                    frame.FogMistIntensity01,
                    frame.VisibilityMeters,
                    frame.WindDirectionXZ,
                    frame.WindSpeedMetersPerSecond,
                    frame.WindGustSpeedMetersPerSecond,
                    frame.ExposureContext);
            }

            public static NativeWeatherValues Lerp(
                in NativeWeatherValues from,
                in NativeWeatherValues to,
                float progress01)
            {
                float t = Mathf.Clamp01(progress01);
                Vector2 direction = Vector2.Lerp(
                    from.WindDirectionXZ,
                    to.WindDirectionXZ,
                    t);
                if (direction.sqrMagnitude > 0.000001f)
                {
                    direction.Normalize();
                }
                else
                {
                    direction = to.WindDirectionXZ;
                }

                float timeAngle = Mathf.LerpAngle(
                    from.TimeOfDay01 * 360f,
                    to.TimeOfDay01 * 360f,
                    t);
                return new NativeWeatherValues(
                    Mathf.Repeat(timeAngle / 360f, 1f),
                    Mathf.Lerp(from.CloudCoverage01, to.CloudCoverage01, t),
                    Mathf.Lerp(from.CloudIntensity01, to.CloudIntensity01, t),
                    Mathf.Lerp(from.Precipitation01, to.Precipitation01, t),
                    Mathf.Lerp(from.Drizzle01, to.Drizzle01, t),
                    Mathf.Lerp(from.FogIntensity01, to.FogIntensity01, t),
                    Mathf.Lerp(from.VisibilityMeters, to.VisibilityMeters, t),
                    direction,
                    Mathf.Lerp(
                        from.WindSpeedMetersPerSecond,
                        to.WindSpeedMetersPerSecond,
                        t),
                    Mathf.Lerp(
                        from.WindGustMetersPerSecond,
                        to.WindGustMetersPerSecond,
                        t),
                    t < 0.5f ? from.ExposureContext : to.ExposureContext);
            }
        }
    }
}
