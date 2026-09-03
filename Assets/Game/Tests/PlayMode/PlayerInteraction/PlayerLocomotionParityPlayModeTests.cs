using System.Collections;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.PlayerInteraction
{
    public sealed class PlayerLocomotionParityPlayModeTests
    {
        [UnityTest]
        public IEnumerator ConfiguredMotor_UsesOriginalTraversalCapsule()
        {
            using MotorRig rig = MotorRig.Create(Vector3.zero);
            yield return null;

            Assert.That(rig.Controller.radius, Is.EqualTo(0.12f).Within(0.001f));
            Assert.That(rig.Controller.height, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(rig.Controller.center.y, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(rig.Controller.stepOffset, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(rig.Controller.slopeLimit, Is.EqualTo(90f).Within(0.001f));
            Assert.That(rig.Controller.skinWidth, Is.EqualTo(0.03f).Within(0.001f));
            Assert.That(rig.Controller.minMoveDistance, Is.Zero.Within(0.0001f));

            rig.Motor.TrySetPosture(PlayerPosture.DeepCrouch);
            yield return null;

            Assert.That(rig.Controller.height, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(rig.Controller.stepOffset, Is.EqualTo(0.4f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator RunWaitsForWalkingMomentumThenReachesTunedSpeed()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.02f, 0f));
            rig.CreateFloor(
                new Vector3(0f, -0.05f, 6f),
                new Vector3(5f, 0.1f, 16f));
            Physics.SyncTransforms();

            yield return Settle(rig, 8);
            rig.Motor.SetRunRequested(true);
            rig.Motor.SetMoveInput(Vector2.up);
            yield return null;

            Assert.That(
                rig.Motor.IsRunning,
                Is.False,
                "Run engaged from rest instead of waiting for walking momentum.");

            yield return RunUntil(
                () => rig.Motor.IsRunning,
                maximumFrames: 60);
            Assert.That(rig.Motor.IsRunning, Is.True);

            yield return RunUntil(
                () => rig.Motor.HorizontalSpeedMetersPerSecond > 6f,
                maximumFrames: 180);
            Assert.That(
                rig.Motor.HorizontalSpeedMetersPerSecond,
                Is.InRange(6f, 6.5f));
        }

        [UnityTest]
        public IEnumerator JumpReachesTunedHeightAndDoesNotDoubleUseCoyoteWindow()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.02f, 0f));
            rig.CreateFloor(
                new Vector3(0f, -0.05f, 0f),
                new Vector3(5f, 0.1f, 5f));
            Physics.SyncTransforms();

            yield return Settle(rig, 8);
            float startHeight = rig.Root.transform.position.y;
            float maximumHeight = startHeight;
            rig.Motor.RequestJump();

            for (int frame = 0; frame < 180; frame++)
            {
                yield return null;
                maximumHeight = Mathf.Max(
                    maximumHeight,
                    rig.Root.transform.position.y);
                if (frame > 10 && rig.Motor.IsGrounded)
                {
                    break;
                }
            }

            Assert.That(
                maximumHeight - startHeight,
                Is.InRange(0.95f, 1.12f));

            rig.Motor.RequestJump();
            yield return null;

            Assert.That(rig.Motor.IsPreparingJump, Is.True);
            Assert.That(
                rig.Motor.VerticalSpeedMetersPerSecond,
                Is.LessThanOrEqualTo(0f),
                "The capsule launched before the preparation crouch.");
            Assert.That(
                rig.Motion.CurrentVerticalOffsetMeters,
                Is.LessThan(0f),
                "Jump preparation did not lower the presentation rig.");
            Assert.That(
                rig.Root.transform.position.y,
                Is.EqualTo(startHeight).Within(0.01f),
                "The traversal capsule moved instead of preparing visually.");

            yield return RunUntil(
                () => rig.Motor.VerticalSpeedMetersPerSecond > 6f,
                maximumFrames: 30);
            Assert.That(rig.Motor.VerticalSpeedMetersPerSecond, Is.GreaterThan(6f));
            Assert.That(rig.Motor.IsPreparingJump, Is.False);
            Assert.That(
                rig.Motion.CurrentVerticalOffsetMeters,
                Is.LessThan(0f),
                "Jump did not produce the restrained event camera impulse.");
            float firstFrameJumpDip =
                Mathf.Abs(rig.Motion.CurrentVerticalOffsetMeters);
            rig.Motor.RequestJump();
            yield return null;
            Assert.That(
                rig.Motor.VerticalSpeedMetersPerSecond,
                Is.LessThan(6.64f),
                "The consumed coyote allowance permitted a second airborne jump.");

            float maximumJumpDip = firstFrameJumpDip;
            float cameraSampleDeadline = Time.realtimeSinceStartup + 0.08f;
            while (Time.realtimeSinceStartup < cameraSampleDeadline)
            {
                yield return null;
                maximumJumpDip = Mathf.Max(
                    maximumJumpDip,
                    Mathf.Abs(rig.Motion.CurrentVerticalOffsetMeters));
            }

            Assert.That(
                firstFrameJumpDip,
                Is.LessThan(0.025f),
                "Jump camera feedback snapped to its full displacement.");
            Assert.That(
                maximumJumpDip,
                Is.GreaterThan(0.03f),
                "Jump camera feedback did not exceed the previous peak dip.");
        }

        [UnityTest]
        public IEnumerator CrouchedJump_CommitsToStandingAndStaysStanding()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.02f, 0f));
            rig.CreateFloor(
                new Vector3(0f, -0.05f, 0f),
                new Vector3(5f, 0.1f, 5f));
            Physics.SyncTransforms();

            yield return Settle(rig, 8);
            Assert.That(
                rig.Motor.TrySetPosture(PlayerPosture.Crouch),
                Is.True);
            yield return null;

            rig.Motor.RequestJump();
            yield return null;

            Assert.That(rig.Motor.Posture, Is.EqualTo(PlayerPosture.Standing));
            Assert.That(rig.Motor.IsPreparingJump, Is.True);
            yield return RunUntil(
                () => rig.Motor.VerticalSpeedMetersPerSecond > 6f,
                maximumFrames: 30);
            yield return RunUntil(
                () => !rig.Motor.IsGrounded,
                maximumFrames: 30);
            yield return RunUntil(
                () => rig.Motor.IsGrounded,
                maximumFrames: 180);

            Assert.That(rig.Motor.Posture, Is.EqualTo(PlayerPosture.Standing));
            Assert.That(rig.Motor.LastLandingImpact.OriginatedFromJump, Is.True);
            Assert.That(
                rig.Motor.LastLandingImpact.JumpForceMetersPerSecond,
                Is.EqualTo(6.65f).Within(0.01f));
            Assert.That(
                rig.Motion.LastLandingStrength,
                Is.GreaterThan(0.4f),
                "Landing presentation did not consume jump-force evidence.");
        }

        [UnityTest]
        public IEnumerator StandingRise_UsesTheFastDedicatedTransition()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.02f, 0f));
            rig.CreateFloor(
                new Vector3(0f, -0.05f, 0f),
                new Vector3(5f, 0.1f, 5f));
            Physics.SyncTransforms();

            yield return Settle(rig, 8);
            Assert.That(
                rig.Motor.TrySetPosture(PlayerPosture.DeepCrouch),
                Is.True);
            yield return RunUntil(
                () => EyeHeight(rig) <= 0.39f,
                maximumFrames: 90);
            Assert.That(EyeHeight(rig), Is.EqualTo(0.38f).Within(0.015f));

            Assert.That(
                rig.Motor.TrySetPosture(PlayerPosture.Standing),
                Is.True);
            for (int frame = 0; frame < 12; frame++)
            {
                yield return null;
            }

            Assert.That(EyeHeight(rig), Is.EqualTo(1.48f).Within(0.02f));
        }

        [UnityTest]
        public IEnumerator DynamicSupport_ReceivesTheConfiguredPlayerWeight()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.02f, 0f));
            Time.captureDeltaTime = 1f / 120f;
            BoxCollider platform = rig.CreateObstacle(
                "DynamicWeightPlatform",
                new Vector3(0f, -0.05f, 0f),
                new Vector3(5f, 0.1f, 5f));
            Rigidbody body = platform.gameObject.AddComponent<Rigidbody>();
            body.mass = 10_000f;
            body.useGravity = false;
            body.constraints =
                RigidbodyConstraints.FreezePositionX |
                RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotation;
            body.sleepThreshold = 0f;
            Assert.That(rig.Motor.TrySetBodyMassKilograms(96f), Is.True);
            Physics.SyncTransforms();

            // The physical-load boundary now confirms a full footprint before
            // ramping, so measure the settled fixed-step force after that
            // intentional anti-rocker delay.
            yield return Settle(rig, 60);

            Assert.That(rig.Motor.LastSupportedRigidbody, Is.SameAs(body));
            float expectedFixedImpulse =
                96f * Mathf.Abs(Physics.gravity.y) * Time.fixedDeltaTime;
            Assert.That(
                -rig.Motor.LastSupportLoadImpulseNewtonSeconds.y,
                Is.EqualTo(expectedFixedImpulse).Within(
                    expectedFixedImpulse * 0.05f),
                "Support load must use the fixed physics cadence, not the " +
                "render-frame delta time.");
            yield return new WaitForFixedUpdate();
            Assert.That(
                body.linearVelocity.y,
                Is.LessThan(0f),
                "The support body did not receive the player's downward load.");
        }

        [UnityTest]
        public IEnumerator SprungDynamicSupport_SettlesWithoutPlayerBounceLoop()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.02f, 0f));
            BoxCollider platform = rig.CreateObstacle(
                "SprungVehiclePanel",
                new Vector3(0f, -0.05f, 0f),
                new Vector3(5f, 0.1f, 5f));
            Rigidbody body = platform.gameObject.AddComponent<Rigidbody>();
            body.mass = 389f;
            body.useGravity = false;
            body.constraints =
                RigidbodyConstraints.FreezePositionX |
                RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotation;
            body.sleepThreshold = 0f;
            const float restBodyY = -0.05f;
            const float springNewtonsPerMeter = 30_000f;
            const float damperNewtonSecondsPerMeter = 6_500f;
            float minimumPlayerY = float.PositiveInfinity;
            float maximumPlayerY = float.NegativeInfinity;
            float minimumRelativeHeight = float.PositiveInfinity;
            float maximumRelativeHeight = float.NegativeInfinity;
            Physics.SyncTransforms();

            const int simulationSteps = 300;
            const int sampleSteps = 60;
            for (int step = 0; step < simulationSteps; step++)
            {
                float springForce =
                    (restBodyY - body.position.y) * springNewtonsPerMeter -
                    body.linearVelocity.y * damperNewtonSecondsPerMeter;
                body.AddForce(Vector3.up * springForce, ForceMode.Force);
                yield return new WaitForFixedUpdate();

                if (step < simulationSteps - sampleSteps)
                {
                    continue;
                }

                float playerY = rig.Root.transform.position.y;
                float relativeHeight = playerY -
                    (body.position.y + platform.bounds.extents.y);
                minimumPlayerY = Mathf.Min(minimumPlayerY, playerY);
                maximumPlayerY = Mathf.Max(maximumPlayerY, playerY);
                minimumRelativeHeight = Mathf.Min(
                    minimumRelativeHeight,
                    relativeHeight);
                maximumRelativeHeight = Mathf.Max(
                    maximumRelativeHeight,
                    relativeHeight);
            }

            Assert.That(rig.Motor.IsGrounded, Is.True);
            Assert.That(rig.Motor.LastSupportedRigidbody, Is.SameAs(body));
            Assert.That(
                maximumPlayerY - minimumPlayerY,
                Is.LessThan(0.015f),
                "The player kept bouncing after the sprung support settled.");
            Assert.That(
                maximumRelativeHeight - minimumRelativeHeight,
                Is.LessThan(0.01f),
                "The CharacterController alternated between penetrating and " +
                "losing the sprung vehicle support.");
            Assert.That(
                Mathf.Abs(body.linearVelocity.y),
                Is.LessThan(0.05f),
                "The fixed player load left the sprung support oscillating.");
        }

        [UnityTest]
        public IEnumerator KinematicPanel_AllowsLoadToReachDynamicChassisBelow()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.06f, 0f));
            BoxCollider chassisCollider = rig.CreateObstacle(
                "DynamicChassisBelowPanel",
                new Vector3(0f, -0.1f, 0f),
                new Vector3(5f, 0.2f, 5f));
            Rigidbody chassis = chassisCollider.gameObject
                .AddComponent<Rigidbody>();
            chassis.mass = 10_000f;
            chassis.useGravity = false;
            chassis.constraints = RigidbodyConstraints.FreezeAll;

            BoxCollider panelCollider = rig.CreateObstacle(
                "KinematicHoodOrBootlidPanel",
                new Vector3(0f, 0.02f, 0f),
                new Vector3(5f, 0.04f, 5f));
            Rigidbody panel = panelCollider.gameObject.AddComponent<Rigidbody>();
            panel.isKinematic = true;
            panelCollider.transform.SetParent(
                chassisCollider.transform,
                worldPositionStays: true);
            Physics.SyncTransforms();

            yield return Settle(rig, 20);

            Assert.That(
                rig.Motor.LastSupportedRigidbody,
                Is.SameAs(chassis),
                "A kinematic hood/bootlid presentation must not hide the " +
                "dynamic chassis load receiver underneath it.");
            Assert.That(
                rig.Motor.LastSupportLoadImpulseNewtonSeconds.y,
                Is.LessThan(0f));
        }

        [UnityTest]
        public IEnumerator StaticGround_BlocksDynamicBodyBelowFromSupportLoad()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.02f, 0f));
            rig.CreateFloor(
                new Vector3(0f, -0.025f, 0f),
                new Vector3(5f, 0.05f, 5f));
            BoxCollider buriedBodyCollider = rig.CreateObstacle(
                "DynamicBodyBelowStaticGround",
                new Vector3(0f, -0.15f, 0f),
                new Vector3(5f, 0.1f, 5f));
            Rigidbody buriedBody = buriedBodyCollider.gameObject
                .AddComponent<Rigidbody>();
            buriedBody.mass = 600f;
            buriedBody.useGravity = false;
            buriedBody.constraints = RigidbodyConstraints.FreezeAll;
            Physics.SyncTransforms();

            yield return Settle(rig, 20);

            Assert.That(
                rig.Motor.LastSupportedRigidbody,
                Is.Null,
                "A static floor under the player's feet must occlude a " +
                "dynamic vehicle body deeper in the support probe.");
            Assert.That(
                rig.Motor.LastSupportLoadImpulseNewtonSeconds,
                Is.EqualTo(Vector3.zero));
        }

        [UnityTest]
        public IEnumerator SlopedVehicleSide_IsNeverTreatedAsFeetSupport()
        {
            using MotorRig rig = MotorRig.Create(
                new Vector3(0f, 0.02f, -1.2f));
            rig.CreateFloor(
                new Vector3(0f, -0.05f, 0f),
                new Vector3(5f, 0.1f, 5f));
            BoxCollider vehicleSide = rig.CreateObstacle(
                "SlopedVehicleSide",
                new Vector3(0f, 1.1f, 0.4f),
                new Vector3(1f, 2f, 1f));
            vehicleSide.transform.rotation = Quaternion.Euler(8f, 0f, 0f);
            Rigidbody body = vehicleSide.gameObject.AddComponent<Rigidbody>();
            body.mass = 389f;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            Physics.SyncTransforms();

            yield return Settle(rig, 8);
            rig.Motor.SetMoveInput(Vector2.up);
            yield return RunUntil(
                () => rig.Root.transform.position.z > -0.45f,
                maximumFrames: 90);
            bool wasMisclassifiedAsSupport = false;
            for (int frame = 0; frame < 15; frame++)
            {
                yield return null;
                wasMisclassifiedAsSupport |=
                    rig.Motor.LastSupportedRigidbody == body;
            }

            rig.Motor.ResetInputIntent();
            Assert.That(
                rig.Root.transform.position.z,
                Is.GreaterThan(-0.5f),
                "The player never reached the regression vehicle side.");
            Assert.That(
                wasMisclassifiedAsSupport,
                Is.False,
                "A shallow upward component on a vehicle side became a fake " +
                "feet support and received the player's full body weight.");
        }

        [UnityTest]
        public IEnumerator CrouchedPlayerAgainstVehicleRocker_DoesNotApplyWeight()
        {
            using MotorRig rig = MotorRig.Create(
                new Vector3(0f, 0.02f, -1.2f));
            rig.CreateFloor(
                new Vector3(0f, -0.05f, 0f),
                new Vector3(5f, 0.1f, 5f));

            var vehicle = new GameObject("CompoundVehicleBody");
            Rigidbody body = vehicle.AddComponent<Rigidbody>();
            body.mass = 600f;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            rig.Track(vehicle);

            BoxCollider rocker = rig.CreateChildObstacle(
                vehicle.transform,
                "LowRockerShelf",
                new Vector3(0f, 0.02f, 0f),
                new Vector3(1f, 0.04f, 0.5f));
            BoxCollider side = rig.CreateChildObstacle(
                vehicle.transform,
                "TallVehicleSide",
                new Vector3(0f, 0.6f, 0.2f),
                new Vector3(1f, 1.2f, 0.2f));
            Assert.That(rocker.attachedRigidbody, Is.SameAs(body));
            Assert.That(side.attachedRigidbody, Is.SameAs(body));

            Assert.That(
                rig.Motor.TrySetPosture(PlayerPosture.DeepCrouch),
                Is.True);
            Physics.SyncTransforms();
            yield return Settle(rig, 8);

            rig.Motor.SetMoveInput(Vector2.up);
            yield return RunUntil(
                () => rig.Root.transform.position.z > -0.2f,
                maximumFrames: 90);

            bool appliedFalseWeight = false;
            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;
                appliedFalseWeight |=
                    rig.Motor.LastSupportedRigidbody == body;
            }

            rig.Motor.ResetInputIntent();
            Assert.That(
                rig.Root.transform.position.z,
                Is.GreaterThan(-0.25f),
                "The crouched player never reached the compound vehicle side.");
            Assert.That(
                appliedFalseWeight,
                Is.False,
                "A low rocker must not become weight-bearing while the same " +
                "vehicle body blocks the capsule from the side.");
        }

        [UnityTest]
        public IEnumerator UnregisteredLightRigidbody_IsNotShovedByPlayerMotor()
        {
            using MotorRig rig = MotorRig.Create(
                new Vector3(0f, 0.02f, -1.2f));
            rig.CreateFloor(
                new Vector3(0f, -0.05f, 0f),
                new Vector3(5f, 0.1f, 5f));
            BoxCollider bodyCollider = rig.CreateObstacle(
                "UnregisteredVehicleCornerBody",
                new Vector3(0f, 0.5f, 0.4f),
                Vector3.one);
            Rigidbody body = bodyCollider.gameObject.AddComponent<Rigidbody>();
            body.mass = 20f;
            body.useGravity = false;
            body.constraints =
                RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotation;
            float initialZ = body.position.z;
            Physics.SyncTransforms();

            yield return Settle(rig, 8);
            rig.Motor.SetMoveInput(Vector2.up);
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
            }

            rig.Motor.ResetInputIntent();
            Assert.That(
                rig.Root.transform.position.z,
                Is.GreaterThan(-0.5f),
                "The player never reached the regression body.");
            Assert.That(
                body.position.z,
                Is.EqualTo(initialZ).Within(0.01f),
                "Mass alone must not opt a vehicle body into player pushing; " +
                "only an explicit pickup capability may do that.");
        }

        [UnityTest]
        public IEnumerator RollingHeavyRigidbody_DoesNotGainPlayerMomentum()
        {
            using MotorRig rig = MotorRig.Create(
                new Vector3(0f, 0.02f, -1.2f));
            rig.CreateFloor(
                new Vector3(0f, -0.05f, 0f),
                new Vector3(5f, 0.1f, 5f));
            BoxCollider bodyCollider = rig.CreateObstacle(
                "RollingVehicleBody",
                new Vector3(0f, 0.6f, 0.3f),
                Vector3.one);
            Rigidbody body = bodyCollider.gameObject.AddComponent<Rigidbody>();
            body.mass = 600f;
            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints =
                RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotation;
            const float initialSpeed = 0.2f;
            body.linearVelocity = Vector3.forward * initialSpeed;
            Physics.SyncTransforms();

            yield return Settle(rig, 8);
            body.linearVelocity = Vector3.forward * initialSpeed;
            rig.Motor.SetMoveInput(Vector2.up);
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
            }

            rig.Motor.ResetInputIntent();
            Assert.That(
                rig.Root.transform.position.z,
                Is.GreaterThan(-0.5f),
                "The player never reached the rolling regression body.");
            Assert.That(
                body.linearVelocity.z,
                Is.EqualTo(initialSpeed).Within(0.005f),
                "A non-pickup vehicle body that is already rolling must not " +
                "gain momentum from the player's CharacterController.");
        }

        [UnityTest]
        public IEnumerator KinematicPlayerProxy_PreventsHeavyBodyReversal()
        {
            using MotorRig rig = MotorRig.Create(
                new Vector3(0f, 0.02f, 1.6f));
            rig.Root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            rig.CreateFloor(
                new Vector3(0f, -0.05f, 0f),
                new Vector3(5f, 0.1f, 5f));
            BoxCollider bodyCollider = rig.CreateObstacle(
                "OncomingRollingVehicleBody",
                new Vector3(0f, 0.6f, 0.3f),
                Vector3.one);
            Rigidbody body = bodyCollider.gameObject.AddComponent<Rigidbody>();
            body.mass = 600f;
            body.useGravity = false;
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.constraints =
                RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotation;
            bodyCollider.excludeLayers = 1 << 9;
            bodyCollider.layerOverridePriority = 1;

            BoxCollider proxyCollider = rig.CreateChildObstacle(
                body.transform,
                "PlayerOnlyKinematicProxy",
                Vector3.zero,
                Vector3.one);
            Rigidbody proxyBody = proxyCollider.gameObject
                .AddComponent<Rigidbody>();
            proxyBody.isKinematic = true;
            proxyBody.useGravity = false;
            proxyBody.interpolation = RigidbodyInterpolation.Interpolate;
            proxyBody.collisionDetectionMode =
                CollisionDetectionMode.ContinuousSpeculative;
            proxyCollider.excludeLayers = ~(1 << 9);
            proxyCollider.layerOverridePriority = 1;
            const float initialSpeed = 0.2f;
            body.linearVelocity = Vector3.forward * initialSpeed;
            Assert.That(
                rig.Motor.TrySetPosture(PlayerPosture.DeepCrouch),
                Is.True);
            Physics.SyncTransforms();

            yield return Settle(rig, 8);
            body.linearVelocity = Vector3.forward * initialSpeed;
            rig.Motor.SetMoveInput(Vector2.up);
            float minimumForwardSpeed = initialSpeed;
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                minimumForwardSpeed = Mathf.Min(
                    minimumForwardSpeed,
                    body.linearVelocity.z);
            }

            rig.Motor.ResetInputIntent();
            Assert.That(
                rig.Root.transform.position.z,
                Is.LessThan(1.45f),
                "The crouched player never reached the oncoming body.");
            Assert.That(
                minimumForwardSpeed,
                Is.GreaterThanOrEqualTo(-0.01f),
                "The CharacterController reversed an oncoming 600 kg body.");
        }

        [UnityTest]
        public IEnumerator ExplicitPickupRigidbody_RemainsPlayerPushable()
        {
            using MotorRig rig = MotorRig.Create(
                new Vector3(0f, 0.02f, -1.2f));
            rig.CreateFloor(
                new Vector3(0f, -0.05f, 0f),
                new Vector3(5f, 0.1f, 5f));
            BoxCollider bodyCollider = rig.CreateObstacle(
                "ExplicitPushableItem",
                new Vector3(0f, 0.25f, 0.4f),
                new Vector3(0.5f, 0.5f, 0.5f));
            Rigidbody body = bodyCollider.gameObject.AddComponent<Rigidbody>();
            body.mass = 10f;
            body.useGravity = false;
            body.constraints =
                RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotation;
            StableEntityIdAuthoring identity = bodyCollider.gameObject
                .AddComponent<StableEntityIdAuthoring>();
            identity.InitializeExplicitRuntimeId(StableEntityId.New());
            PhysicsPickupTarget pickup = bodyCollider.gameObject
                .AddComponent<PhysicsPickupTarget>();
            pickup.Configure(body, identity, "Pick up", 35f);
            InteractionTargetHost host = bodyCollider.gameObject
                .AddComponent<InteractionTargetHost>();
            host.Configure(pickup);
            float initialZ = body.position.z;
            Physics.SyncTransforms();

            yield return Settle(rig, 8);
            rig.Motor.SetMoveInput(Vector2.up);
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
            }

            rig.Motor.ResetInputIntent();
            Assert.That(
                body.position.z,
                Is.GreaterThan(initialZ + 0.005f),
                "The explicit pickup capability should retain ordinary item " +
                "nudging after vehicle bodies are excluded.");
        }

        [UnityTest]
        public IEnumerator CoyoteWindowAcceptsJumpJustAfterLeavingAnEdge()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.02f, 0f));
            rig.CreateFloor(
                new Vector3(0f, -0.05f, 0f),
                new Vector3(2f, 0.1f, 1f));
            Physics.SyncTransforms();

            yield return Settle(rig, 8);
            rig.Motor.SetMoveInput(Vector2.up);
            yield return RunUntil(
                () => !rig.Motor.IsGrounded,
                maximumFrames: 120);
            Assert.That(rig.Motor.IsGrounded, Is.False);

            rig.Motor.RequestJump();
            yield return null;

            Assert.That(rig.Motor.IsPreparingJump, Is.True);
            yield return RunUntil(
                () => rig.Motor.VerticalSpeedMetersPerSecond > 6f,
                maximumFrames: 30);
            Assert.That(
                rig.Motor.VerticalSpeedMetersPerSecond,
                Is.GreaterThan(6f));
        }

        [UnityTest]
        public IEnumerator HorizontalFovAndHoldZoomRemainAspectCorrect()
        {
            var cameraObject = new GameObject("CcrFovTestCamera");
            cameraObject.SetActive(false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.aspect = 16f / 9f;
            FirstPersonCameraFieldOfView fieldOfView =
                cameraObject.AddComponent<FirstPersonCameraFieldOfView>();
            fieldOfView.Configure(camera, 120f, 0.5f, 0f);
            cameraObject.SetActive(true);

            try
            {
                Assert.That(
                    camera.fieldOfView,
                    Is.EqualTo(
                        FirstPersonCameraFieldOfView
                            .HorizontalToVerticalDegrees(120f, 16f / 9f))
                        .Within(0.001f));

                fieldOfView.SetZoomRequested(true);
                yield return null;
                Assert.That(
                    camera.fieldOfView,
                    Is.EqualTo(
                        FirstPersonCameraFieldOfView
                            .HorizontalToVerticalDegrees(60f, 16f / 9f))
                        .Within(0.001f));

                fieldOfView.enabled = false;
                Assert.That(
                    camera.fieldOfView,
                    Is.EqualTo(
                        FirstPersonCameraFieldOfView
                            .HorizontalToVerticalDegrees(120f, 16f / 9f))
                        .Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [UnityTest]
        public IEnumerator LookMotionAddsBoundedInertiaBodySwayAndReturnsToCenter()
        {
            var root = new GameObject("CameraInertiaTestRoot");
            root.SetActive(false);
            var motionPivot = new GameObject("MotionPivot");
            motionPivot.transform.SetParent(root.transform, false);
            var pitchPivot = new GameObject("PitchPivot");
            pitchPivot.transform.SetParent(motionPivot.transform, false);

            FirstPersonLook look = root.AddComponent<FirstPersonLook>();
            look.Configure(root.transform, pitchPivot.transform, false);
            FirstPersonCameraMotion motion =
                motionPivot.AddComponent<FirstPersonCameraMotion>();
            motion.Configure(null, look, motionPivot.transform);
            root.SetActive(true);

            try
            {
                look.ApplyLook(
                    new Vector2(10f, -5f),
                    isPointerDelta: true,
                    deltaTime: 1f / 60f);

                Assert.That(motion.CurrentYawInertiaDegrees, Is.LessThan(0f));
                Assert.That(motion.CurrentPitchInertiaDegrees, Is.LessThan(0f));
                Assert.That(motion.CurrentTurnSwayMeters, Is.LessThan(0f));
                Assert.That(
                    Mathf.DeltaAngle(0f, root.transform.eulerAngles.y),
                    Is.EqualTo(1f).Within(0.001f),
                    "Gameplay/body yaw did not follow the look input immediately.");
                Assert.That(
                    Mathf.Abs(motion.CurrentYawInertiaDegrees),
                    Is.LessThanOrEqualTo(1.25f));
                Assert.That(
                    Mathf.Abs(motion.CurrentPitchInertiaDegrees),
                    Is.LessThanOrEqualTo(0.85f));
                Assert.That(
                    Mathf.Abs(motion.CurrentTurnSwayMeters),
                    Is.LessThanOrEqualTo(0.018f));

                float initialYawMagnitude =
                    Mathf.Abs(motion.CurrentYawInertiaDegrees);
                yield return new WaitForSecondsRealtime(0.3f);

                Assert.That(
                    Mathf.Abs(motion.CurrentYawInertiaDegrees),
                    Is.LessThan(initialYawMagnitude * 0.25f));
                Assert.That(
                    Mathf.Abs(motion.CurrentTurnSwayMeters),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public IEnumerator OriginalWidthCapsule_PassesThroughThirtySixCentimeterOpening()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.02f, -1.5f));
            rig.CreateFloor(new Vector3(0f, -0.05f, 1f), new Vector3(5f, 0.1f, 8f));
            rig.CreateObstacle(
                "CorridorLeft",
                new Vector3(-0.28f, 1f, 1f),
                new Vector3(0.2f, 2f, 5f));
            rig.CreateObstacle(
                "CorridorRight",
                new Vector3(0.28f, 1f, 1f),
                new Vector3(0.2f, 2f, 5f));
            Physics.SyncTransforms();

            yield return Settle(rig, 8);
            rig.Motor.SetMoveInput(Vector2.up);
            yield return RunUntil(
                () => rig.Root.transform.position.z > 2.2f,
                maximumFrames: 240);

            Assert.That(
                rig.Root.transform.position.z,
                Is.GreaterThan(2.2f),
                "The player capsule was wedged in an opening wider than the donor capsule.");
        }

        [UnityTest]
        public IEnumerator StepOffset_RemainsAvailableAndClimbsTwentyEightCentimeterStep()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.02f, -1.5f));
            rig.CreateFloor(new Vector3(0f, -0.05f, 1f), new Vector3(5f, 0.1f, 8f));
            rig.CreateObstacle(
                "Threshold",
                new Vector3(0f, 0.14f, 1.25f),
                new Vector3(2f, 0.28f, 2f));
            Physics.SyncTransforms();

            yield return Settle(rig, 8);
            rig.Motor.SetMoveInput(Vector2.up);
            yield return RunUntil(
                () => rig.Root.transform.position.z > 1.7f,
                maximumFrames: 240);

            Assert.That(rig.Controller.stepOffset, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(
                rig.Root.transform.position.z,
                Is.GreaterThan(1.7f),
                "The motor did not cross a donor-compatible threshold.");
            Assert.That(
                rig.Root.transform.position.y,
                Is.GreaterThan(0.18f),
                "The CharacterController passed the obstacle without stepping onto it.");
        }

        [UnityTest]
        public IEnumerator ForwardLean_StopsHeadSphereBeforeWall()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.02f, 0f));
            rig.CreateFloor(new Vector3(0f, -0.05f, 0f), new Vector3(4f, 0.1f, 4f));
            BoxCollider wall = rig.CreateObstacle(
                "LeanWall",
                new Vector3(0f, 1.35f, 0.35f),
                new Vector3(2f, 1.5f, 0.1f));
            Physics.SyncTransforms();

            yield return Settle(rig, 4);
            rig.Motor.SetForwardLeanRequested(true);
            yield return RunUntil(
                () => rig.Motor.CurrentLeanAngleDegrees > 1f,
                maximumFrames: 60);
            yield return null;

            Assert.That(rig.Motor.CurrentLeanAngleDegrees, Is.GreaterThan(1f));
            Assert.That(rig.Motor.CurrentLeanAngleDegrees, Is.LessThan(39.5f));
            Assert.That(
                rig.CameraPivot.position.z + 0.11f,
                Is.LessThanOrEqualTo(wall.bounds.min.z + 0.012f),
                "The leaned head sphere crossed the wall plane.");
        }

        [UnityTest]
        public IEnumerator ForwardLean_UsesBodyHingeAndReturnsSmoothly()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.02f, 0f));
            rig.CreateFloor(
                new Vector3(0f, -0.05f, 0f),
                new Vector3(4f, 0.1f, 5f));
            Physics.SyncTransforms();

            yield return Settle(rig, 4);
            Vector3 standingCameraPosition = rig.CameraPivot.position;
            Vector3 bodyHingePosition = rig.LeanPivot.position;

            rig.Motor.SetForwardLeanRequested(true);
            yield return RunUntil(
                () => rig.Motor.CurrentLeanAngleDegrees >= 39.5f,
                maximumFrames: 60);

            Assert.That(
                Vector3.Distance(rig.LeanPivot.position, bodyHingePosition),
                Is.LessThan(0.0001f),
                "The donor body hinge must stay fixed below the player.");
            Assert.That(
                rig.CameraPivot.position.z - standingCameraPosition.z,
                Is.GreaterThan(1f),
                "Lean still behaves like a short neck tilt instead of the donor body arc.");
            Assert.That(
                standingCameraPosition.y - rig.CameraPivot.position.y,
                Is.GreaterThan(0.35f));

            float angleBeforeRelease = rig.Motor.CurrentLeanAngleDegrees;
            rig.Motor.SetForwardLeanRequested(false);
            yield return null;

            Assert.That(
                rig.Motor.CurrentLeanAngleDegrees,
                Is.LessThan(angleBeforeRelease));
            Assert.That(
                rig.Motor.CurrentLeanAngleDegrees,
                Is.GreaterThan(0.5f),
                "Releasing lean snapped straight to zero in one frame.");

            yield return RunUntil(
                () => rig.Motor.CurrentLeanAngleDegrees <= 0.01f,
                maximumFrames: 60);
            Assert.That(rig.Motor.CurrentLeanAngleDegrees, Is.Zero.Within(0.01f));
        }

        [UnityTest]
        public IEnumerator RunningForwardWhileLeaningIntoWall_RaisesSingleImpact()
        {
            using MotorRig rig = MotorRig.Create(new Vector3(0f, 0.02f, 0f));
            rig.CreateFloor(new Vector3(0f, -0.05f, 1f), new Vector3(5f, 0.1f, 7f));
            BoxCollider wall = rig.CreateObstacle(
                "ImpactWall",
                new Vector3(0f, 1f, 1.05f),
                new Vector3(3f, 2f, 0.1f));
            Physics.SyncTransforms();

            PlayerLeanImpact reportedImpact = default;
            int reportedCount = 0;
            rig.Motor.ForwardLeanImpactOccurred += impact =>
            {
                reportedImpact = impact;
                reportedCount++;
            };

            yield return Settle(rig, 8);
            rig.Motor.SetRunRequested(true);
            rig.Motor.SetMoveInput(Vector2.up);
            rig.Motor.SetForwardLeanRequested(true);
            yield return RunUntil(
                () => rig.Motor.LeanImpactCount > 0,
                maximumFrames: 180);
            rig.Motor.ResetInputIntent();

            Assert.That(rig.Motor.LeanImpactCount, Is.EqualTo(1));
            Assert.That(reportedCount, Is.EqualTo(1));
            Assert.That(reportedImpact.Collider, Is.SameAs(wall));
            Assert.That(
                reportedImpact.ForwardSpeedMetersPerSecond,
                Is.GreaterThan(3f));
            Assert.That(
                Vector3.Angle(reportedImpact.Normal, -rig.Root.transform.forward),
                Is.LessThan(30f));
            Assert.That(rig.Feedback.PresentedImpactCount, Is.EqualTo(1));
            Assert.That(rig.Feedback.IsEffectActive, Is.True);
        }

        private static IEnumerator Settle(MotorRig rig, int frameCount)
        {
            for (int frame = 0; frame < frameCount; frame++)
            {
                yield return null;
            }

            Assert.That(rig.Motor.IsGrounded, Is.True);
        }

        private static float EyeHeight(MotorRig rig) =>
            rig.CameraPivot.position.y - rig.Root.transform.position.y;

        private static IEnumerator RunUntil(
            System.Func<bool> predicate,
            int maximumFrames)
        {
            for (int frame = 0; frame < maximumFrames && !predicate(); frame++)
            {
                yield return null;
            }
        }

        private sealed class MotorRig : System.IDisposable
        {
            private readonly System.Collections.Generic.List<GameObject> objects =
                new System.Collections.Generic.List<GameObject>();
            private float previousCaptureDeltaTime;

            private MotorRig()
            {
            }

            public GameObject Root { get; private set; }
            public CharacterController Controller { get; private set; }
            public FirstPersonMotor Motor { get; private set; }
            public Transform LeanPivot { get; private set; }
            public Transform CameraPivot { get; private set; }
            public PlayerLeanImpactFeedbackPresenter Feedback { get; private set; }
            public FirstPersonCameraMotion Motion { get; private set; }

            public static MotorRig Create(Vector3 position)
            {
                var rig = new MotorRig();
                rig.previousCaptureDeltaTime = Time.captureDeltaTime;
                Time.captureDeltaTime = 1f / 60f;
                rig.Root = new GameObject("PlayerLocomotionParityRig");
                rig.Root.layer = 9;
                rig.Root.SetActive(false);
                rig.Root.transform.position = position;

                rig.Controller = rig.Root.AddComponent<CharacterController>();

                var leanPivot = new GameObject("LeanPivot");
                leanPivot.transform.SetParent(rig.Root.transform, false);
                leanPivot.transform.localPosition = new Vector3(0f, -0.3f, 0f);
                rig.LeanPivot = leanPivot.transform;

                var cameraPivot = new GameObject("CameraPivot");
                cameraPivot.transform.SetParent(leanPivot.transform, false);
                cameraPivot.transform.localPosition = new Vector3(0f, 1.78f, 0f);
                rig.CameraPivot = cameraPivot.transform;

                var impactPivot = new GameObject("ImpactPivot");
                impactPivot.transform.SetParent(cameraPivot.transform, false);

                var motionPivot = new GameObject("MotionPivot");
                motionPivot.transform.SetParent(
                    impactPivot.transform,
                    false);

                rig.Motor = rig.Root.AddComponent<FirstPersonMotor>();
                rig.Motor.Configure(
                    rig.Controller,
                    leanPivot.transform,
                    cameraPivot.transform,
                    impactPivot.transform);
                rig.Motion =
                    motionPivot.AddComponent<FirstPersonCameraMotion>();
                rig.Motion.Configure(
                    rig.Motor,
                    playerLook: null,
                    authoredMotionPivot: motionPivot.transform);
                rig.Feedback =
                    rig.Root.AddComponent<PlayerLeanImpactFeedbackPresenter>();
                rig.Feedback.Configure(rig.Motor);
                rig.Root.SetActive(true);
                rig.objects.Add(rig.Root);
                return rig;
            }

            public void CreateFloor(Vector3 position, Vector3 scale)
            {
                CreateObstacle("Floor", position, scale);
            }

            public BoxCollider CreateObstacle(
                string name,
                Vector3 position,
                Vector3 scale)
            {
                GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                obstacle.name = name;
                obstacle.transform.SetPositionAndRotation(position, Quaternion.identity);
                obstacle.transform.localScale = scale;
                objects.Add(obstacle);
                return obstacle.GetComponent<BoxCollider>();
            }

            public BoxCollider CreateChildObstacle(
                Transform parent,
                string name,
                Vector3 localPosition,
                Vector3 scale)
            {
                GameObject obstacle = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                obstacle.name = name;
                obstacle.transform.SetParent(parent, false);
                obstacle.transform.localPosition = localPosition;
                obstacle.transform.localRotation = Quaternion.identity;
                obstacle.transform.localScale = scale;
                return obstacle.GetComponent<BoxCollider>();
            }

            public void Track(GameObject value)
            {
                if (value != null && !objects.Contains(value))
                {
                    objects.Add(value);
                }
            }

            public void Dispose()
            {
                Time.captureDeltaTime = previousCaptureDeltaTime;
                for (int index = objects.Count - 1; index >= 0; index--)
                {
                    if (objects[index] != null)
                    {
                        Object.DestroyImmediate(objects[index]);
                    }
                }
            }
        }
    }
}
