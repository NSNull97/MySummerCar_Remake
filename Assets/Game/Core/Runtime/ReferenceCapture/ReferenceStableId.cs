using System;
using System.Security.Cryptography;
using System.Text;

namespace MSC.Core.ReferenceCapture
{
    public readonly struct ReferenceStableId : IEquatable<ReferenceStableId>
    {
        public ReferenceStableId(string value)
        {
            if (!IsCanonical(value))
                throw new ArgumentException("Reference stable ID must contain 32 lowercase hexadecimal characters.", nameof(value));
            Value = value;
        }

        public string Value { get; }
        public bool Equals(ReferenceStableId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ReferenceStableId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;

        public static bool IsCanonical(string value)
        {
            if (value == null || value.Length != 32) return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))) return false;
            }
            return true;
        }
    }

    public static class ReferenceStableIdUtility
    {
        public static ReferenceStableId Create(string canonicalKey)
        {
            if (string.IsNullOrWhiteSpace(canonicalKey))
                throw new ArgumentException("Canonical reference key must not be empty.", nameof(canonicalKey));

            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalKey.Trim().ToLowerInvariant()));
            var builder = new StringBuilder(32);
            for (int index = 0; index < 16; index++) builder.Append(hash[index].ToString("x2"));
            return new ReferenceStableId(builder.ToString());
        }
    }
}
