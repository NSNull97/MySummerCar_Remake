using System;
using System.Linq;
using MSC.Audio;
using MSC.Audio.UnityFallback;
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
    public sealed class SatsumaEngineSymptomAudioTests
    {
        [Test]
        public void HealthyRunningAndFailedCrankingDoNotInventFaultSounds()
        {
            using var f = new Fixture();
            for (int i = 0; i < 100; i++) f.Tick(Point(), VehicleEngineStatus.Running);
            Assert.That(f.Audio.PostAttempts, Is.Zero);
            for (int i = 0; i < 100; i++) f.Tick(Point(1f), VehicleEngineStatus.Cranking);
            Assert.That(f.Audio.Handles.All(h => h.EventId == SatsumaEngineAudioIds.BeltSqueal), Is.True);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.BeltSqueal), Is.EqualTo(1));
            f.Tick(Point(1f), VehicleEngineStatus.Off, installed: false);
            Assert.That(f.Audio.Handles.All(h => !h.IsPlaying), Is.True);
        }

        [Test]
        public void FaultsHaveCorrectSpatialOwnersAndStopAfterRepairWithoutDuplicateLoops()
        {
            using var f = new Fixture();
            for (int i = 0; i < 300; i++) f.Tick(Point(1f));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.BeltSqueal), Is.EqualTo(1));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.Pinging), Is.EqualTo(1));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.ValveTick), Is.GreaterThan(0));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.BearingKnock), Is.GreaterThan(0));
            Assert.That(f.Audio.Requests.Where(r => r.EventId == SatsumaEngineAudioIds.IntakeSpit)
                .All(r => ReferenceEquals(r.Emitter, f.Intake)), Is.True);
            Assert.That(f.Audio.Requests.Where(r => r.EventId == SatsumaEngineAudioIds.ExhaustBackfire)
                .All(r => ReferenceEquals(r.Emitter, f.Exhaust)), Is.True);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.IntakeSpit), Is.GreaterThan(0));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.ExhaustBackfire), Is.GreaterThan(0));
            Assert.That(f.Audio.Handles.Count(h => h.IsPlaying), Is.LessThanOrEqualTo(6));
            f.Tick(Point());
            Assert.That(f.Audio.Handles.All(h => !h.IsPlaying), Is.True);
            Assert.That(f.Audio.Parameters[SatsumaEngineAudioIds.BeltGain.Value], Is.Zero);
            Assert.That(f.Audio.StopAllCalls, Is.Zero, "Never stop other audio owners.");
        }

        [Test]
        public void ValveCadenceTracksCrankRevolutionsAndResetDoesNotReplayAccumulatedPops()
        {
            using var a = new Fixture();
            using var b = new Fixture();
            for (int i = 0; i < 200; i++) a.Controller.Tick(Point(1f), VehicleEngineStatus.Running, true, false, 600f, .01f);
            for (int i = 0; i < 100; i++) b.Controller.Tick(Point(1f), VehicleEngineStatus.Running, true, false, 600f, .02f);
            Assert.That(a.Audio.Count(SatsumaEngineAudioIds.ValveTick), Is.InRange(9, 10));
            Assert.That(b.Audio.Count(SatsumaEngineAudioIds.ValveTick), Is.EqualTo(a.Audio.Count(SatsumaEngineAudioIds.ValveTick)).Within(1));
            int before = a.Audio.Count(SatsumaEngineAudioIds.IntakeSpit);
            a.Controller.Reset();
            a.Controller.Tick(Point(1f), VehicleEngineStatus.Running, true, false, 600f, 0f);
            Assert.That(a.Audio.Handles.All(h => !h.IsPlaying), Is.True);
            a.Controller.Tick(Point(1f), VehicleEngineStatus.Running, true, false, 600f, .01f);
            Assert.That(a.Audio.Count(SatsumaEngineAudioIds.IntakeSpit), Is.EqualTo(before));
        }

        [Test]
        public void ElectricFanRunsOnHotEngineOffAndRestoreSeedsLoopWithoutHistoricalStart()
        {
            using var f = new Fixture();
            f.Controller.Tick(default, VehicleEngineStatus.Off, false, true, 0f, .02f);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.FanLoop), Is.EqualTo(1));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.FanStarted), Is.Zero);
            f.Controller.Tick(default, VehicleEngineStatus.Off, false, false, 0f, .02f);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.FanStopped), Is.EqualTo(1));
            f.Controller.Tick(default, VehicleEngineStatus.Off, false, true, 0f, .02f);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.FanStarted), Is.EqualTo(1));
            Assert.That(f.Audio.Handles.Where(h => h.EventId == SatsumaEngineAudioIds.FanLoop).All(h => !h.IsPlaying), Is.True);
            for (int i = 0; i < 130; i++) f.Controller.Tick(default, VehicleEngineStatus.Off, false, true, 0f, .02f);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.FanLoop), Is.EqualTo(2));
            Assert.That(f.Audio.Requests.All(r => ReferenceEquals(r.Emitter, f.Fan)), Is.True);
            f.Controller.Reset();
            Assert.That(f.Audio.Handles.All(h => !h.IsPlaying), Is.True);
            f.Controller.Tick(default, VehicleEngineStatus.Off, false, true, 0f, .02f);
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.FanStarted), Is.EqualTo(1));
            Assert.That(f.Audio.Count(SatsumaEngineAudioIds.FanLoop), Is.EqualTo(3));
        }

        [Test]
        public void MissingFaultMediaRetriesAreBoundedAndDisposeOwnsNoOtherHandles()
        {
            using var f = new Fixture();
            f.Audio.ReturnInvalid = true;
            for (int i = 0; i < 100; i++) f.Tick(Point(1f), VehicleEngineStatus.Cranking);
            Assert.That(f.Audio.PostAttempts, Is.InRange(1, 3));
            f.Controller.Dispose();
            Assert.That(f.Audio.StopAllCalls, Is.Zero);
        }

        [Test]
        public void CanonicalBindingsAreIdempotentAndEverySymptomHasReviewedMedia()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(Phase1SatsumaBaselineBuilder.RuntimePrefabPath);
            try
            {
                var assembly = root.GetComponent<VehicleAssemblyController>();
                Assert.That(Phase1SatsumaEngineFeedbackAuthoring.ApplyToInstance(assembly), Is.Zero);
                var feedback = root.GetComponent<SatsumaEngineFeedbackPresenter>();
                Assert.That(feedback.IntakeEmitter.AudioTransform, Is.EqualTo(assembly.Parts.Single(p =>
                    p.Definition.DefinitionId == "vehicle.satsuma.part.carburetor").transform));
                Assert.That(feedback.RadiatorEmitter.AudioTransform, Is.EqualTo(assembly.Parts.Single(p =>
                    p.Definition.DefinitionId == "vehicle.satsuma.part.radiator").transform));
                Phase1SatsumaEngineAudioImporter.EnsureGeneratedForBuild();
                var library = AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(Phase1SatsumaEngineAudioImporter.LibraryPath);
                Assert.That(library.DefinitionCount, Is.EqualTo(17));
                foreach (AudioEventId id in new[] { SatsumaEngineAudioIds.BeltSqueal, SatsumaEngineAudioIds.Pinging,
                    SatsumaEngineAudioIds.ValveTick, SatsumaEngineAudioIds.BearingKnock, SatsumaEngineAudioIds.IntakeSpit,
                    SatsumaEngineAudioIds.ExhaustBackfire, SatsumaEngineAudioIds.FanStarted,
                    SatsumaEngineAudioIds.FanLoop, SatsumaEngineAudioIds.FanStopped })
                    Assert.That(library.TryResolve(id, out var entry) && entry.Clip != null, Is.True, id.Value);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static SatsumaEngineOperatingPoint Point(float fault = 0f) =>
            new(true, true, 800f, 1f, 0f, 1f, 14.7f, fault, fault, fault, fault, fault, 1f, true, 1f, 1f, 1f, fault);

        private sealed class Fixture : IDisposable
        {
            private readonly GameObject root = new("Engine symptoms test");
            public readonly EngineFeedbackRecordingBackend Audio;
            public readonly AudioEmitterAuthoring Engine, Exhaust, Intake, Fan;
            public readonly SatsumaEngineSymptomAudio Controller;
            public Fixture()
            {
                Audio = root.AddComponent<EngineFeedbackRecordingBackend>();
                Engine = Emitter("engine"); Exhaust = Emitter("exhaust"); Intake = Emitter("intake"); Fan = Emitter("fan");
                Controller = new SatsumaEngineSymptomAudio(Audio, Engine, Exhaust, Intake, Fan);
            }
            private AudioEmitterAuthoring Emitter(string suffix)
            {
                var go = new GameObject(suffix); go.transform.SetParent(root.transform, false);
                var emitter = go.AddComponent<AudioEmitterAuthoring>();
                emitter.Configure("audio.emitter.test.symptom." + suffix, null, go.transform);
                return emitter;
            }
            public void Tick(SatsumaEngineOperatingPoint point, VehicleEngineStatus status = VehicleEngineStatus.Running,
                bool installed = true) => Controller.Tick(point, status, installed, false, 800f, .02f);
            public void Dispose() { Controller.Dispose(); Object.DestroyImmediate(root); }
        }
    }
}
