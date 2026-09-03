using MSC.Core.Lifecycle;
using UnityEngine;

namespace MSC.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FirstPersonMotor))]
    public sealed class PlayerLeanImpactFeedbackPresenter :
        MonoBehaviour,
        IUiVisibilityGate
    {
        [SerializeField]
        private FirstPersonMotor motor;

        [Header("Eyelid impact response")]
        [SerializeField, Min(0.001f)]
        private float closingSeconds = 0.055f;

        [SerializeField, Min(0f)]
        private float closedHoldSeconds = 0.035f;

        [SerializeField, Min(0.001f)]
        private float openingSeconds = 0.32f;

        [SerializeField, Range(0f, 1f)]
        private float maximumClosure01 = 1f;

        [SerializeField, Range(0f, 1f)]
        private float blackoutAlpha = 0.2f;

        [SerializeField, Min(0f)]
        private float softEdgePixels = 12f;

        private bool subscribed;
        private bool uiSuppressed;
        private float effectTimeSeconds = float.PositiveInfinity;
        private float currentClosure01;

        public bool IsEffectActive =>
            !float.IsPositiveInfinity(effectTimeSeconds);
        public bool IsUiSuppressed => uiSuppressed;
        public float CurrentClosure01 => currentClosure01;
        public int PresentedImpactCount { get; private set; }

        public void Configure(FirstPersonMotor authoredMotor)
        {
            Unsubscribe();
            motor = authoredMotor;
            if (isActiveAndEnabled)
            {
                Subscribe();
            }
        }

        public void SetUiSuppressed(bool suppressed)
        {
            uiSuppressed = suppressed;
        }

        private void Reset()
        {
            motor = GetComponent<FirstPersonMotor>();
        }

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<FirstPersonMotor>();
            }
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
            effectTimeSeconds = float.PositiveInfinity;
            currentClosure01 = 0f;
        }

        private void Update()
        {
            if (!IsEffectActive)
            {
                return;
            }

            effectTimeSeconds += Time.unscaledDeltaTime;
            currentClosure01 = EvaluateClosure(effectTimeSeconds);
            if (effectTimeSeconds >= TotalDurationSeconds)
            {
                effectTimeSeconds = float.PositiveInfinity;
                currentClosure01 = 0f;
            }
        }

        private void OnGUI()
        {
            if (uiSuppressed ||
                currentClosure01 <= 0.001f ||
                Event.current.type != EventType.Repaint)
            {
                return;
            }

            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = -2000;

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float closure = Mathf.Clamp01(currentClosure01);
            GUI.color = new Color(
                0f,
                0f,
                0f,
                blackoutAlpha * closure);
            GUI.DrawTexture(
                new Rect(0f, 0f, screenWidth, screenHeight),
                Texture2D.whiteTexture);

            float lidHeight = screenHeight * 0.5f * closure;
            GUI.color = new Color(0f, 0f, 0f, 0.985f);
            GUI.DrawTexture(
                new Rect(0f, 0f, screenWidth, lidHeight),
                Texture2D.whiteTexture);
            GUI.DrawTexture(
                new Rect(
                    0f,
                    screenHeight - lidHeight,
                    screenWidth,
                    lidHeight),
                Texture2D.whiteTexture);

            DrawSoftEdges(screenWidth, screenHeight, lidHeight, closure);
            GUI.depth = previousDepth;
            GUI.color = previousColor;
        }

        private void DrawSoftEdges(
            float screenWidth,
            float screenHeight,
            float lidHeight,
            float closure)
        {
            const int EdgeSteps = 4;
            float edgeHeight = Mathf.Min(
                softEdgePixels,
                Mathf.Max(0f, screenHeight * 0.5f - lidHeight));
            if (edgeHeight <= 0.01f)
            {
                return;
            }

            float stripHeight = edgeHeight / EdgeSteps;
            for (int index = 0; index < EdgeSteps; index++)
            {
                float alpha =
                    (1f - (index + 1f) / (EdgeSteps + 1f)) *
                    0.55f *
                    closure;
                GUI.color = new Color(0f, 0f, 0f, alpha);
                GUI.DrawTexture(
                    new Rect(
                        0f,
                        lidHeight + index * stripHeight,
                        screenWidth,
                        stripHeight + 0.5f),
                    Texture2D.whiteTexture);
                GUI.DrawTexture(
                    new Rect(
                        0f,
                        screenHeight - lidHeight -
                        (index + 1f) * stripHeight,
                        screenWidth,
                        stripHeight + 0.5f),
                    Texture2D.whiteTexture);
            }
        }

        private void Subscribe()
        {
            if (subscribed || motor == null)
            {
                return;
            }

            motor.ForwardLeanImpactOccurred += HandleLeanImpact;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || motor == null)
            {
                subscribed = false;
                return;
            }

            motor.ForwardLeanImpactOccurred -= HandleLeanImpact;
            subscribed = false;
        }

        private void HandleLeanImpact(PlayerLeanImpact impact)
        {
            effectTimeSeconds = 0f;
            currentClosure01 = EvaluateClosure(0f);
            PresentedImpactCount++;
        }

        private float EvaluateClosure(float elapsedSeconds)
        {
            float closeEnd = closingSeconds;
            if (elapsedSeconds < closeEnd)
            {
                return maximumClosure01 * Mathf.SmoothStep(
                    0f,
                    1f,
                    elapsedSeconds / closeEnd);
            }

            float holdEnd = closeEnd + closedHoldSeconds;
            if (elapsedSeconds < holdEnd)
            {
                return maximumClosure01;
            }

            float openProgress = Mathf.Clamp01(
                (elapsedSeconds - holdEnd) / openingSeconds);
            return maximumClosure01 *
                (1f - Mathf.SmoothStep(0f, 1f, openProgress));
        }

        private float TotalDurationSeconds =>
            closingSeconds + closedHoldSeconds + openingSeconds;
    }
}
