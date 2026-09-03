using System;
using System.Linq;
using MSC.Lighting;
using NUnit.Framework;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed class LightingSaveParticipantTests
    {
        [Test]
        public void CapturePayload_ExcludesRuntimeDerivedPresentationState()
        {
            ElectricalGridService grid = CreateGrid(includeRuntimeDerived: true);
            var participant = new LightingSaveParticipant(grid);

            ElectricalGridStateDto captured =
                SaveParticipantJson.Deserialize<ElectricalGridStateDto>(
                    participant.CapturePayload());

            Assert.That(
                captured.sources.Select(value => value.id),
                Is.EqualTo(new[] { "source.grid.main" }));
            Assert.That(
                captured.circuits.Select(value => value.id),
                Is.EqualTo(new[] { "grid.home.kitchen" }));
            Assert.That(
                captured.switches.Select(value => value.id),
                Is.EqualTo(new[] { "switch.home.kitchen" }));
        }

        [Test]
        public void LegacyRuntimeDerivedEntries_DoNotBlockStaticGridRestore()
        {
            ElectricalGridService grid = CreateGrid(includeRuntimeDerived: false);
            var participant = new LightingSaveParticipant(grid);
            ElectricalGridStateDto legacyState = CreatePersistentState(
                sourceAvailable: false);
            legacyState.sources = legacyState.sources.Concat(new[]
            {
                State("source.item.flashlight", true),
                State("source.traffic.vehicle", true),
            }).ToArray();
            legacyState.circuits = legacyState.circuits.Concat(new[]
            {
                State(
                    "grid.item.flashlight.8a60bfa0fb3651ae8dc0afae19b2fe0a",
                    true),
                State("grid.traffic.vehicle.p1.npc.018", true),
            }).ToArray();
            legacyState.switches = legacyState.switches.Concat(new[]
            {
                State(
                    "switch.item.flashlight.8a60bfa0fb3651ae8dc0afae19b2fe0a",
                    true),
            }).ToArray();

            object prepared = participant.PrepareRestore(
                Envelope(legacyState),
                RestorePreparationContext());

            Assert.DoesNotThrow(() => participant.ApplyPreparedRestore(
                prepared,
                RestoreContext()));
            Assert.That(
                grid.CaptureState().sources.Single().value,
                Is.False,
                "The persistent source must still restore after legacy " +
                "runtime-derived entries are removed.");
        }

        [Test]
        public void UnknownPersistentEntry_StillRejectsRestoreTransactionally()
        {
            ElectricalGridService grid = CreateGrid(includeRuntimeDerived: false);
            var participant = new LightingSaveParticipant(grid);
            ElectricalGridStateDto state = CreatePersistentState(
                sourceAvailable: false);
            state.switches = state.switches.Concat(new[]
            {
                State("switch.removed-content", true),
            }).ToArray();
            object prepared = participant.PrepareRestore(
                Envelope(state),
                RestorePreparationContext());

            InvalidOperationException failure = Assert.Throws<
                InvalidOperationException>(() =>
                participant.ApplyPreparedRestore(prepared, RestoreContext()));

            Assert.That(failure.Message, Does.Contain("unknown"));
            Assert.That(
                grid.CaptureState().sources.Single().value,
                Is.True,
                "A real unknown ID must reject the DTO before any valid entry applies.");
        }

        private static ElectricalGridService CreateGrid(
            bool includeRuntimeDerived)
        {
            var grid = new ElectricalGridService();
            grid.RegisterSource("source.grid.main", true);
            grid.RegisterCircuit(
                "grid.home.kitchen",
                "source.grid.main",
                true);
            grid.RegisterSwitch("switch.home.kitchen", true);
            if (!includeRuntimeDerived)
            {
                return grid;
            }

            grid.RegisterSource("source.item.flashlight", true);
            grid.RegisterCircuit(
                "grid.item.flashlight.8a60bfa0fb3651ae8dc0afae19b2fe0a",
                "source.item.flashlight",
                true);
            grid.RegisterSwitch(
                "switch.item.flashlight.8a60bfa0fb3651ae8dc0afae19b2fe0a",
                true);
            grid.RegisterSource("source.traffic.vehicle", true);
            grid.RegisterCircuit(
                "grid.traffic.vehicle.p1.npc.018",
                "source.traffic.vehicle",
                true);
            return grid;
        }

        private static ElectricalGridStateDto CreatePersistentState(
            bool sourceAvailable)
        {
            return new ElectricalGridStateDto
            {
                sources = new[]
                {
                    State("source.grid.main", sourceAvailable),
                },
                circuits = new[]
                {
                    State("grid.home.kitchen", true),
                },
                switches = new[]
                {
                    State("switch.home.kitchen", true),
                },
            };
        }

        private static ElectricalBooleanStateDto State(string id, bool value) =>
            new ElectricalBooleanStateDto
            {
                id = id,
                value = value,
            };

        private static SaveDomainEnvelope Envelope(
            ElectricalGridStateDto state)
        {
            return new SaveDomainEnvelope
            {
                DomainId = LightingSaveParticipant.DomainId,
                SchemaVersion = ElectricalGridStateDto.CurrentSchemaVersion,
                Required = false,
                PayloadJson = SaveParticipantJson.Serialize(state),
            };
        }

        private static SaveRestorePreparationContext
            RestorePreparationContext() => new SaveRestorePreparationContext(
                new UnresolvedContentReport(),
                new DeferredStableEntityStore());

        private static SaveRestoreContext RestoreContext() =>
            new SaveRestoreContext(
                new UnresolvedContentReport(),
                new DeferredStableEntityStore());
    }
}
