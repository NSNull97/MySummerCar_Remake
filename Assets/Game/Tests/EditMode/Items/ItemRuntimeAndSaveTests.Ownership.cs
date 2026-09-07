using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MSC.Interaction.Carrying;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using MSC.Items.Presentation;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.ItemsIntegration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Items.Tests.EditMode
{
    public sealed partial class ItemRuntimeAndSaveTests
    {
        [Test]
        public void PurchasedMechanicalWearKeepsItemAuthorityAndDoesNotResetOtherState()
        {
            WorldItemInstance item = SpawnOwnedTestItem("item.alternator-belt", "mechanical.wear");
            var binding = item.gameObject.AddComponent<ItemPartPresentationBinding>(); binding.BindCondition(item);
            ItemInstanceState before = item.State;
            Assert.That(binding.TryApplyWear(12.5f), Is.True);
            Assert.That(item.ConditionPercent, Is.EqualTo(before.condition - 12.5f));
            Assert.That(item.State.content, Is.EqualTo(before.content));
            Assert.That(item.StableId.Value, Is.EqualTo(before.stableEntityId));
            Assert.That(item.GetComponent<AssemblyMechanicalConditionState>(), Is.Null);
            Assert.That(binding.TryApplyWear(-2f), Is.False);
            binding.TryApplyWear(100f);
            Assert.That(item.IsBroken, Is.True); Assert.That(item.ConditionPercent, Is.Zero);
            item.ApplyState(before);
            Assert.That(binding.ConditionPercent, Is.EqualTo(before.condition));
            Assert.That(binding.IsBroken, Is.EqualTo(before.isBroken));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ItemRestoreTransactionRollbackPreservesInactiveWrapperTransformPose(
            bool inactiveThroughParent)
        {
            WorldItemInstance item = SpawnOwnedTestItem(
                "item.spark-plug", "journal.inactive-pose");
            Rigidbody body = item.GetComponent<Rigidbody>();
            body.isKinematic = true;
            var owner = new GameObject("Inactive item rollback owner");
            try
            {
                if (inactiveThroughParent)
                {
                    owner.SetActive(false);
                    item.transform.SetParent(owner.transform, true);
                }
                else
                {
                    item.gameObject.SetActive(false);
                }

                Vector3 expectedPosition = new Vector3(13f, 2f, -7f);
                Quaternion expectedRotation = Quaternion.Euler(19f, 127f, -11f);
                item.transform.SetPositionAndRotation(expectedPosition, expectedRotation);
                Assert.That(item.gameObject.activeInHierarchy, Is.False);
                Assert.That(item.gameObject.activeSelf, Is.EqualTo(inactiveThroughParent));

                ItemDynamicRestoreTransaction transaction =
                    fixture.Runtime.BeginDynamicRestoreTransaction();
                item.transform.SetPositionAndRotation(
                    new Vector3(-4f, 8f, 6f), Quaternion.Euler(-8f, 23f, 41f));
                transaction.Rollback();

                Assert.That(fixture.Runtime.TryGetInstance(
                    item.StableId.Value, out WorldItemInstance restored), Is.True);
                Assert.That(restored, Is.SameAs(item));
                Assert.That(item.gameObject.activeInHierarchy, Is.False);
                Assert.That(item.gameObject.activeSelf, Is.EqualTo(inactiveThroughParent));
                Assert.That(item.transform.parent,
                    Is.EqualTo(inactiveThroughParent ? owner.transform : null));
                Assert.That(Vector3.Distance(item.transform.position, expectedPosition),
                    Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(item.transform.rotation, expectedRotation),
                    Is.LessThan(0.001f));
            }
            finally
            {
                // Keep the retained item available for the fixture's normal disposal.
                if (item != null && item.transform.parent == owner.transform)
                {
                    item.gameObject.SetActive(false);
                    item.transform.SetParent(null, true);
                }
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void ItemRestoreTransactionRollbackKeepsOriginalWrapperAndRemovesIntroducedItem()
        {
            WorldItemInstance original = SpawnOwnedTestItem("item.spark-plug", "journal.original", "source.old");
            Rigidbody body = original.GetComponent<Rigidbody>();
            body.position = new Vector3(4f, 2f, 3f);
            body.rotation = Quaternion.Euler(17f, 43f, -8f);
            body.linearVelocity = new Vector3(1f, 0f, 2f);
            body.angularVelocity = new Vector3(.25f, -.5f, .125f);
            Vector3 position = body.position;
            Quaternion rotation = body.rotation;
            fixture.Runtime.SetExternalOwnershipPlan(new[] { original.StableId.Value });
            ItemDynamicRestoreTransaction transaction = fixture.Runtime.BeginDynamicRestoreTransaction();
            fixture.Runtime.SetExternalOwnershipPlan(Array.Empty<string>());
            Assert.That(fixture.Runtime.TryRemoveDynamic(original), Is.True);
            Assert.That(original, Is.Not.Null, "Removal during restore must retain the wrapper.");
            Assert.That(original.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Runtime.TryGetInstance(original.StableId.Value, out _), Is.False);
            WorldItemInstance introduced = SpawnOwnedTestItem("item.spark-plug", "journal.introduced");
            string introducedId = introduced.StableId.Value;
            transaction.RestoreRetainedInstances();
            Assert.That(fixture.Runtime.TryGetInstance(original.StableId.Value, out WorldItemInstance restored), Is.True);
            Assert.That(restored, Is.SameAs(original));
            transaction.Rollback();
            Assert.That(fixture.Runtime.TryGetInstance(introducedId, out _), Is.False);
            Assert.That(introduced == null, Is.True);
            Assert.That(original.gameObject.activeSelf, Is.True);
            Assert.That(body.position, Is.EqualTo(position));
            Assert.That(original.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(body.rotation, rotation), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(original.transform.rotation, rotation), Is.LessThan(.001f));
            Assert.That(body.linearVelocity, Is.EqualTo(new Vector3(1f, 0f, 2f)));
            Assert.That(body.angularVelocity, Is.EqualTo(new Vector3(.25f, -.5f, .125f)));
            Assert.That(fixture.Runtime.TryGetSourceCellId(original.StableId.Value, out string source), Is.True);
            Assert.That(source, Is.EqualTo("source.old"));
            Assert.That(fixture.Runtime.CaptureExternalOwnershipPlan(), Is.EqualTo(new[] { original.StableId.Value }));
        }

        [Test]
        public void ItemRestoreCommitDestroysRetiredWrapperAndPublishesRemovalOnce()
        {
            WorldItemInstance item = SpawnOwnedTestItem("item.spark-plug", "journal.commit");
            int removed = 0;
            fixture.Runtime.InstanceRemoved += observed => { if (observed == item) removed++; };
            ItemDynamicRestoreTransaction transaction = fixture.Runtime.BeginDynamicRestoreTransaction();
            Assert.That(fixture.Runtime.TryRemoveDynamic(item), Is.True);
            Assert.That(removed, Is.EqualTo(1));
            Assert.That(item == null, Is.False);
            transaction.Commit();
            Assert.That(item == null, Is.True);
            Assert.That(removed, Is.EqualTo(1));
            Assert.DoesNotThrow(() => fixture.Runtime.BeginDynamicRestoreTransaction().Rollback());
        }

        [Test]
        public void ThrowingRemovalObserverStillLeavesOriginalItemAvailableForRollback()
        {
            WorldItemInstance item = SpawnOwnedTestItem("item.spark-plug", "journal.throw");
            Action<WorldItemInstance> throwing = _ => throw new InvalidOperationException("Injected removal failure");
            fixture.Runtime.InstanceRemoved += throwing;
            ItemDynamicRestoreTransaction transaction = fixture.Runtime.BeginDynamicRestoreTransaction();
            Assert.Throws<InvalidOperationException>(() => fixture.Runtime.TryRemoveDynamic(item));
            fixture.Runtime.InstanceRemoved -= throwing;
            transaction.RestoreRetainedInstances();
            transaction.Rollback();
            Assert.That(fixture.Runtime.TryGetInstance(item.StableId.Value, out WorldItemInstance restored), Is.True);
            Assert.That(restored, Is.SameAs(item));
        }

        [Test]
        public void ExternalOwnerAndSourceMetadataExistBeforePublicItemEvents()
        {
            var owner = new TestExternalItemOwner();
            fixture.Runtime.RegisterExternalPhysicsOwner(owner);
            bool presentationObserved = false;
            bool materializedObserved = false;
            fixture.Runtime.PresentationAttached += (item, _) =>
            {
                Assert.That(owner.BoundIds.Contains(item.StableId.Value), Is.True);
                Assert.That(fixture.Runtime.IsExternallyOwned(item.StableId.Value), Is.True);
                Assert.That(fixture.Runtime.TryGetSourceCellId(item.StableId.Value, out string source), Is.True);
                Assert.That(source, Is.EqualTo("source.ready"));
                presentationObserved = true;
            };
            fixture.Runtime.InstanceMaterialized += item =>
            {
                Assert.That(owner.BoundIds.Contains(item.StableId.Value), Is.True);
                materializedObserved = true;
            };
            SpawnOwnedTestItem("item.spark-plug", "owner.events", "source.ready");
            Assert.That(presentationObserved && materializedObserved, Is.True);
        }

        [Test]
        public void DispenseOwnerFailurePreservesEveryPaidChildIdentity()
        {
            WorldItemInstance box = SpawnOwnedTestItem("item.sparkplug-box", "owner.package");
            ItemInstanceState previous = box.CaptureState();
            var owner = new TestExternalItemOwner { FailBinding = true };
            fixture.Runtime.RegisterExternalPhysicsOwner(owner);
            Assert.Throws<InvalidOperationException>(() => box.TryPerformPrimaryAction(fixture.Context));
            ItemInstanceState after = box.CaptureState();
            Assert.That(after.containedStableIds, Is.EqualTo(previous.containedStableIds));
            Assert.That(after.content, Is.EqualTo(previous.content));
            Assert.That(fixture.Runtime.TryGetInstance(previous.containedStableIds[0], out _), Is.False);
            owner.FailBinding = false;
            Assert.That(box.TryPerformPrimaryAction(fixture.Context), Is.True);
            Assert.That(fixture.Runtime.TryGetInstance(previous.containedStableIds[0], out _), Is.True);
            Assert.That(box.CaptureState().containedStableIds, Does.Not.Contain(previous.containedStableIds[0]));
        }

        [Test]
        public void DynamicBridgeReusesFourDistinctChildWrappersWithoutChangingBaseRoster()
        {
            using var bridgeFixture = new ItemBridgeFixture(fixture.Runtime, "item.spark-plug");
            WorldItemInstance box = SpawnOwnedTestItem("item.sparkplug-box", "bridge.package");
            string[] originalIds = box.CaptureState().containedStableIds;
            for (int index = 0; index < 4; index++)
                Assert.That(box.TryPerformPrimaryAction(fixture.Context), Is.True);
            Assert.That(bridgeFixture.Assembly.Parts, Is.Empty);
            Assert.That(bridgeFixture.Assembly.AllRuntimeParts.Length, Is.EqualTo(4));
            foreach (string id in originalIds)
            {
                Assert.That(fixture.Runtime.TryGetInstance(id, out WorldItemInstance item), Is.True);
                PartInstance part = item.GetComponent<PartInstance>();
                Assert.That(part.StableId, Is.EqualTo(item.StableId));
                Assert.That(part.Body, Is.SameAs(item.GetComponent<Rigidbody>()));
                Assert.That(part.PickupTarget, Is.SameAs(item.GetComponent<PhysicsPickupTarget>()));
                Assert.That(fixture.Runtime.IsExternallyOwned(id), Is.True);
            }
            bridgeFixture.Bridge.ReconcileBindings();
            Assert.That(bridgeFixture.Assembly.AllRuntimeParts.Length, Is.EqualTo(4));
            Assert.That(box.CaptureState().containedStableIds, Is.Empty);
        }

        [Test]
        public void DynamicBridgeExactRestoreSelectionDoesNotRegisterOtherLiveSpares()
        {
            using var bridgeFixture = new ItemBridgeFixture(fixture.Runtime, "item.spark-plug");
            WorldItemInstance selected = SpawnOwnedTestItem("item.spark-plug", "bridge.selected");
            WorldItemInstance spare = SpawnOwnedTestItem("item.spark-plug", "bridge.spare");
            Assert.That(bridgeFixture.Assembly.TrySetDynamicPartRegistrationsForRestore(
                Array.Empty<DynamicPartRegistration>(), out _), Is.True);
            bridgeFixture.Bridge.ReconcileBindings(new[] { selected.StableId.Value });
            Assert.That(bridgeFixture.Assembly.AllRuntimeParts, Is.EqualTo(new[] { selected.GetComponent<PartInstance>() }));
            Assert.That(fixture.Runtime.TryGetInstance(spare.StableId.Value, out _), Is.True);
        }

        [Test]
        public void RestoredConsumedItemNeverRegistersAsFreshAssemblyPart()
        {
            using var bridgeFixture = new ItemBridgeFixture(fixture.Runtime, "item.spark-plug");
            WorldItemInstance previous = SpawnOwnedTestItem("item.spark-plug", "bridge.consumed");
            ItemInstanceState state = previous.CaptureState();
            state.isConsumed = true;
            Assert.That(fixture.Runtime.TryRemoveDynamic(previous), Is.True);
            WorldItemInstance restored = fixture.Runtime.MaterializeForRestore(new ItemRuntimeSaveRecord
            {
                state = state, materializationRotation = Quaternion.identity,
            });
            Assert.That(restored.State.isConsumed, Is.True);
            Assert.That(restored.GetComponent<PartInstance>(), Is.Null);
            Assert.That(bridgeFixture.Assembly.AllRuntimeParts, Is.Empty);
        }

        [Test]
        public void InstalledDynamicPartKeepsRemovalQueryAndReturnsSamePickupWhenDetached()
        {
            using var bridgeFixture = new ItemBridgeFixture(fixture.Runtime, "item.spark-plug", "mount.test.dynamic-plug");
            WorldItemInstance item = SpawnOwnedTestItem("item.spark-plug", "bridge.interaction");
            PartInstance part = item.GetComponent<PartInstance>();
            InteractionTargetHost host = item.GetComponent<InteractionTargetHost>();
            Assert.That(host.TryGetCapability(out IPickupTarget pickup), Is.True);
            Assert.That(pickup, Is.TypeOf<AssemblySubassemblyPickupTarget>());
            Assert.That(pickup.Body, Is.SameAs(part.Body));
            AssemblyInstalledPartInteractionProxy proxy = item.GetComponentInChildren<AssemblyInstalledPartInteractionProxy>(true);
            Assert.That(proxy, Is.Not.Null);
            Assert.That(proxy.InteractionCollider.enabled, Is.False);
            Assert.That(bridgeFixture.Assembly.TryInstall(part, bridgeFixture.Mount).Succeeded, Is.True);
            Assert.That(proxy.InteractionCollider.enabled, Is.True);
            Assert.That(proxy.InteractionCollider.isTrigger, Is.True);
            Assert.That(item.GetComponent<Collider>().enabled, Is.False);
            var removal = item.GetComponent<AssemblyInstalledPartInteractionTarget>();
            Assert.That(removal.CanInteract(fixture.Context), Is.True);
            removal.Interact(fixture.Context);
            Assert.That(part.IsInstalled, Is.False);
            Assert.That(proxy.InteractionCollider.enabled, Is.False);
            Assert.That(pickup.Body, Is.SameAs(item.GetComponent<Rigidbody>()));
            Assert.That(fixture.Runtime.TryGetInstance(item.StableId.Value, out WorldItemInstance kept), Is.True);
            Assert.That(kept, Is.SameAs(item));
        }

        [Test]
        public void AggregatePresentationCallbackReceivesOriginalWrapperAndVisualBeforeProxyCreation()
        {
            using var bridgeFixture = new ItemBridgeFixture(fixture.Runtime, "item.spark-plug");
            TestAggregateItemPresentationBinding callback = bridgeFixture.Assembly.gameObject
                .AddComponent<TestAggregateItemPresentationBinding>();
            bridgeFixture.Bridge.BindRuntime(fixture.Runtime);
            WorldItemInstance item = SpawnOwnedTestItem("item.spark-plug", "bridge.presenter-callback");
            PartInstance part = item.GetComponent<PartInstance>();
            Rigidbody body = item.GetComponent<Rigidbody>();
            Assert.That(callback.CallCount, Is.EqualTo(1));
            Assert.That(callback.Part, Is.SameAs(part));
            Assert.That(callback.Visual, Is.SameAs(item.PresentationRoot.transform));
            Assert.That(callback.ProxyExistedOnFirstCall, Is.False);
            Assert.That(callback.Part.StableId, Is.EqualTo(item.StableId));
            Vector3 authoredPosition = new Vector3(.001f, .002f, .003f);
            Quaternion authoredRotation = Quaternion.Euler(0f, 0f, 23f);
            item.PresentationRoot.transform.localPosition = authoredPosition;
            item.PresentationRoot.transform.localRotation = authoredRotation;
            bridgeFixture.Bridge.ReconcilePresentation(item, item.PresentationRoot);
            Assert.That(callback.CallCount, Is.EqualTo(2));
            Assert.That(callback.Part, Is.SameAs(part));
            Assert.That(part.Body, Is.SameAs(body));
            Assert.That(item.PresentationRoot.transform.localPosition, Is.EqualTo(authoredPosition));
            Assert.That(Quaternion.Angle(item.PresentationRoot.transform.localRotation, authoredRotation), Is.LessThan(.001f));
        }

        [TestCase("item.spark-plug")]
        [TestCase("item.alternator-belt")]
        [TestCase("item.light-bulb")]
        public void NonFilterConditionIsBoundBeforeEventsAndReadsReplacedItemStateWithoutAdjustment(string itemId)
        {
            typeof(ItemWorldRuntime).GetField("presentationProvider", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(fixture.Runtime, new EmptyItemTestPresentationProvider());
            using var bridgeFixture = new ItemBridgeFixture(fixture.Runtime, itemId);
            bool presentationObserved = false;
            bool materializedObserved = false;
            fixture.Runtime.PresentationAttached += (observed, _) =>
            {
                IAssemblyItemCondition earlyCondition = observed.GetComponent<ItemPartPresentationBinding>();
                Assert.That(earlyCondition, Is.Not.Null);
                Assert.That(earlyCondition.ConditionPercent, Is.EqualTo(100f));
                Assert.That(earlyCondition.IsBroken, Is.False);
                presentationObserved = true;
            };
            fixture.Runtime.InstanceMaterialized += observed =>
            {
                Assert.That(observed.GetComponent<ItemPartPresentationBinding>(), Is.Not.Null);
                materializedObserved = true;
            };
            WorldItemInstance item = SpawnOwnedTestItem(itemId, "bridge.condition." + itemId);
            PartInstance part = item.GetComponent<PartInstance>();
            Rigidbody body = part.Body;
            ItemPartPresentationBinding binding = item.GetComponent<ItemPartPresentationBinding>();
            IAssemblyItemCondition condition = binding;
            Assert.That(presentationObserved && materializedObserved, Is.True);
            Assert.That(item.PresentationRoot.GetComponentInChildren<Renderer>(true), Is.Null);

            ItemInstanceState broken = item.CaptureState();
            broken.condition = 6f;
            broken.isBroken = true;
            item.ApplyState(broken);
            Assert.That(condition.ConditionPercent, Is.EqualTo(6f));
            Assert.That(condition.IsBroken, Is.True);

            ItemInstanceState repaired = item.CaptureState();
            repaired.condition = 74f;
            repaired.isBroken = false;
            item.ApplyState(repaired);
            bridgeFixture.Bridge.ReconcileBindings();
            Assert.That(condition.ConditionPercent, Is.EqualTo(74f));
            Assert.That(condition.IsBroken, Is.False);
            Assert.That(item.GetComponents<ItemPartPresentationBinding>(), Is.EqualTo(new[] { binding }));
            Assert.That(item.GetComponent<PartInstance>(), Is.SameAs(part));
            Assert.That(part.Body, Is.SameAs(body));
            Assert.That(part.StableId, Is.EqualTo(item.StableId));
            Assert.That(binding.PresentationRoot, Is.Null, "Non-filter condition binding must not configure filter visuals.");
            Assert.That(item.GetComponent<AssemblyEngineAdjustmentState>(), Is.Null);
            Assert.That(item.GetComponentInChildren<AssemblyEngineAdjustmentTarget>(true), Is.Null);
        }

        [TestCase("item.spark-plug", "vehicle.satsuma.part.spark-plug")]
        [TestCase("item.alternator-belt", "vehicle.satsuma.part.alternator-belt")]
        [TestCase("item.oil-filter", "vehicle.satsuma.part.oilfilter0")]
        [TestCase("item.light-bulb", "vehicle.satsuma.part.light-bulb")]
        public void ItemPartCatalogAllowsOnlyReviewedExactMappings(string itemId, string partId)
        {
            PartDefinition definition = ScriptableObject.CreateInstance<PartDefinition>();
            VehicleItemPartCatalog catalog = ScriptableObject.CreateInstance<VehicleItemPartCatalog>();
            try
            {
                definition.Configure(partId, "Synthetic consumable", PartCategory.Engine, 0.05f, null, null);
                catalog.Configure(new[] { new VehicleItemPartRecord(itemId, definition) });
                Assert.That(catalog.TryResolve(itemId, out PartDefinition resolved), Is.True);
                Assert.That(resolved, Is.SameAs(definition));
                Assert.Throws<ArgumentException>(() => catalog.Configure(new[]
                {
                    new VehicleItemPartRecord(itemId, definition), new VehicleItemPartRecord(itemId, definition),
                }));
                Assert.Throws<ArgumentException>(() => catalog.Configure(new[]
                {
                    new VehicleItemPartRecord("item.mail-order.unsupported", definition),
                }));
                Assert.That(catalog.TryResolve(itemId, out resolved), Is.True, "Failed authoring must preserve the prior valid catalog.");
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); UnityEngine.Object.DestroyImmediate(definition); }
        }

        [Test]
        public void PurchasedFilterReusesHandAdjustmentAndPreservesSettingAcrossVisualReplacement()
        {
            typeof(ItemWorldRuntime).GetField("presentationProvider", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(fixture.Runtime, new SolidItemTestPresentationProvider());
            using var bridgeFixture = new ItemBridgeFixture(fixture.Runtime, "item.oil-filter",
                "mount.satsuma.engine-block.oil-filter");
            WorldItemInstance item = SpawnOwnedTestItem("item.oil-filter", "bridge.filter");
            PartInstance part = item.GetComponent<PartInstance>();
            Rigidbody body = part.Body;
            AssemblyEngineAdjustmentState state = item.GetComponent<AssemblyEngineAdjustmentState>();
            IAssemblyItemCondition condition = item.GetComponent<ItemPartPresentationBinding>();
            Assert.That(condition.ConditionPercent, Is.EqualTo(item.ConditionPercent));
            Assert.That(condition.IsBroken, Is.EqualTo(item.IsBroken));
            Assert.That(state, Is.Not.Null);
            Assert.That(state.Kind, Is.EqualTo(SatsumaEngineAdjustmentKind.OilFilter));
            Assert.That(state.Setting, Is.Zero);
            Assert.That(bridgeFixture.Assembly.TryInstall(part, bridgeFixture.Mount).Succeeded, Is.True);
            Assert.That(state.TryAdjust(4f), Is.True);
            float setting = state.Setting;
            Assert.That(setting, Is.GreaterThan(0f));
            Assert.That(fixture.Runtime.TryRemoveDynamic(item), Is.False, "Installed item cannot bypass assembly removal.");
            typeof(ItemWorldRuntime).GetMethod("HandlePresentationProviderChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(fixture.Runtime, new object[] { new SolidItemTestPresentationProvider() });
            Assert.That(item.GetComponent<AssemblyEngineAdjustmentState>(), Is.SameAs(state));
            Assert.That(state.Setting, Is.EqualTo(setting));
            Assert.That(item.GetComponent<Rigidbody>(), Is.SameAs(body));
            DynamicAssemblyPartSaveDto saved = bridgeFixture.Assembly.CaptureSaveData().dynamicParts.Single();
            Assert.That(saved.part.stableEntityId, Is.EqualTo(item.StableId.Value));
            Assert.That(saved.part.hasEngineAdjustment, Is.True);
            Assert.That(saved.part.engineAdjustment.value, Is.EqualTo(setting));
        }

        private WorldItemInstance SpawnOwnedTestItem(string definitionId, string key, string sourceCell = "") =>
            fixture.Runtime.SpawnDynamic(definitionId, ItemStableIdUtility.CreateDeterministic("test." + key),
                Vector3.zero, Quaternion.identity, SceneManager.GetActiveScene(), sourceCell);

        private sealed class TestExternalItemOwner : IItemExternalPhysicsOwner
        {
            public readonly HashSet<string> BoundIds = new HashSet<string>(StringComparer.Ordinal);
            public bool FailBinding;
            public bool OwnsDefinition(string id) => id == "item.spark-plug";
            public void BindMaterialized(WorldItemInstance instance)
            {
                if (!OwnsDefinition(instance.Definition.DefinitionId)) return;
                if (FailBinding) throw new InvalidOperationException("Injected owner registration failure");
                BoundIds.Add(instance.StableId.Value);
            }
            public void ReconcilePresentation(WorldItemInstance item, GameObject visual) { }
            public bool TryReleaseInstance(WorldItemInstance item, out string failure)
            { BoundIds.Remove(item.StableId.Value); failure = string.Empty; return true; }
        }

        private sealed class TestAggregateItemPresentationBinding : MonoBehaviour, IAssemblyItemPartPresentationBinding
        {
            public int CallCount { get; private set; }
            public PartInstance Part { get; private set; }
            public Transform Visual { get; private set; }
            public bool ProxyExistedOnFirstCall { get; private set; }
            public void BindPartPresentation(PartInstance part, Transform visualRoot)
            {
                if (CallCount == 0)
                    ProxyExistedOnFirstCall = part.GetComponentInChildren<AssemblyInstalledPartInteractionProxy>(true) != null;
                CallCount++;
                Part = part;
                Visual = visualRoot;
            }
        }

        private sealed class ItemBridgeFixture : IDisposable
        {
            private readonly ItemWorldRuntime runtime;
            private readonly GameObject root;
            private readonly VehicleItemPartCatalog catalog;
            private readonly PartDefinition definition;
            private readonly MountPointDefinition mountDefinition;
            public VehicleAssemblyController Assembly { get; }
            public VehicleItemAssemblyBridge Bridge { get; }
            public MountPointAuthoring Mount { get; }

            public ItemBridgeFixture(ItemWorldRuntime itemRuntime, string itemId, string mountId = "")
            {
                runtime = itemRuntime;
                definition = ScriptableObject.CreateInstance<PartDefinition>();
                definition.Configure(VehicleItemPartCatalog.ExpectedPartDefinitionId(itemId),
                    "Synthetic consumable", PartCategory.Engine, 0.05f, null,
                    new[] { PartCompatibilityRule.Create("test.consumable") });
                catalog = ScriptableObject.CreateInstance<VehicleItemPartCatalog>();
                catalog.Configure(new[] { new VehicleItemPartRecord(itemId, definition) });
                root = new GameObject("Synthetic consumable assembly");
                Assembly = root.AddComponent<VehicleAssemblyController>();
                if (!string.IsNullOrEmpty(mountId))
                {
                    mountDefinition = ScriptableObject.CreateInstance<MountPointDefinition>();
                    mountDefinition.Configure(mountId, "Synthetic consumable mount", "test.consumable", string.Empty,
                        new[] { definition.DefinitionId }, new MountConstraint(2f, 180f, 3f, 0f),
                        0f, Array.Empty<FastenerDefinition>());
                    GameObject mount = new GameObject("Synthetic consumable mount");
                    mount.transform.SetParent(root.transform, false);
                    Mount = mount.AddComponent<MountPointAuthoring>();
                    Mount.Configure(mountDefinition, mountId, mount.transform, 0);
                }
                Assembly.Configure(Array.Empty<PartInstance>(), Mount != null ? new[] { Mount } : Array.Empty<MountPointAuthoring>(),
                    Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), null);
                Bridge = root.AddComponent<VehicleItemAssemblyBridge>();
                Bridge.Configure(Assembly, catalog);
                Bridge.BindRuntime(runtime);
            }

            public void Dispose()
            {
                Assembly.TrySetDynamicPartRegistrationsForRestore(Array.Empty<DynamicPartRegistration>(), out _);
                runtime.RemoveDynamicInstancesExcept(new HashSet<string>(StringComparer.Ordinal));
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(definition);
                if (mountDefinition != null) UnityEngine.Object.DestroyImmediate(mountDefinition);
            }
        }

        private sealed class EmptyItemTestPresentationProvider : IItemPresentationProvider
        {
            public bool TryInstantiate(ItemDefinitionRecord definition, Transform parent, out GameObject visualRoot)
            {
                visualRoot = new GameObject("Synthetic item presentation without renderer");
                visualRoot.transform.SetParent(parent, false);
                return true;
            }
        }

        private sealed class SolidItemTestPresentationProvider : IItemPresentationProvider
        {
            public bool TryInstantiate(ItemDefinitionRecord definition, Transform parent, out GameObject visualRoot)
            {
                visualRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Object.DestroyImmediate(visualRoot.GetComponent<Collider>());
                visualRoot.transform.SetParent(parent, false);
                visualRoot.transform.localScale = definition.ProxySize;
                return true;
            }
        }
    }
}
