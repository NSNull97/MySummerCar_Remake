using System;
using System.Collections.Generic;

namespace MSC.Weather.Domain
{
    /// <summary>
    /// Project-owned stable identifier for a logical weather state.
    /// It is deliberately independent from Enviro assets and display names.
    /// </summary>
    public readonly struct WeatherStateId : IEquatable<WeatherStateId>, IComparable<WeatherStateId>
    {
        private readonly string value;

        public WeatherStateId(string value)
        {
            if (!IsValid(value))
            {
                throw new ArgumentException(
                    "Weather state IDs must be lower-case ASCII segments separated by dots or underscores.",
                    nameof(value));
            }

            this.value = value;
        }

        public string Value => value ?? string.Empty;

        public bool IsEmpty => string.IsNullOrEmpty(value);

        public static bool TryCreate(string value, out WeatherStateId result)
        {
            if (IsValid(value))
            {
                result = new WeatherStateId(value);
                return true;
            }

            result = default;
            return false;
        }

        public static bool IsValid(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate) || candidate.Length > 64)
            {
                return false;
            }

            bool previousWasSeparator = true;
            for (int index = 0; index < candidate.Length; index++)
            {
                char character = candidate[index];
                bool isAlphaNumeric =
                    (character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9');
                bool isSeparator = character == '.' || character == '_';
                if (!isAlphaNumeric && !isSeparator)
                {
                    return false;
                }

                if (isSeparator && previousWasSeparator)
                {
                    return false;
                }

                previousWasSeparator = isSeparator;
            }

            return !previousWasSeparator;
        }

        public bool Equals(WeatherStateId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is WeatherStateId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public int CompareTo(WeatherStateId other) =>
            string.Compare(Value, other.Value, StringComparison.Ordinal);

        public override string ToString() => Value;

        public static bool operator ==(WeatherStateId left, WeatherStateId right) => left.Equals(right);

        public static bool operator !=(WeatherStateId left, WeatherStateId right) => !left.Equals(right);
    }

    public static class WeatherStateIds
    {
        public static readonly WeatherStateId Clear = new WeatherStateId("weather.clear");
        public static readonly WeatherStateId PartlyCloudy = new WeatherStateId("weather.partly_cloudy");
        public static readonly WeatherStateId Overcast = new WeatherStateId("weather.overcast");
        public static readonly WeatherStateId Drizzle = new WeatherStateId("weather.drizzle");
        public static readonly WeatherStateId SteadyRain = new WeatherStateId("weather.steady_rain");
        public static readonly WeatherStateId HeavyRain = new WeatherStateId("weather.heavy_rain");
        public static readonly WeatherStateId Thunderstorm = new WeatherStateId("weather.thunderstorm");
        public static readonly WeatherStateId MorningMist = new WeatherStateId("weather.morning_mist");
        public static readonly WeatherStateId DenseFog = new WeatherStateId("weather.dense_fog");
        public static readonly WeatherStateId BrightOvercast = new WeatherStateId("weather.bright_overcast");
        public static readonly WeatherStateId HeavyOvercast = new WeatherStateId("weather.heavy_overcast");
        public static readonly WeatherStateId LightRain = new WeatherStateId("weather.light_rain");
        public static readonly WeatherStateId PostRainWet = new WeatherStateId("weather.post_rain_wet");
        public static readonly WeatherStateId ClearingAfterRain = new WeatherStateId("weather.clearing_after_rain");
        public static readonly WeatherStateId ColdClearEvening = new WeatherStateId("weather.cold_clear_evening");
        public static readonly WeatherStateId BlueHour = new WeatherStateId("weather.blue_hour");

        // Keep the original public set stable for milestone 00-08A callers and
        // tests. The seven Finnish-summer states are additive and available
        // through AllKnown without changing the meaning of All.
        private static readonly WeatherStateId[] AllValues =
        {
            Clear,
            PartlyCloudy,
            Overcast,
            Drizzle,
            SteadyRain,
            HeavyRain,
            Thunderstorm,
            MorningMist,
            DenseFog,
        };

