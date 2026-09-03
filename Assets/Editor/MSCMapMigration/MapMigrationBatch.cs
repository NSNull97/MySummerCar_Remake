using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MSCMapMigration
{
    public static class MapMigrationBatch
    {
        public static void RunFullPipeline()
        {
            MapMigrationSettings settingsAsset = MapMigrationSettings.LoadOrCreate();
            IMapMigrationSettings settings = settingsAsset.CreateSnapshot();
            MapScanResult scan = null;
            try
            {
                settingsAsset.ValidateOrThrow();
                scan = MapMeshScanner.Scan(settings);
                MapMeshInventoryWriter.Write(scan.Inventory);
                AssetDatabase.Refresh();

                if (settings.DryRun)
                {
                    Debug.Log(
                        "MAP_MIGRATION_DRY_RUN_OK " +
                        $"instances={scan.Inventory.summary.meshInstanceCount} " +
                        $"ground={scan.Inventory.summary.groundCandidateCount} " +
                        $"roads={scan.Inventory.summary.roadMeshCount}");
                    return;
                }

                MapExportManifest export = BlenderMapExporter.Export(settings, scan);
                PersistExportReference(export, scan.Inventory.sourceSetFingerprint);
                settings = MapMigrationSettings.LoadOrCreate().CreateSnapshot();
                TerrainBuildResult build = TerrainGridBuilder.BuildPreview(settings, scan);
                MapMeshInventoryWriter.Write(scan.Inventory);
                MapMigrationValidationReport preview = MapMigrationValidator.Validate(
                    settings,
                    scan.Inventory,
                    build,
                    requireCommitted: false);
                if (!preview.passed)
                {
                    throw new InvalidDataException(
                        "Terrain preview validation failed; source renderers were not disabled. " +
                        string.Join(" | ", preview.errors.Take(5)));
                }

                int committed = TerrainGridBuilder.CommitReplacement(settings, preview);
                MapMigrationValidationReport final = MapMigrationValidator.Validate(
                    settings,
                    scan.Inventory,
                    build,
                    requireCommitted: true);
                if (!final.passed)
                {
                    throw new InvalidDataException(
                        "Committed migration validation failed: " +
                        string.Join(" | ", final.errors.Take(5)));
                }

                Debug.Log(
                    "MAP_TERRAIN_MIGRATION_PIPELINE_PASS " +
                    $"instances={scan.Inventory.summary.meshInstanceCount} " +
                    $"ground={scan.Inventory.summary.groundCandidateCount} " +
                    $"roads={scan.Inventory.summary.roadMeshCount} " +
                    $"exported={export.instanceCount} " +
                    $"tiles={final.terrainTileCount} " +
                    $"committedGround={committed} " +
                    $"meanError={final.meanHeightError:R} " +
                    $"maxError={final.maximumHeightError:R} " +
                    $"exportRoot={export.exportRoot}");
            }
            catch (Exception exception)
            {
                WriteFailureReport(exception);
                Debug.LogException(exception);
                throw;
            }
            finally
            {
                MapMeshScanner.Release(scan);
                MapMigrationProgress.Clear();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        public static void ScanSourceMap()
        {
            RunWithScan((settings, scan) =>
            {
                MapMeshInventoryWriter.Write(scan.Inventory);
                Debug.Log(
                    "MAP_MIGRATION_SCAN_OK " +
                    $"instances={scan.Inventory.summary.meshInstanceCount} " +
                    $"ground={scan.Inventory.summary.groundCandidateCount} " +
                    $"roads={scan.Inventory.summary.roadMeshCount}");
            });
        }

        public static void ExportSourceMeshesForBlender()
        {
            RunWithScan((settings, scan) =>
            {
                MapMeshInventoryWriter.Write(scan.Inventory);
                MapExportManifest manifest = BlenderMapExporter.Export(settings, scan);
                PersistExportReference(manifest, scan.Inventory.sourceSetFingerprint);
                Debug.Log(
                    $"MAP_MIGRATION_EXPORT_OK instances={manifest.instanceCount} " +
                    $"root={manifest.exportRoot} format={manifest.preferredFormat}");
            });
        }

        public static void BuildTerrainPreview()
        {
            RunWithScan((settings, scan) =>
            {
                MapMeshInventoryWriter.Write(scan.Inventory);
                TerrainBuildResult build = TerrainGridBuilder.BuildPreview(settings, scan);
                MapMeshInventoryWriter.Write(scan.Inventory);
                Debug.Log(
                    $"MAP_MIGRATION_TERRAIN_PREVIEW_OK tiles=" +
                    $"{build.Domain.TileCountX * build.Domain.TileCountZ} " +
                    $"resolution={build.Domain.HeightmapResolution}");
            });
        }

        public static void ValidateMigration()
        {
            IMapMigrationSettings settings = MapMigrationSettings
                .LoadOrCreate()
                .CreateSnapshot();
            try
            {
                MapMeshInventory inventory = MapMeshInventoryWriter.Read();
                bool committed = ReadCommittedState(settings);
                MapMigrationValidationReport report = MapMigrationValidator.Validate(
                    settings,
                    inventory,
                    build: null,
                    requireCommitted: committed);
                if (!report.passed)
                {
                    throw new InvalidDataException(
                        "Map migration validation failed: " +
                        string.Join(" | ", report.errors.Take(5)));
                }

                Debug.Log("MAP_MIGRATION_VALIDATION_PASS");
            }
            finally
            {
                MapMigrationProgress.Clear();
            }
        }

        public static void CommitTerrainReplacement()
        {
            IMapMigrationSettings settings = MapMigrationSettings
                .LoadOrCreate()
                .CreateSnapshot();
            MapMeshInventory inventory = MapMeshInventoryWriter.Read();
            MapMigrationValidationReport preview = MapMigrationValidator.Validate(
                settings,
                inventory,
                build: null,
                requireCommitted: false);
            if (!preview.passed)
            {
                throw new InvalidDataException(
                    "Preview validation failed; commit was refused.");
            }

            int committed = TerrainGridBuilder.CommitReplacement(settings, preview);
            MapMigrationValidationReport final = MapMigrationValidator.Validate(
                settings,
                inventory,
                build: null,
                requireCommitted: true);
            if (!final.passed)
            {
                throw new InvalidDataException("Post-commit validation failed.");
            }

            Debug.Log($"MAP_MIGRATION_COMMIT_PASS ground={committed}");
        }

        public static void OpenExportFolder()
        {
            IMapMigrationSettings settings = MapMigrationSettings
                .LoadOrCreate()
                .CreateSnapshot();
            string path = string.IsNullOrWhiteSpace(settings.LastExportRelativePath)
                ? MapMigrationPaths.ToAbsoluteProjectPath(settings.BlenderExportFolder)
                : MapMigrationPaths.ToAbsoluteProjectPath(settings.LastExportRelativePath);
            Directory.CreateDirectory(path);
            EditorUtility.RevealInFinder(path);
        }

        public static void GenerateVisualComparison()
        {
            MapMigrationVisualComparison.Generate(
                MapMigrationSettings.LoadOrCreate().CreateSnapshot());
            Debug.Log("MAP_MIGRATION_VISUAL_COMPARISON_PASS");
        }

        public static void OpenGeneratedScene()
        {
            IMapMigrationSettings settings = MapMigrationSettings
                .LoadOrCreate()
                .CreateSnapshot();
            if (!File.Exists(MapMigrationPaths.ToAbsoluteProjectPath(settings.OutputScene)))
            {
                throw new FileNotFoundException(
                    "Generated map migration scene does not exist.",
                    settings.OutputScene);
            }

            EditorSceneManager.OpenScene(settings.OutputScene, OpenSceneMode.Single);
        }

        private static void RunWithScan(Action<IMapMigrationSettings, MapScanResult> action)
        {
            MapMigrationSettings settingsAsset = MapMigrationSettings.LoadOrCreate();
            IMapMigrationSettings settings = settingsAsset.CreateSnapshot();
            MapScanResult scan = null;
            try
            {
                settingsAsset.ValidateOrThrow();
                scan = MapMeshScanner.Scan(settings);
                action(settings, scan);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                MapMeshScanner.Release(scan);
                MapMigrationProgress.Clear();
            }
        }

        private static bool ReadCommittedState(IMapMigrationSettings settings)
        {
            if (!File.Exists(MapMigrationPaths.ToAbsoluteProjectPath(settings.OutputScene)))
            {
                return false;
            }

            var scene = EditorSceneManager.OpenScene(settings.OutputScene, OpenSceneMode.Single);
            MapMigrationGeneratedMarker marker = scene
                .GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MapMigrationGeneratedMarker>(true))
                .SingleOrDefault();
            return marker != null && marker.Committed;
        }

        private static void PersistExportReference(
            MapExportManifest manifest,
            string sourceFingerprint)
        {
            MapMigrationSettings settings = MapMigrationSettings.LoadOrCreate();
            settings.RecordExport(manifest.exportRoot, sourceFingerprint);
            AssetDatabase.SaveAssets();
        }

        private static void WriteFailureReport(Exception exception)
        {
            try
            {
                MapMigrationPaths.EnsureAssetFolder(MapMigrationPaths.ReportsRoot);
                string message = exception.GetBaseException().ToString();
                var report = new MapMigrationValidationReport
                {
                    generatedUtc = DateTime.UtcNow.ToString("O"),
                    passed = false,
                    errors = { "Pipeline failed before a PASS report: " + message }
                };
                File.WriteAllText(
                    MapMigrationPaths.ToAbsoluteProjectPath(MapMigrationPaths.ValidationJson),
                    JsonUtility.ToJson(report, true),
                    new UTF8Encoding(false));
                File.WriteAllText(
                    MapMigrationPaths.ToAbsoluteProjectPath(MapMigrationPaths.ValidationMarkdown),
                    "# Map terrain migration validation\n\nStatus: **FAIL**\n\n```text\n" +
                    message.Replace("```", "'''") +
                    "\n```\n",
                    new UTF8Encoding(false));
            }
            catch (Exception reportException)
            {
                Debug.LogWarning(
                    "Could not write map migration failure report: " + reportException.Message);
            }
        }
    }
}
