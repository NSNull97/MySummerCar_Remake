using System;
using MSC.Weather.Domain;
using Unity.Profiling;
using UnityEngine;

namespace MSC.Weather.Production
{
    /// <summary>
    /// Resolves loaded world-space zones and nearby portals at a bounded cadence,
    /// then smooths every exposure channel independently.
    /// </summary>
    [DefaultExecutionOrder(220)]
    [DisallowMultipleComponent]
    public sealed class WeatherExposureResolver : MonoBehaviour
    {
        private static readonly ProfilerMarker ResolveMarker =
            new ProfilerMarker("MSC.WeatherExposureResolver");
        private static readonly ProfilerMarker PortalMarker =
            new ProfilerMarker("MSC.PortalResolver");

        [SerializeField] private WeatherZoneRegistry registry;
        [SerializeField] private ProductionWeatherStateSource weatherStateSource;
        [SerializeField] private Transform visualAnchor;
        [SerializeField] private AudioListener audioListener;
        [SerializeField] private Transform gameplayAnchor;
        [SerializeField, Range(0.05f, 0.5f)] private float resolveIntervalSeconds = 0.1f;
        [SerializeField, Range(0f, 1f)] private float zoneExitHysteresisSeconds = 0.2f;
        [SerializeField, Min(0f)] private float precipitationSmoothingSeconds = 0.35f;
        [SerializeField, Min(0f)] private float fogSmoothingSeconds = 1.2f;
        [SerializeField, Min(0f)] private float windSmoothingSeconds = 1f;
        [SerializeField, Min(0f)] private float audioSmoothingSeconds = 0.8f;

        private WeatherExposureState current = WeatherExposureState.Exterior;
        private WeatherExposureState target = WeatherExposureState.Exterior;
        private WeatherZone currentZone;
        private float zoneExitDeadline;
        private bool zoneExitPending;
        private float nextResolveTime;
        private int activePortalCount;
        private float strongestPortalOpenness;
        private float strongestPortalDistance;
        private float nextRuntimeAnchorSearchTime;

        public WeatherExposureState Current => current;
        public WeatherExposureState Target => target;
        public WeatherZone CurrentZone => currentZone;
        public int ActivePortalCount => activePortalCount;
        public float StrongestPortalOpenness => strongestPortalOpenness;
        public float StrongestPortalDistance => strongestPortalDistance;
        public Vector3 VisualAnchorPosition => ResolveVisualPosition();
        public Vector3 AudioAnchorPosition => IsUsable(audioListener)
            ? audioListener.transform.position
            : ResolveVisualPosition();

        public event Action<WeatherExposureState> ExposureChanged;

        public void ConfigureForAuthoring(
            WeatherZoneRegistry authoredRegistry,
            ProductionWeatherStateSource authoredWeatherSource,
            Transform authoredVisualAnchor,
            AudioListener authoredAudioListener,
            Transform authoredGameplayAnchor)
        {
            registry = authoredRegistry;
            weatherStateSource = authoredWeatherSource;
            visualAnchor = authoredVisualAnchor;
            audioListener = authoredAudioListener;
            gameplayAnchor = authoredGameplayAnchor;
        }

        public bool TryBindRuntimeCamera(Camera camera)
        {
            if (!IsUsable(camera))
            {
                return false;
            }

            visualAnchor = camera.transform;
            AudioListener cameraListener = camera.GetComponent<AudioListener>();
            audioListener = IsUsable(cameraListener) ? cameraListener : null;
            ResolveImmediately();
            return true;
        }

        private void Awake()
        {
            if (registry == null)
            {
                registry = WeatherZoneRegistry.Active;
            }

            RefreshRuntimeAnchors(force: true);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (now >= nextResolveTime)
            {
                nextResolveTime = now + resolveIntervalSeconds;
                ResolveTarget(now);
            }

            SmoothCurrent(Time.unscaledDeltaTime);
        }

        public void ResolveImmediately()
        {
            RefreshRuntimeAnchors(force: true);
            ResolveTarget(Time.unscaledTime);
            current = target;
            ExposureChanged?.Invoke(current);
        }

