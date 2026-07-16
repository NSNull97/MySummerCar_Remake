using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MSC.Editor.WorldTransfer;
using UnityEngine;

namespace MSC.Editor.WorldBaseline
{
    public enum DonorWorldCompatibilityClass
    {
        OpaqueLit = 0,
        AlphaClipLit = 1,
        TransparentLit = 2,
        Unlit = 3,
        EmissiveLit = 4,
        TemporaryWater = 5,
        Unsupported = 6,
        TransparentUnlit = 7
    }

    public enum DonorWorldTextureRole
    {
        Color = 0,
        Normal = 1,
        Linear = 2,
        CubemapExcluded = 3,
        DetailNormalPacked = 4
    }

    public sealed class DonorWorldTextureTransform
    {
        public DonorWorldTextureTransform(
            Vector2 scale,
            Vector2 offset)
        {
            Scale = scale;
            Offset = offset;
        }

        public Vector2 Scale { get; }
        public Vector2 Offset { get; }
    }

    public sealed class DonorWorldTextureEnvironment
    {
        public DonorWorldTextureEnvironment(
            string propertyName,
            string textureGuid,
            Vector2 scale,
            Vector2 offset)
        {
            PropertyName = propertyName;
            TextureGuid = textureGuid;
            Scale = scale;
            Offset = offset;
        }

        public string PropertyName { get; }
        public string TextureGuid { get; }
        public Vector2 Scale { get; }
        public Vector2 Offset { get; }
    }

    public sealed class DonorWorldSourceMaterial
    {
        public DonorWorldSourceMaterial(
            string sourceGuid,
            string sourceRelativePath,
            string sourceAbsolutePath,
            string sourceSha256,
            string materialName,
            string sourceShaderGuid,
            string sourceShaderIdentity,
            string sourceShaderRelativePath,
            string sourceShaderSha256,
            string shaderKeywords,
            int customRenderQueue,
            IReadOnlyDictionary<string, DonorWorldTextureEnvironment>
                textures,
            IReadOnlyDictionary<string, DonorWorldTextureTransform>
                textureTransforms,
            IReadOnlyDictionary<string, float> floats,
            IReadOnlyDictionary<string, Color> colors,
            DonorWorldCompatibilityClass compatibilityClass,
            bool doubleSided)
        {
            SourceGuid = sourceGuid;
            SourceRelativePath = sourceRelativePath;
            SourceAbsolutePath = sourceAbsolutePath;
            SourceSha256 = sourceSha256;
            MaterialName = materialName;
            SourceShaderGuid = sourceShaderGuid;
            SourceShaderIdentity = sourceShaderIdentity;
            SourceShaderRelativePath = sourceShaderRelativePath;
            SourceShaderSha256 = sourceShaderSha256;
            ShaderKeywords = shaderKeywords;
            CustomRenderQueue = customRenderQueue;
            Textures = textures;
            TextureTransforms = textureTransforms;
            Floats = floats;
            Colors = colors;
            CompatibilityClass = compatibilityClass;
            DoubleSided = doubleSided;
        }

        public string SourceGuid { get; }
        public string SourceRelativePath { get; }
        public string SourceAbsolutePath { get; }
        public string SourceSha256 { get; }
        public string MaterialName { get; }
        public string SourceShaderGuid { get; }
        public string SourceShaderIdentity { get; }
        public string SourceShaderRelativePath { get; }
        public string SourceShaderSha256 { get; }
        public string ShaderKeywords { get; }
        public int CustomRenderQueue { get; }
        public IReadOnlyDictionary<string, DonorWorldTextureEnvironment>
            Textures { get; }
        public IReadOnlyDictionary<string, DonorWorldTextureTransform>
            TextureTransforms { get; }
        public IReadOnlyDictionary<string, float> Floats { get; }
        public IReadOnlyDictionary<string, Color> Colors { get; }
        public DonorWorldCompatibilityClass CompatibilityClass { get; }
        public bool DoubleSided { get; }

        public bool TryGetTexture(
            out DonorWorldTextureEnvironment texture,
            params string[] propertyNames)
        {
            foreach (string propertyName in propertyNames)
            {
                if (Textures.TryGetValue(propertyName, out texture))
                {
                    return true;
                }
            }

            texture = null;
            return false;
        }

        public bool TryGetTextureTransform(
            string propertyName,
            out DonorWorldTextureTransform transform) =>
            TextureTransforms.TryGetValue(propertyName, out transform);

        public float GetFloat(string propertyName, float fallback) =>
            Floats.TryGetValue(propertyName, out float value)
                ? value
                : fallback;

        public Color GetColor(string propertyName, Color fallback) =>
            Colors.TryGetValue(propertyName, out Color value)
                ? value
                : fallback;
    }

