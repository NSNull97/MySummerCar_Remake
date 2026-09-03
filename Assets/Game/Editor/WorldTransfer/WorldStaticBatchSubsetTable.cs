using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.LegacyImport.Editor.Pipeline;
using MSC.World.Data;
using UnityEditor;

namespace MSC.Editor.WorldTransfer
{
    public static class WorldStaticBatchSubsetTable
    {
        private const string ExtractedSceneRelativePath = "assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity";
        private const string ExpectedExtractedSceneSha256 = "c3f2f3373ccad4fcbe104840fcb83e364f55438070e808d11ebe4996bc0476c4";

        public static IReadOnlyDictionary<long, int[]> Synchronize(IReadOnlyList<WorldEntityPlacement> records)
        {
            if (records == null) throw new ArgumentNullException(nameof(records));
            string scenePath = SafeCombine(WorldTransferEditorConfiguration.Load().RawExtractionPath, ExtractedSceneRelativePath);
            if (!File.Exists(scenePath)) throw new FileNotFoundException("Frozen extracted GAME scene is missing.", scenePath);
            if (!Sha256FileHasher.Matches(scenePath, ExpectedExtractedSceneSha256))
                throw new InvalidDataException("Frozen extracted GAME scene hash differs from 04A1 provenance.");

            IReadOnlyDictionary<long, int[]> allSubsets = ParseScene(scenePath);
            WorldEntityPlacement[] eligible = records.Where(record => record.ReferenceWorldEligible).OrderBy(record => record.SourceObjectId).ToArray();
            var filtered = new Dictionary<long, int[]>();
            var csv = new StringBuilder("SourceObjectId,StableId,MeshGuid,SubMeshIndices\n");
            foreach (WorldEntityPlacement record in eligible)
            {
                if (!allSubsets.TryGetValue(record.SourceObjectId, out int[] indices)) continue;
                filtered.Add(record.SourceObjectId, indices);
                csv.Append(record.SourceObjectId.ToString(CultureInfo.InvariantCulture)).Append(',')
                    .Append(record.StableId).Append(',').Append(record.MeshGuid).Append(',')
                    .AppendLine(string.Join(";", indices.Select(index => index.ToString(CultureInfo.InvariantCulture))));
            }

            string assetPath = WorldTransferPaths.StaticBatchSubsetTableAssetPath;
            File.WriteAllText(WorldTransferPaths.ToAbsoluteProjectPath(assetPath), csv.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            UnityEngine.Debug.Log($"WORLD_STATIC_BATCH_SUBSETS_OK sceneRenderers={allSubsets.Count} eligible={filtered.Count}");
            return filtered;
        }

        public static IReadOnlyDictionary<long, int[]> ParseCommittedTable()
        {
            string path = WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.StaticBatchSubsetTableAssetPath);
            if (!File.Exists(path)) throw new FileNotFoundException("05C static-batch subset table is missing.", path);
            var result = new Dictionary<long, int[]>();
            foreach (string line in File.ReadLines(path).Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                List<string> values = WorldEntityTable.ParseRow(line);
                if (values.Count != 4) throw new FormatException("05C static-batch subset table has an invalid row.");
                result.Add(long.Parse(values[0], CultureInfo.InvariantCulture), values[3].Split(';').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray());
            }
            return result;
        }

        public static IReadOnlyDictionary<long, int[]> ParseFrozenScene()
        {
            string scenePath = SafeCombine(
                WorldTransferEditorConfiguration.Load().RawExtractionPath,
                ExtractedSceneRelativePath);
            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException(
                    "Frozen extracted GAME scene is missing.",
                    scenePath);
            }
            if (!Sha256FileHasher.Matches(
                    scenePath,
                    ExpectedExtractedSceneSha256))
            {
                throw new InvalidDataException(
                    "Frozen extracted GAME scene hash differs from 04A1 provenance.");
            }

            return ParseScene(scenePath);
        }

        private static IReadOnlyDictionary<long, int[]> ParseScene(string scenePath)
        {
            var result = new Dictionary<long, int[]>();
            using var reader = new StreamReader(scenePath);
            bool inMeshRenderer = false;
            long gameObjectId = 0;
            string subsetHex = string.Empty;

            void Flush()
            {
                if (!inMeshRenderer || gameObjectId == 0 || string.IsNullOrEmpty(subsetHex)) return;
                int[] decoded = DecodeLittleEndianInt32Array(subsetHex);
                if (!result.TryAdd(gameObjectId, decoded))
                    throw new InvalidDataException("Extracted GAME scene has multiple static-batch MeshRenderers for GameObject " + gameObjectId);
            }

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    Flush();
                    inMeshRenderer = line.StartsWith("--- !u!23 &", StringComparison.Ordinal);
                    gameObjectId = 0;
                    subsetHex = string.Empty;
                    continue;
                }
                if (!inMeshRenderer) continue;
                string trimmed = line.Trim();
                if (trimmed.StartsWith("m_GameObject: {fileID: ", StringComparison.Ordinal))
                {
                    int start = "m_GameObject: {fileID: ".Length;
                    int end = trimmed.IndexOf('}', start);
                    gameObjectId = long.Parse(trimmed[start..end], CultureInfo.InvariantCulture);
                }
                else if (trimmed.StartsWith("m_SubsetIndices: ", StringComparison.Ordinal))
                {
                    subsetHex = trimmed["m_SubsetIndices: ".Length..].Trim();
                }
            }
            Flush();
            return result;
        }

        private static int[] DecodeLittleEndianInt32Array(string hex)
        {
            if (hex.Length == 0 || hex.Length % 8 != 0 || hex.Any(character => !Uri.IsHexDigit(character)))
                throw new FormatException("Invalid MeshRenderer m_SubsetIndices payload: " + hex);
            var result = new int[hex.Length / 8];
            for (int index = 0; index < result.Length; index++)
            {
                int offset = index * 8;
                int byte0 = int.Parse(hex.Substring(offset, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int byte1 = int.Parse(hex.Substring(offset + 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int byte2 = int.Parse(hex.Substring(offset + 4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                int byte3 = int.Parse(hex.Substring(offset + 6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                result[index] = byte0 | byte1 << 8 | byte2 << 16 | byte3 << 24;
                if (result[index] < 0) throw new FormatException("Negative static-batch submesh index is unsupported.");
            }
            return result;
        }

        private static string SafeCombine(string root, string relative)
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Static-batch source path escapes the frozen extraction root.");
            return candidate;
        }
    }
}
