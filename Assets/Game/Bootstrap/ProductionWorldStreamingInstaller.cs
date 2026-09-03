using System;
using System.Collections;
using MSC.Audio;
using MSC.Audio.Composition;
using MSC.Audio.PlayerIntegration;
using MSC.Bootstrap.Development;
using MSC.Characters;
using MSC.Core.Lifecycle;
using MSC.Economy;
using MSC.Home;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Items;
using MSC.Needs;
using MSC.Needs.Presentation;
using MSC.NPC;
using MSC.Player;
using MSC.Save.Integration;
using MSC.Services;
using MSC.Services.Presentation;
using MSC.Traffic;
using MSC.Vehicle;
using MSC.Weather.Production;
using MSC.Weather.Wetness;
using MSC.World.Lighting;
using MSC.World.Streaming;
using MSC.World.Vegetation;
using UnityEngine;

namespace MSC.Bootstrap
{
    public enum ProductionWorldStartupMode
    {
        ProductionEnvironmentRequired = 0,
        WorldOnlyDevelopment = 1,
    }

    /// <summary>
    /// Explicit bootstrap composition for the bounded 05B.1 production-world session.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class ProductionWorldStreamingInstaller : MonoBehaviour,
        IGameplaySessionPreparationGate
    {
        private const int MaximumEnvironmentBackendStartupFrames = 16;
        private const string CharacterPresentationResourcePath =
            "Phase1Characters/CharacterPresentationCatalog";
        private const string TrafficPresentationResourcePath =
            "Phase1Traffic/TrafficPresentationCatalog";

        [SerializeField] private GameCompositionRoot compositionRoot;
        [SerializeField] private ProductionWorldStreamingService worldStreaming;
        [SerializeField] private ProductionEnvironmentController environment;
        [SerializeField] private ProductionAudioComposition audioComposition;
        [SerializeField] private ProductionWorldStartupMode startupMode =
            ProductionWorldStartupMode.ProductionEnvironmentRequired;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private ItemDefinitionCatalog itemDefinitionCatalog;
        [SerializeField] private ItemPlacementCatalog itemPlacementCatalog;
        [SerializeField] private WorldLightingProbeCatalog lightingProbeCatalog;
        [SerializeField] private CharacterDefinitionCatalog characterDefinitionCatalog;
        [SerializeField] private NpcFoundationCatalog npcFoundationCatalog;
        [SerializeField] private NpcDialogueCatalog npcDialogueCatalog;
        [SerializeField] private CharacterPresentationCatalog characterPresentationCatalog;
        [SerializeField] private TrafficRoadNetworkCatalog trafficRoadNetworkCatalog;
        [SerializeField] private TrafficPresentationCatalog trafficPresentationCatalog;
        [SerializeField] private EconomyPriceCatalog economyPriceCatalog;
        [SerializeField] private ServiceCatalog serviceCatalog;
        [SerializeField] private Vector3 playerSpawnPosition;
        [SerializeField] private Vector3 playerSpawnEulerAngles;
        [SerializeField] private float outOfBoundsMinimumY = -64f;
        [SerializeField] private Vector3 importantObjectRecoveryOrigin =
            new Vector3(157.08923f, 8f, -1022f);

        private GameObject spawnedPlayer;
        private Camera spawnedPlayerCamera;
        private bool spawnedPlayerCameraInitiallyEnabled;
        private bool ownsPackedWoodyCameraAuthority;
        private WorldOutOfBoundsRecovery outOfBoundsRecovery;
        private NativeSaveSessionController nativeSaveSession;
        private ItemWorldRuntime itemWorldRuntime;
        private PlayerNeedsRuntime playerNeeds;
        private HomeSystemRuntime homeRuntime;
        private ProductionFoodApplianceInstaller foodApplianceInstaller;
        private ProductionPuddleBridge puddleBridge;
        private ProductionItemEffectsBridge itemEffectsBridge;
        private WorldLightingProbeRuntime lightingProbeRuntime;
        private NpcWorldRuntime npcWorldRuntime;
        private TrafficWorldRuntime trafficWorldRuntime;
        private EconomyRuntime economyRuntime;
        private ServiceRuntime serviceRuntime;
        private ProductionServiceInteractionInstaller serviceInteractionInstaller;
        private ProductionDeveloperConsole developerConsole;
        private ProductionSatsumaInstaller satsumaInstaller;
        private bool deferGameplayActivation;
        private bool startupRoutineStarted;

        public GameCompositionRoot CompositionRoot => compositionRoot;
        public ProductionWorldStreamingService WorldStreaming => worldStreaming;
        public ProductionEnvironmentController Environment => environment;
        public ProductionAudioComposition AudioComposition => audioComposition;
        public ProductionServiceInteractionInstaller ServiceInteractionInstaller =>
            serviceInteractionInstaller;
        public ProductionWorldStartupMode StartupMode => startupMode;
        public bool HasCoherentStartupConfiguration
        {
            get
            {
                ProductionEnvironmentBackendActivator backendActivator =
                    GetComponent<ProductionEnvironmentBackendActivator>();
                switch (startupMode)
                {
                    case ProductionWorldStartupMode
                        .ProductionEnvironmentRequired:
                        return environment != null &&
                            environment.enabled &&
                            backendActivator != null &&
                            backendActivator.enabled;

                    case ProductionWorldStartupMode.WorldOnlyDevelopment:
                        if (environment != null ||
                            backendActivator != null &&
                            backendActivator.enabled)
                        {
                            return false;
                        }

                        ProductionEnvironmentController[] controllers =
                            GetComponents<ProductionEnvironmentController>();
                        for (int index = 0;
                             index < controllers.Length;
                             index++)
                        {
                            if (controllers[index].enabled)
                            {
                                return false;
                            }
                        }

                        MonoBehaviour[] rootBehaviours =
                            GetComponents<MonoBehaviour>();
                        for (int index = 0;
                             index < rootBehaviours.Length;
                             index++)
                        {
                            MonoBehaviour behaviour = rootBehaviours[index];
                            if (behaviour != null &&
                                behaviour.enabled &&
                                behaviour is IWetnessShaderBridge)
                            {
                                return false;
                            }
                        }

                        ProductionEnvironmentBackendMarker[] markers =
                            GetComponentsInChildren<
                                ProductionEnvironmentBackendMarker>(true);
                        for (int index = 0;
                             index < markers.Length;
                             index++)
                        {
                            if (markers[index].gameObject.activeInHierarchy)
                            {
                                return false;
                            }
                        }

                        return true;

                    default:
                        return false;
                }
            }
        }
        public GameObject PlayerPrefab => playerPrefab;
        public Vector3 PlayerSpawnPosition => playerSpawnPosition;
        public Quaternion PlayerSpawnRotation => Quaternion.Euler(playerSpawnEulerAngles);
        public GameObject SpawnedPlayer => spawnedPlayer;
        public WorldOutOfBoundsRecovery OutOfBoundsRecovery =>
            outOfBoundsRecovery;
        public NativeSaveSessionController NativeSaveSession =>
            nativeSaveSession;
        public ItemWorldRuntime ItemWorldRuntime => itemWorldRuntime;
        public PlayerNeedsRuntime PlayerNeeds => playerNeeds;
        public HomeSystemRuntime HomeRuntime => homeRuntime;
        public ProductionFoodApplianceInstaller FoodApplianceInstaller =>
            foodApplianceInstaller;
        public WorldLightingProbeRuntime LightingProbeRuntime =>
            lightingProbeRuntime;
        public NpcWorldRuntime NpcWorldRuntime => npcWorldRuntime;
        public TrafficWorldRuntime TrafficWorldRuntime => trafficWorldRuntime;
        public EconomyRuntime EconomyRuntime => economyRuntime;
        public ServiceRuntime ServiceRuntime => serviceRuntime;
        public ProductionDeveloperConsole DeveloperConsole => developerConsole;
        public float OutOfBoundsMinimumY => outOfBoundsMinimumY;
        public Vector3 ImportantObjectRecoveryOrigin =>
            importantObjectRecoveryOrigin;
        public bool IsReady { get; private set; }
        public bool IsGameplayPrepared { get; private set; }

        public bool TryConfigureNewGameSatsumaPaint(
            int paletteIndex,
            Color bodyColor,
            out string failure)
        {
            if (nativeSaveSession != null && nativeSaveSession.HasRestoredSave)
            {
                failure = "A restored vehicle cannot be repainted by New Game setup.";
                return false;
            }

            if (satsumaInstaller == null)
            {
                failure = "The Phase 1 Satsuma installer is not ready.";
                return false;
            }

            return satsumaInstaller.TryApplyNewGamePaint(
                paletteIndex,
                bodyColor,
                out failure);
        }
        public bool IsGameplayActive { get; private set; }
        public bool IsGameplayPreparationRunning { get; private set; }
        public string LastGameplayPreparationFailure { get; private set; } =
            string.Empty;

        /// <summary>
        /// Configures the initial front-end policy after this installer's Awake
        /// has created the player, but before Unity invokes Start. Without a UI
        /// installer this is never called, preserving the established automatic
        /// world reveal used by development and validation scenes.
        /// </summary>
        public void ConfigureGameplayActivationDeferred(bool deferred)
        {
            if (startupRoutineStarted || IsGameplayPrepared || IsGameplayActive)
            {
                throw new InvalidOperationException(
                    "Gameplay activation policy must be configured before world startup begins.");
            }

            deferGameplayActivation = deferred;
            if (deferred && spawnedPlayerCamera != null)
            {
                // Apply while the player hierarchy is still inactive so there
                // cannot be a rendered gameplay frame behind the main menu.
                spawnedPlayerCamera.enabled = false;
            }
        }

        public bool TryActivateGameplay(out string failure)
        {
            if (IsGameplayActive)
            {
                failure = string.Empty;
                return true;
            }

            if (!IsGameplayPrepared)
            {
                failure = "The production world is not prepared for gameplay activation.";
                return false;
            }

            if (spawnedPlayer == null)
            {
                failure = "The prepared production player is missing.";
                return false;
            }

            if (deferGameplayActivation && spawnedPlayerCamera == null)
            {
                failure = "The deferred production gameplay camera is missing.";
                return false;
            }

            if (startupMode ==
                    ProductionWorldStartupMode.ProductionEnvironmentRequired &&
                (environment == null || !environment.IsWorldRevealReady))
            {
                failure = "The production environment is not ready for gameplay activation.";
                return false;
            }

            bool environmentStartedHere = false;
            try
            {
                if (!spawnedPlayer.activeSelf)
                {
                    spawnedPlayer.SetActive(true);
                }

                if (startupMode ==
                        ProductionWorldStartupMode.ProductionEnvironmentRequired &&
                    !environment.IsSimulationActive)
                {
                    environment.BeginSimulationAfterWorldReveal();
                    environmentStartedHere = true;
                }

                if (spawnedPlayerCamera != null)
                {
                    spawnedPlayerCamera.enabled =
                        spawnedPlayerCameraInitiallyEnabled;
                }

                IsGameplayActive = true;
                failure = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                if (spawnedPlayerCamera != null)
                {
                    spawnedPlayerCamera.enabled = false;
                }

                if (environmentStartedHere)
                {
                    environment.StopSimulation();
                }

                failure = "Gameplay activation failed: " + exception.Message;
                return false;
            }
        }

        public bool TryBeginGameplayPreparation(out string failure)
        {
            if (IsGameplayPrepared || IsGameplayPreparationRunning)
            {
                failure = string.Empty;
                return true;
            }

            if (!startupRoutineStarted)
            {
                failure =
                    "Gameplay preparation cannot begin before Bootstrap startup.";
                return false;
            }

            LastGameplayPreparationFailure = string.Empty;
            StartCoroutine(PrepareGameplay());
            failure = string.Empty;
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            GameCompositionRoot configuredRoot,
            ProductionWorldStreamingService configuredStreaming,
            GameObject configuredPlayerPrefab,
            Vector3 spawnPosition,
            Quaternion spawnRotation)
        {
            compositionRoot = configuredRoot;
            worldStreaming = configuredStreaming;
            playerPrefab = configuredPlayerPrefab;
            playerSpawnPosition = spawnPosition;
            playerSpawnEulerAngles = spawnRotation.eulerAngles;
            startupMode = environment == null
                ? ProductionWorldStartupMode.WorldOnlyDevelopment
                : ProductionWorldStartupMode.ProductionEnvironmentRequired;
        }

        public void ConfigureWorldOnlyForAuthoring()
        {
            environment = null;
            ProductionEnvironmentController[] controllers =
                GetComponents<ProductionEnvironmentController>();
            for (int index = 0; index < controllers.Length; index++)
            {
                DestroyImmediate(controllers[index]);
            }

            ProductionEnvironmentBackendActivator[] activators =
                GetComponents<ProductionEnvironmentBackendActivator>();
            for (int index = 0; index < activators.Length; index++)
            {
                DestroyImmediate(activators[index]);
            }

            MonoBehaviour[] rootBehaviours = GetComponents<MonoBehaviour>();
            for (int index = 0; index < rootBehaviours.Length; index++)
            {
                MonoBehaviour behaviour = rootBehaviours[index];
                if (behaviour != null && behaviour is IWetnessShaderBridge)
                {
                    DestroyImmediate(behaviour);
                }
            }

            ProductionEnvironmentBackendMarker[] markers =
                GetComponentsInChildren<
                    ProductionEnvironmentBackendMarker>(true);
            for (int index = 0; index < markers.Length; index++)
            {
                if (markers[index].gameObject == gameObject)
                {
                    throw new InvalidOperationException(
                        "World-only conversion cannot delete a backend marker " +
                        "from the composition-root object.");
                }

                DestroyImmediate(markers[index].gameObject);
            }

            startupMode = ProductionWorldStartupMode.WorldOnlyDevelopment;
        }

        public void ConfigureEnvironmentForAuthoring(
            ProductionEnvironmentController configuredEnvironment)
        {
            environment = configuredEnvironment ??
                throw new ArgumentNullException(nameof(configuredEnvironment));
            environment.enabled = true;
            startupMode =
                ProductionWorldStartupMode.ProductionEnvironmentRequired;
        }

        public void ConfigureAudioForAuthoring(
            ProductionAudioComposition configuredAudioComposition)
        {
            audioComposition = configuredAudioComposition ??
                throw new ArgumentNullException(nameof(configuredAudioComposition));
        }

        public void ConfigureItemsForAuthoring(
            ItemDefinitionCatalog configuredDefinitions,
            ItemPlacementCatalog configuredPlacements)
        {
            itemDefinitionCatalog = configuredDefinitions ??
                throw new ArgumentNullException(nameof(configuredDefinitions));
            itemPlacementCatalog = configuredPlacements ??
                throw new ArgumentNullException(nameof(configuredPlacements));
        }

        public void ConfigureLightingForAuthoring(
            WorldLightingProbeCatalog configuredCatalog)
        {
            lightingProbeCatalog = configuredCatalog ??
                throw new ArgumentNullException(nameof(configuredCatalog));
        }

        public void ConfigureNpcFoundationForAuthoring(
            CharacterDefinitionCatalog configuredCharacters,
            NpcFoundationCatalog configuredFoundation,
            CharacterPresentationCatalog configuredPresentation)
        {
            characterDefinitionCatalog = configuredCharacters ??
                throw new ArgumentNullException(nameof(configuredCharacters));
            npcFoundationCatalog = configuredFoundation ??
                throw new ArgumentNullException(nameof(configuredFoundation));
            if (configuredPresentation == null)
            {
                throw new ArgumentNullException(nameof(configuredPresentation));
            }

            // The private ignored catalog receives a local asset GUID. Do not
            // serialize that machine-local GUID into the tracked Bootstrap
            // scene; resolve the explicit project-owned Resources registry at
            // session startup after the build guard has validated it.
            characterPresentationCatalog = null;
        }

        public void ConfigureNpcFoundationForAuthoring(
            CharacterDefinitionCatalog configuredCharacters,
            NpcFoundationCatalog configuredFoundation,
            NpcDialogueCatalog configuredDialogue,
            CharacterPresentationCatalog configuredPresentation)
        {
            ConfigureNpcFoundationForAuthoring(
                configuredCharacters,
                configuredFoundation,
                configuredPresentation);
            npcDialogueCatalog = configuredDialogue ??
                throw new ArgumentNullException(nameof(configuredDialogue));
        }

        public void ConfigureTrafficForAuthoring(
            TrafficRoadNetworkCatalog configuredRoadNetwork,
            TrafficPresentationCatalog configuredPresentation)
        {
            trafficRoadNetworkCatalog = configuredRoadNetwork ??
                throw new ArgumentNullException(nameof(configuredRoadNetwork));
            if (configuredPresentation == null)
            {
                throw new ArgumentNullException(nameof(configuredPresentation));
            }

            // Private donor-derived presentation has a machine-local asset
            // GUID. Runtime resolves the explicit Resources registry instead
            // of serializing that GUID into the tracked Bootstrap scene.
            trafficPresentationCatalog = null;
        }

        public void ConfigureEconomyForAuthoring(
            EconomyPriceCatalog configuredCatalog)
        {
            economyPriceCatalog = configuredCatalog ??
                throw new ArgumentNullException(nameof(configuredCatalog));
        }

        public void ConfigureServicesForAuthoring(
            ServiceCatalog configuredCatalog)
        {
            serviceCatalog = configuredCatalog ??
                throw new ArgumentNullException(nameof(configuredCatalog));
            serviceInteractionInstaller =
                GetComponent<ProductionServiceInteractionInstaller>() ??
                gameObject.AddComponent<ProductionServiceInteractionInstaller>();
            serviceInteractionInstaller.ConfigureForAuthoring(serviceCatalog);
        }

        public void ConfigurePlayerSpawnForAuthoring(
            Vector3 spawnPosition,
            Quaternion spawnRotation)
        {
            playerSpawnPosition = spawnPosition;
            playerSpawnEulerAngles = spawnRotation.eulerAngles;
        }
#endif

        private void Awake()
        {
            ValidateConfiguration();

            spawnedPlayer = Instantiate(
                playerPrefab,
                playerSpawnPosition,
                PlayerSpawnRotation,
                compositionRoot.transform);
            spawnedPlayer.SetActive(false);
            spawnedPlayerCamera =
                spawnedPlayer.GetComponentInChildren<Camera>(true);
            spawnedPlayerCameraInitiallyEnabled =
                spawnedPlayerCamera != null && spawnedPlayerCamera.enabled;
            if (spawnedPlayerCamera != null)
            {
                PackedWoodyCollisionPool.BindAuthoritativeGameCamera(
                    spawnedPlayerCamera);
                ownsPackedWoodyCameraAuthority = true;
            }
            outOfBoundsRecovery =
                spawnedPlayer.GetComponent<WorldOutOfBoundsRecovery>();
            if (outOfBoundsRecovery == null)
            {
                outOfBoundsRecovery =
                    spawnedPlayer.AddComponent<WorldOutOfBoundsRecovery>();
            }

            outOfBoundsRecovery.Configure(
                playerSpawnPosition,
                PlayerSpawnRotation,
                outOfBoundsMinimumY);

            if (startupMode ==
                ProductionWorldStartupMode.ProductionEnvironmentRequired)
            {
                if (!environment.BindPresentationCamera(spawnedPlayerCamera))
                {
                    throw new InvalidOperationException(
                        "Production environment could not bind the spawned player camera: " +
                        environment.LastFailure);
                }
            }

            if (lightingProbeCatalog != null &&
                startupMode ==
                    ProductionWorldStartupMode.ProductionEnvironmentRequired)
            {
                lightingProbeRuntime =
                    GetComponent<WorldLightingProbeRuntime>() ??
                    gameObject.AddComponent<WorldLightingProbeRuntime>();
                lightingProbeRuntime.Initialize(
                    lightingProbeCatalog,
                    worldStreaming,
                    environment.AuthoritativeGameTime,
                    spawnedPlayer.transform);
            }

            if (itemDefinitionCatalog != null && itemPlacementCatalog != null)
            {
                itemWorldRuntime = GetComponent<ItemWorldRuntime>() ??
                    gameObject.AddComponent<ItemWorldRuntime>();
                itemWorldRuntime.Initialize(
                    itemDefinitionCatalog,
                    itemPlacementCatalog,
                    worldStreaming,
                    gameObject.scene,
                    outOfBoundsMinimumY,
                    environment.AuthoritativeGameTime,
                    importantObjectRecoveryOrigin);
            }

            if (startupMode ==
                    ProductionWorldStartupMode.ProductionEnvironmentRequired &&
                (audioComposition == null ||
                 !audioComposition.InitializeSession(spawnedPlayer, environment)))
            {
                throw new InvalidOperationException(
                    "Production audio composition failed: " +
                    (audioComposition != null
                        ? audioComposition.LastFailure
                        : "composition is missing."));
            }

            if (startupMode ==
                ProductionWorldStartupMode.ProductionEnvironmentRequired)
            {
                puddleBridge = GetComponent<ProductionPuddleBridge>() ??
                    gameObject.AddComponent<ProductionPuddleBridge>();
                puddleBridge.Initialize(
                    environment,
                    itemWorldRuntime,
                    spawnedPlayer.transform,
                    compositionRoot.transform);
                itemEffectsBridge =
                    GetComponent<ProductionItemEffectsBridge>() ??
                    gameObject.AddComponent<ProductionItemEffectsBridge>();
                itemEffectsBridge.Initialize(itemWorldRuntime);
            }

            if (startupMode ==
                ProductionWorldStartupMode.ProductionEnvironmentRequired)
            {
                FirstPersonMotor motor =
                    spawnedPlayer.GetComponentInChildren<FirstPersonMotor>(true) ??
                    throw new InvalidOperationException(
                        "Production player has no FirstPersonMotor needs boundary.");
                playerNeeds = GetComponent<PlayerNeedsRuntime>() ??
                    gameObject.AddComponent<PlayerNeedsRuntime>();
                playerNeeds.Initialize(
                    environment.GameTime,
                    motor,
                    itemWorldRuntime,
                    environment);

                PlayerInputRouter input =
                    spawnedPlayer.GetComponentInChildren<PlayerInputRouter>(true) ??
                    throw new InvalidOperationException(
                        "Production player has no PlayerInputRouter life-action boundary.");
                PlayerLifeActionController playerLifeActions =
                    spawnedPlayer.GetComponent<PlayerLifeActionController>() ??
                    spawnedPlayer.AddComponent<PlayerLifeActionController>();
                playerLifeActions.Initialize(playerNeeds, input);

                FirstPersonLifeActionPresenter lifeActionPresenter =
                    spawnedPlayer.GetComponent<FirstPersonLifeActionPresenter>() ??
                    spawnedPlayer.AddComponent<FirstPersonLifeActionPresenter>();
                lifeActionPresenter.Initialize(
                    spawnedPlayer.transform,
                    motor,
                    input,
                    spawnedPlayerCamera,
                    itemWorldRuntime,
                    playerNeeds);
                PlayerNeedsPerceptionPresenter perceptionPresenter =
                    spawnedPlayer.GetComponent<PlayerNeedsPerceptionPresenter>() ??
                    spawnedPlayer.AddComponent<PlayerNeedsPerceptionPresenter>();
                perceptionPresenter.Initialize(
                    spawnedPlayerCamera,
                    playerNeeds);
                PlayerFatigueHdrpPresenter fatigueHdrpPresenter =
                    spawnedPlayer.GetComponent<PlayerFatigueHdrpPresenter>() ??
                    spawnedPlayer.AddComponent<PlayerFatigueHdrpPresenter>();
                fatigueHdrpPresenter.Initialize(playerNeeds);
                CreateHomeSleepTargets(
                    playerNeeds,
                    lifeActionPresenter);

                ProductionHomeInstaller homeInstaller =
                    GetComponent<ProductionHomeInstaller>() ??
                    gameObject.AddComponent<ProductionHomeInstaller>();
                homeRuntime = homeInstaller.Initialize(
                    compositionRoot.transform,
                    environment.GameTime,
                    playerNeeds,
                    spawnedPlayer.transform,
                    message =>
                        lifeActionPresenter.ShowStatusMessage(message));

                foodApplianceInstaller =
                    GetComponent<ProductionFoodApplianceInstaller>() ??
                    gameObject.AddComponent<ProductionFoodApplianceInstaller>();
                foodApplianceInstaller.Initialize(
                    compositionRoot.transform,
                    worldStreaming,
                    itemWorldRuntime,
                    homeRuntime,
                    audioComposition.Backend);

                satsumaInstaller =
                    GetComponent<ProductionSatsumaInstaller>() ??
                    gameObject.AddComponent<ProductionSatsumaInstaller>();
                MonoBehaviour satsumaAssemblyAudioBackend = null;
                if (audioComposition.TryLoadFallbackOverrideEventLibrary(
                        VehicleAssemblyAudioPresenter
                            .FallbackOverrideResourcesPath,
                        out string satsumaAudioFailure))
                {
                    // Gameplay posts stable IDs through the router. The Unity
                    // fallback resolves the private override locally, while a
                    // ready official Wwise backend receives the same IDs.
                    satsumaAssemblyAudioBackend =
                        audioComposition.Backend as MonoBehaviour;
                }
                else
                {
                    // Silence is preferable to the old generic interaction
                    // placeholder that sounded like a door opening.
                    Debug.LogWarning(
                        "Private Phase 1 Satsuma assembly audio is unavailable: " +
                        satsumaAudioFailure,
                        this);
                }

                GameObject spawnedSatsuma = satsumaInstaller.Initialize(
                    compositionRoot.transform,
                    satsumaAssemblyAudioBackend);

                economyRuntime = GetComponent<EconomyRuntime>() ??
                    gameObject.AddComponent<EconomyRuntime>();
                economyRuntime.Initialize(
                    economyPriceCatalog,
                    environment.GameTime);

                if (!audioComposition.TryLoadFallbackSupplementalEventLibrary(
                        PlayerVoiceReactionCatalog.FallbackResourcesPath,
                        out string playerVoiceLibraryFailure))
                {
                    Debug.LogWarning(
                        "Private Phase 1 player voice fallback is unavailable: " +
                        playerVoiceLibraryFailure,
                        this);
                }

                PlayerVoiceReactionController voiceReactions =
                    spawnedPlayer.GetComponent<PlayerVoiceReactionController>() ??
                    spawnedPlayer.AddComponent<PlayerVoiceReactionController>();
                voiceReactions.Initialize(
                    input,
                    playerNeeds,
                    playerNeeds,
                    economyRuntime,
                    audioComposition.Backend,
                    audioComposition.FallbackBackend,
                    subtitle => lifeActionPresenter.ShowStatusMessage(
                        subtitle,
                        2.5f));

                npcWorldRuntime = GetComponent<NpcWorldRuntime>() ??
                    gameObject.AddComponent<NpcWorldRuntime>();
                npcWorldRuntime.ConfigureVehicleAudio(
                    // The private Phase 1 traffic clips are intentionally kept
                    // in the removable Unity supplemental library until their
                    // Wwise events and bank media are authored. Routing these
                    // loops through the ready-but-unmapped Wwise backend
                    // produces valid simulation with complete silence.
                    audioComposition.FallbackBackend as MonoBehaviour);
                npcWorldRuntime.Initialize(
                    characterDefinitionCatalog,
                    npcFoundationCatalog,
                    characterPresentationCatalog,
                    environment.GameTime,
                    worldStreaming,
                    spawnedPlayer.transform);
                PhysicalCarryController storyCarry =
                    spawnedPlayer.GetComponentInChildren<
                        PhysicalCarryController>(true) ??
                    throw new InvalidOperationException(
                        "Production player has no carry boundary for the Suski rescue flow.");
                npcWorldRuntime.ConfigureStoryTrafficRescue(storyCarry);
                playerNeeds.LifeActionCompleted +=
                    HandlePlayerLifeActionCompleted;
                npcWorldRuntime.ConfigureDialogue(
                    npcDialogueCatalog,
                    (line, worldPosition) =>
                    {
                        if (!string.IsNullOrWhiteSpace(line.FallbackSubtitle))
                        {
                            lifeActionPresenter.ShowStatusMessage(
                                line.FallbackSubtitle,
                                4f);
                        }

                        if (audioComposition?.Backend == null ||
                            !audioComposition.Backend.IsReady ||
                            string.IsNullOrWhiteSpace(line.AudioEventId))
                        {
                            return;
                        }

                        var request = new AudioEventRequest(
                            new AudioEventId(line.AudioEventId),
                            worldPosition: worldPosition,
                            allowMultiple: false);
                        IAudioEventHandle handle =
                            audioComposition.Backend.PostEvent(in request);
                        if (!handle.IsValid &&
                            audioComposition.FallbackBackend != null &&
                            audioComposition.FallbackBackend.IsReady)
                        {
                            // Phase 1 donor voice clips live only in the
                            // removable Unity fallback supplement. A future
                            // preferred-backend mapping wins automatically.
                            audioComposition.FallbackBackend.PostEvent(in request);
                        }
                    });

                TeimoServicePresentationDirector teimoServicePresentation =
                    GetComponent<TeimoServicePresentationDirector>() ??
                    gameObject.AddComponent<TeimoServicePresentationDirector>();
                teimoServicePresentation.Configure(
                    npcWorldRuntime,
                    itemWorldRuntime?.Definitions,
                    spawnedPlayerCamera.transform,
                    input,
                    lifeActionPresenter.UrineStream,
                    message => lifeActionPresenter.ShowStatusMessage(message));
                var pubEffects = new TeimoPubEffectHandoffBackend();
                var itemHandoff = new ServiceItemHandoffBackend(
                    serviceCatalog,
                    itemWorldRuntime,
                    gameObject.scene,
                    pubEffects);
                var preparedHandoff =
                    new TeimoPreparedServiceHandoffBackend(
                        itemHandoff,
                        teimoServicePresentation);

                serviceRuntime = GetComponent<ServiceRuntime>() ??
                    gameObject.AddComponent<ServiceRuntime>();
                if (!serviceCatalog.TryGetLocation(
                        "service.location.workshop.fleetari",
                        out ServiceLocationDefinition fleetariWorkshop))
                {
                    throw new InvalidOperationException(
                        "The Fleetari workshop location is missing from the service catalog.");
                }

                serviceRuntime.Initialize(
                    serviceCatalog,
                    economyRuntime,
                    environment.GameTime,
                    new ProductionNpcServiceAvailabilitySource(
                        npcWorldRuntime),
                    new ProductionPlayerServiceProximitySource(
                        serviceCatalog,
                        spawnedPlayer.transform),
                    preparedHandoff,
                    configuredInspection: null,
                    configuredWorkshopOutcome:
                        new SatsumaWorkshopOutcomeBackend(
                            spawnedSatsuma,
                            fleetariWorkshop.WorldPosition));
                preparedHandoff.BindRuntime(serviceRuntime);

                serviceInteractionInstaller =
                    GetComponent<ProductionServiceInteractionInstaller>() ??
                    gameObject.AddComponent<ProductionServiceInteractionInstaller>();
                serviceInteractionInstaller.Initialize(
                    compositionRoot.transform,
                    serviceCatalog,
                    serviceRuntime,
                    feedback =>
                    {
                        if (!string.IsNullOrWhiteSpace(feedback.FallbackMessage))
                        {
                            lifeActionPresenter.ShowStatusMessage(
                                feedback.FallbackMessage);
                        }
                    },
                    teimoServicePresentation,
                    spawnedPlayerCamera,
                    input,
                    itemWorldRuntime);

                trafficWorldRuntime = GetComponent<TrafficWorldRuntime>() ??
                    gameObject.AddComponent<TrafficWorldRuntime>();
                trafficWorldRuntime.Initialize(
                    trafficRoadNetworkCatalog,
                    trafficPresentationCatalog,
                    environment.GameTime,
                    spawnedPlayer.transform,
                    audioComposition.FallbackBackend as MonoBehaviour,
                    worldStreaming);
                trafficWorldRuntime.ConfigureBusDriverSubtitleFeedback(
                    (_, fallbackSubtitle, _) =>
                    {
                        if (!string.IsNullOrWhiteSpace(fallbackSubtitle))
                        {
                            lifeActionPresenter.ShowStatusMessage(
                                fallbackSubtitle,
                                4f);
                        }
                    });

                developerConsole =
                    GetComponent<ProductionDeveloperConsole>() ??
                    gameObject.AddComponent<ProductionDeveloperConsole>();
                developerConsole.Initialize(
                    spawnedPlayer,
                    input,
                    playerNeeds,
                    environment,
                    lifeActionPresenter,
                    homeRuntime);

                nativeSaveSession =
                    GetComponent<NativeSaveSessionController>() ??
                    gameObject.AddComponent<NativeSaveSessionController>();
                nativeSaveSession.ConfigureImportantObjectRecovery(
                    importantObjectRecoveryOrigin,
                    outOfBoundsMinimumY);
                nativeSaveSession.Initialize(
                    spawnedPlayer,
                    environment,
                    worldStreaming,
                    itemWorldRuntime,
                    playerNeeds,
                    homeRuntime,
                    npcWorldRuntime,
                    trafficWorldRuntime,
                    economyRuntime,
                    serviceRuntime);
            }

            compositionRoot.Initialize(startupMode ==
                ProductionWorldStartupMode.ProductionEnvironmentRequired
                ? GameServiceBindings.CreateProductionEnvironmentAudioSavePartial(
                    environment.GameTime,
                    environment.Weather,
                    nativeSaveSession.SaveService,
                    audioComposition.Backend,
                    worldStreaming)
                : GameServiceBindings.CreateWorldStreamingPartial(
                    worldStreaming));
        }

        private void HandlePlayerLifeActionCompleted(
            PlayerLifeActionCompleted action)
        {
            if (action.Succeeded &&
                action.Kind == PlayerLifeActionKind.Sleep &&
                npcWorldRuntime != null &&
                npcWorldRuntime.IsInitialized)
            {
                npcWorldRuntime.NotifyPlayerSlept();
            }
        }

        private void OnDestroy()
        {
            if (ownsPackedWoodyCameraAuthority)
            {
                PackedWoodyCollisionPool.ReleaseAuthoritativeGameCamera(
                    spawnedPlayerCamera);
                ownsPackedWoodyCameraAuthority = false;
            }
            if (playerNeeds != null)
            {
                playerNeeds.LifeActionCompleted -=
                    HandlePlayerLifeActionCompleted;
            }

            npcWorldRuntime?.ConfigureStoryTrafficRescue(null);
        }

        private void CreateHomeSleepTargets(
            PlayerNeedsRuntime configuredNeeds,
            FirstPersonLifeActionPresenter configuredPresenter)
        {
            // Frozen M04A1 donor registry positions. These project-owned
            // targets use stable action IDs and never search donor hierarchy
            // names at runtime. Thin contact patches stay on furniture surfaces
            // instead of introducing invisible obstacles above the furniture.
            CreateSleepTarget(
                "09C Player Bed Sleep Target",
                "home.bed.player",
                new Vector3(157.87459f, 1.5751781f, -1027.2485f),
                new Vector3(0.88f, 0.025f, 1.72f),
                180f,
                configuredNeeds,
                configuredPresenter);
            CreateSleepTarget(
                "09C Parents Bed Sleep Target",
                "home.bed.parents",
                new Vector3(168.3286f, 1.56f, -1027.5336f),
                new Vector3(1.72f, 0.025f, 2.02f),
                180f,
                configuredNeeds,
                configuredPresenter);
            CreateSleepTarget(
                "09C Living Room Sofa Sleep Target",
                "home.sofa.livingroom",
                new Vector3(165.44293f, 1.63f, -1031.7847f),
                new Vector3(0.82f, 0.025f, 1.96f),
                0f,
                configuredNeeds,
                configuredPresenter);
        }

        private void CreateSleepTarget(
            string objectName,
            string actionId,
            Vector3 worldPosition,
            Vector3 surfaceSize,
            float sleepYawDegrees,
            PlayerNeedsRuntime configuredNeeds,
            FirstPersonLifeActionPresenter configuredPresenter)
        {
            var targetObject = new GameObject(objectName);
            targetObject.transform.SetParent(compositionRoot.transform, false);
            targetObject.transform.position = worldPosition;

            BoxCollider collider = targetObject.AddComponent<BoxCollider>();
            collider.size = surfaceSize;
            collider.isTrigger = false;

            PlayerLifeActionInteractionTarget target =
                targetObject.AddComponent<PlayerLifeActionInteractionTarget>();
            target.Configure(
                actionId,
                "Спать",
                PlayerLifeActionKind.Sleep,
                configuredNeeds,
                configuredPresenter,
                worldPosition + Vector3.up * 0.34f,
                Quaternion.Euler(0f, sleepYawDegrees, 0f));
            InteractionTargetHost host =
                targetObject.AddComponent<InteractionTargetHost>();
            host.Configure(target);
        }

        private IEnumerator Start()
        {
            startupRoutineStarted = true;
            if (deferGameplayActivation)
            {
                // Main-menu presentation is project-owned static UI. Keep all
                // additive world scenes unloaded until New Game is requested.
                yield break;
            }

            yield return PrepareGameplay();
            if (!IsGameplayPrepared)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(LastGameplayPreparationFailure)
                        ? "Production world preparation failed."
                        : LastGameplayPreparationFailure);
            }

