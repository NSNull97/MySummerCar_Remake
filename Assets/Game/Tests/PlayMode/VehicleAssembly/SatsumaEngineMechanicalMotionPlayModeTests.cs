using System.Collections;
using System.Linq;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    public sealed partial class SatsumaEngineFeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator MechanicalLateUpdatePauseRestoreDisableAndDetachPreservePresentationOwners()
        {
            var assembly = vehicle.GetComponent<VehicleAssemblyController>();
            var motion = vehicle.GetComponent<SatsumaEngineMechanicalMotion>();
            Assert.That(motion, Is.Not.Null);
            // Same controlled presentation-only fixture as the audio tests.
            // Mechanical assembly flows are tested with real graph restores separately.
            foreach (string suffix in new[] { "crankshaft", "camshaft", "camshaft-gear", "timing-chain", "piston1", "rocker-shaft", "radiator" })
            {
                var part = assembly.Parts.Single(p => p.Definition.DefinitionId == "vehicle.satsuma.part." + suffix);
                var mount = assembly.MountPoints.First(m => m.Definition.AcceptedPartDefinitionIds.Contains(part.Definition.DefinitionId));
                part.RuntimeState.SetInstalled(mount.MountId, false);
            }
            var timing = assembly.Parts.Single(p => p.Definition.DefinitionId == AssemblyCamshaftTimingState.PartDefinitionId)
                .GetComponent<AssemblyCamshaftTimingState>();
            timing.RestoreValidated(new AssemblyCamshaftTimingSaveDto());
            var piston = motion.Reciprocating.First(b => b.Kind == SatsumaReciprocatingKind.Piston && b.Cylinder == 0);
            var original = piston.Filter.sharedMesh;
            Vector3 partPosition = piston.Owner.transform.position;
            ApplyLiveTelemetry(VehicleEngineStatus.Cranking, 600f);
            yield return Frames(3);
            Assert.That(motion.CrankDegrees, Is.GreaterThan(0f));
            Assert.That(motion.CamDegrees, Is.EqualTo(motion.CrankDegrees * .5f).Within(.01f));
            Assert.That(piston.Filter.sharedMesh, Is.Not.SameAs(original));
            Assert.That(Vector3.Distance(piston.Owner.transform.position, partPosition), Is.LessThan(1e-6f));
            Time.timeScale = 0f; yield return Frames(2);
            float phase = motion.CrankDegrees;
            Vector3[] vertices = piston.Filter.sharedMesh.vertices;
            Quaternion gearPose = timing.GearMesh.localRotation;
            yield return Frames(5);
            Assert.That(motion.CrankDegrees, Is.EqualTo(phase));
            Assert.That(piston.Filter.sharedMesh.vertices, Is.EqualTo(vertices));
            Assert.That(Quaternion.Angle(timing.GearMesh.localRotation, gearPose), Is.LessThan(.01f));
            var dto = simulation.State.CaptureDto();
            Assert.That(simulation.TryRestoreSimulationState(dto, out string failure), Is.True, failure);
            Assert.That(motion.CrankDegrees, Is.Zero);
            Assert.That(piston.Filter.sharedMesh, Is.SameAs(original));
            Time.timeScale = 1f; yield return Frames(3);
            Assert.That(piston.Filter.sharedMesh, Is.Not.SameAs(original));
            motion.enabled = false;
            Assert.That(piston.Filter.sharedMesh, Is.SameAs(original), "Real OnDisable releases only owned meshes.");
            Assert.That(Quaternion.Angle(timing.GearMesh.localRotation, timing.MeshBaseLocalRotation), Is.LessThan(.01f));
            motion.enabled = true; yield return Frames(2);
            Assert.That(piston.Filter.sharedMesh, Is.Not.SameAs(original));
            piston.Owner.RuntimeState.SetLoose(piston.Owner.transform.position, piston.Owner.transform.rotation);
            yield return Frames(2);
            Assert.That(piston.Filter.sharedMesh, Is.SameAs(original), "Detached parts do not keep cycling.");
            Assert.That(simulation.FixedTickCount, Is.Zero);
        }
    }
}
