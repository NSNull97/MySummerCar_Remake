using System.Collections;
using Enviro;
using MSC.Bootstrap;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Production;
using MSC.Weather.System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.WeatherProduction
{
    public sealed class HybridEnvironmentLifecyclePlayModeTests
    {
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string TeimoCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_-3_0_Legacy.unity";
        private const string FleetariCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_3_-1_Legacy.unity";

        [UnityTest]
        public IEnumerator Bootstrap_ActivatesSingleHybridOwnershipGraph()
        {
            yield return DestroyPersistentRootIfPresent();
            yield return SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            for (int frame = 0; frame < 120; frame++)
            {
                NativeHdrpWeatherBridge candidate =
                    Object.FindFirstObjectByType<NativeHdrpWeatherBridge>(
                        FindObjectsInactive.Include);
                GameWeatherSystem weatherCandidate =
                    Object.FindFirstObjectByType<GameWeatherSystem>(
                        FindObjectsInactive.Include);
                Enviro3EnvironmentAdapter adapterCandidate =
                    Object.FindFirstObjectByType<Enviro3EnvironmentAdapter>(
                        FindObjectsInactive.Include);
                if (candidate != null && candidate.IsReady &&
                    CountActive<EnviroManager>() == 1 &&
                    weatherCandidate != null && weatherCandidate.IsAttached &&
                    adapterCandidate != null && adapterCandidate.IsAttached)
                {
                    break;
                }

                yield return null;
            }

            EnviroManager manager = Object.FindFirstObjectByType<EnviroManager>(
                FindObjectsInactive.Include);
            Enviro3EnvironmentAdapter adapter =
                Object.FindFirstObjectByType<Enviro3EnvironmentAdapter>(
                    FindObjectsInactive.Include);
            NativeHdrpWeatherBridge bridge =
                Object.FindFirstObjectByType<NativeHdrpWeatherBridge>(
                    FindObjectsInactive.Include);
            HybridEnvironmentMarker marker =
                Object.FindFirstObjectByType<HybridEnvironmentMarker>(
                    FindObjectsInactive.Include);
            GameWeatherSystem weatherSystem =
                Object.FindFirstObjectByType<GameWeatherSystem>(
                    FindObjectsInactive.Include);

            Assert.That(CountActive<EnviroManager>(), Is.EqualTo(1));
            Assert.That(CountActive<NativeHdrpWeatherBridge>(), Is.EqualTo(1));
            Assert.That(CountActive<HybridEnvironmentMarker>(), Is.EqualTo(1));
            Assert.That(
                CountActive<Enviro3ShelterRemovalBridge>(),
                Is.Zero,
                "Legacy and hybrid shelter removal must never run together.");
            Assert.That(manager, Is.Not.Null);
            Assert.That(adapter, Is.Not.Null);
            Assert.That(weatherSystem, Is.Not.Null);
            Assert.That(
                weatherSystem.SelectedBackend,
                Is.EqualTo(WeatherBackendType.EnviroLegacy));
            Assert.That(weatherSystem.IsAttached, Is.True);
            Assert.That(adapter.IsAttached, Is.True);
            Assert.That(adapter.HybridNativeHdrpOwnership, Is.True);
            Assert.That(
                weatherSystem.ActiveBackend.Ownership &
                (WeatherPresentationOwnership.Fog |
                 WeatherPresentationOwnership.Exposure |
                 WeatherPresentationOwnership.IndirectLighting),
                Is.EqualTo(WeatherPresentationOwnership.None));
            Assert.That(adapter.HasConfiguredCelestialSky, Is.True);
            Assert.That(adapter.HasConfiguredMoonLighting, Is.True);
            Assert.That(bridge, Is.Not.Null);
            Assert.That(bridge.IsReady, Is.True);
            Assert.That(bridge.FogOwner, Is.EqualTo(nameof(NativeHdrpWeatherBridge)));
            Assert.That(
                bridge.ExposureOwner,
                Is.EqualTo(nameof(NativeHdrpWeatherBridge)));
            Assert.That(bridge.UsesCameraIndependentExposure, Is.True);
            Assert.That(bridge.CurrentFixedExposureEv, Is.InRange(7f, 14f));
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.IsComplete, Is.True);
            Assert.That(manager.Fog.Settings.controlHDRPFog, Is.False);
            Assert.That(manager.Fog.Settings.controlHDRPVolumetrics, Is.False);
            Assert.That(manager.Lighting.Settings.controlExposure, Is.False);
        }

        [UnityTest]
        public IEnumerator StreamedBuildingCatalog_RegistersAndUnloadsWithoutStaleZones()
        {
            yield return DestroyPersistentRootIfPresent();
            yield return SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            yield return null;

            WeatherZoneStreamingBinder binder =
                Object.FindFirstObjectByType<WeatherZoneStreamingBinder>(
                    FindObjectsInactive.Include);
            WeatherZoneRegistry registry =
                Object.FindFirstObjectByType<WeatherZoneRegistry>(
                    FindObjectsInactive.Include);
            Enviro3WeatherZoneRemovalBridge removalBridge =
                Object.FindFirstObjectByType<Enviro3WeatherZoneRemovalBridge>(
                    FindObjectsInactive.Include);
            Assert.That(binder, Is.Not.Null);
            Assert.That(binder.Catalog, Is.Not.Null);
            Assert.That(
                binder.Catalog.Definitions.Count,
                Is.EqualTo(20),
                "All active static donor NoRain locations must be represented; " +
                "the moving bus cabin is handled by its vehicle presenter.");
            Assert.That(registry, Is.Not.Null);
            Assert.That(removalBridge, Is.Not.Null);
            int baselineRemovalCount = removalBridge.ActiveRemovalZoneCount;

            yield return LoadCellIfNeeded(TeimoCellScenePath);
            yield return null;
            Assert.That(
                registry.TryGetZone(
                    "weather.zone.cell_-3_0.teimo.shop.v1",
                    out WeatherZone shop),
                Is.True);
            Assert.That(shop.Profile.Kind, Is.EqualTo(WeatherZoneKind.ClosedInterior));
            Assert.That(
                shop.GetComponent<BoxCollider>().size,
                Is.EqualTo(new Vector3(6.6733313f, 10.67478f, 3.60f)));
            Assert.That(
                registry.TryGetZone(
                    "weather.zone.cell_-3_0.teimo.pub.v1",
                    out WeatherZone pub),
                Is.True);
            Assert.That(
                pub.GetComponent<BoxCollider>().size,
                Is.EqualTo(new Vector3(6.251738f, 9.712752f, 3.60f)));
            Assert.That(
                registry.TryGetZone(
                    "weather.zone.cell_-3_0.rowhouse.apartment.v1",
                    out _),
                Is.True);
            Assert.That(
                registry.TryGetZone(
                    "weather.zone.cell_-3_0.inspection.hall.v1",
                    out WeatherZone inspectionHall),
                Is.True);
            Assert.That(
                inspectionHall.Profile.Kind,
                Is.EqualTo(WeatherZoneKind.ClosedInterior));
            Assert.That(
                inspectionHall.Profile.CreateClosedExposure()
                    .WeatherAudioExposure,
                Is.EqualTo(0.15f));
            Assert.That(
                inspectionHall.GetComponent<BoxCollider>().size,
                Is.EqualTo(new Vector3(42.57523f, 15.707606f, 4.00f)));
            Assert.That(
                registry.TryGetZone(
                    "weather.zone.cell_-3_0.inspection.office.v1",
                    out WeatherZone inspectionOffice),
                Is.True);
            Assert.That(
                inspectionOffice.Profile.Kind,
                Is.EqualTo(WeatherZoneKind.ClosedInterior));
            Assert.That(binder.SpawnedZoneCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(
                removalBridge.ActiveRemovalZoneCount,
                Is.GreaterThan(baselineRemovalCount));

            yield return SceneManager.UnloadSceneAsync(TeimoCellScenePath);
            yield return null;
            Assert.That(
                registry.TryGetZone(
                    "weather.zone.cell_-3_0.teimo.shop.v1",
                    out _),
                Is.False);
            Assert.That(
                registry.TryGetZone(
                    "weather.zone.cell_-3_0.teimo.pub.v1",
                    out _),
                Is.False);

            yield return LoadCellIfNeeded(FleetariCellScenePath);
            yield return null;
            Assert.That(
                registry.TryGetZone(
                    "weather.zone.cell_3_-1.fleetari.workshop.v1",
                    out WeatherZone workshop),
                Is.True);
            Assert.That(
                workshop.GetComponent<BoxCollider>().size,
                Is.EqualTo(new Vector3(17.05844f, 21.653284f, 5.40f)));
            Assert.That(
                registry.TryGetZone(
                    "weather.zone.cell_3_-1.abandoned_house.main.v1",
                    out _),
                Is.True);
            Assert.That(
                registry.TryGetZone(
                    "weather.zone.cell_3_-1.bridge.dirt.shelter.v1",
                    out WeatherZone dirtBridge),
                Is.True);
            AssertOpenShelter(dirtBridge);

            yield return SceneManager.UnloadSceneAsync(FleetariCellScenePath);
            yield return null;
            Assert.That(
                registry.TryGetZone(
                    "weather.zone.cell_3_-1.fleetari.workshop.v1",
                    out _),
                Is.False);

            (string ScenePath, string ZoneId)[] remainingCells =
            {
                (
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_0_-1_Legacy.unity",
                    "weather.zone.cell_0_-1.cabin.interior.v1"),
                (
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_0_0_Legacy.unity",
                    "weather.zone.cell_0_0.cabin.shed.v1"),
                (
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_-2_-2_Legacy.unity",
                    "weather.zone.cell_-2_-2.cottage.interior.v1"),
                (
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_1_0_Legacy.unity",
                    "weather.zone.cell_1_0.dancehall.shelter.v1"),
                (
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_-1_-5_Legacy.unity",
                    "weather.zone.cell_-1_-5.jail.interior.v1"),
                (
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_2_-1_Legacy.unity",
                    "weather.zone.cell_2_-1.abandoned_house.west.v1"),
                (
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_4_-3_Legacy.unity",
                    "weather.zone.cell_4_-3.factory.machine_hall.v1"),
                (
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_-2_0_Legacy.unity",
                    "weather.zone.cell_-2_0.farm.machine_hall.v1"),
                (
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_-3_-4_Legacy.unity",
                    "weather.zone.cell_-3_-4.strawberry.machine_hall.v1"),
                (
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_0_-3_Legacy.unity",
                    "weather.zone.cell_0_-3.yard.machine_hall.v1"),
                (
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_3_0_Legacy.unity",
                    "weather.zone.cell_3_0.bridge.highway.shelter.v1"),
            };
            for (int index = 0; index < remainingCells.Length; index++)
            {
                yield return LoadCellIfNeeded(remainingCells[index].ScenePath);
                yield return null;
                Assert.That(
                    registry.TryGetZone(
                        remainingCells[index].ZoneId,
                        out WeatherZone streamedZone),
                    Is.True,
                    remainingCells[index].ZoneId);
                if (streamedZone.Profile.Kind == WeatherZoneKind.Shelter)
                {
                    AssertOpenShelter(streamedZone);
                }
                Assert.That(
                    removalBridge.ActiveRemovalZoneCount,
                    Is.GreaterThan(baselineRemovalCount),
                    remainingCells[index].ZoneId +
                    " did not create local precipitation removal.");
                yield return SceneManager.UnloadSceneAsync(
                    remainingCells[index].ScenePath);
                yield return null;
                Assert.That(
                    registry.TryGetZone(remainingCells[index].ZoneId, out _),
                    Is.False,
                    remainingCells[index].ZoneId + " remained registered.");
            }

            Assert.That(binder.ActiveCellCount, Is.Zero);
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

        private static void AssertOpenShelter(WeatherZone zone)
        {
            Assert.That(zone, Is.Not.Null);
            Assert.That(zone.Profile.Kind, Is.EqualTo(WeatherZoneKind.Shelter));
            WeatherExposureState exposure = zone.Profile.CreateClosedExposure();
            Assert.That(exposure.WeatherAudioExposure, Is.EqualTo(1f));
            Assert.That(exposure.IndoorFactor, Is.Zero);
            Assert.That(exposure.PrecipitationExposure, Is.EqualTo(0.1f));
        }

        private static IEnumerator DestroyPersistentRootIfPresent()
        {
            GameCompositionRoot[] roots = Object.FindObjectsByType<GameCompositionRoot>(
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

        private static IEnumerator LoadCellIfNeeded(string scenePath)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield return SceneManager.LoadSceneAsync(
                    scenePath,
                    LoadSceneMode.Additive);
            }
        }
    }
}
