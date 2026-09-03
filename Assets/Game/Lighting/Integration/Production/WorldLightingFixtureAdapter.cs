using System;
using System.Collections;
using System.Collections.Generic;
using MSC.LegacyImport;
using MSC.World.Lighting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Lighting.Production
{
    [DefaultExecutionOrder(-31000)]
    [DisallowMultipleComponent]
    public sealed class WorldLightingFixtureAdapter : MonoBehaviour
    {
        // The donor home has no baked interior bounce, so its previously
        // accepted Phase 1 compensation remains explicit. Applying that boost
        // to every commercial room overexposes dense banks such as the four
        // inspection-office fluorescents and increases leakage through the
        // temporary thin shell geometry.
        private const float RuntimeBaselineHomeIntensityMultiplier = 1.5f;
        // Three 1.8 klm donor point lights are standing in for fluorescent
        // area fixtures in a garage with no baked bounce. The additional
        // bounded compensation yields roughly 16.2 klm per fixture, so the
        // switch changes actual room illumination instead of relying on an
        // artificial room-exposure override.
        private const float RuntimeBaselineGarageIntensityMultiplier = 6f;
        private const float RuntimeLightReconcileIntervalSeconds = 1f;
        private const string HomeCircuitPrefix = "grid.home.";
        private const string GarageCircuitId = "grid.home.garage";

        [SerializeField] private LightingProfileCatalog profileCatalog;
        [SerializeField] private WorldLightingBindingCatalog bindingCatalog;

        private readonly Dictionary<int, SceneFixtureIndex> fixtureIndexes =
            new Dictionary<int, SceneFixtureIndex>();
        private bool subscribed;
        private Material runtimeEmissionMaterial;
        private float nextRuntimeLightReconcileTime;

        public int BoundRuntimeLightCount { get; private set; }
        public int RejectedRuntimeLightCount { get; private set; }

        private void OnEnable()
        {
            if (!Application.isPlaying || subscribed)
            {
                return;
            }

            ValidateConfiguration();
            WorldLightingProbeRuntime.RuntimeLightCreated +=
                HandleRuntimeLightCreated;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            subscribed = true;
            StartCoroutine(ReplayExistingRuntimeLights());
        }

        private void OnDisable()
        {
            if (!subscribed)
            {
                return;
            }

            WorldLightingProbeRuntime.RuntimeLightCreated -=
                HandleRuntimeLightCreated;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            fixtureIndexes.Clear();
            subscribed = false;
        }

        private void Update()
        {
            if (!subscribed ||
                Time.unscaledTime < nextRuntimeLightReconcileTime)
            {
                return;
            }

            nextRuntimeLightReconcileTime = Time.unscaledTime +
                RuntimeLightReconcileIntervalSeconds;
            WorldLightingProbeRuntime runtime = GetComponent<
                WorldLightingProbeRuntime>();
            runtime?.ReplayRuntimeLights(HandleRuntimeLightCreated);
        }

        private void OnDestroy()
        {
            if (runtimeEmissionMaterial != null)
            {
                Destroy(runtimeEmissionMaterial);
                runtimeEmissionMaterial = null;
            }
        }

        private void HandleRuntimeLightCreated(
            WorldRuntimeLightRegistration registration)
        {
            if (registration.Light == null || registration.Definition == null ||
                !bindingCatalog.TryGet(
                    registration.Definition.LightId,
                    out WorldLightingFixtureBinding binding))
            {
                RejectedRuntimeLightCount++;
                Debug.LogError(
                    "[Lighting] Generated world light has no stable fixture binding.",
                    registration.Light);
                return;
            }

            GameLightFixture existingFixture =
                registration.Light.GetComponent<GameLightFixture>();
            if (existingFixture != null && string.Equals(
                    existingFixture.FixtureId,
                    registration.Definition.LightId,
                    StringComparison.Ordinal))
            {
                existingFixture.RepublishLifecycleRegistration();
                EnsureActivationOverride(registration.Light);
                return;
            }

            GameLightFixture fixture = existingFixture;
            if (fixture == null)
            {
                fixture = registration.Light.gameObject.AddComponent<
                    GameLightFixture>();
            }

            LightFixtureProfile profile =
                profileCatalog.GetRequiredProfile(binding.Category);
            Renderer donorFixtureRenderer = ResolveDonorFixtureRenderer(
                registration.Light.gameObject.scene,
                registration.Definition);
            if (donorFixtureRenderer != null && IsAreaShape(profile.Shape))
            {
                // Captured point-light rotations were irrelevant in the donor.
                // The fixture renderer retains the actual tube/rectangle basis,
                // including the required 90 degree garage/Teimo orientation.
                if (!registration.Definition.HasAreaSizeOverride)
                {
                    registration.Light.transform.rotation =
                        donorFixtureRenderer.transform.rotation;
                }
            }

            Renderer[] emissiveRenderers = CreateEmissionPresentation(
                registration.Light,
                donorFixtureRenderer,
                profile,
                registration.Definition.SuppressGeneratedEmission);
            fixture.InitializeRuntime(
                registration.Definition.LightId,
                profile,
                new[] { registration.Light },
                emissiveRenderers,
                binding.PowerSourceId,
                binding.CircuitId,
                binding.SwitchId,
                binding.ZoneId,
                binding.BusinessId,
                string.Empty,
                true,
                ResolveIntensityMultiplier(
                    registration.Definition,
                    binding,
                    profile));
            if (registration.Definition.HasAreaSizeOverride)
            {
                registration.Light.areaSize =
                    registration.Definition.AreaSize;
            }
            EnsureActivationOverride(registration.Light);
            BoundRuntimeLightCount++;
        }

        private static void EnsureActivationOverride(Light light)
        {
            if (light != null && light.GetComponent<
                    WorldRuntimeLightActivationOverride>() == null)
            {
                light.gameObject.AddComponent<
                    WorldRuntimeLightActivationOverride>();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Scene callbacks can race the single-scene teardown boundary:
            // Unity may deactivate this persistent root just before it invokes
            // the callback, while OnDisable is still queued. Never start work
            // from an owner that has already left the active session.
            if (Application.isPlaying && subscribed && isActiveAndEnabled)
            {
                StartCoroutine(ReplayStreamedSceneNextFrame(scene));
            }
        }

        private IEnumerator ReplayExistingRuntimeLights()
        {
            // The world runtime may replay already-open additive cells during
            // the same startup frame. Waiting one frame makes this adapter
            // insensitive to component/scene callback ordering.
            yield return null;
            WorldLightingProbeRuntime runtime = GetComponent<
                WorldLightingProbeRuntime>();
            runtime?.ReconcileLoadedCellScenes();
            runtime?.ReplayRuntimeLights(HandleRuntimeLightCreated);
        }

        private IEnumerator ReplayStreamedSceneNextFrame(Scene scene)
        {
            yield return null;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                yield break;
            }

            WorldLightingProbeRuntime runtime = GetComponent<
                WorldLightingProbeRuntime>();
            runtime?.ReconcileLoadedCellScenes();
            runtime?.ReplayRuntimeLights(scene, HandleRuntimeLightCreated);
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            fixtureIndexes.Remove(scene.handle);
        }

        private Renderer ResolveDonorFixtureRenderer(
            Scene scene,
            WorldLightDefinition definition)
        {
            if (!scene.IsValid() || !scene.isLoaded || definition == null)
            {
                return null;
            }

            if (!fixtureIndexes.TryGetValue(
                    scene.handle,
                    out SceneFixtureIndex index))
            {
                index = BuildFixtureIndex(scene);
                fixtureIndexes.Add(scene.handle, index);
            }

            const string generatedPrefix = "world.light.";
            string id = definition.LightId ?? string.Empty;
            if (id.StartsWith(generatedPrefix, StringComparison.Ordinal))
            {
                string donorStableId = id.Substring(generatedPrefix.Length);
                if (index.ByStableId.TryGetValue(
                        donorStableId,
                        out Renderer renderer))
                {
                    return renderer;
                }
            }

            string path = definition.DonorReferencePath ?? string.Empty;
            return index.BySourcePath.TryGetValue(path, out Renderer byPath)
                ? byPath
                : null;
        }

        private static SceneFixtureIndex BuildFixtureIndex(Scene scene)
        {
            var result = new SceneFixtureIndex();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                DonorWorldBaselineEntityMetadata[] entities =
                    roots[rootIndex].GetComponentsInChildren<
                        DonorWorldBaselineEntityMetadata>(true);
                for (int entityIndex = 0;
                     entityIndex < entities.Length;
                     entityIndex++)
                {
                    DonorWorldBaselineEntityMetadata entity =
                        entities[entityIndex];
                    Renderer renderer = entity.GetComponent<Renderer>();
                    if (renderer == null)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(entity.StableId))
                    {
                        result.ByStableId[entity.StableId] = renderer;
                    }

                    if (!string.IsNullOrWhiteSpace(
                            entity.SourceHierarchyPath))
                    {
                        result.BySourcePath[entity.SourceHierarchyPath] =
                            renderer;
                    }
                }
            }

            return result;
        }

        private Renderer[] CreateEmissionPresentation(
            Light light,
            Renderer donorFixtureRenderer,
            LightFixtureProfile profile,
            bool suppressGeneratedEmission)
        {
            if (suppressGeneratedEmission)
            {
                return Array.Empty<Renderer>();
            }

            Material material = GetRuntimeEmissionMaterial();
            if (light == null || profile == null || material == null)
            {
                return Array.Empty<Renderer>();
            }

            bool area = IsAreaShape(profile.Shape);
            bool generatedLens = area || IsSpotShape(profile.Shape);
            if (!generatedLens)
            {
                if (donorFixtureRenderer == null)
                {
                    return Array.Empty<Renderer>();
                }

                // Use the actual donor lampshade/bulb surface. A generated
                // sphere ignored the shade's silhouette and alpha and showed
                // up as a solid HDR-white ball. The temporary translucent
                // shade must also not cast an opaque cap over a point light.
                donorFixtureRenderer.shadowCastingMode =
                    ShadowCastingMode.Off;
                return new[] { donorFixtureRenderer };
            }

            GameObject emitter = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            emitter.name = "Emission Lens (Runtime)";
            emitter.hideFlags = HideFlags.DontSave;
            SceneManager.MoveGameObjectToScene(
                emitter,
                light.gameObject.scene);
            emitter.transform.SetParent(light.transform, true);
            bool exteriorDonorLens = donorFixtureRenderer != null &&
                profile.Category == LightFixtureCategory.HomeExterior;
            Vector3 emitterPosition = area && donorFixtureRenderer != null
                ? donorFixtureRenderer.bounds.center
                : light.transform.position;
            Quaternion emitterRotation = area && donorFixtureRenderer != null
                ? donorFixtureRenderer.transform.rotation
                : light.transform.rotation;
            if (exteriorDonorLens)
            {
                Vector3 lensNormal = donorFixtureRenderer.transform.forward;
                lensNormal = lensNormal.sqrMagnitude > 0.5f
                    ? lensNormal.normalized
                    : Vector3.down;
                float surfaceOffset = SupportDistance(
                    donorFixtureRenderer.bounds.extents,
                    lensNormal) + 0.004f;
                emitterPosition = donorFixtureRenderer.bounds.center +
                    lensNormal * surfaceOffset;
                Vector3 lensUp = Mathf.Abs(Vector3.Dot(
                        lensNormal,
                        donorFixtureRenderer.transform.up)) < 0.95f
                    ? donorFixtureRenderer.transform.up
                    : donorFixtureRenderer.transform.right;
                emitterRotation = Quaternion.LookRotation(lensNormal, lensUp);
            }

            emitter.transform.SetPositionAndRotation(
                emitterPosition,
                emitterRotation);
            emitter.transform.localScale = new Vector3(
                Mathf.Max(0.04f, profile.SourceWidthMeters * 0.9f),
                Mathf.Max(0.035f, profile.SourceHeightMeters * 0.9f),
                0.008f);

            Collider generatedCollider = emitter.GetComponent<Collider>();
            if (generatedCollider != null)
            {
                generatedCollider.enabled = false;
                Destroy(generatedCollider);
            }

            Renderer renderer = emitter.GetComponent<Renderer>();
            emitter.AddComponent<RuntimeEmissionLens>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode =
                MotionVectorGenerationMode.ForceNoMotion;
            return new[] { renderer };
        }

        private Material GetRuntimeEmissionMaterial()
        {
            if (runtimeEmissionMaterial != null)
            {
                return runtimeEmissionMaterial;
            }

            Shader shader = Shader.Find("HDRP/Unlit") ??
                Shader.Find("Unlit/Color");
            if (shader == null)
            {
                Debug.LogError(
                    "[Lighting] No HDRP/Unlit fallback shader is available for runtime fixture emission.",
                    this);
                return null;
            }

            runtimeEmissionMaterial = new Material(shader)
            {
                name = "Runtime Fixture Emission",
                hideFlags = HideFlags.DontSave,
                enableInstancing = true,
            };
            return runtimeEmissionMaterial;
        }

        private static bool IsAreaShape(LightFixtureShape shape) =>
            shape == LightFixtureShape.AreaRectangle ||
            shape == LightFixtureShape.AreaTube;

        private static bool IsSpotShape(LightFixtureShape shape) =>
            shape == LightFixtureShape.SpotCone ||
            shape == LightFixtureShape.SpotPyramid ||
            shape == LightFixtureShape.SpotBox;

        private static float SupportDistance(Vector3 extents, Vector3 axis) =>
            Mathf.Abs(axis.x) * extents.x +
            Mathf.Abs(axis.y) * extents.y +
            Mathf.Abs(axis.z) * extents.z;

        private static float ResolveIntensityMultiplier(
            WorldLightDefinition definition,
            WorldLightingFixtureBinding binding,
            LightFixtureProfile profile)
        {
            float capturedPhotometryFloor = profile.Intensity > 0f
                ? Mathf.Max(1f, definition.Intensity / profile.Intensity)
                : 1f;
            bool isHomeCircuit = !string.IsNullOrWhiteSpace(
                    binding.CircuitId) &&
                binding.CircuitId.StartsWith(
                    HomeCircuitPrefix,
                    StringComparison.Ordinal);

            // Generic donor office values were captured from a non-physical
            // point-light setup. Outside the accepted home baseline, the HDRP
            // fluorescent profile is authoritative and deliberately bounded.
            if (profile.Category == LightFixtureCategory.Fluorescent &&
                !isHomeCircuit)
            {
                return 1f;
            }

            float multiplier = capturedPhotometryFloor *
                (isHomeCircuit
                    ? RuntimeBaselineHomeIntensityMultiplier
                    : 1f);
            if (profile.Category == LightFixtureCategory.Fluorescent &&
                string.Equals(
                    binding.CircuitId,
                    GarageCircuitId,
                    StringComparison.Ordinal))
            {
                multiplier *= RuntimeBaselineGarageIntensityMultiplier;
            }

            return multiplier;
        }

        private void ValidateConfiguration()
        {
            if (profileCatalog == null || bindingCatalog == null)
            {
                throw new InvalidOperationException(
                    "World lighting adapter requires profile and binding catalogs.");
            }

            bindingCatalog.Validate();
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            LightingProfileCatalog configuredProfiles,
            WorldLightingBindingCatalog configuredBindings)
        {
            profileCatalog = configuredProfiles;
            bindingCatalog = configuredBindings;
        }
#endif

        private sealed class SceneFixtureIndex
        {
            public readonly Dictionary<string, Renderer> ByStableId =
                new Dictionary<string, Renderer>(StringComparer.Ordinal);
            public readonly Dictionary<string, Renderer> BySourcePath =
                new Dictionary<string, Renderer>(
                    StringComparer.OrdinalIgnoreCase);
        }
    }
}
