using System;
using System.Diagnostics;
using UnityEngine;

namespace MSC.Vehicle.Simulation
{
    /// <summary>
    /// Pure fixed-step orchestration. Unity presentation and Rigidbody authoring
    /// remain behind IWheelPhysicsBackend and the runtime host.
    /// </summary>
    public sealed class VehicleSimulationRoot
    {
        private readonly VehicleSimulationConfig config;
        private readonly IWheelPhysicsBackend wheelBackend;
        private readonly IVehicleSimulationPrerequisiteSource prerequisiteSource;
        private readonly WheelPhysicsSample[] wheelSamples;
        private readonly WheelPhysicsCommand[] wheelCommands;
        private readonly WheelPhysicsCommand[] commandAccumulator;
        private readonly PowertrainGraph powertrainGraph = new PowertrainGraph();
        private readonly StarterSimulation starter = new StarterSimulation();
        private readonly EngineSimulation engine = new EngineSimulation();
        private readonly ClutchSimulation clutch = new ClutchSimulation();
        private readonly GearboxSimulation gearbox = new GearboxSimulation();
        private readonly DifferentialSimulation differential = new DifferentialSimulation();
        private readonly SteeringSimulation steering = new SteeringSimulation();
        private readonly WheelSimulation wheels = new WheelSimulation();
        private readonly ElectricalSimulation electrical = new ElectricalSimulation();
        private readonly FluidSimulation fluids = new FluidSimulation();
        private readonly ThermalSimulation thermal = new ThermalSimulation();
        private VehicleSimulationPrerequisites prerequisites;
        private float lastFixedStepSeconds;

        public VehicleSimulationRoot(
            VehicleSimulationConfig simulationConfig,
            IWheelPhysicsBackend physicsBackend,
            IVehicleSimulationPrerequisiteSource simulationPrerequisiteSource)
        {
            config = simulationConfig != null
                ? simulationConfig
                : throw new ArgumentNullException(nameof(simulationConfig));
            wheelBackend = physicsBackend ?? throw new ArgumentNullException(nameof(physicsBackend));
            prerequisiteSource = simulationPrerequisiteSource ??
                throw new ArgumentNullException(nameof(simulationPrerequisiteSource));
            if (!powertrainGraph.Validate(config, out string configFailure))
            {
                throw new ArgumentException(configFailure, nameof(simulationConfig));
            }

            if (wheelBackend.WheelCount != config.WheelCount)
            {
                throw new ArgumentException(
                    $"Wheel backend count {wheelBackend.WheelCount} does not match config count {config.WheelCount}.",
                    nameof(physicsBackend));
            }

            wheelSamples = new WheelPhysicsSample[config.WheelCount];
            wheelCommands = new WheelPhysicsCommand[config.WheelCount];
            commandAccumulator = new WheelPhysicsCommand[config.WheelCount];
            State = new VehicleSimulationState();
            State.Reset(config);
            Telemetry = new VehicleTelemetry();
            Telemetry.ConfigureWheelCount(config.WheelCount);
            prerequisites.Reset();
            UpdateTelemetry(VehicleInputState.Neutral(), 0f, 0f, 0f);
        }

        public VehicleSimulationConfig Config => config;
        public VehicleSimulationState State { get; }
        public VehicleTelemetry Telemetry { get; }
        public VehicleSimulationPrerequisites Prerequisites => prerequisites;
        public PowertrainGraph PowertrainGraph => powertrainGraph;
        public bool LastTickWasFinite { get; private set; } = true;

