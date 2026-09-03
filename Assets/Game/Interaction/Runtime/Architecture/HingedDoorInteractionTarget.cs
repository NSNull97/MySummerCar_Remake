using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Interaction.Architecture
{
    /// <summary>
    /// Project-owned interaction authority for a transform-driven hinged door.
    /// The handle owns the InteractionTargetHost; the moving leaf owns only
    /// physical collision, so clicking the panel cannot activate the door.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HingedDoorInteractionTarget : MonoBehaviour,
        IContextInteractionTarget,
        IContinuousContextInteractionTarget,
        IPrimaryInteractionOnlyTarget
    {
        [SerializeField] private Transform hingePivot;
        [SerializeField] private Quaternion closedLocalRotation =
            Quaternion.identity;
        [SerializeField] private Vector3 hingeAxisLocal = Vector3.up;
        [SerializeField, Range(1f, 170f)] private float swingAngleDegrees = 85f;
        [SerializeField, Min(1f)] private float angularSpeedDegrees = 200f;
        [SerializeField] private string openPrompt = "Открыть дверь";
        [SerializeField] private string closePrompt = "Закрыть дверь";
        [SerializeField, Range(0f, 1f)] private float openNormalized;
        [SerializeField] private bool targetOpen;
        [SerializeField] private float openingSign = 1f;
        [SerializeField] private bool requiresDirectionalHold;
        [SerializeField, Min(1f)] private float directionalAccelerationDegrees =
            1100f;
        [SerializeField, Min(1f)] private float releaseDecelerationDegrees =
            1300f;

        private bool continuousInteractionActive;
        private float currentAngularVelocityDegrees;

        public string InteractionPrompt => requiresDirectionalHold
            ? openPrompt
            : targetOpen
                ? closePrompt
                : openPrompt;

        public float OpenNormalized => openNormalized;

        public bool TargetOpen => targetOpen;

        public float OpeningSign => openingSign;

        public float EstimatedTravelSeconds =>
            swingAngleDegrees / angularSpeedDegrees;

        public bool UsesDirectionalHold => requiresDirectionalHold;

        private void Awake()
        {
            if (hingePivot == null)
            {
                hingePivot = transform;
                closedLocalRotation = hingePivot.localRotation;
            }

            NormalizeConfiguration();
            ApplyRotation();
        }

        private void Update()
        {
            if (requiresDirectionalHold)
            {
                if (!continuousInteractionActive)
                {
                    AdvanceReleasedMotion(Time.deltaTime);
                }
            }
            else
            {
                Advance(Time.deltaTime);
            }
        }

        public bool CanInteract(in InteractionContext context) =>
            !requiresDirectionalHold &&
            enabled && gameObject.activeInHierarchy && hingePivot != null;

        public void Interact(in InteractionContext context)
        {
            SetOpen(!targetOpen, immediate: false);
        }

        public void Configure(
            Transform pivot,
            Quaternion authoredClosedLocalRotation,
            Vector3 authoredHingeAxisLocal,
            float authoredSwingAngleDegrees,
            float authoredAngularSpeedDegrees,
            string authoredOpenPrompt,
            string authoredClosePrompt,
            float authoredOpeningSign = 1f,
            bool authoredRequiresDirectionalHold = false,
            float authoredDirectionalAccelerationDegrees = 1100f,
            float authoredReleaseDecelerationDegrees = 1300f)
        {
            hingePivot = pivot != null ? pivot : transform;
            closedLocalRotation = authoredClosedLocalRotation;
            hingeAxisLocal = authoredHingeAxisLocal;
            swingAngleDegrees = authoredSwingAngleDegrees;
            angularSpeedDegrees = authoredAngularSpeedDegrees;
            openPrompt = string.IsNullOrWhiteSpace(authoredOpenPrompt)
                ? "Открыть дверь"
                : authoredOpenPrompt;
            closePrompt = string.IsNullOrWhiteSpace(authoredClosePrompt)
                ? "Закрыть дверь"
                : authoredClosePrompt;
            openNormalized = 0f;
            targetOpen = false;
            openingSign = authoredOpeningSign < 0f ? -1f : 1f;
            requiresDirectionalHold = authoredRequiresDirectionalHold;
            directionalAccelerationDegrees =
                authoredDirectionalAccelerationDegrees;
            releaseDecelerationDegrees =
                authoredReleaseDecelerationDegrees;
            continuousInteractionActive = false;
            currentAngularVelocityDegrees = 0f;
            NormalizeConfiguration();
            ApplyRotation();
        }

        public bool CanBeginContinuousInteraction(
            in InteractionContext context,
            ContinuousContextInteractionDirection direction) =>
            requiresDirectionalHold &&
            enabled && gameObject.activeInHierarchy && hingePivot != null &&
            (direction == ContinuousContextInteractionDirection.Primary
                ? openNormalized < 1f
                : openNormalized > 0f);

        public void BeginContinuousInteraction(
            in InteractionContext context,
            ContinuousContextInteractionDirection direction)
        {
            if (!CanBeginContinuousInteraction(context, direction))
            {
                continuousInteractionActive = false;
                return;
            }

            targetOpen =
                direction == ContinuousContextInteractionDirection.Primary;
            continuousInteractionActive = true;
        }

        public bool ContinueContinuousInteraction(float deltaTime)
        {
            if (!continuousInteractionActive ||
                !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return false;
            }

            float desiredVelocity = targetOpen
                ? angularSpeedDegrees
                : -angularSpeedDegrees;
            float previousVelocity = currentAngularVelocityDegrees;
            currentAngularVelocityDegrees = Mathf.MoveTowards(
                currentAngularVelocityDegrees,
                desiredVelocity,
                directionalAccelerationDegrees * deltaTime);
            AdvanceDirectionalMotion(
                previousVelocity,
                currentAngularVelocityDegrees,
                deltaTime);

            return continuousInteractionActive;
        }

        public void EndContinuousInteraction()
        {
            continuousInteractionActive = false;
        }

        public void AdvanceReleasedMotion(float deltaTime)
        {
            if (!requiresDirectionalHold || continuousInteractionActive ||
                !float.IsFinite(deltaTime) || deltaTime <= 0f ||
                Mathf.Approximately(currentAngularVelocityDegrees, 0f))
            {
                return;
            }

            float previousVelocity = currentAngularVelocityDegrees;
            currentAngularVelocityDegrees = Mathf.MoveTowards(
                currentAngularVelocityDegrees,
                0f,
                releaseDecelerationDegrees * deltaTime);
            AdvanceDirectionalMotion(
                previousVelocity,
                currentAngularVelocityDegrees,
                deltaTime);
        }

        public void Advance(float deltaTime)
        {
            if (hingePivot == null || !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            float target = targetOpen ? 1f : 0f;
            if (Mathf.Approximately(openNormalized, target))
            {
                return;
            }

            float normalizedSpeed = angularSpeedDegrees / swingAngleDegrees;
            openNormalized = Mathf.MoveTowards(
                openNormalized,
                target,
                normalizedSpeed * deltaTime);
            ApplyRotation();
        }

        public void SetOpen(bool value, bool immediate)
        {
            targetOpen = value;
            if (immediate)
            {
                continuousInteractionActive = false;
                currentAngularVelocityDegrees = 0f;
                openNormalized = value ? 1f : 0f;
                ApplyRotation();
            }
        }

        public void RestoreState(
            bool restoredTargetOpen,
            float restoredOpenNormalized)
        {
            targetOpen = restoredTargetOpen;
            openNormalized = Mathf.Clamp01(restoredOpenNormalized);
            continuousInteractionActive = false;
            currentAngularVelocityDegrees = 0f;
            ApplyRotation();
        }

        private void NormalizeConfiguration()
        {
            hingeAxisLocal = hingeAxisLocal.sqrMagnitude > 0.0001f
                ? hingeAxisLocal.normalized
                : Vector3.up;
            swingAngleDegrees = Mathf.Clamp(swingAngleDegrees, 1f, 170f);
            angularSpeedDegrees = Mathf.Max(1f, angularSpeedDegrees);
            directionalAccelerationDegrees = Mathf.Max(
                1f,
                directionalAccelerationDegrees);
            releaseDecelerationDegrees = Mathf.Max(
                1f,
                releaseDecelerationDegrees);
            openNormalized = Mathf.Clamp01(openNormalized);
            openingSign = openingSign < 0f ? -1f : 1f;
        }

        private void AdvanceDirectionalMotion(
            float previousVelocityDegrees,
            float currentVelocityDegrees,
            float deltaTime)
        {
            float averageVelocity =
                (previousVelocityDegrees + currentVelocityDegrees) * 0.5f;
            float nextOpen = openNormalized +
                averageVelocity / swingAngleDegrees * deltaTime;
            openNormalized = Mathf.Clamp01(nextOpen);

            bool reachedOpeningStop = openNormalized >= 1f &&
                currentAngularVelocityDegrees > 0f;
            bool reachedClosingStop = openNormalized <= 0f &&
                currentAngularVelocityDegrees < 0f;
            if (reachedOpeningStop || reachedClosingStop)
            {
                currentAngularVelocityDegrees = 0f;
                if ((reachedOpeningStop && targetOpen) ||
                    (reachedClosingStop && !targetOpen))
                {
                    continuousInteractionActive = false;
                }
            }

            ApplyRotation();
        }

        private void ApplyRotation()
        {
            if (hingePivot == null)
            {
                return;
            }

            Quaternion openRotation = closedLocalRotation *
                Quaternion.AngleAxis(
                    openingSign * swingAngleDegrees,
                    hingeAxisLocal);
            float easedOpen = openNormalized * openNormalized *
                (3f - 2f * openNormalized);
            hingePivot.localRotation = Quaternion.SlerpUnclamped(
                closedLocalRotation,
                openRotation,
                easedOpen);
        }
    }
}
