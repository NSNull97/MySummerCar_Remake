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
        public void GeneratedAuditReports_PreserveAccepted06B2Gate()
        {
            string visualReport = File.ReadAllText(
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    WorldBaseline06B2Paths.VisualCompletenessReport));
            Assert.That(
                visualReport,
                Does.Contain("Automated status: **PASS**."));
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
    }
}
