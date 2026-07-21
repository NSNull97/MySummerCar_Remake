using System;
using System.Linq;
using MSC.Editor.WorldBaseline;
using NUnit.Framework;
using UnityEngine.Rendering;

namespace MSC.Tests.EditMode.WorldBaseline
{
    public sealed class DonorWorldRendererCompatibilityPolicyEditModeTests
    {
        private const string HomeRoofStableId =
            "3f48c3f8da113c762f5ef4d32409e45c";
        private const string RailroadGroundStableId =
            "a22aca3591d398a904329d3a830b0722";

        [Test]
        public void HomeRoof_ReceivesAndCastsTwoSidedShadowsWithProbes()
        {
            DonorWorldMaterialTexturePlan materials =
                DonorWorldMaterialTexturePlan.Load();
            WorldBaselineSanitationEntry roof =
                WorldBaselineSanitationPlan.Load()
                    .Single(entry => string.Equals(
                        entry.Placement.StableId,
                        HomeRoofStableId,
                        StringComparison.Ordinal));

            DonorWorldRendererCompatibilityPolicy.Settings settings =
                DonorWorldRendererCompatibilityPolicy.Evaluate(
                    roof,
                    roof.SourceMaterialGuids,
                    materials);

            Assert.That(
                settings.ShadowCastingMode,
                Is.EqualTo(ShadowCastingMode.TwoSided));
            Assert.That(settings.ReceiveShadows, Is.True);
            Assert.That(
                settings.LightProbeUsage,
                Is.EqualTo(LightProbeUsage.BlendProbes));
            Assert.That(
                settings.ReflectionProbeUsage,
                Is.EqualTo(ReflectionProbeUsage.Simple));
        }

        [Test]
        public void GroundSurfaces_ReceiveButDoNotCastShadows()
        {
            DonorWorldMaterialTexturePlan materials =
                DonorWorldMaterialTexturePlan.Load();
            WorldBaselineSanitationEntry ground =
                WorldBaselineSanitationPlan.Load()
                    .Single(entry => string.Equals(
                        entry.Placement.StableId,
                        RailroadGroundStableId,
                        StringComparison.Ordinal));

            DonorWorldRendererCompatibilityPolicy.Settings settings =
                DonorWorldRendererCompatibilityPolicy.Evaluate(
                    ground,
                    ground.SourceMaterialGuids,
                    materials);

            Assert.That(
                settings.ShadowCastingMode,
                Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(settings.ReceiveShadows, Is.True);
        }

        [TestCase("955c924423eb767f622bd89b84c5c62c")]
        [TestCase("ee5d1d5e23a3699e1705340c676b0d75")]
        [TestCase("1284ca8b944bd0aabc5da61b523d7d54")]
        [TestCase("0723a966fbd21d7741e17c3517b19bdc")]
        public void OpaqueMisclassifiedObjects_StillCastShadows(string stableId)
        {
            DonorWorldMaterialTexturePlan materials =
                DonorWorldMaterialTexturePlan.Load();
            WorldBaselineSanitationEntry entry =
                WorldBaselineSanitationPlan.Load()
                    .Single(candidate => string.Equals(
                        candidate.Placement.StableId,
                        stableId,
                        StringComparison.Ordinal));

            Assert.That(entry.IncludeRenderer, Is.True);
            Assert.That(
                entry.SourceMaterialGuids.Any(guid =>
                    materials.Materials.TryGetValue(
                        guid,
                        out DonorWorldSourceMaterial source) &&
                    source.CompatibilityClass is
                        DonorWorldCompatibilityClass.OpaqueLit or
                        DonorWorldCompatibilityClass.AlphaClipLit or
                        DonorWorldCompatibilityClass.EmissiveLit),
                Is.True,
                "Regression fixture must retain an opaque shadow-capable material.");

            DonorWorldRendererCompatibilityPolicy.Settings settings =
                DonorWorldRendererCompatibilityPolicy.Evaluate(
                    entry,
                    entry.SourceMaterialGuids,
                    materials);

            Assert.That(
                settings.ShadowCastingMode,
                Is.Not.EqualTo(ShadowCastingMode.Off));
            Assert.That(settings.ReceiveShadows, Is.True);
        }

        [TestCase(
            "8eb5122d77e12287ba36daef701c884e",
            "Water")]
        [TestCase(
            "6737e72920f4e1d521c172dfb330840f",
            "Water")]
        [TestCase(
            "6d4a387bd94c67634227c3563884f648",
            "Wire")]
        [TestCase(
            "0d702ba7bf60435734f019cf21ff0459",
            "Wire")]
        public void ReviewedStaticProps_OverrideBroadSourceCategoryAndCast(
            string stableId,
            string sourceCategory)
        {
            DonorWorldMaterialTexturePlan materials =
                DonorWorldMaterialTexturePlan.Load();
            WorldBaselineSanitationEntry entry =
                WorldBaselineSanitationPlan.Load()
                    .Single(candidate => string.Equals(
                        candidate.Placement.StableId,
                        stableId,
                        StringComparison.Ordinal));

            Assert.That(entry.IncludeRenderer, Is.True);
            Assert.That(entry.Placement.Category, Is.EqualTo(sourceCategory));
            Assert.That(
                DonorWorldRendererCompatibilityPolicy
                    .ResolveSemanticCategory(
                        entry.Placement.StableId,
                        entry.Placement.Category),
                Is.EqualTo("StaticProp"));

            DonorWorldRendererCompatibilityPolicy.Settings settings =
                DonorWorldRendererCompatibilityPolicy.Evaluate(
                    entry,
                    entry.SourceMaterialGuids,
                    materials);

            Assert.That(
                settings.ShadowCastingMode,
                Is.Not.EqualTo(ShadowCastingMode.Off));
            Assert.That(settings.ReceiveShadows, Is.True);
        }

        [Test]
        public void Water_DoesNotCastOrReceiveWorldShadows()
        {
            DonorWorldMaterialTexturePlan materials =
                DonorWorldMaterialTexturePlan.Load();
            WorldBaselineSanitationEntry water =
                WorldBaselineSanitationPlan.Load()
                    .First(entry =>
                        entry.IncludeRenderer &&
                        string.Equals(
                            entry.Placement.Category,
                            "Water",
                            StringComparison.Ordinal));

            DonorWorldRendererCompatibilityPolicy.Settings settings =
                DonorWorldRendererCompatibilityPolicy.Evaluate(
                    water,
                    water.SourceMaterialGuids,
                    materials);

            Assert.That(
                settings.ShadowCastingMode,
                Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(settings.ReceiveShadows, Is.False);
        }
    }
}
