using System;
using MSC.Core.Identity;
using UnityEngine;

namespace MSC.Traffic
{
    public enum TrafficEventKind
    {
        OfficialRally = 0,
        DragRace = 1,
        PoliceCheckpoint = 2,
    }

    public enum TrafficEventActorPhase
    {
        Inactive = 0,
        Staged = 1,
        Crawl = 2,
        Burnout = 3,
        Reverse = 4,
        LineUp = 5,
        RevUp = 6,
        Running = 7,
        Checkpoint = 8,
        Chase = 9,
        Finished = 10,
    }

    /// <summary>
    /// Immutable, project-owned configuration for the donor's bounded event
    /// vehicles. Event actors deliberately do not share ambient spawn rolls or
    /// root switching: their lifecycle belongs to the rally, drag and police
    /// event authorities.
    /// </summary>
    [Serializable]
    public sealed class TrafficEventActorDefinition
    {
        [SerializeField] private string actorId = string.Empty;
        [SerializeField] private string stableInstanceId = string.Empty;
        [SerializeField] private string vehicleFeatureId = string.Empty;
        [SerializeField] private string presentationId = string.Empty;
        [SerializeField] private TrafficEventKind kind;
        [SerializeField] private string routeId = string.Empty;
        [SerializeField, Min(0)] private int primaryStartPointIndex;
        [SerializeField, Min(0)] private int primaryEndPointIndex;
        [SerializeField, Min(0)] private int alternateStartPointIndex;
        [SerializeField, Min(0)] private int alternateEndPointIndex;
        [SerializeField] private bool travelsForward = true;
        [SerializeField] private Vector3 stagingWorldPosition;
        [SerializeField] private Quaternion stagingWorldRotation =
            Quaternion.identity;
        [SerializeField] private float laneOffsetMeters;
        [SerializeField, Min(0.1f)] private float minimumSpeedMetersPerSecond;
        [SerializeField, Min(0.1f)] private float maximumSpeedMetersPerSecond;
        [SerializeField, Min(0.1f)] private float chaseMinimumSpeedMetersPerSecond;
        [SerializeField, Min(0.1f)] private float chaseMaximumSpeedMetersPerSecond;
        [SerializeField, Min(10f)] private float materializeDistanceMeters;
        [SerializeField, Min(10f)] private float dematerializeDistanceMeters;
        [SerializeField, Min(0.1f)] private float requiredDistanceMeters;
        [SerializeField, Min(0.1f)] private float emergencyDistanceMeters;
        [SerializeField, Min(0.1f)] private float brakeDistanceMeters;
        [SerializeField, Min(100f)] private float massKilograms;
        [SerializeField] private int deterministicSeed;

