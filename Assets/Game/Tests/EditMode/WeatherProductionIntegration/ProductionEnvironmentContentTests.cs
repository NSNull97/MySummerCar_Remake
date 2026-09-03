using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enviro;
using MSC.Bootstrap;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Enviro3Integration.Editor;
using MSC.Weather.Production;
using MSC.Weather.Wetness;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools.Utils;
using GameWeatherSystem = MSC.Weather.System.GameWeatherSystem;
using NativeHDRPWeatherBackend =
    MSC.Weather.System.NativeHDRP.NativeHDRPWeatherBackend;
using WeatherBackendType = MSC.Weather.System.WeatherBackendType;

namespace MSC.Tests.EditMode.WeatherProductionIntegration
{
    public sealed class ProductionEnvironmentContentTests
    {
        [Test]
        public void BootstrapEnvironment_PassesProductionOwnerAndContentAudit()
        {
            Assert.DoesNotThrow(
                ProductionEnvironmentValidator.ValidateOrThrow);
        }

        [Test]
        public void BootstrapEnvironment_HasOnePersistentOwnerAndProductionProfile()
        {
            Scene scene = EditorSceneManager.OpenScene(
                ProductionEnvironmentBuilder.BootstrapScenePath,
                OpenSceneMode.Single);
            GameCompositionRoot[] roots = FindAll<GameCompositionRoot>(scene);
            ProductionEnvironmentController[] controllers =
                FindAll<ProductionEnvironmentController>(scene);
            EnviroManager[] managers = FindAll<EnviroManager>(scene);
            Enviro3EnvironmentAdapter[] adapters =
                FindAll<Enviro3EnvironmentAdapter>(scene);
            ProductionEnvironmentBackendActivator[] activators =
                FindAll<ProductionEnvironmentBackendActivator>(scene);
            ProductionEnvironmentBackendMarker[] markers =
                FindAll<ProductionEnvironmentBackendMarker>(scene);
            ProductionWorldStreamingInstaller[] installers =
                FindAll<ProductionWorldStreamingInstaller>(scene);
            GameWeatherSystem[] weatherSystems =
                FindAll<GameWeatherSystem>(scene);
            NativeHDRPWeatherBackend[] nativeBackends =
                FindAll<NativeHDRPWeatherBackend>(scene);

            Assert.That(roots, Has.Length.EqualTo(1));
            Assert.That(controllers, Has.Length.EqualTo(1));
            Assert.That(managers, Has.Length.EqualTo(1));
            Assert.That(adapters, Has.Length.EqualTo(1));
            Assert.That(activators, Has.Length.EqualTo(1));
            Assert.That(markers, Has.Length.EqualTo(1));
            Assert.That(installers, Has.Length.EqualTo(1));
            Assert.That(weatherSystems, Has.Length.EqualTo(1));
            Assert.That(nativeBackends, Has.Length.EqualTo(1));
            Assert.That(FindAll<WindZone>(scene), Is.Empty);
            Assert.That(IsActive(roots[0]), Is.True);
            Assert.That(IsActive(controllers[0]), Is.True);
            Assert.That(IsActive(activators[0]), Is.True);
            Assert.That(IsActive(managers[0]), Is.False);
            Assert.That(IsActive(adapters[0]), Is.False);
            Assert.That(IsActive(weatherSystems[0]), Is.True);
            Assert.That(IsActive(nativeBackends[0]), Is.True);
            Assert.That(
                weatherSystems[0].SelectedBackend,
                Is.EqualTo(WeatherBackendType.EnviroLegacy),
                "Bootstrap must keep Enviro as the active celestial/sky backend " +
                "until Native HDRP has feature parity for moon and stars.");
            Assert.That(markers[0].gameObject.activeSelf, Is.False);
            Assert.That(activators[0].BackendMarker, Is.SameAs(markers[0]));
            Assert.That(activators[0].HasValidBinding, Is.True);
            Assert.That(
                installers[0].StartupMode,
                Is.EqualTo(
                    ProductionWorldStartupMode.ProductionEnvironmentRequired));
            Assert.That(
                installers[0].Environment,
                Is.SameAs(controllers[0]));
            Assert.That(
                controllers[0].gameObject,
                Is.SameAs(roots[0].gameObject));
            Assert.That(
                managers[0].transform.IsChildOf(markers[0].transform),
                Is.True);
            Assert.That(
                new SerializedObject(controllers[0])
                    .FindProperty("adapterBehaviour")
                    .objectReferenceValue,
                Is.SameAs(weatherSystems[0]));
            Assert.That(
                PrefabUtility.IsPartOfPrefabInstance(managers[0].gameObject),
                Is.True);
            Assert.That(
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    managers[0].gameObject),
                Is.EqualTo(
                    ProductionEnvironmentBuilder.EnviroSourcePrefabPath));
            Assert.That(
                AssetDatabase.GetAssetPath(
                    managers[0].volumeHDRP.sharedProfile),
                Is.EqualTo(
                    HybridEnvironmentMigrationTool.EnviroSkyProfilePath));
            Assert.That(
                managers[0].volumeHDRP.sharedProfile.TryGet(
                    out VisualEnvironment visualEnvironment),
                Is.True);
            Assert.That(visualEnvironment.skyType.value, Is.EqualTo(990));
            Assert.That(
                managers[0].volumeHDRP.sharedProfile.TryGet(
                    out EnviroHDRPSky _),
                Is.True);
            Assert.That(
                managers[0].volumeHDRP.sharedProfile.TryGet(out Fog _),
                Is.False);
            Assert.That(
                managers[0].volumeHDRP.sharedProfile.TryGet(out Exposure _),
                Is.False);
            Assert.That(
                managers[0].volumeHDRP.sharedProfile.TryGet(
                    out IndirectLightingController _),
                Is.False);

