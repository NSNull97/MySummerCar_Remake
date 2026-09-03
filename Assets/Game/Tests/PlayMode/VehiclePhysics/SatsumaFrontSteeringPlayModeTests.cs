using System.Collections;
using System.Linq;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.NWH;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class SatsumaFrontSteeringPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator FlushRuntimePhysicsHelpers()
        {
            yield return null;
            yield return null;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator OuterJointConnectsAtEightAndStaysConnectedUntilZero()
        {
            GameObject instance = CreateFixture();
            try
            {
                yield return null;
                var assembly = instance.GetComponent<VehicleAssemblyController>();
                var steering = instance.GetComponent<SatsumaFrontSteeringController>();
                InstallStructure(instance, includeStruts: true);
                foreach (SatsumaFrontSuspensionCornerBinding corner in steering.Corners)
                {
                    MountPointRuntime mount = assembly.ResolveMount(corner.SteeringRodMount);
                    AssemblySteeringAlignmentState alignment = mount.InstalledPart
                        .GetComponent<AssemblySteeringAlignmentState>();
                    alignment.RestoreValidated(new AssemblySteeringAlignmentSaveDto { alignmentDegrees = 4f });
                    steering.ApplyNow();
                    AssertState(steering, corner, connected: false, yaw: 4f);
                    TurnToStage(assembly, mount, 7);
                    steering.ApplyNow();
                    AssertState(steering, corner, connected: false, yaw: 4f);
                    Assert.That(steering.TryGetFreeYawBody(corner.Wheel, out Rigidbody freeBody), Is.True);
                    freeBody.rotation = instance.transform.rotation * Quaternion.Euler(0f, -12f, 0f);
                    steering.ApplyNow();
                    AssertState(steering, corner, connected: false, yaw: -12f);
                    Assert.That(steering.ResolveSteerAngle(corner.Wheel, 11f), Is.EqualTo(-12f).Within(0.01f));

                    TurnToStage(assembly, mount, 8);
                    steering.ApplyNow();
                    AssertState(steering, corner, connected: true, yaw: 4f);
                    Assert.That(steering.TryGetFreeYawBody(corner.Wheel, out _), Is.False);
                    Assert.That(steering.ResolveSteerAngle(corner.Wheel, 11f), Is.EqualTo(15f).Within(0.01f));
                    TurnToStage(assembly, mount, 7);
                    steering.ApplyNow();
                    AssertState(steering, corner, connected: true, yaw: 4f);
                    Assert.That(corner.Wheel.SteerAngle, Is.EqualTo(15f).Within(0.01f));
                    TurnToStage(assembly, mount, 0);
                    steering.ApplyNow();
                    // Donor input steering rotates a child, not the carrier.
                    // Disconnect sets that input to zero; it does not become toe.
                    AssertState(steering, corner, connected: false, yaw: 4f);
                    Assert.That(corner.Wheel.SteerAngle, Is.EqualTo(4f).Within(0.01f));
                }
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        [UnityTest]
        public IEnumerator StrutInstallationGatesConnectionAndAdjusterSnapsEitherMode()
        {
            GameObject instance = CreateFixture();
            try
            {
                yield return null;
                var assembly = instance.GetComponent<VehicleAssemblyController>();
                var steering = instance.GetComponent<SatsumaFrontSteeringController>();
                InstallStructure(instance, includeStruts: false);
                foreach (SatsumaFrontSuspensionCornerBinding corner in steering.Corners)
                {
                    MountPointRuntime mount = assembly.ResolveMount(corner.SteeringRodMount);
                    var alignment = mount.InstalledPart.GetComponent<AssemblySteeringAlignmentState>();
                    alignment.RestoreValidated(new AssemblySteeringAlignmentSaveDto { alignmentDegrees = 3f });
                    TurnToStage(assembly, mount, 8);
                    steering.ApplyNow();
                    Assert.That(mount.FastenerGroup.IsBolted, Is.True);
                    AssertState(steering, corner, connected: false, yaw: 3f);
                    Assert.That(alignment.TryAdjust(tighten: true), Is.True);
                    steering.ApplyNow();
                    AssertState(steering, corner, connected: false, yaw: 2.9f);
                    TurnToStage(assembly, mount, 7);
                    PartInstance strut = FindPart(assembly, "strut-" + corner.CornerId);
                    Install(assembly, strut, corner.StrutMount, tighten: false);
                    steering.ApplyNow();
                    AssertState(steering, corner, connected: true, yaw: 2.9f);
                    Assert.That(alignment.TryAdjust(tighten: false), Is.True);
                    steering.ApplyNow();
                    AssertState(steering, corner, connected: true, yaw: 3f);

                    AssemblyOperationResult removal = assembly.TryRemove(strut);
                    Assert.That(removal.Succeeded, Is.True, removal.Message);
                    strut.Body.isKinematic = true;
                    strut.Body.detectCollisions = false;
                    steering.ApplyNow();
                    AssertState(steering, corner, connected: false, yaw: 3f);
                    Assert.That(mount.FastenerGroup.IsBolted, Is.True);
                    Install(assembly, strut, corner.StrutMount, tighten: false);
                    steering.ApplyNow();
                    AssertState(steering, corner, connected: true, yaw: 3f);
                }
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        [UnityTest]
        public IEnumerator FreeYawUsesRealLimitedJointWithoutOwningVerticalContact()
        {
            GameObject instance = CreateFixture();
            Rigidbody[] freeBodies = null;
            try
            {
                yield return null;
                var steering = instance.GetComponent<SatsumaFrontSteeringController>();
                steering.ApplyNow();
                freeBodies = new Rigidbody[steering.Corners.Length];
                for (int i = 0; i < steering.Corners.Length; i++)
                {
                    SatsumaFrontSuspensionCornerBinding corner = steering.Corners[i];
                    float initialAlignment = corner.SteeringRodPresentation.Part
                        .GetComponent<AssemblySteeringAlignmentState>().AlignmentDegrees;
                    AssertState(steering, corner, connected: false, yaw: initialAlignment);
                    Assert.That(steering.TryGetFreeYawBody(corner.Wheel, out freeBodies[i]), Is.True);
                    Assert.That(freeBodies[i].GetComponentsInChildren<Collider>(), Is.Empty);
                    Assert.That(freeBodies[i].transform.IsChildOf(instance.transform), Is.False);
                    HingeJoint joint = freeBodies[i].GetComponent<HingeJoint>();
                    Assert.That(joint, Is.Not.Null);
                    Assert.That(joint.connectedBody.isKinematic, Is.True);
                    Assert.That(joint.axis, Is.EqualTo(Vector3.up));
                    Assert.That(joint.useLimits, Is.True);
                    Assert.That(joint.limits.min, Is.EqualTo(-33f));
                    Assert.That(joint.limits.max, Is.EqualTo(33f));
                    Assert.That(joint.useMotor || joint.useSpring, Is.False);
                    Assert.That(corner.Wheel.TargetRigidbody, Is.SameAs(instance.GetComponent<Rigidbody>()));
                }

                // Test the actual solver, not a Clamp() pretending to be a hinge.
                // Limits are relative to the joint creation pose (initial toe).
                for (int step = 0; step < 80; step++)
                {
                    for (int i = 0; i < freeBodies.Length; i++)
                    {
                        freeBodies[i].AddTorque(Vector3.up * (i == 0 ? 30f : -30f), ForceMode.Acceleration);
                    }
                    yield return new WaitForFixedUpdate();
                }
                foreach (Rigidbody body in freeBodies)
                {
                    float angle = Mathf.Abs(body.GetComponent<HingeJoint>().angle);
                    Assert.That(angle, Is.GreaterThan(25f));
                    Assert.That(angle, Is.LessThan(35f));
                }
            }
            finally
            {
                Object.Destroy(instance);
            }
            yield return null;
            yield return null;
            foreach (Rigidbody body in freeBodies)
            {
                Assert.That(body == null, Is.True, "The separate yaw rig leaked after its vehicle was destroyed.");
            }
        }

        private static GameObject CreateFixture()
        {
            GameObject prefab = Resources.Load<GameObject>("Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore("Private Satsuma baseline is unavailable.");
            }
            GameObject instance = Object.Instantiate(prefab, new Vector3(0f, 2f, 0f), Quaternion.identity);
            Rigidbody chassis = instance.GetComponent<Rigidbody>();
            chassis.useGravity = false;
            chassis.constraints = RigidbodyConstraints.FreezeAll;
            VehicleSimulationHost host = instance.GetComponent<VehicleSimulationHost>();
            if (host != null)
            {
                host.enabled = false;
            }
            foreach (PartInstance part in instance.GetComponent<VehicleAssemblyController>().Parts)
            {
                if (part != null && !part.IsAssemblyRoot && part.Body != null)
                {
                    part.Body.isKinematic = true;
                    part.Body.detectCollisions = false;
                }
            }
            Assert.That(instance.GetComponent<SatsumaFrontSteeringController>(), Is.Not.Null,
                "Regenerate the private Satsuma baseline with the steering binding.");
            return instance;
        }

        private static void InstallStructure(GameObject instance, bool includeStruts)
        {
            var assembly = instance.GetComponent<VehicleAssemblyController>();
            Install(assembly, FindPart(assembly, "sub-frame"), FindMount(assembly, "sub-frame"), true);
            Install(assembly, FindPart(assembly, "steering-rack"), FindMount(assembly, "steering-rack"), true);
            foreach (SatsumaFrontSuspensionCornerBinding corner in instance.GetComponent<SatsumaFrontSteeringController>().Corners)
            {
                Install(assembly, FindPart(assembly, "wishbone-" + corner.CornerId), corner.WishboneMount, true);
                Install(assembly, FindPart(assembly, "spindle-" + corner.CornerId), corner.SpindleMount, true);
                if (includeStruts)
                {
                    Install(assembly, FindPart(assembly, "strut-" + corner.CornerId), corner.StrutMount, false);
                }
                Install(assembly, FindPart(assembly, "steering-rod-" + corner.CornerId), corner.SteeringRodMount, false);
            }
        }

        private static void AssertState(SatsumaFrontSteeringController controller,
            SatsumaFrontSuspensionCornerBinding corner, bool connected, float yaw)
        {
            Assert.That(controller.TryGetSteeringState(corner.Wheel, out bool actual, out float actualYaw), Is.True);
            Assert.That(actual, Is.EqualTo(connected), corner.CornerId);
            Assert.That(Mathf.DeltaAngle(yaw, actualYaw), Is.EqualTo(0f).Within(0.02f), corner.CornerId);
        }

        private static void Install(VehicleAssemblyController assembly, PartInstance part,
            MountPointAuthoring mount, bool tighten)
        {
            part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            part.Body.position = mount.Pose.position;
            part.Body.rotation = mount.Pose.rotation;
            AssemblyOperationResult result = assembly.TryInstall(part, mount);
            Assert.That(result.Succeeded, Is.True, result.Message);
            if (tighten)
            {
                MountPointRuntime runtime = assembly.ResolveMount(mount);
                foreach (FastenerInstance fastener in runtime.Fasteners)
                {
                    Turn(assembly, runtime, fastener, fastener.Definition.MaximumStage);
                }
            }
        }

        private static void TurnToStage(VehicleAssemblyController assembly, MountPointRuntime mount, int stage) =>
            Turn(assembly, mount, mount.Fasteners.Single(), stage);

        private static void Turn(VehicleAssemblyController assembly, MountPointRuntime mount,
            FastenerInstance fastener, int stage)
        {
            int remaining = fastener.Definition.MaximumStage + 1;
            while (fastener.Stage != stage)
            {
                Assert.That(remaining--, Is.GreaterThan(0));
                ToolDefinition tool = assembly.Tools.First(value => value != null && value.Size == fastener.Definition.Size);
                AssemblyOperationResult result = assembly.TryOperateFastener(mount.MountId,
                    fastener.Definition.DefinitionId, tool, fastener.Stage < stage);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }
        }

        private static PartInstance FindPart(VehicleAssemblyController assembly, string slug) =>
            assembly.Parts.Single(part => part.Definition != null &&
                part.Definition.DefinitionId == "vehicle.satsuma.part." + slug);

        private static MountPointAuthoring FindMount(VehicleAssemblyController assembly, string slug) =>
            assembly.MountPoints.Single(mount => mount.MountId == "mount.satsuma." + slug);
    }
}
