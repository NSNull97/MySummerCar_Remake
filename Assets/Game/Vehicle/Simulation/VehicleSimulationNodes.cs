using System;
using UnityEngine;

namespace MSC.Vehicle.Simulation
{
    public sealed class PowertrainGraph
    {
        private static readonly string[] NodeNames =
        {
            "Engine",
            "Clutch",
            "Gearbox",
            "FinalDrive",
            "OpenDifferential",
            "LeftDrivenWheel",
            "RightDrivenWheel"
        };

        public int NodeCount => NodeNames.Length;

        public string GetNodeName(int index)
        {
            return NodeNames[index];
        }

        public bool Validate(VehicleSimulationConfig config, out string failure)
        {
            if (config == null)
            {
                failure = "Vehicle simulation config is missing.";
                return false;
            }

            if (!config.Validate(out failure))
            {
                return false;
            }

            if (config.WheelCount != 4 ||
                config.LeftDrivenWheelIndex == config.RightDrivenWheelIndex)
            {
                failure = "Prototype powertrain requires four wheels and two distinct configured driven wheels.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    public sealed class StarterSimulation
    {
        public float CalculateTorque(
            EngineSimulationConfig config,
            float engineRpm,
            bool requested,
            bool canCrank)
        {
            return requested && canCrank && engineRpm < config.StarterMaximumRpm
                ? config.StarterTorqueNewtonMeters
                : 0f;
        }
    }

    public sealed class EngineSimulation
    {
        public float InterpolateTorque(EngineSimulationConfig config, float rpm)
        {
            return config.EvaluateTorque(rpm);
        }

        public void Step(
            VehicleSimulationConfig config,
            VehicleSimulationState state,
            in VehicleInputState input,
            in VehicleSimulationPrerequisites prerequisites,
            float clutchLoadTorque,
            float deltaSeconds,
            StarterSimulation starter,
            SatsumaEngineOperatingPoint? operatingPoint = null)
        {
            EngineSimulationConfig engine = config.Engine;
            float throttle = Mathf.MoveTowards(
                state.FilteredThrottle01,
                input.Throttle01,
                engine.ThrottleResponsePerSecond * deltaSeconds);
            float starterTorque = starter.CalculateTorque(
                engine,
                state.EngineRpm,
                input.StarterRequested && state.EngineStatus != VehicleEngineStatus.Running,
                prerequisites.CanCrank) * (operatingPoint?.StarterScale ?? 1f);
            // The RPM limiter temporarily removes torque, not key intent.
            // Treating it as disengagement made failed cranking chatter between
            // Cranking/Stalled and replay start/stop feedback every few substeps.
            bool starterEngaged = input.StarterRequested && input.IgnitionOn && prerequisites.CanCrank;

            VehicleEngineStatus status = state.EngineStatus;
            if (!input.IgnitionOn && status != VehicleEngineStatus.Off)
            {
                status = VehicleEngineStatus.Off;
            }
            else if (starterEngaged && status != VehicleEngineStatus.Running)
            {
                status = VehicleEngineStatus.Cranking;
            }

            bool combustionAvailable = prerequisites.CanRun && input.IgnitionOn &&
                (operatingPoint?.CombustionAllowed ?? true);
            if (combustionAvailable &&
                status == VehicleEngineStatus.Cranking &&
                state.EngineRpm >= engine.StartThresholdRpm)
            {
                status = VehicleEngineStatus.Running;
            }
            else if (status == VehicleEngineStatus.Cranking && !starterEngaged)
            {
                status = VehicleEngineStatus.Stalled;
            }

            float idleTarget = operatingPoint?.IdleRpm ?? engine.IdleTargetRpm;
            float idleError = idleTarget - state.EngineRpm;
            float idleThrottle = status == VehicleEngineStatus.Running
                ? Mathf.Clamp01(0.14f + idleError / Mathf.Max(400f, idleTarget) * 0.55f)
                : 0f;
            float effectiveThrottle = Mathf.Max(throttle, idleThrottle);
            float combustionTorque = status == VehicleEngineStatus.Running &&
                                      state.EngineRpm < engine.RedlineRpm && combustionAvailable
                ? engine.EvaluateTorque(state.EngineRpm) * effectiveThrottle * (operatingPoint?.TorqueScale ?? 1f)
                : 0f;
            float angularSpeed = state.EngineRpm * VehicleSimulationMath.RpmToRadiansPerSecond;
            float frictionMagnitude = angularSpeed > 0.01f
                ? engine.BaseFrictionNewtonMeters +
                  engine.ViscousFrictionNewtonMetersPerRadian * angularSpeed +
                  (operatingPoint?.ExtraFriction ?? 0f) +
                  (effectiveThrottle < 0.05f ? engine.EngineBrakingNewtonMeters : 0f)
                : 0f;
            float netTorque = starterTorque + combustionTorque - frictionMagnitude - clutchLoadTorque;
            angularSpeed = Mathf.Max(
                0f,
                angularSpeed + netTorque / engine.InertiaKilogramSquareMeters * deltaSeconds);
            float rpm = Mathf.Min(
                engine.MaximumRpm,
                angularSpeed * VehicleSimulationMath.RadiansPerSecondToRpm);

            float stallTimer = state.StallTimerSeconds;
            if (status == VehicleEngineStatus.Running && rpm < engine.StallRpm && starterTorque <= 0f)
            {
                stallTimer += deltaSeconds;
                if (stallTimer >= engine.StallDelaySeconds)
                {
                    status = VehicleEngineStatus.Stalled;
                    combustionTorque = 0f;
                }
            }
            else
            {
                stallTimer = 0f;
            }

            if (status == VehicleEngineStatus.Off && starterTorque <= 0f && rpm < 5f)
            {
                rpm = 0f;
            }

            state.EngineStatus = status;
            if (status == VehicleEngineStatus.Running && combustionTorque > 0f)
                state.CombustionRundownActive = true;
            else if (status == VehicleEngineStatus.Cranking || rpm <= 0f)
                state.CombustionRundownActive = false;
            state.EngineRpm = rpm;
            state.FilteredThrottle01 = throttle;
            state.EngineTorqueNewtonMeters = combustionTorque;
            state.EngineLoad01 = Mathf.Clamp01(
                Mathf.Abs(clutchLoadTorque) /
                Mathf.Max(1f, engine.EvaluateTorque(Mathf.Max(engine.IdleTargetRpm, rpm))));
            state.StallTimerSeconds = stallTimer;
        }
    }

    public sealed class ClutchSimulation
    {
        public float CalculateEngagement(ClutchSimulationConfig config, float clutchPedal01)
        {
            float released = 1f - Mathf.Clamp01(clutchPedal01);
            return Mathf.Pow(released, config.EngagementExponent);
        }

        public float CalculateTransferTorque(
            ClutchSimulationConfig config,
            float engineRadiansPerSecond,
            float gearboxInputRadiansPerSecond,
            float clutchPedal01,
            out float slipRadiansPerSecond)
        {
            float engagement = CalculateEngagement(config, clutchPedal01);
            slipRadiansPerSecond = engineRadiansPerSecond - gearboxInputRadiansPerSecond;
            float capacity = config.MaximumTorqueNewtonMeters * engagement;
            return Mathf.Clamp(
                slipRadiansPerSecond * config.SlipStiffness * engagement,
                -capacity,
                capacity);
        }

        public float UpdateTemperature(
            float currentCelsius,
            float ambientCelsius,
            float transferredTorque,
            float slipRadiansPerSecond,
            float deltaSeconds)
        {
            float heating = Mathf.Abs(transferredTorque * slipRadiansPerSecond) * 0.00004f;
            float cooling = (currentCelsius - ambientCelsius) * 0.025f;
            return Mathf.Max(ambientCelsius, currentCelsius + (heating - cooling) * deltaSeconds);
        }
    }

    public sealed class GearboxSimulation
    {
        public bool TrySelectGear(
            GearboxSimulationConfig config,
            int requestedGear,
            ref int selectedGear)
        {
            if (!config.TryGetRatio(requestedGear, out _))
            {
                return false;
            }

            selectedGear = requestedGear;
            return true;
        }

        public float GetRatio(GearboxSimulationConfig config, int gear)
        {
            return config.TryGetRatio(gear, out float ratio) ? ratio : 0f;
        }

        public float CalculateOutputTorque(
            GearboxSimulationConfig config,
            int gear,
            float clutchTorque)
        {
            float ratio = GetRatio(config, gear);
            return clutchTorque * ratio * config.FinalDriveRatio * config.Efficiency;
        }
    }

    public sealed class DifferentialSimulation
    {
        public void DistributeOpenDifferential(
            float inputTorqueNewtonMeters,
            out float leftTorqueNewtonMeters,
            out float rightTorqueNewtonMeters)
        {
            leftTorqueNewtonMeters = inputTorqueNewtonMeters * 0.5f;
            rightTorqueNewtonMeters = inputTorqueNewtonMeters * 0.5f;
        }
    }

    public sealed class BrakeSimulation
    {
        public float CalculateBrakeTorque(
            VehicleDynamicsConfig config,
            float brake01,
            float handbrake01,
            bool rearWheel)
        {
            float service = Mathf.Clamp01(brake01) * config.MaximumBrakeTorqueNewtonMeters;
            float parking = rearWheel
                ? Mathf.Clamp01(handbrake01) * config.MaximumHandbrakeTorqueNewtonMeters
                : 0f;
            return service + parking;
        }
    }

    public sealed class SteeringSimulation
    {
        public float CalculateAngleDegrees(
            VehicleDynamicsConfig config,
            float steeringInput,
            float speedMetersPerSecond)
        {
            float t = Mathf.Clamp01(
                Mathf.Abs(speedMetersPerSecond) /
                Mathf.Max(0.1f, config.SteeringFadeSpeedMetersPerSecond));
            float maximum = Mathf.Lerp(
                config.MaximumSteeringAngleDegrees,
                config.HighSpeedSteeringAngleDegrees,
                t);
            return Mathf.Clamp(steeringInput, -1f, 1f) * maximum;
        }
    }

    public sealed class SuspensionSimulation
    {
        public float CalculateForceNewtons(
            VehicleDynamicsConfig config,
            float compressionMeters,
            float compressionVelocityMetersPerSecond)
        {
            return Mathf.Max(
                0f,
                compressionMeters * config.SpringRateNewtonPerMeter +
                compressionVelocityMetersPerSecond * config.DamperRateNewtonSecondsPerMeter);
        }
    }

    public sealed class WheelSimulation
    {
        public void BuildCommands(
            VehicleSimulationConfig config,
            float leftDriveTorque,
            float rightDriveTorque,
            float steeringDegrees,
            in VehicleInputState input,
            WheelPhysicsCommand[] destination,
            float frontBrakeEfficiency = 1f,
            float rearBrakeEfficiency = 1f)
        {
            for (int index = 0; index < destination.Length; index++)
            {
                float driveTorque = index == config.LeftDrivenWheelIndex
                    ? leftDriveTorque
                    : index == config.RightDrivenWheelIndex
                        ? rightDriveTorque
                        : 0f;
                float serviceBrakeTorque = index >= 2
                    ? config.Dynamics.MaximumRearBrakeTorqueNewtonMeters
                    : config.Dynamics.MaximumBrakeTorqueNewtonMeters;
                float brakeTorque = Mathf.Clamp01(input.Brake01) *
                                    serviceBrakeTorque * Mathf.Clamp01(index >= 2
                                        ? rearBrakeEfficiency : frontBrakeEfficiency);
                destination[index] = new WheelPhysicsCommand(
                    driveTorque,
                    brakeTorque,
                    config.IsSteeredWheel(index) ? steeringDegrees : 0f);
            }
        }
    }

    public sealed class ElectricalSimulation
    {
        public void Step(
            VehicleSimulationConfig config,
            VehicleSimulationState state,
            bool starterActive,
            bool engineRunning,
            float deltaSeconds)
        {
            VehicleSupportSystemsConfig support = config.SupportSystems;
            float voltage = state.BatteryVoltage;
            if (starterActive)
            {
                voltage -= support.StarterDrainVoltsPerSecond * deltaSeconds;
            }
            else if (engineRunning)
            {
                voltage += support.AlternatorChargeVoltsPerSecond * deltaSeconds;
            }

            state.BatteryVoltage = Mathf.Clamp(voltage, 0f, support.NominalBatteryVoltage);
            state.BatteryCharge01 = Mathf.Clamp01(
                state.BatteryVoltage / Mathf.Max(0.1f, support.NominalBatteryVoltage));
        }
    }

    public sealed class FluidSimulation
    {
        public void Step(
            VehicleSimulationConfig config,
            VehicleSimulationState state,
            float throttle01,
            bool engineRunning,
            float deltaSeconds)
        {
            if (!engineRunning)
            {
                return;
            }

            VehicleSupportSystemsConfig support = config.SupportSystems;
            float rate = Mathf.Lerp(
                support.IdleFuelLitersPerSecond,
                support.FullLoadFuelLitersPerSecond,
                Mathf.Clamp01(throttle01));
            state.FuelLiters = Mathf.Max(0f, state.FuelLiters - rate * deltaSeconds);
        }
    }

    public sealed class ThermalSimulation
    {
        public void Step(
            VehicleSimulationConfig config,
            VehicleSimulationState state,
            bool engineRunning,
            float load01,
            float deltaSeconds)
        {
            VehicleSupportSystemsConfig support = config.SupportSystems;
            float target = engineRunning
                ? support.OperatingTemperatureCelsius + Mathf.Clamp01(load01) * 8f
                : support.AmbientTemperatureCelsius;
            float rate = engineRunning
                ? support.HeatingRatePerSecond
                : support.CoolingRatePerSecond;
            state.EngineTemperatureCelsius = Mathf.MoveTowards(
                state.EngineTemperatureCelsius,
                target,
                Mathf.Abs(target - state.EngineTemperatureCelsius) * rate * deltaSeconds +
                0.02f * deltaSeconds);
            state.CoolantTemperatureCelsius = Mathf.MoveTowards(
                state.CoolantTemperatureCelsius,
                state.EngineTemperatureCelsius,
                6f * deltaSeconds);
        }
    }
}
