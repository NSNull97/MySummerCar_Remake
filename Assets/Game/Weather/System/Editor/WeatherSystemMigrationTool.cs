using System;
using System.Collections.Generic;
using MSC.Audio;
using MSC.Weather.Enviro3Integration;
using MSC.Weather.Production;
using MSC.Weather.System.Audio;
using MSC.Weather.System.EnviroLegacy;
using MSC.Weather.System.Local;
using MSC.Weather.System.NativeHDRP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.Weather.System.Editor
{
    /// <summary>
    /// Explicit, Undo-backed scene migration. It never deletes Enviro assets or
    /// rewrites door gameplay; exterior portal endpoints remain a manual choice.
    /// </summary>
    public static class WeatherSystemMigrationTool
    {
        private const string StagedRootName =
            "GameWeatherSystem (Staged Migration)";
        private const string FinalRootName = "GameWeatherSystem";
        private const string StagedNativeVolumeName =
            "Native HDRP Weather Volume (Inactive while EnviroLegacy)";
        private const string FinalNativeVolumeName =
            "Native HDRP Weather Volume";
        private const string BootstrapScenePath =
            "Assets/Game/Bootstrap/Bootstrap.unity";
        private const string NativeVfxDirectory =
            "Assets/Game/Weather/System/Content/FinnishSummer/VFX";
        private const string PrecipitationTexturePath =
            NativeVfxDirectory + "/WeatherPrecipitationStreak.asset";
        private const string PrecipitationMaterialPath =
            NativeVfxDirectory + "/WeatherPrecipitationStreak.mat";

        [MenuItem("Tools/MSC/Weather System/Migrate Loaded Scene (Enviro Preserved)")]
        public static void MigrateLoadedScene()
        {
            if (!EditorUtility.DisplayDialog(
                    "Stage Weather System Migration",
                    "This adds the reversible weather facade and adapters to the loaded scene. " +
                    "Enviro stays selected. Door zone pairs are not guessed. Continue?",
                    "Migrate with Undo",
                    "Cancel"))
            {
                return;
            }

            TryMigrateLoadedScene(WeatherBackendType.EnviroLegacy);
        }

        [MenuItem(
            "Tools/MSC/Weather System/Preview Loaded Scene " +
            "(Full Native HDRP - No Moon/Stars)")]
        public static void FinalizeLoadedSceneNative()
        {
            if (!EditorUtility.DisplayDialog(
                    "Preview Full Native HDRP Weather",
                    "This reversible preview selects NativeHDRP and suspends " +
                    "Enviro presentation. Native currently has no accepted " +
                    "moon/stars parity and must not be saved as the production " +
                    "default. Continue?",
                    "Preview with Undo",
                    "Cancel"))
            {
                return;
            }

            TryMigrateLoadedScene(WeatherBackendType.NativeHDRP);
        }

        /// <summary>
        /// Deterministic CI/batch entry point. It opens and saves only Bootstrap;
        /// the interactive menu path intentionally remains review-before-save.
        /// </summary>
        public static void MigrateBootstrapBatch()
        {
            Scene bootstrap = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            if (!bootstrap.IsValid() || !bootstrap.isLoaded)
            {
                throw new InvalidOperationException(
                    $"Could not open Bootstrap scene at '{BootstrapScenePath}'.");
            }

            if (!TryMigrateLoadedScene(WeatherBackendType.EnviroLegacy))
            {
                throw new InvalidOperationException(
                    "Bootstrap weather migration failed. See the Unity log for the exact prerequisite failure.");
            }

            if (!EditorSceneManager.SaveScene(bootstrap))
            {
                throw new InvalidOperationException(
                    "Bootstrap weather migration completed but the scene could not be saved.");
            }

            Debug.Log(
                $"Weather migration saved '{BootstrapScenePath}'. EnviroLegacy remains selected.");
        }

        public static void FinalizeBootstrapNativeBatch()
        {
            Scene bootstrap = EditorSceneManager.OpenScene(
                BootstrapScenePath,
                OpenSceneMode.Single);
            if (!bootstrap.IsValid() || !bootstrap.isLoaded)
            {
                throw new InvalidOperationException(
                    $"Could not open Bootstrap scene at '{BootstrapScenePath}'.");
            }

            if (!TryMigrateLoadedScene(WeatherBackendType.NativeHDRP))
            {
                throw new InvalidOperationException(
                    "Bootstrap native weather finalization failed. See the Unity log for the exact prerequisite failure.");
            }

            if (!EditorSceneManager.SaveScene(bootstrap))
            {
                throw new InvalidOperationException(
                    "Bootstrap native weather finalization completed but the scene could not be saved.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Native HDRP weather finalization saved '{BootstrapScenePath}'. Enviro assets remain available for rollback.");
        }

        private static bool TryMigrateLoadedScene(
            WeatherBackendType selectedBackend)
        {

            if (!TryGetSingle(
                    out ProductionEnvironmentController production,
                    out string productionFailure))
            {
                Debug.LogError("Weather migration stopped: " + productionFailure);
                return false;
            }

            if (!TryGetSingle(
                    out Enviro3EnvironmentAdapter enviroAdapter,
                    out string enviroFailure))
            {
                Debug.LogError("Weather migration stopped: " + enviroFailure);
                return false;
            }

            if (!TryResolveSun(out Light sun, out string sunFailure))
            {
                Debug.LogError("Weather migration stopped: " + sunFailure);
                return false;
            }

            FinnishSummerClimateAssetBuilder.BuildAll();
            FinnishSummerClimateProfile climate =
                AssetDatabase.LoadAssetAtPath<FinnishSummerClimateProfile>(
                    FinnishSummerClimateAssetBuilder.ClimateProfilePath);
            if (climate == null)
            {
                Debug.LogError(
                    "Weather migration stopped: Finnish summer climate asset was not created.");
                return false;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Stage Game Weather System");

            GameObject root = FindWeatherRoot();
            if (root == null)
            {
                root = new GameObject(StagedRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create weather system root");
                SceneManager.MoveGameObjectToScene(
                    root,
                    production.gameObject.scene);
            }

            Undo.RecordObject(root, "Name weather system root");
            root.name = selectedBackend == WeatherBackendType.NativeHDRP
                ? FinalRootName
                : StagedRootName;

            Transform sessionRoot = production.transform.root;
            if (root.transform.parent != sessionRoot)
            {
                Undo.SetTransformParent(
                    root.transform,
                    sessionRoot,
                    "Parent weather system under session composition");
                root.transform.localPosition = Vector3.zero;
                root.transform.localRotation = Quaternion.identity;
                root.transform.localScale = Vector3.one;
            }

            WeatherPortalSystem portalSystem = GetOrAdd<WeatherPortalSystem>(root);
            LegacyWeatherStreamingBridge streamingBridge =
                GetOrAdd<LegacyWeatherStreamingBridge>(root);
            GameWeatherSystem router = GetOrAdd<GameWeatherSystem>(root);
            WeatherDebugController debug = GetOrAdd<WeatherDebugController>(root);
            EnviroWeatherBackend enviroBackend =
                GetOrAdd<EnviroWeatherBackend>(root);
            NativeHDRPWeatherBackend nativeBackend =
                GetOrAdd<NativeHDRPWeatherBackend>(root);
            InteriorZoneResolver interiorResolver =
                GetOrAdd<InteriorZoneResolver>(root);
            RoofExposureResolver roofResolver =
                GetOrAdd<RoofExposureResolver>(root);
            WeatherEnvironmentResolver environmentResolver =
                GetOrAdd<WeatherEnvironmentResolver>(root);
            WeatherPortalDebugGizmos portalGizmos =
                GetOrAdd<WeatherPortalDebugGizmos>(root);

            GameObject nativeVolumeObject = FindChildByEitherName(
                root.transform,
                FinalNativeVolumeName,
                StagedNativeVolumeName) ?? FindOrCreateChild(
                root.transform,
                StagedNativeVolumeName);
            Undo.RecordObject(nativeVolumeObject, "Name native weather volume");
            nativeVolumeObject.name =
                selectedBackend == WeatherBackendType.NativeHDRP
                    ? FinalNativeVolumeName
                    : StagedNativeVolumeName;
            Volume nativeVolume = GetOrAdd<Volume>(nativeVolumeObject);
            Undo.RecordObject(nativeVolume, "Configure native weather volume");
            nativeVolume.isGlobal = true;
            nativeVolume.priority = 1000f;
            nativeVolume.weight = 0f;
            nativeVolume.enabled = false;
            nativeVolume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(
                FinnishSummerClimateAssetBuilder.NativeVolumeProfilePath);

            WeatherGeographySettings geography =
                AssetDatabase.LoadAssetAtPath<WeatherGeographySettings>(
                    FinnishSummerClimateAssetBuilder.GeographyPath);
            HDAdditionalLightData sunData =
                sun.GetComponent<HDAdditionalLightData>();
            EnsureNativePresentation(
                root.transform,
                out ParticleSystem[] rainSystems,
                out ParticleSystem[] drizzleSystems,
                out Light lightningFlash);
            Volume[] legacyWeatherVolumes = FindLegacyWeatherVolumes(nativeVolume);
            Behaviour[] legacyOwners = FindLegacyPresentationOwners(
                enviroAdapter,
                nativeBackend);

            Undo.RecordObject(enviroBackend, "Configure Enviro weather wrapper");
            enviroBackend.ConfigureForAuthoring(enviroAdapter);
            Undo.RecordObject(nativeBackend, "Configure native HDRP weather backend");
            nativeBackend.ConfigureForAuthoring(
                nativeVolume,
                sun,
                sunData,
                geography,
                rainSystems,
                drizzleSystems,
                legacyWeatherVolumes,
                legacyOwners,
                environmentResolver,
                lightningFlash);
            Undo.RecordObject(router, "Configure weather backend router");
            router.ConfigureForAuthoring(
                selectedBackend,
                enviroBackend,
                nativeBackend,
                debug);

            Transform listenerAnchor = ResolveListenerAnchor();
            SetReference(interiorResolver, "portalSystem", portalSystem);
            SetReference(interiorResolver, "gameWeatherSystem", router);
            SetReference(interiorResolver, "listenerAnchor", listenerAnchor);
            SetReference(roofResolver, "listenerAnchor", listenerAnchor);
            SetReference(
                environmentResolver,
                "interiorResolver",
                interiorResolver);
            SetReference(environmentResolver, "roofResolver", roofResolver);
            SetReference(debug, "localContextSourceComponent", environmentResolver);
            SetReference(portalGizmos, "portalSystem", portalSystem);
            SetReference(portalGizmos, "resolver", interiorResolver);
            WeatherZoneRegistry[] legacyRegistries =
                Find<WeatherZoneRegistry>();
            if (legacyRegistries.Length == 1)
            {
                SetReference(
                    streamingBridge,
                    "legacyRegistry",
                    legacyRegistries[0]);
            }

            SerializedObject productionSerialized =
                new SerializedObject(production);
            SerializedProperty adapterProperty =
                productionSerialized.FindProperty("adapterBehaviour");
            if (adapterProperty != null)
            {
                Undo.RecordObject(production, "Route production weather through facade");
                adapterProperty.objectReferenceValue = router;
                productionSerialized.ApplyModifiedProperties();
            }

            Undo.RecordObject(production, "Assign Finnish summer climate");
            production.ConfigureClimateForAuthoring(climate);

            AddLegacyZoneAdapters();
            TryAddWeatherAudioController(
                root,
                production,
                environmentResolver,
                debug);

            EditorUtility.SetDirty(root);
            EditorUtility.SetDirty(production);
            EditorSceneManager.MarkSceneDirty(production.gameObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = root;
            Debug.Log(
                $"Weather migration configured with {selectedBackend} selected. " +
                "Enviro assets and references remain intact for reversible rollback; " +
                "streamed zones and explicitly marked exterior gameplay doors are bound at runtime.",
                root);
            return true;
        }

        [MenuItem("Tools/MSC/Weather System/Revert Loaded Scene to Direct Enviro")]
        public static void RevertLoadedScene()
        {
            if (!EditorUtility.DisplayDialog(
                    "Revert Weather System Migration",
                    "This removes only the staged facade root and generated legacy-zone adapters, " +
                    "then reconnects the production owner directly to Enviro. Continue?",
                    "Revert with Undo",
                    "Cancel"))
            {
                return;
            }

            bool hasProduction = TryGetSingle(
                out ProductionEnvironmentController production,
                out string productionFailure);
            bool hasEnviro = TryGetSingle(
                out Enviro3EnvironmentAdapter enviroAdapter,
                out string enviroFailure);
            if (!hasProduction || !hasEnviro)
            {
                Debug.LogError(
                    "Weather migration revert stopped: " +
                    productionFailure + " " + enviroFailure);
                return;
            }

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Revert Game Weather System");

            SerializedObject serialized = new SerializedObject(production);
            SerializedProperty adapter = serialized.FindProperty("adapterBehaviour");
            if (adapter != null)
            {
                Undo.RecordObject(production, "Restore direct Enviro adapter");
                adapter.objectReferenceValue = enviroAdapter;
                serialized.ApplyModifiedProperties();
            }

            LegacyWeatherZoneAdapter[] adapters =
                Find<LegacyWeatherZoneAdapter>();
            for (int index = 0; index < adapters.Length; index++)
            {
                Undo.DestroyObjectImmediate(adapters[index]);
            }

            GameObject root = FindWeatherRoot();
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
            }

            EditorUtility.SetDirty(production);
            EditorSceneManager.MarkSceneDirty(production.gameObject.scene);
            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log(
                "Weather migration reverted. Production weather points directly at the preserved Enviro adapter.",
                production);
        }

        private static void AddLegacyZoneAdapters()
        {
            WeatherZone[] zones = Find<WeatherZone>();
            for (int index = 0; index < zones.Length; index++)
            {
                WeatherZone zone = zones[index];
                LegacyWeatherZoneAdapter adapter =
                    zone.GetComponent<LegacyWeatherZoneAdapter>();
                if (adapter == null)
                {
                    adapter = Undo.AddComponent<LegacyWeatherZoneAdapter>(
                        zone.gameObject);
                }

                Undo.RecordObject(adapter, "Configure legacy weather zone adapter");
                adapter.ConfigureForAuthoring(zone);
                EditorUtility.SetDirty(adapter);
            }
        }

        private static void TryAddWeatherAudioController(
            GameObject root,
            ProductionEnvironmentController production,
            WeatherEnvironmentResolver resolver,
            WeatherDebugController debug)
        {
            Behaviour legacyPresenter = FindBehaviourByTypeName(
                "MSC.Audio.WeatherIntegration.WeatherAudioPresenter");
            if (legacyPresenter == null)
            {
                Debug.LogWarning(
                    "Weather migration: no existing WeatherAudioPresenter was found. " +
                    "The new audio controller was not guessed or partially wired.",
                    root);
                return;
            }

            SerializedObject legacySerialized = new SerializedObject(legacyPresenter);
            MonoBehaviour backend = GetReference<MonoBehaviour>(
                legacySerialized,
                "backendComponent");
            AudioEmitterAuthoring emitter = GetReference<AudioEmitterAuthoring>(
                legacySerialized,
                "ambienceEmitter");
            AudioListenerContextPresenter listener =
                GetReference<AudioListenerContextPresenter>(
                    legacySerialized,
                    "listenerPresenter");
            if (!(backend is IAudioBackend))
            {
                Debug.LogWarning(
                    "Weather migration: the existing weather presenter has no valid " +
                    "IAudioBackend. Audio ownership remains unchanged.",
                    legacyPresenter);
                return;
            }

            WeatherAudioController controller =
                GetOrAdd<WeatherAudioController>(root);
            Undo.RecordObject(controller, "Configure weather audio controller");
            controller.ConfigureForAuthoring(
                production,
                resolver,
                backend,
                emitter,
                listener,
                debug,
                legacyPresenter);
            SerializedObject controllerSerialized = new SerializedObject(controller);
            SerializedProperty suspend = controllerSerialized.FindProperty(
                "suspendLegacyPresenterWhenActive");
            if (suspend != null)
            {
                suspend.boolValue = true;
                controllerSerialized.ApplyModifiedProperties();
            }
        }

        private static void EnsureNativePresentation(
            Transform root,
            out ParticleSystem[] rainSystems,
            out ParticleSystem[] drizzleSystems,
            out Light lightningFlash)
        {
            Material material = EnsurePrecipitationMaterial();
            GameObject presentation = FindOrCreateChild(
                root,
                "Native Weather Presentation");
            ParticleSystem rain = EnsurePrecipitationSystem(
                presentation.transform,
                "Rain Emitter",
                material,
                4200f,
                0.82f,
                0.011f,
                27f,
                new Color(0.78f, 0.82f, 0.88f, 0.28f),
                5200);
            ParticleSystem drizzle = EnsurePrecipitationSystem(
                presentation.transform,
                "Drizzle Emitter",
                material,
                1800f,
                1.45f,
                0.008f,
                10f,
                new Color(0.8f, 0.84f, 0.89f, 0.2f),
                2800);

            GameObject flashObject = FindOrCreateChild(
                presentation.transform,
                "Lightning Flash");
            lightningFlash = GetOrAdd<Light>(flashObject);
            Undo.RecordObject(lightningFlash, "Configure weather lightning flash");
            lightningFlash.type = LightType.Point;
            lightningFlash.range = 420f;
            lightningFlash.color = new Color(0.72f, 0.82f, 1f);
            lightningFlash.shadows = LightShadows.None;
            lightningFlash.intensity = 0f;
            lightningFlash.enabled = false;
            GetOrAdd<HDAdditionalLightData>(flashObject);

            rainSystems = new[] { rain };
            drizzleSystems = new[] { drizzle };
        }

        private static ParticleSystem EnsurePrecipitationSystem(
            Transform parent,
            string name,
            Material material,
            float ratePerSecond,
            float lifetimeSeconds,
            float sizeMeters,
            float fallSpeedMetersPerSecond,
            Color color,
            int maximumParticles)
        {
            GameObject owner = FindOrCreateChild(parent, name);
            ParticleSystem system = GetOrAdd<ParticleSystem>(owner);
            Undo.RecordObject(system, "Configure native weather precipitation");

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.startLifetime = lifetimeSeconds;
            main.startSpeed = 0f;
            main.startSize = sizeMeters;
            main.startColor = color;
            main.gravityModifier = 0f;
            main.maxParticles = maximumParticles;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = ratePerSecond;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = fallSpeedMetersPerSecond > 12f
                ? new Vector3(18f, 0.6f, 14f)
                : new Vector3(16f, 0.6f, 12f);

            ParticleSystem.VelocityOverLifetimeModule velocity =
                system.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = 0f;
            velocity.y = -fallSpeedMetersPerSecond;
            velocity.z = 0f;

            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.collidesWith = Physics.DefaultRaycastLayers;
            collision.dampen = 0.04f;
            collision.bounce = 0f;
            collision.lifetimeLoss = 1f;
            collision.radiusScale = 0.12f;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.enableDynamicColliders = false;
            collision.maxCollisionShapes = 128;
            collision.sendCollisionMessages = true;

            ParticleSystemRenderer renderer =
                GetOrAdd<ParticleSystemRenderer>(owner);
            Undo.RecordObject(renderer, "Configure precipitation renderer");
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = fallSpeedMetersPerSecond > 12f
                ? 0.022f
                : 0.015f;
            renderer.lengthScale = fallSpeedMetersPerSecond > 12f
                ? 0.42f
                : 0.28f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sharedMaterial = material;
            renderer.sortingFudge = 0f;
            EditorUtility.SetDirty(system);
            EditorUtility.SetDirty(renderer);
            return system;
        }

        private static Material EnsurePrecipitationMaterial()
        {
            EnsureAssetFolder(NativeVfxDirectory);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                PrecipitationTexturePath);
            if (texture == null)
            {
                const int Width = 16;
                const int Height = 64;
                texture = new Texture2D(
                    Width,
                    Height,
                    TextureFormat.RGBA32,
                    mipChain: false,
                    linear: true)
                {
                    name = "WeatherPrecipitationStreak",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
                var pixels = new Color[Width * Height];
                for (int y = 0; y < Height; y++)
                {
                    float vertical = Mathf.Sin(
                        Mathf.PI * (y + 0.5f) / Height);
                    for (int x = 0; x < Width; x++)
                    {
                        float horizontal = Mathf.Abs(
                            ((x + 0.5f) / Width - 0.5f) * 2f);
                        float alpha = Mathf.Exp(-horizontal * horizontal * 8f) *
                                      Mathf.Pow(vertical, 0.35f);
                        pixels[y * Width + x] = new Color(
                            0.72f,
                            0.78f,
                            0.84f,
                            alpha * 0.52f);
                    }
                }

                texture.SetPixels(pixels);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
                AssetDatabase.CreateAsset(texture, PrecipitationTexturePath);
            }

            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "HDRP/Unlit is required for native weather precipitation.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                PrecipitationMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "WeatherPrecipitationStreak",
                };
                AssetDatabase.CreateAsset(material, PrecipitationMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.renderQueue = (int)RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            SetMaterialFloat(material, "_SurfaceType", 1f);
            SetMaterialFloat(material, "_BlendMode", 0f);
            SetMaterialFloat(material, "_TransparentZWrite", 0f);
            SetMaterialFloat(material, "_ZWrite", 0f);
            SetMaterialFloat(material, "_EnableFogOnTransparent", 1f);
            SetMaterialFloat(material, "_DoubleSidedEnable", 1f);
            SetMaterialColor(
                material,
                "_BaseColor",
                new Color(0.58f, 0.62f, 0.68f, 0.48f));
            SetMaterialColor(
                material,
                "_UnlitColor",
                new Color(0.58f, 0.62f, 0.68f, 0.48f));
            SetMaterialTexture(material, "_BaseColorMap", texture);
            SetMaterialTexture(material, "_UnlitColorMap", texture);
            HDMaterial.SetSurfaceType(material, transparent: true);
            HDMaterial.SetRenderingPass(
                material,
                HDMaterial.RenderingPass.Default);
            if (!HDMaterial.ValidateMaterial(material))
            {
                throw new InvalidOperationException(
                    "Native weather precipitation material failed HDRP validation.");
            }

            EditorUtility.SetDirty(texture);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureAssetFolder(string path)
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

        private static void SetMaterialFloat(
            Material material,
            string property,
            float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private static void SetMaterialColor(
            Material material,
            string property,
            Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }

        private static void SetMaterialTexture(
            Material material,
            string property,
            Texture value)
        {
            if (material.HasProperty(property))
            {
                material.SetTexture(property, value);
            }
        }

        private static Volume[] FindLegacyWeatherVolumes(Volume nativeVolume)
        {
            Volume[] all = Find<Volume>();
            var result = new List<Volume>(4);
            for (int index = 0; index < all.Length; index++)
            {
                Volume volume = all[index];
                if (volume == null || volume == nativeVolume ||
                    volume.sharedProfile == null)
                {
                    continue;
                }

                List<VolumeComponent> components = volume.sharedProfile.components;
                bool ownsWeather = false;
                for (int componentIndex = 0;
                     componentIndex < components.Count;
                     componentIndex++)
                {
                    string typeName = components[componentIndex]?.GetType().Name ??
                                      string.Empty;
                    if (typeName == "VisualEnvironment" || typeName == "Fog" ||
                        typeName == "VolumetricClouds" ||
                        typeName == "PhysicallyBasedSky" ||
                        typeName == "Exposure" ||
                        typeName == "WhiteBalance" ||
                        typeName == "ColorAdjustments" ||
                        typeName == "Tonemapping" ||
                        typeName == "IndirectLightingController")
                    {
                        ownsWeather = true;
                        break;
                    }
                }

                if (ownsWeather)
                {
                    result.Add(volume);
                }
            }

            return result.ToArray();
        }

        private static Behaviour[] FindLegacyPresentationOwners(
            Enviro3EnvironmentAdapter enviro,
            NativeHDRPWeatherBackend native)
        {
            var result = new List<Behaviour>(4) { enviro };
            Behaviour enviroManager = GetReference<Behaviour>(
                new SerializedObject(enviro),
                "manager");
            if (enviroManager != null && enviroManager != enviro)
            {
                result.Add(enviroManager);
            }

            Behaviour[] behaviours = Find<Behaviour>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                Behaviour behaviour = behaviours[index];
                if (behaviour == null || behaviour == native || behaviour == enviro)
                {
                    continue;
                }

                if (string.Equals(
                        behaviour.GetType().FullName,
                        "MSC.Weather.Production.NativeHdrpWeatherBridge",
                        StringComparison.Ordinal))
                {
                    result.Add(behaviour);
                }
            }

            return result.ToArray();
        }

        private static bool TryResolveSun(out Light sun, out string failure)
        {
            Light[] lights = Find<Light>();
            var candidates = new List<Light>(4);
            var named = new List<Light>(2);
            for (int index = 0; index < lights.Length; index++)
            {
                Light light = lights[index];
                if (light.type != LightType.Directional ||
                    light.name.IndexOf("moon", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                candidates.Add(light);
                if (light.name.IndexOf("sun", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    named.Add(light);
                }
            }

            if (named.Count == 1)
            {
                sun = named[0];
                failure = string.Empty;
                return true;
            }

            if (candidates.Count == 1)
            {
                sun = candidates[0];
                failure = string.Empty;
                return true;
            }

            sun = null;
            failure = candidates.Count == 0
                ? "No directional sun candidate exists."
                : "Directional sun ownership is ambiguous; name exactly one candidate with 'Sun'.";
            return false;
        }

        private static Transform ResolveListenerAnchor()
        {
            AudioListener[] listeners = Find<AudioListener>();
            if (listeners.Length == 1)
            {
                return listeners[0].transform;
            }

            Camera main = Camera.main;
            return main != null ? main.transform : null;
        }

        private static Behaviour FindBehaviourByTypeName(string fullName)
        {
            Behaviour[] behaviours = Find<Behaviour>();
            for (int index = 0; index < behaviours.Length; index++)
            {
                Behaviour behaviour = behaviours[index];
                if (behaviour != null && string.Equals(
                        behaviour.GetType().FullName,
                        fullName,
                        StringComparison.Ordinal))
                {
                    return behaviour;
                }
            }

            return null;
        }

        private static T GetReference<T>(
            SerializedObject serialized,
            string propertyName) where T : UnityEngine.Object
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            return property?.objectReferenceValue as T;
        }

        private static void SetReference(
            UnityEngine.Object target,
            string propertyName,
            UnityEngine.Object value)
        {
            Undo.RecordObject(target, "Configure weather system reference");
            SerializedObject serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                Debug.LogError(
                    $"Weather migration could not find serialized field '{propertyName}' on {target.GetType().Name}.",
                    target);
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        private static GameObject FindOrCreateChild(
            Transform parent,
            string childName)
        {
            Transform existing = parent.Find(childName);
            if (existing != null)
            {
                return existing.gameObject;
            }

            var child = new GameObject(childName);
            Undo.RegisterCreatedObjectUndo(child, "Create weather system child");
            child.transform.SetParent(parent, false);
            return child;
        }

        private static GameObject FindWeatherRoot() =>
            GameObject.Find(FinalRootName) ?? GameObject.Find(StagedRootName);

        private static GameObject FindChildByEitherName(
            Transform parent,
            string firstName,
            string secondName)
        {
            Transform child = parent.Find(firstName) ?? parent.Find(secondName);
            return child == null ? null : child.gameObject;
        }

        private static T GetOrAdd<T>(GameObject target)
            where T : Component
        {
            T component = target.GetComponent<T>();
            return component != null
                ? component
                : Undo.AddComponent<T>(target);
        }

        private static bool TryGetSingle<T>(out T value, out string failure)
            where T : UnityEngine.Object
        {
            T[] values = Find<T>();
            if (values.Length == 1)
            {
                value = values[0];
                failure = string.Empty;
                return true;
            }

            value = null;
            failure = values.Length == 0
                ? $"No {typeof(T).Name} exists in loaded scenes."
                : $"Expected one {typeof(T).Name}, found {values.Length}.";
            return false;
        }

        private static T[] Find<T>() where T : UnityEngine.Object =>
            UnityEngine.Object.FindObjectsByType<T>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
    }
}
