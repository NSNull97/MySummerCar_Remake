using System.Collections;
using MSC.Bootstrap;
using MSC.Core.Time;
using MSC.Weather.Domain;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Production;
using MSC.Weather.Wetness;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
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
            for (int frame = 0; frame < 900 && !installer.IsReady; frame++)
            {
                yield return null;
            }

            Assert.That(installer.IsReady, Is.True);
            Assert.That(owner.IsWorldRevealReady, Is.True);
            Assert.That(owner.WasRestoreAppliedBeforeReveal, Is.True);
            Assert.That(owner.IsSimulationActive, Is.True);
            Assert.That(owner.PresentationStatus.IsOperational, Is.True);
            Assert.That(installer.SpawnedPlayer.activeSelf, Is.True);
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
            Assert.That(
                enviroAdapter.HasIsolatedRuntimeVolumeTargets,
                Is.True);
            Assert.That(enviroAdapter.IsCustomHeightFogDisabled, Is.True);
            Assert.That(
                enviroAdapter.ActiveFogMeanFreePathMeters,
                Is.GreaterThanOrEqualTo(300f));
            Assert.That(
                enviroAdapter.ActiveVolumetricLightDimmer,
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                enviroAdapter.ActiveRainParticleMaxScreenSize,
                Is.GreaterThanOrEqualTo(0.01f));
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
            Assert.That(enviroAdapter.IsAuroraSuppressed, Is.True);

            Enviro3ShelterRemovalBridge shelterBridge =
                Object.FindFirstObjectByType<Enviro3ShelterRemovalBridge>(
                    FindObjectsInactive.Include);
            Assert.That(shelterBridge, Is.Not.Null);
            for (int frame = 0;
                 frame < 60 && shelterBridge.ActiveShelterCount != 2;
                 frame++)
            {
                yield return null;
            }

            Assert.That(owner.ActiveShelterVolumeCount, Is.EqualTo(2));
            Assert.That(shelterBridge.ActiveShelterCount, Is.EqualTo(2));
            Assert.That(shelterBridge.ActiveZoneCount, Is.EqualTo(5));

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
                for (int frame = 0; frame < 900; frame++)
                {
                    ProductionWorldStreamingInstaller installer =
                        Object.FindFirstObjectByType<
                            ProductionWorldStreamingInstaller>(
                            FindObjectsInactive.Include);
                    if (installer != null && installer.IsReady)
                    {
                        break;
                    }

                    yield return null;
                }
            }

            GameCompositionRoot establishedRoot =
                GameCompositionRoot.ActiveRoot;
            ProductionEnvironmentController establishedEnvironment =
                ProductionEnvironmentController.ActiveOwner;
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
            Assert.That(
                CountActive<Enviro3EnvironmentAdapter>(),
                Is.EqualTo(1));
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
            for (int frame = 0; frame < 900; frame++)
            {
                ProductionWorldStreamingInstaller installer =
                    Object.FindFirstObjectByType<
                        ProductionWorldStreamingInstaller>(
                        FindObjectsInactive.Include);
                if (installer != null && installer.IsReady)
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail("Production Bootstrap did not become ready.");
        }
    }
}
