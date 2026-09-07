using System.Collections;
using System.Linq;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Interaction.Query;
using MSC.LegacyImport;
using MSC.Player;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.PlayerInteraction
{
    public sealed class SatsumaCabinControlFramePlayModeTests : InputTestFixture
    {
        private GameObject vehicle;
        private GameObject player;
        private GameObject looseRoot;
        private PartInstance[] parts;
        private InputActionAsset actions;
        private float oldTimeScale;
        private VehicleAssemblyController assembly;
        private RaycastInteractionCandidateSource query;
        private PlayerInteractionController interaction;

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (player != null) Object.Destroy(player);
            if (parts != null)
                foreach (PartInstance part in parts)
                    if (part != null && (vehicle == null || !part.transform.IsChildOf(vehicle.transform))) Object.Destroy(part.gameObject);
            if (vehicle != null) Object.Destroy(vehicle);
            if (looseRoot != null) Object.Destroy(looseRoot);
            if (actions != null) Object.Destroy(actions);
            yield return null;
            yield return null;
            Time.timeScale = oldTimeScale;
        }

        [UnityTest]
        public IEnumerator InstalledUnpoweredWiperKnobReceivesCabinRayAndThreeMouseClicks()
        {
            CreateFixture();
            RestoreInstalled("vehicle.satsuma.part.dashboard", "mount.satsuma.dashboard");
            RestoreInstalled("vehicle.satsuma.part.dashboard-meters", "mount.satsuma.dashboard.meters");
            DisableUnrelatedInventory();
            var wipers = vehicle.GetComponent<SatsumaWiperController>();
            var target = wipers.SwitchKnob.GetComponent<SatsumaWiperSwitchInteractionTarget>();
            Assert.That(wipers.HasSwitchKnobBaseLocalRotation, Is.True, "Run the scoped cabin-frame refresh first.");
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
            for (int index = 0; index < 3; index++)
            {
                AimFromCabin(target.GetComponent<SphereCollider>());
                Assert.That(interaction.CurrentCandidate.TryGetCapability(out IContextInteractionTarget contextTarget), Is.True);
                Assert.That(contextTarget, Is.SameAs(target), DescribeRay());
                Assert.That(target.CanInteract(default), Is.True);
                Press(mouse.leftButton);
                yield return null;
                Release(mouse.leftButton);
                yield return null;
                Assert.That((int)wipers.Mode, Is.EqualTo((index + 1) % 3));
                Assert.That(wipers.IsCycleActive, Is.False, "The physical switch works without electrical power.");
                Quaternion expected = wipers.SwitchKnobBaseLocalRotation * Quaternion.AngleAxis(-45f * (int)wipers.Mode, Vector3.up);
                Assert.That(Mathf.Abs(Quaternion.Dot(wipers.SwitchKnob.localRotation, expected)), Is.GreaterThan(.999999f));
            }
        }

        [UnityTest]
        public IEnumerator ActualHoodHandleIsReachableFromCabinWithoutInstalledHood()
        {
            CreateFixture();
            RestoreInstalled("vehicle.satsuma.part.dashboard", "mount.satsuma.dashboard");
            DisableUnrelatedInventory();
            var release = parts.Single(value => value.Definition != null &&
                value.Definition.DefinitionId == "vehicle.satsuma.part.dashboard")
                .GetComponentsInChildren<AssemblyHoodReleaseInteractionTarget>(true).Single();
            Assert.That(release.UsesReviewedHandlePull, Is.True, "Run the scoped cabin-frame refresh first.");
            yield return null;
            AimFromCabin(release.GetComponent<SphereCollider>());
            Assert.That(interaction.CurrentCandidate.TryGetCapability(out IContextInteractionTarget target), Is.True);
            Assert.That(target, Is.SameAs(release), DescribeRay());
            Assert.That(release.CanInteract(default), Is.True, "A missing hood must not disable the handle itself.");
            Assert.That(release.LeverVisual, Is.Not.SameAs(release.transform));
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            Press(mouse.leftButton);
            yield return null;
            Assert.That(release.CanInteract(default), Is.False, "An active pull must not overlap another pull.");
            Release(mouse.leftButton);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InstalledUnpoweredChokeReceivesCabinMouseHoldReleaseAndReturn()
        {
            SatsumaDashboardControlsController controls = CreateInstalledDashboardControlsFixture();
            SatsumaDashboardControlInteractionTarget target = FindDashboardControlTarget(SatsumaDashboardControlKind.Choke);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
            Assert.That(controls.Choke01, Is.Zero);
            Vector3 restPosition = controls.ChokeKnob.localPosition;
            Quaternion restRotation = controls.ChokeKnob.localRotation;
            Vector3 markerPosition = target.transform.localPosition;
            Quaternion markerRotation = target.transform.localRotation;
            AimFromCabin(target.GetComponent<SphereCollider>());
            AssertDashboardCandidate(target);

            Press(mouse.leftButton);
            yield return null;
            yield return new WaitForSecondsRealtime(.12f);
            Assert.That(controls.ChokeHeldDirection, Is.EqualTo(1));
            Assert.That(controls.Choke01, Is.GreaterThan(0f));
            Vector3 expected = restPosition + restRotation * new Vector3(
                0f, -SatsumaDashboardControlsController.ChokeTravelMeters * controls.Choke01, 0f);
            Assert.That(Vector3.Distance(controls.ChokeKnob.localPosition, expected), Is.LessThan(.00001f));
            Assert.That(Mathf.Abs(Quaternion.Dot(controls.ChokeKnob.localRotation, restRotation)), Is.GreaterThan(.999999f));
            Assert.That(Vector3.Distance(target.transform.localPosition, markerPosition), Is.LessThan(.000001f));
            Assert.That(Mathf.Abs(Quaternion.Dot(target.transform.localRotation, markerRotation)), Is.GreaterThan(.999999f));

            Release(mouse.leftButton);
            yield return null;
            Assert.That(controls.ChokeHeldDirection, Is.Zero);
            float releasedLevel = controls.Choke01;
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(controls.Choke01, Is.EqualTo(releasedLevel).Within(.000001f),
                "Releasing the physical knob keeps its setting instead of springing back.");

            AimFromCabin(target.GetComponent<SphereCollider>());
            AssertDashboardCandidate(target);
            Press(mouse.rightButton);
            yield return null;
            Assert.That(controls.ChokeHeldDirection, Is.EqualTo(-1));
            float deadline = Time.realtimeSinceStartup + 1f;
            while (controls.Choke01 > 0f && Time.realtimeSinceStartup < deadline) yield return null;
            Release(mouse.rightButton);
            yield return null;
            Assert.That(controls.Choke01, Is.Zero, "The actual RMB hold must push the knob fully home.");
            Assert.That(controls.ChokeHeldDirection, Is.Zero);
            Assert.That(Vector3.Distance(controls.ChokeKnob.localPosition, restPosition), Is.LessThan(.00001f));
            Assert.That(Mathf.Abs(Quaternion.Dot(controls.ChokeKnob.localRotation, restRotation)), Is.GreaterThan(.999999f));
            Assert.That(controls.Electrical.ElectricsOk, Is.False);
        }

        [UnityTest]
        public IEnumerator InstalledUnpoweredHazardsReceiveTwoCabinMouseClicksWithoutHoldRepeats()
        {
            SatsumaDashboardControlsController controls = CreateInstalledDashboardControlsFixture();
            SatsumaDashboardControlInteractionTarget target = FindDashboardControlTarget(SatsumaDashboardControlKind.Hazards);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
            Assert.That(controls.HazardsOn, Is.False);
            Vector3 restPosition = controls.HazardKnob.localPosition;
            Quaternion restRotation = controls.HazardKnob.localRotation;
            for (int index = 0; index < 2; index++)
            {
                bool expectedOn = index == 0;
                AimFromCabin(target.GetComponent<SphereCollider>());
                AssertDashboardCandidate(target);
                Press(mouse.leftButton);
                yield return null;
                Assert.That(controls.HazardsOn, Is.EqualTo(expectedOn));
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(controls.HazardsOn, Is.EqualTo(expectedOn), "Holding LMB must not toggle every frame.");
                Release(mouse.leftButton);
                yield return null;
                Quaternion expected = restRotation * Quaternion.AngleAxis(expectedOn ? -45f : 0f, Vector3.up);
                Assert.That(Mathf.Abs(Quaternion.Dot(controls.HazardKnob.localRotation, expected)), Is.GreaterThan(.999999f));
                Assert.That(Vector3.Distance(controls.HazardKnob.localPosition, restPosition), Is.LessThan(.000001f));
                Assert.That(controls.Electrical.ElectricsOk, Is.False);
                Assert.That(vehicle.GetComponent<SatsumaDashboardLightingPresenter>().Lamps.All(lamp => !lamp.Light.enabled), Is.True,
                    "Mechanical hazard intent must not invent electrical power or lit lamps.");
            }
        }

        [UnityTest]
        public IEnumerator InstalledUnpoweredLightsReceiveThreeCabinMouseClicksAndKeepKnobBaseFrame()
        {
            SatsumaDashboardControlsController controls = CreateInstalledDashboardControlsFixture();
            SatsumaDashboardControlInteractionTarget target = FindDashboardControlTarget(SatsumaDashboardControlKind.Lights);
            Mouse mouse = InputSystem.AddDevice<Mouse>();
            yield return null;
            Assert.That(controls.HeadlightsMode, Is.EqualTo(SatsumaHeadlightsMode.Off));
            Vector3 restPosition = controls.LightsKnob.localPosition;
            Quaternion restRotation = controls.LightsKnob.localRotation;
            for (int index = 0; index < 3; index++)
            {
                var expectedMode = (SatsumaHeadlightsMode)((index + 1) % 3);
                AimFromCabin(target.GetComponent<SphereCollider>());
                AssertDashboardCandidate(target);
                Press(mouse.leftButton);
                yield return null;
                Assert.That(controls.HeadlightsMode, Is.EqualTo(expectedMode));
                Assert.That(target.InteractionPrompt, Is.EqualTo(expectedMode switch
                {
                    SatsumaHeadlightsMode.Off => "Выключено · ЛКМ — габариты",
                    SatsumaHeadlightsMode.Parking => "Габариты · ЛКМ — фары",
                    _ => "Фары · ЛКМ — выключить",
                }));
                yield return new WaitForSecondsRealtime(.08f);
                Assert.That(controls.HeadlightsMode, Is.EqualTo(expectedMode), "Holding LMB must not skip switch positions.");
                Release(mouse.leftButton);
                yield return null;
                Quaternion expected = restRotation * Quaternion.AngleAxis(-45f * (int)expectedMode, Vector3.up);
                Assert.That(Mathf.Abs(Quaternion.Dot(controls.LightsKnob.localRotation, expected)), Is.GreaterThan(.999999f));
                Assert.That(Vector3.Distance(controls.LightsKnob.localPosition, restPosition), Is.LessThan(.000001f));
                Assert.That(controls.Electrical.ElectricsOk, Is.False);
                Assert.That(vehicle.GetComponent<SatsumaDashboardLightingPresenter>().Lamps.All(lamp => !lamp.Light.enabled), Is.True,
                    "Switch selection is mechanical; actual light output still needs its circuits and bulbs.");
            }
        }

        [UnityTest]
        public IEnumerator BothDoorInstallRaysUseDonorAimWhileFinalPoseRemainsTheHinge()
        {
            CreateFixture();
            yield return null;
            foreach (string side in new[] { "left", "right" })
            {
                PartInstance door = parts.Single(value => value.Definition.DefinitionId == "vehicle.satsuma.part.door-" + side);
                MountPointAuthoring mount = assembly.MountPoints.Single(value => value.MountId == "mount.satsuma.door-" + side);
                var anchor = mount.GetComponent<AssemblyMountInteractionAnchor>();
                Assert.That(anchor, Is.Not.Null, "Run the scoped mount-frame refresh first.");
                var pickup = door.GetComponent<PhysicsPickupTarget>();
                Vector3 aim = anchor.WorldPosition;
                door.transform.position = aim;
                door.transform.rotation = Quaternion.Euler(40f, 110f, -35f);
                pickup.Body.position = door.transform.position;
                pickup.Body.rotation = door.transform.rotation;
                query.SetCarriedObjectTargetsEnabled(true);
                query.SetCarriedObjectTarget(pickup);
                query.SetIgnoredBody(pickup.Body);
                player.transform.position = aim + vehicle.transform.TransformDirection(new Vector3(side == "left" ? -.7f : .7f, .12f, -.1f));
                player.transform.LookAt(aim);
                Physics.SyncTransforms();
                InteractionCandidate candidate = query.Query();
                Assert.That(candidate.TryGetCapability(out IMountHandoffTarget handoff), Is.True, DescribeRay());
                var context = new InteractionContext(player, player.transform.position, player.transform.forward);
                Assert.That(handoff.CanAccept(pickup, context), Is.True, handoff.HandoffPrompt);
                Assert.That(assembly.EvaluateHandoffInstall(door, mount).Succeeded, Is.True);
                AssemblyOperationResult result = assembly.TryInstallFromHandoff(pickup, mount);
                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(door.RuntimeState.InstalledMountId, Is.EqualTo(mount.MountId));
                Assert.That(Vector3.Distance(door.transform.position, mount.Pose.position), Is.LessThan(.0001f));
                Assert.That(Vector3.Distance(door.transform.position, aim), Is.GreaterThan(.54f));
            }
        }

        [UnityTest]
        public IEnumerator DisablingDashboardLightingClearsEveryOutputThroughRealLifecycle()
        {
            CreateFixture();
            yield return null;
            var lighting = vehicle.GetComponent<SatsumaDashboardLightingPresenter>();
            Assert.That(lighting, Is.Not.Null);
            Assert.That(lighting.Lamps.Length, Is.GreaterThan(0));
            // Cleanup contract is independent of electrical availability. The
            // detailed electrical and blink gates are tested in EditMode.
            foreach (var lamp in lighting.Lamps) lamp.Light.enabled = true;
            lighting.enabled = false;
            Assert.That(lighting.Lamps.All(lamp => !lamp.Light.enabled), Is.True);
        }

        private void CreateFixture()
        {
            oldTimeScale = Time.timeScale;
            Time.timeScale = 0f; // Input remains live; no unrelated car settling in a control-ray test.
            GameObject prefab = Resources.Load<GameObject>("Phase1Vehicles/Satsuma_Phase1_V1a");
            Assert.That(prefab, Is.Not.Null);
            vehicle = Object.Instantiate(prefab, new Vector3(20f, 80f, 20f), Quaternion.identity);
            looseRoot = vehicle.GetComponent<LegacySatsumaLoosePartsRoot>().LoosePartsRoot.gameObject;
            assembly = vehicle.GetComponent<VehicleAssemblyController>();
            parts = assembly.Parts.ToArray();
            assembly.Initialize();
            var simulation = vehicle.GetComponent<VehicleSimulationHost>();
            simulation.enabled = false;
            Assert.That(simulation.TryInitialize(out string failure), Is.True, failure);
            vehicle.GetComponent<Rigidbody>().useGravity = false;
            player = new GameObject("Cabin input-ray fixture");
            player.SetActive(false);
            Transform carryAnchor = new GameObject("Carry anchor").transform;
            carryAnchor.SetParent(player.transform, false);
            carryAnchor.localPosition = Vector3.forward;
            var carry = player.AddComponent<PhysicalCarryController>();
            carry.Configure(carryAnchor, null);
            query = player.AddComponent<RaycastInteractionCandidateSource>();
            query.Configure(player.transform, 2.25f, ~0);
            interaction = player.AddComponent<PlayerInteractionController>();
            interaction.Configure(query, carry, player.transform, ~0);
            actions = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = new InputActionMap("Player");
            actions.AddActionMap(map);
            map.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            map.AddAction("Look", InputActionType.Value, expectedControlLayout: "Vector2");
            map.AddAction("RotateModifier", InputActionType.Value, "<Mouse>/scroll/y");
            string[] buttons = { "Crouch", "Run", "Jump", "Zoom", "ForwardLean", "Interact", "Drop", "Place", "Throw", "RotateAxis", "ToolActivate", "Urinate", "Wave", "MiddleFinger", "Swear", "AlternativeActions" };
            foreach (string button in buttons) map.AddAction(button, InputActionType.Button);
            map.FindAction("Interact").AddBinding("<Mouse>/leftButton");
            map.FindAction("Throw").AddBinding("<Mouse>/rightButton");
            map.FindAction("ToolActivate").AddBinding("<Keyboard>/f");
            player.AddComponent<PlayerInputRouter>().Configure(actions, null, null, interaction);
            player.SetActive(true);
        }

        private SatsumaDashboardControlsController CreateInstalledDashboardControlsFixture()
        {
            CreateFixture();
            RestoreInstalled("vehicle.satsuma.part.dashboard", "mount.satsuma.dashboard");
            RestoreInstalled("vehicle.satsuma.part.dashboard-meters", "mount.satsuma.dashboard.meters");
            DisableUnrelatedInventory();
            var controls = vehicle.GetComponent<SatsumaDashboardControlsController>();
            Assert.That(controls, Is.Not.Null, "Run the scoped dashboard-control refresh first.");
            Assert.That(controls.CanOperate, Is.True);
            Assert.That(controls.Electrical.ElectricsOk, Is.False, "This fixture deliberately leaves the battery/wiring absent.");
            return controls;
        }

        private SatsumaDashboardControlInteractionTarget FindDashboardControlTarget(SatsumaDashboardControlKind kind) =>
            vehicle.GetComponentsInChildren<SatsumaDashboardControlInteractionTarget>(true).Single(target => target.Kind == kind);

        private void AssertDashboardCandidate(SatsumaDashboardControlInteractionTarget expected)
        {
            Assert.That(interaction.CurrentCandidate.TryGetCapability(out IContinuousContextInteractionTarget target), Is.True, DescribeRay());
            Assert.That(target, Is.SameAs(expected), DescribeRay());
        }

        private void RestoreInstalled(string partId, string mountId)
        {
            VehicleAssemblySaveData data = assembly.CaptureSaveData();
            MountPointAuthoring mount = assembly.MountPoints.Single(value => value.MountId == mountId);
            PartSaveDto part = data.parts.Single(value => value.partDefinitionId == partId);
            part.lifecycleState = PartLifecycleState.Installed;
            part.installedMountId = mountId;
            part.worldPosition = mount.Pose.position;
            part.worldRotation = mount.Pose.rotation;
            data.mounts.Single(value => value.mountId == mountId).installedPartStableEntityId = part.stableEntityId;
            foreach (FastenerSaveDto fastener in data.fasteners.Where(value => value.mountId == mountId))
            {
                fastener.inserted = true;
                fastener.seated = true;
                fastener.stage = mount.Definition.Fasteners.Single(value => value.DefinitionId == fastener.fastenerDefinitionId).MaximumStage;
            }
            Assert.That(assembly.Graph.TryGetMount(mountId, out MountPointRuntime runtimeMount), Is.True);
            FastenerGroupDefinition group = runtimeMount.FastenerGroup.Definition;
            int tightness = Mathf.Clamp(data.fasteners.Where(value => value.mountId == mountId &&
                    group.FastenerDefinitionIds.Contains(value.fastenerDefinitionId)).Sum(value => value.stage),
                0, group.AggregateMaximumTightness);
            // Dashboard itself has no bolts; meters have their own real group.
            // Installed/fully safe must not fabricate an IsBolted latch for an
            // empty group, which native save validation correctly rejects.
            bool bolted = group.HasFasteners && tightness >= group.BoltedOnThreshold;
            Assert.That(group.IsLatchConsistent(tightness, bolted, true), Is.True, mountId);
            data.fastenerGroups.Single(value => value.mountId == mountId).isBolted = bolted;
            AssemblyOperationResult result = assembly.RestoreSaveData(data);
            Assert.That(result.Succeeded, Is.True, result.Message);
            Assert.That(runtimeMount.FastenerGroup.IsBolted, Is.EqualTo(bolted));
        }

        private void DisableUnrelatedInventory()
        {
            foreach (PartInstance part in parts)
                if (!part.IsAssemblyRoot && !part.IsInstalled) part.gameObject.SetActive(false);
        }
        private void AimFromCabin(SphereCollider target)
        {
            player.transform.position = vehicle.transform.TransformPoint(new Vector3(-.25f, .68f, .02f));
            player.transform.LookAt(target.transform.TransformPoint(target.center));
            Physics.SyncTransforms();
            interaction.RefreshCandidate();
        }
        private string DescribeRay() => "target=" + (query.Query().Host != null ? query.Query().Host.name : "none") +
            ", origin=" + player.transform.position + ", hit=" + (query.HasLastHit ? query.LastHit.collider.name : "none");
    }
}
