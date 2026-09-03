using System;
using System.Collections.Generic;
using System.Linq;
using Enviro;
using MSC.Bootstrap;
using MSC.Weather.Production;
using MSC.Weather.Production.LegacyBaseline;
using MSC.Weather.Wetness;
using MSC.World.Streaming;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using GameWeatherSystem = MSC.Weather.System.GameWeatherSystem;
using NativeHDRPWeatherBackend =
    MSC.Weather.System.NativeHDRP.NativeHDRPWeatherBackend;
using WeatherBackendType = MSC.Weather.System.WeatherBackendType;

namespace MSC.Weather.Enviro3Integration.Editor
{
    public static class ProductionEnvironmentValidator
    {
        private const string MenuPath =
            "Tools/MSC Remake/Production Weather/Validate Bootstrap Environment";

        [MenuItem(MenuPath)]
        public static void ValidateOrThrow()
        {
            Scene scene = EditorSceneManager.OpenScene(
                ProductionEnvironmentBuilder.BootstrapScenePath,
                OpenSceneMode.Single);
            ValidateSceneOrThrow(scene);
            Debug.Log(
                "M07C_PRODUCTION_ENVIRONMENT_VALIDATION_PASS " +
                "owners=1 bindings=valid " +
                "activeWorld=donor-feature-parity-06b2 " +
                "privateBuildGuard=validated");
        }

        public static void ValidateProjectBoundaryOrThrow()
        {
            Scene scene = EditorSceneManager.OpenScene(
                ProductionEnvironmentBuilder.BootstrapScenePath,
                OpenSceneMode.Single);
            ProductionWorldStreamingInstaller installer =
                FindSingleActive<ProductionWorldStreamingInstaller>(scene);
            ValidateActiveWorldProfile(installer);
            ValidateBuildSettings();
        }

