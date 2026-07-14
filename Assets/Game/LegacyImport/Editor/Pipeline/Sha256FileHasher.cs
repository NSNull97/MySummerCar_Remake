using System;
using System.IO;
using System.Security.Cryptography;

namespace MSC.LegacyImport.Editor.Pipeline
{
    public static class Sha256FileHasher
    {
        public static string Compute(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("File path must not be empty.", nameof(path));
            }

            using (FileStream stream = File.OpenRead(path))
            using (SHA256 algorithm = SHA256.Create())
            {
                byte[] digest = algorithm.ComputeHash(stream);
                return BitConverter.ToString(digest).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        public static bool Matches(string path, string expectedCanonicalHash)
        {
            if (!Sha256Digest.IsCanonical(expectedCanonicalHash))
            {
                return false;
            }

            return string.Equals(
                Compute(path),
                expectedCanonicalHash,
                StringComparison.Ordinal);
        }
    }
}
