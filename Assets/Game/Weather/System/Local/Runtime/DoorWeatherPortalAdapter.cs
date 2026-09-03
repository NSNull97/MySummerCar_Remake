using System;
using MSC.Interaction.Architecture;
using MSC.Weather.Production;
using UnityEngine;

namespace MSC.Weather.System.Local
{
    public enum EnvironmentPortalMotionState
    {
        Closed = 0,
        Opening = 1,
        Open = 2,
        Closing = 3,
        Ajar = 4,
    }

    /// <summary>
    /// Bounded polling adapter for the project-owned door. The door API and its
    /// gameplay behavior remain untouched; adapter events are derived locally.
    /// </summary>
    [DefaultExecutionOrder(-420)]
    [DisallowMultipleComponent]
    public sealed class DoorWeatherPortalAdapter : MonoBehaviour,
        IEnvironmentPortal,
        IWeatherPortalStateProvider
    {
        [SerializeField] private string stableId = string.Empty;
        [SerializeField] private HingedDoorInteractionTarget door;
        [SerializeField] private MonoBehaviour zoneAComponent;
        [SerializeField] private MonoBehaviour zoneBComponent;
        [SerializeField] private bool connectsToOutdoor;
        [SerializeField] private Transform openingTransform;
        [SerializeField] private Vector2 openingSizeMeters = new Vector2(0.9f, 2f);
        [SerializeField, Range(1f, 170f)] private float fullyOpenAngleDegrees = 85f;
        [SerializeField, Range(0f, 1f)] private float closedAudioTransmission = 0.12f;
        [SerializeField, Range(0f, 1f)] private float closedThunderTransmission = 0.55f;
        [SerializeField] private AnimationCurve visualTransmission =
            CreateVisualCurve();
        [SerializeField] private AnimationCurve audioTransmission =
            CreateAudioCurve();
        [SerializeField] private AnimationCurve windTransmission =
            CreateWindCurve();
        [SerializeField] private AnimationCurve fogTransmission =
            CreateFogCurve();
        [SerializeField] private AnimationCurve precipitationTransmission =
            CreatePrecipitationCurve();
        [SerializeField, Min(0.02f)] private float pollIntervalSeconds = 0.1f;
        [SerializeField, Range(0.0001f, 0.1f)] private float changeThreshold01 = 0.005f;

        private IInteriorZone zoneA;
        private IInteriorZone zoneB;
        private float lastOpenness01;
        private float nextPollTime;
        private EnvironmentPortalMotionState motionState;
        private bool initialized;

        public event Action<IEnvironmentPortal> TransmissionChanged;
        public event Action<EnvironmentPortalMotionState> MotionStarted;
        public event Action<EnvironmentPortalMotionState> MotionCompleted;
        public event Action Closed;

        public string StableId => stableId;
        public IInteriorZone ZoneA => IsAlive(zoneA) ? zoneA : null;
        public IInteriorZone ZoneB => IsAlive(zoneB) ? zoneB : null;
        public MonoBehaviour ZoneAComponent =>
            zoneAComponent != null ? zoneAComponent : null;
        public MonoBehaviour ZoneBComponent =>
            zoneBComponent != null ? zoneBComponent : null;
        public HingedDoorInteractionTarget Door => door;
        public bool ConnectsToOutdoor => connectsToOutdoor;
        public Transform OpeningTransform =>
            openingTransform != null ? openingTransform : transform;
        public Vector2 OpeningSizeMeters => openingSizeMeters;
        public float Openness01 => door != null
            ? Mathf.Clamp01(door.OpenNormalized)
            : 0f;
        public float CurrentAngleDegrees => Openness01 * fullyOpenAngleDegrees;
        public EnvironmentPortalMotionState MotionState => motionState;
        public bool IsAvailable =>
            isActiveAndEnabled &&
            door != null &&
            !string.IsNullOrWhiteSpace(stableId) &&
            (ZoneA != null || ZoneB != null) &&
            openingSizeMeters.x > 0f && openingSizeMeters.y > 0f;
        public PortalTransmission Transmission
        {
            get
            {
                float openness = Openness01;
                float area01 = OpeningAreaTransmission01();
                float audioMaximum = Mathf.Lerp(0.5f, 1f, area01);
                float thunderMaximum = Mathf.Lerp(0.7f, 1f, area01);
                return new PortalTransmission(
                    Evaluate(visualTransmission, openness) *
                    Mathf.Lerp(0.45f, 1f, area01),
                    Mathf.Lerp(
                        closedAudioTransmission *
                        Mathf.Lerp(0.55f, 1f, area01),
                        audioMaximum,
                        Evaluate(audioTransmission, openness)),
                    Evaluate(windTransmission, openness) *
                    Mathf.Lerp(0.3f, 1f, area01),
                    Evaluate(fogTransmission, openness) *
                    Mathf.Lerp(0.45f, 1f, area01),
                    Evaluate(precipitationTransmission, openness) *
                    Mathf.Lerp(0.25f, 1f, area01),
                    Mathf.Lerp(
                        closedThunderTransmission *
                        Mathf.Lerp(0.75f, 1f, area01),
                        thunderMaximum,
                        Evaluate(audioTransmission, openness)));
            }
        }

        private void Awake()
        {
            ResolveZones();
            InitializeState();
        }

        private void OnEnable()
        {
            ResolveZones();
            InitializeState();
            WeatherPortalSystem.ActiveChanged += HandleRegistryChanged;
            WeatherPortalSystem.Active?.RegisterPortal(this);
        }

        private void OnDisable()
        {
            WeatherPortalSystem.ActiveChanged -= HandleRegistryChanged;
            WeatherPortalSystem.Active?.UnregisterPortal(this);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextPollTime)
            {
                return;
            }

            nextPollTime = Time.unscaledTime + pollIntervalSeconds;
            PollDoor();
        }

