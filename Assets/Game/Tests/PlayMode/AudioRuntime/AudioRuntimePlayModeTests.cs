using System;
using System.Collections;
using System.Collections.Generic;
using MSC.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.AudioRuntime
{
    public sealed class AudioRuntimePlayModeTests
    {
        private readonly List<GameObject> cleanup = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                {
                    UnityEngine.Object.Destroy(cleanup[index]);
                }
            }

            cleanup.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator AdditiveCellUnload_RemovesRegisteredEmitter()
        {
            RecordingAudioBackend backend = CreateComponent<RecordingAudioBackend>("Backend");
            AudioBackendRouter router = CreateComponent<AudioBackendRouter>("Router");
            router.Configure(null, backend, scanLoadedScenes: false);

            Scene cell = SceneManager.CreateScene("M08_AudioCell_UnloadTest");
            GameObject emitterObject = new GameObject("CellEmitter");
            SceneManager.MoveGameObjectToScene(emitterObject, cell);
            AudioEmitterAuthoring emitter = emitterObject.AddComponent<AudioEmitterAuthoring>();
            emitter.Configure("audio.emitter.cell.unload_test");

            Assert.That(router.RegisterEmitter(emitter, out string failure), Is.True, failure);
            Assert.That(router.RegisteredEmitterCount, Is.EqualTo(1));
            Assert.That(backend.RegisteredEmitterCount, Is.EqualTo(1));

            AsyncOperation unload = SceneManager.UnloadSceneAsync(cell);
            Assert.That(unload, Is.Not.Null);
            while (!unload.isDone)
            {
                yield return null;
            }

            yield return null;
            Assert.That(router.RegisteredEmitterCount, Is.Zero);
            Assert.That(backend.RegisteredEmitterCount, Is.Zero);
            Assert.That(backend.UnregisterCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DestroyedEmitterInLoadedScene_IsPrunedFromRouterAndBackend()
        {
            RecordingAudioBackend backend = CreateComponent<RecordingAudioBackend>("Backend");
            AudioBackendRouter router = CreateComponent<AudioBackendRouter>("Router");
            router.Configure(null, backend, scanLoadedScenes: false);

            GameObject emitterObject = CreateObject("DestroyedEmitter");
            AudioEmitterAuthoring emitter = emitterObject.AddComponent<AudioEmitterAuthoring>();
            emitter.Configure("audio.emitter.destroyed_same_scene_test");

            Assert.That(router.RegisterEmitter(emitter, out string failure), Is.True, failure);
            Assert.That(router.RegisteredEmitterCount, Is.EqualTo(1));
            Assert.That(backend.RegisteredEmitterCount, Is.EqualTo(1));

            UnityEngine.Object.Destroy(emitterObject);
            yield return null;

            Assert.That(router.RegisteredEmitterCount, Is.Zero);
            Assert.That(backend.RegisteredEmitterCount, Is.Zero);
            Assert.That(backend.UnregisterCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DisableThenEnable_ReconcilesEmitterMissedDuringSceneUnload()
        {
            RecordingAudioBackend backend = CreateComponent<RecordingAudioBackend>("Backend");
            AudioBackendRouter router = CreateComponent<AudioBackendRouter>("Router");
            router.Configure(null, backend, scanLoadedScenes: true);

            Scene cell = SceneManager.CreateScene("M08_AudioCell_DisabledRouterTest");
            GameObject emitterObject = new GameObject("DisabledRouterCellEmitter");
            SceneManager.MoveGameObjectToScene(emitterObject, cell);
            AudioEmitterAuthoring emitter = emitterObject.AddComponent<AudioEmitterAuthoring>();
            emitter.Configure("audio.emitter.disabled_router_test");

            Assert.That(router.RegisterEmitter(emitter, out string failure), Is.True, failure);
            router.enabled = false;

            AsyncOperation unload = SceneManager.UnloadSceneAsync(cell);
            Assert.That(unload, Is.Not.Null);
            while (!unload.isDone)
            {
                yield return null;
            }

            yield return null;
            Assert.That(backend.RegisteredEmitterCount, Is.EqualTo(1));

            router.enabled = true;
            Assert.That(router.RegisteredEmitterCount, Is.Zero);
            Assert.That(backend.RegisteredEmitterCount, Is.Zero);
            Assert.That(backend.UnregisterCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ListenerTrigger_TransitionsInteriorAndExteriorWithoutRaycasts()
        {
            RecordingAudioBackend backend = CreateComponent<RecordingAudioBackend>("Backend");

            GameObject zoneObject = CreateObject("InteriorZone");
            BoxCollider zoneCollider = zoneObject.AddComponent<BoxCollider>();
            zoneCollider.size = new Vector3(4f, 4f, 4f);
            AudioEnvironmentZone zone = zoneObject.AddComponent<AudioEnvironmentZone>();
            zone.Configure(
                "audio.zone.test.interior",
                zoneCollider,
                10,
                AudioListenerSpace.Interior,
                1f,
                0.25f,
                0.4f);

            GameObject listenerObject = CreateObject("Listener");
            listenerObject.SetActive(false);
            listenerObject.transform.position = Vector3.right * 10f;
            SphereCollider listenerCollider = listenerObject.AddComponent<SphereCollider>();
            listenerCollider.radius = 0.25f;
            Rigidbody listenerBody = listenerObject.AddComponent<Rigidbody>();
            listenerBody.useGravity = false;
            listenerBody.isKinematic = true;
            AudioListenerContextPresenter presenter =
                listenerObject.AddComponent<AudioListenerContextPresenter>();
            presenter.Configure(backend, listenerObject.transform, "audio.listener.test");
            listenerObject.SetActive(true);

            yield return null;
            Assert.That(
                presenter.CurrentEnvironment.ListenerSpace,
                Is.EqualTo(AudioListenerSpace.Exterior));

            listenerBody.position = Vector3.zero;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(
                presenter.CurrentEnvironment.ListenerSpace,
                Is.EqualTo(AudioListenerSpace.Interior));

            listenerBody.position = Vector3.right * 10f;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(
                presenter.CurrentEnvironment.ListenerSpace,
                Is.EqualTo(AudioListenerSpace.Exterior));
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            GameObject gameObject = CreateObject(name);
            return gameObject.AddComponent<T>();
        }

        private GameObject CreateObject(string name)
        {
            var gameObject = new GameObject(name);
            cleanup.Add(gameObject);
            return gameObject;
        }

        private sealed class RecordingAudioBackend : MonoBehaviour, IAudioBackend
        {
            private readonly HashSet<IAudioEmitter> emitters = new HashSet<IAudioEmitter>();
            private AudioListenerContext listener;

            public string BackendId => "audio.test.play_mode";
            public AudioBackendKind Kind => AudioBackendKind.Unity;
            public bool IsReady => true;
            public string FailureReason => string.Empty;
            public int RegisteredEmitterCount => emitters.Count;
            public int UnregisterCount { get; private set; }

            public bool RegisterEmitter(IAudioEmitter emitter, out string failure)
            {
                if (emitter == null || !emitters.Add(emitter))
                {
                    failure = "duplicate";
                    return false;
                }

                failure = string.Empty;
                return true;
            }

            public bool UnregisterEmitter(IAudioEmitter emitter)
            {
                bool removed = emitters.Remove(emitter);
                if (removed)
                {
                    UnregisterCount++;
                }

                return removed;
            }

            public IAudioEventHandle PostEvent(in AudioEventRequest request) =>
                AudioEventHandles.Invalid;

            public bool SetParameter(
                AudioParameterId parameterId,
                float value,
                IAudioEmitter emitter = null) => true;

            public bool SetSwitch(
                AudioSwitchId switchGroupId,
                AudioSwitchId switchValueId,
                IAudioEmitter emitter = null) => true;

            public bool SetState(AudioStateId stateGroupId, AudioStateId stateValueId) => true;

            public void SetListenerContext(in AudioListenerContext context)
            {
                listener = context;
            }

            public void ApplySettings(in AudioSettingsState settings)
            {
            }

            public void StopAll(float fadeSeconds = 0f)
            {
            }

            public AudioRuntimeSnapshot CaptureSnapshot() => new AudioRuntimeSnapshot(
                BackendId,
                Kind,
                true,
                true,
                emitters.Count,
                0,
                0,
                Array.Empty<string>(),
                listener,
                string.Empty);
        }
    }
}
