using System;
using MSC.Needs;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Needs.Presentation
{
    /// <summary>
    /// HDRP-only presentation adapter for fatigue. The needs simulation remains
    /// render-pipeline agnostic; this component owns and disposes its transient
    /// global volume.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerFatigueHdrpPresenter : MonoBehaviour
    {
        private const float DisabledWeightEpsilon = 0.0005f;
        private const float FullBlinkClosureThreshold = 0.985f;
        private const float MaximumFatigueThreshold = 99.999f;
        private const float BlackoutMinimumIntervalSeconds = 7f;
        private const float BlackoutMaximumIntervalSeconds = 12f;
        private const float BlackoutFadeInSeconds = 0.45f;
        private const float BlackoutHoldSeconds = 1.1f;
        private const float BlackoutFadeOutSeconds = 0.65f;
        private const float BlackoutDurationSeconds =
            BlackoutFadeInSeconds +
            BlackoutHoldSeconds +
            BlackoutFadeOutSeconds;

        [Header("Fatigue response")]
        [SerializeField, Range(0f, 100f)]
        private float effectOnset = 62f;
        [SerializeField, Range(0f, 100f)]
        private float fullEffectAt = 100f;
        [SerializeField, Min(0.01f)]
        private float fadeInSeconds = 1.8f;
        [SerializeField, Min(0.01f)]
        private float fadeOutSeconds = 1.15f;

        [Header("HDRP presentation")]
        [SerializeField, Range(0f, 1f)]
        private float maximumVolumeWeight = 0.72f;
        [SerializeField, Min(0f)]
        private float volumePriority = 800f;

        private PlayerNeedsRuntime needs;
        private GameObject volumeRoot;
        private Volume volume;
        private VolumeProfile profile;
        private Vignette vignette;
        private ColorAdjustments blinkColorAdjustments;
        private float targetWeight;
        private float currentWeight;
        private float fatigueSeverity;
        private float blinkClosure;
        private float nextBlinkAt = float.PositiveInfinity;
        private float blinkStartedAt;
        private uint blinkSequence;
        private bool blinkActive;
        private bool maximumFatigue;
        private bool initialized;

        /// <summary>
        /// Creates the owned runtime HDRP volume and starts observing fatigue.
        /// </summary>
        public void Initialize(PlayerNeedsRuntime configuredNeeds)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Fatigue HDRP presenter is already initialized.");
            }

            needs = configuredNeeds ??
                throw new ArgumentNullException(nameof(configuredNeeds));
            BuildRuntimeVolume();
            needs.StateChanged += HandleNeedsChanged;
            ApplySnapshot(needs.Snapshot, true);
            initialized = true;
        }

        private void HandleNeedsChanged(PlayerNeedsSnapshot snapshot)
        {
            ApplySnapshot(snapshot, false);
        }

        private void ApplySnapshot(
            PlayerNeedsSnapshot snapshot,
            bool applyImmediately)
        {
            bool previousMaximumFatigue = maximumFatigue;
            maximumFatigue =
                snapshot.Fatigue >= MaximumFatigueThreshold;
            float normalized = Mathf.InverseLerp(
                effectOnset,
                Mathf.Max(effectOnset + 0.01f, fullEffectAt),
                snapshot.Fatigue);
            normalized =
                normalized * normalized * (3f - 2f * normalized);
            float previousSeverity = fatigueSeverity;
            fatigueSeverity = normalized;
            targetWeight = normalized * maximumVolumeWeight;
            if (fatigueSeverity <= 0.08f)
            {
                ResetBlink();
            }
            else if (previousSeverity <= 0.08f ||
                     previousMaximumFatigue != maximumFatigue)
            {
                ScheduleNextBlink(Time.unscaledTime);
            }

            if (!applyImmediately)
            {
                if (targetWeight > DisabledWeightEpsilon &&
                    volume != null)
                {
                    volume.enabled = true;
                }

                return;
            }

            currentWeight = targetWeight;
            ApplyVolumeWeight();
        }

        private void Update()
        {
            if (!initialized || volume == null)
            {
                return;
            }

            UpdateBlink(Time.unscaledTime);

            float duration =
                targetWeight > currentWeight
                    ? fadeInSeconds
                    : fadeOutSeconds;
            float speed = maximumVolumeWeight / Mathf.Max(0.01f, duration);
            currentWeight = Mathf.MoveTowards(
                currentWeight,
                targetWeight,
                speed * Time.unscaledDeltaTime);
            ApplyVolumeWeight();
        }

        private void OnEnable()
        {
            if (initialized && needs != null)
            {
                ApplySnapshot(needs.Snapshot, true);
                if (fatigueSeverity > 0.08f)
                {
                    ScheduleNextBlink(Time.unscaledTime);
                }
            }
        }

        private void ApplyVolumeWeight()
        {
            if (volume == null)
            {
                return;
            }

            float blinkWeight = Mathf.Clamp01(
                blinkClosure / FullBlinkClosureThreshold);
            volume.weight = Mathf.Lerp(
                currentWeight,
                1f,
                blinkWeight);
            bool active =
                currentWeight > DisabledWeightEpsilon ||
                targetWeight > DisabledWeightEpsilon ||
                blinkWeight > DisabledWeightEpsilon;
            volume.enabled = active;

            if (vignette != null)
            {
                vignette.intensity.value = Mathf.Lerp(
                    0.32f,
                    1f,
                    blinkWeight);
                vignette.smoothness.value = Mathf.Lerp(
                    0.58f,
                    1f,
                    blinkWeight);
                vignette.roundness.value = Mathf.Lerp(
                    0.9f,
                    0.68f,
                    blinkWeight);
            }

            if (blinkColorAdjustments != null)
            {
                blinkColorAdjustments.active =
                    blinkWeight > DisabledWeightEpsilon;
                blinkColorAdjustments.colorFilter.value = Color.Lerp(
                    Color.white,
                    Color.black,
                    blinkWeight);
            }
        }

        private void UpdateBlink(float unscaledTime)
        {
            if (fatigueSeverity <= 0.08f)
            {
                ResetBlink();
                return;
            }

            if (!blinkActive && unscaledTime >= nextBlinkAt)
            {
                blinkActive = true;
                blinkStartedAt = unscaledTime;
            }

            if (!blinkActive)
            {
                blinkClosure = 0f;
                return;
            }

            float elapsed = unscaledTime - blinkStartedAt;
            bool blackout = maximumFatigue;
            float duration = blackout
                ? BlackoutDurationSeconds
                : FatigueBlinkResponse.BlinkDurationSeconds;
            blinkClosure = blackout
                ? EvaluateBlackoutClosure(elapsed)
                : FatigueBlinkResponse.EvaluateClosure(elapsed);
            if (elapsed < duration)
            {
                return;
            }

            blinkActive = false;
            blinkClosure = 0f;
            ScheduleNextBlink(unscaledTime);
        }

        private void ScheduleNextBlink(float unscaledTime)
        {
            blinkActive = false;
            blinkClosure = 0f;
            float normalInterval =
                FatigueBlinkResponse.GetIntervalSeconds(blinkSequence++);
            float interval = normalInterval;
            if (maximumFatigue)
            {
                float normalizedInterval = Mathf.InverseLerp(
                    FatigueBlinkResponse.MinimumIntervalSeconds,
                    FatigueBlinkResponse.MaximumIntervalSeconds,
                    normalInterval);
                interval = Mathf.Lerp(
                    BlackoutMinimumIntervalSeconds,
                    BlackoutMaximumIntervalSeconds,
                    normalizedInterval);
            }

            nextBlinkAt = unscaledTime + interval;
        }

        private static float EvaluateBlackoutClosure(float elapsed)
        {
            if (elapsed <= 0f)
            {
                return 0f;
            }

            if (elapsed < BlackoutFadeInSeconds)
            {
                return SmoothStep01(elapsed / BlackoutFadeInSeconds);
            }

            float fadeOutStart =
                BlackoutFadeInSeconds + BlackoutHoldSeconds;
            if (elapsed <= fadeOutStart)
            {
                return 1f;
            }

            if (elapsed < BlackoutDurationSeconds)
            {
                float fadeOut =
                    (elapsed - fadeOutStart) / BlackoutFadeOutSeconds;
                return 1f - SmoothStep01(fadeOut);
            }

            return 0f;
        }

        private static float SmoothStep01(float value)
        {
            float clamped = Mathf.Clamp01(value);
            return clamped * clamped * (3f - 2f * clamped);
        }

        private void ResetBlink()
        {
            blinkActive = false;
            blinkClosure = 0f;
            nextBlinkAt = float.PositiveInfinity;
        }

        private void BuildRuntimeVolume()
        {
            volumeRoot = new GameObject("09C Fatigue HDRP Volume")
            {
                hideFlags = HideFlags.DontSave,
                layer = 0,
            };
            volumeRoot.transform.SetParent(transform, false);

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "09C Fatigue HDRP Profile (Runtime)";
            profile.hideFlags = HideFlags.DontSave;

            DepthOfField depthOfField =
                profile.Add<DepthOfField>(true);
            depthOfField.active = true;
            depthOfField.focusMode.Override(DepthOfFieldMode.Manual);
            depthOfField.nearFocusStart.Override(0f);
            depthOfField.nearFocusEnd.Override(0.8f);
            depthOfField.farFocusStart.Override(3.5f);
            depthOfField.farFocusEnd.Override(12f);
            depthOfField.quality.overrideState = true;
            depthOfField.quality.levelAndOverride =
                ((int)ScalableSettingLevelParameter.Level.Low, true);
            depthOfField.resolution = DepthOfFieldResolution.Half;
            depthOfField.nearSampleCount = 3;
            depthOfField.farSampleCount = 4;
            depthOfField.nearMaxBlur = 1.5f;
            depthOfField.farMaxBlur = 3f;
            depthOfField.highQualityFiltering = true;
            depthOfField.physicallyBased = false;
            depthOfField.coCStabilization.Override(false);

            vignette = profile.Add<Vignette>(true);
            vignette.active = true;
            vignette.mode.Override(VignetteMode.Procedural);
            vignette.color.Override(new Color(0.012f, 0.015f, 0.018f));
            vignette.center.Override(new Vector2(0.5f, 0.5f));
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.58f);
            vignette.roundness.Override(0.9f);
            vignette.rounded.Override(true);

            blinkColorAdjustments = profile.Add<ColorAdjustments>(true);
            blinkColorAdjustments.active = false;
            blinkColorAdjustments.postExposure.Override(0f);
            blinkColorAdjustments.contrast.Override(0f);
            blinkColorAdjustments.colorFilter.Override(Color.white);
            blinkColorAdjustments.hueShift.Override(0f);
            blinkColorAdjustments.saturation.Override(0f);

            volume = volumeRoot.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = volumePriority;
            volume.weight = 0f;
            volume.sharedProfile = profile;
            volume.enabled = false;
        }

        private void OnDisable()
        {
            currentWeight = 0f;
            targetWeight = 0f;
            ResetBlink();
            if (volume != null)
            {
                volume.weight = 0f;
                volume.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (needs != null)
            {
                needs.StateChanged -= HandleNeedsChanged;
            }

            if (volume != null)
            {
                volume.sharedProfile = null;
            }

            if (profile != null)
            {
                Destroy(profile);
                profile = null;
            }

            vignette = null;
            blinkColorAdjustments = null;

            if (volumeRoot != null)
            {
                Destroy(volumeRoot);
                volumeRoot = null;
            }
        }
    }
}
