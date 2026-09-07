using System;
using System.Linq;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Fixture = MSC.Tests.EditMode.VehicleAssembly.SatsumaOperatingSourceTests.Fixture;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaEngineMechanicalMotionTests
    {
        [Test]
        public void CanonicalMotionHasExplicitSafeBindingsAndDoesNotMoveAssemblyOrCollision()
        {
            using var f = new Fixture();
            var motion = f.Root.GetComponent<SatsumaEngineMechanicalMotion>();
            Assert.That(motion, Is.Not.Null); Assert.That(motion.Shafts.Length, Is.EqualTo(9));
            Assert.That(motion.Reciprocating.Length, Is.EqualTo(32));
            Assert.That(Phase1SatsumaEngineMotionAuthoring.ApplyToInstance(f.Assembly), Is.Zero);
            SetRpm(f, 600f);
            var transforms = f.Assembly.Parts.Select(p => p.transform).Concat(f.Root.GetComponentsInChildren<Collider>(true).Select(c => c.transform)).Distinct().ToArray();
            var positions = transforms.Select(t => t.position).ToArray(); var rotations = transforms.Select(t => t.rotation).ToArray();
            var meshes = motion.Reciprocating.Select(b => b.Filter.sharedMesh).ToArray();
            var originalVertices = meshes.Select(m => m.vertices).ToArray();
            string dto = JsonUtility.ToJson(f.Assembly.CaptureSaveData());
            for (int i = 0; i < 13; i++) motion.ApplyFrame(.01f);
            Assert.That(motion.CrankDegrees, Is.EqualTo(468f).Within(.002f));
            Assert.That(motion.CamDegrees, Is.EqualTo(234f).Within(.002f));
            for (int i = 0; i < transforms.Length; i++)
            {
                Assert.That(Vector3.Distance(transforms[i].position, positions[i]), Is.LessThan(1e-6f));
                Assert.That(Quaternion.Angle(transforms[i].rotation, rotations[i]), Is.LessThan(.001f));
            }
            for (int i = 0; i < meshes.Length; i++) Assert.That(meshes[i].vertices, Is.EqualTo(originalVertices[i]), "No shared mesh writes.");
            Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(dto), "Motion is not saved authority.");
            Assert.That(motion.Reciprocating.Any(b => b.Filter.sharedMesh.hideFlags == HideFlags.HideAndDontSave), Is.True);
            motion.enabled = false;
            motion.ResetPresentation(); // EditMode is not the live OnDisable lifecycle test.
            for (int i = 0; i < meshes.Length; i++) Assert.That(motion.Reciprocating[i].Filter.sharedMesh, Is.SameAs(meshes[i]));
        }

        [Test]
        public void ShaftPhaseComposesWithTuningAndPauseDoesNotAccumulateRotation()
        {
            using var f = new Fixture();
            var motion = f.Root.GetComponent<SatsumaEngineMechanicalMotion>();
            var cam = f.Part("camshaft-gear").GetComponent<AssemblyCamshaftTimingState>();
            var alternator = f.Part("alternator").GetComponent<AssemblyEngineAdjustmentState>();
            SetRpm(f, 600f);
            cam.RestoreValidated(new AssemblyCamshaftTimingSaveDto { angleDegrees = 10f });
            var altLeaf = motion.Shafts.Single(b => b.Drive == SatsumaMechanicalDrive.Alternator).Leaf;
            alternator.RefreshPresentation(); Quaternion altBase = altLeaf.localRotation;
            motion.ApplyFrame(.01f);
            Quaternion expectedCam = cam.MeshBaseLocalRotation * Quaternion.Euler(28f, 0f, 0f);
            Assert.That(Quaternion.Angle(cam.GearMesh.localRotation, expectedCam), Is.LessThan(.01f));
            Quaternion expectedAlt = altBase * Quaternion.Euler(36f * SatsumaEngineMechanicalMotion.AlternatorRatio, 0f, 0f);
            Assert.That(Quaternion.Angle(altLeaf.localRotation, expectedAlt), Is.LessThan(.02f));
            for (int i = 0; i < 120; i++) { alternator.RefreshPresentation(); motion.ApplyFrame(0f); }
            Assert.That(Quaternion.Angle(cam.GearMesh.localRotation, expectedCam), Is.LessThan(.01f));
            Assert.That(Quaternion.Angle(altLeaf.localRotation, expectedAlt), Is.LessThan(.02f));
            Assert.That(cam.AngleDegrees, Is.EqualTo(10f)); Assert.That(alternator.Setting, Is.EqualTo(7f));
            cam.RestoreValidated(new AssemblyCamshaftTimingSaveDto { angleDegrees = 15f }); motion.ApplyFrame(0f);
            Assert.That(Quaternion.Angle(cam.GearMesh.localRotation, cam.MeshBaseLocalRotation * Quaternion.Euler(33f, 0f, 0f)), Is.LessThan(.01f));
            motion.ResetPresentation();
            Assert.That(Quaternion.Angle(cam.GearMesh.localRotation, cam.MeshBaseLocalRotation * Quaternion.Euler(15f, 0f, 0f)), Is.LessThan(.01f));
        }

        [Test]
        public void BrokenDrivesStopOnlyTheirConnectedShaftsAndFanDoesNotRequireCrankRpm()
        {
            using var f = new Fixture(); var motion = f.Root.GetComponent<SatsumaEngineMechanicalMotion>();
            SetRpm(f, 600f); motion.ApplyFrame(.01f);
            var pump = motion.Shafts.Single(b => b.Drive == SatsumaMechanicalDrive.WaterPump).Leaf;
            Quaternion before = pump.localRotation; float camBefore = motion.CamDegrees;
            Assert.That(f.Assembly.TryBreakInstalledPart(f.Belt).Succeeded, Is.True);
            Assert.That(f.Assembly.TryBreakInstalledPart(f.Part("timing-chain")).Succeeded, Is.True);
            motion.ApplyFrame(.01f);
            Assert.That(Quaternion.Angle(pump.localRotation, before), Is.LessThan(.01f));
            Assert.That(motion.CamDegrees, Is.EqualTo(camBefore)); Assert.That(motion.CrankDegrees, Is.EqualTo(72f).Within(.002f));
            SetRpm(f, 0f, true);
            var fan = motion.Shafts.Single(b => b.Drive == SatsumaMechanicalDrive.RadiatorFan).Leaf;
            Quaternion fanBase = fan.localRotation; motion.ApplyFrame(.01f);
            Assert.That(Quaternion.Angle(fan.localRotation, fanBase * Quaternion.Euler(0f, 27f, 0f)), Is.LessThan(.01f));
            Assert.That(motion.CrankDegrees, Is.Zero);
            var saved = f.Host.State.CaptureDto(); Assert.That(f.Host.TryRestoreSimulationState(saved, out string failure), Is.True, failure);
            Assert.That(motion.CrankDegrees, Is.Zero);
            Assert.That(Quaternion.Angle(fan.localRotation, fanBase), Is.LessThan(.01f), "Restore clears transient phase only.");
        }

        [Test]
        public void PistonPairsAndValveOrderFollowAClosedFourStrokeCycle()
        {
            Assert.That(SatsumaReciprocatingMesh.PistonDisplacement(180f, 0), Is.EqualTo(-.050017f).Within(1e-6f));
            Assert.That(SatsumaReciprocatingMesh.PistonDisplacement(180f, 1), Is.EqualTo(.050017f).Within(1e-6f));
            for (int angle = 0; angle <= 720; angle += 7)
            {
                Assert.That(SatsumaReciprocatingMesh.PistonDisplacement(angle, 0), Is.EqualTo(SatsumaReciprocatingMesh.PistonDisplacement(angle, 3)).Within(1e-7f));
                Assert.That(SatsumaReciprocatingMesh.PistonDisplacement(angle, 1), Is.EqualTo(SatsumaReciprocatingMesh.PistonDisplacement(angle, 2)).Within(1e-7f));
                Assert.That(Mathf.Abs(SatsumaReciprocatingMesh.RodAngle(angle, 0)), Is.LessThan(12f));
            }
            int[] firing = { 0, 540, 180, 360 };
            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                Assert.That(SatsumaReciprocatingMesh.PistonDisplacement(720f, cylinder), Is.EqualTo(0f).Within(1e-6f));
                Assert.That(SatsumaReciprocatingMesh.ValveLift01(firing[cylinder] + 450f, cylinder * 2), Is.EqualTo(1f).Within(1e-6f));
                Assert.That(SatsumaReciprocatingMesh.ValveLift01(firing[cylinder] + 270f, cylinder * 2 + 1), Is.EqualTo(1f).Within(1e-6f));
                Assert.That(SatsumaReciprocatingMesh.ValveLift01(firing[cylinder], cylinder * 2), Is.Zero);
            }
        }

        [Test]
        public void ActualBeltUvTravelPreservesSharedMaterialAndFixedScaleAxis()
        {
            using var f = new Fixture();
            var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/InstalledAlternatorBeltPresentation.prefab");
            Assert.That(template, Is.Not.Null);
            var visual = new GameObject("TEST ONLY original item visual").transform; visual.SetParent(f.Belt.transform, false);
            var belt = f.Belt.gameObject.AddComponent<AssemblyAlternatorBeltPresentation>(); belt.Configure(f.Assembly, f.Belt, visual, template);
            var renderer = belt.InstalledRig.GetComponentInChildren<SkinnedMeshRenderer>(true);
            var material = renderer.sharedMaterial; string before = EditorJsonUtility.ToJson(material);
            Vector3 bodyPosition = f.Belt.transform.position; Quaternion bodyRotation = f.Belt.transform.rotation;
            var block = new MaterialPropertyBlock(); block.SetFloat("_TestUnrelatedProperty", .375f); renderer.SetPropertyBlock(block);
            belt.ApplyOperatingMotion(.25f, 7f, true); renderer.GetPropertyBlock(block);
            Assert.That(block.GetVector("_BaseColorMap_ST").w, Is.EqualTo(.25f));
            Assert.That(block.GetFloat("_TestUnrelatedProperty"), Is.EqualTo(.375f));
            Assert.That(renderer.bones[1].localScale.y, Is.EqualTo(1f));
            Assert.That(renderer.bones[1].localScale.x, Is.InRange(1f, 1.1f));
            Assert.That(EditorJsonUtility.ToJson(material), Is.EqualTo(before));
            belt.ResetOperatingMotion(); renderer.GetPropertyBlock(block);
            Assert.That(block.GetVector("_BaseColorMap_ST").w, Is.Zero);
            Assert.That(renderer.bones[1].localScale, Is.EqualTo(new Vector3(1.1f, 1f, 1.1f)));
            Assert.That(f.Belt.transform.position, Is.EqualTo(bodyPosition)); Assert.That(f.Belt.transform.rotation, Is.EqualTo(bodyRotation));
        }

        [Test]
        public void WarmMechanicalLoopReportsCpuAndHasNoPerFrameManagedAllocation()
        {
            using var f = new Fixture(); var motion = f.Root.GetComponent<SatsumaEngineMechanicalMotion>();
            SetRpm(f, 800f);
            for (int i = 0; i < 100; i++) motion.ApplyFrame(.016f);
            var watch = new System.Diagnostics.Stopwatch();
            long before = GC.GetAllocatedBytesForCurrentThread(); watch.Start();
            for (int i = 0; i < 200; i++) motion.ApplyFrame(.016f);
            watch.Stop(); long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Debug.Log("SATSUMA_MOTION_CPU frames=200 milliseconds=" + watch.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " allocatedBytes=" + allocated);
            Assert.That(allocated, Is.Zero, "No managed allocation after explicit bindings and mesh clones are warm.");
        }

        [Test]
        public void MeshIslandsRemainRigidAndFixedRockerShaftDoesNotDeform()
        {
            using var f = new Fixture(); var motion = f.Root.GetComponent<SatsumaEngineMechanicalMotion>();
            var piston = motion.Reciprocating.First(b => b.Kind == SatsumaReciprocatingKind.Piston && b.Cylinder == 0);
            Mesh original = piston.Filter.sharedMesh; var vertices = original.vertices;
            using (var mesh = new SatsumaReciprocatingMesh(piston))
            {
                mesh.Apply(90f, 450f); var changed = mesh.OwnedMesh.vertices;
                foreach (int group in new[] { 0, 1 })
                {
                    int[] ids = Enumerable.Range(0, vertices.Length).Where(i => piston.VertexGroups[i] == group).ToArray();
                    int a = ids.First(), b = ids.Last();
                    Assert.That(Vector3.Distance(changed[a], changed[b]), Is.EqualTo(Vector3.Distance(vertices[a], vertices[b])).Within(1e-5f));
                }
                Assert.That(Vector3.Distance(changed[0], vertices[0]), Is.GreaterThan(.005f));
            }
            Assert.That(piston.Filter.sharedMesh, Is.SameAs(original));
            var valves = motion.Reciprocating.Single(b => b.Kind == SatsumaReciprocatingKind.Valves);
            vertices = valves.Filter.sharedMesh.vertices;
            using var valveMesh = new SatsumaReciprocatingMesh(valves); valveMesh.Apply(0f, 450f);
            var moved = valveMesh.OwnedMesh.vertices;
            for (int i = 0; i < vertices.Length; i++)
                if (valves.VertexGroups[i] < 0) Assert.That(Vector3.Distance(vertices[i], moved[i]), Is.LessThan(1e-7f));
        }

        private static void SetRpm(Fixture f, float rpm, bool fan = false)
        {
            var dto = f.Host.State.CaptureDto(); dto.engineRpm = rpm; dto.engineStatus = rpm > 0f ? VehicleEngineStatus.Cranking : VehicleEngineStatus.Off;
            dto.satsumaOperatingState.radiatorFanRunning = fan;
            Assert.That(f.Host.TryRestoreSimulationState(dto, out string failure), Is.True, failure);
            f.Host.Root.SatsumaOperatingModel.Evaluate(f.Host.State, VehicleInputState.Neutral(), f.Source.CaptureConditions(), .02f);
        }
    }
}
