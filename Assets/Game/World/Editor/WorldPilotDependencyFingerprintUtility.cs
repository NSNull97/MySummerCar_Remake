using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MSC.World.Remaster.Editor
{
    internal readonly struct WorldPilotDependencyFingerprint
    {
        public WorldPilotDependencyFingerprint(string value, int fileCount)
        {
            Value = value;
            FileCount = fileCount;
        }

        public string Value { get; }
        public int FileCount { get; }
    }

    internal static class WorldPilotDependencyFingerprintUtility
    {
        private static readonly string[] SeedAssets =
        {
            WorldRemasterPaths.PilotPlaytestScene,
            WorldRemasterPaths.PilotCellScene,
            WorldRemasterPaths.PlayerPrefab
        };

        private static readonly string[] ExplicitContractFiles =
        {
            "Assets/Game/World/Editor/WorldPilotDependencyFingerprintUtility.cs",
            "Assets/Game/World/Editor/WorldPilotTraversalAuthoring.cs",
            "Assets/Game/World/Editor/WorldPilotTraversalEvidenceReader.cs",
            "Assets/Game/Tests/PlayMode/WorldRemaster/WorldPilotTraversalPlayModeTests.cs",
            "Assets/Game/World/Production/Runtime/WorldPilotTraversalRoute.cs",
            "Assets/Game/World/Production/Runtime/WorldHingedArchitecture.cs",
            "Assets/Game/Player/Runtime/FirstPersonMotor.cs",
            "Assets/Game/Player/Runtime/PlayerInputRouter.cs",
            "ProjectSettings/ProjectVersion.txt"
        };

        public static WorldPilotDependencyFingerprint CalculateCurrent()
        {
            string projectRoot = GetProjectRoot();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string seed in SeedAssets)
            {
                string normalizedSeed = Normalize(seed);
                if (!File.Exists(ResolveProjectPath(projectRoot, normalizedSeed)))
                {
                    throw new FileNotFoundException("Traversal dependency seed is missing.", normalizedSeed);
                }

                foreach (string dependency in AssetDatabase.GetDependencies(normalizedSeed, recursive: true))
                {
                    string normalizedDependency = Normalize(dependency);
                    if (string.Equals(
                            normalizedDependency,
                            WorldRemasterPaths.PilotPlaytestScene,
                            StringComparison.Ordinal))
                    {
                        // The playtest scene stores this fingerprint, so hashing the scene here would be circular.
                        continue;
                    }

                    if (normalizedDependency.StartsWith("Assets/", StringComparison.Ordinal) &&
                        File.Exists(ResolveProjectPath(projectRoot, normalizedDependency)))
                    {
                        AddPathAndMeta(paths, projectRoot, normalizedDependency);
                    }
                }

                if (!string.Equals(normalizedSeed, WorldRemasterPaths.PilotPlaytestScene, StringComparison.Ordinal))
                {
                    AddPathAndMeta(paths, projectRoot, normalizedSeed);
                }
            }

            foreach (string contractFile in ExplicitContractFiles)
            {
                string normalized = Normalize(contractFile);
                if (!File.Exists(ResolveProjectPath(projectRoot, normalized)))
                {
                    throw new FileNotFoundException("Traversal contract fingerprint input is missing.", normalized);
                }

                AddPathAndMeta(paths, projectRoot, normalized);
            }

            string[] orderedPaths = paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var canonical = new StringBuilder();
            foreach (string path in orderedPaths)
            {
                canonical.Append(path).Append('\n')
                    .Append(CalculateFileSha256(ResolveProjectPath(projectRoot, path))).Append('\n');
            }

            using SHA256 sha256 = SHA256.Create();
            string value = ToLowerHex(sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())));
            return new WorldPilotDependencyFingerprint(value, orderedPaths.Length);
        }

        private static string GetProjectRoot() =>
            Directory.GetParent(Application.dataPath)?.FullName ??
            throw new InvalidOperationException("Unity project root cannot be resolved.");

        private static string ResolveProjectPath(string projectRoot, string projectRelativePath) =>
            Path.Combine(projectRoot, projectRelativePath.Replace('/', Path.DirectorySeparatorChar));

        private static string Normalize(string path) => (path ?? string.Empty).Replace('\\', '/');

        private static void AddPathAndMeta(
            ISet<string> paths,
            string projectRoot,
            string normalizedPath)
        {
            paths.Add(normalizedPath);
            if (!normalizedPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return;
            }

            string metaPath = normalizedPath + ".meta";
            if (File.Exists(ResolveProjectPath(projectRoot, metaPath)))
            {
                paths.Add(metaPath);
            }
        }

        private static string CalculateFileSha256(string absolutePath)
        {
            using SHA256 sha256 = SHA256.Create();
            using FileStream stream = File.OpenRead(absolutePath);
            return ToLowerHex(sha256.ComputeHash(stream));
        }

        private static string ToLowerHex(byte[] hash)
        {
            var output = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                output.Append(value.ToString("x2"));
            }

            return output.ToString();
        }
    }
}
