using System;

namespace MSC.Core.Time
{
    /// <summary>
    /// Exact mutable clock state represented as an immutable value. A sub-tick
    /// remainder preserves progression across save/restore without quantization.
    /// </summary>
    public readonly struct GameTimeState : IEquatable<GameTimeState>
    {
        public GameTimeState(
            long elapsedGameTicks,
            double fractionalGameTickRemainder,
            double timeScale,
            bool isPaused)
        {
            if (elapsedGameTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedGameTicks));
            }

            if (!GameTimeConfig.IsFinite(fractionalGameTickRemainder) ||
                fractionalGameTickRemainder < 0d ||
                fractionalGameTickRemainder >= 1d)
            {
                throw new ArgumentOutOfRangeException(nameof(fractionalGameTickRemainder));
            }

            if (!GameTimeConfig.IsValidTimeScale(timeScale))
            {
                throw new ArgumentOutOfRangeException(nameof(timeScale));
            }

            ElapsedGameTicks = elapsedGameTicks;
            FractionalGameTickRemainder = fractionalGameTickRemainder;
            TimeScale = timeScale;
            IsPaused = isPaused;
        }

        public long ElapsedGameTicks { get; }

        public double FractionalGameTickRemainder { get; }

        public double TimeScale { get; }

        public bool IsPaused { get; }

        public double ElapsedGameSeconds =>
            (ElapsedGameTicks + FractionalGameTickRemainder) /
            GameTimeConfig.TicksPerGameSecond;

        public bool Equals(GameTimeState other)
        {
            return ElapsedGameTicks == other.ElapsedGameTicks &&
                   FractionalGameTickRemainder.Equals(other.FractionalGameTickRemainder) &&
                   TimeScale.Equals(other.TimeScale) &&
                   IsPaused == other.IsPaused;
        }

        public override bool Equals(object obj)
        {
            return obj is GameTimeState other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ElapsedGameTicks.GetHashCode();
                hash = hash * 397 ^ FractionalGameTickRemainder.GetHashCode();
                hash = hash * 397 ^ TimeScale.GetHashCode();
                hash = hash * 397 ^ IsPaused.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(GameTimeState left, GameTimeState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameTimeState left, GameTimeState right)
        {
            return !left.Equals(right);
        }
    }
}
