using System.Collections;
using System.Linq;
using MSC.Audio;
using MSC.Audio.UnityFallback;
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
        public IEnumerator SelectedEngineMixReachesCanonicalEmittersCrankIdleHeadroomAndUserControls()
        {
            var library = Resources.Load<UnityAudioEventLibrary>(SatsumaEngineFeedbackPresenter.FallbackResourcesPath);
            var selected = Resources.Load<UnityAudioEventLibrary>("Phase1UserSelectedAudio/Phase1UserSelectedAudioEventLibrary");
            Assert.That(library, Is.Not.Null);
            Assert.That(selected, Is.Not.Null);
            var backendObject = new GameObject("Real Satsuma event mix backend");
            backendObject.transform.SetParent(vehicle.transform, false);
            var fallback = backendObject.AddComponent<UnityAudioBackend>();
            fallback.ConfigureForAuthoring(library);
            Assert.That(fallback.TrySetReplacementEventLibrary(selected, out string failure), Is.True, failure);
            if (!Object.FindObjectsByType<AudioListener>().Any(value => value.isActiveAndEnabled))
            {
                var listenerObject = new GameObject("Explicit Satsuma mix listener");
                listenerObject.transform.SetParent(vehicle.transform, false);
                listenerObject.AddComponent<AudioListener>();
            }
            AudioSettingsState Settings(float master = 1f, float engine = 1f) =>
                new(master, engine, 1f, 1f, 1f, 1f, AudioDynamicRangeMode.Wide, false, false, false, false);
            fallback.ApplySettings(Settings());
            var assembly = vehicle.GetComponent<VehicleAssemblyController>();
            // The existing fixture controls telemetry and occupancy, not the
            // mechanics of installing an exhaust or starting a physical engine.
            foreach (var part in new[] { presenter.ExhaustBinding.Headers, presenter.ExhaustBinding.Pipe, presenter.ExhaustBinding.Muffler })
            {
                var mount = assembly.MountPoints.Single(value => value.Definition != null &&
                    value.Definition.AcceptedPartDefinitionIds.Contains(part.Definition.DefinitionId));
                part.RuntimeState.SetInstalled(mount.MountId, false);
            }
            presenter.ExhaustBinding.Invalidate();
            Assert.That(presenter.ConfigureBackend(fallback), Is.True, presenter.LastFailure);
            AudioSource Voice(AudioEventId id)
            {
                Assert.That(selected.TryResolve(id, out var definition), Is.True);
                return backendObject.GetComponentsInChildren<AudioSource>().Single(value => value.clip == definition.Clip);
            }

            ApplyLiveTelemetry(VehicleEngineStatus.Cranking, 250f);
            Assert.That(fallback.TryGetEventDurationSeconds(SatsumaEngineAudioIds.StarterEngaged, out float leadSeconds), Is.True);
            Assert.That(leadSeconds, Is.InRange(.324f, .328f), "Silent lead tail must not postpone the cranking bed to .534s.");
            yield return Frames(19); // .38s: after the prepared lead, before the old padded lead ended.
            AudioSource starter = Voice(SatsumaEngineAudioIds.StarterLoop);
            Assert.That(starter.isPlaying, Is.True);
            Assert.That(starter.volume, Is.EqualTo(1f));
            Assert.That(starter.GetComponent<UnityAudioCalibrationFilter>().OverflowGain,
                Is.EqualTo(.32f * 1.5848932f * Mathf.Pow(10f, 12.5f / 20f)).Within(.00001f));
            Assert.That(Vector3.Distance(starter.transform.position, presenter.EngineEmitter.AudioTransform.position), Is.LessThan(.002f));

            ApplyLiveTelemetry(VehicleEngineStatus.Running, 800f);
            yield return Frames(3);
            Assert.That(presenter.ExhaustBinding.CurrentOutlet, Is.EqualTo(SatsumaExhaustOutlet.Muffler));
            AudioSource coast = Voice(SatsumaEngineAudioIds.EngineCoastLoop);
            float idleGain = .75f;
            Assert.That(coast.volume, Is.EqualTo(idleGain).Within(.00001f));
            Assert.That(coast.pitch, Is.EqualTo(.57f).Within(.00001f));
            Assert.That(coast.rolloffMode, Is.EqualTo(AudioRolloffMode.Logarithmic));
            Assert.That(coast.minDistance, Is.EqualTo(2f));
            Assert.That(coast.maxDistance, Is.EqualTo(40f));
            Assert.That(coast.GetComponent<UnityAudioCalibrationFilter>().enabled, Is.False);
            block.transform.position += new Vector3(2f, .3f, -1f);
            yield return Frames(3);
            Assert.That(Vector3.Distance(coast.transform.position, block.transform.position), Is.LessThan(.002f));
            AudioSource exhaust = Voice(SatsumaEngineAudioIds.ExhaustLoop);
            Assert.That(Vector3.Distance(exhaust.transform.position, presenter.ExhaustBinding.Emitter.AudioTransform.position), Is.LessThan(.002f));

            fallback.ApplySettings(Settings(master: .5f));
            yield return Frames(3);
            Assert.That(coast.volume, Is.EqualTo(idleGain * .5f).Within(.00001f));
            fallback.ApplySettings(Settings(engine: 0f));
            yield return Frames(3);
            Assert.That(backendObject.GetComponentsInChildren<AudioSource>().Where(value => value.isPlaying)
                .All(value => value.volume == 0f && value.GetComponent<UnityAudioCalibrationFilter>().OverflowGain == 1f), Is.True);
            fallback.ApplySettings(Settings());

            ApplyLiveTelemetry(VehicleEngineStatus.Running, 3000f, .45f);
            yield return Frames(3);
            Assert.That(Voice(SatsumaEngineAudioIds.EngineThrottleLoop).volume, Is.EqualTo(.6f).Within(.00001f));
            fallback.ApplySettings(Settings(master: .5f));
            yield return Frames(3);
            Assert.That(Voice(SatsumaEngineAudioIds.EngineThrottleLoop).volume, Is.EqualTo(.3f).Within(.00001f),
                "The added-boost ceiling must follow user volume, not flatten the slider.");
            fallback.ApplySettings(Settings());
            ApplyLiveTelemetry(VehicleEngineStatus.Running, 6000f, 1f);
            yield return Frames(3);
            AudioSource throttle = Voice(SatsumaEngineAudioIds.EngineThrottleLoop);
            Assert.That(throttle.volume, Is.EqualTo(.45f * 1.5848932f * Mathf.Pow(10f, 1f / 20f)).Within(.00001f),
                "The explicitly selected throttle calibration gains 1 dB; unchanged base RTPC and user controls remain authoritative.");
            Assert.That(throttle.GetComponent<UnityAudioCalibrationFilter>().enabled, Is.False);
            Assert.That(coast.volume, Is.Zero, "Closed coast RTPC remains silent.");
            ApplyLiveTelemetry(VehicleEngineStatus.Running, 8000f, 0f);
            yield return Frames(3);
            Assert.That(coast.volume, Is.EqualTo(.75f).Within(.00001f));
            Assert.That(coast.GetComponent<UnityAudioCalibrationFilter>().OverflowGain, Is.EqualTo(1f),
                "Selected coast headroom must bound the calibrated base, not merely added boost.");
            fallback.ApplySettings(Settings(master: .5f)); yield return Frames(3);
            Assert.That(coast.volume, Is.EqualTo(.375f).Within(.00001f));
            ApplyLiveTelemetry(VehicleEngineStatus.Off, 0f);
            yield return Frames(3);
            Assert.That(fallback.ActiveGenericVoiceCount, Is.Zero);
            Assert.That(simulation.FixedTickCount, Is.Zero, "This is real presentation lifecycle, not a physical engine-start claim.");
        }
    }
}
