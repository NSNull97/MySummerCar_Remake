using System;
using System.Collections.Generic;
using MSC.World.Partition;
using MSC.World.Streaming;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.WorldStreaming
{
    /// <summary>
    /// Registers already saved, project-owned presentation scenes. Does not
    /// rebuild legacy cells, alter gameplay catalogs, or delete scene content.
    /// </summary>
    public static class ProductionWorldCellLayerBuilder
    {
        public static void ReplaceLayerScenes(
            ProductionWorldStreamingManifest manifest,
            string layerId,
            IReadOnlyList<ProductionWorldCellLayerScene> replacements,
            IReadOnlyCollection<WorldCellIndex> affectedCells = null)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (string.IsNullOrWhiteSpace(layerId))
            {
                throw new ArgumentException("A stable layer ID is required.", nameof(layerId));
            }

            if (replacements == null)
            {
                throw new ArgumentNullException(nameof(replacements));
            }

            var affected = affectedCells != null
                ? new HashSet<WorldCellIndex>(affectedCells)
                : null;
            var replacementIndices = new HashSet<WorldCellIndex>();
            var requiredPaths = new List<string>();
            for (int index = 0; index < replacements.Count; index++)
            {
                ProductionWorldCellLayerScene entry = replacements[index];
                if (!string.Equals(entry.LayerId, layerId, StringComparison.Ordinal) ||
                    (affected != null && !affected.Contains(entry.Index)) ||
                    !replacementIndices.Add(entry.Index))
                {
                    throw new ArgumentException(
                        "Replacement entries must uniquely belong to the selected layer and cells.",
                        nameof(replacements));
                }

                if (string.IsNullOrWhiteSpace(entry.ScenePath) ||
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(entry.ScenePath) == null)
                {
                    throw new InvalidOperationException(
                        "Save the generated cell scene before registering it: " + entry.ScenePath);
                }

                if (entry.ScenePath.StartsWith("Assets/Game/LegacyImport/RuntimeBaseline/",
                        StringComparison.Ordinal) && !manifest.PrivateLocalRuntimeBaseline)
                {
                    throw new InvalidOperationException(
                        "Temporary runtime-baseline vegetation requires a private local manifest.");
                }

                requiredPaths.Add(entry.ScenePath);
            }

            EditorBuildSettingsScene[] originalBuildScenes = EditorBuildSettings.scenes;
            EditorBuildSettingsScene[] updatedBuildScenes = AppendEnabledScenes(
                originalBuildScenes, requiredPaths);
            var merged = new List<ProductionWorldCellLayerScene>();
            IReadOnlyList<ProductionWorldCellLayerScene> existing = manifest.CellLayers;
            var previous = new ProductionWorldCellLayerScene[existing.Count];
            for (int index = 0; index < existing.Count; index++)
            {
                ProductionWorldCellLayerScene entry = existing[index];
                previous[index] = entry;
                if (!string.Equals(entry.LayerId, layerId, StringComparison.Ordinal) ||
                    (affected != null && !affected.Contains(entry.Index)))
                {
                    merged.Add(entry);
                }
            }

            for (int index = 0; index < replacements.Count; index++)
            {
                ProductionWorldCellLayerScene entry = replacements[index];
                merged.Add(new ProductionWorldCellLayerScene(entry.LayerId, entry.Index,
                    GetEnabledIndex(updatedBuildScenes, entry.ScenePath), entry.ScenePath,
                    entry.MinimumLoadingRadiusCells,
                    entry.MinimumUnloadingRadiusCells,
                    entry.DeferInitialLoad));
            }

            merged.Sort((left, right) =>
            {
                int layerComparison = string.Compare(left.LayerId, right.LayerId, StringComparison.Ordinal);
                if (layerComparison != 0)
                {
                    return layerComparison;
                }

                int zComparison = left.Index.Z.CompareTo(right.Index.Z);
                return zComparison != 0 ? zComparison : left.Index.X.CompareTo(right.Index.X);
            });

            // Validate a temporary copy before changing either project asset.
            ProductionWorldStreamingManifest candidate = UnityEngine.Object.Instantiate(manifest);
            try
            {
                candidate.ConfigureCellLayersForAuthoring(merged.ToArray());
                IReadOnlyList<string> errors = candidate.ValidateConfiguration();
                if (errors.Count != 0)
                {
                    throw new InvalidOperationException(
                        "Cell-layer registration is invalid: " + string.Join(" | ", errors));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
            }

            try
            {
                EditorBuildSettings.scenes = updatedBuildScenes;
                manifest.ConfigureCellLayersForAuthoring(merged.ToArray());
                EditorUtility.SetDirty(manifest);
                AssetDatabase.SaveAssetIfDirty(manifest);
            }
            catch
            {
                EditorBuildSettings.scenes = originalBuildScenes;
                manifest.ConfigureCellLayersForAuthoring(previous);
                EditorUtility.SetDirty(manifest);
                AssetDatabase.SaveAssetIfDirty(manifest);
                throw;
            }
        }

        private static EditorBuildSettingsScene[] AppendEnabledScenes(
            EditorBuildSettingsScene[] original,
            IReadOnlyList<string> requiredPaths)
        {
            var updated = new List<EditorBuildSettingsScene>(original);
            for (int index = 0; index < requiredPaths.Count; index++)
            {
                string path = requiredPaths[index];
                int existing = updated.FindIndex(scene =>
                    string.Equals(scene.path, path, StringComparison.Ordinal));
                if (existing >= 0 && updated[existing].enabled)
                {
                    continue;
                }

                if (existing >= 0)
                {
                    // Re-enabling in place would shift every following enabled
                    // build index, including entries in other manifests.
                    updated.RemoveAt(existing);
                }

                updated.Add(new EditorBuildSettingsScene(path, true));
            }

            return updated.ToArray();
        }

        private static int GetEnabledIndex(EditorBuildSettingsScene[] scenes, string path)
        {
            int enabledIndex = 0;
            for (int index = 0; index < scenes.Length; index++)
            {
                if (!scenes[index].enabled)
                {
                    continue;
                }

                if (string.Equals(scenes[index].path, path, StringComparison.Ordinal))
                {
                    return enabledIndex;
                }

                enabledIndex++;
            }

            return -1;
        }
    }
}
