using System;
using MSC.World.Vegetation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Tests.PlayMode.WorldRemaster
{
    public sealed class PackedWoodyCollisionPoolPlayModeTests
    {
        private GameObject owner;
        private PackedWoodyCellAsset asset;

        [TearDown]
        public void TearDown()
        {
            if (owner != null) Object.DestroyImmediate(owner);
            if (asset != null) Object.DestroyImmediate(asset);
        }

        [Test]
        public void Pool_IsBoundedReportsOverflowAndSkipsStationaryRescan()
        {
            asset = CreateCollisionAsset(4);
            owner = new GameObject("Packed woody collision pool test");
            owner.SetActive(false);
            PackedWoodyCollisionPool pool =
                owner.AddComponent<PackedWoodyCollisionPool>();
            pool.ConfigureForAuthoring(asset);
            pool.ConfigurePoolForTests(20f, 30f, 2, 8);
            owner.SetActive(true);

            pool.SetTrackedPositionForTests(Vector3.zero);
            pool.RefreshImmediatelyForTests();
            Assert.That(pool.ActiveColliderCount, Is.EqualTo(2));
            Assert.That(pool.CreatedColliderCount, Is.EqualTo(2));
            Assert.That(pool.LastCandidateCount, Is.EqualTo(4));
            Assert.That(pool.LastOverflowCandidateCount, Is.EqualTo(2));
            Assert.That(owner.GetComponentsInChildren<CapsuleCollider>(true).Length,
                Is.EqualTo(2));

            int refreshes = pool.RefreshExecutionCount;
            int examinations = pool.LastCandidateRecordExaminationCount;
            int mutations = pool.TotalMutationCount;
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            pool.TickImmediatelyForTests();
            long allocated = GC.GetAllocatedBytesForCurrentThread() -
                allocatedBefore;
            Assert.That(pool.RefreshExecutionCount, Is.EqualTo(refreshes));
            Assert.That(pool.LastCandidateRecordExaminationCount,
                Is.EqualTo(examinations));
            Assert.That(pool.TotalMutationCount, Is.EqualTo(mutations));
            Assert.That(allocated, Is.Zero,
                "A stationary settled pool must not scan or allocate.");

            pool.SetTrackedPositionForTests(new Vector3(5f, 0f, 0f));
            pool.TickImmediatelyForTests();
            Assert.That(pool.RefreshExecutionCount, Is.EqualTo(refreshes + 1));

            pool.SetTrackedPositionForTests(new Vector3(500f, 0f, 500f));
            pool.RefreshImmediatelyForTests();
            Assert.That(pool.ActiveColliderCount, Is.Zero);
        }

        [Test]
        public void NonPlayableCategory_NeverCreatesCollisionObjects()
        {
            asset = CreateCollisionAsset(1,
                PackedWoodyCategory.BoundaryForest);
            owner = new GameObject("Boundary collision pool test");
            owner.SetActive(false);
            PackedWoodyCollisionPool pool =
                owner.AddComponent<PackedWoodyCollisionPool>();
            pool.ConfigureForAuthoring(asset);
            LogAssert.Expect(
                LogType.Error,
                "PACKED_WOODY_COLLISION_INVALID object=" +
                "Boundary collision pool test: collision asset category is " +
                "not playable OriginalTree");
            owner.SetActive(true);
            Assert.That(pool.enabled, Is.False,
                "An invalid collision category must fail closed.");
            pool.SetTrackedPositionForTests(Vector3.zero);
            pool.RefreshImmediatelyForTests();
            Assert.That(pool.ActiveColliderCount, Is.Zero);
            Assert.That(pool.CreatedColliderCount, Is.Zero);
        }

        [Test]
        public void OverflowSelection_KeepsNearestCollisionRecords()
        {
            asset = CreateCollisionAsset(4);
            owner = new GameObject("Packed woody nearest collision test");
            owner.SetActive(false);
            PackedWoodyCollisionPool pool =
                owner.AddComponent<PackedWoodyCollisionPool>();
            pool.ConfigureForAuthoring(asset);
            pool.ConfigurePoolForTests(20f, 30f, 2, 8);
            owner.SetActive(true);

            pool.SetTrackedPositionForTests(new Vector3(6f, 0f, 0f));
            pool.RefreshImmediatelyForTests();
            CapsuleCollider[] colliders =
                owner.GetComponentsInChildren<CapsuleCollider>();
            Assert.That(colliders.Length, Is.EqualTo(2));
            float first = colliders[0].transform.position.x;
            float second = colliders[1].transform.position.x;
            Assert.That(new[] { first, second },
                Is.EquivalentTo(new[] { 4f, 6f }));
        }

        [Test]
        public void PendingReleases_ContinueWithoutFurtherCameraMovement()
        {
            asset = CreateCollisionAsset(3);
            owner = new GameObject("Packed woody pending release test");
            owner.SetActive(false);
            PackedWoodyCollisionPool pool =
                owner.AddComponent<PackedWoodyCollisionPool>();
            pool.ConfigureForAuthoring(asset);
            pool.ConfigurePoolForTests(20f, 30f, 3, 1);
            owner.SetActive(true);

            pool.SetTrackedPositionForTests(Vector3.zero);
            pool.RefreshImmediatelyForTests();
            Assert.That(pool.ActiveColliderCount, Is.EqualTo(3));
            int refreshes = pool.RefreshExecutionCount;

            pool.SetTrackedPositionForTests(new Vector3(500f, 0f, 500f));
            pool.TickImmediatelyForTests();
            Assert.That(pool.ActiveColliderCount, Is.EqualTo(2));
            Assert.That(pool.RefreshExecutionCount, Is.EqualTo(refreshes + 1));

            pool.TickImmediatelyForTests();
            Assert.That(pool.ActiveColliderCount, Is.EqualTo(1));
            Assert.That(pool.RefreshExecutionCount, Is.EqualTo(refreshes + 2),
                "A stationary camera must continue budgeted pending releases.");

            pool.TickImmediatelyForTests();
            Assert.That(pool.ActiveColliderCount, Is.Zero);
            Assert.That(pool.RefreshExecutionCount, Is.EqualTo(refreshes + 3));

            pool.TickImmediatelyForTests();
            Assert.That(pool.RefreshExecutionCount, Is.EqualTo(refreshes + 3),
                "A settled stationary pool must return to the zero-scan path.");
        }

        [Test]
        public void ResidentPools_ShareOneFairGlobalMutationBudget()
        {
            asset = CreateCollisionAsset(64);
            PackedWoodyCellAsset secondAsset = CreateCollisionAsset(64);
            owner = CreateConfiguredPoolOwner("Global pool A", asset,
                out PackedWoodyCollisionPool first);
            GameObject secondOwner = CreateConfiguredPoolOwner(
                "Global pool B", secondAsset,
                out PackedWoodyCollisionPool second);
            try
            {
                first.SetTrackedPositionForTests(Vector3.zero);
                second.SetTrackedPositionForTests(Vector3.zero);

                PackedWoodyCollisionPool.TickAllPoolsImmediatelyForTests();
                Assert.That(PackedWoodyCollisionPool.LastGlobalMutationCount,
                    Is.EqualTo(
                        PackedWoodyCollisionPool
                            .GlobalMaximumMutationsPerFixedUpdate));
                Assert.That(first.TotalMutationCount +
                    second.TotalMutationCount,
                    Is.EqualTo(PackedWoodyCollisionPool
                        .GlobalMaximumMutationsPerFixedUpdate));

                PackedWoodyCollisionPool.TickAllPoolsImmediatelyForTests();
                Assert.That(PackedWoodyCollisionPool.LastGlobalMutationCount,
                    Is.EqualTo(PackedWoodyCollisionPool
                        .GlobalMaximumMutationsPerFixedUpdate));
                Assert.That(first.TotalMutationCount, Is.GreaterThan(0));
                Assert.That(second.TotalMutationCount, Is.GreaterThan(0),
                    "Round-robin progress must prevent a later cell from starving.");
                Assert.That(first.TotalMutationCount +
                    second.TotalMutationCount,
                    Is.EqualTo(PackedWoodyCollisionPool
                        .GlobalMaximumMutationsPerFixedUpdate * 2));
            }
            finally
            {
                Object.DestroyImmediate(secondOwner);
                Object.DestroyImmediate(secondAsset);
            }
        }

        [Test]
        public void ResidentPools_ShareOneGlobalRefreshScanBudget()
        {
            const int poolCount = 5;
            var owners = new GameObject[poolCount];
            var assets = new PackedWoodyCellAsset[poolCount];
            var pools = new PackedWoodyCollisionPool[poolCount];
            try
            {
                for (int index = 0; index < poolCount; index++)
                {
                    assets[index] = CreateCollisionAsset(1);
                    owners[index] = CreateConfiguredPoolOwner(
                        "Global refresh pool " + index,
                        assets[index],
                        out pools[index]);
                    pools[index].SetTrackedPositionForTests(Vector3.zero);
                }
                int before = 0;
                for (int index = 0; index < poolCount; index++)
                    before += pools[index].RefreshExecutionCount;

                PackedWoodyCollisionPool.TickAllPoolsImmediatelyForTests();

                int executed = 0;
                for (int index = 0; index < poolCount; index++)
                    executed += pools[index].RefreshExecutionCount;
                executed -= before;
                Assert.That(executed, Is.EqualTo(
                    PackedWoodyCollisionPool
                        .GlobalMaximumPoolRefreshesPerFixedUpdate));
                Assert.That(
                    PackedWoodyCollisionPool.LastGlobalEligiblePoolCount,
                    Is.EqualTo(PackedWoodyCollisionPool
                        .GlobalMaximumPoolRefreshesPerFixedUpdate));
                Assert.That(
                    PackedWoodyCollisionPool.LastGlobalMutationCount,
                    Is.LessThanOrEqualTo(PackedWoodyCollisionPool
                        .GlobalMaximumMutationsPerFixedUpdate));
            }
            finally
            {
                for (int index = 0; index < poolCount; index++)
                {
                    if (owners[index] != null)
                        Object.DestroyImmediate(owners[index]);
                    if (assets[index] != null)
                        Object.DestroyImmediate(assets[index]);
                }
            }
        }

        [Test]
        public void MultipleGameCameras_DoNotUseLastRenderOrderAsAuthority()
        {
            asset = CreateCollisionAsset(1);
            owner = new GameObject("Packed woody multi-camera test");
            owner.SetActive(false);
            PackedWoodyCollisionPool pool =
                owner.AddComponent<PackedWoodyCollisionPool>();
            pool.ConfigureForAuthoring(asset);
            owner.SetActive(true);

            var gameplayOwner = new GameObject("Gameplay camera");
            var captureOwner = new GameObject("Capture camera");
            var captureTarget = new RenderTexture(8, 8, 0);
            Camera gameplayCamera = gameplayOwner.AddComponent<Camera>();
            Camera captureCamera = captureOwner.AddComponent<Camera>();
            gameplayOwner.transform.position = new Vector3(10f, 2f, 3f);
            captureOwner.transform.position = new Vector3(100f, 40f, 30f);
            captureCamera.targetTexture = captureTarget;
            try
            {
                pool.ConsiderGameCameraForTests(captureCamera);
                pool.ConsiderGameCameraForTests(gameplayCamera);
                Assert.That(pool.TrackedPositionForTests,
                    Is.EqualTo(gameplayOwner.transform.position));

                pool.ConsiderGameCameraForTests(captureCamera);
                Assert.That(pool.TrackedPositionForTests,
                    Is.EqualTo(gameplayOwner.transform.position),
                    "A later capture camera must not steal collision authority.");

                PackedWoodyCollisionPool.BindAuthoritativeGameCamera(
                    captureCamera);
                pool.ConsiderGameCameraForTests(gameplayCamera);
                pool.ConsiderGameCameraForTests(captureCamera);
                Assert.That(pool.TrackedPositionForTests,
                    Is.EqualTo(captureOwner.transform.position),
                    "An explicit gameplay-camera binding must win regardless " +
                    "of target texture or render order.");

                PackedWoodyCollisionPool.BindAuthoritativeGameCamera(
                    gameplayCamera);
                PackedWoodyCollisionPool.ReleaseAuthoritativeGameCamera(
                    captureCamera);
                pool.ConsiderGameCameraForTests(captureCamera);
                pool.ConsiderGameCameraForTests(gameplayCamera);
                Assert.That(pool.TrackedPositionForTests,
                    Is.EqualTo(gameplayOwner.transform.position),
                    "A previous session must not clear the newer camera " +
                    "authority during deferred destruction.");
            }
            finally
            {
                PackedWoodyCollisionPool.ReleaseAuthoritativeGameCamera(
                    gameplayCamera);
                PackedWoodyCollisionPool.ReleaseAuthoritativeGameCamera(
                    captureCamera);
                captureCamera.targetTexture = null;
                Object.DestroyImmediate(captureTarget);
                Object.DestroyImmediate(captureOwner);
                Object.DestroyImmediate(gameplayOwner);
            }
        }

        private static GameObject CreateConfiguredPoolOwner(
            string name,
            PackedWoodyCellAsset configuredAsset,
            out PackedWoodyCollisionPool pool)
        {
            var configuredOwner = new GameObject(name);
            configuredOwner.SetActive(false);
            pool = configuredOwner.AddComponent<PackedWoodyCollisionPool>();
            pool.ConfigureForAuthoring(configuredAsset);
            pool.ConfigurePoolForTests(200f, 220f, 64, 48);
            configuredOwner.SetActive(true);
            return configuredOwner;
        }

        private static PackedWoodyCellAsset CreateCollisionAsset(
            int count,
            PackedWoodyCategory category = PackedWoodyCategory.OriginalTree)
        {
            var configured =
                ScriptableObject.CreateInstance<PackedWoodyCellAsset>();
            var collisions = new PackedWoodyCollisionRecord[count];
            for (int index = 0; index < count; index++)
            {
                collisions[index] = new PackedWoodyCollisionRecord(
                    Hash128.Compute("collision:" + index),
                    new Vector3(index * 2f, 0f, 0f),
                    10f,
                    .2f);
            }
            var tiles = count == 0
                ? Array.Empty<PackedWoodyCollisionTile>()
                : new[]
                {
                    new PackedWoodyCollisionTile(
                        0, 0, 0, count,
                        new Bounds(
                            new Vector3(Mathf.Max(0f, count - 1), 5f, 0f),
                            new Vector3(Mathf.Max(2f, count * 2f), 10f, 2f)))
                };
            configured.ConfigureForAuthoring(
                "test-generator",
                "test-presentation",
                "cell_0_0",
                "fingerprint",
                category,
                new Bounds(Vector3.zero, Vector3.one * 100f),
                Array.Empty<PackedWoodyPrototypeAsset>(),
                Array.Empty<PackedWoodyBatch>(),
                Array.Empty<PackedWoodyPlacementRecord>(),
                tiles,
                collisions);
            return configured;
        }
    }
}
