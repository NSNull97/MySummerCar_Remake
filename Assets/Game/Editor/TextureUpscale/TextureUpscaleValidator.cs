using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.TextureUpscale
{
    public static class TextureUpscaleValidator
    {
        private const string ApplyRequestRelativePath =
            "Reports/TextureUpscale/apply_request.json";
        private const string InventoryRelativePath =
            "Reports/TextureUpscale/unity_texture_inventory.json";
        private const string ValidationRelativePath =
            "Reports/TextureUpscale/validation_unity.json";

        [MenuItem("MSC/Texture Upscale/Validate Applied Sample")]
        public static void ValidateMenu() => ValidateBatch();

        public static void ValidateBatch()
        {
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            TextureApplyRequest request = ReadJson<TextureApplyRequest>(
                ApplyRequestRelativePath);
            TextureInventoryData inventory = ReadJson<TextureInventoryData>(
                InventoryRelativePath);
            Dictionary<string, TextureInventoryRecord> inventoryByPath =
                inventory.textures.ToDictionary(
                    value => value.assetPath,
                    StringComparer.OrdinalIgnoreCase);
            var report = new TextureUnityValidationReport
            {
                schemaVersion = 1,
                generatedAtUtc = DateTime.UtcNow.ToString("O")
            };
            foreach (TextureApplyItem item in request.textures)
            {
                inventoryByPath.TryGetValue(
                    item.assetPath,
                    out TextureInventoryRecord original);
                report.items.Add(ValidateOne(item, original));
            }
            report.failureCount = report.items.Count(
                value => value.failures.Count > 0);
            report.warningCount = report.items.Count(
                value => value.warnings.Count > 0);
            TextureInventoryExporter.WriteJson(
                ValidationRelativePath,
                report);
            if (report.failureCount > 0)
            {
                throw new InvalidOperationException(
                    $"Unity texture validation found " +
                    $"{report.failureCount} failing assets. See " +
                    ValidationRelativePath + ".");
            }
            Debug.Log(
                $"Unity texture validation passed: " +
                $"{report.items.Count} assets, " +
                $"{report.warningCount} with warnings.");
        }

        private static TextureUnityValidationItem ValidateOne(
            TextureApplyItem item,
            TextureInventoryRecord original)
        {
            var result = new TextureUnityValidationItem
            {
                assetPath = item.assetPath,
                category = item.category
            };
            string absolutePath = ToAbsoluteProjectPath(item.assetPath);
            if (!File.Exists(absolutePath))
            {
                result.failures.Add("Texture source file is missing.");
                return result;
            }
            if (!File.Exists(absolutePath + ".meta"))
            {
                result.failures.Add("Texture .meta file is missing.");
                return result;
            }

            string guid = AssetDatabase.AssetPathToGUID(item.assetPath);
            result.actualGuid = guid;
            if (!string.Equals(
                    guid,
                    item.expectedGuid,
                    StringComparison.OrdinalIgnoreCase))
            {
                result.failures.Add(
                    $"GUID changed: expected={item.expectedGuid}, " +
                    $"actual={guid}.");
            }

            TextureImporter importer =
                AssetImporter.GetAtPath(item.assetPath) as TextureImporter;
            if (importer == null)
            {
                result.failures.Add("TextureImporter is unavailable.");
                return result;
            }
            Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(
                item.assetPath);
            if (texture == null)
            {
                result.failures.Add("Imported Texture object is missing.");
                return result;
            }
            result.importedWidth = texture.width;
            result.importedHeight = texture.height;
            result.maxTextureSize = importer.maxTextureSize;
            result.textureType = importer.textureType.ToString();
            result.sRGB = importer.sRGBTexture;
            result.hasAlpha = importer.DoesSourceTextureHaveAlpha();
            result.spriteMode = importer.spriteImportMode.ToString();
            result.spritePixelsPerUnit = importer.spritePixelsPerUnit;

            if (texture.width != item.newWidth ||
                texture.height != item.newHeight)
            {
                result.failures.Add(
                    $"Imported size mismatch: " +
                    $"expected={item.newWidth}x{item.newHeight}, " +
                    $"actual={texture.width}x{texture.height}.");
            }
            if (Mathf.Max(item.newWidth, item.newHeight) >
                importer.maxTextureSize)
            {
                result.failures.Add(
                    "Texture exceeds importer Max Texture Size.");
            }
            if (item.category == "Normal Map")
            {
                if (importer.textureType !=
                    TextureImporterType.NormalMap)
                {
                    result.failures.Add(
                        "Normal map lost TextureImporterType.NormalMap.");
                }
                if (importer.sRGBTexture)
                {
                    result.failures.Add(
                        "Normal map is incorrectly imported as sRGB.");
                }
            }
            if (item.category == "HDRP Mask Map" &&
                importer.sRGBTexture)
            {
                result.failures.Add(
                    "HDRP Mask Map is incorrectly imported as sRGB.");
            }
            if (original != null)
            {
                if (original.sourceHasAlpha && !result.hasAlpha)
                {
                    result.failures.Add("Source alpha was lost.");
                }
                if (importer.wrapModeU.ToString() != original.wrapU ||
                    importer.wrapModeV.ToString() != original.wrapV ||
                    importer.wrapModeW.ToString() != original.wrapW)
                {
                    result.failures.Add("Wrap Mode changed.");
                }
                if (importer.filterMode.ToString() != original.filterMode ||
                    importer.anisoLevel != original.anisoLevel)
                {
                    result.failures.Add("Filter or anisotropy settings changed.");
                }
                if (importer.mipmapEnabled != original.mipmaps ||
                    importer.mipMapsPreserveCoverage !=
                        original.mipMapsPreserveCoverage)
                {
                    result.failures.Add("Mipmap settings changed.");
                }
                if (item.updateSpritePpu)
                {
                    float expectedPpu = original.spritePixelsPerUnit *
                        item.scaleFactor;
                    if (!Mathf.Approximately(
                            importer.spritePixelsPerUnit,
                            expectedPpu))
                    {
                        result.failures.Add(
                            $"Sprite PPU was not scaled correctly: " +
                            $"expected={expectedPpu}, " +
                            $"actual={importer.spritePixelsPerUnit}.");
                    }
                }
                else if (!Mathf.Approximately(
                             importer.spritePixelsPerUnit,
                             original.spritePixelsPerUnit))
                {
                    result.failures.Add("Sprite PPU changed unexpectedly.");
                }
                ValidateMaterialUses(
                    item.assetPath,
                    original.materialUses,
                    result);
            }
            else
            {
                result.warnings.Add(
                    "Texture was not found in the pre-apply Unity inventory.");
            }
            return result;
        }

        private static void ValidateMaterialUses(
            string expectedTexturePath,
            IEnumerable<TextureMaterialUseData> uses,
            TextureUnityValidationItem result)
        {
            foreach (TextureMaterialUseData use in uses)
            {
                Material material =
                    AssetDatabase.LoadAssetAtPath<Material>(
                        use.materialPath);
                if (material == null)
                {
                    result.failures.Add(
                        "Referenced material is missing: " +
                        use.materialPath);
                    continue;
                }
                if (material.shader == null ||
                    material.shader.name.IndexOf(
                        "InternalErrorShader",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.failures.Add(
                        "Referenced material has a missing/error shader: " +
                        use.materialPath);
                    continue;
                }
                if (!material.HasProperty(use.propertyName))
                {
                    result.failures.Add(
                        $"Material property is missing: " +
                        $"{use.materialPath}::{use.propertyName}");
                    continue;
                }
                Texture assigned = material.GetTexture(use.propertyName);
                string assignedPath = assigned != null
                    ? AssetDatabase.GetAssetPath(assigned)
                    : string.Empty;
                if (!string.Equals(
                        assignedPath,
                        expectedTexturePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    result.failures.Add(
                        $"Material texture reference changed: " +
                        $"{use.materialPath}::{use.propertyName} -> " +
                        assignedPath);
                }
            }
        }

        private static T ReadJson<T>(string projectRelativePath)
            where T : class
        {
            string absolutePath = ToAbsoluteProjectPath(
                projectRelativePath);
            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException(
                    "Texture pipeline JSON is missing.",
                    absolutePath);
            }
            T value = JsonUtility.FromJson<T>(
                File.ReadAllText(absolutePath, Encoding.UTF8));
            return value ?? throw new InvalidDataException(
                "Texture pipeline JSON could not be parsed: " +
                projectRelativePath);
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
    public sealed class TextureUnityValidationReport
    {
        public int schemaVersion;
        public string generatedAtUtc = string.Empty;
        public int failureCount;
        public int warningCount;
        public List<TextureUnityValidationItem> items = new();
    }

    [Serializable]
    public sealed class TextureUnityValidationItem
    {
        public string assetPath = string.Empty;
        public string category = string.Empty;
        public string actualGuid = string.Empty;
        public int importedWidth;
        public int importedHeight;
        public int maxTextureSize;
        public string textureType = string.Empty;
        public bool sRGB;
        public bool hasAlpha;
        public string spriteMode = string.Empty;
        public float spritePixelsPerUnit;
        public List<string> failures = new();
        public List<string> warnings = new();
    }
}
