using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaMechanicalConditionTests
    {
        [Test]
        public void Wear_IsPartOwned_AndSurvivesCaptureRestoreAndRebind()
        {
            using var f = new Fixture();
            Assert.That(f.Health.TryApplyWear(37.25f), Is.True);
            VehicleAssemblySaveData saved = f.Assembly.CaptureSaveData();
            PartSaveDto part = saved.parts.Single(value => value.stableEntityId == f.Part.StableId.Value);
            Assert.That(part.hasMechanicalCondition, Is.True);
            Assert.That(part.mechanicalCondition.conditionPercent, Is.EqualTo(62.75f));
            f.Health.TryApplyWear(10f); f.Health.Configure(f.Part);
            Assert.That(f.Health.ConditionPercent, Is.EqualTo(52.75f));
            Assert.That(f.Assembly.RestoreSaveData(saved).Succeeded, Is.True);
            Assert.That(f.Health.ConditionPercent, Is.EqualTo(62.75f));
            part.mechanicalCondition.conditionPercent = 1f;
            Assert.That(f.Health.ConditionPercent, Is.EqualTo(62.75f));
        }

        [Test]
        public void LegacyOmissionHasExplicitHealthyDefault_NoActivationResetForNewState()
        {
            using var f = new Fixture(); f.Health.TryApplyWear(10f);
            f.Part.gameObject.SetActive(false); f.Part.gameObject.SetActive(true);
            Assert.That(f.Health.ConditionPercent, Is.EqualTo(90f));
            VehicleAssemblySaveData saved = f.Assembly.CaptureSaveData();
            saved.parts.Single(value => value.hasMechanicalCondition).hasMechanicalCondition = false;
            Assert.That(f.Assembly.RestoreSaveData(saved).Succeeded, Is.True);
            Assert.That(f.Health.ConditionPercent, Is.EqualTo(100f));
        }

        [TestCase("nan")] [TestCase("version")] [TestCase("wrong-part")] [TestCase("zero-not-broken")]
        public void InvalidWearFailsBeforeAssemblyMutation(string corruption)
        {
            using var f = new Fixture(); VehicleAssemblySaveData saved = f.Assembly.CaptureSaveData();
            PartSaveDto dto = saved.parts.Single(value => value.hasMechanicalCondition);
            switch (corruption)
            {
                case "nan": dto.mechanicalCondition.conditionPercent = float.NaN; break;
                case "version": dto.mechanicalCondition.schemaVersion = 12; break;
                case "wrong-part":
                    saved.parts[0].hasMechanicalCondition = true;
                    saved.parts[0].mechanicalCondition = dto.mechanicalCondition.Clone(); break;
                case "zero-not-broken": dto.mechanicalCondition.conditionPercent = 0f; break;
            }
            string checkpoint = JsonUtility.ToJson(f.Assembly.CaptureSaveData());
            Assert.That(f.Assembly.RestoreSaveData(saved).Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(checkpoint));
        }

        [Test]
        public void SubFloatUlpWearAccumulates_AndCannotHealOrOverflow()
        {
            using var f = new Fixture();
            for (int i = 0; i < 100000; i++) f.Health.TryApplyWear(0.0000001f);
            Assert.That(f.Health.ConditionPercent, Is.EqualTo(99.99f).Within(0.00001f));
            Assert.That(f.Health.TryApplyWear(float.NaN), Is.False);
            Assert.That(f.Health.TryApplyWear(-1f), Is.False);
            f.Health.TryApplyWear(1000f);
            Assert.That(f.Health.ConditionPercent, Is.Zero); Assert.That(f.Health.IsBroken, Is.True);
            Assert.That(f.Health.CaptureSaveData().IsValid, Is.True);
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new("Wear fixture");
            private readonly List<ScriptableObject> assets = new();
            public VehicleAssemblyController Assembly { get; }
            public PartInstance Part { get; }
            public AssemblyMechanicalConditionState Health { get; }
            public Fixture()
            {
                PartInstance body = CreatePart("test.body", true);
                Part = CreatePart("vehicle.satsuma.part.crankshaft", false);
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(new[] { body, Part }, Array.Empty<MountPointAuthoring>(),
                    Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), root.transform);
                Health = Part.gameObject.AddComponent<AssemblyMechanicalConditionState>(); Health.Configure(Part);
            }
            private PartInstance CreatePart(string id, bool assemblyRoot)
            {
                var owner = new GameObject(id); owner.transform.SetParent(root.transform, false);
                var definition = ScriptableObject.CreateInstance<PartDefinition>(); assets.Add(definition);
                definition.Configure(id, id, PartCategory.Engine, 1f, null,
                    new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                var identity = owner.AddComponent<StableEntityIdAuthoring>(); identity.InitializeExplicitRuntimeId(StableEntityId.New());
                Rigidbody rigidbody = owner.AddComponent<Rigidbody>();
                PhysicsPickupTarget pickup = owner.AddComponent<PhysicsPickupTarget>(); pickup.Configure(rigidbody, identity, id, 35f);
                PartInstance part = owner.AddComponent<PartInstance>(); part.Configure(definition, identity, rigidbody, pickup, assemblyRoot, "");
                return part;
            }
            public void Dispose()
            { Object.DestroyImmediate(root); foreach (ScriptableObject asset in assets) Object.DestroyImmediate(asset); }
        }
    }
}
