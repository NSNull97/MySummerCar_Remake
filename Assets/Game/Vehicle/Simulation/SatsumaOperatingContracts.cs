using System;
using UnityEngine;

namespace MSC.Vehicle.Simulation
{
    public enum SatsumaServiceFluid
    {
        MotorOil = 0, Coolant = 1, BrakeFront = 2, BrakeRear = 3, Clutch = 4,
    }

    public static class SatsumaServiceFluidRules
    {
        // Frozen CapTrigger_* records; these are litres, not donor normalized levels.
        public const float OilCapacityLiters = 3f;
        public const float CoolantCapacityLiters = 5.4f;
        public const float BrakeCapacityLiters = 1f;
        public const float ClutchCapacityLiters = 0.5f;

        public static float Capacity(SatsumaServiceFluid fluid) => fluid switch
        {
            SatsumaServiceFluid.MotorOil => OilCapacityLiters,
            SatsumaServiceFluid.Coolant => CoolantCapacityLiters,
            SatsumaServiceFluid.BrakeFront or SatsumaServiceFluid.BrakeRear => BrakeCapacityLiters,
            SatsumaServiceFluid.Clutch => ClutchCapacityLiters,
            _ => throw new ArgumentOutOfRangeException(nameof(fluid)),
        };

        public static float PourLitersPerSecond(SatsumaServiceFluid fluid) =>
            fluid == SatsumaServiceFluid.Coolant ? 0.2f : 0.1f;

        public static string LiquidId(SatsumaServiceFluid fluid) => fluid switch
        {
            SatsumaServiceFluid.MotorOil => "liquid.motor-oil",
            SatsumaServiceFluid.Coolant => "liquid.coolant",
            SatsumaServiceFluid.BrakeFront or SatsumaServiceFluid.BrakeRear or SatsumaServiceFluid.Clutch =>
                "liquid.brake-fluid",
            _ => throw new ArgumentOutOfRangeException(nameof(fluid)),
        };
    }

    [Serializable]
    public sealed class SatsumaOperatingSaveDto
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public float brakeFrontLiters;
        public float brakeRearLiters;
        public float clutchLiters;
        public float oilContaminationPercent;
        public float oilPressureBar;
        public float coolantPressurePsi;
        public float crankingSeconds;
        public bool radiatorFanRunning;
        // Optional schema-1 extension. Donor OdometerReading counts 10 km;
        // its sixth wheel is driven by the partial 0..10000 m accumulator.
        public bool hasOdometerState;
        public int odometerTenKilometerUnits;
        public double odometerPartialMeters;

        public bool IsValid => schemaVersion == CurrentSchemaVersion &&
            InRange(brakeFrontLiters, 0f, SatsumaServiceFluidRules.BrakeCapacityLiters) &&
            InRange(brakeRearLiters, 0f, SatsumaServiceFluidRules.BrakeCapacityLiters) &&
            InRange(clutchLiters, 0f, SatsumaServiceFluidRules.ClutchCapacityLiters) &&
            InRange(oilContaminationPercent, 0f, 100f) && InRange(oilPressureBar, 0f, 10f) &&
            InRange(coolantPressurePsi, 0f, 50f) && InRange(crankingSeconds, 0f, 60f) &&
            (!hasOdometerState || odometerTenKilometerUnits >= 10000 && odometerTenKilometerUnits <= 99999 &&
                double.IsFinite(odometerPartialMeters) && odometerPartialMeters >= 0d && odometerPartialMeters < 10000d);

