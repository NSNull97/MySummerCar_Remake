using System.Collections;
using System.Collections.Generic;
using MSC.Audio.Composition;
using MSC.Vehicle;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Audio.VehicleIntegration
{
    /// <summary>
    /// Development-only M08 composition. It reuses the bounded M06 drivable
    /// fixture and replaces only its local diagnostic audio route with the
    /// production audio router. No prototype vehicle is added to Bootstrap.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class VehicleAudioPlaytestBootstrap : MonoBehaviour
    {
        public const string DefaultPrototypeSceneName = "VehicleSimulationPrototype";

        [SerializeField] private MonoBehaviour backendComponent;
        [SerializeField] private MonoBehaviour runtimeOwnerComponent;
        [SerializeField] private string prototypeSceneName = DefaultPrototypeSceneName;

        public bool IsInitialized { get; private set; }
        public string LastFailure { get; private set; } = string.Empty;
        public VehicleSimulationHost SimulationHost { get; private set; }
        public VehicleAudioPresenter Presenter { get; private set; }
        public VehicleAudioEmitterBackend VehicleBackend { get; private set; }

        private IEnumerator Start()
        {
            if (!TryValidate(out string failure))
            {
                Fail(failure);
                yield break;
            }

            Scene prototypeScene = SceneManager.GetSceneByName(prototypeSceneName.Trim());
            if (!prototypeScene.IsValid() || !prototypeScene.isLoaded)
            {
                AsyncOperation load = SceneManager.LoadSceneAsync(
                    prototypeSceneName.Trim(),
                    LoadSceneMode.Additive);
                if (load == null)
                {
                    Fail("Could not start loading the M06 vehicle prototype scene.");
                    yield break;
                }

                while (!load.isDone)
                {
                    yield return null;
                }

                prototypeScene = SceneManager.GetSceneByName(prototypeSceneName.Trim());
            }

            if (!prototypeScene.IsValid() || !prototypeScene.isLoaded)
            {
                Fail("The M06 vehicle prototype scene did not finish loading.");
                yield break;
            }

            if (!TryBindPrototype(prototypeScene, out failure))
            {
                Fail(failure);
                yield break;
            }

            LastFailure = string.Empty;
            IsInitialized = true;
            Debug.Log(
                "M08 vehicle audio playtest ready: production router bound to the M06 drivable fixture.",
                this);
        }

        public bool TryValidate(out string failure)
        {
            if (!(backendComponent is IAudioBackend))
            {
                failure = "Vehicle audio playtest backend does not implement IAudioBackend.";
                return false;
            }

            if (!(runtimeOwnerComponent is IAudioRuntimeOwner))
            {
                failure = "Vehicle audio playtest runtime owner does not implement IAudioRuntimeOwner.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(prototypeSceneName))
            {
                failure = "Vehicle audio playtest prototype scene name is empty.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        public bool TryBindPrototype(Scene prototypeScene, out string failure)
        {
            if (!TryFindExactlyOne(prototypeScene, out VehicleSimulationHost host, out failure) ||
                !TryFindExactlyOne(prototypeScene, out VehicleAudioPresenter presenter, out failure) ||
                !TryFindExactlyOne(prototypeScene, out Camera camera, out failure))
            {
                return false;
            }

            Behaviour oldLocalBackend = presenter.BackendComponent as Behaviour;
            presenter.enabled = false;
            if (oldLocalBackend != null)
            {
                oldLocalBackend.enabled = false;
            }

            AudioEmitterAuthoring emitter = host.GetComponent<AudioEmitterAuthoring>();
            if (emitter == null)
            {
                emitter = host.gameObject.AddComponent<AudioEmitterAuthoring>();
            }

            emitter.Configure(
                "audio.emitter.vehicle.m08.playtest",
                backendComponent,
                host.transform);

            VehicleAudioEmitterBackend adapter =
                host.GetComponent<VehicleAudioEmitterBackend>();
            if (adapter == null)
            {
                adapter = host.gameObject.AddComponent<VehicleAudioEmitterBackend>();
            }

            adapter.Configure(backendComponent, emitter);
            adapter.enabled = true;
            if (!adapter.TryInitialize(out failure))
            {
                adapter.enabled = false;
                return false;
            }

            presenter.Configure(host, adapter);
            if (!presenter.TryInitialize(out failure))
            {
                return false;
            }

            presenter.enabled = true;
            if (!((IAudioRuntimeOwner)runtimeOwnerComponent)
                    .BindListener(camera.transform, out failure))
            {
                presenter.enabled = false;
                return false;
            }

            SimulationHost = host;
            Presenter = presenter;
            VehicleBackend = adapter;
            failure = string.Empty;
            return true;
        }

        public void Configure(
            MonoBehaviour configuredBackend,
            MonoBehaviour configuredRuntimeOwner,
            string configuredPrototypeSceneName = DefaultPrototypeSceneName)
        {
            backendComponent = configuredBackend;
            runtimeOwnerComponent = configuredRuntimeOwner;
            prototypeSceneName = string.IsNullOrWhiteSpace(configuredPrototypeSceneName)
                ? DefaultPrototypeSceneName
                : configuredPrototypeSceneName.Trim();
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            MonoBehaviour configuredBackend,
            MonoBehaviour configuredRuntimeOwner,
            string configuredPrototypeSceneName = DefaultPrototypeSceneName) =>
            Configure(
                configuredBackend,
                configuredRuntimeOwner,
                configuredPrototypeSceneName);
#endif

        private static bool TryFindExactlyOne<T>(
            Scene scene,
            out T result,
            out string failure)
            where T : Component
        {
            var matches = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                matches.AddRange(roots[index].GetComponentsInChildren<T>(true));
            }

            if (matches.Count != 1)
            {
                result = null;
                failure =
                    $"Scene '{scene.name}' must contain exactly one {typeof(T).Name}; found {matches.Count}.";
                return false;
            }

            result = matches[0];
            failure = string.Empty;
            return true;
        }

        private void Fail(string failure)
        {
            IsInitialized = false;
            LastFailure = string.IsNullOrWhiteSpace(failure)
                ? "M08 vehicle audio playtest initialization failed."
                : failure;
            Debug.LogError(LastFailure, this);
        }
    }
}
