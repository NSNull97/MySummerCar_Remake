using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using UnityEngine;

namespace MSC.NPC
{
    public enum SuskiRescueStage
    {
        Passenger = 0,
        AwaitingPickup = 1,
        Transporting = 2,
        RestingAtParentsBed = 3,
        Rescued = 4,
        // The donor terminal incident first incapacitates Suski inside Jani's
        // car. AwaitingPickup begins only after an explicit player extraction;
        // keeping this additive value preserves every previously serialized
        // enum ordinal in traffic.state schema 1.
        CrashedInCar = 5,
    }

    [Serializable]
    public sealed class StoryTrafficDriverStateDto
    {
        public string characterDefinitionId = string.Empty;
        public string stableInstanceId = string.Empty;
        public int crashCount;
        public bool terminalCrash;
        public long lastCrashDayIndex = -1L;
        public double lastCrashSecondsOfDay;
        public float lastCrashSpeedMetersPerSecond;
        public Vector3 worldPosition;
        public Quaternion worldRotation = Quaternion.identity;
        // Additive schema-1 fields. Zero/false migrate older saves into the
        // not-yet-consumed social event and active route state.
        public bool teimoSocialStopConsumed;
        public float teimoSocialStopSecondsRemaining;
        public bool routeFullStopReached;

        public StoryTrafficDriverStateDto DeepClone() =>
            new StoryTrafficDriverStateDto
            {
                characterDefinitionId = characterDefinitionId,
                stableInstanceId = stableInstanceId,
                crashCount = crashCount,
                terminalCrash = terminalCrash,
                lastCrashDayIndex = lastCrashDayIndex,
                lastCrashSecondsOfDay = lastCrashSecondsOfDay,
                lastCrashSpeedMetersPerSecond =
                    lastCrashSpeedMetersPerSecond,
                worldPosition = worldPosition,
                worldRotation = worldRotation,
                teimoSocialStopConsumed = teimoSocialStopConsumed,
                teimoSocialStopSecondsRemaining =
                    teimoSocialStopSecondsRemaining,
                routeFullStopReached = routeFullStopReached,
            };
    }

    [Serializable]
    public sealed class SuskiRescueStateDto
    {
        public string characterDefinitionId =
            NpcWorldRuntime.SuskiDefinitionId;
        public string stableInstanceId = string.Empty;
        public SuskiRescueStage stage = SuskiRescueStage.Passenger;
        public Vector3 worldPosition;
        public Quaternion worldRotation = Quaternion.identity;

        public SuskiRescueStateDto DeepClone() =>
            new SuskiRescueStateDto
            {
                characterDefinitionId = characterDefinitionId,
                stableInstanceId = stableInstanceId,
                stage = stage,
                worldPosition = worldPosition,
                worldRotation = worldRotation,
            };
    }

    /// <summary>
    /// Additive state owned by the general traffic runtime. It lives in the
    /// already-established optional traffic.state envelope so version-13 saves
    /// and the accepted story-traffic contract remain loadable without a
    /// second competing traffic save domain.
    /// </summary>
    [Serializable]
    public sealed class AmbientTrafficActorStateDto
    {
        public string actorId = string.Empty;
        public string stableInstanceId = string.Empty;
        public string routeId = string.Empty;
        public float routeProgress01;
        public bool travelsForward = true;
        public bool active = true;
        public float desiredSpeedMetersPerSecond;
        public int completedCircuits;
        public int spawnAttemptCount;
        public float inactiveRetryGameSeconds;
        public bool hasPhysicalPose;
        public Vector3 worldPosition;
        public Quaternion worldRotation = Quaternion.identity;
        public float currentSpeedMetersPerSecond;
        public float cruiseSpeedMetersPerSecond;
        public float laneOffsetMeters;
        public int maneuverState;
        public float maneuverStateSeconds;
        public float driftSlipDegrees;
        public int recoveryCount;
        public bool hasSafePose;
        public Vector3 safePosition;
        public Quaternion safeRotation = Quaternion.identity;
        public bool townExcursionPending;
        public float townDecisionGameSecondsRemaining;
        public int townDecisionAttemptCount;
        public int completedTownExcursions;
        // 0 = undecided, 1 = town loop, 2 = gas-pump branch. Persisting the
        // choice lets an occupied pump hold survive save/load deterministically.
        public int townExcursionDestination;
        // Additive stable-cousin context. Zero-valued fields migrate older
        // saves to the ordinary dirt-road Fittan without duplicating Pena.
        // Version zero denotes a pre-Pena-lifecycle row whose old KUSKI pose
        // must not be applied to the ordinary car.
        public int cousinStateVersion;
        public int cousinContext;
        public int cousinRouteStage;
        public int cousinActivationOrdinal;
        public float cousinScheduleCheckRealSecondsRemaining;

