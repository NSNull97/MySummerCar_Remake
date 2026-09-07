using System.Linq;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaEnginePickupAuthoringTests
    {
        [Test]
        public void RefreshRepairsExistingBindingAndPriorityThenBecomesIdempotent()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                VehicleAssemblyController assembly = contents.GetComponent<VehicleAssemblyController>();
                Phase1SatsumaEnginePickupAuthoring.Configure(assembly);
                PartInstance block = assembly.Parts.Single(part => part.Definition.DefinitionId ==
                    "vehicle.satsuma.part.engine-block");
                AssemblySubassemblyPickupTarget adapter = block.GetComponent<AssemblySubassemblyPickupTarget>();
                Assert.That(adapter, Is.Not.Null);
                InteractionTargetHost host = block.GetComponent<InteractionTargetHost>();
                string bodyBefore = EditorJsonUtility.ToJson(block.Body);
                adapter.Configure(null, null);
                host.AddCapabilityFirst(block.PickupTarget);
                Assert.That(Phase1SatsumaEnginePickupAuthoring.Configure(assembly), Is.GreaterThan(0));
                Assert.That(host.TryGetCapability(out IPickupTarget pickup), Is.True);
                Assert.That(pickup, Is.SameAs(adapter));
                Assert.That(Phase1SatsumaEnginePickupAuthoring.Configure(assembly), Is.Zero);
                Assert.That(EditorJsonUtility.ToJson(block.Body), Is.EqualTo(bodyBefore));
                Assert.That(assembly.Parts.Where(part => part.Definition.Category == PartCategory.Suspension)
                    .All(part => part.GetComponent<AssemblySubassemblyPickupTarget>() == null), Is.True);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
