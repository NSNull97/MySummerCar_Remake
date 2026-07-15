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

        private readonly HashSet<int> ownedLoadedBuildIndices = new HashSet<int>();
        private bool isStreaming;
        private bool hasObservedCell;
        private WorldCellIndex observedCell;
        private Coroutine automaticRefresh;

        public bool IsStreaming => isStreaming;
        public bool HasFocus => focus != null;
        public int OwnedLoadedSceneCount => ownedLoadedBuildIndices.Count;
        public ProductionWorldStreamingManifest Manifest => manifest;
        public Transform Focus => focus;

        public void BindFocus(Transform streamingFocus)
        {
            focus = streamingFocus != null
                ? streamingFocus
                : throw new ArgumentNullException(nameof(streamingFocus));
            hasObservedCell = false;
        }

        public bool IsCellLoaded(string cellId)
        {
            return manifest != null &&
                   manifest.TryGetCell(cellId, out ProductionWorldCellScene cell) &&
                   TryGetLoadedScene(cell.BuildIndex, out _);
        }

        public IEnumerator RefreshNow()
        {
            if (isStreaming)
            {
                yield break;
            }

            ValidateRuntimeConfiguration();
            isStreaming = true;
            try
            {
                WorldCellIndex center = WorldCellMembershipUtility.FromPosition(focus.position, manifest.CellSizeMeters);
                observedCell = center;
                hasObservedCell = true;

                IReadOnlyList<ProductionWorldCellScene> cells = manifest.Cells;
                for (int index = 0; index < cells.Count; index++)
                {
                    ProductionWorldCellScene cell = cells[index];
                    ValidateBuildEntry(cell);
                    int distance = Mathf.Max(
                        Mathf.Abs(cell.Index.X - center.X),
                        Mathf.Abs(cell.Index.Z - center.Z));

                    bool isLoaded = TryGetLoadedScene(cell.BuildIndex, out Scene loadedScene);
                    if (distance <= manifest.LoadingRadiusCells && !isLoaded)
                    {
                        AsyncOperation load = SceneManager.LoadSceneAsync(cell.BuildIndex, LoadSceneMode.Additive);
                        if (load == null)
                        {
                            throw new InvalidOperationException(
                                $"Could not start additive load for production cell {cell.CellId} at build index {cell.BuildIndex}.");
                        }

                        yield return load;
                        if (!TryGetLoadedScene(cell.BuildIndex, out _))
                        {
                            throw new InvalidOperationException(
                                $"Production cell {cell.CellId} did not become loaded after its load operation completed.");
                        }

                        ownedLoadedBuildIndices.Add(cell.BuildIndex);
                    }
                    else if (distance > manifest.UnloadingRadiusCells &&
                             isLoaded &&
                             ownedLoadedBuildIndices.Contains(cell.BuildIndex))
                    {
                        AsyncOperation unload = SceneManager.UnloadSceneAsync(loadedScene);
                        if (unload == null)
                        {
                            throw new InvalidOperationException(
                                $"Could not start unload for owned production cell {cell.CellId}.");
                        }

                        yield return unload;
                        ownedLoadedBuildIndices.Remove(cell.BuildIndex);
                    }
                }
            }
            finally
            {
                isStreaming = false;
            }
        }

        public IEnumerator UnloadOwnedScenes()
        {
            if (isStreaming)
            {
                yield break;
            }

            isStreaming = true;
            try
            {
                var ownedIndices = new List<int>(ownedLoadedBuildIndices);
                for (int index = 0; index < ownedIndices.Count; index++)
                {
                    int buildIndex = ownedIndices[index];
                    if (TryGetLoadedScene(buildIndex, out Scene scene))
                    {
                        AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                        if (unload != null)
                        {
                            yield return unload;
                        }
                    }

                    ownedLoadedBuildIndices.Remove(buildIndex);
                }
            }
            finally
            {
                isStreaming = false;
                hasObservedCell = false;
            }
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(ProductionWorldStreamingManifest configuredManifest)
        {
            manifest = configuredManifest;
        }
#endif

        private void Update()
        {
            if (focus == null || manifest == null || isStreaming || automaticRefresh != null)
            {
                return;
            }

            WorldCellIndex currentCell = WorldCellMembershipUtility.FromPosition(focus.position, manifest.CellSizeMeters);
            if (!hasObservedCell || !currentCell.Equals(observedCell))
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

            IReadOnlyList<string> errors = manifest.ValidateConfiguration();
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Production world streaming manifest is invalid: " + string.Join(" | ", errors));
            }
        }

        private static void ValidateBuildEntry(ProductionWorldCellScene cell)
        {
            string buildPath = SceneUtility.GetScenePathByBuildIndex(cell.BuildIndex);
            if (!string.Equals(buildPath, cell.ScenePath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Production cell {cell.CellId} expects build index {cell.BuildIndex} to resolve to " +
                    $"{cell.ScenePath}, but it resolves to {buildPath ?? "<null>"}.");
            }
        }

        private static bool TryGetLoadedScene(int buildIndex, out Scene scene)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene candidate = SceneManager.GetSceneAt(index);
                if (candidate.buildIndex == buildIndex && candidate.isLoaded)
                {
                    scene = candidate;
                    return true;
                }
            }

            scene = default;
            return false;
        }
    }
}
