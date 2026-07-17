using System;

namespace MSC.Core.Time
{
    /// <summary>
    /// Allocation-free Gregorian calendar date used by the project clock.
    /// </summary>
    public readonly struct GameDate : IEquatable<GameDate>, IComparable<GameDate>
    {
        public GameDate(int year, int month, int day)
        {
            if (!IsValid(year, month, day))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    $"Invalid Gregorian game date: {year:D4}-{month:D2}-{day:D2}.");
            }

            Year = year;
            Month = month;
            Day = day;
        }

        public int Year { get; }

        public int Month { get; }

        public int Day { get; }

        public static bool TryCreate(int year, int month, int day, out GameDate date)
        {
            if (!IsValid(year, month, day))
            {
                date = default;
                return false;
            }

            date = new GameDate(year, month, day);
            return true;
        }

        public bool TryAddDays(long days, out GameDate date)
        {
            long currentDayNumber = ToDayNumber();
            long targetDayNumber;
            try
            {
                targetDayNumber = checked(currentDayNumber + days);
            }
            catch (OverflowException)
            {
                date = default;
                return false;
            }

            long maximumDayNumber = DateTime.MaxValue.Date.Ticks / TimeSpan.TicksPerDay;
            if (targetDayNumber < 0 || targetDayNumber > maximumDayNumber)
            {
                date = default;
                return false;
            }

            var projected = new DateTime(targetDayNumber * TimeSpan.TicksPerDay);
            date = new GameDate(projected.Year, projected.Month, projected.Day);
            return true;
        }

        public long DaysUntil(GameDate other)
        {
            return other.ToDayNumber() - ToDayNumber();
        }

        public int CompareTo(GameDate other)
        {
            return ToDayNumber().CompareTo(other.ToDayNumber());
        }

        public bool Equals(GameDate other)
        {
            return Year == other.Year && Month == other.Month && Day == other.Day;
        }

        public override bool Equals(object obj)
        {
            return obj is GameDate other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Year;
                hash = hash * 397 ^ Month;
                hash = hash * 397 ^ Day;
                return hash;
            }
        }

        public override string ToString()
        {
            return $"{Year:D4}-{Month:D2}-{Day:D2}";
        }

        public static bool operator ==(GameDate left, GameDate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameDate left, GameDate right)
        {
            return !left.Equals(right);
        }

        private static bool IsValid(int year, int month, int day)
        {
            if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1)
            {
                return false;
            }

            return day <= DateTime.DaysInMonth(year, month);
        }

        private long ToDayNumber()
        {
            return new DateTime(Year, Month, Day).Ticks / TimeSpan.TicksPerDay;
        }
    }
}