    public sealed class DonorWorldTextureConversion
    {
        public DonorWorldTextureConversion(
            string sourceGuid,
            DonorWorldTextureRole role,
            string sourceRelativePath,
            string sourceAbsolutePath,
            string sourceSha256,
            string sourceMetaSha256,
            long sourceBytes,
            int sourceWrapMode,
            int sourceFilterMode,
            int sourceAnisoLevel,
            IReadOnlyList<string> sourceProperties,
            int referenceCount,
            string conversionFingerprintSha256)
        {
            SourceGuid = sourceGuid;
            Role = role;
            SourceRelativePath = sourceRelativePath;
            SourceAbsolutePath = sourceAbsolutePath;
            SourceSha256 = sourceSha256;
            SourceMetaSha256 = sourceMetaSha256;
            SourceBytes = sourceBytes;
            SourceWrapMode = sourceWrapMode;
            SourceFilterMode = sourceFilterMode;
            SourceAnisoLevel = sourceAnisoLevel;
            SourceProperties = sourceProperties;
            ReferenceCount = referenceCount;
            ConversionFingerprintSha256 =
                conversionFingerprintSha256;
        }

        public string SourceGuid { get; }
        public DonorWorldTextureRole Role { get; }
        public string SourceRelativePath { get; }
        public string SourceAbsolutePath { get; }
        public string SourceSha256 { get; }
        public string SourceMetaSha256 { get; }
        public long SourceBytes { get; }
        public int SourceWrapMode { get; }
        public int SourceFilterMode { get; }
        public int SourceAnisoLevel { get; }
        public IReadOnlyList<string> SourceProperties { get; }
        public int ReferenceCount { get; }
        public string ConversionFingerprintSha256 { get; }
        public string Key => BuildKey(SourceGuid, Role);
        public bool IsImported =>
            Role != DonorWorldTextureRole.CubemapExcluded;
        public string GeneratedAssetPath =>
            IsImported
                ? WorldBaselinePaths.ConvertedTexture(
                    SourceGuid,
                    Role.ToString())
                : string.Empty;
        public bool SRgb =>
            Role == DonorWorldTextureRole.Color;
        public bool IsNormalMap =>
            Role == DonorWorldTextureRole.Normal;
        public bool IsPackedDetailNormal =>
            Role == DonorWorldTextureRole.DetailNormalPacked;

        public static string BuildKey(
            string sourceGuid,
            DonorWorldTextureRole role) =>
            sourceGuid + "|" + role;
    }

    public sealed class DonorWorldMaterialTexturePlan
    {
        public const string ConverterVersion = "06B2-v5.1.5";
        public const string BuiltInFallbackGuid =
            "0000000000000000f000000000000000";
        public const int ExpectedRendererCount = 2605;
        public const int ExpectedDeclaredMaterialSlotCount = 2744;
        public const int ExpectedUniqueMaterialGuidCount = 293;
        public const int ExpectedResolvedMaterialCount = 292;
        public const int ExpectedTextureSourceCount = 265;
        public const int ExpectedTextureConversionCount = 269;
        public const int ExpectedImportedTextureConversionCount = 268;
        public const int ExpectedBuiltInFallbackRendererCount = 2;

        private const string ExtractedAssetsRelativePath =
            "assetripper-unity-project/ExportedProject/Assets";

        private DonorWorldMaterialTexturePlan(
            IReadOnlyList<WorldBaselineSanitationEntry> rendererEntries,
            IReadOnlyDictionary<string, DonorWorldSourceMaterial> materials,
            IReadOnlyDictionary<string, DonorWorldTextureConversion>
                textures,
            IReadOnlyDictionary<string, int> materialReferenceCounts,
            string presentationFingerprintSha256)
        {
            RendererEntries = rendererEntries;
            Materials = materials;
            Textures = textures;
            MaterialReferenceCounts = materialReferenceCounts;
            PresentationFingerprintSha256 =
                presentationFingerprintSha256;
        }

        public IReadOnlyList<WorldBaselineSanitationEntry>
            RendererEntries { get; }
        public IReadOnlyDictionary<string, DonorWorldSourceMaterial>
            Materials { get; }
        public IReadOnlyDictionary<string, DonorWorldTextureConversion>
            Textures { get; }
        public IReadOnlyDictionary<string, int>
            MaterialReferenceCounts { get; }
        public string PresentationFingerprintSha256 { get; }

        public DonorWorldSourceMaterial GetMaterial(string sourceGuid)
        {
            if (!Materials.TryGetValue(
                    sourceGuid,
                    out DonorWorldSourceMaterial material))
            {
                throw new KeyNotFoundException(
                    "Resolved donor material is absent from the plan: " +
                    sourceGuid);
            }

            return material;
        }

