using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Notifications;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaFrontInstallPolicyTests
    {
        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        [TestCase(false, true)]
        public void InstallationPolicyRejectsUnknownOrSelfReferences(bool supportCheck, bool self)
        {
            using var fixture = new AttemptFixture("fl");
            string invalidId = self ? fixture.Target.MountId : "missing.installation.mount";
            fixture.Target.Definition.ConfigureInstallationChecks(
                supportCheck ? invalidId : string.Empty,
                supportCheck ? Array.Empty<string>() : new[] { invalidId });
            var issues = VehicleAssemblyValidator.Validate(fixture.Assembly.Parts,
                fixture.Assembly.MountPoints, fixture.Assembly.Dependencies, fixture.Assembly.Tools);
            string code = supportCheck ? "MOUNT-INSTALLATION-SUPPORT" : "MOUNT-INSTALLATION-BLOCKED-BOLTED";
            Assert.That(issues.Any(issue => issue.Code == code &&
                issue.Severity == VehicleAssemblyValidationSeverity.Error), Is.True);
            Assert.That(fixture.Assembly.TryInstall(fixture.Incoming, fixture.Target).Succeeded, Is.False);
            Assert.That(fixture.Support.IsInstalled, Is.True);
            Assert.That(fixture.Incoming.IsInstalled, Is.False);
            if (!supportCheck)
                Assert.That(fixture.Assembly.EvaluateInstall(fixture.Incoming, fixture.Target).Succeeded, Is.False,
                    "An invalid inverse rule must not silently become an unrestricted install.");
        }

        [TestCase("fl")]
        [TestCase("fr")]
        public void LooseInstalledSupportAllowsPreviewWithoutMutation(string corner)
        {
            using var fixture = new AttemptFixture(corner);
            var actions = new List<AssemblyActionCompleted>();
            fixture.Assembly.ActionCompleted += actions.Add;

            Assert.That(fixture.Assembly.EvaluateInstall(fixture.Incoming, fixture.Target).Succeeded,
                Is.True);
            Assert.That(fixture.Assembly.EvaluateHandoffInstall(fixture.Incoming, fixture.Target).Succeeded,
                Is.True);
            Assert.That(fixture.Assembly.TryPrepareHandoffInstall(fixture.Incoming, fixture.Target).Succeeded,
                Is.True, "Pre-release preparation validates presence without collapsing the support.");
            AssemblyMountCandidate candidate = fixture.Assembly.FindBestMount(fixture.Incoming);
            Assert.That(candidate.IsValid, Is.True);
            Assert.That(candidate.Mount.Authoring, Is.SameAs(fixture.Target));
            Assert.That(fixture.Support.IsInstalled, Is.True);
            Assert.That(fixture.SupportRuntime.FastenerGroup.IsBolted, Is.False);
            Assert.That(fixture.Incoming.IsInstalled, Is.False);
            Assert.That(fixture.TargetRuntime.IsOccupied, Is.False);
            Assert.That(actions, Is.Empty);
        }

        [TestCase("fl")]
        [TestCase("fr")]
        public void MissingSupportRejectsPreviewAndAttemptWithoutDetachment(string corner)
        {
            using var fixture = new AttemptFixture(corner, supportInstalled: false);
            var actions = new List<AssemblyActionCompleted>();
            fixture.Assembly.ActionCompleted += actions.Add;

            Assert.That(fixture.Assembly.EvaluateInstall(fixture.Incoming, fixture.Target).FailureReason,
                Is.EqualTo(AssemblyFailureReason.MissingPrerequisite));
            Assert.That(fixture.Assembly.FindBestMount(fixture.Incoming).IsValid, Is.False);
            AssemblyOperationResult result = fixture.Assembly.TryInstall(fixture.Incoming, fixture.Target);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.MissingPrerequisite));
            Assert.That(fixture.Incoming.IsInstalled, Is.False);
            Assert.That(fixture.Opposite.IsInstalled, Is.True);
            Assert.That(actions, Is.Empty);
        }

        [TestCase("fl", "direct")]
        [TestCase("fr", "direct")]
        [TestCase("fl", "handoff")]
        [TestCase("fr", "handoff")]
        [TestCase("fl", "begin")]
        [TestCase("fr", "begin")]
        public void UnboltedSupportInstallsIncomingThenCollapsesAffectedAssembly(
            string corner, string entryPoint)
        {
            using var fixture = new AttemptFixture(corner);
            if (entryPoint != "direct")
                fixture.PlaceIncoming(fixture.Target.Pose.position + Vector3.right * 0.2f);

            var actions = new List<AssemblyActionCompleted>();
            bool incomingWasInstalledBeforeCollapse = false;
            fixture.Assembly.ActionCompleted += action =>
            {
                actions.Add(action);
                if (action.Action == AssemblyActionKind.PartInstalled)
                {
                    incomingWasInstalledBeforeCollapse = fixture.Incoming.IsInstalled &&
                        fixture.TargetRuntime.InstalledPart == fixture.Incoming &&
                        fixture.Support.IsInstalled;
                    Assert.That(Vector3.Distance(fixture.Incoming.Body.position, fixture.Target.Pose.position),
                        Is.LessThan(0.0001f), "Installation must reach the actual mount before collapse.");
                }
            };

            AssemblyOperationResult result = entryPoint switch
            {
                "handoff" => fixture.Assembly.TryInstallFromHandoff(fixture.Pickup, fixture.Target),
                "begin" => fixture.Assembly.BeginInstallFromHandoff(fixture.Pickup, fixture.Target),
                _ => fixture.Assembly.TryInstall(fixture.Incoming, fixture.Target),
            };

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(result.Operation, Is.EqualTo(AssemblyOperation.Install));
            Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.None));
            Assert.That(incomingWasInstalledBeforeCollapse, Is.True);
            AssertInstalledThenCollapsed(actions, fixture.Incoming, fixture.Support, fixture.Incoming);
            Assert.That(fixture.Support.IsInstalled, Is.False);
            Assert.That(fixture.SupportRuntime.IsOccupied, Is.False);
            Assert.That(fixture.Opposite.IsInstalled, Is.True);
            Assert.That(fixture.Incoming.IsInstalled, Is.False);
            Assert.That(fixture.TargetRuntime.IsOccupied, Is.False);
            Assert.That(fixture.Body.IsInstalled, Is.True);
            Assert.That(Vector3.Distance(fixture.Incoming.Body.position, fixture.Target.Pose.position),
                Is.LessThan(0.0001f), "Collapsed incoming part remains at the completed mount pose, not its carry origin.");
            Assert.That(fixture.Incoming.Body.isKinematic, Is.False);
            Assert.That(fixture.Incoming.Body.useGravity, Is.True);
            Assert.That(fixture.Incoming.Body.detectCollisions, Is.True);
            Assert.That(fixture.Pickup.IsCarried, Is.False);

            Assert.That(fixture.Assembly.TryInstall(fixture.Incoming, fixture.Target).Succeeded, Is.False);
            Assert.That(actions.Count, Is.EqualTo(3), "An empty support must not emit a second installation or collapse.");
        }

        [TestCase("fl")]
        [TestCase("fr")]
        public void BoltedSupportAllowsExactlyOneNormalInstall(string corner)
        {
            using var fixture = new AttemptFixture(corner);
            fixture.SetSupportTightness(2);
            var actions = new List<AssemblyActionCompleted>();
            fixture.Assembly.ActionCompleted += actions.Add;

            Assert.That(fixture.Assembly.TryInstall(fixture.Incoming, fixture.Target).Succeeded, Is.True);
            Assert.That(fixture.Support.IsInstalled, Is.True);
            Assert.That(fixture.Opposite.IsInstalled, Is.True);
            Assert.That(fixture.TargetRuntime.InstalledPart, Is.SameAs(fixture.Incoming));
            Assert.That(fixture.Incoming.IsInstalled, Is.True);
            Assert.That(actions.Count, Is.EqualTo(1));
            Assert.That(actions[0].Action, Is.EqualTo(AssemblyActionKind.PartInstalled));
            Assert.That(actions[0].Part, Is.SameAs(fixture.Incoming));
        }

        [TestCase("fl")]
        [TestCase("fr")]
        public void InstallationOnUnboltedSupportCollapsesLinkedDescendantsButNotUnrelatedBodyBranch(string corner)
        {
            using var fixture = new AttemptFixture(corner, includeInstalledDependent: true);
            MountPointRuntime dependentMount = fixture.Assembly.Graph.Mounts.Single(mount =>
                mount.InstalledPart == fixture.Dependent);
            Assert.That(dependentMount.Definition.RequiredOccupiedMountIds,
                Does.Contain(fixture.SupportMount.MountId));
            MountPointRuntime descendantMount = fixture.Assembly.Graph.Mounts.Single(mount =>
                mount.InstalledPart == fixture.Descendant);
            Assert.That(descendantMount.Definition.RequiredOccupiedMountIds,
                Does.Contain(dependentMount.MountId));
            var actions = new List<AssemblyActionCompleted>();
            fixture.Assembly.ActionCompleted += actions.Add;

            AssemblyOperationResult result = fixture.Assembly.TryInstall(fixture.Incoming, fixture.Target);

            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(fixture.Support.IsInstalled, Is.False);
            Assert.That(fixture.Dependent.IsInstalled, Is.False);
            Assert.That(fixture.Descendant.IsInstalled, Is.False);
            Assert.That(dependentMount.IsOccupied, Is.False);
            Assert.That(descendantMount.IsOccupied, Is.False);
            Assert.That(fixture.Opposite.IsInstalled, Is.True);
            Assert.That(fixture.Body.IsInstalled, Is.True);
            Assert.That(fixture.Incoming.IsInstalled, Is.False);
            Assert.That(fixture.TargetRuntime.IsOccupied, Is.False);
            AssertInstalledThenCollapsed(actions, fixture.Incoming,
                fixture.Support, fixture.Incoming, fixture.Dependent, fixture.Descendant);
        }

        [TestCase("fl")]
        [TestCase("fr")]
        public void UnconfiguredMountRetainsOrdinaryInstallBehavior(string corner)
        {
            using var fixture = new AttemptFixture(corner, configureAttemptPolicy: false);
            Assert.That(fixture.Target.Definition.InstallAttemptBoltedSupportMountId, Is.Empty);
            Assert.That(fixture.Target.Definition.InstallationBlockedWhileBoltedMountIds, Is.Empty);
            Assert.That(fixture.Assembly.TryInstall(fixture.Incoming, fixture.Target).Succeeded, Is.True);
            Assert.That(fixture.Support.IsInstalled, Is.True);
            Assert.That(fixture.SupportRuntime.FastenerGroup.IsBolted, Is.False);
        }

        [TestCase("fl", false)]
        [TestCase("fr", false)]
        [TestCase("fl", true)]
        [TestCase("fr", true)]
        public void RealCarryTransfersIncomingThenCollapsesAssemblyAndPublishesOneHandoff(
            string corner, bool useSurfaceTarget)
        {
            using var fixture = new AttemptFixture(corner);
            var player = new GameObject("Front attempted-install carrier");
            try
            {
                player.transform.position = fixture.Target.Pose.position - Vector3.forward;
                var anchor = new GameObject("Carry anchor");
                anchor.transform.SetParent(player.transform, false);
                anchor.transform.localPosition = Vector3.forward;
                PhysicalCarryController carry = player.AddComponent<PhysicalCarryController>();
                carry.Configure(anchor.transform, player.AddComponent<BoxCollider>());
                var context = new InteractionContext(player, player.transform.position, Vector3.forward);
                Assert.That(carry.TryPickup(fixture.Pickup, context), Is.True);
                var notifications = new List<InteractionActionCompleted>();
                carry.ActionCompleted += notifications.Add;
                var actions = new List<AssemblyActionCompleted>();
                bool installedAfterCarryRelease = false;
                fixture.Assembly.ActionCompleted += action =>
                {
                    actions.Add(action);
                    if (action.Action == AssemblyActionKind.PartInstalled)
                        installedAfterCarryRelease = !carry.HasHeldObject && !fixture.Pickup.IsCarried &&
                            fixture.TargetRuntime.InstalledPart == fixture.Incoming;
                };
                IMountHandoffTarget target;
                if (useSurfaceTarget)
                {
                    PartInstance owner = fixture.Assembly.Parts.Single(part => part.IsAssemblyRoot);
                    var surface = owner.gameObject.AddComponent<AssemblySurfaceMountHandoffTarget>();
                    surface.Configure(fixture.Assembly, owner);
                    target = surface;
                }
                else
                {
                    var marker = fixture.Target.gameObject.AddComponent<AssemblyMountHandoffTarget>();
                    marker.Configure(fixture.Assembly, fixture.Target);
                    target = marker;
                }

                Assert.That(target.CanAccept(fixture.Pickup, context), Is.True,
                    "An unbolted but present support still permits installation preview.");
                Assert.That(carry.TryHandoff(target, context), Is.True,
                    "The item is transferred, installed, and only then collapses with its support.");
                Assert.That(fixture.Support.IsInstalled, Is.False);
                Assert.That(carry.HasHeldObject, Is.False);
                Assert.That(carry.HeldTarget, Is.Null);
                Assert.That(carry.HeldBody, Is.Null);
                Assert.That(fixture.Pickup.IsCarried, Is.False);
                Assert.That(installedAfterCarryRelease, Is.True);
                Assert.That(fixture.Incoming.IsInstalled, Is.False);
                Assert.That(fixture.TargetRuntime.IsOccupied, Is.False);
                Assert.That(fixture.Opposite.IsInstalled, Is.True);
                Assert.That(fixture.Body.IsInstalled, Is.True);
                Assert.That(fixture.Incoming.Body.isKinematic, Is.False);
                Assert.That(fixture.Incoming.Body.useGravity, Is.True);
                Assert.That(notifications.Count, Is.EqualTo(1));
                Assert.That(notifications[0].Action, Is.EqualTo(InteractionActionKind.MountHandoff),
                    "A completed transfer emits one handoff, not a fallback manual Drop.");
                AssertInstalledThenCollapsed(actions, fixture.Incoming, fixture.Support, fixture.Incoming);
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [TestCase("fl")]
        [TestCase("fr")]
        public void RealCarryUnconfiguredMountStillReleasesAndPublishesSuccessfulHandoff(string corner)
        {
            using var fixture = new AttemptFixture(corner, configureAttemptPolicy: false);
            var player = new GameObject("Compatibility handoff carrier");
            try
            {
                PhysicalCarryController carry = player.AddComponent<PhysicalCarryController>();
                carry.Configure(player.transform, player.AddComponent<BoxCollider>());
                var context = new InteractionContext(player, fixture.Target.Pose.position - Vector3.forward,
                    Vector3.forward);
                Assert.That(carry.TryPickup(fixture.Pickup, context), Is.True);
                var notifications = new List<InteractionActionCompleted>();
                carry.ActionCompleted += notifications.Add;
                var marker = fixture.Target.gameObject.AddComponent<AssemblyMountHandoffTarget>();
                marker.Configure(fixture.Assembly, fixture.Target);

                Assert.That(carry.TryHandoff(marker, context), Is.True);
                Assert.That(carry.HasHeldObject, Is.False);
                Assert.That(fixture.Incoming.IsInstalled, Is.True);
                Assert.That(fixture.Support.IsInstalled, Is.True);
                Assert.That(notifications.Count, Is.EqualTo(1));
                Assert.That(notifications[0].Action, Is.EqualTo(InteractionActionKind.MountHandoff));
            }
            finally
            {
                Object.DestroyImmediate(player);
            }
        }

        [TestCase("fl", false, false)]
        [TestCase("fr", false, false)]
        [TestCase("fl", true, false)]
        [TestCase("fr", true, false)]
        [TestCase("fl", true, true)]
        [TestCase("fr", true, true)]
        public void HalfshaftStyleGateRequiresInstalledUnboltedDiscWithoutDropSideEffects(
            string corner, bool discInstalled, bool discBolted)
        {
            using var fixture = new AttemptFixture(corner, discInstalled,
                configureAttemptPolicy: false, blockWhileSupportBolted: true);
            if (discBolted) fixture.SetSupportTightness(2);
            var actions = new List<AssemblyActionCompleted>();
            fixture.Assembly.ActionCompleted += actions.Add;
            bool expected = discInstalled && !discBolted;

            Assert.That(fixture.Assembly.EvaluateInstall(fixture.Incoming, fixture.Target).Succeeded,
                Is.EqualTo(expected));
            Assert.That(fixture.Assembly.EvaluateHandoffInstall(fixture.Incoming, fixture.Target).Succeeded,
                Is.EqualTo(expected));
            AssemblyOperationResult result = fixture.Assembly.TryInstall(fixture.Incoming, fixture.Target);
            Assert.That(result.Succeeded, Is.EqualTo(expected));
            Assert.That(fixture.Support.IsInstalled, Is.EqualTo(discInstalled));
            Assert.That(fixture.Incoming.IsInstalled, Is.EqualTo(expected));
            Assert.That(actions.Any(action => action.Action == AssemblyActionKind.PartBrokenLoose), Is.False);
            Assert.That(actions.Count, Is.EqualTo(expected ? 1 : 0));
        }

        [TestCase("wishbone-fl", "sub-frame")]
        [TestCase("wishbone-fr", "sub-frame")]
        [TestCase("steering-rack", "sub-frame")]
        [TestCase("spindle-fl", "wishbone-fl")]
        [TestCase("spindle-fr", "wishbone-fr")]
        [TestCase("strut-fl", "spindle-fl")]
        [TestCase("strut-fr", "spindle-fr")]
        [TestCase("discbrake-fl", "spindle-fl")]
        [TestCase("discbrake-fr", "spindle-fr")]
        [TestCase("steering-rod-fl", "steering-rack")]
        [TestCase("steering-rod-fr", "steering-rack")]
        [TestCase("steering-column", "steering-rack")]
        public void GeneratedFrontAttemptChecksUseExactSupportAndKeepPreviewPresenceOnly(
            string targetSuffix, string supportSuffix)
        {
            MountPointDefinition definition = GeneratedMount(targetSuffix).Definition;
            string supportId = "mount.satsuma." + supportSuffix;
            Assert.That(definition.InstallAttemptBoltedSupportMountId, Is.EqualTo(supportId));
            Assert.That(definition.RequiredOccupiedMountIds, Does.Contain(supportId));
            Assert.That(definition.RequiredBoltedMountIds, Does.Not.Contain(supportId),
                "A one-shot Check bolts must not become a preview or continuous retention gate.");
            Assert.That(definition.InstallationBlockedWhileBoltedMountIds, Is.Empty);
        }

        [TestCase("fl")]
        [TestCase("fr")]
        public void GeneratedHalfshaftUsesDiscPresenceAndInverseBoltedGate(string corner)
        {
            MountPointDefinition definition = GeneratedMount("halfshaft-" + corner).Definition;
            string discId = "mount.satsuma.discbrake-" + corner;
            Assert.That(definition.RequiredOccupiedMountIds, Does.Contain(discId));
            Assert.That(definition.InstallAttemptBoltedSupportMountId, Is.Empty);
            Assert.That(definition.InstallationBlockedWhileBoltedMountIds, Is.EqualTo(new[] { discId }));
            Assert.That(definition.RequiredBoltedMountIds, Does.Not.Contain(discId));
        }

        [TestCase("fl")]
        [TestCase("fr")]
        public void GeneratedFrontDoesNotReintroduceRemovedOrderingOrEarlyBoltedDependencies(string corner)
        {
            VehicleAssemblyController assembly = GeneratedAssembly();
            MountPointDefinition rod = GeneratedMount("steering-rod-" + corner).Definition;
            MountPointDefinition disc = GeneratedMount("discbrake-" + corner).Definition;
            Assert.That(rod.RequiredOccupiedMountIds, Does.Not.Contain("mount.satsuma.spindle-" + corner));
            Assert.That(disc.RequiredOccupiedMountIds, Does.Not.Contain("mount.satsuma.strut-" + corner));
            string prefix = "vehicle.satsuma.part.";
            Assert.That(assembly.Dependencies.Any(dependency =>
                dependency.DependentPartDefinitionId == prefix + "steering-rod-" + corner &&
                dependency.RelatedPartDefinitionId == prefix + "spindle-" + corner &&
                dependency.Kind == AssemblyDependencyKind.InstallRequiresInstalled), Is.False);
            foreach (string part in new[] { "spindle-", "strut-" })
                Assert.That(assembly.Dependencies.Any(dependency =>
                    dependency.DependentPartDefinitionId == prefix + part + corner &&
                    dependency.Kind == AssemblyDependencyKind.InstallRequiresBolted), Is.False);
        }

        [TestCase("sub-frame", 32, 26)]
        [TestCase("steering-rack", 32, 24)]
        [TestCase("steering-column", 16, 10)]
        [TestCase("wishbone-fl", 16, 2)]
        [TestCase("wishbone-fr", 16, 2)]
        [TestCase("spindle-fl", 8, 2)]
        [TestCase("spindle-fr", 8, 2)]
        [TestCase("strut-fl", 56, 3)]
        [TestCase("strut-fr", 56, 3)]
        [TestCase("steering-rod-fl", 8, 8)]
        [TestCase("steering-rod-fr", 8, 8)]
        [TestCase("discbrake-fl", 8, 2)]
        [TestCase("discbrake-fr", 8, 2)]
        [TestCase("halfshaft-fl", 24, 2)]
        [TestCase("halfshaft-fr", 24, 2)]
        [TestCase("wheelfl-new", 32, 1)]
        [TestCase("wheelfr-new", 32, 1)]
        public void GeneratedFrontThresholdsRetainDonorHysteresis(
            string mountSuffix, int maximum, int boltedOn)
        {
            MountPointDefinition definition = GeneratedMount(mountSuffix).Definition;
            FastenerGroupDefinition groupDefinition = definition.FastenerGroup;
            Assert.That(groupDefinition.AggregateMaximumTightness, Is.EqualTo(maximum));
            Assert.That(groupDefinition.BoltedOnThreshold, Is.EqualTo(boltedOn));
            Assert.That(groupDefinition.BoltedOffThreshold, Is.Zero);
            var fasteners = definition.Fasteners.Select(value => new FastenerInstance(value)).ToArray();
            var group = new FastenerGroupState(groupDefinition, fasteners);

            SetAggregate(fasteners, group, boltedOn - 1);
            Assert.That(group.IsBolted, Is.False, "The cold latch must remain OFF at ON-1.");
            SetAggregate(fasteners, group, boltedOn);
            Assert.That(group.IsBolted, Is.True, "The latch must engage at ON, not full tightness.");
            if (boltedOn > 1)
            {
                SetAggregate(fasteners, group, boltedOn - 1);
                Assert.That(group.IsBolted, Is.True, "Partial loosening must retain the ON latch.");
            }
            SetAggregate(fasteners, group, 0);
            Assert.That(group.IsBolted, Is.False, "Zero tightness must release the latch.");
            SetAggregate(fasteners, group, boltedOn - 1);
            Assert.That(group.IsBolted, Is.False, "After OFF, ON-1 must not recreate the latch.");
        }

        private static VehicleAssemblyController GeneratedAssembly()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null, "Regenerate the private Satsuma baseline before this contract suite.");
            return prefab.GetComponent<VehicleAssemblyController>();
        }

        private static MountPointAuthoring GeneratedMount(string suffix) =>
            GeneratedAssembly().MountPoints.Single(value => value.MountId == "mount.satsuma." + suffix);

        private static void SetAggregate(FastenerInstance[] fasteners, FastenerGroupState group, int total)
        {
            int remaining = total;
            foreach (FastenerInstance fastener in fasteners)
            {
                int stage = Mathf.Min(remaining, fastener.Definition.MaximumStage);
                Assert.That(fastener.TryRestore(true, true, stage), Is.True);
                remaining -= stage;
            }
            Assert.That(remaining, Is.Zero);
            group.Reevaluate(true);
            Assert.That(group.Tightness, Is.EqualTo(total));
        }

        private static void AssertInstalledThenCollapsed(List<AssemblyActionCompleted> actions,
            PartInstance incoming, params PartInstance[] collapsedParts)
        {
            Assert.That(actions.Count, Is.EqualTo(1 + collapsedParts.Length));
            Assert.That(actions[0].Action, Is.EqualTo(AssemblyActionKind.PartInstalled));
            Assert.That(actions[0].Part, Is.SameAs(incoming));
            Assert.That(actions.Skip(1).All(action => action.Action == AssemblyActionKind.PartBrokenLoose),
                Is.True, "All detach notifications must follow the successful incoming installation.");
            Assert.That(actions.Skip(1).Select(action => action.Part), Is.EquivalentTo(collapsedParts));
            foreach (PartInstance part in collapsedParts)
                Assert.That(actions.Count(action => action.Action == AssemblyActionKind.PartBrokenLoose &&
                    action.Part == part), Is.EqualTo(1), part.name + " must break loose once only.");
        }

        private sealed class AttemptFixture : IDisposable
        {
            private readonly List<ScriptableObject> definitions = new();
            private readonly GameObject root;

            public AttemptFixture(string corner, bool supportInstalled = true,
                bool configureAttemptPolicy = true, bool blockWhileSupportBolted = false,
                bool includeInstalledDependent = false)
            {
                root = new GameObject("Front install attempt fixture " + corner);
                root.transform.position = new Vector3(10f, 50f, 20f);
                Body = CreatePart(root, "test.body", true, string.Empty);
                Support = CreateChildPart("test.support." + corner,
                    supportInstalled ? "test.mount.support." + corner : string.Empty);
                Opposite = CreateChildPart("test.opposite", "test.mount.opposite");
                Incoming = CreateChildPart("test.incoming." + corner, string.Empty);
                Pickup = Incoming.GetComponent<PhysicsPickupTarget>();
                SupportMount = CreateMount("test.mount.support." + corner, Support);
                MountPointAuthoring oppositeMount = CreateMount("test.mount.opposite", Opposite);
                Target = CreateMount("test.mount.incoming." + corner, Incoming);
                Target.Definition.ConfigureSequence(new[] { SupportMount.MountId },
                    Array.Empty<string>(), Array.Empty<string>());
                if (configureAttemptPolicy || blockWhileSupportBolted)
                    Target.Definition.ConfigureInstallationChecks(
                        configureAttemptPolicy ? SupportMount.MountId : string.Empty,
                        blockWhileSupportBolted ? new[] { SupportMount.MountId } : Array.Empty<string>());
                var parts = new List<PartInstance> { Body, Support, Opposite, Incoming };
                var mounts = new List<MountPointAuthoring> { SupportMount, oppositeMount, Target };
                if (includeInstalledDependent)
                {
                    Dependent = CreateChildPart("test.dependent." + corner, "test.mount.dependent." + corner);
                    MountPointAuthoring dependentMount = CreateMount("test.mount.dependent." + corner, Dependent);
                    dependentMount.Definition.ConfigureSequence(new[] { SupportMount.MountId },
                        Array.Empty<string>(), Array.Empty<string>());
                    parts.Add(Dependent);
                    mounts.Add(dependentMount);
                    Descendant = CreateChildPart("test.descendant." + corner, "test.mount.descendant." + corner);
                    MountPointAuthoring descendantMount = CreateMount("test.mount.descendant." + corner, Descendant);
                    descendantMount.Definition.ConfigureSequence(new[] { dependentMount.MountId },
                        Array.Empty<string>(), Array.Empty<string>());
                    parts.Add(Descendant);
                    mounts.Add(descendantMount);
                }
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(parts.ToArray(), mounts.ToArray(), Array.Empty<AssemblyDependency>(),
                    Array.Empty<ToolDefinition>(), root.transform);
                SupportRuntime = Assembly.ResolveMount(SupportMount);
                TargetRuntime = Assembly.ResolveMount(Target);
                PlaceIncoming(Target.Pose.position);
            }

            public VehicleAssemblyController Assembly { get; }
            public PartInstance Body { get; }
            public PartInstance Support { get; }
            public PartInstance Opposite { get; }
            public PartInstance Incoming { get; }
            public PartInstance Dependent { get; }
            public PartInstance Descendant { get; }
            public PhysicsPickupTarget Pickup { get; }
            public MountPointAuthoring SupportMount { get; }
            public MountPointAuthoring Target { get; }
            public MountPointRuntime SupportRuntime { get; }
            public MountPointRuntime TargetRuntime { get; }

            public void SetSupportTightness(int value) =>
                SetAggregate(SupportRuntime.Fasteners.ToArray(), SupportRuntime.FastenerGroup, value);

            public void PlaceIncoming(Vector3 position)
            {
                Incoming.transform.SetPositionAndRotation(position, Target.Pose.rotation);
                Incoming.Body.position = position;
                Incoming.Body.rotation = Target.Pose.rotation;
                Physics.SyncTransforms();
            }

            private PartInstance CreateChildPart(string id, string initialMount)
            {
                var owner = new GameObject(id);
                owner.transform.SetParent(root.transform, false);
                return CreatePart(owner, id, false, initialMount);
            }

            private PartInstance CreatePart(GameObject owner, string id, bool isRoot, string initialMount)
            {
                PartDefinition definition = NewDefinition<PartDefinition>();
                definition.Configure(id, id, PartCategory.Suspension, 1f, null,
                    new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                StableEntityIdAuthoring identity = owner.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                Rigidbody body = owner.AddComponent<Rigidbody>();
                body.useGravity = true;
                PhysicsPickupTarget pickup = owner.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(body, identity, "Test part", 35f, useGravityWhenLoose: true);
                PartInstance instance = owner.AddComponent<PartInstance>();
                instance.Configure(definition, identity, body, pickup, isRoot, initialMount);
                return instance;
            }

            private MountPointAuthoring CreateMount(string id, PartInstance acceptedPart)
            {
                FastenerDefinition fastener = NewDefinition<FastenerDefinition>();
                fastener.Configure(id + ".bolt", "Test bolt", FastenerSize.Millimeter8, 8,
                    FastenerDirection.ClockwiseToTighten, true, true,
                    ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter8));
                MountPointDefinition definition = NewDefinition<MountPointDefinition>();
                definition.Configure(id, id, "test.socket", "test.body",
                    new[] { acceptedPart.Definition.DefinitionId },
                    new MountConstraint(0.1f, 30f, 1f, 0f), 0f, new[] { fastener });
                var group = new FastenerGroupDefinition();
                group.Configure(new[] { fastener.DefinitionId }, 8, 2, 0);
                definition.ConfigureFastenerGroup(group);
                var owner = new GameObject(id);
                owner.transform.SetParent(root.transform, false);
                MountPointAuthoring authoring = owner.AddComponent<MountPointAuthoring>();
                authoring.Configure(definition, id, owner.transform, 0);
                return authoring;
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
                foreach (ScriptableObject definition in definitions) Object.DestroyImmediate(definition);
            }
        }
    }
}
