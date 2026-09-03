using System;
using MSC.Weather.System;
using Unity.Profiling;
using UnityEngine;

namespace MSC.Weather.System.Local
{
    [DefaultExecutionOrder(-370)]
    [DisallowMultipleComponent]
    public sealed class InteriorZoneResolver : MonoBehaviour
    {
        private static readonly ProfilerMarker ResolveMarker =
            new ProfilerMarker("Weather.ZoneResolve");

        [SerializeField] private WeatherPortalSystem portalSystem;
        [SerializeField] private GameWeatherSystem gameWeatherSystem;
        [SerializeField] private Transform listenerAnchor;
        [SerializeField, Min(0.02f)] private float resolveIntervalSeconds = 0.1f;
        [SerializeField, Min(0f)] private float zoneExitHysteresisSeconds = 0.2f;
        [SerializeField, Min(0f)] private float contextSmoothingSeconds = 0.35f;

        private IInteriorZone currentZone;
        private IEnvironmentPortal nearestPortal;
        private LocalWeatherContext current = LocalWeatherContext.Outdoor;
        private LocalWeatherContext target = LocalWeatherContext.Outdoor;
        private float nextResolveTime;
        private float secondsOutsideCurrentZone;
        private float nearestPortalDistance = float.PositiveInfinity;

        public event Action<LocalWeatherContext> ContextChanged;

        public LocalWeatherContext Current => current;
        public IInteriorZone CurrentZone => currentZone;
        public IEnvironmentPortal NearestPortal => nearestPortal;
        public float NearestPortalDistanceMeters => nearestPortalDistance;
        public Transform ListenerAnchor => listenerAnchor;

        private void Awake()
        {
            if (portalSystem == null)
            {
                portalSystem = WeatherPortalSystem.Active;
            }
        }

        private void OnEnable()
        {
            WeatherPortalSystem.ActiveChanged += HandlePortalSystemChanged;
            if (gameWeatherSystem != null)
            {
                gameWeatherSystem.PresentationCameraChanged += HandleCameraChanged;
                if (gameWeatherSystem.PresentationCamera != null)
                {
                    listenerAnchor =
                        gameWeatherSystem.PresentationCamera.transform;
                }
            }
        }

        private void OnDisable()
        {
            WeatherPortalSystem.ActiveChanged -= HandlePortalSystemChanged;
            if (gameWeatherSystem != null)
            {
                gameWeatherSystem.PresentationCameraChanged -= HandleCameraChanged;
            }
        }

        private void Update()
        {
            float delta = Time.unscaledDeltaTime;
            if (Time.unscaledTime >= nextResolveTime)
            {
                nextResolveTime = Time.unscaledTime + resolveIntervalSeconds;
                ResolveTarget(resolveIntervalSeconds);
            }

            SmoothCurrent(delta);
        }

        public void BindAnchor(Transform anchor)
        {
            listenerAnchor = anchor;
            nextResolveTime = 0f;
        }

        public LocalWeatherContext ResolveImmediately(Vector3 worldPosition)
        {
            ResolveTargetAt(worldPosition, resolveIntervalSeconds);
            current = target;
            return current;
        }

        private void ResolveTarget(float elapsedSeconds)
        {
            Vector3 position = listenerAnchor != null
                ? listenerAnchor.position
                : transform.position;
            ResolveTargetAt(position, elapsedSeconds);
        }

        private void ResolveTargetAt(Vector3 position, float elapsedSeconds)
        {
            using (ResolveMarker.Auto())
            {
                if (portalSystem == null)
                {
                    portalSystem = WeatherPortalSystem.Active;
                }

                IInteriorZone resolved = ResolveBestZone(position);
                if (resolved == null && currentZone != null &&
                    IsAlive(currentZone))
                {
                    secondsOutsideCurrentZone += Mathf.Max(0f, elapsedSeconds);
                    if (secondsOutsideCurrentZone < zoneExitHysteresisSeconds)
                    {
                        resolved = currentZone;
                    }
                }
                else
                {
                    secondsOutsideCurrentZone = 0f;
                }

                currentZone = resolved;
                if (resolved == null)
                {
                    nearestPortal = null;
                    nearestPortalDistance = float.PositiveInfinity;
                    target = LocalWeatherContext.Outdoor;
                    return;
                }

                LocalWeatherZoneSettings settings = resolved.Settings;
                PortalTransmission path = PortalTransmission.Blocked;
                portalSystem?.TryResolveOutdoorTransmission(resolved, out path);
                if (portalSystem == null ||
                    !portalSystem.TryGetNearestPortal(
                        resolved,
                        position,
                        out nearestPortal,
                        out nearestPortalDistance))
                {
                    nearestPortal = null;
                    nearestPortalDistance = float.PositiveInfinity;
                }

                float depth = nearestPortal == null
                    ? 1f
                    : Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.Clamp01(
                            nearestPortalDistance /
                            settings.TransitionDistanceMeters));
                float portalDirection = CalculatePortalDirection(
                    nearestPortal,
                    position);
                float outdoor = ApplyPath(
                    settings.OutdoorExposure01,
                    path.Visual01 * Mathf.Lerp(0.75f, 1f, portalDirection),
                    depth,
                    0.1f);
                float precipitation = ApplyPath(
                    settings.PrecipitationExposure01,
                    path.Precipitation01 *
                    Mathf.Lerp(0.65f, 1f, portalDirection),
                    depth,
                    0.02f);
                float fog = ApplyPath(
                    settings.FogExposure01,
                    path.Fog01 * Mathf.Lerp(0.75f, 1f, portalDirection),
                    depth,
                    0.1f);
                float wind = ApplyPath(
                    settings.WindExposure01,
                    path.Wind01 * Mathf.Lerp(0.35f, 1f, portalDirection),
                    depth,
                    0.15f);
                float audio = ApplyPath(
                    settings.AudioLeak01,
                    path.Audio01 * Mathf.Lerp(0.85f, 1f, portalDirection),
                    depth,
                    0.4f);
                float exteriorAudio = ApplyPath(
                    settings.ExteriorAudioExposure01,
                    path.Audio01 * Mathf.Lerp(0.85f, 1f, portalDirection),
                    depth,
                    0.45f);
                float thunder = ApplyPath(
                    settings.ThunderExposure01,
                    path.Thunder01 * Mathf.Lerp(0.95f, 1f, portalDirection),
                    depth,
                    0.7f);
                float openness = nearestPortal?.Openness01 ?? 0f;
                float exposureCompensation =
                    settings.ExposureCompensationEv * depth * (1f - outdoor);
                target = new LocalWeatherContext(
                    resolved,
                    true,
                    true,
                    outdoor,
                    precipitation,
                    fog,
                    wind,
                    audio,
                    exteriorAudio,
                    thunder,
                    depth,
                    0f,
                    Mathf.Max(openness, path.Audio01),
                    exposureCompensation);
            }
        }

