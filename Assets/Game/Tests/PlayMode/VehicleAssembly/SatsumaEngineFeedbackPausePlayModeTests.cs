using System.Collections;
using System.Linq;
using MSC.Audio;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    public sealed partial class SatsumaEngineFeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator PauseRunning_StopsOwnedAudio_ResumeRestartsOnlyContinuousBeds()
        {
            ApplyLiveTelemetry(VehicleEngineStatus.Cranking, 250f);
            yield return Frames(20);
            ApplyLiveTelemetry(VehicleEngineStatus.Running, 1800f);
            yield return Frames(2);
            AssertBedsActive(true, true);
            Assert.That(audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.EqualTo(1));
            Assert.That(audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.EqualTo(1));
            int beforePause = audio.Handles.Count;

            Time.timeScale = 0f;
            yield return Frames(3);
            Assert.That(audio.Handles.All(handle => !handle.IsPlaying), Is.True);
            Assert.That(audio.Handles.Count, Is.EqualTo(beforePause));
            Assert.That(simulation.State.EngineStatus, Is.EqualTo(VehicleEngineStatus.Running));
            Assert.That(simulation.Telemetry.EngineRpm, Is.EqualTo(1800f));
            Assert.That(audio.Emitters.Count, Is.EqualTo(5));

            Time.timeScale = 1f;
            yield return Frames(3);
            AssertBedsActive(true, true);
            Assert.That(audio.Handles.Count, Is.EqualTo(beforePause + 3));
            Assert.That(audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.EqualTo(1));
            Assert.That(audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.EqualTo(1));
            Assert.That(audio.Count(SatsumaEngineAudioIds.KeyInserted), Is.Zero);
            Assert.That(audio.Count(SatsumaEngineAudioIds.KeyRemoved), Is.Zero);
            AssertNoGlobalOrUnregisteredAudio();
        }

        [UnityTest]
        public IEnumerator PauseDuringStarterLeadIn_ResumesCrankLoopWithoutReplayingLeadIn()
        {
            ApplyLiveTelemetry(VehicleEngineStatus.Cranking, 250f);
            yield return Frames(2);
            Assert.That(audio.Active(SatsumaEngineAudioIds.StarterEngaged), Is.EqualTo(1));
            Assert.That(audio.Count(SatsumaEngineAudioIds.StarterLoop), Is.Zero);

            Time.timeScale = 0f;
            yield return Frames(3);
            Assert.That(audio.Handles.All(handle => !handle.IsPlaying), Is.True);
            Time.timeScale = 1f;
            yield return Frames(3);

            Assert.That(audio.Active(SatsumaEngineAudioIds.StarterLoop), Is.EqualTo(1));
            Assert.That(audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.EqualTo(1));
            Assert.That(audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.Zero);
            AssertBedsActive(false, false);
            AssertNoGlobalOrUnregisteredAudio();
        }

        [UnityTest]
        public IEnumerator RestoreWhilePaused_RemainsSilent_ResumeSeedsRestoredState()
        {
            ApplyLiveTelemetry(VehicleEngineStatus.Cranking, 250f);
            yield return Frames(2);
            Time.timeScale = 0f;
            yield return Frames(2);
            RestoreTelemetry(VehicleEngineStatus.Running, 2100f);
            yield return Frames(3);
            Assert.That(audio.Handles.All(handle => !handle.IsPlaying), Is.True);

            Time.timeScale = 1f;
            yield return Frames(3);
            AssertBedsActive(true, true);
            Assert.That(audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.Zero);
            Assert.That(audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.EqualTo(1));
            Assert.That(audio.Count(SatsumaEngineAudioIds.KeyInserted), Is.Zero);
            Assert.That(audio.Count(SatsumaEngineAudioIds.KeyRemoved), Is.Zero);
            AssertNoGlobalOrUnregisteredAudio();
        }
    }
}
