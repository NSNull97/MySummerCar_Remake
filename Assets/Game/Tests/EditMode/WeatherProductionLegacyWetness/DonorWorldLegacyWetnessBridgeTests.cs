using System.Collections.Generic;
using MSC.LegacyImport;
using MSC.Weather.Production.LegacyBaseline;
using MSC.Weather.Wetness;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WeatherProductionLegacyWetness
{
    public sealed class DonorWorldLegacyWetnessBridgeTests
    {
        private const string GroundGuid =
            "00000000000000000000000000000001";
        private const string RoadGuid =
            "00000000000000000000000000000002";
        private const string ExteriorGuid =
            "00000000000000000000000000000003";
        private const string VegetationGuid =
            "00000000000000000000000000000004";
        private const string TransparentGuid =
            "00000000000000000000000000000005";
        private const string UnlitGuid =
            "00000000000000000000000000000006";
        private const string EmissiveGuid =
            "00000000000000000000000000000007";
        private const string UnknownGuid =
            "00000000000000000000000000000008";
        private const string CategoryMismatchGuid =
            "00000000000000000000000000000009";

        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId =
            Shader.PropertyToID("_Smoothness");
        private static readonly int SurfaceTypeId =
            Shader.PropertyToID("_SurfaceType");
        private static readonly int AlphaCutoffEnableId =
            Shader.PropertyToID("_AlphaCutoffEnable");
        private static readonly int EmissiveColorId =
            Shader.PropertyToID("_EmissiveColor");
        private static readonly int SentinelId =
            Shader.PropertyToID("_MSC_TestSentinel");

        private readonly List<UnityEngine.Object> createdObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = createdObjects.Count - 1;
                 index >= 0;
                 index--)
            {
                UnityEngine.Object value = createdObjects[index];
                if (value != null)
                {
                    UnityEngine.Object.DestroyImmediate(value);
                }
            }

            createdObjects.Clear();
        }

        [Test]
        public void RefreshLoadedScenes_ReportsReviewedAndExcludedCoverage()
        {
            LegacyWetnessCoverageProfile profile = CreateProfile(
                Entry(GroundGuid, LegacyWetnessSurfaceCategory.Ground),
                Entry(VegetationGuid, LegacyWetnessSurfaceCategory.Vegetation),
                Entry(TransparentGuid, LegacyWetnessSurfaceCategory.Ground),
                Entry(UnlitGuid, LegacyWetnessSurfaceCategory.Exterior),
                Entry(EmissiveGuid, LegacyWetnessSurfaceCategory.Exterior),
                Entry(
                    CategoryMismatchGuid,
                    LegacyWetnessSurfaceCategory.Vegetation));
            DonorWorldLegacyWetnessBridge bridge = CreateBridge(profile);
            LegacyWetnessCoverageCounters before = bridge.Coverage;

            CreateBinding(
                new[]
                {
                    CreateLitMaterial(),
                    CreateLitMaterial(alphaClip: true),
                    CreateLitMaterial(transparent: true),
                    CreateUnlitMaterial(),
                    CreateLitMaterial(emissive: true),
                    CreateLitMaterial(),
                    CreateLitMaterial()
                },
                new[]
                {
                    GroundGuid,
                    VegetationGuid,
                    TransparentGuid,
                    UnlitGuid,
                    EmissiveGuid,
                    UnknownGuid,
                    CategoryMismatchGuid
                });

            bridge.RefreshLoadedScenes();
            LegacyWetnessCoverageCounters after = bridge.Coverage;

            Assert.That(
                after.RegisteredBindingCount -
                before.RegisteredBindingCount,
                Is.EqualTo(1));
            Assert.That(
                after.TotalSlotCount - before.TotalSlotCount,
                Is.EqualTo(7));
            Assert.That(
                after.OpaqueLitSlotCount -
                before.OpaqueLitSlotCount,
                Is.EqualTo(3));
            Assert.That(
                after.AlphaClipLitSlotCount -
                before.AlphaClipLitSlotCount,
                Is.EqualTo(1));
            Assert.That(
                after.ApprovedGroundSlotCount -
                before.ApprovedGroundSlotCount,
                Is.EqualTo(1));
            Assert.That(
                after.ApprovedVegetationSlotCount -
                before.ApprovedVegetationSlotCount,
                Is.EqualTo(1));
            Assert.That(
                after.ExcludedTransparentOrWaterSlotCount -
                before.ExcludedTransparentOrWaterSlotCount,
                Is.EqualTo(1));
            Assert.That(
                after.ExcludedUnlitSlotCount -
                before.ExcludedUnlitSlotCount,
                Is.EqualTo(1));
            Assert.That(
                after.ExcludedEmissiveSlotCount -
                before.ExcludedEmissiveSlotCount,
                Is.EqualTo(1));
            Assert.That(
                after.ExcludedNotAllowlistedSlotCount -
                before.ExcludedNotAllowlistedSlotCount,
                Is.EqualTo(1));
            Assert.That(
                after.ExcludedCategoryMismatchSlotCount -
                before.ExcludedCategoryMismatchSlotCount,
                Is.EqualTo(1));
        }

        [Test]
        public void Apply_IsCategorySpecific_PerSlot_AndPreservesForeignBlockData()
        {
            LegacyWetnessCoverageProfile profile = CreateProfile(
                Entry(GroundGuid, LegacyWetnessSurfaceCategory.Ground),
                Entry(VegetationGuid, LegacyWetnessSurfaceCategory.Vegetation));
            DonorWorldLegacyWetnessBridge bridge = CreateBridge(profile);
            Material groundMaterial = CreateLitMaterial(
                new Color(0.8f, 0.4f, 0.2f, 1f),
                0.3f);
            Material vegetationMaterial = CreateLitMaterial(
                new Color(0.2f, 0.6f, 0.4f, 1f),
                0.1f,
                alphaClip: true);
            BindingFixture fixture = CreateBinding(
                new[] { groundMaterial, vegetationMaterial },
                new[] { GroundGuid, VegetationGuid });

            var original = new MaterialPropertyBlock();
            var groundOverride = new Color(0.7f, 0.35f, 0.175f, 1f);
            original.SetColor(BaseColorId, groundOverride);
            original.SetFloat(SmoothnessId, 0.4f);
            original.SetFloat(SentinelId, 7f);
            fixture.Renderer.SetPropertyBlock(original, 0);
            bridge.RefreshLoadedScenes();

            bridge.Apply(CreateOutputs(
                revision: 1U,
                ground: 0.5f,
                road: 0.75f,
                puddle: 0.25f,
                vegetation: 1f));

            Material[] sharedAfter = fixture.Renderer.sharedMaterials;
            Assert.That(sharedAfter[0], Is.SameAs(groundMaterial));
            Assert.That(sharedAfter[1], Is.SameAs(vegetationMaterial));

            var block = new MaterialPropertyBlock();
            fixture.Renderer.GetPropertyBlock(block, 0);
            Color expectedGround = groundOverride * 0.92f;
            expectedGround.a = 1f;
            AssertColor(block.GetColor(BaseColorId), expectedGround);
            Assert.That(
                block.GetFloat(SmoothnessId),
                Is.EqualTo(0.425f).Within(0.0001f));
            Assert.That(block.GetFloat(SentinelId), Is.EqualTo(7f));

            block.Clear();
            fixture.Renderer.GetPropertyBlock(block, 1);
            var expectedVegetation =
                new Color(0.18f, 0.54f, 0.36f, 1f);
            AssertColor(block.GetColor(BaseColorId), expectedVegetation);
            Assert.That(
                block.GetFloat(SmoothnessId),
                Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(
                bridge.OpaqueWetSmoothness,
                Is.EqualTo(
                    DonorWorldLegacyWetnessBridge
                        .ProductionOpaqueWetSmoothness));
            Assert.That(
                bridge.AlphaClipWetSmoothness,
                Is.EqualTo(
                    DonorWorldLegacyWetnessBridge
                        .ProductionAlphaClipWetSmoothness));
            Assert.That(bridge.LastAppliedSlotCount, Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void Apply_SkipsPuddleOnlyAndSameBucketChanges()
        {
            LegacyWetnessCoverageProfile profile = CreateProfile(
                Entry(GroundGuid, LegacyWetnessSurfaceCategory.Ground));
            DonorWorldLegacyWetnessBridge bridge = CreateBridge(profile);
            CreateBinding(
                new[] { CreateLitMaterial() },
                new[] { GroundGuid });
            bridge.RefreshLoadedScenes();

            bridge.Apply(CreateOutputs(
                revision: 1U,
                ground: 0.5f,
                road: 0.5f,
                puddle: 0f,
                vegetation: 0.5f));
            ulong writesAfterFirstApply = bridge.PropertyBlockWriteCount;

            bridge.Apply(CreateOutputs(
                revision: 2U,
                ground: 0.505f,
                road: 0.505f,
                puddle: 1f,
                vegetation: 0.505f));

            Assert.That(
                bridge.PropertyBlockWriteCount,
                Is.EqualTo(writesAfterFirstApply));
            Assert.That(bridge.LastObservedRevision, Is.EqualTo(2U));
            Assert.That(bridge.LastAppliedRevision, Is.EqualTo(1U));

            bridge.Apply(CreateOutputs(
                revision: 3U,
                ground: 0.55f,
                road: 0.55f,
                puddle: 1f,
                vegetation: 0.55f));

            Assert.That(
                bridge.PropertyBlockWriteCount,
                Is.GreaterThan(writesAfterFirstApply));
            Assert.That(bridge.LastAppliedRevision, Is.EqualTo(3U));
        }

        [Test]
        public void ResetPresentation_RestoresExactOriginalSlotBlocks()
        {
            LegacyWetnessCoverageProfile profile = CreateProfile(
                Entry(GroundGuid, LegacyWetnessSurfaceCategory.Ground),
                Entry(RoadGuid, LegacyWetnessSurfaceCategory.Road));
            DonorWorldLegacyWetnessBridge bridge = CreateBridge(profile);
            BindingFixture fixture = CreateBinding(
                new[] { CreateLitMaterial(), CreateLitMaterial() },
                new[] { GroundGuid, RoadGuid });

            var original = new MaterialPropertyBlock();
            var originalColor = new Color(0.3f, 0.4f, 0.5f, 1f);
            original.SetColor(BaseColorId, originalColor);
            original.SetFloat(SmoothnessId, 0.37f);
            original.SetFloat(SentinelId, 11f);
            fixture.Renderer.SetPropertyBlock(original, 0);
            bridge.RefreshLoadedScenes();

            bridge.Apply(CreateOutputs(
                revision: 1U,
                ground: 1f,
                road: 1f,
                puddle: 1f,
                vegetation: 1f));
            bridge.ResetPresentation();

            var block = new MaterialPropertyBlock();
            fixture.Renderer.GetPropertyBlock(block, 0);
            AssertColor(block.GetColor(BaseColorId), originalColor);
            Assert.That(
                block.GetFloat(SmoothnessId),
                Is.EqualTo(0.37f).Within(0.0001f));
            Assert.That(block.GetFloat(SentinelId), Is.EqualTo(11f));

            block.Clear();
            fixture.Renderer.GetPropertyBlock(block, 1);
            Assert.That(block.isEmpty, Is.True);
        }

        [Test]
        public void RefreshLoadedScenes_ExcludesInactiveDiagnosticPresentation()
        {
            LegacyWetnessCoverageProfile profile = CreateProfile(
                Entry(ExteriorGuid, LegacyWetnessSurfaceCategory.Exterior));
            DonorWorldLegacyWetnessBridge bridge = CreateBridge(profile);
            LegacyWetnessCoverageCounters baseline = bridge.Coverage;
            Material textured = CreateLitMaterial();
            Material diagnostic = CreateUnlitMaterial();
            BindingFixture fixture = CreateBinding(
                new[] { textured },
                new[] { ExteriorGuid },
                new[] { diagnostic });
            bridge.RefreshLoadedScenes();

            Assert.That(
                bridge.Coverage.ApprovedExteriorSlotCount -
                baseline.ApprovedExteriorSlotCount,
                Is.EqualTo(1));
            bridge.Apply(CreateOutputs(1U, 1f, 1f, 0f, 1f));

            Assert.That(
                fixture.Binding.Apply(
                    DonorWorldLegacyPresentationMode.LegacyDiagnostic),
                Is.True);
            bridge.RefreshLoadedScenes();

            Assert.That(fixture.Renderer.sharedMaterial, Is.SameAs(diagnostic));
            Assert.That(
                bridge.Coverage.ApprovedExteriorSlotCount -
                baseline.ApprovedExteriorSlotCount,
                Is.EqualTo(0));
            Assert.That(
                bridge.Coverage.ExcludedInactivePresentationSlotCount -
                baseline.ExcludedInactivePresentationSlotCount,
                Is.EqualTo(1));
            var block = new MaterialPropertyBlock();
            fixture.Renderer.GetPropertyBlock(block, 0);
            Assert.That(block.isEmpty, Is.True);
        }

        private DonorWorldLegacyWetnessBridge CreateBridge(
            LegacyWetnessCoverageProfile profile)
        {
            var gameObject = new GameObject("Legacy Wetness Bridge Test");
            createdObjects.Add(gameObject);
            DonorWorldLegacyWetnessBridge bridge =
                gameObject.AddComponent<
                    DonorWorldLegacyWetnessBridge>();
            bridge.SetCoverageProfile(profile);
            return bridge;
        }

        private LegacyWetnessCoverageProfile CreateProfile(
            params LegacyWetnessMaterialCoverageEntry[] entries)
        {
            LegacyWetnessCoverageProfile profile =
                ScriptableObject.CreateInstance<
                    LegacyWetnessCoverageProfile>();
            createdObjects.Add(profile);
            profile.Configure(entries);
            return profile;
        }

        private BindingFixture CreateBinding(
            Material[] texturedMaterials,
            string[] sourceGuids,
            Material[] diagnosticMaterials = null)
        {
            Assert.That(
                sourceGuids.Length,
                Is.EqualTo(texturedMaterials.Length));
            var gameObject = new GameObject("Legacy Binding Test");
            createdObjects.Add(gameObject);
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = texturedMaterials;

            DonorWorldLegacyMaterialBinding binding =
                gameObject.AddComponent<
                    DonorWorldLegacyMaterialBinding>();
            binding.Configure(
                renderer,
                sourceGuids,
                texturedMaterials,
                diagnosticMaterials ?? texturedMaterials);
            return new BindingFixture(renderer, binding);
        }

        private Material CreateLitMaterial(
            bool alphaClip = false,
            bool transparent = false,
            bool emissive = false) =>
            CreateLitMaterial(
                Color.white,
                0.2f,
                alphaClip,
                transparent,
                emissive);

        private Material CreateLitMaterial(
            Color baseColor,
            float smoothness,
            bool alphaClip = false,
            bool transparent = false,
            bool emissive = false)
        {
            Shader shader = Shader.Find("HDRP/Lit");
            Assert.That(shader, Is.Not.Null, "HDRP/Lit shader is required.");
            var material = new Material(shader);
            createdObjects.Add(material);
            material.SetColor(BaseColorId, baseColor);
            material.SetFloat(SmoothnessId, smoothness);
            material.SetFloat(
                SurfaceTypeId,
                transparent ? 1f : 0f);
            material.SetFloat(
                AlphaCutoffEnableId,
                alphaClip ? 1f : 0f);
            material.SetColor(
                EmissiveColorId,
                emissive ? Color.white : Color.black);
            material.renderQueue = transparent
                ? 3000
                : alphaClip
                    ? 2450
                    : 2000;
            if (emissive)
            {
                material.EnableKeyword("_EMISSIVE_COLOR_MAP");
            }

            return material;
        }

        private Material CreateUnlitMaterial()
        {
            Shader shader = Shader.Find("HDRP/Unlit");
            Assert.That(shader, Is.Not.Null, "HDRP/Unlit shader is required.");
            var material = new Material(shader);
            createdObjects.Add(material);
            material.renderQueue = 2000;
            return material;
        }

        private static LegacyWetnessMaterialCoverageEntry Entry(
            string sourceGuid,
            LegacyWetnessSurfaceCategory category) =>
            new LegacyWetnessMaterialCoverageEntry(sourceGuid, category);

        private static WetnessEnvironmentOutputs CreateOutputs(
            uint revision,
            float ground,
            float road,
            float puddle,
            float vegetation) =>
            new WetnessEnvironmentOutputs(
                new WetnessState(
                    ground,
                    road,
                    puddle,
                    vegetation),
                SurfaceExposureProfile.Exterior.StableId,
                revision);

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.0001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.0001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.0001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.0001f));
        }

        private readonly struct BindingFixture
        {
            public BindingFixture(
                MeshRenderer renderer,
                DonorWorldLegacyMaterialBinding binding)
            {
                Renderer = renderer;
                Binding = binding;
            }

            public MeshRenderer Renderer { get; }
            public DonorWorldLegacyMaterialBinding Binding { get; }
        }
    }
}
