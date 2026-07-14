using System;
using System.Security.Cryptography;
using System.Text;
using MSC.Core.Identity;

namespace MSC.World.Data
{
    [Serializable]
    public readonly struct WorldStableId : IEquatable<WorldStableId>
    {
        private readonly string value;

        private WorldStableId(string value)
        {
            this.value = value;
        }

        public string Value => value ?? string.Empty;

        public bool IsValid => StableEntityId.TryParse(Value, out _);

        public static bool TryParse(string serializedValue, out WorldStableId stableId)
        {
            stableId = default;
            if (!StableEntityId.TryParse(serializedValue, out _))
            {
                return false;
            }

            stableId = new WorldStableId(serializedValue);
            return true;
        }

        public bool Equals(WorldStableId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is WorldStableId other && Equals(other);

        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

        public override string ToString() => Value;
    }

    public static class WorldStableIdUtility
    {
        public static WorldStableId Create(
            string sourceId,
            string sceneName,
            long sourceObjectId,
            long transformId,
            string hierarchyPath,
            string role)
        {
            string canonical = string.Join(
                "|",
                Normalize(sourceId),
                Normalize(sceneName),
                sourceObjectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                transformId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Normalize(hierarchyPath),
                Normalize(role));

            byte[] hash;
            using (SHA256 sha256 = SHA256.Create())
            {
                hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
            }

            var builder = new StringBuilder(32);
            for (int index = 0; index < 16; index++)
            {
                builder.Append(hash[index].ToString("x2"));
            }

            if (!WorldStableId.TryParse(builder.ToString(), out WorldStableId stableId))
            {
                throw new InvalidOperationException("Deterministic world stable ID generation produced an invalid value.");
            }

            return stableId;
        }

        private static string Normalize(string value) => (value ?? string.Empty).Trim().ToLowerInvariant();
    }
}
