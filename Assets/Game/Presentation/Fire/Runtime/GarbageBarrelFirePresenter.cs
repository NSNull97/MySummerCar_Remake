using System;
using MSC.Items;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace MSC.Presentation.Fire
{
    /// <summary>
    /// HDRP-safe fire presentation for a project-owned ignited item state.
    /// Gameplay ignition, fuel, and item consumption remain in
    /// MSC.Items.Runtime. The established type name is retained for serialized
    /// compatibility with the original garbage-barrel integration.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GarbageBarrelFirePresenter : MonoBehaviour
    {
        public const float FireOriginExtentFraction = 0.25f;
        public const float PresetScalePerBarrelDiameter = 0.72f;
        public const float MinimumPresetScale = 0.05f;
        public const float MaximumPresetScale = 1.15f;
        public const string FirePresetResourcePath = "MSC/Fire/Fire001";
        public const string FireTextureResourcePath = "MSC/Fire/FireSeq1";
        public const string SmokeTextureResourcePath = "MSC/Fire/Smoke1";
        public const string SparkTextureResourcePath = "MSC/Fire/PointGlow";

        private WorldItemInstance barrel;
        private GameObject fireObject;
        private ParticleSystem[] particleSystems =
            Array.Empty<ParticleSystem>();
        private ParticleSystemRenderer[] particleRenderers =
            Array.Empty<ParticleSystemRenderer>();
        private ParticleSystemRenderer particleRenderer;
        private Light fireLight;
        private Material fireMaterial;
        private Material smokeMaterial;
        private Material sparkMaterial;
        private float fireLightBaseIntensity = 720f;
        private bool configured;

        public bool IsConfigured => configured;
        public bool IsFireVisible =>
            fireObject != null && fireObject.activeSelf;
        public ParticleSystemRenderer FireRenderer => particleRenderer;
        public int ParticleSystemCount => particleSystems.Length;
        public Transform FireTransform => fireObject != null
            ? fireObject.transform
            : null;
        public Light FireLight => fireLight;
        public bool UsesImportedPreset =>
            fireObject != null && particleSystems.Length >= 6;

        public void Configure(
            WorldItemInstance configuredBarrel,
            GameObject presentationRoot)
        {
            if (configured)
            {
                throw new InvalidOperationException(
                    "Garbage-barrel fire presenter is already configured.");
            }

            barrel = configuredBarrel ??
                throw new ArgumentNullException(nameof(configuredBarrel));
            CreateFireObject();
            barrel.StatusChanged += HandleBarrelStatusChanged;
            configured = true;
            BindPresentation(presentationRoot);
            ApplyVisibility();
        }

        public bool BindPresentation(GameObject presentationRoot)
        {
            if (fireObject == null || presentationRoot == null)
            {
                return false;
            }

            if (barrel?.Definition?.Combustion.IsConfigured == true &&
                barrel.Definition.HeatSource.ProvidesCookingHeat)
            {
                ItemHeatSourceDefinition heat =
                    barrel.Definition.HeatSource;
                fireObject.transform.localPosition = heat.LocalCenter;
                Vector3 localUp = heat.LocalUp;
                fireObject.transform.localRotation =
                    Quaternion.FromToRotation(Vector3.up, localUp);
                int verticalAxis = LargestAbsoluteAxis(localUp);
                int configuredRadialA = verticalAxis == 0 ? 1 : 0;
                int configuredRadialB = verticalAxis == 2 ? 1 : 2;
                float authoredDiameter = Mathf.Min(
                    GetAxis(heat.LocalSize, configuredRadialA),
                    GetAxis(heat.LocalSize, configuredRadialB));
                float presentationScale = barrel.Definition.Combustion
                    .FirePresentationScale;
                fireObject.transform.localScale = Vector3.one * Mathf.Clamp(
                    authoredDiameter * PresetScalePerBarrelDiameter *
                    presentationScale,
                    MinimumPresetScale,
                    MaximumPresetScale);
                return true;
            }

            Renderer[] renderers =
                presentationRoot.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds bounds = default;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                Vector3 minimum = renderer.localBounds.min;
                Vector3 maximum = renderer.localBounds.max;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 localCorner = new Vector3(
                        (corner & 1) == 0 ? minimum.x : maximum.x,
                        (corner & 2) == 0 ? minimum.y : maximum.y,
                        (corner & 4) == 0 ? minimum.z : maximum.z);
                    Vector3 rootLocal = transform.InverseTransformPoint(
                        renderer.transform.TransformPoint(localCorner));
                    if (!hasBounds)
                    {
                        bounds = new Bounds(rootLocal, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(rootLocal);
                    }
                }
            }

            if (!hasBounds)
            {
                return false;
            }

            int axisIndex = LargestAxis(bounds.size);
            Vector3 axis = AxisVector(axisIndex);
            if (Vector3.Dot(
                    transform.TransformDirection(axis),
                    Vector3.up) < 0f)
            {
                axis = -axis;
            }

            float verticalExtent = GetAxis(bounds.extents, axisIndex);
            fireObject.transform.localPosition =
                bounds.center + axis * verticalExtent *
                FireOriginExtentFraction;
            fireObject.transform.localRotation =
                Quaternion.FromToRotation(Vector3.up, axis);
            int radialA = axisIndex == 0 ? 1 : 0;
            int radialB = axisIndex == 2 ? 1 : 2;
            float barrelDiameter = Mathf.Min(
                GetAxis(bounds.size, radialA),
                GetAxis(bounds.size, radialB));
            float presetScale = Mathf.Clamp(
                barrelDiameter * PresetScalePerBarrelDiameter,
                0.35f,
                MaximumPresetScale);
            fireObject.transform.localScale = Vector3.one * presetScale;
            return true;
        }

        private void CreateFireObject()
        {
            GameObject preset = Resources.Load<GameObject>(
                FirePresetResourcePath);
            if (preset == null)
            {
                throw new InvalidOperationException(
                    $"Fire preset resource is missing: {FirePresetResourcePath}.");
            }

            Texture2D flameTexture = LoadPresetTexture(
                FireTextureResourcePath);
            Texture2D smokeTexture = LoadPresetTexture(
                SmokeTextureResourcePath);
            Texture2D sparkTexture = LoadPresetTexture(
                SparkTextureResourcePath);
            fireMaterial = BuildFireMaterial(
                flameTexture,
                "MSC HDRP Fire001 Flame",
                new Color(2.8f, 0.52f, 0.025f, 1f),
                new Color(4f, 1.1f, 0.08f, 1f),
                12);
            smokeMaterial = BuildFireMaterial(
                smokeTexture,
                "MSC HDRP Fire001 Smoke",
                new Color(0.34f, 0.3f, 0.27f, 0.52f),
                new Color(0.08f, 0.06f, 0.045f, 1f),
                6);
            sparkMaterial = BuildFireMaterial(
                sparkTexture,
                "MSC HDRP Fire001 Sparks",
                new Color(2.4f, 0.62f, 0.045f, 1f),
                new Color(5f, 1.5f, 0.08f, 1f),
                18);

            fireObject = Instantiate(preset, transform, false);
            fireObject.name = "HDRP garbage barrel fire - Fire001";
            particleSystems =
                fireObject.GetComponentsInChildren<ParticleSystem>(true);
            particleRenderers =
                fireObject.GetComponentsInChildren<
                    ParticleSystemRenderer>(true);
            if (particleSystems.Length < 6 ||
                particleRenderers.Length != particleSystems.Length)
            {
                throw new InvalidOperationException(
                    "Fire001 preset must provide its six particle layers.");
            }

            ConfigureContinuousBarrelBurn();

            for (int index = 0; index < particleRenderers.Length; index++)
            {
                ParticleSystemRenderer renderer = particleRenderers[index];
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sharedMaterial = SelectPresetMaterial(renderer.name);
                if (particleRenderer == null ||
                    renderer.gameObject == fireObject)
                {
                    particleRenderer = renderer;
                }
            }

            if (particleRenderer == null)
            {
                throw new InvalidOperationException(
                    "Fire001 preset has no usable particle renderer.");
            }

            fireLight = fireObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, 0.24f, 0.035f);
            bool hasCombustionProfile =
                barrel.Definition.Combustion.IsConfigured;
            fireLightBaseIntensity = hasCombustionProfile
                ? barrel.Definition.Combustion.FireLightIntensity
                : 720f;
            fireLight.range = hasCombustionProfile
                ? barrel.Definition.Combustion.FireLightRange
                : 4.2f;
            fireLight.intensity = fireLightBaseIntensity;
            fireLight.shadows = LightShadows.Soft;
            fireObject.SetActive(false);
        }

        private void ConfigureContinuousBarrelBurn()
        {
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem system = particleSystems[index];
                system.Stop(
                    withChildren: false,
                    stopBehavior:
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                main.playOnAwake = true;
                main.startDelay = 0f;
                main.stopAction = ParticleSystemStopAction.None;

                ParticleSystem.EmissionModule emission = system.emission;
                emission.enabled = true;
                emission.SetBursts(Array.Empty<ParticleSystem.Burst>());
                float emissionMultiplier =
                    barrel.Definition.Combustion.IsConfigured
                        ? barrel.Definition.Combustion
                            .FireEmissionMultiplier
                        : 1f;
                emission.rateOverTime = new ParticleSystem.MinMaxCurve(
                    ResolveContinuousEmissionRate(system.name) *
                    emissionMultiplier);
            }
        }

        private static float ResolveContinuousEmissionRate(string layerName)
        {
            if (layerName.IndexOf(
                    "Smoke",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 50f;
            }

            if (layerName.IndexOf(
                    "Spark",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 100f;
            }

            if (layerName.IndexOf(
                    "Fire1",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 50f;
            }

            if (layerName.IndexOf(
                    "Fire2",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 100f;
            }

            return layerName.IndexOf(
                    "Fire3",
                    StringComparison.OrdinalIgnoreCase) >= 0
                ? 100f
                : 10f;
        }

        private void Update()
        {
            if (!IsFireVisible || fireLight == null)
            {
                return;
            }

            fireLight.intensity = Mathf.Max(
                0f,
                fireLightBaseIntensity +
                Mathf.Sin(Time.time * 17f) *
                    fireLightBaseIntensity * 0.16f +
                Mathf.Sin(Time.time * 7.3f) *
                    fireLightBaseIntensity * 0.075f);
        }

        private void HandleBarrelStatusChanged(IItemStatusSource source)
        {
            ApplyVisibility();
        }

        private void ApplyVisibility()
        {
            if (fireObject != null)
            {
                bool shouldShow = barrel != null && barrel.IsIgnited;
                bool becomingVisible = shouldShow && !fireObject.activeSelf;
                fireObject.SetActive(shouldShow);
                if (becomingVisible)
                {
                    PrimeContinuousFire();
                }
            }
        }

        private void PrimeContinuousFire()
        {
            for (int index = 0; index < particleSystems.Length; index++)
            {
                ParticleSystem system = particleSystems[index];
                system.Stop(
                    withChildren: false,
                    stopBehavior:
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Simulate(
                    0.8f,
                    withChildren: false,
                    restart: true,
                    fixedTimeStep: true);
                system.Play(withChildren: false);
            }
        }

        private Material SelectPresetMaterial(string rendererName)
        {
            if (rendererName.IndexOf(
                    "Smoke",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return smokeMaterial;
            }

            return rendererName.IndexOf(
                    "Spark",
                    StringComparison.OrdinalIgnoreCase) >= 0
                ? sparkMaterial
                : fireMaterial;
        }

        private static Texture2D LoadPresetTexture(string resourcePath)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
            {
                throw new InvalidOperationException(
                    $"Fire001 texture resource is missing: {resourcePath}.");
            }

            return texture;
        }

        private static Material BuildFireMaterial(
            Texture texture,
            string materialName,
            Color tint,
            Color emissiveColor,
            int renderQueueOffset)
        {
            Shader shader = Shader.Find("HDRP/Unlit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "HDRP/Unlit is required for garbage-barrel fire.");
            }

            var material = new Material(shader)
            {
                name = materialName,
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue =
                    (int)RenderQueue.Transparent + renderQueueOffset,
            };
            material.SetOverrideTag("RenderType", "Transparent");
            SetColorIfPresent(material, "_BaseColor", tint);
            SetColorIfPresent(material, "_UnlitColor", tint);
            SetColorIfPresent(
                material,
                "_EmissiveColor",
                emissiveColor);
            SetTextureIfPresent(material, "_BaseColorMap", texture);
            SetTextureIfPresent(material, "_UnlitColorMap", texture);
            SetTextureIfPresent(material, "_MainTex", texture);
            SetFloatIfPresent(material, "_SurfaceType", 1f);
            SetFloatIfPresent(material, "_BlendMode", 1f);
            SetFloatIfPresent(material, "_TransparentZWrite", 0f);
            SetFloatIfPresent(material, "_ZWrite", 0f);
            SetFloatIfPresent(material, "_EnableFogOnTransparent", 1f);
            SetFloatIfPresent(material, "_UseEmissiveIntensity", 1f);
            SetFloatIfPresent(material, "_EmissiveIntensity", 4f);
            SetFloatIfPresent(material, "_DoubleSidedEnable", 1f);
            HDMaterial.SetSurfaceType(material, transparent: true);
            HDMaterial.SetRenderingPass(
                material,
                HDMaterial.RenderingPass.Default);
            if (!HDMaterial.ValidateMaterial(material))
            {
                throw new InvalidOperationException(
                    "Garbage-barrel fire material failed HDRP validation.");
            }

            return material;
        }

        private static int LargestAxis(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z)
            {
                return 0;
            }

            return size.y >= size.z ? 1 : 2;
        }

        private static int LargestAbsoluteAxis(Vector3 value)
        {
            Vector3 absolute = new(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
            return LargestAxis(absolute);
        }

        private static Vector3 AxisVector(int axis) =>
            axis switch
            {
                0 => Vector3.right,
                1 => Vector3.up,
                _ => Vector3.forward,
            };

        private static float GetAxis(Vector3 value, int axis) =>
            axis switch
            {
                0 => value.x,
                1 => value.y,
                _ => value.z,
            };

        private static void SetColorIfPresent(
            Material material,
            string property,
            Color value)
        {
            if (material.HasProperty(property))
            {
                material.SetColor(property, value);
            }
        }

        private static void SetTextureIfPresent(
            Material material,
            string property,
            Texture value)
        {
            if (value != null && material.HasProperty(property))
            {
                material.SetTexture(property, value);
            }
        }

        private static void SetFloatIfPresent(
            Material material,
            string property,
            float value)
        {
            if (material.HasProperty(property))
            {
                material.SetFloat(property, value);
            }
        }

        private void OnDestroy()
        {
            if (barrel != null)
            {
                barrel.StatusChanged -= HandleBarrelStatusChanged;
            }

            DestroyRuntimeObject(fireMaterial);
            DestroyRuntimeObject(smokeMaterial);
            DestroyRuntimeObject(sparkMaterial);
        }

        private static void DestroyRuntimeObject(UnityEngine.Object value)
        {
            if (value == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(value);
            }
            else
            {
                DestroyImmediate(value);
            }
        }
    }
}