        public DonorWorldTextureConversion GetTexture(
            string sourceGuid,
            DonorWorldTextureRole role)
        {
            string key = DonorWorldTextureConversion.BuildKey(
                sourceGuid,
                role);
            if (!Textures.TryGetValue(
                    key,
                    out DonorWorldTextureConversion texture))
            {
                throw new KeyNotFoundException(
                    "Resolved donor texture conversion is absent from " +
                    "the plan: " + key);
            }

            return texture;
        }

        public static string[] ResolveSourceMaterialSlots(
            WorldBaselineSanitationEntry entry,
            int subMeshCount)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            string[] source = entry.SourceMaterialGuids;
            int targetCount = Mathf.Max(1, subMeshCount);
            if (source.Length == targetCount)
            {
                return (string[])source.Clone();
            }
            if (source.Length == 1 && targetCount == 2)
            {
                return new[] { source[0], source[0] };
            }

            throw new InvalidDataException(
                "Source material-slot count does not match sanitized mesh " +
                $"submeshes for {entry.Placement.StableId}: " +
                $"source={source.Length}, submeshes={targetCount}.");
        }

        public static DonorWorldMaterialTexturePlan Load()
        {
            WorldBaselineSanitationEntry[] rendererEntries =
                WorldBaselineSanitationPlan.Load()
                    .Where(entry => entry.IncludeRenderer)
                    .OrderBy(
                        entry => entry.Placement.StableId,
                        StringComparer.Ordinal)
                    .ToArray();
            int declaredSlots = rendererEntries.Sum(entry =>
                entry.SourceMaterialGuids.Length);
            string[] sourceMaterialGuids = rendererEntries
                .SelectMany(entry => entry.SourceMaterialGuids)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            int fallbackRendererCount = rendererEntries.Count(entry =>
                entry.SourceMaterialGuids.Contains(
                    BuiltInFallbackGuid,
                    StringComparer.Ordinal));
            if (rendererEntries.Length != ExpectedRendererCount ||
                declaredSlots != ExpectedDeclaredMaterialSlotCount ||
                sourceMaterialGuids.Length !=
                    ExpectedUniqueMaterialGuidCount ||
                fallbackRendererCount !=
                    ExpectedBuiltInFallbackRendererCount)
            {
                throw new InvalidDataException(
                    "Frozen renderer/material closure drifted: " +
                    $"renderers={rendererEntries.Length}, " +
                    $"slots={declaredSlots}, " +
                    $"materialGuids={sourceMaterialGuids.Length}, " +
                    $"fallbackRenderers={fallbackRendererCount}.");
            }

            string sourceAssetsRoot = ResolveSourceAssetsRoot();
            IReadOnlyDictionary<string, SourceAssetRecord> assetMap =
                ScanSourceAssetMap(sourceAssetsRoot);
            IReadOnlyDictionary<string, SourceShaderRecord> shaderMap =
                ScanSourceShaderMap(sourceAssetsRoot, assetMap);

            var materials =
                new Dictionary<string, DonorWorldSourceMaterial>(
                    StringComparer.Ordinal);
            foreach (string materialGuid in sourceMaterialGuids)
            {
                if (string.Equals(
                        materialGuid,
                        BuiltInFallbackGuid,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!assetMap.TryGetValue(
                        materialGuid,
                        out SourceAssetRecord source) ||
                    !source.AbsolutePath.EndsWith(
                        ".mat",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new FileNotFoundException(
                        "Referenced frozen donor material is missing: " +
                        materialGuid);
                }

                materials.Add(
                    materialGuid,
                    ParseMaterial(
                        materialGuid,
                        source,
                        shaderMap));
            }

            if (materials.Count != ExpectedResolvedMaterialCount)
            {
                throw new InvalidDataException(
                    "Resolved donor material count drifted: " +
                    materials.Count + ".");
            }

            var textureUses =
                new Dictionary<string, TextureUseAccumulator>(
                    StringComparer.Ordinal);
            foreach (DonorWorldSourceMaterial material in
                     materials.Values)
            {
                foreach (DonorWorldTextureEnvironment texture in
                         material.Textures.Values)
                {
                    DonorWorldTextureRole role =
                        ClassifyTextureRole(texture.PropertyName);
                    string key =
                        DonorWorldTextureConversion.BuildKey(
                            texture.TextureGuid,
                            role);
                    if (!textureUses.TryGetValue(
                            key,
                            out TextureUseAccumulator accumulator))
                    {
                        accumulator = new TextureUseAccumulator(
                            texture.TextureGuid,
                            role);
                        textureUses.Add(key, accumulator);
                    }

                    accumulator.Add(texture.PropertyName);
                }
            }

            var textures =
                new Dictionary<string, DonorWorldTextureConversion>(
                    StringComparer.Ordinal);
            foreach (TextureUseAccumulator use in textureUses.Values
                         .OrderBy(
                             value => value.SourceGuid,
                             StringComparer.Ordinal)
                         .ThenBy(value => value.Role))
            {
                if (!assetMap.TryGetValue(
                        use.SourceGuid,
                        out SourceAssetRecord source) ||
                    !IsSupportedTextureSource(source.AbsolutePath))
                {
                    throw new FileNotFoundException(
                        "Referenced frozen donor texture is missing: " +
                        use.SourceGuid);
                }

                string metaPath = source.AbsolutePath + ".meta";
                SourceTextureImporterRecord importer =
                    ParseTextureImporter(metaPath);
                string sourceHash =
                    DonorWorldBaselineManifest.ComputeFileSha256(
                        source.AbsolutePath);
                string metaHash =
                    DonorWorldBaselineManifest.ComputeFileSha256(
                        metaPath);
                string fingerprint =
                    DonorWorldBaselineManifest.Sha256Text(
                        ConverterVersion + "|" +
                        use.SourceGuid + "|" +
                        sourceHash + "|" +
                        metaHash + "|" +
                        use.Role + "|" +
                        importer.WrapMode + "|" +
                        importer.FilterMode + "|" +
                        importer.AnisoLevel + "|" +
                        "mipmaps=1|readable=0|streaming=1|" +
                        "compression=CompressedHQ");
                var conversion =
                    new DonorWorldTextureConversion(
                        use.SourceGuid,
                        use.Role,
                        source.RelativePath,
                        source.AbsolutePath,
                        sourceHash,
                        metaHash,
                        new FileInfo(source.AbsolutePath).Length,
                        importer.WrapMode,
                        importer.FilterMode,
                        importer.AnisoLevel,
                        use.Properties
                            .OrderBy(
                                value => value,
                                StringComparer.Ordinal)
                            .ToArray(),
                        use.ReferenceCount,
                        fingerprint);
                textures.Add(conversion.Key, conversion);
            }

            int sourceTextureCount = textures.Values
                .Select(texture => texture.SourceGuid)
                .Distinct(StringComparer.Ordinal)
                .Count();
            int importedTextureCount = textures.Values.Count(
                texture => texture.IsImported);
            if (sourceTextureCount != ExpectedTextureSourceCount ||
                textures.Count != ExpectedTextureConversionCount ||
                importedTextureCount !=
                    ExpectedImportedTextureConversionCount)
            {
                throw new InvalidDataException(
                    "Frozen donor texture closure drifted: " +
                    $"sources={sourceTextureCount}, " +
                    $"conversions={textures.Count}, " +
                    $"imported={importedTextureCount}.");
            }

            Dictionary<string, int> materialReferenceCounts =
                rendererEntries
                    .SelectMany(entry => entry.SourceMaterialGuids)
                    .GroupBy(value => value, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Count(),
                        StringComparer.Ordinal);
            string presentationFingerprint =
                ComputePresentationFingerprint(
                    rendererEntries,
                    materials,
                    textures);
            return new DonorWorldMaterialTexturePlan(
                rendererEntries,
                materials,
                textures,
                materialReferenceCounts,
                presentationFingerprint);
        }

        public static DonorWorldTextureRole ClassifyTextureRole(
            string propertyName)
        {
            if (string.Equals(
                    propertyName,
                    "_DetailNormalMap",
                    StringComparison.Ordinal))
            {
                return DonorWorldTextureRole.DetailNormalPacked;
            }
            if (propertyName.IndexOf(
                    "Cube",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DonorWorldTextureRole.CubemapExcluded;
            }
            if (propertyName.IndexOf(
                    "Bump",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                propertyName.IndexOf(
                    "Normal",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DonorWorldTextureRole.Normal;
            }
            if (propertyName.IndexOf(
                    "Spec",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                propertyName.IndexOf(
                    "Metallic",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                propertyName.IndexOf(
                    "Occlusion",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                propertyName.IndexOf(
                    "Mask",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                propertyName.IndexOf(
                    "Parallax",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DonorWorldTextureRole.Linear;
            }

            return DonorWorldTextureRole.Color;
        }

        private static string ResolveSourceAssetsRoot()
        {
            string rawRoot =
                WorldTransferEditorConfiguration.Load().RawExtractionPath;
            string root = Path.GetFullPath(Path.Combine(
                rawRoot,
                ExtractedAssetsRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar)));
            string normalizedRawRoot =
                Path.GetFullPath(rawRoot)
                    .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!root.StartsWith(
                    normalizedRawRoot,
                    StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(root))
            {
                throw new DirectoryNotFoundException(
                    "Frozen AssetRipper Assets root is missing or escapes " +
                    "the configured staging root: " + root);
            }

            return root;
        }

        private static IReadOnlyDictionary<string, SourceAssetRecord>
            ScanSourceAssetMap(string sourceAssetsRoot)
        {
            var result =
                new Dictionary<string, SourceAssetRecord>(
                    StringComparer.Ordinal);
            foreach (string metaPath in Directory.EnumerateFiles(
                         sourceAssetsRoot,
                         "*.meta",
                         SearchOption.AllDirectories))
            {
                string assetPath = metaPath.Substring(
                    0,
                    metaPath.Length - ".meta".Length);
                if (!File.Exists(assetPath))
                {
                    continue;
                }

                string guid = ReadMetaGuid(metaPath);
                if (guid.Length == 0)
                {
                    continue;
                }
                if (!result.TryAdd(
                        guid,
                        new SourceAssetRecord(
                            ToSourceRelativePath(
                                sourceAssetsRoot,
                                assetPath),
                            assetPath)))
                {
                    throw new InvalidDataException(
                        "Frozen AssetRipper export contains duplicate GUID " +
                        guid + ".");
                }
            }

            return result;
        }

        private static IReadOnlyDictionary<string, SourceShaderRecord>
            ScanSourceShaderMap(
                string sourceAssetsRoot,
                IReadOnlyDictionary<string, SourceAssetRecord> assetMap)
        {
            var result =
                new Dictionary<string, SourceShaderRecord>(
                    StringComparer.Ordinal);
            foreach (KeyValuePair<string, SourceAssetRecord> pair in
                     assetMap)
            {
                if (!pair.Value.AbsolutePath.EndsWith(
                        ".shader",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string identity = Path.GetFileNameWithoutExtension(
                    pair.Value.AbsolutePath);
                foreach (string line in File.ReadLines(
                             pair.Value.AbsolutePath))
                {
                    Match match = Regex.Match(
                        line,
                        "^\\s*Shader\\s+\"([^\"]+)\"");
                    if (match.Success)
                    {
                        identity = match.Groups[1].Value;
                        break;
                    }
                }

                result.Add(
                    pair.Key,
                    new SourceShaderRecord(
                        identity,
                        pair.Value.RelativePath,
                        DonorWorldBaselineManifest.ComputeFileSha256(
                            pair.Value.AbsolutePath)));
            }

            return result;
        }

        private static DonorWorldSourceMaterial ParseMaterial(
            string materialGuid,
            SourceAssetRecord source,
            IReadOnlyDictionary<string, SourceShaderRecord> shaderMap)
        {
            string materialName =
                Path.GetFileNameWithoutExtension(source.AbsolutePath);
            string shaderGuid = string.Empty;
            string shaderIdentity = "Unknown";
            string shaderRelativePath = string.Empty;
            string shaderSha256 = string.Empty;
            string shaderKeywords = string.Empty;
            int customRenderQueue = -1;
            var textures =
                new Dictionary<string, DonorWorldTextureEnvironment>(
                    StringComparer.Ordinal);
            var textureTransforms =
                new Dictionary<string, DonorWorldTextureTransform>(
                    StringComparer.Ordinal);
            var floats =
                new Dictionary<string, float>(StringComparer.Ordinal);
            var colors =
                new Dictionary<string, Color>(StringComparer.Ordinal);

            SavedPropertySection section =
                SavedPropertySection.None;
            string propertyName = string.Empty;
            string textureGuid = string.Empty;
            Vector2 textureScale = Vector2.one;
            Vector2 textureOffset = Vector2.zero;

            void FlushTexture()
            {
                if (section == SavedPropertySection.Textures &&
                    propertyName.Length > 0)
                {
                    textureTransforms[propertyName] =
                        new DonorWorldTextureTransform(
                            textureScale,
                            textureOffset);
                    if (textureGuid.Length > 0)
                    {
                        textures[propertyName] =
                            new DonorWorldTextureEnvironment(
                                propertyName,
                                textureGuid,
                                textureScale,
                                textureOffset);
                    }
                }

                propertyName = string.Empty;
                textureGuid = string.Empty;
                textureScale = Vector2.one;
                textureOffset = Vector2.zero;
            }

            foreach (string line in File.ReadLines(source.AbsolutePath))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(
                        "m_Name:",
                        StringComparison.Ordinal))
                {
                    materialName = trimmed.Substring(
                        "m_Name:".Length).Trim();
                    continue;
                }
                if (trimmed.StartsWith(
                        "m_Shader:",
                        StringComparison.Ordinal))
                {
                    Match match = Regex.Match(
                        trimmed,
                        "fileID:\\s*([^,}]+),\\s*guid:\\s*([^,}]+)");
                    if (match.Success)
                    {
                        string fileId =
                            match.Groups[1].Value.Trim();
                        shaderGuid =
                            match.Groups[2].Value.Trim();
                        if (string.Equals(
                                shaderGuid,
                                "0000000000000000f000000000000000",
                                StringComparison.Ordinal))
                        {
                            shaderIdentity = "BuiltIn/" + fileId;
                        }
                        else if (shaderMap.TryGetValue(
                                     shaderGuid,
                                     out SourceShaderRecord shader))
                        {
                            shaderIdentity = shader.Identity;
                            shaderRelativePath =
                                shader.RelativePath;
                            shaderSha256 = shader.Sha256;
                        }
                        else
                        {
                            shaderIdentity =
                                "ExternalShader/" + shaderGuid;
                        }
                    }
                    continue;
                }
                if (trimmed.StartsWith(
                        "m_ShaderKeywords:",
                        StringComparison.Ordinal))
                {
                    shaderKeywords = trimmed.Substring(
                        "m_ShaderKeywords:".Length).Trim();
                    continue;
                }
                if (trimmed.StartsWith(
                        "m_CustomRenderQueue:",
                        StringComparison.Ordinal))
                {
                    int.TryParse(
                        trimmed.Substring(
                            "m_CustomRenderQueue:".Length).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out customRenderQueue);
                    continue;
                }

                if (trimmed == "m_TexEnvs:")
                {
                    FlushTexture();
                    section = SavedPropertySection.Textures;
                    continue;
                }
                if (trimmed == "m_Floats:")
                {
                    FlushTexture();
                    section = SavedPropertySection.Floats;
                    propertyName = string.Empty;
                    continue;
                }
                if (trimmed == "m_Colors:")
                {
                    FlushTexture();
                    section = SavedPropertySection.Colors;
                    propertyName = string.Empty;
                    continue;
                }
                if (trimmed == "data:")
                {
                    FlushTexture();
                    continue;
                }
                if (trimmed.StartsWith(
                        "name:",
                        StringComparison.Ordinal))
                {
                    propertyName =
                        trimmed.Substring("name:".Length).Trim();
                    continue;
                }

                if (section == SavedPropertySection.Textures)
                {
                    if (trimmed.StartsWith(
                            "m_Texture:",
                            StringComparison.Ordinal))
                    {
                        Match match = Regex.Match(
                            trimmed,
                            "fileID:\\s*([^,}]+)(?:,\\s*guid:\\s*([^,}]+))?");
                        if (match.Success &&
                            !string.Equals(
                                match.Groups[1].Value.Trim(),
                                "0",
                                StringComparison.Ordinal))
                        {
                            textureGuid =
                                match.Groups[2].Value.Trim();
                        }
                    }
                    else if (trimmed.StartsWith(
                                 "m_Scale:",
                                 StringComparison.Ordinal))
                    {
                        textureScale = ParseVector2(
                            trimmed,
                            textureScale);
                    }
                    else if (trimmed.StartsWith(
                                 "m_Offset:",
                                 StringComparison.Ordinal))
                    {
                        textureOffset = ParseVector2(
                            trimmed,
                            textureOffset);
                    }
                }
                else if (section == SavedPropertySection.Floats &&
                         propertyName.Length > 0 &&
                         trimmed.StartsWith(
                             "second:",
                             StringComparison.Ordinal))
                {
                    if (float.TryParse(
                            trimmed.Substring("second:".Length).Trim(),
                            NumberStyles.Float,
                            CultureInfo.InvariantCulture,
                            out float value))
                    {
                        floats[propertyName] = value;
                    }
                    propertyName = string.Empty;
                }
                else if (section == SavedPropertySection.Colors &&
                         propertyName.Length > 0 &&
                         trimmed.StartsWith(
                             "second:",
                             StringComparison.Ordinal))
                {
                    if (TryParseColor(trimmed, out Color color))
                    {
                        colors[propertyName] = color;
                    }
                    propertyName = string.Empty;
                }
            }
            FlushTexture();

            DonorWorldCompatibilityClass compatibilityClass =
                ClassifyMaterial(
                    materialName,
                    shaderIdentity,
                    shaderKeywords,
                    floats,
                    colors,
                    textures);
            bool doubleSided =
                shaderIdentity.IndexOf(
                    "Leaves",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderIdentity.IndexOf(
                    "DOUBLE",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                materialName.IndexOf(
                    "treewall",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                materialName.IndexOf(
                    "vegetation",
                    StringComparison.OrdinalIgnoreCase) >= 0;

            return new DonorWorldSourceMaterial(
                materialGuid,
                source.RelativePath,
                source.AbsolutePath,
                DonorWorldBaselineManifest.ComputeFileSha256(
                    source.AbsolutePath),
                materialName,
                shaderGuid,
                shaderIdentity,
                shaderRelativePath,
                shaderSha256,
                shaderKeywords,
                customRenderQueue,
                textures,
                textureTransforms,
                floats,
                colors,
                compatibilityClass,
                doubleSided);
        }

        private static DonorWorldCompatibilityClass ClassifyMaterial(
            string materialName,
            string shaderIdentity,
            string shaderKeywords,
            IReadOnlyDictionary<string, float> floats,
            IReadOnlyDictionary<string, Color> colors,
            IReadOnlyDictionary<string, DonorWorldTextureEnvironment>
                textures)
        {
            float mode =
                floats.TryGetValue("_Mode", out float sourceMode)
                    ? sourceMode
                    : -1f;
            if (shaderIdentity.IndexOf(
                    "Water",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DonorWorldCompatibilityClass.TemporaryWater;
            }

            bool unlit =
                shaderIdentity.StartsWith(
                    "BuiltIn/10752",
                    StringComparison.Ordinal) ||
                shaderIdentity.StartsWith(
                    "BuiltIn/10755",
                    StringComparison.Ordinal) ||
                shaderIdentity.IndexOf(
                    "Unlit",
                    StringComparison.OrdinalIgnoreCase) >= 0;
            if (mode >= 1.5f ||
                shaderKeywords.IndexOf(
                    "_ALPHABLEND_ON",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderKeywords.IndexOf(
                    "_ALPHAPREMULTIPLY_ON",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderIdentity.IndexOf(
                    "Transparent",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return unlit
                    ? DonorWorldCompatibilityClass.TransparentUnlit
                    : DonorWorldCompatibilityClass.TransparentLit;
            }
            if (Mathf.Abs(mode - 1f) < 0.01f ||
                shaderKeywords.IndexOf(
                    "_ALPHATEST_ON",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderIdentity.IndexOf(
                    "Leaves",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderIdentity.IndexOf(
                    "AlphaTest",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return DonorWorldCompatibilityClass.AlphaClipLit;
            }
            if (unlit)
            {
                return DonorWorldCompatibilityClass.Unlit;
            }

            bool emissive =
                shaderKeywords.IndexOf(
                    "_EMISSION",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderIdentity.IndexOf(
                    "Emiss",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderIdentity.IndexOf(
                    "Emmiss",
                    StringComparison.OrdinalIgnoreCase) >= 0 ||
                textures.ContainsKey("_EmissionMap") ||
                colors.TryGetValue(
                    "_EmissionColor",
                    out Color emission) &&
                emission.maxColorComponent > 0.001f;
            return emissive
                ? DonorWorldCompatibilityClass.EmissiveLit
                : DonorWorldCompatibilityClass.OpaqueLit;
        }

        private static SourceTextureImporterRecord
            ParseTextureImporter(string metaPath)
        {
            int wrapMode = 0;
            int filterMode = 1;
            int aniso = 1;
            foreach (string line in File.ReadLines(metaPath))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(
                        "wrapMode:",
                        StringComparison.Ordinal))
                {
                    int.TryParse(
                        trimmed.Substring("wrapMode:".Length).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out wrapMode);
                }
                else if (trimmed.StartsWith(
                             "filterMode:",
                             StringComparison.Ordinal))
                {
                    int.TryParse(
                        trimmed.Substring("filterMode:".Length).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out filterMode);
                }
                else if (trimmed.StartsWith(
                             "aniso:",
                             StringComparison.Ordinal))
                {
                    int.TryParse(
                        trimmed.Substring("aniso:".Length).Trim(),
                        NumberStyles.Integer,
                        CultureInfo.InvariantCulture,
                        out aniso);
                }
            }

            return new SourceTextureImporterRecord(
                wrapMode,
                filterMode,
                Mathf.Clamp(aniso, 1, 16));
        }

        private static string ComputePresentationFingerprint(
            IEnumerable<WorldBaselineSanitationEntry> rendererEntries,
            IReadOnlyDictionary<string, DonorWorldSourceMaterial> materials,
            IReadOnlyDictionary<string, DonorWorldTextureConversion> textures)
        {
            var builder = new StringBuilder();
            builder.Append(ConverterVersion).Append('\n')
                .Append(WorldBaselinePaths.SourceRevisionId).Append('\n')
                .Append(WorldBaselinePaths.SourceSceneSha256).Append('\n');
            foreach (WorldBaselineSanitationEntry entry in rendererEntries)
            {
                builder.Append(entry.Placement.StableId)
                    .Append('|')
                    .Append(entry.SourceMaterialGuidsText)
                    .Append('\n');
            }
            foreach (DonorWorldSourceMaterial material in materials.Values
                         .OrderBy(
                             value => value.SourceGuid,
                             StringComparer.Ordinal))
            {
                builder.Append(material.SourceGuid)
                    .Append('|')
                    .Append(material.SourceSha256)
                    .Append('|')
                    .Append(material.SourceShaderIdentity)
                    .Append('|')
                    .Append(material.CompatibilityClass)
                    .Append('|')
                    .Append(material.DoubleSided ? '1' : '0')
                    .Append('\n');
            }
            foreach (DonorWorldTextureConversion texture in textures.Values
                         .OrderBy(
                             value => value.Key,
                             StringComparer.Ordinal))
            {
                builder.Append(texture.Key)
                    .Append('|')
                    .Append(texture.ConversionFingerprintSha256)
                    .Append('\n');
            }

            return DonorWorldBaselineManifest.Sha256Text(
                builder.ToString());
        }

        private static string ReadMetaGuid(string metaPath)
        {
            foreach (string line in File.ReadLines(metaPath))
            {
                if (line.StartsWith(
                        "guid:",
                        StringComparison.Ordinal))
                {
                    return line.Substring("guid:".Length).Trim();
                }
            }

            return string.Empty;
        }

        private static string ToSourceRelativePath(
            string sourceAssetsRoot,
            string absolutePath)
        {
            string relative = Path.GetRelativePath(
                    sourceAssetsRoot,
                    absolutePath)
                .Replace('\\', '/');
            return ExtractedAssetsRelativePath + "/" + relative;
        }

        private static bool IsSupportedTextureSource(string path) =>
            string.Equals(
                Path.GetExtension(path),
                ".png",
                StringComparison.OrdinalIgnoreCase);

        private static Vector2 ParseVector2(
            string value,
            Vector2 fallback)
        {
            Match match = Regex.Match(
                value,
                "\\{x:\\s*([^,}]+),\\s*y:\\s*([^,}]+)");
            if (!match.Success ||
                !float.TryParse(
                    match.Groups[1].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float x) ||
                !float.TryParse(
                    match.Groups[2].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float y))
            {
                return fallback;
            }

            return new Vector2(x, y);
        }

        private static bool TryParseColor(
            string value,
            out Color color)
        {
            Match match = Regex.Match(
                value,
                "\\{r:\\s*([^,}]+),\\s*g:\\s*([^,}]+),\\s*b:\\s*([^,}]+),\\s*a:\\s*([^,}]+)");
            if (match.Success &&
                float.TryParse(
                    match.Groups[1].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float r) &&
                float.TryParse(
                    match.Groups[2].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float g) &&
                float.TryParse(
                    match.Groups[3].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float b) &&
                float.TryParse(
                    match.Groups[4].Value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float a))
            {
                color = new Color(r, g, b, a);
                return true;
            }

            color = default;
            return false;
        }

        private enum SavedPropertySection
        {
            None = 0,
            Textures = 1,
            Floats = 2,
            Colors = 3
        }

        private readonly struct SourceAssetRecord
        {
            public SourceAssetRecord(
                string relativePath,
                string absolutePath)
            {
                RelativePath = relativePath;
                AbsolutePath = absolutePath;
            }

            public string RelativePath { get; }
            public string AbsolutePath { get; }
        }

        private readonly struct SourceShaderRecord
        {
            public SourceShaderRecord(
                string identity,
                string relativePath,
                string sha256)
            {
                Identity = identity;
                RelativePath = relativePath;
                Sha256 = sha256;
            }

            public string Identity { get; }
            public string RelativePath { get; }
            public string Sha256 { get; }
        }

        private readonly struct SourceTextureImporterRecord
        {
            public SourceTextureImporterRecord(
                int wrapMode,
                int filterMode,
                int anisoLevel)
            {
                WrapMode = wrapMode;
                FilterMode = filterMode;
                AnisoLevel = anisoLevel;
            }

            public int WrapMode { get; }
            public int FilterMode { get; }
            public int AnisoLevel { get; }
        }

        private sealed class TextureUseAccumulator
        {
            private readonly HashSet<string> properties =
                new HashSet<string>(StringComparer.Ordinal);

            public TextureUseAccumulator(
                string sourceGuid,
                DonorWorldTextureRole role)
            {
                SourceGuid = sourceGuid;
                Role = role;
            }

            public string SourceGuid { get; }
            public DonorWorldTextureRole Role { get; }
            public IEnumerable<string> Properties => properties;
            public int ReferenceCount { get; private set; }

            public void Add(string propertyName)
            {
                properties.Add(propertyName);
                ReferenceCount++;
            }
        }
    }
}
