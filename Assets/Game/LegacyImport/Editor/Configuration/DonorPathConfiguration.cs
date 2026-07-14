using System;
using System.IO;
using UnityEngine;

namespace MSC.LegacyImport.Editor.Configuration
{
    public sealed class DonorPathConfiguration
    {
        private DonorPathConfiguration(DonorPathConfigurationData data)
        {
            OriginalGameDirectory = NormalizeDirectoryPath(data.OriginalGameDirectory);
            UnityProjectDirectory = NormalizeDirectoryPath(data.UnityProjectDirectory);
            DonorStagingDirectory = NormalizeDirectoryPath(data.DonorStagingDirectory);
            LegacyReferenceDirectory = NormalizeDirectoryPath(data.LegacyReferenceDirectory);
            ReferenceMediaDirectory = NormalizeDirectoryPath(data.ReferenceMediaDirectory);
            AssetRipperExecutable = NormalizeOptionalFilePath(data.AssetRipperExecutable);
            ILSpyCmdExecutable = NormalizeOptionalFilePath(data.ILSpyCmdExecutable);
            UnityEditorExecutable = NormalizeOptionalFilePath(data.UnityEditorExecutable);
        }

        public string OriginalGameDirectory { get; }

        public string UnityProjectDirectory { get; }

        public string DonorStagingDirectory { get; }

        public string LegacyReferenceDirectory { get; }

        public string ReferenceMediaDirectory { get; }

        public string AssetRipperExecutable { get; }

        public string ILSpyCmdExecutable { get; }

        public string UnityEditorExecutable { get; }

        public static DonorPathConfiguration ParseJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Path configuration JSON must not be empty.", nameof(json));
            }

            DonorPathConfigurationData data = JsonUtility.FromJson<DonorPathConfigurationData>(json);
            if (data == null)
            {
                throw new FormatException("Path configuration JSON could not be parsed.");
            }

            return new DonorPathConfiguration(data);
        }

        public static DonorPathConfiguration LoadFromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Configuration path must not be empty.", nameof(path));
            }

            return ParseJson(File.ReadAllText(path));
        }

        public static string NormalizeDirectoryPath(string path)
        {
            string normalized = NormalizeRequiredPath(path);
            string root = Path.GetPathRoot(normalized);

            if (!string.Equals(normalized, root, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            return normalized;
        }

        private static string NormalizeOptionalFilePath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : NormalizeRequiredPath(path);
        }

        private static string NormalizeRequiredPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new FormatException("A required directory path is missing from the local configuration.");
            }

            string expanded = Environment.ExpandEnvironmentVariables(path.Trim());
            return Path.GetFullPath(expanded);
        }

        [Serializable]
        private sealed class DonorPathConfigurationData
        {
            public string OriginalGameDirectory = string.Empty;
            public string UnityProjectDirectory = string.Empty;
            public string DonorStagingDirectory = string.Empty;
            public string LegacyReferenceDirectory = string.Empty;
            public string ReferenceMediaDirectory = string.Empty;
            public string AssetRipperExecutable = string.Empty;
            public string ILSpyCmdExecutable = string.Empty;
            public string UnityEditorExecutable = string.Empty;
        }
    }
}
