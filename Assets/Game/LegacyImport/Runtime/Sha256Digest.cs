using System;

namespace MSC.LegacyImport
{
    public static class Sha256Digest
    {
        public const int HexLength = 64;

        public static bool IsCanonical(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length != HexLength)
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool isDigit = character >= '0' && character <= '9';
                bool isLowerHex = character >= 'a' && character <= 'f';
                if (!isDigit && !isLowerHex)
                {
                    return false;
                }
            }

            return true;
        }

        public static string Canonicalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string canonical = value.Trim().ToLowerInvariant();
            if (!IsCanonical(canonical))
            {
                throw new FormatException("SHA-256 must contain exactly 64 hexadecimal characters.");
            }

            return canonical;
        }
    }
}
