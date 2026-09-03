using System;
using System.Collections;
using System.Collections.Generic;
using MSC.Core.Time;
using MSC.World.Streaming;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.World.Lighting
{
    public readonly struct WorldRuntimeLightRegistration
    {
        public WorldRuntimeLightRegistration(
            Light light,
            WorldLightDefinition definition)
        {
            Light = light;
            Definition = definition;
        }

        public Light Light { get; }
        public WorldLightDefinition Definition { get; }
    }

    /// <summary>
    /// Marker added by a project-owned higher-level lighting authority. The
    /// accepted 09C spawner keeps streaming ownership while activation and
    /// shadow budgets move to that authority.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldRuntimeLightActivationOverride : MonoBehaviour
    {
    }

    [DisallowMultipleComponent]
    public sealed class WorldLightingProbeRuntime : MonoBehaviour
    {
        public static event Action<WorldRuntimeLightRegistration>
            RuntimeLightCreated;

        private const float NightStatePollIntervalSeconds = 0.25f;
        private const float DynamicShadowBudgetPollIntervalSeconds = 0.20f;
        private const float LoadedCellReconcileIntervalSeconds = 1f;
        private const float MaximumDynamicShadowDistanceMeters = 32f;
        private const int MaximumDynamicShadowLights = 4;
        private const int MaximumDynamicShadowFaces = 6;
        private const int SpotShadowResolution = 256;
        private const int PointShadowResolution = 128;

        private readonly Dictionary<int, GameObject> sceneRoots =
            new Dictionary<int, GameObject>();
        private readonly List<RuntimeLight> runtimeLights =
            new List<RuntimeLight>();
        private readonly RuntimeLight[] dynamicShadowSelection =
            new RuntimeLight[MaximumDynamicShadowLights];
        private readonly Queue<HDAdditionalReflectionData>
            pendingProbeRenders =
                new Queue<HDAdditionalReflectionData>();

        private WorldLightingProbeCatalog catalog;
        private ProductionWorldStreamingService streaming;
        private GameTimeService gameTime;
        private Transform shadowFocus;
        private float nextNightStatePollTime;
        private float nextDynamicShadowBudgetPollTime;
        private float nextLoadedCellReconcileTime;
        private bool initialized;
        private bool isNight;

        public int ActiveLightCount => runtimeLights.Count;
        public int ActiveProbeCount => CountActiveProbes();
        public int ActiveDynamicShadowLightCount { get; private set; }

        public void ReplayRuntimeLights(
            Action<WorldRuntimeLightRegistration> receiver)
        {
            if (receiver == null)
            {
                return;
            }

            for (int index = 0; index < runtimeLights.Count; index++)
            {
                RuntimeLight runtime = runtimeLights[index];
                if (runtime.Light != null)
                {
                    receiver(new WorldRuntimeLightRegistration(
                        runtime.Light,
                        runtime.Definition));
                }
            }
        }

        public void ReplayRuntimeLights(
            Scene scene,
            Action<WorldRuntimeLightRegistration> receiver)
        {
            if (!scene.IsValid() || receiver == null)
            {
                return;
            }

            for (int index = 0; index < runtimeLights.Count; index++)
            {
                RuntimeLight runtime = runtimeLights[index];
                if (runtime.Light != null &&
                    runtime.Light.gameObject.scene.handle == scene.handle)
                {
                    receiver(new WorldRuntimeLightRegistration(
                        runtime.Light,
                        runtime.Definition));
                }
            }
        }

        /// <summary>
        /// Reconciles every currently loaded manifest cell against the
        /// authoritative lighting catalog. Scene callbacks are notifications,
        /// not durable state: an exception in a presentation subscriber or an
        /// unusual additive-load order must not leave a cell permanently dark.
        /// </summary>
        public void ReconcileLoadedCellScenes()
        {
            if (!initialized)
            {
                return;
            }

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded)
                {
                    HandleOwnedSceneLoaded(scene);
                }
            }
        }

        public void Initialize(
            WorldLightingProbeCatalog configuredCatalog,
            ProductionWorldStreamingService configuredStreaming,
            GameTimeService configuredGameTime)
        {
            Initialize(
                configuredCatalog,
                configuredStreaming,
                configuredGameTime,
                null);
        }

        public void Initialize(
            WorldLightingProbeCatalog configuredCatalog,
            ProductionWorldStreamingService configuredStreaming,
            GameTimeService configuredGameTime,
            Transform configuredShadowFocus)
        {
            if (initialized)
            {
                throw new InvalidOperationException(
                    "World lighting runtime is already initialized.");
            }

            catalog = configuredCatalog ??
                throw new ArgumentNullException(nameof(configuredCatalog));
            streaming = configuredStreaming ??
                throw new ArgumentNullException(nameof(configuredStreaming));
            gameTime = configuredGameTime ??
                throw new ArgumentNullException(nameof(configuredGameTime));
            shadowFocus = configuredShadowFocus;
            catalog.Validate();

            isNight = DetermineNightState(gameTime.Snapshot);
            streaming.OwnedSceneLoaded += HandleOwnedSceneLoaded;
            streaming.OwnedSceneWillUnload += HandleOwnedSceneWillUnload;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            initialized = true;
            ReplayAlreadyLoadedCellScenes();
        }

        private void Update()
        {
            if (!initialized)
            {
                return;
            }

            if (Time.unscaledTime >= nextLoadedCellReconcileTime)
            {
                nextLoadedCellReconcileTime =
                    Time.unscaledTime + LoadedCellReconcileIntervalSeconds;
                ReconcileLoadedCellScenes();
            }

            if (Time.unscaledTime >= nextNightStatePollTime)
            {
                nextNightStatePollTime =
                    Time.unscaledTime + NightStatePollIntervalSeconds;
                bool nextNight = DetermineNightState(gameTime.Snapshot);
                if (nextNight != isNight)
                {
                    isNight = nextNight;
                    RefreshLightActivation();
                    QueueAllProbeRenders();
                }
            }

            if (Time.unscaledTime >= nextDynamicShadowBudgetPollTime)
            {
                nextDynamicShadowBudgetPollTime =
                    Time.unscaledTime +
                    DynamicShadowBudgetPollIntervalSeconds;
                RefreshDynamicShadowBudget();
            }

            if (pendingProbeRenders.Count > 0)
            {
                HDAdditionalReflectionData probe =
                    pendingProbeRenders.Dequeue();
                if (probe != null && probe.isActiveAndEnabled)
                {
                    probe.RequestRenderNextUpdate();
                }
            }
        }

        private void OnDestroy()
        {
            if (streaming != null)
            {
                streaming.OwnedSceneLoaded -= HandleOwnedSceneLoaded;
                streaming.OwnedSceneWillUnload -= HandleOwnedSceneWillUnload;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;

            sceneRoots.Clear();
            runtimeLights.Clear();
            Array.Clear(
                dynamicShadowSelection,
                0,
                dynamicShadowSelection.Length);
            pendingProbeRenders.Clear();
            shadowFocus = null;
            ActiveDynamicShadowLightCount = 0;
            initialized = false;
        }

        private void HandleOwnedSceneLoaded(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded ||
                !streaming.TryGetCellIdForScene(scene, out string cellId))
            {
                return;
            }

            if (sceneRoots.TryGetValue(
                    scene.handle,
                    out GameObject existingRoot))
            {
                if (existingRoot != null)
                {
                    ReconcileSceneLights(
                        scene,
                        existingRoot.transform,
                        cellId);
                    RefreshDynamicShadowBudget();
                    return;
                }

                sceneRoots.Remove(scene.handle);
            }

            var root = new GameObject(
                "WorldLighting_" + cellId.Replace('-', 'N'));
            SceneManager.MoveGameObjectToScene(root, scene);
            sceneRoots.Add(scene.handle, root);

            for (int index = 0; index < catalog.Lights.Count; index++)
            {
                WorldLightDefinition definition = catalog.Lights[index];
                if (!string.Equals(
                        definition.CellId,
                        cellId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                TryCreateLight(root.transform, definition);
            }

            for (int index = 0; index < catalog.Probes.Count; index++)
            {
                WorldReflectionProbeDefinition definition =
                    catalog.Probes[index];
                if (!string.Equals(
                        definition.CellId,
                        cellId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                CreateProbe(root.transform, definition);
            }

            RefreshDynamicShadowBudget();
        }

        private void ReconcileSceneLights(
            Scene scene,
            Transform root,
            string cellId)
        {
            RemoveDestroyedRuntimeLights();
            for (int index = 0; index < catalog.Lights.Count; index++)
            {
                WorldLightDefinition definition = catalog.Lights[index];
                if (!string.Equals(
                        definition.CellId,
                        cellId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                RuntimeLight runtime = FindRuntimeLight(
                    scene.handle,
                    definition.LightId);
                if (runtime == null)
                {
                    TryCreateLight(root, definition);
                    continue;
                }

                // A subscriber can fail after the HDRP Light has already been
                // created. Re-publish only until the production adapter marks
                // the hand-off complete; its own manager registration is
                // independently replayed by the adapter.
                runtime.ExternalAuthority = runtime.Light.GetComponent<
                    WorldRuntimeLightActivationOverride>() != null;
                if (!runtime.ExternalAuthority)
                {
                    PublishRuntimeLightCreated(runtime);
                    runtime.ExternalAuthority = runtime.Light.GetComponent<
                        WorldRuntimeLightActivationOverride>() != null;
                }
            }
        }

        private RuntimeLight FindRuntimeLight(
            int sceneHandle,
            string lightId)
        {
            for (int index = 0; index < runtimeLights.Count; index++)
            {
                RuntimeLight runtime = runtimeLights[index];
                if (runtime.Light != null &&
                    runtime.Light.gameObject.scene.handle == sceneHandle &&
                    string.Equals(
                        runtime.Definition.LightId,
                        lightId,
                        StringComparison.Ordinal))
                {
                    return runtime;
                }
            }

            return null;
        }

        private void RemoveDestroyedRuntimeLights()
        {
            for (int index = runtimeLights.Count - 1; index >= 0; index--)
            {
                if (runtimeLights[index].Light == null)
                {
                    runtimeLights.RemoveAt(index);
                }
            }
        }

        private void TryCreateLight(
            Transform parent,
            WorldLightDefinition definition)
        {
            GameObject attemptedLightObject = null;
            try
            {
                CreateLightCore(parent, definition, out attemptedLightObject);
            }
            catch (Exception exception)
            {
                RemoveRuntimeLight(attemptedLightObject);
                if (attemptedLightObject != null)
                {
                    attemptedLightObject.SetActive(false);
                    Destroy(attemptedLightObject);
                }

                Debug.LogError(
                    $"[WorldLighting] Could not create fixture " +
                    $"'{definition.LightId}' in cell " +
                    $"'{definition.CellId}'. It will be retried.",
                    this);
                Debug.LogException(exception, this);
            }
        }

        private void RemoveRuntimeLight(GameObject lightObject)
        {
            if (lightObject == null)
            {
                return;
            }

            for (int index = runtimeLights.Count - 1; index >= 0; index--)
            {
                Light light = runtimeLights[index].Light;
                if (light == null || light.gameObject == lightObject)
                {
                    runtimeLights.RemoveAt(index);
                }
            }
        }

        private void ReplayAlreadyLoadedCellScenes()
        {
            // World cells are commonly kept open additively while authoring.
            // The streaming service deliberately does not claim or re-emit a
            // load event for those scenes, so runtime consumers must replay
            // their current loaded state just like the door installer does.
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded)
                {
                    HandleOwnedSceneLoaded(scene);
                }
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            HandleOwnedSceneLoaded(scene);
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            RemoveSceneRuntime(scene.handle, false);
        }

        private void HandleOwnedSceneWillUnload(Scene scene)
        {
            RemoveSceneRuntime(scene.handle, true);
        }

        private void RemoveSceneRuntime(
            int sceneHandle,
            bool destroyRoot)
        {
            sceneRoots.Remove(sceneHandle, out GameObject root);

            for (int index = runtimeLights.Count - 1; index >= 0; index--)
            {
                RuntimeLight runtime = runtimeLights[index];
                if (runtime.Light == null ||
                    runtime.Light.gameObject.scene.handle == sceneHandle)
                {
                    runtimeLights.RemoveAt(index);
                }
            }

            if (destroyRoot && root != null)
            {
                // Disable first so streamed GameLightFixture components
                // unregister synchronously. Destroy is deferred until end of
                // frame; without this, a same-cell reload can register the new
                // fixture IDs before the old root receives OnDisable.
                root.SetActive(false);
                Destroy(root);
            }

            RefreshDynamicShadowBudget();
        }

        private void CreateLight(
            Transform parent,
            WorldLightDefinition definition)
        {
            CreateLightCore(parent, definition, out _);
        }

        private void CreateLightCore(
            Transform parent,
            WorldLightDefinition definition,
            out GameObject lightObject)
        {
            bool isStreetLight =
                definition.ActivationPolicy ==
                WorldLightActivationPolicy.NightOnly;
            bool isSpotLight =
                definition.SourceKind == WorldLightSourceKind.Spot;
            lightObject = new GameObject(
                isStreetLight
                    ? "Street Light (Spot) · " + ShortId(definition.LightId)
                    : "Interior Light · " + ShortId(definition.LightId));
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.position = definition.Position;
            // Rotation is irrelevant for points, but the production lighting
            // adapter may promote the generated Light to a tube or rectangle.
            // Preserve the authored pose before that hand-off.
            lightObject.transform.rotation = Quaternion.Euler(
                definition.RotationEulerAngles);

            HDAdditionalLightData hdLight =
                lightObject.AddHDLight(
                    isSpotLight ? LightType.Spot : LightType.Point);
            Light light = hdLight.GetComponent<Light>();
#if UNITY_EDITOR
            // Bake mode is an Editor authoring setting; Unity omits this API
            // from players. Runtime-created lights have no baked contribution.
            light.lightmapBakeType = ResolveBakeType(
                definition.BakeMode);
#endif
            light.color = definition.Color;
            light.lightUnit = definition.IntensityIsLux
                ? LightUnit.Lux
                : LightUnit.Lumen;
            light.intensity = definition.Intensity;
            light.luxAtDistance = definition.LuxAtDistance;
            light.useColorTemperature =
                definition.ColorTemperatureKelvin > 0f;
            if (light.useColorTemperature)
            {
                light.colorTemperature =
                    definition.ColorTemperatureKelvin;
            }

            light.bounceIntensity = definition.IndirectMultiplier;
            light.range = definition.Range;
            hdLight.shapeRadius = definition.ShapeRadius;
            hdLight.affectsVolumetric = definition.VolumetricEnabled;
            hdLight.volumetricDimmer = definition.VolumetricDimmer;
            hdLight.volumetricShadowDimmer = definition.VolumetricDimmer;
            if (isSpotLight)
            {
                light.spotAngle = definition.SpotOuterAngle;
                light.innerSpotAngle = definition.SpotInnerAngle;
            }

            light.shadows = definition.CastsShadows
                ? LightShadows.Soft
                : LightShadows.None;
            if (definition.CastsShadows)
            {
                // The donor house uses thin paired roof/wall shells. HDRP's
                // default normal bias visibly pushed local shadows through
                // those shells, so streamed fixture lights use a restrained
                // bias and a close near plane.
                light.shadowBias = 0.02f;
                light.shadowNormalBias = 0.15f;
                light.shadowNearPlane = 0.05f;
                hdLight.normalBias = 0.15f;
                hdLight.slopeBias = 0.25f;
                hdLight.shadowNearPlane = 0.05f;
                int shadowResolution = isSpotLight
                    ? SpotShadowResolution
                    : PointShadowResolution;
                if (definition.Range > 100f)
                {
                    shadowResolution = 512;
                }

                hdLight.SetShadowResolution(shadowResolution);
                hdLight.SetShadowResolutionOverride(true);
                // Runtime-created cached lights can be registered twice while
                // additive cells integrate under Unity 6.3 HDRP. The bounded
                // dynamic-shadow selector below owns the safe realtime budget.
                hdLight.shadowUpdateMode = ShadowUpdateMode.EveryFrame;
                hdLight.alwaysDrawDynamicShadows = false;
            }

            light.renderMode = LightRenderMode.Auto;
            light.enabled =
                definition.ActivationPolicy ==
                    WorldLightActivationPolicy.AlwaysOn ||
                isNight;
            var runtimeLight = new RuntimeLight(
                light,
                hdLight,
                definition,
                definition.ActivationPolicy,
                definition.CastsShadows,
                isSpotLight ? 1 : 6);
            runtimeLights.Add(runtimeLight);
            PublishRuntimeLightCreated(runtimeLight);
            runtimeLight.ExternalAuthority = light.GetComponent<
                WorldRuntimeLightActivationOverride>() != null;
            if (runtimeLight.ExternalAuthority)
            {
                // The production lighting manager applies its own four-light
                // realtime budget on the next refresh. Do not expose an
                // unbudgeted shadow caster between registration and that pass.
                light.shadows = LightShadows.None;
                runtimeLight.DynamicShadowsEnabled = false;
            }
        }

        private void PublishRuntimeLightCreated(RuntimeLight runtime)
        {
            Action<WorldRuntimeLightRegistration> receivers =
                RuntimeLightCreated;
            if (receivers == null || runtime?.Light == null)
            {
                return;
            }

            var registration = new WorldRuntimeLightRegistration(
                runtime.Light,
                runtime.Definition);
            Delegate[] invocationList = receivers.GetInvocationList();
            for (int index = 0; index < invocationList.Length; index++)
            {
                try
                {
                    ((Action<WorldRuntimeLightRegistration>)
                        invocationList[index]).Invoke(registration);
                }
                catch (Exception exception)
                {
                    // One presentation integration must not abort the rest of
                    // a streamed cell's physical light creation. The missing
                    // hand-off is retried by the reconciliation pass.
                    Debug.LogError(
                        $"[WorldLighting] Fixture subscriber rejected " +
                        $"'{runtime.Definition.LightId}'. It will be retried.",
                        runtime.Light);
                    Debug.LogException(exception, runtime.Light);
                }
            }
        }

        private static LightmapBakeType ResolveBakeType(
            WorldLightBakeMode bakeMode)
        {
            switch (bakeMode)
            {
                case WorldLightBakeMode.Realtime:
                    return LightmapBakeType.Realtime;
                case WorldLightBakeMode.Baked:
                    return LightmapBakeType.Baked;
                case WorldLightBakeMode.Mixed:
                    return LightmapBakeType.Mixed;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(bakeMode),
                        bakeMode,
                        "Unsupported world-light bake mode.");
            }
        }

        private void CreateProbe(
            Transform parent,
            WorldReflectionProbeDefinition definition)
        {
            var probeObject = new GameObject(
                "Reflection Probe · " + ShortId(definition.ProbeId));
            probeObject.transform.SetParent(parent, false);
            probeObject.transform.position = definition.Position;
            ReflectionProbe probe =
                probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode =
                ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.boxProjection = true;
            probe.size = definition.Size;
            probe.blendDistance = Mathf.Min(
                Mathf.Min(definition.Size.x, definition.Size.z) * 0.15f,
                0.75f);
            probe.intensity = definition.Intensity;
            probe.resolution = definition.Resolution;
            probe.importance = 10;
            HDAdditionalReflectionData hdProbe =
                probeObject.AddComponent<HDAdditionalReflectionData>();
            hdProbe.mode = ProbeSettings.Mode.Realtime;
            hdProbe.realtimeMode = ProbeSettings.RealtimeMode.OnDemand;
            hdProbe.timeSlicing = true;
            hdProbe.influenceVolume.shape = InfluenceShape.Box;
            hdProbe.influenceVolume.boxSize = definition.Size;
            Vector3 blendDistance = new Vector3(
                Mathf.Min(definition.Size.x * 0.15f, 0.75f),
                Mathf.Min(definition.Size.y * 0.15f, 0.5f),
                Mathf.Min(definition.Size.z * 0.15f, 0.75f));
            hdProbe.influenceVolume.boxBlendDistancePositive =
                blendDistance;
            hdProbe.influenceVolume.boxBlendDistanceNegative =
                blendDistance;
            hdProbe.influenceVolume.boxBlendNormalDistancePositive =
                blendDistance * 0.75f;
            hdProbe.influenceVolume.boxBlendNormalDistanceNegative =
                blendDistance * 0.75f;
            hdProbe.multiplier = definition.Intensity;
            hdProbe.importance = 10;
            ref ProbeSettings rawSettings = ref hdProbe.settingsRaw;
            rawSettings.proxySettings.useInfluenceVolumeAsProxyVolume = true;
            rawSettings.cameraSettings.customRenderingSettings = true;
            ref FrameSettings frameSettings = ref hdProbe.frameSettings;
            ref FrameSettingsOverrideMask overrideMask =
                ref hdProbe.frameSettingsOverrideMask;
            DisableProbeCaptureFeature(
                ref frameSettings,
                ref overrideMask,
                FrameSettingsField.AtmosphericScattering);
            DisableProbeCaptureFeature(
                ref frameSettings,
                ref overrideMask,
                FrameSettingsField.Volumetrics);
            DisableProbeCaptureFeature(
                ref frameSettings,
                ref overrideMask,
                FrameSettingsField.ReprojectionForVolumetrics);
            pendingProbeRenders.Enqueue(hdProbe);
        }

        private static void DisableProbeCaptureFeature(
            ref FrameSettings frameSettings,
            ref FrameSettingsOverrideMask overrideMask,
            FrameSettingsField field)
        {
            frameSettings.SetEnabled(field, false);
            overrideMask.mask[(uint)field] = true;
        }

        private void RefreshLightActivation()
        {
            for (int index = 0; index < runtimeLights.Count; index++)
            {
                RuntimeLight runtime = runtimeLights[index];
                if (runtime.Light != null)
                {
                    if (runtime.ExternalAuthority)
                    {
                        continue;
                    }

                    runtime.Light.enabled =
                        runtime.ActivationPolicy ==
                            WorldLightActivationPolicy.AlwaysOn ||
                        isNight;
                }
            }

            RefreshDynamicShadowBudget();
        }

        private void RefreshDynamicShadowBudget()
        {
            Array.Clear(
                dynamicShadowSelection,
                0,
                dynamicShadowSelection.Length);
            ActiveDynamicShadowLightCount = 0;

            if (shadowFocus != null && shadowFocus.gameObject.activeInHierarchy)
            {
                int usedShadowFaces = 0;
                for (int selectionIndex = 0;
                     selectionIndex < dynamicShadowSelection.Length;
                     selectionIndex++)
                {
                    RuntimeLight best = null;
                    float bestDistanceSquared = float.PositiveInfinity;

                    for (int lightIndex = 0;
                         lightIndex < runtimeLights.Count;
                         lightIndex++)
                    {
                        RuntimeLight candidate = runtimeLights[lightIndex];
                        if (!CanUseDynamicShadows(
                                candidate,
                                usedShadowFaces) ||
                            IsAlreadySelected(candidate))
                        {
                            continue;
                        }

                        float distanceSquared =
                            (candidate.Light.transform.position -
                             shadowFocus.position).sqrMagnitude;
                        float maximumDistance = Mathf.Min(
                            MaximumDynamicShadowDistanceMeters,
                            Mathf.Max(12f, candidate.Light.range + 4f));
                        if (distanceSquared >
                                maximumDistance * maximumDistance ||
                            distanceSquared >= bestDistanceSquared)
                        {
                            continue;
                        }

                        best = candidate;
                        bestDistanceSquared = distanceSquared;
                    }

                    if (best == null)
                    {
                        break;
                    }

                    dynamicShadowSelection[selectionIndex] = best;
                    usedShadowFaces += best.ShadowFaceCost;
                    ActiveDynamicShadowLightCount++;
                }
            }

            for (int index = 0; index < runtimeLights.Count; index++)
            {
                RuntimeLight runtime = runtimeLights[index];
                bool shouldDrawDynamicShadows =
                    IsAlreadySelected(runtime);
                if (runtime.DynamicShadowsEnabled ==
                    shouldDrawDynamicShadows)
                {
                    continue;
                }

                runtime.DynamicShadowsEnabled =
                    shouldDrawDynamicShadows;
                if (runtime.Light != null)
                {
                    runtime.Light.shadows = shouldDrawDynamicShadows
                        ? LightShadows.Soft
                        : LightShadows.None;
                }

                if (runtime.AdditionalData != null)
                {
                    runtime.AdditionalData.shadowUpdateMode =
                        ShadowUpdateMode.EveryFrame;
                    runtime.AdditionalData.alwaysDrawDynamicShadows = false;
                }
            }
        }

        private bool CanUseDynamicShadows(
            RuntimeLight candidate,
            int usedShadowFaces)
        {
            return candidate != null &&
                   candidate.Light != null &&
                   !candidate.ExternalAuthority &&
                   candidate.Light.enabled &&
                   candidate.WantsShadows &&
                   candidate.AdditionalData != null &&
                   usedShadowFaces + candidate.ShadowFaceCost <=
                       MaximumDynamicShadowFaces;
        }

        private bool IsAlreadySelected(RuntimeLight candidate)
        {
            for (int index = 0;
                 index < dynamicShadowSelection.Length;
                 index++)
            {
                if (ReferenceEquals(
                        dynamicShadowSelection[index],
                        candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private void QueueAllProbeRenders()
        {
            pendingProbeRenders.Clear();
            foreach (GameObject root in sceneRoots.Values)
            {
                if (root == null)
                {
                    continue;
                }

                HDAdditionalReflectionData[] probes =
                    root.GetComponentsInChildren<
                        HDAdditionalReflectionData>(true);
                for (int index = 0; index < probes.Length; index++)
                {
                    pendingProbeRenders.Enqueue(probes[index]);
                }
            }
        }

        private bool DetermineNightState(GameTimeSnapshot snapshot)
        {
            double normalized =
                snapshot.SecondsOfDay / 86_400d;
            return normalized < gameTime.Config.SunriseNormalized01 ||
                   normalized >= gameTime.Config.SunsetNormalized01;
        }

        private int CountActiveProbes()
        {
            int count = 0;
            foreach (GameObject root in sceneRoots.Values)
            {
                if (root != null)
                {
                    count += root.GetComponentsInChildren<
                            HDAdditionalReflectionData>(true)
                        .Length;
                }
            }

            return count;
        }

        private static string ShortId(string stableId)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                return "Unnamed";
            }

            int separator = stableId.LastIndexOf('.');
            string suffix = separator >= 0
                ? stableId.Substring(separator + 1)
                : stableId;
            return suffix.Length <= 12
                ? suffix
                : suffix.Substring(0, 12);
        }

        private sealed class RuntimeLight
        {
            public RuntimeLight(
                Light light,
                HDAdditionalLightData additionalData,
                WorldLightDefinition definition,
                WorldLightActivationPolicy activationPolicy,
                bool wantsShadows,
                int shadowFaceCost)
            {
                Light = light;
                AdditionalData = additionalData;
                Definition = definition;
                ActivationPolicy = activationPolicy;
                WantsShadows = wantsShadows;
                ShadowFaceCost = shadowFaceCost;
                DynamicShadowsEnabled = wantsShadows;
            }

            public Light Light { get; }
            public HDAdditionalLightData AdditionalData { get; }
            public WorldLightDefinition Definition { get; }
            public WorldLightActivationPolicy ActivationPolicy { get; }
            public bool WantsShadows { get; }
            public int ShadowFaceCost { get; }
            public bool DynamicShadowsEnabled { get; set; }
            public bool ExternalAuthority { get; set; }
        }
    }
}
