using System;
using UnityEngine;

namespace MSC.Editor.Vegetation
{
    internal enum MapVegetationTreeMaterialRole
    {
        Foliage,
        Bark,
        Billboard
    }

    /// <summary>
    /// Project-owned PBR calibration for the temporary Phase 1 tree presentation.
    /// The source packages provide geometry and texture identity; their legacy
    /// metallic/gloss values and scene-lighting tints are deliberately not copied.
    /// </summary>
    internal readonly struct MapVegetationTreeMaterialPolicy
    {
        public const float FoliageSpecularF0 = 0.018f;
        public const float BillboardSpecularF0 = 0.012f;

        public MapVegetationTreeMaterialPolicy(
            string species,
            MapVegetationTreeMaterialRole role,
            Color tint,
            float smoothness,
            float specularF0,
            float normalScale,
            bool doubleSided)
        {
            if (string.IsNullOrWhiteSpace(species))
            {
                throw new ArgumentException("Tree species is required.", nameof(species));
            }
            if (smoothness < 0f || smoothness > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(smoothness));
            }
            if (specularF0 < 0f || specularF0 > 0.08f)
            {
                throw new ArgumentOutOfRangeException(nameof(specularF0));
            }
            if (normalScale < 0f || normalScale > 2f)
            {
                throw new ArgumentOutOfRangeException(nameof(normalScale));
            }

            Species = species;
            Role = role;
            Tint = new Color(
                Mathf.Clamp01(tint.r),
                Mathf.Clamp01(tint.g),
                Mathf.Clamp01(tint.b),
                1f);
            Smoothness = smoothness;
            SpecularF0 = specularF0;
            NormalScale = normalScale;
            DoubleSided = doubleSided;
        }

        public string Species { get; }
        public MapVegetationTreeMaterialRole Role { get; }
        public Color Tint { get; }
        public float Smoothness { get; }
        public float SpecularF0 { get; }
        public float NormalScale { get; }
        public bool DoubleSided { get; }
        public bool AlphaClipped => Role != MapVegetationTreeMaterialRole.Bark;
        public bool UsesSpecularColor => AlphaClipped;

        /// <summary>
        /// A deterministic upper-bound proxy for sun-facing whitening. It uses
        /// the authored tint at a unit-white texel plus the calibrated F0 lobe;
        /// the real albedo texture can only lower the diffuse term.
        /// </summary>
        public float SunFacingWhiteningRisk =>
            RelativeLuminance(Tint) +
            SpecularF0 * Mathf.Lerp(0.35f, 1f, Smoothness);

        public static MapVegetationTreeMaterialPolicy Create(
            string species,
            MapVegetationTreeMaterialRole role,
            Color sourceTint)
        {
            switch (role)
            {
                case MapVegetationTreeMaterialRole.Foliage:
                    return new MapVegetationTreeMaterialPolicy(
                        species,
                        role,
                        FoliageTint(species),
                        0.045f,
                        FoliageSpecularF0,
                        0.72f,
                        true);
                case MapVegetationTreeMaterialRole.Billboard:
                    return new MapVegetationTreeMaterialPolicy(
                        species,
                        role,
                        BillboardTint(species),
                        0.02f,
                        BillboardSpecularF0,
                        0f,
                        true);
                case MapVegetationTreeMaterialRole.Bark:
                    return new MapVegetationTreeMaterialPolicy(
                        species,
                        role,
                        BarkTint(species, sourceTint),
                        species == "Pine" || species == "Spruce" ? 0.12f : 0.09f,
                        0.04f,
                        0.85f,
                        false);
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, null);
            }
        }

        private static Color FoliageTint(string species)
        {
            switch (species)
            {
                // Restrained boreal-summer palette: conifers stay deep and cool,
                // deciduous species are greener without the vendor's yellow cast.
                case "Spruce": return new Color(0.66f, 0.76f, 0.60f, 1f);
                case "Pine": return new Color(0.70f, 0.78f, 0.60f, 1f);
                case "Birch": return new Color(0.66f, 0.80f, 0.58f, 1f);
                case "Aspen": return new Color(0.64f, 0.78f, 0.56f, 1f);
                default: throw UnknownSpecies(species);
            }
        }

        private static Color BillboardTint(string species)
        {
            switch (species)
            {
                // Billboards contain both crown and trunk, so their correction is
                // deliberately more neutral than the foliage-only material.
                case "Spruce": return new Color(0.70f, 0.78f, 0.65f, 1f);
                case "Pine": return new Color(0.74f, 0.79f, 0.65f, 1f);
                case "Birch": return new Color(0.82f, 0.87f, 0.75f, 1f);
                case "Aspen": return new Color(0.76f, 0.83f, 0.68f, 1f);
                default: throw UnknownSpecies(species);
            }
        }

        private static Color BarkTint(string species, Color sourceTint)
        {
            Color multiplier;
            switch (species)
            {
                case "Spruce": multiplier = new Color(0.90f, 0.88f, 0.82f, 1f); break;
                case "Pine": multiplier = new Color(0.92f, 0.88f, 0.80f, 1f); break;
                case "Birch": multiplier = new Color(0.86f, 0.88f, 0.82f, 1f); break;
                case "Aspen": multiplier = new Color(0.82f, 0.86f, 0.78f, 1f); break;
                default: throw UnknownSpecies(species);
            }
            return new Color(
                sourceTint.r * multiplier.r,
                sourceTint.g * multiplier.g,
                sourceTint.b * multiplier.b,
                1f);
        }

        private static float RelativeLuminance(Color color) =>
            color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;

        private static ArgumentException UnknownSpecies(string species) =>
            new ArgumentException("Unknown tree material species: " + species, nameof(species));
    }
}
