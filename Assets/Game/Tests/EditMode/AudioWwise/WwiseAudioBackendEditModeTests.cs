using System;
using System.Collections.Generic;
using MSC.Audio;
using MSC.Audio.Wwise;
using MSC.Tests.AudioWwise;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.AudioWwise
{
    public sealed class WwiseAudioBackendEditModeTests
    {
        private readonly List<UnityEngine.Object> ownedObjects =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = ownedObjects.Count - 1; index >= 0; index--)
            {
                if (ownedObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(ownedObjects[index]);
                }
            }

            ownedObjects.Clear();
        }

        [Test]
        public void Backend_RemainsNotReady_WhenOfficialSoundEngineIsUnavailable()
        {
            RecordingWwiseSoundEngineApi api = new RecordingWwiseSoundEngineApi
            {
                IsInitializedValue = false,
            };
            WwiseAudioBackend backend = CreateBackend(api, out _, out _, out _);

            Assert.That(backend.IsReady, Is.False);
            StringAssert.Contains("not initialized", backend.FailureReason);
            Assert.That(api.RegisteredGameObjects, Is.Empty);
        }

        [Test]
        public void Backend_ResolvesProjectIdsToWwiseNames_AndMapsSettingsRanges()
        {
            var api = new RecordingWwiseSoundEngineApi();
            WwiseAudioBackend backend = CreateBackend(
                api,
                out AudioEventMap eventMap,
                out AudioParameterMap parameterMap,
                out WwiseBackendNameMap nameMap);
            ConfigureMappings(eventMap, parameterMap, nameMap);
            Reactivate(backend);

            var emitterObject = Own(new GameObject("WwiseEmitter"));
            RecordingAudioEmitter emitter = emitterObject.AddComponent<RecordingAudioEmitter>();
            emitter.Configure("audio.emitter.vehicle.test");

            Assert.That(backend.IsReady, Is.True, backend.FailureReason);
            Assert.That(backend.RegisterEmitter(emitter, out string failure), Is.True, failure);
            Assert.That(
                backend.SetParameter(AudioProjectIds.Parameters.VehicleRpm, 1234f, emitter),
                Is.True);
            Assert.That(
                backend.SetSwitch(
                    AudioProjectIds.Switches.SurfaceGroup,
                    AudioProjectIds.Switches.SurfaceGravel,
                    emitter),
                Is.True);
            Assert.That(
                backend.SetState(
                    AudioProjectIds.States.EnvironmentGroup,
                    AudioProjectIds.States.EnvironmentInterior),
                Is.True);

            backend.ApplySettings(new AudioSettingsState(
                master01: 0.25f,
                vehicle01: 0.5f,
                effects01: 0.6f,
                ambience01: 0.7f,
                music01: 0.8f,
                ui01: 0.9f,
                AudioDynamicRangeMode.Balanced,
                muteOnFocusLoss: true,
                subtitlesEnabled: false,
                captionsEnabled: false,
                reduceLoudSounds: false));

            Assert.That(api.RtpcCalls[0].Name, Is.EqualTo("Vehicle_RPM"));
            Assert.That(api.RtpcCalls[0].Value, Is.EqualTo(1234f));
            Assert.That(api.RtpcCalls[0].GameObject, Is.EqualTo(emitterObject));
            Assert.That(api.SwitchCalls[0].GroupName, Is.EqualTo("Surface"));
            Assert.That(api.SwitchCalls[0].ValueName, Is.EqualTo("Gravel"));
            Assert.That(api.StateCalls[0].GroupName, Is.EqualTo("Environment"));
            Assert.That(api.StateCalls[0].ValueName, Is.EqualTo("Interior"));

            RecordingWwiseSoundEngineApi.RtpcCall masterCall = api.RtpcCalls.Find(
                call => string.Equals(call.Name, "Mixer_Master", StringComparison.Ordinal));
            Assert.That(masterCall.Value, Is.EqualTo(25f).Within(0.001f));
            Assert.That(masterCall.GameObject, Is.Null);

            backend.ReportBankLoaded("Weather.bnk");
            AudioRuntimeSnapshot snapshot = backend.CaptureSnapshot();
            Assert.That(snapshot.BackendId, Is.EqualTo("wwise.official.2025.1.9.4241"));
            Assert.That(snapshot.Kind, Is.EqualTo(AudioBackendKind.Wwise));
            Assert.That(snapshot.IsReady, Is.True);
            Assert.That(snapshot.IsFallback, Is.False);
            Assert.That(snapshot.RegisteredEmitterCount, Is.EqualTo(1));
            Assert.That(snapshot.LoadedBankCount, Is.EqualTo(1));
            Assert.That(snapshot.MissingBanks, Is.Empty);
        }

        [Test]
        public void RequiredBankGate_RemainsClosedUntilEveryBankIsReported()
        {
            var api = new RecordingWwiseSoundEngineApi();
            WwiseAudioBackend backend = CreateBackend(
                api,
                out AudioEventMap eventMap,
                out AudioParameterMap parameterMap,
                out WwiseBackendNameMap nameMap);
            ConfigureMappings(eventMap, parameterMap, nameMap);
            backend.ConfigureBankReadinessForAuthoring(
                true,
                "Init",
                "Weather");
            Reactivate(backend);

            Assert.That(backend.IsReady, Is.False);
            StringAssert.Contains("Init", backend.FailureReason);

            backend.ReportBankLoaded("Init.bnk");
            Assert.That(backend.IsReady, Is.False);
            StringAssert.Contains("Weather", backend.FailureReason);

            backend.ReportBankLoaded("Weather");
            Assert.That(backend.IsReady, Is.True, backend.FailureReason);

            backend.ReportBankUnloaded("Weather.bnk");
            Assert.That(backend.IsReady, Is.False);
            StringAssert.Contains("Weather", backend.FailureReason);
        }

        private WwiseAudioBackend CreateBackend(
            RecordingWwiseSoundEngineApi api,
            out AudioEventMap eventMap,
            out AudioParameterMap parameterMap,
            out WwiseBackendNameMap nameMap)
        {
            eventMap = Own(ScriptableObject.CreateInstance<AudioEventMap>());
            parameterMap = Own(ScriptableObject.CreateInstance<AudioParameterMap>());
            nameMap = Own(ScriptableObject.CreateInstance<WwiseBackendNameMap>());
            var backendObject = Own(new GameObject("WwiseBackend"));
            backendObject.SetActive(false);
            WwiseAudioBackend backend = backendObject.AddComponent<WwiseAudioBackend>();
            backend.ConfigureForTests(eventMap, parameterMap, nameMap, api);
            backendObject.SetActive(true);
            backend.ActivateForTests();
            return backend;
        }

        private static void ConfigureMappings(
            AudioEventMap eventMap,
            AudioParameterMap parameterMap,
            WwiseBackendNameMap nameMap)
        {
            eventMap.ConfigureForTests(new AudioEventMapEntry(
                AudioProjectIds.Events.WeatherThunder.Value,
                "Play_Thunder",
                "Weather.bnk",
                isSpatialized: true,
                canOverlap: true));
            parameterMap.ConfigureForTests(
                new AudioParameterMapEntry(
                    AudioProjectIds.Parameters.VehicleRpm.Value,
                    "Vehicle_RPM",
                    0f,
                    8000f,
                    0f),
                MixerEntry(AudioProjectIds.Parameters.Master, "Mixer_Master"),
                MixerEntry(AudioProjectIds.Parameters.Vehicle, "Mixer_Vehicle"),
                MixerEntry(AudioProjectIds.Parameters.Effects, "Mixer_Effects"),
                MixerEntry(AudioProjectIds.Parameters.Ambience, "Mixer_Ambience"),
                MixerEntry(AudioProjectIds.Parameters.Music, "Mixer_Music"),
                MixerEntry(AudioProjectIds.Parameters.Ui, "Mixer_UI"));
            nameMap.ConfigureForTests(
                new WwiseBackendNameMapEntry(
                    AudioProjectIds.Switches.SurfaceGroup.Value,
                    "Surface"),
                new WwiseBackendNameMapEntry(
                    AudioProjectIds.Switches.SurfaceGravel.Value,
                    "Gravel"),
                new WwiseBackendNameMapEntry(
                    AudioProjectIds.States.EnvironmentGroup.Value,
                    "Environment"),
                new WwiseBackendNameMapEntry(
                    AudioProjectIds.States.EnvironmentInterior.Value,
                    "Interior"));
        }

        private static AudioParameterMapEntry MixerEntry(
            AudioParameterId id,
            string backendName) =>
            new AudioParameterMapEntry(id.Value, backendName, 0f, 100f, 100f);

        private static void Reactivate(WwiseAudioBackend backend)
        {
            backend.ActivateForTests();
        }

        private T Own<T>(T value) where T : UnityEngine.Object
        {
            ownedObjects.Add(value);
            return value;
        }
    }
}
