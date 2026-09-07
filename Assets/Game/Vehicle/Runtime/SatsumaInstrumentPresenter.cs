using System;
using MSC.Core.Time;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    public enum SatsumaInstrumentKind { Speed, Coolant, Fuel, ClockHour, ClockMinute, Odometer }
    public enum SatsumaInstrumentWarning { OilPressure, Charging, LeftIndicator, RightIndicator, HighBeam }

    [Serializable]
    public sealed class SatsumaInstrumentNeedle
    {
        [SerializeField] private SatsumaInstrumentKind kind;
        [SerializeField] private PartInstance owner;
        [SerializeField] private Transform leaf;
        [SerializeField] private Quaternion zeroRotation = Quaternion.identity;
        [SerializeField] private int digitPower;
        public SatsumaInstrumentKind Kind => kind;
        public PartInstance Owner => owner;
        public Transform Leaf => leaf;
        public Quaternion ZeroRotation => zeroRotation;
        public int DigitPower => digitPower;
        public SatsumaInstrumentNeedle(SatsumaInstrumentKind type, PartInstance part, Transform visual, Quaternion zero, int power = 0)
        { kind = type; owner = part; leaf = visual; zeroRotation = zero; digitPower = power; }
        public void SetAngle(float degrees) => leaf.localRotation = zeroRotation *
            Quaternion.AngleAxis(degrees, kind == SatsumaInstrumentKind.Odometer ? Vector3.right : Vector3.up);
    }

    [Serializable]
    public sealed class SatsumaInstrumentIllumination
    {
        [SerializeField] private PartInstance owner;
        [SerializeField] private Renderer renderer;
        [SerializeField, ColorUsage(true, true)] private Color emission;
        private MaterialPropertyBlock properties;
        private bool initialized, wasOn;
        private static readonly int EmissionId = Shader.PropertyToID("_EmissiveColor");
        public PartInstance Owner => owner;
        public Renderer Renderer => renderer;
        public SatsumaInstrumentIllumination(PartInstance part, Renderer visual, Color color)
        { owner = part; renderer = visual; emission = color; }
        public void SetOn(bool on)
        {
            on &= owner != null && owner.IsInstalled && renderer != null;
            if (renderer == null || initialized && on == wasOn) return;
            properties ??= new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
            properties.SetColor(EmissionId, on ? emission : Color.black); renderer.SetPropertyBlock(properties);
            initialized = true; wasOn = on;
        }
    }

    [Serializable]
    public sealed class SatsumaInstrumentWarningBinding
    {
        [SerializeField] private SatsumaInstrumentWarning kind;
        [SerializeField] private GameObject visual;
        public SatsumaInstrumentWarning Kind => kind;
        public GameObject Visual => visual;
        public SatsumaInstrumentWarningBinding(SatsumaInstrumentWarning type, GameObject target) { kind = type; visual = target; }
        public void SetOn(bool on) { if (visual != null && visual.activeSelf != on) visual.SetActive(on); }
    }

    /// <summary>Read-only cockpit presentation. Project simulation, assembly and game time remain authoritative.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(80)]
    public sealed class SatsumaInstrumentPresenter : MonoBehaviour
    {
        [SerializeField] private VehicleSimulationHost simulation;
        [SerializeField] private VehicleAssemblyController assembly;
        [SerializeField] private SatsumaDashboardControlsController controls;
        [SerializeField] private SatsumaIgnitionController ignition;
        [SerializeField] private PartInstance dashboard;
        [SerializeField] private PartInstance meters;
        [SerializeField] private PartInstance clock;
        [SerializeField] private SatsumaInstrumentNeedle[] needles = Array.Empty<SatsumaInstrumentNeedle>();
        [SerializeField] private SatsumaInstrumentIllumination[] illumination = Array.Empty<SatsumaInstrumentIllumination>();
        [SerializeField] private SatsumaInstrumentWarningBinding[] warnings = Array.Empty<SatsumaInstrumentWarningBinding>();
        private IGameTimeService gameTime;
        public SatsumaInstrumentNeedle[] Needles => needles;
        public SatsumaInstrumentIllumination[] Illumination => illumination;
        public SatsumaInstrumentWarningBinding[] Warnings => warnings;
        public bool HasGameTimeBinding => gameTime != null;

        public void Configure(VehicleSimulationHost host, VehicleAssemblyController vehicle, SatsumaDashboardControlsController inputs,
            SatsumaIgnitionController key, PartInstance dashPart, PartInstance metersPart, PartInstance clockPart,
            SatsumaInstrumentNeedle[] rotations, SatsumaInstrumentIllumination[] lighting, SatsumaInstrumentWarningBinding[] lamps)
        {
            if (host == null || vehicle == null || inputs == null || key == null || dashPart == null || metersPart == null ||
                clockPart == null || rotations == null || lighting == null || lamps == null) throw new ArgumentException("Explicit cockpit bindings required.");
            foreach (var binding in rotations)
                if (binding == null || binding.Owner == null || !SatsumaEngineVisualVibration.IsSafeVisualLeaf(binding.Leaf))
                    throw new ArgumentException("Instrument motion may only target an owned render leaf.");
            simulation = host; assembly = vehicle; controls = inputs; ignition = key; dashboard = dashPart; meters = metersPart; clock = clockPart;
            needles = (SatsumaInstrumentNeedle[])rotations.Clone(); illumination = (SatsumaInstrumentIllumination[])lighting.Clone();
            warnings = (SatsumaInstrumentWarningBinding[])lamps.Clone();
        }
        public void BindGameTime(IGameTimeService service) => gameTime = service ?? throw new ArgumentNullException(nameof(service));
        public void RefreshOutputs()
        {
            if (simulation == null || simulation.State == null || controls == null || controls.Electrical == null) return;
            var state = simulation.State; var power = controls.Electrical;
            bool installed = isActiveAndEnabled && dashboard.IsInstalled && meters.IsInstalled;
            bool dashWire = power.IsConnectionInstalled(SatsumaElectricalConnection.Dash1);
            bool live = installed && power.ElectricsOk && dashWire;
            bool acc = ignition != null && ignition.IgnitionOn;
            // The frozen clock checks five Installed flags, not ACC or the
            // starter circuit. Do not invent a key-on dependency for a clock.
            bool clockReady = installed && clock.IsInstalled && power.IsBatteryInstalled && dashWire && gameTime != null;
            float differentialRadians = 0f;
            if (state.WheelCount == simulation.Config.WheelCount)
                differentialRadians = (state.GetWheelState(simulation.Config.LeftDrivenWheelIndex).AngularSpeedRadiansPerSecond +
                    state.GetWheelState(simulation.Config.RightDrivenWheelIndex).AngularSpeedRadiansPerSecond) * .5f;
            foreach (var needle in needles)
            {
                if (needle.Leaf == null || !needle.Owner.IsInstalled) continue;
                float angle;
                switch (needle.Kind)
                {
                    // Donor differentialSpeed is rad/s, not km/h. This also
                    // preserves wheelspin indication without a GPS-speed fake.
                    case SatsumaInstrumentKind.Speed: angle = SpeedAngle(differentialRadians); break;
                    case SatsumaInstrumentKind.Coolant: angle = live ? CoolantAngle(state.CoolantTemperatureCelsius) : 0f; break;
                    case SatsumaInstrumentKind.Fuel: angle = live && acc && power.IsConnectionInstalled(SatsumaElectricalConnection.FuelTank)
                        ? FuelAngle(state.FuelLiters) : 0f; break;
                    case SatsumaInstrumentKind.ClockHour:
                        if (!clockReady) continue; angle = ClockHourAngle(gameTime.Snapshot.SecondsOfDay); break;
                    case SatsumaInstrumentKind.ClockMinute:
                        if (!clockReady) continue; angle = ClockMinuteAngle(gameTime.Snapshot.SecondsOfDay); break;
                    case SatsumaInstrumentKind.Odometer:
                        if (state.SatsumaOperating == null) continue;
                        angle = OdometerAngle(state.SatsumaOperating.OdometerKilometers, needle.DigitPower); break;
                    default: continue;
                }
                needle.SetAngle(angle);
            }
            bool lit = live && controls.CanOperate && controls.HeadlightsMode != SatsumaHeadlightsMode.Off &&
                power.IsConnectionInstalled(SatsumaElectricalConnection.SwitchLights);
            foreach (var binding in illumination) binding.SetOn(lit);
            bool generating = simulation.Root?.SatsumaOperatingModel?.LastPoint.AlternatorGenerating == true &&
                power.IsConnectionInstalled(SatsumaElectricalConnection.Alternator) &&
                power.IsConnectionInstalled(SatsumaElectricalConnection.RegulatorHarness);
            foreach (var warning in warnings) warning.SetOn(live && (warning.Kind switch
            {
                SatsumaInstrumentWarning.OilPressure => acc && state.SatsumaOperating != null && state.SatsumaOperating.OilPressureBar < .8f,
                SatsumaInstrumentWarning.Charging => acc && !generating,
                SatsumaInstrumentWarning.LeftIndicator or SatsumaInstrumentWarning.RightIndicator => controls.HazardPulseOn,
                // The established controller has no high-beam intent yet.
                // Keep the reviewed lens dark instead of mislabelling dipped beams.
                _ => false,
            }));
        }
        public static float SpeedAngle(float radiansPerSecond) => Mathf.Clamp(radiansPerSecond, 0f, 250f) * -1.6875f;
        public static float CoolantAngle(float celsius) => Mathf.Clamp(celsius * -.461f, -75f, 0f);
        public static float FuelAngle(float liters) => Mathf.Clamp(liters * -1.66f, -70f, 0f);
        public static float ClockHourAngle(double seconds) => (float)(-(seconds % 43200d) / 120d);
        public static float ClockMinuteAngle(double seconds) => (float)(-(seconds % 3600d) / 10d);
        public static float OdometerAngle(double kilometers, int power)
        {
            if (power is < 0 or > 5) throw new ArgumentOutOfRangeException(nameof(power));
            double digit = power == 0 ? kilometers % 10d : Math.Floor(kilometers / Math.Pow(10d, power)) % 10d;
            return (float)(digit * -36d);
        }
        private void LateUpdate() => RefreshOutputs();
        private void OnDisable()
        {
            foreach (var binding in illumination) binding.SetOn(false);
            foreach (var binding in warnings) binding.SetOn(false);
        }
    }
}
