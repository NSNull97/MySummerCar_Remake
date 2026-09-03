using System;
using System.Collections.Generic;
using System.Reflection;
using MSC.Audio;
using MSC.Audio.WeatherIntegration;
using MSC.Weather.Domain;
using MSC.Weather.Lightning;
using MSC.Weather.Production;
using MSC.Weather.Wetness;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.AudioWeatherIntegration
{
    public sealed class WeatherAudioMappingTests
    {
        [Test]
        public void PrecipitationAndExposureMapToStableProjectIds()
        {
            Assert.That(
                WeatherAudioPresenter.MapPrecipitation(WeatherPrecipitationType.None),
                Is.EqualTo(AudioProjectIds.Switches.WeatherPrecipitationNone));
            Assert.That(
                WeatherAudioPresenter.MapPrecipitation(WeatherPrecipitationType.Drizzle),
                Is.EqualTo(AudioProjectIds.Switches.WeatherPrecipitationDrizzle));
            Assert.That(
                WeatherAudioPresenter.MapPrecipitation(WeatherPrecipitationType.Rain),
                Is.EqualTo(AudioProjectIds.Switches.WeatherPrecipitationRain));
            Assert.That(
                WeatherAudioPresenter.MapExposure(WeatherExposureContext.Exterior),
                Is.EqualTo(AudioListenerSpace.Exterior));
            Assert.That(
                WeatherAudioPresenter.MapExposure(WeatherExposureContext.Sheltered),
                Is.EqualTo(AudioListenerSpace.Sheltered));
            Assert.That(
                WeatherAudioPresenter.MapExposure(WeatherExposureContext.Interior),
                Is.EqualTo(AudioListenerSpace.Interior));
        }

        [Test]
        public void ThunderRequestPreservesWorldPositionIntensityAndDelay()
        {
            var thunder = new ThunderAudioRequest(
                7U,
                new Vector3(30f, 4f, -20f),
                0.65f,
                1.75d);

            AudioEventRequest request = WeatherAudioPresenter.MapThunderRequest(in thunder);

            Assert.That(request.EventId, Is.EqualTo(AudioProjectIds.Events.WeatherThunder));
            Assert.That(request.WorldPosition, Is.EqualTo(thunder.WorldPosition));
            Assert.That(request.Volume01, Is.EqualTo(0.65f));
            Assert.That(request.DelaySeconds, Is.EqualTo(1.75d));
            Assert.That(request.Emitter, Is.Null);
        }

        [Test]
        public void WeatherLoopsUseHysteresisAndStayStoppedAtZeroIntensity()
        {
            Assert.That(
                WeatherAudioPresenter.ShouldKeepLoopActive(0f, false),
                Is.False);
            Assert.That(
                WeatherAudioPresenter.ShouldKeepLoopActive(0.009f, false),
                Is.False);
            Assert.That(
                WeatherAudioPresenter.ShouldKeepLoopActive(0.01f, false),
                Is.True);
            Assert.That(
                WeatherAudioPresenter.ShouldKeepLoopActive(0.007f, true),
                Is.True);
            Assert.That(
                WeatherAudioPresenter.ShouldKeepLoopActive(0.005f, true),
                Is.False);
            Assert.That(
                WeatherAudioPresenter.ShouldKeepLoopActive(float.NaN, true),
                Is.False);
        }

        [Test]
        public void ClearWeatherWind_RemainsStoppedUntilAudibleWindThreshold()
        {
            Assert.That(
                WeatherAudioPresenter.ShouldKeepLoopActive(
                    0.1f,
                    false,
                    WeatherAudioPresenter.WindLoopStartThreshold,
                    WeatherAudioPresenter.WindLoopStopThreshold),
                Is.False);
            Assert.That(
                WeatherAudioPresenter.ShouldKeepLoopActive(
                    0.18f,
                    false,
                    WeatherAudioPresenter.WindLoopStartThreshold,
                    WeatherAudioPresenter.WindLoopStopThreshold),
                Is.True);
            Assert.That(
                WeatherAudioPresenter.ShouldKeepLoopActive(
                    0.13f,
                    true,
                    WeatherAudioPresenter.WindLoopStartThreshold,
                    WeatherAudioPresenter.WindLoopStopThreshold),
                Is.True);
            Assert.That(
                WeatherAudioPresenter.ShouldKeepLoopActive(
                    0.12f,
                    true,
                    WeatherAudioPresenter.WindLoopStartThreshold,
                    WeatherAudioPresenter.WindLoopStopThreshold),
                Is.False);
        }

        [Test]
        public void ProductionShelterFallback_ProducesInteriorListenerContext()
        {
            AudioEnvironmentContext context =
                AudioListenerContextPresenter.CreateWeatherFallbackContext(
                    AudioListenerSpace.Interior,
                    0.8f,
                    0.5f,
                    0.25f);

            Assert.That(context.ListenerSpace, Is.EqualTo(AudioListenerSpace.Interior));
            Assert.That(context.Shelter01, Is.EqualTo(1f));
            Assert.That(context.Precipitation01, Is.EqualTo(0.8f));
            Assert.That(context.Wind01, Is.EqualTo(0.5f));
        }

        [Test]
        public void HybridExposure_ReusesContinuousShelterContextAndExistingRtpc()
        {
            var exposure = new WeatherExposureState(
                0.6f,
                1f,
                0.25f,
                0.4f,
                0.3f,
                0.35f,
                0.7f,
                0.4f,
                0.7f);

            float shelter =
                WeatherAudioPresenter.CalculateExistingShelterParameter(exposure);
            AudioEnvironmentContext context =
                AudioListenerContextPresenter.CreateContinuousWeatherContext(
                    AudioListenerSpace.Interior,
                    shelter,
                    0.36f,
                    0.21f,
                    0.8f,
                    0.5f,
                    0.25f);

            Assert.That(shelter, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(
                WeatherAudioPresenter.CalculateAudiblePrecipitation(
                    0.8f,
                    exposure),
                Is.EqualTo(0.28f).Within(0.0001f));
            Assert.That(context.Shelter01, Is.EqualTo(0.65f).Within(0.0001f));
            Assert.That(context.Obstruction01, Is.EqualTo(0.36f).Within(0.0001f));
            Assert.That(context.ListenerSpace, Is.EqualTo(AudioListenerSpace.Interior));
        }

        [Test]
        public void BackendIdentityChange_ReplaysWeatherStateAndRestartsLoops()
        {
            var controllerObject = new GameObject("WeatherController");
            var presenterObject = new GameObject("WeatherAudioPresenter");
            controllerObject.SetActive(false);
            presenterObject.SetActive(false);

            try
            {
                ProductionEnvironmentController controller =
                    controllerObject.AddComponent<ProductionEnvironmentController>();
                WeatherEnvironmentOutputs outputs = CreateRainOutputs();
                SetCurrentOutputs(controller, outputs);

                SwitchableAudioBackend backend =
                    presenterObject.AddComponent<SwitchableAudioBackend>();
                WeatherAudioPresenter presenter =
                    presenterObject.AddComponent<WeatherAudioPresenter>();
                presenter.Configure(controller, backend, null);
                InvokeEnvironmentOutputs(presenter, outputs);

                Assert.That(backend.ParameterUpdateCount, Is.EqualTo(3));
                Assert.That(backend.SwitchUpdateCount, Is.EqualTo(1));
                Assert.That(backend.PostedEvents.Count, Is.EqualTo(2));

                backend.BackendIdValue = "wwise.test";
                InvokeUpdate(presenter);

                Assert.That(backend.ParameterUpdateCount, Is.EqualTo(6));
                Assert.That(backend.SwitchUpdateCount, Is.EqualTo(2));
                Assert.That(backend.PostedEvents.Count, Is.EqualTo(4));
                Assert.That(backend.StoppedHandleCount, Is.EqualTo(2));
                Assert.That(
                    backend.PostedEvents[2],
                    Is.EqualTo(AudioProjectIds.Events.WeatherRainExterior));
                Assert.That(
                    backend.PostedEvents[3],
                    Is.EqualTo(AudioProjectIds.Events.WeatherWind));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenterObject);
                UnityEngine.Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void ProductionInteriorOutput_SelectsInteriorRainWithListenerPresenterPresent()
        {
            var controllerObject = new GameObject("WeatherController");
            var audioObject = new GameObject("WeatherAudioPresenter");
            var listenerObject = new GameObject("AudioListenerPresenter");
            controllerObject.SetActive(false);
            audioObject.SetActive(false);
            listenerObject.SetActive(false);

            try
            {
                ProductionEnvironmentController controller =
                    controllerObject.AddComponent<ProductionEnvironmentController>();
                WeatherEnvironmentOutputs outputs = CreateRainOutputs(
                    WeatherExposureContext.Interior);
                SetCurrentOutputs(controller, outputs);
                SwitchableAudioBackend backend =
                    audioObject.AddComponent<SwitchableAudioBackend>();
                AudioListenerContextPresenter listener =
                    listenerObject.AddComponent<AudioListenerContextPresenter>();
                listener.Configure(backend, listenerObject.transform);
                WeatherAudioPresenter presenter =
                    audioObject.AddComponent<WeatherAudioPresenter>();
                presenter.Configure(controller, backend, null, listener);

                InvokeEnvironmentOutputs(presenter, outputs);

                Assert.That(backend.PostedEvents, Is.Not.Empty);
                Assert.That(
                    backend.PostedEvents[0],
                    Is.EqualTo(AudioProjectIds.Events.WeatherRainInterior));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(listenerObject);
                UnityEngine.Object.DestroyImmediate(audioObject);
                UnityEngine.Object.DestroyImmediate(controllerObject);
            }
        }

        private static WeatherEnvironmentOutputs CreateRainOutputs(
            WeatherExposureContext exposure = WeatherExposureContext.Exterior)
        {
            WeatherState state = WeatherProfileCatalog
                .CreateRemakeDesignTargets()
                .Get(WeatherStateIds.Thunderstorm)
                .TargetState;
            var context = new WeatherEnvironmentOutputContext(
                new WeatherClockOutput(1995, 8, 1, 0, 0.5f),
                new WetnessEnvironmentOutputs(
                    new WetnessState(0.2f, 0.3f, 0.1f, 0.4f),
                    SurfaceExposureProfile.Exterior.StableId,
                    1U),
                exposure,
                new WeatherPresentationStatusOutput(
                    "quality.high",
                    WeatherPresentationHealth.Ready,
                    1U));
            return WeatherEnvironmentOutputs.Compose(state, 1U, context);
        }

        private static void SetCurrentOutputs(
            ProductionEnvironmentController controller,
            WeatherEnvironmentOutputs outputs)
        {
            FieldInfo field = typeof(ProductionEnvironmentController).GetField(
                "currentOutputs",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(controller, outputs);
        }

        private static void InvokeUpdate(WeatherAudioPresenter presenter)
        {
            MethodInfo method = typeof(WeatherAudioPresenter).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(presenter, null);
        }

        private static void InvokeEnvironmentOutputs(
            WeatherAudioPresenter presenter,
            WeatherEnvironmentOutputs outputs)
        {
            MethodInfo method = typeof(WeatherAudioPresenter).GetMethod(
                "HandleEnvironmentOutputs",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(presenter, new object[] { outputs });
        }

        private sealed class SwitchableAudioBackend : MonoBehaviour, IAudioBackend
        {
            private readonly List<TestHandle> handles = new List<TestHandle>();

            public string BackendIdValue { get; set; } = "unity.test";
            public string BackendId => BackendIdValue;
            public AudioBackendKind Kind => BackendIdValue.StartsWith("wwise", StringComparison.Ordinal)
                ? AudioBackendKind.Wwise
                : AudioBackendKind.Unity;
            public bool IsReady => true;
            public string FailureReason => string.Empty;
            public int ParameterUpdateCount { get; private set; }
            public int SwitchUpdateCount { get; private set; }
            public int StoppedHandleCount { get; private set; }
            public List<AudioEventId> PostedEvents { get; } = new List<AudioEventId>();

            public bool RegisterEmitter(IAudioEmitter emitter, out string failure)
            {
                failure = string.Empty;
                return true;
            }

            public bool UnregisterEmitter(IAudioEmitter emitter) => true;

            public IAudioEventHandle PostEvent(in AudioEventRequest request)
            {
                PostedEvents.Add(request.EventId);
                var handle = new TestHandle(
                    (ulong)PostedEvents.Count,
                    request.EventId,
                    () => StoppedHandleCount++);
                handles.Add(handle);
                return handle;
            }

            public bool SetParameter(
                AudioParameterId parameterId,
                float value,
                IAudioEmitter emitter = null)
            {
                ParameterUpdateCount++;
                return true;
            }

            public bool SetSwitch(
                AudioSwitchId switchGroupId,
                AudioSwitchId switchValueId,
                IAudioEmitter emitter = null)
            {
                SwitchUpdateCount++;
                return true;
            }

            public bool SetState(AudioStateId stateGroupId, AudioStateId stateValueId) => true;
            public void SetListenerContext(in AudioListenerContext context) { }
            public void ApplySettings(in AudioSettingsState settings) { }

            public void StopAll(float fadeSeconds = 0f)
            {
                for (int index = 0; index < handles.Count; index++)
                {
                    handles[index].Stop(fadeSeconds);
                }
            }

            public AudioRuntimeSnapshot CaptureSnapshot() => new AudioRuntimeSnapshot(
                BackendId,
                Kind,
                true,
                Kind == AudioBackendKind.Unity,
                0,
                0,
                0,
                Array.Empty<string>(),
                default,
                string.Empty);
        }

        private sealed class TestHandle : IAudioEventHandle
        {
            private readonly Action stopped;
            private bool valid = true;
            private bool playing = true;

            public TestHandle(ulong handleId, AudioEventId eventId, Action onStopped)
            {
                HandleId = handleId;
                EventId = eventId;
                stopped = onStopped;
            }

            public ulong HandleId { get; }
            public AudioEventId EventId { get; }
            public bool IsValid => valid;
            public bool IsPlaying => valid && playing;

            public void Stop(float fadeSeconds = 0f)
            {
                if (!playing)
                {
                    return;
                }

                playing = false;
                stopped();
            }

            public void Dispose()
            {
                valid = false;
                playing = false;
            }
        }
    }
}
