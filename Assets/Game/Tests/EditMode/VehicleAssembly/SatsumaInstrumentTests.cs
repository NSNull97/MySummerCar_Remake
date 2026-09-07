using System;
using System.Linq;
using MSC.Core.Time;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;
using Fixture = MSC.Tests.EditMode.VehicleAssembly.SatsumaOperatingSourceTests.Fixture;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaInstrumentTests
    {
        [Test]
        public void DonorUnitsAndZeroesAreNotConfusedWithGpsSpeedOrTenthsOfKilometers()
        {
            Assert.That(SatsumaInstrumentPresenter.SpeedAngle(100f), Is.EqualTo(-168.75f));
            Assert.That(SatsumaInstrumentPresenter.SpeedAngle(-10f), Is.Zero);
            Assert.That(SatsumaInstrumentPresenter.SpeedAngle(300f), Is.EqualTo(-421.875f));
            Assert.That(SatsumaInstrumentPresenter.CoolantAngle(90f), Is.EqualTo(-41.49f).Within(.0001f));
            Assert.That(SatsumaInstrumentPresenter.FuelAngle(36f), Is.EqualTo(-59.76f).Within(.0001f));
            Assert.That(SatsumaInstrumentPresenter.ClockMinuteAngle(13.5 * 3600), Is.EqualTo(-180f));
            Assert.That(SatsumaInstrumentPresenter.ClockHourAngle(13.5 * 3600), Is.EqualTo(-45f));
            Assert.That(SatsumaInstrumentPresenter.OdometerAngle(123456.5,0), Is.EqualTo(-234f));
            Assert.That(SatsumaInstrumentPresenter.OdometerAngle(123456.5,5), Is.EqualTo(-36f));
        }

        [Test]
        public void CanonicalNeedlesUseIndependentCircuitsAndNeverChangePhysicalPartsOrSharedMaterials()
        {
            using var f = new Fixture();
            var p = f.Root.GetComponent<SatsumaInstrumentPresenter>(); Assert.That(p, Is.Not.Null);
            Assert.That(Phase1SatsumaInstrumentAuthoring.ApplyToInstance(f.Assembly), Is.Zero);
            var time = new GameTimeService(); time.SetTimeScale(1f); p.BindGameTime(time);
            var controls = f.Root.GetComponent<SatsumaDashboardControlsController>();
            var key = f.Root.GetComponent<SatsumaIgnitionController>(); key.RestorePersistentState(true);
            var dto = f.Host.State.CaptureDto(); dto.coolantTemperatureCelsius = 90f; dto.fuelLiters = 30f;
            dto.wheels[0].AngularSpeedRadiansPerSecond = dto.wheels[1].AngularSpeedRadiansPerSecond = 100f;
            Assert.That(f.Host.TryRestoreSimulationState(dto, out _), Is.True);
            string before = JsonUtility.ToJson(f.Assembly.CaptureSaveData());
            string[] materials = p.Illumination.Select(l => UnityEditor.EditorJsonUtility.ToJson(l.Renderer.sharedMaterial)).ToArray();
            SatsumaInstrumentNeedle Needle(SatsumaInstrumentKind k) => p.Needles.Single(n => n.Kind == k);
            void Angle(SatsumaInstrumentKind k, float value)
            {
                var n = Needle(k); Assert.That(Quaternion.Angle(n.Leaf.localRotation, n.ZeroRotation * Quaternion.AngleAxis(value, Vector3.up)), Is.LessThan(.01f), k.ToString());
            }
            p.RefreshOutputs(); Angle(SatsumaInstrumentKind.Speed, -168.75f); Angle(SatsumaInstrumentKind.Coolant, -41.49f); Angle(SatsumaInstrumentKind.Fuel, -49.8f);
            Assert.That(p.Warnings.Single(w => w.Kind == SatsumaInstrumentWarning.OilPressure).Visual.activeSelf, Is.True);
            Assert.That(controls.TryCycleLights(), Is.True); p.RefreshOutputs();
            var propertyBlock = new MaterialPropertyBlock(); p.Illumination[0].Renderer.GetPropertyBlock(propertyBlock);
            Assert.That(propertyBlock.GetColor("_EmissiveColor").maxColorComponent, Is.GreaterThan(0f));
            key.RestorePersistentState(false); p.RefreshOutputs(); Angle(SatsumaInstrumentKind.Fuel,0); Angle(SatsumaInstrumentKind.Coolant,-41.49f);
            Quaternion clockBefore = Needle(SatsumaInstrumentKind.ClockMinute).Leaf.localRotation;
            double secondsBefore = time.Snapshot.SecondsOfDay; time.Advance(60f); p.RefreshOutputs();
            Assert.That(Quaternion.Angle(clockBefore, Needle(SatsumaInstrumentKind.ClockMinute).Leaf.localRotation),
                Is.EqualTo((time.Snapshot.SecondsOfDay - secondsBefore) / 10d).Within(.01f), "Clock follows accelerated game time without ACC.");
            var power = controls.Electrical.CaptureSaveData();
            power.installedConnectionIds = power.installedConnectionIds.Where(id => id != SatsumaElectricalConnection.Dash1.ToString()).ToArray();
            Assert.That(controls.Electrical.TryRestore(power, out _), Is.True); p.RefreshOutputs();
            Angle(SatsumaInstrumentKind.Speed,-168.75f); Angle(SatsumaInstrumentKind.Coolant,0f);
            clockBefore = Needle(SatsumaInstrumentKind.ClockMinute).Leaf.localRotation; time.Advance(60); p.RefreshOutputs();
            Assert.That(Needle(SatsumaInstrumentKind.ClockMinute).Leaf.localRotation, Is.EqualTo(clockBefore));
            for (int i = 0; i < materials.Length; i++) Assert.That(UnityEditor.EditorJsonUtility.ToJson(p.Illumination[i].Renderer.sharedMaterial), Is.EqualTo(materials[i]));
            Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(before));
        }

        [Test]
        public void MechanicalOdometerAccumulatesOncePerTickWithIgnitionOffAndPersistsAcrossRollover()
        {
            using var f = new Fixture();
            var wheels = new InstrumentDistanceWheels { Speed = -10f };
            var root = new VehicleSimulationRoot(f.Host.Config, wheels, f.Prerequisites, f.Source);
            var dto = f.Host.State.CaptureDto(); dto.satsumaOperatingState.odometerPartialMeters = 9999;
            Assert.That(root.State.TryRestoreDto(dto, f.Host.Config), Is.True);
            for (int i = 0; i < 100; i++) root.Tick(.02f, VehicleInputState.Neutral(false));
            Assert.That(root.State.SatsumaOperating.OdometerTenKilometerUnits, Is.EqualTo(10001));
            Assert.That(root.State.SatsumaOperating.OdometerPartialMeters, Is.EqualTo(19d).Within(.00001d));
            Assert.That(root.State.SatsumaOperating.OdometerKilometers, Is.EqualTo(100010.019d).Within(.00001d));
            dto = root.State.CaptureDto();
            var restored = new VehicleSimulationRoot(f.Host.Config, wheels, f.Prerequisites, f.Source);
            Assert.That(restored.State.TryRestoreDto(dto,f.Host.Config), Is.True);
            Assert.That(restored.State.SatsumaOperating.OdometerKilometers, Is.EqualTo(root.State.SatsumaOperating.OdometerKilometers));
        }

        private sealed class InstrumentDistanceWheels : IWheelPhysicsBackend
        {
            public float Speed;
            public int WheelCount => 4;
            public float VehicleSpeedMetersPerSecond => Speed;
            public void Sample(float dt, WheelPhysicsSample[] samples)
            { for (int i = 0; i < samples.Length; i++) samples[i] = WheelPhysicsSample.NoContact; }
            public void Apply(float dt, WheelPhysicsCommand[] commands) { }
            public void Reset() { }
        }

        [Test]
        public void OdometerOldSaveDefaultsAndNewSaveRoundtripAreIndependentOfPresentation()
        {
            using var f = new Fixture(); var dto = f.Host.State.CaptureDto();
            dto.satsumaOperatingState.hasOdometerState = false;
            Assert.That(f.Host.TryRestoreSimulationState(dto, out _), Is.True);
            Assert.That(f.Host.State.SatsumaOperating.OdometerKilometers, Is.EqualTo(100000d));
            dto = f.Host.State.CaptureDto(); dto.satsumaOperatingState.odometerTenKilometerUnits = 12345;
            dto.satsumaOperatingState.odometerPartialMeters = 6789.25;
            Assert.That(f.Host.TryRestoreSimulationState(dto, out _), Is.True);
            Assert.That(f.Host.State.SatsumaOperating.OdometerKilometers, Is.EqualTo(123456.78925).Within(1e-8));
            string json = JsonUtility.ToJson(f.Host.State.CaptureDto());
            Assert.That(f.Host.TryRestoreSimulationState(JsonUtility.FromJson<VehicleSimulationStateDto>(json), out _), Is.True);
            Assert.That(JsonUtility.ToJson(f.Host.State.CaptureDto()), Is.EqualTo(json));
            dto.satsumaOperatingState.odometerPartialMeters = double.NaN;
            Assert.That(f.Host.TryRestoreSimulationState(dto, out _), Is.False);
        }
    }
}