        public SatsumaOperatingSaveDto Clone() => (SatsumaOperatingSaveDto)MemberwiseClone();
        private static bool InRange(float value, float min, float max) =>
            float.IsFinite(value) && value >= min && value <= max;
    }

    /// <summary>Explicit typed bridge; no Unity scene, donor hierarchy or item dependency in the solver.</summary>
    public interface ISatsumaOperatingConditionSource
    {
        SatsumaOperatingInputs CaptureConditions();
        void ApplyWear(in SatsumaWearDelta wear);
    }

    public readonly struct SatsumaTuningInputs
    {
        public SatsumaTuningInputs(float mixture, float choke, float sparkDegrees, float camDegrees,
            float alternatorAngle, Vector4 intakeValves, Vector4 exhaustValves)
        {
            MixtureSetting = mixture; Choke01 = choke; SparkDegrees = sparkDegrees;
            CamDegrees = camDegrees; AlternatorAngle = alternatorAngle;
            IntakeValves = intakeValves; ExhaustValves = exhaustValves;
        }
        public float MixtureSetting { get; }
        public float Choke01 { get; }
        public float SparkDegrees { get; }
        public float CamDegrees { get; }
        public float AlternatorAngle { get; }
        public Vector4 IntakeValves { get; }
        public Vector4 ExhaustValves { get; }
    }

    public readonly struct SatsumaAncillaryInputs
    {
        public SatsumaAncillaryInputs(bool batteryConnected, bool batteryIgnitionPowered,
            bool alternatorChargeWired, bool alternatorIgnitionWired, bool fanWired,
            bool beltUsable, bool waterPumpUsable, bool alternatorUsable)
        {
            BatteryConnected = batteryConnected; BatteryIgnitionPowered = batteryIgnitionPowered;
            AlternatorChargeWired = alternatorChargeWired; AlternatorIgnitionWired = alternatorIgnitionWired;
            FanWired = fanWired; BeltUsable = beltUsable; WaterPumpUsable = waterPumpUsable;
            AlternatorUsable = alternatorUsable;
        }
        public bool BatteryConnected { get; }
        public bool BatteryIgnitionPowered { get; }
        public bool AlternatorChargeWired { get; }
        public bool AlternatorIgnitionWired { get; }
        public bool FanWired { get; }
        public bool BeltUsable { get; }
        public bool WaterPumpUsable { get; }
        public bool AlternatorUsable { get; }
    }

    public readonly struct SatsumaFluidHardwareInputs
    {
        public SatsumaFluidHardwareInputs(bool oilPanInstalled, bool filterInstalled,
            float filterTightness, float panTightness, float coverTightness, bool headGasketHealthy,
            bool radiatorInstalled, bool hosesInstalled, float hoseTightness,
            bool radiatorCapClosed, bool oilCapClosed,
            bool frontHydraulicsConnected, bool rearHydraulicsConnected, bool clutchHydraulicsConnected,
            float brakeLineSeal01 = 1f, float clutchLineSeal01 = 1f, bool oilDrainOpen = false)
        {
            OilPanInstalled = oilPanInstalled; FilterInstalled = filterInstalled;
            FilterTightness = filterTightness; PanTightness = panTightness; CoverTightness = coverTightness;
            HeadGasketHealthy = headGasketHealthy; RadiatorInstalled = radiatorInstalled;
            HosesInstalled = hosesInstalled; HoseTightness = hoseTightness;
            RadiatorCapClosed = radiatorCapClosed; OilCapClosed = oilCapClosed;
            FrontHydraulicsConnected = frontHydraulicsConnected;
            RearHydraulicsConnected = rearHydraulicsConnected;
            ClutchHydraulicsConnected = clutchHydraulicsConnected;
            BrakeLineSeal01 = brakeLineSeal01; ClutchLineSeal01 = clutchLineSeal01;
            OilDrainOpen = oilDrainOpen;
        }
        public bool OilPanInstalled { get; }
        public bool FilterInstalled { get; }
        public float FilterTightness { get; }
        public float PanTightness { get; }
        public float CoverTightness { get; }
        public bool HeadGasketHealthy { get; }
        public bool RadiatorInstalled { get; }
        public bool HosesInstalled { get; }
        public float HoseTightness { get; }
        public bool RadiatorCapClosed { get; }
        public bool OilCapClosed { get; }
        public bool FrontHydraulicsConnected { get; }
        public bool RearHydraulicsConnected { get; }
        public bool ClutchHydraulicsConnected { get; }
        public float BrakeLineSeal01 { get; }
        public float ClutchLineSeal01 { get; }
        public bool OilDrainOpen { get; }
    }

    public readonly struct SatsumaOperatingInputs
    {
        public SatsumaOperatingInputs(in SatsumaTuningInputs tuning, in SatsumaAncillaryInputs ancillary,
            in SatsumaFluidHardwareInputs fluids, int firingCylinderMask, float crankshaftCondition,
            float ambientCelsius, bool debugAutocharge = false, float plugRoughness01 = 0f,
            float rockerShaftCondition = 100f)
        {
            Tuning = tuning; Ancillary = ancillary; Fluids = fluids; FiringCylinderMask = firingCylinderMask;
            CrankshaftCondition = crankshaftCondition; AmbientCelsius = ambientCelsius;
            DebugAutocharge = debugAutocharge;
            PlugRoughness01 = plugRoughness01; RockerShaftCondition = rockerShaftCondition;
        }
        public SatsumaTuningInputs Tuning { get; }
        public SatsumaAncillaryInputs Ancillary { get; }
        public SatsumaFluidHardwareInputs Fluids { get; }
        public int FiringCylinderMask { get; }
        public float CrankshaftCondition { get; }
        public float AmbientCelsius { get; }
        public bool DebugAutocharge { get; }
        public float PlugRoughness01 { get; }
        public float RockerShaftCondition { get; }
        public bool IsFinite => float.IsFinite(Tuning.MixtureSetting) && float.IsFinite(Tuning.Choke01) &&
            float.IsFinite(Tuning.SparkDegrees) && float.IsFinite(Tuning.CamDegrees) &&
            float.IsFinite(Tuning.AlternatorAngle) && Finite(Tuning.IntakeValves) && Finite(Tuning.ExhaustValves) &&
            float.IsFinite(CrankshaftCondition) && float.IsFinite(AmbientCelsius) &&
            float.IsFinite(Fluids.FilterTightness) && float.IsFinite(Fluids.PanTightness) &&
            float.IsFinite(Fluids.CoverTightness) && float.IsFinite(Fluids.HoseTightness) &&
            float.IsFinite(Fluids.BrakeLineSeal01) && float.IsFinite(Fluids.ClutchLineSeal01) &&
            float.IsFinite(PlugRoughness01) && float.IsFinite(RockerShaftCondition);
        private static bool Finite(Vector4 value) => float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z) && float.IsFinite(value.w);
    }

    public readonly struct SatsumaWearDelta
    {
        public SatsumaWearDelta(float piston, float crankshaft, float headGasket, float belt, float waterPump,
            float rockerShaft = 0f)
        { Piston = piston; Crankshaft = crankshaft; HeadGasket = headGasket; Belt = belt; WaterPump = waterPump; RockerShaft = rockerShaft; }
        public float Piston { get; }
        public float Crankshaft { get; }
        public float HeadGasket { get; }
        public float Belt { get; }
        public float WaterPump { get; }
        public float RockerShaft { get; }
    }

    public readonly struct SatsumaEngineOperatingPoint
    {
        public SatsumaEngineOperatingPoint(bool combustionAllowed, bool ignitionPowered,
            float idleRpm, float torqueScale, float extraFriction, float starterScale,
            float afr, float knock, float valveNoise, float intakeSpit, float exhaustBackfire,
            float beltSqueal, float beltDrive, bool alternatorGenerating,
            float frontBrakeEfficiency, float rearBrakeEfficiency, float clutchPedalEfficiency,
            float bearingKnock01 = 0f)
        {
            CombustionAllowed = combustionAllowed; IgnitionPowered = ignitionPowered;
            IdleRpm = idleRpm; TorqueScale = torqueScale; ExtraFriction = extraFriction;
            StarterScale = starterScale; AirFuelRatio = afr; Knock01 = knock; ValveNoise01 = valveNoise;
            IntakeSpit01 = intakeSpit; ExhaustBackfire01 = exhaustBackfire; BeltSqueal01 = beltSqueal;
            BeltDrive01 = beltDrive; AlternatorGenerating = alternatorGenerating;
            FrontBrakeEfficiency = frontBrakeEfficiency; RearBrakeEfficiency = rearBrakeEfficiency;
            ClutchPedalEfficiency = clutchPedalEfficiency;
            BearingKnock01 = bearingKnock01;
        }
        public bool CombustionAllowed { get; }
        public bool IgnitionPowered { get; }
        public float IdleRpm { get; }
        public float TorqueScale { get; }
        public float ExtraFriction { get; }
        public float StarterScale { get; }
        public float AirFuelRatio { get; }
        public float Knock01 { get; }
        public float ValveNoise01 { get; }
        public float IntakeSpit01 { get; }
        public float ExhaustBackfire01 { get; }
        public float BeltSqueal01 { get; }
        public float BeltDrive01 { get; }
        public bool AlternatorGenerating { get; }
        public float FrontBrakeEfficiency { get; }
        public float RearBrakeEfficiency { get; }
        public float ClutchPedalEfficiency { get; }
        public float BearingKnock01 { get; }
    }
}
