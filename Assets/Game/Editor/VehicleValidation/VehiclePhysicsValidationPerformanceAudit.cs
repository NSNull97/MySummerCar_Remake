using System;
using System.Diagnostics;
using System.IO;
using MSC.Editor.VehicleSimulation;
using MSC.Vehicle.Simulation;
using UnityEditor;
using UnityEngine;

namespace MSC.Editor.VehicleValidation
{
    public static class VehiclePhysicsValidationPerformanceAudit
    {
        private const int EvidenceSchemaVersion = 2;
        private const string ValidatorId = "m06a-pure-managed-performance.v2";
        private const int WarmupTicks = 512;
        private const int MeasuredTicks = 20000;
        private const double MaximumSanityMicrosecondsPerTick = 2000d;
        private const double MaximumSanityAllocatedBytesPerTick = 0.25d;
        private static double checksum;

        [Serializable]
        private sealed class Evidence
        {
            public int schemaVersion = EvidenceSchemaVersion;
            public string validatorId = ValidatorId;
            public string milestone = "06A";
            public string capturedUtc = string.Empty;
            public string unityVersion = string.Empty;
            public string operatingSystem = string.Empty;
            public string processor = string.Empty;
            public string graphicsDevice = string.Empty;
            public string executionMode = string.Empty;
            public bool batchMode;
            public float fixedTimestepSeconds;
            public long stopwatchFrequency;
            public string vehicleConfigurationAssetPath = string.Empty;
            public string vehicleConfigurationId = string.Empty;
            public int tuningSchemaVersion;
            public string configJsonHash = string.Empty;
            public string calibrationProfileAssetPath = string.Empty;
            public string calibrationProfileId = string.Empty;
            public int calibrationProfileSchemaVersion;
            public string calibrationProfileRevision = string.Empty;
            public string calibrationProfileFingerprint = string.Empty;
            public int selectedSimulationSubsteps;
            public int warmupTicks;
            public int measuredTicks;
            public double maximumSanityMicrosecondsPerTick =
                MaximumSanityMicrosecondsPerTick;
            public double maximumSanityAllocatedBytesPerTick =
                MaximumSanityAllocatedBytesPerTick;
            public bool passedSanityChecks;
            public string[] sanityFailures = Array.Empty<string>();
            public PerformanceRow[] rows = Array.Empty<PerformanceRow>();
            public string benchmarkScope =
                "Editor Stopwatch audit of VehicleSimulationRoot with FixedPerformanceBackend. " +
                "The backend supplies fixed wheel samples and only checks emitted commands; " +
                "no Rigidbody, collision detection, solver, raycast, rendering, streaming or player build is measured.";
            public string resultInterpretation =
                "Thresholds are regression sanity guards for this pure managed benchmark only. " +
                "They are not an FPS target, frame-time budget, PhysX result or production-player performance claim.";
            public string backendComparison =
                "N/A: only PrototypeRaycastWheelPhysicsBackend exists; no second custom/simple backend pair.";
            public string physicsProcessingBoundary =
                "Unity Physics.Processing is not executed or isolated by this fixed-backend Editor Stopwatch audit; " +
                "PlayMode fixed-step evidence is reported separately.";
        }

        [Serializable]
        private sealed class PerformanceRow
        {
            public string metricId = string.Empty;
            public int substeps;
            public double microsecondsPerTick;
            public long allocatedBytes;
            public double allocatedBytesPerTick;
            public double maximumSanityMicrosecondsPerTick =
                MaximumSanityMicrosecondsPerTick;
            public double maximumSanityAllocatedBytesPerTick =
                MaximumSanityAllocatedBytesPerTick;
            public bool passedSanityCheck;
            public string sanityFailure = string.Empty;
            public string scope = string.Empty;
        }

