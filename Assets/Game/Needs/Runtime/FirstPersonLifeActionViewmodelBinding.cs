using System;
using UnityEngine;

namespace MSC.Needs
{
    public enum FirstPersonLifeActionVisual
    {
        None = 0,
        Drink = 1,
        Smoke = 2,
        Hello = 3,
        MiddleFinger = 4,
        Push = 5,
        Fist = 6,
    }

    /// <summary>
    /// Project-owned wrapper around the sanitized private Phase 1 player-hand
    /// presentation. The generated prefab may contain donor-derived meshes and
    /// legacy clips, but never owns gameplay state, input or action timing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirstPersonLifeActionViewmodelBinding : MonoBehaviour
    {
        private const int CurrentDrinkPresentationSchemaVersion = 1;

        public const string ResourcesPath =
            "Phase1PlayerViewmodel/Phase1PlayerViewmodelBinding";
        public const string ExpectedBindingId =
            "phase1.player.viewmodel.legacy.v1";
        public const string ExpectedReplacementKey =
            "presentation.player.viewmodel.phase2";

        [SerializeField]
        private string bindingId = ExpectedBindingId;

        [SerializeField]
        private string productionReplacementKey = ExpectedReplacementKey;

        [SerializeField]
        private GameObject drinkRoot;

        [SerializeField]
        private Animation drinkAnimation;

        [SerializeField]
        private AnimationClip drinkRotate;

        [SerializeField]
        private AnimationClip drinkRotateShort;

        [SerializeField]
        private AnimationClip drinkThrow;

        [SerializeField]
        private AnimationClip drinkSpray;

        [SerializeField]
        private Transform drinkGripAnchor;

        [SerializeField]
        private int drinkPresentationSchemaVersion;

        [SerializeField, Min(0f)]
        private float drinkReadyPoseTimeSeconds = 0.5f;

        [SerializeField]
        private Vector3 drinkReadyLocalPositionOffset =
            new Vector3(-0.22f, 0.06f, 0.54f);

        [SerializeField]
        private Vector3 drinkReadyLocalEulerOffset =
            new Vector3(122f, -12f, -8f);

        [SerializeField]
        private Vector3 drinkReadyEntryLocalPositionOffset =
            new Vector3(0f, -0.18f, 0.04f);

        [SerializeField, Min(0.05f)]
        private float drinkPoseTransitionDuration = 0.26f;

        [SerializeField]
        private GameObject smokeRoot;

        [SerializeField]
        private Animation smokeAnimation;

        [SerializeField]
        private AnimationClip smokeIn;

        [SerializeField]
        private AnimationClip smokeLightUp;

        [SerializeField]
        private AnimationClip smokeOut;

        [SerializeField]
        private AnimationClip smokePutOff;

        [SerializeField]
        private AnimationClip smokeReset;

        [SerializeField]
        private Transform cigaretteGripAnchor;

        [SerializeField]
        private GameObject helloRoot;

        [SerializeField]
        private Animation helloAnimation;

        [SerializeField]
        private AnimationClip helloClip;

        [SerializeField]
        private GameObject middleFingerRoot;

        [SerializeField]
        private Animation middleFingerAnimation;

        [SerializeField]
        private AnimationClip middleFingerClip;

        [SerializeField]
        private GameObject pushRoot;

        [SerializeField]
        private Animation pushAnimation;

        [SerializeField]
        private AnimationClip pushOnClip;

        [SerializeField]
        private AnimationClip pushOffClip;

        [SerializeField]
        private GameObject fistRoot;

        [SerializeField]
        private Animation fistAnimation;

        [SerializeField]
        private AnimationClip fistClip;

        private FirstPersonLifeActionVisual activeVisual;
        private float autoStopAt;
        private GameObject sequenceRoot;
        private Animation sequenceAnimation;
        private AnimationClip[] sequenceClips = Array.Empty<AnimationClip>();
        private int sequenceClipIndex;
        private float sequenceAdvanceAt;
        private bool sequenceStopsAfterLastClip;
        private bool neutralRootPoseCaptured;
        private Vector3 neutralRootLocalPosition;
        private Quaternion neutralRootLocalRotation;
        private bool drinkReadyPresentationActive;
        private bool drinkRootTransitionActive;
        private bool finalizeDrinkReadyAfterTransition;
        private AnimationClip directlySampledClip;
        private GameObject directlySampledTarget;
        private float directlySampledStartedAt;
        private float directlySampledStartTime;
        private float directlySampledSpeed;
        private WrapMode directlySampledWrapMode;
        private float drinkRootTransitionStartedAt;
        private float drinkRootTransitionDuration;
        private Vector3 drinkRootTransitionStartPosition;
        private Quaternion drinkRootTransitionStartRotation;
        private Vector3 drinkRootTransitionTargetPosition;
        private Quaternion drinkRootTransitionTargetRotation;

        public string BindingId => bindingId ?? string.Empty;

        public string ProductionReplacementKey =>
            productionReplacementKey ?? string.Empty;

        public FirstPersonLifeActionVisual ActiveVisual => activeVisual;

        public Transform DrinkGripAnchor => drinkGripAnchor;

        public Transform CigaretteGripAnchor => cigaretteGripAnchor;

        public bool IsConfigured =>
            string.Equals(
                bindingId,
                ExpectedBindingId,
                StringComparison.Ordinal) &&
            string.Equals(
                productionReplacementKey,
                ExpectedReplacementKey,
                StringComparison.Ordinal) &&
            drinkRoot != null &&
            drinkAnimation != null &&
            drinkRotate != null &&
            drinkRotateShort != null &&
            drinkThrow != null &&
            drinkSpray != null &&
            drinkGripAnchor != null &&
            drinkPresentationSchemaVersion ==
                CurrentDrinkPresentationSchemaVersion &&
            float.IsFinite(drinkReadyPoseTimeSeconds) &&
            drinkReadyPoseTimeSeconds >= 0f &&
            IsFinite(drinkReadyLocalPositionOffset) &&
            IsFinite(drinkReadyLocalEulerOffset) &&
            IsFinite(drinkReadyEntryLocalPositionOffset) &&
            float.IsFinite(drinkPoseTransitionDuration) &&
            drinkPoseTransitionDuration >= 0.05f &&
            smokeRoot != null &&
            smokeAnimation != null &&
            smokeIn != null &&
            smokeLightUp != null &&
            smokeOut != null &&
            smokePutOff != null &&
            smokeReset != null &&
            cigaretteGripAnchor != null &&
            helloRoot != null &&
            helloAnimation != null &&
            helloClip != null &&
            middleFingerRoot != null &&
            middleFingerAnimation != null &&
            middleFingerClip != null &&
            pushRoot != null &&
            pushAnimation != null &&
            pushOnClip != null &&
            pushOffClip != null &&
            fistRoot != null &&
            fistAnimation != null &&
            fistClip != null;

        public static bool TryInstantiateLocalPhase1(
            Transform parent,
            out FirstPersonLifeActionViewmodelBinding binding)
        {
            binding = null;
            if (parent == null)
            {
                return false;
            }

            GameObject prefab = Resources.Load<GameObject>(ResourcesPath);
            if (prefab == null ||
                !prefab.TryGetComponent(
                    out FirstPersonLifeActionViewmodelBinding source) ||
                !source.IsConfigured)
            {
                return false;
            }

            GameObject instance = Instantiate(prefab, parent, false);
            instance.name = "Phase 1 Legacy Player Viewmodel";
            instance.hideFlags = HideFlags.DontSave;
            instance.transform.SetLocalPositionAndRotation(
                Vector3.zero,
                Quaternion.identity);
            instance.transform.localScale = Vector3.one;
            if (!instance.TryGetComponent(out binding) ||
                !binding.IsConfigured)
            {
                Destroy(instance);
                binding = null;
                return false;
            }

            binding.ConfigureRuntimeSkinning();
            binding.Stop();
            return true;
        }

        public bool Play(FirstPersonLifeActionVisual visual)
        {
            if (visual == FirstPersonLifeActionVisual.Drink)
            {
                return PlayDrink();
            }

            Stop();
            switch (visual)
            {
                case FirstPersonLifeActionVisual.Smoke:
                    return PlaySequence(
                        visual,
                        smokeRoot,
                        smokeAnimation,
                        smokeIn,
                        smokeLightUp,
                        smokeOut,
                        smokePutOff,
                        smokeReset);

                case FirstPersonLifeActionVisual.Hello:
                    return PlayClip(
                        visual,
                        helloRoot,
                        helloAnimation,
                        helloClip,
                        WrapMode.Once,
                        autoStop: true);

                case FirstPersonLifeActionVisual.MiddleFinger:
                    return PlayClip(
                        visual,
                        middleFingerRoot,
                        middleFingerAnimation,
                        middleFingerClip,
                        WrapMode.Once,
                        autoStop: true);

                case FirstPersonLifeActionVisual.Push:
                    return PlaySequence(
                        visual,
                        pushRoot,
                        pushAnimation,
                        pushOnClip,
                        pushOffClip,
                        stopAfterLastClip: true);

                case FirstPersonLifeActionVisual.Fist:
                    return PlayClip(
                        visual,
                        fistRoot,
                        fistAnimation,
                        fistClip,
                        WrapMode.Once,
                        autoStop: true);

                default:
                    return false;
            }
        }

        public bool ShowDrinkReady()
        {
            return ShowDrinkReady(smooth: false);
        }

        public bool ShowDrinkReady(bool smooth)
        {
            if (drinkRoot == null ||
                drinkAnimation == null ||
                drinkRotate == null ||
                drinkGripAnchor == null)
            {
                return false;
            }

            CaptureNeutralRootPose();
            bool returningFromDrink =
                activeVisual == FirstPersonLifeActionVisual.Drink &&
                !drinkReadyPresentationActive &&
                drinkRoot.activeSelf;
            if (activeVisual != FirstPersonLifeActionVisual.Drink)
            {
                Stop();
                CaptureNeutralRootPose();
            }

            ClearSequenceState();
            autoStopAt = 0f;
            SetRootActive(drinkRoot, true);
            RegisterClip(
                drinkAnimation,
                drinkRotate,
                makeDefault: true);
            AnimationState state = drinkAnimation[drinkRotate.name];
            if (state == null)
            {
                SetRootActive(drinkRoot, false);
                return false;
            }

            state.wrapMode = WrapMode.ClampForever;
            activeVisual = FirstPersonLifeActionVisual.Drink;
            drinkReadyPresentationActive = true;

            if (returningFromDrink && smooth)
            {
                float readyTime = GetDrinkReadyPoseTime();
                float currentTime = directlySampledClip == drinkRotate
                    ? Mathf.Clamp(
                        GetDirectSampleTime(),
                        readyTime,
                        drinkRotate.length)
                    : Mathf.Clamp(
                        state.time,
                        readyTime,
                        drinkRotate.length);

                float rewindDuration = Mathf.Max(
                    0.05f,
                    drinkPoseTransitionDuration);
                state.wrapMode = WrapMode.ClampForever;
                state.speed = currentTime > readyTime + 0.001f
                    ? -(currentTime - readyTime) / rewindDuration
                    : 0f;
                BeginDirectSampling(
                    drinkAnimation,
                    drinkRotate,
                    currentTime,
                    state.speed,
                    WrapMode.ClampForever);
                StartDrinkRootTransition(
                    GetDrinkReadyRootPosition(),
                    GetDrinkReadyRootRotation(),
                    rewindDuration,
                    finalizeReady: true);
                return true;
            }

            HoldDrinkReadySample();
            if (smooth)
            {
                transform.localPosition =
                    GetDrinkReadyRootPosition() +
                    drinkReadyEntryLocalPositionOffset;
                transform.localRotation = GetDrinkReadyRootRotation();
                StartDrinkRootTransition(
                    GetDrinkReadyRootPosition(),
                    GetDrinkReadyRootRotation(),
                    drinkPoseTransitionDuration,
                    finalizeReady: true);
            }
            else
            {
                SetDrinkRootPose(
                    GetDrinkReadyRootPosition(),
                    GetDrinkReadyRootRotation());
            }

            return true;
        }

        public void Stop()
        {
            StopAnimation(drinkAnimation);
            StopAnimation(smokeAnimation);
            StopAnimation(helloAnimation);
            StopAnimation(middleFingerAnimation);
            StopAnimation(pushAnimation);
            StopAnimation(fistAnimation);
            SetRootActive(drinkRoot, false);
            SetRootActive(smokeRoot, false);
            SetRootActive(helloRoot, false);
            SetRootActive(middleFingerRoot, false);
            SetRootActive(pushRoot, false);
            SetRootActive(fistRoot, false);
            activeVisual = FirstPersonLifeActionVisual.None;
            autoStopAt = 0f;
            ClearDirectSamplingState();
            ClearSequenceState();
            drinkReadyPresentationActive = false;
            drinkRootTransitionActive = false;
            finalizeDrinkReadyAfterTransition = false;
            ResetDrinkPresentationRoot();
        }

        public void ConfigureMetadata(
            string configuredBindingId,
            string configuredReplacementKey)
        {
            bindingId = configuredBindingId ?? string.Empty;
            productionReplacementKey =
                configuredReplacementKey ?? string.Empty;
        }

        public void ConfigureDrink(
            GameObject configuredRoot,
            Animation configuredAnimation,
            AnimationClip configuredRotate,
            AnimationClip configuredRotateShort,
            AnimationClip configuredThrow,
            AnimationClip configuredSpray,
            Transform configuredGripAnchor)
        {
            drinkRoot = configuredRoot;
            drinkAnimation = configuredAnimation;
            drinkRotate = configuredRotate;
            drinkRotateShort = configuredRotateShort;
            drinkThrow = configuredThrow;
            drinkSpray = configuredSpray;
            drinkGripAnchor = configuredGripAnchor;
            RegisterClip(drinkAnimation, drinkRotate, makeDefault: true);
            RegisterClip(drinkAnimation, drinkRotateShort, makeDefault: false);
            RegisterClip(drinkAnimation, drinkThrow, makeDefault: false);
            RegisterClip(drinkAnimation, drinkSpray, makeDefault: false);
            if (drinkPresentationSchemaVersion !=
                CurrentDrinkPresentationSchemaVersion)
            {
                ConfigureDrinkReadyPresentation(
                    drinkReadyPoseTimeSeconds,
                    drinkReadyLocalPositionOffset,
                    drinkReadyLocalEulerOffset,
                    drinkReadyEntryLocalPositionOffset,
                    drinkPoseTransitionDuration);
            }
        }

        public void ConfigureDrinkReadyPresentation(
            float readyPoseTimeSeconds,
            Vector3 readyLocalPositionOffset,
            Vector3 readyLocalEulerOffset,
            Vector3 entryLocalPositionOffset,
            float transitionDuration)
        {
            if (!float.IsFinite(readyPoseTimeSeconds) ||
                readyPoseTimeSeconds < 0f ||
                !IsFinite(readyLocalPositionOffset) ||
                !IsFinite(readyLocalEulerOffset) ||
                !IsFinite(entryLocalPositionOffset) ||
                !float.IsFinite(transitionDuration) ||
                transitionDuration < 0.05f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(transitionDuration),
                    "Drink-ready presentation values must be finite and the " +
                    "transition must be at least 0.05 seconds.");
            }

            drinkPresentationSchemaVersion =
                CurrentDrinkPresentationSchemaVersion;
            drinkReadyPoseTimeSeconds = readyPoseTimeSeconds;
            drinkReadyLocalPositionOffset = readyLocalPositionOffset;
            drinkReadyLocalEulerOffset = readyLocalEulerOffset;
            drinkReadyEntryLocalPositionOffset =
                entryLocalPositionOffset;
            drinkPoseTransitionDuration = transitionDuration;
        }

