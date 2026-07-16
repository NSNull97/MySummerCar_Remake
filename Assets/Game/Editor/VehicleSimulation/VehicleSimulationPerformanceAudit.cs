using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using MSC.Vehicle.Simulation;
using MSC.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace MSC.Editor.VehicleSimulation
{
    public static class VehicleSimulationPerformanceAudit
    {
        private const int WarmupTicks = 512;
        private const int MeasuredTicks = 10000;
        private const int BackendIterations = 50000;
        private const int TelemetryIterations = 50000;
        private const float FixedStepSeconds = 0.02f;

        [Serializable]
        private sealed class AuditReport
        {
            public int schemaVersion = 1;
            public string capturedUtc = string.Empty;
            public string unityVersion = string.Empty;
            public string tuningLabel = string.Empty;
            public string simulationMethodology =
                "Pure VehicleSimulationRoot with preallocated synthetic contact backend; isolates simulation/substep cost.";
            public string backendMethodology =
                "Actual PrototypeRaycastWheelPhysicsBackend.Sample+Apply in the authored M06 scene; excludes the later Physics.Processing phase.";
            public string telemetryMethodology =
                "Full fixed-capacity value snapshot equivalent to recorder frame copy; excludes FixedUpdate scheduling, CSV formatting and file IO.";
            public string physicsProcessing =
                "UnavailableNotMeasured: Physics.Processing was not isolated by this EditMode Stopwatch audit.";
            public int warmupTicks;
            public int measuredTicks;
            public SubstepResult[] substeps = Array.Empty<SubstepResult>();
            public LoopResult backend = new LoopResult();
            public LoopResult telemetryBuffer = new LoopResult();
        }

        [Serializable]
        private sealed class SubstepResult
        {
            public int substepCount;
            public double totalMilliseconds;
            public double microsecondsPerTick;
            public long allocatedBytes;
        }

        [Serializable]
        private sealed class LoopResult
        {
            public int iterations;
            public double totalMilliseconds;
            public double microsecondsPerIteration;
            public long allocatedBytes;
        }

        private struct TelemetryBufferFrame
        {
            public float EngineRpm;
            public float ElapsedSeconds;
            public float Throttle;
            public float EngineLoad;
            public float EngineTorque;
            public float ClutchSlip;
            public float GearboxInput;
            public float GearboxOutput;
            public float DifferentialTorque;
            public float Speed;
            public float Steering;
            public float Brake;
            public float BatteryVoltage;
            public float EngineTemperature;
            public float FixedStep;
            public double BackendSampleMilliseconds;
            public double BackendApplyMilliseconds;
            public double SimulationMilliseconds;
            public int Gear;
            public VehicleShiftStatus ShiftStatus;
            public int Substeps;
            public VehicleSimulationPrerequisiteFailure Failures;
            public TelemetryWheelFrame Wheel0;
            public TelemetryWheelFrame Wheel1;
            public TelemetryWheelFrame Wheel2;
            public TelemetryWheelFrame Wheel3;

            public static TelemetryBufferFrame Capture(
                VehicleSimulationState state,
                VehicleTelemetry telemetry)
            {
                return new TelemetryBufferFrame
                {
                    ElapsedSeconds = state.ElapsedSeconds,
                    EngineRpm = telemetry.EngineRpm,
                    Throttle = telemetry.Throttle01,
                    EngineLoad = telemetry.EngineLoad01,
                    EngineTorque = telemetry.EngineTorqueNewtonMeters,
                    ClutchSlip = telemetry.ClutchSlipRpm,
                    GearboxInput = telemetry.GearboxInputRadiansPerSecond,
                    GearboxOutput = telemetry.GearboxOutputRadiansPerSecond,
                    DifferentialTorque = telemetry.DifferentialTorqueNewtonMeters,
                    Speed = telemetry.VehicleSpeedMetersPerSecond,
                    Steering = telemetry.SteeringAngleDegrees,
                    Brake = telemetry.Brake01,
                    BatteryVoltage = telemetry.BatteryVoltage,
                    EngineTemperature = telemetry.EngineTemperatureCelsius,
                    FixedStep = telemetry.FixedStepSeconds,
                    BackendSampleMilliseconds = telemetry.BackendSampleMilliseconds,
                    BackendApplyMilliseconds = telemetry.BackendApplyMilliseconds,
                    SimulationMilliseconds = telemetry.SimulationMilliseconds,
                    Gear = telemetry.SelectedGear,
                    ShiftStatus = telemetry.ShiftStatus,
                    Substeps = telemetry.SubstepCount,
                    Failures = telemetry.PrerequisiteFailures,
                    Wheel0 = TelemetryWheelFrame.Capture(telemetry.GetWheel(0)),
                    Wheel1 = TelemetryWheelFrame.Capture(telemetry.GetWheel(1)),
                    Wheel2 = TelemetryWheelFrame.Capture(telemetry.GetWheel(2)),
                    Wheel3 = TelemetryWheelFrame.Capture(telemetry.GetWheel(3))
                };
            }
        }

        private struct TelemetryWheelFrame
        {
            public bool Contact;
            public float NormalLoad;
            public float LongitudinalSlip;
            public float LateralSlip;
            public float AngularSpeed;
            public float Suspension;
            public VehicleSurfaceType Surface;

            public static TelemetryWheelFrame Capture(VehicleWheelTelemetry telemetry)
            {
                return new TelemetryWheelFrame
                {
                    Contact = telemetry.HasContact,
                    NormalLoad = telemetry.NormalLoadNewtons,
                    LongitudinalSlip = telemetry.LongitudinalSlip,
                    LateralSlip = telemetry.LateralSlip,
                    AngularSpeed = telemetry.AngularSpeedRadiansPerSecond,
                    Suspension = telemetry.SuspensionCompression01,
                    Surface = telemetry.Surface
                };
            }
        }

        [MenuItem(VehicleSimulationPrototypePaths.MenuRoot + "Run Performance Audit")]
        public static void Run()
        {
            VehicleSimulationConfig source = AssetDatabase.LoadAssetAtPath<VehicleSimulationConfig>(
                VehicleSimulationPrototypePaths.PrototypeConfig);
            if (source == null)
            {
                throw new InvalidOperationException("M06 performance audit config is missing.");
            }

            var report = new AuditReport
            {
                capturedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                tuningLabel = source.DynamicTuning.Label,
                warmupTicks = WarmupTicks,
                measuredTicks = MeasuredTicks,
                substeps = new SubstepResult[3]
            };

            int[] substeps = { 1, 2, 4 };
            for (int index = 0; index < substeps.Length; index++)
            {
                report.substeps[index] = MeasureSimulation(source, substeps[index]);
            }

            report.backend = MeasureActualBackend();
            report.telemetryBuffer = MeasureTelemetryBuffer(source);

            for (int index = 0; index < report.substeps.Length; index++)
            {
                if (report.substeps[index].allocatedBytes != 0)
                {
                    throw new InvalidOperationException(
                        $"M06 normal simulation loop allocated {report.substeps[index].allocatedBytes} bytes " +
                        $"with {report.substeps[index].substepCount} substeps.");
                }
            }

            if (report.backend.allocatedBytes != 0 || report.telemetryBuffer.allocatedBytes != 0)
            {
                throw new InvalidOperationException(
                    $"M06 benchmark loops allocated memory: backend={report.backend.allocatedBytes}, " +
                    $"telemetry={report.telemetryBuffer.allocatedBytes}.");
            }

            string fullPath = Path.GetFullPath(VehicleSimulationPrototypePaths.PerformanceReport);
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("M06 performance report path has no parent.");
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(
                fullPath,
                JsonUtility.ToJson(report, prettyPrint: true) + Environment.NewLine,
                new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log(
                "M06_VEHICLE_SIMULATION_PERFORMANCE_OK " +
                $"substep1_us={report.substeps[0].microsecondsPerTick:0.###} " +
                $"substep2_us={report.substeps[1].microsecondsPerTick:0.###} " +
                $"substep4_us={report.substeps[2].microsecondsPerTick:0.###} " +
                $"backend_us={report.backend.microsecondsPerIteration:0.###} " +
                $"telemetry_us={report.telemetryBuffer.microsecondsPerIteration:0.###} " +
                "physicsProcessing=UnavailableNotMeasured");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("M06 performance batch entry requires batch mode.");
            }

            Run();
        }

        private static SubstepResult MeasureSimulation(
            VehicleSimulationConfig source,
            int substepCount)
        {
            VehicleSimulationConfig config = UnityEngine.Object.Instantiate(source);
            config.SetSubstepCountForTesting(substepCount);
            try
            {
                var backend = new BenchmarkWheelBackend(config.WheelCount);
                var root = new VehicleSimulationRoot(
                    config,
                    backend,
                    new BenchmarkPrerequisiteSource());
                VehicleInputState input = new VehicleInputState(
                    0.35f,
                    1f,
                    0f,
                    0.15f,
                    ignitionOn: true,
                    starterRequested: true,
                    gearChangeRequested: false,
                    requestedGear: 0);
                for (int index = 0; index < WarmupTicks; index++)
                {
                    root.Tick(FixedStepSeconds, input);
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long timestamp = Stopwatch.GetTimestamp();
                for (int index = 0; index < MeasuredTicks; index++)
                {
                    root.Tick(FixedStepSeconds, input);
                }

                double elapsed = ElapsedMilliseconds(timestamp);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
                return new SubstepResult
                {
                    substepCount = substepCount,
                    totalMilliseconds = elapsed,
                    microsecondsPerTick = elapsed * 1000d / MeasuredTicks,
                    allocatedBytes = allocated
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        private static LoopResult MeasureActualBackend()
        {
            Scene scene = EditorSceneManager.OpenScene(
                VehicleSimulationPrototypePaths.PrototypeScene,
                OpenSceneMode.Single);
            PrototypeRaycastWheelPhysicsBackend[] backends =
                VehicleSimulationEditorUtility.FindAllInScene<PrototypeRaycastWheelPhysicsBackend>(scene);
            if (backends.Length != 1)
            {
                throw new InvalidOperationException(
                    $"M06 performance audit expected one authored raycast backend, found {backends.Length}.");
            }

            PrototypeRaycastWheelPhysicsBackend backend = backends[0];
            backend.Configure(backend.Chassis, backend.Config, backend.Wheels);
            Physics.SyncTransforms();
            var samples = new WheelPhysicsSample[4];
            var commands = new WheelPhysicsCommand[4];
            for (int index = 0; index < 256; index++)
            {
                backend.Sample(FixedStepSeconds, samples);
                backend.Apply(FixedStepSeconds, commands);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long timestamp = Stopwatch.GetTimestamp();
            for (int index = 0; index < BackendIterations; index++)
            {
                backend.Sample(FixedStepSeconds, samples);
                backend.Apply(FixedStepSeconds, commands);
            }

            double elapsed = ElapsedMilliseconds(timestamp);
            return new LoopResult
            {
                iterations = BackendIterations,
                totalMilliseconds = elapsed,
                microsecondsPerIteration = elapsed * 1000d / BackendIterations,
                allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore
            };
        }

        private static LoopResult MeasureTelemetryBuffer(VehicleSimulationConfig source)
        {
            var backend = new BenchmarkWheelBackend(4);
            var root = new VehicleSimulationRoot(
                source,
                backend,
                new BenchmarkPrerequisiteSource());
            var buffer = new TelemetryBufferFrame[4096];
            VehicleInputState input = VehicleInputState.Neutral(ignitionOn: true);
            for (int index = 0; index < 256; index++)
            {
                root.Tick(FixedStepSeconds, input);
                buffer[index & (buffer.Length - 1)] =
                    TelemetryBufferFrame.Capture(root.State, root.Telemetry);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            long timestamp = Stopwatch.GetTimestamp();
            for (int index = 0; index < TelemetryIterations; index++)
            {
                buffer[index & (buffer.Length - 1)] =
                    TelemetryBufferFrame.Capture(root.State, root.Telemetry);
            }

            double elapsed = ElapsedMilliseconds(timestamp);
            GC.KeepAlive(buffer);
            return new LoopResult
            {
                iterations = TelemetryIterations,
                totalMilliseconds = elapsed,
                microsecondsPerIteration = elapsed * 1000d / TelemetryIterations,
                allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore
            };
        }

        private static double ElapsedMilliseconds(long started)
        {
            return (Stopwatch.GetTimestamp() - started) * 1000d / Stopwatch.Frequency;
        }

        private sealed class BenchmarkPrerequisiteSource : IVehicleSimulationPrerequisiteSource
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

        private sealed class BenchmarkWheelBackend : IWheelPhysicsBackend
        {
            public BenchmarkWheelBackend(int wheelCount)
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
