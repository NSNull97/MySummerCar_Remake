using System;
using System.IO;
using System.Linq;
using MSC.Items;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed class VehicleItemOwnershipSaveTests
    {
        private const string ItemId = "00000000000000000000000000000011";
        private const string SecondItemId = "00000000000000000000000000000012";
        private const string RootId = "00000000000000000000000000000021";
        private const string VehicleId = "00000000000000000000000000000031";
        private PartDefinition definition;

        [SetUp]
        public void SetUp()
        {
            definition = ScriptableObject.CreateInstance<PartDefinition>();
            definition.Configure("vehicle.satsuma.part.light-bulb", "test bulb", PartCategory.Electrical, .02f,
                null, Array.Empty<PartCompatibilityRule>());
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(definition);

        [TestCase(1)]
        [TestCase(2)]
        public void LegacyPurchasesKeepIdsConditionWorldPoseAndOldLatchSchema(int legacySchema)
        {
            ItemDomainSaveDto original = ItemDomain();
            ItemDomainSaveDto items = original.DeepClone();
            WorldEntityDomainSaveDto world = WorldDomain();
            VehicleDomainSaveDto vehicles = VehicleDomain(legacySchema);
            string originalItemJson = JsonUtility.ToJson(original);
            var owners = VehicleItemSaveRestorePlanFactory.NormalizeAndValidate(items, world, vehicles, Resolve);
            Assert.That(owners.Keys, Is.EquivalentTo(new[] { ItemId, SecondItemId }));
            Assert.That(world.entities, Is.Empty);
            Assert.That(vehicles.vehicles[0].assembly.schemaVersion, Is.EqualTo(legacySchema));
            Assert.That(vehicles.vehicles[0].assembly.parts.Single().stableEntityId, Is.EqualTo(RootId));
            DynamicAssemblyPartSaveDto first = vehicles.vehicles[0].assembly.dynamicParts.Single(value => value.part.stableEntityId == ItemId);
            Assert.That(first.itemDefinitionId, Is.EqualTo("item.light-bulb"));
            Assert.That(first.part.partDefinitionId, Is.EqualTo(definition.DefinitionId));
            Assert.That(first.part.lifecycleState, Is.EqualTo(PartLifecycleState.Loose));
            Assert.That(first.part.installedMountId, Is.Empty);
            Assert.That(first.part.worldPosition, Is.EqualTo(new Vector3(20, 3, 9)));
            Assert.That(first.linearVelocity, Is.EqualTo(Vector3.right));
            Assert.That(items.instances[0].state.condition, Is.EqualTo(63));
            Assert.That(JsonUtility.ToJson(original), Is.EqualTo(originalItemJson));
        }

        [TestCase("world-owner")]
        [TestCase("missing-item")]
        [TestCase("consumed")]
        [TestCase("definition")]
        [TestCase("missing-descriptor")]
        [TestCase("duplicate")]
        [TestCase("nan")]
        [TestCase("mount-mismatch")]
        [TestCase("part-definition")]
        [TestCase("optional-state")]
        [TestCase("cross-vehicle")]
        public void CurrentCrossDomainCorruptionFailsClosed(string corruption)
        {
            ItemDomainSaveDto items = ItemDomain();
            VehicleDomainSaveDto vehicles = VehicleDomain(3);
            vehicles.vehicles[0].assembly.dynamicParts = items.instances.Select(value => new DynamicAssemblyPartSaveDto
            {
                itemDefinitionId = "item.light-bulb", part = new PartSaveDto
                { stableEntityId = value.state.stableEntityId, partDefinitionId = definition.DefinitionId, lifecycleState = PartLifecycleState.Loose },
            }).ToArray();
            var world = new WorldEntityDomainSaveDto();
            DynamicAssemblyPartSaveDto first = vehicles.vehicles[0].assembly.dynamicParts[0];
            switch (corruption)
            {
                case "world-owner": world = WorldDomain(); break;
                case "missing-item": items.instances = items.instances.Skip(1).ToArray(); break;
                case "consumed": items.instances[0].state.isConsumed = true; break;
                case "definition": first.itemDefinitionId = "item.oil-filter"; break;
                case "missing-descriptor": vehicles.vehicles[0].assembly.dynamicParts = new[] { first }; break;
                case "duplicate": vehicles.vehicles[0].assembly.dynamicParts = new[] { first, first }; break;
                case "nan": first.linearVelocity = new Vector3(float.NaN, 0, 0); break;
                case "mount-mismatch": first.part.lifecycleState = PartLifecycleState.Installed; first.part.installedMountId = "mount.test"; break;
                case "part-definition": first.part.partDefinitionId = "vehicle.satsuma.part.spark-plug"; break;
                case "optional-state": first.part.hasCamshaftTiming = true; break;
                case "cross-vehicle":
                    VehicleSaveRecordDto other = VehicleDomain(3).vehicles[0];
                    other.stableVehicleId = "00000000000000000000000000000032";
                    other.assembly.parts[0].stableEntityId = "00000000000000000000000000000022";
                    other.assembly.dynamicParts = new[] { first };
                    vehicles.vehicles = new[] { vehicles.vehicles[0], other };
                    break;
            }
            Assert.Throws<InvalidDataException>(() => VehicleItemSaveRestorePlanFactory.NormalizeAndValidate(items, world, vehicles, Resolve));
        }

        [Test]
        public void LegacyMissingWorldPoseCannotInventAReplacementOrTeleportFromLogicalPosition()
        {
            Assert.Throws<InvalidDataException>(() => VehicleItemSaveRestorePlanFactory.NormalizeAndValidate(
                ItemDomain(), new WorldEntityDomainSaveDto(), VehicleDomain(2), Resolve));
        }

        [Test]
        public void CurrentValidOwnershipIsIdempotentAndPreservesUnrelatedWorldRecords()
        {
            ItemDomainSaveDto items = ItemDomain();
            WorldEntityDomainSaveDto world = WorldDomain();
            VehicleDomainSaveDto vehicles = VehicleDomain(2);
            VehicleItemSaveRestorePlanFactory.NormalizeAndValidate(items, world, vehicles, Resolve);
            vehicles.vehicles[0].assembly.schemaVersion = 3;
            world.entities = new[] { new WorldEntityStateDto { stableEntityId = "00000000000000000000000000000099" } };
            string before = JsonUtility.ToJson(vehicles);
            VehicleItemSaveRestorePlanFactory.NormalizeAndValidate(items, world, vehicles, Resolve);
            Assert.That(JsonUtility.ToJson(vehicles), Is.EqualTo(before));
            Assert.That(world.entities.Single().stableEntityId, Does.EndWith("99"));
        }

        [Test]
        public void ForwardDocumentGatePreservesAllDomainPayloadsAndSource()
        {
            var source = new SaveDocument { Header = new SaveHeader { DocumentVersion = 17 },
                Domains = new[] { new SaveDomainEnvelope { DomainId = "vehicle.satsuma", SchemaVersion = 1, PayloadJson = "unaltered-source-payload" } } };
            SaveDocument migrated = new SatsumaDynamicAssemblySaveMigration().Migrate(source, new UnresolvedContentReport());
            Assert.That(migrated.Header.DocumentVersion, Is.EqualTo(18));
            Assert.That(source.Header.DocumentVersion, Is.EqualTo(17));
            Assert.That(migrated.Domains[0].PayloadJson, Is.EqualTo(source.Domains[0].PayloadJson));
            Assert.That(migrated.Domains[0], Is.Not.SameAs(source.Domains[0]));
        }

        [Test]
        public void DirectWorldRegistrationHonorsOwnershipAndRollbackRetainsTheSameWrapper()
        {
            var root = new GameObject("Owned pickup save fixture");
            try
            {
                StableEntityId.TryParse(ItemId, out StableEntityId stableId);
                StableEntityIdAuthoring identity = root.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(stableId);
                Rigidbody body = root.AddComponent<Rigidbody>();
                PhysicsPickupTarget pickup = root.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(body, identity, "test", 5f);
                var deferred = new DeferredStableEntityStore();
                var staged = new DeferredStableEntityStore();
                var participant = new WorldEntitySaveParticipant(deferred);
                participant.RegisterTarget(pickup);
                object checkpoint = participant.CaptureCheckpoint();
                var payload = new DeferredStableEntityPayload { OwnerDomainId = WorldEntitySaveParticipant.DomainId,
                    StableEntityId = ItemId, SchemaVersion = 2, PayloadJson = "{}" };
                deferred.Enqueue(payload);
                staged.Enqueue(payload);
                participant.ConfigureExternalOwnership(id => id == ItemId);
                participant.RegisterTarget(pickup);
                participant.TransferToExternalOwnership(ItemId, staged);
                Assert.That(participant.TryResolve(ItemId, out _), Is.False);
                Assert.That(deferred.Count, Is.Zero);
                Assert.That(staged.Count, Is.Zero);
                participant.ConfigureExternalOwnership(null);
                participant.Rollback(checkpoint);
                Assert.That(participant.TryResolve(ItemId, out PhysicsPickupTarget restored), Is.True);
                Assert.That(restored, Is.SameAs(pickup));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void DirectWorldRegistrationAlsoExcludesExistingPartAncestorWithoutAnItemResolver()
        {
            var root = new GameObject("Part-owned pickup save fixture");
            try
            {
                StableEntityId.TryParse(ItemId, out StableEntityId stableId);
                StableEntityIdAuthoring identity = root.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(stableId);
                Rigidbody body = root.AddComponent<Rigidbody>();
                PhysicsPickupTarget pickup = root.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(body, identity, "test", 5f);
                root.AddComponent<PartInstance>().Configure(definition, identity, body, pickup, false, string.Empty);
                var participant = new WorldEntitySaveParticipant(new DeferredStableEntityStore());
                participant.RegisterTarget(pickup);
                Assert.That(participant.TryResolve(ItemId, out _), Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private PartDefinition Resolve(string owner, string item) => owner == VehicleId && item == "item.light-bulb" ? definition : null;

        private static ItemDomainSaveDto ItemDomain() => new ItemDomainSaveDto
        {
            instances = new[] { ItemId, SecondItemId }.Select(id => new ItemRuntimeSaveRecord
            { state = new ItemInstanceState { stableEntityId = id, definitionId = "item.light-bulb", condition = 63 },
                materializationPosition = new Vector3(-100, 2, 7), sourceCellId = "shop", isCanonicalPlacement = false }).ToArray(),
        };

        private static WorldEntityDomainSaveDto WorldDomain() => new WorldEntityDomainSaveDto
        {
            entities = new[] { ItemId, SecondItemId }.Select(id => new WorldEntityStateDto
            { stableEntityId = id, worldPosition = new Vector3(20, 3, 9), linearVelocity = Vector3.right, useGravity = true }).ToArray(),
        };

        private static VehicleDomainSaveDto VehicleDomain(int schema) => new VehicleDomainSaveDto
        {
            vehicles = new[] { new VehicleSaveRecordDto { stableVehicleId = VehicleId, assembly = new VehicleAssemblySaveData
            { schemaVersion = schema, parts = new[] { new PartSaveDto { stableEntityId = RootId,
                partDefinitionId = "vehicle.satsuma.part.body-shell", lifecycleState = PartLifecycleState.AssemblyRoot } } } } },
        };
    }
}
