using System;
using UnityEngine;

namespace MSC.Vehicle
{
    public enum SatsumaWiperMode
    {
        Off = 0,
        Slow = 1,
        Fast = 2,
    }

    [Serializable]
    public sealed class SatsumaWiperSaveDto
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public int mode;
        public bool cycleActive;
        public float cycleTimeSeconds;

        public bool TryValidate(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion ||
                mode < (int)SatsumaWiperMode.Off ||
                mode > (int)SatsumaWiperMode.Fast ||
                !float.IsFinite(cycleTimeSeconds) ||
                cycleTimeSeconds < 0f || cycleTimeSeconds > 1f)
            {
                failure = "Satsuma wiper save payload is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    /// <summary>
    /// Reimplementation of the fixed donor Satsuma wipers. A sweep already in
    /// progress always returns to park; electrical power is checked only before
    /// another cycle begins.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [DisallowMultipleComponent]
    public sealed class SatsumaWiperController : MonoBehaviour
    {
        public const float HalfSweepSeconds = 0.5f;
        public const float FullSweepSeconds = 1f;
        public const float SlowDelaySeconds = 3f;
        public const float DonorSweepDegrees = 86f;

        [SerializeField] private SatsumaElectricalSystem electricalSystem;
        [SerializeField] private Transform leftTap;
        [SerializeField] private Transform leftRod;
        [SerializeField] private Transform rightTap;
        [SerializeField] private Transform rightRod;
        [SerializeField] private Transform switchKnob;
        [SerializeField] private Quaternion switchKnobBaseLocalRotation = Quaternion.identity;
        [SerializeField] private bool hasSwitchKnobBaseLocalRotation;
        [SerializeField] private SatsumaWiperMode mode;
        [SerializeField] private bool cycleActive;
        [SerializeField, Range(0f, FullSweepSeconds)]
        private float cycleTimeSeconds;
        [SerializeField, Min(0f)] private float delayRemainingSeconds;

        private Quaternion leftTapBase = Quaternion.identity;
        private Quaternion rightTapBase = Quaternion.identity;

        public SatsumaWiperMode Mode => mode;
        public bool IsCycleActive => cycleActive;
        public float CycleTimeSeconds => cycleTimeSeconds;
        public Transform SwitchKnob => switchKnob;
        public Quaternion SwitchKnobBaseLocalRotation => switchKnobBaseLocalRotation;
        public bool HasSwitchKnobBaseLocalRotation => hasSwitchKnobBaseLocalRotation;
        public bool CanOperateSwitch =>
            electricalSystem != null &&
            electricalSystem.DashboardControlsAvailable;
        public float Sweep01 => cycleTimeSeconds <= HalfSweepSeconds
            ? Mathf.Clamp01(cycleTimeSeconds / HalfSweepSeconds)
            : Mathf.Clamp01(
                (FullSweepSeconds - cycleTimeSeconds) / HalfSweepSeconds);

        public void Configure(
            SatsumaElectricalSystem configuredElectricalSystem,
            Transform configuredLeftTap,
            Transform configuredLeftRod,
            Transform configuredRightTap,
            Transform configuredRightRod,
            Transform configuredSwitchKnob)
        {
            electricalSystem = configuredElectricalSystem != null
                ? configuredElectricalSystem
                : throw new ArgumentNullException(nameof(configuredElectricalSystem));
            leftTap = RequireTransform(configuredLeftTap, nameof(configuredLeftTap));
            leftRod = RequireTransform(configuredLeftRod, nameof(configuredLeftRod));
            rightTap = RequireTransform(configuredRightTap, nameof(configuredRightTap));
            rightRod = RequireTransform(configuredRightRod, nameof(configuredRightRod));
            switchKnob = RequireTransform(
                configuredSwitchKnob,
                nameof(configuredSwitchKnob));
            switchKnobBaseLocalRotation = switchKnob.localRotation;
            hasSwitchKnobBaseLocalRotation = true;
            leftTapBase = leftTap.localRotation;
            rightTapBase = rightTap.localRotation;
            ResetState();
        }

        // Explicit authoring repair for already flattened donor presentation.
        // Persist the rest basis rather than recapturing the current mode after reload.
        public void ConfigureSwitchKnobRestRotation(Quaternion restRotation)
        {
            switchKnobBaseLocalRotation = restRotation.normalized;
            hasSwitchKnobBaseLocalRotation = true;
            ApplySwitchPresentation();
        }

        private void Awake()
        {
            if (!hasSwitchKnobBaseLocalRotation && switchKnob != null)
            {
                switchKnobBaseLocalRotation = switchKnob.localRotation *
                    Quaternion.AngleAxis(45f * (int)mode, Vector3.up);
                hasSwitchKnobBaseLocalRotation = true;
            }
        }

        public void CycleMode()
        {
            mode = mode switch
            {
                SatsumaWiperMode.Off => SatsumaWiperMode.Slow,
                SatsumaWiperMode.Slow => SatsumaWiperMode.Fast,
                _ => SatsumaWiperMode.Off,
            };
            if (mode == SatsumaWiperMode.Fast)
            {
                delayRemainingSeconds = 0f;
            }

            ApplyPresentation();
        }

        public void Simulate(float deltaSeconds)
        {
            if (!float.IsFinite(deltaSeconds) || deltaSeconds <= 0f)
            {
                return;
            }

            if (cycleActive)
            {
                cycleTimeSeconds = Mathf.Min(
                    FullSweepSeconds,
                    cycleTimeSeconds + deltaSeconds);
                if (cycleTimeSeconds >= FullSweepSeconds)
                {
                    cycleTimeSeconds = 0f;
                    cycleActive = false;
                    delayRemainingSeconds = mode == SatsumaWiperMode.Slow
                        ? SlowDelaySeconds
                        : 0f;
                }
            }
            else if (delayRemainingSeconds > 0f)
            {
                delayRemainingSeconds = Mathf.Max(
                    0f,
                    delayRemainingSeconds - deltaSeconds);
            }
            else if (mode != SatsumaWiperMode.Off &&
                     electricalSystem != null &&
                     electricalSystem.WipersPowered)
            {
                cycleActive = true;
                cycleTimeSeconds = Mathf.Min(deltaSeconds, FullSweepSeconds);
            }

            ApplyPresentation();
        }

        public SatsumaWiperSaveDto CaptureSaveData() => new()
        {
            mode = (int)mode,
            cycleActive = cycleActive,
            cycleTimeSeconds = cycleTimeSeconds,
        };

        public bool TryRestore(SatsumaWiperSaveDto dto, out string failure)
        {
            if (dto == null)
            {
                ResetState();
                failure = string.Empty;
                return true;
            }

            if (!dto.TryValidate(out failure))
            {
                return false;
            }

            mode = (SatsumaWiperMode)dto.mode;
            cycleActive = dto.cycleActive;
            cycleTimeSeconds = dto.cycleActive
                ? dto.cycleTimeSeconds
                : 0f;
            delayRemainingSeconds = 0f;
            ApplyPresentation();
            failure = string.Empty;
            return true;
        }

        public void ResetState()
        {
            mode = SatsumaWiperMode.Off;
            cycleActive = false;
            cycleTimeSeconds = 0f;
            delayRemainingSeconds = 0f;
            ApplyPresentation();
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                Simulate(Time.deltaTime);
            }
        }

        private void ApplyPresentation()
        {
            float sweep = Sweep01;
            if (leftTap != null)
            {
                leftTap.localRotation = leftTapBase *
                    Quaternion.AngleAxis(DonorSweepDegrees * sweep, Vector3.forward);
            }

            if (rightTap != null)
            {
                rightTap.localRotation = rightTapBase *
                    Quaternion.AngleAxis(DonorSweepDegrees * sweep, Vector3.forward);
            }

            if (leftRod != null)
            {
                leftRod.localRotation = Quaternion.Euler(
                    0f,
                    EvaluateRodAngle(
                        cycleTimeSeconds,
                        10f,
                        1f,
                        -3.5f,
                        1f,
                        10f),
                    0f);
            }

            if (rightRod != null)
            {
                rightRod.localRotation = Quaternion.Euler(
                    0f,
                    EvaluateRodAngle(
                        cycleTimeSeconds,
                        -3f,
                        -1.5f,
                        -3.5f,
                        -1.5f,
                        -3f),
                    0f);
            }

            ApplySwitchPresentation();
        }

        private void ApplySwitchPresentation()
        {
            if (switchKnob != null)
            {
                switchKnob.localRotation = switchKnobBaseLocalRotation *
                    Quaternion.AngleAxis(-45f * (int)mode, Vector3.up);
            }
        }

        private static float EvaluateRodAngle(
            float time,
            float atZero,
            float atQuarter,
            float atHalf,
            float atThreeQuarters,
            float atOne)
        {
            float clamped = Mathf.Clamp01(time);
            if (clamped <= 0.25f)
            {
                return Mathf.Lerp(atZero, atQuarter, clamped * 4f);
            }

            if (clamped <= 0.5f)
            {
                return Mathf.Lerp(atQuarter, atHalf, (clamped - 0.25f) * 4f);
            }

            if (clamped <= 0.75f)
            {
                return Mathf.Lerp(
                    atHalf,
                    atThreeQuarters,
                    (clamped - 0.5f) * 4f);
            }

            return Mathf.Lerp(
                atThreeQuarters,
                atOne,
                (clamped - 0.75f) * 4f);
        }

        private static Transform RequireTransform(
            Transform value,
            string parameterName) => value != null
                ? value
                : throw new ArgumentNullException(parameterName);
    }
}
