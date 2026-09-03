using MSC.Weather.Production;
using UnityEngine;

namespace MSC.Weather.System.Local
{
    /// <summary>
    /// Non-destructive adapter over the accepted 07C WeatherZone. Its collider,
    /// stable ID and authored independent exposures remain authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WeatherZone))]
    public sealed class LegacyWeatherZoneAdapter : MonoBehaviour, IInteriorZone
    {
        [SerializeField] private WeatherZone legacyZone;
        [SerializeField, Min(0.1f)] private float minimumTransitionDistance = 1.2f;

        public string StableId => legacyZone != null
            ? legacyZone.StableId
            : string.Empty;
        public int Priority => legacyZone != null ? legacyZone.Priority : int.MinValue;
        public bool IsAvailable =>
            isActiveAndEnabled && legacyZone != null && legacyZone.IsConfigured;

        public LocalWeatherZoneSettings Settings
        {
            get
            {
                if (legacyZone == null || legacyZone.Profile == null)
                {
                    return LocalWeatherZoneSettings.ClosedInterior;
                }

                WeatherExposureState exposure =
                    legacyZone.Profile.CreateClosedExposure();
                return new LocalWeatherZoneSettings(
                    1f - exposure.EnclosureFactor,
                    exposure.PrecipitationExposure,
                    exposure.FogExposure,
                    exposure.WindExposure,
                    exposure.WeatherAudioExposure,
                    exposure.WeatherAudioExposure,
                    exposure.ThunderExposure,
                    exposure.IndoorFactor * 1.1f,
                    Mathf.Max(
                        minimumTransitionDistance,
                        legacyZone.Profile.FogVoidBlendDistanceMeters));
            }
        }

        private void Reset()
        {
            legacyZone = GetComponent<WeatherZone>();
        }

        private void Awake()
        {
            if (legacyZone == null)
            {
                legacyZone = GetComponent<WeatherZone>();
            }
        }

        private void OnEnable()
        {
            WeatherPortalSystem.ActiveChanged += HandleRegistryChanged;
            WeatherPortalSystem.Active?.RegisterZone(this);
        }

        private void OnDisable()
        {
            WeatherPortalSystem.ActiveChanged -= HandleRegistryChanged;
            WeatherPortalSystem.Active?.UnregisterZone(this);
        }

        public bool Contains(Vector3 worldPosition) =>
            IsAvailable && legacyZone.Contains(worldPosition);

        public void Configure(
            WeatherZone authoredLegacyZone,
            float authoredTransitionDistance = 1.2f)
        {
            legacyZone = authoredLegacyZone;
            minimumTransitionDistance = Mathf.Max(
                0.1f,
                authoredTransitionDistance);
            if (isActiveAndEnabled)
            {
                WeatherPortalSystem.Active?.UnregisterZone(this);
                WeatherPortalSystem.Active?.RegisterZone(this);
            }
        }

        public void ConfigureForAuthoring(
            WeatherZone authoredLegacyZone,
            float authoredTransitionDistance = 1.2f) =>
            Configure(authoredLegacyZone, authoredTransitionDistance);

        private void HandleRegistryChanged(WeatherPortalSystem registry)
        {
            registry?.RegisterZone(this);
        }
    }
}
