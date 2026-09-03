using MSC.Characters;
using MSC.Vehicle.NWH;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using NWH.WheelController3D;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MSC.Tests.EditMode.VehicleNwhIntegration
{
    public sealed class NwhWheelPhysicsBackendTests
    {
        private GameObject root;

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Configure_AcceptsDynamicChassisAndChildWheels()
        {
            NwhWheelPhysicsBackend backend = CreateBackend(out _, out _);

            Assert.That(backend.TryValidate(out string failure), Is.True, failure);
            Assert.That(backend.WheelCount, Is.EqualTo(4));
        }

        [Test]
        public void Apply_TransfersProjectCommandsWithoutMovingTheChassis()
        {
            NwhWheelPhysicsBackend backend = CreateBackend(
                out Rigidbody chassis,
                out WheelController[] wheels);
            Vector3 initialPosition = chassis.position;
            var commands = new[]
            {
                new WheelPhysicsCommand(125f, 10f, 12f),
                new WheelPhysicsCommand(120f, 11f, 11f),
                new WheelPhysicsCommand(115f, 12f, 0f),
                new WheelPhysicsCommand(110f, 13f, 0f),
            };

            backend.Apply(0.02f, commands);

            Assert.That(wheels[0].MotorTorque, Is.EqualTo(125f));
            Assert.That(wheels[0].BrakeTorque, Is.EqualTo(10f));
            Assert.That(wheels[0].SteerAngle, Is.EqualTo(12f));
            Assert.That(wheels[3].MotorTorque, Is.EqualTo(110f));
            Assert.That(chassis.position, Is.EqualTo(initialPosition));
            Assert.That(chassis.isKinematic, Is.False);
        }

        [Test]
        public void Reset_ClearsAllWheelCommandsAndAngularState()
        {
            NwhWheelPhysicsBackend backend = CreateBackend(
                out _,
                out WheelController[] wheels);
            wheels[0].MotorTorque = 100f;
            wheels[0].BrakeTorque = 25f;
            wheels[0].SteerAngle = -8f;
            wheels[0].wheel.angularVelocity = 40f;

            backend.Reset();

            Assert.That(wheels[0].MotorTorque, Is.Zero);
            Assert.That(wheels[0].BrakeTorque, Is.Zero);
            Assert.That(wheels[0].SteerAngle, Is.Zero);
            Assert.That(wheels[0].AngularVelocity, Is.Zero);
        }

        [Test]
        public void Sample_RejectsUndersizedBuffer()
        {
            NwhWheelPhysicsBackend backend = CreateBackend(out _, out _);

            Assert.Throws<System.ArgumentException>(() =>
                backend.Sample(0.02f, new WheelPhysicsSample[3]));
        }

        [Test]
        public void AxleStability_LeftCompression_LiftsLeftAndPushesRightDown()
        {
            Vector2 upwardForces =
                NwhWheelPhysicsBackend.CalculateAntiRollUpwardForces(
                    leftGrounded: true,
                    leftCompression: 0.8f,
                    rightGrounded: true,
                    rightCompression: 0.3f,
                    forceNewtons: 2000f);

            Assert.That(upwardForces.x, Is.EqualTo(1000f).Within(0.001f));
            Assert.That(upwardForces.y, Is.EqualTo(-1000f).Within(0.001f));
        }

        [Test]
        public void AxleStability_RightCompression_LiftsRightAndPushesLeftDown()
        {
            Vector2 upwardForces =
                NwhWheelPhysicsBackend.CalculateAntiRollUpwardForces(
                    leftGrounded: true,
                    leftCompression: 0.2f,
                    rightGrounded: true,
                    rightCompression: 0.7f,
                    forceNewtons: 1500f);

            Assert.That(upwardForces.x, Is.EqualTo(-750f).Within(0.001f));
            Assert.That(upwardForces.y, Is.EqualTo(750f).Within(0.001f));
        }

        [Test]
        public void AxleStability_UngroundedSide_DoesNotReceiveForce()
        {
            Vector2 upwardForces =
                NwhWheelPhysicsBackend.CalculateAntiRollUpwardForces(
                    leftGrounded: false,
                    leftCompression: 1f,
                    rightGrounded: true,
                    rightCompression: 0.4f,
                    forceNewtons: 1000f);

            Assert.That(upwardForces.x, Is.Zero);
            Assert.That(upwardForces.y, Is.EqualTo(400f).Within(0.001f));
        }

        [Test]
        public void GroundedLoadedWheel_AtStationaryChassis_ClampsResidualSpin()
        {
            Assert.That(
                NwhWheelPhysicsBackend.ShouldClampStationaryWheel(
                    chassisPlanarSpeedMetersPerSecond: 0f,
                    chassisAngularSpeedRadiansPerSecond: 0f,
                    grounded: true,
                    loadNewtons: 850f,
                    driveTorqueNewtonMeters: 0f,
                    wheelAngularSpeedRadiansPerSecond: 0.32f),
                Is.True);
        }

        [TestCase(0.04f, 0f, true, 850f, 0f, 0.32f)]
        [TestCase(0f, 0.06f, true, 850f, 0f, 0.32f)]
        [TestCase(0f, 0f, false, 850f, 0f, 0.32f)]
        [TestCase(0f, 0f, true, 0f, 0f, 0.32f)]
        [TestCase(0f, 0f, true, 850f, 1f, 0.32f)]
        [TestCase(0f, 0f, true, 850f, 0f, 1f)]
        public void MovingOrDrivenWheel_DoesNotUseStationaryRestClamp(
            float chassisPlanarSpeed,
            float chassisAngularSpeed,
            bool grounded,
            float load,
            float driveTorque,
            float wheelAngularSpeed)
        {
            Assert.That(
                NwhWheelPhysicsBackend.ShouldClampStationaryWheel(
                    chassisPlanarSpeed,
                    chassisAngularSpeed,
                    grounded,
                    load,
                    driveTorque,
                    wheelAngularSpeed),
                Is.False);
        }

        [Test]
        public void Backend_ExecutesAfterNwhWheelContactSimulation()
        {
            DefaultExecutionOrder executionOrder =
                typeof(NwhWheelPhysicsBackend)
                    .GetCustomAttributes(typeof(DefaultExecutionOrder), false)
                    .Cast<DefaultExecutionOrder>()
                    .Single();

            Assert.That(executionOrder.order, Is.EqualTo(150));
        }

        [Test]
        public void SuspensionOffset_CompressionMovesWheelUpFromAuthoredRest()
        {
            Vector3 offset = NwhStoryTrafficVehicleMotionBackend
                .CalculateSuspensionLocalOffset(
                    Vector3.up,
                    restLengthMeters: 0.112f,
                    currentLengthMeters: 0.052f);

            Assert.That(offset.x, Is.Zero.Within(0.0001f));
            Assert.That(offset.y, Is.EqualTo(0.06f).Within(0.0001f));
            Assert.That(offset.z, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void Presentation_AppliesOptionalSuspensionOffsetToSpinPivot()
        {
            root = new GameObject("Wheel_Pose_Presentation_Test");
            var pivots = new Transform[4];
            for (int index = 0; index < pivots.Length; index++)
            {
                var pivot = new GameObject("WheelSpinPivot_" + index);
                pivot.transform.SetParent(root.transform, false);
                pivot.transform.localPosition = new Vector3(index, 0.4f, 0f);
                pivots[index] = pivot.transform;
            }

            OffsetMotionBackend motion =
                root.AddComponent<OffsetMotionBackend>();
            motion.LocalSuspensionOffset = new Vector3(0f, 0.075f, 0f);
            StoryTrafficVehiclePresentationBinding presentation =
                root.AddComponent<StoryTrafficVehiclePresentationBinding>();
            presentation.ConfigureForAuthoring(
                "P1.NPC.999",
                System.Array.Empty<string>(),
                System.Array.Empty<GameObject>(),
                pivots,
                configuredWheelDegreesPerMeter: 190f,
                configuredGroundContactCalibrationMeters: 0f);
            presentation.ConfigureMotionBackendForAuthoring(motion);

            MethodInfo lateUpdate = typeof(
                    StoryTrafficVehiclePresentationBinding)
                .GetMethod(
                    "LateUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lateUpdate, Is.Not.Null);
            lateUpdate.Invoke(presentation, null);

            Assert.That(pivots[0].localPosition.x, Is.Zero.Within(0.0001f));
            Assert.That(
                pivots[0].localPosition.y,
                Is.EqualTo(0.475f).Within(0.0001f));
            Assert.That(
                pivots[3].localPosition.x,
                Is.EqualTo(3f).Within(0.0001f));
            Assert.That(
                pivots[3].localPosition.y,
                Is.EqualTo(0.475f).Within(0.0001f));
        }

        [Test]
        public void StoryTrafficGearSelection_UpshiftsOneRatioAtATimeFromRpm()
        {
            int selected = NwhStoryTrafficVehicleMotionBackend.SelectForwardGear(
                desiredSpeedMetersPerSecond: 35f,
                currentSpeedMetersPerSecond: 16f,
                forwardGearCount: 5,
                currentGear: 1,
                engineRpm: 7000f,
                configuredUpshiftRpm: 6900f,
                configuredDownshiftRpm: 2500f);

            Assert.That(selected, Is.EqualTo(2));
        }

        [Test]
        public void StoryTrafficGearSelection_DownshiftsOneRatioAtLowRpm()
        {
            int selected = NwhStoryTrafficVehicleMotionBackend.SelectForwardGear(
                desiredSpeedMetersPerSecond: 28f,
                currentSpeedMetersPerSecond: 8f,
                forwardGearCount: 5,
                currentGear: 4,
                engineRpm: 1800f,
                configuredUpshiftRpm: 6900f,
                configuredDownshiftRpm: 2500f);

            Assert.That(selected, Is.EqualTo(3));
        }

        [Test]
        public void StoryTrafficGearSelection_DoesNotUpshiftStationaryWheelspin()
        {
            int selected = NwhStoryTrafficVehicleMotionBackend.SelectForwardGear(
                desiredSpeedMetersPerSecond: 35f,
                currentSpeedMetersPerSecond: 0.2f,
                forwardGearCount: 5,
                currentGear: 1,
                engineRpm: 7200f,
                configuredUpshiftRpm: 6900f,
                configuredDownshiftRpm: 2500f);

            Assert.That(selected, Is.EqualTo(1));
        }

        [Test]
        public void StoryTrafficGearSelection_UpshiftsBeforeFirstGearSpeedBand()
        {
            int selected = NwhStoryTrafficVehicleMotionBackend.SelectForwardGear(
                desiredSpeedMetersPerSecond: 30f,
                currentSpeedMetersPerSecond: 7f,
                forwardGearCount: 5,
                currentGear: 1,
                engineRpm: 6500f,
                configuredUpshiftRpm: 6900f,
                configuredDownshiftRpm: 2500f);

            Assert.That(selected, Is.EqualTo(2),
                "First gear must not require a road speed it cannot reach.");
        }

        [Test]
        public void StoryTrafficGearSelection_DoesNotUndoUpshiftDuringClutchRpmDip()
        {
            int selected = NwhStoryTrafficVehicleMotionBackend.SelectForwardGear(
                desiredSpeedMetersPerSecond: 35f,
                currentSpeedMetersPerSecond: 16f,
                forwardGearCount: 5,
                currentGear: 2,
                engineRpm: 900f,
                configuredUpshiftRpm: 6900f,
                configuredDownshiftRpm: 2500f);

            Assert.That(selected, Is.EqualTo(2),
                "A normal RPM dip while the clutch is open must not undo a road-speed-correct upshift.");
        }

        [Test]
        public void StoryTrafficGearSelection_RoadSpeedCanAdvanceHeavyVehicleRatio()
        {
            int selected = NwhStoryTrafficVehicleMotionBackend.SelectForwardGear(
                desiredSpeedMetersPerSecond: 23f,
                currentSpeedMetersPerSecond: 22f,
                forwardGearCount: 6,
                currentGear: 2,
                engineRpm: 1200f,
                configuredUpshiftRpm: 2200f,
                configuredDownshiftRpm: 900f);

            Assert.That(selected, Is.EqualTo(3),
                "A loaded bus must not remain in a low ratio solely because clutch slip holds RPM below its upshift landmark.");
        }

        [Test]
        public void StoryTrafficGearSelection_DoesNotStackHeavyVehicleShiftsOnClutchSlip()
        {
            int selected = NwhStoryTrafficVehicleMotionBackend.SelectForwardGear(
                desiredSpeedMetersPerSecond: 23f,
                currentSpeedMetersPerSecond: 1.8f,
                forwardGearCount: 6,
                currentGear: 2,
                engineRpm: 2300f,
                configuredUpshiftRpm: 2200f,
                configuredDownshiftRpm: 900f,
                minimumRpmUpshiftSpeedMetersPerSecond: 2.6f);

            Assert.That(selected, Is.EqualTo(2),
                "Clutch slip must not walk a bus through tall ratios before the chassis catches up.");
        }

        [Test]
        public void StoryTrafficGearSelection_AllowsHeavyVehicleShiftAtSynchronousSpeed()
        {
            int selected = NwhStoryTrafficVehicleMotionBackend.SelectForwardGear(
                desiredSpeedMetersPerSecond: 23f,
                currentSpeedMetersPerSecond: 3.2f,
                forwardGearCount: 6,
                currentGear: 2,
                engineRpm: 2300f,
                configuredUpshiftRpm: 2200f,
                configuredDownshiftRpm: 900f,
                minimumRpmUpshiftSpeedMetersPerSecond: 2.6f);

            Assert.That(selected, Is.EqualTo(3));
        }

        [Test]
        public void StoryTrafficGearSelection_HoldsBusThirdGearAboveDownshiftRpm()
        {
            int selected = NwhStoryTrafficVehicleMotionBackend.SelectForwardGear(
                desiredSpeedMetersPerSecond: 22.916668f,
                currentSpeedMetersPerSecond: 6.5f,
                forwardGearCount: 6,
                currentGear: 3,
                engineRpm: 1300f,
                configuredUpshiftRpm: 2200f,
                configuredDownshiftRpm: 900f,
                minimumRpmUpshiftSpeedMetersPerSecond: 8f);

            Assert.That(selected, Is.EqualTo(3),
                "Passenger-car speed bands must not force a loaded bus " +
                "back into second while its diesel is above downshift RPM.");
        }

        [Test]
        public void StoryTrafficGearSelection_DownshiftsTallBusRatioBelowUsefulBand()
        {
            int selected = NwhStoryTrafficVehicleMotionBackend.SelectForwardGear(
                desiredSpeedMetersPerSecond: 22.916668f,
                currentSpeedMetersPerSecond: 10f,
                forwardGearCount: 6,
                currentGear: 5,
                engineRpm: 950f,
                configuredUpshiftRpm: 2200f,
                configuredDownshiftRpm: 1100f,
                minimumRpmUpshiftSpeedMetersPerSecond: 14f);

            Assert.That(selected, Is.EqualTo(4),
                "The bus must leave fifth when road speed drags the diesel " +
                "below its useful band.");
        }

        [Test]
        public void StoryTrafficHillGear_SequentiallyLeavesTallBusRatio()
        {
            var gearbox = new GearboxSimulationConfig();
            gearbox.Configure(
                configuredReverseRatio: -6.62f,
                configuredForwardRatios: new[]
                {
                    7.41f,
                    4.27f,
                    2.75f,
                    1.84f,
                    1.24f,
                    1f,
                },
                configuredFinalDriveRatio: 4.7f,
                configuredEfficiency: 0.92f);

            int selected = NwhStoryTrafficVehicleMotionBackend
                .SelectHillClimbGear(
                    currentSpeedMetersPerSecond: 10f,
                    wheelRadiusMeters: 0.6031f,
                    gearbox: gearbox,
                    currentGear: 5,
                    ordinaryDesiredGear: 5,
                    minimumLoadedRpm: 1100f);

            Assert.That(selected, Is.EqualTo(4),
                "The wastewater climb must request one physical kick-down, " +
                "not retain fifth or jump across the sequential gearbox.");
        }

        [Test]
        public void StoryTrafficHillGear_HoldsTallestLoadedBusRatio()
        {
            var gearbox = new GearboxSimulationConfig();
            gearbox.Configure(
                configuredReverseRatio: -6.62f,
                configuredForwardRatios: new[]
                {
                    7.41f,
                    4.27f,
                    2.75f,
                    1.84f,
                    1.24f,
                    1f,
                },
                configuredFinalDriveRatio: 4.7f,
                configuredEfficiency: 0.92f);

            int selected = NwhStoryTrafficVehicleMotionBackend
                .SelectHillClimbGear(
                    currentSpeedMetersPerSecond: 10f,
                    wheelRadiusMeters: 0.6031f,
                    gearbox: gearbox,
                    currentGear: 4,
                    ordinaryDesiredGear: 4,
                    minimumLoadedRpm: 1100f);

            Assert.That(selected, Is.EqualTo(4),
                "Fourth provides the tallest loaded ratio at 10 m/s and " +
                "must not hunt between gears on the climb.");
        }

        [Test]
        public void StoryTrafficTerminalControl_StopsEngineAndAppliesServiceBrake()
        {
            NwhWheelPhysicsBackend wheelBackend = CreateBackend(
                out Rigidbody chassis,
                out WheelController[] wheels);
            VehicleSimulationConfig config = ScriptableObject.CreateInstance<
                VehicleSimulationConfig>();
            try
            {
                config.ApplyProvisionalPrototypeDefaults();
                NwhStoryTrafficVehicleMotionBackend motion =
                    root.AddComponent<NwhStoryTrafficVehicleMotionBackend>();
                motion.Configure(chassis, wheelBackend, config);

                var drivingCommand = new StoryTrafficVehicleDriveCommand(
                    Vector3.forward * 30f,
                    Vector3.forward,
                    desiredSpeedMetersPerSecond: 25f,
                    reverse: false,
                    fullBrake: false,
                    handbrake: false);
                for (int step = 0; step < 100; step++)
                {
                    motion.Step(0.02f, in drivingCommand);
                }

                Assert.That(motion.EngineRpm, Is.GreaterThan(100f),
                    "The fixture never established a running/cranking engine.");
                motion.SetTerminallyDisabled(true);
                for (int step = 0; step < 500; step++)
                {
                    motion.Step(0.02f, in drivingCommand);
                }

                Assert.That(motion.IsTerminallyDisabled, Is.True);
                Assert.That(motion.EngineRpm, Is.Zero.Within(0.001f));
                Assert.That(
                    wheels.All(wheel =>
                        Mathf.Abs(wheel.MotorTorque) <= 0.001f),
                    Is.True,
                    "A terminal wreck retained driven-wheel torque.");
                Assert.That(
                    wheels.All(wheel => wheel.BrakeTorque > 0f),
                    Is.True,
                    "The donor terminal hold did not retain service braking.");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        private NwhWheelPhysicsBackend CreateBackend(
            out Rigidbody chassis,
            out WheelController[] wheels)
        {
            root = new GameObject("NWH_Backend_Test");
            chassis = root.AddComponent<Rigidbody>();
            chassis.isKinematic = false;
            wheels = new WheelController[4];
            for (int index = 0; index < wheels.Length; index++)
            {
                var wheelObject = new GameObject("Wheel_" + index);
                wheelObject.transform.SetParent(root.transform, false);
                wheels[index] = wheelObject.AddComponent<WheelController>();
            }

            NwhWheelPhysicsBackend backend =
                root.AddComponent<NwhWheelPhysicsBackend>();
            backend.Configure(chassis, wheels);
            return backend;
        }

        private sealed class OffsetMotionBackend : MonoBehaviour,
            IStoryTrafficVehicleMotionBackend,
            IStoryTrafficWheelPoseBackend
        {
            public Vector3 LocalSuspensionOffset { get; set; }

            public bool IsOperational => true;
            public float SpeedMetersPerSecond => 0f;
            public Vector3 VelocityMetersPerSecond => Vector3.zero;
            public bool HasGroundContact => true;
            public float EngineRpm => 900f;
            public float EngineRedlineRpm => 6000f;
            public float EngineLoad01 => 0f;
            public int SelectedGear => 1;

            public void Step(
                float fixedDeltaSeconds,
                in StoryTrafficVehicleDriveCommand command)
            {
            }

            public void SnapToPose(
                Vector3 position,
                Quaternion rotation,
                float forwardSpeedMetersPerSecond)
            {
            }

            public void ResetMotion()
            {
            }

            public bool TryGetWheelVisualState(
                int wheelIndex,
                out float steeringAngleDegrees,
                out float axleAngleDegrees)
            {
                steeringAngleDegrees = 0f;
                axleAngleDegrees = 0f;
                return wheelIndex >= 0 && wheelIndex < 4;
            }

            public bool TryGetWheelSuspensionOffset(
                int wheelIndex,
                out Vector3 localPositionOffset)
            {
                localPositionOffset = LocalSuspensionOffset;
                return wheelIndex >= 0 && wheelIndex < 4;
            }
        }
    }
}