        public void Tick(float fixedDeltaSeconds, in VehicleInputState requestedInput)
        {
            if (!(fixedDeltaSeconds > 0f) || !VehicleSimulationMath.IsFinite(fixedDeltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(fixedDeltaSeconds));
            }

            bool inputWasFinite = requestedInput.IsFinite;
            VehicleInputState input = inputWasFinite
                ? requestedInput
                : VehicleInputState.Neutral();
            lastFixedStepSeconds = fixedDeltaSeconds;
            long tickStarted = Stopwatch.GetTimestamp();
            long backendSampleStarted = Stopwatch.GetTimestamp();
            wheelBackend.Sample(fixedDeltaSeconds, wheelSamples);
            double sampleMilliseconds = ElapsedMilliseconds(backendSampleStarted);

            CopyWheelSamplesToState();
            float sampledVehicleSpeed = wheelBackend.VehicleSpeedMetersPerSecond;
            if (!VehicleSimulationMath.IsFinite(sampledVehicleSpeed))
            {
                LastTickWasFinite = false;
                throw new InvalidOperationException(
                    "Wheel backend produced a non-finite vehicle speed.");
            }

            State.VehicleSpeedMetersPerSecond = sampledVehicleSpeed;

            bool shiftAccepted = true;
            State.ShiftStatus = VehicleShiftStatus.Stable;
            if (input.GearChangeRequested)
            {
                int selectedGear = State.SelectedGear;
                shiftAccepted = gearbox.TrySelectGear(config.Gearbox, input.RequestedGear, ref selectedGear);
                if (shiftAccepted)
                {
                    State.SelectedGear = selectedGear;
                    State.ShiftStatus = VehicleShiftStatus.AcceptedThisTick;
                }
                else
                {
                    State.InvalidShiftCount++;
                    State.ShiftStatus = VehicleShiftStatus.RejectedThisTick;
                }
            }

            EvaluatePrerequisites(input, shiftAccepted, inputWasFinite);
            Array.Clear(commandAccumulator, 0, commandAccumulator.Length);

            int substeps = Mathf.Clamp(config.SubstepCount, 1, 8);
            float substepSeconds = fixedDeltaSeconds / substeps;
            float accumulatedDifferentialTorque = 0f;
            float steeringDegrees = steering.CalculateAngleDegrees(
                config.Dynamics,
                input.SteeringMinusOneToOne,
                State.VehicleSpeedMetersPerSecond);

            for (int substep = 0; substep < substeps; substep++)
            {
                StepPureSimulation(
                    substepSeconds,
                    input,
                    steeringDegrees,
                    ref accumulatedDifferentialTorque);
                for (int wheelIndex = 0; wheelIndex < wheelCommands.Length; wheelIndex++)
                {
                    WheelPhysicsCommand previous = commandAccumulator[wheelIndex];
                    WheelPhysicsCommand next = wheelCommands[wheelIndex];
                    commandAccumulator[wheelIndex] = new WheelPhysicsCommand(
                        previous.DriveTorqueNewtonMeters + next.DriveTorqueNewtonMeters,
                        previous.BrakeTorqueNewtonMeters + next.BrakeTorqueNewtonMeters,
                        previous.SteeringAngleDegrees + next.SteeringAngleDegrees);
                }
            }

            for (int wheelIndex = 0; wheelIndex < wheelCommands.Length; wheelIndex++)
            {
                WheelPhysicsCommand total = commandAccumulator[wheelIndex];
                wheelCommands[wheelIndex] = new WheelPhysicsCommand(
                    total.DriveTorqueNewtonMeters / substeps,
                    total.BrakeTorqueNewtonMeters / substeps,
                    total.SteeringAngleDegrees / substeps);
            }

            State.DifferentialTorqueNewtonMeters = accumulatedDifferentialTorque / substeps;
            State.SteeringAngleDegrees = steeringDegrees;
            State.BrakeTorqueNewtonMeters = input.Brake01 *
                                            config.Dynamics.MaximumBrakeTorqueNewtonMeters;

            long backendApplyStarted = Stopwatch.GetTimestamp();
            wheelBackend.Apply(fixedDeltaSeconds, wheelCommands);
            double applyMilliseconds = ElapsedMilliseconds(backendApplyStarted);

            LastTickWasFinite = State.IsFinite();
            if (!LastTickWasFinite)
            {
                throw new InvalidOperationException("Vehicle simulation produced a non-finite state.");
            }

            double totalMilliseconds = ElapsedMilliseconds(tickStarted);
            UpdateTelemetry(
                input,
                totalMilliseconds,
                sampleMilliseconds,
                applyMilliseconds);
        }

        public void Reset()
        {
            wheelBackend.Reset();
            State.Reset(config);
            prerequisites.Reset();
            Array.Clear(wheelSamples, 0, wheelSamples.Length);
            Array.Clear(wheelCommands, 0, wheelCommands.Length);
            Array.Clear(commandAccumulator, 0, commandAccumulator.Length);
            LastTickWasFinite = true;
            lastFixedStepSeconds = 0f;
            UpdateTelemetry(VehicleInputState.Neutral(), 0f, 0f, 0f);
        }

