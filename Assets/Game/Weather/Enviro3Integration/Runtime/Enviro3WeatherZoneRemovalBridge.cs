using System;
using System.Collections;
using System.Collections.Generic;
using Enviro;
using MSC.Weather.Production;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Weather.Enviro3Integration
{
    public readonly struct Enviro3WeatherRemovalZoneLayout
    {
        public Enviro3WeatherRemovalZoneLayout(
            Vector3 center,
            Vector3 up,
            float radius,
            float stretch)
        {
            Center = center;
            Up = up;
            Radius = radius;
            Stretch = stretch;
        }

        public Vector3 Center { get; }
        public Vector3 Up { get; }
        public float Radius { get; }
        public float Stretch { get; }
    }

    /// <summary>
    /// Local-only precipitation/screen-effect suppression for explicitly authored
    /// closed and roof-shelter zones. It never changes global Enviro
    /// precipitation intensity.
    /// </summary>
    [DefaultExecutionOrder(260)]
    [DisallowMultipleComponent]
    public sealed class Enviro3WeatherZoneRemovalBridge : MonoBehaviour
    {
        private static readonly ProfilerMarker RefreshMarker =
            new ProfilerMarker("MSC.EnviroZoneRemovalBridge");

        [SerializeField] private EnviroManager manager;
        [SerializeField] private WeatherZoneRegistry registry;
        [SerializeField] private WeatherExposureResolver exposureResolver;
        [SerializeField, Range(-2f, 0f)] private float density = -2f;
        [SerializeField, Range(0f, 1f)] private float feather = 0.25f;

        private readonly List<GameObject> runtimeZones =
            new List<GameObject>(32);
        private bool refreshRequested;

        public int ActiveRemovalZoneCount => runtimeZones.Count;
        public WeatherExposureResolver ExposureResolver => exposureResolver;
        public float CurrentParticleExposure { get; private set; } = 1f;
        public string LastFailure { get; private set; } = string.Empty;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            EnviroManager authoredManager,
            WeatherZoneRegistry authoredRegistry,
            WeatherExposureResolver authoredExposureResolver)
        {
            manager = authoredManager;
            registry = authoredRegistry;
            exposureResolver = authoredExposureResolver;
        }
#endif

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            AttachRegistry();
            RefreshLoadedZones();
            StartCoroutine(RefreshWhenDependenciesAreReady());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            DetachRegistry();
            DestroyRuntimeZones();
            refreshRequested = false;
            CurrentParticleExposure = 1f;
        }

        private void LateUpdate()
        {
            if (refreshRequested)
            {
                refreshRequested = false;
                RefreshLoadedZones();
            }

            if (exposureResolver == null)
            {
                exposureResolver = FindFirstObjectByType<WeatherExposureResolver>();
            }

            CurrentParticleExposure = exposureResolver != null
                ? Mathf.Clamp01(
                    exposureResolver.Current.PrecipitationExposure)
                : 1f;
            ApplyRuntimeRainParticleExposure(CurrentParticleExposure);
        }

        private void ApplyRuntimeRainParticleExposure(float exposure01)
        {
            if (manager == null || manager.Effects == null ||
                manager.Effects.Settings == null ||
                manager.Effects.Settings.effectTypes == null)
            {
                return;
            }

            for (int index = 0;
                 index < manager.Effects.Settings.effectTypes.Count;
                 index++)
            {
                EnviroEffectTypes effect =
                    manager.Effects.Settings.effectTypes[index];
                if (effect == null || effect.mySystem == null ||
                    !string.Equals(effect.name, "Rain", StringComparison.Ordinal))
                {
                    continue;
                }

                float globalEmission =
                    Mathf.Max(0f, effect.maxEmission * effect.emissionRate);
                manager.Effects.SetEmissionRate(
                    effect.mySystem,
                    CalculateLocalEmission(globalEmission, exposure01));
            }
        }

        public static float CalculateLocalEmission(
            float globalEmission,
            float precipitationExposure01) =>
            Mathf.Max(0f, globalEmission) *
            Mathf.Clamp01(precipitationExposure01);

        public void RefreshLoadedZones()
        {
            using (RefreshMarker.Auto())
            {
                DestroyRuntimeZones();
                if (manager == null || EnviroManager.instance != manager)
                {
                    LastFailure =
                        "Enviro zone-removal bridge has no active configured manager.";
                    return;
                }

                if (registry == null)
                {
                    registry = WeatherZoneRegistry.Active;
                    AttachRegistry();
                }

                if (registry == null)
                {
                    LastFailure = "WeatherZoneRegistry is unavailable.";
                    return;
                }

                LastFailure = string.Empty;

                var zones = registry.Zones;
                for (int zoneIndex = 0; zoneIndex < zones.Count; zoneIndex++)
                {
                    WeatherZone zone = zones[zoneIndex];
                    if (!ShouldRemovePrecipitation(zone))
                    {
                        continue;
                    }

                    var colliders = zone.VolumeColliders;
                    for (int colliderIndex = 0;
                         colliderIndex < colliders.Count;
                         colliderIndex++)
                    {
                        if (colliders[colliderIndex] is BoxCollider box &&
                            box.enabled)
                        {
                            CreateTiledRemovalZones(zone, box);
                        }
                        else if (colliders[colliderIndex] != null)
                        {
                            LastFailure =
                                "Only explicit BoxCollider weather volumes can be " +
                                "safely converted to Enviro removal zones: " +
                                zone.StableId;
                        }
                    }
                }
            }
        }

        private void CreateTiledRemovalZones(
            WeatherZone zone,
            BoxCollider box)
        {
            Vector3 scale = box.transform.lossyScale;
            Vector3 size = new Vector3(
                Mathf.Abs(scale.x * box.size.x),
                Mathf.Abs(scale.y * box.size.y),
                Mathf.Abs(scale.z * box.size.z));
            Vector3 center = box.transform.TransformPoint(box.center);
            Enviro3WeatherRemovalZoneLayout[] layout = CalculateZoneLayout(
                center,
                box.transform.rotation,
                size);
            for (int index = 0; index < layout.Length; index++)
            {
                GameObject runtimeZone = new GameObject(
                    "HybridPrecipitationRemoval_" + zone.StableId + "_" +
                    (index + 1).ToString("D2"));
                runtimeZone.transform.SetParent(transform, false);
                runtimeZone.transform.SetPositionAndRotation(
                    layout[index].Center,
                    Quaternion.FromToRotation(Vector3.up, layout[index].Up));
                EnviroEffectRemovalZone removal =
                    runtimeZone.AddComponent<EnviroEffectRemovalZone>();
                removal.radius = layout[index].Radius;
                removal.stretch = layout[index].Stretch;
                removal.density = density;
                removal.feather = feather;
                runtimeZones.Add(runtimeZone);
            }
        }

        /// <summary>
        /// Converts an arbitrarily oriented BoxCollider into Enviro's vertical
        /// ellipsoid layout. Donor-converted buildings commonly store world-up
        /// in local Z rather than local Y, so the vertical axis is inferred from
        /// the box rotation instead of being assumed.
        /// </summary>
        public static Enviro3WeatherRemovalZoneLayout[] CalculateZoneLayout(
            Vector3 worldCenter,
            Quaternion worldRotation,
            Vector3 sizeMeters)
        {
            if (!IsFinite(worldCenter) || !IsFinite(sizeMeters) ||
                sizeMeters.x <= 0f || sizeMeters.y <= 0f ||
                sizeMeters.z <= 0f || !IsFinite(worldRotation))
            {
                throw new ArgumentOutOfRangeException(nameof(sizeMeters));
            }

            Vector3[] axes =
            {
                worldRotation * Vector3.right,
                worldRotation * Vector3.up,
                worldRotation * Vector3.forward,
            };
            float[] lengths =
            {
                sizeMeters.x,
                sizeMeters.y,
                sizeMeters.z,
            };
            int verticalIndex = 0;
            float bestVerticalAlignment = -1f;
            for (int index = 0; index < axes.Length; index++)
            {
                axes[index].Normalize();
                float alignment = Mathf.Abs(Vector3.Dot(axes[index], Vector3.up));
                if (alignment > bestVerticalAlignment)
                {
                    bestVerticalAlignment = alignment;
                    verticalIndex = index;
                }
            }

            int firstHorizontalIndex = (verticalIndex + 1) % 3;
            int secondHorizontalIndex = (verticalIndex + 2) % 3;
            int longHorizontalIndex =
                lengths[firstHorizontalIndex] >= lengths[secondHorizontalIndex]
                    ? firstHorizontalIndex
                    : secondHorizontalIndex;
            int shortHorizontalIndex =
                longHorizontalIndex == firstHorizontalIndex
                    ? secondHorizontalIndex
                    : firstHorizontalIndex;
            float radius = Mathf.Max(
                0.1f,
                lengths[shortHorizontalIndex] * 0.5f);
            float longLength = lengths[longHorizontalIndex];
            int count = Mathf.Max(
                1,
                Mathf.CeilToInt(longLength / (radius * 2f)));
            float availableOffset = Mathf.Max(
                0f,
                longLength * 0.5f - radius);
            Vector3 up = axes[verticalIndex];
            if (Vector3.Dot(up, Vector3.up) < 0f)
            {
                up = -up;
            }

            var result = new Enviro3WeatherRemovalZoneLayout[count];
            for (int index = 0; index < count; index++)
            {
                float normalized = count == 1
                    ? 0.5f
                    : index / (float)(count - 1);
                float offset = Mathf.Lerp(
                    -availableOffset,
                    availableOffset,
                    normalized);
                result[index] = new Enviro3WeatherRemovalZoneLayout(
                    worldCenter + axes[longHorizontalIndex] * offset,
                    up,
                    radius,
                    Mathf.Max(
                        1f,
                        lengths[verticalIndex] / (radius * 2f)));
            }

            return result;
        }

        private static bool ShouldRemovePrecipitation(WeatherZone zone) =>
            zone != null && zone.isActiveAndEnabled && zone.Profile != null &&
            (zone.Profile.Kind == WeatherZoneKind.ClosedInterior ||
             zone.Profile.Kind == WeatherZoneKind.VehicleCabin ||
             zone.Profile.Kind == WeatherZoneKind.Shelter);

        private void DestroyRuntimeZones()
        {
            for (int index = runtimeZones.Count - 1; index >= 0; index--)
            {
                if (runtimeZones[index] != null)
                {
                    runtimeZones[index].SetActive(false);
                    Destroy(runtimeZones[index]);
                }
            }

            runtimeZones.Clear();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            registry?.RefreshLoadedObjects();
            refreshRequested = true;
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            registry?.RefreshLoadedObjects();
            refreshRequested = true;
        }

        private void AttachRegistry()
        {
            if (registry == null)
            {
                registry = WeatherZoneRegistry.Active;
            }

            if (registry != null)
            {
                registry.Changed -= HandleRegistryChanged;
                registry.Changed += HandleRegistryChanged;
            }
        }

        private void DetachRegistry()
        {
            if (registry != null)
            {
                registry.Changed -= HandleRegistryChanged;
            }
        }

        private void HandleRegistryChanged() => refreshRequested = true;

        private IEnumerator RefreshWhenDependenciesAreReady()
        {
            const int maximumFrames = 60;
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (manager != null && EnviroManager.instance == manager &&
                    (registry != null || WeatherZoneRegistry.Active != null))
                {
                    AttachRegistry();
                    RefreshLoadedZones();
                    yield break;
                }

                yield return null;
            }
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);
    }
}
