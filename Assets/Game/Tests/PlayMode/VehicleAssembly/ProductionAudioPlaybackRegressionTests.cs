using System.Collections;
using System.Linq;
using MSC.Audio;
using MSC.Audio.UnityFallback;
using MSC.Bootstrap;
using MSC.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.VehicleAssembly
{
    /// <summary>Real production composition, real preferred Wwise, real private
    /// clips and Unity sources. No recording backend is substituted.</summary>
    public sealed class ProductionAudioPlaybackRegressionTests
    {
        private float previousTimeScale;
        private GameObject probe;

        [UnityTest]
        public IEnumerator ProductionWwiseAndExplicitClipsPlayAcrossTwoSessions()
        {
            Assert.That(SystemInfo.graphicsDeviceType,
                Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null),
                "Run with a graphics device for the real Bootstrap/menu composition.");
            previousTimeScale = Time.timeScale;
            for (int session = 0; session < 2; session++)
            {
                yield return DestroySession();
                yield return SceneManager.LoadSceneAsync("Assets/Game/Bootstrap/Bootstrap.unity", LoadSceneMode.Single);
                yield return null;
                var installer = Object.FindFirstObjectByType<ProductionWorldStreamingInstaller>(FindObjectsInactive.Include);
                Assert.That(installer, Is.Not.Null);
                Assert.That(installer.TryBeginGameplayPreparation(out string preparationFailure), Is.True, preparationFailure);
                float deadline = Time.realtimeSinceStartup + 150f;
                while (!installer.IsGameplayPrepared && installer.IsGameplayPreparationRunning && Time.realtimeSinceStartup < deadline)
                    yield return null;
                Assert.That(installer.IsGameplayPrepared, Is.True, installer.LastGameplayPreparationFailure);
                Assert.That(installer.TryActivateGameplay(out string activationFailure), Is.True, activationFailure);
                Time.timeScale = 1f;
                var router = Object.FindFirstObjectByType<AudioBackendRouter>();
                for (int frame = 0; frame < 360 && router.Kind != AudioBackendKind.Wwise; frame++) yield return null;
                Assert.That(router.Kind, Is.EqualTo(AudioBackendKind.Wwise), router.CaptureSnapshot().LastFailure);
                Assert.That(router.CaptureSnapshot().LoadedBankCount, Is.EqualTo(6));
                Assert.That(router.CaptureSnapshot().MissingBanks, Is.Empty);
                Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                    .Count(listener => listener.isActiveAndEnabled), Is.EqualTo(1));

                probe = new GameObject("Audio regression probe");
                probe.transform.position = Camera.main.transform.position;
                var emitter = probe.AddComponent<AudioEmitterAuthoring>();
                emitter.Configure("audio.emitter.regression.production", router, probe.transform);
                var settings = new AudioSettingsState(1f, 1f, 1f, 1f, 1f, 1f,
                    AudioDynamicRangeMode.Balanced, false, false, false, false);
                router.ApplySettings(in settings);
                string[] libraries = { SatsumaEngineFeedbackPresenter.FallbackResourcesPath,
                    VehicleAssemblyAudioPresenter.FallbackOverrideResourcesPath,
                    ProductionWorldStreamingInstaller.LightingSwitchAudioResourcesPath };
                var selected = Resources.Load<UnityAudioEventLibrary>("Phase1UserSelectedAudio/Phase1UserSelectedAudioEventLibrary");
                foreach (string resource in libraries)
                {
                    var library = Resources.Load<UnityAudioEventLibrary>(resource);
                    Assert.That(library, Is.Not.Null, resource);
                    foreach (UnityAudioEventDefinition definition in library.Definitions)
                    {
                        var id = new AudioEventId(definition.EventId);
                        UnityAudioEventDefinition effective = selected != null && selected.TryResolve(id, out var replacement)
                            ? replacement : definition;
                        Assert.That(router.ResolveEventBackend(id), Is.TypeOf<UnityAudioBackend>(), id.Value);
                        var fallback = (UnityAudioBackend)router.ResolveEventBackend(id);
                        router.SetParameter(definition.VolumeParameterId.IsEmpty
                            ? SatsumaEngineAudioIds.ThrottleGain : definition.VolumeParameterId, 1f, emitter);
                        if (!definition.PitchParameterId.IsEmpty) router.SetParameter(definition.PitchParameterId, 1f, emitter);
                        var request = new AudioEventRequest(id, emitter, allowMultiple: false);
                        IAudioEventHandle handle = router.PostEvent(in request);
                        Assert.That(handle.IsValid, Is.True, id + ": " + router.CaptureSnapshot().LastFailure);
                        yield return null;
                        // Several existing traffic events intentionally share
                        // the selected engine clip. Count this explicit probe
                        // emitter's voice, not every car playing the same media.
                        AudioSource[] sources = fallback.GetComponentsInChildren<AudioSource>(true)
                            .Where(source => source.clip == effective.Clip && source.isPlaying &&
                                Vector3.SqrMagnitude(source.transform.position - probe.transform.position) < .000001f).ToArray();
                        Assert.That(sources, Has.Length.EqualTo(1), id.Value);
                        Assert.That(sources[0].volume, Is.GreaterThan(0f), id.Value);
                        Assert.That(sources[0].pitch, Is.GreaterThan(0f), id.Value);
                        Assert.That(effective.Clip.samples, Is.GreaterThan(0), id.Value);
                        Assert.That(effective.Clip.loadState, Is.EqualTo(AudioDataLoadState.Loaded), id.Value);
                        bool decodedPcmChecked = effective.Clip.loadType == AudioClipLoadType.DecompressOnLoad;
                        if (decodedPcmChecked)
                        {
                            float[] samples = new float[Mathf.Min(262144, effective.Clip.samples * effective.Clip.channels)];
                            Assert.That(effective.Clip.GetData(samples, 0), Is.True, id.Value);
                            Assert.That(samples.Any(sample => Mathf.Abs(sample) > 0.00001f), Is.True,
                                id + " must contain nonzero PCM; a handle alone is insufficient.");
                        }
                        // CompressedInMemory user media is decoded by Unity's
                        // audio thread. Do not mutate its import policy or call
                        // a zero-filled GetData buffer a passed PCM check.
                        // Actual listener output is checked by the device probe.
                        Debug.Log($"AUDIO_REPAIR_PRODUCTION_PLAYBACK session={session + 1} event={id.Value} " +
                            $"backend={fallback.BackendId} playing=1 volume={sources[0].volume:F4} " +
                            $"pitch={sources[0].pitch:F4} nonzeroPcmChecked={decodedPcmChecked} mixDb={effective.MixGainDb:F1} preferred={router.Kind}");
                        handle.Stop();
                    }
                }
                Assert.That(router.ResolveEventBackend(AudioProjectIds.Events.WeatherThunder).Kind,
                    Is.EqualTo(AudioBackendKind.Wwise), "Do not globally bypass official Wwise.");
                var nativeRequest = new AudioEventRequest(AudioProjectIds.Events.WeatherThunder, emitter,
                    volume01: 0.05f, allowMultiple: false);
                IAudioEventHandle nativeHandle = router.PostEvent(in nativeRequest);
                Assert.That(nativeHandle.IsValid, Is.True, router.CaptureSnapshot().LastFailure);
                yield return null;
                Assert.That(nativeHandle.IsPlaying, Is.True, "The real native Wwise route must remain operational.");
                Debug.Log($"AUDIO_REPAIR_NATIVE_WWISE_PLAYBACK session={session + 1} event={nativeHandle.EventId} playing=true banks=6");
                nativeHandle.Stop();
                router.StopAll();
                Object.Destroy(probe);
                probe = null;
                yield return null;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (probe != null) Object.Destroy(probe);
            yield return DestroySession();
            Time.timeScale = previousTimeScale;
        }

        private static IEnumerator DestroySession()
        {
            var root = GameCompositionRoot.ActiveRoot;
            if (root != null)
            {
                root.gameObject.SetActive(false);
                Object.Destroy(root.gameObject);
            }
            yield return null;
            yield return null;
        }
    }
}