        public void PollDoor()
        {
            if (door == null)
            {
                return;
            }

            float openness = Openness01;
            float delta = openness - lastOpenness01;
            EnvironmentPortalMotionState previousState = motionState;
            EnvironmentPortalMotionState nextState = ResolveState(openness, delta);
            bool transmissionChanged = !initialized ||
                Mathf.Abs(delta) >= changeThreshold01;
            bool stateChanged = !initialized || nextState != previousState;

            lastOpenness01 = openness;
            motionState = nextState;
            initialized = true;
            if (transmissionChanged)
            {
                TransmissionChanged?.Invoke(this);
            }

            if (!stateChanged)
            {
                return;
            }

            if (nextState == EnvironmentPortalMotionState.Opening ||
                nextState == EnvironmentPortalMotionState.Closing)
            {
                MotionStarted?.Invoke(nextState);
            }

            if (nextState == EnvironmentPortalMotionState.Open ||
                nextState == EnvironmentPortalMotionState.Closed ||
                nextState == EnvironmentPortalMotionState.Ajar)
            {
                MotionCompleted?.Invoke(nextState);
            }

            if (nextState == EnvironmentPortalMotionState.Closed)
            {
                Closed?.Invoke();
            }
        }

        public void Configure(
            string authoredStableId,
            HingedDoorInteractionTarget authoredDoor,
            MonoBehaviour authoredZoneA,
            MonoBehaviour authoredZoneB,
            Transform authoredOpening,
            Vector2 authoredOpeningSizeMeters,
            float authoredFullyOpenAngleDegrees = 85f)
        {
            stableId = authoredStableId;
            door = authoredDoor;
            zoneAComponent = authoredZoneA;
            zoneBComponent = authoredZoneB;
            connectsToOutdoor = authoredZoneA == null || authoredZoneB == null;
            openingTransform = authoredOpening;
            openingSizeMeters = authoredOpeningSizeMeters;
            fullyOpenAngleDegrees = Mathf.Clamp(
                authoredFullyOpenAngleDegrees,
                1f,
                170f);
            ResolveZones();
            InitializeState();
            if (isActiveAndEnabled)
            {
                WeatherPortalSystem.Active?.UnregisterPortal(this);
                WeatherPortalSystem.Active?.RegisterPortal(this);
            }
        }

