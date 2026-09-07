using System;
using System.Linq;
using MSC.Interaction.Query;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaIgnitionAuthoringTests
    {
        [Test]
        public void GeneratedIgnitionHasOnePhysicalTargetAndIndependentInputBridge()
        {
            GameObject prefab = Prefab();
            var controller = prefab.GetComponent<SatsumaIgnitionController>();
            var adapter = prefab.GetComponent<SatsumaIgnitionInputAdapter>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(adapter, Is.Not.Null);
            Assert.That(prefab.GetComponents<SatsumaIgnitionController>(), Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponent<VehicleSimulationHost>().InputSourceComponent, Is.SameAs(adapter));
            Assert.That(prefab.GetComponent<VehicleInputRouter>().enabled, Is.False);
            Assert.That(prefab.GetComponent<VehiclePersistenceBinding>().IgnitionController, Is.SameAs(controller));
            var targets = prefab.GetComponentsInChildren<SatsumaIgnitionInteractionTarget>(true);
            Assert.That(targets, Has.Length.EqualTo(1));
            var target = targets.Single();
            PartInstance column = target.transform.parent.GetComponent<PartInstance>();
            Assert.That(column.Definition.DefinitionId, Is.EqualTo(Phase1SatsumaIgnitionAuthoring.ColumnPartId));
            Assert.That(Vector3.Distance(target.transform.localPosition,
                Phase1SatsumaIgnitionAuthoring.TriggerPosition), Is.LessThan(0.000001f));
            Assert.That(Quaternion.Angle(target.transform.localRotation,
                Phase1SatsumaIgnitionAuthoring.TriggerRotation), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(target.transform.localScale,
                Phase1SatsumaIgnitionAuthoring.TriggerScale), Is.LessThan(0.000001f));
            SphereCollider shape = target.GetComponent<SphereCollider>();
            Assert.That(shape.isTrigger, Is.True);
            Assert.That(shape.radius, Is.EqualTo(0.7f));
            Assert.That(shape.center, Is.EqualTo(new Vector3(0f, 0.5f, 0f)));
            Assert.That(target.GetComponent<InteractionTargetHost>().OutlineRenderers.Count, Is.EqualTo(2));
        }

        [Test]
        public void ReviewedMeshesAreRuntimeWrappedNotReferenceOnlyAndKeepColumnRelativePoses()
        {
            GameObject prefab = Prefab();
            MeshFilter[] meshes = prefab.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter socket = meshes.Single(mesh => AssetDatabase.GetAssetPath(mesh.sharedMesh) ==
                Phase1SatsumaIgnitionAuthoring.GeneratedRoot + "/Meshes/" + Phase1SatsumaIgnitionAuthoring.SocketMeshGuid + ".asset");
            MeshFilter key = meshes.Single(mesh => AssetDatabase.GetAssetPath(mesh.sharedMesh) ==
                Phase1SatsumaIgnitionAuthoring.GeneratedRoot + "/Meshes/" + Phase1SatsumaIgnitionAuthoring.KeyMeshGuid + ".asset");
            Assert.That(socket.transform.parent, Is.SameAs(key.transform.parent));
            Transform presentation = socket.transform.parent.parent;
            Assert.That(presentation.parent.GetComponent<PartInstance>().Definition.DefinitionId,
                Is.EqualTo(Phase1SatsumaIgnitionAuthoring.ColumnPartId));
            Assert.That(Vector3.Distance(presentation.localPosition, Phase1SatsumaIgnitionAuthoring.LockPosition),
                Is.LessThan(0.000001f));
            Assert.That(Quaternion.Angle(presentation.localRotation, Phase1SatsumaIgnitionAuthoring.LockRotation),
                Is.LessThan(0.001f));
            Assert.That(socket.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(socket.transform.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(Vector3.Distance(key.transform.localPosition,
                new Vector3(-0.00013179361f, -0.000003356341f, 0.0015042042f)), Is.LessThan(0.000001f));
            Assert.That(key.gameObject.activeSelf, Is.False);
            Assert.That(socket.GetComponent<Collider>(), Is.Null);
            Assert.That(key.GetComponent<Collider>(), Is.Null);
            foreach (MeshFilter mesh in new[] { key, socket })
            {
                Assert.That(AssetDatabase.GetAssetPath(mesh.sharedMesh), Does.Not.Contain("ReferenceOnly"));
                Assert.That(AssetDatabase.GetAssetPath(mesh.GetComponent<Renderer>().sharedMaterial),
                    Is.EqualTo(Phase1SatsumaIgnitionAuthoring.GeneratedRoot + "/Materials/" +
                        Phase1SatsumaIgnitionAuthoring.AtlasMaterialGuid + ".mat"));
            }
        }

        [Test]
        public void RepeatedAuthoringIsNoOpAndDriftedInputOwnerIsRejected()
        {
            using var fixture = new Fixture();
            Transform[] before = fixture.Root.GetComponentsInChildren<Transform>(true);
            var poses = before.Select(value => (value.localPosition, value.localRotation, value.localScale)).ToArray();
            Assert.That(Phase1SatsumaIgnitionAuthoring.ApplyBindings(fixture.Root,
                Phase1SatsumaIgnitionAuthoring.GeneratedRoot), Is.False);
            Assert.That(fixture.Root.GetComponentsInChildren<Transform>(true), Is.EqualTo(before));
            Assert.That(before.Select(value => (value.localPosition, value.localRotation, value.localScale)).ToArray(),
                Is.EqualTo(poses));
            var serialized = new SerializedObject(fixture.Root.GetComponent<VehicleSimulationHost>());
            serialized.FindProperty("inputSourceComponent").objectReferenceValue = fixture.Root.GetComponent<VehicleInputRouter>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.Throws<System.IO.InvalidDataException>(() => Phase1SatsumaIgnitionAuthoring.ApplyBindings(
                fixture.Root, Phase1SatsumaIgnitionAuthoring.GeneratedRoot));
            Assert.That(fixture.Root.GetComponents<SatsumaIgnitionController>(), Has.Length.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExistingIgnitionBoolRoundTripsThroughControllerWithoutRequiringKeyOrColumn(bool ignitionOn)
        {
            using var fixture = new Fixture();
            var persistence = fixture.Root.GetComponent<VehiclePersistenceBinding>();
            var simulation = fixture.Root.GetComponent<VehicleSimulationHost>();
            Assert.That(simulation.TryInitialize(out string failure), Is.True, failure);
            var access = new SatsumaKeyAccessState();
            access.SetAccess(false);
            persistence.BindKeyAccess(access);
            var controller = persistence.IgnitionController;
            controller.RestorePersistentState(ignitionOn);
            // An opposite router value must not override physical intent.
            fixture.Root.GetComponent<VehicleInputRouter>().RestorePersistentState(!ignitionOn);
            Assert.That(persistence.TryCapture(out VehicleSaveRecordDto record, out failure), Is.True, failure);
            Assert.That(record.ignitionOn, Is.EqualTo(ignitionOn));
            controller.RestorePersistentState(!ignitionOn);
            Assert.That(persistence.TryRestore(JsonUtility.FromJson<VehicleSaveRecordDto>(JsonUtility.ToJson(record)),
                out failure), Is.True, failure);
            Assert.That(controller.IgnitionOn, Is.EqualTo(ignitionOn));
            Assert.That(controller.IsHeld, Is.False);
            Assert.That(access.HasAccess, Is.False, "Vehicle restore does not own logical key possession.");
            Assert.That(fixture.Root.GetComponent<VehicleInputRouter>().enabled, Is.False);
            Assert.That(simulation.InputSource.ConsumeFixedInput(0).StarterRequested, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LegacyRouterOnlyBindingStillCapturesItsBool(bool ignitionOn)
        {
            using var fixture = new Fixture();
            var persistence = fixture.Root.GetComponent<VehiclePersistenceBinding>();
            Assert.That(fixture.Root.GetComponent<VehicleSimulationHost>().TryInitialize(out string failure), Is.True, failure);
            persistence.ConfigureIgnition(null);
            fixture.Root.GetComponent<VehicleInputRouter>().RestorePersistentState(ignitionOn);
            Assert.That(persistence.TryCapture(out VehicleSaveRecordDto record, out failure), Is.True, failure);
            Assert.That(record.ignitionOn, Is.EqualTo(ignitionOn));
        }

        private static GameObject Prefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null, "Private Satsuma baseline is required for the executed C2 integration suite.");
            return prefab;
        }

        private sealed class Fixture : IDisposable
        {
            private readonly PartInstance[] parts;
            public GameObject Root { get; }

            public Fixture()
            {
                Root = Object.Instantiate(Prefab());
                var assembly = Root.GetComponent<VehicleAssemblyController>();
                parts = assembly.Parts.ToArray();
                assembly.Initialize();
            }

            public void Dispose()
            {
                foreach (PartInstance part in parts)
                    if (part != null && !part.transform.IsChildOf(Root.transform)) Object.DestroyImmediate(part.gameObject);
                Object.DestroyImmediate(Root);
            }
        }
    }
}
