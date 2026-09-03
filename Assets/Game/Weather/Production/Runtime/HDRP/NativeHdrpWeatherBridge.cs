using System;
using System.Collections.Generic;
using MSC.Weather.Domain;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Weather.Production
{
    /// <summary>
    /// The sole hybrid writer for the active native HDRP Fog and Exposure
    /// overrides. It owns a runtime clone and never mutates the shared asset.
    /// </summary>
    [DefaultExecutionOrder(240)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Volume))]
    public sealed class NativeHdrpWeatherBridge : MonoBehaviour
    {
        private static readonly ProfilerMarker BridgeMarker =
            new ProfilerMarker("MSC.HDRPWeatherBridge");

        // Accepted in-game visual calibration captured on 2026-09-02.
        // These are production offsets, not mutable development controls.
        public const float ProductionColorGradeStrength = 1.36f;
        public const float ProductionTemperatureOffset = 27.14f;
        public const float ProductionTintOffset = 2.76f;
        public const float ProductionContrastOffset = 0.75f;
        public const float ProductionSaturationOffset = 1.21f;
        public const float ProductionExposureOffsetEv = 0.41f;

        [SerializeField] private ProductionWeatherStateSource weatherStateSource;
        [SerializeField] private WeatherExposureResolver weatherExposureResolver;
        [SerializeField] private HybridWeatherRenderProfile renderProfile;
        [SerializeField] private Volume globalVolume;
        [SerializeField] private HDAdditionalLightData sunLight;
        [SerializeField] private HDAdditionalLightData moonLight;
        [SerializeField, Min(0.05f)] private float updateIntervalSeconds = 0.1f;
        [SerializeField, Min(1f)] private float minimumFogMeanFreePathMeters = 20f;
        [SerializeField, Min(1f)] private float maximumFogMeanFreePathMeters = 6000f;
        [SerializeField] private Vector2 exposureLimitsEv = new Vector2(-1f, 14f);
        [SerializeField, Range(-1f, 16f)] private float baseFixedExposureEv =
            NativeHdrpExposureMath.FinnishSummerDaylightFixedExposureEv;
        [SerializeField, Range(-1f, 16f)] private float readableNightFixedExposureEv = 7.25f;
        [SerializeField, Range(0f, 4f)] private float maximumIndoorExposureLiftEv = 2f;
        [SerializeField, Range(0f, 1f)] private float minimumIndoorIndirectDiffuse = 0.18f;
        [SerializeField, Range(0f, 1f)] private float minimumIndoorReflection = 0.35f;

        // Kept non-serialized so stale development values in existing scenes
        // cannot override the accepted code-owned calibration.
        private float runtimeColorGradeStrength = ProductionColorGradeStrength;
        private float runtimeTemperatureOffset = ProductionTemperatureOffset;
        private float runtimeTintOffset = ProductionTintOffset;
        private float runtimeContrastOffset = ProductionContrastOffset;
        private float runtimeSaturationOffset = ProductionSaturationOffset;
        private float runtimeExposureOffsetEv = ProductionExposureOffsetEv;

        private readonly List<UnityEngine.Object> runtimeObjects =
            new List<UnityEngine.Object>(12);
        private VolumeProfile authoredProfile;
        private VolumeProfile runtimeProfile;
        private Fog fog;
        private Exposure exposure;
        private IndirectLightingController indirectLighting;
        private ColorAdjustments colorAdjustments;
        private WhiteBalance whiteBalance;
        private WeatherRuntimeState pendingState;
        private bool hasPendingState;
        private float nextApplyTime;

        public bool IsReady =>
            globalVolume != null && runtimeProfile != null &&
            fog != null && exposure != null && colorAdjustments != null &&
            whiteBalance != null;
        public float CurrentFogMeanFreePathMeters =>
            fog != null ? fog.meanFreePath.value : float.NaN;
        public float CurrentExposureCompensationEv =>
            exposure != null ? exposure.compensation.value : float.NaN;
        public float CurrentFixedExposureEv =>
            exposure != null ? exposure.fixedExposure.value : float.NaN;
        public float CurrentIndirectDiffuseMultiplier =>
            indirectLighting != null
                ? indirectLighting.indirectDiffuseLightingMultiplier.value
                : float.NaN;
        public bool UsesCameraIndependentExposure =>
            exposure != null && exposure.active &&
            exposure.mode.value == ExposureMode.Fixed;
        public float RuntimeColorGradeStrength => runtimeColorGradeStrength;
        public float RuntimeTemperatureOffset => runtimeTemperatureOffset;
        public float RuntimeTintOffset => runtimeTintOffset;
        public float RuntimeContrastOffset => runtimeContrastOffset;
        public float RuntimeSaturationOffset => runtimeSaturationOffset;
        public float RuntimeExposureOffsetEv => runtimeExposureOffsetEv;
        public string FogOwner => nameof(NativeHdrpWeatherBridge);
        public string ExposureOwner => nameof(NativeHdrpWeatherBridge);

        public void SetRuntimeColorTuning(
            float gradeStrength,
            float temperatureOffset,
            float tintOffset,
            float contrastOffset,
            float saturationOffset,
            float exposureOffsetEv)
        {
            runtimeColorGradeStrength = Mathf.Clamp(gradeStrength, 0f, 2f);
            runtimeTemperatureOffset = Mathf.Clamp(
                temperatureOffset,
                -50f,
                50f);
            runtimeTintOffset = Mathf.Clamp(tintOffset, -50f, 50f);
            runtimeContrastOffset = Mathf.Clamp(contrastOffset, -30f, 30f);
            runtimeSaturationOffset = Mathf.Clamp(
                saturationOffset,
                -30f,
                30f);
            runtimeExposureOffsetEv = Mathf.Clamp(exposureOffsetEv, -2f, 2f);
            RequestRuntimeReapply();
        }

        public void ResetRuntimeColorTuning()
        {
            SetRuntimeColorTuning(
                ProductionColorGradeStrength,
                ProductionTemperatureOffset,
                ProductionTintOffset,
                ProductionContrastOffset,
                ProductionSaturationOffset,
                ProductionExposureOffsetEv);
        }

        public string GetRuntimeColorTuningSummary()
        {
            return string.Format(
                global::System.Globalization.CultureInfo.InvariantCulture,
                "grade={0:F2}; temperature={1:F2}; tint={2:F2}; " +
                "contrast={3:F2}; saturation={4:F2}; exposureEV={5:F2}",
                runtimeColorGradeStrength,
                runtimeTemperatureOffset,
                runtimeTintOffset,
                runtimeContrastOffset,
                runtimeSaturationOffset,
                runtimeExposureOffsetEv);
        }

        public void ConfigureForAuthoring(
            ProductionWeatherStateSource authoredWeatherStateSource,
            WeatherExposureResolver authoredWeatherExposureResolver,
            HybridWeatherRenderProfile authoredRenderProfile,
            Volume authoredGlobalVolume,
            HDAdditionalLightData authoredSun,
            HDAdditionalLightData authoredMoon)
        {
            weatherStateSource = authoredWeatherStateSource;
            weatherExposureResolver = authoredWeatherExposureResolver;
            renderProfile = authoredRenderProfile;
            globalVolume = authoredGlobalVolume;
            sunLight = authoredSun;
            moonLight = authoredMoon;
            exposureLimitsEv = new Vector2(-1f, 14f);
            baseFixedExposureEv =
                NativeHdrpExposureMath.FinnishSummerDaylightFixedExposureEv;
            readableNightFixedExposureEv = 7.25f;
            maximumIndoorExposureLiftEv = 2f;
        }

        private void Awake()
        {
            if (globalVolume == null)
            {
                globalVolume = GetComponent<Volume>();
            }

            PrepareRuntimeProfile();
        }

        private void OnEnable()
        {
            if (runtimeProfile == null)
            {
                PrepareRuntimeProfile();
            }

            if (weatherStateSource != null)
            {
                weatherStateSource.StateChanged += HandleWeatherChanged;
                if (weatherStateSource.IsReady)
                {
                    HandleWeatherChanged(weatherStateSource.Current);
                }
            }
        }

        private void OnDisable()
        {
            if (weatherStateSource != null)
            {
                weatherStateSource.StateChanged -= HandleWeatherChanged;
            }

            RestoreAuthoredProfile();
            DestroyRuntimeProfile();
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            bool periodicReassert = pendingState.IsValid && now >= nextApplyTime;
            if (!hasPendingState && !periodicReassert)
            {
                return;
            }

            // New authoritative values apply on the same rendered frame. The
            // configured interval now only bounds a safety reassert when the
            // source is unchanged; it no longer quantizes fog into visible steps.
            nextApplyTime = now + updateIntervalSeconds;
            Apply(pendingState);
            hasPendingState = false;
        }

        private void HandleWeatherChanged(WeatherRuntimeState state)
        {
            if (!state.IsValid)
            {
                return;
            }

            pendingState = state;
            hasPendingState = true;
        }

        public void ApplyImmediately(in WeatherRuntimeState state)
        {
            if (!state.IsValid)
            {
                throw new ArgumentException("Weather runtime state is invalid.", nameof(state));
            }

            Apply(state);
            pendingState = state;
            hasPendingState = false;
        }

        private void Apply(in WeatherRuntimeState state)
        {
            using (BridgeMarker.Auto())
            {
                if (!IsReady)
                {
                    return;
                }

                HybridWeatherRenderSettings from = renderProfile != null
                    ? renderProfile.Resolve(state.CurrentWeatherId)
                    : HybridWeatherRenderSettings.Default;
                HybridWeatherRenderSettings to = renderProfile != null
                    ? renderProfile.Resolve(state.TargetWeatherId)
                    : from;
                HybridWeatherRenderSettings settings =
                    HybridWeatherRenderSettings.Lerp(
                        from,
                        to,
                        state.WeatherTransition01);

                float meanFreePath = Mathf.Clamp(
                    NativeHdrpFogMath.MeanFreePathFromVisibility(
                        state.VisibilityMeters),
                    minimumFogMeanFreePathMeters,
                    maximumFogMeanFreePathMeters);
                fog.active = true;
                fog.enabled.Override(true);
                fog.meanFreePath.Override(meanFreePath);
                fog.baseHeight.Override(settings.BaseHeightMeters);
                fog.maximumHeight.Override(Mathf.Max(
                    settings.BaseHeightMeters + 1f,
                    settings.MaximumHeightMeters));
                fog.maxFogDistance.Override(Mathf.Max(
                    1f,
                    settings.MaximumFogDistanceMeters));
                fog.colorMode.Override(FogColorMode.SkyColor);
                fog.tint.Override(settings.FogTint);
                fog.enableVolumetricFog.Override(true);
                fog.albedo.Override(settings.SingleScatteringAlbedo);
                fog.anisotropy.Override(settings.Anisotropy);
                fog.globalLightProbeDimmer.Override(1f);

                float indoorFactor = weatherExposureResolver != null
                    ? weatherExposureResolver.Current.IndoorFactor
                    : 0f;
                float exteriorExposureEv =
                    NativeHdrpExposureMath.CalculateFixedExposureEv(
                        baseFixedExposureEv,
                        readableNightFixedExposureEv,
                        state.TimeOfDay01,
                        settings.ExposureCompensationEv,
                        state.AmbientDarkness01,
                        exposureLimitsEv);
                exposure.active = true;
                exposure.mode.Override(ExposureMode.Fixed);
                exposure.fixedExposure.Override(
                    NativeHdrpExposureMath.ApplyIndoorExposureLift(
                        exteriorExposureEv + runtimeExposureOffsetEv,
                        indoorFactor,
                        maximumIndoorExposureLiftEv,
                        exposureLimitsEv));
                exposure.compensation.Override(0f);
                exposure.limitMin.Override(Mathf.Min(
                    exposureLimitsEv.x,
                    exposureLimitsEv.y));
                exposure.limitMax.Override(Mathf.Max(
                    exposureLimitsEv.x,
                    exposureLimitsEv.y));

                if (indirectLighting != null)
                {
                    indirectLighting.active = true;
                    indirectLighting.indirectDiffuseLightingMultiplier.Override(
                        NativeHdrpExposureMath
                            .CalculateIndirectDiffuseMultiplier(
                                state.SunVisibility01,
                                indoorFactor,
                                minimumIndoorIndirectDiffuse));
                    float reflectionMultiplier = Mathf.Lerp(
                        1f,
                        minimumIndoorReflection,
                        Mathf.Clamp01(indoorFactor));
                    indirectLighting.reflectionLightingMultiplier.Override(
                        reflectionMultiplier);
                    indirectLighting.reflectionProbeIntensityMultiplier.Override(
                        reflectionMultiplier);
                }

                float gradeWeight = NativeHdrpColorGradingMath
                    .CalculateGradeWeight(state.TimeOfDay01, indoorFactor) *
                    runtimeColorGradeStrength;
                colorAdjustments.active = true;
                colorAdjustments.postExposure.Override(0f);
                colorAdjustments.contrast.Override(
                    NativeHdrpColorGradingMath.ApplyWeight(
                        settings.DaytimeContrast,
                        gradeWeight) + runtimeContrastOffset);
                colorAdjustments.colorFilter.Override(
                    NativeHdrpColorGradingMath.ApplyWeight(
                        settings.DaytimeColorFilter,
                        gradeWeight));
                colorAdjustments.hueShift.Override(0f);
                colorAdjustments.saturation.Override(
                    NativeHdrpColorGradingMath.ApplyWeight(
                        settings.DaytimeSaturation,
                        gradeWeight) + runtimeSaturationOffset);

                whiteBalance.active = true;
                whiteBalance.temperature.Override(
                    NativeHdrpColorGradingMath.ApplyWeight(
                        settings.DaytimeTemperature,
                        gradeWeight) + runtimeTemperatureOffset);
                whiteBalance.tint.Override(
                    NativeHdrpColorGradingMath.ApplyWeight(
                        settings.DaytimeTint,
                        gradeWeight) + runtimeTintOffset);

                ApplyVolumetricDimmer(
                    sunLight,
                    settings.VolumetricLightDimmer);
                ApplyVolumetricDimmer(
                    moonLight,
                    settings.VolumetricLightDimmer);
            }
        }

        private void PrepareRuntimeProfile()
        {
            if (globalVolume == null || globalVolume.sharedProfile == null)
            {
                enabled = false;
                return;
            }

            authoredProfile = globalVolume.sharedProfile;
            runtimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            runtimeProfile.name = authoredProfile.name + " (Hybrid Runtime)";
            runtimeProfile.hideFlags = HideFlags.DontSave;
            runtimeObjects.Add(runtimeProfile);
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

            fog = GetOrAddRuntimeComponent<Fog>();
            exposure = GetOrAddRuntimeComponent<Exposure>();
            indirectLighting = GetOrAddRuntimeComponent<IndirectLightingController>();
            colorAdjustments = GetOrAddRuntimeComponent<ColorAdjustments>();
            whiteBalance = GetOrAddRuntimeComponent<WhiteBalance>();
            globalVolume.sharedProfile = runtimeProfile;
        }

        private void RequestRuntimeReapply()
        {
            if (!pendingState.IsValid)
            {
                return;
            }

            hasPendingState = true;
            nextApplyTime = 0f;
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
            if (globalVolume != null && authoredProfile != null &&
                globalVolume.sharedProfile == runtimeProfile)
            {
                globalVolume.sharedProfile = authoredProfile;
            }
        }

        private void DestroyRuntimeProfile()
        {
            for (int index = runtimeObjects.Count - 1; index >= 0; index--)
            {
                if (runtimeObjects[index] != null)
                {
                    Destroy(runtimeObjects[index]);
                }
            }

            runtimeObjects.Clear();
            runtimeProfile = null;
            fog = null;
            exposure = null;
            indirectLighting = null;
            colorAdjustments = null;
            whiteBalance = null;
        }

        private static void ApplyVolumetricDimmer(
            HDAdditionalLightData light,
            float value)
        {
            if (light == null)
            {
                return;
            }

            float dimmer = Mathf.Clamp01(value);
            light.volumetricDimmer = dimmer;
            light.volumetricShadowDimmer = dimmer;
        }
    }
}
