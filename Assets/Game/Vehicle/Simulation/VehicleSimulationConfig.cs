using System;
using UnityEngine;

namespace MSC.Vehicle.Simulation
{
    public enum VehicleReferenceClassification
    {
        MeasuredDonorReference = 0,
        ObservedDonorReference = 1,
        DerivedReference = 2,
        PlausibilityTarget = 3,
        RemakeDesignTarget = 4,
        Unknown = 5
    }

    [Serializable]
    public sealed class VehicleTuningProvenance
    {
        [SerializeField] private VehicleReferenceClassification classification;
        [SerializeField] private string label = string.Empty;
        [SerializeField] private string source = string.Empty;
        [SerializeField] private string notes = string.Empty;

        public VehicleReferenceClassification Classification => classification;
        public string Label => label;
        public string Source => source;
        public string Notes => notes;
        public bool IsProvisional => string.Equals(label, "ProvisionalProjectTuning", StringComparison.Ordinal);

        public void Configure(
            VehicleReferenceClassification valueClassification,
            string valueLabel,
            string valueSource,
            string valueNotes)
        {
            classification = valueClassification;
            label = valueLabel ?? string.Empty;
            source = valueSource ?? string.Empty;
            notes = valueNotes ?? string.Empty;
        }
    }

    [Serializable]
    public struct VehicleTorqueSample
    {
        [SerializeField] private float rpm;
        [SerializeField] private float torqueNewtonMeters;

        public VehicleTorqueSample(float sampleRpm, float sampleTorqueNewtonMeters)
        {
            rpm = Mathf.Max(0f, sampleRpm);
            torqueNewtonMeters = Mathf.Max(0f, sampleTorqueNewtonMeters);
        }

        public float Rpm => rpm;
        public float TorqueNewtonMeters => torqueNewtonMeters;
    }

    [Serializable]
    public sealed class EngineSimulationConfig
    {
        [SerializeField, Min(0.01f)] private float inertiaKilogramSquareMeters = 0.24f;
        [SerializeField, Min(100f)] private float idleTargetRpm = 900f;
        [SerializeField, Min(50f)] private float startThresholdRpm = 450f;
        [SerializeField, Min(50f)] private float stallRpm = 500f;
        [SerializeField, Min(500f)] private float redlineRpm = 7000f;
        [SerializeField, Min(500f)] private float maximumRpm = 7400f;
        [SerializeField, Min(0f)] private float baseFrictionNewtonMeters = 8f;
        [SerializeField, Min(0f)] private float viscousFrictionNewtonMetersPerRadian = 0.02f;
        [SerializeField, Min(0f)] private float engineBrakingNewtonMeters = 18f;
        [SerializeField, Min(0.1f)] private float throttleResponsePerSecond = 5f;
        [SerializeField, Min(0f)] private float starterTorqueNewtonMeters = 65f;
        [SerializeField, Min(50f)] private float starterMaximumRpm = 650f;
        [SerializeField, Min(0.01f)] private float stallDelaySeconds = 0.28f;
        [SerializeField] private VehicleTorqueSample[] torqueCurve = Array.Empty<VehicleTorqueSample>();

        public float InertiaKilogramSquareMeters => inertiaKilogramSquareMeters;
        public float IdleTargetRpm => idleTargetRpm;
        public float StartThresholdRpm => startThresholdRpm;
        public float StallRpm => stallRpm;
        public float RedlineRpm => redlineRpm;
        public float MaximumRpm => maximumRpm;
        public float BaseFrictionNewtonMeters => baseFrictionNewtonMeters;
        public float ViscousFrictionNewtonMetersPerRadian => viscousFrictionNewtonMetersPerRadian;
        public float EngineBrakingNewtonMeters => engineBrakingNewtonMeters;
        public float ThrottleResponsePerSecond => throttleResponsePerSecond;
        public float StarterTorqueNewtonMeters => starterTorqueNewtonMeters;
        public float StarterMaximumRpm => starterMaximumRpm;
        public float StallDelaySeconds => stallDelaySeconds;
        public VehicleTorqueSample[] TorqueCurve => torqueCurve;