        private void StepPureSimulation(
            float deltaSeconds,
            in VehicleInputState input,
            float steeringDegrees,
            ref float accumulatedDifferentialTorque)
        {
            float ratio = gearbox.GetRatio(config.Gearbox, State.SelectedGear);
            float drivenWheelOmega = 0.5f *
                (wheelSamples[config.LeftDrivenWheelIndex].AngularSpeedRadiansPerSecond +
                 wheelSamples[config.RightDrivenWheelIndex].AngularSpeedRadiansPerSecond);
            float gearboxInputOmega = drivenWheelOmega * ratio * config.Gearbox.FinalDriveRatio;
            float engineOmega = State.EngineRpm * VehicleSimulationMath.RpmToRadiansPerSecond;
            float clutchTorque = ratio == 0f
                ? 0f
                : clutch.CalculateTransferTorque(
                    config.Clutch,
                    engineOmega,
                    gearboxInputOmega,
                    input.ClutchPedal01,
                    out _);
            if (!prerequisites.CanTransmitDrive)
            {
                clutchTorque = 0f;
            }

            bool starterActive = input.StarterRequested && prerequisites.CanCrank &&
                                 State.EngineStatus != VehicleEngineStatus.Running &&
                                 State.EngineRpm < config.Engine.StarterMaximumRpm;

            engine.Step(
                config,
                State,
                input,
                prerequisites,
                clutchTorque,
                deltaSeconds,
                starter);

            engineOmega = State.EngineRpm * VehicleSimulationMath.RpmToRadiansPerSecond;
            float slipOmega = engineOmega - gearboxInputOmega;
            clutchTorque = 0f;
            if (ratio != 0f)
            {
                clutchTorque = clutch.CalculateTransferTorque(
                    config.Clutch,
                    engineOmega,
                    gearboxInputOmega,
                    input.ClutchPedal01,
                    out slipOmega);
            }

            if (!prerequisites.CanTransmitDrive)
            {
                clutchTorque = 0f;
            }
            float engagement = clutch.CalculateEngagement(config.Clutch, input.ClutchPedal01);
            State.ClutchEngagement01 = engagement;
            State.ClutchSlipRpm = slipOmega * VehicleSimulationMath.RadiansPerSecondToRpm;
            State.ClutchTransferredTorqueNewtonMeters = clutchTorque;
            State.ClutchTemperatureCelsius = clutch.UpdateTemperature(
                State.ClutchTemperatureCelsius,
                config.AmbientTemperatureCelsius,
                clutchTorque,
                slipOmega,
                deltaSeconds);
            State.GearboxInputRadiansPerSecond = gearboxInputOmega;
            State.GearboxOutputRadiansPerSecond = ratio == 0f
                ? 0f
                : gearboxInputOmega / ratio;

            float outputTorque = prerequisites.CanTransmitDrive
                ? gearbox.CalculateOutputTorque(config.Gearbox, State.SelectedGear, clutchTorque)
                : 0f;
            differential.DistributeOpenDifferential(outputTorque, out float leftTorque, out float rightTorque);
            accumulatedDifferentialTorque += outputTorque;
            wheels.BuildCommands(
                config,
                leftTorque,
                rightTorque,
                steeringDegrees,
                input,
                wheelCommands);

            bool running = State.EngineStatus == VehicleEngineStatus.Running;
            electrical.Step(config, State, starterActive, running, deltaSeconds);
            fluids.Step(config, State, State.FilteredThrottle01, running, deltaSeconds);
            thermal.Step(config, State, running, State.EngineLoad01, deltaSeconds);
            State.ElapsedSeconds += deltaSeconds;
        }

