using System;
using UnityEngine;

namespace MSC.Needs
{
    /// <summary>
    /// Lightweight first-person feedback for fatigue, intoxication and
    /// hangover. This presenter deliberately avoids a live screen-copy blur:
    /// its overlay is a single procedural texture and its camera motion is
    /// presentation-only.
    /// </summary>
    [DefaultExecutionOrder(1500)]
    [DisallowMultipleComponent]
    public sealed class PlayerNeedsPerceptionPresenter : MonoBehaviour
    {
        private const int VignetteTextureSize = 128;
        private const float RotationOwnershipToleranceDegrees = 0.02f;

        private Camera playerCamera;
        private PlayerNeedsRuntime needs;
        private PlayerNeedsSnapshot snapshot;
        private Texture2D vignetteTexture;
        private Quaternion lastBaseRotation;
        private Quaternion lastComposedRotation;
        private bool ownsCameraRotation;
        private bool initialized;

        public void Initialize(
            Camera configuredCamera,
            PlayerNeedsRuntime configuredNeeds)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "Player-needs perception presenter is already initialized.");
            }

            playerCamera = configuredCamera ??
                throw new ArgumentNullException(nameof(configuredCamera));
            needs = configuredNeeds ??
                throw new ArgumentNullException(nameof(configuredNeeds));
            snapshot = needs.Snapshot;
            vignetteTexture = CreateVignetteTexture();
            needs.StateChanged += HandleNeedsChanged;
            initialized = true;
        }

        private void HandleNeedsChanged(PlayerNeedsSnapshot changedSnapshot)
        {
            snapshot = changedSnapshot;
        }

        private void LateUpdate()
        {
            if (!initialized || playerCamera == null)
            {
                return;
            }

            Transform cameraTransform = playerCamera.transform;
            Quaternion observedRotation = cameraTransform.localRotation;
            Quaternion baseRotation =
                ownsCameraRotation &&
                Quaternion.Angle(
                    observedRotation,
                    lastComposedRotation) <=
                RotationOwnershipToleranceDegrees
                    ? lastBaseRotation
                    : observedRotation;

            float intoxicationSeverity = EvaluateSeverity(
                snapshot.Intoxication,
                0.25f,
                16f);
            float time = Time.unscaledTime;
            float slowSway =
                Mathf.Sin(time * 0.72f) +
                Mathf.Sin(time * 1.31f + 1.2f) * 0.42f;
            float rollAmplitudeDegrees =
                Mathf.Lerp(0.8f, 4.5f, intoxicationSeverity) *
                intoxicationSeverity;
            float rollDegrees = slowSway * rollAmplitudeDegrees;

            lastBaseRotation = baseRotation;
            lastComposedRotation =
                baseRotation * Quaternion.Euler(0f, 0f, rollDegrees);
            cameraTransform.localRotation = lastComposedRotation;
            ownsCameraRotation = true;
        }

        private void OnGUI()
        {
            if (!initialized ||
                vignetteTexture == null ||
                Event.current.type != EventType.Repaint)
            {
                return;
            }

            float fatigueSeverity = EvaluateSeverity(
                snapshot.Fatigue,
                52f,
                100f);
            float intoxicationSeverity = EvaluateSeverity(
                snapshot.Intoxication,
                0.25f,
                16f);
            float hangoverSeverity =
                snapshot.Intoxication <= 0.1f &&
                snapshot.PendingIntoxicationEffect <= 0.1f
                    ? EvaluateSeverity(
                        snapshot.Hangover,
                        0.25f,
                        30f)
                    : 0f;

            Rect screenRect = new Rect(
                0f,
                0f,
                Screen.width,
                Screen.height);
            Color previousColor = GUI.color;

            if (hangoverSeverity > 0.001f)
            {
                GUI.color = new Color(
                    0.22f,
                    0.16f,
                    0.08f,
                    hangoverSeverity * 0.085f);
                GUI.DrawTexture(screenRect, Texture2D.whiteTexture);
            }

            float vignetteOpacity = Mathf.Clamp01(
                fatigueSeverity * 0.10f +
                hangoverSeverity * 0.34f +
                intoxicationSeverity * 0.12f);
            if (vignetteOpacity > 0.001f)
            {
                GUI.color = new Color(
                    0.025f,
                    0.02f + hangoverSeverity * 0.018f,
                    0.018f,
                    vignetteOpacity);
                GUI.DrawTexture(
                    screenRect,
                    vignetteTexture,
                    ScaleMode.StretchToFill,
                    true);
            }

            GUI.color = previousColor;
        }

        private void OnDisable()
        {
            ReleaseCameraRotation();
        }

        private void OnDestroy()
        {
            if (needs != null)
            {
                needs.StateChanged -= HandleNeedsChanged;
            }

            ReleaseCameraRotation();
            if (vignetteTexture != null)
            {
                Destroy(vignetteTexture);
                vignetteTexture = null;
            }

        }

        private void ReleaseCameraRotation()
        {
            if (!ownsCameraRotation || playerCamera == null)
            {
                ownsCameraRotation = false;
                return;
            }

            Transform cameraTransform = playerCamera.transform;
            if (Quaternion.Angle(
                    cameraTransform.localRotation,
                    lastComposedRotation) <=
                RotationOwnershipToleranceDegrees)
            {
                cameraTransform.localRotation = lastBaseRotation;
            }

            ownsCameraRotation = false;
        }

        private static float EvaluateSeverity(
            float value,
            float onset,
            float fullStrength)
        {
            float normalized = Mathf.InverseLerp(
                onset,
                fullStrength,
                value);
            return normalized * normalized * (3f - 2f * normalized);
        }

        private static Texture2D CreateVignetteTexture()
        {
            var texture = new Texture2D(
                VignetteTextureSize,
                VignetteTextureSize,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = "MSC Needs Perception Vignette",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[
                VignetteTextureSize * VignetteTextureSize];
            float denominator = VignetteTextureSize - 1f;
            int index = 0;
            for (int y = 0; y < VignetteTextureSize; y++)
            {
                float normalizedY = y / denominator * 2f - 1f;
                for (int x = 0; x < VignetteTextureSize; x++)
                {
                    float normalizedX = x / denominator * 2f - 1f;
                    float radius = Mathf.Sqrt(
                        normalizedX * normalizedX +
                        normalizedY * normalizedY);
                    float edge = Mathf.InverseLerp(
                        0.38f,
                        1.18f,
                        radius);
                    edge = edge * edge * (3f - 2f * edge);
                    pixels[index++] = new Color32(
                        255,
                        255,
                        255,
                        (byte)Mathf.RoundToInt(edge * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

    }
}
