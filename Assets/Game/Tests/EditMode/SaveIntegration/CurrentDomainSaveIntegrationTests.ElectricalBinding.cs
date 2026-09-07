using System.Text.RegularExpressions;
using MSC.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed partial class CurrentDomainSaveIntegrationTests
    {
        [TestCase(1)]
        [TestCase(2)]
        public void ElectricalRecordWithoutRuntimeBindingRejectsBeforeAnyMutation(int schemaVersion)
        {
            using var fixture = SyntheticVehicleFixture.Create();
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            Assert.That(record.electrical, Is.Null);
            string before = JsonUtility.ToJson(record);
            record.electrical = new SatsumaElectricalSaveDto
            {
                schemaVersion = schemaVersion,
                installedConnectionIds = new[] { "BatteryHarness", "GroundBattery", "Starter" },
                batteryPlusInstalled = true,
                batteryMinusInstalled = true,
                batteryHarnessInstalled = true,
                batteryPlusStage = 8,
                batteryMinusStage = 8,
                starterCableStage = schemaVersion == 2 ? 8 : 0,
            };
            record.physics.worldPosition += Vector3.up * 10f;
            string source = JsonUtility.ToJson(record);

            Assert.That(record.TryValidateBasic(out failure), Is.True, failure);
            Assert.That(fixture.Binding.CanRestore(record, out failure), Is.False);
            Assert.That(failure, Is.EqualTo("Saved Satsuma electrical state has no runtime binding."));
            Assert.That(fixture.Binding.TryRestore(record, out failure), Is.False);
            Assert.That(failure, Is.EqualTo("Saved Satsuma electrical state has no runtime binding."));

            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto after, out failure), Is.True, failure);
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(record), Is.EqualTo(source));
        }

        [TestCase("unknown-connection")]
        [TestCase("excess-stage")]
        [TestCase("future-schema")]
        public void MalformedElectricalRecordWithoutRuntimeBindingRejectsBeforeAnyMutation(string corruption)
        {
            using var fixture = SyntheticVehicleFixture.Create();
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            string before = JsonUtility.ToJson(record);
            record.electrical = new SatsumaElectricalSaveDto
            {
                schemaVersion = corruption == "future-schema" ? 3 : 2,
                installedConnectionIds = new[] { corruption == "unknown-connection" ? "UnknownWire" : "BatteryHarness" },
                batteryPlusStage = corruption == "excess-stage" ? 9 : 0,
            };
            record.physics.worldPosition += Vector3.up * 10f;
            string source = JsonUtility.ToJson(record);

            Assert.That(record.TryValidateBasic(out _), Is.False);
            Assert.That(fixture.Binding.CanRestore(record, out _), Is.False);
            Assert.That(fixture.Binding.TryRestore(record, out _), Is.False);

            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto after, out failure), Is.True, failure);
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(record), Is.EqualTo(source));
        }

        [TestCase("battery-plus")]
        [TestCase("battery-minus")]
        [TestCase("battery-harness")]
        [TestCase("ignition")]
        [TestCase("switch-lights")]
        public void ElectricalLegacyFlagIsNotSilentlyDiscardedEvenInSchemaTwo(string flag)
        {
            using var fixture = SyntheticVehicleFixture.Create();
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            string before = JsonUtility.ToJson(record);
            record.electrical = new SatsumaElectricalSaveDto
            {
                batteryPlusInstalled = flag == "battery-plus",
                batteryMinusInstalled = flag == "battery-minus",
                batteryHarnessInstalled = flag == "battery-harness",
                ignitionInstalled = flag == "ignition",
                switchLightsInstalled = flag == "switch-lights",
            };
            record.physics.worldPosition += Vector3.up * 10f;
            string source = JsonUtility.ToJson(record);

            Assert.That(record.TryValidateBasic(out failure), Is.True, failure);
            Assert.That(fixture.Binding.CanRestore(record, out failure), Is.False);
            Assert.That(failure, Is.EqualTo("Saved Satsuma electrical state has no runtime binding."));
            Assert.That(fixture.Binding.TryRestore(record, out _), Is.False);

            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto after, out failure), Is.True, failure);
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(record), Is.EqualTo(source));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyNullElectricalJsonRoundTripRestoresWithoutRuntimeBinding(bool omitElectricalField)
        {
            using var fixture = SyntheticVehicleFixture.Create();
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            Assert.That(record.electrical, Is.Null);
            record.physics.worldPosition += Vector3.up * 10f;
            string json = JsonUtility.ToJson(record);
            if (omitElectricalField)
            {
                // This optional DTO has scalar fields and a string array, no nested objects.
                json = Regex.Replace(json, ",\\\"electrical\\\":(?:null|\\{[^{}]*\\})", string.Empty);
                Assert.That(json, Does.Not.Contain("\"electrical\":"));
            }

            VehicleSaveRecordDto roundTrip = JsonUtility.FromJson<VehicleSaveRecordDto>(json);
            Assert.That(record.electrical, Is.Null, "JSON conversion must not mutate the caller's nullable field.");
            if (!omitElectricalField)
            {
                Assert.That(roundTrip.electrical, Is.Not.Null, "Reproduce Unity's inline null materialization.");
                Assert.That(roundTrip.electrical.schemaVersion, Is.EqualTo(SatsumaElectricalSaveDto.CurrentSchemaVersion));
                Assert.That(JsonUtility.ToJson(roundTrip.electrical), Is.EqualTo(JsonUtility.ToJson(new SatsumaElectricalSaveDto())));
            }
            string source = JsonUtility.ToJson(roundTrip);

            Assert.That(roundTrip.TryValidateBasic(out failure), Is.True, failure);
            Assert.That(fixture.Binding.CanRestore(roundTrip, out failure), Is.True, failure);
            Assert.That(fixture.Binding.TryRestore(roundTrip, out failure), Is.True, failure);

            Assert.That(Vector3.Distance(fixture.Chassis.position, record.physics.worldPosition), Is.LessThan(0.0001f));
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto after, out failure), Is.True, failure);
            Assert.That(after.electrical, Is.Null);
            Assert.That(JsonUtility.ToJson(roundTrip), Is.EqualTo(source));
        }

        [Test]
        public void LegacyNullElectricalRecordStillRestoresWithoutRuntimeElectricalBinding()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            Assert.That(record.electrical, Is.Null);
            record.physics.worldPosition += Vector3.up * 10f;
            string source = JsonUtility.ToJson(record);

            Assert.That(fixture.Binding.CanRestore(record, out failure), Is.True, failure);
            Assert.That(fixture.Binding.TryRestore(record, out failure), Is.True, failure);

            Assert.That(Vector3.Distance(fixture.Chassis.position, record.physics.worldPosition), Is.LessThan(0.0001f));
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto after, out failure), Is.True, failure);
            Assert.That(after.electrical, Is.Null);
            Assert.That(JsonUtility.ToJson(record), Is.EqualTo(source));
        }
    }
}
