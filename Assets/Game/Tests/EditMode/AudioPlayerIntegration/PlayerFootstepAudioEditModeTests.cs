using MSC.Audio;
using MSC.Audio.PlayerIntegration;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.AudioPlayerIntegration
{
    public sealed class PlayerFootstepAudioEditModeTests
    {
        [Test]
        public void CadenceUsesObservedDistanceAndRejectsTeleport()
        {
            var cadence = new FootstepCadenceTracker(
                walkingStepDistanceMeters: 1.5f,
                crouchingStepDistanceMeters: 1f,
                teleportThresholdMeters: 2.5f);

            cadence.Reset(Vector3.zero);
            Assert.That(
                cadence.Advance(Vector3.zero, isGrounded: true, isCrouching: false),
                Is.False,
                "Input intent without displacement must not create a footstep.");
            Assert.That(
                cadence.Advance(Vector3.right, isGrounded: true, isCrouching: false),
                Is.False);
            Assert.That(
                cadence.Advance(Vector3.right * 1.6f, isGrounded: true, isCrouching: false),
                Is.True);

            Assert.That(
                cadence.Advance(Vector3.right * 20f, isGrounded: true, isCrouching: false),
                Is.False,
                "A streamed-world relocation must reset cadence without a sound burst.");
            Assert.That(cadence.AccumulatedDistanceMeters, Is.Zero);
        }

        [Test]
        public void CadenceUsesShorterCrouchingStepDistanceAndResetsInAir()
        {
            var cadence = new FootstepCadenceTracker(1.5f, 0.8f, 2.5f);
            cadence.Reset(Vector3.zero);

            Assert.That(
                cadence.Advance(Vector3.right * 0.9f, true, true),
                Is.True);
            Assert.That(
                cadence.Advance(Vector3.right * 1.5f, false, true),
                Is.False);
            Assert.That(cadence.AccumulatedDistanceMeters, Is.Zero);
        }

        [Test]
        public void TypedSurfaceMappingCoversWetAndVehicleMetadata()
        {
            var wetWood = new AudioSurfaceMetadata(
                AudioSurfaceKind.Wood,
                wetness01: 0.75f,
                roughness01: 0.4f);
            Assert.That(
                AudioSurfaceMapping.ToFootstepSwitch(wetWood),
                Is.EqualTo(AudioProjectIds.Switches.FootstepSurfaceWet));

            var owner = new GameObject("VehicleSurfaceMetadataTest");
            try
            {
                VehicleSurfaceMetadataAuthoring vehicleSurface =
                    owner.AddComponent<VehicleSurfaceMetadataAuthoring>();
                vehicleSurface.Configure(VehicleSurfaceType.Gravel);

                IAudioSurfaceMetadataProvider provider = vehicleSurface;
                Assert.That(provider.AudioSurface.Kind, Is.EqualTo(AudioSurfaceKind.Gravel));
                Assert.That(
                    AudioSurfaceMapping.ToFootstepSwitch(provider.AudioSurface),
                    Is.EqualTo(AudioProjectIds.Switches.FootstepSurfaceGravel));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }
    }
}
