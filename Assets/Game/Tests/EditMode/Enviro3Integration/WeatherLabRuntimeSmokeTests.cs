using System;
using System.Collections;
using System.Reflection;
using Enviro;
using MSC.Development.WeatherLab;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Presentation;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.Enviro3Integration
{
    public sealed class WeatherLabRuntimeSmokeTests
    {
        [UnityTest]
        public IEnumerator WeatherLab_ExplicitStatesStayOperationalAndSingleOwned()
        {
            EditorSceneManager.OpenScene(
                WeatherLabSceneMarker.SceneAssetPath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            yield return new EnterPlayMode();
            for (int frame = 0; frame < 8; frame++)
            {
                yield return null;
            }

            SmokeObservations observations = null;
            Exception runtimeException = null;
            bool rainStateOperational = false;
            bool rainParticlesOperational = false;
            bool stormStateOperational = false;
            bool stormParticlesOperational = false;
            try
            {
                WeatherLabStateController controller =
                    Object.FindFirstObjectByType<WeatherLabStateController>();
                Assert.That(controller, Is.Not.Null);
                rainStateOperational = controller
                    .ApplyPreset(EnvironmentPresentationPresetKind.Rain)
                    .IsOperational;
            }
            catch (Exception exception)
            {
                runtimeException = exception;
            }

            if (runtimeException == null)
            {
                for (int frame = 0; frame < 4; frame++)
                {
                    yield return null;
                }

                try
                {
                    EnviroManager manager = Object.FindFirstObjectByType<EnviroManager>();
                    rainParticlesOperational =
                        rainStateOperational &&
                        SmokeObservations.IsRainEffectOperational(manager.Effects, 0.5f);

                    WeatherLabStateController controller =
                        Object.FindFirstObjectByType<WeatherLabStateController>();
                    stormStateOperational = controller
                        .ApplyPreset(EnvironmentPresentationPresetKind.Storm)
                        .IsOperational;
                }
                catch (Exception exception)
                {
                    runtimeException = exception;
                }
            }

            if (runtimeException == null)
            {
                for (int frame = 0; frame < 4; frame++)
                {
                    yield return null;
                }

                try
                {
                    EnviroManager manager = Object.FindFirstObjectByType<EnviroManager>();
                    stormParticlesOperational =
                        stormStateOperational &&
                        SmokeObservations.IsRainEffectOperational(manager.Effects, 1f);
                    observations = SmokeObservations.Capture(
                        rainParticlesOperational,
                        stormParticlesOperational);
                }
                catch (Exception exception)
                {
                    runtimeException = exception;
                }
            }

            yield return new ExitPlayMode();

            Assert.That(
                runtimeException,
                Is.Null,
                runtimeException == null ? string.Empty : runtimeException.ToString());
            Assert.That(observations, Is.Not.Null);
            Assert.That(observations.AdapterAttached, Is.True);
            Assert.That(observations.AllStatesOperational, Is.True);
            Assert.That(observations.AllMappingsExact, Is.True);
            Assert.That(observations.BothQualityTiersOperational, Is.True);
            Assert.That(observations.BothQualityMappingsExact, Is.True);
            Assert.That(observations.LightningRequestOperational, Is.True);
            Assert.That(observations.RainParticlesOperational, Is.True);
            Assert.That(observations.StormParticlesOperational, Is.True);
            Assert.That(observations.TimeSimulationDisabled, Is.True);
            Assert.That(observations.AudioSilent, Is.True);
            Assert.That(observations.ManagerCount, Is.EqualTo(1));
            Assert.That(observations.EnabledDirectionalLightCount, Is.EqualTo(1));
            Assert.That(observations.GlobalVolumeCount, Is.EqualTo(1));
        }

        private sealed class SmokeObservations
        {
            public bool AdapterAttached { get; private set; }

            public bool AllStatesOperational { get; private set; }

            public bool AllMappingsExact { get; private set; }

            public bool BothQualityTiersOperational { get; private set; }

            public bool BothQualityMappingsExact { get; private set; }

            public bool LightningRequestOperational { get; private set; }

            public bool RainParticlesOperational { get; private set; }

            public bool StormParticlesOperational { get; private set; }

            public bool TimeSimulationDisabled { get; private set; }

            public bool AudioSilent { get; private set; }

            public int ManagerCount { get; private set; }

            public int EnabledDirectionalLightCount { get; private set; }

            public int GlobalVolumeCount { get; private set; }

            public static SmokeObservations Capture(
                bool rainParticlesOperational,
                bool stormParticlesOperational)
            {
                WeatherLabStateController controller =
                    Object.FindFirstObjectByType<WeatherLabStateController>();
                Enviro3EnvironmentAdapter adapter =
                    Object.FindFirstObjectByType<Enviro3EnvironmentAdapter>();
                EnviroManager manager = Object.FindFirstObjectByType<EnviroManager>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(adapter, Is.Not.Null);
                Assert.That(manager, Is.Not.Null);

                Enviro3EnvironmentBindings bindings = GetPrivateField<Enviro3EnvironmentBindings>(
                    adapter,
                    "bindings");
                Assert.That(bindings, Is.Not.Null);

                bool statesOperational = true;
                bool mappingsExact = true;
                statesOperational &= ApplyAndCheck(
                    controller,
                    manager,
                    EnvironmentPresentationPresetKind.Clear,
                    bindings.Clear,
                    expectRuntimeClone: false,
                    ref mappingsExact);
                statesOperational &= ApplyAndCheck(
                    controller,
                    manager,
                    EnvironmentPresentationPresetKind.Overcast,
                    bindings.Overcast,
                    expectRuntimeClone: false,
                    ref mappingsExact);
                statesOperational &= ApplyAndCheck(
                    controller,
                    manager,
                    EnvironmentPresentationPresetKind.Rain,
                    bindings.Rain,
                    expectRuntimeClone: false,
                    ref mappingsExact);
                statesOperational &= ApplyAndCheck(
                    controller,
                    manager,
                    EnvironmentPresentationPresetKind.Storm,
                    bindings.Storm,
                    expectRuntimeClone: true,
                    ref mappingsExact);
                statesOperational &= ApplyAndCheck(
                    controller,
                    manager,
                    EnvironmentPresentationPresetKind.Night,
                    bindings.Clear,
                    expectRuntimeClone: false,
                    ref mappingsExact);
                statesOperational &= ApplyAndCheck(
                    controller,
                    manager,
                    EnvironmentPresentationPresetKind.Mist,
                    bindings.Fog,
                    expectRuntimeClone: false,
                    ref mappingsExact);

                EnvironmentPresentationStatus lowStatus =
                    controller.ApplyQuality(EnvironmentQualityTier.Low);
                bool lowMapped = manager.Quality.Settings.defaultQuality == bindings.Low;
                EnvironmentPresentationStatus highStatus =
                    controller.ApplyQuality(EnvironmentQualityTier.High);
                bool highMapped = manager.Quality.Settings.defaultQuality == bindings.High;

                EnvironmentPresentationStatus lightningStatus =
                    controller.TriggerAmbientLightning();

                EnviroManager[] managers = Object.FindObjectsByType<EnviroManager>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                Light[] lights = Object.FindObjectsByType<Light>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                Volume[] volumes = Object.FindObjectsByType<Volume>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

                int enabledDirectionalLights = 0;
                for (int index = 0; index < lights.Length; index++)
                {
                    if (lights[index] != null && lights[index].enabled &&
                        lights[index].type == LightType.Directional)
                    {
                        enabledDirectionalLights++;
                    }
                }

                int globalVolumes = 0;
                for (int index = 0; index < volumes.Length; index++)
                {
                    if (volumes[index] != null && volumes[index].enabled && volumes[index].isGlobal)
                    {
                        globalVolumes++;
                    }
                }

                return new SmokeObservations
                {
                    AdapterAttached = adapter.IsAttached,
                    AllStatesOperational = statesOperational,
                    AllMappingsExact = mappingsExact,
                    BothQualityTiersOperational = lowStatus.IsOperational && highStatus.IsOperational,
                    BothQualityMappingsExact = lowMapped && highMapped,
                    LightningRequestOperational = lightningStatus.IsOperational,
                    RainParticlesOperational = rainParticlesOperational,
                    StormParticlesOperational = stormParticlesOperational,
                    TimeSimulationDisabled = manager.Time != null &&
                                             manager.Time.Settings != null &&
                                             !manager.Time.Settings.simulate,
                    AudioSilent = IsAudioSilent(manager.Audio),
                    ManagerCount = managers.Length,
                    EnabledDirectionalLightCount = enabledDirectionalLights,
                    GlobalVolumeCount = globalVolumes
                };
            }

            public static bool IsRainEffectOperational(
                EnviroEffectsModule effects,
                float expectedNormalizedEmission)
            {
                if (effects == null || effects.Settings == null || effects.Settings.effectTypes == null)
                {
                    return false;
                }

                for (int index = 0; index < effects.Settings.effectTypes.Count; index++)
                {
                    EnviroEffectTypes effect = effects.Settings.effectTypes[index];
                    if (effect == null || effect.name != "Rain")
                    {
                        continue;
                    }

                    ParticleSystem system = effect.mySystem;
                    ParticleSystemRenderer renderer =
                        system != null ? system.GetComponent<ParticleSystemRenderer>() : null;
                    Material material = renderer != null ? renderer.sharedMaterial : null;
                    float expectedEmission = effect.maxEmission * effect.emissionRate;
                    float actualEmission = system != null
                        ? system.emission.rateOverTime.constantMax
                        : 0f;

                    return effect.prefab != null &&
                           Mathf.Approximately(
                               effect.emissionRate,
                               expectedNormalizedEmission) &&
                           expectedEmission > 0f &&
                           system != null &&
                           system.isPlaying &&
                           system.particleCount > 0 &&
                           Mathf.Approximately(actualEmission, expectedEmission) &&
                           renderer != null && renderer.enabled &&
                           material != null && material.shader != null;
                }

                return false;
            }

            private static bool ApplyAndCheck(
                WeatherLabStateController controller,
                EnviroManager manager,
                EnvironmentPresentationPresetKind preset,
                EnviroWeatherType expected,
                bool expectRuntimeClone,
                ref bool mappingsExact)
            {
                EnvironmentPresentationStatus status = controller.ApplyPreset(preset);
                EnviroWeatherType actual = manager.Weather.targetWeatherType;
                mappingsExact &= expectRuntimeClone
                    ? actual != null && actual != expected
                    : actual == expected;
                return status.IsOperational;
            }

            private static bool IsAudioSilent(EnviroAudioModule audio)
            {
                return audio != null && audio.Settings != null &&
                       Mathf.Approximately(audio.Settings.ambientMasterVolume, 0f) &&
                       Mathf.Approximately(audio.Settings.weatherMasterVolume, 0f) &&
                       Mathf.Approximately(audio.Settings.thunderMasterVolume, 0f) &&
                       Mathf.Approximately(audio.ambientVolumeModifier, 0f) &&
                       Mathf.Approximately(audio.weatherVolumeModifier, 0f) &&
                       Mathf.Approximately(audio.thunderVolumeModifier, 0f);
            }

            private static T GetPrivateField<T>(object target, string fieldName)
            {
                FieldInfo field = target.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (field == null)
                {
                    throw new MissingFieldException(target.GetType().FullName, fieldName);
                }

                return (T)field.GetValue(target);
            }
        }
    }
}
