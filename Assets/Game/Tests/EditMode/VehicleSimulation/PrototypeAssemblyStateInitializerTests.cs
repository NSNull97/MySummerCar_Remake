using System;
using System.Collections.Generic;
using MSC.Core.Identity;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleSimulation
{
    public sealed class PrototypeAssemblyStateInitializerTests
    {
        [Test]
        public void FullyInstalledPrototypeReconstructsOnlyOccupiedFastenerLatchesAndCanRepeat()
        {
            using var fixture = new Fixture();
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Assert.That(fixture.Initializer.TryInitialize(out string failure), Is.True, failure);
                VehicleAssemblySaveData data = fixture.Assembly.CaptureSaveData();
                Assert.That(data.schemaVersion, Is.EqualTo(VehicleAssemblySaveData.CurrentSchemaVersion));
                Assert.That(data.fastenerGroups.Length, Is.EqualTo(3));
                Assert.That(data.fastenerGroups[0].isBolted, Is.True,
                    "The occupied maximum-stage bolt group must not retain its initial false latch.");
                Assert.That(data.fastenerGroups[1].isBolted, Is.False,
                    "An occupied snap-on mount has no bolted latch.");
                Assert.That(data.fastenerGroups[2].isBolted, Is.False,
                    "The unused compatible mount must stay unoccupied and unbolted.");
                Assert.That(data.fasteners[0].stage, Is.EqualTo(8));
                Assert.That(data.fasteners[1].stage, Is.Zero);
                Assert.That(fixture.Assembly.RestoreSaveData(data).Succeeded, Is.True);
            }
        }

        [Test]
        public void ContradictorySavedLatchStillFailsTheUnchangedPublicRestoreValidator()
        {
            using var fixture = new Fixture();
            Assert.That(fixture.Initializer.TryInitialize(out string failure), Is.True, failure);
            VehicleAssemblySaveData data = fixture.Assembly.CaptureSaveData();
            data.fastenerGroups[0].isBolted = false;
            AssemblyOperationResult result = fixture.Assembly.RestoreSaveData(data);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.InvalidSaveData));
            Assert.That(fixture.Assembly.CaptureSaveData().fastenerGroups[0].isBolted, Is.True);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new("prototype latch fixture");
            private readonly List<Object> definitions = new();
            public readonly VehicleAssemblyController Assembly;
            public readonly PrototypeAssemblyStateInitializer Initializer;

            public Fixture()
            {
                root.SetActive(false);
                var loose = new GameObject("loose parts");
                loose.transform.SetParent(root.transform, false);
                PartInstance chassis = Part("part.prototype.root", root, true);
                PartInstance bolted = Part("part.prototype.bolted", new GameObject("bolted"), false);
                PartInstance snap = Part("part.prototype.snap", new GameObject("snap"), false);
                bolted.transform.SetParent(loose.transform, false);
                snap.transform.SetParent(loose.transform, false);
                var bolt = New<FastenerDefinition>();
                bolt.Configure("fastener.prototype.bolt", "fixture bolt", FastenerSize.Millimeter10, 8,
                    FastenerDirection.ClockwiseToTighten, true, true,
                    ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter10));
                MountPointAuthoring first = Mount("mount.prototype.bolted", chassis, bolted, new[] { bolt });
                MountPointAuthoring second = Mount("mount.prototype.snap", chassis, snap, Array.Empty<FastenerDefinition>());
                MountPointAuthoring unused = Mount("mount.prototype.unused", chassis, bolted, new[] { bolt });
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(new[] { chassis, bolted, snap }, new[] { first, second, unused },
                    Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), loose.transform);
                Initializer = root.AddComponent<PrototypeAssemblyStateInitializer>();
                Initializer.Configure(Assembly, initializeAutomatically: false);
            }

            private PartInstance Part(string id, GameObject go, bool isRoot)
            {
                go.SetActive(false);
                PartDefinition definition = New<PartDefinition>();
                definition.Configure(id, id, PartCategory.Engine, 1f, null,
                    new[] { PartCompatibilityRule.Create("prototype-latch-fixture") });
                var identity = go.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = go.AddComponent<Rigidbody>();
                body.useGravity = false;
                var part = go.AddComponent<PartInstance>();
                part.Configure(definition, identity, body, null, isRoot, string.Empty);
                return part;
            }

            private MountPointAuthoring Mount(string id, PartInstance owner, PartInstance part, FastenerDefinition[] fasteners)
            {
                MountPointDefinition definition = New<MountPointDefinition>();
                definition.Configure(id, id, "prototype-latch-fixture", owner.Definition.DefinitionId,
                    new[] { part.Definition.DefinitionId }, new MountConstraint(.2f, 45f, 1f, 0f), 0f, fasteners);
                definition.ConfigureFastenerGroup(FastenerGroupDefinition.CreateCompatibility(fasteners));
                var go = new GameObject(id);
                go.transform.SetParent(owner.transform, false);
                var mount = go.AddComponent<MountPointAuthoring>();
                mount.Configure(definition, id, go.transform, 0);
                go.AddComponent<AssemblyOwnedMountAuthoring>().Configure(owner);
                return mount;
            }

            private T New<T>() where T : ScriptableObject
            {
                T value = ScriptableObject.CreateInstance<T>();
                definitions.Add(value);
                return value;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                foreach (Object definition in definitions) Object.DestroyImmediate(definition);
            }
        }
    }
}
