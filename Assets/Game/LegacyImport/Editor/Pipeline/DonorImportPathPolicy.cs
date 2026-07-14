using System;
using System.Collections.Generic;
using System.IO;
using MSC.LegacyImport.Editor.Configuration;

namespace MSC.LegacyImport.Editor.Pipeline
{
    public static class DonorImportPathPolicy
    {
        public const string ReferenceOnlyRoot = "Assets/Game/LegacyImport/ReferenceOnly";
        public const string DonorGeneratedRoot = "Assets/Game/Imported/DonorGenerated";
        public const string GameAssetRoot = "Assets/Game";

        public static bool TryNormalizeRelativePath(string path, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                error = "Relative path is empty.";
                return false;
            }

            string candidate = path.Trim().Replace('\\', '/');
            if (candidate.StartsWith("/", StringComparison.Ordinal) ||
                candidate.Contains(":") ||
                Path.IsPathRooted(candidate))
            {
                error = "Relative path must not be rooted or contain a drive prefix.";
                return false;
            }

            string[] segments = candidate.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                error = "Relative path contains no usable segments.";
                return false;
            }

            var normalizedSegments = new List<string>(segments.Length);
            foreach (string segment in segments)
            {
                if (segment == "." || segment == "..")
                {
                    error = "Relative path traversal is not allowed.";
                    return false;
                }

                if (segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    error = $"Path segment '{segment}' contains invalid characters.";
                    return false;
                }

                normalizedSegments.Add(segment);
            }

            normalized = string.Join("/", normalizedSegments);
            return true;
        }

        public static bool TryNormalizeReferenceAssetPath(
            string assetPath,
            out string normalized,
            out string error)
        {
            if (!TryNormalizeAssetPath(assetPath, out normalized, out error))
            {
                return false;
            }

            if (!IsUnderAssetRoot(normalized, ReferenceOnlyRoot))
            {
                error = $"Reference destination must be below {ReferenceOnlyRoot}.";
                return false;
            }

            if (normalized.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                error = "Unity .meta files must never be imported from donor staging.";
                return false;
            }

            return true;
        }

        public static bool TryNormalizeProductionAssetPath(
            string assetPath,
            out string normalized,
            out string error)
        {
            if (!TryNormalizeAssetPath(assetPath, out normalized, out error))
            {
                return false;
            }

            if (!IsUnderAssetRoot(normalized, GameAssetRoot))
            {
                error = $"Production destination must be below {GameAssetRoot}.";
                return false;
            }

            if (IsUnderAssetRoot(normalized, ReferenceOnlyRoot) ||
                IsUnderAssetRoot(normalized, DonorGeneratedRoot))
            {
                error = "Production destination must not be inside donor reference/generated roots.";
                return false;
            }

            return true;
        }

        public static string ResolveStagingFile(string stagingRoot, string stagedRelativePath)
        {
            if (!TryNormalizeRelativePath(stagedRelativePath, out string normalized, out string error))
            {
                throw new ArgumentException(error, nameof(stagedRelativePath));
            }

            string normalizedRoot = DonorPathConfiguration.NormalizeDirectoryPath(stagingRoot);
            string candidate = Path.GetFullPath(Path.Combine(
                normalizedRoot,
                normalized.Replace('/', Path.DirectorySeparatorChar)));
            EnsureContained(candidate, normalizedRoot, nameof(stagedRelativePath));
            return candidate;
        }

        public static string ResolveProjectAssetFile(string projectRoot, string assetPath)
        {
            if (!TryNormalizeAssetPath(assetPath, out string normalized, out string error))
            {
                throw new ArgumentException(error, nameof(assetPath));
            }

            string normalizedRoot = DonorPathConfiguration.NormalizeDirectoryPath(projectRoot);
            string candidate = Path.GetFullPath(Path.Combine(
                normalizedRoot,
                normalized.Replace('/', Path.DirectorySeparatorChar)));
            EnsureContained(candidate, normalizedRoot, nameof(assetPath));
            return candidate;
        }

        public static bool IsUnderAssetRoot(string assetPath, string root)
        {
            return assetPath.StartsWith(root + "/", StringComparison.Ordinal) &&
                   assetPath.Length > root.Length + 1;
        }

        private static bool TryNormalizeAssetPath(string path, out string normalized, out string error)
        {
            normalized = string.Empty;
            error = string.Empty;

            if (!TryNormalizeRelativePath(path, out string relative, out error))
            {
                return false;
            }

            if (!relative.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = "Unity asset path must start with 'Assets/'.";
                return false;
            }

            normalized = relative;
            return true;
        }

        private static void EnsureContained(string candidate, string root, string parameterName)
        {
            string rootWithSeparator = root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Resolved path escapes its configured root.", parameterName);
            }
        }
    }
}
