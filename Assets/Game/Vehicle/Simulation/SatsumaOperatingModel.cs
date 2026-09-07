using System;
using UnityEngine;

namespace MSC.Vehicle.Simulation
{
    /// <summary>
    /// Reimplemented stock Satsuma operating model. Frozen scalar rules are
    /// identified below; continuous heat/pressure, torque normalization and
    /// electrical integration are a modern solver calibration, not a donor FSM port.
    /// </summary>
    public sealed class SatsumaOperatingModel
    {
        public SatsumaEngineOperatingPoint LastPoint { get; private set; }
        public void Reset() => LastPoint = default;

        public SatsumaEngineOperatingPoint Evaluate(VehicleSimulationState state,
            in VehicleInputState input, in SatsumaOperatingInputs conditions, float deltaSeconds)
        {
            if (state?.SatsumaOperating == null || !(deltaSeconds > 0f) || !float.IsFinite(deltaSeconds))
                throw new ArgumentException("Satsuma operating state and a finite positive timestep are required.");
            if (!conditions.IsFinite) throw new ArgumentException("Satsuma operating inputs must be finite.", nameof(conditions));
            SatsumaOperatingState services = state.SatsumaOperating;
            SatsumaTuningInputs tuning = conditions.Tuning;
            SatsumaAncillaryInputs ancillary = conditions.Ancillary;
            SatsumaFluidHardwareInputs hardware = conditions.Fluids;

            // Mixture109336: the mean of the reviewed RandomFloat(throttle,1.8)
            // interval replaces global random state. Preserve the 22 multiplier cap.
            float choke = Mathf.Clamp01(tuning.Choke01);
            float density = Mathf.Clamp(0.36f + (state.EngineTemperatureCelsius + 50f) / 200f, 0.9f, 1.1f);
            float ratio = Mathf.Clamp(14f + (Mathf.Clamp01(input.Throttle01) + 1.8f) * 0.5f, 0f, 15f);
            float mixture = (ratio / density) /
                Mathf.Clamp(Mathf.Clamp(tuning.MixtureSetting, 10f, 22f) * (1f + choke), 0.01f, 22f);
            float afr = mixture * 14.7f;
            float coldDelay = Mathf.Clamp((100f - state.EngineTemperatureCelsius) / 20f, 0.5f, 8f);
            if (input.StarterRequested && input.IgnitionOn && state.EngineStatus != VehicleEngineStatus.Running &&
                ancillary.BatteryIgnitionPowered && state.EngineRpm > 100f)
                services.CrankingSeconds = Mathf.Min(60f, services.CrankingSeconds + deltaSeconds);
            else if (!input.StarterRequested || !input.IgnitionOn || !ancillary.BatteryIgnitionPowered)
                services.CrankingSeconds = 0f;

            float camError = Mathf.DeltaAngle(0f, tuning.CamDegrees);
            bool camOff = Mathf.Abs(camError) > 1f;
            int firingCount = CountBits(conditions.FiringCylinderMask & 15);
            float firingFraction = Mathf.Max(0, firingCount - (camOff ? 1 : 0)) * 0.25f;
            float torqueDecrease = afr < 12.7f ? (mixture - 0.5f) * 11f :
                afr < 14.05f || afr > 16f ? (mixture - 1f) * 30f : 0f;
            float powerDecrease = afr < 12.7f ? (mixture - 0.5f) * 11f :
                afr < 14.05f || afr > 16f ? (mixture - 1f) * 40f : 0f;
            float intakeSum = 0f, exhaustSum = 0f;
            int badIntake = 0, badExhaust = 0;
            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                float intake = tuning.IntakeValves[cylinder], exhaust = tuning.ExhaustValves[cylinder];
                if (intake >= 6f && intake <= 8f) intakeSum += intake; else badIntake++;
                if (exhaust >= 5f && exhaust <= 7f) exhaustSum += exhaust; else badExhaust++;
            }
            // Valves108555, normalized to existing healthy stock torque curve.
            float torqueScale = Mathf.Clamp((intakeSum - badExhaust * 3f) / 1.6f - torqueDecrease, 1f, 55f) / 17.5f;
            float powerScale = Mathf.Clamp(exhaustSum * 2.658f - powerDecrease, 10f, 350f) / (24f * 2.658f);
            torqueScale = Mathf.Lerp(torqueScale, powerScale, Mathf.InverseLerp(2000f, 6000f, state.EngineRpm));
            float sparkScale = tuning.SparkDegrees < 5f ? 0.3f : tuning.SparkDegrees > 15f ? 0.75f : tuning.SparkDegrees / 15f;
            torqueScale *= sparkScale * firingFraction * (1f - Mathf.Clamp01(conditions.PlugRoughness01) * .3f);

