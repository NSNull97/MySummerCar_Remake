using System;
using System.Collections;
using MSC.Audio;
using MSC.Bootstrap;
using MSC.Core.Time;
using MSC.Weather.Domain;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Production;
using MSC.Weather.Wetness;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using GameWeatherSystem = MSC.Weather.System.GameWeatherSystem;
using NativeHDRPWeatherBackend =
    MSC.Weather.System.NativeHDRP.NativeHDRPWeatherBackend;
using WeatherBackendType = MSC.Weather.System.WeatherBackendType;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.WeatherProduction
{
    public sealed class ProductionEnvironmentLifecyclePlayModeTests
    {
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string NonSessionScenePath =
            "Assets/Game/Player/Content/Scenes/PlayerInteractionPrototype.unity";
        private const string EnvironmentlessStreamingScenePath =
            "Assets/Game/World/Debug/Streaming/PrototypeWorldStreamingFixture.unity";
        private const float BootstrapReadyTimeoutSeconds = 120f;

        [UnityTest]
        public IEnumerator Bootstrap_RestoresBeforeRevealAndSurvivesAdditiveLifecycle()
        {
            yield return DestroyPersistentRootIfPresent();

            var desiredTime = new GameTimeService();
            desiredTime.Advance(42d);
            long desiredTicks = desiredTime.CaptureState().ElapsedGameTicks;
            bool staged = false;
            void StageRestore(Scene scene, LoadSceneMode mode)
            {
                if (scene.path != BootstrapScenePath)
                {
                    return;
                }

                ProductionEnvironmentController environment =
                    Object.FindFirstObjectByType<ProductionEnvironmentController>(
                        FindObjectsInactive.Include);
                ProductionEnvironmentSaveDto dto = environment.CaptureState();
                dto.GameTime = desiredTime.CaptureDto();
                environment.StageRestore(dto);
                staged = true;
            }

            SceneManager.sceneLoaded += StageRestore;
            AsyncOperation load = SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            yield return load;
            SceneManager.sceneLoaded -= StageRestore;
            Assert.That(staged, Is.True);

            ProductionWorldStreamingInstaller installer =
                Object.FindFirstObjectByType<ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include);
            ProductionEnvironmentController owner = installer.Environment;
            yield return null;

            Assert.That(installer.IsReady, Is.False);
            Assert.That(installer.IsGameplayPrepared, Is.False);
            Assert.That(installer.IsGameplayActive, Is.False);
            Assert.That(installer.WorldStreaming.HasFocus, Is.False);
            Assert.That(installer.WorldStreaming.OwnedLoadedSceneCount, Is.Zero);
            Assert.That(installer.SpawnedPlayer.activeSelf, Is.False);
            Assert.That(owner.IsSimulationActive, Is.False);
            Assert.That(
                installer.TryBeginGameplayPreparation(
                    out string preparationFailure),
                Is.True,
                preparationFailure);
            float startupDeadline =
                Time.realtimeSinceStartup + BootstrapReadyTimeoutSeconds;
            while (!installer.IsReady &&
                   Time.realtimeSinceStartup < startupDeadline)
            {
                yield return null;
            }

            Assert.That(
                installer.IsReady,
                Is.True,
                "Bootstrap did not become ready before the real-time " +
                $"{BootstrapReadyTimeoutSeconds:0}s budget. " +
                $"preparing={installer.IsGameplayPreparationRunning} " +
                $"focus={installer.WorldStreaming.HasFocus} " +
                $"streaming={installer.WorldStreaming.IsStreaming} " +
                $"loaded={installer.WorldStreaming.OwnedLoadedSceneCount}.");
            Assert.That(owner.IsWorldRevealReady, Is.True);
            Assert.That(owner.WasRestoreAppliedBeforeReveal, Is.True);
            Assert.That(installer.IsGameplayPrepared, Is.True);
            Assert.That(installer.IsGameplayActive, Is.False);
            Assert.That(owner.IsSimulationActive, Is.False);
            Camera deferredCamera = installer.SpawnedPlayer
                .GetComponentInChildren<Camera>(true);
            Assert.That(deferredCamera, Is.Not.Null);
            Assert.That(deferredCamera.enabled, Is.False);
            Assert.That(
                installer.TryActivateGameplay(out string activationFailure),
                Is.True,
                activationFailure);
            yield return null;

            Assert.That(installer.IsGameplayActive, Is.True);
            Assert.That(owner.IsSimulationActive, Is.True);
            Assert.That(deferredCamera.enabled, Is.True);
            Assert.That(owner.PresentationStatus.IsOperational, Is.True);
            Assert.That(installer.SpawnedPlayer.activeSelf, Is.True);
            AudioListenerContextPresenter audioListener =
                installer.SpawnedPlayer.GetComponent<AudioListenerContextPresenter>();
            Assert.That(audioListener, Is.Not.Null);
            Assert.That(audioListener.enabled, Is.True);
            Assert.That(
                installer.SpawnedPlayer
                    .GetComponentInChildren<Camera>(true)
                    .GetComponent<AudioListenerContextPresenter>(),
                Is.Null,
                "The child camera cannot receive the root CharacterController's zone triggers.");
            yield return null;
            Assert.That(
                owner.DevRefreshPresentation().IsOperational,
                Is.True);
            Assert.That(
                owner.AuthoritativeGameTime.CaptureState().ElapsedGameTicks,
                Is.GreaterThanOrEqualTo(desiredTicks));
            Assert.That(
                owner.AuthoritativeLightning.IsGameplayStrikeAllowed,
                Is.False);

            Enviro3EnvironmentAdapter enviroAdapter =
                Object.FindFirstObjectByType<Enviro3EnvironmentAdapter>(
                    FindObjectsInactive.Include);
            Assert.That(enviroAdapter, Is.Not.Null);
            GameWeatherSystem weatherSystem =
                Object.FindFirstObjectByType<GameWeatherSystem>(
                    FindObjectsInactive.Include);
            Assert.That(weatherSystem, Is.Not.Null);
            if (weatherSystem.SelectedBackend == WeatherBackendType.EnviroLegacy)
            {
            Assert.That(weatherSystem.IsAttached, Is.True);
            Assert.That(
                weatherSystem.ActiveBackend.BackendType,
                Is.EqualTo(WeatherBackendType.EnviroLegacy));
            Assert.That(enviroAdapter.IsAttached, Is.True);
            Assert.That(enviroAdapter.HybridNativeHdrpOwnership, Is.True);
            NativeHdrpWeatherBridge hybridBridge =
                Object.FindFirstObjectByType<NativeHdrpWeatherBridge>(
                    FindObjectsInactive.Include);
            Assert.That(hybridBridge, Is.Not.Null);
            Assert.That(hybridBridge.IsReady, Is.True);
            NativeHDRPWeatherBackend inactiveNativeFallback =
                Object.FindFirstObjectByType<NativeHDRPWeatherBackend>(
                    FindObjectsInactive.Include);
            Assert.That(inactiveNativeFallback, Is.Not.Null);
            Assert.That(inactiveNativeFallback.IsAttached, Is.False);
            Assert.That(
                inactiveNativeFallback.RuntimeWeatherVolume.enabled,
                Is.False);
            Assert.That(enviroAdapter.HasConfiguredCelestialSky, Is.True);
            Assert.That(enviroAdapter.HasConfiguredMoonLighting, Is.True);
            Assert.That(
                enviroAdapter.ActiveMoonScale,
                Is.GreaterThanOrEqualTo(
                    Enviro3ProductionVisualPolicy.MinimumVisibleMoonScale));
            Assert.That(
                enviroAdapter.HasIsolatedRuntimeVolumeTargets,
                Is.True);
            Assert.That(
                float.IsFinite(enviroAdapter.ActiveExposureEv),
                Is.True);
            Assert.That(
                enviroAdapter.ActiveIndirectDiffuseMultiplier,
                Is.InRange(
                    Enviro3ProductionVisualPolicy
                        .NeutralIndirectLightingMultiplier,
                    Enviro3ProductionVisualPolicy
                        .ExteriorDaylightIndirectDiffuseMultiplier));
            Assert.That(
                enviroAdapter.ActiveIndirectReflectionMultiplier,
                Is.EqualTo(
                        Enviro3ProductionVisualPolicy
                            .IndirectReflectionLightingMultiplier)
                    .Within(0.0001f));
            Assert.That(enviroAdapter.IsCustomHeightFogDisabled, Is.True);
            Assert.That(
                enviroAdapter.ActiveFogMeanFreePathMeters,
                Is.GreaterThanOrEqualTo(300f));
            Assert.That(
                enviroAdapter.ActiveVolumetricLightDimmer,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                enviroAdapter.ActiveRainParticleMaxScreenSize,
                Is.GreaterThanOrEqualTo(
                    Enviro3ProductionVisualPolicy
                        .MinimumReadableRainParticleScreenSize));
            Assert.That(enviroAdapter.IsRainSurfaceSplashConfigured, Is.True);
            Assert.That(
                enviroAdapter.ActiveRainParticleBudget,
                Is.LessThanOrEqualTo(
                    Enviro3ProductionVisualPolicy
                        .MaximumRainParticleBudget));
            Assert.That(
                enviroAdapter.ActiveRainMaximumEmissionPerSecond,
                Is.EqualTo(
                        Enviro3ProductionVisualPolicy
                            .MaximumRainEmissionPerSecond)
                    .Within(0.0001f));
            Assert.That(
                enviroAdapter.ActiveRainSurfaceSplashParticleBudget,
                Is.LessThanOrEqualTo(
                    Enviro3ProductionVisualPolicy
                        .MaximumRainSplashParticleBudget));
            Assert.That(
                enviroAdapter.IsRainCollisionPerformanceBounded,
                Is.True);
            Assert.That(
                enviroAdapter.ActiveRainSurfaceSplashMaxScreenSize,
                Is.LessThanOrEqualTo(
                    Enviro3ProductionVisualPolicy
                        .MaximumRainSplashParticleScreenSize));
            Assert.That(
                enviroAdapter.IsProductionTimeLocationApplied,
                Is.True);
            Assert.That(enviroAdapter.IsAuroraSuppressed, Is.True);
            Assert.That(
                enviroAdapter.ActiveGlobalReflectionIntensity,
                Is.EqualTo(
                        Enviro3ProductionVisualPolicy
                            .TemporaryBaselineReflectionIntensity)
                    .Within(0.0001f));

            Assert.That(
                owner.DevTryApplyWeatherOverride(
                    WeatherStateIds.Clear.Value,
                    0f,
                    out string weatherFailure),
                Is.True,
                weatherFailure);
            yield return null;
            float clearSunMultiplier =
                enviroAdapter.ActiveDirectSunlightMultiplier;
            float clearShadowStrength =
                enviroAdapter.ActiveDirectionalLightShadowStrength;
            Assert.That(
                owner.DevTryApplyWeatherOverride(
                    WeatherStateIds.PartlyCloudy.Value,
                    0f,
                    out weatherFailure),
                Is.True,
                weatherFailure);
            yield return null;
            float partlyCoverage = enviroAdapter.ActiveTargetCloudCoverage;
            float partlyDensity = enviroAdapter.ActiveTargetCloudDensity;
            float partlyCirrus = enviroAdapter.ActiveTargetCirrusAlpha;
            uint partlyCloudSeed = enviroAdapter.ActiveCloudFieldSeed;
            Vector2 partlyCloudOffset = enviroAdapter.ActiveCloudFieldOffset;
            Assert.That(
                owner.DevTryApplyWeatherOverride(
                    WeatherStateIds.BrightOvercast.Value,
                    0f,
                    out weatherFailure),
                Is.True,
                weatherFailure);
            yield return null;
            float brightCoverage = enviroAdapter.ActiveTargetCloudCoverage;
            float brightDensity = enviroAdapter.ActiveTargetCloudDensity;
            float brightDetailErosion =
                enviroAdapter.ActiveTargetCloudDetailErosion;
            float brightLightAbsorption =
                enviroAdapter.ActiveTargetCloudLightAbsorption;
            float brightCirrus = enviroAdapter.ActiveTargetCirrusAlpha;
            uint brightCloudSeed = enviroAdapter.ActiveCloudFieldSeed;
            Vector2 brightCloudOffset = enviroAdapter.ActiveCloudFieldOffset;
            Assert.That(
                owner.DevTryApplyWeatherOverride(
                    WeatherStateIds.HeavyOvercast.Value,
                    0f,
                    out weatherFailure),
                Is.True,
                weatherFailure);
            yield return null;
            float heavyCoverage = enviroAdapter.ActiveTargetCloudCoverage;
            float heavyLightAbsorption =
                enviroAdapter.ActiveTargetCloudLightAbsorption;

            Assert.That(partlyCoverage, Is.EqualTo(-0.08f).Within(0.001f));
            Assert.That(partlyDensity, Is.EqualTo(0.95f).Within(0.001f));
            Assert.That(partlyCirrus, Is.LessThanOrEqualTo(0.05f));
            Assert.That(brightCoverage, Is.EqualTo(0.34f).Within(0.001f));
            Assert.That(brightDensity, Is.EqualTo(0.62f).Within(0.001f));
            Assert.That(brightDetailErosion,
                Is.EqualTo(0.58f).Within(0.001f));
            Assert.That(brightCirrus, Is.LessThanOrEqualTo(0.03f));
            Assert.That(heavyCoverage, Is.GreaterThan(brightCoverage));
            Assert.That(heavyLightAbsorption,
                Is.GreaterThan(brightLightAbsorption));
            Assert.That(partlyCloudSeed, Is.Not.Zero);
            Assert.That(brightCloudSeed, Is.Not.Zero);
            Assert.That(brightCloudSeed, Is.Not.EqualTo(partlyCloudSeed));
            Assert.That(brightCloudOffset, Is.Not.EqualTo(partlyCloudOffset));
            Assert.That(
                enviroAdapter.ActiveCloudWindSpeedModifier,
                Is.EqualTo(
                        Enviro3ProductionVisualPolicy
                            .CloudFieldWindSpeedModifier)
                    .Within(0.0001f));
            Assert.That(
                enviroAdapter.ActiveCloudTravelSpeed,
                Is.EqualTo(
                        Enviro3ProductionVisualPolicy.CloudFieldTravelSpeed)
                    .Within(0.0001f));
            Assert.That(
                enviroAdapter.ActiveDirectSunlightMultiplier,
                Is.LessThan(clearSunMultiplier * 0.3f),
                "Closed cloud cover must attenuate Enviro direct sunlight.");
            Assert.That(
                enviroAdapter.ActiveDirectionalLightShadowStrength,
                Is.LessThan(clearShadowStrength * 0.3f),
                "Closed cloud cover must suppress hard directional shadows.");
            Assert.That(owner.DevRemoveWeatherOverride(), Is.True);

            Assert.That(
                owner.DevTrySetDateAndTime(
                    new GameDate(1995, 8, 1),
                    20.5d * 3600d,
                    out string timeFailure),
                Is.True,
                timeFailure);
            Assert.That(
                enviroAdapter.ActiveSunLocalHeight,
                Is.GreaterThan(0f),
                "The accepted production dusk must keep the sun above the horizon at 20:30 on 1995-08-01.");
            Assert.That(
                owner.DevTrySetDateAndTime(
                    new GameDate(1995, 8, 1),
                    22d * 3600d,
                    out timeFailure),
                Is.True,
                timeFailure);
            Assert.That(
                enviroAdapter.ActiveSunLocalHeight,
                Is.LessThan(0f),
                "The production design target must pass sunset before 22:00 on 1995-08-01.");
            Assert.That(
                owner.DevTrySetDateAndTime(
                    new GameDate(1995, 8, 2),
                    4.5d * 3600d,
                    out timeFailure),
                Is.True,
                timeFailure);
            Assert.That(
                enviroAdapter.ActiveSunLocalHeight,
                Is.LessThan(0f),
                "The production design target must keep the sun below the horizon at 04:30 on 1995-08-02.");
            Assert.That(
                owner.DevTrySetDateAndTime(
                    new GameDate(1995, 8, 2),
                    5.5d * 3600d,
                    out timeFailure),
                Is.True,
                timeFailure);
            Assert.That(
                enviroAdapter.ActiveSunLocalHeight,
                Is.GreaterThan(0f),
                "The production design target must place sunrise near 05:00 on 1995-08-02.");

            Assert.That(
                owner.DevTrySetDateAndTime(
                    new GameDate(1995, 8, 1),
                    (23d * 60d + 20d) * 60d,
                    out timeFailure),
                Is.True,
                timeFailure);
            Vector3 sunDirectionAt2320 = enviroAdapter.ActiveSunDirection;
            float starIntensityAt2320 = enviroAdapter.ActiveStarIntensity;
            Assert.That(float.IsFinite(starIntensityAt2320), Is.True);
            Assert.That(
                starIntensityAt2320,
                Is.GreaterThanOrEqualTo(2f),
                "The calibrated clear-night sky must keep stars visibly above " +
                "the bright Finnish summer horizon.");
            Assert.That(
                owner.DevTrySetDateAndTime(
                    new GameDate(1995, 8, 1),
                    (23d * 60d + 22d) * 60d,
                    out timeFailure),
                Is.True,
                timeFailure);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(
                Vector3.Angle(
                    sunDirectionAt2320,
                    enviroAdapter.ActiveSunDirection),
                Is.GreaterThan(0.01f),
                "Enviro celestial transforms must follow every authoritative " +
                "clock update instead of snapping only at the night threshold.");
            Assert.That(
                enviroAdapter.ActiveStarIntensity,
                Is.GreaterThan(0f));
            Assert.That(
                Mathf.Abs(
                    enviroAdapter.ActiveStarIntensity -
                    starIntensityAt2320),
                Is.LessThan(0.25f),
                "The 23:20 -> 23:22 transition must not replace the sky with " +
                "a binary black frame.");
            Assert.That(
                float.IsFinite(enviroAdapter.ActiveMoonLocalHeight),
                Is.True);
            }
            else
            {
                NativeHDRPWeatherBackend native =
                    Object.FindFirstObjectByType<NativeHDRPWeatherBackend>(
                        FindObjectsInactive.Include);
                Assert.That(
                    weatherSystem.SelectedBackend,
                    Is.EqualTo(WeatherBackendType.NativeHDRP));
                Assert.That(native, Is.Not.Null);
                Assert.That(native.IsAttached, Is.True);
                Assert.That(weatherSystem.ActiveBackend, Is.SameAs(native));
                Assert.That(enviroAdapter.IsAttached, Is.False);
                Assert.That(native.RuntimeWeatherVolume, Is.Not.Null);
                Assert.That(native.RuntimeWeatherVolume.enabled, Is.True);
                Assert.That(native.RuntimeWeatherVolume.weight, Is.EqualTo(1f));
                Assert.That(native.RuntimeProfile, Is.Not.Null);
                Assert.That(
                    native.RuntimeProfile.TryGet(out PhysicallyBasedSky _),
                    Is.True);
                Assert.That(
                    native.RuntimeProfile.TryGet(out VolumetricClouds _),
                    Is.True);
                Assert.That(
                    native.RuntimeProfile.TryGet(out Fog nativeFog) &&
                    nativeFog.enabled.value,
                    Is.True);
                Assert.That(
                    native.RuntimeProfile.TryGet(out Exposure nativeExposure),
                    Is.True);
                Assert.That(
                    nativeExposure.mode.value,
                    Is.EqualTo(ExposureMode.Automatic));
                Assert.That(native.DirectionalSun, Is.Not.Null);
                Assert.That(native.Geography, Is.Not.Null);
            }

            Enviro3ShelterRemovalBridge shelterBridge =
                Object.FindFirstObjectByType<Enviro3ShelterRemovalBridge>(
                    FindObjectsInactive.Include);
            Assert.That(shelterBridge, Is.Not.Null);
            Assert.That(shelterBridge.enabled, Is.False);
            Enviro3WeatherZoneRemovalBridge hybridShelterBridge =
                Object.FindFirstObjectByType<Enviro3WeatherZoneRemovalBridge>(
                    FindObjectsInactive.Include);
            Assert.That(hybridShelterBridge, Is.Not.Null);
            // House 7 + garage 3 + garage doorway transition 7 + yard hall 3.
            // The doorway was authored on 2026-08-14; the same 13 -> 20 mismatch
            // already exists in DonorWorldLightingBootstrapPlayMode-retry.xml
            // from 2026-09-01, before the Unity 6.6 migration.
            const int expectedRemovalZoneCount = 20;
            for (int frame = 0;
                 frame < 60 &&
                 hybridShelterBridge.ActiveRemovalZoneCount != expectedRemovalZoneCount;
                 frame++)
            {
                yield return null;
            }

            Assert.That(owner.ActiveShelterVolumeCount, Is.EqualTo(2));
            WeatherZoneRegistry zoneRegistry = WeatherZoneRegistry.Active;
            Assert.That(zoneRegistry, Is.Not.Null);
            Assert.That(zoneRegistry.Zones, Has.Count.EqualTo(3));
            Assert.That(zoneRegistry.TryGetZone(
                "weather.shelter.home.house.interior.v1", out _), Is.True);
            Assert.That(zoneRegistry.TryGetZone(
                "weather.shelter.home.garage.interior.v1", out _), Is.True);
            Assert.That(zoneRegistry.TryGetZone(
                "weather.zone.cell_0_-3.yard.machine_hall.v1", out _), Is.True);
            Assert.That(
                hybridShelterBridge.ActiveRemovalZoneCount,
                Is.EqualTo(expectedRemovalZoneCount),
                "The house (7), garage (3), garage doorway transition (7), " +
                "and home-yard machine hall (3) removal ellipsoids must be active.");

            ProductionShelterVolumeAuthoring[] authoredShelters =
                Object.FindObjectsByType<ProductionShelterVolumeAuthoring>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            ProductionShelterVolumeAuthoring houseShelter = null;
            for (int index = 0; index < authoredShelters.Length; index++)
            {
                if (authoredShelters[index].StableId ==
                    "weather.shelter.home.house.interior.v1")
                {
                    houseShelter = authoredShelters[index];
                    break;
                }
            }

            Assert.That(houseShelter, Is.Not.Null);
            Assert.That(
                houseShelter.TryCreateVolume(
                    out ShelterVolume houseVolume,
                    out string shelterFailure),
                Is.True,
                shelterFailure);
            Camera playerCamera = installer.SpawnedPlayer
                .GetComponentInChildren<Camera>(true);
            Assert.That(playerCamera, Is.Not.Null);
            var interiorProbeObject = new GameObject(
                "M07C_InteriorExposureProbe");
            Camera interiorProbeCamera =
                interiorProbeObject.AddComponent<Camera>();
            interiorProbeCamera.enabled = false;
            interiorProbeObject.transform.position = houseVolume.Center;
            Assert.That(
                owner.BindPresentationCamera(interiorProbeCamera),
                Is.True,
                owner.LastFailure);
            owner.DevRefreshPresentation();
            Assert.That(
                owner.ExposureContext,
                Is.EqualTo(WeatherExposureContext.Interior));
            Assert.That(owner.BindPresentationCamera(playerCamera), Is.True);
            Object.Destroy(interiorProbeObject);

            GameTimeService sameTime = owner.AuthoritativeGameTime;
            double weatherSecondsBefore =
                owner.AuthoritativeWeather.SimulationSeconds;
            Scene temporary = SceneManager.CreateScene(
                "M07C_AdditiveLifecycleProbe");
            yield return null;
            yield return null;
            yield return null;
            Assert.That(owner.AuthoritativeGameTime, Is.SameAs(sameTime));
            Assert.That(owner.IsSimulationActive, Is.True);
            yield return SceneManager.UnloadSceneAsync(temporary);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(owner.AuthoritativeGameTime, Is.SameAs(sameTime));
            Assert.That(owner.IsSimulationActive, Is.True);
            Assert.That(
                owner.AuthoritativeWeather.SimulationSeconds,
                Is.GreaterThanOrEqualTo(weatherSecondsBefore));
        }

        [UnityTest]
        public IEnumerator AdditiveBootstrap_DoesNotCreateSecondPersistentOwner()
        {
            ProductionEnvironmentController existing =
                Object.FindFirstObjectByType<ProductionEnvironmentController>(
                    FindObjectsInactive.Include);
            if (existing == null || !existing.IsWorldRevealReady)
            {
                yield return SceneManager.LoadSceneAsync(
                    BootstrapScenePath,
                    LoadSceneMode.Single);
                // Scene activation completes before Start. Let the Bootstrap
                // installer open its explicit preparation gate first.
                yield return null;
                float startupDeadline =
                    Time.realtimeSinceStartup + BootstrapReadyTimeoutSeconds;
                while (Time.realtimeSinceStartup < startupDeadline)
                {
                    ProductionWorldStreamingInstaller installer =
                        Object.FindFirstObjectByType<
                            ProductionWorldStreamingInstaller>(
                            FindObjectsInactive.Include);
                    if (installer != null &&
                        !installer.IsReady &&
                        !installer.IsGameplayPreparationRunning)
                    {
                        Assert.That(
                            installer.TryBeginGameplayPreparation(
                                out string preparationFailure),
                            Is.True,
                            preparationFailure);
                    }

                    if (installer != null && installer.IsReady)
                    {
                        break;
                    }

                    yield return null;
                }
            }

            GameCompositionRoot establishedRoot =
                GameCompositionRoot.ActiveRoot;
            ProductionWorldStreamingInstaller establishedInstaller =
                Object.FindFirstObjectByType<ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include);
            ProductionEnvironmentController establishedEnvironment =
                ProductionEnvironmentController.ActiveOwner;
            Assert.That(establishedInstaller, Is.Not.Null);
            Assert.That(
                establishedInstaller.TryActivateGameplay(
                    out string activationFailure),
                Is.True,
                activationFailure);
            yield return null;
            Assert.That(establishedEnvironment, Is.Not.Null);

            AsyncOperation duplicateLoad = SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Additive);
            yield return duplicateLoad;
            Scene duplicateScene = SceneManager.GetSceneAt(
                SceneManager.sceneCount - 1);
            yield return null;
            yield return null;
            yield return null;

            Assert.That(
                CountActive<GameCompositionRoot>(),
                Is.EqualTo(1));
            Assert.That(
                CountActive<ProductionEnvironmentController>(),
                Is.EqualTo(1));
            GameWeatherSystem establishedWeatherSystem =
                Object.FindFirstObjectByType<GameWeatherSystem>(
                    FindObjectsInactive.Include);
            Assert.That(establishedWeatherSystem, Is.Not.Null);
            Assert.That(
                CountActive<Enviro3EnvironmentAdapter>(),
                Is.EqualTo(
                    establishedWeatherSystem.SelectedBackend ==
                    WeatherBackendType.NativeHDRP
                        ? 0
                        : 1));
            Assert.That(
                GameCompositionRoot.ActiveRoot,
                Is.SameAs(establishedRoot));
            Assert.That(
                ProductionEnvironmentController.ActiveOwner,
                Is.SameAs(establishedEnvironment));
            Assert.That(establishedEnvironment.IsSimulationActive, Is.True);
            Assert.That(
                establishedEnvironment.PresentationStatus.IsOperational,
                Is.True);

            if (duplicateScene.IsValid() && duplicateScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(duplicateScene);
            }
        }

        [UnityTest]
        public IEnumerator SingleBootstrapReload_ReplacesThePlayableSession()
        {
            yield return DestroyPersistentRootIfPresent();
            yield return SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            yield return WaitForBootstrapReady();
            GameCompositionRoot previousRoot =
                GameCompositionRoot.ActiveRoot;
            Assert.That(previousRoot, Is.Not.Null);

            yield return SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            yield return WaitForBootstrapReady();

            Assert.That(GameCompositionRoot.ActiveRoot, Is.Not.Null);
            Assert.That(
                GameCompositionRoot.ActiveRoot,
                Is.Not.SameAs(previousRoot));
            Assert.That(CountActive<GameCompositionRoot>(), Is.EqualTo(1));
            Assert.That(
                CountActive<ProductionEnvironmentController>(),
                Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DifferentSingleRootScene_ReplacesSessionAndKeepsEnvironmentlessFixtureWorking()
        {
            yield return DestroyPersistentRootIfPresent();
            yield return SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            yield return WaitForBootstrapReady();
            GameCompositionRoot previousRoot =
                GameCompositionRoot.ActiveRoot;

            yield return SceneManager.LoadSceneAsync(
                EnvironmentlessStreamingScenePath,
                LoadSceneMode.Single);

            ProductionWorldStreamingInstaller replacementInstaller = null;
            float startupDeadline = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < startupDeadline)
            {
                replacementInstaller = GameCompositionRoot.ActiveRoot == null
                    ? null
                    : GameCompositionRoot.ActiveRoot.GetComponent<
                        ProductionWorldStreamingInstaller>();
                if (replacementInstaller != null &&
                    replacementInstaller.IsReady)
                {
                    break;
                }

                yield return null;
            }

            Assert.That(replacementInstaller, Is.Not.Null);
            Assert.That(
                replacementInstaller.IsReady,
                Is.True,
                replacementInstaller == null
                    ? "Replacement installer was not created."
                    : "World-only replacement did not become ready. " +
                      $"coherent={replacementInstaller.HasCoherentStartupConfiguration} " +
                      $"hasFocus={replacementInstaller.WorldStreaming.HasFocus} " +
                      $"isStreaming={replacementInstaller.WorldStreaming.IsStreaming}");
            Assert.That(replacementInstaller.Environment, Is.Null);
            Assert.That(replacementInstaller.SpawnedPlayer.activeSelf, Is.True);
            Assert.That(GameCompositionRoot.ActiveRoot, Is.Not.Null);
            Assert.That(GameCompositionRoot.ActiveRoot, Is.Not.SameAs(previousRoot));
            Assert.That(GameCompositionRoot.ActiveRoot.ReplacedPreviousSession, Is.True);
            Assert.That(GameCompositionRoot.ActiveRoot.BoundServiceCount, Is.EqualTo(1));
            Assert.That(CountActive<GameCompositionRoot>(), Is.EqualTo(1));
            Assert.That(
                CountActive<ProductionEnvironmentController>(),
                Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator SameSceneRuntimeDuplicate_DoesNotReplaceSession()
        {
            yield return DestroyPersistentRootIfPresent();
            yield return SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            yield return WaitForBootstrapReady();
            GameCompositionRoot establishedRoot =
                GameCompositionRoot.ActiveRoot;

            var duplicateObject = new GameObject(
                "M07C_SameSceneCompositionRootDuplicate");
            SceneManager.MoveGameObjectToScene(
                duplicateObject,
                SceneManager.GetActiveScene());
            duplicateObject.AddComponent<GameCompositionRoot>();
            yield return null;
            yield return null;

            Assert.That(
                GameCompositionRoot.ActiveRoot,
                Is.SameAs(establishedRoot));
            Assert.That(CountActive<GameCompositionRoot>(), Is.EqualTo(1));
            Assert.That(duplicateObject == null, Is.True);
        }

        [UnityTest]
        public IEnumerator LeavingBootstrapForSingleScene_TearsDownSession()
        {
            yield return DestroyPersistentRootIfPresent();
            yield return SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            yield return WaitForBootstrapReady();
            GameCompositionRoot previousRoot =
                GameCompositionRoot.ActiveRoot;

            yield return SceneManager.LoadSceneAsync(
                NonSessionScenePath,
                LoadSceneMode.Single);
            yield return null;
            yield return null;

            Assert.That(GameCompositionRoot.ActiveRoot, Is.Null);
            Assert.That(
                ProductionEnvironmentController.ActiveOwner,
                Is.Null);
            Assert.That(previousRoot == null, Is.True);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return DestroyPersistentRootIfPresent();
        }

        [UnityTest]
        public IEnumerator BootstrapAudioBackend_UsesFallbackOrLoadsAllRequiredWwiseBanks()
        {
            yield return DestroyPersistentRootIfPresent();
            yield return SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            yield return WaitForBootstrapReady();

            AudioBackendRouter router = Object.FindFirstObjectByType<AudioBackendRouter>(
                FindObjectsInactive.Include);
            Assert.That(router, Is.Not.Null);
            bool requireLiveWwise = string.Equals(
                Environment.GetEnvironmentVariable("MSC_REQUIRE_LIVE_WWISE"),
                "1",
                StringComparison.Ordinal);

            for (int frame = 0;
                 frame < 360 && requireLiveWwise && router.Kind != AudioBackendKind.Wwise;
                 frame++)
            {
                yield return null;
            }

            AudioRuntimeSnapshot snapshot = router.CaptureSnapshot();
            Assert.That(snapshot.IsReady, Is.True, snapshot.LastFailure);
            if (requireLiveWwise)
            {
                Assert.That(snapshot.Kind, Is.EqualTo(AudioBackendKind.Wwise));
                Assert.That(snapshot.IsFallback, Is.False);
                Assert.That(snapshot.LoadedBankCount, Is.EqualTo(6));
                Assert.That(snapshot.MissingBanks, Is.Empty);
            }
            else
            {
                Assert.That(
                    snapshot.Kind == AudioBackendKind.Wwise ||
                    snapshot.Kind == AudioBackendKind.Unity,
                    Is.True,
                    snapshot.LastFailure);
            }
        }

        private static int CountActive<T>() where T : Component
        {
            T[] values = Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int count = 0;
            for (int index = 0; index < values.Length; index++)
            {
                T value = values[index];
                if (value.gameObject.activeInHierarchy &&
                    (!(value is Behaviour behaviour) || behaviour.enabled))
                {
                    count++;
                }
            }

            return count;
        }

        private static IEnumerator DestroyPersistentRootIfPresent()
        {
            GameCompositionRoot[] roots = Object.FindObjectsByType<
                GameCompositionRoot>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < roots.Length; index++)
            {
                Object.Destroy(roots[index].gameObject);
            }

            if (roots.Length > 0)
            {
                yield return null;
                yield return null;
            }
        }

        private static IEnumerator WaitForBootstrapReady()
        {
            // LoadSceneAsync completes before Start is guaranteed to run.
            yield return null;
            float startupDeadline =
                Time.realtimeSinceStartup + BootstrapReadyTimeoutSeconds;
            while (Time.realtimeSinceStartup < startupDeadline)
            {
                ProductionWorldStreamingInstaller installer =
                    Object.FindFirstObjectByType<
                        ProductionWorldStreamingInstaller>(
                        FindObjectsInactive.Include);
                if (installer != null &&
                    !installer.IsReady &&
                    !installer.IsGameplayPreparationRunning)
                {
                    Assert.That(
                        installer.TryBeginGameplayPreparation(
                            out string preparationFailure),
                        Is.True,
                        preparationFailure);
                }

                if (installer != null && installer.IsReady)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                "Production Bootstrap did not become ready before the " +
                $"real-time {BootstrapReadyTimeoutSeconds:0}s budget.");
        }
    }
}
