using System;
using System.Collections;
using MSC.Weather.Presentation;
using UnityEngine;

namespace MSC.Development.WeatherLab
{
    public sealed class WeatherLabStateController : MonoBehaviour
    {
        private const int FixedYear = 1995;
        private const int FixedMonth = 8;
        private const int FixedDay = 1;

        [SerializeField] private MonoBehaviour adapterBehaviour;
        [SerializeField] private Camera[] presentationCameras = Array.Empty<Camera>();
        [SerializeField] private Transform lightningTarget;
        [SerializeField] private EnvironmentPresentationPresetKind initialPreset =
            EnvironmentPresentationPresetKind.Clear;
        [SerializeField] private EnvironmentQualityTier initialQuality =
            EnvironmentQualityTier.High;

        private IEnvironmentPresentationAdapter adapter;
        private EnvironmentPresentationPresetKind currentPreset;
        private EnvironmentQualityTier currentQuality;
        private ulong revision;
        private uint lightningSequence;

        public EnvironmentPresentationPresetKind CurrentPreset => currentPreset;

        public EnvironmentQualityTier CurrentQuality => currentQuality;

        public EnvironmentPresentationStatus Status => adapter?.Status ??
            EnvironmentPresentationStatus.Detached;

        public void ConfigureForAuthoring(
            MonoBehaviour authoredAdapter,
            Camera[] authoredCameras,
            Transform authoredLightningTarget)
        {
            adapterBehaviour = authoredAdapter;
            presentationCameras = authoredCameras ?? Array.Empty<Camera>();
            lightningTarget = authoredLightningTarget;
            initialPreset = EnvironmentPresentationPresetKind.Clear;
            initialQuality = EnvironmentQualityTier.High;
        }

        public EnvironmentPresentationStatus ApplyPreset(
            EnvironmentPresentationPresetKind preset)
        {
            EnsureAdapter();
            currentPreset = preset;
            return PresentCurrentState(EnvironmentLightningVisualRequest.None);
        }

        public EnvironmentPresentationStatus ApplyQuality(EnvironmentQualityTier quality)
        {
            EnsureAdapter();
            currentQuality = quality;
            return PresentCurrentState(EnvironmentLightningVisualRequest.None);
        }

        public EnvironmentPresentationStatus TriggerAmbientLightning()
        {
            EnsureAdapter();
            if (lightningTarget == null)
            {
                throw new InvalidOperationException("WeatherLab lightning target is not assigned.");
            }

            lightningSequence++;
            var request = new EnvironmentLightningVisualRequest(
                true,
                lightningSequence,
                lightningTarget.position,
                1f);
            return PresentCurrentState(request);
        }

        public bool SwitchCamera(int cameraIndex)
        {
            EnsureAdapter();
            if (presentationCameras == null || cameraIndex < 0 ||
                cameraIndex >= presentationCameras.Length ||
                presentationCameras[cameraIndex] == null)
            {
                return false;
            }

            for (int index = 0; index < presentationCameras.Length; index++)
            {
                if (presentationCameras[index] != null)
                {
                    presentationCameras[index].enabled = index == cameraIndex;
                }
            }

            return adapter is IEnvironmentPresentationCameraTarget cameraTarget &&
                   cameraTarget.TrySetPresentationCamera(presentationCameras[cameraIndex]);
        }

        private void Awake()
        {
            EnsureAdapter();
            currentPreset = initialPreset;
            currentQuality = initialQuality;
        }

        private IEnumerator Start()
        {
            const int maximumStartupFrames = 8;
            for (int attempt = 0; attempt < maximumStartupFrames && !adapter.IsAttached; attempt++)
            {
                adapter.Attach();
                if (!adapter.IsAttached)
                {
                    yield return null;
                }
            }

            if (adapter.IsAttached)
            {
                SwitchCamera(0);
                PresentCurrentState(EnvironmentLightningVisualRequest.None);
            }
        }

        private void OnDisable()
        {
            adapter?.Detach();
        }

        private void EnsureAdapter()
        {
            if (adapter != null)
            {
                return;
            }

            adapter = adapterBehaviour as IEnvironmentPresentationAdapter;
            if (adapter == null)
            {
                throw new InvalidOperationException(
                    "WeatherLab adapter must implement IEnvironmentPresentationAdapter.");
            }
        }