        public AmbientTrafficActorStateDto DeepClone() =>
            new AmbientTrafficActorStateDto
            {
                actorId = actorId,
                stableInstanceId = stableInstanceId,
                routeId = routeId,
                routeProgress01 = routeProgress01,
                travelsForward = travelsForward,
                active = active,
                desiredSpeedMetersPerSecond = desiredSpeedMetersPerSecond,
                completedCircuits = completedCircuits,
                spawnAttemptCount = spawnAttemptCount,
                inactiveRetryGameSeconds = inactiveRetryGameSeconds,
                hasPhysicalPose = hasPhysicalPose,
                worldPosition = worldPosition,
                worldRotation = worldRotation,
                currentSpeedMetersPerSecond = currentSpeedMetersPerSecond,
                cruiseSpeedMetersPerSecond = cruiseSpeedMetersPerSecond,
                laneOffsetMeters = laneOffsetMeters,
                maneuverState = maneuverState,
                maneuverStateSeconds = maneuverStateSeconds,
                driftSlipDegrees = driftSlipDegrees,
                recoveryCount = recoveryCount,
                hasSafePose = hasSafePose,
                safePosition = safePosition,
                safeRotation = safeRotation,
                townExcursionPending = townExcursionPending,
                townDecisionGameSecondsRemaining =
                    townDecisionGameSecondsRemaining,
                townDecisionAttemptCount = townDecisionAttemptCount,
                completedTownExcursions = completedTownExcursions,
                townExcursionDestination = townExcursionDestination,
                cousinStateVersion = cousinStateVersion,
                cousinContext = cousinContext,
                cousinRouteStage = cousinRouteStage,
                cousinActivationOrdinal = cousinActivationOrdinal,
                cousinScheduleCheckRealSecondsRemaining =
                    cousinScheduleCheckRealSecondsRemaining,
            };
    }

    [Serializable]
    public sealed class TransportTrafficActorStateDto
    {
        public string transportId = string.Empty;
        public string stableInstanceId = string.Empty;
        public int kind;
        public string routeId = string.Empty;
        public float routeProgress01;
        public bool active;
        public bool usingSecondaryRoute;
        public int completedTrips;
        public int lastDepartureAbsoluteHour = int.MinValue;
        public float dwellGameSecondsRemaining;
        public int nextStopIndex;
        public bool hasPhysicalPose;
        public Vector3 worldPosition;
        public Quaternion worldRotation = Quaternion.identity;
        public float currentSpeedMetersPerSecond;
        public int busAbandonmentPhase;
        public bool busStallMonitorArmed;
        public float busStuckRealSeconds;
        public float busShutdownDelayRealSecondsRemaining;
        public bool busHasForwardProgressAnchor;
        public float busForwardProgressAnchor01;
        public int busForwardProgressAnchorCompletedTrips;
        public int busRecoveryCountAtForwardProgress;
        public bool busRecoveryEpisodeActive;
        public bool busRecoveryExhausted;
        public bool hasBusDriverPose;
        public Vector3 busDriverWorldPosition;
        public Quaternion busDriverWorldRotation = Quaternion.identity;
        public float busDriverCurseCooldownRealSeconds;