        internal static void ValidateSceneOrThrow(Scene scene)
        {
            EnviroManager manager = FindSingleTotal<EnviroManager>(scene);
            Enviro3EnvironmentAdapter adapter =
                FindSingleTotal<Enviro3EnvironmentAdapter>(scene);
            Enviro3ShelterRemovalBridge shelterBridge =
                FindSingleTotal<Enviro3ShelterRemovalBridge>(scene);
            ProductionEnvironmentController environment =
                FindSingleActive<ProductionEnvironmentController>(scene);
            DonorWorldLegacyWetnessBridge wetness =
                FindSingleActive<DonorWorldLegacyWetnessBridge>(scene);
            GameCompositionRoot root =
                FindSingleActive<GameCompositionRoot>(scene);
            ProductionWorldStreamingInstaller installer =
                FindSingleActive<ProductionWorldStreamingInstaller>(scene);
            ProductionEnvironmentBackendActivator activator =
                FindSingleActive<ProductionEnvironmentBackendActivator>(scene);
            ProductionEnvironmentBackendMarker marker =
                FindSingleTotal<ProductionEnvironmentBackendMarker>(scene);
            GameWeatherSystem[] weatherRouters =
                FindAll<GameWeatherSystem>(scene);
            if (weatherRouters.Length > 1)
            {
                throw new InvalidOperationException(
                    "Expected at most one GameWeatherSystem route; found " +
                    weatherRouters.Length + ".");
            }

            GameWeatherSystem weatherRouter = weatherRouters.Length == 1
                ? weatherRouters[0]
                : null;
            WindZone[] windZones = FindAll<WindZone>(scene);
            if (windZones.Length != 0)
            {
                throw new InvalidOperationException(
                    "Inactive canonical Enviro prefab must not serialize a " +
                    "runtime-created WindZone; " +
                    $"found {windZones.Length}.");
            }

            if (!marker.IsValid ||
                marker.gameObject.activeSelf ||
                activator.BackendMarker != marker ||
                !activator.HasValidBinding ||
                !activator.IsBackendAuthoredInactive)
            {
                throw new InvalidOperationException(
                    "Production environment backend ownership/activation " +
                    "binding is invalid or authored active.");
            }

            if (environment.gameObject != root.gameObject ||
                wetness.gameObject != root.gameObject ||
                installer.gameObject != root.gameObject ||
                installer.Environment != environment ||
                installer.StartupMode !=
                    ProductionWorldStartupMode.ProductionEnvironmentRequired)
            {
                throw new InvalidOperationException(
                    "Production environment lifetime is not rooted in Bootstrap.");
            }

            if (!wetness.CoverageProfileIsValid ||
                wetness.CoverageProfile == null ||
                wetness.CoverageProfile.EntryCount != 20 ||
                !string.Equals(
                    AssetDatabase.GetAssetPath(wetness.CoverageProfile),
                    ProductionEnvironmentBuilder.LegacyWetnessCoveragePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Production legacy wetness coverage is missing or not the reviewed 20-entry allowlist.");
            }

            if (!Mathf.Approximately(
                    wetness.OpaqueWetSmoothness,
                    DonorWorldLegacyWetnessBridge
                        .ProductionOpaqueWetSmoothness) ||
                !Mathf.Approximately(
                    wetness.AlphaClipWetSmoothness,
                    DonorWorldLegacyWetnessBridge
                        .ProductionAlphaClipWetSmoothness))
            {
                throw new InvalidOperationException(
                    "Production legacy wetness smoothness caps do not match the reviewed temporary-baseline policy.");
            }

            if (!marker.transform.IsChildOf(root.transform) ||
                !adapter.transform.IsChildOf(marker.transform) ||
                !shelterBridge.transform.IsChildOf(marker.transform) ||
                manager.gameObject != adapter.gameObject)
            {
                throw new InvalidOperationException(
                    "Enviro manager/adapter is outside the persistent Bootstrap root.");
            }

            string prefabPath =
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    manager.gameObject);
            if (!PrefabUtility.IsPartOfPrefabInstance(manager.gameObject) ||
                !string.Equals(
                    prefabPath,
                    ProductionEnvironmentBuilder.EnviroSourcePrefabPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Inactive production Enviro instance is not linked to the " +
                    "canonical vendor prefab.");
            }

            ValidateVolume(manager, adapter);
            ValidateMeasuredShelters(scene, root.transform);
            if (weatherRouter != null &&
                weatherRouter.SelectedBackend == WeatherBackendType.NativeHDRP)
            {
                ValidateNativeWeatherRoute(
                    scene,
                    root,
                    environment,
                    manager,
                    adapter,
                    weatherRouter);
            }
            else if (!adapter.HybridNativeHdrpOwnership)
            {
                ValidateNoLegacyPlaceholderOwners(scene, manager.transform);
            }

            ValidateBindings();
            ValidateActiveWorldProfile(installer);
            ValidateBuildSettings();
        }

        public static void RunBatch()
        {
            ValidateOrThrow();
        }