            NativeHDRPWeatherBackend native = nativeBackends[0];
            Assert.That(native.RuntimeWeatherVolume, Is.Not.Null);
            Assert.That(
                native.RuntimeWeatherVolume.isGlobal,
                Is.True);
            Assert.That(
                native.RuntimeWeatherVolume.enabled,
                Is.False,
                "The native Volume must activate transactionally at runtime.");
            Assert.That(native.RuntimeWeatherVolume.weight, Is.Zero);
            Assert.That(
                AssetDatabase.GetAssetPath(
                    native.RuntimeWeatherVolume.sharedProfile),
                Is.EqualTo(
                    "Assets/Game/Weather/System/Content/FinnishSummer/" +
                    "NativeHDRPWeatherVolume.asset"));
            Assert.That(native.DirectionalSun, Is.Not.Null);
            Assert.That(native.DirectionalSunData, Is.Not.Null);
            Assert.That(native.Geography, Is.Not.Null);
            Assert.That(native.RainSystems.Any(value => value != null), Is.True);
            Assert.That(
                native.DrizzleSystems.Any(value => value != null),
                Is.True);
            Assert.That(native.LightningFlashLight, Is.Not.Null);
            Assert.That(
                native.LegacyVolumesToSuspend.Contains(
                    managers[0].volumeHDRP),
                Is.True);
            Assert.That(
                native.LegacyOwnersToSuspend.Contains(adapters[0]),
                Is.True);
        }

