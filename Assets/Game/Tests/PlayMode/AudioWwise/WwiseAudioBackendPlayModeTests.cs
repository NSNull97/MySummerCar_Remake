using System.Collections;
using System.Collections.Generic;
using MSC.Audio;
using MSC.Audio.Wwise;
using MSC.Tests.AudioWwise;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.AudioWwise
{
    public sealed class WwiseAudioBackendPlayModeTests
    {
        private readonly List<UnityEngine.Object> ownedObjects =
            new List<UnityEngine.Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = ownedObjects.Count - 1; index >= 0; index--)
            {
                if (ownedObjects[index] != null)
                {
                    UnityEngine.Object.Destroy(ownedObjects[index]);
                }
            }

            ownedObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator EventHandle_StopsTheWwisePlayingId_AndLifecycleUnregistersObjects()
        {
            var api = new RecordingWwiseSoundEngineApi();
            WwiseAudioBackend backend = CreateBackend(api);
            var emitterObject = Own(new GameObject("Emitter"));
            RecordingAudioEmitter emitter = emitterObject.AddComponent<RecordingAudioEmitter>();
            emitter.Configure("audio.emitter.playmode");
            Assert.That(backend.RegisterEmitter(emitter, out string failure), Is.True, failure);

            IAudioEventHandle handle = backend.PostEvent(new AudioEventRequest(
                AudioProjectIds.Events.WeatherThunder,
                emitter,
                volume01: 0.4f));

            Assert.That(handle.IsValid, Is.True);
            Assert.That(handle.IsPlaying, Is.True);
            Assert.That(api.EventCalls, Has.Count.EqualTo(1));
            Assert.That(api.EventCalls[0].EventName, Is.EqualTo("Play_Thunder"));
            Assert.That(api.EventCalls[0].GameObject, Is.EqualTo(emitterObject));
            Assert.That(api.RtpcPlayingIdCalls, Has.Count.EqualTo(1));
            Assert.That(api.RtpcPlayingIdCalls[0].Name, Is.EqualTo("Event_Volume"));
            Assert.That(api.RtpcPlayingIdCalls[0].Value, Is.EqualTo(40f).Within(0.001f));

            uint playingId = api.EventCalls[0].PlayingId;
            handle.Stop(0.25f);
            Assert.That(api.StopCalls, Has.Count.EqualTo(1));
            Assert.That(api.StopCalls[0].PlayingId, Is.EqualTo(playingId));
            Assert.That(api.StopCalls[0].FadeMilliseconds, Is.EqualTo(250));
            Assert.That(handle.IsValid, Is.False);
            Assert.That(backend.CaptureSnapshot().ActiveVoiceCount, Is.Zero);

            Assert.That(backend.UnregisterEmitter(emitter), Is.True);
            Assert.That(api.UnregisteredGameObjects, Does.Contain(emitterObject));
            GameObject backendObject = backend.gameObject;
            UnityEngine.Object.Destroy(backendObject);
            yield return null;
            Assert.That(api.UnregisteredGameObjects, Does.Contain(backendObject));
        }

        [UnityTest]
        public IEnumerator EndCallback_ReleasesHandle_WithoutRequiringLoadedBanks()
        {
            var api = new RecordingWwiseSoundEngineApi();
            WwiseAudioBackend backend = CreateBackend(api);
            IAudioEventHandle handle = backend.PostEvent(new AudioEventRequest(
                AudioProjectIds.Events.WeatherThunder,
                worldPosition: new Vector3(2f, 3f, 4f)));

            Assert.That(handle.IsPlaying, Is.True);
            Assert.That(api.RegisteredGameObjects, Has.Count.EqualTo(2));
            uint playingId = api.EventCalls[0].PlayingId;
            api.Complete(playingId);
            yield return null;

            Assert.That(handle.IsValid, Is.False);
            Assert.That(backend.CaptureSnapshot().ActiveVoiceCount, Is.Zero);
            Assert.That(api.UnregisteredGameObjects, Has.Count.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisposingDelayedHandle_CancelsPostBeforeSoundEngineCall()
        {
            var api = new RecordingWwiseSoundEngineApi();
            WwiseAudioBackend backend = CreateBackend(api);
            IAudioEventHandle handle = backend.PostEvent(new AudioEventRequest(
                AudioProjectIds.Events.WeatherThunder,
                delaySeconds: 1d));

            Assert.That(handle.IsValid, Is.True);
            Assert.That(handle.IsPlaying, Is.False);
            handle.Dispose();
            yield return null;

            Assert.That(handle.IsValid, Is.False);
            Assert.That(api.EventCalls, Is.Empty);
        }

        private WwiseAudioBackend CreateBackend(RecordingWwiseSoundEngineApi api)
        {
            AudioEventMap eventMap = Own(ScriptableObject.CreateInstance<AudioEventMap>());
            eventMap.ConfigureForTests(new AudioEventMapEntry(
                AudioProjectIds.Events.WeatherThunder.Value,
                "Play_Thunder",
                "Weather",
                isSpatialized: true,
                canOverlap: true));
            AudioParameterMap parameterMap =
                Own(ScriptableObject.CreateInstance<AudioParameterMap>());
            WwiseBackendNameMap nameMap =
                Own(ScriptableObject.CreateInstance<WwiseBackendNameMap>());
            var backendObject = Own(new GameObject("WwiseBackend"));
            backendObject.SetActive(false);
            WwiseAudioBackend backend = backendObject.AddComponent<WwiseAudioBackend>();
            backend.ConfigureForTests(
                eventMap,
                parameterMap,
                nameMap,
                api,
                "Event_Volume");
            backendObject.SetActive(true);
            return backend;
        }

        private T Own<T>(T value) where T : UnityEngine.Object
        {
            ownedObjects.Add(value);
            return value;
        }
    }
}
