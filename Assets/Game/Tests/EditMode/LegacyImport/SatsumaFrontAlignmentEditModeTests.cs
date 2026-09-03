using System;
using System.Linq;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaFrontAlignmentEditModeTests
    {
        private Random.State randomState;

        [SetUp]
        public void PreserveRandomState() => randomState = Random.state;

        [TearDown]
        public void RestoreRandomState() => Random.state = randomState;

        [Test]
        public void FreshLooseAlignmentUsesDonorRangeAndInitializesOnlyOnce()
        {
            GameObject instance = new GameObject("Fresh steering alignment state");
            try
            {
                PartInstance part = instance.AddComponent<PartInstance>();
                AssemblySteeringAlignmentState state =
                    instance.AddComponent<AssemblySteeringAlignmentState>();
                state.Configure(part);
                Random.InitState(61141);
                float expected = Random.Range(-6f, 6f);
                Random.InitState(61141);

                Assert.That(state.AlignmentDegrees, Is.EqualTo(expected));
                Assert.That(state.AlignmentDegrees, Is.InRange(-6f, 6f));
                int revision = state.Revision;
                Assert.That(state.CaptureSaveData().alignmentDegrees, Is.EqualTo(expected));
                Assert.That(state.AlignmentDegrees, Is.EqualTo(expected));
                Assert.That(state.Revision, Is.EqualTo(revision));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LooseLoadRandomizesRatherThanRestoringSavedToe(bool hasSavedValue)
        {
            GameObject instance = new GameObject("Loose steering alignment load");
            try
            {
                PartInstance part = instance.AddComponent<PartInstance>();
                AssemblySteeringAlignmentState state =
                    instance.AddComponent<AssemblySteeringAlignmentState>();
                state.Configure(part);
                AssemblySteeringAlignmentSaveDto saved = hasSavedValue
                    ? new AssemblySteeringAlignmentSaveDto { alignmentDegrees = 5.5f }
                    : null;
                Random.InitState(12014);
                float expected = Random.Range(-6f, 6f);
                Random.InitState(12014);

                state.RestoreValidated(saved);

                Assert.That(state.AlignmentDegrees, Is.EqualTo(expected));
                if (saved != null)
                {
                    Assert.That(saved.alignmentDegrees, Is.EqualTo(5.5f),
                        "Loading must not rewrite the caller's saved rod.");
                }
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [Test]
        public void AdjustmentHasDonorDirectionLimitsAndRepeatedLimitNotifications()
        {
            GameObject instance = new GameObject("Steering adjustment contract");
            try
            {
                PartInstance part = instance.AddComponent<PartInstance>();
                part.RuntimeState.SetInstalled("test.rod", isAssemblyRoot: false);
                AssemblySteeringAlignmentState state =
                    instance.AddComponent<AssemblySteeringAlignmentState>();
                state.Configure(part);
                state.RestoreValidated(new AssemblySteeringAlignmentSaveDto
                    { alignmentDegrees = 0f });
                Assert.That(state.TryAdjust(tighten: true), Is.True);
                Assert.That(state.AlignmentDegrees, Is.EqualTo(-0.1f).Within(0.00001f));
                Assert.That(state.TryAdjust(tighten: false), Is.True);
                Assert.That(state.AlignmentDegrees, Is.EqualTo(0f).Within(0.00001f));

                foreach (bool tighten in new[] { true, false })
                {
                    float limit = tighten ? -6f : 6f;
                    state.RestoreValidated(new AssemblySteeringAlignmentSaveDto
                        { alignmentDegrees = limit });
                    int revision = state.Revision;
                    Assert.That(state.TryAdjust(tighten), Is.True);
                    Assert.That(state.AlignmentDegrees, Is.EqualTo(limit));
                    Assert.That(state.Revision, Is.EqualTo(revision + 1),
                        "Even a clamped turn must notify the one-shot hub alignment consumer.");
                }

                part.RuntimeState.SetLoose(Vector3.zero, Quaternion.identity);
                int beforeRejectedTurn = state.Revision;
                Assert.That(state.TryAdjust(tighten: true), Is.False);
                Assert.That(state.AlignmentDegrees, Is.EqualTo(6f));
                Assert.That(state.Revision, Is.EqualTo(beforeRejectedTurn));
            }
            finally { Object.DestroyImmediate(instance); }
        }

        [TestCase("fl")]
        [TestCase("fr")]
        public void OrdinaryRodRemovalAndReinstallKeepItsAlignment(string corner)
        {
            using (var fixture = new Fixture())
            {
                fixture.PrepareRod(corner);
                PartInstance rod = fixture.Part("steering-rod-" + corner);
                AssemblySteeringAlignmentState state = Alignment(rod);
                state.RestoreValidated(new AssemblySteeringAlignmentSaveDto
                    { alignmentDegrees = 2.3f });
                int revision = state.Revision;

                AssertSuccess(fixture.Assembly.TryRemove(rod));
                Assert.That(state.AlignmentDegrees, Is.EqualTo(2.3f));
                fixture.Install("steering-rod-" + corner);

                Assert.That(state.AlignmentDegrees, Is.EqualTo(2.3f));
                Assert.That(state.Revision, Is.EqualTo(revision),
                    "An ordinary mount round trip is not the donor's loose-save load branch.");
            }
        }

        [Test]
        public void CurrentJsonRoundTripPreservesBothInstalledToeSettingsAndFasteners()
        {
            using (var fixture = new Fixture())
            {
                fixture.PrepareRod("fl");
                fixture.PrepareRod("fr");
                fixture.SetAlignment("fl", -3.7f);
                fixture.SetAlignment("fr", 4.2f);
                fixture.TurnFirst("steering-rod-fl", 5);
                fixture.TurnFirst("steering-rod-fr", 8);
                VehicleAssemblySaveData saved = fixture.Assembly.CaptureSaveData();
                string savedJson = JsonUtility.ToJson(saved);
                VehicleAssemblySaveData decoded =
                    JsonUtility.FromJson<VehicleAssemblySaveData>(savedJson);
                fixture.SetAlignment("fl", 0f);
                fixture.SetAlignment("fr", 0f);

                AssertSuccess(fixture.Assembly.RestoreSaveData(decoded));
                VehicleAssemblySaveData restored = fixture.Assembly.CaptureSaveData();

                Assert.That(fixture.Alignment("fl").AlignmentDegrees, Is.EqualTo(-3.7f));
                Assert.That(fixture.Alignment("fr").AlignmentDegrees, Is.EqualTo(4.2f));
                Assert.That(Fasteners(restored), Is.EquivalentTo(Fasteners(saved)));
                Assert.That(Groups(restored), Is.EquivalentTo(Groups(saved)));
                Assert.That(JsonUtility.ToJson(decoded), Is.EqualTo(savedJson));
            }
        }

        [Test]
        public void LegacyInstalledRodsWithoutAlignmentRestoreNeutralWithoutRewritingFasteners()
        {
            using (var fixture = new Fixture())
            {
                fixture.PrepareRod("fl");
                fixture.PrepareRod("fr");
                fixture.SetAlignment("fl", -5f);
                fixture.SetAlignment("fr", 3f);
                fixture.TurnFirst("steering-rod-fl", 8);
                fixture.TurnFirst("steering-rod-fl", 1);
                VehicleAssemblySaveData legacy = fixture.Assembly.CaptureSaveData();
                foreach (PartSaveDto part in legacy.parts)
                {
                    part.hasSteeringAlignment = false;
                    part.steeringAlignment = null;
                }
                string originalJson = JsonUtility.ToJson(legacy);

                AssertSuccess(fixture.Assembly.RestoreSaveData(legacy));

                Assert.That(fixture.Alignment("fl").AlignmentDegrees, Is.Zero);
                Assert.That(fixture.Alignment("fr").AlignmentDegrees, Is.Zero);
                VehicleAssemblySaveData restored = fixture.Assembly.CaptureSaveData();
                Assert.That(Fasteners(restored), Is.EquivalentTo(Fasteners(legacy)));
                Assert.That(Groups(restored), Is.EquivalentTo(Groups(legacy)));
                Assert.That(JsonUtility.ToJson(legacy), Is.EqualTo(originalJson));
            }
        }

        [Test]
        public void Legacy252MigrationPreservesInstalledToeAndNativeLatchHistory()
        {
            using (var fixture = new Fixture())
            {
                fixture.PrepareRod("fl");
                fixture.Install("strut-fl");
                fixture.SetAlignment("fl", 1.9f);
                fixture.TurnAll("wishbone-fl", 0);
                fixture.TurnFirst("wishbone-fl", 2);
                fixture.TurnFirst("wishbone-fl", 1);
                Assert.That(fixture.MountRuntime("wishbone-fl").FastenerGroup.IsBolted, Is.True);
                VehicleAssemblySaveData legacy = fixture.Assembly.CaptureSaveData();
                legacy.fasteners = BuildLegacy252Shape(legacy);
                Assert.That(legacy.fasteners, Has.Length.EqualTo(252));
                string originalJson = JsonUtility.ToJson(legacy);
                fixture.SetAlignment("fl", -2f);

                AssertSuccess(fixture.Assembly.RestoreSaveData(legacy));

                VehicleAssemblySaveData restored = fixture.Assembly.CaptureSaveData();
                Assert.That(restored.fasteners, Has.Length.EqualTo(280));
                Assert.That(fixture.Alignment("fl").AlignmentDegrees, Is.EqualTo(1.9f));
                Assert.That(UnchangedFasteners(restored),
                    Is.EquivalentTo(UnchangedFasteners(legacy)));
                Assert.That(Groups(restored), Is.EquivalentTo(Groups(legacy)));
                Assert.That(fixture.MountRuntime("wishbone-fl").FastenerGroup.IsBolted, Is.True);
                Assert.That(restored.fasteners.Where(IsLower).All(value => value.stage == 0), Is.True);
                Assert.That(JsonUtility.ToJson(legacy), Is.EqualTo(originalJson));
            }
        }

        [TestCase("nan")]
        [TestCase("positive-infinity")]
        [TestCase("negative-infinity")]
        [TestCase("over-limit")]
        [TestCase("under-limit")]
        [TestCase("schema")]
        [TestCase("wrong-part")]
        public void InvalidAlignmentSaveIsRejectedBeforeMutatingAnyLiveState(string fault)
        {
            using (var fixture = new Fixture())
            {
                fixture.PrepareRod("fl");
                fixture.SetAlignment("fl", 1.25f);
                fixture.TurnFirst("steering-rod-fl", 4);
                VehicleAssemblySaveData corrupt = fixture.Assembly.CaptureSaveData();
                PartSaveDto target = corrupt.parts.Single(value => value.partDefinitionId ==
                    "vehicle.satsuma.part." +
                    (fault == "wrong-part" ? "sub-frame" : "steering-rod-fl"));
                target.steeringAlignment = new AssemblySteeringAlignmentSaveDto
                    { alignmentDegrees = 1.25f };
                target.hasSteeringAlignment = true;
                switch (fault)
                {
                    case "nan": target.steeringAlignment.alignmentDegrees = float.NaN; break;
                    case "positive-infinity":
                        target.steeringAlignment.alignmentDegrees = float.PositiveInfinity; break;
                    case "negative-infinity":
                        target.steeringAlignment.alignmentDegrees = float.NegativeInfinity; break;
                    case "over-limit": target.steeringAlignment.alignmentDegrees = 6.001f; break;
                    case "under-limit": target.steeringAlignment.alignmentDegrees = -6.001f; break;
                    case "schema": target.steeringAlignment.schemaVersion = 99; break;
                }
                string before = JsonUtility.ToJson(fixture.Assembly.CaptureSaveData());
                int revision = fixture.Alignment("fl").Revision;
                int graphRevision = fixture.Assembly.GraphMutationCount;

                Assert.That(fixture.Assembly.ValidateSaveDataForRestore(corrupt).FailureReason,
                    Is.EqualTo(AssemblyFailureReason.InvalidSaveData));
                Assert.That(fixture.Assembly.RestoreSaveData(corrupt).FailureReason,
                    Is.EqualTo(AssemblyFailureReason.InvalidSaveData));

                Assert.That(JsonUtility.ToJson(fixture.Assembly.CaptureSaveData()), Is.EqualTo(before));
                Assert.That(fixture.Alignment("fl").Revision, Is.EqualTo(revision));
                Assert.That(fixture.Assembly.GraphMutationCount, Is.EqualTo(graphRevision));
            }
        }

        [TestCase("wishbone-fl", "spindle-fl")]
        [TestCase("wishbone-fr", "spindle-fr")]
        [TestCase("spindle-fl", "strut-fl")]
        [TestCase("spindle-fr", "strut-fr")]
        public void FrontInstallGateUsesDonorLatchNotFullTightnessOrCurrentStageAlone(
            string support, string dependent)
        {
            using (var fixture = new Fixture())
            {
                fixture.PrepareSupport(support);
                MountPointRuntime supportMount = fixture.MountRuntime(support);
                Assert.That(supportMount.FastenerGroup.Definition.BoltedOnThreshold, Is.EqualTo(2));
                Assert.That(supportMount.FastenerGroup.Definition.BoltedOffThreshold, Is.Zero);
                Assert.That(supportMount.FastenerGroup.Definition.AggregateMaximumTightness,
                    Is.EqualTo(support.StartsWith("wishbone-", StringComparison.Ordinal) ? 16 : 8));

                fixture.AssertInstallAllowed(dependent, expected: false);
                fixture.TurnFirst(support, 1);
                fixture.AssertInstallAllowed(dependent, expected: false);
                fixture.TurnFirst(support, 2);
                fixture.AssertInstallAllowed(dependent, expected: true);
                fixture.TurnFirst(support, 1);
                fixture.AssertInstallAllowed(dependent, expected: true);
                fixture.TurnFirst(support, 0);
                fixture.AssertInstallAllowed(dependent, expected: false);
                fixture.TurnFirst(support, 2);
                fixture.Install(dependent);
                Assert.That(fixture.Part(dependent).IsInstalled, Is.True);

                // Installation-only gates must not create a new cascade on an
                // existing construction or turn historical saves into errors.
                fixture.TurnFirst(support, 0);
                Assert.That(fixture.Part(dependent).IsInstalled, Is.True);
                VehicleAssemblySaveData saved = fixture.Assembly.CaptureSaveData();
                AssertSuccess(fixture.Assembly.RestoreSaveData(saved));
                Assert.That(fixture.Part(dependent).IsInstalled, Is.True);
                Assert.That(fixture.MountRuntime(support).FastenerGroup.IsBolted, Is.False);
            }
        }

        [TestCase("spindle-fl")]
        [TestCase("spindle-fr")]
        [TestCase("strut-fl")]
        [TestCase("strut-fr")]
        public void HandoffSnapIgnoresCameraAndCarriedRotation(string slug)
        {
            using (var fixture = new Fixture())
            {
                string corner = slug.EndsWith("fl", StringComparison.Ordinal) ? "fl" : "fr";
                string support = slug.StartsWith("spindle-", StringComparison.Ordinal)
                    ? "wishbone-" + corner : "spindle-" + corner;
                fixture.PrepareSupport(support);
                fixture.TurnFirst(support, 2);
                PartInstance part = fixture.Part(slug);
                MountPointAuthoring mount = fixture.Mount(slug);
                GameObject cameraObject = new GameObject("Unrelated assembly view camera");
                cameraObject.AddComponent<Camera>().enabled = false;
                try
                {
                    foreach (Vector3 euler in new[]
                    {
                        new Vector3(0f, 0f, 0f),
                        new Vector3(73f, -142f, 51f),
                        new Vector3(-88f, 179f, -133f),
                    })
                    {
                        cameraObject.transform.SetPositionAndRotation(
                            mount.Pose.position + Vector3.one, Quaternion.Euler(euler));
                        Quaternion carried = Quaternion.Euler(euler) * Quaternion.Euler(31f, 83f, 17f);
                        part.transform.SetPositionAndRotation(mount.Pose.position, carried);
                        part.Body.position = mount.Pose.position;
                        part.Body.rotation = carried;
                        AssertSuccess(fixture.Assembly.TryInstallFromHandoff(part.PickupTarget, mount));
                        Assert.That(Quaternion.Angle(part.transform.rotation, mount.Pose.rotation),
                            Is.LessThan(0.001f));
                        Assert.That(Quaternion.Angle(part.Body.rotation, mount.Pose.rotation),
                            Is.LessThan(0.001f));
                        Assert.That(Vector3.Distance(part.transform.position, mount.Pose.position),
                            Is.LessThan(0.0001f));
                        AssertSuccess(fixture.Assembly.TryRemove(part));
                    }
                }
                finally { Object.DestroyImmediate(cameraObject); }
            }
        }

        private static AssemblySteeringAlignmentState Alignment(PartInstance part)
        {
            AssemblySteeringAlignmentState state = part.GetComponent<AssemblySteeringAlignmentState>();
            Assert.That(state, Is.Not.Null, part.Definition.DefinitionId);
            Assert.That(state.Part, Is.SameAs(part));
            return state;
        }

        private static bool IsLower(FastenerSaveDto value) =>
            value.fastenerDefinitionId.StartsWith("fastener.satsuma.strut-fl.lower-", StringComparison.Ordinal) ||
            value.fastenerDefinitionId.StartsWith("fastener.satsuma.strut-fr.lower-", StringComparison.Ordinal);

        private static FastenerSaveDto[] BuildLegacy252Shape(
            VehicleAssemblySaveData current)
        {
            var legacy = current.fasteners
                .Where(value => !IsLower(value) &&
                    !IsExteriorPanelFastener(value) &&
                    !IsSteeringWheelNut(value))
                .Select(CloneFastener)
                .ToList();
            FastenerSaveDto secondPhysicalColumnBolt = legacy.Single(value =>
                value.mountId == "mount.satsuma.steering-column" &&
                value.fastenerDefinitionId ==
                    "fastener.satsuma.steering-column.boltpm-2");
            legacy.Add(new FastenerSaveDto
            {
                mountId = secondPhysicalColumnBolt.mountId,
                fastenerDefinitionId =
                    "fastener.satsuma.steering-column.boltpm-3",
                inserted = secondPhysicalColumnBolt.inserted,
                seated = secondPhysicalColumnBolt.seated,
                stage = secondPhysicalColumnBolt.stage,
            });
            return legacy.ToArray();
        }

        private static bool IsExteriorPanelFastener(FastenerSaveDto value)
        {
            int count;
            switch (value.mountId)
            {
                case "mount.satsuma.bumper-front":
                case "mount.satsuma.bumper-rear":
                case "mount.satsuma.grille":
                    count = 2;
                    break;
                case "mount.satsuma.fender-left":
                case "mount.satsuma.fender-right":
                    count = 5;
                    break;
                case "mount.satsuma.hood":
                    count = 4;
                    break;
                default:
                    return false;
            }

            string prefix = "fastener." + value.mountId.Substring("mount.".Length) +
                ".boltpm-";
            return Enumerable.Range(1, count).Any(index =>
                value.fastenerDefinitionId == prefix + index);
        }

        private static bool IsSteeringWheelNut(FastenerSaveDto value) =>
            value.mountId == "mount.satsuma.steering-wheel" &&
            value.fastenerDefinitionId ==
                "fastener.satsuma.steering-wheel.boltpm-1";

        private static bool IsSteeringColumnRevisionFastener(
            FastenerSaveDto value) =>
            value.mountId == "mount.satsuma.steering-column" &&
            (value.fastenerDefinitionId ==
                 "fastener.satsuma.steering-column.boltpm-2" ||
             value.fastenerDefinitionId ==
                 "fastener.satsuma.steering-column.boltpm-3");

        private static FastenerSaveDto CloneFastener(FastenerSaveDto value) =>
            new FastenerSaveDto
            {
                mountId = value.mountId,
                fastenerDefinitionId = value.fastenerDefinitionId,
                inserted = value.inserted,
                seated = value.seated,
                stage = value.stage,
            };

        private static string[] UnchangedFasteners(
            VehicleAssemblySaveData data) => data.fasteners.Where(value =>
                !IsLower(value) &&
                !IsExteriorPanelFastener(value) &&
                !IsSteeringWheelNut(value) &&
                !IsSteeringColumnRevisionFastener(value)).Select(value =>
                    string.Join("|", value.mountId, value.fastenerDefinitionId,
                        value.inserted, value.seated, value.stage)).ToArray();

        private static string[] Fasteners(VehicleAssemblySaveData data) =>
            data.fasteners.Select(value =>
                string.Join("|", value.mountId, value.fastenerDefinitionId,
                    value.inserted, value.seated, value.stage)).ToArray();

        private static string[] Groups(VehicleAssemblySaveData data) =>
            data.fastenerGroups.Select(value => value.mountId + "|" + value.isBolted).ToArray();

        private static void AssertSuccess(AssemblyOperationResult result) =>
            Assert.That(result.Succeeded, Is.True, result.Message);

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject instance;

            public Fixture()
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
                if (prefab == null)
                {
                    Assert.Ignore("Private donor-derived Satsuma baseline has not been built.");
                }
                instance = Object.Instantiate(prefab);
                Assembly = instance.GetComponent<VehicleAssemblyController>();
                Assembly.Initialize();
            }

            public VehicleAssemblyController Assembly { get; }

            public PartInstance Part(string slug) => Assembly.Parts.Single(value =>
                value.Definition != null && value.Definition.DefinitionId == "vehicle.satsuma.part." + slug);

            public MountPointAuthoring Mount(string slug) => Assembly.MountPoints.Single(value =>
                value.MountId == "mount.satsuma." + slug);

            public MountPointRuntime MountRuntime(string slug) => Assembly.ResolveMount(Mount(slug));

            public AssemblySteeringAlignmentState Alignment(string corner) =>
                SatsumaFrontAlignmentEditModeTests.Alignment(Part("steering-rod-" + corner));

            public void SetAlignment(string corner, float degrees) =>
                Alignment(corner).RestoreValidated(new AssemblySteeringAlignmentSaveDto
                    { alignmentDegrees = degrees });

            public void PrepareSupport(string support)
            {
                Install("sub-frame", tighten: true);
                string corner = support.EndsWith("fl", StringComparison.Ordinal) ? "fl" : "fr";
                if (support.StartsWith("spindle-", StringComparison.Ordinal))
                {
                    Install("wishbone-" + corner, tighten: true);
                }
                Install(support);
            }

            public void PrepareRod(string corner)
            {
                Install("sub-frame", tighten: true);
                Install("steering-rack", tighten: true);
                Install("wishbone-" + corner, tighten: true);
                Install("spindle-" + corner, tighten: true);
                Install("steering-rod-" + corner);
            }

            public void Install(string slug, bool tighten = false)
            {
                PartInstance part = Part(slug);
                MountPointAuthoring mount = Mount(slug);
                if (!part.IsInstalled)
                {
                    MoveToMount(part, mount);
                    AssertSuccess(Assembly.TryInstall(part, mount));
                    Assert.That(part.IsInstalled, Is.True, slug);
                }
                if (tighten)
                {
                    foreach (FastenerDefinition definition in mount.Definition.Fasteners)
                    {
                        Turn(slug, definition.DefinitionId, definition.MaximumStage);
                    }
                }
            }

            public void AssertInstallAllowed(string slug, bool expected)
            {
                PartInstance part = Part(slug);
                MountPointAuthoring mount = Mount(slug);
                MoveToMount(part, mount);
                AssemblyOperationResult result = Assembly.EvaluateInstall(part, mount);
                Assert.That(result.Succeeded, Is.EqualTo(expected), slug + ": " + result.Message);
                Assert.That(Assembly.EvaluateHandoffInstall(part, mount).Succeeded, Is.EqualTo(expected));
                if (!expected)
                {
                    Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.MissingPrerequisite));
                }
            }

            public void TurnFirst(string slug, int stage) =>
                Turn(slug, Mount(slug).Definition.Fasteners[0].DefinitionId, stage);

            public void TurnAll(string slug, int stage)
            {
                foreach (FastenerDefinition definition in Mount(slug).Definition.Fasteners)
                {
                    Turn(slug, definition.DefinitionId, stage);
                }
            }

            private void Turn(string slug, string id, int stage)
            {
                MountPointRuntime mount = MountRuntime(slug);
                Assert.That(mount.TryGetFastener(id, out FastenerInstance fastener), Is.True, id);
                int attempts = 0;
                while (fastener.Stage != stage)
                {
                    Assert.That(attempts++, Is.LessThan(fastener.Definition.MaximumStage + 2));
                    ToolDefinition tool = Assembly.Tools.First(value =>
                        value != null && value.Size == fastener.Definition.Size);
                    AssertSuccess(Assembly.TryOperateFastener(mount.MountId, id, tool,
                        tighten: fastener.Stage < stage));
                }
            }

            private static void MoveToMount(PartInstance part, MountPointAuthoring mount)
            {
                part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
                part.Body.position = mount.Pose.position;
                part.Body.rotation = mount.Pose.rotation;
            }

            public void Dispose() => Object.DestroyImmediate(instance);
        }
    }
}
