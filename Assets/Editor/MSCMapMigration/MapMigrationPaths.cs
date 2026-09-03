using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MSCMapMigration
{
    internal static class MapMigrationPaths
    {
        public const string GeneratedRoot =
            "Assets/_Generated/MapTerrainMigration";
        public const string ReportsRoot = GeneratedRoot + "/Reports";
        public const string InventoryJson = ReportsRoot + "/MapMeshInventory.json";
        public const string InventoryCsv = ReportsRoot + "/MapMeshInventory.csv";
        public const string ValidationJson = ReportsRoot + "/MapMigrationValidation.json";
        public const string ValidationMarkdown = ReportsRoot + "/MapMigrationReport.md";
        public const string VisualComparisonOverview =
            ReportsRoot + "/MapTerrainComparison_Overview.png";
        public const string VisualComparisonHeightDetail =
            ReportsRoot + "/MapTerrainComparison_HeightOutlier.png";
        public const string VisualComparisonRoadDetail =
            ReportsRoot + "/MapTerrainComparison_RoadGap.png";
        public const string VisualComparisonMarkdown =
            ReportsRoot + "/MapTerrainVisualComparison.md";
        public const string TerrainDataRoot = GeneratedRoot + "/TerrainData";
        public const string TerrainLayerRoot = GeneratedRoot + "/TerrainLayers";
        public const string ResidualMeshRoot = GeneratedRoot + "/ResidualMeshes";
        public const string ToolManifest = GeneratedRoot + "/MapMigrationGeneratedManifest.json";
        public const string GeneratedRootObjectName = "MSC_MAP_TERRAIN_MIGRATION_GENERATED";
        public const string TerrainRootObjectName = "Terrain_Generated";

        public static string ProjectRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        public static string ToAbsoluteProjectPath(string projectRelativePath) =>
            Path.GetFullPath(Path.Combine(
                ProjectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));

        public static void EnsureAssetFolder(string assetPath)
        {
            string normalized = assetPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            string parent = Path.GetDirectoryName(normalized)!.Replace('\\', '/');
            string leaf = Path.GetFileName(normalized);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        public static void EnsureFilesystemDirectoryForFile(string assetOrRelativePath)
        {
            string absolute = ToAbsoluteProjectPath(assetOrRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        }

        public static string ComputeSha256(string assetOrAbsolutePath)
        {
            string absolute = Path.IsPathRooted(assetOrAbsolutePath)
                ? assetOrAbsolutePath
                : ToAbsoluteProjectPath(assetOrAbsolutePath);
            using FileStream stream = File.OpenRead(absolute);
            using SHA256 sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(stream);
            var builder = new StringBuilder(hash.Length * 2);
            foreach (byte value in hash)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }

        public static string StableHash(string value)
        {
            using SHA256 sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
            var builder = new StringBuilder(16);
            for (int index = 0; index < 8; index++)
            {
                builder.Append(bytes[index].ToString("x2"));
            }

            return builder.ToString();
        }

        public static string NormalizeRelative(string path) =>
            path.Replace('\\', '/');
    }

    internal static class MapMigrationProgress
    {
        public static void Check(string title, string info, float progress)
        {
            if (Application.isBatchMode)
            {
                if (Mathf.Approximately(progress, 0f) || progress >= 1f)
                {
                    Debug.Log($"[Map Migration] {title}: {info}");
                }

                return;
            }

            if (EditorUtility.DisplayCancelableProgressBar(
                    title,
                    info,
                    Mathf.Clamp01(progress)))
            {
                throw new MapMigrationCancelledException();
            }
        }

        public static void Clear()
        {
            if (!Application.isBatchMode)
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
