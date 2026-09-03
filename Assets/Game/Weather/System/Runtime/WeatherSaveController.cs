using System;
using UnityEngine;

namespace MSC.Weather.System
{
    /// <summary>
    /// Versioned, backend-free serialization facade for the climate director.
    /// Storage ownership remains with the native save coordinator.
    /// </summary>
    public sealed class WeatherSaveController
    {
        private readonly WeatherDirector director;

        public WeatherSaveController(WeatherDirector weatherDirector)
        {
            director = weatherDirector ?? throw new ArgumentNullException(
                nameof(weatherDirector));
        }

        public WeatherDirectorSnapshot Capture() => director.CaptureSnapshot();

        public string CaptureJson(bool prettyPrint = false) =>
            JsonUtility.ToJson(Capture(), prettyPrint);

        public void Restore(WeatherDirectorSnapshot snapshot) =>
            director.Restore(snapshot);

        public void RestoreJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Weather save JSON is empty.", nameof(json));
            }

            WeatherDirectorSnapshot snapshot =
                JsonUtility.FromJson<WeatherDirectorSnapshot>(json);
            if (snapshot == null)
            {
                throw new ArgumentException("Weather save JSON could not be parsed.", nameof(json));
            }

            Restore(snapshot);
        }
    }
}
