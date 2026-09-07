using System.Linq;
using MSC.Audio;
using MSC.Audio.UnityFallback;
using MSC.LegacyImport.Editor.GameplayPresentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class SatsumaAudioEventMixTests
    {
        [Test]
        public void AuthoredEngineAndSelectedPackShareEventBalanceButRetainIndependentClipCorrection()
        {
            Phase1SatsumaEngineAudioImporter.EnsureGeneratedForBuild();
            Phase1UserSelectedAudioImporter.EnsureGeneratedForBuild();
            var engine = AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(Phase1SatsumaEngineAudioImporter.LibraryPath);
            var selected = AssetDatabase.LoadAssetAtPath<UnityAudioEventLibrary>(Phase1UserSelectedAudioImporter.LibraryPath);
            Assert.That(engine.Definitions.Count(value => value.MixGainDb != 0f), Is.EqualTo(15));
            Assert.That(selected.Definitions.Count(value => value.MixGainDb != 0f), Is.EqualTo(6));
            foreach (var definition in selected.Definitions)
            {
                var id = new AudioEventId(definition.EventId);
                if (!engine.TryResolve(id, out var original))
                {
                    Assert.That(definition.MixGainDb, Is.Zero, definition.EventId + " is outside Satsuma engine scope.");
                    continue;
                }
                Assert.That(definition.MixGainDb, Is.EqualTo(original.MixGainDb), definition.EventId);
                Assert.That(definition.MixBoostCeiling, Is.EqualTo(original.MixBoostCeiling), definition.EventId);
                Assert.That(definition.Volume, Is.EqualTo(original.Volume));
                Assert.That(definition.MinimumDistanceMeters, Is.EqualTo(original.MinimumDistanceMeters));
                Assert.That(definition.MaximumDistanceMeters, Is.EqualTo(original.MaximumDistanceMeters));
                Assert.That(definition.RolloffMode, Is.EqualTo(original.RolloffMode));
                Assert.That(definition.VolumeParameterId, Is.EqualTo(original.VolumeParameterId));
                Assert.That(definition.PitchParameterId, Is.EqualTo(original.PitchParameterId));
            }
            foreach (var definition in engine.Definitions)
            { Assert.That(definition.CalibrationGainDb, Is.Zero); Assert.That(definition.OutputGainCeiling, Is.Zero); }
            Assert.That(engine.TryResolve(SatsumaEngineAudioIds.KeyInserted, out var key), Is.True);
            Assert.That(key.MixGainDb, Is.Zero);
            Assert.That(selected.TryResolve(SatsumaEngineAudioIds.EngineCoastLoop, out var coast), Is.True);
            Assert.That(coast.CalibrationGainDb, Is.EqualTo(6.3f));
            Assert.That(coast.MixGainDb, Is.EqualTo(18f));
            Assert.That(coast.MixBoostCeiling, Is.EqualTo(.82f));
            Assert.That(coast.OutputGainCeiling, Is.EqualTo(.75f));
            Assert.That(coast.Clip.length, Is.EqualTo(925218f / 44100f).Within(.002f));
            foreach (var definition in selected.Definitions.Where(d => d != coast)) Assert.That(definition.OutputGainCeiling, Is.Zero);
            var untouchedCoast = AssetDatabase.LoadAssetAtPath<AudioClip>(Phase1UserSelectedAudioImporter.OutputRoot + "/Clips/idle_sisa4b.snd.wav");
            foreach (string id in new[]{"audio.event.traffic.jani.engine.loop","audio.event.traffic.petteri.engine.loop","audio.event.vehicle.engine.mechanical"})
            { Assert.That(selected.TryResolve(new AudioEventId(id),out var npc),Is.True); Assert.That(npc.Clip,Is.SameAs(untouchedCoast)); }
            Assert.That(coast.MinimumDistanceMeters, Is.EqualTo(2f));
            Assert.That(selected.TryResolve(SatsumaEngineAudioIds.EngineThrottleLoop, out var throttle), Is.True);
            Assert.That(throttle.MixGainDb, Is.EqualTo(12f));
            Assert.That(throttle.MixBoostCeiling, Is.EqualTo(.6f));
            Assert.That(throttle.MinimumDistanceMeters, Is.EqualTo(2f));
            Assert.That(selected.TryResolve(SatsumaEngineAudioIds.StarterLoop, out var starter), Is.True);
            Assert.That(starter.CalibrationGainDb, Is.EqualTo(6.5f));
            Assert.That(starter.MixGainDb, Is.EqualTo(6f));
            Assert.That(starter.MinimumDistanceMeters, Is.EqualTo(1f), "Only running front beds gain a wider near field.");
            Assert.That(starter.Clip.length, Is.EqualTo(132079f / 44100f).Within(.002f));
            Assert.That(selected.TryResolve(SatsumaEngineAudioIds.StarterEngaged, out var lead), Is.True);
            Assert.That(lead.Clip.length, Is.EqualTo(14377f / 44100f).Within(.002f));
            Assert.That(engine.TryResolve(SatsumaEngineAudioIds.ExhaustBackfire, out var pop), Is.True);
            Assert.That(pop.MixGainDb, Is.EqualTo(6f));
        }

        [Test]
        public void SerializedOverLimitMixCannotEnterPlaybackLibrary()
        {
            var clip = AudioClip.Create("Mix validation fixture", 128, 1, 48000, false);
            var library = ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
            try
            {
                var definition = new UnityAudioEventDefinition();
                definition.ConfigureForAuthoring("audio.event.test.event-mix", clip, UnityAudioCategory.Vehicle,
                    true, 1f, 1f, 1f, 1f, 40f);
                library.ConfigureForAuthoring(definition);
                Assert.That(library.Validate(out _), Is.True);
                JsonUtility.FromJsonOverwrite("{\"calibrationGainDb\":12,\"mixGainDb\":12}", definition);
                Assert.That(library.Validate(out string[] failures), Is.False);
                Assert.That(failures, Has.Some.Contains("combined gain ceiling"));
                JsonUtility.FromJsonOverwrite("{\"calibrationGainDb\":0,\"mixGainDb\":0,\"outputGainCeiling\":1.1}", definition);
                Assert.That(library.Validate(out _), Is.False);
            }
            finally { Object.DestroyImmediate(library); Object.DestroyImmediate(clip); }
        }
    }
}
