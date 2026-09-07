using System;
using System.Linq;
using MSC.Bootstrap;
using MSC.UI.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace MSC.UI.EditorTools
{
    public static class ProductionUiAuthoring
    {
        public const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        public const string PlayerActionsPath =
            "Assets/Game/Player/Content/Input/M4_Player.inputactions";
        public const string VehicleActionsPath =
            "Assets/Game/Vehicle/Content/Simulation/Input/M06_Vehicle.inputactions";
        public const string MainMenuBackdropPath =
            "Assets/Game/UI/Presentation/Content/MainMenu/M08A_TemporaryGarageBackdrop.png";
        public const string MainMenuLogoPath =
            "Assets/Game/UI/Presentation/Content/MainMenu/M08A_MenuLogo.png";
        public const string UiBlurShaderPath =
            "Assets/Game/UI/Presentation/Content/Shaders/M08A_SeparableGaussianBlur.shader";

        [MenuItem("Tools/My Summer Car/UI/Milestone 08A/Wire Production Bootstrap")]
        public static void WireProductionBootstrap()
        {
            EnsureBootstrapIsFirstEnabledBuildScene();

            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException(
                    "Production UI authoring was cancelled because open scene changes were not saved.");
            }

            SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();
            bool restoreSetup = !Application.isBatchMode && previousSetup.Length > 0;
            try
            {
                Scene scene = EditorSceneManager.OpenScene(
                    BootstrapScenePath,
                    OpenSceneMode.Single);
                // Opening a scene may unload native asset objects that are not
                // referenced by it. Load both canonical action assets only
                // after the Bootstrap scene transition so their handles remain
                // valid while the new serialized references are assigned.
                InputActionAsset playerActions =
                    RequireAsset<InputActionAsset>(PlayerActionsPath);
                InputActionAsset vehicleActions =
                    RequireAsset<InputActionAsset>(VehicleActionsPath);
                var menuVehicle = RequireAsset<MainMenuVehicleModel>(MainMenuVehicleAuthoring.PrefabPath);
                var menuEnvironment = RequireAsset<MainMenuEnvironmentModel>(MainMenuEnvironmentAuthoring.PrefabPath);
                ConfigureMainMenuLogoImport();
                Texture2D mainMenuLogo =
                    RequireAsset<Texture2D>(MainMenuLogoPath);
                Shader uiBlurShader = RequireAsset<Shader>(UiBlurShaderPath);
                GameCompositionRoot compositionRoot =
                    RequireSingleSceneComponent<GameCompositionRoot>(scene);
                ProductionWorldStreamingInstaller worldInstaller =
                    RequireSingleSceneComponent<ProductionWorldStreamingInstaller>(scene);
                ProductionUiInstaller uiInstaller =
                    GetOrAddSingleSceneComponent<ProductionUiInstaller>(
                        scene,
                        compositionRoot.gameObject);

                if (worldInstaller.gameObject != compositionRoot.gameObject ||
                    uiInstaller.gameObject != compositionRoot.gameObject)
                {
                    throw new InvalidOperationException(
                        "Production world and UI installers must share the Game Composition Root object.");
                }

                uiInstaller.ConfigureForAuthoring(
                    worldInstaller,
                    playerActions,
                    vehicleActions,
                    null,
                    mainMenuLogo,
                    uiBlurShader,
                    configuredStartInMainMenu: true);
                uiInstaller.ConfigureMenuEnvironmentForAuthoring(menuVehicle, menuEnvironment);
                if (!uiInstaller.TryValidateAuthoringConfiguration(
                        out string configurationFailure))
                {
                    throw new InvalidOperationException(
                        "Production UI authoring produced an incoherent configuration: " +
                        configurationFailure);
                }

                EditorUtility.SetDirty(uiInstaller);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, BootstrapScenePath))
                {
                    throw new InvalidOperationException(
                        "Could not save production UI wiring to Bootstrap scene.");
                }

                AssetDatabase.SaveAssets();
                Debug.Log("M08A production UI wired to Bootstrap build index 0.");
            }
            finally
            {
                if (restoreSetup)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                }
            }
        }

        [MenuItem("Tools/My Summer Car/UI/Milestone 08A/Validate Production Bootstrap")]
        public static void ValidateProductionBootstrap()
        {
            EnsureBootstrapIsFirstEnabledBuildScene();
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(BootstrapScenePath);
                GameCompositionRoot compositionRoot =
                    RequireSingleSceneComponent<GameCompositionRoot>(scene);
                ProductionWorldStreamingInstaller worldInstaller =
                    RequireSingleSceneComponent<ProductionWorldStreamingInstaller>(scene);
                ProductionUiInstaller uiInstaller =
                    RequireSingleSceneComponent<ProductionUiInstaller>(scene);

                if (worldInstaller.gameObject != compositionRoot.gameObject ||
                    uiInstaller.gameObject != compositionRoot.gameObject)
                {
                    throw new InvalidOperationException(
                        "Production world and UI installers do not share the composition root.");
                }

                if (!uiInstaller.TryValidateAuthoringConfiguration(
                        out string configurationFailure))
                {
                    throw new InvalidOperationException(
                        "Production Bootstrap UI composition is not coherent: " +
                        configurationFailure);
                }

                if (uiInstaller.PlayerActions !=
                        RequireAsset<InputActionAsset>(PlayerActionsPath) ||
                    uiInstaller.VehicleActions !=
                        RequireAsset<InputActionAsset>(VehicleActionsPath) ||
                    uiInstaller.MainMenuBackdrop != null ||
                    uiInstaller.MainMenuPreviewBackdropShader != null ||
                    uiInstaller.MainMenuVehiclePrefab !=
                        RequireAsset<MainMenuVehicleModel>(MainMenuVehicleAuthoring.PrefabPath) ||
                    uiInstaller.MainMenuEnvironmentPrefab !=
                        RequireAsset<MainMenuEnvironmentModel>(MainMenuEnvironmentAuthoring.PrefabPath) ||
                    uiInstaller.MainMenuLogo !=
                        RequireAsset<Texture2D>(MainMenuLogoPath) ||
                    uiInstaller.UiBlurShader !=
                        RequireAsset<Shader>(UiBlurShaderPath))
                {
                    throw new InvalidOperationException(
                        "Production Bootstrap does not reference the canonical 08A input, menu, or blur assets.");
                }

                Debug.Log(
                    "M08A production Bootstrap validation PASS: Bootstrap is build index 0; " +
                    "world, time, audio, player input, vehicle input, and UI composition are explicit.");
            }
            finally
            {
                if (scene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        public static void WireAndValidateBatch()
        {
            WireProductionBootstrap();
            ValidateProductionBootstrap();
        }

        private static void EnsureBootstrapIsFirstEnabledBuildScene()
        {
            EditorBuildSettingsScene[] enabled = EditorBuildSettings.scenes
                .Where(item => item.enabled)
                .ToArray();
            if (enabled.Length == 0 ||
                !string.Equals(
                    enabled[0].path,
                    BootstrapScenePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Bootstrap must remain the first enabled scene (build index 0).");
            }
        }

        private static T GetOrAddSingleSceneComponent<T>(
            Scene scene,
            GameObject owner)
            where T : Component
        {
            T[] components = GetSceneComponents<T>(scene);
            if (components.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Bootstrap scene contains {components.Length} {typeof(T).Name} components; exactly one is allowed.");
            }

            return components.Length == 1
                ? components[0]
                : owner.AddComponent<T>();
        }

        private static T RequireSingleSceneComponent<T>(Scene scene)
            where T : Component
        {
            T[] components = GetSceneComponents<T>(scene);
            if (components.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Bootstrap scene must contain exactly one {typeof(T).Name}; found {components.Length}.");
            }

            return components[0];
        }

        private static T[] GetSceneComponents<T>(Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(includeInactive: true))
                .ToArray();
        }

        private static T RequireAsset<T>(string path)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException("Required M08A asset is missing: " + path);
            }

            return asset;
        }

        private static void ConfigureMainMenuBackdropImport()
        {
            TextureImporter importer = AssetImporter.GetAtPath(MainMenuBackdropPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Required M08A menu backdrop importer is missing: " + MainMenuBackdropPath);
            }

            bool changed = importer.textureType != TextureImporterType.Default ||
                importer.alphaSource != TextureImporterAlphaSource.None ||
                importer.mipmapEnabled ||
                !importer.sRGBTexture ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.maxTextureSize != 2048 ||
                importer.npotScale != TextureImporterNPOTScale.None ||
                importer.textureCompression != TextureImporterCompression.CompressedHQ;
            if (!changed)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void ConfigureMainMenuLogoImport()
        {
            TextureImporter importer = AssetImporter.GetAtPath(MainMenuLogoPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Required M08A menu logo importer is missing: " + MainMenuLogoPath);
            }

            bool changed = importer.textureType != TextureImporterType.Default ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                !importer.alphaIsTransparency ||
                importer.mipmapEnabled ||
                !importer.sRGBTexture ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.maxTextureSize != 1024 ||
                importer.npotScale != TextureImporterNPOTScale.None ||
                importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!changed)
            {
                return;
            }

            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
