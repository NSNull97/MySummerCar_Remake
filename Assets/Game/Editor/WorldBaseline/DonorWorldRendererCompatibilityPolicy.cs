using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace MSC.Editor.WorldBaseline
{
    /// <summary>
    /// Deterministic Phase 1 lighting policy for generated donor-world
    /// renderers. It is deliberately category/material/geometry based so
    /// regeneration does not depend on scene names at runtime.
    /// </summary>
    public static class DonorWorldRendererCompatibilityPolicy
    {
        public readonly struct Settings
        {
            public Settings(
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows,
                LightProbeUsage lightProbeUsage,
                ReflectionProbeUsage reflectionProbeUsage)
            {
                ShadowCastingMode = shadowCastingMode;
                ReceiveShadows = receiveShadows;
                LightProbeUsage = lightProbeUsage;
                ReflectionProbeUsage = reflectionProbeUsage;
            }

            public ShadowCastingMode ShadowCastingMode { get; }
            public bool ReceiveShadows { get; }
            public LightProbeUsage LightProbeUsage { get; }
            public ReflectionProbeUsage ReflectionProbeUsage { get; }
        }

        private const float MinimumGroundShortAxisMeters = 1.5f;
        private const float MinimumGroundLongAxisMeters = 4f;
        private const float GroundThicknessToleranceMeters = 0.5f;
        private const float MaximumGroundThicknessRatio = 0.25f;

        private static readonly HashSet<string> AlwaysNonCastingCategories =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "ColliderOnly",
                "SpawnMarker",
                "VegetationGrass",
                "Water",
                "Wire"
            };

        private static readonly HashSet<string> GroundSurfaceCategories =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "Field",
                "Floor",
                "Road",
                "Terrain"
            };

        private static readonly HashSet<string>
            ReviewedStaticPropEntityIds =
                new HashSet<string>(StringComparer.Ordinal)
                {
                    // Physical well shells misclassified by the donor's
                    // broad Water category.
                    "8eb5122d77e12287ba36daef701c884e",
                    "6737e72920f4e1d521c172dfb330840f",

                    // Fixed construction-site cable reels misclassified by
                    // the broad Wire category.
                    "6d4a387bd94c67634227c3563884f648",
                    "0d702ba7bf60435734f019cf21ff0459"
                };

        public static Settings Evaluate(
            WorldBaselineSanitationEntry entry,
            IReadOnlyList<string> sourceMaterialSlots,
            DonorWorldMaterialTexturePlan materialPlan)
        {
            if (entry == null)
            {
                throw new ArgumentNullException(nameof(entry));
            }

            if (sourceMaterialSlots == null)
            {
                throw new ArgumentNullException(nameof(sourceMaterialSlots));
            }

            if (materialPlan == null)
            {
                throw new ArgumentNullException(nameof(materialPlan));
            }

            bool hasLitSurface = false;
            bool hasOpaqueShadowCaster = false;
            bool requiresTwoSidedShadow = false;
            for (int index = 0; index < sourceMaterialSlots.Count; index++)
            {
                string sourceGuid = sourceMaterialSlots[index];
                if (string.Equals(
                        sourceGuid,
                        DonorWorldMaterialTexturePlan.BuiltInFallbackGuid,
                        StringComparison.Ordinal) ||
                    !materialPlan.Materials.TryGetValue(
                        sourceGuid,
                        out DonorWorldSourceMaterial source))
                {
                    continue;
                }

                bool lit = source.CompatibilityClass is not
                    (DonorWorldCompatibilityClass.Unlit or
                     DonorWorldCompatibilityClass.TransparentUnlit or
                     DonorWorldCompatibilityClass.Unsupported);
                hasLitSurface |= lit;
                hasOpaqueShadowCaster |= source.CompatibilityClass is
                    DonorWorldCompatibilityClass.OpaqueLit or
                    DonorWorldCompatibilityClass.AlphaClipLit or
                    DonorWorldCompatibilityClass.EmissiveLit;
                requiresTwoSidedShadow |=
                    source.DoubleSided ||
                    source.CompatibilityClass ==
                    DonorWorldCompatibilityClass.AlphaClipLit;
            }

            string category = ResolveSemanticCategory(
                entry.Placement.StableId,
                entry.Placement.Category);
            bool confidentGroundSurface =
                IsConfidentGroundSurface(entry, category);
            bool hasReviewedMatteGroundMaterial =
                sourceMaterialSlots.Any(sourceGuid =>
                    DonorWorldMaterialTexturePipeline
                        .LegacyDiffuseDetailSurfaceGuids.Contains(
                            sourceGuid));
            bool casts = hasOpaqueShadowCaster &&
                         !AlwaysNonCastingCategories.Contains(category) &&
                         !confidentGroundSurface;
            ShadowCastingMode castingMode = !casts
                ? ShadowCastingMode.Off
                : requiresTwoSidedShadow ||
                  string.Equals(category, "Roof", StringComparison.Ordinal)
                    ? ShadowCastingMode.TwoSided
                    : ShadowCastingMode.On;

            bool receives = hasLitSurface &&
                            !string.Equals(
                                category,
                                "Water",
                                StringComparison.Ordinal);
            return new Settings(
                castingMode,
                receives,
                hasLitSurface
                    ? LightProbeUsage.BlendProbes
                    : LightProbeUsage.Off,
                hasLitSurface &&
                !confidentGroundSurface &&
                !hasReviewedMatteGroundMaterial
                    ? ReflectionProbeUsage.Simple
                    : ReflectionProbeUsage.Off);
        }

        internal static string ResolveSemanticCategory(
            string entityStableId,
            string sourceCategory)
        {
            return ReviewedStaticPropEntityIds.Contains(
                entityStableId ?? string.Empty)
                    ? "StaticProp"
                    : sourceCategory ?? string.Empty;
        }

        private static bool IsConfidentGroundSurface(
            WorldBaselineSanitationEntry entry,
            string category)
        {
            if (!GroundSurfaceCategories.Contains(category))
            {
                return false;
            }

            // Donor semantic categories are noisy: tunnels and sign faces can
            // be tagged Road, while floor-jack parts can be tagged Floor. Only
            // suppress casting when the measured bounds prove a broad, thin
            // horizontal surface. Missing/review-only point bounds therefore
            // fail safe to casting shadows.
            Vector3 size = entry.Placement.Bounds.size;
            float sizeX = Mathf.Abs(size.x);
            float sizeY = Mathf.Abs(size.y);
            float sizeZ = Mathf.Abs(size.z);
            if (!float.IsFinite(sizeX) ||
                !float.IsFinite(sizeY) ||
                !float.IsFinite(sizeZ))
            {
                return false;
            }

            float shortHorizontalAxis = Mathf.Min(sizeX, sizeZ);
            float longHorizontalAxis = Mathf.Max(sizeX, sizeZ);
            if (shortHorizontalAxis < MinimumGroundShortAxisMeters ||
                longHorizontalAxis < MinimumGroundLongAxisMeters)
            {
                return false;
            }

            float permittedThickness = Mathf.Max(
                GroundThicknessToleranceMeters,
                shortHorizontalAxis * MaximumGroundThicknessRatio);
            return sizeY <= permittedThickness;
        }

        public static void Apply(
            MeshRenderer renderer,
            WorldBaselineSanitationEntry entry,
            IReadOnlyList<string> sourceMaterialSlots,
            DonorWorldMaterialTexturePlan materialPlan)
        {
            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            Settings settings = Evaluate(
                entry,
                sourceMaterialSlots,
                materialPlan);
            renderer.shadowCastingMode = settings.ShadowCastingMode;
            renderer.receiveShadows = settings.ReceiveShadows;
            renderer.lightProbeUsage = settings.LightProbeUsage;
            renderer.reflectionProbeUsage = settings.ReflectionProbeUsage;
        }
    }
}
