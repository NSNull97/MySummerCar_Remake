using System;
using UnityEngine;

namespace MSC.World.Streaming
{
    /// <summary>
    /// Project-owned safety recovery for the temporary feature-parity world.
    /// It does not alter donor geometry and returns only its explicitly
    /// configured owner to a project-authored safe transform.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldOutOfBoundsRecovery : MonoBehaviour
    {
        [SerializeField] private float minimumAllowedY = -64f;
        [SerializeField] private Vector3 recoveryPosition;
        [SerializeField] private Vector3 recoveryEulerAngles;
        [SerializeField] private bool configured;

        private CharacterController characterController;
        private Rigidbody attachedRigidbody;

        public bool IsConfigured => configured;
        public float MinimumAllowedY => minimumAllowedY;
        public Vector3 RecoveryPosition => recoveryPosition;
        public Quaternion RecoveryRotation =>
            Quaternion.Euler(recoveryEulerAngles);
        public int RecoveryCount { get; private set; }

        public void Configure(
            Vector3 safePosition,
            Quaternion safeRotation,
            float minimumWorldY)
        {
            if (!IsFinite(safePosition) ||
                !IsFinite(safeRotation) ||
                !float.IsFinite(minimumWorldY))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(safePosition),
                    "Out-of-bounds recovery configuration must be finite.");
            }

            if (minimumWorldY >= safePosition.y)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumWorldY),
                    "Recovery height must remain above the out-of-bounds threshold.");
            }

            recoveryPosition = safePosition;
            recoveryEulerAngles = safeRotation.eulerAngles;
            minimumAllowedY = minimumWorldY;
            configured = true;
        }

        public void RecoverNow()
        {
            if (!configured)
            {
                throw new InvalidOperationException(
                    "World out-of-bounds recovery has not been configured.");
            }

            CachePhysicsComponents();

            bool controllerWasEnabled =
                characterController != null &&
                characterController.enabled;
            if (controllerWasEnabled)
            {
                characterController.enabled = false;
            }

            if (attachedRigidbody != null)
            {
                attachedRigidbody.linearVelocity = Vector3.zero;
                attachedRigidbody.angularVelocity = Vector3.zero;
                attachedRigidbody.position = recoveryPosition;
                attachedRigidbody.rotation = RecoveryRotation;
            }

            transform.SetPositionAndRotation(
                recoveryPosition,
                RecoveryRotation);

            if (controllerWasEnabled)
            {
                characterController.enabled = true;
            }

            Physics.SyncTransforms();
            RecoveryCount++;
        }

        private void Awake()
        {
            CachePhysicsComponents();
        }

        private void Start()
        {
            if (!configured)
            {
                throw new InvalidOperationException(
                    "World out-of-bounds recovery must be configured before Start.");
            }
        }

        private void LateUpdate()
        {
            Vector3 position = transform.position;
            if (!configured ||
                (IsFinite(position) && position.y >= minimumAllowedY))
            {
                return;
            }

            RecoverNow();
        }

        private void CachePhysicsComponents()
        {
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
            }

            if (attachedRigidbody == null)
            {
                attachedRigidbody = GetComponent<Rigidbody>();
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);
    }
}
