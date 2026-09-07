using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.NWH;
using MSC.Vehicle.Simulation;
using NWH.WheelController3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class SatsumaHandbrakePlayModeTests
    {
        private readonly List<GameObject> cleanup = new();
        private float originalTimeScale;

        [SetUp]
        public void UseRunningClock()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator DestroyFixture()
        {
            for (int index = 0; index < cleanup.Count; index++)
            {
                if (cleanup[index] != null)
                {
                    Object.Destroy(cleanup[index]);
                }
            }
            cleanup.Clear();
            yield return null;
            yield return null;
            Physics.SyncTransforms();
            Time.timeScale = originalTimeScale;
        }

        [UnityTest]
        public IEnumerator RearParkingTorqueAddsWithoutAccumulationAndReleasesWithoutServiceLoss()
        {
            Fixture context = CreateFixture(Quaternion.Euler(0f, 180f, 0f));
            yield return null;
            SetLever(context, 20f);

            var commands = new WheelPhysicsCommand[4];
            commands[2] = new WheelPhysicsCommand(0f, 120f, 0f);
            commands[3] = new WheelPhysicsCommand(0f, 80f, 0f);
            for (int index = 0; index < 10; index++)
            {
                context.Backend.Apply(Time.fixedDeltaTime, commands);
                context.Adapter.ApplyNow();
            }

            Assert.That(context.Backend.Wheels[0].BrakeTorque, Is.Zero);
            Assert.That(context.Backend.Wheels[1].BrakeTorque, Is.Zero);
            Assert.That(context.Adapter.RearLeftWheel.BrakeTorque, Is.EqualTo(1120f));
            Assert.That(context.Adapter.RearRightWheel.BrakeTorque, Is.EqualTo(1080f));

            SetLever(context, 0f);
            Assert.That(context.Adapter.RearLeftWheel.BrakeTorque, Is.EqualTo(120f));
            Assert.That(context.Adapter.RearRightWheel.BrakeTorque, Is.EqualTo(80f));

            SetLever(context, 20f);
            context.Backend.Reset();
            Assert.That(context.Adapter.RearLeftWheel.BrakeTorque, Is.EqualTo(1000f),
                "Resetting the engine simulation must not release a mechanically latched brake.");
            context.Adapter.enabled = false;
            Assert.That(context.Adapter.RearLeftWheel.BrakeTorque, Is.Zero,
                "Removing the parking channel must not leave a sticky last-frame torque.");

            SetLever(context, 10f);
            Assert.That(context.Controller.TrySetHeldDirection(1), Is.True);
            context.Controller.enabled = false;
            Assert.That(context.Controller.IsHeld, Is.False,
                "PlayMode must deliver OnDisable and release held input immediately.");
            Assert.That(context.Controller.BrakeInput01, Is.Zero);
            context.Controller.enabled = true;
            Assert.That(context.Controller.IsHeld, Is.False);
            Assert.That(context.Controller.PositionDegrees, Is.EqualTo(10f));
        }

        [UnityTest]
        public IEnumerator RearContactsRollBrakeHoldAndReleaseOnControlledFiveDegreeSlope()
        {
            Quaternion slopeRotation = Quaternion.Euler(5f, 0f, 0f);
            Fixture context = CreateFixture(slopeRotation * Quaternion.Euler(0f, 180f, 0f));
            SetLever(context, 20f);

            WheelController left = context.Adapter.RearLeftWheel;
            WheelController right = context.Adapter.RearRightWheel;
            Vector3 slopeUp = slopeRotation * Vector3.up;
            Vector3 contactCenter = (left.transform.position + right.transform.position) * 0.5f -
                slopeUp * (left.SpringMaxLength + left.Radius - 0.025f);
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cleanup.Add(ground);
            ground.name = "Handbrake controlled 5 degree test slope";
            ground.layer = 0;
            ground.transform.SetPositionAndRotation(contactCenter - slopeUp * 0.1f, slopeRotation);
            ground.transform.localScale = new Vector3(10f, 0.2f, 80f);

            // A two-wheel rear-axle fixture with a known 200 kg supported load.
            // Rotation is constrained only to avoid needing a front axle; its
            // translation is physically free and no velocity is zeroed below.
            context.Body.mass = 200f;
            context.Body.constraints = RigidbodyConstraints.FreezeRotation;
            context.Body.useGravity = true;
            Physics.SyncTransforms();
            for (int step = 0; step < 150; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(left.IsGrounded && right.IsGrounded, Is.True,
                "Both existing NWH rear contacts must support the known-load fixture.");
            Assert.That(left.HitCollider, Is.SameAs(ground.GetComponent<Collider>()));
            Assert.That(right.HitCollider, Is.SameAs(ground.GetComponent<Collider>()));
            Assert.That(context.Backend.ReleaseUnbrakedWheelsOnSlopes, Is.True,
                "Rebuild the Satsuma prefab to enable its explicit slope-release policy.");
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, slopeUp).normalized;
            SetLever(context, 0f);
            Assert.That(left.BrakeTorque, Is.Zero);
            Assert.That(right.BrakeTorque, Is.Zero);
            Vector3 releasedAt = context.Body.position;
            for (int step = 0; step < 120; step++)
            {
                yield return new WaitForFixedUpdate();
            }
            float freeSpeed = Vector3.Dot(context.Body.linearVelocity, downhill);
            Assert.That(freeSpeed, Is.GreaterThan(0.3f), "Released wheels must roll under gravity.");
            Assert.That(Vector3.Dot(context.Body.position - releasedAt, downhill),
                Is.GreaterThan(0.3f));

            SetLever(context, 20f);
            for (int step = 0; step < 120; step++)
            {
                yield return new WaitForFixedUpdate();
            }
            Vector3 heldAt = context.Body.position;
            for (int step = 0; step < 60; step++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(Mathf.Abs(Vector3.Dot(context.Body.linearVelocity, downhill)),
                Is.LessThan(0.1f), "The parking torque must stop the rolling axle.");
            Assert.That(Mathf.Abs(Vector3.Dot(context.Body.position - heldAt, downhill)),
                Is.LessThan(0.06f), "The stopped axle must remain held on the slope.");
            Assert.That(left.BrakeTorque, Is.EqualTo(1000f));
            Assert.That(right.BrakeTorque, Is.EqualTo(1000f));

            SetLever(context, 0f);
            for (int step = 0; step < 120; step++)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.That(Vector3.Dot(context.Body.linearVelocity, downhill), Is.GreaterThan(0.3f),
                "Release must resume physical rolling, without needing a part mutation.");
            Assert.That(left.BrakeTorque, Is.Zero);
            Assert.That(right.BrakeTorque, Is.Zero);
        }

        private Fixture CreateFixture(Quaternion rotation)
        {
            GameObject prefab = Resources.Load<GameObject>("Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore("Private donor-derived Satsuma baseline is unavailable.");
            }
            GameObject instance = Object.Instantiate(prefab, new Vector3(0f, 80f, 0f), rotation);
            cleanup.Add(instance);
            Rigidbody body = instance.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            instance.GetComponent<VehicleSimulationHost>().enabled = false;
            instance.GetComponent<AssemblyChassisMassController>().enabled = false;
            VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
            foreach (PartInstance part in assembly.Parts)
            {
                if (part != null && !part.IsAssemblyRoot && part.Body != null)
                {
                    part.Body.isKinematic = true;
                    part.Body.detectCollisions = false;
                }
            }
            SatsumaRearSuspensionController rear = instance.GetComponent<SatsumaRearSuspensionController>();
            foreach (SatsumaRearSuspensionCornerBinding corner in rear.Corners)
            {
                string suffix = corner.CornerId == "rl" ? "1" : "2";
                Install(assembly, "trail-arm-" + corner.CornerId, corner.TrailingArmMountId);
                Install(assembly, "drum-brake-" + suffix, corner.DrumMountId);
                Install(assembly, "coil-spring-" + suffix, corner.StockSpringMountId);
                Install(assembly, "shock-absorber-" + suffix, corner.ShockMountId);
                PartInstance wheel = Install(assembly, "wheel-stock-" + corner.CornerId,
                    corner.RoadWheelMountId);
                wheel.GetComponentInChildren<AssemblyWheelTireState>(true).SetTireInstalled(true);
            }
            Install(assembly, "handbrake", SatsumaHandbrakeController.MountId);
            instance.GetComponent<NwhAssemblyWheelSupportController>().RefreshSupport(force: true);
            instance.GetComponent<SatsumaRearNwhSuspensionController>().ApplyNow();
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.isTrigger)
                {
                    collider.enabled = false;
                }
            }
            Physics.SyncTransforms();
            SatsumaHandbrakeController controller = instance.GetComponent<SatsumaHandbrakeController>();
            SatsumaHandbrakeNwhAdapter adapter = instance.GetComponent<SatsumaHandbrakeNwhAdapter>();
            Assert.That(controller, Is.Not.Null, "Rebuild the generated handbrake baseline before this test.");
            Assert.That(adapter, Is.Not.Null);
            Assert.That(controller.CanOperate, Is.True);
            return new Fixture(body, controller, adapter, instance.GetComponent<NwhWheelPhysicsBackend>());
        }

        private static PartInstance Install(VehicleAssemblyController assembly, string slug, string mountId)
        {
            PartInstance part = assembly.Parts.Single(value => value.Definition != null &&
                value.Definition.DefinitionId == "vehicle.satsuma.part." + slug);
            MountPointAuthoring mount = assembly.MountPoints.Single(value => value.MountId == mountId);
            part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            part.Body.position = mount.Pose.position;
            part.Body.rotation = mount.Pose.rotation;
            AssemblyOperationResult installed = assembly.TryInstall(part, mount);
            Assert.That(installed.Succeeded, Is.True, installed.Message);
            foreach (FastenerInstance fastener in assembly.ResolveMount(mount).Fasteners)
            {
                ToolDefinition tool = assembly.Tools.FirstOrDefault(value =>
                    value != null && value.Size == fastener.Definition.Size);
                Assert.That(tool, Is.Not.Null, fastener.Definition.DefinitionId);
                while (fastener.Stage < fastener.Definition.MaximumStage)
                {
                    AssemblyOperationResult tightened = assembly.TryOperateFastener(
                        mountId, fastener.Definition.DefinitionId, tool, tighten: true);
                    Assert.That(tightened.Succeeded, Is.True, tightened.Message);
                }
            }
            return part;
        }

        private static void SetLever(Fixture context, float position)
        {
            Assert.That(context.Controller.TryRestore(new SatsumaHandbrakeSaveDto
            {
                positionDegrees = position,
            }, out string failure), Is.True, failure);
            context.Adapter.ApplyNow();
        }

        private sealed class Fixture
        {
            public Fixture(Rigidbody body, SatsumaHandbrakeController controller,
                SatsumaHandbrakeNwhAdapter adapter, NwhWheelPhysicsBackend backend)
            {
                Body = body;
                Controller = controller;
                Adapter = adapter;
                Backend = backend;
            }
            public Rigidbody Body { get; }
            public SatsumaHandbrakeController Controller { get; }
            public SatsumaHandbrakeNwhAdapter Adapter { get; }
            public NwhWheelPhysicsBackend Backend { get; }
        }
    }
}
