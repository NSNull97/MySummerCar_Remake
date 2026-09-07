#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSC.Audio;
using MSC.Audio.UnityFallback;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.AudioUnityFallback
{
    /// <summary>Actual Unity voices behind a production router, with an explicitly
    /// recording preferred backend. This proves routing/source state, not human
    /// audibility or Wwise's native output device.</summary>
    public sealed class AudioHybridRoutingPlayModeTests
    {
        private static readonly AudioEventId Supplemental = new("audio.event.hybrid.supplemental");
        private static readonly AudioEventId Override = new("audio.event.hybrid.override");
        private static readonly AudioEventId Ordinary = new("audio.event.hybrid.ordinary");
        private static readonly AudioEventId UserInterface = new("audio.event.hybrid.ui");
        private static readonly AudioParameterId Gain = new("audio.parameter.hybrid.gain");
        private static readonly AudioParameterId Pitch = new("audio.parameter.hybrid.pitch");
        private readonly List<Object> cleanup = new();
        private readonly List<Scene> cells = new();
        private GameObject backendObject;
        private UnityAudioBackend fallback;
        private HybridRecordingPrimaryBackend primary;
        private AudioBackendRouter router;
        private AudioClip clip;
        private AudioEmitterAuthoring emitter;
        private float previousTimeScale;

        [SetUp]
        public void SetUp()
        {
            previousTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            if (!Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                    .Any(listener => listener.isActiveAndEnabled))
                CreateObject("Hybrid fixture listener").AddComponent<AudioListener>();
            clip = AudioClip.Create("Project-owned hybrid routing sine", 48000 * 8, 1, 48000, false);
            cleanup.Add(clip);
            var samples = new float[clip.samples];
            for (int index = 0; index < samples.Length; index++)
                samples[index] = .05f * Mathf.Sin(2f * Mathf.PI * 330f * index / 48000f);
            Assert.That(clip.SetData(samples, 0), Is.True);

            backendObject = CreateObject("Real Unity fallback");
            fallback = backendObject.AddComponent<UnityAudioBackend>();
            fallback.ConfigureForAuthoring(Library(Definition(Ordinary), Definition(Override)));
            fallback.ConfigureSupplementalEventLibrariesForAuthoring(Library(
                Definition(Supplemental, parameters: true),
                Definition(UserInterface, category: UnityAudioCategory.UserInterface)));
            fallback.ConfigureOverrideEventLibrariesForAuthoring(Library(Definition(Override)));
            primary = CreateObject("Ready preferred backend").AddComponent<HybridRecordingPrimaryBackend>();
            primary.Supported.Add(Ordinary);
            primary.Supported.Add(Override);
            GameObject routerObject = CreateObject("Hybrid router");
            routerObject.SetActive(false);
            router = routerObject.AddComponent<AudioBackendRouter>();
            router.Configure(primary, fallback, scanLoadedScenes: false);
            routerObject.SetActive(true);
            router.ApplySettings(Settings());
            emitter = CreateEmitter("audio.emitter.hybrid.first");
            Assert.That(router.RegisterEmitter(emitter, out string failure), Is.True, failure);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = previousTimeScale;
            if (router != null) router.EndGameSession();
            foreach (Scene cell in cells)
                if (cell.IsValid() && cell.isLoaded) yield return SceneManager.UnloadSceneAsync(cell);
            cells.Clear();
            for (int index = cleanup.Count - 1; index >= 0; index--)
                if (cleanup[index] != null) Object.Destroy(cleanup[index]);
            cleanup.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator UserSelection_ReplacesExistingOverrideSupplementAndPrimaryBeforePreferredPosting()
        {
            AudioClip replacementClip = AudioClip.Create("Explicit user clip fixture", 24000, 1, 48000, false);
            cleanup.Add(replacementClip);
            UnityAudioEventDefinition Replacement(AudioEventId id)
            {
                var definition = Definition(id, parameters: true);
                definition.ConfigureForAuthoring(id.Value, replacementClip, UnityAudioCategory.Vehicle,
                    true, .5f, 1f, 1f, 1f, 40f);
                return definition;
            }
            var library = Library(Replacement(Override), Replacement(Supplemental), Replacement(Ordinary));
            Assert.That(fallback.TrySetReplacementEventLibrary(library, out string failure), Is.True, failure);
            Assert.That(fallback.TrySetReplacementEventLibrary(library, out failure), Is.True, failure);
            Assert.That(fallback.TrySetReplacementEventLibrary(Library(Definition(Override)), out failure), Is.False);
            Assert.That(router.TryGetEventDurationSeconds(Override, out float duration), Is.True);
            Assert.That(duration, Is.EqualTo(.5f).Within(.001f));
            foreach (AudioEventId id in new[] { Override, Supplemental, Ordinary })
            {
                Assert.That(router.ResolveEventBackend(id), Is.SameAs(fallback));
                Assert.That(Post(id).IsValid, Is.True);
            }
            yield return null;
            AudioSource[] selected = backendObject.GetComponentsInChildren<AudioSource>()
                .Where(source => source.clip == replacementClip).ToArray();
            Assert.That(selected, Has.Length.EqualTo(3));
            Assert.That(primary.Posts, Is.Empty);
            Assert.That(fallback.ActiveGenericVoiceCount, Is.EqualTo(3));
            router.StopAll();
            yield return null;
            Assert.That(selected.All(source => !source.isPlaying), Is.True);
        }

        [UnityTest, Category("RequiresRealtimeAudioDevice")]
        public IEnumerator UserSelection_CalibrationIsPresentInActualUnityPcmReadback()
        {
            if (Application.isBatchMode)
                Assert.Ignore("Realtime PCM capture requires the interactive Test Runner: this Unity 6000.6 batch backend returns no device PCM or AudioRenderer samples.");
            var definition = Definition(Override);
            definition.ConfigureCalibrationForAuthoring(6f);
            Assert.That(fallback.TrySetReplacementEventLibrary(Library(definition), out string failure), Is.True, failure);
            router.ApplySettings(Settings(master: 1f));
            var data = new float[2048];
            float Peak()
            {
                // Source readback precedes OnAudioFilterRead overflow on this
                // Unity backend. The listener proves the rendered correction.
                AudioListener.GetOutputData(data, 0);
                float peak = 0f;
                foreach (float sample in data) peak = Mathf.Max(peak, Mathf.Abs(sample));
                return peak;
            }
            IAudioEventHandle ordinary = Post(Supplemental);
            yield return new WaitForSecondsRealtime(.2f);
            AudioSource source = Voices().Single();
            float baseline = Peak();
            Assert.That(baseline, Is.GreaterThan(.001f), "A real unmuted audio device must produce PCM for this check.");
            ordinary.Stop(); Post(Override);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Voices().Single(), Is.SameAs(source));
            float corrected = Peak();
            TestContext.WriteLine("UNITY_AUDIO_CALIBRATION_PCM baselinePeak=" + baseline + " correctedPeak=" + corrected);
            Assert.That(corrected / baseline, Is.EqualTo(1.9952623f).Within(.12f));
            router.ApplySettings(Settings(master: 0f));
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Peak(), Is.LessThan(.00001f), "Mute must silence calibrated PCM too.");
        }

        [UnityTest]
        public IEnumerator UserSelection_CalibrationReachesRealVoiceOverflowMuteAndPooledReset()
        {
            var definition = Definition(Override, parameters: true);
            definition.ConfigureCalibrationForAuthoring(6f);
            Assert.That(fallback.TrySetReplacementEventLibrary(Library(definition), out string failure), Is.True, failure);
            IAudioEventHandle corrected = Post(Override);
            yield return null;
            AudioSource source = Voices().Single();
            var filter = source.GetComponent<UnityAudioCalibrationFilter>();
            Assert.That(source.volume, Is.EqualTo(.5f * .2f * 1.5848932f * 1.9952623f).Within(.00001f));
            Assert.That(filter.enabled, Is.False, "Use ordinary linear gain while the source has headroom.");
            router.ApplySettings(Settings(master: 1f));
            yield return null;
            Assert.That(source.volume, Is.EqualTo(1f));
            Assert.That(filter.enabled, Is.True);
            Assert.That(filter.OverflowGain, Is.EqualTo(.5f * 1.5848932f * 1.9952623f).Within(.00001f));
            // Settings and scoped controls must still silence a calibrated voice.
            Assert.That(router.SetParameter(Gain, 0f, emitter), Is.True);
            yield return null;
            Assert.That(source.volume, Is.Zero);
            Assert.That(filter.enabled, Is.False);
            router.SetParameter(Gain, 1f, emitter);
            router.ApplySettings(Settings(master: 0f));
            yield return null;
            Assert.That(source.volume, Is.Zero);
            corrected.Stop();
            router.ApplySettings(Settings());
            Post(Supplemental);
            yield return null;
            Assert.That(Voices().Single(), Is.SameAs(source));
            Assert.That(source.volume, Is.EqualTo(.5f * .2f * 1.5848932f).Within(.00001f));
            Assert.That(filter.enabled, Is.False);
            Assert.That(filter.OverflowGain, Is.EqualTo(1f));
            Assert.That(primary.Posts, Is.Empty);
        }

        [UnityTest]
        public IEnumerator EventMixAndClipCorrectionReachRealSourceWithoutChangingOldEventsOrMutes()
        {
            var definition = Definition(Override, parameters: true);
            definition.ConfigureCalibrationForAuthoring(3f);
            definition.ConfigureMixGainForAuthoring(6f);
            Assert.That(fallback.TrySetReplacementEventLibrary(Library(definition), out string failure), Is.True, failure);
            IAudioEventHandle changed = Post(Override);
            yield return null;
            AudioSource source = Voices().Single();
            var filter = source.GetComponent<UnityAudioCalibrationFilter>();
            float gains = Mathf.Pow(10f, 9f / 20f);
            Assert.That(source.volume, Is.EqualTo(.5f * .2f * 1.5848932f * gains).Within(.00001f));
            Assert.That(filter.enabled, Is.False);
            router.ApplySettings(Settings(master: 1f));
            yield return null;
            Assert.That(source.volume, Is.EqualTo(1f));
            Assert.That(filter.OverflowGain, Is.EqualTo(.5f * 1.5848932f * gains).Within(.00001f));
            router.SetParameter(Gain, 0f, emitter);
            yield return null;
            Assert.That(source.volume, Is.Zero);
            Assert.That(filter.enabled, Is.False);
            router.SetParameter(Gain, 1f, emitter);
            foreach (var muted in new[] { Settings(master: 0f), Settings(vehicle: 0f) })
            {
                router.ApplySettings(muted);
                yield return null;
                Assert.That(source.volume, Is.Zero);
                Assert.That(filter.OverflowGain, Is.EqualTo(1f));
            }
            changed.Stop();
            router.ApplySettings(Settings());
            Post(Supplemental);
            yield return null;
            Assert.That(Voices().Single(), Is.SameAs(source));
            Assert.That(source.volume, Is.EqualTo(.5f * .2f * 1.5848932f).Within(.00001f));
            Assert.That(filter.enabled, Is.False);
            Assert.That(primary.Posts, Is.Empty);
        }

        [UnityTest]
        public IEnumerator UserSelection_RolloffIsPerEventAndResetsWhenVoiceIsReused()
        {
            var definition = Definition(Override);
            definition.ConfigureDistanceRolloffForAuthoring(UnityAudioDistanceRolloff.Logarithmic);
            Assert.That(fallback.TrySetReplacementEventLibrary(Library(definition), out string failure), Is.True, failure);
            IAudioEventHandle logarithmic = Post(Override);
            yield return null;
            AudioSource first = Voices().Single();
            Assert.That(first.rolloffMode, Is.EqualTo(AudioRolloffMode.Logarithmic));
            logarithmic.Stop();
            IAudioEventHandle linear = Post(Supplemental);
            yield return null;
            AudioSource second = Voices().Single();
            Assert.That(second, Is.SameAs(first), "Exercise a reused voice, not merely a new default source.");
            Assert.That(second.rolloffMode, Is.EqualTo(AudioRolloffMode.Linear));
            Assert.That(linear.IsValid, Is.True);
        }

        [UnityTest]
        public IEnumerator UserSelection_InvalidMappingsAndMidPlaybackSelectionLeaveExistingRoutesUntouched()
        {
            Assert.That(fallback.TrySetReplacementEventLibrary(null, out _), Is.False);
            var invalid = ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
            cleanup.Add(invalid);
            invalid.ConfigureForAuthoring(Definition(Override), Definition(Override));
            Assert.That(fallback.TrySetReplacementEventLibrary(invalid, out _), Is.False);
            IAudioEventHandle handle = Post(Supplemental);
            Assert.That(fallback.TrySetReplacementEventLibrary(Library(Definition(Ordinary)), out _), Is.False);
            Assert.That(router.ResolveEventBackend(Ordinary), Is.SameAs(primary));
            yield return null;
            Assert.That(handle.IsPlaying, Is.True);
            Assert.That(fallback.ActiveGenericVoiceCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ExplicitSupplementalAndOverride_PlayOnceInUnityBeforePreferredPosting()
        {
            Assert.That(router.IsUsingFallback, Is.False);
            Assert.That(router.ResolveEventBackend(Supplemental), Is.SameAs(fallback));
            Assert.That(router.ResolveEventBackend(Override), Is.SameAs(fallback));
            IAudioEventHandle first = Post(Supplemental);
            IAudioEventHandle second = Post(Override);
            Assert.That(Post(Supplemental).HandleId, Is.EqualTo(first.HandleId));
            yield return null;
            Assert.That(first.IsPlaying && second.IsPlaying, Is.True);
            Assert.That(fallback.ActiveGenericVoiceCount, Is.EqualTo(2));
            Assert.That(Voices(), Has.Length.EqualTo(2));
            Assert.That(Voices().All(source => source.clip == clip && source.volume > 0f), Is.True);
            Assert.That(primary.Posts, Is.Empty, "An override must be selected before posting, not after Wwise rejects it.");
        }

        [UnityTest]
        public IEnumerator BaseLibraryDoesNotHijackPreferredEvents_UnknownNeverUsesUnrelatedClip()
        {
            Assert.That(router.ResolveEventBackend(Ordinary), Is.SameAs(primary));
            Assert.That(Post(Ordinary).IsValid, Is.True);
            Assert.That(primary.Posts, Is.EqualTo(new[] { Ordinary }));
            Assert.That(fallback.ActiveGenericVoiceCount, Is.Zero);
            var missing = new AudioEventId("audio.event.hybrid.missing");
            LogAssert.Expect(LogType.Warning,
                "Audio event audio.event.hybrid.missing was rejected by audio.test.hybrid.primary: Missing test primary mapping.");
            Assert.That(Post(missing).IsValid, Is.False);
            Assert.That(Post(missing).IsValid, Is.False, "Repeated rejection must not fabricate a fallback sound.");
            yield return null;
            Assert.That(fallback.ActiveGenericVoiceCount, Is.Zero);
            Assert.That(Voices(), Is.Empty);
            Assert.That(primary.Posts.Count(value => value == missing), Is.EqualTo(2));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator EmitterScopedGainPitch_ReachOnlyExplicitParameterOwnerAndRemainIndependent()
        {
            AudioEmitterAuthoring secondEmitter = CreateEmitter("audio.emitter.hybrid.second");
            secondEmitter.transform.position = Vector3.right * 10f;
            Assert.That(router.RegisterEmitter(secondEmitter, out string failure), Is.True, failure);
            Assert.That(router.SetParameter(Gain, .25f, emitter), Is.True);
            Assert.That(router.SetParameter(Pitch, .7f, emitter), Is.True);
            Assert.That(router.SetParameter(Gain, .75f, secondEmitter), Is.True);
            Assert.That(router.SetParameter(Pitch, 1.4f, secondEmitter), Is.True);
            Post(Supplemental);
            Post(Supplemental, secondEmitter);
            yield return null;
            AudioSource first = Voices().Single(source => source.transform.position == emitter.transform.position);
            AudioSource second = Voices().Single(source => source.transform.position == secondEmitter.transform.position);
            Assert.That(second.volume / first.volume, Is.EqualTo(3f).Within(.001f));
            Assert.That(first.pitch, Is.EqualTo(.7f).Within(.001f));
            Assert.That(second.pitch, Is.EqualTo(1.4f).Within(.001f));
            Assert.That(primary.ParameterIds, Has.No.Member(Gain));
            Assert.That(primary.ParameterIds, Has.No.Member(Pitch));
            Assert.That(router.SetParameter(AudioProjectIds.Parameters.VehicleRpm, 2000f, emitter), Is.True);
            Assert.That(primary.ParameterIds, Has.Member(AudioProjectIds.Parameters.VehicleRpm));
        }

        [UnityTest]
        public IEnumerator SettingsAndListenerReachBothRoutes_MasterVehicleAndFocusMuteRealSource()
        {
            Post(Supplemental);
            yield return null;
            AudioSource source = Voices().Single();
            float baseline = source.volume;
            Assert.That(baseline, Is.GreaterThan(0f));
            router.ApplySettings(Settings(master: .1f, vehicle: .5f));
            yield return null;
            Assert.That(source.volume / baseline, Is.EqualTo(.25f).Within(.001f));
            Assert.That(primary.Settings.Master01, Is.EqualTo(.1f));
            Assert.That(primary.Settings.Vehicle01, Is.EqualTo(.5f));
            router.ApplySettings(Settings(vehicle: 0f));
            yield return null;
            Assert.That(source.volume, Is.Zero);
            router.ApplySettings(Settings(master: 0f));
            yield return null;
            Assert.That(source.volume, Is.Zero);

            router.ApplySettings(Settings(muteOnFocusLoss: true));
            var context = new AudioListenerContext("audio.listener.hybrid", new Vector3(3f, 4f, 5f),
                Vector3.forward, Vector3.up, AudioEnvironmentContext.Exterior, hasFocus: false);
            router.SetListenerContext(in context);
            yield return null;
            Assert.That(source.volume, Is.Zero);
            Assert.That(primary.Listener.StableListenerId, Is.EqualTo(context.StableListenerId));
            Assert.That(fallback.CaptureSnapshot().Listener.WorldPosition, Is.EqualTo(context.WorldPosition));
            router.ApplySettings(Settings(muteOnFocusLoss: false));
            yield return null;
            Assert.That(source.volume, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator StopAllAndEndSession_StopBothRoutesAndRemoveRegistrations()
        {
            IAudioEventHandle unityVoice = Post(Supplemental);
            IAudioEventHandle primaryVoice = Post(Ordinary);
            router.StopAll();
            Assert.That(unityVoice.IsPlaying || primaryVoice.IsPlaying, Is.False);
            unityVoice = Post(Supplemental);
            primaryVoice = Post(Ordinary);
            router.EndGameSession();
            router.EndGameSession();
            yield return null;
            Assert.That(unityVoice.IsPlaying || primaryVoice.IsPlaying, Is.False);
            Assert.That(router.RegisteredEmitterCount, Is.Zero);
            Assert.That(primary.Emitters, Is.Empty);
            Assert.That(fallback.CaptureSnapshot().RegisteredEmitterCount, Is.Zero);
            Assert.That(fallback.ActiveGenericVoiceCount, Is.Zero);
            Assert.That(Post(Supplemental).IsValid, Is.False);
        }

        [UnityTest]
        public IEnumerator CellUnload_CleansBothRoutesAndPreservesAnotherCellsVoices()
        {
            Scene cell = SceneManager.CreateScene("Hybrid audio cell " + Guid.NewGuid().ToString("N"));
            cells.Add(cell);
            AudioEmitterAuthoring cellEmitter = CreateEmitter("audio.emitter.hybrid.cell");
            SceneManager.MoveGameObjectToScene(cellEmitter.gameObject, cell);
            Assert.That(router.RegisterEmitter(cellEmitter, out string failure), Is.True, failure);
            IAudioEventHandle cellUnity = Post(Supplemental, cellEmitter);
            IAudioEventHandle cellPrimary = Post(Ordinary, cellEmitter);
            IAudioEventHandle persistentUnity = Post(Supplemental);
            IAudioEventHandle persistentPrimary = Post(Ordinary);
            yield return SceneManager.UnloadSceneAsync(cell);
            yield return null;
            Assert.That(cellUnity.IsPlaying || cellPrimary.IsPlaying, Is.False);
            Assert.That(persistentUnity.IsPlaying && persistentPrimary.IsPlaying, Is.True);
            Assert.That(router.RegisteredEmitterCount, Is.EqualTo(1));
            Assert.That(primary.Emitters, Has.Count.EqualTo(1));
            Assert.That(fallback.CaptureSnapshot().RegisteredEmitterCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PreferredBecomesReady_ExplicitRouteRemainsUnityAndBaseReturnsToPreferred()
        {
            primary.Ready = false;
            Assert.That(router.IsUsingFallback, Is.True);
            IAudioEventHandle oldBase = Post(Ordinary);
            Assert.That(oldBase.IsValid, Is.True);
            Assert.That(primary.Posts, Is.Empty);
            primary.Ready = true;
            Assert.That(router.ResolveEventBackend(Supplemental), Is.SameAs(fallback));
            Assert.That(router.ResolveEventBackend(Ordinary), Is.SameAs(primary));
            Assert.That(oldBase.IsPlaying, Is.False);
            Assert.That(Post(Supplemental).IsValid, Is.True);
            Assert.That(Post(Ordinary).IsValid, Is.True);
            yield return null;
            Assert.That(fallback.ActiveGenericVoiceCount, Is.EqualTo(1));
            Assert.That(primary.Posts, Is.EqualTo(new[] { Ordinary }));
            Assert.That(primary.DuplicateRegistrationCount, Is.Zero);
            Assert.That(fallback.CaptureSnapshot().RegisteredEmitterCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PreferredReadinessTransition_ReplaysOnceSetGlobalAndEmitterControls()
        {
            primary.Ready = false;
            Assert.That(router.IsUsingFallback, Is.True);
            AudioParameterId parameter = AudioProjectIds.Parameters.VehicleDamage;
            AudioSwitchId switchGroup = AudioProjectIds.Switches.WeatherPrecipitationGroup;
            AudioSwitchId switchValue = AudioProjectIds.Switches.WeatherPrecipitationRain;
            AudioStateId stateGroup = AudioProjectIds.States.EnvironmentGroup;
            AudioStateId stateValue = AudioProjectIds.States.EnvironmentInterior;
            Assert.That(router.SetParameter(parameter, .63f), Is.True);
            Assert.That(router.SetSwitch(switchGroup, switchValue), Is.True);
            Assert.That(router.SetState(stateGroup, stateValue), Is.True);
            Assert.That(router.SetParameter(AudioProjectIds.Parameters.VehicleRpm, 2300f, emitter), Is.True);
            Assert.That(router.SetSwitch(AudioProjectIds.Switches.SurfaceGroup,
                AudioProjectIds.Switches.SurfaceGravel, emitter), Is.True);
            Assert.That(primary.ParameterIds, Is.Empty);
            Assert.That(primary.SwitchValues, Is.Empty);
            Assert.That(primary.StateValues, Is.Empty);

            primary.Ready = true;
            Assert.That(router.ResolveEventBackend(Ordinary), Is.SameAs(primary));
            yield return null;
            Assert.That(primary.ParameterValues[(parameter, string.Empty)], Is.EqualTo(.63f));
            Assert.That(primary.SwitchValues[(switchGroup, string.Empty)], Is.EqualTo(switchValue));
            Assert.That(primary.StateValues[stateGroup], Is.EqualTo(stateValue));
            Assert.That(primary.ParameterValues[(AudioProjectIds.Parameters.VehicleRpm, emitter.StableId)],
                Is.EqualTo(2300f));
            Assert.That(primary.SwitchValues[(AudioProjectIds.Switches.SurfaceGroup, emitter.StableId)],
                Is.EqualTo(AudioProjectIds.Switches.SurfaceGravel));

            // The caller never resends these once-set values. A second ready
            // transition must replay them again, not rely on stale engine state.
            primary.Ready = false;
            Assert.That(router.IsUsingFallback, Is.True);
            primary.ParameterValues.Clear();
            primary.SwitchValues.Clear();
            primary.StateValues.Clear();
            primary.Ready = true;
            Assert.That(router.IsUsingFallback, Is.False);
            Assert.That(primary.ParameterValues[(parameter, string.Empty)], Is.EqualTo(.63f));
            Assert.That(primary.SwitchValues[(switchGroup, string.Empty)], Is.EqualTo(switchValue));
            Assert.That(primary.StateValues[stateGroup], Is.EqualTo(stateValue));
        }

        [UnityTest]
        public IEnumerator LateSecondaryReadiness_RegistrationRebindsOldEmitterAndAddsNewExactlyOnce()
        {
            fallback.enabled = false;
            Assert.That(router.ResolveEventBackend(Ordinary), Is.SameAs(primary));
            Assert.That(fallback.CaptureSnapshot().RegisteredEmitterCount, Is.Zero);
            AudioEmitterAuthoring newEmitter = CreateEmitter("audio.emitter.hybrid.late_secondary");

            // Do not resolve a route between enabling and RegisterEmitter: this
            // is the transition which formerly registered the new ID twice.
            fallback.enabled = true;
            Assert.That(router.RegisterEmitter(newEmitter, out string failure), Is.True, failure);
            Assert.That(router.CaptureSnapshot().LastFailure, Is.Empty);
            Assert.That(primary.DuplicateRegistrationCount, Is.Zero);
            Assert.That(primary.Emitters, Has.Count.EqualTo(2));
            Assert.That(fallback.CaptureSnapshot().RegisteredEmitterCount, Is.EqualTo(2));
            Assert.That(Post(Supplemental, newEmitter).IsValid, Is.True);
            Assert.That(Post(Supplemental).IsValid, Is.True);
            yield return null;
            Assert.That(fallback.ActiveGenericVoiceCount, Is.EqualTo(2));
            Assert.That(primary.Posts, Is.Empty);
        }

        [UnityTest]
        public IEnumerator PauseRetainsGameplayHandleAndSampleCursor_UiContinues_ResumeDoesNotRestartClip()
        {
            IAudioEventHandle gameplay = Post(Supplemental);
            IAudioEventHandle ui = Post(UserInterface);
            yield return null;
            AudioSource gameplaySource = Voices().First();
            AudioSource uiSource = Voices().Last();
            // A nonzero cursor makes a restart-to-zero detectable even on a
            // batch audio device whose hardware clock advances in coarse ticks.
            gameplaySource.timeSamples = 96000;
            Time.timeScale = 0f;
            yield return null;
            yield return null;
            int pausedCursor = gameplaySource.timeSamples;
            Assert.That(gameplay.IsValid && gameplay.IsPlaying, Is.True,
                "Paused handles must retain ownership so NPC music cannot repost from zero.");
            Assert.That(gameplaySource.isPlaying, Is.False);
            Assert.That(ui.IsPlaying, Is.True);
            Assert.That(uiSource.isPlaying, Is.True, "UI audio is not part of the gameplay pause.");
            yield return new WaitForSecondsRealtime(.08f);
            Assert.That(gameplaySource.timeSamples, Is.EqualTo(pausedCursor));
            Assert.That(gameplaySource.clip, Is.SameAs(clip));
            Time.timeScale = 1f;
            yield return null;
            yield return null;
            Assert.That(gameplaySource.timeSamples, Is.GreaterThanOrEqualTo(pausedCursor));
            Assert.That(gameplaySource.isPlaying, Is.True);
            Assert.That(Post(Supplemental).HandleId, Is.EqualTo(gameplay.HandleId));
            Assert.That(fallback.ActiveGenericVoiceCount, Is.EqualTo(2));
            Assert.That(primary.Posts, Is.Empty);
        }

        private IAudioEventHandle Post(AudioEventId id, IAudioEmitter owner = null) =>
            router.PostEvent(new AudioEventRequest(id, owner ?? emitter, allowMultiple: false));

        private AudioSource[] Voices() => backendObject.GetComponentsInChildren<AudioSource>(true)
            .Where(source => source.clip == clip).ToArray();

        private static AudioSettingsState Settings(float master = .2f, float vehicle = 1f,
            bool muteOnFocusLoss = false) => new(master, vehicle, 1f, 1f, 1f, 1f,
                AudioDynamicRangeMode.Balanced, muteOnFocusLoss, false, false, false);

        private UnityAudioEventDefinition Definition(AudioEventId id, bool parameters = false,
            UnityAudioCategory category = UnityAudioCategory.Vehicle)
        {
            var definition = new UnityAudioEventDefinition();
            definition.ConfigureForAuthoring(id.Value, clip, category, true, .5f, 1f, 0f, 1f, 40f);
            if (parameters) definition.ConfigureParameterBindingsForAuthoring(Gain, Pitch);
            return definition;
        }

        private UnityAudioEventLibrary Library(params UnityAudioEventDefinition[] definitions)
        {
            var library = ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
            library.ConfigureForAuthoring(definitions);
            cleanup.Add(library);
            Assert.That(library.Validate(out string[] failures), Is.True, string.Join("; ", failures));
            return library;
        }

        private AudioEmitterAuthoring CreateEmitter(string id)
        {
            var authored = CreateObject(id).AddComponent<AudioEmitterAuthoring>();
            authored.Configure(id);
            return authored;
        }

        private GameObject CreateObject(string name)
        {
            var created = new GameObject(name);
            cleanup.Add(created);
            return created;
        }
    }

    public sealed class HybridRecordingPrimaryBackend : MonoBehaviour, IAudioBackend
    {
        public readonly HashSet<AudioEventId> Supported = new();
        public readonly HashSet<IAudioEmitter> Emitters = new();
        public readonly List<AudioEventId> Posts = new();
        public readonly List<AudioParameterId> ParameterIds = new();
        public readonly Dictionary<(AudioParameterId, string), float> ParameterValues = new();
        public readonly Dictionary<(AudioSwitchId, string), AudioSwitchId> SwitchValues = new();
        public readonly Dictionary<AudioStateId, AudioStateId> StateValues = new();
        private readonly List<HybridHandle> handles = new();
        private string lastOperationalFailure = string.Empty;
        public bool Ready = true;
        public int DuplicateRegistrationCount { get; private set; }
        public AudioListenerContext Listener { get; private set; }
        public AudioSettingsState Settings { get; private set; }
        public string BackendId => "audio.test.hybrid.primary";
        public AudioBackendKind Kind => AudioBackendKind.Wwise;
        public bool IsReady => Ready && isActiveAndEnabled;
        public string FailureReason => IsReady ? string.Empty : "Test primary unavailable.";
        public bool RegisterEmitter(IAudioEmitter value, out string failure)
        {
            bool added = Emitters.Add(value);
            if (!added) DuplicateRegistrationCount++;
            failure = added ? string.Empty : "Duplicate test emitter.";
            return added;
        }

        public bool UnregisterEmitter(IAudioEmitter value)
        {
            foreach (HybridHandle handle in handles)
                if (ReferenceEquals(handle.Emitter, value)) handle.Stop();
            return Emitters.Remove(value);
        }

        public IAudioEventHandle PostEvent(in AudioEventRequest request)
        {
            Posts.Add(request.EventId);
            if (!IsReady || !Supported.Contains(request.EventId))
            {
                lastOperationalFailure = "Missing test primary mapping.";
                return AudioEventHandles.Invalid;
            }
            lastOperationalFailure = string.Empty;
            var handle = new HybridHandle(request.EventId, request.Emitter, (ulong)handles.Count + 1);
            handles.Add(handle);
            return handle;
        }

        public bool SetParameter(AudioParameterId id, float value, IAudioEmitter emitter = null)
        {
            ParameterIds.Add(id);
            ParameterValues[(id, emitter?.StableId ?? string.Empty)] = value;
            return true;
        }
        public bool SetSwitch(AudioSwitchId group, AudioSwitchId value, IAudioEmitter emitter = null)
        { SwitchValues[(group, emitter?.StableId ?? string.Empty)] = value; return true; }
        public bool SetState(AudioStateId group, AudioStateId value)
        { StateValues[group] = value; return true; }
        public void SetListenerContext(in AudioListenerContext value) => Listener = value;
        public void ApplySettings(in AudioSettingsState value) => Settings = value;
        public void StopAll(float fadeSeconds = 0f)
        { foreach (HybridHandle handle in handles) handle.Stop(fadeSeconds); }
        public AudioRuntimeSnapshot CaptureSnapshot() => new(BackendId, Kind, IsReady, false,
            Emitters.Count, handles.Count(handle => handle.IsPlaying), 0, Array.Empty<string>(), Listener,
            lastOperationalFailure);

        private sealed class HybridHandle : IAudioEventHandle
        {
            public HybridHandle(AudioEventId id, IAudioEmitter emitter, ulong handleId)
            { EventId = id; Emitter = emitter; HandleId = handleId; }
            public ulong HandleId { get; }
            public AudioEventId EventId { get; }
            public IAudioEmitter Emitter { get; }
            public bool IsValid => IsPlaying;
            public bool IsPlaying { get; private set; } = true;
            public void Stop(float fadeSeconds = 0f) => IsPlaying = false;
            public void Dispose() => Stop();
        }
    }
}
#endif
