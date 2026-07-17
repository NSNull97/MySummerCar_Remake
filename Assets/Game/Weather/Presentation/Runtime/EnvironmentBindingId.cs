using System;

namespace MSC.Weather.Presentation
{
    /// <summary>
    /// Project-owned stable identifier used to resolve presentation bindings without
    /// relying on vendor display names, array indices, scene objects, or instance IDs.
    /// </summary>
    [Serializable]
    public readonly struct EnvironmentBindingId : IEquatable<EnvironmentBindingId>
    {
        public const int MinimumLength = 3;
        public const int MaximumLength = 64;

        private readonly string value;

        private EnvironmentBindingId(string value)
        {
            this.value = value;
        }

        public string Value => value ?? string.Empty;

        public bool IsValid => TryParse(Value, out _);

        public static bool TryParse(string serializedValue, out EnvironmentBindingId bindingId)
        {
            bindingId = default;

            if (string.IsNullOrEmpty(serializedValue) ||
                serializedValue.Length < MinimumLength ||
                serializedValue.Length > MaximumLength)
            {
                return false;
            }

            if (!IsAsciiLetterOrDigit(serializedValue[0]) ||
                !IsAsciiLetterOrDigit(serializedValue[serializedValue.Length - 1]))
            {
                return false;
            }

            bool previousWasSeparator = false;
            for (int index = 0; index < serializedValue.Length; index++)
            {
                char character = serializedValue[index];
                bool isSeparator = character == '.' || character == '-' || character == '_';
                if (!IsAsciiLetterOrDigit(character) && !isSeparator)
                {
                    return false;
                }

                if (isSeparator && previousWasSeparator)
                {
                    return false;
                }

                previousWasSeparator = isSeparator;
            }

            bindingId = new EnvironmentBindingId(serializedValue);
            return true;
        }

        public bool Equals(EnvironmentBindingId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is EnvironmentBindingId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(EnvironmentBindingId left, EnvironmentBindingId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EnvironmentBindingId left, EnvironmentBindingId right)
        {
            return !left.Equals(right);
        }

        private static bool IsAsciiLetterOrDigit(char character)
        {
            return character >= 'a' && character <= 'z' ||
                   character >= '0' && character <= '9';
        }
    }
}
