using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    /// <summary>
    /// Optional project-owned presentation profile for an assembly mount whose
    /// installed part remains operable around a donor-evidenced hinge axis.
    /// The assembly graph remains authoritative for attachment and removal.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MountPointAuthoring))]
    public sealed class AssemblyHingeMountAuthoring : MonoBehaviour
    {
        [SerializeField] private MountPointAuthoring mountPoint;
        [SerializeField] private Vector3 localAxis = Vector3.forward;
        [SerializeField] private float minimumAngleDegrees;
        [SerializeField] private float maximumAngleDegrees = 80f;
        [SerializeField, Min(0f)] private float donorBreakForce;
        [SerializeField, Min(0f)] private float donorBreakTorque;
        [SerializeField] private Vector3 donorOpenTorqueLocal;
        [SerializeField] private Vector3 donorCloseTorqueLocal;
        [SerializeField, Range(0.1f, 10f)]
        private float closeLatchThresholdDegrees = 1f;
        [SerializeField, Min(0f)] private float donorLatchBreakForce = 12000f;
        [SerializeField, Min(0f)] private float donorLatchBreakTorque = 12000f;
        [SerializeField, Min(0f)] private float donorOpenHoldWindowDegrees;
        [SerializeField] private bool requiresReleaseBeforeOpening;
        [SerializeField] private Transform[] fastenerTargets =
            System.Array.Empty<Transform>();

        public MountPointAuthoring MountPoint => mountPoint;
        public Vector3 LocalAxis => localAxis.sqrMagnitude > 0.0001f
            ? localAxis.normalized
            : Vector3.forward;
        public float MinimumAngleDegrees => minimumAngleDegrees;
        public float MaximumAngleDegrees => maximumAngleDegrees;
        public float DonorBreakForce => donorBreakForce;
        public float DonorBreakTorque => donorBreakTorque;
        public Vector3 DonorOpenTorqueLocal => donorOpenTorqueLocal;
        public Vector3 DonorCloseTorqueLocal => donorCloseTorqueLocal;
        public float CloseLatchThresholdDegrees => closeLatchThresholdDegrees;
        public float DonorLatchBreakForce => donorLatchBreakForce;
        public float DonorLatchBreakTorque => donorLatchBreakTorque;
        public float DonorOpenHoldWindowDegrees =>
            donorOpenHoldWindowDegrees;
        public bool HasDonorOpenHold => donorOpenHoldWindowDegrees > 0.001f;
        public bool RequiresReleaseBeforeOpening =>
            requiresReleaseBeforeOpening;
        public Transform[] FastenerTargets => fastenerTargets;
        public float OpenAngleDegrees =>
            Mathf.Abs(maximumAngleDegrees) >= Mathf.Abs(minimumAngleDegrees)
                ? maximumAngleDegrees
                : minimumAngleDegrees;
        public float OpenHoldMinimumAngleDegrees => OpenAngleDegrees < 0f
            ? OpenAngleDegrees
            : Mathf.Max(
                minimumAngleDegrees,
                OpenAngleDegrees - donorOpenHoldWindowDegrees);
        public float OpenHoldMaximumAngleDegrees => OpenAngleDegrees < 0f
            ? Mathf.Min(
                maximumAngleDegrees,
                OpenAngleDegrees + donorOpenHoldWindowDegrees)
            : OpenAngleDegrees;

        public void Configure(
            MountPointAuthoring authoredMountPoint,
            Vector3 authoredLocalAxis,
            float authoredMinimumAngleDegrees,
            float authoredMaximumAngleDegrees,
            float authoredDonorBreakForce,
            float authoredDonorBreakTorque,
            Vector3 authoredDonorOpenTorqueLocal,
            Vector3 authoredDonorCloseTorqueLocal,
            float authoredCloseLatchThresholdDegrees = 1f,
            float authoredDonorLatchBreakForce = 12000f,
            float authoredDonorLatchBreakTorque = 12000f,
            float authoredDonorOpenHoldWindowDegrees = 0f,
            bool authoredRequiresReleaseBeforeOpening = false,
            Transform[] authoredFastenerTargets = null)
        {
            mountPoint = authoredMountPoint;
            localAxis = authoredLocalAxis.sqrMagnitude > 0.0001f
                ? authoredLocalAxis.normalized
                : Vector3.forward;
            minimumAngleDegrees = Mathf.Min(
                authoredMinimumAngleDegrees,
                authoredMaximumAngleDegrees);
            maximumAngleDegrees = Mathf.Max(
                authoredMinimumAngleDegrees,
                authoredMaximumAngleDegrees);
            donorBreakForce = Mathf.Max(0f, authoredDonorBreakForce);
            donorBreakTorque = Mathf.Max(0f, authoredDonorBreakTorque);
            donorOpenTorqueLocal = authoredDonorOpenTorqueLocal;
            donorCloseTorqueLocal = authoredDonorCloseTorqueLocal;
            closeLatchThresholdDegrees = Mathf.Clamp(
                authoredCloseLatchThresholdDegrees,
                0.1f,
                10f);
            donorLatchBreakForce = Mathf.Max(
                0f,
                authoredDonorLatchBreakForce);
            donorLatchBreakTorque = Mathf.Max(
                0f,
                authoredDonorLatchBreakTorque);
            donorOpenHoldWindowDegrees = Mathf.Clamp(
                authoredDonorOpenHoldWindowDegrees,
                0f,
                maximumAngleDegrees - minimumAngleDegrees);
            requiresReleaseBeforeOpening =
                authoredRequiresReleaseBeforeOpening;
            fastenerTargets = authoredFastenerTargets ??
                System.Array.Empty<Transform>();
        }

        internal void AttachFastenerTargets(Transform installedPart)
        {
            if (installedPart == null)
            {
                return;
            }

            for (int index = 0; index < fastenerTargets.Length; index++)
            {
                Transform target = fastenerTargets[index];
                if (target != null)
                {
                    target.SetParent(installedPart, false);
                }
            }
        }

        internal void ResetFastenerTargets()
        {
            for (int index = 0; index < fastenerTargets.Length; index++)
            {
                Transform target = fastenerTargets[index];
                if (target != null)
                {
                    target.SetParent(transform, false);
                }
            }
        }
    }
}
