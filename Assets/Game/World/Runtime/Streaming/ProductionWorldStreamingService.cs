using System;
using System.Collections;
using System.Collections.Generic;
using MSC.Core.Lifecycle;
using MSC.World.Partition;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace MSC.World.Streaming
{
    /// <summary>
    /// Loads the explicitly catalogued production cells around an explicitly bound focus.
    /// The service unloads only scenes that it loaded itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionWorldStreamingService : MonoBehaviour,
        IWorldStreamingService,
        IGameSessionLifetime
    {
        public const int MaximumAutomaticLayerLoadsPerRefresh = 1;
        public const int PresentationLayerAsyncOperationPriority = -1;

        private static readonly ProfilerMarker startSceneLoadMarker = new ProfilerMarker("MSC.Streaming.StartSceneLoad");
        private static readonly ProfilerMarker sceneLoadedNotificationMarker = new ProfilerMarker("MSC.Streaming.NotifySceneLoaded");
        private static readonly ProfilerMarker unusedAssetsMarker = new ProfilerMarker("MSC.Streaming.StartUnusedAssetCleanup");
        [SerializeField] private ProductionWorldStreamingManifest manifest;
        [SerializeField] private Transform focus;

        private readonly Dictionary<string, int> ownedLoadedSceneHandles =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> retainedCellByOwner =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private bool isStreaming;
        private bool hasObservedCell;
        private bool hasObservedLoadingRadius;
        private WorldCellIndex observedCell;
        private int observedLoadingRadius;
        private uint retentionRevision;
        private uint observedRetentionRevision;
        private float reportedFocusSpeedMetersPerSecond;
        private Rigidbody boundFocusRigidbody;
        private Coroutine automaticRefresh;
        private bool unusedAssetCleanupPending;
        private InFlightOwnedSceneLoad inFlightOwnedSceneLoad;

        public bool IsStreaming => isStreaming;
        public bool HasFocus => focus != null;
        public int OwnedLoadedSceneCount
        {
            get
            {
                PruneStaleOwnership();
                return ownedLoadedSceneHandles.Count;
            }
        }
        public ProductionWorldStreamingManifest Manifest => manifest;
        public Transform Focus => focus;
        public float ReportedFocusSpeedMetersPerSecond =>
            reportedFocusSpeedMetersPerSecond;
        public int CompletedSceneLoadCount { get; private set; }
        public string LastSceneLoadPath { get; private set; } = string.Empty;
        public int LastSceneLoadStartedFrame { get; private set; } = -1;
        public int LastSceneLoadCompletedFrame { get; private set; } = -1;
        /// <summary>Wall time including asynchronous waits; not main-thread CPU time.</summary>
        public double LastSceneLoadElapsedMilliseconds { get; private set; }
        public double LastSceneNotificationCpuMilliseconds { get; private set; }
        public double MaximumSceneNotificationCpuMilliseconds { get; private set; }
        public int LastRefreshStartedLayerLoadCount { get; private set; }
        public int MaximumAutomaticRefreshLayerLoadCount { get; private set; }
        public int CompletedAutomaticRefreshCount { get; private set; }
        public int LastSceneActivationPreparedFrame { get; private set; } = -1;
        public int CompletedUnusedAssetCleanupCount { get; private set; }
        /// <summary>
        /// True after an ordinary distance refresh unloaded an owned optional
        /// layer. The potentially expensive asset reachability scan is deferred
        /// until the explicit <see cref="UnloadOwnedScenes"/> cleanup boundary.
        /// </summary>
        public bool HasPendingUnusedAssetCleanup => unusedAssetCleanupPending;
        /// <summary>Wall time including asynchronous waits; inspect the profiler marker for start-call CPU time.</summary>
        public double LastUnusedAssetCleanupElapsedMilliseconds { get; private set; }
        public int EffectiveLoadingRadiusCells =>
            manifest == null
                ? 0
                : manifest.GetLoadingRadiusForSpeed(
                    reportedFocusSpeedMetersPerSecond);

        /// <summary>
        /// Raised after an owned additive scene is fully loaded and registered.
        /// Save integration uses this explicit boundary to apply deferred stable
        /// entity state without relying on hierarchy names or scene polling.
        /// </summary>
        public event Action<Scene> OwnedSceneLoaded;

        /// <summary>
        /// Raised when an owned additive scene outside the unload radius is
        /// evaluated for unloading. Subscribers may capture mutable stable-
        /// entity state while the scene objects are still valid, or retain the
        /// cell to veto that unload. Retained cells are re-evaluated on a later
        /// refresh so a subscriber can release its retention safely.
        /// </summary>
        public event Action<Scene> OwnedSceneWillUnload;

        public void BindFocus(Transform streamingFocus)
        {
            focus = streamingFocus != null
                ? streamingFocus
                : throw new ArgumentNullException(nameof(streamingFocus));
            hasObservedCell = false;
            hasObservedLoadingRadius = false;
            reportedFocusSpeedMetersPerSecond = 0f;
            boundFocusRigidbody =
                focus.GetComponentInParent<Rigidbody>();
        }

        public void ReportFocusSpeedMetersPerSecond(float speedMetersPerSecond)
        {
            if (!float.IsFinite(speedMetersPerSecond) ||
                speedMetersPerSecond < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(speedMetersPerSecond),
                    "Streaming focus speed must be finite and non-negative.");
            }

            reportedFocusSpeedMetersPerSecond = speedMetersPerSecond;
        }

        public bool IsCellLoaded(string cellId)
        {
            return manifest != null &&
                   manifest.TryGetCell(cellId, out ProductionWorldCellScene cell) &&
                   TryGetLoadedScene(cell.ScenePath, out _);
        }

        public bool IsCellLayerLoaded(string cellId, string layerId)
        {
            if (manifest == null)
            {
                return false;
            }

            IReadOnlyList<ProductionWorldCellLayerScene> layers = manifest.CellLayers;
            for (int index = 0; index < layers.Count; index++)
            {
                ProductionWorldCellLayerScene layer = layers[index];
                if (string.Equals(layer.CellId, cellId, StringComparison.Ordinal) &&
                    string.Equals(layer.LayerId, layerId, StringComparison.Ordinal))
                {
                    return TryGetLoadedScene(layer.ScenePath, out _);
                }
            }

            return false;
        }

        public bool IsGlobalSceneLoaded(string sceneId)
        {
            if (manifest == null)
            {
                return false;
            }

            IReadOnlyList<ProductionWorldGlobalScene> globalScenes =
                manifest.GlobalScenes;
            for (int index = 0; index < globalScenes.Count; index++)
            {
                ProductionWorldGlobalScene globalScene = globalScenes[index];
                if (string.Equals(
                        globalScene.SceneId,
                        sceneId,
                        StringComparison.Ordinal))
                {
                    return TryGetLoadedScene(globalScene.ScenePath, out _);
                }
            }

            return false;
        }

        /// <summary>
        /// Keeps one project-owned cell available while a persistent stable
        /// entity is being resolved. Each explicit owner may retain one cell;
        /// callers persist the stable cell ID, never a scene path.
        /// </summary>
        public void RetainCell(string ownerId, string cellId)
        {
            if (string.IsNullOrWhiteSpace(ownerId))
            {
                throw new ArgumentException(
                    "A cell-retention owner ID is required.",
                    nameof(ownerId));
            }

            if (manifest == null || !manifest.TryGetCell(cellId, out _))
            {
                throw new ArgumentException(
                    $"Unknown production world cell '{cellId}'.",
                    nameof(cellId));
            }

            if (retainedCellByOwner.TryGetValue(ownerId, out string current) &&
                string.Equals(current, cellId, StringComparison.Ordinal))
            {
                return;
            }

            retainedCellByOwner[ownerId] = cellId;
            retentionRevision++;
        }

        public void ReleaseCellRetention(string ownerId)
        {
            if (!string.IsNullOrWhiteSpace(ownerId) &&
                retainedCellByOwner.Remove(ownerId))
            {
                retentionRevision++;
            }
        }

        public bool TryGetCellIdForScene(Scene scene, out string cellId)
        {
            if (manifest != null && scene.IsValid())
            {
                IReadOnlyList<ProductionWorldCellScene> cells = manifest.Cells;
                for (int index = 0; index < cells.Count; index++)
                {
                    if (string.Equals(
                            cells[index].ScenePath,
                            scene.path,
                            StringComparison.Ordinal))
                    {
                        cellId = cells[index].CellId;
                        return true;
                    }
                }
            }

            cellId = string.Empty;
            return false;
        }

        public bool TryGetCellIdForPosition(
            Vector3 worldPosition,
            out string cellId)
        {
            if (manifest != null && IsFinite(worldPosition) &&
                manifest.TryGetCell(
                    WorldCellMembershipUtility.FromPosition(
                        worldPosition,
                        manifest.CellSizeMeters),
                    out ProductionWorldCellScene cell))
            {
                cellId = cell.CellId;
                return true;
            }

            cellId = string.Empty;
            return false;
        }

        public bool OwnsLoadedScene(string scenePath)
        {
            if (string.IsNullOrWhiteSpace(scenePath) ||
                !TryGetLoadedScene(scenePath, out Scene scene))
            {
                ownedLoadedSceneHandles.Remove(
                    scenePath ?? string.Empty);
                return false;
            }

            return OwnsLoadedScene(scene);
        }

        public IEnumerator RefreshNow()
        {
            return RefreshCore(false);
        }

        private IEnumerator RefreshCore(bool automatic)
        {
            while (isStreaming)
            {
                yield return null;
            }

            ValidateRuntimeConfiguration();
            PruneStaleOwnership();
            isStreaming = true;
            LastRefreshStartedLayerLoadCount = 0;
            try
            {
                WorldCellIndex center = GetCurrentFocusCell();
                int loadingRadius = manifest.GetLoadingRadiusForSpeed(
                    reportedFocusSpeedMetersPerSecond);
                uint appliedRetentionRevision = retentionRevision;

                yield return EnsureGlobalScenesLoaded();

                IReadOnlyList<ProductionWorldCellScene> cells = manifest.Cells;
                for (int index = 0; index < cells.Count; index++)
                {
                    ProductionWorldCellScene cell = cells[index];
                    int distance = Mathf.Max(
                        Mathf.Abs(cell.Index.X - center.X),
                        Mathf.Abs(cell.Index.Z - center.Z));

                    bool isLoaded = TryGetLoadedScene(
                        cell.ScenePath,
                        out Scene loadedScene);
                    bool retained = IsCellRetained(cell.CellId);
                    if ((distance <= loadingRadius || retained) && !isLoaded)
                    {
                        yield return LoadOwnedScene(
                            cell.BuildIndex,
                            cell.ScenePath,
                            "production cell " + cell.CellId);
                    }
                    else if (distance > manifest.UnloadingRadiusCells &&
                             isLoaded &&
                             OwnsLoadedScene(loadedScene))
                    {
                        OwnedSceneWillUnload?.Invoke(loadedScene);
                        if (IsCellRetained(cell.CellId))
                        {
                            // A subscriber discovered live persistent state
                            // that cannot yet be transferred out of this cell.
                            // Retention is an explicit veto for this refresh.
                            continue;
                        }

                        int ownedHandle = loadedScene.handle;
                        AsyncOperation unload = SceneManager.UnloadSceneAsync(loadedScene);
                        if (unload == null)
                        {
                            throw new InvalidOperationException(
                                $"Could not start unload for owned production cell {cell.CellId}.");
                        }

                        yield return unload;
                        RemoveOwnershipIfHandleMatches(
                            cell.ScenePath,
                            ownedHandle);
                    }
                }

                yield return RefreshCellLayers(
                    center,
                    loadingRadius,
                    automatic
                        ? MaximumAutomaticLayerLoadsPerRefresh
                        : int.MaxValue,
                    skipDeferredInitialLoads: !automatic && !hasObservedCell);

                observedCell = center;
                hasObservedCell = true;
                observedLoadingRadius = loadingRadius;
                hasObservedLoadingRadius = true;
                observedRetentionRevision = appliedRetentionRevision;
            }
            finally
            {
                if (automatic)
                {
                    CompletedAutomaticRefreshCount++;
                    MaximumAutomaticRefreshLayerLoadCount = Math.Max(
                        MaximumAutomaticRefreshLayerLoadCount,
                        LastRefreshStartedLayerLoadCount);
                }

                isStreaming = false;
            }
        }

        public IEnumerator UnloadOwnedScenes()
        {
            while (isStreaming)
            {
                yield return null;
            }

            isStreaming = true;
            try
            {
                PruneStaleOwnership();
                var ownedPaths = new List<string>(
                    ownedLoadedSceneHandles.Keys);
                ownedPaths.Sort(CompareOwnedUnloadOrder);
                for (int index = 0; index < ownedPaths.Count; index++)
                {
                    string scenePath = ownedPaths[index];
                    if (!ownedLoadedSceneHandles.TryGetValue(
                            scenePath,
                            out int ownedHandle))
                    {
                        continue;
                    }

                    if (TryGetLoadedScene(scenePath, out Scene scene) &&
                        scene.handle == ownedHandle)
                    {
                        OwnedSceneWillUnload?.Invoke(scene);
                        AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                        if (unload != null)
                        {
                            yield return unload;
                            unusedAssetCleanupPending |=
                                IsCellLayerScenePath(scenePath);
                        }
                    }

                    RemoveOwnershipIfHandleMatches(
                        scenePath,
                        ownedHandle);
                }

                if (unusedAssetCleanupPending)
                {
                    // Scene destruction releases renderers, not their asset
                    // payloads. This explicit teardown boundary coalesces every
                    // earlier distance unload into one reachability scan and
                    // preserves assets still used by retained/external scenes.
                    yield return UnloadUnusedAssetsAfterLayerBatch();
                    unusedAssetCleanupPending = false;
                }
            }
            finally
            {
                isStreaming = false;
                hasObservedCell = false;
                hasObservedLoadingRadius = false;
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(ProductionWorldStreamingManifest configuredManifest)
        {
            manifest = configuredManifest;
        }
#endif

        private void Awake()
        {
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
        }

        public void EndGameSession()
        {
            // Capture and detach a load before stopping the driving coroutine.
            // A presentation load can be parked at progress 0.9 with scene
            // activation disabled; abandoning that AsyncOperation would leave
            // it permanently pending, while merely enabling activation would
            // let an unowned scene appear after this service is destroyed.
            InFlightOwnedSceneLoad pendingLoad = inFlightOwnedSceneLoad;
            if (pendingLoad != null)
            {
                pendingLoad.Detached = true;
                pendingLoad.Operation.allowSceneActivation = true;
                inFlightOwnedSceneLoad = null;
            }

            if (automaticRefresh != null)
            {
                StopCoroutine(automaticRefresh);
                automaticRefresh = null;
            }

            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            var ownedPaths = new List<string>(
                ownedLoadedSceneHandles.Keys);
            bool cleanupRequired = unusedAssetCleanupPending;
            var pendingUnloads = new List<AsyncOperation>();
            for (int index = 0; index < ownedPaths.Count; index++)
            {
                string scenePath = ownedPaths[index];
                if (ownedLoadedSceneHandles.TryGetValue(
                        scenePath,
                        out int ownedHandle) &&
                    TryGetLoadedScene(scenePath, out Scene scene) &&
                    scene.handle == ownedHandle)
                {
                    OwnedSceneWillUnload?.Invoke(scene);
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(
                        scene);
                    if (unload != null)
                    {
                        pendingUnloads.Add(unload);
                        cleanupRequired |= IsCellLayerScenePath(scenePath);
                    }
                }
            }
            cleanupRequired |= pendingLoad != null;
            ScheduleDetachedSessionCleanup(pendingUnloads, pendingLoad,
                cleanupRequired);
            unusedAssetCleanupPending = false;

            focus = null;
            boundFocusRigidbody = null;
            ownedLoadedSceneHandles.Clear();
            retainedCellByOwner.Clear();
            isStreaming = false;
            hasObservedCell = false;
            hasObservedLoadingRadius = false;
            reportedFocusSpeedMetersPerSecond = 0f;
            retentionRevision = 0;
            observedRetentionRevision = 0;
        }

        private static void ScheduleDetachedSessionCleanup(
            IReadOnlyList<AsyncOperation> pendingUnloads,
            InFlightOwnedSceneLoad pendingLoad,
            bool cleanupRequired)
        {
            int pendingUnloadCount = pendingUnloads?.Count ?? 0;
            int operationChainCount = pendingUnloadCount +
                                      (pendingLoad == null ? 0 : 1);
            if (operationChainCount == 0)
            {
                if (cleanupRequired)
                {
                    using (unusedAssetsMarker.Auto())
                        Resources.UnloadUnusedAssets();
                }
                return;
            }

            // IGameSessionLifetime is synchronous and its owner is destroyed
            // immediately after this call. AsyncOperation callbacks retain a
            // tiny detached countdown until every requested unload ends. An
            // in-flight load is first forced through activation, then its exact
            // scene path is unloaded as one operation chain. Only after every
            // chain ends is one coalesced reachability scan started.
            var cleanup = new DetachedSessionCleanup(operationChainCount,
                cleanupRequired);
            if (pendingUnloads != null)
            {
                foreach (AsyncOperation unload in pendingUnloads)
                    cleanup.ObserveUnload(unload);
            }
            if (pendingLoad != null)
                cleanup.ObserveLoadThenUnload(pendingLoad);
        }

        private sealed class InFlightOwnedSceneLoad
        {
            public readonly AsyncOperation Operation;
            public readonly string ScenePath;
            public bool Detached;

            public InFlightOwnedSceneLoad(AsyncOperation operation,
                string scenePath)
            {
                Operation = operation ?? throw new ArgumentNullException(
                    nameof(operation));
                ScenePath = !string.IsNullOrWhiteSpace(scenePath)
                    ? scenePath
                    : throw new ArgumentException(
                        "In-flight scene path is required.", nameof(scenePath));
            }
        }

        private sealed class DetachedSessionCleanup
        {
            private int remaining;
            private readonly bool cleanupRequired;

            public DetachedSessionCleanup(int operationCount,
                bool cleanupRequired)
            {
                remaining = operationCount;
                this.cleanupRequired = cleanupRequired;
            }

            public void ObserveUnload(AsyncOperation operation)
            {
                if (operation == null)
                {
                    CompleteChain();
                    return;
                }
                if (operation.isDone)
                {
                    CompleteChain();
                    return;
                }
                operation.completed += HandleUnloadCompleted;
            }

            public void ObserveLoadThenUnload(
                InFlightOwnedSceneLoad pendingLoad)
            {
                AsyncOperation operation = pendingLoad.Operation;
                operation.allowSceneActivation = true;
                if (operation.isDone)
                {
                    UnloadCompletedLoad(pendingLoad.ScenePath);
                    return;
                }
                operation.completed += completed =>
                    UnloadCompletedLoad(pendingLoad.ScenePath);
            }

            private void UnloadCompletedLoad(string scenePath)
            {
                if (!TryGetLoadedScene(scenePath, out Scene scene))
                {
                    CompleteChain();
                    return;
                }

                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload == null || unload.isDone)
                {
                    CompleteChain();
                    return;
                }
                unload.completed += HandleUnloadCompleted;
            }

            private void HandleUnloadCompleted(AsyncOperation operation)
            {
                operation.completed -= HandleUnloadCompleted;
                CompleteChain();
            }

            private void CompleteChain()
            {
                remaining--;
                if (remaining != 0) return;
                if (cleanupRequired)
                {
                    using (unusedAssetsMarker.Auto())
                        Resources.UnloadUnusedAssets();
                }
            }
        }

        private void Update()
        {
            if (focus == null || manifest == null || isStreaming || automaticRefresh != null)
            {
                return;
            }

            if (boundFocusRigidbody != null)
            {
                float rigidbodySpeed =
                    boundFocusRigidbody.linearVelocity.magnitude;
                if (!float.IsFinite(rigidbodySpeed))
                {
                    return;
                }

                reportedFocusSpeedMetersPerSecond =
                    rigidbodySpeed;
            }

            if (!IsFinite(focus.position))
            {
                return;
            }

            WorldCellIndex currentCell =
                WorldCellMembershipUtility.FromPosition(
                    focus.position,
                    manifest.CellSizeMeters);
            int currentLoadingRadius =
                manifest.GetLoadingRadiusForSpeed(
                    reportedFocusSpeedMetersPerSecond);
            if (!hasObservedCell ||
                !currentCell.Equals(observedCell) ||
                !hasObservedLoadingRadius ||
                currentLoadingRadius != observedLoadingRadius ||
                observedRetentionRevision != retentionRevision ||
                !AreGlobalScenesLoaded() ||
                !AreRequiredCellScenesLoaded(
                    currentCell,
                    currentLoadingRadius) ||
                !AreRequiredCellLayersLoaded(currentCell, currentLoadingRadius))
            {
                automaticRefresh = StartCoroutine(RefreshAutomatically());
            }
        }

        private IEnumerator RefreshAutomatically()
        {
            try
            {
                // Re-evaluate the focus after every presentation-layer load.
                // A player moving while a large scene is prepared must not make
                // this coroutine continue loading a stale radius ring.
                yield return RefreshCore(true);
            }
            finally
            {
                automaticRefresh = null;
            }
        }

        private void ValidateRuntimeConfiguration()
        {
            if (manifest == null)
            {
                throw new InvalidOperationException("Production world streaming manifest is not assigned.");
            }

            if (focus == null)
            {
                throw new InvalidOperationException("Production world streaming focus is not bound.");
            }

            if (!IsFinite(focus.position))
            {
                throw new InvalidOperationException(
                    "Production world streaming focus has a non-finite position.");
            }

            IReadOnlyList<string> errors = manifest.ValidateConfiguration();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Production world streaming manifest is invalid: " + string.Join(" | ", errors));
            }

            IReadOnlyList<ProductionWorldGlobalScene> globalScenes =
                manifest.GlobalScenes;
            for (int index = 0; index < globalScenes.Count; index++)
            {
                ProductionWorldGlobalScene globalScene = globalScenes[index];
                ValidateBuildEntry(
                    globalScene.BuildIndex,
                    globalScene.ScenePath,
                    "Global scene " + globalScene.SceneId);
            }

            IReadOnlyList<ProductionWorldCellScene> cells = manifest.Cells;
            for (int index = 0; index < cells.Count; index++)
            {
                ProductionWorldCellScene cell = cells[index];
                ValidateBuildEntry(
                    cell.BuildIndex,
                    cell.ScenePath,
                    "Production cell " + cell.CellId);
            }

            IReadOnlyList<ProductionWorldCellLayerScene> layers = manifest.CellLayers;
            for (int index = 0; index < layers.Count; index++)
            {
                ProductionWorldCellLayerScene layer = layers[index];
                ValidateBuildEntry(layer.BuildIndex, layer.ScenePath,
                    "Cell layer " + layer.LayerId + "/" + layer.CellId);
            }
        }

        private IEnumerator RefreshCellLayers(
            WorldCellIndex center,
            int loadingRadius,
            int maximumNewLoads,
            bool skipDeferredInitialLoads)
        {
            bool newLoadBudgetExhausted = false;
            // Change execution order only, never serialized addresses or ownership.
            // A far forest layer must not delay the focus cell's presentation.
            var layers = new List<ProductionWorldCellLayerScene>(manifest.CellLayers);
            layers.Sort((left, right) =>
            {
                int comparison = CompareLayerPriority(left, right);
                if (comparison != 0)
                {
                    return comparison;
                }

                int leftDistance = Mathf.Max(Mathf.Abs(left.Index.X - center.X), Mathf.Abs(left.Index.Z - center.Z));
                int rightDistance = Mathf.Max(Mathf.Abs(right.Index.X - center.X), Mathf.Abs(right.Index.Z - center.Z));
                comparison = leftDistance.CompareTo(rightDistance);
                return comparison != 0 ? comparison : string.Compare(left.ScenePath, right.ScenePath, StringComparison.Ordinal);
            });
            for (int index = 0; index < layers.Count; index++)
            {
                ProductionWorldCellLayerScene layer = layers[index];
                int distance = Mathf.Max(Mathf.Abs(layer.Index.X - center.X),
                    Mathf.Abs(layer.Index.Z - center.Z));
                bool isLoaded = TryGetLoadedScene(layer.ScenePath, out Scene scene);
                bool retained = IsCellRetained(layer.CellId);
                bool shouldLoad = distance <= layer.GetLoadingRadius(loadingRadius) ||
                                  retained;
                if (shouldLoad && !isLoaded)
                {
                    if (skipDeferredInitialLoads &&
                        layer.DeferInitialLoad &&
                        !retained)
                    {
                        continue;
                    }

                    if (newLoadBudgetExhausted)
                    {
                        continue;
                    }

                    LastRefreshStartedLayerLoadCount++;
                    yield return LoadOwnedScene(
                        layer.BuildIndex,
                        layer.ScenePath,
                        "cell layer " + layer.LayerId + "/" + layer.CellId,
                        true);
                    // Give the just-activated scene a frame before requesting
                    // another layer; GPU uploads have their own shared budget.
                    yield return null;
                    if (LastRefreshStartedLayerLoadCount >= maximumNewLoads)
                    {
                        // Keep scanning so stale owned scenes can unload in the
                        // same batch. The next automatic refresh recomputes the
                        // focus before it starts another new layer load.
                        newLoadBudgetExhausted = true;
                    }
                }
                else if (distance > layer.GetUnloadingRadius(manifest.UnloadingRadiusCells) &&
                         isLoaded && OwnsLoadedScene(scene))
                {
                    OwnedSceneWillUnload?.Invoke(scene);
                    if (IsCellRetained(layer.CellId))
                    {
                        continue;
                    }

                    int handle = scene.handle;
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                    if (unload == null)
                    {
                        throw new InvalidOperationException(
                            "Could not unload cell layer " + layer.LayerId + "/" + layer.CellId + ".");
                    }

                    yield return unload;
                    RemoveOwnershipIfHandleMatches(layer.ScenePath, handle);
                    // Do not start Resources.UnloadUnusedAssets in a critical
                    // cell transition. Coalesce the reachability scan at the
                    // explicit teardown/cleanup boundary instead.
                    unusedAssetCleanupPending = true;
                }
            }
        }

        private static int CompareLayerPriority(
            ProductionWorldCellLayerScene left,
            ProductionWorldCellLayerScene right)
        {
            int leftPriority = GetLayerPriority(left.LayerId);
            int rightPriority = GetLayerPriority(right.LayerId);
            int comparison = leftPriority.CompareTo(rightPriority);
            if (comparison != 0)
            {
                return comparison;
            }

            // Unknown optional layers stay deterministic without acquiring
            // priority over either project-owned vegetation tier.
            return leftPriority >= 2
                ? string.Compare(left.LayerId, right.LayerId,
                    StringComparison.Ordinal)
                : 0;
        }

        private static int GetLayerPriority(string layerId)
        {
            if (string.Equals(layerId, "vegetation", StringComparison.Ordinal))
            {
                return 0;
            }

            if (string.Equals(layerId, "vegetation-backdrop",
                    StringComparison.Ordinal))
            {
                return 1;
            }

            return 2;
        }

        private IEnumerator UnloadUnusedAssetsAfterLayerBatch()
        {
            long started = Stopwatch.GetTimestamp();
            AsyncOperation cleanup;
            using (unusedAssetsMarker.Auto()) cleanup = Resources.UnloadUnusedAssets();
            yield return cleanup;
            LastUnusedAssetCleanupElapsedMilliseconds = ElapsedMilliseconds(started);
            CompletedUnusedAssetCleanupCount++;
        }

        private IEnumerator EnsureGlobalScenesLoaded()
        {
            IReadOnlyList<ProductionWorldGlobalScene> globalScenes =
                manifest.GlobalScenes;
            for (int index = 0; index < globalScenes.Count; index++)
            {
                ProductionWorldGlobalScene globalScene = globalScenes[index];
                if (TryGetLoadedScene(globalScene.ScenePath, out _))
                {
                    continue;
                }

                yield return LoadOwnedScene(
                    globalScene.BuildIndex,
                    globalScene.ScenePath,
                    "global scene " + globalScene.SceneId);
            }
        }

        private IEnumerator LoadOwnedScene(
            int buildIndex,
            string scenePath,
            string label,
            bool preparePresentationActivation = false)
        {
            if (inFlightOwnedSceneLoad != null)
            {
                throw new InvalidOperationException(
                    "Production world streaming already owns an in-flight " +
                    "scene load at " + inFlightOwnedSceneLoad.ScenePath + ".");
            }
            LastSceneLoadPath = scenePath;
            LastSceneLoadStartedFrame = Time.frameCount;
            LastSceneActivationPreparedFrame = -1;
            long started = Stopwatch.GetTimestamp();
            AsyncOperation load;
            using (startSceneLoadMarker.Auto())
                load = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Additive);
            if (load == null)
            {
                throw new InvalidOperationException(
                    $"Could not start additive load for {label} at build " +
                    $"index {buildIndex} ({scenePath}).");
            }
            var trackedLoad = new InFlightOwnedSceneLoad(load, scenePath);
            inFlightOwnedSceneLoad = trackedLoad;
            try
            {
                if (preparePresentationActivation)
                {
                    // Unity scene deserialization can run in the background,
                    // but its final Awake/activation integration is indivisible.
                    // Keep background work low-priority and gate that final
                    // integration to a later frame so it cannot share the
                    // load-request frame.
                    load.priority = PresentationLayerAsyncOperationPriority;
                    load.allowSceneActivation = false;
                    while (load.progress < 0.9f && !trackedLoad.Detached)
                    {
                        yield return null;
                    }

                    if (trackedLoad.Detached) yield break;
                    yield return null;
                    if (trackedLoad.Detached) yield break;
                    LastSceneActivationPreparedFrame = Time.frameCount;
                    load.allowSceneActivation = true;
                }

                yield return load;
                if (trackedLoad.Detached) yield break;
                if (!TryGetLoadedScene(scenePath, out Scene loadedScene))
                {
                    throw new InvalidOperationException(
                        $"{label} did not become loaded at its authoritative " +
                        $"scene path {scenePath}.");
                }

                LastSceneLoadCompletedFrame = Time.frameCount;
                LastSceneLoadElapsedMilliseconds = ElapsedMilliseconds(started);
                CompletedSceneLoadCount++;
                ownedLoadedSceneHandles[scenePath] = loadedScene.handle;
                long notificationStarted = Stopwatch.GetTimestamp();
                try
                {
                    using (sceneLoadedNotificationMarker.Auto())
                        OwnedSceneLoaded?.Invoke(loadedScene);
                }
                finally
                {
                    LastSceneNotificationCpuMilliseconds =
                        ElapsedMilliseconds(notificationStarted);
                    MaximumSceneNotificationCpuMilliseconds = Math.Max(
                        MaximumSceneNotificationCpuMilliseconds,
                        LastSceneNotificationCpuMilliseconds);
                }
            }
            finally
            {
                if (ReferenceEquals(inFlightOwnedSceneLoad, trackedLoad))
                    inFlightOwnedSceneLoad = null;
            }
        }

        private static double ElapsedMilliseconds(long started) =>
            (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;

        private void HandleSceneUnloaded(Scene scene)
        {
            RemoveOwnershipIfHandleMatches(
                scene.path,
                scene.handle);
        }

        private bool OwnsLoadedScene(Scene scene)
        {
            if (!ownedLoadedSceneHandles.TryGetValue(
                    scene.path,
                    out int ownedHandle))
            {
                return false;
            }
            if (ownedHandle == scene.handle)
            {
                return true;
            }

            ownedLoadedSceneHandles.Remove(scene.path);
            return false;
        }

        private void PruneStaleOwnership()
        {
            if (ownedLoadedSceneHandles.Count == 0)
            {
                return;
            }

            var stalePaths = new List<string>();
            foreach (KeyValuePair<string, int> owned in
                     ownedLoadedSceneHandles)
            {
                if (!TryGetLoadedScene(
                        owned.Key,
                        out Scene scene) ||
                    scene.handle != owned.Value)
                {
                    stalePaths.Add(owned.Key);
                }
            }

            for (int index = 0;
                 index < stalePaths.Count;
                 index++)
            {
                ownedLoadedSceneHandles.Remove(stalePaths[index]);
            }
        }

        private void RemoveOwnershipIfHandleMatches(
            string scenePath,
            int sceneHandle)
        {
            if (!string.IsNullOrWhiteSpace(scenePath) &&
                ownedLoadedSceneHandles.TryGetValue(
                    scenePath,
                    out int ownedHandle) &&
                ownedHandle == sceneHandle)
            {
                ownedLoadedSceneHandles.Remove(scenePath);
            }
        }

        private WorldCellIndex GetCurrentFocusCell()
        {
            Vector3 position = focus.position;
            if (!IsFinite(position))
            {
                throw new InvalidOperationException(
                    "Production world streaming focus has a non-finite position.");
            }

            return WorldCellMembershipUtility.FromPosition(
                position,
                manifest.CellSizeMeters);
        }

        private int CompareOwnedUnloadOrder(string left, string right)
        {
            bool leftIsGlobal = IsGlobalScenePath(left);
            bool rightIsGlobal = IsGlobalScenePath(right);
            if (leftIsGlobal != rightIsGlobal)
            {
                return leftIsGlobal ? 1 : -1;
            }

            return string.Compare(left, right, StringComparison.Ordinal);
        }

        private bool IsGlobalScenePath(string scenePath)
        {
            if (manifest == null)
            {
                return false;
            }

            IReadOnlyList<ProductionWorldGlobalScene> globalScenes =
                manifest.GlobalScenes;
            for (int index = 0; index < globalScenes.Count; index++)
            {
                if (string.Equals(
                        globalScenes[index].ScenePath,
                        scenePath,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsCellLayerScenePath(string scenePath)
        {
            if (manifest == null) return false;
            IReadOnlyList<ProductionWorldCellLayerScene> layers = manifest.CellLayers;
            for (int index = 0; index < layers.Count; index++)
                if (string.Equals(layers[index].ScenePath, scenePath, StringComparison.Ordinal)) return true;
            return false;
        }

        private bool AreGlobalScenesLoaded()
        {
            IReadOnlyList<ProductionWorldGlobalScene> globalScenes =
                manifest.GlobalScenes;
            for (int index = 0; index < globalScenes.Count; index++)
            {
                if (!TryGetLoadedScene(
                        globalScenes[index].ScenePath,
                        out _))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AreRequiredCellScenesLoaded(
            WorldCellIndex center,
            int loadingRadius)
        {
            IReadOnlyList<ProductionWorldCellScene> cells =
                manifest.Cells;
            for (int index = 0; index < cells.Count; index++)
            {
                ProductionWorldCellScene cell = cells[index];
                int distance = Mathf.Max(
                    Mathf.Abs(cell.Index.X - center.X),
                    Mathf.Abs(cell.Index.Z - center.Z));
                if ((distance <= loadingRadius ||
                     IsCellRetained(cell.CellId)) &&
                    !TryGetLoadedScene(cell.ScenePath, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private bool AreRequiredCellLayersLoaded(WorldCellIndex center, int loadingRadius)
        {
            IReadOnlyList<ProductionWorldCellLayerScene> layers = manifest.CellLayers;
            for (int index = 0; index < layers.Count; index++)
            {
                ProductionWorldCellLayerScene layer = layers[index];
                int distance = Mathf.Max(Mathf.Abs(layer.Index.X - center.X),
                    Mathf.Abs(layer.Index.Z - center.Z));
                if ((distance <= layer.GetLoadingRadius(loadingRadius) ||
                     IsCellRetained(layer.CellId)) &&
                    !TryGetLoadedScene(layer.ScenePath, out _))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Read-only diagnostic for explicit persistent-state retention.
        /// </summary>
        public bool IsCellRetained(string cellId)
        {
            foreach (string retainedCellId in retainedCellByOwner.Values)
            {
                if (string.Equals(
                        retainedCellId,
                        cellId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateBuildEntry(
            int buildIndex,
            string scenePath,
            string label)
        {
            string buildPath = SceneUtility.GetScenePathByBuildIndex(buildIndex);
            if (!string.Equals(buildPath, scenePath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{label} expects build index {buildIndex} to resolve to " +
                    $"{scenePath}, but it resolves to " +
                    $"{buildPath ?? "<null>"}.");
            }
        }

        private static bool TryGetLoadedScene(string scenePath, out Scene scene)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene candidate = SceneManager.GetSceneAt(index);
                if (candidate.isLoaded &&
                    string.Equals(
                        candidate.path,
                        scenePath,
                        StringComparison.Ordinal))
                {
                    scene = candidate;
                    return true;
                }
            }

            scene = default;
            return false;
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }
}
