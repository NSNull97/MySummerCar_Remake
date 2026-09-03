using System;
using UnityEngine;

namespace MSC.Weather.System.Local
{
    [DisallowMultipleComponent]
    public sealed class InteriorZone : MonoBehaviour, IInteriorZone
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private EnvironmentZoneProfile profile;
        [SerializeField] private int priority;
        [SerializeField] private Collider[] volumeColliders = Array.Empty<Collider>();

        public string StableId => stableId;
        public int Priority => priority;
        public LocalWeatherZoneSettings Settings => profile != null
            ? profile.CreateSettings()
            : LocalWeatherZoneSettings.ClosedInterior;
        public bool IsAvailable =>
            isActiveAndEnabled &&
            !string.IsNullOrWhiteSpace(stableId) &&
            profile != null &&
            volumeColliders != null && volumeColliders.Length > 0;
        public EnvironmentZoneProfile Profile => profile;

        private void OnEnable()
        {
            WeatherPortalSystem.ActiveChanged += HandleRegistryChanged;
            WeatherPortalSystem.Active?.RegisterZone(this);
        }

        private void OnDisable()
        {
            WeatherPortalSystem.ActiveChanged -= HandleRegistryChanged;
            WeatherPortalSystem.Active?.UnregisterZone(this);
        }

        public bool Contains(Vector3 worldPosition)
        {
            if (!IsAvailable || !IsFinite(worldPosition))
            {
                return false;
            }

            for (int index = 0; index < volumeColliders.Length; index++)
            {
                Collider volume = volumeColliders[index];
                if (volume == null || !volume.enabled ||
                    !volume.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (volume is BoxCollider box)
                {
                    Vector3 local = box.transform.InverseTransformPoint(
                        worldPosition) - box.center;
                    Vector3 half = box.size * 0.5f;
                    if (Mathf.Abs(local.x) <= half.x + 0.001f &&
                        Mathf.Abs(local.y) <= half.y + 0.001f &&
                        Mathf.Abs(local.z) <= half.z + 0.001f)
                    {
                        return true;
                    }

                    continue;
                }

                Vector3 closest = volume.ClosestPoint(worldPosition);
                if ((closest - worldPosition).sqrMagnitude <= 0.000001f)
                {
                    return true;
                }
            }

            return false;
        }

        public void ConfigureForAuthoring(
            string authoredStableId,
            EnvironmentZoneProfile authoredProfile,
            int authoredPriority,
            params Collider[] authoredColliders)
        {
            stableId = authoredStableId;
            profile = authoredProfile;
            priority = authoredPriority;
            volumeColliders = authoredColliders ?? Array.Empty<Collider>();
        }

        private void HandleRegistryChanged(WeatherPortalSystem registry)
        {
            registry?.RegisterZone(this);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
