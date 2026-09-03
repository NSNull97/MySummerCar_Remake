using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MSC.Bootstrap;
using MSC.Editor.WorldStreaming;
using MSC.Editor.WorldTransfer;
using MSC.LegacyImport;
using MSC.World.Partition;
using MSC.World.Streaming;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MSC.Editor.WorldBaseline
{
    public static class DonorWorldCellizationBuilder
    {
        private const string GlobalRootName = "World_Global_Legacy";
        private const string ContentRootName =
            "TEMPORARY_DIRECT_IMPORT_ENTITIES";

        [MenuItem(
            "Tools/MSC Remake/World Baseline 06B2/" +
            "Build Active Donor Streaming Profile")]
        public static void Build()
        {
            if (!Application.isBatchMode &&
                !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                throw new OperationCanceledException(
                    "06B2 cellization was cancelled to preserve unsaved scenes.");
            }

            AssertEntryGate();
            DonorWorldCellizationPlan plan =
                DonorWorldCellizationPlan.Load();
            DonorWorldMaterialTexturePlan presentationPlan =
                DonorWorldMaterialTexturePlan.Load();
            SceneSetup[] previousSetup =
                EditorSceneManager.GetSceneManagerSetup();
            bool restoreSetup =
                !Application.isBatchMode && previousSetup.Length > 0;
            try
            {
                EnsureFolders();
                DonorWorldMaterialTextureAssets presentation =
                    DonorWorldMaterialTexturePipeline.Build(
                        presentationPlan);
                IReadOnlyDictionary<string, Mesh> collisionMeshes =
                    SynchronizeCollisionMeshes(plan.SafeColliders);
                GenerateStreamingScenes(
                    plan,
                    collisionMeshes,
                    presentation);

                ProductionWorldStreamingBuilder.Build();
                WorldGameplayCellCatalog gameplayCatalog =
                    BuildGameplayCatalog();
                EnsureFeatureParityBuildSettings(plan);
                ProductionWorldStreamingManifest activeManifest =
                    CreateOrUpdateActiveManifest(plan, gameplayCatalog);
                WireActiveBootstrap(activeManifest);
                WriteOwnershipExports(plan, presentationPlan);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);

                DonorWorldCellizationValidationResult validation =
                    DonorWorldCellizationValidator.Validate(
                        verifySourceHashes: true,
                        inspectAllGeneratedScenes: true);
                if (!validation.Passed)
                {
                    throw new InvalidOperationException(
                        "06B2 generated profile failed validation:\n- " +
                        string.Join("\n- ", validation.Errors));
                }

                Debug.Log(
                    "DONOR_WORLD_CELLIZATION_06B2_BUILD_OK " +
                    $"entities={plan.Assignments.Count} " +
                    $"global={plan.GetOwnerEntries("global").Count} " +
                    $"cellOwned={plan.Assignments.Count - plan.GetOwnerEntries("global").Count} " +
                    $"cells={plan.CellIds.Count} " +
                    $"colliders={plan.SafeColliders.Count} " +
                    $"anchors={gameplayCatalog.Anchors.Count} " +
                    $"ownershipFingerprint={plan.OwnershipFingerprintSha256}");
            }
            finally
            {
                if (restoreSetup)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(
                        previousSetup);
                }
            }
        }

        [MenuItem(
            "Tools/MSC Remake/World Baseline 06B2/" +
            "Dry Run + Export Ownership")]
        public static void DryRunAndExportOwnership()
        {
            AssertEntryGate();
            DonorWorldCellizationPlan plan =
                DonorWorldCellizationPlan.Load();
            DonorWorldMaterialTexturePlan presentationPlan =
                DonorWorldMaterialTexturePlan.Load();
            WriteOwnershipExports(plan, presentationPlan);
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);
            Debug.Log(
                "DONOR_WORLD_CELLIZATION_06B2_DRY_RUN_OK " +
                $"entities={plan.Assignments.Count} " +
                $"global={plan.GetOwnerEntries("global").Count} " +
                $"cells={plan.CellIds.Count} " +
                $"colliders={plan.SafeColliders.Count} " +
                $"ownershipFingerprint={plan.OwnershipFingerprintSha256}");
        }

        public static void RunBatch() => Build();

        private static void AssertEntryGate()
        {
            DonorWorldBaselineManifest.AssertCanonicalFrozenInputsMatchRevision();
            DonorWorldBaselineManifest.AssertExistingSourceLockUnchanged();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    WorldBaselinePaths.CanonicalScene) == null)
            {
                throw new FileNotFoundException(
                    "06B2 requires the completed canonical 06B1 scene.",
                    WorldBaselinePaths.ToAbsoluteProjectPath(
                        WorldBaselinePaths.CanonicalScene));
            }

            DonorWorldBaselineValidationResult baseline =
                DonorWorldBaselineValidator.Validate(
                    verifySourceHashes: true);
            if (!baseline.IsValid)
            {
                throw new InvalidOperationException(
                    "06B1 entry gate failed:\n- " +
                    string.Join("\n- ", baseline.Errors));
            }
        }

        private static IReadOnlyDictionary<string, Mesh>
            SynchronizeCollisionMeshes(
                IReadOnlyList<DonorWorldSafeColliderRecord> colliders)
        {
            string[] meshGuids = colliders
                .Where(collider =>
                    collider.ColliderType == "MeshCollider")
                .Select(collider => collider.MeshGuid)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            WorldReferenceMeshSyncResult sync =
                WorldReferenceMeshLibrarySync.SynchronizeGuids(meshGuids);
            if (sync.MissingGuids.Count > 0 ||
                sync.ResolvedAssetCount != meshGuids.Length)
            {
                throw new InvalidOperationException(
                    "06B2 collider mesh sync is incomplete. Missing: " +
                    string.Join(", ", sync.MissingGuids));
            }

            var expectedDestinations = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (string meshGuid in meshGuids)
            {
                string sourcePath =
                    AssetDatabase.GUIDToAssetPath(meshGuid);
                if (!WorldReferenceMeshLibrarySync.IsBelowReferenceMeshRoot(
                        sourcePath) ||
                    AssetDatabase.LoadAssetAtPath<Mesh>(sourcePath) == null)
                {
                    throw new InvalidDataException(
                        "Collider mesh GUID does not resolve inside the " +
                        "audited reference library: " + meshGuid);
                }

                string destination =
                    WorldBaseline06B2Paths.CollisionMesh(meshGuid);
                expectedDestinations.Add(destination);
                CopyAssetIfChanged(sourcePath, destination);
            }

            PruneAssets(
                WorldBaseline06B2Paths.CollisionMeshRoot,
                expectedDestinations,
                "t:Mesh");
            AssetDatabase.Refresh(
                ImportAssetOptions.ForceSynchronousImport |
                ImportAssetOptions.ForceUpdate);

            var result = new Dictionary<string, Mesh>(
                StringComparer.Ordinal);
            foreach (string meshGuid in meshGuids)
            {
                string destination =
                    WorldBaseline06B2Paths.CollisionMesh(meshGuid);
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(
                    destination);
                if (mesh == null ||
                    mesh.vertexCount <= 0 ||
                    mesh.subMeshCount <= 0)
                {
                    throw new InvalidDataException(
                        "Sanitized collider mesh is invalid: " +
                        destination);
                }

                result.Add(meshGuid, mesh);
            }

            return result;
        }

        private static void GenerateStreamingScenes(
            DonorWorldCellizationPlan plan,
            IReadOnlyDictionary<string, Mesh> collisionMeshes,
            DonorWorldMaterialTextureAssets presentation)
        {
            var expectedScenes = new HashSet<string>(
                StringComparer.Ordinal)
            {
                WorldBaseline06B2Paths.GlobalScene
            };
            foreach (string cellId in plan.CellIds)
            {
                expectedScenes.Add(
                    WorldBaseline06B2Paths.CellScene(cellId));
            }

            PruneAssets(
                WorldBaseline06B2Paths.StreamingSceneRoot,
                expectedScenes,
                "t:Scene");

            IReadOnlyDictionary<long, int[]> staticBatchSubsets =
                WorldStaticBatchSubsetTable.ParseCommittedTable();
            GenerateOwnerScene(
                plan,
                "global",
                WorldBaseline06B2Paths.GlobalScene,
                collisionMeshes,
                staticBatchSubsets,
                presentation);
            foreach (string cellId in plan.CellIds)
            {
                GenerateOwnerScene(
                    plan,
                    cellId,
                    WorldBaseline06B2Paths.CellScene(cellId),
                    collisionMeshes,
                    staticBatchSubsets,
                    presentation);
            }
        }

        private static void GenerateOwnerScene(
            DonorWorldCellizationPlan plan,
            string ownerId,
            string scenePath,
            IReadOnlyDictionary<string, Mesh> collisionMeshes,
            IReadOnlyDictionary<long, int[]> staticBatchSubsets,
            DonorWorldMaterialTextureAssets presentation)
        {
            IReadOnlyList<DonorWorldCellizationAssignment> assignments =
                plan.GetOwnerEntries(ownerId);
            IReadOnlyList<DonorWorldSafeColliderRecord> ownerColliders =
                plan.GetCollidersForOwner(ownerId);
            ILookup<string, DonorWorldSafeColliderRecord> collidersByEntity =
                ownerColliders.ToLookup(
                    collider => collider.EntityStableId,
                    StringComparer.Ordinal);

            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            ConfigureNoLightingSceneEnvironment();
            string sceneId = string.Equals(
                ownerId,
                "global",
                StringComparison.Ordinal)
                ? "global-legacy"
                : ownerId + "-legacy";
            string rootName = string.Equals(
                ownerId,
                "global",
                StringComparison.Ordinal)
                ? GlobalRootName
                : "World_" + ownerId + "_Legacy";
            var root = new GameObject(rootName);
            var metadata =
                root.AddComponent<DonorWorldStreamingSceneMetadata>();
            if (string.Equals(
                    ownerId,
                    "global",
                    StringComparison.Ordinal))
            {
                root.AddComponent<DonorWorldLegacyReplacementRegistry>();
            }

            var content = new GameObject(ContentRootName);
            content.transform.SetParent(root.transform, false);

            int rendererCount = 0;
            foreach (DonorWorldCellizationAssignment assignment in
                     assignments)
            {
                WorldBaselineSanitationEntry entry =
                    assignment.SanitationEntry;
                var entity = new GameObject(
                    DonorWorldBaselineDisplayName.Create(
                        entry.Placement.HierarchyPath,
                        entry.Placement.SourceObjectId,
                        entry.Placement.Category));
                entity.layer = 0;
                entity.transform.SetParent(content.transform, false);
                entity.transform.SetPositionAndRotation(
                    entry.Placement.Position,
                    entry.Placement.Rotation);
                entity.transform.localScale = entry.Placement.Scale;

                entity.AddComponent<DonorWorldBaselineEntityMetadata>()
                    .Configure(
                        entry.Placement.StableId,
                        entry.Placement.SourceObjectId,
                        entry.SourceParentStableId,
                        entry.Placement.HierarchyPath,
                        entry.Placement.MeshGuid,
                        entry.Placement.CellId,
                        entry.Placement.Category,
                        entry.ComponentClassIdsText,
                        entry.Disposition,
                        entry.Reason,
                        entry.SourceActiveSelf,
                        entry.EffectiveActive,
                        entry.IncludeRenderer);

                if (entry.IncludeRenderer)
                {
                    Mesh mesh = LoadSanitizedRenderMesh(
                        entry,
                        staticBatchSubsets);
                    entity.AddComponent<MeshFilter>().sharedMesh = mesh;
                    MeshRenderer renderer =
                        entity.AddComponent<MeshRenderer>();
                    Material diagnosticMaterial =
                        AssetDatabase.LoadAssetAtPath<Material>(
                            WorldBaselinePaths.CategoryMaterial(
                                entry.Placement.Category));
                    if (diagnosticMaterial == null)
                    {
                        throw new FileNotFoundException(
                            "06B1 category material is missing.",
                            WorldBaselinePaths.ToAbsoluteProjectPath(
                                WorldBaselinePaths.CategoryMaterial(
                                    entry.Placement.Category)));
                    }

                    string[] sourceMaterialSlots =
                        presentation.ResolveSourceMaterialSlots(
                            entry,
                            mesh.subMeshCount);
                    Material[] texturedMaterials =
                        presentation.ResolveTexturedMaterials(
                            sourceMaterialSlots);
                    Material[] diagnosticMaterials =
                        Enumerable.Repeat(
                                diagnosticMaterial,
                                sourceMaterialSlots.Length)
                            .ToArray();
                    renderer.sharedMaterials = texturedMaterials;
                    var binding = entity.AddComponent<
                        DonorWorldLegacyMaterialBinding>();
                    binding.Configure(
                        renderer,
                        sourceMaterialSlots,
                        texturedMaterials,
                        diagnosticMaterials);
                    if (!binding.Apply(
                            DonorWorldLegacyPresentationMode
                                .LegacyTextured))
                    {
                        throw new InvalidOperationException(
                            "Could not apply LegacyTextured materials for " +
                            entry.Placement.StableId);
                    }
                    DonorWorldRendererCompatibilityPolicy.Apply(
                        renderer,
                        entry,
                        sourceMaterialSlots,
                        presentation.Plan);
                    renderer.motionVectorGenerationMode =
                        MotionVectorGenerationMode.ForceNoMotion;
                    renderer.allowOcclusionWhenDynamic = true;
                    rendererCount++;
                }

                foreach (DonorWorldSafeColliderRecord colliderRecord in
                         collidersByEntity[entry.Placement.StableId])
                {
                    AddCollider(
                        entity.transform,
                        colliderRecord,
                        collisionMeshes);
                }

                entity.SetActive(entry.EffectiveActive);
            }

            string ownerFingerprint = ComputeOwnerFingerprint(
                plan,
                ownerId,
                assignments,
                ownerColliders);
            metadata.Configure(
                WorldBaseline06B2Paths.GeneratorVersion,
                sceneId,
                string.Equals(
                    ownerId,
                    "global",
                    StringComparison.Ordinal)
                    ? DonorWorldStreamingOwnership.GlobalLegacy
                    : DonorWorldStreamingOwnership.CellLegacy,
                string.Equals(
                    ownerId,
                    "global",
                    StringComparison.Ordinal)
                    ? string.Empty
                    : ownerId,
                WorldBaselinePaths.SourceRevisionId,
                WorldBaselinePaths.SourceSceneSha256,
                assignments.Count,
                rendererCount,
                ownerColliders.Count,
                ownerFingerprint);

            if (!EditorSceneManager.SaveScene(scene, scenePath))
            {
                throw new IOException(
                    "Could not save generated 06B2 scene: " +
                    scenePath);
            }
        }

        private static Mesh LoadSanitizedRenderMesh(
            WorldBaselineSanitationEntry entry,
            IReadOnlyDictionary<long, int[]> staticBatchSubsets)
        {
            string path = staticBatchSubsets.ContainsKey(
                entry.Placement.SourceObjectId)
                ? WorldBaselinePaths.DerivedMeshRoot + "/" +
                  entry.Placement.StableId + ".asset"
                : WorldBaselinePaths.SourceMeshRoot + "/" +
                  entry.Placement.MeshGuid + ".asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                throw new FileNotFoundException(
                    "06B1 sanitized render mesh is missing.",
                    WorldBaselinePaths.ToAbsoluteProjectPath(path));
            }

            return mesh;
        }

        private static void AddCollider(
            Transform entity,
            DonorWorldSafeColliderRecord record,
            IReadOnlyDictionary<string, Mesh> collisionMeshes)
        {
            var child = new GameObject(
                "COLLIDER_" + record.ColliderStableId);
            int collisionLayer = LayerMask.NameToLayer(
                record.CollisionLayerName);
            if (collisionLayer < 0)
            {
                throw new InvalidDataException(
                    "Required collision layer is missing: " +
                    record.CollisionLayerName);
            }
            child.layer = collisionLayer;
            child.transform.SetParent(entity, false);
            child.AddComponent<DonorWorldBaselineColliderMetadata>()
                .Configure(
                    record.ColliderStableId,
                    record.EntityStableId,
                    record.MeshGuid,
                    record.ColliderType,
                    record.Disposition,
                    record.CollisionLayerName,
                    record.PhysicsMaterialAssetPath,
                    record.IsSafetyCritical);

            PhysicsMaterial physicsMaterial =
                AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(
                    record.PhysicsMaterialAssetPath);
            if (physicsMaterial == null)
            {
                throw new FileNotFoundException(
                    "Project-owned collision PhysicsMaterial is missing.",
                    WorldBaselinePaths.ToAbsoluteProjectPath(
                        record.PhysicsMaterialAssetPath));
            }

            if (record.ColliderType == "MeshCollider")
            {
                if (!collisionMeshes.TryGetValue(
                        record.MeshGuid,
                        out Mesh mesh) ||
                    mesh == null)
                {
                    throw new InvalidDataException(
                        "Sanitized collider mesh is missing for " +
                        record.ColliderStableId);
                }

                MeshCollider collider =
                    child.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
                collider.convex = record.RuntimeConvex;
                collider.sharedMaterial = physicsMaterial;
                collider.isTrigger = false;
                collider.enabled = true;
            }
            else if (record.ColliderType == "BoxCollider")
            {
                BoxCollider collider =
                    child.AddComponent<BoxCollider>();
                collider.center = record.Center;
                collider.size = record.Size;
                collider.sharedMaterial = physicsMaterial;
                collider.isTrigger = false;
                collider.enabled = true;
            }
            else if (record.ColliderType == "CapsuleCollider")
            {
                CapsuleCollider collider =
                    child.AddComponent<CapsuleCollider>();
                collider.center = record.Center;
                collider.radius = record.Radius;
                collider.height = record.Height;
                collider.direction = record.Direction;
                collider.sharedMaterial = physicsMaterial;
                collider.isTrigger = false;
                collider.enabled = true;
            }
            else if (record.ColliderType == "SphereCollider")
            {
                SphereCollider collider =
                    child.AddComponent<SphereCollider>();
                collider.center = record.Center;
                collider.radius = record.Radius;
                collider.sharedMaterial = physicsMaterial;
                collider.isTrigger = false;
                collider.enabled = true;
            }
            else
            {
                throw new InvalidDataException(
                    "Unsupported safe collider type: " +
                    record.ColliderType);
            }
        }

        private static WorldGameplayCellCatalog BuildGameplayCatalog()
        {
            WorldGameplayAnchorManifestData manifest =
                WorldGameplayAnchorManifest.Load();
            WorldGameplayAnchorRecord[] ordered =
                manifest.Records.ToArray();

            EnsureAssetFolder(
                WorldBaseline06B2Paths.GameplayCatalog);
            WorldGameplayCellCatalog catalog =
                AssetDatabase.LoadAssetAtPath<WorldGameplayCellCatalog>(
                    WorldBaseline06B2Paths.GameplayCatalog);
            if (catalog == null)
            {
                catalog =
                    ScriptableObject.CreateInstance<
                        WorldGameplayCellCatalog>();
                catalog.name = "WorldGameplayCellCatalog";
                AssetDatabase.CreateAsset(
                    catalog,
                    WorldBaseline06B2Paths.GameplayCatalog);
            }

            catalog.ConfigureForAuthoring(
                WorldBaseline06B2Paths.GameplayCatalogId,
                ordered);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void EnsureFeatureParityBuildSettings(
            DonorWorldCellizationPlan plan)
        {
            var expectedGenerated = new List<string>
            {
                WorldBaseline06B2Paths.GlobalScene
            };
            expectedGenerated.AddRange(
                plan.CellIds.Select(
                    WorldBaseline06B2Paths.CellScene));

            var updated = new List<EditorBuildSettingsScene>();
            var existingPaths = new HashSet<string>(
                StringComparer.Ordinal);
            foreach (EditorBuildSettingsScene existing in
                     EditorBuildSettings.scenes)
            {
                if (existing.path.StartsWith(
                        WorldBaseline06B2Paths.StreamingSceneRoot + "/",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        existing.path,
                        WorldBaseline06B2Paths.GlobalScene,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (!existingPaths.Add(existing.path))
                {
                    throw new InvalidDataException(
                        "Build Settings contains duplicate scene path: " +
                        existing.path);
                }

                bool enabled = existing.enabled ||
                    string.Equals(
                        existing.path,
                        WorldBaseline06B2Paths.ActiveBootstrapScene,
                        StringComparison.Ordinal);
                updated.Add(
                    new EditorBuildSettingsScene(
                        existing.path,
                        enabled));
            }

            if (updated.Count == 0 ||
                !string.Equals(
                    updated[0].path,
                    WorldBaseline06B2Paths.ActiveBootstrapScene,
                    StringComparison.Ordinal))
            {
                updated.RemoveAll(scene => string.Equals(
                    scene.path,
                    WorldBaseline06B2Paths.ActiveBootstrapScene,
                    StringComparison.Ordinal));
                updated.Insert(
                    0,
                    new EditorBuildSettingsScene(
                        WorldBaseline06B2Paths.ActiveBootstrapScene,
                        true));
            }

            foreach (string scenePath in expectedGenerated)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                        scenePath) == null)
                {
                    throw new FileNotFoundException(
                        "Generated 06B2 scene is missing before Build " +
                        "Settings update.",
                        WorldBaselinePaths.ToAbsoluteProjectPath(
                            scenePath));
                }

                updated.Add(
                    new EditorBuildSettingsScene(scenePath, true));
            }

            EditorBuildSettings.scenes = updated.ToArray();
        }

        private static ProductionWorldStreamingManifest
            CreateOrUpdateActiveManifest(
                DonorWorldCellizationPlan plan,
                WorldGameplayCellCatalog gameplayCatalog)
        {
            EnsureAssetFolder(
                WorldBaseline06B2Paths.ActiveManifest);
            ProductionWorldStreamingManifest manifest =
                AssetDatabase.LoadAssetAtPath<
                    ProductionWorldStreamingManifest>(
                    WorldBaseline06B2Paths.ActiveManifest);
            if (manifest == null)
            {
                manifest =
                    ScriptableObject.CreateInstance<
                        ProductionWorldStreamingManifest>();
                manifest.name =
                    "ProductionWorldStreamingManifest";
                AssetDatabase.CreateAsset(
                    manifest,
                    WorldBaseline06B2Paths.ActiveManifest);
            }

            int globalBuildIndex =
                ProductionWorldStreamingBuilder.GetEnabledBuildIndex(
                    WorldBaseline06B2Paths.GlobalScene);
            if (globalBuildIndex < 0)
            {
                throw new InvalidOperationException(
                    "06B2 global scene is not enabled in Build Settings.");
            }

            ProductionWorldCellScene[] cells = plan.CellIds
                .Select(cellId =>
                {
                    WorldCellIndex index =
                        DonorWorldCellizationPlan.ParseCellId(cellId);
                    string path =
                        WorldBaseline06B2Paths.CellScene(cellId);
                    int buildIndex =
                        ProductionWorldStreamingBuilder
                            .GetEnabledBuildIndex(path);
                    if (buildIndex < 0)
                    {
                        throw new InvalidOperationException(
                            "06B2 cell scene is not enabled in Build " +
                            "Settings: " + path);
                    }

                    return new ProductionWorldCellScene(
                        cellId,
                        index.X,
                        index.Z,
                        buildIndex,
                        path);
                })
                .ToArray();
            manifest.ConfigureForAuthoring(
                WorldBaseline06B2Paths.ProfileId,
                ProductionWorldProfileKind.DonorFeatureParity,
                true,
                WorldBaseline06B2Paths.CellSizeMeters,
                WorldBaseline06B2Paths.LoadingRadiusCells,
                WorldBaseline06B2Paths.UnloadingRadiusCells,
                WorldBaseline06B2Paths
                    .VehiclePreloadSpeedMetersPerSecond,
                WorldBaseline06B2Paths.VehiclePreloadRadiusCells,
                new[]
                {
                    new ProductionWorldGlobalScene(
                        "global-legacy",
                        globalBuildIndex,
                        WorldBaseline06B2Paths.GlobalScene)
                },
                cells,
                gameplayCatalog);
            EditorUtility.SetDirty(manifest);
            return manifest;
        }

        private static void WireActiveBootstrap(
            ProductionWorldStreamingManifest manifest)
        {
            Scene scene = EditorSceneManager.OpenScene(
                WorldBaseline06B2Paths.ActiveBootstrapScene,
                OpenSceneMode.Single);
            GameCompositionRoot root =
                RequireSingleSceneComponent<GameCompositionRoot>(scene);
            ProductionWorldStreamingService service =
                RequireSingleSceneComponent<
                    ProductionWorldStreamingService>(scene);
            ProductionWorldStreamingInstaller installer =
                RequireSingleSceneComponent<
                    ProductionWorldStreamingInstaller>(scene);
            GameObject playerPrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    ProductionWorldStreamingBuilder.PlayerPrefabPath);
            if (playerPrefab == null)
            {
                throw new FileNotFoundException(
                    "M4 player prefab is missing.",
                    WorldBaselinePaths.ToAbsoluteProjectPath(
                        ProductionWorldStreamingBuilder.PlayerPrefabPath));
            }
            if (root.gameObject != service.gameObject ||
                root.gameObject != installer.gameObject)
            {
                throw new InvalidOperationException(
                    "Active Bootstrap streaming composition must share one " +
                    "process-lifetime object.");
            }

            DonorWorldLegacyPresentationController[] controllers =
                root.GetComponents<
                    DonorWorldLegacyPresentationController>();
            if (controllers.Length > 1)
            {
                throw new InvalidOperationException(
                    "Active Bootstrap contains duplicate donor-world " +
                    "presentation controllers.");
            }
            DonorWorldLegacyPresentationController presentationController =
                controllers.Length == 1
                    ? controllers[0]
                    : root.gameObject.AddComponent<
                        DonorWorldLegacyPresentationController>();

            service.ConfigureForAuthoring(manifest);
            installer.ConfigureForAuthoring(
                root,
                service,
                playerPrefab,
                ProductionWorldStreamingBuilder.PlayerSpawnPosition,
                ProductionWorldStreamingBuilder.PlayerSpawnRotation);
            EditorUtility.SetDirty(service);
            EditorUtility.SetDirty(installer);
            EditorUtility.SetDirty(presentationController);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(
                    scene,
                    WorldBaseline06B2Paths.ActiveBootstrapScene))
            {
                throw new IOException(
                    "Could not save active 06B2 Bootstrap wiring.");
            }
        }

        private static void WriteOwnershipExports(
            DonorWorldCellizationPlan plan,
            DonorWorldMaterialTexturePlan presentation)
        {
            Dictionary<string, string> colliderByEntity =
                plan.SafeColliders
                    .GroupBy(
                        collider => collider.EntityStableId,
                        StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => string.Join(
                            ";",
                            group.OrderBy(
                                    collider => collider.ColliderStableId,
                                    StringComparer.Ordinal)
                                .Select(collider =>
                                    collider.ColliderStableId)),
                    StringComparer.Ordinal);
            IReadOnlyDictionary<long, int[]> staticBatchSubsets =
                WorldStaticBatchSubsetTable.ParseCommittedTable();
            var manifest = new StringBuilder();
            manifest.AppendLine(
                "LegacyWorldObjectId,ReplacementKey,SourceObjectId," +
                "SourceHierarchyPath,SemanticCategory,SourceCellId," +
                "AssignedOwner,OwnershipReason,HasRenderer," +
                "EffectiveActive,ColliderStableId,Classification," +
                "SourceRevisionId,SourceMaterialGuids," +
                "LegacyTexturedMaterialPaths," +
                "LegacyDiagnosticMaterialPath,PresentationStatus");
            foreach (DonorWorldCellizationAssignment assignment in
                     plan.Assignments)
            {
                WorldBaselineSanitationEntry entry =
                    assignment.SanitationEntry;
                colliderByEntity.TryGetValue(
                    entry.Placement.StableId,
                    out string colliderId);
                string[] effectiveMaterialSlots;
                if (entry.IncludeRenderer)
                {
                    Mesh mesh = LoadSanitizedRenderMesh(
                        entry,
                        staticBatchSubsets);
                    effectiveMaterialSlots =
                        DonorWorldMaterialTexturePlan
                            .ResolveSourceMaterialSlots(
                                entry,
                                mesh.subMeshCount);
                }
                else
                {
                    effectiveMaterialSlots = Array.Empty<string>();
                }
                AppendCsvRow(
                    manifest,
                    entry.Placement.StableId,
                    "legacy-world:" + entry.Placement.StableId,
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
                        : "MetadataOnly");
            }

            WriteProjectText(
                WorldBaseline06B2Paths.OwnershipManifest,
                manifest.ToString());

            var matrix = new StringBuilder();
            matrix.AppendLine(
                "OwnershipClass,Rule,EntityCount,RendererCount," +
                "ColliderCount,Reason,FutureAction," +
                "PresentationAssets,PresentationPolicy");
            foreach (IGrouping<string, DonorWorldCellizationAssignment> group
                     in plan.Assignments
                         .GroupBy(
                             assignment =>
                                 assignment.OwnershipReason,
                             StringComparer.Ordinal)
                         .OrderBy(
                             group => group.Key,
                             StringComparer.Ordinal))
            {
                HashSet<string> groupEntityIds = group
                    .Select(assignment =>
                        assignment.SanitationEntry.Placement.StableId)
                    .ToHashSet(StringComparer.Ordinal);
                AppendCsvRow(
                    matrix,
                    group.First().IsGlobal
                        ? "GlobalLegacy"
                        : "CellLegacy",
                    group.Key,
                    group.Count().ToString(
                        CultureInfo.InvariantCulture),
                    group.Count(assignment =>
                            assignment.SanitationEntry.IncludeRenderer)
                        .ToString(CultureInfo.InvariantCulture),
                    plan.SafeColliders.Count(collider =>
                            groupEntityIds.Contains(
                                collider.EntityStableId))
                        .ToString(CultureInfo.InvariantCulture),
                    OwnershipReasonDescription(group.Key),
                    group.First().IsGlobal
                        ? "Keep global until a seam-safe production " +
                          "replacement tool is validated."
                        : "Replace cell-by-cell through the matching " +
                          "ReplacementKey.",
                    presentation.Materials.Count.ToString(
                        CultureInfo.InvariantCulture) +
                    " resolved HDRP materials + 1 reviewed fallback; " +
                    presentation.Textures.Values.Count(texture =>
                        texture.IsImported).ToString(
                            CultureInfo.InvariantCulture) +
                    " texture role variants",
                    "Shared source-guid assets; LegacyTextured active; " +
                    "LegacyDiagnostic available; donor shader/runtime " +
                    "systems excluded.");
            }

            WriteProjectText(
                WorldBaseline06B2Paths.OwnershipMatrix,
                matrix.ToString());
            WriteSolidColliderDispositionExport(plan);
        }

        private static void WriteSolidColliderDispositionExport(
            DonorWorldCellizationPlan plan)
        {
            var manifest = new StringBuilder();
            manifest.AppendLine(
                "ColliderStableId,EntityStableId,SourceHierarchyPath," +
                "ObjectName,SemanticCategory,ColliderType,SourceEnabled," +
                "SourceIsTrigger,SourceConvex,RuntimeConvex," +
                "SourceHasRigidbodyInAncestry,MeshGuid,MeshFileId," +
                "EffectiveActive,Disposition,Included,OwnershipPolicy," +
                "CollisionLayer,PhysicsMaterial,Reason,Classification," +
                "SourceRevisionId,PolicyVersion");
            foreach (DonorWorldSafeColliderRecord record in
                     plan.ColliderDispositions)
            {
                AppendCsvRow(
                    manifest,
                    record.ColliderStableId,
                    record.EntityStableId,
                    record.HierarchyPath,
                    record.ObjectName,
                    record.SemanticCategory,
                    record.ColliderType,
                    record.SourceEnabled ? "1" : "0",
                    record.SourceIsTrigger ? "1" : "0",
                    record.Convex ? "1" : "0",
                    record.RuntimeConvex ? "1" : "0",
                    record.SourceHasRigidbodyInAncestry ? "1" : "0",
                    record.MeshGuid,
                    record.MeshFileId.ToString(
                        CultureInfo.InvariantCulture),
                    record.EffectiveActive ? "1" : "0",
                    record.Disposition,
                    record.IsIncluded ? "1" : "0",
                    record.OwnershipPolicy,
                    record.CollisionLayerName,
                    record.PhysicsMaterialAssetPath,
                    record.Reason,
                    "TemporaryDirectImport",
                    WorldBaselinePaths.SourceRevisionId,
                    DonorWorldSolidCollisionPolicy.PolicyVersion);
            }

            string normalized = manifest.ToString().Replace("\r\n", "\n");
            WriteProjectText(
                WorldBaseline06B2Paths.SolidColliderDispositionManifest,
                normalized);
            string hash = DonorWorldBaselineManifest.Sha256Text(normalized);
            WriteProjectText(
                WorldBaseline06B2Paths
                    .SolidColliderDispositionManifestSha256,
                hash + "  LEGACY_SOLID_COLLIDER_DISPOSITIONS.csv\n");
        }

        private static string OwnershipReasonDescription(string rule) =>
            rule switch
            {
                "SourceGlobalLargeOrContinuous" =>
                    "The frozen 06B1 partition already classified the " +
                    "object as global because it is large or continuous.",
                "ExplicitMapMeshAggregateGlobal" =>
                    "MAP/MESH contains static-batch aggregates whose " +
                    "source pivot/bounds do not represent their real " +
                    "cross-cell mesh extent.",
                "ExplicitCrossCellTraversalGlobal" =>
                    "Traversal geometry crosses or approaches a cell " +
                    "boundary and is not split in 06B2.",
                "SafetyCriticalCollisionGlobal" =>
                    "The immutable 06B2 safety collider must exist before " +
                    "the focus cell finishes loading or remain continuous " +
                    "while travelling.",
                "SourceCellDeterministic" =>
                    "Normal static content keeps the frozen source cell " +
                    "assignment; no geometry is moved or split.",
                _ => "Documented deterministic ownership rule."
            };

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
            foreach (DonorWorldCellizationAssignment assignment in assignments)
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

        private static void CopyAssetIfChanged(
            string source,
            string destination)
        {
            string sourceAbsolute =
                WorldBaselinePaths.ToAbsoluteProjectPath(source);
            string destinationAbsolute =
                WorldBaselinePaths.ToAbsoluteProjectPath(destination);
            if (File.Exists(destinationAbsolute) &&
                new FileInfo(sourceAbsolute).Length ==
                new FileInfo(destinationAbsolute).Length &&
                string.Equals(
                    DonorWorldBaselineManifest.ComputeFileSha256(
                        sourceAbsolute),
                    DonorWorldBaselineManifest.ComputeFileSha256(
                        destinationAbsolute),
                    StringComparison.Ordinal))
            {
                return;
            }

            EnsureAssetFolder(destination);
            if (AssetDatabase.LoadMainAssetAtPath(destination) != null &&
                !AssetDatabase.DeleteAsset(destination))
            {
                throw new IOException(
                    "Could not replace sanitized collider mesh: " +
                    destination);
            }
            if (!AssetDatabase.CopyAsset(source, destination))
            {
                throw new IOException(
                    $"Could not copy collider mesh '{source}' to " +
                    $"'{destination}'.");
            }
        }

        private static void PruneAssets(
            string root,
            ISet<string> expectedPaths,
            string filter)
        {
            if (!AssetDatabase.IsValidFolder(root))
            {
                return;
            }

            foreach (string guid in AssetDatabase.FindAssets(
                         filter,
                         new[] { root }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!expectedPaths.Contains(path) &&
                    path.StartsWith(root + "/", StringComparison.Ordinal) &&
                    !AssetDatabase.DeleteAsset(path))
                {
                    throw new IOException(
                        "Could not prune stale generated 06B2 asset: " +
                        path);
                }
            }
        }

        private static void ConfigureNoLightingSceneEnvironment()
        {
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.black;
            RenderSettings.reflectionIntensity = 0f;
        }

        private static T RequireSingleSceneComponent<T>(Scene scene)
            where T : Component
        {
            T[] components = scene.GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<T>(
                        includeInactive: true))
                .ToArray();
            if (components.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Scene {scene.path} must contain exactly one " +
                    $"{typeof(T).Name}; found {components.Length}.");
            }

            return components[0];
        }

        private static void EnsureFolders()
        {
            EnsureFolder(WorldBaseline06B2Paths.StreamingSceneRoot);
            EnsureFolder(
                WorldBaseline06B2Paths.StreamingCellSceneRoot);
            EnsureFolder(WorldBaseline06B2Paths.CollisionMeshRoot);
            EnsureAssetFolder(
                WorldBaseline06B2Paths.PrototypeFixtureScene);
            EnsureAssetFolder(
                WorldBaseline06B2Paths.GameplayCatalog);
        }

        private static void EnsureAssetFolder(string assetPath)
        {
            string directory = Path.GetDirectoryName(assetPath)
                ?.Replace('\\', '/');
            if (!string.IsNullOrWhiteSpace(directory))
            {
                EnsureFolder(directory);
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(
                        current,
                        parts[index]);
                }

                current = next;
            }
        }

        private static void WriteProjectText(
            string projectRelativePath,
            string content)
        {
            string absolute =
                WorldBaselinePaths.ToAbsoluteProjectPath(
                    projectRelativePath);
            Directory.CreateDirectory(
                Path.GetDirectoryName(absolute) ??
                throw new InvalidOperationException(
                    "Generated report has no directory."));
            string normalized = content.Replace("\r\n", "\n");
            if (!normalized.EndsWith(
                    "\n",
                    StringComparison.Ordinal))
            {
                normalized += "\n";
            }

            if (File.Exists(absolute) &&
                string.Equals(
                    File.ReadAllText(absolute),
                    normalized,
                    StringComparison.Ordinal))
            {
                return;
            }

            File.WriteAllText(
                absolute,
                normalized,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false));
        }

        private static void AppendCsvRow(
            StringBuilder builder,
            params string[] values)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                string value = values[index] ?? string.Empty;
                if (value.IndexOfAny(
                        new[] { ',', '"', '\r', '\n' }) >= 0)
                {
                    builder.Append('"')
                        .Append(value.Replace("\"", "\"\""))
                        .Append('"');
                }
                else
                {
                    builder.Append(value);
                }
            }

            builder.Append('\n');
        }
    }
}
