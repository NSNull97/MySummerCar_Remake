using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.World.Data;
using MSC.World.Debugging;
using MSC.World.Streaming;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldTransfer
{
    public static class WorldPartitionBuilder
    {
        public const string GeneratorVersion = "1.0.0";
        public const string DatabaseVersion = "04A1.1";

        public static void GenerateAll()
        {
            IReadOnlyList<WorldEntityPlacement> records = LoadRecords();
            WorldEntityPlacement[] eligible = records.Where(record => record.ReferenceWorldEligible).ToArray();
            EnsureFolders();
            Dictionary<string, Material> materials = CreateCategoryMaterials(eligible.Select(record => record.Category).Distinct());
            IGrouping<string, WorldEntityPlacement>[] cellGroups = eligible.Where(record => record.CellId != "global")
                .GroupBy(record => record.CellId).OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();

            try
            {
                for (int index = 0; index < cellGroups.Length; index++)
                {
                    IGrouping<string, WorldEntityPlacement> group = cellGroups[index];
                    if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar(
                            "World Transfer", "Generating " + group.Key, index / (float)Mathf.Max(1, cellGroups.Length)))
                        throw new OperationCanceledException("World reference generation was cancelled. Run validation or rebuild before using partial output.");
                    BuildProxyScene(WorldTransferPaths.CellScene(group.Key), group.Key, group.ToArray(), materials);
                }

                BuildProxyScene(WorldTransferPaths.GlobalScene, "global", eligible.Where(record => record.CellId == "global").ToArray(), materials);
                BuildPersistentScene(eligible);
                BuildBootstrapScene(eligible);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"WORLD_TRANSFER_GENERATION_OK eligible={eligible.Length} cells={cellGroups.Length}");
            }
            finally
            {
                if (!Application.isBatchMode) EditorUtility.ClearProgressBar();
            }
        }

        public static void GenerateSelected(string cellId)
        {
            if (string.IsNullOrWhiteSpace(cellId) || string.Equals(cellId, "global", StringComparison.Ordinal) || string.Equals(cellId, "excluded", StringComparison.Ordinal))
                throw new ArgumentException("Selected cell must be a concrete cell_X_Z ID.", nameof(cellId));
            WorldEntityPlacement[] records = LoadRecords().Where(record => record.ReferenceWorldEligible && record.CellId == cellId).ToArray();
            if (records.Length == 0) throw new InvalidOperationException("Selected cell has no records: " + cellId);
            EnsureFolders();
            BuildProxyScene(WorldTransferPaths.CellScene(cellId), cellId, records, CreateCategoryMaterials(records.Select(record => record.Category).Distinct()));
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void OpenReferenceOverview()
        {
            if (!File.Exists(WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.BootstrapScene)))
                throw new FileNotFoundException("Generate world reference scenes first.");
            EditorSceneManager.OpenScene(WorldTransferPaths.BootstrapScene, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(WorldTransferPaths.PersistentScene, OpenSceneMode.Additive);
            EditorSceneManager.OpenScene(WorldTransferPaths.GlobalScene, OpenSceneMode.Additive);
            foreach (string cell in new[] { "cell_-1_-1", "cell_-1_0", "cell_0_-1", "cell_0_0" })
            {
                string path = WorldTransferPaths.CellScene(cell);
                if (File.Exists(WorldTransferPaths.ToAbsoluteProjectPath(path)))
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }
        }

        public static void ClearGenerated()
        {
            if (AssetDatabase.IsValidFolder(WorldTransferPaths.GeneratedRoot))
                AssetDatabase.DeleteAsset(WorldTransferPaths.GeneratedRoot);
            AssetDatabase.Refresh();
        }

        private static IReadOnlyList<WorldEntityPlacement> LoadRecords()
        {
            string path = WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.EntityTableAssetPath);
            if (!File.Exists(path)) throw new FileNotFoundException("World entity table is missing.", path);
            return WorldEntityTable.Parse(File.ReadAllText(path));
        }

        private static void BuildProxyScene(string path, string cellId, IReadOnlyList<WorldEntityPlacement> records, IReadOnlyDictionary<string, Material> materials)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = Path.GetFileNameWithoutExtension(path);
            var root = new GameObject($"REFERENCE_ONLY_{cellId}_{DatabaseVersion}");
            root.tag = "EditorOnly";
            root.AddComponent<WorldGeneratedSceneStamp>().Configure(DatabaseVersion, GeneratorVersion, cellId, records.Count);
            foreach (WorldEntityPlacement record in records.OrderBy(record => record.StableId, StringComparer.Ordinal))
                CreateProxy(root.transform, record, materials[record.Category]);
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void BuildPersistentScene(IReadOnlyList<WorldEntityPlacement> records)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "World_Persistent";
            var root = new GameObject("REFERENCE_ONLY_WORLD_PERSISTENT");
            root.tag = "EditorOnly";
            WorldEntityPlacement[] landmarks = records.Where(record => !string.IsNullOrEmpty(record.LandmarkTag))
                .GroupBy(record => $"{record.LandmarkTag}|{Mathf.Floor(record.Position.x / 128f)}|{Mathf.Floor(record.Position.z / 128f)}")
                .Select(group => group.OrderBy(record => record.HierarchyPath.Count(character => character == '/')).ThenBy(record => record.SourceObjectId).First())
                .OrderBy(record => record.LandmarkTag, StringComparer.Ordinal).ThenBy(record => record.SourceObjectId).ToArray();
            root.AddComponent<WorldGeneratedSceneStamp>().Configure(DatabaseVersion, GeneratorVersion, "persistent", landmarks.Length);
            root.AddComponent<WorldLandmarkRegistry>().Configure(landmarks
                .Select(record => new WorldLandmarkEntry(record.StableId, record.LandmarkTag + ":" + record.OriginalName, record.Position)).ToArray());
            EditorSceneManager.SaveScene(scene, WorldTransferPaths.PersistentScene);
        }

        private static void BuildBootstrapScene(IReadOnlyList<WorldEntityPlacement> records)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "WorldTransfer_Bootstrap";
            var root = new GameObject("REFERENCE_ONLY_WORLD_TRANSFER_BOOTSTRAP");
            root.tag = "EditorOnly";
            root.AddComponent<WorldGeneratedSceneStamp>().Configure(DatabaseVersion, GeneratorVersion, "bootstrap", 0);

            var cameraObject = new GameObject("WorldTransfer_FreeFlyCamera");
            cameraObject.transform.position = new Vector3(0f, 180f, -240f);
            cameraObject.transform.LookAt(Vector3.zero);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 12000f;
            camera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<WorldTransferDebugFlyCamera>();

            var loaderObject = new GameObject("WorldReferenceCellLoader_DISABLED_REFERENCE_ONLY");
            WorldReferenceCellLoader loader = loaderObject.AddComponent<WorldReferenceCellLoader>();
            WorldReferenceCellScene[] cells = records.Where(record => record.CellId != "global")
                .Select(record => record.CellId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)
                .Select(ParseCellScene).ToArray();
            loader.Configure(cameraObject.transform, 512f, 1, 2, cells);
            loader.enabled = false;

            var lightObject = new GameObject("Reference_Sun");
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 50000f;
            light.shadows = LightShadows.None;
            EditorSceneManager.SaveScene(scene, WorldTransferPaths.BootstrapScene);
        }

        private static WorldReferenceCellScene ParseCellScene(string cellId)
        {
            string[] parts = cellId.Split('_');
            return new WorldReferenceCellScene(int.Parse(parts[1]), int.Parse(parts[2]), "World_" + cellId);
        }

        private static void CreateProxy(Transform parent, WorldEntityPlacement record, Material material)
        {
            GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            proxy.name = Sanitize(record.Category + "_" + record.OriginalName + "_" + record.StableId[..8]);
            proxy.tag = "EditorOnly";
            proxy.transform.SetParent(parent, false);
            Vector3 size = record.Bounds.size;
            bool pointProxy = size.x < 0.05f && size.y < 0.05f && size.z < 0.05f;
            proxy.transform.position = pointProxy ? record.Position : record.Bounds.center;
            proxy.transform.localScale = pointProxy ? Vector3.one * 1.5f : new Vector3(
                Mathf.Clamp(size.x, 0.15f, 2048f), Mathf.Clamp(size.y, 0.15f, 2048f), Mathf.Clamp(size.z, 0.15f, 2048f));
            proxy.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(proxy.GetComponent<Collider>());
            proxy.AddComponent<WorldReferenceEntity>().Configure(record.StableId, record.SourceObjectId, record.HierarchyPath,
                record.Category, record.ReplacementStatus, record.TransferStatus);
        }

        private static Dictionary<string, Material> CreateCategoryMaterials(IEnumerable<string> categories)
        {
            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null) throw new InvalidOperationException("HDRP/Unlit shader is unavailable.");
            foreach (string category in categories.OrderBy(value => value, StringComparer.Ordinal))
            {
                string path = WorldTransferPaths.GeneratedMaterialRoot + "/" + Sanitize(category) + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader) { name = "WT_" + category };
                    AssetDatabase.CreateAsset(material, path);
                }
                Color color = CategoryColor(category);
                if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", color);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                result[category] = material;
            }
            return result;
        }

        private static Color CategoryColor(string category)
        {
            if (category.Contains("Terrain")) return new Color(0.35f, 0.26f, 0.12f);
            if (category.Contains("Road") || category is "Bridge" or "Ditch") return new Color(0.15f, 0.16f, 0.18f);
            if (category.Contains("Building") || category is "Roof" or "Floor" or "Door" or "Window") return new Color(0.72f, 0.48f, 0.27f);
            if (category.Contains("Vegetation")) return new Color(0.12f, 0.55f, 0.18f);
            if (category.Contains("Water") || category == "Shoreline") return new Color(0.08f, 0.42f, 0.78f);
            if (category.Contains("Collider")) return new Color(0.85f, 0.15f, 0.75f);
            if (category.Contains("Unknown") || category.Contains("Interactive")) return new Color(0.9f, 0.68f, 0.12f);
            return new Color(0.55f, 0.58f, 0.62f);
        }

        private static string Sanitize(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
            return value.Length > 96 ? value[..96] : value;
        }

        private static void EnsureFolders()
        {
            EnsureFolder(WorldTransferPaths.GeneratedSceneRoot);
            EnsureFolder(WorldTransferPaths.GeneratedMaterialRoot);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        public static void RunBatch() => GenerateAll();

        public static void RebuildBatch()
        {
            ClearGenerated();
            GenerateAll();
        }
    }
}
