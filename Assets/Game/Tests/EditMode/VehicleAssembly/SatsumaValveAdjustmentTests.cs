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
using Migration = MSC.Vehicle.Assembly.SatsumaRockerShaftFastenerMigration;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaValveAdjustmentTests
    {
        [TestCase(0, 7f, 4f, 10f)] [TestCase(1, 6f, 3f, 9f)]
        public void OneNotchUsesActualDataLimitsNotTemplateSnapshot(int index, float initial, float minimum, float maximum)
        {
            Assert.That(AssemblyValveAdjustmentState.TryStep(index, initial, 99f, out float next), Is.True);
            Assert.That(next, Is.EqualTo(initial - 0.3f).Within(0.00001f));
            Assert.That(AssemblyValveAdjustmentState.TryStep(index, minimum, 1f, out next), Is.False);
            Assert.That(next, Is.EqualTo(minimum));
            Assert.That(AssemblyValveAdjustmentState.TryStep(index, maximum, -1f, out next), Is.False);
            Assert.That(next, Is.EqualTo(maximum));
        }

        [Test]
        public void SavedSettingsAreNotMountingStages_AndToolMustBeScrewdriver()
        {
            using var f = new Fixture();
            Assert.That(f.Target.TryActivateHeldTool(new Tool("Wrench", "6"), default, 1f), Is.False);
            Assert.That(f.Target.TryActivateHeldTool(new Tool("Screwdriver", "0"), default, 1f), Is.True);
            Assert.That(f.Settings.GetSetting(0), Is.EqualTo(6.7f).Within(0.00001f));
            VehicleAssemblySaveData save = f.Assembly.CaptureSaveData();
            PartSaveDto dto = save.parts.Single(value => value.hasValveAdjustment);
            Assert.That(save.fasteners.All(value => value.stage == 0), Is.True);
            f.Settings.TryAdjust(0, -1f);
            Assert.That(f.Assembly.RestoreSaveData(save).Succeeded, Is.True);
            Assert.That(f.Settings.GetSetting(0), Is.EqualTo(6.7f).Within(0.00001f));
            dto.valveAdjustment.intake = Vector4.one * 9f;
            Assert.That(f.Settings.GetSetting(0), Is.EqualTo(6.7f).Within(0.00001f));
            dto.hasValveAdjustment = false;
            Assert.That(f.Assembly.RestoreSaveData(save).Succeeded, Is.True);
            Assert.That(f.Settings.GetSetting(0), Is.EqualTo(7f));
        }

        [TestCase(0)] [TestCase(3)] [TestCase(8)]
        public void ExactRetirementPreservesFiveRealStagesAndDoesNotMutateSource(int retainedStage)
        {
            using var f = new Fixture(); VehicleAssemblySaveData old = f.Legacy(retainedStage);
            string original = JsonUtility.ToJson(old);
            Assert.That(f.Assembly.RestoreSaveData(old).Succeeded, Is.True);
            VehicleAssemblySaveData saved = f.Assembly.CaptureSaveData();
            Assert.That(saved.fasteners, Has.Length.EqualTo(5));
            Assert.That(saved.fasteners.Select(value => value.fastenerDefinitionId), Is.EquivalentTo(Migration.CanonicalIds));
            Assert.That(saved.fasteners.All(value => value.stage == retainedStage), Is.True);
            Assert.That(saved.fastenerGroups.Single().isBolted, Is.EqualTo(retainedStage > 0));
            Assert.That(f.Settings.Intake, Is.EqualTo(Vector4.one * 7f));
            Assert.That(JsonUtility.ToJson(old), Is.EqualTo(original));
            Assert.That(f.Assembly.RestoreSaveData(saved).Succeeded, Is.True);
        }

        [TestCase("partial")] [TestCase("duplicate")] [TestCase("bad-stage")]
        [TestCase("bad-latch")] [TestCase("invalid-valve")]
        public void CorruptOldOrNewStateRejectsBeforeMutation(string corruption)
        {
            using var f = new Fixture(); VehicleAssemblySaveData old = f.Legacy(2);
            switch (corruption)
            {
                case "partial": old.fasteners = old.fasteners.Skip(1).ToArray(); break;
                case "duplicate": old.fasteners[0].fastenerDefinitionId = old.fasteners[1].fastenerDefinitionId; break;
                case "bad-stage": old.fasteners.Last().stage = 9; break;
                case "bad-latch": old.fastenerGroups.Single().isBolted = false; break;
                case "invalid-valve": old.parts.Single(value => value.hasValveAdjustment).valveAdjustment.exhaust.x = 12f; break;
            }
            string before = JsonUtility.ToJson(f.Assembly.CaptureSaveData());
            Assert.That(f.Assembly.RestoreSaveData(old).Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(before));
        }

        [Test]
        public void CanonicalPrefabContainsEightTypedAdjustersAndFiveRetainedBolts()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            VehicleAssemblyController assembly = prefab.GetComponent<VehicleAssemblyController>();
            MountPointDefinition mount = assembly.MountPoints.Single(value => value.MountId == Migration.MountId).Definition;
            Assert.That(Migration.IsCanonicalShape(mount.Fasteners.Select(value => value.DefinitionId).ToArray()), Is.True,
                "Run the scoped valve authoring pass before integration tests.");
            Assert.That(mount.FastenerGroup.BoltedOnThreshold, Is.EqualTo(16));
            var targets = prefab.GetComponentsInChildren<AssemblyValveAdjustmentTarget>(true);
            Assert.That(targets, Has.Length.EqualTo(8));
            Assert.That(targets.Select(value => value.ValveIndex), Is.EquivalentTo(Enumerable.Range(0, 8)));
            Assert.That(targets.Select(value => value.ScrewRenderer).Distinct().Count(), Is.EqualTo(8));
            Assert.That(prefab.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Any(value => Migration.RetiredIds.Contains(value.FastenerDefinitionId)), Is.False);
        }

        private sealed class Tool : IHeldToolIdentity
        { public Tool(string type, string variant) { ToolType = type; ToolVariant = variant; }
          public string ToolType { get; } public string ToolVariant { get; } }
        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new("Valve fixture");
            private readonly List<ScriptableObject> assets = new();
            public VehicleAssemblyController Assembly { get; }
            public AssemblyValveAdjustmentState Settings { get; }
            public AssemblyValveAdjustmentTarget Target { get; }
            public Fixture()
            {
                PartInstance body = Part("test.body", true, "");
                PartInstance rocker = Part(AssemblyValveAdjustmentState.PartId, false, Migration.MountId);
                FastenerDefinition[] bolts = Migration.CanonicalIds.Select(id =>
                {
                    var bolt = Asset<FastenerDefinition>();
                    bolt.Configure(id, id, FastenerSize.Millimeter8, 8, FastenerDirection.ClockwiseToTighten,
                        true, true, ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter8)); return bolt;
                }).ToArray();
                var definition = Asset<MountPointDefinition>();
                definition.Configure(Migration.MountId, "Rocker shaft", "test.socket", "test.body",
                    new[] { AssemblyValveAdjustmentState.PartId }, new MountConstraint(.1f, 30f, 1f, 0f), 0f, bolts);
                var group = new FastenerGroupDefinition(); group.Configure(Migration.CanonicalIds, 40, 16, 0); definition.ConfigureFastenerGroup(group);
                var mountObject = new GameObject("Mount"); mountObject.transform.SetParent(root.transform, false);
                var mount = mountObject.AddComponent<MountPointAuthoring>(); mount.Configure(definition, Migration.MountId, mountObject.transform, 0);
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(new[] { body, rocker }, new[] { mount }, Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), root.transform);
                Settings = rocker.gameObject.AddComponent<AssemblyValveAdjustmentState>(); Settings.Configure(rocker, Assembly);
                var visual = new GameObject("Separate screw renderer"); visual.transform.SetParent(rocker.transform, false);
                var renderer = visual.AddComponent<MeshRenderer>();
                var control = new GameObject("Typed screwdriver point"); control.transform.SetParent(rocker.transform, false);
                control.AddComponent<SphereCollider>().isTrigger = true;
                Target = control.AddComponent<AssemblyValveAdjustmentTarget>(); Target.Configure(Settings, 0, renderer);
            }
            public VehicleAssemblySaveData Legacy(int stage)
            {
                VehicleAssemblySaveData data = Assembly.CaptureSaveData();
                foreach (FastenerSaveDto bolt in data.fasteners) { bolt.inserted = bolt.seated = true; bolt.stage = stage; }
                data.fasteners = data.fasteners.Concat(Migration.RetiredIds.Select(id => new FastenerSaveDto
                    { mountId = Migration.MountId, fastenerDefinitionId = id, inserted = true, seated = true, stage = 8 })).ToArray();
                data.fastenerGroups.Single().isBolted = true; return data;
            }
            private PartInstance Part(string id, bool isRoot, string mount)
            {
                var owner = new GameObject(id); owner.transform.SetParent(root.transform, false);
                var definition = Asset<PartDefinition>(); definition.Configure(id, id, PartCategory.Engine, 1f, null,
                    new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                var identity = owner.AddComponent<StableEntityIdAuthoring>(); identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var body = owner.AddComponent<Rigidbody>(); var pickup = owner.AddComponent<PhysicsPickupTarget>(); pickup.Configure(body, identity, id, 35f);
                var part = owner.AddComponent<PartInstance>(); part.Configure(definition, identity, body, pickup, isRoot, mount); return part;
            }
            private T Asset<T>() where T : ScriptableObject
            { T asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }
            public void Dispose() { Object.DestroyImmediate(root); foreach (ScriptableObject asset in assets) Object.DestroyImmediate(asset); }
        }
    }
}
