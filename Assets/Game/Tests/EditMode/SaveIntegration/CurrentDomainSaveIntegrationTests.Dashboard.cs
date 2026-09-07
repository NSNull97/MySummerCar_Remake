using System;
using System.Globalization;
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
        [Test]
        public void DashboardVehicleRecordJsonRoundTripRestoresPersistentControlsSilently()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaDashboardControlsController controls = BindDashboard(fixture);
            SetDashboard(controls, 0.35f, 2, true);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            Assert.That(record.hasDashboardControls, Is.True);
            string savedJson = JsonUtility.ToJson(record);
            SetDashboard(controls, 0.9f, 0, true);
            controls.Simulate(SatsumaDashboardControlsController.HazardHalfCycleSeconds + 0.01f);
            Assert.That(controls.HazardPulseOn, Is.False);
            int actions = 0;
            controls.ActionRequested += _ => actions++;

            Assert.That(fixture.Binding.TryRestore(JsonUtility.FromJson<VehicleSaveRecordDto>(savedJson), out failure), Is.True, failure);

            AssertDashboard(controls, 0.35f, 2, true);
            Assert.That(controls.HazardPulseOn, Is.True, "Blink phase is transient and restarts on restore.");
            Assert.That(actions, Is.Zero, "Restoring controls must not request player actions or their audio.");
            Assert.That(JsonUtility.ToJson(record), Is.EqualTo(savedJson), "Restore must not rewrite its source DTO.");
        }

        [Test]
        public void DashboardLegacyJsonOmittingBothFieldsRestoresQuietDefaults()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaDashboardControlsController controls = BindDashboard(fixture);
            SetDashboard(controls, 0.65f, 2, true);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            string legacyJson = JsonUtility.ToJson(record)
                .Replace(",\"hasDashboardControls\":true", string.Empty)
                .Replace(",\"dashboardControls\":" + JsonUtility.ToJson(record.dashboardControls), string.Empty);
            Assert.That(legacyJson, Does.Not.Contain("DashboardControls"));
            Assert.That(legacyJson, Does.Not.Contain("dashboardControls"));
            VehicleSaveRecordDto legacy = JsonUtility.FromJson<VehicleSaveRecordDto>(legacyJson);
            Assert.That(legacy.hasDashboardControls, Is.False);
            int actions = 0;
            controls.ActionRequested += _ => actions++;

            Assert.That(fixture.Binding.TryRestore(legacy, out failure), Is.True, failure);

            AssertDashboard(controls, 0f, 0, false);
            Assert.That(actions, Is.Zero, "Legacy defaults must be applied silently.");
        }

        [Test]
        public void DashboardAbsentPresenceIgnoresAnInvalidMaterializedInlineObject()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaDashboardControlsController controls = BindDashboard(fixture);
            SetDashboard(controls, 0.4f, 1, true);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            record.hasDashboardControls = false;
            record.dashboardControls = new SatsumaDashboardControlsSaveDto
            { schemaVersion = -1, choke01 = float.NaN, headlightsMode = -1, hazardsOn = true };

            Assert.That(record.TryValidateBasic(out failure), Is.True, failure);
            Assert.That(fixture.Binding.TryRestore(record, out failure), Is.True, failure);

            AssertDashboard(controls, 0f, 0, false);
        }

        [TestCase("schema")]
        [TestCase("nan")]
        [TestCase("infinity")]
        [TestCase("negative-choke")]
        [TestCase("excess-choke")]
        [TestCase("negative-mode")]
        [TestCase("excess-mode")]
        [TestCase("missing")]
        public void DashboardPresentCorruptionFailsBeforeRuntimeMutation(string corruption)
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaDashboardControlsController controls = BindDashboard(fixture);
            SetDashboard(controls, 0.7f, 1, true);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            string before = JsonUtility.ToJson(record);
            switch (corruption)
            {
                case "schema": record.dashboardControls.schemaVersion = 2; break;
                case "nan": record.dashboardControls.choke01 = float.NaN; break;
                case "infinity": record.dashboardControls.choke01 = float.PositiveInfinity; break;
                case "negative-choke": record.dashboardControls.choke01 = -0.01f; break;
                case "excess-choke": record.dashboardControls.choke01 = 1.01f; break;
                case "negative-mode": record.dashboardControls.headlightsMode = -1; break;
                case "excess-mode": record.dashboardControls.headlightsMode = 3; break;
                case "missing": record.dashboardControls = null; break;
                default: throw new ArgumentOutOfRangeException(nameof(corruption));
            }

            Assert.That(record.TryValidateBasic(out _), Is.False);
            Assert.That(fixture.Binding.CanRestore(record, out _), Is.False);
            Assert.That(fixture.Binding.TryRestore(record, out _), Is.False);

            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto after, out failure), Is.True, failure);
            Assert.That(JsonUtility.ToJson(after), Is.EqualTo(before));
        }

        [Test]
        public void DashboardPresentRecordCannotBeSilentlyDroppedByAnUnboundVehicle()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            Assert.That(record.hasDashboardControls, Is.False);
            record.hasDashboardControls = true;
            record.dashboardControls = new SatsumaDashboardControlsSaveDto { choke01 = 0.8f, headlightsMode = 2, hazardsOn = true };
            Vector3 before = fixture.Chassis.position;
            record.physics.worldPosition += Vector3.up * 10f;

            Assert.That(fixture.Binding.CanRestore(record, out failure), Is.False);
            Assert.That(failure, Does.Contain("dashboard"));
            Assert.That(fixture.Binding.TryRestore(record, out _), Is.False);
            Assert.That(fixture.Chassis.position, Is.EqualTo(before));
        }

        [Test]
        public void DashboardBindingLocalFailureRestoresItsCapturedControls()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaDashboardControlsController controls = BindDashboard(fixture);
            SetDashboard(controls, 0.2f, 1, false);
            var synchronizer = fixture.Root.AddComponent<DashboardFailOnceSynchronizer>();
            synchronizer.Controls = controls;
            fixture.Binding.Configure(fixture.Root.GetComponent<StableEntityIdAuthoring>(),
                fixture.Binding.AssemblyController, fixture.Root.GetComponent<VehicleSimulationHost>(),
                input: null, targetChassis: fixture.Chassis, physicsRestoreSynchronizer: synchronizer);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            record.dashboardControls = new SatsumaDashboardControlsSaveDto { choke01 = 0.85f, headlightsMode = 2, hazardsOn = true };
            int actions = 0;
            controls.ActionRequested += _ => actions++;

            Assert.That(fixture.Binding.TryRestore(record, out failure), Is.False);

            Assert.That(failure, Is.EqualTo("expected dashboard restore failure"));
            Assert.That(synchronizer.Calls, Is.EqualTo(2), "The binding must reapply its checkpoint after the failed synchronization.");
            Assert.That(synchronizer.FirstObserved.choke01, Is.EqualTo(0.85f));
            AssertDashboard(controls, 0.2f, 1, false);
            Assert.That(actions, Is.Zero, "Neither the attempted restore nor its local rollback may request audio/actions.");
        }

        [Test]
        public void DashboardParticipantCheckpointSurvivesALaterDomainFailure()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaDashboardControlsController controls = BindDashboard(fixture);
            SetDashboard(controls, 0.25f, 1, false);
            var deferred = new DeferredStableEntityStore();
            var world = new WorldEntitySaveParticipant(deferred);
            var vehicle = new VehicleSaveParticipant(deferred);
            vehicle.RegisterHierarchy(fixture.Root);
            var later = new DashboardFailingParticipant(controls);
            var registry = new SaveParticipantRegistry(new ISaveParticipant[] { later, vehicle, world });
            SaveDocument document = DashboardDocument(registry.CaptureDomains());
            SaveDomainEnvelope vehicleEnvelope = document.Domains.Single(value => value.DomainId == VehicleSaveParticipant.DomainId);
            VehicleDomainSaveDto target = SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(vehicleEnvelope.PayloadJson);
            target.vehicles.Single().dashboardControls = new SatsumaDashboardControlsSaveDto
            { choke01 = 0.9f, headlightsMode = 2, hazardsOn = true };
            vehicleEnvelope.PayloadJson = SaveParticipantJson.Serialize(target);
            var unresolved = new UnresolvedContentReport();
            int actions = 0;
            controls.ActionRequested += _ => actions++;
            PreparedSaveRestore prepared = registry.PrepareRestore(document, unresolved, deferred);

            Assert.Throws<InvalidOperationException>(() => registry.ApplyRestore(prepared, unresolved, deferred));

            Assert.That(later.Observed.choke01, Is.EqualTo(0.9f));
            Assert.That(later.Observed.headlightsMode, Is.EqualTo(2));
            Assert.That(later.Observed.hazardsOn, Is.True);
            AssertDashboard(controls, 0.25f, 1, false);
            Assert.That(actions, Is.Zero, "Cross-domain rollback must restore persistent intent silently.");
        }

        [Test]
        public void DashboardDeferredVehicleRetainsControlsUntilItsBindingLoads()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            SatsumaDashboardControlsController controls = BindDashboard(fixture);
            SetDashboard(controls, 0.6f, 2, true);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            var deferred = new DeferredStableEntityStore();
            var vehicle = new VehicleSaveParticipant(deferred);
            var unresolved = new UnresolvedContentReport();
            var source = new VehicleDomainSaveDto { vehicles = new[] { record } };
            object prepared = vehicle.PrepareRestore(DashboardVehicleEnvelope(source),
                new SaveRestorePreparationContext(unresolved, deferred));
            vehicle.ApplyPreparedRestore(prepared, new SaveRestoreContext(unresolved, deferred));

            Assert.That(deferred.TryPeek(VehicleSaveParticipant.DomainId, record.stableVehicleId, out _), Is.True);
            VehicleSaveRecordDto archived = SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(vehicle.CapturePayload()).vehicles.Single();
            Assert.That(archived.hasDashboardControls, Is.True);
            Assert.That(JsonUtility.ToJson(archived.dashboardControls), Is.EqualTo(JsonUtility.ToJson(record.dashboardControls)));
            SetDashboard(controls, 0.1f, 0, false);
            int actions = 0;
            controls.ActionRequested += _ => actions++;
            vehicle.RegisterHierarchy(fixture.Root);

            AssertDashboard(controls, 0.6f, 2, true);
            Assert.That(deferred.TryPeek(VehicleSaveParticipant.DomainId, record.stableVehicleId, out _), Is.False);
            Assert.That(actions, Is.Zero, "A deferred vehicle load must not replay player action feedback.");
        }

        [Test]
        public void DashboardInvalidDeferredPayloadFailsPreflightBeforeBeingQueued()
        {
            using var fixture = SyntheticVehicleFixture.Create();
            BindDashboard(fixture);
            Assert.That(fixture.Binding.TryCapture(out VehicleSaveRecordDto record, out string failure), Is.True, failure);
            record.dashboardControls.schemaVersion = 2;
            var deferred = new DeferredStableEntityStore();
            var vehicle = new VehicleSaveParticipant(deferred);

            Assert.Throws<InvalidDataException>(() => vehicle.PrepareRestore(
                DashboardVehicleEnvelope(new VehicleDomainSaveDto { vehicles = new[] { record } }),
                new SaveRestorePreparationContext(new UnresolvedContentReport(), deferred)));

            Assert.That(deferred.Snapshot(), Is.Empty);
        }

        private static SatsumaDashboardControlsController BindDashboard(SyntheticVehicleFixture fixture)
        {
            var controls = fixture.Root.AddComponent<SatsumaDashboardControlsController>();
            fixture.Binding.ConfigureDashboardControls(controls);
            return controls;
        }

        private static void SetDashboard(SatsumaDashboardControlsController controls, float choke, int headlights, bool hazards)
        {
            Assert.That(controls.TryRestore(new SatsumaDashboardControlsSaveDto
            { choke01 = choke, headlightsMode = headlights, hazardsOn = hazards }, out string failure), Is.True, failure);
        }

        private static void AssertDashboard(SatsumaDashboardControlsController controls, float choke, int headlights, bool hazards)
        {
            Assert.That(controls.Choke01, Is.EqualTo(choke));
            Assert.That((int)controls.HeadlightsMode, Is.EqualTo(headlights));
            Assert.That(controls.HazardsOn, Is.EqualTo(hazards));
        }

        private static SaveDomainEnvelope DashboardVehicleEnvelope(VehicleDomainSaveDto state) => new()
        {
            DomainId = VehicleSaveParticipant.DomainId,
            SchemaVersion = VehicleDomainSaveDto.CurrentSchemaVersion,
            Required = true,
            PayloadJson = SaveParticipantJson.Serialize(state),
        };

        private static SaveDocument DashboardDocument(SaveDomainEnvelope[] domains)
        {
            string timestamp = new DateTimeOffset(2026, 9, 5, 0, 0, 0, TimeSpan.Zero)
                .ToString("O", CultureInfo.InvariantCulture);
            return new SaveDocument
            {
                Header = new SaveHeader
                {
                    SaveId = "save-dashboard-tests", SlotId = "slot-dashboard", BuildId = "tests",
                    CreatedUtc = timestamp, UpdatedUtc = timestamp,
                },
                Domains = domains,
            };
        }

        private sealed class DashboardFailOnceSynchronizer : MonoBehaviour, IVehiclePhysicsRestoreSynchronizer
        {
            public SatsumaDashboardControlsController Controls;
            public int Calls;
            public SatsumaDashboardControlsSaveDto FirstObserved;
            public bool TrySynchronizeRestoredPhysics(out string failure)
            {
                Calls++;
                if (Calls == 1)
                {
                    FirstObserved = Controls.CaptureSaveData();
                    failure = "expected dashboard restore failure";
                    return false;
                }
                failure = string.Empty;
                return true;
            }
        }

        private sealed class DashboardFailingParticipant : ISaveParticipant
        {
            private readonly SatsumaDashboardControlsController controls;
            public DashboardFailingParticipant(SatsumaDashboardControlsController controls) { this.controls = controls; }
            public SaveParticipantDescriptor Descriptor { get; } = new("test.dashboard-later", 1, true,
                SaveRestorePhase.PresentationSync, VehicleSaveParticipant.DomainId);
            public SatsumaDashboardControlsSaveDto Observed;
            public string CapturePayload() => "{}";
            public object PrepareRestore(SaveDomainEnvelope envelope, SaveRestorePreparationContext context) => null;
            public object CaptureCheckpoint() => null;
            public void ApplyPreparedRestore(object preparedState, SaveRestoreContext context)
            {
                Observed = controls.CaptureSaveData();
                throw new InvalidOperationException("expected later domain failure");
            }
            public void Rollback(object checkpoint) { }
        }
    }
}
