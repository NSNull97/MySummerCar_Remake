using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Presentation.AntiAliasing
{
    public readonly struct AntiAliasingCapabilities
    {
        private AntiAliasingCapabilities(
            bool hdrpActive,
            bool motionVectors,
            bool dynamicResolution,
            bool taaUConfigured,
            bool dlssConfigured,
            bool dlssHardwareSupported,
            string graphicsDevice)
        {
            HdrpActive = hdrpActive;
            MotionVectorsSupported = motionVectors;
            DynamicResolutionConfigured = dynamicResolution;
            TaaUConfigured = taaUConfigured;
            DlssConfigured = dlssConfigured;
            DlssHardwareSupported = dlssHardwareSupported;
            GraphicsDevice = graphicsDevice ?? string.Empty;
        }

        public bool HdrpActive { get; }
        public bool MotionVectorsSupported { get; }
        public bool DynamicResolutionConfigured { get; }
        public bool TaaUConfigured { get; }
        public bool DlssConfigured { get; }
        public bool DlssHardwareSupported { get; }
        public bool DlssAvailable => HdrpActive && DynamicResolutionConfigured &&
                                     DlssConfigured && DlssHardwareSupported;
        public string GraphicsDevice { get; }

        public static AntiAliasingCapabilities Evaluate()
        {
            HDRenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            if (asset == null)
            {
                return new AntiAliasingCapabilities(
                    false, false, false, false, false, false,
                    SystemInfo.graphicsDeviceName);
            }

            RenderPipelineSettings pipelineSettings = asset.currentPlatformRenderPipelineSettings;
            GlobalDynamicResolutionSettings dynamicSettings = pipelineSettings.dynamicResolutionSettings;
            bool dlssConfigured = ContainsUpscaler(dynamicSettings.advancedUpscalerNames, "DLSS");
            bool taaUConfigured = dynamicSettings.enabled &&
                                  dynamicSettings.upsampleFilter == DynamicResUpscaleFilter.TAAU;

            return new AntiAliasingCapabilities(
                true,
                pipelineSettings.supportMotionVectors,
                dynamicSettings.enabled,
                taaUConfigured,
                dlssConfigured,
                IsDlssHardwareSupported(),
                SystemInfo.graphicsDeviceName);
        }

        public static bool IsDlssHardwareSupported()
        {
#if ENABLE_NVIDIA
            if (string.IsNullOrEmpty(SystemInfo.graphicsDeviceVendor) ||
                SystemInfo.graphicsDeviceVendor.IndexOf(
                    "nvidia",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            UnityEngine.NVIDIA.GraphicsDevice device =
                UnityEngine.NVIDIA.GraphicsDevice.device ??
                UnityEngine.NVIDIA.GraphicsDevice.CreateGraphicsDevice();
            return device != null &&
                   device.IsFeatureAvailable(UnityEngine.NVIDIA.GraphicsDeviceFeature.DLSS);
#else
            return false;
#endif
        }

        private static bool ContainsUpscaler(IReadOnlyList<string> names, string expected)
        {
            if (names == null)
            {
                return false;
            }

            for (int index = 0; index < names.Count; index++)
            {
                if (string.Equals(names[index], expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
