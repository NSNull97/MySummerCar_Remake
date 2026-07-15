using System;
using System.IO;
using UnityEngine;

namespace MSC.Editor.WorldTransfer
{
    public static class WorldTransferPaths
    {
        public const string DatabaseAssetPath = "Assets/Game/World/Content/WorldTransfer/M04A1_WorldGeometryDatabase.json";
        public const string EntityTableAssetPath = "Assets/Game/World/Content/WorldTransfer/M04A1_WorldEntities.csv";
        public const string ColliderTableAssetPath = "Assets/Game/World/Content/WorldTransfer/M04A1_WorldColliders.csv";
        public const string LandmarkTableAssetPath = "Assets/Game/World/Content/WorldTransfer/M04A1_WorldLandmarks.csv";
        public const string StaticBatchSubsetTableAssetPath = "Assets/Game/World/Content/WorldTransfer/M05C_StaticBatchSubsets.csv";
        public const string ReferenceRoot = "Assets/Game/LegacyImport/ReferenceOnly/World";
        public const string ReferenceMeshRoot = ReferenceRoot + "/MeshLibrary";
        public const string GeneratedRoot = ReferenceRoot + "/Generated";
        public const string GeneratedSceneRoot = GeneratedRoot + "/Scenes";
        public const string GeneratedMaterialRoot = GeneratedRoot + "/Materials";
        public const string GeneratedMeshRoot = GeneratedRoot + "/Meshes";
        public const string BootstrapScene = GeneratedSceneRoot + "/WorldTransfer_Bootstrap.unity";
        public const string PersistentScene = GeneratedSceneRoot + "/World_Persistent.unity";
        public const string GlobalScene = GeneratedSceneRoot + "/World_GlobalReference.unity";
        public const string LocalConfigRelativePath = "Config/WorldTransferConfig.local.json";
        public const string ExampleConfigRelativePath = "Config/WorldTransferConfig.example.json";
        public const string RulesRelativePath = "Config/WorldTransferRules.example.json";
        public const string ReportsRelativePath = "Docs/WorldTransfer";
        public const string GeometryEvaluationCaptureRoot = "PerformanceCaptures/Milestone05C";

        public static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName ??
            throw new InvalidOperationException("Unity project root could not be resolved.");

        public static string ToAbsoluteProjectPath(string relativePath)
        {
            string candidate = Path.GetFullPath(Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            string root = Path.GetFullPath(ProjectRoot).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Path escapes the Unity project root.", nameof(relativePath));
            }

            return candidate;
        }

        public static string CellScene(string cellId) => GeneratedSceneRoot + "/World_" + cellId + ".unity";
    }
}
