using Unity.Profiling;
using UnityEngine;

namespace MSC.Weather.System.Local
{
    /// <summary>
    /// Cached bounded roof query. Five non-alloc upward rays run only after a
    /// configured interval or meaningful listener movement.
    /// </summary>
    [DefaultExecutionOrder(-360)]
    [DisallowMultipleComponent]
    public sealed class RoofExposureResolver : MonoBehaviour
    {
        private static readonly ProfilerMarker RoofMarker =
            new ProfilerMarker("Weather.RoofResolve");

        [SerializeField] private Transform listenerAnchor;
        [SerializeField] private LayerMask roofLayers = ~0;
        [SerializeField, Min(0.5f)] private float maximumRayDistanceMeters = 40f;
        [SerializeField, Min(0.02f)] private float resolveIntervalSeconds = 0.2f;
        [SerializeField, Min(0f)] private float movementThresholdMeters = 0.35f;
        [SerializeField, Min(0f)] private float sampleSpreadMeters = 0.28f;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[1];
        private Vector3 lastResolvedPosition;
        private float nextResolveTime;
        private bool hasResolvedPosition;

        public float RoofExposure01 { get; private set; } = 1f;
        public bool HasRoofCover => RoofExposure01 <= 0.4f;
        public Transform ListenerAnchor => listenerAnchor;

        private void Update()
        {
            Vector3 position = listenerAnchor != null
                ? listenerAnchor.position
                : transform.position;
            bool moved = !hasResolvedPosition ||
                (position - lastResolvedPosition).sqrMagnitude >=
                movementThresholdMeters * movementThresholdMeters;
            if (!moved && Time.unscaledTime < nextResolveTime)
            {
                return;
            }

            Resolve(position);
        }

        public void BindAnchor(Transform anchor)
        {
            listenerAnchor = anchor;
            hasResolvedPosition = false;
            nextResolveTime = 0f;
        }

        public float ResolveImmediately(Vector3 worldPosition)
        {
            Resolve(worldPosition);
            return RoofExposure01;
        }

        private void Resolve(Vector3 position)
        {
            using (RoofMarker.Auto())
            {
                int covered = 0;
                covered += IsCovered(position) ? 1 : 0;
                covered += IsCovered(position + Vector3.right * sampleSpreadMeters)
                    ? 1
                    : 0;
                covered += IsCovered(position - Vector3.right * sampleSpreadMeters)
                    ? 1
                    : 0;
                covered += IsCovered(position + Vector3.forward * sampleSpreadMeters)
                    ? 1
                    : 0;
                covered += IsCovered(position - Vector3.forward * sampleSpreadMeters)
                    ? 1
                    : 0;
                RoofExposure01 = 1f - covered / 5f;
                lastResolvedPosition = position;
                hasResolvedPosition = true;
                nextResolveTime = Time.unscaledTime + resolveIntervalSeconds;
            }
        }

        private bool IsCovered(Vector3 origin) =>
            Physics.RaycastNonAlloc(
                origin,
                Vector3.up,
                hitBuffer,
                maximumRayDistanceMeters,
                roofLayers,
                QueryTriggerInteraction.Ignore) > 0;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 position = listenerAnchor != null
                ? listenerAnchor.position
                : transform.position;
            Gizmos.color = Color.Lerp(Color.green, Color.red, RoofExposure01);
            Gizmos.DrawRay(position, Vector3.up * maximumRayDistanceMeters);
            Gizmos.DrawWireSphere(position, sampleSpreadMeters);
        }
#endif
    }
}
