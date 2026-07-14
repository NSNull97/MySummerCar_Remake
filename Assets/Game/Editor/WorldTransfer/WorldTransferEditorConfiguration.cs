using System;
using System.IO;
using UnityEngine;

namespace MSC.Editor.WorldTransfer
{
    public sealed class WorldTransferEditorConfiguration
    {
        private WorldTransferEditorConfiguration(Data data)
        {
            DonorGamePath = Normalize(data.donorGamePath);
            DonorStagingPath = Normalize(data.donorStagingPath);
            LegacyReferencePath = Normalize(data.legacyReferencePath);
            RawExtractionPath = ResolveBelow(DonorStagingPath, data.rawExtractionRelativePath);
            NormalizedDataPath = ResolveBelow(DonorStagingPath, data.normalizedDataRelativePath);
            PartitionCellSizeMeters = data.partitionCellSizeMeters;
            StrictValidation = data.strictValidation;
            AllowUnsupportedObjects = data.allowUnsupportedObjects;
        }

        public string DonorGamePath { get; }
        public string DonorStagingPath { get; }
        public string LegacyReferencePath { get; }
        public string RawExtractionPath { get; }
        public string NormalizedDataPath { get; }
        public float PartitionCellSizeMeters { get; }
        public bool StrictValidation { get; }
        public bool AllowUnsupportedObjects { get; }

        public static WorldTransferEditorConfiguration Load()
        {
            string path = WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.LocalConfigRelativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Create the ignored WorldTransferConfig.local.json from the committed example.", path);
            }

            Data data = JsonUtility.FromJson<Data>(File.ReadAllText(path));
            if (data == null || data.schemaVersion != 1)
            {
                throw new FormatException("World transfer local configuration schema is unsupported.");
            }

            var config = new WorldTransferEditorConfiguration(data);
            config.ValidateRootSeparation();
            return config;
        }

        public void ValidateRootSeparation()
        {
            string project = Normalize(WorldTransferPaths.ProjectRoot);
            string[] roots = { DonorGamePath, DonorStagingPath, LegacyReferencePath, project };
            for (int left = 0; left < roots.Length; left++)
            for (int right = left + 1; right < roots.Length; right++)
            {
                if (ContainsOrEquals(roots[left], roots[right]) || ContainsOrEquals(roots[right], roots[left]))
                {
                    throw new InvalidOperationException($"World transfer roots overlap: '{roots[left]}' and '{roots[right]}'.");
                }
            }
        }

        private static string ResolveBelow(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(".."))
                throw new FormatException("World transfer staging subpaths must be safe relative paths.");
            string candidate = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!ContainsOrEquals(root, candidate))
                throw new FormatException("World transfer staging subpath escapes the configured staging root.");
            return candidate;
        }

        private static bool ContainsOrEquals(string root, string candidate)
        {
            string normalizedRoot = Normalize(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedCandidate = Normalize(candidate);
            return string.Equals(normalizedRoot.TrimEnd(Path.DirectorySeparatorChar), normalizedCandidate, StringComparison.OrdinalIgnoreCase) ||
                normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new FormatException("World transfer configuration contains an empty path.");
            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim())).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        [Serializable]
        private sealed class Data
        {
            public int schemaVersion;
            public string donorGamePath = string.Empty;
            public string donorStagingPath = string.Empty;
            public string legacyReferencePath = string.Empty;
            public string rawExtractionRelativePath = string.Empty;
            public string normalizedDataRelativePath = string.Empty;
            public float partitionCellSizeMeters = 512f;
            public bool strictValidation = true;
            public bool allowUnsupportedObjects = true;
        }
    }
}
