using System;
using System.Collections.Generic;
using System.Linq;
using MSC.Editor.Vegetation;
using MSC.World.Partition;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class MapVegetationPlanningRegressionTests
    {
        [Test]
        public void OverlappingBoundaryBandsChooseNearestSegmentIndependentOfInputOrder()
        {
            var distant = new MapVegetationBoundarySegment
                { Id = "a-distant", A = new Vector3(-20f, 0f, 8f), B = new Vector3(20f, 0f, 8f) };
            var nearest = new MapVegetationBoundarySegment
                { Id = "z-nearest", A = new Vector3(-20f, 0f, 0f), B = new Vector3(20f, 0f, 0f) };
            var forward = MapVegetationPlanning.BoundaryCandidates(new[] { distant, nearest },
                2f, 15f, 479, Matrix4x4.identity);
            var reverse = MapVegetationPlanning.BoundaryCandidates(new[] { nearest, distant },
                2f, 15f, 479, Matrix4x4.identity);
            Assert.That(forward.Select(point => (point.X, point.Z, point.Distance, point.SourceId)),
                Is.EqualTo(reverse.Select(point => (point.X, point.Z, point.Distance, point.SourceId))));
            var nearGroundEdge = forward.Single(point => point.X == 0 && point.Z == 0);
            Assert.That(nearGroundEdge.SourceId, Is.EqualTo("z-nearest"),
                "A far segment visited first must not consume the denser near-edge sample.");
            Assert.That(nearGroundEdge.Distance, Is.LessThan(2f));
        }

        [Test]
        public void BoundaryTopologyClosesOnlyStrictSmallGapWithSyntheticDistantSegment()
        {
            MapVegetationBoundarySegment[] open = Segments("hi", new[]
            {
                new Vector2(0f, 0f),
                new Vector2(2000f, 0f),
                new Vector2(2000f, 2000f),
                new Vector2(0f, 2000f),
                new Vector2(0f, 140f)
            }, close: false);

            MapVegetationPlanning.BoundaryEnclosure enclosure =
                MapVegetationPlanning.BuildBoundaryEnclosures(open).Single();
            Assert.That(enclosure.ClosedBySmallGap, Is.True);
            Assert.That(enclosure.ClosingGap, Is.EqualTo(140f).Within(0.01f));
            Assert.That(enclosure.Vertices.Count,
                Is.EqualTo(open.Length + 1),
                "An open chain polygon must retain both gap endpoints before the synthetic closure is evaluated.");
            Assert.That(enclosure.Segments.Count(segment =>
                segment.IsSyntheticClosure), Is.EqualTo(1));
            MapVegetationPlanning.OrientedBoundarySegment closure = enclosure
                .Segments.Single(segment => segment.IsSyntheticClosure);
            Assert.That(closure.Segment.OrientationMethod,
                Is.EqualTo("SyntheticSmallGapClosureForDistantMaskingOnly"));

            MapVegetationBoundarySegment[] unsafeGap = Segments("unsafe",
                new[]
                {
                    new Vector2(0f, 0f), new Vector2(100f, 0f),
                    new Vector2(100f, 100f), new Vector2(0f, 100f),
                    new Vector2(0f, 50f)
                }, close: false);
            Assert.Throws<System.IO.InvalidDataException>(() =>
                MapVegetationPlanning.BuildBoundaryEnclosures(unsafeGap));

            MapVegetationBoundarySegment[] absoluteLimit = Segments(
                "absolute-limit", new[]
                {
                    new Vector2(0f, 0f), new Vector2(10000f, 0f),
                    new Vector2(10000f, 5000f),
                    new Vector2(0f, 5000f), new Vector2(0f, 256f)
                }, close: false);
            Assert.That(MapVegetationPlanning.BuildBoundaryEnclosures(
                absoluteLimit).Single().ClosingGap,
                Is.EqualTo(256f).Within(0.01f));
            absoluteLimit[absoluteLimit.Length - 1].B =
                new Vector3(0f, 0f, 257f);
            Assert.Throws<System.IO.InvalidDataException>(() =>
                MapVegetationPlanning.BuildBoundaryEnclosures(absoluteLimit),
                "A visually material gap above the explicit 256 m ceiling must never be silently closed.");
        }

        [Test]
        public void SignedBackdropUsesOnlyOuterEnvelopeAndStaysOutsideEveryEnclosure()
        {
            MapVegetationBoundarySegment[] inner = Segments("low", new[]
            {
                new Vector2(-100f, -100f),
                new Vector2(100f, -100f),
                new Vector2(100f, 100f),
                new Vector2(-100f, 100f)
            }, close: true);
            MapVegetationBoundarySegment[] outer = Segments("high", new[]
            {
                new Vector2(-200f, -200f),
                new Vector2(200f, -200f),
                new Vector2(200f, 200f),
                new Vector2(-200f, 200f)
            }, close: true);
            MapVegetationBoundarySegment[] all = inner.Concat(outer).ToArray();
            ApplyValidatedTopology(all);
            IReadOnlyList<MapVegetationPlanning.BoundaryEnclosure> topology =
                MapVegetationPlanning.BuildBoundaryEnclosures(all);
            IReadOnlyList<MapVegetationPlanning.BoundaryCandidate> candidates =
                MapVegetationPlanning.SignedOutwardBoundaryCandidates(all,
                    20f, 80f, 479, Matrix4x4.identity);

            Assert.That(candidates, Is.Not.Empty);
            Assert.That(candidates.All(candidate =>
                candidate.SourceId.StartsWith("high:",
                    StringComparison.Ordinal)), Is.True,
                "The inner LOW ring must not receive a separate distant layer.");
            Assert.That(candidates.All(candidate => topology.All(enclosure =>
                !enclosure.Contains(new Vector2(candidate.Position.x,
                    candidate.Position.z)))), Is.True,
                "A locally outward half-plane must also be outside the union of proven enclosures.");
            Assert.That(candidates.All(candidate =>
                Vector3.Dot(candidate.Position - candidate.BoundaryPoint,
                    candidate.Outward) >= -0.001f), Is.True);
        }

        [Test]
        public void SignedBackdropSupportsClockwiseReversedDisjointAndMirroredTopology()
        {
            MapVegetationBoundarySegment[] clockwise = Segments("clockwise",
                new[]
                {
                    new Vector2(-200f, -200f),
                    new Vector2(-200f, 200f),
                    new Vector2(200f, 200f),
                    new Vector2(200f, -200f)
                }, close: true);
            MapVegetationBoundarySegment[] disjoint = Segments("disjoint",
                new[]
                {
                    new Vector2(600f, -100f),
                    new Vector2(800f, -100f),
                    new Vector2(800f, 100f),
                    new Vector2(600f, 100f)
                }, close: true);
            MapVegetationBoundarySegment[] all = clockwise.Concat(disjoint)
                .ToArray();
            // Endpoint direction and enumeration order are donor extraction
            // details, not semantic orientation evidence.
            foreach (MapVegetationBoundarySegment segment in all)
            {
                Vector3 swap = segment.A;
                segment.A = segment.B;
                segment.B = swap;
            }
            Array.Reverse(all);
            ApplyValidatedTopology(all);
            IReadOnlyList<MapVegetationPlanning.BoundaryEnclosure> topology =
                MapVegetationPlanning.BuildBoundaryEnclosures(all);
            Assert.That(topology.Count(item => item.IsOuterEnvelope),
                Is.EqualTo(2));

            Matrix4x4 mirrored = Matrix4x4.TRS(
                new Vector3(50f, 0f, -25f), Quaternion.identity,
                new Vector3(-1f, 1f, 1f));
            Matrix4x4 inverse = MapVegetationPlanning
                .InvertCoordinateMapping(mirrored);
            IReadOnlyList<MapVegetationPlanning.BoundaryCandidate> candidates =
                MapVegetationPlanning.SignedOutwardBoundaryCandidates(all,
                    20f, 80f, 479, mirrored);
            Assert.That(candidates, Is.Not.Empty);
            Assert.That(candidates.Select(candidate => candidate.SourceId
                    .Split(':')[0]).Distinct(),
                Is.EquivalentTo(new[] { "clockwise", "disjoint" }));
            Assert.That(candidates.All(candidate =>
                !MapVegetationPlanning.ContainsMappedPoint(topology,
                    candidate.Position, inverse)), Is.True);
            Assert.That(candidates.All(candidate => Vector3.Dot(
                    candidate.Position - candidate.BoundaryPoint,
                    candidate.Outward) >= -0.001f), Is.True,
                "No inward candidate may survive a mirrored coordinate correction.");

            IReadOnlyList<MapVegetationPlanning.BoundaryCandidate> inward =
                MapVegetationPlanning.SignedInwardBoundaryCandidates(all,
                    20f, 80f, 479, mirrored);
            Assert.That(inward, Is.Not.Empty);
            Assert.That(inward.All(candidate =>
                MapVegetationPlanning.ContainsMappedPoint(topology,
                    candidate.Position, inverse)), Is.True,
                "The near front layer must remain on the proven donor-forest side.");
            Assert.That(inward.All(candidate => Vector3.Dot(
                    candidate.Position - candidate.BoundaryPoint,
                    candidate.Outward) <= 0.001f), Is.True);
        }

        [Test]
        public void DistantCoverageBackboneUsesDeterministicConvexOutsideEnvelope()
        {
            MapVegetationBoundarySegment[] concave = Segments("concave",
                new[]
                {
                    new Vector2(-400f, -300f),
                    new Vector2(400f, -300f),
                    new Vector2(400f, 300f),
                    new Vector2(100f, 300f),
                    new Vector2(100f, -50f),
                    new Vector2(-100f, -50f),
                    new Vector2(-100f, 300f),
                    new Vector2(-400f, 300f)
                }, close: true);
            ApplyValidatedTopology(concave);
            IReadOnlyList<MapVegetationPlanning.BoundaryEnclosure>
                sourceTopology = MapVegetationPlanning
                    .BuildBoundaryEnclosures(concave);

            MapVegetationBoundarySegment[] envelope =
                MapVegetationPlanning.BuildDistantOuterEnvelopeSegments(
                    concave);
            Assert.That(envelope.Length, Is.EqualTo(4),
                "The deep concave notch must not become an inward-facing distant horizon edge.");
            Assert.That(envelope.All(segment => segment
                    .OrientationMethod ==
                "ConvexOuterEnvelopeDerivedFromDonorValidatedBoundaryForCollisionlessDistantMasking"),
                Is.True);
            IReadOnlyList<MapVegetationPlanning.BoundaryCandidate> targets =
                MapVegetationPlanning.DistantCoverageBackboneCandidates(
                    envelope, 95f, 650f, 30f, Matrix4x4.identity);
            int expected = envelope.Sum(segment =>
                3 * (Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(
                    segment.A, segment.B) / 30f)) + 1));
            Assert.That(targets.Count, Is.EqualTo(expected));
            Assert.That(targets.All(target =>
                !MapVegetationPlanning.ContainsMappedPoint(sourceTopology,
                    target.Position, Matrix4x4.identity)), Is.True,
                "Mandatory coverage silhouettes must stay outside the original donor enclosure too.");
            Assert.That(targets.All(target =>
                MapVegetationPlanning.DistantDepthBand(target.Distance,
                    95f, 650f) == target.Z), Is.True);

            foreach (MapVegetationBoundarySegment segment in envelope)
            {
                Vector2 a = new Vector2(segment.A.x, segment.A.z);
                Vector2 edge = new Vector2(segment.B.x - segment.A.x,
                    segment.B.z - segment.A.z);
                float length = edge.magnitude;
                Vector2 along = edge / length;
                for (int band = 0; band < 3; band++)
                {
                    float[] alongDistances = targets.Where(target =>
                            target.SourceId == segment.Id &&
                            target.Z == band)
                        .Select(target => Vector2.Dot(new Vector2(
                            target.BoundaryPoint.x,
                            target.BoundaryPoint.z) - a, along)).ToArray();
                    Assert.That(alongDistances.Length,
                        Is.EqualTo(Mathf.Max(1, Mathf.CeilToInt(length /
                            30f)) + 1));
                    int samples = Mathf.Max(1, Mathf.CeilToInt(length / 2f));
                    for (int sample = 0; sample <= samples; sample++)
                        Assert.That(MapVegetationPlanning
                                .HasAlongEdgeCoverage(
                                    (float)sample / samples * length,
                                    alongDistances, 15.001f), Is.True,
                            "Every segment and signed band needs a deterministic <=30 m backbone interval.");
                }
            }

            MapVegetationBoundarySegment[] reversed = concave.Reverse()
                .ToArray();
            MapVegetationBoundarySegment[] reversedEnvelope =
                MapVegetationPlanning.BuildDistantOuterEnvelopeSegments(
                    reversed);
            IReadOnlyList<MapVegetationPlanning.BoundaryCandidate>
                reversedTargets = MapVegetationPlanning
                    .DistantCoverageBackboneCandidates(reversedEnvelope,
                        95f, 650f, 30f, Matrix4x4.identity);
            Assert.That(reversedEnvelope.Select(segment => new
                {
                    segment.Id, segment.A, segment.B, segment.Outward
                }), Is.EqualTo(envelope.Select(segment => new
                {
                    segment.Id, segment.A, segment.B, segment.Outward
                })));
            Assert.That(reversedTargets.Select(target => new
                {
                    target.SourceId, target.X, target.Z, target.Position,
                    target.BoundaryPoint, target.Distance
                }), Is.EqualTo(targets.Select(target => new
                {
                    target.SourceId, target.X, target.Z, target.Position,
                    target.BoundaryPoint, target.Distance
                })));
        }

        [Test]
        public void NaturalInfillRequiresDonorTreesAroundTheGap()
        {
            Vector3 candidate = Vector3.zero;
            Vector3[] oneSided =
            {
                Polar(20f, -40f), Polar(24f, -15f), Polar(30f, 5f),
                Polar(35f, 25f), Polar(40f, 45f)
            };
            Vector3[] surrounded =
            {
                Polar(20f, 0f), Polar(25f, 70f), Polar(30f, 160f),
                Polar(35f, 250f), Polar(40f, 320f)
            };
            Assert.That(MapVegetationPlanning.HasSurroundingDonorForest(
                candidate, oneSided, 55f), Is.False,
                "A forest edge on one side is not an internal empty patch.");
            Assert.That(MapVegetationPlanning.HasSurroundingDonorForest(
                candidate, surrounded, 55f), Is.True);

            Vector3[] mirrored = surrounded.Select(tree =>
                new Vector3(-tree.x, tree.y, tree.z)).ToArray();
            Assert.That(MapVegetationPlanning.HasSurroundingDonorForest(
                candidate, mirrored, 55f), Is.True,
                "Mirroring the optional coordinate correction must preserve angular enclosure evidence.");
            Vector3[] exactHalfCircle =
            {
                Polar(20f, 0f), Polar(25f, 60f), Polar(30f, 120f),
                Polar(35f, 180f)
            };
            Assert.That(MapVegetationPlanning.HasSurroundingDonorForest(
                candidate, exactHalfCircle, 55f), Is.False,
                "An empty 180 degree arc is still one-sided donor evidence.");
            Assert.That(MapVegetationSurfaceQuery.ClassifyCanonical(
                "Clearing", "MAP/NATURAL_GROUND"),
                Is.EqualTo(MapVegetationExclusionKind.OpenSpace),
                "Known donor clearings remain a hard tree veto in addition to the angular test.");
        }

        [Test]
        public void DistantCoverageUsesThreeSignedDepthBandsAndSpacingCrownGap()
        {
            Assert.That(MapVegetationPlanning.DistantDepthBand(94.99f, 95f,
                650f), Is.EqualTo(-1));
            Assert.That(MapVegetationPlanning.DistantDepthBand(95f, 95f,
                650f), Is.EqualTo(0));
            Assert.That(MapVegetationPlanning.DistantDepthBand(199.99f, 95f,
                650f), Is.EqualTo(0));
            Assert.That(MapVegetationPlanning.DistantDepthBand(200f, 95f,
                650f), Is.EqualTo(1));
            Assert.That(MapVegetationPlanning.DistantDepthBand(400f, 95f,
                650f), Is.EqualTo(2));
            Assert.That(MapVegetationPlanning.DistantDepthBand(650f, 95f,
                650f), Is.EqualTo(2));
            Assert.That(MapVegetationPlanning.DistantDepthBand(650.01f, 95f,
                650f), Is.EqualTo(-1));

            float maximumGap = MapVegetationPlanning
                .MaximumDistantAlongEdgeGap(15f, 3f);
            Assert.That(maximumGap, Is.EqualTo(5.4f).Within(0.001f),
                "A trunk 36 m away must no longer count as crown closure.");
            Assert.That(MapVegetationPlanning.HasAlongEdgeCoverage(100f,
                new[] { 94.61f }, maximumGap), Is.True);
            Assert.That(MapVegetationPlanning.HasAlongEdgeCoverage(100f,
                new[] { 94.59f }, maximumGap), Is.False);

            Assert.That(MapVegetationPlanning
                    .DistantCoverageBackboneMinimumHeight(
                        new Vector2(11f, 24f)),
                Is.EqualTo(15.55f).Within(0.001f));
            Assert.That(MapVegetationPlanning
                    .MaximumDistantCrownCenterSpacing(3f),
                Is.EqualTo(5.4f).Within(0.001f));
        }

        [Test]
        public void DistantCrownCoverage_RejectsTrunkProximityAndRequiresClosedIntervals()
        {
            MapVegetationPlanning.DistantCrownCoverage sparse =
                MapVegetationPlanning.MeasureDistantCrownCoverage(20f,
                    new[]
                    {
                        new MapVegetationPlanning.DistantCrownProjection(
                            0f, 3f),
                        new MapVegetationPlanning.DistantCrownProjection(
                            10f, 3f),
                        new MapVegetationPlanning.DistantCrownProjection(
                            20f, 3f)
                    });
            Assert.That(sparse.IsClosed, Is.False,
                "Nearby trunks with non-overlapping crowns must fail.");
            Assert.That(sparse.GapCount, Is.EqualTo(2));
            Assert.That(sparse.LargestGap, Is.EqualTo(4f).Within(0.001f));

            MapVegetationPlanning.DistantCrownCoverage closed =
                MapVegetationPlanning.MeasureDistantCrownCoverage(20f,
                    new[]
                    {
                        new MapVegetationPlanning.DistantCrownProjection(
                            0f, 5.01f),
                        new MapVegetationPlanning.DistantCrownProjection(
                            10f, 5.01f),
                        new MapVegetationPlanning.DistantCrownProjection(
                            20f, 5.01f)
                    });
            Assert.That(closed.IsClosed, Is.True);
            Assert.That(closed.GapCount, Is.Zero);
            Assert.That(closed.CoveredLength,
                Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void PlacementFingerprintDetectsHeightNormalSpeciesAndSettingsChanges()
        {
            var plan = new MapVegetationCellPlan { Cell = new WorldCellIndex(0, 0), SettingsFingerprint = "settings-a" };
            var tree = new MapVegetationPlacement { id = "tree", category = MapVegetationCategories.OriginalTrees,
                position = new Vector3(1f, 2f, 3f), normal = Vector3.up, height = 10f, species = "pine" };
            plan.Woody.Add(tree);
            string original = MapVegetationPlanning.Fingerprint(plan);
            Assert.That(MapVegetationPlanning.Fingerprint(plan), Is.EqualTo(original));
            tree.height = 11f;
            Assert.That(MapVegetationPlanning.Fingerprint(plan), Is.Not.EqualTo(original));
            tree.height = 10f;
            tree.normal = new Vector3(0.1f, 1f, 0f).normalized;
            Assert.That(MapVegetationPlanning.Fingerprint(plan), Is.Not.EqualTo(original));
            tree.normal = Vector3.up;
            tree.species = "birch";
            Assert.That(MapVegetationPlanning.Fingerprint(plan), Is.Not.EqualTo(original));
            tree.species = "pine";
            plan.SettingsFingerprint = "settings-b";
            Assert.That(MapVegetationPlanning.Fingerprint(plan), Is.Not.EqualTo(original));
        }

        [Test]
        public void TrunkSpacingCrossesCellAndNegativeGridBoundaries()
        {
            var spacing = new MapVegetationPlanning.SpacingIndex(3f);
            spacing.Add(new Vector3(-0.2f, 2f, 511.5f));
            Assert.That(spacing.HasNeighbor(new Vector3(0.2f, 25f, 512.5f), 3f), Is.True);
            Assert.That(spacing.HasNeighbor(new Vector3(5f, 2f, 512.5f), 3f), Is.False);
        }

        [Test]
        public void InfillCap_UsesGlobalStableScoreInsteadOfCellIterationOrder()
        {
            var candidates = new[]
            {
                new RankedCandidate("cell_-4_1:a", 100u,
                    new Vector3(-2040f, 0f, 520f)),
                new RankedCandidate("cell_-4_1:b", 110u,
                    new Vector3(-2025f, 0f, 520f)),
                new RankedCandidate("cell_7_3:a", 1u,
                    new Vector3(3600f, 0f, 1550f)),
                new RankedCandidate("cell_7_3:spacing-reject", 2u,
                    new Vector3(3601f, 0f, 1550f))
            };

            var forward = MapVegetationPlanning
                .SelectStableCappedIndependentSet(candidates,
                    new Vector3[0], item => item.Score, item => item.Id,
                    item => item.Position, 3f, 2, out int forwardEligible);
            var reverse = MapVegetationPlanning
                .SelectStableCappedIndependentSet(candidates.Reverse(),
                    new Vector3[0], item => item.Score, item => item.Id,
                    item => item.Position, 3f, 2, out int reverseEligible);

            Assert.That(forwardEligible, Is.EqualTo(3));
            Assert.That(reverseEligible, Is.EqualTo(forwardEligible));
            Assert.That(forward.Select(item => item.Id),
                Is.EqualTo(reverse.Select(item => item.Id)));
            Assert.That(forward.Select(item => item.Id), Is.EqualTo(new[]
            {
                "cell_7_3:a", "cell_-4_1:a"
            }));
            Assert.That(forwardEligible - forward.Count, Is.EqualTo(1),
                "One otherwise eligible candidate is rejected only by the global cap.");
        }

        private sealed class RankedCandidate
        {
            public readonly string Id;
            public readonly uint Score;
            public readonly Vector3 Position;
            public RankedCandidate(string id, uint score, Vector3 position)
            {
                Id = id;
                Score = score;
                Position = position;
            }
        }

        private static MapVegetationBoundarySegment[] Segments(string source,
            IReadOnlyList<Vector2> points, bool close)
        {
            int count = close ? points.Count : points.Count - 1;
            var result = new MapVegetationBoundarySegment[count];
            for (int index = 0; index < count; index++)
            {
                Vector2 a = points[index];
                Vector2 b = points[(index + 1) % points.Count];
                result[index] = new MapVegetationBoundarySegment
                {
                    Id = source + ":" + index,
                    SourceStableId = source,
                    SourcePath = source,
                    A = new Vector3(a.x, 0f, a.y),
                    B = new Vector3(b.x, 0f, b.y),
                    AdjacentTriangleNormal = Vector3.forward
                };
            }
            return result;
        }

        private static void ApplyValidatedTopology(
            IReadOnlyList<MapVegetationBoundarySegment> segments)
        {
            foreach (MapVegetationPlanning.BoundaryEnclosure enclosure in
                     MapVegetationPlanning.BuildBoundaryEnclosures(segments))
            foreach (MapVegetationPlanning.OrientedBoundarySegment item in
                     enclosure.Segments)
            {
                if (item.IsSyntheticClosure) continue;
                item.Segment.Outward = new Vector3(item.Outward.x, 0f,
                    item.Outward.y);
                item.Segment.HasValidatedOutward = true;
                item.Segment.IsOuterEnvelope = enclosure.IsOuterEnvelope;
                item.Segment.EnclosureId = enclosure.Id;
            }
        }

        private static Vector3 Polar(float radius, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(radians) * radius, 0f,
                Mathf.Sin(radians) * radius);
        }
    }
}
