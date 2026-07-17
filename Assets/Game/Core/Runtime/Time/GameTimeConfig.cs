using System;

namespace MSC.Core.Time
{
    public enum GameTimeTuningClassification
    {
        RemakeDesignTarget = 0,
        DonorMeasured = 1
    }

    /// <summary>
    /// Immutable time-domain configuration. The built-in defaults are explicitly
    /// remake design targets because donor timing evidence is currently missing.
    /// </summary>
    public sealed class GameTimeConfig
    {
        private readonly double gameTicksPerSimulationSecondAtScaleOne;

        public const long TicksPerGameSecond = 1_000_000L;
        public const long TicksPerGameDay = 86_400L * TicksPerGameSecond;
        public const double MaximumTimeScale = 1_000d;

        public static readonly GameTimeConfig RemakeDesignTargetDefaults = new GameTimeConfig(
            "time.remake-default.v1",
            new GameDate(1995, 8, 1),
            12d * 60d * 60d,
            dayLengthSimulationSeconds: 1_200d,
            defaultTimeScale: 1d,
            sunriseNormalized01: 0.25d,
            sunsetNormalized01: 0.875d,
            GameTimeTuningClassification.RemakeDesignTarget);

        public GameTimeConfig(
            string configId,
            GameDate startDate,
            double startTimeOfDaySeconds,
            double dayLengthSimulationSeconds,
            double defaultTimeScale,
            double sunriseNormalized01,
            double sunsetNormalized01,
            GameTimeTuningClassification tuningClassification)
        {
            if (!IsValidConfigId(configId))
            {
                throw new ArgumentException(
                    "Config ID must be a canonical lower-case stable ID.",
                    nameof(configId));
            }

            if (!IsFinite(startTimeOfDaySeconds) ||
                startTimeOfDaySeconds < 0d ||
                startTimeOfDaySeconds >= 86_400d)
            {
                throw new ArgumentOutOfRangeException(nameof(startTimeOfDaySeconds));
            }

            if (!IsFinite(dayLengthSimulationSeconds) || dayLengthSimulationSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(dayLengthSimulationSeconds));
            }

            double derivedTickRate = TicksPerGameDay / dayLengthSimulationSeconds;
            if (!IsFinite(derivedTickRate) || derivedTickRate <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(dayLengthSimulationSeconds),
                    "Day length must produce a finite positive game-tick rate.");
            }

            if (!IsValidTimeScale(defaultTimeScale))
            {
                throw new ArgumentOutOfRangeException(nameof(defaultTimeScale));
            }

            if (!IsNormalized(sunriseNormalized01) ||
                !IsNormalized(sunsetNormalized01) ||
                sunriseNormalized01 >= sunsetNormalized01)
            {
                throw new ArgumentException(
                    "Sunrise and sunset must be finite ordered normalized values.");
            }

            if (!Enum.IsDefined(typeof(GameTimeTuningClassification), tuningClassification))
            {
                throw new ArgumentOutOfRangeException(nameof(tuningClassification));
            }

            long startTicks = checked((long)Math.Round(
                startTimeOfDaySeconds * TicksPerGameSecond,
                MidpointRounding.AwayFromZero));
            if (startTicks < 0 || startTicks >= TicksPerGameDay)
            {
                throw new ArgumentOutOfRangeException(nameof(startTimeOfDaySeconds));
            }

            ConfigId = configId;
            StartDate = startDate;
            StartTimeOfDayTicks = startTicks;
            DayLengthSimulationSeconds = dayLengthSimulationSeconds;
            gameTicksPerSimulationSecondAtScaleOne = derivedTickRate;
            DefaultTimeScale = defaultTimeScale;
            SunriseNormalized01 = sunriseNormalized01;
            SunsetNormalized01 = sunsetNormalized01;
            TuningClassification = tuningClassification;

            var finalDate = new GameDate(9999, 12, 31);
            long remainingCalendarDays = startDate.DaysUntil(finalDate);
            MaximumElapsedGameTicks = checked(
                remainingCalendarDays * TicksPerGameDay +
                (TicksPerGameDay - 1L - StartTimeOfDayTicks));
        }

        public string ConfigId { get; }

        public GameDate StartDate { get; }

        public long StartTimeOfDayTicks { get; }

        public double StartTimeOfDaySeconds =>
            StartTimeOfDayTicks / (double)TicksPerGameSecond;

        /// <summary>
        /// Explicit caller-supplied simulation seconds required for one game day
        /// when TimeScale is one.
        /// </summary>
        public double DayLengthSimulationSeconds { get; }

        public double DefaultTimeScale { get; }

        public double SunriseNormalized01 { get; }

        public double SunsetNormalized01 { get; }

        public GameTimeTuningClassification TuningClassification { get; }

        public long MaximumElapsedGameTicks { get; }

        public double GameTicksPerSimulationSecondAtScaleOne =>
            gameTicksPerSimulationSecondAtScaleOne;

        public static bool IsValidTimeScale(double value)
        {
            return IsFinite(value) && value > 0d && value <= MaximumTimeScale;
        }

        internal static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static bool IsNormalized(double value)
        {
            return IsFinite(value) && value >= 0d && value < 1d;
        }

        private static bool IsValidConfigId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 3 || value.Length > 64)
            {
                return false;
            }

            bool previousSeparator = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool alphaNumeric = character >= 'a' && character <= 'z' ||
                                    character >= '0' && character <= '9';
                bool separator = character == '.' || character == '-' || character == '_';
                if (!alphaNumeric && !separator ||
                    separator && (index == 0 || index == value.Length - 1 || previousSeparator))
                {
                    return false;
                }

                previousSeparator = separator;
            }

            return true;
        }
    }
}
