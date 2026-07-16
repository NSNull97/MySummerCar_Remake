using System;
using System.Collections;
using System.Collections.Generic;
using MSC.World.Partition;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.World.Streaming
{
    /// <summary>
    /// Loads the explicitly catalogued production cells around an explicitly bound focus.
    /// The service unloads only scenes that it loaded itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProductionWorldStreamingService : MonoBehaviour, IWorldStreamingService
    {
        [SerializeField] private ProductionWorldStreamingManifest manifest;
        [SerializeField] private Transform focus;

        private readonly Dictionary<string, int> ownedLoadedSceneHandles =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private bool isStreaming;
        private bool hasObservedCell;
        private bool hasObservedLoadingRadius;
        private WorldCellIndex observedCell;
        private int observedLoadingRadius;
        private float reportedFocusSpeedMetersPerSecond;
        private Rigidbody boundFocusRigidbody;
        private Coroutine automaticRefresh;

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
        public int EffectiveLoadingRadiusCells =>
            manifest == null
                ? 0
                : manifest.GetLoadingRadiusForSpeed(
                    reportedFocusSpeedMetersPerSecond);

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
            while (isStreaming)
            {
                yield return null;
            }

            ValidateRuntimeConfiguration();
            PruneStaleOwnership();
            isStreaming = true;
            try
            {
                WorldCellIndex center = GetCurrentFocusCell();
                int loadingRadius = manifest.GetLoadingRadiusForSpeed(
                    reportedFocusSpeedMetersPerSecond);

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
                    if (distance <= loadingRadius && !isLoaded)
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

                observedCell = center;
                hasObservedCell = true;
                observedLoadingRadius = loadingRadius;
                hasObservedLoadingRadius = true;
            }
            finally
            {
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
                        AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                        if (unload != null)
                        {
                            yield return unload;
                        }
                    }

                    RemoveOwnershipIfHandleMatches(
                        scenePath,
                        ownedHandle);
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
                !AreGlobalScenesLoaded() ||
                !AreRequiredCellScenesLoaded(
                    currentCell,
                    currentLoadingRadius))
            {
                automaticRefresh = StartCoroutine(RefreshAutomatically());
            }
        }

        private IEnumerator RefreshAutomatically()
        {
            try
            {
                yield return RefreshNow();
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
            string label)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                buildIndex,
                LoadSceneMode.Additive);
            if (load == null)
            {
                throw new InvalidOperationException(
                    $"Could not start additive load for {label} at build " +
                    $"index {buildIndex} ({scenePath}).");
            }

            yield return load;
            if (!TryGetLoadedScene(scenePath, out Scene loadedScene))
            {
                throw new InvalidOperationException(
                    $"{label} did not become loaded at its authoritative " +
                    $"scene path {scenePath}.");
            }

            ownedLoadedSceneHandles[scenePath] = loadedScene.handle;
        }

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
                if (distance <= loadingRadius &&
                    !TryGetLoadedScene(cell.ScenePath, out _))
                {
                    return false;
                }
            }

            return true;
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