        public void ConfigureSmoke(
            GameObject configuredRoot,
            Animation configuredAnimation,
            AnimationClip configuredIn,
            AnimationClip configuredLightUp,
            AnimationClip configuredOut,
            AnimationClip configuredPutOff,
            AnimationClip configuredReset,
            Transform configuredCigaretteGripAnchor)
        {
            smokeRoot = configuredRoot;
            smokeAnimation = configuredAnimation;
            smokeIn = configuredIn;
            smokeLightUp = configuredLightUp;
            smokeOut = configuredOut;
            smokePutOff = configuredPutOff;
            smokeReset = configuredReset;
            cigaretteGripAnchor = configuredCigaretteGripAnchor;
            RegisterClip(smokeAnimation, smokeIn, makeDefault: true);
            RegisterClip(smokeAnimation, smokeLightUp, makeDefault: false);
            RegisterClip(smokeAnimation, smokeOut, makeDefault: false);
            RegisterClip(smokeAnimation, smokePutOff, makeDefault: false);
            RegisterClip(smokeAnimation, smokeReset, makeDefault: false);
        }

        public void ConfigureHello(
            GameObject configuredRoot,
            Animation configuredAnimation,
            AnimationClip configuredClip)
        {
            helloRoot = configuredRoot;
            helloAnimation = configuredAnimation;
            helloClip = configuredClip;
            RegisterClip(helloAnimation, helloClip, makeDefault: true);
        }

