using System;
using System.IO;
using System.Linq;
using MSC.Interaction;
using MSC.Interaction.Capabilities;
using MSC.LegacyImport.Editor.Configuration;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.NWH;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaFrontSteeringAlignmentGeneratedTests
    {
        private DonorUnitySceneModel scene;

        [OneTimeSetUp]
        public void ReadDonor()
        {
            const string path = "Config/DonorPaths.local.json";
            if (!File.Exists(path))
            {
                Assert.Ignore("Private donor source paths are not configured.");
            }
            DonorPathConfiguration paths = DonorPathConfiguration.LoadFromFile(path);
            scene = DonorUnitySceneModel.Parse(Path.Combine(paths.DonorStagingDirectory,
                "raw/world/milestone-04a1/assetripper-unity-project/ExportedProject/Assets/_Scenes/GAME.unity"));
        }

        [Test]
        public void GeneratedAdjustmentTargetsPreserveDonorOuterBoneAndNutFrame()
        {
            GameObject prefab = LoadPrefab();
            AssemblySteeringAlignmentInteractionTarget[] targets = prefab
                .GetComponentsInChildren<AssemblySteeringAlignmentInteractionTarget>(true);
            Assert.That(targets.Length, Is.EqualTo(2));
            Assert.That(prefab.GetComponentsInChildren<AssemblySteeringAlignmentState>(true).Length,
                Is.EqualTo(2));
            SatsumaCanonicalNightTestShape.AssertCanonicalTargets(
                prefab.GetComponentsInChildren<AssemblyFastenerInteractionTarget>(true));
            Assert.That(targets.All(target => target.GetComponent<AssemblyFastenerInteractionTarget>() == null),
                Is.True, "Toe adjustment targets are not staged fasteners.");
            SatsumaFrontSuspensionCornerBinding[] corners = prefab
                .GetComponent<SatsumaFrontSuspensionController>().Corners;
            foreach (AssemblySteeringAlignmentInteractionTarget target in targets)
            {
                bool left = target.CornerId == "fl";
                DonorStaticRendererRecord source =
                    Phase1SatsumaBaselineBuilder.ReadReviewedSteeringAlignmentNut(scene, left);
                DonorTransformRecord marker = scene.GetTransform(left ? 64267L : 42271L);
                DonorTransformRecord visual = scene.GetTransform(
                    scene.GetTransformIdForGameObject(source.GameObjectId));
                SatsumaFrontSuspensionCornerBinding corner = corners.Single(value =>
                    value.CornerId == target.CornerId);
                Assert.That(target.transform.parent, Is.SameAs(corner.SteeringOuterTarget));
                Assert.That(target.Alignment.Part, Is.SameAs(corner.SteeringRodPresentation.Part));
                Assert.That(target.Alignment.transform, Is.SameAs(target.Alignment.Part.transform));
                Assert.That(Vector3.Distance(target.transform.localPosition, marker.LocalPosition),
                    Is.LessThan(0.000001f));
                Assert.That(Quaternion.Angle(target.transform.localRotation, marker.LocalRotation),
                    Is.LessThan(0.001f));
                Assert.That(target.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(Vector3.Distance(target.NutPresentation.localPosition,
                    Vector3.Scale(marker.LocalScale, visual.LocalPosition)), Is.LessThan(0.000001f));
                Assert.That(Vector3.Distance(target.NutPresentation.localScale,
                    marker.LocalScale), Is.LessThan(0.00001f));
                Mesh mesh = target.NutPresentation.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(AssetDatabase.GetAssetPath(mesh), Does.EndWith(source.MeshGuid + ".asset"));
                Assert.That(target.GetComponent<Collider>().isTrigger, Is.True);
                Assert.That(target.gameObject.layer,
                    Is.EqualTo(LayerMask.NameToLayer(FastenerToolRaycastLayer.Name)));
            }
        }

        [Test]
        public void SourceGuardDistinguishesToeAdjustmentFromTwelveMillimeterScrew()
        {
            foreach (bool left in new[] { true, false })
            {
                long markerGo = left ? 28212L : 6217L;
                long visualGo = left ? 34245L : 32418L;
                long wheelGo = left ? 29428L : 13829L;
                string adjustment = scene.GetMonoBehaviours(markerGo).Single(value =>
                    value.ComponentId == (left ? 112113L : 105829L)).SerializedBody;
                Assert.That(Phase1SatsumaBaselineBuilder.IsReviewedSteeringAlignmentScrew(
                    adjustment, visualGo, wheelGo), Is.True);
                Assert.That(Phase1SatsumaBaselineBuilder.IsReviewedSteeringAlignmentScrew(
                    adjustment, visualGo + 1, wheelGo), Is.False);
                string fastening = scene.GetMonoBehaviours(left ? 32885L : 34430L).Single(value =>
                    value.ComponentId == (left ? 113544L : 113958L)).SerializedBody;
                Assert.That(Phase1SatsumaBaselineBuilder.IsReviewedSteeringAlignmentScrew(
                    fastening, visualGo, wheelGo), Is.False);
            }
        }

        [Test]
        public void FrontLatchesAndInstallationOnlyGatesMatchDonor()
        {
            VehicleAssemblyController assembly = LoadPrefab().GetComponent<VehicleAssemblyController>();
            foreach (string corner in new[] { "fl", "fr" })
            {
                foreach (string slug in new[] { "wishbone", "spindle", "steering-rod" })
                {
                    MountPointDefinition mount = assembly.MountPoints.Single(value =>
                        value.MountId == "mount.satsuma." + slug + "-" + corner).Definition;
                    Assert.That(mount.FastenerGroup.AggregateMaximumTightness,
                        Is.EqualTo(slug == "wishbone" ? 16 : 8));
                    Assert.That(mount.FastenerGroup.BoltedOnThreshold,
                        Is.EqualTo(slug == "steering-rod" ? 8 : 2));
                    Assert.That(mount.FastenerGroup.BoltedOffThreshold, Is.Zero);
                }
                AssertInstallGate(assembly, "spindle-" + corner, "wishbone-" + corner);
                AssertInstallGate(assembly, "strut-" + corner, "spindle-" + corner);
            }
            Assert.That(assembly.Dependencies.Count(value =>
                value.Kind == AssemblyDependencyKind.InstallRequiresBolted), Is.Zero,
                "Donor Check bolts is an attempted-install outcome, not a preview blocker.");
        }

        private static void AssertInstallGate(VehicleAssemblyController assembly,
            string dependent, string prerequisite)
        {
            MountPointDefinition mount = assembly.MountPoints.Single(value =>
                value.MountId == "mount.satsuma." + dependent).Definition;
            Assert.That(mount.RequiredOccupiedMountIds,
                Does.Contain("mount.satsuma." + prerequisite));
            Assert.That(mount.InstallAttemptBoltedSupportMountId,
                Is.EqualTo("mount.satsuma." + prerequisite));
            Assert.That(mount.RequiredBoltedMountIds, Is.Empty,
                "An install-attempt check must not become a permanent structural constraint.");
        }

        private static GameObject LoadPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            Assert.That(prefab, Is.Not.Null);
            return prefab;
        }
    }

    public sealed class SatsumaFrontSteeringAlignmentTargetTests
    {
        private GameObject root;
        private PartInstance part;
        private AssemblySteeringAlignmentState alignment;
        private AssemblySteeringAlignmentInteractionTarget target;
        private Transform nut;
        private float initialTimeScale;
        private readonly InteractionContext context = default;
        private readonly IHeldToolIdentity wrench = new ToolIdentity("Wrench", "14");

        [SetUp]
        public void SetUp()
        {
            initialTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            root = new GameObject("Toe target test");
            part = root.AddComponent<PartInstance>();
            part.RuntimeState.SetInstalled("test.rod-mount", false);
            alignment = root.AddComponent<AssemblySteeringAlignmentState>();
            alignment.Configure(part);
            alignment.RestoreValidated(new AssemblySteeringAlignmentSaveDto());
            var marker = new GameObject("Toe marker");
            marker.transform.SetParent(root.transform, false);
            marker.AddComponent<SphereCollider>().isTrigger = true;
            nut = new GameObject("Toe nut").transform;
            nut.SetParent(marker.transform, false);
            Renderer renderer = nut.gameObject.AddComponent<MeshRenderer>();
            target = marker.AddComponent<AssemblySteeringAlignmentInteractionTarget>();
            target.Configure(alignment, nut, renderer, "fl");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
            Time.timeScale = initialTimeScale;
        }

        [Test]
        public void RequiresFourteenMillimeterWrenchAndConsumesOneSignedStep()
        {
            Assert.That(target.TryActivateHeldTool(new ToolIdentity("Wrench", "13"), context, 1f), Is.False);
            Assert.That(target.TryActivateHeldTool(new ToolIdentity("Ratchet", "14"), context, 1f), Is.False);
            Assert.That(target.TryActivateHeldTool(wrench, context, float.NaN), Is.False);
            Assert.That(target.TryActivateHeldTool(wrench, context, 0f), Is.False);
            Assert.That(alignment.AlignmentDegrees, Is.Zero);
            Assert.That(target.TryActivateHeldTool(wrench, context, 100f), Is.True);
            Assert.That(alignment.AlignmentDegrees, Is.EqualTo(-0.1f).Within(0.00001f));
            Assert.That(Quaternion.Angle(nut.localRotation, Quaternion.Euler(0f, 0f, -13f)),
                Is.LessThan(0.001f));
            int revision = alignment.Revision;
            Assert.That(target.TryActivateHeldTool(wrench, context, -100f), Is.True);
            Assert.That(alignment.Revision, Is.EqualTo(revision), "Input inside cooldown must not apply twice.");
        }

        [Test]
        public void ClampedAlignmentStillTurnsNutAndPublishesRevision()
        {
            alignment.RestoreValidated(new AssemblySteeringAlignmentSaveDto { alignmentDegrees = -6f });
            int revision = alignment.Revision;
            Assert.That(target.TryActivateHeldTool(wrench, context, 1f), Is.True);
            Assert.That(alignment.AlignmentDegrees, Is.EqualTo(-6f));
            Assert.That(alignment.Revision, Is.EqualTo(revision + 1));
            Assert.That(Quaternion.Angle(nut.localRotation, Quaternion.Euler(0f, 0f, -13f)),
                Is.LessThan(0.001f));
        }

        [Test]
        public void DirectionalHint_HidesTheClampedAlignmentDirection()
        {
            Assert.That(
                target.CanActivateHeldTool(
                    wrench,
                    context,
                    InteractionScrollDirection.Positive),
                Is.True);
            Assert.That(
                target.CanActivateHeldTool(
                    wrench,
                    context,
                    InteractionScrollDirection.Negative),
                Is.True);
            Assert.That(
                target.GetHeldToolScrollPrompt(
                    InteractionScrollDirection.Positive),
                Is.EqualTo("УМЕНЬШИТЬ СХОЖДЕНИЕ"));
            Assert.That(
                target.GetHeldToolScrollPrompt(
                    InteractionScrollDirection.Negative),
                Is.EqualTo("УВЕЛИЧИТЬ СХОЖДЕНИЕ"));

            alignment.RestoreValidated(
                new AssemblySteeringAlignmentSaveDto
                {
                    alignmentDegrees =
                        -AssemblySteeringAlignmentState.LimitDegrees,
                });
            Assert.That(
                target.CanActivateHeldTool(
                    wrench,
                    context,
                    InteractionScrollDirection.Positive),
                Is.False);
            Assert.That(
                target.CanActivateHeldTool(
                    wrench,
                    context,
                    InteractionScrollDirection.Negative),
                Is.True);

            alignment.RestoreValidated(
                new AssemblySteeringAlignmentSaveDto
                {
                    alignmentDegrees =
                        AssemblySteeringAlignmentState.LimitDegrees,
                });
            Assert.That(
                target.CanActivateHeldTool(
                    wrench,
                    context,
                    InteractionScrollDirection.Positive),
                Is.True);
            Assert.That(
                target.CanActivateHeldTool(
                    wrench,
                    context,
                    InteractionScrollDirection.Negative),
                Is.False);
        }

        [Test]
        public void RemovedRodHidesTargetWithoutResettingItsAlignment()
        {
            Assert.That(target.TryActivateHeldTool(wrench, context, -1f), Is.True);
            part.RuntimeState.SetLoose(Vector3.zero, Quaternion.identity);
            target.RefreshAvailability();
            Assert.That(target.GetComponent<Collider>().enabled, Is.False);
            Assert.That(nut.GetComponent<Renderer>().enabled, Is.False);
            Assert.That(target.TryActivateHeldTool(wrench, context, 1f), Is.False);
            Assert.That(alignment.AlignmentDegrees, Is.EqualTo(0.1f).Within(0.00001f));
            part.RuntimeState.SetInstalled("test.rod-mount", false);
            target.RefreshAvailability();
            Assert.That(target.GetComponent<Collider>().enabled, Is.True);
            Assert.That(nut.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(alignment.AlignmentDegrees, Is.EqualTo(0.1f).Within(0.00001f));
        }

        [Test]
        public void OnlyDirectionalWheelInputMutatesAndSnapUsesSpannerReferencePose()
        {
            int revision = alignment.Revision;
            target.ActivateTool(context);
            target.ActivateHeldTool(wrench, context);
            Assert.That(target.CanActivateTool(context), Is.False);
            Assert.That(target.CanActivateHeldTool(wrench, context), Is.False);
            Assert.That(alignment.Revision, Is.EqualTo(revision));
            Assert.That(target.ToolPrompt, Does.Not.Contain("°"), "Do not reveal diagnostic alignment in ordinary HUD.");
            Assert.That(target.TryResolveToolSnapAnchor(wrench, context, out Transform anchor), Is.True);
            Assert.That(anchor.localPosition, Is.EqualTo(new Vector3(-0.073f, 0.028f, 0.016f)));
            Assert.That(Quaternion.Angle(anchor.localRotation, Quaternion.Euler(0f, 0f, 160f)),
                Is.LessThan(0.001f));
            Assert.That(target.TryResolveToolSnapAnchor(new ToolIdentity("Ratchet", "14"), context, out _), Is.False);
        }

        [Test]
        public void PausedInputCannotChangeAlignmentOrTurnNut()
        {
            Time.timeScale = 0f;
            Assert.That(target.TryActivateHeldTool(wrench, context, 1f), Is.False);
            Assert.That(alignment.AlignmentDegrees, Is.Zero);
            Assert.That(nut.localRotation, Is.EqualTo(Quaternion.identity));
        }

        private sealed class ToolIdentity : IHeldToolIdentity
        {
            public ToolIdentity(string type, string variant)
            {
                ToolType = type;
                ToolVariant = variant;
            }
            public string ToolType { get; }
            public string ToolVariant { get; }
        }
    }
}
