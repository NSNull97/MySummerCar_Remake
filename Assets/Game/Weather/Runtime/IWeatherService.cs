using MSC.Weather.Domain;

namespace MSC.Weather
{
    /// <summary>
    /// Read-only project weather authority. Presentation backends must consume mapped
    /// outputs and must never write vendor state back through this boundary.
    /// </summary>
    public interface IWeatherService
    {
        WeatherState CurrentState { get; }

        WeatherSnapshot Snapshot { get; }

        WeatherTimeline Timeline { get; }

        uint Revision { get; }

        bool IsScheduleFrozen { get; }

        /// <summary>
        /// Compatibility projection retained for existing gameplay consumers.
        /// </summary>
        float RainIntensity { get; }
    }
}