        public TransportTrafficActorStateDto DeepClone() =>
            new TransportTrafficActorStateDto
            {
                transportId = transportId,
                stableInstanceId = stableInstanceId,
                kind = kind,
                routeId = routeId,
                routeProgress01 = routeProgress01,
                active = active,
                usingSecondaryRoute = usingSecondaryRoute,
                completedTrips = completedTrips,
                lastDepartureAbsoluteHour = lastDepartureAbsoluteHour,
                dwellGameSecondsRemaining = dwellGameSecondsRemaining,
                nextStopIndex = nextStopIndex,
                hasPhysicalPose = hasPhysicalPose,
                worldPosition = worldPosition,
                worldRotation = worldRotation,
                currentSpeedMetersPerSecond = currentSpeedMetersPerSecond,
                busAbandonmentPhase = busAbandonmentPhase,
                busStallMonitorArmed = busStallMonitorArmed,
                busStuckRealSeconds = busStuckRealSeconds,
                busShutdownDelayRealSecondsRemaining =
                    busShutdownDelayRealSecondsRemaining,
                busHasForwardProgressAnchor = busHasForwardProgressAnchor,
                busForwardProgressAnchor01 = busForwardProgressAnchor01,
                busForwardProgressAnchorCompletedTrips =
                    busForwardProgressAnchorCompletedTrips,
                busRecoveryCountAtForwardProgress =
                    busRecoveryCountAtForwardProgress,
                busRecoveryEpisodeActive = busRecoveryEpisodeActive,
                busRecoveryExhausted = busRecoveryExhausted,
                hasBusDriverPose = hasBusDriverPose,
                busDriverWorldPosition = busDriverWorldPosition,
                busDriverWorldRotation = busDriverWorldRotation,
                busDriverCurseCooldownRealSeconds =
                    busDriverCurseCooldownRealSeconds,
            };
    }

    [Serializable]
    public sealed class TrafficEventActorStateDto
    {
        public string actorId = string.Empty;
        public string stableInstanceId = string.Empty;
        public int kind;
        public int phase;
        public string routeId = string.Empty;
        public float routeProgress01;
        public bool travelsForward = true;
        public bool resident;
        public float desiredSpeedMetersPerSecond;
        public float phaseRealSecondsRemaining;
        public int completedRuns;
        public long eventDayIndex = -1L;
        public bool hasPhysicalPose;
        public Vector3 worldPosition;
        public Quaternion worldRotation = Quaternion.identity;
        public float currentSpeedMetersPerSecond;

        public TrafficEventActorStateDto DeepClone() =>
            new TrafficEventActorStateDto
            {
                actorId = actorId,
                stableInstanceId = stableInstanceId,
                kind = kind,
                phase = phase,
                routeId = routeId,
                routeProgress01 = routeProgress01,
                travelsForward = travelsForward,
                resident = resident,
                desiredSpeedMetersPerSecond = desiredSpeedMetersPerSecond,
                phaseRealSecondsRemaining = phaseRealSecondsRemaining,
                completedRuns = completedRuns,
                eventDayIndex = eventDayIndex,
                hasPhysicalPose = hasPhysicalPose,
                worldPosition = worldPosition,
                worldRotation = worldRotation,
                currentSpeedMetersPerSecond = currentSpeedMetersPerSecond,
            };
    }

    [Serializable]
    public sealed class TrafficEventGroupStateDto
    {
        public string groupId = string.Empty;
        public bool scheduleActive;
        public bool resident;
        public long evaluatedDayIndex = -1L;
        public float realSecondsRemaining;
        public int sequenceOrdinal;
        public int selectedSiteIndex = -1;
        public bool dailyChanceEvaluated;
        public bool chaseRequested;

        public TrafficEventGroupStateDto DeepClone() =>
            new TrafficEventGroupStateDto
            {
                groupId = groupId,
                scheduleActive = scheduleActive,
                resident = resident,
                evaluatedDayIndex = evaluatedDayIndex,
                realSecondsRemaining = realSecondsRemaining,
                sequenceOrdinal = sequenceOrdinal,
                selectedSiteIndex = selectedSiteIndex,
                dailyChanceEvaluated = dailyChanceEvaluated,
                chaseRequested = chaseRequested,
            };
    }

