using System;
using System.Linq;
using MSC.Editor.WorldBaseline;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;

namespace MSC.Tests.EditMode.WorldBaseline
{
    [Category("LocalDonorBaseline")]
    public sealed class DonorWorldCellizationEditModeTests
    {
        [SetUp]
        public void RequireGeneratedRuntimeBaseline()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    WorldBaseline06B2Paths.GlobalScene) == null)
            {
                Assert.Ignore(
                    "Локальный generated RuntimeBaseline 06B2 отсутствует. " +
                    "Сгенерируйте donor streaming profile перед запуском " +
                    "этих тестов.");
            }
        }

        [Test]
        public void CellizationPlan_RepeatedLoadHasLockedCountsAndFingerprint()
        {
            DonorWorldCellizationPlan first =
                DonorWorldCellizationPlan.Load();
            DonorWorldCellizationPlan second =
                DonorWorldCellizationPlan.Load();

            Assert.That(
                first.Assignments.Count,
                Is.EqualTo(DonorWorldCellizationPlan.ExpectedEntityCount));
            Assert.That(
                first.Assignments.Count(assignment => assignment.IsGlobal),
                Is.EqualTo(
                    DonorWorldCellizationPlan.ExpectedGlobalEntityCount));
            Assert.That(
                first.Assignments.Count(assignment => !assignment.IsGlobal),
                Is.EqualTo(
                    DonorWorldCellizationPlan.ExpectedCellEntityCount));
            Assert.That(
                first.CellIds.Count,
                Is.EqualTo(DonorWorldCellizationPlan.ExpectedCellCount));
            Assert.That(
                first.SafeColliders.Count,
                Is.EqualTo(DonorWorldCellizationPlan.ExpectedColliderCount));
            Assert.That(
                first.SafeColliders.Count(collider =>
                    collider.ColliderType == "MeshCollider"),
                Is.EqualTo(
                    DonorWorldCellizationPlan.ExpectedMeshColliderCount));
            Assert.That(
                first.SafeColliders.Count(collider =>
                    collider.ColliderType == "BoxCollider"),
                Is.EqualTo(
                    DonorWorldCellizationPlan.ExpectedBoxColliderCount));

            Assert.That(
                first.CellIds,
                Is.EqualTo(second.CellIds));
            Assert.That(
                first.OwnershipFingerprintSha256,
                Is.EqualTo(second.OwnershipFingerprintSha256));
            Assert.That(
                first.OwnershipFingerprintSha256,
                Has.Length.EqualTo(64));
            Assert.That(
                first.OwnershipFingerprintSha256.All(character =>
                    Uri.IsHexDigit(character) &&
                    !char.IsUpper(character)),
                Is.True,
                "Ownership fingerprint must be canonical lowercase SHA-256.");
        }

        [Test]
        public void ActiveDonorManifest_ExcludesPrototypePathsAndKeepsGameplayCatalog()
        {
            DonorWorldCellizationPlan plan =
                DonorWorldCellizationPlan.Load();
            ProductionWorldStreamingManifest active =
                AssetDatabase.LoadAssetAtPath<
                    ProductionWorldStreamingManifest>(
                    WorldBaseline06B2Paths.ActiveManifest);
            ProductionWorldStreamingManifest prototype =
                AssetDatabase.LoadAssetAtPath<
                    ProductionWorldStreamingManifest>(
                    WorldBaseline06B2Paths.PrototypeManifest);

            Assert.That(active, Is.Not.Null);
            Assert.That(prototype, Is.Not.Null);
            Assert.That(
                active.ValidateConfiguration(),
                Is.Empty);
            Assert.That(
                active.ProfileId,
                Is.EqualTo(WorldBaseline06B2Paths.ProfileId));
            Assert.That(
                active.ProfileKind,
                Is.EqualTo(
                    ProductionWorldProfileKind.DonorFeatureParity));
            Assert.That(active.PrivateLocalRuntimeBaseline, Is.True);
            Assert.That(
                active.CellSizeMeters,
                Is.EqualTo(WorldBaseline06B2Paths.CellSizeMeters));
            Assert.That(
                active.LoadingRadiusCells,
                Is.EqualTo(WorldBaseline06B2Paths.LoadingRadiusCells));
            Assert.That(
                active.UnloadingRadiusCells,
                Is.EqualTo(WorldBaseline06B2Paths.UnloadingRadiusCells));
            Assert.That(
                active.VehiclePreloadSpeedMetersPerSecond,
                Is.EqualTo(
                    WorldBaseline06B2Paths
                        .VehiclePreloadSpeedMetersPerSecond));
            Assert.That(
                active.VehiclePreloadRadiusCells,
                Is.EqualTo(
                    WorldBaseline06B2Paths.VehiclePreloadRadiusCells));

            Assert.That(active.GlobalScenes.Count, Is.EqualTo(1));
            Assert.That(
                active.GlobalScenes[0].SceneId,
                Is.EqualTo("global-legacy"));
            Assert.That(
                active.GlobalScenes[0].ScenePath,
                Is.EqualTo(WorldBaseline06B2Paths.GlobalScene));
            Assert.That(
                active.Cells.Select(cell => cell.CellId).ToArray(),
                Is.EqualTo(plan.CellIds.ToArray()));
            Assert.That(
                active.Cells.All(cell =>
                    string.Equals(
                        cell.ScenePath,
                        WorldBaseline06B2Paths.CellScene(cell.CellId),
                        StringComparison.Ordinal)),
                Is.True);

            string[] prototypePaths = prototype.Cells
                .Select(cell => cell.ScenePath)
                .Concat(prototype.GlobalScenes.Select(scene =>
                    scene.ScenePath))
                .ToArray();
            string[] activePaths = active.Cells
                .Select(cell => cell.ScenePath)
                .Concat(active.GlobalScenes.Select(scene =>
                    scene.ScenePath))
                .ToArray();
            Assert.That(
                activePaths.Any(path => path.Contains(
                    "/Generated/ProductionCells/",
                    StringComparison.Ordinal)),
                Is.False);
            Assert.That(
                activePaths.Intersect(
                    prototypePaths,
                    StringComparer.Ordinal),
                Is.Empty);

            WorldGameplayCellCatalog catalog = active.GameplayCatalog;
            Assert.That(catalog, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(catalog),
                Is.EqualTo(WorldBaseline06B2Paths.GameplayCatalog));
            Assert.That(
                catalog.CatalogId,
                Is.EqualTo(WorldBaseline06B2Paths.GameplayCatalogId));
            Assert.That(catalog.ValidateConfiguration(), Is.Empty);
            Assert.That(catalog.Anchors.Count, Is.EqualTo(15));
            Assert.That(
                catalog.Anchors.Select(anchor => anchor.AnchorId).Distinct(
                    StringComparer.Ordinal).Count(),
                Is.EqualTo(catalog.Anchors.Count));
            Assert.That(
                catalog.Anchors.Select(anchor => anchor.StableEntityId)
                    .Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(catalog.Anchors.Count));
            Assert.That(
                catalog.Anchors.Select(anchor => anchor.Cell.Id).Distinct(
                    StringComparer.Ordinal),
                Is.EquivalentTo(new[] { "cell_0_-3", "cell_0_-2" }));
        }

        [Test]
        public void FullValidatorContract_PassesWithoutInspectingEveryGeneratedScene()
        {
            DonorWorldCellizationValidationResult result =
                DonorWorldCellizationValidator.Validate(
                    verifySourceHashes: false,
                    inspectAllGeneratedScenes: false);
            DonorWorldCellizationPlan plan =
                DonorWorldCellizationPlan.Load();

            Assert.That(
                result.Passed,
                Is.True,
                string.Join(" | ", result.Errors));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(
                result.OwnershipFingerprintSha256,
                Is.EqualTo(plan.OwnershipFingerprintSha256));
            Assert.That(result.GameplayAnchorCount, Is.EqualTo(15));
        }

        [Test]
        public void Plan_HasUniqueLegacyReplacementAndColliderIds()
        {
            DonorWorldCellizationPlan plan =
                DonorWorldCellizationPlan.Load();
            string[] entityIds = plan.Assignments
                .Select(assignment =>
                    assignment.SanitationEntry.Placement.StableId)
                .ToArray();
            string[] replacementKeys = entityIds
                .Select(id => "legacy-world:" + id)
                .ToArray();
            string[] colliderIds = plan.SafeColliders
                .Select(collider => collider.ColliderStableId)
                .ToArray();
            string[] colliderEntityIds = plan.SafeColliders
                .Select(collider => collider.EntityStableId)
                .ToArray();
            string[] globalEntityIds = plan.Assignments
                .Where(assignment => assignment.IsGlobal)
                .Select(assignment =>
                    assignment.SanitationEntry.Placement.StableId)
                .ToArray();

            Assert.That(
                entityIds.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(entityIds.Length));
            Assert.That(
                replacementKeys.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(replacementKeys.Length));
            Assert.That(
                colliderIds.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(colliderIds.Length));
            Assert.That(
                colliderEntityIds.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(colliderEntityIds.Length));
            Assert.That(
                colliderEntityIds.Except(
                    globalEntityIds,
                    StringComparer.Ordinal),
                Is.Empty,
                "All allowlisted traversal colliders must remain global.");
        }
    }
}
