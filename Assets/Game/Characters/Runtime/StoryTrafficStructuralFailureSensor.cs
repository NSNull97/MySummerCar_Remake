using System;
using UnityEngine;

namespace MSC.Characters
{
    /// <summary>
    /// Project-owned reconstruction of the donor Jani crash proof mass. The
    /// original car used a 0.5 kg Rigidbody connected to the chassis by a
    /// FixedJoint. The joint was unbreakable until the car was near the player
    /// and travelling faster than 55 km/h, at which point both break limits
    /// became 600 N / Nm. Breaking that joint, not an arbitrary contact-speed
    /// threshold, is the authoritative terminal story-crash signal.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(FixedJoint))]
    public sealed class StoryTrafficStructuralFailureSensor : MonoBehaviour
    {
        public const float DonorProofMassKilograms = 0.5f;
        public const float DonorArmSpeedKilometersPerHour = 55f;
        public const float DonorArmDistanceMeters = 1500f;
        public const float DonorBreakForceNewtons = 600f;
        public const float DonorBreakTorqueNewtonMeters = 600f;

        [SerializeField] private Rigidbody chassis;
        [SerializeField] private Transform player;
        [SerializeField] private Rigidbody proofMass;
        [SerializeField] private FixedJoint structuralJoint;

        private bool initialized;
        private bool failureReported;
        private bool suspended;

        public bool IsArmed { get; private set; }

        public event Action StructuralFailure;

        public void Configure(Rigidbody configuredChassis, Transform configuredPlayer)
        {
            chassis = configuredChassis ??
                throw new ArgumentNullException(nameof(configuredChassis));
            player = configuredPlayer ??
                throw new ArgumentNullException(nameof(configuredPlayer));
            proofMass = GetComponent<Rigidbody>();
            structuralJoint = GetComponent<FixedJoint>();

            proofMass.mass = DonorProofMassKilograms;
            proofMass.linearDamping = 0f;
            proofMass.angularDamping = 0.05f;
            proofMass.useGravity = true;
            proofMass.isKinematic = false;
            proofMass.interpolation = RigidbodyInterpolation.None;
            proofMass.collisionDetectionMode = CollisionDetectionMode.Discrete;

            structuralJoint.connectedBody = chassis;
            structuralJoint.breakForce = Mathf.Infinity;
            structuralJoint.breakTorque = Mathf.Infinity;
            structuralJoint.enableCollision = false;
            structuralJoint.enablePreprocessing = true;

            initialized = true;
            failureReported = false;
            suspended = false;
            IsArmed = false;
        }

        public void SetSuspended(bool value)
        {
            suspended = value;
            if (value)
            {
                SetJointArmed(false);
            }
        }

        private void FixedUpdate()
        {
            if (!initialized || failureReported || structuralJoint == null ||
                chassis == null || player == null)
            {
                return;
            }

            float speedKilometersPerHour =
                chassis.linearVelocity.magnitude * 3.6f;
            float sqrDistance =
                (chassis.worldCenterOfMass - player.position).sqrMagnitude;
            bool shouldArm = !suspended &&
                speedKilometersPerHour > DonorArmSpeedKilometersPerHour &&
                sqrDistance <= DonorArmDistanceMeters * DonorArmDistanceMeters;
            SetJointArmed(shouldArm);
        }

        private void SetJointArmed(bool armed)
        {
            IsArmed = armed;
            if (structuralJoint == null)
            {
                return;
            }

            structuralJoint.breakForce = armed
                ? DonorBreakForceNewtons
                : Mathf.Infinity;
            structuralJoint.breakTorque = armed
                ? DonorBreakTorqueNewtonMeters
                : Mathf.Infinity;
        }

        private void OnDisable()
        {
            // Streaming/destruction is not a structural crash. Disarm before
            // Unity tears down either Rigidbody participating in the joint.
            SetJointArmed(false);
        }

        private void OnJointBreak(float breakForce)
        {
            // A joint can disappear because its hierarchy is being streamed
            // out or its connected body is being rebuilt. Only a joint that
            // was explicitly armed by the donor speed/distance contract is a
            // terminal story crash.
            if (!initialized || failureReported || suspended ||
                !isActiveAndEnabled || !IsArmed)
            {
                return;
            }

            failureReported = true;
            IsArmed = false;
            StructuralFailure?.Invoke();

            // The donor proof mass has no renderer or collider. Once it has
            // delivered the state transition it has no further gameplay role.
            Destroy(gameObject);
        }
    }
}
