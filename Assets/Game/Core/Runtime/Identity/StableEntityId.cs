using System;

namespace MSC.Core.Identity
{
    /// <summary>
    /// Project-owned persistent identity stored as a canonical lower-case GUID without separators.
    /// </summary>
    [Serializable]
    public readonly struct StableEntityId : IEquatable<StableEntityId>
    {
        public const int SerializedLength = 32;

        private readonly string value;

        private StableEntityId(string value)
        {
            this.value = value;
        }

        public string Value => value ?? string.Empty;

        public bool IsValid => TryParse(Value, out _);

        public static StableEntityId New()
        {
            return new StableEntityId(Guid.NewGuid().ToString("N"));
        }

        public static bool TryParse(string serializedValue, out StableEntityId entityId)
        {
            entityId = default;

            if (string.IsNullOrEmpty(serializedValue) || serializedValue.Length != SerializedLength)
            {
                return false;
            }

            if (!Guid.TryParseExact(serializedValue, "N", out Guid parsed))
            {
                return false;
            }

            string canonicalValue = parsed.ToString("N");
            if (!string.Equals(serializedValue, canonicalValue, StringComparison.Ordinal))
            {
                return false;
            }

            entityId = new StableEntityId(canonicalValue);
            return true;
        }

        public bool Equals(StableEntityId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is StableEntityId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(StableEntityId left, StableEntityId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(StableEntityId left, StableEntityId right)
        {
            return !left.Equals(right);
        }
    }
}
