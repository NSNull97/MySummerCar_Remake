using System;
using System.Collections.Generic;
using UnityEngine;

namespace MSC.Weather.Production
{
    [DisallowMultipleComponent]
    public sealed class WeatherZone : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private WeatherZoneProfile profile;
        [SerializeField] private int priority;
        [SerializeField] private Collider[] volumeColliders = Array.Empty<Collider>();

        public string StableId => stableId;
        public WeatherZoneProfile Profile => profile;
        public int Priority => priority;
        public IReadOnlyList<Collider> VolumeColliders => volumeColliders;

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(stableId) &&
            profile != null &&
            volumeColliders != null &&
            volumeColliders.Length > 0;

        private void OnEnable() => WeatherZoneRegistry.Active?.Register(this);

        private void OnDisable() => WeatherZoneRegistry.Active?.Unregister(this);

        public bool Contains(Vector3 worldPosition) =>
            ContainsInternal(worldPosition, true);

        /// <summary>
        /// Geometry-only containment for editor validation of deliberately
        /// inactive authoring roots. Runtime resolution must use Contains.
        /// </summary>
        public bool ContainsAuthoredGeometry(Vector3 worldPosition) =>
            ContainsInternal(worldPosition, false);

        private bool ContainsInternal(
            Vector3 worldPosition,
            bool requireActiveCollider)
        {
            if (!IsFinite(worldPosition) || volumeColliders == null)
            {
                return false;
            }

            for (int index = 0; index < volumeColliders.Length; index++)
            {
                Collider volume = volumeColliders[index];
                if (volume == null ||
                    (requireActiveCollider &&
                     (!volume.enabled || !volume.gameObject.activeInHierarchy)))
                {
                    continue;
                }

                if (volume is BoxCollider box)
                {
                    Vector3 localPosition =
                        box.transform.InverseTransformPoint(worldPosition) -
                        box.center;
                    Vector3 halfSize = box.size * 0.5f;
                    const float boundaryTolerance = 0.001f;
                    if (Mathf.Abs(localPosition.x) <=
                            halfSize.x + boundaryTolerance &&
                        Mathf.Abs(localPosition.y) <=
                            halfSize.y + boundaryTolerance &&
                        Mathf.Abs(localPosition.z) <=
                            halfSize.z + boundaryTolerance)
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

        public void Configure(
            string authoredStableId,
            WeatherZoneProfile authoredProfile,
            int authoredPriority,
            params Collider[] authoredColliders)
        {
            stableId = authoredStableId;
            profile = authoredProfile;
            priority = authoredPriority;
            volumeColliders = authoredColliders ?? Array.Empty<Collider>();
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string authoredStableId,
            WeatherZoneProfile authoredProfile,
            int authoredPriority,
            params Collider[] authoredColliders) =>
            Configure(
                authoredStableId,
                authoredProfile,
                authoredPriority,
                authoredColliders);

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = profile != null &&
                           profile.Kind == WeatherZoneKind.Shelter
                ? new Color(1f, 0.75f, 0.1f, 0.25f)
                : new Color(0.1f, 0.7f, 1f, 0.25f);
            if (volumeColliders == null)
            {
                return;
            }

            for (int index = 0; index < volumeColliders.Length; index++)
            {
                if (volumeColliders[index] is BoxCollider box)
                {
                    Matrix4x4 previous = Gizmos.matrix;
                    Gizmos.matrix = box.transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(box.center, box.size);
                    Gizmos.matrix = previous;
                }
                else if (volumeColliders[index] != null)
                {
                    Gizmos.DrawWireCube(
                        volumeColliders[index].bounds.center,
                        volumeColliders[index].bounds.size);
                }
            }
        }
#endif

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);
    }
}
