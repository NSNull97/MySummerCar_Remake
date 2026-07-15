using System.Collections;
using System.Linq;
using MSC.Interaction;
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

            controller = Object.FindFirstObjectByType<VehicleAssemblyController>();
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
        public IEnumerator M4CarryHandoff_InstallsRepresentativeDrum()
        {
            PhysicalCarryController carry = Object.FindFirstObjectByType<PhysicalCarryController>();
            InteractionContext context = CreateContext(carry.gameObject);
            Vector3 scaleBeforeInstall = drum.transform.lossyScale;
            Renderer[] visualRenderers = drum.GetComponentsInChildren<Renderer>(true);
            Assert.That(visualRenderers, Is.Not.Empty);
            Assert.That(carry.TryPickup(drum.PickupTarget, context), Is.True);
            drum.transform.SetPositionAndRotation(drumMount.Pose.position, drumMount.Pose.rotation);
            Physics.SyncTransforms();
            AssemblyMountHandoffTarget target = drumMount.GetComponent<AssemblyMountHandoffTarget>();

            Assert.That(carry.TryHandoff(target, context), Is.True, target.HandoffPrompt);
            yield return new WaitForFixedUpdate();

            Assert.That(carry.HasHeldObject, Is.False);
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
                drumMount.GetComponentInChildren<AssemblyFastenerInteractionTarget>(true);
            InteractionContext context = CreateContext(target.gameObject);
            for (int i = 0; i < 8; i++)
            {
                target.ActivateTool(context);
            }

            Assert.That(GetDrumFastener().Stage, Is.EqualTo(8));
            Assert.That(GetDrumFastener().State, Is.EqualTo(FastenerState.Tightened));
            target.ActivateTool(context);
            yield return null;

            Assert.That(GetDrumFastener().Stage, Is.EqualTo(7));
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
    }
}
