using System;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Adapts the donor loose-part presentation to the donor's installed rear
    /// suspension hierarchy. The loose spring is a static renderer, while the
    /// installed donor spring is a two-bone skinned mesh. The loose shock is a
    /// rigid two-piece object, while the installed donor drives each half from
    /// a separate chassis/arm pivot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SatsumaRearSuspensionPartPresentation : MonoBehaviour,
        IAssemblyInstallTransitionPresentation
    {
        private enum PresentationKind
        {
            None = 0,
            Spring = 1,
            Shock = 2,
        }

        [SerializeField] private PresentationKind kind;
        [SerializeField] private MeshRenderer springLooseRenderer;
        [SerializeField] private Transform shockTopMesh;
        [SerializeField] private Transform shockBottomMesh;
        [SerializeField] private Vector3 shockTopLoosePosition;
        [SerializeField] private Quaternion shockTopLooseRotation =
            Quaternion.identity;
        [SerializeField] private Vector3 shockTopLooseScale = Vector3.one;
        [SerializeField] private Vector3 shockBottomLoosePosition;
        [SerializeField] private Quaternion shockBottomLooseRotation =
            Quaternion.identity;
        [SerializeField] private Vector3 shockBottomLooseScale = Vector3.one;
        [SerializeField]
        private SatsumaRearSuspensionController suspensionController;

        private MeshRenderer installedSpringRenderer;
        private Transform installedSpringObject;
        private Vector3 installedSpringBaseLocalScale = Vector3.one;
        private Vector3 springNegativeEndpointLocal;
        private Vector3 springPositiveEndpointLocal;
        private Vector3 springTopEndpointLocal;
        private Vector3 springBottomEndpointLocal;
        private int springAxisIndex;
        private bool springEndpointsOriented;
        private bool installedPresentationActive;
        private Transform transitionTopTarget;
        private Transform transitionBottomTarget;
        private Vector3 transitionSourceTop;
        private Vector3 transitionSourceBottom;
        private bool transitionPreviewActive;

        public bool IsSpring => kind == PresentationKind.Spring;

        public bool IsShock => kind == PresentationKind.Shock;

        public SatsumaRearSuspensionController SuspensionController =>
            suspensionController;

        public bool IsInstallTransitionPreviewActive =>
            transitionPreviewActive;

        public Renderer ActiveSpringRenderer => installedSpringRenderer != null &&
            installedSpringRenderer.enabled
                ? installedSpringRenderer
                : springLooseRenderer;

        public void ConfigureSpring(MeshRenderer looseRenderer)
        {
            kind = PresentationKind.Spring;
            springLooseRenderer = looseRenderer;
            shockTopMesh = null;
            shockBottomMesh = null;
        }

        public void BindSuspensionController(
            SatsumaRearSuspensionController configuredController)
        {
            suspensionController = configuredController;
        }

        public void ConfigureShock(
            Transform topMesh,
            Transform bottomMesh)
        {
            if (topMesh == null)
            {
                throw new ArgumentNullException(nameof(topMesh));
            }

            if (bottomMesh == null)
            {
                throw new ArgumentNullException(nameof(bottomMesh));
            }

            kind = PresentationKind.Shock;
            springLooseRenderer = null;
            shockTopMesh = topMesh;
            shockBottomMesh = bottomMesh;
            shockTopLoosePosition = topMesh.localPosition;
            shockTopLooseRotation = topMesh.localRotation;
            shockTopLooseScale = topMesh.localScale;
            shockBottomLoosePosition = bottomMesh.localPosition;
            shockBottomLooseRotation = bottomMesh.localRotation;
            shockBottomLooseScale = bottomMesh.localScale;
        }

        public void PresentInstalledSpring(
            Transform topBone,
            Transform bottomBone)
        {
            if (!IsSpring || springLooseRenderer == null ||
                topBone == null || bottomBone == null)
            {
                return;
            }

            EnsureInstalledSpringRenderer();
            FitInstalledSpringBetween(topBone.position, bottomBone.position);
            installedSpringRenderer.enabled = true;
            springLooseRenderer.enabled = false;
            installedPresentationActive = true;
            ConfigureOutline(installedSpringRenderer);
        }

        public void PresentInstalledShock(
            Transform topTarget,
            Transform bottomTarget)
        {
            if (!IsShock || shockTopMesh == null || shockBottomMesh == null ||
                topTarget == null || bottomTarget == null)
            {
                return;
            }

            shockTopMesh.SetPositionAndRotation(
                topTarget.position,
                topTarget.rotation);
            shockBottomMesh.SetPositionAndRotation(
                bottomTarget.position,
                bottomTarget.rotation);
            installedPresentationActive = true;
        }

        public void BeginInstallTransition(MountPointAuthoring mount)
        {
            transitionPreviewActive = false;
            transitionTopTarget = null;
            transitionBottomTarget = null;
            if (!IsSpring || springLooseRenderer == null || mount == null ||
                suspensionController == null ||
                !suspensionController.TryResolveSpringTransitionTargets(
                    mount.MountId,
                    out transitionTopTarget,
                    out transitionBottomTarget))
            {
                return;
            }

            EnsureInstalledSpringRenderer();
            if (!springEndpointsOriented)
            {
                springTopEndpointLocal = springNegativeEndpointLocal;
                springBottomEndpointLocal = springPositiveEndpointLocal;
                springEndpointsOriented = true;
            }

            transitionSourceTop = springLooseRenderer.transform.TransformPoint(
                springTopEndpointLocal);
            transitionSourceBottom = springLooseRenderer.transform.TransformPoint(
                springBottomEndpointLocal);
            transitionPreviewActive = true;
            installedPresentationActive = true;
            installedSpringRenderer.enabled = true;
            springLooseRenderer.enabled = false;
            ApplyInstallTransition(0f);
        }

        public void ApplyInstallTransition(float normalizedProgress)
        {
            if (!transitionPreviewActive || transitionTopTarget == null ||
                transitionBottomTarget == null)
            {
                return;
            }

            Vector3 targetTop = transitionTopTarget.position;
            Vector3 topToBottom = transitionBottomTarget.position - targetTop;
            float distance = topToBottom.magnitude;
            Vector3 direction = distance > 0.0001f
                ? topToBottom / distance
                : -transitionTopTarget.up;
            Vector3 compressedBottom = targetTop + direction *
                Mathf.Min(distance, 0.062f);
            float progress = Mathf.Clamp01(normalizedProgress);
            FitInstalledSpringBetween(
                Vector3.LerpUnclamped(
                    transitionSourceTop,
                    targetTop,
                    progress),
                Vector3.LerpUnclamped(
                    transitionSourceBottom,
                    compressedBottom,
                    progress));
            installedSpringRenderer.enabled = true;
            springLooseRenderer.enabled = false;
        }

        public void CompleteInstallTransition(bool installed)
        {
            if (!transitionPreviewActive)
            {
                return;
            }

            transitionPreviewActive = false;
            transitionTopTarget = null;
            transitionBottomTarget = null;
            if (installed)
            {
                installedPresentationActive = true;
                installedSpringRenderer.enabled = true;
                springLooseRenderer.enabled = false;
                return;
            }

            RestoreLoosePresentation();
        }

        public void RestoreLoosePresentation()
        {
            if (!installedPresentationActive && !transitionPreviewActive)
            {
                return;
            }

            transitionPreviewActive = false;
            transitionTopTarget = null;
            transitionBottomTarget = null;

            if (IsSpring)
            {
                if (installedSpringRenderer != null)
                {
                    installedSpringRenderer.enabled = false;
                }

                if (springLooseRenderer != null)
                {
                    springLooseRenderer.enabled = true;
                    ConfigureOutline(springLooseRenderer);
                }
            }
            else if (IsShock)
            {
                RestoreLocalPose(
                    shockTopMesh,
                    shockTopLoosePosition,
                    shockTopLooseRotation,
                    shockTopLooseScale);
                RestoreLocalPose(
                    shockBottomMesh,
                    shockBottomLoosePosition,
                    shockBottomLooseRotation,
                    shockBottomLooseScale);
            }

            installedPresentationActive = false;
        }

        private void EnsureInstalledSpringRenderer()
        {
            if (installedSpringRenderer != null)
            {
                return;
            }

            MeshFilter sourceFilter = springLooseRenderer.GetComponent<
                MeshFilter>();
            if (sourceFilter == null || sourceFilter.sharedMesh == null)
            {
                throw new InvalidOperationException(
                    "The rear spring presentation has no donor mesh.");
            }

            var owner = new GameObject("Installed two-bone spring");
            installedSpringObject = owner.transform;
            installedSpringObject.SetParent(
                springLooseRenderer.transform.parent,
                false);
            installedSpringObject.SetLocalPositionAndRotation(
                springLooseRenderer.transform.localPosition,
                springLooseRenderer.transform.localRotation);
            installedSpringObject.localScale =
                springLooseRenderer.transform.localScale;
            MeshFilter installedFilter = owner.AddComponent<MeshFilter>();
            installedFilter.sharedMesh = sourceFilter.sharedMesh;
            installedSpringRenderer = owner.AddComponent<MeshRenderer>();
            installedSpringRenderer.sharedMaterials =
                springLooseRenderer.sharedMaterials;
            installedSpringRenderer.shadowCastingMode =
                springLooseRenderer.shadowCastingMode;
            installedSpringRenderer.receiveShadows =
                springLooseRenderer.receiveShadows;
            installedSpringRenderer.lightProbeUsage =
                springLooseRenderer.lightProbeUsage;
            installedSpringRenderer.reflectionProbeUsage =
                springLooseRenderer.reflectionProbeUsage;
            installedSpringBaseLocalScale = installedSpringObject.localScale;
            ConfigureSpringEndpoints(sourceFilter.sharedMesh.bounds);
            installedSpringRenderer.enabled = false;
        }

        private void ConfigureSpringEndpoints(Bounds meshBounds)
        {
            Vector3 size = meshBounds.size;
            springAxisIndex = size.y > size.x ? 1 : 0;
            if (size.z > GetAxis(size, springAxisIndex))
            {
                springAxisIndex = 2;
            }

            Vector3 axis = AxisVector(springAxisIndex);
            float halfLength = GetAxis(size, springAxisIndex) * 0.5f;
            springNegativeEndpointLocal = meshBounds.center -
                axis * halfLength;
            springPositiveEndpointLocal = meshBounds.center +
                axis * halfLength;
            springEndpointsOriented = false;
        }

        private void FitInstalledSpringBetween(
            Vector3 top,
            Vector3 bottom)
        {
            Vector3 topToBottom = bottom - top;
            float desiredLength = topToBottom.magnitude;
            if (desiredLength <= 0.0001f)
            {
                return;
            }

            if (!springEndpointsOriented)
            {
                Vector3 negativeWorld = springLooseRenderer.transform
                    .TransformPoint(springNegativeEndpointLocal);
                Vector3 positiveWorld = springLooseRenderer.transform
                    .TransformPoint(springPositiveEndpointLocal);
                bool negativeIsTop =
                    (negativeWorld - top).sqrMagnitude <=
                    (positiveWorld - top).sqrMagnitude;
                springTopEndpointLocal = negativeIsTop
                    ? springNegativeEndpointLocal
                    : springPositiveEndpointLocal;
                springBottomEndpointLocal = negativeIsTop
                    ? springPositiveEndpointLocal
                    : springNegativeEndpointLocal;
                springEndpointsOriented = true;
            }

            Vector3 sourceTop = springLooseRenderer.transform.TransformPoint(
                springTopEndpointLocal);
            Vector3 sourceBottom = springLooseRenderer.transform.TransformPoint(
                springBottomEndpointLocal);
            float sourceLength = Vector3.Distance(sourceTop, sourceBottom);
            if (sourceLength <= 0.0001f)
            {
                return;
            }

            Vector3 orientedLocalAxis =
                (springBottomEndpointLocal - springTopEndpointLocal).normalized;
            Vector3 scaledLocalAxis = Vector3.Scale(
                orientedLocalAxis,
                installedSpringBaseLocalScale).normalized;
            Quaternion sourceRotation = springLooseRenderer.transform.rotation;
            Vector3 sourceAxisWorld = sourceRotation * scaledLocalAxis;
            Quaternion targetRotation = Quaternion.FromToRotation(
                sourceAxisWorld,
                topToBottom / desiredLength) * sourceRotation;
            Vector3 stretchedScale = installedSpringBaseLocalScale;
            SetAxis(
                ref stretchedScale,
                springAxisIndex,
                GetAxis(stretchedScale, springAxisIndex) *
                desiredLength / sourceLength);

            installedSpringObject.SetPositionAndRotation(
                springLooseRenderer.transform.position,
                targetRotation);
            installedSpringObject.localScale = stretchedScale;
            installedSpringObject.position += top -
                installedSpringObject.TransformPoint(
                    springTopEndpointLocal);
        }

        private static Vector3 AxisVector(int axisIndex)
        {
            switch (axisIndex)
            {
                case 1:
                    return Vector3.up;
                case 2:
                    return Vector3.forward;
                default:
                    return Vector3.right;
            }
        }

        private static float GetAxis(Vector3 value, int axisIndex)
        {
            switch (axisIndex)
            {
                case 1:
                    return value.y;
                case 2:
                    return value.z;
                default:
                    return value.x;
            }
        }

        private static void SetAxis(
            ref Vector3 value,
            int axisIndex,
            float component)
        {
            switch (axisIndex)
            {
                case 1:
                    value.y = component;
                    break;
                case 2:
                    value.z = component;
                    break;
                default:
                    value.x = component;
                    break;
            }
        }

        private void ConfigureOutline(Renderer renderer)
        {
            InteractionTargetHost host = GetComponent<InteractionTargetHost>();
            if (host != null && renderer != null)
            {
                host.ConfigureOutlineRenderers(renderer);
            }
        }

        private static void RestoreLocalPose(
            Transform target,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            if (target == null)
            {
                return;
            }

            target.SetLocalPositionAndRotation(position, rotation);
            target.localScale = scale;
        }

        private void OnDisable()
        {
            RestoreLoosePresentation();
        }
    }
}
