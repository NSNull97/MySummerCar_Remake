using System;
using System.Linq;
using System.Text.RegularExpressions;
using MSC.World.Remaster;
using MSC.World.Remaster.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MSC.Tests.EditMode.WorldRemaster
{
    public sealed class WorldValidationEditModeTests
    {
        [Test]
        public void GateCalculator_NoneWhenPilotHasBlockingIssues()
        {
            var results = new[]
            {
                new WorldValidationGateResult { gate = WorldValidationGate.PilotGate.ToString(), achieved = false },
                new WorldValidationGateResult { gate = WorldValidationGate.VerticalSliceGate.ToString(), achieved = false },
                new WorldValidationGateResult { gate = WorldValidationGate.FullWorldGate.ToString(), achieved = false }
            };

            Assert.That(WorldValidationGateCalculator.HighestAchieved(results), Is.EqualTo(WorldValidationGate.None));
        }

        [Test]
        public void GateCalculator_PilotDoesNotImplyVerticalOrFull()
        {
            var results = new[]
            {
                new WorldValidationGateResult { gate = WorldValidationGate.PilotGate.ToString(), achieved = true },
                new WorldValidationGateResult { gate = WorldValidationGate.VerticalSliceGate.ToString(), achieved = false },
                new WorldValidationGateResult { gate = WorldValidationGate.FullWorldGate.ToString(), achieved = false }
            };

            Assert.That(WorldValidationGateCalculator.HighestAchieved(results), Is.EqualTo(WorldValidationGate.PilotGate));
        }

        [Test]
        public void GateCalculator_DoesNotSkipFailedPrerequisites()
        {
            var results = new[]
            {
                new WorldValidationGateResult { gate = WorldValidationGate.PilotGate.ToString(), achieved = false },
                new WorldValidationGateResult { gate = WorldValidationGate.VerticalSliceGate.ToString(), achieved = true },
                new WorldValidationGateResult { gate = WorldValidationGate.FullWorldGate.ToString(), achieved = true }
            };

            Assert.That(WorldValidationGateCalculator.HighestAchieved(results), Is.EqualTo(WorldValidationGate.None));
        }

        [Test]
        public void CurrentWorld_ReportsExactCoverageAndAchievesOnlyPilotGate()
        {
            WorldValidationResult result = WorldValidationRunner.ValidateAllCached();

            Assert.That(result.sourceRecordCount, Is.EqualTo(13509));
            Assert.That(result.eligibleWorldRecordCount, Is.EqualTo(3842));
            Assert.That(result.productionBindingCount, Is.EqualTo(33));
            Assert.That(result.concreteCellCount, Is.EqualTo(49));
            Assert.That(result.productionBoundCellCount, Is.EqualTo(2));
            Assert.That(result.supplementalSafetyPieceCount, Is.EqualTo(2));
            Assert.That(result.approvedReplacementCount, Is.Zero);
            Assert.That(result.achievedGate, Is.EqualTo(WorldValidationGate.PilotGate.ToString()));

            WorldValidationGateResult pilot = result.gates.Single(gate => gate.gate == WorldValidationGate.PilotGate.ToString());
            Assert.That(pilot.achieved, Is.True);
            Assert.That(pilot.blockingIssueIds, Is.Empty);
            Assert.That(result.issues.Single(issue => issue.issueId == "WORLD-STREAM-001").IsOpen, Is.False);
            Assert.That(result.issues.Single(issue => issue.issueId == "WORLD-STREAM-002").IsOpen, Is.False);
            Assert.That(result.issues.Single(issue => issue.issueId == "WORLD-COL-002").IsOpen, Is.False);
            Assert.That(result.issues.Single(issue => issue.issueId == "WORLD-PERF-001").IsOpen, Is.False);
            Assert.That(result.validatorRuns.Single(run => run.validatorId == "production-streaming-wiring").passed, Is.True);
            Assert.That(result.validatorRuns.Single(run =>
                run.validatorId == "production-streaming-lifecycle").passed, Is.True);
            Assert.That(result.validatorRuns.Single(run => run.validatorId == "m4-character-controller-traversal").passed, Is.True);
            Assert.That(result.validatorRuns.Single(run =>
                run.validatorId == WorldPilotPerformanceEvidenceReader.ValidatorId).passed, Is.True);
            Assert.That(
                result.performanceLocations.Where(location =>
                    location.locationId is "pilot-home" or "dense-vegetation" or
                        "interior-transition" or "water-shoreline").Select(location => location.status),
                Is.All.EqualTo("MeasuredBounded"));

            WorldValidationGateResult vertical = result.gates.Single(gate =>
                gate.gate == WorldValidationGate.VerticalSliceGate.ToString());
            Assert.That(vertical.achieved, Is.False);
        }

        [Test]
        public void StableIdParity_SourceRegistryAndSupplementsDoNotCollide()
        {
            WorldValidationResult result = WorldValidationRunner.ValidateAllCached();
            WorldValidationIssue identity = result.issues.Single(issue => issue.issueId == "WORLD-ID-001");

            Assert.That(identity.state, Is.EqualTo(WorldValidationIssueState.Closed.ToString()));
            Assert.That(WorldValidationRunner.LoadSupplementalStableIds(), Is.Unique);
        }

        [Test]
        public void ProductionDependencyAudit_CoversBuildScenesProductionAndVoidFill()
        {
            WorldValidationDependencyAudit audit = WorldValidationRunner.BuildDependencyAudit();

            Assert.That(audit.seedAssets, Is.GreaterThan(0));
            Assert.That(audit.visitedAssets, Is.GreaterThan(0));
            Assert.That(audit.dependencyEdges, Is.GreaterThan(0));
            Assert.That(audit.enabledBuildScenes, Does.Contain(WorldRemasterPaths.PilotCellScene));
            Assert.That(audit.enabledBuildScenes, Does.Contain(WorldRemasterPaths.NextZoneCellScene));
            Assert.That(
                audit.enabledBuildScenes,
                Is.EqualTo(UnityEditor.EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray()));
            Assert.That(audit.violations, Is.Empty);
        }

        [Test]
        public void SelectedZone_FilterRetainsGlobalBlockersAndOnlySelectedZoneRow()
        {
            WorldValidationResult result = WorldValidationRunner.ValidateZoneCached(
                WorldRemasterPaths.NextZoneId,
                WorldValidationGate.PilotGate);

            Assert.That(result.issues.Any(issue => issue.issueId == "WORLD-STREAM-001"), Is.True);
            Assert.That(
                result.coverage.Where(row => row.scope == "Zone").Select(row => row.scopeId),
                Is.EqualTo(new[] { WorldRemasterPaths.NextZoneId }));
            Assert.That(result.scope, Is.EqualTo("Zone"));
            Assert.That(result.issues.Any(issue => issue.issueId == "WORLD-COL-002"), Is.False);
            WorldValidationGateResult pilot = result.gates.Single(candidate =>
                candidate.gate == WorldValidationGate.PilotGate.ToString());
            Assert.That(pilot.blockingIssueIds, Does.Not.Contain("WORLD-COL-002"));
        }

        [Test]
        public void SpatialMetrics_CalculateNearestRankP95()
        {
            Assert.That(
                WorldValidationGateCalculator.Percentile95(new[] { 0f, 0.1f, 0.2f, 0.3f, 1f }),
                Is.EqualTo(1f).Within(0.000001f));
            Assert.That(float.IsNaN(WorldValidationGateCalculator.Percentile95(Array.Empty<float>())), Is.True);
        }

        [Test]
        public void Exports_AreDeterministicAndIssueIdsAreStable()
        {
            WorldValidationResult first = WorldValidationRunner.ValidateAllCached();
            WorldValidationResult second = WorldValidationRunner.ValidateAllCached();

            Assert.That(JsonUtility.ToJson(second), Is.EqualTo(JsonUtility.ToJson(first)));
            Assert.That(first.issues.Select(issue => issue.issueId), Is.Unique);
            Assert.That(
                first.issues.All(issue => Regex.IsMatch(
                    issue.issueId,
                    @"^WORLD-(ID|GEO|ROAD|BLD|COL|LOD|STREAM|PERF|DONOR)-[0-9]{3}$")),
                Is.True);
        }

        [Test]
        public void Export_RejectsScopedZoneResult()
        {
            WorldValidationResult scoped = WorldValidationRunner.ValidateZoneCached(
                WorldRemasterPaths.NextZoneId,
                WorldValidationGate.PilotGate);

            Assert.Throws<InvalidOperationException>(() => WorldValidationRunner.Export(scoped));
        }

        [Test]
        public void MissingReplacementCoverage_BlocksVerticalAndFullGates()
        {
            WorldValidationResult result = WorldValidationRunner.ValidateAllCached();
            WorldValidationIssue coverage = result.issues.Single(issue => issue.issueId == "WORLD-GEO-001");

            Assert.That(coverage.IsOpen, Is.True);
            Assert.That(coverage.Blocks(WorldValidationGate.VerticalSliceGate), Is.True);
            Assert.That(coverage.Blocks(WorldValidationGate.FullWorldGate), Is.True);
            Assert.That(coverage.Blocks(WorldValidationGate.PilotGate), Is.False);
        }

        [Test]
        public void RegistryZoneBindings_MatchReviewedStableWorldIdSets()
        {
            WorldProductionAssetRegistry registry = UnityEditor.AssetDatabase.LoadAssetAtPath<WorldProductionAssetRegistry>(
                WorldRemasterPaths.RegistryAsset);
            Assert.That(registry, Is.Not.Null);

            string[] pilotExpected =
            {
                "16d106ad9e02f79366ef6f57515f256d", "1f9b558b6b736aff90218d6f698262fe",
                "20b94503abb28f5b1d40f4817cbb5245", "250d1cb74558e7c7e27e5e860981c938",
                "35617a145297c4d6cb70a8ab9478bcda", "366dd19254f921ef78e095702f5ed505",
                "3d723f0edfad5b10af107ef24845322d", "419f49d30da6bff0fcfa679c84d652ce",
                "52fa266116cb5c138c2e90b8dd4b6bad", "5b3e05ba0c1362f86ceb10538db83035",
                "6565829bc23d922257712654b4607ecf", "674681b73ae9d9ea1030a148f70648e4",
                "89912bcee006371d2641f9faa328e371", "9c5d1f176cabbc3945c75ebc533e5b36",
                "b2f988842fbd9f52370994c7d6548f52", "b5e7b987d5aac9da197a6ce662beb64c",
                "c5f236e95738088bfd0c4f3925b1e6a4", "e78dd29f9ccbde760e86f6fca6a901d1",
                "e8660bda40e1d2946e004456e245c803", "ea6d046fbf56abd80120a480ede030ce",
                "eebd5ee197a22fbc9bc38d8af5b32e1a", "f7f5381e1e99a73ec53e01cfc34110aa",
                "f886e748a2d6fefc994d59272ed46fa2", "fc7439d36774290084d16e553de0f117"
            };
            string[] nextExpected =
            {
                "345dc7662dae9f1f01d77b15f74e5f8f", "449b18de0c10f87887e3f3304a90366e",
                "56a7aa7c66146248d6c820c31a6b99fd", "847f56ce8c1be238f4bcae514bb55fdf",
                "b412961b75cb019e74a83b24faac32a4", "b8de7336e204fae3ba333227b3e94d19",
                "de5d5682cbef7d27a48a473d5e85877d", "e0fa39e1ceeed93727dd86e749c6d115",
                "f700b12cf5c75a3906dd079acea3f274"
            };

            Assert.That(BoundIds(registry, WorldRemasterPaths.PilotZoneId), Is.EqualTo(pilotExpected));
            Assert.That(BoundIds(registry, WorldRemasterPaths.NextZoneId), Is.EqualTo(nextExpected));
        }

        private static string[] BoundIds(WorldProductionAssetRegistry registry, string zoneId) => registry.Records
            .Where(record => record.HasProductionReplacement && record.ProductionZone == zoneId)
            .Select(record => record.StableWorldId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }
}
