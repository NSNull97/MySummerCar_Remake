using System;
using UnityEngine;

namespace MSC.Weather.System.Local
{
    /// <summary>
    /// Validation boundary for an authored building. It has no gameplay state;
    /// streaming can load and unload it without touching global weather.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeatherBuildingEnvelope : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private MonoBehaviour[] interiorZoneComponents =
            Array.Empty<MonoBehaviour>();
        [SerializeField] private DoorWeatherPortalAdapter[] doorPortals =
            Array.Empty<DoorWeatherPortalAdapter>();
        [SerializeField] private WeatherPrecipitationBlocker[] precipitationBlockers =
            Array.Empty<WeatherPrecipitationBlocker>();

        public string StableId => stableId;
        public MonoBehaviour[] InteriorZoneComponents => interiorZoneComponents;
        public DoorWeatherPortalAdapter[] DoorPortals => doorPortals;
        public WeatherPrecipitationBlocker[] PrecipitationBlockers =>
            precipitationBlockers;

        public bool HasValidInteriorZone
        {
            get
            {
                if (interiorZoneComponents == null)
                {
                    return false;
                }

                for (int index = 0; index < interiorZoneComponents.Length; index++)
                {
                    if (interiorZoneComponents[index] is IInteriorZone zone &&
                        !string.IsNullOrWhiteSpace(zone.StableId))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void ConfigureForAuthoring(
            string authoredStableId,
            MonoBehaviour[] authoredZones,
            DoorWeatherPortalAdapter[] authoredPortals,
            WeatherPrecipitationBlocker[] authoredBlockers)
        {
            stableId = authoredStableId?.Trim() ?? string.Empty;
            interiorZoneComponents = authoredZones ?? Array.Empty<MonoBehaviour>();
            doorPortals = authoredPortals ?? Array.Empty<DoorWeatherPortalAdapter>();
            precipitationBlockers = authoredBlockers ??
                Array.Empty<WeatherPrecipitationBlocker>();
        }
    }
}
