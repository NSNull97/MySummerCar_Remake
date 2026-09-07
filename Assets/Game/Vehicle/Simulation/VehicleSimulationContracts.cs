using System;
using UnityEngine;

namespace MSC.Vehicle.Simulation
{
    public enum VehicleSurfaceType
    {
        Unknown = 0,
        Paved = 1,
        Gravel = 2,
        Dirt = 3,
        Grass = 4,
        MudWet = 5
    }

    public enum VehicleEngineStatus
    {
        Off = 0,
        Cranking = 1,
        Running = 2,
        Stalled = 3
    }

    public enum VehicleShiftStatus
    {
        Stable = 0,
        AcceptedThisTick = 1,
        RejectedThisTick = 2
    }

    [Flags]
    public enum VehicleSimulationPrerequisiteFailure
    {
        None = 0,
        PrerequisiteSourceUnavailable = 1 << 0,
        EngineAssemblyMissing = 1 << 1,
        StarterMissing = 1 << 2,
        BatteryMissing = 1 << 3,
        FuelUnavailable = 1 << 4,
        DrivetrainMissing = 1 << 5,
        DrivenWheelsMissing = 1 << 6,
        DrivenWheelsUnsecured = 1 << 7,
        IgnitionOff = 1 << 8,
        InvalidSelectedGear = 1 << 9,
        InvalidClutchState = 1 << 10,
        BatteryVoltageLow = 1 << 11,
        OilUnavailable = 1 << 12,
        CoolantUnavailable = 1 << 13,
        NeutralSelected = 1 << 14,
        ClutchDisengaged = 1 << 15,
        CombustionUnavailable = 1 << 16,
        AlternatorUnavailable = 1 << 17
    }

    [Serializable]
    public readonly struct VehicleInputState
    {
        public VehicleInputState(
            float throttle01,
            float clutchPedal01,
            float brake01,
            float steeringMinusOneToOne,
            bool ignitionOn,
            bool starterRequested,
            bool gearChangeRequested,
            int requestedGear)
        {
            Throttle01 = Mathf.Clamp01(throttle01);
            ClutchPedal01 = Mathf.Clamp01(clutchPedal01);
            Brake01 = Mathf.Clamp01(brake01);
            SteeringMinusOneToOne = Mathf.Clamp(steeringMinusOneToOne, -1f, 1f);
            IgnitionOn = ignitionOn;
            StarterRequested = starterRequested;
            GearChangeRequested = gearChangeRequested;
            RequestedGear = requestedGear;
        }

        public float Throttle01 { get; }
        public float ClutchPedal01 { get; }
        public float Brake01 { get; }
        public float SteeringMinusOneToOne { get; }
        public bool IgnitionOn { get; }
        public bool StarterRequested { get; }
        public bool GearChangeRequested { get; }
        public int RequestedGear { get; }

        public bool IsFinite =>
            VehicleSimulationMath.IsFinite(Throttle01) &&
            VehicleSimulationMath.IsFinite(ClutchPedal01) &&
            VehicleSimulationMath.IsFinite(Brake01) &&
            VehicleSimulationMath.IsFinite(SteeringMinusOneToOne);

        public static VehicleInputState Neutral(bool ignitionOn = false)
        {
            return new VehicleInputState(0f, 1f, 0f, 0f, ignitionOn, false, false, 0);
        }
    }

    [Serializable]
    public struct VehicleSimulationPrerequisites
    {
        [SerializeField]
        private VehicleSimulationPrerequisiteFailure failureFlags;
        private bool independentCrankingRequirements;
        private bool satsumaOperatingRequirements;
        private bool satsumaIgnitionPowered;

        public VehicleSimulationPrerequisiteFailure FailureFlags => failureFlags;
        public bool IsReady => failureFlags == VehicleSimulationPrerequisiteFailure.None;

        public bool CanCrank => !HasAny(
            VehicleSimulationPrerequisiteFailure.PrerequisiteSourceUnavailable |
            VehicleSimulationPrerequisiteFailure.EngineAssemblyMissing |
            VehicleSimulationPrerequisiteFailure.StarterMissing |
            VehicleSimulationPrerequisiteFailure.BatteryMissing |
            VehicleSimulationPrerequisiteFailure.IgnitionOff |
            VehicleSimulationPrerequisiteFailure.BatteryVoltageLow) &&
            (independentCrankingRequirements || !HasAny(
                VehicleSimulationPrerequisiteFailure.FuelUnavailable |
                VehicleSimulationPrerequisiteFailure.OilUnavailable |
                VehicleSimulationPrerequisiteFailure.CoolantUnavailable));

