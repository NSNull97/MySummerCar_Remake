using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Audio;
using MSC.LegacyImport.Editor.GameplayPresentation;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.VehicleAssembly
{
    public sealed class SatsumaEngineFeedbackTests
    {
        [TestCase(800f, 0f, .012f, .1f, .625f, .57f)]
        [TestCase(4000f, 1f, .36f, 0f, 1.125f, .85f)]
        [TestCase(8000f, .5f, .42f, .5f, 1.75f, 1.2f)]
        public void HealthyLoopsFollowFrozenSoundControllerValues(float rpm, float throttle,
            float drive, float coast, float drivePitch, float coastPitch)
        {
            var mix = SatsumaEngineFeedbackRules.Evaluate(VehicleEngineStatus.Running, rpm, throttle);
            Assert.That(mix.ThrottleGain, Is.EqualTo(drive).Within(.00001f));
            Assert.That(mix.CoastGain, Is.EqualTo(coast).Within(.00001f));
            Assert.That(mix.ThrottlePitch, Is.EqualTo(drivePitch).Within(.00001f));
            Assert.That(mix.CoastPitch, Is.EqualTo(coastPitch).Within(.00001f));
        }

        [Test]
        public void CatchManifestUsesReviewedStartEngineGain()
        {
            var manifest = JsonUtility.FromJson<AudioManifestForTest>(
                System.IO.File.ReadAllText(Phase1SatsumaEngineAudioImporter.ManifestPath));
            Assert.That(manifest.clips.Single(clip => clip.eventId == SatsumaEngineAudioIds.EngineCaught.Value).volume,
                Is.EqualTo(.45f), "Frozen Starter106807.Start engine / Starting/start3 volume is .45, not 1.");
        }

        [Serializable] private sealed class AudioManifestForTest
        { public AudioClipForTest[] clips = Array.Empty<AudioClipForTest>(); }
        [Serializable] private sealed class AudioClipForTest
        {
            public string eventId = string.Empty, sourceSha256 = string.Empty;
            public string volumeParameterId = string.Empty, pitchParameterId = string.Empty;
            public float volume = 0f, minimumDistanceMeters = 0f, maximumDistanceMeters = 0f;
        }

        [Test]
        public void StoppedAndInvalidTelemetryCannotProduceEngineGainOrVibration()
        {
            foreach (VehicleEngineStatus status in new[] { VehicleEngineStatus.Off, VehicleEngineStatus.Stalled, VehicleEngineStatus.Cranking })
            {
                var mix = SatsumaEngineFeedbackRules.Evaluate(status, 0, 1);
                Assert.That(mix.Running, Is.False);
                Assert.That(mix.ThrottleGain + mix.CoastGain, Is.Zero);
            }
            var invalid = SatsumaEngineFeedbackRules.Evaluate(VehicleEngineStatus.Running, float.NaN, float.NaN);
            Assert.That(float.IsFinite(invalid.ThrottlePitch), Is.True);
            Assert.That(invalid.ThrottleGain + invalid.CoastGain, Is.Zero);
            Assert.That(SatsumaEngineFeedbackRules.VibrationOffset(1, float.NaN, 1, true), Is.EqualTo(Vector3.zero));
            for (int i = 0; i < 360; i++)
                Assert.That(SatsumaEngineFeedbackRules.VibrationOffset(i, 800, 1, true).magnitude, Is.LessThan(.0012f));
        }

        [TestCase(VehicleEngineStatus.Off)]
        [TestCase(VehicleEngineStatus.Stalled)]
        public void IgnitionOffOrStallPreservesActualRpmRundown(VehicleEngineStatus status)
        {
            var mix = SatsumaEngineFeedbackRules.Evaluate(status, 800, 0);
            Assert.That(mix.Running, Is.False);
            Assert.That(mix.Cranking, Is.False);
            Assert.That(mix.EngineLoopsActive, Is.True);
            Assert.That(mix.CoastGain, Is.EqualTo(.1f).Within(.00001f));
            Assert.That(mix.ThrottleGain, Is.EqualTo(.012f).Within(.00001f));
            using var f = new Fixture();
            f.Presenter.Tick(VehicleEngineStatus.Running, 1200, 0, 0, .02f);
            f.Presenter.Tick(status, 800, 0, 0, .02f);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.EngineCoastLoop), Is.EqualTo(1));
            Assert.That(f.Audio.Handles.Where(handle => handle.EventId == SatsumaEngineAudioIds.EngineCoastLoop)
                .All(handle => handle.IsPlaying), Is.True);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.Zero);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.Zero);
            f.Presenter.Tick(status, 0, 0, 0, .02f);
            Assert.That(f.Audio.Handles.All(handle => !handle.IsPlaying), Is.True);
        }

        [TestCase(VehicleEngineStatus.Off)]
        [TestCase(VehicleEngineStatus.Stalled)]
        public void RestoredRundownNeverReplaysStartAndDetachedBlockIsSilent(VehicleEngineStatus status)
        {
            using var f = new Fixture();
            var state = f.Host.State.CaptureDto();
            state.engineStatus = status; state.engineRpm = 800;
            state.combustionRundownActive = true;
            Assert.That(f.Host.TryRestoreSimulationState(state, out string failure), Is.True, failure);
            f.Presenter.Tick(status, 800, 0, 0, .02f);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.EngineCoastLoop), Is.EqualTo(1));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.Zero);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.Zero);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.KeyInserted), Is.Zero);
            f.Block.RuntimeState.SetLoose(f.Block.transform.position, f.Block.transform.rotation);
            f.Presenter.Tick(status, 800, 0, 0, .02f);
            Assert.That(f.Audio.Handles.All(handle => !handle.IsPlaying), Is.True);
            Assert.That(f.Audio.Parameters[SatsumaEngineAudioIds.CoastGain.Value], Is.Zero);
        }

        [TestCase(VehicleEngineStatus.Off)]
        [TestCase(VehicleEngineStatus.Stalled)]
        public void FailedAttemptAndItsRestoredRundown_NeverPlayCombustionBeds(VehicleEngineStatus stopped)
        {
            using var f = new Fixture();
            for (int i = 0; i < 250; i++)
                f.Presenter.Tick(VehicleEngineStatus.Cranking, 650, 0, 0, .02f);
            f.Presenter.Tick(stopped, 600, 0, 0, .02f);
            var dto = f.Host.State.CaptureDto();
            dto.engineStatus = stopped;
            dto.engineRpm = 500;
            dto.combustionRundownActive = false;
            Assert.That(f.Host.TryRestoreSimulationState(dto, out _), Is.True);
            f.Presenter.Tick(stopped, 500, 0, 0, .02f);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.EqualTo(1));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.Zero);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.EngineThrottleLoop), Is.Zero);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.EngineCoastLoop), Is.Zero);
            Assert.That(SatsumaExhaustFeedbackRules.IsAudible(stopped, 600, true, false), Is.False);
            Assert.That(f.Audio.Handles.All(handle => !handle.IsPlaying), Is.True);
        }

        [Test]
        public void CrankCatchRunStopOwnsOneLoopEachAndNeverInventsAStopEvent()
        {
            using var f = new Fixture();
            f.Presenter.Tick(VehicleEngineStatus.Cranking, 300, 0, 0, .1f);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.EqualTo(1));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.StarterLoop), Is.Zero);
            f.Presenter.Tick(VehicleEngineStatus.Cranking, 300, 0, 0, .22f);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.StarterLoop), Is.EqualTo(1));
            f.Presenter.Tick(VehicleEngineStatus.Running, 900, .1f, .2f, .02f);
            for (int i = 0; i < 100; i++) f.Presenter.Tick(VehicleEngineStatus.Running, 4000, 1, 1, .02f);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.EqualTo(1));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.EngineThrottleLoop), Is.EqualTo(1));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.EngineCoastLoop), Is.EqualTo(1));
            Assert.That(f.Audio.Parameters[SatsumaEngineAudioIds.ThrottleGain.Value], Is.EqualTo(.36f).Within(.0001f));
            int beforeStop = f.Audio.Handles.Count;
            f.Presenter.Tick(VehicleEngineStatus.Stalled, 0, 0, 0, .02f);
            Assert.That(f.Audio.Handles.All(handle => !handle.IsPlaying), Is.True);
            Assert.That(f.Audio.Handles.Count, Is.EqualTo(beforeStop));
            Assert.That(f.Audio.StopAllCalls, Is.Zero, "Do not silence unrelated world audio.");
        }

        [Test]
        public void RestoreRunningSeedsLoopsWithoutCatchAndDetachStopsThem()
        {
            using var f = new Fixture();
            var state = f.Host.State.CaptureDto();
            state.engineStatus = VehicleEngineStatus.Running; state.engineRpm = 900;
            Assert.That(f.Host.TryRestoreSimulationState(state, out string failure), Is.True, failure);
            f.Presenter.Tick(VehicleEngineStatus.Running, 900, 0, 0, .02f);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.Zero);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.Zero);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.KeyInserted), Is.Zero);
            f.Block.RuntimeState.SetLoose(f.Block.transform.position, f.Block.transform.rotation);
            f.Presenter.Tick(VehicleEngineStatus.Running, 900, 0, 0, .02f);
            Assert.That(f.Audio.Handles.All(handle => !handle.IsPlaying), Is.True);
            Assert.That(f.Presenter.ConfigureBackend(f.Audio), Is.True);
            Assert.That(f.Audio.Emitters.Count, Is.EqualTo(2));
        }

        [Test]
        public void MissingMediaRetriesBoundedlyAndDisabledPresenterDoesNotPost()
        {
            using var f = new Fixture();
            f.Audio.ReturnInvalid = true;
            for (int i = 0; i < 50; i++) f.Presenter.Tick(VehicleEngineStatus.Running, 900, 0, 0, .01f);
            Assert.That(f.Audio.PostAttempts, Is.EqualTo(2));
            f.Presenter.enabled = false;
            f.Presenter.Tick(VehicleEngineStatus.Cranking, 300, 0, 0, 2f);
            Assert.That(f.Audio.PostAttempts, Is.EqualTo(2));
        }

        [Test]
        public void RendererVibrationNeverMovesPhysicalRootAndRestoresOnStop()
        {
            using var f = new Fixture();
            var go = new GameObject("Test collider-free engine mesh");
            go.transform.SetParent(f.Block.transform, false);
            go.AddComponent<MeshRenderer>();
            var vibration = f.Root.GetComponent<SatsumaEngineVisualVibration>() ?? f.Root.AddComponent<SatsumaEngineVisualVibration>();
            vibration.Configure(f.Host, f.Block, new[] { go.transform }, new[] { f.Block });
            Vector3 rootPosition = f.Block.transform.position;
            Quaternion rootRotation = f.Block.transform.rotation;
            Vector3 local = go.transform.localPosition;
            vibration.ApplyFrame(VehicleEngineStatus.Running, 850, 1, .017f);
            Assert.That(Vector3.Distance(go.transform.localPosition, local), Is.InRange(.000001f, .0012f));
            Assert.That(f.Block.transform.position, Is.EqualTo(rootPosition));
            Assert.That(f.Block.transform.rotation, Is.EqualTo(rootRotation));
            vibration.ApplyFrame(VehicleEngineStatus.Off, 0, 0, .02f);
            Assert.That(go.transform.localPosition, Is.EqualTo(local));
            go.AddComponent<BoxCollider>();
            Assert.That(SatsumaEngineVisualVibration.IsSafeVisualLeaf(go.transform), Is.False);
            Assert.Throws<ArgumentException>(() => vibration.Configure(f.Host, f.Block, new[] { go.transform }, new[] { f.Block }));
        }

        [Test]
        public void FeedbackAuthoringIsIdempotentAndTargetsOnlySafeOwnedEngineLeaves()
        {
            GameObject root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Phase1SatsumaBaselineBuilder.RuntimePrefabPath));
            try
            {
                var assembly = root.GetComponent<VehicleAssemblyController>();
                Phase1SatsumaEngineFeedbackAuthoring.ApplyToInstance(assembly);
                Assert.That(Phase1SatsumaEngineFeedbackAuthoring.ApplyToInstance(assembly), Is.Zero);
                var audio = root.GetComponent<SatsumaEngineFeedbackPresenter>();
                Assert.That(audio.EngineEmitter.AudioTransform, Is.SameAs(audio.EnginePart.transform));
                Assert.That(audio.KeyEmitter.AudioTransform, Is.SameAs(audio.Ignition.KeyPivot));
                Assert.That(audio.EngineEmitter.StableId, Is.EqualTo(Phase1SatsumaEngineFeedbackAuthoring.EngineEmitterId));
                var vibration = root.GetComponent<SatsumaEngineVisualVibration>();
                Assert.That(vibration.VisualLeaves.Length, Is.GreaterThan(10));
                Assert.That(vibration.VisualLeaves.All(SatsumaEngineVisualVibration.IsSafeVisualLeaf), Is.True);
                Assert.That(vibration.Owners.All(part => part != null && !part.IsAssemblyRoot), Is.True);
                Phase1SatsumaEngineAudioImporter.EnsureGeneratedForBuild();
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(false, false, false, SatsumaExhaustOutlet.Engine)]
        [TestCase(false, true, false, SatsumaExhaustOutlet.Engine)]
        [TestCase(false, false, true, SatsumaExhaustOutlet.Engine)]
        [TestCase(false, true, true, SatsumaExhaustOutlet.Engine)]
        [TestCase(true, false, false, SatsumaExhaustOutlet.Headers)]
        [TestCase(true, false, true, SatsumaExhaustOutlet.Headers)]
        [TestCase(true, true, false, SatsumaExhaustOutlet.Pipe)]
        [TestCase(true, true, true, SatsumaExhaustOutlet.Muffler)]
        public void StockExhaustRequiresAContiguousInstalledChain(bool headers, bool pipe, bool muffler,
            SatsumaExhaustOutlet expected)
        {
            Assert.That(SatsumaExhaustFeedbackRules.SelectStock(headers, pipe, muffler), Is.EqualTo(expected));
        }

        [TestCase(SatsumaExhaustOutlet.Engine, 1.5f, 1f, 1f)]
        [TestCase(SatsumaExhaustOutlet.Headers, 1.5f, 1f, 1f)]
        [TestCase(SatsumaExhaustOutlet.Pipe, .9f, .7f, .8f)]
        [TestCase(SatsumaExhaustOutlet.Muffler, .5f, .5f, .2f)]
        public void ExhaustModeUpdatesBothEngineBedsAndIndependentOutlet(SatsumaExhaustOutlet outlet,
            float throttleVolume, float coastVolume, float outletVolume)
        {
            var mix = SatsumaEngineFeedbackRules.Evaluate(VehicleEngineStatus.Running, 4000, .5f, outlet);
            Assert.That(mix.ThrottleGain, Is.EqualTo(.7f * throttleVolume * .5f).Within(.00001f));
            Assert.That(mix.CoastGain, Is.EqualTo(.5f * coastVolume * .5f).Within(.00001f));
            Assert.That(SatsumaExhaustFeedbackRules.OutletVolume(outlet), Is.EqualTo(outletVolume));
            Assert.That(SatsumaExhaustFeedbackRules.Pitch(4000), Is.EqualTo(.95f).Within(.00001f));
        }

        [TestCase(0, SatsumaExhaustOutlet.Engine)]
        [TestCase(1, SatsumaExhaustOutlet.Headers)]
        [TestCase(2, SatsumaExhaustOutlet.Pipe)]
        public void RemovingEachStockLinkMovesOneExistingEmitterWithoutRestartingLoop(int removed,
            SatsumaExhaustOutlet expected)
        {
            using var f = new Fixture(withExhaust: true);
            var binding = f.Presenter.ExhaustBinding;
            PartInstance[] chain = { binding.Headers, binding.Pipe, binding.Muffler };
            foreach (PartInstance part in chain) SetExhaustPartInstalled(part, true);
            // No fasteners are tightened here: donor outlet selection is Installed-only.
            f.Presenter.Tick(VehicleEngineStatus.Running, 800, 0, 0, .02f);
            Assert.That(binding.CurrentOutlet, Is.EqualTo(SatsumaExhaustOutlet.Muffler));
            Assert.That(f.Audio.Parameters[SatsumaEngineAudioIds.ExhaustGain.Value], Is.EqualTo(.2f));
            SetExhaustPartInstalled(chain[removed], false);
            f.Presenter.Tick(VehicleEngineStatus.Running, 800, 0, 0, .02f);
            Assert.That(binding.CurrentOutlet, Is.EqualTo(expected));
            Assert.That(binding.Emitter.AudioTransform.localPosition,
                Is.EqualTo(Phase1SatsumaEngineFeedbackAuthoring.ReviewedExhaustOutletPositions[(int)expected]));
            Assert.That(binding.Emitter.AudioTransform.parent, Is.SameAs(f.Root.transform));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.ExhaustLoop), Is.EqualTo(1));
            SetExhaustPartInstalled(chain[removed], true);
            f.Presenter.Tick(VehicleEngineStatus.Running, 800, 0, 0, .02f);
            Assert.That(binding.CurrentOutlet, Is.EqualTo(SatsumaExhaustOutlet.Muffler));
            Assert.That(f.Audio.Emitters.Count, Is.EqualTo(3));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.ExhaustLoop), Is.EqualTo(1));
        }

        [Test]
        public void PartialNewGameAndRestoredEngineAreQuietUntilActualRunningOrRundown()
        {
            using var f = new Fixture(withExhaust: true);
            var binding = f.Presenter.ExhaustBinding;
            SetExhaustPartInstalled(binding.Headers, false);
            SetExhaustPartInstalled(binding.Pipe, false);
            SetExhaustPartInstalled(binding.Muffler, false);
            f.Presenter.Tick(VehicleEngineStatus.Off, 0, 0, 0, .02f);
            Assert.That(f.Audio.Handles.Count, Is.Zero);
            f.Presenter.Tick(VehicleEngineStatus.Cranking, 300, 0, 0, .02f);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.ExhaustLoop), Is.Zero,
                "Failed cranking must not sound like a combusting engine.");
            var state = f.Host.State.CaptureDto();
            state.engineStatus = VehicleEngineStatus.Running; state.engineRpm = 800;
            Assert.That(f.Host.TryRestoreSimulationState(state, out string failure), Is.True, failure);
            int leadBefore = f.Audio.Count(SatsumaEngineAudioIds.StarterEngaged);
            f.Presenter.Tick(VehicleEngineStatus.Running, 800, 0, 0, .02f);
            Assert.That(binding.CurrentOutlet, Is.EqualTo(SatsumaExhaustOutlet.Engine));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.ExhaustLoop), Is.EqualTo(1));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.EngineCaught), Is.Zero);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.StarterEngaged), Is.EqualTo(leadBefore));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.KeyInserted), Is.Zero);
            f.Presenter.Tick(VehicleEngineStatus.Off, 111, 0, 0, .02f);
            Assert.That(f.Audio.Handles.Single(h => h.EventId == SatsumaEngineAudioIds.ExhaustLoop).IsPlaying, Is.True);
            f.Presenter.Tick(VehicleEngineStatus.Off, 110, 0, 0, .02f);
            Assert.That(f.Audio.Handles.Single(h => h.EventId == SatsumaEngineAudioIds.ExhaustLoop).IsPlaying, Is.False);
            Assert.That(f.Audio.Handles.Single(h => h.EventId == SatsumaEngineAudioIds.EngineCoastLoop).IsPlaying, Is.True);
            f.Block.RuntimeState.SetLoose(f.Block.transform.position, f.Block.transform.rotation);
            f.Presenter.Tick(VehicleEngineStatus.Running, 800, 0, 0, .02f);
            Assert.That(f.Audio.Handles.All(h => !h.IsPlaying), Is.True);
            Assert.That(f.Audio.Parameters[SatsumaEngineAudioIds.ExhaustGain.Value], Is.Zero);
        }

        [Test]
        public void ExhaustMediaIsEighthHashLockedClipWithIndependentRtpcAndReviewedRange()
        {
            var manifest = JsonUtility.FromJson<AudioManifestForTest>(
                System.IO.File.ReadAllText(Phase1SatsumaEngineAudioImporter.ManifestPath));
            Assert.That(manifest.clips.Length, Is.EqualTo(17));
            var clip = manifest.clips.Single(value => value.eventId == SatsumaEngineAudioIds.ExhaustLoop.Value);
            Assert.That(clip.volume, Is.EqualTo(1));
            Assert.That(clip.sourceSha256, Is.EqualTo("2c5de6300a501f0787525db9799d8a4c90efdcce62ae3f28c3977b4056993fe6"));
            Assert.That(clip.volumeParameterId, Is.EqualTo(SatsumaEngineAudioIds.ExhaustGain.Value));
            Assert.That(clip.pitchParameterId, Is.EqualTo(SatsumaEngineAudioIds.ExhaustPitch.Value));
            Assert.That(clip.minimumDistanceMeters, Is.EqualTo(1));
            Assert.That(clip.maximumDistanceMeters, Is.EqualTo(500));
        }

        private static void SetExhaustPartInstalled(PartInstance part, bool installed)
        {
            if (installed) part.RuntimeState.SetInstalled("mount.test.exhaust-chain", false);
            else part.RuntimeState.SetLoose(part.transform.position, part.transform.rotation);
        }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly PartInstance Block;
            public readonly VehicleSimulationHost Host;
            public readonly SatsumaEngineFeedbackPresenter Presenter;
            public readonly EngineFeedbackRecordingBackend Audio;
            public Fixture(bool withExhaust = false)
            {
                Root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Phase1SatsumaBaselineBuilder.RuntimePrefabPath));
                var assembly = Root.GetComponent<VehicleAssemblyController>();
                if (withExhaust) Phase1SatsumaEngineFeedbackAuthoring.ApplyToInstance(assembly);
                assembly.Initialize();
                Host = Root.GetComponent<VehicleSimulationHost>();
                Assert.That(Host.TryInitialize(out string failure), Is.True, failure);
                Host.ResetSimulation();
                Block = assembly.Parts.Single(part => part.Definition.DefinitionId == Phase1SatsumaEngineDockingAuthoring.BlockPartId);
                // Presenter unit input: the assembly/docking flow is covered by
                // its own tests; do not repair or mutate a player save here.
                Block.RuntimeState.SetInstalled(Phase1SatsumaEngineDockingAuthoring.MountId, false);
                Audio = Root.AddComponent<EngineFeedbackRecordingBackend>();
                var engineGo = new GameObject("Test engine emitter"); engineGo.transform.SetParent(Root.transform, false);
                var engine = engineGo.AddComponent<AudioEmitterAuthoring>(); engine.Configure("audio.emitter.test.engine", null, Block.transform);
                var keyGo = new GameObject("Test key emitter"); keyGo.transform.SetParent(Root.transform, false);
                var key = keyGo.AddComponent<AudioEmitterAuthoring>(); key.Configure("audio.emitter.test.key", null, keyGo.transform);
                Presenter = Root.GetComponent<SatsumaEngineFeedbackPresenter>() ?? Root.AddComponent<SatsumaEngineFeedbackPresenter>();
                Presenter.ConfigureExhaust(withExhaust ? Root.GetComponent<SatsumaEngineExhaustBinding>() : null);
                Presenter.ConfigureSymptomEmitters(null, null); // This fixture isolates the original three emitters.
                Assert.That(Presenter.Configure(Host, Root.GetComponent<SatsumaIgnitionController>(), Block, Audio, engine, key), Is.True);
            }
            public void Dispose() => Object.DestroyImmediate(Root);
        }
    }

    public sealed class EngineFeedbackRecordingBackend : MonoBehaviour, IAudioBackend
    {
        public readonly HashSet<IAudioEmitter> Emitters = new();
        public readonly List<RecordingHandle> Handles = new();
        public readonly List<AudioEventRequest> Requests = new();
        public readonly Dictionary<string, float> Parameters = new();
        public int StopAllCalls, PostAttempts;
        public bool ReturnInvalid;
        public string BackendId => "audio.backend.engine_feedback_test";
        public AudioBackendKind Kind => AudioBackendKind.Unity;
        public bool IsReady => true;
        public string FailureReason => "";
        public bool RegisterEmitter(IAudioEmitter emitter, out string failure) { failure = ""; return Emitters.Add(emitter); }
        public bool UnregisterEmitter(IAudioEmitter emitter) => Emitters.Remove(emitter);
        public IAudioEventHandle PostEvent(in AudioEventRequest request)
        {
            PostAttempts++;
            Requests.Add(request);
            if (ReturnInvalid) return AudioEventHandles.Invalid;
            var result = new RecordingHandle(request.EventId, (ulong)PostAttempts); Handles.Add(result); return result;
        }
        public int Count(AudioEventId id) => Handles.Count(value => value.EventId == id);
        public bool SetParameter(AudioParameterId id, float value, IAudioEmitter emitter = null) { Parameters[id.Value] = value; return true; }
        public bool SetSwitch(AudioSwitchId group, AudioSwitchId value, IAudioEmitter emitter = null) => true;
        public bool SetState(AudioStateId group, AudioStateId value) => true;
        public void SetListenerContext(in AudioListenerContext context) { }
        public void ApplySettings(in AudioSettingsState settings) { }
        public void StopAll(float fadeSeconds = 0f) { StopAllCalls++; }
        public AudioRuntimeSnapshot CaptureSnapshot() => default;
        public sealed class RecordingHandle : IAudioEventHandle
        {
            public RecordingHandle(AudioEventId id, ulong handleId) { EventId = id; HandleId = handleId; }
            public ulong HandleId { get; }
            public AudioEventId EventId { get; }
            public bool IsValid => IsPlaying;
            public bool IsPlaying { get; private set; } = true;
            public void Stop(float fadeSeconds = 0) => IsPlaying = false;
            public void Dispose() => IsPlaying = false;
        }
    }
}