            if (!TryActivateGameplay(out string activationFailure))
            {
                throw new InvalidOperationException(activationFailure);
            }
        }

        private IEnumerator PrepareGameplay()
        {
            if (IsGameplayPrepared || IsGameplayPreparationRunning)
            {
                yield break;
            }

            IsGameplayPreparationRunning = true;
            LastGameplayPreparationFailure = string.Empty;
            bool completed = false;
            try
            {
                if (startupMode ==
                    ProductionWorldStartupMode.ProductionEnvironmentRequired)
                {
                    ProductionEnvironmentBackendActivator backendActivator =
                        GetComponent<ProductionEnvironmentBackendActivator>();
                    if (backendActivator == null)
                    {
                        throw new InvalidOperationException(
                            "Production environment backend activator is missing.");
                    }

                    for (int frame = 0;
                         frame < MaximumEnvironmentBackendStartupFrames &&
                         !backendActivator.IsBackendRuntimeActive;
                         frame++)
                    {
                        yield return null;
                    }

                    if (!backendActivator.IsBackendRuntimeActive)
                    {
                        LastGameplayPreparationFailure =
                            "Production environment backend did not activate before " +
                            "the startup deadline.";
                        yield break;
                    }
                }

                bool hasPendingNativeRestore =
                    nativeSaveSession?.HasPendingRestore == true;
                if (!hasPendingNativeRestore &&
                    startupMode ==
                        ProductionWorldStartupMode.ProductionEnvironmentRequired)
                {
                    // Preserve the established New Game boot order. Only a
                    // queued native load needs static world support before its
                    // environment state can be staged.
                    yield return environment.InitializeBeforeWorldReveal();
                }

                // Binding the focus is the explicit point at which the streaming
                // service may begin loading additive gameplay scenes. It must not
                // happen during front-end boot.
                worldStreaming.BindFocus(spawnedPlayer.transform);
                yield return worldStreaming.RefreshNow();
                bool initialWorldReady =
                    worldStreaming.HasFocus && !worldStreaming.IsStreaming;
                if (initialWorldReady && nativeSaveSession != null &&
                    !nativeSaveSession.TryRestorePendingLoadAfterWorldReady(
                        out string restoreFailure))
                {
                    LastGameplayPreparationFailure =
                        "Native save restore failed after static world materialization: " +
                        restoreFailure;
                    yield break;
                }

                // A native restore stages authoritative time and weather. Static
                // world support must exist before mutable physics is applied, but
                // the environment must remain unrevealed until that staged state
                // is ready. This ordering keeps both contracts true.
                if (hasPendingNativeRestore &&
                    startupMode ==
                    ProductionWorldStartupMode.ProductionEnvironmentRequired)
                {
                    yield return environment.InitializeBeforeWorldReveal();
                }

                if (initialWorldReady &&
                    nativeSaveSession != null &&
                    nativeSaveSession.HasRestoredSave)
                {
                    // Player restore can move the streaming focus to a different
                    // cell. Load that cell's static support before the player,
                    // remote items, or vehicle bodies are allowed to simulate.
                    yield return worldStreaming.RefreshNow();
                }

                IsGameplayPrepared = initialWorldReady &&
                    worldStreaming.HasFocus && !worldStreaming.IsStreaming;
                if (!IsGameplayPrepared)
                {
                    LastGameplayPreparationFailure =
                        "Production world preparation completed without a valid streaming focus.";
                    yield break;
                }

                spawnedPlayer.SetActive(true);
                IsReady = IsGameplayPrepared;
                completed = true;
            }
            finally
            {
                nativeSaveSession?.CompletePendingLoadWorldMaterialization();
                IsGameplayPreparationRunning = false;
                if (!completed &&
                    string.IsNullOrWhiteSpace(LastGameplayPreparationFailure))
                {
                    LastGameplayPreparationFailure =
                        "Production world preparation was interrupted.";
                }
            }
        }

        private void ValidateConfiguration()
        {
            if (characterPresentationCatalog == null &&
                characterDefinitionCatalog != null &&
                npcFoundationCatalog != null)
            {
                characterPresentationCatalog =
                    Resources.Load<CharacterPresentationCatalog>(
                        CharacterPresentationResourcePath);
            }

            if (trafficPresentationCatalog == null &&
                trafficRoadNetworkCatalog != null)
            {
                trafficPresentationCatalog =
                    Resources.Load<TrafficPresentationCatalog>(
                        TrafficPresentationResourcePath);
            }

            if (compositionRoot == null)
            {
                throw new InvalidOperationException("Production streaming installer has no composition root.");
            }

            if (worldStreaming == null)
            {
                throw new InvalidOperationException("Production streaming installer has no world streaming service.");
            }

            switch (startupMode)
            {
                case ProductionWorldStartupMode.ProductionEnvironmentRequired:
                    if (!HasCoherentStartupConfiguration ||
                        !environment.IsPrimaryOwner ||
                        audioComposition == null ||
                        itemDefinitionCatalog == null ||
                        itemPlacementCatalog == null ||
                        characterDefinitionCatalog == null ||
                        npcFoundationCatalog == null ||
                        npcDialogueCatalog == null ||
                        characterPresentationCatalog == null ||
                        trafficRoadNetworkCatalog == null ||
                        trafficPresentationCatalog == null ||
                        economyPriceCatalog == null ||
                        !economyPriceCatalog.TryValidate(out _) ||
                        serviceCatalog == null ||
                        !serviceCatalog.TryValidate(out _))
                    {
                        throw new InvalidOperationException(
                            "Production environment startup mode has no coherent active primary owner/backend composition.");
                    }

                    break;

                case ProductionWorldStartupMode.WorldOnlyDevelopment:
                    if (!HasCoherentStartupConfiguration)
                    {
                        throw new InvalidOperationException(
                            "World-only development startup contains an active production environment owner/backend.");
                    }

                    break;

                default:
                    throw new InvalidOperationException(
                        "Production streaming installer has an unsupported startup mode.");
            }

            if (playerPrefab == null)
            {
                throw new InvalidOperationException("Production streaming installer has no player prefab.");
            }

            if ((itemDefinitionCatalog == null) !=
                (itemPlacementCatalog == null))
            {
                throw new InvalidOperationException(
                    "Item definition and placement catalogs must be configured together.");
            }

            if (!IsFinite(playerSpawnPosition) ||
                !IsFinite(playerSpawnEulerAngles))
            {
                throw new InvalidOperationException(
                    "Production streaming installer has a non-finite player " +
                    "spawn transform.");
            }

            if (!float.IsFinite(outOfBoundsMinimumY) ||
                outOfBoundsMinimumY >= playerSpawnPosition.y)
            {
                throw new InvalidOperationException(
                    "Production streaming installer has an invalid " +
                    "out-of-bounds recovery threshold.");
            }

            if (!IsFinite(importantObjectRecoveryOrigin) ||
                importantObjectRecoveryOrigin.y <= outOfBoundsMinimumY)
            {
                throw new InvalidOperationException(
                    "Important-object recovery anchor must be finite and above " +
                    "the out-of-bounds threshold.");
            }

            if (!compositionRoot.IsPrimaryRoot)
            {
                throw new InvalidOperationException(
                    "Production streaming installer belongs to a duplicate composition root.");
            }

            if (compositionRoot.IsInitialized)
            {
                throw new InvalidOperationException("Production streaming installer cannot reuse an initialized composition root.");
            }

            if (worldStreaming.gameObject != compositionRoot.gameObject ||
                gameObject != compositionRoot.gameObject ||
                (startupMode ==
                     ProductionWorldStartupMode.ProductionEnvironmentRequired &&
                 (environment.gameObject != compositionRoot.gameObject ||
                  audioComposition.gameObject != compositionRoot.gameObject)))
            {
                throw new InvalidOperationException(
                    "Composition root, production streaming service, optional production environment/audio, and installer must share the process-lifetime bootstrap object.");
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }
}
