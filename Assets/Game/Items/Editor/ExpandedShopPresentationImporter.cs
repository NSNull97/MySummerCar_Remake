using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using MSC.Items.Presentation;
using MSC.LegacyImport.Editor.Configuration;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Items.Editor
{
    /// <summary>
    /// Imports only presentation evidence from the user-supplied Expanded Shop
    /// bundle extraction. Old scripts, physics, FSMs, tags and layers never
    /// enter the generated runtime wrappers.
    /// </summary>
    internal static class ExpandedShopPresentationImporter
    {
        internal const int ExpectedBindingCount = 19;
        internal const string ManifestPath =
            "Assets/Game/LegacyImport/Manifests/" +
            "ExpandedShopPresentationManifest.json";
        internal const string GeneratedSourceRoot =
            ItemLegacyPresentationPaths.GeneratedSourceRoot +
            "/ExpandedShop";
        internal const string GeneratedShelfCatalogPath =
            ItemLegacyPresentationPaths.GeneratedRoot +
            "/Resources/ExpandedShopShelfLayoutCatalog.asset";

        private const string ConfigurationPath =
            "Config/DonorPaths.local.json";
        private const string GeneratedMaterialPrefix =
            "ExpandedShopMaterial_";
        private const string GeneratedPrefabPrefix =
            "ExpandedShopItemVisual_";
        private const string GeneratedShelfPrefabPrefix =
            "ExpandedShopShelfGroup_";

        private static readonly string[] CopiedSourceDirectories =
        {
            "Mesh",
            "Material",
            "Texture2D",
            "Shader",
            "assets/prefab/drinks",
            "assets/mesh/cans",
        };

        internal static IReadOnlyList<ItemPresentationBinding> Build(
            ItemDefinitionCatalog definitions)
        {
            if (definitions == null)
            {
                throw new ArgumentNullException(nameof(definitions));
            }

            ExpandedShopPresentationManifest manifest = LoadManifest();
            DonorPathConfiguration configuration =
                DonorPathConfiguration.LoadFromFile(ConfigurationPath);
            string sourceRoot = Path.Combine(
                configuration.DonorStagingDirectory,
                manifest.source.stagingRootRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            string bundlePath = Path.Combine(
                configuration.DonorStagingDirectory,
                manifest.source.bundleRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            string shelfPrefabEvidencePath = Path.Combine(
                sourceRoot,
                manifest.source.shelfPrefabRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
            RequireHash(
                bundlePath,
                manifest.source.bundleSha256,
                "Expanded Shop embedded bundle");
            RequireHash(
                shelfPrefabEvidencePath,
                manifest.source.shelfPrefabSha256,
                "Expanded Shop authored shelf layout");
            foreach (ExpandedShopPresentationItem item in manifest.items)
            {
                RequireHash(
                    Path.Combine(
                        sourceRoot,
                        item.prefabRelativePath.Replace(
                            '/',
                            Path.DirectorySeparatorChar)),
                    item.prefabSha256,
                    item.definitionId);
            }

            CopySanitizedSourceClosure(sourceRoot);
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            ConfigureImportedTextures();

            var result = new List<ItemPresentationBinding>(
                manifest.items.Length);
            foreach (ExpandedShopPresentationItem item in manifest.items)
            {
                if (!definitions.TryGet(
                        item.definitionId,
                        out ItemDefinitionRecord definition))
                {
                    throw new InvalidDataException(
                        "Expanded Shop presentation has no item definition: " +
                        item.definitionId);
                }

                string sourcePrefabPath = GeneratedSourceRoot + "/" +
                    item.prefabRelativePath;
                GameObject sourcePrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        sourcePrefabPath) ??
                    throw new InvalidDataException(
                        "Expanded Shop source prefab did not import: " +
                        sourcePrefabPath);
                string generatedPrefabPath = BuildSanitizedPrefab(
                    item,
                    sourcePrefab);
                GameObject generatedPrefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        generatedPrefabPath) ??
                    throw new InvalidDataException(
                        "Expanded Shop sanitized prefab did not load: " +
                        generatedPrefabPath);
                var binding = new ItemPresentationBinding();
                binding.Configure(
                    definition.DefinitionId,
                    definition.ReplacementKey,
                    generatedPrefab);
                result.Add(binding);
            }

            BuildShelfLayoutCatalog(
                manifest,
                shelfPrefabEvidencePath);
            AssetDatabase.SaveAssets();
            return result;
        }

        internal static IReadOnlyList<string> GetExpectedDefinitionIds() =>
            LoadManifest().items
                .Select(value => value.definitionId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

        private static ExpandedShopPresentationManifest LoadManifest()
        {
            string absolutePath = ItemLegacyPresentationEvidence
                .ToAbsoluteProjectPath(ManifestPath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    "Expanded Shop presentation manifest is missing.",
                    absolutePath);
            }

            ExpandedShopPresentationManifest manifest =
                JsonUtility.FromJson<ExpandedShopPresentationManifest>(
                    File.ReadAllText(absolutePath));
            if (manifest == null || manifest.schemaVersion != 2 ||
                !string.Equals(
                    manifest.classification,
                    "TemporaryDirectImport",
                    StringComparison.Ordinal) ||
                manifest.source == null ||
                string.IsNullOrWhiteSpace(
                    manifest.source.stagingRootRelativePath) ||
                string.IsNullOrWhiteSpace(
                    manifest.source.bundleRelativePath) ||
                !IsSha256(manifest.source.bundleSha256) ||
                !IsSha256(manifest.source.dllSha256) ||
                string.IsNullOrWhiteSpace(
                    manifest.source.shelfPrefabRelativePath) ||
                !IsSha256(manifest.source.shelfPrefabSha256) ||
                !ExpandedShopShelfLayoutCatalog.HasPositiveScale(
                    manifest.source.projectStoreRootWorldScale) ||
                !ExpandedShopShelfLayoutCatalog.HasUsableRotation(
                    manifest.source.projectStoreRootWorldRotation) ||
                manifest.shelfGroups == null ||
                manifest.shelfGroups.Length !=
                ExpandedShopShelfLayoutCatalog.ExpectedGroupCount ||
                manifest.items == null ||
                manifest.items.Length != ExpectedBindingCount)
            {
                throw new InvalidDataException(
                    "Expanded Shop presentation manifest is invalid.");
            }

            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            var prefabPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExpandedShopPresentationItem item in manifest.items)
            {
                if (item == null ||
                    !ItemDefinitionId.IsValid(item.definitionId) ||
                    string.IsNullOrWhiteSpace(item.prefabRelativePath) ||
                    !IsSha256(item.prefabSha256) ||
                    !definitionIds.Add(item.definitionId) ||
                    !prefabPaths.Add(item.prefabRelativePath))
                {
                    throw new InvalidDataException(
                        "Expanded Shop presentation item identity is invalid.");
                }
            }

            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            var groupNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (ExpandedShopShelfGroupSource group in manifest.shelfGroups)
            {
                if (group == null ||
                    string.IsNullOrWhiteSpace(group.groupId) ||
                    string.IsNullOrWhiteSpace(group.sourceGroupName) ||
                    string.IsNullOrWhiteSpace(group.offerId) ||
                    !group.offerId.StartsWith(
                        "service.store.expanded-shop-",
                        StringComparison.Ordinal) ||
                    group.expectedStockUnitCount < 0 ||
                    group.stockIndexOffset < 0 ||
                    !groupIds.Add(group.groupId) ||
                    !groupNames.Add(group.sourceGroupName))
                {
                    throw new InvalidDataException(
                        "Expanded Shop shelf group mapping is invalid.");
                }
            }

            return manifest;
        }

        private static void CopySanitizedSourceClosure(string sourceRoot)
        {
            EnsureAssetFolder(GeneratedSourceRoot);
            foreach (string relativeDirectory in CopiedSourceDirectories)
            {
                string sourceDirectory = Path.Combine(
                    sourceRoot,
                    relativeDirectory.Replace(
                        '/',
                        Path.DirectorySeparatorChar));
                if (!Directory.Exists(sourceDirectory))
                {
                    throw new DirectoryNotFoundException(
                        "Expanded Shop extracted source directory is missing: " +
                        sourceDirectory);
                }

                string destinationAssetDirectory = GeneratedSourceRoot + "/" +
                    relativeDirectory;
                string destinationDirectory = ItemLegacyPresentationEvidence
                    .ToAbsoluteProjectPath(destinationAssetDirectory);
                foreach (string sourceFile in Directory.GetFiles(
                             sourceDirectory,
                             "*",
                             SearchOption.AllDirectories))
                {
                    string relativeFile = Path.GetRelativePath(
                        sourceDirectory,
                        sourceFile);
                    string destinationFile = Path.Combine(
                        destinationDirectory,
                        relativeFile);
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(destinationFile) ??
                        throw new InvalidOperationException(
                            "Expanded Shop destination directory is invalid."));
                    File.Copy(sourceFile, destinationFile, overwrite: true);
                }
            }
        }

        private static void ConfigureImportedTextures()
        {
            string textureRoot = GeneratedSourceRoot + "/Texture2D/";
            string[] texturePaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => path.StartsWith(
                    textureRoot,
                    StringComparison.Ordinal))
                .Where(path => path.EndsWith(
                    ".png",
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            foreach (string path in texturePaths)
            {
                if (AssetImporter.GetAtPath(path) is not
                    TextureImporter importer)
                {
                    throw new InvalidDataException(
                        "Expanded Shop texture importer is unavailable: " +
                        path);
                }

                bool normalMap = Path.GetFileNameWithoutExtension(path)
                    .EndsWith("_n", StringComparison.OrdinalIgnoreCase);
                importer.textureType = normalMap
                    ? TextureImporterType.NormalMap
                    : TextureImporterType.Default;
                importer.sRGBTexture = !normalMap;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = true;
                importer.textureCompression =
                    TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        private static string BuildSanitizedPrefab(
            ExpandedShopPresentationItem item,
            GameObject sourcePrefab)
        {
            string sanitizedId = SanitizeAssetName(item.definitionId);
            string prefabPath =
                ItemLegacyPresentationPaths.GeneratedPrefabRoot + "/" +
                GeneratedPrefabPrefix + sanitizedId + ".prefab";
            GameObject sourceInstance = UnityEngine.Object.Instantiate(
                sourcePrefab);
            var root = new GameObject(
                GeneratedPrefabPrefix + sanitizedId);
            try
            {
                sourceInstance.hideFlags = HideFlags.HideAndDontSave;
                sourceInstance.SetActive(true);
                sourceInstance.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);
                sourceInstance.transform.localScale = Vector3.one;
                root.transform.localRotation = item.zUpModel
                    ? Quaternion.Euler(-90f, 0f, 0f)
                    : Quaternion.identity;

                MeshRenderer[] sourceRenderers = sourceInstance
                    .GetComponentsInChildren<MeshRenderer>(
                        includeInactive: false);
                int builtRendererCount = 0;
                for (int index = 0;
                     index < sourceRenderers.Length;
                     index++)
                {
                    MeshRenderer sourceRenderer = sourceRenderers[index];
                    MeshFilter sourceFilter =
                        sourceRenderer.GetComponent<MeshFilter>();
                    if (sourceFilter?.sharedMesh == null)
                    {
                        continue;
                    }

                    var visual = new GameObject(
                        "mesh_" + index.ToString("D2"));
                    visual.transform.SetParent(root.transform, false);
                    Matrix4x4 relative = sourceInstance.transform
                        .worldToLocalMatrix *
                        sourceRenderer.transform.localToWorldMatrix;
                    visual.transform.localPosition = relative.GetColumn(3);
                    visual.transform.localRotation = relative.rotation;
                    visual.transform.localScale = relative.lossyScale;
                    visual.AddComponent<MeshFilter>().sharedMesh =
                        sourceFilter.sharedMesh;
                    MeshRenderer renderer =
                        visual.AddComponent<MeshRenderer>();
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    renderer.sharedMaterials = sourceRenderer.sharedMaterials
                        .Select(GetOrCreateHdrpMaterial)
                        .ToArray();
                    builtRendererCount++;
                }

                if (builtRendererCount == 0)
                {
                    throw new InvalidDataException(
                        "Expanded Shop source has no active renderable mesh: " +
                        item.prefabRelativePath);
                }

                bool success;
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath,
                    out success);
                if (!success)
                {
                    throw new IOException(
                        "Unity failed to save Expanded Shop presentation: " +
                        prefabPath);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceInstance);
                UnityEngine.Object.DestroyImmediate(root);
            }

            return prefabPath;
        }

        private static void BuildShelfLayoutCatalog(
            ExpandedShopPresentationManifest manifest,
            string shelfPrefabEvidencePath)
        {
            ExpandedShopPrefabEvidence evidence =
                ExpandedShopPrefabEvidence.Parse(shelfPrefabEvidencePath);
            ExpandedShopPrefabNode sourceRoot = evidence.RequireRoot(
                "TeimoDrinksMod");
            if (sourceRoot.Children.Count != manifest.shelfGroups.Length)
            {
                throw new InvalidDataException(
                    $"Expanded Shop shelf root has " +
                    $"{sourceRoot.Children.Count} physical groups; locked " +
                    $"mapping expects {manifest.shelfGroups.Length}.");
            }
            var definitions = new List<
                ExpandedShopShelfGroupDefinition>(
                manifest.shelfGroups.Length);
            foreach (ExpandedShopShelfGroupSource mapping in
                     manifest.shelfGroups)
            {
                ExpandedShopPrefabNode sourceGroup =
                    sourceRoot.FindDirectChild(mapping.sourceGroupName) ??
                    throw new InvalidDataException(
                        "Expanded Shop shelf group is missing: " +
                        mapping.sourceGroupName);
                ExpandedShopPrefabNode sourceColliderNode =
                    sourceGroup.FindDirectChild("Col") ??
                    throw new InvalidDataException(
                        "Expanded Shop shelf group has no Col child: " +
                        mapping.sourceGroupName);
                ExpandedShopBoxColliderEvidence sourceCollider =
                    sourceColliderNode.BoxCollider ??
                    throw new InvalidDataException(
                        "Expanded Shop shelf group Col is not a " +
                        "BoxCollider: " + mapping.sourceGroupName);
                ExpandedShopPrefabNode sourceInventory =
                    sourceGroup.FindDirectChild("Inventory");
                int stockUnitCount = sourceInventory == null
                    ? 0
                    : sourceInventory.Children.Count;
                if (stockUnitCount != mapping.expectedStockUnitCount)
                {
                    throw new InvalidDataException(
                        $"Expanded Shop shelf group " +
                        $"'{mapping.sourceGroupName}' has " +
                        $"{stockUnitCount} units; locked evidence expects " +
                        $"{mapping.expectedStockUnitCount}.");
                }

                GameObject presentationPrefab =
                    BuildSanitizedShelfGroupPrefab(
                        mapping,
                        sourceGroup,
                        sourceInventory);
                var definition = new
                    ExpandedShopShelfGroupDefinition();
                definition.ConfigureForAuthoring(
                    mapping.groupId,
                    mapping.sourceGroupName,
                    mapping.offerId,
                    sourceGroup.LocalPosition,
                    sourceGroup.LocalRotation,
                    sourceGroup.LocalScale,
                    sourceColliderNode.LocalPosition,
                    sourceColliderNode.LocalRotation,
                    sourceColliderNode.LocalScale,
                    sourceCollider.Center,
                    sourceCollider.Size,
                    sourceCollider.IsTrigger,
                    mapping.stockIndexOffset,
                    stockUnitCount,
                    presentationPrefab);
                definitions.Add(definition);
            }

            EnsureAssetFolder(
                ItemLegacyPresentationPaths.GeneratedRoot +
                "/Resources");
            ExpandedShopShelfLayoutCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    ExpandedShopShelfLayoutCatalog>(
                    GeneratedShelfCatalogPath);
            if (catalog == null)
            {
                UnityEngine.Object existing =
                    AssetDatabase.LoadMainAssetAtPath(
                        GeneratedShelfCatalogPath);
                if (existing != null)
                {
                    throw new InvalidDataException(
                        "Expanded Shop shelf catalog path is occupied by " +
                        "an incompatible asset: " +
                        GeneratedShelfCatalogPath);
                }

                catalog = ScriptableObject.CreateInstance<
                    ExpandedShopShelfLayoutCatalog>();
                AssetDatabase.CreateAsset(
                    catalog,
                    GeneratedShelfCatalogPath);
            }

            catalog.ConfigureForAuthoring(
                manifest.source.dllSha256,
                manifest.source.bundleSha256,
                manifest.source.shelfPrefabSha256,
                manifest.source.projectStoreRootWorldPosition,
                manifest.source.projectStoreRootWorldRotation,
                manifest.source.projectStoreRootWorldScale,
                definitions.ToArray());
            if (!catalog.TryValidate(out string failure))
            {
                throw new InvalidDataException(failure);
            }

            EditorUtility.SetDirty(catalog);
        }

        private static GameObject BuildSanitizedShelfGroupPrefab(
            ExpandedShopShelfGroupSource mapping,
            ExpandedShopPrefabNode sourceGroup,
            ExpandedShopPrefabNode sourceInventory)
        {
            IReadOnlyList<ExpandedShopPrefabNode> groupRenderers =
                sourceGroup.GetRenderableDescendants();
            if (groupRenderers.Count == 0 && sourceInventory == null)
            {
                // The mod's sausage entry deliberately contributes only its
                // purchase collider over the donor game's existing stock.
                return null;
            }

            string prefabPath =
                ItemLegacyPresentationPaths.GeneratedPrefabRoot + "/" +
                GeneratedShelfPrefabPrefix +
                SanitizeAssetName(mapping.groupId) + ".prefab";
            var root = new GameObject(
                GeneratedShelfPrefabPrefix +
                SanitizeAssetName(mapping.groupId));
            var stockUnits = new List<GameObject>();
            var outlineRenderers = new List<Renderer>();
            try
            {
                if (sourceInventory != null)
                {
                    for (int index = 0;
                         index < sourceInventory.Children.Count;
                         index++)
                    {
                        ExpandedShopPrefabNode sourceUnit =
                            sourceInventory.Children[index];
                        var targetUnit = new GameObject(
                            "StockUnit_" + index.ToString("D3"));
                        targetUnit.transform.SetParent(root.transform, false);
                        ApplyRelativeMatrix(
                            targetUnit.transform,
                            sourceUnit.GetMatrixRelativeTo(sourceGroup));
                        IReadOnlyList<ExpandedShopPrefabNode> unitRenderers =
                            sourceUnit.GetRenderableDescendants();
                        if (unitRenderers.Count == 0)
                        {
                            throw new InvalidDataException(
                                $"Expanded Shop shelf unit " +
                                $"'{mapping.sourceGroupName}/{index}' has no " +
                                "renderable mesh.");
                        }

                        for (int rendererIndex = 0;
                             rendererIndex < unitRenderers.Count;
                             rendererIndex++)
                        {
                            outlineRenderers.Add(CloneShelfRenderer(
                                unitRenderers[rendererIndex],
                                sourceUnit,
                                targetUnit.transform,
                                rendererIndex));
                        }

                        stockUnits.Add(targetUnit);
                    }
                }

                var decorationRoot = new GameObject("Decoration");
                decorationRoot.transform.SetParent(root.transform, false);
                int decorationCount = 0;
                for (int rendererIndex = 0;
                     rendererIndex < groupRenderers.Count;
                     rendererIndex++)
                {
                    ExpandedShopPrefabNode sourceRenderer =
                        groupRenderers[rendererIndex];
                    if (sourceInventory != null &&
                        sourceRenderer.IsSelfOrDescendantOf(sourceInventory))
                    {
                        continue;
                    }

                    outlineRenderers.Add(CloneShelfRenderer(
                        sourceRenderer,
                        sourceGroup,
                        decorationRoot.transform,
                        decorationCount));
                    decorationCount++;
                }

                if (decorationCount == 0)
                {
                    UnityEngine.Object.DestroyImmediate(decorationRoot);
                }

                ExpandedShopShelfGroupPresentation presentation =
                    root.AddComponent<
                        ExpandedShopShelfGroupPresentation>();
                presentation.ConfigureForAuthoring(
                    stockUnits.ToArray(),
                    outlineRenderers.ToArray());
                if (!presentation.TryValidate(out string failure))
                {
                    throw new InvalidDataException(failure);
                }

                bool success;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath,
                    out success);
                if (!success || saved == null)
                {
                    throw new IOException(
                        "Unity failed to save Expanded Shop shelf group: " +
                        prefabPath);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) ??
                throw new InvalidDataException(
                    "Expanded Shop sanitized shelf group did not load: " +
                    prefabPath);
        }

        private static MeshRenderer CloneShelfRenderer(
            ExpandedShopPrefabNode sourceRenderer,
            ExpandedShopPrefabNode sourceRelativeRoot,
            Transform targetParent,
            int rendererIndex)
        {
            string meshPath = AssetDatabase.GUIDToAssetPath(
                sourceRenderer.MeshGuid);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) ??
                throw new InvalidDataException(
                    "Expanded Shop shelf mesh did not import: " +
                    sourceRenderer.MeshGuid);
            var visual = new GameObject(
                "mesh_" + rendererIndex.ToString("D2"));
            visual.transform.SetParent(targetParent, false);
            ApplyRelativeMatrix(
                visual.transform,
                sourceRenderer.GetMatrixRelativeTo(sourceRelativeRoot));
            visual.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            var materials = new Material[
                sourceRenderer.MaterialGuids.Count];
            for (int materialIndex = 0;
                 materialIndex < materials.Length;
                 materialIndex++)
            {
                string materialGuid =
                    sourceRenderer.MaterialGuids[materialIndex];
                string materialPath = AssetDatabase.GUIDToAssetPath(
                    materialGuid);
                Material sourceMaterial =
                    AssetDatabase.LoadAssetAtPath<Material>(materialPath) ??
                    throw new InvalidDataException(
                        "Expanded Shop shelf material did not import: " +
                        materialGuid);
                materials[materialIndex] =
                    GetOrCreateHdrpMaterial(sourceMaterial);
            }

            renderer.sharedMaterials = materials;
            return renderer;
        }

        private static void ApplyRelativeMatrix(
            Transform target,
            Matrix4x4 relative)
        {
            target.localPosition = relative.GetColumn(3);
            target.localRotation = relative.rotation;
            target.localScale = relative.lossyScale;
        }

        private static Material GetOrCreateHdrpMaterial(
            Material sourceMaterial)
        {
            if (sourceMaterial == null)
            {
                throw new InvalidDataException(
                    "Expanded Shop renderer has a missing source material.");
            }

            string sourcePath = AssetDatabase.GetAssetPath(sourceMaterial);
            string sourceGuid = AssetDatabase.AssetPathToGUID(sourcePath);
            if (string.IsNullOrWhiteSpace(sourceGuid) ||
                !sourcePath.StartsWith(
                    GeneratedSourceRoot + "/",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Expanded Shop material is outside its ignored source " +
                    "closure: " + sourcePath);
            }

            string targetPath =
                ItemLegacyPresentationPaths.GeneratedMaterialRoot + "/" +
                GeneratedMaterialPrefix + sourceGuid + ".mat";
            Shader shader = Shader.Find("HDRP/Lit") ??
                throw new InvalidOperationException(
                    "HDRP/Lit is unavailable for Expanded Shop presentation.");
            Material target = AssetDatabase.LoadAssetAtPath<Material>(
                targetPath);
            if (target == null)
            {
                target = new Material(shader);
                AssetDatabase.CreateAsset(target, targetPath);
            }

            target.name = GeneratedMaterialPrefix + sourceGuid;
            target.shader = shader;
            target.enableInstancing = true;
            Texture albedo = ResolveSourceTexture(
                sourceMaterial,
                sourcePath,
                "_MainTex");
            if (albedo == null)
            {
                throw new InvalidDataException(
                    "Expanded Shop source material has no reviewed albedo: " +
                    sourcePath);
            }

            target.SetTexture("_BaseColorMap", albedo);
            target.SetColor("_BaseColor", Color.white);
            target.SetFloat("_Metallic", 0f);
            target.SetFloat("_Smoothness", 0.24f);
            target.SetFloat("_SurfaceType", 0f);
            target.SetFloat("_AlphaCutoffEnable", 0f);
            target.SetFloat("_DoubleSidedEnable", 0f);
            target.SetOverrideTag("RenderType", "Opaque");
            target.renderQueue = -1;
            target.DisableKeyword("_ALPHATEST_ON");
            target.DisableKeyword("_DOUBLESIDED_ON");

            Texture normal = ResolveSourceTexture(
                sourceMaterial,
                sourcePath,
                "_BumpMap");
            target.SetTexture("_NormalMap", normal);
            if (normal != null)
            {
                target.SetFloat("_NormalScale", 1f);
                target.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
            }
            else
            {
                target.DisableKeyword("_NORMALMAP_TANGENT_SPACE");
            }

            EditorUtility.SetDirty(target);
            return target;
        }

        private static Texture ResolveSourceTexture(
            Material sourceMaterial,
            string sourceMaterialPath,
            string propertyName)
        {
            if (sourceMaterial.HasProperty(propertyName))
            {
                Texture value = sourceMaterial.GetTexture(propertyName);
                if (value != null)
                {
                    return value;
                }
            }

            string absolutePath = ItemLegacyPresentationEvidence
                .ToAbsoluteProjectPath(sourceMaterialPath);
            string yaml = File.ReadAllText(absolutePath);
            Match match = Regex.Match(
                yaml,
                "name:\\s*" + Regex.Escape(propertyName) +
                "[\\s\\S]{0,320}?guid:\\s*([0-9a-fA-F]{32})",
                RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                return null;
            }

            string texturePath = AssetDatabase.GUIDToAssetPath(
                match.Groups[1].Value);
            return AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
        }

        private static void RequireHash(
            string path,
            string expected,
            string label)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Locked Expanded Shop presentation source is missing: " +
                    label,
                    path);
            }

            using FileStream stream = File.OpenRead(path);
            using SHA256 sha = SHA256.Create();
            string actual = string.Concat(sha.ComputeHash(stream).Select(
                value => value.ToString("x2")));
            if (!string.Equals(
                    actual,
                    expected,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Expanded Shop source hash mismatch for '{label}'. " +
                    $"Expected {expected}, got {actual}.");
            }
        }

        private static bool IsSha256(string value) =>
            !string.IsNullOrWhiteSpace(value) &&
            Regex.IsMatch(
                value,
                "^[0-9a-fA-F]{64}$",
                RegexOptions.CultureInvariant);

        private static void EnsureAssetFolder(string assetPath)
        {
            string[] parts = assetPath.Split('/');
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

        private static string SanitizeAssetName(string value) =>
            new string(value.Select(character =>
                    char.IsLetterOrDigit(character) ||
                    character is '-' or '_'
                        ? character
                        : '_')
                .ToArray());

        /// <summary>
        /// Minimal reader for AssetRipper's legacy prefab YAML. The supplied
        /// layout contains duplicate component file IDs, so importing it as a
        /// Unity prefab is intentionally forbidden. Only reviewed transform,
        /// mesh/material and BoxCollider presentation data are consumed.
        /// </summary>
        private sealed class ExpandedShopPrefabEvidence
        {
            private static readonly Regex DocumentHeaderRegex = new(
                "^--- !u!(?<type>[0-9]+) &(?<id>[0-9]+)\\r?$",
                RegexOptions.Multiline | RegexOptions.CultureInvariant);

            private readonly IReadOnlyList<ExpandedShopPrefabNode> nodes;

            private ExpandedShopPrefabEvidence(
                IReadOnlyList<ExpandedShopPrefabNode> configuredNodes)
            {
                nodes = configuredNodes;
            }

            public static ExpandedShopPrefabEvidence Parse(string path)
            {
                string yaml = File.ReadAllText(path);
                MatchCollection headers = DocumentHeaderRegex.Matches(yaml);
                if (headers.Count == 0)
                {
                    throw new InvalidDataException(
                        "Expanded Shop shelf evidence has no YAML documents.");
                }

                var documents = new List<ExpandedShopYamlDocument>(
                    headers.Count);
                for (int index = 0; index < headers.Count; index++)
                {
                    Match header = headers[index];
                    int bodyStart = header.Index + header.Length;
                    if (bodyStart < yaml.Length && yaml[bodyStart] == '\r')
                    {
                        bodyStart++;
                    }
                    if (bodyStart < yaml.Length && yaml[bodyStart] == '\n')
                    {
                        bodyStart++;
                    }

                    int bodyEnd = index + 1 < headers.Count
                        ? headers[index + 1].Index
                        : yaml.Length;
                    documents.Add(new ExpandedShopYamlDocument(
                        ParseInt(header.Groups["type"].Value),
                        ParseLong(header.Groups["id"].Value),
                        yaml.Substring(bodyStart, bodyEnd - bodyStart)));
                }

                var byGameObjectId = new Dictionary<
                    long,
                    ExpandedShopPrefabNode>();
                for (int index = 0; index < documents.Count; index++)
                {
                    ExpandedShopYamlDocument document = documents[index];
                    if (document.TypeId != 1)
                    {
                        continue;
                    }

                    string name = ReadString(document.Body, "m_Name");
                    var node = new ExpandedShopPrefabNode(
                        document.FileId,
                        name,
                        ReadInt(document.Body, "m_IsActive", 1) != 0);
                    if (!byGameObjectId.TryAdd(document.FileId, node))
                    {
                        throw new InvalidDataException(
                            "Expanded Shop shelf evidence has a duplicate " +
                            "GameObject ID: " + document.FileId);
                    }
                }

                var byTransformId = new Dictionary<
                    long,
                    ExpandedShopPrefabNode>();
                for (int index = 0; index < documents.Count; index++)
                {
                    ExpandedShopYamlDocument document = documents[index];
                    if (document.TypeId is not (4 or 23 or 33 or 65))
                    {
                        continue;
                    }

                    long gameObjectId = ReadFileId(
                        document.Body,
                        "m_GameObject");
                    if (!byGameObjectId.TryGetValue(
                            gameObjectId,
                            out ExpandedShopPrefabNode node))
                    {
                        throw new InvalidDataException(
                            "Expanded Shop component references an unknown " +
                            "GameObject ID: " + gameObjectId);
                    }

                    switch (document.TypeId)
                    {
                        case 4:
                            node.ConfigureTransform(
                                document.FileId,
                                ReadVector3(
                                    document.Body,
                                    "m_LocalPosition",
                                    Vector3.zero),
                                ReadQuaternion(
                                    document.Body,
                                    "m_LocalRotation",
                                    Quaternion.identity),
                                ReadVector3(
                                    document.Body,
                                    "m_LocalScale",
                                    Vector3.one),
                                ReadFileId(
                                    document.Body,
                                    "m_Father",
                                    required: false),
                                ReadChildFileIds(document.Body));
                            if (!byTransformId.TryAdd(document.FileId, node))
                            {
                                throw new InvalidDataException(
                                    "Expanded Shop shelf evidence has a " +
                                    "duplicate Transform ID: " +
                                    document.FileId);
                            }
                            break;
                        case 23:
                            node.ConfigureRenderer(
                                ReadInt(
                                    document.Body,
                                    "m_Enabled",
                                    1) != 0,
                                ReadMaterialGuids(document.Body));
                            break;
                        case 33:
                            node.ConfigureMesh(ReadGuidReference(
                                document.Body,
                                "m_Mesh"));
                            break;
                        case 65:
                            node.ConfigureBoxCollider(
                                new ExpandedShopBoxColliderEvidence(
                                    ReadInt(
                                        document.Body,
                                        "m_IsTrigger",
                                        0) != 0,
                                    ReadVector3(
                                        document.Body,
                                        "m_Center",
                                        Vector3.zero),
                                    ReadVector3(
                                        document.Body,
                                        "m_Size",
                                        Vector3.one)));
                            break;
                    }
                }

                foreach (ExpandedShopPrefabNode parent in
                         byGameObjectId.Values)
                {
                    parent.ResolveChildren(byTransformId);
                }

                return new ExpandedShopPrefabEvidence(
                    byGameObjectId.Values.ToArray());
            }

            public ExpandedShopPrefabNode RequireRoot(string rootName)
            {
                ExpandedShopPrefabNode result = null;
                for (int index = 0; index < nodes.Count; index++)
                {
                    ExpandedShopPrefabNode node = nodes[index];
                    if (node.Parent != null ||
                        !string.Equals(
                            node.Name,
                            rootName,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (result != null)
                    {
                        throw new InvalidDataException(
                            "Expanded Shop shelf evidence has duplicate roots: " +
                            rootName);
                    }

                    result = node;
                }

                return result ?? throw new InvalidDataException(
                    "Expanded Shop shelf evidence root is missing: " +
                    rootName);
            }

            private static long[] ReadChildFileIds(string body)
            {
                Match block = Regex.Match(
                    body,
                    "^  m_Children:\\r?$" +
                    "(?<children>(?:\\r?\\n  - \\{fileID: -?[0-9]+\\})*)",
                    RegexOptions.Multiline |
                    RegexOptions.CultureInvariant);
                if (!block.Success)
                {
                    return Array.Empty<long>();
                }

                MatchCollection matches = Regex.Matches(
                    block.Groups["children"].Value,
                    "fileID: (?<id>-?[0-9]+)",
                    RegexOptions.CultureInvariant);
                var result = new long[matches.Count];
                for (int index = 0; index < matches.Count; index++)
                {
                    result[index] = ParseLong(
                        matches[index].Groups["id"].Value);
                }

                return result;
            }

            private static string[] ReadMaterialGuids(string body)
            {
                Match block = Regex.Match(
                    body,
                    "^  m_Materials:\\r?$" +
                    "(?<materials>(?:\\r?\\n  - \\{[^\\r\\n]*\\})*)",
                    RegexOptions.Multiline |
                    RegexOptions.CultureInvariant);
                if (!block.Success)
                {
                    return Array.Empty<string>();
                }

                MatchCollection matches = Regex.Matches(
                    block.Groups["materials"].Value,
                    "guid: (?<guid>[0-9a-fA-F]{32})",
                    RegexOptions.CultureInvariant);
                var result = new string[matches.Count];
                for (int index = 0; index < matches.Count; index++)
                {
                    result[index] = matches[index]
                        .Groups["guid"].Value.ToLowerInvariant();
                }

                return result;
            }

            private static string ReadGuidReference(
                string body,
                string field)
            {
                Match match = Regex.Match(
                    body,
                    "^  " + Regex.Escape(field) +
                    ": \\{[^\\r\\n]*guid: " +
                    "(?<guid>[0-9a-fA-F]{32})",
                    RegexOptions.Multiline |
                    RegexOptions.CultureInvariant);
                return match.Success
                    ? match.Groups["guid"].Value.ToLowerInvariant()
                    : string.Empty;
            }

            private static long ReadFileId(
                string body,
                string field,
                bool required = true)
            {
                Match match = Regex.Match(
                    body,
                    "^  " + Regex.Escape(field) +
                    ": \\{fileID: (?<id>-?[0-9]+)",
                    RegexOptions.Multiline |
                    RegexOptions.CultureInvariant);
                if (!match.Success)
                {
                    if (!required)
                    {
                        return 0L;
                    }

                    throw new InvalidDataException(
                        "Expanded Shop YAML field is missing: " + field);
                }

                return ParseLong(match.Groups["id"].Value);
            }

            private static string ReadString(string body, string field)
            {
                Match match = Regex.Match(
                    body,
                    "^  " + Regex.Escape(field) + ":(?<value>[^\\r\\n]*)",
                    RegexOptions.Multiline |
                    RegexOptions.CultureInvariant);
                if (!match.Success)
                {
                    throw new InvalidDataException(
                        "Expanded Shop YAML field is missing: " + field);
                }

                string value = match.Groups["value"].Value.Trim();
                if (value.Length >= 2 && value[0] == '"' &&
                    value[value.Length - 1] == '"')
                {
                    value = value.Substring(1, value.Length - 2);
                }

                return value;
            }

            private static int ReadInt(
                string body,
                string field,
                int fallback)
            {
                Match match = Regex.Match(
                    body,
                    "^  " + Regex.Escape(field) +
                    ": (?<value>-?[0-9]+)\\r?$",
                    RegexOptions.Multiline |
                    RegexOptions.CultureInvariant);
                return match.Success
                    ? ParseInt(match.Groups["value"].Value)
                    : fallback;
            }

            private static Vector3 ReadVector3(
                string body,
                string field,
                Vector3 fallback)
            {
                Match match = Regex.Match(
                    body,
                    "^  " + Regex.Escape(field) +
                    ": \\{x: (?<x>[^,}]+), y: (?<y>[^,}]+), " +
                    "z: (?<z>[^,}]+)\\}\\r?$",
                    RegexOptions.Multiline |
                    RegexOptions.CultureInvariant);
                return match.Success
                    ? new Vector3(
                        ParseFloat(match.Groups["x"].Value),
                        ParseFloat(match.Groups["y"].Value),
                        ParseFloat(match.Groups["z"].Value))
                    : fallback;
            }

            private static Quaternion ReadQuaternion(
                string body,
                string field,
                Quaternion fallback)
            {
                Match match = Regex.Match(
                    body,
                    "^  " + Regex.Escape(field) +
                    ": \\{x: (?<x>[^,}]+), y: (?<y>[^,}]+), " +
                    "z: (?<z>[^,}]+), w: (?<w>[^,}]+)\\}\\r?$",
                    RegexOptions.Multiline |
                    RegexOptions.CultureInvariant);
                return match.Success
                    ? new Quaternion(
                        ParseFloat(match.Groups["x"].Value),
                        ParseFloat(match.Groups["y"].Value),
                        ParseFloat(match.Groups["z"].Value),
                        ParseFloat(match.Groups["w"].Value))
                    : fallback;
            }

            private static int ParseInt(string value) => int.Parse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);

            private static long ParseLong(string value) => long.Parse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture);

            private static float ParseFloat(string value) => float.Parse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture);
        }

        private sealed class ExpandedShopPrefabNode
        {
            private readonly List<ExpandedShopPrefabNode> children = new();
            private long[] childTransformIds = Array.Empty<long>();
            private long fatherTransformId;
            private bool rendererEnabled;
            private string meshGuid = string.Empty;
            private string[] materialGuids = Array.Empty<string>();

            public ExpandedShopPrefabNode(
                long gameObjectId,
                string name,
                bool activeSelf)
            {
                GameObjectId = gameObjectId;
                Name = name;
                ActiveSelf = activeSelf;
                LocalRotation = Quaternion.identity;
                LocalScale = Vector3.one;
            }

            public long GameObjectId { get; }
            public string Name { get; }
            public bool ActiveSelf { get; }
            public long TransformFileId { get; private set; }
            public Vector3 LocalPosition { get; private set; }
            public Quaternion LocalRotation { get; private set; }
            public Vector3 LocalScale { get; private set; }
            public ExpandedShopPrefabNode Parent { get; private set; }
            public IReadOnlyList<ExpandedShopPrefabNode> Children => children;
            public string MeshGuid => meshGuid;
            public IReadOnlyList<string> MaterialGuids => materialGuids;
            public ExpandedShopBoxColliderEvidence BoxCollider {
                get;
                private set;
            }

            public void ConfigureTransform(
                long transformFileId,
                Vector3 localPosition,
                Quaternion localRotation,
                Vector3 localScale,
                long configuredFatherTransformId,
                long[] configuredChildTransformIds)
            {
                TransformFileId = transformFileId;
                LocalPosition = localPosition;
                LocalRotation = localRotation;
                LocalScale = localScale;
                fatherTransformId = configuredFatherTransformId;
                childTransformIds = configuredChildTransformIds ??
                    Array.Empty<long>();
            }

            public void ConfigureRenderer(
                bool configuredEnabled,
                string[] configuredMaterialGuids)
            {
                rendererEnabled = configuredEnabled;
                materialGuids = configuredMaterialGuids ??
                    Array.Empty<string>();
            }

            public void ConfigureMesh(string configuredMeshGuid) =>
                meshGuid = configuredMeshGuid ?? string.Empty;

            public void ConfigureBoxCollider(
                ExpandedShopBoxColliderEvidence configuredCollider) =>
                BoxCollider = configuredCollider;

            public void ResolveChildren(
                IReadOnlyDictionary<long, ExpandedShopPrefabNode> byTransformId)
            {
                children.Clear();
                for (int index = 0;
                     index < childTransformIds.Length;
                     index++)
                {
                    long childTransformId = childTransformIds[index];
                    if (!byTransformId.TryGetValue(
                            childTransformId,
                            out ExpandedShopPrefabNode child))
                    {
                        throw new InvalidDataException(
                            "Expanded Shop hierarchy references an unknown " +
                            "Transform ID: " + childTransformId);
                    }

                    children.Add(child);
                    if (child.Parent != null && child.Parent != this)
                    {
                        throw new InvalidDataException(
                            "Expanded Shop hierarchy assigns two parents to: " +
                            child.Name);
                    }

                    child.Parent = this;
                }

                if (fatherTransformId != 0 && Parent != null &&
                    Parent.TransformFileId != fatherTransformId)
                {
                    throw new InvalidDataException(
                        "Expanded Shop hierarchy father mismatch for: " +
                        Name);
                }
            }

            public ExpandedShopPrefabNode FindDirectChild(string childName)
            {
                for (int index = 0; index < children.Count; index++)
                {
                    if (string.Equals(
                            children[index].Name,
                            childName,
                            StringComparison.Ordinal))
                    {
                        return children[index];
                    }
                }

                return null;
            }

            public IReadOnlyList<ExpandedShopPrefabNode>
                GetRenderableDescendants()
            {
                var result = new List<ExpandedShopPrefabNode>();
                CollectRenderable(this, ancestorsActive: true, result);
                return result;
            }

            public bool IsSelfOrDescendantOf(
                ExpandedShopPrefabNode ancestor)
            {
                for (ExpandedShopPrefabNode current = this;
                     current != null;
                     current = current.Parent)
                {
                    if (current == ancestor)
                    {
                        return true;
                    }
                }

                return false;
            }

            public Matrix4x4 GetMatrixRelativeTo(
                ExpandedShopPrefabNode ancestor)
            {
                if (ancestor == null)
                {
                    throw new ArgumentNullException(nameof(ancestor));
                }

                if (this == ancestor)
                {
                    return Matrix4x4.identity;
                }

                var chain = new List<ExpandedShopPrefabNode>();
                ExpandedShopPrefabNode current = this;
                while (current != null && current != ancestor)
                {
                    chain.Add(current);
                    current = current.Parent;
                }

                if (current != ancestor)
                {
                    throw new InvalidDataException(
                        $"Expanded Shop node '{Name}' is not below " +
                        $"'{ancestor.Name}'.");
                }

                Matrix4x4 result = Matrix4x4.identity;
                for (int index = chain.Count - 1; index >= 0; index--)
                {
                    ExpandedShopPrefabNode node = chain[index];
                    result *= Matrix4x4.TRS(
                        node.LocalPosition,
                        node.LocalRotation,
                        node.LocalScale);
                }

                return result;
            }

            private static void CollectRenderable(
                ExpandedShopPrefabNode node,
                bool ancestorsActive,
                ICollection<ExpandedShopPrefabNode> result)
            {
                bool active = ancestorsActive && node.ActiveSelf;
                if (active && node.rendererEnabled &&
                    !string.IsNullOrWhiteSpace(node.meshGuid) &&
                    node.materialGuids.Length > 0)
                {
                    result.Add(node);
                }

                for (int index = 0; index < node.children.Count; index++)
                {
                    CollectRenderable(node.children[index], active, result);
                }
            }
        }

        private sealed class ExpandedShopBoxColliderEvidence
        {
            public ExpandedShopBoxColliderEvidence(
                bool isTrigger,
                Vector3 center,
                Vector3 size)
            {
                IsTrigger = isTrigger;
                Center = center;
                Size = size;
            }

            public bool IsTrigger { get; }
            public Vector3 Center { get; }
            public Vector3 Size { get; }
        }

        private readonly struct ExpandedShopYamlDocument
        {
            public ExpandedShopYamlDocument(
                int typeId,
                long fileId,
                string body)
            {
                TypeId = typeId;
                FileId = fileId;
                Body = body;
            }

            public int TypeId { get; }
            public long FileId { get; }
            public string Body { get; }
        }

        [Serializable]
        private sealed class ExpandedShopPresentationManifest
        {
            public int schemaVersion;
            public string manifestId;
            public string classification;
            public ExpandedShopPresentationSource source;
            public ExpandedShopShelfGroupSource[] shelfGroups;
            public ExpandedShopPresentationItem[] items;
        }

        [Serializable]
        private sealed class ExpandedShopPresentationSource
        {
            public string stagingRootRelativePath;
            public string bundleRelativePath;
            public string bundleSha256;
            public string dllSha256;
            public string shelfPrefabRelativePath;
            public string shelfPrefabSha256;
            public Vector3 projectStoreRootWorldPosition;
            public Quaternion projectStoreRootWorldRotation;
            public Vector3 projectStoreRootWorldScale;
        }

        [Serializable]
        private sealed class ExpandedShopShelfGroupSource
        {
            public string groupId;
            public string sourceGroupName;
            public string offerId;
            public int expectedStockUnitCount;
            public int stockIndexOffset;
        }

        [Serializable]
        private sealed class ExpandedShopPresentationItem
        {
            public string definitionId;
            public string prefabRelativePath;
            public string prefabSha256;
            public bool zUpModel;
        }
    }
}