        private IInteriorZone ResolveBestZone(Vector3 position)
        {
            if (portalSystem == null)
            {
                return null;
            }

            IInteriorZone best = null;
            var zones = portalSystem.Zones;
            for (int index = 0; index < zones.Count; index++)
            {
                IInteriorZone candidate = zones[index];
                if (!IsAlive(candidate) || !candidate.IsAvailable ||
                    !candidate.Contains(position))
                {
                    continue;
                }

                if (best == null || candidate.Priority > best.Priority ||
                    (candidate.Priority == best.Priority &&
                     string.CompareOrdinal(candidate.StableId, best.StableId) < 0))
                {
                    best = candidate;
                }
            }

            return best;
        }

        private void SmoothCurrent(float deltaSeconds)
        {
            LocalWeatherContext previous = current;
            float t = contextSmoothingSeconds <= 0f
                ? 1f
                : 1f - Mathf.Exp(-Mathf.Max(0f, deltaSeconds) /
                                 contextSmoothingSeconds);
            current = LocalWeatherContext.Lerp(current, target, t);
            if (!Approximately(previous, current))
            {
                ContextChanged?.Invoke(current);
            }
        }

        private void HandlePortalSystemChanged(WeatherPortalSystem system)
        {
            portalSystem = system;
            nextResolveTime = 0f;
        }

        private void HandleCameraChanged(Camera camera)
        {
            if (camera != null)
            {
                BindAnchor(camera.transform);
            }
        }

        private static float ApplyPath(
            float closedValue,
            float pathTransmission,
            float interiorDepth,
            float deepRoomFraction)
        {
            float localTransmission = Mathf.Clamp01(pathTransmission) *
                Mathf.Lerp(1f, Mathf.Clamp01(deepRoomFraction), interiorDepth);
            return Mathf.Lerp(
                Mathf.Clamp01(closedValue),
                1f,
                localTransmission);
        }

        private static float CalculatePortalDirection(
            IEnvironmentPortal portal,
            Vector3 listenerPosition)
        {
            Transform opening = portal?.OpeningTransform;
            if (opening == null)
            {
                return 1f;
            }

            Vector3 offset = listenerPosition - opening.position;
            if (offset.sqrMagnitude <= 0.0001f)
            {
                return 1f;
            }

            return Mathf.Abs(Vector3.Dot(
                opening.forward.normalized,
                offset.normalized));
        }

        private static bool Approximately(
            in LocalWeatherContext left,
            in LocalWeatherContext right)
        {
            const float threshold = 0.001f;
            return string.Equals(
                       left.CurrentZoneId,
                       right.CurrentZoneId,
                       StringComparison.Ordinal) &&
                   left.IsInside == right.IsInside &&
                   left.HasRoofCover == right.HasRoofCover &&
                   Mathf.Abs(left.OutdoorExposure01 -
                             right.OutdoorExposure01) < threshold &&
                   Mathf.Abs(left.PrecipitationExposure01 -
                             right.PrecipitationExposure01) < threshold &&
                   Mathf.Abs(left.FogExposure01 - right.FogExposure01) < threshold &&
                   Mathf.Abs(left.WindExposure01 - right.WindExposure01) < threshold &&
                   Mathf.Abs(left.AudioLeak01 - right.AudioLeak01) < threshold &&
                   Mathf.Abs(left.ExteriorAudioExposure01 -
                             right.ExteriorAudioExposure01) < threshold &&
                   Mathf.Abs(left.ThunderExposure01 -
                             right.ThunderExposure01) < threshold &&
                   Mathf.Abs(left.InteriorDepth01 -
                             right.InteriorDepth01) < threshold &&
                   Mathf.Abs(left.RoofExposure01 -
                             right.RoofExposure01) < threshold &&
                   Mathf.Abs(left.DoorTransmission01 -
                             right.DoorTransmission01) < threshold &&
                   Mathf.Abs(left.ExposureCompensationEv -
                             right.ExposureCompensationEv) < threshold;
        }

        private static bool IsAlive(object value)
        {
            if (value == null)
            {
                return false;
            }

            return !(value is UnityEngine.Object unityObject) || unityObject != null;
        }
    }
}