        public void ConfigureMiddleFinger(
            GameObject configuredRoot,
            Animation configuredAnimation,
            AnimationClip configuredClip)
        {
            middleFingerRoot = configuredRoot;
            middleFingerAnimation = configuredAnimation;
            middleFingerClip = configuredClip;
            RegisterClip(
                middleFingerAnimation,
                middleFingerClip,
                makeDefault: true);
        }

        public void ConfigurePush(
            GameObject configuredRoot,
            Animation configuredAnimation,
            AnimationClip configuredOn,
            AnimationClip configuredOff)
        {
            pushRoot = configuredRoot;
            pushAnimation = configuredAnimation;
            pushOnClip = configuredOn;
            pushOffClip = configuredOff;
            RegisterClip(pushAnimation, pushOnClip, makeDefault: true);
            RegisterClip(pushAnimation, pushOffClip, makeDefault: false);
        }

        public void ConfigureFist(
            GameObject configuredRoot,
            Animation configuredAnimation,
            AnimationClip configuredClip)
        {
            fistRoot = configuredRoot;
            fistAnimation = configuredAnimation;
            fistClip = configuredClip;
            RegisterClip(fistAnimation, fistClip, makeDefault: true);
        }

        private bool PlayClip(
            FirstPersonLifeActionVisual visual,
            GameObject root,
            Animation animation,
            AnimationClip clip,
            WrapMode wrapMode,
            bool autoStop)
        {
            return PlayClip(
                visual,
                root,
                animation,
                clip,
                wrapMode,
                autoStop,
                startTime: 0f,
                playbackSpeed: 1f);
        }

