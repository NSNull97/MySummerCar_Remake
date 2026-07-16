using System;
using System.Globalization;
using System.IO;
using System.Text;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>
    /// Fixed-capacity development recorder. The fixed-step capture path copies
    /// value types only; CSV strings and file IO are created exclusively on stop.
    /// </summary>
    [DefaultExecutionOrder(220)]
    [DisallowMultipleComponent]
    public sealed class VehicleTelemetryRecorder : MonoBehaviour
    {
        private const int MinimumCapacity = 64;
        private const string DefaultExportPath =
            "VehicleTelemetry/Milestone06/VehicleTelemetry.csv";

        [SerializeField] private VehicleSimulationHost host;
        [SerializeField, Min(MinimumCapacity)] private int capacity = 18000;
        [SerializeField] private string exportPath = DefaultExportPath;

        private TelemetryFrame[] frames = Array.Empty<TelemetryFrame>();
        private int count;
        private int lastCapturedFixedTick = -1;
        private bool capturing;
        private bool capacityReached;

        public VehicleSimulationHost Host => host;

        public int Capacity => capacity;

        public int Count => count;

        public bool IsCapturing => capturing;

        public bool CapacityReached => capacityReached;

        public string LastExportPath { get; private set; } = string.Empty;

        private void Awake()
        {
            EnsureStorage();
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            enabled = false;
#endif
        }

        private void FixedUpdate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!capturing || host == null || host.Telemetry == null ||
                host.FixedTickCount == lastCapturedFixedTick)
            {
                return;
            }

            if (count >= frames.Length)
            {
                capacityReached = true;
                capturing = false;
                return;
            }

            frames[count++] = TelemetryFrame.Capture(host.State, host.Telemetry);
            lastCapturedFixedTick = host.FixedTickCount;
#endif
        }

        public void Configure(
            VehicleSimulationHost simulationHost,
            int frameCapacity = 18000,
            string targetExportPath = DefaultExportPath)
        {
            host = simulationHost;
            capacity = Mathf.Max(MinimumCapacity, frameCapacity);
            exportPath = string.IsNullOrWhiteSpace(targetExportPath)
                ? DefaultExportPath
                : targetExportPath;
            EnsureStorage();
        }

        public bool StartCapture()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureStorage();
            count = 0;
            lastCapturedFixedTick = -1;
            capacityReached = false;
            LastExportPath = string.Empty;
            capturing = host != null;
            return capturing;
#else
            return false;
#endif
        }

        public string StopCaptureAndExport()
        {
            return StopCaptureAndExport(exportPath);
        }

        public string StopCaptureAndExport(string targetPath)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            capturing = false;
            string requestedPath = string.IsNullOrWhiteSpace(targetPath)
                ? DefaultExportPath
                : targetPath;
            string absolutePath = Path.GetFullPath(requestedPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("Telemetry export path has no parent directory.");
            }

            Directory.CreateDirectory(directory);
            var csv = new StringBuilder(Mathf.Max(4096, count * 420));
            AppendHeader(csv);
            for (int index = 0; index < count; index++)
            {
                frames[index].AppendCsv(csv);
            }

            File.WriteAllText(absolutePath, csv.ToString(), new UTF8Encoding(false));
            LastExportPath = absolutePath;
            return absolutePath;
#else
            return string.Empty;
