using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MSC.Save
{
    public static class SaveLimits
    {
        public const int MaximumDocumentBytes = 16 * 1024 * 1024;
        public const int MaximumDomainCount = 512;
        public const int MaximumDomainPayloadBytes = 2 * 1024 * 1024;
        public const int MaximumShortStringLength = 256;
        public const int MaximumDisplayNameLength = 128;
        public const int MaximumUnresolvedEntries = 4096;
        public const int MaximumDeferredEntities = 65536;
        public const int MaximumSlotCount = 256;
    }

    public static class SaveDocumentValidator
    {
        public static void Validate(SaveDocument document, bool requireCurrentVersion = false)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (document.Header == null || document.Metadata == null || document.Domains == null)
            {
                throw new InvalidDataException("Save document sections cannot be null.");
            }

            SaveHeader header = document.Header;
            RequireExact(header.FormatId, SaveHeader.CurrentFormatId, nameof(header.FormatId));
            if (header.DocumentVersion < 1 ||
                (requireCurrentVersion && header.DocumentVersion != SaveDocument.CurrentDocumentVersion))
            {
                throw new NotSupportedException($"Unsupported save document version {header.DocumentVersion}.");
            }

            SaveSlotId.Validate(header.SlotId);
            RequireBounded(header.SaveId, nameof(header.SaveId), SaveLimits.MaximumShortStringLength, false);
            RequireBounded(header.BuildId, nameof(header.BuildId), SaveLimits.MaximumShortStringLength, true);
            RequireUtc(header.CreatedUtc, nameof(header.CreatedUtc));
            RequireUtc(header.UpdatedUtc, nameof(header.UpdatedUtc));
            if (string.CompareOrdinal(header.CreatedUtc, header.UpdatedUtc) > 0)
            {
                throw new InvalidDataException("CreatedUtc cannot be later than UpdatedUtc.");
            }

            if (!string.IsNullOrEmpty(header.IntegritySha256) && !IsLowerHexSha256(header.IntegritySha256))
            {
                throw new InvalidDataException("IntegritySha256 must be a lowercase SHA-256 value.");
            }

            RequireBounded(document.Metadata.DisplayName, nameof(document.Metadata.DisplayName), SaveLimits.MaximumDisplayNameLength, true);
            RequireBounded(document.Metadata.GameTimestamp, nameof(document.Metadata.GameTimestamp), SaveLimits.MaximumShortStringLength, true);
            RequireBounded(document.Metadata.LocationStableId, nameof(document.Metadata.LocationStableId), SaveLimits.MaximumShortStringLength, true);
            if (double.IsNaN(document.Metadata.PlayTimeSeconds) ||
                double.IsInfinity(document.Metadata.PlayTimeSeconds) ||
                document.Metadata.PlayTimeSeconds < 0d)
            {
                throw new InvalidDataException("PlayTimeSeconds must be finite and non-negative.");
            }

            if (document.Domains.Length > SaveLimits.MaximumDomainCount)
            {
                throw new InvalidDataException($"Save document exceeds {SaveLimits.MaximumDomainCount} domains.");
            }

            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (SaveDomainEnvelope domain in document.Domains)
            {
                if (domain == null)
                {
                    throw new InvalidDataException("Save document contains a null domain envelope.");
                }

                SaveDomainId.Validate(domain.DomainId);
                if (!ids.Add(domain.DomainId))
                {
                    throw new InvalidDataException($"Duplicate save domain '{domain.DomainId}'.");
                }

                if (domain.SchemaVersion < 1)
                {
                    throw new InvalidDataException($"Domain '{domain.DomainId}' has an invalid schema version.");
                }

                if (domain.PayloadJson == null)
                {
                    throw new InvalidDataException($"Domain '{domain.DomainId}' has a null payload.");
                }

                if (Encoding.UTF8.GetByteCount(domain.PayloadJson) > SaveLimits.MaximumDomainPayloadBytes)
                {
                    throw new InvalidDataException($"Domain '{domain.DomainId}' exceeds the payload limit.");
                }
            }
        }

        internal static void RequireBounded(string value, string name, int maximumLength, bool allowEmpty)
        {
            if (value == null || (!allowEmpty && string.IsNullOrWhiteSpace(value)))
            {
                throw new InvalidDataException($"{name} is required.");
            }

            if (value.Length > maximumLength)
            {
                throw new InvalidDataException($"{name} exceeds {maximumLength} characters.");
            }
        }

        private static void RequireExact(string value, string expected, string name)
        {
            if (!string.Equals(value, expected, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{name} must equal '{expected}'.");
            }
        }

        private static void RequireUtc(string value, string name)
        {
            RequireBounded(value, name, 64, false);
            if (!DateTimeOffset.TryParseExact(
                    value,
                    "O",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.RoundtripKind,
                    out DateTimeOffset parsed) || parsed.Offset != TimeSpan.Zero)
            {
                throw new InvalidDataException($"{name} must be an ISO-8601 UTC timestamp.");
            }
        }

        private static bool IsLowerHexSha256(string value)
        {
            if (value.Length != 64)
            {
                return false;
            }

            foreach (char character in value)
            {
                if (!((character >= '0' && character <= '9') || (character >= 'a' && character <= 'f')))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public static class SaveDomainId
    {
        public static void Validate(string domainId)
        {
            SaveDocumentValidator.RequireBounded(domainId, nameof(domainId), 96, false);
            for (int index = 0; index < domainId.Length; index++)
            {
                char character = domainId[index];
                bool valid = character >= 'a' && character <= 'z' ||
                             character >= '0' && character <= '9' ||
                             character == '.' || character == '-' || character == '_';
                if (!valid || index == 0 && !(character >= 'a' && character <= 'z'))
                {
                    throw new InvalidDataException($"Invalid save domain ID '{domainId}'.");
                }
            }
        }
    }
}