        private bool PlayClip(
            FirstPersonLifeActionVisual visual,
            GameObject root,
            Animation animation,
            AnimationClip clip,
            WrapMode wrapMode,
            bool autoStop,
            float startTime,
            float playbackSpeed)
        {
            if (root == null || animation == null || clip == null)
            {
                return false;
            }

            ConfigureRuntimeSkinning(root);
            SetRootActive(root, true);
            RegisterClip(animation, clip, makeDefault: true);
            AnimationState state = animation[clip.name];
            if (state == null)
            {
                SetRootActive(root, false);
                return false;
            }

            state.wrapMode = wrapMode;
            state.speed = Mathf.Max(0.0001f, playbackSpeed);
            BeginDirectSampling(
                animation,
                clip,
                startTime,
                state.speed,
                wrapMode);
            activeVisual = visual;
            autoStopAt = autoStop
                ? Time.unscaledTime + Mathf.Max(
                    0.05f,
                    (clip.length - state.time) / state.speed)
                : 0f;
            return true;
        }

        private float GetDrinkReadyPoseTime()
        {
            return drinkRotate != null
                ? Mathf.Clamp(
                    drinkReadyPoseTimeSeconds,
                    0f,
                    drinkRotate.length)
                : 0f;
        }

        private bool PlayDrink()
        {
            if (drinkRoot == null ||
                drinkAnimation == null ||
                drinkRotate == null ||
                drinkGripAnchor == null)
            {
                return false;
            }

            ConfigureRuntimeSkinning(drinkRoot);
            CaptureNeutralRootPose();
            if (activeVisual != FirstPersonLifeActionVisual.Drink)
            {
                Stop();
                CaptureNeutralRootPose();
                SetDrinkRootPose(
                    GetDrinkReadyRootPosition(),
                    GetDrinkReadyRootRotation());
            }

            ClearSequenceState();
            autoStopAt = 0f;
            SetRootActive(drinkRoot, true);
            RegisterClip(
                drinkAnimation,
                drinkRotate,
                makeDefault: true);
            AnimationState state = drinkAnimation[drinkRotate.name];
            if (state == null)
            {
                SetRootActive(drinkRoot, false);
                return false;
            }

            float readyTime = GetDrinkReadyPoseTime();
            float remainingDuration = Mathf.Max(
                0.01f,
                drinkRotate.length - readyTime);
            state.wrapMode = WrapMode.ClampForever;
            state.speed =
                remainingDuration / Mathf.Max(0.01f, drinkRotate.length);
            BeginDirectSampling(
                drinkAnimation,
                drinkRotate,
                readyTime,
                state.speed,
                WrapMode.ClampForever);
            activeVisual = FirstPersonLifeActionVisual.Drink;
            drinkReadyPresentationActive = false;
            StartDrinkRootTransition(
                neutralRootLocalPosition,
                neutralRootLocalRotation,
                drinkPoseTransitionDuration,
                finalizeReady: false);
            return true;
        }

