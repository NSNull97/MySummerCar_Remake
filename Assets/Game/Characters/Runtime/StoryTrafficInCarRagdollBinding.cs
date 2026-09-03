using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MSC.Characters
{
    /// <summary>
    /// Project-owned articulated occupant response for a terminal story-car
    /// crash. Bone references are authored by the sanitized presentation
    /// importer; runtime code never searches donor hierarchy names.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryTrafficInCarRagdollBinding : MonoBehaviour
    {
        public enum ColliderShape
        {
            Box = 0,
            Sphere = 1,
            Capsule = 2,
        }

        [Serializable]
        public sealed class BodyDefinition
        {
            [SerializeField] private Transform bone;
            [SerializeField] private Transform endpoint;
            [SerializeField] private int connectedBodyIndex = -1;
            [SerializeField] private ColliderShape colliderShape;
            [SerializeField] private Vector3 boxSize = Vector3.one * 0.2f;
            [SerializeField, Min(0.01f)] private float radius = 0.08f;
            [SerializeField, Min(0.1f)] private float massKilograms = 3f;
            [SerializeField, Range(1f, 90f)] private float twistLimitDegrees =
                25f;
            [SerializeField, Range(1f, 120f)] private float swingLimitDegrees =
                45f;

            public Transform Bone => bone;
            public Transform Endpoint => endpoint;
            public int ConnectedBodyIndex => connectedBodyIndex;
            public ColliderShape Shape => colliderShape;
            public Vector3 BoxSize => boxSize;
            public float Radius => radius;
            public float MassKilograms => massKilograms;
            public float TwistLimitDegrees => twistLimitDegrees;
            public float SwingLimitDegrees => swingLimitDegrees;

#if UNITY_EDITOR
            public BodyDefinition(
                Transform configuredBone,
                Transform configuredEndpoint,
                int configuredConnectedBodyIndex,
                ColliderShape configuredShape,
                Vector3 configuredBoxSize,
                float configuredRadius,
                float configuredMassKilograms,
                float configuredTwistLimitDegrees,
                float configuredSwingLimitDegrees)
            {
                bone = configuredBone;
                endpoint = configuredEndpoint;
                connectedBodyIndex = configuredConnectedBodyIndex;
                colliderShape = configuredShape;
                boxSize = configuredBoxSize;
                radius = configuredRadius;
                massKilograms = configuredMassKilograms;
                twistLimitDegrees = configuredTwistLimitDegrees;
                swingLimitDegrees = configuredSwingLimitDegrees;
            }
#endif
        }

        [Serializable]
        public sealed class OccupantDefinition
        {
            [SerializeField] private string featureId = string.Empty;
            [SerializeField] private GameObject presentationRoot;
            [SerializeField] private Behaviour[] poseDrivers =
                Array.Empty<Behaviour>();
            [SerializeField] private BodyDefinition[] bodies =
                Array.Empty<BodyDefinition>();

            public string FeatureId => featureId;
            public GameObject PresentationRoot => presentationRoot;
            public IReadOnlyList<Behaviour> PoseDrivers =>
                poseDrivers ?? Array.Empty<Behaviour>();
            public IReadOnlyList<BodyDefinition> Bodies =>
                bodies ?? Array.Empty<BodyDefinition>();

#if UNITY_EDITOR
            public OccupantDefinition(
                string configuredFeatureId,
                GameObject configuredPresentationRoot,
                IEnumerable<Behaviour> configuredPoseDrivers,
                IEnumerable<BodyDefinition> configuredBodies)
            {
                featureId = configuredFeatureId ?? string.Empty;
                presentationRoot = configuredPresentationRoot;
                poseDrivers = (configuredPoseDrivers ??
                        Enumerable.Empty<Behaviour>())
                    .ToArray();
                bodies = (configuredBodies ??
                        Enumerable.Empty<BodyDefinition>())
                    .ToArray();
            }
#endif
        }

        private sealed class RuntimeBody
        {
            public BodyDefinition Definition;
            public Rigidbody Body;
            public Collider Collider;
            public Joint Joint;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        private sealed class RuntimeOccupant
        {
            public OccupantDefinition Definition;
            public RuntimeBody[] Bodies;
            public bool[] PoseDriverEnabledStates;
        }

        [SerializeField] private Rigidbody chassisBody;
        [SerializeField] private OccupantDefinition[] occupants =
            Array.Empty<OccupantDefinition>();
        [SerializeField, Min(0.05f)] private float seatRestraintLimitMeters =
            0.24f;
        [SerializeField, Range(1f, 90f)]
        private float seatRestraintAngularLimitDegrees = 35f;

        private readonly List<RuntimeOccupant> runtimeOccupants = new();
        private bool terminalCrashActive;
        private int activationCount;

        public bool IsTerminalCrashActive => terminalCrashActive;
        public int ActivationCount => activationCount;
        public int OccupantCount => occupants?.Length ?? 0;
        public int ActiveBodyCount => terminalCrashActive
            ? runtimeOccupants.Sum(occupant => occupant.Bodies.Length)
            : 0;

        /// <summary>
        /// An in-car occupant is deliberately not a carry target. Suski's
        /// separate rescue presentation creates pickup capability only after
        /// NpcWorldRuntime has completed the explicit wreck extraction state.
        /// </summary>
        public bool IsPickupAvailable => false;

        public void SetTerminalCrashActive(bool active)
        {
            if (terminalCrashActive == active)
            {
                return;
            }

            if (active)
            {
                ActivateTerminalCrash();
            }
            else
            {
                DeactivateTerminalCrash();
            }
        }

        public bool TryGetPrimaryBody(
            string featureId,
            out Rigidbody body)
        {
            RuntimeOccupant occupant = runtimeOccupants.FirstOrDefault(
                candidate => string.Equals(
                    candidate.Definition.FeatureId,
                    featureId,
                    StringComparison.Ordinal));
            body = occupant?.Bodies.FirstOrDefault()?.Body;
            return body != null;
        }

        public bool TryGetSeatRestraint(
            string featureId,
            out ConfigurableJoint restraint)
        {
            restraint = null;
            if (!TryGetPrimaryBody(featureId, out Rigidbody body))
            {
                return false;
            }

            restraint = body.GetComponent<ConfigurableJoint>();
            return restraint != null && restraint.connectedBody == chassisBody;
        }

        public bool TryValidate(out string failure)
        {
            if (chassisBody == null || chassisBody.transform != transform)
            {
                failure =
                    "In-car ragdoll requires the wrapper's authored chassis Rigidbody.";
                return false;
            }

            OccupantDefinition[] configuredOccupants = occupants ??
                Array.Empty<OccupantDefinition>();
            if (configuredOccupants.Length == 0 ||
                configuredOccupants.Any(occupant => occupant == null) ||
                configuredOccupants.Select(occupant => occupant.FeatureId)
                    .Distinct(StringComparer.Ordinal).Count() !=
                configuredOccupants.Length)
            {
                failure =
                    "In-car ragdoll requires distinct, explicitly authored occupants.";
                return false;
            }

            var allBones = new HashSet<Transform>();
            foreach (OccupantDefinition occupant in configuredOccupants)
            {
                if (string.IsNullOrWhiteSpace(occupant.FeatureId) ||
                    !occupant.FeatureId.StartsWith(
                        "P1.NPC.",
                        StringComparison.Ordinal) ||
                    occupant.PresentationRoot == null ||
                    !occupant.PresentationRoot.transform.IsChildOf(transform))
                {
                    failure =
                        "In-car ragdoll occupant identity/root is invalid.";
                    return false;
                }

                if (occupant.PoseDrivers.Any(driver => driver == null) ||
                    occupant.PoseDrivers.Distinct().Count() !=
                    occupant.PoseDrivers.Count)
                {
                    failure =
                        $"In-car ragdoll occupant '{occupant.FeatureId}' has invalid pose drivers.";
                    return false;
                }

                IReadOnlyList<BodyDefinition> bodies = occupant.Bodies;
                if (bodies.Count == 0)
                {
                    failure =
                        $"In-car ragdoll occupant '{occupant.FeatureId}' has no articulated bodies.";
                    return false;
                }

                for (int index = 0; index < bodies.Count; index++)
                {
                    BodyDefinition body = bodies[index];
                    if (body == null || body.Bone == null ||
                        !body.Bone.IsChildOf(transform) ||
                        !allBones.Add(body.Bone) ||
                        body.MassKilograms <= 0f ||
                        !float.IsFinite(body.MassKilograms) ||
                        body.Radius <= 0f ||
                        !float.IsFinite(body.Radius) ||
                        body.ConnectedBodyIndex >= index ||
                        (index == 0 && body.ConnectedBodyIndex != -1) ||
                        (index > 0 && body.ConnectedBodyIndex < 0) ||
                        (body.Shape == ColliderShape.Capsule &&
                         (body.Endpoint == null ||
                          !body.Endpoint.IsChildOf(transform))) ||
                        (body.Shape == ColliderShape.Box &&
                         (!IsFinitePositive(body.BoxSize.x) ||
                          !IsFinitePositive(body.BoxSize.y) ||
                          !IsFinitePositive(body.BoxSize.z))))
                    {
                        failure =
                            $"In-car ragdoll occupant '{occupant.FeatureId}' body {index} is invalid or duplicated.";
                        return false;
                    }
                }
            }

            if (!float.IsFinite(seatRestraintLimitMeters) ||
                seatRestraintLimitMeters <= 0f ||
                !float.IsFinite(seatRestraintAngularLimitDegrees) ||
                seatRestraintAngularLimitDegrees <= 0f)
            {
                failure = "In-car ragdoll seat restraint limits are invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private void ActivateTerminalCrash()
        {
            if (!TryValidate(out string failure))
            {
                throw new InvalidOperationException(
                    $"Cannot activate in-car ragdoll: {failure}");
            }

            if (runtimeOccupants.Count == 0)
            {
                BuildRuntimeBodies();
            }

            foreach (SkinnedMeshRenderer renderer in
                     GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                renderer.updateWhenOffscreen = true;
                Bounds bounds = renderer.localBounds;
                bounds.size = new Vector3(
                    Mathf.Max(4f, bounds.size.x),
                    Mathf.Max(4f, bounds.size.y),
                    Mathf.Max(4f, bounds.size.z));
                renderer.localBounds = bounds;
            }

            Vector3 chassisVelocity = chassisBody.linearVelocity;
            Vector3 chassisAngularVelocity = chassisBody.angularVelocity;
            foreach (RuntimeOccupant occupant in runtimeOccupants)
            {
                DisablePoseDrivers(occupant);
                for (int index = 0; index < occupant.Bodies.Length; index++)
                {
                    RuntimeBody runtimeBody = occupant.Bodies[index];
                    Transform bone = runtimeBody.Definition.Bone;
                    runtimeBody.LocalPosition = bone.localPosition;
                    runtimeBody.LocalRotation = bone.localRotation;
                    ConfigureJoint(occupant, index);
                    runtimeBody.Collider.enabled = true;
                    runtimeBody.Body.detectCollisions = true;
                    runtimeBody.Body.isKinematic = false;
                    runtimeBody.Body.useGravity = true;
                    runtimeBody.Body.linearVelocity = chassisVelocity;
                    runtimeBody.Body.angularVelocity = chassisAngularVelocity;
                    runtimeBody.Body.WakeUp();
                }
            }

            terminalCrashActive = true;
            activationCount++;
            Physics.SyncTransforms();
        }

        private void DeactivateTerminalCrash()
        {
            foreach (RuntimeOccupant occupant in runtimeOccupants)
            {
                foreach (RuntimeBody runtimeBody in occupant.Bodies)
                {
                    runtimeBody.Body.linearVelocity = Vector3.zero;
                    runtimeBody.Body.angularVelocity = Vector3.zero;
                    runtimeBody.Body.isKinematic = true;
                    runtimeBody.Body.useGravity = false;
                    runtimeBody.Body.detectCollisions = false;
                    runtimeBody.Collider.enabled = false;
                    runtimeBody.Joint.connectedBody = null;
                    runtimeBody.Definition.Bone.localPosition =
                        runtimeBody.LocalPosition;
                    runtimeBody.Definition.Bone.localRotation =
                        runtimeBody.LocalRotation;
                }

                RestorePoseDrivers(occupant);
            }

            terminalCrashActive = false;
            Physics.SyncTransforms();
        }

        private void BuildRuntimeBodies()
        {
            Collider[] chassisColliders = GetComponentsInChildren<Collider>(true);
            foreach (OccupantDefinition occupant in occupants)
            {
                var runtime = new RuntimeOccupant
                {
                    Definition = occupant,
                    Bodies = new RuntimeBody[occupant.Bodies.Count],
                    PoseDriverEnabledStates =
                        new bool[occupant.PoseDrivers.Count],
                };

                for (int index = 0; index < occupant.Bodies.Count; index++)
                {
                    BodyDefinition definition = occupant.Bodies[index];
                    Rigidbody body = definition.Bone.gameObject
                        .AddComponent<Rigidbody>();
                    body.mass = definition.MassKilograms;
                    body.linearDamping = 0.08f;
                    body.angularDamping = 0.22f;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    body.collisionDetectionMode =
                        CollisionDetectionMode.ContinuousSpeculative;
                    body.useGravity = false;
                    body.isKinematic = true;
                    body.detectCollisions = false;

                    Collider collider = CreateCollider(definition);
                    collider.enabled = false;
                    Joint joint = index == 0
                        ? definition.Bone.gameObject
                            .AddComponent<ConfigurableJoint>()
                        : definition.Bone.gameObject
                            .AddComponent<CharacterJoint>();
                    joint.enableCollision = false;
                    joint.enablePreprocessing = true;

                    runtime.Bodies[index] = new RuntimeBody
                    {
                        Definition = definition,
                        Body = body,
                        Collider = collider,
                        Joint = joint,
                        LocalPosition = definition.Bone.localPosition,
                        LocalRotation = definition.Bone.localRotation,
                    };

                    foreach (Collider chassisCollider in chassisColliders)
                    {
                        if (chassisCollider != null &&
                            chassisCollider != collider)
                        {
                            Physics.IgnoreCollision(
                                collider,
                                chassisCollider,
                                true);
                        }
                    }
                }

                runtimeOccupants.Add(runtime);
            }
        }

        private void ConfigureJoint(RuntimeOccupant occupant, int bodyIndex)
        {
            RuntimeBody runtimeBody = occupant.Bodies[bodyIndex];
            if (bodyIndex == 0)
            {
                var restraint = (ConfigurableJoint)runtimeBody.Joint;
                restraint.connectedBody = chassisBody;
                restraint.autoConfigureConnectedAnchor = false;
                restraint.anchor = Vector3.zero;
                restraint.connectedAnchor = chassisBody.transform
                    .InverseTransformPoint(runtimeBody.Body.position);
                restraint.xMotion = ConfigurableJointMotion.Limited;
                restraint.yMotion = ConfigurableJointMotion.Limited;
                restraint.zMotion = ConfigurableJointMotion.Limited;
                restraint.angularXMotion = ConfigurableJointMotion.Limited;
                restraint.angularYMotion = ConfigurableJointMotion.Limited;
                restraint.angularZMotion = ConfigurableJointMotion.Limited;
                restraint.linearLimit = new SoftJointLimit
                {
                    limit = seatRestraintLimitMeters,
                };
                restraint.lowAngularXLimit = new SoftJointLimit
                {
                    limit = -seatRestraintAngularLimitDegrees,
                };
                restraint.highAngularXLimit = new SoftJointLimit
                {
                    limit = seatRestraintAngularLimitDegrees,
                };
                restraint.angularYLimit = new SoftJointLimit
                {
                    limit = seatRestraintAngularLimitDegrees,
                };
                restraint.angularZLimit = new SoftJointLimit
                {
                    limit = seatRestraintAngularLimitDegrees,
                };
                restraint.projectionMode = JointProjectionMode.PositionAndRotation;
                restraint.projectionDistance = 0.08f;
                restraint.projectionAngle = 10f;
                return;
            }

            var articulation = (CharacterJoint)runtimeBody.Joint;
            articulation.connectedBody = occupant.Bodies[
                runtimeBody.Definition.ConnectedBodyIndex].Body;
            articulation.enableProjection = true;
            articulation.projectionDistance = 0.05f;
            articulation.projectionAngle = 8f;
            articulation.lowTwistLimit = new SoftJointLimit
            {
                limit = -runtimeBody.Definition.TwistLimitDegrees,
            };
            articulation.highTwistLimit = new SoftJointLimit
            {
                limit = runtimeBody.Definition.TwistLimitDegrees,
            };
            articulation.swing1Limit = new SoftJointLimit
            {
                limit = runtimeBody.Definition.SwingLimitDegrees,
            };
            articulation.swing2Limit = new SoftJointLimit
            {
                limit = runtimeBody.Definition.SwingLimitDegrees,
            };
        }

        private static Collider CreateCollider(BodyDefinition definition)
        {
            switch (definition.Shape)
            {
                case ColliderShape.Box:
                {
                    BoxCollider collider = definition.Bone.gameObject
                        .AddComponent<BoxCollider>();
                    collider.center = Vector3.zero;
                    collider.size = definition.BoxSize;
                    return collider;
                }
                case ColliderShape.Sphere:
                {
                    SphereCollider collider = definition.Bone.gameObject
                        .AddComponent<SphereCollider>();
                    collider.center = Vector3.zero;
                    collider.radius = definition.Radius;
                    return collider;
                }
                case ColliderShape.Capsule:
                {
                    Vector3 localEnd = definition.Bone.InverseTransformPoint(
                        definition.Endpoint.position);
                    Vector3 absolute = new(
                        Mathf.Abs(localEnd.x),
                        Mathf.Abs(localEnd.y),
                        Mathf.Abs(localEnd.z));
                    int direction = absolute.x >= absolute.y &&
                                    absolute.x >= absolute.z
                        ? 0
                        : absolute.y >= absolute.z ? 1 : 2;
                    float length = direction == 0
                        ? absolute.x
                        : direction == 1 ? absolute.y : absolute.z;
                    CapsuleCollider collider = definition.Bone.gameObject
                        .AddComponent<CapsuleCollider>();
                    collider.direction = direction;
                    collider.center = localEnd * 0.5f;
                    collider.radius = definition.Radius;
                    collider.height = Mathf.Max(
                        definition.Radius * 2f,
                        length);
                    return collider;
                }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static void DisablePoseDrivers(RuntimeOccupant occupant)
        {
            for (int index = 0;
                 index < occupant.Definition.PoseDrivers.Count;
                 index++)
            {
                Behaviour driver = occupant.Definition.PoseDrivers[index];
                occupant.PoseDriverEnabledStates[index] = driver.enabled;
                if (driver is Animation animation)
                {
                    animation.Sample();
                    animation.Stop();
                }
                else if (driver is Animator animator && animator.enabled)
                {
                    animator.Update(0f);
                }

                driver.enabled = false;
            }
        }

        private static void RestorePoseDrivers(RuntimeOccupant occupant)
        {
            for (int index = 0;
                 index < occupant.Definition.PoseDrivers.Count;
                 index++)
            {
                Behaviour driver = occupant.Definition.PoseDrivers[index];
                if (driver != null)
                {
                    driver.enabled =
                        occupant.PoseDriverEnabledStates[index];
                }
            }
        }

        private static bool IsFinitePositive(float value)
        {
            return float.IsFinite(value) && value > 0f;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            Rigidbody configuredChassisBody,
            IEnumerable<OccupantDefinition> configuredOccupants)
        {
            chassisBody = configuredChassisBody;
            occupants = (configuredOccupants ??
                    Enumerable.Empty<OccupantDefinition>())
                .ToArray();
        }
#endif
    }
}
