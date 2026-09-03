using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Weather.Production
{
    /// <summary>
    /// Composition-owned registry. Streaming zones and portals register through
    /// OnEnable/OnDisable, so scene unload cannot leave stale entries.
    /// </summary>
    [DefaultExecutionOrder(-400)]
    [DisallowMultipleComponent]
    public sealed class WeatherZoneRegistry : MonoBehaviour
    {
        private readonly List<WeatherZone> zones = new List<WeatherZone>(64);
        private readonly List<WeatherPortal> portals = new List<WeatherPortal>(64);

        public static WeatherZoneRegistry Active { get; private set; }

        public IReadOnlyList<WeatherZone> Zones => zones;
        public IReadOnlyList<WeatherPortal> Portals => portals;
        public event Action Changed;

        private void OnEnable()
        {
            if (Active != null && Active != this)
            {
                enabled = false;
                return;
            }

            Active = this;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            RefreshLoadedObjects();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            if (Active == this)
            {
                Active = null;
            }

            zones.Clear();
            portals.Clear();
            Changed?.Invoke();
        }

        public void Register(WeatherZone zone)
        {
            if (zone != null && !zones.Contains(zone))
            {
                zones.Add(zone);
                Changed?.Invoke();
            }
        }

        public void Unregister(WeatherZone zone)
        {
            if (zone != null)
            {
                if (zones.Remove(zone))
                {
                    Changed?.Invoke();
                }
            }
        }

        public void Register(WeatherPortal portal)
        {
            if (portal != null && !portals.Contains(portal))
            {
                portals.Add(portal);
                Changed?.Invoke();
            }
        }

        public void Unregister(WeatherPortal portal)
        {
            if (portal != null)
            {
                if (portals.Remove(portal))
                {
                    Changed?.Invoke();
                }
            }
        }

        public bool TryGetZone(string stableId, out WeatherZone zone)
        {
            for (int index = 0; index < zones.Count; index++)
            {
                WeatherZone candidate = zones[index];
                if (candidate != null &&
                    string.Equals(
                        candidate.StableId,
                        stableId,
                        StringComparison.Ordinal))
                {
                    zone = candidate;
                    return true;
                }
            }

            zone = null;
            return false;
        }

        public void RefreshLoadedObjects()
        {
            zones.Clear();
            portals.Clear();
            WeatherZone[] loadedZones = FindObjectsByType<WeatherZone>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < loadedZones.Length; index++)
            {
                if (loadedZones[index] != null)
                {
                    zones.Add(loadedZones[index]);
                }
            }

            WeatherPortal[] loadedPortals = FindObjectsByType<WeatherPortal>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < loadedPortals.Length; index++)
            {
                if (loadedPortals[index] != null)
                {
                    portals.Add(loadedPortals[index]);
                }
            }

            Changed?.Invoke();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode) =>
            RefreshLoadedObjects();

        private void HandleSceneUnloaded(Scene scene) =>
            RefreshLoadedObjects();
    }
}
