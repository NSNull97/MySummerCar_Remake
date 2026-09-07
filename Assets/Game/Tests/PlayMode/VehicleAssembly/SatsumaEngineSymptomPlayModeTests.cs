using System.Collections;
using System.Linq;
using MSC.Audio;
using MSC.Audio.UnityFallback;
using MSC.Vehicle;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    public sealed partial class SatsumaEngineFeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator OperatingFaultsUseRealLateUpdateAndPauseRestoreDoNotReplayImpulses()
        {
            ApplyLiveTelemetry(VehicleEngineStatus.Running, 3000f);
            EvaluateFaultPoint();
            yield return Frames(70);
            Assert.That(audio.Active(SatsumaEngineAudioIds.BeltSqueal), Is.EqualTo(1));
            Assert.That(audio.Active(SatsumaEngineAudioIds.Pinging), Is.EqualTo(1));
            Assert.That(audio.Count(SatsumaEngineAudioIds.ValveTick), Is.GreaterThan(0));
            Assert.That(audio.Count(SatsumaEngineAudioIds.BearingKnock), Is.GreaterThan(0));
            Assert.That(audio.Handles.Where(h => h.EventId == SatsumaEngineAudioIds.IntakeSpit)
                .Any(h => h.Emitter == presenter.IntakeEmitter), Is.True);
            Assert.That(audio.Handles.Where(h => h.EventId == SatsumaEngineAudioIds.ExhaustBackfire)
                .Any(h => h.Emitter == presenter.ExhaustBinding.Emitter), Is.True);

            int beforePause = audio.Handles.Count;
            Time.timeScale = 0f;
            yield return Frames(4);
            Assert.That(audio.Handles.All(h => !h.IsPlaying), Is.True);
            Assert.That(audio.Handles.Count, Is.EqualTo(beforePause));
            RestoreTelemetry(VehicleEngineStatus.Running, 800f);
            Time.timeScale = 1f;
            yield return Frames(4);
            AssertBedsActive(true, true);
            Assert.That(audio.Active(SatsumaEngineAudioIds.BeltSqueal), Is.Zero,
                "Restore clears transient operating feedback until a fresh simulation step.");
            Assert.That(audio.Active(SatsumaEngineAudioIds.Pinging), Is.Zero);
            Assert.That(audio.Handles.Count, Is.EqualTo(beforePause + 3));
            AssertNoGlobalOrUnregisteredAudio();
        }

        [UnityTest]
        public IEnumerator RealImportedUnityVoicesFollowEngineAndFanAndRespectPauseAndPitchExpiry()
        {
            var library = Resources.Load<UnityAudioEventLibrary>(SatsumaEngineFeedbackPresenter.FallbackResourcesPath);
            Assert.That(library, Is.Not.Null);
            Assert.That(library.Validate(out var failures), Is.True, string.Join("; ", failures));
            var backendObject = new GameObject("Real imported symptom backend");
            backendObject.transform.SetParent(vehicle.transform, false);
            var fallback = backendObject.AddComponent<UnityAudioBackend>();
            fallback.ConfigureForAuthoring(library);
            if (!Object.FindObjectsByType<AudioListener>().Any(listener => listener.isActiveAndEnabled))
            {
                var listener = new GameObject("Symptom fixture listener");
                listener.transform.SetParent(vehicle.transform, false);
                listener.AddComponent<AudioListener>();
            }
            Assert.That(presenter.ConfigureBackend(fallback), Is.True, presenter.LastFailure);
            ApplyLiveTelemetry(VehicleEngineStatus.Running, 3000f);
            EvaluateFaultPoint();
            yield return Frames(4);
            Assert.That(library.TryResolve(SatsumaEngineAudioIds.BeltSqueal, out var belt), Is.True);
            AudioSource voice = backendObject.GetComponentsInChildren<AudioSource>()
                .Single(source => source.clip == belt.Clip && source.isPlaying);
            Assert.That(voice.rolloffMode, Is.EqualTo(AudioRolloffMode.Linear));
            Assert.That(voice.pitch, Is.EqualTo(1.1f).Within(.001f));
            Assert.That(Vector3.Distance(voice.transform.position, presenter.EngineEmitter.AudioTransform.position), Is.LessThan(.002f));
            block.transform.position += new Vector3(3f, .5f, -2f);
            yield return Frames(3);
            Assert.That(Vector3.Distance(voice.transform.position, block.transform.position), Is.LessThan(.002f));

            Time.timeScale = 0f;
            yield return Frames(3);
            Assert.That(fallback.ActiveGenericVoiceCount, Is.Zero);
            var hotOff = TelemetryDto(VehicleEngineStatus.Off, 0f, 0f, 0f);
            hotOff.satsumaOperatingState.radiatorFanRunning = true;
            Assert.That(simulation.TryRestoreSimulationState(hotOff, out string failure), Is.True, failure);
            Time.timeScale = 1f;
            yield return Frames(4);
            Assert.That(library.TryResolve(SatsumaEngineAudioIds.FanLoop, out var fan), Is.True);
            AudioSource fanVoice = backendObject.GetComponentsInChildren<AudioSource>()
                .Single(source => source.clip == fan.Clip && source.isPlaying);
            Assert.That(fallback.ActiveGenericVoiceCount, Is.EqualTo(1));
            Assert.That(fanVoice.rolloffMode, Is.EqualTo(AudioRolloffMode.Logarithmic));
            Assert.That(Vector3.Distance(fanVoice.transform.position, presenter.RadiatorEmitter.AudioTransform.position), Is.LessThan(.002f));
            presenter.enabled = false;
            yield return Frames(2);
            Assert.That(fallback.ActiveGenericVoiceCount, Is.Zero);
        }

        private void EvaluateFaultPoint()
        {
            // A controlled telemetry fixture, not a fake claim that the car is
            // physically assembled. Full source/assembly starts have EditMode coverage.
            var tuning = new SatsumaTuningInputs(10f, 0f, 20f, -5f, 2f, Vector4.one, Vector4.one);
            var ancillary = new SatsumaAncillaryInputs(true, true, true, true, true, true, true, true);
            var conditions = new SatsumaOperatingInputs(tuning, ancillary, default, 15, 5f, 20f);
            var input = new VehicleInputState();
            simulation.Root.SatsumaOperatingModel.Evaluate(simulation.State, input, conditions, .02f);
        }
    }
}
