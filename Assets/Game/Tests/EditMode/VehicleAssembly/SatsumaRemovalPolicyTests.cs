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
    public sealed class SatsumaRemovalPolicyTests
    {
        [TestCase("fl", 0)]
        [TestCase("fl", 1)]
        [TestCase("fr", 0)]
        [TestCase("fr", 1)]
        public void BoltedBlockerUsesItsExactCornerAndTheDonorLatch(
            string corner,
            int installedVariant)
        {
            using var fixture = new RemovalFixture(corner, installedVariant,
                includeDependents: false);
            fixture.Target.Definition.ConfigureRemovalChecks(
                new[] { fixture.Blocker.MountId },
                Array.Empty<string>());

            fixture.SetTightness(fixture.WrongBlockerRuntime, 2);
            fixture.SetTightness(fixture.BlockerRuntime, 1);
            Assert.That(fixture.WrongBlockerRuntime.FastenerGroup.IsBolted, Is.True);
            Assert.That(fixture.BlockerRuntime.FastenerGroup.IsBolted, Is.False);
            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.TargetPart).Succeeded, Is.True,
                "The bolted opposite corner must not block this instance.");

            fixture.SetTightness(fixture.BlockerRuntime, 2);
            Assert.That(fixture.BlockerRuntime.FastenerGroup.IsBolted, Is.True);
            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.TargetPart).FailureReason,
                Is.EqualTo(AssemblyFailureReason.RemovalBlocked));

            fixture.SetTightness(fixture.BlockerRuntime, 1);
            Assert.That(fixture.BlockerRuntime.FastenerGroup.IsBolted, Is.True,
                "The blocker remains bolted after loosening below ON but above OFF.");
            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.TargetPart).FailureReason,
                Is.EqualTo(AssemblyFailureReason.RemovalBlocked));

            fixture.SetTightness(fixture.BlockerRuntime, 0);
            Assert.That(fixture.BlockerRuntime.FastenerGroup.IsBolted, Is.False);
            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.TargetPart).Succeeded, Is.True);
        }

        [TestCase("fl")]
        [TestCase("fr")]
        public void OwnFastenerLatchStillTakesPriorityOverExternalRemovalPolicy(string corner)
        {
            using var fixture = new RemovalFixture(corner, 0, includeDependents: false);
            fixture.Target.Definition.ConfigureRemovalChecks(
                new[] { fixture.Blocker.MountId },
                Array.Empty<string>());

            fixture.SetTightness(fixture.TargetRuntime, 2);
            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.TargetPart).FailureReason,
                Is.EqualTo(AssemblyFailureReason.FastenerSecured));

            fixture.SetTightness(fixture.TargetRuntime, 1);
            Assert.That(fixture.TargetRuntime.FastenerGroup.IsBolted, Is.True);
            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.TargetPart).FailureReason,
                Is.EqualTo(AssemblyFailureReason.FastenerSecured));

            fixture.SetTightness(fixture.TargetRuntime, 0);
            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.TargetPart).Succeeded, Is.True);
        }

        [TestCase("fl", 0)]
        [TestCase("fl", 1)]
        [TestCase("fr", 0)]
        [TestCase("fr", 1)]
        public void IgnoredDependentSuppressesOnlyTheExactInverseRemovalEdge(
            string corner,
            int installedVariant)
        {
            using var fixture = new RemovalFixture(corner, installedVariant);
            fixture.Target.Definition.ConfigureRemovalChecks(
                Array.Empty<string>(),
                new[] { fixture.Dependent.MountId });
            var actions = new List<AssemblyActionCompleted>();
            fixture.Assembly.ActionCompleted += actions.Add;

            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.TargetPart).Succeeded, Is.True);
            Assert.That(
                fixture.Assembly.EvaluateRemoval(fixture.OppositeTargetPart).FailureReason,
                Is.EqualTo(AssemblyFailureReason.RemovalBlocked),
                "An ignored FL/FR edge must not leak into the opposite socket.");

            Assert.That(fixture.Assembly.TryRemove(fixture.TargetPart).Succeeded, Is.True);
            Assert.That(fixture.TargetPart.IsInstalled, Is.False);
            Assert.That(fixture.DependentPart.IsInstalled, Is.True,
                "The manual exception is not a cascade instruction.");
            Assert.That(fixture.DependentRuntime.IsOccupied, Is.True);
            Assert.That(fixture.OppositeTargetPart.IsInstalled, Is.True);
            Assert.That(fixture.OppositeDependentPart.IsInstalled, Is.True);
            Assert.That(actions.Count(action => action.Action == AssemblyActionKind.PartRemoved),
                Is.EqualTo(1));
            Assert.That(actions.Any(action => action.Action == AssemblyActionKind.PartBrokenLoose),
                Is.False);
        }

        [TestCase("fl")]
        [TestCase("fr")]
        public void IgnoredDependentUsesTheInstalledInstanceWhenBothCornersShareDefinition(
            string corner)
        {
            using var fixture = new RemovalFixture(corner, 0,
                sameTargetDefinition: true);
            Assert.That(fixture.TargetPart.Definition,
                Is.SameAs(fixture.OppositeTargetPart.Definition));
            fixture.Target.Definition.ConfigureRemovalChecks(
                Array.Empty<string>(),
                new[] { fixture.Dependent.MountId });

            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.TargetPart).Succeeded, Is.True);
            Assert.That(
                fixture.Assembly.EvaluateRemoval(fixture.OppositeTargetPart).FailureReason,
                Is.EqualTo(AssemblyFailureReason.RemovalBlocked),
                "A shared PartDefinition must not make removal resolve the first occupied corner.");
        }

        [TestCase("fl")]
        [TestCase("fr")]
        public void UnconfiguredMountRetainsTheLegacyInverseRemovalBlock(string corner)
        {
            using var fixture = new RemovalFixture(corner, 0);
            Assert.That(fixture.Target.Definition.RemovalBlockedWhileBoltedMountIds, Is.Empty);
            Assert.That(fixture.Target.Definition.RemovalIgnoredDependentMountIds, Is.Empty);
            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.TargetPart).FailureReason,
                Is.EqualTo(AssemblyFailureReason.RemovalBlocked));
        }

        [TestCase("explicit")]
        [TestCase("owned")]
        public void IgnoredDependentDoesNotBypassOtherRemovalAuthorities(string authority)
        {
            using var fixture = new RemovalFixture("fl", 0,
                explicitRemovalBlocker: authority == "explicit",
                includeOwnedChild: authority == "owned");
            fixture.Target.Definition.ConfigureRemovalChecks(
                Array.Empty<string>(),
                new[] { fixture.Dependent.MountId });

            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.TargetPart).FailureReason,
                Is.EqualTo(AssemblyFailureReason.RemovalBlocked));
        }

        [TestCase("blocked", false)]
        [TestCase("blocked", true)]
        [TestCase("ignored", false)]
        [TestCase("ignored", true)]
        public void InvalidRemovalPolicyReferenceFailsClosedAndIsValidated(
            string policy,
            bool selfReference)
        {
            using var fixture = new RemovalFixture("fl", 0, includeDependents: false);
            string invalidId = selfReference
                ? fixture.Target.MountId
                : "missing.removal.mount";
            fixture.Target.Definition.ConfigureRemovalChecks(
                policy == "blocked" ? new[] { invalidId } : Array.Empty<string>(),
                policy == "ignored" ? new[] { invalidId } : Array.Empty<string>());

            string expectedCode = policy == "blocked"
                ? "MOUNT-REMOVAL-BLOCKED-BOLTED"
                : "MOUNT-REMOVAL-IGNORED-DEPENDENT";
            IReadOnlyList<VehicleAssemblyValidationIssue> issues =
                VehicleAssemblyValidator.Validate(
                    fixture.Assembly.Parts,
                    fixture.Assembly.MountPoints,
                    fixture.Assembly.Dependencies,
                    fixture.Assembly.Tools);

            Assert.That(issues.Any(issue => issue.Code == expectedCode &&
                issue.Severity == VehicleAssemblyValidationSeverity.Error), Is.True);
            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.TargetPart).FailureReason,
                Is.EqualTo(AssemblyFailureReason.RemovalBlocked),
                "Invalid policy data must not silently widen removal authority.");
        }

        [TestCase("fl")]
        [TestCase("fr")]
        public void ManualRemovalExceptionDoesNotChangeInstallOrForcedCollapse(string corner)
        {
            using var fixture = new RemovalFixture(corner, 0, includeIncoming: true);
            fixture.Target.Definition.ConfigureRemovalChecks(
                Array.Empty<string>(),
                new[] { fixture.Dependent.MountId });
            var actions = new List<AssemblyActionCompleted>();
            fixture.Assembly.ActionCompleted += actions.Add;

            Assert.That(fixture.Assembly.EvaluateInstall(
                fixture.IncomingPart, fixture.Incoming).Succeeded, Is.True);
            Assert.That(fixture.Assembly.TryInstall(
                fixture.IncomingPart, fixture.Incoming).Succeeded, Is.True);

            Assert.That(fixture.TargetPart.IsInstalled, Is.False);
            Assert.That(fixture.DependentPart.IsInstalled, Is.False,
                "The exception is manual-removal-only; structural collapse keeps its edge.");
            Assert.That(fixture.IncomingPart.IsInstalled, Is.False);
            Assert.That(fixture.OppositeTargetPart.IsInstalled, Is.True);
            Assert.That(actions.Count(action =>
                action.Action == AssemblyActionKind.PartInstalled &&
                action.Part == fixture.IncomingPart), Is.EqualTo(1));
            Assert.That(actions.Count(action => action.Action ==
                AssemblyActionKind.PartBrokenLoose), Is.EqualTo(3));
        }

        [Test]
        public void SaveRoundTripPreservesOccupancyLatchAndAppliesPolicyOnlyToNextCommand()
        {
            using var fixture = new RemovalFixture("rl", 1);
            fixture.Target.Definition.ConfigureRemovalChecks(
                new[] { fixture.Blocker.MountId },
                new[] { fixture.Dependent.MountId });
            fixture.SetTightness(fixture.BlockerRuntime, 2);
            VehicleAssemblySaveData saved = fixture.Assembly.CaptureSaveData();

            AssemblyOperationResult restore = fixture.Assembly.RestoreSaveData(saved);

            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(fixture.TargetRuntime.InstalledPart, Is.SameAs(fixture.TargetPart));
            Assert.That(fixture.BlockerRuntime.InstalledPart, Is.SameAs(fixture.BlockerPart));
            Assert.That(fixture.DependentRuntime.InstalledPart, Is.SameAs(fixture.DependentPart));
            Assert.That(fixture.BlockerRuntime.FastenerGroup.Tightness, Is.EqualTo(2));
            Assert.That(fixture.BlockerRuntime.FastenerGroup.IsBolted, Is.True);
            Assert.That(fixture.Assembly.EvaluateRemoval(fixture.TargetPart).FailureReason,
                Is.EqualTo(AssemblyFailureReason.RemovalBlocked));

            fixture.SetTightness(fixture.BlockerRuntime, 0);
            Assert.That(fixture.Assembly.TryRemove(fixture.TargetPart).Succeeded, Is.True,
                "After release, the audited inverse exception still applies to the restored graph.");
            Assert.That(fixture.DependentPart.IsInstalled, Is.True);
        }

        private sealed class RemovalFixture : IDisposable
        {
            private readonly List<ScriptableObject> definitions = new();
            private readonly GameObject root;
            private readonly GameObject visualTemplate;

            public RemovalFixture(
                string corner,
                int installedVariant,
                bool includeDependents = true,
                bool explicitRemovalBlocker = false,
                bool includeOwnedChild = false,
                bool includeIncoming = false,
                bool sameTargetDefinition = false)
            {
                string opposite = corner switch
                {
                    "fl" => "fr",
                    "fr" => "fl",
                    "rl" => "rr",
                    _ => "rl",
                };
                root = new GameObject("Satsuma removal policy fixture " + corner);
                root.transform.position = new Vector3(20f, 50f, -15f);
                visualTemplate = new GameObject("Test visual prefab");
                visualTemplate.transform.SetParent(root.transform, false);
                visualTemplate.SetActive(false);

                PartDefinition bodyDefinition = CreateDefinition("test.body");
                PartDefinition targetA = CreateDefinition("test.target-a");
                PartDefinition targetB = CreateDefinition("test.target-b");
                PartDefinition blockerDefinition = CreateDefinition("test.blocker");
                PartDefinition dependentDefinition = CreateDefinition("test.dependent");
                PartDefinition incomingDefinition = CreateDefinition("test.incoming");
                PartDefinition ownedDefinition = CreateDefinition("test.owned");

                BodyPart = CreatePart("body", bodyDefinition, true, string.Empty);
                PartDefinition selected = installedVariant == 0 ? targetA : targetB;
                PartDefinition other = sameTargetDefinition
                    ? selected
                    : installedVariant == 0 ? targetB : targetA;
                TargetPart = CreatePart("target-" + corner, selected, false,
                    "test.mount.target-" + corner);
                OppositeTargetPart = CreatePart("target-" + opposite, other, false,
                    "test.mount.target-" + opposite);
                BlockerPart = CreatePart("blocker-" + corner, blockerDefinition, false,
                    "test.mount.blocker-" + corner);
                WrongBlockerPart = CreatePart("blocker-" + opposite, blockerDefinition, false,
                    "test.mount.blocker-" + opposite);

                var parts = new List<PartInstance>
                {
                    BodyPart,
                    TargetPart,
                    OppositeTargetPart,
                    BlockerPart,
                    WrongBlockerPart,
                };
                var mounts = new List<MountPointAuthoring>();
                Target = CreateMount("test.mount.target-" + corner,
                    new[] { targetA, targetB }, bodyDefinition.DefinitionId);
                OppositeTarget = CreateMount("test.mount.target-" + opposite,
                    new[] { targetA, targetB }, bodyDefinition.DefinitionId);
                Blocker = CreateMount("test.mount.blocker-" + corner,
                    new[] { blockerDefinition }, bodyDefinition.DefinitionId);
                WrongBlocker = CreateMount("test.mount.blocker-" + opposite,
                    new[] { blockerDefinition }, bodyDefinition.DefinitionId);
                mounts.AddRange(new[] { Target, OppositeTarget, Blocker, WrongBlocker });

                if (includeDependents)
                {
                    DependentPart = CreatePart("dependent-" + corner,
                        dependentDefinition, false, "test.mount.dependent-" + corner);
                    OppositeDependentPart = CreatePart("dependent-" + opposite,
                        dependentDefinition, false, "test.mount.dependent-" + opposite);
                    Dependent = CreateMount("test.mount.dependent-" + corner,
                        new[] { dependentDefinition }, bodyDefinition.DefinitionId);
                    OppositeDependent = CreateMount("test.mount.dependent-" + opposite,
                        new[] { dependentDefinition }, bodyDefinition.DefinitionId);
                    Dependent.Definition.ConfigureSequence(
                        new[] { Target.MountId }, Array.Empty<string>(), Array.Empty<string>());
                    OppositeDependent.Definition.ConfigureSequence(
                        new[] { OppositeTarget.MountId }, Array.Empty<string>(), Array.Empty<string>());
                    parts.Add(DependentPart);
                    parts.Add(OppositeDependentPart);
                    mounts.Add(Dependent);
                    mounts.Add(OppositeDependent);
                }

                if (includeOwnedChild)
                {
                    PartInstance ownedPart = CreatePart("owned-child", ownedDefinition, false,
                        "test.mount.owned-child");
                    MountPointAuthoring ownedMount = CreateMount("test.mount.owned-child",
                        new[] { ownedDefinition }, TargetPart.Definition.DefinitionId);
                    parts.Add(ownedPart);
                    mounts.Add(ownedMount);
                }

                if (includeIncoming)
                {
                    IncomingPart = CreatePart("incoming", incomingDefinition, false, string.Empty);
                    Incoming = CreateMount("test.mount.incoming-" + corner,
                        new[] { incomingDefinition }, bodyDefinition.DefinitionId);
                    Incoming.Definition.ConfigureSequence(
                        new[] { Target.MountId }, Array.Empty<string>(), Array.Empty<string>());
                    Incoming.Definition.ConfigureInstallationChecks(
                        Target.MountId, Array.Empty<string>());
                    parts.Add(IncomingPart);
                    mounts.Add(Incoming);
                }

                AssemblyDependency[] dependencies = explicitRemovalBlocker
                    ? new[]
                    {
                        AssemblyDependency.Create(
                            TargetPart.Definition.DefinitionId,
                            BlockerPart.Definition.DefinitionId,
                            AssemblyDependencyKind.RemovalBlockedWhileInstalled),
                    }
                    : Array.Empty<AssemblyDependency>();
                ToolDefinition tool = NewDefinition<ToolDefinition>();
                tool.Configure("test.tool", "Test wrench", "Wrench", FastenerSize.Millimeter8);
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(parts.ToArray(), mounts.ToArray(), dependencies,
                    new[] { tool }, root.transform);

                TargetRuntime = Assembly.ResolveMount(Target);
                BlockerRuntime = Assembly.ResolveMount(Blocker);
                WrongBlockerRuntime = Assembly.ResolveMount(WrongBlocker);
                if (Dependent != null)
                    DependentRuntime = Assembly.ResolveMount(Dependent);

                if (IncomingPart != null)
                {
                    IncomingPart.transform.SetPositionAndRotation(
                        Incoming.Pose.position, Incoming.Pose.rotation);
                    IncomingPart.Body.position = Incoming.Pose.position;
                    IncomingPart.Body.rotation = Incoming.Pose.rotation;
                    Physics.SyncTransforms();
                }
            }

            public VehicleAssemblyController Assembly { get; }
            public PartInstance BodyPart { get; }
            public PartInstance TargetPart { get; }
            public PartInstance OppositeTargetPart { get; }
            public PartInstance BlockerPart { get; }
            public PartInstance WrongBlockerPart { get; }
            public PartInstance DependentPart { get; }
            public PartInstance OppositeDependentPart { get; }
            public PartInstance IncomingPart { get; }
            public MountPointAuthoring Target { get; }
            public MountPointAuthoring OppositeTarget { get; }
            public MountPointAuthoring Blocker { get; }
            public MountPointAuthoring WrongBlocker { get; }
            public MountPointAuthoring Dependent { get; }
            public MountPointAuthoring OppositeDependent { get; }
            public MountPointAuthoring Incoming { get; }
            public MountPointRuntime TargetRuntime { get; }
            public MountPointRuntime BlockerRuntime { get; }
            public MountPointRuntime WrongBlockerRuntime { get; }
            public MountPointRuntime DependentRuntime { get; }

            public void SetTightness(MountPointRuntime mount, int total)
            {
                int remaining = total;
                foreach (FastenerInstance fastener in mount.Fasteners)
                {
                    int stage = Mathf.Min(remaining, fastener.Definition.MaximumStage);
                    Assert.That(fastener.TryRestore(true, true, stage), Is.True);
                    remaining -= stage;
                }

                Assert.That(remaining, Is.Zero);
                mount.FastenerGroup.Reevaluate(true);
                Assert.That(mount.FastenerGroup.Tightness, Is.EqualTo(total));
            }

            private PartDefinition CreateDefinition(string id)
            {
                PartDefinition definition = NewDefinition<PartDefinition>();
                definition.Configure(id, id, PartCategory.Suspension, 1f,
                    visualTemplate,
                    new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                return definition;
            }

            private PartInstance CreatePart(
                string name,
                PartDefinition definition,
                bool assemblyRoot,
                string initialMountId)
            {
                var owner = new GameObject(name);
                owner.transform.SetParent(root.transform, false);
                StableEntityIdAuthoring identity = owner.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                Rigidbody body = owner.AddComponent<Rigidbody>();
                PhysicsPickupTarget pickup = owner.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(body, identity, name, 35f, useGravityWhenLoose: true);
                PartInstance part = owner.AddComponent<PartInstance>();
                part.Configure(definition, identity, body, pickup, assemblyRoot, initialMountId);
                return part;
            }

            private MountPointAuthoring CreateMount(
                string id,
                PartDefinition[] accepted,
                string ownerDefinitionId)
            {
                FastenerDefinition fastener = NewDefinition<FastenerDefinition>();
                fastener.Configure(id + ".bolt", "Test bolt", FastenerSize.Millimeter8, 8,
                    FastenerDirection.ClockwiseToTighten, true, true,
                    ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter8));
                MountPointDefinition definition = NewDefinition<MountPointDefinition>();
                definition.Configure(id, id, "test.socket", ownerDefinitionId,
                    accepted.Select(value => value.DefinitionId).ToArray(),
                    new MountConstraint(0.2f, 40f, 1f, 0f), 0f, new[] { fastener });
                var group = new FastenerGroupDefinition();
                group.Configure(new[] { fastener.DefinitionId }, 8, 2, 0);
                definition.ConfigureFastenerGroup(group);
                var owner = new GameObject(id);
                owner.transform.SetParent(root.transform, false);
                owner.transform.localPosition = Vector3.right *
                    (root.transform.childCount * 0.25f);
                MountPointAuthoring mount = owner.AddComponent<MountPointAuthoring>();
                mount.Configure(definition, id, owner.transform, 0);
                return mount;
            }

            private T NewDefinition<T>() where T : ScriptableObject
            {
                T definition = ScriptableObject.CreateInstance<T>();
                definitions.Add(definition);
                return definition;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                foreach (ScriptableObject definition in definitions)
                    Object.DestroyImmediate(definition);
            }
        }
    }
}
