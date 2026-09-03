using MSC.Traffic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MSC.Tests.EditMode.Traffic
{
    public sealed class TownExcursionRouteControlEditModeTests
    {
        private const string CatalogPath =
            "Assets/Game/Traffic/Content/Phase1/" +
            "TrafficRoadNetworkCatalog.asset";

        [Test]
        public void ImportedTownBranches_JoinNearestContinuationInsteadOfRouteOrigin()
        {
            TrafficRoadNetworkCatalog catalog =
                AssetDatabase.LoadAssetAtPath<TrafficRoadNetworkCatalog>(
                    CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.TryGetRoute(
                "route.traffic.highway", out TrafficRouteDefinition highway),
                Is.True);
            Assert.That(catalog.TryGetRoute(
                "route.traffic.mod-town-entry",
                out TrafficRouteDefinition townEntry), Is.True);
            Assert.That(catalog.TryGetRoute(
                "route.traffic.mod-town-loop",
                out TrafficRouteDefinition townLoop), Is.True);
            Assert.That(catalog.TryGetRoute(
                "route.traffic.mod-gas-pump",
                out TrafficRouteDefinition gasPump), Is.True);

            var townLoopGeometry = new TrafficRouteGeometry(townLoop);
            var gasPumpGeometry = new TrafficRouteGeometry(gasPump);
            var highwayGeometry = new TrafficRouteGeometry(highway);
            Vector3 entryEnd = townEntry.WorldPoints[
                townEntry.WorldPoints.Count - 1];

            AssertNearestJoin(
                townLoopGeometry,
                entryEnd,
                forward: true,
                "The town-loop branch must continue from the imported entry " +
                "endpoint instead of steering back to point zero.");
            AssertNearestJoin(
                gasPumpGeometry,
                entryEnd,
                forward: true,
                "The gas-pump branch must continue from the imported entry " +
                "endpoint instead of steering back to point zero.");

            AssertNearestJoin(
                highwayGeometry,
                townLoop.WorldPoints[townLoop.WorldPoints.Count - 1],
                forward: false,
                "The short town loop must rejoin the reverse highway branch " +
                "at its physical endpoint.");
            AssertNearestJoin(
                highwayGeometry,
                gasPump.WorldPoints[gasPump.WorldPoints.Count - 1],
                forward: false,
                "The pump visit must rejoin the reverse highway branch at " +
                "its physical endpoint.");

            float loopJoinProgress =
                TrafficWorldRuntime.ResolveTownRouteJoinProgress(
                    townLoopGeometry,
                    entryEnd,
                    forward: true);
            float pumpJoinProgress =
                TrafficWorldRuntime.ResolveTownRouteJoinProgress(
                    gasPumpGeometry,
                    entryEnd,
                    forward: true);
            Assert.That(loopJoinProgress, Is.GreaterThan(0.001f));
            Assert.That(pumpJoinProgress, Is.GreaterThan(0.001f));
            Assert.That(Vector3.Distance(
                    townLoopGeometry.Resolve(0f, true).Position,
                    entryEnd),
                Is.GreaterThan(8f),
                "This regression must retain evidence that route point zero " +
                "is the wrong continuation target.");
        }

        [Test]
        public void TownBranchController_UsesShortPreviewAndHystereticRejoin()
        {
            Assert.That(
                TrafficWorldRuntime.ResolveTownExcursionLookAheadMeters(
                    "route.traffic.mod-town-entry",
                    speedMetersPerSecond: 55f / 3.6f,
                    routeRejoinActive: false),
                Is.EqualTo(10f).Within(0.001f));
            Assert.That(
                TrafficWorldRuntime.ResolveTownExcursionLookAheadMeters(
                    "route.traffic.mod-town-loop",
                    speedMetersPerSecond: 55f / 3.6f,
                    routeRejoinActive: false),
                Is.EqualTo(9f).Within(0.001f));
            Assert.That(
                TrafficWorldRuntime.ResolveTownExcursionLookAheadMeters(
                    "route.traffic.mod-gas-pump",
                    speedMetersPerSecond: 55f / 3.6f,
                    routeRejoinActive: false),
                Is.EqualTo(6.5f).Within(0.001f));

            Assert.That(TrafficWorldRuntime.ResolveTownRouteRejoinActive(
                currentlyActive: false,
                routeSeparationMeters: 4.51f,
                headingDot: 1f), Is.True);
            Assert.That(TrafficWorldRuntime.ResolveTownRouteRejoinActive(
                currentlyActive: true,
                routeSeparationMeters: 3f,
                headingDot: 1f), Is.True,
                "Recovery must not chatter off before returning to the inner " +
                "corridor.");
            Assert.That(TrafficWorldRuntime.ResolveTownRouteRejoinActive(
                currentlyActive: true,
                routeSeparationMeters: 2.49f,
                headingDot: 0.76f), Is.False);
            Assert.That(
                TrafficWorldRuntime.ResolveTownExcursionLookAheadMeters(
                    "route.traffic.mod-town-loop",
                    speedMetersPerSecond: 12f,
                    routeRejoinActive: true),
                Is.EqualTo(6f).Within(0.001f));
        }

        private static void AssertNearestJoin(
            TrafficRouteGeometry destination,
            Vector3 sourceEndpoint,
            bool forward,
            string message)
        {
            float progress = TrafficWorldRuntime.ResolveTownRouteJoinProgress(
                destination,
                sourceEndpoint,
                forward);
            TrafficRouteSample join = destination.Resolve(progress, forward);
            Assert.That(
                Vector3.Distance(join.Position, sourceEndpoint),
                Is.LessThan(2.5f),
                message);
        }
    }
}
