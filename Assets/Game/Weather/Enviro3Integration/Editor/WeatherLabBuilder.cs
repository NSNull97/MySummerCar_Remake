using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enviro;
using MSC.Development.WeatherLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MSC.Weather.Enviro3Integration.Editor
{
    public static class WeatherLabBuilder
    {
        public const string BuilderVersion = "1.1.0";
        public const string MenuRoot = "Tools/MSC Remake/Enviro 3 Preflight/";
        public const string VendorRoot = "Assets/Enviro 3 - Sky and Weather";
        public const string BindingsAssetPath =
            "Assets/Game/Development/WeatherLab/Content/Profiles/WeatherLabEnviro3Bindings.asset";
        public const string VolumeProfilePath =
            "Assets/Game/Development/WeatherLab/Content/Profiles/WeatherLabHDRPVolume.asset";

        private const string EnviroPrefabPath = VendorRoot + "/Enviro 3.prefab";
        private const string ConfigurationPath =
            VendorRoot + "/Profiles/Configurations/Default Enviro Configuration.asset";
        public const string EffectsSourceAssetPath =
            VendorRoot + "/Scripts/Runtime/Modules/Effects/Preset/Default Effects Preset.asset";
        private const string WeatherPath = VendorRoot + "/Profiles/Weather Types/";
        private const string QualityPath = VendorRoot + "/Profiles/Quality/";
        private const string SprucePrefabPath =
            "Assets/Game/World/Production/Prefabs/WR_SpruceTree.prefab";

        [MenuItem(MenuRoot + "Build or Rebuild WeatherLab")]
        public static void Build()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                Debug.Log("WeatherLab build cancelled.");
                return;
            }

            AssertExcludedFromBuildSettings();
            RequireVendorAsset<GameObject>(EnviroPrefabPath);
            EnsureAssetFolders();

            Material ground = EnsureMaterial("WL_Ground", new Color(0.16f, 0.25f, 0.12f), 0.05f);
            Material asphalt = EnsureMaterial("WL_Asphalt", new Color(0.055f, 0.06f, 0.065f), 0.18f);
            Material gravel = EnsureMaterial("WL_Gravel", new Color(0.27f, 0.24f, 0.19f), 0.08f);
            Material dirt = EnsureMaterial("WL_Dirt", new Color(0.22f, 0.13f, 0.075f), 0.03f);
            Material wall = EnsureMaterial("WL_Wall", new Color(0.42f, 0.16f, 0.09f), 0.22f);
            Material trim = EnsureMaterial("WL_Trim", new Color(0.68f, 0.64f, 0.52f), 0.3f);
            Material metal = EnsureMaterial("WL_Metal", new Color(0.11f, 0.18f, 0.24f), 0.62f, 0.75f);
            Material glass = EnsureTransparentMaterial("WL_Glass", new Color(0.55f, 0.75f, 0.84f, 0.28f), 0.75f);
            Material water = EnsureTransparentMaterial("WL_Water", new Color(0.08f, 0.3f, 0.36f, 0.52f), 0.86f);
            Material marker = EnsureMaterial("WL_Marker", new Color(0.92f, 0.45f, 0.04f), 0.25f);

            VolumeProfile volumeProfile = EnsureVolumeProfile();
            Enviro3EnvironmentBindings bindings = EnsureBindings();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject root = new GameObject("WeatherLab_Root");
            SceneManager.MoveGameObjectToScene(root, scene);

            Transform geometry = CreateGroup(root.transform, "Geometry");
            Transform vegetation = CreateGroup(root.transform, "Vegetation");
            Transform props = CreateGroup(root.transform, "Props");
            Transform camerasRoot = CreateGroup(root.transform, "Cameras");
            Transform anchorsRoot = CreateGroup(root.transform, "CaptureAnchors");
            Transform lightsRoot = CreateGroup(root.transform, "LightingFixtures");
            Transform diagnostics = CreateGroup(root.transform, "Diagnostics");
            Transform environmentBackend = CreateGroup(root.transform, "EnvironmentBackend");

            var wetGround = new List<Renderer>();
            var wetRoad = new List<Renderer>();
            var wetExterior = new List<Renderer>();
            var wetVegetation = new List<Renderer>();
            var wetPuddle = new List<Renderer>();
            BuildGround(
                geometry,
                ground,
                asphalt,
                gravel,
                dirt,
                water,
                wetGround,
                wetRoad,
                wetPuddle);
            BuildBuilding(geometry, wall, trim, glass, wetExterior);
            BuildMaterialSamples(props, wall, metal, glass);
            Transform vehicle = BuildVehicleProxy(props, metal, trim);
            BuildVegetation(vegetation, wetVegetation);

            Camera exterior = CreateCamera(
                camerasRoot, "ExteriorCamera", new Vector3(12f, 4.5f, -14f),
                new Vector3(0f, 1f, 4f), enabled: true, isMain: true);
            Camera interior = CreateCamera(
                camerasRoot, "InteriorLookingOutCamera", new Vector3(-10f, 1.65f, 8f),
                new Vector3(-4f, 1.4f, 2f), enabled: false, isMain: false);
            Camera vehicleCamera = CreateCamera(
                camerasRoot, "VehicleInteriorLookingOutCamera", new Vector3(3f, 1.25f, -8f),
                new Vector3(3f, 1f, 4f), enabled: false, isMain: false);
            Camera[] cameras = { exterior, interior, vehicleCamera };

            Transform lightningOrigin = CreateAnchorTransform(
                anchorsRoot, "LightningOrigin", new Vector3(6f, 55f, 7f));
            Transform lightningTarget = CreateAnchorTransform(
                anchorsRoot, "LightningTarget", new Vector3(6f, 0f, 7f));
            WeatherLabCaptureAnchor[] anchors =
            {
                CreateCaptureAnchor(anchorsRoot, "W07A-CAP-EXT-01", exterior.transform.position, exterior),
                CreateCaptureAnchor(anchorsRoot, "W07A-CAP-INT-01", interior.transform.position, interior),
                CreateCaptureAnchor(anchorsRoot, "W07A-CAP-VEH-01", vehicleCamera.transform.position, vehicleCamera),
                CreateCaptureAnchor(anchorsRoot, "W07A-CAP-WATERFOG-01", new Vector3(10f, 1.4f, 3f), exterior),
                CreateCaptureAnchor(anchorsRoot, "W07A-CAP-HEADLIGHTS-01", new Vector3(3f, 1.2f, -2f), vehicleCamera),
                CreateCaptureAnchor(anchorsRoot, "W07A-CAP-PERF-01", new Vector3(0f, 2f, -12f), exterior)
            };

            BuildLightingFixtures(lightsRoot, vehicle);

            GameObject enviroPrefab = RequireVendorAsset<GameObject>(EnviroPrefabPath);
            GameObject enviroObject = (GameObject)PrefabUtility.InstantiatePrefab(enviroPrefab, scene);
            enviroObject.name = "Enviro3_Instance";
            enviroObject.transform.SetParent(environmentBackend, false);
            if (PrefabUtility.IsPartOfAnyPrefab(enviroObject))
            {
                PrefabUtility.UnpackPrefabInstance(
                    enviroObject,
                    PrefabUnpackMode.OutermostRoot,
                    InteractionMode.AutomatedAction);
            }

            EnviroManager manager = enviroObject.GetComponent<EnviroManager>();
            if (manager == null)
            {
                throw new InvalidOperationException("Enviro prefab has no EnviroManager component.");
            }

            manager.dontDestroyOnLoad = false;
            manager.ChangeCamera(exterior);
            ReplaceEnviroVolume(manager, volumeProfile);

            Enviro3EnvironmentAdapter adapter =
                enviroObject.GetComponent<Enviro3EnvironmentAdapter>() ??
                enviroObject.AddComponent<Enviro3EnvironmentAdapter>();
            adapter.ConfigureForAuthoring(
                manager,
                exterior,
                bindings,
                lightningOrigin,
                lightningTarget);

            WeatherLabStateController stateController =
                diagnostics.gameObject.AddComponent<WeatherLabStateController>();
            WeatherLabWetnessMaterialBridge wetnessBridge =
                diagnostics.gameObject.AddComponent<WeatherLabWetnessMaterialBridge>();
            wetnessBridge.ConfigureForAuthoring(
                wetGround.ToArray(),
                wetRoad.ToArray(),
                wetExterior.ToArray(),
                vehicle.GetComponentsInChildren<Renderer>(includeInactive: true),
                wetVegetation.ToArray(),
                wetPuddle.ToArray());
            stateController.ConfigureForAuthoring(
                adapter,
                cameras,
                lightningTarget,
                wetnessBridge);
            diagnostics.gameObject.AddComponent<WeatherLabPerformanceProbe>();

            WeatherLabSceneMarker sceneMarker = root.AddComponent<WeatherLabSceneMarker>();
            sceneMarker.ConfigureForAuthoring(cameras, anchors);

            CreateBox(
                diagnostics, "PerformanceMarker", new Vector3(0f, 0.35f, -12f),
                new Vector3(0.35f, 0.7f, 0.35f), marker, collider: false);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, WeatherLabSceneMarker.SceneAssetPath))
            {
                throw new InvalidOperationException("Could not save WeatherLab scene.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AssertExcludedFromBuildSettings();
            Debug.Log(
                "M07A_WEATHERLAB_BUILD_OK " +
                $"version={BuilderVersion} cameras={cameras.Length} anchors={anchors.Length} " +
                "buildSettings=excluded donorBaseline=absent");
        }

        public static void RunBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("WeatherLab batch build requires batch mode.");
            }

            Build();
            Enviro3PreflightValidator.ValidateOrThrow();
            Enviro3PreflightValidator.ExportDiagnostics();
        }

        [MenuItem(MenuRoot + "Open WeatherLab")]
        public static void OpenWeatherLab()
        {
            if (!File.Exists(Path.GetFullPath(WeatherLabSceneMarker.SceneAssetPath)))
            {
                Build();
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(WeatherLabSceneMarker.SceneAssetPath, OpenSceneMode.Single);
        }

        public static void AssertExcludedFromBuildSettings()
        {
            bool listed = EditorBuildSettings.scenes.Any(scene =>
                string.Equals(scene.path, WeatherLabSceneMarker.SceneAssetPath, StringComparison.Ordinal));
            if (listed)
            {
                throw new InvalidOperationException(
                    "WeatherLab must not appear in Build Settings, even as a disabled scene.");
            }
        }

        private static void EnsureAssetFolders()
        {
            EnsureFolder("Assets/Game/Development/WeatherLab/Content");
            EnsureFolder("Assets/Game/Development/WeatherLab/Content/Materials");
            EnsureFolder("Assets/Game/Development/WeatherLab/Content/Profiles");
            EnsureFolder("Assets/Game/Development/WeatherLab/Scenes");
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
                throw new InvalidOperationException("Invalid asset folder path: " + path);
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static Material EnsureMaterial(
            string name,
            Color color,
            float smoothness,
            float metallic = 0f)
        {
            string path = $"Assets/Game/Development/WeatherLab/Content/Materials/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException("HDRP/Lit shader is unavailable.");
            }

            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material EnsureTransparentMaterial(
            string name,
            Color color,
            float smoothness)
        {
            Material material = EnsureMaterial(name, color, smoothness);
            material.SetFloat("_SurfaceType", 1f);
            material.SetFloat("_BlendMode", 0f);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static VolumeProfile EnsureVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "WeatherLabHDRPVolume";
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            // VolumeProfile.Add only adds the component to the in-memory list. Editor-authored
            // profiles must also persist each component as a sub-asset or all references become
            // null after the next domain/project reload.
            profile.components.RemoveAll(component => component == null);

            VisualEnvironment visualEnvironment = GetOrAddVolumeComponent<VisualEnvironment>(profile);

            visualEnvironment.skyType.Override(990);

            EnviroHDRPSky enviroSky = GetOrAddVolumeComponent<EnviroHDRPSky>(profile);

            Fog fog = GetOrAddVolumeComponent<Fog>(profile);

            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);

            Exposure exposure = GetOrAddVolumeComponent<Exposure>(profile);

            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(10f);
            EditorUtility.SetDirty(visualEnvironment);
            EditorUtility.SetDirty(enviroSky);
            EditorUtility.SetDirty(fog);
            EditorUtility.SetDirty(exposure);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAddVolumeComponent<T>(VolumeProfile profile)
            where T : VolumeComponent
        {
            string profilePath = AssetDatabase.GetAssetPath(profile);
            if (string.IsNullOrEmpty(profilePath))
            {
                throw new InvalidOperationException(
                    $"Volume profile '{profile.name}' is not a persistent asset.");
            }

            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
            }

            string componentPath = AssetDatabase.GetAssetPath(component);
            if (string.IsNullOrEmpty(componentPath))
            {
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            else if (!string.Equals(componentPath, profilePath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Volume component {typeof(T).Name} belongs to a different asset: {componentPath}.");
            }

            return component;
        }

        private static Enviro3EnvironmentBindings EnsureBindings()
        {
            Enviro3EnvironmentBindings bindings =
                AssetDatabase.LoadAssetAtPath<Enviro3EnvironmentBindings>(BindingsAssetPath);
            if (bindings == null)
            {
                bindings = ScriptableObject.CreateInstance<Enviro3EnvironmentBindings>();
                bindings.name = "WeatherLabEnviro3Bindings";
                AssetDatabase.CreateAsset(bindings, BindingsAssetPath);
            }

            bindings.ConfigureForAuthoring(
                RequireVendorAsset<EnviroConfiguration>(ConfigurationPath),
                RequireVendorAsset<EnviroEffectsModule>(EffectsSourceAssetPath),
                RequireVendorAsset<EnviroWeatherType>(WeatherPath + "Clear Sky.asset"),
                RequireVendorAsset<EnviroWeatherType>(WeatherPath + "Cloudy 1.asset"),
                RequireVendorAsset<EnviroWeatherType>(WeatherPath + "Cloudy 3.asset"),
                RequireVendorAsset<EnviroWeatherType>(WeatherPath + "Rain.asset"),
                RequireVendorAsset<EnviroWeatherType>(WeatherPath + "Storm.asset"),
                RequireVendorAsset<EnviroWeatherType>(WeatherPath + "Foggy.asset"),
                RequireVendorAsset<EnviroQuality>(QualityPath + "Low.asset"),
                RequireVendorAsset<EnviroQuality>(QualityPath + "High.asset"));
            EditorUtility.SetDirty(bindings);
            return bindings;
        }

        private static T RequireVendorAsset<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new FileNotFoundException(
                    $"Required local Enviro asset is missing or has the wrong type {typeof(T).Name}.",
                    path);
            }

            return asset;
        }

        private static void ReplaceEnviroVolume(EnviroManager manager, VolumeProfile profile)
        {
            if (manager.volumeHDRP != null)
            {
                Object.DestroyImmediate(manager.volumeHDRP.gameObject);
            }

            GameObject volumeObject = new GameObject("Enviro3_HDRPVolume");
            volumeObject.transform.SetParent(manager.transform, false);
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;
            manager.volumeHDRP = volume;
        }

        private static void BuildGround(
            Transform parent,
            Material ground,
            Material asphalt,
            Material gravel,
            Material dirt,
            Material water,
            ICollection<Renderer> wetGround,
            ICollection<Renderer> wetRoad,
            ICollection<Renderer> wetPuddle)
        {
            wetGround.Add(CreateBox(
                parent, "TerrainPatch_40x40", new Vector3(0f, -0.15f, 0f),
                new Vector3(40f, 0.3f, 40f), ground).GetComponent<Renderer>());
            wetRoad.Add(CreateBox(
                parent, "AsphaltRoad_6x36", new Vector3(0f, 0.02f, 0f),
                new Vector3(6f, 0.04f, 36f), asphalt).GetComponent<Renderer>());
            wetRoad.Add(CreateBox(
                parent, "GravelPatch_8x10", new Vector3(-9f, 0.015f, -5f),
                new Vector3(8f, 0.03f, 10f), gravel).GetComponent<Renderer>());
            wetRoad.Add(CreateBox(
                parent, "DirtPatch_8x10", new Vector3(9f, 0.015f, -5f),
                new Vector3(8f, 0.03f, 10f), dirt).GetComponent<Renderer>());
            CreateBox(parent, "PuddleDepression", new Vector3(10f, -0.24f, 8f),
                new Vector3(8f, 0.12f, 12f), dirt);
            GameObject waterProxy = CreateBox(parent, "WaterProxy_8x12", new Vector3(10f, -0.12f, 8f),
                new Vector3(8f, 0.03f, 12f), water, collider: false);
            waterProxy.layer = 0;
            wetPuddle.Add(waterProxy.GetComponent<Renderer>());
            CreateBox(parent, "ShoreProxy", new Vector3(5.85f, -0.08f, 8f),
                new Vector3(0.3f, 0.12f, 12f), gravel);
        }

        private static void BuildBuilding(
            Transform parent,
            Material wall,
            Material trim,
            Material glass,
            ICollection<Renderer> wetExterior)
        {
            Transform building = CreateGroup(parent, "Building");
            Vector3 center = new Vector3(-10f, 0f, 10f);
            CreateBox(building, "InteriorFloor", center + new Vector3(0f, 0.05f, 0f),
                new Vector3(9f, 0.1f, 8f), trim);
            wetExterior.Add(CreateBox(building, "BackWall", center + new Vector3(0f, 1.5f, 4f),
                new Vector3(9f, 3f, 0.2f), wall).GetComponent<Renderer>());
            wetExterior.Add(CreateBox(building, "LeftWall", center + new Vector3(-4.5f, 1.5f, 0f),
                new Vector3(0.2f, 3f, 8f), wall).GetComponent<Renderer>());
            wetExterior.Add(CreateBox(building, "RightWall", center + new Vector3(4.5f, 1.5f, 0f),
                new Vector3(0.2f, 3f, 8f), wall).GetComponent<Renderer>());
            wetExterior.Add(CreateBox(building, "FrontWallLeft", center + new Vector3(-3.1f, 1.5f, -4f),
                new Vector3(2.8f, 3f, 0.2f), wall).GetComponent<Renderer>());
            wetExterior.Add(CreateBox(building, "FrontWallRight", center + new Vector3(2.9f, 1.5f, -4f),
                new Vector3(3.2f, 3f, 0.2f), wall).GetComponent<Renderer>());
            wetExterior.Add(CreateBox(building, "DoorwayHeader", center + new Vector3(-0.1f, 2.65f, -4f),
                new Vector3(3.2f, 0.7f, 0.2f), wall).GetComponent<Renderer>());
            wetExterior.Add(CreateBox(building, "Roof", center + new Vector3(0f, 3.2f, 0f),
                new Vector3(9.6f, 0.25f, 8.6f), trim).GetComponent<Renderer>());
            CreateBox(building, "TransparentWindow", center + new Vector3(4.37f, 1.65f, 0.5f),
                new Vector3(0.04f, 1.4f, 2.2f), glass, collider: false);
        }

        private static void BuildMaterialSamples(
            Transform parent,
            Material opaque,
            Material metal,
            Material transparent)
        {
            CreateBox(parent, "OpaqueMaterialSample", new Vector3(-7f, 0.6f, -8f),
                Vector3.one * 1.2f, opaque);
            CreatePrimitive(parent, "MetalMaterialSample", PrimitiveType.Sphere,
                new Vector3(-5f, 0.75f, -8f), Vector3.one * 1.5f, metal);
            CreateBox(parent, "TransparentMaterialSample", new Vector3(-3f, 0.8f, -8f),
                new Vector3(1.4f, 1.6f, 0.08f), transparent, collider: false);
        }

        private static Transform BuildVehicleProxy(Transform parent, Material body, Material trim)
        {
            Transform vehicle = CreateGroup(parent, "StationaryVehicleProxy");
            vehicle.position = new Vector3(3f, 0f, -8f);
            CreateBox(vehicle, "Body", new Vector3(0f, 0.7f, 0f),
                new Vector3(2.2f, 0.7f, 4.2f), body);
            CreateBox(vehicle, "Cabin", new Vector3(0f, 1.35f, -0.2f),
                new Vector3(1.8f, 0.75f, 2f), trim);
            Vector3[] wheelPositions =
            {
                new Vector3(-1.15f, 0.45f, -1.35f), new Vector3(1.15f, 0.45f, -1.35f),
                new Vector3(-1.15f, 0.45f, 1.35f), new Vector3(1.15f, 0.45f, 1.35f)
            };
            foreach (Vector3 position in wheelPositions)
            {
                GameObject wheel = CreatePrimitive(vehicle, "Wheel", PrimitiveType.Cylinder,
                    position, new Vector3(0.7f, 0.28f, 0.7f), trim);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            return vehicle;
        }

        private static void BuildVegetation(
            Transform parent,
            ICollection<Renderer> wetVegetation)
        {
            GameObject sprucePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SprucePrefabPath);
            Vector3[] positions =
            {
                new Vector3(-17f, 0f, -14f), new Vector3(-17f, 0f, -6f),
                new Vector3(-17f, 0f, 2f), new Vector3(-17f, 0f, 10f),
                new Vector3(17f, 0f, -13f), new Vector3(17f, 0f, -4f),
                new Vector3(17f, 0f, 5f), new Vector3(17f, 0f, 14f)
            };
            for (int index = 0; index < positions.Length; index++)
            {
                if (sprucePrefab == null)
                {
                    GameObject proxy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    proxy.name = "ApprovedVegetationProxy_" + index;
                    proxy.transform.SetParent(parent, false);
                    proxy.transform.position = positions[index] + Vector3.up * 2f;
                    proxy.transform.localScale = new Vector3(0.5f, 2f, 0.5f);
                    wetVegetation.Add(proxy.GetComponent<Renderer>());
                }
                else
                {
                    GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(sprucePrefab);
                    tree.name = "ApprovedSpruce_" + index;
                    tree.transform.SetParent(parent, false);
                    tree.transform.position = positions[index];
                    tree.transform.localScale = Vector3.one * (0.75f + index % 3 * 0.12f);
                    Renderer[] renderers = tree.GetComponentsInChildren<Renderer>(includeInactive: true);
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        wetVegetation.Add(renderers[rendererIndex]);
                    }
                }
            }
        }

        private static void BuildLightingFixtures(Transform parent, Transform vehicle)
        {
            for (int index = 0; index < 2; index++)
            {
                GameObject lightObject = new GameObject("Headlight_" + (index + 1));
                lightObject.transform.SetParent(vehicle, false);
                lightObject.transform.localPosition = new Vector3(index == 0 ? -0.65f : 0.65f, 0.75f, 2.12f);
                lightObject.transform.localRotation = Quaternion.identity;
                Light light = lightObject.AddComponent<Light>();
                light.type = LightType.Spot;
                light.range = 35f;
                light.spotAngle = 42f;
                light.intensity = 1400f;
                light.color = new Color(1f, 0.86f, 0.65f);
                light.enabled = true;
                lightObject.AddComponent<HDAdditionalLightData>();
            }

            GameObject interior = new GameObject("InteriorPractical");
            interior.transform.SetParent(parent, false);
            interior.transform.position = new Vector3(-10f, 2.55f, 10f);
            Light practical = interior.AddComponent<Light>();
            practical.type = LightType.Point;
            practical.range = 8f;
            practical.intensity = 450f;
            practical.color = new Color(1f, 0.72f, 0.45f);
            interior.AddComponent<HDAdditionalLightData>();
        }

        private static Camera CreateCamera(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 lookAt,
            bool enabled,
            bool isMain)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            gameObject.transform.LookAt(lookAt);
            Camera camera = gameObject.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 500f;
            camera.enabled = enabled;
            if (isMain)
            {
                gameObject.tag = "MainCamera";
            }
            gameObject.AddComponent<HDAdditionalCameraData>();
            return camera;
        }

        private static WeatherLabCaptureAnchor CreateCaptureAnchor(
            Transform parent,
            string id,
            Vector3 position,
            Camera camera)
        {
            GameObject gameObject = new GameObject(id);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            WeatherLabCaptureAnchor anchor = gameObject.AddComponent<WeatherLabCaptureAnchor>();
            anchor.ConfigureForAuthoring(id, camera);
            return anchor;
        }

        private static Transform CreateAnchorTransform(Transform parent, string name, Vector3 position)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            return gameObject.transform;
        }

        private static Transform CreateGroup(Transform parent, string name)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static GameObject CreateBox(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool collider = true)
        {
            return CreatePrimitive(parent, name, PrimitiveType.Cube, position, scale, material, collider);
        }

        private static GameObject CreatePrimitive(
            Transform parent,
            string name,
            PrimitiveType type,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool collider = true)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localScale = scale;
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
            }

            if (!collider)
            {
                Collider primitiveCollider = gameObject.GetComponent<Collider>();
                if (primitiveCollider != null)
                {
                    Object.DestroyImmediate(primitiveCollider);
                }
            }

            return gameObject;
        }
    }
}
