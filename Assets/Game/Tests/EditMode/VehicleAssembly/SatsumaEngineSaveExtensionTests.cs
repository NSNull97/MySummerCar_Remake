using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.Interaction.Capabilities;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using Carb = MSC.Vehicle.Assembly.SatsumaCarburetorFastenerMigration;
using Cover = MSC.Vehicle.Assembly.SatsumaRockerCoverFastenerMigration;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaEngineSaveExtensionTests
    {
        private static readonly string[] AddedBodySlugs =
            { "exhaust-pipe", "exhaust-muffler", "fuel-tank", "seat-driver", "seat-passenger", "seat-rear" };
        private static bool IsAddedBody(string mountId) => AddedBodySlugs.Any(slug => mountId == "mount.satsuma." + slug);
        private static bool IsConsumableSocket(string mountId) => mountId.Contains("spark-plug-") ||
            mountId.EndsWith(".alternator-belt", StringComparison.Ordinal) || mountId.EndsWith(".light-bulb", StringComparison.Ordinal);

        [TestCase(273)] [TestCase(245)] [TestCase(253)]
        [TestCase(280)] [TestCase(252)] [TestCase(260)]
        public void ReviewedTwentyOneAdditionComposesWithoutRetighteningPreviousFasteners(int oldCount)
        {
            using var f = new Fixture(true, true, reviewedBody: true);
            VehicleAssemblySaveData source = f.Source(oldCount);
            source.schemaVersion = VehicleAssemblySaveData.FastenerGroupSchemaVersion;
            FastenerSaveDto retained = source.fasteners.First(value => value.fastenerDefinitionId.StartsWith("test.fastener.filler-", StringComparison.Ordinal));
            retained.stage = 3;
            source.fastenerGroups.Single(value => value.mountId == retained.mountId).isBolted = true;
            string original = JsonUtility.ToJson(source);
            MountPointRuntime sameMount = f.Assembly.Graph.Mounts[0];
            AssemblyOperationResult result = f.Assembly.RestoreSaveData(source);
            Assert.That(result.Succeeded, Is.True, result.Message);
            VehicleAssemblySaveData saved = f.Assembly.CaptureSaveData();
            Assert.That(saved.fasteners, Has.Length.EqualTo(294));
            Assert.That(saved.fasteners.Where(value => IsAddedBody(value.mountId)).All(value => value.inserted && value.seated && value.stage == 0), Is.True);
            Assert.That(saved.fastenerGroups.Where(value => IsAddedBody(value.mountId)).All(value => !value.isBolted), Is.True);
            Assert.That(saved.fasteners.Single(value => value.fastenerDefinitionId == retained.fastenerDefinitionId).stage, Is.EqualTo(3));
            if (oldCount is 245 or 252)
                Assert.That(saved.fasteners.Where(value => IsLower(value.mountId)).All(value => value.stage == 0), Is.True);
            Assert.That(f.Assembly.Graph.Mounts[0], Is.SameAs(sameMount));
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(original));
            Assert.That(f.Assembly.RestoreSaveData(saved).Succeeded, Is.True);
        }

        [Test]
        public void SevenNewSocketsAndGroupsStartEmptyWithoutChangingBaseRoster()
        {
            using var f = new Fixture(true, true, reviewedBody: true, consumableSockets: true);
            VehicleAssemblySaveData source = f.Source(273);
            source.schemaVersion = 2;
            PartSaveDto seat = source.parts.Single(value => value.partDefinitionId == "vehicle.satsuma.part.seat-driver");
            source.mounts.Single(value => value.mountId == seat.installedMountId).installedPartStableEntityId = string.Empty;
            seat.lifecycleState = PartLifecycleState.Loose;
            seat.installedMountId = string.Empty;
            var result = f.Assembly.RestoreSaveData(source);
            Assert.That(result.Succeeded, Is.True, result.Message);
            VehicleAssemblySaveData saved = f.Assembly.CaptureSaveData();
            Assert.That(saved.parts.Select(value => value.stableEntityId), Is.EquivalentTo(source.parts.Select(value => value.stableEntityId)));
            Assert.That(saved.mounts, Has.Length.EqualTo(124));
            Assert.That(saved.fastenerGroups, Has.Length.EqualTo(124));
            Assert.That(saved.fasteners, Has.Length.EqualTo(298));
            Assert.That(saved.fasteners.Where(value => IsConsumableSocket(value.mountId)).All(value => !value.inserted && !value.seated && value.stage == 0), Is.True);
            Assert.That(saved.mounts.Where(value => IsConsumableSocket(value.mountId)).All(value => string.IsNullOrEmpty(value.installedPartStableEntityId)), Is.True);
            Assert.That(saved.fastenerGroups.Where(value => IsConsumableSocket(value.mountId)).All(value => !value.isBolted), Is.True);
            Assert.That(saved.fasteners.Where(value => value.mountId == "mount.satsuma.seat-driver").All(value => !value.inserted && !value.seated && value.stage == 0), Is.True);
            Assert.That(saved.parts.Single(value => value.stableEntityId == seat.stableEntityId).lifecycleState, Is.EqualTo(PartLifecycleState.Loose));
        }

        [TestCase("body-tool")] [TestCase("plug-tool")] [TestCase("plug-id")]
        public void UnreviewedTargetFastenerContractCannotUseTheContentMigration(string corruption)
        {
            using var f = new Fixture(true, true, reviewedBody: true, consumableSockets: true);
            VehicleAssemblySaveData source = f.Source(273);
            string mountId = corruption == "body-tool" ? "mount.satsuma.fuel-tank" :
                SatsumaConsumableAssemblyRules.SparkPlugMountId(1);
            Assert.That(f.Assembly.Graph.TryGetMount(mountId, out MountPointRuntime mount), Is.True);
            FastenerDefinition definition = mount.Fasteners[0].Definition;
            definition.Configure(corruption == "plug-id" ? "unknown.thread" : definition.DefinitionId,
                definition.DisplayName, definition.Size, definition.MaximumStage, definition.TighteningDirection,
                definition.InsertedOnInstall, definition.RequiredForRemoval,
                ToolCompatibilityRule.Create(corruption == "plug-id" ? definition.ToolRule.ToolType : "WrongTool", definition.Size));
            string checkpoint = JsonUtility.ToJson(f.Assembly.CaptureSaveData());
            Assert.That(f.Assembly.RestoreSaveData(source).Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(checkpoint));
        }

        [TestCase("partial-new")] [TestCase("wrong-old-id")] [TestCase("old-bolted-group")]
        public void MalformedReviewedRevisionFailsBeforeMutation(string corruption)
        {
            using var f = new Fixture(true, true, reviewedBody: true);
            VehicleAssemblySaveData source = f.Source(273);
            if (corruption == "old-bolted-group") source.fastenerGroups.Single(value => value.mountId == "mount.satsuma.fuel-tank").isBolted = true;
            else if (corruption == "wrong-old-id") source.fasteners[0].fastenerDefinitionId = "unknown.old.fastener";
            else source.fasteners[0] = new FastenerSaveDto { mountId = "mount.satsuma.fuel-tank", fastenerDefinitionId = "fastener.satsuma.fuel-tank.boltpm-1" };
            string checkpoint = JsonUtility.ToJson(f.Assembly.CaptureSaveData());
            Assert.That(f.Assembly.RestoreSaveData(source).Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(checkpoint));
        }

        [TestCase(false, false, 280, 280)]
        [TestCase(true, false, 280, 274)]
        [TestCase(true, true, 280, 273)]
        [TestCase(true, true, 274, 273)]
        [TestCase(true, true, 273, 273)]
        [TestCase(false, false, 252, 280)]
        [TestCase(false, false, 260, 280)]
        [TestCase(true, false, 252, 274)]
        [TestCase(true, false, 260, 274)]
        [TestCase(true, true, 252, 273)]
        [TestCase(true, true, 260, 273)]
        [TestCase(true, true, 245, 273)]
        [TestCase(true, true, 253, 273)]
        public void ExactRetirementComposesWithHistoricalAdditionsAndOldGeneratedTargets(
            bool canonicalCover, bool canonicalCarb, int sourceCount, int expectedCount)
        {
            using var fixture = new Fixture(canonicalCover, canonicalCarb);
            VehicleAssemblySaveData source = fixture.Source(sourceCount);
            string originalJson = JsonUtility.ToJson(source);
            AssemblyOperationResult validation = fixture.Assembly.ValidateSaveDataForRestore(source);
            Assert.That(validation.Succeeded, Is.True, validation.Message);
            AssemblyOperationResult restore = fixture.Assembly.RestoreSaveData(source);
            Assert.That(restore.Succeeded, Is.True, restore.Message);
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(originalJson), "Migration must not mutate its source.");
            VehicleAssemblySaveData current = fixture.Assembly.CaptureSaveData();
            Assert.That(current.fasteners.Length, Is.EqualTo(expectedCount));
            Assert.That(fixture.Assembly.RestoreSaveData(current).Succeeded, Is.True, "Current DTO is idempotent.");
            if (sourceCount is 252 or 260 or 245 or 253)
            {
                Assert.That(current.fasteners.Where(value => IsExterior(value.mountId)).All(value => value.stage == 8),
                    Is.True, "Missing installed exterior fasteners retain their existing safe migration.");
                Assert.That(current.fasteners.Where(value => IsLower(value.mountId)).All(value => value.stage == 0),
                    Is.True, "New lower strut fasteners must remain loose.");
            }
        }

        [Test]
        public void CarbRetirementKeepsFourStagesAndDoesNotMapFakeStageIntoMixture()
        {
            using var fixture = new Fixture(true, true);
            VehicleAssemblySaveData source = fixture.Source(280);
            foreach (FastenerSaveDto state in source.fasteners.Where(value => value.mountId == Carb.MountId))
                state.stage = state.fastenerDefinitionId == Carb.RetiredId ? 8 : 1;
            source.fastenerGroups.Single(value => value.mountId == Carb.MountId).isBolted = true;
            string before = JsonUtility.ToJson(source);
            Assert.That(Carb.TryMigrate(source, fixture.CarbMount.Definition,
                out VehicleAssemblySaveData migrated, out AssemblyOperationResult result), Is.True, result.Message);
            Assert.That(migrated.fasteners.Count(value => value.mountId == Carb.MountId), Is.EqualTo(4));
            Assert.That(migrated.fasteners.Where(value => value.mountId == Carb.MountId).All(value => value.stage == 1), Is.True);
            Assert.That(migrated.fastenerGroups.Single(value => value.mountId == Carb.MountId).isBolted, Is.True);
            Assert.That(migrated.parts, Is.SameAs(source.parts));
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before));
            Assert.That(Carb.TryMigrate(migrated, fixture.CarbMount.Definition,
                out VehicleAssemblySaveData again, out _), Is.True);
            Assert.That(again, Is.SameAs(migrated));
        }

        [Test]
        public void OnlyFakeTightnessCannotKeepCarbLatchedAfterRetirement()
        {
            using var fixture = new Fixture(true, true);
            VehicleAssemblySaveData source = fixture.Source(280);
            source.fasteners.Single(value => value.fastenerDefinitionId == Carb.RetiredId).stage = 8;
            source.fastenerGroups.Single(value => value.mountId == Carb.MountId).isBolted = true;
            Assert.That(fixture.Assembly.RestoreSaveData(source).Succeeded, Is.True);
            Assert.That(fixture.Assembly.Graph.TryGetMount(Carb.MountId, out MountPointRuntime mount), Is.True);
            Assert.That(mount.FastenerGroup.IsBolted, Is.False);
            Assert.That(mount.Fasteners.All(value => value.Stage == 0), Is.True);
        }

        [TestCase("unknown")] [TestCase("duplicate")] [TestCase("stage")]
        [TestCase("flags")] [TestCase("latch")] [TestCase("missing-group")]
        [TestCase("partial-cover")] [TestCase("wrong-additive-hole")]
        public void CorruptRetirementOrCountLookalikeFailsBeforeMutation(string corruption)
        {
            using var fixture = new Fixture(true, true);
            VehicleAssemblySaveData source = fixture.Source(corruption == "wrong-additive-hole" ? 252 : 280);
            FastenerSaveDto fake = source.fasteners.Single(value => value.fastenerDefinitionId == Carb.RetiredId);
            switch (corruption)
            {
                case "unknown": fake.fastenerDefinitionId += ".unknown"; break;
                case "duplicate": fake.fastenerDefinitionId = Carb.CanonicalIds[0]; break;
                case "stage": fake.stage = 9; break;
                case "flags": fake.inserted = false; fake.stage = 1; break;
                case "latch": fake.stage = 1; break;
                case "missing-group": source.fastenerGroups = source.fastenerGroups.Where(value => value.mountId != Carb.MountId).ToArray(); break;
                case "partial-cover": source.fasteners = source.fasteners.Where(value => value.fastenerDefinitionId != Cover.RetiredIds[0]).ToArray(); break;
                case "wrong-additive-hole":
                    // Keep the historical aggregate count but exchange a required
                    // ordinary fastener for an unexpected lower strut fastener.
                    FastenerSaveDto ordinary = source.fasteners.First(value => value.mountId == "test.mount.filler-0");
                    ordinary.mountId = "mount.satsuma.strut-fl";
                    ordinary.fastenerDefinitionId = "fastener.satsuma.strut-fl.lower-1";
                    break;
            }
            string before = JsonUtility.ToJson(fixture.Assembly.CaptureSaveData());
            Assert.That(fixture.Assembly.RestoreSaveData(source).Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(fixture.Assembly.CaptureSaveData()), Is.EqualTo(before));
        }

        [Test]
        public void NewRetiredSaveDoesNotSilentlyResurrectAliasesOnAnOldPrefab()
        {
            using var fixture = new Fixture(false, false);
            Assert.That(fixture.Assembly.RestoreSaveData(fixture.Source(273)).Succeeded, Is.False);
        }

        [TestCase(SatsumaEngineAdjustmentKind.CarburetorMixture, 18f)]
        [TestCase(SatsumaEngineAdjustmentKind.OilFilter, 4f)]
        [TestCase(SatsumaEngineAdjustmentKind.Alternator, 5f)]
        [TestCase(SatsumaEngineAdjustmentKind.Distributor, 12f)]
        public void OptionalSettingRoundTripAndOldMissingFieldUsePartOwnedDefaults(
            SatsumaEngineAdjustmentKind kind, float value)
        {
            using var fixture = new Fixture(true, true);
            AssemblyEngineAdjustmentState state = fixture.AddSetting(kind);
            state.RestoreValidated(new AssemblyEngineAdjustmentSaveDto { kind = kind, value = value });
            VehicleAssemblySaveData source = fixture.Assembly.CaptureSaveData();
            PartSaveDto dto = source.parts.Single(part => part.stableEntityId == state.Part.StableId.Value);
            Assert.That(dto.hasEngineAdjustment, Is.True);
            Assert.That(dto.engineAdjustment.value, Is.EqualTo(value));
            state.RestoreValidated(null);
            Assert.That(fixture.Assembly.RestoreSaveData(JsonUtility.FromJson<VehicleAssemblySaveData>(JsonUtility.ToJson(source))).Succeeded, Is.True);
            Assert.That(state.Setting, Is.EqualTo(value));
            dto.hasEngineAdjustment = false;
            dto.engineAdjustment = new AssemblyEngineAdjustmentSaveDto { value = float.NaN };
            Assert.That(fixture.Assembly.RestoreSaveData(source).Succeeded, Is.True, "Presence bit owns optionality.");
            Assert.That(state.Setting, Is.EqualTo(SatsumaEngineAdjustmentRules.InitialValue(kind)));
        }

        [TestCase("nan")] [TestCase("wrong-kind")] [TestCase("schema")]
        [TestCase("missing-component")] [TestCase("loose-filter")]
        public void MalformedOptionalSettingRejectsBeforeAnyGraphMutation(string corruption)
        {
            using var fixture = new Fixture(true, true);
            AssemblyEngineAdjustmentState state = fixture.AddSetting(SatsumaEngineAdjustmentKind.OilFilter);
            VehicleAssemblySaveData source = fixture.Assembly.CaptureSaveData();
            PartSaveDto dto = source.parts.Single(part => part.stableEntityId == state.Part.StableId.Value);
            switch (corruption)
            {
                case "nan": dto.engineAdjustment.value = float.NaN; break;
                case "wrong-kind": dto.engineAdjustment.kind = SatsumaEngineAdjustmentKind.Alternator; break;
                case "schema": dto.engineAdjustment.schemaVersion++; break;
                case "missing-component": source.parts[0].hasEngineAdjustment = true; source.parts[0].engineAdjustment = dto.engineAdjustment; break;
                case "loose-filter": dto.lifecycleState = PartLifecycleState.Loose; dto.installedMountId = ""; dto.engineAdjustment.value = 1; break;
            }
            string before = JsonUtility.ToJson(fixture.Assembly.CaptureSaveData());
            Assert.That(fixture.Assembly.RestoreSaveData(source).Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(fixture.Assembly.CaptureSaveData()), Is.EqualTo(before));
        }

        [Test]
        public void FilterGuardBlocksOrdinaryRemovalButNotStructuralDetachAndImmediateCapture()
        {
            using var fixture = new Fixture(true, true);
            AssemblyEngineAdjustmentState state = fixture.AddSetting(SatsumaEngineAdjustmentKind.OilFilter);
            state.RestoreValidated(new AssemblyEngineAdjustmentSaveDto { kind = state.Kind, value = 2 });
            Assert.That(fixture.Assembly.EvaluateRemoval(state.Part).FailureReason, Is.EqualTo(AssemblyFailureReason.FastenerSecured));
            Assert.That(fixture.Assembly.TryBreakInstalledPart(state.Part).Succeeded, Is.True);
            VehicleAssemblySaveData save = fixture.Assembly.CaptureSaveData();
            Assert.That(save.parts.Single(value => value.stableEntityId == state.Part.StableId.Value).engineAdjustment.value, Is.Zero);
            Assert.That(fixture.Assembly.ValidateSaveDataForRestore(save).Succeeded, Is.True);
        }

        [Test]
        public void CurrentPoseRemovalPreservesWorldPoseWhileNormalRemovalKeepsItsOffset()
        {
            using var fixture = new Fixture(true, true);
            PartInstance part = fixture.CarbPart;
            Vector3 position = new Vector3(3, 4, 5);
            Quaternion rotation = Quaternion.Euler(15, 26, 37);
            part.transform.SetPositionAndRotation(position, rotation);
            Assert.That(fixture.Assembly.TryRemoveAtCurrentPose(part).Succeeded, Is.True);
            Assert.That(part.transform.position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(part.transform.rotation, rotation), Is.LessThan(.01f));
        }

        [Test]
        public void OptionalLooseEngineDockingRoundTripAndMissingFieldResetPendingOnly()
        {
            using var fixture = new Fixture(true, true);
            AssemblyEngineDockingState docking = fixture.AddDocking();
            docking.RestoreValidated(new AssemblyEngineDockingSaveDto { pendingStages = new[] { 0, 1, 0 } });
            VehicleAssemblySaveData save = fixture.Assembly.CaptureSaveData();
            PartSaveDto dto = save.parts.Single(value => value.stableEntityId == docking.Block.StableId.Value);
            Assert.That(dto.hasEngineDocking, Is.True);
            Assert.That(dto.lifecycleState, Is.EqualTo(PartLifecycleState.Loose));
            Assert.That(dto.engineDocking.pendingStages, Is.EqualTo(new[] { 0, 1, 0 }));
            docking.RestoreValidated(null);
            Assert.That(fixture.Assembly.RestoreSaveData(save).Succeeded, Is.True);
            Assert.That(docking.CaptureSaveData().pendingStages, Is.EqualTo(new[] { 0, 1, 0 }));
            dto.hasEngineDocking = false;
            dto.engineDocking = null;
            Assert.That(fixture.Assembly.RestoreSaveData(save).Succeeded, Is.True);
            Assert.That(docking.CaptureSaveData().IsEmpty, Is.True);
        }

        [TestCase("schema")] [TestCase("shape")] [TestCase("two-turns")]
        [TestCase("negative")] [TestCase("wrong-part")] [TestCase("installed-pending")]
        public void MalformedOptionalDockingFailsBeforeGraphMutation(string corruption)
        {
            using var fixture = new Fixture(true, true);
            AssemblyEngineDockingState docking = fixture.AddDocking();
            VehicleAssemblySaveData save = fixture.Assembly.CaptureSaveData();
            PartSaveDto dto = save.parts.Single(value => value.stableEntityId == docking.Block.StableId.Value);
            switch (corruption)
            {
                case "schema": dto.engineDocking.schemaVersion++; break;
                case "shape": dto.engineDocking.pendingStages = new int[2]; break;
                case "two-turns": dto.engineDocking.pendingStages = new[] { 1, 0, 1 }; break;
                case "negative": dto.engineDocking.pendingStages[0] = -1; break;
                case "wrong-part": save.parts[0].hasEngineDocking = true; save.parts[0].engineDocking = dto.engineDocking; break;
                case "installed-pending": dto.lifecycleState = PartLifecycleState.Installed; dto.engineDocking.pendingStages[0] = 1; break;
            }
            string before = JsonUtility.ToJson(fixture.Assembly.CaptureSaveData());
            Assert.That(fixture.Assembly.RestoreSaveData(save).Succeeded, Is.False);
            Assert.That(JsonUtility.ToJson(fixture.Assembly.CaptureSaveData()), Is.EqualTo(before));
        }

        [Test]
        public void DirectRestoreCancelsLiveCarbThrottleOnlyAfterPayloadPassesValidation()
        {
            using var fixture = new Fixture(true, true);
            var linkage = new GameObject("Throttle linkage");
            linkage.transform.SetParent(fixture.CarbPart.transform, false);
            var target = fixture.CarbPart.gameObject.AddComponent<AssemblyCarburetorThrottleTarget>();
            target.Configure(fixture.CarbPart, linkage.transform, null, null,
                Vector3.zero, Vector3.zero, Quaternion.identity);
            VehicleAssemblySaveData save = fixture.Assembly.CaptureSaveData();
            target.BeginContinuousInteraction(default, ContinuousContextInteractionDirection.Primary);
            Assert.That(target.IsHeld, Is.True);
            Assert.That(target.RequestedThrottle01, Is.EqualTo(1f));
            Assert.That(fixture.Assembly.RestoreSaveData(null).Succeeded, Is.False);
            Assert.That(target.IsHeld, Is.True, "Rejected restore must not change live input.");
            Assert.That(fixture.Assembly.RestoreSaveData(save).Succeeded, Is.True);
            Assert.That(target.IsHeld, Is.False);
            Assert.That(target.RequestedThrottle01, Is.Zero);
        }

        [Test]
        public void DirectRestoreRefreshesExplicitCompoundMassBeforeReturning()
        {
            using var fixture = new Fixture(true, true);
            PartInstance part = fixture.CarbPart;
            var shape = part.gameObject.AddComponent<BoxCollider>();
            shape.enabled = false; // Original installed source, not a second solid actor.
            var physics = fixture.Assembly.gameObject.AddComponent<AssemblyLooseCompoundPhysics>();
            physics.Configure(fixture.Assembly, new[]
                { new AssemblyCompoundShapeBinding(part, new Collider[] { shape }) });
            VehicleAssemblySaveData save = fixture.Assembly.CaptureSaveData();
            // Change an immutable fixture definition to make a missed explicit
            // refresh observable without advancing Unity's physics player loop.
            part.Definition.Configure(part.Definition.DefinitionId, "carb", PartCategory.Engine, 3f, null,
                new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
            part.Body.mass = 1f;
            Assert.That(fixture.Assembly.RestoreSaveData(save).Succeeded, Is.True);
            Assert.That(part.Body.mass, Is.EqualTo(3f).Within(.00001f));
            Assert.That(physics.ActiveProxyCount, Is.Zero);
        }

        private static bool IsLower(string id) => id is "mount.satsuma.strut-fl" or "mount.satsuma.strut-fr";
        private static bool IsExterior(string id) => id is "mount.satsuma.bumper-front" or
            "mount.satsuma.bumper-rear" or "mount.satsuma.grille" or "mount.satsuma.fender-left" or
            "mount.satsuma.fender-right" or "mount.satsuma.hood";

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("Engine native save regression");
            private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
            private readonly List<PartInstance> parts = new List<PartInstance>();
            private readonly List<MountPointAuthoring> mounts = new List<MountPointAuthoring>();
            public VehicleAssemblyController Assembly { get; }
            public MountPointAuthoring CarbMount { get; }
            public PartInstance CarbPart { get; }

            public Fixture(bool canonicalCover, bool canonicalCarb, bool reviewedBody = false, bool consumableSockets = false)
            {
                Part("test.body", true, "");
                AddMount(Cover.MountId, "test.cover", canonicalCover ? Cover.CanonicalIds :
                    Enumerable.Range(1, 12).Select(value => Cover.FastenerPrefix + value).ToArray(),
                    FastenerSize.Millimeter7, canonicalCover ? 2 : 1);
                CarbMount = AddMount(Carb.MountId, SatsumaEngineAdjustmentRules.PartId(SatsumaEngineAdjustmentKind.CarburetorMixture),
                    canonicalCarb ? Carb.CanonicalIds : Enumerable.Range(1, 5).Select(value => Carb.FastenerPrefix + value).ToArray(),
                    FastenerSize.Millimeter8, canonicalCarb ? 8 : 1);
                CarbPart = parts.Last();
                foreach (string side in new[] { "fl", "fr" })
                    AddMount("mount.satsuma.strut-" + side, "test.strut." + side,
                        Enumerable.Range(1, 4).Select(value => "fastener.satsuma.strut-" + side + ".lower-" + value).ToArray());
                foreach (string panel in new[] { "bumper-front", "bumper-rear", "grille", "fender-left", "fender-right", "hood" })
                {
                    int count = panel.StartsWith("fender", StringComparison.Ordinal) ? 5 : panel == "hood" ? 4 : 2;
                    AddMount("mount.satsuma." + panel, "test.panel." + panel,
                        Enumerable.Range(1, count).Select(value => "fastener.satsuma." + panel + ".boltpm-" + value).ToArray());
                }
                if (reviewedBody)
                {
                    int[] counts = { 3, 1, 7, 4, 4, 2 };
                    int[] thresholds = { 6, 2, 12, 7, 7, 6 };
                    FastenerSize[] sizes = { FastenerSize.Millimeter7, FastenerSize.Millimeter7, FastenerSize.Millimeter11,
                        FastenerSize.Millimeter9, FastenerSize.Millimeter9, FastenerSize.Millimeter9 };
                    for (int index = 0; index < AddedBodySlugs.Length; index++)
                    {
                        string slug = AddedBodySlugs[index];
                        AddMount("mount.satsuma." + slug, "vehicle.satsuma.part." + slug,
                            Enumerable.Range(1, counts[index]).Select(number => "fastener.satsuma." + slug + ".boltpm-" + number).ToArray(),
                            sizes[index], thresholds[index]);
                    }
                }
                for (int index = 0; index < (reviewedBody ? 101 : 107); index++)
                    AddMount("test.mount.filler-" + index, "test.filler-" + index,
                        index == 0 ? Enumerable.Range(0, 235).Select(value => "test.fastener.filler-" + value).ToArray() : Array.Empty<string>());
                while (parts.Count < 126) Part("test.loose-" + parts.Count, false, "");
                if (consumableSockets)
                {
                    for (int index = 1; index <= 4; index++)
                        AddMount("mount.satsuma.cylinder-head.spark-plug-" + index, "vehicle.satsuma.part.spark-plug",
                            new[] { SatsumaConsumableAssemblyRules.SparkPlugFastenerId(index) }, FastenerSize.None,
                            installPart: false, toolType: SatsumaAuxiliaryAssemblyTools.SparkPlugWrenchType);
                    foreach (string id in new[] { "mount.satsuma.engine-block.alternator-belt", "mount.satsuma.headlight-left.light-bulb", "mount.satsuma.headlight-right.light-bulb" })
                        AddMount(id, "test.consumable", Array.Empty<string>(), installPart: false);
                }
                Assembly = root.AddComponent<VehicleAssemblyController>();
                Assembly.Configure(parts.ToArray(), mounts.ToArray(), Array.Empty<AssemblyDependency>(),
                    Array.Empty<ToolDefinition>(), root.transform, true);
                foreach (MountPointRuntime mount in Assembly.Graph.Mounts)
                {
                    foreach (FastenerInstance fastener in mount.Fasteners) fastener.TryRestore(mount.IsOccupied, mount.IsOccupied, 0);
                    Assert.That(mount.FastenerGroup.TryRestoreLatch(false, mount.IsOccupied), Is.True);
                }
            }

            public VehicleAssemblySaveData Source(int count)
            {
                VehicleAssemblySaveData source = Assembly.CaptureSaveData();
                var fasteners = source.fasteners.ToList();
                if (count <= 280)
                {
                    fasteners.RemoveAll(value => IsAddedBody(value.mountId) || IsConsumableSocket(value.mountId));
                    source.mounts = source.mounts.Where(value => !IsConsumableSocket(value.mountId)).ToArray();
                    source.fastenerGroups = source.fastenerGroups.Where(value => !IsConsumableSocket(value.mountId)).ToArray();
                }
                bool wantsLegacyCover = count is 280 or 252 or 260;
                bool wantsLegacyCarb = count is 280 or 274 or 252 or 260;
                foreach (string id in Cover.RetiredIds)
                    if (wantsLegacyCover && fasteners.All(value => value.fastenerDefinitionId != id))
                        fasteners.Add(new FastenerSaveDto { mountId = Cover.MountId, fastenerDefinitionId = id, inserted = true, seated = true });
                    else if (!wantsLegacyCover) fasteners.RemoveAll(value => value.fastenerDefinitionId == id);
                if (wantsLegacyCarb && fasteners.All(value => value.fastenerDefinitionId != Carb.RetiredId))
                    fasteners.Add(new FastenerSaveDto { mountId = Carb.MountId, fastenerDefinitionId = Carb.RetiredId, inserted = true, seated = true });
                else if (!wantsLegacyCarb) fasteners.RemoveAll(value => value.fastenerDefinitionId == Carb.RetiredId);
                if (count is 252 or 245) fasteners.RemoveAll(value => IsLower(value.mountId));
                if (count is 252 or 260 or 245 or 253) fasteners.RemoveAll(value => IsExterior(value.mountId));
                source.fasteners = fasteners.ToArray();
                Assert.That(source.fasteners.Length, Is.EqualTo(count), "Fixture source shape");
                return source;
            }

            public AssemblyEngineAdjustmentState AddSetting(SatsumaEngineAdjustmentKind kind)
            {
                PartInstance part = kind == SatsumaEngineAdjustmentKind.CarburetorMixture ? CarbPart : parts[12];
                if (part != CarbPart)
                {
                    part.Definition.Configure(SatsumaEngineAdjustmentRules.PartId(kind), "setting", PartCategory.Engine, 1,
                        null, new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                    part.Configure(part.Definition, part.GetComponent<StableEntityIdAuthoring>(), part.Body, null,
                        false, part.InitialMountId);
                }
                // The fixture's dedicated filler mount must still explicitly
                // accept the new definition ID for ordinary save validation.
                if (part != CarbPart)
                {
                    MountPointAuthoring mount = mounts[11];
                    mount.Definition.Configure(mount.MountId, "setting", "test.socket", "test.body",
                        new[] { part.Definition.DefinitionId }, new MountConstraint(.1f, 30, 1, 0), 0,
                        Array.Empty<FastenerDefinition>());
                }
                Assembly.Configure(parts.ToArray(), mounts.ToArray(), Array.Empty<AssemblyDependency>(),
                    Array.Empty<ToolDefinition>(), root.transform, true);
                foreach (MountPointRuntime mount in Assembly.Graph.Mounts)
                {
                    foreach (FastenerInstance fastener in mount.Fasteners) fastener.TryRestore(true, true, 0);
                    Assert.That(mount.FastenerGroup.TryRestoreLatch(false, mount.IsOccupied), Is.True);
                }
                var mesh = new GameObject("Explicit setting presentation");
                mesh.transform.SetParent(part.transform, false);
                AssemblyEngineAdjustmentState state = part.gameObject.AddComponent<AssemblyEngineAdjustmentState>();
                state.Configure(kind, part, Assembly, Vector3.zero, Vector3.right, 0,
                    new[] { new AssemblyEngineAdjustmentPresentation(mesh.transform, Vector3.zero, Quaternion.identity) });
                state.RestoreValidated(null);
                return state;
            }

            public AssemblyEngineDockingState AddDocking()
            {
                PartInstance block = parts[13];
                MountPointAuthoring mount = mounts[12];
                block.Definition.Configure("vehicle.satsuma.part.engine-block", "block", PartCategory.Engine, 95,
                    null, new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                var pickup = block.gameObject.AddComponent<PhysicsPickupTarget>();
                pickup.Configure(block.Body, block.GetComponent<StableEntityIdAuthoring>(), "block", 120f);
                block.Configure(block.Definition, block.GetComponent<StableEntityIdAuthoring>(), block.Body, pickup,
                    false, block.InitialMountId);
                FastenerDefinition[] bolts = Enumerable.Range(1, 3).Select(index =>
                {
                    var bolt = Asset<FastenerDefinition>();
                    bolt.Configure("fastener.satsuma.engine-assembly.boltpm-" + index, "bolt", FastenerSize.Millimeter11, 8,
                        FastenerDirection.ClockwiseToTighten, true, true,
                        ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter11));
                    return bolt;
                }).ToArray();
                mount.Definition.Configure(mount.MountId, "engine socket", "test.socket", "test.body",
                    new[] { block.Definition.DefinitionId }, new MountConstraint(.1f, 30, 1, 0), 0, bolts);
                var dockingGroup = new FastenerGroupDefinition();
                dockingGroup.Configure(bolts.Select(value => value.DefinitionId).ToArray(), 24, 2, 0);
                mount.Definition.ConfigureFastenerGroup(dockingGroup);
                Assembly.Configure(parts.ToArray(), mounts.ToArray(), Array.Empty<AssemblyDependency>(),
                    Array.Empty<ToolDefinition>(), root.transform, true);
                foreach (MountPointRuntime point in Assembly.Graph.Mounts)
                {
                    foreach (FastenerInstance fastener in point.Fasteners) fastener.TryRestore(true, true, 0);
                    Assert.That(point.FastenerGroup.TryRestoreLatch(false, point.IsOccupied), Is.True);
                }
                Assert.That(Assembly.TryRemoveAtCurrentPose(block).Succeeded, Is.True);
                var state = block.gameObject.AddComponent<AssemblyEngineDockingState>();
                state.Configure(Assembly, block, mount, root.transform, new Vector3[3], new Vector3[3],
                    bolts.Select(value => value.DefinitionId).ToArray());
                state.RestoreValidated(null);
                return state;
            }

            private MountPointAuthoring AddMount(string id, string partId, string[] ids,
                FastenerSize size = FastenerSize.Millimeter8, int threshold = 1, bool installPart = true, string toolType = "Wrench")
            {
                if (installPart) Part(partId, false, id);
                FastenerDefinition[] definitions = ids.Select(fastenerId =>
                {
                    var bolt = Asset<FastenerDefinition>();
                    bolt.Configure(fastenerId, "bolt", size, 8, FastenerDirection.ClockwiseToTighten, true, true,
                        ToolCompatibilityRule.Create(toolType, size));
                    return bolt;
                }).ToArray();
                var definition = Asset<MountPointDefinition>();
                definition.Configure(id, id, "test.socket", "test.body", new[] { partId },
                    new MountConstraint(.1f, 30, 1, 0), 0, definitions);
                var group = new FastenerGroupDefinition();
                group.Configure(ids, ids.Length * 8, ids.Length == 0 ? 0 : threshold, 0);
                definition.ConfigureFastenerGroup(group);
                var go = new GameObject(id); go.transform.SetParent(root.transform, false);
                MountPointAuthoring mount = go.AddComponent<MountPointAuthoring>();
                mount.Configure(definition, id, go.transform, 0);
                mounts.Add(mount); return mount;
            }

            private PartInstance Part(string id, bool isRoot, string mountId)
            {
                var go = new GameObject(id); go.transform.SetParent(root.transform, false);
                var definition = Asset<PartDefinition>();
                definition.Configure(id, id, PartCategory.Engine, 1, null,
                    new[] { PartCompatibilityRule.Create("test.socket", "test.body") });
                var identity = go.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(StableEntityId.New());
                var part = go.AddComponent<PartInstance>();
                part.Configure(definition, identity, go.AddComponent<Rigidbody>(), null, isRoot, mountId);
                parts.Add(part); return part;
            }

            private T Asset<T>() where T : ScriptableObject
            { T asset = ScriptableObject.CreateInstance<T>(); assets.Add(asset); return asset; }
            public void Dispose()
            { Object.DestroyImmediate(root); foreach (ScriptableObject asset in assets) Object.DestroyImmediate(asset); }
        }
    }
}