        public void ConfigureForAuthoring(
            string authoredStableId,
            HingedDoorInteractionTarget authoredDoor,
            MonoBehaviour authoredZoneA,
            MonoBehaviour authoredZoneB,
            Transform authoredOpening,
            Vector2 authoredOpeningSizeMeters,
            float authoredFullyOpenAngleDegrees = 85f) =>
            Configure(
                authoredStableId,
                authoredDoor,
                authoredZoneA,
                authoredZoneB,
                authoredOpening,
                authoredOpeningSizeMeters,
                authoredFullyOpenAngleDegrees);

#if UNITY_EDITOR
        private void OnValidate()
        {
            stableId = stableId?.Trim() ?? string.Empty;
            openingSizeMeters.x = Mathf.Max(0.05f, openingSizeMeters.x);
            openingSizeMeters.y = Mathf.Max(0.05f, openingSizeMeters.y);
            fullyOpenAngleDegrees = Mathf.Clamp(fullyOpenAngleDegrees, 1f, 170f);
            pollIntervalSeconds = Mathf.Max(0.02f, pollIntervalSeconds);
            changeThreshold01 = Mathf.Clamp(changeThreshold01, 0.0001f, 0.1f);
            ResolveZones();
        }
#endif

        private void ResolveZones()
        {
            zoneA = zoneAComponent != null
                ? zoneAComponent as IInteriorZone
                : null;
            zoneB = zoneBComponent != null
                ? zoneBComponent as IInteriorZone
                : null;
        }

        private static bool IsAlive(IInteriorZone zone)
        {
            if (zone == null)
            {
                return false;
            }

            return !(zone is UnityEngine.Object unityObject) ||
                unityObject != null;
        }

        private void InitializeState()
        {
            lastOpenness01 = Openness01;
            motionState = ResolveState(lastOpenness01, 0f);
            initialized = true;
        }

        private EnvironmentPortalMotionState ResolveState(
            float openness,
            float delta)
        {
            if (openness <= 0.01f && (door == null || !door.TargetOpen))
            {
                return EnvironmentPortalMotionState.Closed;
            }

            if (openness >= 0.99f && door != null && door.TargetOpen)
            {
                return EnvironmentPortalMotionState.Open;
            }

            if (delta > changeThreshold01 * 0.25f ||
                (door != null && door.TargetOpen && openness < 0.99f))
            {
                return EnvironmentPortalMotionState.Opening;
            }

            if (delta < -changeThreshold01 * 0.25f ||
                (door != null && !door.TargetOpen && openness > 0.01f))
            {
                return EnvironmentPortalMotionState.Closing;
            }

            return EnvironmentPortalMotionState.Ajar;
        }

        private void HandleRegistryChanged(WeatherPortalSystem registry)
        {
            registry?.RegisterPortal(this);
        }

        private static float Evaluate(AnimationCurve curve, float openness) =>
            Mathf.Clamp01(curve == null
                ? openness
                : curve.Evaluate(Mathf.Clamp01(openness)));

        private float OpeningAreaTransmission01()
        {
            float areaSquareMeters = Mathf.Max(
                0f,
                openingSizeMeters.x * openingSizeMeters.y);
            return Mathf.InverseLerp(0.2f, 1.8f, areaSquareMeters);
        }

        private static AnimationCurve CreateVisualCurve() => new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.1f, 0.03f),
            new Keyframe(0.3f, 0.28f),
            new Keyframe(0.6f, 0.82f),
            new Keyframe(1f, 1f));

        private static AnimationCurve CreateAudioCurve() => new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.1f, 0.14f),
            new Keyframe(0.3f, 0.48f),
            new Keyframe(0.6f, 0.88f),
            new Keyframe(1f, 1f));

        private static AnimationCurve CreateWindCurve() => new AnimationCurve(
            new Keyframe(0f, 0.02f),
            new Keyframe(0.15f, 0.08f),
            new Keyframe(0.4f, 0.48f),
            new Keyframe(1f, 1f));

        private static AnimationCurve CreateFogCurve() => new AnimationCurve(
            new Keyframe(0f, 0.01f),
            new Keyframe(0.15f, 0.05f),
            new Keyframe(0.5f, 0.42f),
            new Keyframe(1f, 1f));

        private static AnimationCurve CreatePrecipitationCurve() =>
            new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.2f, 0.015f),
                new Keyframe(0.5f, 0.22f),
                new Keyframe(1f, 1f));

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Transform opening = OpeningTransform;
            Gizmos.color = Color.Lerp(Color.red, Color.green, Openness01);
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = opening.localToWorldMatrix;
            Gizmos.DrawWireCube(
                Vector3.zero,
                new Vector3(openingSizeMeters.x, openingSizeMeters.y, 0.04f));
            Gizmos.DrawRay(Vector3.zero, Vector3.forward);
            Gizmos.matrix = previous;
        }
#endif
    }
}
