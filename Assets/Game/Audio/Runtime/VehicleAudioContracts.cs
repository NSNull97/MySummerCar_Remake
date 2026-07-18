using System;

namespace MSC.Audio
{
    public enum VehicleAudioEngineState
    {
        Off = 0,
        Cranking = 1,
        Running = 2,
        Stalled = 3
    }

    public enum VehicleAudioSurface
    {
        Unknown = 0,
        Paved = 1,
        Gravel = 2,
        Dirt = 3,
        Grass = 4,
        MudWet = 5
    }

    public enum VehicleAudioEvent
    {
        StarterEngaged = 0,
        StarterDisengaged = 1,
        EngineStarted = 2,
        EngineStopped = 3,
        EngineStalled = 4,
        Reset = 5
    }

    /// <summary>
    /// Additional vehicle signals introduced after the original M06 diagnostic
    /// contract. Availability flags keep future gameplay domains explicit: an
    /// unavailable value must not be presented as measured zero.
    /// </summary>
    public readonly struct VehicleAudioSupplementalParameters
    {
        public VehicleAudioSupplementalParameters(
            bool ignitionOn,
            bool starterRequested,
            bool starterActive,
            float brake01,
            float aggregateWheelSpeedRadiansPerSecond,
            float signedVehicleSpeedMetersPerSecond,
            float suspensionImpact01,
            bool damageAvailable = false,
            float damage01 = 0f,
            bool interiorContextAvailable = false,
            float interiorBlend01 = 0f,
            bool openingsContextAvailable = false,
            float doorOpenness01 = 0f,
            float windowOpenness01 = 0f)
        {
            IgnitionOn = ignitionOn;
            StarterRequested = starterRequested;
            StarterActive = starterActive;
            Brake01 = brake01;
            AggregateWheelSpeedRadiansPerSecond = aggregateWheelSpeedRadiansPerSecond;
            SignedVehicleSpeedMetersPerSecond = signedVehicleSpeedMetersPerSecond;
            SuspensionImpact01 = suspensionImpact01;
            DamageAvailable = damageAvailable;
            Damage01 = damage01;
            InteriorContextAvailable = interiorContextAvailable;
            InteriorBlend01 = interiorBlend01;
            OpeningsContextAvailable = openingsContextAvailable;
            DoorOpenness01 = doorOpenness01;
            WindowOpenness01 = windowOpenness01;
        }

        public bool IgnitionOn { get; }
        public bool StarterRequested { get; }
        public bool StarterActive { get; }
        public float Brake01 { get; }
        public float AggregateWheelSpeedRadiansPerSecond { get; }
        public float SignedVehicleSpeedMetersPerSecond { get; }
        public float SuspensionImpact01 { get; }
        public bool DamageAvailable { get; }
        public float Damage01 { get; }
        public bool InteriorContextAvailable { get; }
        public float InteriorBlend01 { get; }
        public bool OpeningsContextAvailable { get; }
        public float DoorOpenness01 { get; }
        public float WindowOpenness01 { get; }

        public static VehicleAudioSupplementalParameters Unavailable => default;
    }

