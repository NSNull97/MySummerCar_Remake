using System;
using UnityEngine;

namespace MSC.Presentation.AntiAliasing
{
    public enum AntiAliasingMode
    {
        Off = 0,
        Fxaa = 1,
        Smaa = 2,
        Taa = 3,
        TemporalUpscaler = 4,
        MaximumQuality = 5,
    }

    public enum AntiAliasingPreset
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Ultra = 3,
        Custom = 4,
    }

    public enum AntiAliasingCameraRole
    {
        Auto = 0,
        Gameplay = 1,
        ViewModel = 2,
        Auxiliary = 3,
        UserInterface = 4,
        Excluded = 5,
    }

    public enum TemporalUpscalerKind
    {
        None = 0,
        TaaU = 1,
        Dlss = 2,
    }

    public enum TemporalHistoryResetReason
    {
        None = 0,
        InitialTemporalActivation = 1,
        CameraTeleport = 2,
        CameraCut = 3,
        FieldOfViewJump = 4,
        ResolutionChange = 5,
        ModeChange = 6,
        ExplicitRequest = 7,
    }

    [Serializable]
    public struct AntiAliasingProfile
    {
        [SerializeField] private AntiAliasingMode mode;
        [SerializeField, Range(0f, 1f)] private float sharpening;
        [SerializeField, Range(0f, 1f)] private float antiFlicker;
        [SerializeField, Range(0f, 1f)] private float motionVectorRejection;
        [SerializeField, Range(0f, 1f)] private float ringingReduction;
        [SerializeField, Range(0f, 1f)] private float historySharpening;
        [SerializeField] private bool antiHistoryRinging;
        [SerializeField, Range(0.6f, 0.95f)] private float baseBlendFactor;
        [SerializeField, Range(0.1f, 1f)] private float jitterScale;

        public AntiAliasingMode Mode => mode;
        public float Sharpening => sharpening;
        public float AntiFlicker => antiFlicker;
        public float MotionVectorRejection => motionVectorRejection;
        public float RingingReduction => ringingReduction;
        public float HistorySharpening => historySharpening;
        public bool AntiHistoryRinging => antiHistoryRinging;
        public float BaseBlendFactor => baseBlendFactor;
        public float JitterScale => jitterScale;

        public static AntiAliasingProfile Create(
            AntiAliasingMode profileMode,
            float profileSharpening,
            float profileAntiFlicker,
            float profileMotionVectorRejection,
            float profileRingingReduction,
            float profileHistorySharpening,
            bool profileAntiHistoryRinging,
            float profileBaseBlendFactor,
            float profileJitterScale)
        {
            return new AntiAliasingProfile
            {
                mode = profileMode,
                sharpening = Mathf.Clamp01(profileSharpening),
                antiFlicker = Mathf.Clamp01(profileAntiFlicker),
                motionVectorRejection = Mathf.Clamp01(profileMotionVectorRejection),
                ringingReduction = Mathf.Clamp01(profileRingingReduction),
                historySharpening = Mathf.Clamp01(profileHistorySharpening),
                antiHistoryRinging = profileAntiHistoryRinging,
                baseBlendFactor = Mathf.Clamp(profileBaseBlendFactor, 0.6f, 0.95f),
                jitterScale = Mathf.Clamp(profileJitterScale, 0.1f, 1f),
            };
        }
    }

    public readonly struct AntiAliasingTelemetry
    {
        public AntiAliasingTelemetry(
            AntiAliasingPreset preset,
            AntiAliasingMode requestedMode,
            AntiAliasingMode effectiveMode,
            TemporalUpscalerKind upscaler,
            uint upscalerQuality,
            float dynamicResolutionScale,
            Vector2Int outputResolution,
            Vector2Int internalResolution,
            bool motionVectors,
            float sharpening,
            int managedCameraCount,
            string fallbackReason)
        {
            Preset = preset;
            RequestedMode = requestedMode;
            EffectiveMode = effectiveMode;
            Upscaler = upscaler;
            UpscalerQuality = upscalerQuality;
            DynamicResolutionScale = dynamicResolutionScale;
            OutputResolution = outputResolution;
            InternalResolution = internalResolution;
            MotionVectors = motionVectors;
            Sharpening = sharpening;
            ManagedCameraCount = managedCameraCount;
            FallbackReason = fallbackReason ?? string.Empty;
        }

        public AntiAliasingPreset Preset { get; }
        public AntiAliasingMode RequestedMode { get; }
        public AntiAliasingMode EffectiveMode { get; }
        public TemporalUpscalerKind Upscaler { get; }
        public uint UpscalerQuality { get; }
        public float DynamicResolutionScale { get; }
        public Vector2Int OutputResolution { get; }
        public Vector2Int InternalResolution { get; }
        public bool MotionVectors { get; }
        public float Sharpening { get; }
        public int ManagedCameraCount { get; }
        public string FallbackReason { get; }
    }
}
