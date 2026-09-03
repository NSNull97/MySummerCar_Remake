using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MSC.Editor.WorldTransfer;
using MSC.LegacyImport;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldBaseline
{
    public sealed class DonorWorldBaselineValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public int EntityCount { get; internal set; }
        public int RendererCount { get; internal set; }
        public int MetadataOnlyCount { get; internal set; }
        public int ColliderCount { get; internal set; }
        public int SourceFileCount { get; internal set; }
        public string SemanticFingerprintSha256 { get; internal set; } =
            string.Empty;
        public string SourceFilesFingerprintSha256 { get; internal set; } =
            string.Empty;
        public string RuntimePayloadFingerprintSha256 { get; internal set; } =
            string.Empty;
        public bool IsValid => Errors.Count == 0;
    }

    public static class DonorWorldBaselineValidator
    {
        private const float PositionTolerance = 0.0001f;
        private const float RotationToleranceDegrees = 0.001f;
        private const string MetadataRuntimeSource =
            "Assets/Game/LegacyImport/Runtime/" +
            "DonorWorldBaselineEntityMetadata.cs";
        private const string ItemPresentationRuntimeRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/Items";
        private const string GameplayPresentationRuntimeRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/GameplayPresentation";
        private const string PlayerViewmodelRuntimeRoot =
            GameplayPresentationRuntimeRoot + "/PlayerViewmodel";
        private const string CharacterPresentationRuntimeRoot =
            "Assets/Game/LegacyImport/RuntimeBaseline/Characters";

        private static readonly HashSet<string> AllowedComponentTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                typeof(Transform).FullName,
                typeof(MeshFilter).FullName,
                typeof(MeshRenderer).FullName,
                typeof(Light).FullName,
                typeof(DonorWorldBaselineSceneMetadata).FullName,
                typeof(DonorWorldBaselineEntityMetadata).FullName,
                "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData",
                "UnityEngine.Rendering.HighDefinition.HDAdditionalShadowData"
            };

        private static readonly string[] ForbiddenDependencyFragments =
        {
            "/PlayMaker",
            "/Assembly-CSharp",
            "/MSCLoader",
            "/Steamworks",
            "/ES2",
            "/Assets/Plugins/"
        };

        private static readonly string[] DonorHierarchyRootLiterals =
        {
            "\"MAP/",
            "\"YARD/",
            "\"STORE/",
            "\"PERAJARVI/",
            "\"REPAIRSHOP/",
            "\"CABIN/",
            "\"COTTAGE/"
        };

        public static DonorWorldBaselineValidationResult Validate(
            bool verifySourceHashes)
        {
            var result = new DonorWorldBaselineValidationResult();
            IReadOnlyList<WorldBaselineSanitationEntry> plan;
            DonorWorldBaselineSourceManifestData manifest;
            try
            {
                plan = WorldBaselineSanitationPlan.Load();
                manifest = DonorWorldBaselineManifest.Read();
            }
            catch (Exception exception)
            {
                result.Errors.Add(exception.Message);
                return result;
            }

            ValidateManifest(manifest, plan, verifySourceHashes, result);
            ValidateGeneratedPayloadBoundary(plan, manifest, result);
            ValidateBuildIsolation(result);
            ValidateGameplayHierarchyIsolation(result);

            string sceneAbsolutePath = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaselinePaths.CanonicalScene);
            if (!File.Exists(sceneAbsolutePath) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    WorldBaselinePaths.CanonicalScene) == null)
            {
                result.Errors.Add(
                    "Canonical donor world baseline scene is missing or not imported: " +
                    WorldBaselinePaths.CanonicalScene);
                return result;
            }

            ValidateSceneDependencies(result);

            Scene scene = SceneManager.GetSceneByPath(
                WorldBaselinePaths.CanonicalScene);
            bool openedForValidation = !scene.IsValid() || !scene.isLoaded;
            Scene previousActiveScene = SceneManager.GetActiveScene();
            try
            {
                if (openedForValidation)
                {
                    scene = EditorSceneManager.OpenScene(
                        WorldBaselinePaths.CanonicalScene,
                        OpenSceneMode.Additive);
                }

                if (SceneManager.GetActiveScene() != scene &&
                    !SceneManager.SetActiveScene(scene))
                {
                    result.Errors.Add(
                        "Canonical baseline could not be made active for " +
                        "scene-environment validation.");
                }
                ValidateScene(scene, plan, manifest, result);
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "Canonical baseline scene could not be inspected: " +
                    exception.Message);
            }
            finally
            {
                if (previousActiveScene.IsValid() &&
                    previousActiveScene.isLoaded &&
                    previousActiveScene != scene)
                {
                    SceneManager.SetActiveScene(previousActiveScene);
                }
                if (openedForValidation && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }

            result.Warnings.Add(
                "Runtime collision count is intentionally zero in 06B1; " +
                "safe collider transfer and traversal validation belong to 06B2.");
            result.Warnings.Add(
                "Temporary category materials are diagnostic presentation only; " +
                "donor textures and final HDRP materials are not reconstructed.");
            return result;
        }

        public static void RunBatch()
        {
            DonorWorldBaselineValidationResult result =
                Validate(verifySourceHashes: true);
            foreach (string warning in result.Warnings)
            {
                Debug.LogWarning("DONOR_WORLD_BASELINE_WARNING " + warning);
            }

            if (!result.IsValid)
            {
                throw new InvalidOperationException(
                    "Donor world baseline validation failed:\n- " +
                    string.Join("\n- ", result.Errors));
            }

            Debug.Log(
                "DONOR_WORLD_BASELINE_VALIDATION_OK " +
                $"entities={result.EntityCount} renderers={result.RendererCount} " +
                $"metadataOnly={result.MetadataOnlyCount} " +
                $"colliders={result.ColliderCount} " +
                $"sourceFiles={result.SourceFileCount} " +
                $"semanticFingerprint={result.SemanticFingerprintSha256} " +
                $"sourceFingerprint={result.SourceFilesFingerprintSha256} " +
                $"payloadFingerprint={result.RuntimePayloadFingerprintSha256}");
        }

        private static void ValidateManifest(
            DonorWorldBaselineSourceManifestData manifest,
            IReadOnlyList<WorldBaselineSanitationEntry> plan,
            bool verifySourceHashes,
            DonorWorldBaselineValidationResult result)
        {
            RequireEqual(result, "manifest schema", manifest.schemaVersion, 1);
            RequireEqual(
                result, "manifest ID", manifest.manifestId,
                "donor-world-baseline-source");
            RequireEqual(
                result, "source revision", manifest.sourceRevisionId,
                WorldBaselinePaths.SourceRevisionId);
            RequireEqual(
                result, "classification", manifest.classification,
                "TemporaryDirectImport");
            RequireEqual(
                result, "activation state", manifest.activationState,
                "PreparedNotActiveUntil06B2");
            RequireEqual(
                result, "canonical scene path", manifest.canonicalScenePath,
                WorldBaselinePaths.CanonicalScene);
            RequireEqual(
                result, "generated payload boundary",
                manifest.generatedPayloadBoundary, WorldBaselinePaths.RuntimeRoot);
            RequireEqual(
                result, "extracted scene hash",
                manifest.extractedSceneSha256,
                WorldBaselinePaths.SourceSceneSha256);
            RequireEqual(
                result, "sanitation policy version",
                manifest.sanitationPolicyVersion,
                WorldBaselineSanitationPlan.PolicyVersion);

            DonorWorldBaselineSanitationCounts counts = manifest.counts;
            if (counts == null)
            {
                result.Errors.Add("Source manifest has no sanitation counts.");
            }
            else
            {
                RequireEqual(
                    result, "eligible entity count",
                    counts.eligibleEntityCount, plan.Count);
                RequireEqual(
                    result, "renderer-accepted count",
                    counts.rendererAcceptedCount,
                    plan.Count(entry => entry.IncludeRenderer));
                RequireEqual(
                    result, "metadata-only count",
                    counts.metadataOnlyCount,
                    plan.Count(entry => !entry.IncludeRenderer));
                RequireEqual(
                    result, "source activeSelf entity count",
                    counts.sourceActiveSelfEntityCount,
                    plan.Count(entry => entry.SourceActiveSelf));
                RequireEqual(
                    result, "source inactiveSelf entity count",
                    counts.sourceInactiveSelfEntityCount,
                    plan.Count(entry => !entry.SourceActiveSelf));
                RequireEqual(
                    result, "effective active entity count",
                    counts.effectiveActiveEntityCount,
                    plan.Count(entry => entry.EffectiveActive));
                RequireEqual(
                    result, "effective inactive entity count",
                    counts.effectiveInactiveEntityCount,
                    plan.Count(entry => !entry.EffectiveActive));
                RequireEqual(
                    result, "active renderer count",
                    counts.activeRendererCount,
                    plan.Count(entry =>
                        entry.IncludeRenderer && entry.EffectiveActive));
                RequireEqual(
                    result, "inactive renderer count",
                    counts.inactiveRendererCount,
                    plan.Count(entry =>
                        entry.IncludeRenderer && !entry.EffectiveActive));
                RequireEqual(
                    result, "missing-mesh metadata-only count",
                    counts.metadataOnlyMissingMeshCount,
                    plan.Count(entry =>
                        entry.Reason == "NoUsableMeshGuid"));
                RequireEqual(
                    result, "skinned metadata-only count",
                    counts.metadataOnlySkinnedMeshCount,
                    plan.Count(entry =>
                        entry.Reason == "SkinnedMeshRendererExcluded"));
                RequireEqual(
                    result, "character-hierarchy metadata-only count",
                    counts.metadataOnlyCharacterHierarchyCount,
                    plan.Count(entry =>
                        entry.Reason == "CharacterHierarchyExcluded"));
                RequireEqual(
                    result, "runtime collider count",
                    counts.runtimeColliderCount, 0);
                RequireEqual(
                    result, "partition cell count",
                    counts.partitionCellCount, 49);
                RequireEqual(
                    result, "global entity count",
                    counts.globalEntityCount,
                    plan.Count(entry =>
                        entry.Placement.CellId == "global"));
            }

            ValidateCoordinateContract(manifest, result);
            ValidatePrototypeCellDisposition(manifest, result);
            ValidateSourceFileRecords(
                manifest, plan, verifySourceHashes, result);
        }

        private static void ValidateCoordinateContract(
            DonorWorldBaselineSourceManifestData manifest,
            DonorWorldBaselineValidationResult result)
        {
            DonorWorldBaselineCoordinateRecord coordinate =
                manifest.coordinateSystem;
            if (coordinate == null)
            {
                result.Errors.Add("Source manifest has no coordinate contract.");
                return;
            }

            RequireVector(
                result, "source-to-project translation",
                coordinate.sourceToProjectTranslation,
                new Vector3(169.98f, 1.611f, -1040.625f));
            RequireQuaternion(
                result, "source-to-project rotation",
                coordinate.sourceToProjectRotation, Quaternion.identity);
            RequireVector(
                result, "source-to-project scale",
                coordinate.sourceToProjectScale, Vector3.one);
            RequireVector(
                result, "canonical root position",
                coordinate.canonicalSceneRootPosition, Vector3.zero);
            RequireQuaternion(
                result, "canonical root rotation",
                coordinate.canonicalSceneRootRotation, Quaternion.identity);
            RequireVector(
                result, "canonical root scale",
                coordinate.canonicalSceneRootScale, Vector3.one);

            DonorWorldBaselineBoundsRecord bounds = manifest.mapBounds;
            if (bounds == null)
            {
                result.Errors.Add("Source manifest has no map bounds.");
                return;
            }

            RequireVector(
                result, "source bounds min", bounds.sourceMin,
                new Vector3(-3163.1853f, -414.44f, -2254.6733f));
            RequireVector(
                result, "source bounds max", bounds.sourceMax,
                new Vector3(3383.1853f, 911.76f, 3638.5908f));
            RequireVector(
                result, "converted bounds min", bounds.convertedMin,
                new Vector3(-2993.2053f, -412.829f, -3295.2983f));
            RequireVector(
                result, "converted bounds max", bounds.convertedMax,
                new Vector3(3553.1653f, 913.371f, 2597.9658f));
        }

        private static void ValidatePrototypeCellDisposition(
            DonorWorldBaselineSourceManifestData manifest,
            DonorWorldBaselineValidationResult result)
        {
            DonorWorldBaselinePrototypeCellRecord[] cells =
                manifest.prototypeCells ??
                Array.Empty<DonorWorldBaselinePrototypeCellRecord>();
            string[] expectedIds = { "cell_0_-3", "cell_0_-2" };
            foreach (string expectedId in expectedIds)
            {
                DonorWorldBaselinePrototypeCellRecord record = cells.SingleOrDefault(
                    cell => string.Equals(
                        cell.cellId, expectedId, StringComparison.Ordinal));
                if (record == null)
                {
                    result.Errors.Add(
                        "Prototype disposition is missing for " + expectedId);
                    continue;
                }

                if (!record.disposition.Contains(
                        "PrototypeOnly", StringComparison.Ordinal) ||
                    !record.disposition.Contains(
                        "RejectedForFidelity", StringComparison.Ordinal) ||
                    !string.Equals(
                        record.featureParityProfileState,
                        "Inactive",
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Prototype cell disposition is unsafe for " + expectedId);
                }
            }
        }

        private static void ValidateSourceFileRecords(
            DonorWorldBaselineSourceManifestData manifest,
            IReadOnlyList<WorldBaselineSanitationEntry> plan,
            bool verifySourceHashes,
            DonorWorldBaselineValidationResult result)
        {
            DonorWorldBaselineSourceFileRecord[] records =
                manifest.sourceFiles ??
                Array.Empty<DonorWorldBaselineSourceFileRecord>();
            result.SourceFileCount = records.Length;
            if (records.Length == 0)
            {
                result.Errors.Add("Source manifest contains no source files.");
                return;
            }

            IReadOnlyDictionary<long, int[]> subsets =
                WorldStaticBatchSubsetTable.ParseCommittedTable();
            int expectedMeshGuidCount = plan
                .Where(entry => entry.IncludeRenderer)
                .Select(entry => entry.Placement.MeshGuid)
                .Distinct(StringComparer.Ordinal)
                .Count();
            int expectedDerivedMeshCount = plan.Count(entry =>
                entry.IncludeRenderer &&
                subsets.ContainsKey(entry.Placement.SourceObjectId));
            int expectedRecordCount =
                2 + 12 + 6 +
                expectedMeshGuidCount * 2 +
                expectedDerivedMeshCount;
            RequireEqual(
                result, "source manifest record count",
                records.Length, expectedRecordCount);
            RequireRoleCount(
                result, records, "CanonicalExtractedScene", 1);
            RequireRoleCount(
                result, records, "AssetRipperPathIdMap", 1);
            RequireRoleCount(
                result, records, "NormalizedWorldManifest", 12);
            RequireRoleCount(
                result, records, "ProjectOwnedFrozenTransferInput", 6);
            RequireRoleCount(
                result, records, "WhitelistedMeshAsset",
                expectedMeshGuidCount);
            RequireRoleCount(
                result, records, "WhitelistedMeshImporterMetadata",
                expectedMeshGuidCount);
            RequireRoleCount(
                result, records, "AuditedDerivedStaticBatchMesh",
                expectedDerivedMeshCount);

            string internalFingerprint =
                DonorWorldBaselineManifest.ComputeFileSetFingerprint(records);
            if (!string.Equals(
                    internalFingerprint,
                    manifest.sourceFilesFingerprintSha256,
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    "Source-file manifest fingerprint does not match its records.");
            }

            var uniquePaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (DonorWorldBaselineSourceFileRecord record in records)
            {
                if (Path.IsPathRooted(record.relativePath) ||
                    record.relativePath.Contains("\\", StringComparison.Ordinal) ||
                    record.relativePath.Split('/').Any(
                        part => part == string.Empty ||
                                part == "." ||
                                part == ".."))
                {
                    result.Errors.Add(
                        "Unsafe source manifest path: " + record.relativePath);
                }

                if (!uniquePaths.Add(record.rootId + "|" + record.relativePath))
                {
                    result.Errors.Add(
                        "Duplicate source manifest record: " +
                        record.rootId + "|" + record.relativePath);
                }
            }

            DonorWorldBaselineSourceFileRecord[] canonicalScenes = records
                .Where(record =>
                    record.role == "CanonicalExtractedScene")
                .ToArray();
            if (canonicalScenes.Length != 1 ||
                !string.Equals(
                    canonicalScenes[0].sha256,
                    WorldBaselinePaths.SourceSceneSha256,
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    "Canonical extracted GAME.unity source record is missing " +
                    "or has the wrong hash.");
            }

            if (!verifySourceHashes)
            {
                result.SourceFilesFingerprintSha256 = internalFingerprint;
                return;
            }

            WorldTransferEditorConfiguration configuration;
            try
            {
                configuration = WorldTransferEditorConfiguration.Load();
            }
            catch (Exception exception)
            {
                result.Errors.Add(exception.Message);
                return;
            }

            var actualRecords =
                new List<DonorWorldBaselineSourceFileRecord>(records.Length);
            foreach (DonorWorldBaselineSourceFileRecord record in records)
            {
                string root;
                switch (record.rootId)
                {
                    case "rawExtraction":
                        root = configuration.RawExtractionPath;
                        break;
                    case "normalizedData":
                        root = configuration.NormalizedDataPath;
                        break;
                    case "unityProject":
                        root = WorldTransferPaths.ProjectRoot;
                        break;
                    default:
                        result.Errors.Add(
                            "Unknown source manifest root ID: " + record.rootId);
                        continue;
                }

                string absolutePath;
                try
                {
                    absolutePath = SafeCombine(root, record.relativePath);
                }
                catch (Exception exception)
                {
                    result.Errors.Add(exception.Message);
                    continue;
                }

                if (!File.Exists(absolutePath))
                {
                    result.Errors.Add(
                        "Locked source file is missing: " +
                        record.rootId + "/" + record.relativePath);
                    continue;
                }

                var info = new FileInfo(absolutePath);
                string hash =
                    DonorWorldBaselineManifest.ComputeFileSha256(absolutePath);
                if (info.Length != record.lengthBytes)
                {
                    result.Errors.Add(
                        "Locked source length drift: " +
                        record.rootId + "/" + record.relativePath);
                }
                if (!string.Equals(
                        hash, record.sha256, StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Locked source hash drift: " +
                        record.rootId + "/" + record.relativePath);
                }

                actualRecords.Add(new DonorWorldBaselineSourceFileRecord
                {
                    rootId = record.rootId,
                    relativePath = record.relativePath,
                    lengthBytes = info.Length,
                    sha256 = hash,
                    role = record.role
                });
            }

            string actualFingerprint =
                DonorWorldBaselineManifest.ComputeFileSetFingerprint(
                    actualRecords);
            result.SourceFilesFingerprintSha256 = actualFingerprint;
            if (actualRecords.Count == records.Length &&
                !string.Equals(
                    actualFingerprint,
                    manifest.sourceFilesFingerprintSha256,
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    "Current canonical source file set fingerprint drifted.");
            }
        }

        private static void ValidateGeneratedPayloadBoundary(
            IReadOnlyList<WorldBaselineSanitationEntry> plan,
            DonorWorldBaselineSourceManifestData manifest,
            DonorWorldBaselineValidationResult result)
        {
            string absoluteRoot = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaselinePaths.RuntimeRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                result.Errors.Add(
                    "RuntimeBaseline payload boundary does not exist.");
                return;
            }

            var allowedExtensions = new HashSet<string>(
                new[] { ".asset", ".mat", ".unity", ".meta" },
                StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.EnumerateFiles(
                         absoluteRoot, "*", SearchOption.AllDirectories))
            {
                if (string.Equals(
                        Path.GetFileName(file),
                        ".gitkeep",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                string extension = Path.GetExtension(file);
                string projectRelativePath =
                    ToProjectRelativePath(file);
                if (IsItemPresentationAsset(projectRelativePath))
                {
                    // Phase 1 gameplay presentation is governed by the Items
                    // manifest/validator and is intentionally outside the
                    // canonical world-payload fingerprint. The world boundary
                    // still rejects executable or misplaced payload here.
                    if (!IsAllowedItemPresentationFile(
                            projectRelativePath,
                            extension))
                    {
                        result.Errors.Add(
                            "Forbidden file type inside item RuntimeBaseline: " +
                            projectRelativePath);
                    }

                    continue;
                }

                if (IsGameplayPresentationAsset(projectRelativePath))
                {
                    // Section 6.5 explicitly permits a sanitized, private
                    // gameplay-presentation baseline. Keep this allowlist
                    // narrower than the general world payload: no scripts,
                    // controllers, shaders, assemblies or arbitrary assets.
                    if (!IsAllowedGameplayPresentationFile(
                            projectRelativePath,
                            extension))
                    {
                        result.Errors.Add(
                            "Forbidden file type inside gameplay presentation " +
                            "RuntimeBaseline: " + projectRelativePath);
                    }

                    continue;
                }

                if (IsCharacterPresentationAsset(projectRelativePath))
                {
                    // Section 6.5 also permits bounded private character
                    // presentation. Its dedicated manifest/importer owns
                    // provenance and semantic validation; the world validator
                    // retains a strict passive-asset allowlist here.
                    if (!IsAllowedCharacterPresentationFile(
                            projectRelativePath,
                            extension))
                    {
                        result.Errors.Add(
                            "Forbidden file type inside character " +
                            "RuntimeBaseline: " + projectRelativePath);
                    }

                    continue;
                }

                bool isPresentationTexture =
                    projectRelativePath.StartsWith(
                        WorldBaselinePaths.TextureRoot + "/",
                        StringComparison.Ordinal) &&
                    extension.Equals(
                        ".png",
                        StringComparison.OrdinalIgnoreCase);
                if (!allowedExtensions.Contains(extension) &&
                    !isPresentationTexture)
                {
                    result.Errors.Add(
                        "Forbidden file type inside RuntimeBaseline: " +
                        projectRelativePath);
                }
            }

            IReadOnlyDictionary<long, int[]> subsets =
                WorldStaticBatchSubsetTable.ParseCommittedTable();
            var expectedAssets = new HashSet<string>(
                StringComparer.Ordinal)
            {
                WorldBaselinePaths.CanonicalScene
            };
            foreach (WorldBaselineSanitationEntry entry in
                     plan.Where(entry => entry.IncludeRenderer))
            {
                string meshPath = subsets.ContainsKey(
                    entry.Placement.SourceObjectId)
                    ? WorldBaselinePaths.DerivedMeshRoot + "/" +
                      entry.Placement.StableId + ".asset"
                    : WorldBaselinePaths.SourceMeshRoot + "/" +
                      entry.Placement.MeshGuid + ".asset";
                expectedAssets.Add(meshPath);
                expectedAssets.Add(
                    WorldBaselinePaths.CategoryMaterial(
                        entry.Placement.Category));
            }

            string[] actualAssets = Directory.EnumerateFiles(
                    absoluteRoot, "*", SearchOption.AllDirectories)
                .Where(file => !ToProjectRelativePath(file).StartsWith(
                    WorldBaseline06B2Paths.StreamingRoot + "/",
                    StringComparison.Ordinal))
                .Where(file => !ToProjectRelativePath(file).StartsWith(
                    WorldBaselinePaths.TexturedMaterialRoot + "/",
                    StringComparison.Ordinal))
                .Where(file => !ToProjectRelativePath(file).StartsWith(
                    WorldBaselinePaths.TextureRoot + "/",
                    StringComparison.Ordinal))
                .Where(file => !IsItemPresentationAsset(
                    ToProjectRelativePath(file)))
                .Where(file => !IsGameplayPresentationAsset(
                    ToProjectRelativePath(file)))
                .Where(file => !IsCharacterPresentationAsset(
                    ToProjectRelativePath(file)))
                .Where(file =>
                {
                    string extension = Path.GetExtension(file);
                    return extension.Equals(
                               ".asset",
                               StringComparison.OrdinalIgnoreCase) ||
                           extension.Equals(
                               ".mat",
                               StringComparison.OrdinalIgnoreCase) ||
                           extension.Equals(
                               ".unity",
                               StringComparison.OrdinalIgnoreCase);
                })
                .Select(ToProjectRelativePath)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            var actualSet = actualAssets.ToHashSet(StringComparer.Ordinal);
            foreach (string missing in expectedAssets.Except(
                         actualSet, StringComparer.Ordinal).Take(20))
            {
                result.Errors.Add(
                    "Expected sanitized baseline asset is missing: " + missing);
            }
            foreach (string stale in actualSet.Except(
                         expectedAssets, StringComparer.Ordinal).Take(20))
            {
                result.Errors.Add(
                    "Unexpected or stale sanitized baseline asset: " + stale);
            }
            RequireEqual(
                result, "sanitized generated asset count",
                actualSet.Count, expectedAssets.Count);

            try
            {
                string payloadFingerprint =
                    DonorWorldBaselineManifest
                        .ComputeRuntimePayloadFingerprint(plan);
                result.RuntimePayloadFingerprintSha256 = payloadFingerprint;
                if (string.IsNullOrWhiteSpace(
                        manifest.runtimePayloadFingerprintSha256) ||
                    !string.Equals(
                        payloadFingerprint,
                        manifest.runtimePayloadFingerprintSha256,
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Generated runtime-baseline payload fingerprint drifted.");
                }
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "Generated runtime-baseline payload fingerprint could not " +
                    "be verified: " + exception.Message);
            }

            string gitIgnorePath =
                WorldBaselinePaths.ToAbsoluteProjectPath(".gitignore");
            string gitIgnore = File.ReadAllText(gitIgnorePath);
            if (!gitIgnore.Contains(
                    "Assets/Game/LegacyImport/RuntimeBaseline/**",
                    StringComparison.Ordinal) ||
                !gitIgnore.Contains(
                    "!Assets/Game/LegacyImport/RuntimeBaseline/.gitkeep",
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    "RuntimeBaseline generated payload is not correctly " +
                    "isolated in .gitignore.");
            }
        }

        private static bool IsItemPresentationAsset(string projectRelativePath)
        {
            return projectRelativePath.StartsWith(
                ItemPresentationRuntimeRoot + "/",
                StringComparison.Ordinal);
        }

        private static bool IsAllowedItemPresentationFile(
            string projectRelativePath,
            string extension)
        {
            if (extension.Equals(".meta", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return
                (projectRelativePath.StartsWith(
                     ItemPresentationRuntimeRoot + "/Meshes/",
                     StringComparison.Ordinal) &&
                 extension.Equals(".asset", StringComparison.OrdinalIgnoreCase)) ||
                (projectRelativePath.StartsWith(
                     ItemPresentationRuntimeRoot + "/Materials/",
                     StringComparison.Ordinal) &&
                 extension.Equals(".mat", StringComparison.OrdinalIgnoreCase)) ||
                (projectRelativePath.StartsWith(
                     ItemPresentationRuntimeRoot + "/Prefabs/",
                     StringComparison.Ordinal) &&
                 extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsGameplayPresentationAsset(
            string projectRelativePath)
        {
            return projectRelativePath.StartsWith(
                GameplayPresentationRuntimeRoot + "/",
                StringComparison.Ordinal);
        }

        private static bool IsCharacterPresentationAsset(
            string projectRelativePath)
        {
            return projectRelativePath.StartsWith(
                CharacterPresentationRuntimeRoot + "/",
                StringComparison.Ordinal);
        }

        private static bool IsAllowedCharacterPresentationFile(
            string projectRelativePath,
            string extension)
        {
            if (extension.Equals(".meta", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return
                (projectRelativePath.StartsWith(
                     CharacterPresentationRuntimeRoot + "/Source/Mesh/",
                     StringComparison.Ordinal) &&
                 extension.Equals(".asset", StringComparison.OrdinalIgnoreCase)) ||
                (projectRelativePath.StartsWith(
                     CharacterPresentationRuntimeRoot +
                     "/Source/AnimationClip/",
                     StringComparison.Ordinal) &&
                 extension.Equals(".anim", StringComparison.OrdinalIgnoreCase)) ||
                (projectRelativePath.StartsWith(
                     CharacterPresentationRuntimeRoot + "/Generated/",
                     StringComparison.Ordinal) &&
                 extension.Equals(".mat", StringComparison.OrdinalIgnoreCase)) ||
                (projectRelativePath.StartsWith(
                     CharacterPresentationRuntimeRoot +
                     "/Resources/Phase1Characters/",
                     StringComparison.Ordinal) &&
                 (extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase) ||
                  extension.Equals(".asset", StringComparison.OrdinalIgnoreCase))) ||
                (string.Equals(
                     projectRelativePath,
                     CharacterPresentationRuntimeRoot +
                     "/Phase1CharacterPresentationBuildReport.json",
                     StringComparison.Ordinal) &&
                 extension.Equals(".json", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsAllowedGameplayPresentationFile(
            string projectRelativePath,
            string extension)
        {
            if (extension.Equals(".meta", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return
                (projectRelativePath.StartsWith(
                     PlayerViewmodelRuntimeRoot + "/Source/Mesh/",
                     StringComparison.Ordinal) &&
                 extension.Equals(".asset", StringComparison.OrdinalIgnoreCase)) ||
                (projectRelativePath.StartsWith(
                     PlayerViewmodelRuntimeRoot + "/Source/Textures/",
                     StringComparison.Ordinal) &&
                 extension.Equals(".png", StringComparison.OrdinalIgnoreCase)) ||
                (projectRelativePath.StartsWith(
                     PlayerViewmodelRuntimeRoot + "/Source/AnimationClips/",
                     StringComparison.Ordinal) &&
                 extension.Equals(".anim", StringComparison.OrdinalIgnoreCase)) ||
                (projectRelativePath.StartsWith(
                     PlayerViewmodelRuntimeRoot + "/Generated/",
                     StringComparison.Ordinal) &&
                 extension.Equals(".mat", StringComparison.OrdinalIgnoreCase)) ||
                (projectRelativePath.StartsWith(
                     PlayerViewmodelRuntimeRoot +
                     "/Resources/Phase1PlayerViewmodel/",
                     StringComparison.Ordinal) &&
                 extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase)) ||
                (string.Equals(
                     projectRelativePath,
                     PlayerViewmodelRuntimeRoot +
                     "/Phase1PlayerViewmodelBuildReport.json",
                     StringComparison.Ordinal) &&
                 extension.Equals(".json", StringComparison.OrdinalIgnoreCase));
        }

        private static void ValidateBuildIsolation(
            DonorWorldBaselineValidationResult result)
        {
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.path.StartsWith(
                        WorldBaselinePaths.RuntimeRoot + "/",
                        StringComparison.Ordinal) &&
                    !IsAllowed06B2StreamingScene(scene.path))
                {
                    result.Errors.Add(
                        "06B1 baseline scene must not be present in Build Settings: " +
                        scene.path);
                }

                if (!scene.enabled)
                {
                    continue;
                }

                string[] dependencies =
                    AssetDatabase.GetDependencies(scene.path, recursive: true);
                foreach (string dependency in dependencies)
                {
                    if (dependency.StartsWith(
                            WorldBaselinePaths.RuntimeRoot + "/",
                            StringComparison.Ordinal) &&
                        !(IsAllowed06B2StreamingScene(scene.path) &&
                          !string.Equals(
                              dependency,
                              WorldBaselinePaths.CanonicalScene,
                              StringComparison.Ordinal)))
                    {
                        result.Errors.Add(
                            "Enabled build scene depends on inactive 06B1 " +
                            "RuntimeBaseline content: " + scene.path);
                    }
                }
            }

            string[] consumerAssets = AssetDatabase.GetAllAssetPaths()
                .Where(path =>
                    path.StartsWith("Assets/Game/", StringComparison.Ordinal) &&
                    !path.StartsWith(
                        WorldBaselinePaths.RuntimeRoot + "/",
                        StringComparison.Ordinal) &&
                    !AssetDatabase.IsValidFolder(path))
                .ToArray();
            string[] leakedDependencies = AssetDatabase.GetDependencies(
                    consumerAssets, recursive: true)
                .Where(path => path.StartsWith(
                    WorldBaselinePaths.RuntimeRoot + "/",
                    StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            foreach (string leaked in leakedDependencies)
            {
                result.Errors.Add(
                    "Project asset outside RuntimeBaseline depends on inactive " +
                    "baseline payload: " + leaked);
            }
        }

        private static bool IsAllowed06B2StreamingScene(string scenePath) =>
            string.Equals(
                scenePath,
                WorldBaseline06B2Paths.GlobalScene,
                StringComparison.Ordinal) ||
            scenePath.StartsWith(
                WorldBaseline06B2Paths.StreamingCellSceneRoot + "/",
                StringComparison.Ordinal);

        private static void ValidateGameplayHierarchyIsolation(
            DonorWorldBaselineValidationResult result)
        {
            string gameRoot = WorldBaselinePaths.ToAbsoluteProjectPath(
                "Assets/Game");
            string metadataSourceAbsolute =
                WorldBaselinePaths.ToAbsoluteProjectPath(MetadataRuntimeSource);
            foreach (string file in Directory.EnumerateFiles(
                         gameRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.Contains("/Editor/", StringComparison.Ordinal) ||
                    normalized.Contains("/Tests/", StringComparison.Ordinal) ||
                    string.Equals(
                        Path.GetFullPath(file),
                        Path.GetFullPath(metadataSourceAbsolute),
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string source = File.ReadAllText(file);
                if (source.Contains(
                        "SourceHierarchyPath",
                        StringComparison.Ordinal) ||
                    source.Contains(
                        "sourceHierarchyPath",
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Runtime code consumes donor hierarchy metadata: " +
                        ToProjectRelativePath(file));
                }

                bool performsNameLookup =
                    source.Contains("GameObject.Find(", StringComparison.Ordinal) ||
                    source.Contains(".transform.Find(", StringComparison.Ordinal) ||
                    source.Contains("Transform.Find(", StringComparison.Ordinal);
                if (!performsNameLookup)
                {
                    continue;
                }

                foreach (string donorRoot in DonorHierarchyRootLiterals)
                {
                    if (source.Contains(donorRoot, StringComparison.Ordinal))
                    {
                        result.Errors.Add(
                            "Runtime code performs a donor hierarchy-name lookup: " +
                            ToProjectRelativePath(file));
                        break;
                    }
                }
            }
        }

        private static void ValidateSceneDependencies(
            DonorWorldBaselineValidationResult result)
        {
            string[] dependencies = AssetDatabase.GetDependencies(
                WorldBaselinePaths.CanonicalScene, recursive: true);
            foreach (string dependency in dependencies)
            {
                string normalized = "/" + dependency.Replace('\\', '/');
                if (dependency.StartsWith(
                        WorldTransferPaths.ReferenceRoot + "/",
                        StringComparison.Ordinal) ||
                    dependency.StartsWith(
                        "Assets/Game/Imported/DonorGenerated/",
                        StringComparison.Ordinal) ||
                    dependency.StartsWith(
                        "Assets/Game/World/Production/",
                        StringComparison.Ordinal) ||
                    dependency.StartsWith(
                        "Assets/Game/World/Generated/ProductionCells/",
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Canonical baseline has a forbidden donor/reference/" +
                        "prototype dependency: " + dependency);
                }

                if (ForbiddenDependencyFragments.Any(fragment =>
                        normalized.Contains(
                            fragment, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Errors.Add(
                        "Canonical baseline has a forbidden runtime dependency: " +
                        dependency);
                }
            }
        }

        private static void ValidateScene(
            Scene scene,
            IReadOnlyList<WorldBaselineSanitationEntry> plan,
            DonorWorldBaselineSourceManifestData manifest,
            DonorWorldBaselineValidationResult result)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                result.Errors.Add("Canonical baseline scene did not load.");
                return;
            }

            if (!string.Equals(
                    scene.path,
                    WorldBaselinePaths.CanonicalScene,
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    "Loaded canonical scene path does not match the locked path.");
            }

            GameObject[] roots = scene.GetRootGameObjects();
            if (roots.Length != 1)
            {
                result.Errors.Add(
                    "Canonical baseline scene must have exactly one root object.");
                return;
            }

            GameObject root = roots[0];
            if (!string.Equals(
                    root.name,
                    "DONOR_WORLD_BASELINE_TEMPORARY_DIRECT_IMPORT",
                    StringComparison.Ordinal))
            {
                result.Errors.Add("Canonical baseline root name is unexpected.");
            }
            RequireTransform(
                result, "canonical baseline root", root.transform,
                Vector3.zero, Quaternion.identity, Vector3.one);
            if (!root.activeSelf || !root.activeInHierarchy)
            {
                result.Errors.Add(
                    "Canonical baseline root must be active.");
            }

            string[] requiredChildren =
            {
                "SANITIZED_STATIC_RENDER_GEOMETRY",
                "EXCLUDED_SOURCE_METADATA_ONLY",
                "PROJECT_OWNED_DEVELOPMENT_LIGHTING"
            };
            RequireEqual(
                result, "canonical direct child count",
                root.transform.childCount, requiredChildren.Length);
            var directChildren = Enumerable.Range(
                    0, root.transform.childCount)
                .Select(index => root.transform.GetChild(index))
                .ToArray();
            foreach (string duplicateName in directChildren
                         .GroupBy(child => child.name, StringComparer.Ordinal)
                         .Where(group => group.Count() > 1)
                         .Select(group => group.Key))
            {
                result.Errors.Add(
                    "Duplicate canonical direct child: " + duplicateName);
            }
            foreach (string childName in requiredChildren)
            {
                Transform child = directChildren.FirstOrDefault(
                    candidate => string.Equals(
                        candidate.name, childName, StringComparison.Ordinal));
                if (child == null)
                {
                    result.Errors.Add(
                        "Canonical baseline root is missing child: " + childName);
                    continue;
                }

                RequireTransform(
                    result, childName, child,
                    Vector3.zero, Quaternion.identity, Vector3.one);
                if (!child.gameObject.activeSelf ||
                    !child.gameObject.activeInHierarchy)
                {
                    result.Errors.Add(
                        "Canonical container must be active: " + childName);
                }
            }

            ValidateComponentWhitelist(root, result);

            Transform geometryRoot = directChildren.FirstOrDefault(child =>
                child.name == "SANITIZED_STATIC_RENDER_GEOMETRY");
            Transform metadataOnlyRoot = directChildren.FirstOrDefault(child =>
                child.name == "EXCLUDED_SOURCE_METADATA_ONLY");
            Transform lightingRoot = directChildren.FirstOrDefault(child =>
                child.name == "PROJECT_OWNED_DEVELOPMENT_LIGHTING");
            int expectedRendererCount =
                plan.Count(entry => entry.IncludeRenderer);
            if (geometryRoot != null)
            {
                RequireEqual(
                    result, "geometry direct entity count",
                    geometryRoot.childCount, expectedRendererCount);
            }
            if (metadataOnlyRoot != null)
            {
                RequireEqual(
                    result, "metadata-only direct entity count",
                    metadataOnlyRoot.childCount,
                    plan.Count - expectedRendererCount);
            }
            if (lightingRoot != null)
            {
                RequireEqual(
                    result, "lighting direct child count",
                    lightingRoot.childCount, 1);
            }

            Transform[] allTransforms =
                root.GetComponentsInChildren<Transform>(includeInactive: true);
            RequireEqual(
                result, "canonical transform count",
                allTransforms.Length, plan.Count + 5);

            DonorWorldBaselineSceneMetadata[] sceneMetadata =
                root.GetComponentsInChildren<DonorWorldBaselineSceneMetadata>(
                    includeInactive: true);
            if (sceneMetadata.Length != 1)
            {
                result.Errors.Add(
                    "Canonical baseline must have exactly one scene metadata component.");
                return;
            }

            DonorWorldBaselineSceneMetadata stamp = sceneMetadata[0];
            if (stamp.gameObject != root)
            {
                result.Errors.Add(
                    "Scene metadata must be attached to the canonical root.");
            }
            RequireEqual(
                result, "scene source revision",
                stamp.SourceRevisionId, WorldBaselinePaths.SourceRevisionId);
            RequireEqual(
                result, "scene source hash",
                stamp.SourceSceneSha256, WorldBaselinePaths.SourceSceneSha256);
            if (stamp.Classification !=
                DonorWorldBaselineClassification.TemporaryDirectImport)
            {
                result.Errors.Add(
                    "Scene metadata classification is not TemporaryDirectImport.");
            }
            if (stamp.ActivationState !=
                DonorWorldBaselineActivationState.PreparedNotActiveUntil06B2)
            {
                result.Errors.Add(
                    "Canonical baseline was activated before 06B2.");
            }
            RequireVector(
                result, "scene source translation",
                stamp.SourceToProjectTranslation,
                new Vector3(169.98f, 1.611f, -1040.625f));
            RequireQuaternion(
                result, "scene source rotation",
                stamp.SourceToProjectRotation, Quaternion.identity);
            RequireVector(
                result, "scene source scale",
                stamp.SourceToProjectScale, Vector3.one);

            DonorWorldBaselineEntityMetadata[] entities =
                root.GetComponentsInChildren<DonorWorldBaselineEntityMetadata>(
                    includeInactive: true);
            MeshFilter[] meshFilters =
                root.GetComponentsInChildren<MeshFilter>(includeInactive: true);
            MeshRenderer[] renderers =
                root.GetComponentsInChildren<MeshRenderer>(includeInactive: true);
            Collider[] colliders =
                root.GetComponentsInChildren<Collider>(includeInactive: true);
            Light[] lights =
                root.GetComponentsInChildren<Light>(includeInactive: true);

            result.EntityCount = entities.Length;
            result.RendererCount = renderers.Length;
            result.MetadataOnlyCount =
                entities.Count(entity => !entity.HasSanitizedRenderer);
            result.ColliderCount = colliders.Length;

            RequireEqual(
                result, "scene entity count", entities.Length, plan.Count);
            RequireEqual(
                result, "scene MeshFilter count", meshFilters.Length,
                plan.Count(entry => entry.IncludeRenderer));
            RequireEqual(
                result, "scene MeshRenderer count", renderers.Length,
                plan.Count(entry => entry.IncludeRenderer));
            RequireEqual(
                result, "scene metadata-only count",
                result.MetadataOnlyCount,
                plan.Count(entry => !entry.IncludeRenderer));
            RequireEqual(
                result, "scene collider count", colliders.Length, 0);
            RequireEqual(result, "development light count", lights.Length, 1);

            RequireEqual(
                result, "scene metadata source entity count",
                stamp.SourceEntityCount, entities.Length);
            RequireEqual(
                result, "scene metadata renderer entity count",
                stamp.RendererEntityCount, renderers.Length);
            RequireEqual(
                result, "scene metadata-only entity count",
                stamp.MetadataOnlyEntityCount, result.MetadataOnlyCount);

            if (RenderSettings.fog)
            {
                result.Errors.Add(
                    "Canonical baseline imports or enables fog.");
            }
            if (RenderSettings.skybox != null)
            {
                result.Errors.Add(
                    "Canonical baseline imports or assigns a skybox.");
            }
            if (RenderSettings.ambientMode != AmbientMode.Flat)
            {
                result.Errors.Add(
                    "Canonical baseline ambient mode is not the locked neutral flat mode.");
            }
            RequireColor(
                result,
                "canonical ambient light",
                RenderSettings.ambientLight,
                new Color(0.42f, 0.42f, 0.42f, 1f));
            if (Mathf.Abs(RenderSettings.reflectionIntensity) >
                PositionTolerance)
            {
                result.Errors.Add(
                    "Canonical baseline reflection intensity is not zero.");
            }
            if (root.GetComponentsInChildren<Camera>(true).Length != 0 ||
                root.GetComponentsInChildren<AudioListener>(true).Length != 0 ||
                root.GetComponentsInChildren<AudioSource>(true).Length != 0)
            {
                result.Errors.Add(
                    "Canonical baseline contains camera or audio components.");
            }

            if (lights.Length == 1)
            {
                Light light = lights[0];
                if (light.type != LightType.Directional ||
                    light.shadows != LightShadows.None ||
                    !ColorsEqual(
                        light.color,
                        new Color(1f, 0.98f, 0.94f, 1f)) ||
                    Mathf.Abs(light.intensity - 50000f) >
                    PositionTolerance ||
                    !string.Equals(
                        light.gameObject.name,
                        "Neutral_Directional_Light",
                        StringComparison.Ordinal) ||
                    lightingRoot == null ||
                    light.transform.parent != lightingRoot ||
                    !light.gameObject.activeSelf ||
                    !light.gameObject.activeInHierarchy)
                {
                    result.Errors.Add(
                        "Canonical baseline light is not the neutral project-owned " +
                        "development light.");
                }
                RequireTransform(
                    result,
                    "neutral development light",
                    light.transform,
                    Vector3.zero,
                    Quaternion.Euler(50f, -35f, 0f),
                    Vector3.one);
            }

            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(true))
            {
                foreach (Component component in
                         transform.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        continue;
                    }

                    string typeName = component.GetType().FullName ??
                                      component.GetType().Name;
                    if ((typeName ==
                         "UnityEngine.Rendering.HighDefinition.HDAdditionalLightData" ||
                         typeName ==
                         "UnityEngine.Rendering.HighDefinition.HDAdditionalShadowData") &&
                        (lights.Length != 1 ||
                         component.gameObject != lights[0].gameObject))
                    {
                        result.Errors.Add(
                            "HDRP additional light data exists outside the " +
                            "neutral development light.");
                    }
                }
            }

            ValidateEntities(
                entities,
                plan,
                geometryRoot,
                metadataOnlyRoot,
                result);

            string semanticFingerprint =
                DonorWorldBaselineFingerprint.Compute(root, entities);
            result.SemanticFingerprintSha256 = semanticFingerprint;
            if (!string.Equals(
                    semanticFingerprint,
                    stamp.SemanticFingerprintSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    semanticFingerprint,
                    manifest.semanticFingerprintSha256,
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    "Canonical scene semantic fingerprint drifted.");
            }
        }

        private static void ValidateComponentWhitelist(
            GameObject root,
            DonorWorldBaselineValidationResult result)
        {
            var unexpectedTypes = new Dictionary<string, string>(
                StringComparer.Ordinal);
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(
                         includeInactive: true))
            {
                foreach (Component component in
                         transform.gameObject.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        result.Errors.Add(
                            "Missing script component in canonical baseline at " +
                            GetHierarchyPath(transform));
                        continue;
                    }

                    string typeName = component.GetType().FullName ??
                                      component.GetType().Name;
                    if (!AllowedComponentTypes.Contains(typeName) &&
                        !unexpectedTypes.ContainsKey(typeName))
                    {
                        unexpectedTypes.Add(
                            typeName, GetHierarchyPath(transform));
                    }
                }

                if (transform.gameObject.CompareTag("EditorOnly"))
                {
                    result.Errors.Add(
                        "RuntimeBaseline object is tagged EditorOnly: " +
                        GetHierarchyPath(transform));
                }
            }

            foreach (KeyValuePair<string, string> unexpected in unexpectedTypes)
            {
                result.Errors.Add(
                    "Component type is outside the explicit 06B1 whitelist: " +
                    unexpected.Key + " at " + unexpected.Value);
            }
        }

        private static void ValidateEntities(
            IReadOnlyList<DonorWorldBaselineEntityMetadata> entities,
            IReadOnlyList<WorldBaselineSanitationEntry> plan,
            Transform geometryRoot,
            Transform metadataOnlyRoot,
            DonorWorldBaselineValidationResult result)
        {
            var expected = plan.ToDictionary(
                entry => entry.Placement.StableId, StringComparer.Ordinal);
            var stableIds = new HashSet<string>(StringComparer.Ordinal);
            var sourceObjectIds = new HashSet<long>();
            IReadOnlyDictionary<long, int[]> subsets =
                WorldStaticBatchSubsetTable.ParseCommittedTable();

            foreach (DonorWorldBaselineEntityMetadata entity in entities)
            {
                if (!stableIds.Add(entity.StableId))
                {
                    result.Errors.Add(
                        "Duplicate canonical stable ID: " + entity.StableId);
                    continue;
                }
                if (!sourceObjectIds.Add(entity.SourceObjectId))
                {
                    result.Errors.Add(
                        "Duplicate source object ID in canonical baseline: " +
                        entity.SourceObjectId.ToString(
                            CultureInfo.InvariantCulture));
                }
                if (!expected.TryGetValue(
                        entity.StableId,
                        out WorldBaselineSanitationEntry entry))
                {
                    result.Errors.Add(
                        "Canonical scene contains an unexpected stable ID: " +
                        entity.StableId);
                    continue;
                }

                Transform expectedParent = entry.IncludeRenderer
                    ? geometryRoot
                    : metadataOnlyRoot;
                ValidateEntityMetadata(
                    entity, entry, expectedParent, result);
                ValidateEntityPresentation(
                    entity, entry, subsets, result);
            }

            if (!stableIds.SetEquals(expected.Keys))
            {
                result.Errors.Add(
                    "Canonical baseline stable-ID set differs from the sanitation plan.");
            }
        }

        private static void ValidateEntityMetadata(
            DonorWorldBaselineEntityMetadata entity,
            WorldBaselineSanitationEntry entry,
            Transform expectedParent,
            DonorWorldBaselineValidationResult result)
        {
            string prefix = "Entity " + entity.StableId + ": ";
            if (entity.Classification !=
                DonorWorldBaselineClassification.TemporaryDirectImport)
            {
                result.Errors.Add(
                    prefix + "classification is not TemporaryDirectImport.");
            }
            RequireEqual(
                result, prefix + "replacement key",
                entity.ReplacementKey, "legacy-world:" + entity.StableId);
            RequireEqual(
                result, prefix + "source object ID",
                entity.SourceObjectId, entry.Placement.SourceObjectId);
            RequireEqual(
                result, prefix + "source parent stable ID",
                entity.SourceParentStableId,
                entry.SourceParentStableId);
            RequireEqual(
                result, prefix + "source hierarchy path",
                entity.SourceHierarchyPath, entry.Placement.HierarchyPath);
            RequireEqual(
                result, prefix + "source mesh GUID",
                entity.SourceMeshGuid, entry.Placement.MeshGuid);
            RequireEqual(
                result, prefix + "source cell",
                entity.SourceCellId, entry.Placement.CellId);
            RequireEqual(
                result, prefix + "semantic category",
                entity.SemanticCategory, entry.Placement.Category);
            RequireEqual(
                result, prefix + "source component IDs",
                entity.SourceComponentClassIds,
                entry.ComponentClassIdsText);
            RequireEqual(
                result, prefix + "sanitation disposition",
                entity.SanitationDisposition, entry.Disposition);
            RequireEqual(
                result, prefix + "sanitation reason",
                entity.SanitationReason, entry.Reason);
            RequireEqual(
                result, prefix + "source activeSelf",
                entity.SourceActiveSelf, entry.SourceActiveSelf);
            RequireEqual(
                result, prefix + "source active-in-hierarchy",
                entity.SourceActiveInHierarchy, entry.EffectiveActive);
            RequireEqual(
                result, prefix + "renderer flag",
                entity.HasSanitizedRenderer, entry.IncludeRenderer);
            RequireEqual(
                result, prefix + "active state",
                entity.gameObject.activeSelf, entry.EffectiveActive);
            RequireEqual(
                result, prefix + "active-in-hierarchy state",
                entity.gameObject.activeInHierarchy,
                entry.EffectiveActive);
            if (expectedParent == null ||
                entity.transform.parent != expectedParent)
            {
                result.Errors.Add(
                    prefix + "is not a direct child of its sanitation container.");
            }
            RequireTransform(
                result, prefix + "transform",
                entity.transform,
                entry.Placement.Position,
                entry.Placement.Rotation,
                entry.Placement.Scale);
            if (Vector3.Distance(
                    entity.transform.position,
                    entry.Placement.Position) > PositionTolerance)
            {
                result.Errors.Add(
                    prefix + "world position drifted through its parent transform.");
            }
            if (Quaternion.Angle(
                    entity.transform.rotation,
                    entry.Placement.Rotation) >
                RotationToleranceDegrees)
            {
                result.Errors.Add(
                    prefix + "world rotation drifted through its parent transform.");
            }
        }

        private static void ValidateEntityPresentation(
            DonorWorldBaselineEntityMetadata entity,
            WorldBaselineSanitationEntry entry,
            IReadOnlyDictionary<long, int[]> subsets,
            DonorWorldBaselineValidationResult result)
        {
            MeshFilter meshFilter = entity.GetComponent<MeshFilter>();
            MeshRenderer renderer = entity.GetComponent<MeshRenderer>();
            if (!entry.IncludeRenderer)
            {
                if (meshFilter != null || renderer != null)
                {
                    result.Errors.Add(
                        "Metadata-only entity has render components: " +
                        entity.StableId);
                }
                return;
            }

            if (meshFilter == null || renderer == null ||
                meshFilter.sharedMesh == null)
            {
                result.Errors.Add(
                    "Renderer-accepted entity is missing MeshFilter, MeshRenderer " +
                    "or Mesh: " + entity.StableId);
                return;
            }

            Mesh mesh = meshFilter.sharedMesh;
            if (mesh.vertexCount <= 0 ||
                mesh.subMeshCount <= 0 ||
                !IsFinite(mesh.bounds.min) ||
                !IsFinite(mesh.bounds.max))
            {
                result.Errors.Add(
                    "Sanitized renderer uses an empty or invalid mesh: " +
                    entity.StableId);
            }

            string meshPath =
                AssetDatabase.GetAssetPath(mesh);
            string expectedMeshPath = subsets.ContainsKey(
                entry.Placement.SourceObjectId)
                ? WorldBaselinePaths.DerivedMeshRoot + "/" +
                  entry.Placement.StableId + ".asset"
                : WorldBaselinePaths.SourceMeshRoot + "/" +
                  entry.Placement.MeshGuid + ".asset";
            if (!string.Equals(
                    meshPath, expectedMeshPath, StringComparison.Ordinal))
            {
                result.Errors.Add(
                    "Sanitized renderer uses the wrong locked mesh: " +
                    entity.StableId + " -> " + meshPath +
                    "; expected " + expectedMeshPath);
            }

            Material[] materials = renderer.sharedMaterials;
            int expectedMaterialSlots =
                Mathf.Max(1, mesh.subMeshCount);
            if (materials.Length != expectedMaterialSlots)
            {
                result.Errors.Add(
                    "Sanitized renderer material-slot count mismatch: " +
                    entity.StableId);
            }
            string expectedMaterialPath =
                WorldBaselinePaths.CategoryMaterial(
                    entry.Placement.Category);
            foreach (Material material in materials)
            {
                string materialPath =
                    material == null ? string.Empty :
                    AssetDatabase.GetAssetPath(material);
                if (material == null ||
                    !string.Equals(
                        materialPath,
                        expectedMaterialPath,
                        StringComparison.Ordinal) ||
                    material.shader == null ||
                    !string.Equals(
                        material.shader.name,
                        "HDRP/Unlit",
                        StringComparison.Ordinal) ||
                    !material.enableInstancing)
                {
                    result.Errors.Add(
                        "Sanitized renderer has a missing, wrong or non-HDRP " +
                        "category material: " +
                        entity.StableId);
                    break;
                }

                foreach (string textureProperty in
                         material.GetTexturePropertyNames())
                {
                    if (material.GetTexture(textureProperty) != null)
                    {
                        result.Errors.Add(
                            "Neutral category material unexpectedly references " +
                            "a texture: " + materialPath + " / " +
                            textureProperty);
                        break;
                    }
                }
            }

            if (!renderer.enabled ||
                renderer.shadowCastingMode != ShadowCastingMode.Off ||
                renderer.receiveShadows ||
                renderer.lightProbeUsage != LightProbeUsage.Off ||
                renderer.reflectionProbeUsage != ReflectionProbeUsage.Off ||
                renderer.motionVectorGenerationMode !=
                MotionVectorGenerationMode.ForceNoMotion)
            {
                result.Errors.Add(
                    "Sanitized renderer settings drifted: " +
                    entity.StableId);
            }
        }

        private static void RequireTransform(
            DonorWorldBaselineValidationResult result,
            string label,
            Transform transform,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            RequireVector(
                result, label + " position",
                transform.localPosition, position);
            RequireQuaternion(
                result, label + " rotation",
                transform.localRotation, rotation);
            RequireVector(
                result, label + " scale",
                transform.localScale, scale);
        }

        private static void RequireVector(
            DonorWorldBaselineValidationResult result,
            string label,
            Vector3 actual,
            Vector3 expected)
        {
            if (Vector3.Distance(actual, expected) > PositionTolerance)
            {
                result.Errors.Add(
                    $"{label} mismatch: actual={actual}, expected={expected}.");
            }
        }

        private static void RequireQuaternion(
            DonorWorldBaselineValidationResult result,
            string label,
            Quaternion actual,
            Quaternion expected)
        {
            if (Quaternion.Angle(actual, expected) >
                RotationToleranceDegrees)
            {
                result.Errors.Add(
                    $"{label} mismatch: actual={actual.eulerAngles}, " +
                    $"expected={expected.eulerAngles}.");
            }
        }

        private static void RequireColor(
            DonorWorldBaselineValidationResult result,
            string label,
            Color actual,
            Color expected)
        {
            if (!ColorsEqual(actual, expected))
            {
                result.Errors.Add(
                    $"{label} mismatch: actual='{actual}', expected='{expected}'.");
            }
        }

        private static bool ColorsEqual(Color actual, Color expected) =>
            Mathf.Abs(actual.r - expected.r) <= PositionTolerance &&
            Mathf.Abs(actual.g - expected.g) <= PositionTolerance &&
            Mathf.Abs(actual.b - expected.b) <= PositionTolerance &&
            Mathf.Abs(actual.a - expected.a) <= PositionTolerance;

        private static void RequireEqual<T>(
            DonorWorldBaselineValidationResult result,
            string label,
            T actual,
            T expected)
        {
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
            {
                result.Errors.Add(
                    $"{label} mismatch: actual='{actual}', expected='{expected}'.");
            }
        }

        private static void RequireRoleCount(
            DonorWorldBaselineValidationResult result,
            IEnumerable<DonorWorldBaselineSourceFileRecord> records,
            string role,
            int expected)
        {
            int actual = records.Count(record => string.Equals(
                record.role, role, StringComparison.Ordinal));
            RequireEqual(
                result, "source manifest role " + role,
                actual, expected);
        }

        private static string SafeCombine(
            string root,
            string relativePath)
        {
            if (Path.IsPathRooted(relativePath) ||
                relativePath.Split('/').Any(
                    part => part == string.Empty ||
                            part == "." ||
                            part == ".."))
            {
                throw new InvalidDataException(
                    "Unsafe source-relative path: " + relativePath);
            }

            string normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(
                root,
                relativePath.Replace(
                    '/', Path.DirectorySeparatorChar)));
            if (!candidate.StartsWith(
                    normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    "Source-relative path escapes its configured root: " +
                    relativePath);
            }
            return candidate;
        }

        private static string ToProjectRelativePath(string absolutePath)
        {
            string projectRoot = Path.GetFullPath(WorldTransferPaths.ProjectRoot)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            string normalized = Path.GetFullPath(absolutePath);
            if (!normalized.StartsWith(
                    projectRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                return normalized;
            }

            return normalized.Substring(projectRoot.Length + 1)
                .Replace('\\', '/');
        }

        private static string GetHierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }
            return string.Join("/", names);
        }

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
