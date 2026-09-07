using System.Reflection;
using System.Linq;
using MSC.LegacyImport;
using MSC.World.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class WorldLightingProbeCatalogTests
    {
        private const string CatalogPath =
            "Assets/Game/World/Content/Lighting/" +
            "Phase1WorldLightingProbeCatalog.asset";
        private const string TeimoLongStreetLightId =
            "world.light.8946ab55e770546f2c76e72945c54f24";
        private const string TeimoShortStreetLightId =
            "world.light.5321706220112780baeed0bbe56912b3";
        private const string CapturedHomeLightPrefix = "world.light.";
        private static readonly string[] InspectionOfficeLightIds =
        {
            "world.light.29a30f4762c2e56b22c91263035533f4",
            "world.light.66481b9e0a97c74cd440dc9268ca28fe",
            "world.light.8b9d6187517e7976053089bbe5a0a94b",
            "world.light.f4fa0862592f9c9059b4e9aa2635ea35",
        };

        private static readonly HomeLightExpectation[] HomeLightExpectations =
        {
            new HomeLightExpectation(
                "770b22bf8eb3d0bfeacfede083978dca",
                new Vector3(157.86172f, 3.574f, -1030.6177f),
                new Vector3(90f, 0f, 0f),
                1_500f, 5.5f, 78f, 118f, 0.35f),
            new HomeLightExpectation(
                "6ec5b3ebe08e5c321e68a7d649fa2d83",
                new Vector3(159.37042f, 3.1599321f, -1027.905f),
                new Vector3(90f, 0f, 0f),
                900f, 4.2f, 82f, 122f, 0f),
            new HomeLightExpectation(
                "44779c4c4e4cb4dabce3dbc5e862e68c",
                new Vector3(160.02295f, 3.4368477f, -1030.6078f),
                new Vector3(90f, 0f, 0f),
                1_500f, 5.5f, 78f, 118f, 0.35f),
            new HomeLightExpectation(
                "4b838825763b9266233ff52963a2d792",
                new Vector3(162.10258f, 3.2381797f, -1028.9606f),
                new Vector3(90f, 0f, 0f),
                700f, 3f, 75f, 115f, 0.2f),
            new HomeLightExpectation(
                "450510e720a076d53aeb44a05dd6480d",
                new Vector3(166.31563f, 3.4368472f, -1027.8579f),
                new Vector3(90f, 0f, 0f),
                900f, 4.2f, 82f, 122f, 0.2f),
            new HomeLightExpectation(
                "78aeb3c71258a73257d315d8e8eeb0ed",
                new Vector3(159.94843f, 2.8189328f, -1034.593f),
                new Vector3(90f, 0f, 0f),
                1_300f, 5f, 80f, 120f, 0f),
            new HomeLightExpectation(
                "babe31b77f1e3a06de0caa90083cec81",
                new Vector3(157.35641f, 3.3429327f, -1036.827f),
                new Vector3(90f, 0f, 0f),
                900f, 4f, 80f, 120f, 0.2f),
            new HomeLightExpectation(
                "d47572b57298d90c2aa5f707fc5519ff",
                new Vector3(157.35641f, 3.2989328f, -1039.4491f),
                new Vector3(90f, 0f, 0f),
                800f, 3.6f, 78f, 118f, 0.15f),
            new HomeLightExpectation(
                "6a07d52868491a36248ac5e51ecd81d2",
                new Vector3(156.92699f, 1.61f, -1043.254f),
                Vector3.zero,
                650f, 3f, 72f, 112f, 0.1f),
            new HomeLightExpectation(
                "daa0926d1766b35f989095845c1ccbab",
                new Vector3(165.40744f, 2.6349335f, -1032.9551f),
                Vector3.zero,
                1_500f, 5.5f, 78f, 118f, 0.35f),
            new HomeLightExpectation(
                "cbf1b6cb6074e84c3c2cc8042b8d3e44",
                new Vector3(153.37552f, 3.354f, -1040.9165f),
                new Vector3(90f, 0f, 0f),
                1_800f, 4.2f, 78f, 112f, 0f),
            new HomeLightExpectation(
                "cda8c8cbd2913a9f16e802515f9b230d",
                new Vector3(153.37552f, 3.354f, -1038.4955f),
                new Vector3(90f, 0f, 0f),
                1_800f, 4.2f, 78f, 112f, 0f),
            new HomeLightExpectation(
                "4f6ac2527ef70e970cb89ef70baaefa9",
                new Vector3(153.37553f, 3.354f, -1036.3374f),
                new Vector3(90f, 0f, 0f),
                1_800f, 4.2f, 78f, 112f, 0f),
            new HomeLightExpectation(
                "d76c0bb63327c058f2b82bc57a699d02",
                new Vector3(153.49457f, 3.234f, -1032.9905f),
                new Vector3(45f, 0f, 0f),
                1_100f, 8f, 65f, 95f, 0f),
        };

        [Test]
        public void GeneratedCatalog_IsValidAndUsesStableUniqueIds()
        {
            WorldLightingProbeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    WorldLightingProbeCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(catalog.Validate);
            // Accepted expanded baseline: 46 donor-evidenced sources plus
            // three refrigerator fills, with ten bounded reflection probes.
            Assert.That(catalog.Lights.Count, Is.EqualTo(49));
            Assert.That(catalog.Probes.Count, Is.EqualTo(10));
            Assert.That(
                catalog.Lights.Select(light => light.LightId)
                    .Concat(catalog.Probes.Select(probe => probe.ProbeId))
                    .Distinct()
                    .Count(),
                Is.EqualTo(catalog.Lights.Count + catalog.Probes.Count));
        }

        [Test]
        public void StreetLights_AreNightOnly()
        {
            WorldLightingProbeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    WorldLightingProbeCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.Lights.Count(light =>
                    light.IntensityIsLux &&
                    light.ActivationPolicy ==
                        WorldLightActivationPolicy.NightOnly),
                Is.EqualTo(14));
            Assert.That(
                catalog.Lights
                    .Where(light => light.IntensityIsLux &&
                        light.ActivationPolicy ==
                            WorldLightActivationPolicy.NightOnly)
                    .All(light =>
                        light.CastsShadows &&
                        light.IntensityIsLux &&
                        Mathf.Approximately(
                            light.Intensity,
                            400f) &&
                        Mathf.Approximately(
                            light.LuxAtDistance,
                            15.6f) &&
                        Mathf.Approximately(
                            light.Range,
                            35f) &&
                        Mathf.Approximately(
                            light.ColorTemperatureKelvin,
                            6_570f) &&
                        Mathf.Approximately(
                            light.IndirectMultiplier,
                            0f) &&
                        Mathf.Approximately(
                            light.ShapeRadius,
                            0.025f) &&
                        light.VolumetricEnabled &&
                        Mathf.Approximately(
                            light.VolumetricDimmer,
                            1f) &&
                        Mathf.Approximately(
                            light.SpotInnerAngle,
                            107f) &&
                        Mathf.Approximately(
                            light.SpotOuterAngle,
                            138f) &&
                        light.SourceKind ==
                            WorldLightSourceKind.Spot &&
                        light.BakeMode ==
                            WorldLightBakeMode.Realtime),
                Is.True);
            WorldLightDefinition homeExterior = catalog.Lights.Single(light =>
                light.LightId ==
                CapturedHomeLightPrefix +
                "d76c0bb63327c058f2b82bc57a699d02");
            Assert.That(
                homeExterior.ActivationPolicy,
                Is.EqualTo(WorldLightActivationPolicy.NightOnly));
            Assert.That(homeExterior.IntensityIsLux, Is.False);
            Assert.That(
                catalog.Lights.All(light => light.CastsShadows),
                Is.True);
        }

        [Test]
        public void HomeLights_PreserveFixturePosesAndRoomBoundedOverrides()
        {
            WorldLightingProbeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    WorldLightingProbeCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(HomeLightExpectations, Has.Length.EqualTo(14));
            foreach (HomeLightExpectation expected in HomeLightExpectations)
            {
                WorldLightDefinition light = catalog.Lights.Single(
                    candidate =>
                        candidate.LightId ==
                        CapturedHomeLightPrefix + expected.StableId);
                Assert.That(light.CellId, Is.EqualTo("cell_0_-3"));
                AssertVector3(light.Position, expected.Position);
                AssertVector3(
                    light.RotationEulerAngles,
                    expected.RotationEulerAngles);
                Assert.That(
                    light.Intensity,
                    Is.EqualTo(expected.IntensityLumens).Within(0.01f));
                Assert.That(
                    light.Range,
                    Is.EqualTo(expected.Range).Within(0.0001f));
                Assert.That(
                    light.SpotInnerAngle,
                    Is.EqualTo(expected.InnerSpotAngle).Within(0.01f));
                Assert.That(
                    light.SpotOuterAngle,
                    Is.EqualTo(expected.OuterSpotAngle).Within(0.01f));
                Assert.That(
                    light.IndirectMultiplier,
                    Is.EqualTo(expected.IndirectMultiplier).Within(0.01f));
                Assert.That(
                    light.SourceKind,
                    Is.EqualTo(WorldLightSourceKind.Spot));
                Assert.That(
                    light.BakeMode,
                    Is.EqualTo(WorldLightBakeMode.Realtime));
                Assert.That(light.IntensityIsLux, Is.False);
                Assert.That(light.Intensity, Is.LessThanOrEqualTo(1_800f));
                Assert.That(light.Range, Is.LessThanOrEqualTo(8f));
            }

            Assert.That(
                catalog.Lights.Count(light =>
                    light.SourceKind == WorldLightSourceKind.Spot &&
                    light.BakeMode == WorldLightBakeMode.Realtime &&
                    HomeLightExpectations.Any(expected =>
                        light.LightId ==
                        CapturedHomeLightPrefix + expected.StableId)),
                Is.EqualTo(HomeLightExpectations.Length));
        }

        [Test]
        public void InspectionOfficeLights_UseLeakBoundedPhotometry()
        {
            WorldLightingProbeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WorldLightingProbeCatalog>(
                    CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            WorldLightDefinition[] inspectionLights = catalog.Lights
                .Where(light => InspectionOfficeLightIds.Contains(
                    light.LightId))
                .ToArray();
            Assert.That(inspectionLights, Has.Length.EqualTo(4));
            Assert.That(
                inspectionLights.All(light =>
                    Mathf.Approximately(light.Intensity, 1_200f) &&
                    Mathf.Approximately(light.Range, 6.5f) &&
                    Mathf.Approximately(light.IndirectMultiplier, 0.1f) &&
                    light.CastsShadows),
                Is.True);
        }

        [Test]
        public void TeimoStreetLights_PreserveMeasuredLampHeadOverrides()
        {
            WorldLightingProbeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    WorldLightingProbeCatalog>(CatalogPath);

            WorldLightDefinition longLight = catalog.Lights.Single(
                light => light.LightId == TeimoLongStreetLightId);
            WorldLightDefinition shortLight = catalog.Lights.Single(
                light => light.LightId == TeimoShortStreetLightId);

            Assert.That(
                longLight.Position,
                Is.EqualTo(
                    new Vector3(-1355.972f, 15.098f, 136.196f)));
            Assert.That(
                longLight.RotationEulerAngles,
                Is.EqualTo(new Vector3(75f, 0f, 37.948f)));
            Assert.That(longLight.Range, Is.EqualTo(35f));

            Assert.That(
                shortLight.Position,
                Is.EqualTo(
                    new Vector3(-1379.331f, 13.527f, 119.488f)));
            Assert.That(
                shortLight.RotationEulerAngles,
                Is.EqualTo(new Vector3(75f, 0f, 24.229f)));
            Assert.That(shortLight.Range, Is.EqualTo(35f));
        }

        [Test]
        public void TeimoRefrigerators_PreserveCapturedAreaLightOverrides()
        {
            WorldLightingProbeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    WorldLightingProbeCatalog>(CatalogPath);
            WorldLightDefinition[] refrigeratorLights = catalog.Lights
                .Where(light => light.LightId.StartsWith(
                    "world.light.teimo.shop.refrigerator.",
                    System.StringComparison.Ordinal))
                .OrderBy(light => light.LightId)
                .ToArray();

            Assert.That(refrigeratorLights, Has.Length.EqualTo(3));
            Assert.That(
                refrigeratorLights.Select(light => light.Position).ToArray(),
                Is.EqualTo(new[]
                {
                    new Vector3(-1374.99f, 7.073f, 141.494f),
                    new Vector3(-1375.44f, 7.078f, 142.2f),
                    new Vector3(-1379.74f, 6.527f, 142.99f),
                }));
            Assert.That(
                refrigeratorLights.Select(light =>
                    light.RotationEulerAngles).ToArray(),
                Is.EqualTo(new[]
                {
                    new Vector3(90f, 147.397f, 90.001f),
                    new Vector3(-270f, 0f, -57.444f),
                    new Vector3(-270f, -122.602f, 0f),
                }));
            Assert.That(
                refrigeratorLights.Select(light => light.AreaSize).ToArray(),
                Is.EqualTo(new[]
                {
                    new Vector2(0.54187f, 0.4683933f),
                    new Vector2(0.8432465f, 0.5421705f),
                    new Vector2(0.1790103f, 1.450276f),
                }));
            Assert.That(refrigeratorLights.All(light =>
                light.CellId == "cell_-3_0" &&
                Mathf.Approximately(light.Intensity, 15_000f) &&
                Mathf.Approximately(light.Range, 3.5f) &&
                Mathf.Approximately(
                    light.ColorTemperatureKelvin,
                    15_000f) &&
                light.SuppressGeneratedEmission &&
                !light.VolumetricEnabled), Is.True);
        }

        [Test]
        public void ReflectionProbes_UseRestrainedBaselineMultiplier()
        {
            WorldLightingProbeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    WorldLightingProbeCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                catalog.Probes.All(probe => probe.Intensity <= 0.35f),
                Is.True);
        }

        [Test]
        public void HomeReflectionProbes_AreCapturedInsideBoundedRooms()
        {
            WorldLightingProbeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    WorldLightingProbeCatalog>(CatalogPath);

            Assert.That(catalog, Is.Not.Null);
            WorldReflectionProbeDefinition[] home = catalog.Probes
                .Where(probe => probe.ProbeId.StartsWith(
                    "world.probe.cell-0--3.home.",
                    System.StringComparison.Ordinal))
                .OrderBy(probe => probe.ProbeId)
                .ToArray();

            Assert.That(home, Has.Length.EqualTo(3));
            Assert.That(
                catalog.Probes.Any(probe =>
                    probe.ProbeId == "world.probe.cell-0--3.yard"),
                Is.False,
                "The former YARD-wide probe captured below the floor and " +
                "projected the garage/house cubemap onto exterior surfaces.");
            Assert.That(home.All(probe =>
                probe.CellId == "cell_0_-3" &&
                probe.Position.y >= 2.2f &&
                probe.Size.x <= 10.8f &&
                probe.Size.y <= 3f &&
                probe.Size.z <= 9f &&
                Mathf.Approximately(probe.Intensity, 0.12f)), Is.True);
        }

        [Test]
        public void LegacyDisplayName_IsReadableButPreservesSourceIdentity()
        {
            string displayName = DonorWorldBaselineDisplayName.Create(
                "STORE/LOD/ShopFunctions/office_lamp",
                12345,
                "BuildingExterior");

            Assert.That(
                displayName,
                Is.EqualTo(
                    "BuildingExterior · office lamp · STORE / " +
                    "ShopFunctions [12345]"));
            Assert.That(displayName, Does.Not.StartWith("LegacyWorld_"));
        }

        [Test]
        public void Runtime_CreatesStreetSpotAndBlendedReflectionProbe()
        {
            WorldLightingProbeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    WorldLightingProbeCatalog>(CatalogPath);
            WorldLightDefinition streetLight = catalog.Lights.First(light =>
                light.IntensityIsLux &&
                light.ActivationPolicy ==
                WorldLightActivationPolicy.NightOnly);
            var runtimeObject = new GameObject("Lighting Runtime Test");
            var parentObject = new GameObject("Cell Root");
            try
            {
                WorldLightingProbeRuntime runtime =
                    runtimeObject.AddComponent<WorldLightingProbeRuntime>();
                MethodInfo createLight = typeof(WorldLightingProbeRuntime)
                    .GetMethod(
                        "CreateLight",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo createProbe = typeof(WorldLightingProbeRuntime)
                    .GetMethod(
                        "CreateProbe",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(createLight, Is.Not.Null);
                Assert.That(createProbe, Is.Not.Null);
                createLight.Invoke(
                    runtime,
                    new object[] { parentObject.transform, streetLight });
                createProbe.Invoke(
                    runtime,
                    new object[] { parentObject.transform, catalog.Probes[0] });

                Light light = parentObject.GetComponentInChildren<Light>();
                HDAdditionalLightData hdLight =
                    parentObject.GetComponentInChildren<
                        HDAdditionalLightData>();
                ReflectionProbe probe =
                    parentObject.GetComponentInChildren<ReflectionProbe>();
                HDAdditionalReflectionData hdProbe =
                    parentObject.GetComponentInChildren<
                        HDAdditionalReflectionData>();
                Assert.That(light, Is.Not.Null);
                Assert.That(light.type, Is.EqualTo(LightType.Spot));
                Assert.That(light.lightUnit, Is.EqualTo(LightUnit.Lux));
                Assert.That(light.spotAngle, Is.EqualTo(138f).Within(0.01f));
                Assert.That(
                    light.innerSpotAngle,
                    Is.EqualTo(107f).Within(0.01f));
                Assert.That(
                    light.luxAtDistance,
                    Is.EqualTo(15.6f).Within(0.01f));
                Assert.That(
                    light.colorTemperature,
                    Is.EqualTo(6_570f).Within(0.01f));
                Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft));
                Assert.That(hdLight, Is.Not.Null);
                Assert.That(
                    light.shapeRadius,
                    Is.EqualTo(0.025f).Within(0.001f));
                Assert.That(hdLight.affectsVolumetric, Is.True);
                Assert.That(
                    hdLight.volumetricDimmer,
                    Is.EqualTo(1f).Within(0.001f));
                Assert.That(
                    hdLight.shadowUpdateMode,
                    Is.EqualTo(ShadowUpdateMode.EveryFrame));
                Assert.That(hdLight.alwaysDrawDynamicShadows, Is.False);
                Assert.That(probe, Is.Not.Null);
                Assert.That(hdProbe, Is.Not.Null);
                Assert.That(probe.blendDistance, Is.GreaterThan(0f));
                Assert.That(probe.blendDistance, Is.LessThanOrEqualTo(0.75f));
                Assert.That(
                    hdProbe.settingsRaw.proxySettings
                        .useInfluenceVolumeAsProxyVolume,
                    Is.True);
                Assert.That(
                    hdProbe.settingsRaw.cameraSettings
                        .customRenderingSettings,
                    Is.True);
                Assert.That(
                    hdProbe.frameSettings.IsEnabled(
                        FrameSettingsField.AtmosphericScattering),
                    Is.False);
                Assert.That(
                    hdProbe.frameSettings.IsEnabled(
                        FrameSettingsField.Volumetrics),
                    Is.False);
            }
            finally
            {
                Object.DestroyImmediate(parentObject);
                Object.DestroyImmediate(runtimeObject);
            }
        }

        [Test]
        public void Runtime_CreatesCapturedHomeLightAsRealtimeDynamicSpot()
        {
            WorldLightingProbeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    WorldLightingProbeCatalog>(CatalogPath);
            WorldLightDefinition definition = catalog.Lights.Single(light =>
                light.LightId ==
                CapturedHomeLightPrefix +
                "770b22bf8eb3d0bfeacfede083978dca");
            var runtimeObject = new GameObject("Lighting Runtime Test");
            var parentObject = new GameObject("Cell Root");
            try
            {
                WorldLightingProbeRuntime runtime =
                    runtimeObject.AddComponent<WorldLightingProbeRuntime>();
                MethodInfo createLight = typeof(WorldLightingProbeRuntime)
                    .GetMethod(
                        "CreateLight",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(createLight, Is.Not.Null);
                createLight.Invoke(
                    runtime,
                    new object[] { parentObject.transform, definition });

                Light light = parentObject.GetComponentInChildren<Light>();
                Assert.That(light, Is.Not.Null);
                Assert.That(light.type, Is.EqualTo(LightType.Spot));
                Assert.That(
                    light.lightmapBakeType,
                    Is.EqualTo(LightmapBakeType.Realtime));
                Assert.That(light.lightUnit, Is.EqualTo(LightUnit.Lumen));
                Assert.That(
                    light.intensity,
                    Is.EqualTo(1_500f).Within(0.01f));
                Assert.That(light.range, Is.EqualTo(5.5f).Within(0.01f));
                Assert.That(
                    light.innerSpotAngle,
                    Is.EqualTo(78f).Within(0.01f));
                Assert.That(
                    light.spotAngle,
                    Is.EqualTo(118f).Within(0.01f));
                HDAdditionalLightData hdLight =
                    parentObject.GetComponentInChildren<
                        HDAdditionalLightData>();
                Assert.That(hdLight, Is.Not.Null);
                Assert.That(
                    hdLight.shadowUpdateMode,
                    Is.EqualTo(ShadowUpdateMode.EveryFrame));
                Assert.That(hdLight.alwaysDrawDynamicShadows, Is.False);
                Assert.That(
                    hdLight.normalBias,
                    Is.EqualTo(0.15f).Within(0.001f));
                Assert.That(
                    hdLight.slopeBias,
                    Is.EqualTo(0.25f).Within(0.001f));
                Assert.That(
                    hdLight.shadowNearPlane,
                    Is.EqualTo(0.05f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(parentObject);
                Object.DestroyImmediate(runtimeObject);
            }
        }

        [Test]
        public void Runtime_PreservesPoseForPointPromotedToAreaLight()
        {
            WorldLightingProbeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    WorldLightingProbeCatalog>(CatalogPath);
            WorldLightDefinition definition = catalog.Lights.Single(light =>
                light.LightId == "world.light.teimo.shop.refrigerator.01");
            var runtimeObject = new GameObject("Lighting Runtime Test");
            var parentObject = new GameObject("Cell Root");
            try
            {
                WorldLightingProbeRuntime runtime =
                    runtimeObject.AddComponent<WorldLightingProbeRuntime>();
                MethodInfo createLight = typeof(WorldLightingProbeRuntime)
                    .GetMethod(
                        "CreateLight",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(createLight, Is.Not.Null);
                createLight.Invoke(
                    runtime,
                    new object[] { parentObject.transform, definition });

                Light light = parentObject.GetComponentInChildren<Light>();
                Assert.That(light, Is.Not.Null);
                // Match the captured rectangle pose locked independently by
                // TeimoRefrigerators_PreserveCapturedAreaLightOverrides above,
                // not the superseded point-light yaw (0, -117.5, 0).
                Assert.That(
                    Quaternion.Angle(
                        light.transform.rotation,
                        Quaternion.Euler(90f, 147.397f, 90.001f)),
                    Is.LessThan(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(parentObject);
                Object.DestroyImmediate(runtimeObject);
            }
        }

        [Test]
        public void Runtime_BudgetsRealtimeDynamicShadowsAroundFocus()
        {
            WorldLightingProbeCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    WorldLightingProbeCatalog>(CatalogPath);
            WorldLightDefinition[] definitions = catalog.Lights
                .Where(light => HomeLightExpectations.Any(expected =>
                    light.LightId ==
                    CapturedHomeLightPrefix + expected.StableId))
                .Take(5)
                .ToArray();
            var runtimeObject = new GameObject("Lighting Runtime Test");
            var parentObject = new GameObject("Cell Root");
            var focusObject = new GameObject("Player Shadow Focus");
            try
            {
                WorldLightingProbeRuntime runtime =
                    runtimeObject.AddComponent<WorldLightingProbeRuntime>();
                MethodInfo createLight = typeof(WorldLightingProbeRuntime)
                    .GetMethod(
                        "CreateLight",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo refreshBudget = typeof(WorldLightingProbeRuntime)
                    .GetMethod(
                        "RefreshDynamicShadowBudget",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                FieldInfo shadowFocus = typeof(WorldLightingProbeRuntime)
                    .GetField(
                        "shadowFocus",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(createLight, Is.Not.Null);
                Assert.That(refreshBudget, Is.Not.Null);
                Assert.That(shadowFocus, Is.Not.Null);
                for (int index = 0; index < definitions.Length; index++)
                {
                    createLight.Invoke(
                        runtime,
                        new object[]
                        {
                            parentObject.transform,
                            definitions[index],
                        });
                }

                focusObject.transform.position = definitions[0].Position;
                shadowFocus.SetValue(runtime, focusObject.transform);

                Light[] createdLegacyLights =
                    parentObject.GetComponentsInChildren<Light>();
                Assert.That(createdLegacyLights, Has.Length.EqualTo(5));
                Assert.That(
                    createdLegacyLights.Count(light =>
                        light.enabled &&
                        light.shadows == LightShadows.Soft),
                    Is.EqualTo(5));
                Assert.That(focusObject.activeInHierarchy, Is.True);
                Assert.That(
                    shadowFocus.GetValue(runtime),
                    Is.SameAs(focusObject.transform));
                refreshBudget.Invoke(runtime, null);

                HDAdditionalLightData[] createdLights =
                    parentObject.GetComponentsInChildren<
                        HDAdditionalLightData>();
                Assert.That(createdLights, Has.Length.EqualTo(5));
                Assert.That(
                    createdLights.Count(light =>
                        light.alwaysDrawDynamicShadows),
                    Is.Zero);
                Assert.That(
                    runtime.ActiveDynamicShadowLightCount,
                    Is.EqualTo(4));
                Assert.That(
                    createdLegacyLights.Count(light =>
                        light.enabled &&
                        light.shadows == LightShadows.Soft),
                    Is.EqualTo(4));
                Assert.That(
                    createdLights.All(light =>
                        light.shadowUpdateMode ==
                        ShadowUpdateMode.EveryFrame),
                    Is.True);
            }
            finally
            {
                Object.DestroyImmediate(focusObject);
                Object.DestroyImmediate(parentObject);
                Object.DestroyImmediate(runtimeObject);
            }
        }

        private static void AssertVector3(Vector3 actual, Vector3 expected)
        {
            Assert.That(
                Vector3.Distance(actual, expected),
                Is.LessThan(0.0001f),
                $"Expected {expected}, but was {actual}.");
        }

        private readonly struct HomeLightExpectation
        {
            public HomeLightExpectation(
                string stableId,
                Vector3 position,
                Vector3 rotationEulerAngles,
                float intensityLumens,
                float range,
                float innerSpotAngle,
                float outerSpotAngle,
                float indirectMultiplier)
            {
                StableId = stableId;
                Position = position;
                RotationEulerAngles = rotationEulerAngles;
                IntensityLumens = intensityLumens;
                Range = range;
                InnerSpotAngle = innerSpotAngle;
                OuterSpotAngle = outerSpotAngle;
                IndirectMultiplier = indirectMultiplier;
            }

            public string StableId { get; }
            public Vector3 Position { get; }
            public Vector3 RotationEulerAngles { get; }
            public float IntensityLumens { get; }
            public float Range { get; }
            public float InnerSpotAngle { get; }
            public float OuterSpotAngle { get; }
            public float IndirectMultiplier { get; }
        }
    }
}
