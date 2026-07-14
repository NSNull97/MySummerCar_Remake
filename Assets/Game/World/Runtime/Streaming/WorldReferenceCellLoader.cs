using System;
using System.Collections;
using System.Collections.Generic;
using MSC.World.Partition;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.World.Streaming
{
    [Serializable]
    public struct WorldReferenceCellScene
    {
        [SerializeField] private int x;
        [SerializeField] private int z;
        [SerializeField] private string sceneName;

        public WorldReferenceCellScene(int cellX, int cellZ, string name)
        {
            x = cellX;
            z = cellZ;
            sceneName = name;
        }

        public WorldCellIndex Index => new WorldCellIndex(x, z);
        public string SceneName => sceneName;
    }

    [DisallowMultipleComponent]
    public sealed class WorldReferenceCellLoader : MonoBehaviour, IWorldStreamingService
    {
        [SerializeField] private Transform focus;
        [SerializeField, Min(1f)] private float cellSizeMeters = 512f;
        [SerializeField, Min(0)] private int loadingRadiusCells = 1;
        [SerializeField, Min(0)] private int unloadingRadiusCells = 2;
        [SerializeField] private WorldReferenceCellScene[] cellScenes = Array.Empty<WorldReferenceCellScene>();

        private readonly HashSet<string> ownedLoadedScenes = new HashSet<string>(StringComparer.Ordinal);
        private bool isStreaming;

        public bool IsStreaming => isStreaming;

        public void Configure(Transform streamingFocus, float cellSize, int loadRadius, int unloadRadius, WorldReferenceCellScene[] scenes)
        {
            focus = streamingFocus;
            cellSizeMeters = cellSize;
            loadingRadiusCells = loadRadius;
            unloadingRadiusCells = Mathf.Max(loadRadius, unloadRadius);
            cellScenes = scenes ?? Array.Empty<WorldReferenceCellScene>();
        }

        public IEnumerator RefreshNow()
        {
            if (focus == null || isStreaming)
            {
                yield break;
            }

            isStreaming = true;
            WorldCellIndex center = WorldCellMembershipUtility.FromPosition(focus.position, cellSizeMeters);
            for (int index = 0; index < cellScenes.Length; index++)
            {
                WorldReferenceCellScene cell = cellScenes[index];
                int distance = Mathf.Max(Mathf.Abs(cell.Index.X - center.X), Mathf.Abs(cell.Index.Z - center.Z));
                Scene scene = SceneManager.GetSceneByName(cell.SceneName);
                if (distance <= loadingRadiusCells && !scene.isLoaded)
                {
                    AsyncOperation load = SceneManager.LoadSceneAsync(cell.SceneName, LoadSceneMode.Additive);
                    if (load != null)
                    {
                        yield return load;
                        ownedLoadedScenes.Add(cell.SceneName);
                    }
                }
                else if (distance > unloadingRadiusCells && scene.isLoaded && ownedLoadedScenes.Contains(cell.SceneName))
                {
                    AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                    if (unload != null)
                    {
                        yield return unload;
                    }
                    ownedLoadedScenes.Remove(cell.SceneName);
                }
            }
            isStreaming = false;
        }
    }
}
