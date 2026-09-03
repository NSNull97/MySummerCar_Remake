using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using MSC.Editor.WorldBaseline;
using MSC.World.Partition;
using MSC.World.Streaming;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    /// <summary>
    /// Produces the exact three saved cells and freshness evidence consumed by
    /// the explicit activation acceptance benchmarks.
    /// </summary>
    public static class MapVegetationActivationBenchmarkPreparation
    {
        public const string GatePath =
            MapVegetationRebuildOptions.GeneratedRoot +
            "/Benchmark/ThreeCellActivationBenchmarkGate.json";
        public const string StreamingBaseScenePath =
            "Assets/Game/World/Debug/Streaming/" +
            "PrototypeWorldStreamingFixture.unity";
        private const string SchemaVersion =
            "msc.vegetation-activation-preparation.v1";
        private const string GeneratedDependencyFingerprintVersion =
            "msc.vegetation-generated-dependencies.v1";

        private static readonly WorldCellIndex[] BenchmarkCells =
        {
            new WorldCellIndex(0, -3),
            new WorldCellIndex(1, -3),
            new WorldCellIndex(2, -3)
        };

        private static readonly string[] AcceptanceEvidencePaths =
        {
            MapVegetationRebuild.Reports + "/pilot-gate.json",
            MapVegetationRebuild.Reports + "/latest-report.json",
            MapVegetationRebuild.Reports +
            "/VisualAudit/capture-report.json",
            MapVegetationBackdropPresentation.ReportPath
        };

        [MenuItem(
            "Tools/MSC Remake/Vegetation/Prepare Three-Cell Activation " +
            "Benchmark",
            priority = 1894)]
        public static void PrepareFromMenu() => PrepareBatch();

        public static void PrepareBenchmarkCells() => PrepareBatch();

        /// <summary>
        /// Batch prerequisite for both explicit PlayMode acceptance benchmarks.
        /// It regenerates only cell_0_-3, cell_1_-3 and cell_2_-3, enables their
        /// saved scenes, and seals the inputs/results in GatePath.
        /// </summary>
        public static void PrepareBatch() => ExecutePreservingEvidence(
            PrepareBatchCore, AcceptanceEvidencePaths);

        private static void PrepareBatchCore()
        {
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(GatePath) != null)
                AssetDatabase.DeleteAsset(GatePath);
            else if (File.Exists(ProjectPath(GatePath)))
                File.Delete(ProjectPath(GatePath));
            MapVegetationRebuildOptions options =
                MapVegetationRebuild.LoadOptions();
            if (options.Categories != MapVegetationCategories.All)
                throw new InvalidOperationException(
                    "The activation acceptance benchmark requires all four " +
                    "vegetation categories in each saved cell.");
            var serialized = new SerializedObject(options);
            SerializedProperty selected =
                serialized.FindProperty("selectedCell");
            string originalCell = selected.stringValue;
            try
            {
                foreach (WorldCellIndex cell in BenchmarkCells)
                {
                    serialized.Update();
                    selected.stringValue = cell.Id;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(options);
                    AssetDatabase.SaveAssets();
                    MapVegetationRebuild.Run(
                        options, false, false, false);
                }
            }
            finally
            {
                serialized.Update();
                selected.stringValue = originalCell;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(options);
                AssetDatabase.SaveAssets();
            }

            BenchmarkPreparationGate gate = BuildGate(options);
            string folder = Path.GetDirectoryName(GatePath)
                .Replace('\\', '/');
            EnsureFolder(folder);
            File.WriteAllText(
                ProjectPath(GatePath),
                JsonUtility.ToJson(gate, true),
                new UTF8Encoding(false));
            AssetDatabase.ImportAsset(
                GatePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            if (!gate.passed)
                throw new InvalidDataException(
                    "Three-cell activation benchmark preparation failed: " +
                    string.Join("; ", gate.errors.Take(12)));
            EnableRequiredBuildScenes(gate.cells.Select(cell =>
                cell.scenePath).Append(StreamingBaseScenePath));
            Debug.Log(
                "MAP_VEGETATION_ACTIVATION_BENCHMARK_READY cells=" +
                string.Join(",", BenchmarkCells.Select(cell => cell.Id)) +
                " gate=" + GatePath);
        }

        /// <summary>
        /// Benchmark preparation intentionally rebuilds three non-pilot cells.
        /// Those rebuilds use the normal generation pipeline and therefore
        /// write its latest/pilot reports. Preserve the independently accepted
        /// pilot and capture evidence byte-for-byte so benchmark setup cannot
        /// silently replace the full-map generation gate.
        /// </summary>
        internal static void ExecutePreservingEvidence(
            Action operation,
            IReadOnlyList<string> paths)
        {
            if (operation == null)
                throw new ArgumentNullException(nameof(operation));
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));

            EvidenceSnapshot[] snapshots = paths
                .Select(CaptureEvidence)
                .ToArray();
            Exception operationFailure = null;
            try
            {
                operation();
            }
            catch (Exception exception)
            {
                operationFailure = exception;
            }

            List<Exception> restoreFailures = null;
            foreach (EvidenceSnapshot snapshot in snapshots)
            {
                try
                {
                    RestoreEvidence(snapshot);
                }
                catch (Exception exception)
                {
                    restoreFailures ??= new List<Exception>();
                    restoreFailures.Add(exception);
                }
            }

            if (operationFailure != null && restoreFailures != null)
            {
                var failures = new List<Exception>(
                    restoreFailures.Count + 1) { operationFailure };
                failures.AddRange(restoreFailures);
                throw new AggregateException(
                    "Benchmark preparation and acceptance-evidence restore " +
                    "both failed.", failures);
            }
            if (operationFailure != null)
                ExceptionDispatchInfo.Capture(operationFailure).Throw();
            if (restoreFailures == null) return;
            if (restoreFailures.Count == 1)
                ExceptionDispatchInfo.Capture(restoreFailures[0]).Throw();
            throw new AggregateException(
                "Multiple acceptance-evidence files could not be restored.",
                restoreFailures);
        }

        private static EvidenceSnapshot CaptureEvidence(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException(
                    "Acceptance evidence path cannot be empty.",
                    nameof(path));
            string fullPath = ProjectPath(path);
            bool exists = File.Exists(fullPath);
            return new EvidenceSnapshot(
                fullPath,
                exists,
                exists ? File.ReadAllBytes(fullPath) : null,
                exists ? File.GetLastWriteTimeUtc(fullPath) : default);
        }

        private static void RestoreEvidence(EvidenceSnapshot snapshot)
        {
            if (!snapshot.Existed)
            {
                if (File.Exists(snapshot.FullPath))
                    File.Delete(snapshot.FullPath);
                return;
            }

            string folder = Path.GetDirectoryName(snapshot.FullPath);
            if (!string.IsNullOrWhiteSpace(folder))
                Directory.CreateDirectory(folder);
            File.WriteAllBytes(snapshot.FullPath, snapshot.Bytes);
            File.SetLastWriteTimeUtc(snapshot.FullPath, snapshot.LastWriteUtc);
        }

        /// <summary>
        /// Reflection-safe Editor endpoint used by the PlayMode assembly. It
        /// deliberately avoids opening source scenes while the player loop runs.
        /// The dependency fingerprint covers every configured source scene and
        /// all of its asset dependencies.
        /// </summary>
        public static string CaptureCurrentPrerequisitesJson()
        {
            MapVegetationRebuildOptions options = AssetDatabase
                .LoadAssetAtPath<MapVegetationRebuildOptions>(
                    MapVegetationRebuildOptions.AssetPath);
            if (options == null)
                throw new InvalidDataException(
                    "Map vegetation rebuild options are missing.");
            ProductionWorldStreamingManifest manifest = AssetDatabase
                .LoadAssetAtPath<ProductionWorldStreamingManifest>(
                    WorldBaseline06B2Paths.ActiveManifest);
            if (manifest == null)
                throw new InvalidDataException(
                    "The active production streaming manifest is missing.");
            MapVegetationPackedWoodyBuilder.ClearCaches();
            var current = new CurrentPrerequisites
            {
                settingsHash = MapVegetationRebuild.SettingsHash(options),
                sourceDependencyFingerprint =
                    SourceDependencyFingerprint(options, manifest),
                projectPolicySignature = MapVegetationPackedWoodyBuilder
                    .ProjectPolicySignature()
            };
            foreach (WorldCellIndex cell in BenchmarkCells)
            {
                string scenePath = MapVegetationRebuild.CellScenePath(cell);
                GeneratedDependencySeal dependencies =
                    CaptureGeneratedDependencySeal(scenePath, cell.Id);
                if (dependencies.Errors.Count > 0)
                    throw new InvalidDataException(
                        "Current generated dependencies are invalid for " +
                        cell.Id + ": " +
                        string.Join("; ", dependencies.Errors.Take(12)));
                current.cells.Add(new PreparedCell
                {
                    cellId = cell.Id,
                    scenePath = scenePath,
                    generatedDependencyFingerprint =
                        dependencies.Fingerprint,
                    generatedDependencies = dependencies.Dependencies
                });
            }
            return JsonUtility.ToJson(current);
        }

        private static BenchmarkPreparationGate BuildGate(
            MapVegetationRebuildOptions options)
        {
            var gate = new BenchmarkPreparationGate
            {
                schemaVersion = SchemaVersion,
                utc = DateTime.UtcNow.ToString("O"),
                generatorId = MapVegetationRebuildOptions.GeneratorId,
                packedPresentationVersion =
                    MapVegetationPackedWoodyBuilder.PresentationVersion,
                settingsHash = MapVegetationRebuild.SettingsHash(options),
                projectPolicySignature = MapVegetationPackedWoodyBuilder
                    .ProjectPolicySignature()
            };
            using (var context = new MapVegetationContext(options))
            {
                gate.sourceFingerprint = context.SourceFingerprint;
                gate.sourceDependencyFingerprint =
                    SourceDependencyFingerprint(options, context.Manifest);
                foreach (WorldCellIndex cell in BenchmarkCells)
                {
                    MapVegetationCellPlan plan =
                        context.CellPlan(cell, true);
                    string scenePath =
                        MapVegetationRebuild.CellScenePath(cell);
                    var capture = new PreparedCell
                    {
                        cellId = cell.Id,
                        scenePath = scenePath,
                        planFingerprint = plan.Fingerprint
                    };
                    if (!File.Exists(ProjectPath(scenePath)))
                        gate.errors.Add(
                            "Missing regenerated benchmark scene: " +
                            scenePath);
                    else
                    {
                        capture.sceneSha256 = HashFile(scenePath);
                        GeneratedDependencySeal dependencies =
                            CaptureGeneratedDependencySeal(
                                scenePath, cell.Id);
                        capture.generatedDependencyFingerprint =
                            dependencies.Fingerprint;
                        capture.generatedDependencies =
                            dependencies.Dependencies;
                        foreach (string error in dependencies.Errors)
                            gate.errors.Add(
                                "Generated dependency " + cell.Id + ": " +
                                error);
                    }
                    if (string.IsNullOrWhiteSpace(plan.Fingerprint))
                        gate.errors.Add(
                            "Missing current plan fingerprint for " +
                            cell.Id);
                    if (plan.Grass.Count == 0 || plan.Woody.Count == 0)
                        gate.errors.Add(
                            "Benchmark cell has no current grass/woody " +
                            "population: " + cell.Id);
                    gate.cells.Add(capture);
                }
            }
            gate.passed = gate.errors.Count == 0 &&
                !string.IsNullOrWhiteSpace(gate.settingsHash) &&
                !string.IsNullOrWhiteSpace(gate.sourceFingerprint) &&
                !string.IsNullOrWhiteSpace(
                    gate.sourceDependencyFingerprint) &&
                !string.IsNullOrWhiteSpace(
                    gate.projectPolicySignature) &&
                gate.cells.Count == BenchmarkCells.Length;
            return gate;
        }

        private static GeneratedDependencySeal
            CaptureGeneratedDependencySeal(
                string scenePath,
                string cellId)
        {
            string generatedPrefix =
                MapVegetationRebuildOptions.GeneratedRoot + "/";
            string[] paths = AssetDatabase.GetDependencies(
                    scenePath, true)
                .Where(path => path.StartsWith(
                    generatedPrefix, StringComparison.Ordinal))
                .Where(path => !string.Equals(
                    path, scenePath, StringComparison.Ordinal))
                .ToArray();
            GeneratedDependencySeal seal =
                CaptureGeneratedDependencySeal(paths);
            foreach (string missing in
                     MissingRequiredGeneratedDependencyPaths(
                         cellId,
                         seal.Dependencies.Select(item => item.path)))
            {
                seal.Errors.Add(
                    "Required generated asset is not a transitive scene " +
                    "dependency: " + missing);
            }
            if (seal.Dependencies.Count == 0)
                seal.Errors.Add(
                    "No generated vegetation dependencies were discovered " +
                    "for " + scenePath + ".");
            return seal;
        }

        /// <summary>
        /// Seals exact generated asset bytes in a stable path order. Kept
        /// separate from AssetDatabase discovery so deterministic hashing and
        /// stale-file behavior can be covered by focused EditMode tests.
        /// </summary>
        internal static GeneratedDependencySeal
            CaptureGeneratedDependencySeal(
                IEnumerable<string> dependencyPaths)
        {
            if (dependencyPaths == null)
                throw new ArgumentNullException(nameof(dependencyPaths));
            var seal = new GeneratedDependencySeal();
            string[] paths = dependencyPaths
                .Select(path => (path ?? string.Empty).Replace('\\', '/'))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            foreach (string path in paths)
            {
                string fullPath = ProjectPath(path);
                if (!File.Exists(fullPath))
                {
                    seal.Errors.Add(
                        "Generated dependency file is missing: " + path);
                    continue;
                }
                seal.Dependencies.Add(new GeneratedDependency
                {
                    path = path,
                    bytes = new FileInfo(fullPath).Length,
                    sha256 = HashFile(path)
                });
            }
            seal.Fingerprint = GeneratedDependencyFingerprint(
                seal.Dependencies);
            return seal;
        }

        internal static IReadOnlyList<string>
            MissingRequiredGeneratedDependencyPaths(
                string cellId,
                IEnumerable<string> capturedPaths)
        {
            if (string.IsNullOrWhiteSpace(cellId))
                throw new ArgumentException(
                    "Benchmark cell ID cannot be empty.", nameof(cellId));
            if (capturedPaths == null)
                throw new ArgumentNullException(nameof(capturedPaths));
            var captured = new HashSet<string>(
                capturedPaths.Select(path =>
                    (path ?? string.Empty).Replace('\\', '/')),
                StringComparer.Ordinal);
            string dataRoot = MapVegetationRebuildOptions.GeneratedRoot +
                "/Data/" + cellId + "/";
            string[] required =
            {
                dataRoot + "GrassCell.asset",
                dataRoot + "Catalog.asset",
                dataRoot + "Density.asset",
                dataRoot + "PackedOriginalTrees.asset",
                dataRoot + "PackedBoundaryForest.asset",
                dataRoot + "PackedShrubsAndUndergrowth.asset"
            };
            return required.Where(path => !captured.Contains(path)).ToArray();
        }

        private static string GeneratedDependencyFingerprint(
            IReadOnlyList<GeneratedDependency> dependencies)
        {
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(
                       bytes, new UTF8Encoding(false), true))
            {
                writer.Write(GeneratedDependencyFingerprintVersion);
                writer.Write(dependencies.Count);
                foreach (GeneratedDependency dependency in dependencies)
                {
                    writer.Write(dependency.path ?? string.Empty);
                    writer.Write(dependency.bytes);
                    writer.Write(dependency.sha256 ?? string.Empty);
                }
            }
            using SHA256 sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes.ToArray()))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string SourceDependencyFingerprint(
            MapVegetationRebuildOptions options,
            ProductionWorldStreamingManifest manifest)
        {
            if (options == null || options.Placement == null)
                throw new ArgumentNullException(nameof(options));
            string global = string.IsNullOrWhiteSpace(
                    options.Placement.SourceGlobalScenePath)
                ? WorldBaseline06B2Paths.GlobalScene
                : options.Placement.SourceGlobalScenePath;
            IEnumerable<string> cells =
                options.Placement.SourceCellScenePaths.Count > 0
                    ? options.Placement.SourceCellScenePaths
                    : manifest.Cells.Select(cell => cell.ScenePath);
            string[] paths = new[] { global }.Concat(cells)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (paths.Length == 0)
                throw new InvalidDataException(
                    "No vegetation source scenes are configured.");
            using SHA256 sha = SHA256.Create();
            using var stream = new CryptoStream(
                Stream.Null, sha, CryptoStreamMode.Write);
            using (var writer = new StreamWriter(
                       stream, new UTF8Encoding(false), 4096, true))
            {
                writer.WriteLine("msc.vegetation-source-dependencies.v1");
                foreach (string path in paths)
                {
                    if (!File.Exists(ProjectPath(path)))
                        throw new FileNotFoundException(
                            "Configured vegetation source scene is missing.",
                            path);
                    writer.Write(path);
                    writer.Write('|');
                    writer.WriteLine(
                        AssetDatabase.GetAssetDependencyHash(path).ToString());
                }
            }
            stream.FlushFinalBlock();
            return BitConverter.ToString(sha.Hash)
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static void EnableRequiredBuildScenes(
            IEnumerable<string> requiredPaths)
        {
            var scenes = new List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);
            foreach (string path in requiredPaths.Distinct(
                         StringComparer.Ordinal))
            {
                if (!File.Exists(ProjectPath(path)))
                    throw new FileNotFoundException(
                        "Required benchmark scene is missing.", path);
                int index = scenes.FindIndex(scene =>
                    string.Equals(scene.path, path,
                        StringComparison.Ordinal));
                if (index < 0)
                    scenes.Add(new EditorBuildSettingsScene(path, true));
                else if (!scenes[index].enabled)
                    scenes[index] = new EditorBuildSettingsScene(path, true);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static string HashFile(string path)
        {
            using SHA256 sha = SHA256.Create();
            using FileStream stream = File.OpenRead(ProjectPath(path));
            return BitConverter.ToString(sha.ComputeHash(stream))
                .Replace("-", string.Empty).ToLowerInvariant();
        }

        private static string ProjectPath(string path) =>
            Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", path));

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [Serializable]
        private sealed class CurrentPrerequisites
        {
            public string settingsHash, sourceDependencyFingerprint,
                projectPolicySignature;
            public List<PreparedCell> cells = new List<PreparedCell>();
        }

        [Serializable]
        private sealed class PreparedCell
        {
            public string cellId, scenePath, sceneSha256, planFingerprint,
                generatedDependencyFingerprint;
            public List<GeneratedDependency> generatedDependencies =
                new List<GeneratedDependency>();
        }

        [Serializable]
        internal sealed class GeneratedDependency
        {
            public string path, sha256;
            public long bytes;
        }

        internal sealed class GeneratedDependencySeal
        {
            public string Fingerprint;
            public List<GeneratedDependency> Dependencies =
                new List<GeneratedDependency>();
            public List<string> Errors = new List<string>();
        }

        [Serializable]
        private sealed class BenchmarkPreparationGate
        {
            public string schemaVersion, utc, generatorId,
                packedPresentationVersion, settingsHash, sourceFingerprint,
                sourceDependencyFingerprint, projectPolicySignature;
            public bool passed;
            public List<PreparedCell> cells = new List<PreparedCell>();
            public List<string> errors = new List<string>();
        }

        private readonly struct EvidenceSnapshot
        {
            public readonly string FullPath;
            public readonly bool Existed;
            public readonly byte[] Bytes;
            public readonly DateTime LastWriteUtc;

            public EvidenceSnapshot(
                string fullPath,
                bool existed,
                byte[] bytes,
                DateTime lastWriteUtc)
            {
                FullPath = fullPath;
                Existed = existed;
                Bytes = bytes;
                LastWriteUtc = lastWriteUtc;
            }
        }
    }
}
