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
    public sealed class UnityAudioBackendPlayModeTests
    {
        private GameObject backendObject;
        private UnityAudioBackend backend;
        private GameObject emitterObject;
        private UnityAudioEventLibrary runtimeLibrary;
        private UnityAudioEventLibrary supplementalLibrary;
        private UnityAudioEventLibrary overrideLibrary;
        private AudioClip runtimeClip;
        private AudioClip overrideClip;

        [SetUp]
        public void SetUp()
        {
            backendObject = new GameObject("UnityAudioBackend_PlayModeTest");
            backend = backendObject.AddComponent<UnityAudioBackend>();
        }

        [TearDown]
        public void TearDown()
        {
            if (emitterObject != null)
            {
                Object.DestroyImmediate(emitterObject);
            }

            if (backendObject != null)
            {
                Object.DestroyImmediate(backendObject);
            }

            if (runtimeLibrary != null)
            {
                Object.DestroyImmediate(runtimeLibrary);
            }

            if (supplementalLibrary != null)
            {
                Object.DestroyImmediate(supplementalLibrary);
            }

            if (overrideLibrary != null)
            {
                Object.DestroyImmediate(overrideLibrary);
            }

            if (runtimeClip != null)
            {
                Object.DestroyImmediate(runtimeClip);
            }

            if (overrideClip != null)
            {
                Object.DestroyImmediate(overrideClip);
            }
        }

        [Test]
        public void Backend_IsOperationalWithoutEventLibraryOrDonorClips()
        {
            Assert.That(backend.IsReady, Is.True);
            Assert.That(backend.FailureReason, Is.Empty);
            Assert.That(backend.BackendId, Is.EqualTo("unity.fallback"));
            Assert.That(backend.Kind, Is.EqualTo(AudioBackendKind.Unity));

            AudioRuntimeSnapshot snapshot = backend.CaptureSnapshot();
            Assert.That(snapshot.IsReady, Is.True);
            Assert.That(snapshot.IsFallback, Is.True);
            Assert.That(snapshot.LoadedBankCount, Is.Zero);
            Assert.That(snapshot.RegisteredEmitterCount, Is.Zero);
        }

        [Test]
        public void RegisterEmitter_RejectsDuplicateAndTracksLifecycle()
        {
            emitterObject = new GameObject("Emitter_PlayModeTest");
            var emitter = new TestEmitter("audio.emitter.play_mode", emitterObject.transform);

            Assert.That(backend.RegisterEmitter(emitter, out string firstFailure), Is.True);
            Assert.That(firstFailure, Is.Empty);
            Assert.That(backend.CaptureSnapshot().RegisteredEmitterCount, Is.EqualTo(1));

            Assert.That(backend.RegisterEmitter(emitter, out string duplicateFailure), Is.False);
            StringAssert.Contains("already registered", duplicateFailure);
            Assert.That(backend.CaptureSnapshot().RegisteredEmitterCount, Is.EqualTo(1));

            Assert.That(backend.UnregisterEmitter(emitter), Is.True);
            Assert.That(backend.UnregisterEmitter(emitter), Is.False);
            Assert.That(backend.CaptureSnapshot().RegisteredEmitterCount, Is.Zero);
        }

        [Test]
        public void DestroyedEmitter_IsRemovedWithoutWaitingForSceneUnload()
        {
            emitterObject = new GameObject("DestroyedEmitter_PlayModeTest");
            AudioEmitterAuthoring emitter =
                emitterObject.AddComponent<AudioEmitterAuthoring>();
            emitter.Configure("audio.emitter.unity_fallback_destroyed_test");

            Assert.That(backend.RegisterEmitter(emitter, out string failure), Is.True, failure);
            Assert.That(backend.CaptureSnapshot().RegisteredEmitterCount, Is.EqualTo(1));

            Object.DestroyImmediate(emitterObject);

            Assert.That(backend.CaptureSnapshot().RegisteredEmitterCount, Is.Zero);
        }

        [Test]
        public void MissingMapping_ReturnsInvalidHandleWithoutDisablingFallback()
        {
            const string warning =
                "Unity fallback event library is not assigned; event audio.event.test was not played.";
            LogAssert.Expect(LogType.Warning, warning);

            IAudioEventHandle handle = backend.PostEvent(
                new AudioEventRequest(new AudioEventId("audio.event.test")));

            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.IsValid, Is.False);
            Assert.That(handle.IsPlaying, Is.False);
            Assert.That(backend.IsReady, Is.True);
            Assert.That(backend.CaptureSnapshot().LastFailure, Is.EqualTo(warning));
        }

        [Test]
        public void MappedLoop_UsesPooledVoiceAndHandleStopsIt()
        {
            runtimeClip = AudioClip.Create(
                "ProjectOwnedFallback_Test",
                lengthSamples: 4800,
                channels: 1,
                frequency: 48000,
                stream: false);
            var definition = new UnityAudioEventDefinition();
            SetPrivateField(definition, "eventId", "audio.event.loop_test");
            SetPrivateField(definition, "clip", runtimeClip);
            SetPrivateField(definition, "loop", true);
            SetPrivateField(definition, "spatialBlend", 0f);

            runtimeLibrary = ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
            SetPrivateField(
                runtimeLibrary,
                "events",
                new[] { definition });
            SetPrivateField(backend, "eventLibrary", runtimeLibrary);

            IAudioEventHandle handle = backend.PostEvent(
                new AudioEventRequest(new AudioEventId("audio.event.loop_test")));

            Assert.That(handle.IsValid, Is.True);
            Assert.That(handle.IsPlaying, Is.True);
            Assert.That(backend.ActiveGenericVoiceCount, Is.EqualTo(1));

            handle.Stop();
            Assert.That(handle.IsValid, Is.False);
            Assert.That(handle.IsPlaying, Is.False);
            Assert.That(backend.ActiveGenericVoiceCount, Is.Zero);
        }

        [Test]
        public void SupplementalLibrary_ResolvesPrivatePhase1Event()
        {
            runtimeClip = AudioClip.Create(
                "TemporaryDirectImport_NpcVoice_Test",
                lengthSamples: 4800,
                channels: 1,
                frequency: 48000,
                stream: false);
            var definition = new UnityAudioEventDefinition();
            SetPrivateField(definition, "eventId", "audio.npc.fixture.voice");
            SetPrivateField(definition, "clip", runtimeClip);
            SetPrivateField(definition, "loop", false);
            SetPrivateField(definition, "spatialBlend", 0f);

            supplementalLibrary =
                ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
            SetPrivateField(
                supplementalLibrary,
                "events",
                new[] { definition });
            SetPrivateField(
                backend,
                "supplementalEventLibraries",
                new[] { supplementalLibrary });

            IAudioEventHandle handle = backend.PostEvent(
                new AudioEventRequest(
                    new AudioEventId("audio.npc.fixture.voice")));

            Assert.That(handle.IsValid, Is.True);
            Assert.That(handle.IsPlaying, Is.True);
            backend.StopAll();
            Assert.That(handle.IsValid, Is.False);
        }

        [Test]
        public void OverrideLibrary_ReplacesPrimaryPlaceholderForStableEventId()
        {
            runtimeClip = AudioClip.Create(
                "GenericDoorLikePlaceholder_Test",
                4800,
                1,
                48000,
                false);
            overrideClip = AudioClip.Create(
                "TemporaryDirectImport_Assemble_Test",
                4800,
                1,
                48000,
                false);
            UnityAudioEventDefinition primaryDefinition = CreateDefinition(
                AudioProjectIds.Events.InteractionPartInstall.Value,
                runtimeClip);
            UnityAudioEventDefinition overrideDefinition = CreateDefinition(
                AudioProjectIds.Events.InteractionPartInstall.Value,
                overrideClip);

            runtimeLibrary =
                ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
            SetPrivateField(
                runtimeLibrary,
                "events",
                new[] { primaryDefinition });
            overrideLibrary =
                ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
            SetPrivateField(
                overrideLibrary,
                "events",
                new[] { overrideDefinition });
            SetPrivateField(backend, "eventLibrary", runtimeLibrary);
            SetPrivateField(
                backend,
                "overrideEventLibraries",
                new[] { overrideLibrary });

            IAudioEventHandle handle = backend.PostEvent(
                new AudioEventRequest(
                    AudioProjectIds.Events.InteractionPartInstall));

            Assert.That(handle.IsValid, Is.True);
            AudioSource selected = backendObject
                .GetComponentsInChildren<AudioSource>(true)
                .Single(source => source.clip == overrideClip);
            Assert.That(selected.clip, Is.SameAs(overrideClip));
            Assert.That(
                backendObject.GetComponentsInChildren<AudioSource>(true)
                    .Any(source => source.clip == runtimeClip),
                Is.False);
        }

        [UnityTest]
        public IEnumerator StoryTrafficEngineLoop_IsLocalAndTracksEmitterRpm()
        {
            runtimeClip = AudioClip.Create(
                "TemporaryDirectImport_StoryTrafficEngine_Test",
                lengthSamples: 48000,
                channels: 1,
                frequency: 48000,
                stream: false);
            var definition = new UnityAudioEventDefinition();
            SetPrivateField(
                definition,
                "eventId",
                "audio.event.traffic.jani.engine.loop");
            SetPrivateField(definition, "clip", runtimeClip);
            SetPrivateField(definition, "loop", true);
            SetPrivateField(definition, "spatialBlend", 1f);
            SetPrivateField(definition, "minimumDistanceMeters", 3f);
            SetPrivateField(definition, "maximumDistanceMeters", 65f);

            supplementalLibrary =
                ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
            SetPrivateField(
                supplementalLibrary,
                "events",
                new[] { definition });
            SetPrivateField(
                backend,
                "supplementalEventLibraries",
                new[] { supplementalLibrary });

            emitterObject = new GameObject(
                "StoryTrafficEmitter_PlayModeTest");
            var emitter = new TestEmitter(
                "audio.emitter.traffic.jani",
                emitterObject.transform);
            Assert.That(
                backend.RegisterEmitter(emitter, out string failure),
                Is.True,
                failure);
            IAudioEventHandle handle = backend.PostEvent(
                new AudioEventRequest(
                    new AudioEventId(
                        "audio.event.traffic.jani.engine.loop"),
                    emitter));
            Assert.That(handle.IsValid, Is.True);
            Assert.That(
                backend.SetParameter(
                    AudioProjectIds.Parameters.VehicleRpmNormalized,
                    0f,
                    emitter),
                Is.True);
            yield return null;

            AudioSource source = null;
            foreach (AudioSource candidate in
                     backendObject.GetComponentsInChildren<AudioSource>())
            {
                if (candidate.clip == runtimeClip)
                {
                    source = candidate;
                    break;
                }
            }

            Assert.That(source, Is.Not.Null);
            Assert.That(source.spatialBlend, Is.EqualTo(1f).Within(0.001f));
            Assert.That(source.minDistance, Is.EqualTo(3f).Within(0.001f));
            Assert.That(source.maxDistance, Is.EqualTo(65f).Within(0.001f));
            Assert.That(source.rolloffMode, Is.EqualTo(AudioRolloffMode.Linear));
            Assert.That(source.dopplerLevel, Is.EqualTo(0.15f).Within(0.001f));
            Vector3 movedEmitterPosition = new Vector3(17f, 2f, -11f);
            emitterObject.transform.position = movedEmitterPosition;
            yield return null;
            Assert.That(
                Vector3.Distance(source.transform.position, movedEmitterPosition),
                Is.LessThan(0.001f),
                "The pooled 3D traffic voice stopped following its owner emitter.");
            float idlePitch = source.pitch;

            Assert.That(
                backend.SetParameter(
                    AudioProjectIds.Parameters.VehicleRpmNormalized,
                    1f,
                    emitter),
                Is.True);
            yield return new WaitForSeconds(0.25f);

            Assert.That(source.pitch, Is.GreaterThan(idlePitch + 0.25f),
                "Unity fallback story-traffic engine pitch did not follow " +
                "its emitter-scoped RPM RTPC.");
            handle.Stop();
        }

        [Test]
        public void DisableThenEnable_UpdatesReadinessAndStopsActiveVoices()
        {
            runtimeClip = AudioClip.Create(
                "ProjectOwnedFallback_DisableTest",
                lengthSamples: 4800,
                channels: 1,
                frequency: 48000,
                stream: false);
            var definition = new UnityAudioEventDefinition();
            SetPrivateField(definition, "eventId", "audio.event.disable_test");
            SetPrivateField(definition, "clip", runtimeClip);
            SetPrivateField(definition, "loop", true);
            SetPrivateField(definition, "spatialBlend", 0f);

            runtimeLibrary = ScriptableObject.CreateInstance<UnityAudioEventLibrary>();
            SetPrivateField(runtimeLibrary, "events", new[] { definition });
            SetPrivateField(backend, "eventLibrary", runtimeLibrary);

            var eventId = new AudioEventId("audio.event.disable_test");
            IAudioEventHandle firstHandle = backend.PostEvent(new AudioEventRequest(eventId));
            Assert.That(firstHandle.IsValid, Is.True);

            backend.enabled = false;

            Assert.That(backend.IsReady, Is.False);
            Assert.That(backend.FailureReason, Does.Contain("disabled"));
            Assert.That(firstHandle.IsValid, Is.False);
            Assert.That(backend.ActiveGenericVoiceCount, Is.Zero);
            LogAssert.Expect(LogType.Warning, "Unity Audio fallback is disabled.");
            Assert.That(
                backend.PostEvent(new AudioEventRequest(eventId)).IsValid,
                Is.False);

            backend.enabled = true;

            Assert.That(backend.IsReady, Is.True);
            Assert.That(backend.FailureReason, Is.Empty);
            IAudioEventHandle secondHandle = backend.PostEvent(
                new AudioEventRequest(eventId));
            Assert.That(secondHandle.IsValid, Is.True);
            backend.StopAll();
            Assert.That(secondHandle.IsValid, Is.False);
            Assert.That(backend.ActiveGenericVoiceCount, Is.Zero);
        }

        private static UnityAudioEventDefinition CreateDefinition(
            string eventId,
            AudioClip clip)
        {
            var definition = new UnityAudioEventDefinition();
            SetPrivateField(definition, "eventId", eventId);
            SetPrivateField(definition, "clip", clip);
            SetPrivateField(definition, "loop", false);
            SetPrivateField(definition, "spatialBlend", 0f);
            return definition;
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing test field {fieldName}.");
            field.SetValue(target, value);
        }

        private sealed class TestEmitter : IAudioEmitter
        {
            public TestEmitter(string stableId, Transform audioTransform)
            {
                StableId = stableId;
                AudioTransform = audioTransform;
            }

            public string StableId { get; }
            public Transform AudioTransform { get; }
            public int OwningSceneHandle => AudioTransform.gameObject.scene.handle;
            public bool IsAudioEmitterActive => AudioTransform.gameObject.activeInHierarchy;
            public AudioSurfaceContext SurfaceContext => AudioSurfaceContext.Unknown;
            public AudioEnvironmentContext EnvironmentContext => AudioEnvironmentContext.Exterior;
        }
    }
}