        [MenuItem(VehiclePhysicsValidationPaths.MenuRoot + "Run Performance Audit")]
        public static void Run()
        {
            VehicleSimulationConfig source = AssetDatabase.LoadAssetAtPath<VehicleSimulationConfig>(
                VehicleSimulationPrototypePaths.PrototypeConfig);
            if (source == null)
            {
                throw new InvalidOperationException("M06A performance audit requires the M06 simulation config.");
            }

            if (!source.Validate(out string sourceFailure))
            {
                throw new InvalidOperationException("M06A performance config is invalid: " + sourceFailure);
            }

            VehicleCalibrationProfile profile =
                AssetDatabase.LoadAssetAtPath<VehicleCalibrationProfile>(
                    VehiclePhysicsValidationPaths.Profile);
            if (profile == null)
            {
                throw new InvalidOperationException(
                    "M06A performance audit requires the active calibration profile.");
            }

            if (!profile.Validate(out string profileFailure))
            {
                throw new InvalidOperationException(
                    "M06A performance calibration profile is invalid: " + profileFailure);
            }

            if (profile.VehicleConfiguration != source)
            {
                throw new InvalidOperationException(
                    "M06A performance profile does not reference the active M06 simulation config asset.");
            }

            float fixedTimestepSeconds = Time.fixedDeltaTime;
            if (!(fixedTimestepSeconds > 0f) ||
                float.IsNaN(fixedTimestepSeconds) ||
                float.IsInfinity(fixedTimestepSeconds))
            {
                throw new InvalidOperationException(
                    "M06A performance audit requires a finite positive Unity fixed timestep.");
            }

            int selectedSubsteps = source.SubstepCount;
            VehicleSimulationConfig config = UnityEngine.Object.Instantiate(source);
            config.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                var rows = new PerformanceRow[6];
                rows[0] = MeasureRoot(
                    config,
                    fixedTimestepSeconds,
                    1,
                    captureTelemetry: false,
                    scriptedInput: false,
                    "simulation.baseline.substeps1", "Pure root; fixed backend; recorder off.");
                rows[1] = MeasureRoot(
                    config,
                    fixedTimestepSeconds,
                    2,
                    captureTelemetry: false,
                    scriptedInput: false,
                    "simulation.substeps2", "Pure root; fixed backend; recorder off.");
                rows[2] = MeasureRoot(
                    config,
                    fixedTimestepSeconds,
                    selectedSubsteps,
                    captureTelemetry: false,
                    scriptedInput: false,
                    "simulation.selected.active_config",
                    "Active config substep count; pure root; fixed backend; recorder off.");
                rows[3] = MeasureRoot(
                    config,
                    fixedTimestepSeconds,
                    selectedSubsteps,
                    captureTelemetry: true,
                    scriptedInput: false,
                    "telemetry.snapshot.on", "Root plus fixed telemetry snapshot copy; CSV formatting/I/O excluded.");
                rows[4] = MeasureRoot(
                    config,
                    fixedTimestepSeconds,
                    selectedSubsteps,
                    captureTelemetry: false,
                    scriptedInput: true,
                    "validation.scripted.input", "Root plus repeatable eight-phase scripted-input evaluation.");
                rows[5] = MeasureRoot(
                    config,
                    fixedTimestepSeconds,
                    selectedSubsteps,
                    captureTelemetry: true,
                    scriptedInput: true,
                    "validation.scripted.with_telemetry", "Scripted validation plus fixed telemetry snapshot copy.");

                string[] sanityFailures = BuildSanityFailures(rows);
                var evidence = new Evidence
                {
                    capturedUtc = DateTime.UtcNow.ToString("O"),
                    unityVersion = Application.unityVersion,
                    operatingSystem = SystemInfo.operatingSystem,
                    processor = SystemInfo.processorType,
                    graphicsDevice = SystemInfo.graphicsDeviceName,
                    executionMode = Application.isBatchMode
                        ? "EditModeBatch/PureManagedStopwatch"
                        : "EditModeInteractive/PureManagedStopwatch",
                    batchMode = Application.isBatchMode,
                    fixedTimestepSeconds = fixedTimestepSeconds,
                    stopwatchFrequency = Stopwatch.Frequency,
                    vehicleConfigurationAssetPath =
                        AssetDatabase.GetAssetPath(source),
                    vehicleConfigurationId = source.ConfigurationId,
                    tuningSchemaVersion = source.TuningSchemaVersion,
                    configJsonHash =
                        Hash128.Compute(JsonUtility.ToJson(source)).ToString(),
                    calibrationProfileAssetPath =
                        AssetDatabase.GetAssetPath(profile),
                    calibrationProfileId = profile.ProfileId,
                    calibrationProfileSchemaVersion = profile.SchemaVersion,
                    calibrationProfileRevision = profile.Revision,
                    calibrationProfileFingerprint =
                        profile.ComputeContentFingerprint(),
                    selectedSimulationSubsteps = selectedSubsteps,
                    warmupTicks = WarmupTicks,
                    measuredTicks = MeasuredTicks,
                    passedSanityChecks = sanityFailures.Length == 0,
                    sanityFailures = sanityFailures,
                    rows = rows
                };

                string fullPath = Path.GetFullPath(VehiclePhysicsValidationPaths.PerformanceEvidence);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? ".");
                File.WriteAllText(fullPath, JsonUtility.ToJson(evidence, prettyPrint: true));
                AssetDatabase.Refresh();
                if (!evidence.passedSanityChecks)
                {
                    throw new InvalidOperationException(
                        "M06A pure managed performance sanity checks failed: " +
                        string.Join(" | ", evidence.sanityFailures) +
                        ". Evidence was written to " + fullPath);
                }

