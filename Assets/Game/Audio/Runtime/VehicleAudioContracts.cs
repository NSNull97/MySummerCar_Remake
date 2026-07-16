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
        string FailureReason { get; }

        void SetVehicleParameters(in VehicleAudioParameters parameters);

        void PostVehicleEvent(VehicleAudioEvent audioEvent);
    }
}
