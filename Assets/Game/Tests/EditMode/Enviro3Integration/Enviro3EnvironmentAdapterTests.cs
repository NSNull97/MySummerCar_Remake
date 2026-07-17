using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Enviro;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.EditMode.Enviro3Integration
{
    public sealed class Enviro3EnvironmentAdapterTests
    {
        private const string MissingReferenceDiagnosticCode = "ENVIRO3-ATTACH-001";

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
            Assert.That(observations.RuntimeAudioSilent, Is.True);
            Assert.That(observations.SourceAudioUnchanged, Is.True);
            Assert.That(observations.RuntimeRainEffectBound, Is.True);
            Assert.That(observations.RuntimeStormIsolated, Is.True);
            Assert.That(observations.RuntimeStormAutomaticLightningDisabled, Is.True);
            Assert.That(observations.SourceStormUnchanged, Is.True);
            Assert.That(observations.RepeatedAttachStayedReady, Is.True);
            Assert.That(observations.FirstDetachWasDetached, Is.True);
            Assert.That(observations.SecondDetachWasDetached, Is.True);
            Assert.That(observations.RuntimeStormReleasedOnDetach, Is.True);
            Assert.That(observations.RepeatedLightningReusedMaterial, Is.True);
            Assert.That(observations.LightningPrefabMaterialRestored, Is.True);
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

            public EnviroWeatherType SourceStorm { get; private set; }

            public Lightning LightningPrefab { get; private set; }

            public Material SourceLightningFlashMaterial => lightningFlashMaterial;

            public void Initialize()
            {
                TrackingVolumetricCloudsModule.DisableCallCount = 0;
                SourceConfiguration = Create<EnviroConfiguration>();
                SourceConfiguration.name = "Enviro Source Configuration";
                CreateSourceModules(SourceConfiguration);

                EnviroWeatherType clear = Create<EnviroWeatherType>();
                EnviroWeatherType overcast = Create<EnviroWeatherType>();
                EnviroWeatherType rain = Create<EnviroWeatherType>();
                SourceStorm = Create<EnviroWeatherType>();
                SourceStorm.lightningOverride = new EnviroWeatherTypeLightningOverride
                {
                    lightningStorm = true
                };
                EnviroWeatherType fog = Create<EnviroWeatherType>();
                EnviroQuality low = Create<EnviroQuality>();
                EnviroQuality high = Create<EnviroQuality>();

                rainPrefabGameObject = new GameObject("Enviro Rain Prefab Test Double");
                rainPrefabGameObject.SetActive(false);
                rainPrefabGameObject.AddComponent<ParticleSystem>();
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
                    clear,
                    overcast,
                    rain,
                    SourceStorm,
                    fog,
                    low,
                    high);

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
                configuration.lightingModule.Settings = new EnviroLighting();

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
                configuration.Environment.Settings = new EnviroEnvironment();

                configuration.Audio = Create<EnviroAudioModule>();
                configuration.Audio.Settings = CreateUnsafeAudioSettings();
                configuration.Audio.ambientVolumeModifier = 0.6f;
                configuration.Audio.weatherVolumeModifier = 0.5f;
                configuration.Audio.thunderVolumeModifier = 0.4f;
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

            public bool RuntimeAudioSilent { get; private set; }

            public bool SourceAudioUnchanged { get; private set; }

            public bool RuntimeRainEffectBound { get; private set; }

            public bool RuntimeStormIsolated { get; private set; }

            public bool RuntimeStormAutomaticLightningDisabled { get; private set; }

            public bool SourceStormUnchanged { get; private set; }

            public bool RepeatedAttachStayedReady { get; private set; }

            public bool FirstDetachWasDetached { get; private set; }

            public bool SecondDetachWasDetached { get; private set; }

            public bool RuntimeStormReleasedOnDetach { get; private set; }

            public bool RepeatedLightningReusedMaterial { get; private set; }

            public bool LightningPrefabMaterialRestored { get; private set; }

            public Material RuntimeLightningFlashMaterial { get; private set; }

            public static RuntimeObservations Capture(RuntimeFixture fixture)
            {
                EnviroManager manager = fixture.Manager;
                EnviroConfiguration source = fixture.SourceConfiguration;
                Enviro3EnvironmentAdapter adapter = fixture.Adapter;
                EnvironmentPresentationStatus initialStatus = adapter.Status;
                EnviroWeatherType runtimeStorm = GetPrivateField<EnviroWeatherType>(adapter, "runtimeStorm");

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
                        source.Lightning.Settings.lightningStorm,
                    RuntimeTimeSimulationDisabled = !manager.Time.Settings.simulate,
                    RuntimeAutomaticLightningDisabled = !manager.Lightning.Settings.lightningStorm,
                    RuntimeAudioSilent = IsAudioSilent(manager.Audio),
                    SourceAudioUnchanged = IsSourceAudioUnchanged(source.Audio),
                    RuntimeRainEffectBound = IsRuntimeRainEffectBound(manager.Effects),
                    RuntimeStormIsolated = runtimeStorm != null && runtimeStorm != fixture.SourceStorm,
                    RuntimeStormAutomaticLightningDisabled =
                        runtimeStorm != null &&
                        runtimeStorm.lightningOverride != null &&
                        !runtimeStorm.lightningOverride.lightningStorm,
                    SourceStormUnchanged =
                        fixture.SourceStorm.lightningOverride != null &&
                        fixture.SourceStorm.lightningOverride.lightningStorm
                };

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
                       !ReferenceEquals(manager.Audio.Settings, source.Audio.Settings);
            }

            private static bool IsSourceGraphIntact(RuntimeFixture fixture)
            {
                EnviroConfiguration source = fixture.SourceConfiguration;
                return fixture.Bindings.SourceConfiguration == source &&
                       fixture.Bindings.EffectsSource == fixture.EffectsSource &&
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
                    BindingFlags.Instance | BindingFlags.NonPublic);
                if (method == null)
                {
                    throw new MissingMethodException(target.GetType().FullName, methodName);
                }

                method.Invoke(target, null);
            }
        }
    }
}
