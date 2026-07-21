using System.Globalization;
using System.Text;
using MSC.Core.Lifecycle;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    /// <summary>Development-only immediate telemetry view for the M06 prototype.</summary>
    [DefaultExecutionOrder(210)]
    [DisallowMultipleComponent]
    public sealed class VehicleTelemetryOverlay : MonoBehaviour, IUiVisibilityGate
    {
        [SerializeField] private VehicleSimulationHost host;
        [SerializeField] private bool visible = true;
        [SerializeField] private Rect screenRect = new Rect(12f, 12f, 470f, 430f);

        private bool uiSuppressed;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private readonly StringBuilder textBuffer = new StringBuilder(2048);
        private GUIStyle labelStyle;
#endif

        public VehicleSimulationHost Host => host;

        public bool Visible => visible;

        public bool IsUiSuppressed => uiSuppressed;

        public void SetUiSuppressed(bool suppressed)
        {
            uiSuppressed = suppressed;
        }

        private void Awake()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            labelStyle = null;
#else
            enabled = false;
#endif
        }

        public void Configure(VehicleSimulationHost simulationHost, bool initiallyVisible = true)
        {
            host = simulationHost;
            visible = initiallyVisible;
        }

        public void SetVisible(bool value)
        {
            visible = value;
        }

        public void ToggleVisible()
        {
            visible = !visible;
        }

        public VehicleSimulationPrerequisiteFailure GetVisiblePrerequisiteFailures()
        {
            return host != null && host.Telemetry != null
                ? host.Telemetry.PrerequisiteFailures
                : VehicleSimulationPrerequisiteFailure.PrerequisiteSourceUnavailable;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void OnGUI()
        {
            if (!visible || uiSuppressed || host == null || host.Telemetry == null)
            {
                return;
            }

            VehicleTelemetry telemetry = host.Telemetry;
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = false,
                    fontSize = 13
                };
            }

            textBuffer.Clear();
            AppendLine("M06 VEHICLE SIMULATION", string.Empty);
            AppendLine("RPM", telemetry.EngineRpm.ToString("0", CultureInfo.InvariantCulture));
            AppendLine("Throttle / load", FormatPair(telemetry.Throttle01, telemetry.EngineLoad01));
            AppendLine("Engine torque Nm", telemetry.EngineTorqueNewtonMeters.ToString("0.0", CultureInfo.InvariantCulture));
            AppendLine("Gear / shift", telemetry.SelectedGear + " / " + telemetry.ShiftStatus);
            AppendLine("Clutch slip RPM", telemetry.ClutchSlipRpm.ToString("0", CultureInfo.InvariantCulture));
            AppendLine("Gearbox in/out rad/s", FormatPair(telemetry.GearboxInputRadiansPerSecond, telemetry.GearboxOutputRadiansPerSecond));
            AppendLine("Differential torque Nm", telemetry.DifferentialTorqueNewtonMeters.ToString("0.0", CultureInfo.InvariantCulture));
            AppendLine("Speed km/h", (telemetry.VehicleSpeedMetersPerSecond * 3.6f).ToString("0.0", CultureInfo.InvariantCulture));
            AppendLine("Steering deg / brake", telemetry.SteeringAngleDegrees.ToString("0.0", CultureInfo.InvariantCulture) + " / " + telemetry.Brake01.ToString("0.00", CultureInfo.InvariantCulture));
            AppendLine("Battery V / engine C", telemetry.BatteryVoltage.ToString("0.00", CultureInfo.InvariantCulture) + " / " + telemetry.EngineTemperatureCelsius.ToString("0.0", CultureInfo.InvariantCulture));
            AppendLine("Prerequisites", telemetry.PrerequisiteFailures.ToString());
            AppendLine("Fixed / substeps", telemetry.FixedStepSeconds.ToString("0.0000", CultureInfo.InvariantCulture) + " / " + telemetry.SubstepCount);
            AppendLine("Tick ms", telemetry.SimulationMilliseconds.ToString("0.000", CultureInfo.InvariantCulture));
            AppendLine("Backend sample/apply ms", telemetry.BackendSampleMilliseconds.ToString("0.000", CultureInfo.InvariantCulture) + " / " + telemetry.BackendApplyMilliseconds.ToString("0.000", CultureInfo.InvariantCulture));

            for (int index = 0; index < telemetry.WheelCount; index++)
            {
                VehicleWheelTelemetry wheel = telemetry.GetWheel(index);
                textBuffer.Append("W").Append(index)
                    .Append(" contact=").Append(wheel.HasContact ? '1' : '0')
                    .Append(" load=").Append(wheel.NormalLoadNewtons.ToString("0", CultureInfo.InvariantCulture))
                    .Append(" slip=").Append(wheel.LongitudinalSlip.ToString("0.00", CultureInfo.InvariantCulture))
                    .Append('/').Append(wheel.LateralSlip.ToString("0.00", CultureInfo.InvariantCulture))
                    .Append(" omega=").Append(wheel.AngularSpeedRadiansPerSecond.ToString("0.0", CultureInfo.InvariantCulture))
                    .Append(" susp=").Append(wheel.SuspensionCompression01.ToString("0.00", CultureInfo.InvariantCulture))
                    .Append(" surface=").Append(wheel.Surface)
                    .AppendLine();
            }

            GUI.Box(screenRect, GUIContent.none);
            GUI.Label(
                new Rect(
                    screenRect.x + 8f,
                    screenRect.y + 6f,
                    screenRect.width - 16f,
                    screenRect.height - 12f),
                textBuffer.ToString(),
                labelStyle);
        }

        private void AppendLine(string label, string value)
        {
            textBuffer.Append(label);
            if (!string.IsNullOrEmpty(value))
            {
                textBuffer.Append(": ").Append(value);
            }

            textBuffer.AppendLine();
        }

        private static string FormatPair(float first, float second)
        {
            return first.ToString("0.00", CultureInfo.InvariantCulture) + " / " +
                   second.ToString("0.00", CultureInfo.InvariantCulture);
        }
#endif
    }
}
