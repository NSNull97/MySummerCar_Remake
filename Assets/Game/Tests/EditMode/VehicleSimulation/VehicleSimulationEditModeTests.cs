using System;
using MSC.Audio;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.VehicleSimulation
{
    public sealed class VehicleSimulationEditModeTests
    {
        private const float FixedDeltaSeconds = 0.02f;

        private VehicleSimulationConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<VehicleSimulationConfig>();
            config.ApplyProvisionalPrototypeDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(config);
        }

        [Test]
        public void ProvisionalConfig_ValidatesAndCarriesExplicitProvenance()
        {
            Assert.That(config.Validate(out string failure), Is.True, failure);
            Assert.That(config.ConfigurationId, Is.EqualTo("m06.prototype.satsuma"));
            Assert.That(config.DynamicTuning.IsProvisional, Is.True);
            Assert.That(
                config.DynamicTuning.Classification,
                Is.EqualTo(VehicleReferenceClassification.RemakeDesignTarget));
            Assert.That(config.DynamicTuning.Label, Is.EqualTo(VehicleSimulationConfig.PrototypeTuningLabel));
            Assert.That(config.DynamicTuning.Source, Is.EqualTo("ProjectAuthored/Milestone06"));
            Assert.That(config.DynamicTuning.Notes, Does.Contain("04B.4 dynamic fixtures are Missing"));
            Assert.That(
                config.GeometryReference.Classification,
                Is.EqualTo(VehicleReferenceClassification.DerivedReference));
            Assert.That(config.GeometryReference.Label, Is.EqualTo("04B.4VehicleWheelAnchors"));

            System.Reflection.FieldInfo inertiaField = typeof(EngineSimulationConfig).GetField(
                "inertiaKilogramSquareMeters",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That(inertiaField, Is.Not.Null);
            inertiaField.SetValue(config.Engine, float.NaN);
            Assert.That(config.Validate(out string nonFiniteFailure), Is.False);
            Assert.That(nonFiniteFailure, Does.Contain("finite"));
        }

        [Test]
        public void TorqueCurve_InterpolatesAndClampsToEndpointSamples()
        {
            var engine = new EngineSimulation();

            Assert.That(engine.InterpolateTorque(config.Engine, -100f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(engine.InterpolateTorque(config.Engine, 350f), Is.EqualTo(34f).Within(0.0001f));
            Assert.That(engine.InterpolateTorque(config.Engine, 1250f), Is.EqualTo(82f).Within(0.0001f));
            Assert.That(engine.InterpolateTorque(config.Engine, 3500f), Is.EqualTo(108f).Within(0.0001f));
            Assert.That(engine.InterpolateTorque(config.Engine, 8000f), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void VehicleAudioParameters_SanitizePresentationInputsWithoutAffectingSimulation()
        {
            var parameters = new VehicleAudioParameters(
                VehicleAudioEngineState.Running,
                float.NaN,
                float.NegativeInfinity,
                float.PositiveInfinity,
                2f,
                3,
                float.NegativeInfinity,
                -12f,
                -2f,
                VehicleAudioSurface.Gravel,
                -5f,
                float.PositiveInfinity);

            Assert.That(parameters.EngineRpm, Is.Zero);
            Assert.That(parameters.RedlineRpm, Is.EqualTo(1f));
            Assert.That(parameters.EngineLoad01, Is.Zero);
            Assert.That(parameters.Throttle01, Is.EqualTo(1f));
            Assert.That(parameters.SelectedGear, Is.EqualTo(3));
            Assert.That(parameters.ClutchSlipRpm, Is.Zero);
            Assert.That(parameters.VehicleSpeedMetersPerSecond, Is.EqualTo(12f));
            Assert.That(parameters.WheelSlip01, Is.EqualTo(1f));
            Assert.That(parameters.Surface, Is.EqualTo(VehicleAudioSurface.Gravel));
            Assert.That(parameters.BatteryVoltage, Is.Zero);
            Assert.That(parameters.EngineTemperatureCelsius, Is.Zero);
        }

        [Test]
        public void StarterAndPrerequisiteFlags_ExposeExplicitCrankFailures()
        {
            var starter = new StarterSimulation();
            Assert.That(
                starter.CalculateTorque(config.Engine, 0f, requested: true, canCrank: true),
                Is.EqualTo(config.Engine.StarterTorqueNewtonMeters));
            Assert.That(starter.CalculateTorque(config.Engine, 0f, true, false), Is.Zero);
            Assert.That(starter.CalculateTorque(config.Engine, 0f, false, true), Is.Zero);
            Assert.That(
                starter.CalculateTorque(
                    config.Engine,
                    config.Engine.StarterMaximumRpm + 1f,
                    true,
                    true),
                Is.Zero);

            var prerequisites = new VehicleSimulationPrerequisites();
            prerequisites.Reset();
            Assert.That(prerequisites.CanCrank, Is.True);
            prerequisites.Add(VehicleSimulationPrerequisiteFailure.BatteryVoltageLow);
            Assert.That(prerequisites.CanCrank, Is.False);
            Assert.That(prerequisites.CanRun, Is.True);
            prerequisites.Add(VehicleSimulationPrerequisiteFailure.FuelUnavailable);
            Assert.That(prerequisites.CanRun, Is.False);
        }

        [Test]
        public void EngineFriction_ReducesAngularSpeedWithoutCombustion()
        {
            var state = new VehicleSimulationState();
            state.Reset(config);
            VehicleSimulationStateDto dto = state.CaptureDto();
            dto.engineStatus = VehicleEngineStatus.Off;
            dto.engineRpm = 1000f;
            Assert.That(state.TryRestoreDto(dto, config), Is.True);
            var prerequisites = new VehicleSimulationPrerequisites();
            prerequisites.Reset();

            new EngineSimulation().Step(
                config,
                state,
                VehicleInputState.Neutral(),
                prerequisites,
                clutchLoadTorque: 0f,
                deltaSeconds: FixedDeltaSeconds,
                starter: new StarterSimulation());

            Assert.That(state.EngineStatus, Is.EqualTo(VehicleEngineStatus.Off));
            Assert.That(state.EngineRpm, Is.LessThan(1000f));
            Assert.That(state.EngineRpm, Is.GreaterThan(900f));
        }

        [Test]
        public void Engine_StartsSettlesAtIdleAndStallsUnderLockedDrivetrainLoad()
        {
            var backend = new RecordingWheelBackend(config.WheelCount);
            var root = CreateRoot(backend);

            StartAndSettleAtIdle(root, FixedDeltaSeconds);

            Assert.That(root.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running));
            Assert.That(
                root.State.EngineRpm,
                Is.EqualTo(config.Engine.IdleTargetRpm).Within(40f));

            root.Tick(
                FixedDeltaSeconds,
                Input(
                    ignition: true,
                    clutchPedal: 1f,
                    gearChange: true,
                    requestedGear: 1));
            Assert.That(root.State.SelectedGear, Is.EqualTo(1));

            VehicleInputState lockedDrivetrain = Input(ignition: true, clutchPedal: 0f);
            for (int tick = 0;
                 tick < 100 && root.State.EngineStatus != VehicleEngineStatus.Stalled;
                 tick++)
            {
                root.Tick(FixedDeltaSeconds, lockedDrivetrain);
            }

            Assert.That(root.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Stalled));
            Assert.That(root.State.EngineRpm, Is.LessThan(config.Engine.StallRpm));

            for (int tick = 0; tick < 20; tick++)
            {
                root.Tick(FixedDeltaSeconds, lockedDrivetrain);
            }

            Assert.That(
                root.State.EngineStatus,
                Is.EqualTo(VehicleEngineStatus.Stalled),
                "A stalled engine must not restart without an explicit starter request.");
        }

        [Test]
        public void Engine_IgnitionOffShutsDownAndRpmDecays()
        {
            var root = CreateRoot(new RecordingWheelBackend(config.WheelCount));
            StartAndSettleAtIdle(root, FixedDeltaSeconds);
            float runningRpm = root.State.EngineRpm;

            root.Tick(FixedDeltaSeconds, Input(ignition: false, clutchPedal: 1f));

            Assert.That(root.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Off));
            Assert.That(root.State.EngineRpm, Is.LessThan(runningRpm));
            Assert.That(
                root.Prerequisites.HasAny(VehicleSimulationPrerequisiteFailure.IgnitionOff),
                Is.True);
            Assert.That(root.State.IsFinite(), Is.True);
        }

        [Test]
        public void Clutch_UsesPedalEngagementSlipAndTorqueCapacity()
        {
            var clutch = new ClutchSimulation();
            Assert.That(clutch.CalculateEngagement(config.Clutch, 0f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(clutch.CalculateEngagement(config.Clutch, 1f), Is.EqualTo(0f).Within(0.0001f));

            float transferred = clutch.CalculateTransferTorque(
                config.Clutch,
                engineRadiansPerSecond: 100f,
                gearboxInputRadiansPerSecond: 0f,
                clutchPedal01: 0f,
                out float positiveSlip);
            Assert.That(positiveSlip, Is.EqualTo(100f).Within(0.0001f));
            Assert.That(transferred, Is.EqualTo(config.Clutch.MaximumTorqueNewtonMeters).Within(0.0001f));

            float reverseTransfer = clutch.CalculateTransferTorque(
                config.Clutch,
                engineRadiansPerSecond: 0f,
                gearboxInputRadiansPerSecond: 100f,
                clutchPedal01: 0f,
                out float negativeSlip);
            Assert.That(negativeSlip, Is.EqualTo(-100f).Within(0.0001f));
            Assert.That(reverseTransfer, Is.EqualTo(-config.Clutch.MaximumTorqueNewtonMeters).Within(0.0001f));
            Assert.That(
                clutch.CalculateTransferTorque(config.Clutch, 100f, 0f, 1f, out _),
                Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Gearbox_MapsNeutralReverseForwardAndRejectsInvalidShift()
        {
            var gearbox = new GearboxSimulation();
            Assert.That(gearbox.GetRatio(config.Gearbox, 0), Is.Zero);
            Assert.That(gearbox.GetRatio(config.Gearbox, -1), Is.EqualTo(-3.25f).Within(0.0001f));
            Assert.That(gearbox.GetRatio(config.Gearbox, 1), Is.EqualTo(3.5f).Within(0.0001f));
            Assert.That(gearbox.GetRatio(config.Gearbox, 5), Is.EqualTo(0.82f).Within(0.0001f));

            int selectedGear = 0;
            Assert.That(gearbox.TrySelectGear(config.Gearbox, 5, ref selectedGear), Is.True);
            Assert.That(selectedGear, Is.EqualTo(5));
            Assert.That(gearbox.TrySelectGear(config.Gearbox, 6, ref selectedGear), Is.False);
            Assert.That(selectedGear, Is.EqualTo(5));
            Assert.That(
                gearbox.CalculateOutputTorque(config.Gearbox, 1, 100f),
                Is.EqualTo(1228.5f).Within(0.001f));
        }

        [Test]
        public void DifferentialBrakeSteeringAndSuspension_ProduceBoundedOutputs()
        {
            var differential = new DifferentialSimulation();
            differential.DistributeOpenDifferential(240f, out float left, out float right);
            Assert.That(left, Is.EqualTo(120f).Within(0.0001f));
            Assert.That(right, Is.EqualTo(120f).Within(0.0001f));
            Assert.That(left + right, Is.EqualTo(240f).Within(0.0001f));

            var wheelSimulation = new WheelSimulation();
            var wheelCommands = new WheelPhysicsCommand[config.WheelCount];
            wheelSimulation.BuildCommands(
                config,
                leftDriveTorque: 120f,
                rightDriveTorque: 120f,
                steeringDegrees: 10f,
                input: Input(ignition: false, clutchPedal: 1f),
                destination: wheelCommands);
            Assert.That(config.IsDrivenWheel(0), Is.True);
            Assert.That(config.IsDrivenWheel(1), Is.True);
            Assert.That(config.IsDrivenWheel(2), Is.False);
            Assert.That(config.IsDrivenWheel(3), Is.False);
            Assert.That(wheelCommands[0].DriveTorqueNewtonMeters, Is.EqualTo(120f));
            Assert.That(wheelCommands[1].DriveTorqueNewtonMeters, Is.EqualTo(120f));
            Assert.That(wheelCommands[2].DriveTorqueNewtonMeters, Is.Zero);
            Assert.That(wheelCommands[3].DriveTorqueNewtonMeters, Is.Zero);

            config.SetDrivenWheelsForTesting(leftWheelIndex: 2, rightWheelIndex: 3);
            Assert.That(config.Validate(out string drivenWheelFailure), Is.True, drivenWheelFailure);
            wheelSimulation.BuildCommands(
                config,
                leftDriveTorque: 80f,
                rightDriveTorque: 60f,
                steeringDegrees: 0f,
                input: Input(ignition: false, clutchPedal: 1f),
                destination: wheelCommands);
            Assert.That(wheelCommands[0].DriveTorqueNewtonMeters, Is.Zero);
            Assert.That(wheelCommands[1].DriveTorqueNewtonMeters, Is.Zero);
            Assert.That(wheelCommands[2].DriveTorqueNewtonMeters, Is.EqualTo(80f));
            Assert.That(wheelCommands[3].DriveTorqueNewtonMeters, Is.EqualTo(60f));

            var brakes = new BrakeSimulation();
            Assert.That(
                brakes.CalculateBrakeTorque(config.Dynamics, 0.5f, 0.5f, rearWheel: false),
                Is.EqualTo(900f).Within(0.0001f));
            Assert.That(
                brakes.CalculateBrakeTorque(config.Dynamics, 0.5f, 0.5f, rearWheel: true),
                Is.EqualTo(1325f).Within(0.0001f));

            var steering = new SteeringSimulation();
            Assert.That(
                steering.CalculateAngleDegrees(config.Dynamics, 1f, 0f),
                Is.EqualTo(config.Dynamics.MaximumSteeringAngleDegrees).Within(0.0001f));
            Assert.That(
                steering.CalculateAngleDegrees(
                    config.Dynamics,
                    -1f,
                    config.Dynamics.SteeringFadeSpeedMetersPerSecond),
                Is.EqualTo(-config.Dynamics.HighSpeedSteeringAngleDegrees).Within(0.0001f));

            var suspension = new SuspensionSimulation();
            Assert.That(
                suspension.CalculateForceNewtons(config.Dynamics, 0.1f, 0.5f),
                Is.EqualTo(4550f).Within(0.001f));
            Assert.That(
                suspension.CalculateForceNewtons(config.Dynamics, 0f, -1f),
                Is.Zero);
        }

        [Test]
        public void Root_SamplesAndAppliesBackendOncePerFixedTickRegardlessOfSubsteps()
        {
            config.SetSubstepCountForTesting(8);
            var backend = new RecordingWheelBackend(config.WheelCount);
            var root = CreateRoot(backend);
            VehicleInputState input = Input(
                ignition: true,
                clutchPedal: 1f,
                brake: 0.6f,
                steering: 0.5f);

            root.Tick(FixedDeltaSeconds, input);

            Assert.That(backend.SampleCallCount, Is.EqualTo(1));
            Assert.That(backend.ApplyCallCount, Is.EqualTo(1));
            Assert.That(backend.LastSampleDeltaSeconds, Is.EqualTo(FixedDeltaSeconds));
            Assert.That(backend.LastApplyDeltaSeconds, Is.EqualTo(FixedDeltaSeconds));
            Assert.That(root.Telemetry.SubstepCount, Is.EqualTo(8));
            Assert.That(
                backend.LastCommands[0].BrakeTorqueNewtonMeters,
                Is.EqualTo(1080f).Within(0.001f));
            Assert.That(backend.LastCommands[0].SteeringAngleDegrees, Is.EqualTo(15f).Within(0.001f));
            Assert.That(backend.LastCommands[1].SteeringAngleDegrees, Is.EqualTo(15f).Within(0.001f));
            Assert.That(backend.LastCommands[2].SteeringAngleDegrees, Is.EqualTo(0f).Within(0.001f));
            Assert.That(backend.LastCommands[3].SteeringAngleDegrees, Is.EqualTo(0f).Within(0.001f));

            root.Reset();
            Assert.That(backend.ResetCallCount, Is.EqualTo(1));
            Assert.That(root.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Off));
            Assert.That(root.State.IsFinite(), Is.True);
        }

        [Test]
        public void Root_ReportsAcceptedRejectedAndStableShiftStatusExplicitly()
        {
            var root = CreateRoot(new RecordingWheelBackend(config.WheelCount));

            root.Tick(
                FixedDeltaSeconds,
                Input(
                    ignition: true,
                    clutchPedal: 1f,
                    gearChange: true,
                    requestedGear: 1));
            Assert.That(root.State.SelectedGear, Is.EqualTo(1));
            Assert.That(root.State.ShiftStatus, Is.EqualTo(VehicleShiftStatus.AcceptedThisTick));
            Assert.That(root.Telemetry.ShiftStatus, Is.EqualTo(VehicleShiftStatus.AcceptedThisTick));

            root.Tick(
                FixedDeltaSeconds,
                Input(
                    ignition: true,
                    clutchPedal: 1f,
                    gearChange: true,
                    requestedGear: config.Gearbox.ForwardGearCount + 1));
            Assert.That(root.State.SelectedGear, Is.EqualTo(1));
            Assert.That(root.State.InvalidShiftCount, Is.EqualTo(1));
            Assert.That(root.State.ShiftStatus, Is.EqualTo(VehicleShiftStatus.RejectedThisTick));
            Assert.That(root.Telemetry.ShiftStatus, Is.EqualTo(VehicleShiftStatus.RejectedThisTick));
            Assert.That(
                root.Prerequisites.HasAny(
                    VehicleSimulationPrerequisiteFailure.InvalidSelectedGear),
                Is.True);

            root.Tick(FixedDeltaSeconds, Input(ignition: true, clutchPedal: 1f));
            Assert.That(root.State.ShiftStatus, Is.EqualTo(VehicleShiftStatus.Stable));
            Assert.That(root.Telemetry.ShiftStatus, Is.EqualTo(VehicleShiftStatus.Stable));
        }

        [Test]
        public void FixedStepResults_AreEquivalentWithinNumericTolerance()
        {
            var rootAtFiftyHertz = CreateRoot(new RecordingWheelBackend(config.WheelCount));
            var rootAtHundredHertz = CreateRoot(new RecordingWheelBackend(config.WheelCount));

            RunFor(rootAtFiftyHertz, 0.02f, 0.5f, Input(ignition: true, clutchPedal: 1f, starter: true));
            RunFor(rootAtHundredHertz, 0.01f, 0.5f, Input(ignition: true, clutchPedal: 1f, starter: true));
            RunFor(rootAtFiftyHertz, 0.02f, 3f, Input(ignition: true, clutchPedal: 1f));
            RunFor(rootAtHundredHertz, 0.01f, 3f, Input(ignition: true, clutchPedal: 1f));

            Assert.That(rootAtFiftyHertz.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running));
            Assert.That(rootAtHundredHertz.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running));
            Assert.That(
                rootAtFiftyHertz.State.EngineRpm,
                Is.EqualTo(rootAtHundredHertz.State.EngineRpm).Within(15f));
            Assert.That(
                rootAtFiftyHertz.State.FuelLiters,
                Is.EqualTo(rootAtHundredHertz.State.FuelLiters).Within(0.0001f));
        }

        [Test]
        public void StateDto_JsonRoundTripPreservesFiniteSimulationState()
        {
            var backend = new RecordingWheelBackend(config.WheelCount);
            backend.SetSurface(VehicleSurfaceType.Gravel);
            var root = CreateRoot(backend);
            StartAndSettleAtIdle(root, FixedDeltaSeconds);
            root.Tick(
                FixedDeltaSeconds,
                Input(ignition: true, clutchPedal: 1f, brake: 0.25f, steering: -0.4f));

            VehicleSimulationStateDto captured = root.State.CaptureDto();
            string json = JsonUtility.ToJson(captured);
            VehicleSimulationStateDto deserialized = JsonUtility.FromJson<VehicleSimulationStateDto>(json);
            var restored = new VehicleSimulationState();
            restored.Reset(config);

            Assert.That(restored.TryRestoreDto(deserialized, config), Is.True);
            Assert.That(restored.EngineStatus, Is.EqualTo(root.State.EngineStatus));
            Assert.That(restored.EngineRpm, Is.EqualTo(root.State.EngineRpm).Within(0.001f));
            Assert.That(restored.FuelLiters, Is.EqualTo(root.State.FuelLiters).Within(0.000001f));
            Assert.That(restored.SelectedGear, Is.EqualTo(root.State.SelectedGear));
            Assert.That(restored.WheelCount, Is.EqualTo(config.WheelCount));
            Assert.That(restored.GetWheelState(0).Surface, Is.EqualTo(VehicleSurfaceType.Gravel));
            Assert.That(restored.IsFinite(), Is.True);

            deserialized.schemaVersion = VehicleSimulationStateDto.CurrentSchemaVersion + 1;
            Assert.That(restored.TryRestoreDto(deserialized, config), Is.False);
            deserialized.schemaVersion = VehicleSimulationStateDto.CurrentSchemaVersion;
            deserialized.engineRpm = float.NaN;
            Assert.That(restored.TryRestoreDto(deserialized, config), Is.False);
        }

        [Test]
        public void Telemetry_ReflectsInputWheelSamplesAndPrerequisiteFlags()
        {
            var backend = new RecordingWheelBackend(config.WheelCount);
            backend.VehicleSpeedMetersPerSecond = 5f;
            backend.SetSurface(VehicleSurfaceType.Grass);
            var root = CreateRoot(backend);

            root.Tick(
                FixedDeltaSeconds,
                Input(
                    ignition: true,
                    clutchPedal: 1f,
                    brake: 0.4f,
                    steering: 1f));

            Assert.That(root.Telemetry.Brake01, Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(root.Telemetry.VehicleSpeedMetersPerSecond, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(root.Telemetry.SteeringAngleDegrees, Is.GreaterThan(0f));
            Assert.That(root.Telemetry.WheelCount, Is.EqualTo(4));
            Assert.That(root.Telemetry.GetWheel(0).HasContact, Is.True);
            Assert.That(root.Telemetry.GetWheel(0).NormalLoadNewtons, Is.EqualTo(1500f));
            Assert.That(root.Telemetry.GetWheel(0).Surface, Is.EqualTo(VehicleSurfaceType.Grass));
            Assert.That(
                root.Telemetry.PrerequisiteFailures &
                VehicleSimulationPrerequisiteFailure.NeutralSelected,
                Is.Not.EqualTo(VehicleSimulationPrerequisiteFailure.None));
            Assert.That(root.LastTickWasFinite, Is.True);
        }

        [Test]
        public void Root_RejectsNonFiniteBackendStateExplicitly()
        {
            var backend = new RecordingWheelBackend(config.WheelCount);
            backend.SetNonFiniteSlip();
            var root = CreateRoot(backend);

            Assert.That(
                () => root.Tick(FixedDeltaSeconds, Input(ignition: true, clutchPedal: 1f)),
                Throws.TypeOf<InvalidOperationException>());
            Assert.That(root.LastTickWasFinite, Is.False);
        }

        [Test]
        public void Root_DoesNotAllocatePerTickAfterWarmup()
        {
            var backend = new RecordingWheelBackend(config.WheelCount);
            var root = CreateRoot(backend);
            VehicleInputState input = Input(ignition: true, clutchPedal: 1f);
            for (int warmup = 0; warmup < 64; warmup++)
            {
                root.Tick(FixedDeltaSeconds, input);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int tick = 0; tick < 256; tick++)
            {
                root.Tick(FixedDeltaSeconds, input);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocatedBytes, Is.Zero, "Normal fixed-tick path allocated managed memory after warmup.");
        }

        [Test]
        public void AssemblyAdapter_ReportsMissingSourceAndAvailabilityWithoutThrowing()
        {
            var gameObject = new GameObject("M06_PrerequisiteAdapter_EditModeFixture");
            try
            {
                AssemblyVehiclePrerequisiteAdapter adapter =
                    gameObject.AddComponent<AssemblyVehiclePrerequisiteAdapter>();
                adapter.SetPrototypeAvailability(
                    hasFuel: false,
                    hasOil: false,
                    hasCoolant: false,
                    voltage: 0f);
                var result = new VehicleSimulationPrerequisites();

                adapter.Evaluate(VehicleInputState.Neutral(), ref result);

                Assert.That(
                    result.HasAny(VehicleSimulationPrerequisiteFailure.PrerequisiteSourceUnavailable),
                    Is.True);
                Assert.That(result.HasAny(VehicleSimulationPrerequisiteFailure.FuelUnavailable), Is.True);
                Assert.That(result.HasAny(VehicleSimulationPrerequisiteFailure.OilUnavailable), Is.True);
                Assert.That(result.HasAny(VehicleSimulationPrerequisiteFailure.CoolantUnavailable), Is.True);
                Assert.That(result.HasAny(VehicleSimulationPrerequisiteFailure.BatteryVoltageLow), Is.True);
                Assert.That(result.HasAny(VehicleSimulationPrerequisiteFailure.IgnitionOff), Is.True);
                Assert.That(result.CanCrank, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private VehicleSimulationRoot CreateRoot(RecordingWheelBackend backend)
        {
            return new VehicleSimulationRoot(config, backend, new ReadyPrerequisiteSource());
        }

        private static VehicleInputState Input(
            bool ignition,
            float clutchPedal,
            float throttle = 0f,
            float brake = 0f,
            float steering = 0f,
            bool starter = false,
            bool gearChange = false,
            int requestedGear = 0)
        {
            return new VehicleInputState(
                throttle,
                clutchPedal,
                brake,
                steering,
                ignition,
                starter,
                gearChange,
                requestedGear);
        }

        private static void StartAndSettleAtIdle(VehicleSimulationRoot root, float deltaSeconds)
        {
            VehicleInputState crank = Input(ignition: true, clutchPedal: 1f, starter: true);
            for (int tick = 0;
                 tick < 100 && root.State.EngineStatus != VehicleEngineStatus.Running;
                 tick++)
            {
                root.Tick(deltaSeconds, crank);
            }

            Assert.That(root.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running));
            RunFor(root, deltaSeconds, 3f, Input(ignition: true, clutchPedal: 1f));
        }

        private static void RunFor(
            VehicleSimulationRoot root,
            float deltaSeconds,
            float durationSeconds,
            in VehicleInputState input)
        {
            int tickCount = Mathf.RoundToInt(durationSeconds / deltaSeconds);
            for (int tick = 0; tick < tickCount; tick++)
            {
                root.Tick(deltaSeconds, input);
            }
        }

        private sealed class ReadyPrerequisiteSource : IVehicleSimulationPrerequisiteSource
        {
            public void Evaluate(
                in VehicleInputState input,
                ref VehicleSimulationPrerequisites result)
            {
                result.Reset();
                if (!input.IgnitionOn)
                {
                    result.Add(VehicleSimulationPrerequisiteFailure.IgnitionOff);
                }
            }
        }

        private sealed class RecordingWheelBackend : IWheelPhysicsBackend
        {
            private readonly WheelPhysicsSample[] samples;

            public RecordingWheelBackend(int wheelCount)
            {
                WheelCount = wheelCount;
                samples = new WheelPhysicsSample[wheelCount];
                LastCommands = new WheelPhysicsCommand[wheelCount];
                SetSurface(VehicleSurfaceType.Paved);
            }

            public int WheelCount { get; }

            public float VehicleSpeedMetersPerSecond { get; set; }

            public int SampleCallCount { get; private set; }

            public int ApplyCallCount { get; private set; }

            public int ResetCallCount { get; private set; }

            public float LastSampleDeltaSeconds { get; private set; }

            public float LastApplyDeltaSeconds { get; private set; }

            public WheelPhysicsCommand[] LastCommands { get; }

            public void Sample(float fixedDeltaSeconds, WheelPhysicsSample[] destination)
            {
                SampleCallCount++;
                LastSampleDeltaSeconds = fixedDeltaSeconds;
                for (int index = 0; index < WheelCount; index++)
                {
                    destination[index] = samples[index];
                }
            }

            public void Apply(float fixedDeltaSeconds, WheelPhysicsCommand[] commands)
            {
                ApplyCallCount++;
                LastApplyDeltaSeconds = fixedDeltaSeconds;
                for (int index = 0; index < WheelCount; index++)
                {
                    LastCommands[index] = commands[index];
                }
            }

            public void Reset()
            {
                ResetCallCount++;
            }

            public void SetSurface(VehicleSurfaceType surface)
            {
                for (int index = 0; index < WheelCount; index++)
                {
                    samples[index] = new WheelPhysicsSample(
                        hasContact: true,
                        contactPoint: new Vector3(index, 0f, 0f),
                        contactNormal: Vector3.up,
                        normalLoadNewtons: 1500f,
                        longitudinalSlip: 0.05f,
                        lateralSlip: -0.02f,
                        angularSpeedRadiansPerSecond: 0f,
                        suspensionCompression01: 0.5f,
                        surface: surface);
                }
            }

            public void SetNonFiniteSlip()
            {
                samples[0] = new WheelPhysicsSample(
                    hasContact: true,
                    contactPoint: Vector3.zero,
                    contactNormal: Vector3.up,
                    normalLoadNewtons: 1500f,
                    longitudinalSlip: float.NaN,
                    lateralSlip: 0f,
                    angularSpeedRadiansPerSecond: 0f,
                    suspensionCompression01: 0.5f,
                    surface: VehicleSurfaceType.Paved);
            }
        }
    }
}