        public float EvaluateTorque(float rpm)
        {
            if (torqueCurve == null || torqueCurve.Length == 0)
            {
                return 0f;
            }

            float clampedRpm = Mathf.Max(0f, rpm);
            if (clampedRpm <= torqueCurve[0].Rpm)
            {
                return torqueCurve[0].TorqueNewtonMeters;
            }

            for (int index = 1; index < torqueCurve.Length; index++)
            {
                VehicleTorqueSample upper = torqueCurve[index];
                if (clampedRpm > upper.Rpm)
                {
                    continue;
                }

                VehicleTorqueSample lower = torqueCurve[index - 1];
                float range = Mathf.Max(1f, upper.Rpm - lower.Rpm);
                return Mathf.Lerp(
                    lower.TorqueNewtonMeters,
                    upper.TorqueNewtonMeters,
                    (clampedRpm - lower.Rpm) / range);
            }

            return torqueCurve[torqueCurve.Length - 1].TorqueNewtonMeters;
        }

        public void ApplyPrototypeDefaults()
        {
            inertiaKilogramSquareMeters = 0.24f;
            idleTargetRpm = 900f;
            startThresholdRpm = 450f;
            stallRpm = 500f;
            redlineRpm = 7000f;
            maximumRpm = 7400f;
            baseFrictionNewtonMeters = 8f;
            viscousFrictionNewtonMetersPerRadian = 0.02f;
            engineBrakingNewtonMeters = 18f;
            throttleResponsePerSecond = 5f;
            starterTorqueNewtonMeters = 65f;
            starterMaximumRpm = 650f;
            stallDelaySeconds = 0.28f;
            torqueCurve = new[]
            {
                new VehicleTorqueSample(0f, 0f),
                new VehicleTorqueSample(700f, 68f),
                new VehicleTorqueSample(1800f, 96f),
                new VehicleTorqueSample(3500f, 108f),
                new VehicleTorqueSample(5200f, 92f),
                new VehicleTorqueSample(7000f, 0f)
            };
        }
    }

    [Serializable]
    public sealed class ClutchSimulationConfig
    {
        [SerializeField, Min(1f)] private float maximumTorqueNewtonMeters = 220f;
        [SerializeField, Min(0.01f)] private float slipStiffness = 7.5f;
        [SerializeField, Min(0.1f)] private float engagementExponent = 1.6f;

        public float MaximumTorqueNewtonMeters => maximumTorqueNewtonMeters;
        public float SlipStiffness => slipStiffness;
        public float EngagementExponent => engagementExponent;

        public void ApplyPrototypeDefaults()
        {
            maximumTorqueNewtonMeters = 220f;
            slipStiffness = 7.5f;
            engagementExponent = 1.6f;
        }
    }

    [Serializable]
    public sealed class GearboxSimulationConfig
    {
        [SerializeField] private float reverseRatio = -3.25f;
        [SerializeField] private float[] forwardRatios = Array.Empty<float>();
        [SerializeField, Min(0.1f)] private float finalDriveRatio = 3.9f;
        [SerializeField, Range(0.1f, 1f)] private float efficiency = 0.9f;

        public float ReverseRatio => reverseRatio;
        public float[] ForwardRatios => forwardRatios;
        public int ForwardGearCount => forwardRatios?.Length ?? 0;
        public float FinalDriveRatio => finalDriveRatio;
        public float Efficiency => efficiency;

        public bool TryGetRatio(int gear, out float ratio)
        {
            if (gear == 0)
            {
                ratio = 0f;
                return true;
            }

            if (gear == -1)
            {
                ratio = reverseRatio;
                return true;
            }

            if (gear > 0 && forwardRatios != null && gear <= forwardRatios.Length)
            {
                ratio = forwardRatios[gear - 1];
                return true;
            }

            ratio = 0f;
            return false;
        }

        public void ApplyPrototypeDefaults()
        {
            reverseRatio = -3.25f;
            forwardRatios = new[] { 3.5f, 2.05f, 1.36f, 1f, 0.82f };
            finalDriveRatio = 3.9f;
            efficiency = 0.9f;
        }
    }

