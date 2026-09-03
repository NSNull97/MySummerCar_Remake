using System;
using UnityEngine;

namespace MSC.Traffic
{
    public enum CousinTrafficContext
    {
        OrdinaryFittan = 0,
        SaturdayKuski = 1,
    }

    public enum CousinRouteStage
    {
        DancehallDeparture = 0,
        FirstRoadRace = 1,
        DirtRoad = 2,
        TrackField = 3,
        RepeatingRoadRace = 4,
    }

    public readonly struct CousinRouteLeg
    {
        public CousinRouteLeg(
            string routeId,
            int startPointIndex,
            int endPointIndex,
            bool travelsForward,
            float laneOffsetMeters)
        {
            RouteId = routeId ?? string.Empty;
            StartPointIndex = startPointIndex;
            EndPointIndex = endPointIndex;
            TravelsForward = travelsForward;
            LaneOffsetMeters = laneOffsetMeters;
        }

        public string RouteId { get; }
        public int StartPointIndex { get; }
        public int EndPointIndex { get; }
        public bool TravelsForward { get; }
        public float LaneOffsetMeters { get; }
    }

    /// <summary>
    /// Locked donor schedule and route program for the two mutually exclusive
    /// presentations of Pena/cousin. No game-clock delta advances a vehicle.
    /// </summary>
    public static class TrafficCousinBehaviorRules
    {
        public const string ActorId = "traffic.ambient.dirt-road.pena";
        public const string OrdinaryPresentationId =
            "presentation.traffic.pena-fittan";
        public const string SaturdayPresentationId =
            "presentation.traffic.kuski";
        public const float SchedulePollRealSeconds = 5f;
        public const float OrdinaryRetirementDistanceMeters = 500f;
        public const float SaturdayActivationDistanceMeters = 200f;
        public const float SaturdayRetirementDistanceMeters = 500f;

        // Donor FittanSpawns plus the locked source-to-project translation
        // (169.98, 1.611, -1040.625). These are configuration evidence, not
        // hierarchy lookups.
        public static readonly Vector3[] OrdinarySpawnAnchors =
        {
            new(-666.77f, 3.921f, -1416.775f),
            new(-110.71f, 3.621f, -1851.675f),
            new(605.70f, 3.381f, -2351.675f),
            new(1256.88f, 5.641f, -1972.875f),
            new(2019.29f, 6.511f, -1975.775f),
            new(2120.65f, 0.121f, -1021.565f),
            new(1707.01f, 6.821f, -304.925f),
            new(796.58f, 0.761f, 244.575f),
            new(-335.21f, 3.811f, 209.145f),
            new(-1262.60f, 4.971f, 179.095f),
            new(-1284.99f, 7.091f, -1668.625f),
        };

        public static bool IsSaturdayKuskiWindow(
            long dayIndex,
            double secondsOfDay)
        {
            long weekday = ((dayIndex % 7L) + 7L) % 7L;
            double seconds = Math.Clamp(secondsOfDay, 0d, 86400d);
            // The authoritative game-time configuration starts on Tuesday,
            // 1995-08-01. Saturday is therefore day-index modulo 7 == 4, and
            // the donor setup continues through the serialized 24/02 hours
            // into early Sunday. The old Wednesday assumption activated the
            // EDM KUSKI car on Friday in place of Pena's ordinary FITTAN.
            return weekday == 4L && seconds >= 18d * 3600d ||
                   weekday == 5L && seconds < 4d * 3600d;
        }

        public static CousinRouteLeg ResolveLeg(CousinRouteStage stage) =>
            stage switch
            {
                CousinRouteStage.DancehallDeparture => new CousinRouteLeg(
                    "route.traffic.dancehall", 268, 2, false, 0f),
                CousinRouteStage.FirstRoadRace => new CousinRouteLeg(
                    "route.traffic.road-race", 0, 113, true, 2f),
                CousinRouteStage.DirtRoad => new CousinRouteLeg(
                    "route.traffic.dirt-road", 0, 1894, true, 0f),
                CousinRouteStage.TrackField => new CousinRouteLeg(
                    "route.traffic.track-field", 28, 290, true, 2f),
                CousinRouteStage.RepeatingRoadRace => new CousinRouteLeg(
                    "route.traffic.road-race", 3, 113, true, 2f),
                _ => throw new ArgumentOutOfRangeException(nameof(stage)),
            };

        public static CousinRouteStage ResolveNextStage(
            CousinRouteStage stage) =>
            stage switch
            {
                CousinRouteStage.DancehallDeparture =>
                    CousinRouteStage.FirstRoadRace,
                CousinRouteStage.FirstRoadRace => CousinRouteStage.DirtRoad,
                CousinRouteStage.DirtRoad => CousinRouteStage.TrackField,
                CousinRouteStage.TrackField =>
                    CousinRouteStage.RepeatingRoadRace,
                CousinRouteStage.RepeatingRoadRace => CousinRouteStage.DirtRoad,
                _ => throw new ArgumentOutOfRangeException(nameof(stage)),
            };

        public static int ResolveOrdinarySpawnAnchorIndex(
            int deterministicSeed,
            int activationOrdinal,
            long dayIndex)
        {
            unchecked
            {
                uint value = (uint)deterministicSeed;
                value ^= (uint)Mathf.Max(0, activationOrdinal) * 0x9e3779b9u;
                value ^= (uint)dayIndex * 0x85ebca6bu;
                value ^= value >> 16;
                value *= 0x7feb352du;
                value ^= value >> 15;
                value *= 0x846ca68bu;
                value ^= value >> 16;
                return (int)(value % (uint)OrdinarySpawnAnchors.Length);
            }
        }
    }
}
