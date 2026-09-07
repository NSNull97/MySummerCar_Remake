using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MSC.Editor.WorldBaseline
{
    /// <summary>
    /// Prevents project-authored Unity objects from entering a player with a
    /// session-local or missing MonoScript reference.
    /// </summary>
    public sealed class ProjectSerializedScriptReferenceBuildGuard :
        IPreprocessBuildWithReport
    {
        public const string SupplementalScriptPath =
            "Assets/Game/LegacyImport/Runtime/" +
            "DonorWorldSupplementalEntityMetadata.cs";
        public const string CalibrationScriptPath =
            "Assets/Game/Lighting/Runtime/LightingCalibrationProfile.cs";
        public const string CalibrationAssetPath =
            "Assets/Game/Lighting/Content/Profiles/" +
            "Phase1LightingCalibration.asset";
        public const string LightingCatalogAssetPath =
            "Assets/Game/Lighting/Content/Profiles/" +
            "Phase1LocalLightingCatalog.asset";
        public const string SupplementalSourceScenePath =
            "Assets/Scenes/Generated/FullMap_UnifiedBaseTerrain.unity";
        public const int ExpectedSupplementalRecordCount = 328;

        private const string SupplementalClassIdentifier =
            "MSC.LegacyImport.Runtime::MSC.LegacyImport." +
            "DonorWorldSupplementalEntityMetadata";
        private const string CalibrationClassIdentifier =
            "MSC.Lighting.Runtime:MSC.Lighting:LightingCalibrationProfile";
        private const string CanonicalCalibrationClassIdentifier =
            "MSC.Lighting.Runtime::MSC.Lighting." +
            "LightingCalibrationProfile";

        private static readonly IReadOnlyDictionary<string, int>
            SupplementalSceneCounts = new Dictionary<string, int>(
                StringComparer.Ordinal)
            {
                [CellScene("-3_-4")] = 43,
                [CellScene("-3_0")] = 75,
                [CellScene("-2_-4")] = 6,
                [CellScene("-2_0")] = 55,
                [CellScene("1_-5")] = 17,
                [CellScene("3_-4")] = 24,
                [CellScene("3_-1")] = 63,
                [CellScene("4_-4")] = 6,
                [CellScene("4_-3")] = 39,
            };

        private static readonly Regex ValidScriptReference = new Regex(
            @"^m_Script: \{fileID: 11500000, guid: " +
            @"(?<guid>[0-9a-f]{32}), type: 3\}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant |
            RegexOptions.IgnoreCase);
        private static readonly Regex ValidBuiltInScriptReference = new Regex(
            @"^m_Script: \{fileID: [1-9][0-9]*, guid: " +
            @"0000000000000000e000000000000000, type: 0\}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant |
            RegexOptions.IgnoreCase);

        public int callbackOrder => 10_000;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateProjectAuthoredReferences();
        }

        [MenuItem(
            "MSC/Validation/Validate Serialized Script References",
            priority = 2050)]
        public static void ValidateProjectAuthoredReferences()
        {
            var failures = new List<string>();
            string supplementalGuid = ValidateMonoScript(
                SupplementalScriptPath,
                "MSC.LegacyImport.DonorWorldSupplementalEntityMetadata",
                "MSC.LegacyImport.Runtime",
                failures);
            string calibrationGuid = ValidateMonoScript(
                CalibrationScriptPath,
                "MSC.Lighting.LightingCalibrationProfile",
                "MSC.Lighting.Runtime",
                failures);

            bool hasAnyGeneratedScene = SupplementalSceneCounts.Keys.Any(
                AssetExists);
            var cellRecords = new List<MonoBehaviourRecord>();
            if (hasAnyGeneratedScene)
            {
                foreach (KeyValuePair<string, int> scene in
                         SupplementalSceneCounts)
                {
                    cellRecords.AddRange(ValidateTargetRecords(
                        scene.Key,
                        SupplementalClassIdentifier,
                        supplementalGuid,
                        scene.Value,
                        failures));
                }

                ValidateStableIds(
                    cellRecords,
                    "generated cell supplemental records",
                    failures);
                if (cellRecords.Count != ExpectedSupplementalRecordCount)
                {
                    failures.Add(
                        "Expected " + ExpectedSupplementalRecordCount +
                        " supplemental records but validated " +
                        cellRecords.Count + ".");
                }
            }

            bool hasSupplementalSource = AssetExists(
                SupplementalSourceScenePath);
            List<MonoBehaviourRecord> sourceRecords =
                hasSupplementalSource
                    ? ValidateTargetRecords(
                        SupplementalSourceScenePath,
                        SupplementalClassIdentifier,
                        supplementalGuid,
                        ExpectedSupplementalRecordCount,
                        failures)
                    : new List<MonoBehaviourRecord>();
            if (hasSupplementalSource)
            {
                ValidateStableIds(
                    sourceRecords,
                    "generated source supplemental records",
                    failures);
            }
            if (hasAnyGeneratedScene && hasSupplementalSource)
            {
                ValidateMatchingStableIdSets(
                    sourceRecords,
                    cellRecords,
                    failures);
            }

            ValidateTargetRecords(
                CalibrationAssetPath,
                CalibrationClassIdentifier,
                calibrationGuid,
                expectedCount: 1,
                failures);
            ValidateLightingAssets(failures);

            ProjectScanResult scan = ScanProjectAuthoredYaml();
            if (scan.InvalidScriptReferenceCount > 0)
            {
                failures.Add(
                    scan.InvalidScriptReferenceCount +
                    " project-authored m_Script reference(s) are missing " +
                    "a permanent MonoScript GUID. " +
                    string.Join(" | ", scan.InvalidScriptExamples));
            }
            if (scan.UnresolvedScriptGuids.Count > 0)
            {
                failures.Add(
                    scan.UnresolvedScriptGuids.Count +
                    " permanent-looking script GUID(s) do not resolve to a " +
                    "compiled MonoScript: " +
                    string.Join(", ", scan.UnresolvedScriptGuids));
            }
            int expectedYamlSupplementalCount = hasAnyGeneratedScene
                ? ExpectedSupplementalRecordCount
                : 0;
            if (scan.SupplementalClassIdentifierCount !=
                expectedYamlSupplementalCount)
            {
                failures.Add(
                    "Build-dependency YAML contains " +
                    scan.SupplementalClassIdentifierCount +
                    " supplemental class identifier(s); expected " +
                    expectedYamlSupplementalCount + ".");
            }
            if (scan.CalibrationClassIdentifierCount != 1)
            {
                failures.Add(
                    "Project YAML contains " +
                    scan.CalibrationClassIdentifierCount +
                    " calibration class identifier(s); expected 1.");
            }

            if (failures.Count > 0)
            {
                throw new BuildFailedException(
                    "Serialized script reference validation failed:\n - " +
                    string.Join("\n - ", failures));
            }

            Debug.Log(
                "Serialized script reference validation passed. " +
                (hasAnyGeneratedScene
                    ? ExpectedSupplementalRecordCount +
                      " cell supplemental records, " +
                      (hasSupplementalSource
                          ? ExpectedSupplementalRecordCount +
                            " matching source records, and "
                          : string.Empty) +
                      "one calibration asset use permanent MonoScript GUIDs."
                    : "The optional generated donor baseline is absent; " +
                      "the tracked calibration asset and all available " +
                      "project-authored YAML use permanent MonoScript GUIDs."));
        }

        public static bool IsResolvablePermanentScriptReference(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            Match match = ValidScriptReference.Match(line.Trim());
            if (!match.Success)
            {
                return false;
            }

            string assetPath = AssetDatabase.GUIDToAssetPath(
                match.Groups["guid"].Value);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                assetPath);
            return script != null && script.GetClass() != null;
        }

        private static string ValidateMonoScript(
            string assetPath,
            string expectedFullName,
            string expectedAssembly,
            ICollection<string> failures)
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                assetPath);
            if (script == null)
            {
                failures.Add("Missing MonoScript at '" + assetPath + "'.");
                return string.Empty;
            }

            Type scriptClass = script.GetClass();
            if (scriptClass == null ||
                !string.Equals(
                    scriptClass.FullName,
                    expectedFullName,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    scriptClass.Assembly.GetName().Name,
                    expectedAssembly,
                    StringComparison.Ordinal))
            {
                failures.Add(
                    "MonoScript '" + assetPath + "' resolves to '" +
                    (scriptClass?.AssemblyQualifiedName ?? "<null>") +
                    "', expected '" + expectedFullName + ", " +
                    expectedAssembly + "'.");
            }

            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrWhiteSpace(guid))
            {
                failures.Add(
                    "MonoScript '" + assetPath +
                    "' has no permanent asset GUID.");
            }

            return guid;
        }

        private static List<MonoBehaviourRecord> ValidateTargetRecords(
            string assetPath,
            string expectedClassIdentifier,
            string expectedGuid,
            int expectedCount,
            ICollection<string> failures)
        {
            if (!AssetExists(assetPath))
            {
                failures.Add("Missing serialized target '" + assetPath + "'.");
                return new List<MonoBehaviourRecord>();
            }

            string expectedScriptReference =
                "{fileID: 11500000, guid: " + expectedGuid +
                ", type: 3}";
            List<MonoBehaviourRecord> targets = ReadTargetRecords(
                assetPath,
                expectedClassIdentifier);
            if (targets.Count != expectedCount)
            {
                failures.Add(
                    "Serialized target '" + assetPath + "' contains " +
                    targets.Count + " record(s) for '" +
                    expectedClassIdentifier + "'; expected " +
                    expectedCount + ".");
            }

            int invalidCount = targets.Count(record => !string.Equals(
                record.ScriptReference,
                expectedScriptReference,
                StringComparison.Ordinal));
            if (invalidCount > 0)
            {
                failures.Add(
                    "Serialized target '" + assetPath + "' contains " +
                    invalidCount + " record(s) for '" +
                    expectedClassIdentifier +
                    "' without the expected permanent GUID '" +
                    expectedGuid + "'.");
            }

            return targets;
        }

        private static void ValidateStableIds(
            IReadOnlyCollection<MonoBehaviourRecord> records,
            string description,
            ICollection<string> failures)
        {
            int missingCount = records.Count(record =>
                string.IsNullOrWhiteSpace(record.StableId));
            int uniqueCount = records
                .Select(record => record.StableId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .Count();
            if (missingCount > 0 || uniqueCount != records.Count)
            {
                failures.Add(
                    description + " contain " + missingCount +
                    " missing and " + (records.Count - uniqueCount) +
                    " duplicate stable ID occurrence(s).");
            }
        }

        private static void ValidateMatchingStableIdSets(
            IReadOnlyCollection<MonoBehaviourRecord> sourceRecords,
            IReadOnlyCollection<MonoBehaviourRecord> cellRecords,
            ICollection<string> failures)
        {
            var sourceIds = new HashSet<string>(
                sourceRecords.Select(record => record.StableId),
                StringComparer.Ordinal);
            var cellIds = new HashSet<string>(
                cellRecords.Select(record => record.StableId),
                StringComparer.Ordinal);
            if (!sourceIds.SetEquals(cellIds))
            {
                failures.Add(
                    "Generated source and cell supplemental stable-ID sets " +
                    "do not match. Source-only=" +
                    sourceIds.Except(cellIds).Count() + ", cell-only=" +
                    cellIds.Except(sourceIds).Count() + ".");
            }
        }

        private static void ValidateLightingAssets(
            ICollection<string> failures)
        {
            UnityEngine.Object calibration =
                AssetDatabase.LoadMainAssetAtPath(CalibrationAssetPath);
            if (calibration == null ||
                !string.Equals(
                    calibration.GetType().FullName,
                    "MSC.Lighting.LightingCalibrationProfile",
                    StringComparison.Ordinal))
            {
                failures.Add(
                    "Calibration asset does not load as " +
                    "MSC.Lighting.LightingCalibrationProfile.");
                return;
            }

            UnityEngine.Object catalog = AssetDatabase.LoadMainAssetAtPath(
                LightingCatalogAssetPath);
            if (catalog == null)
            {
                failures.Add(
                    "Missing lighting catalog at '" +
                    LightingCatalogAssetPath + "'.");
                return;
            }

            var serializedCatalog = new SerializedObject(catalog);
            SerializedProperty calibrationProperty =
                serializedCatalog.FindProperty("calibration");
            if (calibrationProperty == null ||
                calibrationProperty.objectReferenceValue != calibration)
            {
                failures.Add(
                    "Lighting catalog does not retain the Phase 1 " +
                    "calibration asset binding.");
            }
        }

        private static ProjectScanResult ScanProjectAuthoredYaml()
        {
            var result = new ProjectScanResult();
            foreach (string path in EnumerateProjectYamlPaths())
            {
                string assetPath = ToAssetPath(path);
                int lineNumber = 0;
                foreach (string line in File.ReadLines(path))
                {
                    lineNumber++;
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith(
                            "m_Script:",
                            StringComparison.Ordinal))
                    {
                        Match match = ValidScriptReference.Match(trimmed);
                        if (!match.Success &&
                            !ValidBuiltInScriptReference.IsMatch(trimmed))
                        {
                            result.InvalidScriptReferenceCount++;
                            if (result.InvalidScriptExamples.Count < 20)
                            {
                                result.InvalidScriptExamples.Add(
                                    assetPath + ":" + lineNumber + " '" +
                                    trimmed + "'");
                            }
                        }
                        else
                        {
                            result.ScriptGuids.Add(
                                match.Groups["guid"].Value);
                        }
                    }
                    else if (string.Equals(
                                 trimmed,
                                 "m_EditorClassIdentifier: " +
                                 SupplementalClassIdentifier,
                                 StringComparison.Ordinal) &&
                             !string.Equals(
                                 assetPath,
                                 SupplementalSourceScenePath,
                                 StringComparison.Ordinal))
                    {
                        result.SupplementalClassIdentifierCount++;
                    }
                    else if (string.Equals(
                                 trimmed,
                                 "m_EditorClassIdentifier: " +
                                 CalibrationClassIdentifier,
                                 StringComparison.Ordinal) ||
                             string.Equals(
                                 trimmed,
                                 "m_EditorClassIdentifier: " +
                                 CanonicalCalibrationClassIdentifier,
                                 StringComparison.Ordinal))
                    {
                        result.CalibrationClassIdentifierCount++;
                    }
                }
            }

            foreach (string guid in result.ScriptGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(
                    assetPath);
                if (script == null || script.GetClass() == null)
                {
                    result.UnresolvedScriptGuids.Add(guid);
                }
            }

            return result;
        }

        private static IEnumerable<string> EnumerateProjectYamlPaths()
        {
            var paths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            string[] enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            foreach (string scenePath in enabledScenes)
            {
                AddSerializedAssetPath(paths, scenePath);
            }
            foreach (string dependency in AssetDatabase.GetDependencies(
                         enabledScenes,
                         recursive: true))
            {
                if (IsProjectAuthoredAssetPath(dependency))
                {
                    AddSerializedAssetPath(paths, dependency);
                }
            }

            AddSerializedAssetPath(paths, CalibrationAssetPath);
            AddSerializedAssetPath(paths, LightingCatalogAssetPath);

            return paths.OrderBy(path => path, StringComparer.Ordinal);
        }

        private static bool IsProjectAuthoredAssetPath(string assetPath)
        {
            return assetPath.StartsWith(
                       "Assets/Game/",
                       StringComparison.Ordinal) &&
                   !assetPath.StartsWith(
                       "Assets/Game/LegacyImport/ReferenceOnly/",
                       StringComparison.Ordinal);
        }

        private static void AddSerializedAssetPath(
            ISet<string> paths,
            string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath) ||
                !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !IsSerializedYamlPath(assetPath))
            {
                return;
            }

            string absolutePath = ToAbsolutePath(assetPath);
            if (File.Exists(absolutePath))
            {
                paths.Add(absolutePath);
            }
        }

        private static bool IsSerializedYamlPath(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".asset",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".prefab",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".unity",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static List<MonoBehaviourRecord> ReadTargetRecords(
            string assetPath,
            string expectedClassIdentifier)
        {
            var records = new List<MonoBehaviourRecord>();
            bool inMonoBehaviour = false;
            string scriptReference = null;
            string classIdentifier = null;
            string stableId = null;

            void FlushRecord()
            {
                if (inMonoBehaviour && ClassIdentifierMatches(
                        classIdentifier,
                        expectedClassIdentifier))
                {
                    records.Add(new MonoBehaviourRecord(
                        scriptReference,
                        classIdentifier,
                        stableId));
                }
                scriptReference = null;
                classIdentifier = null;
                stableId = null;
            }

            foreach (string line in File.ReadLines(ToAbsolutePath(assetPath)))
            {
                if (line.StartsWith("--- !u!", StringComparison.Ordinal))
                {
                    FlushRecord();
                    inMonoBehaviour = line.StartsWith(
                        "--- !u!114 ",
                        StringComparison.Ordinal);
                    continue;
                }
                if (!inMonoBehaviour)
                {
                    continue;
                }
                if (line.StartsWith("  m_Script: ", StringComparison.Ordinal))
                {
                    scriptReference = line.Substring("  m_Script: ".Length)
                        .Trim();
                }
                else if (line.StartsWith(
                             "  m_EditorClassIdentifier: ",
                             StringComparison.Ordinal))
                {
                    classIdentifier = line.Substring(
                            "  m_EditorClassIdentifier: ".Length)
                        .Trim();
                }
                else if (line.StartsWith(
                             "  stableId: ",
                             StringComparison.Ordinal))
                {
                    stableId = line.Substring("  stableId: ".Length).Trim();
                }
            }

            FlushRecord();
            return records;
        }

        private static bool ClassIdentifierMatches(
            string actual,
            string expected)
        {
            return string.Equals(actual, expected, StringComparison.Ordinal) ||
                   string.Equals(
                       expected,
                       CalibrationClassIdentifier,
                       StringComparison.Ordinal) &&
                   string.Equals(
                       actual,
                       CanonicalCalibrationClassIdentifier,
                       StringComparison.Ordinal);
        }

        private static bool AssetExists(string assetPath)
        {
            return File.Exists(ToAbsolutePath(assetPath));
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)
                ?.FullName ?? throw new InvalidOperationException(
                    "Unity project root is unavailable.");
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static string ToAssetPath(string absolutePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)
                ?.FullName ?? throw new InvalidOperationException(
                    "Unity project root is unavailable.");
            return absolutePath.Substring(projectRoot.Length + 1)
                .Replace('\\', '/');
        }

        private static string CellScene(string cellId)
        {
            return "Assets/Game/LegacyImport/RuntimeBaseline/Generated/" +
                   "World/Streaming/Scenes/Cells/World_Cell_" + cellId +
                   "_Legacy.unity";
        }

        private readonly struct MonoBehaviourRecord
        {
            public MonoBehaviourRecord(
                string scriptReference,
                string classIdentifier,
                string stableId)
            {
                ScriptReference = scriptReference;
                ClassIdentifier = classIdentifier;
                StableId = stableId;
            }

            public string ScriptReference { get; }
            public string ClassIdentifier { get; }
            public string StableId { get; }
        }

        private sealed class ProjectScanResult
        {
            public int InvalidScriptReferenceCount { get; set; }
            public int SupplementalClassIdentifierCount { get; set; }
            public int CalibrationClassIdentifierCount { get; set; }
            public List<string> InvalidScriptExamples { get; } =
                new List<string>();
            public HashSet<string> ScriptGuids { get; } =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public List<string> UnresolvedScriptGuids { get; } =
                new List<string>();
        }
    }
}
