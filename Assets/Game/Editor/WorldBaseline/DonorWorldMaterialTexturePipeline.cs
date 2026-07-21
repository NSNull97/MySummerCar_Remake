using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using MSC.LegacyImport;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[assembly: InternalsVisibleTo("MSC.Tests.EditMode")]

namespace MSC.Editor.WorldBaseline
{
    public sealed class DonorWorldMaterialTextureAssets
    {
        private readonly IReadOnlyDictionary<string, Material>
            texturedMaterials;
        private readonly IReadOnlyDictionary<string, Texture>
            convertedTextures;

        public DonorWorldMaterialTextureAssets(
            DonorWorldMaterialTexturePlan plan,
            IReadOnlyDictionary<string, Material> materials,
            IReadOnlyDictionary<string, Texture> textures,
            Material unsupportedMaterial)
        {
            Plan = plan;
            texturedMaterials = materials;
            convertedTextures = textures;
            UnsupportedMaterial = unsupportedMaterial;
        }

        public DonorWorldMaterialTexturePlan Plan { get; }
        public Material UnsupportedMaterial { get; }
        public IReadOnlyDictionary<string, Material>
            TexturedMaterials => texturedMaterials;
        public IReadOnlyDictionary<string, Texture>
            ConvertedTextures => convertedTextures;

        public Material GetTexturedMaterial(string sourceMaterialGuid)
        {
            if (string.Equals(
                    sourceMaterialGuid,
                    DonorWorldMaterialTexturePlan.BuiltInFallbackGuid,
                    StringComparison.Ordinal))
            {
                return UnsupportedMaterial;
            }
            if (!texturedMaterials.TryGetValue(
                    sourceMaterialGuid,
                    out Material material) ||
                material == null)
            {
                throw new KeyNotFoundException(
                    "Generated HDRP compatibility material is missing: " +
                    sourceMaterialGuid);
            }

            return material;
        }

        public Texture GetConvertedTexture(
            string sourceTextureGuid,
            DonorWorldTextureRole role)
        {
            string key = DonorWorldTextureConversion.BuildKey(
                sourceTextureGuid,
                role);
            if (!convertedTextures.TryGetValue(
                    key,
                    out Texture texture) ||
                texture == null)
            {
                throw new KeyNotFoundException(
                    "Generated compatibility texture is missing: " + key);
            }

            return texture;
        }

        public string[] ResolveSourceMaterialSlots(
            WorldBaselineSanitationEntry entry,
            int subMeshCount) =>
            DonorWorldMaterialTexturePlan.ResolveSourceMaterialSlots(
                entry,
                subMeshCount);

        public Material[] ResolveTexturedMaterials(
            IReadOnlyList<string> sourceMaterialSlots)
        {
            var result = new Material[sourceMaterialSlots.Count];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = GetTexturedMaterial(
                    sourceMaterialSlots[index]);
            }

