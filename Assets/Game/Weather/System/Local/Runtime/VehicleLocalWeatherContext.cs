using System;
using UnityEngine;

namespace MSC.Weather.System.Local
{
    /// <summary>
    /// Separate cabin context. It does not register as a building room and can
    /// consume future door/window implementations through IEnvironmentPortal.
    /// Body-rain and listener-rain exposure remain distinct domain outputs.
    /// </summary>
    [DefaultExecutionOrder(-355)]
    [DisallowMultipleComponent]
    public sealed class VehicleLocalWeatherContext : MonoBehaviour,
        ILocalWeatherContextSource,
        IInteriorZone
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField, Range(0f, 1f)] private float closedOutdoorExposure01 = 0.1f;
        [SerializeField, Range(0f, 1f)] private float closedListenerRainExposure01 = 0.08f;
        [SerializeField, Range(0f, 1f)] private float bodyRainExposure01 = 1f;
        [SerializeField, Range(0f, 1f)] private float closedFogExposure01 = 0.03f;
        [SerializeField, Range(0f, 1f)] private float closedWindExposure01 = 0.08f;
        [SerializeField, Range(0f, 1f)] private float closedAudioLeak01 = 0.22f;
        [SerializeField, Range(0f, 1f)] private float closedThunderExposure01 = 0.68f;
        [SerializeField] private float exposureCompensationEv = 0.65f;
        [SerializeField] private MonoBehaviour[] apertureComponents =
            Array.Empty<MonoBehaviour>();

        private IEnvironmentPortal[] apertures = Array.Empty<IEnvironmentPortal>();

        public event Action<LocalWeatherContext> ContextChanged;

        public string StableId => stableId;
        public int Priority => int.MaxValue;
        public bool IsAvailable =>
            isActiveAndEnabled && !string.IsNullOrWhiteSpace(stableId);
        public LocalWeatherZoneSettings Settings =>
            new LocalWeatherZoneSettings(
                closedOutdoorExposure01,
                closedListenerRainExposure01,
                closedFogExposure01,
                closedWindExposure01,
                closedAudioLeak01,
                closedAudioLeak01,
                closedThunderExposure01,
                exposureCompensationEv,
                1f);
        public LocalWeatherContext Current { get; private set; } =
            LocalWeatherContext.Outdoor;
        public float ListenerRainExposure01 =>
            Current.PrecipitationExposure01;
        public float BodyRainExposure01 => bodyRainExposure01;

        private void Awake()
        {
            ResolveApertures();
            Recompute();
        }

        private void OnEnable()
        {
            ResolveApertures();
            Subscribe();
            Recompute();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        public bool Contains(Vector3 worldPosition) => false;

        public void ConfigureForAuthoring(
            string authoredStableId,
            params MonoBehaviour[] authoredApertures)
        {
            Unsubscribe();
            stableId = authoredStableId?.Trim() ?? string.Empty;
            apertureComponents = authoredApertures ?? Array.Empty<MonoBehaviour>();
            ResolveApertures();
            if (isActiveAndEnabled)
            {
                Subscribe();
            }

            Recompute();
        }

        private void ResolveApertures()
        {
            if (apertureComponents == null || apertureComponents.Length == 0)
            {
                apertures = Array.Empty<IEnvironmentPortal>();
                return;
            }

            apertures = new IEnvironmentPortal[apertureComponents.Length];
            for (int index = 0; index < apertureComponents.Length; index++)
            {
                apertures[index] = apertureComponents[index] as IEnvironmentPortal;
            }
        }

        private void Subscribe()
        {
            for (int index = 0; index < apertures.Length; index++)
            {
                if (apertures[index] != null)
                {
                    apertures[index].TransmissionChanged += HandleApertureChanged;
                }
            }
        }

        private void Unsubscribe()
        {
            for (int index = 0; index < apertures.Length; index++)
            {
                if (apertures[index] != null)
                {
                    apertures[index].TransmissionChanged -= HandleApertureChanged;
                }
            }
        }

        private void HandleApertureChanged(IEnvironmentPortal portal)
        {
            Recompute();
        }

        private void Recompute()
        {
            PortalTransmission transmission = PortalTransmission.Blocked;
            float openness = 0f;
            for (int index = 0; index < apertures.Length; index++)
            {
                IEnvironmentPortal aperture = apertures[index];
                if (aperture == null || !aperture.IsAvailable)
                {
                    continue;
                }

                transmission = PortalTransmission.Max(
                    transmission,
                    aperture.Transmission);
                openness = Mathf.Max(openness, aperture.Openness01);
            }

            LocalWeatherContext next = new LocalWeatherContext(
                this,
                true,
                true,
                Leak(closedOutdoorExposure01, transmission.Visual01),
                Leak(
                    closedListenerRainExposure01,
                    transmission.Precipitation01),
                Leak(closedFogExposure01, transmission.Fog01),
                Leak(closedWindExposure01, transmission.Wind01),
                Leak(closedAudioLeak01, transmission.Audio01),
                Leak(closedAudioLeak01, transmission.Audio01),
                Leak(closedThunderExposure01, transmission.Thunder01),
                1f - Mathf.Clamp01(transmission.Visual01),
                0f,
                Mathf.Max(openness, transmission.Audio01),
                exposureCompensationEv *
                (1f - transmission.Visual01));
            if (!next.Equals(Current))
            {
                Current = next;
                ContextChanged?.Invoke(Current);
            }
            else
            {
                Current = next;
            }
        }

        private static float Leak(float closedValue, float transmission01) =>
            Mathf.Lerp(
                Mathf.Clamp01(closedValue),
                1f,
                Mathf.Clamp01(transmission01));
    }
}