    /// <summary>
    /// Physical and normalized values exposed to an audio implementation. Audio is
    /// presentation-only and must never feed state back into the simulation.
    /// </summary>
    public readonly struct VehicleAudioParameters
    {
        public VehicleAudioParameters(
            VehicleAudioEngineState engineState,
            float engineRpm,
            float redlineRpm,
            float engineLoad01,
            float throttle01,
            int selectedGear,
            float clutchSlipRpm,
            float vehicleSpeedMetersPerSecond,
            float wheelSlip01,
            VehicleAudioSurface surface,
            float batteryVoltage,
            float engineTemperatureCelsius)
            : this(
                engineState,
                engineRpm,
                redlineRpm,
                engineLoad01,
                throttle01,
                selectedGear,
                clutchSlipRpm,
                vehicleSpeedMetersPerSecond,
                wheelSlip01,
                surface,
                batteryVoltage,
                engineTemperatureCelsius,
                VehicleAudioSupplementalParameters.Unavailable)
        {
        }

        public VehicleAudioParameters(
            VehicleAudioEngineState engineState,
            float engineRpm,
            float redlineRpm,
            float engineLoad01,
            float throttle01,
            int selectedGear,
            float clutchSlipRpm,
            float vehicleSpeedMetersPerSecond,
            float wheelSlip01,
            VehicleAudioSurface surface,
            float batteryVoltage,
            float engineTemperatureCelsius,
            VehicleAudioSupplementalParameters supplemental)
        {
            EngineState = engineState;
            EngineRpm = NonNegativeFinite(engineRpm);
            RedlineRpm = Math.Max(1f, NonNegativeFinite(redlineRpm));
            EngineLoad01 = Clamp01(engineLoad01);
            Throttle01 = Clamp01(throttle01);
            SelectedGear = selectedGear;
            ClutchSlipRpm = NonNegativeFinite(Math.Abs(clutchSlipRpm));
            VehicleSpeedMetersPerSecond = NonNegativeFinite(Math.Abs(vehicleSpeedMetersPerSecond));
            WheelSlip01 = Clamp01(Math.Abs(wheelSlip01));
            Surface = surface;
            BatteryVoltage = NonNegativeFinite(batteryVoltage);
            EngineTemperatureCelsius = FiniteOrZero(engineTemperatureCelsius);
            IgnitionOn = supplemental.IgnitionOn;
            StarterRequested = supplemental.StarterRequested;
            StarterActive = supplemental.StarterActive &&
                            engineState == VehicleAudioEngineState.Cranking;
            Brake01 = Clamp01(supplemental.Brake01);
            AggregateWheelSpeedRadiansPerSecond = NonNegativeFinite(
                Math.Abs(supplemental.AggregateWheelSpeedRadiansPerSecond));
            SignedVehicleSpeedMetersPerSecond = FiniteOrZero(
                supplemental.SignedVehicleSpeedMetersPerSecond);
            SuspensionImpact01 = Clamp01(supplemental.SuspensionImpact01);
            DamageAvailable = supplemental.DamageAvailable;
            Damage01 = DamageAvailable ? Clamp01(supplemental.Damage01) : 0f;
            InteriorContextAvailable = supplemental.InteriorContextAvailable;
            InteriorBlend01 = InteriorContextAvailable
                ? Clamp01(supplemental.InteriorBlend01)
                : 0f;
            OpeningsContextAvailable = supplemental.OpeningsContextAvailable;
            DoorOpenness01 = OpeningsContextAvailable
                ? Clamp01(supplemental.DoorOpenness01)
                : 0f;
            WindowOpenness01 = OpeningsContextAvailable
                ? Clamp01(supplemental.WindowOpenness01)
                : 0f;
        }

        public VehicleAudioEngineState EngineState { get; }
        public float EngineRpm { get; }
        public float RedlineRpm { get; }
        public float EngineLoad01 { get; }
        public float Throttle01 { get; }
        public int SelectedGear { get; }
        public float ClutchSlipRpm { get; }
        public float VehicleSpeedMetersPerSecond { get; }
        public float WheelSlip01 { get; }
        public VehicleAudioSurface Surface { get; }
        public float BatteryVoltage { get; }
        public float EngineTemperatureCelsius { get; }
        public bool IgnitionOn { get; }
        public bool StarterRequested { get; }
        public bool StarterActive { get; }
        public float Brake01 { get; }

        /// <summary>Arithmetic mean of absolute wheel angular speeds.</summary>
        public float AggregateWheelSpeedRadiansPerSecond { get; }

        /// <summary>
        /// Signed source speed when the simulation backend provides direction.
        /// VehicleSpeedMetersPerSecond remains the legacy absolute magnitude.
        /// </summary>
        public float SignedVehicleSpeedMetersPerSecond { get; }

        public float SuspensionImpact01 { get; }
        public bool DamageAvailable { get; }
        public float Damage01 { get; }
        public bool InteriorContextAvailable { get; }
        public float InteriorBlend01 { get; }
        public bool OpeningsContextAvailable { get; }
        public float DoorOpenness01 { get; }
        public float WindowOpenness01 { get; }

        public static VehicleAudioParameters Silent => new VehicleAudioParameters(
            VehicleAudioEngineState.Off,
            0f,
            1f,
            0f,
            0f,
            0,
            0f,
            0f,
            0f,
            VehicleAudioSurface.Unknown,
            0f,
            0f);

        private static float Clamp01(float value)
        {
            value = FiniteOrZero(value);
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static float NonNegativeFinite(float value)
        {
            return Math.Max(0f, FiniteOrZero(value));
        }

        private static float FiniteOrZero(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }
    }

    /// <summary>
    /// Typed vehicle-audio boundary shared by the Unity fallback and a future
    /// official Wwise adapter.
    /// </summary>
    public interface IVehicleAudioBackend : IAudioBackend
    {
        void SetVehicleParameters(in VehicleAudioParameters parameters);

        void PostVehicleEvent(VehicleAudioEvent audioEvent);
    }
}