    [Serializable]
    public sealed class VehicleDynamicsConfig
    {
        [SerializeField, Min(100f)] private float provisionalMassKilograms = 650f;
        [SerializeField, Min(0.05f)] private float wheelRadiusMeters = 0.272667f;
        [SerializeField, Min(0.01f)] private float wheelInertiaKilogramSquareMeters = 1.15f;
        [SerializeField, Min(0f)] private float maximumBrakeTorqueNewtonMeters = 1800f;
        [SerializeField, Min(0f)] private float maximumHandbrakeTorqueNewtonMeters = 850f;
        [SerializeField, Range(1f, 60f)] private float maximumSteeringAngleDegrees = 30f;
        [SerializeField, Range(1f, 60f)] private float highSpeedSteeringAngleDegrees = 12f;
        [SerializeField, Min(1f)] private float steeringFadeSpeedMetersPerSecond = 25f;
        [SerializeField, Min(0.01f)] private float suspensionRestLengthMeters = 0.32f;
        [SerializeField, Min(0.01f)] private float suspensionTravelMeters = 0.18f;
        [SerializeField, Min(1f)] private float springRateNewtonPerMeter = 28000f;
        [SerializeField, Min(1f)] private float damperRateNewtonSecondsPerMeter = 3500f;
        [SerializeField, Min(1f)] private float longitudinalStiffness = 7000f;
        [SerializeField, Min(1f)] private float lateralStiffness = 6200f;
        [SerializeField, Min(0f)] private float rollingResistanceCoefficient = 0.015f;

        public float ProvisionalMassKilograms => provisionalMassKilograms;
        public float WheelRadiusMeters => wheelRadiusMeters;
        public float WheelInertiaKilogramSquareMeters => wheelInertiaKilogramSquareMeters;
        public float MaximumBrakeTorqueNewtonMeters => maximumBrakeTorqueNewtonMeters;
        public float MaximumHandbrakeTorqueNewtonMeters => maximumHandbrakeTorqueNewtonMeters;
        public float MaximumSteeringAngleDegrees => maximumSteeringAngleDegrees;
        public float HighSpeedSteeringAngleDegrees => highSpeedSteeringAngleDegrees;
        public float SteeringFadeSpeedMetersPerSecond => steeringFadeSpeedMetersPerSecond;
        public float SuspensionRestLengthMeters => suspensionRestLengthMeters;
        public float SuspensionTravelMeters => suspensionTravelMeters;
        public float SpringRateNewtonPerMeter => springRateNewtonPerMeter;
        public float DamperRateNewtonSecondsPerMeter => damperRateNewtonSecondsPerMeter;
        public float LongitudinalStiffness => longitudinalStiffness;
        public float LateralStiffness => lateralStiffness;
        public float RollingResistanceCoefficient => rollingResistanceCoefficient;

        public void ApplyPrototypeDefaults()
        {
            provisionalMassKilograms = 650f;
            wheelRadiusMeters = 0.272667f;
            wheelInertiaKilogramSquareMeters = 1.15f;
            maximumBrakeTorqueNewtonMeters = 1800f;
            maximumHandbrakeTorqueNewtonMeters = 850f;
            maximumSteeringAngleDegrees = 30f;
            highSpeedSteeringAngleDegrees = 12f;
            steeringFadeSpeedMetersPerSecond = 25f;
            suspensionRestLengthMeters = 0.32f;
            suspensionTravelMeters = 0.18f;
            springRateNewtonPerMeter = 28000f;
            damperRateNewtonSecondsPerMeter = 3500f;
            longitudinalStiffness = 7000f;
            lateralStiffness = 6200f;
            rollingResistanceCoefficient = 0.015f;
        }
    }

