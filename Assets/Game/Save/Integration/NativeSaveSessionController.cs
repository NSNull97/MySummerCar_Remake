using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MSC.Core.Lifecycle;
using MSC.Core.Time;
using MSC.Economy;
using MSC.Home;
using MSC.Interaction.Carrying;
using MSC.Items;
using MSC.Lighting;
using MSC.Needs;
using MSC.NPC;
using MSC.Save.Migration;
using MSC.Services;
using MSC.Traffic;
using MSC.Vehicle;
using MSC.Weather.Production;
using MSC.World.Streaming;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Save.Integration
{
    /// <summary>
    /// Explicit Bootstrap-owned composition for the native save pipeline.
    /// A load handoff is consumed during a fresh Bootstrap Awake, but mutable
    /// physics is restored only after the first streaming refresh has
    /// materialized static support collision.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NativeSaveSessionController : MonoBehaviour,
        IGameSessionLifetime
    {
        public const string DefaultSlotId = "slot-01";
        private const string SaveDirectoryName = "Saves";
        private const string NativeDirectoryName = "Native";
        private const string ImportantRecoveryRetentionOwnerId =
            "native-save.important-recovery-materialization";

        private ProductionWorldStreamingService worldStreaming;
        private ProductionEnvironmentController environment;
        private ItemWorldRuntime itemRuntime;
        private PlayerNeedsRuntime playerNeeds;
        private HomeSystemRuntime homeRuntime;
        private NpcWorldRuntime npcRuntime;
        private TrafficWorldRuntime trafficRuntime;
        private EconomyRuntime economyRuntime;
        private ServiceRuntime serviceRuntime;
        private DeferredStableEntityStore deferredEntities;
        private ItemSaveParticipant itemParticipant;
        private WorldEntitySaveParticipant worldParticipant;
        private VehicleSaveParticipant vehicleParticipant;
        private SatsumaKeyAccessState satsumaKeyAccess;
        private CarrySaveParticipant carryParticipant;
        private SaveCoordinator coordinator;
        private string bootstrapScenePath = string.Empty;
        private int bootstrapSceneBuildIndex = -1;
        private double accumulatedPlayTimeSeconds;
        private double sessionStartRealtimeSeconds;
        private string pendingRestoreSlotId = string.Empty;
        private bool initialized;
        private bool ending;
        private bool importantObjectRecoveryConfigured;
        private bool importantRecoveryCellRetained;
        private Vector3 importantObjectRecoveryOrigin;
        private float importantObjectRecoveryMinimumY = -64f;

        public ISaveService SaveService => coordinator;
        public bool IsInitialized => initialized;
        public bool HasPendingRestore =>
            !string.IsNullOrEmpty(pendingRestoreSlotId);
        public bool HasRestoredSave { get; private set; }
        public string ActiveSlotId { get; private set; } = DefaultSlotId;
        public string LastLoadFailure { get; private set; } = string.Empty;
        public SaveLoadResult LastLoadResult { get; private set; }
        public SaveReadStatus? LastLoadReadStatus => LastLoadResult?.ReadStatus;
        public DeferredStableEntityStore DeferredEntities => deferredEntities;
        public ISatsumaKeyAccess SatsumaKeyAccess => satsumaKeyAccess;

        public void ConfigureImportantObjectRecovery(
            Vector3 recoveryOrigin,
            float minimumWorldY)
        {
            if (initialized ||
                !IsFinite(recoveryOrigin) ||
                !float.IsFinite(minimumWorldY) ||
                minimumWorldY >= recoveryOrigin.y)
            {
                throw new InvalidOperationException(
                    "Important-object recovery must be configured with a finite " +
                    "above-threshold home-front anchor before save initialization.");
            }

            importantObjectRecoveryOrigin = recoveryOrigin;
            importantObjectRecoveryMinimumY = minimumWorldY;
            importantObjectRecoveryConfigured = true;
        }

        public void Initialize(
            GameObject player,
            ProductionEnvironmentController productionEnvironment,
            ProductionWorldStreamingService streaming)
        {
            Initialize(
                player,
                productionEnvironment,
                streaming,
                itemRuntime: null,
                configuredNeeds: null,
                configuredHome: null,
                configuredNpc: null);
        }

        public void Initialize(
            GameObject player,
            ProductionEnvironmentController productionEnvironment,
            ProductionWorldStreamingService streaming,
            ItemWorldRuntime itemRuntime)
        {
            Initialize(
                player,
                productionEnvironment,
                streaming,
                itemRuntime,
                configuredNeeds: null,
                configuredHome: null,
                configuredNpc: null);
        }

        public void Initialize(
            GameObject player,
            ProductionEnvironmentController productionEnvironment,
            ProductionWorldStreamingService streaming,
            ItemWorldRuntime itemRuntime,
            PlayerNeedsRuntime configuredNeeds)
        {
            Initialize(
                player,
                productionEnvironment,
                streaming,
                itemRuntime,
                configuredNeeds,
                configuredHome: null,
                configuredNpc: null);
        }

        public void Initialize(
            GameObject player,
            ProductionEnvironmentController productionEnvironment,
            ProductionWorldStreamingService streaming,
            ItemWorldRuntime itemRuntime,
            PlayerNeedsRuntime configuredNeeds,
            HomeSystemRuntime configuredHome)
        {
            Initialize(
                player,
                productionEnvironment,
                streaming,
                itemRuntime,
                configuredNeeds,
                configuredHome,
                configuredNpc: null);
        }

        public void Initialize(
            GameObject player,
            ProductionEnvironmentController productionEnvironment,
            ProductionWorldStreamingService streaming,
            ItemWorldRuntime itemRuntime,
            PlayerNeedsRuntime configuredNeeds,
            HomeSystemRuntime configuredHome,
            NpcWorldRuntime configuredNpc)
        {
            Initialize(
                player,
                productionEnvironment,
                streaming,
                itemRuntime,
                configuredNeeds,
                configuredHome,
                configuredNpc,
                configuredTraffic: null);
        }

        public void Initialize(
            GameObject player,
            ProductionEnvironmentController productionEnvironment,
            ProductionWorldStreamingService streaming,
            ItemWorldRuntime itemRuntime,
            PlayerNeedsRuntime configuredNeeds,
            HomeSystemRuntime configuredHome,
            NpcWorldRuntime configuredNpc,
            TrafficWorldRuntime configuredTraffic)
        {
            Initialize(
                player,
                productionEnvironment,
                streaming,
                itemRuntime,
                configuredNeeds,
                configuredHome,
                configuredNpc,
                configuredTraffic,
                configuredEconomy: null);
        }

        public void Initialize(
            GameObject player,
            ProductionEnvironmentController productionEnvironment,
            ProductionWorldStreamingService streaming,
            ItemWorldRuntime itemRuntime,
            PlayerNeedsRuntime configuredNeeds,
            HomeSystemRuntime configuredHome,
            NpcWorldRuntime configuredNpc,
            TrafficWorldRuntime configuredTraffic,
            EconomyRuntime configuredEconomy)
        {
            Initialize(
                player,
                productionEnvironment,
                streaming,
                itemRuntime,
                configuredNeeds,
                configuredHome,
                configuredNpc,
                configuredTraffic,
                configuredEconomy,
                configuredServices: null);
        }

        public void Initialize(
            GameObject player,
            ProductionEnvironmentController productionEnvironment,
            ProductionWorldStreamingService streaming,
            ItemWorldRuntime itemRuntime,
            PlayerNeedsRuntime configuredNeeds,
            HomeSystemRuntime configuredHome,
            NpcWorldRuntime configuredNpc,
            TrafficWorldRuntime configuredTraffic,
            EconomyRuntime configuredEconomy,
            ServiceRuntime configuredServices)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Native save session is already initialized.");
            }

            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            environment = productionEnvironment ??
                throw new ArgumentNullException(nameof(productionEnvironment));
            worldStreaming = streaming ??
                throw new ArgumentNullException(nameof(streaming));

            Scene bootstrapScene = SceneManager.GetActiveScene();
            Scene persistentScene = gameObject.scene;
            bootstrapScenePath = bootstrapScene.path;
            bootstrapSceneBuildIndex = bootstrapScene.buildIndex;
            deferredEntities = new DeferredStableEntityStore();
            satsumaKeyAccess = new SatsumaKeyAccessState();
            PhysicalCarryController physicalCarry =
                player.GetComponentInChildren<PhysicalCarryController>(true) ??
                throw new InvalidOperationException(
                    "Production player has no PhysicalCarryController save boundary.");
            ImportantObjectRecoveryFormation recoveryFormation =
                importantObjectRecoveryConfigured
                    ? new ImportantObjectRecoveryFormation(
                        importantObjectRecoveryOrigin)
                    : null;
            worldParticipant = new WorldEntitySaveParticipant(
                deferredEntities,
                worldStreaming,
                persistentScene,
                physicalCarry,
                recoveryFormation,
                importantObjectRecoveryMinimumY);
            this.itemRuntime = itemRuntime;
            playerNeeds = configuredNeeds;
            homeRuntime = configuredHome;
            npcRuntime = configuredNpc;
            trafficRuntime = configuredTraffic;
            economyRuntime = configuredEconomy;
            serviceRuntime = configuredServices;
            if (serviceRuntime != null && economyRuntime == null)
            {
                throw new InvalidOperationException(
                    "Services save state requires the economy save domain.");
            }
            itemParticipant = this.itemRuntime != null
                ? new ItemSaveParticipant(this.itemRuntime, deferredEntities)
                : null;
            vehicleParticipant = new VehicleSaveParticipant(
                deferredEntities,
                worldStreaming,
                recoveryFormation,
                importantObjectRecoveryMinimumY,
                satsumaKeyAccess,
                this.itemRuntime);
            carryParticipant = new CarrySaveParticipant(
                player,
                worldParticipant,
                deferredEntities);
            var environmentRestoreBridge =
                new ProductionEnvironmentRestoreBridge(environment);
            var participants = new List<ISaveParticipant>
            {
                new SatsumaKeyAccessSaveParticipant(satsumaKeyAccess),
                new CoreTimeSaveParticipant(
                    environment,
                    environmentRestoreBridge),
                new WeatherEnvironmentSaveParticipant(
                    environment,
                    environmentRestoreBridge),
            };
            if (itemParticipant != null)
            {
                participants.Add(itemParticipant);
            }

            if (playerNeeds != null)
            {
                participants.Add(new PlayerNeedsSaveParticipant(playerNeeds));
            }

            if (homeRuntime != null)
            {
                participants.Add(new HomeSaveParticipant(homeRuntime));
            }

            if (LightingSessionAuthority.ActiveGrid != null)
            {
                participants.Add(new LightingSaveParticipant(
                    LightingSessionAuthority.ActiveGrid));
            }

            if (economyRuntime != null)
            {
                participants.Add(
                    new EconomySaveParticipant(
                        economyRuntime,
                        environmentRestoreBridge));
            }

            if (serviceRuntime != null)
            {
                participants.Add(
                    new ServicesSaveParticipant(
                        serviceRuntime,
                        environmentRestoreBridge));
            }

            if (npcRuntime != null)
            {
                participants.Add(new NpcSaveParticipant(npcRuntime));
                participants.Add(
                    new StoryTrafficSaveParticipant(
                        npcRuntime,
                        trafficRuntime));
            }

            participants.Add(worldParticipant);
            participants.Add(vehicleParticipant);
            participants.Add(new PlayerSaveParticipant(player));
            participants.Add(carryParticipant);
            VehicleItemSaveRestorePlanFactory itemAssemblyRestore = null;
            if (this.itemRuntime != null)
            {
                worldParticipant.ConfigureExternalOwnership(this.itemRuntime.IsExternallyOwned);
                itemAssemblyRestore = new VehicleItemSaveRestorePlanFactory(
                    this.itemRuntime, vehicleParticipant, worldParticipant, deferredEntities,
                    itemParticipant.NormalizeLegacyFoodState);
                itemParticipant.ConfigureAssemblyOwnerAvailability(itemAssemblyRestore.IsOwnerUnavailable);
            }
            var participantRegistry = new SaveParticipantRegistry(participants, itemAssemblyRestore);
            string saveRoot = Path.Combine(
                Application.persistentDataPath,
                SaveDirectoryName,
                NativeDirectoryName);
            coordinator = new SaveCoordinator(
                new FileSystemSaveStorage(saveRoot),
                participantRegistry,
                new SaveMigrationPipeline(new ISaveDocumentMigration[]
                {
                    new Milestone09BSaveMigration(),
                    new Milestone09CNeedsSaveMigration(),
                    new Milestone09CDelayedNeedsSaveMigration(),
                    new Milestone09CLifeActionsSaveMigration(),
                    new Milestone09CHomeSaveMigration(),
                    new Milestone09CSaunaControlsSaveMigration(),
                    new Milestone10ANpcSaveMigration(
                        npcRuntime != null
                            ? npcRuntime.CaptureDto()
                            : new NpcStateDto(),
                        domainRequired: npcRuntime != null),
                    new Milestone10BR1NpcSaveMigration(
                        npcRuntime != null
                            ? npcRuntime.CaptureDto()
                            : new NpcStateDto()),
                    new WorldItemPhysicsSaveMigration(),
                    new HelmetPaintItemStateSaveMigration(),
                    new Milestone10BR2NpcSaveMigration(
                        npcRuntime != null
                            ? npcRuntime.CaptureDto()
                            : new NpcStateDto()),
                    new Milestone10BR3NpcSaveMigration(
                        npcRuntime != null
                            ? npcRuntime.CaptureDto()
                            : new NpcStateDto()),
                    new Milestone12AEconomySaveMigration(
                        economyRuntime != null
                            ? economyRuntime.CaptureDto()
                            : null,
                        domainRequired: economyRuntime != null),
                    new Milestone12AServicesSaveMigration(
                        serviceRuntime != null
                            ? serviceRuntime.CaptureDto()
                            : null,
                        domainRequired: serviceRuntime != null),
                    new VehicleJackItemStateSaveMigration(),
                    new SatsumaKeyAccessSaveMigration(),
                    new SatsumaDynamicAssemblySaveMigration(),
                }),
                deferredEntities);
            coordinator.OperationCompleted += HandleOperationCompleted;
            if (this.itemRuntime != null)
            {
                this.itemRuntime.InstanceMaterialized += HandleItemMaterialized;
                this.itemRuntime.InstanceRemoved += HandleItemRemoved;
            }
            worldStreaming.OwnedSceneLoaded += HandleOwnedSceneLoaded;
            worldStreaming.OwnedSceneWillUnload += HandleOwnedSceneWillUnload;

            RegisterCurrentlyLoadedScenes();
            vehicleParticipant.GuardPersistentVehiclesUntilWorldReady();
            sessionStartRealtimeSeconds = Time.realtimeSinceStartupAsDouble;
            initialized = true;
            if (NativeSaveLoadHandoff.TryConsume(out string pendingSlotId))
            {
                pendingRestoreSlotId = pendingSlotId;
            }
        }

        public bool TryRestorePendingLoadAfterWorldReady(out string failure)
        {
            RequireInitialized();
            if (!worldStreaming.HasFocus || worldStreaming.IsStreaming)
            {
                failure =
                    "Native load cannot release physics before world streaming is ready.";
                return false;
            }

            if (string.IsNullOrEmpty(pendingRestoreSlotId))
            {
                vehicleParticipant.ReleasePersistentVehicleStartupGuard();
                failure = string.Empty;
                return true;
            }

            if (importantObjectRecoveryConfigured)
            {
                if (!worldStreaming.TryGetCellIdForPosition(
                        importantObjectRecoveryOrigin,
                        out string recoveryCellId))
                {
                    failure =
                        "Important-object recovery anchor is outside the world manifest.";
                    return false;
                }

                worldStreaming.RetainCell(
                    ImportantRecoveryRetentionOwnerId,
                    recoveryCellId);
                importantRecoveryCellRetained = true;
            }

            vehicleParticipant.ReleasePersistentVehicleStartupGuard();
            string slotId = pendingRestoreSlotId;
            pendingRestoreSlotId = string.Empty;
            TryRestorePendingSlot(slotId);
            failure = LastLoadFailure;
            if (!HasRestoredSave)
            {
                CompletePendingLoadWorldMaterialization();
            }

            return HasRestoredSave;
        }

        public void CompletePendingLoadWorldMaterialization()
        {
            if (!importantRecoveryCellRetained)
            {
                return;
            }

            worldStreaming?.ReleaseCellRetention(
                ImportantRecoveryRetentionOwnerId);
            importantRecoveryCellRetained = false;
        }

        public SaveRequest CreateSaveRequest(string slotId)
        {
            RequireInitialized();
            SaveSlotId.Validate(slotId);
            GameTimeSnapshot clock = environment.GameTime.Snapshot;
            TimeSpan timeOfDay = TimeSpan.FromSeconds(clock.SecondsOfDay);
            return new SaveRequest
            {
                SlotId = slotId,
                BuildId = CreateBuildId(),
                Metadata = new SaveMetadata
                {
                    DisplayName = "Сохранение " + slotId,
                    PlayTimeSeconds = accumulatedPlayTimeSeconds + Math.Max(
                        0d,
                        Time.realtimeSinceStartupAsDouble -
                        sessionStartRealtimeSeconds),
                    GameTimestamp = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0} {1:00}:{2:00}",
                        clock.Date,
                        (int)timeOfDay.TotalHours,
                        timeOfDay.Minutes),
                    LocationStableId = string.Empty,
                },
            };
        }

        public bool RequestLoad(string slotId, out string failure)
        {
            try
            {
                RequireInitialized();
                SaveSlotId.Validate(slotId);
                SaveSlotSummary summary = coordinator.EnumerateSlots()
                    .FirstOrDefault(candidate => string.Equals(
                        candidate.SlotId,
                        slotId,
                        StringComparison.Ordinal));
                if (summary == null || !summary.IsValid)
                {
                    failure = summary?.Message ??
                        $"Слот '{slotId}' не содержит корректного сохранения.";
                    return false;
                }

                if (!NativeSaveLoadHandoff.TryQueue(slotId, out failure))
                {
                    return false;
                }

                AsyncOperation reload = bootstrapSceneBuildIndex >= 0
                    ? SceneManager.LoadSceneAsync(
                        bootstrapSceneBuildIndex,
                        LoadSceneMode.Single)
                    : SceneManager.LoadSceneAsync(
                        bootstrapScenePath,
                        LoadSceneMode.Single);
                if (reload == null)
                {
                    NativeSaveLoadHandoff.Clear(slotId);
                    failure = "Не удалось запустить чистую перезагрузку Bootstrap.";
                    return false;
                }

                failure = string.Empty;
                return true;
            }
            catch (Exception exception)
            {
                NativeSaveLoadHandoff.Clear(slotId);
                failure = exception.Message;
                return false;
            }
        }

        public void EndGameSession()
        {
            if (ending)
            {
                return;
            }

            ending = true;
            CompletePendingLoadWorldMaterialization();
            if (coordinator != null)
            {
                coordinator.OperationCompleted -= HandleOperationCompleted;
            }

            if (worldStreaming != null)
            {
                worldStreaming.OwnedSceneLoaded -= HandleOwnedSceneLoaded;
                worldStreaming.OwnedSceneWillUnload -= HandleOwnedSceneWillUnload;
            }

            if (itemRuntime != null)
            {
                itemRuntime.InstanceMaterialized -= HandleItemMaterialized;
                itemRuntime.InstanceRemoved -= HandleItemRemoved;
            }
        }

        private void OnDestroy()
        {
            EndGameSession();
        }

        private void TryRestorePendingSlot(string slotId)
        {
            try
            {
                LastLoadResult = coordinator.Load(slotId);
                ActiveSlotId = slotId;
                accumulatedPlayTimeSeconds = Math.Max(
                    0d,
                    LastLoadResult.Document.Metadata?.PlayTimeSeconds ?? 0d);
                sessionStartRealtimeSeconds =
                    Time.realtimeSinceStartupAsDouble;
                HasRestoredSave = true;
                LastLoadFailure = string.Empty;
            }
            catch (Exception exception)
            {
                HasRestoredSave = false;
                LastLoadResult = null;
                LastLoadFailure = exception.Message;
                Debug.LogError(
                    $"Native save slot '{slotId}' could not be restored: {exception}",
                    this);
            }
        }

        private void RegisterCurrentlyLoadedScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded)
                {
                    WorldEntitySaveParticipant.SceneRegistrationBatch batch =
                        worldParticipant.BeginRegisterScene(scene);
                    itemParticipant?.RegisterScene(scene);
                    vehicleParticipant.RegisterScene(scene);
                    WorldEntitySaveParticipant.CompleteRegisterScene(batch);
                    vehicleParticipant.CompleteSceneLoad(scene);
                }
            }

            // GameCompositionRoot is moved into Unity's hidden
            // DontDestroyOnLoad scene before production content is spawned.
            // That scene is not returned by SceneManager.sceneCount, so the
            // production Satsuma must also be discovered from the persistent
            // composition hierarchy. RegisterBinding is idempotent for the
            // same object, which keeps this safe in development compositions
            // that have not been moved yet.
            vehicleParticipant.RegisterHierarchy(transform.root.gameObject);
        }

        private void HandleOwnedSceneLoaded(Scene scene)
        {
            WorldEntitySaveParticipant.SceneRegistrationBatch batch =
                worldParticipant.BeginRegisterScene(scene);
            itemParticipant?.RegisterScene(scene);
            vehicleParticipant.RegisterScene(scene);
            WorldEntitySaveParticipant.CompleteRegisterScene(batch);
            vehicleParticipant.CompleteSceneLoad(scene);
            carryParticipant.ApplyDeferredAfterSceneLoad();
        }

        private void HandleOwnedSceneWillUnload(Scene scene)
        {
            itemParticipant?.CaptureScene(scene);
            worldParticipant.CaptureAndUnregisterScene(scene);
            itemParticipant?.ReconcileAfterSceneUnload(scene);
            vehicleParticipant.CaptureAndUnregisterScene(scene);
        }

        private void HandleItemMaterialized(WorldItemInstance instance)
        {
            PhysicsPickupTarget target =
                instance != null
                    ? instance.GetComponent<PhysicsPickupTarget>()
                    : null;
            // Some canonical donor items are intentionally static and have no
            // Rigidbody/pickup capability. Their transform and logical state
            // are still owned by ItemSaveParticipant; only physical pickup
            // targets belong in the world-entity physics domain.
            if (target != null)
            {
                worldParticipant.RegisterTarget(target);
            }
        }

        private void HandleItemRemoved(WorldItemInstance instance)
        {
            PhysicsPickupTarget target =
                instance != null
                    ? instance.GetComponent<PhysicsPickupTarget>()
                    : null;
            worldParticipant.UnregisterTarget(target);
        }

        private void HandleOperationCompleted(
            object sender,
            SaveOperationEventArgs eventArgs)
        {
            if (eventArgs.Operation == SaveOperationKind.Save &&
                !string.IsNullOrEmpty(eventArgs.SlotId))
            {
                ActiveSlotId = eventArgs.SlotId;
            }
        }

        private static string CreateBuildId()
        {
            string playerBuild = string.IsNullOrWhiteSpace(Application.buildGUID)
                ? "editor"
                : Application.buildGUID;
            return $"{Application.version}|unity-{Application.unityVersion}|{playerBuild}";
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private void RequireInitialized()
        {
            if (!initialized || coordinator == null)
            {
                throw new InvalidOperationException(
                    "Native save session is not initialized.");
            }
        }
    }

    internal static class NativeSaveLoadHandoff
    {
        private static readonly object Sync = new object();
        private static string pendingSlotId = string.Empty;

        public static bool TryQueue(string slotId, out string failure)
        {
            SaveSlotId.Validate(slotId);
            lock (Sync)
            {
                if (!string.IsNullOrEmpty(pendingSlotId))
                {
                    failure = "Запрос загрузки уже выполняется.";
                    return false;
                }

                pendingSlotId = slotId;
                failure = string.Empty;
                return true;
            }
        }

        public static bool TryConsume(out string slotId)
        {
            lock (Sync)
            {
                slotId = pendingSlotId;
                pendingSlotId = string.Empty;
                return !string.IsNullOrEmpty(slotId);
            }
        }

        public static void Clear(string slotId)
        {
            lock (Sync)
            {
                if (string.IsNullOrEmpty(slotId) ||
                    string.Equals(
                        pendingSlotId,
                        slotId,
                        StringComparison.Ordinal))
                {
                    pendingSlotId = string.Empty;
                }
            }
        }
    }
}