        private void ResolveTarget(float now)
        {
            using (ResolveMarker.Auto())
            {
                if (registry == null)
                {
                    registry = WeatherZoneRegistry.Active;
                }

                RefreshRuntimeAnchors(force: false);
                Vector3 visualPosition = ResolveVisualPosition();
                WeatherZone selected = SelectZone(visualPosition);
                if (selected != null)
                {
                    currentZone = selected;
                    zoneExitPending = false;
                }
                else if (currentZone != null)
                {
                    if (!zoneExitPending)
                    {
                        zoneExitPending = true;
                        zoneExitDeadline = now + zoneExitHysteresisSeconds;
                    }

                    if (now < zoneExitDeadline)
                    {
                        selected = currentZone;
                    }
                    else
                    {
                        currentZone = null;
                        zoneExitPending = false;
                    }
                }

                currentZone = selected;
                WeatherExposureState closed = currentZone == null ||
                                              currentZone.Profile == null
                    ? WeatherExposureState.Exterior
                    : currentZone.Profile.CreateClosedExposure();
                float portalInfluence = ResolvePortalInfluence(
                    AudioAnchorPosition,
                    currentZone);
                target = currentZone == null
                    ? WeatherExposureState.Exterior
                    : WeatherExposureMath.ApplyPortal(
                        closed,
                        portalInfluence);
            }
        }

        private WeatherZone SelectZone(Vector3 position)
        {
            if (registry == null)
            {
                return null;
            }

            WeatherZone best = null;
            var zones = registry.Zones;
            for (int index = 0; index < zones.Count; index++)
            {
                WeatherZone candidate = zones[index];
                if (candidate == null || !candidate.isActiveAndEnabled ||
                    !candidate.Contains(position))
                {
                    continue;
                }

                if (best == null || candidate.Priority > best.Priority ||
                    (candidate.Priority == best.Priority &&
                     string.CompareOrdinal(
                         candidate.StableId,
                         best.StableId) < 0))
                {
                    best = candidate;
                }
            }

            return best;
        }

        private float ResolvePortalInfluence(
            Vector3 listenerPosition,
            WeatherZone zone)
        {
            using (PortalMarker.Auto())
            {
                activePortalCount = 0;
                strongestPortalOpenness = 0f;
                strongestPortalDistance = 0f;
                if (registry == null || zone == null)
                {
                    return 0f;
                }

                WeatherRuntimeState weather = weatherStateSource != null &&
                                              weatherStateSource.IsReady
                    ? weatherStateSource.Current
                    : default;
                Vector3 windDirection = weather.IsValid
                    ? weather.WindDirection
                    : Vector3.zero;
                float weatherFactor = weather.IsValid
                    ? Mathf.Max(
                        weather.Precipitation01,
                        Mathf.Max(
                            weather.FogIntensity01,
                            weather.WindSpeed01))
                    : 1f;
                float combined = 0f;
                var portals = registry.Portals;
                for (int index = 0; index < portals.Count; index++)
                {
                    WeatherPortal portal = portals[index];
                    if (portal == null || !portal.isActiveAndEnabled ||
                        portal.AffectedZone != zone)
                    {
                        continue;
                    }

                    float influence = portal.EvaluateInfluence(
                        listenerPosition,
                        windDirection,
                        weatherFactor);
                    if (influence <= 0f)
                    {
                        continue;
                    }

                    activePortalCount++;
                    combined = WeatherExposureMath.CombinePortalInfluence(
                        combined,
                        influence);
                    if (portal.Openness01 > strongestPortalOpenness)
                    {
                        strongestPortalOpenness = portal.Openness01;
                        strongestPortalDistance = Vector3.Distance(
                            listenerPosition,
                            portal.PortalPlane.position);
                    }
                }

                return combined;
            }
        }

