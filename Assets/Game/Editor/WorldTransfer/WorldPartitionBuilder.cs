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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldTransfer
{
    public static class WorldPartitionBuilder
    {
        public const string GeneratorVersion = "2.1.0-05C";
        public const string DatabaseVersion = "04A1.1";

        public static void GenerateAll()
        {
            IReadOnlyList<WorldEntityPlacement> records = LoadRecords();
            WorldEntityPlacement[] eligible = records.Where(record => record.ReferenceWorldEligible).ToArray();
            WorldReferenceMeshLibrarySync.Synchronize(eligible);
            IReadOnlyDictionary<long, int[]> staticBatchSubsets = WorldStaticBatchSubsetTable.Synchronize(eligible);
            EnsureFolders();
            ResetGeneratedMeshRoot();
            Dictionary<string, Material> materials = CreateCategoryMaterials(eligible.Select(record => record.Category).Distinct());
            IGrouping<string, WorldEntityPlacement>[] cellGroups = eligible.Where(record => record.CellId != "global")
                .GroupBy(record => record.CellId).OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();

            try
            {
                // 05C creates 1,683 derived meshes. Keep the AssetDatabase in
                // editing mode for the whole deterministic batch; importing
                // each generated asset inline causes a refresh storm and can
                // prevent batchmode Unity from completing.
                AssetDatabase.StartAssetEditing();
                for (int index = 0; index < cellGroups.Length; index++)
                {
                    IGrouping<string, WorldEntityPlacement> group = cellGroups[index];
                    if (!Application.isBatchMode && EditorUtility.DisplayCancelableProgressBar(
                            "World Transfer", "Generating " + group.Key, index / (float)Mathf.Max(1, cellGroups.Length)))
                        throw new OperationCanceledException("World reference generation was cancelled. Run validation or rebuild before using partial output.");
                    BuildReferenceScene(WorldTransferPaths.CellScene(group.Key), group.Key, group.ToArray(), materials, staticBatchSubsets);
                }

                BuildReferenceScene(WorldTransferPaths.GlobalScene, "global", eligible.Where(record => record.CellId == "global").ToArray(), materials, staticBatchSubsets);
                BuildPersistentScene(eligible);
                BuildBootstrapScene(eligible);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                if (!Application.isBatchMode) EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Debug.Log($"WORLD_TRANSFER_GENERATION_OK eligible={eligible.Length} cells={cellGroups.Length}");
        }

        public static void GenerateSelected(string cellId)
        {
            if (string.IsNullOrWhiteSpace(cellId) || string.Equals(cellId, "global", StringComparison.Ordinal) || string.Equals(cellId, "excluded", StringComparison.Ordinal))
                throw new ArgumentException("Selected cell must be a concrete cell_X_Z ID.", nameof(cellId));
            IReadOnlyList<WorldEntityPlacement> allRecords = LoadRecords();
            WorldEntityPlacement[] records = allRecords.Where(record => record.ReferenceWorldEligible && record.CellId == cellId).ToArray();
            if (records.Length == 0) throw new InvalidOperationException("Selected cell has no records: " + cellId);
            WorldReferenceMeshLibrarySync.Synchronize(records);
            IReadOnlyDictionary<long, int[]> staticBatchSubsets = WorldStaticBatchSubsetTable.Synchronize(allRecords);
            EnsureFolders();
            AssetDatabase.StartAssetEditing();
            try
            {
                BuildReferenceScene(WorldTransferPaths.CellScene(cellId), cellId, records, CreateCategoryMaterials(records.Select(record => record.Category).Distinct()), staticBatchSubsets);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }

        public static void OpenReferenceOverview()
        {
            if (!File.Exists(WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.BootstrapScene)))
                throw new FileNotFoundException("Generate world reference scenes first.");
            EditorSceneManager.OpenScene(WorldTransferPaths.BootstrapScene, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(WorldTransferPaths.PersistentScene, OpenSceneMode.Additive);
            EditorSceneManager.OpenScene(WorldTransferPaths.GlobalScene, OpenSceneMode.Additive);
            string sceneRoot = WorldTransferPaths.ToAbsoluteProjectPath(WorldTransferPaths.GeneratedSceneRoot);
            foreach (string path in Directory.EnumerateFiles(sceneRoot, "World_cell_*.unity")
                         .Select(path => Path.GetRelativePath(WorldTransferPaths.ProjectRoot, path).Replace('\\', '/'))
                         .OrderBy(path => path, StringComparer.Ordinal))
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            FrameFullMapInSceneView(LoadRecords().Where(record => record.ReferenceWorldEligible));
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

        private static void BuildReferenceScene(
            string path,
            string cellId,
            IReadOnlyList<WorldEntityPlacement> records,
            IReadOnlyDictionary<string, Material> materials,
            IReadOnlyDictionary<long, int[]> staticBatchSubsets)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = Path.GetFileNameWithoutExtension(path);
            var root = new GameObject($"REFERENCE_ONLY_{cellId}_{DatabaseVersion}");
            root.tag = "EditorOnly";
            var geometryRoot = new GameObject("DONOR_REFERENCE_GEOMETRY");
            geometryRoot.tag = "EditorOnly";
            geometryRoot.transform.SetParent(root.transform, false);
            var fallbackRoot = new GameObject("BOUNDS_AND_MISSING_PROXIES");
            fallbackRoot.tag = "EditorOnly";
            fallbackRoot.transform.SetParent(root.transform, false);
            int actualMeshCount = 0;
            foreach (WorldEntityPlacement record in records.OrderBy(record => record.StableId, StringComparer.Ordinal))
            {
                if (CreateReferenceEntity(geometryRoot.transform, fallbackRoot.transform, record, materials[record.Category], staticBatchSubsets))
                    actualMeshCount++;
            }
            root.AddComponent<WorldGeneratedSceneStamp>().Configure(
                DatabaseVersion, GeneratorVersion, cellId, records.Count, actualMeshCount, records.Count - actualMeshCount);
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
            cameraObject.tag = "EditorOnly";
            cameraObject.transform.SetParent(root.transform, false);
            cameraObject.transform.position = new Vector3(0f, 180f, -240f);
            cameraObject.transform.LookAt(Vector3.zero);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 12000f;
            camera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<WorldTransferDebugFlyCamera>();

            var loaderObject = new GameObject("WorldReferenceCellLoader_DISABLED_REFERENCE_ONLY");
            loaderObject.tag = "EditorOnly";
            loaderObject.transform.SetParent(root.transform, false);
            WorldReferenceCellLoader loader = loaderObject.AddComponent<WorldReferenceCellLoader>();
            WorldReferenceCellScene[] cells = records.Where(record => record.CellId != "global")
                .Select(record => record.CellId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)
                .Select(ParseCellScene).ToArray();
            loader.Configure(cameraObject.transform, 512f, 1, 2, cells);
            loader.enabled = false;

            var lightObject = new GameObject("Reference_Sun");
            lightObject.tag = "EditorOnly";
            lightObject.transform.SetParent(root.transform, false);
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

        private static bool CreateReferenceEntity(
            Transform geometryParent,
            Transform fallbackParent,
            WorldEntityPlacement record,
            Material material,
            IReadOnlyDictionary<long, int[]> staticBatchSubsets)
        {
            Mesh mesh = ResolveReferenceMesh(record.MeshGuid);
            if (mesh == null)
            {
                CreateBoundsProxy(fallbackParent, record, material);
                return false;
            }

            if (staticBatchSubsets.TryGetValue(record.SourceObjectId, out int[] subMeshIndices))
            {
                mesh = CreateStaticBatchSubsetMesh(mesh, subMeshIndices, record);
                if (mesh == null)
                {
                    CreateBoundsProxy(fallbackParent, record, material);
                    return false;
                }
            }

            var reference = new GameObject(Sanitize(record.Category + "_" + record.OriginalName + "_" + record.StableId[..8]));
            reference.tag = "EditorOnly";
            reference.transform.SetParent(geometryParent, false);
            reference.transform.SetPositionAndRotation(record.Position, record.Rotation);
            reference.transform.localScale = record.Scale;
            reference.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = reference.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = Enumerable.Repeat(material, Mathf.Max(1, mesh.subMeshCount)).ToArray();
            reference.AddComponent<WorldReferenceEntity>().Configure(record.StableId, record.SourceObjectId, record.HierarchyPath,
                record.Category, record.ReplacementStatus, record.TransferStatus, WorldReferenceVisualizationKind.ActualMesh, record.MeshGuid);
            return true;
        }

        private static Mesh CreateStaticBatchSubsetMesh(Mesh source, IReadOnlyList<int> subMeshIndices, WorldEntityPlacement record)
        {
            if (subMeshIndices.Count == 0 || subMeshIndices.Any(index => index < 0 || index >= source.subMeshCount))
            {
                Debug.LogError($"WORLD_STATIC_BATCH_INVALID stableId={record.StableId} mesh={record.MeshGuid} sourceSubMeshes={source.subMeshCount} requested={string.Join(";", subMeshIndices)}");
                return null;
            }

            Vector3[] sourceVertices = source.vertices;
            Vector2[] sourceUv = source.uv;
            Matrix4x4 inverseSourceTransform = Matrix4x4.TRS(record.SourcePosition, record.Rotation, record.Scale).inverse;
            var vertexMap = new Dictionary<int, int>();
            var vertices = new List<Vector3>();
            var uv = new List<Vector2>();
            var remappedSubMeshes = new List<int[]>(subMeshIndices.Count);
            foreach (int subMeshIndex in subMeshIndices)
            {
                int[] sourceIndices = source.GetIndices(subMeshIndex, true);
                var remapped = new int[sourceIndices.Length];
                for (int index = 0; index < sourceIndices.Length; index++)
                {
                    int sourceVertex = sourceIndices[index];
                    if (sourceVertex < 0 || sourceVertex >= sourceVertices.Length)
                        throw new InvalidDataException($"Static-batch mesh index {sourceVertex} is outside vertex buffer {sourceVertices.Length} for {record.StableId}.");
                    if (!vertexMap.TryGetValue(sourceVertex, out int destinationVertex))
                    {
                        destinationVertex = vertices.Count;
                        vertexMap.Add(sourceVertex, destinationVertex);
                        vertices.Add(inverseSourceTransform.MultiplyPoint3x4(sourceVertices[sourceVertex]));
                        if (sourceUv.Length == sourceVertices.Length) uv.Add(sourceUv[sourceVertex]);
                    }
                    remapped[index] = destinationVertex;
                }
                remappedSubMeshes.Add(remapped);
            }

            if (vertices.Count == 0) return null;
            var mesh = new Mesh
            {
                name = "WT05C_" + record.StableId,
                indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16
            };
            mesh.SetVertices(vertices);
            if (uv.Count == vertices.Count) mesh.SetUVs(0, uv);
            mesh.subMeshCount = remappedSubMeshes.Count;
            for (int index = 0; index < remappedSubMeshes.Count; index++)
                mesh.SetIndices(remappedSubMeshes[index], source.GetTopology(subMeshIndices[index]), index, false);
            mesh.RecalculateBounds();

            string path = WorldTransferPaths.GeneratedMeshRoot + "/" + record.StableId + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static void CreateBoundsProxy(Transform parent, WorldEntityPlacement record, Material material)
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
                record.Category, record.ReplacementStatus, record.TransferStatus, WorldReferenceVisualizationKind.BoundsFallback);
        }

        private static Mesh ResolveReferenceMesh(string guid)
        {
            if (!WorldReferenceMeshLibrarySync.IsUsableMeshGuid(guid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return WorldReferenceMeshLibrarySync.IsBelowReferenceMeshRoot(path) ? AssetDatabase.LoadAssetAtPath<Mesh>(path) : null;
        }

        private static void FrameFullMapInSceneView(IEnumerable<WorldEntityPlacement> records)
        {
            WorldEntityPlacement[] values = records.ToArray();
            if (values.Length == 0 || SceneView.lastActiveSceneView == null) return;
            Bounds bounds = values[0].Bounds;
            for (int index = 1; index < values.Length; index++) bounds.Encapsulate(values[index].Bounds);
            float size = Mathf.Max(100f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.6f);
            SceneView.lastActiveSceneView.LookAt(bounds.center, Quaternion.Euler(90f, 0f, 0f), size, false, true);
            SceneView.lastActiveSceneView.Repaint();
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
            EnsureFolder(WorldTransferPaths.GeneratedMeshRoot);
        }

        private static void ResetGeneratedMeshRoot()
        {
            if (AssetDatabase.IsValidFolder(WorldTransferPaths.GeneratedMeshRoot)) AssetDatabase.DeleteAsset(WorldTransferPaths.GeneratedMeshRoot);
            EnsureFolder(WorldTransferPaths.GeneratedMeshRoot);
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
