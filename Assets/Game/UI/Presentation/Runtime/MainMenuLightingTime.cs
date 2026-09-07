using System;

namespace MSC.UI.Presentation
{
    /// <summary>
    /// Artistic menu lighting driven by the computer's local wall-clock time.
    /// The caller supplies DateTime.Now; no timezone, date, latitude or weather
    /// conversion takes place here. This is not an astronomical sun model.
    /// </summary>
    public static class MainMenuLightingTime
    {
        public static MainMenuLightingState Evaluate(DateTime localTime) =>
            Evaluate(localTime.TimeOfDay);

        public static MainMenuLightingState Evaluate(TimeSpan timeOfDay)
        {
            double hours = timeOfDay.TotalHours % 24d;
            if (hours < 0d) hours += 24d;

            float daylight = 0f;
            float twilight = 0f;
            float night = 0f;
            if (hours < 6d || hours >= 22d)
            {
                night = 1f;
            }
            else if (hours < 7d)
            {
                twilight = SmoothStep(hours - 6d);
                night = 1f - twilight;
            }
            else if (hours < 8d)
            {
                daylight = SmoothStep(hours - 7d);
                twilight = 1f - daylight;
            }
            else if (hours < 18d)
            {
                daylight = 1f;
            }
            else if (hours < 20d)
            {
                twilight = SmoothStep((hours - 18d) / 2d);
                daylight = 1f - twilight;
            }
            else
            {
                night = SmoothStep((hours - 20d) / 2d);
                twilight = 1f - night;
            }

            float elevation;
            if (hours < 6d || hours >= 22d)
            {
                elevation = -8f;
            }
            else if (hours < 8d)
            {
                elevation = -8f + 24f * SmoothStep((hours - 6d) / 2d);
            }
            else if (hours < 18d)
            {
                // Squared sine has zero slope at both daytime boundaries.
                double arc = Math.Sin(Math.PI * (hours - 8d) / 10d);
                elevation = 16f + 44f * (float)(arc * arc);
            }
            else
            {
                elevation = 16f - 24f * SmoothStep((hours - 18d) / 4d);
            }

            return new MainMenuLightingState(
                daylight, twilight, night,
                daylight * 60000f + twilight * 10000f + night * 2400f,
                daylight * 22000f + twilight * 10000f + night * 9000f,
                daylight * 6500f + twilight * 2600f + night * 1800f,
                daylight * 0.35f + twilight * 0.75f + night,
                elevation,
                (float)(hours * 15d));
        }

        private static float SmoothStep(double value)
        {
            double clamped = Math.Max(0d, Math.Min(1d, value));
            return (float)(clamped * clamped * (3d - 2d * clamped));
        }
    }

    /// <summary>
    /// Normalized day/twilight/night weights also provide a color-blending
    /// contract to the renderer. Sun angles are degrees; azimuth wraps at 360.
    /// </summary>
    public readonly struct MainMenuLightingState
    {
        internal MainMenuLightingState(
            float daylight, float twilight, float night,
            float keyLux, float fillLux, float skyIntensity, float lampFactor,
            float sunElevationDegrees, float sunAzimuthDegrees)
        {
            Daylight = daylight;
            Twilight = twilight;
            Night = night;
            KeyLux = keyLux;
            FillLux = fillLux;
            SkyIntensity = skyIntensity;
            LampFactor = lampFactor;
            SunElevationDegrees = sunElevationDegrees;
            SunAzimuthDegrees = sunAzimuthDegrees;
        }

        public float Daylight { get; }
        public float Twilight { get; }
        public float Night { get; }
        public float KeyLux { get; }
        public float FillLux { get; }
        public float SkyIntensity { get; }
        public float LampFactor { get; }
        public float SunElevationDegrees { get; }
        public float SunAzimuthDegrees { get; }
    }
}
