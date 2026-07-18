using System;
using System.Collections.Generic;
using MSC.Audio;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.AudioRuntime
{
    public sealed class AudioRuntimeEditModeTests
    {
        private readonly List<UnityEngine.Object> cleanup = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = cleanup.Count - 1; index >= 0; index--)
            {
                if (cleanup[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(cleanup[index]);
                }
            }

            cleanup.Clear();
        }

        [Test]
        public void StableIds_RejectInvalidValuesAndPreserveValidValue()
        {
            Assert.That(
                AudioIdValidation.TryValidate("audio.event.vehicle.engine.started", out _),
                Is.True);
            Assert.That(
                new AudioEventId("audio.event.vehicle.engine.started").Value,
                Is.EqualTo("audio.event.vehicle.engine.started"));

            Assert.That(AudioIdValidation.TryValidate("Audio.Event", out _), Is.False);
            Assert.That(AudioIdValidation.TryValidate("audio..event", out _), Is.False);
            Assert.That(AudioIdValidation.TryValidate("audio.event.", out _), Is.False);
            Assert.Throws<ArgumentException>(() => new AudioParameterId("audio parameter"));
        }

        [Test]
        public void ParameterEntry_ClampsNonFiniteAndOutOfRangeValues()
        {
            var entry = new AudioParameterMapEntry(
                "audio.parameter.test",
                "MSC_Test",
                -1f,
                2f,
                0f);

            Assert.That(entry.Clamp(-5f), Is.EqualTo(-1f));
            Assert.That(entry.Clamp(5f), Is.EqualTo(2f));
            Assert.That(entry.Clamp(float.NaN), Is.Zero);
            Assert.That(entry.Clamp(float.PositiveInfinity), Is.Zero);
        }

        [Test]
        public void Router_PrefersWwiseAndFallsBackWhenPreferredUnavailable()
        {
            RecordingAudioBackend preferred = CreateBackend(
                "wwise.test",
                AudioBackendKind.Wwise,
                ready: false);
            RecordingAudioBackend fallback = CreateBackend(
                "unity.test",
                AudioBackendKind.Unity,
                ready: true);
            AudioBackendRouter router = CreateComponent<AudioBackendRouter>("AudioRouter");
            preferred.MissingBanks = new[] { "MSC_Weather" };

            router.Configure(preferred, fallback, scanLoadedScenes: false);
            Assert.That(router.IsReady, Is.True);
            Assert.That(router.Kind, Is.EqualTo(AudioBackendKind.Unity));
            Assert.That(router.IsUsingFallback, Is.True);
            Assert.That(
                router.CaptureSnapshot().MissingBanks,
                Is.EqualTo(new[] { "MSC_Weather" }));

            preferred.Ready = true;
            Assert.That(router.Kind, Is.EqualTo(AudioBackendKind.Wwise));
            Assert.That(router.IsUsingFallback, Is.False);
            Assert.That(fallback.StopAllCount, Is.EqualTo(1));
        }

        [Test]
        public void Router_RejectsDuplicateEmitterIdentityAndDeduplicatesParameters()
        {
            RecordingAudioBackend fallback = CreateBackend(
                "unity.test",
                AudioBackendKind.Unity,
                ready: true);
            AudioBackendRouter router = CreateComponent<AudioBackendRouter>("AudioRouter");
            router.Configure(null, fallback, scanLoadedScenes: false);

            GameObject firstObject = CreateGameObject("FirstEmitter");
            GameObject secondObject = CreateGameObject("SecondEmitter");
            var first = new TestEmitter("audio.emitter.duplicate", firstObject.transform);
            var second = new TestEmitter("audio.emitter.duplicate", secondObject.transform);

            Assert.That(router.RegisterEmitter(first, out string firstFailure), Is.True);
            Assert.That(firstFailure, Is.Empty);
            Assert.That(router.RegisterEmitter(first, out _), Is.True);
            Assert.That(router.RegisterEmitter(second, out string duplicateFailure), Is.False);
            StringAssert.Contains("Duplicate", duplicateFailure);
            Assert.That(router.RegisteredEmitterCount, Is.EqualTo(1));

            Assert.That(
                router.SetParameter(AudioProjectIds.Parameters.VehicleRpm, 1000f, first),
                Is.True);
            Assert.That(
                router.SetParameter(AudioProjectIds.Parameters.VehicleRpm, 1000f, first),
                Is.True);
            Assert.That(fallback.ParameterUpdateCount, Is.EqualTo(1));
        }

        [Test]
        public void Validation_ReportsDuplicateIdsAndMissingBanks()
        {
            AudioEventMap eventMap = ScriptableObject.CreateInstance<AudioEventMap>();
            AudioParameterMap parameterMap = ScriptableObject.CreateInstance<AudioParameterMap>();
            cleanup.Add(eventMap);
            cleanup.Add(parameterMap);
            eventMap.ConfigureForTests(
                new AudioEventMapEntry(
                    "audio.event.test",
                    "Play_Test",
                    "MSC_Test"),
                new AudioEventMapEntry(
                    "audio.event.test",
                    "Play_Test_Duplicate",
                    "MSC_Test"));
            parameterMap.ConfigureForTests(
                new AudioParameterMapEntry(
                    "audio.parameter.test",
                    "MSC_Test",
                    0f,
                    1f,
                    0f));
            RecordingAudioBackend backend = CreateBackend(
                "wwise.test",
                AudioBackendKind.Wwise,
                ready: true);
            backend.MissingBanks = new[] { "MSC_Test" };

            AudioValidationReport report = AudioValidationService.Validate(
                eventMap,
                parameterMap,
                backend);

            Assert.That(report.Passed, Is.False);
            Assert.That(
                HasIssue(report, "AUDIO-EVENT-DUPLICATE"),
                Is.True);
            Assert.That(HasIssue(report, "AUDIO-BANK-MISSING"), Is.True);
        }

        [Test]
        public void VehicleAdapter_MapsTelemetrySurfaceAndStateToStableIds()
        {
            RecordingAudioBackend backend = CreateBackend(
                "unity.test",
                AudioBackendKind.Unity,
                ready: true);
            GameObject vehicle = CreateGameObject("VehicleAudio");
            vehicle.SetActive(false);
            AudioEmitterAuthoring emitter = vehicle.AddComponent<AudioEmitterAuthoring>();
            emitter.Configure("audio.emitter.vehicle.test");
            VehicleAudioEmitterBackend adapter =
                vehicle.AddComponent<VehicleAudioEmitterBackend>();
            adapter.Configure(backend, emitter);
            vehicle.SetActive(true);
            Assert.That(adapter.TryInitialize(out string failure), Is.True, failure);

            var supplemental = new VehicleAudioSupplementalParameters(
                ignitionOn: true,
                starterRequested: false,
                starterActive: false,
                brake01: 0.4f,
                aggregateWheelSpeedRadiansPerSecond: 31f,
                signedVehicleSpeedMetersPerSecond: 12f,
                suspensionImpact01: 0.2f);
            var parameters = new VehicleAudioParameters(
                VehicleAudioEngineState.Running,
                3200f,
                7000f,
                0.7f,
                0.8f,
                3,
                125f,
                12f,
                0.15f,
                VehicleAudioSurface.Gravel,
                13.8f,
                92f,
                supplemental);

            adapter.SetVehicleParameters(in parameters);

            Assert.That(
                backend.Parameters[AudioProjectIds.Parameters.VehicleRpm],
                Is.EqualTo(3200f));
            Assert.That(
                backend.Parameters[AudioProjectIds.Parameters.VehicleBrake],
                Is.EqualTo(0.4f));
            Assert.That(
                backend.Switches[AudioProjectIds.Switches.SurfaceGroup],
                Is.EqualTo(AudioProjectIds.Switches.SurfaceGravel));
            Assert.That(
                backend.States[AudioProjectIds.States.VehicleEngineGroup],
                Is.EqualTo(AudioProjectIds.States.VehicleEngineRunning));
        }

        [Test]
        public void EnvironmentContexts_ClampAndMapInteriorExteriorStates()
        {
            var context = new AudioEnvironmentContext(
                AudioListenerSpace.Interior,
                2f,
                float.NaN,
                -1f,
                0.75f,
                3f,
                -2f);

            Assert.That(context.Shelter01, Is.EqualTo(1f));
            Assert.That(context.Obstruction01, Is.Zero);
            Assert.That(context.ReverbSend01, Is.Zero);
            Assert.That(context.Wind01, Is.EqualTo(1f));
            Assert.That(context.NormalizedDayTime01, Is.Zero);
            Assert.That(
                VehicleAudioEmitterBackend.MapSurfaceSwitch(VehicleAudioSurface.MudWet),
                Is.EqualTo(AudioProjectIds.Switches.SurfaceMudWet));
            Assert.That(
                AudioListenerContextPresenter.MapDayPhaseState(5f / 24f),
                Is.EqualTo(AudioProjectIds.States.DayPhaseDawn));
            Assert.That(
                AudioListenerContextPresenter.MapDayPhaseState(12f / 24f),
                Is.EqualTo(AudioProjectIds.States.DayPhaseDay));
            Assert.That(
                AudioListenerContextPresenter.MapDayPhaseState(19f / 24f),
                Is.EqualTo(AudioProjectIds.States.DayPhaseEvening));
            Assert.That(
                AudioListenerContextPresenter.MapDayPhaseState(float.NaN),
                Is.EqualTo(AudioProjectIds.States.DayPhaseNight));
        }

        private RecordingAudioBackend CreateBackend(
            string id,
            AudioBackendKind kind,
            bool ready)
        {
            RecordingAudioBackend backend =
                CreateComponent<RecordingAudioBackend>(id);
            backend.Configure(id, kind, ready);
            return backend;
        }

        private T CreateComponent<T>(string name) where T : Component
        {
            GameObject gameObject = CreateGameObject(name);
            return gameObject.AddComponent<T>();
        }

        private GameObject CreateGameObject(string name)
        {
            var gameObject = new GameObject(name);
            cleanup.Add(gameObject);
            return gameObject;
        }

        private static bool HasIssue(AudioValidationReport report, string code)
        {
            for (int index = 0; index < report.Issues.Count; index++)
            {
                if (string.Equals(report.Issues[index].Code, code, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class TestEmitter : IAudioEmitter
        {
            public TestEmitter(string stableId, Transform transform)
            {
                StableId = stableId;
                AudioTransform = transform;
            }

            public string StableId { get; }
            public Transform AudioTransform { get; }
            public int OwningSceneHandle => AudioTransform.gameObject.scene.handle;
            public bool IsAudioEmitterActive => AudioTransform.gameObject.activeInHierarchy;
            public AudioSurfaceContext SurfaceContext => AudioSurfaceContext.Unknown;
            public AudioEnvironmentContext EnvironmentContext => AudioEnvironmentContext.Exterior;
        }

        private sealed class RecordingAudioBackend : MonoBehaviour, IAudioBackend
        {
            private string backendId = "audio.test";
            private AudioBackendKind kind = AudioBackendKind.Unity;
            private readonly HashSet<IAudioEmitter> emitters = new HashSet<IAudioEmitter>();

            public readonly Dictionary<AudioParameterId, float> Parameters =
                new Dictionary<AudioParameterId, float>();
            public readonly Dictionary<AudioSwitchId, AudioSwitchId> Switches =
                new Dictionary<AudioSwitchId, AudioSwitchId>();
            public readonly Dictionary<AudioStateId, AudioStateId> States =
                new Dictionary<AudioStateId, AudioStateId>();

            public string BackendId => backendId;
            public AudioBackendKind Kind => kind;
            public bool Ready { get; set; }
            public bool IsReady => Ready;
            public string FailureReason => Ready ? string.Empty : "test backend unavailable";
            public int ParameterUpdateCount { get; private set; }
            public int StopAllCount { get; private set; }
            public string[] MissingBanks { get; set; } = Array.Empty<string>();

            public void Configure(string id, AudioBackendKind backendKind, bool ready)
            {
                backendId = id;
                kind = backendKind;
                Ready = ready;
            }

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

            public bool UnregisterEmitter(IAudioEmitter emitter) => emitters.Remove(emitter);

            public IAudioEventHandle PostEvent(in AudioEventRequest request) =>
                AudioEventHandles.Invalid;

            public bool SetParameter(
                AudioParameterId parameterId,
                float value,
                IAudioEmitter emitter = null)
            {
                ParameterUpdateCount++;
                Parameters[parameterId] = value;
                return true;
            }

            public bool SetSwitch(
                AudioSwitchId switchGroupId,
                AudioSwitchId switchValueId,
                IAudioEmitter emitter = null)
            {
                Switches[switchGroupId] = switchValueId;
                return true;
            }

            public bool SetState(AudioStateId stateGroupId, AudioStateId stateValueId)
            {
                States[stateGroupId] = stateValueId;
                return true;
            }

            public void SetListenerContext(in AudioListenerContext context)
            {
            }

            public void ApplySettings(in AudioSettingsState settings)
            {
            }

            public void StopAll(float fadeSeconds = 0f)
            {
                StopAllCount++;
            }

            public AudioRuntimeSnapshot CaptureSnapshot() => new AudioRuntimeSnapshot(
                BackendId,
                Kind,
                IsReady,
                Kind == AudioBackendKind.Unity,
                emitters.Count,
                0,
                0,
                MissingBanks,
                default,
                FailureReason);
        }
    }
}
