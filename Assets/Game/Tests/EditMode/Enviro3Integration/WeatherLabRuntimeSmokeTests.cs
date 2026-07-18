using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Enviro;
using MSC.Development.WeatherLab;
using MSC.Weather.Domain;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Presentation;
using MSC.Weather.Wetness;
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
                        SmokeObservations.IsRainEffectOperational(manager.Effects, 0.55f);

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
            Assert.That(observations.AllQualityTiersOperational, Is.True);
            Assert.That(observations.AllQualityMappingsExact, Is.True);
            Assert.That(observations.LightningRequestOperational, Is.True);
            Assert.That(observations.RainParticlesOperational, Is.True);
            Assert.That(observations.StormParticlesOperational, Is.True);
            Assert.That(observations.TimeSimulationDisabled, Is.True);
            Assert.That(observations.AudioSilent, Is.True);
            Assert.That(observations.ManagerCount, Is.EqualTo(1));
            Assert.That(observations.EnabledDirectionalLightCount, Is.EqualTo(1));
            Assert.That(observations.GlobalVolumeCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator WeatherLab_WetnessUsesPropertyBlocksAndKeepsGlobalSurfacesExterior()
        {
            EditorSceneManager.OpenScene(
                WeatherLabSceneMarker.SceneAssetPath,
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            yield return new EnterPlayMode();
            for (int frame = 0; frame < 8; frame++)
            {
                yield return null;
            }

            WetnessSmokeProbe probe = null;
            WetnessSmokeObservations observations = null;
            Exception runtimeException = null;
            try
            {
                probe = WetnessSmokeProbe.Begin();
            }
            catch (Exception exception)
            {
                runtimeException = exception;
            }

            if (runtimeException == null)
            {
                yield return new WaitForSecondsRealtime(0.25f);

                try
                {
                    observations = probe.Capture();
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
            Assert.That(observations.ConfiguredRendererCount, Is.GreaterThan(0));
            Assert.That(observations.PuddleRendererCount, Is.GreaterThan(0));
            Assert.That(observations.InitialSurfaceExposureWasExterior, Is.True);
            Assert.That(observations.InteriorCameraSwitchOperational, Is.True);
            Assert.That(observations.ListenerExposureBecameInterior, Is.True);
            Assert.That(observations.GlobalSurfaceExposureStayedExterior, Is.True);
            Assert.That(observations.PuddlePropertyBlockApplied, Is.True);
            Assert.That(observations.PuddleRippleAdvanced, Is.True);
            Assert.That(observations.PuddleRipplePropertyMatchesBridge, Is.True);
            Assert.That(observations.SharedMaterialSlotCountUnchanged, Is.True);
            Assert.That(observations.SharedMaterialReferencesUnchanged, Is.True);
        }

        private sealed class WetnessSmokeProbe
        {
            private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
            private static readonly int PuddleAmountId = Shader.PropertyToID("_MSC_PuddleAmount");
            private static readonly int PuddleRipplePhaseId =
                Shader.PropertyToID("_MSC_PuddleRipplePhase");
            private const string ExteriorExposureId = "exposure.exterior";

            private readonly WeatherLabStateController controller;
            private readonly WeatherLabWetnessMaterialBridge bridge;
            private readonly Renderer[] sampleRenderers;
            private readonly Renderer[] puddleRenderers;
            private readonly Material[][] sharedMaterialsBefore;
            private readonly uint rippleCountBefore;
            private readonly float ripplePhaseBefore;
            private readonly bool initialSurfaceExposureWasExterior;
            private readonly bool interiorCameraSwitchOperational;
            private readonly bool listenerExposureBecameInterior;
            private readonly bool globalSurfaceExposureStayedExterior;
            private readonly bool puddlePropertyBlockApplied;

            private WetnessSmokeProbe(
                WeatherLabStateController controller,
                WeatherLabWetnessMaterialBridge bridge,
                Renderer[] sampleRenderers,
                Renderer[] puddleRenderers,
                Material[][] sharedMaterialsBefore,
                uint rippleCountBefore,
                float ripplePhaseBefore,
                bool initialSurfaceExposureWasExterior,
                bool interiorCameraSwitchOperational,
                bool listenerExposureBecameInterior,
                bool globalSurfaceExposureStayedExterior,
                bool puddlePropertyBlockApplied)
            {
                this.controller = controller;
                this.bridge = bridge;
                this.sampleRenderers = sampleRenderers;
                this.puddleRenderers = puddleRenderers;
                this.sharedMaterialsBefore = sharedMaterialsBefore;
                this.rippleCountBefore = rippleCountBefore;
                this.ripplePhaseBefore = ripplePhaseBefore;
                this.initialSurfaceExposureWasExterior = initialSurfaceExposureWasExterior;
                this.interiorCameraSwitchOperational = interiorCameraSwitchOperational;
                this.listenerExposureBecameInterior = listenerExposureBecameInterior;
                this.globalSurfaceExposureStayedExterior = globalSurfaceExposureStayedExterior;
                this.puddlePropertyBlockApplied = puddlePropertyBlockApplied;
            }

            public static WetnessSmokeProbe Begin()
            {
                WeatherLabStateController controller =
                    Object.FindFirstObjectByType<WeatherLabStateController>();
                WeatherLabWetnessMaterialBridge bridge =
                    Object.FindFirstObjectByType<WeatherLabWetnessMaterialBridge>();
                Assert.That(controller, Is.Not.Null);
                Assert.That(bridge, Is.Not.Null);

                Renderer[] sampleRenderers = CollectConfiguredRenderers(bridge);
                Renderer[] puddleRenderers = GetPrivateField<Renderer[]>(bridge, "puddleSamples");
                Material[][] sharedMaterialsBefore = CaptureSharedMaterials(sampleRenderers);
                bool initialSurfaceExposureWasExterior =
                    controller.Wetness.ExposureProfileId == ExteriorExposureId;

                controller.SetWetness(1f, 1f, 1f, 1f);
                bool puddlePropertyBlockApplied =
                    PuddlePropertyBlockMatches(puddleRenderers, 1f, bridge.PuddleRipplePhase01);
                uint rippleCountBefore = bridge.RippleUpdateCount;
                float ripplePhaseBefore = bridge.PuddleRipplePhase01;

                bool interiorCameraSwitchOperational = controller.SwitchCamera(1);
                bool listenerExposureBecameInterior =
                    controller.ExposureContext == WeatherExposureContext.Interior;
                controller.SetWetness(0.9f, 0.85f, 0.75f, 0.8f);
                bool globalSurfaceExposureStayedExterior =
                    controller.Wetness.ExposureProfileId == SurfaceExposureProfile.Exterior.StableId;

                return new WetnessSmokeProbe(
                    controller,
                    bridge,
                    sampleRenderers,
                    puddleRenderers,
                    sharedMaterialsBefore,
                    rippleCountBefore,
                    ripplePhaseBefore,
                    initialSurfaceExposureWasExterior,
                    interiorCameraSwitchOperational,
                    listenerExposureBecameInterior,
                    globalSurfaceExposureStayedExterior,
                    puddlePropertyBlockApplied);
            }

            public WetnessSmokeObservations Capture()
            {
                Material[][] sharedMaterialsAfter = CaptureSharedMaterials(sampleRenderers);
                bool slotCountsUnchanged = SharedMaterialSlotCountsMatch(
                    sharedMaterialsBefore,
                    sharedMaterialsAfter);
                bool referencesUnchanged = SharedMaterialReferencesMatch(
                    sharedMaterialsBefore,
                    sharedMaterialsAfter);
                bool rippleAdvanced =
                    bridge.RippleUpdateCount > rippleCountBefore &&
                    !Mathf.Approximately(bridge.PuddleRipplePhase01, ripplePhaseBefore);
                bool ripplePropertyMatches = PuddlePropertyBlockMatches(
                    puddleRenderers,
                    controller.Wetness.PuddleAmount01,
                    bridge.PuddleRipplePhase01);
                bool stayedExterior = globalSurfaceExposureStayedExterior &&
                    controller.Wetness.ExposureProfileId == SurfaceExposureProfile.Exterior.StableId;
                bool stayedInterior = listenerExposureBecameInterior &&
                    controller.ExposureContext == WeatherExposureContext.Interior;

                return new WetnessSmokeObservations
                {
                    ConfiguredRendererCount = bridge.ConfiguredRendererCount,
                    PuddleRendererCount = puddleRenderers.Length,
                    InitialSurfaceExposureWasExterior = initialSurfaceExposureWasExterior,
                    InteriorCameraSwitchOperational = interiorCameraSwitchOperational,
                    ListenerExposureBecameInterior = stayedInterior,
                    GlobalSurfaceExposureStayedExterior = stayedExterior,
                    PuddlePropertyBlockApplied = puddlePropertyBlockApplied,
                    PuddleRippleAdvanced = rippleAdvanced,
                    PuddleRipplePropertyMatchesBridge = ripplePropertyMatches,
                    SharedMaterialSlotCountUnchanged = slotCountsUnchanged,
                    SharedMaterialReferencesUnchanged = referencesUnchanged
                };
            }

            private static Renderer[] CollectConfiguredRenderers(
                WeatherLabWetnessMaterialBridge bridge)
            {
                string[] fieldNames =
                {
                    "groundSamples",
                    "roadSamples",
                    "exteriorSamples",
                    "vehicleSamples",
                    "vegetationSamples",
                    "puddleSamples"
                };
                var renderers = new List<Renderer>();
                var instanceIds = new HashSet<int>();
                for (int fieldIndex = 0; fieldIndex < fieldNames.Length; fieldIndex++)
                {
                    Renderer[] values = GetPrivateField<Renderer[]>(bridge, fieldNames[fieldIndex]);
                    for (int rendererIndex = 0; rendererIndex < values.Length; rendererIndex++)
                    {
                        Renderer renderer = values[rendererIndex];
                        if (renderer != null && instanceIds.Add(renderer.GetInstanceID()))
                        {
                            renderers.Add(renderer);
                        }
                    }
                }

                return renderers.ToArray();
            }

            private static Material[][] CaptureSharedMaterials(Renderer[] renderers)
            {
                var result = new Material[renderers.Length][];
                for (int index = 0; index < renderers.Length; index++)
                {
                    result[index] = renderers[index].sharedMaterials;
                }

                return result;
            }

            private static bool SharedMaterialSlotCountsMatch(
                Material[][] before,
                Material[][] after)
            {
                if (before.Length != after.Length)
                {
                    return false;
                }

                for (int index = 0; index < before.Length; index++)
                {
                    if (before[index].Length != after[index].Length)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool SharedMaterialReferencesMatch(
                Material[][] before,
                Material[][] after)
            {
                if (!SharedMaterialSlotCountsMatch(before, after))
                {
                    return false;
                }

                for (int rendererIndex = 0; rendererIndex < before.Length; rendererIndex++)
                {
                    for (int materialIndex = 0;
                         materialIndex < before[rendererIndex].Length;
                         materialIndex++)
                    {
                        if (before[rendererIndex][materialIndex] !=
                            after[rendererIndex][materialIndex])
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            private static bool PuddlePropertyBlockMatches(
                Renderer[] puddleRenderers,
                float expectedAmount01,
                float expectedPhase01)
            {
                if (puddleRenderers == null || puddleRenderers.Length == 0)
                {
                    return false;
                }

                var block = new MaterialPropertyBlock();
                for (int index = 0; index < puddleRenderers.Length; index++)
                {
                    Renderer renderer = puddleRenderers[index];
                    if (renderer == null || !renderer.HasPropertyBlock())
                    {
                        return false;
                    }

                    renderer.GetPropertyBlock(block);
                    Color color = block.GetColor(BaseColorId);
                    if (!Mathf.Approximately(block.GetFloat(PuddleAmountId), expectedAmount01) ||
                        !Mathf.Approximately(block.GetFloat(PuddleRipplePhaseId), expectedPhase01) ||
                        color.a <= 0f)
                    {
                        return false;
                    }
                }

                return true;
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

        private sealed class WetnessSmokeObservations
        {
            public int ConfiguredRendererCount { get; set; }

            public int PuddleRendererCount { get; set; }

            public bool InitialSurfaceExposureWasExterior { get; set; }

            public bool InteriorCameraSwitchOperational { get; set; }

            public bool ListenerExposureBecameInterior { get; set; }

            public bool GlobalSurfaceExposureStayedExterior { get; set; }

            public bool PuddlePropertyBlockApplied { get; set; }

            public bool PuddleRippleAdvanced { get; set; }

            public bool PuddleRipplePropertyMatchesBridge { get; set; }

            public bool SharedMaterialSlotCountUnchanged { get; set; }

            public bool SharedMaterialReferencesUnchanged { get; set; }
        }

        private sealed class SmokeObservations
        {
            public bool AdapterAttached { get; private set; }

            public bool AllStatesOperational { get; private set; }

            public bool AllMappingsExact { get; private set; }

            public bool AllQualityTiersOperational { get; private set; }

            public bool AllQualityMappingsExact { get; private set; }

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
                    expectRuntimeClone: true,
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
                EnvironmentPresentationStatus mediumStatus =
                    controller.ApplyQuality(EnvironmentQualityTier.Medium);
                bool mediumMapped =
                    manager.Quality.Settings.defaultQuality == bindings.Medium;
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
                    AllQualityTiersOperational =
                        lowStatus.IsOperational &&
                        mediumStatus.IsOperational &&
                        highStatus.IsOperational,
                    AllQualityMappingsExact =
                        lowMapped && mediumMapped && highMapped,
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
