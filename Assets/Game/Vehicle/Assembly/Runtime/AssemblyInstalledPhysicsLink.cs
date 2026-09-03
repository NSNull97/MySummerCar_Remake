using UnityEngine;

namespace MSC.Vehicle.Assembly
{
    public enum AssemblyInstalledPhysicsLinkMode
    {
        Fixed = 0,
        TrailingArmHinge = 1,
        FrontWishboneHinge = 2,
        RoadWheelAxle = 3,
        OperablePanelHinge = 4,
    }

    /// <summary>
    /// Opt-in physical installation for structural parts. Fixed children pass
    /// contact into their owning rigidbody, while suspension arms retain their
    /// authored pivot and travel around a vehicle-frame suspension axis.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class AssemblyInstalledPhysicsLink : MonoBehaviour
    {
        [SerializeField] private Rigidbody targetBody;
        [SerializeField] private Rigidbody connectedBodyOverride;
        [SerializeField] private AssemblyInstalledPhysicsLinkMode linkMode;
        [SerializeField] private float hingeMinimumDegrees = -32f;
        [SerializeField] private float hingeMaximumDegrees = 22f;
        [SerializeField, Min(0.0001f)]
        private float weldedProjectionDistance = 0.001f;
        [SerializeField, Range(0.1f, 5f)]
        private float weldedProjectionAngle = 0.5f;
        [SerializeField, Range(6, 64)] private int installedSolverIterations = 20;
        [SerializeField, Range(1, 32)]
        private int installedSolverVelocityIterations = 8;

        private Joint installedJoint;
        private Rigidbody roadWheelRestReferenceBody;
        private float roadWheelGroundContactExpiresAt;

        private const float RoadWheelRestLinearSpeedMetersPerSecond = 0.03f;
        private const float RoadWheelRestChassisAngularSpeedRadiansPerSecond =
            0.05f;
        private const float RoadWheelRestAngularSpeedRadiansPerSecond = 0.75f;
        private const float RoadWheelGroundContactGraceSeconds = 0.06f;

        public bool IsAttached => installedJoint != null &&
            targetBody != null && !targetBody.isKinematic;

        public AssemblyInstalledPhysicsLinkMode LinkMode => linkMode;

        public Rigidbody ConnectedBodyOverride => connectedBodyOverride;

        public Joint InstalledJoint => installedJoint;

        public HingeJoint InstalledHinge => installedJoint as HingeJoint;

        public ConfigurableJoint InstalledWeld =>
            installedJoint as ConfigurableJoint;

        public float WeldedProjectionDistance => weldedProjectionDistance;

        public float WeldedProjectionAngle => weldedProjectionAngle;

        public int InstalledSolverIterations => installedSolverIterations;

        public int InstalledSolverVelocityIterations =>
            installedSolverVelocityIterations;

        public Rigidbody ConnectedBody => installedJoint != null
            ? installedJoint.connectedBody
            : null;

        public void Configure(Rigidbody body)
        {
            Configure(body, AssemblyInstalledPhysicsLinkMode.Fixed);
        }

        public void Configure(
            Rigidbody body,
            AssemblyInstalledPhysicsLinkMode configuredMode,
            float minimumDegrees = -32f,
            float maximumDegrees = 22f,
            Rigidbody preferredConnectedBody = null)
        {
            targetBody = body;
            connectedBodyOverride = preferredConnectedBody;
            linkMode = configuredMode;
            hingeMinimumDegrees = Mathf.Min(minimumDegrees, maximumDegrees);
            hingeMaximumDegrees = Mathf.Max(minimumDegrees, maximumDegrees);
        }

        internal bool TryAttach(MountPointAuthoring mountPoint)
        {
            if (targetBody == null || mountPoint == null)
            {
                return false;
            }

            Rigidbody ownerBody = ResolveOwnerBody(mountPoint);
            if (ownerBody == null || ownerBody == targetBody)
            {
                return false;
            }

            DetachJoint();
            Vector3 inheritedLinearVelocity = ownerBody.isKinematic
                ? Vector3.zero
                : ownerBody.linearVelocity;
            Vector3 inheritedAngularVelocity = ownerBody.isKinematic
                ? Vector3.zero
                : ownerBody.angularVelocity;
            targetBody.useGravity = true;
            targetBody.isKinematic = false;
            targetBody.detectCollisions = true;
            targetBody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
            targetBody.interpolation = ownerBody.interpolation;
            targetBody.solverIterations = Mathf.Max(
                targetBody.solverIterations,
                installedSolverIterations);
            targetBody.solverVelocityIterations = Mathf.Max(
                targetBody.solverVelocityIterations,
                installedSolverVelocityIterations);
            ownerBody.solverIterations = Mathf.Max(
                ownerBody.solverIterations,
                installedSolverIterations);
            ownerBody.solverVelocityIterations = Mathf.Max(
                ownerBody.solverVelocityIterations,
                installedSolverVelocityIterations);
            targetBody.linearVelocity = ownerBody.isKinematic
                ? inheritedLinearVelocity
                : ownerBody.GetPointVelocity(targetBody.worldCenterOfMass);
            targetBody.angularVelocity = linkMode ==
                AssemblyInstalledPhysicsLinkMode.RoadWheelAxle
                    ? Vector3.zero
                    : inheritedAngularVelocity;

            VehicleAssemblyController vehicleAssembly = mountPoint
                .GetComponentInParent<VehicleAssemblyController>();
            Transform vehicleFrame = vehicleAssembly != null
                ? vehicleAssembly.transform
                : null;
            Transform suspensionFrame = vehicleFrame != null
                ? vehicleFrame
                : ownerBody.transform;
            roadWheelRestReferenceBody = vehicleAssembly != null
                ? vehicleAssembly.GetComponent<Rigidbody>()
                : ownerBody;

            installedJoint = linkMode switch
            {
                AssemblyInstalledPhysicsLinkMode.TrailingArmHinge =>
                    CreateSuspensionHinge(
                        ownerBody,
                        suspensionFrame.right),
                AssemblyInstalledPhysicsLinkMode.FrontWishboneHinge =>
                    CreateSuspensionHinge(
                        ownerBody,
                        suspensionFrame.forward),
                AssemblyInstalledPhysicsLinkMode.RoadWheelAxle =>
                    CreateRoadWheelAxle(
                        ownerBody,
                        suspensionFrame.right),
                AssemblyInstalledPhysicsLinkMode.OperablePanelHinge =>
                    CreateOperablePanelHinge(ownerBody, mountPoint),
                _ => CreateProjectedWeld(ownerBody),
            };
            targetBody.WakeUp();
            if (!ownerBody.isKinematic)
            {
                ownerBody.WakeUp();
            }

            return true;
        }

        internal bool RequiresOwnerReattach(MountPointAuthoring mountPoint)
        {
            Rigidbody desiredOwner = ResolveOwnerBody(mountPoint);
            return installedJoint != null && desiredOwner != null &&
                installedJoint.connectedBody != desiredOwner;
        }

        private Rigidbody ResolveOwnerBody(MountPointAuthoring mountPoint)
        {
            if (connectedBodyOverride != null)
            {
                PartInstance preferredPart = connectedBodyOverride
                    .GetComponent<PartInstance>();
                if (preferredPart == null || preferredPart.IsInstalled)
                {
                    return connectedBodyOverride;
                }
            }

            PartInstance ownerPart = mountPoint != null
                ? mountPoint.transform.GetComponentInParent<PartInstance>()
                : null;
            return ownerPart != null && ownerPart.gameObject != gameObject
                ? ownerPart.Body
                : mountPoint?.GetComponentInParent<VehicleAssemblyController>()
                    ?.GetComponent<Rigidbody>();
        }

        private ConfigurableJoint CreateProjectedWeld(Rigidbody ownerBody)
        {
            ConfigurableJoint joint = targetBody.gameObject
                .AddComponent<ConfigurableJoint>();
            ConfigureCommonJoint(joint, ownerBody);
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = Vector3.zero;
            joint.connectedAnchor = ownerBody.transform.InverseTransformPoint(
                targetBody.transform.position);
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Locked;
            joint.angularYMotion = ConfigurableJointMotion.Locked;
            joint.angularZMotion = ConfigurableJointMotion.Locked;
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = weldedProjectionDistance;
            joint.projectionAngle = weldedProjectionAngle;

            // A grounded drum is the final body in a chassis -> arm -> drum
            // joint chain. PhysX preprocessing may soften that closed contact
            // constraint and let a FixedJoint visibly stretch. A fully locked
            // projected weld keeps the reviewed arm-local socket authoritative
            // while the drum collider still transfers ground force to the arm.
            joint.enablePreprocessing = false;
            return joint;
        }

        private HingeJoint CreateSuspensionHinge(
            Rigidbody ownerBody,
            Vector3 worldAxis)
        {
            return CreateHinge(ownerBody, worldAxis, useLimits: true);
        }

        private HingeJoint CreateRoadWheelAxle(
            Rigidbody ownerBody,
            Vector3 worldAxis)
        {
            return CreateHinge(ownerBody, worldAxis, useLimits: false);
        }

        private HingeJoint CreateOperablePanelHinge(
            Rigidbody ownerBody,
            MountPointAuthoring mountPoint)
        {
            AssemblyHingeMountAuthoring profile = mountPoint
                .GetComponent<AssemblyHingeMountAuthoring>();
            if (profile == null)
            {
                return CreateHinge(
                    ownerBody,
                    mountPoint.Pose.forward,
                    useLimits: true);
            }

            Vector3 worldAxis = mountPoint.Pose.TransformDirection(
                profile.LocalAxis);
            HingeJoint hinge = CreateHinge(
                ownerBody,
                worldAxis,
                useLimits: true);
            JointLimits limits = hinge.limits;
            limits.min = profile.MinimumAngleDegrees;
            limits.max = profile.MaximumAngleDegrees;
            limits.bounciness = 0f;
            limits.contactDistance = 0.25f;
            hinge.limits = limits;
            // Keep the audited donor break values on the authoring profile,
            // but do not feed them directly to Unity 6 PhysX. Constraint
            // impulses from a moving chassis can exceed the old Unity value
            // during ordinary panel motion, destroying the HingeJoint. The
            // assembly graph already owns the donor-visible breakaway rule:
            // an entirely unfastened panel detaches at full opening.
            hinge.breakForce = float.PositiveInfinity;
            hinge.breakTorque = float.PositiveInfinity;
            return hinge;
        }

        private HingeJoint CreateHinge(
            Rigidbody ownerBody,
            Vector3 worldAxis,
            bool useLimits)
        {
            HingeJoint hinge = targetBody.gameObject.AddComponent<HingeJoint>();
            ConfigureCommonJoint(hinge, ownerBody);

            // Donor suspension roots sit at their body-side pivots. Imported
            // part roots have presentation-space rotations unrelated to the
            // vehicle frame, so the caller supplies a chassis-derived axis.
            hinge.autoConfigureConnectedAnchor = false;
            hinge.anchor = Vector3.zero;
            hinge.connectedAnchor = ownerBody.transform.InverseTransformPoint(
                targetBody.transform.position);
            hinge.axis = targetBody.transform.InverseTransformDirection(
                worldAxis).normalized;
            hinge.useSpring = false;
            hinge.useMotor = false;
            hinge.useLimits = useLimits;
            if (useLimits)
            {
                JointLimits limits = hinge.limits;
                limits.min = hingeMinimumDegrees;
                limits.max = hingeMaximumDegrees;
                limits.bounciness = 0f;
                limits.contactDistance = 1f;
                hinge.limits = limits;
            }

            return hinge;
        }

        private void FixedUpdate()
        {
            ClampRoadWheelRestState();
        }

        private void LateUpdate()
        {
            // Rigidbody contacts are solved after FixedUpdate and can add a
            // tiny axle velocity back to an otherwise stationary loaded wheel.
            // The donor wheel integrator resolves rolling resistance to an
            // exact zero; repeat the same rest latch after the PhysX step so
            // presentation cannot accumulate that solver residue.
            ClampRoadWheelRestState();
        }

        private void ClampRoadWheelRestState()
        {
            if (linkMode != AssemblyInstalledPhysicsLinkMode.RoadWheelAxle ||
                !IsAttached || targetBody == null ||
                Time.fixedTime > roadWheelGroundContactExpiresAt)
            {
                return;
            }

            HingeJoint axle = installedJoint as HingeJoint;
            Rigidbody ownerBody = axle != null ? axle.connectedBody : null;
            if (axle == null || ownerBody == null)
            {
                return;
            }

            Rigidbody restReference = roadWheelRestReferenceBody != null
                ? roadWheelRestReferenceBody
                : ownerBody;
            Vector3 gravity = Physics.gravity;
            Vector3 planarReferenceVelocity = gravity.sqrMagnitude > 0.0001f
                ? Vector3.ProjectOnPlane(
                    restReference.linearVelocity,
                    gravity.normalized)
                : restReference.linearVelocity;
            if (planarReferenceVelocity.magnitude >
                    RoadWheelRestLinearSpeedMetersPerSecond ||
                restReference.angularVelocity.magnitude >
                    RoadWheelRestChassisAngularSpeedRadiansPerSecond)
            {
                return;
            }

            Vector3 worldAxis = targetBody.transform.TransformDirection(
                axle.axis).normalized;
            float axleAngularSpeed = Vector3.Dot(
                targetBody.angularVelocity,
                worldAxis);
            if (!float.IsFinite(axleAngularSpeed) ||
                Mathf.Abs(axleAngularSpeed) >
                    RoadWheelRestAngularSpeedRadiansPerSecond)
            {
                return;
            }

            targetBody.angularVelocity -= worldAxis * axleAngularSpeed;
        }

        private void OnCollisionStay(Collision collision)
        {
            if (linkMode != AssemblyInstalledPhysicsLinkMode.RoadWheelAxle ||
                collision == null)
            {
                return;
            }

            Vector3 gravity = Physics.gravity;
            Vector3 up = gravity.sqrMagnitude > 0.0001f
                ? -gravity.normalized
                : Vector3.up;
            int contactCount = collision.contactCount;
            for (int index = 0; index < contactCount; index++)
            {
                if (Vector3.Dot(collision.GetContact(index).normal, up) <= 0.35f)
                {
                    continue;
                }

                roadWheelGroundContactExpiresAt = Time.fixedTime +
                    RoadWheelGroundContactGraceSeconds;
                // Collision callbacks run after the PhysX solve that may have
                // reintroduced a tiny axle speed. Latch the donor-equivalent
                // zero here as well as in the regular update passes so the
                // rendered wheel never accumulates contact jitter at rest.
                ClampRoadWheelRestState();
                return;
            }
        }

        private static void ConfigureCommonJoint(
            Joint joint,
            Rigidbody ownerBody)
        {
            joint.connectedBody = ownerBody;
            joint.enableCollision = false;
            joint.enablePreprocessing = true;
            joint.breakForce = float.PositiveInfinity;
            joint.breakTorque = float.PositiveInfinity;
        }

        internal void DetachJoint()
        {
            roadWheelGroundContactExpiresAt = float.NegativeInfinity;
            roadWheelRestReferenceBody = null;
            if (installedJoint == null)
            {
                return;
            }

            Joint joint = installedJoint;
            installedJoint = null;
            if (Application.isPlaying)
            {
                Destroy(joint);
            }
            else
            {
                DestroyImmediate(joint);
            }
        }

        private void Awake()
        {
            if (targetBody == null)
            {
                targetBody = GetComponent<Rigidbody>();
            }
        }
    }
}
