using System;
using UnityEngine;

namespace MSC.Traffic
{
    public enum BusTerminalAbandonmentPhase
    {
        Monitoring = 0,
        StuckConfirmation = 1,
        ShutdownDelay = 2,
        Abandoned = 3,
    }

    public readonly struct BusTerminalAbandonmentState
    {
        public BusTerminalAbandonmentState(
            BusTerminalAbandonmentPhase phase,
            bool monitorArmed,
            float stuckRealSeconds,
            float shutdownDelayRealSecondsRemaining)
        {
            Phase = phase;
            MonitorArmed = monitorArmed;
            StuckRealSeconds = Mathf.Max(0f, stuckRealSeconds);
            ShutdownDelayRealSecondsRemaining = Mathf.Max(
                0f,
                shutdownDelayRealSecondsRemaining);
        }

        public BusTerminalAbandonmentPhase Phase { get; }
        public bool MonitorArmed { get; }
        public float StuckRealSeconds { get; }
        public float ShutdownDelayRealSecondsRemaining { get; }
        public bool IsTerminal => Phase >=
                                  BusTerminalAbandonmentPhase.ShutdownDelay;
    }

    /// <summary>
    /// Pure donor-evidenced lifecycle rules shared by transport runtime and
    /// EditMode tests. They intentionally do not advance routes from game time.
    /// </summary>
    public static class TrafficTransportBehaviorRules
    {
        public const float BoatTwoActivationDistanceMeters = 700f;
        public const float BusStuckMonitorArmSpeedMetersPerSecond = 5f;
        public const float BusStuckSpeedMetersPerSecond = 2f;
        public const float BusStuckConfirmationRealSeconds = 25f;
        public const float BusDriverExitDelayRealSeconds = 4f;
        public const float BusServiceDoorOpenRealSeconds = 1f;
        public const float BusDriverCurseRepeatRealSeconds = 25f;
        public const float BusDriverCurseDistanceMeters = 15f;
        public const float BusMeaningfulForwardProgressMeters = 5f;
        public const int BusMaximumRecoveryAttemptsWithoutProgress = 3;

        public static BusTerminalAbandonmentState
            ResetBusAbandonmentForNewDeparture() =>
                new(
                    BusTerminalAbandonmentPhase.Monitoring,
                    monitorArmed: false,
                    stuckRealSeconds: 0f,
                    shutdownDelayRealSecondsRemaining: 0f);

        public static float ResolveBusServiceDoorOpen01(
            BusTerminalAbandonmentPhase phase,
            float shutdownDelayRealSecondsRemaining,
            float openDurationRealSeconds = BusServiceDoorOpenRealSeconds)
        {
            if (phase < BusTerminalAbandonmentPhase.ShutdownDelay)
            {
                return 0f;
            }

            if (phase >= BusTerminalAbandonmentPhase.Abandoned)
            {
                return 1f;
            }

            float elapsed = BusDriverExitDelayRealSeconds - Mathf.Clamp(
                shutdownDelayRealSecondsRemaining,
                0f,
                BusDriverExitDelayRealSeconds);
            return Mathf.Clamp01(
                elapsed / Mathf.Max(0.01f, openDurationRealSeconds));
        }

