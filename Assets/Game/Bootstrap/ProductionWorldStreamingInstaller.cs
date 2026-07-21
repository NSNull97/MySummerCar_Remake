using System;
using System.Collections;
using MSC.Audio.Composition;
using MSC.Core.Lifecycle;
using MSC.Weather.Production;
using MSC.Weather.Wetness;
using MSC.World.Streaming;
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
        IGameplaySessionGate
    {
        private const int MaximumEnvironmentBackendStartupFrames = 16;

        [SerializeField] private GameCompositionRoot compositionRoot;
        [SerializeField] private ProductionWorldStreamingService worldStreaming;
        [SerializeField] private ProductionEnvironmentController environment;
        [SerializeField] private ProductionAudioComposition audioComposition;
        [SerializeField] private ProductionWorldStartupMode startupMode =
            ProductionWorldStartupMode.ProductionEnvironmentRequired;
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Vector3 playerSpawnPosition;
        [SerializeField] private Vector3 playerSpawnEulerAngles;
        [SerializeField] private float outOfBoundsMinimumY = -64f;

        private GameObject spawnedPlayer;
        private Camera spawnedPlayerCamera;
        private bool spawnedPlayerCameraInitiallyEnabled;
        private WorldOutOfBoundsRecovery outOfBoundsRecovery;
        private bool deferGameplayActivation;
        private bool startupRoutineStarted;

        public GameCompositionRoot CompositionRoot => compositionRoot;
        public ProductionWorldStreamingService WorldStreaming => worldStreaming;
        public ProductionEnvironmentController Environment => environment;
        public ProductionAudioComposition AudioComposition => audioComposition;
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
        public float OutOfBoundsMinimumY => outOfBoundsMinimumY;
        public bool IsReady { get; private set; }
        public bool IsGameplayPrepared { get; private set; }
        public bool IsGameplayActive { get; private set; }

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

            worldStreaming.BindFocus(spawnedPlayer.transform);
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

            compositionRoot.Initialize(startupMode ==
                ProductionWorldStartupMode.ProductionEnvironmentRequired
                ? GameServiceBindings.CreateProductionEnvironmentAudioPartial(
                    environment.GameTime,
                    environment.Weather,
                    audioComposition.Backend,
                    worldStreaming)
                : GameServiceBindings.CreateWorldStreamingPartial(
                    worldStreaming));
        }

        private IEnumerator Start()
        {
            startupRoutineStarted = true;
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
                    throw new InvalidOperationException(
                        "Production environment backend did not activate before " +
                        "the startup deadline.");
                }

                yield return environment.InitializeBeforeWorldReveal();
            }

            yield return worldStreaming.RefreshNow();
            IsGameplayPrepared =
                worldStreaming.HasFocus && !worldStreaming.IsStreaming;
            if (!IsGameplayPrepared)
            {
                throw new InvalidOperationException(
                    "Production world preparation completed without a valid streaming focus.");
            }

            spawnedPlayer.SetActive(true);
            if (!deferGameplayActivation &&
                !TryActivateGameplay(out string activationFailure))
            {
                throw new InvalidOperationException(activationFailure);
            }

            IsReady = IsGameplayPrepared;
        }

        private void ValidateConfiguration()
        {
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
                        audioComposition == null)
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
