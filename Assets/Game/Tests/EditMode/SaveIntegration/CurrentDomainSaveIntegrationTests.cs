using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MSC.Core.Identity;
using MSC.Core.Time;
using MSC.Interaction.Capabilities;
using MSC.Interaction.Carrying;
using MSC.Vehicle;
using MSC.Vehicle.Assembly;
using MSC.Vehicle.Simulation;
using MSC.World.Streaming;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Save.Integration.Tests.EditMode
{
    public sealed class CurrentDomainSaveIntegrationTests
    {
        private const string EntityId = "9a7a5b9ed31f4f2c8da73b05b2a91041";
        private const string SecondaryEntityId =
            "3c3c4b0a19684bb09de87ef7d409e40f";
        private GameObject item;
        private Rigidbody body;
        private PhysicsPickupTarget pickup;
        private WorldEntitySaveParticipant participant;

        [SetUp]
        public void SetUp()
        {
            NativeSaveLoadHandoff.Clear(string.Empty);
            item = new GameObject("StablePickupSaveFixture");
            body = item.AddComponent<Rigidbody>();
            StableEntityIdAuthoring identity =
                item.AddComponent<StableEntityIdAuthoring>();
            typeof(StableEntityIdAuthoring)
                .GetField("stableId", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(identity, EntityId);
            pickup = item.AddComponent<PhysicsPickupTarget>();
            pickup.Configure(body, identity, "Pickup", 35f);
            participant = new WorldEntitySaveParticipant(
                new DeferredStableEntityStore());
            participant.RegisterScene(item.scene);
        }

        [TearDown]
        public void TearDown()
        {
            if (item != null)
            {
                UnityEngine.Object.DestroyImmediate(item);
            }

            NativeSaveLoadHandoff.Clear(string.Empty);
        }

        [Test]
        public void LoadedWorldEntity_RoundTripsThroughParticipantPayload()
        {
            body.position = new Vector3(12f, 3f, -7f);
            body.rotation = Quaternion.Euler(0f, 42f, 0f);
            string payload = participant.CapturePayload();
            body.position = Vector3.zero;
            body.rotation = Quaternion.identity;

            object prepared = participant.PrepareRestore(
                Envelope(payload),
                new SaveRestorePreparationContext(
                    new UnresolvedContentReport(),
                    new DeferredStableEntityStore()));
            participant.ApplyPreparedRestore(
                prepared,
                new SaveRestoreContext(
                    new UnresolvedContentReport(),
                    new DeferredStableEntityStore()));

            Assert.That(body.position, Is.EqualTo(new Vector3(12f, 3f, -7f)));
            Assert.That(
                Quaternion.Angle(body.rotation, Quaternion.Euler(0f, 42f, 0f)),
                Is.LessThan(0.01f));
        }

        [Test]
        public void PresentationHeldEntity_SaveRestoreThenDropUsesWorldPhysics()
        {
            var player = new GameObject("CarrySavePlayer");
            BoxCollider playerCollider = player.AddComponent<BoxCollider>();
            var anchorObject = new GameObject("CarryAnchor");
            anchorObject.transform.SetParent(player.transform, false);
            anchorObject.transform.localPosition = Vector3.forward;
            PhysicalCarryController carry =
                player.AddComponent<PhysicalCarryController>();
            carry.Configure(anchorObject.transform, playerCollider);
            var gripObject = new GameObject("AnimatedBottleGrip");
            gripObject.transform.SetPositionAndRotation(
                new Vector3(4f, 2f, -3f),
                Quaternion.Euler(20f, 80f, -5f));
            var interactionContext = new MSC.Interaction.InteractionContext(
                player,
                player.transform.position,
                player.transform.forward);

            try
            {
                participant = new WorldEntitySaveParticipant(
                    new DeferredStableEntityStore(),
                    worldStreaming: null,
                    persistentScene: item.scene,
                    carryController: carry);
                participant.RegisterScene(item.scene);
                Assert.That(carry.TryPickup(pickup, interactionContext), Is.True);
                carry.SetHeldPresentationAnchor(gripObject.transform, 1f);
                Assert.That(body.isKinematic, Is.True);
                Assert.That(body.detectCollisions, Is.False);

                CarriedObjectSaveState carryState = carry.CaptureSaveState();
                string payload = participant.CapturePayload();
                WorldEntityDomainSaveDto captured =
                    JsonUtility.FromJson<WorldEntityDomainSaveDto>(payload);

                Assert.That(captured.entities, Has.Length.EqualTo(1));
                Assert.That(captured.entities[0].isKinematic, Is.False);
                Assert.That(captured.entities[0].useGravity, Is.True);
                Assert.That(
                    captured.entities[0].linearVelocity,
                    Is.EqualTo(Vector3.zero));
                Assert.That(
                    captured.entities[0].angularVelocity,
                    Is.EqualTo(Vector3.zero));

                Assert.That(carry.Drop(), Is.True);
                body.isKinematic = true;
                body.useGravity = false;
                object prepared = participant.PrepareRestore(
                    Envelope(payload),
                    new SaveRestorePreparationContext(
                        new UnresolvedContentReport(),
                        new DeferredStableEntityStore()));
                participant.ApplyPreparedRestore(
                    prepared,
                    new SaveRestoreContext(
                        new UnresolvedContentReport(),
                        new DeferredStableEntityStore()));

                Assert.That(body.isKinematic, Is.False);
                Assert.That(body.useGravity, Is.True);
                Assert.That(
                    carry.TryRestoreSaveState(
                        carryState,
                        pickup,
                        interactionContext,
                        out string failure),
                    Is.True,
                    failure);
                Assert.That(carry.HasHeldObject, Is.True);
                Assert.That(carry.HasActiveContinuousHeldActivation, Is.False);

                Assert.That(carry.Drop(), Is.True);
                Assert.That(body.isKinematic, Is.False);
                Assert.That(body.detectCollisions, Is.True);
                Assert.That(body.useGravity, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gripObject);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void MissingWorldEntity_IsDeferredByStableIdWithoutFallbackLookup()
        {
            body.position = new Vector3(2f, 4f, 6f);
            string payload = participant.CapturePayload();
            UnityEngine.Object.DestroyImmediate(item);
            item = null;
            var deferred = new DeferredStableEntityStore();
            var unresolved = new UnresolvedContentReport();

            object prepared = participant.PrepareRestore(
                Envelope(payload),
                new SaveRestorePreparationContext(unresolved, deferred));
            participant.ApplyPreparedRestore(
                prepared,
                new SaveRestoreContext(unresolved, deferred));

            Assert.That(
                deferred.TryPeek(
                    WorldEntitySaveParticipant.DomainId,
                    EntityId,
                    out DeferredStableEntityPayload retained),
                Is.True);
            Assert.That(retained.StableEntityId, Is.EqualTo(EntityId));
            Assert.That(unresolved.Entries, Has.Count.EqualTo(1));
            Assert.That(
                unresolved.Entries[0].Reason,
                Is.EqualTo(UnresolvedContentReason.DeferredUntilCellLoad));
        }

        [Test]
        public void CarriedWorldEntity_IsTransferredBeforeSourceSceneUnload()
        {
            Scene persistentScene = item.scene;
            Scene sourceScene = EditorSceneManager.NewPreviewScene();
            try
            {
                SceneManager.MoveGameObjectToScene(item, sourceScene);
                var deferred = new DeferredStableEntityStore();
                participant = new WorldEntitySaveParticipant(
                    deferred,
                    worldStreaming: null,
                    persistentScene: persistentScene);
                participant.RegisterScene(sourceScene);
                pickup.NotifyPickedUp(default);

                participant.CaptureAndUnregisterScene(sourceScene);

                Assert.That(item.scene.handle, Is.EqualTo(persistentScene.handle));
                Assert.That(
                    participant.TryResolve(EntityId, out PhysicsPickupTarget resolved),
                    Is.True);
                Assert.That(resolved, Is.SameAs(pickup));
                Assert.That(
                    deferred.TryPeek(
                        WorldEntitySaveParticipant.DomainId,
                        EntityId,
                        out _),
                    Is.False);
            }
            finally
            {
                if (sourceScene.IsValid() && sourceScene.isLoaded)
                {
                    EditorSceneManager.ClosePreviewScene(sourceScene);
                }
            }
        }

        [Test]
        public void ReloadedSourceCell_DiscardsCanonicalCloneOfRehomedEntity()
        {
            Scene persistentScene = item.scene;
            Scene sourceScene = EditorSceneManager.NewPreviewScene();
            ProductionWorldStreamingManifest manifest =
                ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
            var streamingObject = new GameObject("SaveStreamingCloneFixture");
            GameObject canonicalClone = null;
            GameObject canonicalCloneRoot = null;
            try
            {
                manifest.ConfigureForAuthoring(
                    512f,
                    0,
                    1,
                    new[]
                    {
                        new ProductionWorldCellScene(
                            "cell_0_0",
                            0,
                            0,
                            6,
                            sourceScene.path),
                    });
                ProductionWorldStreamingService streaming =
                    streamingObject.AddComponent<ProductionWorldStreamingService>();
                streaming.ConfigureForAuthoring(manifest);
                SceneManager.MoveGameObjectToScene(item, sourceScene);
                participant = new WorldEntitySaveParticipant(
                    new DeferredStableEntityStore(),
                    streaming,
                    persistentScene);
                participant.RegisterScene(sourceScene);
                pickup.NotifyPickedUp(default);
                participant.CaptureAndUnregisterScene(sourceScene);

                canonicalClone = CreatePickupObject(
                    "ReloadedCanonicalClone",
                    EntityId,
                    out _,
                    out PhysicsPickupTarget canonicalPickup);
                canonicalCloneRoot = new GameObject("ReloadedCanonicalEntityRoot");
                Rigidbody canonicalRootBody =
                    canonicalCloneRoot.AddComponent<Rigidbody>();
                canonicalClone.transform.SetParent(
                    canonicalCloneRoot.transform,
                    worldPositionStays: false);
                canonicalPickup.Configure(
                    canonicalRootBody,
                    canonicalClone.GetComponent<StableEntityIdAuthoring>(),
                    "Pickup",
                    35f);
                SceneManager.MoveGameObjectToScene(
                    canonicalCloneRoot,
                    sourceScene);

                Assert.DoesNotThrow(() => participant.RegisterScene(sourceScene));
                Assert.That(canonicalClone == null, Is.True);
                Assert.That(canonicalCloneRoot == null, Is.True);
                Assert.That(
                    participant.TryResolve(EntityId, out PhysicsPickupTarget resolved),
                    Is.True);
                Assert.That(resolved, Is.SameAs(pickup));
                Assert.That(item.scene.handle, Is.EqualTo(persistentScene.handle));
            }
            finally
            {
                if (canonicalClone != null)
                {
                    UnityEngine.Object.DestroyImmediate(canonicalClone);
                }

                if (canonicalCloneRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(canonicalCloneRoot);
                }

                UnityEngine.Object.DestroyImmediate(streamingObject);
                UnityEngine.Object.DestroyImmediate(manifest);
                if (sourceScene.IsValid() && sourceScene.isLoaded)
                {
                    EditorSceneManager.ClosePreviewScene(sourceScene);
                }
            }
        }

        [Test]
        public void UnsafeLiveHierarchy_VetoesUnloadWithoutPartialRegistryMutation()
        {
            Scene persistentScene = item.scene;
            Scene sourceScene = EditorSceneManager.NewPreviewScene();
            ProductionWorldStreamingManifest manifest =
                ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
            var streamingObject = new GameObject("SaveStreamingVetoFixture");
            GameObject blocker = null;
            GameObject externalBodyObject = null;
            try
            {
                manifest.ConfigureForAuthoring(
                    512f,
                    0,
                    1,
                    new[]
                    {
                        new ProductionWorldCellScene(
                            "cell_0_0",
                            0,
                            0,
                            6,
                            sourceScene.path),
                    });
                ProductionWorldStreamingService streaming =
                    streamingObject.AddComponent<ProductionWorldStreamingService>();
                streaming.ConfigureForAuthoring(manifest);
                var deferred = new DeferredStableEntityStore();

                SceneManager.MoveGameObjectToScene(item, sourceScene);
                blocker = CreatePickupObject(
                    "UnsafeHierarchyPickup",
                    SecondaryEntityId,
                    out _,
                    out PhysicsPickupTarget blockerPickup);
                externalBodyObject = new GameObject("ExternalPickupBody");
                Rigidbody externalBody =
                    externalBodyObject.AddComponent<Rigidbody>();
                blockerPickup.Configure(
                    externalBody,
                    blocker.GetComponent<StableEntityIdAuthoring>(),
                    "Pickup",
                    35f);
                SceneManager.MoveGameObjectToScene(blocker, sourceScene);
                SceneManager.MoveGameObjectToScene(externalBodyObject, sourceScene);

                participant = new WorldEntitySaveParticipant(
                    deferred,
                    streaming,
                    persistentScene);
                participant.RegisterScene(sourceScene);
                blockerPickup.NotifyPickedUp(default);

                participant.CaptureAndUnregisterScene(sourceScene);

                Assert.That(streaming.IsCellRetained("cell_0_0"), Is.True);
                Assert.That(deferred.Count, Is.Zero);
                Assert.That(participant.TryResolve(EntityId, out _), Is.True);
                Assert.That(
                    participant.TryResolve(SecondaryEntityId, out _),
                    Is.True);

                blockerPickup.NotifyReleased(PickupReleaseReason.Dropped);
                externalBody.position = Vector3.zero;
                participant.CaptureAndUnregisterScene(sourceScene);

                Assert.That(streaming.IsCellRetained("cell_0_0"), Is.False);
                Assert.That(deferred.Count, Is.EqualTo(2));
                Assert.That(participant.TryResolve(EntityId, out _), Is.False);
                Assert.That(
                    participant.TryResolve(SecondaryEntityId, out _),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(streamingObject);
                UnityEngine.Object.DestroyImmediate(manifest);
                if (sourceScene.IsValid() && sourceScene.isLoaded)
                {
                    EditorSceneManager.ClosePreviewScene(sourceScene);
                }

                if (blocker != null)
                {
                    UnityEngine.Object.DestroyImmediate(blocker);
                }

                if (externalBodyObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(externalBodyObject);
                }
            }
        }

        [Test]
        public void WorldEntityRollback_ReleasesRetentionIntroducedByRestore()
        {
            ProductionWorldStreamingManifest manifest =
                ScriptableObject.CreateInstance<ProductionWorldStreamingManifest>();
            var streamingObject = new GameObject("SaveStreamingRetentionFixture");
            try
            {
                manifest.ConfigureForAuthoring(
                    512f,
                    0,
                    1,
                    new[]
                    {
                        new ProductionWorldCellScene(
                            "cell_0_0",
                            0,
                            0,
                            6,
                            "Assets/Cell_0_0.unity"),
                    });
                ProductionWorldStreamingService streaming =
                    streamingObject.AddComponent<ProductionWorldStreamingService>();
                streaming.ConfigureForAuthoring(manifest);
                var deferred = new DeferredStableEntityStore();
                var stagedDeferred = new DeferredStableEntityStore();
                participant = new WorldEntitySaveParticipant(
                    deferred,
                    streaming,
                    item.scene);
                var saved = new WorldEntityDomainSaveDto
                {
                    entities = new[]
                    {
                        new WorldEntityStateDto
                        {
                            stableEntityId = EntityId,
                            sourceCellId = "cell_0_0",
                            worldRotation = Quaternion.identity,
                        },
                    },
                };
                object checkpoint = participant.CaptureCheckpoint();
                var unresolved = new UnresolvedContentReport();
                object prepared = participant.PrepareRestore(
                    Envelope(JsonUtility.ToJson(saved)),
                    new SaveRestorePreparationContext(
                        unresolved,
                        stagedDeferred));

                participant.ApplyPreparedRestore(
                    prepared,
                    new SaveRestoreContext(unresolved, stagedDeferred));
                Assert.That(streaming.IsCellRetained("cell_0_0"), Is.True);

                participant.Rollback(checkpoint);
                Assert.That(streaming.IsCellRetained("cell_0_0"), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(streamingObject);
                UnityEngine.Object.DestroyImmediate(manifest);
            }
        }

        [Test]
        public void LoadHandoff_IsSinglePendingSlotAndConsumesExactlyOnce()
        {
            Assert.That(
                NativeSaveLoadHandoff.TryQueue("slot-a", out string failure),
                Is.True,
                failure);
            Assert.That(
                NativeSaveLoadHandoff.TryQueue("slot-b", out _),
                Is.False);
            Assert.That(
                NativeSaveLoadHandoff.TryConsume(out string slotId),
                Is.True);
            Assert.That(slotId, Is.EqualTo("slot-a"));
            Assert.That(NativeSaveLoadHandoff.TryConsume(out _), Is.False);
        }

        [Test]
        public void EmptyVehicleDomain_IsValidButDoesNotClaimProductionVehicle()
        {
            var dto = new VehicleDomainSaveDto();

            Assert.That(dto.TryValidateBasic(out string failure), Is.True, failure);
            Assert.That(dto.vehicles, Is.Empty);
        }

        [Test]
        public void SyntheticVehicleRecovery_RoundTripsCorruptedChassisAndLooseEngineBlockWithoutMutatingInstalledEngine()
        {
            using SyntheticVehicleFixture fixture =
                SyntheticVehicleFixture.Create();
            var deferred = new DeferredStableEntityStore();
            var formation = new ImportantObjectRecoveryFormation(
                fixture.RecoveryOrigin);
            var vehicleParticipant = new VehicleSaveParticipant(
                deferred,
                worldStreaming: null,
                recoveryFormation: formation,
                recoveryMinimumY: -64f);
            vehicleParticipant.RegisterHierarchy(fixture.Root);

            VehicleDomainSaveDto contaminated =
                SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(
                    vehicleParticipant.CapturePayload());
            Assert.That(contaminated.vehicles, Has.Length.EqualTo(1));
            VehicleSaveRecordDto contaminatedVehicle = contaminated.vehicles[0];
            PartSaveDto installedBefore = contaminatedVehicle.assembly.parts.Single(
                candidate => candidate.stableEntityId ==
                    SyntheticVehicleFixture.InstalledEngineStableId);
            Assert.That(
                installedBefore.lifecycleState,
                Is.EqualTo(PartLifecycleState.Installed));
            Assert.That(
                installedBefore.installedMountId,
                Is.EqualTo(SyntheticVehicleFixture.EngineMountId));
            installedBefore.worldPosition = new Vector3(
                installedBefore.worldPosition.x,
                -512f,
                installedBefore.worldPosition.z);

            contaminatedVehicle.physics.worldPosition = new Vector3(
                contaminatedVehicle.physics.worldPosition.x,
                -128f,
                contaminatedVehicle.physics.worldPosition.z);
            contaminatedVehicle.physics.linearVelocity =
                new Vector3(18f, -24f, 7f);
            contaminatedVehicle.physics.angularVelocity =
                new Vector3(3f, 5f, -2f);
            contaminatedVehicle.physics.sleeping = false;

            PartSaveDto[] looseBefore = contaminatedVehicle.assembly.parts
                .Where(candidate => candidate.stableEntityId ==
                    SyntheticVehicleFixture.LooseEngineAStableId ||
                    candidate.stableEntityId ==
                    SyntheticVehicleFixture.LooseEngineBStableId)
                .OrderBy(candidate => candidate.stableEntityId, StringComparer.Ordinal)
                .ToArray();
            Assert.That(looseBefore, Has.Length.EqualTo(2));
            for (int index = 0; index < looseBefore.Length; index++)
            {
                looseBefore[index].worldPosition = new Vector3(
                    looseBefore[index].worldPosition.x,
                    -96f - index,
                    looseBefore[index].worldPosition.z);
            }

            fixture.LooseEngineABody.linearVelocity = new Vector3(8f, -12f, 4f);
            fixture.LooseEngineABody.angularVelocity = new Vector3(2f, 3f, 5f);
            fixture.LooseEngineBBody.linearVelocity = new Vector3(-6f, -9f, 7f);
            fixture.LooseEngineBBody.angularVelocity = new Vector3(4f, -2f, 3f);
            string contaminatedPayload = SaveParticipantJson.Serialize(contaminated);

            RestoreVehiclePayload(
                vehicleParticipant,
                deferred,
                contaminatedPayload);
            Physics.SyncTransforms();

            AssertRecoveredChassis(fixture);
            AssertInstalledEngineUnchanged(fixture, installedBefore);
            AssertRecoveredLooseEngine(
                fixture,
                fixture.LooseEngineA,
                fixture.LooseEngineABody,
                fixture.LooseEngineACollider,
                SyntheticVehicleFixture.LooseEngineAStableId,
                SyntheticVehicleFixture.LooseEngineADefinitionId);
            AssertRecoveredLooseEngine(
                fixture,
                fixture.LooseEngineB,
                fixture.LooseEngineBBody,
                fixture.LooseEngineBCollider,
                SyntheticVehicleFixture.LooseEngineBStableId,
                SyntheticVehicleFixture.LooseEngineBDefinitionId);
            Assert.That(
                formation.ReservationCount,
                Is.EqualTo(3),
                "Only the chassis and two Loose parts should consume recovery slots; " +
                "the Installed engine's negative saved worldPosition is mount-owned.");
            Assert.That(
                fixture.ChassisCollider.bounds.Intersects(
                    fixture.LooseEngineACollider.bounds),
                Is.False);
            Assert.That(
                fixture.ChassisCollider.bounds.Intersects(
                    fixture.LooseEngineBCollider.bounds),
                Is.False);
            Assert.That(
                fixture.LooseEngineACollider.bounds.Intersects(
                    fixture.LooseEngineBCollider.bounds),
                Is.False,
                "Recovered loose engine parts must occupy distinct formation slots.");

            string healedPayload = vehicleParticipant.CapturePayload();
            VehicleDomainSaveDto healed =
                SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(healedPayload);
            AssertVehiclePartIdentityIsUnique(healed);
            AssertCapturedChassisHealed(healed);
            AssertCapturedInstalledEngineUnchanged(healed, installedBefore);
            AssertCapturedLooseEngineHealed(
                healed,
                SyntheticVehicleFixture.LooseEngineAStableId,
                SyntheticVehicleFixture.LooseEngineADefinitionId);
            AssertCapturedLooseEngineHealed(
                healed,
                SyntheticVehicleFixture.LooseEngineBStableId,
                SyntheticVehicleFixture.LooseEngineBDefinitionId);

            fixture.LooseEngineABody.position = new Vector3(40f, 6f, 40f);
            fixture.LooseEngineABody.linearVelocity = new Vector3(3f, 2f, 1f);
            fixture.LooseEngineBBody.position = new Vector3(42f, 6f, 40f);
            fixture.LooseEngineBBody.linearVelocity = new Vector3(-1f, 2f, 3f);
            RestoreVehiclePayload(vehicleParticipant, deferred, healedPayload);
            Physics.SyncTransforms();

            VehicleDomainSaveDto secondCycle =
                SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(
                    vehicleParticipant.CapturePayload());
            AssertVehiclePartIdentityIsUnique(secondCycle);
            AssertCapturedChassisHealed(secondCycle);
            AssertCapturedInstalledEngineUnchanged(secondCycle, installedBefore);
            AssertCapturedLooseEngineHealed(
                secondCycle,
                SyntheticVehicleFixture.LooseEngineAStableId,
                SyntheticVehicleFixture.LooseEngineADefinitionId);
            AssertCapturedLooseEngineHealed(
                secondCycle,
                SyntheticVehicleFixture.LooseEngineBStableId,
                SyntheticVehicleFixture.LooseEngineBDefinitionId);
        }

        [Test]
        public void PersistentSatsumaHierarchy_RoundTripsAssemblyFastenerAndLoosePose()
        {
            const string prefabPath =
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/" +
                "Satsuma/Resources/Phase1Vehicles/" +
                "Satsuma_Phase1_V1a.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                VehicleSimulationHost simulation =
                    instance.GetComponent<VehicleSimulationHost>();
                Assert.That(simulation, Is.Not.Null);
                Assert.That(
                    simulation.TryInitialize(out string simulationFailure),
                    Is.True,
                    simulationFailure);
                VehicleAssemblyController assembly =
                    instance.GetComponent<VehicleAssemblyController>();
                PartInstance trailArm = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.trail-arm-rl");
                MountPointAuthoring trailArmMount = assembly.MountPoints.Single(value =>
                    value.MountId == "mount.satsuma.trail-arm-rl");
                trailArm.transform.SetPositionAndRotation(
                    trailArmMount.Pose.position,
                    trailArmMount.Pose.rotation);
                Assert.That(
                    assembly.TryInstall(trailArm, trailArmMount).Succeeded,
                    Is.True);

                Assert.That(
                    assembly.Graph.TryGetMount(
                        trailArmMount.MountId,
                        out MountPointRuntime runtimeMount),
                    Is.True);
                FastenerInstance fastener = runtimeMount.Fasteners[0];
                ToolDefinition wrench = assembly.Tools.Single(value =>
                    value.Size == fastener.Definition.Size);
                FastenerRotationDirection tightenDirection =
                    fastener.Definition.TighteningDirection ==
                    FastenerDirection.ClockwiseToTighten
                        ? FastenerRotationDirection.Clockwise
                        : FastenerRotationDirection.CounterClockwise;
                FastenerRotationDirection loosenDirection =
                    tightenDirection == FastenerRotationDirection.Clockwise
                        ? FastenerRotationDirection.CounterClockwise
                        : FastenerRotationDirection.Clockwise;
                Assert.That(
                    assembly.TryTurnFastener(
                        trailArmMount.MountId,
                        fastener.Definition.DefinitionId,
                        wrench,
                        tightenDirection).Succeeded,
                    Is.True);

                PartInstance looseWheel = assembly.Parts.Single(value =>
                    value.Definition.DefinitionId ==
                    "vehicle.satsuma.part.wheel-gt-fl");
                Vector3 savedLoosePosition = new Vector3(8.25f, 1.75f, -4.5f);
                Quaternion savedLooseRotation = Quaternion.Euler(13f, 71f, -22f);
                looseWheel.transform.SetPositionAndRotation(
                    savedLoosePosition,
                    savedLooseRotation);

                var deferred = new DeferredStableEntityStore();
                var vehicleParticipant = new VehicleSaveParticipant(deferred);
                vehicleParticipant.RegisterHierarchy(instance.transform.root.gameObject);
                string payload = vehicleParticipant.CapturePayload();
                VehicleDomainSaveDto captured =
                    SaveParticipantJson.Deserialize<VehicleDomainSaveDto>(payload);
                Assert.That(captured.vehicles, Has.Length.EqualTo(1));
                Assert.That(captured.vehicles[0].assembly.parts, Has.Length.EqualTo(126));
                Assert.That(captured.vehicles[0].assembly.schemaVersion, Is.EqualTo(2));
                Assert.That(captured.vehicles[0].assembly.fasteners, Has.Length.EqualTo(280));
                Assert.That(
                    captured.vehicles[0].assembly.fastenerGroups,
                    Has.Length.EqualTo(117));

                Assert.That(
                    assembly.TryTurnFastener(
                        trailArmMount.MountId,
                        fastener.Definition.DefinitionId,
                        wrench,
                        loosenDirection).Succeeded,
                    Is.True);
                Assert.That(assembly.TryRemove(trailArm).Succeeded, Is.True);
                looseWheel.transform.SetPositionAndRotation(
                    Vector3.zero,
                    Quaternion.identity);

                object prepared = vehicleParticipant.PrepareRestore(
                    new SaveDomainEnvelope
                    {
                        DomainId = VehicleSaveParticipant.DomainId,
                        SchemaVersion = VehicleDomainSaveDto.CurrentSchemaVersion,
                        Required = true,
                        PayloadJson = payload,
                    },
                    new SaveRestorePreparationContext(
                        new UnresolvedContentReport(),
                        deferred));
                vehicleParticipant.ApplyPreparedRestore(
                    prepared,
                    new SaveRestoreContext(
                        new UnresolvedContentReport(),
                        deferred));

                Assert.That(trailArm.IsInstalled, Is.True);
                Assert.That(runtimeMount.Fasteners[0].Stage, Is.EqualTo(1));
                Assert.That(
                    Vector3.Distance(
                        looseWheel.transform.position,
                        savedLoosePosition),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(
                        looseWheel.transform.rotation,
                        savedLooseRotation),
                    Is.LessThan(0.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PersistentSatsumaReload_RecreatesAggregateAndRestoresAssemblyFastenerAndLoosePose()
        {
            const string prefabPath =
                "Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/" +
                "Satsuma/Resources/Phase1Vehicles/" +
                "Satsuma_Phase1_V1a.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Assert.Ignore(
                    "Private donor-derived Satsuma baseline has not been built on this machine.");
            }

            const string mountId = "mount.satsuma.sub-frame";
            const string partId = "vehicle.satsuma.part.sub-frame";
            const string loosePartId = "vehicle.satsuma.part.wheel-gt-fr";
            Vector3 savedLoosePosition = new Vector3(-6.5f, 0.85f, 3.25f);
            Quaternion savedLooseRotation = Quaternion.Euler(-17f, 123f, 31f);
            var deferred = new DeferredStableEntityStore();
            var vehicleParticipant = new VehicleSaveParticipant(deferred);
            GameObject original = null;
            GameObject restored = null;
            try
            {
                original = UnityEngine.Object.Instantiate(prefab);
                VehicleSimulationHost originalSimulation =
                    original.GetComponent<VehicleSimulationHost>();
                Assert.That(
                    originalSimulation.TryInitialize(out string simulationFailure),
                    Is.True,
                    simulationFailure);
                VehicleAssemblyController originalAssembly =
                    original.GetComponent<VehicleAssemblyController>();
                PartInstance installedPart = originalAssembly.Parts.Single(value =>
                    value.Definition.DefinitionId == partId);
                MountPointAuthoring mount = originalAssembly.MountPoints.Single(value =>
                    value.MountId == mountId);
                installedPart.transform.SetPositionAndRotation(
                    mount.Pose.position,
                    mount.Pose.rotation);
                Assert.That(
                    originalAssembly.TryInstall(installedPart, mount).Succeeded,
                    Is.True);
                Assert.That(
                    originalAssembly.Graph.TryGetMount(
                        mountId,
                        out MountPointRuntime originalRuntimeMount),
                    Is.True);
                FastenerInstance fastener = originalRuntimeMount.Fasteners[0];
                ToolDefinition wrench = originalAssembly.Tools.Single(value =>
                    value.Size == fastener.Definition.Size);
                FastenerRotationDirection tightenDirection =
                    fastener.Definition.TighteningDirection ==
                    FastenerDirection.ClockwiseToTighten
                        ? FastenerRotationDirection.Clockwise
                        : FastenerRotationDirection.CounterClockwise;
                for (int stage = 0; stage < 3; stage++)
                {
                    Assert.That(
                        originalAssembly.TryTurnFastener(
                            mountId,
                            fastener.Definition.DefinitionId,
                            wrench,
                            tightenDirection).Succeeded,
                        Is.True);
                }

                PartInstance loosePart = originalAssembly.Parts.Single(value =>
                    value.Definition.DefinitionId == loosePartId);
                loosePart.transform.SetPositionAndRotation(
                    savedLoosePosition,
                    savedLooseRotation);
                vehicleParticipant.RegisterHierarchy(original.transform.root.gameObject);
                string payload = vehicleParticipant.CapturePayload();

                UnityEngine.Object.DestroyImmediate(original);
                original = null;
                restored = UnityEngine.Object.Instantiate(prefab);
                VehicleSimulationHost restoredSimulation =
                    restored.GetComponent<VehicleSimulationHost>();
                Assert.That(
                    restoredSimulation.TryInitialize(out simulationFailure),
                    Is.True,
                    simulationFailure);
                vehicleParticipant.RegisterHierarchy(restored.transform.root.gameObject);
                object prepared = vehicleParticipant.PrepareRestore(
                    new SaveDomainEnvelope
                    {
                        DomainId = VehicleSaveParticipant.DomainId,
                        SchemaVersion = VehicleDomainSaveDto.CurrentSchemaVersion,
                        Required = true,
                        PayloadJson = payload,
                    },
                    new SaveRestorePreparationContext(
                        new UnresolvedContentReport(),
                        deferred));
                vehicleParticipant.ApplyPreparedRestore(
                    prepared,
                    new SaveRestoreContext(
                        new UnresolvedContentReport(),
                        deferred));

                VehicleAssemblyController restoredAssembly =
                    restored.GetComponent<VehicleAssemblyController>();
                PartInstance restoredInstalledPart = restoredAssembly.Parts.Single(value =>
                    value.Definition.DefinitionId == partId);
                Assert.That(restoredInstalledPart.IsInstalled, Is.True);
                Assert.That(
                    restoredAssembly.Graph.TryGetMount(
                        mountId,
                        out MountPointRuntime restoredRuntimeMount),
                    Is.True);
                FastenerInstance restoredFastener =
                    restoredRuntimeMount.Fasteners.Single(value =>
                        value.Definition.DefinitionId ==
                        fastener.Definition.DefinitionId);
                Assert.That(restoredFastener.Stage, Is.EqualTo(3));

                PartInstance restoredLoosePart = restoredAssembly.Parts.Single(value =>
                    value.Definition.DefinitionId == loosePartId);
                Assert.That(
                    Vector3.Distance(
                        restoredLoosePart.transform.position,
                        savedLoosePosition),
                    Is.LessThan(0.001f));
                Assert.That(
                    Quaternion.Angle(
                        restoredLoosePart.transform.rotation,
                        savedLooseRotation),
                    Is.LessThan(0.01f));
            }
            finally
            {
                if (original != null)
                {
                    UnityEngine.Object.DestroyImmediate(original);
                }

                if (restored != null)
                {
                    UnityEngine.Object.DestroyImmediate(restored);
                }
            }
        }

        [Test]
        public void CurrentParticipantDomainIds_MatchCoverageContracts()
        {
            Assert.That(CoreTimeSaveParticipant.DomainId, Is.EqualTo("core.time"));
            Assert.That(
                WeatherEnvironmentSaveParticipant.DomainId,
                Is.EqualTo("weather.environment"));
            Assert.That(
                WorldEntitySaveParticipant.DomainId,
                Is.EqualTo("world.entities"));
            Assert.That(
                VehicleSaveParticipant.DomainId,
                Is.EqualTo("vehicle.satsuma"));
            Assert.That(PlayerSaveParticipant.DomainId, Is.EqualTo("player.state"));
            Assert.That(
                CarrySaveParticipant.DomainId,
                Is.EqualTo("interaction.carry"));
        }

        [Test]
        public void CoreTimeRestore_NormalizesPauseCapturedByOlderSlots()
        {
            var saved = new GameTimeSaveDto
            {
                isPaused = true,
            };

            CoreTimeSaveParticipant.NormalizeSessionState(saved);

            Assert.That(saved.isPaused, Is.False);
        }

        private static void RestoreVehiclePayload(
            VehicleSaveParticipant participant,
            DeferredStableEntityStore deferred,
            string payload)
        {
            object prepared = participant.PrepareRestore(
                new SaveDomainEnvelope
                {
                    DomainId = VehicleSaveParticipant.DomainId,
                    SchemaVersion = VehicleDomainSaveDto.CurrentSchemaVersion,
                    Required = true,
                    PayloadJson = payload,
                },
                new SaveRestorePreparationContext(
                    new UnresolvedContentReport(),
                    deferred));
            participant.ApplyPreparedRestore(
                prepared,
                new SaveRestoreContext(
                    new UnresolvedContentReport(),
                    deferred));
        }

        private static void AssertInstalledEngineUnchanged(
            SyntheticVehicleFixture fixture,
            PartSaveDto expected)
        {
            Assert.That(fixture.InstalledEngine.IsInstalled, Is.True);
            Assert.That(
                fixture.InstalledEngine.StableId.Value,
                Is.EqualTo(expected.stableEntityId));
            Assert.That(
                fixture.InstalledEngine.Definition.DefinitionId,
                Is.EqualTo(expected.partDefinitionId));
            Assert.That(
                fixture.InstalledEngine.RuntimeState.LifecycleState,
                Is.EqualTo(expected.lifecycleState));
            Assert.That(
                fixture.InstalledEngine.RuntimeState.InstalledMountId,
                Is.EqualTo(expected.installedMountId));
            Assert.That(
                Vector3.Distance(
                    fixture.InstalledEngine.transform.position,
                    fixture.EngineMount.Pose.position),
                Is.LessThan(0.001f),
                "Installed engine pose must remain authoritative at its mount even " +
                "when the saved informational worldPosition is below terrain.");
        }

        private static void AssertRecoveredChassis(
            SyntheticVehicleFixture fixture)
        {
            Assert.That(
                fixture.Binding.StableVehicleId,
                Is.EqualTo(SyntheticVehicleFixture.VehicleStableId));
            Assert.That(
                fixture.Chassis.position.y,
                Is.GreaterThanOrEqualTo(-64f));
            Assert.That(
                fixture.Chassis.linearVelocity.sqrMagnitude,
                Is.LessThan(0.000001f));
            Assert.That(
                fixture.Chassis.angularVelocity.sqrMagnitude,
                Is.LessThan(0.000001f));
            AssertSupported(
                fixture,
                fixture.ChassisCollider,
                "Recovered chassis has no project-owned ground support.");
        }

        private static void AssertRecoveredLooseEngine(
            SyntheticVehicleFixture fixture,
            PartInstance part,
            Rigidbody recoveredBody,
            BoxCollider recoveredCollider,
            string expectedStableId,
            string expectedDefinitionId)
        {
            Assert.That(part.StableId.Value, Is.EqualTo(expectedStableId));
            Assert.That(
                part.Definition.DefinitionId,
                Is.EqualTo(expectedDefinitionId));
            Assert.That(
                part.RuntimeState.LifecycleState,
                Is.EqualTo(PartLifecycleState.Loose));
            Assert.That(part.RuntimeState.InstalledMountId, Is.Empty);
            Assert.That(recoveredBody.position.y, Is.GreaterThanOrEqualTo(-64f));
            Assert.That(recoveredBody.linearVelocity.sqrMagnitude, Is.LessThan(0.000001f));
            Assert.That(recoveredBody.angularVelocity.sqrMagnitude, Is.LessThan(0.000001f));

            AssertSupported(
                fixture,
                recoveredCollider,
                "Recovered loose engine part has no project-owned ground support.");
        }

        private static void AssertSupported(
            SyntheticVehicleFixture fixture,
            BoxCollider recoveredCollider,
            string failureMessage)
        {
            int supportMask = 1 << fixture.Ground.layer;
            Vector3 probe = new Vector3(
                recoveredCollider.bounds.center.x,
                recoveredCollider.bounds.max.y + 1f,
                recoveredCollider.bounds.center.z);
            Assert.That(
                Physics.Raycast(
                    probe,
                    Vector3.down,
                    out RaycastHit hit,
                    8f,
                    supportMask,
                    QueryTriggerInteraction.Ignore),
                Is.True,
                failureMessage);
            Assert.That(hit.collider, Is.SameAs(fixture.GroundCollider));
            float bottomClearance = recoveredCollider.bounds.min.y -
                fixture.GroundCollider.bounds.max.y;
            Assert.That(bottomClearance, Is.InRange(-0.001f, 0.1f));
        }

        private static void AssertVehiclePartIdentityIsUnique(
            VehicleDomainSaveDto state)
        {
            Assert.That(state.vehicles, Has.Length.EqualTo(1));
            string[] stableIds = state.vehicles[0].assembly.parts
                .Select(candidate => candidate.stableEntityId)
                .ToArray();
            Assert.That(stableIds, Has.Length.EqualTo(4));
            Assert.That(
                stableIds.Distinct(StringComparer.Ordinal).Count(),
                Is.EqualTo(stableIds.Length),
                "Vehicle capture/JSON restore introduced duplicate part stable IDs.");
            Assert.That(
                state.vehicles[0].stableVehicleId,
                Is.EqualTo(SyntheticVehicleFixture.VehicleStableId));
        }

        private static void AssertCapturedChassisHealed(
            VehicleDomainSaveDto state)
        {
            VehiclePhysicsSaveDto physics = state.vehicles[0].physics;
            Assert.That(physics.worldPosition.y, Is.GreaterThanOrEqualTo(-64f));
            Assert.That(physics.linearVelocity.sqrMagnitude, Is.LessThan(0.000001f));
            Assert.That(physics.angularVelocity.sqrMagnitude, Is.LessThan(0.000001f));
        }

        private static void AssertCapturedInstalledEngineUnchanged(
            VehicleDomainSaveDto state,
            PartSaveDto expected)
        {
            PartSaveDto actual = state.vehicles[0].assembly.parts.Single(
                candidate => candidate.stableEntityId == expected.stableEntityId);
            Assert.That(actual.partDefinitionId, Is.EqualTo(expected.partDefinitionId));
            Assert.That(actual.lifecycleState, Is.EqualTo(expected.lifecycleState));
            Assert.That(actual.installedMountId, Is.EqualTo(expected.installedMountId));
            Assert.That(
                actual.worldPosition.y,
                Is.GreaterThanOrEqualTo(-64f),
                "Installed engine capture must follow its authoritative mount, not " +
                "the deliberately corrupted informational saved worldPosition.");
        }

        private static void AssertCapturedLooseEngineHealed(
            VehicleDomainSaveDto state,
            string stableId,
            string definitionId)
        {
            PartSaveDto actual = state.vehicles[0].assembly.parts.Single(
                candidate => candidate.stableEntityId == stableId);
            Assert.That(actual.partDefinitionId, Is.EqualTo(definitionId));
            Assert.That(actual.lifecycleState, Is.EqualTo(PartLifecycleState.Loose));
            Assert.That(actual.installedMountId, Is.Empty);
            Assert.That(actual.worldPosition.y, Is.GreaterThanOrEqualTo(-64f));
        }

        private static SaveDomainEnvelope Envelope(string payload) =>
            new SaveDomainEnvelope
            {
                DomainId = WorldEntitySaveParticipant.DomainId,
                SchemaVersion = WorldEntityDomainSaveDto.CurrentSchemaVersion,
                Required = true,
                PayloadJson = payload,
            };

        private static GameObject CreatePickupObject(
            string name,
            string stableEntityId,
            out Rigidbody createdBody,
            out PhysicsPickupTarget createdPickup)
        {
            var created = new GameObject(name);
            createdBody = created.AddComponent<Rigidbody>();
            StableEntityIdAuthoring identity =
                created.AddComponent<StableEntityIdAuthoring>();
            typeof(StableEntityIdAuthoring)
                .GetField("stableId", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(identity, stableEntityId);
            createdPickup = created.AddComponent<PhysicsPickupTarget>();
            createdPickup.Configure(
                createdBody,
                identity,
                "Pickup",
                35f);
            return created;
        }

        private sealed class SyntheticVehicleFixture : IDisposable
        {
            public const string VehicleStableId =
                "11111111111111111111111111111111";
            public const string RootPartStableId =
                "22222222222222222222222222222222";
            public const string InstalledEngineStableId =
                "33333333333333333333333333333333";
            public const string LooseEngineAStableId =
                "44444444444444444444444444444444";
            public const string LooseEngineBStableId =
                "55555555555555555555555555555555";
            public const string RootPartDefinitionId =
                "test.vehicle.part.chassis-root";
            public const string InstalledEngineDefinitionId =
                "test.vehicle.part.engine-installed";
            public const string LooseEngineADefinitionId =
                "test.vehicle.part.engine-block";
            public const string LooseEngineBDefinitionId =
                "test.vehicle.part.engine-loose-b";
            public const string EngineMountId =
                "mount.test.vehicle.engine-installed";

            private const string EngineSocketType =
                "test.vehicle.socket.engine";
            private readonly List<ScriptableObject> authoredDefinitions =
                new List<ScriptableObject>();

            public GameObject Root { get; private set; }
            public GameObject LoosePartsRoot { get; private set; }
            public GameObject Ground { get; private set; }
            public BoxCollider GroundCollider { get; private set; }
            public Vector3 RecoveryOrigin { get; } = new Vector3(0f, 8f, 0f);
            public Rigidbody Chassis { get; private set; }
            public BoxCollider ChassisCollider { get; private set; }
            public VehiclePersistenceBinding Binding { get; private set; }
            public MountPointAuthoring EngineMount { get; private set; }
            public PartInstance InstalledEngine { get; private set; }
            public PartInstance LooseEngineA { get; private set; }
            public PartInstance LooseEngineB { get; private set; }
            public Rigidbody LooseEngineABody { get; private set; }
            public Rigidbody LooseEngineBBody { get; private set; }
            public BoxCollider LooseEngineACollider { get; private set; }
            public BoxCollider LooseEngineBCollider { get; private set; }

            public static SyntheticVehicleFixture Create()
            {
                var fixture = new SyntheticVehicleFixture();
                try
                {
                    fixture.Build();
                    return fixture;
                }
                catch
                {
                    fixture.Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                if (Root != null)
                {
                    UnityEngine.Object.DestroyImmediate(Root);
                    Root = null;
                }

                if (Ground != null)
                {
                    UnityEngine.Object.DestroyImmediate(Ground);
                    Ground = null;
                }

                if (LoosePartsRoot != null)
                {
                    UnityEngine.Object.DestroyImmediate(LoosePartsRoot);
                    LoosePartsRoot = null;
                }

                for (int index = 0; index < authoredDefinitions.Count; index++)
                {
                    if (authoredDefinitions[index] != null)
                    {
                        UnityEngine.Object.DestroyImmediate(
                            authoredDefinitions[index]);
                    }
                }

                authoredDefinitions.Clear();
            }

            private void Build()
            {
                int worldSurfaceLayer = LayerMask.NameToLayer("WorldSurface");
                if (worldSurfaceLayer < 0)
                {
                    throw new InvalidOperationException(
                        "Synthetic vehicle recovery fixture requires WorldSurface layer.");
                }

                Ground = new GameObject("Synthetic home-front recovery support");
                Ground.layer = worldSurfaceLayer;
                Ground.transform.position = new Vector3(0f, -0.5f, 0f);
                GroundCollider = Ground.AddComponent<BoxCollider>();
                GroundCollider.size = new Vector3(60f, 1f, 20f);

                Root = new GameObject("Synthetic project-owned vehicle aggregate");
                Root.SetActive(false);
                Root.transform.position = new Vector3(25f, 2f, 25f);
                StableEntityIdAuthoring vehicleIdentity =
                    AddStableIdentity(Root, VehicleStableId);
                Chassis = Root.AddComponent<Rigidbody>();
                Chassis.useGravity = false;
                Chassis.isKinematic = false;
                ChassisCollider = Root.AddComponent<BoxCollider>();
                ChassisCollider.size = new Vector3(2f, 1f, 3f);
                VehicleAssemblyController assembly =
                    Root.AddComponent<VehicleAssemblyController>();

                LoosePartsRoot = new GameObject("Synthetic loose parts root");
                LoosePartsRoot.SetActive(false);

                PartDefinition rootDefinition = CreatePartDefinition(
                    RootPartDefinitionId,
                    "Synthetic chassis root",
                    PartCategory.Structural,
                    Array.Empty<PartCompatibilityRule>());
                PartDefinition installedEngineDefinition = CreatePartDefinition(
                    InstalledEngineDefinitionId,
                    "Synthetic installed engine",
                    PartCategory.Engine,
                    new[]
                    {
                        PartCompatibilityRule.Create(
                            EngineSocketType,
                            RootPartDefinitionId),
                    });
                PartDefinition looseEngineADefinition = CreatePartDefinition(
                    LooseEngineADefinitionId,
                    "Synthetic loose engine block",
                    PartCategory.Engine,
                    Array.Empty<PartCompatibilityRule>());
                PartDefinition looseEngineBDefinition = CreatePartDefinition(
                    LooseEngineBDefinitionId,
                    "Synthetic loose engine B",
                    PartCategory.Engine,
                    Array.Empty<PartCompatibilityRule>());

                PartInstance rootPart = CreatePart(
                    "Synthetic chassis part",
                    Root.transform,
                    RootPartStableId,
                    rootDefinition,
                    Root.transform.position,
                    isAssemblyRoot: true,
                    initialMountId: string.Empty,
                    withBody: false,
                    out _,
                    out _);

                MountPointDefinition engineMountDefinition =
                    ScriptableObject.CreateInstance<MountPointDefinition>();
                authoredDefinitions.Add(engineMountDefinition);
                engineMountDefinition.Configure(
                    "test.vehicle.mount-definition.engine",
                    "Synthetic engine mount",
                    EngineSocketType,
                    RootPartDefinitionId,
                    new[] { InstalledEngineDefinitionId },
                    new MountConstraint(0.5f, 180f, 1f, 0f),
                    referenceRadiusMeters: 0.5f,
                    fastenerDefinitions: Array.Empty<FastenerDefinition>());
                var mountObject = new GameObject("Synthetic engine mount");
                mountObject.transform.SetParent(rootPart.transform, false);
                mountObject.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                EngineMount =
                    mountObject.AddComponent<MountPointAuthoring>();
                EngineMount.Configure(
                    engineMountDefinition,
                    EngineMountId,
                    mountObject.transform,
                    0);

                InstalledEngine = CreatePart(
                    "Synthetic installed engine part",
                    LoosePartsRoot.transform,
                    InstalledEngineStableId,
                    installedEngineDefinition,
                    Root.transform.position + new Vector3(0f, 1f, -2f),
                    isAssemblyRoot: false,
                    initialMountId: EngineMountId,
                    withBody: true,
                    out _,
                    out _);
                LooseEngineA = CreatePart(
                    "Synthetic loose engine part A",
                    LoosePartsRoot.transform,
                    LooseEngineAStableId,
                    looseEngineADefinition,
                    Root.transform.position + new Vector3(0f, 1f, -4f),
                    isAssemblyRoot: false,
                    initialMountId: string.Empty,
                    withBody: true,
                    out Rigidbody looseABody,
                    out BoxCollider looseACollider);
                LooseEngineABody = looseABody;
                LooseEngineACollider = looseACollider;
                LooseEngineB = CreatePart(
                    "Synthetic loose engine part B",
                    LoosePartsRoot.transform,
                    LooseEngineBStableId,
                    looseEngineBDefinition,
                    Root.transform.position + new Vector3(2f, 1f, -4f),
                    isAssemblyRoot: false,
                    initialMountId: string.Empty,
                    withBody: true,
                    out Rigidbody looseBBody,
                    out BoxCollider looseBCollider);
                LooseEngineBBody = looseBBody;
                LooseEngineBCollider = looseBCollider;

                assembly.Configure(
                    new[]
                    {
                        rootPart,
                        InstalledEngine,
                        LooseEngineA,
                        LooseEngineB,
                    },
                    new[] { EngineMount },
                    Array.Empty<AssemblyDependency>(),
                    Array.Empty<ToolDefinition>(),
                    LoosePartsRoot.transform);
                AssemblyVehiclePrerequisiteAdapter prerequisites =
                    Root.AddComponent<AssemblyVehiclePrerequisiteAdapter>();
                prerequisites.Configure(assembly);
                prerequisites.ConfigureRequirements(
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    Array.Empty<string>());

                VehicleSimulationConfig simulationConfig =
                    ScriptableObject.CreateInstance<VehicleSimulationConfig>();
                authoredDefinitions.Add(simulationConfig);
                simulationConfig.ApplyProvisionalPrototypeDefaults();
                SyntheticWheelPhysicsBackend backend =
                    Root.AddComponent<SyntheticWheelPhysicsBackend>();
                VehicleSimulationHost simulation =
                    Root.AddComponent<VehicleSimulationHost>();
                simulation.Configure(
                    simulationConfig,
                    backend,
                    prerequisites,
                    runtimeInputSource: null);
                Binding =
                    Root.AddComponent<VehiclePersistenceBinding>();
                Binding.Configure(
                    vehicleIdentity,
                    assembly,
                    simulation,
                    input: null,
                    targetChassis: Chassis);

                Root.SetActive(true);
                LoosePartsRoot.SetActive(true);
                if (!simulation.TryInitialize(out string failure))
                {
                    throw new InvalidOperationException(
                        "Synthetic vehicle simulation failed to initialize: " +
                        failure);
                }

                Physics.SyncTransforms();
            }

            private PartDefinition CreatePartDefinition(
                string definitionId,
                string displayName,
                PartCategory category,
                PartCompatibilityRule[] compatibility)
            {
                PartDefinition definition =
                    ScriptableObject.CreateInstance<PartDefinition>();
                authoredDefinitions.Add(definition);
                definition.Configure(
                    definitionId,
                    displayName,
                    category,
                    mass: 1f,
                    prefab: null,
                    rules: compatibility);
                return definition;
            }

            private static PartInstance CreatePart(
                string name,
                Transform parent,
                string stableId,
                PartDefinition definition,
                Vector3 worldPosition,
                bool isAssemblyRoot,
                string initialMountId,
                bool withBody,
                out Rigidbody body,
                out BoxCollider collider)
            {
                var partObject = new GameObject(name);
                partObject.transform.SetParent(parent, false);
                partObject.transform.position = worldPosition;
                StableEntityIdAuthoring identity =
                    AddStableIdentity(partObject, stableId);
                body = withBody ? partObject.AddComponent<Rigidbody>() : null;
                collider = withBody
                    ? partObject.AddComponent<BoxCollider>()
                    : null;
                if (body != null)
                {
                    body.useGravity = true;
                    body.isKinematic = false;
                    body.detectCollisions = true;
                }

                if (collider != null)
                {
                    collider.size = Vector3.one * 0.6f;
                }

                PartInstance part = partObject.AddComponent<PartInstance>();
                part.Configure(
                    definition,
                    identity,
                    body,
                    pickup: null,
                    isRoot: isAssemblyRoot,
                    mountedAtStart: initialMountId);
                return part;
            }

            private static StableEntityIdAuthoring AddStableIdentity(
                GameObject target,
                string stableId)
            {
                if (!StableEntityId.TryParse(
                        stableId,
                        out StableEntityId parsed))
                {
                    throw new InvalidOperationException(
                        "Synthetic stable ID is invalid: " + stableId);
                }

                StableEntityIdAuthoring identity =
                    target.AddComponent<StableEntityIdAuthoring>();
                identity.InitializeExplicitRuntimeId(parsed);
                return identity;
            }
        }

        private sealed class SyntheticWheelPhysicsBackend : MonoBehaviour,
            IWheelPhysicsBackend
        {
            public int WheelCount => 4;

            public float VehicleSpeedMetersPerSecond => 0f;

            public void Sample(
                float fixedDeltaSeconds,
                WheelPhysicsSample[] destination)
            {
                if (destination == null)
                {
                    return;
                }

                for (int index = 0; index < destination.Length; index++)
                {
                    destination[index] = WheelPhysicsSample.NoContact;
                }
            }

            public void Apply(
                float fixedDeltaSeconds,
                WheelPhysicsCommand[] commands)
            {
            }

            public void Reset()
            {
            }
        }
    }
}
