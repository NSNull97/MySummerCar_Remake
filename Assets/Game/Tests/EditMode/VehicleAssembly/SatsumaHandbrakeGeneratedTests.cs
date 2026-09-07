using System.Linq;
using MSC.Interaction.Query;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.NWH;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaHandbrakeGeneratedTests
    {
        [Test]
        public void GeneratedHandbrakeHasFiveBoltsCorrectLatchAndOnlyMovingLever()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            SatsumaHandbrakeController controller = prefab.GetComponent<SatsumaHandbrakeController>();
            Assert.That(controller, Is.Not.Null);
            SatsumaHandbrakeInteractionTarget target = prefab
                .GetComponentsInChildren<SatsumaHandbrakeInteractionTarget>(true).Single();
            PartInstance part = target.GetComponentInParent<PartInstance>(true);
            Assert.That(part.Definition.DefinitionId, Is.EqualTo(SatsumaHandbrakeController.PartDefinitionId));
            Assert.That(controller.LeverPivot.parent, Is.SameAs(part.transform));
            MeshFilter moving = controller.LeverPivot.GetComponentsInChildren<MeshFilter>(true).Single();
            Assert.That(AssetDatabase.GetAssetPath(moving.sharedMesh),
                Does.EndWith("/ab90c359dad610a4a8325d7a8a3c3162.asset"));
            Assert.That(part.GetComponentsInChildren<MeshFilter>(true).Length, Is.EqualTo(2),
                "The fixed base must remain separate from the one moving lever mesh.");
            MeshFilter rod = prefab.GetComponentsInChildren<MeshFilter>(true).Single(value =>
                AssetDatabase.GetAssetPath(value.sharedMesh)
                    .EndsWith("/1114fb760fb4d7a4cac87438f8bdc8a9.asset"));
            Assert.That(rod.transform.parent, Is.SameAs(prefab.transform),
                "The permanent linkage must not follow the removable lever part.");
            Assert.That(rod.gameObject.activeSelf, Is.True);
            Assert.That(rod.GetComponent<Renderer>().shadowCastingMode,
                Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            Assert.That(target.GetComponent<InteractionTargetHost>().OutlineRenderers.Single(),
                Is.SameAs(moving.GetComponent<Renderer>()));
            CapsuleCollider shape = target.GetComponent<CapsuleCollider>();
            Assert.That(shape.isTrigger, Is.True);
            Assert.That(shape.radius, Is.EqualTo(0.03f).Within(0.00001f));
            Assert.That(shape.height, Is.EqualTo(0.35f).Within(0.00001f));

            MountPointDefinition definition = prefab.GetComponentsInChildren<MountPointAuthoring>(true)
                .Single(value => value.MountId == SatsumaHandbrakeController.MountId).Definition;
            Assert.That(definition.Fasteners.Select(value => (int)value.Size),
                Is.EqualTo(new[] { 8, 8, 8, 8, 5 }));
            Assert.That(definition.FastenerGroup.BoltedOnThreshold, Is.EqualTo(6));
            Assert.That(definition.FastenerGroup.BoltedOffThreshold, Is.Zero);
            var bolts = prefab.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Where(value => value.FastenerDefinitionId.StartsWith("fastener.satsuma.handbrake."))
                .ToArray();
            Assert.That(bolts, Has.Length.EqualTo(5));
            foreach (AssemblyFastenerInteractionTarget bolt in bolts)
            {
                MeshFilter mesh = bolt.GetComponentsInChildren<MeshFilter>(true).Single();
                Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh),
                    Does.EndWith("/aec6c756751308a4d830708366ad5cdb.asset"));
            }
            SatsumaHandbrakeNwhAdapter brakes = prefab.GetComponent<SatsumaHandbrakeNwhAdapter>();
            Assert.That(brakes.Controller, Is.SameAs(controller));
            Assert.That(brakes.RearLeftWheel, Is.SameAs(brakes.Backend.Wheels[2]));
            Assert.That(brakes.RearRightWheel, Is.SameAs(brakes.Backend.Wheels[3]));
        }

        [Test]
        public void NativeVehicleSaveRestoresLeverAndAcceptsLegacyMissingField()
        {
            GameObject instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath));
            try
            {
                VehiclePersistenceBindingCore persistence = instance.GetComponent<VehiclePersistenceBindingCore>();
                Assert.That(instance.GetComponent<VehicleSimulationHost>().TryInitialize(
                    out string initializationFailure), Is.True, initializationFailure);
                VehicleAssemblyController assembly = persistence.AssemblyController;
                PartInstance part = assembly.Parts.Single(value => value.Definition.DefinitionId ==
                    SatsumaHandbrakeController.PartDefinitionId);
                MountPointAuthoring mount = assembly.MountPoints.Single(value => value.MountId ==
                    SatsumaHandbrakeController.MountId);
                part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                part.Body.position = mount.Pose.position;
                part.Body.rotation = mount.Pose.rotation;
                AssemblyOperationResult installed = assembly.TryInstall(part, mount);
                Assert.That(installed.Succeeded, Is.True, installed.Message);
                Assert.That(assembly.Graph.TryGetMount(mount.MountId, out MountPointRuntime runtime), Is.True);
                foreach (FastenerInstance fastener in runtime.Fasteners)
                    Assert.That(fastener.TryRestore(true, true, 8), Is.True);
                runtime.FastenerGroup.Reevaluate(true);

                SatsumaHandbrakeController handbrake = instance.GetComponent<SatsumaHandbrakeController>();
                Assert.That(handbrake.TryRestore(new SatsumaHandbrakeSaveDto { positionDegrees = 12f },
                    out string failure), Is.True, failure);
                Assert.That(persistence.TryCapture(out VehicleSaveRecordDto record, out failure), Is.True, failure);
                string json = JsonUtility.ToJson(record);
                handbrake.ResetState();
                Assert.That(persistence.TryRestore(JsonUtility.FromJson<VehicleSaveRecordDto>(json), out failure),
                    Is.True, failure);
                Assert.That(handbrake.PositionDegrees, Is.EqualTo(12f));
                Assert.That(handbrake.BrakeInput01, Is.EqualTo(0.6f).Within(0.00001f));
                Assert.That(handbrake.IsHeld, Is.False);

                record.handbrake = null;
                Assert.That(persistence.TryRestore(record, out failure), Is.True, failure);
                Assert.That(handbrake.PositionDegrees, Is.EqualTo(0.1f));
                Assert.That(handbrake.BrakeInput01, Is.Zero);
                record.handbrake = new SatsumaHandbrakeSaveDto { positionDegrees = float.NaN };
                Assert.That(persistence.CanRestore(record, out _), Is.False);
                Assert.That(handbrake.PositionDegrees, Is.EqualTo(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
