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
using Authoring = MSC.LegacyImport.Editor.GameplayPresentation.Phase1SatsumaEngineCapAndCoverAuthoring;
using Migration = MSC.Vehicle.Assembly.SatsumaRockerCoverFastenerMigration;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaEngineCapAndCoverTests
    {
        [Test]
        public void AuthoringRetainsExactSixStockIdentitiesAndOnlyReplacesTheirMesh()
        {
            using var fixture = new Fixture();
            string[] definitions = fixture.Cover.Fasteners.Select(value => EditorJsonUtility.ToJson(value)).ToArray();
            Vector3[] positions = fixture.Targets.Select(target => target.transform.localPosition).ToArray();
            Assert.That(Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh), Is.EqualTo(17));
            Assert.That(fixture.Cover.Fasteners.Select(fastener => fastener.DefinitionId),
                Is.EqualTo(Migration.CanonicalIds));
            Assert.That(fixture.Cover.FastenerGroup.FastenerDefinitionIds, Is.EqualTo(Migration.CanonicalIds));
            Assert.That(fixture.Cover.FastenerGroup.AggregateMaximumTightness, Is.EqualTo(48));
            Assert.That(fixture.Cover.FastenerGroup.BoltedOnThreshold, Is.EqualTo(2));
            Assert.That(fixture.Cover.FastenerGroup.BoltedOffThreshold, Is.Zero);
            foreach (FastenerDefinition fastener in fixture.AllFasteners)
            {
                int number = int.Parse(fastener.DefinitionId.Substring(Migration.FastenerPrefix.Length));
                Assert.That(EditorJsonUtility.ToJson(fastener), Is.EqualTo(definitions[number - 1]));
                AssemblyFastenerInteractionTarget target = fixture.Targets[number - 1];
                if (Migration.RetiredIds.Contains(fastener.DefinitionId)) Assert.That(target == null, Is.True);
                else
                {
                    Assert.That(target.transform.localPosition, Is.EqualTo(positions[number - 1]));
                    Assert.That(target.GetComponentInChildren<MeshFilter>(true).sharedMesh, Is.SameAs(fixture.BoltMesh));
                    Assert.That(new SerializedObject(target).FindProperty("fastenerPresentationBaseLocalPosition")
                        .vector3Value, Is.EqualTo(new Vector3(.001f, .002f, -.02f)));
                }
            }
            Assert.That(fixture.Cover.RemovalBlockedWhileOccupiedMountIds, Is.EqualTo(new[] { "test.blocker" }));
        }

        [Test]
        public void ExactLegacyGtWorldRoundTripPosesRetireWithoutMovingStockAndAreIdempotent()
        {
            using var fixture = new Fixture();
            // Read from the pre-dedupe generated prefab, not derived from the authoring table.
            Vector3[] importedGtPositions =
            {
                new Vector3(-.13671875f, -.0513037f, -.034413148f),
                new Vector3(-.048339844f, -.0513037f, -.034416962f),
                new Vector3(.12145996f, .062221676f, -.034409795f),
                new Vector3(-.052246094f, .062221676f, -.034413133f),
                new Vector3(-.13891602f, .062221676f, -.034413133f),
                new Vector3(.122924805f, -.0513037f, -.034416962f),
            };
            var retained = Migration.CanonicalIds.Select(id =>
                fixture.Targets.Single(target => target.FastenerDefinitionId == id)).ToArray();
            Vector3[] stockPositions = retained.Select(target => target.transform.localPosition).ToArray();
            for (int index = 0; index < Migration.RetiredIds.Length; index++)
            {
                fixture.Targets.Single(target => target.FastenerDefinitionId == Migration.RetiredIds[index])
                    .transform.localPosition = importedGtPositions[index];
            }

            Assert.That(Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh), Is.EqualTo(17));
            Assert.That(retained.Select(target => target.transform.localPosition), Is.EqualTo(stockPositions));
            Assert.That(fixture.Cover.Fasteners.Select(fastener => fastener.DefinitionId),
                Is.EqualTo(Migration.CanonicalIds));
            Assert.That(Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh), Is.Zero);
        }

        [TestCase(.00003f)]
        [TestCase(.0002f)]
        public void UnreviewedSmallGtPoseDriftStillRejectsBeforeMutation(float displacement)
        {
            using var fixture = new Fixture();
            fixture.Targets.Single(target => target.FastenerDefinitionId == Migration.RetiredIds[0])
                .transform.localPosition += Vector3.forward * displacement;
            string definitionBefore = EditorJsonUtility.ToJson(fixture.Cover);

            Assert.Throws<InvalidDataException>(() => Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh));
            Assert.That(EditorJsonUtility.ToJson(fixture.Cover), Is.EqualTo(definitionBefore));
            Assert.That(fixture.Targets.All(target => target != null), Is.True);
            Assert.That(fixture.Pistons.All(part => part.GetComponent<SatsumaPistonCapPresenter>() == null), Is.True);
        }

        [Test]
        public void CapIsHiddenLooseVisibleImmediatelyInstalledAndHiddenAgainOnRemoval()
        {
            using var fixture = new Fixture();
            Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh);
            foreach (PartInstance piston in fixture.Pistons)
            {
                SatsumaPistonCapPresenter presenter = piston.GetComponent<SatsumaPistonCapPresenter>();
                Assert.That(presenter.CapPresentation.activeSelf, Is.False);
                piston.RuntimeState.SetInstalled("test.loose-engine.piston", false);
                presenter.RefreshPresentation();
                Assert.That(presenter.CapPresentation.activeSelf, Is.True);
                Assert.That(piston.Body.isKinematic, Is.False, "Presentation must not alter physical state.");
                Assert.That(presenter.CapPresentation.GetComponentsInChildren<Collider>(true), Is.Empty);
                piston.RuntimeState.SetLoose(Vector3.zero, Quaternion.identity);
                presenter.RefreshPresentation();
                Assert.That(presenter.CapPresentation.activeSelf, Is.False);
                Assert.That(presenter.gameObject.activeSelf, Is.True);
            }
        }

        [Test]
        public void IdempotentPassRepairsBindingsAndRestoredInstalledVisibilityWithoutRecapturingPose()
        {
            using var fixture = new Fixture();
            Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh);
            Assert.That(Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh), Is.Zero);
            var presenter = fixture.Pistons[0].GetComponent<SatsumaPistonCapPresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("capPresentation").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            fixture.Pistons[0].RuntimeState.SetInstalled("test.restored.engine.piston", false);
            Assert.That(Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh), Is.EqualTo(1));
            Assert.That(presenter.CapPresentation.activeSelf, Is.True);
            Assert.That(Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh), Is.Zero);
        }

        [TestCase("cap")]
        [TestCase("target")]
        [TestCase("pose")]
        [TestCase("group")]
        public void IncompleteOrUnrecognizedPacketFailsBeforeAnyMutation(string defect)
        {
            using var fixture = new Fixture();
            switch (defect)
            {
                case "cap": Object.DestroyImmediate(fixture.Caps[3]); break;
                case "target": Object.DestroyImmediate(fixture.Targets[11]); break;
                case "pose": fixture.Targets[11].transform.localPosition += Vector3.right; break;
                case "group": fixture.Cover.FastenerGroup.Configure(Migration.CanonicalIds, 48, 2, 0); break;
            }
            string definitionBefore = EditorJsonUtility.ToJson(fixture.Cover);
            Assert.Throws<InvalidDataException>(() => Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh));
            Assert.That(EditorJsonUtility.ToJson(fixture.Cover), Is.EqualTo(definitionBefore));
            Assert.That(fixture.Pistons.All(part => part.GetComponent<SatsumaPistonCapPresenter>() == null), Is.True);
            Assert.That(fixture.AllFasteners.Length, Is.EqualTo(12));
        }

        [Test]
        public void SaveMigrationMergesExactPairsAndPreservesOriginalPayloadAndUnrelatedExtensions()
        {
            using var fixture = new Fixture();
            Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh);
            VehicleAssemblySaveData source = LegacySave();
            int[] stages = { 1, 6, 3, 2, 8, 0, 4, 2, 1, 7, 5, 0 };
            for (int index = 0; index < stages.Length; index++) source.fasteners[index].stage = stages[index];
            string before = JsonUtility.ToJson(source);
            Assert.That(Migration.TryMigrate(source, fixture.Cover, out var migrated, out var result), Is.True, result.Message);
            Assert.That(migrated, Is.Not.SameAs(source));
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before));
            Assert.That(migrated.fasteners.Select(state => state.fastenerDefinitionId), Is.EqualTo(Migration.CanonicalIds));
            Assert.That(migrated.fasteners.Select(state => state.stage), Is.EqualTo(new[] { 8, 5, 3, 4, 7, 1 }));
            Assert.That(migrated.fasteners.All(state => state.inserted && state.seated), Is.True);
            Assert.That(migrated.fastenerGroups[0].isBolted, Is.True);
            Assert.That(migrated.parts, Is.SameAs(source.parts));
            Assert.That(migrated.mounts, Is.SameAs(source.mounts));
            Assert.That(Migration.TryMigrate(migrated, fixture.Cover, out var repeated, out result), Is.True, result.Message);
            Assert.That(repeated, Is.SameAs(migrated));
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(8, true)]
        public void SavedLatchHistorySurvivesDedupeAndNewOnTwoThreshold(int stage, bool latched)
        {
            using var fixture = new Fixture();
            Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh);
            VehicleAssemblySaveData source = LegacySave();
            source.fasteners[0].stage = stage;
            source.fastenerGroups[0].isBolted = latched;
            Assert.That(Migration.TryMigrate(source, fixture.Cover, out var migrated, out var result), Is.True, result.Message);
            Assert.That(migrated.fasteners.Single(state => state.fastenerDefinitionId.EndsWith("-12")).stage, Is.EqualTo(stage));
            Assert.That(migrated.fastenerGroups[0].isBolted, Is.EqualTo(latched));
            var states = fixture.Cover.Fasteners.Select(definition => new FastenerInstance(definition)).ToArray();
            for (int index = 0; index < states.Length; index++)
            {
                FastenerSaveDto dto = migrated.fasteners[index];
                Assert.That(states[index].TryRestore(dto.inserted, dto.seated, dto.stage), Is.True);
            }
            var group = new FastenerGroupState(fixture.Cover.FastenerGroup, states);
            Assert.That(group.TryRestoreLatch(latched, true), Is.True);
        }

        [Test]
        public void LegacySchemaAndEmptyMountMigrateWithoutInventingInsertedBolts()
        {
            using var fixture = new Fixture();
            Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh);
            VehicleAssemblySaveData source = LegacySave();
            source.schemaVersion = VehicleAssemblySaveData.LegacySchemaVersion;
            source.fastenerGroups = Array.Empty<FastenerGroupSaveDto>();
            source.mounts[0].installedPartStableEntityId = string.Empty;
            foreach (FastenerSaveDto state in source.fasteners) { state.inserted = false; state.seated = false; }
            Assert.That(Migration.TryMigrate(source, fixture.Cover, out var migrated, out var result), Is.True, result.Message);
            Assert.That(migrated.fasteners.All(state => !state.inserted && !state.seated && state.stage == 0), Is.True);
            Assert.That(migrated.fastenerGroups, Is.Empty);
        }

        [TestCase("partial")]
        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("stage")]
        [TestCase("flags")]
        [TestCase("latch")]
        [TestCase("missing-group")]
        [TestCase("duplicate-group")]
        public void CorruptLegacyPayloadIsRejectedWithoutChangingIt(string defect)
        {
            using var fixture = new Fixture();
            Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh);
            VehicleAssemblySaveData source = LegacySave();
            source.fastenerGroups[0].isBolted = false;
            switch (defect)
            {
                case "partial": source.fasteners = source.fasteners.Take(11).ToArray(); break;
                case "duplicate": source.fasteners[11].fastenerDefinitionId = source.fasteners[0].fastenerDefinitionId; break;
                case "unknown": source.fasteners[11].fastenerDefinitionId = Migration.FastenerPrefix + "99"; break;
                case "stage": source.fasteners[0].stage = 9; break;
                case "flags": source.fasteners[0].inserted = false; break;
                case "latch": source.fastenerGroups[0].isBolted = true; break;
                case "missing-group": source.fastenerGroups = Array.Empty<FastenerGroupSaveDto>(); break;
                case "duplicate-group": source.fastenerGroups = new[] { source.fastenerGroups[0], source.fastenerGroups[0] }; break;
            }
            string before = JsonUtility.ToJson(source);
            Assert.That(Migration.TryMigrate(source, fixture.Cover, out var prepared, out var result), Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.InvalidSaveData));
            Assert.That(prepared, Is.SameAs(source));
            Assert.That(JsonUtility.ToJson(source), Is.EqualTo(before));
        }

        [Test]
        public void UnrelatedStatesAndAlreadyCanonicalHistoryAreNotRewritten()
        {
            using var fixture = new Fixture();
            Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh);
            VehicleAssemblySaveData source = LegacySave();
            source.fastenerGroups[0].isBolted = false;
            var unrelated = new FastenerSaveDto { mountId = "mount.unrelated", fastenerDefinitionId = "fastener.other", stage = 3 };
            source.fasteners = source.fasteners.Concat(new[] { unrelated }).ToArray();
            Assert.That(Migration.TryMigrate(source, fixture.Cover, out var migrated, out var result), Is.True, result.Message);
            Assert.That(migrated.fasteners[0], Is.SameAs(unrelated));
            migrated.fasteners.Last().stage = 1;
            Assert.That(Migration.TryMigrate(migrated, fixture.Cover, out var canonical, out result), Is.True, result.Message);
            Assert.That(canonical, Is.SameAs(migrated), "New ON2 allows a not-yet-latched T1; preserve it.");
        }

        [Test]
        public void PairInsertionAndSeatingAreMergedWithoutInventingStages()
        {
            using var fixture = new Fixture();
            Authoring.Configure(fixture.Assembly, fixture.CapMesh, fixture.BoltMesh);
            VehicleAssemblySaveData source = LegacySave();
            source.fastenerGroups[0].isBolted = false;
            source.fasteners[11].inserted = false;
            source.fasteners[11].seated = false;
            source.fasteners[0].seated = false;
            Assert.That(Migration.TryMigrate(source, fixture.Cover, out var migrated, out var result), Is.True, result.Message);
            FastenerSaveDto merged = migrated.fasteners.Single(state => state.fastenerDefinitionId.EndsWith("-12"));
            Assert.That(merged.inserted, Is.True);
            Assert.That(merged.seated, Is.False);
            Assert.That(merged.stage, Is.Zero);
        }

        [Test]
        public void OldAuthoredTwelveBoltPrefabIsNotForcedToLoadASixBoltSave()
        {
            using var fixture = new Fixture();
            VehicleAssemblySaveData source = LegacySave();
            Assert.That(Migration.TryMigrate(source, fixture.Cover, out var prepared, out var result), Is.True, result.Message);
            Assert.That(prepared, Is.SameAs(source));
            Assert.That(Migration.IsLegacyShape(source.fasteners.Select(state => state.fastenerDefinitionId).ToArray()), Is.True);
            string[] wrongSix = Migration.CanonicalIds;
            wrongSix[0] = wrongSix[1];
            Assert.That(Migration.IsCanonicalShape(wrongSix), Is.False);
        }

        private static VehicleAssemblySaveData LegacySave() => new VehicleAssemblySaveData
        {
            parts = new[] { new PartSaveDto { stableEntityId = "test.cover", hasCamshaftTiming = true } },
            mounts = new[] { new MountSaveDto { mountId = Migration.MountId, installedPartStableEntityId = "test.cover" } },
            fasteners = Enumerable.Range(1, 12).Select(number => new FastenerSaveDto
            {
                mountId = Migration.MountId, fastenerDefinitionId = Migration.FastenerPrefix + number,
                inserted = true, seated = true, stage = 0,
            }).ToArray(),
            fastenerGroups = new[] { new FastenerGroupSaveDto { mountId = Migration.MountId, isBolted = true } },
        };

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new GameObject("cap and cover fixture");
            private readonly List<Object> owned = new List<Object>();
            public readonly VehicleAssemblyController Assembly;
            public readonly Mesh CapMesh = new Mesh();
            public readonly Mesh BoltMesh = new Mesh();
            public readonly PartInstance[] Pistons = new PartInstance[4];
            public readonly GameObject[] Caps = new GameObject[4];
            public readonly MountPointDefinition Cover;
            public readonly FastenerDefinition[] AllFasteners = new FastenerDefinition[12];
            public readonly AssemblyFastenerInteractionTarget[] Targets = new AssemblyFastenerInteractionTarget[12];

            public Fixture()
            {
                owned.Add(CapMesh); owned.Add(BoltMesh);
                for (int index = 0; index < Pistons.Length; index++)
                {
                    var definition = ScriptableObject.CreateInstance<PartDefinition>();
                    definition.Configure(Authoring.PistonIds[index], "piston", PartCategory.Engine, 1f, root,
                        Array.Empty<PartCompatibilityRule>());
                    owned.Add(definition);
                    var go = new GameObject("piston " + index);
                    go.transform.SetParent(root.transform);
                    var identity = go.AddComponent<StableEntityIdAuthoring>();
                    identity.InitializeExplicitRuntimeId(StableEntityId.New());
                    Pistons[index] = go.AddComponent<PartInstance>();
                    Pistons[index].Configure(definition, identity, go.AddComponent<Rigidbody>(), null, false, string.Empty);
                    var cap = new GameObject("cap"); cap.transform.SetParent(go.transform);
                    cap.AddComponent<MeshFilter>().sharedMesh = CapMesh;
                    cap.AddComponent<MeshRenderer>(); cap.SetActive(false); Caps[index] = cap;
                }
                Cover = ScriptableObject.CreateInstance<MountPointDefinition>(); owned.Add(Cover);
                for (int index = 0; index < AllFasteners.Length; index++)
                {
                    var fastener = ScriptableObject.CreateInstance<FastenerDefinition>(); owned.Add(fastener);
                    fastener.Configure(Migration.FastenerPrefix + (index + 1), "Cover bolt " + (index + 1),
                        FastenerSize.Millimeter7, 8, FastenerDirection.ClockwiseToTighten, true, true,
                        ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter7));
                    AllFasteners[index] = fastener;
                }
                Cover.Configure(Migration.MountId, "Cover", "cover.socket", "vehicle.satsuma.part.cylinder-head",
                    new[] { "vehicle.satsuma.part.rocker-cover", "vehicle.satsuma.part.gt-rocker-cover-gt" },
                    new MountConstraint(.2f, 50f, .75f, 0f), .03f, AllFasteners);
                Cover.ConfigureFastenerGroup(FastenerGroupDefinition.CreateCompatibility(AllFasteners));
                Cover.ConfigureRemovalBlockers(new[] { "test.blocker" });
                var socket = new GameObject("cover socket"); socket.transform.SetParent(root.transform);
                var mount = socket.AddComponent<MountPointAuthoring>(); mount.Configure(Cover, Migration.MountId, socket.transform, 0);
                Assembly = root.AddComponent<VehicleAssemblyController>();
                var serialized = new SerializedObject(Assembly);
                var parts = serialized.FindProperty("parts"); parts.arraySize = 4;
                for (int index = 0; index < 4; index++) parts.GetArrayElementAtIndex(index).objectReferenceValue = Pistons[index];
                var mounts = serialized.FindProperty("mountPoints"); mounts.arraySize = 1;
                mounts.GetArrayElementAtIndex(0).objectReferenceValue = mount;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                for (int index = 0; index < 12; index++)
                {
                    string id = AllFasteners[index].DefinitionId;
                    int positionIndex = Array.IndexOf(Migration.CanonicalIds, id);
                    if (positionIndex < 0) positionIndex = Array.IndexOf(Migration.RetiredIds, id);
                    var marker = new GameObject(id); marker.transform.SetParent(socket.transform);
                    marker.transform.localPosition = Authoring.CoverMarkerPositions[positionIndex];
                    var visible = new GameObject("bolt visual"); visible.transform.SetParent(marker.transform);
                    visible.transform.localPosition = new Vector3(.001f, .002f, -.02f);
                    visible.AddComponent<MeshFilter>().sharedMesh = CapMesh; visible.AddComponent<MeshRenderer>();
                    Targets[index] = marker.AddComponent<AssemblyFastenerInteractionTarget>();
                    Targets[index].Configure(Assembly, Migration.MountId, id, null, false, visible.transform, .5f);
                }
            }

            public void Dispose()
            {
                Object.DestroyImmediate(root);
                foreach (Object value in owned) Object.DestroyImmediate(value);
            }
        }
    }
}
