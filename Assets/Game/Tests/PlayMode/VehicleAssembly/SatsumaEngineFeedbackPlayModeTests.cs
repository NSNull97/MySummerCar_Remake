using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSC.Audio;
using MSC.LegacyImport;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    /// <summary>
    /// Authored presentation lifecycle, not a mechanical engine-start fixture.
    /// Telemetry is controlled through the simulation's public DTO boundary;
    /// the presenter is driven exclusively by real Unity LateUpdate frames.
    /// </summary>
    public sealed partial class SatsumaEngineFeedbackPlayModeTests
    {
        private Scene fixtureScene;
        private GameObject vehicle;
        private VehicleSimulationHost simulation;
        private SatsumaEngineFeedbackPresenter presenter;
        private PartInstance block;
        private EngineFeedbackLifecycleBackend audio;
        private float oldTimeScale;
        private float oldCaptureDeltaTime;

        [UnitySetUp]
        public IEnumerator CreateFixture()
        {
            oldTimeScale = Time.timeScale;
            oldCaptureDeltaTime = Time.captureDeltaTime;
            Time.timeScale = 1f;
            // Deterministic frame duration still goes through Unity's player
            // loop; there are no direct Tick or reflected lifecycle calls.
            Time.captureDeltaTime = .02f;
            GameObject prefab = Resources.Load<GameObject>("Phase1Vehicles/Satsuma_Phase1_V1a");
            Assert.That(prefab, Is.Not.Null, "The scoped Satsuma refresh must precede this fixture.");
            fixtureScene = SceneManager.CreateScene("engine feedback lifecycle " + System.Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            vehicle = Object.Instantiate(prefab);
            SceneManager.MoveGameObjectToScene(vehicle, fixtureScene);
            // Awake detaches this authored inventory scope. Keep both roots in
            // the isolated scene so teardown never leaves loose actors behind.
            GameObject looseRoot = vehicle.GetComponent<LegacySatsumaLoosePartsRoot>().LoosePartsRoot.gameObject;
            if (looseRoot.scene != fixtureScene) SceneManager.MoveGameObjectToScene(looseRoot, fixtureScene);
            var assembly = vehicle.GetComponent<VehicleAssemblyController>();
            assembly.Initialize();
            assembly.enabled = false;
            assembly.GetComponent<AssemblyLooseCompoundPhysics>().enabled = false;
            simulation = vehicle.GetComponent<VehicleSimulationHost>();
            Assert.That(simulation.IsInitialized, Is.True, "The real prefab Awake must initialize the host.");
            simulation.enabled = false;
            simulation.ResetSimulation();
            block = assembly.Parts.Single(part => part.Definition.DefinitionId == "vehicle.satsuma.part.engine-block");
            block.GetComponent<AssemblyEngineDockingState>().enabled = false;
            // Assembly/docking mechanics have separate tests. Suppress their
            // stepping and the local physics scene, not the audio lifecycle.
            block.RuntimeState.SetInstalled("mount.satsuma.engine-assembly", false);
            presenter = vehicle.GetComponent<SatsumaEngineFeedbackPresenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.ExhaustBinding, Is.Not.Null);
            Assert.That(presenter.ExhaustBinding.IsConfigured, Is.True);
            audio = vehicle.AddComponent<EngineFeedbackLifecycleBackend>();
            Assert.That(presenter.ConfigureBackend(audio), Is.True, presenter.LastFailure);
            yield return Frames(2);
            Assert.That(presenter.IsBound, Is.True);
            Assert.That(audio.Emitters.Select(value => value.StableId), Is.EquivalentTo(new[]
            {
                "audio.emitter.vehicle.satsuma.engine",
                "audio.emitter.vehicle.satsuma.ignition",
                "audio.emitter.vehicle.satsuma.exhaust",
                "audio.emitter.vehicle.satsuma.intake",
                "audio.emitter.vehicle.satsuma.radiator",
            }));
            Assert.That(audio.RegistrationCalls, Is.EqualTo(5));
            Assert.That(audio.Handles, Is.Empty, "Awake/binding an off engine must not synthesize a start.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (fixtureScene.IsValid() && fixtureScene.isLoaded)
                yield return SceneManager.UnloadSceneAsync(fixtureScene);
            Time.captureDeltaTime = oldCaptureDeltaTime;
            Time.timeScale = oldTimeScale;
        }

        [UnityTest]
        public IEnumerator LateUpdateRoutesCrankCatchRunningAndBothRundownStatusesToAuthoredEmitters()
        {
            ApplyLiveTelemetry(VehicleEngineStatus.Cranking, 250f);
            Assert.That(audio.Handles, Is.Empty, "Changing native state must wait for the presentation frame.");
            yield return Frames(2);
            Assert.That(audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.EqualTo(1));
            Assert.That(audio.Count(SatsumaEngineAudioIds.StarterLoop), Is.Zero);
            Assert.That(audio.Count(SatsumaEngineAudioIds.ExhaustLoop), Is.Zero);
            yield return Frames(20); // More than the donor .316-second starter lead-in.
            Assert.That(audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.EqualTo(1));
            Assert.That(audio.Count(SatsumaEngineAudioIds.StarterLoop), Is.EqualTo(1));
            Assert.That(audio.Active(SatsumaEngineAudioIds.StarterLoop), Is.EqualTo(1));

            ApplyLiveTelemetry(VehicleEngineStatus.Running, 3000f, .65f, .4f);
            yield return Frames(2);
            Assert.That(audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.EqualTo(1));
            Assert.That(audio.Active(SatsumaEngineAudioIds.StarterEngaged), Is.Zero);
            Assert.That(audio.Active(SatsumaEngineAudioIds.StarterLoop), Is.Zero);
            AssertBedsActive(true, true);
            Assert.That(audio.Parameter(AudioProjectIds.Parameters.VehicleRpm, presenter.EngineEmitter), Is.EqualTo(3000f));
            Assert.That(audio.Parameter(AudioProjectIds.Parameters.VehicleThrottle, presenter.EngineEmitter), Is.EqualTo(.65f).Within(.0001f));
            Assert.That(audio.Parameter(AudioProjectIds.Parameters.VehicleEngineLoad, presenter.EngineEmitter), Is.EqualTo(.4f).Within(.0001f));
            Assert.That(audio.Handles.Where(handle => handle.EventId != SatsumaEngineAudioIds.ExhaustLoop)
                .All(handle => ReferenceEquals(handle.Emitter, presenter.EngineEmitter)), Is.True);
            Assert.That(audio.Handles.Single(handle => handle.EventId == SatsumaEngineAudioIds.ExhaustLoop).Emitter,
                Is.SameAs(presenter.ExhaustBinding.Emitter));
            int runningPosts = audio.Handles.Count;
            yield return Frames(5);
            Assert.That(audio.Handles.Count, Is.EqualTo(runningPosts), "Healthy loops must not repost every frame.");

            foreach (VehicleEngineStatus rundown in new[] { VehicleEngineStatus.Off, VehicleEngineStatus.Stalled })
            {
                // Each separate rundown needs a preceding real running state.
                // Reintroducing RPM after reaching zero is not proof of combustion.
                ApplyLiveTelemetry(VehicleEngineStatus.Running, 1200f);
                yield return Frames(2);
                ApplyLiveTelemetry(rundown, 800f);
                yield return Frames(2);
                AssertBedsActive(true, true);
                Assert.That(audio.Active(SatsumaEngineAudioIds.EngineCaught), Is.Zero);
                ApplyLiveTelemetry(rundown, 110f);
                yield return Frames(2);
                AssertBedsActive(true, false);
                ApplyLiveTelemetry(rundown, 0f);
                yield return Frames(2);
                AssertBedsActive(false, false);
            }
            Assert.That(audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.EqualTo(1), "Rundown must never fabricate a catch.");
            AssertNoGlobalOrUnregisteredAudio();
        }

        [UnityTest]
        public IEnumerator FailedCrankAndRestoredFailedRundown_KeepBothEngineAndExhaustSilent()
        {
            ApplyLiveTelemetry(VehicleEngineStatus.Cranking, 650f);
            yield return Frames(40);
            Assert.That(audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.EqualTo(1));
            Assert.That(audio.Active(SatsumaEngineAudioIds.StarterLoop), Is.EqualTo(1));
            AssertBedsActive(false, false);
            ApplyLiveTelemetry(VehicleEngineStatus.Off, 600f);
            yield return Frames(2);
            AssertBedsActive(false, false);
            var failed = TelemetryDto(VehicleEngineStatus.Stalled, 500f, 0f, 0f);
            failed.combustionRundownActive = false;
            Assert.That(simulation.TryRestoreSimulationState(failed, out _), Is.True);
            yield return Frames(2);
            AssertBedsActive(false, false);
            Assert.That(audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.Zero);
            Assert.That(audio.Count(SatsumaEngineAudioIds.ExhaustLoop), Is.Zero);
        }

        [UnityTest]
        public IEnumerator NativeRestoreSeedsRunningAndCrankingQuietlyWithoutKeyOrCatchReplay()
        {
            ApplyLiveTelemetry(VehicleEngineStatus.Cranking, 250f);
            yield return Frames(2);
            Assert.That(audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.EqualTo(1));
            RestoreTelemetry(VehicleEngineStatus.Running, 1800f);
            Assert.That(audio.Handles.All(handle => !handle.IsPlaying), Is.True,
                "The real SimulationRestored event must stop old transient handles immediately.");
            yield return Frames(2);
            AssertBedsActive(true, true);
            Assert.That(audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.Zero,
                "Restoring Running after Cranking is not an audible start transition.");

            RestoreTelemetry(VehicleEngineStatus.Cranking, 250f);
            yield return Frames(2);
            Assert.That(audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.EqualTo(1));
            Assert.That(audio.Active(SatsumaEngineAudioIds.StarterLoop), Is.EqualTo(1),
                "A restored crank resumes its loop without repeating the lead-in.");
            AssertBedsActive(false, false);
            RestoreTelemetry(VehicleEngineStatus.Off, 600f);
            yield return Frames(2);
            AssertBedsActive(true, true);
            RestoreTelemetry(VehicleEngineStatus.Off, 0f);
            yield return Frames(2);
            AssertBedsActive(false, false);
            Assert.That(audio.Count(SatsumaEngineAudioIds.KeyInserted), Is.Zero);
            Assert.That(audio.Count(SatsumaEngineAudioIds.KeyRemoved), Is.Zero);
            Assert.That(audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.Zero);
            Assert.That(audio.Emitters, Has.Count.EqualTo(5));
            Assert.That(audio.RegistrationCalls, Is.EqualTo(5), "State restoration must not multiply emitter registrations.");
            AssertNoGlobalOrUnregisteredAudio();
        }

        [UnityTest]
        public IEnumerator DisableReenableAndDetachedEngineStopOnlyOwnedHandlesWithoutReplayingStart()
        {
            ApplyLiveTelemetry(VehicleEngineStatus.Running, 2400f);
            yield return Frames(2);
            AssertBedsActive(true, true);
            presenter.enabled = false;
            Assert.That(presenter.IsBound, Is.False);
            Assert.That(audio.Emitters, Is.Empty);
            Assert.That(audio.UnregistrationCalls, Is.EqualTo(5));
            Assert.That(audio.Handles.All(handle => !handle.IsPlaying), Is.True);
            int beforeDisableFrames = audio.Handles.Count;
            yield return Frames(3);
            Assert.That(audio.Handles.Count, Is.EqualTo(beforeDisableFrames));

            presenter.enabled = true;
            Assert.That(presenter.IsBound, Is.True, presenter.LastFailure);
            Assert.That(audio.Emitters, Has.Count.EqualTo(5));
            Assert.That(audio.RegistrationCalls, Is.EqualTo(10));
            yield return Frames(2);
            AssertBedsActive(true, true);
            Assert.That(audio.Handles.Count, Is.EqualTo(beforeDisableFrames + 3));
            Assert.That(audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.Zero);
            Assert.That(audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.Zero);

            block.RuntimeState.SetLoose(block.transform.position, block.transform.rotation);
            yield return Frames(2);
            Assert.That(simulation.Telemetry.EngineRpm, Is.EqualTo(2400f), "Deliberately retain stale telemetry to exercise the installed gate.");
            AssertBedsActive(false, false);
            Assert.That(audio.Parameter(SatsumaEngineAudioIds.ExhaustGain, presenter.ExhaustBinding.Emitter), Is.Zero);
            block.RuntimeState.SetInstalled("mount.satsuma.engine-assembly", false);
            yield return Frames(2);
            AssertBedsActive(true, true);
            Assert.That(audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.Zero);
            AssertNoGlobalOrUnregisteredAudio();
        }

        private void ApplyLiveTelemetry(VehicleEngineStatus status, float rpm, float throttle = 0f, float load = 0f)
        {
            VehicleSimulationStateDto data = TelemetryDto(status, rpm, throttle, load);
            // Deliberately bypass only the host's restore notification: this
            // models changed simulation telemetry, not a loaded save.
            Assert.That(simulation.Root.TryRestoreState(data, out string failure), Is.True, failure);
        }

        private void RestoreTelemetry(VehicleEngineStatus status, float rpm)
        {
            Assert.That(simulation.TryRestoreSimulationState(TelemetryDto(status, rpm, 0f, 0f), out string failure),
                Is.True, failure);
        }

        private VehicleSimulationStateDto TelemetryDto(VehicleEngineStatus status, float rpm, float throttle, float load)
        {
            VehicleSimulationStateDto data = simulation.State.CaptureDto();
            data.engineStatus = status;
            data.engineRpm = rpm;
            data.filteredThrottle01 = throttle;
            data.engineLoad01 = load;
            return data;
        }

        private void AssertBedsActive(bool engine, bool exhaust)
        {
            Assert.That(audio.Active(SatsumaEngineAudioIds.EngineThrottleLoop), Is.EqualTo(engine ? 1 : 0));
            Assert.That(audio.Active(SatsumaEngineAudioIds.EngineCoastLoop), Is.EqualTo(engine ? 1 : 0));
            Assert.That(audio.Active(SatsumaEngineAudioIds.ExhaustLoop), Is.EqualTo(exhaust ? 1 : 0));
        }

        private void AssertNoGlobalOrUnregisteredAudio()
        {
            Assert.That(audio.StopAllCalls, Is.Zero, "A vehicle presenter cannot stop unrelated world audio.");
            Assert.That(audio.UnregisteredPostAttempts, Is.Zero);
            Assert.That(audio.DuplicateRegistrationAttempts, Is.Zero);
            Assert.That(simulation.FixedTickCount, Is.Zero, "Only presentation frames run in this fixture.");
        }

        private static IEnumerator Frames(int count)
        {
            for (int index = 0; index < count; index++) yield return null;
        }
    }

    public sealed class EngineFeedbackLifecycleBackend : MonoBehaviour, IAudioBackend
    {
        public readonly HashSet<IAudioEmitter> Emitters = new();
        public readonly List<RecordingHandle> Handles = new();
        private readonly Dictionary<string, float> parameters = new();
        public int RegistrationCalls, UnregistrationCalls, DuplicateRegistrationAttempts, UnregisteredPostAttempts, StopAllCalls;
        public string BackendId => "audio.backend.engine_feedback_lifecycle_test";
        public AudioBackendKind Kind => AudioBackendKind.Unity;
        public bool IsReady => true;
        public string FailureReason => string.Empty;
        public bool RegisterEmitter(IAudioEmitter emitter, out string failure)
        {
            RegistrationCalls++;
            bool added = Emitters.Add(emitter);
            if (!added) DuplicateRegistrationAttempts++;
            failure = added ? string.Empty : "Duplicate emitter registration.";
            return added;
        }
        public bool UnregisterEmitter(IAudioEmitter emitter)
        {
            UnregistrationCalls++;
            return Emitters.Remove(emitter);
        }
        public IAudioEventHandle PostEvent(in AudioEventRequest request)
        {
            if (request.Emitter == null || !Emitters.Contains(request.Emitter))
            {
                UnregisteredPostAttempts++;
                return AudioEventHandles.Invalid;
            }
            var handle = new RecordingHandle(request.EventId, request.Emitter, (ulong)Handles.Count + 1);
            Handles.Add(handle);
            return handle;
        }
        public int Count(AudioEventId id) => Handles.Count(handle => handle.EventId == id);
        public int Active(AudioEventId id) => Handles.Count(handle => handle.EventId == id && handle.IsPlaying);
        public float Parameter(AudioParameterId id, IAudioEmitter emitter) => parameters[id.Value + "/" + emitter.StableId];
        public bool SetParameter(AudioParameterId id, float value, IAudioEmitter emitter = null)
        {
            parameters[id.Value + "/" + (emitter?.StableId ?? string.Empty)] = value;
            return true;
        }
        public bool SetSwitch(AudioSwitchId group, AudioSwitchId value, IAudioEmitter emitter = null) => true;
        public bool SetState(AudioStateId group, AudioStateId value) => true;
        public void SetListenerContext(in AudioListenerContext context) { }
        public void ApplySettings(in AudioSettingsState settings) { }
        public void StopAll(float fadeSeconds = 0f) => StopAllCalls++;
        public AudioRuntimeSnapshot CaptureSnapshot() => default;

        public sealed class RecordingHandle : IAudioEventHandle
        {
            public RecordingHandle(AudioEventId id, IAudioEmitter emitter, ulong handleId)
            { EventId = id; Emitter = emitter; HandleId = handleId; }
            public ulong HandleId { get; }
            public AudioEventId EventId { get; }
            public IAudioEmitter Emitter { get; }
            public bool IsValid => IsPlaying;
            public bool IsPlaying { get; private set; } = true;
            public void Stop(float fadeSeconds = 0f) => IsPlaying = false;
            public void Dispose() => IsPlaying = false;
        }
    }
}
