using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MSC.World.Data;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.WorldTransfer
{
    public sealed class WorldMapGeometryEvaluationValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public int EligibleEntityCount { get; internal set; }
        public int CellCount { get; internal set; }
        public int UniqueMeshCount { get; internal set; }
        public int ActualMeshEntityCount { get; internal set; }
        public int BoundsFallbackCount { get; internal set; }
        public bool IsValid => Errors.Count == 0;
    }

    public static class WorldMapGeometryEvaluationValidator
    {
        public const int ExpectedEligibleEntityCount = 3842;
        public const int ExpectedCellCount = 49;
        public const int ExpectedUniqueMeshCount = 503;
        public const int ExpectedActualMeshEntityCount = 2784;
        public const int ExpectedBoundsFallbackCount = 1058;
        public const int ExpectedStaticBatchEntityCount = 1683;

        public static WorldMapGeometryEvaluationValidationResult Validate(bool requireGeneratedScenes)
        {
            var result = new WorldMapGeometryEvaluationValidationResult();
            IReadOnlyList<WorldEntityPlacement> records;
            try
            {
                ValidateFrozenStaging(result);
                records = WorldEntityTable.Parse(File.ReadAllText(
                    WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.EntityTableAssetPath)));
            }
            catch (Exception exception)
            {
                result.Errors.Add(exception.Message);
                return result;
            }

            WorldEntityPlacement[] eligible = records.Where(record => record.ReferenceWorldEligible).ToArray();
            WorldEntityPlacement[] meshEntities = eligible.Where(record => WorldReferenceMeshLibrarySync.IsUsableMeshGuid(record.MeshGuid)).ToArray();
            result.EligibleEntityCount = eligible.Length;
            result.CellCount = eligible.Where(record => record.CellId != "global").Select(record => record.CellId).Distinct(StringComparer.Ordinal).Count();
            result.UniqueMeshCount = meshEntities.Select(record => record.MeshGuid).Distinct(StringComparer.Ordinal).Count();
            result.ActualMeshEntityCount = meshEntities.Length;
            result.BoundsFallbackCount = eligible.Length - meshEntities.Length;

            RequireCount(result, "eligible reference entities", result.EligibleEntityCount, ExpectedEligibleEntityCount);
            RequireCount(result, "concrete cells", result.CellCount, ExpectedCellCount);
            RequireCount(result, "unique mesh GUIDs", result.UniqueMeshCount, ExpectedUniqueMeshCount);
            RequireCount(result, "actual-mesh entities", result.ActualMeshEntityCount, ExpectedActualMeshEntityCount);
            RequireCount(result, "bounds fallbacks", result.BoundsFallbackCount, ExpectedBoundsFallbackCount);

            ValidateDatabaseRecords(eligible, result);
            ValidateMeshLibrary(meshEntities, result);
            ValidateStaticBatchSubsets(eligible, result);
            ValidateBuildIsolation(result);
            if (requireGeneratedScenes) ValidateGeneratedScenes(eligible, result);
            return result;
        }

        private static void ValidateFrozenStaging(WorldMapGeometryEvaluationValidationResult result)
        {
            WorldTransferEditorConfiguration config = WorldTransferEditorConfiguration.Load();
            if (!Directory.Exists(config.RawExtractionPath)) result.Errors.Add("Frozen raw world extraction is missing: " + config.RawExtractionPath);
            if (!Directory.Exists(config.NormalizedDataPath)) result.Errors.Add("Frozen normalized world data is missing: " + config.NormalizedDataPath);
            foreach (string file in new[] { "WorldMeshManifest.csv", "WorldGeometryManifest.json", "WorldObjectPlacements.csv" })
            {
                if (!File.Exists(Path.Combine(config.NormalizedDataPath, file))) result.Errors.Add("Frozen world fixture is missing: " + file);
            }

            string database = File.ReadAllText(WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.DatabaseAssetPath));
            if (!database.Contains("\"databaseVersion\": \"04A1.1\"", StringComparison.Ordinal) ||
                !database.Contains("39e5c8f38edd83652325b403e06449eb6fe4e5c588e4dad2bf04b5c9ff406c31", StringComparison.Ordinal))
                result.Errors.Add("Frozen 04A1.1 database version/source hash stamp is missing.");
        }

        private static void ValidateDatabaseRecords(IEnumerable<WorldEntityPlacement> records, WorldMapGeometryEvaluationValidationResult result)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (WorldEntityPlacement record in records)
            {
                if (!ids.Add(record.StableId)) result.Errors.Add("Duplicate eligible stable ID: " + record.StableId);
                if (!IsFinite(record.Position) || !IsFinite(record.Bounds.min) || !IsFinite(record.Bounds.max))
                    result.Errors.Add("Non-finite placement/bounds: " + record.StableId);
                float rotationMagnitude = Mathf.Sqrt(
                    record.Rotation.x * record.Rotation.x + record.Rotation.y * record.Rotation.y +
                    record.Rotation.z * record.Rotation.z + record.Rotation.w * record.Rotation.w);
                if (Mathf.Abs(rotationMagnitude - 1f) > 0.0001f)
                    result.Errors.Add("Non-normalized source rotation: " + record.StableId);
                if (WorldReferenceMeshLibrarySync.IsUsableMeshGuid(record.MeshGuid) &&
                    (Mathf.Abs(record.Scale.x) < 0.000001f || Mathf.Abs(record.Scale.y) < 0.000001f || Mathf.Abs(record.Scale.z) < 0.000001f || !IsFinite(record.Scale)))
                    result.Errors.Add("Actual-mesh entity has invalid/zero scale: " + record.StableId);
                if (string.IsNullOrWhiteSpace(record.CellId) || string.Equals(record.CellId, "excluded", StringComparison.Ordinal))
                    result.Errors.Add("Eligible entity lacks generated cell ownership: " + record.StableId);
            }
        }

        private static void ValidateMeshLibrary(IEnumerable<WorldEntityPlacement> meshEntities, WorldMapGeometryEvaluationValidationResult result)
        {
            foreach (string guid in meshEntities.Select(record => record.MeshGuid).Distinct(StringComparer.Ordinal))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!WorldReferenceMeshLibrarySync.IsBelowReferenceMeshRoot(path))
                    result.Errors.Add($"Mesh GUID {guid} does not resolve below the ignored reference library (resolved '{path}').");
                else if (AssetDatabase.LoadAssetAtPath<Mesh>(path) == null)
                    result.Errors.Add("Reference mesh failed to import as Mesh: " + path);
            }
        }

        private static void ValidateGeneratedScenes(IReadOnlyList<WorldEntityPlacement> eligible, WorldMapGeometryEvaluationValidationResult result)
        {
            string generatedRoot = WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.GeneratedSceneRoot);
            if (!Directory.Exists(generatedRoot))
            {
                result.Errors.Add("Generated 05C scene root is missing.");
                return;
            }

            string[] cellScenes = Directory.GetFiles(generatedRoot, "World_cell_*.unity");
            RequireCount(result, "generated cell scenes", cellScenes.Length, ExpectedCellCount);
            foreach (string required in new[] { WorldTransferPaths.BootstrapScene, WorldTransferPaths.PersistentScene, WorldTransferPaths.GlobalScene })
            {
                if (!File.Exists(WorldTransferPaths.ToAbsoluteProjectPath(required))) result.Errors.Add("Generated scene is missing: " + required);
            }

            var generatedIds = new HashSet<string>(StringComparer.Ordinal);
            int actualCount = 0;
            int fallbackCount = 0;
            foreach (IGrouping<string, WorldEntityPlacement> group in eligible.GroupBy(record => record.CellId, StringComparer.Ordinal))
            {
                string path = group.Key == "global" ? WorldTransferPaths.GlobalScene : WorldTransferPaths.CellScene(group.Key);
                string absolutePath = WorldTransferPaths.ToAbsoluteProjectPath(path);
                if (!File.Exists(absolutePath))
                {
                    result.Errors.Add("Generated reference scene is missing: " + path);
                    continue;
                }

                int sceneEntityCount = 0;
                int sceneActualCount = 0;
                int sceneFallbackCount = 0;
                bool hasGeometryRoot = false;
                bool hasFallbackRoot = false;
                bool hasVersionStamp = false;
                foreach (string rawLine in File.ReadLines(absolutePath))
                {
                    string line = rawLine.Trim();
                    if (line == "m_Name: DONOR_REFERENCE_GEOMETRY") hasGeometryRoot = true;
                    else if (line == "m_Name: BOUNDS_AND_MISSING_PROXIES") hasFallbackRoot = true;
                    else if (line == "generatorVersion: " + WorldPartitionBuilder.GeneratorVersion) hasVersionStamp = true;
                    else if (line.StartsWith("stableId: ", StringComparison.Ordinal))
                    {
                        sceneEntityCount++;
                        string id = line["stableId: ".Length..];
                        if (!generatedIds.Add(id)) result.Errors.Add("Duplicate generated stable ID: " + id);
                    }
                    else if (line == "visualizationKind: 0") sceneActualCount++;
                    else if (line == "visualizationKind: 1") sceneFallbackCount++;
                }

                int expectedActual = group.Count(record => WorldReferenceMeshLibrarySync.IsUsableMeshGuid(record.MeshGuid));
                int expectedFallback = group.Count() - expectedActual;
                if (!hasGeometryRoot || !hasFallbackRoot || !hasVersionStamp)
                    result.Errors.Add("05C hierarchy/version stamp mismatch: " + path);
                if (sceneEntityCount != group.Count() || sceneActualCount != expectedActual || sceneFallbackCount != expectedFallback)
                    result.Errors.Add($"05C scene coverage mismatch in {path}: entities {sceneEntityCount}/{group.Count()}, actual {sceneActualCount}/{expectedActual}, fallback {sceneFallbackCount}/{expectedFallback}.");
                actualCount += sceneActualCount;
                fallbackCount += sceneFallbackCount;
            }

            if (!eligible.Select(record => record.StableId).ToHashSet(StringComparer.Ordinal).SetEquals(generatedIds))
                result.Errors.Add("Generated 05C stable-ID set differs from frozen eligible records.");
            RequireCount(result, "generated actual-mesh entities", actualCount, ExpectedActualMeshEntityCount);
            RequireCount(result, "generated bounds fallbacks", fallbackCount, ExpectedBoundsFallbackCount);
        }

        private static void ValidateStaticBatchSubsets(IReadOnlyList<WorldEntityPlacement> eligible, WorldMapGeometryEvaluationValidationResult result)
        {
            IReadOnlyDictionary<long, int[]> subsets;
            try
            {
                subsets = WorldStaticBatchSubsetTable.ParseCommittedTable();
            }
            catch (Exception exception)
            {
                result.Errors.Add(exception.Message);
                return;
            }

            RequireCount(result, "eligible static-batch records", subsets.Count, ExpectedStaticBatchEntityCount);
            Dictionary<long, WorldEntityPlacement> bySourceObject = eligible.ToDictionary(record => record.SourceObjectId);
            foreach ((long sourceObjectId, int[] indices) in subsets)
            {
                if (!bySourceObject.TryGetValue(sourceObjectId, out WorldEntityPlacement record))
                {
                    result.Errors.Add("Static-batch subset references a non-eligible source object: " + sourceObjectId);
                    continue;
                }
                if (!WorldReferenceMeshLibrarySync.IsUsableMeshGuid(record.MeshGuid))
                {
                    result.Errors.Add("Static-batch record lacks a usable mesh GUID: " + record.StableId);
                    continue;
                }
                string derivedPath = WorldTransferPaths.GeneratedMeshRoot + "/" + record.StableId + ".asset";
                Mesh derived = AssetDatabase.LoadAssetAtPath<Mesh>(derivedPath);
                if (derived == null || derived.vertexCount == 0 || derived.subMeshCount != indices.Length)
                    result.Errors.Add("Derived static-batch mesh is missing or incomplete: " + derivedPath);
                else if (!IsFinite(derived.bounds.min) || !IsFinite(derived.bounds.max))
                    result.Errors.Add("Derived static-batch mesh has non-finite bounds: " + derivedPath);
            }

            int generatedMeshCount = Directory.Exists(WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.GeneratedMeshRoot))
                ? Directory.EnumerateFiles(WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.GeneratedMeshRoot), "*.asset").Count()
                : 0;
            RequireCount(result, "generated static-batch subset meshes", generatedMeshCount, ExpectedStaticBatchEntityCount);
        }

        private static void ValidateBuildIsolation(WorldMapGeometryEvaluationValidationResult result)
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path.StartsWith(WorldTransferPaths.ReferenceRoot + "/", StringComparison.Ordinal))
                    result.Errors.Add("Reference-only world scene is present in Build Settings: " + scene.path);
            }

            string gitIgnore = File.ReadAllText(WorldTransferPaths.ToAbsoluteProjectPath(".gitignore"));
            if (!gitIgnore.Contains("Assets/Game/LegacyImport/ReferenceOnly/**", StringComparison.Ordinal))
                result.Errors.Add("ReferenceOnly tree is not excluded by .gitignore.");

            string[] productionAssets = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith("Assets/Game/", StringComparison.Ordinal) &&
                               !path.StartsWith(WorldTransferPaths.ReferenceRoot + "/", StringComparison.Ordinal))
                .ToArray();
            string[] leakedDependencies = AssetDatabase.GetDependencies(productionAssets, true)
                .Where(path => path.StartsWith(WorldTransferPaths.ReferenceRoot + "/", StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal).ToArray();
            foreach (string leaked in leakedDependencies) result.Errors.Add("Production asset depends on reference-only donor content: " + leaked);
        }

        private static void RequireCount(WorldMapGeometryEvaluationValidationResult result, string label, int actual, int expected)
        {
            if (actual != expected) result.Errors.Add($"Unexpected {label}: {actual.ToString(CultureInfo.InvariantCulture)}; expected {expected.ToString(CultureInfo.InvariantCulture)}.");
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        public static void RunBatch()
        {
            WorldMapGeometryEvaluationValidationResult result = Validate(requireGeneratedScenes: true);
            foreach (string warning in result.Warnings) Debug.LogWarning("WORLD_MAP_05C_WARNING " + warning);
            if (!result.IsValid) throw new InvalidOperationException("World map 05C validation failed:\n" + string.Join("\n", result.Errors));
            Debug.Log($"WORLD_MAP_05C_VALIDATION_OK eligible={result.EligibleEntityCount} cells={result.CellCount} meshes={result.UniqueMeshCount} actual={result.ActualMeshEntityCount} fallback={result.BoundsFallbackCount}");
        }
    }
}
