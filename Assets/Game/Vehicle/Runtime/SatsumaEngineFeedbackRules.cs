using MSC.Vehicle.Simulation;
using UnityEngine;

namespace MSC.Vehicle
{
    public readonly struct SatsumaEngineFeedbackMix
    {
        public readonly bool Running;
        public readonly bool Cranking;
        public readonly bool EngineLoopsActive;
        public readonly float ThrottleGain;
        public readonly float ThrottlePitch;
        public readonly float CoastGain;
        public readonly float CoastPitch;

        public SatsumaEngineFeedbackMix(bool running, bool cranking, float rpm, float throttle,
            bool allowRundown = false, float throttleVolume = .6f, float coastVolume = 1f)
        {
            Running = running;
            Cranking = cranking;
            // Locked donor Drivetrain112082 maxRPM8000 + SoundController112089.
            float normalized = float.IsFinite(rpm) ? Mathf.Clamp(rpm / 8000f, 0f, 2f) : 0f;
            float pedal = float.IsFinite(throttle) ? Mathf.Clamp01(throttle) : 0f;
            // SoundController follows rotation, not the ignition switch. Native
            // simulation still has real RPM while Off/Stalled winds down.
            EngineLoopsActive = running || allowRundown && normalized > 0f;
            ThrottleGain = EngineLoopsActive ? Mathf.Clamp01((pedal + 0.2f) * throttleVolume * normalized) : 0f;
            CoastGain = EngineLoopsActive ? Mathf.Clamp01((1f - pedal) * coastVolume * normalized) : 0f;
            ThrottlePitch = 0.5f + 1.25f * normalized;
            CoastPitch = 0.5f + 0.7f * normalized;
        }
    }

    public static class SatsumaEngineFeedbackRules
    {
        // MotorShake111116 half-period=(10000-rpm)/200000 seconds.
        // This only transfers its frequency, not the donor AddTorque simulation.
        public static float VibrationFrequencyHz(float rpm) => float.IsFinite(rpm) && rpm > 0f
            ? 100000f / (10000f - Mathf.Clamp(rpm, 0f, 8000f)) : 0f;

        public static SatsumaEngineFeedbackMix Evaluate(VehicleEngineStatus status, float rpm, float throttle,
            bool combustionRundownActive = true) =>
            new(status == VehicleEngineStatus.Running, status == VehicleEngineStatus.Cranking, rpm, throttle,
                combustionRundownActive && (status == VehicleEngineStatus.Off || status == VehicleEngineStatus.Stalled));

        public static SatsumaEngineFeedbackMix Evaluate(VehicleEngineStatus status, float rpm,
            float throttle, SatsumaExhaustOutlet outlet, bool combustionRundownActive = true) =>
            new(status == VehicleEngineStatus.Running, status == VehicleEngineStatus.Cranking, rpm, throttle,
                combustionRundownActive && (status == VehicleEngineStatus.Off || status == VehicleEngineStatus.Stalled),
                SatsumaExhaustFeedbackRules.ThrottleVolume(outlet), SatsumaExhaustFeedbackRules.CoastVolume(outlet));

        /// <summary>
        /// Bounded presentation approximation, not a donor force algorithm.
        /// Cyclic engine excitation follows real RPM; never writes a rigidbody.
        /// </summary>
        public static Vector3 VibrationOffset(float phaseRadians, float rpm, float load, bool running)
        {
            if (!running || !float.IsFinite(rpm) || rpm <= 0f || !float.IsFinite(phaseRadians)) return Vector3.zero;
            float safeLoad = float.IsFinite(load) ? Mathf.Clamp01(load) : 0f;
            float amplitude = Mathf.Lerp(0.0008f, 0.00035f, Mathf.Clamp01((rpm - 800f) / 5000f)) *
                (1f + safeLoad * 0.3f);
            return new Vector3(Mathf.Sin(phaseRadians), Mathf.Sin(phaseRadians * 2f) * 0.35f,
                Mathf.Cos(phaseRadians) * 0.2f) * amplitude;
        }
    }
}
