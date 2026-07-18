using System;
using System.Collections;
using MSC.Audio;
using MSC.Audio.Composition;
using MSC.Audio.VehicleIntegration;
using MSC.Vehicle;
using NUnit.Framework;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MSC.Tests.PlayMode.AudioVehicleIntegration
{
    public sealed class VehicleAudioPlaytestBootstrapTests
    {
        private const string PlaytestScenePath =
            "Assets/Game/Audio/Content/Scenes/M08_VehicleAudioPlaytest.unity";

        [UnityTest]
        public IEnumerator AuthoredPlaytestScene_LoadsDrivableFixtureAndProductionRoute()
        {
#if UNITY_EDITOR
            AsyncOperation load = EditorSceneManager.LoadSceneAsyncInPlayMode(
                PlaytestScenePath,
                new LoadSceneParameters(LoadSceneMode.Single));
            Assert.That(load, Is.Not.Null);
            while (!load.isDone)
            {
                yield return null;
            }

            VehicleAudioPlaytestBootstrap playtest = null;
            for (int frame = 0; frame < 900; frame++)
            {
                playtest = UnityEngine.Object.FindFirstObjectByType<
                    VehicleAudioPlaytestBootstrap>(FindObjectsInactive.Include);
                if (playtest != null && playtest.IsInitialized)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(playtest, Is.Not.Null);
            Assert.That(playtest.IsInitialized, Is.True, playtest.LastFailure);
            Assert.That(playtest.SimulationHost, Is.Not.Null);
            Assert.That(playtest.Presenter, Is.Not.Null);
            Assert.That(playtest.VehicleBackend, Is.Not.Null);
            Assert.That(
                playtest.Presenter.BackendComponent,
                Is.SameAs(playtest.VehicleBackend));

            bool requireLiveWwise = string.Equals(
                Environment.GetEnvironmentVariable("MSC_REQUIRE_LIVE_WWISE"),
                "1",
                StringComparison.Ordinal);
            for (int frame = 0;
                 frame < 360 && requireLiveWwise &&
                 playtest.VehicleBackend.Kind != AudioBackendKind.Wwise;
                 frame++)
            {
                yield return null;
            }

            AudioRuntimeSnapshot snapshot = playtest.VehicleBackend.CaptureSnapshot();
            Assert.That(snapshot.IsReady, Is.True, snapshot.LastFailure);
            if (requireLiveWwise)
            {
                Assert.That(snapshot.Kind, Is.EqualTo(AudioBackendKind.Wwise));
                Assert.That(snapshot.LoadedBankCount, Is.EqualTo(6));
                Assert.That(snapshot.MissingBanks, Is.Empty);
            }
#else
            Assert.Ignore("The authored M08 fixture is loaded by asset path in the Editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator ExistingM06Fixture_IsReboundToProductionAudioAdapter()
        {
            Scene fixture = SceneManager.CreateScene("M08_VehicleAudioSyntheticFixture");
            var vehicle = new GameObject("Vehicle");
            vehicle.SetActive(false);
            SceneManager.MoveGameObjectToScene(vehicle, fixture);
            VehicleSimulationHost host = vehicle.AddComponent<VehicleSimulationHost>();
            RecordingVehicleBackend oldLocalBackend =
                vehicle.AddComponent<RecordingVehicleBackend>();
            VehicleAudioPresenter presenter = vehicle.AddComponent<VehicleAudioPresenter>();
            presenter.Configure(host, oldLocalBackend);

            var cameraObject = new GameObject("VehicleCamera");
            cameraObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(cameraObject, fixture);
            Camera camera = cameraObject.AddComponent<Camera>();

            var playtestObject = new GameObject("PlaytestComposition");
            playtestObject.SetActive(false);
            RecordingBackend productionBackend =
                playtestObject.AddComponent<RecordingBackend>();
            RecordingRuntimeOwner runtimeOwner =
                playtestObject.AddComponent<RecordingRuntimeOwner>();
            VehicleAudioPlaytestBootstrap playtest =
                playtestObject.AddComponent<VehicleAudioPlaytestBootstrap>();
            playtest.Configure(
                productionBackend,
                runtimeOwner,
                fixture.name);

            Assert.That(
                playtest.TryBindPrototype(fixture, out string failure),
                Is.True,
                failure);
            Assert.That(playtest.SimulationHost, Is.SameAs(host));
            Assert.That(playtest.Presenter, Is.SameAs(presenter));
            Assert.That(playtest.VehicleBackend, Is.Not.Null);
            Assert.That(presenter.BackendComponent, Is.SameAs(playtest.VehicleBackend));
            Assert.That(oldLocalBackend.enabled, Is.False);
            Assert.That(runtimeOwner.BoundListener, Is.SameAs(camera.transform));
            Assert.That(productionBackend.RegisteredEmitterCount, Is.EqualTo(1));

            UnityEngine.Object.Destroy(playtestObject);
            AsyncOperation unload = SceneManager.UnloadSceneAsync(fixture);
            while (unload != null && !unload.isDone)
            {
                yield return null;
            }
        }

        private sealed class RecordingRuntimeOwner : MonoBehaviour, IAudioRuntimeOwner
        {
            public Transform BoundListener { get; private set; }

            public bool BindListener(Transform listenerTransform, out string failure)
            {
                BoundListener = listenerTransform;
                failure = string.Empty;
                return true;
            }
        }

        private class RecordingBackend : MonoBehaviour, IAudioBackend
        {
            public string BackendId => "audio.test.production";
            public AudioBackendKind Kind => AudioBackendKind.Unity;
            public bool IsReady => true;
            public string FailureReason => string.Empty;
            public int RegisteredEmitterCount { get; private set; }

            public bool RegisterEmitter(IAudioEmitter emitter, out string failure)
            {
                RegisteredEmitterCount++;
                failure = string.Empty;
                return true;
            }

            public bool UnregisterEmitter(IAudioEmitter emitter)
            {
                RegisteredEmitterCount = Mathf.Max(0, RegisteredEmitterCount - 1);
                return true;
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

            public bool SetState(
                AudioStateId stateGroupId,
                AudioStateId stateValueId) => true;

            public void SetListenerContext(in AudioListenerContext context) { }
            public void ApplySettings(in AudioSettingsState settings) { }
            public void StopAll(float fadeSeconds = 0f) { }

            public AudioRuntimeSnapshot CaptureSnapshot() => new AudioRuntimeSnapshot(
                BackendId,
                Kind,
                true,
                true,
                RegisteredEmitterCount,
                0,
                0,
                Array.Empty<string>(),
                default,
                string.Empty);
        }

        private sealed class RecordingVehicleBackend : RecordingBackend, IVehicleAudioBackend
        {
            public void SetVehicleParameters(in VehicleAudioParameters parameters) { }
            public void PostVehicleEvent(VehicleAudioEvent audioEvent) { }
        }
    }
}
