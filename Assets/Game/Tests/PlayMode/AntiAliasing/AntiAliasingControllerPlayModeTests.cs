using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.TestTools;

namespace MSC.Presentation.AntiAliasing.Tests.PlayMode
{
    public sealed class AntiAliasingControllerPlayModeTests
    {
        private GameObject cameraOwner;
        private Camera camera;
        private RenderTexture cameraTarget;
        private AntiAliasingController controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            controller = AntiAliasingController.Instance;
            controller.SetExternalDlssRequest(false, 1u);
            controller.SetPreset(AntiAliasingPreset.High, persist: false);

            cameraOwner = new GameObject("AA PlayMode Main Camera");
            camera = cameraOwner.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 1f);
            cameraTarget = new RenderTexture(640, 360, 16);
            cameraTarget.Create();
            camera.targetTexture = cameraTarget;
            cameraOwner.AddComponent<AntiAliasingCameraPolicy>().Configure(
                AntiAliasingCameraRole.Gameplay);
            yield return RenderedFrames(2);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            controller.SetExternalDlssRequest(false, 1u);
            controller.SetPreset(AntiAliasingPreset.High, persist: false);
            if (cameraOwner != null)
            {
                Object.Destroy(cameraOwner);
            }
            if (cameraTarget != null)
            {
                cameraTarget.Release();
                Object.Destroy(cameraTarget);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator HighPresetConfiguresNativeTaaAndMotionVectors()
        {
            controller.SetPreset(AntiAliasingPreset.High, persist: false);
            yield return RenderedFrames(2);

            HDAdditionalCameraData hdCamera = camera.GetComponent<HDAdditionalCameraData>();
            Assert.That(hdCamera, Is.Not.Null);
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing));
            Assert.That(hdCamera.TAAQuality,
                Is.EqualTo(HDAdditionalCameraData.TAAQualityLevel.High));
            Assert.That(hdCamera.taaSharpenMode,
                Is.EqualTo(HDAdditionalCameraData.TAASharpenMode.ContrastAdaptiveSharpening));
            AntiAliasingProfile profile =
                controller.Settings.GetProfile(AntiAliasingPreset.High);
            Assert.That(hdCamera.taaBaseBlendFactor,
                Is.EqualTo(profile.BaseBlendFactor).Within(0.0001f));
            Assert.That(hdCamera.taaJitterScale,
                Is.EqualTo(profile.JitterScale).Within(0.0001f));
            Assert.That(hdCamera.taaAntiFlicker,
                Is.EqualTo(profile.AntiFlicker).Within(0.0001f));
            Assert.That(hdCamera.taaMotionVectorRejection,
                Is.EqualTo(profile.MotionVectorRejection).Within(0.0001f));
            Assert.That(hdCamera.taaBaseBlendFactor, Is.LessThan(0.8f));
            Assert.That(hdCamera.taaJitterScale, Is.LessThan(1f));
            Assert.That(camera.allowMSAA, Is.False);
            Assert.That(hdCamera.renderingPathCustomFrameSettings.IsEnabled(
                FrameSettingsField.MotionVectors), Is.True);
            Assert.That(hdCamera.renderingPathCustomFrameSettings.IsEnabled(
                FrameSettingsField.ObjectMotionVectors), Is.True);
        }

        [UnityTest]
        public IEnumerator RuntimeSwitchClearsOldUpscalerAndAaState()
        {
            HDAdditionalCameraData hdCamera = camera.GetComponent<HDAdditionalCameraData>();

            controller.SetMode(AntiAliasingMode.Fxaa, persist: false);
            yield return RenderedFrames(1);
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing));
            Assert.That(hdCamera.allowDeepLearningSuperSampling, Is.False);
            Assert.That(hdCamera.allowFidelityFX2SuperResolution, Is.False);
            Assert.That(camera.allowDynamicResolution, Is.False);

            controller.SetMode(AntiAliasingMode.Smaa, persist: false);
            yield return RenderedFrames(1);
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing));

            controller.SetMode(AntiAliasingMode.Off, persist: false);
            yield return RenderedFrames(1);
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.None));

            controller.SetMode(AntiAliasingMode.Taa, persist: false);
            yield return RenderedFrames(1);
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing));
        }

        [UnityTest]
        public IEnumerator DynamicAuxiliaryCameraUsesLightweightSmaa()
        {
            GameObject auxiliaryOwner = new GameObject("AA Auxiliary Camera");
            RenderTexture target = new RenderTexture(320, 180, 16);
            Camera auxiliary = auxiliaryOwner.AddComponent<Camera>();
            auxiliary.targetTexture = target;
            auxiliaryOwner.AddComponent<AntiAliasingCameraPolicy>().Configure(
                AntiAliasingCameraRole.Auxiliary);
            try
            {
                yield return RenderedFrames(2);
                HDAdditionalCameraData hdCamera = auxiliary.GetComponent<HDAdditionalCameraData>();
                Assert.That(hdCamera, Is.Not.Null);
                Assert.That(hdCamera.antialiasing,
                    Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing));
                Assert.That(hdCamera.allowDynamicResolution, Is.False);
                Assert.That(hdCamera.allowDeepLearningSuperSampling, Is.False);
            }
            finally
            {
                Object.Destroy(auxiliaryOwner);
                target.Release();
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator UiOnlyCameraIsNotTemporallyManaged()
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            Assert.That(uiLayer, Is.GreaterThanOrEqualTo(0));
            GameObject uiOwner = new GameObject("AA UI Camera");
            Camera uiCamera = uiOwner.AddComponent<Camera>();
            uiCamera.cullingMask = 1 << uiLayer;
            try
            {
                yield return RenderedFrames(2);
                Assert.That(uiCamera.GetComponent<HDAdditionalCameraData>(), Is.Null);
            }
            finally
            {
                Object.Destroy(uiOwner);
            }
        }

        [UnityTest]
        public IEnumerator ExplicitCameraCutUsesOneNonTemporalFrameThenRestoresTaa()
        {
            controller.SetMode(AntiAliasingMode.Taa, persist: false);
            yield return RenderedFrames(2);
            HDAdditionalCameraData hdCamera = camera.GetComponent<HDAdditionalCameraData>();

            controller.NotifyCameraCut(camera, TemporalHistoryResetReason.ExplicitRequest);
            yield return null;
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.None));

            yield return null;
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing));
        }

        [UnityTest]
        public IEnumerator AutomaticDiscontinuitiesResetAndRestoreTemporalHistory()
        {
            controller.SetMode(AntiAliasingMode.Taa, persist: false);
            yield return RenderedFrames(2);
            HDAdditionalCameraData hdCamera = camera.GetComponent<HDAdditionalCameraData>();

            camera.transform.position += Vector3.forward *
                                         (controller.Settings.TeleportDistanceMeters + 1f);
            yield return null;
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.None),
                "teleport reset frame");
            yield return null;
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing),
                "teleport restore");

            camera.fieldOfView += controller.Settings.FieldOfViewJumpDegrees + 1f;
            yield return null;
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.None),
                "FOV reset frame");
            yield return null;
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing),
                "FOV restore");

            camera.transform.rotation *= Quaternion.Euler(
                0f,
                controller.Settings.CameraCutAngleDegrees + 1f,
                0f);
            yield return null;
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.None),
                "hard cut reset frame");
            yield return null;
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing),
                "hard cut restore");

            camera.targetTexture = null;
            cameraTarget.Release();
            Object.Destroy(cameraTarget);
            cameraTarget = new RenderTexture(800, 450, 16);
            cameraTarget.Create();
            camera.targetTexture = cameraTarget;
            yield return null;
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.None),
                "resolution reset frame");
            yield return null;
            Assert.That(hdCamera.antialiasing,
                Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing),
                "resolution restore");
        }

        [UnityTest]
        public IEnumerator TemporalUpscalerEitherActivatesSafelyOrFallsBackToTaa()
        {
            controller.SetMode(AntiAliasingMode.TemporalUpscaler, persist: false);
            yield return RenderedFrames(2);
            HDAdditionalCameraData hdCamera = camera.GetComponent<HDAdditionalCameraData>();

            if (controller.Capabilities.DlssAvailable)
            {
                Assert.That(hdCamera.allowDeepLearningSuperSampling, Is.True);
                Assert.That(hdCamera.allowDynamicResolution, Is.True);
                Assert.That(hdCamera.antialiasing,
                    Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.None));
            }
            else
            {
                Assert.That(hdCamera.allowDeepLearningSuperSampling, Is.False);
                Assert.That(hdCamera.allowDynamicResolution, Is.False);
                Assert.That(hdCamera.antialiasing,
                    Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing));
                Assert.That(controller.GetTelemetry(camera).FallbackReason, Is.Not.Empty);
            }
        }

        [UnityTest]
        public IEnumerator MaximumQualityUsesDlaaQualityAndStablePresetOrFallsBackSafely()
        {
            const uint dlaaQuality = 4u;
            const uint presetDefault = 0u;
            const uint presetF = 1u;

            controller.SetMode(AntiAliasingMode.MaximumQuality, persist: false);
            yield return RenderedFrames(2);

            HDAdditionalCameraData hdCamera = camera.GetComponent<HDAdditionalCameraData>();
            HDRenderPipelineAsset asset =
                GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            Assert.That(asset, Is.Not.Null);
            GlobalDynamicResolutionSettings dynamicSettings =
                asset.currentPlatformRenderPipelineSettings.dynamicResolutionSettings;
            Assert.That(dynamicSettings.DLSSRenderPresetForDLAA, Is.EqualTo(presetF));
            Assert.That(dynamicSettings.DLSSRenderPresetForQuality,
                Is.EqualTo(presetDefault), "Ordinary DLSS must keep its default Preset K path.");

            if (controller.Capabilities.DlssAvailable)
            {
                Assert.That(hdCamera.allowDeepLearningSuperSampling, Is.True);
                Assert.That(hdCamera.allowDynamicResolution, Is.True);
                Assert.That(hdCamera.deepLearningSuperSamplingUseCustomQualitySettings, Is.True);
                Assert.That(hdCamera.deepLearningSuperSamplingQuality, Is.EqualTo(dlaaQuality));
                Assert.That(hdCamera.antialiasing,
                    Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.None));
            }
            else
            {
                Assert.That(hdCamera.allowDeepLearningSuperSampling, Is.False);
                Assert.That(hdCamera.allowDynamicResolution, Is.False);
                Assert.That(hdCamera.antialiasing,
                    Is.EqualTo(HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing));
                Assert.That(controller.GetTelemetry(camera).FallbackReason, Is.Not.Empty);
            }
        }

        private static IEnumerator RenderedFrames(int count)
        {
            for (int index = 0; index < count; index++)
            {
                yield return null;
            }
        }
    }
}
