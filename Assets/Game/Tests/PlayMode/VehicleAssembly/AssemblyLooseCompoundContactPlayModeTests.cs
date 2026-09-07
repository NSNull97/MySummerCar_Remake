using System;
using System.Collections;
using System.Collections.Generic;
using MSC.Core.Identity;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    public sealed class AssemblyLooseCompoundContactPlayModeTests
    {
        [UnityTest]
        public IEnumerator InstalledChildColliderSupportsLooseEngineOnTable()
        {
            using (var f = new Fixture())
            {
                f.InstallChild();
                f.Step(180);
                Assert.That(f.Compound.ActiveProxyCount, Is.EqualTo(1));
                Assert.That(f.ChildShape.enabled, Is.False);
                Assert.That(f.Child.Body.isKinematic, Is.True);
                Assert.That(f.Block.Body.mass, Is.EqualTo(25f).Within(.001f));
                Assert.That(f.Block.Body.position.y, Is.InRange(.56f, .66f),
                    "Block must rest on its lower child's compound shape, not fall through to its own small collider.");
                Assert.That(f.Block.Body.linearVelocity.magnitude, Is.LessThan(.08f));
                AssemblyCompoundColliderProxy proxy = f.Block.GetComponentInChildren<AssemblyCompoundColliderProxy>();
                Assert.That(proxy.GetComponent<Collider>().attachedRigidbody, Is.SameAs(f.Block.Body));
                Assert.That(proxy.GetComponent<Collider>().bounds.min.y, Is.InRange(-.025f, .025f));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RemovingSupportingChildRestoresIndependentBodiesWithoutGhostSupport()
        {
            using (var f = new Fixture())
            {
                f.InstallChild();
                f.Step(150);
                // Move the table-supported engine aside before detaching, so the
                // loose child cannot physically remain as an incidental support.
                f.Block.Body.position += Vector3.right * 2f;
                f.Block.transform.position = f.Block.Body.position;
                f.Compound.Refresh(true);
                Assert.That(f.Assembly.TryRemove(f.Child).Succeeded, Is.True);
                f.Child.Body.position = new Vector3(-2f, .2f, 0f);
                f.Child.transform.position = f.Child.Body.position;
                f.Step(180);
                Assert.That(f.Compound.ActiveProxyCount, Is.Zero);
                Assert.That(f.ChildShape.enabled, Is.True);
                Assert.That(f.Child.Body.isKinematic, Is.False);
                Assert.That(f.Block.Body.mass, Is.EqualTo(20f));
                Assert.That(f.Child.Body.mass, Is.EqualTo(5f));
                Assert.That(f.Block.Body.position.y, Is.InRange(.075f, .14f));
                Assert.That(f.Child.Body.position.y, Is.InRange(.075f, .14f));
            }
            yield return null;
        }

        private sealed class Fixture : IDisposable
        {
            private readonly Scene scene;
            private readonly PhysicsScene physics;
            private readonly GameObject root;
            private readonly GameObject loose;
            private readonly GameObject table;
            private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
            public readonly PartInstance Block, Child;
            public readonly BoxCollider ChildShape;
            public readonly VehicleAssemblyController Assembly;
            public readonly AssemblyLooseCompoundPhysics Compound;
            private readonly MountPointAuthoring mount;

            public Fixture()
            {
                scene = SceneManager.CreateScene("compound contact " + Guid.NewGuid().ToString("N"),
                    new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                physics = scene.GetPhysicsScene();
                root = InScene("assembly controller"); loose = InScene("loose parts"); table = InScene("table");
                table.transform.position = new Vector3(0f, -.25f, 0f);
                table.AddComponent<BoxCollider>().size = new Vector3(10f, .5f, 10f);
                Block = Part("test.block", 20f, new Vector3(0f, 1.6f, 0f), Vector3.one * .2f);
                Child = Part("test.child", 5f, new Vector3(-3f, 1f, 0f), new Vector3(1f, .2f, 1f));
                ChildShape = Child.GetComponent<BoxCollider>();
                var mountDefinition = ScriptableObject.CreateInstance<MountPointDefinition>(); definitions.Add(mountDefinition);
                mountDefinition.Configure("test.mount.child", "child", "test.socket", Block.Definition.DefinitionId,
                    new[] { Child.Definition.DefinitionId }, new MountConstraint(1f, 180f, 1f, 0f),
                    0f, Array.Empty<FastenerDefinition>());
                var socket = new GameObject("socket"); socket.transform.SetParent(Block.transform);
                socket.transform.localPosition = Vector3.down * .5f;
                mount = socket.AddComponent<MountPointAuthoring>(); mount.Configure(mountDefinition, "test.mount.child", socket.transform, 0);
                socket.AddComponent<AssemblyOwnedMountAuthoring>().Configure(Block);
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(new[] { Block, Child }, new[] { mount }, Array.Empty<AssemblyDependency>(),
                    Array.Empty<ToolDefinition>(), loose.transform);
                Compound = root.AddComponent<AssemblyLooseCompoundPhysics>();
                Compound.Configure(Assembly, new[]
                {
                    new AssemblyCompoundShapeBinding(Block, new Collider[] { Block.GetComponent<BoxCollider>() }),
                    new AssemblyCompoundShapeBinding(Child, new Collider[] { ChildShape }),
                });
                Compound.Refresh(true);
            }

            public void InstallChild()
            {
                Child.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                Child.Body.position = mount.Pose.position; Child.Body.rotation = mount.Pose.rotation;
                AssemblyOperationResult result = Assembly.TryInstall(Child, mount);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }

            public void Step(int count)
            {
                for (int index = 0; index < count; index++)
                {
                    Compound.Refresh();
                    Physics.SyncTransforms();
                    physics.Simulate(.02f);
                }
                Assembly.SynchronizeInstalledParts();
                Physics.SyncTransforms();
            }

            private GameObject InScene(string name)
            {
                var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go;
            }

            private PartInstance Part(string id, float mass, Vector3 position, Vector3 shapeSize)
            {
                var definition = ScriptableObject.CreateInstance<PartDefinition>(); definitions.Add(definition);
                definition.Configure(id, id, PartCategory.Engine, mass, root,
                    new[] { PartCompatibilityRule.Create("test.socket", string.Empty) });
                GameObject go = InScene(id); go.transform.SetParent(loose.transform); go.transform.position = position;
                var identity = go.AddComponent<StableEntityIdAuthoring>(); identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = go.AddComponent<Rigidbody>(); body.mass = mass; body.constraints = RigidbodyConstraints.FreezeRotation;
                go.AddComponent<BoxCollider>().size = shapeSize; body.centerOfMass = Vector3.zero;
                var part = go.AddComponent<PartInstance>(); part.Configure(definition, identity, body, null, false, string.Empty);
                return part;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(loose); Object.DestroyImmediate(table);
                foreach (ScriptableObject definition in definitions) Object.DestroyImmediate(definition);
                SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