        private EnvironmentPresentationStatus PresentCurrentState(
            EnvironmentLightningVisualRequest lightning)
        {
            if (!adapter.IsAttached)
            {
                EnvironmentPresentationStatus attached = adapter.Attach();
                if (!attached.IsOperational)
                {
                    return attached;
                }

                if (!adapter.IsAttached)
                {
                    return attached;
                }
            }

            PresetValues values = GetPresetValues(currentPreset);
            if (!EnvironmentBindingId.TryParse(values.BindingId, out EnvironmentBindingId bindingId))
            {
                throw new InvalidOperationException(
                    "WeatherLab contains an invalid built-in environment binding ID.");
            }

            revision++;
            var frame = new EnvironmentPresentationFrame(
                revision,
                true,
                FixedYear,
                FixedMonth,
                FixedDay,
                values.NormalizedTime,
                bindingId,
                values.CloudType,
                values.CloudCoverage,
                values.CloudIntensity,
                values.PrecipitationType,
                values.PrecipitationIntensity,
                values.FogIntensity,
                new Vector2(0.7071068f, 0.7071068f),
                values.WindSpeed,
                values.WindGust,
                lightning,
                EnvironmentRefreshRequest.None,
                currentQuality,
                0f);
            return adapter.Present(frame);
        }

        private static PresetValues GetPresetValues(
            EnvironmentPresentationPresetKind preset)
        {
            switch (preset)
            {
                case EnvironmentPresentationPresetKind.Clear:
                    return new PresetValues(
                        "weather.clear", 0.52f, EnvironmentCloudType.Clear,
                        0f, 0.15f, EnvironmentPrecipitationType.None,
                        0f, 0.05f, 2f, 4f);
                case EnvironmentPresentationPresetKind.Overcast:
                    return new PresetValues(
                        "weather.overcast", 0.55f, EnvironmentCloudType.Overcast,
                        0.85f, 0.8f, EnvironmentPrecipitationType.None,
                        0f, 0.25f, 4f, 7f);
                case EnvironmentPresentationPresetKind.Rain:
                    return new PresetValues(
                        "weather.rain", 0.62f, EnvironmentCloudType.Overcast,
                        0.9f, 0.9f, EnvironmentPrecipitationType.Rain,
                        0.5f, 0.35f, 6f, 10f);
                case EnvironmentPresentationPresetKind.Storm:
                    return new PresetValues(
                        "weather.storm_visual", 0.68f, EnvironmentCloudType.Storm,
                        1f, 1f, EnvironmentPrecipitationType.Rain,
                        1f, 0.65f, 10f, 16f);
                case EnvironmentPresentationPresetKind.Night:
                    return new PresetValues(
                        "weather.night", 0.96f, EnvironmentCloudType.Clear,
                        0.1f, 0.2f, EnvironmentPrecipitationType.None,
                        0f, 0.15f, 2f, 5f);
                case EnvironmentPresentationPresetKind.Mist:
                    return new PresetValues(
                        "weather.fog", 0.28f, EnvironmentCloudType.Scattered,
                        0.3f, 0.4f, EnvironmentPrecipitationType.None,
                        0f, 1f, 1f, 3f);
                default:
                    throw new ArgumentOutOfRangeException(nameof(preset), preset, null);
            }
        }

        private readonly struct PresetValues
        {
            public PresetValues(
                string bindingId,
                float normalizedTime,
                EnvironmentCloudType cloudType,
                float cloudCoverage,
                float cloudIntensity,
                EnvironmentPrecipitationType precipitationType,
                float precipitationIntensity,
                float fogIntensity,
                float windSpeed,
                float windGust)
            {
                BindingId = bindingId;
                NormalizedTime = normalizedTime;
                CloudType = cloudType;
                CloudCoverage = cloudCoverage;
                CloudIntensity = cloudIntensity;
                PrecipitationType = precipitationType;
                PrecipitationIntensity = precipitationIntensity;
                FogIntensity = fogIntensity;
                WindSpeed = windSpeed;
                WindGust = windGust;
            }

            public string BindingId { get; }
            public float NormalizedTime { get; }
            public EnvironmentCloudType CloudType { get; }
            public float CloudCoverage { get; }
            public float CloudIntensity { get; }
            public EnvironmentPrecipitationType PrecipitationType { get; }
            public float PrecipitationIntensity { get; }
            public float FogIntensity { get; }
            public float WindSpeed { get; }
            public float WindGust { get; }
        }
    }
}