            // Rotation/Jumping110290: correct adjustment is 7, not the original
            // authoring default 2. Slack squeals and slips; over-tension adds wear.
            float beltSqueal = ancillary.BeltUsable ? Mathf.Clamp01(7f - tuning.AlternatorAngle) : 0f;
            float beltDrive = ancillary.BeltUsable ? Mathf.Lerp(1f, 0.25f, beltSqueal) : 0f;
            bool generating = ancillary.AlternatorUsable && beltDrive > 0f && state.EngineRpm * beltDrive > 550f;
            bool ignitionPowered = ancillary.BatteryIgnitionPowered ||
                generating && ancillary.AlternatorIgnitionWired;
            bool primed = state.EngineStatus == VehicleEngineStatus.Running || services.CrankingSeconds >= coldDelay;
            bool combustion = mixture > 0.5f && primed && conditions.CrankshaftCondition > 0f &&
                conditions.RockerShaftCondition > 0f && ignitionPowered;
            float knock = Mathf.Max(afr > 16f ? Mathf.Clamp01((afr - 16f) / 3f + 0.15f) : 0f,
                tuning.SparkDegrees > 15f ? Mathf.Clamp01((tuning.SparkDegrees - 15f) / 5f) : 0f);
            float oilFraction = Mathf.Clamp01(state.OilLiters / SatsumaServiceFluidRules.OilCapacityLiters);
            float extraFriction = Mathf.Max(0f, 30f - state.EngineTemperatureCelsius) * 0.08f +
                (1f - oilFraction) * 8f + Mathf.Max(0f, 10f - conditions.CrankshaftCondition) * 2f;

            LastPoint = new SatsumaEngineOperatingPoint(combustion, ignitionPowered,
                Mathf.Clamp(mixture * 1000f + choke * 1000f, 500f, 2400f), Mathf.Max(0f, torqueScale),
                extraFriction, Mathf.Lerp(0.5f, 1f, Mathf.InverseLerp(9.5f, 12.6f, state.BatteryVoltage)),
                afr, knock, Mathf.Max((badIntake + badExhaust) / 8f, Mathf.Clamp01((10f - conditions.RockerShaftCondition) / 10f)),
                Mathf.Max(Mathf.Max(badIntake / 4f, camError > 1f ? 0.7f : 0f), conditions.PlugRoughness01 * .5f), camError < -1f ? 0.7f : 0f,
                beltSqueal, beltDrive, generating,
                HydraulicEfficiency(services.BrakeFrontLiters, 1f, hardware.FrontHydraulicsConnected),
                HydraulicEfficiency(services.BrakeRearLiters, 1f, hardware.RearHydraulicsConnected),
                HydraulicEfficiency(services.ClutchLiters, 0.5f, hardware.ClutchHydraulicsConnected),
                Mathf.Clamp01((10f - conditions.CrankshaftCondition) / 10f));
            return LastPoint;
        }