        public TrafficEventActorDefinition(
            string configuredActorId,
            string configuredStableInstanceId,
            string configuredVehicleFeatureId,
            string configuredPresentationId,
            TrafficEventKind configuredKind,
            string configuredRouteId,
            int configuredPrimaryStartPointIndex,
            int configuredPrimaryEndPointIndex,
            int configuredAlternateStartPointIndex,
            int configuredAlternateEndPointIndex,
            bool configuredTravelsForward,
            Vector3 configuredStagingWorldPosition,
            Quaternion configuredStagingWorldRotation,
            float configuredLaneOffsetMeters,
            float configuredMinimumSpeedMetersPerSecond,
            float configuredMaximumSpeedMetersPerSecond,
            float configuredChaseMinimumSpeedMetersPerSecond,
            float configuredChaseMaximumSpeedMetersPerSecond,
            float configuredMaterializeDistanceMeters,
            float configuredDematerializeDistanceMeters,
            float configuredRequiredDistanceMeters,
            float configuredEmergencyDistanceMeters,
            float configuredBrakeDistanceMeters,
            float configuredMassKilograms,
            int configuredDeterministicSeed)
        {
            actorId = configuredActorId?.Trim() ?? string.Empty;
            stableInstanceId = configuredStableInstanceId?.Trim() ??
                               string.Empty;
            vehicleFeatureId = configuredVehicleFeatureId?.Trim() ??
                               string.Empty;
            presentationId = configuredPresentationId?.Trim() ?? string.Empty;
            kind = configuredKind;
            routeId = configuredRouteId?.Trim() ?? string.Empty;
            primaryStartPointIndex = configuredPrimaryStartPointIndex;
            primaryEndPointIndex = configuredPrimaryEndPointIndex;
            alternateStartPointIndex = configuredAlternateStartPointIndex;
            alternateEndPointIndex = configuredAlternateEndPointIndex;
            travelsForward = configuredTravelsForward;
            stagingWorldPosition = configuredStagingWorldPosition;
            stagingWorldRotation = configuredStagingWorldRotation;
            laneOffsetMeters = configuredLaneOffsetMeters;
            minimumSpeedMetersPerSecond =
                configuredMinimumSpeedMetersPerSecond;
            maximumSpeedMetersPerSecond =
                configuredMaximumSpeedMetersPerSecond;
            chaseMinimumSpeedMetersPerSecond =
                configuredChaseMinimumSpeedMetersPerSecond;
            chaseMaximumSpeedMetersPerSecond =
                configuredChaseMaximumSpeedMetersPerSecond;
            materializeDistanceMeters = configuredMaterializeDistanceMeters;
            dematerializeDistanceMeters = configuredDematerializeDistanceMeters;
            requiredDistanceMeters = configuredRequiredDistanceMeters;
            emergencyDistanceMeters = configuredEmergencyDistanceMeters;
            brakeDistanceMeters = configuredBrakeDistanceMeters;
            massKilograms = configuredMassKilograms;
            deterministicSeed = configuredDeterministicSeed;
        }

        public string ActorId => actorId;
        public string StableInstanceId => stableInstanceId;
        public string VehicleFeatureId => vehicleFeatureId;
        public string PresentationId => presentationId;
        public TrafficEventKind Kind => kind;
        public string RouteId => routeId;
        public int PrimaryStartPointIndex => primaryStartPointIndex;
        public int PrimaryEndPointIndex => primaryEndPointIndex;
        public int AlternateStartPointIndex => alternateStartPointIndex;
        public int AlternateEndPointIndex => alternateEndPointIndex;
        public bool TravelsForward => travelsForward;
        public Vector3 StagingWorldPosition => stagingWorldPosition;
        public Quaternion StagingWorldRotation => stagingWorldRotation;
        public float LaneOffsetMeters => laneOffsetMeters;
        public float MinimumSpeedMetersPerSecond =>
            minimumSpeedMetersPerSecond;
        public float MaximumSpeedMetersPerSecond =>
            maximumSpeedMetersPerSecond;
        public float ChaseMinimumSpeedMetersPerSecond =>
            chaseMinimumSpeedMetersPerSecond;
        public float ChaseMaximumSpeedMetersPerSecond =>
            chaseMaximumSpeedMetersPerSecond;
        public float MaterializeDistanceMeters => materializeDistanceMeters;
        public float DematerializeDistanceMeters => dematerializeDistanceMeters;
        public float RequiredDistanceMeters => requiredDistanceMeters;
        public float EmergencyDistanceMeters => emergencyDistanceMeters;
        public float BrakeDistanceMeters => brakeDistanceMeters;
        public float MassKilograms => massKilograms;
        public int DeterministicSeed => deterministicSeed;

