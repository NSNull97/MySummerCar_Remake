using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle.NWH
{
    /// <summary>
    /// Rebuilds the donor steering rod as a two-bone skin while installed.
    /// The inner bone remains at the rack-side mount and the outer bone follows
    /// NWH's non-rotating steering arm target, so steering and suspension travel
    /// deform the rubber joint without inheriting wheel spin.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SatsumaFrontSteeringRodPresentation : MonoBehaviour,
        IAssemblyInstallTransitionPresentation
    {
        [SerializeField] private PartInstance part;
        [SerializeField] private MeshRenderer looseRenderer;
        [SerializeField] private SkinnedMeshRenderer installedRenderer;
        [SerializeField] private Transform installedInnerBone;
        [SerializeField] private Transform installedOuterTarget;

        private Transform previewInnerBone;
        private Transform previewOuterBone;
        private MountPointAuthoring transitionMount;
        private Matrix4x4 innerBonePartLocalMatrix;
        private Vector3 innerStartPosition;
        private Quaternion innerStartRotation;
        private Vector3 outerStartPosition;
        private Quaternion outerStartRotation;
        private bool transitionPreviewActive;

        public PartInstance Part => part;
        public MeshRenderer LooseRenderer => looseRenderer;
        public SkinnedMeshRenderer InstalledRenderer => installedRenderer;
        public Transform InstalledInnerBone => installedInnerBone;
        public Transform InstalledOuterTarget => installedOuterTarget;
        public bool IsInstallTransitionPreviewActive =>
            transitionPreviewActive;
        public bool IsInstalledPresentationActive =>
            installedRenderer != null && installedRenderer.enabled;

        public void Configure(
            PartInstance configuredPart,
            MeshRenderer configuredLooseRenderer,
            SkinnedMeshRenderer configuredInstalledRenderer,
            Transform configuredInstalledInnerBone,
            Transform configuredInstalledOuterTarget)
        {
            part = configuredPart;
            looseRenderer = configuredLooseRenderer;
            installedRenderer = configuredInstalledRenderer;
            installedInnerBone = configuredInstalledInnerBone;
            installedOuterTarget = configuredInstalledOuterTarget;
            RefreshPresentation();
        }

        private void OnEnable()
        {
            RefreshPresentation();
        }

        private void Update()
        {
            RefreshPresentation();
        }

        public void RefreshPresentation()
        {
            if (transitionPreviewActive)
            {
                if (looseRenderer != null)
                {
                    looseRenderer.enabled = false;
                }

                if (installedRenderer != null)
                {
                    installedRenderer.enabled = true;
                }

                return;
            }

            bool installed = part != null && part.IsInstalled;
            if (looseRenderer != null)
            {
                looseRenderer.enabled = !installed;
            }

            if (installedRenderer != null)
            {
                installedRenderer.enabled = installed;
            }
        }

        public void BeginInstallTransition(MountPointAuthoring mount)
        {
            transitionPreviewActive = false;
            transitionMount = null;
            if (mount == null || part == null || looseRenderer == null ||
                installedRenderer == null || installedInnerBone == null ||
                installedOuterTarget == null ||
                installedRenderer.sharedMesh == null)
            {
                return;
            }

            Matrix4x4[] bindPoses = installedRenderer.sharedMesh.bindposes;
            if (bindPoses == null || bindPoses.Length < 2)
            {
                return;
            }

            EnsurePreviewBones();
            Matrix4x4 rendererWorld = installedRenderer.transform
                .localToWorldMatrix;
            SetWorldPose(previewInnerBone, rendererWorld * bindPoses[0].inverse);
            SetWorldPose(previewOuterBone, rendererWorld * bindPoses[1].inverse);
            innerStartPosition = previewInnerBone.position;
            innerStartRotation = previewInnerBone.rotation;
            outerStartPosition = previewOuterBone.position;
            outerStartRotation = previewOuterBone.rotation;
            innerBonePartLocalMatrix = part.transform.worldToLocalMatrix *
                installedInnerBone.localToWorldMatrix;
            transitionMount = mount;
            transitionPreviewActive = true;
            installedRenderer.bones = new[]
            {
                previewInnerBone,
                previewOuterBone,
            };
            installedRenderer.rootBone = previewInnerBone;
            ApplyInstallTransition(0f);
        }

        public void ApplyInstallTransition(float normalizedProgress)
        {
            if (!transitionPreviewActive || transitionMount == null ||
                installedOuterTarget == null)
            {
                return;
            }

            float progress = Mathf.Clamp01(normalizedProgress);
            Transform mountPose = transitionMount.Pose;
            Matrix4x4 mountWorld = Matrix4x4.TRS(
                mountPose.position,
                mountPose.rotation,
                Vector3.one);
            Matrix4x4 innerTargetMatrix = mountWorld *
                innerBonePartLocalMatrix;
            previewInnerBone.SetPositionAndRotation(
                Vector3.LerpUnclamped(
                    innerStartPosition,
                    innerTargetMatrix.GetColumn(3),
                    progress),
                Quaternion.SlerpUnclamped(
                    innerStartRotation,
                    innerTargetMatrix.rotation,
                    progress));
            previewOuterBone.SetPositionAndRotation(
                Vector3.LerpUnclamped(
                    outerStartPosition,
                    installedOuterTarget.position,
                    progress),
                Quaternion.SlerpUnclamped(
                    outerStartRotation,
                    installedOuterTarget.rotation,
                    progress));
            RefreshPresentation();
        }

        public void CompleteInstallTransition(bool installed)
        {
            transitionPreviewActive = false;
            transitionMount = null;
            if (installedRenderer != null && installedInnerBone != null &&
                installedOuterTarget != null)
            {
                installedRenderer.bones = new[]
                {
                    installedInnerBone,
                    installedOuterTarget,
                };
                installedRenderer.rootBone = installedInnerBone;
            }

            RefreshPresentation();
        }

        private void EnsurePreviewBones()
        {
            if (previewInnerBone != null && previewOuterBone != null)
            {
                return;
            }

            previewInnerBone = new GameObject(
                "Install preview steering inner bone").transform;
            previewInnerBone.SetParent(part.transform, false);
            previewOuterBone = new GameObject(
                "Install preview steering outer bone").transform;
            previewOuterBone.SetParent(part.transform, false);
        }

        private static void SetWorldPose(Transform target, Matrix4x4 worldMatrix)
        {
            target.SetPositionAndRotation(
                worldMatrix.GetColumn(3),
                worldMatrix.rotation);
            target.localScale = Vector3.one;
        }

        private void OnDisable()
        {
            if (transitionPreviewActive)
            {
                CompleteInstallTransition(false);
            }
        }
    }
}
