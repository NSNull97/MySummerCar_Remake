using MSC.Vehicle.Assembly;
using UnityEngine;

namespace MSC.Vehicle.NWH
{
    /// <summary>
    /// Switches the loose donor strut mesh to the donor two-bone skin while the
    /// part is installed. The upper bone follows the installed mount and the
    /// lower bone follows NWH's moving spindle target.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SatsumaFrontStrutPresentation : MonoBehaviour,
        IAssemblyInstallTransitionPresentation
    {
        [SerializeField] private PartInstance part;
        [SerializeField] private MeshRenderer looseRenderer;
        [SerializeField] private SkinnedMeshRenderer installedRenderer;
        [SerializeField] private Transform installedUpperBone;
        [SerializeField] private Transform installedLowerTarget;

        private Transform previewUpperBone;
        private Transform previewLowerBone;
        private MountPointAuthoring transitionMount;
        private Matrix4x4 upperBonePartLocalMatrix;
        private Vector3 upperStartPosition;
        private Quaternion upperStartRotation;
        private Vector3 lowerStartPosition;
        private Quaternion lowerStartRotation;
        private bool transitionPreviewActive;

        public PartInstance Part => part;
        public MeshRenderer LooseRenderer => looseRenderer;
        public SkinnedMeshRenderer InstalledRenderer => installedRenderer;
        public Transform InstalledUpperBone => installedUpperBone;
        public Transform InstalledLowerTarget => installedLowerTarget;
        public bool IsInstallTransitionPreviewActive =>
            transitionPreviewActive;
        public bool IsInstalledPresentationActive =>
            installedRenderer != null && installedRenderer.enabled;

        public void Configure(
            PartInstance configuredPart,
            MeshRenderer configuredLooseRenderer,
            SkinnedMeshRenderer configuredInstalledRenderer,
            Transform configuredInstalledUpperBone,
            Transform configuredInstalledLowerTarget)
        {
            part = configuredPart;
            looseRenderer = configuredLooseRenderer;
            installedRenderer = configuredInstalledRenderer;
            installedUpperBone = configuredInstalledUpperBone;
            installedLowerTarget = configuredInstalledLowerTarget;
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
                installedRenderer == null || installedUpperBone == null ||
                installedLowerTarget == null ||
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
            SetWorldPose(
                previewUpperBone,
                rendererWorld * bindPoses[0].inverse);
            SetWorldPose(
                previewLowerBone,
                rendererWorld * bindPoses[1].inverse);
            upperStartPosition = previewUpperBone.position;
            upperStartRotation = previewUpperBone.rotation;
            lowerStartPosition = previewLowerBone.position;
            lowerStartRotation = previewLowerBone.rotation;
            upperBonePartLocalMatrix = part.transform.worldToLocalMatrix *
                installedUpperBone.localToWorldMatrix;
            transitionMount = mount;
            transitionPreviewActive = true;
            installedRenderer.bones = new[]
            {
                previewUpperBone,
                previewLowerBone,
            };
            installedRenderer.rootBone = previewUpperBone;
            ApplyInstallTransition(0f);
        }

        public void ApplyInstallTransition(float normalizedProgress)
        {
            if (!transitionPreviewActive || transitionMount == null ||
                installedLowerTarget == null)
            {
                return;
            }

            float progress = Mathf.Clamp01(normalizedProgress);
            Transform mountPose = transitionMount.Pose;
            Matrix4x4 mountWorld = Matrix4x4.TRS(
                mountPose.position,
                mountPose.rotation,
                Vector3.one);
            Matrix4x4 upperTargetMatrix = mountWorld *
                upperBonePartLocalMatrix;
            Vector3 upperTargetPosition = upperTargetMatrix.GetColumn(3);
            Quaternion upperTargetRotation = upperTargetMatrix.rotation;
            previewUpperBone.SetPositionAndRotation(
                Vector3.LerpUnclamped(
                    upperStartPosition,
                    upperTargetPosition,
                    progress),
                Quaternion.SlerpUnclamped(
                    upperStartRotation,
                    upperTargetRotation,
                    progress));
            previewLowerBone.SetPositionAndRotation(
                Vector3.LerpUnclamped(
                    lowerStartPosition,
                    installedLowerTarget.position,
                    progress),
                Quaternion.SlerpUnclamped(
                    lowerStartRotation,
                    installedLowerTarget.rotation,
                    progress));
            RefreshPresentation();
        }

        public void CompleteInstallTransition(bool installed)
        {
            transitionPreviewActive = false;
            transitionMount = null;
            if (installedRenderer != null && installedUpperBone != null &&
                installedLowerTarget != null)
            {
                installedRenderer.bones = new[]
                {
                    installedUpperBone,
                    installedLowerTarget,
                };
                installedRenderer.rootBone = installedUpperBone;
            }

            RefreshPresentation();
        }

        private void EnsurePreviewBones()
        {
            if (previewUpperBone != null && previewLowerBone != null)
            {
                return;
            }

            previewUpperBone = new GameObject(
                "Install preview upper bone").transform;
            previewUpperBone.SetParent(part.transform, false);
            previewLowerBone = new GameObject(
                "Install preview lower bone").transform;
            previewLowerBone.SetParent(part.transform, false);
        }

        private static void SetWorldPose(
            Transform target,
            Matrix4x4 worldMatrix)
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