    /// <summary>
    /// Dedicated story-traffic persistence domain. It deliberately complements
    /// the accepted NPC schedule DTO instead of changing that schema: physical
    /// wreck poses, collision outcomes and the Suski rescue flow are mutable
    /// incident state, not authored character scheduling.
    /// </summary>
    [Serializable]
    public sealed class StoryTrafficStateDto
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;
        public StoryTrafficDriverStateDto[] drivers =
            Array.Empty<StoryTrafficDriverStateDto>();
        public SuskiRescueStateDto suski = new SuskiRescueStateDto();
        // Optional additive 11B-T1 payload. Empty means an older save or a
        // session built before general ambient traffic was installed.
        public AmbientTrafficActorStateDto[] ambientActors =
            Array.Empty<AmbientTrafficActorStateDto>();
        // Optional additive 11B-T2 payload for the bus, train and two boats.
        public TransportTrafficActorStateDto[] transportActors =
            Array.Empty<TransportTrafficActorStateDto>();
        // Optional additive 13B/13C payload for official rally, drag and the
        // reusable police checkpoint/chase vehicles.
        public TrafficEventActorStateDto[] eventActors =
            Array.Empty<TrafficEventActorStateDto>();
        public TrafficEventGroupStateDto[] eventGroups =
            Array.Empty<TrafficEventGroupStateDto>();

        public StoryTrafficStateDto DeepClone()
        {
            StoryTrafficDriverStateDto[] source = drivers ??
                Array.Empty<StoryTrafficDriverStateDto>();
            var clonedDrivers =
                new StoryTrafficDriverStateDto[source.Length];
            for (int index = 0; index < source.Length; index++)
            {
                clonedDrivers[index] = source[index]?.DeepClone();
            }

            return new StoryTrafficStateDto
            {
                schemaVersion = schemaVersion,
                drivers = clonedDrivers,
                suski = suski?.DeepClone(),
                ambientActors = (ambientActors ??
                        Array.Empty<AmbientTrafficActorStateDto>())
                    .Select(value => value?.DeepClone())
                    .ToArray(),
                transportActors = (transportActors ??
                        Array.Empty<TransportTrafficActorStateDto>())
                    .Select(value => value?.DeepClone())
                    .ToArray(),
                eventActors = (eventActors ??
                        Array.Empty<TrafficEventActorStateDto>())
                    .Select(value => value?.DeepClone())
                    .ToArray(),
                eventGroups = (eventGroups ??
                        Array.Empty<TrafficEventGroupStateDto>())
                    .Select(value => value?.DeepClone())
                    .ToArray(),
            };
        }

        public bool TryValidate(out string failure)
        {
            if (schemaVersion != CurrentSchemaVersion)
            {
                failure =
                    $"Unsupported story-traffic schema {schemaVersion}.";
                return false;
            }

            StoryTrafficDriverStateDto[] source = drivers ??
                Array.Empty<StoryTrafficDriverStateDto>();
            var definitionIds = new HashSet<string>(StringComparer.Ordinal);
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < source.Length; index++)
            {
                StoryTrafficDriverStateDto driver = source[index];
                if (driver == null ||
                    !IsDriverDefinitionId(driver.characterDefinitionId) ||
                    !definitionIds.Add(driver.characterDefinitionId) ||
                    !StableEntityId.TryParse(
                        driver.stableInstanceId,
                        out _) ||
                    !stableIds.Add(driver.stableInstanceId) ||
                    driver.crashCount < 0 ||
                    driver.lastCrashDayIndex < -1L ||
                    !double.IsFinite(driver.lastCrashSecondsOfDay) ||
                    driver.lastCrashSecondsOfDay < 0d ||
                    driver.lastCrashSecondsOfDay >= 86400d ||
                    !float.IsFinite(
                        driver.lastCrashSpeedMetersPerSecond) ||
                    driver.lastCrashSpeedMetersPerSecond < 0f ||
                    !float.IsFinite(
                        driver.teimoSocialStopSecondsRemaining) ||
                    driver.teimoSocialStopSecondsRemaining < 0f ||
                    driver.teimoSocialStopSecondsRemaining > 8f ||
                    driver.teimoSocialStopSecondsRemaining > 0f &&
                    !driver.teimoSocialStopConsumed ||
                    !IsFinite(driver.worldPosition) ||
                    !IsFinite(driver.worldRotation))
                {
                    failure =
                        $"Story-traffic driver row {index} is invalid.";
                    return false;
                }
            }

