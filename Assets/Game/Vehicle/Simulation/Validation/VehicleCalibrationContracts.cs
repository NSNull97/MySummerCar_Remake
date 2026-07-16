using System;
using UnityEngine;

namespace MSC.Vehicle.Simulation
{
    public enum VehicleCalibrationFixtureKind
    {
        Custom = 0,
        StartIdle = 1,
        Acceleration = 2,
        Braking = 3,
        CoastDown = 4,
        SteeringStep = 5,
        Slalom = 6,
        SuspensionBump = 7,
        SurfaceComparison = 8,
        GarageClearance = 9,
        WorldTransition = 10,
        HillStart = 11
    }

    public enum VehicleCalibrationFixtureStatus
    {
        Ready = 0,
        Blocked = 1,
        Disabled = 2
    }

    public enum VehicleCalibrationRunStatus
    {
        NotStarted = 0,
        Running = 1,
        Passed = 2,
        Failed = 3,
        Inconclusive = 4,
        Blocked = 5,
        InvalidConfiguration = 6
    }

    public enum VehicleCalibrationTrialStatus
    {
        NotStarted = 0,
        Completed = 1,
        Failed = 2,
        InvalidNumericState = 3,
        Blocked = 4
    }

    public enum VehicleMetricDefinitionStatus
    {
        Active = 0,
        Informational = 1,
        Blocked = 2
    }

    public enum VehicleMetricResultStatus
    {
        NotEvaluated = 0,
        Passed = 1,
        Failed = 2,
        Inconclusive = 3,
        Informational = 4,
        UnknownReference = 5,
        Blocked = 6,
        InvalidSamples = 7,
        InvalidConfiguration = 8
    }

    public enum VehicleReferenceTargetStatus
    {
        Available = 0,
        Provisional = 1,
        Unknown = 2,
        Blocked = 3
    }

    public enum VehicleReferenceTargetMode
    {
        Unspecified = 0,
        ValueWithTolerance = 1,
        InclusiveRange = 2,
        Maximum = 3,
        Minimum = 4,
        Informational = 5
    }

    public enum VehicleMetricStatistic
    {
        Mean = 0,
        Median = 1,
        Minimum = 2,
        Maximum = 3,
        StandardDeviation = 4,
        LastValidSample = 5
    }

    public enum VehicleMetricDirection
    {
        TwoSided = 0,
        LowerIsBetter = 1,
        HigherIsBetter = 2,
        Informational = 3
    }

    public enum VehicleCalibrationInputSource
    {
        Unknown = 0,
        Scripted = 1,
        KeyboardMouse = 2,
        Gamepad = 3,
        AutomationDriver = 4
    }

    public enum VehicleCalibrationExecutionMode
    {
        Unknown = 0,
        EditMode = 1,
        PlayMode = 2,
        DevelopmentBuild = 3,
        PlayerBuild = 4
    }

    [Serializable]
    public sealed class VehicleCalibrationPartMass
    {
        [SerializeField] private string stablePartId = string.Empty;
        [SerializeField] private string partDefinitionId = string.Empty;
        [SerializeField] private bool installed = true;
        [SerializeField] private bool includedInPhysicsMass;
        [SerializeField, Min(0f)] private float massKilograms;

        public string StablePartId => stablePartId;
        public string PartDefinitionId => partDefinitionId;
        public bool Installed => installed;
        public bool IncludedInPhysicsMass => includedInPhysicsMass;
        public float MassKilograms => massKilograms;

        public VehicleCalibrationPartMass()
        {
        }

        public VehicleCalibrationPartMass(
            string valueStablePartId,
            string valuePartDefinitionId,
            bool valueInstalled,
            bool valueIncludedInPhysicsMass,
            float valueMassKilograms)
        {
            stablePartId = valueStablePartId ?? string.Empty;
            partDefinitionId = valuePartDefinitionId ?? string.Empty;
            installed = valueInstalled;
            includedInPhysicsMass = valueIncludedInPhysicsMass;
            massKilograms = valueMassKilograms;
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(stablePartId) ||
                string.IsNullOrWhiteSpace(partDefinitionId))
            {
                failure = "Part mass record requires stable and definition IDs.";
                return false;
            }