        private void EvaluatePrerequisites(
            in VehicleInputState input,
            bool shiftAccepted,
            bool inputWasFinite)
        {
            prerequisites.Reset();
            prerequisiteSource.Evaluate(input, ref prerequisites);
            VehicleSupportSystemsConfig support = config.SupportSystems;
            if (State.FuelLiters <= support.MinimumFuelLiters)
            {
                prerequisites.Add(VehicleSimulationPrerequisiteFailure.FuelUnavailable);
            }

            if (State.OilLiters <= support.MinimumOilLiters)
            {
                prerequisites.Add(VehicleSimulationPrerequisiteFailure.OilUnavailable);
            }

            if (State.CoolantLiters <= support.MinimumCoolantLiters)
            {
                prerequisites.Add(VehicleSimulationPrerequisiteFailure.CoolantUnavailable);
            }

            if (State.BatteryVoltage < support.MinimumCrankVoltage)
            {
                prerequisites.Add(VehicleSimulationPrerequisiteFailure.BatteryVoltageLow);
            }

            if (!shiftAccepted)
            {
                prerequisites.Add(VehicleSimulationPrerequisiteFailure.InvalidSelectedGear);
            }

            if (State.SelectedGear == 0)
            {
                prerequisites.Add(VehicleSimulationPrerequisiteFailure.NeutralSelected);
            }

            if (input.ClutchPedal01 >= 0.98f)
            {
                prerequisites.Add(VehicleSimulationPrerequisiteFailure.ClutchDisengaged);
            }

            if (!inputWasFinite)
            {
                prerequisites.Add(VehicleSimulationPrerequisiteFailure.InvalidClutchState);
            }
        }

        private void CopyWheelSamplesToState()
        {
            for (int index = 0; index < wheelSamples.Length; index++)
            {
                WheelPhysicsSample sample = wheelSamples[index];
                var wheelState = new VehicleWheelState
                {
                    HasContact = sample.HasContact,
                    NormalLoadNewtons = sample.NormalLoadNewtons,
                    LongitudinalSlip = sample.LongitudinalSlip,
                    LateralSlip = sample.LateralSlip,
                    AngularSpeedRadiansPerSecond = sample.AngularSpeedRadiansPerSecond,
                    SuspensionCompression01 = sample.SuspensionCompression01,
                    SuspensionForceNewtons = sample.NormalLoadNewtons,
                    Surface = sample.Surface
                };
                State.SetWheelState(index, wheelState);
            }
        }

        private void UpdateTelemetry(
            in VehicleInputState input,
            double simulationMilliseconds,
            double backendSampleMilliseconds,
            double backendApplyMilliseconds)
        {
            Telemetry.EngineRpm = State.EngineRpm;
            Telemetry.Throttle01 = State.FilteredThrottle01;
            Telemetry.EngineLoad01 = State.EngineLoad01;
            Telemetry.EngineTorqueNewtonMeters = State.EngineTorqueNewtonMeters;
            Telemetry.ClutchSlipRpm = State.ClutchSlipRpm;
            Telemetry.SelectedGear = State.SelectedGear;
            Telemetry.ShiftStatus = State.ShiftStatus;
            Telemetry.GearboxInputRadiansPerSecond = State.GearboxInputRadiansPerSecond;
            Telemetry.GearboxOutputRadiansPerSecond = State.GearboxOutputRadiansPerSecond;
            Telemetry.DifferentialTorqueNewtonMeters = State.DifferentialTorqueNewtonMeters;
            Telemetry.VehicleSpeedMetersPerSecond = State.VehicleSpeedMetersPerSecond;
            Telemetry.SteeringAngleDegrees = State.SteeringAngleDegrees;
            Telemetry.Brake01 = input.Brake01;
            Telemetry.BatteryVoltage = State.BatteryVoltage;
            Telemetry.EngineTemperatureCelsius = State.EngineTemperatureCelsius;
            Telemetry.PrerequisiteFailures = prerequisites.FailureFlags;
            Telemetry.SubstepCount = config.SubstepCount;
            Telemetry.FixedStepSeconds = lastFixedStepSeconds;
            Telemetry.SimulationMilliseconds = simulationMilliseconds;
            Telemetry.BackendSampleMilliseconds = backendSampleMilliseconds;
            Telemetry.BackendApplyMilliseconds = backendApplyMilliseconds;
            for (int index = 0; index < State.WheelCount; index++)
            {
                VehicleWheelState wheelState = State.GetWheelState(index);
                var wheelTelemetry = new VehicleWheelTelemetry(wheelState);
                Telemetry.SetWheel(index, wheelTelemetry);
            }
        }

        private static double ElapsedMilliseconds(long startedTimestamp)
        {
            return (Stopwatch.GetTimestamp() - startedTimestamp) * 1000.0 / Stopwatch.Frequency;
        }
    }
}
