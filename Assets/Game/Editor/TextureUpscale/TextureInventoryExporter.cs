using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.TextureUpscale
{
    public static class TextureInventoryExporter
    {
        public const string InventoryRelativePath =
            "Reports/TextureUpscale/unity_texture_inventory.json";

        [MenuItem("MSC/Texture Upscale/Export Texture Inventory")]
        public static void ExportMenu() => ExportBatch();

        public static void ExportBatch()
        {
            TextureInventoryData inventory = BuildInventory();
            WriteJson(InventoryRelativePath, inventory);
            TextureImportSettingsBackup.WriteSnapshot(inventory);
            Debug.Log(
                $"Texture Upscale inventory exported: " +
                $"{inventory.textures.Count} textures, " +
                $"{inventory.materialCount} materials -> " +
                InventoryRelativePath);
        }

        public static TextureInventoryData BuildInventory()
        {
            Dictionary<string, List<TextureMaterialUseData>> materialUses =
                BuildMaterialUses(out int materialCount);
            string[] textureGuids = AssetDatabase.FindAssets(
                "t:Texture",
                new[] { "Assets" });
            var textures = new List<TextureInventoryRecord>(
                textureGuids.Length);
            foreach (string guid in textureGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsSourceTexturePath(assetPath))
                {
                    continue;
                }

                TextureImporter importer =
                    AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    continue;
                }

                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(
                    assetPath);
                var record = new TextureInventoryRecord
                {
                    assetPath = assetPath.Replace('\\', '/'),
                    guid = guid,
                    textureType = importer.textureType.ToString(),
                    textureShape = importer.textureShape.ToString(),
                    sRGB = importer.sRGBTexture,
                    alphaSource = importer.alphaSource.ToString(),
                    alphaIsTransparency = importer.alphaIsTransparency,
                    sourceHasAlpha = importer.DoesSourceTextureHaveAlpha(),
                    wrapMode = importer.wrapMode.ToString(),
                    wrapU = importer.wrapModeU.ToString(),
                    wrapV = importer.wrapModeV.ToString(),
                    wrapW = importer.wrapModeW.ToString(),
                    filterMode = importer.filterMode.ToString(),
                    anisoLevel = importer.anisoLevel,
                    mipmaps = importer.mipmapEnabled,
                    mipmapFilter = importer.mipmapFilter.ToString(),
                    mipMapsPreserveCoverage =
                        importer.mipMapsPreserveCoverage,
                    mipmapFadeDistanceStart =
                        importer.mipmapFadeDistanceStart,
                    mipmapFadeDistanceEnd =
                        importer.mipmapFadeDistanceEnd,
                    maxTextureSize = importer.maxTextureSize,
                    textureCompression =
                        importer.textureCompression.ToString(),
                    compressionQuality = importer.compressionQuality,
                    crunchedCompression = importer.crunchedCompression,
                    readable = importer.isReadable,
                    streamingMipmaps = importer.streamingMipmaps,
                    streamingMipmapsPriority =
                        importer.streamingMipmapsPriority,
                    spriteMode = importer.spriteImportMode.ToString(),
                    spritePixelsPerUnit = importer.spritePixelsPerUnit,
                    spritePivot = Vector2Data.From(importer.spritePivot),
                    spriteBorder = Vector4Data.From(importer.spriteBorder),
                    convertToNormalMap = importer.convertToNormalmap,
                    flipGreenChannel = importer.flipGreenChannel,
                    normalMapFilter = importer.normalmapFilter.ToString(),
                    heightMapScale = importer.heightmapScale,
                    importedWidth = texture != null ? texture.width : 0,
                    importedHeight = texture != null ? texture.height : 0,
                    importedDimension =
                        texture != null
                            ? texture.dimension.ToString()
                            : string.Empty,
                    importedFormat =
                        texture is Texture2D texture2D
                            ? texture2D.format.ToString()
                            : string.Empty,
                    materialUses =
                        materialUses.TryGetValue(
                            assetPath,
                            out List<TextureMaterialUseData> uses)
                            ? uses
                            : new List<TextureMaterialUseData>(),
                    platformOverrides = BuildPlatformOverrides(importer),
                    sprites = BuildSpriteRecords(importer)
                };
                record.spriteCount = record.sprites.Count;
                record.spriteRects = record.sprites
                    .Select(value => value.rect)
                    .ToList();
                record.spriteBorders = record.sprites
                    .Select(value => value.border)
                    .ToList();
                textures.Add(record);
            }

            textures.Sort((left, right) => string.Compare(
                left.assetPath,
                right.assetPath,
                StringComparison.OrdinalIgnoreCase));
            return new TextureInventoryData
            {
                schemaVersion = 1,
                generatedAtUtc = DateTime.UtcNow.ToString(
                    "O",
                    CultureInfo.InvariantCulture),
                unityVersion = Application.unityVersion,
                materialCount = materialCount,
                textureCount = textures.Count,
                textures = textures
            };
        }

        private static Dictionary<string, List<TextureMaterialUseData>>
            BuildMaterialUses(out int materialCount)
        {
            var result =
                new Dictionary<string, List<TextureMaterialUseData>>(
                    StringComparer.OrdinalIgnoreCase);
            string[] materialGuids = AssetDatabase.FindAssets(
                "t:Material",
                new[] { "Assets" });
            materialCount = materialGuids.Length;
            foreach (string materialGuid in materialGuids)
            {
                string materialPath =
                    AssetDatabase.GUIDToAssetPath(materialGuid);
                Material material =
                    AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if (material == null || material.shader == null)
                {
                    continue;
                }

                Shader shader = material.shader;
                int propertyCount = ShaderUtil.GetPropertyCount(shader);
                for (int propertyIndex = 0;
                     propertyIndex < propertyCount;
                     propertyIndex++)
                {
                    if (ShaderUtil.GetPropertyType(
                            shader,
                            propertyIndex) !=
                        ShaderUtil.ShaderPropertyType.TexEnv)
                    {
                        continue;
                    }

                    string propertyName = ShaderUtil.GetPropertyName(
                        shader,
                        propertyIndex);
                    Texture texture = material.GetTexture(propertyName);
                    if (texture == null)
                    {
                        continue;
                    }

                    string texturePath = AssetDatabase.GetAssetPath(texture);
                    if (string.IsNullOrEmpty(texturePath))
                    {
                        continue;
                    }

                    if (!result.TryGetValue(
                            texturePath,
                            out List<TextureMaterialUseData> uses))
                    {
                        uses = new List<TextureMaterialUseData>();
                        result.Add(texturePath, uses);
                    }
                    uses.Add(new TextureMaterialUseData
                    {
                        materialPath = materialPath.Replace('\\', '/'),
                        materialGuid = materialGuid,
                        materialName = material.name,
                        shaderName = shader.name,
                        propertyName = propertyName,
                        scale = Vector2Data.From(
                            material.GetTextureScale(propertyName)),
                        offset = Vector2Data.From(
                            material.GetTextureOffset(propertyName))
                    });
                }
            }

            foreach (List<TextureMaterialUseData> uses in result.Values)
            {
                uses.Sort((left, right) =>
                {
                    int pathComparison = string.Compare(
                        left.materialPath,
                        right.materialPath,
                        StringComparison.OrdinalIgnoreCase);
                    return pathComparison != 0
                        ? pathComparison
                        : string.Compare(
                            left.propertyName,
                            right.propertyName,
                            StringComparison.Ordinal);
                });
            }
            return result;
        }

        private static List<TexturePlatformOverrideData>
            BuildPlatformOverrides(TextureImporter importer)
        {
            string[] names =
            {
                "DefaultTexturePlatform",
                "Standalone",
                "Windows Store Apps",
                "Android",
                "iPhone"
            };
            var result = new List<TexturePlatformOverrideData>();
            foreach (string name in names)
            {
                TextureImporterPlatformSettings settings =
                    importer.GetPlatformTextureSettings(name);
                result.Add(new TexturePlatformOverrideData
                {
                    name = name,
                    overridden = settings.overridden,
                    maxTextureSize = settings.maxTextureSize,
                    resizeAlgorithm = settings.resizeAlgorithm.ToString(),
                    format = settings.format.ToString(),
                    textureCompression =
                        settings.textureCompression.ToString(),
                    compressionQuality = settings.compressionQuality,
                    crunchedCompression = settings.crunchedCompression,
                    allowsAlphaSplitting = settings.allowsAlphaSplitting
                });
            }
            return result;
        }