        // Opt-in for the donor-evidenced Satsuma starter. Existing prototype
        // sources retain their original contract unless they explicitly opt in.
        public void UseIndependentCrankingRequirements() => independentCrankingRequirements = true;

        public bool CanRun => !HasAny(
            VehicleSimulationPrerequisiteFailure.PrerequisiteSourceUnavailable |
            VehicleSimulationPrerequisiteFailure.EngineAssemblyMissing |
            VehicleSimulationPrerequisiteFailure.CombustionUnavailable |
            VehicleSimulationPrerequisiteFailure.FuelUnavailable |
            VehicleSimulationPrerequisiteFailure.IgnitionOff) &&
            (satsumaOperatingRequirements ? satsumaIgnitionPowered : !HasAny(
                VehicleSimulationPrerequisiteFailure.BatteryMissing |
                VehicleSimulationPrerequisiteFailure.OilUnavailable |
                VehicleSimulationPrerequisiteFailure.CoolantUnavailable));

        // Diagnostic missing-fluid flags remain visible. In the opt-in model
        // lubrication/cooling have physical consequences, not a starter lock.
        public void UseSatsumaOperatingRequirements(bool ignitionPowered)
        { satsumaOperatingRequirements = true; satsumaIgnitionPowered = ignitionPowered; }

        public bool CanTransmitDrive => CanRun && !HasAny(
            VehicleSimulationPrerequisiteFailure.DrivetrainMissing |
            VehicleSimulationPrerequisiteFailure.DrivenWheelsMissing |
            VehicleSimulationPrerequisiteFailure.DrivenWheelsUnsecured |
            VehicleSimulationPrerequisiteFailure.InvalidSelectedGear |
            VehicleSimulationPrerequisiteFailure.InvalidClutchState |
            VehicleSimulationPrerequisiteFailure.NeutralSelected) &&
            (satsumaOperatingRequirements || !HasAny(VehicleSimulationPrerequisiteFailure.ClutchDisengaged));

        public void Reset()
        {
            failureFlags = VehicleSimulationPrerequisiteFailure.None;
            independentCrankingRequirements = false;
            satsumaOperatingRequirements = false;
            satsumaIgnitionPowered = false;
        }

        public void Add(VehicleSimulationPrerequisiteFailure failures)
        {
            failureFlags |= failures;
        }

        public bool HasAny(VehicleSimulationPrerequisiteFailure failures)
        {
            return (failureFlags & failures) != 0;
        }
    }

    public interface IVehicleSimulationPrerequisiteSource
    {
        void Evaluate(in VehicleInputState input, ref VehicleSimulationPrerequisites result);
    }

    /// <summary>Explicit session-only test override, not a saved fluid refill.</summary>
    public interface IVehicleFluidReadinessTestOverride
    {
        bool IgnoreFluidReadinessForTesting { get; }
    }

    public interface IVehicleFuelReadinessTestOverride
    {
        bool IgnoreFuelReadinessForTesting { get; }
    }

    public readonly struct WheelPhysicsSample
    {
        public WheelPhysicsSample(
            bool hasContact,
            Vector3 contactPoint,
            Vector3 contactNormal,
            float normalLoadNewtons,
            float longitudinalSlip,
            float lateralSlip,
            float angularSpeedRadiansPerSecond,
            float suspensionCompression01,
            VehicleSurfaceType surface)
        {
            HasContact = hasContact;
            ContactPoint = contactPoint;
            ContactNormal = contactNormal.sqrMagnitude > 0.000001f
                ? contactNormal.normalized
                : Vector3.up;
            NormalLoadNewtons = Mathf.Max(0f, normalLoadNewtons);
            LongitudinalSlip = longitudinalSlip;
            LateralSlip = lateralSlip;
            AngularSpeedRadiansPerSecond = angularSpeedRadiansPerSecond;
            SuspensionCompression01 = Mathf.Clamp01(suspensionCompression01);
            Surface = surface;
        }

        public bool HasContact { get; }
        public Vector3 ContactPoint { get; }
        public Vector3 ContactNormal { get; }
        public float NormalLoadNewtons { get; }
        public float LongitudinalSlip { get; }
        public float LateralSlip { get; }
        public float AngularSpeedRadiansPerSecond { get; }
        public float SuspensionCompression01 { get; }
        public VehicleSurfaceType Surface { get; }

        public bool IsFinite =>
            VehicleSimulationMath.IsFinite(ContactPoint) &&
            VehicleSimulationMath.IsFinite(ContactNormal) &&
            VehicleSimulationMath.IsFinite(NormalLoadNewtons) &&
            VehicleSimulationMath.IsFinite(LongitudinalSlip) &&
            VehicleSimulationMath.IsFinite(LateralSlip) &&
            VehicleSimulationMath.IsFinite(AngularSpeedRadiansPerSecond) &&
            VehicleSimulationMath.IsFinite(SuspensionCompression01);

        public static WheelPhysicsSample NoContact => new WheelPhysicsSample(
            false,
            Vector3.zero,
            Vector3.up,
            0f,
            0f,
            0f,
            0f,
            0f,
            VehicleSurfaceType.Unknown);
    }

