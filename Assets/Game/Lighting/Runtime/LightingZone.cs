using System;
using UnityEngine;

namespace MSC.Lighting
{
    [DisallowMultipleComponent]
    public sealed class LightingZone : MonoBehaviour
    {
        [SerializeField] private string zoneId = string.Empty;
        [SerializeField] private LightingZoneType zoneType;
        [SerializeField] private Collider boundsCollider;
        [SerializeField] private Vector3 fallbackCenter;
        [SerializeField] private Vector3 fallbackSize = new Vector3(8f, 4f, 8f);
        [SerializeField] private string[] adjacentZoneIds = Array.Empty<string>();
        [SerializeField, Min(0f)] private float exitHysteresisMeters = 0.75f;

        public string ZoneId => zoneId;
        public LightingZoneType ZoneType => zoneType;
        public float ExitHysteresisMeters => exitHysteresisMeters;

        public bool Contains(Vector3 worldPosition, bool expanded)
        {
            Bounds bounds = boundsCollider != null
                ? boundsCollider.bounds
                : new Bounds(transform.TransformPoint(fallbackCenter), fallbackSize);
            if (expanded)
            {
                bounds.Expand(exitHysteresisMeters * 2f);
            }

            return bounds.Contains(worldPosition);
        }

        public bool IsAdjacent(string otherZoneId)
        {
            for (int index = 0; index < adjacentZoneIds.Length; index++)
            {
                if (string.Equals(
                        adjacentZoneIds[index],
                        otherZoneId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string id,
            LightingZoneType type,
            Collider configuredBounds,
            Vector3 center,
            Vector3 size,
            string[] adjacent,
            float hysteresis)
        {
            zoneId = id ?? string.Empty;
            zoneType = type;
            boundsCollider = configuredBounds;
            fallbackCenter = center;
            fallbackSize = size;
            adjacentZoneIds = adjacent ?? Array.Empty<string>();
            exitHysteresisMeters = Mathf.Max(0f, hysteresis);
        }
#endif
    }
}
