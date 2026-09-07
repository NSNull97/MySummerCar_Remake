using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using NUnit.Framework;

namespace MSC.Tests.EditMode.VehicleSimulation
{
    public sealed class SatsumaStartReadinessTests
    {
        [Test]
        public void AllSixteenCylinderCombinationsUseTheDonorPairsNotAllFourPlugs()
        {
            for (int mask = 0; mask < 16; mask++)
            {
                bool oneOrFour = (mask & 1) != 0 || (mask & 8) != 0;
                bool twoOrThree = (mask & 2) != 0 || (mask & 4) != 0;
                Assert.That(SatsumaEngineAssemblyReadiness.HasFiringPairs(mask),
                    Is.EqualTo(oneOrFour && twoOrThree), "Cylinder mask " + mask);
            }
        }

        [TestCase(0f, false, false)]
        [TestCase(.99f, false, false)]
        [TestCase(1f, false, true)]
        [TestCase(9f, false, true)]
        [TestCase(100f, false, true)]
        [TestCase(100f, true, false)]
        public void PlugConditionUsesOnePercentThresholdNotTheMisfireThreshold(float wear, bool broken, bool usable)
        {
            Assert.That(SatsumaEngineAssemblyReadiness.IsPlugUsable(wear, broken), Is.EqualTo(usable));
            Assert.That(SatsumaEngineAssemblyReadiness.IsPlugUsable(float.NaN, false), Is.False);
            Assert.That(SatsumaEngineAssemblyReadiness.IsPlugUsable(float.PositiveInfinity, false), Is.False);
        }

        [Test]
        public void SatsumaStarterCanCrankWithoutCombustionOrFluidsButCannotRun()
        {
            var result = new VehicleSimulationPrerequisites();
            result.UseIndependentCrankingRequirements();
            result.Add(VehicleSimulationPrerequisiteFailure.CombustionUnavailable |
                VehicleSimulationPrerequisiteFailure.FuelUnavailable |
                VehicleSimulationPrerequisiteFailure.OilUnavailable |
                VehicleSimulationPrerequisiteFailure.CoolantUnavailable);
            Assert.That(result.CanCrank, Is.True);
            Assert.That(result.CanRun, Is.False);
            result.Reset();
            result.Add(VehicleSimulationPrerequisiteFailure.FuelUnavailable);
            Assert.That(result.CanCrank, Is.False, "Reset preserves the opt-in boundary for existing prototype sources.");
        }

        [TestCase(VehicleSimulationPrerequisiteFailure.EngineAssemblyMissing)]
        [TestCase(VehicleSimulationPrerequisiteFailure.StarterMissing)]
        [TestCase(VehicleSimulationPrerequisiteFailure.BatteryMissing)]
        [TestCase(VehicleSimulationPrerequisiteFailure.BatteryVoltageLow)]
        [TestCase(VehicleSimulationPrerequisiteFailure.IgnitionOff)]
        [TestCase(VehicleSimulationPrerequisiteFailure.PrerequisiteSourceUnavailable)]
        public void IndependentCrankingDoesNotBypassPhysicalStarterAndSupplyFailures(VehicleSimulationPrerequisiteFailure failure)
        {
            var result = new VehicleSimulationPrerequisites();
            result.UseIndependentCrankingRequirements();
            result.Add(failure);
            Assert.That(result.CanCrank, Is.False);
        }

        [Test]
        public void BareGraphAndMissingConditionAreNotImplicitlyACompleteEngine()
        {
            Assert.That(SatsumaEngineAssemblyReadiness.IsStructuralCombustionReady(null), Is.False);
            Assert.That(SatsumaEngineAssemblyReadiness.EvaluateFiringCylinderMask(null), Is.Zero);
        }

        [Test]
        public void AlternatorAvailabilityDoesNotPreventStartingFromAChargedBattery()
        {
            var result = new VehicleSimulationPrerequisites();
            result.UseIndependentCrankingRequirements();
            result.Add(VehicleSimulationPrerequisiteFailure.AlternatorUnavailable);
            Assert.That(result.CanCrank, Is.True);
            Assert.That(result.CanRun, Is.True);
        }
    }
}