    public readonly struct WheelPhysicsCommand
    {
        public WheelPhysicsCommand(
            float driveTorqueNewtonMeters,
            float brakeTorqueNewtonMeters,
            float steeringAngleDegrees)
        {
            DriveTorqueNewtonMeters = VehicleSimulationMath.Sanitize(driveTorqueNewtonMeters);
            BrakeTorqueNewtonMeters = Mathf.Max(
                0f,
                VehicleSimulationMath.Sanitize(brakeTorqueNewtonMeters));
            SteeringAngleDegrees = VehicleSimulationMath.Sanitize(steeringAngleDegrees);
        }

        public float DriveTorqueNewtonMeters { get; }
        public float BrakeTorqueNewtonMeters { get; }
        public float SteeringAngleDegrees { get; }

        public static WheelPhysicsCommand Zero => new WheelPhysicsCommand(0f, 0f, 0f);
    }

    public interface IWheelPhysicsBackend
    {
        int WheelCount { get; }

        float VehicleSpeedMetersPerSecond { get; }

        void Sample(float fixedDeltaSeconds, WheelPhysicsSample[] destination);

        void Apply(float fixedDeltaSeconds, WheelPhysicsCommand[] commands);

        void Reset();
    }

    [Serializable]
    public struct VehicleWheelState
    {
        public bool HasContact;
        public float NormalLoadNewtons;
        public float LongitudinalSlip;
        public float LateralSlip;
        public float AngularSpeedRadiansPerSecond;
        public float SuspensionCompression01;
        public float SuspensionForceNewtons;
        public VehicleSurfaceType Surface;

        public bool IsFinite =>
            VehicleSimulationMath.IsFinite(NormalLoadNewtons) &&
            VehicleSimulationMath.IsFinite(LongitudinalSlip) &&
            VehicleSimulationMath.IsFinite(LateralSlip) &&
            VehicleSimulationMath.IsFinite(AngularSpeedRadiansPerSecond) &&
            VehicleSimulationMath.IsFinite(SuspensionCompression01) &&
            VehicleSimulationMath.IsFinite(SuspensionForceNewtons);
    }

