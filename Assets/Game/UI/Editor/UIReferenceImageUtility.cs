using System;
using System.IO;
using UnityEngine;

namespace MSC.UI.EditorTools
{
    public static class UIReferenceImageUtility
    {
        private const float AspectTolerance = 0.002f;

        public static Texture2D LoadPng(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("PNG file is missing.", filePath);
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                name = Path.GetFileNameWithoutExtension(filePath),
                hideFlags = HideFlags.HideAndDontSave,
            };

            try
            {
                if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(filePath), false))
                {
                    throw new InvalidDataException($"Unity could not decode PNG '{filePath}'.");
                }

                return texture;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }

        public static Texture2D NormalizeToCanonical(Texture2D source)
        {
            return Normalize(
                source,
                UIReferenceCatalog.CanonicalWidth,
                UIReferenceCatalog.CanonicalHeight);
        }

        public static Texture2D Normalize(Texture2D source, int targetWidth, int targetHeight)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (targetWidth <= 0 || targetHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetWidth),
                    $"Target dimensions must be positive, got {targetWidth}x{targetHeight}.");
            }

            var sourceAspect = (float)source.width / source.height;
            var targetAspect = (float)targetWidth / targetHeight;
            if (Mathf.Abs(sourceAspect - targetAspect) > AspectTolerance)
            {
                throw new InvalidDataException(
                    $"Implementation aspect {source.width}x{source.height} ({sourceAspect:F5}) " +
                    $"does not match canonical {targetWidth}x{targetHeight} ({targetAspect:F5}). " +
                    "Capture at a 16:9 viewport; the review tool will not crop or stretch it.");
            }

            return ResizeBilinear(source, targetWidth, targetHeight);
        }

        public static Texture2D Blend(Texture2D implementation, Texture2D reference, float referenceOpacity)
        {
            if (implementation == null)
            {
                throw new ArgumentNullException(nameof(implementation));
            }

            if (reference == null)
            {
                throw new ArgumentNullException(nameof(reference));
            }

            if (implementation.width != reference.width || implementation.height != reference.height)
            {
                throw new ArgumentException(
                    "Implementation and reference textures must have matching dimensions.");
            }

            var opacity = Mathf.Clamp01(referenceOpacity);
            var implementationPixels = implementation.GetPixels32();
            var referencePixels = reference.GetPixels32();
            var blendedPixels = new Color32[implementationPixels.Length];

            for (var index = 0; index < implementationPixels.Length; index++)
            {
                var effectiveOpacity = opacity * (referencePixels[index].a / 255f);
                blendedPixels[index] = Lerp(implementationPixels[index], referencePixels[index], effectiveOpacity);
            }

            return CreateTexture(
                implementation.width,
                implementation.height,
                blendedPixels,
                "UIReferenceBlend");
        }

        public static byte[] EncodePng(Texture2D texture)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            var bytes = ImageConversion.EncodeToPNG(texture);
            if (bytes == null || bytes.Length == 0)
            {
                throw new InvalidOperationException("Unity returned an empty PNG payload.");
            }

            return bytes;
        }

        public static void WriteBytesAtomically(string outputPath, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
            {
                throw new ArgumentException("Capture payload must not be empty.", nameof(bytes));
            }

            var directory = Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new ArgumentException("Output path has no parent directory.", nameof(outputPath));
            }

            Directory.CreateDirectory(directory);
            var temporaryPath = outputPath + ".tmp";
            File.WriteAllBytes(temporaryPath, bytes);

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            File.Move(temporaryPath, outputPath);
        }

        private static Texture2D ResizeBilinear(Texture2D source, int targetWidth, int targetHeight)
        {
            var sourcePixels = source.GetPixels32();
            var targetPixels = new Color32[targetWidth * targetHeight];
            var scaleX = (float)source.width / targetWidth;
            var scaleY = (float)source.height / targetHeight;

            for (var targetY = 0; targetY < targetHeight; targetY++)
            {
                var sourceY = ((targetY + 0.5f) * scaleY) - 0.5f;
                var y0 = Mathf.Clamp(Mathf.FloorToInt(sourceY), 0, source.height - 1);
                var y1 = Mathf.Min(y0 + 1, source.height - 1);
                var ty = Mathf.Clamp01(sourceY - y0);

                for (var targetX = 0; targetX < targetWidth; targetX++)
                {
                    var sourceX = ((targetX + 0.5f) * scaleX) - 0.5f;
                    var x0 = Mathf.Clamp(Mathf.FloorToInt(sourceX), 0, source.width - 1);
                    var x1 = Mathf.Min(x0 + 1, source.width - 1);
                    var tx = Mathf.Clamp01(sourceX - x0);

                    var bottom = Lerp(
                        sourcePixels[(y0 * source.width) + x0],
                        sourcePixels[(y0 * source.width) + x1],
                        tx);
                    var top = Lerp(
                        sourcePixels[(y1 * source.width) + x0],
                        sourcePixels[(y1 * source.width) + x1],
                        tx);
                    targetPixels[(targetY * targetWidth) + targetX] = Lerp(bottom, top, ty);
                }
            }

            return CreateTexture(targetWidth, targetHeight, targetPixels, "UICanonicalCapture");
        }

        private static Texture2D CreateTexture(
            int width,
            int height,
            Color32[] pixels,
            string textureName)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Color32 Lerp(Color32 from, Color32 to, float t)
        {
            return new Color32(
                LerpByte(from.r, to.r, t),
                LerpByte(from.g, to.g, t),
                LerpByte(from.b, to.b, t),
                LerpByte(from.a, to.a, t));
        }

        private static byte LerpByte(byte from, byte to, float t)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(from + ((to - from) * t)), 0, 255);
        }
    }
}
