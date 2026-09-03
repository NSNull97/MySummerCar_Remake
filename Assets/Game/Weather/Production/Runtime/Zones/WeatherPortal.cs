using UnityEngine;

namespace MSC.Weather.Production
{
    public interface IWeatherPortalStateProvider
    {
        float Openness01 { get; }
    }

    public enum WeatherPortalKind
    {
        Door = 0,
        Window = 1,
        Gate = 2,
        Hatch = 3,
        OpenPassage = 4,
    }

    [DisallowMultipleComponent]
    public sealed class WeatherPortal : MonoBehaviour
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private WeatherPortalKind kind;
        [SerializeField] private WeatherZone affectedZone;
        [SerializeField] private MonoBehaviour stateProviderComponent;
        [SerializeField] private Transform portalPlane;
        [SerializeField] private Vector2 openingSizeMeters = new Vector2(1f, 2f);
        [SerializeField, Min(0.1f)] private float influenceRadiusMeters = 5f;
        [SerializeField, Range(0f, 1f)] private float visibilityFactor = 1f;
        [SerializeField, Range(0f, 1f)] private float weatherFactor = 1f;

        private IWeatherPortalStateProvider stateProvider;

        public string StableId => stableId;
        public WeatherPortalKind Kind => kind;
        public WeatherZone AffectedZone => affectedZone;
        public Transform PortalPlane => portalPlane != null ? portalPlane : transform;
        public float InfluenceRadiusMeters => influenceRadiusMeters;
        public float Openness01 => stateProvider == null
            ? kind == WeatherPortalKind.OpenPassage ? 1f : 0f
            : Mathf.Clamp01(stateProvider.Openness01);

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(stableId) &&
            affectedZone != null &&
            (stateProviderComponent is IWeatherPortalStateProvider ||
             kind == WeatherPortalKind.OpenPassage) &&
            openingSizeMeters.x > 0f && openingSizeMeters.y > 0f &&
            influenceRadiusMeters > 0f;

        private void Awake()
        {
            stateProvider = stateProviderComponent as IWeatherPortalStateProvider;
        }

        private void OnEnable()
        {
            stateProvider = stateProviderComponent as IWeatherPortalStateProvider;
            WeatherZoneRegistry.Active?.Register(this);
        }

        private void OnDisable() => WeatherZoneRegistry.Active?.Unregister(this);

        public float EvaluateInfluence(
            Vector3 listenerPosition,
            Vector3 windDirection,
            float activeWeatherFactor01)
        {
            if (!IsConfigured)
            {
                return 0f;
            }

            float distance = Vector3.Distance(
                listenerPosition,
                PortalPlane.position);
            if (distance >= influenceRadiusMeters)
            {
                return 0f;
            }

            float distanceFalloff = 1f - Mathf.SmoothStep(
                0f,
                1f,
                distance / influenceRadiusMeters);
            float area = openingSizeMeters.x * openingSizeMeters.y;
            float sizeFactor = Mathf.Clamp01(Mathf.Sqrt(area) / 2.5f);
            Vector3 normalizedWind = windDirection.sqrMagnitude > 0.000001f
                ? windDirection.normalized
                : Vector3.zero;
            float alignment = normalizedWind == Vector3.zero
                ? 0.75f
                : Mathf.Lerp(
                    0.6f,
                    1f,
                    Mathf.Abs(Vector3.Dot(
                        PortalPlane.forward,
                        normalizedWind)));
            return Mathf.Clamp01(
                Openness01 *
                sizeFactor *
                distanceFalloff *
                visibilityFactor *
                weatherFactor *
                Mathf.Clamp01(activeWeatherFactor01) *
                alignment);
        }

        public void Configure(
            string authoredStableId,
            WeatherPortalKind authoredKind,
            WeatherZone authoredZone,
            MonoBehaviour authoredStateProvider,
            Transform authoredPortalPlane,
            Vector2 authoredOpeningSizeMeters,
            float authoredInfluenceRadiusMeters)
        {
            stableId = authoredStableId;
            kind = authoredKind;
            affectedZone = authoredZone;
            stateProviderComponent = authoredStateProvider;
            portalPlane = authoredPortalPlane;
            openingSizeMeters = authoredOpeningSizeMeters;
            influenceRadiusMeters = Mathf.Max(
                0.1f,
                authoredInfluenceRadiusMeters);
            stateProvider = stateProviderComponent as IWeatherPortalStateProvider;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            string authoredStableId,
            WeatherPortalKind authoredKind,
            WeatherZone authoredZone,
            MonoBehaviour authoredStateProvider,
            Transform authoredPortalPlane,
            Vector2 authoredOpeningSizeMeters,
            float authoredInfluenceRadiusMeters) =>
            Configure(
                authoredStableId,
                authoredKind,
                authoredZone,
                authoredStateProvider,
                authoredPortalPlane,
                authoredOpeningSizeMeters,
                authoredInfluenceRadiusMeters);

        private void OnDrawGizmosSelected()
        {
            Transform plane = PortalPlane;
            Gizmos.color = Color.Lerp(Color.red, Color.green, Openness01);
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = plane.localToWorldMatrix;
            Gizmos.DrawWireCube(
                Vector3.zero,
                new Vector3(openingSizeMeters.x, openingSizeMeters.y, 0.05f));
            Gizmos.DrawRay(Vector3.zero, Vector3.forward);
            Gizmos.matrix = previous;
            Gizmos.DrawWireSphere(plane.position, influenceRadiusMeters);
        }
#endif
    }

    public enum PortalRotationAxis
    {
        X = 0,
        Y = 1,
        Z = 2,
    }

    /// <summary>
    /// Generic event-free adapter for existing transform/hinge/animator doors.
    /// The resolver polls it at a bounded cadence, so a stationary listener still
    /// reacts to a moving door without reflection or per-frame object searches.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TransformAngleWeatherPortalStateProvider : MonoBehaviour,
        IWeatherPortalStateProvider
    {
        [SerializeField] private Transform observedTransform;
        [SerializeField] private PortalRotationAxis axis = PortalRotationAxis.Y;
        [SerializeField] private float closedAngleDegrees;
        [SerializeField] private float fullyOpenAngleDegrees = 90f;

        public float Openness01 => WeatherExposureMath.CalculatePortalOpenness(
            ReadAngle(),
            closedAngleDegrees,
            fullyOpenAngleDegrees);

        private float ReadAngle()
        {
            Transform target = observedTransform != null
                ? observedTransform
                : transform;
            Vector3 angles = target.localEulerAngles;
            switch (axis)
            {
                case PortalRotationAxis.X:
                    return angles.x;
                case PortalRotationAxis.Z:
                    return angles.z;
                default:
                    return angles.y;
            }
        }

        public void Configure(
            Transform authoredTransform,
            PortalRotationAxis authoredAxis,
            float authoredClosedAngleDegrees,
            float authoredFullyOpenAngleDegrees)
        {
            observedTransform = authoredTransform;
            axis = authoredAxis;
            closedAngleDegrees = authoredClosedAngleDegrees;
            fullyOpenAngleDegrees = authoredFullyOpenAngleDegrees;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            Transform authoredTransform,
            PortalRotationAxis authoredAxis,
            float authoredClosedAngleDegrees,
            float authoredFullyOpenAngleDegrees) =>
            Configure(
                authoredTransform,
                authoredAxis,
                authoredClosedAngleDegrees,
                authoredFullyOpenAngleDegrees);
#endif
    }
}
