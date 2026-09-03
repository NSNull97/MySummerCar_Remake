using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace MSC.Weather.Production
{
    /// <summary>
    /// Materializes project-owned weather volumes inside legacy streaming scenes.
    /// Definitions use stable IDs and world-space measurements; donor hierarchy
    /// names remain provenance only and are never queried at runtime.
    /// </summary>
    [DefaultExecutionOrder(-350)]
    [DisallowMultipleComponent]
    public sealed class WeatherZoneStreamingBinder : MonoBehaviour
    {
        [SerializeField] private WeatherZoneCellCatalog catalog;

        private readonly Dictionary<int, GameObject> rootsByScene =
            new Dictionary<int, GameObject>();
        private readonly List<WeatherZoneCellDefinition> sceneDefinitions =
            new List<WeatherZoneCellDefinition>(8);

        public WeatherZoneCellCatalog Catalog => catalog;
        public int ActiveCellCount => rootsByScene.Count;
        public int SpawnedZoneCount { get; private set; }

        public void Configure(WeatherZoneCellCatalog authoredCatalog)
        {
            catalog = authoredCatalog;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(WeatherZoneCellCatalog authoredCatalog) =>
            Configure(authoredCatalog);
#endif

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                EnsureSceneZones(SceneManager.GetSceneAt(index));
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            foreach (KeyValuePair<int, GameObject> pair in rootsByScene)
            {
                if (pair.Value != null)
                {
                    pair.Value.SetActive(false);
                    Destroy(pair.Value);
                }
            }

            rootsByScene.Clear();
            SpawnedZoneCount = 0;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) =>
            EnsureSceneZones(scene);

        private void HandleSceneUnloaded(Scene scene)
        {
            if (!rootsByScene.Remove(scene.handle, out GameObject root))
            {
                return;
            }

            if (root != null)
            {
                root.SetActive(false);
            }

            RecountSpawnedZones();
        }

        private void EnsureSceneZones(Scene scene)
        {
            if (catalog == null || !scene.IsValid() || !scene.isLoaded ||
                rootsByScene.ContainsKey(scene.handle))
            {
                return;
            }

            sceneDefinitions.Clear();
            if (catalog.CopyDefinitionsForScene(scene.path, sceneDefinitions) == 0)
            {
                return;
            }

            var root = new GameObject("MSC_WeatherZones_" + scene.name);
            root.SetActive(false);
            SceneManager.MoveGameObjectToScene(root, scene);
            rootsByScene.Add(scene.handle, root);
            for (int index = 0; index < sceneDefinitions.Count; index++)
            {
                CreateZone(root.transform, sceneDefinitions[index]);
            }

            root.SetActive(true);
            RecountSpawnedZones();
        }

        private static void CreateZone(
            Transform root,
            WeatherZoneCellDefinition definition)
        {
            var owner = new GameObject("WeatherZone_" + definition.StableId);
            owner.transform.SetParent(root, false);
            owner.transform.SetPositionAndRotation(
                definition.WorldCenter,
                definition.WorldRotation);
            BoxCollider volume = owner.AddComponent<BoxCollider>();
            volume.isTrigger = true;
            volume.size = definition.SizeMeters;
            WeatherZone zone = owner.AddComponent<WeatherZone>();
            zone.Configure(
                definition.StableId,
                definition.Profile,
                definition.Priority,
                volume);

            if (!definition.CreateFogVoid)
            {
                return;
            }

            var fogOwner = new GameObject("HDRP_FogVoid");
            fogOwner.transform.SetParent(owner.transform, false);
            _ = fogOwner.AddComponent<LocalVolumetricFog>();
            WeatherFogVoidAuthoring authoring =
                fogOwner.AddComponent<WeatherFogVoidAuthoring>();
            authoring.Configure(
                zone,
                definition.SizeMeters,
                definition.Profile != null
                    ? definition.Profile.FogVoidBlendDistanceMeters
                    : 0.8f);
        }

        private void RecountSpawnedZones()
        {
            int count = 0;
            foreach (KeyValuePair<int, GameObject> pair in rootsByScene)
            {
                if (pair.Value != null)
                {
                    count += pair.Value.GetComponentsInChildren<WeatherZone>(true).Length;
                }
            }

            SpawnedZoneCount = count;
        }
    }
}
