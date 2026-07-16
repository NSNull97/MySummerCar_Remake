using System;
using System.Collections.Generic;
using System.IO;
using MSC.Editor.WorldBaseline;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MSC.Editor.GaragePrototype
{
    public static class GaragePrototypePerformanceBuild
    {
        [MenuItem("Tools/My Summer Car/Milestone 3/Build 1080p Performance Player")]
        public static void BuildPlayer()
        {
            IReadOnlyList<string> validationErrors = GaragePrototypeValidator.Validate();
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Cannot build Milestone 3 performance player:\n- " +
                    string.Join("\n- ", validationErrors));
            }

            string fullOutputPath = Path.GetFullPath(GaragePrototypePaths.PerformanceBuildPath);
            string directory = Path.GetDirectoryName(fullOutputPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Performance build path has no parent directory.");
            }

            Directory.CreateDirectory(directory);
            bool previousFrameTimingState = PlayerSettings.enableFrameTimingStats;
            try
            {
                PlayerSettings.enableFrameTimingStats = true;
                string[] buildScenes =
                {
                    GaragePrototypePaths.ProductionScene
                };
                using IDisposable buildGuardScope =
                    DonorRuntimeBaselineBuildGuard
                        .BeginExplicitSceneBuild(buildScenes);
                var options = new BuildPlayerOptions
                {
                    scenes = buildScenes,
                    locationPathName = fullOutputPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development
                };
                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        "Milestone 3 performance player build failed: " + report.summary.result);
                }

                Debug.Log(
                    $"M3_PERFORMANCE_BUILD_OK bytes={report.summary.totalSize} " +
                    $"duration={report.summary.totalTime} path={fullOutputPath}");
            }
            finally
            {
                PlayerSettings.enableFrameTimingStats = previousFrameTimingState;
            }
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Milestone 3 performance build requires batch mode.");
            }

            BuildPlayer();
        }
    }
}
