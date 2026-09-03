using System;
using System.IO;
using System.Linq;
using MSC.LegacyImport.Editor.GameplayPresentation;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.LegacyImport
{
    public sealed class TrafficCarExpansionRouteEvidenceTests
    {
        private const string DllSha256 =
            "7b626ca489a4abd7acdd34ca987199e6a27dd5bcd6cd398bab441360471313f5";
        private const string BundleSha256 =
            "adc45a6b161834ab88cb507d51d529ffff09687e0781c84b7ac5749b10e2bcaa";
        private const string PrefabSha256 =
            "76854020dff8d9562a88d9fd77e36155aa8c185f8a11b71e436db7a5af878752";

        [Test]
        public void FullResolutionManifest_PreservesLockedHashesAndNoStride()
        {
            EvidenceManifest manifest = LoadManifest();

            Assert.That(manifest.schemaVersion, Is.EqualTo(1));
            Assert.That(manifest.source.sha256, Is.EqualTo(DllSha256));
            Assert.That(manifest.source.extractedBundleSha256,
                Is.EqualTo(BundleSha256));
            Assert.That(manifest.source.routePrefabSha256,
                Is.EqualTo(PrefabSha256));
            Assert.That(manifest.source.extractionToolVersion,
                Is.EqualTo("traffic-car-expansion-route-evidence-v2"));
            Assert.That(manifest.routes.Length, Is.EqualTo(3));

            AssertRouteMetadata(
                manifest,
                "route.traffic.mod-town-entry",
                68,
                18,
                308.490f,
                5.673f,
                22.059f,
                1.307f,
                30);
            AssertRouteMetadata(
                manifest,
                "route.traffic.mod-town-loop",
                321,
                81,
                1631.214f,
                8.571f,
                33.041f,
                1.655f,
                294);
            AssertRouteMetadata(
                manifest,
                "route.traffic.mod-gas-pump",
                112,
                29,
                411.614f,
                7.546f,
                28.756f,
                1.313f,
                90);
        }

        [Test]
        public void Loader_PreservesSourceOrderEndpointsAndTranslation()
        {
            LockedTrafficRoute[] routes = Phase1TrafficRouteEvidence
                .LoadTrafficExpansionRouteEvidence()
                .ToArray();

            Assert.That(routes.Length, Is.EqualTo(3));
            AssertRoute(
                routes,
                "route.traffic.mod-town-entry",
                68,
                new Vector3(-1661.4641f, 4.925204f, 876.6011f),
                new Vector3(-1584.0558f, 3.269267f, 1140.0593f),
                5.674f);
            AssertRoute(
                routes,
                "route.traffic.mod-town-loop",
                321,
                new Vector3(-1593.8889f, 3.540428f, 1130.5618f),
                new Vector3(-1742.3616f, 5.510266f, 1150.7104f),
                8.572f);
            AssertRoute(
                routes,
                "route.traffic.mod-gas-pump",
                112,
                new Vector3(-1592.766f, 3.4857242f, 1131.8678f),
                new Vector3(-1734.383f, 5.8229966f, 1119.3253f),
                7.547f);
        }

        private static EvidenceManifest LoadManifest()
        {
            string path = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "Game",
                "LegacyImport",
                "Manifests",
                "TrafficCarExpansionBehaviorEvidence.json"));
            Assert.That(File.Exists(path), Is.True, path);
            EvidenceManifest manifest = JsonUtility.FromJson<EvidenceManifest>(
                File.ReadAllText(path));
            Assert.That(manifest, Is.Not.Null);
            Assert.That(manifest.source, Is.Not.Null);
            Assert.That(manifest.routes, Is.Not.Null);
            return manifest;
        }

        private static void AssertRouteMetadata(
            EvidenceManifest manifest,
            string routeId,
            int pointCount,
            int oldPointCount,
            float length,
            float maximumSegment,
            float oldMaximumSegment,
            float oldMaximumChordDeviation,
            int oldMaximumChordDeviationPointIndex)
        {
            RouteEvidence route = manifest.routes.Single(candidate =>
                candidate.routeId == routeId);
            Assert.That(route.closesLoop, Is.False,
                "TrafficCarExpansion's town branches reconnect through the " +
                "highway route; none closes onto itself.");
            Assert.That(route.sourcePointCount, Is.EqualTo(pointCount));
            Assert.That(route.sourcePointStride, Is.EqualTo(1));
            Assert.That(route.sourceWorldPoints.Length, Is.EqualTo(pointCount));
            Assert.That(route.extractionMetrics.fullPolylineLengthMeters,
                Is.EqualTo(length).Within(0.001f));
            Assert.That(route.extractionMetrics.maximumSourceSegmentLengthMeters,
                Is.EqualTo(maximumSegment).Within(0.001f));
            Assert.That(route.extractionMetrics.legacyStrideFourPointCount,
                Is.EqualTo(oldPointCount));
            Assert.That(
                route.extractionMetrics
                    .legacyStrideFourMaximumSegmentLengthMeters,
                Is.EqualTo(oldMaximumSegment).Within(0.001f));
            Assert.That(
                route.extractionMetrics
                    .legacyStrideFourMaximumPlanarChordDeviationMeters,
                Is.EqualTo(oldMaximumChordDeviation).Within(0.001f));
            Assert.That(
                route.extractionMetrics
                    .legacyStrideFourMaximumPlanarChordDeviationSourcePointIndex,
                Is.EqualTo(oldMaximumChordDeviationPointIndex));
            Assert.That(
                route.extractionMetrics
                    .legacyStrideFourMaximumPlanarChordDeviationMeters,
                Is.GreaterThan(1.3f),
                "The old stride-four route can cut more than 1.3 m inside a " +
                "corner, which is material relative to a traffic lane.");
            Assert.That(
                route.extractionMetrics
                    .legacyStrideFourMaximumSegmentLengthMeters,
                Is.GreaterThan(
                    route.extractionMetrics.maximumSourceSegmentLengthMeters *
                    3f));
        }

        private static void AssertRoute(
            LockedTrafficRoute[] routes,
            string routeId,
            int pointCount,
            Vector3 expectedFirst,
            Vector3 expectedLast,
            float maximumAllowedSegmentMeters)
        {
            LockedTrafficRoute route = routes.Single(candidate =>
                candidate.RouteId == routeId);
            Assert.That(route.SourceWorldPoints.Count, Is.EqualTo(pointCount));
            AssertVector(route.SourceWorldPoints[0], expectedFirst);
            AssertVector(route.SourceWorldPoints[pointCount - 1], expectedLast);
            Assert.That(
                Vector2.Distance(
                    new Vector2(expectedFirst.x, expectedFirst.z),
                    new Vector2(expectedLast.x, expectedLast.z)),
                Is.GreaterThan(100f),
                "The source branch is intentionally open, not a closed loop.");

            Vector3 translation = new Vector3(169.98f, 1.611f, -1040.625f);
            float maximumSegment = 0f;
            for (int index = 0; index < pointCount; index++)
            {
                AssertVector(
                    route.ProjectWorldPoints[index],
                    route.SourceWorldPoints[index] + translation);
                if (index > 0)
                {
                    maximumSegment = Mathf.Max(
                        maximumSegment,
                        Vector3.Distance(
                            route.SourceWorldPoints[index - 1],
                            route.SourceWorldPoints[index]));
                }
            }

            Assert.That(maximumSegment,
                Is.LessThan(maximumAllowedSegmentMeters));
        }

        private static void AssertVector(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0002f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0002f));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(0.0002f));
        }

        [Serializable]
        private sealed class EvidenceManifest
        {
            public int schemaVersion = 0;
            public SourceEvidence source = new SourceEvidence();
            public RouteEvidence[] routes = Array.Empty<RouteEvidence>();
        }

        [Serializable]
        private sealed class SourceEvidence
        {
            public string sha256 = string.Empty;
            public string extractedBundleSha256 = string.Empty;
            public string routePrefabSha256 = string.Empty;
            public string extractionToolVersion = string.Empty;
        }

        [Serializable]
        private sealed class RouteEvidence
        {
            public string routeId = string.Empty;
            public bool closesLoop = false;
            public int sourcePointCount = 0;
            public int sourcePointStride = 0;
            public Vector3[] sourceWorldPoints = Array.Empty<Vector3>();
            public ExtractionMetrics extractionMetrics =
                new ExtractionMetrics();
        }

        [Serializable]
        private sealed class ExtractionMetrics
        {
            public float fullPolylineLengthMeters = 0f;
            public float maximumSourceSegmentLengthMeters = 0f;
            public int legacyStrideFourPointCount = 0;
            public float legacyStrideFourMaximumSegmentLengthMeters = 0f;
            public float legacyStrideFourMaximumPlanarChordDeviationMeters = 0f;
            public int legacyStrideFourMaximumPlanarChordDeviationSourcePointIndex =
                0;
        }
    }
}
