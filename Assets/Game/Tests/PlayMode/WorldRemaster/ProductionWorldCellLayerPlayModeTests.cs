using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using MSC.World.Partition;
using MSC.World.Streaming;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace MSC.Tests.PlayMode.WorldRemaster
{
    public sealed class ProductionWorldCellLayerPlayModeTests : IPrebuildSetup, IPostBuildCleanup
    {
        private const string BasePath =
            "Assets/Game/World/Debug/Streaming/CellLayerBaseFixture.unity";
        private const string LayerPath =
            "Assets/Game/World/Debug/Streaming/CellLayerVegetationFixture.unity";
        private const string AssetFixtureFolder = "Assets/Game/World/Debug/Streaming/GeneratedCellLayerAssetFixture";
        private const string SharedScenePath = AssetFixtureFolder + "/SharedScene.unity";
        private const string OwnedScenePath = AssetFixtureFolder + "/OwnedScene.unity";
        private const string FixtureCatalogName = "CellLayerAssetLifetime_Catalog";
        private const string FixtureCellName = "CellLayerAssetLifetime_Cell";
        private const string FixtureMaskName = "CellLayerAssetLifetime_Mask";
        private ProductionWorldStreamingManifest manifest;
        private ProductionWorldStreamingService service;
        private GameObject owner;
        private GameObject focus;

        public void Setup()
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(SessionState.GetString(BuildSettingsSnapshotKey, string.Empty)))
                throw new InvalidOperationException("The previous cell-layer fixture cleanup is still pending; restore it before starting another run.");
            if (AssetDatabase.IsValidFolder(AssetFixtureFolder) || System.IO.Directory.Exists(AssetFixtureFolder))
                throw new InvalidOperationException("An existing asset-lifetime fixture directory must not be overwritten: " + AssetFixtureFolder);
            EditorBuildSettingsScene[] original = EditorBuildSettings.scenes;
            var scenes = new List<EditorBuildSettingsScene>(original);
            AddFixture(scenes, BasePath);
            AddFixture(scenes, LayerPath);
            var snapshot = new BuildSettingsSnapshot { scenes = new BuildSceneRecord[original.Length] };
            for (int index = 0; index < original.Length; index++)
                snapshot.scenes[index] = new BuildSceneRecord { path = original[index].path, enabled = original[index].enabled };
            // PrebuildSetup runs before EnterPlayMode. A normal NUnit SetUp is
            // too late: Unity has already snapshotted its runtime scene table.
            SessionState.SetString(BuildSettingsSnapshotKey, JsonUtility.ToJson(snapshot));
            try
            {
                CreateAssetLifetimeFixtures();
                AddFixture(scenes, SharedScenePath);
                AddFixture(scenes, OwnedScenePath);
                EditorBuildSettings.scenes = scenes.ToArray();
            }
            catch
            {
                DeleteOwnedAssetLifetimeFixtures();
                SessionState.EraseString(BuildSettingsSnapshotKey);
                throw;
            }
#endif
        }

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            manifest = ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
            manifest.ConfigureForAuthoring(512f, 0, 1, new[]
            {
                new ProductionWorldCellScene("cell_0_0", 0, 0,
                    GetBuildIndex(BasePath), BasePath)
            });
            manifest.ConfigureCellLayersForAuthoring(new[]
            {
                new ProductionWorldCellLayerScene("vegetation", new WorldCellIndex(1, 0),
                    GetBuildIndex(LayerPath), LayerPath, 1, 2)
            });
            owner = new GameObject("CellLayerLifecycleService");
            service = owner.AddComponent<ProductionWorldStreamingService>();
            service.enabled = false;
            service.ConfigureForAuthoring(manifest);
            focus = new GameObject("CellLayerLifecycleFocus");
            focus.transform.position = new Vector3(16f, 0f, 16f);
            service.BindFocus(focus.transform);
#else
            Assert.Ignore("The isolated fixture manages its temporary Editor build entries.");
