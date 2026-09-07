using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.NWH;
using NWH.WheelController3D;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class SatsumaInstalledPartPhysicsPlayModeTests
    {
        // UnityTest can abort on an unexpected engine log without reaching the
        // iterator's finally block. Teardown therefore owns every created object
        // independently, including parts which installation/removal can reparent.
        private readonly List<GameObject> ownedObjects = new();
        private readonly Dictionary<GameObject, GameObject[]> ownedVehicleParts = new();

        [UnityTearDown]
        public IEnumerator FlushDeferredFixtureDestruction()
        {
            for (int index = ownedObjects.Count - 1; index >= 0; index--)
            {
                DeactivateAndDestroy(ownedObjects[index]);
            }
            ownedObjects.Clear();
            ownedVehicleParts.Clear();

            // Owner OnDestroy also destroys separate front-yaw helper bodies.
            // Flush both deferred generations before sharing the default PhysX
            // scene with the next fixture at the same coordinates.
            yield return null;
            yield return null;
            Physics.SyncTransforms();
        }

        private GameObject InstantiateOwnedSatsuma(
            GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject instance = Object.Instantiate(prefab, position, rotation);
            ownedObjects.Add(instance);
            VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
            GameObject[] parts = assembly != null
                ? assembly.Parts.Where(part => part != null)
                    .Select(part => part.gameObject).Distinct().ToArray()
                : new GameObject[0];
            ownedVehicleParts.Add(instance, parts);
            foreach (GameObject part in parts)
            {
                if (part != instance)
                {
                    ownedObjects.Add(part);
                }
            }
            return instance;
        }

        private GameObject CreateOwnedPrimitive(PrimitiveType type)
        {
            GameObject result = GameObject.CreatePrimitive(type);
            ownedObjects.Add(result);
            return result;
        }

        private void DestroyOwnedObject(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }
            if (ownedVehicleParts.TryGetValue(instance, out GameObject[] parts))
            {
                // The fresh-save test destroys its source before spawning the
                // restore target in the same test. Clear detached source parts
                // now, not only at teardown after they could affect the target.
                foreach (GameObject part in parts)
                {
                    if (part != null && part != instance &&
                        !part.transform.IsChildOf(instance.transform))
                    {
                        DeactivateAndDestroy(part);
                    }
                }
                ownedVehicleParts.Remove(instance);
            }
            DeactivateAndDestroy(instance);
        }

        private static void DeactivateAndDestroy(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }
            instance.SetActive(false);
            Object.Destroy(instance);
        }

        [UnityTest]
        public IEnumerator RearInstalledArmUsesPhysicalHingeAtDonorMount()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                PartInstance trailArm = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.trail-arm-rl");
                MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                    value.MountId == "mount.satsuma.trail-arm-rl");

                trailArm.transform.SetPositionAndRotation(
                    mount.Pose.position,
                    mount.Pose.rotation);
                trailArm.Body.position = mount.Pose.position;
                trailArm.Body.rotation = mount.Pose.rotation;
                AssemblyOperationResult result = assembly.TryInstall(
                    trailArm,
                    mount);
                Assert.That(result.Succeeded, Is.True, result.Message);

                yield return new WaitForFixedUpdate();

                Assert.That(trailArm.UsesDynamicInstalledPhysics, Is.True);
                Assert.That(trailArm.Body.isKinematic, Is.False);
                AssemblyInstalledPhysicsLink link = trailArm
                    .GetComponent<AssemblyInstalledPhysicsLink>();
                Assert.That(link, Is.Not.Null);
                Assert.That(
                    link.LinkMode,
                    Is.EqualTo(
                        AssemblyInstalledPhysicsLinkMode.TrailingArmHinge));
                HingeJoint hinge = trailArm.GetComponent<HingeJoint>();
                Assert.That(hinge, Is.Not.Null);
                Assert.That(
                    hinge.connectedBody,
                    Is.EqualTo(instance.GetComponent<Rigidbody>()));
                Assert.That(trailArm.GetComponent<FixedJoint>(), Is.Null);
                Assert.That(
                    trailArm.GetComponentsInChildren<Collider>(true).Any(value =>
                        value != null && value.enabled && !value.isTrigger &&
                        value.attachedRigidbody == trailArm.Body),
                    Is.True,
                    "The installed arm must retain solid collision for the " +
                    "physical suspension chain.");
                AssemblyInstalledPartInteractionProxy proxy = trailArm
                    .GetComponentInChildren<
                        AssemblyInstalledPartInteractionProxy>(true);
                Assert.That(proxy, Is.Not.Null);
                Assert.That(proxy.GetComponent<Collider>().enabled, Is.True);
                Assert.That(proxy.GetComponent<Collider>().isTrigger, Is.True);
            }
            finally
            {
                DestroyOwnedObject(instance);
            }
        }

        [UnityTest]
        public IEnumerator RearArmInteractionProxyStaysCompactAndRaycastable()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                PartInstance trailArm = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.trail-arm-rl");
                MountPointAuthoring mount = assembly.MountPoints.Single(value =>
                    value.MountId == "mount.satsuma.trail-arm-rl");

                trailArm.transform.SetPositionAndRotation(
                    mount.Pose.position,
                    mount.Pose.rotation);
                trailArm.Body.position = mount.Pose.position;
                trailArm.Body.rotation = mount.Pose.rotation;
                AssemblyOperationResult result = assembly.TryInstall(
                    trailArm,
                    mount);
                Assert.That(result.Succeeded, Is.True, result.Message);

                yield return new WaitForFixedUpdate();

                AssemblyInstalledPartInteractionProxy proxy = trailArm
                    .GetComponentInChildren<
                        AssemblyInstalledPartInteractionProxy>(true);
                BoxCollider proxyCollider = proxy != null
                    ? proxy.GetComponent<BoxCollider>()
                    : null;
                Assert.That(proxyCollider, Is.Not.Null);
                Assert.That(proxyCollider.enabled, Is.True);
                Assert.That(proxyCollider.isTrigger, Is.True);
                Assert.That(
                    proxyCollider.size.magnitude,
                    Is.LessThan(1.5f),
                    "Owned mounts must not inflate the arm raycast volume.");

                Vector3 center = proxy.transform.TransformPoint(
                    proxyCollider.center);
                Vector3 origin = proxy.transform.TransformPoint(
                    proxyCollider.center + Vector3.right *
                    (proxyCollider.size.x * 0.5f + 0.2f));
                Physics.SyncTransforms();
                Assert.That(
                    proxyCollider.Raycast(
                        new Ray(origin, (center - origin).normalized),
                        out _,
                        1f),
                    Is.True,
                    "The installed arm must remain raycastable from close range.");
            }
            finally
            {
                DestroyOwnedObject(instance);
            }
        }

        [UnityTest]
        public IEnumerator RearDrumUsesNwhKinematicPresentationAtExactArmOwnedDonorSocket()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                SatsumaRearSuspensionController presentation = instance
                    .GetComponent<SatsumaRearSuspensionController>();
                SatsumaRearNwhSuspensionController rearAuthority = instance
                    .GetComponent<SatsumaRearNwhSuspensionController>();
                PartInstance trailArm = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.trail-arm-rl");
                PartInstance drum = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.drum-brake-1");
                MountPointAuthoring armMount = assembly.MountPoints.Single(
                    value => value.MountId ==
                        "mount.satsuma.trail-arm-rl");
                MountPointAuthoring drumMount = assembly.MountPoints.Single(
                    value => value.MountId ==
                        "mount.satsuma.drum-brake-rl");

                InstallAtMount(assembly, trailArm, armMount);
                InstallAtMount(assembly, drum, drumMount);
                support.RefreshSupport(force: true);
                rearAuthority.ApplyNow();

                yield return new WaitForFixedUpdate();
                yield return null;
                rearAuthority.ApplyNow();

                Assert.That(support.Bindings[2].Enabled, Is.True);
                Assert.That(support.IsSupported(2), Is.True);
                Assert.That(support.Bindings[2].Wheel.enabled, Is.True);
                Assert.That(presentation.ExternalWheelAuthority, Is.True);
                Assert.That(rearAuthority, Is.Not.Null);
                Assert.That(rearAuthority.Corners.Length, Is.EqualTo(2));
                Assert.That(rearAuthority.Corners[0].CornerId, Is.EqualTo("rl"));
                Assert.That(trailArm.UsesDynamicInstalledPhysics, Is.False);
                Assert.That(trailArm.Body.isKinematic, Is.True);
                Assert.That(trailArm.GetComponent<HingeJoint>(), Is.Null);
                Assert.That(drum.UsesDynamicInstalledPhysics, Is.False);
                Assert.That(drum.Body.isKinematic, Is.True);
                Assert.That(drum.GetComponent<ConfigurableJoint>(), Is.Null);
                Assert.That(
                    trailArm.GetComponentsInChildren<Collider>(true).Any(value =>
                        value != null && value.enabled && !value.isTrigger &&
                        value.attachedRigidbody == trailArm.Body),
                    Is.False,
                    "The NWH-owned arm must not retain a second solid contact solver.");
                Assert.That(
                    drum.GetComponentsInChildren<Collider>(true).Any(value =>
                        value != null && value.enabled && !value.isTrigger &&
                        value.attachedRigidbody == drum.Body),
                    Is.False,
                    "The NWH drum contact profile, not the installed mesh collider, owns ground contact.");
                AssemblyInstalledPhysicsLink link = drum
                    .GetComponent<AssemblyInstalledPhysicsLink>();
                Assert.That(link, Is.Not.Null);
                Assert.That(
                    link.LinkMode,
                    Is.EqualTo(AssemblyInstalledPhysicsLinkMode.Fixed));
                Vector3 donorLocalPosition = trailArm.transform
                    .InverseTransformPoint(drum.transform.position);
                Assert.That(
                    Vector3.Distance(
                        donorLocalPosition,
                        new Vector3(
                            0.18000037f,
                            -0.31490782f,
                            -0.000143628f)),
                    Is.LessThan(0.004f),
                    "The moving drum must retain the reviewed arm-local hub centre. " +
                    "Part local: " + donorLocalPosition.ToString("F9") +
                    "; mount local: " + trailArm.transform.InverseTransformPoint(
                        drumMount.transform.position).ToString("F9") +
                    "; arm local: " + assembly.transform.InverseTransformPoint(
                        trailArm.transform.position).ToString("F9"));
            }
            finally
            {
                DestroyOwnedObject(instance);
            }
        }

        [UnityTest]
        public IEnumerator RearRoadWheelsUseDonorStandardMountPoseUnderNwhAuthority()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                SatsumaRearNwhSuspensionController rearAuthority = instance
                    .GetComponent<SatsumaRearNwhSuspensionController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                chassis.useGravity = false;
                chassis.constraints = RigidbodyConstraints.FreezeAll;

                string[] corners = { "rl", "rr" };
                string[] drumIds =
                {
                    "vehicle.satsuma.part.drum-brake-1",
                    "vehicle.satsuma.part.drum-brake-2",
                };
                string[] roadWheelIds =
                {
                    "vehicle.satsuma.part.wheel-stock-fr",
                    "vehicle.satsuma.part.wheel-gt-fl",
                };
                Vector3[] expectedOwnerPositions =
                {
                    new Vector3(
                        0.21999949f,
                        -0.31319135f,
                        -0.00074551045f),
                    new Vector3(
                        -0.22000039f,
                        -0.31318748f,
                        -0.0005988565f),
                };
                Quaternion[] expectedOwnerRotations =
                {
                    new Quaternion(
                        -0.6341037f,
                        0.00000061712484f,
                        -0.00000042188907f,
                        -0.77324796f),
                    new Quaternion(
                        -0.00000050477684f,
                        -0.752298f,
                        -0.65882295f,
                        -0.000001705379f),
                };
                Vector3 seatingOffset = new Vector3(-0.040f, 0f, 0f);

                for (int index = 0; index < corners.Length; index++)
                {
                    string corner = corners[index];
                    PartInstance arm = FindPart(
                        assembly,
                        "vehicle.satsuma.part.trail-arm-" + corner);
                    PartInstance drum = FindPart(assembly, drumIds[index]);
                    PartInstance wheel = FindPart(
                        assembly,
                        roadWheelIds[index]);
                    MountPointAuthoring drumMount = FindMount(
                        assembly,
                        "mount.satsuma.drum-brake-" + corner);
                    MountPointAuthoring wheelMount = FindMount(
                        assembly,
                        "mount.satsuma.wheel" + corner + "-new");

                    InstallAtMount(
                        assembly,
                        arm,
                        FindMount(
                            assembly,
                            "mount.satsuma.trail-arm-" + corner));
                    InstallAtMount(
                        assembly,
                        drum,
                        drumMount);
                    support.RefreshSupport(force: true);
                    rearAuthority.ApplyNow();
                    yield return new WaitForFixedUpdate();
                    yield return null;
                    rearAuthority.ApplyNow();

                    WheelController nwhWheel = support.Bindings[index + 2].Wheel;
                    Vector3 nwhLocalPositionBeforeWheelInstallation =
                        nwhWheel.transform.localPosition;
                    Quaternion nwhLocalRotationBeforeWheelInstallation =
                        nwhWheel.transform.localRotation;
                    InstallAtMount(assembly, wheel, wheelMount);
                    support.RefreshSupport(force: true);
                    rearAuthority.ApplyNow();

                    yield return new WaitForFixedUpdate();
                    yield return null;
                    rearAuthority.ApplyNow();

                    Assert.That(
                        Vector3.Distance(
                            wheelMount.transform.localPosition,
                            expectedOwnerPositions[index]),
                        Is.LessThan(0.00001f),
                        corner + " reviewed wheel-mount owner position");
                    Assert.That(
                        Quaternion.Angle(
                            wheelMount.transform.localRotation,
                            expectedOwnerRotations[index]),
                        Is.LessThan(0.001f),
                        corner + " reviewed wheel-mount owner orientation");
                    Assert.That(
                        Vector3.Distance(
                            wheelMount.Pose.localPosition,
                            seatingOffset),
                        Is.LessThan(0.00001f),
                        corner + " installed-wheel local X seating correction");
                    Assert.That(
                        Quaternion.Angle(
                            wheelMount.Pose.localRotation,
                            Quaternion.identity),
                        Is.LessThan(0.001f),
                        corner + " seating pose rotation");

                    Vector3 expectedInstalledPosition =
                        expectedOwnerPositions[index] +
                        expectedOwnerRotations[index] * seatingOffset;
                    float installedPositionError = Vector3.Distance(
                        arm.transform.InverseTransformPoint(
                            wheel.transform.position),
                        expectedInstalledPosition);
                    Assert.That(
                        installedPositionError,
                        Is.LessThan(0.001f),
                        corner + " donor-standard wheel installation centre");
                    Assert.That(
                        Quaternion.Angle(
                            Quaternion.Inverse(arm.transform.rotation) *
                                wheel.transform.rotation,
                            expectedOwnerRotations[index]),
                        Is.LessThan(0.25f),
                        corner + " reviewed wheel installation orientation");
                    AssertInstalledAtMount(wheel, wheelMount);
                    Assert.That(wheel.UsesDynamicInstalledPhysics, Is.False);
                    Assert.That(wheel.Body.isKinematic, Is.True);
                    Assert.That(wheel.GetComponent<HingeJoint>(), Is.Null, corner);
                    AssemblyInstalledPhysicsLink wheelLink = wheel
                        .GetComponent<AssemblyInstalledPhysicsLink>();
                    Assert.That(wheelLink, Is.Not.Null, corner);
                    Assert.That(
                        wheelLink.LinkMode,
                        Is.EqualTo(
                            AssemblyInstalledPhysicsLinkMode.RoadWheelAxle),
                        corner + " keeps its physical handoff metadata");
                    Assert.That(
                        wheel.GetComponentsInChildren<Collider>(true).Any(value =>
                            value != null && value.enabled && !value.isTrigger &&
                            value.attachedRigidbody == wheel.Body),
                        Is.False,
                        corner +
                        " installed mesh collider became a second contact solver");
                    Assert.That(
                        Vector3.Distance(
                            nwhWheel.transform.localPosition,
                            nwhLocalPositionBeforeWheelInstallation),
                        Is.LessThan(0.00001f),
                        corner +
                        " visual seating correction moved the NWH wheel anchor");
                    Assert.That(
                        Quaternion.Angle(
                            nwhWheel.transform.localRotation,
                            nwhLocalRotationBeforeWheelInstallation),
                        Is.LessThan(0.001f),
                        corner +
                        " visual seating correction rotated the NWH wheel anchor");

                    Transform nonRotating =
                        nwhWheel.wheel.nonRotatingContainer;
                    Transform rotating = nwhWheel.wheel.rotatingContainer;
                    Assert.That(nonRotating, Is.Not.Null, corner);
                    Assert.That(rotating, Is.Not.Null, corner);
                    AssemblyFastenerInteractionTarget drumFastener = drumMount
                        .GetComponentInChildren<
                            AssemblyFastenerInteractionTarget>(true);
                    AssemblyFastenerInteractionTarget wheelFastener = wheelMount
                        .GetComponentInChildren<
                            AssemblyFastenerInteractionTarget>(true);
                    Assert.That(drumFastener, Is.Not.Null, corner);
                    Assert.That(wheelFastener, Is.Not.Null, corner);

                    Vector3 drumMountPositionBeforeSpin =
                        drumMount.transform.position;
                    Vector3 wheelMountPositionBeforeSpin =
                        wheelMount.transform.position;
                    Vector3 wheelPosePositionBeforeSpin =
                        wheelMount.Pose.position;
                    Quaternion drumRotationBeforeSpin =
                        drum.transform.rotation;
                    Quaternion wheelRotationBeforeSpin =
                        wheel.transform.rotation;
                    Quaternion drumFastenerRotationBeforeSpin =
                        drumFastener.transform.rotation;
                    Quaternion wheelFastenerRotationBeforeSpin =
                        wheelFastener.transform.rotation;
                    Quaternion injectedRoll = Quaternion.AngleAxis(
                        37f,
                        rotating.TransformDirection(Vector3.right));
                    rotating.rotation = injectedRoll * rotating.rotation;

                    rearAuthority.ApplyNow();

                    Assert.That(
                        Quaternion.Angle(
                            drum.transform.rotation,
                            injectedRoll * drumRotationBeforeSpin),
                        Is.LessThan(0.1f),
                        corner + " drum discarded NWH wheel spin");
                    Assert.That(
                        Quaternion.Angle(
                            wheel.transform.rotation,
                            injectedRoll * wheelRotationBeforeSpin),
                        Is.LessThan(0.1f),
                        corner + " road wheel discarded NWH wheel spin");
                    Assert.That(
                        Quaternion.Angle(
                            drumFastener.transform.rotation,
                            injectedRoll * drumFastenerRotationBeforeSpin),
                        Is.LessThan(0.1f),
                        corner + " drum fastener discarded NWH wheel spin");
                    Assert.That(
                        Quaternion.Angle(
                            wheelFastener.transform.rotation,
                            injectedRoll * wheelFastenerRotationBeforeSpin),
                        Is.LessThan(0.1f),
                        corner + " wheel fastener discarded NWH wheel spin");
                    Assert.That(
                        Vector3.Distance(
                            drumMount.transform.position,
                            drumMountPositionBeforeSpin),
                        Is.LessThan(0.0001f),
                        corner + " drum centre orbited while rolling");
                    Assert.That(
                        Vector3.Distance(
                            wheelMount.transform.position,
                            wheelMountPositionBeforeSpin),
                        Is.LessThan(0.0001f),
                        corner + " wheel mount centre orbited while rolling");
                    Assert.That(
                        Vector3.Distance(
                            wheelMount.Pose.position,
                            wheelPosePositionBeforeSpin),
                        Is.LessThan(0.0001f),
                        corner + " accepted -0.040 m seating moved while rolling");

                    Quaternion drumRotationAfterSpin =
                        drum.transform.rotation;
                    Quaternion wheelRotationAfterSpin =
                        wheel.transform.rotation;
                    Quaternion drumFastenerRotationAfterSpin =
                        drumFastener.transform.rotation;
                    Quaternion wheelFastenerRotationAfterSpin =
                        wheelFastener.transform.rotation;

                    rearAuthority.ApplyNow();

                    Assert.That(
                        Quaternion.Angle(
                            drum.transform.rotation,
                            drumRotationAfterSpin),
                        Is.LessThan(0.01f),
                        corner + " accumulated drum roll on a stable NWH pose");
                    Assert.That(
                        Quaternion.Angle(
                            wheel.transform.rotation,
                            wheelRotationAfterSpin),
                        Is.LessThan(0.01f),
                        corner + " accumulated wheel roll on a stable NWH pose");
                    Assert.That(
                        Quaternion.Angle(
                            drumFastener.transform.rotation,
                            drumFastenerRotationAfterSpin),
                        Is.LessThan(0.01f),
                        corner + " accumulated drum-fastener roll");
                    Assert.That(
                        Quaternion.Angle(
                            wheelFastener.transform.rotation,
                            wheelFastenerRotationAfterSpin),
                        Is.LessThan(0.01f),
                        corner + " accumulated wheel-fastener roll");
                    AssertInstalledAtMount(drum, drumMount);
                    AssertInstalledAtMount(wheel, wheelMount);
                    Assert.That(drum.UsesDynamicInstalledPhysics, Is.False);
                    Assert.That(wheel.UsesDynamicInstalledPhysics, Is.False);
                }
            }
            finally
            {
                DestroyOwnedObject(instance);
            }
        }

        [UnityTest]
        public IEnumerator RearArmSpringAndShockUseNwhAuthorityAtLockedMounts()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                SatsumaRearSuspensionController presentation = instance
                    .GetComponent<SatsumaRearSuspensionController>();
                SatsumaRearNwhSuspensionController rearAuthority = instance
                    .GetComponent<SatsumaRearNwhSuspensionController>();
                PartInstance chassis = assembly.Parts.Single(value =>
                    value.IsAssemblyRoot);
                PartInstance trailArm = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.trail-arm-rl");
                PartInstance drum = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.drum-brake-1");
                PartInstance spring = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.coil-spring-1");
                MountPointAuthoring armMount = assembly.MountPoints.Single(
                    value => value.MountId ==
                        "mount.satsuma.trail-arm-rl");
                MountPointAuthoring drumMount = assembly.MountPoints.Single(
                    value => value.MountId ==
                        "mount.satsuma.drum-brake-rl");
                MountPointAuthoring springMount = assembly.MountPoints.Single(
                    value => value.MountId ==
                        "mount.satsuma.coilspring-rl");
                MountPointAuthoring shockMount = assembly.MountPoints.Single(
                    value => value.MountId ==
                        "mount.satsuma.shock-rl");
                PartInstance shock = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.shock-absorber-1");

                InstallAtMount(assembly, trailArm, armMount);
                InstallAtMount(assembly, drum, drumMount);
                InstallAtMount(assembly, spring, springMount);
                InstallAtMount(assembly, shock, shockMount);
                support.RefreshSupport(force: true);
                rearAuthority.ApplyNow();

                Assert.That(support.Bindings[2].Enabled, Is.True);
                Assert.That(support.IsSupported(2), Is.True);
                Assert.That(support.Bindings[2].Wheel.enabled, Is.True);
                Assert.That(presentation, Is.Not.Null);
                Assert.That(presentation.ExternalWheelAuthority, Is.True);
                Assert.That(rearAuthority, Is.Not.Null);
                Assert.That(rearAuthority.Corners.Length, Is.EqualTo(2));
                Assert.That(rearAuthority.Corners[0].CornerId, Is.EqualTo("rl"));
                Assert.That(
                    rearAuthority.Corners[0].Wheel,
                    Is.SameAs(support.Bindings[2].Wheel));
                Assert.That(
                    instance.GetComponentsInChildren<
                        SatsumaRearSuspensionPartPresentation>(true).Length,
                    Is.GreaterThanOrEqualTo(2),
                    "The installed spring and shock must retain their runtime presentations.");

                yield return null;
                for (int step = 0; step < 5; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                yield return null;
                // Unity batchmode has no rendered end-of-frame callback. Apply
                // the same public synchronization pass that LateUpdate runs in
                // the player before measuring the final installed presentation.
                assembly.SynchronizeInstalledParts();
                rearAuthority.ApplyNow();

                Assert.That(trailArm.UsesDynamicInstalledPhysics, Is.False);
                Assert.That(trailArm.Body.isKinematic, Is.True);
                Assert.That(trailArm.GetComponent<HingeJoint>(), Is.Null);
                AssemblyInstalledPhysicsLink armLink = trailArm
                    .GetComponent<AssemblyInstalledPhysicsLink>();
                Assert.That(armLink, Is.Not.Null);
                Assert.That(
                    armLink.LinkMode,
                    Is.EqualTo(
                        AssemblyInstalledPhysicsLinkMode.TrailingArmHinge));
                Assert.That(
                    Vector3.Distance(
                        trailArm.transform.position,
                        armMount.Pose.position),
                    Is.LessThan(0.006f),
                    "The NWH presentation pivot must remain on the reviewed chassis mount.");
                Assert.That(drum.UsesDynamicInstalledPhysics, Is.False);
                Assert.That(drum.Body.isKinematic, Is.True);
                Assert.That(
                    drum.GetComponent<ConfigurableJoint>(),
                    Is.Null);
                AssemblyInstalledPhysicsLink drumLink = drum
                    .GetComponent<AssemblyInstalledPhysicsLink>();
                Assert.That(drumLink, Is.Not.Null);
                Assert.That(
                    drumLink.LinkMode,
                    Is.EqualTo(AssemblyInstalledPhysicsLinkMode.Fixed));
                Assert.That(
                    trailArm.GetComponentsInChildren<Collider>(true).Any(value =>
                        value != null && value.enabled && !value.isTrigger &&
                        value.attachedRigidbody == trailArm.Body),
                    Is.False);
                Assert.That(
                    drum.GetComponentsInChildren<Collider>(true).Any(value =>
                        value != null && value.enabled && !value.isTrigger &&
                        value.attachedRigidbody == drum.Body),
                    Is.False);
                AssertInstalledAtMount(spring, springMount);
                AssertInstalledAtMount(shock, shockMount);
                Assert.That(
                    Vector3.Distance(
                        instance.transform.InverseTransformPoint(
                            spring.transform.position),
                        new Vector3(
                            -0.4300001f,
                            -0.079f,
                            -0.98564017f)),
                    Is.LessThan(0.005f));
                Assert.That(
                    Vector3.Distance(
                        instance.transform.InverseTransformPoint(
                            shock.transform.position),
                        new Vector3(-0.47f, 0.14f, -1.1500002f)),
                    Is.LessThan(0.005f));
            }
            finally
            {
                DestroyOwnedObject(instance);
            }
        }

        [UnityTest]
        public IEnumerator RearArmWithoutSpringRemainsDynamicAndContactsGround()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            GameObject ground = null;
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                chassis.useGravity = false;
                chassis.constraints = RigidbodyConstraints.FreezeAll;
                PartInstance arm = FindPart(
                    assembly,
                    "vehicle.satsuma.part.trail-arm-rl");
                MountPointAuthoring armMount = FindMount(
                    assembly,
                    "mount.satsuma.trail-arm-rl");
                MountPointAuthoring drumMount = FindMount(
                    assembly,
                    "mount.satsuma.drum-brake-rl");
                InstallAtMount(assembly, arm, armMount);
                yield return new WaitForFixedUpdate();

                Collider solid = FindEnabledSolidCollider(arm);
                HingeJoint hinge = arm.GetComponent<HingeJoint>();
                Assert.That(arm.UsesDynamicInstalledPhysics, Is.True);
                Assert.That(arm.Body.useGravity, Is.True);
                Assert.That(hinge, Is.Not.Null);

                ground = CreateOwnedPrimitive(PrimitiveType.Cube);
                ground.name = "Springless rear-arm contact ground";
                ground.transform.localScale = new Vector3(1f, 0.02f, 1f);
                float groundTop = solid.bounds.min.y - 0.02f;
                ground.transform.position = new Vector3(
                    solid.bounds.center.x,
                    groundTop - 0.01f,
                    solid.bounds.center.z);
                Physics.SyncTransforms();
                Assert.That(
                    Physics.GetIgnoreCollision(
                        solid,
                        ground.GetComponent<Collider>()),
                    Is.False,
                    "A springless trail arm was filtered out of world contact.");

                float startAngle = hinge.angle;
                for (int step = 0; step < 60; step++)
                {
                    arm.Body.AddForceAtPosition(
                        Vector3.down * 120f,
                        drumMount.Pose.position,
                        ForceMode.Force);
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(
                    Mathf.Abs(Mathf.DeltaAngle(startAngle, hinge.angle)),
                    Is.GreaterThan(0.05f),
                    "The springless trail arm stayed a frozen presentation " +
                    "object instead of rotating on its physical hinge.");
                Assert.That(
                    solid.bounds.min.y,
                    Is.GreaterThanOrEqualTo(groundTop - 0.012f),
                    "The physical trail arm fell through the ground before " +
                    "a spring was installed. Colliders: " +
                    DescribeColliders(arm));
            }
            finally
            {
                DestroyOwnedObject(instance);
                DestroyOwnedObject(ground);
            }
        }

        [UnityTest]
        public IEnumerator FrontStructureKeepsWeakContactBeforeAndAfterStrutRoundTrip()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                chassis.useGravity = false;
                chassis.constraints = RigidbodyConstraints.FreezeAll;
                PartInstance subframe = FindPart(
                    assembly,
                    "vehicle.satsuma.part.sub-frame");
                PartInstance wishbone = FindPart(
                    assembly,
                    "vehicle.satsuma.part.wishbone-fl");
                PartInstance spindle = FindPart(
                    assembly,
                    "vehicle.satsuma.part.spindle-fl");
                PartInstance strut = FindPart(
                    assembly,
                    "vehicle.satsuma.part.strut-fl");

                InstallAtMount(
                    assembly,
                    subframe,
                    FindMount(assembly, "mount.satsuma.sub-frame"));
                InstallAtMount(
                    assembly,
                    wishbone,
                    FindMount(assembly, "mount.satsuma.wishbone-fl"));
                InstallAtMount(
                    assembly,
                    spindle,
                    FindMount(assembly, "mount.satsuma.spindle-fl"));
                yield return new WaitForFixedUpdate();
                yield return null;

                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                SatsumaFrontSuspensionController rig = instance
                    .GetComponent<SatsumaFrontSuspensionController>();
                support.RefreshSupport(force: true);
                rig.ApplyNow();
                Assert.That(support.IsSupported(0), Is.True,
                    "The wishbone must retain donor weak contact without a strut.");
                Assert.That(support.IsSupported(1), Is.False,
                    "A missing wishbone must not create invisible support.");
                AssertNoStrutContactProfile(support.Bindings[0].Wheel);
                Assert.That(support.AllowsAxleStability(
                    support.Bindings[0].Wheel), Is.False);
                Assert.That(subframe.UsesDynamicInstalledPhysics, Is.True);
                Assert.That(wishbone.UsesDynamicInstalledPhysics, Is.False);
                Assert.That(spindle.UsesDynamicInstalledPhysics, Is.False);
                Assert.That(
                    subframe.GetComponent<ConfigurableJoint>().connectedBody,
                    Is.SameAs(chassis));
                Assert.That(
                    wishbone.GetComponent<AssemblyInstalledPhysicsLink>(),
                    Is.Null,
                    "Donor installed wishbone is transform IK, not a " +
                    "gravity-driven hinge.");
                Assert.That(
                    spindle.GetComponent<AssemblyInstalledPhysicsLink>(),
                    Is.Null,
                    "Donor installed spindle is kinematic presentation.");
                AssertInstalledAtMount(
                    wishbone,
                    FindMount(assembly, "mount.satsuma.wishbone-fl"));
                AssertInstalledAtMount(
                    spindle,
                    FindMount(assembly, "mount.satsuma.spindle-fl"));
                Assert.That(
                    spindle.GetComponentsInChildren<Collider>(true).Any(value =>
                        value != null && value.enabled && !value.isTrigger &&
                        value.attachedRigidbody == spindle.Body),
                    Is.False,
                    "The donor IK spindle retained a competing solid actor.");

                InstallAtMount(
                    assembly,
                    strut,
                    FindMount(assembly, "mount.satsuma.strut-fl"));
                support.RefreshSupport(force: true);
                rig.ApplyNow();
                yield return new WaitForFixedUpdate();

                Assert.That(support.IsSupported(0), Is.True);
                Assert.That(support.AllowsAxleStability(
                    support.Bindings[0].Wheel), Is.True);
                Assert.That(subframe.UsesDynamicInstalledPhysics, Is.True,
                    "The subframe must remain a solid force path to the shell.");
                Assert.That(wishbone.UsesDynamicInstalledPhysics, Is.False);
                Assert.That(spindle.UsesDynamicInstalledPhysics, Is.False);
                Assert.That(wishbone.Body.isKinematic, Is.True);
                Assert.That(spindle.Body.isKinematic, Is.True);
                Assert.That(
                    spindle.GetComponentsInChildren<Collider>(true).Any(value =>
                        value != null && value.enabled && !value.isTrigger &&
                        value.attachedRigidbody == spindle.Body),
                    Is.False,
                    "The completed NWH corner retained a second solid spindle " +
                    "simulation and can fight the wheel backend.");

                LoosenMountBelowBoltedThreshold(
                    assembly,
                    FindMount(assembly, "mount.satsuma.strut-fl"));
                AssemblyOperationResult removal = assembly.TryRemove(strut);
                Assert.That(
                    removal.Succeeded,
                    Is.True,
                    "The regression requires a complete strut remove " +
                    "round-trip. " + removal.FailureReason + ": " +
                    removal.Message);
                support.RefreshSupport(force: true);
                rig.ApplyNow();
                yield return new WaitForFixedUpdate();
                yield return null;

                Assert.That(strut.IsInstalled, Is.False);
                Assert.That(support.IsSupported(0), Is.True,
                    "Removing the strut must restore weak contact, not disable it.");
                AssertNoStrutContactProfile(support.Bindings[0].Wheel);
                Assert.That(support.AllowsAxleStability(
                    support.Bindings[0].Wheel), Is.False);
                AssertInstalledAtMount(
                    wishbone,
                    FindMount(assembly, "mount.satsuma.wishbone-fl"));
                AssertInstalledAtMount(
                    spindle,
                    FindMount(assembly, "mount.satsuma.spindle-fl"));

                MountPointAuthoring spindleMount = FindMount(
                    assembly,
                    "mount.satsuma.spindle-fl");
                if (assembly.ResolveMount(spindleMount).FastenerGroup
                    .Definition.HasFasteners)
                {
                    LoosenMountBelowBoltedThreshold(assembly, spindleMount);
                }
                AssemblyOperationResult spindleRemoval =
                    assembly.TryRemove(spindle);
                Assert.That(spindleRemoval.Succeeded, Is.True,
                    spindleRemoval.Message);
                support.RefreshSupport(force: true);
                Assert.That(support.IsSupported(0), Is.True,
                    "A wishbone alone still has donor contact.");

                LoosenMountBelowBoltedThreshold(
                    assembly,
                    FindMount(assembly, "mount.satsuma.wishbone-fl"));
                AssemblyOperationResult wishboneRemoval =
                    assembly.TryRemove(wishbone);
                Assert.That(wishboneRemoval.Succeeded, Is.True,
                    wishboneRemoval.Message);
                support.RefreshSupport(force: true);
                yield return null;
                yield return new WaitForFixedUpdate();
                Assert.That(support.IsSupported(0), Is.False);
                MeshCollider contactCollider =
                    support.Bindings[0].Wheel.wheel.meshCollider;
                Assert.That(contactCollider == null ||
                    !contactCollider.enabled ||
                    !contactCollider.gameObject.activeInHierarchy, Is.True,
                    "Removing the wishbone left an invisible NWH solid collider.");
            }
            finally
            {
                DestroyOwnedObject(instance);
            }
        }

        [UnityTest]
        public IEnumerator FrontWishbonesFollowDonorNoStrutAirborneTarget()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            Quaternion vehicleTilt = Quaternion.Euler(31f, 19f, 47f);
            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                vehicleTilt);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                chassis.useGravity = false;
                chassis.constraints = RigidbodyConstraints.FreezeAll;

                PartInstance subframe = FindPart(
                    assembly,
                    "vehicle.satsuma.part.sub-frame");
                InstallAtMount(
                    assembly,
                    subframe,
                    FindMount(assembly, "mount.satsuma.sub-frame"));

                PartInstance[] wishbones = new PartInstance[2];
                PartInstance[] spindles = new PartInstance[2];
                string[] corners = { "fl", "fr" };
                for (int index = 0; index < corners.Length; index++)
                {
                    string corner = corners[index];
                    wishbones[index] = FindPart(
                        assembly,
                        "vehicle.satsuma.part.wishbone-" + corner);
                    spindles[index] = FindPart(
                        assembly,
                        "vehicle.satsuma.part.spindle-" + corner);
                    InstallAtMount(
                        assembly,
                        wishbones[index],
                        FindMount(
                            assembly,
                            "mount.satsuma.wishbone-" + corner));
                    InstallAtMount(
                        assembly,
                        spindles[index],
                        FindMount(
                            assembly,
                            "mount.satsuma.spindle-" + corner));
                }

                SatsumaFrontSuspensionController rig = instance
                    .GetComponent<SatsumaFrontSuspensionController>();
                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                support.RefreshSupport(force: true);
                // NWH initializes at 70% extension and extends over fixed
                // steps. This fixture deliberately proves only airborne IK.
                for (int step = 0; step < 45; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                yield return null;
                rig.ApplyNow();
                Vector3[] initialPositions = new Vector3[corners.Length];
                for (int index = 0; index < corners.Length; index++)
                {
                    SatsumaFrontSuspensionCornerBinding binding = rig.Corners
                        .Single(value => value.CornerId == corners[index]);
                    Assert.That(
                        wishbones[index]
                            .GetComponent<AssemblyInstalledPhysicsLink>(),
                        Is.Null);
                    Assert.That(
                        spindles[index]
                            .GetComponent<AssemblyInstalledPhysicsLink>(),
                        Is.Null);
                    Assert.That(wishbones[index].UsesDynamicInstalledPhysics,
                        Is.False);
                    Assert.That(spindles[index].UsesDynamicInstalledPhysics,
                        Is.False);
                    Assert.That(
                        FindPart(
                            assembly,
                            "vehicle.satsuma.part.strut-" + corners[index])
                            .IsInstalled,
                        Is.False,
                        "This regression only covers the donor no-strut " +
                        "front-corner pose while airborne.");
                    Assert.That(binding.Wheel.enabled, Is.True);
                    Assert.That(binding.Wheel.IsGrounded, Is.False);
                    AssertNoStrutContactProfile(binding.Wheel);
                    Assert.That(
                        instance.transform.InverseTransformPoint(
                            binding.Wheel.WheelPosition).y,
                        Is.EqualTo(-0.35f).Within(0.003f),
                        corners[index] + " donor no-strut full-droop height");
                    Assert.That(Vector3.Distance(binding.Wheel.WheelPosition,
                            ExpectedAirborneHubPosition(instance.transform, binding)),
                        Is.LessThan(0.003f));
                    // The donor camber and independent free yaw move the IK
                    // endpoint. The old 29.899-degree constant assumed both
                    // angles were zero; keep the actual mesh-to-hub relation strict.
                    Quaternion expectedRotation = ExpectedFrontWishboneRotation(
                        assembly.transform, binding);
                    Assert.That(
                        Quaternion.Angle(
                            binding.WishboneMount.Pose.rotation,
                            expectedRotation),
                        Is.LessThan(0.01f));
                    AssertInstalledAtMount(
                        wishbones[index],
                        binding.WishboneMount);
                    AssertInstalledAtMount(
                        spindles[index],
                        binding.SpindleMount);
                    initialPositions[index] =
                        wishbones[index].transform.localPosition;
                }

                for (int step = 0; step < 45; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                yield return null;
                rig.ApplyNow();
                for (int index = 0; index < corners.Length; index++)
                {
                    Assert.That(
                        Vector3.Distance(
                            wishbones[index].transform.localPosition,
                            initialPositions[index]),
                        Is.LessThan(0.00001f),
                        corners[index] + " no-strut wishbone drifted");
                    Assert.That(
                        Quaternion.Angle(
                            wishbones[index].transform.rotation,
                            ExpectedFrontWishboneRotation(
                                assembly.transform,
                                rig.Corners.Single(value => value.CornerId == corners[index]))),
                        Is.LessThan(0.01f),
                        corners[index] + " no-strut wishbone drifted away " +
                        "from its current physical hub endpoint");
                }
            }
            finally
            {
                DestroyOwnedObject(instance);
            }
        }

        [UnityTest]
        public IEnumerator FrontNoStrutWishboneAndSpindleRespondToRaisedGroundOnBothSides()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 2f, 0f),
                Quaternion.identity);
            GameObject[] patches = new GameObject[2];
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                chassis.useGravity = false;
                chassis.constraints = RigidbodyConstraints.FreezeAll;
                SuppressLoosePartPhysics(assembly);
                InstallAtMount(
                    assembly,
                    FindPart(assembly, "vehicle.satsuma.part.sub-frame"),
                    FindMount(assembly, "mount.satsuma.sub-frame"));

                SatsumaFrontSuspensionController rig = instance
                    .GetComponent<SatsumaFrontSuspensionController>();
                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                for (int index = 0; index < rig.Corners.Length; index++)
                {
                    SatsumaFrontSuspensionCornerBinding corner =
                        rig.Corners[index];
                    InstallAtMount(
                        assembly,
                        FindPart(assembly,
                            "vehicle.satsuma.part.wishbone-" + corner.CornerId),
                        corner.WishboneMount);
                    Vector3 airborneHub = instance.transform.TransformPoint(
                        corner.CanonicalNoStrutHubLocalPosition);
                    patches[index] = CreateFrontContactPatch(
                        airborneHub.x,
                        airborneHub.z,
                        topY: airborneHub.y - 1f);
                }

                support.RefreshSupport(force: true);
                for (int step = 0; step < 45; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                yield return null;
                // Both assembly stages must articulate, even before a spindle
                // exists. A fixed airborne pose would pass the old test only.
                for (int stage = 0; stage < 2; stage++)
                {
                    if (stage == 1)
                    {
                        foreach (SatsumaFrontSuspensionCornerBinding corner in
                                 rig.Corners)
                        {
                            InstallAtMount(
                                assembly,
                                FindPart(assembly,
                                    "vehicle.satsuma.part.spindle-" +
                                    corner.CornerId),
                                corner.SpindleMount);
                        }

                        support.RefreshSupport(force: true);
                    }

                    Quaternion[] airborneArmRotations = new Quaternion[2];
                    for (int index = 0; index < rig.Corners.Length; index++)
                    {
                        SatsumaFrontSuspensionCornerBinding corner =
                            rig.Corners[index];
                        WheelController wheel = corner.Wheel;
                        AssertNoStrutContactProfile(wheel);
                        Assert.That(support.AllowsAxleStability(wheel), Is.False);
                        Assert.That(wheel.enabled, Is.True);
                        Assert.That(wheel.IsGrounded, Is.False);
                        Vector3 airborneHub = ExpectedAirborneHubPosition(
                            instance.transform, corner);
                        Assert.That(Vector3.Distance(wheel.WheelPosition,
                            airborneHub), Is.LessThan(0.003f));
                        airborneArmRotations[index] =
                            corner.WishboneMount.Pose.rotation;
                        float raisedTop = airborneHub.y - wheel.Radius + 0.10f;
                        patches[index].transform.position = new Vector3(
                            airborneHub.x,
                            raisedTop - 0.05f,
                            airborneHub.z);
                    }

                    Physics.SyncTransforms();
                    for (int step = 0; step < 12; step++)
                    {
                        yield return new WaitForFixedUpdate();
                    }

                    yield return null;
                    rig.ApplyNow();
                    for (int index = 0; index < rig.Corners.Length; index++)
                    {
                        SatsumaFrontSuspensionCornerBinding corner =
                            rig.Corners[index];
                        WheelController wheel = corner.Wheel;
                        BoxCollider patch = patches[index]
                            .GetComponent<BoxCollider>();
                        string context = corner.CornerId +
                            (stage == 0 ? " wishbone-only" : " with spindle");
                        Assert.That(patches[index].layer, Is.EqualTo(0),
                            "The real driveway uses Default, not only WorldSurface.");
                        Assert.That(wheel.IsGrounded, Is.True, context);
                        Assert.That(wheel.HitCollider, Is.SameAs(patch),
                            context + " contact came from a different actor");
                        Assert.That(wheel.SpringLength,
                            Is.EqualTo(0.10f).Within(0.012f), context);
                        Assert.That(wheel.Load, Is.GreaterThan(0.01f), context);
                        Assert.That(wheel.Load, Is.LessThan(0.5f),
                            context + " unexpectedly acquired a real strut force");
                        Assert.That(wheel.WheelPosition.y - wheel.Radius,
                            Is.GreaterThanOrEqualTo(patch.bounds.max.y - 0.012f),
                            context + " contact hub penetrated the ground");
                        Assert.That(Quaternion.Angle(
                            corner.WishboneMount.Pose.rotation,
                            airborneArmRotations[index]), Is.GreaterThan(5f),
                            context + " remained frozen at airborne droop");
                        AssertInstalledAtMount(
                            FindPart(assembly,
                                "vehicle.satsuma.part.wishbone-" +
                                corner.CornerId),
                            corner.WishboneMount);
                        if (stage == 1)
                        {
                            Transform nonRotating = wheel.wheel
                                .nonRotatingContainer;
                            Vector3 expectedSpindle = wheel.WheelPosition +
                                nonRotating.rotation *
                                corner.HubToSpindleMeshLocalOffset;
                            Assert.That(Vector3.Distance(
                                corner.SpindleMount.Pose.position,
                                expectedSpindle), Is.LessThan(0.004f), context);
                            AssertInstalledAtMount(
                                FindPart(assembly,
                                    "vehicle.satsuma.part.spindle-" +
                                    corner.CornerId),
                                corner.SpindleMount);
                        }

                        patches[index].transform.position -= Vector3.up;
                    }

                    Physics.SyncTransforms();
                    for (int step = 0; step < 45; step++)
                    {
                        yield return new WaitForFixedUpdate();
                    }

                    yield return null;
                    rig.ApplyNow();
                    for (int index = 0; index < rig.Corners.Length; index++)
                    {
                        SatsumaFrontSuspensionCornerBinding corner =
                            rig.Corners[index];
                        Assert.That(corner.Wheel.IsGrounded, Is.False);
                        Assert.That(corner.Wheel.Load, Is.EqualTo(0f));
                        Assert.That(Vector3.Distance(
                            corner.Wheel.WheelPosition,
                            ExpectedAirborneHubPosition(instance.transform, corner)),
                            Is.LessThan(0.003f));
                        Assert.That(Quaternion.Angle(
                            corner.WishboneMount.Pose.rotation,
                            airborneArmRotations[index]), Is.LessThan(0.1f));
                    }
                }
            }
            finally
            {
                DestroyOwnedObject(instance);
                foreach (GameObject patch in patches)
                {
                    DestroyOwnedObject(patch);
                }
            }
        }

        [UnityTest]
        public IEnumerator FrontNoStrutDynamicChassisCompressesWithoutPhantomSpringLaunch()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            const float initialHeight = 0.62f;
            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, initialHeight, 0f),
                Quaternion.identity);
            GameObject[] patches = new GameObject[2];
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                chassis.useGravity = false;
                chassis.constraints = RigidbodyConstraints.FreezeAll;
                SuppressLoosePartPhysics(assembly);
                PartInstance subframe = FindPart(
                    assembly,
                    "vehicle.satsuma.part.sub-frame");
                InstallAtMount(assembly, subframe,
                    FindMount(assembly, "mount.satsuma.sub-frame"));
                SatsumaFrontSuspensionController rig = instance
                    .GetComponent<SatsumaFrontSuspensionController>();
                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                for (int index = 0; index < rig.Corners.Length; index++)
                {
                    SatsumaFrontSuspensionCornerBinding corner =
                        rig.Corners[index];
                    InstallAtMount(assembly,
                        FindPart(assembly,
                            "vehicle.satsuma.part.wishbone-" + corner.CornerId),
                        corner.WishboneMount);
                    InstallAtMount(assembly,
                        FindPart(assembly,
                            "vehicle.satsuma.part.spindle-" + corner.CornerId),
                        corner.SpindleMount);
                    Vector3 hub = instance.transform.TransformPoint(
                        corner.CanonicalNoStrutHubLocalPosition);
                    patches[index] = CreateFrontContactPatch(
                        hub.x, hub.z, topY: 0f);
                    Collider patchCollider = patches[index]
                        .GetComponent<Collider>();
                    // Isolate the corner's force path. These small patches
                    // must not support the body/subframe directly and hide a
                    // disabled wheel solver as a false success.
                    foreach (Collider bodyCollider in rig.ChassisColliders)
                    {
                        if (bodyCollider != null)
                        {
                            Physics.IgnoreCollision(patchCollider, bodyCollider);
                        }
                    }

                    foreach (Collider subframeCollider in subframe
                                 .GetComponentsInChildren<Collider>(true))
                    {
                        if (subframeCollider != null &&
                            subframeCollider.attachedRigidbody == subframe.Body)
                        {
                            Physics.IgnoreCollision(
                                patchCollider, subframeCollider);
                        }
                    }
                }

                support.RefreshSupport(force: true);
                for (int step = 0; step < 45; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                chassis.constraints = RigidbodyConstraints.FreezePositionX |
                    RigidbodyConstraints.FreezePositionZ |
                    RigidbodyConstraints.FreezeRotation;
                chassis.useGravity = true;
                chassis.WakeUp();
                float maximumHeight = chassis.position.y;
                for (int step = 0; step < 200; step++)
                {
                    yield return new WaitForFixedUpdate();
                    maximumHeight = Mathf.Max(maximumHeight, chassis.position.y);
                    Assert.That(float.IsFinite(chassis.position.y), Is.True);
                    Assert.That(chassis.position.y, Is.GreaterThan(-0.1f),
                        "The incomplete front corner fell through its ground patches.");
                }

                yield return null;
                for (int index = 0; index < rig.Corners.Length; index++)
                {
                    Assert.That(rig.Corners[index].Wheel.HitCollider,
                        Is.SameAs(patches[index].GetComponent<Collider>()),
                        rig.Corners[index].CornerId +
                        ": the no-strut force fixture contacted foreign geometry.");
                }
                Assert.That(maximumHeight,
                    Is.LessThanOrEqualTo(initialHeight + 0.02f),
                    "A no-strut contact stage launched the shell upward.");
                Assert.That(chassis.position.y, Is.InRange(0.23f, 0.32f),
                    "Weak donor springs should compress to the bare-hub bottom stop.");
                Assert.That(Mathf.Abs(chassis.linearVelocity.y), Is.LessThan(0.15f));
                foreach (SatsumaFrontSuspensionCornerBinding corner in
                         rig.Corners)
                {
                    AssertNoStrutContactProfile(corner.Wheel);
                    Assert.That(corner.Wheel.IsGrounded, Is.True);
                    Assert.That(corner.Wheel.SpringCompression, Is.GreaterThan(0.90f));
                    Assert.That(corner.Wheel.Load, Is.LessThan(0.8f),
                        "A no-strut corner must not inherit full strut spring force.");
                    Assert.That(corner.Wheel.WheelPosition.y - corner.Wheel.Radius,
                        Is.GreaterThan(-0.025f),
                        "The physical bottom stop left the visual hub underground.");
                }
            }
            finally
            {
                DestroyOwnedObject(instance);
                foreach (GameObject patch in patches)
                {
                    DestroyOwnedObject(patch);
                }
            }
        }

        [UnityTest]
        public IEnumerator FrontAssemblyEnablesNwhSupportAndDrivesDonorRig()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject ground = CreateOwnedPrimitive(PrimitiveType.Cube);
            ground.name = "Front suspension physics test ground";
            ground.transform.SetPositionAndRotation(
                new Vector3(0f, -0.1f, 0f),
                Quaternion.identity);
            ground.transform.localScale = new Vector3(20f, 0.2f, 20f);
            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 0.8f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                chassis.constraints = RigidbodyConstraints.FreezeRotation;
                foreach (PartInstance part in assembly.Parts)
                {
                    if (part == null || part.IsAssemblyRoot ||
                        part.Body == null)
                    {
                        continue;
                    }

                    part.Body.isKinematic = true;
                    part.Body.detectCollisions = false;
                }

                for (int step = 0; step < 45; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                float bodyOnlyHeight = chassis.position.y;
                InstallAtMount(
                    assembly,
                    FindPart(assembly, "vehicle.satsuma.part.sub-frame"),
                    FindMount(assembly, "mount.satsuma.sub-frame"));
                foreach (string corner in new[] { "fl", "fr" })
                {
                    InstallAtMount(
                        assembly,
                        FindPart(
                            assembly,
                            "vehicle.satsuma.part.wishbone-" + corner),
                        FindMount(
                            assembly,
                            "mount.satsuma.wishbone-" + corner));
                    InstallAtMount(
                        assembly,
                        FindPart(
                            assembly,
                            "vehicle.satsuma.part.spindle-" + corner),
                        FindMount(
                            assembly,
                            "mount.satsuma.spindle-" + corner));
                    InstallAtMount(
                        assembly,
                        FindPart(
                            assembly,
                            "vehicle.satsuma.part.strut-" + corner),
                        FindMount(
                            assembly,
                            "mount.satsuma.strut-" + corner));
                }

                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                SatsumaFrontSuspensionController rig = instance
                    .GetComponent<SatsumaFrontSuspensionController>();
                Assert.That(rig, Is.Not.Null);
                support.RefreshSupport(force: true);
                Assert.That(support.IsSupported(0), Is.True);
                Assert.That(support.IsSupported(1), Is.True);
                Assert.That(support.IsSupported(2), Is.False);
                Assert.That(support.IsSupported(3), Is.False);
                Assert.That(support.Bindings[0].Wheel.Radius,
                    Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(support.Bindings[1].Wheel.Radius,
                    Is.EqualTo(0.12f).Within(0.0001f));

                for (int step = 0; step < 100; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                yield return null;
                foreach (SatsumaFrontSuspensionCornerBinding corner in rig.Corners)
                {
                    Assert.That(corner.Wheel.HitCollider,
                        Is.SameAs(ground.GetComponent<Collider>()),
                        corner.CornerId +
                        ": the assembled-front force fixture contacted foreign geometry.");
                }
                Assert.That(
                    chassis.position.y,
                    Is.GreaterThan(bodyOnlyHeight + 0.01f),
                    "The assembled front suspension created no physical " +
                    "support through its bare hub contact stage.");
                foreach (SatsumaFrontSuspensionCornerBinding corner in
                         rig.Corners)
                {
                    WheelController wheel = corner.Wheel;
                    Transform nonRotating = wheel.transform.Find(
                        "NonRotating");
                    Assert.That(nonRotating, Is.Not.Null);
                    Assert.That(wheel.transform.Find("Collider"), Is.Not.Null);
                    Vector3 expectedSpindle = wheel.WheelPosition +
                        nonRotating.rotation *
                        corner.HubToSpindleMeshLocalOffset;
                    Assert.That(
                        Vector3.Distance(
                            corner.SpindleMount.transform.position,
                            expectedSpindle),
                        Is.LessThan(0.004f),
                        corner.CornerId +
                        " spindle presentation did not follow the NWH hub.");
                    Vector3 expectedShockBottom = wheel.WheelPosition +
                        nonRotating.rotation *
                        corner.HubToShockBottomLocalOffset;
                    Assert.That(
                        Vector3.Distance(
                            corner.ShockBottomTarget.position,
                            expectedShockBottom),
                        Is.LessThan(0.004f),
                        corner.CornerId +
                        " strut lower bone did not follow the NWH hub.");
                    Assert.That(
                        corner.StrutPresentation
                            .IsInstalledPresentationActive,
                        Is.True,
                        corner.CornerId +
                        " still renders the rigid loose strut after install.");
                }

                chassis.linearVelocity = Vector3.zero;
                chassis.angularVelocity = Vector3.zero;
                foreach (SatsumaFrontSuspensionCornerBinding corner in
                         rig.Corners)
                {
                    corner.Wheel.wheel.angularVelocity = 0.1f;
                    corner.Wheel.wheel.prevAngularVelocity = 0.1f;
                }

                yield return new WaitForFixedUpdate();

                foreach (SatsumaFrontSuspensionCornerBinding corner in
                         rig.Corners)
                {
                    Assert.That(
                        Mathf.Abs(corner.Wheel.wheel.angularVelocity),
                        Is.LessThan(0.0001f),
                        corner.CornerId +
                        " kept residual wheel spin while the loaded chassis " +
                        "was motionless on level ground.");
                    Assert.That(
                        Mathf.Abs(corner.Wheel.wheel.prevAngularVelocity),
                        Is.LessThan(0.0001f),
                        corner.CornerId +
                        " resurrected residual wheel spin from the previous " +
                        "NWH integration sample.");
                }
            }
            finally
            {
                DestroyOwnedObject(instance);
                DestroyOwnedObject(ground);
            }
        }

        [UnityTest]
        public IEnumerator FrontSteeringDiscsAndRoadWheelsFollowNwhHubs()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                chassis.useGravity = false;
                chassis.constraints = RigidbodyConstraints.FreezeAll;
                foreach (PartInstance part in assembly.Parts)
                {
                    if (part == null || part.IsAssemblyRoot ||
                        part.Body == null)
                    {
                        continue;
                    }

                    part.Body.isKinematic = true;
                    part.Body.detectCollisions = false;
                }

                InstallAtMount(
                    assembly,
                    FindPart(assembly, "vehicle.satsuma.part.sub-frame"),
                    FindMount(assembly, "mount.satsuma.sub-frame"));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.steering-rack"),
                    FindMount(assembly, "mount.satsuma.steering-rack"));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.steering-column"),
                    FindMount(assembly, "mount.satsuma.steering-column"));

                string[] corners = { "fl", "fr" };
                string[] discIds =
                {
                    "vehicle.satsuma.part.disc-brake-1",
                    "vehicle.satsuma.part.disc-brake-2",
                };
                string[] wheelIds =
                {
                    "vehicle.satsuma.part.wheel-stock-fl",
                    "vehicle.satsuma.part.wheel-gt-fr",
                };
                for (int index = 0; index < corners.Length; index++)
                {
                    string corner = corners[index];
                    InstallAtMount(
                        assembly,
                        FindPart(
                            assembly,
                            "vehicle.satsuma.part.wishbone-" + corner),
                        FindMount(
                            assembly,
                            "mount.satsuma.wishbone-" + corner));
                    InstallAtMount(
                        assembly,
                        FindPart(
                            assembly,
                            "vehicle.satsuma.part.spindle-" + corner),
                        FindMount(
                            assembly,
                            "mount.satsuma.spindle-" + corner));
                    InstallAtMount(
                        assembly,
                        FindPart(
                            assembly,
                            "vehicle.satsuma.part.strut-" + corner),
                        FindMount(
                            assembly,
                            "mount.satsuma.strut-" + corner));
                    InstallAtMount(
                        assembly,
                        FindPart(
                            assembly,
                            "vehicle.satsuma.part.steering-rod-" + corner),
                        FindMount(
                            assembly,
                            "mount.satsuma.steering-rod-" + corner));
                    InstallAtMount(
                        assembly,
                        FindPart(assembly, discIds[index]),
                        FindMount(
                            assembly,
                            "mount.satsuma.discbrake-" + corner));
                    InstallAtMount(
                        assembly,
                        FindPart(assembly, wheelIds[index]),
                        FindMount(
                            assembly,
                            index == 0
                                ? "mount.satsuma.wheelfl-new"
                                : "mount.satsuma.wheelfr-new"));
                }

                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                SatsumaFrontSuspensionController rig = instance
                    .GetComponent<SatsumaFrontSuspensionController>();
                support.RefreshSupport(force: true);
                yield return null;
                yield return new WaitForFixedUpdate();
                rig.ApplyNow();

                for (int index = 0; index < corners.Length; index++)
                {
                    string corner = corners[index];
                    SatsumaFrontSuspensionCornerBinding binding =
                        rig.Corners[index];
                    WheelController wheel = binding.Wheel;
                    Assert.That(
                        Vector3.Distance(
                            binding.RoadWheelMount.Pose.localPosition,
                            new Vector3(-0.043f, 0f, 0f)),
                        Is.LessThan(0.00001f),
                        corner +
                        " installed wheel lost its donor-standard local X seating");
                    Transform nonRotating = wheel.transform.Find(
                        "NonRotating");
                    Transform rotating = wheel.transform.Find("Rotating");
                    Assert.That(nonRotating, Is.Not.Null);
                    Assert.That(rotating, Is.Not.Null);
                    Assert.That(
                        wheel.Radius,
                        Is.EqualTo(index == 0
                                ? support.Bindings[index].StageProfile
                                    .RoadWheelRadiusMeters
                                : support.Bindings[index].StageProfile
                                    .RimContactRadiusMeters)
                            .Within(0.0001f),
                        corner + " contact radius does not match the " +
                        "installed wheel's tyre state.");

                    Vector3 expectedSteeringOuter = wheel.WheelPosition +
                        nonRotating.rotation *
                        binding.HubToSteeringOuterLocalOffset;
                    Assert.That(
                        Vector3.Distance(
                            binding.SteeringOuterTarget.position,
                            expectedSteeringOuter),
                        Is.LessThan(0.004f),
                        corner + " steering rod ignored the NWH steering hub.");
                    Assert.That(
                        binding.SteeringRodPresentation
                            .IsInstalledPresentationActive,
                        Is.True,
                        corner + " still renders the rigid loose steering rod.");

                    Vector3 expectedDisc = wheel.WheelPosition +
                        rotating.rotation *
                        binding.HubToDiscBrakeLocalOffset;
                    Vector3 expectedRoadWheel = wheel.WheelPosition +
                        rotating.rotation *
                        binding.HubToRoadWheelLocalOffset;
                    Assert.That(
                        Vector3.Distance(
                            binding.DiscBrakeMount.transform.position,
                            expectedDisc),
                        Is.LessThan(0.004f),
                        corner + " disc does not follow the rotating hub.");
                    Assert.That(
                        Vector3.Distance(
                            binding.RoadWheelMount.transform.position,
                            expectedRoadWheel),
                        Is.LessThan(0.004f),
                        corner + " road wheel does not follow the rotating hub.");

                    PartInstance disc = FindPart(
                        assembly,
                        discIds[index]);
                    PartInstance roadWheel = FindPart(
                        assembly,
                        wheelIds[index]);
                    AssertInstalledAtMount(disc, binding.DiscBrakeMount);
                    AssertInstalledAtMount(roadWheel, binding.RoadWheelMount);

                    Quaternion beforeRotation =
                        binding.RoadWheelMount.transform.rotation;
                    Vector3 spinAxis = rotating.TransformDirection(
                        Vector3.right);
                    rotating.rotation = Quaternion.AngleAxis(
                        37f,
                        spinAxis) * rotating.rotation;
                    rig.ApplyNow();
                    Assert.That(
                        Quaternion.Angle(
                            beforeRotation,
                            binding.RoadWheelMount.transform.rotation),
                        Is.GreaterThan(30f),
                        corner + " wheel mount discarded NWH wheel spin.");
                    AssertInstalledAtMount(disc, binding.DiscBrakeMount);
                    AssertInstalledAtMount(roadWheel, binding.RoadWheelMount);
                }
            }
            finally
            {
                DestroyOwnedObject(instance);
            }
        }

        [UnityTest]
        public IEnumerator BareGtFrontWheelSupportsCarOnlyAtMetalRimRadius()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                chassis.useGravity = false;
                chassis.constraints = RigidbodyConstraints.FreezeAll;
                foreach (PartInstance part in assembly.Parts)
                {
                    if (part == null || part.IsAssemblyRoot ||
                        part.Body == null)
                    {
                        continue;
                    }

                    part.Body.isKinematic = true;
                    part.Body.detectCollisions = false;
                }

                InstallAtMount(
                    assembly,
                    FindPart(assembly, "vehicle.satsuma.part.sub-frame"),
                    FindMount(assembly, "mount.satsuma.sub-frame"));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.wishbone-fl"),
                    FindMount(assembly, "mount.satsuma.wishbone-fl"));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.spindle-fl"),
                    FindMount(assembly, "mount.satsuma.spindle-fl"));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.strut-fl"),
                    FindMount(assembly, "mount.satsuma.strut-fl"));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.disc-brake-1"),
                    FindMount(assembly, "mount.satsuma.discbrake-fl"));

                PartInstance bareWheel = FindPart(
                    assembly,
                    "vehicle.satsuma.part.wheel-gt-fl");
                InstallAtMount(
                    assembly,
                    bareWheel,
                    FindMount(assembly, "mount.satsuma.wheelfl-new"));

                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                support.RefreshSupport(force: true);
                yield return null;

                AssemblyWheelTireState tireState = bareWheel.GetComponent<
                    AssemblyWheelTireState>();
                Assert.That(tireState, Is.Not.Null);
                Assert.That(tireState.HasTire, Is.False);
                Assert.That(
                    support.Bindings[0].Wheel.Radius,
                    Is.EqualTo(tireState.RimRadiusMeters).Within(0.0001f));
                Assert.That(
                    support.Bindings[0].Wheel.Width,
                    Is.EqualTo(tireState.RimWidthMeters).Within(0.0001f));
            }
            finally
            {
                DestroyOwnedObject(instance);
            }
        }

        [UnityTest]
        public IEnumerator RearSpringExpansionUsesStockNwhStageAndKinematicArm()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                SatsumaRearSuspensionController suspension = instance
                    .GetComponent<SatsumaRearSuspensionController>();
                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                SatsumaRearNwhSuspensionController rearAuthority = instance
                    .GetComponent<SatsumaRearNwhSuspensionController>();
                PartInstance trailArm = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.trail-arm-rl");
                PartInstance drum = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.drum-brake-1");
                PartInstance spring = assembly.Parts.Single(value =>
                    value.Definition != null &&
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.coil-spring-1");
                MountPointAuthoring armMount = assembly.MountPoints.Single(
                    value => value.MountId ==
                        "mount.satsuma.trail-arm-rl");
                MountPointAuthoring drumMount = assembly.MountPoints.Single(
                    value => value.MountId ==
                        "mount.satsuma.drum-brake-rl");
                MountPointAuthoring springMount = assembly.MountPoints.Single(
                    value => value.MountId ==
                        "mount.satsuma.coilspring-rl");

                Assert.That(suspension, Is.Not.Null);
                chassis.useGravity = false;
                chassis.constraints = RigidbodyConstraints.FreezeAll;
                InstallAtMount(assembly, trailArm, armMount);
                InstallAtMount(assembly, drum, drumMount);
                InstallAtMount(assembly, spring, springMount);
                support.RefreshSupport(force: true);
                rearAuthority.ApplyNow();

                for (int step = 0; step < 50; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                yield return null;
                rearAuthority.ApplyNow();

                WheelController wheel = support.Bindings[2].Wheel;
                SatsumaRearNwhCornerBinding rearCorner = rearAuthority.Corners[0];

                Assert.That(
                    suspension.Corners[0].SpringExpansion01,
                    Is.GreaterThan(0.99f),
                    "The installed spring never completed its compressed-to-expanded transition.");
                Assert.That(suspension.ExternalWheelAuthority, Is.True);
                Assert.That(support.IsSupported(2), Is.True);
                Assert.That(wheel.enabled, Is.True);
                Assert.That(
                    wheel.transform.localPosition.y,
                    Is.EqualTo(-0.165f).Within(0.0001f));
                Assert.That(
                    wheel.SpringMaxLength,
                    Is.EqualTo(0.14f).Within(0.0001f));
                Assert.That(
                    wheel.SpringMaxForce,
                    Is.EqualTo(2968f).Within(0.001f));
                Assert.That(
                    wheel.DamperBumpRate,
                    Is.EqualTo(2f).Within(0.001f));
                Assert.That(
                    wheel.DamperReboundRate,
                    Is.EqualTo(2f).Within(0.001f));
                float compression = Mathf.Clamp(
                    wheel.SpringMaxLength - wheel.SpringLength,
                    0f,
                    wheel.SpringMaxLength);
                float expectedAngle = SatsumaRearSuspensionTravel.ResolveArmAngle(
                    rearCorner.NeutralArmPivotLocalPosition,
                    compression,
                    hasStockSpring: true,
                    hasLongSpring: false);
                Quaternion expectedRotation = instance.transform.rotation *
                    Quaternion.AngleAxis(expectedAngle, Vector3.right) *
                    rearCorner.NeutralArmLocalRotation;
                Assert.That(
                    Quaternion.Angle(
                        rearCorner.TrailingArmMount.Pose.rotation,
                        expectedRotation),
                    Is.LessThan(0.2f),
                    "The trailing-arm presentation did not follow NWH compression.");
                Assert.That(trailArm.UsesDynamicInstalledPhysics, Is.False);
                Assert.That(trailArm.Body.isKinematic, Is.True);
                Assert.That(trailArm.GetComponent<HingeJoint>(), Is.Null);
                Assert.That(drum.UsesDynamicInstalledPhysics, Is.False);
                Assert.That(drum.Body.isKinematic, Is.True);
                Assert.That(drum.GetComponent<ConfigurableJoint>(), Is.Null);
            }
            finally
            {
                DestroyOwnedObject(instance);
            }
        }

        [UnityTest]
        public IEnumerator RearSpringAndShockFollowNwhCompressionAndDampingStage()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            GameObject patch = null;
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                SatsumaRearSuspensionController suspension = instance
                    .GetComponent<SatsumaRearSuspensionController>();
                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                SatsumaRearNwhSuspensionController rearAuthority = instance
                    .GetComponent<SatsumaRearNwhSuspensionController>();
                chassis.useGravity = false;
                chassis.constraints = RigidbodyConstraints.FreezeAll;
                SuppressLoosePartPhysics(assembly);

                PartInstance arm = FindPart(
                    assembly,
                    "vehicle.satsuma.part.trail-arm-rl");
                PartInstance drum = FindPart(
                    assembly,
                    "vehicle.satsuma.part.drum-brake-1");
                InstallAtMount(
                    assembly,
                    arm,
                    FindMount(assembly, "mount.satsuma.trail-arm-rl"));
                InstallAtMount(
                    assembly,
                    drum,
                    FindMount(assembly, "mount.satsuma.drum-brake-rl"));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.coil-spring-1"),
                    FindMount(assembly, "mount.satsuma.coilspring-rl"));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.shock-absorber-1"),
                    FindMount(assembly, "mount.satsuma.shock-rl"));
                support.RefreshSupport(force: true);
                rearAuthority.ApplyNow();

                Assert.That(suspension, Is.Not.Null);
                Assert.That(suspension.ExternalWheelAuthority, Is.True);
                Assert.That(suspension.Corners.Count, Is.GreaterThan(0));
                SatsumaRearSuspensionCornerBinding corner =
                    suspension.Corners[0];
                Assert.That(corner.SpringTopBone, Is.Not.Null);
                Assert.That(corner.SpringBottomAnchor, Is.Not.Null);

                for (int step = 0; step < 35; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                yield return null;
                rearAuthority.ApplyNow();
                WheelController wheel = support.Bindings[2].Wheel;
                SatsumaRearNwhCornerBinding rearCorner = rearAuthority.Corners[0];
                float unloadedLength = Vector3.Distance(
                    corner.SpringTopBone.position,
                    corner.SpringBottomAnchor.position);
                const float desiredCompression = 0.05f;
                float desiredSpringLength = wheel.SpringMaxLength -
                    desiredCompression;
                float groundTop = wheel.transform.position.y -
                    desiredSpringLength - wheel.Radius;
                patch = CreateOwnedPrimitive(PrimitiveType.Cube);
                patch.name = "Rear NWH compression patch";
                patch.layer = 0;
                patch.transform.position = new Vector3(
                    wheel.transform.position.x,
                    groundTop - 0.05f,
                    wheel.transform.position.z);
                patch.transform.localScale = new Vector3(0.45f, 0.1f, 0.45f);
                Physics.SyncTransforms();

                for (int step = 0; step < 35; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                yield return null;
                rearAuthority.ApplyNow();
                float loadedCompression = wheel.SpringMaxLength -
                    wheel.SpringLength;
                float loadedLength = Vector3.Distance(
                    corner.SpringTopBone.position,
                    corner.SpringBottomAnchor.position);
                Assert.That(wheel.IsGrounded, Is.True);
                Assert.That(
                    wheel.HitCollider,
                    Is.SameAs(patch.GetComponent<Collider>()));
                Assert.That(
                    loadedCompression,
                    Is.EqualTo(desiredCompression).Within(0.004f));
                Assert.That(
                    unloadedLength - loadedLength,
                    Is.GreaterThan(0.005f),
                    "The NWH-driven spring presentation showed no loaded deflection.");
                Assert.That(
                    wheel.DamperBumpRate,
                    Is.EqualTo(1000f).Within(0.001f));
                Assert.That(
                    wheel.DamperReboundRate,
                    Is.EqualTo(1000f).Within(0.001f));
                float expectedLoadedAngle =
                    SatsumaRearSuspensionTravel.ResolveArmAngle(
                        rearCorner.NeutralArmPivotLocalPosition,
                        loadedCompression,
                        hasStockSpring: true,
                        hasLongSpring: false);
                Quaternion expectedLoadedRotation = instance.transform.rotation *
                    Quaternion.AngleAxis(expectedLoadedAngle, Vector3.right) *
                    rearCorner.NeutralArmLocalRotation;
                Assert.That(
                    Quaternion.Angle(
                        rearCorner.TrailingArmMount.Pose.rotation,
                        expectedLoadedRotation),
                    Is.LessThan(0.2f));
                Assert.That(arm.UsesDynamicInstalledPhysics, Is.False);
                Assert.That(arm.Body.isKinematic, Is.True);
                Assert.That(arm.GetComponent<HingeJoint>(), Is.Null);
                Assert.That(drum.UsesDynamicInstalledPhysics, Is.False);
                Assert.That(drum.Body.isKinematic, Is.True);
                Assert.That(drum.GetComponent<ConfigurableJoint>(), Is.Null);

                patch.transform.position -= Vector3.up;
                Physics.SyncTransforms();
                for (int step = 0; step < 45; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                yield return null;
                rearAuthority.ApplyNow();
                float recoveredLength = Vector3.Distance(
                    corner.SpringTopBone.position,
                    corner.SpringBottomAnchor.position);
                Assert.That(wheel.IsGrounded, Is.False);
                Assert.That(
                    wheel.SpringMaxLength - wheel.SpringLength,
                    Is.LessThan(0.002f),
                    "The rear NWH stage did not return to full droop after load release.");
                Assert.That(
                    Mathf.Abs(recoveredLength - unloadedLength),
                    Is.LessThan(0.005f),
                    "The NWH-driven spring presentation failed to recover its airborne length.");
            }
            finally
            {
                DestroyOwnedObject(instance);
                DestroyOwnedObject(patch);
            }
        }

        [UnityTest]
        public IEnumerator RearNwhSpringsSupportTheChassisThroughGroundContact()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject ground = CreateOwnedPrimitive(PrimitiveType.Cube);
            ground.name = "Rear suspension physics test ground";
            ground.transform.SetPositionAndRotation(
                new Vector3(0f, -0.1f, 0f),
                Quaternion.identity);
            ground.transform.localScale = new Vector3(20f, 0.2f, 20f);
            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 2f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                SatsumaRearSuspensionController presentation = instance
                    .GetComponent<SatsumaRearSuspensionController>();
                SatsumaRearNwhSuspensionController rearAuthority = instance
                    .GetComponent<SatsumaRearNwhSuspensionController>();
                chassis.constraints = RigidbodyConstraints.FreezeRotation;

                foreach (PartInstance part in assembly.Parts)
                {
                    if (part == null || part.IsAssemblyRoot ||
                        part.Body == null)
                    {
                        continue;
                    }

                    part.Body.isKinematic = true;
                    part.Body.detectCollisions = false;
                }

                Physics.SyncTransforms();
                Collider[] chassisColliders = instance
                    .GetComponentsInChildren<Collider>(true)
                    .Where(value => value != null && value.enabled &&
                        !value.isTrigger &&
                        value.attachedRigidbody == chassis)
                    .ToArray();
                Assert.That(chassisColliders, Is.Not.Empty);
                float chassisBottom = chassisColliders.Min(
                    value => value.bounds.min.y);
                chassis.position += Vector3.up * (0.015f - chassisBottom);
                Physics.SyncTransforms();

                for (int step = 0; step < 50; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                float settledHeight = chassis.position.y;
                InstallAtMount(
                    assembly,
                    FindPart(assembly, "vehicle.satsuma.part.trail-arm-rl"),
                    FindMount(assembly, "mount.satsuma.trail-arm-rl"));
                InstallAtMount(
                    assembly,
                    FindPart(assembly, "vehicle.satsuma.part.trail-arm-rr"),
                    FindMount(assembly, "mount.satsuma.trail-arm-rr"));
                InstallAtMount(
                    assembly,
                    FindPart(assembly, "vehicle.satsuma.part.drum-brake-1"),
                    FindMount(assembly, "mount.satsuma.drum-brake-rl"));
                InstallAtMount(
                    assembly,
                    FindPart(assembly, "vehicle.satsuma.part.drum-brake-2"),
                    FindMount(assembly, "mount.satsuma.drum-brake-rr"));
                InstallAtMount(
                    assembly,
                    FindPart(assembly, "vehicle.satsuma.part.coil-spring-1"),
                    FindMount(assembly, "mount.satsuma.coilspring-rl"));
                InstallAtMount(
                    assembly,
                    FindPart(assembly, "vehicle.satsuma.part.coil-spring-2"),
                    FindMount(assembly, "mount.satsuma.coilspring-rr"));
                support.RefreshSupport(force: true);
                rearAuthority.ApplyNow();
                foreach (Collider chassisCollider in chassisColliders)
                {
                    chassisCollider.enabled = false;
                }

                chassis.WakeUp();

                PartInstance leftArm = FindPart(
                    assembly,
                    "vehicle.satsuma.part.trail-arm-rl");
                PartInstance rightArm = FindPart(
                    assembly,
                    "vehicle.satsuma.part.trail-arm-rr");
                PartInstance leftDrum = FindPart(
                    assembly,
                    "vehicle.satsuma.part.drum-brake-1");
                PartInstance rightDrum = FindPart(
                    assembly,
                    "vehicle.satsuma.part.drum-brake-2");
                MountPointAuthoring leftDrumMount = FindMount(
                    assembly,
                    "mount.satsuma.drum-brake-rl");
                MountPointAuthoring rightDrumMount = FindMount(
                    assembly,
                    "mount.satsuma.drum-brake-rr");
                float maximumDrumPositionError = 0f;
                float maximumDrumRotationError = 0f;

                for (int step = 0; step < 150; step++)
                {
                    yield return new WaitForFixedUpdate();
                    maximumDrumPositionError = Mathf.Max(
                        maximumDrumPositionError,
                        Vector3.Distance(
                            leftDrum.Body.position,
                            leftDrumMount.Pose.position),
                        Vector3.Distance(
                            rightDrum.Body.position,
                            rightDrumMount.Pose.position));
                    maximumDrumRotationError = Mathf.Max(
                        maximumDrumRotationError,
                        Quaternion.Angle(
                            leftDrum.Body.rotation,
                            leftDrumMount.Pose.rotation),
                        Quaternion.Angle(
                            rightDrum.Body.rotation,
                            rightDrumMount.Pose.rotation));
                }

                yield return null;
                rearAuthority.ApplyNow();
                assembly.SynchronizeInstalledParts();

                Assert.That(presentation.ExternalWheelAuthority, Is.True);
                Assert.That(support.Bindings[2].Enabled, Is.True);
                Assert.That(support.Bindings[3].Enabled, Is.True);
                Assert.That(support.IsSupported(2), Is.True);
                Assert.That(support.IsSupported(3), Is.True);
                Assert.That(support.Bindings[2].Wheel.enabled, Is.True);
                Assert.That(support.Bindings[3].Wheel.enabled, Is.True);
                Assert.That(support.Bindings[2].Wheel.IsGrounded, Is.True);
                Assert.That(support.Bindings[3].Wheel.IsGrounded, Is.True);
                float expectedCompression =
                    chassis.mass * Mathf.Abs(Physics.gravity.y) /
                    (2f * SatsumaRearSuspensionForce.StockWheelRate);
                for (int index = 2; index <= 3; index++)
                {
                    WheelController wheel = support.Bindings[index].Wheel;
                    float measuredCompression =
                        wheel.SpringMaxLength - wheel.SpringLength;
                    Assert.That(
                        measuredCompression,
                        Is.EqualTo(expectedCompression).Within(0.012f),
                        $"Rear NWH corner {index} did not settle at the " +
                        "donor linear spring-rate equilibrium.");
                }

                float expectedWeight =
                    chassis.mass * Mathf.Abs(Physics.gravity.y);
                float measuredSpringSupport =
                    support.Bindings[2].Wheel.SpringForce +
                    support.Bindings[3].Wheel.SpringForce;
                Assert.That(
                    measuredSpringSupport,
                    Is.EqualTo(expectedWeight).Within(550f),
                    "Rear NWH spring force did not settle near the supported " +
                    "chassis weight.");
                Assert.That(
                    Mathf.Abs(chassis.linearVelocity.y),
                    Is.LessThan(0.15f),
                    "Rear-only grounded fixture did not settle under NWH support. " +
                    $"Before={settledHeight:F4}; after={chassis.position.y:F4}.");
                Assert.That(
                    maximumDrumPositionError,
                    Is.LessThan(0.003f),
                    "A rear drum visibly walked away from its arm-owned socket " +
                    "under spring and ground load. Maximum error=" +
                    maximumDrumPositionError.ToString("F6") + " m.");
                Assert.That(
                    maximumDrumRotationError,
                    Is.LessThan(1f),
                    "A rear drum visibly twisted inside its arm-owned socket " +
                    "under spring and ground load. Maximum error=" +
                    maximumDrumRotationError.ToString("F4") + " degrees.");
                foreach (PartInstance arm in new[] { leftArm, rightArm })
                {
                    Assert.That(arm.UsesDynamicInstalledPhysics, Is.False);
                    Assert.That(arm.Body.isKinematic, Is.True);
                    Assert.That(arm.GetComponent<HingeJoint>(), Is.Null);
                    Assert.That(
                        arm.GetComponentsInChildren<Collider>(true).Any(value =>
                            value != null && value.enabled && !value.isTrigger &&
                            value.attachedRigidbody == arm.Body),
                        Is.False,
                        "NWH-owned rear arm retained a second solid contact solver.");
                }

                foreach (PartInstance drum in new[] { leftDrum, rightDrum })
                {
                    Assert.That(drum.UsesDynamicInstalledPhysics, Is.False);
                    Assert.That(drum.Body.isKinematic, Is.True);
                    Assert.That(drum.GetComponent<ConfigurableJoint>(), Is.Null);
                    Assert.That(
                        drum.GetComponentsInChildren<Collider>(true).Any(value =>
                            value != null && value.enabled && !value.isTrigger &&
                            value.attachedRigidbody == drum.Body),
                        Is.False,
                        "NWH-owned rear drum retained mesh collision alongside its ray profile.");
                }
            }
            finally
            {
                DestroyOwnedObject(instance);
                DestroyOwnedObject(ground);
            }
        }

        [UnityTest]
        public IEnumerator VehicleRestoreReplaysSatsumaPhysicsBeforeRelease()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                VehiclePersistenceBinding persistence = instance
                    .GetComponent<VehiclePersistenceBinding>();
                VehicleSimulationHost simulation = instance
                    .GetComponent<VehicleSimulationHost>();
                AssemblyChassisMassController massController = instance
                    .GetComponent<AssemblyChassisMassController>();
                NwhAssemblyWheelSupportController support = instance
                    .GetComponent<NwhAssemblyWheelSupportController>();
                SatsumaRearNwhSuspensionController rearAuthority = instance
                    .GetComponent<SatsumaRearNwhSuspensionController>();
                SatsumaNwhPhysicsRestoreSynchronizer restoreSynchronizer =
                    instance.GetComponent<
                        SatsumaNwhPhysicsRestoreSynchronizer>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();

                Assert.That(persistence, Is.Not.Null);
                Assert.That(massController, Is.Not.Null);
                Assert.That(restoreSynchronizer, Is.Not.Null);
                Assert.That(
                    simulation.TryInitialize(out string initializeFailure),
                    Is.True,
                    initializeFailure);

                SuppressLoosePartPhysics(assembly);
                chassis.useGravity = true;
                chassis.constraints = RigidbodyConstraints.FreezeAll;
                yield return null;

                string[] corners = { "rl", "rr" };
                string[] drumIds =
                {
                    "vehicle.satsuma.part.drum-brake-1",
                    "vehicle.satsuma.part.drum-brake-2",
                };
                string[] springIds =
                {
                    "vehicle.satsuma.part.coil-spring-1",
                    "vehicle.satsuma.part.coil-spring-2",
                };
                for (int index = 0; index < corners.Length; index++)
                {
                    string corner = corners[index];
                    InstallAtMount(
                        assembly,
                        FindPart(
                            assembly,
                            "vehicle.satsuma.part.trail-arm-" + corner),
                        FindMount(
                            assembly,
                            "mount.satsuma.trail-arm-" + corner));
                    InstallAtMount(
                        assembly,
                        FindPart(assembly, drumIds[index]),
                        FindMount(
                            assembly,
                            "mount.satsuma.drum-brake-" + corner));
                    InstallAtMount(
                        assembly,
                        FindPart(assembly, springIds[index]),
                        FindMount(
                            assembly,
                            "mount.satsuma.coilspring-" + corner));
                }

                support.RefreshSupport(force: true);
                rearAuthority.ApplyNow();
                massController.RefreshMass(force: true);
                Physics.SyncTransforms();

                float expectedMass = chassis.mass;
                Vector3 expectedCenterOfMass = chassis.centerOfMass;
                float[] expectedRadii =
                {
                    support.Bindings[2].Wheel.Radius,
                    support.Bindings[3].Wheel.Radius,
                };
                float[] expectedSpringForces =
                {
                    support.Bindings[2].Wheel.SpringMaxForce,
                    support.Bindings[3].Wheel.SpringMaxForce,
                };
                Assert.That(support.Bindings[2].Wheel.enabled, Is.True);
                Assert.That(support.Bindings[3].Wheel.enabled, Is.True);

                Assert.That(
                    persistence.TryCapture(
                        out VehicleSaveRecordDto save,
                        out string captureFailure),
                    Is.True,
                    captureFailure);
                save.physics.sleeping = false;
                save.physics.linearVelocity = Vector3.zero;
                save.physics.angularVelocity = Vector3.zero;

                // Reproduce the stale one-frame state that previously survived
                // RestoreSaveData until an ordinary part action refreshed it.
                chassis.mass = 17f;
                chassis.centerOfMass = new Vector3(2f, 3f, 4f);
                for (int index = 2; index < 4; index++)
                {
                    WheelController wheel = support.Bindings[index].Wheel;
                    wheel.Radius = 0.01f;
                    wheel.SpringMaxForce = 0f;
                    wheel.enabled = false;
                }

                int assemblyActionsDuringRestore = 0;
                assembly.ActionCompleted += _ =>
                    assemblyActionsDuringRestore++;
                int synchronizationCountBefore =
                    restoreSynchronizer.SynchronizationCount;

                Assert.That(
                    persistence.TryRestore(
                        save,
                        out string restoreFailure),
                    Is.True,
                    restoreFailure);

                Assert.That(
                    assemblyActionsDuringRestore,
                    Is.Zero,
                    "Save restore must not require a fake part action to wake " +
                    "the assembly-dependent physics pipeline.");
                Assert.That(
                    restoreSynchronizer.SynchronizationCount,
                    Is.EqualTo(synchronizationCountBefore + 1));
                Assert.That(
                    restoreSynchronizer
                        .LastSynchronizationHeldChassisKinematic,
                    Is.True,
                    "NWH state was replayed after the chassis had already " +
                    "returned to live PhysX.");
                Assert.That(chassis.useGravity, Is.True);
                Assert.That(chassis.isKinematic, Is.False);
                Assert.That(chassis.IsSleeping(), Is.False);
                Assert.That(chassis.mass,
                    Is.EqualTo(expectedMass).Within(0.001f));
                Assert.That(
                    Vector3.Distance(
                        chassis.centerOfMass,
                        expectedCenterOfMass),
                    Is.LessThan(0.001f));

                for (int index = 2; index < 4; index++)
                {
                    WheelController wheel = support.Bindings[index].Wheel;
                    Assert.That(wheel.enabled, Is.True, corners[index - 2]);
                    Assert.That(
                        wheel.Radius,
                        Is.EqualTo(expectedRadii[index - 2])
                            .Within(0.0001f),
                        corners[index - 2] + " contact radius");
                    Assert.That(
                        wheel.SpringMaxForce,
                        Is.EqualTo(expectedSpringForces[index - 2])
                            .Within(0.01f),
                        corners[index - 2] + " spring stage");
                    PartInstance arm = FindPart(
                        assembly,
                        "vehicle.satsuma.part.trail-arm-" +
                        corners[index - 2]);
                    PartInstance drum = FindPart(
                        assembly,
                        drumIds[index - 2]);
                    Assert.That(arm.UsesDynamicInstalledPhysics, Is.False);
                    Assert.That(drum.UsesDynamicInstalledPhysics, Is.False);
                }
            }
            finally
            {
                DestroyOwnedObject(instance);
            }
        }

        [UnityTest]
        public IEnumerator FreshVehicleRestoreSettlesWithoutAssemblyMutation()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject ground = CreateOwnedPrimitive(PrimitiveType.Cube);
            ground.name = "Fresh Satsuma restore ground";
            ground.transform.SetPositionAndRotation(
                new Vector3(0f, -0.1f, 0f),
                Quaternion.identity);
            ground.transform.localScale = new Vector3(20f, 0.2f, 20f);
            GameObject source = null;
            GameObject restored = null;
            var diagnostics = new List<string>();
            bool verified = false;
            try
            {
                source = InstantiateOwnedSatsuma(
                    prefab,
                    new Vector3(0f, 1.2f, 0f),
                    Quaternion.identity);
                VehicleAssemblyController sourceAssembly = source
                    .GetComponent<VehicleAssemblyController>();
                VehicleSimulationHost sourceSimulation = source
                    .GetComponent<VehicleSimulationHost>();
                Rigidbody sourceChassis = source.GetComponent<Rigidbody>();
                SuppressLoosePartPhysics(sourceAssembly);
                Assert.That(
                    sourceSimulation.TryInitialize(
                        out string sourceInitializeFailure),
                    Is.True,
                    sourceInitializeFailure);
                yield return null;

                InstallCompleteSuspensionAndWheels(sourceAssembly);
                source.GetComponent<NwhAssemblyWheelSupportController>()
                    .RefreshSupport(force: true);
                source.GetComponent<SatsumaFrontSteeringController>()
                    .ApplyNow();
                source.GetComponent<SatsumaFrontSuspensionController>()
                    .ApplyNow();
                source.GetComponent<SatsumaRearNwhSuspensionController>()
                    .ApplyNow();
                source.GetComponent<AssemblyChassisMassController>()
                    .RefreshMass(force: true);
                Physics.SyncTransforms();
                sourceChassis.WakeUp();

                sourceAssembly.ActionCompleted += action =>
                    diagnostics.Add("FRESH_RESTORE source action " + DescribeFreshRestoreAction(action));
                diagnostics.Add(CaptureFreshRestoreSnapshot("source before settling", source));

                for (int step = 0; step < 150; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                diagnostics.Add(CaptureFreshRestoreSnapshot("source settled", source));

                NwhAssemblyWheelSupportController sourceSupport = source
                    .GetComponent<NwhAssemblyWheelSupportController>();
                Assert.That(
                    sourceSupport.Bindings,
                    Is.All.Matches<NwhAssemblyWheelSupportBinding>(binding =>
                        binding.Wheel != null && binding.Wheel.enabled),
                    "The source fixture did not assemble all four support corners.");
                Assert.That(
                    sourceSupport.Bindings.Count(binding =>
                        binding.Wheel.IsGrounded),
                    Is.GreaterThanOrEqualTo(3),
                    "The source fixture did not settle on its suspension.");

                sourceChassis.linearVelocity = Vector3.zero;
                sourceChassis.angularVelocity = Vector3.zero;
                sourceChassis.WakeUp();
                VehiclePersistenceBinding sourcePersistence = source
                    .GetComponent<VehiclePersistenceBinding>();
                Assert.That(
                    sourcePersistence.TryCapture(
                        out VehicleSaveRecordDto save,
                        out string captureFailure),
                    Is.True,
                    captureFailure);
                save.physics.sleeping = false;
                save.physics.linearVelocity = Vector3.zero;
                save.physics.angularVelocity = Vector3.zero;
                Quaternion savedRotation = save.physics.worldRotation;
                float savedHeight = save.physics.worldPosition.y;

                DestroyOwnedObject(source);
                source = null;
                yield return null;
                yield return null;
                Physics.SyncTransforms();

                restored = InstantiateOwnedSatsuma(
                    prefab,
                    new Vector3(4f, 5f, -3f),
                    Quaternion.Euler(20f, 35f, -15f));
                VehicleAssemblyController restoredAssembly = restored
                    .GetComponent<VehicleAssemblyController>();
                VehicleSimulationHost restoredSimulation = restored
                    .GetComponent<VehicleSimulationHost>();
                VehiclePersistenceBinding restoredPersistence = restored
                    .GetComponent<VehiclePersistenceBinding>();
                SatsumaNwhPhysicsRestoreSynchronizer restoreSynchronizer =
                    restored.GetComponent<
                        SatsumaNwhPhysicsRestoreSynchronizer>();
                Rigidbody restoredChassis = restored.GetComponent<Rigidbody>();
                SuppressLoosePartPhysics(restoredAssembly);
                Assert.That(
                    restoredSimulation.TryInitialize(
                        out string restoredInitializeFailure),
                    Is.True,
                    restoredInitializeFailure);

                int assemblyActions = 0;
                var restoreActions = new List<string>();
                restoredAssembly.ActionCompleted += action =>
                {
                    assemblyActions++;
                    string description = DescribeFreshRestoreAction(action);
                    restoreActions.Add(description);
                    diagnostics.Add("FRESH_RESTORE restored action " + description);
                };
                diagnostics.Add(CaptureFreshRestoreSnapshot("target before restore", restored));
                Assert.That(
                    restoredPersistence.TryRestore(
                        save,
                        out string restoreFailure),
                    Is.True,
                    restoreFailure);
                diagnostics.Add(CaptureFreshRestoreSnapshot("target immediately restored", restored));

                // No yield has happened since Instantiate: this is the same
                // pre-Start restore timing used by the production bootstrap.
                Assert.That(assemblyActions, Is.Zero);
                Assert.That(
                    restoreSynchronizer.SynchronizationCount,
                    Is.EqualTo(1));
                Assert.That(
                    restoreSynchronizer
                        .LastSynchronizationHeldChassisKinematic,
                    Is.True);
                Assert.That(restoredChassis.useGravity, Is.True);
                Assert.That(restoredChassis.isKinematic, Is.False);
                Assert.That(
                    Vector3.Distance(
                        restoredChassis.position,
                        save.physics.worldPosition),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        restoredChassis.rotation,
                        savedRotation),
                    Is.LessThan(0.01f));

                float maximumTiltDegrees = 0f;
                float maximumHeightExcursion = 0f;
                float maximumAngularSpeed = 0f;
                for (int step = 0; step < 120; step++)
                {
                    yield return new WaitForFixedUpdate();
                    if (step == 0 || step == 29 || step == 119)
                        diagnostics.Add(CaptureFreshRestoreSnapshot("target fixed " + (step + 1), restored));
                    Assert.That(
                        float.IsFinite(restoredChassis.position.x) &&
                        float.IsFinite(restoredChassis.position.y) &&
                        float.IsFinite(restoredChassis.position.z),
                        Is.True,
                        "Fresh restore produced a non-finite chassis pose.");
                    maximumTiltDegrees = Mathf.Max(
                        maximumTiltDegrees,
                        Vector3.Angle(
                            savedRotation * Vector3.up,
                            restoredChassis.rotation * Vector3.up));
                    maximumHeightExcursion = Mathf.Max(
                        maximumHeightExcursion,
                        Mathf.Abs(restoredChassis.position.y - savedHeight));
                    maximumAngularSpeed = Mathf.Max(
                        maximumAngularSpeed,
                        restoredChassis.angularVelocity.magnitude);
                }

                yield return null;
                NwhAssemblyWheelSupportController restoredSupport = restored
                    .GetComponent<NwhAssemblyWheelSupportController>();
                Assert.That(assemblyActions, Is.Zero, string.Join(" | ", restoreActions));
                Assert.That(
                    restoredSupport.Bindings,
                    Is.All.Matches<NwhAssemblyWheelSupportBinding>(binding =>
                        binding.Wheel != null && binding.Wheel.enabled));
                Assert.That(
                    restoredSupport.Bindings.Count(binding =>
                        binding.Wheel.IsGrounded),
                    Is.GreaterThanOrEqualTo(3));
                Assert.That(
                    maximumTiltDegrees,
                    Is.LessThan(35f),
                    "The restored chassis pitched or rolled before its " +
                    "suspension state became authoritative.");
                Assert.That(
                    maximumHeightExcursion,
                    Is.LessThan(0.75f),
                    "The restored chassis was launched vertically during " +
                    "the pre-Start physics handoff.");
                Assert.That(
                    maximumAngularSpeed,
                    Is.LessThan(8f),
                    "The restored chassis received an explosive angular impulse.");
                Assert.That(
                    Vector3.Angle(
                        savedRotation * Vector3.up,
                        restoredChassis.rotation * Vector3.up),
                    Is.LessThan(12f),
                    "The restored chassis did not settle back to its saved attitude.");
                verified = true;
            }
            finally
            {
                // Keep contact/projection evidence for failures without flooding
                // successful regression runs with diagnostic-only snapshots.
                if (!verified)
                    foreach (string diagnostic in diagnostics) TestContext.WriteLine(diagnostic);
                if (source != null)
                {
                    DestroyOwnedObject(source);
                }
                if (restored != null)
                {
                    DestroyOwnedObject(restored);
                }
                DestroyOwnedObject(ground);
            }
        }

        [UnityTest]
        public IEnumerator InstalledSpringVisiblyExpandsFromCompressedLength()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                chassis.useGravity = false;
                chassis.constraints = RigidbodyConstraints.FreezeAll;
                PartInstance arm = FindPart(
                    assembly,
                    "vehicle.satsuma.part.trail-arm-rl");
                PartInstance drum = FindPart(
                    assembly,
                    "vehicle.satsuma.part.drum-brake-1");
                PartInstance spring = FindPart(
                    assembly,
                    "vehicle.satsuma.part.coil-spring-1");
                InstallAtMount(
                    assembly,
                    arm,
                    FindMount(assembly, "mount.satsuma.trail-arm-rl"));
                InstallAtMount(
                    assembly,
                    drum,
                    FindMount(assembly, "mount.satsuma.drum-brake-rl"));
                InstallAtMount(
                    assembly,
                    spring,
                    FindMount(assembly, "mount.satsuma.coilspring-rl"));

                yield return null;

                SatsumaRearSuspensionPartPresentation presentation = spring
                    .GetComponent<SatsumaRearSuspensionPartPresentation>();
                Assert.That(presentation, Is.Not.Null);
                Renderer installedRenderer = presentation.ActiveSpringRenderer;
                Assert.That(installedRenderer, Is.Not.Null);
                Assert.That(
                    installedRenderer.gameObject.name,
                    Is.EqualTo("Installed two-bone spring"));
                float compressedLength = MeasureDominantMeshAxisLength(
                    installedRenderer);

                for (int step = 0; step < 40; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                yield return null;
                float expandedLength = MeasureDominantMeshAxisLength(
                    presentation.ActiveSpringRenderer);
                SatsumaRearSuspensionController suspension = instance
                    .GetComponent<SatsumaRearSuspensionController>();
                Assert.That(
                    suspension.Corners[0].SpringExpansion01,
                    Is.GreaterThan(0.99f));
                Assert.That(
                    expandedLength,
                    Is.GreaterThan(compressedLength + 0.04f),
                    "The installed mesh did not visibly follow the spring's " +
                    "compressed-to-expanded state. " +
                    $"Compressed={compressedLength:F4}; expanded={expandedLength:F4}.");
            }
            finally
            {
                DestroyOwnedObject(instance);
            }
        }

        [UnityTest]
        public IEnumerator SuspensionInstallHandoffUsesRiggedPoseWithoutFinalSnap()
        {
            GameObject prefab = Resources.Load<GameObject>(
                "Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = InstantiateOwnedSatsuma(
                prefab,
                new Vector3(0f, 50f, 0f),
                Quaternion.identity);
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                Rigidbody chassis = instance.GetComponent<Rigidbody>();
                chassis.useGravity = false;
                chassis.constraints = RigidbodyConstraints.FreezeAll;

                PartInstance spring = FindPart(
                    assembly,
                    "vehicle.satsuma.part.coil-spring-1");
                MountPointAuthoring springMount = FindMount(
                    assembly,
                    "mount.satsuma.coilspring-rl");
                SatsumaRearSuspensionPartPresentation springPresentation =
                    spring.GetComponent<
                        SatsumaRearSuspensionPartPresentation>();
                Assert.That(springPresentation, Is.Not.Null);
                Renderer looseSpringRenderer =
                    springPresentation.ActiveSpringRenderer;
                Vector3 looseSpringPosition =
                    looseSpringRenderer.transform.position;
                Quaternion looseSpringRotation =
                    looseSpringRenderer.transform.rotation;
                Vector3 looseSpringScale =
                    looseSpringRenderer.transform.lossyScale;

                springPresentation.BeginInstallTransition(springMount);
                Assert.That(
                    springPresentation.IsInstallTransitionPreviewActive,
                    Is.True);
                springPresentation.ApplyInstallTransition(0f);
                Renderer springPreview =
                    springPresentation.ActiveSpringRenderer;
                Assert.That(
                    springPreview.gameObject.name,
                    Is.EqualTo("Installed two-bone spring"));
                Assert.That(
                    Vector3.Distance(
                        springPreview.transform.position,
                        looseSpringPosition),
                    Is.LessThan(0.0001f),
                    "Switching from the loose mesh to the fitted preview " +
                    "introduced a positional pop.");
                Assert.That(
                    Quaternion.Angle(
                        springPreview.transform.rotation,
                        looseSpringRotation),
                    Is.LessThan(0.05f),
                    "The rear spring entered the old horizontal install pose " +
                    "when the handoff began.");
                Assert.That(
                    Vector3.Distance(
                        springPreview.transform.lossyScale,
                        looseSpringScale),
                    Is.LessThan(0.0001f));

                SatsumaRearSuspensionController rearSuspension = instance
                    .GetComponent<SatsumaRearSuspensionController>();
                SatsumaRearSuspensionCornerBinding rearLeft = rearSuspension
                    .Corners.Single(value => value.CornerId == "rl");
                springPresentation.ApplyInstallTransition(1f);
                Vector3 springTarget =
                    rearLeft.SpringBottomAnchor.position -
                    rearLeft.SpringTopBone.position;
                Assert.That(
                    Mathf.Abs(Vector3.Dot(
                        ResolveDominantMeshWorldAxis(springPreview),
                        springTarget.normalized)),
                    Is.GreaterThan(0.999f),
                    "The spring preview did not rotate into its physical rig " +
                    "before assembly state changed.");
                Assert.That(
                    MeasureDominantMeshAxisLength(springPreview),
                    Is.EqualTo(Mathf.Min(springTarget.magnitude, 0.062f))
                        .Within(0.002f),
                    "The rear spring must reach the mount in its compressed " +
                    "shape, then expand under the suspension controller.");
                springPresentation.CompleteInstallTransition(false);
                Assert.That(
                    springPresentation.ActiveSpringRenderer,
                    Is.SameAs(looseSpringRenderer));

                PartInstance strut = FindPart(
                    assembly,
                    "vehicle.satsuma.part.strut-fl");
                MountPointAuthoring strutMount = FindMount(
                    assembly,
                    "mount.satsuma.strut-fl");
                SatsumaFrontStrutPresentation strutPresentation = strut
                    .GetComponent<SatsumaFrontStrutPresentation>();
                Assert.That(strutPresentation, Is.Not.Null);
                SkinnedMeshRenderer strutPreview =
                    strutPresentation.InstalledRenderer;
                Matrix4x4[] bindPoses =
                    strutPreview.sharedMesh.bindposes;
                Matrix4x4 rendererWorld =
                    strutPreview.transform.localToWorldMatrix;
                Matrix4x4 upperPartLocal = strut.transform.worldToLocalMatrix *
                    strutPresentation.InstalledUpperBone.localToWorldMatrix;

                strutPresentation.BeginInstallTransition(strutMount);
                Assert.That(
                    strutPresentation.IsInstallTransitionPreviewActive,
                    Is.True);
                strutPresentation.ApplyInstallTransition(0f);
                AssertTransformMatchesMatrix(
                    strutPreview.bones[0],
                    rendererWorld * bindPoses[0].inverse,
                    "front strut upper preview start");
                AssertTransformMatchesMatrix(
                    strutPreview.bones[1],
                    rendererWorld * bindPoses[1].inverse,
                    "front strut lower preview start");

                strutPresentation.ApplyInstallTransition(1f);
                Matrix4x4 strutMountWorld = Matrix4x4.TRS(
                    strutMount.Pose.position,
                    strutMount.Pose.rotation,
                    Vector3.one);
                AssertTransformMatchesMatrix(
                    strutPreview.bones[0],
                    strutMountWorld * upperPartLocal,
                    "front strut upper preview target");
                Assert.That(
                    Vector3.Distance(
                        strutPreview.bones[1].position,
                        strutPresentation.InstalledLowerTarget.position),
                    Is.LessThan(0.0001f));
                Assert.That(
                    Quaternion.Angle(
                        strutPreview.bones[1].rotation,
                        strutPresentation.InstalledLowerTarget.rotation),
                    Is.LessThan(0.05f));
                strutPresentation.CompleteInstallTransition(false);
                Assert.That(strutPresentation.LooseRenderer.enabled, Is.True);
                Assert.That(strutPresentation.InstalledRenderer.enabled, Is.False);

                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.sub-frame"),
                    FindMount(assembly, "mount.satsuma.sub-frame"));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.wishbone-fl"),
                    FindMount(assembly, "mount.satsuma.wishbone-fl"));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.spindle-fl"),
                    FindMount(assembly, "mount.satsuma.spindle-fl"));
                strut.transform.SetPositionAndRotation(
                    strutMount.Pose.position + new Vector3(-0.25f, 0.08f, 0f),
                    Quaternion.Euler(90f, 0f, 0f));
                strut.Body.position = strut.transform.position;
                strut.Body.rotation = strut.transform.rotation;
                AssemblyOperationResult frontHandoff =
                    assembly.BeginInstallFromHandoff(
                        strut.PickupTarget,
                        strutMount);
                Assert.That(
                    frontHandoff.Succeeded,
                    Is.True,
                    frontHandoff.Message);
                Assert.That(
                    strutPresentation.IsInstallTransitionPreviewActive,
                    Is.True,
                    "The real player handoff did not activate the fitted " +
                    "front-strut preview.");
                float frontTimeoutAt = Time.unscaledTime + 1f;
                while (!strut.IsInstalled &&
                       Time.unscaledTime < frontTimeoutAt)
                {
                    yield return null;
                }

                Assert.That(strut.IsInstalled, Is.True);
                Assert.That(
                    strutPresentation.IsInstallTransitionPreviewActive,
                    Is.False);

                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.trail-arm-rl"),
                    FindMount(assembly, "mount.satsuma.trail-arm-rl"));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.drum-brake-1"),
                    FindMount(assembly, "mount.satsuma.drum-brake-rl"));
                spring.transform.SetPositionAndRotation(
                    springMount.Pose.position + new Vector3(0.25f, 0.08f, 0f),
                    Quaternion.Euler(0f, 0f, 90f));
                spring.Body.position = spring.transform.position;
                spring.Body.rotation = spring.transform.rotation;
                AssemblyOperationResult handoff =
                    assembly.BeginInstallFromHandoff(
                        spring.PickupTarget,
                        springMount);
                Assert.That(handoff.Succeeded, Is.True, handoff.Message);
                Assert.That(
                    springPresentation.IsInstallTransitionPreviewActive,
                    Is.True,
                    "The real player handoff did not activate the fitted " +
                    "spring preview.");

                float rearTimeoutAt = Time.unscaledTime + 1f;
                while (!spring.IsInstalled &&
                       Time.unscaledTime < rearTimeoutAt)
                {
                    yield return null;
                }

                Assert.That(spring.IsInstalled, Is.True);
                Assert.That(
                    springPresentation.IsInstallTransitionPreviewActive,
                    Is.False);
                Assert.That(
                    VehicleAssemblyController.InstallTransitionDurationSeconds,
                    Is.EqualTo(0.17f).Within(0.00001f));
            }
            finally
            {
                DestroyOwnedObject(instance);
            }
        }

        private static void AssertNoStrutContactProfile(WheelController wheel)
        {
            Assert.That(wheel.SpringMaxLength,
                Is.EqualTo(0.20f).Within(0.0001f));
            Assert.That(wheel.SpringMaxForce,
                Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(wheel.DamperBumpRate,
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(wheel.DamperReboundRate,
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(wheel.Camber, Is.EqualTo(-1.4f).Within(0.0001f));
            Assert.That(wheel.transform.localPosition.y,
                Is.EqualTo(-0.15f).Within(0.0001f));
        }

        private static Vector3 ExpectedAirborneHubPosition(
            Transform chassis,
            SatsumaFrontSuspensionCornerBinding corner)
        {
            WheelController wheel = corner.Wheel;
            float offsetX = -wheel.wheel.rimOffset *
                (wheel.transform.localPosition.x < 0f ? -1f : 1f);
            Vector3 offset = Vector3.right * offsetX;
            // NWH steers about a rim-offset pivot. Preserve the original
            // absolute travel assertion while accounting for that known X/Z
            // displacement; camber does not change the wheel centre here.
            Vector3 displacement = offset -
                Quaternion.AngleAxis(wheel.SteerAngle, Vector3.up) * offset;
            return chassis.TransformPoint(corner.CanonicalNoStrutHubLocalPosition) +
                wheel.transform.parent.TransformVector(displacement);
        }

        private static Quaternion ExpectedFrontWishboneRotation(
            Transform chassis,
            SatsumaFrontSuspensionCornerBinding corner)
        {
            Transform hub = corner.Wheel.wheel.nonRotatingContainer;
            Assert.That(hub, Is.Not.Null);
            Vector3 endpoint = corner.Wheel.WheelPosition +
                hub.rotation * corner.HubToWishboneTargetLocalOffset;
            Vector3 direction = chassis.InverseTransformPoint(endpoint) -
                corner.WishboneBodyPivotLocalPosition;
            direction.z = 0f;
            Assert.That(direction.y, Is.LessThan(0f),
                "An airborne unsprung arm must still point down from its fixed pivot.");
            float angle = Vector3.SignedAngle(
                corner.LeftSide ? Vector3.left : Vector3.right,
                direction.normalized,
                Vector3.forward);
            return chassis.rotation * Quaternion.AngleAxis(angle, Vector3.forward) *
                corner.WishboneMeshZeroLocalRotation;
        }

        private GameObject CreateFrontContactPatch(
            float x,
            float z,
            float topY)
        {
            GameObject patch = CreateOwnedPrimitive(PrimitiveType.Cube);
            patch.name = "Default-layer front no-strut contact patch";
            patch.layer = 0;
            patch.transform.position = new Vector3(x, topY - 0.05f, z);
            patch.transform.localScale = new Vector3(0.28f, 0.1f, 0.38f);
            return patch;
        }

        private static string DescribeFreshRestoreAction(AssemblyActionCompleted action) =>
            $"{action.Action}; mount={action.MountId}; part={action.Part?.Definition?.DefinitionId}; " +
            $"fastener={action.FastenerDefinitionId}; pos={action.WorldPosition:F4}";

        private static string CaptureFreshRestoreSnapshot(string label, GameObject vehicle)
        {
            Rigidbody chassis = vehicle.GetComponent<Rigidbody>();
            VehicleAssemblyController assembly = vehicle.GetComponent<VehicleAssemblyController>();
            NwhAssemblyWheelSupportController support = vehicle.GetComponent<NwhAssemblyWheelSupportController>();
            AssemblyLooseCompoundPhysics compound = vehicle.GetComponent<AssemblyLooseCompoundPhysics>();
            string wheels = string.Join(" | ", support.Bindings.Select((binding, index) => binding.Wheel == null
                ? index + ": missing"
                : $"{index}: enabled={binding.Wheel.enabled}; grounded={binding.Wheel.IsGrounded}; " +
                  $"pos={binding.Wheel.transform.position:F4}; spring={binding.Wheel.spring.length:F4}/" +
                  $"{binding.Wheel.SpringMaxLength:F4}; previous={binding.Wheel.spring.prevLength:F4}"));
            string retention = string.Join(" | ", assembly.Graph.Mounts.Where(mount => mount.IsOccupied &&
                mount.FastenerGroup.Definition.SpeedRetentionPolicy != FastenerSpeedRetentionPolicy.None)
                .Select(mount => $"{mount.MountId}: bolted={mount.FastenerGroup.IsBolted}; stages=" +
                    string.Join(",", mount.Fasteners.Select(fastener => fastener.Stage + "/" + fastener.Definition.MaximumStage))));
            return $"FRESH_RESTORE {label}; pos={chassis.position:F4}; rot={chassis.rotation.eulerAngles:F3}; " +
                $"velocity={chassis.linearVelocity:F4}; angular={chassis.angularVelocity:F4}; mass={chassis.mass:F3}; " +
                $"CoM={chassis.centerOfMass:F4}; sleeping={chassis.IsSleeping()}; gravity={chassis.useGravity}; " +
                $"kinematic={chassis.isKinematic}; proxies={compound?.ActiveProxyCount ?? 0}; wheels=[{wheels}]; retention=[{retention}]";
        }

        private static void SuppressLoosePartPhysics(
            VehicleAssemblyController assembly)
        {
            foreach (PartInstance part in assembly.Parts)
            {
                if (part == null || part.IsAssemblyRoot || part.Body == null)
                {
                    continue;
                }

                part.Body.isKinematic = true;
                part.Body.detectCollisions = false;
            }
        }

        private static void InstallCompleteSuspensionAndWheels(
            VehicleAssemblyController assembly)
        {
            InstallAtMount(
                assembly,
                FindPart(assembly, "vehicle.satsuma.part.sub-frame"),
                FindMount(assembly, "mount.satsuma.sub-frame"));
            InstallAtMount(
                assembly,
                FindPart(assembly, "vehicle.satsuma.part.steering-rack"),
                FindMount(assembly, "mount.satsuma.steering-rack"));
            InstallAtMount(
                assembly,
                FindPart(assembly, "vehicle.satsuma.part.steering-column"),
                FindMount(assembly, "mount.satsuma.steering-column"));

            string[] frontCorners = { "fl", "fr" };
            string[] discIds =
            {
                "vehicle.satsuma.part.disc-brake-1",
                "vehicle.satsuma.part.disc-brake-2",
            };
            for (int index = 0; index < frontCorners.Length; index++)
            {
                string corner = frontCorners[index];
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.wishbone-" + corner),
                    FindMount(
                        assembly,
                        "mount.satsuma.wishbone-" + corner));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.spindle-" + corner),
                    FindMount(
                        assembly,
                        "mount.satsuma.spindle-" + corner));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.strut-" + corner),
                    FindMount(
                        assembly,
                        "mount.satsuma.strut-" + corner));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.steering-rod-" + corner),
                    FindMount(
                        assembly,
                        "mount.satsuma.steering-rod-" + corner));
                InstallAtMount(
                    assembly,
                    FindPart(assembly, discIds[index]),
                    FindMount(
                        assembly,
                        "mount.satsuma.discbrake-" + corner));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.wheel-stock-" + corner),
                    FindMount(
                        assembly,
                        "mount.satsuma.wheel" + corner + "-new"));
            }

            string[] rearCorners = { "rl", "rr" };
            string[] drumIds =
            {
                "vehicle.satsuma.part.drum-brake-1",
                "vehicle.satsuma.part.drum-brake-2",
            };
            string[] springIds =
            {
                "vehicle.satsuma.part.coil-spring-1",
                "vehicle.satsuma.part.coil-spring-2",
            };
            string[] shockIds =
            {
                "vehicle.satsuma.part.shock-absorber-1",
                "vehicle.satsuma.part.shock-absorber-2",
            };
            for (int index = 0; index < rearCorners.Length; index++)
            {
                string corner = rearCorners[index];
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.trail-arm-" + corner),
                    FindMount(
                        assembly,
                        "mount.satsuma.trail-arm-" + corner));
                InstallAtMount(
                    assembly,
                    FindPart(assembly, drumIds[index]),
                    FindMount(
                        assembly,
                        "mount.satsuma.drum-brake-" + corner));
                InstallAtMount(
                    assembly,
                    FindPart(assembly, springIds[index]),
                    FindMount(
                        assembly,
                        "mount.satsuma.coilspring-" + corner));
                InstallAtMount(
                    assembly,
                    FindPart(assembly, shockIds[index]),
                    FindMount(
                        assembly,
                        "mount.satsuma.shock-" + corner));
                InstallAtMount(
                    assembly,
                    FindPart(
                        assembly,
                        "vehicle.satsuma.part.wheel-stock-" + corner),
                    FindMount(
                        assembly,
                        "mount.satsuma.wheel" + corner + "-new"));
            }
        }

        private static void AssertInstalledAtMount(
            PartInstance part,
            MountPointAuthoring mount)
        {
            Assert.That(part.IsInstalled, Is.True);
            Assert.That(part.Body.isKinematic, Is.True);
            Assert.That(
                Vector3.Distance(part.transform.position, mount.Pose.position),
                Is.LessThan(0.005f));
            Assert.That(
                Quaternion.Angle(part.transform.rotation, mount.Pose.rotation),
                Is.LessThan(0.25f));
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
            AssemblyOperationResult result = assembly.TryInstall(part, mount);
            Assert.That(result.Succeeded, Is.True, result.Message);

            MountPointRuntime runtimeMount = assembly.ResolveMount(mount);
            if (runtimeMount == null ||
                runtimeMount.FastenerGroup == null ||
                !runtimeMount.FastenerGroup.Definition.HasFasteners)
            {
                return;
            }

            foreach (FastenerInstance fastener in runtimeMount.Fasteners)
            {
                ToolDefinition tool = assembly.Tools.FirstOrDefault(value =>
                    value != null &&
                    value.Size == fastener.Definition.Size);
                Assert.That(
                    tool,
                    Is.Not.Null,
                    "The physics fixture cannot satisfy donor Bolted prerequisites " +
                    "without a matching tool for " +
                    fastener.Definition.DefinitionId + ".");
                while (!runtimeMount.FastenerGroup.IsBolted &&
                       fastener.Stage < fastener.Definition.MaximumStage)
                {
                    AssemblyOperationResult turn = assembly.TryOperateFastener(
                        mount.MountId,
                        fastener.Definition.DefinitionId,
                        tool,
                        tighten: true);
                    Assert.That(turn.Succeeded, Is.True, turn.Message);
                }

                if (runtimeMount.FastenerGroup.IsBolted)
                {
                    break;
                }
            }

            Assert.That(
                runtimeMount.FastenerGroup.IsBolted,
                Is.True,
                mount.MountId +
                " did not reach its authored donor BoltedOnThreshold.");
        }

        private static void LoosenMountBelowBoltedThreshold(
            VehicleAssemblyController assembly,
            MountPointAuthoring mount)
        {
            MountPointRuntime runtimeMount = assembly.ResolveMount(mount);
            Assert.That(runtimeMount, Is.Not.Null);
            Assert.That(runtimeMount.FastenerGroup.IsBolted, Is.True);

            foreach (FastenerInstance fastener in runtimeMount.Fasteners)
            {
                ToolDefinition tool = assembly.Tools.FirstOrDefault(value =>
                    value != null &&
                    value.Size == fastener.Definition.Size);
                Assert.That(tool, Is.Not.Null);
                while (runtimeMount.FastenerGroup.IsBolted &&
                       fastener.Stage > 0)
                {
                    AssemblyOperationResult turn = assembly.TryOperateFastener(
                        mount.MountId,
                        fastener.Definition.DefinitionId,
                        tool,
                        tighten: false);
                    Assert.That(turn.Succeeded, Is.True, turn.Message);
                }

                if (!runtimeMount.FastenerGroup.IsBolted)
                {
                    break;
                }
            }

            Assert.That(
                runtimeMount.FastenerGroup.IsBolted,
                Is.False,
                mount.MountId +
                " did not cross its donor BoltedOffThreshold.");
        }

        private static PartInstance FindPart(
            VehicleAssemblyController assembly,
            string definitionId) => assembly.Parts.Single(value =>
                value.Definition != null &&
                value.Definition.DefinitionId == definitionId);

        private static MountPointAuthoring FindMount(
            VehicleAssemblyController assembly,
            string mountId) => assembly.MountPoints.Single(value =>
                value.MountId == mountId);

        private static Collider FindEnabledSolidCollider(PartInstance part)
        {
            Collider collider = part.GetComponentsInChildren<Collider>(true)
                .FirstOrDefault(value =>
                    value != null && value.enabled && !value.isTrigger &&
                    value.attachedRigidbody == part.Body);
            Assert.That(
                collider,
                Is.Not.Null,
                "No enabled solid collider remains on " +
                part.Definition?.DefinitionId + ". " +
                DescribeColliders(part));
            return collider;
        }

        private static string DescribeColliders(PartInstance part) =>
            string.Join(
                ";",
                part.GetComponentsInChildren<Collider>(true).Select(value =>
                    value == null
                        ? "null"
                        : $"{value.GetType().Name}[enabled={value.enabled}," +
                          $"trigger={value.isTrigger}," +
                          $"body={(value.attachedRigidbody != null ? value.attachedRigidbody.name : "null")}," +
                          $"minY={value.bounds.min.y:F4},maxY={value.bounds.max.y:F4}]"));

        private static float MeasureDominantMeshAxisLength(Renderer renderer)
        {
            MeshFilter filter = renderer != null
                ? renderer.GetComponent<MeshFilter>()
                : null;
            Assert.That(filter, Is.Not.Null);
            Assert.That(filter.sharedMesh, Is.Not.Null);
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 axis = Vector3.right;
            float halfLength = bounds.extents.x;
            if (bounds.size.y > bounds.size.x &&
                bounds.size.y >= bounds.size.z)
            {
                axis = Vector3.up;
                halfLength = bounds.extents.y;
            }
            else if (bounds.size.z > bounds.size.x &&
                     bounds.size.z > bounds.size.y)
            {
                axis = Vector3.forward;
                halfLength = bounds.extents.z;
            }

            return Vector3.Distance(
                renderer.transform.TransformPoint(
                    bounds.center - axis * halfLength),
                renderer.transform.TransformPoint(
                    bounds.center + axis * halfLength));
        }

        private static Vector3 ResolveDominantMeshWorldAxis(Renderer renderer)
        {
            MeshFilter filter = renderer != null
                ? renderer.GetComponent<MeshFilter>()
                : null;
            Assert.That(filter, Is.Not.Null);
            Assert.That(filter.sharedMesh, Is.Not.Null);
            Bounds bounds = filter.sharedMesh.bounds;
            Vector3 axis = Vector3.right;
            if (bounds.size.y > bounds.size.x &&
                bounds.size.y >= bounds.size.z)
            {
                axis = Vector3.up;
            }
            else if (bounds.size.z > bounds.size.x &&
                     bounds.size.z > bounds.size.y)
            {
                axis = Vector3.forward;
            }

            return renderer.transform.TransformDirection(axis).normalized;
        }

        private static void AssertTransformMatchesMatrix(
            Transform actual,
            Matrix4x4 expected,
            string context)
        {
            Assert.That(actual, Is.Not.Null, context);
            Assert.That(
                Vector3.Distance(actual.position, expected.GetColumn(3)),
                Is.LessThan(0.0001f),
                context + " position");
            Assert.That(
                Quaternion.Angle(actual.rotation, expected.rotation),
                Is.LessThan(0.05f),
                context + " rotation");
        }

    }
}