        [Test]
        public void InstallerAuthoring_PreservesProductionUntilExplicitWorldOnlyConversion()
        {
            var rootObject = new GameObject("M07C_InstallerAuthoringProbe");
            try
            {
                GameCompositionRoot root =
                    rootObject.AddComponent<GameCompositionRoot>();
                ProductionWorldStreamingService streaming =
                    rootObject.AddComponent<ProductionWorldStreamingService>();
                ProductionWorldStreamingInstaller installer =
                    rootObject.AddComponent<ProductionWorldStreamingInstaller>();
                ProductionEnvironmentController environment =
                    rootObject.AddComponent<ProductionEnvironmentController>();
                var backendObject = new GameObject("Backend");
                backendObject.transform.SetParent(rootObject.transform, false);
                ProductionEnvironmentBackendMarker marker =
                    backendObject.AddComponent<
                        ProductionEnvironmentBackendMarker>();
                marker.ConfigureForAuthoring();
                backendObject.SetActive(false);
                ProductionEnvironmentBackendActivator activator =
                    rootObject.AddComponent<
                        ProductionEnvironmentBackendActivator>();
                activator.ConfigureForAuthoring(marker);

                installer.ConfigureEnvironmentForAuthoring(environment);
                installer.ConfigureForAuthoring(
                    root,
                    streaming,
                    rootObject,
                    Vector3.zero,
                    Quaternion.identity);

                Assert.That(
                    installer.StartupMode,
                    Is.EqualTo(
                        ProductionWorldStartupMode
                            .ProductionEnvironmentRequired));
                Assert.That(installer.Environment, Is.SameAs(environment));
                Assert.That(
                    installer.HasCoherentStartupConfiguration,
                    Is.True);

                installer.ConfigureWorldOnlyForAuthoring();

                Assert.That(
                    installer.StartupMode,
                    Is.EqualTo(
                        ProductionWorldStartupMode.WorldOnlyDevelopment));
                Assert.That(installer.Environment, Is.Null);
                Assert.That(
                    rootObject.GetComponent<
                        ProductionEnvironmentController>(),
                    Is.Null);
                Assert.That(
                    rootObject.GetComponent<
                        ProductionEnvironmentBackendActivator>(),
                    Is.Null);
                Assert.That(
                    rootObject.GetComponentInChildren<
                        ProductionEnvironmentBackendMarker>(true),
                    Is.Null);
                Assert.That(
                    installer.HasCoherentStartupConfiguration,
                    Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootObject);
            }
        }

        [Test]
        public void ProductionBindings_UseDirectLowMediumHighVendorAssets()
        {
            Enviro3EnvironmentBindings bindings =
                AssetDatabase.LoadAssetAtPath<Enviro3EnvironmentBindings>(
                    ProductionEnvironmentBuilder.BindingsAssetPath);

            Assert.That(bindings, Is.Not.Null);
            Assert.That(bindings.Low, Is.Not.Null);
            Assert.That(bindings.Medium, Is.Not.Null);
            Assert.That(bindings.High, Is.Not.Null);
            Assert.That(bindings.Medium, Is.Not.SameAs(bindings.High));
            Assert.That(
                AssetDatabase.GetAssetPath(bindings.Medium),
                Does.EndWith("/Profiles/Quality/Medium.asset"));
        }

        [Test]
        public void CoreProductionAssemblies_DoNotReferenceEnviroDirectly()
        {
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            string[] paths =
            {
                "Assets/Game/Bootstrap/MSC.Bootstrap.Runtime.asmdef",
                "Assets/Game/Weather/Production/Runtime/MSC.Weather.Production.Runtime.asmdef",
                "Assets/Game/Weather/Production/LegacyBaseline/Runtime/MSC.Weather.Production.LegacyBaseline.Runtime.asmdef",
            };

            for (int index = 0; index < paths.Length; index++)
            {
                string text = File.ReadAllText(
                    Path.Combine(projectRoot, paths[index]));
                Assert.That(
                    text,
                    Does.Not.Contain("Enviro3.Runtime"),
                    paths[index]);
            }
        }

