using System;
using System.IO;
using MSC.Editor.WorldBaseline;
using MSC.UI.EditorTools;
using MSC.UI.Presentation;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.UI.StandaloneValidation.EditorTools
{
    public static class MainMenuStandaloneBuild
    {
        public static void BuildPrivateDevelopment()
        {
            if (!Application.isBatchMode)
                throw new InvalidOperationException("Use a coordinated batch Editor after the active Editor/test run is closed.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Cannot build a fixture during Play mode or compilation.");
            for (int index = 0; index < SceneManager.sceneCount; index++)
                if (SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("Cannot replace an unsaved Editor scene setup.");

            string evidenceRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Artifacts/MainMenuRedesign/Standalone"));
            string executable = Path.Combine(evidenceRoot, "Development", "MainMenuUiFixture.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(executable));
            string scenePath = "Assets/Game/UI/Validation/MainMenuStandaloneBuild_" + Guid.NewGuid().ToString("N") + ".unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            bool replaceCleanUntitled = SceneManager.sceneCount == 1 &&
                string.IsNullOrEmpty(previousActiveScene.path) && !previousActiveScene.isDirty;
            string previousPrivateFlag = Environment.GetEnvironmentVariable(DonorRuntimeBaselineBuildGuard.PrivateBuildEnvironmentVariable);
            Scene temporaryScene = default;
            var evidence = new Evidence
            {
                fixture = "UI-only scene using actual GameUiRoot and canonical authored assets; no production world is loaded.",
                executable = executable,
                releaseBuild = "Blocked: global donor RuntimeBaseline Resources would be included even with a UI-only scene. No guard is bypassed and no assets are moved.",
                resourcesDonorAssets = CountDonorResources()
            };
            evidence.initialScenePath = previousActiveScene.path;
            evidence.initialSceneRoots = Array.ConvertAll(previousActiveScene.GetRootGameObjects(), value => value.name);
            try
            {
                // Unity forbids additive creation beside its clean, unsaved batch
                // startup scene. Only that single untitled scene may be replaced.
                // Named scene setups keep the existing additive/preserve path.
                temporaryScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                    replaceCleanUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
                SceneManager.SetActiveScene(temporaryScene);
                GameObject root = new GameObject("Main menu standalone validation fixture");
                MainMenuStandaloneProbe probe = root.AddComponent<MainMenuStandaloneProbe>();
                probe.Configure(
                    null,
                    Require<Texture2D>(ProductionUiAuthoring.MainMenuLogoPath),
                    Require<Shader>(ProductionUiAuthoring.UiBlurShaderPath),
                    Require<InputActionAsset>(ProductionUiAuthoring.PlayerActionsPath),
                    Require<InputActionAsset>(ProductionUiAuthoring.VehicleActionsPath),
                    Require<MainMenuVehicleModel>(MainMenuVehicleAuthoring.PrefabPath),
                    null,
                    Require<MainMenuEnvironmentModel>(MainMenuEnvironmentAuthoring.PrefabPath));
                GameObject cameraObject = new GameObject("UI-only HDRP camera", typeof(Camera), typeof(HDAdditionalCameraData));
                cameraObject.transform.SetParent(root.transform, false);
                Camera camera = cameraObject.GetComponent<Camera>();
                camera.cullingMask = 0;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                cameraObject.tag = "MainCamera";
                if (!EditorSceneManager.SaveScene(temporaryScene, scenePath))
                    throw new InvalidOperationException("Could not save the temporary UI fixture scene.");

                string[] scenePaths = { scenePath };
                // BuildPlayerOptions and the explicit guard scope already own this
                // build's scene list. Never replace the project's streaming list:
                // a terminated build cannot execute a finally-based restoration.
                Environment.SetEnvironmentVariable(DonorRuntimeBaselineBuildGuard.PrivateBuildEnvironmentVariable, "1");
                using (DonorRuntimeBaselineBuildGuard.BeginExplicitSceneBuild(scenePaths))
                {
                    BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = scenePaths,
                        locationPathName = executable,
                        target = BuildTarget.StandaloneWindows64,
                        options = BuildOptions.Development | BuildOptions.CompressWithLz4
                    });
                    evidence.result = report.summary.result.ToString();
                    evidence.bytes = report.summary.totalSize;
                    evidence.durationSeconds = report.summary.totalTime.TotalSeconds;
                    evidence.errors = report.summary.totalErrors;
                    evidence.warnings = report.summary.totalWarnings;
                    if (report.summary.result != BuildResult.Succeeded)
                        throw new InvalidOperationException("Private UI fixture build failed: " + report.summary.result);
                }
            }
            catch (Exception exception)
            {
                evidence.failure = exception.ToString();
                throw;
            }
            finally
            {
                Environment.SetEnvironmentVariable(DonorRuntimeBaselineBuildGuard.PrivateBuildEnvironmentVariable, previousPrivateFlag);
                if (temporaryScene.IsValid() && temporaryScene.isLoaded)
                {
                    if (replaceCleanUntitled)
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    else
                        EditorSceneManager.CloseScene(temporaryScene, true);
                }
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                    SceneManager.SetActiveScene(previousActiveScene);
                // This GUID-named file was created exclusively by this call.
                // No existing project scene is saved, moved, or deleted.
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null)
                    AssetDatabase.DeleteAsset(scenePath);
                evidence.timestampUtc = DateTime.UtcNow.ToString("O");
                evidence.unityVersion = Application.unityVersion;
                File.WriteAllText(Path.Combine(evidenceRoot, "development-build-result.json"), JsonUtility.ToJson(evidence, true));
            }
            Debug.Log("MSC_MAIN_MENU_PRIVATE_DEVELOPMENT_BUILD_OK " + executable);
        }

        private static T Require<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Required canonical UI asset is missing: " + path);
            return asset;
        }

        private static int CountDonorResources()
        {
            int count = 0;
            string[] paths = AssetDatabase.GetAllAssetPaths();
            foreach (string path in paths)
                if (path.StartsWith("Assets/Game/LegacyImport/RuntimeBaseline/", StringComparison.Ordinal) &&
                    path.Contains("/Resources/") && !AssetDatabase.IsValidFolder(path)) count++;
            return count;
        }

        [Serializable] private sealed class Evidence
        {
            public string fixture, executable, result, releaseBuild, failure, timestampUtc, unityVersion, initialScenePath;
            public string[] initialSceneRoots;
            public int resourcesDonorAssets, errors, warnings;
            public ulong bytes;
            public double durationSeconds;
        }
    }
}
