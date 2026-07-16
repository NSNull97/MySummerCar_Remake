using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Bootstrap;
using MSC.Core.Identity;
using MSC.Editor.WorldStreaming;
using MSC.Editor.WorldTransfer;
using MSC.LegacyImport;
using MSC.World.Data;
using MSC.World.Partition;
using MSC.World.Streaming;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace MSC.Editor.WorldBaseline
{
    public sealed class DonorWorldCellizationValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public List<string> Warnings { get; } = new List<string>();
        public bool Passed => Errors.Count == 0;
        public int SceneCount { get; internal set; }
        public int EntityCount { get; internal set; }
        public int RendererCount { get; internal set; }
        public int ColliderCount { get; internal set; }
        public int GameplayAnchorCount { get; internal set; }
        public int GeneratedMaterialCount { get; internal set; }
        public int GeneratedTextureCount { get; internal set; }
        public string OwnershipFingerprintSha256 { get; internal set; } =
            string.Empty;
        public string GameplayAnchorFingerprintSha256
        {
            get;
            internal set;
        } = string.Empty;
    }

    public static class DonorWorldCellizationValidator
    {
        // HDRP 17.3 offsets decal-supporting Lit materials inside the
        // opaque and alpha-test ranges during material validation.
        private const int HdrpLitOpaqueWithDecalsQueue = 2225;
        private const int HdrpLitAlphaClipWithDecalsQueue = 2475;

        private static readonly HashSet<Type> AllowedGeneratedTypes =
            new HashSet<Type>
            {
                typeof(Transform),
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(MeshCollider),
                typeof(BoxCollider),
                typeof(DonorWorldStreamingSceneMetadata),
                typeof(DonorWorldBaselineEntityMetadata),
                typeof(DonorWorldBaselineColliderMetadata),
                typeof(DonorWorldLegacyReplacementRegistry),
                typeof(DonorWorldLegacyMaterialBinding),
                typeof(DonorWorldLegacyPresentationController)
            };

        private static readonly string[] ForbiddenDependencyFragments =
        {
            "/PlayMaker",
            "/Assembly-CSharp",
            "/Steamworks",
            "/MSCLoader",
            "/ES2",
            "Assets/Plugins/",
            "/Imported/DonorGenerated/",
            "/LegacyImport/ReferenceOnly/"
        };

        [MenuItem(
            "Tools/MSC Remake/World Baseline 06B2/" +
            "Validate Active Donor Streaming Profile")]
        public static void ValidateFromMenu()
        {
            DonorWorldCellizationValidationResult result =
                Validate(
                    verifySourceHashes: true,
                    inspectAllGeneratedScenes: true);
            Log(result);
        }

        public static DonorWorldCellizationValidationResult Validate(
            bool verifySourceHashes,
            bool inspectAllGeneratedScenes)
        {
            var result =
                new DonorWorldCellizationValidationResult();
            DonorWorldCellizationPlan plan;
            try
            {
                plan = DonorWorldCellizationPlan.Load();
                DonorWorldCellizationPlan second =
                    DonorWorldCellizationPlan.Load();
                if (!string.Equals(
                        plan.OwnershipFingerprintSha256,
                        second.OwnershipFingerprintSha256,
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Repeated dry-run ownership planning is not " +
                        "deterministic.");
                }
                result.OwnershipFingerprintSha256 =
                    plan.OwnershipFingerprintSha256;
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "Could not load the deterministic 06B2 plan: " +
                    exception.Message);
                return result;
            }

            DonorWorldBaselineValidationResult baseline =
                DonorWorldBaselineValidator.Validate(
                    verifySourceHashes);
            foreach (string error in baseline.Errors)
            {
                result.Errors.Add("06B1 source gate: " + error);
            }
            foreach (string warning in baseline.Warnings)
            {
                result.Warnings.Add("06B1 source gate: " + warning);
            }

            DonorWorldMaterialTextureAssets presentation = null;
            try
            {
                DonorWorldMaterialTexturePlan presentationPlan =
                    DonorWorldMaterialTexturePlan.Load();
                DonorWorldMaterialTexturePlan repeatedPlan =
                    DonorWorldMaterialTexturePlan.Load();
                if (!string.Equals(
                        presentationPlan.PresentationFingerprintSha256,
                        repeatedPlan.PresentationFingerprintSha256,
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Repeated material/texture planning is not " +
                        "deterministic.");
                }
                presentation =
                    DonorWorldMaterialTexturePipeline.LoadGenerated(
                        presentationPlan);
                ValidatePresentationAssets(presentation, result);
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "06B2 v5.1 presentation gate failed: " +
                    exception.Message);
            }

            ProductionWorldStreamingManifest activeManifest =
                ValidateActiveManifest(plan, result);
            ValidateBuildSettings(plan, activeManifest, result);
            ValidateActiveBootstrap(activeManifest, result);
            ValidatePrototypeFixture(activeManifest, result);
            ValidateGameplayCatalog(activeManifest, result);
            ValidateGeneratedPayloadTrackedState(result);
            ValidateRequiredExports(plan, result);
            if (inspectAllGeneratedScenes && presentation != null)
            {
                ValidateGeneratedScenes(
                    plan,
                    presentation,
                    result);
            }

            result.Warnings.Add(
                "The feature-parity baseline uses temporary donor-derived " +
                "compatibility presentation and unsplit global " +
                "static-batch aggregates; it is not ProductionReady.");
            result.Warnings.Add(
                "The donor night-gradient cubemap and donor shader/runtime " +
                "lighting/weather/water systems remain intentionally " +
                "excluded.");
            result.Warnings.Add(
                "The 32-collider allowlist is deliberately narrow; doors, " +
                "windows, dynamic props, NPCs and trigger volumes remain " +
                "excluded pending their gameplay milestones.");
            return result;
        }

        public static void RunBatch()
        {
            DonorWorldCellizationValidationResult result =
                Validate(
                    verifySourceHashes: true,
                    inspectAllGeneratedScenes: true);
            Log(result);
            if (!result.Passed)
            {
                throw new InvalidOperationException(
                    "06B2 validation failed:\n- " +
                    string.Join("\n- ", result.Errors));
            }
        }

        private static ProductionWorldStreamingManifest
            ValidateActiveManifest(
                DonorWorldCellizationPlan plan,
                DonorWorldCellizationValidationResult result)
        {
            ProductionWorldStreamingManifest manifest =
                AssetDatabase.LoadAssetAtPath<
                    ProductionWorldStreamingManifest>(
                    WorldBaseline06B2Paths.ActiveManifest);
            if (manifest == null)
            {
                result.Errors.Add(
                    "Active 06B2 streaming manifest is missing.");
                return null;
            }

            foreach (string error in manifest.ValidateConfiguration())
            {
                result.Errors.Add("Active manifest: " + error);
            }
            RequireEqual(
                result,
                "active profile ID",
                manifest.ProfileId,
                WorldBaseline06B2Paths.ProfileId);
            RequireEqual(
                result,
                "active profile kind",
                manifest.ProfileKind,
                ProductionWorldProfileKind.DonorFeatureParity);
            RequireEqual(
                result,
                "private-local flag",
                manifest.PrivateLocalRuntimeBaseline,
                true);
            RequireEqual(
                result,
                "cell size",
                manifest.CellSizeMeters,
                WorldBaseline06B2Paths.CellSizeMeters);
            RequireEqual(
                result,
                "loading radius",
                manifest.LoadingRadiusCells,
                WorldBaseline06B2Paths.LoadingRadiusCells);
            RequireEqual(
                result,
                "unloading radius",
                manifest.UnloadingRadiusCells,
                WorldBaseline06B2Paths.UnloadingRadiusCells);
            RequireEqual(
                result,
                "vehicle preload radius",
                manifest.VehiclePreloadRadiusCells,
                WorldBaseline06B2Paths.VehiclePreloadRadiusCells);
            if (manifest.GlobalScenes.Count != 1)
            {
                result.Errors.Add(
                    "Active manifest must have exactly one unsplit global " +
                    "legacy scene.");
            }
            else
            {
                ProductionWorldGlobalScene global =
                    manifest.GlobalScenes[0];
                RequireEqual(
                    result,
                    "global scene ID",
                    global.SceneId,
                    "global-legacy");
                RequireEqual(
                    result,
                    "global scene path",
                    global.ScenePath,
                    WorldBaseline06B2Paths.GlobalScene);
            }

            RequireEqual(
                result,
                "active cell count",
                manifest.Cells.Count,
                DonorWorldCellizationPlan.ExpectedCellCount);
            string[] expectedCells = plan.CellIds.ToArray();
            string[] actualCells = manifest.Cells
                .Select(cell => cell.CellId)
                .ToArray();
            if (!actualCells.SequenceEqual(
                    expectedCells,
                    StringComparer.Ordinal))
            {
                result.Errors.Add(
                    "Active manifest cell order/set differs from the " +
                    "deterministic 06B2 plan.");
            }

            foreach (ProductionWorldCellScene cell in manifest.Cells)
            {
                string expectedPath =
                    WorldBaseline06B2Paths.CellScene(cell.CellId);
                if (!string.Equals(
                        cell.ScenePath,
                        expectedPath,
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Cell path mismatch for " + cell.CellId);
                }
                if (cell.ScenePath.Contains(
                        "/Generated/ProductionCells/",
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Rejected prototype visual path leaked into the " +
                        "active donor profile: " + cell.ScenePath);
                }
            }

            return manifest;
        }

        private static void ValidateBuildSettings(
            DonorWorldCellizationPlan plan,
            ProductionWorldStreamingManifest manifest,
            DonorWorldCellizationValidationResult result)
        {
            EditorBuildSettingsScene[] scenes =
                EditorBuildSettings.scenes;
            string[] enabled = scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();
            if (enabled.Length == 0 ||
                !string.Equals(
                    enabled[0],
                    WorldBaseline06B2Paths.ActiveBootstrapScene,
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    "Active Bootstrap must remain the first enabled scene.");
            }
            if (enabled.Contains(
                    WorldBaselinePaths.CanonicalScene,
                    StringComparer.Ordinal))
            {
                result.Errors.Add(
                    "Canonical 06B1 source scene must never be enabled in " +
                    "Build Settings.");
            }

            var expected = new List<string>
            {
                WorldBaseline06B2Paths.GlobalScene
            };
            expected.AddRange(
                plan.CellIds.Select(
                    WorldBaseline06B2Paths.CellScene));
            foreach (string scenePath in expected)
            {
                int buildIndex =
                    ProductionWorldStreamingBuilder
                        .GetEnabledBuildIndex(scenePath);
                if (buildIndex < 0)
                {
                    result.Errors.Add(
                        "Generated 06B2 scene is not enabled: " +
                        scenePath);
                }
            }

            if (manifest == null)
            {
                return;
            }
            foreach (ProductionWorldGlobalScene global in
                     manifest.GlobalScenes)
            {
                ValidateBuildAddress(
                    global.BuildIndex,
                    global.ScenePath,
                    result);
            }
            foreach (ProductionWorldCellScene cell in manifest.Cells)
            {
                ValidateBuildAddress(
                    cell.BuildIndex,
                    cell.ScenePath,
                    result);
            }
        }

        private static void ValidateBuildAddress(
            int buildIndex,
            string scenePath,
            DonorWorldCellizationValidationResult result)
        {
            int actual =
                ProductionWorldStreamingBuilder.GetEnabledBuildIndex(
                    scenePath);
            if (actual != buildIndex)
            {
                result.Errors.Add(
                    $"Build address mismatch for {scenePath}: manifest " +
                    $"{buildIndex}, enabled index {actual}.");
            }
        }

        private static void ValidateActiveBootstrap(
            ProductionWorldStreamingManifest activeManifest,
            DonorWorldCellizationValidationResult result)
        {
            if (activeManifest == null)
            {
                return;
            }

            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(
                    WorldBaseline06B2Paths.ActiveBootstrapScene);
                GameCompositionRoot[] roots =
                    GetSceneComponents<GameCompositionRoot>(scene);
                ProductionWorldStreamingService[] services =
                    GetSceneComponents<
                        ProductionWorldStreamingService>(scene);
                ProductionWorldStreamingInstaller[] installers =
                    GetSceneComponents<
                        ProductionWorldStreamingInstaller>(scene);
                DonorWorldLegacyPresentationController[] controllers =
                    GetSceneComponents<
                        DonorWorldLegacyPresentationController>(scene);
                if (roots.Length != 1 ||
                    services.Length != 1 ||
                    installers.Length != 1 ||
                    controllers.Length != 1)
                {
                    result.Errors.Add(
                        "Active Bootstrap must contain exactly one root, " +
                        "streaming service, installer and legacy " +
                        "presentation controller.");
                    return;
                }

                ProductionWorldStreamingService service = services[0];
                ProductionWorldStreamingInstaller installer =
                    installers[0];
                DonorWorldLegacyPresentationController controller =
                    controllers[0];
                if (service.Manifest != activeManifest)
                {
                    result.Errors.Add(
                        "Active Bootstrap is not wired to the donor " +
                        "feature-parity manifest.");
                }
                if (installer.WorldStreaming != service ||
                    installer.CompositionRoot != roots[0] ||
                    installer.PlayerPrefab == null ||
                    controller.gameObject != roots[0].gameObject ||
                    controller.Mode !=
                        DonorWorldLegacyPresentationMode.LegacyTextured)
                {
                    result.Errors.Add(
                        "Active Bootstrap composition references are " +
                        "incomplete.");
                }
                if (installer.OutOfBoundsMinimumY >=
                        installer.PlayerSpawnPosition.y ||
                    !float.IsFinite(installer.OutOfBoundsMinimumY))
                {
                    result.Errors.Add(
                        "Active Bootstrap out-of-bounds threshold is " +
                        "invalid.");
                }
                if (Vector3.Distance(
                        installer.PlayerSpawnPosition,
                        ProductionWorldStreamingBuilder
                            .PlayerSpawnPosition) > 0.0001f ||
                    Quaternion.Angle(
                        installer.PlayerSpawnRotation,
                        ProductionWorldStreamingBuilder
                            .PlayerSpawnRotation) > 0.001f)
                {
                    result.Errors.Add(
                        "Active Bootstrap player spawn differs from the " +
                        "accepted project-owned spawn.");
                }
                WorldCellIndex spawnCell =
                    WorldCellMembershipUtility.FromPosition(
                        installer.PlayerSpawnPosition,
                        activeManifest.CellSizeMeters);
                if (!activeManifest.TryGetCell(spawnCell, out _))
                {
                    result.Errors.Add(
                        "Active Bootstrap player spawn belongs to a cell " +
                        "that is absent from the donor streaming manifest: " +
                        spawnCell.Id);
                }
                if (service.Focus != null)
                {
                    result.Errors.Add(
                        "Streaming focus must be bound to the spawned player " +
                        "only at runtime.");
                }
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "Could not inspect active Bootstrap: " +
                    exception.Message);
            }
            finally
            {
                if (scene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        private static void ValidatePrototypeFixture(
            ProductionWorldStreamingManifest activeManifest,
            DonorWorldCellizationValidationResult result)
        {
            ProductionWorldStreamingManifest prototype =
                AssetDatabase.LoadAssetAtPath<
                    ProductionWorldStreamingManifest>(
                    WorldBaseline06B2Paths.PrototypeManifest);
            if (prototype == null)
            {
                result.Errors.Add(
                    "05B.1 prototype fixture manifest is missing.");
                return;
            }
            foreach (string error in prototype.ValidateConfiguration())
            {
                result.Errors.Add(
                    "Prototype fixture manifest: " + error);
            }
            if (prototype == activeManifest ||
                prototype.ProfileKind !=
                    ProductionWorldProfileKind.PrototypeFixture ||
                prototype.Cells.Count != 2)
            {
                result.Errors.Add(
                    "The old two-cell world is not isolated as a separate " +
                    "PrototypeFixture profile.");
            }
            if (activeManifest != null &&
                activeManifest.Cells.Any(cell =>
                    prototype.Cells.Any(prototypeCell =>
                        string.Equals(
                            cell.ScenePath,
                            prototypeCell.ScenePath,
                            StringComparison.Ordinal))))
            {
                result.Errors.Add(
                    "Prototype visual scenes leaked into the active donor " +
                    "profile.");
            }
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    WorldBaseline06B2Paths.PrototypeFixtureScene) == null)
            {
                result.Errors.Add(
                    "Prototype streaming fixture scene is missing.");
            }
        }

        private static void ValidateGameplayCatalog(
            ProductionWorldStreamingManifest manifest,
            DonorWorldCellizationValidationResult result)
        {
            WorldGameplayCellCatalog catalog =
                manifest != null
                    ? manifest.GameplayCatalog
                    : null;
            if (catalog == null)
            {
                result.Errors.Add(
                    "Active profile has no project-owned gameplay catalog.");
                return;
            }
            foreach (string error in catalog.ValidateConfiguration())
            {
                result.Errors.Add(
                    "Gameplay catalog: " + error);
            }
            RequireEqual(
                result,
                "gameplay catalog ID",
                catalog.CatalogId,
                WorldBaseline06B2Paths.GameplayCatalogId);
            result.GameplayAnchorCount = catalog.Anchors.Count;
            WorldGameplayAnchorManifestData expected =
                WorldGameplayAnchorManifest.Load();
            result.GameplayAnchorFingerprintSha256 =
                expected.FingerprintSha256;
            RequireEqual(
                result,
                "migrated gameplay anchor count",
                catalog.Anchors.Count,
                WorldGameplayAnchorManifest.ExpectedAnchorCount);
            if (catalog.Anchors.Count != expected.Records.Count)
            {
                return;
            }

            for (int index = 0;
                 index < expected.Records.Count;
                 index++)
            {
                WorldGameplayAnchorRecord actual =
                    catalog.Anchors[index];
                WorldGameplayAnchorRecord source =
                    expected.Records[index];
                if (!manifest.TryGetCell(actual.Cell, out _))
                {
                    result.Errors.Add(
                        "Gameplay anchor belongs to a cell that is absent " +
                        "from the active donor manifest: " +
                        actual.AnchorId + " -> " + actual.Cell.Id);
                }
                if (!string.Equals(
                        actual.AnchorId,
                        source.AnchorId,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        actual.StableEntityId,
                        source.StableEntityId,
                        StringComparison.Ordinal) ||
                    !actual.Cell.Equals(source.Cell) ||
                    Vector3.Distance(
                        actual.Position,
                        source.Position) > 0.0001f ||
                    Quaternion.Angle(
                        actual.Rotation,
                        source.Rotation) > 0.001f)
                {
                    result.Errors.Add(
                        "Gameplay anchor differs from the project-owned " +
                        "06B2 manifest: " + source.AnchorId);
                }
            }
        }

        private static void ValidatePresentationAssets(
            DonorWorldMaterialTextureAssets presentation,
            DonorWorldCellizationValidationResult result)
        {
            DonorWorldMaterialTexturePlan plan = presentation.Plan;
            result.GeneratedMaterialCount =
                presentation.TexturedMaterials.Count + 1;
            result.GeneratedTextureCount =
                presentation.ConvertedTextures.Count;
            RequireEqual(
                result,
                "generated compatibility material count",
                presentation.TexturedMaterials.Count,
                DonorWorldMaterialTexturePlan
                    .ExpectedResolvedMaterialCount);
            RequireEqual(
                result,
                "generated texture role-variant count",
                presentation.ConvertedTextures.Count,
                DonorWorldMaterialTexturePlan
                    .ExpectedImportedTextureConversionCount);

            foreach (DonorWorldSourceMaterial source in
                     plan.Materials.Values)
            {
                if (!presentation.TexturedMaterials.TryGetValue(
                        source.SourceGuid,
                        out Material material) ||
                    material == null)
                {
                    result.Errors.Add(
                        "Generated HDRP compatibility material is " +
                        "missing: " + source.SourceGuid);
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(material);
                string expectedPath =
                    WorldBaselinePaths.TexturedMaterial(
                        source.SourceGuid);
                bool expectedUnlit =
                    DonorWorldMaterialTexturePipeline.IsUnlit(source);
                string expectedShader =
                    expectedUnlit ? "HDRP/Unlit" : "HDRP/Lit";
                if (!string.Equals(
                        path,
                        expectedPath,
                        StringComparison.Ordinal) ||
                    material.shader == null ||
                    !string.Equals(
                        material.shader.name,
                        expectedShader,
                        StringComparison.Ordinal) ||
                    !material.enableInstancing ||
                    material.name.EndsWith(
                        " (Instance)",
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Generated compatibility material contract " +
                        "drifted: " + source.SourceGuid);
                }

                ValidateCompatibilityMaterialMapping(
                    presentation,
                    source,
                    material,
                    result);

                foreach (string dependency in
                         AssetDatabase.GetDependencies(
                             path,
                             recursive: true))
                {
                    string normalizedDependency =
                        dependency.Replace('\\', '/');
                    if (dependency.StartsWith(
                            "Assets/Game/LegacyImport/" +
                            "RuntimeBaseline/",
                            StringComparison.Ordinal) &&
                        dependency.EndsWith(
                            ".shader",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        result.Errors.Add(
                            "Donor shader entered generated presentation: " +
                            dependency);
                    }
                    if (ForbiddenDependencyFragments.Any(fragment =>
                            normalizedDependency.IndexOf(
                                fragment,
                                StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        result.Errors.Add(
                            "Forbidden donor/runtime dependency entered " +
                            "generated presentation: " + dependency);
                    }
                }
            }

            foreach (DonorWorldTextureConversion conversion in
                     plan.Textures.Values)
            {
                if (!conversion.IsImported)
                {
                    continue;
                }
                if (!presentation.ConvertedTextures.TryGetValue(
                        conversion.Key,
                        out Texture texture) ||
                    texture == null)
                {
                    result.Errors.Add(
                        "Generated compatibility texture is missing: " +
                        conversion.Key);
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(texture);
                TextureImporter importer =
                    AssetImporter.GetAtPath(path) as TextureImporter;
                string absolutePath =
                    WorldBaselinePaths.ToAbsoluteProjectPath(path);
                if (!string.Equals(
                        path,
                        conversion.GeneratedAssetPath,
                        StringComparison.Ordinal) ||
                    importer == null ||
                    importer.isReadable ||
                    !importer.mipmapEnabled ||
                    !importer.streamingMipmaps ||
                    importer.sRGBTexture != conversion.SRgb ||
                    importer.wrapMode !=
                    DonorWorldMaterialTexturePipeline.ResolveWrapMode(
                        conversion.SourceWrapMode) ||
                    importer.filterMode !=
                    DonorWorldMaterialTexturePipeline.ResolveFilterMode(
                        conversion.SourceFilterMode) ||
                    importer.anisoLevel != Mathf.Clamp(
                        conversion.SourceAnisoLevel,
                        1,
                        16) ||
                    importer.textureType !=
                    (conversion.IsNormalMap
                        ? TextureImporterType.NormalMap
                        : TextureImporterType.Default) ||
                    !File.Exists(absolutePath) ||
                    !string.Equals(
                        DonorWorldBaselineManifest.ComputeFileSha256(
                            absolutePath),
                        DonorWorldMaterialTexturePipeline
                            .GetExpectedGeneratedTextureSha256(
                                conversion),
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        "Generated texture import contract drifted: " +
                        conversion.Key);
                }
            }

            Material fallback = presentation.UnsupportedMaterial;
            if (fallback == null ||
                fallback.shader == null ||
                !string.Equals(
                    fallback.shader.name,
                    "HDRP/Unlit",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    AssetDatabase.GetAssetPath(fallback),
                    WorldBaselinePaths.UnsupportedMaterial,
                    StringComparison.Ordinal) ||
                !fallback.enableInstancing ||
                fallback.name.EndsWith(
                    " (Instance)",
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    "Reviewed built-in material fallback is missing or " +
                    "uses the shader-error state.");
            }
            else
            {
                Color expectedFallback =
                    new Color(1f, 0.08f, 0.01f, 1f);
                ValidateColorProperty(
                    fallback,
                    "_UnlitColor",
                    expectedFallback,
                    "built-in fallback",
                    result);
                ValidateColorProperty(
                    fallback,
                    "_BaseColor",
                    expectedFallback,
                    "built-in fallback",
                    result);
            }

            string runtimeRoot =
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    WorldBaselinePaths.RuntimeRoot);
            if (Directory.Exists(runtimeRoot) &&
                Directory.EnumerateFiles(
                        runtimeRoot,
                        "*.shader",
                        SearchOption.AllDirectories)
                    .Any())
            {
                result.Errors.Add(
                    "Generated RuntimeBaseline contains donor shader " +
                    "source, which is forbidden.");
            }

            string csvPath =
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    WorldBaseline06B2Paths.MaterialTextureManifest);
            if (File.Exists(csvPath))
            {
                string csv = File.ReadAllText(csvPath);
                if (csv.IndexOf(
                        ":\\",
                        StringComparison.Ordinal) >= 0 ||
                    csv.IndexOf(
                        "file://",
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Errors.Add(
                        "Material/texture manifest leaks a machine-specific " +
                        "absolute path.");
                }
            }
        }

        private static void ValidateCompatibilityMaterialMapping(
            DonorWorldMaterialTextureAssets presentation,
            DonorWorldSourceMaterial source,
            Material material,
            DonorWorldCellizationValidationResult result)
        {
            string label = "material " + source.SourceGuid;
            bool unlit =
                DonorWorldMaterialTexturePipeline.IsUnlit(source);
            bool alphaClip =
                DonorWorldMaterialTexturePipeline.IsAlphaClip(source);
            bool transparent =
                DonorWorldMaterialTexturePipeline.IsTransparent(source);
            bool emissive =
                DonorWorldMaterialTexturePipeline.IsEmissive(source);

            Color expectedBaseColor =
                DonorWorldMaterialTexturePipeline
                    .GetExpectedBaseColor(source);
            int baseColorPropertyCount = 0;
            baseColorPropertyCount += ValidateColorProperty(
                material,
                "_BaseColor",
                expectedBaseColor,
                label,
                result) ? 1 : 0;
            baseColorPropertyCount += ValidateColorProperty(
                material,
                "_UnlitColor",
                expectedBaseColor,
                label,
                result) ? 1 : 0;
            if (baseColorPropertyCount == 0)
            {
                result.Errors.Add(
                    label + " exposes no supported HDRP base-color property.");
            }

            if (!unlit)
            {
                ValidateFloatProperty(
                    material,
                    "_Metallic",
                    DonorWorldMaterialTexturePipeline
                        .GetExpectedMetallic(source),
                    label,
                    result);
                ValidateFloatProperty(
                    material,
                    "_Smoothness",
                    DonorWorldMaterialTexturePipeline
                        .GetExpectedSmoothness(source),
                    label,
                    result);
                ValidateFloatProperty(
                    material,
                    "_NormalMapSpace",
                    0f,
                    label,
                    result);
                ValidateFloatProperty(
                    material,
                    "_SupportDecals",
                    1f,
                    label,
                    result);
            }

            Texture expectedBaseTexture = null;
            DonorWorldTextureEnvironment baseTextureEnvironment = null;
            if (DonorWorldMaterialTexturePipeline
                .TryGetExpectedBaseTexture(
                    source,
                    out baseTextureEnvironment))
            {
                expectedBaseTexture =
                    presentation.GetConvertedTexture(
                        baseTextureEnvironment.TextureGuid,
                        DonorWorldTextureRole.Color);
            }
            int baseMapPropertyCount = 0;
            baseMapPropertyCount += ValidateTextureProperty(
                material,
                "_BaseColorMap",
                expectedBaseTexture,
                baseTextureEnvironment,
                label,
                result) ? 1 : 0;
            baseMapPropertyCount += ValidateTextureProperty(
                material,
                "_UnlitColorMap",
                expectedBaseTexture,
                baseTextureEnvironment,
                label,
                result) ? 1 : 0;
            if (expectedBaseTexture != null &&
                baseMapPropertyCount == 0)
            {
                result.Errors.Add(
                    label + " exposes no supported HDRP base-map property.");
            }

            Texture expectedNormalTexture = null;
            DonorWorldTextureEnvironment normalEnvironment = null;
            if (!unlit &&
                DonorWorldMaterialTexturePipeline
                    .TryGetExpectedPrimaryNormalTexture(
                        source,
                        out normalEnvironment))
            {
                expectedNormalTexture =
                    presentation.GetConvertedTexture(
                        normalEnvironment.TextureGuid,
                        DonorWorldTextureRole.Normal);
            }
            bool hasNormalProperty = ValidateTextureProperty(
                material,
                "_NormalMap",
                expectedNormalTexture,
                normalEnvironment,
                label,
                result);
            if (expectedNormalTexture != null)
            {
                if (!hasNormalProperty)
                {
                    result.Errors.Add(
                        label +
                        " exposes no supported HDRP primary-normal property.");
                }
                ValidateFloatProperty(
                    material,
                    "_NormalScale",
                    DonorWorldMaterialTexturePipeline
                        .GetExpectedNormalScale(source),
                    label,
                    result);
            }

            Texture expectedDetailTexture = null;
            DonorWorldTextureEnvironment detailEnvironment = null;
            if (!unlit &&
                DonorWorldMaterialTexturePipeline
                    .TryGetExpectedDetailNormalTexture(
                        source,
                        out detailEnvironment))
            {
                expectedDetailTexture =
                    presentation.GetConvertedTexture(
                        detailEnvironment.TextureGuid,
                        DonorWorldTextureRole.DetailNormalPacked);
            }
            bool hasDetailProperty = ValidateTextureProperty(
                material,
                "_DetailMap",
                expectedDetailTexture,
                detailEnvironment,
                label,
                result);
            if (expectedDetailTexture != null)
            {
                if (!hasDetailProperty)
                {
                    result.Errors.Add(
                        label +
                        " exposes no supported HDRP detail-map property.");
                }
                ValidateFloatProperty(
                    material,
                    "_DetailAlbedoScale",
                    0f,
                    label,
                    result);
                ValidateFloatProperty(
                    material,
                    "_DetailNormalScale",
                    DonorWorldMaterialTexturePipeline
                        .GetExpectedDetailNormalScale(source),
                    label,
                    result);
                ValidateFloatProperty(
                    material,
                    "_DetailSmoothnessScale",
                    0f,
                    label,
                    result);
                ValidateFloatProperty(
                    material,
                    "_LinkDetailsWithBase",
                    0f,
                    label,
                    result);
                ValidateFloatProperty(
                    material,
                    "_UVDetail",
                    DonorWorldMaterialTexturePipeline
                        .GetExpectedDetailUv(source),
                    label,
                    result);
                ValidateColorProperty(
                    material,
                    "_UVDetailsMappingMask",
                    DonorWorldMaterialTexturePipeline
                        .GetExpectedDetailUvMask(source),
                    label,
                    result);
            }

            Texture expectedEmissionTexture = null;
            DonorWorldTextureEnvironment emissionEnvironment = null;
            if (emissive &&
                DonorWorldMaterialTexturePipeline
                    .TryGetExpectedEmissionTexture(
                        source,
                        out emissionEnvironment))
            {
                expectedEmissionTexture =
                    presentation.GetConvertedTexture(
                        emissionEnvironment.TextureGuid,
                        DonorWorldTextureRole.Color);
            }
            bool hasEmissionProperty = ValidateTextureProperty(
                material,
                "_EmissiveColorMap",
                expectedEmissionTexture,
                emissionEnvironment,
                label,
                result);
            if (expectedEmissionTexture != null &&
                !hasEmissionProperty)
            {
                result.Errors.Add(
                    label +
                    " exposes no supported HDRP emission-map property.");
            }
            if (emissive)
            {
                ValidateColorProperty(
                    material,
                    "_EmissiveColor",
                    DonorWorldMaterialTexturePipeline
                        .GetExpectedEmissionColor(source),
                    label,
                    result);
            }
            else if (material.HasProperty("_EmissiveColor"))
            {
                ValidateColorProperty(
                    material,
                    "_EmissiveColor",
                    Color.black,
                    label,
                    result);
            }
            if (material.HasProperty("_UseEmissiveIntensity"))
            {
                ValidateFloatProperty(
                    material,
                    "_UseEmissiveIntensity",
                    0f,
                    label,
                    result);
            }

            ValidateFloatProperty(
                material,
                "_SurfaceType",
                transparent ? 1f : 0f,
                label,
                result);
            ValidateFloatProperty(
                material,
                "_AlphaCutoffEnable",
                alphaClip ? 1f : 0f,
                label,
                result);
            ValidateFloatProperty(
                material,
                "_AlphaCutoff",
                Mathf.Clamp01(source.GetFloat("_Cutoff", 0.5f)),
                label,
                result);
            ValidateFloatProperty(
                material,
                "_DoubleSidedEnable",
                source.DoubleSided ? 1f : 0f,
                label,
                result);
            ValidateFloatProperty(
                material,
                "_CullMode",
                source.DoubleSided
                    ? (float)CullMode.Off
                    : (float)CullMode.Back,
                label,
                result);
            ValidateFloatProperty(
                material,
                "_CullModeForward",
                source.DoubleSided
                    ? (float)CullMode.Off
                    : (float)CullMode.Back,
                label,
                result);
            ValidateFloatProperty(
                material,
                "_ZWrite",
                transparent ? 0f : 1f,
                label,
                result);
            if (transparent)
            {
                ValidateFloatProperty(
                    material,
                    "_BlendMode",
                    0f,
                    label,
                    result);
                ValidateFloatProperty(
                    material,
                    "_TransparentZWrite",
                    0f,
                    label,
                    result);
            }

            ValidateKeyword(
                material,
                "_SURFACE_TYPE_TRANSPARENT",
                transparent,
                label,
                result);
            ValidateKeyword(
                material,
                "_ALPHATEST_ON",
                alphaClip,
                label,
                result);
            ValidateKeyword(
                material,
                "_DOUBLESIDED_ON",
                source.DoubleSided,
                label,
                result);
            ValidateKeyword(
                material,
                "_EMISSIVE_COLOR_MAP",
                expectedEmissionTexture != null,
                label,
                result);
            ValidateKeyword(
                material,
                "_NORMALMAP",
                expectedNormalTexture != null ||
                expectedDetailTexture != null,
                label,
                result);
            ValidateKeyword(
                material,
                "_DETAIL_MAP",
                expectedDetailTexture != null,
                label,
                result);
            ValidateKeyword(
                material,
                "_NORMALMAP_TANGENT_SPACE",
                !unlit,
                label,
                result);

            foreach (string intentionallyUnmapped in new[]
                     {
                         "_MaskMap",
                         "_HeightMap",
                         "_SpecularColorMap"
                     })
            {
                if (material.HasProperty(intentionallyUnmapped) &&
                    material.GetTexture(intentionallyUnmapped) != null)
                {
                    result.Errors.Add(
                        label + " retained stale or unsupported texture " +
                        intentionallyUnmapped + ".");
                }
            }

            int expectedRenderQueue =
                transparent
                    ? (int)RenderQueue.Transparent
                    : alphaClip
                        ? (unlit
                            ? (int)RenderQueue.AlphaTest
                            : HdrpLitAlphaClipWithDecalsQueue)
                        : (unlit
                            ? (int)RenderQueue.Geometry
                            : HdrpLitOpaqueWithDecalsQueue);
            if (material.renderQueue != expectedRenderQueue)
            {
                result.Errors.Add(
                    label + " render queue drifted: expected " +
                    expectedRenderQueue + ", actual " +
                    material.renderQueue + ".");
            }
        }

        private static bool ValidateColorProperty(
            Material material,
            string propertyName,
            Color expected,
            string label,
            DonorWorldCellizationValidationResult result)
        {
            if (!material.HasProperty(propertyName))
            {
                return false;
            }

            Color actual = material.GetColor(propertyName);
            if (!Approximately(actual, expected))
            {
                result.Errors.Add(
                    label + " color mapping drifted for " +
                    propertyName + ".");
            }
            return true;
        }

        private static bool ValidateFloatProperty(
            Material material,
            string propertyName,
            float expected,
            string label,
            DonorWorldCellizationValidationResult result)
        {
            if (!material.HasProperty(propertyName))
            {
                return false;
            }

            float actual = material.GetFloat(propertyName);
            if (!Mathf.Approximately(actual, expected))
            {
                result.Errors.Add(
                    label + " float mapping drifted for " +
                    propertyName + $": expected {expected}, actual {actual}.");
            }
            return true;
        }

        private static bool ValidateTextureProperty(
            Material material,
            string propertyName,
            Texture expectedTexture,
            DonorWorldTextureEnvironment expectedEnvironment,
            string label,
            DonorWorldCellizationValidationResult result)
        {
            if (!material.HasProperty(propertyName))
            {
                return false;
            }

            Texture actualTexture = material.GetTexture(propertyName);
            if (actualTexture != expectedTexture)
            {
                result.Errors.Add(
                    label + " texture mapping drifted for " +
                    propertyName + ".");
                return true;
            }
            if (expectedTexture == null)
            {
                return true;
            }

            string expectedPath =
                AssetDatabase.GetAssetPath(expectedTexture);
            string actualPath =
                AssetDatabase.GetAssetPath(actualTexture);
            string expectedGuid =
                AssetDatabase.AssetPathToGUID(expectedPath);
            string actualGuid =
                AssetDatabase.AssetPathToGUID(actualPath);
            if (string.IsNullOrWhiteSpace(expectedGuid) ||
                !string.Equals(
                    actualPath,
                    expectedPath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    actualGuid,
                    expectedGuid,
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    label + " texture asset path/GUID drifted for " +
                    propertyName + ".");
            }
            if (expectedEnvironment == null ||
                !Approximately(
                    material.GetTextureScale(propertyName),
                    expectedEnvironment.Scale) ||
                !Approximately(
                    material.GetTextureOffset(propertyName),
                    expectedEnvironment.Offset))
            {
                result.Errors.Add(
                    label + " texture UV transform drifted for " +
                    propertyName + ".");
            }
            return true;
        }

        private static void ValidateKeyword(
            Material material,
            string keyword,
            bool expectedEnabled,
            string label,
            DonorWorldCellizationValidationResult result)
        {
            if (material.IsKeywordEnabled(keyword) != expectedEnabled)
            {
                result.Errors.Add(
                    label + " keyword mapping drifted for " + keyword + ".");
            }
        }

        private static bool Approximately(Color left, Color right) =>
            Mathf.Abs(left.r - right.r) <= 0.0001f &&
            Mathf.Abs(left.g - right.g) <= 0.0001f &&
            Mathf.Abs(left.b - right.b) <= 0.0001f &&
            Mathf.Abs(left.a - right.a) <= 0.0001f;

        private static bool Approximately(Vector2 left, Vector2 right) =>
            Vector2.SqrMagnitude(left - right) <= 0.00000001f;

        private static void ValidateGeneratedScenes(
            DonorWorldCellizationPlan plan,
            DonorWorldMaterialTextureAssets presentation,
            DonorWorldCellizationValidationResult result)
        {
            var ownerPaths = new List<(string OwnerId, string ScenePath)>
            {
                ("global", WorldBaseline06B2Paths.GlobalScene)
            };
            ownerPaths.AddRange(plan.CellIds.Select(cellId =>
                (cellId, WorldBaseline06B2Paths.CellScene(cellId))));

            var allEntityIds = new HashSet<string>(
                StringComparer.Ordinal);
            var allReplacementKeys = new HashSet<string>(
                StringComparer.Ordinal);
            var allColliderIds = new HashSet<string>(
                StringComparer.Ordinal);
            IReadOnlyDictionary<long, int[]> staticBatchSubsets =
                WorldStaticBatchSubsetTable.ParseCommittedTable();
            foreach ((string ownerId, string scenePath) in ownerPaths)
            {
                ValidateGeneratedScene(
                    plan,
                    ownerId,
                    scenePath,
                    staticBatchSubsets,
                    presentation,
                    allEntityIds,
                    allReplacementKeys,
                    allColliderIds,
                    result);
            }

            result.SceneCount = ownerPaths.Count;
            result.EntityCount = allEntityIds.Count;
            result.ColliderCount = allColliderIds.Count;
            RequireEqual(
                result,
                "generated scene count",
                result.SceneCount,
                DonorWorldCellizationPlan.ExpectedCellCount + 1);
            RequireEqual(
                result,
                "generated entity count",
                result.EntityCount,
                DonorWorldCellizationPlan.ExpectedEntityCount);
            RequireEqual(
                result,
                "generated collider count",
                result.ColliderCount,
                DonorWorldCellizationPlan.ExpectedColliderCount);
            RequireEqual(
                result,
                "unique replacement-key count",
                allReplacementKeys.Count,
                DonorWorldCellizationPlan.ExpectedEntityCount);
        }

        private static void ValidateGeneratedScene(
            DonorWorldCellizationPlan plan,
            string ownerId,
            string scenePath,
            IReadOnlyDictionary<long, int[]> staticBatchSubsets,
            DonorWorldMaterialTextureAssets presentation,
            ISet<string> allEntityIds,
            ISet<string> allReplacementKeys,
            ISet<string> allColliderIds,
            DonorWorldCellizationValidationResult result)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    scenePath) == null)
            {
                result.Errors.Add(
                    "Generated scene is missing: " + scenePath);
                return;
            }

            ValidateGeneratedDependencies(scenePath, result);
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenPreviewScene(scenePath);
                GameObject[] roots = scene.GetRootGameObjects();
                if (roots.Length != 1)
                {
                    result.Errors.Add(
                        scenePath + " must have exactly one root.");
                    return;
                }

                GameObject root = roots[0];
                DonorWorldStreamingSceneMetadata[] stamps =
                    root.GetComponentsInChildren<
                        DonorWorldStreamingSceneMetadata>(
                        includeInactive: true);
                if (stamps.Length != 1)
                {
                    result.Errors.Add(
                        scenePath + " has an invalid scene metadata count.");
                    return;
                }

                DonorWorldStreamingSceneMetadata stamp = stamps[0];
                IReadOnlyList<DonorWorldCellizationAssignment> expected =
                    plan.GetOwnerEntries(ownerId);
                IReadOnlyList<DonorWorldSafeColliderRecord>
                    expectedColliders =
                        plan.GetCollidersForOwner(ownerId);
                bool isGlobal = string.Equals(
                    ownerId,
                    "global",
                    StringComparison.Ordinal);
                string expectedSceneId = isGlobal
                    ? "global-legacy"
                    : ownerId + "-legacy";
                DonorWorldStreamingOwnership expectedOwnership =
                    isGlobal
                        ? DonorWorldStreamingOwnership.GlobalLegacy
                        : DonorWorldStreamingOwnership.CellLegacy;
                string expectedOwnerCellId =
                    isGlobal ? string.Empty : ownerId;
                if (stamp.gameObject != root)
                {
                    result.Errors.Add(
                        scenePath +
                        " scene metadata is not attached to its root.");
                }
                if (root.transform.localPosition != Vector3.zero ||
                    root.transform.localRotation != Quaternion.identity ||
                    root.transform.localScale != Vector3.one)
                {
                    result.Errors.Add(
                        scenePath + " root transform is not identity.");
                }
                RequireEqual(
                    result,
                    scenePath + " generator version",
                    stamp.GeneratorVersion,
                    WorldBaseline06B2Paths.GeneratorVersion);
                RequireEqual(
                    result,
                    scenePath + " scene ID",
                    stamp.SceneId,
                    expectedSceneId);
                RequireEqual(
                    result,
                    scenePath + " ownership",
                    stamp.Ownership,
                    expectedOwnership);
                RequireEqual(
                    result,
                    scenePath + " owner cell ID",
                    stamp.OwnerCellId,
                    expectedOwnerCellId);
                RequireEqual(
                    result,
                    scenePath + " source revision",
                    stamp.SourceRevisionId,
                    WorldBaselinePaths.SourceRevisionId);
                RequireEqual(
                    result,
                    scenePath + " source scene hash",
                    stamp.SourceSceneSha256,
                    WorldBaselinePaths.SourceSceneSha256);
                RequireEqual(
                    result,
                    scenePath + " activation",
                    stamp.ActivationState,
                    DonorWorldBaselineActivationState
                        .ActiveFeatureParityProfile);
                RequireEqual(
                    result,
                    scenePath + " classification",
                    stamp.Classification,
                    DonorWorldBaselineClassification
                        .TemporaryDirectImport);
                RequireEqual(
                    result,
                    scenePath + " entity count stamp",
                    stamp.EntityCount,
                    expected.Count);
                RequireEqual(
                    result,
                    scenePath + " collider count stamp",
                    stamp.ColliderCount,
                    expectedColliders.Count);
                RequireEqual(
                    result,
                    scenePath + " ownership fingerprint",
                    stamp.OwnershipFingerprintSha256,
                    ComputeOwnerFingerprint(
                        plan,
                        ownerId,
                        expected,
                        expectedColliders));

                DonorWorldLegacyReplacementRegistry[] registries =
                    root.GetComponentsInChildren<
                        DonorWorldLegacyReplacementRegistry>(
                        includeInactive: true);
                int expectedRegistryCount =
                    ownerId == "global" ? 1 : 0;
                RequireEqual(
                    result,
                    scenePath + " replacement registry count",
                    registries.Length,
                    expectedRegistryCount);
                DonorWorldLegacyPresentationController[] controllers =
                    root.GetComponentsInChildren<
                        DonorWorldLegacyPresentationController>(
                        includeInactive: true);
                RequireEqual(
                    result,
                    scenePath + " presentation controller count",
                    controllers.Length,
                    0);

                ValidateComponentWhitelist(root, scenePath, result);
                if (root.GetComponentsInChildren<Light>(
                        includeInactive: true).Length != 0 ||
                    root.GetComponentsInChildren<Camera>(
                        includeInactive: true).Length != 0 ||
                    root.GetComponentsInChildren<AudioSource>(
                        includeInactive: true).Length != 0 ||
                    root.GetComponentsInChildren<Rigidbody>(
                        includeInactive: true).Length != 0 ||
                    root.GetComponentsInChildren<Joint>(
                        includeInactive: true).Length != 0 ||
                    root.GetComponentsInChildren<StableEntityIdAuthoring>(
                        includeInactive: true).Length != 0)
                {
                    result.Errors.Add(
                        scenePath + " contains forbidden lighting, camera, " +
                        "audio, physics-body, joint, or gameplay identity " +
                        "components.");
                }

                DonorWorldBaselineEntityMetadata[] entities =
                    root.GetComponentsInChildren<
                        DonorWorldBaselineEntityMetadata>(
                        includeInactive: true);
                Dictionary<string, DonorWorldCellizationAssignment>
                    expectedById = expected.ToDictionary(
                        assignment =>
                            assignment.SanitationEntry.Placement.StableId,
                        StringComparer.Ordinal);
                var localIds = new HashSet<string>(
                    StringComparer.Ordinal);
                int renderers = 0;
                foreach (DonorWorldBaselineEntityMetadata entity in
                         entities)
                {
                    if (!localIds.Add(entity.StableId) ||
                        !allEntityIds.Add(entity.StableId))
                    {
                        result.Errors.Add(
                            "Duplicate generated entity ID: " +
                            entity.StableId);
                        continue;
                    }
                    if (!allReplacementKeys.Add(entity.ReplacementKey))
                    {
                        result.Errors.Add(
                            "Duplicate generated replacement key: " +
                            entity.ReplacementKey);
                    }
                    if (!expectedById.TryGetValue(
                            entity.StableId,
                            out DonorWorldCellizationAssignment assignment))
                    {
                        result.Errors.Add(
                            scenePath + " contains an entity owned by a " +
                            "different cell: " + entity.StableId);
                        continue;
                    }
                    if (entity.GetComponents<DonorWorldBaselineEntityMetadata>()
                            .Length != 1)
                    {
                        result.Errors.Add(
                            "Entity has duplicate legacy metadata: " +
                            entity.StableId);
                    }
                    renderers += ValidateGeneratedEntity(
                        entity,
                        assignment.SanitationEntry,
                        staticBatchSubsets,
                        presentation,
                        result);
                }
                if (!localIds.SetEquals(expectedById.Keys))
                {
                    result.Errors.Add(
                        scenePath + " entity set differs from the " +
                        "deterministic plan.");
                }
                RequireEqual(
                    result,
                    scenePath + " renderer count stamp",
                    stamp.RendererCount,
                    renderers);
                result.RendererCount += renderers;

                DonorWorldBaselineColliderMetadata[] colliderMetadata =
                    root.GetComponentsInChildren<
                        DonorWorldBaselineColliderMetadata>(
                        includeInactive: true);
                Dictionary<string, DonorWorldSafeColliderRecord>
                    expectedColliderById = expectedColliders.ToDictionary(
                        collider => collider.ColliderStableId,
                        StringComparer.Ordinal);
                var localColliderIds = new HashSet<string>(
                    StringComparer.Ordinal);
                foreach (DonorWorldBaselineColliderMetadata metadata in
                         colliderMetadata)
                {
                    if (!localColliderIds.Add(
                            metadata.ColliderStableId) ||
                        !allColliderIds.Add(
                            metadata.ColliderStableId))
                    {
                        result.Errors.Add(
                            "Duplicate generated collider ID: " +
                            metadata.ColliderStableId);
                        continue;
                    }
                    if (!expectedColliderById.TryGetValue(
                            metadata.ColliderStableId,
                            out DonorWorldSafeColliderRecord record))
                    {
                        result.Errors.Add(
                            "Unexpected generated collider: " +
                            metadata.ColliderStableId);
                        continue;
                    }

                    ValidateCollider(
                        metadata,
                        record,
                        result);
                }
                if (!localColliderIds.SetEquals(
                        expectedColliderById.Keys))
                {
                    result.Errors.Add(
                        scenePath + " collider set differs from the " +
                        "committed allowlist.");
                }

                if (root.GetComponentsInChildren<Transform>(
                        includeInactive: true)
                    .Any(transform =>
                        transform.name.StartsWith(
                            "WR_Production_cell_",
                            StringComparison.Ordinal)))
                {
                    result.Errors.Add(
                        "Rejected prototype visual root leaked into " +
                        scenePath);
                }
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "Could not inspect generated scene " + scenePath +
                    ": " + exception.Message);
            }
            finally
            {
                if (scene.IsValid())
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        private static int ValidateGeneratedEntity(
            DonorWorldBaselineEntityMetadata entity,
            WorldBaselineSanitationEntry expected,
            IReadOnlyDictionary<long, int[]> staticBatchSubsets,
            DonorWorldMaterialTextureAssets presentation,
            DonorWorldCellizationValidationResult result)
        {
            string prefix = "Entity " + entity.StableId + ": ";
            if (!string.Equals(
                    entity.name,
                    "LegacyWorld_" + expected.Placement.StableId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    entity.ReplacementKey,
                    "legacy-world:" + expected.Placement.StableId,
                    StringComparison.Ordinal) ||
                entity.SourceObjectId !=
                    expected.Placement.SourceObjectId ||
                !string.Equals(
                    entity.SourceParentStableId,
                    expected.SourceParentStableId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    entity.SourceHierarchyPath,
                    expected.Placement.HierarchyPath,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    entity.SourceMeshGuid,
                    expected.Placement.MeshGuid,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    entity.SourceCellId,
                    expected.Placement.CellId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    entity.SemanticCategory,
                    expected.Placement.Category,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    entity.SourceComponentClassIds,
                    expected.ComponentClassIdsText,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    entity.SanitationDisposition,
                    expected.Disposition,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    entity.SanitationReason,
                    expected.Reason,
                    StringComparison.Ordinal) ||
                entity.SourceActiveSelf != expected.SourceActiveSelf ||
                entity.SourceActiveInHierarchy != expected.EffectiveActive ||
                entity.HasSanitizedRenderer != expected.IncludeRenderer ||
                entity.Classification !=
                    DonorWorldBaselineClassification.TemporaryDirectImport)
            {
                result.Errors.Add(
                    prefix +
                    "metadata differs from the deterministic sanitation plan.");
            }

            Transform transform = entity.transform;
            if (Vector3.Distance(
                    transform.position,
                    expected.Placement.Position) > 0.0001f ||
                Quaternion.Angle(
                    transform.rotation,
                    expected.Placement.Rotation) > 0.001f ||
                Vector3.Distance(
                    transform.localScale,
                    expected.Placement.Scale) > 0.0001f)
            {
                result.Errors.Add(
                    prefix + "source transform drifted.");
            }
            if (entity.gameObject.activeSelf != expected.EffectiveActive)
            {
                result.Errors.Add(
                    prefix + "effective-active state drifted.");
            }

            MeshRenderer[] renderers =
                entity.GetComponentsInChildren<MeshRenderer>(
                    includeInactive: true);
            MeshFilter[] filters =
                entity.GetComponentsInChildren<MeshFilter>(
                    includeInactive: true);
            int expectedRendererCount =
                expected.IncludeRenderer ? 1 : 0;
            if (renderers.Length != expectedRendererCount ||
                filters.Length != expectedRendererCount)
            {
                result.Errors.Add(
                    prefix + "renderer/filter ownership mismatch.");
                return renderers.Length;
            }

            if (!expected.IncludeRenderer)
            {
                return 0;
            }

            MeshRenderer renderer = renderers[0];
            MeshFilter filter = filters[0];
            if (renderer.gameObject != entity.gameObject ||
                filter.gameObject != entity.gameObject ||
                !renderer.enabled ||
                filter.sharedMesh == null)
            {
                result.Errors.Add(
                    prefix + "renderer/filter runtime contract is invalid.");
                return 1;
            }

            string expectedMeshPath =
                staticBatchSubsets.ContainsKey(
                    expected.Placement.SourceObjectId)
                    ? WorldBaselinePaths.DerivedMeshRoot + "/" +
                      expected.Placement.StableId + ".asset"
                    : WorldBaselinePaths.SourceMeshRoot + "/" +
                      expected.Placement.MeshGuid + ".asset";
            string actualMeshPath =
                AssetDatabase.GetAssetPath(filter.sharedMesh);
            if (!string.Equals(
                    actualMeshPath,
                    expectedMeshPath,
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    prefix + "sanitized render mesh path drifted.");
            }

            Material[] materials = renderer.sharedMaterials;
            int expectedMaterialCount =
                Mathf.Max(1, filter.sharedMesh.subMeshCount);
            string[] expectedSourceSlots =
                presentation.ResolveSourceMaterialSlots(
                    expected,
                    filter.sharedMesh.subMeshCount);
            Material[] expectedTextured =
                presentation.ResolveTexturedMaterials(
                    expectedSourceSlots);
            string expectedDiagnosticPath =
                WorldBaselinePaths.CategoryMaterial(
                    expected.Placement.Category);
            DonorWorldLegacyMaterialBinding[] bindings =
                entity.GetComponents<
                    DonorWorldLegacyMaterialBinding>();
            string bindingFailure = "invalid binding count";
            bool bindingConfigured =
                bindings.Length == 1 &&
                bindings[0].TryValidateConfiguration(
                    out bindingFailure);
            if (materials.Length != expectedMaterialCount ||
                bindings.Length != 1 ||
                !bindingConfigured ||
                bindings[0].TargetRenderer != renderer ||
                !bindings[0].SourceMaterialGuids.SequenceEqual(
                    expectedSourceSlots,
                    StringComparer.Ordinal) ||
                bindings[0].TexturedMaterials.Count !=
                    expectedMaterialCount ||
                bindings[0].DiagnosticMaterials.Count !=
                    expectedMaterialCount)
            {
                result.Errors.Add(
                    prefix +
                    "legacy material binding is missing or drifted: " +
                    bindingFailure);
                return 1;
            }

            for (int index = 0;
                 index < expectedMaterialCount;
                 index++)
            {
                Material actualTextured =
                    bindings[0].TexturedMaterials[index];
                Material actualDiagnostic =
                    bindings[0].DiagnosticMaterials[index];
                if (materials[index] != actualTextured ||
                    actualTextured != expectedTextured[index] ||
                    actualTextured == null ||
                    actualTextured.shader == null ||
                    string.Equals(
                        actualTextured.shader.name,
                        "Hidden/InternalErrorShader",
                        StringComparison.Ordinal) ||
                    !AssetDatabase.GetAssetPath(actualTextured)
                        .StartsWith(
                            WorldBaselinePaths.TexturedMaterialRoot +
                            "/",
                            StringComparison.Ordinal) ||
                    actualDiagnostic == null ||
                    !string.Equals(
                        AssetDatabase.GetAssetPath(
                            actualDiagnostic),
                        expectedDiagnosticPath,
                        StringComparison.Ordinal) ||
                    actualTextured.name.EndsWith(
                        " (Instance)",
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        prefix +
                        "textured/diagnostic material slot drifted at " +
                        index + ".");
                    break;
                }
            }

            return 1;
        }

        private static void ValidateCollider(
            DonorWorldBaselineColliderMetadata metadata,
            DonorWorldSafeColliderRecord record,
            DonorWorldCellizationValidationResult result)
        {
            string prefix =
                "Collider " + metadata.ColliderStableId + ": ";
            if (metadata.Classification !=
                DonorWorldBaselineClassification.TemporaryDirectImport ||
                !string.Equals(
                    metadata.EntityStableId,
                    record.EntityStableId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    metadata.ReplacementKey,
                    "legacy-world:" + record.EntityStableId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    metadata.SourceMeshGuid,
                    record.MeshGuid,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    metadata.SourceColliderType,
                    record.ColliderType,
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    prefix + "metadata differs from the allowlist.");
            }

            Transform transform = metadata.transform;
            DonorWorldBaselineEntityMetadata owner =
                metadata.GetComponentInParent<
                    DonorWorldBaselineEntityMetadata>();
            if (owner == null ||
                transform.parent != owner.transform ||
                !string.Equals(
                    owner.StableId,
                    record.EntityStableId,
                    StringComparison.Ordinal))
            {
                result.Errors.Add(
                    prefix + "is attached to the wrong legacy entity.");
            }
            if (transform.localPosition != Vector3.zero ||
                transform.localRotation != Quaternion.identity ||
                transform.localScale != Vector3.one)
            {
                result.Errors.Add(
                    prefix + "child transform is not identity.");
            }
            Collider[] colliders =
                metadata.GetComponents<Collider>();
            if (colliders.Length != 1 ||
                !colliders[0].enabled ||
                colliders[0].isTrigger ||
                colliders[0].gameObject.layer != 0)
            {
                result.Errors.Add(
                    prefix + "runtime collider contract is invalid.");
                return;
            }

            if (record.ColliderType == "MeshCollider")
            {
                MeshCollider collider =
                    colliders[0] as MeshCollider;
                string path = collider?.sharedMesh == null
                    ? string.Empty
                    : AssetDatabase.GetAssetPath(
                        collider.sharedMesh);
                if (collider == null ||
                    collider.convex ||
                    !string.Equals(
                        path,
                        WorldBaseline06B2Paths.CollisionMesh(
                            record.MeshGuid),
                        StringComparison.Ordinal))
                {
                    result.Errors.Add(
                        prefix + "MeshCollider does not use its sanitized " +
                        "collision mesh.");
                }
                else
                {
                    string sourcePath =
                        AssetDatabase.GUIDToAssetPath(record.MeshGuid);
                    if (!WorldReferenceMeshLibrarySync
                            .IsBelowReferenceMeshRoot(sourcePath) ||
                        AssetDatabase.LoadAssetAtPath<Mesh>(
                            sourcePath) == null)
                    {
                        result.Errors.Add(
                            prefix + "audited source collision mesh is " +
                            "missing.");
                    }
                    else
                    {
                        Mesh sourceMesh =
                            AssetDatabase.LoadAssetAtPath<Mesh>(
                                sourcePath);
                        if (!CollisionGeometryMatches(
                                sourceMesh,
                                collider.sharedMesh,
                                out string difference))
                        {
                            result.Errors.Add(
                                prefix + "sanitized collision geometry " +
                                "differs from the audited source mesh: " +
                                difference);
                        }
                    }
                }
            }
            else
            {
                BoxCollider collider =
                    colliders[0] as BoxCollider;
                if (collider == null ||
                    Vector3.Distance(
                        collider.center,
                        record.Center) > 0.0001f ||
                    Vector3.Distance(
                        collider.size,
                        record.Size) > 0.0001f)
                {
                    result.Errors.Add(
                        prefix + "BoxCollider shape drifted.");
                }
            }
        }

        private static bool CollisionGeometryMatches(
            Mesh source,
            Mesh destination,
            out string difference)
        {
            if (source == null || destination == null)
            {
                difference = "one of the mesh assets is missing.";
                return false;
            }
            if (source.vertexCount != destination.vertexCount)
            {
                difference =
                    $"vertex count {source.vertexCount} != " +
                    destination.vertexCount + ".";
                return false;
            }
            if (source.subMeshCount != destination.subMeshCount)
            {
                difference =
                    $"submesh count {source.subMeshCount} != " +
                    destination.subMeshCount + ".";
                return false;
            }

            try
            {
                Vector3[] sourceVertices = source.vertices;
                Vector3[] destinationVertices = destination.vertices;
                for (int index = 0;
                     index < sourceVertices.Length;
                     index++)
                {
                    if (sourceVertices[index] !=
                        destinationVertices[index])
                    {
                        difference =
                            "vertex data differs at index " + index + ".";
                        return false;
                    }
                }

                for (int subMesh = 0;
                     subMesh < source.subMeshCount;
                     subMesh++)
                {
                    MeshTopology sourceTopology =
                        source.GetTopology(subMesh);
                    MeshTopology destinationTopology =
                        destination.GetTopology(subMesh);
                    if (sourceTopology != destinationTopology)
                    {
                        difference =
                            $"submesh {subMesh} topology " +
                            $"{sourceTopology} != {destinationTopology}.";
                        return false;
                    }

                    int[] sourceIndices =
                        source.GetIndices(
                            subMesh,
                            applyBaseVertex: true);
                    int[] destinationIndices =
                        destination.GetIndices(
                            subMesh,
                            applyBaseVertex: true);
                    if (!sourceIndices.SequenceEqual(
                            destinationIndices))
                    {
                        difference =
                            "index data differs in submesh " +
                            subMesh + ".";
                        return false;
                    }
                }
            }
            catch (Exception exception)
            {
                difference =
                    "mesh data could not be read: " +
                    exception.Message;
                return false;
            }

            difference = string.Empty;
            return true;
        }

        private static void ValidateComponentWhitelist(
            GameObject root,
            string scenePath,
            DonorWorldCellizationValidationResult result)
        {
            foreach (Transform transform in
                     root.GetComponentsInChildren<Transform>(
                         includeInactive: true))
            {
                foreach (Component component in
                         transform.GetComponents<Component>())
                {
                    if (component == null)
                    {
                        result.Errors.Add(
                            scenePath + " has a missing script at " +
                            GetHierarchyPath(transform));
                    }
                    else if (!AllowedGeneratedTypes.Contains(
                                 component.GetType()))
                    {
                        result.Errors.Add(
                            scenePath + " has forbidden component " +
                            component.GetType().FullName + " at " +
                            GetHierarchyPath(transform));
                    }
                }
            }
        }

        private static void ValidateGeneratedDependencies(
            string scenePath,
            DonorWorldCellizationValidationResult result)
        {
            string[] dependencies =
                AssetDatabase.GetDependencies(
                    scenePath,
                    recursive: true);
            foreach (string dependency in dependencies)
            {
                string normalized =
                    dependency.Replace('\\', '/');
                foreach (string forbidden in
                         ForbiddenDependencyFragments)
                {
                    if (normalized.Contains(
                            forbidden,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        result.Errors.Add(
                            scenePath +
                            " has forbidden donor/reference dependency: " +
                            normalized);
                    }
                }
            }
        }

        private static void ValidateGeneratedPayloadTrackedState(
            DonorWorldCellizationValidationResult result)
        {
            try
            {
                string projectRoot =
                    Directory.GetParent(Application.dataPath)?.FullName ??
                    throw new InvalidOperationException(
                        "Project root is unavailable.");
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("ls-files");
                startInfo.ArgumentList.Add("--");
                startInfo.ArgumentList.Add(
                    "Assets/Game/LegacyImport/RuntimeBaseline/**");
                using Process process = Process.Start(startInfo) ??
                    throw new InvalidOperationException(
                        "Could not start git.");
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    result.Errors.Add(
                        "Could not inspect tracked RuntimeBaseline state: " +
                        error.Trim());
                    return;
                }

                string[] tracked = output
                    .Split(
                        new[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries)
                    .Where(path => !string.Equals(
                        path,
                        "Assets/Game/LegacyImport/RuntimeBaseline/.gitkeep",
                        StringComparison.Ordinal))
                    .ToArray();
                if (tracked.Length > 0)
                {
                    result.Errors.Add(
                        "Generated donor RuntimeBaseline payload is tracked " +
                        "by Git: " + string.Join(", ", tracked));
                }
            }
            catch (Exception exception)
            {
                result.Errors.Add(
                    "Tracked-payload validation failed: " +
                    exception.Message);
            }
        }

        private static void ValidateRequiredExports(
            DonorWorldCellizationPlan plan,
            DonorWorldCellizationValidationResult result)
        {
            string[] required =
            {
                WorldBaseline06B2Paths.OwnershipManifest,
                WorldBaseline06B2Paths.OwnershipMatrix,
                WorldBaseline06B2Paths.MaterialTextureManifest,
                WorldBaseline06B2Paths.MaterialShaderMapping,
                WorldBaseline06B2Paths.VisualCompletenessReport,
                WorldBaseline06B2Paths.TextureMemoryBaseline,
                WorldBaseline06B2Paths.PresentationSourceManifest
            };
            foreach (string path in required)
            {
                if (!File.Exists(
                        WorldBaselinePaths.ToAbsoluteProjectPath(path)))
                {
                    result.Errors.Add(
                        "Required 06B2 ownership export is missing: " +
                        path);
                }
            }

            if (result.Errors.Any(error =>
                    error.StartsWith(
                        "Required 06B2 ownership export is missing:",
                        StringComparison.Ordinal)))
            {
                return;
            }

            ValidateOwnershipManifest(plan, result);
            ValidateOwnershipMatrix(plan, result);
        }

        private static void ValidateOwnershipManifest(
            DonorWorldCellizationPlan plan,
            DonorWorldCellizationValidationResult result)
        {
            string path = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaseline06B2Paths.OwnershipManifest);
            using var reader = new StreamReader(path);
            List<string> headers = WorldEntityTable.ParseRow(
                reader.ReadLine() ?? string.Empty);
            string[] required =
            {
                "LegacyWorldObjectId", "ReplacementKey",
                "SourceObjectId", "SourceHierarchyPath",
                "SemanticCategory", "SourceCellId", "AssignedOwner",
                "OwnershipReason", "HasRenderer", "EffectiveActive",
                "ColliderStableId", "Classification", "SourceRevisionId",
                "SourceMaterialGuids",
                "LegacyTexturedMaterialPaths",
                "LegacyDiagnosticMaterialPath",
                "PresentationStatus"
            };
            Dictionary<string, int> indices =
                BuildColumnIndices(
                    headers,
                    required,
                    "ownership manifest",
                    result);
            if (indices.Count == 0)
            {
                return;
            }

            Dictionary<string, DonorWorldCellizationAssignment> expected =
                plan.Assignments.ToDictionary(
                    assignment =>
                        assignment.SanitationEntry.Placement.StableId,
                    StringComparer.Ordinal);
            Dictionary<string, string> colliderByEntity =
                plan.SafeColliders.ToDictionary(
                    collider => collider.EntityStableId,
                    collider => collider.ColliderStableId,
                    StringComparer.Ordinal);
            IReadOnlyDictionary<long, int[]> staticBatchSubsets =
                WorldStaticBatchSubsetTable.ParseCommittedTable();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string line;
            int lineNumber = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> values = WorldEntityTable.ParseRow(line);
                if (values.Count != headers.Count)
                {
                    result.Errors.Add(
                        "Ownership manifest line " + lineNumber +
                        " has an invalid column count.");
                    continue;
                }

                string Get(string column) => values[indices[column]];
                string id = Get("LegacyWorldObjectId");
                if (!seen.Add(id) ||
                    !expected.TryGetValue(
                        id,
                        out DonorWorldCellizationAssignment assignment))
                {
                    result.Errors.Add(
                        "Ownership manifest has an unexpected or duplicate " +
                        "entity at line " + lineNumber + ": " + id);
                    continue;
                }

                WorldBaselineSanitationEntry entry =
                    assignment.SanitationEntry;
                colliderByEntity.TryGetValue(
                    id,
                    out string colliderId);
                string[] effectiveMaterialSlots = Array.Empty<string>();
                if (entry.IncludeRenderer)
                {
                    string meshPath =
                        staticBatchSubsets.ContainsKey(
                            entry.Placement.SourceObjectId)
                            ? WorldBaselinePaths.DerivedMeshRoot + "/" +
                              entry.Placement.StableId + ".asset"
                            : WorldBaselinePaths.SourceMeshRoot + "/" +
                              entry.Placement.MeshGuid + ".asset";
                    Mesh mesh =
                        AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (mesh == null)
                    {
                        result.Errors.Add(
                            "Ownership manifest validation cannot load " +
                            "sanitized mesh for entity " + id + ".");
                        effectiveMaterialSlots =
                            entry.SourceMaterialGuids;
                    }
                    else
                    {
                        try
                        {
                            effectiveMaterialSlots =
                                DonorWorldMaterialTexturePlan
                                    .ResolveSourceMaterialSlots(
                                        entry,
                                        mesh.subMeshCount);
                        }
                        catch (Exception exception)
                        {
                            result.Errors.Add(
                                "Ownership manifest material-slot " +
                                "resolution failed for entity " + id +
                                ": " + exception.Message);
                            effectiveMaterialSlots =
                                entry.SourceMaterialGuids;
                        }
                    }
                }
                string[] actual =
                {
                    Get("ReplacementKey"),
                    Get("SourceObjectId"),
                    Get("SourceHierarchyPath"),
                    Get("SemanticCategory"),
                    Get("SourceCellId"),
                    Get("AssignedOwner"),
                    Get("OwnershipReason"),
                    Get("HasRenderer"),
                    Get("EffectiveActive"),
                    Get("ColliderStableId"),
                    Get("Classification"),
                    Get("SourceRevisionId"),
                    Get("SourceMaterialGuids"),
                    Get("LegacyTexturedMaterialPaths"),
                    Get("LegacyDiagnosticMaterialPath"),
                    Get("PresentationStatus")
                };
                string[] expectedValues =
                {
                    "legacy-world:" + id,
                    entry.Placement.SourceObjectId.ToString(
                        CultureInfo.InvariantCulture),
                    entry.Placement.HierarchyPath,
                    entry.Placement.Category,
                    entry.Placement.CellId,
                    assignment.OwnerId,
                    assignment.OwnershipReason,
                    entry.IncludeRenderer ? "1" : "0",
                    entry.EffectiveActive ? "1" : "0",
                    colliderId ?? string.Empty,
                    "TemporaryDirectImport",
                    WorldBaselinePaths.SourceRevisionId,
                    string.Join(";", effectiveMaterialSlots),
                    entry.IncludeRenderer
                        ? string.Join(
                            ";",
                            effectiveMaterialSlots.Select(
                                materialGuid =>
                                    string.Equals(
                                        materialGuid,
                                        DonorWorldMaterialTexturePlan
                                            .BuiltInFallbackGuid,
                                        StringComparison.Ordinal)
                                        ? WorldBaselinePaths
                                            .UnsupportedMaterial
                                        : WorldBaselinePaths
                                            .TexturedMaterial(
                                                materialGuid)))
                        : string.Empty,
                    entry.IncludeRenderer
                        ? WorldBaselinePaths.CategoryMaterial(
                            entry.Placement.Category)
                        : string.Empty,
                    entry.IncludeRenderer
                        ? "LegacyTexturedActive;LegacyDiagnosticAvailable"
                        : "MetadataOnly"
                };
                if (!actual.SequenceEqual(
                        expectedValues,
                        StringComparer.Ordinal))
                {
                    result.Errors.Add(
                        "Ownership manifest row drifted for entity " + id);
                }
            }

            if (!seen.SetEquals(expected.Keys))
            {
                result.Errors.Add(
                    "Ownership manifest entity set differs from the " +
                    "deterministic plan.");
            }
        }

        private static void ValidateOwnershipMatrix(
            DonorWorldCellizationPlan plan,
            DonorWorldCellizationValidationResult result)
        {
            string path = WorldBaselinePaths.ToAbsoluteProjectPath(
                WorldBaseline06B2Paths.OwnershipMatrix);
            using var reader = new StreamReader(path);
            List<string> headers = WorldEntityTable.ParseRow(
                reader.ReadLine() ?? string.Empty);
            string[] required =
            {
                "OwnershipClass", "Rule", "EntityCount",
                "RendererCount", "ColliderCount", "Reason",
                "FutureAction", "PresentationAssets",
                "PresentationPolicy"
            };
            Dictionary<string, int> indices =
                BuildColumnIndices(
                    headers,
                    required,
                    "ownership matrix",
                    result);
            if (indices.Count == 0)
            {
                return;
            }

            Dictionary<string, DonorWorldCellizationAssignment[]> expected =
                plan.Assignments
                    .GroupBy(
                        assignment => assignment.OwnershipReason,
                        StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.ToArray(),
                        StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string line;
            int lineNumber = 1;
            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                List<string> values = WorldEntityTable.ParseRow(line);
                if (values.Count != headers.Count)
                {
                    result.Errors.Add(
                        "Ownership matrix line " + lineNumber +
                        " has an invalid column count.");
                    continue;
                }

                string Get(string column) => values[indices[column]];
                string rule = Get("Rule");
                if (!seen.Add(rule) ||
                    !expected.TryGetValue(
                        rule,
                        out DonorWorldCellizationAssignment[] assignments))
                {
                    result.Errors.Add(
                        "Ownership matrix has an unexpected or duplicate " +
                        "rule at line " + lineNumber + ": " + rule);
                    continue;
                }

                HashSet<string> ids = assignments
                    .Select(assignment =>
                        assignment.SanitationEntry.Placement.StableId)
                    .ToHashSet(StringComparer.Ordinal);
                string ownershipClass =
                    assignments[0].IsGlobal
                        ? "GlobalLegacy"
                        : "CellLegacy";
                string[] actual =
                {
                    Get("OwnershipClass"),
                    Get("EntityCount"),
                    Get("RendererCount"),
                    Get("ColliderCount")
                };
                string[] expectedValues =
                {
                    ownershipClass,
                    assignments.Length.ToString(
                        CultureInfo.InvariantCulture),
                    assignments.Count(assignment =>
                            assignment.SanitationEntry.IncludeRenderer)
                        .ToString(CultureInfo.InvariantCulture),
                    plan.SafeColliders.Count(collider =>
                            ids.Contains(collider.EntityStableId))
                        .ToString(CultureInfo.InvariantCulture)
                };
                if (!actual.SequenceEqual(
                        expectedValues,
                        StringComparer.Ordinal) ||
                    string.IsNullOrWhiteSpace(Get("Reason")) ||
                    string.IsNullOrWhiteSpace(Get("FutureAction")) ||
                    string.IsNullOrWhiteSpace(
                        Get("PresentationAssets")) ||
                    string.IsNullOrWhiteSpace(
                        Get("PresentationPolicy")))
                {
                    result.Errors.Add(
                        "Ownership matrix row drifted for rule " + rule);
                }
            }

            if (!seen.SetEquals(expected.Keys))
            {
                result.Errors.Add(
                    "Ownership matrix rule set differs from the " +
                    "deterministic plan.");
            }
        }

        private static Dictionary<string, int> BuildColumnIndices(
            IReadOnlyList<string> headers,
            IEnumerable<string> required,
            string label,
            DonorWorldCellizationValidationResult result)
        {
            var indices = new Dictionary<string, int>(
                StringComparer.Ordinal);
            for (int index = 0; index < headers.Count; index++)
            {
                indices[headers[index]] = index;
            }
            foreach (string column in required)
            {
                if (!indices.ContainsKey(column))
                {
                    result.Errors.Add(
                        "06B2 " + label + " lacks column " + column + ".");
                    return new Dictionary<string, int>(
                        StringComparer.Ordinal);
                }
            }

            return indices;
        }

        private static T[] GetSceneComponents<T>(Scene scene)
            where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<T>(
                        includeInactive: true))
                .ToArray();

        private static string GetHierarchyPath(Transform transform)
        {
            var names = new Stack<string>();
            for (Transform current = transform;
                 current != null;
                 current = current.parent)
            {
                names.Push(current.name);
            }

            return string.Join("/", names);
        }

        private static string ComputeOwnerFingerprint(
            DonorWorldCellizationPlan plan,
            string ownerId,
            IEnumerable<DonorWorldCellizationAssignment> assignments,
            IEnumerable<DonorWorldSafeColliderRecord> colliders)
        {
            var builder = new StringBuilder();
            builder.Append(plan.OwnershipFingerprintSha256)
                .Append('|')
                .Append(ownerId)
                .Append('\n');
            foreach (DonorWorldCellizationAssignment assignment in
                     assignments)
            {
                builder.Append(
                        assignment.SanitationEntry.Placement.StableId)
                    .Append('\n');
            }
            foreach (DonorWorldSafeColliderRecord collider in colliders)
            {
                builder.Append(collider.ColliderStableId)
                    .Append('\n');
            }

            return DonorWorldBaselineManifest.Sha256Text(
                builder.ToString());
        }

        private static void RequireEqual<T>(
            DonorWorldCellizationValidationResult result,
            string label,
            T actual,
            T expected)
        {
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
            {
                result.Errors.Add(
                    $"{label} mismatch: actual='{actual}', " +
                    $"expected='{expected}'.");
            }
        }

        private static void Log(
            DonorWorldCellizationValidationResult result)
        {
            foreach (string warning in result.Warnings)
            {
                Debug.LogWarning(
                    "DONOR_WORLD_CELLIZATION_06B2_WARNING " +
                    warning);
            }
            if (result.Passed)
            {
                Debug.Log(
                    "DONOR_WORLD_CELLIZATION_06B2_VALIDATION_OK " +
                    $"scenes={result.SceneCount} " +
                    $"entities={result.EntityCount} " +
                    $"renderers={result.RendererCount} " +
                    $"colliders={result.ColliderCount} " +
                    $"anchors={result.GameplayAnchorCount} " +
                    $"ownershipFingerprint=" +
                    result.OwnershipFingerprintSha256);
            }
            else
            {
                Debug.LogError(
                    "DONOR_WORLD_CELLIZATION_06B2_VALIDATION_FAILED\n- " +
                    string.Join("\n- ", result.Errors));
            }
        }
    }
}
