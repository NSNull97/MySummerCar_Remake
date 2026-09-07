using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaEngineDockingTests
    {
        [Test]
        public void ProximityOnlyExposesBoltsAndFirstStageLeavesEngineDynamic()
        {
            using var f = new Fixture();
            Vector3 position = f.Block.transform.position;
            Quaternion rotation = f.Block.transform.rotation;
            for (int index = 0; index < 3; index++) Assert.That(f.Docking.CanExposePending(index), Is.True);
            Assert.That(f.Block.IsInstalled, Is.False);
            Assert.That(f.Block.Body.isKinematic, Is.False);
            Assert.That(f.Turn(0, true).Succeeded, Is.True);
            Assert.That(f.Block.IsInstalled, Is.False);
            Assert.That(f.Block.Body.isKinematic, Is.False);
            Assert.That(f.Mount.IsOccupied, Is.False);
            Assert.That(f.Docking.CaptureSaveData().pendingStages, Is.EqualTo(new[] { 1, 0, 0 }));
            Assert.That(f.Mount.Fasteners.All(value => !value.IsInserted && value.Stage == 0), Is.True);
            Assert.That(f.Block.transform.position, Is.EqualTo(position));
            Assert.That(f.Block.transform.rotation, Is.EqualTo(rotation));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void AggregateSecondTurnInstallsAndTransfersRealBoltStages(int secondBolt)
        {
            using var f = new Fixture();
            Assert.That(f.Turn(0, true).Succeeded, Is.True);
            Assert.That(f.Turn(secondBolt, true).Succeeded, Is.True);
            Assert.That(f.Block.IsInstalled, Is.True);
            Assert.That(f.Block.Body.isKinematic, Is.True);
            Assert.That(f.Mount.InstalledPart, Is.SameAs(f.Block));
            Assert.That(f.Mount.FastenerGroup.Tightness, Is.EqualTo(2));
            Assert.That(f.Mount.FastenerGroup.IsBolted, Is.True);
            Assert.That(f.Mount.Fasteners[0].Stage, Is.EqualTo(secondBolt == 0 ? 2 : 1));
            Assert.That(f.Docking.CaptureSaveData().IsEmpty, Is.True);
            Assert.That(f.Child.IsInstalled, Is.True);
        }

        [Test]
        public void InstallationCallbackSeesCommittedBoltGroupAndValidSaveNotHalfAttachedEngine()
        {
            using var f = new Fixture();
            int callbackCount = 0;
            VehicleAssemblySaveData captured = null;
            f.Assembly.ActionCompleted += action =>
            {
                if (action.Action != AssemblyActionKind.PartInstalled || action.Part != f.Block) return;
                callbackCount++;
                captured = f.Assembly.CaptureSaveData();
                Assert.That(f.Mount.FastenerGroup.IsBolted, Is.True);
                Assert.That(f.Mount.FastenerGroup.Tightness, Is.EqualTo(2));
                Assert.That(captured.fasteners.Where(value => value.mountId == Fixture.EngineMountId)
                    .Sum(value => value.stage), Is.EqualTo(2));
                Assert.That(captured.fastenerGroups.Single(value => value.mountId == Fixture.EngineMountId).isBolted, Is.True);
                Assert.That(captured.parts.Single(value => value.partDefinitionId == Fixture.BlockId)
                    .engineDocking.IsEmpty, Is.True);
            };
            f.Turn(0, true);
            Assert.That(f.Turn(1, true).Succeeded, Is.True);
            Assert.That(callbackCount, Is.EqualTo(1));
            Assert.That(captured, Is.Not.Null);
            Assert.That(f.Assembly.RestoreSaveData(captured).Succeeded, Is.True);
        }

        [Test]
        public void AwayOrWrongToolCannotChangePendingAndRejectedSecondTurnKeepsLoosePose()
        {
            using var f = new Fixture();
            var wrong = ScriptableObject.CreateInstance<ToolDefinition>();
            try
            {
                wrong.Configure("test.key10", "Key10", "Wrench", FastenerSize.Millimeter10);
                Assert.That(f.Docking.TryTurnPending(Fixture.Ids[0], wrong, FastenerRotationDirection.Clockwise).Succeeded, Is.False);
                Assert.That(f.Docking.CaptureSaveData().IsEmpty, Is.True);
                Assert.That(f.Turn(0, true).Succeeded, Is.True);
                f.Block.transform.position += new Vector3(0f, 0.2f, 0f);
                Vector3 position = f.Block.transform.position;
                Quaternion rotation = f.Block.transform.rotation;
                Assert.That(f.Docking.CanExposePending(0), Is.False);
                Assert.That(f.Turn(0, true).Succeeded, Is.False);
                Assert.That(f.Block.IsInstalled, Is.False);
                Assert.That(f.Block.transform.position, Is.EqualTo(position));
                Assert.That(f.Block.transform.rotation, Is.EqualTo(rotation));
                Assert.That(f.Docking.CaptureSaveData().pendingStages, Is.EqualTo(new[] { 1, 0, 0 }));
            }
            finally { Object.DestroyImmediate(wrong); }
        }

        [Test]
        public void DonorToleranceEqualityRetainsPreviousVisibilityInsteadOfFlickering()
        {
            using var f = new Fixture();
            f.Block.transform.position = new Vector3(0f, 0.1f, 0f);
            Assert.That(f.Docking.CanExposePending(0), Is.False, "Equality does not enter Ready.");
            f.Block.transform.position = new Vector3(0f, 0.09f, 0f);
            Assert.That(f.Docking.CanExposePending(0), Is.True);
            f.Block.transform.position = new Vector3(0f, 0.1f, 0f);
            Assert.That(f.Docking.CanExposePending(0), Is.True, "Equality does not leave Ready.");
            f.Block.transform.position = new Vector3(0f, 0.101f, 0f);
            Assert.That(f.Docking.CanExposePending(0), Is.False);
        }

        [Test]
        public void PendingSingleTurnRoundTripsWithoutInstallingAndMissingOldFieldClearsIt()
        {
            using var f = new Fixture();
            f.Turn(2, true);
            VehicleAssemblySaveData data = f.Assembly.CaptureSaveData();
            PartSaveDto dto = data.parts.Single(value => value.partDefinitionId == Fixture.BlockId);
            Assert.That(dto.hasEngineDocking, Is.True);
            Assert.That(dto.engineDocking.pendingStages, Is.EqualTo(new[] { 0, 0, 1 }));
            f.Turn(2, false);
            Assert.That(f.Assembly.RestoreSaveData(data).Succeeded, Is.True);
            Assert.That(f.Block.IsInstalled, Is.False);
            Assert.That(f.Block.Body.isKinematic, Is.False);
            Assert.That(f.Docking.CaptureSaveData().pendingStages, Is.EqualTo(new[] { 0, 0, 1 }));
            dto.hasEngineDocking = false;
            dto.engineDocking = null;
            Assert.That(f.Assembly.RestoreSaveData(data).Succeeded, Is.True);
            Assert.That(f.Docking.CaptureSaveData().IsEmpty, Is.True);
        }

        [Test]
        public void InvalidPendingSumOrWrongPartRejectsSaveBeforeChangingLiveState()
        {
            using var f = new Fixture();
            f.Turn(0, true);
            VehicleAssemblySaveData data = f.Assembly.CaptureSaveData();
            PartSaveDto dto = data.parts.Single(value => value.partDefinitionId == Fixture.BlockId);
            dto.engineDocking.pendingStages = new[] { 1, 1, 0 };
            Assert.That(f.Assembly.RestoreSaveData(data).Succeeded, Is.False);
            Assert.That(f.Docking.CaptureSaveData().pendingStages, Is.EqualTo(new[] { 1, 0, 0 }));
            dto.engineDocking.pendingStages = new[] { 1, 0, 0 };
            PartSaveDto child = data.parts.Single(value => value.partDefinitionId == Fixture.ChildId);
            child.hasEngineDocking = true;
            child.engineDocking = new AssemblyEngineDockingSaveDto();
            Assert.That(f.Assembly.RestoreSaveData(data).Succeeded, Is.False);
            Assert.That(f.Block.IsInstalled, Is.False);
        }

        [Test]
        public void UnscrewingToZeroReleasesAtCurrentPoseAndRetainsInternalAssembly()
        {
            using var f = new Fixture();
            f.Turn(0, true);
            f.Turn(0, true);
            f.Assembly.transform.SetPositionAndRotation(new Vector3(6f, 2f, -4f), Quaternion.Euler(12f, 35f, 4f));
            Vector3 position = f.Block.transform.position;
            Quaternion rotation = f.Block.transform.rotation;
            string childMountId = f.Child.RuntimeState.InstalledMountId;
            Assert.That(f.Assembly.TryTurnFastener(Fixture.EngineMountId, Fixture.Ids[0], f.Key,
                FastenerRotationDirection.CounterClockwise).Succeeded, Is.True);
            Assert.That(f.Docking.ReleaseIfUnfastened(), Is.False, "ON2/OFF0 retains latch at one remaining stage.");
            Assert.That(f.Assembly.TryTurnFastener(Fixture.EngineMountId, Fixture.Ids[0], f.Key,
                FastenerRotationDirection.CounterClockwise).Succeeded, Is.True);
            Assert.That(f.Docking.ReleaseIfUnfastened(), Is.True);
            Assert.That(f.Block.IsInstalled, Is.False);
            Assert.That(f.Block.Body.isKinematic, Is.False);
            Assert.That(Vector3.Distance(f.Block.transform.position, position), Is.LessThan(0.00001f));
            Assert.That(Quaternion.Angle(f.Block.transform.rotation, rotation), Is.LessThan(0.001f));
            Assert.That(f.Child.IsInstalled, Is.True);
            Assert.That(f.Child.RuntimeState.InstalledMountId, Is.EqualTo(childMountId));
            Assert.That(f.Child.transform.IsChildOf(f.Block.transform), Is.True);
        }

        [Test]
        public void PhysicalDockingMarkerBlocksClickHandoffAndGenericSurfaceMountSelection()
        {
            using var f = new Fixture();
            var target = f.Mount.Authoring.gameObject.AddComponent<AssemblyMountHandoffTarget>();
            target.Configure(f.Assembly, f.Mount.Authoring);
            Assert.That(target.CanAccept(f.Block.GetComponent<PhysicsPickupTarget>(), default), Is.False);
            Assert.That(target.CanSelectForCarriedObject(f.Block.GetComponent<PhysicsPickupTarget>(), default), Is.False);
            Assert.That(target.TryPrepareHandoff(f.Block.GetComponent<PhysicsPickupTarget>(), default), Is.False);
            target.Accept(f.Block.GetComponent<PhysicsPickupTarget>(), default);
            Assert.That(f.Block.IsInstalled, Is.False);
        }

        [Test]
        public void GeneratedDockingUsesExistingThreePairsTargetsAndTwoStageLatch()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            var assembly = prefab.GetComponent<VehicleAssemblyController>();
            PartInstance block = assembly.Parts.Single(value => value.Definition.DefinitionId == Fixture.BlockId);
            var docking = block.GetComponent<AssemblyEngineDockingState>();
            Assert.That(docking, Is.Not.Null, "Run scoped engine docking refresh first.");
            Assert.That(docking.Block, Is.SameAs(block));
            Assert.That(docking.Mount.GetComponent<AssemblyPhysicalDockingOnly>(), Is.Not.Null);
            Assert.That(docking.FastenerIds, Is.EqualTo(Fixture.Ids));
            Assert.That(docking.DistanceToleranceMeters, Is.EqualTo(0.1f));
            Assert.That(docking.Mount.Definition.FastenerGroup.BoltedOnThreshold, Is.EqualTo(2));
            Assert.That(docking.Mount.Definition.FastenerGroup.BoltedOffThreshold, Is.Zero);
            Assert.That(docking.Mount.Definition.FastenerGroup.AggregateMaximumTightness, Is.EqualTo(24));
            Mesh shortBolt = AssetDatabase.LoadAssetAtPath<Mesh>(Phase1SatsumaCamshaftTimingAuthoring.GeneratedRoot +
                "/Meshes/" + Phase1SatsumaEngineDockingAuthoring.EngineBoltMeshSourceGuid + ".asset");
            Assert.That(shortBolt, Is.Not.Null);
            foreach (string id in Fixture.Ids)
            {
                AssemblyFastenerInteractionTarget target = prefab.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .Single(value => value.FastenerDefinitionId == id);
                Assert.That(target.PendingDocking, Is.SameAs(docking));
                var targetData = new SerializedObject(target);
                var presentation = targetData.FindProperty("fastenerPresentation").objectReferenceValue as Transform;
                Assert.That(presentation, Is.Not.Null);
                Assert.That(presentation.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(shortBolt), id);
                Assert.That(target.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(Vector3.Distance(presentation.localScale, Vector3.one * 1.1f), Is.LessThan(0.00001f));
                Assert.That(presentation.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(target.FastenerPresentationStageTravelScale, Is.EqualTo(1.1f).Within(0.00001f));
                Assert.That(8f * 0.0025f * target.FastenerPresentationStageTravelScale,
                    Is.EqualTo(0.022f).Within(0.000001f), "Full donor axial travel is22mm, not20mm.");
            }
            Phase1SatsumaEngineDockingAuthoring.ReadFrozenPoints(out Vector3[] blockPoints, out Vector3[] chassisPoints);
            var serialized = new SerializedObject(docking);
            Assert.That(serialized.FindProperty("chassisFrame").objectReferenceValue, Is.SameAs(assembly.transform));
            for (int index = 0; index < 3; index++)
            {
                Assert.That(serialized.FindProperty("blockPoints").GetArrayElementAtIndex(index).vector3Value,
                    Is.EqualTo(blockPoints[index]));
                Assert.That(serialized.FindProperty("chassisPoints").GetArrayElementAtIndex(index).vector3Value,
                    Is.EqualTo(chassisPoints[index]));
            }
        }

        private sealed class Fixture : IDisposable
        {
            public const string EngineMountId = Phase1SatsumaEngineDockingAuthoring.MountId;
            public const string BlockId = Phase1SatsumaEngineDockingAuthoring.BlockPartId;
            public const string ChildId = "vehicle.satsuma.part.oilpan";
            private const string BodyId = Phase1SatsumaEngineDockingAuthoring.BodyPartId;
            private const string ChildMountId = "mount.satsuma.engine-block.oilpan";
            public static string[] Ids => Phase1SatsumaEngineDockingAuthoring.FastenerIds;
            private readonly GameObject root = new("Engine docking fixture");
            private readonly List<ScriptableObject> assets = new();
            public PartInstance Block { get; }
            public PartInstance Child { get; }
            public VehicleAssemblyController Assembly { get; }
            public MountPointRuntime Mount { get; }
            public ToolDefinition Key { get; }
            public AssemblyEngineDockingState Docking { get; }

            public Fixture()
            {
                PartInstance body = CreatePart(BodyId, true, "", "", 240f);
                Block = CreatePart(BlockId, false, "", BodyId, 95f);
                Child = CreatePart(ChildId, false, ChildMountId, BlockId, 3f);
                var fasteners = new FastenerDefinition[3];
                for (int index = 0; index < 3; index++)
                {
                    fasteners[index] = Asset<FastenerDefinition>();
                    fasteners[index].Configure(Ids[index], "Engine mount bolt", FastenerSize.Millimeter11, 8,
                        FastenerDirection.ClockwiseToTighten, true, true,
                        ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter11));
                }
                MountPointAuthoring engineMount = CreateMount(EngineMountId, body, Block, fasteners);
                var group = new FastenerGroupDefinition();
                group.Configure(Ids, 24, 2, 0);
                engineMount.Definition.ConfigureFastenerGroup(group);
                engineMount.Definition.ConfigureRetainedRemovalChildren(new[] { ChildMountId });
                engineMount.gameObject.AddComponent<AssemblyPhysicalDockingOnly>();
                MountPointAuthoring childMount = CreateMount(ChildMountId, Block, Child, Array.Empty<FastenerDefinition>());
                childMount.gameObject.AddComponent<AssemblyOwnedMountAuthoring>().Configure(Block);
                Key = Asset<ToolDefinition>();
                Key.Configure("test.key11", "Key11", "Wrench", FastenerSize.Millimeter11);
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(new[] { body, Block, Child }, new[] { engineMount, childMount },
                    Array.Empty<AssemblyDependency>(), new[] { Key }, root.transform);
                Mount = Assembly.ResolveMount(engineMount);
                Docking = Block.gameObject.AddComponent<AssemblyEngineDockingState>();
                var anchors = new[] { new Vector3(-0.2f, 0f, 0f), new Vector3(0.2f, 0f, 0f), new Vector3(0f, 0f, 0.2f) };
                Docking.Configure(Assembly, Block, engineMount, root.transform, anchors, anchors.ToArray(), Ids);
            }
            public AssemblyOperationResult Turn(int bolt, bool tighten) => Docking.TryTurnPending(Ids[bolt], Key,
                tighten ? FastenerRotationDirection.Clockwise : FastenerRotationDirection.CounterClockwise);
            private PartInstance CreatePart(string id, bool isRoot, string mount, string ownerId, float mass)
            {
                var owner = new GameObject(id);
                owner.transform.SetParent(root.transform, false);
                var definition = Asset<PartDefinition>();
                definition.Configure(id, id, PartCategory.Engine, mass, null,
                    new[] { PartCompatibilityRule.Create("test.socket", ownerId) });
                var identity = owner.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = owner.AddComponent<Rigidbody>();
                var pickup = owner.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(body, identity, id, 200f);
                var part = owner.AddComponent<PartInstance>();
                part.Configure(definition, identity, body, pickup, isRoot, mount);
                return part;
            }
            private MountPointAuthoring CreateMount(string id, PartInstance owner, PartInstance part,
                FastenerDefinition[] fasteners)
            {
                var definition = Asset<MountPointDefinition>();
                definition.Configure(id, id, "test.socket", owner.Definition.DefinitionId,
                    new[] { part.Definition.DefinitionId }, new MountConstraint(0.35f, 40f, 1.25f, 0f), 0.1f, fasteners);
                definition.ConfigureFastenerGroup(FastenerGroupDefinition.CreateCompatibility(fasteners));
                var mountObject = new GameObject(id);
                mountObject.transform.SetParent(owner.transform, false);
                var authoring = mountObject.AddComponent<MountPointAuthoring>();
                authoring.Configure(definition, id, mountObject.transform, 0);
                return authoring;
            }
            private T Asset<T>() where T : ScriptableObject
            {
                var asset = ScriptableObject.CreateInstance<T>();
                assets.Add(asset);
                return asset;
            }
            public void Dispose()
            {
                Object.DestroyImmediate(root);
                foreach (var asset in assets) Object.DestroyImmediate(asset);
            }
        }
    }
}
