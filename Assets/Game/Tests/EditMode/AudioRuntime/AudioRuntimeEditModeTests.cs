using System;
using System.Collections.Generic;
using MSC.Audio;
using MSC.Audio.Composition;
using MSC.Audio.UnityFallback;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

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
        public void WorldAmbientScheduleUsesRareInitialAndPeriodicChainsawWindows()
        {
            uint firstState = 0x4D534341u;
            uint secondState = 0x4D534341u;

            float firstInitial = WorldAmbientAudioPresenter.CalculateNextInterval(
                ref firstState,
                initial: true);
            float secondInitial = WorldAmbientAudioPresenter.CalculateNextInterval(
                ref secondState,
                initial: true);
            float periodic = WorldAmbientAudioPresenter.CalculateNextInterval(
                ref firstState,
                initial: false);

            Assert.That(firstInitial, Is.InRange(300f, 900f));
            Assert.That(secondInitial, Is.EqualTo(firstInitial));
            Assert.That(periodic, Is.InRange(900f, 2100f));
        }

        [Test]
        public void WorldAmbientRespondsToDaylightWeatherAndLocalChainsawPolicy()
        {
            float clearDay = WorldAmbientAudioPresenter.CalculateAmbientGain(
                12d / 24d,
                precipitation01: 0f,
                wind01: 0.1f);
            float rainyDay = WorldAmbientAudioPresenter.CalculateAmbientGain(
                12d / 24d,
                precipitation01: 0.8f,
                wind01: 0.1f);
            float night = WorldAmbientAudioPresenter.CalculateAmbientGain(
                2d / 24d,
                precipitation01: 0f,
                wind01: 0f);

            Assert.That(clearDay, Is.GreaterThan(0.8f));
            Assert.That(rainyDay, Is.LessThan(clearDay * 0.35f));
            Assert.That(night, Is.Zero.Within(0.0001f));
            Assert.That(
                WorldAmbientAudioPresenter.IsChainsawEligible(
                    11d / 24d,
                    paused: false,
                    precipitation01: 0f,
                    wind01: 0.2f,
                    lightningRisk01: 0f),
                Is.True);
            Assert.That(
                WorldAmbientAudioPresenter.IsChainsawEligible(
                    11d / 24d,
                    paused: false,
                    precipitation01: 0.4f,
                    wind01: 0.2f,
                    lightningRisk01: 0f),
                Is.False);

            Vector3 homeKitchen = new Vector3(
                161.89757f,
                2.16918f,
                -1033.2045f);
            Assert.That(
                Vector3.Distance(
                    WorldAmbientAudioPresenter.ChainsawSourceWorldPosition,
                    homeKitchen),
                Is.InRange(50f, 90f));
        }

        [Test]
        public void WorldAmbientPausePolicyUsesClockOrUnityTimeScale()
        {
            Assert.That(
                WorldAmbientAudioPresenter.ShouldPausePlayback(
                    gameTimePaused: false,
                    unityTimeScale: 1f),
                Is.False);
            Assert.That(
                WorldAmbientAudioPresenter.ShouldPausePlayback(
                    gameTimePaused: true,
                    unityTimeScale: 1f),
                Is.True);
            Assert.That(
                WorldAmbientAudioPresenter.ShouldPausePlayback(
                    gameTimePaused: false,
                    unityTimeScale: 0f),
                Is.True);
            Assert.That(
                WorldAmbientAudioPresenter.ShouldPausePlayback(
                    gameTimePaused: false,
                    unityTimeScale: float.NaN),
                Is.True);
        }

        [Test]
        public void WorldAmbientParityRosterContainsEveryDonorDayPhaseLayer()
        {
            IReadOnlyList<WorldAmbientLayerDefinition> layers =
                WorldAmbientAudioPresenter.LayerDefinitions;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var events = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < layers.Count; index++)
            {
                Assert.That(ids.Add(layers[index].LayerId), Is.True);
                events.Add(layers[index].EventId.Value);
            }

            Assert.That(layers.Count, Is.EqualTo(11));
            Assert.That(events, Does.Contain(
                AudioProjectIds.Events.WorldBirdsMorning.Value));
            Assert.That(events, Does.Contain(
                AudioProjectIds.Events.WorldBirdsDay.Value));
            Assert.That(events, Does.Contain(
                AudioProjectIds.Events.WorldBirdsEvening.Value));
            Assert.That(events, Does.Contain(
                AudioProjectIds.Events.WorldBirdsNight.Value));
            Assert.That(events, Does.Contain(
                AudioProjectIds.Events.WorldBirdsSwamp.Value));
            Assert.That(events, Does.Contain(
                AudioProjectIds.Events.WorldMeadow.Value));
            Assert.That(events, Does.Contain(
                AudioProjectIds.Events.WorldDog.Value));
            Assert.That(events, Does.Contain(
                AudioProjectIds.Events.WorldLakeAmbience.Value));
        }

        [Test]
        public void WorldAmbientUsesDonorPhaseBoundariesAndGarageTranslation()
        {
            Assert.That(
                WorldAmbientAudioPresenter.ResolvePhase(2d / 24d),
                Is.EqualTo(WorldAmbientPhase.Night));
            Assert.That(
                WorldAmbientAudioPresenter.ResolvePhase(6d / 24d),
                Is.EqualTo(WorldAmbientPhase.Morning));
            Assert.That(
                WorldAmbientAudioPresenter.ResolvePhase(12d / 24d),
                Is.EqualTo(WorldAmbientPhase.Day));
            Assert.That(
                WorldAmbientAudioPresenter.ResolvePhase(18d / 24d),
                Is.EqualTo(WorldAmbientPhase.Evening));

            WorldAmbientLayerDefinition morning =
                FindAmbientLayer("morning.birds");
            Assert.That(
                morning.WorldPosition,
                Is.EqualTo(new Vector3(26.98f, 14.611f, -100.625f))
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void WorldAmbientLayerGainHonorsPhaseWeatherAndDistance()
        {
            WorldAmbientLayerDefinition day = FindAmbientLayer("day.meadow");
            float nearClear = WorldAmbientAudioPresenter.CalculateLayerGain(
                day,
                14d / 24d,
                paused: false,
                precipitation01: 0f,
                wind01: 0f,
                day.WorldPosition);
            float nearRain = WorldAmbientAudioPresenter.CalculateLayerGain(
                day,
                14d / 24d,
                paused: false,
                precipitation01: 1f,
                wind01: 0f,
                day.WorldPosition);
            float wrongPhase = WorldAmbientAudioPresenter.CalculateLayerGain(
                day,
                2d / 24d,
                paused: false,
                precipitation01: 0f,
                wind01: 0f,
                day.WorldPosition);
            float outOfRange = WorldAmbientAudioPresenter.CalculateLayerGain(
                day,
                14d / 24d,
                paused: false,
                precipitation01: 0f,
                wind01: 0f,
                day.WorldPosition + Vector3.right * 2000f);

            Assert.That(nearClear, Is.EqualTo(day.BaseVolume01).Within(0.0001f));
            Assert.That(nearRain, Is.LessThan(nearClear * 0.2f));
            Assert.That(wrongPhase, Is.Zero.Within(0.0001f));
            Assert.That(outOfRange, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void DialogueFallbackAppliesAudibleSoftGainWithoutClipping()
        {
            float quiet = UnityDialogueGainFilter.ApplySoftGain(
                0.08f,
                UnityDialogueGainFilter.DefaultDialogueGain);
            float loud = UnityDialogueGainFilter.ApplySoftGain(
                1f,
                UnityDialogueGainFilter.DefaultDialogueGain);

            Assert.That(quiet, Is.GreaterThan(0.18f));
            Assert.That(loud, Is.LessThanOrEqualTo(1f));
            Assert.That(loud, Is.GreaterThan(0.9f));
        }

        private static WorldAmbientLayerDefinition FindAmbientLayer(
            string layerId)
        {
            IReadOnlyList<WorldAmbientLayerDefinition> layers =
                WorldAmbientAudioPresenter.LayerDefinitions;
            for (int index = 0; index < layers.Count; index++)
            {
                if (string.Equals(
                        layers[index].LayerId,
                        layerId,
                        StringComparison.Ordinal))
                {
                    return layers[index];
                }
            }

            Assert.Fail($"Ambient layer was not found: {layerId}");
            return default;
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