        public bool TryValidate(out string failure)
        {
            if (string.IsNullOrWhiteSpace(actorId) ||
                !actorId.StartsWith("traffic.event.", StringComparison.Ordinal) ||
                !StableEntityId.TryParse(stableInstanceId, out _) ||
                string.IsNullOrWhiteSpace(vehicleFeatureId) ||
                !vehicleFeatureId.StartsWith("P1.VEHICLE.", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(presentationId) ||
                !presentationId.StartsWith(
                    "presentation.traffic.event.",
                    StringComparison.Ordinal) ||
                !Enum.IsDefined(typeof(TrafficEventKind), kind) ||
                string.IsNullOrWhiteSpace(routeId) ||
                !routeId.StartsWith("route.traffic.", StringComparison.Ordinal) ||
                primaryStartPointIndex < 0 || primaryEndPointIndex < 0 ||
                alternateStartPointIndex < 0 || alternateEndPointIndex < 0 ||
                !IsFinite(stagingWorldPosition) ||
                !IsFinite(stagingWorldRotation) ||
                !float.IsFinite(laneOffsetMeters) ||
                Mathf.Abs(laneOffsetMeters) > 4f ||
                !IsPositive(minimumSpeedMetersPerSecond) ||
                !IsPositive(maximumSpeedMetersPerSecond) ||
                maximumSpeedMetersPerSecond < minimumSpeedMetersPerSecond ||
                !IsPositive(chaseMinimumSpeedMetersPerSecond) ||
                !IsPositive(chaseMaximumSpeedMetersPerSecond) ||
                chaseMaximumSpeedMetersPerSecond <
                chaseMinimumSpeedMetersPerSecond ||
                !IsPositive(materializeDistanceMeters) ||
                !IsPositive(dematerializeDistanceMeters) ||
                dematerializeDistanceMeters < materializeDistanceMeters ||
                !IsPositive(requiredDistanceMeters) ||
                !IsPositive(emergencyDistanceMeters) ||
                emergencyDistanceMeters < requiredDistanceMeters ||
                !IsPositive(brakeDistanceMeters) ||
                !float.IsFinite(massKilograms) || massKilograms < 100f)
            {
                failure = $"Traffic event actor '{actorId}' is invalid.";
                return false;
            }

            failure = string.Empty;
            return true;
        }

        private static bool IsPositive(float value) =>
            float.IsFinite(value) && value > 0f;

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) &&
            float.IsFinite(value.z) && float.IsFinite(value.w) &&
            value.x * value.x + value.y * value.y +
            value.z * value.z + value.w * value.w > 0.0001f;
    }

    /// <summary>
    /// Pure calendar and deterministic-roll rules captured from the locked
    /// donor audit. Continuous project time maps the donor's two-hour values to
    /// half-open windows: 10..18 means 10:00 through 19:59, for example.
    /// </summary>
    public static class TrafficEventBehaviorRules
    {
        public const float RallyDispatchIntervalRealSeconds = 80f;
        public const float PoliceInitialEvaluationDelayRealSeconds = 2f;
        public const float DragLaunchDelayMinimumRealSeconds = 2.8f;
        public const float DragLaunchDelayMaximumRealSeconds = 3.8f;

        public static bool IsRallyOpen(DayOfWeek day, int hour) =>
            (day == DayOfWeek.Saturday || day == DayOfWeek.Sunday) &&
            hour >= 10 && hour < 20;

        public static bool IsDragOpen(DayOfWeek day, int hour) =>
            day == DayOfWeek.Friday && hour >= 6 && hour < 22;

        public static float PoliceActivationProbability(DayOfWeek day) =>
            day == DayOfWeek.Friday || day == DayOfWeek.Saturday ||
            day == DayOfWeek.Sunday
                ? 0.5f
                : 0.1f;

        public static float Deterministic01(int seed, int ordinal, long dayIndex)
        {
            unchecked
            {
                uint value = (uint)seed;
                value ^= (uint)ordinal * 0x9e3779b9u;
                value ^= (uint)dayIndex * 0x85ebca6bu;
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 16777215f;
            }
        }

        public static float ResolveDragLaunchDelayRealSeconds(
            int seed,
            int cycle,
            long dayIndex) =>
            Mathf.Lerp(
                DragLaunchDelayMinimumRealSeconds,
                DragLaunchDelayMaximumRealSeconds,
                Deterministic01(seed, cycle, dayIndex));
    }
}
