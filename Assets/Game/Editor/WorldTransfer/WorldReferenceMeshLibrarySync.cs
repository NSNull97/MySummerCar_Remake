using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MSC.World.Data;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.WorldTransfer
{
    public sealed class WorldReferenceMeshSyncResult
    {
        public int RequestedGuidCount { get; internal set; }
        public int CopiedAssetCount { get; internal set; }
        public int UnchangedAssetCount { get; internal set; }
        public int VerifiedAssetCount { get; internal set; }
        public int ResolvedAssetCount { get; internal set; }
        public List<string> MissingGuids { get; } = new List<string>();
    }

    public static class WorldReferenceMeshLibrarySync
    {
        public const string MeshManifestFileName = "WorldMeshManifest.csv";
        private const string AssetRipperAssetsRelativePath = "assetripper-unity-project/ExportedProject/Assets";
        private const string EmptyMeshGuid = "0000000000000000e000000000000000";

        public static WorldReferenceMeshSyncResult Synchronize(IReadOnlyList<WorldEntityPlacement> records)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));

            string[] requiredGuids = records
                .Where(record => record.ReferenceWorldEligible && IsUsableMeshGuid(record.MeshGuid))
                .Select(record => record.MeshGuid)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return SynchronizeGuids(requiredGuids);
        }

        public static WorldReferenceMeshSyncResult SynchronizeGuids(
            IEnumerable<string> meshGuids)
        {
            if (meshGuids == null)
            {
                throw new ArgumentNullException(nameof(meshGuids));
            }

            string[] requiredGuids = meshGuids
                .Where(IsUsableMeshGuid)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            WorldTransferEditorConfiguration config = WorldTransferEditorConfiguration.Load();
            string manifestPath = Path.Combine(config.NormalizedDataPath, MeshManifestFileName);
            string sourceAssetRoot = SafeCombine(config.RawExtractionPath, AssetRipperAssetsRelativePath);
            if (!Directory.Exists(sourceAssetRoot))
                throw new DirectoryNotFoundException("Frozen AssetRipper Assets root is missing: " + sourceAssetRoot);
            var manifest = new Dictionary<string, string>(
                ReadResolvedManifest(manifestPath),
                StringComparer.Ordinal);
            ResolveRequestedGuidsFromRawMeta(
                sourceAssetRoot,
                requiredGuids.Where(guid => !manifest.ContainsKey(guid)),
                manifest);

            var result = new WorldReferenceMeshSyncResult { RequestedGuidCount = requiredGuids.Length };
            foreach (string guid in requiredGuids)
            {
                if (!manifest.TryGetValue(guid, out string relativeAssetPath))
                {
                    result.MissingGuids.Add(guid);
                    continue;
                }

                string sourceAssetPath = SafeCombine(sourceAssetRoot, relativeAssetPath);
                string sourceMetaPath = sourceAssetPath + ".meta";
                string destinationAssetPath = WorldTransferPaths.ReferenceMeshRoot + "/" + relativeAssetPath.Replace('\\', '/');
                string destinationAbsolutePath = WorldTransferPaths.ToAbsoluteProjectPath(destinationAssetPath);
                string destinationMetaPath = destinationAbsolutePath + ".meta";
                if (!File.Exists(sourceAssetPath) || !File.Exists(sourceMetaPath))
                {
                    result.MissingGuids.Add(guid);
                    continue;
                }

                VerifyMetaGuid(sourceMetaPath, guid);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationAbsolutePath) ?? throw new InvalidOperationException("Mesh destination has no directory."));
                bool copied = CopyIfDifferent(sourceAssetPath, destinationAbsolutePath);
                copied |= CopyIfDifferent(sourceMetaPath, destinationMetaPath);
                if (copied) result.CopiedAssetCount++;
                else result.UnchangedAssetCount++;
                VerifyIdentical(sourceAssetPath, destinationAbsolutePath);
                VerifyIdentical(sourceMetaPath, destinationMetaPath);
                VerifyMetaGuid(destinationMetaPath, guid);
                result.VerifiedAssetCount++;
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            foreach (string guid in requiredGuids)
            {
                string resolved = AssetDatabase.GUIDToAssetPath(guid);
                if (IsBelowReferenceMeshRoot(resolved) && AssetDatabase.LoadAssetAtPath<Mesh>(resolved) != null)
                    result.ResolvedAssetCount++;
            }

            Debug.Log($"WORLD_REFERENCE_MESH_SYNC_OK requested={result.RequestedGuidCount} copied={result.CopiedAssetCount} unchanged={result.UnchangedAssetCount} verified={result.VerifiedAssetCount} resolved={result.ResolvedAssetCount} missing={result.MissingGuids.Count}");
            return result;
        }

        private static void ResolveRequestedGuidsFromRawMeta(
            string sourceAssetRoot,
            IEnumerable<string> unresolvedGuids,
            IDictionary<string, string> resolved)
        {
            var requested = unresolvedGuids.ToHashSet(
                StringComparer.Ordinal);
            if (requested.Count == 0)
            {
                return;
            }

            foreach (string metaPath in Directory.EnumerateFiles(
                         sourceAssetRoot,
                         "*.asset.meta",
                         SearchOption.AllDirectories)
                     .OrderBy(
                         path => path,
                         StringComparer.Ordinal))
            {
                string guid = File.ReadLines(metaPath)
                    .Select(line => line.Trim())
                    .FirstOrDefault(line => line.StartsWith(
                        "guid: ",
                        StringComparison.Ordinal))?["guid: ".Length..] ??
                    string.Empty;
                if (!requested.Contains(guid))
                {
                    continue;
                }

                string assetPath = metaPath.Substring(
                    0,
                    metaPath.Length - ".meta".Length);
                string relative = Path.GetRelativePath(
                        sourceAssetRoot,
                        assetPath)
                    .Replace('\\', '/');
                relative = NormalizeSafeAssetRelativePath(relative);
                if (resolved.TryGetValue(
                        guid,
                        out string existing) &&
                    !string.Equals(
                        existing,
                        relative,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Requested mesh GUID resolves to more than one raw " +
                        "AssetRipper asset: " + guid);
                }

                resolved[guid] = relative;
            }
        }

        public static IReadOnlyDictionary<string, string> ReadResolvedManifest(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("World mesh manifest is missing.", path);
            using var reader = new StreamReader(path);
            List<string> headers = WorldEntityTable.ParseRow(reader.ReadLine() ?? string.Empty);
            int guidIndex = headers.IndexOf("MeshGuid");
            int assetPathIndex = headers.IndexOf("AssetPath");
            int resolvedIndex = headers.IndexOf("Resolved");
            if (guidIndex < 0 || assetPathIndex < 0 || resolvedIndex < 0)
                throw new FormatException("World mesh manifest lacks MeshGuid, AssetPath or Resolved columns.");

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            string line;
            int lineNumber = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line)) continue;
                List<string> values = WorldEntityTable.ParseRow(line);
                if (values.Count != headers.Count)
                    throw new FormatException($"World mesh manifest line {lineNumber} has {values.Count} values; expected {headers.Count}.");
                if (values[resolvedIndex] != "1" || !IsUsableMeshGuid(values[guidIndex])) continue;
                string relativePath = NormalizeSafeAssetRelativePath(values[assetPathIndex]);
                if (!result.TryAdd(values[guidIndex], relativePath) && !string.Equals(result[values[guidIndex]], relativePath, StringComparison.Ordinal))
                    throw new FormatException("World mesh manifest maps one GUID to multiple assets: " + values[guidIndex]);
            }

            return result;
        }

        public static bool IsUsableMeshGuid(string value) =>
            !string.IsNullOrEmpty(value) && value.Length == 32 && !string.Equals(value, EmptyMeshGuid, StringComparison.Ordinal);

        public static bool IsBelowReferenceMeshRoot(string assetPath) =>
            !string.IsNullOrEmpty(assetPath) &&
            (string.Equals(assetPath, WorldTransferPaths.ReferenceMeshRoot, StringComparison.Ordinal) ||
             assetPath.StartsWith(WorldTransferPaths.ReferenceMeshRoot + "/", StringComparison.Ordinal));

        private static string NormalizeSafeAssetRelativePath(string value)
        {
            string normalized = value.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrWhiteSpace(normalized) || Path.IsPathRooted(value) ||
                normalized.Split('/').Any(part => part is "" or "." or "..") ||
                !normalized.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                throw new FormatException("Unsafe or unsupported world mesh asset path: " + value);
            return normalized;
        }

        private static string SafeCombine(string root, string relative)
        {
            string normalizedRelative = relative.Replace('/', Path.DirectorySeparatorChar);
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(root, normalizedRelative));
            if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("World mesh path escapes its allowed root: " + relative);
            return candidate;
        }

        private static bool CopyIfDifferent(string source, string destination)
        {
            if (File.Exists(destination) && FilesEqual(source, destination)) return false;
            File.Copy(source, destination, true);
            return true;
        }

        private static void VerifyIdentical(string source, string destination)
        {
            if (!FilesEqual(source, destination))
                throw new IOException("Synchronized world reference asset failed byte verification: " + destination);
        }

        private static bool FilesEqual(string left, string right)
        {
            var leftInfo = new FileInfo(left);
            var rightInfo = new FileInfo(right);
            if (!leftInfo.Exists || !rightInfo.Exists || leftInfo.Length != rightInfo.Length) return false;
            using SHA256 sha = SHA256.Create();
            using FileStream leftStream = File.OpenRead(left);
            byte[] leftHash = sha.ComputeHash(leftStream);
            using FileStream rightStream = File.OpenRead(right);
            byte[] rightHash = sha.ComputeHash(rightStream);
            return leftHash.SequenceEqual(rightHash);
        }

        private static void VerifyMetaGuid(string metaPath, string expectedGuid)
        {
            string actual = File.ReadLines(metaPath)
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.StartsWith("guid: ", StringComparison.Ordinal))?["guid: ".Length..] ?? string.Empty;
            if (!string.Equals(actual, expectedGuid, StringComparison.Ordinal))
                throw new InvalidDataException($"Mesh meta GUID mismatch for '{metaPath}': expected {expectedGuid}, got {actual}.");
        }
    }
}
