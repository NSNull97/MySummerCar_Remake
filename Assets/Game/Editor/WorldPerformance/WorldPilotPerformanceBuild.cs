using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MSC.Development.Performance;
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
    /// Builds the bounded world-pilot capture player without changing the persistent Bootstrap scene.
    /// Every currently enabled scene is retained in its current order because the immutable streaming
    /// manifest addresses the two production cells by those enabled build indices.
    /// </summary>
    public static class WorldPilotPerformanceBuild
    {
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
                WorldPilotPerformanceBuildContext.Begin(configuration);
                var options = new BuildPlayerOptions
                {
                    scenes = configuration.EnabledScenePaths,
                    locationPathName = fullOutputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
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
                        "05B.1 performance probe was not injected exactly once into the build copy of Bootstrap.");
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
                WorldPilotGateRemediationValidator.Validate(logResult: false);
            if (!validation.Passed)
            {
                throw new InvalidOperationException(
                    "Cannot build the 05B.1 performance player because PilotGate remediation validation failed:\n- " +
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

            string[] enabledScenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (enabledScenePaths.Length == 0 ||
                !string.Equals(
                    enabledScenePaths[0],
                    ProductionWorldStreamingBuilder.BootstrapScenePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Bootstrap must remain enabled build index 0 for the 05B.1 performance player.");
            }

            if (enabledScenePaths.Distinct(StringComparer.Ordinal).Count() != enabledScenePaths.Length)
            {
                throw new InvalidOperationException(
                    "Enabled Build Settings contains duplicate scene paths; build indices are ambiguous.");
            }

            ValidateManifestAddress(pilotCell, enabledScenePaths);
            ValidateManifestAddress(nextCell, enabledScenePaths);

            GitProvenance provenance = GitProvenance.Capture();
            return new BuildConfiguration(
                enabledScenePaths,
                pilotCell.BuildIndex,
                nextCell.BuildIndex,
                provenance.Revision,
                provenance.WorkingTreeState,
                provenance.DirtyPaths,
                provenance.DirtyEntries,
                DateTime.UtcNow.ToString("O"));
        }

        private static void ValidateManifestAddress(
            ProductionWorldCellScene cell,
            IReadOnlyList<string> enabledScenePaths)
        {
            if (cell.BuildIndex < 0 || cell.BuildIndex >= enabledScenePaths.Count)
            {
                throw new InvalidOperationException(
                    $"Manifest cell {cell.CellId} build index {cell.BuildIndex} is outside the enabled scene list.");
            }

            if (!string.Equals(
                    enabledScenePaths[cell.BuildIndex],
                    cell.ScenePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Manifest cell {cell.CellId} expects build index {cell.BuildIndex} to be " +
                    $"{cell.ScenePath}, but Build Settings contains {enabledScenePaths[cell.BuildIndex]}.");
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

            Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            BootstrapInjectionCount = 0;
            Active = true;
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
    /// Adds the opt-in probe only to the transient build copy of Bootstrap.
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
                    "Persistent Bootstrap must not contain WorldPilotPerformanceProbe; it is injected into the build copy.");
            }

            WorldPilotPerformanceBuild.BuildConfiguration configuration =
                WorldPilotPerformanceBuildContext.Configuration;
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