        /// <summary>
        /// Reimplements the donor Route/DrivingIssue terminal branch. The
        /// detector arms only after the bus has genuinely travelled, requires
        /// 25 uninterrupted real seconds at or below the donor speed threshold,
        /// then keeps the seated driver in place for four real seconds after
        /// shutdown/door-open before switching to the walking presentation.
        /// Authored service-stop dwell is explicitly excluded from the stuck
        /// timer; the donor stops are shorter than the timeout, but making that
        /// exclusion explicit prevents future timetable tuning from firing a
        /// false abandonment.
        /// </summary>
        public static BusTerminalAbandonmentState AdvanceBusAbandonment(
            BusTerminalAbandonmentState state,
            float physicalSpeedMetersPerSecond,
            bool intentionalServiceStop,
            float realDeltaSeconds,
            bool meaningfulForwardRouteProgress = false,
            bool recoveryMotionWithoutForwardProgress = false)
        {
            float speed = Mathf.Max(0f, physicalSpeedMetersPerSecond);
            float delta = Mathf.Max(0f, realDeltaSeconds);
            switch (state.Phase)
            {
                case BusTerminalAbandonmentPhase.Abandoned:
                    return new BusTerminalAbandonmentState(
                        BusTerminalAbandonmentPhase.Abandoned,
                        monitorArmed: true,
                        stuckRealSeconds:
                            BusStuckConfirmationRealSeconds,
                        shutdownDelayRealSecondsRemaining: 0f);

                case BusTerminalAbandonmentPhase.ShutdownDelay:
                {
                    float remaining = Mathf.Max(
                        0f,
                        state.ShutdownDelayRealSecondsRemaining - delta);
                    return remaining <= 0f
                        ? new BusTerminalAbandonmentState(
                            BusTerminalAbandonmentPhase.Abandoned,
                            monitorArmed: true,
                            stuckRealSeconds:
                                BusStuckConfirmationRealSeconds,
                            shutdownDelayRealSecondsRemaining: 0f)
                        : new BusTerminalAbandonmentState(
                            BusTerminalAbandonmentPhase.ShutdownDelay,
                            monitorArmed: true,
                            stuckRealSeconds:
                                BusStuckConfirmationRealSeconds,
                            shutdownDelayRealSecondsRemaining: remaining);
                }
            }

            bool armed = state.MonitorArmed ||
                         speed > BusStuckMonitorArmSpeedMetersPerSecond;
            if (!armed)
            {
                return new BusTerminalAbandonmentState(
                    BusTerminalAbandonmentPhase.Monitoring,
                    monitorArmed: false,
                    stuckRealSeconds: 0f,
                    shutdownDelayRealSecondsRemaining: 0f);
            }

            if (intentionalServiceStop || meaningfulForwardRouteProgress ||
                speed > BusStuckSpeedMetersPerSecond &&
                !recoveryMotionWithoutForwardProgress)
            {
                return new BusTerminalAbandonmentState(
                    BusTerminalAbandonmentPhase.Monitoring,
                    monitorArmed: true,
                    stuckRealSeconds: 0f,
                    shutdownDelayRealSecondsRemaining: 0f);
            }

            float stuckSeconds = Mathf.Min(
                BusStuckConfirmationRealSeconds,
                state.StuckRealSeconds + delta);
            if (stuckSeconds + 0.0001f <
                BusStuckConfirmationRealSeconds)
            {
                return new BusTerminalAbandonmentState(
                    BusTerminalAbandonmentPhase.StuckConfirmation,
                    monitorArmed: true,
                    stuckRealSeconds: stuckSeconds,
                    shutdownDelayRealSecondsRemaining: 0f);
            }

            return new BusTerminalAbandonmentState(
                BusTerminalAbandonmentPhase.ShutdownDelay,
                monitorArmed: true,
                stuckRealSeconds: BusStuckConfirmationRealSeconds,
                shutdownDelayRealSecondsRemaining:
                    BusDriverExitDelayRealSeconds);
        }

        /// <summary>
        /// Converts the persisted forward route cursor into metres. Including
        /// the completed-trip ordinal prevents a legitimate loop wrap from
        /// looking like reverse travel.
        /// </summary>
        public static float ResolveBusForwardProgressMeters(
            float anchorProgress01,
            int anchorCompletedTrips,
            float currentProgress01,
            int currentCompletedTrips,
            float routeLengthMeters)
        {
            float length = Mathf.Max(0f, routeLengthMeters);
            if (length <= 0f)
            {
                return 0f;
            }

            double routeDelta =
                Math.Max(0, currentCompletedTrips - anchorCompletedTrips) +
                Mathf.Clamp01(currentProgress01) -
                Mathf.Clamp01(anchorProgress01);
            return Mathf.Max(0f, (float)(routeDelta * length));
        }

        public static bool IsBusRecoveryExhausted(
            int recoveryCountAtLastForwardProgress,
            int currentRecoveryCount) =>
            Mathf.Max(0, currentRecoveryCount) -
            Mathf.Max(0, recoveryCountAtLastForwardProgress) >=
            BusMaximumRecoveryAttemptsWithoutProgress;

        public static bool IsTerminalBusPhysicalPoseAuthoritative(
            BusTerminalAbandonmentPhase phase) =>
            phase >= BusTerminalAbandonmentPhase.ShutdownDelay;

        public static bool IsBoatTwoEligible(
            long dayIndex,
            bool isDaylight,
            Vector3 playerPosition,
            Vector3 boatPosition)
        {
            // The authoritative calendar starts on Tuesday 1995-08-01, so
            // Sunday is day-index modulo 7 == 5.
            long weekday = ((dayIndex % 7L) + 7L) % 7L;
            if (weekday != 5L || !isDaylight)
            {
                return false;
            }

            float maximumDistanceSquared =
                BoatTwoActivationDistanceMeters *
                BoatTwoActivationDistanceMeters;
            return (playerPosition - boatPosition).sqrMagnitude <=
                   maximumDistanceSquared;
        }

        public static int ResolveBoatTwoStartPointIndex(
            long dayIndex,
            int activationOrdinal,
            int pointCount)
        {
            if (pointCount <= 0)
            {
                return 0;
            }

            unchecked
            {
                uint value = (uint)dayIndex * 0x9e3779b9u;
                value ^= (uint)Mathf.Max(0, activationOrdinal) * 0x85ebca6bu;
                value ^= 0x6d2b79f5u;
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return (int)(value % (uint)pointCount);
            }
        }
    }
}
