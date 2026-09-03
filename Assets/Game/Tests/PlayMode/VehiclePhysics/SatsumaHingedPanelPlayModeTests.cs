using System.Collections;
using System.Linq;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Query;
using MSC.Vehicle.Assembly;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class SatsumaHingedPanelPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator FlushDeferredDestruction()
        {
            yield return null;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator InstalledDoorUsesPhysicalTorqueInertiaLatchAndDetachment()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = Object.Instantiate(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassisBody = instance.GetComponent<Rigidbody>();
                PartInstance door = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.door-left");
                MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                    value.MountId == "mount.satsuma.door-left");

                foreach (PartInstance otherPart in assembly.Parts)
                {
                    if (otherPart != null && otherPart != door &&
                        !otherPart.IsAssemblyRoot && !otherPart.IsInstalled)
                    {
                        otherPart.gameObject.SetActive(false);
                    }
                }

                chassisBody.useGravity = false;
                chassisBody.linearVelocity = Vector3.zero;
                chassisBody.angularVelocity = Vector3.zero;
                door.transform.SetPositionAndRotation(
                    mount.Pose.position,
                    mount.Pose.rotation);
                door.Body.position = mount.Pose.position;
                door.Body.rotation = mount.Pose.rotation;
                Physics.SyncTransforms();
                Vector3 chassisStartPosition = chassisBody.position;

                AssemblyOperationResult result = assembly.TryInstall(
                    door,
                    mount);
                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(door.Body.isKinematic, Is.False);
                Assert.That(door.Body.useGravity, Is.True);
                AssemblyInstalledPhysicsLink physicsLink = door
                    .GetComponent<AssemblyInstalledPhysicsLink>();
                Assert.That(physicsLink, Is.Not.Null);
                Assert.That(
                    physicsLink.LinkMode,
                    Is.EqualTo(
                        AssemblyInstalledPhysicsLinkMode.OperablePanelHinge));
                Assert.That(physicsLink.InstalledHinge, Is.Not.Null);
                Assert.That(
                    physicsLink.InstalledHinge.breakForce,
                    Is.EqualTo(float.PositiveInfinity));
                Assert.That(
                    physicsLink.InstalledHinge.breakTorque,
                    Is.EqualTo(float.PositiveInfinity),
                    "The assembly graph, not a legacy PhysX impulse " +
                    "threshold, owns unfastened full-open detachment.");

                for (int index = 0; index < 3; index++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(
                    Vector3.Distance(chassisBody.position, chassisStartPosition),
                    Is.LessThan(0.01f),
                    "Installing a closed panel must not inject a launch impulse.");
                chassisBody.isKinematic = true;

                Collider[] doorColliders = door
                    .GetComponentsInChildren<Collider>(true)
                    .Where(value => value != null && value.enabled &&
                        !value.isTrigger &&
                        value.attachedRigidbody == door.Body)
                    .ToArray();
                Collider[] chassisColliders = instance
                    .GetComponentsInChildren<Collider>(true)
                    .Where(value => value != null && value.enabled &&
                        !value.isTrigger &&
                        value.attachedRigidbody == chassisBody)
                    .ToArray();
                Assert.That(doorColliders, Is.Not.Empty);
                Assert.That(chassisColliders, Is.Not.Empty);
                foreach (Collider doorCollider in doorColliders)
                {
                    foreach (Collider chassisCollider in chassisColliders)
                    {
                        Assert.That(
                            Physics.GetIgnoreCollision(
                                doorCollider,
                                chassisCollider),
                            Is.True,
                            "The installed donor hinge has collision disabled " +
                            "against its connected chassis body.");
                    }
                }

                InteractionTargetHost host = door
                    .GetComponent<InteractionTargetHost>();
                Assert.That(
                    host.TryGetCapability(
                        out IContinuousContextInteractionTarget continuous),
                    Is.True);
                Assert.That(
                    host.TryGetCapability<IToolActivationTarget>(out _),
                    Is.False,
                    "The donor door has no F toggle; motion is mouse-held only.");
                AssemblyHingedPartInteractionTarget hinge = door
                    .GetComponent<AssemblyHingedPartInteractionTarget>();
                var context = new InteractionContext(
                    instance,
                    door.transform.position,
                    door.transform.forward);
                FastenerDefinition fastener = mount.Definition.Fasteners[0];
                ToolDefinition tool = assembly.Tools.Single(value =>
                    value.Size == fastener.Size);
                AssemblyOperationResult fastenerResult = assembly
                    .TryOperateFastener(
                        mount.MountId,
                        fastener.DefinitionId,
                        tool,
                        tighten: true);
                Assert.That(
                    fastenerResult.Succeeded,
                    Is.True,
                    fastenerResult.Message);

                Assert.That(hinge.IsClosedLatched, Is.True);
                continuous.BeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Primary);
                Assert.That(hinge.IsClosedLatched, Is.False,
                    "Opening input must release the closed-door latch.");
                for (int index = 0; index < 8; index++)
                {
                    Assert.That(
                        continuous.ContinueContinuousInteraction(
                            Time.fixedDeltaTime),
                        Is.True);
                    yield return new WaitForFixedUpdate();
                }

                float openAtRelease = hinge.OpenNormalized;
                continuous.EndContinuousInteraction();
                Assert.That(
                    Quaternion.Angle(
                        door.transform.rotation,
                        mount.Pose.rotation),
                    Is.GreaterThan(5f),
                    "Assembly pose synchronization must preserve the hinge's " +
                    "current open angle.");

                for (int index = 0; index < 5; index++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(hinge.OpenNormalized, Is.GreaterThan(openAtRelease),
                    "The door must keep moving on release and lose velocity " +
                    "through damping instead of freezing at the cursor angle.");

                hinge.RestoreOpenState(0.6f);
                Assert.That(hinge.IsClosedLatched, Is.False);
                Collider sweepCollider = doorColliders
                    .OrderByDescending(value => value.bounds.size.sqrMagnitude)
                    .First();
                Vector3 radial = sweepCollider.bounds.center -
                    mount.Pose.position;
                Vector3 currentOuterPoint = sweepCollider.ClosestPoint(
                    sweepCollider.bounds.center + radial.normalized * 2f);
                Vector3 outerPointInDoor = door.transform.InverseTransformPoint(
                    currentOuterPoint);
                AssemblyHingeMountAuthoring hingeProfile = mount
                    .GetComponent<AssemblyHingeMountAuthoring>();
                Quaternion blockedRotation = mount.Pose.rotation *
                    Quaternion.AngleAxis(
                        hingeProfile.OpenAngleDegrees * 0.2f,
                        hingeProfile.LocalAxis);
                Vector3 blockedPoint = mount.Pose.position +
                    blockedRotation * outerPointInDoor;
                GameObject blocker = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                blocker.name = "Door sweep blocker";
                blocker.transform.SetPositionAndRotation(
                    blockedPoint,
                    blockedRotation);
                blocker.transform.localScale = Vector3.one * 0.22f;
                Physics.SyncTransforms();

                continuous.BeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Secondary);
                for (int index = 0; index < 100; index++)
                {
                    continuous.ContinueContinuousInteraction(
                        Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                }

                continuous.EndContinuousInteraction();
                Assert.That(hinge.IsClosedLatched, Is.False,
                    "A world collider in the sweep must physically prevent " +
                    "the door from reaching its latch.");
                Assert.That(hinge.OpenNormalized, Is.GreaterThan(0.03f),
                    "Closing torque must not tunnel the door through an obstacle.");
                Object.Destroy(blocker);
                yield return null;
                Physics.SyncTransforms();

                continuous.BeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Secondary);
                for (int index = 0; index < 150 &&
                     !hinge.IsClosedLatched; index++)
                {
                    continuous.ContinueContinuousInteraction(
                        Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                    Assert.That(
                        physicsLink.InstalledHinge,
                        Is.Not.Null,
                        "The donor hinge broke during ordinary closing at " +
                        "step " + index + ", open=" + hinge.OpenNormalized +
                        ", angularVelocity=" + door.Body.angularVelocity);
                }

                continuous.EndContinuousInteraction();
                Assert.That(door.IsInstalled, Is.True);
                Assert.That(hinge.IsClosedLatched, Is.True,
                    "Holding close must settle and latch at the physical stop. " +
                    "open=" + hinge.OpenNormalized +
                    ", angularVelocity=" + door.Body.angularVelocity);
                Assert.That(hinge.OpenNormalized, Is.EqualTo(0f));
                Assert.That(
                    Quaternion.Angle(
                        door.transform.rotation,
                        mount.Pose.rotation),
                    Is.LessThan(0.1f));

                Assert.That(
                    Vector3.Distance(chassisBody.position, chassisStartPosition),
                    Is.LessThan(0.01f));

                AssemblyOperationResult loosenResult = assembly
                    .TryOperateFastener(
                        mount.MountId,
                        fastener.DefinitionId,
                        tool,
                        tighten: false);
                Assert.That(
                    loosenResult.Succeeded,
                    Is.True,
                    loosenResult.Message);
                continuous.BeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Primary);
                for (int index = 0; index < 180 && door.IsInstalled; index++)
                {
                    continuous.ContinueContinuousInteraction(
                        Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                }

                continuous.EndContinuousInteraction();
                Assert.That(door.IsInstalled, Is.False,
                    "A completely unfastened donor door must detach only when " +
                    "the player keeps opening it through the full travel.");
                foreach (Collider doorCollider in doorColliders)
                {
                    foreach (Collider chassisCollider in chassisColliders)
                    {
                        Assert.That(
                            Physics.GetIgnoreCollision(
                                doorCollider,
                                chassisCollider),
                            Is.False,
                            "Loose-door collision must return after full-open " +
                            "detachment.");
                    }
                }
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        [UnityTest]
        public IEnumerator BothDoorsLatchOnlyAtTheirPhysicalClosedEndpoint()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = Object.Instantiate(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassisBody = instance.GetComponent<Rigidbody>();
                chassisBody.useGravity = false;
                chassisBody.isKinematic = true;
                string[] doorSlugs = { "door-left", "door-right" };
                foreach (PartInstance otherPart in assembly.Parts)
                {
                    string definitionId = otherPart?.Definition?.DefinitionId;
                    if (otherPart != null && !otherPart.IsAssemblyRoot &&
                        !otherPart.IsInstalled &&
                        !doorSlugs.Any(slug => definitionId ==
                            "vehicle.satsuma.part." + slug))
                    {
                        otherPart.gameObject.SetActive(false);
                    }
                }

                foreach (string slug in doorSlugs)
                {
                    PartInstance door = assembly.Parts.Single(value =>
                        value.Definition != null &&
                        value.Definition.DefinitionId ==
                        "vehicle.satsuma.part." + slug);
                    MountPointAuthoring mount = assembly.MountPoints.Single(
                        value => value.MountId ==
                            "mount.satsuma." + slug);
                    InstallAtMount(assembly, door, mount);
                    FastenerDefinition fastener =
                        mount.Definition.Fasteners[0];
                    ToolDefinition tool = assembly.Tools.Single(value =>
                        value.Size == fastener.Size);
                    AssemblyOperationResult tightened = assembly
                        .TryOperateFastener(
                            mount.MountId,
                            fastener.DefinitionId,
                            tool,
                            tighten: true);
                    Assert.That(tightened.Succeeded, Is.True,
                        tightened.Message);

                    AssemblyHingedPartInteractionTarget hinge = door
                        .GetComponent<AssemblyHingedPartInteractionTarget>();
                    hinge.RestoreOpenState(0.7f);
                    yield return new WaitForFixedUpdate();
                    yield return new WaitForFixedUpdate();
                    Assert.That(hinge.IsClosedLatched, Is.False, slug);
                    Assert.That(
                        Quaternion.Angle(
                            door.Body.rotation,
                            mount.Pose.rotation),
                        Is.GreaterThan(45f),
                        slug + " must remain physically open before close input.");

                    var context = new InteractionContext(
                        instance,
                        door.transform.position,
                        door.transform.forward);
                    hinge.BeginContinuousInteraction(
                        context,
                        ContinuousContextInteractionDirection.Secondary);
                    float travelImmediatelyBeforeLatch = float.PositiveInfinity;
                    for (int index = 0;
                         index < 300 && !hinge.IsClosedLatched;
                         index++)
                    {
                        travelImmediatelyBeforeLatch = Quaternion.Angle(
                            door.Body.rotation,
                            mount.Pose.rotation);
                        Assert.That(
                            hinge.ContinueContinuousInteraction(
                                Time.fixedDeltaTime),
                            Is.True,
                            slug + " lost held-close interaction at step " +
                            index + ".");
                        yield return new WaitForFixedUpdate();
                    }

                    hinge.EndContinuousInteraction();
                    Assert.That(hinge.IsClosedLatched, Is.True,
                        slug + " did not latch after reaching its stop.");
                    Assert.That(
                        travelImmediatelyBeforeLatch,
                        Is.LessThanOrEqualTo(2f),
                        slug + " latched from broad travel instead of its " +
                        "physical one-degree endpoint.");
                    Assert.That(
                        Quaternion.Angle(
                            door.Body.rotation,
                            mount.Pose.rotation),
                        Is.LessThan(0.1f),
                        slug);
                }
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        [UnityTest]
        public IEnumerator BootlidUsesDonorOneDegreeOpenHoldAndReleasesItOnClose()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = Object.Instantiate(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassisBody = instance.GetComponent<Rigidbody>();
                chassisBody.useGravity = false;
                chassisBody.isKinematic = true;
                PartInstance bootlidPart = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.bootlid");
                MountPointAuthoring bootlidMount = assembly.MountPoints.Single(
                    value => value.MountId == "mount.satsuma.bootlid");
                foreach (PartInstance otherPart in assembly.Parts)
                {
                    if (otherPart != null && otherPart != bootlidPart &&
                        !otherPart.IsAssemblyRoot && !otherPart.IsInstalled)
                    {
                        otherPart.gameObject.SetActive(false);
                    }
                }

                AssemblyHingedPartInteractionTarget bootlid = bootlidPart
                    .GetComponent<AssemblyHingedPartInteractionTarget>();
                GameObject movingHingeArms = bootlid
                    .InstalledOnlyPresentationObjects.Single();
                GameObject chassisHingeArms = bootlid
                    .DetachedOnlyPresentationObjects.Single();
                Assert.That(movingHingeArms.name, Is.EqualTo("hooks_77027"));
                Assert.That(chassisHingeArms.name, Is.EqualTo("hooks_78823"));
                Assert.That(movingHingeArms.activeSelf, Is.False);
                Assert.That(chassisHingeArms.activeSelf, Is.True);

                InstallAtMount(assembly, bootlidPart, bootlidMount);
                Assert.That(movingHingeArms.activeSelf, Is.True,
                    "The assembled donor hinge arms must move with the bootlid.");
                Assert.That(chassisHingeArms.activeSelf, Is.False,
                    "The stationary donor copy must be hidden after assembly.");
                FastenerDefinition fastener =
                    bootlidMount.Definition.Fasteners[0];
                ToolDefinition tool = assembly.Tools.Single(value =>
                    value.Size == fastener.Size);
                AssemblyOperationResult tightened = assembly.TryOperateFastener(
                    bootlidMount.MountId,
                    fastener.DefinitionId,
                    tool,
                    tighten: true);
                Assert.That(tightened.Succeeded, Is.True, tightened.Message);

                HingeJoint physicalHinge = bootlidPart
                    .GetComponent<AssemblyInstalledPhysicsLink>()
                    .InstalledHinge;
                var context = new InteractionContext(
                    instance,
                    bootlidPart.transform.position,
                    bootlidPart.transform.forward);
                InteractionTargetHost host = bootlidPart
                    .GetComponent<InteractionTargetHost>();
                Assert.That(
                    host.TryGetCapability(
                        out IContinuousContextInteractionTarget continuous),
                    Is.True);

                bootlid.RestoreOpenState(1f);
                yield return null;
                yield return new WaitForFixedUpdate();
                Assert.That(bootlid.IsOpenHeld, Is.True);
                Assert.That(physicalHinge.limits.min,
                    Is.EqualTo(-70f).Within(0.001f));
                Assert.That(physicalHinge.limits.max,
                    Is.EqualTo(-69f).Within(0.001f));

                for (int index = 0; index < 20; index++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(bootlid.IsOpenHeld, Is.True);
                Assert.That(bootlid.OpenNormalized, Is.GreaterThanOrEqualTo(0.98f),
                    "The fully opened bootlid must remain in the donor's " +
                    "one-degree hinge window without continued mouse input.");
                Assert.That(
                    continuous.CanBeginContinuousInteraction(
                        context,
                        ContinuousContextInteractionDirection.Secondary),
                    Is.True);
                continuous.BeginContinuousInteraction(
                    context,
                    ContinuousContextInteractionDirection.Secondary);
                Assert.That(bootlid.IsOpenHeld, Is.False,
                    "Held RMB must restore the complete hinge travel before " +
                    "applying closing torque.");
                Assert.That(physicalHinge.limits.min,
                    Is.EqualTo(-70f).Within(0.001f));
                Assert.That(physicalHinge.limits.max,
                    Is.EqualTo(0f).Within(0.001f));
                continuous.EndContinuousInteraction();

                MountPointRuntime runtimeMount = assembly.ResolveMount(
                    bootlidMount);
                foreach (FastenerInstance state in runtimeMount.Fasteners)
                {
                    ToolDefinition stateTool = assembly.Tools.Single(value =>
                        value.Size == state.Definition.Size);
                    while (state.Stage > 0)
                    {
                        AssemblyOperationResult loosened = assembly
                            .TryOperateFastener(
                                bootlidMount.MountId,
                                state.Definition.DefinitionId,
                                stateTool,
                                tighten: false);
                        Assert.That(loosened.Succeeded, Is.True,
                            loosened.Message);
                    }
                }

                AssemblyOperationResult removed = assembly.TryRemove(
                    bootlidPart);
                Assert.That(removed.Succeeded, Is.True, removed.Message);
                Assert.That(movingHingeArms.activeSelf, Is.False,
                    "Detached bootlid must hide its installed hinge-arm copy.");
                Assert.That(chassisHingeArms.activeSelf, Is.True,
                    "Removing the bootlid must restore the body-side arms.");
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        [UnityTest]
        public IEnumerator HoodRequiresDashboardReleaseBeforeMouseHeldMotion()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = Object.Instantiate(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassisBody = instance.GetComponent<Rigidbody>();
                chassisBody.useGravity = false;
                chassisBody.isKinematic = true;
                PartInstance dashboard = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.dashboard");
                PartInstance hoodPart = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.hood");
                foreach (PartInstance otherPart in assembly.Parts)
                {
                    if (otherPart != null && otherPart != dashboard &&
                        otherPart != hoodPart && !otherPart.IsAssemblyRoot &&
                        !otherPart.IsInstalled)
                    {
                        otherPart.gameObject.SetActive(false);
                    }
                }

                MountPointAuthoring dashboardMount = assembly.MountPoints
                    .Single(value =>
                        value.MountId == "mount.satsuma.dashboard");
                MountPointAuthoring hoodMount = assembly.MountPoints.Single(
                    value => value.MountId == "mount.satsuma.hood");
                InstallAtMount(assembly, dashboard, dashboardMount);
                InstallAtMount(assembly, hoodPart, hoodMount);
                FastenerDefinition hoodFastener =
                    hoodMount.Definition.Fasteners[0];
                ToolDefinition hoodTool = assembly.Tools.Single(value =>
                    value.Size == hoodFastener.Size);
                AssemblyOperationResult tightened = assembly.TryOperateFastener(
                    hoodMount.MountId,
                    hoodFastener.DefinitionId,
                    hoodTool,
                    tighten: true);
                Assert.That(tightened.Succeeded, Is.True, tightened.Message);

                AssemblyHingedPartInteractionTarget hood = hoodPart
                    .GetComponent<AssemblyHingedPartInteractionTarget>();
                AssemblyHoodReleaseInteractionTarget release = instance
                    .GetComponentInChildren<
                        AssemblyHoodReleaseInteractionTarget>(true);
                var hoodContext = new InteractionContext(
                    instance,
                    hoodPart.transform.position,
                    hoodPart.transform.forward);
                var releaseContext = new InteractionContext(
                    instance,
                    release.transform.position,
                    release.transform.forward);
                Assert.That(hood.RequiresReleaseBeforeOpening, Is.True);
                Assert.That(hood.IsClosedLatched, Is.True);
                Assert.That(hood.IsReleasedForOpening, Is.False);
                Assert.That(
                    hood.CanBeginContinuousInteraction(
                        hoodContext,
                        ContinuousContextInteractionDirection.Primary),
                    Is.False,
                    "The exterior hood target must stay locked until the " +
                    "dashboard lever is pulled.");
                Assert.That(release.CanInteract(releaseContext), Is.True);

                release.Interact(releaseContext);
                Assert.That(hood.IsReleasedForOpening, Is.True);
                Assert.That(hood.IsClosedLatched, Is.False);
                Assert.That(
                    hood.CanBeginContinuousInteraction(
                        hoodContext,
                        ContinuousContextInteractionDirection.Primary),
                    Is.True);
                hood.BeginContinuousInteraction(
                    hoodContext,
                    ContinuousContextInteractionDirection.Primary);
                for (int index = 0; index < 20; index++)
                {
                    hood.ContinueContinuousInteraction(Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                }

                hood.EndContinuousInteraction();
                Assert.That(hood.OpenNormalized, Is.GreaterThan(0.01f),
                    "Pulling the cabin release must enable physical held-LMB " +
                    "opening; the lever itself must not animate the hood.");

                hood.BeginContinuousInteraction(
                    hoodContext,
                    ContinuousContextInteractionDirection.Secondary);
                for (int index = 0; index < 300 &&
                     !hood.IsClosedLatched; index++)
                {
                    hood.ContinueContinuousInteraction(Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                }

                hood.EndContinuousInteraction();
                Assert.That(hood.IsClosedLatched, Is.True,
                    "Held RMB must close and relatch the hood at its stop.");
                Assert.That(hood.IsReleasedForOpening, Is.False,
                    "Closing the hood must require the cabin release again.");
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        private static void InstallAtMount(
            VehicleAssemblyController assembly,
            PartInstance part,
            MountPointAuthoring mount)
        {
            part.transform.SetPositionAndRotation(
                mount.Pose.position,
                mount.Pose.rotation);
            part.Body.position = mount.Pose.position;
            part.Body.rotation = mount.Pose.rotation;
            Physics.SyncTransforms();
            AssemblyOperationResult result = assembly.TryInstall(part, mount);
            Assert.That(result.Succeeded, Is.True, result.Message);
        }
    }
}
