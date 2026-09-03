using System.Collections;
using System.Linq;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Player;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    public sealed class VehicleAssemblyPlayModeTests
    {
        private VehicleAssemblyController controller;
        private PartInstance drum;
        private MountPointAuthoring drumMount;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("VehicleAssemblyPrototype", LoadSceneMode.Single);
            while (!load.isDone)
            {
                yield return null;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            controller = Object
                .FindObjectsByType<VehicleAssemblyController>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(value => value.gameObject.scene == activeScene);
            controller.Initialize();
            drum = FindPart("vehicle.brake_drum_rl");
            drumMount = FindMountFor("vehicle.brake_drum_rl");
        }

        [UnityTest]
        public IEnumerator SceneBoots_WithM4PlayerAndAssemblyComposition()
        {
            yield return null;

            Assert.That(controller, Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PlayerInteractionController>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<PhysicalCarryController>(), Is.Not.Null);
            CrossdotPresenter crossdot = Object.FindFirstObjectByType<CrossdotPresenter>();
            Assert.That(crossdot, Is.Not.Null);
            Assert.That(crossdot.Visible, Is.True);
            Assert.That(controller.Parts, Has.Length.EqualTo(15));
            Assert.That(FindPart("vehicle.trailing_arm_rl").IsInstalled, Is.True);
        }

        [UnityTest]
        public IEnumerator M4CarryHandoff_SmoothlyInstallsRepresentativeDrum()
        {
            PhysicalCarryController carry = Object.FindFirstObjectByType<PhysicalCarryController>();
            InteractionContext context = CreateContext(carry.gameObject);
            Vector3 scaleBeforeInstall = drum.transform.lossyScale;
            Renderer[] visualRenderers = drum.GetComponentsInChildren<Renderer>(true);
            Assert.That(visualRenderers, Is.Not.Empty);
            Assert.That(carry.TryPickup(drum.PickupTarget, context), Is.True);
            Vector3 releasedPosition =
                drumMount.Pose.position + Vector3.up * 0.15f;
            drum.transform.SetPositionAndRotation(
                releasedPosition,
                drumMount.Pose.rotation);
            Physics.SyncTransforms();
            AssemblyMountHandoffTarget target = drumMount.GetComponent<AssemblyMountHandoffTarget>();

            Assert.That(carry.TryHandoff(target, context), Is.True, target.HandoffPrompt);
            Assert.That(carry.HasHeldObject, Is.False);
            Assert.That(drum.IsInstalled, Is.False);
            Assert.That(
                Vector3.Distance(drum.transform.position, releasedPosition),
                Is.LessThan(0.01f));

            yield return new WaitForSeconds(0.4f);
            yield return new WaitForFixedUpdate();

            Assert.That(drum.IsInstalled, Is.True);
            Assert.That(drum.Body.isKinematic, Is.True);
            Assert.That(drum.transform.lossyScale.x, Is.EqualTo(scaleBeforeInstall.x).Within(0.001f));
            Assert.That(drum.transform.lossyScale.y, Is.EqualTo(scaleBeforeInstall.y).Within(0.001f));
            Assert.That(drum.transform.lossyScale.z, Is.EqualTo(scaleBeforeInstall.z).Within(0.001f));
            Assert.That(
                visualRenderers.Any(renderer =>
                    renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy),
                Is.True);
        }

        [UnityTest]
        public IEnumerator FastenerTarget_ProgressesAndReversesObservedStages()
        {
            InstallDrumDirect();
            AssemblyFastenerInteractionTarget target =
                drumMount
                    .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .Single(value => value.MountId == drumMount.MountId);
            InteractionContext context = CreateContext(target.gameObject);
            for (int i = 0; i < 8; i++)
            {
                target.ActivateTool(context);
                Assert.That(
                    GetDrumFastener().Stage,
                    Is.EqualTo(i + 1),
                    $"Fastener interaction diverged at turn {i + 1}: " +
                    controller.LastOperationResult.Message);
            }

            Assert.That(GetDrumFastener().Stage, Is.EqualTo(8));
            Assert.That(GetDrumFastener().State, Is.EqualTo(FastenerState.Tightened));
            target.ActivateTool(context);
            yield return null;

            Assert.That(GetDrumFastener().Stage, Is.EqualTo(7));
        }

        [UnityTest]
        public IEnumerator FastenerTarget_MouseWheelDirectionTightensAndLoosensWithMatchingWrench()
        {
            InstallDrumDirect();
            AssemblyFastenerInteractionTarget target = drumMount
                .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                .Single(value => value.MountId == drumMount.MountId);
            FastenerInstance fastener = GetDrumFastener();
            var heldTool = new TestHeldToolIdentity(
                "Wrench",
                ((int)fastener.Definition.Size).ToString());
            InteractionContext context = CreateContext(target.gameObject);
            Assert.That(
                target.TryActivateHeldTool(
                    heldTool,
                    context,
                    1f),
                Is.True);
            Assert.That(fastener.Stage, Is.EqualTo(1));
            Assert.That(
                target.TryActivateHeldTool(
                    heldTool,
                    context,
                    -1f),
                Is.True);
            Assert.That(
                fastener.Stage,
                Is.EqualTo(1),
                "Extra wheel notches must be consumed until the current " +
                "BetterMSC work/regrip cycle finishes.");

            yield return new WaitForSecondsRealtime(0.4f);

            Assert.That(
                target.TryActivateHeldTool(
                    heldTool,
                    context,
                    -1f),
                Is.True);
            Assert.That(fastener.Stage, Is.Zero);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        [UnityTest]
        public IEnumerator FastenerTarget_IsRaycastableOnlyWhileItsPartIsInstalled()
        {
            AssemblyFastenerInteractionTarget target =
                drumMount
                    .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
                    .Single(value => value.MountId == drumMount.MountId);
            Collider targetCollider = target.GetComponent<Collider>();
            yield return null;

            Assert.That(targetCollider, Is.Not.Null);
            Assert.That(targetCollider.enabled, Is.False);

            InstallDrumDirect();
            yield return null;
            Assert.That(targetCollider.enabled, Is.True);

            Assert.That(controller.TryRemove(drum).Succeeded, Is.True);
            yield return null;
            Assert.That(targetCollider.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator WrongTool_DoesNotMutateFastener()
        {
            InstallDrumDirect();
            ToolDefinition wrench11 = controller.Tools.Single(tool => tool.Size == FastenerSize.Millimeter11);
            AssemblyOperationResult result = controller.TryOperateFastener(
                drumMount.MountId,
                GetDrumFastener().Definition.DefinitionId,
                wrench11,
                tighten: true);
            yield return null;

            Assert.That(result.FailureReason, Is.EqualTo(AssemblyFailureReason.InvalidTool));
            Assert.That(GetDrumFastener().Stage, Is.Zero);
        }

        [UnityTest]
        public IEnumerator InstalledWheel_BlocksDrumRemovalFlow()
        {
            InstallDrumDirect();
            PartInstance wheel = FindPart("vehicle.wheel_rl");
            MountPointAuthoring wheelMount = FindMountFor("vehicle.wheel_rl");
            wheel.transform.SetPositionAndRotation(wheelMount.Pose.position, wheelMount.Pose.rotation);
            Physics.SyncTransforms();
            Assert.That(controller.TryInstall(wheel, wheelMount).Succeeded, Is.True);
            yield return null;

            AssemblyOperationResult removal = controller.TryRemove(drum);
            Assert.That(removal.FailureReason, Is.EqualTo(AssemblyFailureReason.RemovalBlocked));
            Assert.That(drum.IsInstalled, Is.True);
        }

        [UnityTest]
        public IEnumerator RemoveFlow_RestoresDynamicPickupReadyState()
        {
            InstallDrumDirect();
            Assert.That(controller.TryRemove(drum).Succeeded, Is.True);
            yield return new WaitForFixedUpdate();

            Assert.That(drum.IsInstalled, Is.False);
            Assert.That(drum.Body.isKinematic, Is.False);
            Assert.That(drum.PickupTarget.CanPickup(CreateContext(drum.gameObject)), Is.True);
        }

        [UnityTest]
        public IEnumerator InstalledParts_FollowMovedAssemblyRootAsOnePhysicalAggregate()
        {
            InstallDrumDirect();
            PartInstance trailingArm = FindPart("vehicle.trailing_arm_rl");
            PartInstance assemblyRoot = controller.Parts.Single(part => part.IsAssemblyRoot);
            Vector3 drumLocalPosition = assemblyRoot.transform.InverseTransformPoint(
                drum.transform.position);
            Vector3 armLocalPosition = assemblyRoot.transform.InverseTransformPoint(
                trailingArm.transform.position);
            Quaternion rotationDelta = Quaternion.Euler(0f, 31f, 0f);
            assemblyRoot.transform.SetPositionAndRotation(
                assemblyRoot.transform.position + new Vector3(2.4f, 0.35f, -1.2f),
                rotationDelta * assemblyRoot.transform.rotation);

            controller.SynchronizeInstalledParts();
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(
                Vector3.Distance(
                    drum.transform.position,
                    assemblyRoot.transform.TransformPoint(drumLocalPosition)),
                Is.LessThan(0.002f));
            Assert.That(
                Vector3.Distance(
                    trailingArm.transform.position,
                    assemblyRoot.transform.TransformPoint(armLocalPosition)),
                Is.LessThan(0.002f));
            Assert.That(
                Vector3.Distance(drum.Body.position, drum.transform.position),
                Is.LessThan(0.0001f));
            Assert.That(
                Vector3.Distance(
                    trailingArm.Body.position,
                    trailingArm.transform.position),
                Is.LessThan(0.0001f));
        }

        [UnityTest]
        public IEnumerator MountPreview_ShowsValidFeedbackWithoutPerFrameSearch()
        {
            PhysicalCarryController carry = Object.FindFirstObjectByType<PhysicalCarryController>();
            AssemblyMountPreviewPresenter preview = Object.FindFirstObjectByType<AssemblyMountPreviewPresenter>();
            InteractionContext context = CreateContext(carry.gameObject);
            Transform carryAnchor = carry.GetComponentsInChildren<Transform>(true)
                .Single(transform => transform.name == "CarryAnchor");
            carryAnchor.position = drumMount.Pose.position;
            carryAnchor.rotation = drumMount.Pose.rotation;
            Assert.That(carry.TryPickup(drum.PickupTarget, context), Is.True);
            drum.transform.SetPositionAndRotation(drumMount.Pose.position, drumMount.Pose.rotation);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;

            Assert.That(preview.LastPreviewMessage, Does.Contain("Установить"));
            Assert.That(controller.CandidateQueryCount, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator SaveRoundTrip_RestoresInstalledPartAndStage()
        {
            InstallDrumDirect();
            FastenerInstance fastener = GetDrumFastener();
            ToolDefinition wrench14 = controller.Tools.Single(tool => tool.Size == FastenerSize.Millimeter14);
            controller.TryOperateFastener(drumMount.MountId, fastener.Definition.DefinitionId, wrench14, true);
            VehicleAssemblySaveData data = controller.CaptureSaveData();
            controller.TryOperateFastener(drumMount.MountId, fastener.Definition.DefinitionId, wrench14, false);
            controller.TryRemove(drum);
            Assert.That(drum.IsInstalled, Is.False);

            Assert.That(controller.RestoreSaveData(data).Succeeded, Is.True);
            yield return null;

            Assert.That(drum.IsInstalled, Is.True);
            Assert.That(GetDrumFastener().Stage, Is.EqualTo(1));
        }

        private void InstallDrumDirect()
        {
            drum.transform.SetPositionAndRotation(drumMount.Pose.position, drumMount.Pose.rotation);
            Physics.SyncTransforms();
            Assert.That(controller.TryInstall(drum, drumMount).Succeeded, Is.True);
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

        private static InteractionContext CreateContext(GameObject interactor)
        {
            return new InteractionContext(interactor, interactor.transform.position, interactor.transform.forward);
        }

        private sealed class TestHeldToolIdentity : IHeldToolIdentity
        {
            public TestHeldToolIdentity(string toolType, string toolVariant)
            {
                ToolType = toolType;
                ToolVariant = toolVariant;
            }

            public string ToolType { get; }

            public string ToolVariant { get; }
        }
    }
}