        private void SmoothCurrent(float deltaSeconds)
        {
            WeatherExposureState previous = current;
            float common = WeatherExposureMath.ExponentialBlendFactor(
                deltaSeconds,
                audioSmoothingSeconds);
            float precipitation = WeatherExposureMath.ExponentialBlendFactor(
                deltaSeconds,
                precipitationSmoothingSeconds);
            float fog = WeatherExposureMath.ExponentialBlendFactor(
                deltaSeconds,
                fogSmoothingSeconds);
            float wind = WeatherExposureMath.ExponentialBlendFactor(
                deltaSeconds,
                windSmoothingSeconds);
            current = new WeatherExposureState(
                Mathf.Lerp(current.EnclosureFactor, target.EnclosureFactor, common),
                Mathf.Lerp(current.ShelterFactor, target.ShelterFactor, common),
                Mathf.Lerp(
                    current.PrecipitationExposure,
                    target.PrecipitationExposure,
                    precipitation),
                Mathf.Lerp(current.FogExposure, target.FogExposure, fog),
                Mathf.Lerp(current.WindExposure, target.WindExposure, wind),
                Mathf.Lerp(
                    current.WeatherAudioExposure,
                    target.WeatherAudioExposure,
                    common),
                Mathf.Lerp(current.ThunderExposure, target.ThunderExposure, common),
                Mathf.Lerp(current.PortalExposure, target.PortalExposure, common),
                Mathf.Lerp(current.IndoorFactor, target.IndoorFactor, common));
            if (!Approximately(previous, current))
            {
                ExposureChanged?.Invoke(current);
            }
        }

        private Vector3 ResolveVisualPosition()
        {
            if (IsUsable(audioListener))
            {
                return audioListener.transform.position;
            }

            if (visualAnchor != null)
            {
                Camera authoredCamera = visualAnchor.GetComponent<Camera>();
                if (authoredCamera == null || IsUsable(authoredCamera))
                {
                    return visualAnchor.position;
                }
            }

            if (gameplayAnchor != null)
            {
                return gameplayAnchor.position;
            }

            return transform.position;
        }

        private void RefreshRuntimeAnchors(bool force)
        {
            if (IsUsable(audioListener))
            {
                visualAnchor = audioListener.transform;
                return;
            }

            // An explicitly authored non-camera anchor is authoritative. Runtime
            // discovery is only needed for the disabled startup camera (or a
            // genuinely missing anchor), not for tests, labs, or custom listeners
            // that deliberately provide their own transform.
            if (IsUsableVisualAnchor())
            {
                return;
            }

            float now = Time.unscaledTime;
            if (!force && now < nextRuntimeAnchorSearchTime)
            {
                return;
            }

            nextRuntimeAnchorSearchTime = now + resolveIntervalSeconds;
            Camera mainCamera = Camera.main;
            if (IsUsable(mainCamera))
            {
                AudioListener mainListener =
                    mainCamera.GetComponent<AudioListener>();
                if (IsUsable(mainListener))
                {
                    audioListener = mainListener;
                    visualAnchor = mainListener.transform;
                    return;
                }
            }

            AudioListener[] listeners = FindObjectsByType<AudioListener>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int index = 0; index < listeners.Length; index++)
            {
                if (!IsUsable(listeners[index]))
                {
                    continue;
                }

                audioListener = listeners[index];
                visualAnchor = listeners[index].transform;
                return;
            }

            if (IsUsable(mainCamera))
            {
                visualAnchor = mainCamera.transform;
            }
        }

        private bool IsUsableVisualAnchor()
        {
            if (visualAnchor == null || !visualAnchor.gameObject.activeInHierarchy)
            {
                return false;
            }

            Camera camera = visualAnchor.GetComponent<Camera>();
            return camera == null || IsUsable(camera);
        }

        private static bool IsUsable(Behaviour value) =>
            value != null && value.enabled && value.gameObject.activeInHierarchy;

        private static bool Approximately(
            in WeatherExposureState left,
            in WeatherExposureState right) =>
            Mathf.Abs(left.EnclosureFactor - right.EnclosureFactor) < 0.0005f &&
            Mathf.Abs(left.PrecipitationExposure - right.PrecipitationExposure) < 0.0005f &&
            Mathf.Abs(left.FogExposure - right.FogExposure) < 0.0005f &&
            Mathf.Abs(left.WindExposure - right.WindExposure) < 0.0005f &&
            Mathf.Abs(left.WeatherAudioExposure - right.WeatherAudioExposure) < 0.0005f &&
            Mathf.Abs(left.ThunderExposure - right.ThunderExposure) < 0.0005f &&
            Mathf.Abs(left.PortalExposure - right.PortalExposure) < 0.0005f;
    }
}