    [Serializable]
    public sealed class VehicleSimulationStateDto
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public VehicleEngineStatus engineStatus;
        // Optional schema-1 extension distinguishes real combustion rundown
        // from a failed starter attempt, including unloaded-cell restore.
        public bool hasCombustionHistory;
        public bool combustionRundownActive;
        public bool hasSatsumaOperatingState;
        public SatsumaOperatingSaveDto satsumaOperatingState;
        public float elapsedSeconds;
        public float engineRpm;
        public float filteredThrottle01;
        public float engineTorqueNewtonMeters;
        public float engineLoad01;
        public float stallTimerSeconds;
        public int selectedGear;
        public int invalidShiftCount;
        public VehicleShiftStatus shiftStatus;
        public float clutchEngagement01;
        public float clutchSlipRpm;
        public float clutchTransferredTorqueNewtonMeters;
        public float clutchTemperatureCelsius;
        public float gearboxInputRadiansPerSecond;
        public float gearboxOutputRadiansPerSecond;
        public float differentialTorqueNewtonMeters;
        public float vehicleSpeedMetersPerSecond;
        public float steeringAngleDegrees;
        public float brakeTorqueNewtonMeters;
        public float batteryCharge01;
        public float batteryVoltage;
        public float fuelLiters;
        public float oilLiters;
        public float coolantLiters;
        public float engineTemperatureCelsius;
        public float coolantTemperatureCelsius;
        public VehicleWheelState[] wheels = Array.Empty<VehicleWheelState>();
    }

    [Serializable]
    public sealed class VehicleSimulationState
    {
        [SerializeField]
        private VehicleEngineStatus engineStatus;
        [SerializeField] private bool combustionRundownActive;
        [SerializeField]
        private float elapsedSeconds;
        [SerializeField]
        private float engineRpm;
        [SerializeField]
        private float filteredThrottle01;
        [SerializeField]
        private float engineTorqueNewtonMeters;
        [SerializeField]
        private float engineLoad01;
        [SerializeField]
        private float stallTimerSeconds;
        [SerializeField]
        private int selectedGear;
        [SerializeField]
        private int invalidShiftCount;
        [SerializeField]
        private VehicleShiftStatus shiftStatus;
        [SerializeField]
        private float clutchEngagement01;
        [SerializeField]
        private float clutchSlipRpm;
        [SerializeField]
        private float clutchTransferredTorqueNewtonMeters;
        [SerializeField]
        private float clutchTemperatureCelsius;
        [SerializeField]
        private float gearboxInputRadiansPerSecond;
        [SerializeField]
        private float gearboxOutputRadiansPerSecond;
        [SerializeField]
        private float differentialTorqueNewtonMeters;
        [SerializeField]
        private float vehicleSpeedMetersPerSecond;
        [SerializeField]
        private float steeringAngleDegrees;
        [SerializeField]
        private float brakeTorqueNewtonMeters;
        [SerializeField]
        private float batteryCharge01;
        [SerializeField]
        private float batteryVoltage;
        [SerializeField]
        private float fuelLiters;
        [SerializeField]
        private float oilLiters;
        [SerializeField]
        private float coolantLiters;
        [SerializeField]
        private float engineTemperatureCelsius;
        [SerializeField]
        private float coolantTemperatureCelsius;
        [SerializeField]
        private VehicleWheelState[] wheels = Array.Empty<VehicleWheelState>();

        public VehicleEngineStatus EngineStatus { get => engineStatus; internal set => engineStatus = value; }
        public bool CombustionRundownActive { get => combustionRundownActive; internal set => combustionRundownActive = value; }
        public float ElapsedSeconds { get => elapsedSeconds; internal set => elapsedSeconds = value; }
        public float EngineRpm { get => engineRpm; internal set => engineRpm = value; }
        public float FilteredThrottle01 { get => filteredThrottle01; internal set => filteredThrottle01 = value; }
        public float EngineTorqueNewtonMeters { get => engineTorqueNewtonMeters; internal set => engineTorqueNewtonMeters = value; }
        public float EngineLoad01 { get => engineLoad01; internal set => engineLoad01 = value; }
        public float StallTimerSeconds { get => stallTimerSeconds; internal set => stallTimerSeconds = value; }
        public int SelectedGear { get => selectedGear; internal set => selectedGear = value; }
        public int InvalidShiftCount { get => invalidShiftCount; internal set => invalidShiftCount = value; }
        public VehicleShiftStatus ShiftStatus { get => shiftStatus; internal set => shiftStatus = value; }
        public float ClutchEngagement01 { get => clutchEngagement01; internal set => clutchEngagement01 = value; }
        public float ClutchSlipRpm { get => clutchSlipRpm; internal set => clutchSlipRpm = value; }
        public float ClutchTransferredTorqueNewtonMeters { get => clutchTransferredTorqueNewtonMeters; internal set => clutchTransferredTorqueNewtonMeters = value; }
        public float ClutchTemperatureCelsius { get => clutchTemperatureCelsius; internal set => clutchTemperatureCelsius = value; }
        public float GearboxInputRadiansPerSecond { get => gearboxInputRadiansPerSecond; internal set => gearboxInputRadiansPerSecond = value; }
        public float GearboxOutputRadiansPerSecond { get => gearboxOutputRadiansPerSecond; internal set => gearboxOutputRadiansPerSecond = value; }
        public float DifferentialTorqueNewtonMeters { get => differentialTorqueNewtonMeters; internal set => differentialTorqueNewtonMeters = value; }
        public float VehicleSpeedMetersPerSecond { get => vehicleSpeedMetersPerSecond; internal set => vehicleSpeedMetersPerSecond = value; }
        public float SteeringAngleDegrees { get => steeringAngleDegrees; internal set => steeringAngleDegrees = value; }
        public float BrakeTorqueNewtonMeters { get => brakeTorqueNewtonMeters; internal set => brakeTorqueNewtonMeters = value; }
        public float BatteryCharge01 { get => batteryCharge01; internal set => batteryCharge01 = value; }
        public float BatteryVoltage { get => batteryVoltage; internal set => batteryVoltage = value; }
        public float FuelLiters { get => fuelLiters; internal set => fuelLiters = value; }
        public float OilLiters { get => oilLiters; internal set => oilLiters = value; }
        public float CoolantLiters { get => coolantLiters; internal set => coolantLiters = value; }
        public float EngineTemperatureCelsius { get => engineTemperatureCelsius; internal set => engineTemperatureCelsius = value; }
        public float CoolantTemperatureCelsius { get => coolantTemperatureCelsius; internal set => coolantTemperatureCelsius = value; }
        public int WheelCount => wheels != null ? wheels.Length : 0;
        public SatsumaOperatingState SatsumaOperating { get; private set; }

        internal void EnableSatsumaOperatingState() =>
            SatsumaOperating ??= new SatsumaOperatingState();

        public float GetServiceFluidLiters(SatsumaServiceFluid fluid) => fluid switch
        {
            SatsumaServiceFluid.MotorOil => OilLiters,
            SatsumaServiceFluid.Coolant => CoolantLiters,
            SatsumaServiceFluid.BrakeFront => SatsumaOperating?.BrakeFrontLiters ?? 0f,
            SatsumaServiceFluid.BrakeRear => SatsumaOperating?.BrakeRearLiters ?? 0f,
            SatsumaServiceFluid.Clutch => SatsumaOperating?.ClutchLiters ?? 0f,
            _ => throw new ArgumentOutOfRangeException(nameof(fluid)),
        };

        // The receiver checks typed liquid identity, geometry, cap and hardware.
        // This final authority bounds the accepted amount; it never creates a container's contents.
        public float AddServiceFluid(SatsumaServiceFluid fluid, float offeredLiters)
        {
            if (SatsumaOperating == null || !float.IsFinite(offeredLiters) || offeredLiters <= 0f) return 0f;
            float before = GetServiceFluidLiters(fluid);
            float accepted = Mathf.Min(offeredLiters, Mathf.Max(0f, SatsumaServiceFluidRules.Capacity(fluid) - before));
            float after = before + accepted;
            switch (fluid)
            {
                case SatsumaServiceFluid.MotorOil:
                    SatsumaOperating.OilContaminationPercent *= after > 0f ? before / after : 0f;
                    OilLiters = after; break;
                case SatsumaServiceFluid.Coolant: CoolantLiters = after; break;
                case SatsumaServiceFluid.BrakeFront: SatsumaOperating.BrakeFrontLiters = after; break;
                case SatsumaServiceFluid.BrakeRear: SatsumaOperating.BrakeRearLiters = after; break;
                case SatsumaServiceFluid.Clutch: SatsumaOperating.ClutchLiters = after; break;
            }
            return accepted;
        }

        public VehicleWheelState GetWheelState(int index)
        {
            return wheels[index];
        }

        internal void SetWheelState(int index, in VehicleWheelState value)
        {
            wheels[index] = value;
        }

        public void Reset(VehicleSimulationConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            engineStatus = VehicleEngineStatus.Off;
            combustionRundownActive = false;
            SatsumaOperating = null;
            elapsedSeconds = 0f;
            engineRpm = 0f;
            filteredThrottle01 = 0f;
            engineTorqueNewtonMeters = 0f;
            engineLoad01 = 0f;
            stallTimerSeconds = 0f;
            selectedGear = 0;
            invalidShiftCount = 0;
            shiftStatus = VehicleShiftStatus.Stable;
            clutchEngagement01 = 0f;
            clutchSlipRpm = 0f;
            clutchTransferredTorqueNewtonMeters = 0f;
            clutchTemperatureCelsius = config.AmbientTemperatureCelsius;
            gearboxInputRadiansPerSecond = 0f;
            gearboxOutputRadiansPerSecond = 0f;
            differentialTorqueNewtonMeters = 0f;
            vehicleSpeedMetersPerSecond = 0f;
            steeringAngleDegrees = 0f;
            brakeTorqueNewtonMeters = 0f;
            batteryCharge01 = 1f;
            batteryVoltage = config.BatteryNominalVoltage;
            fuelLiters = config.InitialFuelLiters;
            oilLiters = config.InitialOilLiters;
            coolantLiters = config.InitialCoolantLiters;
            engineTemperatureCelsius = config.AmbientTemperatureCelsius;
            coolantTemperatureCelsius = config.AmbientTemperatureCelsius;
            if (wheels == null || wheels.Length != config.WheelCount)
            {
                wheels = new VehicleWheelState[config.WheelCount];
            }
            else
            {
                Array.Clear(wheels, 0, wheels.Length);
            }
        }

        public VehicleSimulationStateDto CaptureDto()
        {
            var dto = new VehicleSimulationStateDto
            {
                engineStatus = engineStatus,
                hasCombustionHistory = true,
                combustionRundownActive = combustionRundownActive,
                hasSatsumaOperatingState = SatsumaOperating != null,
                satsumaOperatingState = SatsumaOperating?.Capture(),
                elapsedSeconds = elapsedSeconds,
                engineRpm = engineRpm,
                filteredThrottle01 = filteredThrottle01,
                engineTorqueNewtonMeters = engineTorqueNewtonMeters,
                engineLoad01 = engineLoad01,
                stallTimerSeconds = stallTimerSeconds,
                selectedGear = selectedGear,
                invalidShiftCount = invalidShiftCount,
                shiftStatus = shiftStatus,
                clutchEngagement01 = clutchEngagement01,
                clutchSlipRpm = clutchSlipRpm,
                clutchTransferredTorqueNewtonMeters = clutchTransferredTorqueNewtonMeters,
                clutchTemperatureCelsius = clutchTemperatureCelsius,
                gearboxInputRadiansPerSecond = gearboxInputRadiansPerSecond,
                gearboxOutputRadiansPerSecond = gearboxOutputRadiansPerSecond,
                differentialTorqueNewtonMeters = differentialTorqueNewtonMeters,
                vehicleSpeedMetersPerSecond = vehicleSpeedMetersPerSecond,
                steeringAngleDegrees = steeringAngleDegrees,
                brakeTorqueNewtonMeters = brakeTorqueNewtonMeters,
                batteryCharge01 = batteryCharge01,
                batteryVoltage = batteryVoltage,
                fuelLiters = fuelLiters,
                oilLiters = oilLiters,
                coolantLiters = coolantLiters,
                engineTemperatureCelsius = engineTemperatureCelsius,
                coolantTemperatureCelsius = coolantTemperatureCelsius,
                wheels = wheels != null ? (VehicleWheelState[])wheels.Clone() : Array.Empty<VehicleWheelState>()
            };
            return dto;
        }

        public bool TryRestoreDto(VehicleSimulationStateDto dto, VehicleSimulationConfig config)
        {
            if (dto == null || config == null ||
                dto.schemaVersion != VehicleSimulationStateDto.CurrentSchemaVersion ||
                dto.wheels == null || dto.wheels.Length != config.WheelCount ||
                !IsDtoFinite(dto) ||
                dto.hasSatsumaOperatingState && (dto.satsumaOperatingState == null || !dto.satsumaOperatingState.IsValid) ||
                !Enum.IsDefined(typeof(VehicleEngineStatus), dto.engineStatus) ||
                !Enum.IsDefined(typeof(VehicleShiftStatus), dto.shiftStatus) ||
                !config.Gearbox.TryGetRatio(dto.selectedGear, out _) ||
                dto.elapsedSeconds < 0f || dto.engineRpm < 0f ||
                dto.batteryCharge01 < 0f || dto.batteryCharge01 > 1f ||
                dto.batteryVoltage < 0f || dto.fuelLiters < 0f ||
                dto.oilLiters < 0f || dto.coolantLiters < 0f)
            {
                return false;
            }

            engineStatus = dto.engineStatus;
            SatsumaOperating = dto.hasSatsumaOperatingState
                ? SatsumaOperatingState.Restore(dto.satsumaOperatingState) : null;
            // Old saves cannot disambiguate an Off/Stalled rotating engine.
            // Preserve their former rundown behavior once; new saves are exact.
            combustionRundownActive = dto.engineStatus == VehicleEngineStatus.Running ||
                (dto.hasCombustionHistory ? dto.combustionRundownActive :
                    dto.engineStatus != VehicleEngineStatus.Cranking && dto.engineRpm > 0f);
            elapsedSeconds = dto.elapsedSeconds;
            engineRpm = dto.engineRpm;
            filteredThrottle01 = dto.filteredThrottle01;
            engineTorqueNewtonMeters = dto.engineTorqueNewtonMeters;
            engineLoad01 = dto.engineLoad01;
            stallTimerSeconds = dto.stallTimerSeconds;
            selectedGear = dto.selectedGear;
            invalidShiftCount = dto.invalidShiftCount;
            shiftStatus = dto.shiftStatus;
            clutchEngagement01 = dto.clutchEngagement01;
            clutchSlipRpm = dto.clutchSlipRpm;
            clutchTransferredTorqueNewtonMeters = dto.clutchTransferredTorqueNewtonMeters;
            clutchTemperatureCelsius = dto.clutchTemperatureCelsius;
            gearboxInputRadiansPerSecond = dto.gearboxInputRadiansPerSecond;
            gearboxOutputRadiansPerSecond = dto.gearboxOutputRadiansPerSecond;
            differentialTorqueNewtonMeters = dto.differentialTorqueNewtonMeters;
            vehicleSpeedMetersPerSecond = dto.vehicleSpeedMetersPerSecond;
            steeringAngleDegrees = dto.steeringAngleDegrees;
            brakeTorqueNewtonMeters = dto.brakeTorqueNewtonMeters;
            batteryCharge01 = dto.batteryCharge01;
            batteryVoltage = dto.batteryVoltage;
            fuelLiters = dto.fuelLiters;
            oilLiters = dto.oilLiters;
            coolantLiters = dto.coolantLiters;
            engineTemperatureCelsius = dto.engineTemperatureCelsius;
            coolantTemperatureCelsius = dto.coolantTemperatureCelsius;
            wheels = (VehicleWheelState[])dto.wheels.Clone();
            return true;
        }

        public bool IsFinite()
        {
            if (SatsumaOperating != null && !SatsumaOperating.IsFinite) return false;
            if (!VehicleSimulationMath.IsFinite(elapsedSeconds) ||
                !VehicleSimulationMath.IsFinite(engineRpm) ||
                !VehicleSimulationMath.IsFinite(filteredThrottle01) ||
                !VehicleSimulationMath.IsFinite(engineTorqueNewtonMeters) ||
                !VehicleSimulationMath.IsFinite(engineLoad01) ||
                !VehicleSimulationMath.IsFinite(stallTimerSeconds) ||
                !VehicleSimulationMath.IsFinite(clutchEngagement01) ||
                !VehicleSimulationMath.IsFinite(clutchSlipRpm) ||
                !VehicleSimulationMath.IsFinite(clutchTransferredTorqueNewtonMeters) ||
                !VehicleSimulationMath.IsFinite(clutchTemperatureCelsius) ||
                !VehicleSimulationMath.IsFinite(gearboxInputRadiansPerSecond) ||
                !VehicleSimulationMath.IsFinite(gearboxOutputRadiansPerSecond) ||
                !VehicleSimulationMath.IsFinite(differentialTorqueNewtonMeters) ||
                !VehicleSimulationMath.IsFinite(vehicleSpeedMetersPerSecond) ||
                !VehicleSimulationMath.IsFinite(steeringAngleDegrees) ||
                !VehicleSimulationMath.IsFinite(brakeTorqueNewtonMeters) ||
                !VehicleSimulationMath.IsFinite(batteryCharge01) ||
                !VehicleSimulationMath.IsFinite(batteryVoltage) ||
                !VehicleSimulationMath.IsFinite(fuelLiters) ||
                !VehicleSimulationMath.IsFinite(oilLiters) ||
                !VehicleSimulationMath.IsFinite(coolantLiters) ||
                !VehicleSimulationMath.IsFinite(engineTemperatureCelsius) ||
                !VehicleSimulationMath.IsFinite(coolantTemperatureCelsius))
            {
                return false;
            }

            for (int i = 0; i < wheels.Length; i++)
            {
                if (!wheels[i].IsFinite)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsDtoFinite(VehicleSimulationStateDto dto)
        {
            if (!VehicleSimulationMath.IsFinite(dto.elapsedSeconds) ||
                !VehicleSimulationMath.IsFinite(dto.engineRpm) ||
                !VehicleSimulationMath.IsFinite(dto.filteredThrottle01) ||
                !VehicleSimulationMath.IsFinite(dto.engineTorqueNewtonMeters) ||
                !VehicleSimulationMath.IsFinite(dto.engineLoad01) ||
                !VehicleSimulationMath.IsFinite(dto.stallTimerSeconds) ||
                !VehicleSimulationMath.IsFinite(dto.clutchEngagement01) ||
                !VehicleSimulationMath.IsFinite(dto.clutchSlipRpm) ||
                !VehicleSimulationMath.IsFinite(dto.clutchTransferredTorqueNewtonMeters) ||
                !VehicleSimulationMath.IsFinite(dto.clutchTemperatureCelsius) ||
                !VehicleSimulationMath.IsFinite(dto.gearboxInputRadiansPerSecond) ||
                !VehicleSimulationMath.IsFinite(dto.gearboxOutputRadiansPerSecond) ||
                !VehicleSimulationMath.IsFinite(dto.differentialTorqueNewtonMeters) ||
                !VehicleSimulationMath.IsFinite(dto.vehicleSpeedMetersPerSecond) ||
                !VehicleSimulationMath.IsFinite(dto.steeringAngleDegrees) ||
                !VehicleSimulationMath.IsFinite(dto.brakeTorqueNewtonMeters) ||
                !VehicleSimulationMath.IsFinite(dto.batteryCharge01) ||
                !VehicleSimulationMath.IsFinite(dto.batteryVoltage) ||
                !VehicleSimulationMath.IsFinite(dto.fuelLiters) ||
                !VehicleSimulationMath.IsFinite(dto.oilLiters) ||
                !VehicleSimulationMath.IsFinite(dto.coolantLiters) ||
                !VehicleSimulationMath.IsFinite(dto.engineTemperatureCelsius) ||
                !VehicleSimulationMath.IsFinite(dto.coolantTemperatureCelsius))
            {
                return false;
            }

            for (int i = 0; i < dto.wheels.Length; i++)
            {
                if (!dto.wheels[i].IsFinite)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public readonly struct VehicleWheelTelemetry
    {
        public VehicleWheelTelemetry(in VehicleWheelState state)
        {
            HasContact = state.HasContact;
            NormalLoadNewtons = state.NormalLoadNewtons;
            LongitudinalSlip = state.LongitudinalSlip;
            LateralSlip = state.LateralSlip;
            AngularSpeedRadiansPerSecond = state.AngularSpeedRadiansPerSecond;
            SuspensionCompression01 = state.SuspensionCompression01;
            Surface = state.Surface;
        }

        public bool HasContact { get; }
        public float NormalLoadNewtons { get; }
        public float LongitudinalSlip { get; }
        public float LateralSlip { get; }
        public float AngularSpeedRadiansPerSecond { get; }
        public float SuspensionCompression01 { get; }
        public VehicleSurfaceType Surface { get; }
    }

    public sealed class VehicleTelemetry
    {
        private VehicleWheelTelemetry[] wheels = Array.Empty<VehicleWheelTelemetry>();

        public float EngineRpm { get; internal set; }
        public float Throttle01 { get; internal set; }
        public float EngineLoad01 { get; internal set; }
        public float EngineTorqueNewtonMeters { get; internal set; }
        public float ClutchSlipRpm { get; internal set; }
        public int SelectedGear { get; internal set; }
        public VehicleShiftStatus ShiftStatus { get; internal set; }
        public float GearboxInputRadiansPerSecond { get; internal set; }
        public float GearboxOutputRadiansPerSecond { get; internal set; }
        public float DifferentialTorqueNewtonMeters { get; internal set; }
        public float VehicleSpeedMetersPerSecond { get; internal set; }
        public float SteeringAngleDegrees { get; internal set; }
        public float Brake01 { get; internal set; }
        public float BatteryVoltage { get; internal set; }
        public float EngineTemperatureCelsius { get; internal set; }
        public VehicleSimulationPrerequisiteFailure PrerequisiteFailures { get; internal set; }
        public int SubstepCount { get; internal set; }
        public float FixedStepSeconds { get; internal set; }
        public double SimulationMilliseconds { get; internal set; }
        public double BackendSampleMilliseconds { get; internal set; }
        public double BackendApplyMilliseconds { get; internal set; }
        public int WheelCount => wheels.Length;

        public VehicleWheelTelemetry GetWheel(int index)
        {
            return wheels[index];
        }

        internal void ConfigureWheelCount(int wheelCount)
        {
            if (wheels.Length != wheelCount)
            {
                wheels = new VehicleWheelTelemetry[wheelCount];
            }
        }

        internal void SetWheel(int index, in VehicleWheelTelemetry value)
        {
            wheels[index] = value;
        }
    }

    public static class VehicleSimulationMath
    {
        public const float RadiansPerSecondToRpm = 60f / (2f * Mathf.PI);
        public const float RpmToRadiansPerSecond = 2f * Mathf.PI / 60f;

        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        public static float Sanitize(float value, float fallback = 0f)
        {
            return IsFinite(value) ? value : fallback;
        }
    }
}
