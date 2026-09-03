using System;
using MSC.Weather.Domain;

namespace MSC.Weather
{
    /// <summary>
    /// Read-only normalized weather feed. Implementations may adapt the project
    /// domain or a test source but must not select or advance weather themselves.
    /// </summary>
    public interface IWeatherStateSource
    {
        WeatherRuntimeState Current { get; }

        uint Revision { get; }

        bool IsReady { get; }

        event Action<WeatherRuntimeState> StateChanged;
    }
}