#endif
        }

        [UnityTest]
        public IEnumerator OptionalLayerLoadsAndUnloadsTwiceIncludingCollidersWithoutLegacyCell()
        {
            int loadedEvents = 0;
            int unloadEvents = 0;
            service.OwnedSceneLoaded += scene =>
            {
                if (scene.path == LayerPath) loadedEvents++;
            };
            service.OwnedSceneWillUnload += scene =>
            {
                if (scene.path == LayerPath) unloadEvents++;
            };
            for (int cycle = 0; cycle < 2; cycle++)
            {
                focus.transform.position = new Vector3(16f, 0f, 16f);
                yield return service.RefreshNow();
                Assert.That(service.IsCellLoaded("cell_0_0"), Is.True);
                Assert.That(service.IsCellLoaded("cell_1_0"), Is.False);
                Assert.That(service.IsCellLayerLoaded("cell_1_0", "vegetation"), Is.True);
                Assert.That(service.OwnedLoadedSceneCount, Is.EqualTo(2));
                Scene layer = SceneManager.GetSceneByPath(LayerPath);
                Collider collider = layer.GetRootGameObjects()[0].GetComponent<Collider>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(collider.enabled, Is.True);

                // Layer radius-two hysteresis survives while its base cell is gone.
                focus.transform.position = new Vector3(3 * 512f + 16f, 0f, 16f);
                yield return service.RefreshNow();
                Assert.That(service.IsCellLoaded("cell_0_0"), Is.False);
                Assert.That(service.IsCellLayerLoaded("cell_1_0", "vegetation"), Is.True);

                focus.transform.position = new Vector3(4 * 512f + 16f, 0f, 16f);
                yield return service.RefreshNow();
                Assert.That(service.IsCellLayerLoaded("cell_1_0", "vegetation"), Is.False);
                Assert.That(service.OwnedLoadedSceneCount, Is.Zero);
                Assert.That(collider == null, Is.True,
                    "Far cell must actually unload its physics objects, not only hide renderers.");
            }

            Assert.That(loadedEvents, Is.EqualTo(2));
            Assert.That(unloadEvents, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator UnsortedOptionalLayersLoadNearestFirstWithDistinctActivationFrames()
        {
#if UNITY_EDITOR
            manifest.ConfigureCellLayersForAuthoring(new[]
            {
                new ProductionWorldCellLayerScene("vegetation", new WorldCellIndex(3, 0),
                    GetBuildIndex(LayerPath), LayerPath, 3, 4),
                new ProductionWorldCellLayerScene("vegetation", new WorldCellIndex(0, 0),
                    GetBuildIndex(SharedScenePath), SharedScenePath, 3, 4),
                new ProductionWorldCellLayerScene("vegetation", new WorldCellIndex(1, 0),
                    GetBuildIndex(OwnedScenePath), OwnedScenePath, 3, 4)
            });
#endif
            var paths = new List<string>();
            var frames = new List<int>();
            service.OwnedSceneLoaded += scene =>
            {
                if (scene.path == BasePath) return;
                paths.Add(scene.path);
                frames.Add(Time.frameCount);
            };
            yield return service.RefreshNow();
            Assert.That(paths, Is.EqualTo(new[] { SharedScenePath, OwnedScenePath, LayerPath }));
            Assert.That(frames[1], Is.GreaterThan(frames[0]));
            Assert.That(frames[2], Is.GreaterThan(frames[1]));
            Assert.That(service.OwnedLoadedSceneCount, Is.EqualTo(4));
            Assert.That(service.CompletedSceneLoadCount, Is.EqualTo(4));
            Assert.That(service.LastSceneLoadPath, Is.EqualTo(LayerPath));
            Assert.That(service.LastSceneLoadCompletedFrame, Is.GreaterThanOrEqualTo(service.LastSceneLoadStartedFrame));
            Assert.That(service.LastSceneLoadElapsedMilliseconds, Is.GreaterThanOrEqualTo(0d));
            Assert.That(service.LastSceneNotificationCpuMilliseconds, Is.GreaterThanOrEqualTo(0d));
        }

        [UnityTest]
        public IEnumerator AutomaticBudgetPrioritizesVegetationBeforeBackdropAtEqualDistance()
        {
#if UNITY_EDITOR
            manifest.ConfigureCellLayersForAuthoring(new[]
            {
                // The backdrop path sorts before the near path. Layer priority,
                // rather than serialized/path order, must decide the first load.
                new ProductionWorldCellLayerScene("vegetation-backdrop",
                    new WorldCellIndex(0, 0), GetBuildIndex(LayerPath),
                    LayerPath, 0, 1),
                new ProductionWorldCellLayerScene("vegetation",
                    new WorldCellIndex(0, 0), GetBuildIndex(SharedScenePath),
                    SharedScenePath, 0, 1)
            });
#endif
            string firstLayerPath = null;
            service.OwnedSceneLoaded += scene =>
            {
                if (scene.path == BasePath || firstLayerPath != null)
                {
                    return;
                }

                firstLayerPath = scene.path;
                service.enabled = false;
            };

            service.enabled = true;
            for (int frame = 0; frame < 240 && firstLayerPath == null; frame++)
            {
                yield return null;
            }

            for (int frame = 0; frame < 240 && service.IsStreaming; frame++)
            {
                yield return null;
            }

            service.enabled = false;
            Assert.That(firstLayerPath, Is.EqualTo(SharedScenePath));
            Assert.That(service.IsCellLayerLoaded(
                "cell_0_0", "vegetation"), Is.True);
            Assert.That(service.IsCellLayerLoaded(
                "cell_0_0", "vegetation-backdrop"), Is.False);
            Assert.That(service.MaximumAutomaticRefreshLayerLoadCount,
                Is.LessThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator InitialManualRefreshDefersBackdropThenAutomaticRefreshLoadsIt()
        {
#if UNITY_EDITOR
            manifest.ConfigureCellLayersForAuthoring(new[]
            {
                new ProductionWorldCellLayerScene("vegetation",
                    new WorldCellIndex(0, 0), GetBuildIndex(SharedScenePath),
                    SharedScenePath, 0, 1),
                new ProductionWorldCellLayerScene("vegetation-backdrop",
                    new WorldCellIndex(0, 0), GetBuildIndex(LayerPath),
                    LayerPath, 0, 1, deferInitialLoad: true)
            });
#endif
            yield return service.RefreshNow();
            Assert.That(service.IsCellLoaded("cell_0_0"), Is.True);
            Assert.That(service.IsCellLayerLoaded(
                "cell_0_0", "vegetation"), Is.True);
            Assert.That(service.IsCellLayerLoaded(
                "cell_0_0", "vegetation-backdrop"), Is.False,
                "The startup refresh must not wait for an optional far ring.");
            Assert.That(service.LastRefreshStartedLayerLoadCount, Is.EqualTo(1));

            service.enabled = true;
            for (int frame = 0;
                 frame < 240 && !service.IsCellLayerLoaded(
                     "cell_0_0", "vegetation-backdrop");
                 frame++)
            {
                yield return null;
            }

            for (int frame = 0; frame < 240 && service.IsStreaming; frame++)
            {
                yield return null;
            }

            service.enabled = false;
            Assert.That(service.IsCellLayerLoaded(
                "cell_0_0", "vegetation-backdrop"), Is.True);
            Assert.That(service.CompletedAutomaticRefreshCount,
                Is.GreaterThanOrEqualTo(1));
            Assert.That(service.MaximumAutomaticRefreshLayerLoadCount,
                Is.LessThanOrEqualTo(1));
        }

        [UnityTest]
        public IEnumerator AutomaticRefreshRecomputesFocusAfterEveryLayerActivation()
        {
#if UNITY_EDITOR
            manifest.ConfigureCellLayersForAuthoring(new[]
            {
                new ProductionWorldCellLayerScene("vegetation", new WorldCellIndex(3, 0),
                    GetBuildIndex(LayerPath), LayerPath, 3, 4),
                new ProductionWorldCellLayerScene("vegetation", new WorldCellIndex(0, 0),
                    GetBuildIndex(SharedScenePath), SharedScenePath, 3, 4),
                new ProductionWorldCellLayerScene("vegetation", new WorldCellIndex(1, 0),
                    GetBuildIndex(OwnedScenePath), OwnedScenePath, 3, 4)
            });
#endif
            var paths = new List<string>();
            var perRefreshLoadCounts = new List<int>();
            service.OwnedSceneLoaded += scene =>
            {
                if (scene.path == BasePath) return;
                paths.Add(scene.path);
                perRefreshLoadCounts.Add(
                    service.LastRefreshStartedLayerLoadCount);
            };

            service.enabled = true;
            for (int frame = 0; frame < 240 && paths.Count < 3; frame++)
            {
                yield return null;
            }
            for (int frame = 0; frame < 240 && service.IsStreaming; frame++)
            {
                yield return null;
            }
            service.enabled = false;

            Assert.That(service.IsStreaming, Is.False,
                "Automatic refresh did not settle within the frame budget.");
            Assert.That(paths, Is.EqualTo(new[]
            {
                SharedScenePath,
                OwnedScenePath,
                LayerPath
            }));
            Assert.That(perRefreshLoadCounts, Is.EqualTo(new[] { 1, 1, 1 }));
            Assert.That(
                service.MaximumAutomaticRefreshLayerLoadCount,
                Is.LessThanOrEqualTo(
                    ProductionWorldStreamingService
                        .MaximumAutomaticLayerLoadsPerRefresh));
            Assert.That(service.CompletedAutomaticRefreshCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(
                service.LastSceneActivationPreparedFrame,
                Is.GreaterThanOrEqualTo(service.LastSceneLoadStartedFrame));
        }

        [UnityTest]
        public IEnumerator SessionTeardownBeforePresentationPreparationCompletesLoadThenUnloadsWithoutAdoption()
        {
            yield return SceneManager.LoadSceneAsync(BasePath,
                LoadSceneMode.Additive);
#if UNITY_EDITOR
            manifest.ConfigureCellLayersForAuthoring(new[]
            {
                new ProductionWorldCellLayerScene("vegetation-backdrop",
                    new WorldCellIndex(0, 0), GetBuildIndex(LayerPath),
                    LayerPath, 0, 1, deferInitialLoad: false)
            });
#endif
            int loadedEvents = 0;
            service.OwnedSceneLoaded += scene =>
            {
                if (scene.path == LayerPath) loadedEvents++;
            };

            var stack = new List<IEnumerator> { service.RefreshNow() };
            AdvanceCoroutineTreeToNextFrameYield(stack);
            AsyncOperation load = GetInFlightLoadOperation(service);
            Assert.That(load, Is.Not.Null);
            Assert.That(load.allowSceneActivation, Is.False);
            Assert.That(load.progress, Is.LessThan(0.9f),
                "The fixture must exercise teardown before background preparation reaches the activation gate.");

            service.EndGameSession();
            DisposeCoroutineTree(stack);
            yield return WaitForDetachedLoadAndUnload(load, LayerPath);

            Assert.That(SceneManager.GetSceneByPath(LayerPath).isLoaded,
                Is.False);
            Assert.That(service.OwnedLoadedSceneCount, Is.Zero);
            Assert.That(loadedEvents, Is.Zero,
                "A detached load must never publish ownership after teardown.");
        }

        [UnityTest]
        public IEnumerator SessionTeardownAfterActivationGateUnloadsCompletedLoadWithoutAdoption()
        {
            yield return SceneManager.LoadSceneAsync(BasePath,
                LoadSceneMode.Additive);
#if UNITY_EDITOR
            manifest.ConfigureCellLayersForAuthoring(new[]
            {
                new ProductionWorldCellLayerScene("vegetation-backdrop",
                    new WorldCellIndex(0, 0), GetBuildIndex(LayerPath),
                    LayerPath, 0, 1, deferInitialLoad: false)
            });
#endif
            int loadedEvents = 0;
            service.OwnedSceneLoaded += scene =>
            {
                if (scene.path == LayerPath) loadedEvents++;
            };

            var stack = new List<IEnumerator> { service.RefreshNow() };
            AdvanceCoroutineTreeToNextFrameYield(stack);
            AsyncOperation load = GetInFlightLoadOperation(service);
            Assert.That(load, Is.Not.Null);
            Assert.That(load.allowSceneActivation, Is.False);
            for (int frame = 0; frame < 600 && load.progress < 0.9f;
                 frame++)
            {
                yield return null;
            }
            Assert.That(load.progress, Is.GreaterThanOrEqualTo(0.9f),
                "The fixture never reached the presentation activation gate.");

            // Resume the parked iterator through its deliberate one-frame gate,
            // then once more to enable activation. Do not let it resume after
            // the AsyncOperation: teardown owns the completion from here.
            AdvanceCoroutineTreeToNextFrameYield(stack);
            Assert.That(load.allowSceneActivation, Is.False);
            AdvanceCoroutineTreeToNextFrameYield(stack);
            Assert.That(load.allowSceneActivation, Is.True);
            Assert.That(service.LastSceneActivationPreparedFrame,
                Is.GreaterThanOrEqualTo(service.LastSceneLoadStartedFrame));
            Assert.That(GetInFlightLoadOperation(service), Is.SameAs(load));

            service.EndGameSession();
            DisposeCoroutineTree(stack);
            yield return WaitForDetachedLoadAndUnload(load, LayerPath);

            Assert.That(SceneManager.GetSceneByPath(LayerPath).isLoaded,
                Is.False);
            Assert.That(service.OwnedLoadedSceneCount, Is.Zero);
            Assert.That(loadedEvents, Is.Zero);
        }

        [UnityTest]
        public IEnumerator AlreadyLoadedLayerIsNotAdoptedOrUnloadedByService()
        {
            yield return SceneManager.LoadSceneAsync(LayerPath, LoadSceneMode.Additive);
            yield return service.RefreshNow();
            Assert.That(service.IsCellLayerLoaded("cell_1_0", "vegetation"), Is.True);
            Assert.That(service.OwnsLoadedScene(LayerPath), Is.False);
            Assert.That(service.OwnedLoadedSceneCount, Is.EqualTo(1));
            focus.transform.position = new Vector3(8 * 512f, 0f, 0f);
            yield return service.RefreshNow();
            yield return service.UnloadOwnedScenes();
            Assert.That(SceneManager.GetSceneByPath(LayerPath).isLoaded, Is.True,
                "Author-opened or externally owned scenes must remain loaded.");
        }

        [UnityTest]
        public IEnumerator UnloadBatchPreservesSharedAssetsThenReleasesAndReloadsCellData()
        {
#if UNITY_EDITOR
            manifest.ConfigureCellLayersForAuthoring(new[]
            {
                new ProductionWorldCellLayerScene("vegetation", new WorldCellIndex(1, 0),
                    GetBuildIndex(OwnedScenePath), OwnedScenePath, 1, 2)
            });
#endif
            // The externally loaded scene shares the exact same catalog/cell/mask.
            // It must survive cleanup triggered by an owned vegetation scene.
            yield return SceneManager.LoadSceneAsync(SharedScenePath, LoadSceneMode.Additive);
            yield return service.RefreshNow();
            AssertFixtureDataPresent();
            focus.transform.position = new Vector3(8 * 512f, 0f, 0f);
            yield return service.RefreshNow();
            Assert.That(SceneManager.GetSceneByPath(OwnedScenePath).isLoaded, Is.False);
            Assert.That(SceneManager.GetSceneByPath(SharedScenePath).isLoaded, Is.True);
            AssertFixtureDataPresent();
            Assert.That(service.CompletedUnusedAssetCleanupCount, Is.Zero,
                "An ordinary distance transition must not start a reachability scan.");
            Assert.That(service.HasPendingUnusedAssetCleanup, Is.True);

            // Remove the external owner while the service still owns a reloaded
            // scene; then exercise the service's explicit session-unload path.
            focus.transform.position = new Vector3(16f, 0f, 16f);
            yield return service.RefreshNow();
            yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByPath(SharedScenePath));
            AssertFixtureDataPresent();
            yield return service.UnloadOwnedScenes();
            AssertFixtureDataReleased();
            Assert.That(service.CompletedUnusedAssetCleanupCount, Is.EqualTo(1));
            Assert.That(service.HasPendingUnusedAssetCleanup, Is.False);

            // Disk assets remain intact. A subsequent load restores the payload,
            // while ordinary distance-based unloading defers its expensive asset
            // scan until the next explicit teardown boundary.
            yield return service.RefreshNow();
            AssertFixtureDataPresent();
            focus.transform.position = new Vector3(8 * 512f, 0f, 0f);
            yield return service.RefreshNow();
            AssertFixtureDataResident();
            Assert.That(service.CompletedUnusedAssetCleanupCount, Is.EqualTo(1));
            Assert.That(service.HasPendingUnusedAssetCleanup, Is.True);
            yield return service.UnloadOwnedScenes();
            AssertFixtureDataReleased();
            Assert.That(service.CompletedUnusedAssetCleanupCount, Is.EqualTo(2));
            Assert.That(service.HasPendingUnusedAssetCleanup, Is.False);
        }

        private static void AssertFixtureDataPresent()
        {
            Scene scene = SceneManager.GetSceneByPath(SharedScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = SceneManager.GetSceneByPath(OwnedScenePath);
            Assert.That(scene.IsValid() && scene.isLoaded, Is.True, "No saved asset fixture scene is loaded.");
            VegetationWorldRenderer renderer = scene.GetRootGameObjects()[0].GetComponent<VegetationWorldRenderer>();
            Assert.That(renderer != null && renderer.Catalog != null, Is.True, "The saved fixture lost its catalog reference.");
            VegetationCellCatalog catalog = renderer.Catalog;
            Assert.That(IsFixtureAsset(catalog, "Catalog.asset"), Is.True, "The fixture must reference its exact persistent catalog.");
            Assert.That(catalog.Cells.Count, Is.EqualTo(1));
            VegetationCellAsset cell = catalog.Cells[0];
            Assert.That(cell != null, Is.True);
            Assert.That(IsFixtureAsset(cell, "Cell.asset"), Is.True);
            Assert.That(cell.Tiles[0].TotalInstanceCount, Is.EqualTo(64));
            Assert.That(cell.DensityMask != null, Is.True);
            Assert.That(IsFixtureAsset(cell.DensityMask, "Mask.asset"), Is.True);
            Assert.That(cell.DensityMask.GetPixel(0, 0).g, Is.EqualTo(0.5f).Within(0.01f));
        }

        private static void AssertFixtureDataReleased()
        {
            // Query native liveness without storing asset references in the test
            // coroutine across UnloadUnusedAssets (which would retain the data).
            Assert.That(Array.Exists(Resources.FindObjectsOfTypeAll<VegetationCellCatalog>(),
                item => IsFixtureAsset(item, "Catalog.asset")), Is.False, "Unused catalog remained resident.");
            Assert.That(Array.Exists(Resources.FindObjectsOfTypeAll<VegetationCellAsset>(),
                item => IsFixtureAsset(item, "Cell.asset")), Is.False, "Unused CPU instance payload remained resident.");
            Assert.That(Array.Exists(Resources.FindObjectsOfTypeAll<Texture2D>(),
                item => IsFixtureAsset(item, "Mask.asset")), Is.False, "Unused density texture remained resident.");
        }

        private static void AssertFixtureDataResident()
        {
            Assert.That(Array.Exists(
                Resources.FindObjectsOfTypeAll<VegetationCellCatalog>(),
                item => IsFixtureAsset(item, "Catalog.asset")), Is.True,
                "Deferred cleanup unexpectedly released the catalog during a cell transition.");
            Assert.That(Array.Exists(
                Resources.FindObjectsOfTypeAll<VegetationCellAsset>(),
                item => IsFixtureAsset(item, "Cell.asset")), Is.True,
                "Deferred cleanup unexpectedly released the CPU instance payload during a cell transition.");
            Assert.That(Array.Exists(
                Resources.FindObjectsOfTypeAll<Texture2D>(),
                item => IsFixtureAsset(item, "Mask.asset")), Is.True,
                "Deferred cleanup unexpectedly released the density texture during a cell transition.");
        }

        private static bool IsFixtureAsset(UnityEngine.Object asset, string fileName)
        {
#if UNITY_EDITOR
            // CreateAsset/import may set Object.name from the file's basename.
            // Query the stable persistent asset path without loading anything.
            return asset != null && string.Equals(AssetDatabase.GetAssetPath(asset),
                AssetFixtureFolder + "/" + fileName, StringComparison.Ordinal);
#else
            return false; // This fixture's SetUp explicitly skips player builds.
#endif
        }

        private static object AdvanceCoroutineTreeToNextFrameYield(
            List<IEnumerator> stack)
        {
            while (stack.Count != 0)
            {
                IEnumerator current = stack[stack.Count - 1];
                if (!current.MoveNext())
                {
                    (current as IDisposable)?.Dispose();
                    stack.RemoveAt(stack.Count - 1);
                    continue;
                }

                object yielded = current.Current;
                if (yielded is IEnumerator nested)
                {
                    stack.Add(nested);
                    continue;
                }

                return yielded;
            }

            throw new AssertionException(
                "Streaming refresh ended before it reached an asynchronous frame boundary.");
        }

        private static void DisposeCoroutineTree(List<IEnumerator> stack)
        {
            for (int index = stack.Count - 1; index >= 0; index--)
                (stack[index] as IDisposable)?.Dispose();
            stack.Clear();
        }

        private static AsyncOperation GetInFlightLoadOperation(
            ProductionWorldStreamingService target)
        {
            FieldInfo field = typeof(ProductionWorldStreamingService).GetField(
                "inFlightOwnedSceneLoad",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            object tracked = field.GetValue(target);
            if (tracked == null) return null;
            FieldInfo operation = tracked.GetType().GetField("Operation",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(operation, Is.Not.Null);
            return (AsyncOperation)operation.GetValue(tracked);
        }

        private static IEnumerator WaitForDetachedLoadAndUnload(
            AsyncOperation load, string scenePath)
        {
            for (int frame = 0; frame < 600 && !load.isDone; frame++)
                yield return null;
            Assert.That(load.isDone, Is.True,
                "Detached scene load did not complete after activation was forced.");
            for (int frame = 0; frame < 600; frame++)
            {
                Scene scene = SceneManager.GetSceneByPath(scenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    // Give a late completion callback one more frame; the scene
                    // must remain absent rather than merely not existing yet.
                    yield return null;
                    scene = SceneManager.GetSceneByPath(scenePath);
                    if (!scene.IsValid() || !scene.isLoaded) yield break;
                }
                yield return null;
            }

            Assert.Fail("Detached scene remained loaded after session teardown: " +
                        scenePath);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (service != null)
            {
                yield return service.UnloadOwnedScenes();
            }

            foreach (string path in new[] { SharedScenePath, OwnedScenePath, LayerPath, BasePath })
            {
                Scene scene = SceneManager.GetSceneByPath(path);
                if (scene.IsValid() && scene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(scene);
                }
            }

            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (focus != null) UnityEngine.Object.Destroy(focus);
            if (manifest != null) UnityEngine.Object.Destroy(manifest);
            yield return null;
        }

        public void Cleanup()
        {
#if UNITY_EDITOR
            string saved = SessionState.GetString(BuildSettingsSnapshotKey, string.Empty);
            if (string.IsNullOrEmpty(saved)) return;
            BuildSettingsSnapshot snapshot = JsonUtility.FromJson<BuildSettingsSnapshot>(saved);
            if (snapshot?.scenes == null)
                throw new InvalidOperationException("Cell-layer fixture Build Settings snapshot is invalid; refusing a partial restore.");
            var original = new EditorBuildSettingsScene[snapshot.scenes.Length];
            for (int index = 0; index < original.Length; index++)
                original[index] = new EditorBuildSettingsScene(snapshot.scenes[index].path, snapshot.scenes[index].enabled);
            EditorBuildSettings.scenes = original;
            DeleteOwnedAssetLifetimeFixtures();
            SessionState.EraseString(BuildSettingsSnapshotKey);
#endif
        }

#if UNITY_EDITOR
        private const string BuildSettingsSnapshotKey = "MSC.Tests.CellLayers.BuildSettingsBeforeRun";
        private const string AssetFixtureOwnershipKey = "MSC.Tests.CellLayers.OwnsAssetLifetimeFixture";
        [Serializable] private sealed class BuildSettingsSnapshot { public BuildSceneRecord[] scenes; }
        [Serializable] private sealed class BuildSceneRecord { public string path; public bool enabled; }

        private static void CreateAssetLifetimeFixtures()
        {
            AssetDatabase.CreateFolder("Assets/Game/World/Debug/Streaming", "GeneratedCellLayerAssetFixture");
            SessionState.SetBool(AssetFixtureOwnershipKey, true);
            var mask = new Texture2D(16, 16, TextureFormat.RGBA32, false, true) { name = FixtureMaskName };
            mask.SetPixel(0, 0, new Color(0.25f, 0.5f, 0.75f, 1f));
            mask.Apply();
            AssetDatabase.CreateAsset(mask, AssetFixtureFolder + "/Mask.asset");
            var cell = ScriptableObject.CreateInstance<VegetationCellAsset>();
            cell.name = FixtureCellName;
            cell.ConfigureForAuthoring("cell_1_0", new WorldCellIndex(1, 0),
                new Bounds(new Vector3(768f, 0f, 256f), new Vector3(512f, 128f, 512f)), 16, 32f, mask);
            var records = new VegetationInstanceRecord[64];
            for (int index = 0; index < records.Length; index++)
                records[index] = new VegetationInstanceRecord(new Vector3(520f + index % 8, 0f, 8f + index / 8),
                    Vector3.up, 0f, 1f, 0, 0, 0);
            cell.ReplaceTileForAuthoring(new VegetationTileRecord(0, 0,
                new Bounds(new Vector3(528f, 0f, 16f), new Vector3(32f, 8f, 32f)),
                new[] { new VegetationProfileTileInstances(0, records) },
                Array.Empty<Vector3>(), Array.Empty<VegetationRejectedSample>()));
            AssetDatabase.CreateAsset(cell, AssetFixtureFolder + "/Cell.asset");
            var catalog = ScriptableObject.CreateInstance<VegetationCellCatalog>();
            catalog.name = FixtureCatalogName;
            // Renderer stays disabled: this isolates persistent asset ownership
            // from HDRP/graphics hardware without creating synthetic materials.
            catalog.ConfigureForAuthoring(new[] { cell }, Array.Empty<VegetationProfile>(), ~0, 256f, 512f);
            AssetDatabase.CreateAsset(catalog, AssetFixtureFolder + "/Catalog.asset");
            Scene previous = SceneManager.GetActiveScene();
            foreach (string path in new[] { SharedScenePath, OwnedScenePath })
            {
                Assert.That(AssetDatabase.CopyAsset(LayerPath, path), Is.True);
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    VegetationWorldRenderer renderer = scene.GetRootGameObjects()[0].AddComponent<VegetationWorldRenderer>();
                    renderer.enabled = false;
                    renderer.ConfigureForAuthoring(catalog);
                    Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                }
                finally
                {
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void DeleteOwnedAssetLifetimeFixtures()
        {
            if (!SessionState.GetBool(AssetFixtureOwnershipKey, false)) return;
            if (!AssetDatabase.DeleteAsset(AssetFixtureFolder) && System.IO.Directory.Exists(AssetFixtureFolder))
                throw new InvalidOperationException("Could not delete the owned test asset fixture: " + AssetFixtureFolder);
            SessionState.EraseBool(AssetFixtureOwnershipKey);
        }

        private static void AddFixture(List<EditorBuildSettingsScene> scenes, string path)
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null);
            int existing = scenes.FindIndex(scene => scene.path == path);
            if (existing >= 0 && scenes[existing].enabled) return;
            if (existing >= 0) scenes.RemoveAt(existing);
            scenes.Add(new EditorBuildSettingsScene(path, true));
        }

        private static int GetBuildIndex(string path)
        {
            int index = SceneUtility.GetBuildIndexByScenePath(path);
            Assert.That(index, Is.GreaterThanOrEqualTo(0),
                "IPrebuildSetup must register the fixture before the runtime scene table is created: " + path);
            Assert.That(SceneUtility.GetScenePathByBuildIndex(index), Is.EqualTo(path));
            return index;
        }
#endif
    }
}
