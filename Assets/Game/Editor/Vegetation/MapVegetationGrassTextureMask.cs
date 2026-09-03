using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    /// <summary>Editor CPU snapshots. Source importers are never made readable or modified.</summary>
    internal sealed class MapVegetationGrassTextureMask
    {
        private readonly HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> materials = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Pixels> textures = new Dictionary<string, Pixels>(StringComparer.Ordinal);
        private readonly Dictionary<string, Binding> bindings = new Dictionary<string, Binding>(StringComparer.Ordinal);
        private readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> coverageSet = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> coverage = new List<string>();
        private readonly List<string> warnings;
        private readonly float greenExcess, saturation, value,
            carpetMinimumGreenFraction;
        private readonly int dilationPixels, carpetRadiusPixels,
            carpetMinimumSectors;
        private readonly bool rejectUnsupported;
        public bool Enabled { get; }
        public string SettingsFingerprint { get; }
        public IReadOnlyList<string> Coverage => coverage;
        private long samples, accepted, directGreen, dilatedGreen,
            carpetGreen, nonGreen, unsupported, unmasked;
        public string DiagnosticSummary => $"grassMaskEnabled={Enabled} grassMaskTextures={textures.Count} grassMaskBindings={bindings.Count} grassMaskSamples={samples} grassMaskGreen={accepted} grassMaskDirectGreen={directGreen} grassMaskDilatedGreen={dilatedGreen} grassMaskCarpetGreen={carpetGreen} grassMaskNonGreen={nonGreen} grassMaskUnsupported={unsupported} grassMaskOutsideCoverage={unmasked}";

        // Two staggered rings provide stable areal evidence without scanning a
        // full source-texture disk for every world candidate. Quadrant support
        // distinguishes a hole surrounded by green from the brown side of a
        // single green boundary.
        private static readonly Vector2[] CarpetEvidenceOffsets =
        {
            new Vector2(0.45f, 0f), new Vector2(0.3182f, 0.3182f),
            new Vector2(0f, 0.45f), new Vector2(-0.3182f, 0.3182f),
            new Vector2(-0.45f, 0f), new Vector2(-0.3182f, -0.3182f),
            new Vector2(0f, -0.45f), new Vector2(0.3182f, -0.3182f),
            new Vector2(0.9659f, 0.2588f), new Vector2(0.7071f, 0.7071f),
            new Vector2(0.2588f, 0.9659f), new Vector2(-0.2588f, 0.9659f),
            new Vector2(-0.7071f, 0.7071f), new Vector2(-0.9659f, 0.2588f),
            new Vector2(-0.9659f, -0.2588f), new Vector2(-0.7071f, -0.7071f),
            new Vector2(-0.2588f, -0.9659f), new Vector2(0.2588f, -0.9659f),
            new Vector2(0.7071f, -0.7071f), new Vector2(0.9659f, -0.2588f)
        };

        public MapVegetationGrassTextureMask(MapVegetationPlacementSettings settings, List<string> warnings)
        {
            this.warnings = warnings;
            Enabled = settings.GrassTextureMaskEnabled;
            greenExcess = settings.GrassTextureMinimumGreenExcess;
            saturation = settings.GrassTextureMinimumSaturation;
            value = settings.GrassTextureMinimumValue;
            dilationPixels = settings.GrassTextureGreenDilationPixels;
            carpetRadiusPixels = settings.GrassTextureCarpetRadiusPixels;
            carpetMinimumGreenFraction =
                settings.GrassTextureCarpetMinimumGreenFraction;
            carpetMinimumSectors = settings.GrassTextureCarpetMinimumSectors;
            rejectUnsupported = settings.GrassTextureRejectUnsupported;
            foreach (string path in settings.GrassTextureCanonicalPaths)
                if (!string.IsNullOrWhiteSpace(path)) paths.Add(path);
            foreach (Material material in settings.GrassTextureMaterials)
                if (material != null) materials.Add(Identity(material));
            SettingsFingerprint = Hash(writer =>
            {
                writer.Write("msc-grass-base-colour-mask-v3-local-carpet-evidence");
                writer.Write(Enabled); writer.Write(greenExcess); writer.Write(saturation);
                writer.Write(value); writer.Write(dilationPixels);
                writer.Write(carpetRadiusPixels);
                writer.Write(carpetMinimumGreenFraction);
                writer.Write(carpetMinimumSectors);
                writer.Write(rejectUnsupported);
                var sortedPaths = new List<string>(paths); sortedPaths.Sort(StringComparer.Ordinal);
                writer.Write(sortedPaths.Count); foreach (string path in sortedPaths) writer.Write(path);
                var sortedMaterials = new List<string>();
                foreach (Material material in settings.GrassTextureMaterials)
                    if (material != null) sortedMaterials.Add(StableIdentity(material));
                sortedMaterials.Sort(StringComparer.Ordinal);
                writer.Write(sortedMaterials.Count); foreach (string material in sortedMaterials) writer.Write(material);
            });
        }

        public Binding Capture(Material material, string canonicalPath, string source, bool hasUv0)
        {
            if (!Enabled) return null;
            string materialId = material != null ? Identity(material) : "missing-material";
            if (!paths.Contains(canonicalPath ?? string.Empty) && !materials.Contains(materialId))
            { RecordCoverage(source + " | Outside configured mask coverage; existing ground rules only."); return null; }
            string key = materialId + (hasUv0 ? ":uv0" : ":no-uv0");
            if (bindings.TryGetValue(key, out Binding existing))
            { RecordCoverage(source + " | " + existing.Description); return existing; }
            Binding result;
            try
            {
                if (!hasUv0) throw new InvalidDataException("NoUv0");
                if (material == null) throw new InvalidDataException("NoMaterial");
                if (material.HasProperty("_UVBase") && Mathf.Abs(material.GetFloat("_UVBase")) > 0.001f)
                    throw new InvalidDataException("UnsupportedBaseUvMapping");
                string property = null;
                foreach (string candidate in new[] { "_BaseColorMap", "_BaseMap", "_Base_Color", "_MainTex" })
                    if (material.HasProperty(candidate) && material.GetTexture(candidate) is Texture2D) { property = candidate; break; }
                if (property == null) throw new InvalidDataException("NoBaseColourTexture");
                var texture = (Texture2D)material.GetTexture(property);
                string textureKey = Identity(texture);
                if (!textures.TryGetValue(textureKey, out Pixels pixels))
                {
                    pixels = ReadPixels(texture);
                    textures.Add(textureKey, pixels);
                }
                Color tint = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") :
                    material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                result = new Binding(pixels, material.GetTextureScale(property), material.GetTextureOffset(property),
                    texture.wrapModeU, texture.wrapModeV, texture.filterMode, tint, property, AssetDatabase.GetAssetPath(texture));
            }
            catch (Exception exception) when (exception is UnityException || exception is IOException ||
                exception is InvalidDataException || exception is ArgumentException)
            {
                result = new Binding(exception.Message);
                string warning = "GrassTextureMaskUnsupported:" + source + ":" + exception.Message;
                if (reported.Add(warning)) warnings.Add(warning);
            }
            bindings.Add(key, result);
            RecordCoverage(source + " | " + result.Description);
            return result;
        }

        private void RecordCoverage(string entry) { if (coverageSet.Add(entry)) coverage.Add(entry); }

        public bool Allows(Binding binding, Vector2 uv, string source, out string reason)
        {
            reason = string.Empty;
            if (!Enabled || binding == null) { unmasked++; return true; }
            samples++;
            if (!binding.Supported || !float.IsFinite(uv.x) || !float.IsFinite(uv.y))
            {
                unsupported++;
                if (!rejectUnsupported) return true;
                reason = "GrassTextureUnsupported:" + source + ":" + (binding.Error ?? "NonFiniteUv");
                return false;
            }
            bool direct = IsGreen(binding.Sample(uv));
            bool dilated = false;
            int immediateGreen = 0;
            int supportRadius = Mathf.Max(1, dilationPixels);
            int supportRadiusSquared = supportRadius * supportRadius;
            int dilationSquared = dilationPixels * dilationPixels;
            for (int y = -supportRadius; y <= supportRadius; y++)
            for (int x = -supportRadius; x <= supportRadius; x++)
            {
                if (x == 0 && y == 0 || x * x + y * y > supportRadiusSquared)
                    continue;
                if (!IsGreen(binding.SampleTexelOffset(uv, x, y))) continue;
                immediateGreen++;
                if (dilationPixels > 0 && x * x + y * y <= dilationSquared)
                    dilated = true;
            }

            if (carpetRadiusPixels <= 0)
            {
                if (direct)
                {
                    accepted++; directGreen++;
                    return true;
                }
                if (dilated)
                {
                    accepted++; dilatedGreen++;
                    return true;
                }
            }
            else
            {
                CarpetEvidence evidence = MeasureCarpetEvidence(binding, uv);
                int requiredGreen = Mathf.Max(1, Mathf.CeilToInt(
                    CarpetEvidenceOffsets.Length * carpetMinimumGreenFraction));
                bool surrounded = evidence.GreenSamples >= requiredGreen &&
                    evidence.Sectors >= carpetMinimumSectors;
                bool supportedDirect = direct && immediateGreen >= 2;
                // Retain a narrow authored seam softening band, but require
                // support on both sides of its local half-disk. The larger
                // carpet radius may bridge a brown hole only with green in at
                // least three quadrants.
                bool supportedDilation = !direct && dilated &&
                    evidence.GreenSamples >= Mathf.Max(2,
                        Mathf.CeilToInt(requiredGreen * 0.5f)) &&
                    evidence.Sectors >= 2;
                if (surrounded || supportedDirect || supportedDilation)
                {
                    accepted++;
                    if (direct) directGreen++;
                    else if (dilated) dilatedGreen++;
                    else carpetGreen++;
                    return true;
                }
            }
            nonGreen++;
            reason = "GrassTextureNotGreen:" + source;
            return false;
        }

        private CarpetEvidence MeasureCarpetEvidence(Binding binding,
            Vector2 uv)
        {
            int green = 0, sectors = 0;
            foreach (Vector2 offset in CarpetEvidenceOffsets)
            {
                int x = Mathf.RoundToInt(offset.x * carpetRadiusPixels);
                int y = Mathf.RoundToInt(offset.y * carpetRadiusPixels);
                if (!IsGreen(binding.SampleTexelOffset(uv, x, y))) continue;
                green++;
                int sector = offset.x >= 0f
                    ? offset.y >= 0f ? 0 : 3
                    : offset.y >= 0f ? 1 : 2;
                sectors |= 1 << sector;
            }
            return new CarpetEvidence(green, CountBits(sectors));
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0)
            {
                count += value & 1;
                value >>= 1;
            }
            return count;
        }

        private readonly struct CarpetEvidence
        {
            public readonly int GreenSamples, Sectors;
            public CarpetEvidence(int greenSamples, int sectors)
            {
                GreenSamples = greenSamples;
                Sectors = sectors;
            }
        }

        private bool IsGreen(Color rgb)
        {
            float maximum = Mathf.Max(rgb.r, Mathf.Max(rgb.g, rgb.b));
            float minimum = Mathf.Min(rgb.r, Mathf.Min(rgb.g, rgb.b));
            float colourSaturation = maximum > 0f ? (maximum - minimum) / maximum : 0f;
            return rgb.g - Mathf.Max(rgb.r, rgb.b) + 0.000001f >= greenExcess &&
                colourSaturation >= saturation && maximum >= value;
        }

        private static string Identity(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return !string.IsNullOrEmpty(path) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId)
                ? guid + ":" + localId + ":" + AssetDatabase.GetAssetDependencyHash(path)
                : "temporary:" + asset.GetInstanceID();
        }

        private static string StableIdentity(UnityEngine.Object asset)
        {
            string path = AssetDatabase.GetAssetPath(asset);
            return !string.IsNullOrEmpty(path) ? Identity(asset) : "nonpersistent:" + asset.GetType().FullName + ":" + asset.name;
        }

        private static Pixels ReadPixels(Texture2D texture)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            string extension = Path.GetExtension(path).ToLowerInvariant();
            // Decode source PNG/JPEG at full resolution, independent of platform
            // compression and current mip residency. This is an authoring mask,
            // not an attempt to reproduce a distance-dependent rendered pixel.
            if (File.Exists(path) && (extension == ".png" || extension == ".jpg" || extension == ".jpeg"))
            {
                byte[] bytes = File.ReadAllBytes(path);
                var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                try
                {
                    if (!ImageConversion.LoadImage(decoded, bytes, false)) throw new InvalidDataException("BaseTextureDecodeFailed:" + path);
                    using var sha = SHA256.Create();
                    return new Pixels(decoded.width, decoded.height, decoded.GetPixels32(), Hex(sha.ComputeHash(bytes)));
                }
                finally { UnityEngine.Object.DestroyImmediate(decoded); }
            }
            if (!texture.isReadable) throw new InvalidDataException("UnsupportedUnreadableTextureFormat:" + extension);
            Color32[] colours = texture.GetPixels32(0);
            string digest = Hash(writer =>
            {
                writer.Write(texture.width); writer.Write(texture.height);
                foreach (Color32 colour in colours) { writer.Write(colour.r); writer.Write(colour.g); writer.Write(colour.b); writer.Write(colour.a); }
            });
            return new Pixels(texture.width, texture.height, colours, digest);
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        private static string Hash(Action<BinaryWriter> write)
        {
            using var hash = SHA256.Create();
            using var stream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
            using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
            write(writer); writer.Flush(); stream.FlushFinalBlock();
            return Hex(hash.Hash);
        }

        internal sealed class Pixels
        {
            public readonly int Width, Height;
            public readonly Color32[] Colours;
            public readonly string Hash;
            public Pixels(int width, int height, Color32[] colours, string hash)
            { Width = width; Height = height; Colours = colours; Hash = hash; }
        }

        internal sealed class Binding
        {
            private readonly Pixels pixels;
            private readonly Vector2 scale, offset;
            private readonly TextureWrapMode wrapU, wrapV;
            private readonly FilterMode filter;
            private readonly Color tint;
            public string Fingerprint { get; }
            public string Error { get; }
            public string Description { get; }
            public bool Supported => pixels != null;
            public Binding(string error) { Error = error; Fingerprint = "unsupported:" + error; Description = Fingerprint; }
            public Binding(Pixels pixels, Vector2 scale, Vector2 offset, TextureWrapMode wrapU, TextureWrapMode wrapV,
                FilterMode filter, Color tint, string property, string texturePath)
            {
                this.pixels = pixels; this.scale = scale; this.offset = offset; this.wrapU = wrapU; this.wrapV = wrapV; this.filter = filter; this.tint = tint;
                Description = $"Supported UV0 {property} texture={texturePath} {pixels.Width}x{pixels.Height} ST={scale}/{offset} wrap={wrapU}/{wrapV} filter={filter}; full-resolution source sRGB";
                Fingerprint = Hash(writer =>
                {
                    writer.Write(pixels.Hash); writer.Write(pixels.Width); writer.Write(pixels.Height); writer.Write(property);
                    writer.Write(scale.x); writer.Write(scale.y); writer.Write(offset.x); writer.Write(offset.y);
                    writer.Write((int)wrapU); writer.Write((int)wrapV); writer.Write((int)filter);
                    writer.Write(tint.r); writer.Write(tint.g); writer.Write(tint.b);
                });
            }
            public Color Sample(Vector2 uv)
            {
                uv = Vector2.Scale(uv, scale) + offset;
                return SampleTransformed(uv, 0, 0);
            }
            public Color SampleTexelOffset(Vector2 uv, int xOffset, int yOffset)
            {
                uv = Vector2.Scale(uv, scale) + offset;
                return SampleTransformed(uv, xOffset, yOffset);
            }
            private Color SampleTransformed(Vector2 uv, int xOffset, int yOffset)
            {
                float x = Wrap(uv.x, wrapU) * pixels.Width + xOffset;
                float y = Wrap(uv.y, wrapV) * pixels.Height + yOffset;
                if (filter == FilterMode.Point) return Pixel(Mathf.FloorToInt(x), Mathf.FloorToInt(y)) * tint;
                x -= 0.5f; y -= 0.5f;
                int left = Mathf.FloorToInt(x), bottom = Mathf.FloorToInt(y);
                return Color.LerpUnclamped(Color.LerpUnclamped(Pixel(left, bottom), Pixel(left + 1, bottom), x - left),
                    Color.LerpUnclamped(Pixel(left, bottom + 1), Pixel(left + 1, bottom + 1), x - left), y - bottom) * tint;
            }
            private Color Pixel(int x, int y) => pixels.Colours[Index(y, pixels.Height, wrapV) * pixels.Width + Index(x, pixels.Width, wrapU)];
            private static int Index(int index, int count, TextureWrapMode wrap)
            {
                if (wrap == TextureWrapMode.Repeat) return (index % count + count) % count;
                // Mirrored UVs have already been folded into [0,1]; at a mirror
                // seam the adjacent texel is the same edge texel, as for clamp.
                return Mathf.Clamp(index, 0, count - 1);
            }
            private static float Wrap(float coordinate, TextureWrapMode wrap)
            {
                if (wrap == TextureWrapMode.Repeat) return coordinate - Mathf.Floor(coordinate);
                if (wrap == TextureWrapMode.Mirror) return 1f - Mathf.Abs(Mathf.Repeat(coordinate, 2f) - 1f);
                if (wrap == TextureWrapMode.MirrorOnce) return Mathf.Clamp01(Mathf.Abs(coordinate));
                return Mathf.Clamp01(coordinate);
            }
        }
    }
}
