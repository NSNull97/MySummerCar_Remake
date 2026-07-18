using System;
using System.Collections.Generic;
using System.IO;
using Enviro;
using MSC.Bootstrap;
using MSC.Weather.Presentation;
using MSC.Weather.Production;
using MSC.Weather.Production.LegacyBaseline;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Weather.Enviro3Integration.Editor
{
    /// <summary>
    /// Deterministic, reversible production migration for the persistent Bootstrap
    /// environment. It never edits vendor assets or generated donor scenes.
    /// </summary>
    public static class ProductionEnvironmentBuilder
    {
        public const string BuilderVersion = "1.0.2";
        public const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        public const string BindingsAssetPath =
            "Assets/Game/Weather/Production/Content/Profiles/ProductionEnviro3Bindings.asset";
        public const string VolumeProfilePath =
            "Assets/Game/Weather/Production/Content/Profiles/ProductionEnvironmentHDRPVolume.asset";
        public const string LegacyWetnessCoveragePath =
            "Assets/Game/Weather/Production/Content/Profiles/ProductionLegacyWetnessCoverage.asset";

        private const string MenuRoot =
            "Tools/MSC Remake/Production Weather/";
        private const string VendorRoot =
            "Assets/Enviro 3 - Sky and Weather";
        public const string EnviroSourcePrefabPath =
            VendorRoot + "/Enviro 3.prefab";
        private const string ConfigurationPath =
            VendorRoot + "/Profiles/Configurations/Default Enviro Configuration.asset";
        private const string EffectsPath =
            VendorRoot + "/Scripts/Runtime/Modules/Effects/Preset/Default Effects Preset.asset";
        private const string WeatherPath =
            VendorRoot + "/Profiles/Weather Types/";
        private const string QualityPath =
            VendorRoot + "/Profiles/Quality/";
        private const string BackendName = "ProductionEnvironmentBackend";

        [MenuItem(MenuRoot + "Build or Rebuild Bootstrap Environment")]
        public static void Build()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("Production weather build cancelled.");
                return;
            }

            Enviro3PreflightValidator.ValidateOrThrow();
            ProductionEnvironmentValidator.ValidateProjectBoundaryOrThrow();
            RequireAsset<GameObject>(EnviroSourcePrefabPath);
            EnsureAssetFolders();
            VolumeProfile volumeProfile = EnsureVolumeProfile();
            Enviro3EnvironmentBindings bindings = EnsureBindings();
            LegacyWetnessCoverageProfile wetnessCoverage =
                EnsureLegacyWetnessCoverage();
            ProductionShelterMeasurementTool.MeasurementReport
                shelterMeasurements =
                    ProductionShelterMeasurementTool.MeasureOrThrow();

            Scene scene = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            GameCompositionRoot compositionRoot =
                FindSingleInScene<GameCompositionRoot>(scene);
            ProductionWorldStreamingInstaller installer =
                compositionRoot.GetComponent<ProductionWorldStreamingInstaller>();
            if (installer == null)
            {
                throw new InvalidOperationException(
                    "Bootstrap root has no production world streaming installer.");
            }

            string bootstrapFullPath = Path.GetFullPath(BootstrapScenePath);
            byte[] bootstrapBackup = File.ReadAllBytes(bootstrapFullPath);
            try
            {
            RemoveOwnedBackend(compositionRoot.transform);
            DisableFoundationPlaceholders(scene);

            GameObject backend = new GameObject(BackendName);
            backend.transform.SetParent(compositionRoot.transform, false);
            backend.SetActive(false);
            ProductionEnvironmentBackendMarker backendMarker =
                backend.AddComponent<ProductionEnvironmentBackendMarker>();
            backendMarker.ConfigureForAuthoring();
            ProductionEnvironmentBackendActivator backendActivator =
                compositionRoot.GetComponent<
                    ProductionEnvironmentBackendActivator>() ??
                compositionRoot.gameObject.AddComponent<
                    ProductionEnvironmentBackendActivator>();
            backendActivator.enabled = true;
            backendActivator.ConfigureForAuthoring(backendMarker);
            AuthorProductionShelters(
                backend.transform,
                shelterMeasurements);

            Camera startupCamera = CreateStartupCamera(backend.transform);
            Transform lightningOrigin = CreateAnchor(
                backend.transform,
                "LightningOrigin",
                new Vector3(0f, 80f, 0f));
            Transform lightningTarget = CreateAnchor(
                backend.transform,
                "LightningTarget",
                Vector3.zero);

            GameObject enviroPrefab = RequireAsset<GameObject>(EnviroSourcePrefabPath);
            if (!PrefabUtility.IsPartOfPrefabAsset(enviroPrefab))
            {
                throw new InvalidOperationException(
                    "Enviro source is not imported as a prefab asset: " +
                    PrefabUtility.GetPrefabAssetType(enviroPrefab));
            }
            GameObject enviroObject = (GameObject)PrefabUtility.InstantiatePrefab(
                enviroPrefab,
                backend.transform);
            // EnviroManager.OnEnable intentionally unpacks its own instance in
            // the vendor package. Keep the generated copy inside the single
            // builder-owned backend so rollback remains deterministic without
            // patching vendor source.
            enviroObject.name = "Enviro3_ProductionInstance";
            enviroObject.transform.localPosition = Vector3.zero;
            enviroObject.transform.localRotation = Quaternion.identity;
            enviroObject.transform.localScale = Vector3.one;

            EnviroManager manager = enviroObject.GetComponent<EnviroManager>();
            if (manager == null)
            {
                throw new InvalidOperationException(
                    "Enviro production prefab has no EnviroManager.");
            }

            manager.dontDestroyOnLoad = false;
            manager.ChangeCamera(startupCamera);
            ReplaceEnviroVolume(manager, volumeProfile);

            Enviro3EnvironmentAdapter adapter =
                enviroObject.GetComponent<Enviro3EnvironmentAdapter>() ??
                enviroObject.AddComponent<Enviro3EnvironmentAdapter>();
            adapter.ConfigureForAuthoring(
                manager,
                startupCamera,
                bindings,
                lightningOrigin,
                lightningTarget);
            Enviro3ShelterRemovalBridge shelterRemovalBridge =
                backend.AddComponent<Enviro3ShelterRemovalBridge>();
            shelterRemovalBridge.ConfigureForAuthoring(manager);

            DonorWorldLegacyWetnessBridge wetnessBridge =
                compositionRoot.GetComponent<DonorWorldLegacyWetnessBridge>() ??
                compositionRoot.gameObject.AddComponent<
                    DonorWorldLegacyWetnessBridge>();
            wetnessBridge.enabled = true;
            wetnessBridge.ConfigureForAuthoring(wetnessCoverage);
            ProductionEnvironmentController environment =
                compositionRoot.GetComponent<ProductionEnvironmentController>() ??
                compositionRoot.gameObject.AddComponent<
                    ProductionEnvironmentController>();
            environment.enabled = true;
            environment.ConfigureForAuthoring(
                adapter,
                wetnessBridge,
                EnvironmentQualityTier.Medium,
                19950801UL);
            installer.ConfigureEnvironmentForAuthoring(environment);

            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(adapter);
            EditorUtility.SetDirty(shelterRemovalBridge);
            EditorUtility.SetDirty(backendMarker);
            EditorUtility.SetDirty(backendActivator);
            EditorUtility.SetDirty(wetnessBridge);
            EditorUtility.SetDirty(environment);
            EditorUtility.SetDirty(installer);
            EditorSceneManager.MarkSceneDirty(scene);
            ProductionEnvironmentValidator.ValidateSceneOrThrow(scene);
            if (!EditorSceneManager.SaveScene(scene, BootstrapScenePath))
            {
                throw new InvalidOperationException(
                    "Could not save production Bootstrap environment.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ProductionEnvironmentValidator.ValidateOrThrow();
            Enviro3PreflightValidator.Result preflight =
                Enviro3PreflightValidator.ValidateOrThrow();
            Debug.Log(
                "M07C_PRODUCTION_ENVIRONMENT_BUILD_OK " +
                $"version={BuilderVersion} quality=Medium " +
                $"owner=Bootstrap vendorFiles={preflight.vendorFileCount} " +
                $"vendorFingerprint={preflight.vendorFingerprint}");
            }
            catch
            {
                RestoreBootstrapAfterFailedBuild(
                    bootstrapFullPath,
                    bootstrapBackup);
                throw;
            }
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException(
                    "Production environment batch build requires batch mode.");
            }

            Build();
        }

        private static void EnsureAssetFolders()
        {
            EnsureFolder("Assets/Game/Weather/Production/Content");
            EnsureFolder("Assets/Game/Weather/Production/Content/Profiles");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException(
                    "Invalid production weather asset folder: " + path);
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static VolumeProfile EnsureVolumeProfile()
        {
            VolumeProfile profile =
                AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "ProductionEnvironmentHDRPVolume";
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            profile.components.RemoveAll(component => component == null);
            VisualEnvironment visual =
                GetOrAddVolumeComponent<VisualEnvironment>(profile);
            visual.skyType.Override(990);
            _ = GetOrAddVolumeComponent<EnviroHDRPSky>(profile);
            Fog fog = GetOrAddVolumeComponent<Fog>(profile);
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);
            fog.meanFreePath.Override(
                Enviro3ProductionVisualPolicy.MaximumFogMeanFreePathMeters);
            fog.baseHeight.Override(
                Enviro3ProductionVisualPolicy.FogBaseHeightMeters);
            fog.maximumHeight.Override(
                Enviro3ProductionVisualPolicy.FogMaximumHeightMeters);
            fog.globalLightProbeDimmer.Override(1f);
            Exposure exposure = GetOrAddVolumeComponent<Exposure>(profile);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(10f);

            for (int index = 0; index < profile.components.Count; index++)
            {
                EditorUtility.SetDirty(profile.components[index]);
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAddVolumeComponent<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
            }

            string profilePath = AssetDatabase.GetAssetPath(profile);
            string componentPath = AssetDatabase.GetAssetPath(component);
            if (string.IsNullOrEmpty(componentPath))
            {
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            else if (!string.Equals(
                         componentPath,
                         profilePath,
                         StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Volume component {typeof(T).Name} belongs to another asset.");
            }

            return component;
        }

        private static Enviro3EnvironmentBindings EnsureBindings()
        {
            Enviro3EnvironmentBindings bindings =
                AssetDatabase.LoadAssetAtPath<Enviro3EnvironmentBindings>(
                    BindingsAssetPath);
            if (bindings == null)
            {
                bindings =
                    ScriptableObject.CreateInstance<Enviro3EnvironmentBindings>();
                bindings.name = "ProductionEnviro3Bindings";
                AssetDatabase.CreateAsset(bindings, BindingsAssetPath);
            }

            bindings.ConfigureForAuthoring(
                RequireAsset<EnviroConfiguration>(ConfigurationPath),
                RequireAsset<EnviroEffectsModule>(EffectsPath),
                RequireAsset<EnviroWeatherType>(
                    WeatherPath + "Clear Sky.asset"),
                RequireAsset<EnviroWeatherType>(
                    WeatherPath + "Cloudy 1.asset"),
                RequireAsset<EnviroWeatherType>(
                    WeatherPath + "Cloudy 3.asset"),
                RequireAsset<EnviroWeatherType>(
                    WeatherPath + "Rain.asset"),
                RequireAsset<EnviroWeatherType>(
                    WeatherPath + "Storm.asset"),
                RequireAsset<EnviroWeatherType>(
                    WeatherPath + "Foggy.asset"),
                RequireAsset<EnviroQuality>(QualityPath + "Low.asset"),
                RequireAsset<EnviroQuality>(QualityPath + "Medium.asset"),
                RequireAsset<EnviroQuality>(QualityPath + "High.asset"));
            EditorUtility.SetDirty(bindings);
            return bindings;
        }

        private static LegacyWetnessCoverageProfile
            EnsureLegacyWetnessCoverage()
        {
            LegacyWetnessCoverageProfile profile =
                AssetDatabase.LoadAssetAtPath<
                    LegacyWetnessCoverageProfile>(
                    LegacyWetnessCoveragePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<
                    LegacyWetnessCoverageProfile>();
                profile.name = "ProductionLegacyWetnessCoverage";
                AssetDatabase.CreateAsset(
                    profile,
                    LegacyWetnessCoveragePath);
            }

            // Explicitly reviewed source-material GUIDs from the frozen M06B2
            // manifest. Shared wall/interior materials remain default-deny; the
            // exterior subset is restricted to unambiguously exterior roofs.
            profile.Configure(new[]
            {
                Entry("3053483f32ee213419283150a38bf862", LegacyWetnessSurfaceCategory.Ground), // MUD
                Entry("7a19fb2d522ee4c44a2ae61c01a064fa", LegacyWetnessSurfaceCategory.Ground), // GRASS
                Entry("a0571159690cbed438f5965225dc2df4", LegacyWetnessSurfaceCategory.Ground), // terrain_0
                Entry("da5bc03c62a0f174197555e90357aac9", LegacyWetnessSurfaceCategory.Ground), // TERRAIN
                Entry("2b8378937c6afb64390d5474f4bcd14a", LegacyWetnessSurfaceCategory.Road), // ROAD
                Entry("5cc44389f1f10bf4cabee6d33feb1551", LegacyWetnessSurfaceCategory.Road), // GRAVEL
                Entry("bee65a18eecc0bc409361e160d6b1aca", LegacyWetnessSurfaceCategory.Road), // DIRTROAD
                Entry("e04a30ebc42fb694285479839635f6e3", LegacyWetnessSurfaceCategory.Road), // AIRPORT_road
                Entry("e4f7ddfffd5f47947966d4f2906b4b20", LegacyWetnessSurfaceCategory.Road), // ASPHALT
                Entry("52580e7b1213f134b9d538d61097139e", LegacyWetnessSurfaceCategory.Exterior), // roof_metal 1
                Entry("6523ecc3a1a6530468696a093eccedaf", LegacyWetnessSurfaceCategory.Exterior), // roof_wood2
                Entry("772517eaa38ab4b4ca88da91ff2b6b42", LegacyWetnessSurfaceCategory.Exterior), // tent_roof
                Entry("98f55c71a86a0eb4683e721772261798", LegacyWetnessSurfaceCategory.Exterior), // roof_panel
                Entry("a1f5959787c382a4a890ea8b6542be3a", LegacyWetnessSurfaceCategory.Exterior), // roof_metal 2
                Entry("b3e8f6161e4c4894fbb28e0a00a163a0", LegacyWetnessSurfaceCategory.Exterior), // roof_wood1
                Entry("4c655eee1f5769647aaba353726e3d53", LegacyWetnessSurfaceCategory.Vegetation), // WHEAT_SIDE
                Entry("5331eb6053122a543bb6668ec5bd908e", LegacyWetnessSurfaceCategory.Vegetation), // VEGETATION
                Entry("5a5c0f1993185af4fac578c232e2eaea", LegacyWetnessSurfaceCategory.Vegetation), // TREEWALL_higher
                Entry("74e2af945e6f6e948a99429361720d66", LegacyWetnessSurfaceCategory.Vegetation), // TREEWALL_lower
                Entry("ceece334d48596b47928e148fe30959a", LegacyWetnessSurfaceCategory.Vegetation), // WATER_PLANTS
            });
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static LegacyWetnessMaterialCoverageEntry Entry(
            string sourceGuid,
            LegacyWetnessSurfaceCategory category) =>
            new LegacyWetnessMaterialCoverageEntry(sourceGuid, category);

        private static void AuthorProductionShelters(
            Transform backend,
            ProductionShelterMeasurementTool.MeasurementReport measurements)
        {
            ProductionShelterMeasurementTool
                .ValidateReportAgainstFrozenSourceOrThrow(measurements);
            GameObject container = new GameObject("ProjectOwnedShelterVolumes");
            container.transform.SetParent(backend, false);
            for (int index = 0;
                 index < measurements.shelters.Length;
                 index++)
            {
                ProductionShelterMeasurementTool.ShelterMeasurementRecord
                    measured = measurements.shelters[index];
                if (measured.shelterKind !=
                    (int)ProductionShelterKind.Interior)
                {
                    throw new InvalidOperationException(
                        "The bounded home shelter rollout supports only measured interiors.");
                }

                GameObject volumeObject = new GameObject(
                    measured.stableId ==
                    ProductionShelterMeasurementTool
                        .HomeHouseShelterStableId
                        ? "HomeHouseInterior"
                        : "HomeGarageInterior");
                volumeObject.transform.SetParent(container.transform, false);
                volumeObject.transform.position = measured.center;
                ProductionShelterVolumeAuthoring authoring =
                    volumeObject.AddComponent<
                        ProductionShelterVolumeAuthoring>();
                authoring.ConfigureForAuthoring(
                    measured.stableId,
                    Vector3.zero,
                    measured.extents,
                    ProductionShelterKind.Interior);
                EditorUtility.SetDirty(authoring);
            }
        }

        private static Camera CreateStartupCamera(Transform parent)
        {
            GameObject cameraObject = new GameObject("StartupCameraPlaceholder");
            cameraObject.transform.SetParent(parent, false);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            return camera;
        }

        private static Transform CreateAnchor(
            Transform parent,
            string name,
            Vector3 localPosition)
        {
            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = localPosition;
            return anchor.transform;
        }

        private static void ReplaceEnviroVolume(
            EnviroManager manager,
            VolumeProfile profile)
        {
            Volume volume = manager.volumeHDRP;
            if (volume == null)
            {
                GameObject volumeObject = new GameObject("Enviro3_HDRPVolume");
                volumeObject.transform.SetParent(manager.transform, false);
                volume = volumeObject.AddComponent<Volume>();
            }

            volume.isGlobal = true;
            volume.priority = 10f;
            volume.sharedProfile = profile;
            manager.volumeHDRP = volume;
            EditorUtility.SetDirty(volume);
        }

        private static void DisableFoundationPlaceholders(Scene scene)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                GameObject root = roots[index];
                if (string.Equals(
                        root.name,
                        "Directional Sun",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        root.name,
                        "Global Volume",
                        StringComparison.Ordinal))
                {
                    root.SetActive(false);
                    EditorUtility.SetDirty(root);
                }
            }
        }

        private static void RemoveOwnedBackend(Transform compositionRoot)
        {
            for (int index = compositionRoot.childCount - 1;
                 index >= 0;
                 index--)
            {
                Transform child = compositionRoot.GetChild(index);
                ProductionEnvironmentBackendMarker marker =
                    child.GetComponent<
                        ProductionEnvironmentBackendMarker>();
                if (marker != null && !marker.IsValid)
                {
                    throw new InvalidOperationException(
                        "A production environment backend marker has an " +
                        "unknown owner ID; rebuild will not delete it.");
                }

                bool isOwned = marker != null && marker.IsValid;
                bool isLegacyNameOnlyBackend = marker == null &&
                    string.Equals(
                        child.name,
                        BackendName,
                        StringComparison.Ordinal);
                if (isOwned || isLegacyNameOnlyBackend)
                {
                    Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        private static T FindSingleInScene<T>(Scene scene)
            where T : Component
        {
            var values = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                values.AddRange(roots[index].GetComponentsInChildren<T>(true));
            }

            if (values.Count != 1)
            {
                throw new InvalidOperationException(
                    $"Expected exactly one {typeof(T).Name} in Bootstrap; " +
                    $"found {values.Count}.");
            }

            return values[0];
        }

        private static T RequireAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new FileNotFoundException(
                    $"Required local asset is missing or is not {typeof(T).Name}.",
                    path);
            }

            return asset;
        }

        private static void RestoreBootstrapAfterFailedBuild(
            string fullPath,
            byte[] backup)
        {
            File.WriteAllBytes(fullPath, backup);
            AssetDatabase.ImportAsset(
                BootstrapScenePath,
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
        }

    }
}
