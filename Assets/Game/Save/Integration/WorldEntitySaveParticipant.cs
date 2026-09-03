using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Interaction.Carrying;
using MSC.Items;
using MSC.Vehicle.Assembly;
using MSC.World.Streaming;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Save.Integration
{
    internal sealed class WorldEntitySaveParticipant : ISaveParticipant
    {
        public const string DomainId = "world.entities";
        private const string CellRetentionOwnerPrefix = DomainId + ":";

        private readonly DeferredStableEntityStore deferredEntities;
        private readonly ProductionWorldStreamingService worldStreaming;
        private readonly Scene persistentScene;
        private readonly PhysicalCarryController carryController;
        private readonly ImportantObjectRecoveryFormation recoveryFormation;
        private readonly float recoveryMinimumY;
        private readonly Dictionary<string, PhysicsPickupTarget> loadedTargets =
            new Dictionary<string, PhysicsPickupTarget>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> sourceCellIds =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> rehomedSourceCellIds =
            new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, SuspendedWorldEntity>
            suspendedTargets =
                new Dictionary<string, SuspendedWorldEntity>(
                    StringComparer.Ordinal);
        private readonly HashSet<string> retainedEntityIds =
            new HashSet<string>(StringComparer.Ordinal);

        public WorldEntitySaveParticipant(
            DeferredStableEntityStore deferredEntities)
            : this(deferredEntities, null, default, null, null, -64f)
        {
        }

        public WorldEntitySaveParticipant(
            DeferredStableEntityStore deferredEntities,
            ProductionWorldStreamingService worldStreaming,
            Scene persistentScene,
            PhysicalCarryController carryController = null,
            ImportantObjectRecoveryFormation recoveryFormation = null,
            float recoveryMinimumY = -64f)
        {
            this.deferredEntities = deferredEntities ??
                throw new ArgumentNullException(nameof(deferredEntities));
            this.worldStreaming = worldStreaming;
            this.persistentScene = persistentScene;
            this.carryController = carryController;
            this.recoveryFormation = recoveryFormation;
            this.recoveryMinimumY = recoveryMinimumY;
            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                WorldEntityDomainSaveDto.CurrentSchemaVersion,
                required: true,
                SaveRestorePhase.WorldEntities);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload() =>
            SaveParticipantJson.Serialize(CaptureDomainState());

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            WorldEntityDomainSaveDto state =
                SaveParticipantJson.Deserialize<WorldEntityDomainSaveDto>(
                    envelope.PayloadJson);
            if (!state.TryValidate(out string failure))
            {
                throw new InvalidDataException(
                    "World entity save preflight failed: " + failure);
            }

            return state;
        }

        public object CaptureCheckpoint()
        {
            WorldEntityDomainSaveDto state = CaptureDomainState();
            return new WorldEntityCheckpoint
            {
                State = state,
                SourceCellIds = new Dictionary<string, string>(
                    sourceCellIds,
                    StringComparer.Ordinal),
                RehomedSourceCellIds = new Dictionary<string, string>(
                    rehomedSourceCellIds,
                    StringComparer.Ordinal),
                RetainedEntityIds = retainedEntityIds
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                RuntimeEntities = loadedTargets.Values
                    .Where(target => target != null)
                    .OrderBy(
                        target => target.StableId.Value,
                        StringComparer.Ordinal)
                    .Select(CaptureRuntimeEntityCheckpoint)
                    .ToArray(),
                Suspensions = suspendedTargets.Values
                    .Where(candidate =>
                        candidate != null &&
                        candidate.Target != null &&
                        candidate.State != null)
                    .OrderBy(
                        candidate => candidate.State.stableEntityId,
                        StringComparer.Ordinal)
                    .Select(candidate => new SuspensionCheckpoint
                    {
                        StableEntityId = candidate.State.stableEntityId,
                        CellId = candidate.CellId,
                        DetectCollisions = candidate.DetectCollisions,
                        Interpolation = candidate.Interpolation,
                    })
                    .ToArray(),
            };
        }

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            WorldEntityDomainSaveDto state =
                (WorldEntityDomainSaveDto)preparedState;
            recoveryFormation?.Reset();
            RemoveDeferredOwnerState(context.DeferredEntities);
            WorldEntityStateDto[] orderedEntities = state.entities
                .OrderBy(
                    entity => entity.stableEntityId,
                    StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < orderedEntities.Length; index++)
            {
                WorldEntityStateDto entity = orderedEntities[index];
                if (TryResolve(entity.stableEntityId, out PhysicsPickupTarget target))
                {
                    RecoverImportantTargetIfNeeded(
                        target,
                        entity,
                        context.UnresolvedContent);
                    if (IsSavedPoseOutsideTargetScene(target, entity) &&
                        !TryMoveToPersistentScene(target, entity))
                    {
                        throw new InvalidOperationException(
                            $"World entity '{entity.stableEntityId}' cannot " +
                            "leave its authored cell safely because its " +
                            "Rigidbody root cannot be moved to the persistent scene.");
                    }

                    RememberSourceCell(entity);
                    if (suspendedTargets.TryGetValue(
                            entity.stableEntityId,
                            out SuspendedWorldEntity suspended) &&
                        suspended.Target == target)
                    {
                        ApplyOverExistingSuspension(
                            target,
                            entity,
                            suspended);
                    }
                    else
                    {
                        ApplyOrSuspendRestoredTarget(target, entity);
                    }
                    ReleaseSourceCellRetention(entity.stableEntityId);
                    continue;
                }

                context.DeferredEntities.Enqueue(ToDeferred(entity));
                TryRetainSourceCell(entity, context.UnresolvedContent);
                context.UnresolvedContent.Add(
                    DomainId,
                    entity.stableEntityId,
                    UnresolvedContentReason.DeferredUntilCellLoad,
                    "Mutable pickup state is retained until its owning world cell loads.");
            }
        }

        private void RecoverImportantTargetIfNeeded(
            PhysicsPickupTarget target,
            WorldEntityStateDto state,
            UnresolvedContentReport unresolvedContent)
        {
            if (recoveryFormation == null || target == null ||
                state == null ||
                state.worldPosition.y >= recoveryMinimumY)
            {
                return;
            }

            WorldItemInstance item =
                target.GetComponentInParent<WorldItemInstance>();
            if (item == null || item.Definition == null ||
                !item.Definition.CriticalRecovery)
            {
                return;
            }

            Vector3 previousPosition = state.worldPosition;
            if (!recoveryFormation.TryReserve(
                    item.gameObject,
                    state.worldRotation,
                    out Pose recoveredPose,
                    out string failure))
            {
                throw new InvalidDataException(
                    $"Important item '{state.stableEntityId}' could not be " +
                    "recovered in front of the player home: " + failure);
            }

            state.worldPosition = recoveredPose.position;
            state.worldRotation = recoveredPose.rotation;
            state.linearVelocity = Vector3.zero;
            state.angularVelocity = Vector3.zero;
            state.sleeping = true;
            unresolvedContent?.Add(
                DomainId,
                state.stableEntityId,
                UnresolvedContentReason.ParticipantReported,
                $"Recovered important item from ({previousPosition.x:F3}, " +
                $"{previousPosition.y:F3}, {previousPosition.z:F3}) to " +
                $"'{ImportantObjectRecoveryFormation.AnchorId}' at " +
                $"({recoveredPose.position.x:F3}, " +
                $"{recoveredPose.position.y:F3}, " +
                $"{recoveredPose.position.z:F3}); stable ID preserved.");
        }

        public void Rollback(object checkpoint)
        {
            WorldEntityCheckpoint captured =
                (WorldEntityCheckpoint)checkpoint;
            foreach (string stableEntityId in retainedEntityIds.ToArray())
            {
                ReleaseSourceCellRetention(stableEntityId);
            }

            sourceCellIds.Clear();
            foreach (KeyValuePair<string, string> pair in captured.SourceCellIds)
            {
                sourceCellIds.Add(pair.Key, pair.Value);
            }

            rehomedSourceCellIds.Clear();
            foreach (KeyValuePair<string, string> pair in
                     captured.RehomedSourceCellIds)
            {
                rehomedSourceCellIds.Add(pair.Key, pair.Value);
            }

            var runtimeById = captured.RuntimeEntities.ToDictionary(
                entry => entry.StableEntityId,
                entry => entry,
                StringComparer.Ordinal);
            for (int index = 0;
                 index < captured.RuntimeEntities.Length;
                 index++)
            {
                RestoreRuntimeEntityTopology(captured.RuntimeEntities[index]);
            }

            var suspensionById = captured.Suspensions.ToDictionary(
                entry => entry.StableEntityId,
                entry => entry,
                StringComparer.Ordinal);
            suspendedTargets.Clear();

            WorldEntityDomainSaveDto state = captured.State;
            for (int index = 0; index < captured.RetainedEntityIds.Length; index++)
            {
                string retainedEntityId = captured.RetainedEntityIds[index];
                WorldEntityStateDto retainedState = state.entities.FirstOrDefault(
                    candidate => string.Equals(
                        candidate.stableEntityId,
                        retainedEntityId,
                        StringComparison.Ordinal));
                if (retainedState == null ||
                    !TryRetainSourceCell(retainedState, unresolvedContent: null))
                {
                    throw new InvalidOperationException(
                        $"Could not restore world-cell retention for '{retainedEntityId}'.");
                }
            }

            for (int index = 0; index < state.entities.Length; index++)
            {
                WorldEntityStateDto entity = state.entities[index];
                if (TryResolve(entity.stableEntityId, out PhysicsPickupTarget target))
                {
                    if (suspensionById.TryGetValue(
                            entity.stableEntityId,
                            out SuspensionCheckpoint suspension))
                    {
                        suspendedTargets[entity.stableEntityId] =
                            new SuspendedWorldEntity(
                                target,
                                entity,
                                suspension.CellId,
                                suspension.DetectCollisions,
                                suspension.Interpolation);
                        ApplyStateWhileGuarded(
                            target,
                            entity);
                        Physics.SyncTransforms();
                    }
                    else
                    {
                        if (runtimeById.TryGetValue(
                                entity.stableEntityId,
                                out RuntimeEntityCheckpoint runtime) &&
                            target.Body != null)
                        {
                            target.Body.detectCollisions =
                                runtime.DetectCollisions;
                            target.Body.interpolation =
                                runtime.Interpolation;
                        }

                        ApplyToLoadedTarget(target, entity);
                    }
                }
            }
        }

        public void RegisterScene(Scene scene)
        {
            SceneRegistrationBatch batch = BeginRegisterScene(scene);
            CompleteRegisterScene(batch);
        }

        internal SceneRegistrationBatch BeginRegisterScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return null;
            }

            var batch = new SceneRegistrationBatch();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                PhysicsPickupTarget[] targets =
                    roots[rootIndex].GetComponentsInChildren<PhysicsPickupTarget>(true);
                for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
                {
                    PhysicsPickupTarget target = targets[targetIndex];
                    if (target == null ||
                        target.GetComponentInParent<PartInstance>() != null)
                    {
                        // VehicleAssemblySaveData is authoritative for both
                        // loose and installed Satsuma parts. Registering the
                        // same Rigidbody in world.entities creates two restore
                        // owners and tears subassemblies away from the chassis.
                        continue;
                    }

                    batch.Add(target);
                }
            }

            string loadedCellId = string.Empty;
            if (worldStreaming != null &&
                worldStreaming.TryGetCellIdForScene(scene, out loadedCellId))
            {
                foreach (SuspendedWorldEntity suspended in suspendedTargets
                             .Values
                             .Where(candidate =>
                                 candidate.Target != null &&
                                 string.Equals(
                                     candidate.CellId,
                                     loadedCellId,
                                     StringComparison.Ordinal))
                             .OrderBy(
                                 candidate => candidate.State.stableEntityId,
                                 StringComparer.Ordinal)
                             .ToArray())
                {
                    batch.Add(suspended.Target);
                }
            }

            batch.SortTargetsByStableId();
            batch.FreezeAll();
            for (int index = 0; index < batch.TargetCount; index++)
            {
                RegisterTarget(batch.GetTarget(index), batch);
            }

            if (!string.IsNullOrEmpty(loadedCellId))
            {
                SuspendedWorldEntity[] ready = suspendedTargets.Values
                    .Where(candidate =>
                        candidate.Target != null &&
                        string.Equals(
                            candidate.CellId,
                            loadedCellId,
                            StringComparison.Ordinal))
                    .OrderBy(
                        candidate => candidate.State.stableEntityId,
                        StringComparer.Ordinal)
                    .ToArray();
                for (int index = 0; index < ready.Length; index++)
                {
                    SuspendedWorldEntity suspended = ready[index];
                    batch.ApplySuspendedState(
                        suspended.Target,
                        suspended.State,
                        suspended.DetectCollisions,
                        suspended.Interpolation);
                    string stableEntityId =
                        suspended.State.stableEntityId;
                    PhysicsPickupTarget expectedTarget = suspended.Target;
                    batch.AddCompletion(() => CompleteSuspension(
                        stableEntityId,
                        expectedTarget));
                }
            }

            return batch;
        }

        internal static void CompleteRegisterScene(SceneRegistrationBatch batch)
        {
            // Presentation-only streaming layers freeze/restore no bodies. Avoid
            // synchronizing the entire physics world twice for an empty batch.
            if (batch == null || batch.TargetCount == 0)
            {
                return;
            }

            Physics.SyncTransforms();
            batch.RestoreCollisionsAll();
            Physics.SyncTransforms();
            batch.ReleaseDynamicsAll();
            Physics.SyncTransforms();
            batch.Complete();
        }

        public void CaptureAndUnregisterScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            string unloadingCellId = string.Empty;
            bool isBaseCellScene = worldStreaming != null &&
                worldStreaming.TryGetCellIdForScene(
                    scene,
                    out unloadingCellId);
            PhysicsPickupTarget[] owned = loadedTargets.Values
                .Where(target =>
                    target != null &&
                    (target.gameObject.scene.handle == scene.handle ||
                     isBaseCellScene &&
                     target.gameObject.scene.handle == persistentScene.handle &&
                     !target.IsCarried &&
                     !suspendedTargets.ContainsKey(target.StableId.Value) &&
                     worldStreaming.TryGetCellIdForPosition(
                         target.Body.position,
                         out string currentCellId) &&
                     string.Equals(
                         currentCellId,
                         unloadingCellId,
                         StringComparison.Ordinal)))
                .Distinct()
                .ToArray();
            var plan = new SceneEntityUnloadPlan[owned.Length];
            bool unloadVetoed = false;
            for (int index = 0; index < owned.Length; index++)
            {
                PhysicsPickupTarget target = owned[index];
                WorldEntityStateDto state = CaptureTarget(target);
                bool suspendInPlace =
                    target.gameObject.scene.handle != scene.handle;
                bool mustRemainLoaded =
                    !suspendInPlace &&
                    ShouldRehomeOutsideSourceCell(target, state);
                bool canRehome =
                    mustRemainLoaded && CanMoveToPersistentScene(target);
                plan[index] = new SceneEntityUnloadPlan(
                    target,
                    state,
                    suspendInPlace,
                    suspendInPlace ? unloadingCellId : string.Empty,
                    mustRemainLoaded,
                    canRehome);

                if (!mustRemainLoaded || canRehome)
                {
                    continue;
                }

                if (!TryRetainSourceCell(state, unresolvedContent: null))
                {
                    throw new InvalidOperationException(
                        $"Could not retain source cell for live world entity " +
                        $"'{state.stableEntityId}'.");
                }

                unloadVetoed = true;
            }

            for (int index = 0; index < plan.Length; index++)
            {
                SceneEntityUnloadPlan entry = plan[index];
                if (!entry.MustRemainLoaded || entry.CanRehome)
                {
                    ReleaseSourceCellRetention(entry.State.stableEntityId);
                }
            }

            if (unloadVetoed)
            {
                // Planning is deliberately side-effect free for scene objects
                // and registries. One unsafe live hierarchy vetoes the whole
                // unload so no earlier entity can be left half-unregistered.
                return;
            }

            for (int index = 0; index < plan.Length; index++)
            {
                SceneEntityUnloadPlan entry = plan[index];
                if (entry.SuspendInPlace)
                {
                    SuspendInPlace(entry);
                    continue;
                }

                if (entry.MustRemainLoaded)
                {
                    if (!TryMoveToPersistentScene(entry.Target, entry.State))
                    {
                        throw new InvalidOperationException(
                            $"World entity '{entry.State.stableEntityId}' could " +
                            "not be transferred after a successful unload preflight.");
                    }

                    continue;
                }

                deferredEntities.Enqueue(
                    ToDeferred(entry.State),
                    replaceExisting: true);
                GuardForSceneTeardown(entry.Target);
                loadedTargets.Remove(entry.State.stableEntityId);
                sourceCellIds.Remove(entry.State.stableEntityId);
                rehomedSourceCellIds.Remove(entry.State.stableEntityId);
            }

            if (plan.Length > 0)
            {
                Physics.SyncTransforms();
            }
        }

        public bool TryResolve(
            string stableEntityId,
            out PhysicsPickupTarget target)
        {
            if (loadedTargets.TryGetValue(
                    stableEntityId ?? string.Empty,
                    out target) &&
                target != null)
            {
                return true;
            }

            loadedTargets.Remove(stableEntityId ?? string.Empty);
            sourceCellIds.Remove(stableEntityId ?? string.Empty);
            rehomedSourceCellIds.Remove(stableEntityId ?? string.Empty);
            suspendedTargets.Remove(stableEntityId ?? string.Empty);
            target = null;
            return false;
        }

        private WorldEntityDomainSaveDto CaptureDomainState()
        {
            var byId = new Dictionary<string, WorldEntityStateDto>(
                StringComparer.Ordinal);
            DeferredStableEntityPayload[] pending = deferredEntities.Snapshot();
            for (int index = 0; index < pending.Length; index++)
            {
                DeferredStableEntityPayload payload = pending[index];
                if (!string.Equals(
                        payload.OwnerDomainId,
                        DomainId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                WorldEntityStateDto state =
                    SaveParticipantJson.Deserialize<WorldEntityStateDto>(
                        payload.PayloadJson);
                if (!state.TryValidate(out string failure) ||
                    !string.Equals(
                        state.stableEntityId,
                        payload.StableEntityId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Deferred world entity state is invalid: " + failure);
                }

                byId.Add(state.stableEntityId, state);
            }

            foreach (KeyValuePair<string, PhysicsPickupTarget> pair in
                     loadedTargets.ToArray())
            {
                if (pair.Value == null)
                {
                    loadedTargets.Remove(pair.Key);
                    continue;
                }

                byId[pair.Key] = suspendedTargets.TryGetValue(
                        pair.Key,
                        out SuspendedWorldEntity suspended) &&
                    suspended.Target == pair.Value
                        ? suspended.State
                        : CaptureTarget(pair.Value);
            }

            var dto = new WorldEntityDomainSaveDto
            {
                entities = byId.Values
                    .OrderBy(entity => entity.stableEntityId, StringComparer.Ordinal)
                    .ToArray(),
            };
            if (!dto.TryValidate(out string domainFailure))
            {
                throw new InvalidDataException(
                    "Captured world entity state is invalid: " + domainFailure);
            }

            return dto;
        }

        internal void RegisterTarget(PhysicsPickupTarget target)
        {
            RegisterTarget(target, null);
        }

        private void RegisterTarget(
            PhysicsPickupTarget target,
            SceneRegistrationBatch batch)
        {
            if (target == null || !target.StableId.IsValid)
            {
                throw new InvalidOperationException(
                    "A loaded PhysicsPickupTarget has no valid project-owned stable ID.");
            }

            string id = target.StableId.Value;
            if (loadedTargets.TryGetValue(id, out PhysicsPickupTarget existing) &&
                existing != null && existing != target)
            {
                if (IsCanonicalCloneOfRehomedEntity(
                        id,
                        existing,
                        target))
                {
                    batch?.Discard(target);
                    DestroyCanonicalClone(target);
                    return;
                }

                throw new InvalidOperationException(
                    $"Duplicate loaded pickup stable ID '{id}'.");
            }

            loadedTargets[id] = target;
            if (!sourceCellIds.ContainsKey(id) &&
                worldStreaming != null &&
                worldStreaming.TryGetCellIdForScene(
                    target.gameObject.scene,
                    out string sourceCellId))
            {
                sourceCellIds[id] = sourceCellId;
            }

            if (!deferredEntities.TryPeek(
                    DomainId,
                    id,
                    out DeferredStableEntityPayload pending))
            {
                return;
            }

            WorldEntityStateDto state =
                SaveParticipantJson.Deserialize<WorldEntityStateDto>(
                    pending.PayloadJson);
            if (!state.TryValidate(out string failure) ||
                !string.Equals(state.stableEntityId, id, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Deferred state for world entity '{id}' is invalid: {failure}");
            }

            RecoverImportantTargetIfNeeded(
                target,
                state,
                unresolvedContent: null);
            if (IsSavedPoseOutsideTargetScene(target, state) &&
                !TryMoveToPersistentScene(target, state))
            {
                throw new InvalidOperationException(
                    $"Deferred world entity '{id}' cannot leave its authored " +
                    "cell safely because its Rigidbody root cannot be moved to " +
                    "the persistent scene.");
            }

            RememberSourceCell(state);
            if (batch != null)
            {
                if (ShouldSuspendForMissingSupport(
                        target,
                        state,
                        out string supportCellId))
                {
                    batch.HoldDeferredState(
                        target,
                        state,
                        out bool detectCollisions,
                        out RigidbodyInterpolation interpolation);
                    suspendedTargets[state.stableEntityId] =
                        new SuspendedWorldEntity(
                            target,
                            state,
                            supportCellId,
                            detectCollisions,
                            interpolation);
                }
                else
                {
                    batch.ApplyDeferredState(target, state);
                }
            }
            else
            {
                ApplyOrSuspendRestoredTarget(target, state);
            }
            deferredEntities.TryTake(DomainId, id, out _);
            ReleaseSourceCellRetention(id);
        }

        internal void UnregisterTarget(PhysicsPickupTarget target)
        {
            if (target == null || !target.StableId.IsValid)
            {
                return;
            }

            string id = target.StableId.Value;
            if (!loadedTargets.TryGetValue(id, out PhysicsPickupTarget existing) ||
                existing != target)
            {
                return;
            }

            loadedTargets.Remove(id);
            sourceCellIds.Remove(id);
            rehomedSourceCellIds.Remove(id);
            suspendedTargets.Remove(id);
            ReleaseSourceCellRetention(id);
        }

        private void SuspendInPlace(SceneEntityUnloadPlan entry)
        {
            Rigidbody body = entry.Target?.Body ??
                throw new InvalidOperationException(
                    "A persistent world entity lost its Rigidbody before suspension.");
            var suspended = new SuspendedWorldEntity(
                entry.Target,
                entry.State,
                entry.SuspensionCellId,
                body.detectCollisions,
                body.interpolation);
            suspendedTargets[entry.State.stableEntityId] = suspended;
            GuardForSceneTeardown(entry.Target);
        }

        private void ApplyOrSuspendRestoredTarget(
            PhysicsPickupTarget target,
            WorldEntityStateDto state)
        {
            if (ShouldSuspendForMissingSupport(
                    target,
                    state,
                    out string supportCellId))
            {
                Rigidbody body = target.Body;
                var suspended = new SuspendedWorldEntity(
                    target,
                    state,
                    supportCellId,
                    body.detectCollisions,
                    body.interpolation);
                suspendedTargets[state.stableEntityId] = suspended;
                ApplyStateWhileGuarded(target, state);
                Physics.SyncTransforms();
                return;
            }

            ApplyToLoadedTarget(target, state);
        }

        private void ApplyOverExistingSuspension(
            PhysicsPickupTarget target,
            WorldEntityStateDto state,
            SuspendedWorldEntity previousSuspension)
        {
            if (ShouldSuspendForMissingSupport(
                    target,
                    state,
                    out string supportCellId))
            {
                suspendedTargets[state.stableEntityId] =
                    new SuspendedWorldEntity(
                        target,
                        state,
                        supportCellId,
                        previousSuspension.DetectCollisions,
                        previousSuspension.Interpolation);
                ApplyStateWhileGuarded(target, state);
                Physics.SyncTransforms();
                return;
            }

            suspendedTargets.Remove(state.stableEntityId);
            target.Body.detectCollisions =
                previousSuspension.DetectCollisions;
            target.Body.interpolation =
                previousSuspension.Interpolation;
            ApplyToLoadedTarget(target, state);
        }

        private bool ShouldSuspendForMissingSupport(
            PhysicsPickupTarget target,
            WorldEntityStateDto state,
            out string supportCellId)
        {
            supportCellId = string.Empty;
            return worldStreaming != null &&
                   target != null && target.Body != null &&
                   target.gameObject.scene.handle == persistentScene.handle &&
                   worldStreaming.TryGetCellIdForPosition(
                       state.worldPosition,
                       out supportCellId) &&
                   !worldStreaming.IsCellLoaded(supportCellId);
        }

        private bool IsSavedPoseOutsideTargetScene(
            PhysicsPickupTarget target,
            WorldEntityStateDto state)
        {
            if (worldStreaming == null || target == null || state == null ||
                target.gameObject.scene.handle == persistentScene.handle ||
                !worldStreaming.TryGetCellIdForScene(
                    target.gameObject.scene,
                    out string targetCellId))
            {
                return false;
            }

            return !worldStreaming.TryGetCellIdForPosition(
                       state.worldPosition,
                       out string savedCellId) ||
                   !string.Equals(
                       savedCellId,
                       targetCellId,
                       StringComparison.Ordinal);
        }

        private static void ApplyStateWhileGuarded(
            PhysicsPickupTarget target,
            WorldEntityStateDto state)
        {
            Rigidbody body = target?.Body ??
                throw new InvalidOperationException(
                    "A restored world entity lost its Rigidbody before suspension.");
            target.gameObject.SetActive(state.activeSelf);
            body.interpolation = RigidbodyInterpolation.None;
            body.detectCollisions = false;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            body.useGravity = target.UsesGravityWhenLoose || state.useGravity;
            body.position = state.worldPosition;
            body.rotation = state.worldRotation.normalized;
            body.transform.SetPositionAndRotation(
                state.worldPosition,
                state.worldRotation.normalized);
        }

        private void CompleteSuspension(
            string stableEntityId,
            PhysicsPickupTarget expectedTarget)
        {
            if (suspendedTargets.TryGetValue(
                    stableEntityId,
                    out SuspendedWorldEntity suspended) &&
                suspended.Target == expectedTarget)
            {
                suspendedTargets.Remove(stableEntityId);
                ReleaseSourceCellRetention(stableEntityId);
            }
        }

        private static void GuardForSceneTeardown(
            PhysicsPickupTarget target)
        {
            Rigidbody body = target?.Body ??
                throw new InvalidOperationException(
                    "A world entity lost its Rigidbody before scene teardown.");
            body.interpolation = RigidbodyInterpolation.None;
            body.detectCollisions = false;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.isKinematic = true;
        }

        private WorldEntityStateDto CaptureTarget(
            PhysicsPickupTarget target)
        {
            Rigidbody body = target?.Body ?? throw new InvalidOperationException(
                "A registered pickup lost its Rigidbody save boundary.");
            bool isKinematic = body.isKinematic;
            bool useGravity = target.UsesGravityWhenLoose || body.useGravity;
            bool carriedByConfiguredController =
                carryController != null &&
                carryController.TryGetHeldWorldPersistencePhysics(
                    body,
                    out isKinematic,
                    out useGravity);
            var state = new WorldEntityStateDto
            {
                stableEntityId = target.StableId.Value,
                sourceCellId = ResolveSourceCellId(target),
                worldPosition = body.position,
                worldRotation = body.rotation,
                linearVelocity = body.isKinematic || carriedByConfiguredController
                    ? Vector3.zero
                    : body.linearVelocity,
                angularVelocity = body.isKinematic || carriedByConfiguredController
                    ? Vector3.zero
                    : body.angularVelocity,
                isKinematic = isKinematic,
                useGravity = useGravity,
                sleeping = !carriedByConfiguredController && body.IsSleeping(),
                activeSelf = target.gameObject.activeSelf,
            };
            if (!state.TryValidate(out string failure))
            {
                throw new InvalidDataException(
                    "Cannot capture mutable pickup state: " + failure);
            }

            return state;
        }

        private string ResolveSourceCellId(PhysicsPickupTarget target)
        {
            string stableEntityId = target.StableId.Value;
            if (sourceCellIds.TryGetValue(
                    stableEntityId,
                    out string rememberedCellId))
            {
                return rememberedCellId;
            }

            if (worldStreaming != null &&
                worldStreaming.TryGetCellIdForScene(
                    target.gameObject.scene,
                    out string sourceCellId))
            {
                sourceCellIds[stableEntityId] = sourceCellId;
                return sourceCellId;
            }

            return string.Empty;
        }

        private void RememberSourceCell(WorldEntityStateDto state)
        {
            if (!string.IsNullOrEmpty(state.sourceCellId))
            {
                sourceCellIds[state.stableEntityId] = state.sourceCellId;
            }
        }

        private bool ShouldRehomeOutsideSourceCell(
            PhysicsPickupTarget target,
            WorldEntityStateDto state)
        {
            if (target.IsCarried)
            {
                return true;
            }

            if (worldStreaming == null || string.IsNullOrEmpty(state.sourceCellId))
            {
                return false;
            }

            return !worldStreaming.TryGetCellIdForPosition(
                       state.worldPosition,
                       out string currentCellId) ||
                   !string.Equals(
                       currentCellId,
                       state.sourceCellId,
                       StringComparison.Ordinal);
        }

        private bool CanMoveToPersistentScene(PhysicsPickupTarget target) =>
            TryResolvePersistentEntityRoot(target, out _);

        private bool TryMoveToPersistentScene(
            PhysicsPickupTarget target,
            WorldEntityStateDto state)
        {
            if (!TryResolvePersistentEntityRoot(
                    target,
                    out Transform entityRoot))
            {
                return false;
            }

            if (entityRoot.gameObject.scene.handle == persistentScene.handle)
            {
                RememberRehomedSourceCell(state);
                return true;
            }

            entityRoot.SetParent(null, worldPositionStays: true);
            SceneManager.MoveGameObjectToScene(entityRoot.gameObject, persistentScene);
            RememberRehomedSourceCell(state);
            return true;
        }

        private bool TryResolvePersistentEntityRoot(
            PhysicsPickupTarget target,
            out Transform entityRoot)
        {
            entityRoot = null;
            if (!persistentScene.IsValid() || !persistentScene.isLoaded ||
                target == null || target.Body == null)
            {
                return false;
            }

            Transform targetTransform = target.transform;
            Transform bodyTransform = target.Body.transform;
            if (bodyTransform == targetTransform ||
                bodyTransform.IsChildOf(targetTransform))
            {
                entityRoot = targetTransform;
                return true;
            }

            if (targetTransform.IsChildOf(bodyTransform))
            {
                entityRoot = bodyTransform;
                return true;
            }

            return false;
        }

        private void RememberRehomedSourceCell(WorldEntityStateDto state)
        {
            if (!string.IsNullOrEmpty(state.sourceCellId))
            {
                rehomedSourceCellIds[state.stableEntityId] = state.sourceCellId;
            }
        }

        private bool IsCanonicalCloneOfRehomedEntity(
            string stableEntityId,
            PhysicsPickupTarget existing,
            PhysicsPickupTarget incoming)
        {
            return existing.gameObject.scene.handle == persistentScene.handle &&
                   rehomedSourceCellIds.TryGetValue(
                       stableEntityId,
                       out string sourceCellId) &&
                   worldStreaming != null &&
                   worldStreaming.TryGetCellIdForScene(
                       incoming.gameObject.scene,
                       out string incomingCellId) &&
                   string.Equals(
                       incomingCellId,
                       sourceCellId,
                       StringComparison.Ordinal);
        }

        private void DestroyCanonicalClone(PhysicsPickupTarget target)
        {
            GameObject entityRoot = TryResolvePersistentEntityRoot(
                    target,
                    out Transform resolvedRoot)
                ? resolvedRoot.gameObject
                : target.gameObject;
            entityRoot.SetActive(false);
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(entityRoot);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(entityRoot);
            }
        }

        private bool TryRetainSourceCell(
            WorldEntityStateDto state,
            UnresolvedContentReport unresolvedContent)
        {
            if (worldStreaming == null || string.IsNullOrEmpty(state.sourceCellId))
            {
                return false;
            }

            try
            {
                worldStreaming.RetainCell(
                    GetCellRetentionOwnerId(state.stableEntityId),
                    state.sourceCellId);
                retainedEntityIds.Add(state.stableEntityId);
                return true;
            }
            catch (ArgumentException exception)
            {
                unresolvedContent?.Add(
                    DomainId,
                    state.stableEntityId,
                    UnresolvedContentReason.ParticipantReported,
                    "Saved source world cell is unavailable: " + exception.Message);
                return false;
            }
        }

        private void ReleaseSourceCellRetention(string stableEntityId)
        {
            worldStreaming?.ReleaseCellRetention(
                GetCellRetentionOwnerId(stableEntityId));
            retainedEntityIds.Remove(stableEntityId);
        }

        private RuntimeEntityCheckpoint CaptureRuntimeEntityCheckpoint(
            PhysicsPickupTarget target)
        {
            Rigidbody body = target.Body ??
                throw new InvalidOperationException(
                    $"World entity '{target.StableId.Value}' lost its Rigidbody " +
                    "while a save checkpoint was being captured.");
            bool hasRestorableRoot = TryResolvePersistentEntityRoot(
                target,
                out Transform entityRoot);
            return new RuntimeEntityCheckpoint
            {
                StableEntityId = target.StableId.Value,
                HasRestorableRoot = hasRestorableRoot,
                EntityRoot = hasRestorableRoot ? entityRoot : null,
                OriginalScene = hasRestorableRoot
                    ? entityRoot.gameObject.scene
                    : default,
                HadParent = hasRestorableRoot && entityRoot.parent != null,
                OriginalParent = hasRestorableRoot
                    ? entityRoot.parent
                    : null,
                OriginalSiblingIndex = hasRestorableRoot
                    ? entityRoot.GetSiblingIndex()
                    : 0,
                DetectCollisions = body.detectCollisions,
                Interpolation = body.interpolation,
            };
        }

        private static void RestoreRuntimeEntityTopology(
            RuntimeEntityCheckpoint checkpoint)
        {
            if (checkpoint == null || !checkpoint.HasRestorableRoot ||
                checkpoint.EntityRoot == null)
            {
                return;
            }

            if (!checkpoint.OriginalScene.IsValid() ||
                !checkpoint.OriginalScene.isLoaded)
            {
                throw new InvalidOperationException(
                    $"Cannot roll back world entity " +
                    $"'{checkpoint.StableEntityId}' because its original scene " +
                    "is no longer loaded.");
            }

            Transform entityRoot = checkpoint.EntityRoot;
            if (entityRoot.parent != null)
            {
                entityRoot.SetParent(null, worldPositionStays: true);
            }

            if (entityRoot.gameObject.scene.handle !=
                checkpoint.OriginalScene.handle)
            {
                SceneManager.MoveGameObjectToScene(
                    entityRoot.gameObject,
                    checkpoint.OriginalScene);
            }

            if (checkpoint.HadParent)
            {
                if (checkpoint.OriginalParent == null ||
                    checkpoint.OriginalParent.gameObject.scene.handle !=
                    checkpoint.OriginalScene.handle)
                {
                    throw new InvalidOperationException(
                        $"Cannot roll back world entity " +
                        $"'{checkpoint.StableEntityId}' because its original " +
                        "parent hierarchy is unavailable.");
                }

                entityRoot.SetParent(
                    checkpoint.OriginalParent,
                    worldPositionStays: true);
            }

            entityRoot.SetSiblingIndex(checkpoint.OriginalSiblingIndex);
        }

        private static string GetCellRetentionOwnerId(string stableEntityId) =>
            CellRetentionOwnerPrefix + stableEntityId;

        private sealed class WorldEntityCheckpoint
        {
            public WorldEntityDomainSaveDto State;
            public Dictionary<string, string> SourceCellIds;
            public Dictionary<string, string> RehomedSourceCellIds;
            public string[] RetainedEntityIds;
            public RuntimeEntityCheckpoint[] RuntimeEntities;
            public SuspensionCheckpoint[] Suspensions;
        }

        private sealed class RuntimeEntityCheckpoint
        {
            public string StableEntityId;
            public bool HasRestorableRoot;
            public Transform EntityRoot;
            public Scene OriginalScene;
            public bool HadParent;
            public Transform OriginalParent;
            public int OriginalSiblingIndex;
            public bool DetectCollisions;
            public RigidbodyInterpolation Interpolation;
        }

        private sealed class SuspensionCheckpoint
        {
            public string StableEntityId;
            public string CellId;
            public bool DetectCollisions;
            public RigidbodyInterpolation Interpolation;
        }

        private sealed class SceneEntityUnloadPlan
        {
            public SceneEntityUnloadPlan(
                PhysicsPickupTarget target,
                WorldEntityStateDto state,
                bool suspendInPlace,
                string suspensionCellId,
                bool mustRemainLoaded,
                bool canRehome)
            {
                Target = target;
                State = state;
                SuspendInPlace = suspendInPlace;
                SuspensionCellId = suspensionCellId ?? string.Empty;
                MustRemainLoaded = mustRemainLoaded;
                CanRehome = canRehome;
            }

            public PhysicsPickupTarget Target { get; }
            public WorldEntityStateDto State { get; }
            public bool SuspendInPlace { get; }
            public string SuspensionCellId { get; }
            public bool MustRemainLoaded { get; }
            public bool CanRehome { get; }
        }

        private sealed class SuspendedWorldEntity
        {
            public SuspendedWorldEntity(
                PhysicsPickupTarget target,
                WorldEntityStateDto state,
                string cellId,
                bool detectCollisions,
                RigidbodyInterpolation interpolation)
            {
                Target = target;
                State = state;
                CellId = cellId ?? string.Empty;
                DetectCollisions = detectCollisions;
                Interpolation = interpolation;
            }

            public PhysicsPickupTarget Target { get; }
            public WorldEntityStateDto State { get; set; }
            public string CellId { get; }
            public bool DetectCollisions { get; }
            public RigidbodyInterpolation Interpolation { get; }
        }

        internal sealed class SceneRegistrationBatch
        {
            private readonly List<PhysicsPickupTarget> targets =
                new List<PhysicsPickupTarget>();
            private readonly List<RigidbodyRestorePlan> restorePlans =
                new List<RigidbodyRestorePlan>();
            private readonly List<Action> completionActions =
                new List<Action>();

            public int TargetCount => targets.Count;

            public void Add(PhysicsPickupTarget target)
            {
                if (target != null && target.Body != null &&
                    !targets.Contains(target))
                {
                    targets.Add(target);
                }
            }

            public PhysicsPickupTarget GetTarget(int index) => targets[index];

            public void SortTargetsByStableId()
            {
                targets.Sort((left, right) => string.Compare(
                    left != null && left.StableId.IsValid
                        ? left.StableId.Value
                        : string.Empty,
                    right != null && right.StableId.IsValid
                        ? right.StableId.Value
                        : string.Empty,
                    StringComparison.Ordinal));
            }

            public void FreezeAll()
            {
                restorePlans.Clear();
                for (int index = 0; index < targets.Count; index++)
                {
                    PhysicsPickupTarget target = targets[index];
                    RigidbodyRestorePlan plan =
                        RigidbodyRestorePlan.Capture(target);
                    restorePlans.Add(plan);
                    plan.Freeze();
                }
            }

            public void ApplyDeferredState(
                PhysicsPickupTarget target,
                WorldEntityStateDto state)
            {
                for (int index = 0; index < restorePlans.Count; index++)
                {
                    if (restorePlans[index].Target != target)
                    {
                        continue;
                    }

                    restorePlans[index].ApplyDeferredState(state);
                    return;
                }

                throw new InvalidOperationException(
                    "A deferred world entity was not frozen by its scene batch.");
            }

            public void HoldDeferredState(
                PhysicsPickupTarget target,
                WorldEntityStateDto state,
                out bool detectCollisions,
                out RigidbodyInterpolation interpolation)
            {
                for (int index = 0; index < restorePlans.Count; index++)
                {
                    if (restorePlans[index].Target != target)
                    {
                        continue;
                    }

                    restorePlans[index].ApplyDeferredState(state);
                    restorePlans[index].HoldForMissingSupport(
                        out detectCollisions,
                        out interpolation);
                    return;
                }

                throw new InvalidOperationException(
                    "A deferred world entity was not frozen by its scene batch.");
            }

            public void Discard(PhysicsPickupTarget target)
            {
                for (int index = 0; index < restorePlans.Count; index++)
                {
                    if (restorePlans[index].Target == target)
                    {
                        restorePlans[index].Discard();
                        return;
                    }
                }
            }

            public void ApplySuspendedState(
                PhysicsPickupTarget target,
                WorldEntityStateDto state,
                bool detectCollisions,
                RigidbodyInterpolation interpolation)
            {
                for (int index = 0; index < restorePlans.Count; index++)
                {
                    if (restorePlans[index].Target != target)
                    {
                        continue;
                    }

                    restorePlans[index].ApplySuspendedState(
                        state,
                        detectCollisions,
                        interpolation);
                    return;
                }

                throw new InvalidOperationException(
                    "A suspended world entity was not frozen by its scene batch.");
            }

            public void AddCompletion(Action completion)
            {
                if (completion != null)
                {
                    completionActions.Add(completion);
                }
            }

            public void RestoreCollisionsAll()
            {
                for (int index = 0; index < restorePlans.Count; index++)
                {
                    restorePlans[index].RestoreCollisions();
                }
            }

            public void ReleaseDynamicsAll()
            {
                for (int index = 0; index < restorePlans.Count; index++)
                {
                    restorePlans[index].ReleaseDynamics();
                }
            }

            public void Complete()
            {
                for (int index = 0; index < completionActions.Count; index++)
                {
                    completionActions[index]();
                }
            }
        }

        private sealed class RigidbodyRestorePlan
        {
            private RigidbodyRestorePlan(
                PhysicsPickupTarget target,
                Vector3 position,
                Quaternion rotation,
                Vector3 linearVelocity,
                Vector3 angularVelocity,
                bool isKinematic,
                bool useGravity,
                bool sleeping,
                bool activeSelf,
                bool detectCollisions,
                RigidbodyInterpolation interpolation)
            {
                Target = target;
                Position = position;
                Rotation = rotation;
                LinearVelocity = linearVelocity;
                AngularVelocity = angularVelocity;
                IsKinematic = isKinematic;
                UseGravity = useGravity;
                Sleeping = sleeping;
                ActiveSelf = activeSelf;
                DetectCollisions = detectCollisions;
                Interpolation = interpolation;
            }

            public PhysicsPickupTarget Target { get; }
            private Vector3 Position { get; set; }
            private Quaternion Rotation { get; set; }
            private Vector3 LinearVelocity { get; set; }
            private Vector3 AngularVelocity { get; set; }
            private bool IsKinematic { get; set; }
            private bool UseGravity { get; set; }
            private bool Sleeping { get; set; }
            private bool ActiveSelf { get; set; }
            private bool DetectCollisions { get; set; }
            private RigidbodyInterpolation Interpolation { get; set; }
            private bool IsDiscarded { get; set; }
            private bool IsHeldForMissingSupport { get; set; }

            public static RigidbodyRestorePlan Capture(
                PhysicsPickupTarget target)
            {
                Rigidbody body = target.Body;
                return new RigidbodyRestorePlan(
                    target,
                    body.position,
                    body.rotation,
                    body.isKinematic ? Vector3.zero : body.linearVelocity,
                    body.isKinematic ? Vector3.zero : body.angularVelocity,
                    body.isKinematic,
                    body.useGravity,
                    !body.isKinematic && body.IsSleeping(),
                    target.gameObject.activeSelf,
                    body.detectCollisions,
                    body.interpolation);
            }

            public void Freeze()
            {
                Rigidbody body = Target.Body;
                body.interpolation = RigidbodyInterpolation.None;
                body.detectCollisions = false;
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                body.isKinematic = true;
            }

            public void ApplyDeferredState(WorldEntityStateDto state)
            {
                Rigidbody body = Target.Body;
                Position = state.worldPosition;
                Rotation = state.worldRotation.normalized;
                LinearVelocity = state.linearVelocity;
                AngularVelocity = state.angularVelocity;
                IsKinematic = state.isKinematic;
                UseGravity = Target.UsesGravityWhenLoose || state.useGravity;
                Sleeping = state.sleeping &&
                    !(Target.UsesGravityWhenLoose && !state.useGravity);
                ActiveSelf = state.activeSelf;
                Target.gameObject.SetActive(ActiveSelf);
                body.position = Position;
                body.rotation = Rotation;
                body.transform.SetPositionAndRotation(Position, Rotation);
            }

            public void Discard()
            {
                IsDiscarded = true;
            }

            public void HoldForMissingSupport(
                out bool detectCollisions,
                out RigidbodyInterpolation interpolation)
            {
                IsHeldForMissingSupport = true;
                detectCollisions = DetectCollisions;
                interpolation = Interpolation;
            }

            public void ApplySuspendedState(
                WorldEntityStateDto state,
                bool detectCollisions,
                RigidbodyInterpolation interpolation)
            {
                ApplyDeferredState(state);
                DetectCollisions = detectCollisions;
                Interpolation = interpolation;
            }

            public void RestoreCollisions()
            {
                // Registration can intentionally discard a freshly streamed
                // canonical clone when the authoritative entity was rehomed
                // into the persistent scene. Its pre-registration freeze plan
                // remains in this batch, but there is no Rigidbody left to
                // restore after DestroyCanonicalClone.
                if (IsDiscarded || IsHeldForMissingSupport ||
                    Target == null || Target.Body == null)
                {
                    return;
                }

                Rigidbody body = Target.Body;
                Target.gameObject.SetActive(ActiveSelf);
                body.position = Position;
                body.rotation = Rotation;
                body.transform.SetPositionAndRotation(Position, Rotation);
                body.useGravity = UseGravity;
                body.isKinematic = true;
                body.detectCollisions = DetectCollisions;
            }

            public void ReleaseDynamics()
            {
                if (IsDiscarded || IsHeldForMissingSupport ||
                    Target == null || Target.Body == null)
                {
                    return;
                }

                Rigidbody body = Target.Body;
                body.isKinematic = IsKinematic;
                if (!IsKinematic)
                {
                    body.linearVelocity = LinearVelocity;
                    body.angularVelocity = AngularVelocity;
                    if (Sleeping)
                    {
                        body.Sleep();
                    }
                    else
                    {
                        body.WakeUp();
                    }
                }

                body.interpolation = Interpolation;
            }
        }

        private static void ApplyToLoadedTarget(
            PhysicsPickupTarget target,
            WorldEntityStateDto state)
        {
            if (target == null || target.Body == null ||
                !string.Equals(
                    target.StableId.Value,
                    state.stableEntityId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "World entity restore target no longer matches its stable ID.");
            }

            Rigidbody body = target.Body;
            bool repairedLooseGravity =
                target.UsesGravityWhenLoose && !state.useGravity;
            RigidbodyInterpolation interpolation = body.interpolation;
            bool detectCollisions = body.detectCollisions;
            body.interpolation = RigidbodyInterpolation.None;
            body.detectCollisions = false;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.isKinematic = true;
            target.gameObject.SetActive(state.activeSelf);
            body.position = state.worldPosition;
            body.rotation = state.worldRotation.normalized;
            body.transform.SetPositionAndRotation(
                state.worldPosition,
                state.worldRotation.normalized);
            Physics.SyncTransforms();

            body.useGravity = target.UsesGravityWhenLoose || state.useGravity;
            body.detectCollisions = detectCollisions;
            Physics.SyncTransforms();
            body.isKinematic = state.isKinematic;
            if (!state.isKinematic)
            {
                body.linearVelocity = state.linearVelocity;
                body.angularVelocity = state.angularVelocity;
                if (state.sleeping && !repairedLooseGravity)
                {
                    body.Sleep();
                }
                else
                {
                    body.WakeUp();
                }
            }

            body.interpolation = interpolation;
            // Do not write Transform/Rigidbody pose after Sleep(). Unity treats
            // that write as motion and wakes an otherwise sleeping checkpoint.
            // The guarded pose was already synchronized before dynamics were
            // released; this final sync completes the release barrier only.
            Physics.SyncTransforms();
        }

        private static DeferredStableEntityPayload ToDeferred(
            WorldEntityStateDto state) => new DeferredStableEntityPayload
        {
            StableEntityId = state.stableEntityId,
            OwnerDomainId = DomainId,
            SchemaVersion = WorldEntityDomainSaveDto.CurrentSchemaVersion,
            PayloadJson = SaveParticipantJson.Serialize(state),
        };

        private void RemoveDeferredOwnerState(
            DeferredStableEntityStore store)
        {
            DeferredStableEntityPayload[] snapshot = store.Snapshot();
            for (int index = 0; index < snapshot.Length; index++)
            {
                DeferredStableEntityPayload payload = snapshot[index];
                if (string.Equals(
                        payload.OwnerDomainId,
                        DomainId,
                        StringComparison.Ordinal))
                {
                    ReleaseSourceCellRetention(payload.StableEntityId);
                    store.TryTake(
                        DomainId,
                        payload.StableEntityId,
                        out _);
                }
            }
        }
    }
}
