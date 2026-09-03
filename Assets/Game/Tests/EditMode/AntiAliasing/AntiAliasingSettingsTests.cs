using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Presentation.AntiAliasing.Tests.EditMode
{
    public sealed class AntiAliasingSettingsTests
    {
        [Test]
        public void ProjectSettingsExposeRequiredPresetProgression()
        {
            AntiAliasingSettings settings = Resources.Load<AntiAliasingSettings>(
                AntiAliasingSettings.ResourcesPath);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.DefaultPreset, Is.EqualTo(AntiAliasingPreset.High));
            Assert.That(settings.GetProfile(AntiAliasingPreset.Low).Mode,
                Is.EqualTo(AntiAliasingMode.Fxaa));
            Assert.That(settings.GetProfile(AntiAliasingPreset.Medium).Mode,
                Is.EqualTo(AntiAliasingMode.Smaa));
            Assert.That(settings.GetProfile(AntiAliasingPreset.High).Mode,
                Is.EqualTo(AntiAliasingMode.Taa));
            Assert.That(settings.GetProfile(AntiAliasingPreset.Ultra).Mode,
                Is.EqualTo(AntiAliasingMode.Taa));
            Assert.That(settings.GetProfile(AntiAliasingPreset.High).Sharpening,
                Is.InRange(0.1f, 0.5f));
            Assert.That(settings.GetProfile(AntiAliasingPreset.Ultra).AntiFlicker,
                Is.GreaterThan(settings.GetProfile(AntiAliasingPreset.High).AntiFlicker));

            AntiAliasingProfile high = settings.GetProfile(AntiAliasingPreset.High);
            AntiAliasingProfile ultra = settings.GetProfile(AntiAliasingPreset.Ultra);
            Assert.That(high.BaseBlendFactor, Is.InRange(0.6f, 0.79f));
            Assert.That(ultra.BaseBlendFactor, Is.InRange(0.6f, 0.79f));
            Assert.That(high.JitterScale, Is.LessThan(1f));
            Assert.That(ultra.JitterScale, Is.LessThan(1f));
            Assert.That(high.AntiFlicker, Is.LessThanOrEqualTo(0.3f));
            Assert.That(high.MotionVectorRejection, Is.GreaterThanOrEqualTo(0.8f));
            Assert.That(ultra.MotionVectorRejection, Is.GreaterThanOrEqualTo(0.8f));

            // These are deliberately current-frame-biased. HDRP clamps native TAA history
            // to 0.6, so relaxing these guards would visibly reintroduce reported ghosting.
            Assert.That(high.BaseBlendFactor, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(high.JitterScale, Is.LessThanOrEqualTo(0.7f));
            Assert.That(high.AntiFlicker, Is.LessThanOrEqualTo(0.1f));
            Assert.That(high.MotionVectorRejection, Is.GreaterThanOrEqualTo(0.95f));
            Assert.That(high.HistorySharpening, Is.LessThanOrEqualTo(0.05f));
            Assert.That(ultra.BaseBlendFactor, Is.LessThanOrEqualTo(0.65f));
            Assert.That(ultra.JitterScale, Is.LessThanOrEqualTo(0.75f));
            Assert.That(ultra.MotionVectorRejection, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ActiveHdrpConfigurationSupportsNativeTemporalAaWithoutMsaaStacking()
        {
            HDRenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;

            Assert.That(asset, Is.Not.Null);
            RenderPipelineSettings pipelineSettings = asset.currentPlatformRenderPipelineSettings;
            Assert.That(pipelineSettings.supportMotionVectors, Is.True);
            Assert.That(pipelineSettings.msaaSampleCount, Is.EqualTo(MSAASamples.None));
            Assert.That(
                pipelineSettings.dynamicResolutionSettings.useMipBias,
                Is.False,
                "Negative global mip bias reintroduces DLSS texture and specular shimmer.");
        }

        [Test]
        public void EveryProjectHdrpAssetDisablesNegativeDynamicResolutionMipBias()
        {
            string[] paths = ProjectHdrpAssetPaths();

            for (int index = 0; index < paths.Length; index++)
            {
                HDRenderPipelineAsset asset =
                    AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(paths[index]);
                Assert.That(asset, Is.Not.Null, paths[index]);
                Assert.That(
                    asset.currentPlatformRenderPipelineSettings
                        .dynamicResolutionSettings.useMipBias,
                    Is.False,
                    paths[index]);
            }
        }

        [Test]
        public void EveryProjectHdrpAssetUsesStableDlaaPresetWithoutChangingDlssPresets()
        {
            // UnityEngine.NVIDIA.DLSSRenderPreset.Preset_F is serialized as 1.
            // Preset F is the installed NVIDIA module's stability preset for DLAA;
            // zero preserves the default Preset K path for ordinary DLSS modes.
            const uint presetDefault = 0u;
            const uint presetF = 1u;
            string[] paths = ProjectHdrpAssetPaths();

            for (int index = 0; index < paths.Length; index++)
            {
                HDRenderPipelineAsset asset =
                    AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(paths[index]);
                Assert.That(asset, Is.Not.Null, paths[index]);
                GlobalDynamicResolutionSettings dynamicSettings =
                    asset.currentPlatformRenderPipelineSettings.dynamicResolutionSettings;

                Assert.That(dynamicSettings.DLSSRenderPresetForDLAA,
                    Is.EqualTo(presetF), paths[index]);
                Assert.That(dynamicSettings.DLSSRenderPresetForQuality,
                    Is.EqualTo(presetDefault), paths[index]);
                Assert.That(dynamicSettings.DLSSRenderPresetForBalanced,
                    Is.EqualTo(presetDefault), paths[index]);
                Assert.That(dynamicSettings.DLSSRenderPresetForPerformance,
                    Is.EqualTo(presetDefault), paths[index]);
                Assert.That(dynamicSettings.DLSSRenderPresetForUltraPerformance,
                    Is.EqualTo(presetDefault), paths[index]);
            }
        }

        [Test]
        public void CapabilitiesMatchActivePipelineConfiguration()
        {
            AntiAliasingCapabilities capabilities = AntiAliasingCapabilities.Evaluate();

            Assert.That(capabilities.HdrpActive, Is.True);
            Assert.That(capabilities.MotionVectorsSupported, Is.True);
            Assert.That(capabilities.DynamicResolutionConfigured, Is.True);
            Assert.That(capabilities.DlssConfigured, Is.True);
            Assert.That(capabilities.GraphicsDevice, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void CameraPolicyCanExplicitlyExcludeUiAndSelectAuxiliaryMode()
        {
            GameObject owner = new GameObject("AA Policy Test Camera", typeof(Camera));
            try
            {
                AntiAliasingCameraPolicy policy = owner.AddComponent<AntiAliasingCameraPolicy>();
                policy.Configure(
                    AntiAliasingCameraRole.Auxiliary,
                    useModeOverride: true,
                    AntiAliasingMode.Fxaa);

                Assert.That(policy.Role, Is.EqualTo(AntiAliasingCameraRole.Auxiliary));
                Assert.That(policy.OverrideMode, Is.True);
                Assert.That(policy.Mode, Is.EqualTo(AntiAliasingMode.Fxaa));

                policy.Configure(AntiAliasingCameraRole.UserInterface);
                Assert.That(policy.Role, Is.EqualTo(AntiAliasingCameraRole.UserInterface));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static string[] ProjectHdrpAssetPaths()
        {
            return new[]
            {
                "Assets/Settings/HDRP High Fidelity.asset",
                "Assets/Settings/HDRP Balanced.asset",
                "Assets/Settings/HDRP Performant.asset",
                "Assets/Settings/HDRPDefaultResources/HDRenderPipelineAsset.asset",
            };
        }
    }
}
