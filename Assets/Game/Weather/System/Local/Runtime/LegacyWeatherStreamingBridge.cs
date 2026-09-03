using System;
using System.Collections.Generic;
using MSC.Weather.Production;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Weather.System.Local
{
    /// <summary>
    /// Keeps the accepted streamed WeatherZone topology visible to the new local
    /// weather graph and binds explicitly marked gameplay doors after their cell
    /// has materialized. Work runs only after topology changes, never per frame.
    /// </summary>
    [DefaultExecutionOrder(-440)]
    [DisallowMultipleComponent]
    public sealed class LegacyWeatherStreamingBridge : MonoBehaviour
    {
        [SerializeField] private WeatherZoneRegistry legacyRegistry;
        [SerializeField, Min(0.5f)] private float maximumPortalZoneDistanceMeters =
            5f;

        private WeatherZoneRegistry subscribedRegistry;
        private bool topologyDirty = true;

        public static LegacyWeatherStreamingBridge Active { get; private set; }
        public static event Action<LegacyWeatherStreamingBridge> ActiveChanged;
        public int AdaptedZoneCount { get; private set; }
        public int BoundDoorPortalCount { get; private set; }
        public int UnresolvedDoorPortalCount { get; private set; }

        private void OnEnable()
        {
            ActiveChanged += HandleActiveChanged;
            TryClaimOwnership();
        }

        private void OnDisable()
        {
            ActiveChanged -= HandleActiveChanged;
            ReleaseOwnership();
        }

        private void LateUpdate()
        {
            if (Active != this)
            {
                return;
            }

            BindRegistry();
            if (!topologyDirty)
            {
                return;
            }

            topologyDirty = false;
            Synchronize();
        }

        public static void NotifyTopologyChanged()
        {
            if (Active != null)
            {
                Active.topologyDirty = true;
            }
        }

        public void SynchronizeImmediately()
        {
            if (Active != this)
            {
                return;
            }

            BindRegistry();
            topologyDirty = false;
            Synchronize();
        }

        private void TryClaimOwnership()
        {
            if (!isActiveAndEnabled || Active == this || Active != null)
            {
                return;
            }

            Active = this;
            SceneManager.sceneLoaded += HandleSceneChanged;
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
            BindRegistry();
            topologyDirty = true;
            ActiveChanged?.Invoke(this);
        }

        private void ReleaseOwnership()
        {
            if (Active != this)
            {
                return;
            }

            SceneManager.sceneLoaded -= HandleSceneChanged;
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            UnbindRegistry();
            Active = null;
            ActiveChanged?.Invoke(null);
        }

        private void HandleActiveChanged(LegacyWeatherStreamingBridge owner)
        {
            if (owner == null)
            {
                TryClaimOwnership();
            }
        }

        private void BindRegistry()
        {
            WeatherZoneRegistry requested = legacyRegistry != null
                ? legacyRegistry
                : WeatherZoneRegistry.Active;
            if (requested == subscribedRegistry)
            {
                return;
            }

            UnbindRegistry();
            subscribedRegistry = requested;
            if (subscribedRegistry != null)
            {
                subscribedRegistry.Changed += HandleRegistryChanged;
            }

            topologyDirty = true;
        }

        private void UnbindRegistry()
        {
            if (subscribedRegistry != null)
            {
                subscribedRegistry.Changed -= HandleRegistryChanged;
            }

            subscribedRegistry = null;
        }

        private void Synchronize()
        {
            AdaptedZoneCount = 0;
            BoundDoorPortalCount = 0;
            UnresolvedDoorPortalCount = 0;
            if (subscribedRegistry == null)
            {
                return;
            }

            IReadOnlyList<WeatherZone> zones = subscribedRegistry.Zones;
            for (int index = 0; index < zones.Count; index++)
            {
                WeatherZone zone = zones[index];
                if (zone == null || !zone.isActiveAndEnabled)
                {
                    continue;
                }

                LegacyWeatherZoneAdapter adapter =
                    zone.GetComponent<LegacyWeatherZoneAdapter>();
                if (adapter == null)
                {
                    adapter = zone.gameObject.AddComponent<
                        LegacyWeatherZoneAdapter>();
                }

                adapter.Configure(zone);
                AdaptedZoneCount++;
            }

            DoorWeatherPortalAdapter[] portals =
                FindObjectsByType<DoorWeatherPortalAdapter>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None);
            for (int index = 0; index < portals.Length; index++)
            {
                DoorWeatherPortalAdapter portal = portals[index];
                if (portal == null || portal.Door == null)
                {
                    continue;
                }

                MonoBehaviour zoneAComponent = portal.ZoneAComponent;
                MonoBehaviour zoneBComponent = portal.ZoneBComponent;
                if (zoneAComponent is IInteriorZone ||
                    zoneBComponent is IInteriorZone)
                {
                    EnsureLegacyPortal(portal);
                    BoundDoorPortalCount++;
                    continue;
                }

                WeatherZone zone = ResolveNearestZone(
                    portal.OpeningTransform.position,
                    zones);
                if (zone == null)
                {
                    UnresolvedDoorPortalCount++;
                    continue;
                }

                LegacyWeatherZoneAdapter zoneAdapter =
                    zone.GetComponent<LegacyWeatherZoneAdapter>();
                if (zoneAdapter == null)
                {
                    UnresolvedDoorPortalCount++;
                    continue;
                }

                portal.Configure(
                    portal.StableId,
                    portal.Door,
                    zoneAdapter,
                    null,
                    portal.OpeningTransform,
                    portal.OpeningSizeMeters,
                    85f);
                EnsureLegacyPortal(portal, zone);
                BoundDoorPortalCount++;
            }
        }

        private WeatherZone ResolveNearestZone(
            Vector3 position,
            IReadOnlyList<WeatherZone> zones)
        {
            WeatherZone best = null;
            float bestDistance = maximumPortalZoneDistanceMeters;
            int bestPriority = int.MinValue;
            for (int index = 0; index < zones.Count; index++)
            {
                WeatherZone candidate = zones[index];
                if (candidate == null || !candidate.isActiveAndEnabled ||
                    !candidate.IsConfigured)
                {
                    continue;
                }

                float distance = DistanceToZone(candidate, position);
                if (distance > maximumPortalZoneDistanceMeters ||
                    (distance > bestDistance + 0.001f) ||
                    (Mathf.Abs(distance - bestDistance) <= 0.001f &&
                     candidate.Priority <= bestPriority))
                {
                    continue;
                }

                best = candidate;
                bestDistance = distance;
                bestPriority = candidate.Priority;
            }

            return best;
        }

        private static float DistanceToZone(WeatherZone zone, Vector3 position)
        {
            if (zone.Contains(position))
            {
                return 0f;
            }

            float best = float.PositiveInfinity;
            IReadOnlyList<Collider> colliders = zone.VolumeColliders;
            for (int index = 0; index < colliders.Count; index++)
            {
                Collider collider = colliders[index];
                if (collider == null || !collider.enabled ||
                    !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float distance = Vector3.Distance(
                    position,
                    collider.ClosestPoint(position));
                best = Mathf.Min(best, distance);
            }

            return best;
        }

        private static void EnsureLegacyPortal(
            DoorWeatherPortalAdapter source,
            WeatherZone affectedZone = null)
        {
            WeatherPortal legacy = source.GetComponent<WeatherPortal>();
            if (affectedZone == null && legacy != null &&
                legacy.AffectedZone != null)
            {
                affectedZone = legacy.AffectedZone;
            }

            if (affectedZone == null)
            {
                affectedZone = ResolveLegacyWeatherZone(
                    source.ZoneAComponent);
            }

            if (affectedZone == null)
            {
                affectedZone = ResolveLegacyWeatherZone(
                    source.ZoneBComponent);
            }

            if (affectedZone == null)
            {
                return;
            }

            if (legacy == null)
            {
                legacy = source.gameObject.AddComponent<WeatherPortal>();
            }

            legacy.Configure(
                source.StableId + ".legacy",
                WeatherPortalKind.Door,
                affectedZone,
                source,
                source.OpeningTransform,
                source.OpeningSizeMeters,
                6f);
        }

        private static WeatherZone ResolveLegacyWeatherZone(
            MonoBehaviour component)
        {
            // Unity keeps the managed shell of a destroyed component alive.
            // Check Unity's overloaded null before any interface/pattern access.
            if (component == null)
            {
                return null;
            }

            LegacyWeatherZoneAdapter adapter =
                component as LegacyWeatherZoneAdapter;
            return adapter != null
                ? adapter.GetComponent<WeatherZone>()
                : null;
        }

        private void HandleRegistryChanged()
        {
            topologyDirty = true;
        }

        private void HandleSceneChanged(Scene scene, LoadSceneMode mode)
        {
            topologyDirty = true;
        }

        private void HandleSceneUnloaded(Scene scene)
        {
            topologyDirty = true;
        }
    }
}
