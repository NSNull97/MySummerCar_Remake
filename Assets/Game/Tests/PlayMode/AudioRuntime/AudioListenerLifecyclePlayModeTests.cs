using System;
using System.Collections;
using System.Collections.Generic;
using MSC.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.AudioRuntime
{
    public sealed class AudioListenerLifecyclePlayModeTests
    {
        private readonly List<GameObject> cleanup = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject item in cleanup)
            {
                if (item != null) UnityEngine.Object.Destroy(item);
            }

            cleanup.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ActivePlayer_AddThenConfigure_PublishesCameraContextWithoutFalseError()
        {
            ListenerLifecycleRecordingBackend backend = CreateBackend();
            GameObject player = CreateObject("Active player");
            GameObject camera = CreateObject("Explicit camera");
            camera.transform.SetParent(player.transform, false);
            camera.transform.localPosition = new Vector3(0f, 1.65f, 0f);
            player.transform.position = new Vector3(12f, 3f, 8f);
            AudioListenerContextPresenter listener = player.AddComponent<AudioListenerContextPresenter>();

            listener.Configure(backend, camera.transform);
            yield return null;
            yield return null;

            Assert.That(listener.enabled, Is.True);
            Assert.That(backend.ContextCount, Is.GreaterThan(0));
            Assert.That(backend.Listener.StableListenerId, Is.EqualTo("audio.listener.player"));
            Assert.That(backend.Listener.WorldPosition, Is.EqualTo(camera.transform.position));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MissingBackend_DisablesAtStart_ExplicitRebindRecovers()
        {
            GameObject player = CreateObject("Unconfigured player");
            AudioListenerContextPresenter listener = player.AddComponent<AudioListenerContextPresenter>();
            LogAssert.Expect(LogType.Error,
                "Audio listener requires an explicit component implementing IAudioBackend.");
            yield return null;
            Assert.That(listener.enabled, Is.False);

            ListenerLifecycleRecordingBackend backend = CreateBackend();
            listener.Configure(backend, player.transform, "audio.listener.rebound");
            yield return null;
            yield return null;

            Assert.That(listener.enabled, Is.True);
            Assert.That(backend.Listener.StableListenerId, Is.EqualTo("audio.listener.rebound"));
            Assert.That(backend.ContextCount, Is.GreaterThan(0));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator DeferredPlayer_ConfigureBeforeActivation_PreservesExistingLifecycle()
        {
            ListenerLifecycleRecordingBackend backend = CreateBackend();
            GameObject player = CreateObject("Deferred player");
            player.SetActive(false);
            AudioListenerContextPresenter listener = player.AddComponent<AudioListenerContextPresenter>();
            listener.Configure(backend, player.transform);
            yield return null;
            Assert.That(backend.ContextCount, Is.Zero);

            player.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(listener.enabled, Is.True);
            Assert.That(backend.Listener.IsValid, Is.True);
            Assert.That(backend.ContextCount, Is.GreaterThan(0));
            LogAssert.NoUnexpectedReceived();
        }

        private ListenerLifecycleRecordingBackend CreateBackend() =>
            CreateObject("Listener backend").AddComponent<ListenerLifecycleRecordingBackend>();

        private GameObject CreateObject(string name)
        {
            var created = new GameObject(name);
            cleanup.Add(created);
            return created;
        }
    }

    public sealed class ListenerLifecycleRecordingBackend : MonoBehaviour, IAudioBackend
    {
        public string BackendId => "audio.test.listener.lifecycle";
        public AudioBackendKind Kind => AudioBackendKind.Unity;
        public bool IsReady => true;
        public string FailureReason => string.Empty;
        public int ContextCount { get; private set; }
        public AudioListenerContext Listener { get; private set; }

        public bool RegisterEmitter(IAudioEmitter emitter, out string failure)
        {
            failure = string.Empty;
            return true;
        }

        public bool UnregisterEmitter(IAudioEmitter emitter) => true;
        public IAudioEventHandle PostEvent(in AudioEventRequest request) => AudioEventHandles.Invalid;
        public bool SetParameter(AudioParameterId parameterId, float value, IAudioEmitter emitter = null) => true;
        public bool SetSwitch(AudioSwitchId group, AudioSwitchId value, IAudioEmitter emitter = null) => true;
        public bool SetState(AudioStateId group, AudioStateId value) => true;
        public void SetListenerContext(in AudioListenerContext context)
        {
            Listener = context;
            ContextCount++;
        }

        public void ApplySettings(in AudioSettingsState settings) { }
        public void StopAll(float fadeSeconds = 0f) { }
        public AudioRuntimeSnapshot CaptureSnapshot() => new AudioRuntimeSnapshot(
            BackendId, Kind, true, true, 0, 0, 0, Array.Empty<string>(), Listener, string.Empty);
    }
}
