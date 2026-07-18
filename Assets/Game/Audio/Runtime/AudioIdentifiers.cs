using System;

namespace MSC.Audio
{
    public static class AudioIdValidation
    {
        public static bool TryValidate(string value, out string failure) =>
            AudioStableId.TryValidate(value, out failure);
    }

    internal static class AudioStableId
    {
        public const int MaximumLength = 160;

        public static string Validate(string value, string argumentName)
        {
            if (!TryValidate(value, out string failure))
            {
                throw new ArgumentException(failure, argumentName);
            }

            return value;
        }

        public static bool TryValidate(string value, out string failure)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                failure = "Audio stable ID is required.";
                return false;
            }

            if (value.Length > MaximumLength)
            {
                failure = $"Audio stable ID exceeds {MaximumLength} characters.";
                return false;
            }

            bool previousWasSeparator = true;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool alphaNumeric =
                    character >= 'a' && character <= 'z' ||
                    character >= '0' && character <= '9';
                bool separator = character == '.' || character == '_' || character == '-';
                if (!alphaNumeric && !separator)
                {
                    failure =
                        "Audio stable IDs may contain only lower-case ASCII letters, " +
                        "digits, '.', '_' and '-'.";
                    return false;
                }

                if (separator && previousWasSeparator)
                {
                    failure = "Audio stable IDs may not start with or repeat separators.";
                    return false;
                }

                previousWasSeparator = separator;
            }

            if (previousWasSeparator)
            {
                failure = "Audio stable IDs may not end with a separator.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public readonly struct AudioEventId : IEquatable<AudioEventId>
    {
        public AudioEventId(string value) => Value = AudioStableId.Validate(value, nameof(value));

        public string Value { get; }
        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public bool Equals(AudioEventId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is AudioEventId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AudioEventId left, AudioEventId right) => left.Equals(right);
        public static bool operator !=(AudioEventId left, AudioEventId right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct AudioParameterId : IEquatable<AudioParameterId>
    {
        public AudioParameterId(string value) => Value = AudioStableId.Validate(value, nameof(value));

        public string Value { get; }
        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public bool Equals(AudioParameterId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is AudioParameterId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AudioParameterId left, AudioParameterId right) => left.Equals(right);
        public static bool operator !=(AudioParameterId left, AudioParameterId right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct AudioSwitchId : IEquatable<AudioSwitchId>
    {
        public AudioSwitchId(string value) => Value = AudioStableId.Validate(value, nameof(value));

        public string Value { get; }
        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public bool Equals(AudioSwitchId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is AudioSwitchId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AudioSwitchId left, AudioSwitchId right) => left.Equals(right);
        public static bool operator !=(AudioSwitchId left, AudioSwitchId right) => !left.Equals(right);
    }

    [Serializable]
    public readonly struct AudioStateId : IEquatable<AudioStateId>
    {
        public AudioStateId(string value) => Value = AudioStableId.Validate(value, nameof(value));

        public string Value { get; }
        public bool IsEmpty => string.IsNullOrEmpty(Value);

        public bool Equals(AudioStateId other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is AudioStateId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(AudioStateId left, AudioStateId right) => left.Equals(right);
        public static bool operator !=(AudioStateId left, AudioStateId right) => !left.Equals(right);
    }
}