            return result;
        }
    }

    public static class DonorWorldMaterialTexturePipeline
    {
        private const float DefaultSmoothness = 0.18f;
        private const float DoubleSidedNormalFlip = 0f;
        private const float DoubleSidedNormalNone = 2f;
        internal const string CompatibilityPolicyVersion =
            "08A1-temporary-hdrp-compatibility-v3";
        private const string MaterialPrefix = "M06B2_";

        private static readonly HashSet<string>
            LegacyDiffuseDetailSurfaceMaterialGuids =
                new HashSet<string>(StringComparer.Ordinal)
                {
                    // ROAD, LANDFILL_PILES, GRAVEL, GRASS, ROADSIDE,
                    // DIRTROAD, TRACKFIELD_track, TERRAIN and ROCKS from
                    // the frozen 04A1 donor source revision.
                    "2b8378937c6afb64390d5474f4bcd14a",
                    "5c3c46f13bd5de54bae979b1ee3f87a7",
                    "5cc44389f1f10bf4cabee6d33feb1551",
                    "7a19fb2d522ee4c44a2ae61c01a064fa",
                    "a1ac98c6256c9324c9937bf44cd37a32",
                    "bee65a18eecc0bc409361e160d6b1aca",
                    "d7de8061a89f3cf4d9be04e0cf719f9f",
                    "da5bc03c62a0f174197555e90357aac9",
                    "ebf3d2234b1ae464498b463feec76368"
                };

        internal static IReadOnlyCollection<string>
            LegacyDiffuseDetailSurfaceGuids =>
                LegacyDiffuseDetailSurfaceMaterialGuids;

        [MenuItem(
            "Tools/MSC Remake/World Baseline 06B2/" +
            "Build Legacy Material + Texture Presentation")]
        public static void BuildFromMenu()
        {
            DonorWorldMaterialTexturePlan plan =
                DonorWorldMaterialTexturePlan.Load();
            DonorWorldMaterialTextureAssets assets = Build(plan);
            Debug.Log(
                "DONOR_WORLD_PRESENTATION_06B2_BUILD_OK " +
                $"materials={assets.TexturedMaterials.Count} " +
                $"textureConversions={assets.ConvertedTextures.Count} " +
                $"fingerprint={plan.PresentationFingerprintSha256}");
        }

        public static DonorWorldMaterialTextureAssets Build(
            DonorWorldMaterialTexturePlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            EnsureAssetFolder(WorldBaselinePaths.TexturedMaterialRoot);
            EnsureAssetFolder(WorldBaselinePaths.TextureRoot);
            PruneGeneratedTextures(plan);
            CopySourceTextures(plan);
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            IReadOnlyDictionary<string, Texture> textures =
                ConfigureAndLoadTextures(plan);
            IReadOnlyDictionary<string, Material> materials =
                CreateCompatibilityMaterials(plan, textures);
            Material fallback = CreateUnsupportedMaterial();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var result = new DonorWorldMaterialTextureAssets(
                plan,
                materials,
                textures,
                fallback);
            WriteAuditExports(result);
            return result;
        }

        public static DonorWorldMaterialTextureAssets LoadGenerated(
            DonorWorldMaterialTexturePlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }

            var textures =
                new Dictionary<string, Texture>(StringComparer.Ordinal);
            foreach (DonorWorldTextureConversion conversion in
                     plan.Textures.Values.Where(value => value.IsImported))
            {
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(
                    conversion.GeneratedAssetPath);
                if (texture == null)
                {
                    throw new FileNotFoundException(
                        "Generated compatibility texture is missing.",
                        WorldBaselinePaths.ToAbsoluteProjectPath(
                            conversion.GeneratedAssetPath));
                }
                textures.Add(conversion.Key, texture);
            }

            var materials =
                new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (DonorWorldSourceMaterial source in
                     plan.Materials.Values)
            {
                string path =
                    WorldBaselinePaths.TexturedMaterial(
                        source.SourceGuid);
                Material material =
                    AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    throw new FileNotFoundException(
                        "Generated compatibility material is missing.",
                        WorldBaselinePaths.ToAbsoluteProjectPath(path));
                }
                materials.Add(source.SourceGuid, material);
            }

            Material fallback =
                AssetDatabase.LoadAssetAtPath<Material>(
                    WorldBaselinePaths.UnsupportedMaterial);
            if (fallback == null)
            {
                throw new FileNotFoundException(
                    "Generated unsupported-source material is missing.",
                    WorldBaselinePaths.ToAbsoluteProjectPath(
                        WorldBaselinePaths.UnsupportedMaterial));
            }

            return new DonorWorldMaterialTextureAssets(
                plan,
                materials,
                textures,
                fallback);
        }

        private static void CopySourceTextures(
            DonorWorldMaterialTexturePlan plan)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (DonorWorldTextureConversion conversion in
                         plan.Textures.Values
                             .Where(value => value.IsImported)
                             .OrderBy(
                                 value => value.Key,
                                 StringComparer.Ordinal))
                {
                    string destination =
                        WorldBaselinePaths.ToAbsoluteProjectPath(
                            conversion.GeneratedAssetPath);
                    Directory.CreateDirectory(
                        Path.GetDirectoryName(destination) ??
                        throw new InvalidOperationException(
                            "Generated texture path has no directory."));
                    byte[] generatedBytes =
                        conversion.IsPackedDetailNormal
                            ? BuildPackedDetailNormalPng(
                                conversion.SourceAbsolutePath)
                            : conversion.IsPackedDetailAlbedo
                                ? BuildPackedDetailAlbedoPng(
                                    conversion.SourceAbsolutePath)
                            : null;
                    string expectedSha256 =
                        generatedBytes == null
                            ? conversion.SourceSha256
                            : ComputeSha256(generatedBytes);
                    if (File.Exists(destination) &&
                        string.Equals(
                            DonorWorldBaselineManifest.ComputeFileSha256(
                                destination),
                            expectedSha256,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (generatedBytes == null)
                    {
                        File.Copy(
                            conversion.SourceAbsolutePath,
                            destination,
                            overwrite: true);
                    }
                    else
                    {
                        File.WriteAllBytes(
                            destination,
                            generatedBytes);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
        }

        internal static string GetExpectedGeneratedTextureSha256(
            DonorWorldTextureConversion conversion) =>
            conversion.IsPackedDetailNormal
                ? ComputeSha256(
                    BuildPackedDetailNormalPng(
                        conversion.SourceAbsolutePath))
                : conversion.IsPackedDetailAlbedo
                    ? ComputeSha256(
                        BuildPackedDetailAlbedoPng(
                            conversion.SourceAbsolutePath))
                : conversion.SourceSha256;

        internal static byte[] BuildPackedDetailNormalPng(
            string sourcePath)
        {
            return BuildPackedDetailMapPng(
                sourcePath,
                pixel => new Color32(
                    128,
                    pixel.g,
                    128,
                    pixel.r),
                "donor detail normal");
        }

        internal static byte[] BuildPackedDetailAlbedoPng(
            string sourcePath)
        {
            return BuildPackedDetailMapPng(
                sourcePath,
                pixel => new Color32(
                    GetPackedDetailAlbedoLuminance(pixel),
                    128,
                    128,
                    128),
                "donor detail albedo");
        }

        internal static byte GetPackedDetailAlbedoLuminance(
            Color32 pixel)
        {
            // HDRP consumes detail R as a perceptual signed value around
            // 0.5. Preserve the donor sRGB-domain brightness relationship
            // rather than applying a second gamma conversion.
            int weighted =
                54 * pixel.r +
                183 * pixel.g +
                19 * pixel.b;
            return (byte)((weighted + 128) >> 8);
        }

        private static byte[] BuildPackedDetailMapPng(
            string sourcePath,
            Func<Color32, Color32> packPixel,
            string sourceLabel)
        {
            var source = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true);
            Texture2D packed = null;
            try
            {
                if (!ImageConversion.LoadImage(
                        source,
                        File.ReadAllBytes(sourcePath),
                        markNonReadable: false))
                {
                    throw new InvalidDataException(
                        "Could not decode " + sourceLabel + ": " +
                        sourcePath);
                }

                Color32[] sourcePixels = source.GetPixels32();
                var packedPixels = new Color32[sourcePixels.Length];
                for (int index = 0; index < sourcePixels.Length; index++)
                {
                    packedPixels[index] = packPixel(sourcePixels[index]);
                }

                packed = new Texture2D(
                    source.width,
                    source.height,
                    TextureFormat.RGBA32,
                    mipChain: false,
                    linear: true);
                packed.SetPixels32(packedPixels);
                packed.Apply(
                    updateMipmaps: false,
                    makeNoLongerReadable: false);
                byte[] encoded = packed.EncodeToPNG();
                if (encoded == null || encoded.Length == 0)
                {
                    throw new InvalidDataException(
                        "Could not encode HDRP detail map: " +
                        sourcePath);
                }
                return encoded;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (packed != null)
                {
                    UnityEngine.Object.DestroyImmediate(packed);
                }
            }
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using SHA256 sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(bytes))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        private static IReadOnlyDictionary<string, Texture>
            ConfigureAndLoadTextures(
                DonorWorldMaterialTexturePlan plan)
        {
            var result =
                new Dictionary<string, Texture>(StringComparer.Ordinal);
            foreach (DonorWorldTextureConversion conversion in
                     plan.Textures.Values
                         .Where(value => value.IsImported)
                         .OrderBy(
                             value => value.Key,
                             StringComparer.Ordinal))
            {
                TextureImporter importer =
                    AssetImporter.GetAtPath(
                        conversion.GeneratedAssetPath) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException(
                        "Generated texture has no TextureImporter: " +
                        conversion.GeneratedAssetPath);
                }

                importer.textureType =
                    conversion.IsNormalMap
                        ? TextureImporterType.NormalMap
                        : TextureImporterType.Default;
                importer.sRGBTexture = conversion.SRgb;
                importer.mipmapEnabled = true;
                importer.isReadable = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.streamingMipmaps = true;
                importer.streamingMipmapsPriority = 0;
                importer.alphaIsTransparency =
                    conversion.Role == DonorWorldTextureRole.Color;
                importer.wrapMode = ResolveWrapMode(
                    conversion.SourceWrapMode);
                importer.filterMode = ResolveFilterMode(
                    conversion.SourceFilterMode);
                importer.anisoLevel = Mathf.Clamp(
                    conversion.SourceAnisoLevel,
                    1,
                    16);
                importer.maxTextureSize = 8192;
                importer.textureCompression =
                    TextureImporterCompression.CompressedHQ;
                importer.compressionQuality = 100;
                importer.crunchedCompression = false;
                TextureImporterPlatformSettings standalone =
                    importer.GetPlatformTextureSettings("Standalone");
                standalone.name = "Standalone";
                standalone.overridden = true;
                standalone.maxTextureSize = 8192;
                standalone.format = TextureImporterFormat.Automatic;
                standalone.textureCompression =
                    TextureImporterCompression.CompressedHQ;
                standalone.compressionQuality = 100;
                standalone.crunchedCompression = false;
                standalone.allowsAlphaSplitting = false;
                importer.SetPlatformTextureSettings(standalone);
                importer.SaveAndReimport();

                importer.GetSourceTextureWidthAndHeight(
                    out int sourceWidth,
                    out int sourceHeight);
                if (sourceWidth <= 0 ||
                    sourceHeight <= 0 ||
                    sourceWidth > importer.maxTextureSize ||
                    sourceHeight > importer.maxTextureSize)
                {
                    throw new InvalidDataException(
                        "Generated texture dimensions cannot be preserved: " +
                        conversion.GeneratedAssetPath + " -> " +
                        sourceWidth + "x" + sourceHeight + ".");
                }

                Texture texture =
                    AssetDatabase.LoadAssetAtPath<Texture>(
                        conversion.GeneratedAssetPath);
                if (texture == null ||
                    texture.width != sourceWidth ||
                    texture.height != sourceHeight)
                {
                    throw new InvalidDataException(
                        "Imported texture dimensions differ from source: " +
                        conversion.GeneratedAssetPath + ".");
                }
                result.Add(conversion.Key, texture);
            }

            return result;
        }

        private static IReadOnlyDictionary<string, Material>
            CreateCompatibilityMaterials(
                DonorWorldMaterialTexturePlan plan,
                IReadOnlyDictionary<string, Texture> textures)
        {
            var expectedPaths = plan.Materials.Values
                .Select(source =>
                    WorldBaselinePaths.TexturedMaterial(
                        source.SourceGuid))
                .Append(WorldBaselinePaths.UnsupportedMaterial)
                .ToHashSet(StringComparer.Ordinal);
            PruneAssets(
                WorldBaselinePaths.TexturedMaterialRoot,
                expectedPaths,
                "t:Material");

            var result =
                new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (DonorWorldSourceMaterial source in
                     plan.Materials.Values.OrderBy(
                         value => value.SourceGuid,
                         StringComparer.Ordinal))
            {
                string path =
                    WorldBaselinePaths.TexturedMaterial(
                        source.SourceGuid);
                bool useUnlit =
                    source.CompatibilityClass ==
                        DonorWorldCompatibilityClass.Unlit ||
                    source.CompatibilityClass ==
                        DonorWorldCompatibilityClass.TransparentUnlit;
                Shader shader = Shader.Find(
                    useUnlit ? "HDRP/Unlit" : "HDRP/Lit");
                if (shader == null)
                {
                    throw new InvalidOperationException(
                        (useUnlit ? "HDRP/Unlit" : "HDRP/Lit") +
                        " shader is unavailable.");
                }

                Material material =
                    AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }
                else
                {
                    ResetMaterialState(material, shader);
                }
                material.name = MaterialPrefix +
                                source.SourceGuid + "_" +
                                SanitizeName(source.MaterialName);
                ConfigureCompatibilityMaterial(
                    source,
                    material,
                    textures);
                EditorUtility.SetDirty(material);
                result.Add(source.SourceGuid, material);
            }

            return result;
        }

        private static void ConfigureCompatibilityMaterial(
            DonorWorldSourceMaterial source,
            Material material,
            IReadOnlyDictionary<string, Texture> textures)
        {
            material.shaderKeywords = Array.Empty<string>();
            material.enableInstancing = true;
            material.renderQueue = -1;

            Color baseColor = GetExpectedBaseColor(source);
            SetColorIfPresent(material, "_BaseColor", baseColor);
            SetColorIfPresent(material, "_UnlitColor", baseColor);
            SetFloatIfPresent(
                material,
                "_Metallic",
                GetExpectedMetallic(source));
            SetFloatIfPresent(
                material,
                "_Smoothness",
                GetExpectedSmoothness(source));

            if (TryGetExpectedBaseTexture(
                    source,
                    out DonorWorldTextureEnvironment baseTexture))
            {
                Texture texture = RequireTexture(
                    textures,
                    baseTexture.TextureGuid,
                    DonorWorldTextureRole.Color);
                SetTextureWithTransform(
                    material,
                    "_BaseColorMap",
                    texture,
                    baseTexture);
                SetTextureWithTransform(
                    material,
                    "_UnlitColorMap",
                    texture,
                    baseTexture);
            }

            if (!IsUnlit(source) &&
                TryGetExpectedPrimaryNormalTexture(
                    source,
                    out DonorWorldTextureEnvironment normalTexture))
            {
                Texture texture = RequireTexture(
                    textures,
                    normalTexture.TextureGuid,
                    DonorWorldTextureRole.Normal);
                SetTextureWithTransform(
                    material,
                    "_NormalMap",
                    texture,
                    normalTexture);
                SetFloatIfPresent(
                    material,
                    "_NormalScale",
                    GetExpectedNormalScale(source));
                material.EnableKeyword(
                    "_NORMALMAP_TANGENT_SPACE");
            }

            if (!IsUnlit(source) &&
                TryGetExpectedDetailTexture(
                    source,
                    out DonorWorldTextureEnvironment detailTexture,
                    out DonorWorldTextureRole detailRole))
            {
                Texture texture = RequireTexture(
                    textures,
                    detailTexture.TextureGuid,
                    detailRole);
                SetTextureWithTransform(
                    material,
                    "_DetailMap",
                    texture,
                    detailTexture);
                SetFloatIfPresent(
                    material,
                    "_DetailAlbedoScale",
                    detailRole ==
                        DonorWorldTextureRole.DetailAlbedoPacked
                            ? GetExpectedDetailAlbedoScale(source)
                            : 0f);
                SetFloatIfPresent(
                    material,
                    "_DetailNormalScale",
                    detailRole ==
                        DonorWorldTextureRole.DetailNormalPacked
                            ? GetExpectedDetailNormalScale(source)
                            : 0f);
                SetFloatIfPresent(
                    material,
                    "_DetailSmoothnessScale",
                    0f);
                SetFloatIfPresent(
                    material,
                    "_LinkDetailsWithBase",
                    0f);
                SetFloatIfPresent(
                    material,
                    "_UVDetail",
                    GetExpectedDetailUv(source));
                SetColorIfPresent(
                    material,
                    "_UVDetailsMappingMask",
                    GetExpectedDetailUvMask(source));
                material.EnableKeyword("_DETAIL_MAP");
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword(
                    "_NORMALMAP_TANGENT_SPACE");
            }

            // The donor's static world export contains stateful "on" and
            // "off" variants, but no project-owned gameplay state capable of
            // selecting them yet. Emission therefore stays disabled in the
            // temporary world baseline. Later gameplay presenters may enable
            // it explicitly without changing the compatibility material.
            DisableTemporaryWorldEmission(material);

            ConfigureSurface(source, material);
            ValidateHdrpMaterial(material, source.SourceGuid);
            ApplyFinalTemporaryMaterialBounds(source, material);
        }

        private static void ConfigureSurface(
            DonorWorldSourceMaterial source,
            Material material)
        {
            bool alphaClip = IsAlphaClip(source);
            bool transparent = IsTransparent(source);
            SetFloatIfPresent(
                material,
                "_SurfaceType",
                transparent ? 1f : 0f);
            SetFloatIfPresent(
                material,
                "_AlphaCutoffEnable",
                alphaClip ? 1f : 0f);
            SetFloatIfPresent(
                material,
                "_AlphaCutoff",
                Mathf.Clamp01(
                    source.GetFloat("_Cutoff", 0.5f)));
            SetFloatIfPresent(
                material,
                "_DoubleSidedEnable",
                source.DoubleSided ? 1f : 0f);
            SetFloatIfPresent(
                material,
                "_DoubleSidedNormalMode",
                GetExpectedDoubleSidedNormalMode(source));
            SetVectorIfPresent(
                material,
                "_DoubleSidedConstants",
                GetExpectedDoubleSidedConstants(source));
            SetFloatIfPresent(
                material,
                "_CullMode",
                source.DoubleSided
                    ? (float)CullMode.Off
                    : (float)CullMode.Back);
            SetFloatIfPresent(
                material,
                "_CullModeForward",
                source.DoubleSided
                    ? (float)CullMode.Off
                    : (float)CullMode.Back);

            if (transparent)
            {
                material.renderQueue = (int)RenderQueue.Transparent;
                material.EnableKeyword(
                    "_SURFACE_TYPE_TRANSPARENT");
                SetFloatIfPresent(material, "_BlendMode", 0f);
                SetFloatIfPresent(material, "_ZWrite", 0f);
                SetFloatIfPresent(
                    material,
                    "_TransparentZWrite",
                    0f);
            }
            else if (alphaClip)
            {
                material.renderQueue = (int)RenderQueue.AlphaTest;
                material.EnableKeyword("_ALPHATEST_ON");
                SetFloatIfPresent(material, "_ZWrite", 1f);
            }
            else
            {
                material.renderQueue = (int)RenderQueue.Geometry;
                SetFloatIfPresent(material, "_ZWrite", 1f);
            }

            if (source.DoubleSided)
            {
                material.EnableKeyword("_DOUBLESIDED_ON");
            }
        }

        private static Material CreateUnsupportedMaterial()
        {
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "HDRP/Unlit shader is unavailable.");
            }
            Material material =
                AssetDatabase.LoadAssetAtPath<Material>(
                    WorldBaselinePaths.UnsupportedMaterial);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(
                    material,
                    WorldBaselinePaths.UnsupportedMaterial);
            }
            else
            {
                ResetMaterialState(material, shader);
            }
            material.name = "M06B2_UnsupportedSource_Orange";
            Color fallback =
                new Color(1f, 0.08f, 0.01f, 1f);
            SetColorIfPresent(material, "_UnlitColor", fallback);
            SetColorIfPresent(material, "_BaseColor", fallback);
            material.renderQueue = (int)RenderQueue.Geometry;
            material.enableInstancing = true;
            ValidateHdrpMaterial(
                material,
                DonorWorldMaterialTexturePlan.BuiltInFallbackGuid);
            EditorUtility.SetDirty(material);
            return material;
        }

        internal static bool IsUnlit(
            DonorWorldSourceMaterial source) =>
            source.CompatibilityClass ==
                DonorWorldCompatibilityClass.Unlit ||
            source.CompatibilityClass ==
                DonorWorldCompatibilityClass.TransparentUnlit;

        internal static bool IsAlphaClip(
            DonorWorldSourceMaterial source) =>
            source.CompatibilityClass ==
            DonorWorldCompatibilityClass.AlphaClipLit;

        internal static bool IsTransparent(
            DonorWorldSourceMaterial source) =>
            source.CompatibilityClass ==
                DonorWorldCompatibilityClass.TransparentLit ||
            source.CompatibilityClass ==
                DonorWorldCompatibilityClass.TransparentUnlit ||
            source.CompatibilityClass ==
                DonorWorldCompatibilityClass.TemporaryWater;

        internal static bool IsEmissive(
            DonorWorldSourceMaterial source) =>
            source.CompatibilityClass ==
                DonorWorldCompatibilityClass.EmissiveLit ||
            source.Textures.ContainsKey("_EmissionMap");

        internal static Color GetExpectedBaseColor(
            DonorWorldSourceMaterial source)
        {
            Color sourceColor =
                source.GetColor("_Color", Color.white);
            if (source.CompatibilityClass !=
                DonorWorldCompatibilityClass.TemporaryWater)
            {
                return sourceColor;
            }

            Color waterBaseColor =
                source.GetColor("_BaseColor", sourceColor);
            float waterAlpha = waterBaseColor.a > 0f
                ? waterBaseColor.a
                : sourceColor.a > 0f
                    ? sourceColor.a
                    : 0.35f;
            return new Color(
                0.08f,
                0.22f,
                0.28f,
                Mathf.Clamp(waterAlpha, 0.05f, 0.75f));
        }

        internal static float GetExpectedMetallic(
            DonorWorldSourceMaterial source)
        {
            // Exact Standard-shader identity is not evidence that a donor
            // surface is actually metal. The frozen export contains windows,
            // signs, bottles and dirt-like props with non-zero metallic values.
            // Keep the Phase 1 world baseline dielectric until reviewed
            // per-material metal identities are introduced.
            return 0f;
        }

        internal static float GetExpectedSmoothness(
            DonorWorldSourceMaterial source)
        {
            float donorSmoothness = Mathf.Clamp01(source.GetFloat(
                "_Glossiness",
                source.GetFloat("_Shininess", DefaultSmoothness)));
            return Mathf.Min(
                donorSmoothness,
                GetTemporarySmoothnessCap(source));
        }

        internal static bool UsesStandardMetallicWorkflow(
            DonorWorldSourceMaterial source) =>
            source.CompatibilityClass ==
                DonorWorldCompatibilityClass.OpaqueLit &&
            string.Equals(
                source.SourceShaderIdentity,
                "Standard",
                StringComparison.Ordinal);

        internal static bool IsLegacyDiffuseFamily(
            DonorWorldSourceMaterial source) =>
            string.Equals(
                source.SourceShaderIdentity,
                "BuiltIn/7",
                StringComparison.Ordinal) ||
            source.SourceShaderIdentity.IndexOf(
                "Diffuse",
                StringComparison.OrdinalIgnoreCase) >= 0 ||
            IsFoliage(source);

        internal static bool IsSpecularWorkflow(
            DonorWorldSourceMaterial source) =>
            source.SourceShaderIdentity.IndexOf(
                "Specular",
                StringComparison.OrdinalIgnoreCase) >= 0;

        internal static bool IsFoliage(
            DonorWorldSourceMaterial source) =>
            source.DoubleSided ||
            source.SourceShaderIdentity.IndexOf(
                "Leaves",
                StringComparison.OrdinalIgnoreCase) >= 0;

        internal static float GetTemporarySmoothnessCap(
            DonorWorldSourceMaterial source)
        {
            if (IsUnlit(source))
            {
                return 0f;
            }
            if (source.CompatibilityClass ==
                DonorWorldCompatibilityClass.TemporaryWater)
            {
                return 0.18f;
            }
            if (IsTransparent(source))
            {
                return 0.18f;
            }
            if (IsEmissive(source))
            {
                return 0f;
            }
            if (IsFoliage(source))
            {
                return 0.04f;
            }
            if (TryGetExpectedDetailAlbedoTexture(source, out _))
            {
                return 0f;
            }
            if (IsLegacyDiffuseFamily(source))
            {
                return 0.08f;
            }
            if (IsSpecularWorkflow(source))
            {
                return 0.18f;
            }
            if (UsesStandardMetallicWorkflow(source))
            {
                return 0.18f;
            }

            return 0.12f;
        }

        internal static float GetExpectedNormalScale(
            DonorWorldSourceMaterial source) =>
            Mathf.Clamp(
                source.GetFloat("_BumpScale", 1f),
                0f,
                4f);

        internal static float GetExpectedDetailNormalScale(
            DonorWorldSourceMaterial source) =>
            Mathf.Clamp(
                source.GetFloat("_DetailNormalMapScale", 1f),
                0f,
                2f);

        internal static float GetExpectedDetailAlbedoScale(
            DonorWorldSourceMaterial source) =>
            TryGetExpectedDetailAlbedoTexture(source, out _)
                ? 1f
                : 0f;

        internal static int GetExpectedDetailUv(
            DonorWorldSourceMaterial source) =>
            source.GetFloat("_UVSec", 0f) >= 0.5f ? 1 : 0;

        internal static Color GetExpectedDetailUvMask(
            DonorWorldSourceMaterial source) =>
            GetExpectedDetailUv(source) == 1
                ? new Color(0f, 1f, 0f, 0f)
                : new Color(1f, 0f, 0f, 0f);

        internal static Color GetExpectedEmissionColor(
            DonorWorldSourceMaterial source) => Color.black;

        private static void DisableTemporaryWorldEmission(Material material)
        {
            SetColorIfPresent(material, "_EmissiveColor", Color.black);
            SetFloatIfPresent(material, "_UseEmissiveIntensity", 0f);
            SetFloatIfPresent(material, "_EmissiveIntensity", 0f);
            SetFloatIfPresent(material, "_AlbedoAffectEmissive", 0f);
            if (material.HasProperty("_EmissiveColorMap"))
            {
                material.SetTexture("_EmissiveColorMap", null);
            }
            material.DisableKeyword("_EMISSIVE_COLOR_MAP");
        }

        private static void ApplyFinalTemporaryMaterialBounds(
            DonorWorldSourceMaterial source,
            Material material)
        {
            // HDMaterial validation normalizes a number of defaults and may
            // restore smoothness/detail/SSR values. Re-apply the bounded Phase
            // 1 policy afterwards so regenerated files cannot silently regain
            // glossy or emissive defaults.
            SetFloatIfPresent(material, "_Metallic", GetExpectedMetallic(source));
            SetFloatIfPresent(material, "_Smoothness", GetExpectedSmoothness(source));
            SetFloatIfPresent(material, "_DetailSmoothnessScale", 0f);
            DisableTemporaryWorldEmission(material);

            if (LegacyDiffuseDetailSurfaceMaterialGuids.Contains(
                    source.SourceGuid))
            {
                SetFloatIfPresent(material, "_ReceivesSSR", 0f);
                SetFloatIfPresent(material, "_ReceivesSSRTransparent", 0f);
            }
        }

        internal static bool TryGetExpectedBaseTexture(
            DonorWorldSourceMaterial source,
            out DonorWorldTextureEnvironment texture)
        {
            if (source.CompatibilityClass ==
                DonorWorldCompatibilityClass.TemporaryWater)
            {
                texture = null;
                return false;
            }

            return source.TryGetTexture(
                out texture,
                "_MainTex",
                "_Detail",
                "_DetailAlbedoMap",
                "_ShoreTex");
        }

        internal static bool TryGetExpectedPrimaryNormalTexture(
            DonorWorldSourceMaterial source,
            out DonorWorldTextureEnvironment texture) =>
            source.TryGetTexture(
                out texture,
                "_BumpMap",
                "_NormalMap");

        internal static bool TryGetExpectedDetailNormalTexture(
            DonorWorldSourceMaterial source,
            out DonorWorldTextureEnvironment texture)
        {
            if (!source.TryGetTexture(
                    out DonorWorldTextureEnvironment detailNormal,
                    "_DetailNormalMap"))
            {
                texture = null;
                return false;
            }

            DonorWorldTextureTransform transform =
                source.TryGetTextureTransform(
                    "_DetailAlbedoMap",
                    out DonorWorldTextureTransform detailTransform)
                    ? detailTransform
                    : new DonorWorldTextureTransform(
                        detailNormal.Scale,
                        detailNormal.Offset);
            texture = new DonorWorldTextureEnvironment(
                detailNormal.PropertyName,
                detailNormal.TextureGuid,
                transform.Scale,
                transform.Offset);
            return true;
        }

        internal static bool TryGetExpectedDetailAlbedoTexture(
            DonorWorldSourceMaterial source,
            out DonorWorldTextureEnvironment texture)
        {
            if (!LegacyDiffuseDetailSurfaceMaterialGuids.Contains(
                    source.SourceGuid) ||
                !string.Equals(
                    source.SourceShaderIdentity,
                    "Legacy Shaders/Diffuse Detail",
                    StringComparison.Ordinal))
            {
                texture = null;
                return false;
            }

            return source.TryGetTexture(out texture, "_Detail");
        }

        internal static bool TryGetExpectedDetailTexture(
            DonorWorldSourceMaterial source,
            out DonorWorldTextureEnvironment texture,
            out DonorWorldTextureRole role)
        {
            if (TryGetExpectedDetailNormalTexture(source, out texture))
            {
                role = DonorWorldTextureRole.DetailNormalPacked;
                return true;
            }
            if (TryGetExpectedDetailAlbedoTexture(source, out texture))
            {
                role = DonorWorldTextureRole.DetailAlbedoPacked;
                return true;
            }

            role = default;
            return false;
        }

        internal static float GetExpectedDoubleSidedNormalMode(
            DonorWorldSourceMaterial source) =>
            source.DoubleSided
                ? DoubleSidedNormalFlip
                : DoubleSidedNormalNone;

        internal static Vector4 GetExpectedDoubleSidedConstants(
            DonorWorldSourceMaterial source) =>
            source.DoubleSided
                ? new Vector4(-1f, -1f, -1f, 0f)
                : new Vector4(1f, 1f, 1f, 0f);

        internal static bool TryGetExpectedEmissionTexture(
            DonorWorldSourceMaterial source,
            out DonorWorldTextureEnvironment texture) =>
            source.TryGetTexture(out texture, "_EmissionMap");

        private static void ResetMaterialState(
            Material material,
            Shader shader)
        {
            var clean = new Material(shader);
            EditorUtility.CopySerialized(clean, material);
            UnityEngine.Object.DestroyImmediate(clean);
            material.shader = shader;
        }

        private static Texture RequireTexture(
            IReadOnlyDictionary<string, Texture> textures,
            string sourceGuid,
            DonorWorldTextureRole role)
        {
            string key = DonorWorldTextureConversion.BuildKey(
                sourceGuid,
                role);
            if (!textures.TryGetValue(key, out Texture texture) ||
                texture == null)
            {
                throw new KeyNotFoundException(
                    "Required converted texture is missing: " + key);
            }

            return texture;
        }

        private static void SetTextureWithTransform(
            Material material,
            string propertyName,
            Texture texture,
            DonorWorldTextureEnvironment source)
        {
            if (!material.HasProperty(propertyName))
            {
                return;
            }
            material.SetTexture(propertyName, texture);
            material.SetTextureScale(propertyName, source.Scale);
            material.SetTextureOffset(propertyName, source.Offset);
        }

        private static void SetColorIfPresent(
            Material material,
            string propertyName,
            Color value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetColor(propertyName, value);
            }
        }

        private static void SetFloatIfPresent(
            Material material,
            string propertyName,
            float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private static void SetVectorIfPresent(
            Material material,
            string propertyName,
            Vector4 value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetVector(propertyName, value);
            }
        }

        private static void ValidateHdrpMaterial(
            Material material,
            string sourceGuid)
        {
            if (!HDMaterial.ValidateMaterial(material))
            {
                throw new InvalidOperationException(
                    "Generated material is not backed by a supported " +
                    "HDRP shader: " + sourceGuid + ".");
            }
        }

        internal static TextureWrapMode ResolveWrapMode(int source) =>
            source switch
            {
                1 => TextureWrapMode.Clamp,
                2 => TextureWrapMode.Mirror,
                3 => TextureWrapMode.MirrorOnce,
                _ => TextureWrapMode.Repeat
            };

        internal static FilterMode ResolveFilterMode(int source) =>
            source switch
            {
                0 => FilterMode.Point,
                2 => FilterMode.Trilinear,
                _ => FilterMode.Bilinear
            };

        private static string SanitizeName(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                builder.Append(
                    char.IsLetterOrDigit(character) ||
                    character is '_' or '-'
                        ? character
                        : '_');
            }
            return builder.Length == 0
                ? "Material"
                : builder.ToString();
        }

        private static void PruneGeneratedTextures(
            DonorWorldMaterialTexturePlan plan)
        {
            var expected = plan.Textures.Values
                .Where(value => value.IsImported)
                .Select(value => value.GeneratedAssetPath)
                .ToHashSet(StringComparer.Ordinal);
            string absoluteRoot =
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    WorldBaselinePaths.TextureRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                return;
            }

            foreach (string path in Directory.EnumerateFiles(
                         absoluteRoot,
                         "*.png",
                         SearchOption.TopDirectoryOnly))
            {
                string assetPath =
                    WorldBaselinePaths.TextureRoot + "/" +
                    Path.GetFileName(path);
                if (!expected.Contains(assetPath) &&
                    !AssetDatabase.DeleteAsset(assetPath))
                {
                    throw new IOException(
                        "Could not prune stale generated texture: " +
                        assetPath);
                }
            }
        }

        private static void PruneAssets(
            string root,
            ISet<string> expectedPaths,
            string filter)
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                return;
            }
            foreach (string guid in AssetDatabase.FindAssets(
                         filter,
                         new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!expectedPaths.Contains(path) &&
                    path.StartsWith(
                        root + "/",
                        StringComparison.Ordinal) &&
                    !AssetDatabase.DeleteAsset(path))
                {
                    throw new IOException(
                        "Could not prune stale generated asset: " +
                        path);
                }
            }
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

        private static void WriteAuditExports(
            DonorWorldMaterialTextureAssets assets)
        {
            WriteMaterialTextureManifest(assets);
            WriteShaderMapping(assets.Plan);
            WriteVisualCompletenessReport(assets);
            WriteTextureMemoryBaseline(assets);
            WritePresentationSourceManifest(assets);
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
        }

        private static void WriteMaterialTextureManifest(
            DonorWorldMaterialTextureAssets assets)
        {
            var csv = new StringBuilder();
            csv.AppendLine(
                "AssetKind,SourceGuid,SourceRelativePath,SourceSha256," +
                "SourceMetaSha256,SourceShaderOrProperties," +
                "CompatibilityOrRole,GeneratedAssetPath," +
                "ConversionFingerprintSha256,ReferenceCount,Width,Height," +
                "SourceBytes,EstimatedRuntimeBytes,SRGB,NormalMap,Mipmaps," +
                "StreamingMipmaps,WrapMode,FilterMode,AnisoLevel," +
                "Classification,Status,Notes");
            foreach (DonorWorldSourceMaterial material in
                     assets.Plan.Materials.Values.OrderBy(
                         value => value.SourceGuid,
                         StringComparer.Ordinal))
            {
                string fingerprint =
                    DonorWorldBaselineManifest.Sha256Text(
                        DonorWorldMaterialTexturePlan.ConverterVersion +
                        "|" + CompatibilityPolicyVersion +
                        "|" + material.SourceGuid + "|" +
                        material.SourceSha256 + "|" +
                        material.SourceShaderIdentity + "|" +
                        material.CompatibilityClass + "|" +
                        assets.Plan.PresentationFingerprintSha256);
                AppendCsvRow(
                    csv,
                    "Material",
                    material.SourceGuid,
                    material.SourceRelativePath,
                    material.SourceSha256,
                    string.Empty,
                    material.SourceShaderIdentity,
                    material.CompatibilityClass.ToString(),
                    WorldBaselinePaths.TexturedMaterial(
                        material.SourceGuid),
                    fingerprint,
                    assets.Plan.MaterialReferenceCounts[
                            material.SourceGuid]
                        .ToString(CultureInfo.InvariantCulture),
                    string.Empty,
                    string.Empty,
                    new FileInfo(material.SourceAbsolutePath).Length
                        .ToString(CultureInfo.InvariantCulture),
                    Profiler.GetRuntimeMemorySizeLong(
                            assets.TexturedMaterials[
                                material.SourceGuid])
                        .ToString(CultureInfo.InvariantCulture),
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    "TemporaryDirectImport",
                    "Converted",
                    "Project-owned shader-aware HDRP compatibility " +
                    "material (" + CompatibilityPolicyVersion + "); " +
                    "donor shader code is not imported.");
            }

            AppendCsvRow(
                csv,
                "Material",
                DonorWorldMaterialTexturePlan.BuiltInFallbackGuid,
                "Frozen GAME.unity built-in material reference",
                WorldBaselinePaths.SourceSceneSha256,
                string.Empty,
                "BuiltIn/10302 (semantics unavailable in export)",
                DonorWorldCompatibilityClass.Unsupported.ToString(),
                WorldBaselinePaths.UnsupportedMaterial,
                DonorWorldBaselineManifest.Sha256Text(
                    DonorWorldMaterialTexturePlan.ConverterVersion +
                    "|" + CompatibilityPolicyVersion +
                    "|builtin-10302|fallback-orange"),
                assets.Plan.MaterialReferenceCounts[
                        DonorWorldMaterialTexturePlan.BuiltInFallbackGuid]
                    .ToString(CultureInfo.InvariantCulture),
                string.Empty,
                string.Empty,
                "0",
                Profiler.GetRuntimeMemorySizeLong(
                        assets.UnsupportedMaterial)
                    .ToString(CultureInfo.InvariantCulture),
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "TemporaryDirectImport",
                "ReviewedFallback",
                "Used by two FinishPoles renderers; obvious orange " +
                "development fallback, not Unity shader-error magenta.");

            foreach (DonorWorldTextureConversion conversion in
                     assets.Plan.Textures.Values.OrderBy(
                         value => value.Key,
                         StringComparer.Ordinal))
            {
                Texture texture = conversion.IsImported
                    ? assets.ConvertedTextures[conversion.Key]
                    : null;
                TextureImporter importer = conversion.IsImported
                    ? AssetImporter.GetAtPath(
                        conversion.GeneratedAssetPath) as TextureImporter
                    : null;
                int width = texture == null ? 0 : texture.width;
                int height = texture == null ? 0 : texture.height;
                AppendCsvRow(
                    csv,
                    "Texture",
                    conversion.SourceGuid,
                    conversion.SourceRelativePath,
                    conversion.SourceSha256,
                    conversion.SourceMetaSha256,
                    string.Join(";", conversion.SourceProperties),
                    conversion.Role.ToString(),
                    conversion.GeneratedAssetPath,
                    conversion.ConversionFingerprintSha256,
                    conversion.ReferenceCount.ToString(
                        CultureInfo.InvariantCulture),
                    width == 0
                        ? string.Empty
                        : width.ToString(CultureInfo.InvariantCulture),
                    height == 0
                        ? string.Empty
                        : height.ToString(CultureInfo.InvariantCulture),
                    conversion.SourceBytes.ToString(
                        CultureInfo.InvariantCulture),
                    texture == null
                        ? "0"
                        : Profiler.GetRuntimeMemorySizeLong(texture)
                            .ToString(CultureInfo.InvariantCulture),
                    conversion.IsImported
                        ? (conversion.SRgb ? "1" : "0")
                        : string.Empty,
                    conversion.IsImported
                        ? (conversion.IsNormalMap ? "1" : "0")
                        : string.Empty,
                    conversion.IsImported ? "1" : string.Empty,
                    conversion.IsImported ? "1" : string.Empty,
                    importer == null
                        ? string.Empty
                        : importer.wrapMode.ToString(),
                    importer == null
                        ? string.Empty
                        : importer.filterMode.ToString(),
                    importer == null
                        ? string.Empty
                        : importer.anisoLevel.ToString(
                            CultureInfo.InvariantCulture),
                    "TemporaryDirectImport",
                    conversion.IsImported
                        ? "Converted"
                        : "IntentionallyExcluded",
                    conversion.IsImported
                        ? (conversion.IsPackedDetailNormal
                            ? "Source dimensions preserved; donor detail " +
                              "normal packed deterministically as HDRP " +
                              "R=0.5,G=Y,B=0.5,A=X; linear Default import; " +
                              "Read/Write off; CompressedHQ; shared across cells."
                            : conversion.IsPackedDetailAlbedo
                                ? "Source dimensions preserved; frozen donor " +
                                  "Diffuse Detail luminance packed deterministically " +
                                  "as HDRP R=luma,G=0.5,B=0.5,A=0.5; linear " +
                                  "Default import; Read/Write off; CompressedHQ; " +
                                  "shared across cells."
                                : "Source dimensions preserved; Read/Write off; " +
                                  "CompressedHQ; shared across cells.")
                        : "Donor reflection/night-gradient cubemap is not " +
                          "mapped because donor sky/reflection runtime is " +
                          "outside 06B2.");
            }

            WriteProjectText(
                WorldBaseline06B2Paths.MaterialTextureManifest,
                csv.ToString());
        }

        private static void WriteShaderMapping(
            DonorWorldMaterialTexturePlan plan)
        {
            var markdown = new StringBuilder();
            markdown.AppendLine("# Material shader mapping — 08A.1 candidate v002 remediation")
                .AppendLine()
                .AppendLine(
                    "Status: deterministic project-owned HDRP compatibility mapping.")
                .AppendLine(
                    "Compatibility policy: `" +
                    CompatibilityPolicyVersion + "`.")
                .AppendLine()
                .AppendLine(
                    "Donor `.shader` files are read only for identity/classification. " +
                    "They are never copied, compiled, or referenced by runtime content.")
                .AppendLine()
                .AppendLine(
                    "| Source shader identity | Source materials | HDRP mapping | Notes |")
                .AppendLine(
                    "|---|---:|---|---|");
            foreach (IGrouping<string, DonorWorldSourceMaterial> group in
                     plan.Materials.Values
                         .GroupBy(
                             material => material.SourceShaderIdentity,
                             StringComparer.Ordinal)
                         .OrderBy(
                             group => group.Key,
                             StringComparer.Ordinal))
            {
                string mappings = string.Join(
                    ", ",
                    group.GroupBy(
                            material =>
                                material.CompatibilityClass)
                        .OrderBy(entry => entry.Key)
                        .Select(entry =>
                            entry.Key + " (" + entry.Count() + ")"));
                markdown.Append("| ")
                    .Append(EscapeMarkdown(group.Key))
                    .Append(" | ")
                    .Append(group.Count().ToString(
                        CultureInfo.InvariantCulture))
                    .Append(" | ")
                    .Append(EscapeMarkdown(mappings))
                    .Append(" | Source tint/UV are retained except for the " +
                            "documented project-owned water RGB override; " +
                            "temporary PBR channels follow the bounded " +
                            "shader-aware compatibility policy. |")
                    .AppendLine();
            }
            markdown.AppendLine()
                .AppendLine("## Mapping policy")
                .AppendLine()
                .AppendLine(
                    "- Opaque surfaces -> `HDRP/Lit`.")
                .AppendLine(
                    "- Alpha-test/tree-wall/foliage -> `HDRP/Lit`, alpha clipping, reviewed double-sided intent.")
                .AppendLine(
                    "- The temporary Phase 1 world baseline is dielectric: donor `_Metallic` is not inherited until a material identity is explicitly reviewed as metal.")
                .AppendLine(
                    "- Smoothness is conservatively capped by source semantics: detail ground and disabled emissive `0`, foliage `0.04`, legacy diffuse `0.08`, generic `0.12`, water/transparent/specular/Standard `0.18`.")
                .AppendLine(
                    "- Transparent/glass -> bounded non-metallic `HDRP/Lit` transparent mode.")
                .AppendLine(
                    "- Screen/unlit source families -> `HDRP/Unlit`.")
                .AppendLine(
                    "- Static donor emission is disabled. Stateful lights/screens must later be enabled by project-owned gameplay presenters instead of frozen donor material variants.")
                .AppendLine(
                    "- Frozen road/ground `Legacy Shaders/Diffuse Detail` GUIDs -> deterministic HDRP Detail Map packing (`R=luminance`, neutral normal/smoothness channels), original detail UV transform, and SSR disabled.")
                .AppendLine(
                    "- HDRP double-sided mode uses Flip normals (`0`, constants `-1,-1,-1`) for reviewed foliage/tree-wall sources and None (`2`, constants `1,1,1`) otherwise; every generated material is finalized through `HDMaterial.ValidateMaterial`.")
                .AppendLine(
                    "- Water -> temporary project-owned transparent HDRP material with RGB `(0.08, 0.22, 0.28)` and preserved/clamped donor `_BaseColor.a`; donor shore foam is not misused as the full-surface base map, and donor water runtime is excluded.")
                .AppendLine(
                    "- Built-in material `10302` on two FinishPoles objects -> explicit orange diagnostic fallback.")
                .AppendLine()
                .AppendLine("## Known differences and review disposition")
                .AppendLine()
                .AppendLine(
                    "- AssetRipper shader text does not prove original Cull/Tags/blend implementation; double-sided/culling intent is a bounded shader/material-name heuristic. The representative v5.1 visual review was accepted on 2026-07-16 as temporary legacy visual debt, not production parity.")
                .AppendLine(
                    "- Donor detail normals and allowlisted legacy road/ground detail albedo are deterministically channel-packed into separate HDRP Detail Map variants. Specular/metallic legacy maps remain audit-only where semantics are ambiguous.")
                .AppendLine(
                    "- Alpha cutout, glass, tree walls, water, emission and UV slot order were included in the manual baseline review. Corrected water is `PASS / HumanAccepted`; remaining legacy artifacts are accepted temporary visual debt and still require production replacement.");
            WriteProjectText(
                WorldBaseline06B2Paths.MaterialShaderMapping,
                markdown.ToString());
        }

        private static void WriteVisualCompletenessReport(
            DonorWorldMaterialTextureAssets assets)
        {
            DonorWorldMaterialTexturePlan plan = assets.Plan;
            int cutout = plan.Materials.Values.Count(material =>
                material.CompatibilityClass ==
                DonorWorldCompatibilityClass.AlphaClipLit);
            int transparent = plan.Materials.Values.Count(material =>
                material.CompatibilityClass ==
                    DonorWorldCompatibilityClass.TransparentLit ||
                material.CompatibilityClass ==
                    DonorWorldCompatibilityClass.TransparentUnlit ||
                material.CompatibilityClass ==
                    DonorWorldCompatibilityClass.TemporaryWater);
            int emissive = plan.Materials.Values.Count(material =>
                material.CompatibilityClass ==
                DonorWorldCompatibilityClass.EmissiveLit);
            int detailNormalMaterials = plan.Materials.Values.Count(
                material =>
                    material.Textures.ContainsKey(
                        "_DetailNormalMap"));
            int detailOnlyMaterials = plan.Materials.Values.Count(
                material =>
                    material.Textures.ContainsKey(
                        "_DetailNormalMap") &&
                    !material.Textures.ContainsKey("_BumpMap") &&
                    !material.Textures.ContainsKey("_NormalMap"));
            int detailAlbedoMaterials = plan.Materials.Values.Count(
                material =>
                    TryGetExpectedDetailAlbedoTexture(
                        material,
                        out _));
            string manifestHash =
                DonorWorldBaselineManifest.ComputeFileSha256(
                    WorldBaselinePaths.ToAbsoluteProjectPath(
                        WorldBaseline06B2Paths.MaterialTextureManifest));
            var markdown = new StringBuilder();
            markdown.AppendLine(
                    "# Baseline visual completeness — 08A.1 candidate v002 remediation")
                .AppendLine()
                .AppendLine(
                    "Automated structural status: **PASS (candidate only)**.")
                .AppendLine(
                    "Human visual acceptance: **PENDING after deterministic regeneration**.")
                .AppendLine()
                .AppendLine(
                    "06B3 gate: **GO / accepted; milestone not started**.")
                .AppendLine()
                .AppendLine("## Automated closure")
                .AppendLine()
                .Append("- Generator: `1.1.0-")
                    .Append(DonorWorldMaterialTexturePlan.ConverterVersion)
                    .AppendLine("`.")
                .Append("- Renderers: ").Append(
                    plan.RendererEntries.Count).AppendLine(".")
                .Append("- Declared source material slots: ").Append(
                    DonorWorldMaterialTexturePlan
                        .ExpectedDeclaredMaterialSlotCount).AppendLine(".")
                .Append("- Resolved donor materials: ").Append(
                    plan.Materials.Count).AppendLine(".")
                .Append("- Built-in reviewed fallback renderers: ").Append(
                    DonorWorldMaterialTexturePlan
                        .ExpectedBuiltInFallbackRendererCount).AppendLine(".")
                .Append("- Referenced source images: ").Append(
                    plan.Textures.Values.Select(value => value.SourceGuid)
                        .Distinct(StringComparer.Ordinal).Count())
                    .AppendLine(" (0 missing).")
                .Append("- Imported role-specific texture variants: ").Append(
                    plan.Textures.Values.Count(value => value.IsImported))
                    .AppendLine(".")
                .Append("- Packed detail-normal variants: ").Append(
                    plan.Textures.Values.Count(value =>
                        value.Role ==
                        DonorWorldTextureRole.DetailNormalPacked))
                    .Append("; assigned materials: ")
                    .Append(detailNormalMaterials)
                    .Append(" (detail-only: ")
                .Append(detailOnlyMaterials)
                    .AppendLine(").")
                .Append("- Packed detail-albedo variants: ").Append(
                    plan.Textures.Values.Count(value =>
                        value.Role ==
                        DonorWorldTextureRole.DetailAlbedoPacked))
                    .Append("; assigned legacy road/ground materials: ")
                    .Append(detailAlbedoMaterials)
                    .AppendLine(".")
                .Append("- Material compatibility policy: `")
                    .Append(CompatibilityPolicyVersion)
                    .AppendLine("`; all outputs finalized by HDRP validation.")
                .Append("- Alpha-cutout materials: ").Append(cutout)
                    .AppendLine(".")
                .Append("- Transparent/water materials: ").Append(transparent)
                    .AppendLine(".")
                .Append("- Emissive materials: ").Append(emissive)
                    .AppendLine(".")
                .Append("- Presentation fingerprint: `")
                    .Append(plan.PresentationFingerprintSha256)
                    .AppendLine("`.")
                .AppendLine("- Material/texture manifest SHA-256:")
                .Append("  `").Append(manifestHash).AppendLine("`.")
                .AppendLine()
                .AppendLine("Water-fix closure:")
                .AppendLine()
                .AppendLine(
                    "- `TemporaryWater` alpha uses donor `_BaseColor.a`:")
                .AppendLine(
                    "  `Water4Adv_Lake = 0.2901961`, `Water4Simple = 0.5058824`.")
                .AppendLine(
                    "- Shore-foam texture is no longer used as the full-surface base map.")
                .AppendLine(
                    "- Presentation build: PASS,")
                .AppendLine(
                    "  `Logs/M06B2V51_PresentationBuild12_WaterFix.log`.")
                .AppendLine(
                    "- Full validator: PASS,")
                .AppendLine(
                    "  `Logs/M06B2V51_CellizationValidator09_WaterFix.log`.")
                .AppendLine(
                    "- Focused EditMode: `7/7 PASS`, `65.3441271 s`,")
                .AppendLine(
                    "  `TestResults/M06B2V51_EditMode05_WaterFix.xml`,")
                .AppendLine(
                    "  `Logs/M06B2V51_EditMode05_WaterFix.log`.")
                .AppendLine(
                    "- Focused PlayMode: `6/6 PASS`, `7.4557305 s`,")
                .AppendLine(
                    "  `TestResults/M06B2V51_PlayMode06_WaterFix.xml`.")
                .AppendLine(
                    "- Streaming/material performance PlayMode: `1/1 PASS`, `2.5581146 s`,")
                .AppendLine(
                    "  `TestResults/M06B2V51_PerformancePlayMode06_WaterFix.xml`.")
                .AppendLine(
                    "- Performance capture SHA-256:")
                .AppendLine(
                    "  `60868e5862413b59df0adad2dee998617145b96de2306494a7bbd6e60e3ba271`.")
                .AppendLine()
                .AppendLine("## Explicit exclusions / fallbacks")
                .AppendLine()
                .AppendLine(
                    "- One donor night-gradient reflection cubemap is recorded but intentionally excluded; donor sky/reflection/weather ownership is outside this milestone.")
                .AppendLine(
                    "- Built-in material `10302` has no exported definition and uses the obvious orange fallback on two FinishPoles renderers.")
                .AppendLine(
                    "- Donor lighting, lightmaps, reflection probes, post-processing, weather, audio and shader code are not imported.")
                .AppendLine(
                    "- Severe low-quality, stretched and banded terrain texture remains temporary")
                .AppendLine(
                    "  legacy presentation debt.")
                .AppendLine(
                    "- Proxy/tree-wall geometry and other donor material/texture artifacts remain")
                .AppendLine(
                    "  temporary visual debt. Final production textures and materials will be")
                .AppendLine(
                    "  reauthored.")
                .AppendLine()
                .AppendLine("## Required neutral-lighting review")
                .AppendLine()
                .AppendLine("| Area / contract | Status |")
                .AppendLine("|---|---|")
                .AppendLine(
                    "| Geometry and layout fidelity | Accepted for the temporary baseline |")
                .AppendLine(
                    "| Non-water legacy textures/materials | Accepted as documented temporary visual debt |")
                .AppendLine(
                    "| Terrain low quality/stretch/banding | Accepted as documented temporary visual debt |")
                .AppendLine(
                    "| Proxy/tree-wall and legacy material artifacts | Accepted as documented temporary visual debt |")
                .AppendLine(
                    "| Lake / shoreline visibility after water fix | Accepted / HumanAccepted 2026-07-16 |")
                .AppendLine(
                    "| Bridge traversal and cross-cell transitions | Accepted / HumanAccepted 2026-07-16; bridges walked and character relocated between cells without observed issues |")
                .AppendLine()
                .AppendLine(
                    "The corrected water, temporary visual baseline and bridge/cell-boundary review")
                .AppendLine(
                    "are accepted. The manual method used walking and cross-cell character relocation")
                .AppendLine(
                    "rather than a dedicated vehicle drive; automated high-speed preload validation")
                .AppendLine(
                    "passed. The 06B3 entry gate is **GO**, but 06B3 has not started.");
            WriteProjectText(
                WorldBaseline06B2Paths.VisualCompletenessReport,
                markdown.ToString());
        }

        private static void WriteTextureMemoryBaseline(
            DonorWorldMaterialTextureAssets assets)
        {
            DonorWorldTextureConversion[] uniqueSources =
                assets.Plan.Textures.Values
                    .GroupBy(
                        texture => texture.SourceGuid,
                        StringComparer.Ordinal)
                    .Select(group => group.First())
                    .ToArray();
            long uniqueSourceBytes =
                uniqueSources.Sum(texture => texture.SourceBytes);
            long generatedEncodedBytes = assets.Plan.Textures.Values
                .Where(texture => texture.IsImported)
                .Sum(texture =>
                    new FileInfo(
                        WorldBaselinePaths.ToAbsoluteProjectPath(
                            texture.GeneratedAssetPath)).Length);
            long importedRuntimeBytes =
                assets.ConvertedTextures.Values.Sum(
                    texture =>
                        Profiler.GetRuntimeMemorySizeLong(texture));
            long materialRuntimeBytes =
                assets.TexturedMaterials.Values.Sum(
                    material =>
                        Profiler.GetRuntimeMemorySizeLong(material)) +
                Profiler.GetRuntimeMemorySizeLong(
                    assets.UnsupportedMaterial);
            int generatedMaterialCount =
                assets.TexturedMaterials.Count + 1;
            int materialCopiesAvoided =
                DonorWorldMaterialTexturePlan
                    .ExpectedDeclaredMaterialSlotCount -
                generatedMaterialCount;
            int importedTextureReferenceCount =
                assets.Plan.Textures.Values
                    .Where(texture => texture.IsImported)
                    .Sum(texture => texture.ReferenceCount);
            int textureCopiesAvoided =
                importedTextureReferenceCount -
                assets.ConvertedTextures.Count;
            var markdown = new StringBuilder();
            markdown.AppendLine(
                    "# Texture memory baseline — 08A.1 candidate v002 remediation")
                .AppendLine()
                .AppendLine(
                    "Scope: deterministic imported-asset baseline in Unity Editor. " +
                    "Resident/streamed counts during traversal are captured by the PlayMode performance evidence.")
                .AppendLine()
                .AppendLine("| Metric | Value |")
                .AppendLine("|---|---:|")
                .Append("| Unique source images | ")
                    .Append(uniqueSources.Length).AppendLine(" |")
                .Append("| Role-specific conversion records | ")
                    .Append(assets.Plan.Textures.Count).AppendLine(" |")
                .Append("| Imported role variants | ")
                    .Append(assets.ConvertedTextures.Count).AppendLine(" |")
                .Append("| Declared material-slot references | ")
                    .Append(DonorWorldMaterialTexturePlan
                        .ExpectedDeclaredMaterialSlotCount)
                    .AppendLine(" |")
                .Append("| Shared generated materials (fallback included) | ")
                    .Append(generatedMaterialCount).AppendLine(" |")
                .Append("| Material copies avoided by sharing | ")
                    .Append(materialCopiesAvoided).AppendLine(" |")
                .Append("| Imported texture-property references | ")
                    .Append(importedTextureReferenceCount).AppendLine(" |")
                .Append("| Texture copies avoided by source+role sharing | ")
                    .Append(textureCopiesAvoided).AppendLine(" |")
                .Append("| Excluded cubemap variants | ")
                    .Append(assets.Plan.Textures.Values.Count(
                        texture => !texture.IsImported)).AppendLine(" |")
                .Append("| Unique source PNG bytes | ")
                    .Append(uniqueSourceBytes).AppendLine(" |")
                .Append("| Generated encoded bytes (role splits included) | ")
                    .Append(generatedEncodedBytes).AppendLine(" |")
                .Append("| Unity imported texture runtime-size estimate | ")
                    .Append(importedRuntimeBytes).AppendLine(" |")
                .Append("| Unity generated material runtime-size estimate | ")
                    .Append(materialRuntimeBytes).AppendLine(" |")
                .AppendLine()
                .AppendLine("## Import contract")
                .AppendLine()
                .AppendLine(
                    "- Source dimensions are preserved (maximum source size 8192).")
                .AppendLine(
                    "- World-space variants use mipmaps and texture streaming.")
                .AppendLine(
                    "- Read/Write is disabled.")
                .AppendLine(
                    "- Color variants use sRGB; normal/linear/packed-detail variants do not.")
                .AppendLine(
                    "- Donor detail normals are channel-packed as `R=0.5, G=Y, B=0.5, A=X` for HDRP Detail Map semantics; source dimensions are unchanged.")
                .AppendLine(
                    "- Allowlisted frozen `Legacy Shaders/Diffuse Detail` road/ground textures are packed as `R=perceptual luminance, G=0.5, B=0.5, A=0.5`; the original detail UV transform is preserved and the packed variant is imported as linear data.")
                .AppendLine(
                    "- Standalone import uses Unity `CompressedHQ`; signage/alpha remains a manual readability check.")
                .AppendLine(
                    "- Source wrap/filter/aniso intent is carried into the project importer.")
                .AppendLine()
                .AppendLine("## Limitations")
                .AppendLine()
                .AppendLine(
                    "- `Profiler.GetRuntimeMemorySizeLong` in the Editor is an estimate, not a standalone GPU residency measurement.")
                .AppendLine(
                    "- Texture streaming residency and plateau after repeated travel must be read from `M06B2_STREAMING_PERFORMANCE.json`.")
                .AppendLine(
                    "- No source payload hash duplicates exist among the 265 images; deduplication savings are `2744 -> 293` shared materials (`2451` copies avoided) and `" +
                    importedTextureReferenceCount + " -> " +
                    assets.ConvertedTextures.Count +
                    "` source+role texture variants (`" +
                    textureCopiesAvoided + "` copies avoided).");
            WriteProjectText(
                WorldBaseline06B2Paths.TextureMemoryBaseline,
                markdown.ToString());
        }

        private static void WritePresentationSourceManifest(
            DonorWorldMaterialTextureAssets assets)
        {
            string manifestHash =
                DonorWorldBaselineManifest.ComputeFileSha256(
                    WorldBaselinePaths.ToAbsoluteProjectPath(
                        WorldBaseline06B2Paths.MaterialTextureManifest));
            var data = new PresentationManifestData
            {
                schemaVersion = 2,
                generatorVersion =
                    DonorWorldMaterialTexturePlan.ConverterVersion,
                materialCompatibilityPolicyVersion =
                    CompatibilityPolicyVersion,
                sourceRevisionId =
                    WorldBaselinePaths.SourceRevisionId,
                sourceSceneSha256 =
                    WorldBaselinePaths.SourceSceneSha256,
                rendererCount = assets.Plan.RendererEntries.Count,
                declaredMaterialSlotCount =
                    DonorWorldMaterialTexturePlan
                        .ExpectedDeclaredMaterialSlotCount,
                resolvedMaterialCount =
                    assets.Plan.Materials.Count,
                builtInFallbackRendererCount =
                    DonorWorldMaterialTexturePlan
                        .ExpectedBuiltInFallbackRendererCount,
                sourceTextureCount =
                    assets.Plan.Textures.Values
                        .Select(value => value.SourceGuid)
                        .Distinct(StringComparer.Ordinal)
                        .Count(),
                importedTextureVariantCount =
                    assets.ConvertedTextures.Count,
                excludedTextureVariantCount =
                    assets.Plan.Textures.Values.Count(
                        value => !value.IsImported),
                packedDetailNormalVariantCount =
                    assets.Plan.Textures.Values.Count(
                        value => value.Role ==
                            DonorWorldTextureRole.DetailNormalPacked),
                packedDetailAlbedoVariantCount =
                    assets.Plan.Textures.Values.Count(
                        value => value.Role ==
                            DonorWorldTextureRole.DetailAlbedoPacked),
                hdrpMaterialValidationRequired = true,
                presentationFingerprintSha256 =
                    assets.Plan.PresentationFingerprintSha256,
                materialTextureManifestSha256 = manifestHash,
                classification = "TemporaryDirectImport",
                activeMode = "LegacyTextured",
                diagnosticMode = "LegacyDiagnostic",
                prototypeVisualMode = "PrototypeHidden"
            };
            WriteProjectText(
                WorldBaseline06B2Paths.PresentationSourceManifest,
                JsonUtility.ToJson(data, prettyPrint: true) +
                Environment.NewLine);
        }

        private static void AppendCsvRow(
            StringBuilder builder,
            params string[] values)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }
                string value = values[index] ?? string.Empty;
                bool quote =
                    value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
                if (quote)
                {
                    builder.Append('"')
                        .Append(value.Replace("\"", "\"\""))
                        .Append('"');
                }
                else
                {
                    builder.Append(value);
                }
            }
            builder.AppendLine();
        }

        private static string EscapeMarkdown(string value) =>
            value.Replace("|", "\\|");

        private static void WriteProjectText(
            string assetPath,
            string contents)
        {
            string absolutePath =
                WorldBaselinePaths.ToAbsoluteProjectPath(assetPath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(absolutePath) ??
                throw new InvalidOperationException(
                    "Output path has no directory."));
            File.WriteAllText(
                absolutePath,
                contents,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
        }

        [Serializable]
        private sealed class PresentationManifestData
        {
            public int schemaVersion;
            public string generatorVersion = string.Empty;
            public string materialCompatibilityPolicyVersion = string.Empty;
            public string sourceRevisionId = string.Empty;
            public string sourceSceneSha256 = string.Empty;
            public int rendererCount;
            public int declaredMaterialSlotCount;
            public int resolvedMaterialCount;
            public int builtInFallbackRendererCount;
            public int sourceTextureCount;
            public int importedTextureVariantCount;
            public int excludedTextureVariantCount;
            public int packedDetailNormalVariantCount;
            public int packedDetailAlbedoVariantCount;
            public bool hdrpMaterialValidationRequired;
            public string presentationFingerprintSha256 = string.Empty;
            public string materialTextureManifestSha256 = string.Empty;
            public string classification = string.Empty;
            public string activeMode = string.Empty;
            public string diagnosticMode = string.Empty;
            public string prototypeVisualMode = string.Empty;
        }
    }
}
