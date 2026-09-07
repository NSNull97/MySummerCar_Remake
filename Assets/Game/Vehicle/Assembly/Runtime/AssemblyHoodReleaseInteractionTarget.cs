using System.Collections;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Project-owned equivalent of the donor HoodLocking lever. The dashboard
    /// control only releases the closed hood latch; opening and closing remain
    /// mouse-held operations on the hood itself.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssemblyHoodReleaseInteractionTarget : MonoBehaviour,
        IContextInteractionTarget,
        IPrimaryInteractionOnlyTarget
    {
        [SerializeField] private PartInstance dashboardPart;
        [SerializeField] private AssemblyHingedPartInteractionTarget hood;
        [SerializeField] private Transform leverVisual;
        [SerializeField] private bool useReviewedHandlePull;
        [SerializeField, Min(0.02f)] private float pullDurationSeconds = 0.12f;
        [SerializeField, Min(0.02f)] private float returnDurationSeconds = 0.18f;
        [SerializeField] private Vector3 pulledLocalOffset =
            new Vector3(0f, 0f, -0.018f);
        [SerializeField] private Vector3 pulledLocalEuler =
            new Vector3(-14f, 0f, 0f);

        private Vector3 restLocalPosition;
        private Quaternion restLocalRotation;
        private Coroutine animationRoutine;

        public string InteractionPrompt => "Потянуть рычаг капота";

        public PartInstance DashboardPart => dashboardPart;

        public AssemblyHingedPartInteractionTarget Hood => hood;
        public Transform LeverVisual => leverVisual;
        public bool UsesReviewedHandlePull => useReviewedHandlePull;

        public void ConfigureReviewedHandlePull(Transform handle)
        {
            leverVisual = handle != null ? handle : throw new System.ArgumentNullException(nameof(handle));
            useReviewedHandlePull = true;
            CaptureRestPose();
        }

        public void Configure(
            PartInstance authoredDashboardPart,
            AssemblyHingedPartInteractionTarget authoredHood,
            Transform authoredLeverVisual)
        {
            dashboardPart = authoredDashboardPart;
            hood = authoredHood;
            leverVisual = authoredLeverVisual != null
                ? authoredLeverVisual
                : transform;
            CaptureRestPose();
        }

        public bool CanInteract(in InteractionContext context) =>
            enabled && gameObject.activeInHierarchy &&
            dashboardPart != null && animationRoutine == null &&
            (useReviewedHandlePull || dashboardPart.IsInstalled &&
             hood != null && hood.IsAttachedToHinge &&
             hood.RequiresReleaseBeforeOpening && hood.IsClosedLatched);

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context))
            {
                return;
            }

            // The original handle animates even without a fitted/latched hood;
            // only an installed dashboard can send the latch release.
            if (dashboardPart.IsInstalled && hood != null)
                hood.ReleaseForOpening();

            animationRoutine = StartCoroutine(AnimateLever());
        }

        private void Awake()
        {
            if (leverVisual == null)
            {
                leverVisual = transform;
            }

            CaptureRestPose();
        }

        private void CaptureRestPose()
        {
            if (leverVisual == null)
            {
                return;
            }

            restLocalPosition = leverVisual.localPosition;
            restLocalRotation = leverVisual.localRotation;
        }

        private IEnumerator AnimateLever()
        {
            if (useReviewedHandlePull)
            {
                float time = 0f;
                while (time < 0.25f)
                {
                    time = Mathf.Min(0.25f, time + Time.deltaTime);
                    ApplyPose(restLocalPosition + restLocalRotation * Vector3.up *
                        EvaluateReviewedPullOffset(time), restLocalRotation);
                    yield return null;
                }
                ApplyPose(restLocalPosition, restLocalRotation);
                animationRoutine = null;
                yield break;
            }
            Vector3 pulledPosition = restLocalPosition +
                restLocalRotation * pulledLocalOffset;
            Quaternion pulledRotation = restLocalRotation *
                Quaternion.Euler(pulledLocalEuler);
            float elapsed = 0f;
            while (elapsed < pullDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / pullDurationSeconds);
                ApplyPose(
                    Vector3.LerpUnclamped(
                        restLocalPosition,
                        pulledPosition,
                        SmoothStep(t)),
                    Quaternion.SlerpUnclamped(
                        restLocalRotation,
                        pulledRotation,
                        SmoothStep(t)));
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < returnDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnDurationSeconds);
                ApplyPose(
                    Vector3.LerpUnclamped(
                        pulledPosition,
                        restLocalPosition,
                        SmoothStep(t)),
                    Quaternion.SlerpUnclamped(
                        pulledRotation,
                        restLocalRotation,
                        SmoothStep(t)));
                yield return null;
            }

            ApplyPose(restLocalPosition, restLocalRotation);
            animationRoutine = null;
        }

        private void ApplyPose(Vector3 position, Quaternion rotation)
        {
            if (leverVisual != null)
            {
                leverVisual.SetLocalPositionAndRotation(position, rotation);
            }
        }

        private void OnDisable()
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            ApplyPose(restLocalPosition, restLocalRotation);
        }

        private static float SmoothStep(float value) =>
            value * value * (3f - 2f * value);

        // ConfigurationTransferred: hood_lock_handle clip, three position-Y
        // keys at0,1/6,1/4sec. Relative to its -0.2mm rest; no rotation curve.
        public static float EvaluateReviewedPullOffset(float time)
        {
            if (time <= 0f || time >= 0.25f) return 0f;
            const float middle = 0.16666667f;
            float start = time <= middle ? 0f : middle;
            float duration = time <= middle ? middle : 0.25f - middle;
            float from = time <= middle ? 0f : -0.0086f;
            float to = time <= middle ? -0.0086f : 0f;
            float fromSlope = time <= middle ? -0.0516f : 0.025800005f;
            float toSlope = time <= middle ? 0.025800005f : 0.10320001f;
            float t = (time - start) / duration;
            float t2 = t * t, t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * from +
                (t3 - 2f * t2 + t) * duration * fromSlope +
                (-2f * t3 + 3f * t2) * to + (t3 - t2) * duration * toSlope;
        }
    }
}
