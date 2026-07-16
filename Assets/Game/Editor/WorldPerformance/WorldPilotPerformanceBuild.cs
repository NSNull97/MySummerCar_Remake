using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Development.Performance;
using MSC.Editor.WorldBaseline;
using MSC.Editor.WorldStreaming;
using MSC.World.Streaming;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace MSC.Editor.WorldPerformance
{
    public static class WorldPilotPerformancePaths
    {
        public const string PerformanceBuildPath =
            "Builds/Milestone05B1/MySummerCar_Remake_M05B1_Performance.exe";
        public const string DefaultCapturePath =
            "PerformanceCaptures/Milestone05B1/WorldPilot_1080p.json";
    }

    /// <summary>
    /// Builds the bounded world-pilot capture player without changing the
    /// persistent Bootstrap scene or depending on the project's active build
    /// profile. The build contains only the 05B.1 fixture and its two cells.
    /// </summary>
    public static class WorldPilotPerformanceBuild
    {
        private const int FixtureBuildIndex = 0;
        private const int PilotBuildIndex = 1;
        private const int NextBuildIndex = 2;
        private const string DonorRuntimeBaselineRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/";

        [MenuItem("Tools/MSC Remake/World Validation/Build 05B.1 Performance Player")]
        public static void BuildPlayer()
        {
            BuildConfiguration configuration = ValidateAndCreateConfiguration();
            string fullOutputPath = Path.GetFullPath(WorldPilotPerformancePaths.PerformanceBuildPath);
            string directory = Path.GetDirectoryName(fullOutputPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("05B.1 performance build path has no parent directory.");
            }

            Directory.CreateDirectory(directory);
            bool previousFrameTimingState = PlayerSettings.enableFrameTimingStats;
            try
            {
                PlayerSettings.enableFrameTimingStats = true;
                using IDisposable buildGuardScope =
                    DonorRuntimeBaselineBuildGuard
                        .BeginExplicitSceneBuild(
                            configuration.EnabledScenePaths);
                WorldPilotPerformanceBuildContext.Begin(configuration);
                var options = new BuildPlayerOptions
                {
                    scenes = configuration.EnabledScenePaths,
                    locationPathName = fullOutputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options =
                        BuildOptions.Development |
                        BuildOptions.CleanBuildCache
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "05B.1 performance player build failed: " + report.summary.result);
                }

                if (WorldPilotPerformanceBuildContext.BootstrapInjectionCount != 1)
                {
                    throw new InvalidOperationException(
                        "05B.1 performance probe was not injected exactly once into the build copy of the prototype fixture.");
                }

                Debug.Log(
                    $"M05B1_PERFORMANCE_BUILD_OK bytes={report.summary.totalSize} " +
                    $"duration={report.summary.totalTime} scenes={configuration.EnabledScenePaths.Length} " +
                    $"pilotIndex={configuration.PilotBuildIndex} nextIndex={configuration.NextBuildIndex} " +
                    $"path={fullOutputPath}");
            }
            finally
            {
                WorldPilotPerformanceBuildContext.End();
                PlayerSettings.enableFrameTimingStats = previousFrameTimingState;
            }
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("05B.1 performance build batch entry requires batch mode.");
            }

            BuildPlayer();
        }

        private static BuildConfiguration ValidateAndCreateConfiguration()
        {
            WorldPilotGateRemediationValidationResult validation =
                WorldPilotGateRemediationValidator.Validate(
                    logResult: false);
            if (!validation.Passed)
            {
                throw new InvalidOperationException(
                    "Cannot build the 05B.1 performance player because " +
                    "PilotGate remediation validation failed:\n- " +
                    string.Join("\n- ", validation.Errors));
            }

            ProductionWorldStreamingManifest manifest =
                AssetDatabase.LoadAssetAtPath<ProductionWorldStreamingManifest>(
                    ProductionWorldStreamingBuilder.ManifestAssetPath);
            if (manifest == null)
            {
                throw new InvalidOperationException(
                    "Production streaming manifest is missing: " +
                    ProductionWorldStreamingBuilder.ManifestAssetPath);
            }

            IReadOnlyList<string> manifestErrors = manifest.ValidateConfiguration();
            if (manifestErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Production streaming manifest is invalid:\n- " +
                    string.Join("\n- ", manifestErrors));
            }

            if (manifest.ProfileKind !=
                    ProductionWorldProfileKind.PrototypeFixture ||
                manifest.PrivateLocalRuntimeBaseline ||
                manifest.GlobalScenes.Count != 0)
            {
                throw new InvalidOperationException(
                    "05B.1 performance capture requires the isolated " +
                    "prototype fixture profile without donor global scenes.");
            }

            if (manifest.Cells.Count != 2 ||
                !manifest.TryGetCell(
                    ProductionWorldStreamingBuilder.PilotCellId,
                    out ProductionWorldCellScene pilotCell) ||
                !manifest.TryGetCell(
                    ProductionWorldStreamingBuilder.NextCellId,
                    out ProductionWorldCellScene nextCell))
            {
                throw new InvalidOperationException(
                    "05B.1 performance capture requires the exact accepted two-cell production manifest.");
            }

            ValidateManifestCell(
                pilotCell,
                ProductionWorldStreamingBuilder.PilotCellId,
                0,
                -3,
                ProductionWorldStreamingBuilder.PilotCellScenePath);
            ValidateManifestCell(
                nextCell,
                ProductionWorldStreamingBuilder.NextCellId,
                0,
                -2,
                ProductionWorldStreamingBuilder.NextCellScenePath);

            string[] boundedScenePaths =
            {
                ProductionWorldStreamingBuilder.BootstrapScenePath,
                ProductionWorldStreamingBuilder.PilotCellScenePath,
                ProductionWorldStreamingBuilder.NextCellScenePath
            };
            ValidateBoundedSceneList(boundedScenePaths);

            GitProvenance provenance = GitProvenance.Capture();
            return new BuildConfiguration(
                boundedScenePaths,
                PilotBuildIndex,
                NextBuildIndex,
                provenance.Revision,
                provenance.WorkingTreeState,
                provenance.DirtyPaths,
                provenance.DirtyEntries,
                DateTime.UtcNow.ToString("O"));
        }

        private static void ValidateManifestCell(
            ProductionWorldCellScene cell,
            string expectedId,
            int expectedX,
            int expectedZ,
            string expectedPath)
        {
            if (!string.Equals(
                    cell.CellId,
                    expectedId,
                    StringComparison.Ordinal) ||
                cell.Index.X != expectedX ||
                cell.Index.Z != expectedZ ||
                !string.Equals(
                    cell.ScenePath,
                    expectedPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Manifest cell {expectedId} does not match its accepted " +
                    "ID, coordinates, and scene path.");
            }
        }

        private static void ValidateBoundedSceneList(
            IReadOnlyList<string> scenePaths)
        {
            if (scenePaths.Count != 3 ||
                !string.Equals(
                    scenePaths[FixtureBuildIndex],
                    ProductionWorldStreamingBuilder.BootstrapScenePath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    scenePaths[PilotBuildIndex],
                    ProductionWorldStreamingBuilder.PilotCellScenePath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    scenePaths[NextBuildIndex],
                    ProductionWorldStreamingBuilder.NextCellScenePath,
                    StringComparison.Ordinal) ||
                scenePaths.Distinct(StringComparer.Ordinal).Count() !=
                    scenePaths.Count)
            {
                throw new InvalidOperationException(
                    "05B.1 performance build scene order must be exactly " +
                    "fixture, pilot cell, next cell.");
            }

            for (int index = 0; index < scenePaths.Count; index++)
            {
                string scenePath = scenePaths[index];
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                        scenePath) == null)
                {
                    throw new InvalidOperationException(
                        "Required 05B.1 performance scene is missing: " +
                        scenePath);
                }

                string[] dependencies = AssetDatabase.GetDependencies(
                    scenePath,
                    recursive: true);
                for (int dependencyIndex = 0;
                     dependencyIndex < dependencies.Length;
                     dependencyIndex++)
                {
                    string dependency = dependencies[dependencyIndex]
                        .Replace('\\', '/');
                    if (dependency.StartsWith(
                            DonorRuntimeBaselineRoot,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Bounded 05B.1 performance scene depends on the " +
                            "donor RuntimeBaseline: " + scenePath + " -> " +
                            dependency);
                    }
                }
            }
        }

        internal sealed class BuildConfiguration
        {
            public BuildConfiguration(
                string[] enabledScenePaths,
                int pilotBuildIndex,
                int nextBuildIndex,
                string revision,
                string workingTreeState,
                string[] dirtyPaths,
                WorldPilotSourceDirtyEntry[] dirtyEntries,
                string buildUtc)
            {
                EnabledScenePaths = enabledScenePaths;
                PilotBuildIndex = pilotBuildIndex;
                NextBuildIndex = nextBuildIndex;
                Revision = revision;
                WorkingTreeState = workingTreeState;
                DirtyPaths = dirtyPaths;
                DirtyEntries = dirtyEntries;
                BuildUtc = buildUtc;
            }

            public string[] EnabledScenePaths { get; }
            public int PilotBuildIndex { get; }
            public int NextBuildIndex { get; }
            public string Revision { get; }
            public string WorkingTreeState { get; }
            public string[] DirtyPaths { get; }
            public WorldPilotSourceDirtyEntry[] DirtyEntries { get; }
            public string BuildUtc { get; }
        }

        private sealed class GitProvenance
        {
            private GitProvenance(
                string revision,
                string workingTreeState,
                string[] dirtyPaths,
                WorldPilotSourceDirtyEntry[] dirtyEntries)
            {
                Revision = revision;
                WorkingTreeState = workingTreeState;
                DirtyPaths = dirtyPaths;
                DirtyEntries = dirtyEntries;
            }

            public string Revision { get; }
            public string WorkingTreeState { get; }
            public string[] DirtyPaths { get; }
            public WorldPilotSourceDirtyEntry[] DirtyEntries { get; }

            public static GitProvenance Capture()
            {
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                if (!TryRunGit(projectRoot, "rev-parse HEAD", out string revision) ||
                    !TryRunGit(
                        projectRoot,
                        "-c core.quotePath=false status --porcelain=v1 -z --untracked-files=all",
                        out string status))
                {
                    throw new InvalidOperationException(
                        "Git provenance is unavailable; 05B.1 capture refuses to omit dirty-file status and hashes.");
                }

                WorldPilotSourceDirtyEntry[] dirtyEntries = ParseDirtyEntries(projectRoot, status)
                    .OrderBy(entry => entry.Path, StringComparer.Ordinal)
                    .ThenBy(entry => entry.PorcelainStatus, StringComparer.Ordinal)
                    .ToArray();
                string[] dirtyPaths = dirtyEntries.Select(entry => entry.Path).ToArray();
                return new GitProvenance(
                    revision.Trim(),
                    dirtyPaths.Length == 0 ? "clean" : "dirty",
                    dirtyPaths,
                    dirtyEntries);
            }

            private static IEnumerable<WorldPilotSourceDirtyEntry> ParseDirtyEntries(
                string projectRoot,
                string porcelainOutput)
            {
                string[] fields = porcelainOutput.Split('\0');
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    string row = fields[fieldIndex];
                    if (string.IsNullOrEmpty(row))
                    {
                        continue;
                    }

                    if (row.Length < 4 || row[2] != ' ')
                    {
                        throw new InvalidOperationException(
                            "Git returned an invalid NUL-delimited porcelain status row.");
                    }

                    string status = row.Substring(0, 2);
                    string path = row.Substring(3).Replace('\\', '/');
                    bool renamedOrCopied = status.IndexOf('R') >= 0 || status.IndexOf('C') >= 0;
                    if (renamedOrCopied)
                    {
                        // With porcelain v1 -z, the current destination is in this row and the
                        // following NUL field contains the original path. Only the current bytes
                        // belong in the capture provenance.
                        fieldIndex++;
                        if (fieldIndex >= fields.Length || string.IsNullOrEmpty(fields[fieldIndex]))
                        {
                            throw new InvalidOperationException(
                                "Git returned an incomplete rename/copy porcelain status row.");
                        }
                    }

                    yield return CreateDirtyEntry(projectRoot, status, path);
                }
            }

            private static WorldPilotSourceDirtyEntry CreateDirtyEntry(
                string projectRoot,
                string status,
                string path)
            {
                if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
                {
                    throw new InvalidOperationException(
                        "Git returned a non-relative dirty path: " + path);
                }

                bool deleted = status.IndexOf('D') >= 0;
                string hash = deleted ? "missing" : ComputeCurrentFileSha256(projectRoot, path);
                return new WorldPilotSourceDirtyEntry(status, path, hash);
            }

            private static string ComputeCurrentFileSha256(string projectRoot, string relativePath)
            {
                string fullRoot = Path.GetFullPath(projectRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                string fullPath = Path.GetFullPath(
                    Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
                if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Dirty provenance path escapes the repository root: " + relativePath);
                }

                if (!File.Exists(fullPath))
                {
                    return "missing";
                }

                using (FileStream stream = new FileStream(
                           fullPath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.ReadWrite | FileShare.Delete))
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    var builder = new StringBuilder(hash.Length * 2);
                    for (int index = 0; index < hash.Length; index++)
                    {
                        builder.Append(hash[index].ToString("x2"));
                    }

                    return builder.ToString();
                }
            }

            private static bool TryRunGit(
                string workingDirectory,
                string arguments,
                out string output)
            {
                output = string.Empty;
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = arguments,
                        WorkingDirectory = workingDirectory,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using (Process process = Process.Start(startInfo))
                    {
                        if (process == null)
                        {
                            return false;
                        }

                        output = process.StandardOutput.ReadToEnd();
                        process.StandardError.ReadToEnd();
                        return process.WaitForExit(5000) && process.ExitCode == 0;
                    }
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }
    }

    /// <summary>
    /// Temporary build-pipeline context used only while BuildPipeline.BuildPlayer is processing scenes.
    /// </summary>
    internal static class WorldPilotPerformanceBuildContext
    {
        public static bool Active { get; private set; }
        public static int BootstrapInjectionCount { get; private set; }
        public static WorldPilotPerformanceBuild.BuildConfiguration Configuration { get; private set; }

        public static void Begin(WorldPilotPerformanceBuild.BuildConfiguration configuration)
        {
            if (Active)
            {
                throw new InvalidOperationException("A 05B.1 performance build context is already active.");
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            Configuration = configuration;
            BootstrapInjectionCount = 0;
            Active = true;
        }

        public static ProductionWorldStreamingManifest
            CreateBuildManifest()
        {
            if (!Active || Configuration == null)
            {
                throw new InvalidOperationException(
                    "05B.1 performance build context is not active.");
            }

            ProductionWorldStreamingManifest buildManifest =
                ScriptableObject.CreateInstance<
                    ProductionWorldStreamingManifest>();
            buildManifest.name =
                "M05B1_PerformanceStreamingManifest__BUILD_ONLY";
            buildManifest.ConfigureForAuthoring(
                ProductionWorldStreamingBuilder.CellSizeMeters,
                ProductionWorldStreamingBuilder.LoadingRadiusCells,
                ProductionWorldStreamingBuilder.UnloadingRadiusCells,
                new[]
                {
                    new ProductionWorldCellScene(
                        ProductionWorldStreamingBuilder.PilotCellId,
                        0,
                        -3,
                        Configuration.PilotBuildIndex,
                        ProductionWorldStreamingBuilder
                            .PilotCellScenePath),
                    new ProductionWorldCellScene(
                        ProductionWorldStreamingBuilder.NextCellId,
                        0,
                        -2,
                        Configuration.NextBuildIndex,
                        ProductionWorldStreamingBuilder
                            .NextCellScenePath)
                });

            IReadOnlyList<string> manifestErrors =
                buildManifest.ValidateConfiguration();
            if (manifestErrors.Count > 0)
            {
                UnityEngine.Object.DestroyImmediate(buildManifest);
                throw new InvalidOperationException(
                    "Build-only 05B.1 streaming manifest is invalid:\n- " +
                    string.Join("\n- ", manifestErrors));
            }

            return buildManifest;
        }

        public static void RegisterBootstrapInjection()
        {
            BootstrapInjectionCount++;
        }

        public static void End()
        {
            Active = false;
            BootstrapInjectionCount = 0;
            Configuration = null;
        }
    }

    /// <summary>
    /// Adds the opt-in probe and local-index manifest only to the transient
    /// build copy of the 05B.1 fixture.
    /// No profiling component is saved into production scenes.
    /// </summary>
    public sealed class WorldPilotPerformanceSceneProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 1000;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (!WorldPilotPerformanceBuildContext.Active ||
                !string.Equals(
                    scene.path,
                    ProductionWorldStreamingBuilder.BootstrapScenePath,
                    StringComparison.Ordinal))
            {
                return;
            }

            WorldPilotPerformanceProbe[] existing = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<WorldPilotPerformanceProbe>(includeInactive: true))
                .ToArray();
            if (existing.Length != 0)
            {
                throw new BuildFailedException(
                    "Persistent 05B.1 fixture must not contain " +
                    "WorldPilotPerformanceProbe; it is injected into the " +
                    "build copy.");
            }

            WorldPilotPerformanceBuild.BuildConfiguration configuration =
                WorldPilotPerformanceBuildContext.Configuration;
            ProductionWorldStreamingService[] streamingServices =
                scene.GetRootGameObjects()
                    .SelectMany(
                        root => root.GetComponentsInChildren<
                            ProductionWorldStreamingService>(
                            includeInactive: true))
                    .ToArray();
            if (streamingServices.Length != 1)
            {
                throw new BuildFailedException(
                    "05B.1 fixture build copy requires exactly one " +
                    "streaming service; found " +
                    streamingServices.Length + ".");
            }

            ProductionWorldStreamingManifest buildManifest =
                WorldPilotPerformanceBuildContext
                    .CreateBuildManifest();
            streamingServices[0].ConfigureForAuthoring(
                buildManifest);
            var root = new GameObject("M05B1_PerformanceCapture__DEVELOPMENT_ONLY");
            SceneManager.MoveGameObjectToScene(root, scene);
            WorldPilotPerformanceProbe probe = root.AddComponent<WorldPilotPerformanceProbe>();
            probe.Configure(
                configuration.PilotBuildIndex,
                configuration.NextBuildIndex,
                configuration.Revision,
                configuration.WorkingTreeState,
                configuration.DirtyPaths,
                configuration.DirtyEntries,
                configuration.BuildUtc,
                configuration.EnabledScenePaths);
            WorldPilotPerformanceBuildContext.RegisterBootstrapInjection();
        }
    }
}
