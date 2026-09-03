using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MSC.Core.Identity;
using MSC.Interaction.Carrying;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace MSC.Save.Integration.Tests.PlayMode
{
    public sealed class WorldEntityStreamingPhysicsPlayModeTests
    {
        private const string FixtureScenePrefix =
            "MSC_SaveStreamingPhysicsFixture_";
        private const string ShelfEntityId =
            "4998a542ebf04f56ba12a2a82e6a4481";
        private const string WallEntityId =
            "1df605261ef54fc280b2783cc62c8670";
        private const string PersistentEntityId =
            "304e2a89fa984aa3baf81764073cb00f";
        private const string ProductionTeleportEntityId =
            "91e3a403ca83456889199606c5d61aee";
        private const string RehomedEntityId =
            "105bd56ae74f4514a44514c238e3c193";
        private const string RollbackEntityId =
            "ed3af4e168e841b48a3ce7cd44a6610d";
        private const string SyntheticCellScenePath =
            "Assets/Game/World/Generated/ProductionCells/Production_cell_0_-3.unity";
        private const string SyntheticDestinationCellScenePath =
            "Assets/Game/World/Generated/ProductionCells/Production_cell_0_-2.unity";
        private const int ReloadCycleCount = 2;
        private const int PostRestoreFixedSteps = 8;
        private const float PositionToleranceMeters = 0.025f;
        private const float RotationToleranceDegrees = 1f;
        private const float MaximumLinearSpeedMetersPerSecond = 0.25f;
        private const float MaximumAngularSpeedRadiansPerSecond = 0.5f;
        private const float MaximumContactImpulseNewtonSeconds = 2f;
        private const float MaximumPenetrationMeters = 0.005f;
        private static readonly Vector3 SyntheticCellOrigin =
            new Vector3(0f, 1000f, 0f);
        private readonly List<Object> teardownObjects = new List<Object>();

        [UnityTest]
        public IEnumerator ShelfPickup_TwoActualCellReloadsRestoreAfterStaticSupportWithoutPhysicsBurst()
        {
            yield return ExerciseTwoReloadCycles(
                ContactFixtureKind.Shelf,
                ShelfEntityId);
        }

        [UnityTest]
        public IEnumerator WallContactPickup_TwoActualCellReloadsRestoreAfterStaticSupportWithoutPhysicsBurst()
        {
            yield return ExerciseTwoReloadCycles(
                ContactFixtureKind.Wall,
                WallEntityId);
        }

        [UnityTest]
        public IEnumerator PersistentPickupInsideUnloadingCell_RemainsSuspendedUntilStaticSupportReturns()
        {
            Scene persistentScene = CreateFixtureScene(
                ContactFixtureKind.Shelf,
                100);
            Scene cellScene = default;
            yield return LoadSyntheticCellScene(loaded => cellScene = loaded);
            BoxCollider[] supports = CreateStaticSupports(
                cellScene,
                ContactFixtureKind.Shelf,
                SyntheticCellOrigin);
            Physics.SyncTransforms();

            StreamingFixture streaming = CreateStreamingFixture(persistentScene);
            Vector3 expectedPosition =
                SyntheticCellOrigin + new Vector3(0f, 0.25f, 0f);
            PickupFixture persistentPickup = CreatePickup(
                persistentScene,
                PersistentEntityId,
                expectedPosition,
                Quaternion.identity);
            streaming.Participant.RegisterScene(persistentScene);
            PrepareSettledBody(persistentPickup.Body);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            PrepareSettledBody(persistentPickup.Body);
            expectedPosition = persistentPickup.Body.position;

            streaming.Participant.CaptureAndUnregisterScene(cellScene);

            Assert.That(persistentPickup.Root.scene.handle,
                Is.EqualTo(persistentScene.handle));
            Assert.That(persistentPickup.Body.isKinematic, Is.True,
                "persistent pickup was not frozen before its support cell unloaded");
            Assert.That(persistentPickup.Body.detectCollisions, Is.False,
                "persistent pickup remained collidable while its support cell unloaded");
            AssertVectorBounded(
                persistentPickup.Body.linearVelocity,
                0.0001f,
                "persistent pre-unload linear velocity");
            AssertVectorBounded(
                persistentPickup.Body.angularVelocity,
                0.0001f,
                "persistent pre-unload angular velocity");
            Assert.That(streaming.Deferred.Count, Is.Zero,
                "persistent pickup must suspend in place, not create a canonical deferred clone");
            Assert.That(
                streaming.Participant.TryResolve(
                    PersistentEntityId,
                    out PhysicsPickupTarget suspended),
                Is.True);
            Assert.That(suspended, Is.SameAs(persistentPickup.Pickup));
            Assert.That(CountLoadedStableId(PersistentEntityId), Is.EqualTo(1));

            AsyncOperation unload = SceneManager.UnloadSceneAsync(cellScene);
            Assert.That(unload, Is.Not.Null);
            yield return unload;
            Assert.That(persistentPickup.Root != null, Is.True,
                "persistent pickup was destroyed with the streamed cell");
            yield return new WaitForFixedUpdate();
            AssertPose(
                persistentPickup.Body,
                expectedPosition,
                Quaternion.identity,
                "persistent pickup while support is absent");
            Assert.That(persistentPickup.Body.isKinematic, Is.True);
            Assert.That(persistentPickup.Body.detectCollisions, Is.False);

            yield return LoadSyntheticCellScene(loaded => cellScene = loaded);
            supports = CreateStaticSupports(
                cellScene,
                ContactFixtureKind.Shelf,
                SyntheticCellOrigin);
            Physics.SyncTransforms();
            WorldEntitySaveParticipant.SceneRegistrationBatch batch =
                streaming.Participant.BeginRegisterScene(cellScene);

            Assert.That(batch, Is.Not.Null);
            Assert.That(batch.TargetCount, Is.EqualTo(1));
            Assert.That(persistentPickup.Body.isKinematic, Is.True,
                "persistent dynamics released before replacement support sync");
            Assert.That(persistentPickup.Body.detectCollisions, Is.False,
                "persistent collisions restored before replacement support sync");
            AssertPose(
                persistentPickup.Body,
                expectedPosition,
                Quaternion.identity,
                "persistent collisionless pose apply");

            WorldEntitySaveParticipant.CompleteRegisterScene(batch);

            Assert.That(persistentPickup.Body.detectCollisions, Is.True);
            Assert.That(persistentPickup.Body.isKinematic, Is.False);
            Assert.That(persistentPickup.Body.useGravity, Is.True);
            for (int fixedStep = 0;
                 fixedStep < PostRestoreFixedSteps;
                 fixedStep++)
            {
                yield return new WaitForFixedUpdate();
            }

            AssertStableContact(
                persistentPickup,
                supports,
                expectedPosition,
                Quaternion.identity,
                "persistent pickup after support-cell reload");
            Assert.That(CountLoadedStableId(PersistentEntityId), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ProductionLikeTeleport_AutomaticStreamingGuardsPersistentShelfPickupUntilSupportReloads()
        {
            Scene persistentScene = CreateFixtureScene(
                ContactFixtureKind.Shelf,
                103);
            StreamingFixture streaming = CreateStreamingFixture(
                persistentScene,
                unloadRadiusCells: 0,
                destinationFirst: true);

            var focusRoot = new GameObject("ProductionLikeTeleportFocus");
            SceneManager.MoveGameObjectToScene(focusRoot, persistentScene);
            Vector3 sourceFocusPosition =
                SyntheticCellOrigin + new Vector3(16f, 0f, 16f);
            Vector3 destinationFocusPosition =
                SyntheticCellOrigin + new Vector3(528f, 0f, 16f);
            focusRoot.transform.position = sourceFocusPosition;

            PickupFixture persistentPickup = null;
            BoxCollider[] currentSourceSupports = Array.Empty<BoxCollider>();
            int sourceLoadCount = 0;
            int sourceUnloadCount = 0;
            bool guardedInsideWillUnload = false;
            bool supportAliveWhileGuarded = false;
            bool guardedBeforeReloadCompletion = false;

            streaming.Service.OwnedSceneWillUnload += scene =>
            {
                streaming.Participant.CaptureAndUnregisterScene(scene);
                if (!string.Equals(
                        scene.path,
                        SyntheticCellScenePath,
                        StringComparison.Ordinal))
                {
                    return;
                }

                sourceUnloadCount++;
                if (persistentPickup == null)
                {
                    return;
                }

                guardedInsideWillUnload =
                    persistentPickup.Body.isKinematic &&
                    !persistentPickup.Body.detectCollisions &&
                    persistentPickup.Body.linearVelocity.sqrMagnitude <= 0.00000001f &&
                    persistentPickup.Body.angularVelocity.sqrMagnitude <= 0.00000001f;
                supportAliveWhileGuarded =
                    scene.isLoaded &&
                    currentSourceSupports.All(
                        support => support != null &&
                                   support.enabled &&
                                   support.gameObject.scene.handle == scene.handle);
            };
            streaming.Service.OwnedSceneLoaded += scene =>
            {
                if (string.Equals(
                        scene.path,
                        SyntheticCellScenePath,
                        StringComparison.Ordinal))
                {
                    sourceLoadCount++;
                    currentSourceSupports = CreateStaticSupports(
                        scene,
                        ContactFixtureKind.Shelf,
                        SyntheticCellOrigin);
                    Physics.SyncTransforms();
                }

                WorldEntitySaveParticipant.SceneRegistrationBatch batch =
                    streaming.Participant.BeginRegisterScene(scene);
                if (persistentPickup != null && sourceLoadCount >= 2 &&
                    string.Equals(
                        scene.path,
                        SyntheticCellScenePath,
                        StringComparison.Ordinal))
                {
                    guardedBeforeReloadCompletion =
                        batch != null &&
                        batch.TargetCount == 1 &&
                        persistentPickup.Body.isKinematic &&
                        !persistentPickup.Body.detectCollisions &&
                        currentSourceSupports.All(
                            support => support != null && support.enabled);
                }

                WorldEntitySaveParticipant.CompleteRegisterScene(batch);
            };

            streaming.Service.BindFocus(focusRoot.transform);
            yield return streaming.Service.RefreshNow();

            Assert.That(streaming.Service.IsCellLoaded("cell_0_0"), Is.True);
            Assert.That(streaming.Service.IsCellLoaded("cell_1_0"), Is.False);
            Assert.That(sourceLoadCount, Is.EqualTo(1));
            Assert.That(currentSourceSupports.Length, Is.GreaterThan(0));

            Vector3 expectedPosition =
                SyntheticCellOrigin + new Vector3(0f, 0.25f, 0f);
            persistentPickup = CreatePickup(
                persistentScene,
                ProductionTeleportEntityId,
                expectedPosition,
                Quaternion.identity);
            streaming.Participant.RegisterScene(persistentScene);
            PrepareSettledBody(persistentPickup.Body);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            PrepareSettledBody(persistentPickup.Body);
            expectedPosition = persistentPickup.Body.position;
            AssertStableContact(
                persistentPickup,
                currentSourceSupports,
                expectedPosition,
                Quaternion.identity,
                "production-like teleport initial shelf contact");

            int automaticRefreshCount =
                streaming.Service.CompletedAutomaticRefreshCount;
            focusRoot.transform.position = destinationFocusPosition;
            Physics.SyncTransforms();
            yield return WaitForAutomaticStreaming(
                streaming.Service,
                automaticRefreshCount,
                () => streaming.Service.IsCellLoaded("cell_1_0") &&
                      !streaming.Service.IsCellLoaded("cell_0_0"),
                "teleport away from source shelf");

            Assert.That(sourceUnloadCount, Is.EqualTo(1));
            Assert.That(guardedInsideWillUnload, Is.True,
                "persistent pickup was not frozen inside the real service will-unload callback");
            Assert.That(supportAliveWhileGuarded, Is.True,
                "source support was gone before the real service will-unload guard completed");
            Assert.That(persistentPickup.Root.scene.handle,
                Is.EqualTo(persistentScene.handle));
            Assert.That(persistentPickup.Body.isKinematic, Is.True);
            Assert.That(persistentPickup.Body.detectCollisions, Is.False);
            AssertPose(
                persistentPickup.Body,
                expectedPosition,
                Quaternion.identity,
                "production-like teleport while source support is absent");
            Assert.That(
                CountLoadedStableId(ProductionTeleportEntityId),
                Is.EqualTo(1));

            automaticRefreshCount =
                streaming.Service.CompletedAutomaticRefreshCount;
            focusRoot.transform.position = sourceFocusPosition;
            Physics.SyncTransforms();
            yield return WaitForAutomaticStreaming(
                streaming.Service,
                automaticRefreshCount,
                () => streaming.Service.IsCellLoaded("cell_0_0") &&
                      !streaming.Service.IsCellLoaded("cell_1_0"),
                "teleport back to source shelf");

            Assert.That(sourceLoadCount, Is.EqualTo(2));
            Assert.That(guardedBeforeReloadCompletion, Is.True,
                "persistent pickup dynamics were released before reloaded support was live and synced");
            Assert.That(persistentPickup.Body.detectCollisions, Is.True);
            Assert.That(persistentPickup.Body.isKinematic, Is.False);
            Assert.That(persistentPickup.Body.useGravity, Is.True);
            for (int fixedStep = 0;
                 fixedStep < PostRestoreFixedSteps;
                 fixedStep++)
            {
                yield return new WaitForFixedUpdate();
            }

            AssertStableContact(
                persistentPickup,
                currentSourceSupports,
                expectedPosition,
                Quaternion.identity,
                "production-like teleport after support reload");
            Assert.That(
                CountLoadedStableId(ProductionTeleportEntityId),
                Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RehomedAuthoritativePickup_StreamedCanonicalCloneStaysGuardedUntilDestroyed()
        {
            Scene persistentScene = CreateFixtureScene(
                ContactFixtureKind.Wall,
                101);
            Scene cellScene = default;
            yield return LoadSyntheticCellScene(loaded => cellScene = loaded);
            CreateStaticSupports(
                cellScene,
                ContactFixtureKind.Shelf,
                SyntheticCellOrigin);
            Physics.SyncTransforms();

            StreamingFixture streaming = CreateStreamingFixture(persistentScene);
            Vector3 position =
                SyntheticCellOrigin + new Vector3(0f, 0.25f, 0f);
            PickupFixture authoritative = CreatePickup(
                cellScene,
                RehomedEntityId,
                position,
                Quaternion.identity);
            streaming.Participant.RegisterScene(cellScene);
            authoritative.Pickup.NotifyPickedUp(default);
            authoritative.Body.linearVelocity = Vector3.zero;
            authoritative.Body.angularVelocity = Vector3.zero;
            authoritative.Body.detectCollisions = false;
            authoritative.Body.isKinematic = true;

            streaming.Participant.CaptureAndUnregisterScene(cellScene);

            Assert.That(authoritative.Root.scene.handle,
                Is.EqualTo(persistentScene.handle),
                "authoritative pickup was not rehomed before source-cell teardown");
            Assert.That(
                streaming.Participant.TryResolve(
                    RehomedEntityId,
                    out PhysicsPickupTarget resolved),
                Is.True);
            Assert.That(resolved, Is.SameAs(authoritative.Pickup));

            AsyncOperation unload = SceneManager.UnloadSceneAsync(cellScene);
            Assert.That(unload, Is.Not.Null);
            yield return unload;
            Assert.That(CountLoadedStableId(RehomedEntityId), Is.EqualTo(1));

            yield return LoadSyntheticCellScene(loaded => cellScene = loaded);
            CreateStaticSupports(
                cellScene,
                ContactFixtureKind.Shelf,
                SyntheticCellOrigin);
            Physics.SyncTransforms();
            PickupFixture canonicalClone = CreatePickup(
                cellScene,
                RehomedEntityId,
                position,
                Quaternion.identity);

            WorldEntitySaveParticipant.SceneRegistrationBatch batch =
                streaming.Participant.BeginRegisterScene(cellScene);

            Assert.That(batch, Is.Not.Null);
            Assert.That(batch.TargetCount, Is.EqualTo(1));
            Assert.That(canonicalClone.Root != null, Is.True,
                "clone should remain inspectable until Unity's end-of-frame destroy");
            Assert.That(canonicalClone.Root.activeSelf, Is.False,
                "discarded canonical clone remained active before destroy");
            Assert.That(canonicalClone.Body.isKinematic, Is.True,
                "discarded canonical clone was dynamic before destroy");
            Assert.That(canonicalClone.Body.detectCollisions, Is.False,
                "discarded canonical clone remained collidable before destroy");

            WorldEntitySaveParticipant.CompleteRegisterScene(batch);

            Assert.That(canonicalClone.Root != null, Is.True);
            Assert.That(canonicalClone.Root.activeSelf, Is.False,
                "batch completion reactivated a discarded clone");
            Assert.That(canonicalClone.Body.isKinematic, Is.True,
                "batch completion released a discarded clone's dynamics");
            Assert.That(canonicalClone.Body.detectCollisions, Is.False,
                "batch completion restored a discarded clone's collisions");
            Assert.That(
                streaming.Participant.TryResolve(
                    RehomedEntityId,
                    out resolved),
                Is.True);
            Assert.That(resolved, Is.SameAs(authoritative.Pickup));
            Assert.That(CountLoadedStableId(RehomedEntityId), Is.EqualTo(2),
                "scheduled clone should still be visible to inactive-inclusive diagnostics");

            yield return null;

            Assert.That(canonicalClone.Root == null, Is.True,
                "discarded canonical clone survived the end-of-frame destroy boundary");
            Assert.That(CountLoadedStableId(RehomedEntityId), Is.EqualTo(1),
                "stable-ID diagnostics still see a duplicate after clone destruction");
            Assert.That(
                streaming.Participant.TryResolve(
                    RehomedEntityId,
                    out resolved),
                Is.True);
            Assert.That(resolved, Is.SameAs(authoritative.Pickup));
        }

        [UnityTest]
        public IEnumerator TransactionalRollback_CrossCellSuspensionRestoresSourceTopologyAndPhysics()
        {
            Scene persistentScene = CreateFixtureScene(
                ContactFixtureKind.Shelf,
                102);
            Scene sourceScene = default;
            Scene destinationScene = default;
            yield return LoadSyntheticCellScene(loaded => sourceScene = loaded);

            StreamingFixture streaming = CreateStreamingFixture(persistentScene);
            Assert.That(streaming.Service.IsCellLoaded("cell_0_0"), Is.True);
            Assert.That(streaming.Service.IsCellLoaded("cell_1_0"), Is.False);

            var topologyParent = new GameObject("RollbackTopologyParent");
            SceneManager.MoveGameObjectToScene(topologyParent, sourceScene);
            var beforeSibling = new GameObject("RollbackBeforeSibling");
            beforeSibling.transform.SetParent(
                topologyParent.transform,
                worldPositionStays: false);

            Vector3 originalPosition =
                SyntheticCellOrigin + new Vector3(8f, 4f, 8f);
            Quaternion originalRotation = Quaternion.Euler(7f, 21f, 3f);
            PickupFixture pickup = CreatePickup(
                sourceScene,
                RollbackEntityId,
                originalPosition,
                originalRotation);
            pickup.Root.transform.SetParent(
                topologyParent.transform,
                worldPositionStays: true);

            var afterSibling = new GameObject("RollbackAfterSibling");
            afterSibling.transform.SetParent(
                topologyParent.transform,
                worldPositionStays: false);
            Assert.That(pickup.Root.transform.GetSiblingIndex(), Is.EqualTo(1));

            streaming.Participant.RegisterScene(sourceScene);
            pickup.Body.detectCollisions = true;
            pickup.Body.interpolation = RigidbodyInterpolation.Extrapolate;
            pickup.Body.isKinematic = false;
            pickup.Body.useGravity = true;
            pickup.Body.linearVelocity = Vector3.zero;
            pickup.Body.angularVelocity = Vector3.zero;
            pickup.Body.Sleep();
            Physics.SyncTransforms();
            Assert.That(pickup.Body.IsSleeping(), Is.True);

            object checkpoint = streaming.Participant.CaptureCheckpoint();
            Vector3 restoredDestinationPosition =
                SyntheticCellOrigin + new Vector3(520f, 6f, 8f);
            Quaternion restoredDestinationRotation =
                Quaternion.Euler(15f, 70f, 9f);
            var saved = new WorldEntityDomainSaveDto
            {
                entities = new[]
                {
                    new WorldEntityStateDto
                    {
                        stableEntityId = RollbackEntityId,
                        sourceCellId = "cell_0_0",
                        worldPosition = restoredDestinationPosition,
                        worldRotation = restoredDestinationRotation,
                        linearVelocity = new Vector3(3f, -1f, 2f),
                        angularVelocity = new Vector3(0.5f, 1f, -0.25f),
                        isKinematic = false,
                        useGravity = true,
                        sleeping = false,
                        activeSelf = true,
                    },
                },
            };
            var unresolved = new UnresolvedContentReport();
            object prepared = streaming.Participant.PrepareRestore(
                CreateWorldEntityEnvelope(JsonUtility.ToJson(saved)),
                new SaveRestorePreparationContext(
                    unresolved,
                    streaming.Deferred));

            streaming.Participant.ApplyPreparedRestore(
                prepared,
                new SaveRestoreContext(unresolved, streaming.Deferred));

            Assert.That(pickup.Root.scene.handle,
                Is.EqualTo(persistentScene.handle),
                "cross-cell restore did not rehome the authoritative body");
            Assert.That(pickup.Root.transform.parent, Is.Null,
                "rehomed body retained a parent from the streamed source scene");
            AssertPose(
                pickup.Body,
                restoredDestinationPosition,
                restoredDestinationRotation,
                "cross-cell guarded restore");
            Assert.That(pickup.Body.isKinematic, Is.True,
                "unloaded destination support did not suspend dynamics");
            Assert.That(pickup.Body.detectCollisions, Is.False,
                "unloaded destination support did not guard collisions");
            Assert.That(
                pickup.Body.interpolation,
                Is.EqualTo(RigidbodyInterpolation.None));
            AssertVectorBounded(
                pickup.Body.linearVelocity,
                0.0001f,
                "cross-cell suspended linear velocity");
            AssertVectorBounded(
                pickup.Body.angularVelocity,
                0.0001f,
                "cross-cell suspended angular velocity");
            Assert.That(CountLoadedStableId(RollbackEntityId), Is.EqualTo(1));

            streaming.Participant.Rollback(checkpoint);

            AssertRollbackSourceState(
                streaming,
                pickup,
                sourceScene,
                topologyParent.transform,
                beforeSibling.transform,
                afterSibling.transform,
                originalPosition,
                originalRotation,
                "immediate rollback");

            // Loading the former destination must not find a stale suspension
            // and release the abandoned restore state back onto the body.
            yield return LoadSyntheticCellScene(
                SyntheticDestinationCellScenePath,
                loaded => destinationScene = loaded);
            Physics.SyncTransforms();
            WorldEntitySaveParticipant.SceneRegistrationBatch batch =
                streaming.Participant.BeginRegisterScene(destinationScene);

            Assert.That(batch, Is.Not.Null);
            Assert.That(
                Enumerable.Range(0, batch.TargetCount).All(
                    index => batch.GetTarget(index) != pickup.Pickup),
                Is.True,
                "rollback left the source pickup queued for destination release");
            WorldEntitySaveParticipant.CompleteRegisterScene(batch);

            AssertRollbackSourceState(
                streaming,
                pickup,
                sourceScene,
                topologyParent.transform,
                beforeSibling.transform,
                afterSibling.transform,
                originalPosition,
                originalRotation,
                "after abandoned destination load");
        }

        [UnityTearDown]
        public IEnumerator UnloadFixtureScenes()
        {
            for (int objectIndex = 0;
                 objectIndex < teardownObjects.Count;
                 objectIndex++)
            {
                if (teardownObjects[objectIndex] != null)
                {
                    Object.Destroy(teardownObjects[objectIndex]);
                }
            }
            teardownObjects.Clear();
            yield return null;

            for (int sceneIndex = SceneManager.sceneCount - 1;
                 sceneIndex >= 0;
                 sceneIndex--)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                bool isFixtureScene = scene.IsValid() &&
                    scene.isLoaded &&
                    (scene.name.StartsWith(
                         FixtureScenePrefix,
                         StringComparison.Ordinal) ||
                     string.Equals(
                         scene.path,
                         SyntheticCellScenePath,
                         StringComparison.Ordinal) ||
                     string.Equals(
                         scene.path,
                         SyntheticDestinationCellScenePath,
                         StringComparison.Ordinal));
                if (!isFixtureScene)
                {
                    continue;
                }

                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    yield return unload;
                }
            }
        }

        private static IEnumerator ExerciseTwoReloadCycles(
            ContactFixtureKind fixtureKind,
            string stableEntityId)
        {
            var deferred = new DeferredStableEntityStore();
            var participant = new WorldEntitySaveParticipant(deferred);
            Vector3 expectedPosition = fixtureKind == ContactFixtureKind.Wall
                ? new Vector3(0f, 0.25f, 0.25f)
                : new Vector3(0f, 0.25f, 0f);
            Quaternion expectedRotation = Quaternion.identity;

            Scene currentScene = CreateFixtureScene(fixtureKind, 0);
            BoxCollider[] supports = CreateStaticSupports(
                currentScene,
                fixtureKind);
            Physics.SyncTransforms();
            PickupFixture current = CreatePickup(
                currentScene,
                stableEntityId,
                expectedPosition,
                expectedRotation);
            participant.RegisterScene(currentScene);
            PrepareSettledBody(current.Body);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            PrepareSettledBody(current.Body);
            expectedPosition = current.Body.position;
            expectedRotation = current.Body.rotation;
            AssertStableContact(
                current,
                supports,
                expectedPosition,
                expectedRotation,
                "initial fixture");

            for (int cycle = 1; cycle <= ReloadCycleCount; cycle++)
            {
                PrepareSettledBody(current.Body);
                Vector3 capturedPosition = current.Body.position;
                Quaternion capturedRotation = current.Body.rotation;

                participant.CaptureAndUnregisterScene(currentScene);

                Assert.That(
                    current.Body.isKinematic,
                    Is.True,
                    $"cycle {cycle}: the scene-owned body must be frozen before teardown");
                Assert.That(
                    current.Body.detectCollisions,
                    Is.False,
                    $"cycle {cycle}: the scene-owned body must be collisionless before teardown");
                AssertVectorBounded(
                    current.Body.linearVelocity,
                    0.0001f,
                    $"cycle {cycle}: pre-unload linear velocity");
                AssertVectorBounded(
                    current.Body.angularVelocity,
                    0.0001f,
                    $"cycle {cycle}: pre-unload angular velocity");
                Assert.That(
                    participant.TryResolve(stableEntityId, out _),
                    Is.False,
                    $"cycle {cycle}: unloaded canonical pickup stayed registered");
                Assert.That(
                    deferred.Count,
                    Is.EqualTo(1),
                    $"cycle {cycle}: deferred state must contain exactly one owner record");

                AsyncOperation unload = SceneManager.UnloadSceneAsync(currentScene);
                Assert.That(
                    unload,
                    Is.Not.Null,
                    $"cycle {cycle}: runtime SceneManager refused the fixture unload");
                yield return unload;
                Assert.That(
                    current.Root == null,
                    Is.True,
                    $"cycle {cycle}: the old scene-owned pickup survived actual unload");
                Assert.That(
                    CountLoadedStableId(stableEntityId),
                    Is.Zero,
                    $"cycle {cycle}: an old stable-ID instance leaked across unload");

                currentScene = CreateFixtureScene(fixtureKind, cycle);

                // Recreate static collision first, just as the native scene-load
                // boundary must do before any deferred dynamic pose is released.
                supports = CreateStaticSupports(currentScene, fixtureKind);
                Physics.SyncTransforms();
                Assert.That(
                    supports.All(support => support != null && support.enabled),
                    Is.True,
                    $"cycle {cycle}: support collision was not live before pickup creation");

                current = CreatePickup(
                    currentScene,
                    stableEntityId,
                    capturedPosition + new Vector3(0.75f, 1.25f, 0.75f),
                    Quaternion.Euler(25f, 40f, 15f));
                current.Body.linearVelocity = new Vector3(4f, -3f, 2f);
                current.Body.angularVelocity = new Vector3(2f, 3f, 1f);

                WorldEntitySaveParticipant.SceneRegistrationBatch batch =
                    participant.BeginRegisterScene(currentScene);

                Assert.That(batch, Is.Not.Null);
                Assert.That(
                    batch.TargetCount,
                    Is.EqualTo(1),
                    $"cycle {cycle}: registration did not isolate one dynamic pickup");
                Assert.That(
                    current.Body.isKinematic,
                    Is.True,
                    $"cycle {cycle}: restored pose became dynamic before static sync");
                Assert.That(
                    current.Body.detectCollisions,
                    Is.False,
                    $"cycle {cycle}: restored pose was collidable before static sync");
                AssertPose(
                    current.Body,
                    capturedPosition,
                    capturedRotation,
                    $"cycle {cycle}: collisionless deferred pose apply");
                Assert.That(
                    deferred.Count,
                    Is.Zero,
                    $"cycle {cycle}: deferred state was not consumed exactly once");
                Assert.That(
                    participant.TryResolve(
                        stableEntityId,
                        out PhysicsPickupTarget resolved),
                    Is.True,
                    $"cycle {cycle}: reloaded pickup was not registered");
                Assert.That(resolved, Is.SameAs(current.Pickup));
                Assert.That(
                    CountLoadedStableId(stableEntityId),
                    Is.EqualTo(1),
                    $"cycle {cycle}: duplicate stable-ID instance after recreation");

                WorldEntitySaveParticipant.CompleteRegisterScene(batch);

                Assert.That(
                    current.Body.detectCollisions,
                    Is.True,
                    $"cycle {cycle}: authored collision state was not restored");
                Assert.That(
                    current.Body.isKinematic,
                    Is.False,
                    $"cycle {cycle}: saved dynamic state was not released");
                Assert.That(
                    current.Body.useGravity,
                    Is.True,
                    $"cycle {cycle}: loose-item gravity was not restored");
                AssertPose(
                    current.Body,
                    capturedPosition,
                    capturedRotation,
                    $"cycle {cycle}: completed registration pose");

                for (int fixedStep = 0;
                     fixedStep < PostRestoreFixedSteps;
                     fixedStep++)
                {
                    yield return new WaitForFixedUpdate();
                }

                AssertStableContact(
                    current,
                    supports,
                    expectedPosition,
                    expectedRotation,
                    $"cycle {cycle}: post-restore physics");
                Assert.That(
                    CountLoadedStableId(stableEntityId),
                    Is.EqualTo(1),
                    $"cycle {cycle}: physics step introduced a duplicate stable ID");
            }
        }

        private static Scene CreateFixtureScene(
            ContactFixtureKind fixtureKind,
            int cycle)
        {
            string sceneName =
                FixtureScenePrefix + fixtureKind + "_" + cycle + "_" +
                Guid.NewGuid().ToString("N");
            Scene scene = SceneManager.CreateScene(sceneName);
            Assert.That(scene.IsValid(), Is.True);
            Assert.That(scene.isLoaded, Is.True);
            return scene;
        }

        private static BoxCollider[] CreateStaticSupports(
            Scene scene,
            ContactFixtureKind fixtureKind)
        {
            return CreateStaticSupports(
                scene,
                fixtureKind,
                Vector3.zero);
        }

        private static BoxCollider[] CreateStaticSupports(
            Scene scene,
            ContactFixtureKind fixtureKind,
            Vector3 origin)
        {
            var supports = new List<BoxCollider>
            {
                CreateStaticBox(
                    scene,
                    "StaticShelf",
                    origin + new Vector3(0f, -0.25f, 0f),
                    new Vector3(3f, 0.5f, 3f)),
            };
            if (fixtureKind == ContactFixtureKind.Wall)
            {
                supports.Add(CreateStaticBox(
                    scene,
                    "StaticWall",
                    origin + new Vector3(0f, 1.5f, -0.25f),
                    new Vector3(3f, 3f, 0.5f)));
            }

            return supports.ToArray();
        }

        private static IEnumerator LoadSyntheticCellScene(
            Action<Scene> receiveLoadedScene)
        {
            return LoadSyntheticCellScene(
                SyntheticCellScenePath,
                receiveLoadedScene);
        }

        private static IEnumerator LoadSyntheticCellScene(
            string scenePath,
            Action<Scene> receiveLoadedScene)
        {
            Assert.That(
                SceneManager.GetSceneByPath(scenePath).isLoaded,
                Is.False,
                "synthetic cell scene leaked from an earlier test");
            AsyncOperation load = SceneManager.LoadSceneAsync(
                scenePath,
                LoadSceneMode.Additive);
            Assert.That(load, Is.Not.Null,
                "runtime SceneManager refused the project-owned synthetic cell scene");
            yield return load;
            Scene loaded = SceneManager.GetSceneByPath(scenePath);
            Assert.That(loaded.IsValid(), Is.True);
            Assert.That(loaded.isLoaded, Is.True);
            receiveLoadedScene?.Invoke(loaded);
        }

        private StreamingFixture CreateStreamingFixture(
            Scene persistentScene,
            int unloadRadiusCells = 1,
            bool destinationFirst = false)
        {
            int buildIndex = SceneUtility.GetBuildIndexByScenePath(
                SyntheticCellScenePath);
            Assert.That(buildIndex, Is.GreaterThanOrEqualTo(0),
                "synthetic cell scene is not enabled in EditorBuildSettings");
            int destinationBuildIndex = SceneUtility.GetBuildIndexByScenePath(
                SyntheticDestinationCellScenePath);
            Assert.That(destinationBuildIndex, Is.GreaterThanOrEqualTo(0),
                "synthetic destination cell scene is not enabled in EditorBuildSettings");
            ProductionWorldStreamingManifest manifest =
                ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
            var sourceCell = new ProductionWorldCellScene(
                "cell_0_0",
                0,
                0,
                buildIndex,
                SyntheticCellScenePath);
            var destinationCell = new ProductionWorldCellScene(
                "cell_1_0",
                1,
                0,
                destinationBuildIndex,
                SyntheticDestinationCellScenePath);
            manifest.ConfigureForAuthoring(
                512f,
                0,
                unloadRadiusCells,
                destinationFirst
                    ? new[] { destinationCell, sourceCell }
                    : new[] { sourceCell, destinationCell });
            teardownObjects.Add(manifest);

            var serviceRoot = new GameObject("SyntheticWorldStreamingService");
            SceneManager.MoveGameObjectToScene(serviceRoot, persistentScene);
            ProductionWorldStreamingService service =
                serviceRoot.AddComponent<ProductionWorldStreamingService>();
            service.ConfigureForAuthoring(manifest);
            var deferred = new DeferredStableEntityStore();
            var participant = new WorldEntitySaveParticipant(
                deferred,
                service,
                persistentScene);
            return new StreamingFixture(service, participant, deferred);
        }

        private static IEnumerator WaitForAutomaticStreaming(
            ProductionWorldStreamingService service,
            int previousCompletedRefreshCount,
            Func<bool> completed,
            string context)
        {
            const int maximumFrames = 600;
            for (int frame = 0; frame < maximumFrames; frame++)
            {
                if (!service.IsStreaming &&
                    service.CompletedAutomaticRefreshCount >
                    previousCompletedRefreshCount &&
                    completed())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(
                context + " did not complete through automatic production streaming");
        }

        private static BoxCollider CreateStaticBox(
            Scene scene,
            string name,
            Vector3 position,
            Vector3 size)
        {
            var support = new GameObject(name);
            support.transform.position = position;
            BoxCollider collider = support.AddComponent<BoxCollider>();
            collider.size = size;
            SceneManager.MoveGameObjectToScene(support, scene);
            return collider;
        }

        private static PickupFixture CreatePickup(
            Scene scene,
            string stableEntityId,
            Vector3 position,
            Quaternion rotation)
        {
            var root = new GameObject("StreamingPickup_" + stableEntityId);
            root.transform.SetPositionAndRotation(position, rotation);
            SceneManager.MoveGameObjectToScene(root, scene);

            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = Vector3.one * 0.5f;
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.mass = 1f;
            body.useGravity = true;
            body.isKinematic = false;
            body.detectCollisions = true;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            StableEntityIdAuthoring identity =
                root.AddComponent<StableEntityIdAuthoring>();
            Assert.That(
                StableEntityId.TryParse(
                    stableEntityId,
                    out StableEntityId parsedId),
                Is.True);
            identity.InitializeExplicitRuntimeId(parsedId);

            PhysicsPickupTarget pickup =
                root.AddComponent<PhysicsPickupTarget>();
            pickup.Configure(
                body,
                identity,
                "Pickup",
                35f,
                useGravityWhenLoose: true);
            CollisionImpulseProbe impulseProbe =
                root.AddComponent<CollisionImpulseProbe>();
            return new PickupFixture(
                root,
                body,
                collider,
                pickup,
                impulseProbe);
        }

        private static void PrepareSettledBody(Rigidbody body)
        {
            body.detectCollisions = true;
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.Sleep();
        }

        private static void AssertStableContact(
            PickupFixture pickup,
            IReadOnlyList<BoxCollider> supports,
            Vector3 expectedPosition,
            Quaternion expectedRotation,
            string context)
        {
            AssertPose(pickup.Body, expectedPosition, expectedRotation, context);
            AssertVectorBounded(
                pickup.Body.linearVelocity,
                MaximumLinearSpeedMetersPerSecond,
                context + " linear velocity");
            AssertVectorBounded(
                pickup.Body.angularVelocity,
                MaximumAngularSpeedRadiansPerSecond,
                context + " angular velocity");
            Assert.That(
                pickup.ImpulseProbe.MaximumImpulseMagnitude,
                Is.LessThanOrEqualTo(MaximumContactImpulseNewtonSeconds),
                context + " contact impulse burst");

            float maximumPenetration = 0f;
            for (int index = 0; index < supports.Count; index++)
            {
                BoxCollider support = supports[index];
                if (Physics.ComputePenetration(
                        pickup.Collider,
                        pickup.Collider.transform.position,
                        pickup.Collider.transform.rotation,
                        support,
                        support.transform.position,
                        support.transform.rotation,
                        out _,
                        out float distance))
                {
                    maximumPenetration = Mathf.Max(
                        maximumPenetration,
                        distance);
                }
            }

            Assert.That(
                maximumPenetration,
                Is.LessThanOrEqualTo(MaximumPenetrationMeters),
                context + " collider penetration");
        }

        private static void AssertPose(
            Rigidbody body,
            Vector3 expectedPosition,
            Quaternion expectedRotation,
            string context)
        {
            Assert.That(
                IsFinite(body.position),
                Is.True,
                context + " position contains a non-finite component");
            Assert.That(
                IsFinite(body.rotation),
                Is.True,
                context + " rotation contains a non-finite component");
            Assert.That(
                Vector3.Distance(body.position, expectedPosition),
                Is.LessThanOrEqualTo(PositionToleranceMeters),
                context + " position drift");
            Assert.That(
                Quaternion.Angle(body.rotation, expectedRotation),
                Is.LessThanOrEqualTo(RotationToleranceDegrees),
                context + " rotation drift");
        }

        private static void AssertRollbackSourceState(
            StreamingFixture streaming,
            PickupFixture pickup,
            Scene sourceScene,
            Transform expectedParent,
            Transform beforeSibling,
            Transform afterSibling,
            Vector3 expectedPosition,
            Quaternion expectedRotation,
            string context)
        {
            Assert.That(pickup.Root.scene.handle, Is.EqualTo(sourceScene.handle),
                context + " scene");
            Assert.That(pickup.Root.transform.parent, Is.SameAs(expectedParent),
                context + " parent");
            Assert.That(expectedParent.childCount, Is.EqualTo(3),
                context + " child count");
            Assert.That(expectedParent.GetChild(0), Is.SameAs(beforeSibling),
                context + " leading sibling");
            Assert.That(expectedParent.GetChild(1),
                Is.SameAs(pickup.Root.transform),
                context + " pickup sibling index");
            Assert.That(expectedParent.GetChild(2), Is.SameAs(afterSibling),
                context + " trailing sibling");
            AssertPose(pickup.Body, expectedPosition, expectedRotation, context);
            Assert.That(pickup.Body.isKinematic, Is.False,
                context + " dynamic state");
            Assert.That(pickup.Body.useGravity, Is.True,
                context + " gravity state");
            Assert.That(pickup.Body.detectCollisions, Is.True,
                context + " collision state");
            Assert.That(
                pickup.Body.interpolation,
                Is.EqualTo(RigidbodyInterpolation.Extrapolate),
                context + " interpolation state");
            Assert.That(pickup.Body.IsSleeping(), Is.True,
                context + " sleeping state");
            AssertVectorBounded(
                pickup.Body.linearVelocity,
                0.0001f,
                context + " linear velocity");
            AssertVectorBounded(
                pickup.Body.angularVelocity,
                0.0001f,
                context + " angular velocity");
            Assert.That(streaming.Deferred.Count, Is.Zero,
                context + " deferred state");
            Assert.That(
                streaming.Participant.TryResolve(
                    RollbackEntityId,
                    out PhysicsPickupTarget resolved),
                Is.True,
                context + " participant resolution");
            Assert.That(resolved, Is.SameAs(pickup.Pickup));
            Assert.That(CountLoadedStableId(RollbackEntityId), Is.EqualTo(1),
                context + " stable-ID count");
        }

        private static SaveDomainEnvelope CreateWorldEntityEnvelope(
            string payloadJson)
        {
            return new SaveDomainEnvelope
            {
                DomainId = WorldEntitySaveParticipant.DomainId,
                SchemaVersion = WorldEntityDomainSaveDto.CurrentSchemaVersion,
                Required = true,
                PayloadJson = payloadJson,
            };
        }

        private static void AssertVectorBounded(
            Vector3 value,
            float maximumMagnitude,
            string context)
        {
            Assert.That(
                IsFinite(value),
                Is.True,
                context + " contains a non-finite component");
            Assert.That(
                value.magnitude,
                Is.LessThanOrEqualTo(maximumMagnitude),
                context + " exceeded the bounded magnitude");
        }

        private static int CountLoadedStableId(string stableEntityId) =>
            Object.FindObjectsByType<StableEntityIdAuthoring>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Count(candidate =>
                    candidate != null &&
                    string.Equals(
                        candidate.SerializedId,
                        stableEntityId,
                        StringComparison.Ordinal));

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.x) &&
            float.IsFinite(value.y) &&
            float.IsFinite(value.z) &&
            float.IsFinite(value.w);

        private enum ContactFixtureKind
        {
            Shelf,
            Wall,
        }

        private sealed class PickupFixture
        {
            public PickupFixture(
                GameObject root,
                Rigidbody body,
                BoxCollider collider,
                PhysicsPickupTarget pickup,
                CollisionImpulseProbe impulseProbe)
            {
                Root = root;
                Body = body;
                Collider = collider;
                Pickup = pickup;
                ImpulseProbe = impulseProbe;
            }

            public GameObject Root { get; }
            public Rigidbody Body { get; }
            public BoxCollider Collider { get; }
            public PhysicsPickupTarget Pickup { get; }
            public CollisionImpulseProbe ImpulseProbe { get; }
        }

        private sealed class StreamingFixture
        {
            public StreamingFixture(
                ProductionWorldStreamingService service,
                WorldEntitySaveParticipant participant,
                DeferredStableEntityStore deferred)
            {
                Service = service;
                Participant = participant;
                Deferred = deferred;
            }

            public ProductionWorldStreamingService Service { get; }
            public WorldEntitySaveParticipant Participant { get; }
            public DeferredStableEntityStore Deferred { get; }
        }
    }

    internal sealed class CollisionImpulseProbe : MonoBehaviour
    {
        public float MaximumImpulseMagnitude { get; private set; }

        private void OnCollisionEnter(Collision collision)
        {
            MaximumImpulseMagnitude = Mathf.Max(
                MaximumImpulseMagnitude,
                collision.impulse.magnitude);
        }

        private void OnCollisionStay(Collision collision)
        {
            MaximumImpulseMagnitude = Mathf.Max(
                MaximumImpulseMagnitude,
                collision.impulse.magnitude);
        }
    }
}
