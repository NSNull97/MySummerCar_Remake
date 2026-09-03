using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MSC.Save
{
    public sealed class SaveDocumentCodec
    {
        private const int MaximumJsonStabilizationPasses = 4;
        private static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false, true);

        public string Serialize(SaveDocument document, bool prettyPrint = false)
        {
            SaveDocument normalized = Normalize(document);
            normalized.Header.IntegritySha256 = string.Empty;
            SaveDocumentValidator.Validate(normalized);
            normalized = StabilizeJsonRepresentation(normalized);
            normalized.Header.IntegritySha256 = ComputeIntegrity(normalized);
            string json = JsonUtility.ToJson(normalized, prettyPrint);
            EnsureEncodedSize(json);
            return json + "\n";
        }

        public SaveDocument Deserialize(string json, bool requireCurrentVersion = false)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new InvalidDataException("Save document JSON is empty.");
            }

            EnsureEncodedSize(json);
            SaveDocument document;
            try
            {
                document = JsonUtility.FromJson<SaveDocument>(json);
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException("Save document JSON is malformed.", exception);
            }

            if (document == null)
            {
                throw new InvalidDataException("Save document JSON did not produce a document.");
            }

            SaveDocument normalized = Normalize(document);
            SaveDocumentValidator.Validate(normalized, requireCurrentVersion);
            if (string.IsNullOrWhiteSpace(normalized.Header.IntegritySha256))
            {
                throw new InvalidDataException("Save document has no integrity value.");
            }

            string actual = normalized.Header.IntegritySha256;
            normalized.Header.IntegritySha256 = string.Empty;
            string expected = ComputeIntegrity(normalized);
            if (!FixedTimeEquals(actual, expected))
            {
                throw new InvalidDataException("Save document integrity validation failed.");
            }

            normalized.Header.IntegritySha256 = actual;
            return normalized;
        }

        public SaveDocument Normalize(SaveDocument document)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            SaveDocument result = document.DeepClone();
            result.Header ??= new SaveHeader();
            result.Metadata ??= new SaveMetadata();
            result.Domains = (result.Domains ?? Array.Empty<SaveDomainEnvelope>())
                .OrderBy(domain => domain?.DomainId, StringComparer.Ordinal)
                .ToArray();
            return result;
        }

        private SaveDocument StabilizeJsonRepresentation(SaveDocument document)
        {
            SaveDocument candidate = Normalize(document);
            candidate.Header.IntegritySha256 = string.Empty;
            string canonicalJson = JsonUtility.ToJson(candidate, false);
            for (int pass = 0;
                 pass < MaximumJsonStabilizationPasses;
                 pass++)
            {
                SaveDocument roundTripped = JsonUtility.FromJson<SaveDocument>(
                    canonicalJson);
                if (roundTripped == null)
                {
                    throw new InvalidDataException(
                        "Save document JSON stabilization produced no document.");
                }

                roundTripped = Normalize(roundTripped);
                roundTripped.Header.IntegritySha256 = string.Empty;
                SaveDocumentValidator.Validate(roundTripped);
                string roundTrippedJson = JsonUtility.ToJson(
                    roundTripped,
                    false);
                if (string.Equals(
                        canonicalJson,
                        roundTrippedJson,
                        StringComparison.Ordinal))
                {
                    return roundTripped;
                }

                canonicalJson = roundTrippedJson;
            }

            throw new InvalidDataException(
                "Save document JSON did not stabilize before integrity hashing.");
        }

        public string ComputeIntegrity(SaveDocument documentWithoutIntegrity)
        {
            SaveDocument normalized = Normalize(documentWithoutIntegrity);
            normalized.Header.IntegritySha256 = string.Empty;
            string canonicalJson = JsonUtility.ToJson(normalized, false);
            byte[] bytes = Utf8WithoutBom.GetBytes(canonicalJson);
            using SHA256 sha256 = SHA256.Create();
            byte[] digest = sha256.ComputeHash(bytes);
            StringBuilder builder = new StringBuilder(digest.Length * 2);
            foreach (byte value in digest)
            {
                builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            }

            return builder.ToString();
        }

        private static void EnsureEncodedSize(string json)
        {
            if (Utf8WithoutBom.GetByteCount(json) > SaveLimits.MaximumDocumentBytes)
            {
                throw new InvalidDataException($"Save document exceeds {SaveLimits.MaximumDocumentBytes} bytes.");
            }
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            int difference = 0;
            for (int index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }
    }
}