    [Serializable]
    public sealed class VehicleSupportSystemsConfig
    {
        [SerializeField, Min(1f)] private float nominalBatteryVoltage = 12.6f;
        [SerializeField, Min(1f)] private float minimumCrankVoltage = 9.5f;
        [SerializeField, Min(0.01f)] private float starterDrainVoltsPerSecond = 0.16f;
        [SerializeField, Min(0.01f)] private float alternatorChargeVoltsPerSecond = 0.04f;
        [SerializeField, Min(0f)] private float initialFuelLiters = 15f;
        [SerializeField, Min(0f)] private float minimumFuelLiters = 0.05f;
        [SerializeField, Min(0f)] private float initialOilLiters = 3.5f;
        [SerializeField, Min(0f)] private float minimumOilLiters = 1f;
        [SerializeField, Min(0f)] private float initialCoolantLiters = 5f;
        [SerializeField, Min(0f)] private float minimumCoolantLiters = 1f;
        [SerializeField, Min(0f)] private float idleFuelLitersPerSecond = 0.00035f;
        [SerializeField, Min(0f)] private float fullLoadFuelLitersPerSecond = 0.0025f;
        [SerializeField] private float ambientTemperatureCelsius = 20f;
        [SerializeField] private float operatingTemperatureCelsius = 88f;
        [SerializeField, Min(0f)] private float heatingRatePerSecond = 0.035f;
        [SerializeField, Min(0f)] private float coolingRatePerSecond = 0.012f;

        public float NominalBatteryVoltage => nominalBatteryVoltage;
        public float MinimumCrankVoltage => minimumCrankVoltage;
        public float StarterDrainVoltsPerSecond => starterDrainVoltsPerSecond;
        public float AlternatorChargeVoltsPerSecond => alternatorChargeVoltsPerSecond;
        public float InitialFuelLiters => initialFuelLiters;
        public float MinimumFuelLiters => minimumFuelLiters;
        public float InitialOilLiters => initialOilLiters;
        public float MinimumOilLiters => minimumOilLiters;
        public float InitialCoolantLiters => initialCoolantLiters;
        public float MinimumCoolantLiters => minimumCoolantLiters;
        public float IdleFuelLitersPerSecond => idleFuelLitersPerSecond;
        public float FullLoadFuelLitersPerSecond => fullLoadFuelLitersPerSecond;
        public float AmbientTemperatureCelsius => ambientTemperatureCelsius;
        public float OperatingTemperatureCelsius => operatingTemperatureCelsius;
        public float HeatingRatePerSecond => heatingRatePerSecond;
        public float CoolingRatePerSecond => coolingRatePerSecond;

        public void ApplyPrototypeDefaults()
        {
            nominalBatteryVoltage = 12.6f;
            minimumCrankVoltage = 9.5f;
            starterDrainVoltsPerSecond = 0.16f;
            alternatorChargeVoltsPerSecond = 0.04f;
            initialFuelLiters = 15f;
            minimumFuelLiters = 0.05f;
            initialOilLiters = 3.5f;
            minimumOilLiters = 1f;
            initialCoolantLiters = 5f;
            minimumCoolantLiters = 1f;
            idleFuelLitersPerSecond = 0.00035f;
            fullLoadFuelLitersPerSecond = 0.0025f;
            ambientTemperatureCelsius = 20f;
            operatingTemperatureCelsius = 88f;
            heatingRatePerSecond = 0.035f;
            coolingRatePerSecond = 0.012f;
        }
    }

    [Serializable]
    public struct VehicleSurfaceResponse
    {
        [SerializeField] private VehicleSurfaceType surfaceType;
        [SerializeField, Min(0.05f)] private float frictionMultiplier;
        [SerializeField, Min(0f)] private float rollingResistanceMultiplier;

        public VehicleSurfaceResponse(
            VehicleSurfaceType type,
            float friction,
            float rollingResistance)
        {
            surfaceType = type;
            frictionMultiplier = Mathf.Max(0.05f, friction);
            rollingResistanceMultiplier = Mathf.Max(0f, rollingResistance);
        }

        public VehicleSurfaceType SurfaceType => surfaceType;
        public float FrictionMultiplier => frictionMultiplier;
        public float RollingResistanceMultiplier => rollingResistanceMultiplier;
    }