#pragma warning disable 0618
        private static List<SpriteRecord> BuildSpriteRecords(
            TextureImporter importer)
        {
            SpriteMetaData[] sprites = importer.spritesheet ??
                Array.Empty<SpriteMetaData>();
            return sprites.Select(sprite => new SpriteRecord
                {
                    name = sprite.name,
                    alignment = sprite.alignment,
                    pivot = Vector2Data.From(sprite.pivot),
                    border = Vector4Data.From(sprite.border),
                    rect = RectData.From(sprite.rect)
                })
                .ToList();
        }
#pragma warning restore 0618

        private static bool IsSourceTexturePath(string assetPath)
        {
            string extension = Path.GetExtension(assetPath);
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }
            switch (extension.ToLowerInvariant())
            {
                case ".png":
                case ".jpg":
                case ".jpeg":
                case ".tga":
                case ".tif":
                case ".tiff":
                case ".bmp":
                case ".psd":
                case ".exr":
                case ".hdr":
                case ".dds":
                    return true;
                default:
                    return false;
            }
        }

        public static void WriteJson(
            string projectRelativePath,
            object value)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)
                ?.FullName ?? throw new InvalidOperationException(
                    "Unity project root cannot be resolved.");
            string absolutePath = Path.Combine(
                projectRoot,
                projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(
                Path.GetDirectoryName(absolutePath) ?? projectRoot);
            File.WriteAllText(
                absolutePath,
                JsonUtility.ToJson(value, prettyPrint: true) +
                Environment.NewLine,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
        }
    }

    [Serializable]
    public sealed class TextureInventoryData
    {
        public int schemaVersion;
        public string generatedAtUtc = string.Empty;
        public string unityVersion = string.Empty;
        public int materialCount;
        public int textureCount;
        public List<TextureInventoryRecord> textures = new();
    }

    [Serializable]
    public sealed class TextureInventoryRecord
    {
        public string assetPath = string.Empty;
        public string guid = string.Empty;
        public string textureType = string.Empty;
        public string textureShape = string.Empty;
        public bool sRGB;
        public string alphaSource = string.Empty;
        public bool alphaIsTransparency;
        public bool sourceHasAlpha;
        public string wrapMode = string.Empty;
        public string wrapU = string.Empty;
        public string wrapV = string.Empty;
        public string wrapW = string.Empty;
        public string filterMode = string.Empty;
        public int anisoLevel;
        public bool mipmaps;
        public string mipmapFilter = string.Empty;
        public bool mipMapsPreserveCoverage;
        public int mipmapFadeDistanceStart;
        public int mipmapFadeDistanceEnd;
        public int maxTextureSize;
        public string textureCompression = string.Empty;
        public int compressionQuality;
        public bool crunchedCompression;
        public bool readable;
        public bool streamingMipmaps;
        public int streamingMipmapsPriority;
        public string spriteMode = string.Empty;
        public float spritePixelsPerUnit;
        public Vector2Data spritePivot = new();
        public Vector4Data spriteBorder = new();
        public int spriteCount;
        public List<RectData> spriteRects = new();
        public List<Vector4Data> spriteBorders = new();
        public bool convertToNormalMap;
        public bool flipGreenChannel;
        public string normalMapFilter = string.Empty;
        public float heightMapScale;
        public int importedWidth;
        public int importedHeight;
        public string importedDimension = string.Empty;
        public string importedFormat = string.Empty;
        public List<TexturePlatformOverrideData> platformOverrides = new();
        public List<TextureMaterialUseData> materialUses = new();
        public List<SpriteRecord> sprites = new();
    }

    [Serializable]
    public sealed class TextureMaterialUseData
    {
        public string materialPath = string.Empty;
        public string materialGuid = string.Empty;
        public string materialName = string.Empty;
        public string shaderName = string.Empty;
        public string propertyName = string.Empty;
        public Vector2Data scale = new();
        public Vector2Data offset = new();
    }

    [Serializable]
    public sealed class TexturePlatformOverrideData
    {
        public string name = string.Empty;
        public bool overridden;
        public int maxTextureSize;
        public string resizeAlgorithm = string.Empty;
        public string format = string.Empty;
        public string textureCompression = string.Empty;
        public int compressionQuality;
        public bool crunchedCompression;
        public bool allowsAlphaSplitting;
    }

    [Serializable]
    public sealed class SpriteRecord
    {
        public string name = string.Empty;
        public int alignment;
        public Vector2Data pivot = new();
        public Vector4Data border = new();
        public RectData rect = new();
    }

    [Serializable]
    public sealed class Vector2Data
    {
        public float x;
        public float y;

        public static Vector2Data From(Vector2 value) => new()
        {
            x = value.x,
            y = value.y
        };
    }

    [Serializable]
    public sealed class Vector4Data
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public static Vector4Data From(Vector4 value) => new()
        {
            x = value.x,
            y = value.y,
            z = value.z,
            w = value.w
        };
    }

    [Serializable]
    public sealed class RectData
    {
        public float x;
        public float y;
        public float width;
        public float height;

        public static RectData From(Rect value) => new()
        {
            x = value.x,
            y = value.y,
            width = value.width,
            height = value.height
        };
    }
}
