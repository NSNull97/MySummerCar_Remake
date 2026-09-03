using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Characters;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using UnityEngine;

namespace MSC.NPC
{
    /// <summary>
    /// Project-owned physical rescue presentation for the accepted BetterMSC
    /// Suski mesh. The donor rig is presentation data only; all bodies, joints,
    /// carry identity and state transitions are authored here at runtime.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SuskiRescueRagdoll : MonoBehaviour
    {
        private readonly List<Rigidbody> bodies = new(11);
        private Rigidbody primaryBody;
        private PhysicsPickupTarget pickup;
        private bool initialized;
        private bool? currentRestingState;

        public Rigidbody PrimaryBody => primaryBody;
        public bool IsInitialized => initialized;

        public void Initialize(
            LegacyCharacterPresentationBinding presentation,
            StableEntityId stableId,
            Vector3 primaryWorldPosition,
            Quaternion primaryWorldRotation,
            bool resting,
            NpcDialogueInteractionTarget dialogue)
        {
            if (initialized)
            {
                SetResting(resting);
                return;
            }

            if (presentation == null)
            {
                throw new ArgumentNullException(nameof(presentation));
            }

            if (!stableId.IsValid)
            {
                throw new ArgumentException(
                    "Suski ragdoll requires a valid stable identity.",
                    nameof(stableId));
            }

            foreach (SkinnedMeshRenderer renderer in
                     presentation.GetComponentsInChildren<
                         SkinnedMeshRenderer>(true))
            {
                renderer.updateWhenOffscreen = true;
                Bounds bounds = renderer.localBounds;
                bounds.size = new Vector3(
                    Mathf.Max(4f, bounds.size.x),
                    Mathf.Max(4f, bounds.size.y),
                    Mathf.Max(4f, bounds.size.z));
                renderer.localBounds = bounds;
            }

            CapsuleCollider legacyRootCollider =
                presentation.GetComponent<CapsuleCollider>();
            if (legacyRootCollider != null)
            {
                // Dialogue setup historically added a single body capsule.
                // It must not collide with the articulated BetterMSC rig.
                legacyRootCollider.enabled = false;
            }

            Animation animation = presentation.LegacyAnimation;
            if (animation != null)
            {
                // ApplyState has selected the accepted seated pose. Sample it
                // once, then hand every bone to PhysX.
                animation.Sample();
                animation.Stop();
                animation.enabled = false;
            }

            Transform pelvis = RequireUniqueBone("Bip01 Pelvis");
            Transform spine = RequireUniqueBone("Bip01 Spine2");
            Transform neck = RequireUniqueBone("Bip01 Neck");
            Transform head = RequireUniqueBone("Bip01 Head");
            Transform leftUpperArm = RequireUniqueBone("Bip01 L UpperArm");
            Transform leftForearm = RequireUniqueBone("Bip01 L Forearm");
            Transform leftHand = RequireUniqueBone("Bip01 L Hand");
            Transform rightUpperArm = RequireUniqueBone("Bip01 R UpperArm");
            Transform rightForearm = RequireUniqueBone("Bip01 R Forearm");
            Transform rightHand = RequireUniqueBone("Bip01 R Hand");
            Transform leftThigh = RequireUniqueBone("Bip01 L Thigh");
            Transform leftCalf = RequireUniqueBone("Bip01 L Calf");
            Transform leftFoot = RequireUniqueBone("Bip01 L Foot");
            Transform rightThigh = RequireUniqueBone("Bip01 R Thigh");
            Transform rightCalf = RequireUniqueBone("Bip01 R Calf");
            Transform rightFoot = RequireUniqueBone("Bip01 R Foot");

            primaryBody = AddBody(pelvis, 14f);
            AddBoxCollider(pelvis, new Vector3(0.34f, 0.24f, 0.28f));
            Rigidbody spineBody = AddBody(spine, 12f);
            AddCapsuleCollider(spine, neck, 0.16f);
            Rigidbody headBody = AddBody(head, 5f);
            AddSphereCollider(head, 0.135f);

            Rigidbody leftUpperArmBody = AddBody(leftUpperArm, 3f);
            AddCapsuleCollider(leftUpperArm, leftForearm, 0.075f);
            Rigidbody leftForearmBody = AddBody(leftForearm, 2f);
            AddCapsuleCollider(leftForearm, leftHand, 0.065f);
            Rigidbody rightUpperArmBody = AddBody(rightUpperArm, 3f);
            AddCapsuleCollider(rightUpperArm, rightForearm, 0.075f);
            Rigidbody rightForearmBody = AddBody(rightForearm, 2f);
            AddCapsuleCollider(rightForearm, rightHand, 0.065f);

            Rigidbody leftThighBody = AddBody(leftThigh, 7.5f);
            AddCapsuleCollider(leftThigh, leftCalf, 0.11f);
            Rigidbody leftCalfBody = AddBody(leftCalf, 4.5f);
            AddCapsuleCollider(leftCalf, leftFoot, 0.09f);
            Rigidbody rightThighBody = AddBody(rightThigh, 7.5f);
            AddCapsuleCollider(rightThigh, rightCalf, 0.11f);
            Rigidbody rightCalfBody = AddBody(rightCalf, 4.5f);
            AddCapsuleCollider(rightCalf, rightFoot, 0.09f);

            Connect(spineBody, primaryBody, 25f, 35f);
            Connect(headBody, spineBody, 25f, 30f);
            Connect(leftUpperArmBody, spineBody, 55f, 70f);
            Connect(leftForearmBody, leftUpperArmBody, 12f, 80f);
            Connect(rightUpperArmBody, spineBody, 55f, 70f);
            Connect(rightForearmBody, rightUpperArmBody, 12f, 80f);
            Connect(leftThighBody, primaryBody, 35f, 55f);
            Connect(leftCalfBody, leftThighBody, 8f, 75f);
            Connect(rightThighBody, primaryBody, 35f, 55f);
            Connect(rightCalfBody, rightThighBody, 8f, 75f);

            StableEntityIdAuthoring identity = pelvis.gameObject
                .GetComponent<StableEntityIdAuthoring>();
            if (identity == null)
            {
                identity = pelvis.gameObject
                    .AddComponent<StableEntityIdAuthoring>();
            }

            identity.InitializeExplicitRuntimeId(stableId);
            pickup = pelvis.gameObject.GetComponent<PhysicsPickupTarget>();
            if (pickup == null)
            {
                pickup = pelvis.gameObject.AddComponent<PhysicsPickupTarget>();
            }

            pickup.Configure(
                primaryBody,
                identity,
                "Перенести Суски",
                maximumMassKilograms: 80f,
                useGravityWhenLoose: true);
            InteractionTargetHost host = pelvis.gameObject
                .GetComponent<InteractionTargetHost>();
            if (host == null)
            {
                host = pelvis.gameObject.AddComponent<InteractionTargetHost>();
            }

            if (dialogue != null)
            {
                host.Configure(dialogue, pickup);
            }
            else
            {
                host.Configure(pickup);
            }

            Vector3 originalPrimaryPosition = primaryBody.position;
            Quaternion originalPrimaryRotation = primaryBody.rotation;
            Quaternion rotationDelta = primaryWorldRotation *
                Quaternion.Inverse(originalPrimaryRotation);
            foreach (Rigidbody body in bodies)
            {
                Vector3 relative = body.position - originalPrimaryPosition;
                body.position = primaryWorldPosition +
                    rotationDelta * relative;
                body.rotation = rotationDelta * body.rotation;
            }

            initialized = true;
            SetResting(resting);
        }

        public bool TryGetPrimaryPose(
            out Vector3 worldPosition,
            out Quaternion worldRotation)
        {
            if (!initialized || primaryBody == null)
            {
                worldPosition = default;
                worldRotation = Quaternion.identity;
                return false;
            }

            worldPosition = primaryBody.position;
            worldRotation = primaryBody.rotation;
            return true;
        }

        public void SetResting(bool resting)
        {
            if (!initialized)
            {
                return;
            }

            if (currentRestingState == resting)
            {
                return;
            }

            currentRestingState = resting;

            pickup?.SetPickupEnabled(!resting);
            foreach (Rigidbody body in bodies)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = !resting;
                body.isKinematic = resting;
                if (!resting)
                {
                    body.WakeUp();
                }
            }
        }

