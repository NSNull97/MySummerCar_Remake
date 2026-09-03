using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Lighting.Editor
{
    public static class LightingContentBuilder
    {
        public const string ContentRoot =
            "Assets/Game/Lighting/Content/Profiles";
        public const string CatalogPath = ContentRoot +
            "/Phase1LocalLightingCatalog.asset";

        private readonly struct ProfileSpec
        {
            public ProfileSpec(
                string id,
                LightFixtureCategory category,
                LightFixtureShape shape,
                LightUnit unit,
                float intensity,
                float range,
                float kelvin,
                float radius,
                Vector2 area,
                Vector2 spot,
                float shadowFade,
                float nativeVolumetric,
                Vector4 beams,
                float emissive,
                LightPowerPolicyKind policy)
            {
                Id = id;
                Category = category;
                Shape = shape;
                Unit = unit;
                Intensity = intensity;
                Range = range;
                Kelvin = kelvin;
                Radius = radius;
                Area = area;
                Spot = spot;
                ShadowFade = shadowFade;
                NativeVolumetric = nativeVolumetric;
                Beams = beams;
                Emissive = emissive;
                Policy = policy;
            }

            public string Id { get; }
            public LightFixtureCategory Category { get; }
            public LightFixtureShape Shape { get; }
            public LightUnit Unit { get; }
            public float Intensity { get; }
            public float Range { get; }
            public float Kelvin { get; }
            public float Radius { get; }
            public Vector2 Area { get; }
            public Vector2 Spot { get; }
            public float ShadowFade { get; }
            public float NativeVolumetric { get; }
            public Vector4 Beams { get; }
            public float Emissive { get; }
            public LightPowerPolicyKind Policy { get; }
        }

        [MenuItem("Tools/Lighting/Build Lighting Profiles")]
        public static void BuildProfilesMenu()
        {
            BuildProfiles();
            Debug.Log("[Lighting] Rebuilt the Phase 1 profile catalog.");
        }

        public static LightingProfileCatalog BuildProfiles()
        {
            EnsureFolder("Assets/Game/Lighting/Content");
            EnsureFolder(ContentRoot);

            ProfileSpec[] specs = CreateProfileSpecs();
            var profiles = new List<LightFixtureProfile>(specs.Length);
            for (int index = 0; index < specs.Length; index++)
            {
                ProfileSpec spec = specs[index];
                string fileName = ToPascalCase(spec.Category.ToString()) +
                    ".asset";
                string path = ContentRoot + "/" + fileName;
                LightFixtureProfile profile = LoadOrCreate<LightFixtureProfile>(
                    path);
                Configure(profile, spec);
                EditorUtility.SetDirty(profile);
                profiles.Add(profile);
            }

            LightingQualityProfile[] qualities =
            {
                ConfigureQuality("Low", 4, 1, 0, 6, 70f, 22f, 55f, 0.35f, 0.35f),
                ConfigureQuality("Medium", 8, 2, 1, 12, 95f, 32f, 75f, 0.55f, 0.25f),
                ConfigureQuality("High", 12, 4, 3, 18, 120f, 45f, 100f, 0.75f, 0.2f),
                ConfigureQuality("Ultra", 18, 6, 5, 28, 160f, 60f, 140f, 1f, 0.15f),
            };

            LightingCalibrationProfile calibration =
                LoadOrCreate<LightingCalibrationProfile>(
                    ContentRoot + "/Phase1LightingCalibration.asset");
            calibration.ConfigureForAuthoring(
                "lighting.calibration.phase1.1990s-finland",
                "NativeHdrpWeatherBridge",
                12.5f,
                7.25f,
                12f,
                2f,
                4f);
            EditorUtility.SetDirty(calibration);

            LightingProfileCatalog catalog =
                LoadOrCreate<LightingProfileCatalog>(CatalogPath);
            catalog.ConfigureForAuthoring(
                "lighting.catalog.phase1.local-lighting",
                profiles.ToArray(),
                qualities,
                calibration);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return catalog;
        }

        private static ProfileSpec[] CreateProfileSpecs()
        {
            Vector4 noBeam = Vector4.zero;
            Vector4 environmentBeam = new(0.015f, 0.25f, 0.16f, 0.2f);
            Vector4 vehicleBeam = new(0.002f, 0.025f, 0.015f, 0.02f);
            Vector4 handBeam = new(0.008f, 0.18f, 0.1f, 0.14f);
            return new[]
            {
                new ProfileSpec("lighting.fixture.domestic-incandescent", LightFixtureCategory.DomesticIncandescent, LightFixtureShape.Point, LightUnit.Lumen, 760f, 8f, 3300f, 0.08f, new Vector2(0.08f, 0.08f), new Vector2(45f, 65f), 18f, 0.02f, noBeam, 3.25f, LightPowerPolicyKind.ManualSwitch),
                new ProfileSpec("lighting.fixture.enclosed-ceiling", LightFixtureCategory.EnclosedCeiling, LightFixtureShape.Point, LightUnit.Lumen, 1050f, 9f, 3500f, 0.14f, new Vector2(0.28f, 0.28f), new Vector2(45f, 65f), 20f, 0.03f, noBeam, 3.25f, LightPowerPolicyKind.ManualSwitch),
                new ProfileSpec("lighting.fixture.fluorescent", LightFixtureCategory.Fluorescent, LightFixtureShape.AreaRectangle, LightUnit.Lumen, 1200f, 7.5f, 4200f, 0.035f, new Vector2(1.2f, 0.08f), new Vector2(45f, 65f), 20f, 0.02f, noBeam, 3.5f, LightPowerPolicyKind.ManualSwitch),
                new ProfileSpec("lighting.fixture.teimo-shop", LightFixtureCategory.TeimoShop, LightFixtureShape.AreaRectangle, LightUnit.Lumen, 32_500f, 18f, 6_000f, 0.04f, new Vector2(1.4f, 0.1f), new Vector2(45f, 65f), 38f, 0.05f, noBeam, 5.5f, LightPowerPolicyKind.BusinessOpenAndOwnerPresent),
                new ProfileSpec("lighting.fixture.refrigerated-display", LightFixtureCategory.RefrigeratedDisplay, LightFixtureShape.AreaRectangle, LightUnit.Lumen, 15_000f, 3.5f, 15_000f, 0.01f, new Vector2(0.65f, 1.25f), new Vector2(45f, 65f), 8f, 0f, noBeam, 0f, LightPowerPolicyKind.AlwaysWhenPowered),
                new ProfileSpec("lighting.fixture.teimo-pub", LightFixtureCategory.TeimoPub, LightFixtureShape.AreaRectangle, LightUnit.Lumen, 32_500f, 14f, 6_000f, 0.05f, new Vector2(1.4f, 0.1f), new Vector2(45f, 65f), 32f, 0.03f, noBeam, 5.5f, LightPowerPolicyKind.BusinessOpenAndOwnerPresent),
                new ProfileSpec("lighting.fixture.fleetari-workshop", LightFixtureCategory.FleetariWorkshop, LightFixtureShape.AreaTube, LightUnit.Lumen, 4200f, 18f, 4100f, 0.035f, new Vector2(1.5f, 0.11f), new Vector2(45f, 65f), 38f, 0.08f, noBeam, 7f, LightPowerPolicyKind.BusinessOpenAndOwnerPresent),
                new ProfileSpec("lighting.fixture.street-lamp", LightFixtureCategory.StreetLamp, LightFixtureShape.SpotCone, LightUnit.Lumen, 9000f, 30f, 3100f, 0.09f, new Vector2(0.24f, 0.14f), new Vector2(58f, 105f), 48f, 0.12f, environmentBeam, 7f, LightPowerPolicyKind.DuskToDawn),
                new ProfileSpec("lighting.fixture.exterior-building", LightFixtureCategory.ExteriorBuilding, LightFixtureShape.SpotCone, LightUnit.Lumen, 3500f, 18f, 3000f, 0.05f, new Vector2(0.16f, 0.1f), new Vector2(50f, 95f), 30f, 0.07f, environmentBeam * 0.55f, 6f, LightPowerPolicyKind.DuskToDawn),
                new ProfileSpec("lighting.fixture.vehicle-low-beam", LightFixtureCategory.VehicleLowBeam, LightFixtureShape.SpotCone, LightUnit.Candela, 6500f, 35f, 3300f, 0.045f, new Vector2(0.14f, 0.1f), new Vector2(26f, 48f), 48f, 0.015f, vehicleBeam, 4f, LightPowerPolicyKind.VehicleElectrical),
                new ProfileSpec("lighting.fixture.vehicle-high-beam", LightFixtureCategory.VehicleHighBeam, LightFixtureShape.SpotCone, LightUnit.Candela, 43000f, 95f, 3400f, 0.055f, new Vector2(0.14f, 0.1f), new Vector2(20f, 38f), 110f, 0.24f, vehicleBeam * 1.25f, 9f, LightPowerPolicyKind.VehicleElectrical),
                new ProfileSpec("lighting.fixture.vehicle-tail", LightFixtureCategory.VehicleTail, LightFixtureShape.Point, LightUnit.Lumen, 90f, 7f, 2200f, 0.025f, new Vector2(0.09f, 0.05f), new Vector2(45f, 65f), 10f, 0f, noBeam, 5f, LightPowerPolicyKind.VehicleElectrical),
                new ProfileSpec("lighting.fixture.vehicle-brake", LightFixtureCategory.VehicleBrake, LightFixtureShape.Point, LightUnit.Lumen, 420f, 12f, 2200f, 0.035f, new Vector2(0.1f, 0.06f), new Vector2(45f, 65f), 15f, 0f, noBeam, 8f, LightPowerPolicyKind.VehicleElectrical),
                new ProfileSpec("lighting.fixture.vehicle-indicator", LightFixtureCategory.VehicleIndicator, LightFixtureShape.Point, LightUnit.Lumen, 250f, 10f, 2100f, 0.025f, new Vector2(0.08f, 0.04f), new Vector2(45f, 65f), 12f, 0f, noBeam, 7f, LightPowerPolicyKind.VehicleElectrical),
                new ProfileSpec("lighting.fixture.vehicle-reverse", LightFixtureCategory.VehicleReverse, LightFixtureShape.SpotCone, LightUnit.Lumen, 700f, 14f, 3900f, 0.035f, new Vector2(0.09f, 0.05f), new Vector2(50f, 78f), 18f, 0.03f, noBeam, 6f, LightPowerPolicyKind.VehicleElectrical),
                new ProfileSpec("lighting.fixture.vehicle-license-plate", LightFixtureCategory.VehicleLicensePlate, LightFixtureShape.SpotCone, LightUnit.Lumen, 80f, 4f, 3100f, 0.015f, new Vector2(0.05f, 0.02f), new Vector2(45f, 82f), 5f, 0f, noBeam, 4f, LightPowerPolicyKind.VehicleElectrical),
                new ProfileSpec("lighting.fixture.vehicle-dashboard", LightFixtureCategory.VehicleDashboard, LightFixtureShape.Point, LightUnit.Lumen, 24f, 1.8f, 2500f, 0.01f, new Vector2(0.04f, 0.02f), new Vector2(45f, 65f), 2f, 0f, noBeam, 2f, LightPowerPolicyKind.VehicleElectrical),
                new ProfileSpec("lighting.fixture.vehicle-interior", LightFixtureCategory.VehicleInterior, LightFixtureShape.Point, LightUnit.Lumen, 180f, 3.5f, 2800f, 0.04f, new Vector2(0.12f, 0.06f), new Vector2(45f, 65f), 5f, 0f, noBeam, 4f, LightPowerPolicyKind.VehicleElectrical),
                new ProfileSpec("lighting.fixture.player-flashlight", LightFixtureCategory.PlayerFlashlight, LightFixtureShape.SpotCone, LightUnit.Lumen, 300f, 20f, 4000f, 0.025f, new Vector2(0.06f, 0.06f), new Vector2(20f, 38f), 28f, 0.02f, handBeam * 0.3f, 4.5f, LightPowerPolicyKind.ManualSwitch),
                new ProfileSpec("lighting.fixture.home-exterior", LightFixtureCategory.HomeExterior, LightFixtureShape.SpotCone, LightUnit.Lumen, 4000f, 20f, 6500f, 0.05f, new Vector2(0.32f, 0.18f), new Vector2(50f, 92f), 32f, 0.07f, environmentBeam * 0.55f, 6.5f, LightPowerPolicyKind.ManualSwitch),
            };
        }

        private static void Configure(
            LightFixtureProfile profile,
            ProfileSpec spec)
        {
            bool shadows = spec.Category !=
                LightFixtureCategory.RefrigeratedDisplay;
            LightFixtureQualitySettings low = QualitySettings(
                50f, false, false, ShadowUpdateMode.OnDemand, 256,
                VolumetricBeamQuality.Off);
            LightFixtureQualitySettings medium = QualitySettings(
                75f, shadows, false, ShadowUpdateMode.OnDemand, 512,
                spec.Beams == Vector4.zero
                    ? VolumetricBeamQuality.Off
                    : VolumetricBeamQuality.StandardDefinition);
            LightFixtureQualitySettings high = QualitySettings(
                110f, shadows, shadows, ShadowUpdateMode.OnDemand, 768,
                spec.Beams == Vector4.zero
                    ? VolumetricBeamQuality.Off
                    : VolumetricBeamQuality.HighDefinition);
            LightFixtureQualitySettings ultra = QualitySettings(
                160f, shadows, shadows, ShadowUpdateMode.EveryFrame, 1024,
                spec.Beams == Vector4.zero
                    ? VolumetricBeamQuality.Off
                    : VolumetricBeamQuality.HighDefinition);
            profile.ConfigureForAuthoring(
                spec.Id,
                spec.Category,
                spec.Shape,
                spec.Unit,
                spec.Intensity,
                spec.Range,
                spec.Kelvin,
                Color.white,
                spec.Radius,
                spec.Area,
                spec.Spot,
                spec.Shape == LightFixtureShape.SpotCone,
                spec.ShadowFade,
                spec.NativeVolumetric,
                spec.Beams,
                Color.white,
                spec.Emissive,
                0.2f,
                new Vector2(0.08f, 0.12f),
                spec.Policy,
                low,
                medium,
                high,
                ultra);
        }

        private static LightFixtureQualitySettings QualitySettings(
            float distance,
            bool shadows,
            bool contact,
            ShadowUpdateMode updateMode,
            int resolution,
            VolumetricBeamQuality beam)
        {
            var settings = new LightFixtureQualitySettings();
            settings.ConfigureForAuthoring(
                distance,
                distance * 0.45f,
                shadows,
                contact,
                updateMode,
                resolution,
                beam,
                distance * 0.8f);
            return settings;
        }

        private static LightingQualityProfile ConfigureQuality(
            string tier,
            int shadows,
            int everyFrame,
            int hdBeams,
            int sdBeams,
            float lightDistance,
            float shadowDistance,
            float beamDistance,
            float fogQuality,
            float refresh)
        {
            string path = ContentRoot + "/LightingQuality" + tier + ".asset";
            LightingQualityProfile quality =
                LoadOrCreate<LightingQualityProfile>(path);
            quality.ConfigureForAuthoring(
                "lighting.quality." + tier.ToLowerInvariant(),
                shadows,
                everyFrame,
                hdBeams,
                sdBeams,
                lightDistance,
                shadowDistance,
                beamDistance,
                fogQuality,
                refresh);
            EditorUtility.SetDirty(quality);
            return quality;
        }

        private static T LoadOrCreate<T>(string path)
            where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static string ToPascalCase(string value)
        {
            return value.Replace(" ", string.Empty);
        }
    }
}
