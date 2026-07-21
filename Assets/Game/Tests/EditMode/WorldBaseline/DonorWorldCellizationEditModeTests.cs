using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Editor.WorldBaseline;
using MSC.Editor.WorldTransfer;
using MSC.LegacyImport;
using MSC.World.Data;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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
                first.SafeColliders.Count(collider =>
                    collider.ColliderType == "CapsuleCollider"),
                Is.EqualTo(
                    DonorWorldCellizationPlan.ExpectedCapsuleColliderCount));
            Assert.That(
                first.SafeColliders.Count(collider =>
                    collider.ColliderType == "SphereCollider"),
                Is.EqualTo(
                    DonorWorldCellizationPlan.ExpectedSphereColliderCount));
            Assert.That(
                first.ColliderDispositions.Count,
                Is.EqualTo(
                    DonorWorldCellizationPlan
                        .ExpectedSourceColliderDispositionCount));

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
                plan.SafeColliders
                    .GroupBy(
                        collider => collider.EntityStableId,
                        StringComparer.Ordinal)
                    .Any(group => group.Count() > 1),
                Is.True,
                "The static-world pass must preserve multiple source " +
                "colliders on one entity.");
            Assert.That(
                plan.SafeColliders
                    .Where(collider => collider.IsSafetyCritical)
                    .Select(collider => collider.EntityStableId)
                    .Except(
                    globalEntityIds,
                    StringComparer.Ordinal),
                Is.Empty,
                "The immutable 32 safety-critical colliders must remain " +
                "global.");
            Assert.That(
                plan.SafeColliders.Any(collider =>
                    !collider.IsSafetyCritical &&
                    !globalEntityIds.Contains(
                        collider.EntityStableId,
                        StringComparer.Ordinal)),
                Is.True,
                "Ordinary static-world colliders must remain cell-owned.");
        }

        [Test]
        public void SolidCollisionPolicy_HasExplicitSafeDispositions()
        {
            DonorWorldCellizationPlan plan =
                DonorWorldCellizationPlan.Load();

            var expectedDispositionCounts =
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["ExcludedActorOrPlayer"] = 23,
                    ["ExcludedBuiltinMeshRequiresMapping"] = 24,
                    ["ExcludedCategory"] = 2,
                    ["ExcludedDisabled"] = 32,
                    ["ExcludedDoorRequiresBinding"] = 79,
                    ["ExcludedDynamicRequiresPresenter"] = 108,
                    ["ExcludedInactive"] = 212,
                    ["ExcludedTrigger"] = 414,
                    ["ExcludedVehicle"] = 7,
                    ["ExcludedWeatherShelterVolume"] = 1,
                    ["IncludedSafetyCriticalGlobal"] = 32,
                    ["IncludedStaticWorldSolid"] = 554
                };
            Dictionary<string, int> actualDispositionCounts = plan
                .ColliderDispositions
                .GroupBy(record => record.Disposition, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.Ordinal);
            Assert.That(
                actualDispositionCounts.Keys,
                Is.EquivalentTo(expectedDispositionCounts.Keys));
            foreach (KeyValuePair<string, int> expected in
                     expectedDispositionCounts)
            {
                Assert.That(
                    actualDispositionCounts[expected.Key],
                    Is.EqualTo(expected.Value),
                    expected.Key);
            }

            var expectedNewStaticObstacleCounts =
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["StaticProp"] = 67,
                    ["UtilityPole"] = 12,
                    ["Rock"] = 3,
                    ["Landmark"] = 2
                };
            foreach (KeyValuePair<string, int> expected in
                     expectedNewStaticObstacleCounts)
            {
                Assert.That(
                    plan.SafeColliders.Count(collider =>
                        !collider.IsSafetyCritical &&
                        string.Equals(
                            collider.SemanticCategory,
                            expected.Key,
                            StringComparison.Ordinal)),
                    Is.EqualTo(expected.Value),
                    expected.Key);
            }

            Dictionary<string, int> remainingExcludedCategories = plan
                .ColliderDispositions
                .Where(record => string.Equals(
                    record.Disposition,
                    "ExcludedCategory",
                    StringComparison.Ordinal))
                .GroupBy(
                    record => record.SemanticCategory,
                    StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => group.Count(),
                    StringComparer.Ordinal);
            Assert.That(
                remainingExcludedCategories,
                Is.EquivalentTo(new Dictionary<string, int>(
                    StringComparer.Ordinal)
                {
                    ["InteractivePropCandidate"] = 2
                }));

            Assert.That(
                plan.SafeColliders
                    .Where(collider =>
                        DonorWorldSolidCollisionPolicy
                            .ReviewedStaticObstacleIds.Contains(
                                collider.ColliderStableId))
                    .Select(collider => new
                    {
                        collider.ColliderStableId,
                        collider.SemanticCategory,
                        collider.CollisionLayerName
                    }),
                Is.EquivalentTo(new[]
                {
                    new
                    {
                        ColliderStableId =
                            "c278369891f09728f6be083a38529ade",
                        SemanticCategory = "StaticProp",
                        CollisionLayerName =
                            DonorWorldSolidCollisionPolicy.WorldSolidLayer
                    },
                    new
                    {
                        ColliderStableId =
                            "43903189d1d95d8161c3a2b2e0a9c329",
                        SemanticCategory = "StaticProp",
                        CollisionLayerName =
                            DonorWorldSolidCollisionPolicy.WorldSolidLayer
                    },
                    new
                    {
                        ColliderStableId =
                            "d583dc35d17cb2d7ea5b80aded288d46",
                        SemanticCategory = "StaticProp",
                        CollisionLayerName =
                            DonorWorldSolidCollisionPolicy.WorldSolidLayer
                    },
                    new
                    {
                        ColliderStableId =
                            "42a5b4862a5027c15b740ff3c6ba8588",
                        SemanticCategory = "StaticProp",
                        CollisionLayerName =
                            DonorWorldSolidCollisionPolicy.WorldSolidLayer
                    }
                }),
                "The four frozen reviewed scenery obstacles must bypass " +
                "their broad donor Water/Wire categories as WorldSolid " +
                "StaticProp colliders.");

            Assert.That(
                plan.SafeColliders.Count(collider =>
                    collider.IsSafetyCritical),
                Is.EqualTo(
                    DonorWorldCellizationPlan
                        .ExpectedSafetyCriticalColliderCount));
            Assert.That(
                plan.SafeColliders.All(collider =>
                    collider.SourceEnabled &&
                    !collider.SourceIsTrigger &&
                    collider.EffectiveActive &&
                    !string.IsNullOrWhiteSpace(
                        collider.CollisionLayerName) &&
                    !string.IsNullOrWhiteSpace(
                        collider.PhysicsMaterialAssetPath)),
                Is.True);
            Assert.That(
                plan.SafeColliders.Count(collider =>
                    collider.ColliderType == "MeshCollider" &&
                    collider.Convex),
                Is.EqualTo(78),
                "The donor source-convex evidence must remain audited.");
            Assert.That(
                plan.SafeColliders.All(collider =>
                    !collider.SourceHasRigidbodyInAncestry),
                Is.True,
                "Donor Rigidbody descendants must not become frozen static " +
                "world blockers.");
            Assert.That(
                plan.ColliderDispositions.Count(record =>
                    record.SourceHasRigidbodyInAncestry &&
                    record.Disposition ==
                    "ExcludedDynamicRequiresPresenter"),
                Is.EqualTo(108));
            Assert.That(
                plan.ColliderDispositions.Count(record =>
                    record.SourceHasRigidbodyInAncestry),
                Is.EqualTo(
                    DonorWorldCellizationPlan
                        .ExpectedSourceRigidbodyAncestryColliderCount),
                "All source Rigidbody ancestry evidence must remain " +
                "exported even when an earlier semantic exclusion wins.");
            Assert.That(
                plan.ColliderDispositions.Single(record =>
                    record.ColliderStableId ==
                    "a66a36c2b13188e125848cbb6c51e4ff")
                    .Disposition,
                Is.EqualTo("ExcludedDynamicRequiresPresenter"));
            Assert.That(
                plan.ColliderDispositions.Single(record =>
                    record.ColliderStableId ==
                    "01edf1c72b36d5862cddb4294bf25e1a")
                    .Disposition,
                Is.EqualTo("ExcludedDynamicRequiresPresenter"));
            Assert.That(
                plan.ColliderDispositions.Single(record =>
                    record.ColliderStableId ==
                    "1eb95efde812a3d7e245026c86bf4b1a")
                    .Disposition,
                Is.EqualTo("ExcludedWeatherShelterVolume"));
            Assert.That(
                plan.SafeColliders
                    .Where(collider =>
                        collider.ColliderType == "MeshCollider")
                    .All(collider => !collider.RuntimeConvex),
                Is.True,
                "Static world meshes must remain non-convex at runtime so " +
                "PhysX does not silently reject meshes above its convex " +
                "cooking limit.");
            Assert.That(
                plan.SafeColliders.Single(collider =>
                    collider.ColliderStableId ==
                    "db8f7c3ab9163a68b6d7cb01bf32f415")
                    .SemanticCategory,
                Is.EqualTo("BuildingInterior"),
                "The sauna indoor walls token must not be classified as a door.");
            Assert.That(
                plan.SafeColliders
                    .Where(collider =>
                        DonorWorldSolidCollisionPolicy
                            .ReviewedStaticVehicleObstacleIds.Contains(
                                collider.ColliderStableId))
                    .Select(collider => collider.ColliderStableId),
                Is.EquivalentTo(
                    DonorWorldSolidCollisionPolicy
                        .ReviewedStaticVehicleObstacleIds),
                "Reviewed frozen wrecks and parked vehicles without a donor " +
                "Rigidbody must remain physical static obstacles.");
            Assert.That(
                plan.ColliderDispositions
                    .Where(record => record.Disposition ==
                        "ExcludedVehicle")
                    .All(record =>
                        record.SourceHasRigidbodyInAncestry),
                Is.True,
                "The seven dynamic vehicle rows must remain owned by the " +
                "future project vehicle presenters.");
            Assert.That(
                plan.ColliderDispositions.Any(record =>
                    record.Disposition ==
                    "ExcludedDoorRequiresBinding"),
                Is.True);
            Assert.That(
                plan.ColliderDispositions.Any(record =>
                    record.Disposition ==
                    "ExcludedBuiltinMeshRequiresMapping"),
                Is.True);
            Assert.That(
                plan.ColliderDispositions
                    .Where(record =>
                        record.Disposition is
                            "ExcludedDoorRequiresBinding" or
                            "ExcludedActorOrPlayer" or
                            "ExcludedVehicle" or
                            "ExcludedDisabled" or
                            "ExcludedTrigger")
                    .Any(record => record.IsIncluded),
                Is.False);
            Assert.That(
                LayerMask.NameToLayer(
                    DonorWorldSolidCollisionPolicy.WorldSurfaceLayer),
                Is.GreaterThanOrEqualTo(0));
            Assert.That(
                LayerMask.NameToLayer(
                    DonorWorldSolidCollisionPolicy.WorldSolidLayer),
                Is.GreaterThanOrEqualTo(0));
            Assert.That(
                AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(
                    DonorWorldSolidCollisionPolicy
                        .WorldSurfacePhysicsMaterial),
                Is.Not.Null);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(
                    DonorWorldSolidCollisionPolicy
                        .WorldSolidPhysicsMaterial),
                Is.Not.Null);
        }

        [Test]
        public void MaterialTexturePlan_IsDeterministicAndCoversFrozenRendererClosure()
        {
            DonorWorldMaterialTexturePlan first =
                DonorWorldMaterialTexturePlan.Load();
            DonorWorldMaterialTexturePlan second =
                DonorWorldMaterialTexturePlan.Load();

            Assert.That(
                first.RendererEntries.Count,
                Is.EqualTo(
                    DonorWorldMaterialTexturePlan
                        .ExpectedRendererCount));
            Assert.That(
                first.RendererEntries.Sum(entry =>
                    entry.SourceMaterialGuids.Length),
                Is.EqualTo(
                    DonorWorldMaterialTexturePlan
                        .ExpectedDeclaredMaterialSlotCount));
            Assert.That(
                first.Materials.Count,
                Is.EqualTo(
                    DonorWorldMaterialTexturePlan
                        .ExpectedResolvedMaterialCount));
            Assert.That(
                first.Textures.Count,
                Is.EqualTo(
                    DonorWorldMaterialTexturePlan
                        .ExpectedTextureConversionCount));
            Assert.That(
                first.Textures.Values.Count(texture =>
                    texture.IsImported),
                Is.EqualTo(
                    DonorWorldMaterialTexturePlan
                        .ExpectedImportedTextureConversionCount));
            Assert.That(
                first.Textures.Values.Count(texture =>
                    texture.Role ==
                    DonorWorldTextureRole.DetailNormalPacked),
                Is.EqualTo(8));
            Assert.That(
                first.Textures.Values.Count(texture =>
                    texture.Role ==
                    DonorWorldTextureRole.DetailAlbedoPacked),
                Is.EqualTo(5));
            Assert.That(
                first.PresentationFingerprintSha256,
                Is.EqualTo(
                    second.PresentationFingerprintSha256));
            Assert.That(
                first.PresentationFingerprintSha256,
                Has.Length.EqualTo(64));
            Assert.That(
                first.RendererEntries.Count(entry =>
                    entry.SourceMaterialGuids.Contains(
                        DonorWorldMaterialTexturePlan
                            .BuiltInFallbackGuid,
                        StringComparer.Ordinal)),
                Is.EqualTo(
                    DonorWorldMaterialTexturePlan
                        .ExpectedBuiltInFallbackRendererCount));
        }

        [Test]
        public void GeneratedAuditReports_Declare08A1CandidateMaterialGate()
        {
            string visualReport = File.ReadAllText(
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    WorldBaseline06B2Paths.VisualCompletenessReport));
            Assert.That(
                visualReport,
                Does.Contain(
                    "Automated structural status: **PASS (candidate only)**."));
            Assert.That(
                visualReport,
                Does.Contain(
                    "Human visual acceptance: **PENDING after deterministic regeneration**."));
            Assert.That(
                visualReport,
                Does.Contain(
                    "06B3 gate: **GO / accepted; milestone not started**."));
            Assert.That(
                visualReport,
                Does.Contain(
                    "walking and cross-cell character relocation"));
            Assert.That(
                visualReport,
                Does.Contain(
                    "rather than a dedicated vehicle drive"));
            Assert.That(
                visualReport,
                Does.Not.Contain("Pending human review"));
            Assert.That(
                visualReport,
                Does.Not.Contain("06B3 gate: **NO-GO"));

            string shaderMapping = File.ReadAllText(
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    WorldBaseline06B2Paths.MaterialShaderMapping));
            Assert.That(
                shaderMapping,
                Does.Contain("Known differences and review disposition"));
            Assert.That(
                shaderMapping,
                Does.Contain(
                    DonorWorldMaterialTexturePipeline
                        .CompatibilityPolicyVersion));
            Assert.That(
                shaderMapping,
                Does.Contain("HDMaterial.ValidateMaterial"));
            Assert.That(
                shaderMapping,
                Does.Contain("R=luminance"));
            Assert.That(
                shaderMapping,
                Does.Contain("accepted temporary visual debt"));
            Assert.That(
                shaderMapping,
                Does.Not.Contain("remains Pending visual review"));
        }

        [Test]
        public void FreezeOutputs_RevisionAndDebtSchemasAreHumanAccepted()
        {
            Assert.That(
                WorldBaseline06B3Paths.RequiredOutputFiles.Count,
                Is.EqualTo(9));
            Assert.That(
                WorldBaseline06B3Paths.RequiredOutputFiles,
                Does.Contain(
                    WorldBaseline06B3Paths.BaselineRevision));
            Assert.That(
                WorldBaseline06B3Paths.RequiredOutputFiles,
                Does.Not.Contain(
                    WorldBaseline06B3Paths.FullMapValidationResult));

            DonorWorldBaselineFreezeValidationResult result =
                DonorWorldBaselineFreezeValidator
                    .ValidateOutputContracts();
            Assert.That(
                result.Passed,
                Is.True,
                string.Join(" | ", result.Errors));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(
                result.Status,
                Is.EqualTo(WorldBaseline06B3Paths.Frozen));
            Assert.That(
                result.ManualVehicleTraversalStatus,
                Is.EqualTo(
                    WorldBaseline06B3Paths
                        .PassedHumanAccepted));
            Assert.That(
                result.BaselineRevisionId,
                Is.EqualTo(
                    WorldBaseline06B3Paths.BaselineRevisionId));
            Assert.That(
                result.SourceRevisionId,
                Is.EqualTo(WorldBaselinePaths.SourceRevisionId));
            Assert.That(
                result.ActiveWorldProfileId,
                Is.EqualTo(WorldBaseline06B2Paths.ProfileId));
            Assert.That(
                result.DebtCatalogueRevision,
                Is.EqualTo(
                    WorldBaseline06B3Paths.DebtCatalogueRevision));
            Assert.That(result.TraversalRowCount, Is.GreaterThan(0));
            Assert.That(result.DebtRowCount, Is.GreaterThan(0));
            Assert.That(
                result.RequiredOutputFileHashes.Count,
                Is.EqualTo(9));
            Assert.That(
                result.RequiredOutputFileHashes.Select(record =>
                    record.path),
                Is.EqualTo(
                    WorldBaseline06B3Paths.RequiredOutputFiles));
            Assert.That(
                result.RequiredOutputFileHashes.All(record =>
                    record.lengthBytes > 0 &&
                    record.sha256.Length == 64 &&
                    record.sha256.All(character =>
                        Uri.IsHexDigit(character) &&
                        !char.IsUpper(character)) &&
                    string.Equals(
                        record.sha256,
                        DonorWorldBaselineFreezeValidator
                            .ComputeFileSha256(record.path),
                        StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                result.RevisionValidationResultSha256,
                Is.EqualTo(
                    WorldBaseline06B3Paths
                        .FirstExportHashPlaceholder));
        }

        [Test]
        public void FreezeMachineReadableResult_IsDeterministicAndParseable()
        {
            DonorWorldBaselineFreezeValidationResult first =
                DonorWorldBaselineFreezeValidator
                    .ValidateOutputContracts();
            DonorWorldBaselineFreezeValidationResult second =
                DonorWorldBaselineFreezeValidator
                    .ValidateOutputContracts();
            Assert.That(
                first.Passed,
                Is.True,
                string.Join(" | ", first.Errors));
            Assert.That(
                second.Passed,
                Is.True,
                string.Join(" | ", second.Errors));

            string firstJson =
                DonorWorldBaselineFreezeValidator
                    .BuildMachineReadableJson(first);
            string secondJson =
                DonorWorldBaselineFreezeValidator
                    .BuildMachineReadableJson(second);
            Assert.That(secondJson, Is.EqualTo(firstJson));
            Assert.That(
                DonorWorldBaselineFreezeValidator
                    .ComputeUtf8Sha256(secondJson),
                Is.EqualTo(
                    DonorWorldBaselineFreezeValidator
                        .ComputeUtf8Sha256(firstJson)));
            Assert.That(
                DonorWorldBaselineFreezeValidator
                    .TryValidateMachineReadableJson(
                        firstJson,
                        out string parseError),
                Is.True,
                parseError);
            Assert.That(
                firstJson,
                Does.Contain(
                    "\"status\": \"Frozen\""));
            Assert.That(
                firstJson,
                Does.Contain(
                    "\"manualVehicleTraversalStatus\": " +
                    "\"PassedHumanAccepted\""));
            Assert.That(
                firstJson,
                Does.Not.Contain(
                    "\"manualVehicleTraversalStatus\": " +
                    "\"PendingManualVehicleTraversal\""));

            string mixedStateJson = firstJson.Replace(
                "\"status\": \"Frozen\"",
                "\"status\": \"PendingManualVehicleTraversal\"");
            Assert.That(
                DonorWorldBaselineFreezeValidator
                    .TryValidateMachineReadableJson(
                        mixedStateJson,
                        out string mixedStateError),
                Is.False);
            Assert.That(
                mixedStateError,
                Does.Contain("inconsistent freeze"));
        }

        [Test]
        public void OwnershipManifest_UsesEffectiveSubmeshMaterialSlots()
        {
            DonorWorldCellizationPlan plan =
                DonorWorldCellizationPlan.Load();
            IReadOnlyDictionary<long, int[]> staticBatchSubsets =
                WorldStaticBatchSubsetTable.ParseCommittedTable();
            string manifestPath =
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    WorldBaseline06B2Paths.OwnershipManifest);
            string[] lines = File.ReadAllLines(manifestPath);
            List<string> headers =
                WorldEntityTable.ParseRow(lines[0]);
            int idIndex = headers.IndexOf("LegacyWorldObjectId");
            int sourceSlotsIndex =
                headers.IndexOf("SourceMaterialGuids");
            int materialPathsIndex =
                headers.IndexOf("LegacyTexturedMaterialPaths");
            Assert.That(idIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(sourceSlotsIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(materialPathsIndex, Is.GreaterThanOrEqualTo(0));

            Dictionary<string, List<string>> rows = lines
                .Skip(1)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(WorldEntityTable.ParseRow)
                .ToDictionary(
                    row => row[idIndex],
                    StringComparer.Ordinal);
            var expandedHierarchyPaths =
                new HashSet<string>(StringComparer.Ordinal);

            foreach (DonorWorldCellizationAssignment assignment in
                     plan.Assignments.Where(value =>
                         value.SanitationEntry.IncludeRenderer))
            {
                WorldBaselineSanitationEntry entry =
                    assignment.SanitationEntry;
                string meshPath =
                    staticBatchSubsets.ContainsKey(
                        entry.Placement.SourceObjectId)
                        ? WorldBaselinePaths.DerivedMeshRoot + "/" +
                          entry.Placement.StableId + ".asset"
                        : WorldBaselinePaths.SourceMeshRoot + "/" +
                          entry.Placement.MeshGuid + ".asset";
                Mesh mesh =
                    AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                Assert.That(
                    mesh,
                    Is.Not.Null,
                    entry.Placement.HierarchyPath);

                string[] effectiveSlots =
                    DonorWorldMaterialTexturePlan
                        .ResolveSourceMaterialSlots(
                            entry,
                            mesh.subMeshCount);
                Assert.That(
                    rows.TryGetValue(
                        entry.Placement.StableId,
                        out List<string> row),
                    Is.True,
                    entry.Placement.HierarchyPath);
                Assert.That(
                    row[sourceSlotsIndex],
                    Is.EqualTo(string.Join(";", effectiveSlots)),
                    entry.Placement.HierarchyPath);
                Assert.That(
                    row[materialPathsIndex],
                    Is.EqualTo(string.Join(
                        ";",
                        effectiveSlots.Select(materialGuid =>
                            string.Equals(
                                materialGuid,
                                DonorWorldMaterialTexturePlan
                                    .BuiltInFallbackGuid,
                                StringComparison.Ordinal)
                                ? WorldBaselinePaths.UnsupportedMaterial
                                : WorldBaselinePaths.TexturedMaterial(
                                    materialGuid)))),
                    entry.Placement.HierarchyPath);

                if (entry.SourceMaterialGuids.Length == 1 &&
                    effectiveSlots.Length == 2)
                {
                    Assert.That(
                        effectiveSlots[0],
                        Is.EqualTo(effectiveSlots[1]),
                        entry.Placement.HierarchyPath);
                    expandedHierarchyPaths.Add(
                        entry.Placement.HierarchyPath);
                }
            }

            Assert.That(
                expandedHierarchyPaths.Any(path =>
                    path.EndsWith(
                        "/ModemCordIn",
                        StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                expandedHierarchyPaths.Any(path =>
                    path.EndsWith(
                        "/ModemCordOut",
                        StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                expandedHierarchyPaths.Any(path =>
                    path.EndsWith(
                        "/drumset",
                        StringComparison.Ordinal)),
                Is.True);
            Assert.That(
                expandedHierarchyPaths.Any(path =>
                    path.EndsWith(
                        "/386_monitor(xxxxx)",
                        StringComparison.Ordinal)),
                Is.True);
        }

        [Test]
        public void GeneratedPresentation_UsesSharedHdrpAssetsAndNoDonorShaderPayload()
        {
            DonorWorldMaterialTexturePlan plan =
                DonorWorldMaterialTexturePlan.Load();
            DonorWorldMaterialTextureAssets assets =
                DonorWorldMaterialTexturePipeline.LoadGenerated(plan);

            Assert.That(
                assets.TexturedMaterials.Count,
                Is.EqualTo(plan.Materials.Count));
            Assert.That(
                assets.ConvertedTextures.Count,
                Is.EqualTo(
                    DonorWorldMaterialTexturePlan
                        .ExpectedImportedTextureConversionCount));
            Assert.That(
                assets.TexturedMaterials.Values.All(material =>
                    material != null &&
                    material.shader != null &&
                    (material.shader.name == "HDRP/Lit" ||
                     material.shader.name == "HDRP/Unlit") &&
                    material.enableInstancing &&
                    !material.name.EndsWith(
                        " (Instance)",
                        StringComparison.Ordinal)),
                Is.True);

            Texture asphaltDetailNormal = assets.GetConvertedTexture(
                "60f4c9c6d4d87fc4a9ce0e4f1ce91ff2",
                DonorWorldTextureRole.DetailNormalPacked);
            AssertPackedDetailNormalChannels(
                plan.GetTexture(
                    "60f4c9c6d4d87fc4a9ce0e4f1ce91ff2",
                    DonorWorldTextureRole.DetailNormalPacked));
            AssertPackedDetailNormal(
                assets.GetTexturedMaterial(
                    "e4f7ddfffd5f47947966d4f2906b4b20"),
                asphaltDetailNormal,
                new Vector2(10f, 6f),
                0.5f,
                "ASPHALT");
            AssertPackedDetailNormal(
                assets.GetTexturedMaterial(
                    "f6fbfb9cfb87d3844afaf03acfca5fb2"),
                asphaltDetailNormal,
                new Vector2(100f, 60f),
                0.5f,
                "PAVEMENT");
            Texture roadDetailAlbedo = assets.GetConvertedTexture(
                "ae9770b97d98f9a4393059c619799111",
                DonorWorldTextureRole.DetailAlbedoPacked);
            AssertPackedDetailAlbedoChannels(
                plan.GetTexture(
                    "ae9770b97d98f9a4393059c619799111",
                    DonorWorldTextureRole.DetailAlbedoPacked));
            AssertPackedDetailAlbedo(
                assets.GetTexturedMaterial(
                    "bee65a18eecc0bc409361e160d6b1aca"),
                roadDetailAlbedo,
                new Vector2(3f, 1f),
                "DIRTROAD");
            Texture terrainDetailAlbedo = assets.GetConvertedTexture(
                "666f9dd8a5e12da439fb91ec437c7283",
                DonorWorldTextureRole.DetailAlbedoPacked);
            AssertPackedDetailAlbedo(
                assets.GetTexturedMaterial(
                    "da5bc03c62a0f174197555e90357aac9"),
                terrainDetailAlbedo,
                new Vector2(800f, 500f),
                "TERRAIN");
            AssertTemporaryWater(
                plan.GetMaterial(
                    "bc4c85b54ac6054428d76e49914801cc"),
                assets.GetTexturedMaterial(
                    "bc4c85b54ac6054428d76e49914801cc"),
                0.2901961f,
                "Water4Adv_Lake");
            AssertTemporaryWater(
                plan.GetMaterial(
                    "3e22b6aef244d364e9ca2a6e194891aa"),
                assets.GetTexturedMaterial(
                    "3e22b6aef244d364e9ca2a6e194891aa"),
                0.5058824f,
                "Water4Simple");

            string runtimeRoot =
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    WorldBaselinePaths.RuntimeRoot);
            Assert.That(
                System.IO.Directory.EnumerateFiles(
                        runtimeRoot,
                        "*.shader",
                        System.IO.SearchOption.AllDirectories),
                Is.Empty);

            DonorWorldCellizationValidationResult validation =
                DonorWorldCellizationValidator.Validate(
                    verifySourceHashes: false,
                    inspectAllGeneratedScenes: true);
            Assert.That(
                validation.Passed,
                Is.True,
                string.Join(" | ", validation.Errors));
            Assert.That(
                validation.GeneratedMaterialCount,
                Is.EqualTo(
                    DonorWorldMaterialTexturePlan
                        .ExpectedResolvedMaterialCount + 1));
            Assert.That(
                validation.GeneratedTextureCount,
                Is.EqualTo(
                    DonorWorldMaterialTexturePlan
                        .ExpectedImportedTextureConversionCount));
        }

        private static void AssertPackedDetailNormal(
            Material material,
            Texture expectedTexture,
            Vector2 expectedScale,
            float expectedStrength,
            string label)
        {
            Assert.That(material, Is.Not.Null, label);
            Assert.That(
                material.GetTexture("_DetailMap"),
                Is.SameAs(expectedTexture),
                label);
            Assert.That(
                Vector2.Distance(
                    material.GetTextureScale("_DetailMap"),
                    expectedScale),
                Is.LessThan(0.0001f),
                label);
            Assert.That(
                Vector2.Distance(
                    material.GetTextureOffset("_DetailMap"),
                    Vector2.zero),
                Is.LessThan(0.0001f),
                label);
            Assert.That(
                material.GetFloat("_DetailNormalScale"),
                Is.EqualTo(expectedStrength).Within(0.0001f),
                label);
            Assert.That(
                material.GetFloat("_DetailAlbedoScale"),
                Is.EqualTo(0f).Within(0.0001f),
                label);
            Assert.That(
                material.GetFloat("_DetailSmoothnessScale"),
                Is.EqualTo(0f).Within(0.0001f),
                label);
            Assert.That(
                material.GetFloat("_LinkDetailsWithBase"),
                Is.EqualTo(0f).Within(0.0001f),
                label);
            Assert.That(
                material.GetFloat("_UVDetail"),
                Is.EqualTo(0f).Within(0.0001f),
                label);
            Assert.That(
                material.GetColor("_UVDetailsMappingMask"),
                Is.EqualTo(new Color(1f, 0f, 0f, 0f)),
                label);
            Assert.That(
                material.GetTexture("_NormalMap"),
                Is.Null,
                label);
            Assert.That(
                material.IsKeywordEnabled("_DETAIL_MAP"),
                Is.True,
                label);
            Assert.That(
                material.IsKeywordEnabled("_NORMALMAP"),
                Is.True,
                label);
        }

        private static void AssertPackedDetailAlbedo(
            Material material,
            Texture expectedTexture,
            Vector2 expectedScale,
            string label)
        {
            Assert.That(material, Is.Not.Null, label);
            Assert.That(
                material.GetTexture("_DetailMap"),
                Is.SameAs(expectedTexture),
                label);
            Assert.That(
                Vector2.Distance(
                    material.GetTextureScale("_DetailMap"),
                    expectedScale),
                Is.LessThan(0.0001f),
                label);
            Assert.That(
                Vector2.Distance(
                    material.GetTextureOffset("_DetailMap"),
                    Vector2.zero),
                Is.LessThan(0.0001f),
                label);
            Assert.That(
                material.GetFloat("_DetailAlbedoScale"),
                Is.EqualTo(1f).Within(0.0001f),
                label);
            Assert.That(
                material.GetFloat("_DetailNormalScale"),
                Is.EqualTo(0f).Within(0.0001f),
                label);
            Assert.That(
                material.GetFloat("_DetailSmoothnessScale"),
                Is.EqualTo(0f).Within(0.0001f),
                label);
            Assert.That(
                material.IsKeywordEnabled("_DETAIL_MAP"),
                Is.True,
                label);
            Assert.That(
                material.IsKeywordEnabled("_NORMALMAP"),
                Is.True,
                label);
        }

        private static void AssertTemporaryWater(
            DonorWorldSourceMaterial source,
            Material material,
            float expectedAlpha,
            string label)
        {
            Assert.That(
                source.CompatibilityClass,
                Is.EqualTo(
                    DonorWorldCompatibilityClass.TemporaryWater),
                label + " compatibility class");
            Color donorBaseColor =
                source.GetColor("_BaseColor", Color.clear);
            Color expected = new Color(
                0.08f,
                0.22f,
                0.28f,
                Mathf.Clamp(donorBaseColor.a, 0.05f, 0.75f));
            Assert.That(
                expected.r,
                Is.EqualTo(0.08f).Within(0.0001f),
                label + " red");
            Assert.That(
                expected.g,
                Is.EqualTo(0.22f).Within(0.0001f),
                label + " green");
            Assert.That(
                expected.b,
                Is.EqualTo(0.28f).Within(0.0001f),
                label + " blue");
            Assert.That(
                expected.a,
                Is.EqualTo(expectedAlpha).Within(0.0001f),
                label + " alpha");
            Color actual = material.GetColor("_BaseColor");
            Assert.That(
                actual.r,
                Is.EqualTo(expected.r).Within(0.0001f),
                label + " generated red");
            Assert.That(
                actual.g,
                Is.EqualTo(expected.g).Within(0.0001f),
                label + " generated green");
            Assert.That(
                actual.b,
                Is.EqualTo(expected.b).Within(0.0001f),
                label + " generated blue");
            Assert.That(
                actual.a,
                Is.EqualTo(expected.a).Within(0.0001f),
                label + " generated alpha");
            Assert.That(
                material.GetTexture("_BaseColorMap"),
                Is.Null,
                label + " generated base map");
        }

        private static void AssertPackedDetailNormalChannels(
            DonorWorldTextureConversion conversion)
        {
            var source = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true);
            var packed = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true);
            try
            {
                Assert.That(
                    ImageConversion.LoadImage(
                        source,
                        File.ReadAllBytes(
                            conversion.SourceAbsolutePath),
                        markNonReadable: false),
                    Is.True);
                Assert.That(
                    ImageConversion.LoadImage(
                        packed,
                        File.ReadAllBytes(
                            WorldBaselinePaths
                                .ToAbsoluteProjectPath(
                                    conversion.GeneratedAssetPath)),
                        markNonReadable: false),
                    Is.True);
                Assert.That(packed.width, Is.EqualTo(source.width));
                Assert.That(packed.height, Is.EqualTo(source.height));

                Color32[] sourcePixels = source.GetPixels32();
                Color32[] packedPixels = packed.GetPixels32();
                int[] samples =
                {
                    0,
                    sourcePixels.Length / 2,
                    sourcePixels.Length - 1
                };
                foreach (int index in samples.Distinct())
                {
                    Assert.That(packedPixels[index].r, Is.EqualTo(128));
                    Assert.That(
                        packedPixels[index].g,
                        Is.EqualTo(sourcePixels[index].g));
                    Assert.That(packedPixels[index].b, Is.EqualTo(128));
                    Assert.That(
                        packedPixels[index].a,
                        Is.EqualTo(sourcePixels[index].r));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(packed);
            }
        }

        private static void AssertPackedDetailAlbedoChannels(
            DonorWorldTextureConversion conversion)
        {
            var source = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true);
            var packed = new Texture2D(
                2,
                2,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true);
            try
            {
                Assert.That(
                    ImageConversion.LoadImage(
                        source,
                        File.ReadAllBytes(
                            conversion.SourceAbsolutePath),
                        markNonReadable: false),
                    Is.True);
                Assert.That(
                    ImageConversion.LoadImage(
                        packed,
                        File.ReadAllBytes(
                            WorldBaselinePaths
                                .ToAbsoluteProjectPath(
                                    conversion.GeneratedAssetPath)),
                        markNonReadable: false),
                    Is.True);
                Assert.That(packed.width, Is.EqualTo(source.width));
                Assert.That(packed.height, Is.EqualTo(source.height));

                Color32[] sourcePixels = source.GetPixels32();
                Color32[] packedPixels = packed.GetPixels32();
                int[] samples =
                {
                    0,
                    sourcePixels.Length / 2,
                    sourcePixels.Length - 1
                };
                foreach (int index in samples.Distinct())
                {
                    Assert.That(
                        packedPixels[index].r,
                        Is.EqualTo(
                            DonorWorldMaterialTexturePipeline
                                .GetPackedDetailAlbedoLuminance(
                                    sourcePixels[index])));
                    Assert.That(packedPixels[index].g, Is.EqualTo(128));
                    Assert.That(packedPixels[index].b, Is.EqualTo(128));
                    Assert.That(packedPixels[index].a, Is.EqualTo(128));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                UnityEngine.Object.DestroyImmediate(packed);
            }
        }
    }

    public sealed class DonorWorldMaterialCompatibilityPolicyEditModeTests
    {
        [Test]
        public void SurfaceDetailAllowlist_ResolvesEveryFrozenMaterialGuid()
        {
            DonorWorldMaterialTexturePlan plan =
                DonorWorldMaterialTexturePlan.Load();
            IReadOnlyCollection<string> allowlist =
                DonorWorldMaterialTexturePipeline
                    .LegacyDiffuseDetailSurfaceGuids;

            Assert.That(allowlist.Count, Is.EqualTo(9));
            Assert.That(
                allowlist.Where(guid =>
                    !plan.Materials.ContainsKey(guid)),
                Is.Empty,
                "Every shader-compatibility allowlist GUID must resolve " +
                "against the frozen material source set.");
        }

        [Test]
        public void ShaderAwarePolicy_DoesNotPromoteLegacyOrSpecularFloats()
        {
            DonorWorldSourceMaterial legacy = CreateSource(
                "legacy",
                "Legacy Shaders/Bumped Diffuse",
                DonorWorldCompatibilityClass.OpaqueLit,
                metallic: 1f,
                smoothness: 0.9f);
            DonorWorldSourceMaterial specular = CreateSource(
                "specular",
                "Standard (Specular setup)",
                DonorWorldCompatibilityClass.OpaqueLit,
                metallic: 0.9f,
                smoothness: 0.9f);
            DonorWorldSourceMaterial standard = CreateSource(
                "standard",
                "Standard",
                DonorWorldCompatibilityClass.OpaqueLit,
                metallic: 0.75f,
                smoothness: 0.9f);

            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedMetallic(legacy),
                Is.Zero);
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedSmoothness(legacy),
                Is.EqualTo(0.18f).Within(0.0001f));
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedMetallic(specular),
                Is.Zero);
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedSmoothness(specular),
                Is.EqualTo(0.35f).Within(0.0001f));
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedMetallic(standard),
                Is.EqualTo(0.75f).Within(0.0001f));
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedSmoothness(standard),
                Is.EqualTo(0.55f).Within(0.0001f));
        }

        [Test]
        public void SurfaceDetailPolicy_IsFrozenGuidAndShaderBound()
        {
            DonorWorldSourceMaterial dirtRoad = CreateSource(
                "bee65a18eecc0bc409361e160d6b1aca",
                "Legacy Shaders/Diffuse Detail",
                DonorWorldCompatibilityClass.OpaqueLit,
                metallic: 1f,
                smoothness: 0.9f,
                detailTextureGuid:
                    "ae9770b97d98f9a4393059c619799111");
            DonorWorldSourceMaterial unrelated = CreateSource(
                "unrelated",
                "Legacy Shaders/Diffuse Detail",
                DonorWorldCompatibilityClass.OpaqueLit,
                metallic: 1f,
                smoothness: 0.9f,
                detailTextureGuid:
                    "ae9770b97d98f9a4393059c619799111");

            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .TryGetExpectedDetailTexture(
                        dirtRoad,
                        out DonorWorldTextureEnvironment texture,
                        out DonorWorldTextureRole role),
                Is.True);
            Assert.That(
                texture.TextureGuid,
                Is.EqualTo("ae9770b97d98f9a4393059c619799111"));
            Assert.That(
                role,
                Is.EqualTo(DonorWorldTextureRole.DetailAlbedoPacked));
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedDetailAlbedoScale(dirtRoad),
                Is.EqualTo(1f));
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedSmoothness(dirtRoad),
                Is.EqualTo(0.12f).Within(0.0001f));
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .TryGetExpectedDetailAlbedoTexture(
                        unrelated,
                        out _),
                Is.False);
        }

        [Test]
        public void FoliageAndEmissionPolicy_IsBoundedAndHdrpConsistent()
        {
            DonorWorldSourceMaterial foliage = CreateSource(
                "foliage",
                "Nature/Tree Creator Leaves",
                DonorWorldCompatibilityClass.AlphaClipLit,
                metallic: 1f,
                smoothness: 1f,
                doubleSided: true);
            DonorWorldSourceMaterial emissive = CreateSource(
                "emissive",
                "Car/LightsEmmissive",
                DonorWorldCompatibilityClass.EmissiveLit,
                metallic: 1f,
                smoothness: 1f,
                emission: new Color(4f, 2f, 1f, 1f));

            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedMetallic(foliage),
                Is.Zero);
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedSmoothness(foliage),
                Is.EqualTo(0.08f).Within(0.0001f));
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedDoubleSidedNormalMode(foliage),
                Is.EqualTo(0f));
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedDoubleSidedConstants(foliage),
                Is.EqualTo(new Vector4(-1f, -1f, -1f, 0f)));
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedMetallic(emissive),
                Is.Zero);
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetExpectedSmoothness(emissive),
                Is.EqualTo(0.2f).Within(0.0001f));
            Color boundedEmission =
                DonorWorldMaterialTexturePipeline
                    .GetExpectedEmissionColor(emissive);
            Assert.That(
                Mathf.Max(
                    boundedEmission.r,
                    Mathf.Max(
                        boundedEmission.g,
                        boundedEmission.b)),
                Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void DetailAlbedoLuminance_UsesDeterministicPerceptualWeights()
        {
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetPackedDetailAlbedoLuminance(
                        new Color32(255, 0, 0, 255)),
                Is.EqualTo(54));
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetPackedDetailAlbedoLuminance(
                        new Color32(0, 255, 0, 255)),
                Is.EqualTo(182));
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetPackedDetailAlbedoLuminance(
                        new Color32(0, 0, 255, 255)),
                Is.EqualTo(19));
            Assert.That(
                DonorWorldMaterialTexturePipeline
                    .GetPackedDetailAlbedoLuminance(
                        new Color32(128, 128, 128, 255)),
                Is.EqualTo(128));
        }

        private static DonorWorldSourceMaterial CreateSource(
            string guid,
            string shader,
            DonorWorldCompatibilityClass compatibilityClass,
            float metallic,
            float smoothness,
            bool doubleSided = false,
            string detailTextureGuid = null,
            Color? emission = null)
        {
            var textures =
                new Dictionary<string, DonorWorldTextureEnvironment>(
                    StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(detailTextureGuid))
            {
                textures.Add(
                    "_Detail",
                    new DonorWorldTextureEnvironment(
                        "_Detail",
                        detailTextureGuid,
                        new Vector2(3f, 1f),
                        Vector2.zero));
            }
            var colors = new Dictionary<string, Color>(
                StringComparer.Ordinal);
            if (emission.HasValue)
            {
                colors.Add("_EmissionColor", emission.Value);
            }

            return new DonorWorldSourceMaterial(
                guid,
                "test/" + guid + ".mat",
                "test/" + guid + ".mat",
                new string('0', 64),
                guid,
                "shader-guid",
                shader,
                "test.shader",
                new string('1', 64),
                string.Empty,
                -1,
                textures,
                new Dictionary<string, DonorWorldTextureTransform>(
                    StringComparer.Ordinal),
                new Dictionary<string, float>(StringComparer.Ordinal)
                {
                    { "_Metallic", metallic },
                    { "_Glossiness", smoothness }
                },
                colors,
                compatibilityClass,
                doubleSided);
        }
    }
}
