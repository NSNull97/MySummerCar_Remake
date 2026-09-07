using System;
using System.Collections.Generic;
using MSC.Presentation.AntiAliasing;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.UI.Presentation
{
    /// <summary>Owns a cached, isolated mesh-only home-yard and vehicle presentation.</summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuVehiclePreview : MonoBehaviour, IDisposable
    {
        private const int PreviewLayer = 31;
        private const int PreviewLayerMask = 1 << PreviewLayer;
        private const uint PreviewLightMask = 1u << 7;
        private const uint VehicleFillMask = 1u << 6;
        private const float GarageSpotIntensityCandela = 120000f;
        // The authored starting view sits inside the clear front arc at the
        // canonical spawn. Limits keep the camera outside house/foliage.
        public const float MinOrbitYaw = -20f;
        public const float MaxOrbitYaw = 45f;
        public const float MinOrbitPitch = -3f;
        public const float MaxOrbitPitch = 7f;
        private const float OrbitYawSensitivity = 120f;
        private const float OrbitPitchSensitivity = 70f;
        private static readonly Vector3 StageOrigin = new Vector3(0f, -10000f, 0f);
        private GameObject stage;
        private Camera previewCamera;
        private MainMenuVehicleModel model;
        private MainMenuEnvironmentModel environment;
        private RenderTexture output;
        private VolumeProfile volumeProfile;
        private bool initialized;
        private bool visible;
        private bool disposed;
        private bool rendering;
        private int pendingFrames;
        private float readinessDeadline;
        private float skySettleUntil;
        private float framedScreenAspect;
        private Vector2 orbitAngles;
        private float orbitDistance;
        private Transform vehicleFill;
        private Light garageLampLight;
        private MainMenuVehicleLights vehicleLights;
        private LocalVolumetricFog rearFog;
        private Light keyLight;
        private Light fillLight;
        private GradientSky menuSky;
        private MaterialPropertyBlock lampEmission;
        private Func<DateTime> localTimeProvider;
        private float nextTimeCheck;

        public Texture OutputTexture => output;
        public MainMenuVehicleModel Model => model;
        public MainMenuEnvironmentModel Environment => environment;
        public Camera PreviewCamera => previewCamera;
        public bool IsReady { get; private set; }
        public int RenderCount { get; private set; }
        public Vector2 OrbitAngles => orbitAngles;
        public Light GarageLampLight => garageLampLight;
        public IReadOnlyList<Light> VehicleLights => vehicleLights?.Lights ?? Array.Empty<Light>();
        public float VehicleLampNightFactor => vehicleLights?.NightFactor ?? 0f;
        public LocalVolumetricFog RearFog => rearFog;
        public DateTime LightingLocalTime { get; private set; }
        public MainMenuLightingState LightingState { get; private set; }
        public bool UsesSystemTime => localTimeProvider == null;
        public event Action FrameReady;

        public void Initialize(MainMenuVehicleModel vehiclePrefab, MainMenuEnvironmentModel environmentPrefab,
            Func<DateTime> localTimeProvider = null)
        {
            if (initialized || disposed) throw new InvalidOperationException("Menu preview is already initialized or disposed.");
            if (vehiclePrefab == null || environmentPrefab == null || environmentPrefab.MenuLightingProfile == null)
                throw new ArgumentException("Explicit mesh-only vehicle/environment prefabs and a menu lighting profile are required.");
            ValidatePresentationOnly(vehiclePrefab);
            ValidatePresentationOnly(environmentPrefab);
            if (environmentPrefab.VehicleAnchor == null || environmentPrefab.CameraFromVehicleDirection.sqrMagnitude < 0.1f)
                throw new ArgumentException("The menu environment needs an authored vehicle anchor and camera direction.");
            if (environmentPrefab.GarageLampRenderer == null || environmentPrefab.GarageLampAnchor == null)
                throw new ArgumentException("The menu environment needs its explicitly authored garage lamp.");
            var pipeline = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
            if (pipeline == null || !pipeline.currentPlatformRenderPipelineSettings.supportLightLayers)
                throw new InvalidOperationException("Menu preview requires HDRP light-layer support; global quality settings are never changed.");

            stage = new GameObject("Main menu world presentation") { hideFlags = HideFlags.DontSave, layer = PreviewLayer };
            this.localTimeProvider = localTimeProvider;
            stage.SetActive(false);
            stage.transform.position = StageOrigin;
            try
            {
                environment = Instantiate(environmentPrefab, stage.transform, false);
                environment.name = "Home yard menu presentation";
                environment.transform.localPosition = Vector3.zero;
                environment.transform.localRotation = Quaternion.identity;
                environment.transform.localScale = Vector3.one;
                ConfigureRenderers(environment, PreviewLightMask);

                model = Instantiate(vehiclePrefab, environment.VehicleAnchor, false);
                model.name = "Satsuma menu presentation";
                model.transform.localPosition = Vector3.up * -model.LocalBounds.min.y;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                ConfigureRenderers(model, PreviewLightMask | VehicleFillMask);

                BuildCamera();
                BuildVolume(environmentPrefab.MenuLightingProfile);
                BuildKeyLight();
                BuildVehicleFill();
                BuildGarageLamp();
                vehicleLights = new MainMenuVehicleLights(model, stage.transform, PreviewLayer, PreviewLightMask);
                BuildRearFog();
                framedScreenAspect = CurrentScreenAspect();
                RecreateOutput(framedScreenAspect);
                FitCamera();
                initialized = true;
                SetVisible(true);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void SetPaint(Color color)
        {
            if (!initialized || disposed) return;
            model.ApplyPaint(color);
            pendingFrames = Math.Max(pendingFrames, 1);
        }

        /// <summary>Mouse travel in screen heights; rotates only the presentation camera.</summary>
        public void Orbit(Vector2 normalizedDelta)
        {
            if (!initialized || disposed || !visible ||
                !float.IsFinite(normalizedDelta.x) || !float.IsFinite(normalizedDelta.y)) return;
            SetOrbit(new Vector2(
                Mathf.Clamp(orbitAngles.x - normalizedDelta.x * OrbitYawSensitivity, MinOrbitYaw, MaxOrbitYaw),
                Mathf.Clamp(orbitAngles.y - normalizedDelta.y * OrbitPitchSensitivity, MinOrbitPitch, MaxOrbitPitch)));
        }

        public void ResetOrbit()
        {
            if (!initialized || disposed || !visible) return;
            SetOrbit(Vector2.zero);
        }

        private void SetOrbit(Vector2 angles)
        {
            if (orbitAngles == angles) return;
            orbitAngles = angles;
            PositionCamera();
            // Coalesce multiple input events into one render. At a clamp or
            // after mouse release there is no continuing render/blur work.
            pendingFrames = Math.Max(pendingFrames, 1);
        }

        public void SetVisible(bool value)
        {
            if (!initialized || disposed) return;
            visible = value;
            stage.SetActive(value);
            previewCamera.enabled = false;
            if (value)
            {
                UpdateTimeLighting(force: true);
                IsReady = false;
                pendingFrames = Math.Max(pendingFrames, 2);
                readinessDeadline = Time.realtimeSinceStartup + 10f;
                // HDRP computes its sky ambient probe asynchronously. Submit two
                // warmup frames, then one final frame after a short bounded wait.
                // No render requests are issued while waiting or after settling.
                skySettleUntil = Time.realtimeSinceStartup + 0.25f;
            }
        }

        private void LateUpdate()
        {
            if (!initialized || disposed || !visible || rendering) return;
            UpdateTimeLighting();
            float screenAspect = CurrentScreenAspect();
            if (!Mathf.Approximately(screenAspect, framedScreenAspect))
            {
                framedScreenAspect = screenAspect;
                RecreateOutput(screenAspect);
                FitCamera();
                IsReady = false;
                pendingFrames = Math.Max(pendingFrames, 2);
                readinessDeadline = Time.realtimeSinceStartup + 10f;
            }
            if (pendingFrames == 0)
            {
                if (skySettleUntil == 0f || Time.realtimeSinceStartup < skySettleUntil) return;
                skySettleUntil = 0f;
                pendingFrames = 1;
            }
            var request = new RenderPipeline.StandardRequest { destination = output };
            if (!RenderPipeline.SupportsRenderRequest(previewCamera, request))
            {
                if (Time.realtimeSinceStartup < readinessDeadline) return;
                pendingFrames = 0;
                skySettleUntil = 0f;
                Debug.LogError("Menu preview: active HDRP did not accept an offscreen render request.", this);
                return;
            }

            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = previewCamera.targetTexture;
            rendering = true;
            try
            {
                RenderPipeline.SubmitRenderRequest(previewCamera, request);
                RenderCount++;
                pendingFrames--;
                if (pendingFrames == 0 && skySettleUntil == 0f)
                {
                    IsReady = true;
                    FrameReady?.Invoke();
                }
            }
            catch (Exception exception)
            {
                pendingFrames = 0;
                skySettleUntil = 0f;
                IsReady = false;
                Debug.LogError("Menu preview failed: " + exception.Message, this);
            }
            finally
            {
                previewCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                rendering = false;
            }
        }

        private static void ConfigureRenderers(Component root, uint lightMask)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = PreviewLayer;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.renderingLayerMask = lightMask;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.receiveShadows = true;
            }
        }

        private void BuildCamera()
        {
            GameObject cameraObject = Child("Menu presentation camera");
            previewCamera = cameraObject.AddComponent<Camera>();
            previewCamera.enabled = false;
            previewCamera.cullingMask = PreviewLayerMask;
            previewCamera.clearFlags = CameraClearFlags.Skybox;
            previewCamera.backgroundColor = new Color(0.2f, 0.3f, 0.42f, 1f);
            previewCamera.nearClipPlane = 0.1f;
            previewCamera.farClipPlane = 1500f;
            previewCamera.fieldOfView = 34f;
            previewCamera.allowDynamicResolution = false;
            previewCamera.useOcclusionCulling = false;
            cameraObject.AddComponent<AntiAliasingCameraPolicy>().Configure(AntiAliasingCameraRole.Excluded);
            HDAdditionalCameraData hd = cameraObject.AddComponent<HDAdditionalCameraData>();
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
            hd.backgroundColorHDR = new Color(0.2f, 0.3f, 0.42f, 1f);
            hd.volumeLayerMask = PreviewLayerMask;
            hd.volumeAnchorOverride = cameraObject.transform;
            hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            hd.customRenderingSettings = true;
            Override(hd, FrameSettingsField.LightLayers, true);
            Override(hd, FrameSettingsField.ExposureControl, true);
            Override(hd, FrameSettingsField.ShadowMaps, true);
            Override(hd, FrameSettingsField.CustomPass, false);
            Override(hd, FrameSettingsField.CustomPostProcess, false);
            Override(hd, FrameSettingsField.MotionVectors, false);
            Override(hd, FrameSettingsField.MotionBlur, false);
            Override(hd, FrameSettingsField.SSR, false);
            Override(hd, FrameSettingsField.ReflectionProbe, false);
            Override(hd, FrameSettingsField.SkyReflection, true);
            Override(hd, FrameSettingsField.AdaptiveProbeVolume, false);
            Override(hd, FrameSettingsField.Volumetrics, true);
            Override(hd, FrameSettingsField.ReprojectionForVolumetrics, false);
            Override(hd, FrameSettingsField.AtmosphericScattering, true);
        }

        private static void Override(HDAdditionalCameraData hd, FrameSettingsField field, bool value)
        {
            hd.renderingPathCustomFrameSettings.SetEnabled(field, value);
            FrameSettingsOverrideMask mask = hd.renderingPathCustomFrameSettingsOverrideMask;
            mask.mask[(uint)field] = true;
            hd.renderingPathCustomFrameSettingsOverrideMask = mask;
        }

        private void BuildVolume(VolumeProfile source)
        {
            volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volumeProfile.name = "MenuLighting_Owned";
            volumeProfile.hideFlags = HideFlags.DontSave;
            foreach (VolumeComponent component in source.components)
            {
                if (!(component is Exposure || component is Tonemapping)) continue;
                VolumeComponent copy = Instantiate(component);
                copy.hideFlags = HideFlags.DontSave;
                volumeProfile.components.Add(copy);
            }
            Exposure exposure = GetOrAdd<Exposure>();
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(exposure.fixedExposure.value);
            VisualEnvironment visual = GetOrAdd<VisualEnvironment>();
            visual.skyType.Override((int)SkyType.Gradient);
            visual.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            visual.cloudType.Override(0);
            // GradientSky does not read global celestial-light state. This
            // camera-only static menu sky leaves Enviro/world weather untouched.
            GradientSky sky = GetOrAdd<GradientSky>();
            menuSky = sky;
            sky.top.Override(new Color(0.13f, 0.25f, 0.44f));
            sky.middle.Override(new Color(0.45f, 0.52f, 0.62f));
            sky.bottom.Override(new Color(0.22f, 0.27f, 0.34f));
            sky.gradientDiffusion.Override(1f);
            sky.skyIntensityMode.Override(SkyIntensityMode.Multiplier);
            sky.multiplier.Override(6500f);
            sky.updateMode.Override(EnvironmentUpdateMode.OnChanged);
            GetOrAdd<Bloom>().intensity.Override(0f);
            Fog fog = GetOrAdd<Fog>();
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);
            // Clear foreground air; the finite volume behind the building
            // supplies the visible haze. Respect the selected HDRP quality's
            // volumetric support rather than modifying a shared pipeline asset.
            fog.meanFreePath.Override(100000f);
            fog.baseHeight.Override(StageOrigin.y - 2f);
            fog.maximumHeight.Override(StageOrigin.y + 35f);
            fog.maxFogDistance.Override(500f);
            fog.depthExtent.Override(220f);
            fog.albedo.Override(new Color(0.72f, 0.79f, 0.86f));
            fog.globalLightProbeDimmer.Override(0.85f);
            fog.anisotropy.Override(0.2f);
            // Cached menu frames cannot rely on a continuously accumulated
            // history; spatial denoising also avoids trails during orbit.
            fog.denoisingMode.Override(FogDenoisingMode.Gaussian);
            GameObject volumeObject = Child("Menu presentation lighting");
            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            volume.sharedProfile = volumeProfile;
        }

        private T GetOrAdd<T>() where T : VolumeComponent
        {
            if (!volumeProfile.TryGet(out T component)) component = volumeProfile.Add<T>(true);
            component.active = true;
            return component;
        }

        private void BuildKeyLight()
        {
            GameObject lightObject = Child("Menu warm box key");
            lightObject.transform.localRotation = Quaternion.Euler(22f, 90f, 0f);
            Vector3 target = model.transform.TransformPoint(model.LocalBounds.center);
            lightObject.transform.position = target - lightObject.transform.forward * 35f;
            Light light = lightObject.AddComponent<Light>();
            // A finite parallel box beam uses the punctual shadow atlas. HDRP's
            // one directional cascade atlas remains owned by the world's sun.
            light.type = LightType.Box;
            keyLight = light;
            light.areaSize = new Vector2(50f, 40f);
            light.range = 90f;
            light.cullingMask = PreviewLayerMask;
            light.shadows = LightShadows.Soft;
            HDAdditionalLightData hd = lightObject.AddComponent<HDAdditionalLightData>();
            light.lightUnit = LightUnit.Lux;
            light.useColorTemperature = false;
            light.color = new Color(1f, 0.80f, 0.61f);
            light.intensity = 60000f;
            hd.interactsWithSky = false;
            hd.applyRangeAttenuation = false;
            light.shapeRadius = 0.18f;
            hd.shadowDimmer = 1f;
            hd.normalBias = 0.35f;
            hd.slopeBias = 0.4f;
            hd.SetShadowResolutionOverride(true);
            hd.SetShadowResolution(2048);
            hd.SetLightLayer((UnityEngine.Rendering.HighDefinition.RenderingLayerMask)PreviewLightMask,
                (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)PreviewLightMask);
        }

        private static float CurrentScreenAspect() =>
            Mathf.Max(1, Screen.width) / (float)Mathf.Max(1, Screen.height);

        private void BuildVehicleFill()
        {
            GameObject lightObject = Child("Menu vehicle soft fill");
            vehicleFill = lightObject.transform;
            Vector3 target = model.transform.TransformPoint(model.LocalBounds.center);
            Vector3 direction = environment.transform.TransformDirection(environment.CameraFromVehicleDirection).normalized;
            lightObject.transform.SetPositionAndRotation(target + direction * 8f,
                Quaternion.LookRotation(-direction, environment.transform.up));
            Light light = lightObject.AddComponent<Light>();
            fillLight = light;
            light.type = LightType.Box;
            light.areaSize = new Vector2(7f, 5f);
            light.range = 20f;
            light.cullingMask = PreviewLayerMask;
            light.shadows = LightShadows.None;
            HDAdditionalLightData hd = lightObject.AddComponent<HDAdditionalLightData>();
            light.lightUnit = LightUnit.Lux;
            light.useColorTemperature = false;
            light.color = new Color(0.76f, 0.85f, 1f);
            light.intensity = 22000f;
            hd.interactsWithSky = false;
            hd.applyRangeAttenuation = false;
            // Preserve the yard's dappled contrast while keeping dark paint,
            // wheels and the engine readable. Only the vehicle receives this.
            hd.SetLightLayer((UnityEngine.Rendering.HighDefinition.RenderingLayerMask)VehicleFillMask,
                (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)VehicleFillMask);
        }

        private void BuildGarageLamp()
        {
            var emission = new MaterialPropertyBlock();
            lampEmission = emission;
            Renderer lamp = environment.GarageLampRenderer;
            int materialIndex = environment.GarageLampMaterialIndex;
            lamp.GetPropertyBlock(emission, materialIndex);
            // Emission is linear radiance, not an sRGB UI/paint colour.
            emission.SetVector("_EmissiveColor", new Vector4(6000f, 6000f, 6000f, 1f));
            // The housing and bracket share one atlas/submesh. Let its albedo
            // modulate emission so dark hardware remains darker than the shade.
            emission.SetFloat("_AlbedoAffectEmissive", 1f);
            lamp.SetPropertyBlock(emission, materialIndex);

            GameObject lightObject = Child("Menu garage practical light");
            lightObject.transform.position = environment.GarageLampAnchor.position +
                environment.GarageLampAnchor.forward * 0.30f;
            Vector3 target = model.transform.TransformPoint(model.LocalBounds.center);
            lightObject.transform.rotation = Quaternion.LookRotation(
                target - lightObject.transform.position, environment.transform.up);
            garageLampLight = lightObject.AddComponent<Light>();
            garageLampLight.type = LightType.Spot;
            garageLampLight.spotAngle = 80f;
            garageLampLight.innerSpotAngle = 60f;
            garageLampLight.enableSpotReflector = true;
            garageLampLight.range = 12f;
            garageLampLight.cullingMask = PreviewLayerMask;
            garageLampLight.shadows = LightShadows.Soft;
            HDAdditionalLightData hd = lightObject.AddComponent<HDAdditionalLightData>();
            // HDRP stores punctual intensity in candela. Set that unit
            // explicitly: ten times the previous 12000-cd practical light.
            garageLampLight.lightUnit = LightUnit.Candela;
            garageLampLight.useColorTemperature = false;
            garageLampLight.color = Color.white;
            garageLampLight.intensity = GarageSpotIntensityCandela;
            garageLampLight.shapeRadius = 0.12f;
            hd.interactsWithSky = false;
            hd.SetShadowResolutionOverride(true);
            hd.SetShadowResolution(1024);
            hd.SetLightLayer((UnityEngine.Rendering.HighDefinition.RenderingLayerMask)PreviewLightMask,
                (UnityEngine.Rendering.HighDefinition.RenderingLayerMask)PreviewLightMask);
        }

        private void BuildRearFog()
        {
            Bounds house = environment.HomeExteriorBoundsLocal;
            Vector3 size = new Vector3(160f, 22f, 100f);
            // The house's back is -Z. The front fade ends behind its rear roof
            // edge; even the closest camera/car remains outside this volume.
            Vector3 center = new Vector3(house.center.x, house.min.y + 8f,
                house.min.z - 2f - size.z * 0.5f);
            GameObject fogObject = Child("Menu rear woodland fog");
            fogObject.transform.SetPositionAndRotation(environment.transform.TransformPoint(center), environment.transform.rotation);
            rearFog = fogObject.AddComponent<LocalVolumetricFog>();
            rearFog.parameters = new LocalVolumetricFogArtistParameters(new Color(0.70f, 0.78f, 0.86f), 42f, 0f)
            {
                size = size,
                positiveFade = new Vector3(0.18f, 0.65f, 0.18f),
                negativeFade = new Vector3(0.18f, 0.08f, 0.18f),
                distanceFadeStart = 250f,
                distanceFadeEnd = 400f,
                textureScrollingSpeed = Vector3.zero
            };
        }

        private void UpdateTimeLighting(bool force = false)
        {
            if (!force && Time.realtimeSinceStartup < nextTimeCheck) return;
            nextTimeCheck = Time.realtimeSinceStartup + 1f;
            DateTime local = localTimeProvider == null ? DateTime.Now : localTimeProvider();
            if (!force && local.Ticks / TimeSpan.TicksPerMinute == LightingLocalTime.Ticks / TimeSpan.TicksPerMinute)
                return;

            LightingLocalTime = local;
            MainMenuLightingState state = MainMenuLightingTime.Evaluate(local);
            LightingState = state;
            keyLight.intensity = state.KeyLux;
            keyLight.color = TimeColour(state, new Color(1f, 0.87f, 0.72f),
                new Color(1f, 0.56f, 0.30f), new Color(0.72f, 0.80f, 1f));
            // The finite menu key follows an artistic daily arc. At night the
            // same isolated light supplies restrained cool moonlight.
            keyLight.transform.localRotation = Quaternion.Euler(
                Mathf.Max(12f, state.SunElevationDegrees), state.SunAzimuthDegrees - 150f, 0f);
            Vector3 target = model.transform.TransformPoint(model.LocalBounds.center);
            keyLight.transform.position = target - keyLight.transform.forward * 35f;
            fillLight.intensity = state.FillLux;
            fillLight.color = TimeColour(state, new Color(0.76f, 0.85f, 1f),
                new Color(0.72f, 0.79f, 1f), new Color(0.78f, 0.86f, 1f));
            menuSky.top.Override(TimeColour(state, new Color(0.13f, 0.25f, 0.44f),
                new Color(0.12f, 0.15f, 0.31f), new Color(0.07f, 0.12f, 0.23f)));
            menuSky.middle.Override(TimeColour(state, new Color(0.45f, 0.52f, 0.62f),
                new Color(0.70f, 0.34f, 0.19f), new Color(0.20f, 0.28f, 0.44f)));
            menuSky.bottom.Override(TimeColour(state, new Color(0.22f, 0.27f, 0.34f),
                new Color(0.22f, 0.17f, 0.24f), new Color(0.085f, 0.12f, 0.20f)));
            menuSky.multiplier.Override(state.SkyIntensity);
            // HDRP volumetrics receive global directional illumination even
            // with mesh light layers. Grade this owned haze locally so a warm
            // gameplay sun cannot leave the menu's night horizon brown.
            rearFog.parameters.albedo = TimeColour(state, new Color(0.70f, 0.78f, 0.86f),
                new Color(0.35f, 0.48f, 0.70f), new Color(0.025f, 0.10f, 0.28f));
            garageLampLight.intensity = GarageSpotIntensityCandela * state.LampFactor;
            vehicleLights.Apply(state);
            lampEmission.SetVector("_EmissiveColor", new Vector4(6000f, 6000f, 6000f, 1f) * state.LampFactor);
            environment.GarageLampRenderer.SetPropertyBlock(lampEmission, environment.GarageLampMaterialIndex);
            // Wall-clock polling allocates no per-frame objects. An unchanged
            // minute needs no render or blur; a new minute warms the sky once.
            pendingFrames = Math.Max(pendingFrames, 2);
            readinessDeadline = Time.realtimeSinceStartup + 10f;
            skySettleUntil = Time.realtimeSinceStartup + 0.25f;
            IsReady = false;
        }

        private static Color TimeColour(MainMenuLightingState state, Color day, Color twilight, Color night) =>
            day * state.Daylight + twilight * state.Twilight + night * state.Night;

        private void RecreateOutput(float aspect)
        {
            int height = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(1080f, 1920f / aspect)), 1, 1080);
            int width = Mathf.Clamp(Mathf.RoundToInt(height * aspect), 1, 1920);
            previewCamera.aspect = width / (float)height;
            if (output != null && output.width == width && output.height == height) return;
            RenderTexture old = output;
            // An opaque RGB target makes the full 3D sky/environment visible in
            // uGUI regardless of the HDRP profile's intermediate alpha format.
            output = new RenderTexture(width, height, 24, RenderTextureFormat.RGB111110Float, RenderTextureReadWrite.Linear)
            {
                name = "MainMenu_WorldComposite", hideFlags = HideFlags.DontSave,
                antiAliasing = 1, useMipMap = false, autoGenerateMips = false,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            output.Create();
            if (old != null) { old.Release(); Destroy(old); }
        }

        private void FitCamera()
        {
            // One radius for the entire permitted arc avoids zoom pumping
            // while dragging. Refit only when the viewport changes.
            orbitDistance = RequiredCameraDistance(Vector2.zero);
            const int yawSteps = 24;
            const int pitchSteps = 4;
            for (int yaw = 0; yaw <= yawSteps; yaw++)
            for (int pitch = 0; pitch <= pitchSteps; pitch++)
                orbitDistance = Mathf.Max(orbitDistance, RequiredCameraDistance(new Vector2(
                    Mathf.Lerp(MinOrbitYaw, MaxOrbitYaw, yaw / (float)yawSteps),
                    Mathf.Lerp(MinOrbitPitch, MaxOrbitPitch, pitch / (float)pitchSteps))));
            orbitDistance *= 1.02f;
            PositionCamera();
        }

        private Vector3 OrbitDirection(Vector2 angles)
        {
            Vector3 direction = environment.transform.TransformDirection(environment.CameraFromVehicleDirection).normalized;
            Vector3 up = environment.transform.up;
            direction = Quaternion.AngleAxis(angles.x, up) * direction;
            Vector3 right = Vector3.Cross(up, -direction).normalized;
            return Quaternion.AngleAxis(angles.y, right) * direction;
        }

        private float RequiredCameraDistance(Vector2 angles)
        {
            Vector3 direction = OrbitDirection(angles);
            Quaternion rotation = Quaternion.LookRotation(-direction, environment.transform.up);
            Quaternion inverse = Quaternion.Inverse(rotation);
            Bounds bounds = model.LocalBounds;
            Vector3 center = model.transform.TransformPoint(bounds.center);
            float tangentY = Mathf.Tan(previewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float tangentX = tangentY * previewCamera.aspect;
            float distance = 4.5f;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 sign = new Vector3((corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                Vector3 point = inverse * (model.transform.TransformPoint(
                    bounds.center + Vector3.Scale(bounds.extents, sign)) - center);
                // Perspective inequalities fit every corner inside the car's
                // viewport slot (x .13-.62, y .175-.795), left of the actions.
                distance = Mathf.Max(distance, (point.x - 0.24f * tangentX * point.z) / (0.49f * tangentX));
                distance = Mathf.Max(distance, (point.x + 0.74f * tangentX * point.z) / (-0.49f * tangentX));
                distance = Mathf.Max(distance, (point.y - 0.59f * tangentY * point.z) / (0.62f * tangentY));
                distance = Mathf.Max(distance, (point.y + 0.65f * tangentY * point.z) / (-0.62f * tangentY));
            }
            return distance;
        }

        private void PositionCamera()
        {
            Vector3 center = model.transform.TransformPoint(model.LocalBounds.center);
            Vector3 fromTarget = OrbitDirection(orbitAngles);
            previewCamera.transform.rotation = Quaternion.LookRotation(-fromTarget, environment.transform.up);
            float height = 2f * orbitDistance * Mathf.Tan(previewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            previewCamera.transform.position = center + fromTarget * orbitDistance +
                previewCamera.transform.right * (height * previewCamera.aspect * 0.125f) +
                previewCamera.transform.up * (height * 0.015f);
            // The broad, shadowless camera-side bounce keeps shaded paint
            // readable throughout the arc. The warm yard key stays stationary.
            if (vehicleFill != null)
                vehicleFill.SetPositionAndRotation(center + fromTarget * 8f,
                    Quaternion.LookRotation(-fromTarget, environment.transform.up));
        }

        private GameObject Child(string name)
        {
            var child = new GameObject(name) { layer = PreviewLayer, hideFlags = HideFlags.DontSave };
            child.transform.SetParent(stage.transform, false);
            return child;
        }

        private static void ValidatePresentationOnly(MonoBehaviour wrapper)
        {
            foreach (Component component in wrapper.GetComponentsInChildren<Component>(true))
                if (component == null || !(component is Transform || component is MeshFilter ||
                    component is MeshRenderer || component == wrapper))
                    throw new InvalidOperationException("Menu prefabs may contain only their typed wrapper and static mesh presentation.");
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            visible = false;
            IsReady = false;
            FrameReady = null;
            if (stage != null) { stage.SetActive(false); Destroy(stage); }
            if (output != null) { output.Release(); Destroy(output); }
            if (volumeProfile != null)
            {
                foreach (VolumeComponent component in volumeProfile.components) if (component != null) Destroy(component);
                Destroy(volumeProfile);
            }
            output = null;
            model = null;
            environment = null;
            previewCamera = null;
            vehicleFill = null;
            garageLampLight = null;
            vehicleLights = null;
            rearFog = null;
            keyLight = null;
            fillLight = null;
            menuSky = null;
            lampEmission = null;
            localTimeProvider = null;
            stage = null;
        }

        private void OnDestroy() => Dispose();
    }
}
