using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.ItemsIntegration;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaConsumableMountTests
    {
        [Test]
        public void SevenSocketsAndFourThreadsAreAdditiveAndIdempotent()
        {
            using var f = new Fixture(false);
            PartInstance[] parts = f.Assembly.Parts;
            int oldMounts = f.Assembly.MountPoints.Length;
            Assert.That(Phase1SatsumaConsumableMountAuthoring.ApplyToInstance(f.Assembly, f.Definitions), Is.GreaterThan(0));
            Assert.That(Phase1SatsumaConsumableMountAuthoring.ApplyToInstance(f.Assembly, f.Definitions), Is.Zero);
            Assert.That(f.Assembly.Parts, Is.EqualTo(parts));
            Assert.That(f.Assembly.MountPoints.Length, Is.EqualTo(oldMounts + 7));
            Assert.That(f.Assembly.GetComponent<VehicleItemAssemblyBridge>().Catalog, Is.SameAs(f.Definitions.Catalog));
            for (int index = 0; index < 7; index++)
            {
                MountPointAuthoring mount = f.Mount(Phase1SatsumaConsumableMountAuthoring.MountIds[index]);
                Assert.That(mount.transform.parent, Is.SameAs(f.Part(Phase1SatsumaConsumableMountAuthoring.OwnerId(index)).transform));
                Assert.That(mount.transform.localPosition, Is.EqualTo(Phase1SatsumaConsumableMountAuthoring.LocalPose(index).position));
                Assert.That(mount.Definition.Fasteners.Length, Is.EqualTo(index < 4 ? 1 : 0));
                Assert.That(mount.GetComponentsInChildren<Renderer>(true), Is.Empty, "The purchased part supplies the visual, not a static duplicate.");
                if (index >= 4) continue;
                var thread = mount.Definition.Fasteners[0];
                Assert.That(thread.MaximumStage, Is.EqualTo(8));
                Assert.That(thread.ToolRule.ToolType, Is.EqualTo(SatsumaAuxiliaryAssemblyTools.SparkPlugWrenchType));
                Assert.That(thread.Size, Is.EqualTo(FastenerSize.None));
                Assert.That(mount.GetComponentInChildren<AssemblyFastenerInteractionTarget>().GetComponent<Collider>().enabled, Is.False);
                Assert.That(mount.GetComponentInChildren<AssemblyFastenerInteractionTarget>().gameObject.layer,
                    Is.EqualTo(LayerMask.NameToLayer(FastenerToolRaycastLayer.Name)));
            }
        }

        [Test]
        public void LegacyPlugThreadLayersAreRepairedWithoutChangingMountsOrTargetIdentity()
        {
            using var f = new Fixture();
            var targets = Enumerable.Range(1, 4).Select(index =>
                f.Mount(SatsumaConsumableAssemblyRules.SparkPlugMountId(index))
                    .GetComponentInChildren<AssemblyFastenerInteractionTarget>(true)).ToArray();
            MountPointAuthoring[] beforeMounts = f.Assembly.MountPoints.ToArray();
            string[] beforeDefinitions = beforeMounts.Select(mount => EditorJsonUtility.ToJson(mount.Definition)).ToArray();
            foreach (var target in targets) target.gameObject.layer = target.transform.parent.gameObject.layer;
            Assert.That(Phase1SatsumaConsumableMountAuthoring.ApplyToInstance(f.Assembly, f.Definitions), Is.EqualTo(4));
            Assert.That(Phase1SatsumaConsumableMountAuthoring.ApplyToInstance(f.Assembly, f.Definitions), Is.Zero);
            Assert.That(f.Assembly.MountPoints, Is.EqualTo(beforeMounts));
            Assert.That(beforeMounts.Select(mount => EditorJsonUtility.ToJson(mount.Definition)), Is.EqualTo(beforeDefinitions));
            foreach (var target in targets)
            {
                Assert.That(target.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer(FastenerToolRaycastLayer.Name)));
                Assert.That(f.Mount(target.MountId).GetComponentInChildren<AssemblyFastenerInteractionTarget>(true), Is.SameAs(target));
            }
        }

        [Test]
        public void UnknownPlugRayLayerIsRejectedBeforeRepairingOtherThreads()
        {
            using var f = new Fixture();
            var first = f.Mount(SatsumaConsumableAssemblyRules.SparkPlugMountId(1))
                .GetComponentInChildren<AssemblyFastenerInteractionTarget>(true);
            var last = f.Mount(SatsumaConsumableAssemblyRules.SparkPlugMountId(4))
                .GetComponentInChildren<AssemblyFastenerInteractionTarget>(true);
            first.gameObject.layer = first.transform.parent.gameObject.layer;
            int unexpected = Enumerable.Range(0, 32).First(layer =>
                layer != last.transform.parent.gameObject.layer && layer != LayerMask.NameToLayer(FastenerToolRaycastLayer.Name));
            last.gameObject.layer = unexpected;
            Assert.Throws<System.IO.InvalidDataException>(() =>
                Phase1SatsumaConsumableMountAuthoring.ApplyToInstance(f.Assembly, f.Definitions));
            Assert.That(first.gameObject.layer, Is.EqualTo(first.transform.parent.gameObject.layer));
            Assert.That(last.gameObject.layer, Is.EqualTo(unexpected));
        }

        [Test]
        public void PartialSocketPacketFailsWithoutAddingTheOtherSix()
        {
            using var f = new Fixture(false);
            MountPointAuthoring partial = f.AddMount(Phase1SatsumaConsumableMountAuthoring.MountIds[0], f.Head,
                f.Definitions.Parts[0]);
            f.ResetGraph(f.Assembly.MountPoints.Append(partial).ToArray());
            int count = f.Assembly.MountPoints.Length;
            Assert.Throws<System.IO.InvalidDataException>(() => Phase1SatsumaConsumableMountAuthoring.ApplyToInstance(f.Assembly, f.Definitions));
            Assert.That(f.Assembly.MountPoints.Length, Is.EqualTo(count));
            Assert.That(f.Assembly.GetComponent<VehicleItemAssemblyBridge>(), Is.Null);
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void PurchasedPlugUsesItsOwnThreadAndRetainsIdentityThroughTighteningAndRemoval(int index)
        {
            using var f = new Fixture();
            PartInstance plug = f.Purchase(f.Definitions.Parts[0], "item.spark-plug");
            Transform visual = new GameObject("existing purchased visual").transform;
            visual.SetParent(plug.transform, false);
            var binding = f.Assembly.GetComponent<AssemblyConsumablePresentationBinding>();
            binding.BindPartPresentation(plug, visual);
            var presentation = plug.GetComponent<AssemblySparkPlugStagePresentation>();
            var socket = f.Mount(SatsumaConsumableAssemblyRules.SparkPlugMountId(index));
            StableEntityId identity = plug.StableId;
            Rigidbody body = plug.Body;
            f.Install(plug, socket);
            var target = socket.GetComponentInChildren<AssemblyFastenerInteractionTarget>();
            target.RefreshAvailability();
            Assert.That(target.GetComponent<Collider>().enabled, Is.True);
            var wrong = SatsumaAuxiliaryAssemblyTools.CreateScrewdriver();
            f.Track(wrong);
            Assert.That(f.Assembly.TryTurnFastener(socket.MountId, socket.Definition.Fasteners[0].DefinitionId,
                wrong, FastenerRotationDirection.Clockwise).Succeeded, Is.False);
            for (int stage = 1; stage <= 8; stage++)
            {
                f.Turn(socket, f.Definitions.SparkPlugWrench, true);
                presentation.RefreshPresentation();
                Assert.That(visual.localPosition.z, Is.EqualTo(-.0025f * stage).Within(.0000001f));
                Assert.That(Quaternion.Angle(visual.localRotation, Quaternion.Euler(0f, 0f, stage * 45f)), Is.LessThan(.001f));
                Assert.That(f.Assembly.EvaluateRemoval(plug).Succeeded, Is.False);
                binding.BindPartPresentation(plug, visual);
                Assert.That(visual.localPosition.z, Is.EqualTo(-.0025f * stage).Within(.0000001f), "Rebind must not recapture the tightened pose.");
            }
            for (int stage = 8; stage > 0; stage--) f.Turn(socket, f.Definitions.SparkPlugWrench, false);
            Assert.That(f.Assembly.TryRemove(plug).Succeeded, Is.True);
            presentation.RefreshPresentation();
            target.RefreshAvailability();
            Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(target.GetComponent<Collider>().enabled, Is.False);
            Assert.That(plug.StableId, Is.EqualTo(identity));
            Assert.That(plug.Body, Is.SameAs(body));
            Assert.That(plug.GetComponents<Rigidbody>(), Has.Length.EqualTo(1));
            Assert.That(plug.GetComponents<Collider>(), Has.Length.EqualTo(1));
        }

        [Test]
        public void RemovingHeadRetainsInstalledPlugRatherThanDetachingOrBlockingItsOwner()
        {
            using var f = new Fixture();
            f.Install(f.Head, f.Mount("mount.satsuma.engine-block.cylinder-head"));
            PartInstance plug = f.Purchase(f.Definitions.Parts[0], "item.spark-plug");
            f.Install(plug, f.Mount(SatsumaConsumableAssemblyRules.SparkPlugMountId(1)));
            f.Turn(f.Mount(SatsumaConsumableAssemblyRules.SparkPlugMountId(1)), f.Definitions.SparkPlugWrench, true);
            Assert.That(f.Assembly.TryRemove(f.Head).Succeeded, Is.True);
            Assert.That(plug.IsInstalled, Is.True);
            Assert.That(plug.transform.IsChildOf(f.Head.transform), Is.True);
        }

        [TestCase(3.5f, true)] [TestCase(4f, false)] [TestCase(4.5f, false)]
        public void BeltInstallationUsesStrictOriginalSlackBoundary(float setting, bool allowed)
        {
            using var f = new Fixture();
            f.InstallBeltPrerequisites();
            f.SetAlternator(setting);
            PartInstance belt = f.Purchase(f.Definitions.Parts[1], "item.alternator-belt");
            MountPointAuthoring socket = f.Mount(SatsumaConsumableAssemblyRules.BeltMountId);
            belt.transform.SetPositionAndRotation(socket.Pose.position, socket.Pose.rotation);
            Assert.That(f.Assembly.EvaluateHandoffInstall(belt, socket).Succeeded, Is.EqualTo(allowed));
            Assert.That(f.Assembly.TryInstall(belt, socket).Succeeded, Is.EqualTo(allowed));
        }

        [Test]
        public void BeltRequiresAllThreePartsButRemovalUsesBoltedOrSlackBranches()
        {
            using var f = new Fixture();
            f.InstallBeltPrerequisites();
            PartInstance belt = f.Purchase(f.Definitions.Parts[1], "item.alternator-belt");
            var socket = f.Mount(SatsumaConsumableAssemblyRules.BeltMountId);
            foreach (string prerequisite in Phase1SatsumaConsumableMountAuthoring.BeltPrerequisites.Take(2))
            {
                var part = f.Assembly.ResolveMount(f.Mount(prerequisite)).InstalledPart;
                Assert.That(f.Assembly.TryRemove(part).Succeeded, Is.True);
                belt.transform.SetPositionAndRotation(socket.Pose.position, socket.Pose.rotation);
                Assert.That(f.Assembly.EvaluateHandoffInstall(belt, socket).Succeeded, Is.False);
                f.Install(part, f.Mount(prerequisite));
            }
            f.Install(belt, socket);
            f.SetAlternator(8f);
            Assert.That(f.Assembly.EvaluateRemoval(belt).Succeeded, Is.True, "Unbolted alternator permits removal without a rotation test.");
            var alternator = f.Mount(SatsumaConsumableAssemblyRules.AlternatorMountId);
            f.Turn(alternator, f.AlternatorTool, true);
            Assert.That(f.Assembly.EvaluateRemoval(belt).Succeeded, Is.False);
            f.SetAlternator(3.5f);
            Assert.That(f.Assembly.TryRemove(belt).Succeeded, Is.True, "Bolted but slack is the other donor removal branch.");
        }

        [Test]
        public void BeltPresentationSwitchRetainsOneWrapperAndNeverRecapturesItsRig()
        {
            using var f = new Fixture();
            f.InstallBeltPrerequisites();
            PartInstance belt = f.Purchase(f.Definitions.Parts[1], "item.alternator-belt");
            var visual = new GameObject("existing item visual").transform;
            visual.SetParent(belt.transform, false);
            visual.SetLocalPositionAndRotation(new Vector3(.04f, -.02f, .01f), Quaternion.Euler(0f, 13f, 0f));
            var looseMesh = new GameObject("original loose belt").AddComponent<MeshRenderer>();
            looseMesh.transform.SetParent(visual, false);
            var mesh = new Mesh(); f.Track(mesh);
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity };
            mesh.boneWeights = new[] { new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                new BoneWeight { boneIndex0 = 0, weight0 = 1 }, new BoneWeight { boneIndex0 = 1, weight0 = 1 } };
            var material = new Material(Shader.Find("Hidden/InternalErrorShader")); f.Track(material);
            GameObject template = Phase1SatsumaConsumableBeltPresentationAuthoring.CreateRig(mesh, material); f.Track(template);
            var callback = f.Assembly.GetComponent<AssemblyConsumablePresentationBinding>();
            callback.ConfigureBeltPresentation(template);
            callback.BindPartPresentation(belt, visual);
            var presenter = belt.GetComponent<AssemblyAlternatorBeltPresentation>();
            GameObject rig = presenter.InstalledRig;
            Assert.That(rig.activeSelf, Is.False);
            Assert.That(looseMesh.enabled, Is.True);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                f.Install(belt, f.Mount(SatsumaConsumableAssemblyRules.BeltMountId));
                presenter.RefreshPresentation();
                Assert.That(rig.activeSelf, Is.True);
                Assert.That(looseMesh.enabled, Is.False);
                Assert.That(Vector3.Distance(rig.transform.position, belt.transform.position), Is.LessThan(.000001f));
                Assert.That(Quaternion.Angle(rig.transform.rotation, belt.transform.rotation), Is.LessThan(.001f));
                callback.BindPartPresentation(belt, visual);
                Assert.That(presenter.InstalledRig, Is.SameAs(rig));
                Assert.That(f.Assembly.TryRemove(belt).Succeeded, Is.True);
                presenter.RefreshPresentation();
                Assert.That(rig.activeSelf, Is.False);
                Assert.That(looseMesh.enabled, Is.True);
            }
            Assert.That(belt.GetComponentsInChildren<Rigidbody>(true), Has.Length.EqualTo(1));
            Assert.That(belt.GetComponentsInChildren<Collider>(true), Has.Length.EqualTo(1));
            Assert.That(belt.GetComponentsInChildren<StableEntityIdAuthoring>(true), Has.Length.EqualTo(1));
            Assert.That(rig.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty);
            Assert.That(rig.GetComponentInChildren<SkinnedMeshRenderer>(true).bones, Has.Length.EqualTo(2));
            Assert.That(belt.Body.mass, Is.EqualTo(.5f));
        }

        [Test]
        public void InstalledBeltRigRejectsMeshWithoutOriginalSkinData()
        {
            var mesh = new Mesh();
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            try { Assert.Throws<System.IO.InvalidDataException>(() => Phase1SatsumaConsumableBeltPresentationAuthoring.CreateRig(mesh, material)); }
            finally { Object.DestroyImmediate(mesh); Object.DestroyImmediate(material); }
        }

        [Test]
        public void GeneratedBeltSkinBakesToEngineSizedGeometry()
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Phase1SatsumaInstalledBeltAssets.MeshPath);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(Phase1SatsumaInstalledBeltAssets.MaterialPath);
            Assert.That(mesh, Is.Not.Null, "Run the reviewed consumables refresh/import before this generated-asset check.");
            GameObject rig = Phase1SatsumaConsumableBeltPresentationAuthoring.CreateRig(mesh, material);
            var baked = new Mesh();
            try
            {
                rig.SetActive(true);
                SkinnedMeshRenderer renderer = rig.GetComponentInChildren<SkinnedMeshRenderer>();
                renderer.BakeMesh(baked);
                Assert.That(baked.vertexCount, Is.EqualTo(172));
                Bounds bounds = new Bounds(); bool first = true;
                foreach (Vector3 vertex in baked.vertices)
                {
                    Vector3 local = rig.transform.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                    if (first) { bounds = new Bounds(local, Vector3.zero); first = false; }
                    else bounds.Encapsulate(local);
                }
                Debug.Log("SATSUMA_INSTALLED_BELT_BAKED bounds=" + bounds.ToString("F6"));
                Assert.That(bounds.size.magnitude, Is.InRange(.1f, .6f));
                Assert.That(bounds.center.magnitude, Is.LessThan(.4f));
            }
            finally { Object.DestroyImmediate(rig); Object.DestroyImmediate(baked); }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly List<Object> owned = new();
            private readonly GameObject root = new("consumable test");
            private readonly GameObject loose = new("consumable test loose");
            private readonly List<PartInstance> parts = new();
            public readonly VehicleAssemblyController Assembly;
            public readonly Phase1SatsumaConsumableMountAuthoring.DefinitionSet Definitions;
            public readonly PartInstance Head;
            public readonly ToolDefinition AlternatorTool;
            private readonly PartInstance alternator;
            private readonly AssemblyEngineAdjustmentState alternatorAdjustment;

            public Fixture(bool apply = true)
            {
                PartInstance chassis = CreatePart(Def("vehicle.satsuma.part.body-shell"), root, true);
                PartInstance block = CreatePart(Def(Phase1SatsumaConsumableMountAuthoring.BlockId));
                Head = CreatePart(Def(Phase1SatsumaConsumableMountAuthoring.HeadId));
                PartInstance left = CreatePart(Def(Phase1SatsumaConsumableMountAuthoring.LeftHeadlightId));
                PartInstance right = CreatePart(Def(Phase1SatsumaConsumableMountAuthoring.RightHeadlightId));
                PartInstance filter = CreatePart(Def("vehicle.satsuma.part.oilfilter0"));
                alternator = CreatePart(Def("vehicle.satsuma.part.alternator"));
                PartInstance crank = CreatePart(Def("vehicle.satsuma.part.crankshaft-pulley"));
                PartInstance pump = CreatePart(Def("vehicle.satsuma.part.water-pump-pulley"));
                MountPointAuthoring[] mounts =
                {
                    AddMount("mount.satsuma.engine-assembly", chassis, block.Definition),
                    AddMount("mount.satsuma.engine-block.cylinder-head", block, Head.Definition),
                    AddMount("mount.satsuma.headlight-left", chassis, left.Definition),
                    AddMount("mount.satsuma.headlight-right", chassis, right.Definition),
                    AddMount("mount.satsuma.engine-block.oil-filter", block, filter.Definition),
                    AddMount(SatsumaConsumableAssemblyRules.AlternatorMountId, block, alternator.Definition),
                    AddMount(Phase1SatsumaConsumableMountAuthoring.BeltPrerequisites[0], block, crank.Definition),
                    AddMount(Phase1SatsumaConsumableMountAuthoring.BeltPrerequisites[1], block, pump.Definition),
                };
                var clamp = ScriptableObject.CreateInstance<FastenerDefinition>(); Track(clamp);
                clamp.Configure(SatsumaEngineAdjustmentRules.ClampFastenerId(SatsumaEngineAdjustmentKind.Alternator), "clamp",
                    FastenerSize.None, 8, FastenerDirection.ClockwiseToTighten, true, true, SatsumaAuxiliaryAssemblyTools.ScrewdriverRule());
                MountPointDefinition d = mounts[5].Definition;
                d.Configure(d.DefinitionId, d.DisplayName, d.SocketType, d.OwnerPartDefinitionId, d.AcceptedPartDefinitionIds,
                    d.Constraint, 0f, new[] { clamp });
                d.ConfigureFastenerGroup(FastenerGroupDefinition.CreateCompatibility(new[] { clamp }));
                AlternatorTool = SatsumaAuxiliaryAssemblyTools.CreateScrewdriver(); Track(AlternatorTool);
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(parts.ToArray(), mounts, Array.Empty<AssemblyDependency>(), new[] { AlternatorTool }, loose.transform);
                Definitions = Phase1SatsumaConsumableMountAuthoring.CreateDefinitions(filter.Definition);
                foreach (Object value in Definitions.Parts.Concat<Object>(Definitions.Mounts).Concat(Definitions.Fasteners)
                    .Append(Definitions.Catalog).Append(Definitions.SparkPlugWrench)) Track(value);
                if (apply)
                {
                    Phase1SatsumaConsumableMountAuthoring.ApplyToInstance(Assembly, Definitions);
                    ResetGraph(Assembly.MountPoints);
                }
                var alternatorVisual = new GameObject("alternator visual"); alternatorVisual.transform.SetParent(alternator.transform, false);
                alternatorAdjustment = alternator.gameObject.AddComponent<AssemblyEngineAdjustmentState>();
                alternatorAdjustment.Configure(SatsumaEngineAdjustmentKind.Alternator, alternator, Assembly,
                    Vector3.zero, Vector3.up, 0f, new[] { new AssemblyEngineAdjustmentPresentation(alternatorVisual.transform, Vector3.zero, Quaternion.identity) });
            }

            public void Track(Object value) => owned.Add(value);
            public PartInstance Part(string id) => parts.Single(part => part.Definition.DefinitionId == id);
            public MountPointAuthoring Mount(string id) => Assembly.MountPoints.Single(mount => mount.MountId == id);
            public void ResetGraph(MountPointAuthoring[] mounts) => Assembly.Configure(Assembly.Parts, mounts,
                Array.Empty<AssemblyDependency>(), Assembly.Tools, loose.transform, true);
            public PartInstance Purchase(PartDefinition definition, string item)
            {
                PartInstance part = CreatePart(definition, addBase: false);
                Assert.That(Assembly.TryRegisterDynamicPart(part, item, out string error), Is.True, error);
                return part;
            }
            public void InstallBeltPrerequisites()
            {
                foreach (string id in Phase1SatsumaConsumableMountAuthoring.BeltPrerequisites)
                {
                    var mount = Mount(id);
                    Install(Part(mount.Definition.AcceptedPartDefinitionIds[0]), mount);
                }
            }
            public void SetAlternator(float value) => alternatorAdjustment.RestoreValidated(
                new AssemblyEngineAdjustmentSaveDto { schemaVersion = 1, kind = SatsumaEngineAdjustmentKind.Alternator, value = value });
            public void Install(PartInstance part, MountPointAuthoring mount)
            {
                part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                part.Body.position = mount.Pose.position; part.Body.rotation = mount.Pose.rotation;
                AssemblyOperationResult result = Assembly.TryInstall(part, mount);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }
            public void Turn(MountPointAuthoring mount, ToolDefinition tool, bool tighten)
            {
                var result = Assembly.TryTurnFastener(mount.MountId, mount.Definition.Fasteners[0].DefinitionId, tool,
                    tighten ? FastenerRotationDirection.Clockwise : FastenerRotationDirection.CounterClockwise);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }
            private PartDefinition Def(string id)
            {
                var definition = ScriptableObject.CreateInstance<PartDefinition>(); Track(definition);
                definition.Configure(id, id, PartCategory.Engine, 1f, null, new[] { PartCompatibilityRule.Create("fixture") });
                return definition;
            }
            private PartInstance CreatePart(PartDefinition definition, GameObject existing = null, bool isRoot = false, bool addBase = true)
            {
                GameObject go = existing != null ? existing : new GameObject(definition.DefinitionId);
                if (!isRoot) go.transform.SetParent(loose.transform, false);
                var id = go.AddComponent<StableEntityIdAuthoring>(); id.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = go.AddComponent<Rigidbody>(); body.useGravity = false; body.mass = definition.MassKilograms;
                go.AddComponent<BoxCollider>().size = Vector3.one * .02f;
                var pickup = go.AddComponent<PhysicsPickupTarget>(); pickup.Configure(body, id, "test", 120f);
                var part = go.AddComponent<PartInstance>(); part.Configure(definition, id, body, pickup, isRoot, string.Empty);
                if (addBase) parts.Add(part);
                return part;
            }
            public MountPointAuthoring AddMount(string id, PartInstance owner, PartDefinition accepted)
            {
                var d = ScriptableObject.CreateInstance<MountPointDefinition>(); Track(d);
                d.Configure(id, id, "fixture", owner.Definition.DefinitionId, new[] { accepted.DefinitionId },
                    new MountConstraint(.22f, 50f, .75f, 0f), .03f, Array.Empty<FastenerDefinition>());
                var point = new GameObject(id); point.transform.SetParent(owner.transform, false);
                var mount = point.AddComponent<MountPointAuthoring>(); mount.Configure(d, id, point.transform, 0);
                point.AddComponent<AssemblyOwnedMountAuthoring>().Configure(owner);
                return mount;
            }
            public void Dispose()
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(loose);
                foreach (Object value in owned) if (value != null) Object.DestroyImmediate(value);
            }
        }
    }
}
