using System;

namespace MSC.Core.Time
{
    /// <summary>
    /// Read-only derived clock view. Formatting is deliberately absent so hot-path
    /// consumers do not allocate date or time strings.
    /// </summary>
    public readonly struct GameTimeSnapshot : IEquatable<GameTimeSnapshot>
    {
        internal GameTimeSnapshot(
            ulong revision,
            GameDate date,
            long dayIndex,
            long timeOfDayTicks,
            double fractionalGameTickRemainder,
            double elapsedGameSeconds,
            double timeScale,
            bool isPaused,
            double sunriseNormalized01,
            double sunsetNormalized01)
        {
            Revision = revision;
            Date = date;
            DayIndex = dayIndex;
            TimeOfDayTicks = timeOfDayTicks;
            FractionalGameTickRemainder = fractionalGameTickRemainder;
            ElapsedGameSeconds = elapsedGameSeconds;
            TimeScale = timeScale;
            IsPaused = isPaused;
            SunriseNormalized01 = sunriseNormalized01;
            SunsetNormalized01 = sunsetNormalized01;
        }

        public ulong Revision { get; }

        public GameDate Date { get; }

        public long DayIndex { get; }

        public long TimeOfDayTicks { get; }

        public double FractionalGameTickRemainder { get; }

        public double SecondsOfDay =>
            (TimeOfDayTicks + FractionalGameTickRemainder) /
            GameTimeConfig.TicksPerGameSecond;

        public double NormalizedTimeOfDay01 =>
            (TimeOfDayTicks + FractionalGameTickRemainder) /
            GameTimeConfig.TicksPerGameDay;

        public double ElapsedGameSeconds { get; }

        public double TimeScale { get; }

        public bool IsPaused { get; }

        public double SunriseNormalized01 { get; }

        public double SunsetNormalized01 { get; }

        public bool IsDaylight =>
            NormalizedTimeOfDay01 >= SunriseNormalized01 &&
            NormalizedTimeOfDay01 < SunsetNormalized01;

        public bool Equals(GameTimeSnapshot other)
        {
            return Revision == other.Revision &&
                   Date == other.Date &&
                   DayIndex == other.DayIndex &&
                   TimeOfDayTicks == other.TimeOfDayTicks &&
                   FractionalGameTickRemainder.Equals(other.FractionalGameTickRemainder) &&
                   ElapsedGameSeconds.Equals(other.ElapsedGameSeconds) &&
                   TimeScale.Equals(other.TimeScale) &&
                   IsPaused == other.IsPaused &&
                   SunriseNormalized01.Equals(other.SunriseNormalized01) &&
                   SunsetNormalized01.Equals(other.SunsetNormalized01);
        }

        public override bool Equals(object obj)
        {
            return obj is GameTimeSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Revision.GetHashCode();
                hash = hash * 397 ^ Date.GetHashCode();
                hash = hash * 397 ^ DayIndex.GetHashCode();
                hash = hash * 397 ^ TimeOfDayTicks.GetHashCode();
                return hash;
            }
        }

        internal GameTimeSnapshot WithRevision(ulong revision)
        {
            return new GameTimeSnapshot(
                revision,
                Date,
                DayIndex,
                TimeOfDayTicks,
                FractionalGameTickRemainder,
                ElapsedGameSeconds,
                TimeScale,
                IsPaused,
                SunriseNormalized01,
                SunsetNormalized01);
        }
    }

    internal static class GameTimeProjection
    {
        public static bool TryCreateSnapshot(
            GameTimeConfig config,
            GameTimeState state,
            ulong revision,
            out GameTimeSnapshot snapshot)
        {
            snapshot = default;
            if (config == null || state.ElapsedGameTicks > config.MaximumElapsedGameTicks)
            {
                return false;
            }

            long absoluteTicks = config.StartTimeOfDayTicks + state.ElapsedGameTicks;
            long dayIndex = absoluteTicks / GameTimeConfig.TicksPerGameDay;
            long timeOfDayTicks = absoluteTicks % GameTimeConfig.TicksPerGameDay;
            if (!config.StartDate.TryAddDays(dayIndex, out GameDate date))
            {
                return false;
            }

            snapshot = new GameTimeSnapshot(
                revision,
                date,
                dayIndex,
                timeOfDayTicks,
                state.FractionalGameTickRemainder,
                state.ElapsedGameSeconds,
                state.TimeScale,
                state.IsPaused,
                config.SunriseNormalized01,
                config.SunsetNormalized01);
            return true;
        }
    }
}
