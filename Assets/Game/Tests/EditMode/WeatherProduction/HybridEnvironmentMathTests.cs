using System.Collections.Generic;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Production;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WeatherProduction
{
    public sealed class HybridEnvironmentMathTests
    {
        [TestCase(0f, 0f)]
        [TestCase(45f, 0.5f)]
        [TestCase(90f, 1f)]
        [TestCase(135f, 1f)]
        [TestCase(315f, 0f)]
        public void PortalOpenness_UsesConfiguredHingeArc(
            float currentAngle,
            float expected)
        {
            float actual = WeatherExposureMath.CalculatePortalOpenness(
                currentAngle,
                0f,
                90f);

            Assert.That(actual, Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void PortalInfluences_CombineWithoutExceedingOne()
        {
            float combined = WeatherExposureMath.CombinePortalInfluence(0.5f, 0.5f);

            Assert.That(combined, Is.EqualTo(0.75f).Within(0.0001f));
        }

        [Test]
        public void OpenPortal_ChangesChannelsIndependently()
        {
            var closed = new WeatherExposureState(
                1f,
                1f,
                0f,
                0.05f,
                0.1f,
                0.15f,
                0.65f,
                0f,
                1f);

            WeatherExposureState opened = WeatherExposureMath.ApplyPortal(closed, 0.5f);

            Assert.That(opened.EnclosureFactor, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(opened.ShelterFactor, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(opened.PrecipitationExposure, Is.EqualTo(0.425f).Within(0.0001f));
            Assert.That(opened.FogExposure, Is.EqualTo(0.525f).Within(0.0001f));
            Assert.That(opened.ThunderExposure, Is.EqualTo(0.7375f).Within(0.0001f));
        }

        [Test]
        public void ExponentialSmoothing_IsFrameRateIndependentForEqualElapsedTime()
        {
            float oneStep = WeatherExposureMath.ExponentialBlendFactor(1f, 0.5f);
            float halfStep = WeatherExposureMath.ExponentialBlendFactor(0.5f, 0.5f);
            float twoSteps = 1f - ((1f - halfStep) * (1f - halfStep));

            Assert.That(twoSteps, Is.EqualTo(oneStep).Within(0.0001f));
        }

        [Test]
        public void EnviroRainEmission_UsesExistingGlobalRateAndLocalExposure()
        {
            Assert.That(
                Enviro3WeatherZoneRemovalBridge.CalculateLocalEmission(400f, 1f),
                Is.EqualTo(400f));
            Assert.That(
                Enviro3WeatherZoneRemovalBridge.CalculateLocalEmission(400f, 0.15f),
                Is.EqualTo(60f).Within(0.0001f));
            Assert.That(
                Enviro3WeatherZoneRemovalBridge.CalculateLocalEmission(400f, 0f),
                Is.Zero);
        }

        [Test]
        public void EnviroRemovalLayout_InfersDonorLocalZAsWorldVertical()
        {
            var rotation = new Quaternion(
                -0.70710665f,
                0.0000000115f,
                -0.0000000049f,
                0.7071069f);
            Enviro3WeatherRemovalZoneLayout[] layout =
                Enviro3WeatherZoneRemovalBridge.CalculateZoneLayout(
                    new Vector3(10f, 3f, -20f),
                    rotation,
                    new Vector3(8f, 6f, 3f));

            Assert.That(layout, Has.Length.EqualTo(2));
            for (int index = 0; index < layout.Length; index++)
            {
                Assert.That(
                    Vector3.Dot(layout[index].Up, Vector3.up),
                    Is.GreaterThan(0.9999f));
                Assert.That(layout[index].Radius, Is.EqualTo(3f).Within(0.0001f));
                Assert.That(layout[index].Stretch, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(layout[index].Center.y, Is.EqualTo(3f).Within(0.0001f));
            }
        }

        [Test]
        public void WeatherZone_ContainsRotatedDonorBoxWithoutAxisAssumptions()
        {
            GameObject owner = new GameObject("RotatedDonorWeatherZone_Test");
            WeatherZoneProfile profile =
                ScriptableObject.CreateInstance<WeatherZoneProfile>();
            try
            {
                owner.transform.SetPositionAndRotation(
                    new Vector3(10f, 3f, -20f),
                    new Quaternion(
                        -0.70710665f,
                        0.0000000115f,
                        -0.0000000049f,
                        0.7071069f));
                BoxCollider collider = owner.AddComponent<BoxCollider>();
                collider.size = new Vector3(8f, 6f, 3f);
                WeatherZone zone = owner.AddComponent<WeatherZone>();
                zone.ConfigureForAuthoring(
                    "weather.zone.rotated-donor-test",
                    profile,
                    100,
                    collider);

                Assert.That(
                    zone.Contains(owner.transform.TransformPoint(
                        new Vector3(3.9f, 2.9f, 1.4f))),
                    Is.True);
                Assert.That(
                    zone.Contains(owner.transform.TransformPoint(
                        new Vector3(0f, 0f, 1.6f))),
                    Is.False,
                    "Local Z is the vertical dimension for this donor rotation.");
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(profile);
            }
        }

        [TestCase(0.19f, 0.2f, true)]
        [TestCase(0.2f, 0.2f, false)]
        [TestCase(0.21f, 0.2f, false)]
        public void ZoneExitHysteresis_HasExplicitBoundary(
            float secondsOutside,
            float hysteresis,
            bool expected)
        {
            Assert.That(
                WeatherExposureMath.ShouldRetainZoneDuringExit(
                    secondsOutside,
                    hysteresis),
                Is.EqualTo(expected));
        }

        [Test]
        public void RenderSettings_BlendContinuousValuesAndSwitchIdentityAtMidpoint()
        {
            HybridWeatherRenderSettings clear = HybridWeatherRenderSettings.Create(
                "weather.clear",
                Color.white,
                0f,
                80f,
                6000f,
                0f,
                Color.white,
                0f,
                1f,
                new Color(0.9f, 0.95f, 1f),
                -10f,
                -2f,
                -4f,
                -10f);
            HybridWeatherRenderSettings fog = HybridWeatherRenderSettings.Create(
                "weather.fog",
                Color.gray,
                10f,
                40f,
                500f,
                0.4f,
                Color.gray,
                -1f,
                0.5f,
                new Color(0.8f, 0.9f, 1f),
                -20f,
                -4f,
                -12f,
                -20f);

            HybridWeatherRenderSettings blended =
                HybridWeatherRenderSettings.Lerp(clear, fog, 0.5f);

            Assert.That(blended.WeatherId, Is.EqualTo("weather.fog"));
            Assert.That(blended.BaseHeightMeters, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(blended.MaximumFogDistanceMeters, Is.EqualTo(3250f).Within(0.0001f));
            Assert.That(blended.ExposureCompensationEv, Is.EqualTo(-0.5f).Within(0.0001f));
            Assert.That(blended.DaytimeColorFilter.r,
                Is.EqualTo(0.85f).Within(0.0001f));
            Assert.That(blended.DaytimeTemperature,
                Is.EqualTo(-15f).Within(0.0001f));
            Assert.That(blended.DaytimeContrast,
                Is.EqualTo(-8f).Within(0.0001f));
            Assert.That(blended.DaytimeSaturation,
                Is.EqualTo(-15f).Within(0.0001f));
        }

        [Test]
        public void DonorColorGrade_IsVisibleByDayAndReleasesNightAndInteriors()
        {
            float noonExterior = NativeHdrpColorGradingMath
                .CalculateGradeWeight(12f / 24f, 0f);
            float noonInterior = NativeHdrpColorGradingMath
                .CalculateGradeWeight(12f / 24f, 1f);
            float eveningExterior = NativeHdrpColorGradingMath
                .CalculateGradeWeight((19f + 48f / 60f) / 24f, 0f);
            float midnightExterior = NativeHdrpColorGradingMath
                .CalculateGradeWeight(0f, 0f);
            Color rainyFilter = new Color(0.9f, 0.95f, 1f, 1f);

            Assert.That(noonExterior, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(noonInterior, Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(eveningExterior, Is.InRange(0.2f, 0.8f));
            Assert.That(midnightExterior, Is.Zero);
            Assert.That(
                NativeHdrpColorGradingMath.ApplyWeight(rainyFilter, 0f),
                Is.EqualTo(Color.white));
            Assert.That(
                NativeHdrpColorGradingMath.ApplyWeight(rainyFilter, 1f),
                Is.EqualTo(rainyFilter));
            Assert.That(
                NativeHdrpColorGradingMath.ApplyWeight(rainyFilter, 2f).r,
                Is.EqualTo(0.8f).Within(0.0001f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                NativeHdrpColorGradingMath.CalculateGradeWeight(
                    float.NaN,
                    0f));
        }

        [Test]
        public void ProductionColorTuning_MatchesAcceptedVisualPreset()
        {
            var owner = new GameObject("HDRP production color tuning test");
            try
            {
                NativeHdrpWeatherBridge bridge =
                    owner.AddComponent<NativeHdrpWeatherBridge>();

                AssertAcceptedProductionColorTuning(bridge);

                bridge.SetRuntimeColorTuning(
                    0f,
                    -50f,
                    -50f,
                    -30f,
                    -30f,
                    -2f);
                bridge.ResetRuntimeColorTuning();

                AssertAcceptedProductionColorTuning(bridge);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static void AssertAcceptedProductionColorTuning(
            NativeHdrpWeatherBridge bridge)
        {
            Assert.That(bridge.RuntimeColorGradeStrength, Is.EqualTo(1.36f));
            Assert.That(bridge.RuntimeTemperatureOffset, Is.EqualTo(27.14f));
            Assert.That(bridge.RuntimeTintOffset, Is.EqualTo(2.76f));
            Assert.That(bridge.RuntimeContrastOffset, Is.EqualTo(0.75f));
            Assert.That(bridge.RuntimeSaturationOffset, Is.EqualTo(1.21f));
            Assert.That(bridge.RuntimeExposureOffsetEv, Is.EqualTo(0.41f));
        }

        [Test]
        public void FogVisibility_ConvertsToMeanFreePathUsingMeteorologicalExtinction()
        {
            float meanFreePath = NativeHdrpFogMath.MeanFreePathFromVisibility(3912f);

            Assert.That(meanFreePath, Is.EqualTo(1000f).Within(0.001f));
        }

        [Test]
        public void FogBlend_InterpolatesDensityInsteadOfDistance()
        {
            float blended = NativeHdrpFogMath.BlendMeanFreePathByDensity(
                1000f,
                100f,
                0.5f);

            Assert.That(blended, Is.EqualTo(181.81818f).Within(0.001f));
        }

        [Test]
        public void FixedExposure_UsesDailyClockAndWeatherWithoutCameraMetering()
        {
            float clear = NativeHdrpExposureMath.CalculateFixedExposureEv(
                12.5f,
                7.25f,
                16.2833f / 24f,
                0f,
                0f,
                new Vector2(-1f, 14f));
            float storm = NativeHdrpExposureMath.CalculateFixedExposureEv(
                12.5f,
                7.25f,
                16.2833f / 24f,
                -0.75f,
                1f,
                new Vector2(-1f, 14f));

            Assert.That(clear, Is.EqualTo(12.5f).Within(0.0001f));
            Assert.That(storm, Is.EqualTo(12.75f).Within(0.0001f));
            Assert.That(storm, Is.GreaterThan(clear));
        }

        [Test]
        public void DailyExposure_HoldsReadableNightFloorThroughThreeAm()
        {
            float midnight = NativeHdrpExposureMath.CalculateDailyExposureEv(
                12.5f,
                7.25f,
                0f);
            float threeAm = NativeHdrpExposureMath.CalculateDailyExposureEv(
                12.5f,
                7.25f,
                3f / 24f);
            float screenshotTime =
                NativeHdrpExposureMath.CalculateDailyExposureEv(
                    12.5f,
                    7.25f,
                    (3f + 27f / 60f) / 24f);
            float sevenAm = NativeHdrpExposureMath.CalculateDailyExposureEv(
                12.5f,
                7.25f,
                7f / 24f);

            Assert.That(midnight, Is.EqualTo(7.25f).Within(0.0001f));
            Assert.That(threeAm, Is.EqualTo(7.25f).Within(0.0001f));
            Assert.That(screenshotTime, Is.InRange(7.25f, 7.5f));
            Assert.That(sevenAm, Is.EqualTo(12.5f).Within(0.0001f));
        }

        [Test]
        public void IndoorExposure_LiftsReadabilityFromSmoothedEnclosure()
        {
            var limits = new Vector2(-1f, 14f);

            Assert.That(
                NativeHdrpExposureMath.ApplyIndoorExposureLift(
                    12.5f,
                    0f,
                    2f,
                    limits),
                Is.EqualTo(12.5f).Within(0.0001f));
            Assert.That(
                NativeHdrpExposureMath.ApplyIndoorExposureLift(
                    12.5f,
                    0.5f,
                    2f,
                    limits),
                Is.EqualTo(11.5f).Within(0.0001f));
            Assert.That(
                NativeHdrpExposureMath.ApplyIndoorExposureLift(
                    12.5f,
                    1f,
                    2f,
                    limits),
                Is.EqualTo(10.5f).Within(0.0001f));
        }

        [Test]
        public void DailyExposure_DawnAndDuskRemainContinuousAndMonotonic()
        {
            float dawnEarly = NativeHdrpExposureMath.CalculateDailyExposureEv(
                12.5f,
                7.25f,
                4f / 24f);
            float dawnLate = NativeHdrpExposureMath.CalculateDailyExposureEv(
                12.5f,
                7.25f,
                6f / 24f);
            float duskEarly = NativeHdrpExposureMath.CalculateDailyExposureEv(
                12.5f,
                7.25f,
                20f / 24f);
            float duskLate = NativeHdrpExposureMath.CalculateDailyExposureEv(
                12.5f,
                7.25f,
                22f / 24f);

            Assert.That(dawnLate, Is.GreaterThan(dawnEarly));
            Assert.That(duskLate, Is.LessThan(duskEarly));
            Assert.That(dawnEarly, Is.InRange(7.25f, 12.5f));
            Assert.That(duskLate, Is.InRange(7.25f, 12.5f));
        }

        [Test]
        public void DailyExposure_AdaptsBeforeTheAcceptedEveningIsDark()
        {
            float afternoon = NativeHdrpExposureMath.CalculateDailyExposureEv(
                12.5f,
                7.25f,
                (13f + 39f / 60f) / 24f);
            float evening = NativeHdrpExposureMath.CalculateDailyExposureEv(
                12.5f,
                7.25f,
                (19f + 48f / 60f) / 24f);
            float night = NativeHdrpExposureMath.CalculateDailyExposureEv(
                12.5f,
                7.25f,
                (22f + 59f / 60f) / 24f);

            Assert.That(afternoon, Is.EqualTo(12.5f).Within(0.0001f));
            Assert.That(evening, Is.InRange(9f, 10.25f));
            Assert.That(night, Is.EqualTo(7.25f).Within(0.0001f));
            Assert.That(afternoon, Is.GreaterThan(evening));
            Assert.That(evening, Is.GreaterThan(night));
        }

        [Test]
        public void IndoorExposure_SuppressesUnoccludedLegacySkyIndirectLight()
        {
            float exterior =
                NativeHdrpExposureMath.CalculateIndirectDiffuseMultiplier(
                    1f,
                    0f,
                    0.18f);
            float interior =
                NativeHdrpExposureMath.CalculateIndirectDiffuseMultiplier(
                    1f,
                    1f,
                    0.18f);

            Assert.That(exterior, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(interior, Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(interior, Is.LessThan(exterior));
        }

        [Test]
        public void DonorReference_GlobalLightingKeepsWeatherReadable()
        {
            var limits = new Vector2(-1f, 14f);
            float clear = NativeHdrpExposureMath.CalculateFixedExposureEv(
                NativeHdrpExposureMath.FinnishSummerDaylightFixedExposureEv,
                7.25f,
                13.5f / 24f,
                0f,
                0f,
                limits);
            float overcast = NativeHdrpExposureMath.CalculateFixedExposureEv(
                NativeHdrpExposureMath.FinnishSummerDaylightFixedExposureEv,
                7.25f,
                13.5f / 24f,
                -0.2f,
                0.4f,
                limits);
            float heavyRain = NativeHdrpExposureMath.CalculateFixedExposureEv(
                NativeHdrpExposureMath.FinnishSummerDaylightFixedExposureEv,
                7.25f,
                13.5f / 24f,
                -0.55f,
                0.8f,
                limits);

            Assert.That(clear, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(overcast, Is.EqualTo(clear).Within(0.0001f));
            Assert.That(heavyRain - clear, Is.InRange(0.1f, 0.25f));

            float clearFill =
                NativeHdrpExposureMath.CalculateIndirectDiffuseMultiplier(
                    0.9f,
                    0f,
                    0.18f);
            float overcastFill =
                NativeHdrpExposureMath.CalculateIndirectDiffuseMultiplier(
                    0.22f,
                    0f,
                    0.18f);
            float rainFill =
                NativeHdrpExposureMath.CalculateIndirectDiffuseMultiplier(
                    0f,
                    0f,
                    0.18f);

            Assert.That(clearFill, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(overcastFill, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(rainFill, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void StreamedZoneCatalog_MatchesCellSceneAndRejectsDuplicateIds()
        {
            WeatherZoneProfile profile =
                ScriptableObject.CreateInstance<WeatherZoneProfile>();
            WeatherZoneCellCatalog catalog =
                ScriptableObject.CreateInstance<WeatherZoneCellCatalog>();
            try
            {
                const string scenePath =
                    "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/Streaming/Scenes/Cells/World_Cell_-3_0_Legacy.unity";
                WeatherZoneCellDefinition first =
                    WeatherZoneCellDefinition.CreateForAuthoring(
                        "weather.zone.teimo.test",
                        "Teimo test",
                        scenePath,
                        Vector3.one,
                        Quaternion.identity,
                        new Vector3(4f, 3f, 2f),
                        100,
                        true,
                        profile);
                catalog.ConfigureForAuthoring(first);
                var matching = new List<WeatherZoneCellDefinition>();

                int count = catalog.CopyDefinitionsForScene(
                    scenePath.Replace('/', '\\'),
                    matching);

                Assert.That(count, Is.EqualTo(1));
                Assert.That(matching[0].StableId, Is.EqualTo(first.StableId));
                Assert.That(catalog.ValidateConfiguration(), Is.Empty);

                catalog.ConfigureForAuthoring(first, first);
                Assert.That(
                    catalog.ValidateConfiguration(),
                    Has.Some.Contains("Duplicate streamed weather-zone stable ID"));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void Resolver_SelectsHighestPriorityOverlappingZone()
        {
            GameObject registryOwner = new GameObject("WeatherZoneRegistry_Test");
            GameObject lowOwner = new GameObject("LowPriorityZone_Test");
            GameObject highOwner = new GameObject("HighPriorityZone_Test");
            GameObject resolverOwner = new GameObject("WeatherResolver_Test");
            WeatherZoneProfile profile = ScriptableObject.CreateInstance<WeatherZoneProfile>();
            try
            {
                WeatherZoneRegistry registry =
                    registryOwner.AddComponent<WeatherZoneRegistry>();
                BoxCollider lowCollider = lowOwner.AddComponent<BoxCollider>();
                BoxCollider highCollider = highOwner.AddComponent<BoxCollider>();
                WeatherZone low = lowOwner.AddComponent<WeatherZone>();
                WeatherZone high = highOwner.AddComponent<WeatherZone>();
                low.ConfigureForAuthoring("zone.low", profile, 10, lowCollider);
                high.ConfigureForAuthoring("zone.high", profile, 20, highCollider);
                registry.RefreshLoadedObjects();

                WeatherExposureResolver resolver =
                    resolverOwner.AddComponent<WeatherExposureResolver>();
                resolver.ConfigureForAuthoring(
                    registry,
                    null,
                    resolverOwner.transform,
                    null,
                    resolverOwner.transform);
                resolver.ResolveImmediately();

                Assert.That(resolver.CurrentZone, Is.SameAs(high));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(resolverOwner);
                Object.DestroyImmediate(highOwner);
                Object.DestroyImmediate(lowOwner);
                Object.DestroyImmediate(registryOwner);
            }
        }

        [Test]
        public void Resolver_RebindsFromStartupPlaceholderToRuntimeAudioListener()
        {
            GameObject registryOwner = new GameObject("WeatherZoneRegistry_RuntimeAnchor");
            GameObject zoneOwner = new GameObject("RuntimeListenerZone");
            GameObject resolverOwner = new GameObject("WeatherResolver_RuntimeAnchor");
            GameObject placeholder = new GameObject("DisabledStartupCamera");
            GameObject listenerOwner = new GameObject("RuntimeAudioListener");
            WeatherZoneProfile profile = ScriptableObject.CreateInstance<WeatherZoneProfile>();
            try
            {
                Camera placeholderCamera = placeholder.AddComponent<Camera>();
                placeholderCamera.enabled = false;
                WeatherZoneRegistry registry =
                    registryOwner.AddComponent<WeatherZoneRegistry>();
                zoneOwner.transform.position = new Vector3(20f, 2f, -5f);
                BoxCollider zoneCollider = zoneOwner.AddComponent<BoxCollider>();
                zoneCollider.size = new Vector3(4f, 4f, 4f);
                WeatherZone zone = zoneOwner.AddComponent<WeatherZone>();
                zone.ConfigureForAuthoring(
                    "zone.runtime-listener",
                    profile,
                    100,
                    zoneCollider);
                registry.RefreshLoadedObjects();

                WeatherExposureResolver resolver =
                    resolverOwner.AddComponent<WeatherExposureResolver>();
                resolver.ConfigureForAuthoring(
                    registry,
                    null,
                    placeholder.transform,
                    null,
                    resolverOwner.transform);
                listenerOwner.transform.position = zoneOwner.transform.position;
                Camera runtimeCamera = listenerOwner.AddComponent<Camera>();
                runtimeCamera.enabled = true;
                listenerOwner.tag = "MainCamera";
                AudioListener listener = listenerOwner.AddComponent<AudioListener>();
                listener.enabled = true;

                Assert.That(
                    resolver.TryBindRuntimeCamera(runtimeCamera),
                    Is.True);

                Assert.That(resolver.CurrentZone, Is.SameAs(zone));
                Assert.That(
                    resolver.VisualAnchorPosition,
                    Is.EqualTo(listenerOwner.transform.position));
            }
            finally
            {
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(listenerOwner);
                Object.DestroyImmediate(placeholder);
                Object.DestroyImmediate(resolverOwner);
                Object.DestroyImmediate(zoneOwner);
                Object.DestroyImmediate(registryOwner);
            }
        }
    }
}
