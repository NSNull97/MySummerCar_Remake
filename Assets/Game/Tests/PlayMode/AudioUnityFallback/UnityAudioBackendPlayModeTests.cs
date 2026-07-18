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
        private AudioClip runtimeClip;

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

            if (runtimeClip != null)
            {
                Object.DestroyImmediate(runtimeClip);
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