        public SatsumaWearDelta StepSupport(VehicleSimulationConfig config, VehicleSimulationState state,
            in VehicleInputState input, in SatsumaOperatingInputs conditions, bool starterActive, float dt)
        {
            SatsumaOperatingState services = state.SatsumaOperating;
            SatsumaAncillaryInputs ancillary = conditions.Ancillary;
            SatsumaFluidHardwareInputs hardware = conditions.Fluids;
            bool running = state.EngineStatus == VehicleEngineStatus.Running && LastPoint.CombustionAllowed;
            bool rotating = state.EngineRpm > 100f;
            float voltage = state.BatteryVoltage;
            if (ancillary.BatteryConnected)
            {
                if (conditions.DebugAutocharge) voltage = config.SupportSystems.NominalBatteryVoltage;
                else if (starterActive) voltage -= config.SupportSystems.StarterDrainVoltsPerSecond * dt;
                else if (LastPoint.AlternatorGenerating && ancillary.AlternatorChargeWired)
                    voltage += config.SupportSystems.AlternatorChargeVoltsPerSecond * dt;
                else if (input.IgnitionOn || services.RadiatorFanRunning)
                    voltage -= (services.RadiatorFanRunning ? 0.012f : 0.002f) * dt;
            }
            state.BatteryVoltage = Mathf.Clamp(voltage, 0f, config.SupportSystems.NominalBatteryVoltage);
            state.BatteryCharge01 = Mathf.Clamp01(state.BatteryVoltage / config.SupportSystems.NominalBatteryVoltage);

            if (!hardware.OilPanInstalled) state.OilLiters = 0f;
            // Reviewed drain bolt opens below stage6, even with the engine off.
            // .1 L/s is the explicit presentation/continuous-flow calibration.
            if (hardware.OilDrainOpen) state.OilLiters = Mathf.Max(0f, state.OilLiters - .1f * dt);
            if (rotating && state.OilLiters > 0f)
            {
                // Oil112415 applies these litre losses once per 1.1 s Wait loop.
                float leak = (8f - Mathf.Clamp(hardware.FilterTightness, 0f, 8f)) / 800f +
                    (64f - Mathf.Clamp(hardware.PanTightness, 0f, 64f)) / 1000f +
                    (48f - Mathf.Clamp(hardware.CoverTightness, 0f, 48f)) / 60000f;
                if (!hardware.FilterInstalled) leak += 0.05f;
                if (!hardware.HeadGasketHealthy) leak += 0.01f;
                if (!hardware.OilCapClosed) leak += 0.0008f;
                state.OilLiters = Mathf.Max(0f, state.OilLiters - leak * dt / 1.1f);
                float contamination = Mathf.Max(0.01f, state.EngineRpm / 250000f) -
                    (hardware.FilterInstalled ? 0.01f : 0f);
                services.OilContaminationPercent = Mathf.Clamp(services.OilContaminationPercent + contamination * dt / 1.1f, 0f, 100f);
            }
            float oilFraction = Mathf.Clamp01(state.OilLiters / SatsumaServiceFluidRules.OilCapacityLiters);
            float pressure = rotating && hardware.OilPanInstalled && hardware.FilterInstalled
                ? Mathf.Clamp(state.EngineRpm / 800f, 0f, 5f) * oilFraction *
                    Mathf.Lerp(1.4f, 0.7f, Mathf.InverseLerp(20f, 130f, state.EngineTemperatureCelsius)) : 0f;
            services.OilPressureBar = Mathf.MoveTowards(services.OilPressureBar, pressure, 3f * dt);

            if (!hardware.RadiatorInstalled) state.CoolantLiters = 0f;
            float coolantFraction = Mathf.Clamp01(state.CoolantLiters / SatsumaServiceFluidRules.CoolantCapacityLiters);
            // Cooling106153 stock pressure rule. An open cap is at atmospheric pressure.
            services.CoolantPressurePsi = hardware.RadiatorCapClosed && coolantFraction > 0f
                ? Mathf.Clamp(state.CoolantLiters * state.EngineTemperatureCelsius / 44f * 1.4f, 0.1f, 50f) : 0f;
            bool fanPowered = ancillary.FanWired && (ancillary.BatteryConnected && state.BatteryVoltage > 9.7f ||
                LastPoint.AlternatorGenerating && ancillary.AlternatorChargeWired);
            services.RadiatorFanRunning = hardware.RadiatorInstalled && fanPowered &&
                state.EngineTemperatureCelsius > 97f;
            if (state.CoolantLiters > 0f)
            {
                float leak = !hardware.HosesInstalled ? 0.05f :
                    (40f - Mathf.Clamp(hardware.HoseTightness, 0f, 40f)) / 8000f;
                if (!hardware.HeadGasketHealthy) leak += 0.004f;
                if (services.CoolantPressurePsi > 16f) leak += 0.05f;
                if (!hardware.RadiatorCapClosed && state.EngineTemperatureCelsius > 100f) leak += 0.02f;
                state.CoolantLiters = Mathf.Max(0f, state.CoolantLiters - leak * dt);
            }

            // Continuous energy calibration: coolant, airflow and electric fan
            // are separate paths. A belt must not mechanically spin the radiator fan.
            float ambient = Mathf.Clamp(conditions.AmbientCelsius, -50f, 60f);
            float temperature = state.EngineTemperatureCelsius;
            float heat = running ? 0.22f + state.EngineLoad01 * 0.45f +
                (1f - oilFraction) * 0.45f + LastPoint.Knock01 * 0.12f : 0f;
            float cooling = Mathf.Max(0f, temperature - ambient) *
                (0.0016f + Mathf.Abs(state.VehicleSpeedMetersPerSecond) * 0.0001f);
            if (temperature > 80f && hardware.RadiatorInstalled && hardware.HosesInstalled && ancillary.WaterPumpUsable)
                cooling += (temperature - 80f) * 0.025f * coolantFraction * LastPoint.BeltDrive01;
            if (services.RadiatorFanRunning) cooling += 0.22f * coolantFraction;
            state.EngineTemperatureCelsius = Mathf.Clamp(temperature + (heat - cooling) * dt, ambient, 220f);
            state.CoolantTemperatureCelsius = Mathf.MoveTowards(state.CoolantTemperatureCelsius,
                coolantFraction > 0f ? state.EngineTemperatureCelsius : ambient, 2f * dt);

            // Unconnected hydraulics lose finite fluid; parking brake is a separate mechanical system.
            if (!hardware.FrontHydraulicsConnected) services.BrakeFrontLiters = Mathf.Max(0f, services.BrakeFrontLiters - 0.015f * dt);
            if (!hardware.RearHydraulicsConnected) services.BrakeRearLiters = Mathf.Max(0f, services.BrakeRearLiters - 0.015f * dt);
            if (!hardware.ClutchHydraulicsConnected) services.ClutchLiters = Mathf.Max(0f, services.ClutchLiters - 0.01f * dt);
            // Brakes113311: 88 points of pipe fittings, a .4 s leak cadence and
            // pedal input/50 per cadence. Clutch scaling is a calibrated analogue.
            float brakeLeak = (1f - Mathf.Clamp01(hardware.BrakeLineSeal01)) *
                (.0088f + Mathf.Clamp01(input.Brake01) * .02f) * dt / .4f;
            float clutchLeak = (1f - Mathf.Clamp01(hardware.ClutchLineSeal01)) *
                (.0016f + Mathf.Clamp01(input.ClutchPedal01) * .01f) * dt / .4f;
            services.BrakeFrontLiters = Mathf.Max(0f, services.BrakeFrontLiters - brakeLeak);
            services.BrakeRearLiters = Mathf.Max(0f, services.BrakeRearLiters - brakeLeak);
            services.ClutchLiters = Mathf.Max(0f, services.ClutchLiters - clutchLeak);
            if (!running) return default;
            float deficitRpm = Mathf.Max(0.01f, 3.01f - state.OilLiters) * state.EngineRpm;
            float overheat = Mathf.Max(0f, state.EngineTemperatureCelsius - 130f) * 0.002f;
            float dirt = services.OilContaminationPercent * 10000f;
            float pistonWear = deficitRpm / Mathf.Max(100000f, 1100000f - dirt) / 1.1f + overheat;
            float crankWear = deficitRpm / Mathf.Max(100000f, 1250000f - dirt) / 1.1f + overheat;
            return new SatsumaWearDelta(pistonWear * dt, crankWear * dt,
                (state.EngineTemperatureCelsius / 16500f / 1.1f + overheat) * dt,
                ancillary.BeltUsable ? Mathf.Abs(7f - conditions.Tuning.AlternatorAngle) / 200f * dt : 0f,
                ancillary.WaterPumpUsable ? services.CoolantPressurePsi / 2700f / 1.1f * dt : 0f,
                // Cylinders104983 Break applies 5.5 damage. Seconds are the
                // explicit modern integration cadence, not copied FSM execution.
                Mathf.Abs(Mathf.DeltaAngle(0f, conditions.Tuning.CamDegrees)) > 6f ? 5.5f * dt : 0f);
        }

        private static float HydraulicEfficiency(float liters, float capacity, bool connected) =>
            connected ? Mathf.Clamp01(liters / (capacity * 0.2f)) : 0f;
        private static int CountBits(int mask) => (mask & 1) + ((mask >> 1) & 1) + ((mask >> 2) & 1) + ((mask >> 3) & 1);
    }
}
