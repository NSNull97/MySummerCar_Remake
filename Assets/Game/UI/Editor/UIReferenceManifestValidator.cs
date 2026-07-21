using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace MSC.UI.EditorTools
{
    public static class UIReferenceManifestValidator
    {
        public static UIReferenceValidationResult ValidateProject(string projectRoot)
        {
            var manifestPath = UIReferencePaths.GetManifestPath(projectRoot);
            var referenceDirectory = UIReferencePaths.GetReferenceDirectory(projectRoot);
            return Validate(manifestPath, referenceDirectory);
        }

        public static UIReferenceValidationResult Validate(
            string manifestPath,
            string referenceDirectory)
        {
            var result = new UIReferenceValidationResult();
            if (!File.Exists(manifestPath))
            {
                result.AddIssue($"Approved reference manifest is missing: {manifestPath}");
                return result;
            }

            UIReferenceManifestDocument manifest;
            try
            {
                manifest = JsonUtility.FromJson<UIReferenceManifestDocument>(
                    File.ReadAllText(manifestPath, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                result.AddIssue($"Approved reference manifest cannot be read: {exception.Message}");
                return result;
            }

            if (manifest == null)
            {
                result.AddIssue("Approved reference manifest JSON produced no document.");
                return result;
            }

            ValidateHeader(manifest, result);

            var manifestEntries = manifest.references ?? Array.Empty<UIReferenceManifestEntry>();
            if (manifestEntries.Length != UIReferenceCatalog.All.Count)
            {
                result.AddIssue(
                    $"Manifest must contain exactly {UIReferenceCatalog.All.Count} references, " +
                    $"but contains {manifestEntries.Length}.");
            }

            var seenFiles = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < manifestEntries.Length; index++)
            {
                var file = manifestEntries[index]?.file;
                if (string.IsNullOrWhiteSpace(file))
                {
                    result.AddIssue($"Manifest reference at index {index} has no file name.");
                    continue;
                }

                if (!seenFiles.Add(file))
                {
                    result.AddIssue($"Manifest contains duplicate reference '{file}'.");
                }
            }

            foreach (var descriptor in UIReferenceCatalog.All)
            {
                var entry = FindEntry(manifestEntries, descriptor.ReferenceFileName);
                if (entry == null)
                {
                    result.AddIssue($"Manifest entry is missing: {descriptor.ReferenceFileName}");
                    continue;
                }

                ValidateEntry(referenceDirectory, descriptor, entry, result);
            }

            for (var index = 0; index < manifestEntries.Length; index++)
            {
                var entry = manifestEntries[index];
                if (entry == null || IsExpectedFile(entry.file))
                {
                    continue;
                }

                result.AddIssue($"Manifest contains an unexpected reference: {entry.file}");
            }

            return result;
        }

        public static string ComputeSha256(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var sha256 = SHA256.Create())
            {
                var hash = sha256.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (var index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        public static bool TryReadPngDimensions(
            string filePath,
            out int width,
            out int height,
            out string error)
        {
            width = 0;
            height = 0;
            error = string.Empty;

            try
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var header = new byte[24];
                    if (stream.Read(header, 0, header.Length) != header.Length)
                    {
                        error = "File is too short to contain a PNG IHDR header.";
                        return false;
                    }

                    var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
                    for (var index = 0; index < signature.Length; index++)
                    {
                        if (header[index] == signature[index])
                        {
                            continue;
                        }

                        error = "File signature is not PNG.";
                        return false;
                    }

                    if (header[12] != (byte)'I' ||
                        header[13] != (byte)'H' ||
                        header[14] != (byte)'D' ||
                        header[15] != (byte)'R')
                    {
                        error = "PNG does not begin with an IHDR chunk.";
                        return false;
                    }

                    width = ReadBigEndianInt32(header, 16);
                    height = ReadBigEndianInt32(header, 20);
                    if (width <= 0 || height <= 0)
                    {
                        error = $"PNG dimensions are invalid: {width}x{height}.";
                        return false;
                    }

                    return true;
                }
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static void ValidateHeader(
            UIReferenceManifestDocument manifest,
            UIReferenceValidationResult result)
        {
            if (manifest.schemaVersion != 1)
            {
                result.AddIssue($"Unsupported reference manifest schema: {manifest.schemaVersion}.");
            }

            if (!string.Equals(manifest.milestone, "08A", StringComparison.Ordinal))
            {
                result.AddIssue($"Manifest milestone must be '08A', got '{manifest.milestone}'.");
            }

            if (manifest.runtimeUsageAllowed)
            {
                result.AddIssue("Manifest incorrectly allows runtime usage of approved reference PNGs.");
            }

            if (manifest.canonicalViewport == null ||
                manifest.canonicalViewport.width != UIReferenceCatalog.CanonicalWidth ||
                manifest.canonicalViewport.height != UIReferenceCatalog.CanonicalHeight)
            {
                var actual = manifest.canonicalViewport == null
                    ? "missing"
                    : $"{manifest.canonicalViewport.width}x{manifest.canonicalViewport.height}";
                result.AddIssue(
                    $"Canonical viewport must be {UIReferenceCatalog.CanonicalWidth}x" +
                    $"{UIReferenceCatalog.CanonicalHeight}, got {actual}.");
            }
        }

        private static void ValidateEntry(
            string referenceDirectory,
            UIReferenceScreenDescriptor descriptor,
            UIReferenceManifestEntry entry,
            UIReferenceValidationResult result)
        {
            if (!string.Equals(entry.role, descriptor.Role, StringComparison.Ordinal))
            {
                result.AddIssue(
                    $"Role mismatch for {descriptor.ReferenceFileName}: expected " +
                    $"'{descriptor.Role}', got '{entry.role}'.");
            }

            if (!entry.authoritative)
            {
                result.AddIssue($"Reference is not marked authoritative: {descriptor.ReferenceFileName}");
            }

            if (entry.width != UIReferenceCatalog.CanonicalWidth ||
                entry.height != UIReferenceCatalog.CanonicalHeight)
            {
                result.AddIssue(
                    $"Manifest dimensions for {descriptor.ReferenceFileName} must be " +
                    $"{UIReferenceCatalog.CanonicalWidth}x{UIReferenceCatalog.CanonicalHeight}, " +
                    $"got {entry.width}x{entry.height}.");
            }

            var filePath = Path.GetFullPath(
                Path.Combine(referenceDirectory, descriptor.ReferenceFileName));
            if (!File.Exists(filePath))
            {
                result.AddIssue($"Approved reference is missing: {filePath}");
                return;
            }

            var actualSize = new FileInfo(filePath).Length;
            if (actualSize != entry.sizeBytes)
            {
                result.AddIssue(
                    $"Byte-size mismatch for {descriptor.ReferenceFileName}: expected " +
                    $"{entry.sizeBytes}, got {actualSize}.");
            }

            string actualHash;
            try
            {
                actualHash = ComputeSha256(filePath);
            }
            catch (Exception exception)
            {
                result.AddIssue(
                    $"Cannot hash {descriptor.ReferenceFileName}: {exception.Message}");
                return;
            }

            if (!string.Equals(actualHash, entry.sha256, StringComparison.OrdinalIgnoreCase))
            {
                result.AddIssue(
                    $"SHA-256 mismatch for {descriptor.ReferenceFileName}: expected " +
                    $"{entry.sha256}, got {actualHash}.");
            }

            if (!TryReadPngDimensions(filePath, out var width, out var height, out var error))
            {
                result.AddIssue($"Cannot read PNG dimensions for {descriptor.ReferenceFileName}: {error}");
                return;
            }

            if (width != entry.width || height != entry.height)
            {
                result.AddIssue(
                    $"PNG dimensions mismatch for {descriptor.ReferenceFileName}: manifest " +
                    $"{entry.width}x{entry.height}, file {width}x{height}.");
            }

            result.AddRecord(
                new UIReferenceValidationRecord(
                    descriptor.Screen,
                    descriptor.Role,
                    filePath,
                    actualHash,
                    actualSize,
                    width,
                    height));
        }

        private static UIReferenceManifestEntry FindEntry(
            IReadOnlyList<UIReferenceManifestEntry> entries,
            string fileName)
        {
            for (var index = 0; index < entries.Count; index++)
            {
                if (entries[index] != null &&
                    string.Equals(entries[index].file, fileName, StringComparison.Ordinal))
                {
                    return entries[index];
                }
            }

            return null;
        }

        private static bool IsExpectedFile(string fileName)
        {
            foreach (var descriptor in UIReferenceCatalog.All)
            {
                if (string.Equals(descriptor.ReferenceFileName, fileName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static int ReadBigEndianInt32(IReadOnlyList<byte> bytes, int offset)
        {
            return (bytes[offset] << 24) |
                   (bytes[offset + 1] << 16) |
                   (bytes[offset + 2] << 8) |
                   bytes[offset + 3];
        }

        [Serializable]
        private sealed class UIReferenceManifestDocument
        {
            public int schemaVersion;
            public string milestone;
            public UIReferenceCanonicalViewport canonicalViewport;
            public bool runtimeUsageAllowed;
            public UIReferenceManifestEntry[] references;
        }

        [Serializable]
        private sealed class UIReferenceCanonicalViewport
        {
            public int width;
            public int height;
        }

        [Serializable]
        private sealed class UIReferenceManifestEntry
        {
            public string file;
            public string role;
            public string sha256;
            public long sizeBytes;
            public int width;
            public int height;
            public bool authoritative;
        }
    }
}