    [CreateAssetMenu(menuName = "MSC/Vehicle Simulation/Simulation Config")]
    public sealed class VehicleSimulationConfig : ScriptableObject
    {
        public const string PrototypeTuningLabel = "ProvisionalProjectTuning";

        [SerializeField] private string configurationId = "m06.prototype.satsuma";
        [SerializeField, Range(1, 8)] private int substepCount = 4;
        [SerializeField, Range(0, 3)] private int leftDrivenWheelIndex;
        [SerializeField, Range(0, 3)] private int rightDrivenWheelIndex = 1;
        [SerializeField] private EngineSimulationConfig engine = new EngineSimulationConfig();
        [SerializeField] private ClutchSimulationConfig clutch = new ClutchSimulationConfig();
        [SerializeField] private GearboxSimulationConfig gearbox = new GearboxSimulationConfig();
        [SerializeField] private VehicleDynamicsConfig dynamics = new VehicleDynamicsConfig();
        [SerializeField] private VehicleSupportSystemsConfig supportSystems = new VehicleSupportSystemsConfig();
        [SerializeField] private VehicleSurfaceResponse[] surfaceResponses = Array.Empty<VehicleSurfaceResponse>();
        [SerializeField] private VehicleTuningProvenance dynamicTuning = new VehicleTuningProvenance();
        [SerializeField] private VehicleTuningProvenance geometryReference = new VehicleTuningProvenance();

        public string ConfigurationId => configurationId;
        public int SubstepCount => substepCount;
        public int WheelCount => 4;
        public int LeftDrivenWheelIndex => leftDrivenWheelIndex;
        public int RightDrivenWheelIndex => rightDrivenWheelIndex;
        public EngineSimulationConfig Engine => engine;
        public ClutchSimulationConfig Clutch => clutch;
        public GearboxSimulationConfig Gearbox => gearbox;
        public VehicleDynamicsConfig Dynamics => dynamics;
        public VehicleSupportSystemsConfig SupportSystems => supportSystems;
        public VehicleSurfaceResponse[] SurfaceResponses => surfaceResponses;
        public VehicleTuningProvenance DynamicTuning => dynamicTuning;
        public VehicleTuningProvenance GeometryReference => geometryReference;
        public float AmbientTemperatureCelsius => supportSystems.AmbientTemperatureCelsius;
        public float BatteryNominalVoltage => supportSystems.NominalBatteryVoltage;
        public float InitialFuelLiters => supportSystems.InitialFuelLiters;
        public float InitialOilLiters => supportSystems.InitialOilLiters;
        public float InitialCoolantLiters => supportSystems.InitialCoolantLiters;

        public bool IsDrivenWheel(int wheelIndex) =>
            wheelIndex == leftDrivenWheelIndex || wheelIndex == rightDrivenWheelIndex;
        public bool IsSteeredWheel(int wheelIndex) => wheelIndex == 0 || wheelIndex == 1;

        public VehicleSurfaceResponse GetSurfaceResponse(VehicleSurfaceType type)
        {
            if (surfaceResponses != null)
            {
                for (int index = 0; index < surfaceResponses.Length; index++)
                {
                    if (surfaceResponses[index].SurfaceType == type)
                    {
                        return surfaceResponses[index];
                    }
                }
            }

            return new VehicleSurfaceResponse(VehicleSurfaceType.Unknown, 0.7f, 1.35f);
        }

