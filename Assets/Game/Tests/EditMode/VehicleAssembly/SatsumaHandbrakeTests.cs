using System;
using System.Collections.Generic;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaHandbrakeTests
    {
        private readonly List<ScriptableObject> definitions = new();
        private GameObject root;
        private VehicleAssemblyController assembly;
        private PartInstance body;
        private PartInstance part;
        private MountPointRuntime mount;
        private SatsumaHandbrakeController handbrake;
        private SatsumaHandbrakeInteractionTarget target;
        private Transform lever;
        private Quaternion restRotation;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Handbrake test fixture");
            body = CreatePart(root, "test.body", true, string.Empty);
            var partObject = new GameObject("Handbrake");
            partObject.transform.SetParent(root.transform, false);
            part = CreatePart(partObject, SatsumaHandbrakeController.PartDefinitionId,
                false, SatsumaHandbrakeController.MountId);

            var fasteners = new FastenerDefinition[5];
            var ids = new string[5];
            for (int index = 0; index < fasteners.Length; index++)
            {
                ids[index] = "fastener.satsuma.handbrake.boltpm-" + (index + 1);
                fasteners[index] = NewDefinition<FastenerDefinition>();
                FastenerSize size = index == 4
                    ? FastenerSize.Millimeter5
                    : FastenerSize.Millimeter8;
                fasteners[index].Configure(ids[index], ids[index], size, 8,
                    FastenerDirection.ClockwiseToTighten, true, true,
                    ToolCompatibilityRule.Create("Wrench", size));
            }

            MountPointDefinition mountDefinition = NewDefinition<MountPointDefinition>();
            mountDefinition.Configure(SatsumaHandbrakeController.MountId,
                "Handbrake", "test.handbrake", "test.body",
                new[] { SatsumaHandbrakeController.PartDefinitionId },
                new MountConstraint(1f, 180f, 1f, 0f), 0f, fasteners);
            var group = new FastenerGroupDefinition();
            group.Configure(ids, 40, 6, 0);
            mountDefinition.ConfigureFastenerGroup(group);
            var mountObject = new GameObject("Handbrake mount");
            mountObject.transform.SetParent(root.transform, false);
            MountPointAuthoring authoring = mountObject.AddComponent<MountPointAuthoring>();
            authoring.Configure(mountDefinition, SatsumaHandbrakeController.MountId,
                mountObject.transform, 0);
            assembly = root.AddComponent<VehicleAssemblyController>();
            assembly.Configure(new[] { body, part }, new[] { authoring },
                Array.Empty<AssemblyDependency>(), Array.Empty<ToolDefinition>(), root.transform);
            Assert.That(assembly.Graph.TryGetMount(SatsumaHandbrakeController.MountId,
                out mount), Is.True);
            Assert.That(mount.InstalledPart, Is.SameAs(part));

            lever = new GameObject("Moving lever only").transform;
            lever.SetParent(part.transform, false);
            restRotation = Quaternion.Euler(5f, 20f, -15f);
            lever.localRotation = restRotation;
            handbrake = root.AddComponent<SatsumaHandbrakeController>();
            handbrake.Configure(assembly, part, lever);
            target = partObject.AddComponent<SatsumaHandbrakeInteractionTarget>();
            target.Configure(handbrake);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            foreach (ScriptableObject definition in definitions)
            {
                Object.DestroyImmediate(definition);
            }

            definitions.Clear();
        }

        [Test]
        public void DefaultRestDoesNotBrakeAndHydraulicPartsAreNotRequired()
        {
            SetStages(8, 8, 8, 8, 8);
            Assert.That(handbrake.PositionDegrees, Is.EqualTo(0.1f));
            Assert.That(handbrake.BrakeInput01, Is.Zero);
            RestorePosition(20f);
            Assert.That(handbrake.CanOperate, Is.True);
            Assert.That(handbrake.BrakeInput01, Is.EqualTo(1f));
            Assert.That(assembly.Parts, Has.Length.EqualTo(2),
                "This fixture has no hydraulic lines, master cylinder or fluid system.");
        }

        [Test]
        public void FifthFastenerIsBinaryCableConnectionNotAnalogTension()
        {
            SetStages(8, 8, 8, 8, 0);
            RestorePosition(10f);
            Assert.That(handbrake.CanOperate, Is.True);
            Assert.That(handbrake.BrakeInput01, Is.Zero);
            SetStages(8, 8, 8, 8, 1);
            Assert.That(handbrake.BrakeInput01, Is.EqualTo(0.5f));
            SetStages(8, 8, 8, 8, 8);
            Assert.That(handbrake.BrakeInput01, Is.EqualTo(0.5f));
        }

        [Test]
        public void OperabilityFollowsDonorSixOnZeroOffLatch()
        {
            SetStages(4, 0, 0, 0, 1);
            Assert.That(handbrake.CanOperate, Is.False);
            SetStages(5, 0, 0, 0, 1);
            Assert.That(handbrake.CanOperate, Is.True);
            RestorePosition(20f);
            SetStages(0, 0, 0, 0, 1);
            Assert.That(handbrake.CanOperate, Is.True, "The donor bolted latch has hysteresis.");
            Assert.That(handbrake.BrakeInput01, Is.EqualTo(1f));
            SetStages(0, 0, 0, 0, 0);
            Assert.That(handbrake.CanOperate, Is.False);
            Assert.That(handbrake.BrakeInput01, Is.Zero);
        }

        [Test]
        public void MouseHoldMovesAtOneHundredDegreesPerSecondAndReleaseKeepsPosition()
        {
            SetStages(8, 8, 8, 8, 8);
            var context = new InteractionContext(root, Vector3.zero, Vector3.forward);
            Assert.That(target, Is.Not.InstanceOf<IContextInteractionTarget>(),
                "Handbrake must not expose an F/click-toggle capability.");
            target.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary);
            handbrake.Simulate(0.05f);
            Assert.That(handbrake.PositionDegrees, Is.EqualTo(5.1f).Within(0.0001f));
            Assert.That(target.ContinueContinuousInteraction(0.05f), Is.True);
            Assert.That(handbrake.PositionDegrees, Is.EqualTo(5.1f).Within(0.0001f),
                "Input continuation must not double-simulate the controller.");
            target.EndContinuousInteraction();
            handbrake.Simulate(1f);
            Assert.That(handbrake.PositionDegrees, Is.EqualTo(5.1f).Within(0.0001f));
            target.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Secondary);
            handbrake.Simulate(0.02f);
            Assert.That(handbrake.PositionDegrees, Is.EqualTo(3.1f).Within(0.0001f));
        }

        [Test]
        public void HeldMotionClampsAndRotatesOnlyAuthoredLeverFrame()
        {
            SetStages(8, 8, 8, 8, 8);
            Quaternion partRotation = part.transform.localRotation;
            Assert.That(handbrake.TrySetHeldDirection(1), Is.True);
            handbrake.Simulate(2f);
            Assert.That(handbrake.PositionDegrees, Is.EqualTo(20f));
            Assert.That(Quaternion.Angle(lever.localRotation,
                restRotation * Quaternion.AngleAxis(20f, Vector3.right)), Is.LessThan(0.001f));
            Assert.That(part.transform.localRotation, Is.EqualTo(partRotation));
            Assert.That(handbrake.TrySetHeldDirection(-1), Is.True);
            handbrake.Simulate(2f);
            Assert.That(handbrake.PositionDegrees, Is.Zero);
            Assert.That(handbrake.BrakeInput01, Is.Zero);
        }

        [Test]
        public void RestoreRoundTripClearsHeldIntentWithoutAudioEvent()
        {
            SetStages(8, 8, 8, 8, 8);
            int events = 0;
            handbrake.HoldStarted += _ => events++;
            handbrake.TrySetHeldDirection(1);
            handbrake.Simulate(0.08f);
            string json = JsonUtility.ToJson(handbrake.CaptureSaveData());
            handbrake.ResetState();
            Assert.That(handbrake.TryRestore(JsonUtility.FromJson<SatsumaHandbrakeSaveDto>(json),
                out string failure), Is.True, failure);
            Assert.That(handbrake.PositionDegrees, Is.EqualTo(8.1f).Within(0.0001f));
            Assert.That(handbrake.IsHeld, Is.False);
            Assert.That(events, Is.EqualTo(1));
            Assert.That(handbrake.TryRestore(null, out failure), Is.True, failure);
            Assert.That(handbrake.PositionDegrees, Is.EqualTo(0.1f));
        }

        [TestCase(-1f)]
        [TestCase(20.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void MalformedSaveDoesNotMutateState(float invalidPosition)
        {
            RestorePosition(12f);
            Assert.That(handbrake.TryRestore(new SatsumaHandbrakeSaveDto
                { positionDegrees = invalidPosition }, out _), Is.False);
            Assert.That(handbrake.PositionDegrees, Is.EqualTo(12f));
        }

        [Test]
        public void UnsupportedSaveSchemaIsRejected()
        {
            Assert.That(handbrake.TryRestore(new SatsumaHandbrakeSaveDto
                { schemaVersion = 99, positionDegrees = 10f }, out _), Is.False);
            Assert.That(handbrake.PositionDegrees, Is.EqualTo(0.1f));
        }

        [Test]
        public void UnavailableInteractionOrRemovalStopsHeldIntentAndBrakeDemand()
        {
            SetStages(8, 8, 8, 8, 8);
            var context = new InteractionContext(root, Vector3.zero, Vector3.forward);
            target.BeginContinuousInteraction(context, ContinuousContextInteractionDirection.Primary);
            handbrake.Simulate(0.05f);
            target.enabled = false;
            // Ordinary MonoBehaviour lifecycle callbacks do not run for this
            // EditMode fixture. Exercise the input owner's availability check
            // explicitly; actual OnDisable delivery belongs to PlayMode tests.
            Assert.That(target.ContinueContinuousInteraction(0.01f), Is.False);
            Assert.That(handbrake.IsHeld, Is.False);
            handbrake.TrySetHeldDirection(1);
            handbrake.enabled = false;
            handbrake.Simulate(0.01f);
            Assert.That(handbrake.IsHeld, Is.False);
            Assert.That(handbrake.BrakeInput01, Is.Zero);
            handbrake.enabled = true;
            SetStages(0, 0, 0, 0, 0);
            Assert.That(assembly.TryRemove(part).Succeeded, Is.True);
            handbrake.Simulate(0.1f);
            Assert.That(handbrake.CanOperate, Is.False);
            Assert.That(handbrake.BrakeInput01, Is.Zero);
        }

        [Test]
        public void StaleInstalledLifecycleCannotInventAnOccupiedMount()
        {
            Assert.That(assembly.TryRemove(part).Succeeded, Is.True);
            part.RuntimeState.SetInstalled(SatsumaHandbrakeController.MountId, false);
            RestorePosition(20f);
            Assert.That(handbrake.CanOperate, Is.False);
            Assert.That(handbrake.BrakeInput01, Is.Zero);
        }

        [Test]
        public void InvalidTimeDoesNotMoveLever()
        {
            SetStages(8, 8, 8, 8, 8);
            handbrake.TrySetHeldDirection(1);
            handbrake.Simulate(float.NaN);
            handbrake.Simulate(float.PositiveInfinity);
            handbrake.Simulate(-1f);
            Assert.That(handbrake.PositionDegrees, Is.EqualTo(0.1f));
        }

        [Test]
        public void AudioIntentFiresOncePerSuccessfulDirectionEntry()
        {
            var directions = new List<bool>();
            handbrake.HoldStarted += directions.Add;
            Assert.That(handbrake.TrySetHeldDirection(1), Is.False);
            Assert.That(directions, Is.Empty);
            SetStages(8, 8, 8, 8, 8);
            handbrake.TrySetHeldDirection(1);
            handbrake.TrySetHeldDirection(1);
            handbrake.Simulate(0.01f);
            handbrake.TrySetHeldDirection(-1);
            Assert.That(directions, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void PartialFasteningChecksBreakOnlyOnNewApplication()
        {
            SetStages(5, 0, 0, 0, 1);
            Assert.That(handbrake.TrySetHeldDirection(1, 1f), Is.True);
            handbrake.Simulate(0.01f);
            Assert.That(part.IsInstalled, Is.True, "The first application survives its sample.");
            handbrake.ReleaseHold();
            handbrake.TrySetHeldDirection(1, 0f);
            handbrake.Simulate(0.01f);
            Assert.That(part.IsInstalled, Is.True, "Still raised: do not reroll every mouse press.");
            handbrake.TrySetHeldDirection(-1);
            handbrake.Simulate(1f);
            Assert.That(handbrake.PositionDegrees, Is.Zero);
            handbrake.TrySetHeldDirection(1, 0f);
            handbrake.Simulate(0.01f);
            Assert.That(part.IsInstalled, Is.False);
            Assert.That(mount.IsOccupied, Is.False);
            Assert.That(handbrake.BrakeInput01, Is.Zero);
            Assert.That(handbrake.IsHeld, Is.False);
        }

        [Test]
        public void DonorBreakWeightsNeverBreakFullyFastenedAssembly()
        {
            Assert.That(SatsumaHandbrakeController.ShouldBreakForTightness(40, 0f), Is.False);
            Assert.That(SatsumaHandbrakeController.ShouldBreakForTightness(39, 0f), Is.False);
            Assert.That(SatsumaHandbrakeController.ShouldBreakForTightness(38, 0f), Is.True);
            Assert.That(SatsumaHandbrakeController.ShouldBreakForTightness(38, 0.03f), Is.False);
            Assert.That(SatsumaHandbrakeController.ShouldBreakForTightness(6, 0.45f), Is.True);
            Assert.That(SatsumaHandbrakeController.ShouldBreakForTightness(6, 0.46f), Is.False);
            Assert.That(SatsumaHandbrakeController.ShouldBreakForTightness(6, float.NaN), Is.False);
        }

        [Test]
        public void DamageDetachRejectsRootForeignAndLooseParts()
        {
            Assert.That(assembly.TryBreakInstalledPart(body).Succeeded, Is.False);
            Assert.That(assembly.TryBreakInstalledPart(null).Succeeded, Is.False);
            var foreignObject = new GameObject("Foreign part");
            try
            {
                PartInstance foreign = CreatePart(foreignObject,
                    SatsumaHandbrakeController.PartDefinitionId, false,
                    SatsumaHandbrakeController.MountId);
                foreign.RuntimeState.SetInstalled(SatsumaHandbrakeController.MountId, false);
                Assert.That(assembly.TryBreakInstalledPart(foreign).Succeeded, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(foreignObject);
            }

            Assert.That(assembly.TryRemove(part).Succeeded, Is.True);
            Assert.That(assembly.TryBreakInstalledPart(part).Succeeded, Is.False);
        }

        private PartInstance CreatePart(GameObject owner, string definitionId,
            bool isRoot, string initialMount)
        {
            PartDefinition definition = NewDefinition<PartDefinition>();
            definition.Configure(definitionId, definitionId, PartCategory.Brake, 1f, null,
                new[] { PartCompatibilityRule.Create("test.handbrake", "test.body") });
            StableEntityIdAuthoring identity = owner.AddComponent<StableEntityIdAuthoring>();
            identity.InitializeExplicitRuntimeId(StableEntityId.New());
            PartInstance instance = owner.AddComponent<PartInstance>();
            instance.Configure(definition, identity, null, null, isRoot, initialMount);
            return instance;
        }

        private T NewDefinition<T>() where T : ScriptableObject
        {
            T definition = ScriptableObject.CreateInstance<T>();
            definitions.Add(definition);
            return definition;
        }

        private void SetStages(params int[] stages)
        {
            for (int index = 0; index < stages.Length; index++)
            {
                Assert.That(mount.Fasteners[index].TryRestore(true, true, stages[index]), Is.True);
            }

            mount.FastenerGroup.Reevaluate(mount.IsOccupied);
        }

        private void RestorePosition(float position)
        {
            Assert.That(handbrake.TryRestore(new SatsumaHandbrakeSaveDto
                { positionDegrees = position }, out string failure), Is.True, failure);
        }
    }
}
