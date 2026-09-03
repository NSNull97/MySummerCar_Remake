using System;
using System.Linq;
using MSC.Traffic;
using NUnit.Framework;
using UnityEditor;

namespace MSC.Tests.EditMode.Traffic
{
    public sealed class TrafficEventBehaviorRulesTests
    {
        private const string CatalogPath =
            "Assets/Game/Traffic/Content/Phase1/" +
            "TrafficRoadNetworkCatalog.asset";

        [Test]
        public void RallySchedule_UsesLockedWeekendWindow()
        {
            Assert.That(TrafficEventBehaviorRules.IsRallyOpen(
                DayOfWeek.Saturday, 10), Is.True);
            Assert.That(TrafficEventBehaviorRules.IsRallyOpen(
                DayOfWeek.Sunday, 19), Is.True);
            Assert.That(TrafficEventBehaviorRules.IsRallyOpen(
                DayOfWeek.Saturday, 9), Is.False);
            Assert.That(TrafficEventBehaviorRules.IsRallyOpen(
                DayOfWeek.Sunday, 20), Is.False);
            Assert.That(TrafficEventBehaviorRules.IsRallyOpen(
                DayOfWeek.Friday, 12), Is.False);
            Assert.That(TrafficEventBehaviorRules
                .RallyDispatchIntervalRealSeconds, Is.EqualTo(80f));
        }

        [Test]
        public void DragScheduleAndLaunchDelay_UseLockedFridayRules()
        {
            Assert.That(TrafficEventBehaviorRules.IsDragOpen(
                DayOfWeek.Friday, 6), Is.True);
            Assert.That(TrafficEventBehaviorRules.IsDragOpen(
                DayOfWeek.Friday, 21), Is.True);
            Assert.That(TrafficEventBehaviorRules.IsDragOpen(
                DayOfWeek.Friday, 5), Is.False);
            Assert.That(TrafficEventBehaviorRules.IsDragOpen(
                DayOfWeek.Friday, 22), Is.False);

            float first = TrafficEventBehaviorRules
                .ResolveDragLaunchDelayRealSeconds(42666, 3, 100L);
            float repeated = TrafficEventBehaviorRules
                .ResolveDragLaunchDelayRealSeconds(42666, 3, 100L);
            Assert.That(first, Is.EqualTo(repeated));
            Assert.That(first, Is.InRange(2.8f, 3.8f));
        }

        [Test]
        public void PoliceSchedule_UsesLockedWeekdayAndWeekendChance()
        {
            Assert.That(TrafficEventBehaviorRules.PoliceActivationProbability(
                DayOfWeek.Monday), Is.EqualTo(0.1f));
            Assert.That(TrafficEventBehaviorRules.PoliceActivationProbability(
                DayOfWeek.Thursday), Is.EqualTo(0.1f));
            Assert.That(TrafficEventBehaviorRules.PoliceActivationProbability(
                DayOfWeek.Friday), Is.EqualTo(0.5f));
            Assert.That(TrafficEventBehaviorRules.PoliceActivationProbability(
                DayOfWeek.Sunday), Is.EqualTo(0.5f));
            Assert.That(TrafficEventBehaviorRules
                .PoliceInitialEvaluationDelayRealSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void GeneratedCatalog_ContainsExactlyLockedEventFleet()
        {
            TrafficRoadNetworkCatalog catalog = AssetDatabase.LoadAssetAtPath<
                TrafficRoadNetworkCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryValidate(out string failure), Is.True,
                failure);
            Assert.That(catalog.EventActors.Count, Is.EqualTo(7));
            Assert.That(catalog.EventActors.Count(value =>
                    value.Kind == TrafficEventKind.OfficialRally),
                Is.EqualTo(3));
            Assert.That(catalog.EventActors.Count(value =>
                    value.Kind == TrafficEventKind.DragRace),
                Is.EqualTo(2));
            Assert.That(catalog.EventActors.Count(value =>
                    value.Kind == TrafficEventKind.PoliceCheckpoint),
                Is.EqualTo(2));

            TrafficEventActorDefinition rally = catalog.EventActors.Single(
                value => value.ActorId == "traffic.event.rally.car-1");
            Assert.That(rally.VehicleFeatureId, Is.EqualTo("P1.VEHICLE.107"));
            Assert.That(rally.RouteId,
                Is.EqualTo("route.traffic.dirt-road"));
            Assert.That(rally.PrimaryStartPointIndex, Is.EqualTo(2168));
            Assert.That(rally.PrimaryEndPointIndex, Is.EqualTo(3640));
            Assert.That(rally.AlternateStartPointIndex, Is.EqualTo(56));
            Assert.That(rally.AlternateEndPointIndex, Is.EqualTo(1875));
            Assert.That(rally.MassKilograms, Is.EqualTo(875f));

            TrafficEventActorDefinition drag = catalog.EventActors.Single(
                value => value.ActorId == "traffic.event.drag.car-1");
            Assert.That(drag.VehicleFeatureId, Is.EqualTo("P1.VEHICLE.110"));
            Assert.That(drag.RouteId,
                Is.EqualTo("route.traffic.drag-race"));
            Assert.That(drag.PrimaryStartPointIndex, Is.EqualTo(0));
            Assert.That(drag.PrimaryEndPointIndex, Is.EqualTo(165));
            Assert.That(drag.LaneOffsetMeters, Is.EqualTo(-3f));
            Assert.That(drag.MassKilograms, Is.EqualTo(1150f));

            TrafficEventActorDefinition police = catalog.EventActors.Single(
                value => value.ActorId == "traffic.event.police.car-1");
            Assert.That(police.VehicleFeatureId, Is.EqualTo("P1.VEHICLE.028"));
            Assert.That(police.RouteId,
                Is.EqualTo("route.traffic.highway"));
            Assert.That(police.PrimaryStartPointIndex, Is.EqualTo(422));
            Assert.That(police.TravelsForward, Is.False);
            Assert.That(police.MassKilograms, Is.EqualTo(1330f));
            Assert.That(police.ChaseMinimumSpeedMetersPerSecond,
                Is.EqualTo(130f / 3.6f).Within(0.001f));
            Assert.That(police.ChaseMaximumSpeedMetersPerSecond,
                Is.EqualTo(170f / 3.6f).Within(0.001f));
        }
    }
}
