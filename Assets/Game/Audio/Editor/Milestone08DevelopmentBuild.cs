using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MSC.Audio.Editor
{
    /// <summary>
    /// Reproducible private Windows Development build used by the M08 audio
    /// gate. The existing donor-baseline guard remains authoritative and still
    /// requires MSC_PRIVATE_DONOR_BASELINE_BUILD=1 in the Unity process.
    /// </summary>
    public static class Milestone08DevelopmentBuild
    {
        private const string PrivateBuildEnvironmentVariable =
            "MSC_PRIVATE_DONOR_BASELINE_BUILD";
        private const string OutputEnvironmentVariable = "MSC_M08_BUILD_PATH";

        [MenuItem("Tools/MSC Remake/Audio/Build Private Windows Development Player")]
        public static void BuildFromMenu() => BuildFromCommandLine();

        public static void BuildFromCommandLine()
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable(PrivateBuildEnvironmentVariable),
                    "1",
                    StringComparison.Ordinal))
            {
                throw new BuildFailedException(
                    $"Set {PrivateBuildEnvironmentVariable}=1 for the private M08 build.");
            }

            string outputPath = Environment.GetEnvironmentVariable(
                OutputEnvironmentVariable);
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                outputPath = Path.Combine(
                    projectRoot,
                    "Builds",
                    "Milestone08",
                    "MySummerCar_Remake_M08.exe");
            }

            outputPath = Path.GetFullPath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            string[] scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                throw new BuildFailedException("No enabled scenes are configured for the M08 build.");
            }

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"M08 Windows Development build failed: {report.summary.result}.");
            }

            Debug.Log(
                $"M08 private Windows Development build succeeded: {outputPath}; " +
                $"size={report.summary.totalSize} bytes; " +
                $"duration={report.summary.totalTime}.");
        }
    }
}
