using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.TextureUpscale
{
    public static class TextureUpscaleApply
    {
        private const string ApplyRequestRelativePath =
            "Reports/TextureUpscale/apply_request.json";
        private const string ApplyResultRelativePath =
            "Reports/TextureUpscale/unity_apply_result.json";

        [MenuItem("MSC/Texture Upscale/Apply and Reimport Request")]
        public static void ApplyMenu() => ApplyBatch();

        public static void ApplyBatch()
        {
            TextureApplyRequest request = LoadRequest();
            var result = new TextureApplyResult
            {
                schemaVersion = 1,
                generatedAtUtc = DateTime.UtcNow.ToString("O")
            };
            foreach (TextureApplyItem item in request.textures)
            {
                result.items.Add(ApplyOne(item));
            }
            result.failureCount = result.items.FindAll(
                value => !value.success).Count;
            TextureInventoryExporter.WriteJson(
                ApplyResultRelativePath,
                result);
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            if (result.failureCount > 0)
            {
                throw new InvalidOperationException(
                    $"Texture sample apply/reimport failed for " +
                    $"{result.failureCount} assets. See " +
                    ApplyResultRelativePath + ".");
            }
            Debug.Log(
                $"Texture sample reimport completed: " +
                $"{result.items.Count} assets.");
        }

        public static void ReimportBatch()
        {
            TextureApplyRequest request = LoadRequest();
            foreach (TextureApplyItem item in request.textures)
            {
                AssetDatabase.ImportAsset(
                    item.assetPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            Debug.Log(
                $"Texture request reimported without importer mutations: " +
                $"{request.textures.Count} assets.");
        }

        private static TextureApplyItemResult ApplyOne(
            TextureApplyItem item)
        {
            var result = new TextureApplyItemResult
            {
                assetPath = item.assetPath,
                expectedGuid = item.expectedGuid
            };
            try
            {
                if (!item.assetPath.StartsWith(
                        "Assets/",
                        StringComparison.Ordinal) ||
                    !File.Exists(ToAbsoluteProjectPath(item.assetPath)))
                {
                    throw new FileNotFoundException(
                        "Requested texture is missing or outside Assets.",
                        item.assetPath);
                }

                string guidBefore = AssetDatabase.AssetPathToGUID(
                    item.assetPath);
                if (!string.Equals(
                        guidBefore,
                        item.expectedGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"GUID mismatch before reimport: " +
                        $"expected={item.expectedGuid}, actual={guidBefore}.");
                }

                TextureImporter importer =
                    AssetImporter.GetAtPath(item.assetPath) as
                        TextureImporter ??
                    throw new InvalidDataException(
                        "TextureImporter is unavailable.");
                bool settingsChanged = false;
                int requiredMaxSize = Mathf.NextPowerOfTwo(
                    Mathf.Max(item.newWidth, item.newHeight));
                requiredMaxSize = Mathf.Clamp(requiredMaxSize, 32, 8192);
                if (importer.maxTextureSize < requiredMaxSize)
                {
                    importer.maxTextureSize = requiredMaxSize;
                    settingsChanged = true;
                }

                TextureImporterPlatformSettings standalone =
                    importer.GetPlatformTextureSettings("Standalone");
                if (standalone.overridden &&
                    standalone.maxTextureSize < requiredMaxSize)
                {
                    standalone.maxTextureSize = requiredMaxSize;
                    importer.SetPlatformTextureSettings(standalone);
                    settingsChanged = true;
                }

                if (item.updateSpritePpu && item.scaleFactor > 1)
                {
                    importer.spritePixelsPerUnit *= item.scaleFactor;
                    importer.spriteBorder = ScaleVector4(
                        importer.spriteBorder,
                        item.scaleFactor);
                    settingsChanged = true;
                }

                if (item.updateSpriteSheet && item.scaleFactor > 1)
                {
                    ScaleSpriteSheet(importer, item.scaleFactor);
                    settingsChanged = true;
                }

                if (settingsChanged)
                {
                    importer.SaveAndReimport();
                }
                else
                {
                    AssetDatabase.ImportAsset(
                        item.assetPath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                }

                string guidAfter = AssetDatabase.AssetPathToGUID(
                    item.assetPath);
                if (!string.Equals(
                        guidAfter,
                        item.expectedGuid,
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"GUID changed after reimport: " +
                        $"expected={item.expectedGuid}, actual={guidAfter}.");
                }

                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(
                    item.assetPath);
                if (texture == null)
                {
                    throw new InvalidDataException(
                        "Texture failed to import.");
                }
                result.success = true;
                result.actualGuid = guidAfter;
                result.importedWidth = texture.width;
                result.importedHeight = texture.height;
                result.importerSettingsChanged = settingsChanged;
            }
            catch (Exception exception)
            {
                result.success = false;
                result.error = exception.ToString();
            }
            return result;
        }

#pragma warning disable 0618
        private static void ScaleSpriteSheet(
            TextureImporter importer,
            int scale)
        {
            SpriteMetaData[] sprites = importer.spritesheet ??
                Array.Empty<SpriteMetaData>();
            for (int index = 0; index < sprites.Length; index++)
            {
                SpriteMetaData sprite = sprites[index];
                sprite.rect = new Rect(
                    sprite.rect.x * scale,
                    sprite.rect.y * scale,
                    sprite.rect.width * scale,
                    sprite.rect.height * scale);
                sprite.border = ScaleVector4(sprite.border, scale);
                sprites[index] = sprite;
            }
            importer.spritesheet = sprites;
            importer.spritePixelsPerUnit *= scale;
        }
#pragma warning restore 0618

        private static Vector4 ScaleVector4(
            Vector4 value,
            int scale) => new(
                value.x * scale,
                value.y * scale,
                value.z * scale,
                value.w * scale);

        private static TextureApplyRequest LoadRequest()
        {
            string path = ToAbsoluteProjectPath(
                ApplyRequestRelativePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException(
                    "Texture apply request is missing.",
                    path);
            }
            TextureApplyRequest request = JsonUtility.FromJson<
                TextureApplyRequest>(
                File.ReadAllText(path, Encoding.UTF8));
            if (request == null || request.schemaVersion != 1)
            {
                throw new InvalidDataException(
                    "Unsupported texture apply request schema.");
            }
            return request;
        }

        private static string ToAbsoluteProjectPath(
            string projectRelativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath)
                ?.FullName ?? throw new InvalidOperationException(
                    "Unity project root cannot be resolved.");
            return Path.Combine(
                projectRoot,
                projectRelativePath.Replace(
                    '/',
                    Path.DirectorySeparatorChar));
        }
    }

    [Serializable]
    public sealed class TextureApplyRequest
    {
        public int schemaVersion;
        public string backupManifestPath = string.Empty;
        public List<TextureApplyItem> textures = new();
    }

    [Serializable]
    public sealed class TextureApplyItem
    {
        public string assetPath = string.Empty;
        public string expectedGuid = string.Empty;
        public string category = string.Empty;
        public int scaleFactor;
        public int newWidth;
        public int newHeight;
        public bool updateSpritePpu;
        public bool updateSpriteSheet;
    }

    [Serializable]
    public sealed class TextureApplyResult
    {
        public int schemaVersion;
        public string generatedAtUtc = string.Empty;
        public int failureCount;
        public List<TextureApplyItemResult> items = new();
    }

    [Serializable]
    public sealed class TextureApplyItemResult
    {
        public string assetPath = string.Empty;
        public string expectedGuid = string.Empty;
        public string actualGuid = string.Empty;
        public int importedWidth;
        public int importedHeight;
        public bool importerSettingsChanged;
        public bool success;
        public string error = string.Empty;
    }
}
