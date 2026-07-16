using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSC.Bootstrap;
using MSC.LegacyImport;
using MSC.Player;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.WorldBaseline
{
    [Category("LocalDonorBaseline")]
    public sealed class DonorWorldCellizationPlayModeTests
    {
        private const string RuntimeScenePrefix =
            "Assets/Game/LegacyImport/RuntimeBaseline/Generated/World/" +
            "Streaming/Scenes/";
        private const string GlobalScenePath =
            RuntimeScenePrefix + "World_Global_Legacy.unity";
        private const string HomeCellScenePath =
            RuntimeScenePrefix +
            "Cells/World_Cell_0_-3_Legacy.unity";

        [SetUp]
        public void RequireGeneratedRuntimeBaseline()
        {
#if UNITY_EDITOR
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    GlobalScenePath) == null ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    HomeCellScenePath) == null)
            {
                Assert.Ignore(
                    "Локальный generated RuntimeBaseline 06B2 отсутствует. " +
                    "Сгенерируйте donor streaming profile перед запуском " +
                    "этих тестов.");
            }
#else
            Assert.Ignore(
                "Generated donor RuntimeBaseline доступен только для " +
                "приватной локальной проверки в Unity Editor.");
#endif
        }

        [UnityTest]
        public IEnumerator Bootstrap_LoadsGlobalAndHomeCells_WithUniqueIdsAndRepresentativeMeshColliderRaycasts()
        {
            RuntimeSession session = null;
            yield return StartSession(value => session = value);

            Assert.That(
                session.Streaming.Manifest.ProfileKind,
                Is.EqualTo(
                    ProductionWorldProfileKind.DonorFeatureParity));
            Assert.That(
                session.Streaming.IsGlobalSceneLoaded("global-legacy"),
                Is.True);
            Assert.That(
                session.Streaming.IsCellLoaded("cell_0_-3"),
                Is.True);
            Assert.That(
                session.Streaming.IsCellLoaded("cell_0_-2"),
                Is.True);
            Assert.That(
                Object.FindObjectsByType<Transform>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Any(transform => transform.name.StartsWith(
                        "WR_Production_cell_",
                        StringComparison.Ordinal)),
                Is.False);

            DonorWorldBaselineEntityMetadata[] entities =
                Object.FindObjectsByType<
                    DonorWorldBaselineEntityMetadata>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            DonorWorldBaselineColliderMetadata[] colliderMetadata =
                Object.FindObjectsByType<
                    DonorWorldBaselineColliderMetadata>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            Assert.That(entities, Is.Not.Empty);
            Assert.That(
                entities.Select(entity => entity.StableId)
                    .Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(entities.Length));
            Assert.That(
                entities.Select(entity => entity.ReplacementKey)
                    .Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(entities.Length));
            Assert.That(
                colliderMetadata.Select(metadata =>
                        metadata.ColliderStableId)
                    .Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(colliderMetadata.Length));
            Assert.That(
                colliderMetadata.Select(metadata =>
                        metadata.EntityStableId)
                    .Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(colliderMetadata.Length));
            Assert.That(
                colliderMetadata.All(metadata =>
                    string.Equals(
                        metadata.ReplacementKey,
                        "legacy-world:" + metadata.EntityStableId,
                        StringComparison.Ordinal)),
                Is.True);

            yield return null;
            Assert.That(
                session.Registry.LoadedReplacementCount,
                Is.EqualTo(entities.Length));

            Physics.SyncTransforms();
            MeshCollider[] meshColliders = colliderMetadata
                .Select(metadata =>
                    metadata.GetComponent<MeshCollider>())
                .Where(collider => collider != null &&
                                   collider.enabled &&
                                   collider.gameObject.activeInHierarchy)
                .ToArray();
            var failures = new List<string>();
            int successfulRaycasts = 0;
            foreach (MeshCollider collider in meshColliders)
            {
                if (TryRaycastUpwardFacingTriangle(
                        collider,
                        out string failure))
                {
                    successfulRaycasts++;
                }
                else
                {
                    failures.Add(collider.name + ": " + failure);
                }
            }

            Assert.That(
                meshColliders.Length,
                Is.GreaterThanOrEqualTo(3));
            Assert.That(
                successfulRaycasts,
                Is.GreaterThanOrEqualTo(3),
                "Representative MeshCollider raycasts failed: " +
                string.Join(" | ", failures));
        }

        [UnityTest]
        public IEnumerator GlobalScene_PersistsAcrossRepeatedFocusMoves_AndReportedSpeedPreloadsRadiusTwo()
        {
            RuntimeSession session = null;
            yield return StartSession(value => session = value);
            int globalRootInstanceId =
                session.GlobalMetadata.gameObject.GetInstanceID();
            int registryInstanceId =
                session.Registry.gameObject.GetInstanceID();

            Assert.That(
                session.Streaming.EffectiveLoadingRadiusCells,
                Is.EqualTo(1));
            Assert.That(
                session.Streaming.IsCellLoaded("cell_1_-5"),
                Is.False);

            session.Streaming.ReportFocusSpeedMetersPerSecond(12f);
            Assert.That(
                session.Streaming.EffectiveLoadingRadiusCells,
                Is.EqualTo(2));
            yield return session.Streaming.RefreshNow();
            Assert.That(
                session.Streaming.IsCellLoaded("cell_1_-5"),
                Is.True,
                "ReportFocusSpeed did not preload the known radius-two cell.");
            AssertGlobalLifetime(
                session,
                globalRootInstanceId,
                registryInstanceId);

            session.Streaming.ReportFocusSpeedMetersPerSecond(0f);
            Vector3 farCellPosition =
                new Vector3(1568f, 10f, 32f);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                MoveFocus(session.Player, farCellPosition);
                yield return session.Streaming.RefreshNow();
                Assert.That(
                    session.Streaming.IsCellLoaded("cell_3_0"),
                    Is.True);
                AssertGlobalLifetime(
                    session,
                    globalRootInstanceId,
                    registryInstanceId);

                MoveFocus(
                    session.Player,
                    session.Installer.PlayerSpawnPosition);
                yield return session.Streaming.RefreshNow();
                Assert.That(
                    session.Streaming.IsCellLoaded("cell_0_-3"),
                    Is.True);
                AssertGlobalLifetime(
                    session,
                    globalRootInstanceId,
                    registryInstanceId);
            }
        }

        [UnityTest]
        public IEnumerator EveryRegisteredCell_LoadsAndUnloadsIndividuallyTwice_WhileGlobalSceneRemainsStable()
        {
            RuntimeSession session = null;
            yield return StartSession(value => session = value);
            ProductionWorldStreamingManifest manifest =
                session.Streaming.Manifest;
            ProductionWorldCellScene[] cells =
                manifest.Cells
                    .OrderBy(cell => cell.CellId, StringComparer.Ordinal)
                    .ToArray();
            int globalRootInstanceId =
                session.GlobalMetadata.gameObject.GetInstanceID();
            int registryInstanceId =
                session.Registry.gameObject.GetInstanceID();

            Assert.That(cells.Length, Is.EqualTo(49));
            Assert.That(manifest.GlobalScenes.Count, Is.EqualTo(1));
            Assert.That(
                session.GlobalMetadata.SceneId,
                Is.EqualTo("global-legacy"));
            Assert.That(
                session.GlobalMetadata.Ownership,
                Is.EqualTo(
                    DonorWorldStreamingOwnership.GlobalLegacy));
            Assert.That(session.GlobalMetadata.OwnerCellId, Is.Empty);
            Assert.That(
                session.GlobalMetadata.Classification,
                Is.EqualTo(
                    DonorWorldBaselineClassification
                        .TemporaryDirectImport));
            Assert.That(
                session.GlobalMetadata.ActivationState,
                Is.EqualTo(
                    DonorWorldBaselineActivationState
                        .ActiveFeatureParityProfile));

            yield return UnloadAllRegisteredCellScenes(manifest);
            Assert.That(
                CountLoadedRegisteredCellScenes(manifest),
                Is.Zero);
            AssertGlobalLifetime(
                session,
                globalRootInstanceId,
                registryInstanceId);
            AssertNoActiveRejectedOrProductionOverrideRoots(
                session,
                "initial cell isolation");

            for (int cycle = 0; cycle < 2; cycle++)
            {
                foreach (ProductionWorldCellScene cell in cells)
                {
                    AsyncOperation load =
                        SceneManager.LoadSceneAsync(
                            cell.BuildIndex,
                            LoadSceneMode.Additive);
                    Assert.That(
                        load,
                        Is.Not.Null,
                        $"Could not start cycle {cycle + 1} load for " +
                        $"{cell.CellId}.");
                    yield return load;
                    yield return null;

                    Scene loadedScene =
                        FindLoadedScene(cell.ScenePath);
                    GameObject[] roots =
                        loadedScene.GetRootGameObjects();
                    Assert.That(
                        roots.Length,
                        Is.EqualTo(1),
                        $"{cell.CellId} must have one generated scene root.");
                    DonorWorldStreamingSceneMetadata[] metadata =
                        GetSceneComponents<
                            DonorWorldStreamingSceneMetadata>(
                            loadedScene);
                    Assert.That(
                        metadata.Length,
                        Is.EqualTo(1),
                        $"{cell.CellId} must have one scene metadata stamp.");
                    Assert.That(metadata[0].gameObject, Is.SameAs(roots[0]));
                    Assert.That(
                        metadata[0].SceneId,
                        Is.EqualTo(cell.CellId + "-legacy"));
                    Assert.That(
                        metadata[0].Ownership,
                        Is.EqualTo(
                            DonorWorldStreamingOwnership.CellLegacy));
                    Assert.That(
                        metadata[0].OwnerCellId,
                        Is.EqualTo(cell.CellId));
                    Assert.That(
                        metadata[0].Classification,
                        Is.EqualTo(
                            DonorWorldBaselineClassification
                                .TemporaryDirectImport));
                    Assert.That(
                        metadata[0].ActivationState,
                        Is.EqualTo(
                            DonorWorldBaselineActivationState
                                .ActiveFeatureParityProfile));
                    Assert.That(
                        metadata[0].SourceRevisionId,
                        Is.Not.Empty);
                    Assert.That(
                        metadata[0].SourceSceneSha256,
                        Is.Not.Empty);
                    Assert.That(
                        metadata[0].OwnershipFingerprintSha256,
                        Is.Not.Empty);
                    Assert.That(
                        CountLoadedRegisteredCellScenes(manifest),
                        Is.EqualTo(1),
                        $"Cycle {cycle + 1} loaded more than the isolated " +
                        $"cell {cell.CellId}.");
                    AssertGlobalLifetime(
                        session,
                        globalRootInstanceId,
                        registryInstanceId);
                    AssertNoActiveRejectedOrProductionOverrideRoots(
                        session,
                        $"cycle {cycle + 1}, {cell.CellId}");

                    AsyncOperation unload =
                        SceneManager.UnloadSceneAsync(loadedScene);
                    Assert.That(
                        unload,
                        Is.Not.Null,
                        $"Could not start cycle {cycle + 1} unload for " +
                        $"{cell.CellId}.");
                    yield return unload;
                    yield return null;

                    Assert.That(
                        IsSceneLoaded(cell.ScenePath),
                        Is.False,
                        $"Cycle {cycle + 1} did not unload {cell.CellId}.");
                    Assert.That(
                        CountLoadedRegisteredCellScenes(manifest),
                        Is.Zero,
                        $"Cycle {cycle + 1} left another registered cell " +
                        $"loaded after unloading {cell.CellId}.");
                    AssertGlobalLifetime(
                        session,
                        globalRootInstanceId,
                        registryInstanceId);
                    AssertNoActiveRejectedOrProductionOverrideRoots(
                        session,
                        $"cycle {cycle + 1}, after {cell.CellId} unload");
                }
            }
        }

        [UnityTest]
        public IEnumerator ReplacementRegistry_PreservesOverrideAndGameplayCatalogAcrossCellUnloadReload()
        {
            RuntimeSession session = null;
            yield return StartSession(value => session = value);
            Scene homeScene = FindLoadedScene(HomeCellScenePath);
            DonorWorldBaselineEntityMetadata target =
                GetSceneComponents<DonorWorldBaselineEntityMetadata>(
                        homeScene)
                    .FirstOrDefault(entity =>
                        entity.HasSanitizedRenderer &&
                        entity.gameObject.activeInHierarchy &&
                        entity.GetComponentsInChildren<Renderer>(
                                includeInactive: true)
                            .Any(renderer => renderer.enabled));

            Assert.That(
                target,
                Is.Not.Null,
                "Home cell has no active renderer suitable for a " +
                "replacement-registry round trip.");
            string stableId = target.StableId;
            string replacementKey = target.ReplacementKey;
            Renderer[] initialRenderers =
                target.GetComponentsInChildren<Renderer>(
                    includeInactive: true);
            WorldGameplayCellCatalog catalog =
                session.Streaming.Manifest.GameplayCatalog;
            WorldGameplayAnchorRecord[] anchorSnapshot =
                catalog.Anchors.ToArray();

            Assert.That(
                session.Registry.SetProductionOverrideActive(
                    replacementKey,
                    productionOverrideActive: true),
                Is.True);
            Assert.That(
                session.Registry.IsProductionOverrideActive(
                    replacementKey),
                Is.True);
            Assert.That(
                initialRenderers.All(renderer => !renderer.enabled),
                Is.True);
            Assert.That(
                target.GetComponentsInChildren<Collider>(
                        includeInactive: true)
                    .All(collider => !collider.enabled),
                Is.True);

            MoveFocus(
                session.Player,
                new Vector3(153.495f, 10f, -700f));
            yield return session.Streaming.RefreshNow();
            Assert.That(
                session.Registry.SetProductionOverrideActive(
                    replacementKey,
                    productionOverrideActive: false),
                Is.True);
            Assert.That(
                initialRenderers.Any(renderer => renderer.enabled),
                Is.True,
                "A neighboring scene load must not replace the canonical " +
                "renderer state with the temporary override state.");
            Assert.That(
                session.Registry.SetProductionOverrideActive(
                    replacementKey,
                    productionOverrideActive: true),
                Is.True);

            MoveFocus(
                session.Player,
                new Vector3(1568f, 10f, 32f));
            yield return session.Streaming.RefreshNow();
            Assert.That(
                session.Streaming.IsCellLoaded("cell_0_-3"),
                Is.False);

            MoveFocus(
                session.Player,
                session.Installer.PlayerSpawnPosition);
            yield return session.Streaming.RefreshNow();
            Assert.That(
                session.Streaming.IsCellLoaded("cell_0_-3"),
                Is.True);

            DonorWorldBaselineEntityMetadata reloaded =
                GetSceneComponents<DonorWorldBaselineEntityMetadata>(
                        FindLoadedScene(HomeCellScenePath))
                    .Single(entity => string.Equals(
                        entity.StableId,
                        stableId,
                        StringComparison.Ordinal));
            Assert.That(
                session.Registry.IsProductionOverrideActive(
                    replacementKey),
                Is.True);
            Assert.That(
                reloaded.GetComponentsInChildren<Renderer>(
                        includeInactive: true)
                    .All(renderer => !renderer.enabled),
                Is.True);
            Assert.That(
                reloaded.GetComponentsInChildren<Collider>(
                        includeInactive: true)
                    .All(collider => !collider.enabled),
                Is.True);

            Assert.That(
                session.Streaming.Manifest.GameplayCatalog,
                Is.SameAs(catalog));
            AssertGameplayCatalogUnchanged(
                catalog,
                anchorSnapshot);

            Assert.That(
                session.Registry.SetProductionOverrideActive(
                    replacementKey,
                    productionOverrideActive: false),
                Is.True);
            Assert.That(
                reloaded.GetComponentsInChildren<Renderer>(
                        includeInactive: true)
                    .Any(renderer => renderer.enabled),
                Is.True);
        }

        [UnityTest]
        public IEnumerator ExternalUnloadReload_DoesNotTransferSceneOwnershipToStreamingService()
        {
            RuntimeSession session = null;
            yield return StartSession(value => session = value);
            Assert.That(
                session.Streaming.Manifest.TryGetCell(
                    "cell_0_-3",
                    out ProductionWorldCellScene homeCell),
                Is.True);
            Assert.That(
                session.Streaming.OwnsLoadedScene(
                    homeCell.ScenePath),
                Is.True);
            int ownedBefore =
                session.Streaming.OwnedLoadedSceneCount;

            Scene initiallyOwned =
                FindLoadedScene(homeCell.ScenePath);
            AsyncOperation externalUnload =
                SceneManager.UnloadSceneAsync(initiallyOwned);
            Assert.That(externalUnload, Is.Not.Null);
            yield return externalUnload;
            Assert.That(
                session.Streaming.OwnsLoadedScene(
                    homeCell.ScenePath),
                Is.False);
            Assert.That(
                session.Streaming.OwnedLoadedSceneCount,
                Is.EqualTo(ownedBefore - 1));

            AsyncOperation externalReload =
                SceneManager.LoadSceneAsync(
                    homeCell.BuildIndex,
                    LoadSceneMode.Additive);
            Assert.That(externalReload, Is.Not.Null);
            yield return externalReload;
            yield return null;
            Assert.That(
                FindLoadedScene(homeCell.ScenePath).isLoaded,
                Is.True);
            Assert.That(
                session.Streaming.OwnsLoadedScene(
                    homeCell.ScenePath),
                Is.False);

            MoveFocus(
                session.Player,
                new Vector3(1568f, 10f, 32f));
            yield return session.Streaming.RefreshNow();
            Assert.That(
                FindLoadedScene(homeCell.ScenePath).isLoaded,
                Is.True,
                "Streaming service unloaded an externally reloaded scene.");
            Assert.That(
                session.Streaming.OwnsLoadedScene(
                    homeCell.ScenePath),
                Is.False);
        }

        [UnityTest]
        public IEnumerator PresentationModes_UseSharedAssetsAndReloadWithoutInstanceGrowth()
        {
            RuntimeSession session = null;
            yield return StartSession(value => session = value);
            DonorWorldLegacyPresentationController controller =
                Object.FindObjectsByType<
                        DonorWorldLegacyPresentationController>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Single();
            DonorWorldLegacyMaterialBinding[] bindings =
                Object.FindObjectsByType<
                    DonorWorldLegacyMaterialBinding>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);

            Assert.That(bindings, Is.Not.Empty);
            Assert.That(
                bindings.All(binding => binding.IsConfigured),
                Is.True);
            Assert.That(
                controller.Mode,
                Is.EqualTo(
                    DonorWorldLegacyPresentationMode.LegacyTextured));
            AssertBindingsUseMode(
                bindings,
                DonorWorldLegacyPresentationMode.LegacyTextured);

            controller.SetMode(
                DonorWorldLegacyPresentationMode.LegacyDiagnostic);
            Assert.That(
                controller.LastRejectedBindingCount,
                Is.Zero);
            AssertBindingsUseMode(
                bindings,
                DonorWorldLegacyPresentationMode.LegacyDiagnostic);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                controller.SetMode(
                    (DonorWorldLegacyPresentationMode)999));
            Assert.That(
                controller.Mode,
                Is.EqualTo(
                    DonorWorldLegacyPresentationMode.LegacyDiagnostic));

            HashSet<int> materialIdsBefore =
                CaptureGeneratedAssetIds<Material>();
            HashSet<int> textureIdsBefore =
                CaptureGeneratedAssetIds<Texture>();
            Assert.That(materialIdsBefore, Is.Not.Empty);
            Assert.That(textureIdsBefore, Is.Not.Empty);

            Assert.That(
                session.Streaming.Manifest.TryGetCell(
                    "cell_0_-3",
                    out ProductionWorldCellScene homeCell),
                Is.True);
            Scene homeScene =
                FindLoadedScene(homeCell.ScenePath);
            AsyncOperation unload =
                SceneManager.UnloadSceneAsync(homeScene);
            Assert.That(unload, Is.Not.Null);
            yield return unload;
            AsyncOperation reload =
                SceneManager.LoadSceneAsync(
                    homeCell.BuildIndex,
                    LoadSceneMode.Additive);
            Assert.That(reload, Is.Not.Null);
            yield return reload;
            yield return null;

            DonorWorldLegacyMaterialBinding[] reloadedBindings =
                GetSceneComponents<DonorWorldLegacyMaterialBinding>(
                    FindLoadedScene(homeCell.ScenePath));
            Assert.That(reloadedBindings, Is.Not.Empty);
            Assert.That(
                controller.Mode,
                Is.EqualTo(
                    DonorWorldLegacyPresentationMode.LegacyDiagnostic));
            Assert.That(
                controller.LastRejectedBindingCount,
                Is.Zero);
            AssertBindingsUseMode(
                reloadedBindings,
                DonorWorldLegacyPresentationMode.LegacyDiagnostic);
            Assert.That(
                CaptureGeneratedAssetIds<Material>(),
                Is.EqualTo(materialIdsBefore));
            Assert.That(
                CaptureGeneratedAssetIds<Texture>(),
                Is.EqualTo(textureIdsBefore));
            Assert.That(
                Resources.FindObjectsOfTypeAll<Material>()
                    .Where(material =>
                        material.name.StartsWith(
                            "M06B2_",
                            StringComparison.Ordinal))
                    .Any(material =>
                        material.name.EndsWith(
                            " (Instance)",
                            StringComparison.Ordinal)),
                Is.False);
        }

        [UnityTest]
        public IEnumerator OutOfBoundsRecovery_ReturnsSpawnedPlayerToConfiguredSafeTransform()
        {
            RuntimeSession session = null;
            yield return StartSession(value => session = value);
            WorldOutOfBoundsRecovery recovery =
                session.Installer.OutOfBoundsRecovery;

            Assert.That(recovery, Is.Not.Null);
            Assert.That(recovery.IsConfigured, Is.True);
            int initialRecoveryCount = recovery.RecoveryCount;
            CharacterController controller =
                session.Player.GetComponent<CharacterController>();
            bool controllerWasEnabled =
                controller != null && controller.enabled;
            if (controllerWasEnabled)
            {
                controller.enabled = false;
            }

            session.Player.transform.position =
                new Vector3(
                    recovery.RecoveryPosition.x,
                    recovery.MinimumAllowedY - 10f,
                    recovery.RecoveryPosition.z);
            if (controllerWasEnabled)
            {
                controller.enabled = true;
            }
            Physics.SyncTransforms();

            yield return null;

            Assert.That(
                recovery.RecoveryCount,
                Is.EqualTo(initialRecoveryCount + 1));
            Assert.That(
                Vector3.Distance(
                    session.Player.transform.position,
                    recovery.RecoveryPosition),
                Is.LessThan(0.05f));
            Assert.That(
                Quaternion.Angle(
                    session.Player.transform.rotation,
                    recovery.RecoveryRotation),
                Is.LessThan(0.1f));
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            ProductionWorldStreamingService[] services =
                Object.FindObjectsByType<
                    ProductionWorldStreamingService>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (ProductionWorldStreamingService service in services)
            {
                service.enabled = false;
                double timeout =
                    Time.realtimeSinceStartupAsDouble + 30d;
                while (service.IsStreaming &&
                       Time.realtimeSinceStartupAsDouble < timeout)
                {
                    yield return null;
                }
                if (!service.IsStreaming)
                {
                    yield return service.UnloadOwnedScenes();
                }
            }

            GameCompositionRoot[] roots =
                Object.FindObjectsByType<GameCompositionRoot>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            foreach (GameCompositionRoot root in roots)
            {
                Object.Destroy(root.gameObject);
            }
            yield return null;

            var remainingScenes = new List<Scene>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded &&
                    scene.path.StartsWith(
                        RuntimeScenePrefix,
                        StringComparison.Ordinal))
                {
                    remainingScenes.Add(scene);
                }
            }
            foreach (Scene scene in remainingScenes)
            {
                AsyncOperation unload =
                    SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    yield return unload;
                }
            }
        }

        private static IEnumerator StartSession(
            Action<RuntimeSession> completed)
        {
            AsyncOperation load = SceneManager.LoadSceneAsync(
                "Bootstrap",
                LoadSceneMode.Single);
            Assert.That(
                load,
                Is.Not.Null,
                "Bootstrap is missing from Build Settings.");
            yield return load;
            yield return null;

            ProductionWorldStreamingInstaller installer =
                Object.FindFirstObjectByType<
                    ProductionWorldStreamingInstaller>();
            ProductionWorldStreamingService streaming =
                Object.FindFirstObjectByType<
                    ProductionWorldStreamingService>();
            Assert.That(installer, Is.Not.Null);
            Assert.That(streaming, Is.Not.Null);

            double timeout =
                Time.realtimeSinceStartupAsDouble + 120d;
            while ((!installer.IsReady || streaming.IsStreaming) &&
                   Time.realtimeSinceStartupAsDouble < timeout)
            {
                yield return null;
            }
            Assert.That(
                Time.realtimeSinceStartupAsDouble,
                Is.LessThan(timeout),
                "Bootstrap donor streaming timed out.");
            Assert.That(installer.IsReady, Is.True);
            Assert.That(installer.SpawnedPlayer, Is.Not.Null);

            DisablePlayer(installer.SpawnedPlayer);
            streaming.enabled = false;
            yield return null;

            DonorWorldStreamingSceneMetadata globalMetadata =
                Object.FindObjectsByType<
                        DonorWorldStreamingSceneMetadata>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Single(metadata => string.Equals(
                        metadata.SceneId,
                        "global-legacy",
                        StringComparison.Ordinal));
            DonorWorldLegacyReplacementRegistry registry =
                Object.FindObjectsByType<
                        DonorWorldLegacyReplacementRegistry>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Single();
            completed(new RuntimeSession(
                installer,
                streaming,
                installer.SpawnedPlayer,
                globalMetadata,
                registry));
        }

        private static void DisablePlayer(GameObject player)
        {
            FirstPersonMotor motor =
                player.GetComponent<FirstPersonMotor>();
            PlayerInputRouter input =
                player.GetComponent<PlayerInputRouter>();
            if (motor != null)
            {
                motor.enabled = false;
            }
            if (input != null)
            {
                input.enabled = false;
            }
        }

        private static void MoveFocus(
            GameObject player,
            Vector3 position)
        {
            CharacterController controller =
                player.GetComponent<CharacterController>();
            bool wasEnabled =
                controller != null && controller.enabled;
            if (wasEnabled)
            {
                controller.enabled = false;
            }
            player.transform.position = position;
            if (wasEnabled)
            {
                controller.enabled = true;
            }
            Physics.SyncTransforms();
        }

        private static void AssertGlobalLifetime(
            RuntimeSession session,
            int expectedGlobalRootInstanceId,
            int expectedRegistryInstanceId)
        {
            Assert.That(
                session.Streaming.IsGlobalSceneLoaded("global-legacy"),
                Is.True);
            DonorWorldStreamingSceneMetadata globalMetadata =
                Object.FindObjectsByType<
                        DonorWorldStreamingSceneMetadata>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Single(metadata => string.Equals(
                        metadata.SceneId,
                        "global-legacy",
                        StringComparison.Ordinal));
            DonorWorldLegacyReplacementRegistry registry =
                Object.FindObjectsByType<
                        DonorWorldLegacyReplacementRegistry>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None)
                    .Single();
            Assert.That(
                globalMetadata.gameObject.GetInstanceID(),
                Is.EqualTo(expectedGlobalRootInstanceId));
            Assert.That(
                registry.gameObject.GetInstanceID(),
                Is.EqualTo(expectedRegistryInstanceId));
        }

        private static IEnumerator UnloadAllRegisteredCellScenes(
            ProductionWorldStreamingManifest manifest)
        {
            ProductionWorldCellScene[] cells =
                manifest.Cells.ToArray();
            foreach (ProductionWorldCellScene cell in cells)
            {
                if (!TryFindLoadedScene(
                        cell.ScenePath,
                        out Scene loadedScene))
                {
                    continue;
                }

                AsyncOperation unload =
                    SceneManager.UnloadSceneAsync(loadedScene);
                Assert.That(
                    unload,
                    Is.Not.Null,
                    "Could not isolate registered cell scene: " +
                    cell.ScenePath);
                yield return unload;
            }

            yield return null;
        }

        private static int CountLoadedRegisteredCellScenes(
            ProductionWorldStreamingManifest manifest)
        {
            int loadedCount = 0;
            IReadOnlyList<ProductionWorldCellScene> cells =
                manifest.Cells;
            for (int index = 0; index < cells.Count; index++)
            {
                if (IsSceneLoaded(cells[index].ScenePath))
                {
                    loadedCount++;
                }
            }

            return loadedCount;
        }

        private static void AssertNoActiveRejectedOrProductionOverrideRoots(
            RuntimeSession session,
            string context)
        {
            Assert.That(
                session.Registry.ActiveProductionOverrideCount,
                Is.Zero,
                context + " has an unapproved active replacement override.");

            var invalidRoots = new List<string>();
            for (int sceneIndex = 0;
                 sceneIndex < SceneManager.sceneCount;
                 sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded)
                {
                    continue;
                }

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (!root.activeInHierarchy)
                    {
                        continue;
                    }

                    string rootName = root.name;
                    bool rejectedPrototype =
                        rootName.StartsWith(
                            "WR_Production_cell_",
                            StringComparison.Ordinal) ||
                        rootName.IndexOf(
                            "RejectedForFidelity",
                            StringComparison.OrdinalIgnoreCase) >= 0 ||
                        rootName.IndexOf(
                            "PrototypeOnly",
                            StringComparison.OrdinalIgnoreCase) >= 0;
                    bool productionOverride =
                        rootName.IndexOf(
                            "ProductionOverride",
                            StringComparison.OrdinalIgnoreCase) >= 0;
                    if (rejectedPrototype || productionOverride)
                    {
                        invalidRoots.Add(
                            scene.path + ":" + rootName);
                    }
                }
            }

            Assert.That(
                invalidRoots,
                Is.Empty,
                context + " loaded forbidden active roots: " +
                string.Join(" | ", invalidRoots));
        }

        private static void AssertBindingsUseMode(
            IEnumerable<DonorWorldLegacyMaterialBinding> bindings,
            DonorWorldLegacyPresentationMode mode)
        {
            foreach (DonorWorldLegacyMaterialBinding binding in bindings)
            {
                IReadOnlyList<Material> expected =
                    mode ==
                    DonorWorldLegacyPresentationMode.LegacyTextured
                        ? binding.TexturedMaterials
                        : binding.DiagnosticMaterials;
                Assert.That(
                    binding.TargetRenderer.sharedMaterials,
                    Is.EqualTo(expected.ToArray()));
            }
        }

        private static HashSet<int> CaptureGeneratedAssetIds<T>()
            where T : Object =>
            Resources.FindObjectsOfTypeAll<T>()
                .Where(asset =>
                    asset.name.StartsWith(
                        "M06B2_",
                        StringComparison.Ordinal))
                .Select(asset => asset.GetInstanceID())
                .ToHashSet();

        private static bool TryRaycastUpwardFacingTriangle(
            MeshCollider collider,
            out string failure)
        {
            Mesh mesh = collider.sharedMesh;
            if (mesh == null)
            {
                failure = "shared mesh is missing";
                return false;
            }

            Vector3[] vertices;
            int[] triangles;
            try
            {
                vertices = mesh.vertices;
                triangles = mesh.triangles;
            }
            catch (UnityException exception)
            {
                failure = "mesh data is not readable: " + exception.Message;
                return false;
            }

            Transform transform = collider.transform;
            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                Vector3 first =
                    transform.TransformPoint(vertices[triangles[index]]);
                Vector3 second =
                    transform.TransformPoint(vertices[triangles[index + 1]]);
                Vector3 third =
                    transform.TransformPoint(vertices[triangles[index + 2]]);
                Vector3 normal =
                    Vector3.Cross(second - first, third - first);
                if (normal.sqrMagnitude < 0.000001f)
                {
                    continue;
                }
                normal.Normalize();
                if (Vector3.Dot(normal, Vector3.up) < 0.5f)
                {
                    continue;
                }

                Vector3 centroid = (first + second + third) / 3f;
                var ray = new Ray(
                    centroid + Vector3.up * 0.75f,
                    Vector3.down);
                if (collider.Raycast(
                        ray,
                        out RaycastHit hit,
                        1.5f) &&
                    Vector3.Distance(hit.point, centroid) < 0.05f)
                {
                    failure = string.Empty;
                    return true;
                }
            }

            failure =
                "no upward-facing triangle produced a centroid raycast";
            return false;
        }

        private static Scene FindLoadedScene(string path)
        {
            if (TryFindLoadedScene(path, out Scene scene))
            {
                return scene;
            }

            Assert.Fail("Expected loaded scene was not found: " + path);
            return default;
        }

        private static bool IsSceneLoaded(string path) =>
            TryFindLoadedScene(path, out _);

        private static bool TryFindLoadedScene(
            string path,
            out Scene loadedScene)
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.isLoaded &&
                    string.Equals(
                        scene.path,
                        path,
                        StringComparison.Ordinal))
                {
                    loadedScene = scene;
                    return true;
                }
            }

            loadedScene = default;
            return false;
        }

        private static T[] GetSceneComponents<T>(Scene scene)
            where T : Component =>
            scene.GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<T>(
                        includeInactive: true))
                .ToArray();

        private static void AssertGameplayCatalogUnchanged(
            WorldGameplayCellCatalog catalog,
            IReadOnlyList<WorldGameplayAnchorRecord> expected)
        {
            Assert.That(catalog.Anchors.Count, Is.EqualTo(expected.Count));
            for (int index = 0; index < expected.Count; index++)
            {
                WorldGameplayAnchorRecord actual =
                    catalog.Anchors[index];
                Assert.That(
                    actual.AnchorId,
                    Is.EqualTo(expected[index].AnchorId));
                Assert.That(
                    actual.StableEntityId,
                    Is.EqualTo(expected[index].StableEntityId));
                Assert.That(
                    actual.Cell,
                    Is.EqualTo(expected[index].Cell));
                Assert.That(
                    actual.Position,
                    Is.EqualTo(expected[index].Position));
                Assert.That(
                    Quaternion.Angle(
                        actual.Rotation,
                        expected[index].Rotation),
                    Is.LessThan(0.001f));
            }
        }

        private sealed class RuntimeSession
        {
            public RuntimeSession(
                ProductionWorldStreamingInstaller installer,
                ProductionWorldStreamingService streaming,
                GameObject player,
                DonorWorldStreamingSceneMetadata globalMetadata,
                DonorWorldLegacyReplacementRegistry registry)
            {
                Installer = installer;
                Streaming = streaming;
                Player = player;
                GlobalMetadata = globalMetadata;
                Registry = registry;
            }

            public ProductionWorldStreamingInstaller Installer { get; }
            public ProductionWorldStreamingService Streaming { get; }
            public GameObject Player { get; }
            public DonorWorldStreamingSceneMetadata GlobalMetadata { get; }
            public DonorWorldLegacyReplacementRegistry Registry { get; }
        }
    }
}
