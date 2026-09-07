using System.Collections;
using System.Linq;
using System.Reflection;
using MSC.Audio;
using MSC.Audio.UnityFallback;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.AudioUnityFallback
{
    public sealed class UnityAudioScopedParameterTests
    {
        [UnityTest]
        public IEnumerator OptionalLoopBindingsApplyScopedGainPitchAndKeepLegacyDefaults()
        {
            var root = new GameObject("Test scoped audio backend");
            var emitterObject = new GameObject("Test scoped emitter");
            AudioClip clip = AudioClip.Create("Project owned silent fixture", 48000, 1, 48000, false);
            var library = ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
            try
            {
                var backend = root.AddComponent<UnityAudioBackend>();
                // Compare linear RTPC ratios below the existing +4 dB mix
                // headroom/Clamp01 ceiling. A full-volume legacy voice clips
                // to 1 while its attenuated counterpart does not.
                backend.ApplySettings(new AudioSettingsState(.25f, 1, 1, 1, 1, 1,
                    AudioDynamicRangeMode.Balanced, false, false, false, false));
                var emitter = emitterObject.AddComponent<AudioEmitterAuthoring>();
                emitter.Configure("audio.emitter.scoped_test");
                Assert.That(backend.RegisterEmitter(emitter, out string failure), Is.True, failure);
                var tuned = new UnityAudioEventDefinition();
                var legacy = new UnityAudioEventDefinition();
                Define(tuned, "audio.event.test.tuned_loop", clip);
                Define(legacy, "audio.event.test.legacy_loop", clip);
                Set(tuned, "volumeParameterId", "audio.parameter.test.gain");
                Set(tuned, "pitchParameterId", "audio.parameter.test.pitch");
                Set(library, "events", new[] { tuned, legacy });
                Set(backend, "eventLibrary", library);
                var gain = new AudioParameterId("audio.parameter.test.gain");
                var pitch = new AudioParameterId("audio.parameter.test.pitch");
                backend.SetParameter(gain, .7f); backend.SetParameter(pitch, 1.3f);
                backend.SetParameter(gain, .25f, emitter); backend.SetParameter(pitch, .75f, emitter);
                var tunedHandle = backend.PostEvent(new AudioEventRequest(new AudioEventId(tuned.EventId), emitter));
                var legacyHandle = backend.PostEvent(new AudioEventRequest(new AudioEventId(legacy.EventId), emitter));
                Assert.That(tunedHandle.IsValid && legacyHandle.IsValid, Is.True);
                yield return null;
                AudioSource[] sources = root.GetComponentsInChildren<AudioSource>().Where(source => source.clip == clip).ToArray();
                Assert.That(sources, Has.Length.EqualTo(2));
                AudioSource tunedSource = sources.Single(source => Mathf.Abs(source.pitch - .75f) < .001f);
                AudioSource legacySource = sources.Single(source => Mathf.Abs(source.pitch - 1f) < .001f);
                Assert.That(legacySource.volume, Is.GreaterThan(0f).And.LessThan(.99f),
                    "This scoped-gain fixture must stay audible and below output saturation.");
                Assert.That(tunedSource.volume / legacySource.volume, Is.EqualTo(.25f).Within(.001f));
                backend.SetParameter(gain, .5f, emitter); backend.SetParameter(pitch, 1.7f, emitter);
                yield return null;
                Assert.That(tunedSource.pitch, Is.EqualTo(1.7f).Within(.001f));
                Assert.That(tunedSource.volume / legacySource.volume, Is.EqualTo(.5f).Within(.001f));
                Assert.That(legacySource.pitch, Is.EqualTo(1f));
                tunedHandle.Stop(); legacyHandle.Stop();
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(emitterObject);
                Object.DestroyImmediate(library); Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void PitchBindingRejectsOneShotLifetimeMismatch()
        {
            var library = ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
            AudioClip clip = AudioClip.Create("Project owned one-shot fixture", 4800, 1, 48000, false);
            try
            {
                var entry = new UnityAudioEventDefinition(); Define(entry, "audio.event.test.oneshot", clip);
                Set(entry, "loop", false); Set(entry, "pitchParameterId", "audio.parameter.test.pitch");
                Set(library, "events", new[] { entry });
                Assert.That(library.Validate(out string[] failures), Is.False);
                Assert.That(failures.Any(failure => failure.Contains("one-shot expiry")), Is.True);
            }
            finally { Object.DestroyImmediate(library); Object.DestroyImmediate(clip); }
        }

        private static void Define(UnityAudioEventDefinition definition, string id, AudioClip clip)
        {
            Set(definition, "eventId", id); Set(definition, "clip", clip);
            Set(definition, "loop", true); Set(definition, "volume", 1f);
            Set(definition, "pitch", 1f); Set(definition, "spatialBlend", 0f);
        }
        private static void Set(object instance, string name, object value) => instance.GetType()
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(instance, value);
    }
}
