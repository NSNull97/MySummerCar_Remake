using MSC.Traffic;
using NUnit.Framework;

namespace MSC.Tests.EditMode.Traffic
{
    public sealed class TrafficCousinBehaviorRulesTests
    {
        [TestCase(3L, 18d * 3600d, false)] // Friday
        [TestCase(4L, 17.99d * 3600d, false)]
        [TestCase(4L, 18d * 3600d, true)]
        [TestCase(4L, 23.99d * 3600d, true)]
        [TestCase(5L, 2d * 3600d, true)]
        [TestCase(5L, 3.99d * 3600d, true)]
        [TestCase(5L, 4d * 3600d, false)]
        [TestCase(6L, 2d * 3600d, false)]
        public void SaturdayKuskiWindow_MatchesLockedOvernightSchedule(
            long dayIndex,
            double secondsOfDay,
            bool expected)
        {
            Assert.That(
                TrafficCousinBehaviorRules.IsSaturdayKuskiWindow(
                    dayIndex,
                    secondsOfDay),
                Is.EqualTo(expected));
        }

        [Test]
        public void SaturdayKuskiRouteProgram_RepeatsAfterInitialDeparture()
        {
            CousinRouteStage stage = CousinRouteStage.DancehallDeparture;
            AssertLeg(stage, "route.traffic.dancehall", 268, 2, false, 0f);

            stage = TrafficCousinBehaviorRules.ResolveNextStage(stage);
            AssertLeg(stage, "route.traffic.road-race", 0, 113, true, 2f);
            stage = TrafficCousinBehaviorRules.ResolveNextStage(stage);
            AssertLeg(stage, "route.traffic.dirt-road", 0, 1894, true, 0f);
            stage = TrafficCousinBehaviorRules.ResolveNextStage(stage);
            AssertLeg(stage, "route.traffic.track-field", 28, 290, true, 2f);
            stage = TrafficCousinBehaviorRules.ResolveNextStage(stage);
            AssertLeg(stage, "route.traffic.road-race", 3, 113, true, 2f);
            Assert.That(TrafficCousinBehaviorRules.ResolveNextStage(stage),
                Is.EqualTo(CousinRouteStage.DirtRoad));
        }

        [Test]
        public void OrdinaryFittan_HasExactlyElevenPersistentSpawnAnchors()
        {
            Assert.That(TrafficCousinBehaviorRules.OrdinarySpawnAnchors.Length,
                Is.EqualTo(11));
            int first = TrafficCousinBehaviorRules
                .ResolveOrdinarySpawnAnchorIndex(58170, 2, 3L);
            int repeated = TrafficCousinBehaviorRules
                .ResolveOrdinarySpawnAnchorIndex(58170, 2, 3L);
            Assert.That(first, Is.EqualTo(repeated));
            Assert.That(first, Is.InRange(0, 10));
        }

        private static void AssertLeg(
            CousinRouteStage stage,
            string route,
            int start,
            int end,
            bool forward,
            float lane)
        {
            CousinRouteLeg leg = TrafficCousinBehaviorRules.ResolveLeg(stage);
            Assert.That(leg.RouteId, Is.EqualTo(route));
            Assert.That(leg.StartPointIndex, Is.EqualTo(start));
            Assert.That(leg.EndPointIndex, Is.EqualTo(end));
            Assert.That(leg.TravelsForward, Is.EqualTo(forward));
            Assert.That(leg.LaneOffsetMeters, Is.EqualTo(lane));
        }
    }
}
