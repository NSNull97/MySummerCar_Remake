using System;
using MSC.Vehicle.Simulation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.VehicleSimulation
{
    public static class VehicleSimulationCalibrationRunner
    {
        private const float FixedStepSeconds = 0.02f;

        [MenuItem(VehicleSimulationPrototypePaths.MenuRoot + "Run Calibration Fixture")]
        public static void Run()
        {
            VehicleSimulationConfig config = AssetDatabase.LoadAssetAtPath<VehicleSimulationConfig>(
                VehicleSimulationPrototypePaths.PrototypeConfig);
            if (config == null)
            {
                throw new InvalidOperationException("M06 calibration config is unavailable.");
            }

            if (!config.Validate(out string failure))
            {
                throw new InvalidOperationException("M06 calibration config is invalid: " + failure);
            }

            if (!config.DynamicTuning.IsProvisional)
            {
                throw new InvalidOperationException(
                    "M06 calibration must not run with an unlabelled dynamic tuning set.");
            }

            var backend = new CalibrationWheelBackend(config.WheelCount);
            var prerequisites = new CalibrationPrerequisiteSource();
            var simulation = new VehicleSimulationRoot(config, backend, prerequisites);

            VehicleInputState crank = new VehicleInputState(
                0f,
                1f,
                0f,
                0f,
                ignitionOn: true,
                starterRequested: true,
                gearChangeRequested: false,
                requestedGear: 0);
            int crankTicks = 0;
            while (simulation.State.EngineStatus != VehicleEngineStatus.Running && crankTicks < 300)
            {
                simulation.Tick(FixedStepSeconds, crank);
                crankTicks++;
            }

            if (simulation.State.EngineStatus != VehicleEngineStatus.Running)
            {
                throw new InvalidOperationException(
                    $"M06 calibration failed to start the engine after {crankTicks} ticks; rpm={simulation.State.EngineRpm:0.###}.");
            }

            VehicleInputState idle = new VehicleInputState(
                0f,
                1f,
                0f,
                0f,
                ignitionOn: true,
                starterRequested: false,
                gearChangeRequested: false,
                requestedGear: 0);
            for (int index = 0; index < 500; index++)
            {
                simulation.Tick(FixedStepSeconds, idle);
            }

            float rpm = simulation.State.EngineRpm;
            if (simulation.State.EngineStatus != VehicleEngineStatus.Running ||
                !VehicleSimulationMath.IsFinite(rpm) || rpm <= config.Engine.StallRpm ||
                rpm > config.Engine.MaximumRpm)
            {
                throw new InvalidOperationException(
                    $"M06 calibration idle result is invalid: status={simulation.State.EngineStatus}, rpm={rpm:0.###}.");
            }

            Debug.Log(
                $"M06_VEHICLE_SIMULATION_CALIBRATION_OK label={config.DynamicTuning.Label} " +
                $"crankTicks={crankTicks} idleRpm={rpm:0.###} " +
                "referenceDynamicFixture=Missing provisionalOnly=true");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("M06 calibration batch entry requires batch mode.");
            }

            Run();
        }

        private sealed class CalibrationPrerequisiteSource : IVehicleSimulationPrerequisiteSource
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

        private sealed class CalibrationWheelBackend : IWheelPhysicsBackend
        {
            public CalibrationWheelBackend(int wheelCount)
            {
                WheelCount = wheelCount;
            }

            public int WheelCount { get; }

            public float VehicleSpeedMetersPerSecond => 0f;

            public void Sample(float fixedDeltaSeconds, WheelPhysicsSample[] destination)
            {
                for (int index = 0; index < WheelCount; index++)
                {
                    destination[index] = new WheelPhysicsSample(
                        true,
                        Vector3.zero,
                        Vector3.up,
                        1600f,
                        0f,
                        0f,
                        0f,
                        0.35f,
                        VehicleSurfaceType.Paved);
                }
            }

            public void Apply(float fixedDeltaSeconds, WheelPhysicsCommand[] commands)
            {
            }

            public void Reset()
            {
            }
        }
    }
}
