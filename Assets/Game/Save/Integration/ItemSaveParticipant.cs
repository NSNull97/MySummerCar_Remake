using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MSC.Items;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MSC.Save.Integration
{
    internal sealed class ItemSaveParticipant : ISaveParticipant
    {
        public const string DomainId = "items.instances";

        private readonly ItemWorldRuntime runtime;
        private readonly DeferredStableEntityStore deferredEntities;
        private Func<string, bool> assemblyOwnerUnavailable;

        internal void ConfigureAssemblyOwnerAvailability(Func<string, bool> unavailable) => assemblyOwnerUnavailable = unavailable;

        public ItemSaveParticipant(
            ItemWorldRuntime itemRuntime,
            DeferredStableEntityStore deferredStore)
        {
            runtime = itemRuntime ??
                throw new ArgumentNullException(nameof(itemRuntime));
            deferredEntities = deferredStore ??
                throw new ArgumentNullException(nameof(deferredStore));
            if (!runtime.IsInitialized || runtime.Definitions == null)
            {
                throw new InvalidOperationException(
                    "Item save participant requires an initialized item runtime.");
            }

            Descriptor = new SaveParticipantDescriptor(
                DomainId,
                ItemDomainSaveDto.CurrentSchemaVersion,
                required: true,
                SaveRestorePhase.ItemInstances);
        }

        public SaveParticipantDescriptor Descriptor { get; }

        public string CapturePayload() =>
            SaveParticipantJson.Serialize(CaptureDomainState());

        public object PrepareRestore(
            SaveDomainEnvelope envelope,
            SaveRestorePreparationContext context)
        {
            ItemDomainSaveDto state =
                SaveParticipantJson.Deserialize<ItemDomainSaveDto>(
                    envelope.PayloadJson);
            NormalizeLegacyFoodState(state);
            if (!state.TryValidate(runtime.Definitions, out string failure))
            {
                throw new InvalidDataException(
                    "Item save preflight failed: " + failure);
            }

            return state;
        }

        internal void NormalizeLegacyFoodState(ItemDomainSaveDto domain)
        {
            if (domain?.instances == null)
            {
                return;
            }

            for (int recordIndex = 0;
                 recordIndex < domain.instances.Length;
                 recordIndex++)
            {
                ItemInstanceState state = domain.instances[recordIndex]?.state;
                if (state == null ||
                    !runtime.Definitions.TryGet(
                        state.definitionId,
                        out ItemDefinitionRecord definition))
                {
                    continue;
                }

                if (string.Equals(
                        definition.DefinitionId,
                        "item.sausages-package",
                        StringComparison.Ordinal) &&
                    (state.content > definition.MaximumContent + 0.0001f ||
                     state.containedStableIds == null ||
                     state.containedStableIds.Length !=
                        Mathf.RoundToInt(Mathf.Clamp(
                            state.content,
                            0f,
                            definition.MaximumContent))))
                {
                    int remaining = state.content > definition.MaximumContent
                        ? Mathf.Clamp(
                            Mathf.CeilToInt(state.content / 25f),
                            0,
                            definition.InitialChildCount)
                        : Mathf.Clamp(
                            Mathf.RoundToInt(state.content),
                            0,
                            definition.InitialChildCount);
                    int consumed = definition.InitialChildCount - remaining;
                    state.containedStableIds = Enumerable.Range(
                            consumed,
                            remaining)
                        .Select(index =>
                            ItemStableIdUtility.CreateDeterministic(
                                $"{state.stableEntityId}|contained|{index:000}")
                            .Value)
                        .ToArray();
                    state.content = remaining;
                }

                if (definition.Food.IsConfigured &&
                    !state.foodSimulationInitialized)
                {
                    state.foodSimulationInitialized = true;
                    state.lastFoodSimulationGameSeconds =
                        runtime.CurrentGameTimeSeconds;
                }
            }
        }

        public object CaptureCheckpoint() => CaptureDomainState();

        public void ApplyPreparedRestore(
            object preparedState,
            SaveRestoreContext context)
        {
            ItemDomainSaveDto state = (ItemDomainSaveDto)preparedState;
            RemoveDeferredOwnerState(context.DeferredEntities);
            runtime.RemoveDynamicInstancesExcept(new HashSet<string>(
                state.instances
                    .Where(record => !ShouldArchiveUntilSourceCellLoads(record))
                    .Select(record => record.state.stableEntityId),
                StringComparer.Ordinal));
            for (int index = 0; index < state.instances.Length; index++)
            {
                ItemRuntimeSaveRecord record = state.instances[index];
                if (ShouldArchiveUntilSourceCellLoads(record))
                {
                    context.DeferredEntities.Enqueue(ToDeferred(record));
                    context.UnresolvedContent.Add(
                        DomainId,
                        record.state.stableEntityId,
                        UnresolvedContentReason.DeferredUntilCellLoad,
                        "Dynamic item state is archived until its owning world " +
                        "cell loads; no off-cell Rigidbody is materialized.");
                    continue;
                }

                WorldItemInstance instance = ResolveOrMaterialize(record);
                if (instance != null)
                {
                    instance.ApplyState(
                        record.state,
                        record.materializationPosition);
                    continue;
                }

                context.DeferredEntities.Enqueue(ToDeferred(record));
                context.UnresolvedContent.Add(
                    DomainId,
                    record.state.stableEntityId,
                    UnresolvedContentReason.DeferredUntilCellLoad,
                    "Canonical item state is retained until its owning world " +
                    "cell materializes the project wrapper.");
            }
        }

        public void Rollback(object checkpoint)
        {
            ItemDomainSaveDto captured = (ItemDomainSaveDto)checkpoint;
            var retainedIds = new HashSet<string>(
                captured.instances
                    .Where(record => !ShouldArchiveUntilSourceCellLoads(record))
                    .Select(record => record.state.stableEntityId),
                StringComparer.Ordinal);
            runtime.RemoveDynamicInstancesExcept(retainedIds);
            for (int index = 0; index < captured.instances.Length; index++)
            {
                ItemRuntimeSaveRecord record = captured.instances[index];
                if (ShouldArchiveUntilSourceCellLoads(record))
                {
                    // The coordinator applies against a staged deferred store.
                    // On failure the original deferred checkpoint is untouched,
                    // so rollback must not create an off-cell Rigidbody for it.
                    continue;
                }

                WorldItemInstance instance = ResolveOrMaterialize(record);
                if (instance != null)
                {
                    instance.ApplyState(
                        record.state,
                        record.materializationPosition);
                }
            }
        }

        public void RegisterScene(Scene scene)
        {
            runtime.TryGetCellIdForScene(scene, out string sourceCellId);
            RegisterSceneForCell(scene, sourceCellId);
        }

        internal void RegisterSceneForCell(Scene scene, string sourceCellId)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            MaterializeArchivedDynamicInstances(
                scene,
                sourceCellId ?? string.Empty);

            foreach (WorldItemInstance instance in FindSceneInstances(scene))
            {
                string id = instance.StableId.Value;
                if (!deferredEntities.TryPeek(
                        DomainId,
                        id,
                        out DeferredStableEntityPayload pending))
                {
                    continue;
                }

                ItemRuntimeSaveRecord record =
                    SaveParticipantJson.Deserialize<ItemRuntimeSaveRecord>(
                        pending.PayloadJson);
                if (!record.TryValidate(
                        runtime.Definitions,
                        out string deferredFailure) ||
                    !string.Equals(
                        record.state.stableEntityId,
                        id,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Deferred item state '{id}' is invalid: {deferredFailure}");
                }

                instance.ApplyState(
                    record.state,
                    record.materializationPosition);
                deferredEntities.TryTake(DomainId, id, out _);
            }
        }

        public void ReconcileAfterSceneUnload(Scene unloadedScene)
        {
            if (!unloadedScene.IsValid())
            {
                return;
            }

            foreach (DeferredStableEntityPayload payload in deferredEntities.Snapshot())
            {
                if (!string.Equals(
                        payload.OwnerDomainId,
                        DomainId,
                        StringComparison.Ordinal) ||
                    !runtime.TryGetInstance(
                        payload.StableEntityId,
                        out WorldItemInstance loaded) ||
                    loaded.gameObject.scene.handle == unloadedScene.handle)
                {
                    continue;
                }

                // The world-entity owner moved this live item to the persistent
                // scene. Its loaded state is authoritative over the earlier
                // pre-unload archive taken from the source scene.
                deferredEntities.TryTake(DomainId, payload.StableEntityId, out _);
            }
        }

        public void CaptureScene(Scene scene)
        {
            if (!scene.IsValid())
            {
                return;
            }

            foreach (WorldItemInstance instance in FindSceneInstances(scene))
            {
                ItemRuntimeSaveRecord record = CaptureInstance(instance);
                deferredEntities.Enqueue(
                    ToDeferred(record),
                    replaceExisting: true);
            }
        }

        private ItemDomainSaveDto CaptureDomainState()
        {
            var byId = new Dictionary<string, ItemRuntimeSaveRecord>(
                StringComparer.Ordinal);
            foreach (DeferredStableEntityPayload payload in
                     deferredEntities.Snapshot())
            {
                if (!string.Equals(
                        payload.OwnerDomainId,
                        DomainId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ItemRuntimeSaveRecord record =
                    SaveParticipantJson.Deserialize<ItemRuntimeSaveRecord>(
                        payload.PayloadJson);
                if (!record.TryValidate(
                        runtime.Definitions,
                        out string pendingFailure) ||
                    !string.Equals(
                        record.state.stableEntityId,
                        payload.StableEntityId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Deferred item state is invalid: " + pendingFailure);
                }

                byId.Add(record.state.stableEntityId, record);
            }

            foreach (WorldItemInstance instance in runtime.LoadedInstances)
            {
                if (instance != null)
                {
                    byId[instance.StableId.Value] = CaptureInstance(instance);
                }
            }

            var state = new ItemDomainSaveDto
            {
                instances = byId.Values
                    .OrderBy(
                        record => record.state.stableEntityId,
                        StringComparer.Ordinal)
                    .ToArray(),
            };
            if (!state.TryValidate(runtime.Definitions, out string failure))
            {
                throw new InvalidDataException(
                    "Captured item state is invalid: " + failure);
            }

            return state;
        }

        private ItemRuntimeSaveRecord CaptureInstance(
            WorldItemInstance instance)
        {
            Rigidbody body = instance.GetComponent<Rigidbody>();
            // Consumed or parent-disabled wrappers can remain registered. Their
            // inactive Unity 6000.6 actor getters do not retain the saved pose.
            bool hasActiveBody = body != null && body.gameObject.activeInHierarchy;
            Vector3 position = hasActiveBody
                ? body.position
                : instance.transform.position;
            Quaternion rotation = hasActiveBody
                ? body.rotation
                : instance.transform.rotation;
            string id = instance.StableId.Value;
            return new ItemRuntimeSaveRecord
            {
                state = instance.CaptureState(),
                isCanonicalPlacement = runtime.IsCanonicalPlacement(id),
                sourceCellId = runtime.TryGetSourceCellId(
                        id,
                        out string sourceCellId)
                    ? sourceCellId
                    : string.Empty,
                materializationPosition = position,
                materializationRotation = rotation,
            };
        }

        private WorldItemInstance ResolveOrMaterialize(
            ItemRuntimeSaveRecord record)
        {
            if (runtime.TryGetInstance(
                    record.state.stableEntityId,
                    out WorldItemInstance instance))
            {
                return instance;
            }

            if (!record.isCanonicalPlacement &&
                runtime.IsPositionInCell(
                    record.materializationPosition,
                    record.sourceCellId) &&
                runtime.TryGetLoadedCellScene(
                    record.sourceCellId,
                    out Scene loadedSourceScene))
            {
                return runtime.MaterializeForRestore(record, loadedSourceScene);
            }

            return runtime.MaterializeForRestore(record);
        }

        private bool ShouldArchiveUntilSourceCellLoads(
            ItemRuntimeSaveRecord record)
        {
            if (runtime.IsExternallyOwned(record.state.stableEntityId))
                return assemblyOwnerUnavailable?.Invoke(record.state.stableEntityId) == true;
            return !record.isCanonicalPlacement &&
                   runtime.IsPositionInCell(
                       record.materializationPosition,
                       record.sourceCellId) &&
                   !runtime.TryGetLoadedCellScene(record.sourceCellId, out _);
        }

        private void MaterializeArchivedDynamicInstances(
            Scene scene,
            string sourceCellId)
        {
            if (string.IsNullOrEmpty(sourceCellId))
            {
                return;
            }

            foreach (DeferredStableEntityPayload payload in deferredEntities.Snapshot())
            {
                if (!string.Equals(
                        payload.OwnerDomainId,
                        DomainId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ItemRuntimeSaveRecord record =
                    SaveParticipantJson.Deserialize<ItemRuntimeSaveRecord>(
                        payload.PayloadJson);
                if (runtime.IsExternallyOwned(record?.state?.stableEntityId ?? string.Empty)) continue;
                if (!record.TryValidate(
                        runtime.Definitions,
                        out string failure) ||
                    !string.Equals(
                        record.state.stableEntityId,
                        payload.StableEntityId,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Deferred item state '{payload.StableEntityId}' is invalid: {failure}");
                }

                if (runtime.TryGetInstance(
                        record.state.stableEntityId,
                        out WorldItemInstance loaded))
                {
                    if (loaded.gameObject.scene.handle != scene.handle)
                    {
                        // A carried or otherwise rehomed instance survived the
                        // source-cell unload; do not rewind it to the archive.
                        deferredEntities.TryTake(
                            DomainId,
                            record.state.stableEntityId,
                            out _);
                    }

                    continue;
                }

                if (record.isCanonicalPlacement ||
                    !string.Equals(
                        record.sourceCellId,
                        sourceCellId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                runtime.MaterializeForRestore(record, scene);
            }
        }

        private static WorldItemInstance[] FindSceneInstances(Scene scene)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root =>
                    root.GetComponentsInChildren<WorldItemInstance>(true))
                .ToArray();
        }

        private static DeferredStableEntityPayload ToDeferred(
            ItemRuntimeSaveRecord record) => new DeferredStableEntityPayload
        {
            StableEntityId = record.state.stableEntityId,
            OwnerDomainId = DomainId,
            SchemaVersion = ItemDomainSaveDto.CurrentSchemaVersion,
            PayloadJson = SaveParticipantJson.Serialize(record),
        };

        private static void RemoveDeferredOwnerState(
            DeferredStableEntityStore store)
        {
            foreach (DeferredStableEntityPayload payload in store.Snapshot())
            {
                if (string.Equals(
                        payload.OwnerDomainId,
                        DomainId,
                        StringComparison.Ordinal))
                {
                    store.TryTake(
                        DomainId,
                        payload.StableEntityId,
                        out _);
                }
            }
        }
    }
}
