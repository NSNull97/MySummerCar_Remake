using System;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.VehicleSimulation
{
    public sealed class SatsumaOperatingModelTests
    {
        private VehicleSimulationConfig config;
        [SetUp] public void SetUp()
        { config = ScriptableObject.CreateInstance<VehicleSimulationConfig>(); config.ApplyProvisionalPrototypeDefaults(); }
        [TearDown] public void TearDown() => UnityEngine.Object.DestroyImmediate(config);

        [TestCase(SatsumaServiceFluid.MotorOil, 3f)]
        [TestCase(SatsumaServiceFluid.Coolant, 5.4f)]
        [TestCase(SatsumaServiceFluid.BrakeFront, 1f)]
        [TestCase(SatsumaServiceFluid.BrakeRear, 1f)]
        [TestCase(SatsumaServiceFluid.Clutch, 0.5f)]
        public void Filling_IsFiniteAndBoundedByReviewedCapacity(SatsumaServiceFluid fluid, float capacity)
        {
            VehicleSimulationRoot root = Create(out _, out _);
            VehicleSimulationStateDto dto = root.State.CaptureDto(); dto.oilLiters = dto.coolantLiters = 0f;
            Assert.That(root.TryRestoreState(dto, out string failure), Is.True, failure);
            Assert.That(root.State.AddServiceFluid(fluid, float.NaN), Is.Zero);
            Assert.That(root.State.AddServiceFluid(fluid, -1f), Is.Zero);
            Assert.That(root.State.AddServiceFluid(fluid, 100f), Is.EqualTo(capacity));
            Assert.That(root.State.AddServiceFluid(fluid, 1f), Is.Zero);
            Assert.That(root.State.GetServiceFluidLiters(fluid), Is.EqualTo(capacity));
        }

        [Test]
        public void LegacyRestore_PreservesExistingFluidsAndDoesNotRefillNewReservoirs()
        {
            VehicleSimulationRoot root = Create(out _, out _);
            root.State.AddServiceFluid(SatsumaServiceFluid.BrakeFront, 1f);
            VehicleSimulationStateDto dto = root.State.CaptureDto(); dto.hasSatsumaOperatingState = false;
            dto.oilLiters = 3.5f; dto.coolantLiters = 2.17f; dto.fuelLiters = 0.1f;
            dto.batteryVoltage = 10.4f; dto.engineTemperatureCelsius = 61f;
            // An inline DTO can survive JsonUtility even when the presence bit is absent.
            Assert.That(root.TryRestoreState(dto, out string failure), Is.True, failure);
            Assert.That(root.State.OilLiters, Is.EqualTo(3.5f));
            Assert.That(root.State.CoolantLiters, Is.EqualTo(2.17f));
            Assert.That(root.State.BatteryVoltage, Is.EqualTo(10.4f));
            Assert.That(root.State.EngineTemperatureCelsius, Is.EqualTo(61f));
            Assert.That(root.State.SatsumaOperating.BrakeFrontLiters, Is.Zero);
            Assert.That(root.State.AddServiceFluid(SatsumaServiceFluid.MotorOil, 1f), Is.Zero);
            Assert.That(root.State.OilLiters, Is.EqualTo(3.5f), "Legacy surplus is not silently deleted.");
        }

        [Test]
        public void NewState_RoundTripsWithoutAliasing_AndGenericHostRejectsIt()
        {
            VehicleSimulationRoot root = Create(out _, out _);
            root.State.AddServiceFluid(SatsumaServiceFluid.Clutch, 0.3f);
            VehicleSimulationStateDto dto = root.State.CaptureDto(); dto.satsumaOperatingState.oilContaminationPercent = 47f;
            Assert.That(root.TryRestoreState(dto, out string failure), Is.True, failure);
            dto.satsumaOperatingState.clutchLiters = 0f;
            Assert.That(root.State.SatsumaOperating.ClutchLiters, Is.EqualTo(0.3f));
            var generic = new VehicleSimulationRoot(config, new Wheels(), new Prerequisites());
            Assert.That(generic.CanRestoreState(root.State.CaptureDto(), out failure), Is.False);
            Assert.That(failure, Does.Contain("does not support"));
            Assert.That(generic.State.AddServiceFluid(SatsumaServiceFluid.MotorOil, 1f), Is.Zero);
            root.Reset();
            Assert.That(root.State.SatsumaOperating, Is.Not.Null);
            Assert.That(root.State.SatsumaOperating.ClutchLiters, Is.Zero);
        }

        [TestCase(2f, 1)] [TestCase(float.NaN, 1)] [TestCase(0.3f, 88)]
        public void InvalidExtension_IsRejectedBeforeStateMutation(float amount, int schema)
        {
            VehicleSimulationRoot root = Create(out _, out _);
            string before = JsonUtility.ToJson(root.State.CaptureDto());
            VehicleSimulationStateDto dto = root.State.CaptureDto();
            dto.satsumaOperatingState.brakeFrontLiters = amount; dto.satsumaOperatingState.schemaVersion = schema;
            Assert.That(root.TryRestoreState(dto, out _), Is.False);
            Assert.That(JsonUtility.ToJson(root.State.CaptureDto()), Is.EqualTo(before));
        }

        [Test]
        public void ColdStart_TakesLongerThanHotStart_AndKeyReleaseResetsProgress()
        {
            VehicleSimulationRoot cold = Create(out _, out _); Seed(cold, 20f);
            VehicleSimulationRoot hot = Create(out _, out _); Seed(hot, 90f);
            float coldSeconds = Start(cold), hotSeconds = Start(hot);
            Assert.That(coldSeconds, Is.GreaterThan(3.9f)); Assert.That(hotSeconds, Is.LessThan(1.5f));
            cold.Tick(0.02f, Input(starter: false));
            Assert.That(cold.State.SatsumaOperating.CrankingSeconds, Is.Zero);
        }

        [Test]
        public void Choke_UsesReviewedClampedMultiplier_WithoutDisabledRichHardVeto()
        {
            VehicleSimulationRoot root = Create(out Source source, out _); Seed(root, 20f, 900f);
            SatsumaEngineOperatingPoint noChoke = Evaluate(root, source);
            source.Choke = 1f;
            SatsumaEngineOperatingPoint choke = Evaluate(root, source);
            Assert.That(choke.AirFuelRatio, Is.EqualTo((14.9f / 0.9f / 22f) * 14.7f).Within(0.001f));
            Assert.That(choke.AirFuelRatio, Is.LessThan(noChoke.AirFuelRatio));
            Assert.That(choke.IdleRpm, Is.GreaterThan(noChoke.IdleRpm));
            Assert.That(choke.CombustionAllowed, Is.True);
        }

        [Test]
        public void SparkAndValves_ChangeTorqueAndCausalSymptoms()
        {
            VehicleSimulationRoot root = Create(out Source source, out _); Seed(root, 80f, 1800f);
            float healthy = Evaluate(root, source).TorqueScale;
            source.Spark = 16f; SatsumaEngineOperatingPoint advanced = Evaluate(root, source);
            Assert.That(advanced.TorqueScale, Is.EqualTo(healthy * 0.75f).Within(0.0001f));
            Assert.That(advanced.Knock01, Is.GreaterThan(0f));
            source.Spark = 4f;
            Assert.That(Evaluate(root, source).TorqueScale, Is.EqualTo(healthy * 0.3f).Within(0.0001f));
            source.Spark = 15f; source.Intake = new Vector4(1f, 7f, 7f, 7f);
            SatsumaEngineOperatingPoint valve = Evaluate(root, source);
            Assert.That(valve.TorqueScale, Is.LessThan(healthy)); Assert.That(valve.ValveNoise01, Is.EqualTo(0.125f));
            Assert.That(valve.IntakeSpit01, Is.GreaterThan(0f)); Assert.That(valve.ExhaustBackfire01, Is.Zero);
        }

        [Test]
        public void DryEngine_CanStart_ButLosesPressureAndHeatsAndWearsFaster()
        {
            VehicleSimulationRoot wet = Create(out Source wetSource, out _); Seed(wet, 90f, 2000f);
            VehicleSimulationRoot dry = Create(out Source drySource, out _); Seed(dry, 90f, 2000f, dry: true);
            for (int i = 0; i < 200; i++)
            { wet.Tick(0.02f, Input(throttle: 0.35f)); dry.Tick(0.02f, Input(throttle: 0.35f)); }
            Assert.That(dry.Prerequisites.CanRun, Is.True);
            Assert.That(dry.State.SatsumaOperating.OilPressureBar, Is.Zero);
            Assert.That(wet.State.SatsumaOperating.OilPressureBar, Is.GreaterThan(0.8f));
            Assert.That(dry.State.EngineTemperatureCelsius, Is.GreaterThan(wet.State.EngineTemperatureCelsius));
            Assert.That(drySource.PistonWear, Is.GreaterThan(wetSource.PistonWear * 10f));
            VehicleSimulationRoot freshDry = Create(out _, out _); Seed(freshDry, 20f, dry: true);
            Assert.That(Start(freshDry), Is.LessThan(8f));
        }

        [Test]
        public void Alternator_CanSustainRunningWithoutBattery_ButCannotCrankIt()
        {
            VehicleSimulationRoot root = Create(out Source source, out Prerequisites prerequisites); Seed(root, 80f, 2500f);
            source.Battery = false; prerequisites.Failures = VehicleSimulationPrerequisiteFailure.BatteryMissing;
            for (int i = 0; i < 50; i++) root.Tick(0.02f, Input(throttle: 0.3f));
            Assert.That(root.Prerequisites.CanCrank, Is.False); Assert.That(root.Prerequisites.CanRun, Is.True);
            Assert.That(root.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running));
            source.Belt = false;
            root.Tick(0.02f, Input()); Assert.That(root.Prerequisites.CanRun, Is.False);
            Assert.That(root.State.EngineTorqueNewtonMeters, Is.Zero);
        }

        [Test]
        public void Fan_IsElectricalAndTemperatureDriven_NotBeltDriven()
        {
            VehicleSimulationRoot root = Create(out Source source, out _); Seed(root, 105f);
            source.Belt = false; root.Tick(0.02f, Input());
            Assert.That(root.State.SatsumaOperating.RadiatorFanRunning, Is.True);
            Assert.That(root.SatsumaOperatingModel.LastPoint.BeltDrive01, Is.Zero);
            source.FanWired = false; root.Tick(0.02f, Input());
            Assert.That(root.State.SatsumaOperating.RadiatorFanRunning, Is.False);
        }

        [Test]
        public void HydraulicReservoirs_AreIndependent_AndDryClutchCannotDisengage()
        {
            VehicleSimulationRoot root = Create(out _, out _); Seed(root, 80f, 2000f);
            root.State.AddServiceFluid(SatsumaServiceFluid.BrakeFront, 1f);
            SatsumaOperatingInputs conditions = new Source { Root = root }.CaptureConditions();
            SatsumaEngineOperatingPoint point = root.SatsumaOperatingModel.Evaluate(root.State, Input(), conditions, 0.02f);
            Assert.That(point.FrontBrakeEfficiency, Is.EqualTo(1f)); Assert.That(point.RearBrakeEfficiency, Is.Zero);
            Assert.That(point.ClutchPedalEfficiency, Is.Zero);
            root.Tick(0.02f, new VehicleInputState(0.2f, 1f, 1f, 0f, true, false, true, 1));
            Assert.That(root.State.ClutchEngagement01, Is.EqualTo(1f));
        }

        [Test]
        public void MissingFuel_RemainsACombustionGate_AndBadInputsAreRejected()
        {
            VehicleSimulationRoot root = Create(out Source source, out _);
            VehicleSimulationStateDto dto = root.State.CaptureDto(); dto.fuelLiters = 0f;
            Assert.That(root.TryRestoreState(dto, out _), Is.True);
            for (int i = 0; i < 300; i++) root.Tick(0.02f, Input(starter: true));
            Assert.That(root.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Cranking));
            Assert.That(source.PistonWear, Is.Zero);
            source.Spark = float.NaN;
            Assert.Throws<ArgumentException>(() => root.Tick(0.02f, Input()));
        }

        [Test]
        public void Autocharge_IsExplicitAndCannotCreateAnAbsentBattery()
        {
            VehicleSimulationRoot root = Create(out Source source, out _);
            VehicleSimulationStateDto dto = root.State.CaptureDto(); dto.batteryVoltage = 5f;
            Assert.That(root.TryRestoreState(dto, out _), Is.True);
            source.Battery = false; source.Autocharge = true;
            root.Tick(0.02f, Input()); Assert.That(root.State.BatteryVoltage, Is.EqualTo(5f));
            source.Battery = true; root.Tick(0.02f, Input());
            Assert.That(root.State.BatteryVoltage, Is.EqualTo(config.SupportSystems.NominalBatteryVoltage));
        }

        [Test]
        public void FixedStepVariants_StayWithinTolerance()
        {
            VehicleSimulationRoot a = Create(out _, out _), b = Create(out _, out _);
            Seed(a, 20f); Seed(b, 20f);
            for (int i = 0; i < 500; i++) a.Tick(0.02f, Input(starter: true, throttle: 0.1f));
            for (int i = 0; i < 1000; i++) b.Tick(0.01f, Input(starter: true, throttle: 0.1f));
            Assert.That(a.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running));
            Assert.That(b.State.EngineStatus, Is.EqualTo(a.State.EngineStatus));
            Assert.That(a.State.EngineRpm, Is.EqualTo(b.State.EngineRpm).Within(5f));
            Assert.That(a.State.EngineTemperatureCelsius, Is.EqualTo(b.State.EngineTemperatureCelsius).Within(0.1f));
        }

        private VehicleSimulationRoot Create(out Source source, out Prerequisites prerequisites)
        {
            source = new Source(); prerequisites = new Prerequisites();
            var root = new VehicleSimulationRoot(config, new Wheels(), prerequisites, source);
            source.Root = root; return root;
        }
        private static SatsumaEngineOperatingPoint Evaluate(VehicleSimulationRoot root, Source source) =>
            root.SatsumaOperatingModel.Evaluate(root.State, Input(), source.CaptureConditions(), 0.02f);
        private static void Seed(VehicleSimulationRoot root, float temperature, float rpm = 0f, bool dry = false)
        {
            VehicleSimulationStateDto dto = root.State.CaptureDto(); dto.engineTemperatureCelsius = temperature;
            dto.coolantTemperatureCelsius = temperature; dto.engineRpm = rpm;
            dto.engineStatus = rpm > 0f ? VehicleEngineStatus.Running : VehicleEngineStatus.Off;
            dto.oilLiters = dry ? 0f : 3f; dto.coolantLiters = dry ? 0f : 5.4f;
            Assert.That(root.TryRestoreState(dto, out string failure), Is.True, failure);
        }
        private static float Start(VehicleSimulationRoot root)
        {
            for (int i = 1; i <= 500; i++)
            { root.Tick(0.02f, Input(starter: true)); if (root.State.EngineStatus == VehicleEngineStatus.Running) return i * 0.02f; }
            Assert.Fail("Engine did not catch in ten simulated seconds."); return 10f;
        }
        private static VehicleInputState Input(bool starter = false, float throttle = 0f) =>
            new(throttle, 1f, 0f, 0f, true, starter, false, 0);
        private sealed class Prerequisites : IVehicleSimulationPrerequisiteSource
        {
            public VehicleSimulationPrerequisiteFailure Failures;
            public void Evaluate(in VehicleInputState input, ref VehicleSimulationPrerequisites result)
            { result.Reset(); result.UseIndependentCrankingRequirements(); result.Add(Failures); }
        }
        private sealed class Source : ISatsumaOperatingConditionSource
        {
            public VehicleSimulationRoot Root;
            public float Choke, Spark = 15f, PistonWear;
            public bool Battery = true, Belt = true, FanWired = true, Autocharge;
            public Vector4 Intake = Vector4.one * 7f;
            public SatsumaOperatingInputs CaptureConditions() => new(
                new SatsumaTuningInputs(15f, Choke, Spark, 0f, 7f, Intake, Vector4.one * 6f),
                new SatsumaAncillaryInputs(Battery, Battery && Root.State.BatteryVoltage > 9.7f,
                    true, true, FanWired, Belt, true, true),
                new SatsumaFluidHardwareInputs(true, true, 8f, 64f, 48f, true, true, true, 40f,
                    true, true, true, true, true), 15, 100f, 20f, Autocharge);
            public void ApplyWear(in SatsumaWearDelta wear) => PistonWear += wear.Piston;
        }
        private sealed class Wheels : IWheelPhysicsBackend
        {
            public int WheelCount => 4;
            public float VehicleSpeedMetersPerSecond => 0f;
            public void Sample(float dt, WheelPhysicsSample[] destination)
            { for (int i = 0; i < destination.Length; i++) destination[i] = WheelPhysicsSample.NoContact; }
            public void Apply(float dt, WheelPhysicsCommand[] commands) { }
            public void Reset() { }
        }
    }
}
