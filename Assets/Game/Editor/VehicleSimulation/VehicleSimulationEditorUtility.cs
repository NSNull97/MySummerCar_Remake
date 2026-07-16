using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.VehicleSimulation
{
    internal static class VehicleSimulationEditorUtility
    {
        public static void EnsureFolder(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) &&
                !string.Equals(normalized, "Assets", StringComparison.Ordinal))
            {
                throw new ArgumentException("Unity content folder must be under Assets: " + path, nameof(path));
            }

            string current = "Assets";
            string[] segments = normalized.Split('/');
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        public static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            string directory = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(directory);
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static void AppendPrototypeSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(scene => string.Equals(
                scene.path,
                VehicleSimulationPrototypePaths.PrototypeScene,
                StringComparison.Ordinal));
            scenes.Add(new EditorBuildSettingsScene(
                VehicleSimulationPrototypePaths.PrototypeScene,
                true));
            EditorBuildSettings.scenes = scenes.ToArray();

            var errors = new List<string>();
            ValidateBuildSettings(errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    "M06 build-settings append violated a frozen scene contract:\n- " +
                    string.Join("\n- ", errors));
            }
        }

        public static void ValidateBuildSettings(ICollection<string> errors)
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            int bootstrapIndex = FindEnabledBuildIndex(scenes, VehicleSimulationPrototypePaths.BootstrapScene);
            int pilotCellIndex = FindEnabledBuildIndex(scenes, VehicleSimulationPrototypePaths.PilotProductionCell);
            int nextCellIndex = FindEnabledBuildIndex(scenes, VehicleSimulationPrototypePaths.NextProductionCell);
            int prototypeIndex = FindEnabledBuildIndex(scenes, VehicleSimulationPrototypePaths.PrototypeScene);
            int pilotArrayIndex = Array.FindIndex(scenes, scene =>
                string.Equals(scene.path, VehicleSimulationPrototypePaths.PilotProductionCell, StringComparison.Ordinal));
            int nextArrayIndex = Array.FindIndex(scenes, scene =>
                string.Equals(scene.path, VehicleSimulationPrototypePaths.NextProductionCell, StringComparison.Ordinal));
            int prototypeArrayIndex = Array.FindIndex(scenes, scene =>
                string.Equals(scene.path, VehicleSimulationPrototypePaths.PrototypeScene, StringComparison.Ordinal));

            if (bootstrapIndex != 0)
            {
                errors.Add("Bootstrap must remain enabled at build index 0.");
            }

            if (pilotCellIndex != 6 || nextCellIndex != 8)
            {
                errors.Add(
                    $"Production cells must retain enabled build indices 6/8, found {pilotCellIndex}/{nextCellIndex}.");
            }

            if (pilotArrayIndex != 6 || nextArrayIndex != 8)
            {
                errors.Add(
                    $"Production cells must retain EditorBuildSettings array positions 6/8, found {pilotArrayIndex}/{nextArrayIndex}.");
            }

            int enabledCount = scenes.Count(scene => scene.enabled);
            if (prototypeIndex < 0)
            {
                errors.Add("M06 prototype scene is not enabled in Build Settings.");
            }
            else if (prototypeIndex != 9)
            {
                errors.Add($"M06 prototype scene must keep enabled build index 9, found {prototypeIndex}.");
            }
            else if (prototypeArrayIndex != 9)
            {
                errors.Add(
                    $"M06 prototype scene must be appended at EditorBuildSettings array position 9, found {prototypeArrayIndex}.");
            }
            else if (prototypeIndex != enabledCount - 1)
            {
                errors.Add(
                    $"M06 prototype scene must be appended as the last enabled scene, found {prototypeIndex} of {enabledCount - 1}.");
            }
        }

        public static void ValidateNoDonorDependencies(string rootAssetPath, ICollection<string> errors)
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            if (string.IsNullOrWhiteSpace(rootAssetPath))
            {
                errors.Add("Dependency root path is empty.");
                return;
            }

            string[] dependencies = AssetDatabase.GetDependencies(rootAssetPath, recursive: true);
            for (int index = 0; index < dependencies.Length; index++)
            {
                string path = dependencies[index].Replace('\\', '/');
                if (path.Contains("/LegacyImport/ReferenceOnly/") ||
                    path.Contains("/Imported/DonorGenerated/") ||
                    path.EndsWith("/Assembly-CSharp.dll", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("/PlayMaker.dll", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("M06 production prototype has a forbidden donor/reference dependency: " + path);
                }
            }
        }

        public static T[] FindAllInScene<T>(UnityEngine.SceneManagement.Scene scene)
            where T : Component
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();
        }

        private static int FindEnabledBuildIndex(EditorBuildSettingsScene[] scenes, string path)
        {
            int enabledIndex = 0;
            for (int index = 0; index < scenes.Length; index++)
            {
                EditorBuildSettingsScene scene = scenes[index];
                if (!scene.enabled)
                {
                    continue;
                }

                if (string.Equals(scene.path, path, StringComparison.Ordinal))
                {
                    return enabledIndex;
                }

                enabledIndex++;
            }

            return -1;
        }
    }
}