                UnityEngine.Debug.Log(
                    "M06A_PHYSICS_VALIDATION_PERFORMANCE_OK " +
                    $"selectedUs={rows[2].microsecondsPerTick:0.######} " +
                    $"telemetryUs={rows[3].microsecondsPerTick:0.######} " +
                    $"scriptedUs={rows[4].microsecondsPerTick:0.######} " +
                    $"allocatedBytes={TotalAllocatedBytes(rows)} " +
                    $"scope=pure-managed-fixed-backend output={fullPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("M06A performance batch entry requires batch mode.");
            }

            Run();
        }

        private static PerformanceRow MeasureRoot(
            VehicleSimulationConfig config,
            float fixedTimestepSeconds,
            int substeps,
            bool captureTelemetry,
            bool scriptedInput,
            string metricId,
            string scope)
        {
            config.SetSubstepCountForTesting(substeps);
            var backend = new FixedPerformanceBackend(config);
            var root = new VehicleSimulationRoot(
                config,
                backend,
                new AvailablePrerequisiteSource());

            for (int tick = 0; tick < WarmupTicks; tick++)
            {
                VehicleInputState input = BuildInput(tick, scriptedInput);
                root.Tick(fixedTimestepSeconds, input);
                if (captureTelemetry)
                {
                    checksum += CaptureTelemetry(root.Telemetry);
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            long started = Stopwatch.GetTimestamp();
            for (int tick = 0; tick < MeasuredTicks; tick++)
            {
                VehicleInputState input = BuildInput(tick, scriptedInput);
                root.Tick(fixedTimestepSeconds, input);
                if (captureTelemetry)
                {
                    checksum += CaptureTelemetry(root.Telemetry);
                }
            }

            long stopped = Stopwatch.GetTimestamp();
            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - beforeBytes;
            double elapsedSeconds = (stopped - started) / (double)Stopwatch.Frequency;
            double microsecondsPerTick = elapsedSeconds * 1000000d / MeasuredTicks;
            double allocatedBytesPerTick = allocatedBytes / (double)MeasuredTicks;
            bool timePassed = microsecondsPerTick <= MaximumSanityMicrosecondsPerTick;
            bool allocationsPassed =
                allocatedBytesPerTick <= MaximumSanityAllocatedBytesPerTick;
            return new PerformanceRow
            {
                metricId = metricId,
                substeps = substeps,
                microsecondsPerTick = microsecondsPerTick,
                allocatedBytes = allocatedBytes,
                allocatedBytesPerTick = allocatedBytesPerTick,
                passedSanityCheck = timePassed && allocationsPassed,
                sanityFailure = BuildSanityFailure(
                    timePassed,
                    allocationsPassed,
                    microsecondsPerTick,
                    allocatedBytesPerTick),
                scope = scope
            };
        }

        private static VehicleInputState BuildInput(int tick, bool scripted)
        {
            if (!scripted)
            {
                return VehicleInputState.Neutral(ignitionOn: true);
            }

            int phase = tick % 800;
            if (phase < 50)
            {
                return new VehicleInputState(0f, 1f, 0f, 0f, true, true, false, 0);
            }

            if (phase < 150)
            {
                return new VehicleInputState(0f, 1f, 0f, 0f, true, false, false, 0);
            }

            if (phase < 300)
            {
                return new VehicleInputState(0.8f, 0.65f, 0f, 0f, true, false, phase == 150, 1);
            }

            if (phase < 400)
            {
                return new VehicleInputState(0f, 1f, 1f, 0f, true, false, false, 1);
            }

            if (phase < 500)
            {
                return new VehicleInputState(0f, 1f, 0f, 0f, true, false, phase == 400, 0);
            }

            if (phase < 600)
            {
                return new VehicleInputState(0.35f, 0.75f, 0f, 0.55f, true, false, phase == 500, 1);
            }

            if (phase < 700)
            {
                return new VehicleInputState(0.35f, 0.75f, 0f, -0.55f, true, false, false, 1);
            }

            return new VehicleInputState(0f, 1f, 0.4f, 0f, true, false, false, 1);
        }

        private static double CaptureTelemetry(VehicleTelemetry telemetry)
        {
            double value = telemetry.EngineRpm + telemetry.Throttle01 +
                           telemetry.EngineLoad01 + telemetry.EngineTorqueNewtonMeters +
                           telemetry.ClutchSlipRpm + telemetry.SelectedGear +
                           telemetry.GearboxInputRadiansPerSecond +
                           telemetry.GearboxOutputRadiansPerSecond +
                           telemetry.DifferentialTorqueNewtonMeters +
                           telemetry.VehicleSpeedMetersPerSecond +
                           telemetry.SteeringAngleDegrees + telemetry.Brake01 +
                           telemetry.BatteryVoltage + telemetry.EngineTemperatureCelsius;
            for (int wheel = 0; wheel < telemetry.WheelCount; wheel++)
            {
                VehicleWheelTelemetry item = telemetry.GetWheel(wheel);
                value += item.NormalLoadNewtons + item.LongitudinalSlip +
                         item.LateralSlip + item.AngularSpeedRadiansPerSecond +
                         item.SuspensionCompression01 + (int)item.Surface;
            }

            return value;
        }

        private static long TotalAllocatedBytes(PerformanceRow[] rows)
        {
            long result = 0;
            for (int index = 0; index < rows.Length; index++)
            {
                result += rows[index].allocatedBytes;
            }

            return result;
        }

        private static string BuildSanityFailure(
            bool timePassed,
            bool allocationsPassed,
            double microsecondsPerTick,
            double allocatedBytesPerTick)
        {
            if (timePassed && allocationsPassed)
            {
                return string.Empty;
            }

            if (!timePassed && !allocationsPassed)
            {
                return
                    $"Pure managed time {microsecondsPerTick:0.######} us/tick exceeds " +
                    $"{MaximumSanityMicrosecondsPerTick:0.######} us/tick and allocation " +
                    $"{allocatedBytesPerTick:0.######} B/tick exceeds " +
                    $"{MaximumSanityAllocatedBytesPerTick:0.######} B/tick.";
            }

            return !timePassed
                ? $"Pure managed time {microsecondsPerTick:0.######} us/tick exceeds " +
                  $"{MaximumSanityMicrosecondsPerTick:0.######} us/tick."
                : $"Managed allocation {allocatedBytesPerTick:0.######} B/tick exceeds " +
                  $"{MaximumSanityAllocatedBytesPerTick:0.######} B/tick.";
        }

        private static string[] BuildSanityFailures(PerformanceRow[] rows)
        {
            int failureCount = 0;
            for (int index = 0; index < rows.Length; index++)
            {
                if (!rows[index].passedSanityCheck)
                {
                    failureCount++;
                }
            }

            if (failureCount == 0)
            {
                return Array.Empty<string>();
            }

            var failures = new string[failureCount];
            int destinationIndex = 0;
            for (int index = 0; index < rows.Length; index++)
            {
                PerformanceRow row = rows[index];
                if (row.passedSanityCheck)
                {
                    continue;
                }

                failures[destinationIndex++] =
                    row.metricId + ": " + row.sanityFailure;
            }

            return failures;
        }

        private sealed class AvailablePrerequisiteSource : IVehicleSimulationPrerequisiteSource
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

        private sealed class FixedPerformanceBackend : IWheelPhysicsBackend
        {
            private readonly VehicleSimulationConfig config;

            public FixedPerformanceBackend(VehicleSimulationConfig configured)
            {
                config = configured;
            }

            public int WheelCount => config.WheelCount;
            public float VehicleSpeedMetersPerSecond => 8f;

            public void Sample(float fixedDeltaSeconds, WheelPhysicsSample[] destination)
            {
                float omega = VehicleSpeedMetersPerSecond / config.Dynamics.WheelRadiusMeters;
                float load = config.Dynamics.ProvisionalMassKilograms * 9.81f / WheelCount;
                for (int index = 0; index < WheelCount; index++)
                {
                    destination[index] = new WheelPhysicsSample(
                        true,
                        Vector3.zero,
                        Vector3.up,
                        load,
                        0.02f,
                        0.01f,
                        omega,
                        0.35f,
                        VehicleSurfaceType.Paved);
                }
            }

            public void Apply(float fixedDeltaSeconds, WheelPhysicsCommand[] commands)
            {
                double localChecksum = 0d;
                for (int index = 0; index < commands.Length; index++)
                {
                    localChecksum += commands[index].DriveTorqueNewtonMeters +
                                     commands[index].BrakeTorqueNewtonMeters +
                                     commands[index].SteeringAngleDegrees;
                }

                checksum += localChecksum;
            }

            public void Reset()
            {
            }
        }
    }
}