            if (source.Length != 2 ||
                !definitionIds.Contains("character.jani") ||
                !definitionIds.Contains("character.petteri"))
            {
                failure =
                    "Story-traffic state must contain exactly Jani and Petteri.";
                return false;
            }

            if (suski == null ||
                !string.Equals(
                    suski.characterDefinitionId,
                    NpcWorldRuntime.SuskiDefinitionId,
                    StringComparison.Ordinal) ||
                !StableEntityId.TryParse(suski.stableInstanceId, out _) ||
                !Enum.IsDefined(typeof(SuskiRescueStage), suski.stage) ||
                !IsFinite(suski.worldPosition) ||
                !IsFinite(suski.worldRotation))
            {
                failure = "Suski rescue state is invalid.";
                return false;
            }

            AmbientTrafficActorStateDto[] ambient = ambientActors ??
                Array.Empty<AmbientTrafficActorStateDto>();
            var ambientActorIds = new HashSet<string>(StringComparer.Ordinal);
            var ambientStableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < ambient.Length; index++)
            {
                AmbientTrafficActorStateDto actor = ambient[index];
                if (actor == null ||
                    string.IsNullOrWhiteSpace(actor.actorId) ||
                    !actor.actorId.StartsWith(
                        "traffic.ambient.",
                        StringComparison.Ordinal) ||
                    !ambientActorIds.Add(actor.actorId) ||
                    !StableEntityId.TryParse(actor.stableInstanceId, out _) ||
                    !ambientStableIds.Add(actor.stableInstanceId) ||
                    string.IsNullOrWhiteSpace(actor.routeId) ||
                    !actor.routeId.StartsWith(
                        "route.traffic.",
                        StringComparison.Ordinal) ||
                    !float.IsFinite(actor.routeProgress01) ||
                    actor.routeProgress01 < 0f ||
                    actor.routeProgress01 > 1f ||
                    !float.IsFinite(actor.desiredSpeedMetersPerSecond) ||
                    actor.desiredSpeedMetersPerSecond <= 0f ||
                    actor.completedCircuits < 0 ||
                    actor.spawnAttemptCount < 0 ||
                    !float.IsFinite(actor.inactiveRetryGameSeconds) ||
                    actor.inactiveRetryGameSeconds < 0f ||
                    !float.IsFinite(actor.currentSpeedMetersPerSecond) ||
                    actor.currentSpeedMetersPerSecond < 0f ||
                    !float.IsFinite(actor.cruiseSpeedMetersPerSecond) ||
                    actor.cruiseSpeedMetersPerSecond < 0f ||
                    !float.IsFinite(actor.laneOffsetMeters) ||
                    !float.IsFinite(actor.maneuverStateSeconds) ||
                    actor.maneuverStateSeconds < 0f ||
                    !float.IsFinite(actor.driftSlipDegrees) ||
                    actor.recoveryCount < 0 ||
                    !IsFinite(actor.worldPosition) ||
                    !IsFinite(actor.worldRotation) ||
                    !IsFinite(actor.safePosition) ||
                    !IsFinite(actor.safeRotation))
                {
                    failure =
                        $"Ambient traffic actor row {index} is invalid.";
                    return false;
                }
            }

            TransportTrafficActorStateDto[] transports = transportActors ??
                Array.Empty<TransportTrafficActorStateDto>();
            var transportIds = new HashSet<string>(StringComparer.Ordinal);
            var transportStableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < transports.Length; index++)
            {
                TransportTrafficActorStateDto actor = transports[index];
                if (actor == null ||
                    string.IsNullOrWhiteSpace(actor.transportId) ||
                    !actor.transportId.StartsWith(
                        "traffic.transport.",
                        StringComparison.Ordinal) ||
                    !transportIds.Add(actor.transportId) ||
                    !StableEntityId.TryParse(actor.stableInstanceId, out _) ||
                    !transportStableIds.Add(actor.stableInstanceId) ||
                    actor.kind < 0 || actor.kind > 2 ||
                    string.IsNullOrWhiteSpace(actor.routeId) ||
                    !actor.routeId.StartsWith(
                        "route.traffic.",
                        StringComparison.Ordinal) ||
                    !float.IsFinite(actor.routeProgress01) ||
                    actor.routeProgress01 < 0f ||
                    actor.routeProgress01 > 1f ||
                    actor.completedTrips < 0 ||
                    !float.IsFinite(actor.dwellGameSecondsRemaining) ||
                    actor.dwellGameSecondsRemaining < 0f ||
                    actor.nextStopIndex < 0 ||
                    !IsFinite(actor.worldPosition) ||
                    !IsFinite(actor.worldRotation) ||
                    !float.IsFinite(actor.currentSpeedMetersPerSecond) ||
                    actor.currentSpeedMetersPerSecond < 0f ||
                    actor.busAbandonmentPhase < 0 ||
                    actor.busAbandonmentPhase > 3 ||
                    !float.IsFinite(actor.busStuckRealSeconds) ||
                    actor.busStuckRealSeconds < 0f ||
                    !float.IsFinite(
                     actor.busShutdownDelayRealSecondsRemaining) ||
                     actor.busShutdownDelayRealSecondsRemaining < 0f ||
                    !float.IsFinite(actor.busForwardProgressAnchor01) ||
                    actor.busForwardProgressAnchor01 < 0f ||
                    actor.busForwardProgressAnchor01 > 1f ||
                    actor.busForwardProgressAnchorCompletedTrips < 0 ||
                    actor.busRecoveryCountAtForwardProgress < 0 ||
                    !IsFinite(actor.busDriverWorldPosition) ||
                    !IsFinite(actor.busDriverWorldRotation) ||
                    !float.IsFinite(
                        actor.busDriverCurseCooldownRealSeconds) ||
                    actor.busDriverCurseCooldownRealSeconds < 0f ||
                    !IsValidTransportSpecificState(actor))
                {
                    failure =
                        $"Transport traffic actor row {index} is invalid.";
                    return false;
                }
            }

            TrafficEventActorStateDto[] events = eventActors ??
                Array.Empty<TrafficEventActorStateDto>();
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            var eventStableIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < events.Length; index++)
            {
                TrafficEventActorStateDto actor = events[index];
                if (actor == null ||
                    string.IsNullOrWhiteSpace(actor.actorId) ||
                    !actor.actorId.StartsWith(
                        "traffic.event.",
                        StringComparison.Ordinal) ||
                    !eventIds.Add(actor.actorId) ||
                    !StableEntityId.TryParse(actor.stableInstanceId, out _) ||
                    !eventStableIds.Add(actor.stableInstanceId) ||
                    actor.kind < 0 || actor.kind > 2 ||
                    actor.phase < 0 || actor.phase > 10 ||
                    string.IsNullOrWhiteSpace(actor.routeId) ||
                    !actor.routeId.StartsWith(
                        "route.traffic.",
                        StringComparison.Ordinal) ||
                    !float.IsFinite(actor.routeProgress01) ||
                    actor.routeProgress01 < 0f || actor.routeProgress01 > 1f ||
                    !float.IsFinite(actor.desiredSpeedMetersPerSecond) ||
                    actor.desiredSpeedMetersPerSecond < 0f ||
                    !float.IsFinite(actor.phaseRealSecondsRemaining) ||
                    actor.phaseRealSecondsRemaining < 0f ||
                    actor.completedRuns < 0 || actor.eventDayIndex < -1L ||
                    !IsFinite(actor.worldPosition) ||
                    !IsFinite(actor.worldRotation) ||
                    !float.IsFinite(actor.currentSpeedMetersPerSecond) ||
                    actor.currentSpeedMetersPerSecond < 0f)
                {
                    failure = $"Traffic event actor row {index} is invalid.";
                    return false;
                }
            }

            TrafficEventGroupStateDto[] groups = eventGroups ??
                Array.Empty<TrafficEventGroupStateDto>();
            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < groups.Length; index++)
            {
                TrafficEventGroupStateDto group = groups[index];
                if (group == null ||
                    string.IsNullOrWhiteSpace(group.groupId) ||
                    !group.groupId.StartsWith(
                        "traffic.event.group.",
                        StringComparison.Ordinal) ||
                    !groupIds.Add(group.groupId) ||
                    group.evaluatedDayIndex < -1L ||
                    !float.IsFinite(group.realSecondsRemaining) ||
                    group.realSecondsRemaining < 0f ||
                    group.sequenceOrdinal < 0 ||
                    group.selectedSiteIndex < -1 ||
                    group.selectedSiteIndex > 2)
                {
                    failure = $"Traffic event group row {index} is invalid.";
                    return false;
                }
            }

            failure = string.Empty;
            return true;
        }

        private static bool IsDriverDefinitionId(string value) =>
            string.Equals(value, "character.jani", StringComparison.Ordinal) ||
            string.Equals(
                value,
                "character.petteri",
                StringComparison.Ordinal);

        private static bool IsValidTransportSpecificState(
            TransportTrafficActorStateDto actor)
        {
            const int busKind = 0;
            const int monitoring = 0;
            const int stuckConfirmation = 1;
            const int shutdownDelay = 2;
            const int abandoned = 3;
            const float stuckConfirmationSeconds = 25f;
            const float driverExitDelaySeconds = 4f;
            const float tolerance = 0.001f;

            if (actor.kind != busKind)
            {
                return actor.busAbandonmentPhase == monitoring &&
                       !actor.busStallMonitorArmed &&
                       actor.busStuckRealSeconds <= tolerance &&
                       actor.busShutdownDelayRealSecondsRemaining <= tolerance &&
                       !actor.busHasForwardProgressAnchor &&
                       actor.busForwardProgressAnchor01 <= tolerance &&
                       actor.busForwardProgressAnchorCompletedTrips == 0 &&
                       actor.busRecoveryCountAtForwardProgress == 0 &&
                       !actor.busRecoveryEpisodeActive &&
                       !actor.busRecoveryExhausted &&
                       !actor.hasBusDriverPose &&
                       actor.busDriverWorldPosition.sqrMagnitude <= tolerance * tolerance &&
                       Quaternion.Angle(
                           actor.busDriverWorldRotation,
                           Quaternion.identity) <= tolerance &&
                       actor.busDriverCurseCooldownRealSeconds <= tolerance;
            }

            if (actor.hasBusDriverPose &&
                actor.busAbandonmentPhase != abandoned)
            {
                return false;
            }

            return actor.busAbandonmentPhase switch
            {
                monitoring => actor.busStuckRealSeconds <= tolerance &&
                              actor.busShutdownDelayRealSecondsRemaining <=
                              tolerance,
                stuckConfirmation => actor.busStallMonitorArmed &&
                                     actor.busStuckRealSeconds > 0f &&
                                     actor.busStuckRealSeconds <
                                     stuckConfirmationSeconds &&
                                     actor.busShutdownDelayRealSecondsRemaining <=
                                     tolerance,
                shutdownDelay => actor.busStallMonitorArmed &&
                                 actor.busStuckRealSeconds + tolerance >=
                                 stuckConfirmationSeconds &&
                                 actor.busShutdownDelayRealSecondsRemaining > 0f &&
                                 actor.busShutdownDelayRealSecondsRemaining <=
                                 driverExitDelaySeconds + tolerance,
                abandoned => actor.busStallMonitorArmed &&
                             actor.busStuckRealSeconds + tolerance >=
                             stuckConfirmationSeconds &&
                             actor.busShutdownDelayRealSecondsRemaining <=
                             tolerance,
                _ => false,
            };
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w) &&
            value.x * value.x + value.y * value.y +
            value.z * value.z + value.w * value.w > 0.0001f;
    }
}
