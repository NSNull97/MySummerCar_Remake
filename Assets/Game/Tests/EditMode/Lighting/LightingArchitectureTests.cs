using System.Reflection;
using System.Linq;
using MSC.Lighting.Editor;
using MSC.Lighting.Production;
using MSC.World.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Lighting.Tests.EditMode
{
    public sealed class LightingArchitectureTests
    {
        [Test]
        public void Fixture_DoesNotOwnPerFrameUpdate()
        {
            MethodInfo update = typeof(GameLightFixture).GetMethod(
                "Update",
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);

            Assert.That(update, Is.Null,
                "Per-fixture Update would bypass the central lighting manager.");
        }

        [Test]
        public void RuntimeManager_AppliesInitialOffStateToAuthoredLight()
        {
            var managerObject = new GameObject("Lighting Runtime Manager");
            var fixtureObject = new GameObject("Authored House Light");
            LightFixtureProfile profile = ScriptableObject.CreateInstance<
                LightFixtureProfile>();
            try
            {
                var quality = new LightFixtureQualitySettings();
                profile.ConfigureForAuthoring(
                    "lighting.test.house",
                    LightFixtureCategory.DomesticIncandescent,
                    LightFixtureShape.Point,
                    LightUnit.Lumen,
                    800f,
                    8f,
                    2700f,
                    Color.white,
                    0.03f,
                    new Vector2(0.08f, 0.08f),
                    new Vector2(45f, 65f),
                    false,
                    20f,
                    0f,
                    Vector4.zero,
                    Color.white,
                    5f,
                    0.2f,
                    new Vector2(0.08f, 0.12f),
                    LightPowerPolicyKind.ManualSwitch,
                    quality,
                    quality,
                    quality,
                    quality);

                Light light = fixtureObject.AddComponent<Light>();
                light.enabled = true;
                var fixture = fixtureObject.AddComponent<GameLightFixture>();
                fixture.InitializeRuntime(
                    "fixture.test.house",
                    profile,
                    new[] { light },
                    System.Array.Empty<Renderer>(),
                    "source.test",
                    "circuit.test",
                    "switch.test",
                    "zone.test",
                    string.Empty,
                    string.Empty,
                    true);

                var grid = new ElectricalGridService();
                grid.RegisterSource("source.test", true);
                grid.RegisterCircuit("circuit.test", "source.test", true);
                grid.RegisterSwitch("switch.test", false);
                var manager = managerObject.AddComponent<LightingRuntimeManager>();
                manager.Initialize(grid, null, null, null, null, null);

                Assert.That(fixture.LogicalOn, Is.False);
                Assert.That(fixture.VisualFactor, Is.Zero);
                Assert.That(light.enabled, Is.False);
                Assert.That(light.intensity, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(fixtureObject);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void VolumetricAdapter_DoesNotCompileAgainstVendorAssembly()
        {
            string[] references = typeof(VolumetricBeamAdapter).Assembly
                .GetReferencedAssemblies()
                .Select(assembly => assembly.Name)
                .ToArray();

            Assert.That(references, Does.Not.Contain("Assembly-CSharp"));
            Assert.That(references, Has.None.Contains("VolumetricLightBeam"));
        }

        [Test]
        public void Fixture_UsesOnlyBudgetedRealtimeShadows()
        {
            var fixtureObject = new GameObject("Runtime Shadow Fixture");
            LightFixtureProfile profile = ScriptableObject.CreateInstance<
                LightFixtureProfile>();
            try
            {
                var quality = new LightFixtureQualitySettings();
                quality.ConfigureForAuthoring(
                    30f,
                    20f,
                    true,
                    true,
                    ShadowUpdateMode.OnDemand,
                    256,
                    VolumetricBeamQuality.Off,
                    0f);
                profile.ConfigureForAuthoring(
                    "lighting.test.runtime-shadow",
                    LightFixtureCategory.StreetLamp,
                    LightFixtureShape.SpotCone,
                    LightUnit.Lumen,
                    1_000f,
                    12f,
                    4_000f,
                    Color.white,
                    0.02f,
                    new Vector2(0.04f, 0.04f),
                    new Vector2(30f, 45f),
                    true,
                    20f,
                    0.1f,
                    Vector4.zero,
                    Color.white,
                    4f,
                    0.1f,
                    new Vector2(0.05f, 0.08f),
                    LightPowerPolicyKind.AlwaysWhenPowered,
                    quality,
                    quality,
                    quality,
                    quality);

                Light light = fixtureObject.AddComponent<Light>();
                light.type = LightType.Spot;
                var fixture = fixtureObject.AddComponent<GameLightFixture>();
                fixture.InitializeRuntime(
                    "fixture.test.runtime-shadow",
                    profile,
                    new[] { light },
                    System.Array.Empty<Renderer>(),
                    "source.test",
                    "circuit.test",
                    string.Empty,
                    "zone.test",
                    string.Empty,
                    string.Empty,
                    true,
                    2f);
                fixture.SetLogicalState(true);
                fixture.ApplyVisualFactor(1f);
                Assert.That(light.intensity, Is.EqualTo(2_000f));
                Assert.That(fixture.IntensityMultiplier, Is.EqualTo(2f));

                fixture.ApplyQuality(
                    quality,
                    shadowSelected: true,
                    everyFrameSelected: false,
                    VolumetricBeamQuality.Off,
                    0f);
                HDAdditionalLightData hd = light.GetComponent<
                    HDAdditionalLightData>();
                Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft));
                Assert.That(
                    hd.shadowUpdateMode,
                    Is.EqualTo(ShadowUpdateMode.OnDemand),
                    "A selected secondary light must keep cached room " +
                    "occlusion instead of shining through walls.");

                fixture.ApplyQuality(
                    quality,
                    shadowSelected: true,
                    everyFrameSelected: true,
                    VolumetricBeamQuality.Off,
                    0f);
                Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft));
                Assert.That(
                    hd.shadowUpdateMode,
                    Is.EqualTo(ShadowUpdateMode.EveryFrame));
            }
            finally
            {
                Object.DestroyImmediate(fixtureObject);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void LightSwitch_HidesGeneratedCubeButKeepsInteractionCollider()
        {
            GameObject switchObject = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            try
            {
                Renderer renderer = switchObject.GetComponent<Renderer>();
                Collider collider = switchObject.GetComponent<Collider>();
                LightSwitchInteractionTarget target =
                    switchObject.AddComponent<LightSwitchInteractionTarget>();
                MethodInfo applyPolicy = typeof(LightSwitchInteractionTarget)
                    .GetMethod(
                        "ApplyProxyPresentationPolicy",
                        BindingFlags.Instance | BindingFlags.NonPublic);

                Assert.That(applyPolicy, Is.Not.Null);
                applyPolicy.Invoke(target, null);
                Assert.That(renderer.enabled, Is.False);
                Assert.That(collider.enabled, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(switchObject);
            }
        }

        [TestCase("Directional Sun", LightType.Point, true)]
        [TestCase("Moon Directional Light", LightType.Point, true)]
        [TestCase("Kitchen Lamp", LightType.Directional, true)]
        [TestCase("Kitchen Lamp", LightType.Point, false)]
        public void MapBuilder_RejectsGlobalDirectionalLights(
            string objectName,
            LightType lightType,
            bool expectedRejected)
        {
            var gameObject = new GameObject(objectName);
            try
            {
                Light light = gameObject.AddComponent<Light>();
                light.type = lightType;
                MethodInfo method = typeof(LightingMapBuilder).GetMethod(
                    "IsGlobalDirectionalLight",
                    BindingFlags.Static | BindingFlags.NonPublic);

                Assert.That(method, Is.Not.Null);
                bool rejected = (bool)method.Invoke(
                    null,
                    new object[] { gameObject.transform, light });
                Assert.That(rejected, Is.EqualTo(expectedRejected));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [TestCase(
            "world.light.2ba413fd667b5d7d78eb69c64da0e1a2",
            false)]
        [TestCase(
            "world.light.501e356e7b562a6d2e32ab24bfee8504",
            false)]
        [TestCase(
            "world.light.ba653c2356f3c338bf05d8fe6a63d4e8",
            true)]
        [TestCase(
            "world.light.141ca6c62d6a240aa5b300f0afab5132",
            false)]
        [TestCase(
            "world.light.b0651cf48bfe7aab01faf087e8addd58",
            false)]
        [TestCase(
            "world.light.c72a32fd19487593b4042d11cf7c384b",
            false)]
        public void BindingBuilder_SplitsSharedTeimoLampBank(
            string lightId,
            bool expectedPubBinding)
        {
            MethodInfo method = typeof(WorldLightingBindingCatalogBuilder)
                .GetMethod(
                    "IsSharedTeimoPubLight",
                    BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            bool isPubBinding = (bool)method.Invoke(
                null,
                new object[] { lightId });
            Assert.That(isPubBinding, Is.EqualTo(expectedPubBinding));
        }

        [TestCase("world.light.teimo.shop.refrigerator.01")]
        [TestCase("world.light.teimo.shop.refrigerator.02")]
        [TestCase("world.light.teimo.shop.refrigerator.03")]
        public void BindingBuilder_RecognizesTeimoRefrigeratorLights(
            string lightId)
        {
            MethodInfo method = typeof(WorldLightingBindingCatalogBuilder)
                .GetMethod(
                    "IsTeimoRefrigeratorLight",
                    BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            Assert.That(
                (bool)method.Invoke(null, new object[] { lightId }),
                Is.True);
        }

        [Test]
        public void BindingBuilder_GarageEntryTransition_ReachesDoorThreshold()
        {
            MethodInfo method = typeof(WorldLightingBindingCatalogBuilder)
                .GetMethod(
                    "CreateGarageEntryTransitionBounds",
                    BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            var interior = new Bounds(
                new Vector3(153.3864f, 2.2692845f, -1038.4102f),
                new Vector3(4.589386f, 2.3642187f, 9.502197f));
            Bounds transition = (Bounds)method.Invoke(
                null,
                new object[] { interior });

            Assert.That(
                transition.Contains(
                    new Vector3(153.3864f, 2.5f, -1033.23f)),
                Is.True,
                "A standing camera at the garage-door threshold must use " +
                "indoor exposure so daylight does not erase the lamps.");
            Assert.That(
                transition.min.z,
                Is.LessThan(interior.max.z),
                "The transition must overlap the audited interior bounds.");
            Assert.That(
                transition.Contains(
                    new Vector3(interior.min.x, 2.5f, -1033.23f)),
                Is.False,
                "The exposure lip must stay inside the doorway width instead " +
                "of brightening the exterior side wall.");
        }

        [Test]
        public void GarageFluorescent_RemainsUsefulUnderDaylightExposure()
        {
            WorldLightingBindingCatalog bindings =
                AssetDatabase.LoadAssetAtPath<WorldLightingBindingCatalog>(
                    "Assets/Game/Lighting/Content/Bindings/" +
                    "Phase1WorldLightingBindings.asset");
            LightFixtureProfile profile = AssetDatabase.LoadAssetAtPath<
                LightFixtureProfile>(
                "Assets/Game/Lighting/Content/Profiles/Fluorescent.asset");

            Assert.That(bindings, Is.Not.Null);
            Assert.That(profile, Is.Not.Null);
            WorldLightingFixtureBinding garageBinding = bindings.Bindings
                .First(value =>
                    value.CircuitId == "grid.home.garage" &&
                    value.Category == LightFixtureCategory.Fluorescent);
            WorldLightDefinition definition = bindings.SourceCatalog.Lights
                .First(value => value.LightId == garageBinding.WorldLightId);
            MethodInfo multiplierMethod = typeof(WorldLightingFixtureAdapter)
                .GetMethod(
                    "ResolveIntensityMultiplier",
                    BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(multiplierMethod, Is.Not.Null);
            float multiplier = (float)multiplierMethod.Invoke(
                null,
                new object[] { definition, garageBinding, profile });
            Assert.That(
                profile.Intensity * multiplier,
                Is.GreaterThanOrEqualTo(16_000f),
                "Each garage area light needs a bounded no-bake floor that " +
                "remains visible under daytime fixed exposure.");
        }

        [Test]
        public void TeimoRefrigerators_UseIndependentAlwaysPoweredCircuit()
        {
            MSC.Lighting.Production.WorldLightingBindingCatalog catalog =
                AssetDatabase.LoadAssetAtPath<
                    MSC.Lighting.Production.WorldLightingBindingCatalog>(
                    "Assets/Game/Lighting/Content/Bindings/" +
                    "Phase1WorldLightingBindings.asset");

            Assert.That(catalog, Is.Not.Null);
            MSC.Lighting.Production.WorldLightingFixtureBinding[] bindings =
                catalog.Bindings
                    .Where(binding => binding.Category ==
                        LightFixtureCategory.RefrigeratedDisplay)
                    .ToArray();
            Assert.That(bindings, Has.Length.EqualTo(3));
            Assert.That(
                bindings.All(binding =>
                    binding.CircuitId == "grid.teimo.refrigeration" &&
                    binding.PowerSourceId == "source.grid.main" &&
                    string.IsNullOrEmpty(binding.SwitchId)),
                Is.True,
                "Refrigeration must survive Teimo's shop-light shutdown but " +
                "remain governed by the shared electrical-grid source.");
        }

        [Test]
        public void RefrigeratedDisplayProfile_IsAlwaysPoweredWithoutEmission()
        {
            LightFixtureProfile profile = AssetDatabase.LoadAssetAtPath<
                LightFixtureProfile>(
                "Assets/Game/Lighting/Content/Profiles/" +
                "RefrigeratedDisplay.asset");

            Assert.That(profile, Is.Not.Null);
            Assert.That(
                profile.Category,
                Is.EqualTo(LightFixtureCategory.RefrigeratedDisplay));
            Assert.That(
                profile.Shape,
                Is.EqualTo(LightFixtureShape.AreaRectangle));
            Assert.That(profile.Intensity, Is.EqualTo(15_000f));
            Assert.That(profile.ColorTemperatureKelvin, Is.EqualTo(15_000f));
            Assert.That(profile.EmissiveIntensity, Is.Zero);
            Assert.That(
                profile.PowerPolicy,
                Is.EqualTo(LightPowerPolicyKind.AlwaysWhenPowered));
            Assert.That(profile.SourceWidthMeters, Is.EqualTo(0.65f));
            Assert.That(profile.SourceHeightMeters, Is.EqualTo(1.25f));
            foreach (LightingQualityTier tier in System.Enum.GetValues(
                         typeof(LightingQualityTier)))
            {
                Assert.That(profile.GetQuality(tier).ShadowsEnabled, Is.False);
            }
        }

        [Test]
        public void FluorescentProfile_BoundsDenseCommercialFixtureBanks()
        {
            LightFixtureProfile profile = AssetDatabase.LoadAssetAtPath<
                LightFixtureProfile>(
                "Assets/Game/Lighting/Content/Profiles/Fluorescent.asset");

            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.Shape,
                Is.EqualTo(LightFixtureShape.AreaRectangle));
            Assert.That(profile.Intensity, Is.EqualTo(1_200f));
            Assert.That(profile.RangeMeters, Is.EqualTo(7.5f));
            Assert.That(profile.ShadowFadeDistanceMeters, Is.EqualTo(20f));
            Assert.That(profile.ColorTemperatureKelvin, Is.EqualTo(4_200f));
            Assert.That(profile.NativeVolumetricMultiplier, Is.EqualTo(0.02f));
        }

        [Test]
        public void ExteriorProfiles_ProvideUsefulDuskPhotometry()
        {
            LightFixtureProfile street = AssetDatabase.LoadAssetAtPath<
                LightFixtureProfile>(
                "Assets/Game/Lighting/Content/Profiles/StreetLamp.asset");
            LightFixtureProfile building = AssetDatabase.LoadAssetAtPath<
                LightFixtureProfile>(
                "Assets/Game/Lighting/Content/Profiles/ExteriorBuilding.asset");
            LightFixtureProfile home = AssetDatabase.LoadAssetAtPath<
                LightFixtureProfile>(
                "Assets/Game/Lighting/Content/Profiles/HomeExterior.asset");
            LightingCalibrationProfile calibration =
                AssetDatabase.LoadAssetAtPath<LightingCalibrationProfile>(
                    "Assets/Game/Lighting/Content/Profiles/" +
                    "Phase1LightingCalibration.asset");

            Assert.That(street, Is.Not.Null);
            Assert.That(building, Is.Not.Null);
            Assert.That(home, Is.Not.Null);
            Assert.That(calibration, Is.Not.Null);
            Assert.That(street.Intensity, Is.EqualTo(9_000f));
            Assert.That(building.Intensity, Is.EqualTo(3_500f));
            Assert.That(home.Intensity, Is.EqualTo(4_000f));
            Assert.That(
                calibration.DuskOnSunElevationDegrees,
                Is.EqualTo(12f));
            Assert.That(
                calibration.DawnOffSunElevationDegrees,
                Is.EqualTo(2f));
        }

        [Test]
        public void TeimoCeilingProfiles_AreOneSidedRectangles()
        {
            LightFixtureProfile shop = AssetDatabase.LoadAssetAtPath<
                LightFixtureProfile>(
                "Assets/Game/Lighting/Content/Profiles/TeimoShop.asset");
            LightFixtureProfile pub = AssetDatabase.LoadAssetAtPath<
                LightFixtureProfile>(
                "Assets/Game/Lighting/Content/Profiles/TeimoPub.asset");

            Assert.That(shop, Is.Not.Null);
            Assert.That(pub, Is.Not.Null);
            Assert.That(shop.Shape,
                Is.EqualTo(LightFixtureShape.AreaRectangle));
            Assert.That(pub.Shape,
                Is.EqualTo(LightFixtureShape.AreaRectangle));
            Assert.That(shop.SourceWidthMeters, Is.EqualTo(1.4f));
            Assert.That(pub.SourceWidthMeters, Is.EqualTo(1.4f));
            Assert.That(pub.SourceHeightMeters, Is.EqualTo(0.1f));
            Assert.That(shop.Intensity, Is.EqualTo(32_500f));
            Assert.That(pub.Intensity, Is.EqualTo(32_500f));
            Assert.That(shop.ColorTemperatureKelvin, Is.EqualTo(6_000f));
            Assert.That(pub.ColorTemperatureKelvin, Is.EqualTo(6_000f));
        }

        [Test]
        public void RuntimeManager_FallsBackToNearestShadowCasterWithoutZone()
        {
            var managerObject = new GameObject("Lighting Runtime Manager");
            var fixtureObject = new GameObject("Unzoned Interior Light");
            var focusObject = new GameObject("Lighting Focus");
            LightFixtureProfile profile = ScriptableObject.CreateInstance<
                LightFixtureProfile>();
            LightingQualityProfile quality = ScriptableObject.CreateInstance<
                LightingQualityProfile>();
            try
            {
                var fixtureQuality = new LightFixtureQualitySettings();
                fixtureQuality.ConfigureForAuthoring(
                    30f,
                    20f,
                    true,
                    true,
                    ShadowUpdateMode.OnDemand,
                    256,
                    VolumetricBeamQuality.Off,
                    0f);
                profile.ConfigureForAuthoring(
                    "lighting.test.unzoned-shadow",
                    LightFixtureCategory.Fluorescent,
                    LightFixtureShape.Point,
                    LightUnit.Lumen,
                    1_000f,
                    10f,
                    4_000f,
                    Color.white,
                    0.02f,
                    new Vector2(0.1f, 0.1f),
                    new Vector2(45f, 65f),
                    false,
                    20f,
                    0f,
                    Vector4.zero,
                    Color.white,
                    4f,
                    0f,
                    new Vector2(0f, 0f),
                    LightPowerPolicyKind.AlwaysWhenPowered,
                    fixtureQuality,
                    fixtureQuality,
                    fixtureQuality,
                    fixtureQuality);
                quality.ConfigureForAuthoring(
                    "lighting.quality.test",
                    4,
                    4,
                    0,
                    0,
                    30f,
                    20f,
                    0f,
                    0f,
                    0.2f);

                Light light = fixtureObject.AddComponent<Light>();
                var fixture = fixtureObject.AddComponent<GameLightFixture>();
                fixture.InitializeRuntime(
                    "fixture.test.unzoned-shadow",
                    profile,
                    new[] { light },
                    System.Array.Empty<Renderer>(),
                    "source.test",
                    "circuit.test",
                    string.Empty,
                    "zone.missing.in-temporary-baseline",
                    string.Empty,
                    string.Empty,
                    true);

                var grid = new ElectricalGridService();
                grid.RegisterSource("source.test", true);
                grid.RegisterCircuit("circuit.test", "source.test", true);
                var manager = managerObject.AddComponent<
                    LightingRuntimeManager>();
                manager.ConfigureProfiles(
                    null,
                    quality,
                    quality,
                    quality,
                    quality,
                    focusObject.transform);
                manager.Initialize(
                    grid,
                    focusObject.transform,
                    null,
                    null,
                    null,
                    null);

                Assert.That(fixture.LogicalOn, Is.True);
                Assert.That(light.shadows, Is.EqualTo(LightShadows.Soft));
                Assert.That(
                    manager.ActiveEveryFrameShadowLightCount,
                    Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
                Object.DestroyImmediate(fixtureObject);
                Object.DestroyImmediate(focusObject);
                Object.DestroyImmediate(profile);
                Object.DestroyImmediate(quality);
            }
        }
    }
}