        private Rigidbody AddBody(Transform bone, float mass)
        {
            Rigidbody body = bone.gameObject.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = bone.gameObject.AddComponent<Rigidbody>();
            }

            body.mass = mass;
            body.linearDamping = 0.08f;
            body.angularDamping = 0.22f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
            body.useGravity = true;
            body.isKinematic = false;
            bodies.Add(body);
            return body;
        }

        private static void AddBoxCollider(Transform bone, Vector3 size)
        {
            BoxCollider collider = bone.gameObject.GetComponent<BoxCollider>();
            if (collider == null)
            {
                collider = bone.gameObject.AddComponent<BoxCollider>();
            }

            collider.center = Vector3.zero;
            collider.size = size;
        }

        private static void AddSphereCollider(Transform bone, float radius)
        {
            SphereCollider collider = bone.gameObject
                .GetComponent<SphereCollider>();
            if (collider == null)
            {
                collider = bone.gameObject.AddComponent<SphereCollider>();
            }

            collider.center = Vector3.zero;
            collider.radius = radius;
        }

        private static void AddCapsuleCollider(
            Transform bone,
            Transform endpoint,
            float radius)
        {
            Vector3 localEnd = bone.InverseTransformPoint(endpoint.position);
            Vector3 absolute = new(
                Mathf.Abs(localEnd.x),
                Mathf.Abs(localEnd.y),
                Mathf.Abs(localEnd.z));
            int direction = absolute.x >= absolute.y && absolute.x >= absolute.z
                ? 0
                : absolute.y >= absolute.z ? 1 : 2;
            float length = direction == 0
                ? absolute.x
                : direction == 1 ? absolute.y : absolute.z;
            CapsuleCollider collider = bone.gameObject
                .GetComponent<CapsuleCollider>();
            if (collider == null)
            {
                collider = bone.gameObject.AddComponent<CapsuleCollider>();
            }

            collider.direction = direction;
            collider.center = localEnd * 0.5f;
            collider.radius = radius;
            collider.height = Mathf.Max(radius * 2f, length);
        }

        private static void Connect(
            Rigidbody body,
            Rigidbody connectedBody,
            float twistDegrees,
            float swingDegrees)
        {
            CharacterJoint joint = body.gameObject
                .GetComponent<CharacterJoint>();
            if (joint == null)
            {
                joint = body.gameObject.AddComponent<CharacterJoint>();
            }

            joint.connectedBody = connectedBody;
            joint.enableProjection = true;
            joint.projectionDistance = 0.05f;
            joint.projectionAngle = 8f;
            joint.lowTwistLimit = new SoftJointLimit { limit = -twistDegrees };
            joint.highTwistLimit = new SoftJointLimit { limit = twistDegrees };
            joint.swing1Limit = new SoftJointLimit { limit = swingDegrees };
            joint.swing2Limit = new SoftJointLimit { limit = swingDegrees };
        }

        private Transform RequireUniqueBone(string boneName)
        {
            Transform[] matches = GetComponentsInChildren<Transform>(true)
                .Where(candidate => string.Equals(
                    candidate.name,
                    boneName,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
            {
                throw new InvalidOperationException(
                    $"BetterMSC Suski ragdoll expected one bone '{boneName}', " +
                    $"found {matches.Length}.");
            }

            return matches[0];
        }
    }
}
