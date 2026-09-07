using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.NWH;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehiclePhysics
{
    public sealed class SatsumaFrontFastenerPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator FlushDeferredFixtureDestruction()
        {
            yield return null;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator FrontStrutAndTieFastenersFollowTheirDonorFrames()
        {
            GameObject instance = CreateFixture();
            GameObject[] patches = new GameObject[2];
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                SatsumaFrontSuspensionController rig = instance
                    .GetComponent<SatsumaFrontSuspensionController>();
                InstallFrontStructure(assembly, rig, tightenStruts: true);
                instance.GetComponent<NwhAssemblyWheelSupportController>()
                    .RefreshSupport(force: true);
                for (int step = 0; step < 45; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                yield return null;
                rig.ApplyNow();
                var initialPoses = new Dictionary<string, MarkerPose>();
                foreach (SatsumaFrontSuspensionCornerBinding corner in rig.Corners)
                {
                    AssertStrutCoverage(assembly, corner);
                    AssertSteeringEndpointIncludesDonorParent(corner);
                    foreach (AssemblyFastenerInteractionTarget marker in
                             GetCornerTargets(instance, corner))
                    {
                        AssertAvailable(marker, expected: true);
                        initialPoses.Add(marker.FastenerDefinitionId,
                            new MarkerPose(marker.transform));
                    }
                }

                for (int index = 0; index < rig.Corners.Length; index++)
                {
                    SatsumaFrontSuspensionCornerBinding corner = rig.Corners[index];
                    Assert.That(corner.Wheel.IsGrounded, Is.False, corner.CornerId);
                    Vector3 hub = corner.Wheel.WheelPosition;
                    patches[index] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    patches[index].name = "Front fastener travel contact " + corner.CornerId;
                    patches[index].layer = 0;
                    patches[index].transform.position = new Vector3(
                        hub.x, hub.y - corner.Wheel.Radius + 0.08f - 0.05f, hub.z);
                    patches[index].transform.localScale = new Vector3(0.4f, 0.1f, 0.4f);
                }

                Physics.SyncTransforms();
                for (int step = 0; step < 20; step++)
                {
                    yield return new WaitForFixedUpdate();
                }

                yield return null;
                rig.ApplyNow();
                for (int index = 0; index < rig.Corners.Length; index++)
                {
                    SatsumaFrontSuspensionCornerBinding corner = rig.Corners[index];
                    Assert.That(corner.Wheel.IsGrounded, Is.True, corner.CornerId);
                    Assert.That(corner.Wheel.HitCollider,
                        Is.SameAs(patches[index].GetComponent<Collider>()));
                    Assert.That(corner.Wheel.SpringMaxLength - corner.Wheel.SpringLength,
                        Is.GreaterThan(0.05f), corner.CornerId + " did not compress");
                    AssertMarkerFrames(instance, corner, initialPoses,
                        requireTravel: true, requireSteering: false);

                    // Exercise presentation against a nonzero live steering
                    // frame without altering production steering/physics tuning.
                    Transform nonRotating = corner.Wheel.wheel.nonRotatingContainer;
                    Assert.That(nonRotating, Is.Not.Null);
                    nonRotating.rotation = Quaternion.AngleAxis(
                        corner.LeftSide ? 23f : -19f, instance.transform.up) *
                        nonRotating.rotation;
                    rig.ApplyNow();
                    AssertMarkerFrames(instance, corner, initialPoses,
                        requireTravel: true, requireSteering: true);
                }
            }
            finally
            {
                Object.Destroy(instance);
                foreach (GameObject patch in patches)
                {
                    Object.Destroy(patch);
                }
            }
        }

        [UnityTest]
        public IEnumerator FrontFastenerStagesAndVisibilitySurviveRemovalRoundTrip()
        {
            GameObject instance = CreateFixture();
            try
            {
                VehicleAssemblyController assembly = instance
                    .GetComponent<VehicleAssemblyController>();
                SatsumaFrontSuspensionController rig = instance
                    .GetComponent<SatsumaFrontSuspensionController>();
                foreach (SatsumaFrontSuspensionCornerBinding corner in rig.Corners)
                {
                    foreach (AssemblyFastenerInteractionTarget marker in
                             GetCornerTargets(instance, corner))
                    {
                        marker.RefreshAvailability();
                        AssertAvailable(marker, expected: false);
                    }
                }

                InstallFrontStructure(assembly, rig, tightenStruts: false);
                yield return null;
                yield return new WaitForFixedUpdate();
                yield return null;
                foreach (SatsumaFrontSuspensionCornerBinding corner in rig.Corners)
                {
                    AssertStrutCoverage(assembly, corner);
                    MountPointRuntime strutMount = assembly.ResolveMount(corner.StrutMount);
                    AssemblyFastenerInteractionTarget[] markers = GetTargets(
                        instance, corner.StrutMount.MountId);
                    string[] ids = markers.Select(value => value.FastenerDefinitionId)
                        .OrderBy(value => value).ToArray();
                    foreach (FastenerInstance fastener in strutMount.Fasteners)
                    {
                        Assert.That(fastener.IsInserted, Is.True);
                        Assert.That(fastener.Stage, Is.Zero);
                        TurnToStage(assembly, strutMount, fastener,
                            fastener.Definition.MaximumStage);
                    }

                    Assert.That(strutMount.FastenerGroup.IsBolted, Is.True);
                    PartInstance strut = FindPart(assembly, "strut-" + corner.CornerId);
                    Assert.That(assembly.EvaluateRemoval(strut).Succeeded, Is.False);
                    AssemblyFastenerInteractionTarget tie = GetTargets(
                        instance, corner.SteeringRodMount.MountId).Single();
                    MountPointRuntime tieMount = assembly.ResolveMount(corner.SteeringRodMount);
                    FastenerInstance tieFastener = tieMount.Fasteners.Single();
                    Assert.That(tieFastener.Definition.Size, Is.EqualTo(FastenerSize.Millimeter12));
                    TurnToStage(assembly, tieMount, tieFastener, 3);
                    AssemblyOperationResult wrongSize = assembly.TryOperateFastener(
                        tieMount.MountId, tie.FastenerDefinitionId,
                        FindTool(assembly, FastenerSize.Millimeter14), tighten: true);
                    Assert.That(wrongSize.Succeeded, Is.False,
                        "The donor 14 mm toe adjuster must not impersonate the 12 mm joint bolt.");
                    Assert.That(tieFastener.Stage, Is.EqualTo(3));

                    rig.ApplyNow();
                    yield return null;
                    foreach (AssemblyFastenerInteractionTarget marker in markers)
                    {
                        AssertAvailable(marker, expected: true);
                        Assert.That(strutMount.TryGetFastener(marker.FastenerDefinitionId,
                            out FastenerInstance fastener), Is.True);
                        Assert.That(fastener.Stage,
                            Is.EqualTo(fastener.Definition.MaximumStage));
                    }
                    AssertAvailable(tie, expected: true);

                    foreach (FastenerInstance fastener in strutMount.Fasteners)
                    {
                        TurnToStage(assembly, strutMount, fastener, 0);
                    }
                    Assert.That(strutMount.FastenerGroup.IsBolted, Is.False);
                    // Reviewed Removal FSMs108086/108453 require the same-side
                    // steering rod to be absent, even after its bolt is loose.
                    AssemblyOperationResult blockedByRod = assembly.TryRemove(strut);
                    Assert.That(blockedByRod.Succeeded, Is.False);
                    Assert.That(blockedByRod.FailureReason, Is.EqualTo(AssemblyFailureReason.RemovalBlocked));
                    Assert.That(strut.IsInstalled, Is.True);
                    Assert.That(tieFastener.Stage, Is.EqualTo(3));
                    TurnToStage(assembly, tieMount, tieFastener, 0);
                    Assert.That(assembly.EvaluateRemoval(strut).FailureReason,
                        Is.EqualTo(AssemblyFailureReason.RemovalBlocked), "Rod presence, not its bolt latch, blocks strut removal.");
                    PartInstance rod = FindPart(assembly, "steering-rod-" + corner.CornerId);
                    AssemblyOperationResult rodRemoval = assembly.TryRemove(rod);
                    Assert.That(rodRemoval.Succeeded, Is.True, rodRemoval.Message);
                    rod.Body.isKinematic = true;
                    rod.Body.detectCollisions = false;
                    yield return null;
                    AssertAvailable(tie, expected: false);

                    // Preserve the independent-state regression using the
                    // opposite rod; the removed rod must reset its own bolt.
                    SatsumaFrontSuspensionCornerBinding otherCorner = rig.Corners.Single(value => value.CornerId != corner.CornerId);
                    MountPointRuntime otherTieMount = assembly.ResolveMount(otherCorner.SteeringRodMount);
                    FastenerInstance otherTieFastener = otherTieMount.Fasteners.Single();
                    TurnToStage(assembly, otherTieMount, otherTieFastener, 3);
                    AssemblyFastenerInteractionTarget otherTie = GetTargets(instance, otherTieMount.MountId).Single();
                    AssemblyOperationResult removal = assembly.TryRemove(strut);
                    Assert.That(removal.Succeeded, Is.True, removal.Message);
                    strut.Body.isKinematic = true;
                    strut.Body.detectCollisions = false;
                    yield return null;
                    foreach (AssemblyFastenerInteractionTarget marker in markers)
                    {
                        AssertAvailable(marker, expected: false);
                    }
                    Assert.That(strutMount.Fasteners.All(value =>
                        !value.IsInserted && value.Stage == 0), Is.True);
                    Assert.That(otherTieFastener.Stage, Is.EqualTo(3),
                        "Removing a strut must not overwrite another mount's fastener state.");
                    Assert.That(otherTieFastener.IsInserted, Is.True);
                    AssertAvailable(otherTie, expected: true);
                    Assert.That(tieFastener.IsInserted, Is.False);
                    Assert.That(tieFastener.Stage, Is.Zero);
                    AssertAvailable(tie, expected: false);

                    InstallAtMount(assembly, strut, corner.StrutMount, tighten: false);
                    yield return null;
                    yield return new WaitForFixedUpdate();
                    yield return null;
                    Assert.That(GetTargets(instance, corner.StrutMount.MountId)
                        .Select(value => value.FastenerDefinitionId).OrderBy(value => value),
                        Is.EqualTo(ids));
                    foreach (AssemblyFastenerInteractionTarget marker in markers)
                    {
                        AssertAvailable(marker, expected: true);
                    }
                    Assert.That(strutMount.Fasteners.All(value =>
                        value.IsInserted && value.Stage == 0), Is.True,
                        "Reinstall uses the same IDs with fresh inserted stages, not stale tightness.");

                    AssertAvailable(tie, expected: false);
                    InstallAtMount(assembly, rod, corner.SteeringRodMount, tighten: false);
                    yield return null;
                    AssertAvailable(tie, expected: true);
                    Assert.That(tieFastener.IsInserted, Is.True);
                    Assert.That(tieFastener.Stage, Is.Zero);
                }
            }
            finally
            {
                Object.Destroy(instance);
            }
        }

        private static GameObject CreateFixture()
        {
            GameObject prefab = Resources.Load<GameObject>("Phase1Vehicles/Satsuma_Phase1_V1a");
            if (prefab == null)
            {
                Assert.Ignore("Private donor-derived Satsuma baseline is unavailable.");
            }

            GameObject instance = Object.Instantiate(prefab,
                new Vector3(0f, 2f, 0f), Quaternion.identity);
            Rigidbody chassis = instance.GetComponent<Rigidbody>();
            chassis.useGravity = false;
            chassis.constraints = RigidbodyConstraints.FreezeAll;
            VehicleAssemblyController assembly = instance.GetComponent<VehicleAssemblyController>();
            foreach (PartInstance part in assembly.Parts)
            {
                if (part == null || part.IsAssemblyRoot || part.Body == null)
                {
                    continue;
                }
                part.Body.isKinematic = true;
                part.Body.detectCollisions = false;
            }
            return instance;
        }

        private static void InstallFrontStructure(VehicleAssemblyController assembly,
            SatsumaFrontSuspensionController rig, bool tightenStruts)
        {
            InstallAtMount(assembly, FindPart(assembly, "sub-frame"),
                FindMount(assembly, "sub-frame"), tighten: true);
            InstallAtMount(assembly, FindPart(assembly, "steering-rack"),
                FindMount(assembly, "steering-rack"), tighten: true);
            foreach (SatsumaFrontSuspensionCornerBinding corner in rig.Corners)
            {
                InstallAtMount(assembly, FindPart(assembly, "wishbone-" + corner.CornerId),
                    corner.WishboneMount, tighten: true);
                InstallAtMount(assembly, FindPart(assembly, "spindle-" + corner.CornerId),
                    corner.SpindleMount, tighten: true);
                InstallAtMount(assembly, FindPart(assembly, "strut-" + corner.CornerId),
                    corner.StrutMount, tightenStruts);
                InstallAtMount(assembly, FindPart(assembly, "steering-rod-" + corner.CornerId),
                    corner.SteeringRodMount, tighten: true);
            }
        }

        private static void AssertStrutCoverage(VehicleAssemblyController assembly,
            SatsumaFrontSuspensionCornerBinding corner)
        {
            FastenerDefinition[] definitions = corner.StrutMount.Definition.Fasteners;
            Assert.That(definitions, Has.Length.EqualTo(7), corner.CornerId);
            Assert.That(definitions.Count(value => value.Size == FastenerSize.Millimeter10),
                Is.EqualTo(3), corner.CornerId + " upper bolts");
            Assert.That(definitions.Count(value => value.Size == FastenerSize.Millimeter9),
                Is.EqualTo(4), corner.CornerId + " lower bolts");
            Assert.That(assembly.ResolveMount(corner.StrutMount).Fasteners.Length,
                Is.EqualTo(7));
            AssemblyFastenerInteractionTarget[] markers = GetTargets(
                assembly.gameObject, corner.StrutMount.MountId);
            Assert.That(markers, Has.Length.EqualTo(7));
            Assert.That(markers.Select(value => value.FastenerDefinitionId),
                Is.EquivalentTo(definitions.Select(value => value.DefinitionId)));
            Assert.That(GetTargets(assembly.gameObject, corner.SteeringRodMount.MountId),
                Has.Length.EqualTo(1));
            Assert.That(corner.SteeringRodMount.Definition.Fasteners.Single().Size,
                Is.EqualTo(FastenerSize.Millimeter12));
        }

        private static void AssertMarkerFrames(GameObject instance,
            SatsumaFrontSuspensionCornerBinding corner,
            IReadOnlyDictionary<string, MarkerPose> initialPoses,
            bool requireTravel, bool requireSteering)
        {
            AssertSteeringEndpointIncludesDonorParent(corner);
            foreach (AssemblyFastenerInteractionTarget marker in GetCornerTargets(instance, corner))
            {
                MarkerPose initial = initialPoses[marker.FastenerDefinitionId];
                bool moving = marker.MountId == corner.SteeringRodMount.MountId ||
                    marker.FastenerDefinitionId.Contains(".lower-");
                Transform expectedParent = moving
                    ? corner.ShockBottomTarget : corner.StrutMount.transform;
                Assert.That(marker.transform.parent, Is.SameAs(expectedParent),
                    marker.FastenerDefinitionId + " uses the wrong donor frame");
                Assert.That(Vector3.Distance(marker.transform.localPosition, initial.LocalPosition),
                    Is.LessThan(0.00001f), marker.FastenerDefinitionId);
                Assert.That(Quaternion.Angle(marker.transform.localRotation, initial.LocalRotation),
                    Is.LessThan(0.01f), marker.FastenerDefinitionId);
                if (moving)
                {
                    Assert.That(Vector3.Distance(marker.transform.localPosition,
                        ExpectedMovingMarkerPosition(corner.CornerId, marker.FastenerDefinitionId)),
                        Is.LessThan(0.00001f), marker.FastenerDefinitionId + " donor position");
                    Quaternion hubRotation = corner.Wheel.wheel.nonRotatingContainer.rotation;
                    Vector3 bottomPosition = corner.Wheel.WheelPosition +
                        hubRotation * corner.HubToShockBottomLocalOffset;
                    Quaternion bottomRotation = hubRotation * corner.ShockBottomLocalRotation;
                    Assert.That(Vector3.Distance(marker.transform.position,
                        bottomPosition + bottomRotation * initial.LocalPosition),
                        Is.LessThan(0.004f), marker.FastenerDefinitionId);
                    if (requireTravel)
                    {
                        Assert.That(marker.transform.position.y - initial.WorldPosition.y,
                            Is.GreaterThan(0.045f), marker.FastenerDefinitionId + " stayed behind");
                    }
                    if (requireSteering)
                    {
                        Assert.That(Quaternion.Angle(marker.transform.rotation, initial.WorldRotation),
                            Is.GreaterThan(15f), marker.FastenerDefinitionId + " lost steering");
                    }
                }
                else
                {
                    Assert.That(Vector3.Distance(marker.transform.position, initial.WorldPosition),
                        Is.LessThan(0.001f), marker.FastenerDefinitionId + " moved off the body tower");
                    Assert.That(Quaternion.Angle(marker.transform.rotation, initial.WorldRotation),
                        Is.LessThan(0.1f), marker.FastenerDefinitionId);
                }
                AssertAvailable(marker, expected: true);
            }
        }

        private static void AssertSteeringEndpointIncludesDonorParent(
            SatsumaFrontSuspensionCornerBinding corner)
        {
            // Independent frozen donor hierarchy: hub -> OFFSET (+/-50 mm)
            // -> pivot_steering. The old builder dropped the middle transform.
            Vector3 endpointInOffset = corner.LeftSide
                ? new Vector3(0.06290412f, 0.0357951969f, -0.117504239f)
                : new Vector3(-0.06423402f, 0.0334164947f, -0.1176604f);
            Vector3 offsetInHub = Vector3.right * (corner.LeftSide ? 0.05f : -0.05f);
            Quaternion hubRotation = corner.Wheel.wheel.nonRotatingContainer.rotation;
            Vector3 expected = corner.Wheel.WheelPosition +
                hubRotation * (offsetInHub + endpointInOffset);
            Assert.That(Vector3.Distance(corner.SteeringOuterTarget.position, expected),
                Is.LessThan(0.0001f), corner.CornerId + " lost the donor OFFSET parent");

            SkinnedMeshRenderer renderer = corner.SteeringRodPresentation.InstalledRenderer;
            Assert.That(renderer.enabled, Is.True);
            Assert.That(renderer.bones[1], Is.SameAs(corner.SteeringOuterTarget));
            Mesh source = renderer.sharedMesh;
            BoneWeight[] weights = source.boneWeights;
            int outerVertex = System.Array.FindIndex(weights, value =>
                value.boneIndex0 == 1 && value.weight0 > 0.9999f);
            Assert.That(outerVertex, Is.GreaterThanOrEqualTo(0),
                "The donor skin must contain a vertex fully bound to its outer joint.");
            Mesh baked = new Mesh();
            try
            {
                renderer.BakeMesh(baked);
                Vector3 boneLocalVertex = source.bindposes[1].MultiplyPoint3x4(
                    source.vertices[outerVertex]);
                Vector3 expectedVertex = expected +
                    corner.SteeringOuterTarget.rotation * boneLocalVertex;
                Assert.That(Vector3.Distance(renderer.transform.TransformPoint(
                    baked.vertices[outerVertex]), expectedVertex),
                    Is.LessThan(0.0002f),
                    corner.CornerId + " moved the target but not the rendered rod end");
            }
            finally
            {
                Object.Destroy(baked);
            }
        }

        private static void AssertAvailable(AssemblyFastenerInteractionTarget marker, bool expected)
        {
            Collider collider = marker.GetComponent<Collider>();
            Assert.That(collider, Is.Not.Null, marker.FastenerDefinitionId);
            Assert.That(collider.enabled, Is.EqualTo(expected), marker.FastenerDefinitionId);
            Renderer[] renderers = marker.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers, Has.Length.EqualTo(1), marker.FastenerDefinitionId);
            Assert.That(renderers[0].enabled, Is.EqualTo(expected), marker.FastenerDefinitionId);
            if (!expected)
            {
                return;
            }

            Assert.That(marker.gameObject.activeInHierarchy, Is.True, marker.FastenerDefinitionId);
            Physics.SyncTransforms();
            Vector3 center = collider.bounds.center;
            Vector3 direction = marker.transform.forward;
            Assert.That(collider.Raycast(new Ray(center + direction * 0.2f, -direction),
                out _, 0.4f), Is.True, marker.FastenerDefinitionId + " is not targetable");
        }

        private static AssemblyFastenerInteractionTarget[] GetCornerTargets(GameObject instance,
            SatsumaFrontSuspensionCornerBinding corner) => instance
            .GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
            .Where(value => value.MountId == corner.StrutMount.MountId ||
                value.MountId == corner.SteeringRodMount.MountId).ToArray();

        private static AssemblyFastenerInteractionTarget[] GetTargets(GameObject instance,
            string mountId) => instance.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true)
            .Where(value => value.MountId == mountId).ToArray();

        private static Vector3 ExpectedMovingMarkerPosition(string cornerId, string markerId)
        {
            // Frozen GAME.unity pivot_shock_fl/fr marker-local evidence. The
            // old 14 mm toe adjuster is intentionally absent from this table.
            Vector3[] left =
            {
                new Vector3(-0.067899525f, 0.017903062f, -0.031968057f),
                new Vector3(-0.048283134f, 0.018194495f, -0.031866100f),
                new Vector3(-0.048617974f, 0.018223794f, 0.032312598f),
                new Vector3(-0.068148300f, 0.018928653f, 0.032209553f),
            };
            Vector3[] right =
            {
                new Vector3(-0.068924820f, -0.017994210f, 0.033078585f),
                new Vector3(-0.049415477f, -0.017291188f, 0.033180580f),
                new Vector3(-0.068590510f, -0.017964965f, -0.031099085f),
                new Vector3(-0.0491532f, -0.017268244f, -0.030998401f),
            };
            if (markerId.EndsWith(".outer-joint"))
            {
                return cornerId == "fl"
                    ? new Vector3(-0.058095075f, 0.0042293663f, 0.11687624f)
                    : new Vector3(-0.058272287f, -0.0051064105f, 0.11442554f);
            }

            Assert.That(markerId.Contains(".lower-"), Is.True, markerId);
            int ordinal = int.Parse(markerId.Substring(markerId.LastIndexOf('-') + 1));
            Assert.That(ordinal, Is.InRange(1, 4));
            return (cornerId == "fl" ? left : right)[ordinal - 1];
        }

        private static void InstallAtMount(VehicleAssemblyController assembly,
            PartInstance part, MountPointAuthoring mount, bool tighten)
        {
            part.transform.SetPositionAndRotation(mount.Pose.position, mount.Pose.rotation);
            part.Body.position = mount.Pose.position;
            part.Body.rotation = mount.Pose.rotation;
            AssemblyOperationResult result = assembly.TryInstall(part, mount);
            Assert.That(result.Succeeded, Is.True, result.Message);
            if (tighten)
            {
                MountPointRuntime runtimeMount = assembly.ResolveMount(mount);
                foreach (FastenerInstance fastener in runtimeMount.Fasteners)
                {
                    TurnToStage(assembly, runtimeMount, fastener, fastener.Definition.MaximumStage);
                }
            }
        }

        private static void TurnToStage(VehicleAssemblyController assembly,
            MountPointRuntime mount, FastenerInstance fastener, int requestedStage)
        {
            int turns = 0;
            while (fastener.Stage != requestedStage)
            {
                Assert.That(turns++, Is.LessThan(fastener.Definition.MaximumStage + 2));
                AssemblyOperationResult result = assembly.TryOperateFastener(
                    mount.MountId, fastener.Definition.DefinitionId,
                    FindTool(assembly, fastener.Definition.Size),
                    tighten: fastener.Stage < requestedStage);
                Assert.That(result.Succeeded, Is.True, result.Message);
            }
        }

        private static ToolDefinition FindTool(VehicleAssemblyController assembly, FastenerSize size) =>
            assembly.Tools.First(value => value != null && value.Size == size);

        private static PartInstance FindPart(VehicleAssemblyController assembly, string slug) =>
            assembly.Parts.Single(value => value.Definition != null &&
                value.Definition.DefinitionId == "vehicle.satsuma.part." + slug);

        private static MountPointAuthoring FindMount(VehicleAssemblyController assembly, string slug) =>
            assembly.MountPoints.Single(value => value.MountId == "mount.satsuma." + slug);

        private readonly struct MarkerPose
        {
            public MarkerPose(Transform marker)
            {
                LocalPosition = marker.localPosition;
                LocalRotation = marker.localRotation;
                WorldPosition = marker.position;
                WorldRotation = marker.rotation;
            }

            public Vector3 LocalPosition { get; }
            public Quaternion LocalRotation { get; }
            public Vector3 WorldPosition { get; }
            public Quaternion WorldRotation { get; }
        }
    }
}