        private static readonly WeatherStateId[] AllKnownValues =
        {
            Clear,
            PartlyCloudy,
            Overcast,
            Drizzle,
            SteadyRain,
            HeavyRain,
            Thunderstorm,
            MorningMist,
            DenseFog,
            BrightOvercast,
            HeavyOvercast,
            LightRain,
            PostRainWet,
            ClearingAfterRain,
            ColdClearEvening,
            BlueHour,
        };

        public static IReadOnlyList<WeatherStateId> All => AllValues;

        public static IReadOnlyList<WeatherStateId> AllKnown => AllKnownValues;
    }

    /// <summary>
    /// Seed and stream pair for the versioned project-owned weather random generator.
    /// </summary>
    public readonly struct WeatherSeed : IEquatable<WeatherSeed>
    {
        public WeatherSeed(ulong value, ulong stream = 1UL)
        {
            Value = value;
            Stream = stream;
        }

        public ulong Value { get; }

        public ulong Stream { get; }

        public bool Equals(WeatherSeed other) => Value == other.Value && Stream == other.Stream;

        public override bool Equals(object obj) => obj is WeatherSeed other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Value.GetHashCode() * 397) ^ Stream.GetHashCode();
            }
        }
    }

    public readonly struct WeatherRandomState : IEquatable<WeatherRandomState>
    {
        public const int CurrentVersion = 1;

        public WeatherRandomState(int version, ulong state, ulong increment)
        {
            Version = version;
            State = state;
            Increment = increment;
        }

        public int Version { get; }

        public ulong State { get; }

        public ulong Increment { get; }

        public bool IsValid => Version == CurrentVersion && (Increment & 1UL) != 0UL;

        public bool Equals(WeatherRandomState other) =>
            Version == other.Version && State == other.State && Increment == other.Increment;

        public override bool Equals(object obj) => obj is WeatherRandomState other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = Version;
                hash = (hash * 397) ^ State.GetHashCode();
                hash = (hash * 397) ^ Increment.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// PCG-XSH-RR 32. This generator is versioned and never touches UnityEngine.Random.
    /// </summary>
    public sealed class WeatherRandom
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong state;
        private ulong increment;

        public WeatherRandom(WeatherSeed seed)
        {
            state = 0UL;
            increment = (seed.Stream << 1) | 1UL;
            NextUInt();
            state = unchecked(state + seed.Value);
            NextUInt();
        }

        public WeatherRandom(WeatherRandomState randomState)
        {
            if (!randomState.IsValid)
            {
                throw new ArgumentException("Unsupported or invalid weather RNG state.", nameof(randomState));
            }

            state = randomState.State;
            increment = randomState.Increment;
        }

        public WeatherRandomState CaptureState() =>
            new WeatherRandomState(WeatherRandomState.CurrentVersion, state, increment);

        public void RestoreState(WeatherRandomState randomState)
        {
            if (!randomState.IsValid)
            {
                throw new ArgumentException("Unsupported or invalid weather RNG state.", nameof(randomState));
            }

            state = randomState.State;
            increment = randomState.Increment;
        }

        public uint NextUInt()
        {
            ulong oldState = state;
            state = unchecked((oldState * Multiplier) + increment);
            uint xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
            int rotation = (int)(oldState >> 59);
            return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
        }

        public int NextInt(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            }

            uint bound = (uint)exclusiveMaximum;
            uint threshold = unchecked((uint)(0U - bound)) % bound;
            while (true)
            {
                uint value = NextUInt();
                if (value >= threshold)
                {
                    return (int)(value % bound);
                }
            }
        }

        public float NextFloat01() => (NextUInt() >> 8) * (1f / 16777216f);

        public double NextDouble01()
        {
            ulong high = NextUInt() >> 5;
            ulong low = NextUInt() >> 6;
            return ((high * 67108864.0) + low) * (1.0 / 9007199254740992.0);
        }
    }
}
