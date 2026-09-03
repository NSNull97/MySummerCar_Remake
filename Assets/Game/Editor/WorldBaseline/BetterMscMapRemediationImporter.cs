using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using MSC.Editor.WorldTransfer;
using MSC.LegacyImport;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldBaseline
{
    public static class BetterMscMapRemediationImporter
    {
        private const string SourceExportRelativePath =
            "raw/bettermsc-reference/assetbundle-export/" +
            "ExportedProject/Assets";
        private const string GeneratedRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/" +
            "BetterMscMapRemediation";
        private const string ImportedSourceRoot =
            GeneratedRoot + "/Source";
        private const string GeneratedMaterialRoot =
            GeneratedRoot + "/Materials";
        private const string ForestMaterialPath =
            GeneratedMaterialRoot + "/BetterMscForest_HDRP.mat";
        private const string OverlayRootName =
            "BETTERMSC_MAP_REMEDIATION_TEMPORARY_DIRECT_IMPORT";
        private const string ForestsPrefabRelativePath =
            "prefabs/summer/Forests.prefab";
        private const string MissingTerrainPrefabRelativePath =
            "prefabs/summer/MissingTerrain.prefab";
        private const string TreeWallLowPrefabRelativePath =
            "prefabs/summer and winter/TREEWALL_LOW.prefab";
        private const string TreeWallHighPrefabRelativePath =
            "prefabs/summer and winter/TREEWALL_HI.prefab";
        private const string ForestTextureGuid =
            "fb3d080817d42db4eb38b054f184bec2";
        private const string TreeWallLowPath =
            "MAP/MESH/FOLIAGE/TREEWALL_LOW";
        private const string TreeWallHighPath =
            "MAP/MESH/FOLIAGE/TREEWALL_HI";
        private const string TreeWallLowColliderPath =
            TreeWallLowPath + "/treewallcoll";
        private const string TreeWallHighColliderPath =
            TreeWallHighPath + "/treewallcoll";
        private const string MapYUpAnchorPath =
            "MAP/MESH/TERRAIN_OBJ/Grass1";
        private const string ObsoleteTreeWallFixPath =
            TreeWallLowPath + "/worldsmostsimplefix";
        private const string TemporaryGrassMaterialPath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/" +
            "Materials/LegacyTextured/" +
            "M06B2_da5bc03c62a0f174197555e90357aac9.mat";

        private static readonly string[] EntryPrefabRelativePaths =
        {
            ForestsPrefabRelativePath,
            MissingTerrainPrefabRelativePath,
            TreeWallLowPrefabRelativePath,
            TreeWallHighPrefabRelativePath
        };

        private static readonly Regex GuidReferencePattern =
            new Regex(
                @"guid:\s*([0-9a-fA-F]{32})",
                RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [MenuItem(
            "Tools/MSC Remake/World Baseline/" +
            "Apply BetterMSC Map Remediation")]
        public static void ApplyFromMenu()
        {
            Apply();
        }

        public static void RunBatch()
        {
            Apply();
        }

        public static void Apply()
        {
            WorldTransferEditorConfiguration configuration =
                WorldTransferEditorConfiguration.Load();
            string sourceRoot = Path.GetFullPath(
                Path.Combine(
                    configuration.DonorStagingPath,
                    SourceExportRelativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            AssertSourceRoot(configuration.DonorStagingPath, sourceRoot);

            IReadOnlyDictionary<string, string> sourceGuidMap =
                BuildSourceGuidMap(sourceRoot);
            IReadOnlyCollection<string> dependencies =
                CollectDependencies(sourceRoot, sourceGuidMap);
            CopyDependencies(sourceRoot, dependencies);

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            ConfigureForestTexture(sourceGuidMap);
            Material forestMaterial = CreateOrUpdateForestMaterial();
            Material grassMaterial =
                AssetDatabase.LoadAssetAtPath<Material>(
                    TemporaryGrassMaterialPath);
            if (grassMaterial == null)
            {
                throw new FileNotFoundException(
                    "The existing donor-baseline Grass1 compatibility " +
                    "material is missing.",
                    ToAbsoluteProjectPath(TemporaryGrassMaterialPath));
            }

            PhysicsMaterial worldSolidMaterial =
                AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(
                    DonorWorldSolidCollisionPolicy
                        .WorldSolidPhysicsMaterial);
            if (worldSolidMaterial == null)
            {
                throw new FileNotFoundException(
                    "The project-owned WorldSolid physics material is " +
                    "missing.",
                    ToAbsoluteProjectPath(
                        DonorWorldSolidCollisionPolicy
                            .WorldSolidPhysicsMaterial));
            }

            Scene scene = EditorSceneManager.OpenScene(
                WorldBaseline06B2Paths.GlobalScene,
                OpenSceneMode.Single);
            try
            {
                GameObject contentRoot = FindContentRoot(scene);
                RemoveExistingOverlay(contentRoot);
                GameObject overlayRoot = CreateOverlayRoot(contentRoot);
                Transform mapYUpAnchor =
                    FindEntity(scene, MapYUpAnchorPath).transform;

                GameObject forests = InstantiateSourcePrefab(
                    ForestsPrefabRelativePath,
                    overlayRoot.transform,
                    mapYUpAnchor,
                    "Detailed Forest Replacement");
                ConfigureForestPresentation(
                    forests,
                    forestMaterial,
                    worldSolidMaterial);
                ConfigureMetadata(
                    forests,
                    "bettermsc-remediation:forests",
                    "BetterMSC/Forests",
                    "VegetationTree",
                    true);

                GameObject missingTerrain = InstantiateSourcePrefab(
                    MissingTerrainPrefabRelativePath,
                    overlayRoot.transform,
                    mapYUpAnchor,
                    "Continuous Terrain Void Fill");
                ConfigureTerrainFill(
                    missingTerrain,
                    grassMaterial,
                    worldSolidMaterial);
                ConfigureMetadata(
                    missingTerrain,
                    "bettermsc-remediation:missing-terrain",
                    "BetterMSC/MissingTerrain",
                    "Terrain",
                    true);

                ReplaceTreeWalls(
                    scene,
                    worldSolidMaterial);
                DisableObsoleteTreeWallFix(scene);
                ValidateAppliedScene(
                    scene,
                    overlayRoot,
                    forests,
                    missingTerrain,
                    mapYUpAnchor);

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(
                        scene,
                        WorldBaseline06B2Paths.GlobalScene))
                {
                    throw new IOException(
                        "Failed to save the remediated global world scene.");
                }
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
            }

            Debug.Log(
                "BETTERMSC_MAP_REMEDIATION_APPLY_OK " +
                $"dependencies={dependencies.Count} " +
                "terrainVoidFill=1 forests=1 treeWalls=2");
        }

        private static void AssertSourceRoot(
            string stagingRoot,
            string sourceRoot)
        {
            string normalizedStaging =
                Path.GetFullPath(stagingRoot)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!sourceRoot.StartsWith(
                    normalizedStaging,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "BetterMSC reference export resolves outside donor " +
                    "staging.");
            }
            if (!Directory.Exists(sourceRoot))
            {
                throw new DirectoryNotFoundException(
                    "AssetRipper BetterMSC reference export is missing: " +
                    sourceRoot);
            }

            foreach (string relativePath in EntryPrefabRelativePaths)
            {
                string absolute = ResolveBelow(
                    sourceRoot,
                    relativePath);
                if (!File.Exists(absolute) ||
                    !File.Exists(absolute + ".meta"))
                {
                    throw new FileNotFoundException(
                        "Required BetterMSC reference prefab or its meta " +
                        "file is missing.",
                        absolute);
                }
            }
        }

        private static IReadOnlyDictionary<string, string>
            BuildSourceGuidMap(string sourceRoot)
        {
            var result = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string metaPath in Directory.EnumerateFiles(
                         sourceRoot,
                         "*.meta",
                         SearchOption.AllDirectories))
            {
                Match match = GuidReferencePattern.Match(
                    File.ReadAllText(metaPath));
                if (!match.Success)
                {
                    continue;
                }

                string guid = match.Groups[1].Value.ToLowerInvariant();
                string assetPath =
                    metaPath.Substring(0, metaPath.Length - ".meta".Length);
                if (!result.TryAdd(guid, assetPath))
                {
                    throw new InvalidDataException(
                        "AssetRipper BetterMSC export contains duplicate " +
                        "GUID: " + guid);
                }
            }

            if (!result.ContainsKey(ForestTextureGuid))
            {
                throw new InvalidDataException(
                    "BetterMSC forest atlas GUID is absent from the " +
                    "reference export.");
            }
            return result;
        }

        private static IReadOnlyCollection<string> CollectDependencies(
            string sourceRoot,
            IReadOnlyDictionary<string, string> sourceGuidMap)
        {
            var pending = new Queue<string>();
            var result = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (string relativePath in EntryPrefabRelativePaths)
            {
                pending.Enqueue(ResolveBelow(sourceRoot, relativePath));
            }

            while (pending.Count > 0)
            {
                string sourceAsset = pending.Dequeue();
                if (!result.Add(sourceAsset))
                {
                    continue;
                }

                string extension =
                    Path.GetExtension(sourceAsset).ToLowerInvariant();
                if (extension == ".png" ||
                    extension == ".jpg" ||
                    extension == ".jpeg")
                {
                    continue;
                }

                string text = File.ReadAllText(sourceAsset);
                foreach (Match match in GuidReferencePattern.Matches(text))
                {
                    string guid =
                        match.Groups[1].Value.ToLowerInvariant();
                    if (!sourceGuidMap.TryGetValue(
                            guid,
                            out string dependency))
                    {
                        continue;
                    }

                    string dependencyExtension =
                        Path.GetExtension(dependency).ToLowerInvariant();
                    if (dependencyExtension == ".shader" ||
                        dependencyExtension == ".cs" ||
                        dependencyExtension == ".dll")
                    {
                        continue;
                    }
                    pending.Enqueue(dependency);
                }
            }

            return result
                .OrderBy(
                    path => path,
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static void CopyDependencies(
            string sourceRoot,
            IReadOnlyCollection<string> sourceAssets)
        {
            EnsureAssetFolder(ImportedSourceRoot);
            foreach (string sourceAsset in sourceAssets)
            {
                string relativePath = Path.GetRelativePath(
                        sourceRoot,
                        sourceAsset)
                    .Replace('\\', '/');
                string destinationAsset =
                    ImportedSourceRoot + "/" + relativePath;
                EnsureAssetFolder(destinationAsset);
                AssertNoGuidCollision(
                    sourceAsset + ".meta",
                    destinationAsset);
                CopyIfChanged(sourceAsset, destinationAsset);
                CopyIfChanged(
                    sourceAsset + ".meta",
                    destinationAsset + ".meta");
            }
        }

        private static void AssertNoGuidCollision(
            string sourceMeta,
            string destinationAsset)
        {
            Match match = GuidReferencePattern.Match(
                File.ReadAllText(sourceMeta));
            if (!match.Success)
            {
                throw new InvalidDataException(
                    "Source meta file has no canonical GUID: " +
                    sourceMeta);
            }

            string existingPath = AssetDatabase.GUIDToAssetPath(
                match.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(existingPath) &&
                !string.Equals(
                    existingPath,
                    destinationAsset,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "BetterMSC reference GUID collides with an existing " +
                    "project asset. GUID=" +
                    match.Groups[1].Value +
                    " existing=" +
                    existingPath);
            }
        }

        private static void ConfigureForestTexture(
            IReadOnlyDictionary<string, string> sourceGuidMap)
        {
            string externalTexture = sourceGuidMap[ForestTextureGuid];
            WorldTransferEditorConfiguration configuration =
                WorldTransferEditorConfiguration.Load();
            string sourceRoot = Path.GetFullPath(
                Path.Combine(
                    configuration.DonorStagingPath,
                    SourceExportRelativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            string relativePath = Path.GetRelativePath(
                    sourceRoot,
                    externalTexture).Replace('\\', '/');
            string textureAssetPath =
                ImportedSourceRoot + "/" + relativePath;
            TextureImporter importer =
                AssetImporter.GetAtPath(textureAssetPath) as
                    TextureImporter;
            if (importer == null)
            {
                throw new InvalidDataException(
                    "BetterMSC forest atlas did not import as a texture: " +
                    textureAssetPath);
            }

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource =
                TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static Material CreateOrUpdateForestMaterial()
        {
            EnsureAssetFolder(ForestMaterialPath);
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "HDRP/Lit shader is unavailable.");
            }

            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    ForestMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "BetterMSC Forest HDRP Compatibility"
                };
                AssetDatabase.CreateAsset(
                    material,
                    ForestMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            string texturePath =
                AssetDatabase.GUIDToAssetPath(ForestTextureGuid);
            Texture texture =
                AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
            if (texture == null ||
                !texturePath.StartsWith(
                    ImportedSourceRoot + "/",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Imported BetterMSC forest atlas is unavailable.");
            }

            SetFloatIfPresent(material, "_SurfaceType", 0f);
            SetFloatIfPresent(material, "_AlphaCutoffEnable", 1f);
            SetFloatIfPresent(material, "_AlphaCutoff", 0.5f);
            SetFloatIfPresent(material, "_DoubleSidedEnable", 1f);
            SetFloatIfPresent(material, "_DoubleSidedNormalMode", 0f);
            SetVectorIfPresent(
                material,
                "_DoubleSidedConstants",
                new Vector4(-1f, -1f, -1f, 0f));
            SetFloatIfPresent(
                material,
                "_CullMode",
                (float)CullMode.Off);
            SetFloatIfPresent(
                material,
                "_CullModeForward",
                (float)CullMode.Off);
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.08f);
            SetFloatIfPresent(material, "_ZWrite", 1f);
            SetColorIfPresent(material, "_BaseColor", Color.white);
            SetTextureIfPresent(material, "_BaseColorMap", texture);
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_DOUBLESIDED_ON");
            material.SetOverrideTag("RenderType", "TransparentCutout");
            if (!HDMaterial.ValidateMaterial(material))
            {
                throw new InvalidOperationException(
                    "Generated BetterMSC forest compatibility material " +
                    "is not a valid HDRP material.");
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject FindContentRoot(Scene scene)
        {
            GameObject sceneRoot = scene
                .GetRootGameObjects()
                .SingleOrDefault(
                    root => string.Equals(
                        root.name,
                        "World_Global_Legacy",
                        StringComparison.Ordinal));
            if (sceneRoot == null)
            {
                throw new InvalidDataException(
                    "World_Global_Legacy root is missing.");
            }

            Transform content = sceneRoot.transform.Find(
                "TEMPORARY_DIRECT_IMPORT_ENTITIES");
            if (content == null)
            {
                throw new InvalidDataException(
                    "Global donor content root is missing.");
            }
            return content.gameObject;
        }

        private static void RemoveExistingOverlay(GameObject contentRoot)
        {
            Transform existing =
                contentRoot.transform.Find(OverlayRootName);
            if (existing != null)
            {
                UnityEngine.Object.DestroyImmediate(existing.gameObject);
            }
        }

        private static GameObject CreateOverlayRoot(
            GameObject contentRoot)
        {
            var root = new GameObject(OverlayRootName);
            SceneManager.MoveGameObjectToScene(
                root,
                contentRoot.scene);
            root.transform.SetParent(contentRoot.transform, false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            ConfigureMetadata(
                root,
                "bettermsc-remediation:root",
                "BetterMSC",
                "WorldLayoutReference",
                false);
            return root;
        }

        private static GameObject InstantiateSourcePrefab(
            string relativePath,
            Transform parent,
            Transform mapYUpAnchor,
            string displayName)
        {
            string assetPath =
                ImportedSourceRoot + "/" + relativePath;
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                throw new FileNotFoundException(
                    "Imported BetterMSC prefab is unavailable.",
                    ToAbsoluteProjectPath(assetPath));
            }

            GameObject instance =
                PrefabUtility.InstantiatePrefab(
                    prefab,
                    parent.gameObject.scene) as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Failed to instantiate BetterMSC reference prefab: " +
                    assetPath);
            }
            instance.name = displayName;
            instance.transform.SetParent(parent, false);
            AlignToMapYUpAnchor(instance.transform, mapYUpAnchor);
            return instance;
        }

        private static void AlignToMapYUpAnchor(
            Transform target,
            Transform mapYUpAnchor)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            if (mapYUpAnchor == null)
            {
                throw new ArgumentNullException(nameof(mapYUpAnchor));
            }
            if (!ApproximatelyOne(mapYUpAnchor.lossyScale))
            {
                throw new InvalidDataException(
                    "The canonical map Y-up anchor has a non-unit scale. " +
                    "The BetterMSC alignment contract must be reviewed.");
            }

            // AssetRipper writes a +90-degree X rotation on these prefab
            // roots even though their exported mesh vertices are already
            // Y-up. Reusing that prefab rotation turns the map layer
            // vertical. The generated Grass1 entity is a flattened,
            // canonical map-space anchor, so its world pose is authoritative.
            target.SetPositionAndRotation(
                mapYUpAnchor.position,
                mapYUpAnchor.rotation);
            target.localScale = Vector3.one;
            EditorUtility.SetDirty(target);
        }

        private static void ConfigureForestPresentation(
            GameObject forests,
            Material forestMaterial,
            PhysicsMaterial worldSolidMaterial)
        {
            MeshRenderer[] renderers =
                forests.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidDataException(
                    "BetterMSC Forests prefab contains no renderers.");
            }
            foreach (MeshRenderer renderer in renderers)
            {
                int slotCount = Math.Max(
                    1,
                    renderer.sharedMaterials.Length);
                renderer.sharedMaterials =
                    Enumerable.Repeat(
                            forestMaterial,
                            slotCount)
                        .ToArray();
                renderer.shadowCastingMode =
                    ShadowCastingMode.TwoSided;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage =
                    ReflectionProbeUsage.BlendProbes;
                renderer.gameObject.layer = 0;
                SetStaticFlags(renderer.gameObject);
            }

            int worldSolidLayer =
                LayerMask.NameToLayer(
                    DonorWorldSolidCollisionPolicy.WorldSolidLayer);
            if (worldSolidLayer < 0)
            {
                throw new InvalidOperationException(
                    "WorldSolid layer is unavailable.");
            }
            MeshCollider[] colliders =
                forests.GetComponentsInChildren<MeshCollider>(true);
            if (colliders.Length == 0)
            {
                throw new InvalidDataException(
                    "BetterMSC Forests prefab contains no colliders.");
            }
            foreach (MeshCollider collider in colliders)
            {
                collider.convex = false;
                collider.isTrigger = false;
                collider.sharedMaterial = worldSolidMaterial;
                collider.gameObject.layer = worldSolidLayer;
                SetStaticFlags(collider.gameObject);
            }
        }

        private static void ConfigureTerrainFill(
            GameObject missingTerrain,
            Material grassMaterial,
            PhysicsMaterial worldSolidMaterial)
        {
            MeshRenderer renderer =
                missingTerrain.GetComponentInChildren<MeshRenderer>(true);
            MeshCollider collider =
                missingTerrain.GetComponentInChildren<MeshCollider>(true);
            MeshFilter filter =
                missingTerrain.GetComponentInChildren<MeshFilter>(true);
            if (renderer == null ||
                collider == null ||
                filter == null ||
                filter.sharedMesh == null)
            {
                throw new InvalidDataException(
                    "BetterMSC MissingTerrain prefab is incomplete.");
            }

            renderer.sharedMaterial = grassMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage =
                ReflectionProbeUsage.BlendProbes;
            collider.sharedMesh = filter.sharedMesh;
            collider.convex = false;
            collider.isTrigger = false;
            collider.sharedMaterial = worldSolidMaterial;
            int worldSolidLayer =
                LayerMask.NameToLayer(
                    DonorWorldSolidCollisionPolicy.WorldSolidLayer);
            if (worldSolidLayer < 0)
            {
                throw new InvalidOperationException(
                    "WorldSolid layer is unavailable.");
            }
            collider.gameObject.layer = worldSolidLayer;
            SetStaticFlags(missingTerrain);
        }

        private static void ReplaceTreeWalls(
            Scene scene,
            PhysicsMaterial worldSolidMaterial)
        {
            Mesh lowMesh = LoadPrefabMesh(
                TreeWallLowPrefabRelativePath);
            Mesh highMesh = LoadPrefabMesh(
                TreeWallHighPrefabRelativePath);
            ReplaceTreeWall(
                scene,
                TreeWallLowPath,
                TreeWallLowColliderPath,
                lowMesh,
                worldSolidMaterial,
                colliderRequired: true);
            ReplaceTreeWall(
                scene,
                TreeWallHighPath,
                TreeWallHighColliderPath,
                highMesh,
                worldSolidMaterial,
                colliderRequired: false);
        }

        private static Mesh LoadPrefabMesh(string relativePath)
        {
            string assetPath =
                ImportedSourceRoot + "/" + relativePath;
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            MeshFilter filter =
                prefab != null
                    ? prefab.GetComponentInChildren<MeshFilter>(true)
                    : null;
            if (filter == null || filter.sharedMesh == null)
            {
                throw new InvalidDataException(
                    "BetterMSC tree-wall prefab has no mesh: " +
                    assetPath);
            }
            return filter.sharedMesh;
        }

        private static void ReplaceTreeWall(
            Scene scene,
            string rendererPath,
            string colliderPath,
            Mesh replacementMesh,
            PhysicsMaterial worldSolidMaterial,
            bool colliderRequired)
        {
            DonorWorldBaselineEntityMetadata rendererEntity =
                FindEntity(scene, rendererPath);
            MeshFilter filter =
                rendererEntity.GetComponent<MeshFilter>();
            if (filter == null)
            {
                throw new InvalidDataException(
                    "Tree-wall entity has no MeshFilter: " +
                    rendererPath);
            }
            filter.sharedMesh = replacementMesh;
            EditorUtility.SetDirty(filter);

            DonorWorldBaselineEntityMetadata colliderEntity =
                FindEntity(scene, colliderPath);
            MeshCollider[] colliders =
                colliderEntity.GetComponentsInChildren<MeshCollider>(true);
            if (colliders.Length == 0)
            {
                if (colliderRequired)
                {
                    throw new InvalidDataException(
                        "Tree-wall collider entity has no generated " +
                        "MeshCollider: " + colliderPath);
                }
                return;
            }
            int worldSolidLayer =
                LayerMask.NameToLayer(
                    DonorWorldSolidCollisionPolicy.WorldSolidLayer);
            foreach (MeshCollider collider in colliders)
            {
                collider.sharedMesh = replacementMesh;
                collider.sharedMaterial = worldSolidMaterial;
                collider.convex = false;
                collider.isTrigger = false;
                collider.gameObject.layer = worldSolidLayer;
                EditorUtility.SetDirty(collider);
            }
        }

        private static void DisableObsoleteTreeWallFix(Scene scene)
        {
            DonorWorldBaselineEntityMetadata obsolete =
                FindEntity(scene, ObsoleteTreeWallFixPath);
            obsolete.gameObject.SetActive(false);
            EditorUtility.SetDirty(obsolete.gameObject);
        }

        private static DonorWorldBaselineEntityMetadata FindEntity(
            Scene scene,
            string sourceHierarchyPath)
        {
            DonorWorldBaselineEntityMetadata[] entities = scene
                .GetRootGameObjects()
                .SelectMany(
                    root => root.GetComponentsInChildren<
                        DonorWorldBaselineEntityMetadata>(true))
                .Where(
                    entity => string.Equals(
                        entity.SourceHierarchyPath,
                        sourceHierarchyPath,
                        StringComparison.Ordinal))
                .ToArray();
            if (entities.Length != 1)
            {
                throw new InvalidDataException(
                    "Expected exactly one donor entity for '" +
                    sourceHierarchyPath +
                    "', found " +
                    entities.Length +
                    ".");
            }
            return entities[0];
        }

        private static void ConfigureMetadata(
            GameObject target,
            string stableSource,
            string sourceHierarchyPath,
            string semanticCategory,
            bool hasRenderer)
        {
            DonorWorldBaselineEntityMetadata metadata =
                target.GetComponent<DonorWorldBaselineEntityMetadata>() ??
                target.AddComponent<DonorWorldBaselineEntityMetadata>();
            metadata.Configure(
                StableIdFor(stableSource),
                0,
                string.Empty,
                sourceHierarchyPath,
                string.Empty,
                "global",
                semanticCategory,
                hasRenderer ? "4;23;33;64" : "4;114",
                hasRenderer
                    ? "BetterMscReferenceRemediation"
                    : "RemediationGroup",
                "User-approved BetterMSC map remediation reference.",
                true,
                true,
                hasRenderer);
            EditorUtility.SetDirty(metadata);
        }

        private static void ValidateAppliedScene(
            Scene scene,
            GameObject overlayRoot,
            GameObject forests,
            GameObject missingTerrain,
            Transform mapYUpAnchor)
        {
            if (!overlayRoot.activeInHierarchy ||
                forests.GetComponentsInChildren<MeshRenderer>(true)
                    .Length < 20 ||
                forests.GetComponentsInChildren<MeshCollider>(true)
                    .Length < 10)
            {
                throw new InvalidDataException(
                    "Detailed forest remediation is incomplete.");
            }
            if (missingTerrain.GetComponentInChildren<MeshRenderer>(true) ==
                    null ||
                missingTerrain.GetComponentInChildren<MeshCollider>(true) ==
                    null)
            {
                throw new InvalidDataException(
                    "Terrain-void remediation is incomplete.");
            }
            ValidateMapYUpAlignment(
                forests,
                mapYUpAnchor,
                "Detailed forest remediation",
                maximumHeight: 250f);
            ValidateMapYUpAlignment(
                missingTerrain,
                mapYUpAnchor,
                "Terrain-void remediation",
                maximumHeight: 100f);
            if (FindEntity(
                    scene,
                    ObsoleteTreeWallFixPath)
                .gameObject.activeSelf)
            {
                throw new InvalidDataException(
                    "Obsolete tree-wall fix remains active.");
            }
        }

        private static void ValidateMapYUpAlignment(
            GameObject root,
            Transform mapYUpAnchor,
            string label,
            float maximumHeight)
        {
            if (Vector3.Distance(
                    root.transform.position,
                    mapYUpAnchor.position) > 0.01f ||
                Quaternion.Angle(
                    root.transform.rotation,
                    mapYUpAnchor.rotation) > 0.01f ||
                !ApproximatelyOne(root.transform.lossyScale))
            {
                throw new InvalidDataException(
                    label +
                    " is not aligned to the canonical map Y-up anchor.");
            }

            Renderer[] renderers =
                root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidDataException(
                    label + " contains no renderer bounds.");
            }

            Bounds combined = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                combined.Encapsulate(renderers[index].bounds);
            }

            if (combined.size.x < 1000f ||
                combined.size.z < 1000f ||
                combined.size.y > maximumHeight)
            {
                throw new InvalidDataException(
                    label +
                    " has invalid map-space bounds " +
                    combined.size +
                    ". This usually means an AssetRipper axis rotation " +
                    "was applied twice.");
            }
        }

        private static bool ApproximatelyOne(Vector3 value)
        {
            return Mathf.Abs(value.x - 1f) < 0.0001f &&
                   Mathf.Abs(value.y - 1f) < 0.0001f &&
                   Mathf.Abs(value.z - 1f) < 0.0001f;
        }

        private static void SetStaticFlags(GameObject target)
        {
            GameObjectUtility.SetStaticEditorFlags(
                target,
                StaticEditorFlags.OccludeeStatic);
        }

        private static string StableIdFor(string source)
        {
            using SHA256 algorithm = SHA256.Create();
            byte[] hash =
                algorithm.ComputeHash(Encoding.UTF8.GetBytes(source));
            var builder = new StringBuilder(32);
            for (int index = 0; index < 16; index++)
            {
                builder.Append(hash[index].ToString("x2"));
            }
            return builder.ToString();
        }

        private static string ResolveBelow(
            string root,
            string relativePath)
        {
            string normalizedRoot =
                Path.GetFullPath(root)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(
                Path.Combine(
                    normalizedRoot,
                    relativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(
                    normalizedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Reference dependency escapes the BetterMSC export " +
                    "root: " + relativePath);
            }
            return candidate;
        }

        private static void CopyIfChanged(
            string source,
            string destinationAssetPath)
        {
            string destination = ToAbsoluteProjectPath(
                destinationAssetPath);
            if (File.Exists(destination) &&
                FilesEqual(source, destination))
            {
                return;
            }
            Directory.CreateDirectory(
                Path.GetDirectoryName(destination) ??
                throw new InvalidDataException(
                    "Destination asset has no parent directory."));
            File.Copy(source, destination, true);
        }

        private static bool FilesEqual(string left, string right)
        {
            var leftInfo = new FileInfo(left);
            var rightInfo = new FileInfo(right);
            if (leftInfo.Length != rightInfo.Length)
            {
                return false;
            }
            using SHA256 algorithm = SHA256.Create();
            using FileStream leftStream = File.OpenRead(left);
            using FileStream rightStream = File.OpenRead(right);
            byte[] leftHash = algorithm.ComputeHash(leftStream);
            byte[] rightHash = algorithm.ComputeHash(rightStream);
            return leftHash.SequenceEqual(rightHash);
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string folder = Path.HasExtension(assetPath)
                ? Path.GetDirectoryName(assetPath)?.Replace('\\', '/')
                : assetPath;
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException(
                    "Asset path has no folder.",
                    nameof(assetPath));
            }
            string[] parts = folder.Split('/');
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

        private static string ToAbsoluteProjectPath(string assetPath)
        {
            return Path.GetFullPath(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    assetPath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
        }

        private static void SetFloatIfPresent(
            Material material,
            string property,
            float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static void SetVectorIfPresent(
            Material material,
            string property,
            Vector4 value)
        {
            if (material.HasProperty(property))
            {
                material.SetVector(property, value);
            }
        }

        private static void SetColorIfPresent(
            Material material,
            string property,
            Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }

        private static void SetTextureIfPresent(
            Material material,
            string property,
            Texture value)
        {
            if (material.HasProperty(property))
            {
                material.SetTexture(property, value);
            }
        }
    }
}
