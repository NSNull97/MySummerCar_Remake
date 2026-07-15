using UnityEngine;

namespace MSC.World.Debugging
{
    public enum WorldReferenceVisualizationKind
    {
        ActualMesh = 0,
        BoundsFallback = 1
    }

    [DisallowMultipleComponent]
    public sealed class WorldReferenceEntity : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private long donorObjectId;
        [SerializeField] private string donorHierarchyPath = string.Empty;
        [SerializeField] private string semanticCategory = string.Empty;
        [SerializeField] private string replacementStatus = string.Empty;
        [SerializeField] private string transferStatus = string.Empty;
        [SerializeField] private WorldReferenceVisualizationKind visualizationKind;
        [SerializeField] private string resolvedMeshGuid = string.Empty;

        public string StableId => stableId;
        public long DonorObjectId => donorObjectId;
        public string DonorHierarchyPath => donorHierarchyPath;
        public string SemanticCategory => semanticCategory;
        public string ReplacementStatus => replacementStatus;
        public string TransferStatus => transferStatus;
        public WorldReferenceVisualizationKind VisualizationKind => visualizationKind;
        public string ResolvedMeshGuid => resolvedMeshGuid;

        public void Configure(
            string id,
            long sourceObjectId,
            string hierarchyPath,
            string category,
            string replacement,
            string transfer,
            WorldReferenceVisualizationKind visualization = WorldReferenceVisualizationKind.BoundsFallback,
            string meshGuid = "")
        {
            stableId = id;
            donorObjectId = sourceObjectId;
            donorHierarchyPath = hierarchyPath;
            semanticCategory = category;
            replacementStatus = replacement;
            transferStatus = transfer;
            visualizationKind = visualization;
            resolvedMeshGuid = meshGuid;
        }
    }

    [DisallowMultipleComponent]
    public sealed class WorldGeneratedSceneStamp : MonoBehaviour
    {
        [SerializeField] private string databaseVersion = string.Empty;
        [SerializeField] private string generatorVersion = string.Empty;
        [SerializeField] private string cellId = string.Empty;
        [SerializeField] private int generatedEntityCount;
        [SerializeField] private int actualMeshCount;
        [SerializeField] private int boundsFallbackCount;

        public string DatabaseVersion => databaseVersion;
        public string GeneratorVersion => generatorVersion;
        public string CellId => cellId;
        public int GeneratedEntityCount => generatedEntityCount;
        public int ActualMeshCount => actualMeshCount;
        public int BoundsFallbackCount => boundsFallbackCount;

        public void Configure(string database, string generator, string cell, int count, int actualMeshes = 0, int boundsFallbacks = 0)
        {
            databaseVersion = database;
            generatorVersion = generator;
            cellId = cell;
            generatedEntityCount = count;
            actualMeshCount = actualMeshes;
            boundsFallbackCount = boundsFallbacks;
        }
    }
}
