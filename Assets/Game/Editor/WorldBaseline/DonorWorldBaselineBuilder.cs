using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Editor.WorldTransfer;
using MSC.LegacyImport;
using MSC.World.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldBaseline
{
    public static class DonorWorldBaselineBuilder
    {
        public const string GeneratorVersion = "1.0.0-06B1";

        private const string RootName =
            "DONOR_WORLD_BASELINE_TEMPORARY_DIRECT_IMPORT";
        private const string GeometryRootName = "SANITIZED_STATIC_RENDER_GEOMETRY";
        private const string MetadataRootName = "EXCLUDED_SOURCE_METADATA_ONLY";
        private const string LightingRootName = "PROJECT_OWNED_DEVELOPMENT_LIGHTING";

        public static void Build()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException(
                    "Canonical baseline build was cancelled to preserve " +
                    "unsaved scenes.");
            }

            DonorWorldBaselineManifest.AssertCanonicalFrozenInputsMatchRevision();
            DonorWorldBaselineSourceManifestData previousManifest =
                DonorWorldBaselineManifest.Read();
            DonorWorldBaselineManifest.AssertExistingSourceLockUnchanged();

            IReadOnlyList<WorldBaselineSanitationEntry> plan =
                WorldBaselineSanitationPlan.Load();
            DonorWorldBaselineManifest.AssertSourceLockCoversPlan(
                previousManifest, plan);
            IReadOnlyDictionary<long, int[]> subsets =
                WorldStaticBatchSubsetTable.ParseCommittedTable();

            EnsureFolders();
            PruneUnexpectedAssets(
                WorldBaselinePaths.SceneRoot,
                new[] { WorldBaselinePaths.CanonicalScene },
                "t:Scene");
            IReadOnlyDictionary<string, Mesh> sanitizedMeshes =
                SynchronizeSanitizedMeshes(plan, subsets);
            IReadOnlyDictionary<string, Material> materials =
                CreateCategoryMaterials(
                    plan.Where(entry => entry.IncludeRenderer)
                        .Select(entry => entry.Placement.Category)
                        .Distinct(StringComparer.Ordinal));

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "World_DonorBaseline_Canonical";
            ConfigureNeutralSceneEnvironment();

            var root = new GameObject(RootName);
            var sceneMetadata =
                root.AddComponent<DonorWorldBaselineSceneMetadata>();
            var geometryRoot = CreateChild(root.transform, GeometryRootName);
            var metadataOnlyRoot = CreateChild(root.transform, MetadataRootName);
            var lightingRoot = CreateChild(root.transform, LightingRootName);

            int rendererCount = 0;
            foreach (WorldBaselineSanitationEntry entry in plan)
            {
                WorldEntityPlacement placement = entry.Placement;
                Transform parent = entry.IncludeRenderer
                    ? geometryRoot.transform
                    : metadataOnlyRoot.transform;
                var entity = new GameObject(
                    DonorWorldBaselineDisplayName.Create(
                        placement.HierarchyPath,
                        placement.SourceObjectId,
                        placement.Category));
                entity.transform.SetParent(parent, false);
                entity.transform.SetPositionAndRotation(
                    placement.Position, placement.Rotation);
                entity.transform.localScale = placement.Scale;

                entity.AddComponent<DonorWorldBaselineEntityMetadata>().Configure(
                    placement.StableId,
                    placement.SourceObjectId,
                    entry.SourceParentStableId,
                    placement.HierarchyPath,
                    placement.MeshGuid,
                    placement.CellId,
                    placement.Category,
                    entry.ComponentClassIdsText,
                    entry.Disposition,
                    entry.Reason,
                    entry.SourceActiveSelf,
                    entry.EffectiveActive,
                    entry.IncludeRenderer);

                if (entry.IncludeRenderer)
                {
                    if (!sanitizedMeshes.TryGetValue(
                            placement.StableId, out Mesh mesh) || mesh == null)
                    {
                        throw new InvalidOperationException(
                            "Sanitized mesh mapping is missing for " +
                            placement.StableId);
                    }

                    entity.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer renderer = entity.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = Enumerable.Repeat(
                        materials[placement.Category],
                        Mathf.Max(1, mesh.subMeshCount)).ToArray();
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.motionVectorGenerationMode =
                        MotionVectorGenerationMode.ForceNoMotion;
                    renderer.allowOcclusionWhenDynamic = true;
                    rendererCount++;
                }

                entity.SetActive(entry.EffectiveActive);
            }

            CreateNeutralDevelopmentLight(lightingRoot.transform);

            string semanticFingerprint =
                DonorWorldBaselineFingerprint.Compute(
                    root,
                    root.GetComponentsInChildren<DonorWorldBaselineEntityMetadata>(
                        includeInactive: true));
            sceneMetadata.Configure(
                WorldBaselinePaths.SourceRevisionId,
                WorldBaselinePaths.SourceSceneSha256,
                new Vector3(169.98f, 1.611f, -1040.625f),
                Quaternion.identity,
                Vector3.one,
                plan.Count,
                rendererCount,
                plan.Count - rendererCount,
                semanticFingerprint);

            if (!EditorSceneManager.SaveScene(
                    scene, WorldBaselinePaths.CanonicalScene))
            {
                throw new InvalidOperationException(
                    "Could not save the canonical donor world baseline scene.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            string runtimePayloadFingerprint =
                DonorWorldBaselineManifest.ComputeRuntimePayloadFingerprint(plan);
            DonorWorldBaselineManifest.AssertDeterministicOutputMatchesPrevious(
                previousManifest,
                semanticFingerprint,
                runtimePayloadFingerprint);
            DonorWorldBaselineManifest.Write(
                plan,
                semanticFingerprint,
                runtimePayloadFingerprint);
            AssetDatabase.ImportAsset(
                WorldBaselinePaths.SourceManifest,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            DonorWorldBaselineValidationResult validation =
                DonorWorldBaselineValidator.Validate(verifySourceHashes: true);
            if (!validation.IsValid)
            {
                throw new InvalidOperationException(
                    "Canonical donor world baseline failed validation:\n" +
                    string.Join("\n", validation.Errors));
            }

            Debug.Log(
                $"DONOR_WORLD_BASELINE_BUILD_OK entities={plan.Count} " +
                $"renderers={rendererCount} metadataOnly={plan.Count - rendererCount} " +
                $"semanticFingerprint={semanticFingerprint} " +
                $"payloadFingerprint={runtimePayloadFingerprint}");
        }

        public static void RunBatch() => Build();

        public static void OpenCanonicalScene()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException(
                    "Opening the canonical baseline was cancelled to preserve " +
                    "unsaved scenes.");
            }

            string absolutePath = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaselinePaths.CanonicalScene);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    "Build the canonical donor world baseline first.",
                    absolutePath);
            }

            EditorSceneManager.OpenScene(
                WorldBaselinePaths.CanonicalScene, OpenSceneMode.Single);
        }

        private static IReadOnlyDictionary<string, Mesh> SynchronizeSanitizedMeshes(
            IReadOnlyList<WorldBaselineSanitationEntry> plan,
            IReadOnlyDictionary<long, int[]> subsets)
        {
            WorldBaselineSanitationEntry[] accepted = plan
                .Where(entry => entry.IncludeRenderer)
                .ToArray();
            var copyPlan = new Dictionary<string, string>(StringComparer.Ordinal);
            var destinationByStableId =
                new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (WorldBaselineSanitationEntry entry in accepted)
            {
                string sourcePath;
                string destinationPath;
                if (subsets.ContainsKey(entry.Placement.SourceObjectId))
                {
                    sourcePath = WorldTransferPaths.GeneratedMeshRoot + "/" +
                        entry.Placement.StableId + ".asset";
                    destinationPath = WorldBaselinePaths.DerivedMeshRoot + "/" +
                        entry.Placement.StableId + ".asset";
                }
                else
                {
                    sourcePath =
                        AssetDatabase.GUIDToAssetPath(entry.Placement.MeshGuid);
                    if (!WorldReferenceMeshLibrarySync.IsBelowReferenceMeshRoot(
                            sourcePath))
                    {
                        throw new InvalidOperationException(
                            $"Source mesh {entry.Placement.MeshGuid} for " +
                            $"{entry.Placement.StableId} resolves outside the " +
                            $"audited reference mesh library: '{sourcePath}'.");
                    }

                    destinationPath = WorldBaselinePaths.SourceMeshRoot + "/" +
                        entry.Placement.MeshGuid + ".asset";
                }

                if (AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath) == null)
                {
                    throw new FileNotFoundException(
                        "Audited source mesh is missing or not imported as Mesh.",
                        WorldBaselinePaths.ToAbsoluteProjectPath(sourcePath));
                }

                if (copyPlan.TryGetValue(destinationPath, out string existingSource) &&
                    !string.Equals(existingSource, sourcePath, StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Two source meshes map to one sanitized destination: " +
                        destinationPath);
                }

                copyPlan[destinationPath] = sourcePath;
                destinationByStableId[entry.Placement.StableId] = destinationPath;
            }

            PruneUnexpectedAssets(
                WorldBaselinePaths.SourceMeshRoot,
                copyPlan.Keys.Where(path =>
                    path.StartsWith(
                        WorldBaselinePaths.SourceMeshRoot + "/",
                        StringComparison.Ordinal)),
                "t:Mesh");
            PruneUnexpectedAssets(
                WorldBaselinePaths.DerivedMeshRoot,
                copyPlan.Keys.Where(path =>
                    path.StartsWith(
                        WorldBaselinePaths.DerivedMeshRoot + "/",
                        StringComparison.Ordinal)),
                "t:Mesh");

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (KeyValuePair<string, string> pair in copyPlan
                             .OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    CopyAssetIfChanged(pair.Value, pair.Key);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var result = new Dictionary<string, Mesh>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in destinationByStableId)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(pair.Value);
                if (mesh == null || mesh.vertexCount == 0)
                {
                    throw new InvalidDataException(
                        "Sanitized mesh failed to import: " + pair.Value);
                }

                result.Add(pair.Key, mesh);
            }

            return result;
        }

        private static void CopyAssetIfChanged(string source, string destination)
        {
            string sourceAbsolute =
                WorldBaselinePaths.ToAbsoluteProjectPath(source);
            string destinationAbsolute =
                WorldBaselinePaths.ToAbsoluteProjectPath(destination);
            if (File.Exists(destinationAbsolute) &&
                FilesEqual(sourceAbsolute, destinationAbsolute))
            {
                return;
            }

            if (AssetDatabase.LoadMainAssetAtPath(destination) != null &&
                !AssetDatabase.DeleteAsset(destination))
            {
                throw new IOException(
                    "Could not replace sanitized baseline asset: " + destination);
            }

            EnsureFolder(Path.GetDirectoryName(destination)?.Replace('\\', '/') ??
                throw new InvalidOperationException(
                    "Sanitized asset has no destination directory."));
            if (!AssetDatabase.CopyAsset(source, destination))
            {
                throw new IOException(
                    $"Could not copy sanitized baseline asset '{source}' to " +
                    $"'{destination}'.");
            }
        }

        private static bool FilesEqual(string left, string right)
        {
            var leftInfo = new FileInfo(left);
            var rightInfo = new FileInfo(right);
            return leftInfo.Exists &&
                   rightInfo.Exists &&
                   leftInfo.Length == rightInfo.Length &&
                   string.Equals(
                       DonorWorldBaselineManifest.ComputeFileSha256(left),
                       DonorWorldBaselineManifest.ComputeFileSha256(right),
                       StringComparison.Ordinal);
        }

        private static void PruneUnexpectedAssets(
            string root,
            IEnumerable<string> expectedPaths,
            string assetFilter)
        {
            var expected = expectedPaths.ToHashSet(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets(
                         assetFilter, new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!expected.Contains(path) &&
                    path.StartsWith(root + "/", StringComparison.Ordinal) &&
                    !AssetDatabase.DeleteAsset(path))
                {
                    throw new IOException(
                        "Could not prune stale sanitized mesh: " + path);
                }
            }
        }

        private static IReadOnlyDictionary<string, Material> CreateCategoryMaterials(
            IEnumerable<string> categories)
        {
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "HDRP/Unlit shader is unavailable.");
            }

            var result = new Dictionary<string, Material>(StringComparer.Ordinal);
            string[] orderedCategories = categories
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            PruneUnexpectedAssets(
                WorldBaselinePaths.MaterialRoot,
                orderedCategories.Select(WorldBaselinePaths.CategoryMaterial),
                "t:Material");
            foreach (string category in orderedCategories)
            {
                string path = WorldBaselinePaths.CategoryMaterial(category);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader)
                    {
                        name = "DonorBaseline_" + category
                    };
                    AssetDatabase.CreateAsset(material, path);
                }

                Color color = CategoryColor(category);
                if (material.HasProperty("_UnlitColor"))
                {
                    material.SetColor("_UnlitColor", color);
                }
                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                result.Add(category, material);
            }

            return result;
        }

        private static void ConfigureNeutralSceneEnvironment()
        {
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.42f, 0.42f, 1f);
            RenderSettings.reflectionIntensity = 0f;
        }

        private static void CreateNeutralDevelopmentLight(Transform parent)
        {
            var lightObject = new GameObject("Neutral_Directional_Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.98f, 0.94f, 1f);
            light.intensity = 50000f;
            light.shadows = LightShadows.None;
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void EnsureFolders()
        {
            EnsureFolder(WorldBaselinePaths.SceneRoot);
            EnsureFolder(WorldBaselinePaths.SourceMeshRoot);
            EnsureFolder(WorldBaselinePaths.DerivedMeshRoot);
            EnsureFolder(WorldBaselinePaths.MaterialRoot);
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }
                current = next;
            }
        }

        private static Color CategoryColor(string category)
        {
            if (category.Contains("Terrain", StringComparison.Ordinal) ||
                category == "Field")
            {
                return new Color(0.31f, 0.42f, 0.19f);
            }
            if (category.Contains("Road", StringComparison.Ordinal) ||
                category is "Bridge" or "Fence")
            {
                return new Color(0.20f, 0.21f, 0.22f);
            }
            if (category.Contains("Building", StringComparison.Ordinal) ||
                category is "Roof" or "Floor" or "Door" or "Window")
            {
                return new Color(0.62f, 0.43f, 0.28f);
            }
            if (category.Contains("Vegetation", StringComparison.Ordinal) ||
                category == "Rock")
            {
                return new Color(0.12f, 0.48f, 0.16f);
            }
            if (category.Contains("Water", StringComparison.Ordinal))
            {
                return new Color(0.08f, 0.38f, 0.68f);
            }
            if (category.Contains("Interactive", StringComparison.Ordinal))
            {
                return new Color(0.78f, 0.58f, 0.16f);
            }
            return new Color(0.55f, 0.58f, 0.62f);
        }
    }

    public static class DonorWorldBaselineFingerprint
    {
        public static string Compute(
            GameObject root,
            IEnumerable<DonorWorldBaselineEntityMetadata> metadata)
        {
            var builder = new StringBuilder();
            builder.Append("SCENE|").Append(root.name).Append('|')
                .Append(root.activeSelf ? '1' : '0').Append('|');
            AppendVector(builder, root.transform.localPosition);
            AppendQuaternion(builder, root.transform.localRotation);
            AppendVector(builder, root.transform.localScale);
            builder.Append((int)RenderSettings.ambientMode).Append('|');
            AppendColor(builder, RenderSettings.ambientLight);
            AppendFloat(builder, RenderSettings.reflectionIntensity);
            builder.Append(RenderSettings.fog ? '1' : '0').Append('|')
                .Append(
                    RenderSettings.skybox == null
                        ? string.Empty
                        : AssetDatabase.GetAssetPath(RenderSettings.skybox))
                .Append('\n');

            foreach (Light light in root.GetComponentsInChildren<Light>(
                         includeInactive: true)
                         .OrderBy(value => value.name, StringComparer.Ordinal))
            {
                builder.Append("LIGHT|").Append(light.name).Append('|')
                    .Append((int)light.type).Append('|')
                    .Append((int)light.shadows).Append('|')
                    .Append(light.gameObject.activeSelf ? '1' : '0')
                    .Append('|');
                AppendVector(builder, light.transform.localPosition);
                AppendQuaternion(builder, light.transform.localRotation);
                AppendVector(builder, light.transform.localScale);
                AppendColor(builder, light.color);
                AppendFloat(builder, light.intensity);
                builder.Append('\n');
            }

            foreach (DonorWorldBaselineEntityMetadata entity in metadata
                         .OrderBy(value => value.StableId, StringComparer.Ordinal))
            {
                Transform transform = entity.transform;
                builder.Append(entity.StableId).Append('|')
                    .Append(entity.SourceObjectId.ToString(
                        CultureInfo.InvariantCulture)).Append('|')
                    .Append(entity.SourceParentStableId).Append('|')
                    .Append(entity.SourceCellId).Append('|')
                    .Append(entity.SemanticCategory).Append('|')
                    .Append(entity.SourceHierarchyPath).Append('|')
                    .Append(entity.SourceMeshGuid).Append('|')
                    .Append(entity.SourceComponentClassIds).Append('|')
                    .Append(entity.SanitationDisposition).Append('|')
                    .Append(entity.SanitationReason).Append('|')
                    .Append(entity.SourceActiveSelf ? '1' : '0').Append('|')
                    .Append(entity.SourceActiveInHierarchy ? '1' : '0')
                    .Append('|')
                    .Append(entity.HasSanitizedRenderer ? '1' : '0').Append('|');
                AppendVector(builder, transform.localPosition);
                AppendQuaternion(builder, transform.localRotation);
                AppendVector(builder, transform.localScale);
                builder.Append('\n');
            }

            return DonorWorldBaselineManifest.Sha256Text(builder.ToString());
        }

        private static void AppendColor(StringBuilder builder, Color value)
        {
            AppendFloat(builder, value.r);
            AppendFloat(builder, value.g);
            AppendFloat(builder, value.b);
            AppendFloat(builder, value.a);
        }

        private static void AppendVector(StringBuilder builder, Vector3 value)
        {
            AppendFloat(builder, value.x);
            AppendFloat(builder, value.y);
            AppendFloat(builder, value.z);
        }

        private static void AppendQuaternion(
            StringBuilder builder, Quaternion value)
        {
            AppendFloat(builder, value.x);
            AppendFloat(builder, value.y);
            AppendFloat(builder, value.z);
            AppendFloat(builder, value.w);
        }

        private static void AppendFloat(StringBuilder builder, float value)
        {
            builder.Append(value.ToString("R", CultureInfo.InvariantCulture))
                .Append('|');
        }
    }
}
