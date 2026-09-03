using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSC.LegacyImport;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.NWH;
using NWH.WheelController3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class SatsumaRearLoadedCompressionPlayModeTests
    {
        private const float StockWheelLoadNewtons = 981f;
        private const float LongWheelLoadNewtons = 1471.5f;
        private const float CompressionToleranceMeters = 0.004f;

        private readonly List<GameObject> cleanup = new List<GameObject>();
        private float originalTimeScale;

        [SetUp]
        public void UseRunningPhysicsClock()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator DestroyFixturesAndRestoreClock()
        {
            Time.timeScale = originalTimeScale;
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
        }

        [UnityTest]
        public IEnumerator StockRearSpringsProduceDonorForceAtKnownCompression()
        {
            yield return VerifyKnownCompression(
                useLongSpring: false,
                wheelLoadNewtons: StockWheelLoadNewtons);
        }

        [UnityTest]
        public IEnumerator LongRearSpringsProduceDonorForceAtKnownCompression()
        {
            yield return VerifyKnownCompression(
                useLongSpring: true,
                wheelLoadNewtons: LongWheelLoadNewtons);
        }

        [UnityTest]
        public IEnumerator StockRearSpringsSettleUnderKnownSymmetricChassisLoad()
        {
            GameObject instance = CreateFixture(spawnY: 0.4f);
            VehicleAssemblyController assembly = instance
                .GetComponent<VehicleAssemblyController>();
            SatsumaRearSuspensionController presentation = instance
                .GetComponent<SatsumaRearSuspensionController>();
            NwhAssemblyWheelSupportController support = instance
                .GetComponent<NwhAssemblyWheelSupportController>();
            SatsumaRearNwhSuspensionController rearAuthority = instance
                .GetComponent<SatsumaRearNwhSuspensionController>();
            Rigidbody chassis = instance.GetComponent<Rigidbody>();

            InstallCompleteRear(
                assembly,
                presentation,
                useLongSpring: false,
                includeShock: true);
            support.RefreshSupport(force: true);
            rearAuthority.ApplyNow();
            DisableExistingSolidColliders(instance);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Rear NWH known-load ground";
            ground.layer = 0;
            ground.transform.SetPositionAndRotation(
                new Vector3(0f, -0.1f, 0f),
                Quaternion.identity);
            ground.transform.localScale = new Vector3(6f, 0.2f, 6f);
            cleanup.Add(ground);
            Physics.SyncTransforms();

            chassis.isKinematic = false;
            chassis.useGravity = false;
            chassis.constraints = RigidbodyConstraints.FreezePositionX |
                RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotation;
            chassis.linearVelocity = Vector3.zero;
            chassis.angularVelocity = Vector3.zero;

            WheelController[] rearWheels = support.Bindings
                .Skip(2)
                .Select(value => value.Wheel)
                .ToArray();
            var tailCompression = new List<float>[rearWheels.Length];
            for (int index = 0; index < tailCompression.Length; index++)
            {
                tailCompression[index] = new List<float>();
            }

            const int settleSteps = 360;
            const int tailSteps = 60;
            for (int step = 0; step < settleSteps; step++)
            {
                chassis.AddForce(
                    Vector3.down * (StockWheelLoadNewtons * 2f),
                    ForceMode.Force);
                yield return new WaitForFixedUpdate();
                if (step >= settleSteps - tailSteps)
                {
                    for (int index = 0; index < rearWheels.Length; index++)
                    {
                        tailCompression[index].Add(
                            rearWheels[index].SpringMaxLength -
                            rearWheels[index].SpringLength);
                    }
                }
            }

            yield return null;
            rearAuthority.ApplyNow();
            float expectedCompression = StockWheelLoadNewtons /
                SatsumaRearSuspensionForce.StockWheelRate;
            for (int index = 0; index < rearWheels.Length; index++)
            {
                WheelController wheel = rearWheels[index];
                string corner = index == 0 ? "rl" : "rr";
                Assert.That(wheel.enabled, Is.True, corner);
                Assert.That(wheel.IsGrounded, Is.True, corner);
                Assert.That(
                    wheel.HitCollider,
                    Is.SameAs(ground.GetComponent<Collider>()),
                    corner);
                Assert.That(
                    tailCompression[index].Average(),
                    Is.EqualTo(expectedCompression).Within(0.012f),
                    corner + " settled compression");
                Assert.That(
                    tailCompression[index].Max() -
                    tailCompression[index].Min(),
                    Is.LessThan(0.018f),
                    corner + " tail oscillation");
                Assert.That(
                    wheel.SpringForce,
                    Is.EqualTo(
                        SatsumaRearSuspensionForce.StockWheelRate *
                        (wheel.SpringMaxLength - wheel.SpringLength))
                        .Within(3f),
                    corner + " linear donor spring force");
            }

            Assert.That(
                Mathf.Abs(chassis.linearVelocity.y),
                Is.LessThan(0.15f),
                "Known symmetric rear load must settle instead of leaving the " +
                "body perched at full extension.");
        }

        private IEnumerator VerifyKnownCompression(
            bool useLongSpring,
            float wheelLoadNewtons)
        {
            GameObject instance = CreateFixture(spawnY: 50f);
            VehicleAssemblyController assembly = instance
                .GetComponent<VehicleAssemblyController>();
            SatsumaRearSuspensionController presentation = instance
                .GetComponent<SatsumaRearSuspensionController>();
            NwhAssemblyWheelSupportController support = instance
                .GetComponent<NwhAssemblyWheelSupportController>();
            SatsumaRearNwhSuspensionController rearAuthority = instance
                .GetComponent<SatsumaRearNwhSuspensionController>();

            InstallCompleteRear(
                assembly,
                presentation,
                useLongSpring,
                includeShock: true);
            support.RefreshSupport(force: true);
            rearAuthority.ApplyNow();

            float wheelRate = useLongSpring
                ? SatsumaRearSuspensionForce.LongWheelRate
                : SatsumaRearSuspensionForce.StockWheelRate;
            float expectedCompression = wheelLoadNewtons / wheelRate;
            GameObject[] patches = new GameObject[2];
            for (int index = 0; index < patches.Length; index++)
            {
                WheelController wheel = support.Bindings[index + 2].Wheel;
                float desiredLength = wheel.SpringMaxLength -
                    expectedCompression;
                float groundTop = wheel.transform.position.y -
                    desiredLength - wheel.Radius;
                patches[index] = GameObject.CreatePrimitive(
                    PrimitiveType.Cube);
                patches[index].name = "Rear known-compression patch " + index;
                patches[index].layer = 0;
                patches[index].transform.position = new Vector3(
                    wheel.transform.position.x,
                    groundTop - 0.05f,
                    wheel.transform.position.z);
                patches[index].transform.localScale = new Vector3(
                    0.45f,
                    0.1f,
                    0.45f);
                cleanup.Add(patches[index]);
            }

            Physics.SyncTransforms();
            for (int step = 0; step < 35; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            yield return null;
            rearAuthority.ApplyNow();
            for (int index = 0; index < 2; index++)
            {
                NwhAssemblyWheelSupportBinding supportBinding =
                    support.Bindings[index + 2];
                WheelController wheel = supportBinding.Wheel;
                SatsumaRearNwhCornerBinding rearBinding =
                    rearAuthority.Corners[index];
                float measuredCompression = wheel.SpringMaxLength -
                    wheel.SpringLength;
                string context = rearBinding.CornerId +
                    (useLongSpring ? " long" : " stock");

                Assert.That(wheel.enabled, Is.True, context);
                Assert.That(wheel.IsGrounded, Is.True, context);
                Assert.That(
                    wheel.HitCollider,
                    Is.SameAs(patches[index].GetComponent<Collider>()),
                    context);
                Assert.That(
                    measuredCompression,
                    Is.EqualTo(expectedCompression).Within(
                        CompressionToleranceMeters),
                    context);
                Assert.That(
                    wheel.SpringForce,
                    Is.EqualTo(wheelLoadNewtons).Within(3f),
                    context + " wheel force");
                Assert.That(
                    wheel.DamperForce,
                    Is.EqualTo(0f).Within(2f),
                    context + " settled damper force");
                Assert.That(
                    wheel.DamperBumpRate,
                    Is.EqualTo(
                        SatsumaRearSuspensionForce.StockShockDamper)
                        .Within(0.001f),
                    context);

                float expectedAngle =
                    SatsumaRearSuspensionTravel.ResolveArmAngle(
                        rearBinding.NeutralArmPivotLocalPosition,
                        measuredCompression,
                        hasStockSpring: !useLongSpring,
                        hasLongSpring: useLongSpring);
                Quaternion expectedRotation = instance.transform.rotation *
                    Quaternion.AngleAxis(expectedAngle, Vector3.right) *
                    rearBinding.NeutralArmLocalRotation;
                Assert.That(
                    Quaternion.Angle(
                        rearBinding.TrailingArmMount.Pose.rotation,
                        expectedRotation),
                    Is.LessThan(0.2f),
                    context + " donor IK arm angle");

                PartInstance arm = ResolveInstalledPart(
                    assembly,
                    rearBinding.TrailingArmMount.MountId);
                PartInstance drum = ResolveInstalledPart(
                    assembly,
                    rearBinding.DrumMountId);
                Assert.That(arm.Body.isKinematic, Is.True, context);
                Assert.That(drum.Body.isKinematic, Is.True, context);
                Assert.That(
                    arm.GetComponent<AssemblyInstalledPhysicsLink>()
                        .InstalledHinge,
                    Is.Null,
                    context + " must not retain a second force solver");
            }
        }

        private GameObject CreateFixture(float spawnY)
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            LegacySatsumaBaselineMetadata metadata = prefab
                .GetComponent<LegacySatsumaBaselineMetadata>();
            Assert.That(metadata, Is.Not.Null);
            Assert.That(
                Quaternion.Angle(
                    metadata.DefaultWorldRotation,
                    Quaternion.Euler(0f, 180f, 0f)),
                Is.LessThan(0.01f));
            GameObject instance = Object.Instantiate(
                prefab,
                new Vector3(0f, spawnY, 0f),
                metadata.DefaultWorldRotation);
            cleanup.Add(instance);

            Rigidbody chassis = instance.GetComponent<Rigidbody>();
            chassis.useGravity = false;
            chassis.constraints = RigidbodyConstraints.FreezeAll;
            VehicleSimulationHost simulation = instance
                .GetComponent<VehicleSimulationHost>();
            if (simulation != null)
            {
                simulation.enabled = false;
            }

            VehicleAssemblyController assembly = instance
                .GetComponent<VehicleAssemblyController>();
            foreach (PartInstance part in assembly.Parts)
            {
                if (part != null && !part.IsAssemblyRoot &&
                    part.Body != null)
                {
                    part.Body.isKinematic = true;
                    part.Body.detectCollisions = false;
                }
            }

            Assert.That(
                instance.GetComponent<SatsumaRearNwhSuspensionController>(),
                Is.Not.Null);
            return instance;
        }

        private static void InstallCompleteRear(
            VehicleAssemblyController assembly,
            SatsumaRearSuspensionController presentation,
            bool useLongSpring,
            bool includeShock)
        {
            foreach (SatsumaRearSuspensionCornerBinding corner in
                     presentation.Corners)
            {
                string suffix = corner.CornerId == "rl" ? "1" : "2";
                Install(
                    assembly,
                    "trail-arm-" + corner.CornerId,
                    corner.TrailingArmMountId);
                Install(
                    assembly,
                    "drum-brake-" + suffix,
                    corner.DrumMountId);
                Install(
                    assembly,
                    (useLongSpring
                        ? "extra-long-coil-spring-"
                        : "coil-spring-") + suffix,
                    useLongSpring
                        ? corner.LongSpringMountId
                        : corner.StockSpringMountId);
                if (includeShock)
                {
                    Install(
                        assembly,
                        "shock-absorber-" + suffix,
                        corner.ShockMountId);
                }
            }
        }

        private static PartInstance Install(
            VehicleAssemblyController assembly,
            string slug,
            string mountId)
        {
            PartInstance part = assembly.Parts.Single(value =>
                value.Definition != null &&
                value.Definition.DefinitionId ==
                    "vehicle.satsuma.part." + slug);
            MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                value.MountId == mountId);
            part.transform.SetPositionAndRotation(
                mount.Pose.position,
                mount.Pose.rotation);
            part.Body.position = mount.Pose.position;
            part.Body.rotation = mount.Pose.rotation;
            AssemblyOperationResult installed = assembly.TryInstall(
                part,
                mount);
            Assert.That(installed.Succeeded, Is.True, installed.Message);
            foreach (FastenerInstance fastener in
                     assembly.ResolveMount(mount).Fasteners)
            {
                ToolDefinition tool = assembly.Tools.FirstOrDefault(value =>
                    value != null &&
                    value.Size == fastener.Definition.Size);
                Assert.That(
                    tool,
                    Is.Not.Null,
                    "Missing known-load fixture tool for " +
                    fastener.Definition.DefinitionId);
                while (fastener.Stage < fastener.Definition.MaximumStage)
                {
                    AssemblyOperationResult tightened =
                        assembly.TryOperateFastener(
                            mountId,
                            fastener.Definition.DefinitionId,
                            tool,
                            tighten: true);
                    Assert.That(
                        tightened.Succeeded,
                        Is.True,
                        tightened.Message);
                }
            }

            return part;
        }

        private static PartInstance ResolveInstalledPart(
            VehicleAssemblyController assembly,
            string mountId)
        {
            MountPointRuntime mount = assembly.ResolveMount(
                assembly.MountPoints.Single(value => value.MountId == mountId));
            Assert.That(mount.IsOccupied, Is.True, mountId);
            return mount.InstalledPart;
        }

        private static void DisableExistingSolidColliders(GameObject root)
        {
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(
                         true))
            {
                if (collider != null && !collider.isTrigger)
                {
                    collider.enabled = false;
                }
            }
        }
    }
}
