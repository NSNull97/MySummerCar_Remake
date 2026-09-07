using System;
using System.IO;
using System.Linq;
using MSC.Core.Identity;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed partial class CurrentDomainSaveIntegrationTests
    {
        [TestCase(0, false)]
        [TestCase(8, true)]
        [TestCase(4, false)]
        [TestCase(4, true)]
        public void FuelLineVehicleRecordJsonRoundTripPreservesStageAndLatchHistory(int stage, bool isBolted)
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaFuelLineConnection connection = BindFuelLine(fixture);
            SetFuelLine(connection, stage, isBolted);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            Assert.That(record.hasFuelLineConnection, Is.True);
            string savedJson = JsonUtility.ToJson(record);
            SetFuelLine(connection, stage == 0 ? 8 : 0, stage == 0);

            Assert.That(fixture.Binding.TryRestore(JsonUtility.FromJson<VehicleSaveRecordDto>(savedJson), out failure), Is.True, failure);

            AssertFuelLine(connection, stage, isBolted);
            Assert.That(JsonUtility.ToJson(record), Is.EqualTo(savedJson), "Restore must not rewrite the caller's DTO.");
        }

        [Test]
        public void FuelLineLegacyJsonOmittingBothFieldsRestoresUnfastenedDefaults()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaFuelLineConnection connection = BindFuelLine(fixture);
            SetFuelLine(connection, 7, true);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            string legacyJson = JsonUtility.ToJson(record)
                .Replace(",\"hasFuelLineConnection\":true", string.Empty)
                .Replace(",\"fuelLineConnection\":" + JsonUtility.ToJson(record.fuelLineConnection), string.Empty);
            Assert.That(legacyJson, Does.Not.Contain("FuelLineConnection"));
            Assert.That(legacyJson, Does.Not.Contain("fuelLineConnection"));
            VehicleSaveRecordDto legacy = JsonUtility.FromJson<VehicleSaveRecordDto>(legacyJson);
            Assert.That(legacy.hasFuelLineConnection, Is.False);

            Assert.That(fixture.Binding.TryRestore(legacy, out failure), Is.True, failure);

            AssertFuelLine(connection, 0, false);
            Assert.That(legacy.hasFuelLineConnection, Is.False, "Restoring defaults must not mutate the source presence flag.");
        }

        [Test]
        public void FuelLineAbsentPresenceIgnoresInvalidMaterializedInlineObject()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaFuelLineConnection connection = BindFuelLine(fixture);
            SetFuelLine(connection, 8, true);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            record.hasFuelLineConnection = false;
            record.fuelLineConnection = new SatsumaFuelLineConnectionSaveDto
            { schemaVersion = -1, stage = -1, isBolted = true };
            string sourceJson = JsonUtility.ToJson(record);

            Assert.That(record.TryValidateBasic(out failure), Is.True, failure);
            Assert.That(fixture.Binding.TryRestore(record, out failure), Is.True, failure);

            AssertFuelLine(connection, 0, false);
            Assert.That(JsonUtility.ToJson(record), Is.EqualTo(sourceJson));
        }

        [TestCase("missing")]
        [TestCase("schema-zero")]
        [TestCase("schema-future")]
        [TestCase("negative-stage")]
        [TestCase("excess-stage")]
        [TestCase("zero-bolted")]
        [TestCase("eight-unbolted")]
        public void FuelLinePresentCorruptionFailsBeforeAnyRuntimeMutation(string corruption)
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaFuelLineConnection connection = BindFuelLine(fixture);
            SetFuelLine(connection, 6, true);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            string before = JsonUtility.ToJson(record);
            record.physics.worldPosition += Vector3.up * 10f;
            switch (corruption)
            {
                case "missing": record.fuelLineConnection = null; break;
                case "schema-zero": record.fuelLineConnection.schemaVersion = 0; break;
                case "schema-future": record.fuelLineConnection.schemaVersion = 2; break;
                case "negative-stage": record.fuelLineConnection.stage = -1; break;
                case "excess-stage": record.fuelLineConnection.stage = 9; break;
                case "zero-bolted": record.fuelLineConnection.stage = 0; break;
                case "eight-unbolted":
                    record.fuelLineConnection.stage = 8;
                    record.fuelLineConnection.isBolted = false;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(corruption));
            }
            string corruptSourceJson = JsonUtility.ToJson(record);

            Assert.That(record.TryValidateBasic(out _), Is.False);
            Assert.That(fixture.Binding.CanRestore(record, out _), Is.False);
            Assert.That(fixture.Binding.TryRestore(record, out _), Is.False);

            AssertFuelLine(connection, 6, true);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto after, out failure), Is.True, failure);
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(record), Is.EqualTo(corruptSourceJson), "Validation must not repair or rewrite corrupt input silently.");
        }

        [Test]
        public void FuelLinePresentRecordCannotBeSilentlyDroppedByUnboundVehicle()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            Assert.That(record.hasFuelLineConnection, Is.False);
            record.hasFuelLineConnection = true;
            record.fuelLineConnection = new SatsumaFuelLineConnectionSaveDto
            { schemaVersion = 1, stage = 5, isBolted = true };
            Vector3 before = fixture.Chassis.position;
            record.physics.worldPosition += Vector3.up * 10f;

            Assert.That(record.TryValidateBasic(out failure), Is.True, failure);
            Assert.That(fixture.Binding.CanRestore(record, out failure), Is.False);
            Assert.That(failure, Is.Not.Empty);
            Assert.That(fixture.Binding.TryRestore(record, out _), Is.False);
            Assert.That(fixture.Chassis.position, Is.EqualTo(before));
        }

        [Test]
        public void FuelLineBindingLocalFailureRestoresCapturedStageAndHistoricalLatch()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaFuelLineConnection connection = BindFuelLine(fixture);
            SetFuelLine(connection, 3, false);
            var synchronizer = fixture.Root.AddComponent<FuelLineFailOnceSynchronizer>();
            synchronizer.Connection = connection;
            fixture.Binding.Configure(fixture.Root.GetComponent<StableEntityIdAuthoring>(),
                fixture.Binding.AssemblyController, fixture.Root.GetComponent<VehicleSimulationHost>(),
                input: null, targetChassis: fixture.Chassis, physicsRestoreSynchronizer: synchronizer);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            Vector3 before = fixture.Chassis.position;
            record.physics.worldPosition += Vector3.up * 10f;
            record.fuelLineConnection = new SatsumaFuelLineConnectionSaveDto
            { schemaVersion = 1, stage = 6, isBolted = true };
            string sourceJson = JsonUtility.ToJson(record);

            Assert.That(fixture.Binding.TryRestore(record, out failure), Is.False);

            Assert.That(failure, Is.EqualTo("expected fuel-line restore failure"));
            Assert.That(synchronizer.Calls, Is.EqualTo(2), "The local checkpoint must be reapplied after failed synchronization.");
            Assert.That(synchronizer.FirstObserved.stage, Is.EqualTo(6), "The failure must occur after the target fitting state was actually applied.");
            Assert.That(synchronizer.FirstObserved.isBolted, Is.True);
            AssertFuelLine(connection, 3, false);
            Assert.That(fixture.Chassis.position, Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(record), Is.EqualTo(sourceJson));
        }

        [Test]
        public void FuelLineCaptureReturnsIndependentNestedState()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaFuelLineConnection connection = BindFuelLine(fixture);
            SetFuelLine(connection, 5, true);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto first, out string failure), Is.True, failure);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto second, out failure), Is.True, failure);
            Assert.That(second.fuelLineConnection, Is.Not.SameAs(first.fuelLineConnection));
            first.fuelLineConnection.stage = 1;
            first.fuelLineConnection.isBolted = false;

            AssertFuelLine(connection, 5, true);
            Assert.That(second.fuelLineConnection.stage, Is.EqualTo(5));
            Assert.That(second.fuelLineConnection.isBolted, Is.True);
            SetFuelLine(connection, 0, false);
            Assert.That(second.fuelLineConnection.stage, Is.EqualTo(5), "Later runtime mutation must not rewrite an earlier snapshot.");
            Assert.That(second.fuelLineConnection.isBolted, Is.True);
        }

        [Test]
        public void FuelLineDeferredRoundTripRetainsIndependentStateUntilBindingLoads()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaFuelLineConnection connection = BindFuelLine(fixture);
            SetFuelLine(connection, 7, true);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            string sourceJson = JsonUtility.ToJson(record);
            var deferred = new DeferredStableEntityStore();
            var vehicle = new VehicleSaveParticipant(deferred);
            var unresolved = new UnresolvedContentReport();
            object prepared = vehicle.PrepareRestore(
                FuelLineVehicleEnvelope(new VehicleDomainSaveDto { vehicles = new[] { record } }),
                new SaveRestorePreparationContext(unresolved, deferred));
            vehicle.ApplyPreparedRestore(prepared, new SaveRestoreContext(unresolved, deferred));

            Assert.That(JsonUtility.ToJson(record), Is.EqualTo(sourceJson), "Preparing and queuing a deferred record must not mutate its source.");
            Assert.That(deferred.TryPeek(VehicleSaveParticipant.DomainId, record.stableVehicleId, out _), Is.True);
            VehicleSaveRecordDto archived = SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(vehicle.CapturePayload()).vehicles.Single();
            Assert.That(archived.hasFuelLineConnection, Is.True);
            Assert.That(archived.fuelLineConnection, Is.Not.SameAs(record.fuelLineConnection));
            Assert.That(JsonUtility.ToJson(archived.fuelLineConnection), Is.EqualTo(JsonUtility.ToJson(record.fuelLineConnection)));
            // Neither caller-owned input nor a recaptured DTO may alias the deferred store.
            record.fuelLineConnection.stage = 0;
            record.fuelLineConnection.isBolted = false;
            archived.fuelLineConnection.stage = 8;
            archived.fuelLineConnection.isBolted = true;
            VehicleSaveRecordDto recaptured = SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(vehicle.CapturePayload()).vehicles.Single();
            Assert.That(recaptured.fuelLineConnection.stage, Is.EqualTo(7));
            Assert.That(recaptured.fuelLineConnection.isBolted, Is.True);
            SetFuelLine(connection, 2, false);

            vehicle.RegisterHierarchy(fixture.Root);

            AssertFuelLine(connection, 7, true);
            Assert.That(deferred.TryPeek(VehicleSaveParticipant.DomainId, record.stableVehicleId, out _), Is.False);
        }

        [Test]
        public void FuelLineInvalidDeferredPayloadFailsPreflightBeforeQueueing()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            BindFuelLine(fixture);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            record.fuelLineConnection.stage = 0;
            record.fuelLineConnection.isBolted = true;
            var deferred = new DeferredStableEntityStore();
            var vehicle = new VehicleSaveParticipant(deferred);

            Assert.Throws<InvalidDataException>(() => vehicle.PrepareRestore(
                FuelLineVehicleEnvelope(new VehicleDomainSaveDto { vehicles = new[] { record } }),
                new SaveRestorePreparationContext(new UnresolvedContentReport(), deferred)));

            Assert.That(deferred.Snapshot(), Is.Empty);
        }

        private static SatsumaFuelLineConnection BindFuelLine(SyntheticVehicleFixture fixture)
        {
            // Persistence belongs to the fixed vehicle connection, not to tank visibility.
            var connection = fixture.Root.AddComponent<SatsumaFuelLineConnection>();
            fixture.Binding.ConfigureFuelLineConnection(connection);
            return connection;
        }

        private static void SetFuelLine(SatsumaFuelLineConnection connection, int stage, bool isBolted)
        {
            Assert.That(connection.TryRestore(new SatsumaFuelLineConnectionSaveDto
            { schemaVersion = 1, stage = stage, isBolted = isBolted }, out string failure), Is.True, failure);
        }

        private static void AssertFuelLine(SatsumaFuelLineConnection connection, int stage, bool isBolted)
        {
            Assert.That(connection.Stage, Is.EqualTo(stage));
            Assert.That(connection.IsBolted, Is.EqualTo(isBolted));
        }

        private static SaveDomainEnvelope FuelLineVehicleEnvelope(VehicleDomainSaveDto state) => new()
        {
            DomainId = VehicleSaveParticipant.DomainId,
            SchemaVersion = VehicleDomainSaveDto.CurrentSchemaVersion,
            Required = true,
            PayloadJson = SaveParticipantJson.Serialize(state),
        };

        private sealed class FuelLineFailOnceSynchronizer : MonoBehaviour, IVehiclePhysicsRestoreSynchronizer
        {
            public SatsumaFuelLineConnection Connection;
            public int Calls;
            public SatsumaFuelLineConnectionSaveDto FirstObserved;

            public bool TrySynchronizeRestoredPhysics(out string failure)
            {
                Calls++;
                if (Calls == 1)
                {
                    FirstObserved = Connection.CaptureSaveData();
                    failure = "expected fuel-line restore failure";
                    return false;
                }

                failure = string.Empty;
                return true;
            }
        }
    }
}
