using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using MSC.World.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Editor.WorldBaseline
{
    public static class Phase1VegetationAssetBuilder
    {
        public const string GeneratedRoot =
            "Assets/Game/Presentation/Vegetation/Generated/Phase1";
        public const string SprucePrefabRoot =
            GeneratedRoot + "/Prefabs/Spruce";
        public const string NorwaySprucePrefabRoot =
            GeneratedRoot + "/Prefabs/NorwaySpruceHD";
        public const string EngelmannSprucePrefabRoot =
            GeneratedRoot + "/Prefabs/EngelmannSpruce";
        internal const string EngelmannAdjustedMeshRoot =
            GeneratedRoot + "/Meshes/EngelmannSpruce/CardAdjusted";
        internal const string NorwaySpruceWindShaderName =
            Phase1SpruceWindController.ShaderName;

        private const string SpruceSourceRoot =
            "Assets/Game/Presentation/Vegetation/ThirdParty/" +
            "SprucePack/Source";
        private const string SpruceModelRoot =
            SpruceSourceRoot + "/Models";
        private const string SpruceTextureRoot =
            SpruceSourceRoot + "/Textures";
        private const string SpruceMaterialRoot =
            GeneratedRoot + "/Materials/Spruce";
        private const string GeneratedTextureRoot =
            GeneratedRoot + "/Textures/Spruce";
        private const string NorwaySpruceSourceRoot =
            "Assets/Game/Presentation/Vegetation/ThirdParty/" +
            "NorwaySpruceHD/Source";
        private const string NorwaySpruceModelRoot =
            NorwaySpruceSourceRoot + "/Models";
        private const string NorwaySpruceTextureRoot =
            NorwaySpruceSourceRoot + "/Textures/StandardHealthy";
        private const string NorwaySpruceMaterialRoot =
            GeneratedRoot + "/Materials/NorwaySpruceHD";
        private const string NorwaySpruceGeneratedTextureRoot =
            GeneratedRoot + "/Textures/NorwaySpruceHD";
        private const string NorwaySpruceBillboardMaterialPath =
            NorwaySpruceMaterialRoot +
            "/NorwaySpruce_Billboard_HDRP.mat";
        private const string NorwaySpruceBillboardMeshPath =
            GeneratedRoot +
            "/Meshes/NorwaySpruceHD/NorwaySpruce_CrossBillboard.asset";
        private const string EngelmannSpruceSourceRoot =
            "Assets/Game/Presentation/Vegetation/ThirdParty/" +
            "EngelmannSpruce/Source";
        private const string EngelmannSpruceModelPath =
            EngelmannSpruceSourceRoot +
            "/Models/picea-engelmanni-glauca.fbx";
        private const string EngelmannSpruceTextureRoot =
            EngelmannSpruceSourceRoot + "/Textures";
        private const string EngelmannSpruceMaterialRoot =
            GeneratedRoot + "/Materials/EngelmannSpruce";
        private const string EngelmannSpruceGeneratedTextureRoot =
            GeneratedRoot + "/Textures/EngelmannSpruce";
        private const string EngelmannSpruceBillboardMeshPath =
            GeneratedRoot +
            "/Meshes/EngelmannSpruce/" +
            "EngelmannSpruce_TieredCrossBillboard.asset";

        private const string BarkMaterialPath =
            SpruceMaterialRoot + "/Spruce_Bark_HDRP.mat";
        private const string BranchMaterialPath =
            SpruceMaterialRoot + "/Spruce_Branch_HDRP.mat";
        private const string BillboardMaterialPath =
            SpruceMaterialRoot + "/Spruce_Billboard_HDRP.mat";
        private const string BarkMaskMapPath =
            GeneratedTextureRoot + "/Spruce_Bark_MaskMap.png";
        private const string BranchMaskMapPath =
            GeneratedTextureRoot + "/Spruce_Branch_MaskMap.png";
        private const string BranchBaseColorPath =
            GeneratedTextureRoot +
            "/Spruce_Branch_BaseColorOpacity.png";

        private static readonly string[] SpruceModelNames =
        {
            "Spruce_Forest_01",
            "Spruce_Forest_02",
            "Spruce_Hill_01",
            "Spruce_Hill_02",
            "Spruce_Med_01",
            "Spruce_Med_02",
            "Spruce_Small_01",
            "Spruce_Small_02",
            "Spruce_Dead_01",
            "Spruce_Dead_02"
        };

        private static readonly string[] NorwaySpruceVariantNames =
        {
            "Urban25",
            "RockClimber100",
            "Standard0",
            "Standard50",
            "RockClimber50"
        };

        internal static readonly string[] EngelmannSpruceVariantIds =
        {
            "1",
            "2",
            "5",
            "8",
            "9"
        };

        internal static float GetEngelmannRootBurialFraction(
            string variantId)
        {
            switch (variantId)
            {
                case "1":
                    return 0.32f;
                case "2":
                    return 0.20f;
                case "5":
                    return 0.19f;
                case "8":
                    return 0.15f;
                case "9":
                    return 0.14f;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(variantId),
                        variantId,
                        "Unknown Engelmann spruce grounding profile.");
            }
        }

        internal static float GetEngelmannFoliageCardScale(
            string variantId)
        {
            // Source variants 2 and 8 contain roughly one-metre foliage
            // cards after the common gameplay height normalization. The
            // earlier 0.62 pass still left 0.65-0.75 metre tufts visible at
            // close range. Variant 8 starts with slightly smaller cards and
            // needs more retained coverage than variant 2. Scaling every
            // disconnected quad around its own center limits both variants
            // to roughly 0.4-0.5 metre without moving branch locations.
            switch (variantId)
            {
                case "2":
                    return 0.36f;
                case "8":
                    return 0.44f;
                default:
                    return 1f;
            }
        }

        private static readonly NorwayFoliageProfile[] NorwayFoliageProfiles =
        {
            new NorwayFoliageProfile(
                "Foliage01",
                "Picea_abies_HD_Picea_fol_1_Picea_fol_01_LOD0_Color.png",
                "Picea_abies_HD_Picea_fol_1_Picea_fol_01_n_LOD0_Normal.png",
                "Picea_abies_HD_Picea_fol_1_Picea_fol_01_a_LOD0_Transparency.png"),
            new NorwayFoliageProfile(
                "Foliage02",
                "Picea_abies_HD_Picea_fol_2_Picea_fol_02_LOD0_Color.png",
                "Picea_abies_HD_Picea_fol_2_Picea_fol_02_n_LOD0_Normal.png",
                "Picea_abies_HD_Picea_fol_2_Picea_fol_02_a_LOD0_Transparency.png"),
            new NorwayFoliageProfile(
                "Foliage02Long",
                "Picea_abies_HD_Picea_fol_2_long_Picea_fol_02_long_LOD0_Color.png",
                "Picea_abies_HD_Picea_fol_2_long_Picea_fol_02_long_n_LOD0_Nor.png",
                "Picea_abies_HD_Picea_fol_2_long_Picea_fol_02_long_a_LOD0_Tra.png"),
            new NorwayFoliageProfile(
                "Foliage02Short",
                "Picea_abies_HD_Picea_fol_2_short_Picea_fol_02_short_LOD0_Col.png",
                "Picea_abies_HD_Picea_fol_2_short_Picea_fol_02_short_n_LOD0_N.png",
                "Picea_abies_HD_Picea_fol_2_short_Picea_fol_02_short_a_LOD0_T.png")
        };

        private static readonly NorwayFoliageProfile[]
            EngelmannFoliageProfiles =
        {
            CreateEngelmannFoliageProfile("01"),
            CreateEngelmannFoliageProfile("02"),
            CreateEngelmannFoliageProfile("03"),
            CreateEngelmannFoliageProfile("05"),
            CreateEngelmannFoliageProfile("06")
        };

        private static readonly Regex LodPattern = new Regex(
            @"(?:^|_)LOD(?<index>[0-9]+)(?:$|_)",
            RegexOptions.IgnoreCase |
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant);

        [MenuItem(
            "Tools/MSC Remake/World Baseline/" +
            "Build Phase 1 Vegetation Assets")]
        public static void BuildFromMenu()
        {
            Build();
        }

        [MenuItem(
            "Tools/MSC Remake/World Baseline/" +
            "Audit Norway Spruce Source Models")]
        public static void AuditNorwaySpruceSources()
        {
            foreach (string variant in NorwaySpruceVariantNames)
            {
                for (int lodIndex = 0; lodIndex <= 4; lodIndex++)
                {
                    string modelPath =
                        NorwaySpruceModelRoot + "/" + variant + "/" +
                        $"NorwaySpruce_{variant}_LOD{lodIndex}.fbx";
                    GameObject source =
                        RequireAsset<GameObject>(modelPath);
                    UnityEngine.Object[] assets =
                        AssetDatabase.LoadAllAssetsAtPath(modelPath);
                    string embeddedMaterials = string.Join(
                        ";",
                        assets
                            .OfType<Material>()
                            .Select(material => material.name)
                            .OrderBy(
                                name => name,
                                StringComparer.Ordinal));
                    Renderer[] renderers =
                        source.GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0;
                         rendererIndex < renderers.Length;
                         rendererIndex++)
                    {
                        Renderer renderer = renderers[rendererIndex];
                        Mesh mesh = renderer is SkinnedMeshRenderer skinned
                            ? skinned.sharedMesh
                            : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                        string slots = string.Join(
                            ";",
                            renderer.sharedMaterials.Select(
                                (material, slot) =>
                                    slot + ":" +
                                    (material != null
                                        ? material.name
                                        : "<null>")));
                        int triangleCount = mesh != null
                            ? (int)(mesh.GetIndexCount(0) / 3)
                            : 0;
                        if (mesh != null)
                        {
                            triangleCount = 0;
                            for (int subMesh = 0;
                                 subMesh < mesh.subMeshCount;
                                 subMesh++)
                            {
                                triangleCount +=
                                    (int)(mesh.GetIndexCount(subMesh) / 3);
                            }
                        }
                        Debug.Log(
                            "NORWAY_SPRUCE_SOURCE_AUDIT " +
                            $"variant={variant} lod={lodIndex} " +
                            $"renderer={rendererIndex}:{renderer.name} " +
                            $"vertices={(mesh != null ? mesh.vertexCount : 0)} " +
                            $"triangles={triangleCount} " +
                            $"submeshes={(mesh != null ? mesh.subMeshCount : 0)} " +
                            $"slots=[{slots}] " +
                            $"embedded=[{embeddedMaterials}]");
                    }
                }
            }
            Debug.Log("NORWAY_SPRUCE_SOURCE_AUDIT_OK");
        }

        [MenuItem(
            "Tools/MSC Remake/World Baseline/" +
            "Audit Engelmann Spruce Source Model")]
        public static void AuditEngelmannSpruceSource()
        {
            ConfigureSpruceModelImporter(EngelmannSpruceModelPath);
            GameObject source = RequireAsset<GameObject>(
                EngelmannSpruceModelPath);
            UnityEngine.Object[] assets =
                AssetDatabase.LoadAllAssetsAtPath(
                    EngelmannSpruceModelPath);
            string embeddedMaterials = string.Join(
                ";",
                assets
                    .OfType<Material>()
                    .Select(material => material.name)
                    .OrderBy(name => name, StringComparer.Ordinal));
            Renderer[] renderers =
                source.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                int triangleCount = 0;
                if (mesh != null)
                {
                    for (int subMesh = 0;
                         subMesh < mesh.subMeshCount;
                         subMesh++)
                    {
                        triangleCount +=
                            (int)(mesh.GetIndexCount(subMesh) / 3);
                    }
                }
                string slots = string.Join(
                    ";",
                    renderer.sharedMaterials.Select(
                        (material, slot) =>
                            slot + ":" +
                            (material != null
                                ? material.name
                                : "<null>")));
                Debug.Log(
                    "ENGELMANN_SPRUCE_SOURCE_AUDIT " +
                    $"renderer={GetRelativeTransformPath(source.transform, renderer.transform)} " +
                    $"vertices={(mesh != null ? mesh.vertexCount : 0)} " +
                    $"triangles={triangleCount} " +
                    $"submeshes={(mesh != null ? mesh.subMeshCount : 0)} " +
                    $"bounds={(mesh != null ? mesh.bounds.size.ToString("F3") : "<none>")} " +
                    $"slots=[{slots}] embedded=[{embeddedMaterials}]");
            }
            Debug.Log(
                "ENGELMANN_SPRUCE_SOURCE_AUDIT_OK " +
                $"renderers={renderers.Length}");
        }

        public static IReadOnlyList<GameObject> Build()
        {
            AssertSources();
            EnsureAssetFolder(SpruceMaterialRoot);
            EnsureAssetFolder(GeneratedTextureRoot);
            EnsureAssetFolder(SprucePrefabRoot);
            EnsureAssetFolder(NorwaySpruceMaterialRoot);
            EnsureAssetFolder(NorwaySpruceGeneratedTextureRoot);
            EnsureAssetFolder(NorwaySprucePrefabRoot);
            EnsureAssetFolder(EngelmannSpruceMaterialRoot);
            EnsureAssetFolder(EngelmannSpruceGeneratedTextureRoot);
            EnsureAssetFolder(EngelmannSprucePrefabRoot);
            EnsureAssetFolder(EngelmannAdjustedMeshRoot);

            ConfigureSourceTextureImporters();
            ConfigureSpruceModelImporters();

            CreateHdrpMaskMap(
                SpruceTextureRoot + "/Spruce_Bark_ARM.tga",
                BarkMaskMapPath);
            CreateHdrpMaskMap(
                SpruceTextureRoot + "/Spruce_Branch_ARM.tga",
                BranchMaskMapPath);
            CreateBaseColorWithOpacity(
                SpruceTextureRoot + "/Spruce_Branch_Albedo.tga",
                SpruceTextureRoot + "/Spruce_Branch_Opacity.png",
                BranchBaseColorPath);

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            Material bark = CreateOrUpdateBarkMaterial();
            Material branch = CreateOrUpdateBranchMaterial();
            Material billboard = CreateOrUpdateBillboardMaterial();

            var prefabs = new List<GameObject>(SpruceModelNames.Length);
            foreach (string modelName in SpruceModelNames)
            {
                prefabs.Add(
                    BuildSprucePrefab(
                        modelName,
                        bark,
                        branch,
                        billboard));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            ValidatePrefabs(prefabs);
            IReadOnlyList<GameObject> norwaySpruces =
                BuildNorwaySprucePrefabs();
            IReadOnlyList<GameObject> engelmannSpruces =
                BuildEngelmannSprucePrefabs();
            var allPrefabs = new List<GameObject>(
                prefabs.Count + norwaySpruces.Count +
                engelmannSpruces.Count);
            allPrefabs.AddRange(prefabs);
            allPrefabs.AddRange(norwaySpruces);
            allPrefabs.AddRange(engelmannSpruces);
            Debug.Log(
                "PHASE1_VEGETATION_ASSETS_BUILD_OK " +
                $"sprucePrefabs={prefabs.Count} " +
                $"norwaySprucePrefabs={norwaySpruces.Count} " +
                $"engelmannSprucePrefabs={engelmannSpruces.Count} " +
                "materials=15 generatedTextures=12 generatedMeshes=3");
            return allPrefabs;
        }

        private static void AssertSources()
        {
            foreach (string modelName in SpruceModelNames)
            {
                RequireAsset(
                    SpruceModelRoot + "/" + modelName + ".FBX");
            }

            string[] textures =
            {
                "Spruce_Bark_Albedo.TGA",
                "Spruce_Bark_ARM.tga",
                "Spruce_Bark_Normal.tga",
                "Spruce_Branch_Albedo.tga",
                "Spruce_Branch_ARM.tga",
                "Spruce_Branch_Normal.tga",
                "Spruce_Branch_Opacity.png",
                "Spruce_Billboard_Base_Color.tga",
                "Spruce_Billboard_Normal.tga"
            };
            foreach (string texture in textures)
            {
                RequireAsset(SpruceTextureRoot + "/" + texture);
            }

            foreach (string variant in NorwaySpruceVariantNames)
            {
                for (int lod = 0; lod <= 4; lod++)
                {
                    RequireAsset(
                        NorwaySpruceModelRoot + "/" + variant + "/" +
                        $"NorwaySpruce_{variant}_LOD{lod}.fbx");
                }
            }

            RequireAsset(
                NorwaySpruceTextureRoot + "/" +
                "Picea_abies_HD_Picea_branch_Picea_abies_bark_01_LOD0_Color.png");
            RequireAsset(
                NorwaySpruceTextureRoot + "/" +
                "Picea_abies_HD_Picea_branch_Picea_abies_branch_01_n_LOD0_Nor.png");
            foreach (NorwayFoliageProfile profile in NorwayFoliageProfiles)
            {
                RequireAsset(
                    NorwaySpruceTextureRoot + "/" +
                    profile.BaseColorFileName);
                RequireAsset(
                    NorwaySpruceTextureRoot + "/" +
                    profile.NormalFileName);
                RequireAsset(
                    NorwaySpruceTextureRoot + "/" +
                    profile.OpacityFileName);
            }

            RequireAsset(EngelmannSpruceModelPath);
            RequireAsset(
                EngelmannSpruceTextureRoot +
                "/CL08_Bark01_dif_su.png");
            RequireAsset(
                EngelmannSpruceTextureRoot +
                "/CL08_Bark01_nml_su.png");
            foreach (NorwayFoliageProfile profile in
                     EngelmannFoliageProfiles)
            {
                RequireAsset(
                    EngelmannSpruceTextureRoot + "/" +
                    profile.BaseColorFileName);
                RequireAsset(
                    EngelmannSpruceTextureRoot + "/" +
                    profile.NormalFileName);
                RequireAsset(
                    EngelmannSpruceTextureRoot + "/" +
                    profile.OpacityFileName);
            }
        }

        private static void ConfigureSourceTextureImporters()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:Texture2D",
                new[]
                {
                    SpruceTextureRoot,
                    NorwaySpruceTextureRoot,
                    EngelmannSpruceTextureRoot
                });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer =
                    AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                string lower = Path.GetFileNameWithoutExtension(path)
                    .ToLowerInvariant();
                bool isNormal =
                    lower.Contains("normal") ||
                    lower.Contains("_nml_");
                bool carriesCutoutAlpha =
                    lower.Contains("billboard") &&
                    !isNormal;
                bool isLinear =
                    lower.Contains("arm") ||
                    lower.Contains("roughness") ||
                    lower.Contains("opacity") ||
                    lower.Contains("_alp_") ||
                    lower.Contains("_gls_") ||
                    lower.Contains("_trp_") ||
                    lower.Contains("height") ||
                    lower.EndsWith("_ao", StringComparison.Ordinal);

                bool changed = false;
                changed |= SetIfDifferent(
                    importer.maxTextureSize,
                    2048,
                    value => importer.maxTextureSize = value);
                changed |= SetIfDifferent(
                    importer.textureCompression,
                    TextureImporterCompression.CompressedHQ,
                    value => importer.textureCompression = value);
                changed |= SetIfDifferent(
                    importer.mipmapEnabled,
                    true,
                    value => importer.mipmapEnabled = value);
                changed |= SetIfDifferent(
                    importer.sRGBTexture,
                    !isNormal && !isLinear,
                    value => importer.sRGBTexture = value);
                changed |= SetIfDifferent(
                    importer.textureType,
                    isNormal
                        ? TextureImporterType.NormalMap
                        : TextureImporterType.Default,
                    value => importer.textureType = value);
                if (carriesCutoutAlpha)
                {
                    changed |= SetIfDifferent(
                        importer.alphaSource,
                        TextureImporterAlphaSource.FromInput,
                        value => importer.alphaSource = value);
                    changed |= SetIfDifferent(
                        importer.alphaIsTransparency,
                        true,
                        value => importer.alphaIsTransparency = value);
                }
                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }
        }

        private static void ConfigureSpruceModelImporters()
        {
            foreach (string modelName in SpruceModelNames)
            {
                string path =
                    SpruceModelRoot + "/" + modelName + ".FBX";
                ModelImporter importer =
                    AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    throw new InvalidDataException(
                        "Spruce FBX has no ModelImporter: " + path);
                }

                bool changed = false;
                changed |= SetIfDifferent(
                    importer.importBlendShapes,
                    false,
                    value => importer.importBlendShapes = value);
                changed |= SetIfDifferent(
                    importer.importCameras,
                    false,
                    value => importer.importCameras = value);
                changed |= SetIfDifferent(
                    importer.importLights,
                    false,
                    value => importer.importLights = value);
                changed |= SetIfDifferent(
                    importer.isReadable,
                    false,
                    value => importer.isReadable = value);
                changed |= SetIfDifferent(
                    importer.meshCompression,
                    ModelImporterMeshCompression.Medium,
                    value => importer.meshCompression = value);
                changed |= SetIfDifferent(
                    importer.materialImportMode,
                    ModelImporterMaterialImportMode.ImportStandard,
                    value => importer.materialImportMode = value);
                if (changed)
                {
                    importer.SaveAndReimport();
                }
            }

            foreach (string variant in NorwaySpruceVariantNames)
            {
                for (int lod = 0; lod <= 4; lod++)
                {
                    string path =
                        NorwaySpruceModelRoot + "/" + variant + "/" +
                        $"NorwaySpruce_{variant}_LOD{lod}.fbx";
                    ConfigureSpruceModelImporter(path);
                }
            }
            ConfigureSpruceModelImporter(EngelmannSpruceModelPath);
        }

        private static void ConfigureSpruceModelImporter(
            string path,
            bool isReadable = false)
        {
            ModelImporter importer =
                AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidDataException(
                    "Spruce FBX has no ModelImporter: " + path);
            }

            bool changed = false;
            changed |= SetIfDifferent(
                importer.importBlendShapes,
                false,
                value => importer.importBlendShapes = value);
            changed |= SetIfDifferent(
                importer.importCameras,
                false,
                value => importer.importCameras = value);
            changed |= SetIfDifferent(
                importer.importLights,
                false,
                value => importer.importLights = value);
            changed |= SetIfDifferent(
                importer.isReadable,
                isReadable,
                value => importer.isReadable = value);
            changed |= SetIfDifferent(
                importer.meshCompression,
                ModelImporterMeshCompression.Medium,
                value => importer.meshCompression = value);
            changed |= SetIfDifferent(
                importer.materialImportMode,
                ModelImporterMaterialImportMode.ImportStandard,
                value => importer.materialImportMode = value);
            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void CreateHdrpMaskMap(
            string armPath,
            string destinationPath)
        {
            Texture2D arm = LoadReadableTexture(armPath);
            try
            {
                Color32[] source = arm.GetPixels32();
                var packed = new Color32[source.Length];
                for (int index = 0; index < source.Length; index++)
                {
                    Color32 pixel = source[index];
                    packed[index] = new Color32(
                        pixel.b,
                        pixel.r,
                        255,
                        (byte)(255 - pixel.g));
                }
                WriteGeneratedTexture(
                    destinationPath,
                    arm.width,
                    arm.height,
                    packed,
                    false,
                    false);
            }
            finally
            {
                SetReadable(armPath, false);
            }
        }

        private static void CreateBaseColorWithOpacity(
            string baseColorPath,
            string opacityPath,
            string destinationPath)
        {
            Texture2D baseColor = LoadReadableTexture(baseColorPath);
            Texture2D opacity = LoadReadableTexture(opacityPath);
            try
            {
                if (baseColor.width != opacity.width ||
                    baseColor.height != opacity.height)
                {
                    throw new InvalidDataException(
                        "Spruce branch base color and opacity dimensions " +
                        "do not match.");
                }

                Color32[] colorPixels = baseColor.GetPixels32();
                Color32[] opacityPixels = opacity.GetPixels32();
                for (int index = 0;
                     index < colorPixels.Length;
                     index++)
                {
                    Color32 color = colorPixels[index];
                    color.a = opacityPixels[index].r;
                    colorPixels[index] = color;
                }

                WriteGeneratedTexture(
                    destinationPath,
                    baseColor.width,
                    baseColor.height,
                    colorPixels,
                    true,
                    false);
            }
            finally
            {
                SetReadable(baseColorPath, false);
                SetReadable(opacityPath, false);
            }
        }

        private static Material CreateOrUpdateBarkMaterial()
        {
            Material material = LoadOrCreateMaterial(
                BarkMaterialPath,
                "Spruce Bark HDRP");
            ConfigureCommonMaterial(material, alphaClipped: false);
            material.SetTexture(
                "_BaseColorMap",
                RequireAsset<Texture2D>(
                    SpruceTextureRoot + "/Spruce_Bark_Albedo.TGA"));
            material.SetTexture(
                "_NormalMap",
                RequireAsset<Texture2D>(
                    SpruceTextureRoot + "/Spruce_Bark_Normal.tga"));
            material.SetTexture(
                "_MaskMap",
                RequireAsset<Texture2D>(BarkMaskMapPath));
            material.SetFloat("_NormalScale", 1f);
            material.SetFloat("_Smoothness", 0.32f);
            ValidateAndSave(material);
            return material;
        }

        private static Material CreateOrUpdateBranchMaterial()
        {
            Material material = LoadOrCreateMaterial(
                BranchMaterialPath,
                "Spruce Branch HDRP");
            ConfigureCommonMaterial(material, alphaClipped: true);
            material.SetTexture(
                "_BaseColorMap",
                RequireAsset<Texture2D>(BranchBaseColorPath));
            material.SetTexture(
                "_NormalMap",
                RequireAsset<Texture2D>(
                    SpruceTextureRoot + "/Spruce_Branch_Normal.tga"));
            material.SetTexture(
                "_MaskMap",
                RequireAsset<Texture2D>(BranchMaskMapPath));
            material.SetFloat("_NormalScale", 0.85f);
            material.SetFloat("_AlphaCutoff", 0.42f);
            ValidateAndSave(material);
            return material;
        }

        private static Material CreateOrUpdateBillboardMaterial()
        {
            Material material = LoadOrCreateMaterial(
                BillboardMaterialPath,
                "Spruce Billboard HDRP");
            ConfigureCommonMaterial(material, alphaClipped: true);
            material.SetTexture(
                "_BaseColorMap",
                RequireAsset<Texture2D>(
                    SpruceTextureRoot +
                    "/Spruce_Billboard_Base_Color.tga"));
            material.SetTexture(
                "_NormalMap",
                RequireAsset<Texture2D>(
                    SpruceTextureRoot +
                    "/Spruce_Billboard_Normal.tga"));
            material.SetFloat("_NormalScale", 0.65f);
            material.SetFloat("_AlphaCutoff", 0.38f);
            material.SetFloat("_Smoothness", 0.18f);
            ValidateAndSave(material);
            return material;
        }

        private static void ConfigureCommonMaterial(
            Material material,
            bool alphaClipped)
        {
            material.enableInstancing = true;
            SetFloatIfPresent(material, "_SurfaceType", 0f);
            SetFloatIfPresent(material, "_BlendMode", 0f);
            SetFloatIfPresent(
                material,
                "_AlphaCutoffEnable",
                alphaClipped ? 1f : 0f);
            SetFloatIfPresent(
                material,
                "_UseShadowThreshold",
                alphaClipped ? 1f : 0f);
            SetFloatIfPresent(
                material,
                "_DoubleSidedEnable",
                alphaClipped ? 1f : 0f);
            SetFloatIfPresent(
                material,
                "_DoubleSidedNormalMode",
                alphaClipped ? 1f : 2f);
            SetVectorIfPresent(
                material,
                "_DoubleSidedConstants",
                new Vector4(1f, 1f, -1f, 0f));
            SetFloatIfPresent(material, "_CullMode", alphaClipped ? 0f : 2f);
            SetFloatIfPresent(
                material,
                "_CullModeForward",
                alphaClipped ? 0f : 2f);
            material.renderQueue = alphaClipped
                ? (int)RenderQueue.AlphaTest
                : (int)RenderQueue.Geometry;
            if (alphaClipped)
            {
                material.EnableKeyword("_ALPHATEST_ON");
            }
            else
            {
                material.DisableKeyword("_ALPHATEST_ON");
            }
        }

        private static GameObject BuildSprucePrefab(
            string modelName,
            Material bark,
            Material branch,
            Material billboard)
        {
            string modelPath =
                SpruceModelRoot + "/" + modelName + ".FBX";
            GameObject source = RequireAsset<GameObject>(modelPath);
            GameObject instance =
                PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(source);
            }

            try
            {
                instance.name = modelName;
                Renderer[] renderers =
                    instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    throw new InvalidDataException(
                        "Spruce model has no renderers: " + modelPath);
                }

                foreach (Renderer renderer in renderers)
                {
                    Material[] sourceMaterials =
                        renderer.sharedMaterials;
                    var replacement =
                        new Material[sourceMaterials.Length];
                    for (int index = 0;
                         index < replacement.Length;
                         index++)
                    {
                        string slotName =
                            sourceMaterials[index] != null
                                ? sourceMaterials[index].name
                                : renderer.name;
                        replacement[index] = ResolveSpruceMaterial(
                            slotName,
                            renderer.name,
                            bark,
                            branch,
                            billboard);
                    }
                    renderer.sharedMaterials = replacement;
                    renderer.shadowCastingMode =
                        ShadowCastingMode.TwoSided;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage =
                        LightProbeUsage.BlendProbes;
                    renderer.reflectionProbeUsage =
                        ReflectionProbeUsage.BlendProbes;
                    renderer.motionVectorGenerationMode =
                        MotionVectorGenerationMode.Object;
                }

                ConfigureLodGroup(instance, renderers);
                AddTrunkCollider(instance);
                string prefabPath =
                    SprucePrefabRoot + "/" + modelName + ".prefab";
                return PrefabUtility.SaveAsPrefabAsset(
                    instance,
                    prefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static IReadOnlyList<GameObject>
            BuildNorwaySprucePrefabs()
        {
            string barkBaseColorPath =
                NorwaySpruceTextureRoot + "/" +
                "Picea_abies_HD_Picea_branch_Picea_abies_bark_01_LOD0_Color.png";
            string barkNormalPath =
                NorwaySpruceTextureRoot + "/" +
                "Picea_abies_HD_Picea_branch_Picea_abies_branch_01_n_LOD0_Nor.png";
            Material bark = CreateOrUpdateNorwaySpruceMaterial(
                NorwaySpruceMaterialRoot +
                "/NorwaySpruce_Bark_HDRP.mat",
                "Norway Spruce Bark HDRP",
                barkBaseColorPath,
                barkNormalPath,
                alphaClipped: false);
            Material billboard =
                CreateOrUpdateNorwaySpruceBillboardMaterial();
            Mesh billboardMesh =
                CreateOrUpdateNorwaySpruceBillboardMesh();

            var foliage =
                new Dictionary<string, Material>(
                    StringComparer.Ordinal);
            foreach (NorwayFoliageProfile profile in
                     NorwayFoliageProfiles)
            {
                string baseColorOpacityPath =
                    NorwaySpruceGeneratedTextureRoot + "/" +
                    profile.Id + "_BaseColorOpacity.png";
                CreateBaseColorWithOpacity(
                    NorwaySpruceTextureRoot + "/" +
                    profile.BaseColorFileName,
                    NorwaySpruceTextureRoot + "/" +
                    profile.OpacityFileName,
                    baseColorOpacityPath);
                foliage.Add(
                    profile.Id,
                    CreateOrUpdateNorwaySpruceMaterial(
                        NorwaySpruceMaterialRoot + "/" +
                        "NorwaySpruce_" + profile.Id +
                        "_HDRP.mat",
                        "Norway Spruce " + profile.Id + " HDRP",
                        baseColorOpacityPath,
                        NorwaySpruceTextureRoot + "/" +
                        profile.NormalFileName,
                        alphaClipped: true));
            }

            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            var result = new List<GameObject>(
                NorwaySpruceVariantNames.Length);
            foreach (string variant in NorwaySpruceVariantNames)
            {
                result.Add(
                    BuildNorwaySprucePrefab(
                        variant,
                        bark,
                        foliage,
                        billboard,
                        billboardMesh));
            }
            ValidateNorwaySprucePrefabs(result);
            return result;
        }

        private static IReadOnlyList<GameObject>
            BuildEngelmannSprucePrefabs()
        {
            ConfigureSpruceModelImporter(
                EngelmannSpruceModelPath,
                isReadable: true);
            try
            {
                return BuildReadableEngelmannSprucePrefabs();
            }
            finally
            {
                ConfigureSpruceModelImporter(
                    EngelmannSpruceModelPath,
                    isReadable: false);
            }
        }

        private static IReadOnlyList<GameObject>
            BuildReadableEngelmannSprucePrefabs()
        {
            Material bark = CreateOrUpdateNorwaySpruceMaterial(
                EngelmannSpruceMaterialRoot +
                "/EngelmannSpruce_Bark_HDRP.mat",
                "Engelmann Spruce Bark HDRP",
                EngelmannSpruceTextureRoot +
                "/CL08_Bark01_dif_su.png",
                EngelmannSpruceTextureRoot +
                "/CL08_Bark01_nml_su.png",
                alphaClipped: false);
            var foliage = new Dictionary<string, Material>(
                StringComparer.Ordinal);
            foreach (NorwayFoliageProfile profile in
                     EngelmannFoliageProfiles)
            {
                string baseColorOpacityPath =
                    EngelmannSpruceGeneratedTextureRoot + "/" +
                    profile.Id + "_BaseColorOpacity.png";
                CreateBaseColorWithOpacity(
                    EngelmannSpruceTextureRoot + "/" +
                    profile.BaseColorFileName,
                    EngelmannSpruceTextureRoot + "/" +
                    profile.OpacityFileName,
                    baseColorOpacityPath);
                Material material =
                    CreateOrUpdateNorwaySpruceMaterial(
                        EngelmannSpruceMaterialRoot + "/" +
                        "EngelmannSpruce_" + profile.Id +
                        "_HDRP.mat",
                        "Engelmann Spruce " + profile.Id +
                        " HDRP",
                        baseColorOpacityPath,
                        EngelmannSpruceTextureRoot + "/" +
                        profile.NormalFileName,
                        alphaClipped: true);
                material.SetColor(
                    "_BaseColor",
                    new Color(0.82f, 0.82f, 0.78f, 1f));
                ValidateAndSave(material);
                foliage.Add(profile.Id, material);
            }

            Mesh billboardMesh =
                CreateOrUpdateEngelmannSpruceBillboardMesh();
            GameObject source = RequireAsset<GameObject>(
                EngelmannSpruceModelPath);

            var result = new List<GameObject>(
                EngelmannSpruceVariantIds.Length);
            foreach (string variantId in EngelmannSpruceVariantIds)
            {
                result.Add(
                    BuildEngelmannSprucePrefab(
                        variantId,
                        source,
                        bark,
                        foliage,
                        billboardMesh));
            }
            ValidateEngelmannSprucePrefabs(result);
            return result;
        }

        private static GameObject BuildEngelmannSprucePrefab(
            string variantId,
            GameObject source,
            Material bark,
            IReadOnlyDictionary<string, Material> foliage,
            Mesh billboardMesh)
        {
            var root = new GameObject(
                "EngelmannSpruce_" + variantId);
            GameObject sourceInstance = null;
            try
            {
                sourceInstance =
                    PrefabUtility.InstantiatePrefab(source) as
                        GameObject;
                if (sourceInstance == null)
                {
                    sourceInstance =
                        UnityEngine.Object.Instantiate(source);
                }
                if (PrefabUtility.IsPartOfPrefabInstance(sourceInstance))
                {
                    PrefabUtility.UnpackPrefabInstance(
                        sourceInstance,
                        PrefabUnpackMode.Completely,
                        InteractionMode.AutomatedAction);
                }
                Transform variantTransform = sourceInstance
                    .GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(transform =>
                        transform.name.EndsWith(
                            "_" + variantId + ".001",
                            StringComparison.Ordinal));
                if (variantTransform == null)
                {
                    throw new InvalidDataException(
                        "Engelmann spruce source variant is missing: " +
                        variantId);
                }
                variantTransform.SetParent(root.transform, true);
                GameObject high = variantTransform.gameObject;
                high.name = "LOD0_Engelmann_" + variantId;
                UnityEngine.Object.DestroyImmediate(sourceInstance);
                sourceInstance = null;

                Renderer[] highRenderers = high
                    .GetComponentsInChildren<Renderer>(true);
                ConfigureEngelmannRenderers(
                    highRenderers,
                    bark,
                    foliage);
                ApplyEngelmannFoliageCardScale(
                    highRenderers,
                    variantId);
                NormalizeRendererBase(high, Vector3.zero);
                Bounds highBounds = CalculateBounds(high);
                var groundedVisuals = new GameObject(
                    "GroundedVisuals_Engelmann_" + variantId);
                groundedVisuals.transform.SetParent(
                    root.transform,
                    false);
                high.transform.SetParent(
                    groundedVisuals.transform,
                    true);

                GameObject low = UnityEngine.Object.Instantiate(
                    high,
                    groundedVisuals.transform,
                    true);
                low.name = "LOD1_EngelmannFoliage_" + variantId;
                Renderer[] lowRenderers =
                    KeepEngelmannFoliageRenderers(low);
                Renderer lowTrunk = CreateEngelmannLowTrunk(
                    groundedVisuals.transform,
                    bark,
                    highBounds);
                lowRenderers = lowRenderers
                    .Concat(new[] { lowTrunk })
                    .ToArray();

                Material billboardMaterial = lowRenderers
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .First(IsNorwaySpruceWindMaterial);

                GameObject billboardObject =
                    CreateEngelmannSpruceBillboardObject(
                        groundedVisuals.transform,
                        billboardMesh,
                        billboardMaterial,
                        highBounds);
                var group = root.AddComponent<LODGroup>();
                group.SetLODs(
                    new[]
                    {
                        new LOD(0.32f, highRenderers)
                        {
                            fadeTransitionWidth = 0.12f
                        },
                        new LOD(0.045f, lowRenderers)
                        {
                            fadeTransitionWidth = 0.2f
                        },
                        new LOD(
                            0.0025f,
                            new[]
                            {
                                billboardObject.GetComponent<
                                    MeshRenderer>()
                            })
                        {
                            fadeTransitionWidth = 0.18f
                        }
                    });
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = false;
                group.RecalculateBounds();
                AddTrunkCollider(root);
                groundedVisuals.transform.localPosition = Vector3.down *
                    (highBounds.size.y *
                     GetEngelmannRootBurialFraction(variantId));
                group.RecalculateBounds();
                string prefabPath =
                    EngelmannSprucePrefabRoot + "/" +
                    root.name + ".prefab";
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath);
                if (prefab == null)
                {
                    throw new IOException(
                        "Failed to save Engelmann spruce prefab: " +
                        prefabPath);
                }
                return prefab;
            }
            finally
            {
                if (sourceInstance != null)
                {
                    UnityEngine.Object.DestroyImmediate(sourceInstance);
                }
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Material CreateOrUpdateNorwaySpruceMaterial(
            string materialPath,
            string displayName,
            string baseColorPath,
            string normalPath,
            bool alphaClipped)
        {
            string shaderName = alphaClipped
                ? NorwaySpruceWindShaderName
                : "HDRP/Lit";
            Material material = LoadOrCreateMaterial(
                materialPath,
                displayName,
                shaderName);
            if (alphaClipped)
            {
                ConfigureNorwaySpruceWindMaterial(
                    material,
                    alphaCutoff: 0.43f);
            }
            else
            {
                ConfigureCommonMaterial(material, alphaClipped: false);
            }
            material.SetTexture(
                "_BaseColorMap",
                RequireAsset<Texture2D>(baseColorPath));
            material.SetTexture(
                "_NormalMap",
                RequireAsset<Texture2D>(normalPath));
            material.SetColor(
                "_BaseColor",
                alphaClipped
                    ? new Color(0.82f, 0.82f, 0.78f, 1f)
                    : new Color(0.88f, 0.86f, 0.82f, 1f));
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(
                material,
                "_Smoothness",
                alphaClipped ? 0.06f : 0.18f);
            SetFloatIfPresent(
                material,
                "_NormalScale",
                alphaClipped ? 0.72f : 0.85f);
            SetFloatIfPresent(
                material,
                "_AlphaCutoff",
                alphaClipped ? 0.43f : 0f);
            SetFloatIfPresent(material, "_ReceivesSSR", 0f);
            material.doubleSidedGI = alphaClipped;
            ValidateAndSave(material);
            return material;
        }

        private static GameObject BuildNorwaySprucePrefab(
            string variant,
            Material bark,
            IReadOnlyDictionary<string, Material> foliage,
            Material billboard,
            Mesh billboardMesh)
        {
            var root = new GameObject(
                "NorwaySpruce_" + variant);
            try
            {
                var lods = new List<LOD>(3);
                float[] transitions =
                {
                    0.18f,
                    0.055f
                };
                int[] sourceLodIndices =
                {
                    0,
                    4
                };
                for (int ordinal = 0;
                     ordinal < sourceLodIndices.Length;
                     ordinal++)
                {
                    int lodIndex = sourceLodIndices[ordinal];
                    string modelPath =
                        NorwaySpruceModelRoot + "/" + variant + "/" +
                        $"NorwaySpruce_{variant}_LOD{lodIndex}.fbx";
                    GameObject source =
                        RequireAsset<GameObject>(modelPath);
                    GameObject lodInstance =
                        PrefabUtility.InstantiatePrefab(source) as
                            GameObject;
                    if (lodInstance == null)
                    {
                        lodInstance =
                            UnityEngine.Object.Instantiate(source);
                    }
                    lodInstance.name = "LOD" + lodIndex;
                    lodInstance.transform.SetParent(
                        root.transform,
                        false);
                    lodInstance.transform.localPosition =
                        Vector3.zero;
                    lodInstance.transform.localRotation =
                        Quaternion.Euler(-90f, 0f, 0f);
                    lodInstance.transform.localScale =
                        Vector3.one;

                    Renderer[] renderers =
                        lodInstance.GetComponentsInChildren<
                            Renderer>(true);
                    if (renderers.Length == 0)
                    {
                        throw new InvalidDataException(
                            "Norway spruce LOD has no renderers: " +
                            modelPath);
                    }
                    foreach (Renderer renderer in renderers)
                    {
                        Material[] sourceMaterials =
                            renderer.sharedMaterials;
                        var replacement =
                            new Material[sourceMaterials.Length];
                        for (int materialIndex = 0;
                             materialIndex < replacement.Length;
                             materialIndex++)
                        {
                            string slotName =
                                sourceMaterials[materialIndex] != null
                                    ? sourceMaterials[materialIndex].name
                                    : renderer.name;
                            replacement[materialIndex] =
                                ResolveNorwaySpruceMaterial(
                                    slotName,
                                    renderer.name,
                                    bark,
                                    foliage);
                        }
                        renderer.sharedMaterials = replacement;
                        renderer.shadowCastingMode =
                            lodIndex <= 1
                                ? ShadowCastingMode.On
                                : ShadowCastingMode.Off;
                        renderer.receiveShadows = true;
                        renderer.lightProbeUsage =
                            LightProbeUsage.BlendProbes;
                        renderer.reflectionProbeUsage =
                            ReflectionProbeUsage.BlendProbes;
                        renderer.motionVectorGenerationMode =
                            replacement.Any(IsNorwaySpruceWindMaterial)
                                ? MotionVectorGenerationMode.Object
                                : MotionVectorGenerationMode.ForceNoMotion;
                    }

                    lods.Add(
                        new LOD(
                            transitions[ordinal],
                            renderers)
                        {
                            fadeTransitionWidth = 0.22f
                        });
                }

                Bounds sourceBounds = CalculateBounds(root);
                GameObject billboardObject =
                    CreateNorwaySpruceBillboardObject(
                        root.transform,
                        billboardMesh,
                        billboard,
                        sourceBounds);
                lods.Add(
                    new LOD(
                        0.0025f,
                        new[]
                        {
                            billboardObject.GetComponent<MeshRenderer>()
                        })
                    {
                        fadeTransitionWidth = 0.18f
                    });

                LODGroup group = root.AddComponent<LODGroup>();
                group.SetLODs(lods.ToArray());
                group.fadeMode = LODFadeMode.CrossFade;
                group.animateCrossFading = false;
                group.RecalculateBounds();
                AddTrunkCollider(root);
                string prefabPath =
                    NorwaySprucePrefabRoot + "/" +
                    root.name + ".prefab";
                GameObject prefab =
                    PrefabUtility.SaveAsPrefabAsset(
                        root,
                        prefabPath);
                if (prefab == null)
                {
                    throw new IOException(
                        "Failed to save Norway spruce prefab: " +
                        prefabPath);
                }
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static Material
            CreateOrUpdateNorwaySpruceBillboardMaterial()
        {
            Material material = LoadOrCreateMaterial(
                NorwaySpruceBillboardMaterialPath,
                "Norway Spruce Billboard HDRP",
                NorwaySpruceWindShaderName);
            ConfigureNorwaySpruceWindMaterial(
                material,
                alphaCutoff: 0.34f);
            material.SetTexture(
                "_BaseColorMap",
                RequireAsset<Texture2D>(
                    SpruceTextureRoot +
                    "/Spruce_Billboard_Base_Color.tga"));
            material.SetTexture(
                "_NormalMap",
                RequireAsset<Texture2D>(
                    SpruceTextureRoot +
                    "/Spruce_Billboard_Normal.tga"));
            material.SetColor(
                "_BaseColor",
                new Color(0.88f, 0.88f, 0.84f, 1f));
            SetFloatIfPresent(material, "_Metallic", 0f);
            SetFloatIfPresent(material, "_Smoothness", 0.08f);
            SetFloatIfPresent(material, "_NormalScale", 0.42f);
            SetFloatIfPresent(material, "_AlphaCutoff", 0.34f);
            SetFloatIfPresent(material, "_ReceivesSSR", 0f);
            material.doubleSidedGI = true;
            ValidateAndSave(material);
            return material;
        }

        private static Mesh CreateOrUpdateNorwaySpruceBillboardMesh()
        {
            EnsureAssetFolder(NorwaySpruceBillboardMeshPath);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                NorwaySpruceBillboardMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh
                {
                    name = "Norway Spruce Cross Billboard"
                };
                AssetDatabase.CreateAsset(
                    mesh,
                    NorwaySpruceBillboardMeshPath);
            }
            else
            {
                mesh.Clear();
            }

            const int planeCount = 3;
            const float halfWidth = 2.7f;
            const float height = 10f;
            const float uMin = 0.332f;
            const float uMax = 0.655f;
            const float vMin = 0.418f;
            const float vMax = 0.985f;
            var vertices = new Vector3[planeCount * 4];
            var normals = new Vector3[planeCount * 4];
            var tangents = new Vector4[planeCount * 4];
            var uv = new Vector2[planeCount * 4];
            var triangles = new int[planeCount * 6];
            for (int plane = 0; plane < planeCount; plane++)
            {
                Quaternion rotation = Quaternion.Euler(
                    0f,
                    plane * (180f / planeCount),
                    0f);
                Vector3 right = rotation * Vector3.right;
                Vector3 normal = rotation * Vector3.forward;
                int vertex = plane * 4;
                vertices[vertex] = -right * halfWidth;
                vertices[vertex + 1] = right * halfWidth;
                vertices[vertex + 2] =
                    right * halfWidth + Vector3.up * height;
                vertices[vertex + 3] =
                    -right * halfWidth + Vector3.up * height;
                for (int offset = 0; offset < 4; offset++)
                {
                    normals[vertex + offset] = normal;
                    tangents[vertex + offset] =
                        new Vector4(right.x, right.y, right.z, 1f);
                }
                uv[vertex] = new Vector2(uMin, vMin);
                uv[vertex + 1] = new Vector2(uMax, vMin);
                uv[vertex + 2] = new Vector2(uMax, vMax);
                uv[vertex + 3] = new Vector2(uMin, vMax);

                int triangle = plane * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 2;
            }

            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Mesh
            CreateOrUpdateEngelmannSpruceBillboardMesh()
        {
            EnsureAssetFolder(EngelmannSpruceBillboardMeshPath);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                EngelmannSpruceBillboardMeshPath);
            if (mesh == null)
            {
                mesh = new Mesh
                {
                    name = "Engelmann Spruce Tiered Cross Billboard"
                };
                AssetDatabase.CreateAsset(
                    mesh,
                    EngelmannSpruceBillboardMeshPath);
            }
            else
            {
                mesh.Clear();
            }

            const int tierCount = 7;
            const int planeCount = 3;
            const float height = 10f;
            const float halfBaseWidth = 3.6f;
            float step = height / tierCount;
            var vertices = new List<Vector3>(
                tierCount * planeCount * 4);
            var normals = new List<Vector3>(
                tierCount * planeCount * 4);
            var tangents = new List<Vector4>(
                tierCount * planeCount * 4);
            var uv = new List<Vector2>(
                tierCount * planeCount * 4);
            var triangles = new List<int>(
                tierCount * planeCount * 6);

            for (int tier = 0; tier < tierCount; tier++)
            {
                float bottom = Mathf.Max(
                    0f,
                    tier * step - step * 0.32f);
                float top = Mathf.Min(
                    height,
                    (tier + 1) * step + step * 0.32f);
                float bottomT = bottom / height;
                float topT = top / height;
                float bottomWidth = Mathf.Lerp(
                    halfBaseWidth,
                    0.22f,
                    bottomT);
                float topWidth = Mathf.Lerp(
                    halfBaseWidth * 0.68f,
                    0.08f,
                    topT);

                for (int plane = 0; plane < planeCount; plane++)
                {
                    Quaternion rotation = Quaternion.Euler(
                        0f,
                        plane * (180f / planeCount),
                        0f);
                    Vector3 right = rotation * Vector3.right;
                    Vector3 normal = rotation * Vector3.forward;
                    int vertex = vertices.Count;
                    vertices.Add(-right * bottomWidth +
                                 Vector3.up * bottom);
                    vertices.Add(right * bottomWidth +
                                 Vector3.up * bottom);
                    vertices.Add(right * topWidth +
                                 Vector3.up * top);
                    vertices.Add(-right * topWidth +
                                 Vector3.up * top);
                    for (int offset = 0; offset < 4; offset++)
                    {
                        normals.Add(normal);
                        tangents.Add(
                            new Vector4(
                                right.x,
                                right.y,
                                right.z,
                                1f));
                    }
                    uv.Add(new Vector2(0f, 0f));
                    uv.Add(new Vector2(1f, 0f));
                    uv.Add(new Vector2(1f, 1f));
                    uv.Add(new Vector2(0f, 1f));
                    // Match the authored +forward normal and tangent basis. HDRP
                    // flips normals for back faces, so reversed winding lights
                    // both sides of this billboard from the wrong hemisphere.
                    triangles.Add(vertex);
                    triangles.Add(vertex + 1);
                    triangles.Add(vertex + 2);
                    triangles.Add(vertex);
                    triangles.Add(vertex + 2);
                    triangles.Add(vertex + 3);
                }
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTangents(tangents);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Renderer[] KeepEngelmannFoliageRenderers(
            GameObject target)
        {
            Renderer[] renderers = target
                .GetComponentsInChildren<Renderer>(true);
            var foliageRenderers = new List<Renderer>(renderers.Length);
            foreach (Renderer renderer in renderers)
            {
                bool hasWindMaterial = renderer.sharedMaterials
                    .Any(IsNorwaySpruceWindMaterial);
                bool hasStationaryMaterial = renderer.sharedMaterials
                    .Any(material =>
                        material != null &&
                        !IsNorwaySpruceWindMaterial(material));
                if (hasWindMaterial && hasStationaryMaterial)
                {
                    throw new InvalidDataException(
                        "Engelmann low LOD cannot separate mixed bark " +
                        "and foliage submeshes: " + renderer.name);
                }
                if (hasWindMaterial)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                    renderer.motionVectorGenerationMode =
                        MotionVectorGenerationMode.Object;
                    foliageRenderers.Add(renderer);
                    continue;
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                UnityEngine.Object.DestroyImmediate(renderer);
                if (filter != null)
                {
                    UnityEngine.Object.DestroyImmediate(filter);
                }
            }
            if (foliageRenderers.Count == 0)
            {
                throw new InvalidDataException(
                    "Engelmann low LOD has no retained source foliage.");
            }
            return foliageRenderers.ToArray();
        }

        private static void ApplyEngelmannFoliageCardScale(
            IReadOnlyList<Renderer> renderers,
            string variantId)
        {
            float scale = GetEngelmannFoliageCardScale(variantId);
            if (scale >= 0.999f)
            {
                return;
            }

            int adjustedRendererCount = 0;
            for (int rendererIndex = 0;
                 rendererIndex < renderers.Count;
                 rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                bool hasWindMaterial = renderer.sharedMaterials
                    .Any(IsNorwaySpruceWindMaterial);
                if (!hasWindMaterial)
                {
                    continue;
                }
                if (renderer.sharedMaterials.Any(material =>
                        material != null &&
                        !IsNorwaySpruceWindMaterial(material)))
                {
                    throw new InvalidDataException(
                        "Oversized Engelmann foliage correction cannot " +
                        "modify a mixed bark/foliage renderer: " +
                        renderer.name);
                }

                Mesh sourceMesh = renderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (sourceMesh == null)
                {
                    throw new InvalidDataException(
                        "Engelmann foliage renderer has no mesh: " +
                        renderer.name);
                }

                string safeName = Regex.Replace(
                    sourceMesh.name,
                    @"[^A-Za-z0-9_-]+",
                    "_");
                string scaleTag = Mathf.RoundToInt(scale * 100f)
                    .ToString("D3");
                string assetPath = EngelmannAdjustedMeshRoot + "/" +
                    "EngelmannSpruce_" + variantId + "_" +
                    rendererIndex.ToString("D2") + "_" + safeName +
                    "_CardScale" + scaleTag + ".asset";
                Mesh adjustedMesh =
                    CreateOrUpdateCardAdjustedMesh(
                        sourceMesh,
                        assetPath,
                        scale);
                if (renderer is SkinnedMeshRenderer adjustedSkinned)
                {
                    adjustedSkinned.sharedMesh = adjustedMesh;
                }
                else
                {
                    renderer.GetComponent<MeshFilter>().sharedMesh =
                        adjustedMesh;
                }
                adjustedRendererCount++;
            }

            if (adjustedRendererCount == 0)
            {
                throw new InvalidDataException(
                    "No Engelmann foliage renderer was adjusted for " +
                    "variant " + variantId + ".");
            }
            Debug.Log(
                "ENGELMANN_FOLIAGE_CARD_SCALE_APPLIED " +
                $"variant={variantId} scale={scale:F2} " +
                $"renderers={adjustedRendererCount}");
        }

        private static Mesh CreateOrUpdateCardAdjustedMesh(
            Mesh sourceMesh,
            string assetPath,
            float scale)
        {
            if (!sourceMesh.isReadable)
            {
                throw new InvalidDataException(
                    "Engelmann source mesh must be readable while " +
                    "building adjusted foliage: " + sourceMesh.name);
            }

            Mesh generated = UnityEngine.Object.Instantiate(sourceMesh);
            generated.name = Path.GetFileNameWithoutExtension(assetPath);
            Vector3[] vertices = generated.vertices;
            int[] triangles = generated.triangles;
            if (vertices.Length == 0 || triangles.Length == 0)
            {
                UnityEngine.Object.DestroyImmediate(generated);
                throw new InvalidDataException(
                    "Engelmann foliage mesh is empty: " +
                    sourceMesh.name);
            }

            int[] parents = new int[vertices.Length];
            int[] componentSizes = new int[vertices.Length];
            for (int index = 0; index < parents.Length; index++)
            {
                parents[index] = index;
                componentSizes[index] = 1;
            }
            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                UnionComponents(
                    triangles[index],
                    triangles[index + 1],
                    parents,
                    componentSizes);
                UnionComponents(
                    triangles[index],
                    triangles[index + 2],
                    parents,
                    componentSizes);
            }

            var centers = new Dictionary<int, Vector3>();
            var counts = new Dictionary<int, int>();
            for (int index = 0; index < vertices.Length; index++)
            {
                int root = FindComponent(index, parents);
                centers[root] = centers.TryGetValue(
                    root,
                    out Vector3 sum)
                    ? sum + vertices[index]
                    : vertices[index];
                counts[root] = counts.TryGetValue(
                    root,
                    out int count)
                    ? count + 1
                    : 1;
            }
            int maximumComponentSize = counts.Values.Max();
            if (maximumComponentSize > 8)
            {
                UnityEngine.Object.DestroyImmediate(generated);
                throw new InvalidDataException(
                    "Engelmann foliage topology is no longer isolated " +
                    "card quads; refusing to shrink an entire crown. " +
                    $"Largest component={maximumComponentSize}.");
            }
            foreach (int root in centers.Keys.ToArray())
            {
                centers[root] /= counts[root];
            }
            for (int index = 0; index < vertices.Length; index++)
            {
                int root = FindComponent(index, parents);
                Vector3 center = centers[root];
                vertices[index] = center +
                    (vertices[index] - center) * scale;
            }
            generated.vertices = vertices;
            generated.RecalculateBounds();

            EnsureAssetFolder(assetPath);
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, assetPath);
                generated.UploadMeshData(true);
                EditorUtility.SetDirty(generated);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            existing.name = generated.name;
            existing.UploadMeshData(true);
            EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(generated);
            return existing;
        }

        private static int FindComponent(
            int vertex,
            int[] parents)
        {
            int current = vertex;
            while (parents[current] != current)
            {
                parents[current] = parents[parents[current]];
                current = parents[current];
            }
            return current;
        }

        private static void UnionComponents(
            int left,
            int right,
            int[] parents,
            int[] componentSizes)
        {
            int leftRoot = FindComponent(left, parents);
            int rightRoot = FindComponent(right, parents);
            if (leftRoot == rightRoot)
            {
                return;
            }
            if (componentSizes[leftRoot] < componentSizes[rightRoot])
            {
                int temporary = leftRoot;
                leftRoot = rightRoot;
                rightRoot = temporary;
            }
            parents[rightRoot] = leftRoot;
            componentSizes[leftRoot] += componentSizes[rightRoot];
        }

        private static Renderer CreateEngelmannLowTrunk(
            Transform parent,
            Material bark,
            Bounds sourceBounds)
        {
            GameObject trunk = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            trunk.name = "LOD1_EngelmannLightTrunk";
            trunk.transform.SetParent(parent, false);
            float height = Mathf.Max(sourceBounds.size.y * 0.68f, 0.01f);
            float radius = Mathf.Max(sourceBounds.size.y * 0.022f, 0.01f);
            trunk.transform.localPosition = new Vector3(
                sourceBounds.center.x,
                sourceBounds.min.y + height * 0.5f,
                sourceBounds.center.z);
            trunk.transform.localRotation = Quaternion.identity;
            trunk.transform.localScale = new Vector3(
                radius * 2f,
                height * 0.5f,
                radius * 2f);
            Collider collider = trunk.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
            MeshRenderer renderer = trunk.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = bark;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage =
                ReflectionProbeUsage.BlendProbes;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            return renderer;
        }

        private static GameObject CreateEngelmannSpruceBillboardObject(
            Transform parent,
            Mesh billboardMesh,
            Material billboardMaterial,
            Bounds sourceBounds)
        {
            var billboardObject = new GameObject(
                "LOD2_EngelmannTieredBillboard");
            billboardObject.transform.SetParent(parent, false);
            const float authoredHeight = 10f;
            const float authoredWidth = 7.2f;
            billboardObject.transform.localPosition = new Vector3(
                sourceBounds.center.x,
                sourceBounds.min.y,
                sourceBounds.center.z);
            billboardObject.transform.localScale = new Vector3(
                Mathf.Max(sourceBounds.size.x / authoredWidth, 0.01f),
                Mathf.Max(sourceBounds.size.y / authoredHeight, 0.01f),
                Mathf.Max(sourceBounds.size.z / authoredWidth, 0.01f));
            var filter = billboardObject.AddComponent<MeshFilter>();
            filter.sharedMesh = billboardMesh;
            var renderer = billboardObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = billboardMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage =
                ReflectionProbeUsage.BlendProbes;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.Object;
            return billboardObject;
        }

        private static GameObject CreateNorwaySpruceBillboardObject(
            Transform parent,
            Mesh billboardMesh,
            Material billboardMaterial,
            Bounds sourceBounds)
        {
            var billboardObject = new GameObject("LOD_Billboard");
            billboardObject.transform.SetParent(parent, false);
            const float authoredBillboardHeight = 10f;
            float uniformScale = Mathf.Max(
                sourceBounds.size.y / authoredBillboardHeight,
                0.01f);
            billboardObject.transform.localPosition = new Vector3(
                sourceBounds.center.x,
                sourceBounds.min.y,
                sourceBounds.center.z);
            billboardObject.transform.localScale =
                Vector3.one * uniformScale;
            var filter = billboardObject.AddComponent<MeshFilter>();
            filter.sharedMesh = billboardMesh;
            var renderer =
                billboardObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = billboardMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage =
                ReflectionProbeUsage.BlendProbes;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.Object;
            return billboardObject;
        }

        private static void ConfigureNorwaySpruceWindMaterial(
            Material material,
            float alphaCutoff)
        {
            material.enableInstancing = true;
            material.renderQueue = (int)RenderQueue.AlphaTest;
            SetFloatIfPresent(material, "_AlphaCutoffEnable", 1f);
            SetFloatIfPresent(material, "_AlphaCutoff", alphaCutoff);
            SetFloatIfPresent(material, "_CullMode", 0f);
            SetFloatIfPresent(material, "_CullModeForward", 0f);
            SetFloatIfPresent(material, "_DoubleSidedEnable", 1f);
            if (material.HasProperty("_DoubleSidedConstants"))
            {
                material.SetVector(
                    "_DoubleSidedConstants",
                    new Vector4(-1f, -1f, -1f, 0f));
            }
            SetColorIfPresent(material, "_EmissiveColor", Color.black);
            SetColorIfPresent(material, "_EmissionColor", Color.black);
            // Animated alpha-cutout geometry must tolerate the depth and
            // forward passes sampling adjacent wind time values. Materials
            // migrated from HDRP/Lit can retain CompareFunction.Equal (3),
            // which leaves a black depth silhouette when the color pass is
            // rejected. CompareFunction.LessEqual (4) keeps the prepass while
            // allowing the wind-deformed foliage color to render.
            SetFloatIfPresent(
                material,
                "_ZTestDepthEqualForOpaque",
                (float)CompareFunction.LessEqual);
            SetFloatIfPresent(material, "_WindStrengthMeters", 0.36f);
            SetFloatIfPresent(material, "_WindSpeed", 1.0f);
            SetFloatIfPresent(material, "_WindFrequency", 0.026f);
            SetFloatIfPresent(material, "_WindBaseHeightMeters", 0.7f);
            SetFloatIfPresent(material, "_WindHeightMeters", 13f);
            SetFloatIfPresent(material, "_MinimumWindIntensity", 0.035f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_DOUBLESIDED_ON");
            material.doubleSidedGI = true;
        }

        private static bool IsNorwaySpruceWindMaterial(
            Material material)
        {
            return material != null &&
                   material.shader != null &&
                   string.Equals(
                       material.shader.name,
                       NorwaySpruceWindShaderName,
                       StringComparison.Ordinal);
        }

        private static Material ResolveNorwaySpruceMaterial(
            string slotName,
            string rendererName,
            Material bark,
            IReadOnlyDictionary<string, Material> foliage)
        {
            string identifier =
                Regex.Replace(
                    (slotName + " " + rendererName)
                    .ToLowerInvariant(),
                    @"[\s_\-.]+",
                    " ");
            if (identifier.Contains("fol 2 long"))
            {
                return foliage["Foliage02Long"];
            }
            if (identifier.Contains("fol 2 short"))
            {
                return foliage["Foliage02Short"];
            }
            if (identifier.Contains("fol 2"))
            {
                return foliage["Foliage02"];
            }
            if (identifier.Contains("fol 1") ||
                identifier.Contains("foliage") ||
                identifier.Contains("leaf") ||
                identifier.Contains("needle"))
            {
                return foliage["Foliage01"];
            }
            return bark;
        }

        private static NorwayFoliageProfile
            CreateEngelmannFoliageProfile(string id)
        {
            return new NorwayFoliageProfile(
                "Leaf" + id,
                "CL08_Leaf" + id + "Front_dif_su.png",
                "CL08_Leaf" + id + "_nml_su.png",
                "CL08_Leaf" + id + "_alp_su.png");
        }

        private static void ConfigureEngelmannRenderers(
            IReadOnlyList<Renderer> renderers,
            Material bark,
            IReadOnlyDictionary<string, Material> foliage)
        {
            if (renderers.Count == 0)
            {
                throw new InvalidDataException(
                    "Engelmann spruce variant has no renderers.");
            }
            foreach (Renderer renderer in renderers)
            {
                Material[] sourceMaterials =
                    renderer.sharedMaterials;
                var replacement =
                    new Material[sourceMaterials.Length];
                for (int index = 0;
                     index < replacement.Length;
                     index++)
                {
                    string slotName = sourceMaterials[index] != null
                        ? sourceMaterials[index].name
                        : renderer.name;
                    string identifier =
                        (slotName + " " + renderer.name)
                        .ToLowerInvariant();
                    if (identifier.Contains("bark"))
                    {
                        replacement[index] = bark;
                        continue;
                    }
                    KeyValuePair<string, Material> leaf =
                        foliage.FirstOrDefault(pair =>
                            identifier.Contains(
                                pair.Key.ToLowerInvariant()));
                    if (leaf.Value == null)
                    {
                        throw new InvalidDataException(
                            "Unknown Engelmann spruce material slot: " +
                            slotName + " on " + renderer.name);
                    }
                    replacement[index] = leaf.Value;
                }
                renderer.sharedMaterials = replacement;
                renderer.shadowCastingMode =
                    ShadowCastingMode.TwoSided;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage =
                    LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage =
                    ReflectionProbeUsage.BlendProbes;
                renderer.motionVectorGenerationMode =
                    replacement.Any(IsNorwaySpruceWindMaterial)
                        ? MotionVectorGenerationMode.Object
                        : MotionVectorGenerationMode.ForceNoMotion;
            }
        }

        private static void ConfigureNorwaySpruceRenderers(
            IReadOnlyList<Renderer> renderers,
            Material bark,
            IReadOnlyDictionary<string, Material> foliage,
            bool castShadows)
        {
            foreach (Renderer renderer in renderers)
            {
                Material[] sourceMaterials =
                    renderer.sharedMaterials;
                var replacement =
                    new Material[sourceMaterials.Length];
                for (int index = 0;
                     index < replacement.Length;
                     index++)
                {
                    string slotName = sourceMaterials[index] != null
                        ? sourceMaterials[index].name
                        : renderer.name;
                    replacement[index] =
                        ResolveNorwaySpruceMaterial(
                            slotName,
                            renderer.name,
                            bark,
                            foliage);
                }
                renderer.sharedMaterials = replacement;
                renderer.shadowCastingMode = castShadows
                    ? ShadowCastingMode.On
                    : ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage =
                    LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage =
                    ReflectionProbeUsage.BlendProbes;
                renderer.motionVectorGenerationMode =
                    replacement.Any(IsNorwaySpruceWindMaterial)
                        ? MotionVectorGenerationMode.Object
                        : MotionVectorGenerationMode.ForceNoMotion;
            }
        }

        private static void NormalizeRendererBase(
            GameObject target,
            Vector3 desiredBase)
        {
            Bounds bounds = CalculateBounds(target);
            Vector3 currentBase = new Vector3(
                bounds.center.x,
                bounds.min.y,
                bounds.center.z);
            target.transform.position += desiredBase - currentBase;
        }

        private static int CountRendererTriangles(
            IEnumerable<Renderer> renderers)
        {
            int triangleCount = 0;
            foreach (Renderer renderer in renderers)
            {
                Mesh mesh = renderer is SkinnedMeshRenderer skinned
                    ? skinned.sharedMesh
                    : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }
                for (int subMesh = 0;
                     subMesh < mesh.subMeshCount;
                     subMesh++)
                {
                    triangleCount +=
                        (int)(mesh.GetIndexCount(subMesh) / 3);
                }
            }
            return triangleCount;
        }

        private static void ValidateEngelmannSprucePrefabs(
            IReadOnlyList<GameObject> prefabs)
        {
            if (prefabs.Count !=
                EngelmannSpruceVariantIds.Length ||
                prefabs.Any(prefab => prefab == null))
            {
                throw new InvalidDataException(
                    "Engelmann spruce prefab generation is incomplete.");
            }
            foreach (GameObject prefab in prefabs)
            {
                string variantId = prefab.name.Substring(
                    prefab.name.LastIndexOf('_') + 1);
                if (prefab.name.EndsWith(
                        "_3",
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "The rejected high-cost Engelmann variant 3 " +
                        "must not enter the runtime prefab set.");
                }
                LODGroup group = prefab.GetComponent<LODGroup>();
                if (group == null || group.lodCount != 3)
                {
                    throw new InvalidDataException(
                        "Engelmann spruce prefab has invalid LOD " +
                        "coverage: " + prefab.name);
                }
                if (prefab.GetComponent<CapsuleCollider>() == null)
                {
                    throw new InvalidDataException(
                        "Engelmann spruce prefab has no trunk collider: " +
                        prefab.name);
                }
                LOD[] lods = group.GetLODs();
                int highTriangles =
                    CountRendererTriangles(lods[0].renderers);
                int lowTriangles =
                    CountRendererTriangles(lods[1].renderers);
                if (highTriangles < 50000 ||
                    highTriangles > 320000 ||
                    lowTriangles > 30000)
                {
                    throw new InvalidDataException(
                        "Engelmann spruce LOD triangle budget is " +
                        $"invalid: {prefab.name}, high=" +
                        $"{highTriangles}, low={lowTriangles}");
                }
                if (!lods[0].renderers
                        .SelectMany(renderer =>
                            renderer.sharedMaterials)
                        .Any(IsNorwaySpruceWindMaterial))
                {
                    throw new InvalidDataException(
                        "Engelmann spruce has no wind foliage: " +
                        prefab.name);
                }
                for (int lodIndex = 1;
                     lodIndex < lods.Length;
                     lodIndex++)
                {
                    string[] materialPaths = lods[lodIndex].renderers
                        .Where(renderer => renderer != null)
                        .SelectMany(renderer => renderer.sharedMaterials)
                        .Where(material => material != null)
                        .Select(AssetDatabase.GetAssetPath)
                        .Distinct()
                        .ToArray();
                    if (materialPaths.Length == 0 ||
                        materialPaths.Any(path =>
                            !path.StartsWith(
                                EngelmannSpruceMaterialRoot + "/",
                                StringComparison.Ordinal)))
                    {
                        throw new InvalidDataException(
                            "Engelmann spruce still references a legacy " +
                            $"far-LOD material: {prefab.name}, " +
                            $"lod={lodIndex}, materials=[" +
                            string.Join(",", materialPaths) + "]");
                    }
                    string[] meshPaths = lods[lodIndex].renderers
                        .Where(renderer => renderer != null)
                        .Select(renderer =>
                            renderer is SkinnedMeshRenderer skinned
                                ? skinned.sharedMesh
                                : renderer.GetComponent<MeshFilter>()
                                    ?.sharedMesh)
                        .Where(mesh => mesh != null)
                        .Select(AssetDatabase.GetAssetPath)
                        .Distinct()
                        .ToArray();
                    bool hasRequiredMesh;
                    if (lodIndex == 1 &&
                        GetEngelmannFoliageCardScale(variantId) < 1f)
                    {
                        hasRequiredMesh = meshPaths.Any(path =>
                            path.StartsWith(
                                EngelmannAdjustedMeshRoot + "/",
                                StringComparison.Ordinal));
                    }
                    else
                    {
                        string requiredMeshPath = lodIndex == 1
                            ? EngelmannSpruceModelPath
                            : EngelmannSpruceBillboardMeshPath;
                        hasRequiredMesh =
                            meshPaths.Contains(requiredMeshPath);
                    }
                    if (!hasRequiredMesh ||
                        meshPaths.Any(path =>
                            path.IndexOf(
                                "/NorwaySpruceHD/",
                                StringComparison.Ordinal) >= 0))
                    {
                        throw new InvalidDataException(
                            "Engelmann spruce still references legacy " +
                            $"far-LOD geometry: {prefab.name}, " +
                            $"lod={lodIndex}, meshes=[" +
                            string.Join(",", meshPaths) + "]");
                    }
                }
                Bounds highBounds =
                    CalculateRendererBounds(lods[0].renderers);
                for (int lodIndex = 1;
                     lodIndex < lods.Length;
                     lodIndex++)
                {
                    Bounds lodBounds = CalculateRendererBounds(
                        lods[lodIndex].renderers);
                    float ratio = lodBounds.size.y /
                                  Mathf.Max(
                                      highBounds.size.y,
                                      0.01f);
                    if (ratio < 0.9f || ratio > 1.1f)
                    {
                        throw new InvalidDataException(
                            "Engelmann spruce LOD height mismatch: " +
                            $"{prefab.name}, lod={lodIndex}, " +
                            $"ratio={ratio:0.###}");
                    }
                }
            }
        }

        private static void ValidateNorwaySprucePrefabs(
            IReadOnlyList<GameObject> prefabs)
        {
            if (prefabs.Count !=
                NorwaySpruceVariantNames.Length ||
                prefabs.Any(prefab => prefab == null))
            {
                throw new InvalidDataException(
                    "Norway spruce prefab generation is incomplete.");
            }
            foreach (GameObject prefab in prefabs)
            {
                LODGroup group =
                    prefab.GetComponent<LODGroup>();
                if (group == null || group.lodCount != 3)
                {
                    throw new InvalidDataException(
                        "Norway spruce prefab has invalid LOD coverage: " +
                        prefab.name);
                }
                if (prefab.GetComponent<CapsuleCollider>() == null)
                {
                    throw new InvalidDataException(
                        "Norway spruce prefab has no trunk collider: " +
                        prefab.name);
                }
                LOD[] lods = group.GetLODs();
                Bounds nearBounds =
                    CalculateRendererBounds(lods[0].renderers);
                Bounds billboardBounds =
                    CalculateRendererBounds(
                        lods[lods.Length - 1].renderers);
                float heightRatio = billboardBounds.size.y /
                                    Mathf.Max(
                                        nearBounds.size.y,
                                        0.01f);
                if (heightRatio < 0.92f || heightRatio > 1.08f)
                {
                    throw new InvalidDataException(
                        "Norway spruce billboard height does not match " +
                        $"near geometry: {prefab.name}, ratio=" +
                        heightRatio.ToString("0.###"));
                }
                Material[] materials = prefab
                    .GetComponentsInChildren<Renderer>(true)
                    .SelectMany(renderer => renderer.sharedMaterials)
                    .Where(material => material != null)
                    .Distinct()
                    .ToArray();
                if (!materials.Any(
                        material =>
                            material.HasProperty("_AlphaCutoffEnable") &&
                            material.GetFloat("_AlphaCutoffEnable") > 0.5f) ||
                    !materials.Any(
                        material =>
                            string.Equals(
                                AssetDatabase.GetAssetPath(material),
                                NorwaySpruceBillboardMaterialPath,
                                StringComparison.Ordinal)))
                {
                    throw new InvalidDataException(
                        "Norway spruce prefab has no valid foliage or " +
                        "billboard material coverage: " + prefab.name);
                }
            }
        }

        private static Bounds CalculateRendererBounds(
            IReadOnlyList<Renderer> renderers)
        {
            Renderer first = renderers.FirstOrDefault(
                renderer => renderer != null);
            if (first == null)
            {
                return new Bounds(Vector3.zero, Vector3.zero);
            }
            Bounds bounds = first.bounds;
            for (int index = 0; index < renderers.Count; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer != null && renderer != first)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        private static Material ResolveSpruceMaterial(
            string slotName,
            string rendererName,
            Material bark,
            Material branch,
            Material billboard)
        {
            string identifier =
                (slotName + " " + rendererName).ToLowerInvariant();
            if (identifier.Contains("billboard"))
            {
                return billboard;
            }
            if (identifier.Contains("branch") ||
                identifier.Contains("leaf") ||
                identifier.Contains("needle"))
            {
                return branch;
            }
            return bark;
        }

        private static void ConfigureLodGroup(
            GameObject instance,
            Renderer[] renderers)
        {
            LODGroup group = instance.GetComponent<LODGroup>() ??
                             instance.AddComponent<LODGroup>();
            var byIndex = new SortedDictionary<int, List<Renderer>>();
            foreach (Renderer renderer in renderers)
            {
                int index = FindLodIndex(renderer.transform, instance.transform);
                if (!byIndex.TryGetValue(index, out List<Renderer> list))
                {
                    list = new List<Renderer>();
                    byIndex.Add(index, list);
                }
                list.Add(renderer);
            }

            float[] transitions =
            {
                0.22f,
                0.09f,
                0.035f,
                0.012f,
                0.003f
            };
            var lods = new List<LOD>(byIndex.Count);
            int ordinal = 0;
            foreach (KeyValuePair<int, List<Renderer>> pair in byIndex)
            {
                float transition = transitions[
                    Math.Min(ordinal, transitions.Length - 1)];
                var lod = new LOD(
                    transition,
                    pair.Value.ToArray())
                {
                    fadeTransitionWidth = 0.18f
                };
                lods.Add(lod);
                ordinal++;
            }
            group.SetLODs(lods.ToArray());
            group.fadeMode = LODFadeMode.CrossFade;
            group.animateCrossFading = false;
            group.RecalculateBounds();
        }

        private static int FindLodIndex(
            Transform target,
            Transform root)
        {
            Transform current = target;
            while (current != null)
            {
                Match match = LodPattern.Match(current.name);
                if (match.Success &&
                    int.TryParse(
                        match.Groups["index"].Value,
                        out int index))
                {
                    return index;
                }
                if (current == root)
                {
                    break;
                }
                current = current.parent;
            }
            return 0;
        }

        private static void AddTrunkCollider(GameObject instance)
        {
            if (instance.GetComponent<Collider>() != null)
            {
                return;
            }

            Bounds bounds = CalculateBounds(instance);
            if (bounds.size.y <= 0.01f)
            {
                return;
            }
            var collider = instance.AddComponent<CapsuleCollider>();
            collider.direction = 1;
            collider.radius = Mathf.Clamp(
                bounds.size.y * 0.024f,
                0.12f,
                0.55f);
            collider.height = Mathf.Max(
                collider.radius * 2f,
                bounds.size.y * 0.52f);
            Vector3 localCenter =
                instance.transform.InverseTransformPoint(
                    new Vector3(
                        bounds.center.x,
                        bounds.min.y + collider.height * 0.5f,
                        bounds.center.z));
            collider.center = localCenter;
        }

        private static string GetRelativeTransformPath(
            Transform root,
            Transform target)
        {
            var names = new Stack<string>();
            Transform current = target;
            while (current != null && current != root)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return names.Count > 0
                ? string.Join("/", names)
                : root.name;
        }

        private static Bounds CalculateBounds(GameObject target)
        {
            Renderer[] renderers =
                target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(target.transform.position, Vector3.zero);
            }
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; index++)
            {
                bounds.Encapsulate(renderers[index].bounds);
            }
            return bounds;
        }

        private static void ValidatePrefabs(
            IReadOnlyList<GameObject> prefabs)
        {
            if (prefabs.Count != SpruceModelNames.Length ||
                prefabs.Any(prefab => prefab == null))
            {
                throw new InvalidDataException(
                    "Spruce prefab generation is incomplete.");
            }
            foreach (GameObject prefab in prefabs)
            {
                LODGroup group = prefab.GetComponent<LODGroup>();
                if (group == null || group.lodCount < 3)
                {
                    throw new InvalidDataException(
                        "Spruce prefab has insufficient LOD coverage: " +
                        prefab.name);
                }
                if (prefab.GetComponent<CapsuleCollider>() == null)
                {
                    throw new InvalidDataException(
                        "Spruce prefab has no trunk collider: " +
                        prefab.name);
                }
            }
        }

        private static Texture2D LoadReadableTexture(string assetPath)
        {
            SetReadable(assetPath, true);
            return RequireAsset<Texture2D>(assetPath);
        }

        private static void SetReadable(string assetPath, bool value)
        {
            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidDataException(
                    "Texture has no importer: " + assetPath);
            }
            if (importer.isReadable != value)
            {
                importer.isReadable = value;
                importer.SaveAndReimport();
            }
        }

        private static void WriteGeneratedTexture(
            string assetPath,
            int width,
            int height,
            Color32[] pixels,
            bool sRgb,
            bool normalMap)
        {
            EnsureAssetFolder(assetPath);
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false,
                !sRgb);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                string absolutePath = ToAbsoluteProjectPath(assetPath);
                File.WriteAllBytes(
                    absolutePath,
                    texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            TextureImporter importer =
                AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidDataException(
                    "Generated texture has no importer: " + assetPath);
            }
            importer.sRGBTexture = sRgb;
            importer.textureType = normalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        private static Material LoadOrCreateMaterial(
            string path,
            string displayName,
            string shaderName = "HDRP/Lit")
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException(
                    shaderName + " shader is unavailable.");
            }
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                if (material.shader != shader)
                {
                    material.shader = shader;
                }
                material.name = displayName;
                return material;
            }
            material = new Material(shader)
            {
                name = displayName
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ValidateAndSave(Material material)
        {
            bool isProjectSpruceWind =
                IsNorwaySpruceWindMaterial(material);
            if (!isProjectSpruceWind &&
                !HDMaterial.ValidateMaterial(material))
            {
                throw new InvalidDataException(
                    "HDRP rejected vegetation material: " +
                    material.name);
            }
            EditorUtility.SetDirty(material);
        }

        private static void RequireAsset(string assetPath)
        {
            if (!File.Exists(ToAbsoluteProjectPath(assetPath)))
            {
                throw new FileNotFoundException(
                    "Required vegetation source is missing.",
                    ToAbsoluteProjectPath(assetPath));
            }
        }

        private static T RequireAsset<T>(string assetPath)
            where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);
            if (asset == null)
            {
                throw new FileNotFoundException(
                    "Required vegetation asset is unavailable.",
                    ToAbsoluteProjectPath(assetPath));
            }
            return asset;
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string folder = Path.HasExtension(assetPath)
                ? Path.GetDirectoryName(assetPath)?.Replace('\\', '/')
                : assetPath;
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException(
                    "Asset path has no parent folder.",
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

        private static bool SetIfDifferent<T>(
            T current,
            T expected,
            Action<T> setter)
        {
            if (EqualityComparer<T>.Default.Equals(current, expected))
            {
                return false;
            }
            setter(expected);
            return true;
        }

        private readonly struct NorwayFoliageProfile
        {
            public NorwayFoliageProfile(
                string id,
                string baseColorFileName,
                string normalFileName,
                string opacityFileName)
            {
                Id = id;
                BaseColorFileName = baseColorFileName;
                NormalFileName = normalFileName;
                OpacityFileName = opacityFileName;
            }

            public string Id { get; }
            public string BaseColorFileName { get; }
            public string NormalFileName { get; }
            public string OpacityFileName { get; }
        }
    }
}
