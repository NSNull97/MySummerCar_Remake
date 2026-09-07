using System;
using System.Collections;
using System.Collections.Generic;
using Enviro;
using MSC.Weather.Production;
using MSC.Weather.Wetness;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Weather.Enviro3Integration
{
    public readonly struct Enviro3ShelterRemovalZoneLayout
    {
        public Enviro3ShelterRemovalZoneLayout(
            Vector3 center,
            float radius,
            float stretch)
        {
            Center = center;
            Radius = radius;
            Stretch = stretch;
        }

        public Vector3 Center { get; }

        public float Radius { get; }

        public float Stretch { get; }
    }

    /// <summary>
    /// Vendor-specific presentation bridge for project-owned shelter volumes.
    /// Runtime-only Enviro removal zones suppress local weather effects/fog while
    /// the authoritative exposure context remains in the production domain.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Enviro3ShelterRemovalBridge : MonoBehaviour
    {
        private const int MaximumManagerStartupFrames = 16;

        [SerializeField] private EnviroManager manager;
        [SerializeField, Range(-2f, 0f)] private float density = -2f;
        [SerializeField, Range(0f, 1f)] private float feather = 0.2f;

        private readonly List<GameObject> runtimeZones =
            new List<GameObject>(16);
        private Coroutine startupRoutine;
        private int activeShelterCount;

        public int ActiveZoneCount => runtimeZones.Count;

        public int ActiveShelterCount => activeShelterCount;

        public string LastFailure { get; private set; } = string.Empty;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(EnviroManager authoredManager)
        {
            manager = authoredManager;
        }
#endif

        public static Enviro3ShelterRemovalZoneLayout[] CalculateZoneLayout(
            in ShelterVolume volume)
        {
            Vector3 extents = volume.Extents;
            if (!float.IsFinite(extents.x) || extents.x <= 0f ||
                !float.IsFinite(extents.y) || extents.y <= 0f ||
                !float.IsFinite(extents.z) || extents.z <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(volume));
            }

            float radius = Mathf.Min(extents.x, extents.z);
            // Enviro stretches the removal ellipsoid along transform.up. A value
            // below one pinches its horizontal section near the ceiling and lets
            // rain enter otherwise valid project-owned shelter bounds. Keep every
            // tiled zone at least spherical while preserving its horizontal radius.
            float stretch = Mathf.Max(1f, extents.y / radius);
            if (!float.IsFinite(stretch) || stretch <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(volume));
            }

            float longExtent = Mathf.Max(extents.x, extents.z);
            int count = Mathf.Max(1, Mathf.CeilToInt(longExtent / radius));
            var result = new Enviro3ShelterRemovalZoneLayout[count];
            Vector3 longAxis = extents.x >= extents.z
                ? Vector3.right
                : Vector3.forward;
            float maximumOffset = longExtent - radius;
            for (int index = 0; index < count; index++)
            {
                float normalized = count == 1
                    ? 0.5f
                    : index / (float)(count - 1);
                float offset = Mathf.Lerp(
                    -maximumOffset,
                    maximumOffset,
                    normalized);
                result[index] = new Enviro3ShelterRemovalZoneLayout(
                    volume.Center + longAxis * offset,
                    radius,
                    stretch);
            }

            return result;
        }

        public void RefreshLoadedShelters()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            DestroyRuntimeZones();
            if (manager == null || EnviroManager.instance != manager)
            {
                LastFailure =
                    "Enviro shelter bridge has no active configured manager.";
                return;
            }

            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            var visitedRoots = new HashSet<EntityId>();

            // The bridge and Bootstrap-authored shelters live under the same
            // project-owned root after DontDestroyOnLoad. That special scene
            // is not part of SceneManager.sceneCount.
            CreateRuntimeZonesFromRoot(
                transform.root.gameObject,
                stableIds,
                visitedRoots);

            for (int sceneIndex = 0;
                 sceneIndex < SceneManager.sceneCount;
                 sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0;
                     rootIndex < roots.Length;
                     rootIndex++)
                {
                    CreateRuntimeZonesFromRoot(
                        roots[rootIndex],
                        stableIds,
                        visitedRoots);
                }
            }

            LastFailure = string.Empty;
        }

        private void CreateRuntimeZonesFromRoot(
            GameObject root,
            ISet<string> stableIds,
            ISet<EntityId> visitedRoots)
        {
            if (root == null || !visitedRoots.Add(root.GetEntityId()))
            {
                return;
            }

            ProductionShelterVolumeAuthoring[] authored =
                root.GetComponentsInChildren<
                    ProductionShelterVolumeAuthoring>(true);
            for (int volumeIndex = 0;
                 volumeIndex < authored.Length;
                 volumeIndex++)
            {
                ProductionShelterVolumeAuthoring authoring =
                    authored[volumeIndex];
                if (!authoring.isActiveAndEnabled)
                {
                    continue;
                }

                if (!authoring.TryCreateVolume(
                        out ShelterVolume volume,
                        out string failure))
                {
                    Debug.LogError(failure, authoring);
                    continue;
                }

                if (!stableIds.Add(volume.StableId))
                {
                    continue;
                }

                activeShelterCount++;
                CreateRuntimeZones(volume);
            }
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            startupRoutine = StartCoroutine(AttachAfterManagerStartup());
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            if (startupRoutine != null)
            {
                StopCoroutine(startupRoutine);
                startupRoutine = null;
            }

            DestroyRuntimeZones();
        }

        private IEnumerator AttachAfterManagerStartup()
        {
            for (int frame = 0;
                 frame < MaximumManagerStartupFrames &&
                 EnviroManager.instance != manager;
                 frame++)
            {
                yield return null;
            }

            startupRoutine = null;
            RefreshLoadedShelters();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshLoadedShelters();
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            RefreshLoadedShelters();
        }

        private void CreateRuntimeZones(in ShelterVolume volume)
        {
            Enviro3ShelterRemovalZoneLayout[] layout =
                CalculateZoneLayout(volume);
            for (int index = 0; index < layout.Length; index++)
            {
                Enviro3ShelterRemovalZoneLayout entry = layout[index];
                GameObject zoneObject = new GameObject(
                    "RuntimeShelterRemovalZone_" + volume.StableId +
                    "_" + (index + 1).ToString("D2"));
                zoneObject.transform.SetParent(transform, false);
                zoneObject.transform.position = entry.Center;
                EnviroEffectRemovalZone zone =
                    zoneObject.AddComponent<EnviroEffectRemovalZone>();
                zone.radius = entry.Radius;
                zone.stretch = entry.Stretch;
                zone.density = density;
                zone.feather = feather;
                runtimeZones.Add(zoneObject);
            }
        }

        private void DestroyRuntimeZones()
        {
            for (int index = runtimeZones.Count - 1;
                 index >= 0;
                 index--)
            {
                if (runtimeZones[index] != null)
                {
                    runtimeZones[index].SetActive(false);
                    Destroy(runtimeZones[index]);
                }
            }

            runtimeZones.Clear();
            activeShelterCount = 0;
        }
    }
}
