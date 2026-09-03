using System;
using MSC.World.Partition;
using UnityEngine;

namespace MSC.World.Streaming
{
    /// <summary>
    /// Optional presentation scene owned by a spatial cell. It may exist where
    /// the legacy map has no cell-owned objects; it never adds a save domain.
    /// </summary>
    [Serializable]
    public struct ProductionWorldCellLayerScene
    {
        [SerializeField] private string layerId;
        [SerializeField] private string cellId;
        [SerializeField] private int x;
        [SerializeField] private int z;
        [SerializeField] private int buildIndex;
        [SerializeField] private string scenePath;
        [SerializeField, Min(0)] private int minimumLoadingRadiusCells;
        [SerializeField, Min(0)] private int minimumUnloadingRadiusCells;
        [SerializeField] private bool deferInitialLoad;

        public ProductionWorldCellLayerScene(
            string layer,
            WorldCellIndex cell,
            int index,
            string path,
            int minimumLoadingRadiusCells = 0,
            int minimumUnloadingRadiusCells = 0,
            bool deferInitialLoad = false)
        {
            layerId = layer;
            cellId = cell.Id;
            x = cell.X;
            z = cell.Z;
            buildIndex = index;
            scenePath = path;
            this.minimumLoadingRadiusCells = minimumLoadingRadiusCells;
            this.minimumUnloadingRadiusCells = minimumUnloadingRadiusCells;
            this.deferInitialLoad = deferInitialLoad;
        }

        public string LayerId => layerId;
        public WorldCellIndex Index => new WorldCellIndex(x, z);
        public string CellId => cellId;
        public int BuildIndex => buildIndex;
        public string ScenePath => scenePath;
        public int MinimumLoadingRadiusCells => minimumLoadingRadiusCells;
        public int MinimumUnloadingRadiusCells => minimumUnloadingRadiusCells;
        public bool DeferInitialLoad => deferInitialLoad;

        public int GetLoadingRadius(int manifestLoadingRadius) =>
            Mathf.Max(manifestLoadingRadius, minimumLoadingRadiusCells);

        public int GetUnloadingRadius(int manifestUnloadingRadius) =>
            Mathf.Max(manifestUnloadingRadius,
                Mathf.Max(minimumLoadingRadiusCells, minimumUnloadingRadiusCells));
    }
}