        [Test]
        public void ShelterRemovalBridge_TilesProjectBoundsWithoutExteriorOverhang()
        {
            var volume = new ShelterVolume(
                "weather.shelter.test",
                new Vector3(2f, 3f, 4f),
                new Vector3(5f, 2f, 3f),
                SurfaceExposureProfile.Interior);

            Enviro3ShelterRemovalZoneLayout[] layout =
                Enviro3ShelterRemovalBridge.CalculateZoneLayout(volume);
            Enviro3ShelterRemovalZoneLayout[] repeated =
                Enviro3ShelterRemovalBridge.CalculateZoneLayout(volume);

            Assert.That(layout, Has.Length.EqualTo(2));
            Assert.That(repeated, Has.Length.EqualTo(layout.Length));
            for (int index = 0; index < layout.Length; index++)
            {
                Vector3 local = layout[index].Center - volume.Center;
                Assert.That(
                    Mathf.Abs(local.x) + layout[index].Radius,
                    Is.LessThanOrEqualTo(volume.Extents.x + 0.0001f));
                Assert.That(
                    Mathf.Abs(local.z) + layout[index].Radius,
                    Is.LessThanOrEqualTo(volume.Extents.z + 0.0001f));
                Assert.That(layout[index].Radius, Is.EqualTo(3f));
                Assert.That(
                    layout[index].Stretch,
                    Is.EqualTo(1f).Within(0.0001f));
                Assert.That(repeated[index].Center, Is.EqualTo(layout[index].Center));
                Assert.That(repeated[index].Radius, Is.EqualTo(layout[index].Radius));
                Assert.That(repeated[index].Stretch, Is.EqualTo(layout[index].Stretch));
            }
        }

        [Test]
        public void ShelterRemovalBridge_CoversObservedLivingRoomRainLeakProbes()
        {
            var house = new ShelterVolume(
                "weather.shelter.home.house.interior.v1",
                new Vector3(162.1201f, 2.150744f, -1033.450806f),
                new Vector3(6.081711f, 1.063561f, 7.437134f),
                SurfaceExposureProfile.Interior);

            Enviro3ShelterRemovalZoneLayout[] layout =
                Enviro3ShelterRemovalBridge.CalculateZoneLayout(house);
            var farChairStandingEye =
                new Vector3(167.41089f, 2.717f, -1034.4824f);
            var aboveTelevisionAtCeiling =
                new Vector3(165.29572f, 3.479f, -1035.0538f);

            Assert.That(
                IsInsideAnyRemovalEllipsoid(
                    layout,
                    farChairStandingEye),
                Is.True,
                "The observed far-chair standing-eye probe must remain dry.");
            Assert.That(
                IsInsideAnyRemovalEllipsoid(
                    layout,
                    aboveTelevisionAtCeiling),
                Is.True,
                "The observed ceiling path above the television must remain dry.");
        }

        [Test]
        public void BootstrapEnvironment_HasAuditedHomeHouseAndGarageShelters()
        {
            Scene scene = EditorSceneManager.OpenScene(
                ProductionEnvironmentBuilder.BootstrapScenePath,
                OpenSceneMode.Single);
            ProductionShelterVolumeAuthoring[] authored =
                FindAll<ProductionShelterVolumeAuthoring>(scene);

            Assert.That(
                authored,
                Has.Length.EqualTo(
                    ProductionShelterMeasurementTool.ExpectedShelterCount));
            Assert.That(
                authored.Select(value => value.StableId),
                Is.EquivalentTo(new[]
                {
                    ProductionShelterMeasurementTool
                        .HomeHouseShelterStableId,
                    ProductionShelterMeasurementTool
                        .HomeGarageShelterStableId,
                }));
            for (int index = 0; index < authored.Length; index++)
            {
                Assert.That(
                    authored[index].ShelterKind,
                    Is.EqualTo(ProductionShelterKind.Interior));
                Assert.That(
                    authored[index].TryCreateVolume(
                        out ShelterVolume volume,
                        out string failure),
                    Is.True,
                    failure);
                Assert.That(volume.Extents.x, Is.GreaterThan(0.25f));
                Assert.That(volume.Extents.y, Is.GreaterThan(0.25f));
                Assert.That(volume.Extents.z, Is.GreaterThan(0.25f));
            }

            WeatherZone houseZone = FindAll<WeatherZone>(scene).Single(
                value => value.StableId ==
                    ProductionShelterMeasurementTool.HomeHouseShelterStableId);
            Assert.That(houseZone.VolumeColliders.Count, Is.EqualTo(3));
            Assert.That(
                houseZone.VolumeColliders.All(
                    value => value is BoxCollider box &&
                             box.enabled && box.isTrigger),
                Is.True);
            Assert.That(
                houseZone.ContainsAuthoredGeometry(
                    new Vector3(162.06999f, 3.8f, -1037.315f)),
                Is.True,
                "The bedroom/elevated-camera probe must remain indoors.");
            Assert.That(
                houseZone.ContainsAuthoredGeometry(
                    new Vector3(165.29572f, 3.8f, -1035.0538f)),
                Is.True,
                "The elevated living-room probe must remain indoors.");
        }