#endif
        }

        public void StopWithoutExport()
        {
            capturing = false;
        }

        private void EnsureStorage()
        {
            int required = Mathf.Max(MinimumCapacity, capacity);
            if (frames.Length != required)
            {
                frames = new TelemetryFrame[required];
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void AppendHeader(StringBuilder csv)
        {
            csv.Append("elapsed_s,rpm,throttle,engine_load,engine_torque_nm,clutch_slip_rpm,gear,")
                .Append("shift_status,gearbox_input_rad_s,gearbox_output_rad_s,differential_torque_nm,")
                .Append("speed_m_s,steering_deg,brake,battery_v,engine_temp_c,prerequisite_flags,")
                .Append("fixed_step_s,substeps,simulation_ms,backend_sample_ms,backend_apply_ms");
            for (int wheel = 0; wheel < 4; wheel++)
            {
                csv.Append(",w").Append(wheel).Append("_contact")
                    .Append(",w").Append(wheel).Append("_normal_load_n")
                    .Append(",w").Append(wheel).Append("_longitudinal_slip")
                    .Append(",w").Append(wheel).Append("_lateral_slip")
                    .Append(",w").Append(wheel).Append("_omega_rad_s")
                    .Append(",w").Append(wheel).Append("_suspension_01")
                    .Append(",w").Append(wheel).Append("_surface");
            }

            csv.AppendLine();
        }
#endif

        private struct WheelFrame
        {
            public bool HasContact;
            public float NormalLoad;
            public float LongitudinalSlip;
            public float LateralSlip;
            public float AngularSpeed;
            public float SuspensionCompression;
            public VehicleSurfaceType Surface;

            public static WheelFrame Capture(VehicleTelemetry telemetry, int index)
            {
                if (telemetry == null || index >= telemetry.WheelCount)
                {
                    return default;
                }

                VehicleWheelTelemetry wheel = telemetry.GetWheel(index);
                return new WheelFrame
                {
                    HasContact = wheel.HasContact,
                    NormalLoad = wheel.NormalLoadNewtons,
                    LongitudinalSlip = wheel.LongitudinalSlip,
                    LateralSlip = wheel.LateralSlip,
                    AngularSpeed = wheel.AngularSpeedRadiansPerSecond,
                    SuspensionCompression = wheel.SuspensionCompression01,
                    Surface = wheel.Surface
                };
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            public void AppendCsv(StringBuilder csv)
            {
                csv.Append(',').Append(HasContact ? '1' : '0');
                AppendFloat(csv, NormalLoad);
                AppendFloat(csv, LongitudinalSlip);
                AppendFloat(csv, LateralSlip);
                AppendFloat(csv, AngularSpeed);
                AppendFloat(csv, SuspensionCompression);
                csv.Append(',').Append((int)Surface);
            }
#endif
        }

        private struct TelemetryFrame
        {
            public float ElapsedSeconds;
            public float EngineRpm;
            public float Throttle;
            public float EngineLoad;
            public float EngineTorque;
            public float ClutchSlip;
            public int Gear;
            public VehicleShiftStatus ShiftStatus;
            public float GearboxInput;
            public float GearboxOutput;
            public float DifferentialTorque;
            public float Speed;
            public float Steering;
            public float Brake;
            public float BatteryVoltage;
            public float EngineTemperature;
            public VehicleSimulationPrerequisiteFailure PrerequisiteFailures;
            public float FixedStepSeconds;
            public int Substeps;
            public double SimulationMilliseconds;
            public double BackendSampleMilliseconds;
            public double BackendApplyMilliseconds;
            public WheelFrame Wheel0;
            public WheelFrame Wheel1;
            public WheelFrame Wheel2;
            public WheelFrame Wheel3;

            public static TelemetryFrame Capture(
                VehicleSimulationState state,
                VehicleTelemetry telemetry)
            {
                return new TelemetryFrame
                {
                    ElapsedSeconds = state != null ? state.ElapsedSeconds : 0f,
                    EngineRpm = telemetry.EngineRpm,
                    Throttle = telemetry.Throttle01,
                    EngineLoad = telemetry.EngineLoad01,
                    EngineTorque = telemetry.EngineTorqueNewtonMeters,
                    ClutchSlip = telemetry.ClutchSlipRpm,
                    Gear = telemetry.SelectedGear,
                    ShiftStatus = telemetry.ShiftStatus,
                    GearboxInput = telemetry.GearboxInputRadiansPerSecond,
                    GearboxOutput = telemetry.GearboxOutputRadiansPerSecond,
                    DifferentialTorque = telemetry.DifferentialTorqueNewtonMeters,
                    Speed = telemetry.VehicleSpeedMetersPerSecond,
                    Steering = telemetry.SteeringAngleDegrees,
                    Brake = telemetry.Brake01,
                    BatteryVoltage = telemetry.BatteryVoltage,
                    EngineTemperature = telemetry.EngineTemperatureCelsius,
                    PrerequisiteFailures = telemetry.PrerequisiteFailures,
                    FixedStepSeconds = telemetry.FixedStepSeconds,
                    Substeps = telemetry.SubstepCount,
                    SimulationMilliseconds = telemetry.SimulationMilliseconds,
                    BackendSampleMilliseconds = telemetry.BackendSampleMilliseconds,
                    BackendApplyMilliseconds = telemetry.BackendApplyMilliseconds,
                    Wheel0 = WheelFrame.Capture(telemetry, 0),
                    Wheel1 = WheelFrame.Capture(telemetry, 1),
                    Wheel2 = WheelFrame.Capture(telemetry, 2),
                    Wheel3 = WheelFrame.Capture(telemetry, 3)
                };
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            public void AppendCsv(StringBuilder csv)
            {
                csv.Append(ElapsedSeconds.ToString("R", CultureInfo.InvariantCulture));
                AppendFloat(csv, EngineRpm);
                AppendFloat(csv, Throttle);
                AppendFloat(csv, EngineLoad);
                AppendFloat(csv, EngineTorque);
                AppendFloat(csv, ClutchSlip);
                csv.Append(',').Append(Gear);
                csv.Append(',').Append((int)ShiftStatus);
                AppendFloat(csv, GearboxInput);
                AppendFloat(csv, GearboxOutput);
                AppendFloat(csv, DifferentialTorque);
                AppendFloat(csv, Speed);
                AppendFloat(csv, Steering);
                AppendFloat(csv, Brake);
                AppendFloat(csv, BatteryVoltage);
                AppendFloat(csv, EngineTemperature);
                csv.Append(',').Append((int)PrerequisiteFailures);
                AppendFloat(csv, FixedStepSeconds);
                csv.Append(',').Append(Substeps);
                AppendDouble(csv, SimulationMilliseconds);
                AppendDouble(csv, BackendSampleMilliseconds);
                AppendDouble(csv, BackendApplyMilliseconds);
                Wheel0.AppendCsv(csv);
                Wheel1.AppendCsv(csv);
                Wheel2.AppendCsv(csv);
                Wheel3.AppendCsv(csv);
                csv.AppendLine();
            }
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static void AppendFloat(StringBuilder csv, float value)
        {
            csv.Append(',').Append(value.ToString("R", CultureInfo.InvariantCulture));
        }

        private static void AppendDouble(StringBuilder csv, double value)
        {
            csv.Append(',').Append(value.ToString("R", CultureInfo.InvariantCulture));
        }
#endif
    }
}
