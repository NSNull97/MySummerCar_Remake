using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaDynamicAssemblySaveTests
    {
        [Test]
        public void DescriptorPreflightWorksBeforeMaterializationButApplyRequiresExactRegistration()
        {
            using var f = new Fixture();
            VehicleAssemblySaveData target = f.ReplacementSave(StableEntityId.New().Value);
            string before = JsonUtility.ToJson(f.Assembly.CaptureSaveData());
            Assert.That(f.Assembly.ValidateSaveDataForRestore(target).Succeeded, Is.True);
            Assert.That(f.Assembly.RestoreSaveData(target).Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(before));
            PartInstance replacement = f.Register(target.dynamicParts[0].part.stableEntityId);
            Rigidbody sameBody = replacement.Body;
            MountPointRuntime sameMount = f.Assembly.Graph.Mounts[0];
            AssemblyOperationResult result = f.Assembly.RestoreSaveData(target);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(f.Assembly.Parts, Has.Length.EqualTo(2));
            Assert.That(f.Assembly.AllRuntimeParts, Has.Length.EqualTo(3));
            Assert.That(f.Assembly.Graph.Mounts[0], Is.SameAs(sameMount));
            Assert.That(sameMount.InstalledPart, Is.SameAs(replacement));
            Assert.That(replacement.Body, Is.SameAs(sameBody));
            Assert.That(f.Stock.IsInstalled, Is.False);
            Assert.That(sameMount.Fasteners[0].Stage, Is.EqualTo(3));
            Assert.That(f.Assembly.CaptureSaveData().dynamicParts[0].part.stableEntityId, Is.EqualTo(replacement.StableId.Value));
        }

        [Test]
        public void MaterializedWrapperCannotApplyAnotherAllowedDefinitionUnderTheSameIdentity()
        {
            using var f = new Fixture();
            PartInstance part = f.Register(StableEntityId.New().Value);
            PartDefinition alternative = f.AllowAlternativeDefinition();
            VehicleAssemblySaveData target = f.Assembly.CaptureSaveData();
            target.dynamicParts[0].part.partDefinitionId = alternative.DefinitionId;
            string before = JsonUtility.ToJson(f.Assembly.CaptureSaveData());
            Assert.That(f.Assembly.ValidateSaveDataForRestore(target).Succeeded, Is.True);
            Assert.That(f.Assembly.RestoreSaveData(target).Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(before));
            Assert.That(f.Assembly.AllRuntimeParts.Last(), Is.SameAs(part));
        }

        [Test]
        public void RegistrationPreservesBaseStateAndRejectsDuplicateIdentity()
        {
            using var f = new Fixture();
            VehicleAssemblySaveData before = f.Assembly.CaptureSaveData();
            MountPointRuntime sameMount = f.Assembly.Graph.Mounts[0];
            PartInstance first = f.Register(StableEntityId.New().Value);
            f.Register(StableEntityId.New().Value);
            Assert.That(f.Assembly.TryRegisterDynamicPart(first, "item.test-replacement", out _), Is.False);
            VehicleAssemblySaveData after = f.Assembly.CaptureSaveData();
            Assert.That(after.parts.Select(value => value.stableEntityId), Is.EqualTo(before.parts.Select(value => value.stableEntityId)));
            Assert.That(after.fasteners[0].stage, Is.EqualTo(before.fasteners[0].stage));
            Assert.That(after.fastenerGroups[0].isBolted, Is.EqualTo(before.fastenerGroups[0].isBolted));
            Assert.That(f.Assembly.Graph.Mounts[0], Is.SameAs(sameMount));
            Assert.That(after.dynamicParts, Has.Length.EqualTo(2));
            Assert.Throws<InvalidOperationException>(() => f.Assembly.Configure(f.Assembly.Parts,
                f.Assembly.MountPoints, Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), f.Root.transform));
        }

        [Test]
        public void RestoreRegistryCanSuspendAttachedReplacementAndReturnTheSameWrapperBeforeRollback()
        {
            using var f = new Fixture();
            VehicleAssemblySaveData original = f.Assembly.CaptureSaveData();
            PartInstance part = f.Register(StableEntityId.New().Value);
            Assert.That(f.Assembly.RestoreSaveData(f.ReplacementSave(part.StableId.Value)).Succeeded, Is.True);
            VehicleAssemblySaveData checkpoint = f.Assembly.CaptureSaveData();
            DynamicPartRegistration[] registrations = f.Assembly.CaptureDynamicPartRegistrations();
            MountPointRuntime sameMount = f.Assembly.Graph.Mounts[0];
            Assert.That(f.Assembly.TryUnregisterDynamicPart(part, out _), Is.False);
            Assert.That(f.Assembly.TrySetDynamicPartRegistrationsForRestore(Array.Empty<DynamicPartRegistration>(), out string failure), Is.True, failure);
            Assert.That(part, Is.Not.Null);
            Assert.That(part.IsInstalled, Is.False);
            Assert.That(f.Assembly.RestoreSaveData(original).Succeeded, Is.True);
            Assert.That(f.Assembly.TrySetDynamicPartRegistrationsForRestore(registrations, out failure), Is.True, failure);
            Assert.That(f.Assembly.RestoreSaveData(checkpoint).Succeeded, Is.True);
            Assert.That(sameMount.InstalledPart, Is.SameAs(part));
            Assert.That(f.Assembly.Graph.Mounts[0], Is.SameAs(sameMount));
            Assert.That(sameMount.Fasteners[0].Stage, Is.EqualTo(3));
        }

        [TestCase("base-id")] [TestCase("duplicate")] [TestCase("definition")]
        [TestCase("velocity")] [TestCase("lifecycle")] [TestCase("root")]
        public void InvalidDynamicDescriptorsFailWithoutChangingRuntime(string corruption)
        {
            using var f = new Fixture();
            VehicleAssemblySaveData target = f.ReplacementSave(StableEntityId.New().Value);
            DynamicAssemblyPartSaveDto entry = target.dynamicParts[0];
            switch (corruption)
            {
                case "base-id": entry.part.stableEntityId = f.Stock.StableId.Value; break;
                case "duplicate": target.dynamicParts = new[] { entry, entry }; break;
                case "definition": entry.part.partDefinitionId = "unknown.definition"; break;
                case "velocity": entry.linearVelocity.x = float.NaN; break;
                case "lifecycle": entry.part.lifecycleState = (PartLifecycleState)99; break;
                case "root": entry.part.lifecycleState = PartLifecycleState.AssemblyRoot; break;
            }
            string before = JsonUtility.ToJson(f.Assembly.CaptureSaveData());
            Assert.That(f.Assembly.ValidateSaveDataForRestore(target).Succeeded, Is.False);
            Assert.That(f.Assembly.RestoreSaveData(target).Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(before));
        }

        [Test]
        public void LooseDynamicPoseAndVelocityRoundTripUsesItsExistingBody()
        {
            using var f = new Fixture();
            PartInstance part = f.Register(StableEntityId.New().Value);
            part.transform.position = new Vector3(3, 4, 5);
            part.Body.position = part.transform.position;
            part.Body.linearVelocity = new Vector3(1, 2, 3);
            part.Body.angularVelocity = new Vector3(.1f, .2f, .3f);
            VehicleAssemblySaveData saved = f.Assembly.CaptureSaveData();
            part.Body.linearVelocity = Vector3.zero;
            part.transform.position = Vector3.zero;
            Assert.That(f.Assembly.RestoreSaveData(saved).Succeeded, Is.True);
            Assert.That(part.transform.position, Is.EqualTo(new Vector3(3, 4, 5)));
            Assert.That(part.Body.linearVelocity, Is.EqualTo(new Vector3(1, 2, 3)));
            Assert.That(part.Body.angularVelocity, Is.EqualTo(new Vector3(.1f, .2f, .3f)));
        }

        [Test]
        public void SchemaTwoEmptyExtensionPreservesExistingPartialFastenerAndLatch()
        {
            using var f = new Fixture();
            VehicleAssemblySaveData saved = f.Assembly.CaptureSaveData();
            saved.schemaVersion = 2;
            saved.dynamicParts = null;
            Assert.That(f.Assembly.RestoreSaveData(saved).Succeeded, Is.True);
            VehicleAssemblySaveData current = f.Assembly.CaptureSaveData();
            Assert.That(current.schemaVersion, Is.EqualTo(3));
            Assert.That(current.dynamicParts, Is.Empty);
            Assert.That(current.fasteners[0].stage, Is.EqualTo(3));
            Assert.That(current.fastenerGroups[0].isBolted, Is.False);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
            public GameObject Root { get; } = new GameObject("Dynamic assembly save fixture");
            public VehicleAssemblyController Assembly { get; }
            public PartInstance Stock { get; }
            private readonly PartDefinition definition;

            public Fixture()
            {
                var rootDefinition = Asset<PartDefinition>();
                rootDefinition.Configure("test.body", "body", PartCategory.Engine, 20, null, Array.Empty<PartCompatibilityRule>());
                definition = Asset<PartDefinition>();
                definition.Configure("test.replacement", "replacement", PartCategory.Engine, 1, null,
                    new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                PartInstance body = Part(rootDefinition, StableEntityId.New(), true, "");
                Stock = Part(definition, StableEntityId.New(), false, "test.mount");
                var fastener = Asset<FastenerDefinition>();
                fastener.Configure("test.bolt", "bolt", FastenerSize.Millimeter8, 8,
                    FastenerDirection.ClockwiseToTighten, true, true, ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter8));
                var mountDefinition = Asset<MountPointDefinition>();
                mountDefinition.Configure("test.mount", "mount", "test.socket", "test.body", new[] { definition.DefinitionId },
                    new MountConstraint(.1f, 30, 1, 0), 0, new[] { fastener });
                var group = new FastenerGroupDefinition(); group.Configure(new[] { fastener.DefinitionId }, 8, 6, 0);
                mountDefinition.ConfigureFastenerGroup(group);
                var socket = new GameObject("socket"); socket.transform.SetParent(body.transform, false);
                MountPointAuthoring mount = socket.AddComponent<MountPointAuthoring>();
                mount.Configure(mountDefinition, "test.mount", socket.transform, 0);
                Assembly = Root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(new[] { body, Stock }, new[] { mount }, Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), Root.transform);
                Assembly.ConfigureDynamicPartDefinitions(new[] { definition });
                Assembly.Graph.Mounts[0].Fasteners[0].TryRestore(true, true, 3);
            }

            public VehicleAssemblySaveData ReplacementSave(string stableId)
            {
                VehicleAssemblySaveData saved = Assembly.CaptureSaveData();
                PartSaveDto stock = saved.parts.Single(part => part.stableEntityId == Stock.StableId.Value);
                stock.lifecycleState = PartLifecycleState.Loose; stock.installedMountId = string.Empty;
                saved.mounts[0].installedPartStableEntityId = stableId;
                saved.dynamicParts = new[] { new DynamicAssemblyPartSaveDto
                {
                    itemDefinitionId = "item.test-replacement",
                    part = new PartSaveDto { stableEntityId = stableId, partDefinitionId = definition.DefinitionId,
                        lifecycleState = PartLifecycleState.Installed, installedMountId = "test.mount", worldRotation = Quaternion.identity },
                }};
                return saved;
            }

            public PartDefinition AllowAlternativeDefinition()
            {
                var alternative = Asset<PartDefinition>();
                alternative.Configure("test.other-replacement", "other", PartCategory.Engine, 1,
                    null, new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                Assembly.ConfigureDynamicPartDefinitions(new[] { definition, alternative });
                return alternative;
            }

            public PartInstance Register(string id)
            {
                Assert.That(StableEntityId.TryParse(id, out StableEntityId stableId), Is.True);
                PartInstance part = Part(definition, stableId, false, "");
                Assert.That(Assembly.TryRegisterDynamicPart(part, "item.test-replacement", out string failure), Is.True, failure);
                return part;
            }

            private PartInstance Part(PartDefinition partDefinition, StableEntityId id, bool root, string mount)
            {
                var owner = new GameObject(partDefinition.DefinitionId); owner.transform.SetParent(Root.transform, false);
                StableEntityIdAuthoring identity = owner.AddComponent<StableEntityIdAuthoring>(); identity.InitializeExplicitRuntimeId(id);
                Rigidbody body = owner.AddComponent<Rigidbody>(); body.useGravity = false;
                PartInstance part = owner.AddComponent<PartInstance>(); part.Configure(partDefinition, identity, body, null, root, mount);
                return part;
            }

            private T Asset<T>() where T : ScriptableObject
            { T asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }
            public void Dispose()
            { Object.DestroyImmediate(Root); foreach (ScriptableObject asset in assets) Object.DestroyImmediate(asset); }
        }
    }
}