        public void ApplyProvisionalPrototypeDefaults()
        {
            configurationId = "m06.prototype.satsuma";
            substepCount = 4;
            leftDrivenWheelIndex = 0;
            rightDrivenWheelIndex = 1;
            engine ??= new EngineSimulationConfig();
            clutch ??= new ClutchSimulationConfig();
            gearbox ??= new GearboxSimulationConfig();
            dynamics ??= new VehicleDynamicsConfig();
            supportSystems ??= new VehicleSupportSystemsConfig();
            dynamicTuning ??= new VehicleTuningProvenance();
            geometryReference ??= new VehicleTuningProvenance();
            engine.ApplyPrototypeDefaults();
            clutch.ApplyPrototypeDefaults();
            gearbox.ApplyPrototypeDefaults();
            dynamics.ApplyPrototypeDefaults();
            supportSystems.ApplyPrototypeDefaults();
            surfaceResponses = new[]
            {
                new VehicleSurfaceResponse(VehicleSurfaceType.Unknown, 0.7f, 1.35f),
                new VehicleSurfaceResponse(VehicleSurfaceType.Paved, 1f, 1f),
                new VehicleSurfaceResponse(VehicleSurfaceType.Gravel, 0.72f, 1.45f),
                new VehicleSurfaceResponse(VehicleSurfaceType.Dirt, 0.62f, 1.75f),
                new VehicleSurfaceResponse(VehicleSurfaceType.Grass, 0.48f, 2.1f),
                new VehicleSurfaceResponse(VehicleSurfaceType.MudWet, 0.35f, 2.8f)
            };
            dynamicTuning.Configure(
                VehicleReferenceClassification.RemakeDesignTarget,
                PrototypeTuningLabel,
                "ProjectAuthored/Milestone06",
                "Front-driven topology is a project-owned target-vehicle choice; powertrain, tire, suspension, brake, electrical, fluid and thermal values are provisional because 04B.4 dynamic fixtures are Missing.");
            geometryReference.Configure(
                VehicleReferenceClassification.DerivedReference,
                "04B.4VehicleWheelAnchors",
                "ReferenceCaptureDatabase/vehicle-body-wheel-geometry-v1",
                "Wheelbase/tracks derive from reviewed wheel roots; candidate tire radius remains NeedsReview and is used only as a plausibility target.");
        }

        public void SetSubstepCountForTesting(int count)
        {
            substepCount = Mathf.Clamp(count, 1, 8);
        }

        public void SetDrivenWheelsForTesting(int leftWheelIndex, int rightWheelIndex)
        {
            leftDrivenWheelIndex = leftWheelIndex;
            rightDrivenWheelIndex = rightWheelIndex;
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(configurationId))
            {
                failure = "Configuration ID is empty.";
                return false;
            }

            if (engine == null || clutch == null || gearbox == null || dynamics == null || supportSystems == null)
            {
                failure = "A required simulation config group is missing.";
                return false;
            }

            if (gearbox.ForwardGearCount < 1 || engine.TorqueCurve == null || engine.TorqueCurve.Length < 2)
            {
                failure = "Torque curve or forward gear ratios are incomplete.";
                return false;
            }

            if (!AreFinite(
                    engine.InertiaKilogramSquareMeters,
                    engine.IdleTargetRpm,
                    engine.StartThresholdRpm,
                    engine.StallRpm,
                    engine.RedlineRpm,
                    engine.MaximumRpm,
                    engine.BaseFrictionNewtonMeters,
                    engine.ViscousFrictionNewtonMetersPerRadian,
                    engine.EngineBrakingNewtonMeters,
                    engine.ThrottleResponsePerSecond,
                    engine.StarterTorqueNewtonMeters,
                    engine.StarterMaximumRpm,
                    engine.StallDelaySeconds,
                    clutch.MaximumTorqueNewtonMeters,
                    clutch.SlipStiffness,
                    clutch.EngagementExponent,
                    gearbox.FinalDriveRatio,
                    gearbox.Efficiency,
                    dynamics.ProvisionalMassKilograms,
                    dynamics.WheelRadiusMeters,
                    dynamics.WheelInertiaKilogramSquareMeters,
                    dynamics.MaximumBrakeTorqueNewtonMeters,
                    dynamics.MaximumHandbrakeTorqueNewtonMeters,
                    dynamics.MaximumSteeringAngleDegrees,
                    dynamics.HighSpeedSteeringAngleDegrees,
                    dynamics.SteeringFadeSpeedMetersPerSecond,
                    dynamics.SuspensionRestLengthMeters,
                    dynamics.SuspensionTravelMeters,
                    dynamics.SpringRateNewtonPerMeter,
                    dynamics.DamperRateNewtonSecondsPerMeter,
                    dynamics.LongitudinalStiffness,
                    dynamics.LateralStiffness,
                    dynamics.RollingResistanceCoefficient,
                    supportSystems.NominalBatteryVoltage,
                    supportSystems.MinimumCrankVoltage,
                    supportSystems.StarterDrainVoltsPerSecond,
                    supportSystems.AlternatorChargeVoltsPerSecond,
                    supportSystems.InitialFuelLiters,
                    supportSystems.MinimumFuelLiters,
                    supportSystems.InitialOilLiters,
                    supportSystems.MinimumOilLiters,
                    supportSystems.InitialCoolantLiters,
                    supportSystems.MinimumCoolantLiters,
                    supportSystems.IdleFuelLitersPerSecond,
                    supportSystems.FullLoadFuelLitersPerSecond,
                    supportSystems.AmbientTemperatureCelsius,
                    supportSystems.OperatingTemperatureCelsius,
                    supportSystems.HeatingRatePerSecond,
                    supportSystems.CoolingRatePerSecond))
            {
                failure = "Simulation tuning values must all be finite.";
                return false;
            }

