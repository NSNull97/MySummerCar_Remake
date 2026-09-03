using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.UI.Presentation
{
    public enum UiIconKind
    {
        Play,
        Plus,
        Cross,
        Gear,
        Speaker,
        Gamepad,
        Display,
        Person,
        Wrench,
        Back,
        Check,
        Reset,
        Sun,
        Wallet,
        Thirst,
        Hunger,
        Stress,
        Urine,
        Fatigue,
        Dirtiness,
        Music,
        Folder,
        Credits,
        Braces,
    }

    /// <summary>
    /// Owns the bounded procedural sprites used by 08A. No approved-reference
    /// pixels or external icon packages enter the runtime project.
    /// </summary>
    internal sealed class UiVisualAssets : IDisposable
    {
        private readonly Dictionary<UiIconKind, Sprite> icons =
            new Dictionary<UiIconKind, Sprite>();
        private readonly List<UnityEngine.Object> ownedObjects =
            new List<UnityEngine.Object>();

        public UiVisualAssets()
        {
            UiFontResolution fontResolution = UiFontResolver.Resolve();
            TextFont = fontResolution.Font;
            FontSource = fontResolution.SourceLabel;
            UsesLegacyRuntimeFont = fontResolution.UsesLegacyRuntime;
            if (fontResolution.OwnsFont)
            {
                ownedObjects.Add(TextFont);
            }

            RoundedSprite = CreateRoundedSprite(
                UiThemeTokens.ProceduralSurfaceResolution,
                UiThemeTokens.CornerRadiusMediumPixels);
            RoundedBorderSprite = CreateRoundedBorderSprite(
                UiThemeTokens.ProceduralSurfaceResolution,
                UiThemeTokens.CornerRadiusMediumPixels,
                UiThemeTokens.BorderStrokePixels);
            CircleSprite = CreateRoundedSprite(
                UiThemeTokens.ProceduralSurfaceResolution,
                UiThemeTokens.CornerRadiusCircularPixels);
            TrackSprite = CreateRoundedSprite(
                UiThemeTokens.ProceduralTrackResolution,
                UiThemeTokens.CornerRadiusSmallPixels);
            NeedGradientSprite = CreateHorizontalGradientSprite(
                64,
                UiThemeTokens.TextPrimary,
                UiThemeTokens.Accent);
        }

        public Font TextFont { get; }

        public string FontSource { get; }

        public bool UsesLegacyRuntimeFont { get; }

        public bool UsesFractionalAlphaCoverage { get; private set; }

        public Sprite RoundedSprite { get; }

        public Sprite RoundedBorderSprite { get; }

        public Sprite CircleSprite { get; }

        public Sprite TrackSprite { get; }

        public Sprite NeedGradientSprite { get; }

        public Sprite GetIcon(UiIconKind kind)
        {
            if (icons.TryGetValue(kind, out Sprite existing))
            {
                return existing;
            }

            Sprite created = TryCreateImportedNeedIcon(kind, out Sprite imported)
                ? imported
                : CreateIcon(kind, UiThemeTokens.ProceduralIconResolution);
            icons.Add(kind, created);
            return created;
        }

        private bool TryCreateImportedNeedIcon(
            UiIconKind kind,
            out Sprite sprite)
        {
            string resourceName = kind switch
            {
                UiIconKind.Thirst => "droplet",
                UiIconKind.Hunger => "utensils",
                UiIconKind.Stress => "brain",
                UiIconKind.Urine => "toilet",
                UiIconKind.Fatigue => "bed-double",
                UiIconKind.Dirtiness => "sparkles",
                _ => string.Empty,
            };
            if (string.IsNullOrEmpty(resourceName))
            {
                sprite = null;
                return false;
            }

            Texture2D texture = Resources.Load<Texture2D>(
                "Icons/Needs/" + resourceName);
            if (texture == null)
            {
                sprite = null;
                return false;
            }

            sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 100f,
                extrude: 0,
                SpriteMeshType.FullRect);
            sprite.name = "UI08A_Lucide_" + resourceName;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            ownedObjects.Add(sprite);
            return true;
        }

        public void Dispose()
        {
            for (int index = ownedObjects.Count - 1; index >= 0; index--)
            {
                UnityEngine.Object value = ownedObjects[index];
                if (value == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(value);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(value);
                }
            }

            ownedObjects.Clear();
            icons.Clear();
        }

        private Sprite CreateRoundedSprite(int size, int radius)
        {
            int supersample = UiThemeTokens.ProceduralSupersampleFactor;
            int rasterSize = size * supersample;
            var raster = new Color32[rasterSize * rasterSize];
            Color32 white = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(255, 255, 255, 0);
            for (int y = 0; y < rasterSize; y++)
            {
                for (int x = 0; x < rasterSize; x++)
                {
                    float logicalX = (x + 0.5f) / supersample;
                    float logicalY = (y + 0.5f) / supersample;
                    float nearestX = Mathf.Clamp(logicalX, radius, size - radius);
                    float nearestY = Mathf.Clamp(logicalY, radius, size - radius);
                    float dx = logicalX - nearestX;
                    float dy = logicalY - nearestY;
                    raster[y * rasterSize + x] = dx * dx + dy * dy <= radius * radius
                        ? white
                        : clear;
                }
            }

            return CreateSprite(
                DownsampleCoverage(raster, rasterSize, rasterSize, supersample),
                size,
                size,
                new Vector4(radius, radius, radius, radius),
                "UI08A_Rounded");
        }

        private Sprite CreateHorizontalGradientSprite(
            int width,
            Color start,
            Color end)
        {
            int clampedWidth = Mathf.Max(2, width);
            var pixels = new Color32[clampedWidth];
            for (int x = 0; x < clampedWidth; x++)
            {
                float normalized = x / (float)(clampedWidth - 1);
                pixels[x] = Color.Lerp(start, end, normalized);
            }

            return CreateSprite(
                pixels,
                clampedWidth,
                1,
                Vector4.zero,
                "UI08A_NeedGradient");
        }

        private Sprite CreateRoundedBorderSprite(
            int size,
            int radius,
            float thickness)
        {
            int supersample = UiThemeTokens.ProceduralSupersampleFactor;
            int rasterSize = size * supersample;
            var raster = new Color32[rasterSize * rasterSize];
            Color32 white = new Color32(255, 255, 255, 255);
            Color32 clear = new Color32(255, 255, 255, 0);
            float innerInset = Mathf.Clamp(thickness, 0.5f, radius - 0.5f);
            float innerRadius = Mathf.Max(0.5f, radius - innerInset);

            for (int y = 0; y < rasterSize; y++)
            {
                for (int x = 0; x < rasterSize; x++)
                {
                    float logicalX = (x + 0.5f) / supersample;
                    float logicalY = (y + 0.5f) / supersample;
                    bool insideOuter = IsInsideRoundedRect(
                        logicalX,
                        logicalY,
                        0f,
                        size,
                        radius);
                    bool insideInner = IsInsideRoundedRect(
                        logicalX,
                        logicalY,
                        innerInset,
                        size - innerInset,
                        innerRadius);
                    raster[y * rasterSize + x] = insideOuter && !insideInner
                        ? white
                        : clear;
                }
            }

            return CreateSprite(
                DownsampleCoverage(raster, rasterSize, rasterSize, supersample),
                size,
                size,
                new Vector4(radius, radius, radius, radius),
                "UI08A_RoundedBorder");
        }

        private static bool IsInsideRoundedRect(
            float x,
            float y,
            float minimum,
            float maximum,
            float radius)
        {
            if (x < minimum || x > maximum || y < minimum || y > maximum)
            {
                return false;
            }

            float nearestX = Mathf.Clamp(x, minimum + radius, maximum - radius);
            float nearestY = Mathf.Clamp(y, minimum + radius, maximum - radius);
            float dx = x - nearestX;
            float dy = y - nearestY;
            return dx * dx + dy * dy <= radius * radius;
        }

        private Sprite CreateIcon(UiIconKind kind, int size)
        {
            int supersample = UiThemeTokens.ProceduralSupersampleFactor;
            int rasterSize = size * supersample;
            var raster = new Color32[rasterSize * rasterSize];
            Color32 white = new Color32(255, 255, 255, 255);

            void Line(int x0, int y0, int x1, int y1, int thickness = 1)
            {
                float startX = (x0 + 0.5f) * supersample;
                float startY = (y0 + 0.5f) * supersample;
                float endX = (x1 + 0.5f) * supersample;
                float endY = (y1 + 0.5f) * supersample;
                float radiusPixels = (thickness + 0.52f) * supersample;
                int left = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(startX, endX) - radiusPixels));
                int right = Mathf.Min(rasterSize - 1, Mathf.CeilToInt(Mathf.Max(startX, endX) + radiusPixels));
                int bottom = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(startY, endY) - radiusPixels));
                int top = Mathf.Min(rasterSize - 1, Mathf.CeilToInt(Mathf.Max(startY, endY) + radiusPixels));
                float segmentX = endX - startX;
                float segmentY = endY - startY;
                float segmentLengthSquared = segmentX * segmentX + segmentY * segmentY;
                float radiusSquared = radiusPixels * radiusPixels;
                for (int rasterY = bottom; rasterY <= top; rasterY++)
                {
                    for (int rasterX = left; rasterX <= right; rasterX++)
                    {
                        float pointX = rasterX + 0.5f;
                        float pointY = rasterY + 0.5f;
                        float projection = segmentLengthSquared <= Mathf.Epsilon
                            ? 0f
                            : Mathf.Clamp01(
                                ((pointX - startX) * segmentX +
                                 (pointY - startY) * segmentY) /
                                segmentLengthSquared);
                        float nearestX = startX + segmentX * projection;
                        float nearestY = startY + segmentY * projection;
                        float dx = pointX - nearestX;
                        float dy = pointY - nearestY;
                        if (dx * dx + dy * dy <= radiusSquared)
                        {
                            raster[rasterY * rasterSize + rasterX] = white;
                        }
                    }
                }
            }

            void Circle(int centerX, int centerY, int radius, int thickness = 1)
            {
                float logicalCenterX = centerX + 0.5f;
                float logicalCenterY = centerY + 0.5f;
                float outerRadius = radius + thickness + 0.52f;
                float innerRadius = Mathf.Max(0f, radius - thickness - 0.52f);
                float centerRasterX = logicalCenterX * supersample;
                float centerRasterY = logicalCenterY * supersample;
                float outerPixels = outerRadius * supersample;
                float innerPixels = innerRadius * supersample;
                int left = Mathf.Max(0, Mathf.FloorToInt(centerRasterX - outerPixels));
                int right = Mathf.Min(rasterSize - 1, Mathf.CeilToInt(centerRasterX + outerPixels));
                int bottom = Mathf.Max(0, Mathf.FloorToInt(centerRasterY - outerPixels));
                int top = Mathf.Min(rasterSize - 1, Mathf.CeilToInt(centerRasterY + outerPixels));
                float outerSquared = outerPixels * outerPixels;
                float innerSquared = innerPixels * innerPixels;
                for (int rasterY = bottom; rasterY <= top; rasterY++)
                {
                    for (int rasterX = left; rasterX <= right; rasterX++)
                    {
                        float dx = rasterX + 0.5f - centerRasterX;
                        float dy = rasterY + 0.5f - centerRasterY;
                        float distanceSquared = dx * dx + dy * dy;
                        if (distanceSquared <= outerSquared && distanceSquared >= innerSquared)
                        {
                            raster[rasterY * rasterSize + rasterX] = white;
                        }
                    }
                }
            }

            void Rect(int left, int bottom, int right, int top, int thickness = 1)
            {
                Line(left, bottom, right, bottom, thickness);
                Line(right, bottom, right, top, thickness);
                Line(right, top, left, top, thickness);
                Line(left, top, left, bottom, thickness);
            }

            switch (kind)
            {
                case UiIconKind.Play:
                    for (int x = 9; x <= 23; x++)
                    {
                        int half = Mathf.RoundToInt((x - 9) * 0.62f);
                        Line(x, 16 - half, x, 16 + half, 0);
                    }
                    break;
                case UiIconKind.Plus:
                    Line(7, 16, 25, 16, 2);
                    Line(16, 7, 16, 25, 2);
                    break;
                case UiIconKind.Cross:
                    Line(8, 8, 24, 24, 2);
                    Line(8, 24, 24, 8, 2);
                    break;
                case UiIconKind.Back:
                    Line(7, 16, 24, 16, 1);
                    Line(7, 16, 14, 9, 1);
                    Line(7, 16, 14, 23, 1);
                    break;
                case UiIconKind.Check:
                    Line(6, 16, 13, 23, 2);
                    Line(13, 23, 27, 8, 2);
                    break;
                case UiIconKind.Reset:
                    Circle(16, 16, 9, 1);
                    Line(6, 21, 6, 12, 1);
                    Line(6, 21, 14, 21, 1);
                    break;
                case UiIconKind.Display:
                    Rect(5, 9, 27, 23, 1);
                    Line(16, 8, 16, 4, 1);
                    Line(10, 4, 22, 4, 1);
                    break;
                case UiIconKind.Speaker:
                    Rect(5, 12, 11, 20, 1);
                    Line(11, 12, 19, 7, 1);
                    Line(19, 7, 19, 25, 1);
                    Line(19, 25, 11, 20, 1);
                    Circle(18, 16, 8, 1);
                    break;
                case UiIconKind.Gamepad:
                    Rect(6, 10, 26, 22, 2);
                    Line(9, 16, 15, 16, 1);
                    Line(12, 13, 12, 19, 1);
                    Circle(21, 16, 2, 1);
                    break;
                case UiIconKind.Gear:
                    Circle(16, 16, 10, 2);
                    Circle(16, 16, 4, 1);
                    for (int angle = 0; angle < 360; angle += 45)
                    {
                        float radians = angle * Mathf.Deg2Rad;
                        Line(
                            16 + Mathf.RoundToInt(Mathf.Cos(radians) * 10),
                            16 + Mathf.RoundToInt(Mathf.Sin(radians) * 10),
                            16 + Mathf.RoundToInt(Mathf.Cos(radians) * 14),
                            16 + Mathf.RoundToInt(Mathf.Sin(radians) * 14),
                            1);
                    }
                    break;
                case UiIconKind.Person:
                    Circle(16, 23, 5, 1);
                    Line(16, 17, 16, 5, 2);
                    Line(8, 13, 24, 13, 1);
                    Line(16, 6, 10, 1, 1);
                    Line(16, 6, 22, 1, 1);
                    break;
                case UiIconKind.Wrench:
                    Line(7, 7, 24, 24, 2);
                    Circle(8, 7, 4, 1);
                    Line(21, 27, 27, 21, 2);
                    break;
                case UiIconKind.Sun:
                    Circle(16, 16, 6, 1);
                    for (int angle = 0; angle < 360; angle += 45)
                    {
                        float radians = angle * Mathf.Deg2Rad;
                        Line(
                            16 + Mathf.RoundToInt(Mathf.Cos(radians) * 10),
                            16 + Mathf.RoundToInt(Mathf.Sin(radians) * 10),
                            16 + Mathf.RoundToInt(Mathf.Cos(radians) * 14),
                            16 + Mathf.RoundToInt(Mathf.Sin(radians) * 14),
                            1);
                    }
                    break;
                case UiIconKind.Wallet:
                    Rect(5, 7, 27, 23, 1);
                    Rect(18, 12, 29, 19, 1);
                    break;
                case UiIconKind.Thirst:
                    Circle(16, 10, 8, 1);
                    Line(8, 12, 16, 27, 1);
                    Line(24, 12, 16, 27, 1);
                    break;
                case UiIconKind.Hunger:
                    Circle(12, 16, 7, 1);
                    Line(23, 7, 23, 25, 1);
                    Line(26, 7, 26, 25, 1);
                    break;
                case UiIconKind.Stress:
                    Circle(16, 16, 11, 1);
                    Line(16, 16, 22, 21, 1);
                    break;
                case UiIconKind.Urine:
                    Rect(10, 8, 22, 24, 1);
                    Line(12, 16, 20, 16, 1);
                    break;
                case UiIconKind.Fatigue:
                    Circle(16, 16, 12, 1);
                    Line(8, 12, 13, 12, 1);
                    Line(18, 12, 23, 12, 1);
                    Line(10, 22, 22, 22, 1);
                    break;
                case UiIconKind.Dirtiness:
                    Circle(16, 16, 10, 1);
                    Line(9, 9, 23, 23, 1);
                    Line(23, 9, 9, 23, 1);
                    break;
                case UiIconKind.Music:
                    Line(21, 7, 21, 23, 1);
                    Line(21, 7, 10, 10, 1);
                    Circle(15, 23, 5, 1);
                    Circle(6, 26, 5, 1);
                    Line(10, 10, 10, 26, 1);
                    break;
                case UiIconKind.Folder:
                    Rect(5, 8, 27, 23, 1);
                    Line(5, 23, 13, 23, 1);
                    Line(13, 23, 16, 27, 1);
                    Line(16, 27, 27, 27, 1);
                    break;
                case UiIconKind.Credits:
                    Circle(11, 21, 5, 1);
                    Circle(22, 21, 5, 1);
                    Circle(16, 10, 7, 1);
                    break;
                case UiIconKind.Braces:
                    Line(12, 5, 8, 10, 1);
                    Line(8, 10, 8, 22, 1);
                    Line(8, 22, 12, 27, 1);
                    Line(20, 5, 24, 10, 1);
                    Line(24, 10, 24, 22, 1);
                    Line(24, 22, 20, 27, 1);
                    break;
                default:
                    Circle(16, 16, 10, 1);
                    break;
            }

            return CreateSprite(
                DownsampleCoverage(raster, rasterSize, rasterSize, supersample),
                size,
                size,
                Vector4.zero,
                "UI08A_" + kind);
        }

        private static Color32[] DownsampleCoverage(
            Color32[] raster,
            int rasterWidth,
            int rasterHeight,
            int factor)
        {
            int width = rasterWidth / factor;
            int height = rasterHeight / factor;
            int sampleCount = factor * factor;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int alpha = 0;
                    for (int sampleY = 0; sampleY < factor; sampleY++)
                    {
                        int rasterY = y * factor + sampleY;
                        for (int sampleX = 0; sampleX < factor; sampleX++)
                        {
                            int rasterX = x * factor + sampleX;
                            alpha += raster[rasterY * rasterWidth + rasterX].a;
                        }
                    }

                    byte averagedAlpha = (byte)Mathf.RoundToInt(alpha / (float)sampleCount);
                    pixels[y * width + x] = new Color32(255, 255, 255, averagedAlpha);
                }
            }

            return pixels;
        }

        private Sprite CreateSprite(
            Color32[] pixels,
            int width,
            int height,
            Vector4 border,
            string name)
        {
            for (int index = 0; index < pixels.Length; index++)
            {
                byte alpha = pixels[index].a;
                if (alpha > 0 && alpha < 255)
                {
                    UsesFractionalAlphaCoverage = true;
                    break;
                }
            }

            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: false)
            {
                name = name + "_Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            ownedObjects.Add(sprite);
            ownedObjects.Add(texture);
            return sprite;
        }
    }
}
