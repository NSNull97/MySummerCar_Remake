using System;
using System.IO;

namespace MSC.Save
{
    public static class SaveSlotId
    {
        public const int MaximumLength = 48;

        public static void Validate(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId) || slotId.Length > MaximumLength)
            {
                throw new InvalidDataException($"Save slot ID must contain 1-{MaximumLength} characters.");
            }

            for (int index = 0; index < slotId.Length; index++)
            {
                char character = slotId[index];
                bool valid = character >= 'a' && character <= 'z' ||
                             character >= '0' && character <= '9' ||
                             character == '-' || character == '_';
                if (!valid || index == 0 && !(character >= 'a' && character <= 'z' || character >= '0' && character <= '9'))
                {
                    throw new InvalidDataException($"Invalid save slot ID '{slotId}'.");
                }
            }
        }

        public static string CombineUnderRoot(string rootDirectory, string slotId)
        {
            Validate(slotId);
            if (string.IsNullOrWhiteSpace(rootDirectory))
            {
                throw new ArgumentException("A save root directory is required.", nameof(rootDirectory));
            }

            string root = Path.GetFullPath(rootDirectory);
            string candidate = Path.GetFullPath(Path.Combine(root, slotId));
            string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Save slot path escaped the configured root.");
            }

            return candidate;
        }
    }
}