        private void HoldDrinkReadySample()
        {
            if (drinkAnimation == null || drinkRotate == null)
            {
                return;
            }

            RegisterClip(
                drinkAnimation,
                drinkRotate,
                makeDefault: true);
            AnimationState state = drinkAnimation[drinkRotate.name];
            if (state == null)
            {
                return;
            }

            state.wrapMode = WrapMode.ClampForever;
            state.speed = 0f;
            BeginDirectSampling(
                drinkAnimation,
                drinkRotate,
                GetDrinkReadyPoseTime(),
                0f,
                WrapMode.ClampForever);
        }

        private void StartDrinkRootTransition(
            Vector3 targetPosition,
            Quaternion targetRotation,
            float duration,
            bool finalizeReady)
        {
            drinkRootTransitionStartPosition = transform.localPosition;
            drinkRootTransitionStartRotation = transform.localRotation;
            drinkRootTransitionTargetPosition = targetPosition;
            drinkRootTransitionTargetRotation = targetRotation.normalized;
            drinkRootTransitionStartedAt = Time.unscaledTime;
            drinkRootTransitionDuration = Mathf.Max(0.05f, duration);
            finalizeDrinkReadyAfterTransition = finalizeReady;
            drinkRootTransitionActive = true;
        }

        private void UpdateDrinkRootTransition()
        {
            if (!drinkRootTransitionActive)
            {
                return;
            }

            float normalized = Mathf.Clamp01(
                (Time.unscaledTime - drinkRootTransitionStartedAt) /
                drinkRootTransitionDuration);
            float eased = Mathf.SmoothStep(0f, 1f, normalized);
            SetDrinkRootPose(
                Vector3.LerpUnclamped(
                    drinkRootTransitionStartPosition,
                    drinkRootTransitionTargetPosition,
                    eased),
                Quaternion.SlerpUnclamped(
                    drinkRootTransitionStartRotation,
                    drinkRootTransitionTargetRotation,
                    eased));
            if (normalized < 1f)
            {
                return;
            }

            drinkRootTransitionActive = false;
            if (finalizeDrinkReadyAfterTransition)
            {
                HoldDrinkReadySample();
            }

            finalizeDrinkReadyAfterTransition = false;
        }

