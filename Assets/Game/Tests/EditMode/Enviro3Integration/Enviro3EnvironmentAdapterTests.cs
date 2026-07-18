using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Enviro;
using MSC.Weather.Domain;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.Enviro3Integration
{
    public sealed class Enviro3EnvironmentAdapterTests
    {
        private const string MissingReferenceDiagnosticCode = "ENVIRO3-ATTACH-001";

        [Test]
        public void ProductionVisualPolicy_MapsVisibilityAndExposureDeterministically()
        {
            var exposure = AnimationCurve.Linear(0f, 1.2f, 1f, 12.7f);

            Assert.That(
                Enviro3ProductionVisualPolicy.CalculateFogMeanFreePathMeters(
                    20000f),
                Is.EqualTo(5112.474f).Within(0.01f));
            Assert.That(
                Enviro3ProductionVisualPolicy.CalculateFogMeanFreePathMeters(
                    5500f),
                Is.EqualTo(1405.93f).Within(0.01f));
            Assert.That(
                Enviro3ProductionVisualPolicy.CalculateFogMeanFreePathMeters(
                    1200f),
                Is.EqualTo(306.748f).Within(0.01f));
            Assert.That(
                Enviro3ProductionVisualPolicy
                    .ResolveRainParticleMaxScreenSize(0.001f),
                Is.EqualTo(0.01f));

            float exterior =
                Enviro3ProductionVisualPolicy.CalculateExposureEv(
                    exposure,
                    0.5f,
                    WeatherExposureContext.Exterior);
            float sheltered =
                Enviro3ProductionVisualPolicy.CalculateExposureEv(
                    exposure,
                    0.5f,
                    WeatherExposureContext.Sheltered);
            float interior =
                Enviro3ProductionVisualPolicy.CalculateExposureEv(
                    exposure,
                    0.5f,
                    WeatherExposureContext.Interior);

            Assert.That(
                exterior,
                Is.EqualTo(exposure.Evaluate(0.5f) + 0.25f)
                    .Within(0.0001f));
            Assert.That(
                sheltered,
                Is.EqualTo(exterior - 0.15f).Within(0.0001f));
            Assert.That(
                interior,
                Is.EqualTo(exterior - 0.25f).Within(0.0001f));

            var lowExposure = AnimationCurve.Constant(0f, 1f, 4f);
            float fullNight =
                Enviro3ProductionVisualPolicy.CalculateExposureEv(
                    lowExposure,
                    0.4f,
                    WeatherExposureContext.Exterior);
            float fullNightInterior =
                Enviro3ProductionVisualPolicy.CalculateExposureEv(
                    lowExposure,
                    0.4f,
                    WeatherExposureContext.Interior);
            float twilight =
                Enviro3ProductionVisualPolicy.CalculateExposureEv(
                    lowExposure,
                    0.465f,
                    WeatherExposureContext.Exterior);
            float daylight =
                Enviro3ProductionVisualPolicy.CalculateExposureEv(
                    lowExposure,
                    0.5f,
                    WeatherExposureContext.Exterior);

            Assert.That(
                fullNight,
                Is.EqualTo(
                        Enviro3ProductionVisualPolicy
                            .MinimumNightExposureEv)
                    .Within(0.0001f));
            Assert.That(
                fullNightInterior,
                Is.EqualTo(fullNight - 0.25f).Within(0.0001f));
            Assert.That(twilight, Is.GreaterThan(daylight));
            Assert.That(twilight, Is.LessThan(fullNight));
            Assert.That(
                daylight,
                Is.EqualTo(4.25f).Within(0.0001f),
                "The night exposure floor must not change daylight exposure.");
            Assert.That(
                Enviro3ProductionVisualPolicy
                    .ProductionLatitudeDegrees,
                Is.EqualTo(60f));
            Assert.That(
                Enviro3ProductionVisualPolicy
                    .ProductionLongitudeDegrees,
                Is.EqualTo(27.3f));
            Assert.That(
                Enviro3ProductionVisualPolicy
                    .ProductionUtcOffsetHours,
                Is.EqualTo(3));
            Assert.That(
                Enviro3ProductionVisualPolicy
                    .TemporaryBaselineReflectionIntensity,
                Is.InRange(0f, 1f));
        }

        [Test]
        public void AttachWithoutRuntimeOrReferences_FaultsClosedWithDiagnostic()
        {
            GameObject gameObject = new GameObject("Enviro3 Missing References Test");
            try
            {
                Enviro3EnvironmentAdapter adapter = gameObject.AddComponent<Enviro3EnvironmentAdapter>();

                EnvironmentPresentationStatus status = adapter.Attach();

                Assert.That(adapter.IsAttached, Is.False);
                Assert.That(adapter.Capabilities, Is.EqualTo(EnvironmentPresentationCapabilities.None));
                Assert.That(status.State, Is.EqualTo(EnvironmentPresentationState.Faulted));
                Assert.That(status.IsOperational, Is.False);
                Assert.That(status.ErrorCount, Is.GreaterThanOrEqualTo(1));
                Assert.That(ContainsMissingReferenceError(adapter), Is.True);

                adapter.Detach();
                adapter.Detach();
                Assert.That(adapter.Status.State, Is.EqualTo(EnvironmentPresentationState.Detached));
                Assert.That(adapter.IsAttached, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [UnityTest]
        public IEnumerator RuntimeLifecycle_IsolatesSourceAndDisablesEnviroAutonomyAndAudio()
        {
            yield return new EnterPlayMode();

            Scene isolatedScene = SceneManager.CreateScene("Enviro3IntegrationTests_" + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(isolatedScene);
            List<AsyncOperation> unloadOperations = new List<AsyncOperation>();
            for (int index = SceneManager.sceneCount - 1; index >= 0; index--)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene == isolatedScene || !scene.isLoaded)
                {
                    continue;
                }

                AsyncOperation operation = SceneManager.UnloadSceneAsync(scene);
                if (operation != null)
                {
                    unloadOperations.Add(operation);
                }
            }

            bool scenesAreUnloading = true;
            while (scenesAreUnloading)
            {
                scenesAreUnloading = false;
                for (int index = 0; index < unloadOperations.Count; index++)
                {
                    scenesAreUnloading |= !unloadOperations[index].isDone;
                }

                if (scenesAreUnloading)
                {
                    yield return null;
                }
            }

            ResetEnviroManagerSingleton();
            RuntimeFixture fixture = new RuntimeFixture();
            RuntimeObservations observations = null;
            Exception runtimeException = null;
            try
            {
                fixture.Initialize();
            }
            catch (Exception exception)
            {
                runtimeException = exception;
            }

            if (runtimeException == null)
            {
                yield return null;
                yield return null;
                yield return null;

                try
                {
                    observations = RuntimeObservations.Capture(fixture);
                }
                catch (Exception exception)
                {
                    runtimeException = exception;
                }
            }

            fixture.BeginTeardown();
            yield return null;
            bool runtimeLightningMaterialReleased =
                observations == null || observations.RuntimeLightningFlashMaterial == null;
            int volumetricCloudDisableCallCount =
                TrackingVolumetricCloudsModule.DisableCallCount;
            fixture.FinishTeardown();
            yield return null;
            ResetEnviroManagerSingleton();

            yield return new ExitPlayMode();

            Assert.That(runtimeException, Is.Null, runtimeException == null ? string.Empty : runtimeException.ToString());
            Assert.That(observations, Is.Not.Null);
            Assert.That(observations.InitiallyAttached, Is.True);
            Assert.That(observations.InitialState, Is.EqualTo(EnvironmentPresentationState.Ready));
            Assert.That(observations.InitialErrorCount, Is.Zero);
            Assert.That(observations.InitialCapabilities, Is.Not.EqualTo(EnvironmentPresentationCapabilities.None));
            Assert.That(observations.PresentationCameraApplied, Is.True);
            Assert.That(observations.RuntimeConfigurationIsolated, Is.True);
            Assert.That(observations.RequiredModulesIsolated, Is.True);
            Assert.That(observations.MutableSettingsIsolated, Is.True);
            Assert.That(observations.SourceConfigurationGraphUnchanged, Is.True);
            Assert.That(observations.SourceAutonomyUnchanged, Is.True);
            Assert.That(observations.RuntimeTimeSimulationDisabled, Is.True);
            Assert.That(observations.RuntimeAutomaticLightningDisabled, Is.True);
            Assert.That(observations.RuntimeSeasonAutonomyDisabled, Is.True);
            Assert.That(observations.RuntimeWetnessSnowAutonomyDisabled, Is.True);
            Assert.That(observations.SourceEnvironmentUnchanged, Is.True);
            Assert.That(observations.RuntimeAudioSilent, Is.True);
            Assert.That(observations.SourceAudioUnchanged, Is.True);
            Assert.That(observations.RuntimeRainEffectBound, Is.True);
            Assert.That(observations.RuntimePrecipitationProfilesIsolated, Is.True);
            Assert.That(observations.PrecipitationMappingsAreDistinct, Is.True);
            Assert.That(observations.PrecipitationIntensityUpdatedWithoutRestart, Is.True);
            Assert.That(observations.SourcePrecipitationProfilesUnchanged, Is.True);
            Assert.That(observations.RuntimeStormIsolated, Is.True);
            Assert.That(observations.RuntimeStormAutomaticLightningDisabled, Is.True);
            Assert.That(observations.SourceStormUnchanged, Is.True);
            Assert.That(observations.RepeatedAttachStayedReady, Is.True);
            Assert.That(observations.FirstDetachWasDetached, Is.True);
            Assert.That(observations.SecondDetachWasDetached, Is.True);
            Assert.That(observations.RuntimeStormReleasedOnDetach, Is.True);
            Assert.That(observations.RepeatedLightningReusedMaterial, Is.True);
            Assert.That(observations.LightningPrefabMaterialRestored, Is.True);
            Assert.That(observations.RuntimeLightningPrefabIsolated, Is.True);
            Assert.That(observations.SmoothTransitionUsedTypedWeatherPath, Is.True);
            Assert.That(observations.SmoothTransitionRateApproximatedDuration, Is.True);
            Assert.That(observations.TimeOnlyRevisionDidNotRestartWeatherTransition, Is.True);
            Assert.That(observations.ImmediateBindingChangeUsedInstantPath, Is.True);
            Assert.That(observations.LightningRequestUsedWorldPosition, Is.True);
            Assert.That(observations.LightningSequenceWasDeduplicated, Is.True);
            Assert.That(observations.LightningIntensityLimitationWasReported, Is.True);
            Assert.That(observations.EnvironmentRefreshCapabilityExposed, Is.True);
            Assert.That(observations.RefreshSequenceWasDeduplicatedAndCooledDown, Is.True);
            Assert.That(observations.MediumQualityMapped, Is.True);
            Assert.That(observations.ProjectOwnedWindApplied, Is.True);
            Assert.That(observations.SingleEnviroWindOwner, Is.True);
            Assert.That(observations.AdapterHasSingleBoundedLateUpdateWriter, Is.True);
            Assert.That(observations.LateDuplicateOwnershipFailedClosed, Is.True);
            Assert.That(runtimeLightningMaterialReleased, Is.True);
            Assert.That(
                volumetricCloudDisableCallCount,
                Is.EqualTo(1),
                "Teardown must disable the live volumetric-cloud module exactly once.");
        }

        private static void ResetEnviroManagerSingleton()
        {
            SetEnviroManagerSingleton(null);
        }

        private static void SetEnviroManagerSingleton(EnviroManager manager)
        {
            FieldInfo instanceField = typeof(EnviroManager).GetField(
                "_instance",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (instanceField == null)
            {
                throw new MissingFieldException(typeof(EnviroManager).FullName, "_instance");
            }

            instanceField.SetValue(null, manager);
        }

        private static bool ContainsMissingReferenceError(Enviro3EnvironmentAdapter adapter)
        {
            for (int index = 0; index < adapter.Diagnostics.Count; index++)
            {
                EnvironmentPresentationDiagnostic diagnostic = adapter.Diagnostics[index];
                if (diagnostic.Code == MissingReferenceDiagnosticCode &&
                    diagnostic.Severity == EnvironmentDiagnosticSeverity.Error)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class RuntimeFixture
        {
            private readonly List<ScriptableObject> authoredObjects = new List<ScriptableObject>();

            private GameObject adapterGameObject;
            private GameObject managerGameObject;
            private GameObject cameraGameObject;
            private GameObject lightningPrefabGameObject;
            private GameObject rainPrefabGameObject;
            private Material lightningFlashMaterial;

            public Enviro3EnvironmentAdapter Adapter { get; private set; }

            public EnviroManager Manager { get; private set; }

            public Camera PresentationCamera { get; private set; }

            public Enviro3EnvironmentBindings Bindings { get; private set; }

            public EnviroConfiguration SourceConfiguration { get; private set; }

            public EnviroEffectsModule EffectsSource { get; private set; }

            public EnviroWeatherType SourceClear { get; private set; }

            public EnviroWeatherType SourceRain { get; private set; }

            public EnviroWeatherType SourceStorm { get; private set; }

            public EnviroQuality LowQuality { get; private set; }

            public EnviroQuality MediumQuality { get; private set; }

            public EnviroQuality HighQuality { get; private set; }

            public Lightning LightningPrefab { get; private set; }

            public Material SourceLightningFlashMaterial => lightningFlashMaterial;

            public void Initialize()
            {
                TrackingVolumetricCloudsModule.DisableCallCount = 0;
                SourceConfiguration = Create<EnviroConfiguration>();
                SourceConfiguration.name = "Enviro Source Configuration";
                CreateSourceModules(SourceConfiguration);

                SourceClear = Create<EnviroWeatherType>();
                EnviroWeatherType partlyCloudy = Create<EnviroWeatherType>();
                EnviroWeatherType overcast = Create<EnviroWeatherType>();
                SourceRain = Create<EnviroWeatherType>();
                SourceRain.effectsOverride = CreateRainOverride(0.5f);
                SourceStorm = Create<EnviroWeatherType>();
                SourceStorm.effectsOverride = CreateRainOverride(1f);
                SourceStorm.lightningOverride = new EnviroWeatherTypeLightningOverride
                {
                    lightningStorm = true
                };
                EnviroWeatherType fog = Create<EnviroWeatherType>();
                LowQuality = CreateQuality(32);
                MediumQuality = CreateQuality(48);
                HighQuality = CreateQuality(64);

                rainPrefabGameObject = new GameObject("Enviro Rain Prefab Test Double");
                rainPrefabGameObject.SetActive(false);
                ParticleSystem sourceRainSystem =
                    rainPrefabGameObject.AddComponent<ParticleSystem>();
                sourceRainSystem.GetComponent<ParticleSystemRenderer>()
                    .maxParticleSize = 0.001f;
                EffectsSource = Create<EnviroEffectsModule>();
                EffectsSource.Settings = new EnviroEffects();
                EffectsSource.Settings.effectTypes.Add(new EnviroEffectTypes
                {
                    name = "Rain",
                    prefab = rainPrefabGameObject,
                    maxEmission = 2000f
                });

                Bindings = Create<Enviro3EnvironmentBindings>();
                Bindings.ConfigureForAuthoring(
                    SourceConfiguration,
                    EffectsSource,
                    SourceClear,
                    partlyCloudy,
                    overcast,
                    SourceRain,
                    SourceStorm,
                    fog,
                    LowQuality,
                    MediumQuality,
                    HighQuality);

                lightningPrefabGameObject = new GameObject("Enviro Lightning Prefab Test Double");
                lightningPrefabGameObject.SetActive(false);
                LightningPrefab = lightningPrefabGameObject.AddComponent<Lightning>();
                lightningFlashMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
                LightningPrefab.planeMat = lightningFlashMaterial;
                SourceConfiguration.Lightning.Settings.prefab = LightningPrefab;

                managerGameObject = new GameObject("Enviro Manager Test Double");
                managerGameObject.SetActive(false);
                Manager = managerGameObject.AddComponent<EnviroManager>();
                Manager.enabled = false;
                VolumeProfile volumeProfile = Create<VolumeProfile>();
                volumeProfile.name = "Production HDRP Volume Test Double";
                volumeProfile.Add<Fog>(true);
                volumeProfile.Add<Exposure>(true);
                Volume volume = managerGameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = volumeProfile;
                Manager.volumeHDRP = volume;
                Manager.Objects.sun = new GameObject(
                    "Enviro Sun Test Double");
                Manager.Objects.sun.transform.SetParent(
                    managerGameObject.transform,
                    false);
                Manager.Objects.moon = new GameObject(
                    "Enviro Moon Test Double");
                Manager.Objects.moon.transform.SetParent(
                    managerGameObject.transform,
                    false);
                Manager.Objects.stars = new GameObject(
                    "Enviro Stars Test Double");
                Manager.Objects.stars.transform.SetParent(
                    managerGameObject.transform,
                    false);
                GameObject windZoneObject = new GameObject("Enviro Wind Zone Test Double");
                windZoneObject.transform.SetParent(managerGameObject.transform, false);
                Manager.Objects.windZone = windZoneObject.AddComponent<WindZone>();
                managerGameObject.SetActive(true);
                SetEnviroManagerSingleton(Manager);

                cameraGameObject = new GameObject("Enviro Presentation Camera Test Double");
                PresentationCamera = cameraGameObject.AddComponent<Camera>();
                PresentationCamera.enabled = false;

                adapterGameObject = new GameObject("Enviro Adapter Test Double");
                adapterGameObject.SetActive(false);
                Adapter = adapterGameObject.AddComponent<Enviro3EnvironmentAdapter>();
                Adapter.ConfigureForAuthoring(
                    Manager,
                    PresentationCamera,
                    Bindings,
                    adapterGameObject.transform,
                    adapterGameObject.transform);
                adapterGameObject.SetActive(true);
            }

            public void BeginTeardown()
            {
                if (adapterGameObject == null)
                {
                    return;
                }

                adapterGameObject.SetActive(false);
                Object.Destroy(adapterGameObject);
                adapterGameObject = null;
            }

            public void FinishTeardown()
            {
                DestroyGameObject(managerGameObject);
                DestroyGameObject(cameraGameObject);
                DestroyGameObject(lightningPrefabGameObject);
                DestroyGameObject(rainPrefabGameObject);
                if (lightningFlashMaterial != null)
                {
                    Object.Destroy(lightningFlashMaterial);
                }

                managerGameObject = null;
                cameraGameObject = null;
                lightningPrefabGameObject = null;
                rainPrefabGameObject = null;
                lightningFlashMaterial = null;

                for (int index = authoredObjects.Count - 1; index >= 0; index--)
                {
                    if (authoredObjects[index] != null)
                    {
                        Object.Destroy(authoredObjects[index]);
                    }
                }

                authoredObjects.Clear();
            }

            private void CreateSourceModules(EnviroConfiguration configuration)
            {
                configuration.timeModule = Create<EnviroTimeModule>();
                configuration.timeModule.Settings = new EnviroTime { simulate = true };

                configuration.Sky = Create<EnviroSkyModule>();
                configuration.Sky.Settings = new EnviroSky();

                configuration.lightingModule = Create<EnviroLightingModule>();
                configuration.lightingModule.Settings = CreateLightingSettings();

                configuration.fogModule = Create<EnviroFogModule>();
                configuration.fogModule.Settings = new EnviroFogSettings();

                configuration.volumetricCloudModule = Create<TrackingVolumetricCloudsModule>();

                configuration.Weather = Create<EnviroWeatherModule>();
                configuration.Weather.Settings = new EnviroWeather();

                configuration.Effects = Create<EnviroEffectsModule>();
                configuration.Effects.Settings = new EnviroEffects();

                configuration.Lightning = Create<EnviroLightningModule>();
                configuration.Lightning.Settings = new EnviroLightning
                {
                    lightningStorm = true
                };

                configuration.Quality = Create<EnviroQualityModule>();
                configuration.Quality.Settings = new EnviroQualities();

                configuration.Environment = Create<EnviroEnvironmentModule>();
                configuration.Environment.Settings = new EnviroEnvironment
                {
                    season = EnviroEnvironment.Seasons.Winter,
                    changeSeason = true,
                    wetness = 0.65f,
                    wetnessTarget = 0.8f,
                    snow = 0.45f,
                    snowTarget = 0.7f,
                    wetnessAccumulationSpeed = 0.6f,
                    wetnessDrySpeed = 0.5f,
                    snowAccumulationSpeed = 0.4f,
                    snowMeltSpeed = 0.3f,
                    windDirectionX = -0.25f,
                    windDirectionY = 0.75f,
                    windSpeed = 0.9f,
                    windTurbulence = 0.8f
                };

                configuration.Audio = Create<EnviroAudioModule>();
                configuration.Audio.Settings = CreateUnsafeAudioSettings();
                configuration.Audio.ambientVolumeModifier = 0.6f;
                configuration.Audio.weatherVolumeModifier = 0.5f;
                configuration.Audio.thunderVolumeModifier = 0.4f;
            }

            private EnviroQuality CreateQuality(int fogSteps)
            {
                EnviroQuality quality = Create<EnviroQuality>();
                quality.volumetricCloudsOverride = new EnviroVolumetricCloudsQualitySettings();
                quality.fogOverride = new EnviroFogQualitySettings { steps = fogSteps };
                quality.flatCloudsOverride = new EnviroFlatCloudsQualitySettings();
                quality.auroraOverride = new EnviroAuroraQualitySettings();
                return quality;
            }

            private static EnviroWeatherTypeEffectsOverride CreateRainOverride(float emission)
            {
                var result = new EnviroWeatherTypeEffectsOverride();
                result.effectsOverride.Add(new EnviroEffectsOverrideType
                {
                    name = "Rain",
                    emission = emission
                });
                return result;
            }

            private static EnviroLighting CreateLightingSettings()
            {
                return new EnviroLighting
                {
                    ambientMode = UnityEngine.Rendering.AmbientMode.Flat,
                    ambientIntensityCurve = AnimationCurve.Constant(0f, 1f, 1f),
                    ambientSkyColorGradient = new Gradient(),
                    ambientEquatorColorGradient = new Gradient(),
                    ambientGroundColorGradient = new Gradient()
                };
            }

            private static EnviroAudio CreateUnsafeAudioSettings()
            {
                EnviroAudio audio = new EnviroAudio
                {
                    ambientMasterVolume = 0.9f,
                    weatherMasterVolume = 0.8f,
                    thunderMasterVolume = 0.7f
                };
                audio.ambientClips.Add(new EnviroAudioClip { volume = 0.65f });
                audio.weatherClips.Add(new EnviroAudioClip { volume = 0.55f });
                audio.thunderClips.Add(new EnviroAudioClip { volume = 0.45f });
                return audio;
            }

            private T Create<T>() where T : ScriptableObject
            {
                T instance = ScriptableObject.CreateInstance<T>();
                authoredObjects.Add(instance);
                return instance;
            }

            private static void DestroyGameObject(GameObject gameObject)
            {
                if (gameObject != null)
                {
                    Object.Destroy(gameObject);
                }
            }
        }

        private sealed class TrackingVolumetricCloudsModule : EnviroVolumetricCloudsModule
        {
            public static int DisableCallCount { get; set; }

            public override void Disable()
            {
                DisableCallCount++;
            }
        }

        private sealed class RuntimeObservations
        {
            public bool InitiallyAttached { get; private set; }

            public EnvironmentPresentationState InitialState { get; private set; }

            public int InitialErrorCount { get; private set; }

            public EnvironmentPresentationCapabilities InitialCapabilities { get; private set; }

            public bool PresentationCameraApplied { get; private set; }

            public bool RuntimeConfigurationIsolated { get; private set; }

            public bool RequiredModulesIsolated { get; private set; }

            public bool MutableSettingsIsolated { get; private set; }

            public bool SourceConfigurationGraphUnchanged { get; private set; }

            public bool SourceAutonomyUnchanged { get; private set; }

            public bool RuntimeTimeSimulationDisabled { get; private set; }

            public bool RuntimeAutomaticLightningDisabled { get; private set; }

            public bool RuntimeSeasonAutonomyDisabled { get; private set; }

            public bool RuntimeWetnessSnowAutonomyDisabled { get; private set; }

            public bool SourceEnvironmentUnchanged { get; private set; }

            public bool RuntimeAudioSilent { get; private set; }

            public bool SourceAudioUnchanged { get; private set; }

            public bool RuntimeRainEffectBound { get; private set; }

            public bool RuntimePrecipitationProfilesIsolated { get; private set; }

            public bool PrecipitationMappingsAreDistinct { get; private set; }

            public bool PrecipitationIntensityUpdatedWithoutRestart { get; private set; }

            public bool SourcePrecipitationProfilesUnchanged { get; private set; }

            public bool RuntimeStormIsolated { get; private set; }

            public bool RuntimeStormAutomaticLightningDisabled { get; private set; }

            public bool SourceStormUnchanged { get; private set; }

            public bool RepeatedAttachStayedReady { get; private set; }

            public bool FirstDetachWasDetached { get; private set; }

            public bool SecondDetachWasDetached { get; private set; }

            public bool RuntimeStormReleasedOnDetach { get; private set; }

            public bool RepeatedLightningReusedMaterial { get; private set; }

            public bool LightningPrefabMaterialRestored { get; private set; }

            public bool RuntimeLightningPrefabIsolated { get; private set; }

            public Material RuntimeLightningFlashMaterial { get; private set; }

            public bool SmoothTransitionUsedTypedWeatherPath { get; private set; }

            public bool SmoothTransitionRateApproximatedDuration { get; private set; }

            public bool TimeOnlyRevisionDidNotRestartWeatherTransition { get; private set; }

            public bool ImmediateBindingChangeUsedInstantPath { get; private set; }

            public bool LightningRequestUsedWorldPosition { get; private set; }

            public bool LightningSequenceWasDeduplicated { get; private set; }

            public bool LightningIntensityLimitationWasReported { get; private set; }

            public bool EnvironmentRefreshCapabilityExposed { get; private set; }

            public bool RefreshSequenceWasDeduplicatedAndCooledDown { get; private set; }

            public bool MediumQualityMapped { get; private set; }

            public bool ProjectOwnedWindApplied { get; private set; }

            public bool SingleEnviroWindOwner { get; private set; }

            public bool AdapterHasSingleBoundedLateUpdateWriter { get; private set; }

            public bool LateDuplicateOwnershipFailedClosed { get; private set; }

            public static RuntimeObservations Capture(RuntimeFixture fixture)
            {
                EnviroManager manager = fixture.Manager;
                EnviroConfiguration source = fixture.SourceConfiguration;
                Enviro3EnvironmentAdapter adapter = fixture.Adapter;
                EnvironmentPresentationStatus initialStatus = adapter.Status;
                EnviroWeatherType runtimeDrizzle =
                    GetPrivateField<EnviroWeatherType>(adapter, "runtimeDrizzle");
                EnviroWeatherType runtimeRain =
                    GetPrivateField<EnviroWeatherType>(adapter, "runtimeRain");
                EnviroWeatherType runtimeHeavyRain =
                    GetPrivateField<EnviroWeatherType>(adapter, "runtimeHeavyRain");
                EnviroWeatherType runtimeStorm = GetPrivateField<EnviroWeatherType>(adapter, "runtimeStorm");
                Lightning runtimeLightningPrefab =
                    GetPrivateField<Lightning>(adapter, "runtimeLightningPrefab");

                RuntimeObservations observations = new RuntimeObservations
                {
                    InitiallyAttached = adapter.IsAttached,
                    InitialState = initialStatus.State,
                    InitialErrorCount = initialStatus.ErrorCount,
                    InitialCapabilities = adapter.Capabilities,
                    PresentationCameraApplied = manager.Camera == fixture.PresentationCamera,
                    RuntimeConfigurationIsolated =
                        manager.configuration != null && manager.configuration != source,
                    RequiredModulesIsolated = AreRequiredModulesIsolated(manager, source),
                    MutableSettingsIsolated = AreMutableSettingsIsolated(
                        manager,
                        source,
                        fixture.EffectsSource),
                    SourceConfigurationGraphUnchanged = IsSourceGraphIntact(fixture),
                    SourceAutonomyUnchanged =
                        source.timeModule.Settings.simulate &&
                        source.Lightning.Settings.lightningStorm &&
                        source.Environment.Settings.changeSeason,
                    RuntimeTimeSimulationDisabled = !manager.Time.Settings.simulate,
                    RuntimeAutomaticLightningDisabled = !manager.Lightning.Settings.lightningStorm,
                    RuntimeSeasonAutonomyDisabled =
                        !manager.Environment.Settings.changeSeason &&
                        manager.Environment.Settings.season ==
                        EnviroEnvironment.Seasons.Summer,
                    RuntimeWetnessSnowAutonomyDisabled =
                        IsWetnessAndSnowAuthorityDisabled(manager.Environment.Settings),
                    SourceEnvironmentUnchanged =
                        IsSourceEnvironmentUnchanged(source.Environment.Settings),
                    RuntimeAudioSilent = IsAudioSilent(manager.Audio),
                    SourceAudioUnchanged = IsSourceAudioUnchanged(source.Audio),
                    RuntimeRainEffectBound = IsRuntimeRainEffectBound(manager.Effects),
                    RuntimePrecipitationProfilesIsolated =
                        runtimeDrizzle != null && runtimeDrizzle != fixture.SourceRain &&
                        runtimeRain != null && runtimeRain != fixture.SourceRain &&
                        runtimeHeavyRain != null && runtimeHeavyRain != fixture.SourceRain &&
                        runtimeStorm != null && runtimeStorm != fixture.SourceStorm,
                    RuntimeStormIsolated = runtimeStorm != null && runtimeStorm != fixture.SourceStorm,
                    RuntimeStormAutomaticLightningDisabled =
                        runtimeStorm != null &&
                        runtimeStorm.lightningOverride != null &&
                        !runtimeStorm.lightningOverride.lightningStorm,
                    SourceStormUnchanged =
                        fixture.SourceStorm.lightningOverride != null &&
                        fixture.SourceStorm.lightningOverride.lightningStorm,
                    RuntimeLightningPrefabIsolated =
                        runtimeLightningPrefab != null &&
                        runtimeLightningPrefab != fixture.LightningPrefab &&
                        manager.Lightning.Settings.prefab == runtimeLightningPrefab &&
                        runtimeLightningPrefab.planeMat != fixture.SourceLightningFlashMaterial
                };

                EnvironmentPresentationStatus smoothStatus = adapter.Present(CreateFrame(
                    1,
                    Enviro3EnvironmentBindings.RainIdValue,
                    normalizedTimeOfDay01: 0.25f,
                    transitionDurationSeconds: 10f,
                    precipitationIntensity01: 0.7f));
                bool smoothInstantFlag = GetPrivateField<bool>(manager.Weather, "instantTransition");
                float expectedTransitionRate = 4.60517019f / 10f;
                observations.SmoothTransitionUsedTypedWeatherPath =
                    smoothStatus.ErrorCount == 0 &&
                    manager.Weather.targetWeatherType == runtimeRain &&
                    Mathf.Approximately(GetRainEmission(runtimeRain), 0.7f) &&
                    !smoothInstantFlag;
                observations.SmoothTransitionRateApproximatedDuration =
                    Mathf.Approximately(
                        manager.Weather.Settings.cloudsTransitionSpeed,
                        expectedTransitionRate) &&
                    Mathf.Approximately(
                        manager.Weather.Settings.effectsTransitionSpeed,
                        expectedTransitionRate) &&
                    Mathf.Approximately(
                        manager.Weather.Settings.environmentTransitionSpeed,
                        expectedTransitionRate);

                adapter.Present(CreateFrame(
                    2,
                    Enviro3EnvironmentBindings.RainIdValue,
                    normalizedTimeOfDay01: 0.5f,
                    transitionDurationSeconds: 0f,
                    precipitationIntensity01: 0.7f));
                observations.TimeOnlyRevisionDidNotRestartWeatherTransition =
                    manager.Weather.targetWeatherType == runtimeRain &&
                    !GetPrivateField<bool>(manager.Weather, "instantTransition") &&
                    Mathf.Approximately(
                        manager.Weather.Settings.cloudsTransitionSpeed,
                        expectedTransitionRate);

                adapter.Present(CreateFrame(
                    3,
                    Enviro3EnvironmentBindings.RainIdValue,
                    normalizedTimeOfDay01: 0.5f,
                    transitionDurationSeconds: 0f,
                    precipitationIntensity01: 0.35f));
                observations.PrecipitationIntensityUpdatedWithoutRestart =
                    manager.Weather.targetWeatherType == runtimeRain &&
                    Mathf.Approximately(GetRainEmission(runtimeRain), 0.35f) &&
                    !GetPrivateField<bool>(manager.Weather, "instantTransition");

                adapter.Present(CreateFrame(
                    4,
                    Enviro3EnvironmentBindings.DrizzleIdValue,
                    normalizedTimeOfDay01: 0.5f,
                    transitionDurationSeconds: 0f,
                    precipitationIntensity01: 0.2f));
                bool drizzleMapped =
                    manager.Weather.targetWeatherType == runtimeDrizzle &&
                    Mathf.Approximately(GetRainEmission(runtimeDrizzle), 0.2f);
                adapter.Present(CreateFrame(
                    5,
                    Enviro3EnvironmentBindings.HeavyRainIdValue,
                    normalizedTimeOfDay01: 0.5f,
                    transitionDurationSeconds: 0f,
                    precipitationIntensity01: 0.85f));
                bool heavyMapped =
                    manager.Weather.targetWeatherType == runtimeHeavyRain &&
                    Mathf.Approximately(GetRainEmission(runtimeHeavyRain), 0.85f);
                adapter.Present(CreateFrame(
                    6,
                    Enviro3EnvironmentBindings.StormIdValue,
                    normalizedTimeOfDay01: 0.5f,
                    transitionDurationSeconds: 0f,
                    precipitationIntensity01: 1f));
                bool stormMapped =
                    manager.Weather.targetWeatherType == runtimeStorm &&
                    Mathf.Approximately(GetRainEmission(runtimeStorm), 1f);
                observations.PrecipitationMappingsAreDistinct =
                    drizzleMapped && heavyMapped && stormMapped &&
                    runtimeDrizzle != runtimeRain &&
                    runtimeRain != runtimeHeavyRain &&
                    runtimeHeavyRain != runtimeStorm;
                observations.SourcePrecipitationProfilesUnchanged =
                    Mathf.Approximately(GetRainEmission(fixture.SourceRain), 0.5f) &&
                    Mathf.Approximately(GetRainEmission(fixture.SourceStorm), 1f);

                adapter.Present(CreateFrame(
                    7,
                    Enviro3EnvironmentBindings.ClearIdValue,
                    normalizedTimeOfDay01: 0.5f,
                    transitionDurationSeconds: 0f));
                observations.ImmediateBindingChangeUsedInstantPath =
                    manager.Weather.targetWeatherType == fixture.SourceClear &&
                    GetPrivateField<bool>(manager.Weather, "instantTransition");

                Vector3 requestedLightningPosition = new Vector3(42f, 3f, -17f);
                EnvironmentLightningVisualRequest lightningRequest =
                    new EnvironmentLightningVisualRequest(
                        true,
                        10,
                        requestedLightningPosition,
                        0.4f);
                EnvironmentPresentationStatus lightningStatus = adapter.Present(CreateFrame(
                    8,
                    Enviro3EnvironmentBindings.ClearIdValue,
                    normalizedTimeOfDay01: 0.5f,
                    transitionDurationSeconds: 0f,
                    lightning: lightningRequest));
                Lightning spawnedLightning = FindSpawnedLightning(
                    fixture.LightningPrefab,
                    runtimeLightningPrefab);
                int lightningCountAfterFirstRequest =
                    CountSpawnedLightning(fixture.LightningPrefab, runtimeLightningPrefab);
                observations.LightningRequestUsedWorldPosition =
                    spawnedLightning != null &&
                    spawnedLightning.target == requestedLightningPosition &&
                    spawnedLightning.transform.position == requestedLightningPosition;
                observations.LightningIntensityLimitationWasReported =
                    lightningStatus.State == EnvironmentPresentationState.Degraded &&
                    ContainsDiagnostic(adapter, "ENVIRO3-FRAME-004");

                adapter.Present(CreateFrame(
                    9,
                    Enviro3EnvironmentBindings.ClearIdValue,
                    normalizedTimeOfDay01: 0.5f,
                    transitionDurationSeconds: 0f,
                    lightning: lightningRequest));
                observations.LightningSequenceWasDeduplicated =
                    lightningCountAfterFirstRequest == 1 &&
                    CountSpawnedLightning(fixture.LightningPrefab, runtimeLightningPrefab) ==
                    lightningCountAfterFirstRequest;

                EnvironmentRefreshRequest firstRefresh = new EnvironmentRefreshRequest(
                    EnvironmentRefreshTarget.Ambient,
                    20);
                adapter.Present(CreateFrame(
                    10,
                    Enviro3EnvironmentBindings.ClearIdValue,
                    normalizedTimeOfDay01: 0.5f,
                    transitionDurationSeconds: 0f,
                    refresh: firstRefresh));
                bool firstRefreshExecuted =
                    GetPrivateField<float>(adapter, "nextRefreshAllowedRealtime") >
                    Time.realtimeSinceStartup &&
                    GetPrivateField<uint>(adapter, "pendingRefreshSequence") == 0;

                EnvironmentRefreshRequest cooledRefresh = new EnvironmentRefreshRequest(
                    EnvironmentRefreshTarget.Ambient,
                    21);
                adapter.Present(CreateFrame(
                    11,
                    Enviro3EnvironmentBindings.ClearIdValue,
                    normalizedTimeOfDay01: 0.5f,
                    transitionDurationSeconds: 0f,
                    refresh: cooledRefresh));
                adapter.Present(CreateFrame(
                    12,
                    Enviro3EnvironmentBindings.ClearIdValue,
                    normalizedTimeOfDay01: 0.5f,
                    transitionDurationSeconds: 0f,
                    refresh: cooledRefresh));
                observations.EnvironmentRefreshCapabilityExposed =
                    (adapter.Capabilities & EnvironmentPresentationCapabilities.EnvironmentRefresh) != 0;
                observations.RefreshSequenceWasDeduplicatedAndCooledDown =
                    firstRefreshExecuted &&
                    GetPrivateField<uint>(adapter, "lastRefreshSequence") == 21 &&
                    GetPrivateField<uint>(adapter, "pendingRefreshSequence") == 21 &&
                    GetPrivateField<Coroutine>(adapter, "delayedRefreshRoutine") != null;

                Vector2 projectWindDirection = new Vector2(0.6f, 0.8f);
                EnvironmentPresentationStatus mediumWindStatus = adapter.Present(CreateFrame(
                    13,
                    Enviro3EnvironmentBindings.ClearIdValue,
                    normalizedTimeOfDay01: 0.5f,
                    transitionDurationSeconds: 0f,
                    qualityTier: EnvironmentQualityTier.Medium,
                    windDirectionXZ: projectWindDirection,
                    windSpeedMetersPerSecond: 10f,
                    windGustSpeedMetersPerSecond: 15f));

                EnviroEnvironment runtimeEnvironment = manager.Environment.Settings;
                runtimeEnvironment.changeSeason = true;
                runtimeEnvironment.season = EnviroEnvironment.Seasons.Winter;
                runtimeEnvironment.wetness = 0.9f;
                runtimeEnvironment.wetnessTarget = 1f;
                runtimeEnvironment.snow = 0.8f;
                runtimeEnvironment.snowTarget = 1f;
                runtimeEnvironment.windDirectionX = -1f;
                runtimeEnvironment.windDirectionY = 0f;
                runtimeEnvironment.windSpeed = 1f;
                runtimeEnvironment.windTurbulence = 1f;
                manager.Objects.windZone.windMain = 1f;
                manager.Objects.windZone.windTurbulence = 1f;
                InvokePrivate(adapter, "LateUpdate");

                observations.MediumQualityMapped =
                    mediumWindStatus.ErrorCount == 0 &&
                    manager.Quality.Settings.defaultQuality == fixture.MediumQuality;
                observations.ProjectOwnedWindApplied =
                    Mathf.Approximately(runtimeEnvironment.windDirectionX, 0.6f) &&
                    Mathf.Approximately(runtimeEnvironment.windDirectionY, 0.8f) &&
                    Mathf.Approximately(runtimeEnvironment.windSpeed, 0.5f) &&
                    Mathf.Approximately(runtimeEnvironment.windTurbulence, 0.25f) &&
                    Mathf.Approximately(manager.Objects.windZone.windMain, 0.5f) &&
                    Mathf.Approximately(manager.Objects.windZone.windTurbulence, 0.25f) &&
                    Vector3.Angle(
                        manager.Objects.windZone.transform.forward,
                        new Vector3(-0.6f, 0f, -0.8f)) < 0.01f;
                WindZone[] activeWindZones = Object.FindObjectsByType<WindZone>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
                observations.SingleEnviroWindOwner =
                    activeWindZones.Length == 1 &&
                    activeWindZones[0] == manager.Objects.windZone &&
                    manager.Objects.windZone.transform.IsChildOf(manager.transform);
                observations.RuntimeSeasonAutonomyDisabled &=
                    !runtimeEnvironment.changeSeason &&
                    runtimeEnvironment.season == EnviroEnvironment.Seasons.Summer;
                observations.RuntimeWetnessSnowAutonomyDisabled &=
                    IsWetnessAndSnowAuthorityDisabled(runtimeEnvironment);
                observations.SourceEnvironmentUnchanged &=
                    IsSourceEnvironmentUnchanged(source.Environment.Settings);
                observations.AdapterHasSingleBoundedLateUpdateWriter =
                    typeof(Enviro3EnvironmentAdapter).GetMethod(
                        "LateUpdate",
                        BindingFlags.Instance | BindingFlags.NonPublic) != null;

                InvokePrivate(adapter, "CastLightningVisual");
                Material firstLightningMaterial =
                    GetPrivateField<Material>(adapter, "runtimeLightningFlashMaterial");
                int ownedObjectCountAfterFirst =
                    GetPrivateField<List<UnityEngine.Object>>(adapter, "ownedRuntimeObjects").Count;
                bool prefabRestoredAfterFirst =
                    fixture.LightningPrefab.planeMat == fixture.SourceLightningFlashMaterial;

                InvokePrivate(adapter, "CastLightningVisual");
                Material secondLightningMaterial =
                    GetPrivateField<Material>(adapter, "runtimeLightningFlashMaterial");
                int ownedObjectCountAfterSecond =
                    GetPrivateField<List<UnityEngine.Object>>(adapter, "ownedRuntimeObjects").Count;
                observations.RuntimeLightningFlashMaterial = secondLightningMaterial;
                observations.RepeatedLightningReusedMaterial =
                    firstLightningMaterial != null &&
                    firstLightningMaterial == secondLightningMaterial &&
                    ownedObjectCountAfterFirst == ownedObjectCountAfterSecond;
                observations.LightningPrefabMaterialRestored =
                    prefabRestoredAfterFirst &&
                    fixture.LightningPrefab.planeMat == fixture.SourceLightningFlashMaterial;

                EnvironmentPresentationStatus repeatedAttach = adapter.Attach();
                observations.RepeatedAttachStayedReady =
                    adapter.IsAttached &&
                    repeatedAttach.State == EnvironmentPresentationState.Ready &&
                    repeatedAttach.ErrorCount == 0 &&
                    GetPrivateField<EnviroWeatherType>(adapter, "runtimeStorm") == runtimeStorm;

                var duplicateManagerObject = new GameObject(
                    "Late Additive Enviro Manager Test Double");
                duplicateManagerObject.SetActive(false);
                EnviroManager duplicateManager =
                    duplicateManagerObject.AddComponent<EnviroManager>();
                duplicateManager.enabled = false;
                var foreignWindObject = new GameObject(
                    "Late Additive Wind Owner Test Double");
                WindZone foreignWind = foreignWindObject.AddComponent<WindZone>();
                EnvironmentPresentationStatus ownershipStatus =
                    ((IEnvironmentPresentationSceneOwnershipGuard)adapter)
                    .RevalidateSceneOwnership();
                observations.LateDuplicateOwnershipFailedClosed =
                    ownershipStatus.State ==
                    EnvironmentPresentationState.Faulted &&
                    ownershipStatus.ErrorCount >= 2 &&
                    !adapter.IsAttached &&
                    !duplicateManager.enabled &&
                    !foreignWindObject.activeSelf &&
                    ContainsDiagnostic(adapter, "ENVIRO3-ATTACH-003") &&
                    ContainsDiagnostic(adapter, "ENVIRO3-ATTACH-004");
                Object.Destroy(duplicateManagerObject);
                Object.Destroy(foreignWindObject);

                adapter.Detach();
                observations.FirstDetachWasDetached =
                    !adapter.IsAttached &&
                    adapter.Status.State == EnvironmentPresentationState.Detached;
                observations.RuntimeStormReleasedOnDetach =
                    GetPrivateField<EnviroWeatherType>(adapter, "runtimeStorm") == null;

                adapter.Detach();
                observations.SecondDetachWasDetached =
                    !adapter.IsAttached &&
                    adapter.Status.State == EnvironmentPresentationState.Detached;
                return observations;
            }

            private static EnvironmentPresentationFrame CreateFrame(
                ulong revision,
                string bindingValue,
                float normalizedTimeOfDay01,
                float transitionDurationSeconds,
                float precipitationIntensity01 = 0f,
                EnvironmentLightningVisualRequest lightning = default,
                EnvironmentRefreshRequest refresh = default,
                EnvironmentQualityTier qualityTier = EnvironmentQualityTier.Low,
                Vector2 windDirectionXZ = default,
                float windSpeedMetersPerSecond = 0f,
                float windGustSpeedMetersPerSecond = 0f)
            {
                if (!EnvironmentBindingId.TryParse(
                        bindingValue,
                        out EnvironmentBindingId bindingId))
                {
                    throw new ArgumentException("Invalid test binding ID.", nameof(bindingValue));
                }

                bool isRain =
                    bindingValue == Enviro3EnvironmentBindings.DrizzleIdValue ||
                    bindingValue == Enviro3EnvironmentBindings.RainIdValue ||
                    bindingValue == Enviro3EnvironmentBindings.HeavyRainIdValue ||
                    bindingValue == Enviro3EnvironmentBindings.StormIdValue;
                return new EnvironmentPresentationFrame(
                    revision,
                    true,
                    1995,
                    7,
                    15,
                    normalizedTimeOfDay01,
                    bindingId,
                    isRain ? EnvironmentCloudType.Overcast : EnvironmentCloudType.Clear,
                    isRain ? 0.8f : 0f,
                    isRain ? 0.8f : 0f,
                    isRain
                        ? EnvironmentPrecipitationType.Rain
                        : EnvironmentPrecipitationType.None,
                    isRain ? precipitationIntensity01 : 0f,
                    0f,
                    isRain ? 5500f : 20000f,
                    WeatherExposureContext.Exterior,
                    windDirectionXZ,
                    windSpeedMetersPerSecond,
                    windGustSpeedMetersPerSecond,
                    lightning,
                    refresh,
                    qualityTier,
                    transitionDurationSeconds);
            }

            private static float GetRainEmission(EnviroWeatherType weather)
            {
                if (weather?.effectsOverride?.effectsOverride == null)
                {
                    return -1f;
                }

                for (int index = 0; index < weather.effectsOverride.effectsOverride.Count; index++)
                {
                    EnviroEffectsOverrideType value = weather.effectsOverride.effectsOverride[index];
                    if (value != null && string.Equals(value.name, "Rain", StringComparison.Ordinal))
                    {
                        return value.emission;
                    }
                }

                return -1f;
            }

            private static int CountSpawnedLightning(
                Lightning sourcePrefab,
                Lightning runtimeTemplate)
            {
                Lightning[] lightningObjects = Object.FindObjectsByType<Lightning>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                int count = 0;
                for (int index = 0; index < lightningObjects.Length; index++)
                {
                    if (lightningObjects[index] != sourcePrefab &&
                        lightningObjects[index] != runtimeTemplate)
                    {
                        count++;
                    }
                }

                return count;
            }

            private static Lightning FindSpawnedLightning(
                Lightning sourcePrefab,
                Lightning runtimeTemplate)
            {
                Lightning[] lightningObjects = Object.FindObjectsByType<Lightning>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (int index = 0; index < lightningObjects.Length; index++)
                {
                    if (lightningObjects[index] != sourcePrefab &&
                        lightningObjects[index] != runtimeTemplate)
                    {
                        return lightningObjects[index];
                    }
                }

                return null;
            }

            private static bool ContainsDiagnostic(
                Enviro3EnvironmentAdapter adapter,
                string code)
            {
                for (int index = 0; index < adapter.Diagnostics.Count; index++)
                {
                    if (adapter.Diagnostics[index].Code == code)
                    {
                        return true;
                    }
                }

                return false;
            }

            private static bool AreRequiredModulesIsolated(
                EnviroManager manager,
                EnviroConfiguration source)
            {
                return manager.Time != null && manager.Time != source.timeModule &&
                       manager.Sky != null && manager.Sky != source.Sky &&
                       manager.Lighting != null && manager.Lighting != source.lightingModule &&
                       manager.Fog != null && manager.Fog != source.fogModule &&
                       manager.VolumetricClouds != null &&
                       manager.VolumetricClouds != source.volumetricCloudModule &&
                       manager.Weather != null && manager.Weather != source.Weather &&
                       manager.Effects != null && manager.Effects != source.Effects &&
                       manager.Lightning != null && manager.Lightning != source.Lightning &&
                       manager.Quality != null && manager.Quality != source.Quality &&
                       manager.Environment != null && manager.Environment != source.Environment &&
                       manager.Audio != null && manager.Audio != source.Audio;
            }

            private static bool AreMutableSettingsIsolated(
                EnviroManager manager,
                EnviroConfiguration source,
                EnviroEffectsModule effectsSource)
            {
                return !ReferenceEquals(manager.Time.Settings, source.timeModule.Settings) &&
                       !ReferenceEquals(manager.Weather.Settings, source.Weather.Settings) &&
                       !ReferenceEquals(manager.Effects.Settings, source.Effects.Settings) &&
                       !ReferenceEquals(manager.Effects.Settings, effectsSource.Settings) &&
                       !ReferenceEquals(manager.Lightning.Settings, source.Lightning.Settings) &&
                       !ReferenceEquals(manager.Quality.Settings, source.Quality.Settings) &&
                       !ReferenceEquals(manager.Environment.Settings, source.Environment.Settings) &&
                       !ReferenceEquals(manager.Audio.Settings, source.Audio.Settings);
            }

            private static bool IsSourceGraphIntact(RuntimeFixture fixture)
            {
                EnviroConfiguration source = fixture.SourceConfiguration;
                return fixture.Bindings.SourceConfiguration == source &&
                       fixture.Bindings.EffectsSource == fixture.EffectsSource &&
                       fixture.Bindings.Low == fixture.LowQuality &&
                       fixture.Bindings.Medium == fixture.MediumQuality &&
                       fixture.Bindings.High == fixture.HighQuality &&
                       source.timeModule != null &&
                       source.Sky != null &&
                       source.lightingModule != null &&
                       source.fogModule != null &&
                       source.volumetricCloudModule != null &&
                       source.Weather != null &&
                       source.Effects != null &&
                       source.Lightning != null &&
                       source.Quality != null &&
                       source.Environment != null &&
                       source.Audio != null;
            }

            private static bool IsRuntimeRainEffectBound(EnviroEffectsModule module)
            {
                if (module == null || module.Settings == null || module.Settings.effectTypes == null)
                {
                    return false;
                }

                for (int index = 0; index < module.Settings.effectTypes.Count; index++)
                {
                    EnviroEffectTypes effect = module.Settings.effectTypes[index];
                    if (effect != null && effect.name == "Rain")
                    {
                        return effect.prefab != null && effect.maxEmission > 0f;
                    }
                }

                return false;
            }


            private static bool IsWetnessAndSnowAuthorityDisabled(
                EnviroEnvironment settings)
            {
                return settings != null &&
                       !settings.changeSeason &&
                       settings.season == EnviroEnvironment.Seasons.Summer &&
                       ApproximatelyZero(settings.wetness) &&
                       ApproximatelyZero(settings.wetnessTarget) &&
                       ApproximatelyZero(settings.snow) &&
                       ApproximatelyZero(settings.snowTarget) &&
                       ApproximatelyZero(settings.wetnessAccumulationSpeed) &&
                       ApproximatelyZero(settings.wetnessDrySpeed) &&
                       ApproximatelyZero(settings.snowAccumulationSpeed) &&
                       ApproximatelyZero(settings.snowMeltSpeed);
            }

            private static bool IsSourceEnvironmentUnchanged(
                EnviroEnvironment settings)
            {
                return settings != null &&
                       settings.changeSeason &&
                       settings.season == EnviroEnvironment.Seasons.Winter &&
                       Mathf.Approximately(settings.wetness, 0.65f) &&
                       Mathf.Approximately(settings.wetnessTarget, 0.8f) &&
                       Mathf.Approximately(settings.snow, 0.45f) &&
                       Mathf.Approximately(settings.snowTarget, 0.7f) &&
                       Mathf.Approximately(settings.wetnessAccumulationSpeed, 0.6f) &&
                       Mathf.Approximately(settings.wetnessDrySpeed, 0.5f) &&
                       Mathf.Approximately(settings.snowAccumulationSpeed, 0.4f) &&
                       Mathf.Approximately(settings.snowMeltSpeed, 0.3f) &&
                       Mathf.Approximately(settings.windDirectionX, -0.25f) &&
                       Mathf.Approximately(settings.windDirectionY, 0.75f) &&
                       Mathf.Approximately(settings.windSpeed, 0.9f) &&
                       Mathf.Approximately(settings.windTurbulence, 0.8f);
            }

            private static bool IsAudioSilent(EnviroAudioModule module)
            {
                if (module == null || module.Settings == null)
                {
                    return false;
                }

                return ApproximatelyZero(module.Settings.ambientMasterVolume) &&
                       ApproximatelyZero(module.Settings.weatherMasterVolume) &&
                       ApproximatelyZero(module.Settings.thunderMasterVolume) &&
                       ApproximatelyZero(module.ambientVolumeModifier) &&
                       ApproximatelyZero(module.weatherVolumeModifier) &&
                       ApproximatelyZero(module.thunderVolumeModifier) &&
                       AreClipsSilent(module.Settings.ambientClips) &&
                       AreClipsSilent(module.Settings.weatherClips) &&
                       AreClipsSilent(module.Settings.thunderClips);
            }

            private static bool IsSourceAudioUnchanged(EnviroAudioModule module)
            {
                return module != null && module.Settings != null &&
                       Mathf.Approximately(module.Settings.ambientMasterVolume, 0.9f) &&
                       Mathf.Approximately(module.Settings.weatherMasterVolume, 0.8f) &&
                       Mathf.Approximately(module.Settings.thunderMasterVolume, 0.7f) &&
                       Mathf.Approximately(module.ambientVolumeModifier, 0.6f) &&
                       Mathf.Approximately(module.weatherVolumeModifier, 0.5f) &&
                       Mathf.Approximately(module.thunderVolumeModifier, 0.4f) &&
                       HasSingleClipAtVolume(module.Settings.ambientClips, 0.65f) &&
                       HasSingleClipAtVolume(module.Settings.weatherClips, 0.55f) &&
                       HasSingleClipAtVolume(module.Settings.thunderClips, 0.45f);
            }

            private static bool AreClipsSilent(IList<EnviroAudioClip> clips)
            {
                if (clips == null)
                {
                    return false;
                }

                for (int index = 0; index < clips.Count; index++)
                {
                    if (clips[index] == null || !ApproximatelyZero(clips[index].volume))
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool HasSingleClipAtVolume(
                IList<EnviroAudioClip> clips,
                float expectedVolume)
            {
                return clips != null &&
                       clips.Count == 1 &&
                       clips[0] != null &&
                       Mathf.Approximately(clips[0].volume, expectedVolume);
            }

            private static bool ApproximatelyZero(float value)
            {
                return Mathf.Approximately(value, 0f);
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

            private static void InvokePrivate(object target, string methodName)
            {
                MethodInfo method = target.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    types: Type.EmptyTypes,
                    modifiers: null);
                if (method == null)
                {
                    throw new MissingMethodException(target.GetType().FullName, methodName);
                }

                method.Invoke(target, null);
            }
        }
    }
}