        private static void ValidateVolume(
            EnviroManager manager,
            Enviro3EnvironmentAdapter adapter)
        {
            if (manager.volumeHDRP == null ||
                !manager.volumeHDRP.isGlobal ||
                manager.volumeHDRP.sharedProfile == null)
            {
                throw new InvalidOperationException(
                    "Enviro has no single global production HDRP volume.");
            }

            string path = AssetDatabase.GetAssetPath(
                manager.volumeHDRP.sharedProfile);
            VolumeProfile profile = manager.volumeHDRP.sharedProfile;
            if (adapter.HybridNativeHdrpOwnership)
            {
                if (!string.Equals(
                        path,
                        HybridEnvironmentMigrationTool.EnviroSkyProfilePath,
                        StringComparison.Ordinal) ||
                    !profile.TryGet(out VisualEnvironment hybridVisual) ||
                    hybridVisual.skyType.value != 990 ||
                    !profile.TryGet(out EnviroHDRPSky _) ||
                    profile.TryGet(out Fog _) ||
                    profile.TryGet(out Exposure _) ||
                    profile.TryGet(out IndirectLightingController _))
                {
                    throw new InvalidOperationException(
                        "Preserved Enviro rollback owner does not use the " +
                        "reviewed sky-only hybrid profile.");
                }

                return;
            }

            if (!string.Equals(
                    path,
                    ProductionEnvironmentBuilder.VolumeProfilePath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Enviro uses a non-production HDRP volume profile.");
            }

            if (!profile.TryGet(out VisualEnvironment visual) ||
                visual.skyType.value != 990 ||
                !profile.TryGet(out EnviroHDRPSky _) ||
                !profile.TryGet(out Fog fog) ||
                !fog.enabled.value ||
                !fog.enableVolumetricFog.value ||
                fog.meanFreePath.value <
                    Enviro3ProductionVisualPolicy
                        .MinimumFogMeanFreePathMeters ||
                !profile.TryGet(out Exposure exposure) ||
                exposure.mode.value != ExposureMode.Fixed ||
                !profile.TryGet(
                    out IndirectLightingController indirectLighting) ||
                !indirectLighting.active ||
                !indirectLighting
                    .indirectDiffuseLightingMultiplier.overrideState ||
                !Mathf.Approximately(
                    indirectLighting.indirectDiffuseLightingMultiplier.value,
                    Enviro3ProductionVisualPolicy
                        .NeutralIndirectLightingMultiplier) ||
                !indirectLighting.reflectionLightingMultiplier.overrideState ||
                !Mathf.Approximately(
                    indirectLighting.reflectionLightingMultiplier.value,
                    Enviro3ProductionVisualPolicy
                        .IndirectReflectionLightingMultiplier) ||
                !indirectLighting
                    .reflectionProbeIntensityMultiplier.overrideState ||
                !Mathf.Approximately(
                    indirectLighting.reflectionProbeIntensityMultiplier.value,
                    Enviro3ProductionVisualPolicy
                        .IndirectReflectionProbeIntensityMultiplier))
            {
                throw new InvalidOperationException(
                    "Production HDRP profile has an invalid " +
                    "Enviro sky/fog/exposure/indirect-lighting stack.");
            }
        }

        private static void ValidateNativeWeatherRoute(
            Scene scene,
            GameCompositionRoot compositionRoot,
            ProductionEnvironmentController environment,
            EnviroManager manager,
            Enviro3EnvironmentAdapter enviroAdapter,
            GameWeatherSystem router)
        {
            if (!router.gameObject.activeInHierarchy || !router.enabled ||
                !router.transform.IsChildOf(compositionRoot.transform))
            {
                throw new InvalidOperationException(
                    "Native GameWeatherSystem is not active under the " +
                    "persistent Bootstrap composition root.");
            }

            SerializedProperty adapterProperty =
                new SerializedObject(environment).FindProperty(
                    "adapterBehaviour");
            if (adapterProperty == null ||
                adapterProperty.objectReferenceValue != router)
            {
                throw new InvalidOperationException(
                    "ProductionEnvironmentController is not routed through " +
                    "GameWeatherSystem.");
            }

            NativeHDRPWeatherBackend[] nativeBackends =
                FindAll<NativeHDRPWeatherBackend>(scene);
            if (nativeBackends.Length != 1 ||
                !nativeBackends[0].gameObject.activeInHierarchy ||
                !nativeBackends[0].enabled)
            {
                throw new InvalidOperationException(
                    "Expected exactly one active NativeHDRPWeatherBackend; " +
                    $"found {nativeBackends.Length}.");
            }

            NativeHDRPWeatherBackend native = nativeBackends[0];
            Volume nativeVolume = native.RuntimeWeatherVolume;
            if (native.gameObject != router.gameObject ||
                nativeVolume == null || !nativeVolume.isGlobal ||
                nativeVolume.enabled || nativeVolume.weight > 0f ||
                nativeVolume.sharedProfile == null ||
                !string.Equals(
                    AssetDatabase.GetAssetPath(nativeVolume.sharedProfile),
                    "Assets/Game/Weather/System/Content/FinnishSummer/" +
                    "NativeHDRPWeatherVolume.asset",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Native HDRP weather Volume is missing, active before " +
                    "attachment, or uses the wrong authored profile.");
            }

            if (native.DirectionalSun == null ||
                native.DirectionalSun.type != LightType.Directional ||
                native.DirectionalSunData == null ||
                native.Geography == null ||
                !HasAssignedParticleSystem(native.RainSystems) ||
                !HasAssignedParticleSystem(native.DrizzleSystems) ||
                native.LightningFlashLight == null)
            {
                throw new InvalidOperationException(
                    "Native HDRP weather presentation is missing its sun, " +
                    "geography, rain, drizzle, or lightning authoring.");
            }

            if (!ContainsReference(
                    native.LegacyVolumesToSuspend,
                    manager.volumeHDRP) ||
                !ContainsReference(
                    native.LegacyOwnersToSuspend,
                    enviroAdapter) ||
                !ContainsReference(
                    native.LegacyOwnersToSuspend,
                    manager))
            {
                throw new InvalidOperationException(
                    "Native HDRP rollback does not retain and suspend the " +
                    "preserved Enviro owners.");
            }

            foreach (Volume candidate in FindAll<Volume>(scene))
            {
                if (candidate == nativeVolume ||
                    !candidate.isActiveAndEnabled || !candidate.isGlobal ||
                    candidate.weight <= 0f ||
                    !ContainsWeatherOverride(candidate.sharedProfile))
                {
                    continue;
                }

                if (!ContainsReference(
                        native.LegacyVolumesToSuspend,
                        candidate))
                {
                    throw new InvalidOperationException(
                        "An authored global weather Volume is not included " +
                        "in native rollback suspension: " + candidate.name);
                }
            }

            foreach (Light candidate in FindAll<Light>(scene))
            {
                if (!candidate.isActiveAndEnabled ||
                    candidate.type != LightType.Directional ||
                    candidate == native.DirectionalSun ||
                    candidate.name.IndexOf(
                        "moon",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                throw new InvalidOperationException(
                    "An active directional-light owner exists outside the " +
                    "selected native sun: " + candidate.name);
            }
        }

        private static bool HasAssignedParticleSystem(
            IReadOnlyList<ParticleSystem> systems)
        {
            if (systems == null)
            {
                return false;
            }

            for (int index = 0; index < systems.Count; index++)
            {
                if (systems[index] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsReference<T>(
            IReadOnlyList<T> values,
            T expected)
            where T : UnityEngine.Object
        {
            if (values == null || expected == null)
            {
                return false;
            }

            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] == expected)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsWeatherOverride(VolumeProfile profile)
        {
            if (profile == null)
            {
                return false;
            }

            IReadOnlyList<VolumeComponent> components = profile.components;
            for (int index = 0; index < components.Count; index++)
            {
                string typeName = components[index]?.GetType().Name ??
                    string.Empty;
                if (typeName == "VisualEnvironment" ||
                    typeName == "PhysicallyBasedSky" ||
                    typeName == "EnviroHDRPSky" ||
                    typeName == "VolumetricClouds" || typeName == "Fog" ||
                    typeName == "Exposure" ||
                    typeName == "IndirectLightingController")
                {
                    return true;
                }
            }

            return false;
        }

        private static void ValidateNoLegacyPlaceholderOwners(
            Scene scene,
            Transform enviroRoot)
        {
            foreach (Light light in FindAll<Light>(scene))
            {
                if (light.isActiveAndEnabled &&
                    light.type == LightType.Directional &&
                    !light.transform.IsChildOf(enviroRoot))
                {
                    throw new InvalidOperationException(
                        "An active directional-light owner exists outside Enviro: " +
                        light.name);
                }
            }

            foreach (Volume volume in FindAll<Volume>(scene))
            {
                if (volume.isActiveAndEnabled &&
                    volume.isGlobal &&
                    volume != enviroRoot.GetComponentInChildren<Volume>(true))
                {
                    throw new InvalidOperationException(
                        "An active global volume owner exists outside Enviro: " +
                        volume.name);
                }
            }
        }

        private static void ValidateMeasuredShelters(
            Scene scene,
            Transform compositionRoot)
        {
            TextAsset evidenceAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                ProductionShelterMeasurementTool.EvidenceAssetPath);
            if (evidenceAsset == null)
            {
                throw new InvalidOperationException(
                    "Production shelter measurement evidence is missing: " +
                    ProductionShelterMeasurementTool.EvidenceAssetPath);
            }

            ProductionShelterMeasurementTool.MeasurementReport evidence =
                JsonUtility.FromJson<
                    ProductionShelterMeasurementTool.MeasurementReport>(
                    evidenceAsset.text);
            ProductionShelterMeasurementTool
                .ValidateReportAgainstFrozenSourceOrThrow(evidence);

            ProductionShelterVolumeAuthoring[] authored =
                FindAll<ProductionShelterVolumeAuthoring>(scene);
            if (authored.Length !=
                ProductionShelterMeasurementTool.ExpectedShelterCount)
            {
                throw new InvalidOperationException(
                    "Expected exactly two measured production shelter volumes; " +
                    $"found {authored.Length}.");
            }

            var byStableId = new Dictionary<
                string,
                ProductionShelterVolumeAuthoring>(StringComparer.Ordinal);
            for (int index = 0; index < authored.Length; index++)
            {
                ProductionShelterVolumeAuthoring authoring = authored[index];
                if (!authoring.transform.IsChildOf(compositionRoot) ||
                    authoring.ShelterKind != ProductionShelterKind.Interior)
                {
                    throw new InvalidOperationException(
                        "Production shelter is outside the Bootstrap owner or is not Interior.");
                }

                if (!authoring.TryCreateVolume(
                        out ShelterVolume volume,
                        out string failure))
                {
                    throw new InvalidOperationException(
                        "Invalid production shelter volume: " + failure);
                }

                if (!byStableId.TryAdd(volume.StableId, authoring))
                {
                    throw new InvalidOperationException(
                        "Duplicate production shelter stable ID: " +
                        volume.StableId);
                }
            }

            for (int index = 0; index < evidence.shelters.Length; index++)
            {
                ProductionShelterMeasurementTool.ShelterMeasurementRecord
                    expected = evidence.shelters[index];
                if (!byStableId.TryGetValue(
                        expected.stableId,
                        out ProductionShelterVolumeAuthoring authoring))
                {
                    throw new InvalidOperationException(
                        "Expected measured production shelter is missing: " +
                        expected.stableId);
                }

                if (!authoring.TryCreateVolume(
                        out ShelterVolume actual,
                        out string failure))
                {
                    throw new InvalidOperationException(
                        "Expected measured production shelter is invalid: " +
                        expected.stableId + " " + failure);
                }

                if ((actual.Center - expected.center).sqrMagnitude >
                        0.000001f ||
                    (actual.Extents - expected.extents).sqrMagnitude >
                        0.000001f)
                {
                    throw new InvalidOperationException(
                        "Production shelter bounds do not match audited evidence: " +
                        expected.stableId);
                }
            }
        }

        private static void ValidateBindings()
        {
            Enviro3EnvironmentBindings bindings =
                AssetDatabase.LoadAssetAtPath<Enviro3EnvironmentBindings>(
                    ProductionEnvironmentBuilder.BindingsAssetPath);
            if (bindings == null || bindings.SourceConfiguration == null ||
                bindings.EffectsSource == null || bindings.Clear == null ||
                bindings.PartlyCloudy == null || bindings.Overcast == null ||
                bindings.Rain == null || bindings.Storm == null ||
                bindings.Fog == null || bindings.Low == null ||
                bindings.Medium == null || bindings.High == null)
            {
                throw new InvalidOperationException(
                    "Production Enviro binding asset is incomplete.");
            }
        }

        private static void ValidateBuildSettings()
        {
            string[] paths = EditorBuildSettings.scenes
                .Where(value => value.enabled)
                .Select(value => value.path.Replace('\\', '/'))
                .ToArray();
            if (!paths.Contains(
                    ProductionEnvironmentBuilder.BootstrapScenePath,
                    StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    "Production Bootstrap is not enabled in Build Settings.");
            }

            for (int index = 0; index < paths.Length; index++)
            {
                string path = paths[index];
                if (path.IndexOf("WeatherLab", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.StartsWith(
                        "Assets/Enviro 3 - Sky and Weather/",
                        StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith(
                        "Assets/Enviro 3 - Additional Weather Pack/",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Development/vendor weather scene is enabled for build: " + path);
                }
            }
        }

        private static void ValidateActiveWorldProfile(
            ProductionWorldStreamingInstaller installer)
        {
            ProductionWorldStreamingManifest manifest =
                installer.WorldStreaming != null
                    ? installer.WorldStreaming.Manifest
                    : null;
            if (manifest == null ||
                !string.Equals(
                    manifest.ProfileId,
                    "donor-feature-parity-06b2",
                    StringComparison.Ordinal) ||
                manifest.ProfileKind !=
                    ProductionWorldProfileKind.DonorFeatureParity ||
                !manifest.PrivateLocalRuntimeBaseline ||
                manifest.GlobalScenes.Count != 1 ||
                manifest.Cells.Count != 49)
            {
                throw new InvalidOperationException(
                    "Production weather is not bound to the frozen private " +
                    "donor-feature-parity world profile.");
            }

            IReadOnlyList<string> configurationErrors =
                manifest.ValidateConfiguration();
            if (configurationErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    "Active donor feature-parity manifest is invalid: " +
                    string.Join(" | ", configurationErrors));
            }

            for (int index = 0;
                 index < manifest.GlobalScenes.Count;
                 index++)
            {
                ProductionWorldGlobalScene globalScene =
                    manifest.GlobalScenes[index];
                ValidateWorldBuildEntry(
                    globalScene.BuildIndex,
                    globalScene.ScenePath,
                    globalScene.SceneId);
            }

            for (int index = 0;
                 index < manifest.Cells.Count;
                 index++)
            {
                ProductionWorldCellScene cell = manifest.Cells[index];
                ValidateWorldBuildEntry(
                    cell.BuildIndex,
                    cell.ScenePath,
                    cell.CellId);
            }

            IEnumerable<string> activeWorldPaths =
                manifest.GlobalScenes.Select(value => value.ScenePath)
                    .Concat(manifest.Cells.Select(value => value.ScenePath));
            foreach (string path in activeWorldPaths)
            {
                if (path.IndexOf(
                        "PrototypeWorldStreamingFixture",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf(
                        "Production_cell_0_-3",
                        StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf(
                        "Production_cell_0_-2",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    throw new InvalidOperationException(
                        "Rejected prototype visual content is active in the " +
                        "donor feature-parity profile: " + path);
                }
            }
        }

        private static void ValidateWorldBuildEntry(
            int buildIndex,
            string expectedPath,
            string label)
        {
            string actualPath =
                SceneUtility.GetScenePathByBuildIndex(buildIndex);
            if (!string.Equals(
                    actualPath,
                    expectedPath,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Active world entry {label} expects build index " +
                    $"{buildIndex} to resolve to {expectedPath}, but it " +
                    $"resolves to {actualPath ?? "<null>"}.");
            }
        }

        private static T FindSingleActive<T>(Scene scene)
            where T : Component
        {
            T[] allValues = FindAll<T>(scene);
            if (allValues.Length != 1 ||
                !allValues[0].gameObject.activeInHierarchy ||
                (allValues[0] is Behaviour behaviour && !behaviour.enabled))
            {
                throw new InvalidOperationException(
                    $"Expected exactly one total active {typeof(T).Name}; " +
                    $"found {allValues.Length}.");
            }

            return allValues[0];
        }

        private static T FindSingleTotal<T>(Scene scene)
            where T : Component
        {
            T[] values = FindAll<T>(scene);
            if (values.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one total {typeof(T).Name}; " +
                    $"found {values.Length}.");
            }

            return values[0];
        }

        private static T[] FindAll<T>(Scene scene) where T : Component
        {
            var values = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                values.AddRange(roots[index].GetComponentsInChildren<T>(true));
            }

            return values.ToArray();
        }
    }
}
