using System;
using System.IO;
using MSC.UI.Presentation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MSC.Bootstrap
{
    /// <summary>
    /// Explicit production composition for the project-owned UI. Dependencies
    /// are supplied by the Bootstrap scene; no service locator or hierarchy-name
    /// lookup is used at runtime.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class ProductionUiInstaller : MonoBehaviour
    {
        private const string SettingsDirectoryName = "Settings";
        private const string SettingsFileName = "ui-settings.json";

        [SerializeField] private ProductionWorldStreamingInstaller worldInstaller;
        [SerializeField] private InputActionAsset playerActions;
        [SerializeField] private InputActionAsset vehicleActions;
        [SerializeField] private Texture2D mainMenuBackdrop;
        [SerializeField] private Texture2D mainMenuLogo;
        [SerializeField] private Shader uiBlurShader;
        [SerializeField] private bool startInMainMenu = true;

        private GameUiRoot uiRoot;

        public ProductionWorldStreamingInstaller WorldInstaller => worldInstaller;

        public InputActionAsset PlayerActions => playerActions;

        public InputActionAsset VehicleActions => vehicleActions;

        public Texture2D MainMenuBackdrop => mainMenuBackdrop;

        public Texture2D MainMenuLogo => mainMenuLogo;

        public Shader UiBlurShader => uiBlurShader;

        public bool StartInMainMenu => startInMainMenu;

        public GameUiRoot UiRoot => uiRoot;

        public bool HasCoherentAuthoringConfiguration =>
            TryValidateAuthoringConfiguration(out _);

        public bool TryValidateAuthoringConfiguration(out string failure)
        {
            if (worldInstaller == null || worldInstaller.CompositionRoot == null)
            {
                failure = "Production world/composition root reference is missing.";
                return false;
            }

            if (worldInstaller.gameObject != gameObject ||
                worldInstaller.CompositionRoot.gameObject != gameObject)
            {
                failure = "World and UI installers must share the composition root object.";
                return false;
            }

            if (playerActions == null)
            {
                failure = "Canonical player InputActionAsset is missing.";
                return false;
            }

            if (vehicleActions == null)
            {
                failure = "Canonical vehicle InputActionAsset is missing.";
                return false;
            }

            if (mainMenuBackdrop == null)
            {
                failure = "Project-owned main-menu backdrop is missing.";
                return false;
            }

            if (mainMenuLogo == null)
            {
                failure = "Project-owned main-menu logo is missing.";
                return false;
            }

            if (uiBlurShader == null || !uiBlurShader.isSupported)
            {
                failure = "Project-owned UI Gaussian blur shader is missing or unsupported.";
                return false;
            }

            InputActionMap playerMap = playerActions.FindActionMap(
                "Player",
                throwIfNotFound: false);
            InputActionMap systemMap = playerActions.FindActionMap(
                "System",
                throwIfNotFound: false);
            if (playerMap == null ||
                systemMap?.FindAction("Pause", throwIfNotFound: false) == null)
            {
                failure = "Player and System/Pause input contracts are incomplete.";
                return false;
            }

            if (vehicleActions.FindActionMap(
                    "Vehicle",
                    throwIfNotFound: false) == null)
            {
                failure = "Vehicle input contract is incomplete.";
                return false;
            }

            if (worldInstaller.StartupMode ==
                    ProductionWorldStartupMode.ProductionEnvironmentRequired &&
                (worldInstaller.Environment == null ||
                 worldInstaller.AudioComposition == null))
            {
                failure = "Production time or audio composition reference is missing.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            ProductionWorldStreamingInstaller configuredWorldInstaller,
            InputActionAsset configuredPlayerActions,
            InputActionAsset configuredVehicleActions,
            Texture2D configuredMainMenuBackdrop,
            Texture2D configuredMainMenuLogo,
            Shader configuredUiBlurShader,
            bool configuredStartInMainMenu = true)
        {
            worldInstaller = configuredWorldInstaller ??
                throw new ArgumentNullException(nameof(configuredWorldInstaller));
            playerActions = configuredPlayerActions ??
                throw new ArgumentNullException(nameof(configuredPlayerActions));
            vehicleActions = configuredVehicleActions ??
                throw new ArgumentNullException(nameof(configuredVehicleActions));
            mainMenuBackdrop = configuredMainMenuBackdrop ??
                throw new ArgumentNullException(nameof(configuredMainMenuBackdrop));
            mainMenuLogo = configuredMainMenuLogo ??
                throw new ArgumentNullException(nameof(configuredMainMenuLogo));
            uiBlurShader = configuredUiBlurShader ??
                throw new ArgumentNullException(nameof(configuredUiBlurShader));
            startInMainMenu = configuredStartInMainMenu;
        }
#endif

        private void Awake()
        {
            ValidateRuntimeConfiguration();
            worldInstaller.ConfigureGameplayActivationDeferred(startInMainMenu);

            GameObject uiObject = new GameObject("Production Game UI");
            uiObject.transform.SetParent(
                worldInstaller.CompositionRoot.transform,
                worldPositionStays: false);

            try
            {
                Camera backdropCamera = worldInstaller.SpawnedPlayer
                    .GetComponentInChildren<Camera>(includeInactive: true);
                uiRoot = uiObject.AddComponent<GameUiRoot>();
                uiRoot.Initialize(new GameUiDependencies(
                    () => worldInstaller != null && worldInstaller.IsReady,
                    worldInstaller.Environment != null
                        ? worldInstaller.Environment.GameTime
                        : null,
                    worldInstaller.AudioComposition != null
                        ? worldInstaller.AudioComposition.Backend
                        : null,
                    playerActions,
                    vehicleActions,
                    worldInstaller.CompositionRoot.gameObject,
                    Path.Combine(
                        Application.persistentDataPath,
                        SettingsDirectoryName,
                        SettingsFileName),
                    startInMainMenu,
                    backdropCamera,
                    mainMenuBackdrop,
                    mainMenuLogo,
                    gameplaySessionGate: worldInstaller,
                    uiBlurShader: uiBlurShader));
            }
            catch
            {
                if (Application.isPlaying)
                {
                    Destroy(uiObject);
                }
                else
                {
                    DestroyImmediate(uiObject);
                }

                uiRoot = null;
                throw;
            }
        }

        private void ValidateRuntimeConfiguration()
        {
            if (!TryValidateAuthoringConfiguration(out string failure))
            {
                throw new InvalidOperationException(
                    "Production UI has incomplete Bootstrap or Input System wiring: " +
                    failure);
            }

            if (!worldInstaller.CompositionRoot.IsPrimaryRoot)
            {
                throw new InvalidOperationException(
                    "Production UI belongs to a duplicate composition root.");
            }

            if (worldInstaller.SpawnedPlayer == null)
            {
                throw new InvalidOperationException(
                    "Production UI must initialize after the production world creates its player.");
            }

            if (worldInstaller.StartupMode ==
                    ProductionWorldStartupMode.ProductionEnvironmentRequired &&
                (worldInstaller.Environment == null ||
                 worldInstaller.AudioComposition == null ||
                 !worldInstaller.AudioComposition.IsInitialized))
            {
                throw new InvalidOperationException(
                    "Production UI requires the production time and audio composition to be initialized first.");
            }

            GameUiRoot[] existingRoots =
                worldInstaller.CompositionRoot.GetComponentsInChildren<GameUiRoot>(true);
            if (existingRoots.Length != 0)
            {
                throw new InvalidOperationException(
                    "Production Bootstrap already contains a GameUiRoot instance.");
            }
        }
    }
}