            if (substepCount < 1 || substepCount > 8 ||
                leftDrivenWheelIndex < 0 || leftDrivenWheelIndex >= WheelCount ||
                rightDrivenWheelIndex < 0 || rightDrivenWheelIndex >= WheelCount ||
                leftDrivenWheelIndex == rightDrivenWheelIndex ||
                engine.InertiaKilogramSquareMeters <= 0f ||
                engine.IdleTargetRpm <= 0f ||
                engine.StartThresholdRpm <= 0f ||
                engine.StallRpm <= 0f ||
                engine.BaseFrictionNewtonMeters < 0f ||
                engine.ViscousFrictionNewtonMetersPerRadian < 0f ||
                engine.EngineBrakingNewtonMeters < 0f ||
                engine.ThrottleResponsePerSecond <= 0f ||
                engine.StarterTorqueNewtonMeters < 0f ||
                engine.StallDelaySeconds <= 0f ||
                engine.StartThresholdRpm >= engine.StarterMaximumRpm ||
                engine.StallRpm >= engine.IdleTargetRpm ||
                engine.IdleTargetRpm >= engine.RedlineRpm ||
                engine.RedlineRpm > engine.MaximumRpm ||
                clutch.MaximumTorqueNewtonMeters <= 0f ||
                clutch.SlipStiffness <= 0f ||
                clutch.EngagementExponent <= 0f ||
                gearbox.ReverseRatio >= 0f ||
                gearbox.FinalDriveRatio <= 0f ||
                gearbox.Efficiency <= 0f || gearbox.Efficiency > 1f ||
                dynamics.ProvisionalMassKilograms <= 0f ||
                dynamics.WheelRadiusMeters <= 0f ||
                dynamics.WheelInertiaKilogramSquareMeters <= 0f ||
                dynamics.MaximumBrakeTorqueNewtonMeters < 0f ||
                dynamics.MaximumHandbrakeTorqueNewtonMeters < 0f ||
                dynamics.MaximumSteeringAngleDegrees <= 0f ||
                dynamics.HighSpeedSteeringAngleDegrees <= 0f ||
                dynamics.HighSpeedSteeringAngleDegrees > dynamics.MaximumSteeringAngleDegrees ||
                dynamics.SteeringFadeSpeedMetersPerSecond <= 0f ||
                dynamics.SuspensionRestLengthMeters <= 0f ||
                dynamics.SuspensionTravelMeters <= 0f ||
                dynamics.SpringRateNewtonPerMeter <= 0f ||
                dynamics.DamperRateNewtonSecondsPerMeter <= 0f ||
                dynamics.LongitudinalStiffness <= 0f ||
                dynamics.LateralStiffness <= 0f ||
                dynamics.RollingResistanceCoefficient < 0f ||
                supportSystems.NominalBatteryVoltage <= 0f ||
                supportSystems.MinimumCrankVoltage <= 0f ||
                supportSystems.StarterDrainVoltsPerSecond < 0f ||
                supportSystems.AlternatorChargeVoltsPerSecond < 0f ||
                supportSystems.MinimumCrankVoltage > supportSystems.NominalBatteryVoltage ||
                supportSystems.MinimumFuelLiters < 0f ||
                supportSystems.InitialFuelLiters <= supportSystems.MinimumFuelLiters ||
                supportSystems.MinimumOilLiters < 0f ||
                supportSystems.InitialOilLiters <= supportSystems.MinimumOilLiters ||
                supportSystems.MinimumCoolantLiters < 0f ||
                supportSystems.InitialCoolantLiters <= supportSystems.MinimumCoolantLiters ||
                supportSystems.IdleFuelLitersPerSecond < 0f ||
                supportSystems.FullLoadFuelLitersPerSecond < 0f ||
                supportSystems.HeatingRatePerSecond < 0f ||
                supportSystems.CoolingRatePerSecond < 0f)
            {
                failure = "Simulation ranges are internally inconsistent.";
                return false;
            }