            if (!VehicleCalibrationValidation.IsFiniteNonNegative(massKilograms))
            {
                failure = $"Part '{stablePartId}' mass must be finite and non-negative.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class VehicleCalibrationMassConfiguration
    {
        [SerializeField, Min(0f)] private float bodyMassKilograms;
        [SerializeField, Min(0f)] private float installedPartsMassKilograms;
        [SerializeField, Min(0f)] private float fluidsMassKilograms;
        [SerializeField, Min(0f)] private float payloadMassKilograms;
        [SerializeField, Min(0f)] private float totalMassKilograms;
        [SerializeField] private Vector3 centerOfMassLocalMeters;
        [SerializeField] private VehicleCalibrationPartMass[] installedParts = Array.Empty<VehicleCalibrationPartMass>();

        public float BodyMassKilograms => bodyMassKilograms;
        public float InstalledPartsMassKilograms => installedPartsMassKilograms;
        public float FluidsMassKilograms => fluidsMassKilograms;
        public float PayloadMassKilograms => payloadMassKilograms;
        public float TotalMassKilograms => totalMassKilograms;
        public Vector3 CenterOfMassLocalMeters => centerOfMassLocalMeters;
        public VehicleCalibrationPartMass[] InstalledParts => installedParts;

        public VehicleCalibrationMassConfiguration()
        {
        }

        public VehicleCalibrationMassConfiguration(
            float valueBodyMassKilograms,
            float valueInstalledPartsMassKilograms,
            float valueFluidsMassKilograms,
            float valuePayloadMassKilograms,
            float valueTotalMassKilograms,
            Vector3 valueCenterOfMassLocalMeters,
            VehicleCalibrationPartMass[] valueInstalledParts)
        {
            bodyMassKilograms = valueBodyMassKilograms;
            installedPartsMassKilograms = valueInstalledPartsMassKilograms;
            fluidsMassKilograms = valueFluidsMassKilograms;
            payloadMassKilograms = valuePayloadMassKilograms;
            totalMassKilograms = valueTotalMassKilograms;
            centerOfMassLocalMeters = valueCenterOfMassLocalMeters;
            installedParts = valueInstalledParts ?? Array.Empty<VehicleCalibrationPartMass>();
        }

        public bool Validate(out string failure)
        {
            if (!VehicleCalibrationValidation.AreFiniteNonNegative(
                    bodyMassKilograms,
                    installedPartsMassKilograms,
                    fluidsMassKilograms,
                    payloadMassKilograms) ||
                !VehicleCalibrationValidation.IsFinitePositive(totalMassKilograms))
            {
                failure = "Mass values must be finite and non-negative, and total mass must be positive.";
                return false;
            }

            if (!VehicleCalibrationValidation.IsFinite(centerOfMassLocalMeters))
            {
                failure = "Center of mass must be finite.";
                return false;
            }

            if (installedParts == null)
            {
                failure = "Installed-parts mass records are null.";
                return false;
            }

            float includedPartMass = 0f;
            for (int index = 0; index < installedParts.Length; index++)
            {
                VehicleCalibrationPartMass part = installedParts[index];
                if (part == null)
                {
                    failure = $"Installed-parts mass record at index {index} is null.";
                    return false;
                }

                if (!part.Validate(out failure))
                {
                    failure = $"Invalid installed-parts mass record at index {index}: {failure}";
                    return false;
                }

                if (part.Installed && part.IncludedInPhysicsMass)
                {
                    includedPartMass += part.MassKilograms;
                }
            }

            float composedTotal = bodyMassKilograms + installedPartsMassKilograms +
                                  fluidsMassKilograms + payloadMassKilograms;
            if (Mathf.Abs(includedPartMass - installedPartsMassKilograms) > 0.001f ||
                Mathf.Abs(composedTotal - totalMassKilograms) > 0.001f)
            {
                failure =
                    "Mass composition must match included part records and total physics mass.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class VehicleCalibrationTireConfiguration
    {
        [SerializeField] private string wheelId = string.Empty;
        [SerializeField] private string tireConfigurationId = string.Empty;
        [SerializeField, Min(0.01f)] private float radiusMeters;
        [SerializeField, Min(0f)] private float pressureKilopascals;
        [SerializeField, Min(0f)] private float wear01;

        public string WheelId => wheelId;
        public string TireConfigurationId => tireConfigurationId;
        public float RadiusMeters => radiusMeters;
        public float PressureKilopascals => pressureKilopascals;
        public float Wear01 => wear01;

        public VehicleCalibrationTireConfiguration()
        {
        }

        public VehicleCalibrationTireConfiguration(
            string valueWheelId,
            string valueTireConfigurationId,
            float valueRadiusMeters,
            float valuePressureKilopascals,
            float valueWear01)
        {
            wheelId = valueWheelId ?? string.Empty;
            tireConfigurationId = valueTireConfigurationId ?? string.Empty;
            radiusMeters = valueRadiusMeters;
            pressureKilopascals = valuePressureKilopascals;
            wear01 = valueWear01;
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(wheelId) || string.IsNullOrWhiteSpace(tireConfigurationId))
            {
                failure = "Tire configuration requires wheel and tire IDs.";
                return false;
            }

            if (!VehicleCalibrationValidation.IsFinitePositive(radiusMeters) ||
                !VehicleCalibrationValidation.IsFiniteNonNegative(pressureKilopascals) ||
                !VehicleCalibrationValidation.IsFiniteInRange(wear01, 0f, 1f))
            {
                failure = $"Tire configuration '{wheelId}' contains invalid numeric values.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class VehicleCalibrationSurfaceConfiguration
    {
        [SerializeField] private VehicleSurfaceType surfaceType;
        [SerializeField] private string surfaceConfigurationId = string.Empty;
        [SerializeField, Min(0f)] private float frictionMultiplier = 1f;
        [SerializeField, Min(0f)] private float rollingResistanceMultiplier = 1f;

        public VehicleSurfaceType SurfaceType => surfaceType;
        public string SurfaceConfigurationId => surfaceConfigurationId;
        public float FrictionMultiplier => frictionMultiplier;
        public float RollingResistanceMultiplier => rollingResistanceMultiplier;

        public VehicleCalibrationSurfaceConfiguration()
        {
        }

        public VehicleCalibrationSurfaceConfiguration(
            VehicleSurfaceType valueSurfaceType,
            string valueSurfaceConfigurationId,
            float valueFrictionMultiplier,
            float valueRollingResistanceMultiplier)
        {
            surfaceType = valueSurfaceType;
            surfaceConfigurationId = valueSurfaceConfigurationId ?? string.Empty;
            frictionMultiplier = valueFrictionMultiplier;
            rollingResistanceMultiplier = valueRollingResistanceMultiplier;
        }

        public bool Validate(out string failure)
        {
            if (surfaceType == VehicleSurfaceType.Unknown || string.IsNullOrWhiteSpace(surfaceConfigurationId))
            {
                failure = "Surface type and configuration ID must be explicit.";
                return false;
            }

            if (!VehicleCalibrationValidation.IsFiniteNonNegative(frictionMultiplier) ||
                !VehicleCalibrationValidation.IsFiniteNonNegative(rollingResistanceMultiplier))
            {
                failure = "Surface multipliers must be finite and non-negative.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class VehicleCalibrationWeatherConfiguration
    {
        [SerializeField] private string weatherStateId = "clear";
        [SerializeField, Range(0f, 1f)] private float wetness01;
        [SerializeField] private float ambientTemperatureCelsius = 20f;
        [SerializeField] private float windMetersPerSecond;

        public string WeatherStateId => weatherStateId;
        public float Wetness01 => wetness01;
        public float AmbientTemperatureCelsius => ambientTemperatureCelsius;
        public float WindMetersPerSecond => windMetersPerSecond;

        public VehicleCalibrationWeatherConfiguration()
        {
        }

        public VehicleCalibrationWeatherConfiguration(
            string valueWeatherStateId,
            float valueWetness01,
            float valueAmbientTemperatureCelsius,
            float valueWindMetersPerSecond)
        {
            weatherStateId = valueWeatherStateId ?? string.Empty;
            wetness01 = valueWetness01;
            ambientTemperatureCelsius = valueAmbientTemperatureCelsius;
            windMetersPerSecond = valueWindMetersPerSecond;
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(weatherStateId) ||
                !VehicleCalibrationValidation.IsFiniteInRange(wetness01, 0f, 1f) ||
                !VehicleCalibrationValidation.IsFinite(ambientTemperatureCelsius) ||
                !VehicleCalibrationValidation.IsFiniteNonNegative(windMetersPerSecond))
            {
                failure = "Weather state contains an empty ID or invalid numeric values.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class VehicleCalibrationLoadConfiguration
    {
        [SerializeField, Min(0f)] private float fuelLiters;
        [SerializeField, Min(0f)] private float oilLiters;
        [SerializeField, Min(0f)] private float coolantLiters;
        [SerializeField, Min(0f)] private float driverMassKilograms;
        [SerializeField, Min(0f)] private float cargoMassKilograms;

        public float FuelLiters => fuelLiters;
        public float OilLiters => oilLiters;
        public float CoolantLiters => coolantLiters;
        public float DriverMassKilograms => driverMassKilograms;
        public float CargoMassKilograms => cargoMassKilograms;

        public VehicleCalibrationLoadConfiguration()
        {
        }

        public VehicleCalibrationLoadConfiguration(
            float valueFuelLiters,
            float valueOilLiters,
            float valueCoolantLiters,
            float valueDriverMassKilograms,
            float valueCargoMassKilograms)
        {
            fuelLiters = valueFuelLiters;
            oilLiters = valueOilLiters;
            coolantLiters = valueCoolantLiters;
            driverMassKilograms = valueDriverMassKilograms;
            cargoMassKilograms = valueCargoMassKilograms;
        }

        public bool Validate(out string failure)
        {
            if (!VehicleCalibrationValidation.AreFiniteNonNegative(
                    fuelLiters,
                    oilLiters,
                    coolantLiters,
                    driverMassKilograms,
                    cargoMassKilograms))
            {
                failure = "Fuel, fluid and payload values must be finite and non-negative.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public struct VehicleScriptedInputFrame
    {
        [SerializeField, Min(0f)] private float timeSeconds;
        [SerializeField, Range(0f, 1f)] private float throttle01;
        [SerializeField, Range(0f, 1f)] private float clutchPedal01;
        [SerializeField, Range(0f, 1f)] private float brake01;
        [SerializeField, Range(-1f, 1f)] private float steeringMinusOneToOne;
        [SerializeField] private bool ignitionOn;
        [SerializeField] private bool starterRequested;
        [SerializeField] private bool gearChangeRequested;
        [SerializeField] private int requestedGear;

        public float TimeSeconds => timeSeconds;
        public float Throttle01 => throttle01;
        public float ClutchPedal01 => clutchPedal01;
        public float Brake01 => brake01;
        public float SteeringMinusOneToOne => steeringMinusOneToOne;
        public bool IgnitionOn => ignitionOn;
        public bool StarterRequested => starterRequested;
        public bool GearChangeRequested => gearChangeRequested;
        public int RequestedGear => requestedGear;

        public VehicleScriptedInputFrame(
            float valueTimeSeconds,
            float valueThrottle01,
            float valueClutchPedal01,
            float valueBrake01,
            float valueSteeringMinusOneToOne,
            bool valueIgnitionOn,
            bool valueStarterRequested,
            bool valueGearChangeRequested,
            int valueRequestedGear)
        {
            timeSeconds = valueTimeSeconds;
            throttle01 = valueThrottle01;
            clutchPedal01 = valueClutchPedal01;
            brake01 = valueBrake01;
            steeringMinusOneToOne = valueSteeringMinusOneToOne;
            ignitionOn = valueIgnitionOn;
            starterRequested = valueStarterRequested;
            gearChangeRequested = valueGearChangeRequested;
            requestedGear = valueRequestedGear;
        }

        public VehicleInputState ToInputState()
        {
            return new VehicleInputState(
                throttle01,
                clutchPedal01,
                brake01,
                steeringMinusOneToOne,
                ignitionOn,
                starterRequested,
                gearChangeRequested,
                requestedGear);
        }

        public bool Validate(out string failure)
        {
            if (!VehicleCalibrationValidation.IsFiniteNonNegative(timeSeconds) ||
                !VehicleCalibrationValidation.IsFiniteInRange(throttle01, 0f, 1f) ||
                !VehicleCalibrationValidation.IsFiniteInRange(clutchPedal01, 0f, 1f) ||
                !VehicleCalibrationValidation.IsFiniteInRange(brake01, 0f, 1f) ||
                !VehicleCalibrationValidation.IsFiniteInRange(steeringMinusOneToOne, -1f, 1f))
            {
                failure = "Scripted input frame contains invalid numeric values.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class VehicleCalibrationInputConfiguration
    {
        [SerializeField] private VehicleCalibrationInputSource source = VehicleCalibrationInputSource.Scripted;
        [SerializeField] private string deviceOrScriptId = string.Empty;
        [SerializeField] private VehicleScriptedInputFrame[] scriptedFrames = Array.Empty<VehicleScriptedInputFrame>();

        public VehicleCalibrationInputSource Source => source;
        public string DeviceOrScriptId => deviceOrScriptId;
        public VehicleScriptedInputFrame[] ScriptedFrames => scriptedFrames;

        public VehicleCalibrationInputConfiguration()
        {
        }

        public VehicleCalibrationInputConfiguration(
            VehicleCalibrationInputSource valueSource,
            string valueDeviceOrScriptId,
            VehicleScriptedInputFrame[] valueScriptedFrames)
        {
            source = valueSource;
            deviceOrScriptId = valueDeviceOrScriptId ?? string.Empty;
            scriptedFrames = valueScriptedFrames ?? Array.Empty<VehicleScriptedInputFrame>();
        }

        public bool Validate(out string failure)
        {
            if (source == VehicleCalibrationInputSource.Unknown || string.IsNullOrWhiteSpace(deviceOrScriptId))
            {
                failure = "Input source and device/script ID must be explicit.";
                return false;
            }

            if (scriptedFrames == null)
            {
                failure = "Scripted input frames are null.";
                return false;
            }

            if (source == VehicleCalibrationInputSource.Scripted && scriptedFrames.Length == 0)
            {
                failure = "A scripted input configuration requires at least one frame.";
                return false;
            }

            float previousTime = -1f;
            for (int index = 0; index < scriptedFrames.Length; index++)
            {
                if (!scriptedFrames[index].Validate(out failure))
                {
                    failure = $"Invalid scripted input frame at index {index}: {failure}";
                    return false;
                }

                if (scriptedFrames[index].TimeSeconds < previousTime)
                {
                    failure = "Scripted input frames must be ordered by non-decreasing time.";
                    return false;
                }

                previousTime = scriptedFrames[index].TimeSeconds;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class VehicleCalibrationRuntimeConfiguration
    {
        [SerializeField, Min(0.0001f)] private float fixedTimestepSeconds = 0.02f;
        [SerializeField, Range(1, 32)] private int simulationSubsteps = 4;
        [SerializeField] private string unityVersion = string.Empty;
        [SerializeField] private string physXVersion = string.Empty;
        [SerializeField] private VehicleCalibrationExecutionMode executionMode;
        [SerializeField] private string operatingSystem = string.Empty;
        [SerializeField] private string processor = string.Empty;
        [SerializeField] private string graphicsDevice = string.Empty;
        [SerializeField] private int randomSeed;
        [SerializeField] private bool usesSeed;

        public float FixedTimestepSeconds => fixedTimestepSeconds;
        public int SimulationSubsteps => simulationSubsteps;
        public string UnityVersion => unityVersion;
        public string PhysXVersion => physXVersion;
        public VehicleCalibrationExecutionMode ExecutionMode => executionMode;
        public string OperatingSystem => operatingSystem;
        public string Processor => processor;
        public string GraphicsDevice => graphicsDevice;
        public int RandomSeed => randomSeed;
        public bool UsesSeed => usesSeed;

        public VehicleCalibrationRuntimeConfiguration()
        {
        }

        public VehicleCalibrationRuntimeConfiguration(
            float valueFixedTimestepSeconds,
            int valueSimulationSubsteps,
            string valueUnityVersion,
            string valuePhysXVersion,
            VehicleCalibrationExecutionMode valueExecutionMode,
            string valueOperatingSystem,
            string valueProcessor,
            string valueGraphicsDevice,
            int valueRandomSeed,
            bool valueUsesSeed)
        {
            fixedTimestepSeconds = valueFixedTimestepSeconds;
            simulationSubsteps = valueSimulationSubsteps;
            unityVersion = valueUnityVersion ?? string.Empty;
            physXVersion = valuePhysXVersion ?? string.Empty;
            executionMode = valueExecutionMode;
            operatingSystem = valueOperatingSystem ?? string.Empty;
            processor = valueProcessor ?? string.Empty;
            graphicsDevice = valueGraphicsDevice ?? string.Empty;
            randomSeed = valueRandomSeed;
            usesSeed = valueUsesSeed;
        }

        public bool Validate(out string failure)
        {
            if (!VehicleCalibrationValidation.IsFinitePositive(fixedTimestepSeconds) ||
                simulationSubsteps < 1 || simulationSubsteps > 32)
            {
                failure = "Fixed timestep or substep count is invalid.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(unityVersion) ||
                string.IsNullOrWhiteSpace(physXVersion) ||
                executionMode == VehicleCalibrationExecutionMode.Unknown ||
                string.IsNullOrWhiteSpace(operatingSystem) ||
                string.IsNullOrWhiteSpace(processor) ||
                string.IsNullOrWhiteSpace(graphicsDevice))
            {
                failure = "Unity, PhysX, execution mode and hardware fields must be explicit.";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public sealed class VehicleCalibrationEnvironment
    {
        [SerializeField] private string vehicleConfigurationId = string.Empty;
        [SerializeField] private string vehicleConfigurationRevision = string.Empty;
        [SerializeField] private string sceneOrRouteId = string.Empty;
        [SerializeField] private VehicleCalibrationMassConfiguration mass = new VehicleCalibrationMassConfiguration();
        [SerializeField] private VehicleCalibrationTireConfiguration[] tires = Array.Empty<VehicleCalibrationTireConfiguration>();
        [SerializeField] private VehicleCalibrationSurfaceConfiguration surface = new VehicleCalibrationSurfaceConfiguration();
        [SerializeField] private VehicleCalibrationWeatherConfiguration weather = new VehicleCalibrationWeatherConfiguration();
        [SerializeField] private VehicleCalibrationLoadConfiguration load = new VehicleCalibrationLoadConfiguration();
        [SerializeField] private VehicleCalibrationInputConfiguration input = new VehicleCalibrationInputConfiguration();
        [SerializeField] private VehicleCalibrationRuntimeConfiguration runtime = new VehicleCalibrationRuntimeConfiguration();

        public string VehicleConfigurationId => vehicleConfigurationId;
        public string VehicleConfigurationRevision => vehicleConfigurationRevision;
        public string SceneOrRouteId => sceneOrRouteId;
        public VehicleCalibrationMassConfiguration Mass => mass;
        public VehicleCalibrationTireConfiguration[] Tires => tires;
        public VehicleCalibrationSurfaceConfiguration Surface => surface;
        public VehicleCalibrationWeatherConfiguration Weather => weather;
        public VehicleCalibrationLoadConfiguration Load => load;
        public VehicleCalibrationInputConfiguration Input => input;
        public VehicleCalibrationRuntimeConfiguration Runtime => runtime;

        public VehicleCalibrationEnvironment()
        {
        }

        public VehicleCalibrationEnvironment(
            string valueVehicleConfigurationId,
            string valueVehicleConfigurationRevision,
            string valueSceneOrRouteId,
            VehicleCalibrationMassConfiguration valueMass,
            VehicleCalibrationTireConfiguration[] valueTires,
            VehicleCalibrationSurfaceConfiguration valueSurface,
            VehicleCalibrationWeatherConfiguration valueWeather,
            VehicleCalibrationLoadConfiguration valueLoad,
            VehicleCalibrationInputConfiguration valueInput,
            VehicleCalibrationRuntimeConfiguration valueRuntime)
        {
            vehicleConfigurationId = valueVehicleConfigurationId ?? string.Empty;
            vehicleConfigurationRevision = valueVehicleConfigurationRevision ?? string.Empty;
            sceneOrRouteId = valueSceneOrRouteId ?? string.Empty;
            mass = valueMass;
            tires = valueTires ?? Array.Empty<VehicleCalibrationTireConfiguration>();
            surface = valueSurface;
            weather = valueWeather;
            load = valueLoad;
            input = valueInput;
            runtime = valueRuntime;
        }

        public bool Validate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(vehicleConfigurationId) ||
                string.IsNullOrWhiteSpace(vehicleConfigurationRevision) ||
                string.IsNullOrWhiteSpace(sceneOrRouteId))
            {
                failure = "Vehicle configuration, revision and route IDs must be explicit.";
                return false;
            }

            if (mass == null)
            {
                failure = "Mass configuration is null.";
                return false;
            }

            if (!mass.Validate(out failure))
            {
                failure = $"Invalid mass configuration: {failure}";
                return false;
            }

            if (tires == null || tires.Length == 0)
            {
                failure = "At least one tire configuration is required.";
                return false;
            }

            for (int index = 0; index < tires.Length; index++)
            {
                if (tires[index] == null)
                {
                    failure = $"Tire configuration at index {index} is null.";
                    return false;
                }

                if (!tires[index].Validate(out failure))
                {
                    failure = $"Invalid tire configuration at index {index}: {failure}";
                    return false;
                }
            }

            if (surface == null || weather == null || load == null || input == null || runtime == null)
            {
                failure = "One or more environment configuration groups are null.";
                return false;
            }

            if (!surface.Validate(out failure) ||
                !weather.Validate(out failure) ||
                !load.Validate(out failure) ||
                !input.Validate(out failure) ||
                !runtime.Validate(out failure))
            {
                failure = $"Invalid environment configuration: {failure}";
                return false;
            }

            failure = string.Empty;
            return true;
        }
    }

    [Serializable]
    public struct VehicleStatisticalSummary
    {
        [SerializeField] private int sampleCount;
        [SerializeField] private int validSampleCount;
        [SerializeField] private int invalidSampleCount;
        [SerializeField] private float mean;
        [SerializeField] private float median;
        [SerializeField] private float minimum;
        [SerializeField] private float maximum;
        [SerializeField] private float standardDeviation;
        [SerializeField] private float lastValidSample;
        [SerializeField] private float coefficientOfVariation;
        [SerializeField] private bool hasCoefficientOfVariation;

        public int SampleCount => sampleCount;
        public int ValidSampleCount => validSampleCount;
        public int InvalidSampleCount => invalidSampleCount;
        public float Mean => mean;
        public float Median => median;
        public float Minimum => minimum;
        public float Maximum => maximum;
        public float StandardDeviation => standardDeviation;
        public float LastValidSample => lastValidSample;
        public float CoefficientOfVariation => coefficientOfVariation;
        public bool HasCoefficientOfVariation => hasCoefficientOfVariation;
        public bool HasValidSamples => validSampleCount > 0;
        public bool HasInvalidSamples => invalidSampleCount > 0;

        public VehicleStatisticalSummary(
            int valueSampleCount,
            int valueValidSampleCount,
            int valueInvalidSampleCount,
            float valueMean,
            float valueMedian,
            float valueMinimum,
            float valueMaximum,
            float valueStandardDeviation,
            float valueLastValidSample,
            float valueCoefficientOfVariation,
            bool valueHasCoefficientOfVariation)
        {
            sampleCount = valueSampleCount;
            validSampleCount = valueValidSampleCount;
            invalidSampleCount = valueInvalidSampleCount;
            mean = valueMean;
            median = valueMedian;
            minimum = valueMinimum;
            maximum = valueMaximum;
            standardDeviation = valueStandardDeviation;
            lastValidSample = valueLastValidSample;
            coefficientOfVariation = valueCoefficientOfVariation;
            hasCoefficientOfVariation = valueHasCoefficientOfVariation;
        }

        public float Select(VehicleMetricStatistic statistic)
        {
            return statistic switch
            {
                VehicleMetricStatistic.Median => median,
                VehicleMetricStatistic.Minimum => minimum,
                VehicleMetricStatistic.Maximum => maximum,
                VehicleMetricStatistic.StandardDeviation => standardDeviation,
                VehicleMetricStatistic.LastValidSample => lastValidSample,
                _ => mean
            };
        }

        public bool Validate(out string failure)
        {
            if (sampleCount < 0 || validSampleCount < 0 || invalidSampleCount < 0 ||
                validSampleCount + invalidSampleCount != sampleCount)
            {
                failure = "Statistical sample counts are inconsistent.";
                return false;
            }

            if (!VehicleCalibrationValidation.AreFinite(
                    mean,
                    median,
                    minimum,
                    maximum,
                    standardDeviation,
                    lastValidSample,
                    coefficientOfVariation) ||
                standardDeviation < 0f || coefficientOfVariation < 0f)
            {
                failure = "Statistical summary contains invalid numeric values.";
                return false;
            }

            if (validSampleCount > 0 && minimum > maximum)
            {
                failure = "Statistical summary minimum exceeds maximum.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public static VehicleStatisticalSummary Empty(int invalidSamples = 0)
        {
            return new VehicleStatisticalSummary(
                invalidSamples,
                0,
                invalidSamples,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                false);
        }
    }

    internal static class VehicleCalibrationValidation
    {
        public static bool IsFinite(float value)
        {
            return VehicleSimulationMath.IsFinite(value);
        }

        public static bool IsFinite(Vector3 value)
        {
            return VehicleSimulationMath.IsFinite(value);
        }

        public static bool IsFiniteNonNegative(float value)
        {
            return IsFinite(value) && value >= 0f;
        }

        public static bool IsFinitePositive(float value)
        {
            return IsFinite(value) && value > 0f;
        }

        public static bool IsFiniteInRange(float value, float minimum, float maximum)
        {
            return IsFinite(value) && value >= minimum && value <= maximum;
        }

        public static bool AreFinite(params float[] values)
        {
            if (values == null)
            {
                return false;
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (!IsFinite(values[index]))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool AreFiniteNonNegative(params float[] values)
        {
            if (values == null)
            {
                return false;
            }

            for (int index = 0; index < values.Length; index++)
            {
                if (!IsFiniteNonNegative(values[index]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