        private void CaptureNeutralRootPose()
        {
            if (neutralRootPoseCaptured)
            {
                return;
            }

            neutralRootLocalPosition = transform.localPosition;
            neutralRootLocalRotation = transform.localRotation;
            neutralRootPoseCaptured = true;
        }

        private Vector3 GetDrinkReadyRootPosition()
        {
            CaptureNeutralRootPose();
            return neutralRootLocalPosition +
                   neutralRootLocalRotation *
                   drinkReadyLocalPositionOffset;
        }

        private Quaternion GetDrinkReadyRootRotation()
        {
            CaptureNeutralRootPose();
            return neutralRootLocalRotation *
                   Quaternion.Euler(drinkReadyLocalEulerOffset);
        }

        private void ResetDrinkPresentationRoot()
        {
            CaptureNeutralRootPose();
            SetDrinkRootPose(
                neutralRootLocalPosition,
                neutralRootLocalRotation);
        }

        private void SetDrinkRootPose(
            Vector3 localPosition,
            Quaternion localRotation)
        {
            transform.SetLocalPositionAndRotation(
                localPosition,
                localRotation.normalized);
        }

        private void ClearSequenceState()
        {
            sequenceRoot = null;
            sequenceAnimation = null;
            sequenceClips = Array.Empty<AnimationClip>();
            sequenceClipIndex = 0;
            sequenceAdvanceAt = 0f;
            sequenceStopsAfterLastClip = false;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) &&
                   float.IsFinite(value.y) &&
                   float.IsFinite(value.z);
        }

        private bool PlaySequence(
            FirstPersonLifeActionVisual visual,
            GameObject root,
            Animation animation,
            params AnimationClip[] clips)
        {
            return PlaySequence(
                visual,
                root,
                animation,
                clips,
                stopAfterLastClip: false);
        }

        private bool PlaySequence(
            FirstPersonLifeActionVisual visual,
            GameObject root,
            Animation animation,
            AnimationClip firstClip,
            AnimationClip secondClip,
            bool stopAfterLastClip)
        {
            return PlaySequence(
                visual,
                root,
                animation,
                new[] { firstClip, secondClip },
                stopAfterLastClip);
        }

        private bool PlaySequence(
            FirstPersonLifeActionVisual visual,
            GameObject root,
            Animation animation,
            AnimationClip[] clips,
            bool stopAfterLastClip)
        {
            if (root == null || animation == null ||
                clips == null || clips.Length == 0)
            {
                return false;
            }

            for (int index = 0; index < clips.Length; index++)
            {
                if (clips[index] == null)
                {
                    return false;
                }

                RegisterClip(
                    animation,
                    clips[index],
                    makeDefault: index == 0);
            }

            ConfigureRuntimeSkinning(root);
            SetRootActive(root, true);
            sequenceRoot = root;
            sequenceAnimation = animation;
            sequenceClips = (AnimationClip[])clips.Clone();
            sequenceClipIndex = 0;
            sequenceStopsAfterLastClip = stopAfterLastClip;
            activeVisual = visual;
            autoStopAt = 0f;
            return PlayCurrentSequenceClip();
        }

        private bool PlayCurrentSequenceClip()
        {
            if (sequenceRoot == null ||
                sequenceAnimation == null ||
                sequenceClips == null ||
                sequenceClipIndex < 0 ||
                sequenceClipIndex >= sequenceClips.Length)
            {
                return false;
            }

            AnimationClip clip = sequenceClips[sequenceClipIndex];
            AnimationState state = sequenceAnimation[clip.name];
            if (state == null)
            {
                Stop();
                return false;
            }

            bool isLast = sequenceClipIndex == sequenceClips.Length - 1;
            state.wrapMode =
                isLast && !sequenceStopsAfterLastClip
                    ? WrapMode.ClampForever
                    : WrapMode.Once;
            state.speed = 1f;
            state.time = 0f;
            BeginDirectSampling(
                sequenceAnimation,
                clip,
                0f,
                1f,
                state.wrapMode);
            sequenceAdvanceAt =
                Time.unscaledTime + Mathf.Max(0.01f, clip.length);
            return true;
        }

        private void BeginDirectSampling(
            Animation animation,
            AnimationClip clip,
            float startTime,
            float speed,
            WrapMode wrapMode)
        {
            animation.Stop();
            directlySampledClip = clip;
            directlySampledTarget = animation.gameObject;
            directlySampledStartedAt = Time.unscaledTime;
            directlySampledStartTime = Mathf.Clamp(startTime, 0f, clip.length);
            directlySampledSpeed = float.IsFinite(speed) ? speed : 0f;
            directlySampledWrapMode = wrapMode;
            SampleDirectAnimation();
        }

        private void SampleDirectAnimation()
        {
            if (directlySampledClip == null || directlySampledTarget == null)
            {
                return;
            }

            float sampleTime = GetDirectSampleTime();
            if (directlySampledWrapMode == WrapMode.Loop &&
                directlySampledClip.length > 0f)
            {
                sampleTime = Mathf.Repeat(
                    sampleTime,
                    directlySampledClip.length);
            }
            else
            {
                sampleTime = Mathf.Clamp(
                    sampleTime,
                    0f,
                    directlySampledClip.length);
            }

            // Unity 6's legacy Animation player evaluates this purchased
            // Generic FBX hierarchy incorrectly at runtime even though the
            // same AnimationClip is valid: fingers stretch through the lens
            // and the complete arm is translated into the camera. Direct clip
            // sampling uses Unity's correct Generic-clip evaluation path and
            // remains presentation-only; gameplay timing stays project-owned.
            directlySampledClip.SampleAnimation(
                directlySampledTarget,
                sampleTime);
        }

        private float GetDirectSampleTime()
        {
            float elapsed = Mathf.Max(
                0f,
                Time.unscaledTime - directlySampledStartedAt);
            return directlySampledStartTime +
                   elapsed * directlySampledSpeed;
        }

        private void ClearDirectSamplingState()
        {
            directlySampledClip = null;
            directlySampledTarget = null;
            directlySampledStartedAt = 0f;
            directlySampledStartTime = 0f;
            directlySampledSpeed = 0f;
            directlySampledWrapMode = WrapMode.Default;
        }

        private static void RegisterClip(
            Animation animation,
            AnimationClip clip,
            bool makeDefault)
        {
            if (animation == null || clip == null)
            {
                return;
            }

            if (animation.GetClip(clip.name) == null)
            {
                animation.AddClip(clip, clip.name);
            }

            if (makeDefault)
            {
                animation.clip = clip;
            }

            animation.playAutomatically = false;
            animation.animatePhysics = false;
            animation.cullingType =
                AnimationCullingType.AlwaysAnimate;
        }

        private static void StopAnimation(Animation animation)
        {
            if (animation != null)
            {
                animation.Stop();
            }
        }

        private static void SetRootActive(GameObject root, bool active)
        {
            if (root != null && root.activeSelf != active)
            {
                root.SetActive(active);
            }
        }

        private void ConfigureRuntimeSkinning()
        {
            foreach (SkinnedMeshRenderer renderer in
                     GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                ConfigureRuntimeSkinning(renderer);
            }
        }

        private static void ConfigureRuntimeSkinning(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (SkinnedMeshRenderer renderer in
                     root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                ConfigureRuntimeSkinning(renderer);
            }
        }

        private static void ConfigureRuntimeSkinning(
            SkinnedMeshRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            // The AXIS source uses a long camera-local hierarchy and eight
            // influences per vertex.  HDRP's first realtime render can reuse
            // stale skin matrices after an inactive action root is enabled,
            // stretching the forearm and fingers across Game View.  The audit
            // renderer already used these settings; runtime must use the exact
            // same contract instead of depending on non-serialized defaults.
            renderer.updateWhenOffscreen = true;
            renderer.forceMatrixRecalculationPerRender = true;
            renderer.quality = SkinQuality.Auto;
            renderer.skinnedMotionVectors = false;
        }

        private void OnDisable()
        {
            Stop();
        }

        private void Update()
        {
            UpdateDrinkRootTransition();
            SampleDirectAnimation();

            if (sequenceAdvanceAt > 0f &&
                Time.unscaledTime >= sequenceAdvanceAt)
            {
                bool hasNext =
                    sequenceClipIndex + 1 < sequenceClips.Length;
                if (hasNext)
                {
                    sequenceClipIndex++;
                    PlayCurrentSequenceClip();
                }
                else if (sequenceStopsAfterLastClip)
                {
                    Stop();
                }
                else
                {
                    sequenceAdvanceAt = 0f;
                }
            }

            if (autoStopAt > 0f && Time.unscaledTime >= autoStopAt)
            {
                Stop();
            }
        }
    }
}
