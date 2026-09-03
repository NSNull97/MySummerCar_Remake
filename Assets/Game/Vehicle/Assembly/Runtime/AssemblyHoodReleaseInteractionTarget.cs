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
            dashboardPart != null && dashboardPart.IsInstalled &&
            hood != null && hood.IsAttachedToHinge &&
            hood.RequiresReleaseBeforeOpening &&
            hood.IsClosedLatched && animationRoutine == null;

        public void Interact(in InteractionContext context)
        {
            if (!CanInteract(context) || !hood.ReleaseForOpening())
            {
                return;
            }

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
    }
}
