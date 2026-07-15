using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Editor.VehicleAssembly;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class VehicleAssemblyEditModeTests
    {
        private VehicleAssemblyController controller;
        private PartInstance drum;
        private MountPointAuthoring drumMount;

        [SetUp]
        public void SetUp()
        {
            Scene scene = EditorSceneManager.OpenScene(
                VehicleAssemblyPrototypePaths.PrototypeScene,
                OpenSceneMode.Single);
            controller = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<VehicleAssemblyController>(true))
                .Single();
            controller.Initialize();
            drum = FindPart("vehicle.brake_drum_rl");
            drumMount = FindMountFor("vehicle.brake_drum_rl");
        }

        [Test]
        public void RepresentativeVehicle_HasFifteenPartsAndFourteenMounts()
        {
            Assert.That(controller.Parts, Has.Length.EqualTo(15));
            Assert.That(controller.MountPoints, Has.Length.EqualTo(14));
            Assert.That(controller.Parts.Count(part => part.IsAssemblyRoot), Is.EqualTo(1));
        }

        [Test]
        public void DefinitionAndStableIds_AreUniqueAndExplicit()
        {
            Assert.That(
                controller.Parts.Select(part => part.Definition.DefinitionId).Distinct().Count(),
                Is.EqualTo(15));
            Assert.That(
                controller.Parts.Select(part => part.StableId.Value).Distinct().Count(),
                Is.EqualTo(15));
            Assert.That(controller.Parts.All(part => part.StableId.IsValid), Is.True);
        }

        [Test]
        public void CompatibilityRule_AcceptsOnlyMatchingPartAndMount()
        {
            Assert.That(drum.Definition.IsCompatibleWith(drumMount.Definition), Is.True);
            Assert.That(
                FindPart("vehicle.battery").Definition.IsCompatibleWith(drumMount.Definition),
                Is.False);
        }

        [Test]
        public void CandidateQuery_ReturnsStableMountWinner()
        {
            MoveToMount(drum, drumMount);
            AssemblyMountCandidate first = controller.FindBestMount(drum);
            AssemblyMountCandidate second = controller.FindBestMount(drum);

            Assert.That(first.IsValid, Is.True);
            Assert.That(second.IsValid, Is.True);
            Assert.That(first.Mount.MountId, Is.EqualTo("mount.brake_drum_rl"));
            Assert.That(second.Mount.MountId, Is.EqualTo(first.Mount.MountId));
        }

        [Test]
        public void Install_FailsWhenPrerequisiteIsMissing()
        {
            PartInstance head = FindPart("vehicle.cylinder_head");
            MountPointAuthoring mount = FindMountFor("vehicle.cylinder_head");
            MoveToMount(head, mount);

            AssemblyOperationResult result = controller.TryInstall(head, mount);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.MissingPrerequisite));
        }

        [Test]
        public void Install_SnapsAndCreatesLooseFastenerState()
        {
            InstallDrum();

            Assert.That(drum.IsInstalled, Is.True);
            Assert.That(drum.Body.isKinematic, Is.True);
            Assert.That(drum.transform.parent, Is.EqualTo(drumMount.Pose));
            Assert.That(GetDrumFastener().State, Is.EqualTo(FastenerState.Loose));
        }

        [Test]
        public void Install_RejectsWrongMountAndOccupiedMount()
        {
            PartInstance battery = FindPart("vehicle.battery");
            MoveToMount(battery, drumMount);
            AssemblyOperationResult incompatible = controller.TryInstall(battery, drumMount);
            InstallDrum();
            PartInstance wheel = FindPart("vehicle.wheel_rl");
            MoveToMount(wheel, drumMount);
            AssemblyOperationResult occupied = controller.TryInstall(wheel, drumMount);

            Assert.That(incompatible.FailureReason, Is.EqualTo(AssemblyFailureReason.Incompatible));
            Assert.That(occupied.FailureReason, Is.EqualTo(AssemblyFailureReason.MountOccupied));
        }

        [Test]
        public void Install_EnforcesPositionAndAngularTolerances()
        {
            drum.transform.position = drumMount.Pose.position + Vector3.right * 2f;
            drum.transform.rotation = drumMount.Pose.rotation;
            AssemblyOperationResult distance = controller.EvaluateInstall(drum, drumMount);
            drum.transform.position = drumMount.Pose.position;
            drum.transform.rotation = Quaternion.Euler(0f, 120f, 0f) * drumMount.Pose.rotation;
            AssemblyOperationResult angle = controller.EvaluateInstall(drum, drumMount);

            Assert.That(distance.FailureReason, Is.EqualTo(AssemblyFailureReason.OutsidePositionTolerance));
            Assert.That(angle.FailureReason, Is.EqualTo(AssemblyFailureReason.OutsideAngularTolerance));
        }

        [Test]
        public void ObstructionMarker_BlocksInstallation()
        {
            MoveToMount(drum, drumMount);
            drumMount.Configure(drumMount.Definition, drumMount.MountId, drumMount.Pose, ~0);
            GameObject blocker = new GameObject("TestObstruction");
            blocker.transform.position = drumMount.Pose.position;
            blocker.AddComponent<SphereCollider>().radius = 0.04f;
            blocker.AddComponent<AssemblyMountObstruction>();
            Physics.SyncTransforms();
            try
            {
                AssemblyOperationResult result = controller.EvaluateInstall(drum, drumMount);
                Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.Obstructed));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(blocker);
            }
        }

        [Test]
        public void Fastener_RejectsWrongToolSize()
        {
            InstallDrum();
            ToolDefinition wrongTool = controller.Tools.Single(tool => tool.Size == FastenerSize.Millimeter11);

            AssemblyOperationResult result = controller.TryOperateFastener(
                drumMount.MountId,
                GetDrumFastener().Definition.DefinitionId,
                wrongTool,
                tighten: true);

            Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.InvalidTool));
            Assert.That(GetDrumFastener().Stage, Is.Zero);
        }

        [Test]
        public void FastenerTurnDirection_UsesAuthoredTighteningConvention()
        {
            InstallDrum();
            ToolDefinition wrench14 = GetTool(FastenerSize.Millimeter14);
            FastenerInstance fastener = GetDrumFastener();

            Assert.That(controller.TryTurnFastener(
                drumMount.MountId,
                fastener.Definition.DefinitionId,
                wrench14,
                FastenerRotationDirection.Clockwise).Succeeded, Is.True);
            Assert.That(fastener.Stage, Is.EqualTo(1));
            Assert.That(controller.TryTurnFastener(
                drumMount.MountId,
                fastener.Definition.DefinitionId,
                wrench14,
                FastenerRotationDirection.CounterClockwise).Succeeded, Is.True);
            Assert.That(fastener.Stage, Is.Zero);
        }

        [Test]
        public void RearDrumFastener_UsesObservedZeroToEightDiscreteStages()
        {
            InstallDrum();
            ToolDefinition wrench14 = GetTool(FastenerSize.Millimeter14);
            FastenerInstance fastener = GetDrumFastener();
            for (int i = 0; i < 8; i++)
            {
                Assert.That(controller.TryOperateFastener(
                    drumMount.MountId,
                    fastener.Definition.DefinitionId,
                    wrench14,
                    tighten: true).Succeeded, Is.True);
            }

            Assert.That(fastener.Stage, Is.EqualTo(8));
            Assert.That(fastener.State, Is.EqualTo(FastenerState.Tightened));
            Assert.That(controller.TryOperateFastener(
                drumMount.MountId,
                fastener.Definition.DefinitionId,
                wrench14,
                tighten: true).FailureReason, Is.EqualTo(AssemblyFailureReason.FastenerAtLimit));
        }

        [Test]
        public void FastenerState_RepresentsAbsentInsertedLoosePartialAndTightened()
        {
            FastenerDefinition definition = ScriptableObject.CreateInstance<FastenerDefinition>();
            definition.Configure(
                "test.fastener",
                "Test Fastener",
                FastenerSize.Millimeter10,
                2,
                FastenerDirection.ClockwiseToTighten,
                insertOnInstall: false,
                blocksRemoval: true,
                ToolCompatibilityRule.Create("Wrench", FastenerSize.Millimeter10));
            try
            {
                var fastener = new FastenerInstance(definition);
                Assert.That(fastener.State, Is.EqualTo(FastenerState.Absent));
                Assert.That(fastener.TryInsert(), Is.True);
                Assert.That(fastener.State, Is.EqualTo(FastenerState.Inserted));
                Assert.That(fastener.TryAdvance(tighten: true), Is.True);
                Assert.That(fastener.State, Is.EqualTo(FastenerState.Loose));
                Assert.That(fastener.TryAdvance(tighten: true), Is.True);
                Assert.That(fastener.State, Is.EqualTo(FastenerState.PartiallyTightened));
                Assert.That(fastener.TryAdvance(tighten: true), Is.True);
                Assert.That(fastener.State, Is.EqualTo(FastenerState.Tightened));
                Assert.That(fastener.TryAdvance(tighten: false), Is.True);
                Assert.That(fastener.TryAdvance(tighten: false), Is.True);
                Assert.That(fastener.State, Is.EqualTo(FastenerState.Loose));
                Assert.That(fastener.TryRemove(), Is.True);
                Assert.That(fastener.State, Is.EqualTo(FastenerState.Absent));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void TightenedFastener_BlocksRemovalUntilFullyLoose()
        {
            InstallDrum();
            ToolDefinition tool = GetTool(FastenerSize.Millimeter14);
            FastenerInstance fastener = GetDrumFastener();
            controller.TryOperateFastener(drumMount.MountId, fastener.Definition.DefinitionId, tool, true);
            Assert.That(controller.EvaluateRemoval(drum).FailureReason, Is.EqualTo(AssemblyFailureReason.FastenerSecured));
            controller.TryOperateFastener(drumMount.MountId, fastener.Definition.DefinitionId, tool, false);
            Assert.That(controller.EvaluateRemoval(drum).Succeeded, Is.True);
        }

        [Test]
        public void InstalledWheel_BlocksRearDrumRemoval()
        {
            InstallDrum();
            PartInstance wheel = FindPart("vehicle.wheel_rl");
            MountPointAuthoring wheelMount = FindMountFor("vehicle.wheel_rl");
            MoveToMount(wheel, wheelMount);
            Assert.That(controller.TryInstall(wheel, wheelMount).Succeeded, Is.True);

            AssemblyOperationResult result = controller.EvaluateRemoval(drum);

            Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.RemovalBlocked));
        }

        [Test]
        public void Remove_ReleasesMountAndRestoresDynamicBody()
        {
            InstallDrum();

            AssemblyOperationResult result = controller.TryRemove(drum);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(drum.IsInstalled, Is.False);
            Assert.That(drum.Body.isKinematic, Is.False);
            Assert.That(controller.Graph.TryGetMount(drumMount.MountId, out MountPointRuntime mount), Is.True);
            Assert.That(mount.IsOccupied, Is.False);
        }

        [Test]
        public void SaveDto_JsonRoundTripRestoresPartMountAndFastener()
        {
            InstallDrum();
            FastenerInstance fastener = GetDrumFastener();
            ToolDefinition wrench14 = GetTool(FastenerSize.Millimeter14);
            controller.TryOperateFastener(drumMount.MountId, fastener.Definition.DefinitionId, wrench14, true);
            controller.TryOperateFastener(drumMount.MountId, fastener.Definition.DefinitionId, wrench14, true);
            string json = JsonUtility.ToJson(controller.CaptureSaveData());
            controller.TryOperateFastener(drumMount.MountId, fastener.Definition.DefinitionId, wrench14, false);
            controller.TryOperateFastener(drumMount.MountId, fastener.Definition.DefinitionId, wrench14, false);
            Assert.That(controller.TryRemove(drum).Succeeded, Is.True);

            VehicleAssemblySaveData restored = JsonUtility.FromJson<VehicleAssemblySaveData>(json);
            AssemblyOperationResult result = controller.RestoreSaveData(restored);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(drum.IsInstalled, Is.True);
            Assert.That(GetDrumFastener().Stage, Is.EqualTo(2));
        }

        [Test]
        public void SaveDto_RejectsDuplicatePartStateWithoutMutation()
        {
            VehicleAssemblySaveData data = controller.CaptureSaveData();
            data.parts[1].stableEntityId = data.parts[0].stableEntityId;

            AssemblyOperationResult result = controller.RestoreSaveData(data);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.InvalidSaveData));
            Assert.That(drum.IsInstalled, Is.False);
        }

        [Test]
        public void SaveDto_RejectsMountOccupancyMismatchAndMissingFastener()
        {
            VehicleAssemblySaveData mismatch = controller.CaptureSaveData();
            mismatch.mounts[0].installedPartStableEntityId = drum.StableId.Value;
            Assert.That(controller.RestoreSaveData(mismatch).FailureReason,
                Is.EqualTo(AssemblyFailureReason.InvalidSaveData));

            VehicleAssemblySaveData missingFastener = controller.CaptureSaveData();
            Array.Resize(ref missingFastener.fasteners, missingFastener.fasteners.Length - 1);
            Assert.That(controller.RestoreSaveData(missingFastener).FailureReason,
                Is.EqualTo(AssemblyFailureReason.InvalidSaveData));
        }

        [Test]
        public void Query_ReportsMissingUnsecuredAndCompleteness()
        {
            int initialMissing = controller.Query.GetMissingParts().Count;
            InstallDrum();

            Assert.That(controller.Query.GetMissingParts().Count, Is.EqualTo(initialMissing - 1));
            Assert.That(controller.Query.GetUnsecuredParts(), Does.Contain(drum));
            Assert.That(controller.Query.GetCompleteness01(), Is.InRange(0f, 1f));
            Assert.That(controller.Query.IsConnectionPointReady(drumMount.MountId), Is.True);
        }

        [Test]
        public void Validator_DetectsInstallDependencyCycle()
        {
            AssemblyDependency[] cycle = controller.Dependencies.Concat(new[]
            {
                AssemblyDependency.Create(
                    "vehicle.chassis",
                    "vehicle.trailing_arm_rl",
                    AssemblyDependencyKind.InstallRequiresInstalled)
            }).ToArray();

            IReadOnlyList<VehicleAssemblyValidationIssue> issues = VehicleAssemblyValidator.Validate(
                controller.Parts,
                controller.MountPoints,
                cycle,
                controller.Tools);

            Assert.That(issues.Any(issue => issue.Code == "DEPENDENCY-CYCLE"), Is.True);
        }

        [Test]
        public void RearDrumDefinition_PreservesReferenceProvenanceWithoutDonorAsset()
        {
            FastenerDefinition fastener = drumMount.Definition.Fasteners.Single();
            Assert.That(drumMount.Definition.ReferenceCandidateRadiusMeters, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(fastener.Size, Is.EqualTo(FastenerSize.Millimeter14));
            Assert.That(fastener.MaximumStage, Is.EqualTo(8));
            string prefabPath = UnityEditor.AssetDatabase.GetAssetPath(drum.Definition.VisualPrefab)
                .Replace('\\', '/');
            Assert.That(prefabPath, Does.StartWith("Assets/Game/Vehicle/Content/Assembly/Prefabs/"));
            Assert.That(prefabPath, Does.Not.Contain("LegacyImport"));
        }

        private void InstallDrum()
        {
            MoveToMount(drum, drumMount);
            Assert.That(controller.TryInstall(drum, drumMount).Succeeded, Is.True);
        }

        private void MoveToMount(PartInstance part, MountPointAuthoring mount)
        {
            part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            Physics.SyncTransforms();
        }

        private PartInstance FindPart(string definitionId)
        {
            return controller.Parts.Single(part => part.Definition.DefinitionId == definitionId);
        }

        private MountPointAuthoring FindMountFor(string partDefinitionId)
        {
            return controller.MountPoints.Single(mount => mount.Definition.AcceptsPart(partDefinitionId));
        }

        private FastenerInstance GetDrumFastener()
        {
            controller.Graph.TryGetMount(drumMount.MountId, out MountPointRuntime mount);
            return mount.Fasteners.Single();
        }

        private ToolDefinition GetTool(FastenerSize size)
        {
            return controller.Tools.Single(tool => tool.Size == size);
        }
    }
}
