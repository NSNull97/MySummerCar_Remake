using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaCockpitSteeringRulesTests
    {
        private const string WheelMountId = "mount.satsuma.steering-wheel";
        private const string ColumnMountId = "mount.satsuma.steering-column";
        private const string WheelFastenerId =
            "fastener.satsuma.steering-wheel.boltpm-1";
        private const string ColumnPartId = "vehicle.satsuma.part.steering-column";
        private const string DashboardMountId = "mount.satsuma.dashboard";
        private const string DashboardPartId = "vehicle.satsuma.part.dashboard";
        private const string GtWheelPartId =
            "vehicle.satsuma.part.gt-gt-steering-wheel";
        private const string StockWheelPartId =
            "vehicle.satsuma.part.stock-steering-wheel";

        private static readonly string[] WheelPartIds =
        {
            GtWheelPartId,
            StockWheelPartId,
        };

        private readonly List<Object> transientObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = transientObjects.Count - 1; index >= 0; index--)
            {
                if (transientObjects[index] != null)
                {
                    Object.DestroyImmediate(transientObjects[index]);
                }
            }

            transientObjects.Clear();
        }

        [Test]
        public void GeneratedWheelOwnsTwoVariantsDonorLatchAndInstallOnlyColumnGate()
        {
            GameObject prefab = GeneratedPrefab();
            VehicleAssemblyController assembly =
                prefab.GetComponent<VehicleAssemblyController>();
            MountPointDefinition definition = assembly.MountPoints.Single(value =>
                value.MountId == WheelMountId).Definition;

            Assert.That(definition.DefinitionId, Is.EqualTo(WheelMountId));
            Assert.That(definition.OwnerPartDefinitionId,
                Is.EqualTo("vehicle.satsuma.part.body-shell"));
            Assert.That(definition.AcceptedPartDefinitionIds, Is.EqualTo(WheelPartIds));
            Assert.That(definition.Fasteners, Has.Length.EqualTo(1));
            FastenerDefinition fastener = definition.Fasteners.Single();
            Assert.That(fastener.DefinitionId, Is.EqualTo(WheelFastenerId));
            Assert.That(fastener.Size, Is.EqualTo(FastenerSize.Millimeter10));
            Assert.That(fastener.MaximumStage, Is.EqualTo(8));
            Assert.That(fastener.InsertedOnInstall, Is.True);
            Assert.That(fastener.RequiredForRemoval, Is.True);
            Assert.That(fastener.ToolRule.ToolType, Is.EqualTo("Wrench"));
            Assert.That(fastener.ToolRule.FastenerSize,
                Is.EqualTo(FastenerSize.Millimeter10));

            FastenerGroupDefinition group = definition.FastenerGroup;
            Assert.That(group.FastenerDefinitionIds,
                Is.EqualTo(new[] { WheelFastenerId }));
            Assert.That(group.AggregateMaximumTightness, Is.EqualTo(8));
            Assert.That(group.BoltedOnThreshold, Is.EqualTo(2));
            Assert.That(group.BoltedOffThreshold, Is.Zero);
            Assert.That(group.SpeedRetentionPolicy,
                Is.EqualTo(FastenerSpeedRetentionPolicy.None));
            Assert.That(group.BreakAction, Is.EqualTo(FastenerBreakAction.None));

            Assert.That(definition.InstallationRequiredOccupiedMountIds,
                Is.EqualTo(new[] { ColumnMountId }));
            AssertLegacyAssemblyRulesStayEmpty(definition);

            IReadOnlyList<VehicleAssemblyValidationIssue> issues =
                VehicleAssemblyValidator.Validate(
                    assembly.Parts,
                    assembly.MountPoints,
                    assembly.Dependencies,
                    assembly.Tools);
            Assert.That(issues.Any(issue =>
                    issue.Code == "MOUNT-INSTALLATION-REQUIRES-INSTALLED" &&
                    issue.Message.Contains(WheelMountId, StringComparison.Ordinal)),
                Is.False, "The canonical column reference must resolve in authoring.");
        }

        [Test]
        public void InstallationOnlyPrerequisitesHaveAnEmptyNullSafeDefault()
        {
            MountPointDefinition definition = Track(
                ScriptableObject.CreateInstance<MountPointDefinition>());
            Assert.That(definition.InstallationRequiredOccupiedMountIds, Is.Empty);
            definition.ConfigureInstallationOccupancy(null);
            Assert.That(definition.InstallationRequiredOccupiedMountIds, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InstallationOnlyReferenceRejectsUnknownOrSelfAuthoring(bool self)
        {
            using var fixture = new Fixture();
            MountPointDefinition clone = Track(Object.Instantiate(
                fixture.WheelMount.Definition));
            clone.ConfigureInstallationOccupancy(new[]
            {
                self ? WheelMountId : "mount.satsuma.missing-column",
            });
            Transform pose = fixture.WheelMount.Pose;
            fixture.WheelMount.Configure(clone, WheelMountId, pose, ~0);

            IReadOnlyList<VehicleAssemblyValidationIssue> issues =
                VehicleAssemblyValidator.Validate(
                    fixture.Assembly.Parts,
                    fixture.Assembly.MountPoints,
                    fixture.Assembly.Dependencies,
                    fixture.Assembly.Tools);
            Assert.That(issues.Any(issue =>
                    issue.Code == "MOUNT-INSTALLATION-REQUIRES-INSTALLED" &&
                    issue.Severity == VehicleAssemblyValidationSeverity.Error),
                Is.True);

            PartInstance wheel = fixture.Part(StockWheelPartId);
            fixture.PlaceAtWheel(wheel);
            Assert.That(fixture.Assembly.EvaluateInstall(wheel, fixture.WheelMount)
                    .FailureReason,
                Is.EqualTo(AssemblyFailureReason.MissingPrerequisite),
                "Invalid install-only references must fail closed at runtime.");
        }

        [Test]
        public void InstallationOnlyReferenceRejectsDuplicateAuthoring()
        {
            using var fixture = new Fixture();
            MountPointDefinition clone = Track(Object.Instantiate(
                fixture.WheelMount.Definition));
            clone.ConfigureInstallationOccupancy(new[]
            {
                ColumnMountId,
                ColumnMountId,
            });
            fixture.WheelMount.Configure(
                clone,
                WheelMountId,
                fixture.WheelMount.Pose,
                ~0);

            IReadOnlyList<VehicleAssemblyValidationIssue> issues =
                VehicleAssemblyValidator.Validate(
                    fixture.Assembly.Parts,
                    fixture.Assembly.MountPoints,
                    fixture.Assembly.Dependencies,
                    fixture.Assembly.Tools);
            Assert.That(issues.Any(issue =>
                    issue.Code == "MOUNT-INSTALLATION-REQUIRES-INSTALLED" &&
                    issue.Severity == VehicleAssemblyValidationSeverity.Error),
                Is.True,
                "Duplicate install-only references must fail authoring validation.");
        }

        [Test]
        public void LegacyHelperChangesOnlyLatchAndInstallOnlyArrayThenIsIdempotent()
        {
            MountPointDefinition definition = CreateWheelDefinition(
                onThreshold: 1,
                installationRequired: Array.Empty<string>());
            string before = EditorJsonUtility.ToJson(definition);
            string expected = before
                .Replace("\"boltedOnThreshold\":1", "\"boltedOnThreshold\":2")
                .Replace(
                    "\"installationRequiredOccupiedMountIds\":[]",
                    "\"installationRequiredOccupiedMountIds\":[\"" +
                    ColumnMountId + "\"]");
            Assert.That(expected, Is.Not.EqualTo(before));
            Assert.That(expected.Contains("\"boltedOnThreshold\":2",
                StringComparison.Ordinal), Is.True);
            Assert.That(expected.Contains(ColumnMountId, StringComparison.Ordinal), Is.True);

            Assert.That(Phase1SatsumaCockpitRules.ApplySteeringWheelRules(definition),
                Is.True);
            Assert.That(EditorJsonUtility.ToJson(definition), Is.EqualTo(expected),
                "C1b may change only ON1->2 and the install-only column array.");

            string migrated = EditorJsonUtility.ToJson(definition);
            Assert.That(Phase1SatsumaCockpitRules.ApplySteeringWheelRules(definition),
                Is.False);
            Assert.That(EditorJsonUtility.ToJson(definition), Is.EqualTo(migrated));
        }

        [TestCase("definition-id")]
        [TestCase("owner")]
        [TestCase("accepted-parts")]
        [TestCase("fastener-count")]
        [TestCase("fastener-id")]
        [TestCase("fastener-size")]
        [TestCase("fastener-maximum")]
        [TestCase("fastener-inserted")]
        [TestCase("fastener-required")]
        [TestCase("fastener-tool-type")]
        [TestCase("fastener-tool-size")]
        [TestCase("group-ids")]
        [TestCase("group-maximum")]
        [TestCase("group-on")]
        [TestCase("group-off")]
        [TestCase("retention")]
        [TestCase("break-action")]
        [TestCase("group-loose-negative")]
        [TestCase("group-partial-order")]
        [TestCase("group-chance-zero")]
        [TestCase("install-only")]
        [TestCase("required")]
        [TestCase("required-any")]
        [TestCase("blocked")]
        [TestCase("removal-blocked")]
        [TestCase("removal-bolted")]
        [TestCase("removal-ignored")]
        [TestCase("required-bolted")]
        [TestCase("required-any-bolted")]
        [TestCase("installation-bolted")]
        [TestCase("installation-support")]
        public void HelperRejectsDriftBeforeMutatingDefinition(string drift)
        {
            MountPointDefinition definition = CreateWheelDefinition(
                onThreshold: 1,
                installationRequired: Array.Empty<string>());
            ApplyDrift(definition, drift);
            string before = EditorJsonUtility.ToJson(definition);

            Assert.Throws<InvalidDataException>(() =>
                Phase1SatsumaCockpitRules.ApplySteeringWheelRules(definition));
            Assert.That(EditorJsonUtility.ToJson(definition), Is.EqualTo(before),
                drift + " must fail before either C1b field is changed.");
        }

        [Test]
        public void WheelLatchUsesOnTwoOffZeroHysteresis()
        {
            MountPointDefinition definition = CreateWheelDefinition(
                onThreshold: 2,
                installationRequired: new[] { ColumnMountId });
            var fastener = new FastenerInstance(definition.Fasteners.Single());
            var group = new FastenerGroupState(
                definition.FastenerGroup,
                new[] { fastener });

            SetStage(fastener, group, 1);
            Assert.That(group.IsBolted, Is.False);
            SetStage(fastener, group, 2);
            Assert.That(group.IsBolted, Is.True);
            SetStage(fastener, group, 1);
            Assert.That(group.IsBolted, Is.True,
                "Historical latch remains set above OFF0.");
            SetStage(fastener, group, 0);
            Assert.That(group.IsBolted, Is.False);
            SetStage(fastener, group, 1);
            Assert.That(group.IsBolted, Is.False,
                "After OFF0 a fresh T1 state remains below ON2.");
        }

        [TestCase(GtWheelPartId)]
        [TestCase(StockWheelPartId)]
        public void WheelInstallRequiresInstalledButNotBoltedColumn(string wheelPartId)
        {
            using var fixture = new Fixture();
            PartInstance wheel = fixture.Part(wheelPartId);
            fixture.PlaceAtWheel(wheel);

            Assert.That(fixture.Assembly.FindBestMount(wheel).IsValid, Is.False);
            AssertMissingColumn(fixture.Assembly.EvaluateInstall(
                wheel, fixture.WheelMount));
            AssertMissingColumn(fixture.Assembly.TryInstall(
                wheel, fixture.WheelMount));
            Assert.That(wheel.IsInstalled, Is.False);

            fixture.Restore(fixture.Snapshot(
                columnInstalled: true,
                wheelPartId: null,
                wheelTightness: 0,
                wheelBolted: false));
            Assert.That(fixture.ColumnRuntime.IsOccupied, Is.True);
            Assert.That(fixture.ColumnRuntime.FastenerGroup.IsBolted, Is.False,
                "Column presence, not column fastening, opens the wheel trigger.");
            Assert.That(fixture.ColumnRuntime.FastenerGroup.Tightness, Is.Zero);

            fixture.PlaceAtWheel(wheel);
            Assert.That(fixture.Assembly.FindBestMount(wheel).IsValid, Is.True);
            Assert.That(fixture.Assembly.EvaluateInstall(wheel, fixture.WheelMount)
                .Succeeded, Is.True);
            Assert.That(fixture.Assembly.TryInstall(wheel, fixture.WheelMount)
                .Succeeded, Is.True);
            Assert.That(wheel.IsInstalled, Is.True);
        }

        [Test]
        public void SurfaceHandoffRoutesThroughInstalledColumnOnly()
        {
            using var fixture = new Fixture();
            PartInstance wheel = fixture.Part(StockWheelPartId);
            PartInstance column = fixture.Part(ColumnPartId);
            PartInstance dashboard = fixture.Part(DashboardPartId);
            PartInstance body = fixture.Assembly.Parts.Single(value =>
                value.IsAssemblyRoot);
            AssemblySurfaceMountHandoffTarget columnSurface = column
                .GetComponent<AssemblySurfaceMountHandoffTarget>();
            AssemblySurfaceMountHandoffTarget dashboardSurface = dashboard
                .GetComponent<AssemblySurfaceMountHandoffTarget>();
            AssemblySurfaceMountHandoffTarget bodySurface = body
                .GetComponent<AssemblySurfaceMountHandoffTarget>();
            Assert.That(columnSurface, Is.Not.Null);
            Assert.That(dashboardSurface, Is.Not.Null);
            Assert.That(bodySurface, Is.Not.Null);
            Assert.That(fixture.WheelMount.Definition.OwnerPartDefinitionId,
                Is.EqualTo(body.Definition.DefinitionId),
                "The column route must exercise the install-only prerequisite branch, " +
                "not the wheel mount's direct body-shell owner branch.");

            fixture.PlaceAtWheel(wheel);
            var context = new MSC.Interaction.InteractionContext(
                fixture.Assembly.gameObject,
                fixture.WheelMount.Pose.position,
                fixture.WheelMount.Pose.forward);
            Assert.That(column.IsInstalled, Is.False);
            Assert.That(columnSurface.CanAccept(wheel.PickupTarget, context), Is.False,
                "A loose column surface must not expose the steering-wheel socket.");
            Assert.That(bodySurface.CanAccept(wheel.PickupTarget, context), Is.False,
                "Even the authored body-shell owner route must honor the absent-column gate.");

            fixture.Restore(fixture.Snapshot(
                columnInstalled: true,
                wheelPartId: null,
                wheelTightness: 0,
                wheelBolted: false,
                dashboardInstalled: true));
            fixture.PlaceAtWheel(wheel);
            Assert.That(column.IsInstalled, Is.True);
            Assert.That(dashboard.IsInstalled, Is.True);
            Assert.That(columnSurface.CanAccept(wheel.PickupTarget, context), Is.True,
                columnSurface.HandoffPrompt);
            Assert.That(bodySurface.CanAccept(wheel.PickupTarget, context), Is.True,
                "The existing direct body-shell owner route remains compatible.");
            Assert.That(dashboardSurface.CanAccept(wheel.PickupTarget, context), Is.False,
                "An installed but unrelated cockpit surface must not route the wheel mount.");
        }

        [TestCase(GtWheelPartId)]
        [TestCase(StockWheelPartId)]
        public void InstalledWheelDoesNotBlockManualColumnRemoval(string wheelPartId)
        {
            using var fixture = InstalledWheelFixture(wheelPartId);
            Assert.That(fixture.Assembly.Graph.IsMountDependentOn(
                fixture.WheelRuntime, fixture.ColumnRuntime), Is.False,
                "The install-only prerequisite must not become a collapse edge.");

            PartInstance column = fixture.Part(ColumnPartId);
            Assert.That(fixture.Assembly.EvaluateRemoval(column).Succeeded, Is.True);
            Assert.That(fixture.Assembly.TryRemove(column).Succeeded, Is.True);
            Assert.That(column.IsInstalled, Is.False);
            Assert.That(fixture.WheelRuntime.IsOccupied, Is.True);
            Assert.That(fixture.Part(wheelPartId).IsInstalled, Is.True);
        }

        [TestCase(GtWheelPartId)]
        [TestCase(StockWheelPartId)]
        public void ForcedColumnCollapseDoesNotUseInstallOnlyWheelEdge(string wheelPartId)
        {
            using var fixture = InstalledWheelFixture(wheelPartId);
            Assert.That(fixture.Assembly.TryBreakInstalledPart(
                fixture.Part(ColumnPartId)).Succeeded, Is.True);
            Assert.That(fixture.ColumnRuntime.IsOccupied, Is.False);
            Assert.That(fixture.WheelRuntime.IsOccupied, Is.True);
            Assert.That(fixture.Part(wheelPartId).IsInstalled, Is.True);
        }

        [TestCase(GtWheelPartId)]
        [TestCase(StockWheelPartId)]
        public void LegacyWheelWithoutColumnRestoresButCannotBeReinstalled(
            string wheelPartId)
        {
            using var fixture = new Fixture();
            VehicleAssemblySaveData source = fixture.Snapshot(
                columnInstalled: false,
                wheelPartId,
                wheelTightness: 0,
                wheelBolted: false);
            fixture.AssertRestore(source, expectedWheelTightness: 0,
                expectedWheelBolted: false);
            Assert.That(fixture.ColumnRuntime.IsOccupied, Is.False);

            PartInstance wheel = fixture.Part(wheelPartId);
            Assert.That(fixture.Assembly.TryRemove(wheel).Succeeded, Is.True);
            fixture.PlaceAtWheel(wheel);
            AssertMissingColumn(fixture.Assembly.TryInstall(
                wheel, fixture.WheelMount));
            AssertMissingColumn(fixture.Assembly.TryInstall(
                wheel, fixture.WheelMount));
            Assert.That(wheel.IsInstalled, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HistoricalWheelLatchAtOneSurvivesRealSaveRoundTrip(bool bolted)
        {
            using var fixture = new Fixture();
            VehicleAssemblySaveData source = fixture.Snapshot(
                columnInstalled: false,
                wheelPartId: StockWheelPartId,
                wheelTightness: 1,
                wheelBolted: bolted);
            fixture.AssertRestore(source, expectedWheelTightness: 1,
                expectedWheelBolted: bolted);
            Assert.That(fixture.ColumnRuntime.IsOccupied, Is.False,
                "Restore must not retrofit the new install-only topology rule.");
        }

        private Fixture InstalledWheelFixture(string wheelPartId)
        {
            var fixture = new Fixture();
            try
            {
                fixture.Restore(fixture.Snapshot(
                    columnInstalled: true,
                    wheelPartId: null,
                    wheelTightness: 0,
                    wheelBolted: false));
                PartInstance wheel = fixture.Part(wheelPartId);
                fixture.PlaceAtWheel(wheel);
                Assert.That(fixture.Assembly.TryInstall(
                    wheel, fixture.WheelMount).Succeeded, Is.True);
                return fixture;
            }
            catch
            {
                fixture.Dispose();
                throw;
            }
        }

        private MountPointDefinition CreateWheelDefinition(
            int onThreshold,
            string[] installationRequired)
        {
            FastenerDefinition fastener = Track(
                ScriptableObject.CreateInstance<FastenerDefinition>());
            fastener.Configure(
                WheelFastenerId,
                "Wheel nut sentinel",
                FastenerSize.Millimeter10,
                stages: 8,
                FastenerDirection.CounterClockwiseToTighten,
                insertOnInstall: true,
                blocksRemoval: true,
                ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter10));

            MountPointDefinition definition = Track(
                ScriptableObject.CreateInstance<MountPointDefinition>());
            definition.Configure(
                WheelMountId,
                "Steering wheel sentinel",
                "satsuma.socket.steering-wheel.sentinel",
                "vehicle.satsuma.part.body-shell",
                WheelPartIds.ToArray(),
                new MountConstraint(0.314f, 27f, 0.99f, 0.123f),
                0.019f,
                new[] { fastener });
            definition.ConfigureSequence(
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());
            definition.ConfigureInstallationOccupancy(installationRequired);
            definition.ConfigureBoltedSequence(
                Array.Empty<string>(),
                Array.Empty<string>());
            definition.ConfigureRemovalBlockers(Array.Empty<string>());
            definition.ConfigureRemovalChecks(
                Array.Empty<string>(),
                Array.Empty<string>());
            definition.ConfigureInstallationChecks(
                string.Empty,
                Array.Empty<string>());
            var group = new FastenerGroupDefinition();
            group.Configure(
                new[] { WheelFastenerId },
                maximumTightness: 8,
                onThreshold: onThreshold,
                offThreshold: 0,
                retentionPolicy: FastenerSpeedRetentionPolicy.None,
                configuredLooseBreakSpeedKph: 17.25f,
                configuredPartialCheckSpeedKph: 29.5f,
                configuredChanceDivisor: 73.75f,
                configuredBreakAction: FastenerBreakAction.None);
            definition.ConfigureFastenerGroup(group);
            return definition;
        }

        private static void ApplyDrift(MountPointDefinition definition, string drift)
        {
            FastenerDefinition fastener = definition.Fasteners.Single();
            FastenerGroupDefinition group = definition.FastenerGroup;
            switch (drift)
            {
                case "definition-id":
                    ReconfigureMount(definition, id: "mount.satsuma.wrong-wheel");
                    return;
                case "owner":
                    ReconfigureMount(definition, owner: ColumnPartId);
                    return;
                case "accepted-parts":
                    ReconfigureMount(definition, accepted: WheelPartIds.Reverse().ToArray());
                    return;
                case "fastener-count":
                    ReconfigureMount(definition, fasteners: Array.Empty<FastenerDefinition>());
                    return;
                case "fastener-id":
                    ReconfigureFastener(fastener, id: "fastener.satsuma.wrong-wheel");
                    return;
                case "fastener-size":
                    ReconfigureFastener(fastener, size: FastenerSize.Millimeter9);
                    return;
                case "fastener-maximum":
                    ReconfigureFastener(fastener, maximum: 7);
                    return;
                case "fastener-inserted":
                    ReconfigureFastener(fastener, inserted: false);
                    return;
                case "fastener-required":
                    ReconfigureFastener(fastener, required: false);
                    return;
                case "fastener-tool-type":
                    ReconfigureFastener(fastener, toolType: "Socket");
                    return;
                case "fastener-tool-size":
                    ReconfigureFastener(fastener, toolSize: FastenerSize.Millimeter9);
                    return;
                case "group-ids":
                    ReconfigureGroup(group, ids: new[] { "fastener.satsuma.wrong-wheel" });
                    return;
                case "group-maximum":
                    ReconfigureGroup(group, maximum: 7);
                    return;
                case "group-on":
                    ReconfigureGroup(group, on: 3);
                    return;
                case "group-off":
                    ReconfigureGroup(group, off: 1);
                    return;
                case "retention":
                    ReconfigureGroup(group,
                        retention: FastenerSpeedRetentionPolicy.DonorWheelBoltCheck);
                    return;
                case "break-action":
                    ReconfigureGroup(group,
                        breakAction: FastenerBreakAction.DetachInstalledPart);
                    return;
                case "group-loose-negative":
                    SetSerializedGroupFloat(
                        definition,
                        "looseBreakSpeedKph",
                        -0.25f);
                    return;
                case "group-partial-order":
                    SetSerializedGroupFloat(
                        definition,
                        "partialCheckSpeedKph",
                        10f);
                    return;
                case "group-chance-zero":
                    SetSerializedGroupFloat(
                        definition,
                        "chanceDivisor",
                        0f);
                    return;
                case "install-only":
                    definition.ConfigureInstallationOccupancy(
                        new[] { "mount.satsuma.wrong-column" });
                    return;
                case "required":
                    definition.ConfigureSequence(
                        new[] { ColumnMountId },
                        definition.RequiredAnyOccupiedMountIds,
                        definition.BlockedWhileOccupiedMountIds);
                    return;
                case "required-any":
                    definition.ConfigureSequence(
                        definition.RequiredOccupiedMountIds,
                        new[] { ColumnMountId },
                        definition.BlockedWhileOccupiedMountIds);
                    return;
                case "blocked":
                    definition.ConfigureSequence(
                        definition.RequiredOccupiedMountIds,
                        definition.RequiredAnyOccupiedMountIds,
                        new[] { ColumnMountId });
                    return;
                case "removal-blocked":
                    definition.ConfigureRemovalBlockers(new[] { ColumnMountId });
                    return;
                case "removal-bolted":
                    definition.ConfigureRemovalChecks(
                        new[] { ColumnMountId },
                        definition.RemovalIgnoredDependentMountIds);
                    return;
                case "removal-ignored":
                    definition.ConfigureRemovalChecks(
                        definition.RemovalBlockedWhileBoltedMountIds,
                        new[] { ColumnMountId });
                    return;
                case "required-bolted":
                    definition.ConfigureBoltedSequence(
                        new[] { ColumnMountId },
                        definition.RequiredAnyBoltedMountIds);
                    return;
                case "required-any-bolted":
                    definition.ConfigureBoltedSequence(
                        definition.RequiredBoltedMountIds,
                        new[] { ColumnMountId });
                    return;
                case "installation-bolted":
                    definition.ConfigureInstallationChecks(
                        definition.InstallAttemptBoltedSupportMountId,
                        new[] { ColumnMountId });
                    return;
                case "installation-support":
                    definition.ConfigureInstallationChecks(
                        ColumnMountId,
                        definition.InstallationBlockedWhileBoltedMountIds);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(drift), drift, null);
            }
        }

        private static void ReconfigureMount(
            MountPointDefinition definition,
            string id = null,
            string owner = null,
            string[] accepted = null,
            FastenerDefinition[] fasteners = null)
        {
            definition.Configure(
                id ?? definition.DefinitionId,
                definition.DisplayName,
                definition.SocketType,
                owner ?? definition.OwnerPartDefinitionId,
                accepted ?? definition.AcceptedPartDefinitionIds,
                definition.Constraint,
                definition.ReferenceCandidateRadiusMeters,
                fasteners ?? definition.Fasteners);
        }

        private static void ReconfigureFastener(
            FastenerDefinition definition,
            string id = null,
            FastenerSize? size = null,
            int? maximum = null,
            bool? inserted = null,
            bool? required = null,
            string toolType = null,
            FastenerSize? toolSize = null)
        {
            definition.Configure(
                id ?? definition.DefinitionId,
                definition.DisplayName,
                size ?? definition.Size,
                maximum ?? definition.MaximumStage,
                definition.TighteningDirection,
                inserted ?? definition.InsertedOnInstall,
                required ?? definition.RequiredForRemoval,
                ToolCompatibilityRule.Create(
                    toolType ?? definition.ToolRule.ToolType,
                    toolSize ?? definition.ToolRule.FastenerSize));
        }

        private static void ReconfigureGroup(
            FastenerGroupDefinition group,
            string[] ids = null,
            int? maximum = null,
            int? on = null,
            int? off = null,
            FastenerSpeedRetentionPolicy? retention = null,
            FastenerBreakAction? breakAction = null)
        {
            group.Configure(
                ids ?? group.FastenerDefinitionIds,
                maximum ?? group.AggregateMaximumTightness,
                on ?? group.BoltedOnThreshold,
                off ?? group.BoltedOffThreshold,
                retention ?? group.SpeedRetentionPolicy,
                group.LooseBreakSpeedKph,
                group.PartialCheckSpeedKph,
                group.ChanceDivisor,
                breakAction ?? group.BreakAction);
        }

        private static void SetSerializedGroupFloat(
            MountPointDefinition definition,
            string propertyName,
            float value)
        {
            var serialized = new SerializedObject(definition);
            SerializedProperty property = serialized
                .FindProperty("fastenerGroup")
                ?.FindPropertyRelative(propertyName);
            if (property == null)
            {
                throw new InvalidDataException(
                    "Missing serialized fastener-group field: " + propertyName);
            }

            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized.Update();
            Assert.That(
                serialized.FindProperty("fastenerGroup")
                    ?.FindPropertyRelative(propertyName)?.floatValue,
                Is.EqualTo(value),
                propertyName + " must remain invalid before the helper preflight.");
        }

        private static void AssertLegacyAssemblyRulesStayEmpty(
            MountPointDefinition definition)
        {
            Assert.That(definition.RequiredOccupiedMountIds, Is.Empty);
            Assert.That(definition.RequiredAnyOccupiedMountIds, Is.Empty);
            Assert.That(definition.BlockedWhileOccupiedMountIds, Is.Empty);
            Assert.That(definition.RemovalBlockedWhileOccupiedMountIds, Is.Empty);
            Assert.That(definition.RemovalBlockedWhileBoltedMountIds, Is.Empty);
            Assert.That(definition.RemovalIgnoredDependentMountIds, Is.Empty);
            Assert.That(definition.RequiredBoltedMountIds, Is.Empty);
            Assert.That(definition.RequiredAnyBoltedMountIds, Is.Empty);
            Assert.That(definition.InstallationBlockedWhileBoltedMountIds, Is.Empty);
            Assert.That(definition.InstallAttemptBoltedSupportMountId, Is.Empty);
        }

        private static void SetStage(
            FastenerInstance fastener,
            FastenerGroupState group,
            int stage)
        {
            Assert.That(fastener.TryRestore(
                    restoredInserted: true,
                    restoredSeated: true,
                    restoredStage: stage),
                Is.True);
            group.Reevaluate(mountOccupied: true);
            Assert.That(group.Tightness, Is.EqualTo(stage));
        }

        private static void AssertMissingColumn(AssemblyOperationResult result)
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(AssemblyFailureReason.MissingPrerequisite), result.Message);
        }

        private T Track<T>(T value) where T : Object
        {
            transientObjects.Add(value);
            return value;
        }

        private static GameObject GeneratedPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null,
                "The private generated Satsuma baseline is required.");
            return prefab;
        }

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject instance;
            private readonly VehicleAssemblySaveData emptySnapshot;
            private readonly PartInstance[] ownedParts;

            public Fixture()
            {
                instance = Object.Instantiate(GeneratedPrefab());
                Assembly = instance.GetComponent<VehicleAssemblyController>();
                ownedParts = Assembly.Parts.ToArray();
                Assembly.Initialize();
                emptySnapshot = Assembly.CaptureSaveData();
                Assert.That(WheelRuntime.IsOccupied, Is.False,
                    "The fixture expects the generated wheel mount to start empty.");
                Assert.That(ColumnRuntime.IsOccupied, Is.False,
                    "The fixture expects the generated column mount to start empty.");
                Assert.That(WheelMount.Definition.FastenerGroup.BoltedOnThreshold,
                    Is.EqualTo(2), "Run the C1b scoped refresh first.");
            }

            public VehicleAssemblyController Assembly { get; }

            public MountPointAuthoring WheelMount => Assembly.MountPoints.Single(value =>
                value.MountId == WheelMountId);

            public MountPointAuthoring ColumnMount => Assembly.MountPoints.Single(value =>
                value.MountId == ColumnMountId);

            public MountPointRuntime WheelRuntime => Assembly.ResolveMount(WheelMount);

            public MountPointRuntime ColumnRuntime => Assembly.ResolveMount(ColumnMount);

            public PartInstance Part(string definitionId) => Assembly.Parts.Single(value =>
                value.Definition.DefinitionId == definitionId);

            public void PlaceAtWheel(PartInstance wheel)
            {
                wheel.transform.SetPositionAndRotation(
                    WheelMount.Pose.position,
                    WheelMount.Pose.rotation);
                Physics.SyncTransforms();
            }

            public VehicleAssemblySaveData Snapshot(
                bool columnInstalled,
                string wheelPartId,
                int wheelTightness,
                bool wheelBolted,
                bool dashboardInstalled = false)
            {
                var data = JsonUtility.FromJson<VehicleAssemblySaveData>(
                    JsonUtility.ToJson(emptySnapshot));
                if (columnInstalled)
                {
                    Occupy(data, ColumnMount, ColumnPartId);
                }

                if (dashboardInstalled)
                {
                    Occupy(
                        data,
                        Assembly.MountPoints.Single(value =>
                            value.MountId == DashboardMountId),
                        DashboardPartId);
                }

                if (!string.IsNullOrEmpty(wheelPartId))
                {
                    Occupy(data, WheelMount, wheelPartId);
                    FastenerSaveDto savedFastener = data.fasteners.Single(value =>
                        value.mountId == WheelMountId &&
                        value.fastenerDefinitionId == WheelFastenerId);
                    savedFastener.inserted = true;
                    savedFastener.seated = true;
                    savedFastener.stage = wheelTightness;
                    data.fastenerGroups.Single(value =>
                        value.mountId == WheelMountId).isBolted = wheelBolted;
                }

                return data;
            }

            public void Restore(VehicleAssemblySaveData source)
            {
                string before = JsonUtility.ToJson(source);
                Assert.That(Assembly.ValidateSaveDataForRestore(source).Succeeded,
                    Is.True);
                AssemblyOperationResult result = Assembly.RestoreSaveData(source);
                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before));
            }

            public void AssertRestore(
                VehicleAssemblySaveData source,
                int expectedWheelTightness,
                bool expectedWheelBolted)
            {
                Restore(source);
                Assert.That(WheelRuntime.IsOccupied, Is.True);
                Assert.That(WheelRuntime.FastenerGroup.Tightness,
                    Is.EqualTo(expectedWheelTightness));
                Assert.That(WheelRuntime.FastenerGroup.IsBolted,
                    Is.EqualTo(expectedWheelBolted));

                VehicleAssemblySaveData roundTrip = Assembly.CaptureSaveData();
                Assert.That(roundTrip.fastenerGroups.Single(value =>
                    value.mountId == WheelMountId).isBolted,
                    Is.EqualTo(expectedWheelBolted));
                Restore(roundTrip);
                Assert.That(WheelRuntime.FastenerGroup.Tightness,
                    Is.EqualTo(expectedWheelTightness));
                Assert.That(WheelRuntime.FastenerGroup.IsBolted,
                    Is.EqualTo(expectedWheelBolted));
            }

            public void Dispose()
            {
                foreach (PartInstance part in ownedParts)
                {
                    if (part != null && !part.transform.IsChildOf(instance.transform))
                    {
                        Object.DestroyImmediate(part.gameObject);
                    }
                }

                Object.DestroyImmediate(instance);
            }

            private static void Occupy(
                VehicleAssemblySaveData data,
                MountPointAuthoring mount,
                string partDefinitionId)
            {
                MountSaveDto mountDto = data.mounts.Single(value =>
                    value.mountId == mount.MountId);
                Assert.That(mountDto.installedPartStableEntityId, Is.Empty,
                    mount.MountId + " must be empty in the baseline fixture.");
                PartSaveDto part = data.parts.Single(value =>
                    value.partDefinitionId == partDefinitionId);
                Assert.That(part.lifecycleState, Is.EqualTo(PartLifecycleState.Loose));
                part.lifecycleState = PartLifecycleState.Installed;
                part.installedMountId = mount.MountId;
                part.worldPosition = mount.Pose.position;
                part.worldRotation = mount.Pose.rotation;
                mountDto.installedPartStableEntityId = part.stableEntityId;
            }
        }
    }
}
