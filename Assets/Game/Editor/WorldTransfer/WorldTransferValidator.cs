using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MSC.LegacyImport.Editor.Pipeline;
using MSC.World.Data;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.WorldTransfer
{
    public sealed class WorldTransferValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public int EntityCount { get; set; }
        public int EligibleEntityCount { get; set; }
        public int CellCount { get; set; }
        public bool IsValid => Errors.Count == 0;
    }

    public static class WorldTransferValidator
    {
        public static WorldTransferValidationResult Validate(bool requireGeneratedScenes)
        {
            var result = new WorldTransferValidationResult();
            ValidatePaths(result);
            if (result.Errors.Count > 0)
                return result;

            string databasePath = WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.DatabaseAssetPath);
            string entityPath = WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.EntityTableAssetPath);
            if (!File.Exists(databasePath)) result.Errors.Add("Missing world database: " + WorldTransferPaths.DatabaseAssetPath);
            if (!File.Exists(entityPath)) result.Errors.Add("Missing world entity table: " + WorldTransferPaths.EntityTableAssetPath);
            if (result.Errors.Count > 0) return result;

            string databaseJson = File.ReadAllText(databasePath);
            if (!databaseJson.Contains("\"schemaVersion\": 1", StringComparison.Ordinal) ||
                !databaseJson.Contains("\"databaseVersion\": \"04A1.1\"", StringComparison.Ordinal))
                result.Errors.Add("World database version stamp is missing or unsupported.");

            IReadOnlyList<WorldEntityPlacement> records;
            try
            {
                records = WorldEntityTable.Parse(File.ReadAllText(entityPath));
            }
            catch (Exception exception)
            {
                result.Errors.Add("World entity table parse failed: " + exception.Message);
                return result;
            }

            result.EntityCount = records.Count;
            result.EligibleEntityCount = records.Count(record => record.ReferenceWorldEligible);
            result.CellCount = records.Where(record => record.ReferenceWorldEligible && record.CellId != "global")
                .Select(record => record.CellId).Distinct(StringComparer.Ordinal).Count();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorldEntityPlacement record in records)
            {
                if (!WorldStableId.TryParse(record.StableId, out _)) result.Errors.Add("Invalid stable ID: " + record.StableId);
                else if (!ids.Add(record.StableId)) result.Errors.Add("Duplicate stable ID: " + record.StableId);
                if (!IsFinite(record.Position) || !IsFinite(record.Bounds.min) || !IsFinite(record.Bounds.max))
                    result.Errors.Add("Non-finite transform/bounds: " + record.StableId);
                if (record.ReferenceWorldEligible && string.Equals(record.CellId, "excluded", StringComparison.Ordinal))
                    result.Errors.Add("Eligible world entity has excluded cell: " + record.StableId);
                if (!record.ReferenceWorldEligible && !string.Equals(record.CellId, "excluded", StringComparison.Ordinal))
                    result.Errors.Add("Non-world entity was assigned to a generated cell: " + record.StableId);
            }

            ValidateParentReferences(entityPath, ids, result);
            if (requireGeneratedScenes)
            {
                if (!File.Exists(WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.BootstrapScene)))
                    result.Errors.Add("Generated bootstrap scene is missing.");
                if (!File.Exists(WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.PersistentScene)))
                    result.Errors.Add("Generated persistent scene is missing.");
                if (!File.Exists(WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.GlobalScene)))
                    result.Errors.Add("Generated global scene is missing.");
                int generatedCells = Directory.Exists(WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.GeneratedSceneRoot))
                    ? Directory.EnumerateFiles(WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.GeneratedSceneRoot), "World_cell_*.unity").Count()
                    : 0;
                if (generatedCells != result.CellCount)
                    result.Errors.Add($"Generated cell scene count {generatedCells} does not match database cell count {result.CellCount}.");
                ValidateGeneratedSceneContents(records, result);
            }

            return result;
        }

        private static void ValidateGeneratedSceneContents(IReadOnlyList<WorldEntityPlacement> records, WorldTransferValidationResult result)
        {
            WorldEntityPlacement[] eligible = records.Where(record => record.ReferenceWorldEligible).ToArray();
            var generatedIds = new HashSet<string>(StringComparer.Ordinal);
            var generatedCategories = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (IGrouping<string, WorldEntityPlacement> group in eligible.GroupBy(record => record.CellId, StringComparer.Ordinal))
            {
                string path = group.Key == "global" ? WorldTransferPaths.GlobalScene : WorldTransferPaths.CellScene(group.Key);
                string absolutePath = WorldTransferPaths.ToAbsoluteProjectPath(path);
                int expectedCount = group.Count();
                bool hasDatabaseStamp = false;
                bool hasCellStamp = false;
                bool hasCountStamp = false;
                int sceneEntityCount = 0;
                foreach (string rawLine in File.ReadLines(absolutePath))
                {
                    string line = rawLine.Trim();
                    if (line == "databaseVersion: " + WorldPartitionBuilder.DatabaseVersion) hasDatabaseStamp = true;
                    else if (line == "cellId: " + group.Key) hasCellStamp = true;
                    else if (line == "generatedEntityCount: " + expectedCount.ToString(CultureInfo.InvariantCulture)) hasCountStamp = true;
                    else if (line.StartsWith("stableId: ", StringComparison.Ordinal))
                    {
                        string stableId = line["stableId: ".Length..];
                        sceneEntityCount++;
                        if (!generatedIds.Add(stableId)) result.Errors.Add("Duplicate generated stable ID: " + stableId);
                    }
                    else if (line.StartsWith("semanticCategory: ", StringComparison.Ordinal))
                    {
                        string category = line["semanticCategory: ".Length..];
                        generatedCategories[category] = generatedCategories.GetValueOrDefault(category) + 1;
                    }
                }

                if (!hasDatabaseStamp || !hasCellStamp || !hasCountStamp)
                    result.Errors.Add($"Generated scene stamp mismatch: {path}.");
                if (sceneEntityCount != expectedCount)
                    result.Errors.Add($"Generated scene '{path}' contains {sceneEntityCount} reference entities; expected {expectedCount}.");
            }

            HashSet<string> expectedIds = eligible.Select(record => record.StableId).ToHashSet(StringComparer.Ordinal);
            if (!expectedIds.SetEquals(generatedIds)) result.Errors.Add("Generated stable-ID set does not match the eligible database records.");
            foreach (IGrouping<string, WorldEntityPlacement> expectedCategory in eligible.GroupBy(record => record.Category, StringComparer.Ordinal))
            {
                if (generatedCategories.GetValueOrDefault(expectedCategory.Key) != expectedCategory.Count())
                    result.Errors.Add("Generated category count mismatch: " + expectedCategory.Key);
            }
        }

        public static void ValidatePaths(WorldTransferValidationResult result)
        {
            try
            {
                WorldTransferEditorConfiguration config = WorldTransferEditorConfiguration.Load();
                if (!Directory.Exists(config.DonorGamePath)) result.Errors.Add("Configured donor game path does not exist.");
                if (!Directory.Exists(config.DonorStagingPath)) result.Errors.Add("Configured donor staging path does not exist.");
                if (!Directory.Exists(config.RawExtractionPath)) result.Errors.Add("Raw world extraction path does not exist.");
                if (!Directory.Exists(config.NormalizedDataPath)) result.Errors.Add("Normalized world data path does not exist.");
                string donorData = Path.Combine(config.DonorGamePath, "mysummercar_Data");
                ValidateSourceHash(donorData, "level2", "39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31", result);
                ValidateSourceHash(donorData, "sharedassets3.assets", "1e956c8acd9f3c075b2e4eece3d836228eeb3ad0a18ae41ad1ca6aedb3c9d684", result);
                ValidateSourceHash(donorData, "sharedassets3.resource", "19797fa0386c74d091b530b874a03325191e002c921767240c71ba7791a5a20b", result);

                foreach (string manifest in new[]
                         {
                             "WorldGeometryManifest.json", "WorldObjectPlacements.csv", "WorldMeshManifest.csv",
                             "WorldColliderManifest.csv", "WorldTerrainManifest.json", "WorldRoadManifest.json",
                             "WorldWaterManifest.json", "WorldVegetationManifest.csv", "WorldInteriorManifest.csv",
                             "WorldLandmarkManifest.csv", "WorldMissingReferences.csv", "WorldUnsupportedObjects.csv"
                         })
                {
                    if (!File.Exists(Path.Combine(config.NormalizedDataPath, manifest)))
                        result.Errors.Add("Normalized world manifest is missing: " + manifest);
                }
            }
            catch (Exception exception)
            {
                result.Errors.Add(exception.Message);
            }
        }

        private static void ValidateSourceHash(string donorDataPath, string fileName, string expectedSha256, WorldTransferValidationResult result)
        {
            string path = Path.Combine(donorDataPath, fileName);
            if (!File.Exists(path))
            {
                result.Errors.Add("Donor source is missing: " + fileName);
            }
            else if (!Sha256FileHasher.Matches(path, expectedSha256))
            {
                result.Errors.Add("Donor source hash differs from database provenance: " + fileName);
            }
        }

        private static void ValidateParentReferences(string entityPath, HashSet<string> entityIds, WorldTransferValidationResult result)
        {
            using var reader = new StreamReader(entityPath);
            List<string> headers = WorldEntityTable.ParseRow(reader.ReadLine() ?? string.Empty);
            int stableIndex = headers.IndexOf("StableId");
            int parentIndex = headers.IndexOf("ParentStableId");
            if (stableIndex < 0 || parentIndex < 0)
            {
                result.Errors.Add("Entity table does not contain stable/parent ID columns.");
                return;
            }

            string line;
            int unresolvedParentCount = 0;
            string firstExample = string.Empty;
            while ((line = reader.ReadLine()) != null)
            {
                List<string> values = WorldEntityTable.ParseRow(line);
                string parent = values[parentIndex];
                if (!string.IsNullOrEmpty(parent) && !entityIds.Contains(parent))
                {
                    unresolvedParentCount++;
                    if (string.IsNullOrEmpty(firstExample)) firstExample = $"{parent} for {values[stableIndex]}";
                }
            }
            if (unresolvedParentCount > 0)
                result.Warnings.Add($"{unresolvedParentCount} geometry parents are scanned non-geometry placements (example {firstExample}); full hierarchy remains in external WorldObjectPlacements.csv.");
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        public static void RunBatch()
        {
            WorldTransferValidationResult result = Validate(requireGeneratedScenes: true);
            foreach (string warning in result.Warnings) Debug.LogWarning("WORLD_TRANSFER_WARNING " + warning);
            if (!result.IsValid)
                throw new InvalidOperationException("World transfer validation failed:\n" + string.Join("\n", result.Errors));
            Debug.Log($"WORLD_TRANSFER_VALIDATION_OK entities={result.EntityCount} eligible={result.EligibleEntityCount} cells={result.CellCount} warnings={result.Warnings.Count}");
        }
    }
}
