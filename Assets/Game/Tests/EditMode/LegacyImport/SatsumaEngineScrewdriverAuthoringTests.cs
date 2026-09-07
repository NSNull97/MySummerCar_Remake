using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Core.Identity;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Authoring = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaEngineScrewdriverAuthoring;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaEngineScrewdriverAuthoringTests
    {
        [Test]
        public void ReviewedClampsAcceptScrewdriverAndPreserveFastenerDataAndPose()
        {
            using var fixture = new Fixture();
            Vector3[] basePositions = fixture.Targets.Select(BasePosition).ToArray();
            Assert.That(Authoring.Configure(fixture.Assembly, fixture.Screwdriver), Is.EqualTo(7));
            for (int index = 0; index < 3; index++)
            {
                FastenerDefinition fastener = fixture.Fasteners[index];
                Assert.That(fastener.DefinitionId, Is.EqualTo(Authoring.FastenerIds[index]));
                Assert.That(fastener.Size, Is.EqualTo(FastenerSize.Millimeter6));
                Assert.That(fastener.MaximumStage, Is.EqualTo(8 + index));
                Assert.That(fastener.DisplayName, Is.EqualTo("Clamp " + index));
                Assert.That(fastener.TighteningDirection, Is.EqualTo(FastenerDirection.CounterClockwiseToTighten));
                Assert.That(fastener.InsertedOnInstall, Is.True);
                Assert.That(fastener.RequiredForRemoval, Is.True);
                Assert.That(fastener.ToolRule.Matches(fixture.Screwdriver), Is.True);
                Assert.That(fastener.ToolRule.Matches(fixture.Wrench), Is.False);
                Assert.That(fastener.ToolRule.FastenerSize, Is.EqualTo(FastenerSize.None));
                Assert.That(TargetTool(fixture.Targets[index]), Is.SameAs(fixture.Screwdriver));
                Assert.That(BasePosition(fixture.Targets[index]), Is.EqualTo(basePositions[index]));
                Assert.That(fixture.Assembly.MountPoints[index].Definition.Fasteners.Length, Is.EqualTo(1));
            }
            Assert.That(fixture.Assembly.Tools, Is.EqualTo(new[] { fixture.Wrench, fixture.Screwdriver }));
        }

        [Test]
        public void UnreviewedCarburettorAndOrdinaryWrenchBindingStayUntouched()
        {
            using var fixture = new Fixture();
            string before = EditorJsonUtility.ToJson(fixture.Fasteners[3]);
            Authoring.Configure(fixture.Assembly, fixture.Screwdriver);
            Assert.That(EditorJsonUtility.ToJson(fixture.Fasteners[3]), Is.EqualTo(before));
            Assert.That(TargetTool(fixture.Targets[3]), Is.SameAs(fixture.Wrench));
            Assert.That(fixture.Fasteners[3].ToolRule.Matches(fixture.Screwdriver), Is.False);
            Assert.That(fixture.Assembly.Tools.Any(tool => tool.ToolType == "SparkPlugWrench"), Is.False);
        }

        [Test]
        public void RepeatedPassIsIdempotentAndRepairsAnExistingWrongTargetReference()
        {
            using var fixture = new Fixture();
            Authoring.Configure(fixture.Assembly, fixture.Screwdriver);
            Assert.That(Authoring.Configure(fixture.Assembly, fixture.Screwdriver), Is.Zero);
            var serialized = new SerializedObject(fixture.Targets[1]);
            serialized.FindProperty("tool").objectReferenceValue = fixture.Wrench;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(Authoring.Configure(fixture.Assembly, fixture.Screwdriver), Is.EqualTo(1));
            Assert.That(TargetTool(fixture.Targets[1]), Is.SameAs(fixture.Screwdriver));
            Assert.That(Authoring.Configure(fixture.Assembly, fixture.Screwdriver), Is.Zero);
        }

        [Test]
        public void MissingReviewedTargetFailsBeforeAnyDefinitionsOrRegistryChange()
        {
            using var fixture = new Fixture();
            Object.DestroyImmediate(fixture.Targets[2]);
            Assert.Throws<InvalidDataException>(() => Authoring.Configure(fixture.Assembly, fixture.Screwdriver));
            Assert.That(fixture.Fasteners.All(fastener => fastener.ToolRule.Matches(fixture.Wrench)), Is.True);
            Assert.That(fixture.Assembly.Tools, Is.EqualTo(new[] { fixture.Wrench }));
        }

        [Test]
        public void WrongToolIsRejectedWithoutChangingAssembly()
        {
            using var fixture = new Fixture();
            Assert.Throws<InvalidDataException>(() => Authoring.Configure(fixture.Assembly, fixture.Wrench));
            Assert.That(fixture.Fasteners.All(fastener => fastener.ToolRule.Matches(fixture.Wrench)), Is.True);
            Assert.That(fixture.Assembly.Tools.Length, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeScrollMatcherUsesScrewdriverZeroAndRejectsSixMillimeterWrench()
        {
            using var fixture = new Fixture();
            Authoring.Configure(fixture.Assembly, fixture.Screwdriver);
            for (int index = 0; index < 3; index++)
            {
                MountPointRuntime mount = fixture.Assembly.ResolveMount(fixture.Assembly.MountPoints[index]);
                mount.Fasteners[0].ResetForInstalledPart();
                Assert.That(fixture.Targets[index].CanActivateHeldTool(
                    new HeldTool("Screwdriver", "0"), default,
                    MSC.Interaction.Capabilities.InteractionScrollDirection.Positive), Is.True);
                Assert.That(fixture.Targets[index].CanActivateHeldTool(
                    new HeldTool("Wrench", "6"), default,
                    MSC.Interaction.Capabilities.InteractionScrollDirection.Positive), Is.False);
            }
        }

        private static ToolDefinition TargetTool(AssemblyFastenerInteractionTarget target) =>
            new SerializedObject(target).FindProperty("tool").objectReferenceValue as ToolDefinition;

        private static Vector3 BasePosition(AssemblyFastenerInteractionTarget target) =>
            new SerializedObject(target).FindProperty("fastenerPresentationBaseLocalPosition").vector3Value;

        private sealed class HeldTool : MSC.Interaction.Capabilities.IHeldToolIdentity
        {
            public HeldTool(string type, string variant) { ToolType = type; ToolVariant = variant; }
            public string ToolType { get; }
            public string ToolVariant { get; }
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("engine screw binding fixture");
            private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
            public readonly VehicleAssemblyController Assembly;
            public readonly ToolDefinition Screwdriver;
            public readonly ToolDefinition Wrench;
            public readonly FastenerDefinition[] Fasteners = new FastenerDefinition[4];
            public readonly AssemblyFastenerInteractionTarget[] Targets = new AssemblyFastenerInteractionTarget[4];

            public Fixture()
            {
                Wrench = ScriptableObject.CreateInstance<ToolDefinition>();
                Wrench.Configure("test.wrench6", "Wrench 6", "Wrench", FastenerSize.Millimeter6);
                Screwdriver = SatsumaAuxiliaryAssemblyTools.CreateScrewdriver();
                definitions.Add(Wrench);
                definitions.Add(Screwdriver);
                var mounts = new MountPointAuthoring[4];
                var parts = new PartInstance[4];
                for (int index = 0; index < 4; index++)
                {
                    string mountId = index < 3 ? Authoring.MountIds[index] : "mount.test.carburettor";
                    string fastenerId = index < 3 ? Authoring.FastenerIds[index] : "fastener.test.carburettor-tuning";
                    string partId = "test.part." + index;
                    FastenerDefinition fastener = ScriptableObject.CreateInstance<FastenerDefinition>();
                    fastener.Configure(fastenerId, "Clamp " + index, FastenerSize.Millimeter6, 8 + index,
                        FastenerDirection.CounterClockwiseToTighten, true, true,
                        ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter6));
                    Fasteners[index] = fastener;
                    definitions.Add(fastener);
                    MountPointDefinition definition = ScriptableObject.CreateInstance<MountPointDefinition>();
                    definition.Configure(mountId, mountId, "test.socket", string.Empty,
                        new[] { partId }, new MountConstraint(1f, 180f, 1f, 0f), 0f, new[] { fastener });
                    definitions.Add(definition);
                    var socket = new GameObject(mountId);
                    socket.transform.SetParent(root.transform);
                    mounts[index] = socket.AddComponent<MountPointAuthoring>();
                    mounts[index].Configure(definition, mountId, socket.transform, 0);
                    PartDefinition partDefinition = ScriptableObject.CreateInstance<PartDefinition>();
                    partDefinition.Configure(partId, partId, PartCategory.Engine, 1f, root,
                        new[] { PartCompatibilityRule.Create("test.socket", string.Empty) });
                    definitions.Add(partDefinition);
                    var partObject = new GameObject(partId);
                    partObject.transform.SetParent(root.transform);
                    var identity = partObject.AddComponent<StableEntityIdAuthoring>();
                    identity.InitializeExplicitRuntimeId(StableEntityId.New());
                    var body = partObject.AddComponent<Rigidbody>();
                    parts[index] = partObject.AddComponent<PartInstance>();
                    parts[index].Configure(partDefinition, identity, body, null, false, mountId);
                }
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(parts, mounts, Array.Empty<AssemblyDependency>(),
                    new[] { Wrench }, root.transform);
                for (int index = 0; index < 4; index++)
                {
                    var screw = new GameObject("screw " + index);
                    screw.transform.SetParent(mounts[index].transform);
                    screw.transform.localPosition = new Vector3(0.01f, 0.02f, 0.03f);
                    Targets[index] = screw.AddComponent<AssemblyFastenerInteractionTarget>();
                    Targets[index].Configure(Assembly, mounts[index].MountId,
                        Fasteners[index].DefinitionId, Wrench, false, screw.transform, 0.5f);
                }
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                foreach (ScriptableObject definition in definitions) Object.DestroyImmediate(definition);
            }
        }
    }
}
