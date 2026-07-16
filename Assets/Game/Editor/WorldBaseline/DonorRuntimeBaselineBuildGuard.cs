using System;
using System.Collections.Generic;
using System.Linq;
using MSC.World.Streaming;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldBaseline
{
    /// <summary>
    /// Generated donor RuntimeBaseline content is permitted only for an
    /// explicitly acknowledged private local Development build.
    /// </summary>
    public sealed class DonorRuntimeBaselineBuildGuard :
        IPreprocessBuildWithReport,
        IProcessSceneWithReport
    {
        private static string[] explicitBuildScenePaths;

        public const string PrivateBuildEnvironmentVariable =
            "MSC_PRIVATE_DONOR_BASELINE_BUILD";

        public int callbackOrder => -1000;

        public static IDisposable BeginExplicitSceneBuild(
            IReadOnlyList<string> scenePaths)
        {
            if (explicitBuildScenePaths != null)
            {
                throw new InvalidOperationException(
                    "An explicit build-scene scope is already active.");
            }
            if (scenePaths == null ||
                scenePaths.Count == 0 ||
                scenePaths.Any(string.IsNullOrWhiteSpace) ||
                scenePaths.Distinct(StringComparer.Ordinal).Count() !=
                    scenePaths.Count)
            {
                throw new ArgumentException(
                    "Explicit build scenes must be non-empty, valid, and " +
                    "unique.",
                    nameof(scenePaths));
            }

            explicitBuildScenePaths = scenePaths.ToArray();
            return new ExplicitBuildSceneScope();
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            string[] scenePaths =
                explicitBuildScenePaths ??
                EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray();
            string firstDonorDependency = scenePaths
                .FirstOrDefault(SceneDependsOnRuntimeBaseline);
            if (string.IsNullOrWhiteSpace(firstDonorDependency))
            {
                return;
            }

            RequirePrivateDevelopmentBuild(
                report,
                "Build scene list contains or depends on the donor " +
                "RuntimeBaseline. First match: '" +
                firstDonorDependency + "'.");
        }

        public void OnProcessScene(
            Scene scene,
            BuildReport report)
        {
            if (report == null)
            {
                return;
            }

            if (!SceneDependsOnRuntimeBaseline(scene.path))
            {
                return;
            }

            RequirePrivateDevelopmentBuild(
                report,
                "Build scene '" + scene.path + "' contains or depends on " +
                "the donor RuntimeBaseline.");
        }

        private static void RequirePrivateDevelopmentBuild(
            BuildReport report,
            string reason)
        {
            ProductionWorldStreamingManifest manifest =
                AssetDatabase.LoadAssetAtPath<
                    ProductionWorldStreamingManifest>(
                    WorldBaseline06B2Paths.ActiveManifest);
            bool donorProfileActive =
                manifest != null &&
                manifest.ProfileKind ==
                    ProductionWorldProfileKind.DonorFeatureParity;
            bool explicitlyPrivate = string.Equals(
                Environment.GetEnvironmentVariable(
                    PrivateBuildEnvironmentVariable),
                "1",
                StringComparison.Ordinal);
            bool development =
                (report.summary.options &
                 BuildOptions.Development) != 0;
            bool manifestAllowsPrivateBaseline =
                !donorProfileActive ||
                manifest.PrivateLocalRuntimeBaseline;
            if (!manifestAllowsPrivateBaseline ||
                !explicitlyPrivate ||
                !development)
            {
                throw new BuildFailedException(
                    reason + " Enabled donor RuntimeBaseline content or the " +
                    "active donor feature-parity profile may be " +
                    "built only as an explicitly acknowledged private local " +
                    "Development build. Set " +
                    PrivateBuildEnvironmentVariable +
                    "=1 for the current build process. Public/distributable " +
                    "builds are intentionally blocked.");
            }
        }

        private static bool SceneDependsOnRuntimeBaseline(
            string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath))
            {
                return false;
            }
            if (scenePath.StartsWith(
                    "Assets/Game/LegacyImport/RuntimeBaseline/",
                    StringComparison.Ordinal))
            {
                return true;
            }

            string[] dependencies = AssetDatabase.GetDependencies(
                scenePath,
                recursive: true);
            for (int index = 0; index < dependencies.Length; index++)
            {
                if (dependencies[index].StartsWith(
                        "Assets/Game/LegacyImport/RuntimeBaseline/",
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class ExplicitBuildSceneScope : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                explicitBuildScenePaths = null;
                disposed = true;
            }
        }
    }
}
