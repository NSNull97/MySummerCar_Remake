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
    public sealed class SatsumaVisualPacketRegressionTests
    {
        [Test]
        public void RotatingPumpFastenersFollowTheShaftWithoutMovingTheirInteractionTargets()
        {
            using var f = new Fixture(); var motion = f.Root.GetComponent<SatsumaEngineMechanicalMotion>();
            Assert.That(motion.Fasteners.Length, Is.GreaterThanOrEqualTo(7));
            var pump = motion.Shafts.Single(s => s.Owner == f.Part("water-pump-pulley"));
            var bolts = motion.Fasteners.Where(b => motion.Shafts[b.ShaftIndex] == pump).ToArray();
            Assert.That(bolts.Length, Is.EqualTo(4));
            var before = bolts.Select(b => b.Leaf.position).ToArray();
            var beforeRotations = bolts.Select(b => b.Leaf.rotation).ToArray();
            var targets = bolts.Select(b => b.Leaf.GetComponentInParent<AssemblyFastenerInteractionTarget>()).ToArray();
            var targetPositions = targets.Select(t => t.transform.position).ToArray();
            var dto = f.Host.State.CaptureDto(); dto.engineRpm = 600f;
            Assert.That(f.Host.TryRestoreSimulationState(dto, out _), Is.True);
            f.Host.Root.SatsumaOperatingModel.Evaluate(f.Host.State, VehicleInputState.Neutral(), f.Source.CaptureConditions(), .02f);
            Quaternion rest = pump.Leaf.rotation;
            motion.ApplyFrame(.01f);
            Quaternion delta = pump.Leaf.rotation * Quaternion.Inverse(rest);
            for (int i = 0; i < bolts.Length; i++)
            {
                Assert.That(Vector3.Distance(bolts[i].Leaf.position, pump.Leaf.position + delta * (before[i] - pump.Leaf.position)), Is.LessThan(1e-5f));
                Assert.That(targets[i].transform.position, Is.EqualTo(targetPositions[i]));
                // The central fastener rotates on its own axis; its center need not orbit.
                Assert.That(Quaternion.Angle(bolts[i].Leaf.rotation, beforeRotations[i]), Is.GreaterThan(20f));
            }
            for (int frame = 0; frame < 90; frame++) motion.ApplyFrame(0f);
            motion.ResetPresentation();
            for (int i = 0; i < bolts.Length; i++) Assert.That(Vector3.Distance(bolts[i].Leaf.position, before[i]), Is.LessThan(1e-5f));
        }

        [Test]
        public void RestorePeelsVibrationBeforeMechanicalFastenerOffsetsWithoutResidualPose()
        {
            using var f = new Fixture(); var motion = f.Root.GetComponent<SatsumaEngineMechanicalMotion>();
            var vibration = f.Root.GetComponent<SatsumaEngineVisualVibration>();
            var rest = motion.Fasteners.Select(b => b.Leaf.localPosition).ToArray();
            var rotations = motion.Fasteners.Select(b => b.Leaf.localRotation).ToArray();
            var dto = f.Host.State.CaptureDto(); dto.engineRpm = 600f;
            Assert.That(f.Host.TryRestoreSimulationState(dto, out _), Is.True);
            f.Host.Root.SatsumaOperatingModel.Evaluate(f.Host.State, VehicleInputState.Neutral(), f.Source.CaptureConditions(), .02f);
            motion.ApplyFrame(.01f); vibration.ApplyFrame(VehicleEngineStatus.Running, 900f, .1f, .016f);
            Assert.That(f.Host.TryRestoreSimulationState(dto, out _), Is.True);
            for (int i = 0; i < rest.Length; i++)
            {
                Assert.That(Vector3.Distance(motion.Fasteners[i].Leaf.localPosition, rest[i]), Is.LessThan(1e-6f));
                Assert.That(Quaternion.Angle(motion.Fasteners[i].Leaf.localRotation, rotations[i]), Is.LessThan(.01f));
            }
        }

        [Test]
        public void VibrationIncludesRuntimeConsumableAndExhaustButPreservesEveryPhysicalPoseAndSave()
        {
            using var f = new Fixture();
            var plug = f.Assembly.AllRuntimeParts.First(p => p.Definition.DefinitionId == SatsumaConsumableAssemblyRules.SparkPlugPartId);
            var go = new GameObject("TEST ONLY purchased plug renderer"); go.transform.SetParent(plug.transform, false); go.AddComponent<MeshRenderer>();
            var vibration = f.Root.GetComponent<SatsumaEngineVisualVibration>();
            vibration.RebuildBindings();
            var pipe = f.Part("exhaust-pipe").GetComponentsInChildren<MeshRenderer>(true).First(r => r.gameObject.activeSelf);
            var muffler = f.Part("exhaust-muffler").GetComponentsInChildren<MeshRenderer>(true).First(r => r.gameObject.activeSelf);
            var driven = new[] { go.transform, pipe.transform, muffler.transform };
            var rest = driven.Select(t => t.position).ToArray();
            var physical = f.Assembly.AllRuntimeParts.Select(p => p.transform).Concat(f.Root.GetComponentsInChildren<Collider>(true).Select(c => c.transform)).Distinct().ToArray();
            var positions = physical.Select(t => t.position).ToArray(); var rotations = physical.Select(t => t.rotation).ToArray();
            string before = JsonUtility.ToJson(f.Assembly.CaptureSaveData());
            vibration.ApplyFrame(VehicleEngineStatus.Running, 900f, .1f, .016f);
            for (int i = 0; i < driven.Length; i++) Assert.That(Vector3.Distance(driven[i].position, rest[i]), Is.GreaterThan(.000001f), driven[i].name);
            for (int i = 0; i < physical.Length; i++)
            { Assert.That(physical[i].position, Is.EqualTo(positions[i])); Assert.That(physical[i].rotation, Is.EqualTo(rotations[i])); }
            Assert.That(JsonUtility.ToJson(f.Assembly.CaptureSaveData()), Is.EqualTo(before));
            vibration.ApplyFrame(VehicleEngineStatus.Off, 0f, 0f, .016f);
            for (int i = 0; i < driven.Length; i++) Assert.That(Vector3.Distance(driven[i].position, rest[i]), Is.LessThan(1e-6f));
            Assert.That(f.Assembly.TryBreakInstalledPart(plug).Succeeded, Is.True);
            Vector3 loose = go.transform.position;
            vibration.ApplyFrame(VehicleEngineStatus.Running, 3000f, .4f, .016f);
            Assert.That(go.transform.position, Is.EqualTo(loose));
            vibration.ApplyFrame(VehicleEngineStatus.Off, 0, 0, .016f);
        }

        [Test]
        public void WarmVibrationCacheHasNoPerFrameAllocationsAndRespondsToRpm()
        {
            using var f = new Fixture(); var vibration = f.Root.GetComponent<SatsumaEngineVisualVibration>();
            for (int i = 0; i < 20; i++) vibration.ApplyFrame(VehicleEngineStatus.Running, 900f, 0f, .016f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200; i++) vibration.ApplyFrame(VehicleEngineStatus.Running, 3000f, .5f, .016f);
            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
            Assert.That(SatsumaEngineFeedbackRules.VibrationFrequencyHz(800f), Is.EqualTo(10.869565f).Within(.00001f));
            Assert.That(SatsumaEngineFeedbackRules.VibrationFrequencyHz(3000f), Is.GreaterThan(SatsumaEngineFeedbackRules.VibrationFrequencyHz(800f)));
            vibration.ApplyFrame(VehicleEngineStatus.Off, 0, 0, .016f);
        }

        [Test]
        public void ActualRadiatorFanRunsOnlyWhenHotAndPoweredIncludingIgnitionOff()
        {
            using var f = new Fixture(); var motion = f.Root.GetComponent<SatsumaEngineMechanicalMotion>();
            var fan = motion.Shafts.Single(s => s.Drive == SatsumaMechanicalDrive.RadiatorFan).Leaf;
            void Temperature(float degrees)
            {
                var dto = f.Host.State.CaptureDto(); dto.engineTemperatureCelsius = degrees; dto.coolantTemperatureCelsius = degrees;
                dto.engineStatus = VehicleEngineStatus.Off; dto.engineRpm = 0;
                Assert.That(f.Host.TryRestoreSimulationState(dto, out _), Is.True);
                f.Host.Root.Tick(.02f, VehicleInputState.Neutral(false));
            }
            Temperature(90f); Assert.That(f.Host.State.SatsumaOperating.RadiatorFanRunning, Is.False);
            Quaternion cold = fan.localRotation; motion.ApplyFrame(.01f); Assert.That(fan.localRotation, Is.EqualTo(cold));
            Temperature(101f); Assert.That(f.Host.State.SatsumaOperating.RadiatorFanRunning, Is.True);
            Quaternion hot = fan.localRotation; motion.ApplyFrame(.01f); Assert.That(Quaternion.Angle(hot, fan.localRotation), Is.GreaterThan(20f));
            var electrical = f.Root.GetComponent<SatsumaElectricalSystem>(); var wires = electrical.CaptureSaveData();
            wires.installedConnectionIds = wires.installedConnectionIds.Where(id => id != SatsumaElectricalConnection.RadiatorFan.ToString()).ToArray();
            Assert.That(electrical.TryRestore(wires, out _), Is.True);
            f.Host.Root.Tick(.02f, VehicleInputState.Neutral(false));
            Assert.That(f.Host.State.SatsumaOperating.RadiatorFanRunning, Is.False);
        }

        [Test]
        public void CanonicalInstalledBeltHasActualReviewedTextureAndRigBinding()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Phase1SatsumaInstalledBeltAssets.MaterialPath);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(Phase1SatsumaInstalledBeltAssets.Root + "/Textures/" + Phase1SatsumaInstalledBeltAssets.TextureGuid + ".png");
            Assert.That(texture, Is.Not.Null); Assert.That(material.GetTexture("_BaseColorMap"), Is.SameAs(texture));
            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Phase1SatsumaInstalledBeltAssets.Root + "/InstalledAlternatorBeltPresentation.prefab");
            Assert.That(rig.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMaterial, Is.SameAs(material));
        }
    }
}
