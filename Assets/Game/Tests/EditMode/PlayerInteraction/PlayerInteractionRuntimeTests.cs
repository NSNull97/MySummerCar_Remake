using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MSC.Core.Identity;
using MSC.Editor.PlayerInteraction;
using MSC.Interaction;
using MSC.Interaction.Architecture;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Notifications;
using MSC.Interaction.Prototype;
using MSC.Interaction.Query;
using MSC.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Utils;

namespace MSC.Tests.EditMode.PlayerInteraction
{
    public sealed class PlayerInteractionRuntimeTests
    {
        private GameObject owner;
        private GameObject item;
        private PhysicalCarryController carryController;
        private PhysicsPickupTarget pickupTarget;
        private InteractionContext context;

        [SetUp]
        public void SetUp()
        {
            owner = new GameObject("TestInteractor");
            BoxCollider ownerCollider = owner.AddComponent<BoxCollider>();
            GameObject anchor = new GameObject("CarryAnchor");
            anchor.transform.SetParent(owner.transform, false);
            anchor.transform.localPosition = Vector3.forward;

            carryController = owner.AddComponent<PhysicalCarryController>();
            carryController.Configure(anchor.transform, ownerCollider);
            context = new InteractionContext(owner, owner.transform.position, Vector3.forward);

            item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = "TestPickup";
            Rigidbody body = item.AddComponent<Rigidbody>();
            body.mass = 4f;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            StableEntityIdAuthoring identity = item.AddComponent<StableEntityIdAuthoring>();
            SetStableId(identity, "db3f0cf92afd4c4c84b144592220cd21");
            pickupTarget = item.AddComponent<PhysicsPickupTarget>();
            pickupTarget.Configure(body, identity, "Поднять", 35f);
        }

        [TearDown]
        public void TearDown()
        {
            if (item != null)
            {
                Object.DestroyImmediate(item);
            }

            if (owner != null)
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void AuthoredPlayerPrefab_UsesDonorCapsuleAndSeparatedViewPivots()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PlayerInteractionPrototypePaths.PlayerPrefab);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(LayerMask.NameToLayer("Player"), Is.EqualTo(9));
            Assert.That(prefab.layer, Is.EqualTo(9));

            CharacterController controller = prefab.GetComponent<CharacterController>();
            FirstPersonMotor motor = prefab.GetComponent<FirstPersonMotor>();
            PlayerLeanImpactFeedbackPresenter impactFeedback =
                prefab.GetComponent<PlayerLeanImpactFeedbackPresenter>();
            FirstPersonLook look = prefab.GetComponent<FirstPersonLook>();
            Transform leanPivot = prefab.transform.Find("LeanPivot");
            Transform cameraPivot = leanPivot?.Find("CameraPivot");
            Transform impactPivot = cameraPivot?.Find("ImpactPivot");
            Transform motionPivot = impactPivot?.Find("MotionPivot");
            Transform lookPivot = motionPivot?.Find("LookPitchPivot");
            FirstPersonCameraMotion cameraMotion =
                motionPivot?.GetComponent<FirstPersonCameraMotion>();
            Camera camera = lookPivot?.GetComponentInChildren<Camera>(true);
            FirstPersonCameraFieldOfView fieldOfView =
                camera?.GetComponent<FirstPersonCameraFieldOfView>();
            PhysicalCarryController carry =
                prefab.GetComponent<PhysicalCarryController>();
            PlayerInputRouter inputRouter =
                prefab.GetComponent<PlayerInputRouter>();
            Transform carryAnchor = camera?.transform.Find("CarryAnchor");

