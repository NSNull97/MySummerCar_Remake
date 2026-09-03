using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.Presentation.AntiAliasing
{
    [DefaultExecutionOrder(-9500)]
    [DisallowMultipleComponent]
    public sealed class AntiAliasingController : MonoBehaviour
    {
        private const string RootName = "[MSC Anti-Aliasing]";
        private const string PresetPreferenceKey = "msc.graphics.aa.preset.v1";
        private const string ModePreferenceKey = "msc.graphics.aa.mode.v1";
        private const string SharpenPreferenceKey = "msc.graphics.aa.sharpen.v1";
        private const int DestroyedCameraCleanupIntervalFrames = 300;
        private const uint DlssBalancedQuality = 1u;
        private const uint DlaaQuality = 4u;
        internal const float MaximumSharpening = 0.75f;

        private static AntiAliasingController instance;

        private readonly Dictionary<int, ManagedCameraState> cameras =
            new Dictionary<int, ManagedCameraState>();
        private readonly HashSet<string> emittedWarnings = new HashSet<string>();
        private readonly List<int> staleCameraIds = new List<int>();

        private AntiAliasingSettings settings;
        private AntiAliasingCapabilities capabilities;
        private AntiAliasingPreset selectedPreset;
        private AntiAliasingMode selectedMode;
        private float sharpeningOverride = -1f;
        private bool externalDlssRequested;
        private uint externalDlssQuality = DlssBalancedQuality;
        private bool shuttingDown;
        private int nextDestroyedCameraCleanupFrame;
        private string lastFallbackReason = string.Empty;

        public static AntiAliasingController Instance => EnsureInstance();
        public AntiAliasingSettings Settings => settings;
        public AntiAliasingCapabilities Capabilities => capabilities;
        public AntiAliasingPreset SelectedPreset => selectedPreset;
        public AntiAliasingMode SelectedMode => selectedMode;
        public int ManagedCameraCount => cameras.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static bool TryGetInstance(out AntiAliasingController controller)
        {
            controller = instance;
            return controller != null;
        }

        public static void TryRefreshCamera(Camera camera)
        {
            if (camera != null && TryGetInstance(out AntiAliasingController controller))
            {
                controller.RefreshCamera(camera);
            }
        }

        public static void TryReleaseCamera(Camera camera)
        {
            if (camera != null && TryGetInstance(out AntiAliasingController controller))
            {
                controller.UnmanageCamera(camera);
            }
        }

        public void SetPreset(AntiAliasingPreset preset, bool persist = true)
        {
            if (preset == AntiAliasingPreset.Custom)
            {
                return;
            }

            AntiAliasingMode presetMode = settings.GetProfile(preset).Mode;
            if (selectedPreset == preset &&
                selectedMode == presetMode &&
                sharpeningOverride < 0f)
            {
                return;
            }

            selectedPreset = preset;
            selectedMode = presetMode;
            sharpeningOverride = -1f;
            OnConfigurationChanged(persist);
        }

        public void SetMode(AntiAliasingMode mode, bool persist = true)
        {
            if (selectedPreset == AntiAliasingPreset.Custom && selectedMode == mode)
            {
                return;
            }

            selectedPreset = AntiAliasingPreset.Custom;
            selectedMode = mode;
            OnConfigurationChanged(persist);
        }

        public void SetSharpening(float amount, bool persist = true)
        {
            float sanitizedAmount = Mathf.Clamp01(amount);
            if (selectedPreset == AntiAliasingPreset.Custom &&
                sharpeningOverride >= 0f &&
                Mathf.Approximately(sharpeningOverride, sanitizedAmount))
            {
                return;
            }

            sharpeningOverride = sanitizedAmount;
            selectedPreset = AntiAliasingPreset.Custom;
            OnConfigurationChanged(persist);
        }

        public void SetExternalDlssRequest(bool enabled, uint quality)
        {
            uint sanitizedQuality = Math.Min(quality, DlaaQuality);
            if (externalDlssRequested == enabled && externalDlssQuality == sanitizedQuality)
            {
                return;
            }

            externalDlssRequested = enabled;
            externalDlssQuality = sanitizedQuality;
            OnConfigurationChanged(persist: false);
        }

        public void NotifyCameraCut(
            Camera camera,
            TemporalHistoryResetReason reason = TemporalHistoryResetReason.ExplicitRequest)
        {
            if (camera == null)
            {
                return;
            }

            RefreshCamera(camera);
            if (cameras.TryGetValue(camera.GetInstanceID(), out ManagedCameraState state))
            {
                ScheduleHistoryReset(state, reason);
            }
        }

        public void ResetAllTemporalHistory(
            TemporalHistoryResetReason reason = TemporalHistoryResetReason.ExplicitRequest)
        {
            foreach (ManagedCameraState state in cameras.Values)
            {
                ScheduleHistoryReset(state, reason);
            }
        }

        public AntiAliasingTelemetry GetTelemetry(Camera preferredCamera = null)
        {
            Camera camera = preferredCamera != null ? preferredCamera : Camera.main;
            ManagedCameraState state = null;
            if (camera != null)
            {
                cameras.TryGetValue(camera.GetInstanceID(), out state);
            }

            ResolvedConfiguration resolved = state != null
                ? Resolve(state.Role, state.Policy)
                : Resolve(AntiAliasingCameraRole.Gameplay, null);
            int width = camera != null ? Mathf.Max(1, camera.pixelWidth) : Mathf.Max(1, Screen.width);
            int height = camera != null ? Mathf.Max(1, camera.pixelHeight) : Mathf.Max(1, Screen.height);
            Vector2Int output = new Vector2Int(width, height);
            Vector2Int internalResolution = output;
            float scale = 1f;
            if (resolved.DynamicResolution &&
                state != null &&
                state.HasResolutionTelemetry &&
                state.LastOutputResolution == output)
            {
                internalResolution = state.LastInternalResolution;
                scale = state.LastDynamicResolutionScale;
            }
            else if (resolved.DynamicResolution && capabilities.DynamicResolutionConfigured)
            {
                internalResolution = DynamicResolutionHandler.instance.GetScaledSize(output);
                internalResolution.x = Mathf.Max(1, internalResolution.x);
                internalResolution.y = Mathf.Max(1, internalResolution.y);
                scale = Mathf.Clamp(
                    Mathf.Min(
                        internalResolution.x / (float)width,
                        internalResolution.y / (float)height),
                    0.01f,
                    3f);
            }

            return new AntiAliasingTelemetry(
                selectedPreset,
                externalDlssRequested ? AntiAliasingMode.TemporalUpscaler : selectedMode,
                resolved.Mode,
                resolved.Upscaler,
                resolved.DlssQuality,
                scale,
                output,
                internalResolution,
                resolved.Temporal && capabilities.MotionVectorsSupported,
                resolved.Sharpening,
                cameras.Count,
                lastFallbackReason);
        }

        private static AntiAliasingController EnsureInstance()
        {
            if (instance != null)
            {
                return instance;
            }

            AntiAliasingController existing = FindFirstObjectByType<AntiAliasingController>(
                FindObjectsInactive.Include);
            if (existing != null)
            {
                instance = existing;
                return existing;
            }

            GameObject root = new GameObject(RootName);
            DontDestroyOnLoad(root);
            instance = root.AddComponent<AntiAliasingController>();
            return instance;
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            settings = Resources.Load<AntiAliasingSettings>(AntiAliasingSettings.ResourcesPath);
            if (settings == null)
            {
                settings = AntiAliasingSettings.CreateRuntimeDefaults();
                WarnOnce(
                    "settings-missing",
                    "Anti-aliasing settings asset was not found; safe project defaults are active.");
            }

            capabilities = AntiAliasingCapabilities.Evaluate();
            LoadPreferences();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.quitting += OnApplicationQuitting;
            RegisterLoadedCameras();

            if (settings.ShowDevelopmentOverlay && (Application.isEditor || Debug.isDebugBuild))
            {
                gameObject.AddComponent<AntiAliasingDebugOverlay>();
            }

            if (!capabilities.HdrpActive)
            {
                WarnOnce("not-hdrp", "HDRP is not active; the AA controller will leave cameras untouched.");
            }
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }

            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.quitting -= OnApplicationQuitting;

            if (!shuttingDown)
            {
                RestoreAllCameras();
            }

            instance = null;
        }

        private void OnApplicationQuitting()
        {
            shuttingDown = true;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RegisterLoadedCameras();
            RemoveDestroyedCameras();
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!capabilities.HdrpActive || camera == null || camera.cameraType != CameraType.Game)
            {
                return;
            }

            if (Time.frameCount >= nextDestroyedCameraCleanupFrame)
            {
                RemoveDestroyedCameras();
                nextDestroyedCameraCleanupFrame =
                    Time.frameCount + DestroyedCameraCleanupIntervalFrames;
            }

            AntiAliasingCameraRole role = ClassifyCamera(camera, out AntiAliasingCameraPolicy policy);
            if (role == AntiAliasingCameraRole.UserInterface || role == AntiAliasingCameraRole.Excluded)
            {
                UnmanageCamera(camera);
                return;
            }

            ManagedCameraState state = GetOrCreateState(camera, role, policy);
            DetectDiscontinuity(state);
            ApplyCamera(state);
            CaptureContinuity(state);
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null ||
                !cameras.TryGetValue(camera.GetInstanceID(), out ManagedCameraState state))
            {
                return;
            }

            int width = Mathf.Max(1, camera.pixelWidth);
            int height = Mathf.Max(1, camera.pixelHeight);
            Vector2Int output = new Vector2Int(width, height);
            Vector2 resolvedScale = state.LastResolved.DynamicResolution
                ? DynamicResolutionHandler.instance.GetResolvedScale()
                : Vector2.one;
            Vector2Int internalResolution = new Vector2Int(
                Mathf.Max(1, Mathf.CeilToInt(width * resolvedScale.x)),
                Mathf.Max(1, Mathf.CeilToInt(height * resolvedScale.y)));

            state.LastOutputResolution = output;
            state.LastInternalResolution = internalResolution;
            state.LastDynamicResolutionScale = Mathf.Clamp(
                Mathf.Min(
                    internalResolution.x / (float)width,
                    internalResolution.y / (float)height),
                0.01f,
                3f);
            state.HasResolutionTelemetry = true;
        }

        private void RefreshCamera(Camera camera)
        {
            if (!capabilities.HdrpActive || camera == null || camera.cameraType != CameraType.Game)
            {
                return;
            }

            AntiAliasingCameraRole role = ClassifyCamera(camera, out AntiAliasingCameraPolicy policy);
            if (role == AntiAliasingCameraRole.UserInterface || role == AntiAliasingCameraRole.Excluded)
            {
                UnmanageCamera(camera);
                return;
            }

            ManagedCameraState state = GetOrCreateState(camera, role, policy);
            ApplyCamera(state);
            CaptureContinuity(state);
        }

        private void RegisterLoadedCameras()
        {
            if (!capabilities.HdrpActive)
            {
                return;
            }

            Camera[] loadedCameras = FindObjectsByType<Camera>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < loadedCameras.Length; index++)
            {
                RefreshCamera(loadedCameras[index]);
            }
        }

        private ManagedCameraState GetOrCreateState(
            Camera camera,
            AntiAliasingCameraRole role,
            AntiAliasingCameraPolicy policy)
        {
            int id = camera.GetInstanceID();
            if (cameras.TryGetValue(id, out ManagedCameraState state))
            {
                state.Role = role;
                state.Policy = policy;
                return state;
            }

            HDAdditionalCameraData hdCamera = camera.GetComponent<HDAdditionalCameraData>();
            bool addedHdCamera = hdCamera == null;
            if (addedHdCamera)
            {
                hdCamera = camera.gameObject.AddComponent<HDAdditionalCameraData>();
            }

            state = new ManagedCameraState(camera, hdCamera, addedHdCamera, role, policy);
            cameras.Add(id, state);
            return state;
        }

        private void ApplyCamera(ManagedCameraState state)
        {
            if (state.Camera == null || state.HdCamera == null)
            {
                return;
            }

            ResolvedConfiguration resolved = Resolve(state.Role, state.Policy);
            if (state.HistoryResetFrame >= 0)
            {
                if (!state.HistoryResetFrameRendered ||
                    Time.frameCount == state.HistoryResetFrame)
                {
                    state.HistoryResetFrame = Time.frameCount;
                    state.HistoryResetFrameRendered = true;
                    ApplyHistoryResetFrame(state);
                    state.LastResolved = resolved;
                    return;
                }

                if (Time.frameCount > state.HistoryResetFrame)
                {
                    state.HistoryResetFrame = -1;
                    state.HistoryResetFrameRendered = false;
                }
            }

            Camera camera = state.Camera;
            HDAdditionalCameraData hdCamera = state.HdCamera;
            camera.allowMSAA = false;
            camera.allowDynamicResolution = resolved.DynamicResolution;
            hdCamera.allowDynamicResolution = resolved.DynamicResolution;
            hdCamera.allowDeepLearningSuperSampling = resolved.Upscaler == TemporalUpscalerKind.Dlss;
            hdCamera.allowFidelityFX2SuperResolution = false;

            if (resolved.Upscaler == TemporalUpscalerKind.Dlss)
            {
                hdCamera.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
                hdCamera.deepLearningSuperSamplingUseCustomQualitySettings = true;
                hdCamera.deepLearningSuperSamplingQuality = resolved.DlssQuality;
                hdCamera.deepLearningSuperSamplingUseCustomAttributes = true;
                hdCamera.deepLearningSuperSamplingUseOptimalSettings = true;
#pragma warning disable CS0618
                hdCamera.deepLearningSuperSamplingSharpening = 0f;
#pragma warning restore CS0618
            }
            else
            {
                hdCamera.antialiasing = ToHdrpMode(resolved.Mode);
            }

            ConfigureTemporalTuning(hdCamera, resolved);
            ConfigureManagedFrameSettings(state, resolved);
            state.LastResolved = resolved;
        }

        private void ApplyHistoryResetFrame(ManagedCameraState state)
        {
            state.Camera.allowDynamicResolution = false;
            state.HdCamera.allowDynamicResolution = false;
            state.HdCamera.allowDeepLearningSuperSampling = false;
            state.HdCamera.allowFidelityFX2SuperResolution = false;
            state.HdCamera.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
        }

        private void ConfigureTemporalTuning(
            HDAdditionalCameraData hdCamera,
            ResolvedConfiguration resolved)
        {
            AntiAliasingProfile profile = resolved.Profile;
            hdCamera.SMAAQuality = HDAdditionalCameraData.SMAAQualityLevel.High;
            hdCamera.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.High;
            hdCamera.taaSharpenMode =
                HDAdditionalCameraData.TAASharpenMode.ContrastAdaptiveSharpening;
            hdCamera.taaSharpenStrength = resolved.Upscaler == TemporalUpscalerKind.None
                ? Mathf.Clamp(resolved.Sharpening, 0f, MaximumSharpening)
                : 0f;
            hdCamera.taaRingingReduction = profile.RingingReduction;
            hdCamera.taaHistorySharpening = profile.HistorySharpening;
            hdCamera.taaAntiFlicker = profile.AntiFlicker;
            hdCamera.taaMotionVectorRejection = profile.MotionVectorRejection;
            hdCamera.taaAntiHistoryRinging = profile.AntiHistoryRinging;
            // A lower history weight and a slightly smaller native-TAA jitter pattern
            // trade a little static accumulation for substantially cleaner motion.
            // HDRP ignores taaJitterScale for DLSS/DLAA and other temporal upscalers.
            hdCamera.taaBaseBlendFactor = Mathf.Clamp(
                profile.BaseBlendFactor,
                0.6f,
                0.95f);
            hdCamera.taaJitterScale = Mathf.Clamp(
                profile.JitterScale,
                0.1f,
                1f);
        }

        private void ConfigureManagedFrameSettings(
            ManagedCameraState state,
            ResolvedConfiguration resolved)
        {
            HDAdditionalCameraData hdCamera = state.HdCamera;
            hdCamera.customRenderingSettings = true;
            SetFrameSetting(
                hdCamera,
                FrameSettingsField.Antialiasing,
                resolved.Mode != AntiAliasingMode.Off);

            if (!resolved.Temporal)
            {
                // Motion vectors may still be needed by Motion Blur, SSR or an
                // unrelated camera override. Restore only the fields owned by
                // temporal AA instead of disabling the pass globally.
                state.Original.RestoreMotionVectorFrameSettings(hdCamera);
                return;
            }

            SetFrameSetting(hdCamera, FrameSettingsField.MotionVectors, true);
            SetFrameSetting(hdCamera, FrameSettingsField.ObjectMotionVectors, true);
            SetFrameSetting(
                hdCamera,
                FrameSettingsField.TransparentsWriteMotionVector,
                settings.TransparentMotionVectors);
        }

        private static void SetFrameSetting(
            HDAdditionalCameraData hdCamera,
            FrameSettingsField field,
            bool enabled)
        {
            hdCamera.renderingPathCustomFrameSettings.SetEnabled(field, enabled);
            FrameSettingsOverrideMask mask = hdCamera.renderingPathCustomFrameSettingsOverrideMask;
            mask.mask[(uint)field] = true;
            hdCamera.renderingPathCustomFrameSettingsOverrideMask = mask;
        }

        private ResolvedConfiguration Resolve(
            AntiAliasingCameraRole role,
            AntiAliasingCameraPolicy policy)
        {
            AntiAliasingProfile profile = settings.GetProfile(
                selectedPreset == AntiAliasingPreset.Custom
                    ? AntiAliasingPreset.High
                    : selectedPreset);
            AntiAliasingMode requested = selectedMode;

            if (policy != null && policy.OverrideMode)
            {
                requested = policy.Mode;
            }
            else if (role == AntiAliasingCameraRole.Auxiliary)
            {
                requested = selectedMode == AntiAliasingMode.Off
                    ? AntiAliasingMode.Off
                    : settings.AuxiliaryCameraMode;
            }
            else if (externalDlssRequested)
            {
                requested = AntiAliasingMode.TemporalUpscaler;
            }

            float sharpening = sharpeningOverride >= 0f
                ? sharpeningOverride
                : profile.Sharpening;
            lastFallbackReason = string.Empty;

            if (requested == AntiAliasingMode.Taa && !capabilities.MotionVectorsSupported)
            {
                lastFallbackReason = "Motion vectors are disabled in the active HDRP asset; SMAA fallback is active.";
                WarnOnce("taa-no-motion-vectors", lastFallbackReason);
                requested = AntiAliasingMode.Smaa;
            }

            if (requested == AntiAliasingMode.TemporalUpscaler)
            {
                if (role == AntiAliasingCameraRole.Auxiliary)
                {
                    return new ResolvedConfiguration(
                        AntiAliasingMode.Smaa, TemporalUpscalerKind.None, false, false,
                        0u, 0f, profile);
                }

                if (capabilities.DlssAvailable)
                {
                    return new ResolvedConfiguration(
                        AntiAliasingMode.TemporalUpscaler,
                        TemporalUpscalerKind.Dlss,
                        true,
                        true,
                        externalDlssQuality,
                        0f,
                        profile);
                }

                lastFallbackReason =
                    "The requested temporal upscaler is unavailable or not configured; native TAA fallback is active.";
                WarnOnce("upscaler-unavailable", lastFallbackReason);
                requested = capabilities.MotionVectorsSupported
                    ? AntiAliasingMode.Taa
                    : AntiAliasingMode.Smaa;
            }

            if (requested == AntiAliasingMode.MaximumQuality)
            {
                if (role != AntiAliasingCameraRole.Auxiliary && capabilities.DlssAvailable)
                {
                    return new ResolvedConfiguration(
                        AntiAliasingMode.MaximumQuality,
                        TemporalUpscalerKind.Dlss,
                        true,
                        true,
                        DlaaQuality,
                        0f,
                        profile);
                }

                requested = capabilities.MotionVectorsSupported
                    ? AntiAliasingMode.Taa
                    : AntiAliasingMode.Smaa;
                lastFallbackReason = capabilities.MotionVectorsSupported
                    ? "DLAA Maximum Quality is unavailable or not configured; native TAA fallback is active."
                    : "Maximum Quality requires temporal support; SMAA fallback is active.";
                WarnOnce("maximum-quality-unavailable", lastFallbackReason);
            }

            bool temporal = requested == AntiAliasingMode.Taa;
            return new ResolvedConfiguration(
                requested,
                TemporalUpscalerKind.None,
                temporal,
                false,
                0u,
                temporal ? sharpening : 0f,
                profile);
        }

        private void DetectDiscontinuity(ManagedCameraState state)
        {
            if (!state.HasContinuitySample || state.HistoryResetFrame >= 0)
            {
                return;
            }

            Transform cameraTransform = state.Camera.transform;
            if (Vector3.Distance(cameraTransform.position, state.LastPosition) >=
                settings.TeleportDistanceMeters)
            {
                ScheduleHistoryReset(state, TemporalHistoryResetReason.CameraTeleport);
                return;
            }

            if (Quaternion.Angle(cameraTransform.rotation, state.LastRotation) >=
                settings.CameraCutAngleDegrees)
            {
                ScheduleHistoryReset(state, TemporalHistoryResetReason.CameraCut);
                return;
            }

            if (Mathf.Abs(state.Camera.fieldOfView - state.LastFieldOfView) >=
                settings.FieldOfViewJumpDegrees)
            {
                ScheduleHistoryReset(state, TemporalHistoryResetReason.FieldOfViewJump);
                return;
            }

            if (state.Camera.pixelWidth != state.LastPixelWidth ||
                state.Camera.pixelHeight != state.LastPixelHeight)
            {
                ScheduleHistoryReset(state, TemporalHistoryResetReason.ResolutionChange);
            }
        }

        private static void CaptureContinuity(ManagedCameraState state)
        {
            state.LastPosition = state.Camera.transform.position;
            state.LastRotation = state.Camera.transform.rotation;
            state.LastFieldOfView = state.Camera.fieldOfView;
            state.LastPixelWidth = state.Camera.pixelWidth;
            state.LastPixelHeight = state.Camera.pixelHeight;
            state.HasContinuitySample = true;
        }

        private static void ScheduleHistoryReset(
            ManagedCameraState state,
            TemporalHistoryResetReason reason)
        {
            if (!state.LastResolved.Temporal)
            {
                return;
            }

            state.HistoryResetFrame = Time.frameCount;
            state.HistoryResetFrameRendered = false;
            state.LastHistoryResetReason = reason;
        }

        private void OnConfigurationChanged(bool persist)
        {
            foreach (ManagedCameraState state in cameras.Values)
            {
                // HDRP invalidates temporal history when the AA/upscaler mode changes.
                // A forced blank AA frame is reserved for discontinuities where HDRP
                // cannot infer the cut from public camera state.
                state.HistoryResetFrame = -1;
                state.HistoryResetFrameRendered = false;
                state.LastHistoryResetReason = TemporalHistoryResetReason.ModeChange;
                ApplyCamera(state);
            }

            if (persist && settings.PersistPlayerSelection)
            {
                SavePreferences();
            }
        }

        private void LoadPreferences()
        {
            selectedPreset = settings.DefaultPreset;
            AntiAliasingProfile defaultProfile = settings.GetProfile(selectedPreset);
            selectedMode = defaultProfile.Mode;

            if (!settings.PersistPlayerSelection)
            {
                return;
            }

            int presetValue = PlayerPrefs.GetInt(PresetPreferenceKey, (int)selectedPreset);
            int modeValue = PlayerPrefs.GetInt(ModePreferenceKey, (int)selectedMode);
            selectedPreset = Enum.IsDefined(typeof(AntiAliasingPreset), presetValue)
                ? (AntiAliasingPreset)presetValue
                : settings.DefaultPreset;
            selectedMode = Enum.IsDefined(typeof(AntiAliasingMode), modeValue)
                ? (AntiAliasingMode)modeValue
                : settings.GetProfile(selectedPreset).Mode;
            sharpeningOverride = PlayerPrefs.HasKey(SharpenPreferenceKey)
                ? Mathf.Clamp01(PlayerPrefs.GetFloat(SharpenPreferenceKey))
                : -1f;
        }

        private void SavePreferences()
        {
            PlayerPrefs.SetInt(PresetPreferenceKey, (int)selectedPreset);
            PlayerPrefs.SetInt(ModePreferenceKey, (int)selectedMode);
            if (sharpeningOverride >= 0f)
            {
                PlayerPrefs.SetFloat(SharpenPreferenceKey, sharpeningOverride);
            }
            else
            {
                PlayerPrefs.DeleteKey(SharpenPreferenceKey);
            }

            PlayerPrefs.Save();
        }

        private AntiAliasingCameraRole ClassifyCamera(
            Camera camera,
            out AntiAliasingCameraPolicy policy)
        {
            policy = camera.GetComponent<AntiAliasingCameraPolicy>();
            if (policy != null && policy.Role != AntiAliasingCameraRole.Auto)
            {
                return policy.Role;
            }

            int uiLayer = LayerMask.NameToLayer("UI");
            if (uiLayer >= 0)
            {
                int uiMask = 1 << uiLayer;
                if (camera.cullingMask != 0 && (camera.cullingMask & ~uiMask) == 0)
                {
                    return AntiAliasingCameraRole.UserInterface;
                }
            }

            return camera.targetTexture != null
                ? AntiAliasingCameraRole.Auxiliary
                : AntiAliasingCameraRole.Gameplay;
        }

        private void UnmanageCamera(Camera camera)
        {
            int id = camera.GetInstanceID();
            if (!cameras.TryGetValue(id, out ManagedCameraState state))
            {
                return;
            }

            state.Restore();
            cameras.Remove(id);
        }

        private void RestoreAllCameras()
        {
            foreach (ManagedCameraState state in cameras.Values)
            {
                state.Restore();
            }

            cameras.Clear();
        }

        private void RemoveDestroyedCameras()
        {
            staleCameraIds.Clear();
            foreach (KeyValuePair<int, ManagedCameraState> pair in cameras)
            {
                if (pair.Value.Camera != null)
                {
                    continue;
                }

                staleCameraIds.Add(pair.Key);
            }

            for (int index = 0; index < staleCameraIds.Count; index++)
            {
                cameras.Remove(staleCameraIds[index]);
            }
        }

        private void WarnOnce(string key, string message)
        {
            if (emittedWarnings.Add(key))
            {
                Debug.LogWarning($"[AntiAliasing] {message}", this);
            }
        }

        private static HDAdditionalCameraData.AntialiasingMode ToHdrpMode(
            AntiAliasingMode mode)
        {
            switch (mode)
            {
                case AntiAliasingMode.Fxaa:
                    return HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing;
                case AntiAliasingMode.Smaa:
                    return HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                case AntiAliasingMode.Taa:
                    return HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
                default:
                    return HDAdditionalCameraData.AntialiasingMode.None;
            }
        }

        private readonly struct ResolvedConfiguration
        {
            public ResolvedConfiguration(
                AntiAliasingMode mode,
                TemporalUpscalerKind upscaler,
                bool temporal,
                bool dynamicResolution,
                uint dlssQuality,
                float sharpening,
                AntiAliasingProfile profile)
            {
                Mode = mode;
                Upscaler = upscaler;
                Temporal = temporal;
                DynamicResolution = dynamicResolution;
                DlssQuality = dlssQuality;
                Sharpening = sharpening;
                Profile = profile;
            }

            public AntiAliasingMode Mode { get; }
            public TemporalUpscalerKind Upscaler { get; }
            public bool Temporal { get; }
            public bool DynamicResolution { get; }
            public uint DlssQuality { get; }
            public float Sharpening { get; }
            public AntiAliasingProfile Profile { get; }
        }

        private sealed class ManagedCameraState
        {
            public ManagedCameraState(
                Camera camera,
                HDAdditionalCameraData hdCamera,
                bool addedHdCamera,
                AntiAliasingCameraRole role,
                AntiAliasingCameraPolicy policy)
            {
                Camera = camera;
                HdCamera = hdCamera;
                AddedHdCamera = addedHdCamera;
                Role = role;
                Policy = policy;
                Original = CameraOriginalState.Capture(camera, hdCamera);
                HistoryResetFrame = -1;
            }

            public Camera Camera { get; }
            public HDAdditionalCameraData HdCamera { get; }
            public bool AddedHdCamera { get; }
            public CameraOriginalState Original { get; }
            public AntiAliasingCameraRole Role { get; set; }
            public AntiAliasingCameraPolicy Policy { get; set; }
            public ResolvedConfiguration LastResolved { get; set; }
            public int HistoryResetFrame { get; set; }
            public bool HistoryResetFrameRendered { get; set; }
            public TemporalHistoryResetReason LastHistoryResetReason { get; set; }
            public bool HasContinuitySample { get; set; }
            public Vector3 LastPosition { get; set; }
            public Quaternion LastRotation { get; set; }
            public float LastFieldOfView { get; set; }
            public int LastPixelWidth { get; set; }
            public int LastPixelHeight { get; set; }
            public bool HasResolutionTelemetry { get; set; }
            public Vector2Int LastOutputResolution { get; set; }
            public Vector2Int LastInternalResolution { get; set; }
            public float LastDynamicResolutionScale { get; set; }

            public void Restore()
            {
                if (Camera == null || HdCamera == null)
                {
                    return;
                }

                Original.Restore(Camera, HdCamera);
                if (AddedHdCamera)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(HdCamera);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(HdCamera);
                    }
                }
            }
        }

        private readonly struct CameraOriginalState
        {
            private CameraOriginalState(
                bool cameraAllowMsaa,
                bool cameraAllowDynamicResolution,
                bool hdAllowDynamicResolution,
                bool allowDlss,
                bool allowFsr2,
                HDAdditionalCameraData.AntialiasingMode antialiasing,
                HDAdditionalCameraData.SMAAQualityLevel smaaQuality,
                HDAdditionalCameraData.TAAQualityLevel taaQuality,
                HDAdditionalCameraData.TAASharpenMode sharpenMode,
                float sharpenStrength,
                float ringingReduction,
                float historySharpening,
                float antiFlicker,
                float motionVectorRejection,
                bool antiHistoryRinging,
                float baseBlendFactor,
                float jitterScale,
                bool customRenderingSettings,
                FrameSettings frameSettings,
                FrameSettingsOverrideMask frameSettingsMask,
                bool dlssCustomQuality,
                uint dlssQuality,
                bool dlssCustomAttributes,
                bool dlssOptimalSettings)
            {
                CameraAllowMsaa = cameraAllowMsaa;
                CameraAllowDynamicResolution = cameraAllowDynamicResolution;
                HdAllowDynamicResolution = hdAllowDynamicResolution;
                AllowDlss = allowDlss;
                AllowFsr2 = allowFsr2;
                Antialiasing = antialiasing;
                SmaaQuality = smaaQuality;
                TaaQuality = taaQuality;
                SharpenMode = sharpenMode;
                SharpenStrength = sharpenStrength;
                RingingReduction = ringingReduction;
                HistorySharpening = historySharpening;
                AntiFlicker = antiFlicker;
                MotionVectorRejection = motionVectorRejection;
                AntiHistoryRinging = antiHistoryRinging;
                BaseBlendFactor = baseBlendFactor;
                JitterScale = jitterScale;
                CustomRenderingSettings = customRenderingSettings;
                FrameSettings = frameSettings;
                FrameSettingsMask = frameSettingsMask;
                DlssCustomQuality = dlssCustomQuality;
                DlssQuality = dlssQuality;
                DlssCustomAttributes = dlssCustomAttributes;
                DlssOptimalSettings = dlssOptimalSettings;
            }

            private bool CameraAllowMsaa { get; }
            private bool CameraAllowDynamicResolution { get; }
            private bool HdAllowDynamicResolution { get; }
            private bool AllowDlss { get; }
            private bool AllowFsr2 { get; }
            private HDAdditionalCameraData.AntialiasingMode Antialiasing { get; }
            private HDAdditionalCameraData.SMAAQualityLevel SmaaQuality { get; }
            private HDAdditionalCameraData.TAAQualityLevel TaaQuality { get; }
            private HDAdditionalCameraData.TAASharpenMode SharpenMode { get; }
            private float SharpenStrength { get; }
            private float RingingReduction { get; }
            private float HistorySharpening { get; }
            private float AntiFlicker { get; }
            private float MotionVectorRejection { get; }
            private bool AntiHistoryRinging { get; }
            private float BaseBlendFactor { get; }
            private float JitterScale { get; }
            private bool CustomRenderingSettings { get; }
            private FrameSettings FrameSettings { get; }
            private FrameSettingsOverrideMask FrameSettingsMask { get; }
            private bool DlssCustomQuality { get; }
            private uint DlssQuality { get; }
            private bool DlssCustomAttributes { get; }
            private bool DlssOptimalSettings { get; }

            public static CameraOriginalState Capture(
                Camera camera,
                HDAdditionalCameraData hdCamera)
            {
                return new CameraOriginalState(
                    camera.allowMSAA,
                    camera.allowDynamicResolution,
                    hdCamera.allowDynamicResolution,
                    hdCamera.allowDeepLearningSuperSampling,
                    hdCamera.allowFidelityFX2SuperResolution,
                    hdCamera.antialiasing,
                    hdCamera.SMAAQuality,
                    hdCamera.TAAQuality,
                    hdCamera.taaSharpenMode,
                    hdCamera.taaSharpenStrength,
                    hdCamera.taaRingingReduction,
                    hdCamera.taaHistorySharpening,
                    hdCamera.taaAntiFlicker,
                    hdCamera.taaMotionVectorRejection,
                    hdCamera.taaAntiHistoryRinging,
                    hdCamera.taaBaseBlendFactor,
                    hdCamera.taaJitterScale,
                    hdCamera.customRenderingSettings,
                    hdCamera.renderingPathCustomFrameSettings,
                    hdCamera.renderingPathCustomFrameSettingsOverrideMask,
                    hdCamera.deepLearningSuperSamplingUseCustomQualitySettings,
                    hdCamera.deepLearningSuperSamplingQuality,
                    hdCamera.deepLearningSuperSamplingUseCustomAttributes,
                    hdCamera.deepLearningSuperSamplingUseOptimalSettings);
            }

            public void Restore(Camera camera, HDAdditionalCameraData hdCamera)
            {
                camera.allowMSAA = CameraAllowMsaa;
                camera.allowDynamicResolution = CameraAllowDynamicResolution;
                hdCamera.allowDynamicResolution = HdAllowDynamicResolution;
                hdCamera.allowDeepLearningSuperSampling = AllowDlss;
                hdCamera.allowFidelityFX2SuperResolution = AllowFsr2;
                hdCamera.antialiasing = Antialiasing;
                hdCamera.SMAAQuality = SmaaQuality;
                hdCamera.TAAQuality = TaaQuality;
                hdCamera.taaSharpenMode = SharpenMode;
                hdCamera.taaSharpenStrength = SharpenStrength;
                hdCamera.taaRingingReduction = RingingReduction;
                hdCamera.taaHistorySharpening = HistorySharpening;
                hdCamera.taaAntiFlicker = AntiFlicker;
                hdCamera.taaMotionVectorRejection = MotionVectorRejection;
                hdCamera.taaAntiHistoryRinging = AntiHistoryRinging;
                hdCamera.taaBaseBlendFactor = BaseBlendFactor;
                hdCamera.taaJitterScale = JitterScale;
                hdCamera.deepLearningSuperSamplingUseCustomQualitySettings = DlssCustomQuality;
                hdCamera.deepLearningSuperSamplingQuality = DlssQuality;
                hdCamera.deepLearningSuperSamplingUseCustomAttributes = DlssCustomAttributes;
                hdCamera.deepLearningSuperSamplingUseOptimalSettings = DlssOptimalSettings;
                RestoreFrameSettings(hdCamera);
            }

            public void RestoreFrameSettings(HDAdditionalCameraData hdCamera)
            {
                hdCamera.customRenderingSettings = CustomRenderingSettings;
                hdCamera.renderingPathCustomFrameSettings = FrameSettings;
                hdCamera.renderingPathCustomFrameSettingsOverrideMask = FrameSettingsMask;
            }

            public void RestoreMotionVectorFrameSettings(HDAdditionalCameraData hdCamera)
            {
                RestoreFrameSetting(hdCamera, FrameSettingsField.MotionVectors);
                RestoreFrameSetting(hdCamera, FrameSettingsField.ObjectMotionVectors);
                RestoreFrameSetting(hdCamera, FrameSettingsField.TransparentsWriteMotionVector);
            }

            private void RestoreFrameSetting(
                HDAdditionalCameraData hdCamera,
                FrameSettingsField field)
            {
                hdCamera.renderingPathCustomFrameSettings.SetEnabled(
                    field,
                    FrameSettings.IsEnabled(field));
                FrameSettingsOverrideMask currentMask =
                    hdCamera.renderingPathCustomFrameSettingsOverrideMask;
                currentMask.mask[(uint)field] = FrameSettingsMask.mask[(uint)field];
                hdCamera.renderingPathCustomFrameSettingsOverrideMask = currentMask;
            }
        }
    }
}