        [Test]
        public void ShelterMeasurement_DerivesBoundedInsetsFromFrozenSourceAabbs()
        {
            ProductionShelterMeasurementTool.ShelterMeasurementRecord[] derived =
                ProductionShelterMeasurementTool.DeriveSheltersOrThrow(new[]
                {
                    Record(
                        "42aa8c997b8afde59d68e3ee7f9f73b7",
                        new Vector3(0f, 1f, 0f),
                        new Vector3(2f, 0.01f, 2f)),
                    Record(
                        "f818a8da820952a72d428d74815488b4",
                        new Vector3(3f, 1f, 0f),
                        new Vector3(1f, 0.01f, 1f)),
                    Record(
                        "2b391ab6d5dd0500a5a06391e45b7150",
                        new Vector3(0f, 1f, 3f),
                        new Vector3(1f, 0.01f, 1f)),
                    Record(
                        "a71d9deddebb37f4a37bd02b13cef9f9",
                        new Vector3(0f, 3.1f, 0f),
                        new Vector3(2f, 0.1f, 2f)),
                    Record(
                        "3f48c3f8da113c762f5ef4d32409e45c",
                        new Vector3(0f, 3.2f, 0f),
                        new Vector3(2f, 0.1f, 2f)),
                    Record(
                        "419f49d30da6bff0fcfa679c84d652ce",
                        new Vector3(10f, 2f, 0f),
                        new Vector3(2f, 1f, 3f)),
                    Record(
                        "b5e7b987d5aac9da197a6ce662beb64c",
                        new Vector3(10f, 4f, 0f),
                        new Vector3(2f, 0.5f, 3f)),
                });

            Assert.That(derived, Has.Length.EqualTo(2));
            Assert.That(
                derived[0].stableId,
                Is.EqualTo(
                    ProductionShelterMeasurementTool
                        .HomeHouseShelterStableId));
            Assert.That(
                derived[0].center,
                Is.EqualTo(new Vector3(1f, 1.98f, 1f))
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(
                derived[0].extents,
                Is.EqualTo(new Vector3(2.9f, 0.92f, 2.9f))
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(
                derived[1].stableId,
                Is.EqualTo(
                    ProductionShelterMeasurementTool
                        .HomeGarageShelterStableId));
            Assert.That(
                derived[1].center,
                Is.EqualTo(new Vector3(10f, 2.225f, 0f))
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
            Assert.That(
                derived[1].extents,
                Is.EqualTo(new Vector3(1.9f, 1.175f, 2.9f))
                    .Using(Vector3ComparerWithEqualsOperator.Instance));
        }

        [Test]
        public void ShelterMeasurement_GeometryFingerprintIsStableAndDetectsBoundsDrift()
        {
            string json = File.ReadAllText(
                ProductionShelterMeasurementTool.EvidenceAssetPath);
            ProductionShelterMeasurementTool.MeasurementReport evidence =
                JsonUtility.FromJson<
                    ProductionShelterMeasurementTool.MeasurementReport>(json);
            string expected =
                ProductionShelterMeasurementTool
                    .ExpectedSourceGeometryFingerprintSha256;

            Assert.That(
                ProductionShelterMeasurementTool
                    .ComputeGeometryFingerprintSha256(evidence.records),
                Is.EqualTo(expected));
            Assert.That(
                ProductionShelterMeasurementTool
                    .ComputeGeometryFingerprintSha256(
                        evidence.records.Reverse()),
                Is.EqualTo(expected),
                "Fingerprint ordering must not depend on scene traversal order.");

            evidence.records[0].extents.x += 0.01f;
            Assert.That(
                ProductionShelterMeasurementTool
                    .ComputeGeometryFingerprintSha256(evidence.records),
                Is.Not.EqualTo(expected));

            evidence = JsonUtility.FromJson<
                ProductionShelterMeasurementTool.MeasurementReport>(json);
            evidence.records[0].sourceHierarchyPath += "/drift";
            Assert.That(
                ProductionShelterMeasurementTool
                    .ComputeGeometryFingerprintSha256(evidence.records),
                Is.Not.EqualTo(expected));

            evidence = JsonUtility.FromJson<
                ProductionShelterMeasurementTool.MeasurementReport>(json);
            evidence.records[0].stableId += ".drift";
            Assert.That(
                ProductionShelterMeasurementTool
                    .ComputeGeometryFingerprintSha256(evidence.records),
                Is.Not.EqualTo(expected));
        }

        [Test]
        public void ShelterMeasurement_GeometryFingerprintRejectsIncompleteOrDuplicateRecords()
        {
            string json = File.ReadAllText(
                ProductionShelterMeasurementTool.EvidenceAssetPath);
            ProductionShelterMeasurementTool.MeasurementReport evidence =
                JsonUtility.FromJson<
                    ProductionShelterMeasurementTool.MeasurementReport>(json);

            Assert.Throws<InvalidOperationException>(() =>
                ProductionShelterMeasurementTool
                    .ComputeGeometryFingerprintSha256(
                        evidence.records.Take(
                            evidence.records.Length - 1)));

            ProductionShelterMeasurementTool.MeasurementRecord[] duplicate =
                evidence.records.ToArray();
            duplicate[0] = duplicate[1];
            Assert.Throws<InvalidOperationException>(() =>
                ProductionShelterMeasurementTool
                    .DeriveSheltersOrThrow(duplicate));
        }

        [Test]
        public void ShelterMeasurement_FullSceneShaIsAuditOnlyButMustBeWellFormed()
        {
            string json = File.ReadAllText(
                ProductionShelterMeasurementTool.EvidenceAssetPath);
            ProductionShelterMeasurementTool.MeasurementReport evidence =
                JsonUtility.FromJson<
                    ProductionShelterMeasurementTool.MeasurementReport>(json);

            evidence.sourceSceneSha256 = new string('a', 64);
            Assert.DoesNotThrow(() =>
                ProductionShelterMeasurementTool
                    .ValidateReportAgainstFrozenSourceOrThrow(evidence));

            evidence.sourceSceneSha256 = "malformed";
            Assert.Throws<InvalidOperationException>(() =>
                ProductionShelterMeasurementTool
                    .ValidateReportAgainstFrozenSourceOrThrow(evidence));
        }

        private static ProductionShelterMeasurementTool.MeasurementRecord Record(
            string stableId,
            Vector3 center,
            Vector3 extents) =>
            new ProductionShelterMeasurementTool.MeasurementRecord
            {
                stableId = stableId,
                center = center,
                extents = extents,
            };

        private static T[] FindAll<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .ToArray();

        private static bool IsActive(Component component) =>
            component.gameObject.activeInHierarchy &&
            (!(component is Behaviour behaviour) || behaviour.enabled);

        private static bool IsInsideAnyRemovalEllipsoid(
            IReadOnlyList<Enviro3ShelterRemovalZoneLayout> layout,
            Vector3 point)
        {
            for (int index = 0; index < layout.Count; index++)
            {
                Enviro3ShelterRemovalZoneLayout zone = layout[index];
                Vector3 delta = point - zone.Center;
                float verticalRadius = zone.Radius * zone.Stretch;
                float normalized =
                    (delta.x * delta.x + delta.z * delta.z) /
                    (zone.Radius * zone.Radius) +
                    delta.y * delta.y /
                    (verticalRadius * verticalRadius);
                if (normalized <= 1f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
