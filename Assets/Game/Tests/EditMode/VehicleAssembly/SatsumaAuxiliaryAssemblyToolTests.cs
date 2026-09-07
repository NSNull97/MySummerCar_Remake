using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaAuxiliaryAssemblyToolTests
    {
        [Test]
        public void SpecialToolsAreUnsizedAndNeverMatchEachOtherOrAWrench()
        {
            ToolDefinition screwdriver = SatsumaAuxiliaryAssemblyTools.CreateScrewdriver();
            ToolDefinition sparkPlugWrench = SatsumaAuxiliaryAssemblyTools.CreateSparkPlugWrench();
            ToolDefinition ordinary = ScriptableObject.CreateInstance<ToolDefinition>();
            try
            {
                ordinary.Configure("test.wrench", "Test wrench", "Wrench", FastenerSize.Millimeter7);
                Assert.That(screwdriver.ToolType, Is.EqualTo("Screwdriver"));
                Assert.That(sparkPlugWrench.ToolType, Is.EqualTo("SparkPlugWrench"));
                Assert.That(screwdriver.Size, Is.EqualTo(FastenerSize.None));
                Assert.That(sparkPlugWrench.Size, Is.EqualTo(FastenerSize.None));
                ToolCompatibilityRule screw = SatsumaAuxiliaryAssemblyTools.ScrewdriverRule();
                ToolCompatibilityRule plug = SatsumaAuxiliaryAssemblyTools.SparkPlugWrenchRule();
                Assert.That(screw.Matches(screwdriver), Is.True);
                Assert.That(plug.Matches(sparkPlugWrench), Is.True);
                Assert.That(screw.Matches(sparkPlugWrench), Is.False);
                Assert.That(plug.Matches(screwdriver), Is.False);
                Assert.That(screw.Matches(ordinary), Is.False);
                Assert.That(plug.Matches(ordinary), Is.False);
                Assert.That(ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter7)
                    .Matches(screwdriver), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(screwdriver);
                Object.DestroyImmediate(sparkPlugWrench);
                Object.DestroyImmediate(ordinary);
            }
        }
    }
}
