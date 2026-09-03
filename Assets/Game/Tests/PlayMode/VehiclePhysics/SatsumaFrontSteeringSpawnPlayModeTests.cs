using System.Collections;
using System.Linq;
using MSC.LegacyImport;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.NWH;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class SatsumaFrontSteeringSpawnPlayModeTests
    {
        private float originalTimeScale;

        [SetUp]
        public void UseRunningPhysicsClock()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
        }

        [UnityTearDown]
        public IEnumerator FlushDeferredYawBodies()
        {
            Time.timeScale = originalTimeScale;
            yield return null;
            yield return null;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator DelayedAssemblyAtGlobalYawZeroDoesNotFlip() =>
            RunSpawnScenario(new Vector3(0f, 0f, 0f));

        [UnityTest]
        public IEnumerator DelayedAssemblyAtGlobalYawNinetyDoesNotFlip() =>
            RunSpawnScenario(new Vector3(0f, 90f, 0f));

        [UnityTest]
        public IEnumerator DelayedAssemblyAtGlobalYawOneSixtyFiveDoesNotFlip() =>
            RunSpawnScenario(new Vector3(0f, 165f, 0f));

        [UnityTest]
        public IEnumerator DelayedAssemblyAtGlobalYawOneEightyDoesNotFlip() =>
            RunSpawnScenario(new Vector3(0f, 180f, 0f));

        [UnityTest]
        public IEnumerator DelayedAssemblyAtTiltedGlobalPoseDoesNotFlip() =>
            RunSpawnScenario(new Vector3(15f, 165f, 12f));

        [UnityTest]
        public IEnumerator DelayedAssemblyAtExactProductionMetadataPoseDoesNotFlip() =>
            RunSpawnScenario(Vector3.zero, useProductionMetadataPose: true);

        [UnityTest]
        public IEnumerator ExistingFreeHingesRemainWithinLimitsWhileProductionChassisMoves()
        {
            GameObject instance = CreateProductionPoseFixture();
            try
            {
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                SatsumaFrontSteeringController steering = instance.GetComponent<SatsumaFrontSteeringController>();
                float[] alignment = ReadAlignment(steering);
                yield return FixedSteps(15);
                InstallForPoseChange(instance, includeRods: false);
                yield return FixedSteps(15);
                Rigidbody[] originalBodies = steering.Corners.Select(corner =>
                {
                    Assert.That(steering.TryGetFreeYawBody(corner.Wheel, out Rigidbody body), Is.True);
                    return body;
                }).ToArray();
                HingeJoint[] originalJoints = originalBodies.Select(body => body.GetComponent<HingeJoint>()).ToArray();
                Vector3 startPosition = chassis.position;
                Quaternion startRotation = chassis.rotation;

                // Move only after the free constraints have been simulated.
                // Keep the same hinge: free yaw must not be reset to toe.
                for (int step = 1; step <= 30; step++)
                {
                    float fraction = step / 30f;
                    SetChassisPose(chassis, startPosition + Vector3.up * (0.6f * fraction),
                        startRotation * Quaternion.Euler(15f * fraction, 20f * fraction, 12f * fraction));
                    yield return new WaitForFixedUpdate();
                    steering.ApplyNow();
                    for (int index = 0; index < steering.Corners.Length; index++)
                    {
                        AssertFreeYawWithinLimit(steering, steering.Corners[index], alignment[index],
                            "moving chassis, step " + step);
                    }
                }
                yield return FixedSteps(15);
                steering.ApplyNow();
                for (int index = 0; index < steering.Corners.Length; index++)
                {
                    SatsumaFrontSuspensionCornerBinding corner = steering.Corners[index];
                    AssertFreeYawWithinLimit(steering, corner, alignment[index], "settled after chassis movement");
                    Assert.That(steering.TryGetFreeYawBody(corner.Wheel, out Rigidbody body), Is.True);
                    Assert.That(body, Is.SameAs(originalBodies[index]), "Movement recreated the free body.");
                    Assert.That(body.GetComponent<HingeJoint>(), Is.SameAs(originalJoints[index]),
                        "Movement recreated the hinge instead of retaining free motion.");
                }
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        [UnityTest]
        public IEnumerator RecreatedHingesCaptureChangedChassisPoseBeforeFirstPhysicsStep()
        {
            GameObject instance = CreateProductionPoseFixture();
            try
            {
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
                SatsumaFrontSteeringController steering = instance.GetComponent<SatsumaFrontSteeringController>();
                float[] alignment = ReadAlignment(steering);
                yield return FixedSteps(15);
                InstallForPoseChange(instance, includeRods: true);
                foreach (SatsumaFrontSuspensionCornerBinding corner in steering.Corners)
                {
                    TurnOuterJointToStage(assembly, corner, 8);
                }
                steering.ApplyNow();
                yield return FixedSteps(5);
                foreach (SatsumaFrontSuspensionCornerBinding corner in steering.Corners)
                {
                    Assert.That(steering.TryGetSteeringState(corner.Wheel, out bool connected, out _), Is.True);
                    Assert.That(connected, Is.True);
                    Assert.That(steering.TryGetFreeYawBody(corner.Wheel, out _), Is.False);
                }

                // No yield or global SyncTransforms between the changed pose
                // and 8 -> 0. The new hinge must capture the current anchor now.
                SetChassisPose(chassis, chassis.position + new Vector3(0.2f, 0.5f, 0.1f),
                    chassis.rotation * Quaternion.Euler(15f, 30f, 12f));
                foreach (SatsumaFrontSuspensionCornerBinding corner in steering.Corners)
                {
                    TurnOuterJointToStage(assembly, corner, 0);
                }
                steering.ApplyNow();
                for (int index = 0; index < steering.Corners.Length; index++)
                {
                    SatsumaFrontSuspensionCornerBinding corner = steering.Corners[index];
                    Assert.That(steering.TryGetFreeYawBody(corner.Wheel, out Rigidbody body), Is.True);
                    Rigidbody anchor = body.GetComponent<HingeJoint>().connectedBody;
                    Assert.That(Vector3.Distance(anchor.position, anchor.transform.position), Is.LessThan(0.0001f),
                        "Recreated hinge captured a stale anchor position: " + corner.CornerId);
                    Assert.That(Quaternion.Angle(anchor.rotation, anchor.transform.rotation), Is.LessThan(0.02f),
                        "Recreated hinge captured a stale anchor rotation: " + corner.CornerId);
                    WritePhysicsDiagnostics("recreated before first physics", chassis, steering, corner, alignment[index]);
                }
                yield return FixedSteps(15);
                steering.ApplyNow();
                for (int index = 0; index < steering.Corners.Length; index++)
                {
                    AssertFreeYawWithinLimit(steering, steering.Corners[index], alignment[index],
                        "15 physics steps after same-frame recreation");
                }
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        private static GameObject CreateProductionPoseFixture()
        {
            GameObject prefab = Resources.Load<GameObject>("Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore("Private Satsuma baseline is unavailable.");
            }
            LegacySatsumaBaselineMetadata metadata = prefab.GetComponent<LegacySatsumaBaselineMetadata>();
            Assert.That(metadata, Is.Not.Null);
            GameObject instance = Object.Instantiate(prefab, metadata.DefaultWorldPosition, metadata.DefaultWorldRotation);
            Rigidbody chassis = instance.GetComponent<Rigidbody>();
            chassis.useGravity = false;
            chassis.constraints = RigidbodyConstraints.FreezeAll;
            VehicleSimulationHost simulation = instance.GetComponent<VehicleSimulationHost>();
            if (simulation != null)
            {
                simulation.enabled = false;
            }
            foreach (PartInstance part in instance.GetComponent<VehicleAssemblyController>().Parts)
            {
                if (part != null && !part.IsAssemblyRoot && part.Body != null)
                {
                    part.Body.isKinematic = true;
                    part.Body.detectCollisions = false;
                }
            }
            Assert.That(instance.GetComponent<SatsumaFrontSteeringController>(), Is.Not.Null);
            return instance;
        }

        private static float[] ReadAlignment(SatsumaFrontSteeringController steering) =>
            steering.Corners.Select(corner => corner.SteeringRodPresentation.Part
                .GetComponent<AssemblySteeringAlignmentState>().AlignmentDegrees).ToArray();

        private static void InstallForPoseChange(GameObject instance, bool includeRods)
        {
            VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
            Install(assembly, FindPart(assembly, "sub-frame"), FindMount(assembly, "sub-frame"));
            if (includeRods)
            {
                Install(assembly, FindPart(assembly, "steering-rack"), FindMount(assembly, "steering-rack"));
            }
            foreach (SatsumaFrontSuspensionCornerBinding corner in instance.GetComponent<SatsumaFrontSteeringController>().Corners)
            {
                Install(assembly, FindPart(assembly, "wishbone-" + corner.CornerId), corner.WishboneMount);
                TightenToInstallationThreshold(assembly, corner.WishboneMount);
                Install(assembly, FindPart(assembly, "spindle-" + corner.CornerId), corner.SpindleMount);
                TightenToInstallationThreshold(assembly, corner.SpindleMount);
                Install(assembly, FindPart(assembly, "strut-" + corner.CornerId), corner.StrutMount);
                if (includeRods)
                {
                    Install(assembly, FindPart(assembly, "steering-rod-" + corner.CornerId), corner.SteeringRodMount);
                }
            }
            instance.GetComponent<NwhAssemblyWheelSupportController>().RefreshSupport(force: true);
        }

        private static void SetChassisPose(Rigidbody chassis, Vector3 position, Quaternion rotation)
        {
            // The test moves a coherent chassis; it deliberately does not sync
            // the independent yaw anchor whose creation contract is under test.
            chassis.transform.SetPositionAndRotation(position, rotation);
            chassis.position = position;
            chassis.rotation = rotation;
        }

        private static void TurnOuterJointToStage(VehicleAssemblyController assembly,
            SatsumaFrontSuspensionCornerBinding corner, int stage)
        {
            MountPointRuntime mount = assembly.ResolveMount(corner.SteeringRodMount);
            FastenerInstance fastener = mount.Fasteners.Single();
            ToolDefinition tool = assembly.Tools.First(value => value != null && value.Size == fastener.Definition.Size);
            int remaining = fastener.Definition.MaximumStage + 1;
            while (fastener.Stage != stage)
            {
                Assert.That(remaining--, Is.GreaterThan(0));
                AssemblyOperationResult result = assembly.TryOperateFastener(mount.MountId,
                    fastener.Definition.DefinitionId, tool, fastener.Stage < stage);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }
        }

        private static void AssertFreeYawWithinLimit(SatsumaFrontSteeringController steering,
            SatsumaFrontSuspensionCornerBinding corner, float alignment, string phase)
        {
            Assert.That(steering.TryGetSteeringState(corner.Wheel, out bool connected, out float yaw), Is.True);
            Assert.That(connected, Is.False, phase + ": " + corner.CornerId);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(alignment, yaw)), Is.LessThanOrEqualTo(35f),
                phase + ": free carrier escaped donor +/-33 degree range, " + corner.CornerId);
            Assert.That(steering.TryGetFreeYawBody(corner.Wheel, out Rigidbody body), Is.True);
            Assert.That(Mathf.Abs(body.GetComponent<HingeJoint>().angle), Is.LessThanOrEqualTo(35f),
                phase + ": " + corner.CornerId);
        }

        private static IEnumerator RunSpawnScenario(Vector3 euler, bool useProductionMetadataPose = false)
        {
            GameObject prefab = Resources.Load<GameObject>("Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore("Private Satsuma baseline is unavailable.");
            }
            Vector3 position = new Vector3(0f, 2f, 0f);
            Quaternion rotation = Quaternion.Euler(euler);
            if (useProductionMetadataPose)
            {
                LegacySatsumaBaselineMetadata metadata = prefab.GetComponent<LegacySatsumaBaselineMetadata>();
                Assert.That(metadata, Is.Not.Null);
                // ProductionSatsumaInstaller uses these exact metadata fields.
                // Exercise its spawn pose without loading the entire Bootstrap.
                position = metadata.DefaultWorldPosition;
                rotation = metadata.DefaultWorldRotation;
            }

            GameObject instance = Object.Instantiate(prefab, position, rotation);
            try
            {
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                chassis.useGravity = false;
                chassis.constraints = RigidbodyConstraints.FreezeAll;
                // Do not rewrite chassis/anchor Rigidbody.rotation after
                // Instantiate: that would hide a rotated-spawn initialization bug.
                VehicleSimulationHost simulation = instance.GetComponent<VehicleSimulationHost>();
                if (simulation != null)
                {
                    simulation.enabled = false;
                }
                VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
                foreach (PartInstance part in assembly.Parts)
                {
                    if (part != null && !part.IsAssemblyRoot && part.Body != null)
                    {
                        part.Body.isKinematic = true;
                        part.Body.detectCollisions = false;
                    }
                }
                SatsumaFrontSteeringController steering = instance.GetComponent<SatsumaFrontSteeringController>();
                SatsumaFrontSuspensionController rig = instance.GetComponent<SatsumaFrontSuspensionController>();
                NwhAssemblyWheelSupportController support = instance.GetComponent<NwhAssemblyWheelSupportController>();
                Assert.That(steering, Is.Not.Null, "Regenerate the private steering-enabled prefab.");
                float[] initialAlignment = steering.Corners.Select(corner => corner.SteeringRodPresentation.Part
                    .GetComponent<AssemblySteeringAlignmentState>().AlignmentDegrees).ToArray();
                Quaternion[] initialStrutTop = steering.Corners.Select(corner =>
                    Quaternion.Inverse(instance.transform.rotation) * corner.StrutMount.Pose.rotation).ToArray();
                steering.ApplyNow();
                for (int index = 0; index < steering.Corners.Length; index++)
                {
                    WritePhysicsDiagnostics("before first physics", chassis, steering,
                        steering.Corners[index], initialAlignment[index]);
                }

                // The real player spends time around the bare chassis before
                // installing anything. Earlier tests installed at frame zero.
                yield return FixedSteps(15);
                float[] yawBeforeAssembly = new float[steering.Corners.Length];
                for (int index = 0; index < steering.Corners.Length; index++)
                {
                    Assert.That(steering.TryGetSteeringState(steering.Corners[index].Wheel,
                        out bool connected, out yawBeforeAssembly[index]), Is.True);
                    Assert.That(connected, Is.False);
                    WritePhysicsDiagnostics("after 15 pre-install physics steps", chassis,
                        steering, steering.Corners[index], initialAlignment[index]);
                }

                Install(assembly, FindPart(assembly, "sub-frame"), FindMount(assembly, "sub-frame"));
                foreach (SatsumaFrontSuspensionCornerBinding corner in steering.Corners)
                {
                    Install(assembly, FindPart(assembly, "wishbone-" + corner.CornerId), corner.WishboneMount);
                    TightenToInstallationThreshold(assembly, corner.WishboneMount);
                }
                support.RefreshSupport(force: true);
                yield return FixedSteps(5);

                foreach (SatsumaFrontSuspensionCornerBinding corner in steering.Corners)
                {
                    Install(assembly, FindPart(assembly, "spindle-" + corner.CornerId), corner.SpindleMount);
                    TightenToInstallationThreshold(assembly, corner.SpindleMount);
                    Install(assembly, FindPart(assembly, "strut-" + corner.CornerId), corner.StrutMount);
                    Assert.That(assembly.ResolveMount(corner.SteeringRodMount).IsOccupied, Is.False,
                        "No steering rod or connected-mode snap may rescue this scenario.");
                }
                support.RefreshSupport(force: true);
                yield return FixedSteps(15);
                steering.ApplyNow();
                rig.ApplyNow();

                for (int index = 0; index < steering.Corners.Length; index++)
                {
                    SatsumaFrontSuspensionCornerBinding corner = steering.Corners[index];
                    string label = $"{corner.CornerId}, spawnEuler={rotation.eulerAngles}, " +
                        $"initialToe={initialAlignment[index]}, preInstallYaw={yawBeforeAssembly[index]}";
                    Assert.That(steering.TryGetSteeringState(corner.Wheel, out bool connected, out float yaw), Is.True);
                    Assert.That(connected, Is.False, label);
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(initialAlignment[index], yaw)), Is.LessThanOrEqualTo(35f),
                        "Free carrier escaped the donor +/-33 degree range: " + label);
                    Assert.That(steering.TryGetFreeYawBody(corner.Wheel, out Rigidbody freeBody), Is.True, label);
                    Assert.That(Mathf.Abs(freeBody.GetComponent<HingeJoint>().angle), Is.LessThanOrEqualTo(35f), label);

                    // Independent expected body-relative heading: do not derive
                    // it from the potentially flipped NWH carrier we are testing.
                    Quaternion expectedHub = rotation * Quaternion.Euler(0f, initialAlignment[index], 0f) *
                        corner.FullDroopHubLocalRotation;
                    AssertNotFlipped(corner.SpindleMount.Pose.rotation,
                        expectedHub * corner.SpindleMeshLocalRotation, "spindle mount", label);
                    AssertNotFlipped(corner.ShockBottomTarget.rotation,
                        expectedHub * corner.ShockBottomLocalRotation, "strut lower connection", label);
                    AssertNotFlipped(corner.SteeringOuterTarget.rotation,
                        expectedHub * corner.SteeringOuterLocalRotation, "rod outer connection", label);
                    Assert.That(Quaternion.Angle(corner.StrutMount.Pose.rotation,
                        rotation * initialStrutTop[index]), Is.LessThan(0.1f), "Body-fixed strut top moved: " + label);
                    PartInstance spindle = assembly.ResolveMount(corner.SpindleMount).InstalledPart;
                    PartInstance strut = assembly.ResolveMount(corner.StrutMount).InstalledPart;
                    Assert.That(Quaternion.Angle(spindle.transform.rotation, corner.SpindleMount.Pose.rotation),
                        Is.LessThan(0.1f), "Installed spindle differs from its socket: " + label);
                    Assert.That(Quaternion.Angle(strut.transform.rotation, corner.StrutMount.Pose.rotation),
                        Is.LessThan(0.1f), "Installed strut differs from its socket: " + label);
                    Assert.That(assembly.ResolveMount(corner.StrutMount).Fasteners.All(value => value.Stage == 0),
                        Is.True, "Only prerequisite fasteners should have been tightened.");
                }
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        private static void AssertNotFlipped(Quaternion actual, Quaternion expected, string connection, string label) =>
            Assert.That(Quaternion.Angle(actual, expected), Is.LessThanOrEqualTo(40f),
                connection + " reversed relative to the chassis; allowance includes free yaw and camber: " + label);

        private static void WritePhysicsDiagnostics(string phase, Rigidbody chassis,
            SatsumaFrontSteeringController steering, SatsumaFrontSuspensionCornerBinding corner,
            float initialAlignment)
        {
            steering.TryGetSteeringState(corner.Wheel, out bool connected, out float yaw);
            Assert.That(steering.TryGetFreeYawBody(corner.Wheel, out Rigidbody body), Is.True);
            HingeJoint joint = body.GetComponent<HingeJoint>();
            Rigidbody anchor = joint.connectedBody;
            TestContext.WriteLine(
                $"{phase}; {corner.CornerId}; initialAlignment={initialAlignment:F5}; " +
                $"carrierYaw={yaw:F5}; relativeYaw={Mathf.DeltaAngle(initialAlignment, yaw):F5}; connected={connected}; " +
                $"chassisRB={chassis.rotation.ToString("F5")}; chassisTransform={chassis.transform.rotation.ToString("F5")}; " +
                $"wheelTransform={corner.Wheel.transform.rotation.ToString("F5")}; " +
                $"freeBodyRB={body.rotation.ToString("F5")}; freeBodyTransform={body.transform.rotation.ToString("F5")}; " +
                $"anchorRB={anchor.rotation.ToString("F5")}; anchorTransform={anchor.transform.rotation.ToString("F5")}; " +
                $"jointAngle={joint.angle:F5}");
        }

        private static IEnumerator FixedSteps(int count)
        {
            for (int step = 0; step < count; step++)
            {
                yield return new WaitForFixedUpdate();
            }
            yield return null;
        }

        private static void Install(VehicleAssemblyController assembly, PartInstance part, MountPointAuthoring mount)
        {
            part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            part.Body.position = mount.Pose.position;
            part.Body.rotation = mount.Pose.rotation;
            AssemblyOperationResult result = assembly.TryInstall(part, mount);
            Assert.That(result.Succeeded, Is.True, result.Message);
        }

        private static void TightenToInstallationThreshold(VehicleAssemblyController assembly, MountPointAuthoring authoring)
        {
            MountPointRuntime mount = assembly.ResolveMount(authoring);
            FastenerInstance fastener = mount.Fasteners.First();
            ToolDefinition tool = assembly.Tools.First(value => value != null && value.Size == fastener.Definition.Size);
            int attempts = 0;
            while (!mount.FastenerGroup.IsBolted)
            {
                Assert.That(attempts++, Is.LessThan(8), "The prerequisite never became bolted.");
                AssemblyOperationResult result = assembly.TryOperateFastener(mount.MountId,
                    fastener.Definition.DefinitionId, tool, tighten: true);
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
