using System;
using System.Security.Cryptography;
using System.Text;
using MSC.Core.Identity;

namespace MSC.Items
{
    public static class ItemStableIdUtility
    {
        private const string NamespacePrefix =
            "msc-remake.phase1.items.09b.v1|";

        public static StableEntityId CreateDeterministic(string projectOwnedKey)
        {
            if (string.IsNullOrWhiteSpace(projectOwnedKey))
            {
                throw new ArgumentException(
                    "A project-owned item identity key is required.",
                    nameof(projectOwnedKey));
            }

            byte[] input = Encoding.UTF8.GetBytes(
                NamespacePrefix + projectOwnedKey);
            byte[] hash;
            using (SHA256 algorithm = SHA256.Create())
            {
                hash = algorithm.ComputeHash(input);
            }

            byte[] guidBytes = new byte[16];
            Array.Copy(hash, guidBytes, guidBytes.Length);
            guidBytes[7] = (byte)((guidBytes[7] & 0x0f) | 0x50);
            guidBytes[8] = (byte)((guidBytes[8] & 0x3f) | 0x80);
            string serialized = new Guid(guidBytes).ToString("N");
            if (!StableEntityId.TryParse(serialized, out StableEntityId result))
            {
                throw new InvalidOperationException(
                    "Deterministic item identity generation failed.");
            }

            return result;
        }
    }
}