            float previousRpm = -1f;
            for (int index = 0; index < engine.TorqueCurve.Length; index++)
            {
                VehicleTorqueSample sample = engine.TorqueCurve[index];
                if (!VehicleSimulationMath.IsFinite(sample.Rpm) ||
                    !VehicleSimulationMath.IsFinite(sample.TorqueNewtonMeters) ||
                    sample.Rpm <= previousRpm)
                {
                    failure = "Torque curve samples must be finite and strictly ordered by RPM.";
                    return false;
                }

                previousRpm = sample.Rpm;
            }

            for (int gear = -1; gear <= gearbox.ForwardGearCount; gear++)
            {
                if (!gearbox.TryGetRatio(gear, out float ratio) ||
                    !VehicleSimulationMath.IsFinite(ratio) ||
                    gear != 0 && Mathf.Approximately(ratio, 0f))
                {
                    failure = "Gearbox contains an invalid ratio.";
                    return false;
                }
            }

            int requiredSurfaceMask = 0;
            int actualSurfaceMask = 0;
            for (int typeValue = (int)VehicleSurfaceType.Unknown;
                 typeValue <= (int)VehicleSurfaceType.MudWet;
                 typeValue++)
            {
                requiredSurfaceMask |= 1 << typeValue;
            }

            if (surfaceResponses != null)
            {
                for (int index = 0; index < surfaceResponses.Length; index++)
                {
                    VehicleSurfaceResponse response = surfaceResponses[index];
                    int typeValue = (int)response.SurfaceType;
                    if (typeValue < (int)VehicleSurfaceType.Unknown ||
                        typeValue > (int)VehicleSurfaceType.MudWet)
                    {
                        failure = "Surface response contains an unknown surface type.";
                        return false;
                    }

                    int bit = 1 << typeValue;
                    if ((actualSurfaceMask & bit) != 0 ||
                        !VehicleSimulationMath.IsFinite(response.FrictionMultiplier) ||
                        !VehicleSimulationMath.IsFinite(response.RollingResistanceMultiplier) ||
                        response.FrictionMultiplier <= 0f || response.RollingResistanceMultiplier < 0f)
                    {
                        failure = "Surface response rows must be unique, finite, and non-negative.";
                        return false;
                    }

                    actualSurfaceMask |= bit;
                }
            }

            if (actualSurfaceMask != requiredSurfaceMask)
            {
                failure = "Surface responses must cover Unknown, Paved, Gravel, Dirt, Grass, and MudWet.";
                return false;
            }

            if (dynamicTuning == null || !dynamicTuning.IsProvisional ||
                dynamicTuning.Classification != VehicleReferenceClassification.RemakeDesignTarget)
            {
                failure = "M06 dynamic values must be labelled ProvisionalProjectTuning.";
                return false;
            }

            if (geometryReference == null ||
                geometryReference.Classification != VehicleReferenceClassification.DerivedReference ||
                string.IsNullOrWhiteSpace(geometryReference.Source))
            {
                failure = "M06 geometry reference provenance is incomplete.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool AreFinite(params float[] values)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (!VehicleSimulationMath.IsFinite(values[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
