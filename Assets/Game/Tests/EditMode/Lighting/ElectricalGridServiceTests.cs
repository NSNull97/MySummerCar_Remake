using System;
using System.Linq;
using NUnit.Framework;

namespace MSC.Lighting.Tests.EditMode
{
    public sealed class ElectricalGridServiceTests
    {
        [TestCase(true, true, true, true, true, true)]
        [TestCase(false, true, true, true, true, false)]
        [TestCase(true, false, true, true, true, false)]
        [TestCase(true, true, false, true, true, false)]
        [TestCase(true, true, true, false, true, false)]
        [TestCase(true, true, true, true, false, false)]
        public void IsActuallyOn_RequiresEveryGate(
            bool source,
            bool circuit,
            bool lightSwitch,
            bool policy,
            bool available,
            bool expected)
        {
            ElectricalGridService grid = CreateGrid(
                source,
                circuit,
                lightSwitch);

            Assert.That(
                grid.IsActuallyOn(
                    "circuit.home.kitchen",
                    "switch.home.kitchen",
                    policy,
                    available),
                Is.EqualTo(expected));
        }

        [Test]
        public void StateChanges_AreEventDrivenAndIdempotent()
        {
            ElectricalGridService grid = CreateGrid(true, true, false);
            ElectricalStateChanged observed = default;
            int count = 0;
            grid.StateChanged += state =>
            {
                observed = state;
                count++;
            };

            grid.SetSwitchState("switch.home.kitchen", true);
            grid.SetSwitchState("switch.home.kitchen", true);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(observed.StateId, Is.EqualTo("switch.home.kitchen"));
            Assert.That(observed.Value, Is.True);
        }

        [Test]
        public void CaptureRestore_RoundTripsDeterministically()
        {
            ElectricalGridService grid = CreateGrid(true, true, false);
            int restoreEventCount = 0;
            grid.StateChanged += _ => restoreEventCount++;
            ElectricalGridStateDto initial = grid.CaptureState();
            grid.SetSourceAvailable("power.grid.home", false);
            grid.SetCircuitEnabled("circuit.home.kitchen", false);
            grid.SetSwitchState("switch.home.kitchen", true);
            restoreEventCount = 0;

            Assert.That(grid.TryRestoreState(initial, out string failure),
                Is.True, failure);
            Assert.That(restoreEventCount, Is.EqualTo(3),
                "Restore must notify the runtime for every changed electrical gate.");
            ElectricalGridStateDto restored = grid.CaptureState();
            Assert.That(restored.sources.Single().value, Is.True);
            Assert.That(restored.circuits.Single().value, Is.True);
            Assert.That(restored.switches.Single().value, Is.False);
        }

        [Test]
        public void InvalidRestore_DoesNotPartiallyMutateGrid()
        {
            ElectricalGridService grid = CreateGrid(true, true, false);
            var invalid = new ElectricalGridStateDto
            {
                sources = new[]
                {
                    new ElectricalBooleanStateDto
                    {
                        id = "power.grid.home",
                        value = false,
                    },
                },
                circuits = Array.Empty<ElectricalBooleanStateDto>(),
                switches = new[]
                {
                    new ElectricalBooleanStateDto
                    {
                        id = "switch.unknown",
                        value = true,
                    },
                },
            };

            Assert.That(grid.TryRestoreState(invalid, out string failure),
                Is.False);
            Assert.That(failure, Does.Contain("unknown"));
            Assert.That(
                grid.IsActuallyOn(
                    "circuit.home.kitchen",
                    string.Empty,
                    true,
                    true),
                Is.True,
                "The valid source entry must not apply before the invalid switch is rejected.");
        }

        private static ElectricalGridService CreateGrid(
            bool source,
            bool circuit,
            bool lightSwitch)
        {
            var grid = new ElectricalGridService();
            grid.RegisterSource("power.grid.home", source);
            grid.RegisterCircuit(
                "circuit.home.kitchen",
                "power.grid.home",
                circuit);
            grid.RegisterSwitch("switch.home.kitchen", lightSwitch);
            return grid;
        }
    }
}
