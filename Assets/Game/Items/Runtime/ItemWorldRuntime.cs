using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Core.Time;
using MSC.Interaction;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Items.Presentation;
using MSC.World.Streaming;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Items
{
    [DisallowMultipleComponent]
    public sealed class ItemWorldRuntime : MonoBehaviour
    {
        public const int WorldItemCollisionLayer = 8;
        private readonly Dictionary<string, WorldItemInstance> instances =
            new Dictionary<string, WorldItemInstance>(StringComparer.Ordinal);
        private readonly Dictionary<string, ItemPlacementRecord> placementsById =
            new Dictionary<string, ItemPlacementRecord>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> sourceCellIds =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Pose> recoveryPoses =
            new Dictionary<string, Pose>(StringComparer.Ordinal);
        private readonly HashSet<IItemHeatSource> heatSources =
            new HashSet<IItemHeatSource>();
        private readonly List<WorldItemInstance> recoveryIterationBuffer =
            new List<WorldItemInstance>(64);
        private readonly List<WorldItemInstance> foodIterationBuffer =
            new List<WorldItemInstance>(64);
        private readonly List<WorldItemInstance> combustionIterationBuffer =
            new List<WorldItemInstance>(64);
        private readonly List<WorldItemInstance> cookingIterationBuffer =
            new List<WorldItemInstance>(64);
        private readonly List<IItemHeatSource> heatSourceIterationBuffer =
            new List<IItemHeatSource>(8);

        private ItemDefinitionCatalog definitions;
        private ItemPlacementCatalog placements;
        private ProductionWorldStreamingService worldStreaming;
        private Scene persistentScene;
        private IItemPresentationProvider presentationProvider;
        private PhysicsMaterial livelyItemMaterial;
        private PhysicsMaterial ballItemMaterial;
        private float recoveryMinimumY = -64f;
        private Vector3 importantRecoveryOrigin;
        private bool hasImportantRecoveryOrigin;
        private float nextRecoveryCheckTime;
        private IGameTimeService gameTime;
        private IDisposable gameTimeSubscription;
        private IItemFoodEnvironment foodEnvironment;
        private bool initialized;

        public event Action<ItemActionCompleted> ActionCompleted;
        public event Action<ItemHeldUseStateChanged> HeldUseStateChanged;
        public event Action<WorldItemInstance> InstanceMaterialized;
        public event Action<WorldItemInstance> InstanceRemoved;
        public event Action<WorldItemInstance, GameObject> PresentationAttached;

        public ItemDefinitionCatalog Definitions => definitions;
        public ItemPlacementCatalog Placements => placements;
        /// <summary>
        /// Live, allocation-free view of materialized instances. Callers that
        /// remove items while iterating must take an explicit snapshot first.
        /// </summary>
        public IReadOnlyCollection<WorldItemInstance> LoadedInstances =>
            instances.Values;
        public bool IsInitialized => initialized;
        public double CurrentGameTimeSeconds =>
            gameTime?.CurrentGameTimeSeconds ?? 0d;

        public void Initialize(
            ItemDefinitionCatalog definitionCatalog,
            ItemPlacementCatalog placementCatalog,
            ProductionWorldStreamingService streaming,
            Scene configuredPersistentScene,
            float outOfBoundsMinimumY,
            IGameTimeService configuredGameTime = null,
            Vector3? configuredImportantRecoveryOrigin = null)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Item world runtime is already initialized.");
            }

            definitions = definitionCatalog ??
                throw new ArgumentNullException(nameof(definitionCatalog));
            placements = placementCatalog ??
                throw new ArgumentNullException(nameof(placementCatalog));
            worldStreaming = streaming ??
                throw new ArgumentNullException(nameof(streaming));
            persistentScene = configuredPersistentScene;
            gameTime = configuredGameTime;
            recoveryMinimumY = outOfBoundsMinimumY;
            hasImportantRecoveryOrigin =
                configuredImportantRecoveryOrigin.HasValue;
            importantRecoveryOrigin =
                configuredImportantRecoveryOrigin.GetValueOrDefault();
            if (!persistentScene.IsValid() || !persistentScene.isLoaded ||
                !float.IsFinite(recoveryMinimumY) ||
                hasImportantRecoveryOrigin &&
                !IsFinite(importantRecoveryOrigin))
            {
                throw new InvalidOperationException(
                    "Item runtime persistent scene or recovery threshold is invalid.");
            }

            IReadOnlyList<string> definitionFailures =
                definitions.ValidateConfiguration();
            IReadOnlyList<string> placementFailures =
                placements.ValidateConfiguration(definitions);
            if (definitionFailures.Count > 0 || placementFailures.Count > 0)
            {
                throw new InvalidOperationException(
                    "Item runtime catalogs are invalid: " +
                    string.Join(" | ", definitionFailures.Concat(placementFailures)));
            }

            foreach (ItemPlacementRecord placement in placements.Placements)
            {
                placementsById.Add(placement.StableEntityId, placement);
                recoveryPoses.Add(
                    placement.StableEntityId,
                    new Pose(placement.WorldPosition, placement.WorldRotation));
                if (worldStreaming.TryGetCellIdForPosition(
                        placement.WorldPosition,
                        out string cellId))
                {
                    sourceCellIds.Add(placement.StableEntityId, cellId);
                }
            }

            presentationProvider = ItemPresentationProviderHub.Current;
            ItemPresentationProviderHub.ProviderChanged +=
                HandlePresentationProviderChanged;
            worldStreaming.OwnedSceneLoaded += HandleOwnedSceneLoaded;
            initialized = true;
            if (gameTime != null)
            {
                gameTimeSubscription = gameTime.Subscribe(HandleGameTimeEvent);
            }
            RegisterAlreadyLoadedCells();
            SpawnPlacementsWithoutOwnedCell();
        }

        public void SetFoodEnvironment(IItemFoodEnvironment environment)
        {
            foodEnvironment = environment;
        }

        public void RegisterHeatSource(IItemHeatSource source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            heatSources.Add(source);
        }

        public void UnregisterHeatSource(IItemHeatSource source)
        {
            if (source != null)
            {
                heatSources.Remove(source);
            }
        }

        internal void ReconcileFoodAfterRestore(
            WorldItemInstance instance,
            Vector3 restoredWorldPosition)
        {
            ItemInstanceState state = instance?.State;
            if (instance == null || state == null ||
                !instance.Definition.Food.Perishable ||
                !state.foodSimulationInitialized)
            {
                return;
            }

            double now = CurrentGameTimeSeconds;
            double elapsed = now - state.lastFoodSimulationGameSeconds;
            if (elapsed <= 0d)
            {
                return;
            }

            bool refrigerated = foodEnvironment != null &&
                foodEnvironment.IsRefrigerated(restoredWorldPosition);
            instance.AdvanceFreshness(
                elapsed,
                refrigerated,
                now,
                publish: false);
        }

        public bool TryGetInstance(
            string stableEntityId,
            out WorldItemInstance instance)
        {
            if (instances.TryGetValue(
                    stableEntityId ?? string.Empty,
                    out instance) &&
                instance != null)
            {
                return true;
            }

            instances.Remove(stableEntityId ?? string.Empty);
            instance = null;
            return false;
        }

        public bool TryGetSourceCellId(
            string stableEntityId,
            out string sourceCellId) =>
            sourceCellIds.TryGetValue(
                stableEntityId ?? string.Empty,
                out sourceCellId);

        public bool IsCanonicalPlacement(string stableEntityId) =>
            placementsById.ContainsKey(stableEntityId ?? string.Empty);

        public bool TryGetCellIdForScene(Scene scene, out string sourceCellId)
        {
            sourceCellId = string.Empty;
            return initialized && scene.IsValid() && scene.isLoaded &&
                   worldStreaming.TryGetCellIdForScene(scene, out sourceCellId);
        }

        public bool IsPositionInCell(Vector3 worldPosition, string cellId)
        {
            return initialized && !string.IsNullOrEmpty(cellId) &&
                   worldStreaming.TryGetCellIdForPosition(
                       worldPosition,
                       out string resolvedCellId) &&
                   string.Equals(
                       resolvedCellId,
                       cellId,
                       StringComparison.Ordinal);
        }

        public bool TryGetLoadedCellScene(string cellId, out Scene scene)
        {
            if (initialized && !string.IsNullOrEmpty(cellId) &&
                worldStreaming.IsCellLoaded(cellId))
            {
                for (int index = 0; index < SceneManager.sceneCount; index++)
                {
                    Scene candidate = SceneManager.GetSceneAt(index);
                    if (candidate.IsValid() && candidate.isLoaded &&
                        worldStreaming.TryGetCellIdForScene(
                            candidate,
                            out string candidateCellId) &&
                        string.Equals(
                            candidateCellId,
                            cellId,
                            StringComparison.Ordinal))
                    {
                        scene = candidate;
                        return true;
                    }
                }
            }

            scene = default;
            return false;
        }

        public WorldItemInstance SpawnDynamic(
            string definitionId,
            StableEntityId stableId,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Scene targetScene,
            string sourceCellId = "",
            int variantIndex = 0)
        {
            if (!initialized ||
                !stableId.IsValid ||
                TryGetInstance(stableId.Value, out _) ||
                !definitions.TryGet(definitionId, out ItemDefinitionRecord definition) ||
                !targetScene.IsValid() || !targetScene.isLoaded)
            {
                throw new InvalidOperationException(
                    "Dynamic item materialization request is invalid.");
            }

            WorldItemInstance instance = CreateInstance(
                definition,
                stableId,
                worldPosition,
                worldRotation,
                targetScene,
                variantIndex,
                null);
            if (!string.IsNullOrEmpty(sourceCellId))
            {
                sourceCellIds[stableId.Value] = sourceCellId;
            }
            else if (worldStreaming.TryGetCellIdForPosition(
                         worldPosition,
                         out string resolvedCellId))
            {
                sourceCellIds[stableId.Value] = resolvedCellId;
            }

            recoveryPoses[stableId.Value] =
                new Pose(worldPosition, worldRotation);
            return instance;
        }

        public bool TrySettleDynamicOnHorizontalSurface(
            WorldItemInstance instance,
            float surfaceWorldY,
            float clearance = 0.005f)
        {
            if (!initialized ||
                instance == null ||
                !float.IsFinite(surfaceWorldY) ||
                !float.IsFinite(clearance) ||
                !instances.TryGetValue(
                    instance.StableId.Value,
                    out WorldItemInstance registered) ||
                registered != instance ||
                IsCanonicalPlacement(instance.StableId.Value))
            {
                return false;
            }

            Collider collider = instance.GetComponent<Collider>();
            Rigidbody body = instance.GetComponent<Rigidbody>();
            if (collider == null || body == null)
            {
                return false;
            }

            float targetBottom = surfaceWorldY + Mathf.Max(0f, clearance);
            Vector3 settledPosition = body.position;
            settledPosition.y += targetBottom - collider.bounds.min.y;
            body.position = settledPosition;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
            recoveryPoses[instance.StableId.Value] = new Pose(
                settledPosition,
                instance.transform.rotation);
            return true;
        }

        public WorldItemInstance MaterializeForRestore(
            ItemRuntimeSaveRecord record,
            Scene targetScene = default)
        {
            string failure = string.Empty;
            if (record == null || !record.TryValidate(definitions, out failure))
            {
                throw new InvalidOperationException(
                    "Cannot materialize saved item: " + failure);
            }

            if (TryGetInstance(record.state.stableEntityId, out WorldItemInstance existing))
            {
                return existing;
            }

            if (record.isCanonicalPlacement)
            {
                return null;
            }

            if (!StableEntityId.TryParse(
                    record.state.stableEntityId,
                    out StableEntityId stableId))
            {
                throw new InvalidOperationException(
                    "Saved item has no valid stable identity.");
            }

            Scene materializationScene = targetScene.IsValid() && targetScene.isLoaded
                ? targetScene
                : persistentScene;
            return SpawnDynamic(
                record.state.definitionId,
                stableId,
                record.materializationPosition,
                record.materializationRotation,
                materializationScene,
                record.sourceCellId,
                record.state.variantIndex);
        }

        public void RemoveDynamicInstancesExcept(
            ISet<string> retainedStableIds)
        {
            retainedStableIds ??= new HashSet<string>(StringComparer.Ordinal);
            WorldItemInstance[] remove = instances.Values
                .Where(instance => instance != null &&
                                   !IsCanonicalPlacement(instance.StableId.Value) &&
                                   !retainedStableIds.Contains(instance.StableId.Value))
                .ToArray();
            foreach (WorldItemInstance instance in remove)
            {
                NotifyDestroyed(instance);
                sourceCellIds.Remove(instance.StableId.Value);
                recoveryPoses.Remove(instance.StableId.Value);
                instance.gameObject.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(instance.gameObject);
                }
                else
                {
                    DestroyImmediate(instance.gameObject);
                }
            }
        }

        public bool TryRemoveDynamic(WorldItemInstance instance)
        {
            if (!initialized || instance == null ||
                IsCanonicalPlacement(instance.StableId.Value) ||
                !instances.TryGetValue(
                    instance.StableId.Value,
                    out WorldItemInstance registered) ||
                registered != instance)
            {
                return false;
            }

            NotifyDestroyed(instance);
            sourceCellIds.Remove(instance.StableId.Value);
            recoveryPoses.Remove(instance.StableId.Value);
            instance.gameObject.SetActive(false);
            if (Application.isPlaying)
            {
                Destroy(instance.gameObject);
            }
            else
            {
                DestroyImmediate(instance.gameObject);
            }

            return true;
        }

        internal bool TryConsume(WorldItemInstance instance)
        {
            if (instance == null || !instance.TryConsumeContent(out _))
            {
                return false;
            }

            return true;
        }

        internal bool TryDispenseChild(
            WorldItemInstance container,
            in InteractionContext context)
        {
            if (container == null ||
                string.IsNullOrEmpty(container.Definition.ChildDefinitionId) ||
                !container.TryPeekContainedIdentity(out StableEntityId childId) ||
                TryGetInstance(childId.Value, out _) ||
                !definitions.TryGet(
                    container.Definition.ChildDefinitionId,
                    out _))
            {
                return false;
            }

            if (!container.TryTakeContainedIdentity(
                    out StableEntityId takenChildId) ||
                takenChildId != childId)
            {
                throw new InvalidOperationException(
                    "Contained item identity changed during dispense preflight.");
            }

            Rigidbody sourceBody = container.GetComponent<Rigidbody>();
            Collider sourceCollider = container.GetComponent<Collider>();
            Vector3 direction = context.Direction.sqrMagnitude > 0.0001f
                ? context.Direction.normalized
                : container.transform.forward;
            Bounds sourceBounds = sourceCollider != null
                ? sourceCollider.bounds
                : new Bounds(
                    sourceBody != null
                        ? sourceBody.position
                        : container.transform.position,
                    container.Definition.ProxySize);
            float projectedExtent =
                Mathf.Abs(direction.x) * sourceBounds.extents.x +
                Mathf.Abs(direction.y) * sourceBounds.extents.y +
                Mathf.Abs(direction.z) * sourceBounds.extents.z;
            Vector3 position = sourceBounds.center +
                direction * (projectedExtent + 0.13f) +
                Vector3.up * 0.03f;
            Scene scene = container.gameObject.scene;
            string sourceCellId = TryGetSourceCellId(
                    container.StableId.Value,
                    out string resolvedCellId)
                ? resolvedCellId
                : string.Empty;
            WorldItemInstance child = SpawnDynamic(
                container.Definition.ChildDefinitionId,
                childId,
                position,
                container.transform.rotation,
                scene,
                sourceCellId);
            child.InheritFoodStateFrom(container);
            return true;
        }

        internal bool TryConsumeContainedChild(
            WorldItemInstance container,
            in InteractionContext context)
        {
            if (container == null ||
                string.IsNullOrEmpty(container.Definition.ChildDefinitionId) ||
                !container.TryPeekContainedIdentity(out StableEntityId childId) ||
                TryGetInstance(childId.Value, out _) ||
                !definitions.TryGet(
                    container.Definition.ChildDefinitionId,
                    out ItemDefinitionRecord childDefinition) ||
                childDefinition.PrimaryAction != ItemPrimaryAction.Consume ||
                childDefinition.InitialContent <= 0f ||
                !childDefinition.RetainWhenEmpty)
            {
                return false;
            }

            if (!container.TryTakeContainedIdentity(
                    out StableEntityId takenChildId) ||
                takenChildId != childId)
            {
                throw new InvalidOperationException(
                    "Contained consumable identity changed during use preflight.");
            }

            Vector3 direction = context.Direction.sqrMagnitude > 0.0001f
                ? context.Direction.normalized
                : Vector3.forward;
            Vector3 position = context.Origin + direction * 0.65f -
                               Vector3.up * 0.12f;
            Scene scene = container.gameObject.scene;
            string sourceCellId = TryGetSourceCellId(
                    container.StableId.Value,
                    out string resolvedCellId)
                ? resolvedCellId
                : string.Empty;
            WorldItemInstance emptyContainer = SpawnDynamic(
                childDefinition.DefinitionId,
                childId,
                position,
                Quaternion.LookRotation(direction, Vector3.up),
                scene,
                sourceCellId);
            if (!emptyContainer.TryConsumeContent(out bool depleted) || !depleted)
            {
                throw new InvalidOperationException(
                    "Contained consumable did not reach its authored empty state.");
            }

            Rigidbody body = emptyContainer.GetComponent<Rigidbody>();
            if (body != null)
            {
                Vector3 throwDirection =
                    (direction + Vector3.up * 0.18f).normalized;
                body.AddForce(throwDirection * 2.5f, ForceMode.Impulse);
                body.AddTorque(
                    new Vector3(0.35f, 0.7f, -0.25f),
                    ForceMode.Impulse);
            }

            return true;
        }

        internal void NotifyDestroyed(WorldItemInstance instance)
        {
            if (instance == null)
            {
                return;
            }

            string id = instance.StableId.Value;
            if (instances.TryGetValue(id, out WorldItemInstance existing) &&
                existing == instance)
            {
                instances.Remove(id);
                InstanceRemoved?.Invoke(instance);
            }
        }

        internal void PublishAction(ItemActionCompleted action)
        {
            Action<ItemActionCompleted> handler = ActionCompleted;
            if (handler == null)
            {
                return;
            }

            foreach (Delegate subscriber in handler.GetInvocationList())
            {
                try
                {
                    ((Action<ItemActionCompleted>)subscriber).Invoke(action);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        internal void PublishHeldUseState(ItemHeldUseStateChanged state)
        {
            Action<ItemHeldUseStateChanged> handler = HeldUseStateChanged;
            if (handler == null)
            {
                return;
            }

            foreach (Delegate subscriber in handler.GetInvocationList())
            {
                try
                {
                    ((Action<ItemHeldUseStateChanged>)subscriber).Invoke(state);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!initialized)
            {
                return;
            }

            AdvanceCombustionSimulation(Time.fixedDeltaTime);
            AdvanceCookingSimulation(Time.fixedDeltaTime);
            if (Time.unscaledTime < nextRecoveryCheckTime)
            {
                return;
            }

            nextRecoveryCheckTime = Time.unscaledTime + 0.5f;
            ImportantObjectRecoveryFormation homeFormation = null;
            if (hasImportantRecoveryOrigin &&
                worldStreaming.TryGetCellIdForPosition(
                    importantRecoveryOrigin,
                    out string recoveryCellId) &&
                worldStreaming.IsCellLoaded(recoveryCellId))
            {
                homeFormation = new ImportantObjectRecoveryFormation(
                    importantRecoveryOrigin);
            }

            CaptureInstanceSnapshot(recoveryIterationBuffer);
            recoveryIterationBuffer.Sort(CompareStableIds);
            foreach (WorldItemInstance instance in recoveryIterationBuffer)
            {
                if (instance == null ||
                    instance.Definition == null ||
                    !instance.Definition.CriticalRecovery)
                {
                    continue;
                }

                Rigidbody body = instance.GetComponent<Rigidbody>();
                Vector3 position = body != null
                    ? body.position
                    : instance.transform.position;
                if (IsFinite(position) && position.y >= recoveryMinimumY)
                {
                    continue;
                }

                Pose recoveryPose;
                if (homeFormation != null)
                {
                    Quaternion recoveryRotation = body != null
                        ? body.rotation
                        : instance.transform.rotation;
                    if (!homeFormation.TryReserve(
                            instance.gameObject,
                            recoveryRotation,
                            out recoveryPose,
                            out string recoveryFailure))
                    {
                        Debug.LogError(
                            $"Important item '{instance.StableId.Value}' could " +
                            "not be recovered in front of the player home: " +
                            recoveryFailure,
                            instance);
                        continue;
                    }

                    recoveryPoses[instance.StableId.Value] = recoveryPose;
                }
                else if (hasImportantRecoveryOrigin ||
                         !recoveryPoses.TryGetValue(
                             instance.StableId.Value,
                             out recoveryPose))
                {
                    continue;
                }

                if (body != null)
                {
                    bool wasKinematic = body.isKinematic;
                    bool detectCollisions = body.detectCollisions;
                    RigidbodyInterpolation interpolation = body.interpolation;
                    body.interpolation = RigidbodyInterpolation.None;
                    body.detectCollisions = false;
                    if (!wasKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }

                    body.isKinematic = true;
                    body.position = recoveryPose.position;
                    body.rotation = recoveryPose.rotation;
                    body.transform.SetPositionAndRotation(
                        recoveryPose.position,
                        recoveryPose.rotation);
                    Physics.SyncTransforms();
                    body.detectCollisions = detectCollisions;
                    Physics.SyncTransforms();
                    body.isKinematic = wasKinematic;
                    if (!wasKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                        body.WakeUp();
                    }

                    body.interpolation = interpolation;
                }
                else
                {
                    instance.transform.SetPositionAndRotation(
                        recoveryPose.position,
                        recoveryPose.rotation);
                }

                instance.NotifyRecovered();
            }
        }

        private void OnDestroy()
        {
            gameTimeSubscription?.Dispose();
            gameTimeSubscription = null;
            ItemPresentationProviderHub.ProviderChanged -=
                HandlePresentationProviderChanged;
            if (worldStreaming != null)
            {
                worldStreaming.OwnedSceneLoaded -= HandleOwnedSceneLoaded;
            }

            if (livelyItemMaterial != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(livelyItemMaterial);
                }
                else
                {
                    DestroyImmediate(livelyItemMaterial);
                }

                livelyItemMaterial = null;
            }
        }

        private void HandleGameTimeEvent(in GameTimeEvent gameTimeEvent)
        {
            if (gameTimeEvent.Kind != GameTimeEventKind.Advanced)
            {
                return;
            }

            double current = gameTimeEvent.Current.ElapsedGameSeconds;
            CaptureInstanceSnapshot(foodIterationBuffer);
            foreach (WorldItemInstance instance in foodIterationBuffer)
            {
                if (instance == null || instance.Definition == null ||
                    !instance.Definition.Food.Perishable)
                {
                    continue;
                }

                ItemInstanceState state = instance.State;
                double previous = state.foodSimulationInitialized
                    ? state.lastFoodSimulationGameSeconds
                    : gameTimeEvent.Previous.ElapsedGameSeconds;
                double elapsed = current - previous;
                bool refrigerated = foodEnvironment != null &&
                    foodEnvironment.IsRefrigerated(instance);
                instance.AdvanceFreshness(elapsed, refrigerated, current);
            }
        }

        /// <summary>
        /// Advances authored fuel consumers in real simulation seconds. Item
        /// DTO fields remain the save authority; this method is the explicit
        /// deterministic seam used by both FixedUpdate and EditMode tests.
        /// </summary>
        public void AdvanceCombustionSimulation(float elapsedRealSeconds)
        {
            if (!initialized || !float.IsFinite(elapsedRealSeconds) ||
                elapsedRealSeconds <= 0f)
            {
                return;
            }

            CaptureInstanceSnapshot(combustionIterationBuffer);
            foreach (WorldItemInstance instance in combustionIterationBuffer)
            {
                if (instance?.Definition?.Combustion.IsConfigured == true)
                {
                    instance.AdvanceCombustion(elapsedRealSeconds);
                }
            }
        }

        /// <summary>
        /// Advances contact-based food cooking in real simulation seconds.
        /// This explicit seam keeps the state machine deterministic in tests;
        /// <see cref="FixedUpdate"/> is only the production clock adapter.
        /// </summary>
        public void AdvanceCookingSimulation(float elapsedRealSeconds)
        {
            if (!initialized || !float.IsFinite(elapsedRealSeconds) ||
                elapsedRealSeconds <= 0f || heatSources.Count == 0)
            {
                return;
            }

            CaptureInstanceSnapshot(cookingIterationBuffer);
            heatSourceIterationBuffer.Clear();
            foreach (IItemHeatSource source in heatSources)
            {
                heatSourceIterationBuffer.Add(source);
            }
            foreach (WorldItemInstance instance in cookingIterationBuffer)
            {
                if (instance == null || instance.Definition == null ||
                    !instance.Definition.Food.Cookable ||
                    instance.IsSpoiled)
                {
                    continue;
                }

                Collider itemCollider = instance.GetComponent<Collider>();
                Bounds itemBounds = itemCollider != null
                    ? itemCollider.bounds
                    : new Bounds(instance.transform.position, Vector3.one * 0.05f);
                float strongestRate = 0f;
                foreach (IItemHeatSource source in heatSourceIterationBuffer)
                {
                    if (source == null ||
                        source is UnityEngine.Object unitySource &&
                        unitySource == null)
                    {
                        heatSources.Remove(source);
                        continue;
                    }

                    if (source.IsHeating &&
                        source.WorldBounds.Intersects(itemBounds))
                    {
                        strongestRate = Mathf.Max(
                            strongestRate,
                            source.CookingRate);
                    }
                }

                if (strongestRate > 0f)
                {
                    instance.AdvanceCooking(elapsedRealSeconds, strongestRate);
                }
            }
        }

        private void CaptureInstanceSnapshot(
            List<WorldItemInstance> target)
        {
            target.Clear();
            foreach (WorldItemInstance instance in instances.Values)
            {
                target.Add(instance);
            }
        }

        private static int CompareStableIds(
            WorldItemInstance left,
            WorldItemInstance right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return StringComparer.Ordinal.Compare(
                left.StableId.Value,
                right.StableId.Value);
        }

        private void RegisterAlreadyLoadedCells()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded &&
                    worldStreaming.TryGetCellIdForScene(scene, out string cellId))
                {
                    SpawnPlacementsForCell(scene, cellId);
                }
            }
        }

        private void SpawnPlacementsWithoutOwnedCell()
        {
            foreach (ItemPlacementRecord placement in placements.Placements)
            {
                if (!sourceCellIds.ContainsKey(placement.StableEntityId))
                {
                    SpawnCanonicalPlacement(placement, persistentScene);
                }
            }
        }

        private void HandleOwnedSceneLoaded(Scene scene)
        {
            if (worldStreaming.TryGetCellIdForScene(scene, out string cellId))
            {
                SpawnPlacementsForCell(scene, cellId);
            }
        }

        private void SpawnPlacementsForCell(Scene scene, string cellId)
        {
            foreach (ItemPlacementRecord placement in placements.Placements)
            {
                if (sourceCellIds.TryGetValue(
                        placement.StableEntityId,
                        out string placementCellId) &&
                    string.Equals(
                        placementCellId,
                        cellId,
                        StringComparison.Ordinal))
                {
                    SpawnCanonicalPlacement(placement, scene);
                }
            }
        }

        private void SpawnCanonicalPlacement(
            ItemPlacementRecord placement,
            Scene scene)
        {
            if (TryGetInstance(placement.StableEntityId, out _))
            {
                return;
            }

            if (!definitions.TryGet(
                    placement.DefinitionId,
                    out ItemDefinitionRecord definition) ||
                !StableEntityId.TryParse(
                    placement.StableEntityId,
                    out StableEntityId stableId))
            {
                throw new InvalidOperationException(
                    $"Canonical item placement '{placement.PlacementId}' is invalid.");
            }

            CreateInstance(
                definition,
                stableId,
                placement.WorldPosition,
                placement.WorldRotation,
                scene,
                placement.InitialVariantIndex,
                placement);
        }

        private WorldItemInstance CreateInstance(
            ItemDefinitionRecord definition,
            StableEntityId stableId,
            Vector3 worldPosition,
            Quaternion worldRotation,
            Scene scene,
            int variantIndex,
            ItemPlacementRecord canonicalPlacement)
        {
            var root = new GameObject(
                $"World item [{definition.DefinitionId}]");
            root.transform.SetPositionAndRotation(
                worldPosition,
                worldRotation.normalized);
            root.transform.localScale = canonicalPlacement?.InitialLocalScale ??
                Vector3.one;
            root.layer = WorldItemCollisionLayer;
            SceneManager.MoveGameObjectToScene(root, scene);

            StableEntityIdAuthoring identity =
                root.AddComponent<StableEntityIdAuthoring>();
            identity.InitializeExplicitRuntimeId(stableId);
            Rigidbody body = canonicalPlacement != null &&
                             !canonicalPlacement.DonorHasRigidbody
                ? null
                : root.AddComponent<Rigidbody>();
            if (body != null)
            {
                body.mass = definition.MassKilograms;
                body.useGravity = true;
                body.isKinematic = false;
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                if (definition.MassKilograms >= 1000f)
                {
                    body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    body.linearDamping = 0.3f;
                    body.angularDamping = 2f;
                    body.maxAngularVelocity = 2f;
                    body.sleepThreshold = 0.02f;
                    body.solverIterations = 12;
                    body.solverVelocityIterations = 8;
                    body.maxDepenetrationVelocity = 1.5f;
                }
                else if (UsesStableHeavyContainerPhysics(definition.DefinitionId) ||
                         definition.MassKilograms >= 20f)
                {
                    body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    body.linearDamping = 0.16f;
                    body.angularDamping = 1.1f;
                    body.maxAngularVelocity = 8f;
                    body.sleepThreshold = 0.01f;
                    body.solverIterations = 12;
                    body.solverVelocityIterations = 8;
                    body.maxDepenetrationVelocity = 3f;
                }
                else if (definition.MassKilograms >= 5f)
                {
                    body.collisionDetectionMode =
                        CollisionDetectionMode.ContinuousSpeculative;
                    body.linearDamping = 0.06f;
                    body.angularDamping = 0.2f;
                    body.maxAngularVelocity = 20f;
                    body.sleepThreshold = 0.005f;
                    body.solverIterations = 10;
                    body.solverVelocityIterations = 6;
                    body.maxDepenetrationVelocity = 6f;
                }
                else
                {
                    body.collisionDetectionMode =
                        CollisionDetectionMode.ContinuousSpeculative;
                    body.linearDamping = 0.04f;
                    body.angularDamping = 0.08f;
                    body.maxAngularVelocity = 35f;
                    body.sleepThreshold = 0.002f;
                    body.solverIterations = 8;
                    body.solverVelocityIterations = 4;
                    body.maxDepenetrationVelocity = 12f;
                }

                if (UsesSphericalCollider(definition.DefinitionId))
                {
                    body.linearDamping = 0.015f;
                    body.angularDamping = 0.035f;
                    body.maxAngularVelocity = 50f;
                    body.sleepThreshold = 0.001f;
                }

                if (canonicalPlacement != null)
                {
                    ApplyCanonicalPhysics(body, canonicalPlacement);
                }
            }

            if (body != null && UsesSupplementalContactSpin(definition))
            {
                root.AddComponent<LivelyItemPhysics>();
            }

            Collider collider = CreatePhysicalCollider(root, definition);
            PhysicsMaterial itemMaterial =
                UsesSphericalCollider(definition.DefinitionId)
                    ? GetBallItemMaterial()
                    : GetLivelyItemMaterial();
            foreach (Collider physicalCollider in
                     root.GetComponents<Collider>())
            {
                physicalCollider.sharedMaterial = itemMaterial;
            }

            PhysicsPickupTarget pickup = null;
            if (body != null)
            {
                pickup = root.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(
                    body,
                    identity,
                    "Поднять",
                    definition.MaximumCarryMassKilograms,
                    useGravityWhenLoose: true,
                    canPickupKinematicBody: true);
            }
            WorldItemInstance instance =
                root.AddComponent<WorldItemInstance>();
            instance.Configure(this, definition, identity, variantIndex);
            if (definition.HeatSource.ProvidesCookingHeat)
            {
                ItemHeatSourceVolume heatSource =
                    root.AddComponent<ItemHeatSourceVolume>();
                heatSource.ConfigureForItem(
                    this,
                    instance,
                    definition.HeatSource);
            }
            if (definition.Combustion.IsConfigured)
            {
                ItemFuelPourReceiver fuelReceiver =
                    root.AddComponent<ItemFuelPourReceiver>();
                fuelReceiver.Configure(instance);
            }
            if (LiquidContainerTiltSpiller.TryGetLocalOpenAxis(
                    definition.DefinitionId,
                    out Vector3 liquidOpenAxis))
            {
                LiquidContainerTiltSpiller tiltSpiller =
                    root.AddComponent<LiquidContainerTiltSpiller>();
                tiltSpiller.Configure(instance, liquidOpenAxis);
            }
            if (definition.PrimaryAction == ItemPrimaryAction.Ignite)
            {
                GarbageBarrelBurner burner =
                    root.AddComponent<GarbageBarrelBurner>();
                burner.Configure(instance, definition.ProxySize);
            }
            InteractionTargetHost host =
                root.AddComponent<InteractionTargetHost>();
            if (pickup != null)
            {
                host.Configure(pickup, instance);
            }
            else
            {
                host.Configure(instance);
            }

            instances.Add(stableId.Value, instance);
            AttachPresentation(instance, collider);
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                if (canonicalPlacement != null || body.isKinematic)
                {
                    body.Sleep();
                }
                else
                {
                    body.WakeUp();
                }
            }

            InstanceMaterialized?.Invoke(instance);
            return instance;
        }

        private static void ApplyCanonicalPhysics(
            Rigidbody body,
            ItemPlacementRecord placement)
        {
            body.useGravity = placement.InitialUseGravity;
            body.detectCollisions = placement.InitialDetectCollisions;
            body.constraints = placement.InitialConstraints;
            body.linearDamping = placement.InitialLinearDamping;
            body.angularDamping = placement.InitialAngularDamping;
            body.interpolation = placement.InitialInterpolation;
            body.collisionDetectionMode = placement.InitialIsKinematic
                ? CollisionDetectionMode.ContinuousSpeculative
                : placement.InitialCollisionMode;
            body.isKinematic = placement.InitialIsKinematic;
            body.maxDepenetrationVelocity = Mathf.Min(
                body.maxDepenetrationVelocity,
                3f);
        }

        private void HandlePresentationProviderChanged(
            IItemPresentationProvider provider)
        {
            presentationProvider = provider;
            if (provider == null)
            {
                return;
            }

            foreach (WorldItemInstance instance in instances.Values.ToArray())
            {
                if (instance != null)
                {
                    Rigidbody body = instance.GetComponent<Rigidbody>();
                    bool wasSleeping = body != null && body.IsSleeping();
                    Collider collider = instance.GetComponent<Collider>();
                    AttachPresentation(instance, collider);
                    if (wasSleeping)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                        body.Sleep();
                    }
                }
            }
        }

        private void AttachPresentation(
            WorldItemInstance instance,
            Collider collider)
        {
            ItemProxyPresentation[] proxies =
                instance.GetComponentsInChildren<ItemProxyPresentation>(true);
            if (presentationProvider != null &&
                presentationProvider.TryInstantiate(
                    instance.Definition,
                    instance.transform,
                    out GameObject visualRoot))
            {
                foreach (ItemProxyPresentation proxy in proxies)
                {
                    if (proxy != null)
                    {
                        Destroy(proxy.gameObject);
                    }
                }

                ResizeColliderToRenderers(
                    instance.transform,
                    visualRoot,
                    collider,
                    instance.Definition);
                SetLayerRecursively(visualRoot, WorldItemCollisionLayer);
                instance.SetPresentationRoot(visualRoot);
                PresentationAttached?.Invoke(instance, visualRoot);
                return;
            }

            GameObject presentationRoot = null;
            if (proxies.Length == 0)
            {
                GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
                proxy.name = "Project-owned item proxy";
                proxy.transform.SetParent(instance.transform, false);
                proxy.transform.localScale = instance.Definition.ProxySize;
                proxy.layer = WorldItemCollisionLayer;
                Collider proxyCollider = proxy.GetComponent<Collider>();
                if (proxyCollider != null)
                {
                    Destroy(proxyCollider);
                }

                proxy.AddComponent<ItemProxyPresentation>();
                presentationRoot = proxy;
            }
            else
            {
                presentationRoot = proxies[0].gameObject;
            }

            instance.SetPresentationRoot(presentationRoot);
            PresentationAttached?.Invoke(instance, presentationRoot);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            if (root == null)
            {
                return;
            }

            root.layer = layer;
            Transform rootTransform = root.transform;
            for (int index = 0; index < rootTransform.childCount; index++)
            {
                SetLayerRecursively(
                    rootTransform.GetChild(index).gameObject,
                    layer);
            }
        }

        private static void ResizeColliderToRenderers(
            Transform root,
            GameObject visualRoot,
            Collider collider,
            ItemDefinitionRecord definition)
        {
            Renderer[] renderers =
                visualRoot.GetComponentsInChildren<Renderer>(true);
            if (collider == null || renderers.Length == 0)
            {
                return;
            }

            if (definition?.HasAuthoredColliderShapes == true)
            {
                return;
            }

            if (definition != null &&
                string.Equals(
                    definition.DefinitionId,
                    BeerCaseContentsPresentation.BeerCaseDefinitionId,
                    StringComparison.Ordinal))
            {
                Renderer caseRenderer = FindLargestRenderer(renderers);
                renderers = caseRenderer != null
                    ? new[] { caseRenderer }
                    : Array.Empty<Renderer>();
            }

            bool hasBounds = false;
            Bounds bounds = default;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                Bounds rendererBounds = renderer.localBounds;
                Vector3 minimum = rendererBounds.min;
                Vector3 maximum = rendererBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 localCorner = new Vector3(
                        (corner & 1) == 0 ? minimum.x : maximum.x,
                        (corner & 2) == 0 ? minimum.y : maximum.y,
                        (corner & 4) == 0 ? minimum.z : maximum.z);
                    Vector3 rootLocalCorner = root.InverseTransformPoint(
                        renderer.transform.TransformPoint(localCorner));
                    if (!hasBounds)
                    {
                        bounds = new Bounds(rootLocalCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(rootLocalCorner);
                    }
                }
            }

            if (!hasBounds)
            {
                return;
            }

            Vector3 center = bounds.center;
            Vector3 size = new Vector3(
                Mathf.Max(0.02f, bounds.size.x),
                Mathf.Max(0.02f, bounds.size.y),
                Mathf.Max(0.02f, bounds.size.z));
            if (definition != null &&
                string.Equals(
                    definition.DefinitionId,
                    "item.garbage-barrel",
                    StringComparison.Ordinal) &&
                collider is BoxCollider barrelBottom)
            {
                GarbageBarrelPhysicsShape shape =
                    root.GetComponent<GarbageBarrelPhysicsShape>() ??
                    root.gameObject.AddComponent<
                        GarbageBarrelPhysicsShape>();
                shape.Configure(
                    barrelBottom,
                    new Bounds(center, size),
                    collider.sharedMaterial);
                root.GetComponent<GarbageBarrelBurner>()?.ConfigureVolume(
                    new Bounds(center, size));
                return;
            }

            if (collider is BoxCollider box)
            {
                box.center = center;
                box.size = size;
                return;
            }

            if (collider is CapsuleCollider capsule)
            {
                int direction = LargestAxis(size);
                float firstPerpendicular = direction == 0 ? size.y : size.x;
                float secondPerpendicular = direction == 2 ? size.y : size.z;
                capsule.center = center;
                capsule.direction = direction;
                capsule.radius = Mathf.Max(
                    0.01f,
                    Mathf.Min(firstPerpendicular, secondPerpendicular) * 0.46f);
                float axisLength = direction switch
                {
                    0 => size.x,
                    1 => size.y,
                    _ => size.z,
                };
                capsule.height = Mathf.Max(axisLength, capsule.radius * 2f);
                return;
            }

            if (collider is SphereCollider sphere)
            {
                sphere.center = center;
                sphere.radius = Mathf.Max(
                    0.01f,
                    Mathf.Max(size.x, Mathf.Max(size.y, size.z)) * 0.48f);
            }
        }

        private static Renderer FindLargestRenderer(Renderer[] renderers)
        {
            Renderer largest = null;
            float largestVolume = float.NegativeInfinity;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                Vector3 size = renderer.localBounds.size;
                float volume = Mathf.Abs(size.x * size.y * size.z);
                if (volume > largestVolume)
                {
                    largest = renderer;
                    largestVolume = volume;
                }
            }

            return largest;
        }

        private Collider CreatePhysicalCollider(
            GameObject root,
            ItemDefinitionRecord definition)
        {
            if (definition.HasAuthoredColliderShapes)
            {
                Collider first = null;
                foreach (ItemColliderShapeDefinition shape in
                         definition.ColliderShapes)
                {
                    Collider created;
                    switch (shape.Kind)
                    {
                        case ItemColliderShapeKind.Sphere:
                            var sphere = root.AddComponent<SphereCollider>();
                            sphere.center = shape.Center;
                            sphere.radius = shape.Radius;
                            created = sphere;
                            break;
                        case ItemColliderShapeKind.Capsule:
                            var capsule = root.AddComponent<CapsuleCollider>();
                            capsule.center = shape.Center;
                            capsule.radius = shape.Radius;
                            capsule.height = shape.Height;
                            capsule.direction = shape.Direction;
                            created = capsule;
                            break;
                        default:
                            var box = root.AddComponent<BoxCollider>();
                            box.center = shape.Center;
                            box.size = shape.Size;
                            created = box;
                            break;
                    }

                    created.isTrigger = shape.IsTrigger;
                    first ??= created;
                }

                if (first != null)
                {
                    return first;
                }
            }

            Collider collider;
            if (UsesSphericalCollider(definition.DefinitionId))
            {
                var sphere = root.AddComponent<SphereCollider>();
                sphere.radius = Mathf.Max(
                    0.01f,
                    Mathf.Max(
                        definition.ProxySize.x,
                        Mathf.Max(
                            definition.ProxySize.y,
                            definition.ProxySize.z)) * 0.48f);
                collider = sphere;
            }
            else if (UsesRoundedCollider(definition.DefinitionId))
            {
                var capsule = root.AddComponent<CapsuleCollider>();
                Vector3 size = definition.ProxySize;
                capsule.direction = LargestAxis(size);
                float firstPerpendicular =
                    capsule.direction == 0 ? size.y : size.x;
                float secondPerpendicular =
                    capsule.direction == 2 ? size.y : size.z;
                capsule.radius = Mathf.Max(
                    0.01f,
                    Mathf.Min(firstPerpendicular, secondPerpendicular) * 0.46f);
                float axisLength = capsule.direction switch
                {
                    0 => size.x,
                    1 => size.y,
                    _ => size.z,
                };
                capsule.height = Mathf.Max(axisLength, capsule.radius * 2f);
                collider = capsule;
            }
            else
            {
                var box = root.AddComponent<BoxCollider>();
                box.size = definition.ProxySize;
                collider = box;
            }

            return collider;
        }

        private PhysicsMaterial GetLivelyItemMaterial()
        {
            if (livelyItemMaterial != null)
            {
                return livelyItemMaterial;
            }

            livelyItemMaterial = new PhysicsMaterial("Runtime Lively Item")
            {
                dynamicFriction = 0.55f,
                staticFriction = 0.72f,
                bounciness = 0.06f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                hideFlags = HideFlags.HideAndDontSave,
            };
            return livelyItemMaterial;
        }

        private PhysicsMaterial GetBallItemMaterial()
        {
            if (ballItemMaterial != null)
            {
                return ballItemMaterial;
            }

            ballItemMaterial = new PhysicsMaterial("Runtime Rolling Ball")
            {
                dynamicFriction = 0.42f,
                staticFriction = 0.5f,
                bounciness = 0.68f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                hideFlags = HideFlags.HideAndDontSave,
            };
            return ballItemMaterial;
        }

        private static bool UsesRoundedCollider(string definitionId)
        {
            return string.Equals(
                       definitionId,
                       "item.beer-bottle",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       definitionId,
                       "item.booze-bottle",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       definitionId,
                       "item.loose-sausage",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       definitionId,
                       "item.mosquito-spray",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       definitionId,
                       "item.fire-extinguisher",
                       StringComparison.Ordinal) ||
                   string.Equals(
                       definitionId,
                       "item.flashlight",
                       StringComparison.Ordinal);
        }

        private static bool UsesStableHeavyContainerPhysics(
            string definitionId) =>
            string.Equals(
                definitionId,
                "item.garbage-barrel",
                StringComparison.Ordinal);

        private static bool UsesSphericalCollider(string definitionId) =>
            string.Equals(
                definitionId,
                "item.basketball",
                StringComparison.Ordinal) ||
            string.Equals(
                definitionId,
                "item.football",
                StringComparison.Ordinal);

        private static bool UsesSupplementalContactSpin(
            ItemDefinitionRecord definition) =>
            definition != null &&
            (UsesRoundedCollider(definition.DefinitionId) ||
             UsesSphericalCollider(definition.DefinitionId));

        private static int LargestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
            {
                return 0;
            }

            return size.y >= size.z ? 1 : 2;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }

    /// <summary>
    /// Project-owned compound approximation of the donor barrel's eight
    /// convex collider sections. The open top remains physically usable.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarbageBarrelPhysicsShape : MonoBehaviour
    {
        public const int WallSegmentCount = 8;

        private readonly List<GameObject> generatedWallParts =
            new List<GameObject>(WallSegmentCount);

        public int GeneratedWallPartCount => generatedWallParts.Count;

        public void Configure(
            BoxCollider bottomCollider,
            Bounds localBounds,
            PhysicsMaterial physicsMaterial)
        {
            if (bottomCollider == null)
            {
                throw new ArgumentNullException(nameof(bottomCollider));
            }

            Vector3 size = localBounds.size;
            if (!IsFinitePositive(size))
            {
                throw new ArgumentException(
                    "Garbage barrel bounds must be finite and positive.",
                    nameof(localBounds));
            }

            ClearGeneratedWalls();

            int verticalAxis = LargestAxis(size);
            (int firstRadialAxis, int secondRadialAxis) =
                GetRadialAxes(verticalAxis);
            float verticalLength = GetAxis(size, verticalAxis);
            float firstRadius = GetAxis(size, firstRadialAxis) * 0.5f;
            float secondRadius = GetAxis(size, secondRadialAxis) * 0.5f;
            float wallThickness = Mathf.Clamp(
                Mathf.Min(firstRadius, secondRadius) * 0.12f,
                0.018f,
                0.045f);
            float bottomThickness = Mathf.Clamp(
                verticalLength * 0.055f,
                0.025f,
                0.055f);

            Vector3 bottomSize = size;
            SetAxis(ref bottomSize, verticalAxis, bottomThickness);
            Vector3 bottomCenter = localBounds.center;
            SetAxis(
                ref bottomCenter,
                verticalAxis,
                GetAxis(localBounds.min, verticalAxis) +
                bottomThickness * 0.5f);
            bottomCollider.center = bottomCenter;
            bottomCollider.size = bottomSize;
            bottomCollider.sharedMaterial = physicsMaterial;
            bottomCollider.enabled = true;

            Vector3 verticalDirection = AxisVector(verticalAxis);
            Vector3 firstRadialDirection = AxisVector(firstRadialAxis);
            Vector3 secondRadialDirection = AxisVector(secondRadialAxis);
            float wallHeight = Mathf.Max(
                0.02f,
                verticalLength - bottomThickness * 0.5f);
            float wallVerticalCenter =
                GetAxis(localBounds.min, verticalAxis) +
                bottomThickness * 0.5f +
                wallHeight * 0.5f;
            float tangentLength =
                2f * Mathf.Max(firstRadius, secondRadius) *
                Mathf.Tan(Mathf.PI / WallSegmentCount) * 1.08f;

            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                Vector3 loweredCenterOfMass = localBounds.center;
                SetAxis(
                    ref loweredCenterOfMass,
                    verticalAxis,
                    GetAxis(localBounds.center, verticalAxis) -
                    verticalLength * 0.16f);
                body.centerOfMass = loweredCenterOfMass;
            }

            for (int segment = 0;
                 segment < WallSegmentCount;
                 segment++)
            {
                float angle =
                    segment * Mathf.PI * 2f / WallSegmentCount;
                float cosine = Mathf.Cos(angle);
                float sine = Mathf.Sin(angle);
                Vector3 radialDirection =
                    (firstRadialDirection * cosine +
                     secondRadialDirection * sine).normalized;
                Vector3 wallCenter = localBounds.center +
                    firstRadialDirection *
                    ((firstRadius - wallThickness * 0.5f) * cosine) +
                    secondRadialDirection *
                    ((secondRadius - wallThickness * 0.5f) * sine);
                SetAxis(
                    ref wallCenter,
                    verticalAxis,
                    wallVerticalCenter);

                var wall = new GameObject(
                    $"Generated barrel wall collider {segment + 1}");
                wall.layer = gameObject.layer;
                wall.transform.SetParent(transform, false);
                wall.transform.localPosition = wallCenter;
                wall.transform.localRotation = Quaternion.LookRotation(
                    radialDirection,
                    verticalDirection);

                BoxCollider wallCollider =
                    wall.AddComponent<BoxCollider>();
                wallCollider.size = new Vector3(
                    tangentLength,
                    wallHeight,
                    wallThickness);
                wallCollider.sharedMaterial = physicsMaterial;
                generatedWallParts.Add(wall);
            }
        }

        private void ClearGeneratedWalls()
        {
            for (int index = 0;
                 index < generatedWallParts.Count;
                 index++)
            {
                GameObject wall = generatedWallParts[index];
                if (wall == null)
                {
                    continue;
                }

                wall.SetActive(false);
                if (Application.isPlaying)
                {
                    Destroy(wall);
                }
                else
                {
                    DestroyImmediate(wall);
                }
            }

            generatedWallParts.Clear();
        }

        private static (int first, int second) GetRadialAxes(
            int verticalAxis) =>
            verticalAxis switch
            {
                0 => (1, 2),
                1 => (0, 2),
                _ => (0, 1),
            };

        private static Vector3 AxisVector(int axis) =>
            axis switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                _ => Vector3.forward,
            };

        private static float GetAxis(Vector3 value, int axis) =>
            axis switch
            {
                0 => value.x,
                1 => value.y,
                _ => value.z,
            };

        private static void SetAxis(
            ref Vector3 value,
            int axis,
            float component)
        {
            switch (axis)
            {
                case 0:
                    value.x = component;
                    break;
                case 1:
                    value.y = component;
                    break;
                default:
                    value.z = component;
                    break;
            }
        }

        private static int LargestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
            {
                return 0;
            }

            return size.y >= size.z ? 1 : 2;
        }

        private static bool IsFinitePositive(Vector3 value) =>
            float.IsFinite(value.x) && value.x > 0f &&
            float.IsFinite(value.y) && value.y > 0f &&
            float.IsFinite(value.z) && value.z > 0f;
    }
}
