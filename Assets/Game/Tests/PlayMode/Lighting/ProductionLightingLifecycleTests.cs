using System.Collections;
using System.Linq;
using System.Reflection;
using MSC.Bootstrap;
using MSC.Core.Time;
using MSC.Interaction;
using MSC.Interaction.Query;
using MSC.Items;
using MSC.Lighting.Production;
using MSC.World.Lighting;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Lighting.Tests.PlayMode
{
    public sealed class ProductionLightingLifecycleTests
    {
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string TeimoCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/" +
            "Streaming/Scenes/Cells/World_Cell_-3_0_Legacy.unity";
        private const string FleetariCellScenePath =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/" +
            "Streaming/Scenes/Cells/World_Cell_3_-1_Legacy.unity";
        private static readonly Vector3 FleetariInteriorPosition =
            new Vector3(1724.5f, 7.2f, -302.5f);

        [UnityTest]
        public IEnumerator Bootstrap_InitializesLightingAndRestoresSwitchState()
        {
            yield return DestroyPersistentRootIfPresent();
            AsyncOperation preloadedCell = SceneManager.LoadSceneAsync(
                TeimoCellScenePath,
                LoadSceneMode.Single);
            yield return preloadedCell;
            AsyncOperation load = SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Additive);
            yield return load;

            ProductionWorldStreamingInstaller world =
                Object.FindFirstObjectByType<ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include);
            ProductionLightingInstaller lighting =
                Object.FindFirstObjectByType<ProductionLightingInstaller>(
                    FindObjectsInactive.Include);
            Assert.That(world, Is.Not.Null);
            Assert.That(lighting, Is.Not.Null);

            string prepareFailure = string.Empty;
            for (int frame = 0;
                 frame < 900 && (!world.IsReady || !lighting.IsInitialized);
                 frame++)
            {
                if (!world.IsReady &&
                    !world.IsGameplayPreparationRunning)
                {
                    world.TryBeginGameplayPreparation(out prepareFailure);
                }

                yield return null;
            }

            Assert.That(world.IsReady, Is.True, prepareFailure);
            Assert.That(lighting.IsInitialized, Is.True);
            Assert.That(lighting.ProfileCatalog.Profiles.Length, Is.EqualTo(20));
            Assert.That(lighting.RuntimeManager.RegisteredFixtureCount,
                Is.GreaterThan(0));

            LightingValidationRunner runner =
                Object.FindFirstObjectByType<LightingValidationRunner>(
                    FindObjectsInactive.Include);
            Assert.That(runner, Is.Not.Null);
            world.SpawnedPlayer.transform.position =
                new Vector3(-1378f, 6.1f, 142f);
            Assert.That(
                runner.TrySetEnvironment(
                    19.5d,
                    "weather.clear",
                    out string shopEnvironmentFailure),
                Is.True,
                shopEnvironmentFailure);
            yield return new WaitForSecondsRealtime(0.5f);

            GameLightFixture[] fixtures =
                Object.FindObjectsByType<GameLightFixture>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int liveShopFixtureCount = 0;
            int livePubFixtureCount = 0;
            int refrigeratorFixtureCount = 0;
            int streetFixtureCount = 0;
            int shopShadowCount = 0;
            for (int index = 0; index < fixtures.Length; index++)
            {
                GameLightFixture fixture = fixtures[index];
                if (fixture.Profile.Category ==
                    LightFixtureCategory.StreetLamp)
                {
                    streetFixtureCount++;
                    Assert.That(
                        fixture.transform.Find("Emission Lens (Runtime)"),
                        Is.Not.Null,
                        "A street pole must emit only from a generated lamp " +
                        "lens, never from the complete donor pole renderer.");
                }

                if (fixture.BusinessId == "service.teimo.pub")
                {
                    livePubFixtureCount++;
                    Light pubLight = fixture.GetComponent<Light>();
                    // The pub is deliberately off while Teimo is working in
                    // the shop, so validate authored output on the profile
                    // instead of the power-gated runtime Light value.
                    Assert.That(fixture.Profile.Intensity,
                        Is.EqualTo(32_500f).Within(0.01f));
                    Assert.That(pubLight.colorTemperature,
                        Is.EqualTo(6_000f).Within(0.01f));
                    continue;
                }

                if (fixture.BusinessId != "service.teimo.shop")
                {
                    continue;
                }

                liveShopFixtureCount++;
                Assert.That(
                    fixture.LogicalOn,
                    Is.True,
                    "At 19:30 Teimo is in the shop, so every shop fixture " +
                    "must be logically on.");
                Light liveLight = fixture.GetComponent<Light>();
                Assert.That(liveLight, Is.Not.Null);
                Assert.That(liveLight.enabled, Is.True);
                Assert.That(liveLight.intensity, Is.GreaterThan(0f));
                if (liveLight.shadows != LightShadows.None)
                {
                    shopShadowCount++;
                }
                if (fixture.Profile.Category ==
                    LightFixtureCategory.TeimoShop)
                {
                    Assert.That(liveLight.intensity,
                        Is.EqualTo(32_500f).Within(0.01f));
                    Assert.That(
                        liveLight.colorTemperature,
                        Is.EqualTo(6_000f).Within(0.01f));
                    Assert.That(
                        fixture.transform.Find("Emission Lens (Runtime)"),
                        Is.Not.Null,
                        $"Ceiling fixture '{fixture.FixtureId}' must own an emissive lens.");
                    Assert.That(liveLight.type,
                        Is.EqualTo(LightType.Rectangle));
                    Assert.That(
                        Vector3.Angle(
                            liveLight.transform.forward,
                            Vector3.down),
                        Is.LessThan(1f),
                        "Teimo ceiling rectangles must face down using the " +
                        "captured donor fixture orientation.");
                }
                if (fixture.Profile.Category ==
                    LightFixtureCategory.RefrigeratedDisplay)
                {
                    refrigeratorFixtureCount++;
                    Assert.That(liveLight.type, Is.EqualTo(LightType.Rectangle));
                    Assert.That(
                        liveLight.colorTemperature,
                        Is.EqualTo(15_000f).Within(0.01f));
                    Assert.That(
                        liveLight.intensity,
                        Is.EqualTo(15_000f).Within(0.01f));
                    Assert.That(
                        fixture.transform.Find("Emission Lens (Runtime)"),
                        Is.Null,
                        "Refrigerator area lights must not grow a generated emissive lens.");
                }
            }

            Assert.That(
                liveShopFixtureCount,
                Is.EqualTo(8),
                "The occupied shop owns five ceiling fixtures plus three " +
                "refrigerator display lights.");
            Assert.That(
                livePubFixtureCount,
                Is.EqualTo(1),
                "Only the eastern ceiling fixture belongs to the pub.");
            Assert.That(refrigeratorFixtureCount, Is.EqualTo(3));
            Assert.That(
                shopShadowCount,
                Is.GreaterThan(0),
                "Nearby shop ceiling lights must receive the bounded local " +
                "shadow budget.");
            Assert.That(
                streetFixtureCount,
                Is.GreaterThanOrEqualTo(2),
                "A preloaded Teimo cell must replay its street lights too.");

            // Reproduce a same-frame streamed-cell replacement. Destroy is
            // deferred, so the outgoing root must unregister its stable IDs
            // synchronously before the replacement fixtures are created.
            Scene teimoScene = SceneManager.GetSceneByPath(TeimoCellScenePath);
            Assert.That(teimoScene.IsValid(), Is.True);
            WorldLightingProbeRuntime probeRuntime =
                world.LightingProbeRuntime;
            Assert.That(probeRuntime, Is.Not.Null);
            MethodInfo willUnload = typeof(WorldLightingProbeRuntime).GetMethod(
                "HandleOwnedSceneWillUnload",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo didLoad = typeof(WorldLightingProbeRuntime).GetMethod(
                "HandleOwnedSceneLoaded",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(willUnload, Is.Not.Null);
            Assert.That(didLoad, Is.Not.Null);
            willUnload.Invoke(probeRuntime, new object[] { teimoScene });
            didLoad.Invoke(probeRuntime, new object[] { teimoScene });
            yield return null;
            fixtures = Object.FindObjectsByType<GameLightFixture>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(fixtures.Count(fixture =>
                    fixture.BusinessId == "service.teimo.shop"),
                Is.EqualTo(8));
            Assert.That(fixtures.Count(fixture =>
                    fixture.BusinessId == "service.teimo.pub"),
                Is.EqualTo(1));
            Assert.That(
                fixtures.Where(fixture =>
                        fixture.BusinessId == "service.teimo.shop" ||
                        fixture.BusinessId == "service.teimo.pub")
                    .Select(fixture => fixture.FixtureId)
                    .Distinct()
                    .Count(),
                Is.EqualTo(9),
                "Streaming replacement must preserve nine unique Teimo fixtures.");

            Assert.That(
                runner.TrySetEnvironment(
                    23.75d,
                    "weather.clear",
                    out string environmentFailure),
                Is.True,
                environmentFailure);
            runner.SetSwitch("switch.home.kitchen", true);
            yield return new WaitForSecondsRealtime(1f);

            fixtures = Object.FindObjectsByType<GameLightFixture>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            GameLightFixture[] nightStreetFixtures = fixtures.Where(fixture =>
                    fixture.Profile.Category == LightFixtureCategory.StreetLamp)
                .ToArray();
            Assert.That(
                nightStreetFixtures.Any(fixture => fixture.LogicalOn),
                Is.True,
                "At night the public street circuit must actually power the " +
                "streamed Teimo lamps, not merely instantiate fixture objects.");
            Assert.That(
                nightStreetFixtures.Any(fixture =>
                    fixture.GetComponent<Light>()?.enabled == true),
                Is.True,
                "At least one nearby Teimo street lamp must survive the " +
                "distance budget as a visible HDRP light.");

            Assert.That(lighting.TrafficVehicleAdapter, Is.Not.Null);
            Assert.That(
                lighting.TrafficVehicleAdapter.BoundVehicleCount,
                Is.GreaterThan(0),
                "Active story-traffic cars must receive generated low beams.");
            Assert.That(lighting.TrafficVehicleAdapter.NightLightingOn,
                Is.True,
                "Traffic headlights must share the environment dusk gate.");
            Assert.That(
                lighting.TrafficVehicleAdapter.GeneratedLowBeamLightCount,
                Is.EqualTo(
                    lighting.TrafficVehicleAdapter.BoundVehicleCount * 2),
                "Every bound traffic wrapper must receive two actual low-beam " +
                "Light components.");
            Assert.That(
                lighting.TrafficVehicleAdapter
                    .AreGeneratedFixturesElectricallyBound(),
                Is.True,
                "Every generated low beam must remain bound to the traffic " +
                "electrical/dusk authority across wrapper activation.");
            Assert.That(
                lighting.TrafficVehicleAdapter
                    .AreGeneratedFixturesAlignedToHeadlamps(),
                Is.True,
                "Traffic low beams and their emission lenses must follow the " +
                "exact story-car or donor-wheel-derived front fascia, not " +
                "renderer bounds in front of the vehicle.");
            LightFixtureProfile lowBeamProfile =
                lighting.ProfileCatalog.GetRequiredProfile(
                    LightFixtureCategory.VehicleLowBeam);
            Assert.That(lowBeamProfile.Intensity,
                Is.EqualTo(6500f).Within(0.01f));
            Assert.That(lowBeamProfile.RangeMeters,
                Is.EqualTo(35f).Within(0.01f));
            Assert.That(lowBeamProfile.BeamFogIntensity,
                Is.LessThanOrEqualTo(0.03f),
                "Traffic VLB must remain a restrained fog cue, not a solid " +
                "white searchlight.");

            Assert.That(lighting.BusinessAdapter, Is.Not.Null);
            Assert.That(
                lighting.BusinessAdapter.IsBusinessOpenAndOwnerPresent(
                    "service.teimo.pub"),
                Is.True,
                "At 23:45 Teimo must make the pub available to lighting.");
            yield return new WaitForSecondsRealtime(0.25f);
            GameLightFixture livePubFixture = Object
                .FindObjectsByType<GameLightFixture>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(fixture =>
                    fixture.BusinessId == "service.teimo.pub");
            Assert.That(livePubFixture.LogicalOn, Is.True,
                "At 23:45 the streamed pub fixture itself must be on, not " +
                "merely the business-presence adapter.");
            Assert.That(livePubFixture.GetComponent<Light>().enabled, Is.True);

            FieldInfo bindingCatalogField = typeof(ProductionLightingInstaller)
                .GetField(
                    "bindingCatalog",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(bindingCatalogField, Is.Not.Null);
            var bindingCatalog = (WorldLightingBindingCatalog)
                bindingCatalogField.GetValue(lighting);
            Assert.That(bindingCatalog, Is.Not.Null);
            int pubFixtureCount = 0;
            int shopFixtureCount = 0;
            for (int index = 0;
                 index < bindingCatalog.Bindings.Count;
                 index++)
            {
                WorldLightingFixtureBinding binding =
                    bindingCatalog.Bindings[index];
                if (binding.BusinessId == "service.teimo.pub")
                {
                    pubFixtureCount++;
                }
                else if (binding.BusinessId == "service.teimo.shop")
                {
                    shopFixtureCount++;
                }
            }

            Assert.That(pubFixtureCount, Is.EqualTo(1));
            Assert.That(shopFixtureCount, Is.EqualTo(8));

            LightFixtureProfile domesticProfile =
                lighting.ProfileCatalog.GetRequiredProfile(
                    LightFixtureCategory.DomesticIncandescent);
            LightFixtureProfile enclosedProfile =
                lighting.ProfileCatalog.GetRequiredProfile(
                    LightFixtureCategory.EnclosedCeiling);
            Assert.That(domesticProfile.ColorTemperatureKelvin,
                Is.EqualTo(3300f).Within(0.01f));
            Assert.That(enclosedProfile.ColorTemperatureKelvin,
                Is.EqualTo(3500f).Within(0.01f));

            yield return new WaitForSecondsRealtime(
                lighting.ProfileCatalog.Calibration
                    .BusinessShutdownDelaySeconds + 0.25f);
            Assert.That(
                lighting.BusinessAdapter.IsBusinessOpenAndOwnerPresent(
                    "service.teimo.shop"),
                Is.False,
                    "After the shutdown grace, Teimo's empty shop ceiling must " +
                    "be dark while refrigeration remains powered.");
            for (int index = 0; index < fixtures.Length; index++)
            {
                if (fixtures[index].BusinessId == "service.teimo.shop" &&
                    fixtures[index].Profile.Category !=
                    LightFixtureCategory.RefrigeratedDisplay)
                {
                    Assert.That(fixtures[index].LogicalOn, Is.False);
                }
            }
            Assert.That(
                fixtures.Where(fixture => fixture.Profile.Category ==
                        LightFixtureCategory.RefrigeratedDisplay)
                    .All(fixture => fixture.LogicalOn &&
                        fixture.GetComponent<Light>().enabled),
                Is.True,
                "Teimo's refrigerator display lights must stay powered when " +
                "the shop ceiling lights follow him to the pub.");
            Assert.That(
                fixtures.Count(fixture =>
                    fixture.BusinessId == "service.teimo.pub" &&
                    fixture.GetComponent<Light>().shadows != LightShadows.None),
                Is.GreaterThan(0),
                "After the empty shop powers down, the occupied pub must " +
                "receive at least one bounded local shadow caster even when " +
                "its authored zone is unavailable.");

            world.SpawnedPlayer.transform.position =
                new Vector3(-1352f, 6.1f, 220f);
            yield return new WaitForSecondsRealtime(0.5f);
            string[] inspectionFixtureIds =
            {
                "world.light.29a30f4762c2e56b22c91263035533f4",
                "world.light.66481b9e0a97c74cd440dc9268ca28fe",
                "world.light.8b9d6187517e7976053089bbe5a0a94b",
                "world.light.f4fa0862592f9c9059b4e9aa2635ea35",
            };
            int inspectionFixtureCount = 0;
            int inspectionShadowCount = 0;
            for (int index = 0; index < fixtures.Length; index++)
            {
                GameLightFixture fixture = fixtures[index];
                if (!inspectionFixtureIds.Contains(fixture.FixtureId))
                {
                    continue;
                }

                inspectionFixtureCount++;
                Light inspectionLight = fixture.GetComponent<Light>();
                Assert.That(inspectionLight, Is.Not.Null);
                Assert.That(inspectionLight.intensity, Is.EqualTo(1_200f)
                    .Within(0.01f));
                Assert.That(inspectionLight.range, Is.EqualTo(7.5f)
                    .Within(0.01f));
                if (inspectionLight.shadows != LightShadows.None)
                {
                    inspectionShadowCount++;
                }
            }

            Assert.That(inspectionFixtureCount, Is.EqualTo(4));
            Assert.That(
                inspectionShadowCount,
                Is.EqualTo(4),
                "The four closest inspection-office lights must keep their " +
                "shadows even while the player stands just outside, otherwise " +
                "they leak through the temporary donor shell.");

            // Exercise an actually streamed, non-home cell. This catches the
            // callback-order failure where preloaded/home fixtures existed but
            // newly loaded cells never received their production adapter or
            // emissive presentation.
            Assert.That(
                runner.TrySetEnvironment(
                    13d,
                    "weather.clear",
                    out string fleetariEnvironmentFailure),
                Is.True,
                fleetariEnvironmentFailure);
            world.SpawnedPlayer.transform.position = FleetariInteriorPosition;
            for (int frame = 0; frame < 360; frame++)
            {
                fixtures = Object.FindObjectsByType<GameLightFixture>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                if (fixtures.Count(fixture =>
                        fixture.BusinessId ==
                        "service.fleetari.workshop") >= 2)
                {
                    break;
                }

                yield return null;
            }

            GameLightFixture[] fleetariFixtures = fixtures.Where(fixture =>
                    fixture.BusinessId == "service.fleetari.workshop")
                .ToArray();
            Assert.That(
                fleetariFixtures.Length,
                Is.GreaterThanOrEqualTo(2),
                "A normally streamed non-home cell must recreate its " +
                "fixture bindings.");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(
                fleetariFixtures.All(fixture => fixture.LogicalOn),
                Is.True,
                "During Fleetari working hours all workshop fixtures must be on.");
            Assert.That(
                fleetariFixtures.Any(fixture =>
                    fixture.GetComponent<Light>()?.enabled == true),
                Is.True,
                "A nearby streamed Fleetari fixture must emit real light.");
            Assert.That(
                fleetariFixtures.All(fixture =>
                    fixture.transform.Find("Emission Lens (Runtime)") != null),
                Is.True,
                "Streamed Fleetari area fixtures must recreate their visible lenses.");

            world.SpawnedPlayer.transform.SetPositionAndRotation(
                world.PlayerSpawnPosition,
                world.PlayerSpawnRotation);
            for (int frame = 0;
                 frame < 600 &&
                 (!world.WorldStreaming.IsCellLoaded("cell_0_-3") ||
                  world.WorldStreaming.IsStreaming);
                 frame++)
            {
                yield return null;
            }
            Assert.That(
                world.WorldStreaming.IsCellLoaded("cell_0_-3"),
                Is.True,
                "Returning home must reload the cell used by the existing " +
                "switch-state regression.");
            yield return new WaitForSecondsRealtime(0.5f);

            HomeLightSwitchPresentationAdapter switchAdapter =
                lighting.SwitchPresentationAdapter;
            Assert.That(switchAdapter, Is.Not.Null);
            Assert.That(switchAdapter.ExpectedTargetCount, Is.EqualTo(8));
            Assert.That(switchAdapter.BoundTargetCount, Is.EqualTo(8));

            Assert.That(
                switchAdapter.TryGetTargetByStableId(
                    "7cc6d4604bf93dfdeadbff777dc06861",
                    out LightSwitchInteractionTarget hallwayTarget),
                Is.True,
                "The hallway must bind to its real donor switch button, not " +
                "to the retired Bootstrap proxy.");
            Assert.That(hallwayTarget.HidesProxyRenderers, Is.False);
            Assert.That(hallwayTarget.gameObject.activeInHierarchy, Is.True);
            Assert.That(hallwayTarget.GetComponent<Collider>(), Is.Not.Null);
            BoxCollider hallwayInteractionBox =
                hallwayTarget.GetComponent<BoxCollider>();
            Assert.That(hallwayInteractionBox, Is.Not.Null);
            Assert.That(hallwayInteractionBox.size.x,
                Is.GreaterThanOrEqualTo(0.28f));
            Assert.That(hallwayInteractionBox.size.y,
                Is.GreaterThanOrEqualTo(0.34f));
            Assert.That(hallwayInteractionBox.size.z,
                Is.GreaterThanOrEqualTo(0.16f));
            InteractionTargetHost hallwayHost =
                hallwayTarget.GetComponent<InteractionTargetHost>();
            Assert.That(hallwayHost, Is.Not.Null);
            Assert.That(
                hallwayHost.TryGetCapability(
                    out LightSwitchInteractionTarget hostedHallwayTarget),
                Is.True);
            Assert.That(hostedHallwayTarget, Is.SameAs(hallwayTarget));
            Renderer[] hallwayRenderers =
                hallwayTarget.GetComponentsInChildren<Renderer>(true);
            Assert.That(hallwayRenderers.Length, Is.GreaterThan(0));
            Assert.That(hallwayRenderers.Any(renderer => renderer.enabled),
                Is.True);
            Assert.That(
                Mathf.DeltaAngle(hallwayTarget.CurrentVisualAngleDegrees, 12f),
                Is.EqualTo(0f).Within(0.1f));

            Assert.That(
                switchAdapter.TryGetTargetByStableId(
                    "0347be03a1a816e7cc67a1806622fdfe",
                    out LightSwitchInteractionTarget entryTarget),
                Is.True);
            Assert.That(entryTarget, Is.Not.SameAs(hallwayTarget));
            Assert.That(entryTarget.SwitchId,
                Is.EqualTo("switch.home.hallway"));
            Assert.That(
                Mathf.DeltaAngle(entryTarget.CurrentVisualAngleDegrees, 12f),
                Is.EqualTo(0f).Within(0.1f));

            hallwayTarget.Interact(default(InteractionContext));
            Assert.That(
                lighting.Grid.TryGetSwitchState(
                    "switch.home.hallway",
                    out bool hallwayIsOn),
                Is.True);
            Assert.That(hallwayIsOn, Is.True);
            Assert.That(
                Mathf.DeltaAngle(hallwayTarget.CurrentVisualAngleDegrees, -12f),
                Is.EqualTo(0f).Within(0.1f));
            Assert.That(
                Mathf.DeltaAngle(entryTarget.CurrentVisualAngleDegrees, -12f),
                Is.EqualTo(0f).Within(0.1f));

            fixtures = Object.FindObjectsByType<GameLightFixture>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            string[] sharedHallwayFixtureIds =
            {
                "world.light.44779c4c4e4cb4dabce3dbc5e862e68c",
                "world.light.babe31b77f1e3a06de0caa90083cec81",
                "world.light.daa0926d1766b35f989095845c1ccbab",
            };
            GameLightFixture[] sharedHallwayFixtures = fixtures.Where(
                    fixture => sharedHallwayFixtureIds.Contains(
                        fixture.FixtureId))
                .ToArray();
            Assert.That(sharedHallwayFixtures, Has.Length.EqualTo(3));
            Assert.That(sharedHallwayFixtures.All(fixture =>
                    fixture.SwitchId == "switch.home.hallway" &&
                    fixture.LogicalOn),
                Is.True,
                "The hallway rocker must power entrance, corridor and hall.");

            lighting.Grid.SetSwitchState("switch.home.hallway", false);
            Assert.That(
                Mathf.DeltaAngle(hallwayTarget.CurrentVisualAngleDegrees, 12f),
                Is.EqualTo(0f).Within(0.1f),
                "External state changes and save restore must move the switch " +
                "without requiring another player interaction.");
            Assert.That(
                Mathf.DeltaAngle(entryTarget.CurrentVisualAngleDegrees, 12f),
                Is.EqualTo(0f).Within(0.1f));

            Assert.That(
                switchAdapter.TryGetTarget(
                    "switch.home.toilet",
                    out LightSwitchInteractionTarget toiletTarget),
                Is.True,
                "The real WC rocker must be interactive.");
            toiletTarget.Interact(default(InteractionContext));
            Assert.That(
                lighting.Grid.TryGetSwitchState(
                    "switch.home.toilet",
                    out bool toiletIsOn) && toiletIsOn,
                Is.True);
            Assert.That(fixtures.Single(fixture =>
                    fixture.FixtureId ==
                    "world.light.4b838825763b9266233ff52963a2d792")
                .LogicalOn,
                Is.True);

            LightSwitchInteractionTarget[] switchTargets =
                Object.FindObjectsByType<LightSwitchInteractionTarget>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            int disabledProxyCount = 0;
            for (int index = 0; index < switchTargets.Length; index++)
            {
                if (!switchTargets[index].HidesProxyRenderers)
                {
                    continue;
                }

                disabledProxyCount++;
                Assert.That(
                    switchTargets[index].gameObject.activeSelf,
                    Is.False,
                    $"Retired switch proxy '{switchTargets[index].name}' must " +
                    "not compete with the real donor button for raycasts.");
            }
            Assert.That(disabledProxyCount, Is.EqualTo(7));

            fixtures = Object.FindObjectsByType<GameLightFixture>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            GameLightFixture kitchenFixture = null;
            for (int index = 0; index < fixtures.Length; index++)
            {
                if (fixtures[index].SwitchId == "switch.home.kitchen")
                {
                    kitchenFixture = fixtures[index];
                    break;
                }
            }

            Assert.That(kitchenFixture, Is.Not.Null);
            Assert.That(kitchenFixture.IntensityMultiplier, Is.GreaterThan(1f));
            Light kitchenLight = kitchenFixture.GetComponent<Light>();
            Assert.That(kitchenLight, Is.Not.Null);
            Assert.That(kitchenLight.enabled, Is.True);
            Assert.That(kitchenLight.intensity, Is.GreaterThanOrEqualTo(1_900f));
            Assert.That(
                kitchenLight.shadows,
                Is.EqualTo(LightShadows.Soft));

            GameLightFixture bedroomFixture = fixtures.Single(fixture =>
                fixture.SwitchId == "switch.home.bedroom-boy");
            Assert.That(
                bedroomFixture.transform.Find("Emission Lens (Runtime)"),
                Is.Null,
                "A domestic lampshade must use its donor renderer, not a " +
                "generated opaque sphere.");

            runner.SetSwitch("switch.home.garage", true);
            yield return new WaitForSecondsRealtime(0.25f);
            GameLightFixture garageFixture = fixtures.First(fixture =>
                fixture.CircuitId == "grid.home.garage" &&
                fixture.Profile.Category == LightFixtureCategory.Fluorescent);
            Light garageLight = garageFixture.GetComponent<Light>();
            Assert.That(garageFixture.LogicalOn, Is.True);
            Assert.That(garageLight.intensity, Is.GreaterThanOrEqualTo(16_000f),
                "The donor garage has no baked bounce, so its useful zone " +
                "lighting must remain readable under daytime fixed exposure.");

            GameLightFixture outdoorFixture = fixtures.Single(fixture =>
                fixture.FixtureId ==
                "world.light.d76c0bb63327c058f2b82bc57a699d02");
            Assert.That(outdoorFixture.Profile.Category,
                Is.EqualTo(LightFixtureCategory.HomeExterior));
            Assert.That(outdoorFixture.CircuitId,
                Is.EqualTo("grid.home.garage"));
            Assert.That(outdoorFixture.SwitchId,
                Is.EqualTo("switch.home.garage"));
            Assert.That(outdoorFixture.LogicalOn, Is.True,
                "The donor-evidenced outdoor fixture must follow the garage " +
                "switch and home electrical authority.");
            Assert.That(outdoorFixture.GetComponent<Light>().colorTemperature,
                Is.EqualTo(6500f).Within(0.01f));
            Assert.That(outdoorFixture.GetComponent<Light>().useColorTemperature,
                Is.False,
                "The white donor garage lamp must not receive an amber " +
                "temperature filter.");
            Transform outdoorLens = outdoorFixture.transform.Find(
                "Emission Lens (Runtime)");
            Assert.That(outdoorLens, Is.Not.Null);
            Assert.That(
                Vector3.Distance(
                    outdoorLens.position,
                    outdoorFixture.transform.position),
                Is.GreaterThan(0.05f),
                "The garage exterior emission must sit on the visible donor " +
                "diffuser surface rather than inside the opaque housing.");

            Assert.That(
                world.ItemWorldRuntime.TryGetInstance(
                    "8a60bfa0fb3651ae8dc0afae19b2fe0a",
                    out WorldItemInstance flashlight),
                Is.True,
                "The canonical garage flashlight must materialize after the " +
                "home cell reload.");
            Assert.That(lighting.FlashlightAdapter.BoundFlashlightCount,
                Is.EqualTo(1));
            if (!flashlight.State.isEnabled)
            {
                flashlight.Interact(default(InteractionContext));
            }
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(
                lighting.FlashlightAdapter.TryGetFixture(
                    "8a60bfa0fb3651ae8dc0afae19b2fe0a",
                    out GameLightFixture flashlightFixture),
                Is.True);
            Assert.That(flashlightFixture.LogicalOn, Is.True);
            Assert.That(flashlightFixture.GetComponentInChildren<Light>().enabled,
                Is.True,
                "The item on/off state must drive a real shadow-capable spot.");
            Light flashlightLight =
                flashlightFixture.GetComponentInChildren<Light>();
            Assert.That(flashlightLight.intensity,
                Is.EqualTo(300f).Within(0.01f));
            Assert.That(flashlightLight.range,
                Is.EqualTo(20f).Within(0.01f));
            Assert.That(
                Vector3.Angle(
                    flashlightLight.transform.forward,
                    -flashlight.transform.up),
                Is.LessThan(1f),
                "Donor FlashLight used local X +90: the beam must leave the " +
                "front glass along item -Y, not the battery-cover end.");

            int registeredBeforeReconcile =
                lighting.RuntimeManager.RegisteredFixtureCount;
            GameLightFixture reconcileFixture = fixtures.First(fixture =>
                fixture.BusinessId == "service.teimo.pub");
            MethodInfo unregisterFixture = typeof(LightingRuntimeManager)
                .GetMethod(
                    "UnregisterFixture",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(unregisterFixture, Is.Not.Null);
            unregisterFixture.Invoke(
                lighting.RuntimeManager,
                new object[] { reconcileFixture });
            Assert.That(lighting.RuntimeManager.RegisteredFixtureCount,
                Is.EqualTo(registeredBeforeReconcile - 1));
            yield return new WaitForSecondsRealtime(1.25f);
            Assert.That(lighting.RuntimeManager.RegisteredFixtureCount,
                Is.EqualTo(registeredBeforeReconcile),
                "Periodic world-light replay must heal a missed streamed " +
                "registration without reloading the whole game.");

            ElectricalGridStateDto captured = lighting.Grid.CaptureState();
            runner.SetSwitch("switch.home.kitchen", false);
            Assert.That(
                lighting.Grid.TryRestoreState(captured, out string restoreFailure),
                Is.True,
                restoreFailure);
            Assert.That(
                lighting.Grid.TryGetSwitchState(
                    "switch.home.kitchen",
                    out bool restoredSwitch),
                Is.True);
            Assert.That(restoredSwitch, Is.True);

            LightingValidationSnapshot snapshot = runner.Capture(
                "automated-bootstrap-night");
            Assert.That(snapshot.registeredFixtures, Is.GreaterThan(0));
            Assert.That(lighting.RuntimeManager.ActiveEveryFrameShadowLightCount,
                Is.LessThanOrEqualTo(4));
            Assert.That(lighting.RuntimeManager.ActiveHdBeamCount,
                Is.LessThanOrEqualTo(3));
            Assert.That(snapshot.exposureEv, Is.InRange(6f, 13f));
        }

        [UnityTest]
        public IEnumerator ExternalCell_RealUnloadReload_RestoresLightsAndEmission()
        {
            yield return DestroyPersistentRootIfPresent();
            AsyncOperation load = SceneManager.LoadSceneAsync(
                BootstrapScenePath,
                LoadSceneMode.Single);
            yield return load;

            ProductionWorldStreamingInstaller world =
                Object.FindFirstObjectByType<ProductionWorldStreamingInstaller>(
                    FindObjectsInactive.Include);
            ProductionLightingInstaller lighting =
                Object.FindFirstObjectByType<ProductionLightingInstaller>(
                    FindObjectsInactive.Include);
            Assert.That(world, Is.Not.Null);
            Assert.That(lighting, Is.Not.Null);

            string prepareFailure = string.Empty;
            for (int frame = 0;
                 frame < 900 && (!world.IsReady || !lighting.IsInitialized);
                 frame++)
            {
                if (!world.IsReady &&
                    !world.IsGameplayPreparationRunning)
                {
                    world.TryBeginGameplayPreparation(out prepareFailure);
                }

                yield return null;
            }

            Assert.That(world.IsReady, Is.True, prepareFailure);
            Assert.That(lighting.IsInitialized, Is.True);

            world.WorldStreaming.enabled = false;
            world.SpawnedPlayer.transform.position = FleetariInteriorPosition;
            world.WorldStreaming.enabled = true;
            yield return world.WorldStreaming.RefreshNow();
            yield return WaitForFleetariFixtures();
            AssertFleetariFixturesRestored();

            Scene fleetariScene = SceneManager.GetSceneByPath(
                FleetariCellScenePath);
            Assert.That(fleetariScene.IsValid() && fleetariScene.isLoaded,
                Is.True);
            world.WorldStreaming.enabled = false;
            AsyncOperation unloadFleetari = SceneManager.UnloadSceneAsync(
                fleetariScene);
            Assert.That(unloadFleetari, Is.Not.Null);
            yield return unloadFleetari;
            Assert.That(
                world.WorldStreaming.IsCellLoaded("cell_3_-1"),
                Is.False,
                "Fleetari's owned cell must actually unload for this regression.");

            world.WorldStreaming.enabled = true;
            yield return world.WorldStreaming.RefreshNow();
            yield return WaitForFleetariFixtures();
            Assert.That(
                world.WorldStreaming.IsCellLoaded("cell_3_-1"),
                Is.True,
                "Returning to Fleetari must reload the external cell.");
            AssertFleetariFixturesRestored();
        }

        private static IEnumerator WaitForFleetariFixtures()
        {
            for (int frame = 0; frame < 360; frame++)
            {
                GameLightFixture[] fixtures = Object.FindObjectsByType<
                    GameLightFixture>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                GameLightFixture[] fleetariFixtures = fixtures.Where(fixture =>
                        fixture.BusinessId == "service.fleetari.workshop")
                    .ToArray();
                if (fleetariFixtures.Length == 2 &&
                    fleetariFixtures.All(fixture =>
                        fixture.GetComponent<Light>() != null &&
                        fixture.transform.Find(
                            "Emission Lens (Runtime)") != null))
                {
                    yield break;
                }

                yield return null;
            }
        }

        private static void AssertFleetariFixturesRestored()
        {
            GameLightFixture[] fleetariFixtures = Object
                .FindObjectsByType<GameLightFixture>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Where(fixture =>
                    fixture.BusinessId == "service.fleetari.workshop")
                .ToArray();
            Assert.That(
                fleetariFixtures.Length,
                Is.EqualTo(2),
                "An external cell must own both workshop fixtures exactly once.");
            Assert.That(
                fleetariFixtures.Select(fixture => fixture.FixtureId)
                    .Distinct()
                    .Count(),
                Is.EqualTo(2),
                "An external-cell reload must not duplicate stable fixture IDs.");
            Assert.That(
                fleetariFixtures.All(fixture =>
                    fixture.GetComponent<Light>() != null &&
                    fixture.transform.Find("Emission Lens (Runtime)") != null),
                Is.True,
                "Real light components and emission must both survive an external-cell reload.");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            yield return DestroyPersistentRootIfPresent();
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
    }
}
