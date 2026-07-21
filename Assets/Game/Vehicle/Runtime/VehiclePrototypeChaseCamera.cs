using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Presentation-only chase camera for the M06 graybox. It follows the
    /// interpolated chassis in world space without inheriting suspension roll,
    /// pitch, or high-frequency heave as a transform child.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class VehiclePrototypeChaseCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 2.7f, -5.5f);
        [SerializeField] private Vector3 localLookAtOffset = new Vector3(0f, 0.5f, 2.5f);
        [SerializeField, Min(0.1f)] private float positionSharpness = 7f;
        [SerializeField, Min(0.1f)] private float rotationSharpness = 10f;
        [SerializeField, Min(1f)] private float teleportSnapDistance = 12f;

        private bool initialized;

        public Transform Target => target;

        public void Configure(
            Transform followTarget,
            Vector3 cameraOffset,
            Vector3 lookAtOffset,
            float followPositionSharpness = 7f,
            float followRotationSharpness = 10f)
        {
            target = followTarget;
            localOffset = cameraOffset;
            localLookAtOffset = lookAtOffset;
            positionSharpness = Mathf.Max(0.1f, followPositionSharpness);
            rotationSharpness = Mathf.Max(0.1f, followRotationSharpness);
            initialized = false;
            SnapToTarget();
        }

        private void OnEnable()
        {
            initialized = false;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            CalculateDesiredPose(out Vector3 desiredPosition, out Quaternion desiredRotation);
            if (!initialized ||
                (transform.position - desiredPosition).sqrMagnitude >
                teleportSnapDistance * teleportSnapDistance)
            {
                transform.SetPositionAndRotation(desiredPosition, desiredRotation);
                initialized = true;
                return;
            }

            // Presentation follows the gameplay clock so a UI pause freezes the
            // camera together with vehicle physics instead of visibly settling
            // while the pause menu is open.
            float deltaSeconds = Mathf.Max(0f, Time.deltaTime);
            float positionBlend = 1f - Mathf.Exp(-positionSharpness * deltaSeconds);
            float rotationBlend = 1f - Mathf.Exp(-rotationSharpness * deltaSeconds);
            transform.position = Vector3.Lerp(transform.position, desiredPosition, positionBlend);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
        }

        public void SnapToTarget()
        {
            if (target == null)
            {
                return;
            }

            CalculateDesiredPose(out Vector3 desiredPosition, out Quaternion desiredRotation);
            transform.SetPositionAndRotation(desiredPosition, desiredRotation);
            initialized = true;
        }

        private void CalculateDesiredPose(
            out Vector3 desiredPosition,
            out Quaternion desiredRotation)
        {
            Vector3 planarForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (planarForward.sqrMagnitude < 0.0001f)
            {
                planarForward = Vector3.forward;
            }

            Quaternion yawRotation = Quaternion.LookRotation(planarForward.normalized, Vector3.up);
            desiredPosition = target.position + yawRotation * localOffset;
            Vector3 lookAt = target.position + yawRotation * localLookAtOffset;
            Vector3 lookDirection = lookAt - desiredPosition;
            desiredRotation = lookDirection.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(lookDirection.normalized, Vector3.up)
                : yawRotation;
        }
    }
}