            Assert.That(controller, Is.Not.Null);
            Assert.That(motor, Is.Not.Null);
            Assert.That(impactFeedback, Is.Not.Null);
            Assert.That(look, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            Assert.That(fieldOfView, Is.Not.Null);
            Assert.That(leanPivot, Is.Not.Null);
            Assert.That(cameraPivot, Is.Not.Null);
            Assert.That(impactPivot, Is.Not.Null);
            Assert.That(motionPivot, Is.Not.Null);
            Assert.That(cameraMotion, Is.Not.Null);
            Assert.That(lookPivot, Is.Not.Null);
            Assert.That(carry, Is.Not.Null);
            Assert.That(inputRouter, Is.Not.Null);
            Assert.That(carryAnchor, Is.Not.Null);
            Assert.That(controller.radius, Is.EqualTo(0.12f).Within(0.001f));
            Assert.That(controller.height, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(controller.center.y, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(controller.stepOffset, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(controller.slopeLimit, Is.EqualTo(90f).Within(0.001f));
            Assert.That(
                leanPivot.localPosition.y,
                Is.EqualTo(-0.3f).Within(0.001f));
            Assert.That(
                cameraPivot.localPosition.y,
                Is.EqualTo(1.78f).Within(0.001f));
            Assert.That(
                carryAnchor.localPosition.x,
                Is.EqualTo(0f).Within(0.001f));
            Assert.That(
                carryAnchor.localPosition.y,
                Is.EqualTo(-0.1f).Within(0.001f));
            Assert.That(
                carryAnchor.localPosition.z,
                Is.EqualTo(0.82f).Within(0.001f));

            var motorObject = new SerializedObject(motor);
            var lookObject = new SerializedObject(look);
            var cameraMotionObject = new SerializedObject(cameraMotion);
            var fieldOfViewObject = new SerializedObject(fieldOfView);
            var carryObject = new SerializedObject(carry);
            var inputRouterObject = new SerializedObject(inputRouter);
            Assert.That(
                motorObject.FindProperty("leanPivot").objectReferenceValue,
                Is.SameAs(leanPivot));
            Assert.That(
                motorObject.FindProperty("cameraPivot").objectReferenceValue,
                Is.SameAs(cameraPivot));
            Assert.That(
                motorObject.FindProperty("impactPivot").objectReferenceValue,
                Is.SameAs(impactPivot));
            Assert.That(
                motorObject.FindProperty(
                    "forwardLeanAngularSpeedDegreesPerSecond").floatValue,
                Is.EqualTo(150f).Within(0.001f));
            Assert.That(
                motorObject.FindProperty(
                    "forwardLeanReturnAngularSpeedDegreesPerSecond").floatValue,
                Is.EqualTo(120f).Within(0.001f));
            Assert.That(
                lookObject.FindProperty("pitchPivot").objectReferenceValue,
                Is.SameAs(lookPivot));
            Assert.That(
                lookObject.FindProperty(
                    "mouseSensitivityDegreesPerPixel").floatValue,
                Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(
                lookObject.FindProperty("pitchLimitDegrees").floatValue,
                Is.EqualTo(80f).Within(0.001f));
            Assert.That(
                fieldOfViewObject.FindProperty(
                    "horizontalFieldOfViewDegrees").floatValue,
                Is.EqualTo(120f).Within(0.001f));
            Assert.That(
                camera.fieldOfView,
                Is.EqualTo(
                    FirstPersonCameraFieldOfView.HorizontalToVerticalDegrees(
                        120f,
                        camera.aspect)).Within(0.001f));
            Assert.That(
                motorObject.FindProperty(
                    "minimumRunningEntrySpeedMetersPerSecond").floatValue,
                Is.EqualTo(1.7f).Within(0.001f));
            Assert.That(
                motorObject.FindProperty(
                    "standingEyeHeightMeters").floatValue,
                Is.EqualTo(1.48f).Within(0.001f));
            Assert.That(
                motorObject.FindProperty(
                    "crouchingEyeHeightMeters").floatValue,
                Is.EqualTo(0.93f).Within(0.001f));
            Assert.That(
                motorObject.FindProperty(
                    "deepCrouchingEyeHeightMeters").floatValue,
                Is.EqualTo(0.38f).Within(0.001f));
            Assert.That(
                motorObject.FindProperty(
                    "postureRiseTransitionMetersPerSecond").floatValue,
                Is.EqualTo(6f).Within(0.001f));
            Assert.That(
                motorObject.FindProperty("coyoteTimeSeconds").floatValue,
                Is.EqualTo(0.1f).Within(0.001f));
            Assert.That(
                motorObject.FindProperty(
                    "jumpForceMetersPerSecond").floatValue,
                Is.EqualTo(6.65f).Within(0.001f));
            Assert.That(
                motorObject.FindProperty("bodyMassKilograms").floatValue,
                Is.EqualTo(83f).Within(0.001f));
            Assert.That(
                motorObject.FindProperty("supportLoadScale").floatValue,
                Is.EqualTo(1f).Within(0.001f));
            Assert.That(
                motorObject.FindProperty("jumpPreparationSeconds").floatValue,
                Is.EqualTo(0.085f).Within(0.001f));
            Assert.That(
                cameraMotionObject.FindProperty(
                    "jumpPreparationDipMeters").floatValue,
                Is.EqualTo(0.07f).Within(0.001f));
            Assert.That(
                cameraMotionObject.FindProperty(
                    "jumpForceLandingInfluence").floatValue,
                Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(
                cameraMotionObject.FindProperty(
                    "maximumLandingDipMeters").floatValue,
                Is.EqualTo(0.14f).Within(0.001f));
            Assert.That(
                cameraMotionObject.FindProperty(
                    "maximumLandingPitchDegrees").floatValue,
                Is.EqualTo(3f).Within(0.001f));
            Assert.That(
                carryObject.FindProperty("followAcceleration").floatValue,
                Is.EqualTo(110f).Within(0.001f));
            Assert.That(
                carryObject.FindProperty("followVelocityRetention").floatValue,
                Is.EqualTo(0.82f).Within(0.001f));
            Assert.That(
                carryObject.FindProperty("maximumFollowSpeed").floatValue,
                Is.EqualTo(18f).Within(0.001f));
            Assert.That(
                carryObject.FindProperty("ownerMotionInheritance").floatValue,
                Is.EqualTo(0.96f).Within(0.001f));
            Assert.That(
                carryObject.FindProperty(
                    "maximumInheritedOwnerSpeed").floatValue,
                Is.EqualTo(12f).Within(0.001f));
            Assert.That(
                carryObject.FindProperty("minimumHoldDistance").floatValue,
                Is.EqualTo(0.32f).Within(0.001f));
            Assert.That(
                inputRouterObject.FindProperty(
                    "heldRotationDegreesPerNotch").floatValue,
                Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void CameraSettingsSink_AppliesHorizontalFovAndFarClip()
        {
            var cameraObject = new GameObject("CameraSettingsSinkTest");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.nearClipPlane = 0.03f;
                var fieldOfView =
                    cameraObject.AddComponent<FirstPersonCameraFieldOfView>();
                fieldOfView.Configure(camera, 120f, 0.5f, 0f);

                fieldOfView.ApplyCameraSettings(100f, 1800f);

                Assert.That(
                    fieldOfView.HorizontalFieldOfViewDegrees,
                    Is.EqualTo(100f).Within(0.001f));
                Assert.That(
                    fieldOfView.FarClipPlaneMeters,
                    Is.EqualTo(1800f).Within(0.001f));
                Assert.That(
                    camera.fieldOfView,
                    Is.EqualTo(
                        FirstPersonCameraFieldOfView.HorizontalToVerticalDegrees(
                            100f,
                            camera.aspect)).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void CcrCarrySpring_AcceleratesDirectlyAndRespectsSpeedCap()
        {
            Vector3 accelerated =
                PhysicalCarryController.CalculateFollowVelocity(
                    Vector3.zero,
                    Vector3.forward,
                    springForce: 110f,
                    velocityRetention: 0.82f,
                    maximumSpeed: 18f,
                    fixedDeltaTime: 0.02f);
            Assert.That(
                accelerated.z,
                Is.EqualTo(1.804f).Within(0.001f));

            Vector3 capped =
                PhysicalCarryController.CalculateFollowVelocity(
                    Vector3.forward * 50f,
                    Vector3.forward * 10f,
                    springForce: 110f,
                    velocityRetention: 0.82f,
                    maximumSpeed: 18f,
                    fixedDeltaTime: 0.02f);
            Assert.That(capped.magnitude, Is.EqualTo(18f).Within(0.001f));
        }

        [Test]
        public void CarryMotionCompensation_RemovesAlmostAllOwnerStopInertia()
        {
            Vector3 compensated =
                PhysicalCarryController.CalculateMotionCompensatedFollowVelocity(
                    currentBodyVelocity: Vector3.forward * 6f,
                    previousInheritedVelocity: Vector3.forward * 5.76f,
                    currentInheritedVelocity: Vector3.zero,
                    targetOffset: Vector3.zero,
                    springForce: 110f,
                    velocityRetention: 0.82f,
                    maximumRelativeSpeed: 18f,
                    fixedDeltaTime: 0.02f);

            Assert.That(
                compensated.z,
                Is.EqualTo(0.1968f).Within(0.001f));
            Assert.That(
                compensated.magnitude,
                Is.LessThan(6f * 0.04f));
        }

        [TestCase(0.1f, 0.32f)]
        [TestCase(0.48f, 0.48f)]
        [TestCase(1.7f, 0.82f)]
        public void AdaptiveCarryDistance_KeepsNearGrabAndCapsFarGrab(
            float selectedDistance,
            float expectedDistance)
        {
            Assert.That(
                PhysicalCarryController.CalculateAdaptiveHoldDistance(
                    selectedDistance,
                    minimumDistance: 0.32f,
                    authoredMaximumDistance: 0.82f),
                Is.EqualTo(expectedDistance).Within(0.001f));
        }

        [Test]
        public void OwnerTeleport_RealignsHeldBodyWithoutDroppingIt()
        {
            Assert.That(carryController.TryPickup(pickupTarget, context), Is.True);
            owner.transform.position = new Vector3(120f, 8f, -75f);

            Assert.That(carryController.SynchronizeAfterOwnerTeleport(), Is.True);
            Assert.That(carryController.HasHeldObject, Is.True);
            Assert.That(
                carryController.HeldBody.position,
                Is.EqualTo(owner.transform.TransformPoint(Vector3.forward))
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(
                carryController.HeldBody.linearVelocity,
                Is.EqualTo(Vector3.zero)
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void CcrMovementTuning_UsesDirectionalSpeedsAndBoundedRunEntry()
        {
            Assert.That(
                FirstPersonMovementMath.ResolveDirectionalSpeed(
                    Vector2.up,
                    2.9f,
                    2.5f,
                    2.6f),
                Is.EqualTo(2.9f).Within(0.001f));
            Assert.That(
                FirstPersonMovementMath.ResolveDirectionalSpeed(
                    Vector2.down,
                    2.9f,
                    2.5f,
                    2.6f),
                Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(
                FirstPersonMovementMath.ResolveDirectionalSpeed(
                    Vector2.right,
                    2.9f,
                    2.5f,
                    2.6f),
                Is.EqualTo(2.6f).Within(0.001f));

            Vector3 accelerated =
                FirstPersonMovementMath.ApplyPerFrameResponse(
                    Vector3.zero,
                    Vector3.forward * 2.9f,
                    responsePerSecond: 8f,
                    deltaTime: 1f / 60f);
            Assert.That(
                accelerated.z,
                Is.EqualTo(2.9f * 8f / 60f).Within(0.001f));

            Assert.That(
                FirstPersonMovementMath.CanEnterRun(
                    Vector2.up,
                    horizontalSpeed: 1.7f,
                    minimumEntrySpeed: 1.7f,
                    maximumStrafeInput: 0.9f),
                Is.False);
            Assert.That(
                FirstPersonMovementMath.CanEnterRun(
                    Vector2.up,
                    horizontalSpeed: 1.71f,
                    minimumEntrySpeed: 1.7f,
                    maximumStrafeInput: 0.9f),
                Is.True);
            Assert.That(
                FirstPersonMovementMath.CanEnterRun(
                    Vector2.left,
                    horizontalSpeed: 6f,
                    minimumEntrySpeed: 1.7f,
                    maximumStrafeInput: 0.9f),
                Is.False);
            Assert.That(
                FirstPersonMovementMath.CanEnterRun(
                    Vector2.down,
                    horizontalSpeed: 6f,
                    minimumEntrySpeed: 1.7f,
                    maximumStrafeInput: 0.9f),
                Is.False);
        }

        [Test]
        public void CoyoteWindow_AllowsOneLateJumpInsideOneTenthSecond()
        {
            Assert.That(
                FirstPersonMovementMath.CanUseJump(
                    grounded: false,
                    jumpAvailableSinceGroundContact: true,
                    secondsSinceGrounded: 0.099f,
                    coyoteTimeSeconds: 0.1f),
                Is.True);
            Assert.That(
                FirstPersonMovementMath.CanUseJump(
                    grounded: false,
                    jumpAvailableSinceGroundContact: true,
                    secondsSinceGrounded: 0.101f,
                    coyoteTimeSeconds: 0.1f),
                Is.False);
            Assert.That(
                FirstPersonMovementMath.CanUseJump(
                    grounded: false,
                    jumpAvailableSinceGroundContact: false,
                    secondsSinceGrounded: 0.05f,
                    coyoteTimeSeconds: 0.1f),
                Is.False);
        }

        [Test]
        public void LandingStrength_IncreasesWithConfiguredJumpForce()
        {
            float fallOnly = FirstPersonCameraMotion.CalculateLandingStrength(
                impactSpeedMetersPerSecond: 6.65f,
                jumpForceMetersPerSecond: 0f,
                minimumLandingSpeedMetersPerSecond: 4f,
                maximumLandingSpeedMetersPerSecond: 11f,
                minimumJumpForceMetersPerSecond: 4f,
                maximumJumpForceMetersPerSecond: 9f,
                jumpForceInfluence: 0.35f);
            float ordinaryJump =
                FirstPersonCameraMotion.CalculateLandingStrength(
                    impactSpeedMetersPerSecond: 6.65f,
                    jumpForceMetersPerSecond: 6.65f,
                    minimumLandingSpeedMetersPerSecond: 4f,
                    maximumLandingSpeedMetersPerSecond: 11f,
                    minimumJumpForceMetersPerSecond: 4f,
                    maximumJumpForceMetersPerSecond: 9f,
                    jumpForceInfluence: 0.35f);
            float strongJump =
                FirstPersonCameraMotion.CalculateLandingStrength(
                    impactSpeedMetersPerSecond: 6.65f,
                    jumpForceMetersPerSecond: 9f,
                    minimumLandingSpeedMetersPerSecond: 4f,
                    maximumLandingSpeedMetersPerSecond: 11f,
                    minimumJumpForceMetersPerSecond: 4f,
                    maximumJumpForceMetersPerSecond: 9f,
                    jumpForceInfluence: 0.35f);

            Assert.That(ordinaryJump, Is.GreaterThan(fallOnly));
            Assert.That(strongJump, Is.GreaterThan(ordinaryJump));
            Assert.That(strongJump, Is.LessThanOrEqualTo(1f));
        }

        [Test]
        public void MotorPhysicalTuning_RejectsInvalidMassAndJumpForce()
        {
            var player = new GameObject("PhysicalPlayerTuning");
            try
            {
                CharacterController controller =
                    player.AddComponent<CharacterController>();
                FirstPersonMotor motor = player.AddComponent<FirstPersonMotor>();
                motor.Configure(controller, player.transform);

                Assert.That(motor.BodyMassKilograms, Is.EqualTo(83f));
                Assert.That(motor.JumpForceMetersPerSecond, Is.EqualTo(6.65f));
                Assert.That(motor.TrySetBodyMassKilograms(96f), Is.True);
                Assert.That(motor.BodyMassKilograms, Is.EqualTo(96f));
                Assert.That(motor.TrySetBodyMassKilograms(float.NaN), Is.False);
                Assert.That(motor.TrySetBodyMassKilograms(0f), Is.False);
                Assert.That(motor.TrySetJumpForceMetersPerSecond(8f), Is.True);
                Assert.That(motor.JumpForceMetersPerSecond, Is.EqualTo(8f));
                Assert.That(
                    motor.TrySetJumpForceMetersPerSecond(float.PositiveInfinity),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void PickupAndDropRestoresPhysicalState()
        {
            Rigidbody body = pickupTarget.Body;

            Assert.That(carryController.TryPickup(pickupTarget, context), Is.True);
            Assert.That(carryController.HasHeldObject, Is.True);
            Assert.That(pickupTarget.IsCarried, Is.True);
            Assert.That(body.useGravity, Is.False);

            Assert.That(carryController.Drop(), Is.True);
            Assert.That(carryController.HasHeldObject, Is.False);
            Assert.That(pickupTarget.IsCarried, Is.False);
            Assert.That(body.useGravity, Is.True);
            Assert.That(body.collisionDetectionMode, Is.EqualTo(CollisionDetectionMode.Continuous));
        }

        [Test]
        public void PresentationAnchorHardSnapsAndRestoresCarryPhysics()
        {
            Rigidbody body = pickupTarget.Body;
            Transform originalParent = body.transform.parent;
            var gripObject = new GameObject("AnimatedBottleGrip");
            gripObject.transform.SetPositionAndRotation(
                new Vector3(2.5f, 1.75f, -0.4f),
                Quaternion.Euler(25f, 40f, -12f));

            try
            {
                Assert.That(
                    carryController.TryPickup(pickupTarget, context),
                    Is.True);

                carryController.SetHeldPresentationAnchor(
                    gripObject.transform,
                    1f);

                Assert.That(body.transform.parent, Is.SameAs(originalParent));
                Assert.That(body.isKinematic, Is.True);
                Assert.That(body.detectCollisions, Is.False);
                Assert.That(
                    Vector3.Distance(
                        body.transform.position,
                        gripObject.transform.position),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        body.transform.rotation,
                        gripObject.transform.rotation),
                    Is.LessThan(0.01f));

                gripObject.transform.SetPositionAndRotation(
                    new Vector3(-1.2f, 2.1f, 3.4f),
                    Quaternion.Euler(-15f, 120f, 8f));
                carryController.SetHeldPresentationAnchor(
                    gripObject.transform,
                    1f);

                Assert.That(
                    Vector3.Distance(
                        body.transform.position,
                        gripObject.transform.position),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        body.transform.rotation,
                        gripObject.transform.rotation),
                    Is.LessThan(0.01f));

                carryController.ClearHeldPresentationPose();

                Assert.That(body.isKinematic, Is.False);
                Assert.That(body.detectCollisions, Is.True);
                Assert.That(body.useGravity, Is.False);
                Assert.That(
                    body.interpolation,
                    Is.EqualTo(RigidbodyInterpolation.Interpolate));

                Assert.That(carryController.Drop(), Is.True);
                Assert.That(body.isKinematic, Is.False);
                Assert.That(body.detectCollisions, Is.True);
                Assert.That(body.useGravity, Is.True);
                Assert.That(
                    body.collisionDetectionMode,
                    Is.EqualTo(CollisionDetectionMode.Continuous));
            }
            finally
            {
                Object.DestroyImmediate(gripObject);
            }
        }

        [Test]
        public void CandidateBecomesInvalidWhenTargetIsDestroyed()
        {
            var capability = item.AddComponent<ContextToggleTarget>();
            InteractionTargetHost host = item.AddComponent<InteractionTargetHost>();
            host.Configure(capability);
            var candidate = new InteractionCandidate(host, Vector3.zero, Vector3.up, 1f);

            Assert.That(candidate.IsValid, Is.True);
            Object.DestroyImmediate(item);
            item = null;

            Assert.That(candidate.IsValid, Is.False);
            Assert.That(candidate.TryGetCapability<IContextInteractionTarget>(out _), Is.False);
        }

        [Test]
        public void RaycastQueryRejectsColliderWithoutCapabilityHost()
        {
            item.transform.position = new Vector3(0f, 0f, 1f);
            Physics.SyncTransforms();
            RaycastInteractionCandidateSource query =
                owner.AddComponent<RaycastInteractionCandidateSource>();
            query.Configure(owner.transform, 2f, ~0);

            InteractionCandidate candidate = query.Query();

            Assert.That(query.HasLastHit, Is.True);
            Assert.That(candidate.IsValid, Is.False);
        }

        [Test]
        public void CarriedObjectCanCrossMountHandoffBoundary()
        {
            Assert.That(carryController.TryPickup(pickupTarget, context), Is.True);
            var mountObject = new GameObject("Mount");
            var poseObject = new GameObject("Pose");
            poseObject.transform.SetParent(mountObject.transform, false);
            poseObject.transform.position = new Vector3(2f, 1f, 3f);
            PrototypeMountHandoffTarget mount = mountObject.AddComponent<PrototypeMountHandoffTarget>();
            mount.Configure(poseObject.transform);

            try
            {
                Assert.That(carryController.TryHandoff(mount, context), Is.True);
                Assert.That(carryController.HasHeldObject, Is.False);
                Assert.That(mount.HasMountedTarget, Is.True);
                Assert.That(pickupTarget.Body.isKinematic, Is.True);
                Assert.That(pickupTarget.Body.position, Is.EqualTo(poseObject.transform.position));
            }
            finally
            {
                Object.DestroyImmediate(mountObject);
            }
        }

        [Test]
        public void CarriedObjectSnapshotUsesStableIdentityAndRoundTripsThroughJson()
        {
            Assert.That(carryController.TryPickup(pickupTarget, context), Is.True);

            CarriedObjectSaveState captured = carryController.CaptureSaveState();
            string json = JsonUtility.ToJson(captured);
            CarriedObjectSaveState restored = JsonUtility.FromJson<CarriedObjectSaveState>(json);

            Assert.That(captured.HasCarriedObject, Is.True);
            Assert.That(restored.IsValid, Is.True);
            Assert.That(restored.SchemaVersion, Is.EqualTo(CarriedObjectSaveState.CurrentSchemaVersion));
            Assert.That(restored.StableEntityId, Is.EqualTo(pickupTarget.StableId.Value));
        }

        [Test]
        public void CarriedObjectRestoreUsesExplicitResolvedTarget()
        {
            CarriedObjectSaveState state = CarriedObjectSaveState.Create(
                pickupTarget.StableId,
                new Vector3(0.1f, -0.2f, 0.3f),
                Quaternion.Euler(5f, 20f, 0f));

            Assert.That(
                carryController.TryRestoreSaveState(
                    state,
                    pickupTarget,
                    context,
                    out string failure),
                Is.True,
                failure);
            Assert.That(carryController.HeldStableId, Is.EqualTo(state.StableEntityId));
            Assert.That(pickupTarget.Body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(pickupTarget.Body.angularVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void InvalidCarriedObjectPoseDoesNotMutateController()
        {
            const string json =
                "{\"schemaVersion\":1,\"hasCarriedObject\":true," +
                "\"stableEntityId\":\"db3f0cf92afd4c4c84b144592220cd21\"," +
                "\"anchorLocalPosition\":{\"x\":0,\"y\":0,\"z\":0}," +
                "\"anchorLocalRotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":0}}";
            CarriedObjectSaveState state =
                JsonUtility.FromJson<CarriedObjectSaveState>(json);

            Assert.That(state.TryValidate(out _), Is.False);
            Assert.That(
                carryController.TryRestoreSaveState(
                    state,
                    pickupTarget,
                    context,
                    out _),
                Is.False);
            Assert.That(carryController.HasHeldObject, Is.False);
            Assert.That(pickupTarget.IsCarried, Is.False);
        }

        [Test]
        public void PlayerStateRoundTripRestoresPoseLookAndStance()
        {
            GameObject player = new GameObject("PersistentPlayer");
            CharacterController controller = player.AddComponent<CharacterController>();
            GameObject pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(player.transform, false);
            FirstPersonMotor motor = player.AddComponent<FirstPersonMotor>();
            motor.Configure(controller, pivot.transform);
            FirstPersonLook look = player.AddComponent<FirstPersonLook>();
            look.Configure(player.transform, pivot.transform, shouldLockCursor: false);

            try
            {
                PlayerSaveDto expected = PlayerSaveDto.Create(
                    new Vector3(12f, 3f, -8f),
                    Quaternion.Euler(0f, 135f, 0f),
                    FirstPersonMotorSaveDto.Create(true, -1.5f),
                    FirstPersonLookSaveDto.Create(24f));
                string json = JsonUtility.ToJson(expected);
                PlayerSaveDto restored = JsonUtility.FromJson<PlayerSaveDto>(json);

                Assert.That(
                    PlayerPersistence.TryRestore(
                        restored,
                        motor,
                        look,
                        out string failure),
                    Is.True,
                    failure);
                Assert.That(player.transform.position, Is.EqualTo(expected.WorldPosition));
                Assert.That(
                    Quaternion.Angle(player.transform.rotation, expected.WorldRotation),
                    Is.LessThan(0.01f));
                Assert.That(motor.IsCrouching, Is.True);
                Assert.That(look.PitchDegrees, Is.EqualTo(24f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void RestrictedInterior_DropsOnePostureLevelAndRestoresEntryPosture()
        {
            GameObject player = new GameObject("RestrictedInteriorPlayer");
            CharacterController controller =
                player.AddComponent<CharacterController>();
            GameObject pivot = new GameObject("CameraPivot");
            pivot.transform.SetParent(player.transform, false);
            FirstPersonMotor motor = player.AddComponent<FirstPersonMotor>();
            motor.Configure(controller, pivot.transform);

            GameObject volumeObject = new GameObject("RestrictedInterior");
            BoxCollider trigger = volumeObject.AddComponent<BoxCollider>();
            RestrictedInteriorPostureVolume volume =
                volumeObject.AddComponent<RestrictedInteriorPostureVolume>();
            volume.Configure(trigger);

            try
            {
                Assert.That(volume.TryEnter(controller), Is.True);
                Assert.That(motor.IsInsideRestrictedInterior, Is.True);
                Assert.That(motor.Posture, Is.EqualTo(PlayerPosture.Crouch));
                Assert.That(
                    motor.TrySetPosture(PlayerPosture.Standing),
                    Is.False);

                motor.CyclePosture();
                Assert.That(motor.Posture, Is.EqualTo(PlayerPosture.DeepCrouch));
                motor.CyclePosture();
                Assert.That(motor.Posture, Is.EqualTo(PlayerPosture.Crouch));

                Assert.That(volume.TryExit(controller), Is.True);
                Assert.That(motor.IsInsideRestrictedInterior, Is.False);
                Assert.That(motor.Posture, Is.EqualTo(PlayerPosture.Standing));

                Assert.That(
                    motor.TrySetPosture(PlayerPosture.Crouch),
                    Is.True);
                Assert.That(volume.TryEnter(controller), Is.True);
                Assert.That(motor.Posture, Is.EqualTo(PlayerPosture.DeepCrouch));
                Assert.That(volume.TryExit(controller), Is.True);
                Assert.That(motor.Posture, Is.EqualTo(PlayerPosture.Crouch));
            }
            finally
            {
                Object.DestroyImmediate(volumeObject);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void ToolTargetActivatesOnlyThroughExplicitCapability()
        {
            ToolActivationCounterTarget toolTarget = item.AddComponent<ToolActivationCounterTarget>();

            Assert.That(toolTarget.CanActivateTool(context), Is.True);
            toolTarget.ActivateTool(context);

            Assert.That(toolTarget.ActivationCount, Is.EqualTo(1));
        }

        [Test]
        public void SecondaryWorldTarget_UsesRmbAndDoesNotFallThroughToUse()
        {
            GameObject viewpoint = new GameObject("Viewpoint");
            viewpoint.transform.SetParent(owner.transform, false);
            item.transform.position = Vector3.forward * 2f;
            item.GetComponent<Rigidbody>().isKinematic = true;
            var secondary = item.AddComponent<SecondaryInteractionCounterTarget>();
            InteractionTargetHost host = item.AddComponent<InteractionTargetHost>();
            host.Configure(pickupTarget, secondary);
            RaycastInteractionCandidateSource source =
                owner.AddComponent<RaycastInteractionCandidateSource>();
            source.Configure(viewpoint.transform, 3f, ~0);
            PlayerInteractionController interaction =
                owner.AddComponent<PlayerInteractionController>();
            interaction.Configure(source, carryController, viewpoint.transform, ~0);

            Physics.SyncTransforms();
            interaction.RefreshCandidate();

            Assert.That(interaction.CurrentPromptBindingLabel, Is.EqualTo("ПКМ"));
            Assert.That(interaction.TryPickupOrPlace(), Is.False);
            Assert.That(interaction.TryToolActivation(), Is.False);
            Assert.That(secondary.InteractionCount, Is.Zero);
            Assert.That(interaction.TryBeginSecondaryInteraction(), Is.True);
            Assert.That(secondary.InteractionCount, Is.EqualTo(1));
        }

        [Test]
        public void RaycastQuery_SelectsExplicitTargetWhenOriginIsInsideItsTrigger()
        {
            GameObject viewpoint = new GameObject("OriginOverlapViewpoint");
            viewpoint.transform.SetParent(owner.transform, false);
            GameObject targetObject = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            targetObject.name = "Compact installed-part proxy";
            targetObject.transform.position = viewpoint.transform.position;
            targetObject.transform.localScale = Vector3.one * 0.4f;
            targetObject.GetComponent<Collider>().isTrigger = true;
            OriginOverlapCounterTarget target = targetObject
                .AddComponent<OriginOverlapCounterTarget>();
            InteractionTargetHost host = targetObject
                .AddComponent<InteractionTargetHost>();
            host.Configure(target);

            try
            {
                RaycastInteractionCandidateSource source = owner
                    .AddComponent<RaycastInteractionCandidateSource>();
                source.Configure(viewpoint.transform, 3f, ~0);
                Physics.SyncTransforms();

                InteractionCandidate candidate = source.Query();

                Assert.That(candidate.IsValid, Is.True);
                Assert.That(candidate.Host, Is.SameAs(host));
                Assert.That(candidate.SourceCollider, Is.SameAs(
                    targetObject.GetComponent<Collider>()));
                Assert.That(candidate.Distance, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(targetObject);
                Object.DestroyImmediate(viewpoint);
            }
        }

        [Test]
        public void RaycastQuery_CarryOnlySocketCannotHideWorldPartWhenHandsAreEmpty()
        {
            GameObject viewpoint = new GameObject("CarryOnlyViewpoint");
            viewpoint.transform.SetParent(owner.transform, false);
            GameObject socketObject = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            socketObject.transform.position = Vector3.forward;
            socketObject.transform.localScale = Vector3.one * 0.1f;
            socketObject.GetComponent<Collider>().isTrigger = true;
            CarriedObjectOnlyCounterTarget target = socketObject
                .AddComponent<CarriedObjectOnlyCounterTarget>();
            InteractionTargetHost host = socketObject
                .AddComponent<InteractionTargetHost>();
            host.Configure(target);

            try
            {
                RaycastInteractionCandidateSource source = owner
                    .AddComponent<RaycastInteractionCandidateSource>();
                source.Configure(viewpoint.transform, 2f, ~0);
                Physics.SyncTransforms();

                Assert.That(source.Query().IsValid, Is.False);
                source.SetCarriedObjectTargetsEnabled(true);
                Assert.That(source.Query().Host, Is.SameAs(host));
            }
            finally
            {
                Object.DestroyImmediate(socketObject);
                Object.DestroyImmediate(viewpoint);
            }
        }

        [Test]
        public void RaycastQuery_IncompatibleHighPriorityCarrySocketCannotMaskCompatibleSocket()
        {
            GameObject viewpoint = new GameObject(
                "Carried-part compatibility viewpoint");
            viewpoint.transform.SetParent(owner.transform, false);
            item.transform.position = Vector3.left * 10f;

            GameObject incompatibleSocket = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            incompatibleSocket.name = "Closer incompatible mount";
            incompatibleSocket.transform.position = Vector3.forward;
            incompatibleSocket.transform.localScale = Vector3.one * 0.1f;
            incompatibleSocket.GetComponent<Collider>().isTrigger = true;
            CarriedObjectFilterCounterTarget incompatibleTarget =
                incompatibleSocket.AddComponent<
                    CarriedObjectFilterCounterTarget>();
            incompatibleTarget.AcceptsCarriedObject = false;
            InteractionTargetHost incompatibleHost = incompatibleSocket
                .AddComponent<InteractionTargetHost>();
            incompatibleHost.Configure(incompatibleTarget);
            incompatibleHost.ConfigureSelectionPriority(100);

            GameObject compatibleSocket = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            compatibleSocket.name = "Compatible mount behind it";
            compatibleSocket.transform.position = Vector3.forward * 1.25f;
            compatibleSocket.transform.localScale = Vector3.one * 0.1f;
            compatibleSocket.GetComponent<Collider>().isTrigger = true;
            CarriedObjectFilterCounterTarget compatibleTarget =
                compatibleSocket.AddComponent<
                    CarriedObjectFilterCounterTarget>();
            compatibleTarget.AcceptsCarriedObject = true;
            InteractionTargetHost compatibleHost = compatibleSocket
                .AddComponent<InteractionTargetHost>();
            compatibleHost.Configure(compatibleTarget);
            compatibleHost.ConfigureSelectionPriority(10);

            try
            {
                RaycastInteractionCandidateSource source = owner
                    .AddComponent<RaycastInteractionCandidateSource>();
                source.Configure(viewpoint.transform, 2f, ~0);
                source.SetCarriedObjectTarget(pickupTarget);
                Physics.SyncTransforms();

                InteractionCandidate candidate = source.Query();

                Assert.That(candidate.IsValid, Is.True);
                Assert.That(
                    candidate.Host,
                    Is.SameAs(compatibleHost),
                    "An incompatible direct mount must be filtered before " +
                    "selection priority can hide the correct socket.");
            }
            finally
            {
                Object.DestroyImmediate(compatibleSocket);
                Object.DestroyImmediate(incompatibleSocket);
                Object.DestroyImmediate(viewpoint);
            }
        }

        [Test]
        public void RaycastQuery_DenseOwnedCollidersCannotTruncateNestedCarrySocket()
        {
            GameObject viewpoint = new GameObject("Dense vehicle viewpoint");
            viewpoint.transform.position = Vector3.up * 10f;
            viewpoint.transform.forward = Vector3.forward;
            GameObject assemblyOwner = new GameObject("Dense vehicle owner");
            assemblyOwner.transform.position = viewpoint.transform.position;
            InteractionTargetHost ownerHost = assemblyOwner
                .AddComponent<InteractionTargetHost>();
            ownerHost.Configure(
                assemblyOwner.AddComponent<ToolActivationCounterTarget>());

            // This deliberately exceeds the old 32-hit prototype buffer while
            // staying below the authored 256-hit Satsuma budget. Every solid is
            // owned by the same assembly host, exactly like a dense wheel well.
            for (int index = 0; index < 128; index++)
            {
                GameObject blocker = new GameObject(
                    $"Owned chassis collider {index:000}");
                blocker.transform.SetParent(assemblyOwner.transform, false);
                blocker.transform.localPosition = new Vector3(
                    0f,
                    0f,
                    0.35f + index * 0.008f);
                BoxCollider collider = blocker.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.2f, 0.2f, 0.003f);
            }

            GameObject socket = new GameObject("Nested carried-part socket");
            socket.transform.SetParent(assemblyOwner.transform, false);
            socket.transform.localPosition = Vector3.forward * 1.6f;
            SphereCollider socketCollider = socket.AddComponent<SphereCollider>();
            socketCollider.radius = 0.035f;
            socketCollider.isTrigger = true;
            DenseNestedCarriedObjectTarget socketTarget = socket
                .AddComponent<DenseNestedCarriedObjectTarget>();
            InteractionTargetHost socketHost = socket
                .AddComponent<InteractionTargetHost>();
            socketHost.Configure(socketTarget);
            socketHost.ConfigureSelectionPriority(100);

            try
            {
                RaycastInteractionCandidateSource source = owner
                    .AddComponent<RaycastInteractionCandidateSource>();
                source.Configure(viewpoint.transform, 2f, ~0);
                source.SetCarriedObjectTargetsEnabled(true);
                Physics.SyncTransforms();

                InteractionCandidate candidate = source.Query();

                Assert.That(candidate.IsValid, Is.True);
                Assert.That(
                    candidate.Host,
                    Is.SameAs(socketHost),
                    "Dense same-assembly solids must not truncate the exact " +
                    "nested install socket from the allocation-free ray query.");
            }
            finally
            {
                Object.DestroyImmediate(assemblyOwner);
                Object.DestroyImmediate(viewpoint);
            }
        }

        [Test]
        public void CarryIgnoresAuthoredOwnerScopeOnlyUntilRelease()
        {
            GameObject scopeObject = new GameObject("Vehicle collision scope");
            BoxCollider vehicleCollider = scopeObject.AddComponent<BoxCollider>();
            CarryCollisionBypassScope scope = scopeObject
                .AddComponent<CarryCollisionBypassScope>();
            scope.Configure(new Collider[] { vehicleCollider });
            Collider itemCollider = item.GetComponent<Collider>();
            item.transform.SetParent(scopeObject.transform, true);

            try
            {
                Assert.That(
                    Physics.GetIgnoreCollision(itemCollider, vehicleCollider),
                    Is.False);
                Assert.That(
                    carryController.TryPickup(pickupTarget, context),
                    Is.True);
                Assert.That(
                    Physics.GetIgnoreCollision(itemCollider, vehicleCollider),
                    Is.True,
                    "A carried nested part must be pullable through its owning shell.");

                Assert.That(carryController.Drop(), Is.True);
                Assert.That(
                    Physics.GetIgnoreCollision(itemCollider, vehicleCollider),
                    Is.False,
                    "Owner collision must return immediately after release.");
            }
            finally
            {
                item.transform.SetParent(null, true);
                Object.DestroyImmediate(scopeObject);
            }
        }

        [Test]
        public void RaycastQuery_PrefersNestedAuthoredControlWithoutSeeingThroughWalls()
        {
            GameObject viewpoint = new GameObject("Viewpoint");
            viewpoint.transform.SetParent(owner.transform, false);
            GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shell.name = "ToolboxShell";
            shell.transform.position = Vector3.forward * 2f;
            shell.transform.localScale = new Vector3(1f, 1f, 0.1f);
            InteractionTargetHost shellHost =
                shell.AddComponent<InteractionTargetHost>();
            shellHost.Configure(shell.AddComponent<ToolActivationCounterTarget>());

            GameObject nested = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nested.name = "NestedWrench";
            nested.transform.position = Vector3.forward * 2.01f;
            nested.transform.localScale = Vector3.one * 0.02f;
            nested.GetComponent<Collider>().isTrigger = true;
            InteractionTargetHost nestedHost =
                nested.AddComponent<InteractionTargetHost>();
            nestedHost.Configure(nested.AddComponent<ToolActivationCounterTarget>());
            nestedHost.ConfigureSelectionPriority(30);

            try
            {
                RaycastInteractionCandidateSource source =
                    owner.AddComponent<RaycastInteractionCandidateSource>();
                source.Configure(viewpoint.transform, 3f, ~0);
                Physics.SyncTransforms();

                InteractionCandidate candidate = source.Query();

                Assert.That(candidate.IsValid, Is.True);
                Assert.That(candidate.Host, Is.SameAs(nestedHost));

                shellHost.Configure();
                Object.DestroyImmediate(shell.GetComponent<InteractionTargetHost>());
                Physics.SyncTransforms();

                candidate = source.Query();
                Assert.That(candidate.IsValid, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(nested);
                Object.DestroyImmediate(shell);
            }
        }

        [Test]
        public void HeldActivationUsesExplicitRegisteredCapability()
        {
            HeldActivationCounterTarget heldAction =
                item.AddComponent<HeldActivationCounterTarget>();
            InteractionTargetHost host = item.AddComponent<InteractionTargetHost>();
            host.Configure(pickupTarget, heldAction);

            Assert.That(carryController.TryPickup(pickupTarget, context), Is.True);
            Assert.That(carryController.TryActivateHeld(context), Is.True);
            Assert.That(heldAction.ActivationCount, Is.EqualTo(1));
        }

        [Test]
        public void FirstPersonToolMode_SelectsWithoutEnteringPhysicalCarry()
        {
            Object.DestroyImmediate(pickupTarget);
            pickupTarget = null;
            Object.DestroyImmediate(item.GetComponent<Rigidbody>());
            FirstPersonToolSelectionTarget toolMode =
                item.AddComponent<FirstPersonToolSelectionTarget>();
            InteractionTargetHost host =
                item.AddComponent<InteractionTargetHost>();
            host.Configure(toolMode);
            GameObject viewpoint = new GameObject("ToolViewpoint");
            viewpoint.transform.SetParent(owner.transform, false);
            item.transform.position = Vector3.forward * 2f;
            RaycastInteractionCandidateSource source =
                owner.AddComponent<RaycastInteractionCandidateSource>();
            source.Configure(viewpoint.transform, 3f, ~0);
            PlayerInteractionController interaction =
                owner.AddComponent<PlayerInteractionController>();
            interaction.Configure(
                source,
                carryController,
                viewpoint.transform,
                ~0);

            try
            {
                Physics.SyncTransforms();
                interaction.RefreshCandidate();
                Assert.That(interaction.TryPickupOrPlace(), Is.True);
                Assert.That(interaction.IsFirstPersonToolModeActive, Is.True);
                Assert.That(interaction.HasHeldObject, Is.False);
                Assert.That(carryController.HasHeldObject, Is.False);
                Assert.That(item.GetComponent<Rigidbody>(), Is.Null);
                Assert.That(toolMode.IsSelected, Is.True);
                Assert.That(
                    interaction.CaptureCarriedObjectState().HasCarriedObject,
                    Is.False);

                Assert.That(interaction.DropHeldObject(), Is.True);
                Assert.That(interaction.IsFirstPersonToolModeActive, Is.False);
                Assert.That(toolMode.IsSelected, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(viewpoint);
            }
        }

        [Test]
        public void FirstPersonToolMode_RaycastSelectsOnlyScrollTargetsAndWheelOperatesThem()
        {
            Object.DestroyImmediate(pickupTarget);
            pickupTarget = null;
            Object.DestroyImmediate(item.GetComponent<Rigidbody>());
            FirstPersonToolSelectionTarget toolMode =
                item.AddComponent<FirstPersonToolSelectionTarget>();
            InteractionTargetHost heldHost =
                item.AddComponent<InteractionTargetHost>();
            heldHost.Configure(toolMode);
            item.transform.position = Vector3.forward;

            GameObject viewpoint = new GameObject("ToolViewpoint");
            viewpoint.transform.SetParent(owner.transform, false);
            GameObject ordinary = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            ordinary.transform.position = Vector3.forward * 1.5f;
            ordinary.transform.localScale = Vector3.one * 0.1f;
            ordinary.GetComponent<Collider>().isTrigger = false;
            InteractionTargetHost ordinaryHost =
                ordinary.AddComponent<InteractionTargetHost>();
            ordinaryHost.Configure(
                ordinary.AddComponent<ToolActivationCounterTarget>());
            ordinaryHost.ConfigureSelectionPriority(100);

            GameObject bolt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bolt.transform.position = Vector3.forward * 2f;
            bolt.transform.localScale = Vector3.one * 0.1f;
            bolt.GetComponent<Collider>().isTrigger = true;
            int fastenerLayer = LayerMask.NameToLayer(
                FastenerToolRaycastLayer.Name);
            Assert.That(fastenerLayer, Is.GreaterThanOrEqualTo(0));
            bolt.layer = fastenerLayer;
            ScrollToolCounterTarget scrollTarget =
                bolt.AddComponent<ScrollToolCounterTarget>();
            InteractionTargetHost boltHost =
                bolt.AddComponent<InteractionTargetHost>();
            boltHost.Configure(scrollTarget);

            try
            {
                RaycastInteractionCandidateSource source =
                    owner.AddComponent<RaycastInteractionCandidateSource>();
                source.Configure(viewpoint.transform, 3f, ~0);
                PlayerInteractionController interaction =
                    owner.AddComponent<PlayerInteractionController>();
                interaction.Configure(
                    source,
                    carryController,
                    viewpoint.transform,
                    ~0);

                Physics.SyncTransforms();
                interaction.RefreshCandidate();
                Assert.That(interaction.TryPickupOrPlace(), Is.True);
                interaction.RefreshCandidate();

                Assert.That(interaction.CurrentCandidate.Host, Is.SameAs(boltHost));
                Assert.That(
                    interaction.CurrentCandidate.Distance,
                    Is.GreaterThan(1.8f),
                    "The wrench ray must pass through ordinary solid geometry and reach the fastener-only layer.");
                Assert.That(interaction.CurrentPromptBindingLabel, Is.EqualTo("КОЛЕСО"));
                InteractionActionSnapshot actionSnapshot =
                    interaction.CurrentActionSnapshot;
                Assert.That(actionSnapshot.ActionCount, Is.EqualTo(3));
                Assert.That(
                    actionSnapshot.First.ScrollDirection,
                    Is.EqualTo(InteractionScrollDirection.Positive));
                Assert.That(actionSnapshot.First.Label, Is.EqualTo("ЗАТЯНУТЬ"));
                Assert.That(
                    actionSnapshot.Second.ScrollDirection,
                    Is.EqualTo(InteractionScrollDirection.Negative));
                Assert.That(actionSnapshot.Second.Label, Is.EqualTo("ОСЛАБИТЬ"));
                Assert.That(
                    actionSnapshot.Third.Binding,
                    Is.EqualTo(InteractionActionBinding.Interact));
                Assert.That(
                    interaction.TryOperateCurrentHeldTool(1f),
                    Is.True);
                Assert.That(scrollTarget.SignedTurns, Is.EqualTo(1f));
                Assert.That(
                    interaction.TryOperateCurrentHeldTool(-1f),
                    Is.True);
                Assert.That(scrollTarget.SignedTurns, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(bolt);
                Object.DestroyImmediate(ordinary);
                Object.DestroyImmediate(viewpoint);
            }
        }

        [Test]
        public void PlayerToolActivationCanUseHeldItemWithoutWorldCandidate()
        {
            HeldActivationCounterTarget heldAction =
                item.AddComponent<HeldActivationCounterTarget>();
            InteractionTargetHost host = item.AddComponent<InteractionTargetHost>();
            host.Configure(pickupTarget, heldAction);
            PlayerInteractionController interaction =
                owner.AddComponent<PlayerInteractionController>();
            interaction.Configure(null, carryController, owner.transform, ~0);

            Assert.That(carryController.TryPickup(pickupTarget, context), Is.True);
            Assert.That(interaction.HasCandidate, Is.False);
            Assert.That(interaction.TryToolActivation(), Is.True);
            Assert.That(heldAction.ActivationCount, Is.EqualTo(1));
        }

        [Test]
        public void HeldObjectActionSnapshot_ExposesReleaseThrowAndRotation()
        {
            PlayerInteractionController interaction =
                owner.AddComponent<PlayerInteractionController>();
            interaction.Configure(null, carryController, owner.transform, ~0);

            Assert.That(carryController.TryPickup(pickupTarget, context), Is.True);

            InteractionActionSnapshot snapshot =
                interaction.CurrentActionSnapshot;
            Assert.That(snapshot.Reticle, Is.EqualTo(InteractionReticleKind.Dot));
            Assert.That(snapshot.ActionCount, Is.EqualTo(3));
            Assert.That(snapshot.First.Label, Is.EqualTo("ОТПУСТИТЬ"));
            Assert.That(
                snapshot.First.Binding,
                Is.EqualTo(InteractionActionBinding.Interact));
            Assert.That(snapshot.Second.Label, Is.EqualTo("БРОСИТЬ"));
            Assert.That(
                snapshot.Second.Binding,
                Is.EqualTo(InteractionActionBinding.Throw));
            Assert.That(snapshot.Third.Label, Is.EqualTo("ВРАЩАТЬ"));
            Assert.That(
                snapshot.Third.Binding,
                Is.EqualTo(InteractionActionBinding.Scroll));
        }

        [Test]
        public void FirstUsePressPreparesContinuousTargetAndSecondStartsLifecycle()
        {
            GameObject viewpoint = new GameObject("Viewpoint");
            viewpoint.transform.SetParent(owner.transform, false);
            viewpoint.transform.localPosition = Vector3.up * 2f;
            item.transform.position =
                viewpoint.transform.position + Vector3.forward * 2f;
            var continuous =
                item.AddComponent<ContinuousPickupActivationCounterTarget>();
            InteractionTargetHost host =
                item.AddComponent<InteractionTargetHost>();
            host.Configure(pickupTarget, continuous);
            RaycastInteractionCandidateSource source =
                owner.AddComponent<RaycastInteractionCandidateSource>();
            source.Configure(viewpoint.transform, 3f, ~0);
            PlayerInteractionController interaction =
                owner.AddComponent<PlayerInteractionController>();
            interaction.Configure(
                source,
                carryController,
                viewpoint.transform,
                ~0);

            Physics.SyncTransforms();
            interaction.RefreshCandidate();

            Assert.That(interaction.TryBeginToolActivation(), Is.True);
            Assert.That(carryController.HasHeldObject, Is.True);
            Assert.That(carryController.IsHeldUseReady, Is.True);
            Assert.That(pickupTarget.IsCarried, Is.True);
            Assert.That(continuous.BeginCount, Is.Zero);
            Assert.That(
                interaction.ContinueToolActivation(0.25f),
                Is.False);
            Assert.That(continuous.ContinueCount, Is.Zero);

            Assert.That(interaction.TryBeginToolActivation(), Is.True);
            Assert.That(continuous.BeginCount, Is.EqualTo(1));
            Assert.That(
                interaction.ContinueToolActivation(0.25f),
                Is.True);
            Assert.That(continuous.ContinueCount, Is.EqualTo(1));

            interaction.EndToolActivation();

            Assert.That(continuous.EndCount, Is.EqualTo(1));
            Assert.That(carryController.HasHeldObject, Is.True);
        }

        [Test]
        public void ContinuousUsePreflightFailureDoesNotEmitTransientPickup()
        {
            var continuous =
                item.AddComponent<ContinuousPickupActivationCounterTarget>();
            continuous.CanBegin = false;
            InteractionTargetHost host =
                item.AddComponent<InteractionTargetHost>();
            host.Configure(pickupTarget, continuous);
            int pickupEvents = 0;
            carryController.ActionCompleted += action =>
            {
                if (action.Action == InteractionActionKind.Pickup)
                {
                    pickupEvents++;
                }
            };

            Assert.That(
                carryController.TryPickupAndBeginContinuousHeldActivation(
                    pickupTarget,
                    context),
                Is.False);
            Assert.That(carryController.HasHeldObject, Is.False);
            Assert.That(pickupTarget.IsCarried, Is.False);
            Assert.That(continuous.BeginCount, Is.Zero);
            Assert.That(pickupEvents, Is.Zero);
        }

        [Test]
        public void AtomicUsePickupRequiresExplicitWorldTargetOptIn()
        {
            var continuous =
                item.AddComponent<UnmarkedContinuousActivationCounterTarget>();
            InteractionTargetHost host =
                item.AddComponent<InteractionTargetHost>();
            host.Configure(pickupTarget, continuous);

            Assert.That(
                carryController.TryPickupAndBeginContinuousHeldActivation(
                    pickupTarget,
                    context),
                Is.False);
            Assert.That(carryController.HasHeldObject, Is.False);
            Assert.That(pickupTarget.IsCarried, Is.False);
            Assert.That(continuous.BeginCount, Is.Zero);
        }

        [Test]
        public void PlayerToolActivationUsesLegacyWorldTargetBeforeHeldSelfFallback()
        {
            HeldActivationCounterTarget heldAction =
                item.AddComponent<HeldActivationCounterTarget>();
            InteractionTargetHost heldHost = item.AddComponent<InteractionTargetHost>();
            heldHost.Configure(pickupTarget, heldAction);

            GameObject viewpoint = new GameObject("Viewpoint");
            viewpoint.transform.SetParent(owner.transform, false);
            viewpoint.transform.localPosition = Vector3.up * 2f;
            GameObject worldTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            worldTarget.name = "WorldToolTarget";
            worldTarget.transform.position = viewpoint.transform.position + Vector3.forward * 2f;
            ToolActivationCounterTarget worldAction =
                worldTarget.AddComponent<ToolActivationCounterTarget>();
            InteractionTargetHost worldHost = worldTarget.AddComponent<InteractionTargetHost>();
            worldHost.Configure(worldAction);

            try
            {
                RaycastInteractionCandidateSource source =
                    owner.AddComponent<RaycastInteractionCandidateSource>();
                source.Configure(viewpoint.transform, 3f, ~0);
                PlayerInteractionController interaction =
                    owner.AddComponent<PlayerInteractionController>();
                interaction.Configure(source, carryController, viewpoint.transform, ~0);

                Assert.That(carryController.TryPickup(pickupTarget, context), Is.True);
                Physics.SyncTransforms();
                interaction.RefreshCandidate();

                Assert.That(interaction.HasCandidate, Is.True);
                Assert.That(interaction.TryToolActivation(), Is.True);
                Assert.That(heldAction.ActivationCount, Is.Zero);
                Assert.That(worldAction.ActivationCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(worldTarget);
            }
        }

        [Test]
        public void PlayerToolActivationDoesNotBypassHeldToolBoundaryWithEmptyHands()
        {
            GameObject viewpoint = new GameObject("Viewpoint");
            viewpoint.transform.SetParent(owner.transform, false);
            viewpoint.transform.localPosition = Vector3.up * 2f;
            GameObject worldTarget = GameObject.CreatePrimitive(PrimitiveType.Cube);
            worldTarget.name = "HeldToolRequiredTarget";
            worldTarget.transform.position =
                viewpoint.transform.position + Vector3.forward * 2f;
            HeldToolRequiredCounterTarget worldAction =
                worldTarget.AddComponent<HeldToolRequiredCounterTarget>();
            InteractionTargetHost worldHost =
                worldTarget.AddComponent<InteractionTargetHost>();
            worldHost.Configure(worldAction);

            try
            {
                RaycastInteractionCandidateSource source =
                    owner.AddComponent<RaycastInteractionCandidateSource>();
                source.Configure(viewpoint.transform, 3f, ~0);
                PlayerInteractionController interaction =
                    owner.AddComponent<PlayerInteractionController>();
                interaction.Configure(source, carryController, viewpoint.transform, ~0);

                Physics.SyncTransforms();
                interaction.RefreshCandidate();

                Assert.That(interaction.HasCandidate, Is.True);
                Assert.That(interaction.TryToolActivation(), Is.False);
                Assert.That(worldAction.LegacyActivationCount, Is.Zero);
                Assert.That(worldAction.HeldToolActivationCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(worldTarget);
            }
        }

        [Test]
        public void LookInputScalingUsesDeltaForPointerAndTimeForRateInput()
        {
            Vector2 pointerDegrees = LookInputScaling.ToRotationDegrees(
                new Vector2(10f, -5f),
                isPointerDelta: true,
                pointerDegreesPerPixel: 0.2f,
                rateDegreesPerSecond: 90f,
                unscaledDeltaTime: 0.5f);
            Vector2 stickAtThirtyFps = LookInputScaling.ToRotationDegrees(
                Vector2.one,
                isPointerDelta: false,
                pointerDegreesPerPixel: 0.2f,
                rateDegreesPerSecond: 90f,
                unscaledDeltaTime: 1f / 30f);
            Vector2 stickAtSixtyFps = LookInputScaling.ToRotationDegrees(
                Vector2.one,
                isPointerDelta: false,
                pointerDegreesPerPixel: 0.2f,
                rateDegreesPerSecond: 90f,
                unscaledDeltaTime: 1f / 60f);

            Assert.That(pointerDegrees, Is.EqualTo(new Vector2(2f, -1f)));
            Assert.That(stickAtThirtyFps.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(stickAtThirtyFps.y, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(stickAtSixtyFps.x * 2f, Is.EqualTo(stickAtThirtyFps.x).Within(0.0001f));
            Assert.That(stickAtSixtyFps.y * 2f, Is.EqualTo(stickAtThirtyFps.y).Within(0.0001f));
        }

        [Test]
        public void CrossdotRectStaysCenteredAtDifferentResolutions()
        {
            Rect fullHd = CrossdotPresenter.CalculateCenteredRect(1920f, 1080f, 6f);
            Rect ultrawide = CrossdotPresenter.CalculateCenteredRect(3440f, 1440f, 6f);

            Assert.That(fullHd.center, Is.EqualTo(new Vector2(960f, 540f)));
            Assert.That(fullHd.size, Is.EqualTo(new Vector2(6f, 6f)));
            Assert.That(ultrawide.center, Is.EqualTo(new Vector2(1720f, 720f)));
        }

        [Test]
        public void ContextActionStack_RemainsBottomLeftAndCapsAtThreeRows()
        {
            Rect canonical = CrossdotPresenter.CalculateActionStackBounds(
                1672f,
                941f,
                3,
                240f);
            Rect ultrawide = CrossdotPresenter.CalculateActionStackBounds(
                3440f,
                1440f,
                9,
                240f);

            Assert.That(canonical.x, Is.EqualTo(36f).Within(0.001f));
            Assert.That(canonical.yMax, Is.EqualTo(897f).Within(0.001f));
            Assert.That(canonical.height, Is.EqualTo(134f).Within(0.001f));
            Assert.That(ultrawide.height, Is.GreaterThan(canonical.height));
            Assert.That(ultrawide.xMin, Is.GreaterThan(36f));
            Assert.That(ultrawide.yMax, Is.LessThan(1440f));
        }

        [Test]
        public void ContextTextStack_RemainsBottomCentredAtWideAspectRatios()
        {
            Rect canonical = CrossdotPresenter.CalculateContextTextBounds(
                1672f,
                941f,
                420f,
                81f);
            Rect ultrawide = CrossdotPresenter.CalculateContextTextBounds(
                3440f,
                1440f,
                420f,
                81f);

            Assert.That(canonical.center.x, Is.EqualTo(836f).Within(0.001f));
            Assert.That(canonical.yMax, Is.EqualTo(897f).Within(0.001f));
            Assert.That(ultrawide.center.x, Is.EqualTo(1720f).Within(0.001f));
            Assert.That(ultrawide.yMax, Is.LessThan(1440f));
        }

        [Test]
        public void ContextHudClaimsAndReleasesExplicitSubtitleSource()
        {
            TestPlayerSubtitleSource source =
                owner.AddComponent<TestPlayerSubtitleSource>();
            CrossdotPresenter presenter = owner.AddComponent<CrossdotPresenter>();

            presenter.BindSubtitleSource(source);
            Assert.That(source.ContextHudPresenterActive, Is.True);

            presenter.UnbindSubtitleSource(source);
            Assert.That(source.ContextHudPresenterActive, Is.False);

            presenter.BindSubtitleSource(source);
            Assert.That(source.ContextHudPresenterActive, Is.True);
        }

        [Test]
        public void ContextActionSnapshot_CapsRowsAndAltReplacesTheStack()
        {
            InteractionActionSnapshot normal =
                new InteractionActionSnapshot(InteractionReticleKind.Install)
                    .Add(new InteractionActionHint(
                        InteractionActionBinding.Interact,
                        "УСТАНОВИТЬ"))
                    .Add(new InteractionActionHint(
                        InteractionActionBinding.Throw,
                        "БРОСИТЬ"))
                    .Add(new InteractionActionHint(
                        InteractionActionBinding.Scroll,
                        "ВРАЩАТЬ"))
                    .Add(new InteractionActionHint(
                        InteractionActionBinding.ToolActivate,
                        "ЛИШНЕЕ ДЕЙСТВИЕ"));
            InteractionActionSnapshot alternative =
                normal.WithAlternativeActions();

            Assert.That(normal.ActionCount, Is.EqualTo(3));
            Assert.That(normal.Reticle, Is.EqualTo(InteractionReticleKind.Install));
            Assert.That(normal.Third.Label, Is.EqualTo("ВРАЩАТЬ"));
            Assert.That(alternative.ActionCount, Is.EqualTo(3));
            Assert.That(alternative.Reticle, Is.EqualTo(InteractionReticleKind.Install));
            Assert.That(
                alternative.First.Binding,
                Is.EqualTo(InteractionActionBinding.Wave));
            Assert.That(alternative.First.Label, Is.EqualTo("ПОМАХАТЬ"));
            Assert.That(
                alternative.Third.Binding,
                Is.EqualTo(InteractionActionBinding.Swear));
        }

        [Test]
        public void ContextActionBindingLabels_UseCompactMouseAndKeyboardNames()
        {
            Assert.That(
                PlayerInputRouter.FormatBindingDisplayLabel(
                    "<Mouse>/leftButton"),
                Is.EqualTo("ЛКМ"));
            Assert.That(
                PlayerInputRouter.FormatBindingDisplayLabel(
                    "<Mouse>/rightButton"),
                Is.EqualTo("ПКМ"));
            Assert.That(
                PlayerInputRouter.FormatBindingDisplayLabel(
                    "<Mouse>/scroll/y"),
                Is.EqualTo("КОЛЕСО"));
            Assert.That(
                PlayerInputRouter.FormatBindingDisplayLabel(
                    "<Keyboard>/h"),
                Is.EqualTo("H"));
            Assert.That(
                PlayerInputRouter.FormatBindingDisplayLabel(
                    "<Keyboard>/alt"),
                Is.EqualTo("ALT"));
        }

        [Test]
        public void ContextHudLocalization_UsesOneBilingualPresentationBoundary()
        {
            Assert.That(
                InteractionUiTextCatalog.LocalizeDisplayName(
                    "vehicle.satsuma.part.battery",
                    "battery0",
                    "en-US"),
                Is.EqualTo("Battery"));
            Assert.That(
                InteractionUiTextCatalog.LocalizeDisplayName(
                    "vehicle.satsuma.part.battery",
                    "battery0",
                    "ru-RU"),
                Is.EqualTo("Аккумулятор"));
            Assert.That(
                InteractionUiTextCatalog.LocalizeDisplayName(
                    "item.mail-order-envelope",
                    "Конверт с заказом",
                    "en-US"),
                Is.EqualTo("Mail-order envelope"));
            Assert.That(
                InteractionUiTextCatalog.LocalizeDisplayName(
                    "fastener.satsuma.hood.boltpm-2",
                    "hood bolt 2",
                    "ru-RU"),
                Is.EqualTo("Крепёж №2"));
            Assert.That(
                InteractionUiTextCatalog.LocalizeDisplayName(
                    string.Empty,
                    "2/42  Brake service — 500,00 MK [selected]",
                    "ru-RU"),
                Is.EqualTo(
                    "2/42  Обслуживание тормозов — 500,00 MK [выбрано]"));
            Assert.That(
                InteractionUiTextCatalog.LocalizeActionLabel(
                    "Снять: rocker cover",
                    InteractionActionBinding.Interact,
                    "en-US"),
                Is.EqualTo("REMOVE"));
            Assert.That(
                InteractionUiTextCatalog.LocalizeActionLabel(
                    "ЛКМ: открыть / ПКМ: закрыть",
                    InteractionActionBinding.Throw,
                    "ru-RU"),
                Is.EqualTo("ЗАКРЫТЬ"));
            Assert.That(
                InteractionUiTextCatalog.LocalizeActionLabel(
                    "Заказать услугу",
                    InteractionActionBinding.Interact,
                    "en-US"),
                Is.EqualTo("ORDER SERVICE"));
            Assert.That(
                InteractionUiTextCatalog.LocalizeBindingLabel(
                    "КОЛЕСО",
                    "en-US"),
                Is.EqualTo("WHEEL"));
            Assert.That(
                InteractionUiTextCatalog.LocalizeSubtitle(
                    "Вы пьёте.",
                    "en-US"),
                Is.EqualTo("You are drinking."));
            Assert.That(
                InteractionUiTextCatalog.LocalizeSubtitle(
                    "Магазин закрыт",
                    "en-US"),
                Is.EqualTo("The shop is closed."));
        }

        [Test]
        public void ContextHud_TargetAndSubtitleShareSizeButUseDifferentWeight()
        {
            Assert.That(CrossdotPresenter.ContextTextFontSizePixels, Is.EqualTo(18));
            Assert.That(CrossdotPresenter.ContextTitleFontStyle, Is.EqualTo(FontStyle.Bold));
            Assert.That(CrossdotPresenter.ContextSubtitleFontStyle, Is.EqualTo(FontStyle.Normal));
        }

        [Test]
        public void ContextTargetNameCatalog_CoversItemsServicesAndSatsumaParts()
        {
            string itemCatalog = Path.Combine(
                Application.dataPath,
                "Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset");
            var names = new List<string>();
            AddDisplayNames(itemCatalog, names);
            int itemCount = names.Count;

            string serviceCatalog = Path.Combine(
                Application.dataPath,
                "Game/Services/Content/Phase1/Phase1ServiceCatalog.asset");
            AddDisplayNames(serviceCatalog, names);
            int serviceCount = names.Count - itemCount;

            string satsumaRoot = Path.Combine(
                Application.dataPath,
                "Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma");
            if (Directory.Exists(satsumaRoot))
            {
                foreach (string asset in Directory.GetFiles(
                    satsumaRoot,
                    "*.asset",
                    SearchOption.TopDirectoryOnly))
                {
                    AddDisplayNames(asset, names);
                }

                string looseDefinitions = Path.Combine(
                    satsumaRoot,
                    "LoosePartDefinitions");
                if (Directory.Exists(looseDefinitions))
                {
                    foreach (string asset in Directory.GetFiles(
                        looseDefinitions,
                        "*.asset",
                        SearchOption.TopDirectoryOnly))
                    {
                        AddDisplayNames(asset, names);
                    }
                }
            }

            Assert.That(itemCount, Is.EqualTo(152));
            Assert.That(serviceCount, Is.EqualTo(108));
            Assert.That(names.Count, Is.GreaterThanOrEqualTo(itemCount));
            foreach (string displayName in names)
            {
                Assert.That(
                    InteractionUiTextCatalog.CanLocalizeDisplayName(displayName),
                    Is.True,
                    $"Missing RU/EN contextual title: '{displayName}'.");
            }
        }

        [Test]
        public void ContextActionBindingGlyphs_DescribeTheEffectiveMouseControl()
        {
            Assert.That(
                PlayerInputRouter.ResolveBindingGlyphKind(
                    "<Mouse>/leftButton"),
                Is.EqualTo(InteractionBindingGlyphKind.MouseLeftButton));
            Assert.That(
                PlayerInputRouter.ResolveBindingGlyphKind(
                    "<Mouse>/rightButton"),
                Is.EqualTo(InteractionBindingGlyphKind.MouseRightButton));
            Assert.That(
                PlayerInputRouter.ResolveBindingGlyphKind(
                    "<Mouse>/middleButton"),
                Is.EqualTo(InteractionBindingGlyphKind.MouseMiddleButton));
            Assert.That(
                PlayerInputRouter.ResolveBindingGlyphKind(
                    "<Mouse>/scroll/y"),
                Is.EqualTo(InteractionBindingGlyphKind.MouseWheelScroll));
            Assert.That(
                PlayerInputRouter.ResolveBindingGlyphKind(
                    "<Keyboard>/f"),
                Is.EqualTo(InteractionBindingGlyphKind.Keycap));
            Assert.That(
                CrossdotPresenter.ResolveDefaultBindingGlyph(
                    InteractionActionBinding.Throw),
                Is.EqualTo(InteractionBindingGlyphKind.MouseRightButton));
        }

        private static void SetStableId(StableEntityIdAuthoring authoring, string stableId)
        {
            var serializedObject = new SerializedObject(authoring);
            serializedObject.FindProperty("stableId").stringValue = stableId;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void AddDisplayNames(
            string assetPath,
            ICollection<string> output)
        {
            foreach (string line in File.ReadLines(assetPath))
            {
                string trimmed = line.TrimStart();
                const string prefix = "displayName:";
                if (!trimmed.StartsWith(prefix))
                {
                    continue;
                }

                string value = trimmed.Substring(prefix.Length).Trim();
                if (value.Length >= 2 && value[0] == '"' &&
                    value[value.Length - 1] == '"')
                {
                    value = Regex.Unescape(value.Substring(1, value.Length - 2));
                }
                else if (value.Length >= 2 && value[0] == '\'' &&
                         value[value.Length - 1] == '\'')
                {
                    value = value.Substring(1, value.Length - 2);
                }

                if (!string.IsNullOrWhiteSpace(value))
                {
                    output.Add(value);
                }
            }
        }
    }

    public sealed class TestPlayerSubtitleSource : MonoBehaviour,
        IPlayerSubtitleSource
    {
        public string ActiveSubtitle => "Тестовый субтитр";

        public bool ContextHudPresenterActive { get; private set; }

        public void SetContextHudPresenterActive(bool active)
        {
            ContextHudPresenterActive = active;
        }
    }

    public sealed class HeldActivationCounterTarget : MonoBehaviour, IHeldActivationTarget
    {
        public int ActivationCount { get; private set; }

        public bool CanActivateHeld(in InteractionContext context)
        {
            return enabled && gameObject.activeInHierarchy;
        }

        public void ActivateHeld(in InteractionContext context)
        {
            if (CanActivateHeld(context))
            {
                ActivationCount++;
            }
        }
    }

    public sealed class FirstPersonToolSelectionTarget : MonoBehaviour,
        IFirstPersonToolSelectionTarget
    {
        public bool IsSelected { get; private set; }
        public string SelectionPrompt => "Выбрать ключ 10 мм";
        public Transform ToolVisual => transform;
        public string ToolType => "Wrench";
        public string ToolVariant => "10";
        public Vector3 IdleLocalPosition => new Vector3(0.3f, -0.2f, -0.5f);
        public Quaternion IdleLocalRotation => Quaternion.identity;

        public bool CanSelect(in InteractionContext context) => !IsSelected;

        public void NotifySelected(in InteractionContext context)
        {
            IsSelected = true;
            GetComponent<Collider>().enabled = false;
        }

        public void NotifyDeselected()
        {
            IsSelected = false;
            GetComponent<Collider>().enabled = true;
        }
    }

    public sealed class ScrollToolCounterTarget : MonoBehaviour,
        IToolActivationTarget,
        IDirectionalScrollHeldToolActivationTarget
    {
        public float SignedTurns { get; private set; }
        public string ToolPrompt => "Крутить болт";

        public bool CanActivateTool(in InteractionContext context) => false;

        public void ActivateTool(in InteractionContext context)
        {
        }

        public bool TryActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context,
            float signedNotches)
        {
            if (tool == null ||
                !string.Equals(tool.ToolType, "Wrench") ||
                !string.Equals(tool.ToolVariant, "10"))
            {
                return false;
            }

            SignedTurns += signedNotches;
            return true;
        }

        public bool CanActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context,
            InteractionScrollDirection direction) =>
            tool != null &&
            string.Equals(tool.ToolType, "Wrench") &&
            string.Equals(tool.ToolVariant, "10");

        public string GetHeldToolScrollPrompt(
            InteractionScrollDirection direction) =>
            direction == InteractionScrollDirection.Positive
                ? "ЗАТЯНУТЬ"
                : "ОСЛАБИТЬ";
    }

    public sealed class SecondaryInteractionCounterTarget : MonoBehaviour,
        IContextInteractionTarget,
        ISecondaryInteractionOnlyTarget
    {
        public int InteractionCount { get; private set; }
        public string InteractionPrompt => "Remove";

        public bool CanInteract(in InteractionContext context) => true;

        public void Interact(in InteractionContext context)
        {
            InteractionCount++;
        }
    }

    public sealed class OriginOverlapCounterTarget : MonoBehaviour,
        IContextInteractionTarget,
        IRaycastOriginOverlapTarget
    {
        public string InteractionPrompt => "Remove";

        public bool CanInteract(in InteractionContext context) => true;

        public void Interact(in InteractionContext context)
        {
        }
    }

    public sealed class CarriedObjectOnlyCounterTarget : MonoBehaviour,
        IContextInteractionTarget,
        IRequiresCarriedObjectRaycastTarget
    {
        public string InteractionPrompt => "Install";

        public bool CanInteract(in InteractionContext context) => true;

        public void Interact(in InteractionContext context)
        {
        }
    }

    public sealed class CarriedObjectFilterCounterTarget : MonoBehaviour,
        IContextInteractionTarget,
        IRequiresCarriedObjectRaycastTarget,
        ICarriedObjectRaycastFilter
    {
        public bool AcceptsCarriedObject { get; set; }

        public string InteractionPrompt => "Install";

        public bool CanInteract(in InteractionContext context) => true;

        public void Interact(in InteractionContext context)
        {
        }

        public bool CanSelectForCarriedObject(
            IPickupTarget pickupTarget,
            in InteractionContext context) =>
            AcceptsCarriedObject && pickupTarget != null;
    }

    public sealed class DenseNestedCarriedObjectTarget : MonoBehaviour,
        IContextInteractionTarget,
        IRequiresCarriedObjectRaycastTarget,
        IParentColliderOcclusionBypass
    {
        public string InteractionPrompt => "Install";

        public bool CanInteract(in InteractionContext context) => true;

        public void Interact(in InteractionContext context)
        {
        }

        public bool CanBypassParentCollider(InteractionTargetHost parentHost) =>
            parentHost != null && transform.IsChildOf(parentHost.transform);
    }

    public sealed class HeldToolRequiredCounterTarget : MonoBehaviour,
        IToolActivationTarget,
        IHeldToolActivationTarget
    {
        public int LegacyActivationCount { get; private set; }
        public int HeldToolActivationCount { get; private set; }
        public string ToolPrompt => "Test tool";

        public bool CanActivateTool(in InteractionContext context) => true;

        public void ActivateTool(in InteractionContext context)
        {
            LegacyActivationCount++;
        }

        public bool CanActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context) => tool != null;

        public void ActivateHeldTool(
            IHeldToolIdentity tool,
            in InteractionContext context)
        {
            HeldToolActivationCount++;
        }
    }

    public sealed class ContinuousPickupActivationCounterTarget :
        MonoBehaviour,
        IContinuousHeldActivationTarget,
        IContinuousActivationPickupTarget
    {
        public bool CanBegin { get; set; } = true;
        public int BeginCount { get; private set; }
        public int ContinueCount { get; private set; }
        public int EndCount { get; private set; }
        public bool IsContinuousHeldActivationActive { get; private set; }

        public bool CanPickupForContinuousActivation(
            in InteractionContext context) => CanBegin;

        public bool CanBeginContinuousHeldActivation(
            in InteractionContext context) =>
            CanBegin &&
            !IsContinuousHeldActivationActive;

        public void BeginContinuousHeldActivation(
            in InteractionContext context)
        {
            BeginCount++;
            IsContinuousHeldActivationActive = true;
        }

        public bool ContinueContinuousHeldActivation(
            float unscaledDeltaTime)
        {
            ContinueCount++;
            return IsContinuousHeldActivationActive;
        }

        public void EndContinuousHeldActivation()
        {
            EndCount++;
            IsContinuousHeldActivationActive = false;
        }
    }

    public sealed class UnmarkedContinuousActivationCounterTarget :
        MonoBehaviour,
        IContinuousHeldActivationTarget
    {
        public int BeginCount { get; private set; }
        public bool IsContinuousHeldActivationActive { get; private set; }

        public bool CanBeginContinuousHeldActivation(
            in InteractionContext context) => true;

        public void BeginContinuousHeldActivation(
            in InteractionContext context)
        {
            BeginCount++;
            IsContinuousHeldActivationActive = true;
        }

        public bool ContinueContinuousHeldActivation(
            float unscaledDeltaTime) =>
            IsContinuousHeldActivationActive;

        public void EndContinuousHeldActivation()
        {
            IsContinuousHeldActivationActive = false;
        }
    }
}
